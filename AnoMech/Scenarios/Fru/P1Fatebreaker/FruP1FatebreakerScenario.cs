using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Combat;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P1Fatebreaker.FruP1FatebreakerState;
using Actions = AnoMech.Scenarios.Fru.FruP1Actions;

namespace AnoMech.Scenarios.Fru.P1Fatebreaker;

// All of P1 as one fight on one Fatebreaker, up to the boss's defeat or enrage. The timeline is the
// median of three pulls of one replay, from the first Cyclonic Break cast at 7.2s; the positions
// where the mechanic leaves only a direction are chosen, UNVERIFIED. Clearing it ends this stage only,
// never the duty. There is no enmity table: the boss's target moves only by a valid Provoke or on its
// holder's death (FruP1Enmity), whichever strat is playing.
public sealed class FruP1FatebreakerScenario : IDutyScenario
{
    private const float EnrageFailsAt = 162.15f;
    private const float RestConeInterval = 2.06f;
    private const float BoundResolveAt = 78.66f;
    private const float BoundTetherAt = 64.63f;
    private const float BoundFetterAt = 75.24f;
    private const float BowShockDelay = 0.31f;
    private const float WarpOutSeconds = 1.3f;
    private const float BoundChargeLock = 3.1f;
    private const byte BoundFettersModelState = 28;
    private const float UtopianFogSeconds = 3f;
    private const ushort RestingPoseTimeline = 4575;
    private const ushort RaisedPoseTimeline = 4576;
    private static readonly float RestConeTurn = -22.5f * MathF.PI / 180f;

    // The replay's swings: the boss autos only between its own casts and while it is on the field.
    private static readonly float[] AutoAttackTimes = [0.07f, 3.09f, 6.13f, 26.55f, 29.59f, 81.18f, 89.19f, 92.19f, 115.51f, 123.54f, 132.47f, 145.71f, 148.74f];
    // UNVERIFIED: a bleed nothing has measured.
    private const float BleedTickSeconds = 3f;
    private const uint BleedTickAmount = 2_000;
    private const float DrillLeadIn = 3f;

    // UNVERIFIED, every value; a prototype on the same footing as DutyTuning's own defaults.
    internal static readonly DutyTuning DefaultTuning = new()
    {
        BossMaxHp = 300_000,
        HitOverrides = new Dictionary<uint, uint>
        {
            [ActionId.FatebreakerAutoAttack] = 8_000,
            [ActionId.FatedBurnMark] = 150_000,
        },
    };

