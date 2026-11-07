using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Combat;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios.Fru.P5FulgentBlade;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using Dalamud.Bindings.ImGui;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru.P5CombatPreview;

// Fulgent Blade and Paradise Regained as one fight on one Pandora, with HP and resources carried
// through: the two JP scenarios themselves, each started at its point of this timeline. Not the full
// duty, and its numbers are uncalibrated.
public sealed class FruP5CombatPreviewScenario : IDutyScenario
{
    // UNVERIFIED: the gap between the mechanics, and all the numbers.
    private const float ParadiseAt = 38f;
    private const float CloseOnBossAt = 93f;
    private const float EnrageAt = 240f;

    internal static readonly DutyTuning Tuning = new()
    {
        BossMaxHp = 320_000,
        HitOverrides = new Dictionary<uint, uint>
        {
            [ActionId.PandoraAutoAttack] = 8_000,
            [ActionId.FulgentBlade] = 15_000,
            [ActionId.AkhMornPandoraLeft] = 20_000,
            [ActionId.AkhMornPandoraRight] = 20_000,
            [ActionId.ParadiseTowerExplosion] = 15_000,
            [ActionId.CruelPathOfLight] = 15_000,
            [ActionId.CruelPathOfDarkness] = 15_000,
        },
    };

    public string Name => "Combat preview (JP; experimental)";
    public IPhase Phase => FruZone.P5;
    public IReadOnlyList<IScenarioAi> AiStrats => [new FruP5CombatPreviewAi()];

    private SimWorld world = null!;
    private FruP5FulgentBladeJpScenario? fulgent;
    private FruP5ParadiseRegainedJpScenario? paradise;
    private IScenario? active;
    private DutyCombat? combat;
    private float paradiseStartedAt;

    public bool VictoryReached => combat is { BossDefeated: true };

    public void DrawSettings()
        => ImGui.TextWrapped("Experimental P5 preview, not the full duty. HP, damage, healing and actions are uncalibrated prototype values; "
                             + "only WAR/PLD/WHM/SCH/DRG/MNK/BRD/BLM have a small action kit, with no raises, limit breaks, crits or auto-attack damage.");

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        OnRunEnded();
        fulgent = new FruP5FulgentBladeJpScenario();
        paradise = new FruP5ParadiseRegainedJpScenario();
        active = fulgent;
        combat = new DutyCombat(world, Tuning);
        world.Combat = combat;
        NoteUnsupportedPlayerJob();

        fulgent.Run(world, 0);
        world.Events.Add(0f, BindBoss);
        world.Events.Add(ParadiseAt, StartParadise);
        FruP5CombatPreviewAi.CloseOnBossAfterMechanics(world, CloseOnBossAt);
        world.Events.Add(EnrageAt, Enrage);
    }

    private void NoteUnsupportedPlayerJob()
    {
        var job = (JobId)Natives.BattleCharas.LocalPlayer.ClassJob;
        if (!DutyActions.Supports(job))
            world.Announce($"Combat preview: {job} has no prototype actions, so your presses deal no numeric damage or healing.");
    }

    private void BindBoss()
    {
        if (fulgent?.Pandora is { } pandora) combat?.BindBoss(pandora);
        else world.Fail("Pandora did not spawn");
    }

    // Facing is the one thing Fulgent Blade leaves on her that Paradise Regained doesn't set itself.
    private void StartParadise()
    {
        if (fulgent?.Pandora is not { } pandora || paradise is null) return;
        pandora.KeepFacing(null);
        pandora.SetRotation(MathF.PI);
        paradise.ExistingPandora = pandora;
        paradiseStartedAt = world.Events.Elapsed;
        active = paradise;
        paradise.Run(world, 0);
    }

    private void Enrage()
    {
        world.Party.WipeAllPlayers(SimCharacterDeathExtensions.Environment, "Enrage");
        // Godmode spares everyone, and nothing else ends the run from here.
        if (world.Party.IsAlive) world.Fail("Enrage reached with the party alive");
    }

    public void Tick(float delta, float elapsed)
    {
        if (combat is not { Closed: false } running) return;
        active?.Tick(delta, active == paradise ? elapsed - paradiseStartedAt : elapsed);
        running.Tick(delta);
    }

    public void OnActionResolved(ActionResolved resolved)
    {
        if (combat is { Closed: false }) active?.OnActionResolved(resolved);
    }

    public void OnStatusAdded(StatusAdded added)
    {
        if (combat is { Closed: false }) active?.OnStatusAdded(added);
    }

    public void OnStatusLost(StatusLost lost)
    {
        if (combat is { Closed: false }) active?.OnStatusLost(lost);
    }

    public void OnTetherEnded(TetherEnded ended)
    {
        if (combat is { Closed: false }) active?.OnTetherEnded(ended);
    }

    public void OnVictory()
    {
        foreach (var enemy in world.Children.OfType<SimEnemy>().ToList()) enemy.CancelCast(CastCancelReason.Cancelled);
        foreach (var tether in world.Children.OfType<SimTether>().ToList()) tether.Despawn();
        foreach (var seam in world.Children.OfType<SimEventObject>().ToList()) seam.Despawn();
        foreach (var member in world.Party.AllMembers()) member.StopMoving();
        fulgent?.Pandora?.Defeat();
        OnRunEnded();
    }

    public void OnRunEnded()
    {
        paradise?.HideLiveTowers();
        combat?.Dispose();
        if (world is not null && ReferenceEquals(world.Combat, combat)) world.Combat = null;
    }
}