    public string Name => "Fatebreaker (JP; experimental)";
    public IPhase Phase => FruZone.P1;
    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP1FatebreakerJpAi(drill)];
    public object SettingsOverrides => Overrides;

    internal FruP1FatebreakerStateOverrides Overrides { get; } = new();
    internal DutyTuning Tuning { get; set; } = DefaultTuning;
    internal FruP1FatebreakerState? State => state;
    internal SimCharacter? BossTarget => targeting?.Top;

    private readonly FruP1Drill? drill;
    private bool drillCleared;
    private SimWorld world = null!;
    private FruP1FatebreakerState state = null!;
    private IScenarioAi<FruP1FatebreakerState>? strat;
    private DutyCombat? combat;
    private SimEnemy? boss;
    private SimEnemy? centerImage;
    private SimEnemy? westImage;
    private SimEnemy? northImage;
    private SimEnemy? innerNorthImage;
    private FruP1Enmity? targeting;
    private DutyPeriodicDamage? periodic;
    private bool actedSubscribed;
    private readonly List<SimEnemy> skyImages = [];
    private readonly List<SimEnemy> halos = [];
    private readonly SimEnemy?[] faithSources = new SimEnemy?[FaithTetherCount];
    private readonly SimCharacter?[] boundHolders = new SimCharacter?[2];
    private readonly SimEnemy?[] boundImages = new SimEnemy?[2];

    public FruP1FatebreakerScenario() : this(null)
    {
    }

    internal FruP1FatebreakerScenario(FruP1Drill? drill) => this.drill = drill;

    public bool VictoryReached => drillCleared || combat is { BossDefeated: true };

    public void DrawSettings()
        => ImGui.TextWrapped("Experimental P1 stage, not the full duty. Its timeline, positions, HP and damage are uncalibrated prototype values. "
                             + "There is no enmity table: Provoke is the one action that moves the boss's target.");

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        OnRunEnded();
        state = new FruP1FatebreakerState(world.Rng, Overrides);
        combat = new DutyCombat(world, Overrides.BossMaxHp is { } bossMaxHp ? Tuning with { BossMaxHp = bossMaxHp } : Tuning);
        world.Combat = combat;
        targeting = new FruP1Enmity(world, combat);
        state.Targeting = targeting;
        periodic = new DutyPeriodicDamage(combat, world.Party, [new DutyDot(StatusId.Bleeding, BleedTickAmount, DamageKind.Magic)], BleedTickSeconds);
        boss = centerImage = westImage = northImage = innerNorthImage = null;
        skyImages.Clear();
        halos.Clear();
        Array.Clear(faithSources);
        Array.Clear(boundHolders);
        Array.Clear(boundImages);
        drillCleared = false;
        Natives.UserActions.Acted += OnPlayerActed;
        actedSubscribed = true;
        NoteUnsupportedPlayerJob();

        strat = null;
        if (selectedAi is { } idx && idx < AiStrats.Count)
        {
            strat = (IScenarioAi<FruP1FatebreakerState>)AiStrats[idx];
            strat.Run(state, world);
        }

        if (drill is { } practiced) ScheduleDrill(practiced);
        else ScheduleFullFight();
    }

    private void ScheduleFullFight()
    {
        ScheduleBossArrival(0f);
        ScheduleAutoAttacks(0f, 151f);

        ScheduleBossCyclonicBreak();
        ScheduleFirstPowderMark();

        ScheduleUtopianSky();
        ScheduleImageCyclonicBreak();
        ScheduleTurnOfTheHeavens();
        ScheduleBoundOfFaith();
        ScheduleBossReturn();

        ScheduleFallOfFaith();

        ScheduleExplosions();

        world.Events.Add(150.85f, () => boss?.Cast(Actions.Enrage));
        world.Events.Add(EnrageFailsAt, FailIfEnrageSpared);
    }

    // Keeps the fight's clock and starts it just short of the group, so every absolute time and
    // duration still holds.
    private void ScheduleDrill(FruP1Drill practiced)
    {
        switch (practiced)
        {
            case FruP1Drill.CyclonicBreakBoss:
                ScheduleBossCyclonicBreak();
                ScheduleFirstPowderMark();
                StartDrill(DrillLeadIn, clearAt: 27.65f);
                break;
            case FruP1Drill.UtopianSky:
                ScheduleFirstPowderMark();
                ScheduleUtopianSky();
                ScheduleImageCyclonicBreak();
                StartDrill(19.2f, clearAt: 63.48f);
                break;
            case FruP1Drill.TurnOfTheHeavens:
                ScheduleTurnOfTheHeavens();
                ScheduleBoundOfFaith();
                ScheduleBossReturn();
                StartDrill(56.6f, clearAt: 81.82f);
                break;
            case FruP1Drill.FallOfFaith:
                ScheduleFallOfFaith();
                StartDrill(81.26f, clearAt: 137.61f);
                break;
            case FruP1Drill.Explosion:
                ScheduleExplosions();
                StartDrill(125.11f, clearAt: 148.75f);
                break;
        }
    }

    private void StartDrill(float firstCue, float clearAt)
    {
        var start = firstCue - DrillLeadIn;
        ScheduleBossArrival(start);
        ScheduleAutoAttacks(start, clearAt);
        world.Events.Add(clearAt, () => drillCleared = true);
        world.Events.Advance(start);
    }

    private void ScheduleBossArrival(float at)
    {
        world.Events.Add(at, SpawnBoss);
        world.Events.Add(at + 1f, TargetEnmityStandIn);
    }

    private void ScheduleAutoAttacks(float from, float until)
    {
        foreach (var at in AutoAttackTimes)
            if (at >= from && at < until)
                world.Events.Add(at, SwingAtTarget);
    }

    private void ScheduleFirstPowderMark() => world.Events.Add(19.2f, CastPowderMark);

    private void ScheduleBossReturn()
    {
        world.Events.Add(80.02f, () =>
        {
            boss?.PlayActionTimeline(ActionTimelineId.WarpEnd);
            boss?.SetTargetable(true);
            TargetEnmityStandIn();
        });
    }

    private void ScheduleExplosions()
    {
        world.Events.Add(125.11f, CastPowderMark);
        world.Events.Add(135.19f, StartExplosions);
    }

    private void ScheduleBossCyclonicBreak()
    {
        world.Events.Add(7.2f, () => boss?.Cast(state.FirstCyclonicPairs ? ActionId.CyclonicBreakBossStack : ActionId.CyclonicBreakBossSpread));
        world.Events.Add(14.3f, () => StartCyclonicCones(boss));
        world.Events.Add(16.4f, () => ResolveCyclonicSplit(state.FirstCyclonicPairs));
    }

    private void ScheduleUtopianSky()
    {
        world.Events.Add(30.8f, () => boss?.Cast(state.UtopianStack ? Actions.UtopianSkyStack : Actions.UtopianSkySpread));
        world.Events.Add(34.8f, () =>
        {
            boss?.SetTargetable(false);
            world.Map.SetWeather(WeatherId.P1Fog, UtopianFogSeconds);
        });
        world.Events.Add(37.45f, SpawnSkyImages);
        world.Events.Add(37.8f, () => skyImages.ForEach(image => image.AddStatus(StatusId.Concealed)));
        world.Events.Add(37.96f, () => WarpIn(skyImages.ToArray()));
        world.Events.Add(40.04f, ShowBlastingZoneBars);
        world.Events.Add(39.96f, RaiseBlastingImages);
        world.Events.Add(43.06f, PoseSkyImages);
        world.Events.Add(49f, RevealBlastingImages);
        world.Events.Add(49.13f, FireBlastingZones);
        world.Events.Add(49.93f, ResolveSinbound);
        world.Events.Add(49.85f, () => SetSkyModelState(0));
        world.Events.Add(51.53f, () => world.Map.SetWeather(WeatherId.P1, UtopianFogSeconds));
        world.Events.Add(51.51f, ExitSkyImages);
    }

    private void ScheduleImageCyclonicBreak()
    {
        world.Events.Add(46.9f, SpawnCenterImage);
        world.Events.Add(47.46f, () => WarpIn(centerImage));
        world.Events.Add(48.58f, () => centerImage?.Cast(state.ImageCyclonicPairs ? ActionId.CyclonicBreakImageStack : ActionId.CyclonicBreakImageSpread));
        world.Events.Add(56.16f, () => StartCyclonicCones(centerImage));
        world.Events.Add(58.25f, () => ResolveCyclonicSplit(state.ImageCyclonicPairs));
        world.Events.Add(58.98f, () => centerImage?.PlayActionTimeline(ActionTimelineId.WarpStart));
        // UNVERIFIED: the replay shows no fade on this image; it keeps its old gap after the warp.
        world.Events.Add(63.28f, () => centerImage?.FadeOut());
    }

    private void ScheduleTurnOfTheHeavens()
    {
        world.Events.Add(55f, SpawnHeavensImages);
        world.Events.Add(55.51f, () => WarpIn(westImage, innerNorthImage));
        world.Events.Add(56.6f, StartHeavensLightning);
        world.Events.Add(56.6f, () => innerNorthImage?.Cast(state.LargeIsFire ? Actions.TurnOfHeavensFire : Actions.TurnOfHeavensLightning));
        world.Events.Add(61.04f, () => northImage = SpawnImage(EdgeImage(NorthSpot), SpawnVisibility.HiddenUntilShown));
        world.Events.Add(61.54f, () => WarpIn(northImage));
        world.Events.Add(62.64f, StartHeavensFire);
        world.Events.Add(63.78f, SpawnHalos);
        world.Events.Add(66.07f, () => WarpOut(innerNorthImage));
        world.Events.Add(66.68f, CastBrightfire);
        world.Events.Add(68.71f, () => WarpOut(westImage));
        world.Events.Add(74.76f, () => WarpOut(northImage));
        world.Events.Add(76.74f, () => FadeOut(halos));
    }

    private void ScheduleBoundOfFaith()
    {
        world.Events.Add(63f, SpawnBoundImages);
        world.Events.Add(63.5f, () => WarpIn(boundImages));
        world.Events.Add(BoundTetherAt, MarkBoundTargets);
        world.Events.Add(BoundTetherAt, TetherBoundTargets);
        world.Events.Add(BoundTetherAt, () =>
        {
            foreach (var image in boundImages) image?.Cast(Actions.BoundOfFaith);
        });
        world.Events.Add(BoundFetterAt, FetterBoundTargets);
        world.Events.Add(75.76f, () => SetBoundModelState(BoundFettersModelState));
        world.Events.Add(77.28f, ChargeBoundTargets);
        world.Events.Add(BoundResolveAt, ResolveBoundOfFaith);
        world.Events.Add(78.82f, () => SetBoundModelState(0));
        world.Events.Add(80.37f, () =>
        {
            foreach (var image in boundImages) WarpOut(image);
        });
    }

    private void ScheduleFallOfFaith()
    {
        world.Events.Add(81.26f, () => boss?.Cast(Actions.BurnishedGlory));
        world.Events.Add(92.13f, SpawnFaithImages);
        world.Events.Add(92.63f, () => TetherFaithTarget(0, 9.75f, 13.18f));
        world.Events.Add(92.74f, () => CastFallOfFaith(0));
        world.Events.Add(94.7f, () => WarpIn(faithSources[1]));
        world.Events.Add(95.76f, () => TetherFaithTarget(1, 9.72f, 13.07f));
        world.Events.Add(95.82f, () => CastFallOfFaith(1));
        world.Events.Add(97.26f, () => WarpIn(faithSources[2]));
        world.Events.Add(98.32f, () => CastFallOfFaith(2));
        world.Events.Add(98.32f, () => TetherFaithTarget(2, 9.58f, 13.03f));
        world.Events.Add(99.76f, () => WarpIn(faithSources[3]));
        world.Events.Add(100.79f, () => TetherFaithTarget(3, 9.7f, 13.08f));
        world.Events.Add(100.87f, () => CastFallOfFaith(3));
        world.Events.Add(102.38f, () => FetterFaithTarget(0));
        world.Events.Add(102.91f, () => SetFaithModelState(0, BoundFettersModelState));
        world.Events.Add(104.44f, () => ChargeFaithTarget(0));
        world.Events.Add(105.48f, () => FetterFaithTarget(1));
        world.Events.Add(105.81f, () => ResolveFaithTether(0));
        world.Events.Add(105.94f, () => SetFaithModelState(0, 0));
        world.Events.Add(105.94f, () => SetFaithModelState(1, BoundFettersModelState));
        world.Events.Add(107.51f, () => ChargeFaithTarget(1));
        world.Events.Add(107.9f, () => FetterFaithTarget(2));
        world.Events.Add(108.45f, () => SetFaithModelState(2, BoundFettersModelState));
        world.Events.Add(108.83f, () => ResolveFaithTether(1));
        world.Events.Add(109f, () => SetFaithModelState(1, 0));
        world.Events.Add(110.01f, () => ChargeFaithTarget(2));
        world.Events.Add(110.49f, () => FetterFaithTarget(3));
        world.Events.Add(110.6f, () => WarpOut(faithSources[1]));
        world.Events.Add(110.97f, () => SetFaithModelState(3, BoundFettersModelState));
        world.Events.Add(111.35f, () => ResolveFaithTether(2));
        world.Events.Add(111.46f, () => SetFaithModelState(2, 0));
        world.Events.Add(112.52f, () => ChargeFaithTarget(3));
        world.Events.Add(113.03f, () => WarpOut(faithSources[2]));
        world.Events.Add(113.87f, () => ResolveFaithTether(3));
        world.Events.Add(114f, () => SetFaithModelState(3, 0));
        world.Events.Add(115.66f, () => WarpOut(faithSources[3]));
        world.Events.Add(116.61f, () => boss?.Cast(Actions.BurnishedGlory));
    }

    public void Tick(float delta, float elapsed)
    {
        if (combat is not { Closed: false } running) return;
        strat?.Tick(state, world, delta);
        running.Tick(delta);
        periodic?.Tick(delta);
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        if (combat is not { Closed: false }) return;
        var id = resolved.Action.ActionId;
        if (Actions.ExplosionSoakers(id) is { } soakers)
            Towers.ResolveTower(world, resolved, id, Actions.UnmitigatedExplosion(id), soakers);
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (combat is not { Closed: false }) return;
        if (lost.StatusId == StatusId.PowderMarkTrail && lost.Reason is StatusLossReason.Expired or StatusLossReason.Died)
            DetonatePowderMark(lost.Holder);
        else if (lost.StatusId == StatusId.FatedBurnMark && lost.Reason == StatusLossReason.Died)
        {
            boss?.CastFromHelper(Actions.FatedBurnMark, at: lost.Holder.Placement());
            ReplaceBoundTarget(lost.Holder);
        }
    }

    public void OnVictory()
    {
        foreach (var enemy in world.Children.OfType<SimEnemy>().ToList()) enemy.CancelCast(CastCancelReason.Cancelled);
        foreach (var tether in world.Children.OfType<SimTether>().ToList()) tether.Despawn();
        foreach (var member in world.Party.AllMembers()) member.StopMoving();
        foreach (var enemy in world.Children.OfType<SimEnemy>().Where(IsSummon).ToList()) enemy.FadeOut();
        if (combat is { BossDefeated: true }) boss?.Defeat(3f);
        OnRunEnded();
    }

    public void OnRunEnded()
    {
        if (actedSubscribed)
        {
            actedSubscribed = false;
            Natives.UserActions.Acted -= OnPlayerActed;
        }
        periodic = null;
        combat?.Dispose();
        if (world is not null && ReferenceEquals(world.Combat, combat)) world.Combat = null;
    }

    private static bool IsSummon(SimEnemy enemy)
        => enemy.BNpcBaseId is BNpcBaseId.FatebreakersImage or BNpcBaseId.HaloOfFlame or BNpcBaseId.HaloOfLevin;

    private void NoteUnsupportedPlayerJob()
    {
        var job = (JobId)Natives.BattleCharas.LocalPlayer.ClassJob;
        if (!DutyActions.Supports(job))
            world.Announce($"P1 stage: {job} has no prototype actions, so your presses deal no numeric damage or healing.");
    }

    private void SpawnBoss()
    {
        // The Turn group starts with the boss away; ScheduleBossReturn brings it back.
        var away = drill == FruP1Drill.TurnOfTheHeavens;
        boss = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Fatebreaker,
            NameId: BNpcNameId.Fatebreaker,
            Level: Level,
            Targetable: !away,
            EnemyList: EnemyListMode.Always,
            Visibility: away ? SpawnVisibility.HiddenUntilShown : SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
        if (boss is null)
        {
            world.Fail("Fatebreaker did not spawn");
            return;
        }
        combat?.BindBoss(boss);
        targeting?.BindBoss(boss);
    }

    private SimEnemy? SpawnImage(Placement at, SpawnVisibility visibility = SpawnVisibility.Visible) => world.SpawnEnemy(new EnemySpawnConfig(
        BNpcBaseId: BNpcBaseId.FatebreakersImage,
        NameId: BNpcNameId.FatebreakersImage,
        Level: Level,
        EnemyList: EnemyListMode.Never,
        Visibility: visibility,
        Placement: at));

    private static void WarpIn(params SimEnemy?[] images)
    {
        foreach (var image in images) image?.PlayActionTimeline(ActionTimelineId.WarpEnd);
    }

    private void WarpOut(SimEnemy? image)
    {
        image?.PlayActionTimeline(ActionTimelineId.WarpStart);
        world.Events.Add(WarpOutSeconds, () => image?.Despawn());
    }

    private SimCharacter? Member(PartyRole role) => world.Party.Get(role) is { } member && member.IsAlive() ? member : null;

    private IEnumerable<SimCharacter> Alive => world.Party.ActiveMembers();

    private void TargetEnmityStandIn()
    {
        targeting?.CurrentTarget();
        targeting?.ShowTarget();
    }

    private void SwingAtTarget()
    {
        if (boss is not { Targetable: true, AnimationLock: false } fatebreaker || targeting?.CurrentTarget() is not { } target) return;
        var marked = target.HasStatus(StatusId.PowderMarkTrail) && target.HasStatus(StatusId.PhysicalVulnerabilityUp);
        fatebreaker.CastFromHelper(marked ? Actions.AutoAttackOnMarkedTank : Actions.AutoAttack, target);
    }

    private void OnPlayerActed(PlayerAction acted)
    {
        if (acted.Step != PlayerActionStep.Resolved || acted.ActionType != ActionType.Action || acted.ActionId != FruP1Enmity.ProvokeActionId) return;
        if (boss is not { } fatebreaker || acted.TargetId != (ulong)fatebreaker.GameObjectId) return;
        if (world.Party.Player is { } player) targeting?.TryProvoke(player);
    }

    private void CastPowderMark()
    {
        if (targeting?.CurrentTarget() is { } target) boss?.Cast(Actions.PowderMarkTrail, target);
    }

    private void DetonatePowderMark(SimCharacter holder)
    {
        boss?.CastFromHelper(Actions.BurnMark, holder);
        if (world.Party.Find.Closest(holder.Position, holder) is { } nearest)
            boss?.CastFromHelper(Actions.BurnMark, nearest);
    }

    private void StartCyclonicCones(SimEnemy? source)
    {
        if (source is null) return;
        var from = source.Placement();
        foreach (var member in Alive.ToList())
        {
            var aim = from.Face(member.Position).Rotation;
            source.CastFromHelper(Actions.CyclonicBreakFirst, member, at: from);
            for (var wave = 0; wave < 3; wave++)
            {
                var rotation = aim + wave * RestConeTurn;
                world.Events.Add((wave + 1) * RestConeInterval,
                    () => source.CastFromHelper(Actions.CyclonicBreakRest, at: new Placement(from.Position, rotation)));
            }
        }
    }

    // UNVERIFIED defaults: pairs on the DPS, then the first supports standing in for any who died.
    private void ResolveCyclonicSplit(bool pairs)
    {
        if (!pairs)
        {
            foreach (var target in Alive.ToList()) boss?.CastFromHelper(Actions.CyclonicBreakSinsmite, target, at: target.Placement());
            return;
        }
        var dps = Alive.Where(member => member is ISimPartyMember slot && slot.Role.IsDps()).ToList();
        var standIns = Alive.Except(dps).Take(4 - dps.Count);
        foreach (var target in dps.Concat(standIns).ToList())
            boss?.CastFromHelper(Actions.CyclonicBreakSinsmoke, target);
    }

    private void SpawnSkyImages()
    {
        for (var spot = 0; spot < 8; spot++)
            if (SpawnImage(EdgeImage(spot), SpawnVisibility.HiddenUntilShown) is { } image)
                skyImages.Add(image);
    }

    private void RevealBlastingImages()
    {
        foreach (var spot in state.BlastingImages) SkyImage(spot)?.RemoveStatus(StatusId.Concealed);
    }

    private void PoseSkyImages()
    {
        for (var spot = 0; spot < 8; spot++)
            SkyImage(spot)?.PlayActionTimeline(state.BlastingImages.Contains(spot) ? RaisedPoseTimeline : RestingPoseTimeline);
    }

    private void SetSkyModelState(byte value)
    {
        foreach (var spot in state.BlastingImages) SkyImage(spot)?.SetModelState(value);
    }

    private void ExitSkyImages()
    {
        var blasting = state.BlastingImages.Select(SkyImage).ToList();
        foreach (var image in skyImages)
        {
            if (blasting.Contains(image)) WarpOut(image);
            else image.Despawn();
        }
        skyImages.Clear();
    }

    private SimEnemy? SkyImage(int spot) => skyImages.FirstOrDefault(image => Vector3.DistanceSquared(image.Position, AtSpot(spot, EdgeRadius)) < 0.01f);

    private void ShowBlastingZoneBars()
    {
        foreach (var spot in state.BlastingImages)
            boss?.CastFromHelper(ActionId.BlastingZoneCast, at: EdgeImage(spot));
    }

    private void RaiseBlastingImages()
    {
        foreach (var spot in state.BlastingImages) SkyImage(spot)?.SetModelState(4);
    }

    private void FireBlastingZones()
    {
        foreach (var spot in state.BlastingImages) SkyImage(spot)?.Cast(Actions.BlastingZone);
    }

    // UNVERIFIED defaults: stacks on the healers, then the first others standing in for any who died.
    private void ResolveSinbound()
    {
        if (!state.UtopianStack)
        {
            foreach (var target in Alive.ToList()) boss?.CastFromHelper(Actions.SinboundThunder, target);
            return;
        }
        var healers = new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer }.Select(Member).OfType<SimCharacter>().ToList();
        var standIns = Alive.Except(healers).Take(2 - healers.Count);
        foreach (var target in healers.Concat(standIns).ToList())
            boss?.CastFromHelper(Actions.SinboundFire, target);
    }

    private static void FadeOut(List<SimEnemy> enemies)
    {
        foreach (var enemy in enemies) enemy.FadeOut();
        enemies.Clear();
    }

    private void SpawnCenterImage() => centerImage = SpawnImage(new Placement(Vector3.Zero, MathF.PI), SpawnVisibility.HiddenUntilShown);

    private void SpawnHeavensImages()
    {
        westImage = SpawnImage(EdgeImage(WestSpot), SpawnVisibility.HiddenUntilShown);
        innerNorthImage = SpawnImage(new Placement(AtSpot(NorthSpot, InnerNorthImageRadius), 0f), SpawnVisibility.HiddenUntilShown);
    }

    private void SpawnBoundImages()
    {
        boundImages[0] = SpawnImage(EdgeImage(SouthWestSpot), SpawnVisibility.HiddenUntilShown);
        boundImages[1] = SpawnImage(EdgeImage(SouthEastSpot), SpawnVisibility.HiddenUntilShown);
    }

    private void SetBoundModelState(byte value)
    {
        foreach (var image in boundImages) image?.SetModelState(value);
    }

    private void StartHeavensLightning()
    {
        westImage?.Cast(Actions.TurnOfHeavensBurntStrikeLightning);
        boss?.CastFromHelper(Actions.TurnOfHeavensBurnout, at: EdgeImage(WestSpot));
    }

    private void StartHeavensFire()
    {
        northImage?.Cast(Actions.TurnOfHeavensBurntStrikeFire);
        CastBlastburn(Actions.TurnOfHeavensBlastburnEast, Actions.TurnOfHeavensBlastburnWest);
    }

    private void CastBlastburn(EnemyAction east, EnemyAction west)
    {
        var edge = AtSpot(NorthSpot, EdgeRadius);
        boss?.CastFromHelper(east, at: new Placement(edge, MathF.PI / 2f));
        boss?.CastFromHelper(west, at: new Placement(edge, -MathF.PI / 2f));
    }

    private void SpawnHalos()
    {
        foreach (var spot in state.FlameSpots) SpawnHalo(BNpcBaseId.HaloOfFlame, BNpcNameId.HaloOfFlame, LayoutId.HaloOfFlame, spot);
        foreach (var spot in state.LevinSpots) SpawnHalo(BNpcBaseId.HaloOfLevin, BNpcNameId.HaloOfLevin, LayoutId.HaloOfLevin, spot);
    }

    private void SpawnHalo(uint baseId, uint nameId, uint layoutId, int spot)
    {
        if (world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: baseId,
                NameId: nameId,
                Level: Level,
                EnemyList: EnemyListMode.Never,
                Placement: new Placement(AtSpot(spot, HaloRadius), Bearing(spot) + MathF.PI),
                Engaged: false,
                LayoutId: layoutId)) is { } halo)
            halos.Add(halo);
    }

    private void CastBrightfire()
    {
        foreach (var halo in halos)
        {
            var isFlame = halo.BNpcBaseId == BNpcBaseId.HaloOfFlame;
            halo.Cast(isFlame == state.LargeIsFire ? Actions.BrightfireLarge : Actions.BrightfireSmall);
        }
    }

    private SimEnemy? BoundSource(int index) => boundImages[index];

    private void MarkBoundTargets()
    {
        for (var i = 0; i < boundHolders.Length; i++)
        {
            boundHolders[i] = Member(state.BoundTargets[i]);
            boundHolders[i]?.AddStatus(StatusId.FatedBurnMark, Actions.BoundMarkSeconds);
            boundHolders[i]?.AddStatus(StatusId.Prey, BoundResolveAt - BoundTetherAt);
        }
    }

    private void TetherBoundTargets()
    {
        for (var i = 0; i < boundHolders.Length; i++)
            if (boundHolders[i] is { } target)
                world.Tether(BoundSource(i), target, TetherId.FatebreakerFire, BoundFetterAt - BoundTetherAt);
    }

    // UNVERIFIED who: one of the living who holds no mark, the first in party order.
    private void ReplaceBoundTarget(SimCharacter dead)
    {
        var index = Array.IndexOf(boundHolders, dead);
        var now = world.Events.Elapsed;
        if (index < 0 || now >= BoundResolveAt) return;
        var replacement = Alive.FirstOrDefault(member => Array.IndexOf(boundHolders, member) < 0);
        boundHolders[index] = replacement;
        if (replacement is null) return;
        replacement.AddStatus(StatusId.FatedBurnMark, BoundResolveAt - now);
        replacement.AddStatus(StatusId.Prey, BoundResolveAt - now);
        if (now >= BoundTetherAt && now < BoundFetterAt) world.Tether(BoundSource(index), replacement, TetherId.FatebreakerFire, BoundFetterAt - now);
    }

    private void FetterBoundTargets()
    {
        for (var i = 0; i < boundHolders.Length; i++)
            if (boundHolders[i] is { } target && target.IsAlive())
                BoundSource(i)?.Cast(Actions.BoundFloatingFetters, target);
    }

    private void ChargeBoundTargets()
    {
        for (var i = 0; i < boundHolders.Length; i++)
            if (boundHolders[i] is { } target && target.IsAlive())
                BoundSource(i)?.Cast(ActionId.TurnOfHeavensSolemnCharge, target, BoundChargeLock);
    }

    private void ResolveBoundOfFaith()
    {
        for (var i = 0; i < boundHolders.Length; i++)
            if (boundHolders[i] is { } target && target.IsAlive())
                boss?.CastFromHelper(Actions.BoundOfFaithSinsmoke, target);
    }

    private void StartExplosions()
    {
        boss?.Cast(state.ExplosionFire ? ActionId.ExplosionBurntStrikeFire : ActionId.ExplosionBurntStrikeLightning);
        var edge = EdgeImage(NorthSpot);
        boss?.CastFromHelper(state.ExplosionFire ? Actions.ExplosionBurntStrikeFire : Actions.ExplosionBurntStrikeLightning, at: edge);
        if (state.ExplosionFire) CastBlastburn(Actions.ExplosionBlastburnEast, Actions.ExplosionBlastburnWest);
        else boss?.CastFromHelper(Actions.ExplosionBurnout, at: edge);
        for (var tower = 0; tower < state.TowerSoakers.Count; tower++)
            boss?.CastFromHelper(Actions.Explosion(state.ExplosionFire, state.TowerSoakers[tower]), at: new Placement(state.TowerPosition(tower), 0f));
    }

    private void SpawnFaithImages()
    {
        faithSources[0] = boss;
        faithSources[1] = SpawnImage(EdgeImage(NorthWestSpot), SpawnVisibility.HiddenUntilShown);
        faithSources[2] = SpawnImage(EdgeImage(NorthSpot), SpawnVisibility.HiddenUntilShown);
        faithSources[3] = SpawnImage(EdgeImage(NorthEastSpot), SpawnVisibility.HiddenUntilShown);
    }

    private void CastFallOfFaith(int index)
        => faithSources[index]?.Cast(state.FaithFire[index] ? ActionId.FallOfFaithFire : ActionId.FallOfFaithLightning);

    private void SetFaithModelState(int index, byte value) => faithSources[index]?.SetModelState(value);

    private void ChargeFaithTarget(int index)
    {
        if (world.Party.Get(state.FaithTargets[index]) is { } holder && holder.IsAlive())
            faithSources[index]?.Cast(state.FaithFire[index] ? ActionId.FallOfFaithSolemnChargeFire : ActionId.FallOfFaithSolemnChargeLightning, holder, BoundChargeLock);
    }

    private void TetherFaithTarget(int index, float tetherSeconds, float preySeconds)
    {
        if (Member(state.FaithTargets[index]) is not { } target) return;
        world.Tether(faithSources[index], target, state.FaithFire[index] ? TetherId.FatebreakerFire : TetherId.FatebreakerLightning, tetherSeconds);
        target.AddStatus(StatusId.Prey, preySeconds);
    }

    private void FetterFaithTarget(int index)
    {
        if (Member(state.FaithTargets[index]) is not { } target) return;
        boss?.CastFromHelper(ActionId.FallOfFaithApply, target);
        faithSources[index]?.Cast(Actions.BoundFloatingFetters, target);
    }

    // UNVERIFIED for a dead holder: the attack still comes, from where the body lies.
    private void ResolveFaithTether(int index)
    {
        if (world.Party.Get(state.FaithTargets[index]) is not { } holder) return;
        var fire = state.FaithFire[index];
        var from = holder.Placement();
        if (fire)
        {
            if (world.Party.Find.Closest(holder.Position, holder) is { } bait)
                boss?.CastFromHelper(Actions.FallOfFaithSinblaze, at: from.Face(bait.Position));
            return;
        }
        if (holder.IsAlive()) boss?.CastFromHelper(Actions.FallOfFaithSinsmite, holder);
        world.Events.Add(BowShockDelay, () =>
        {
            foreach (var bait in world.Party.Find.ClosestN(holder.Position, 4).Where(member => !ReferenceEquals(member, holder)).Take(3).ToList())
                boss?.CastFromHelper(Actions.FallOfFaithBowShock, bait, at: from);
        });
    }

    private void FailIfEnrageSpared()
    {
        // Godmode spares everyone, and nothing else ends the run from here.
        if (world.Party.IsAlive) world.Fail("Enrage reached with the party alive");
    }
}
