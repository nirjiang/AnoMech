using System.Globalization;
using AnoMech.Core.Combat;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

// With no strat running, whole-fight runs go on godmode, with enough boss HP to reach the enrage. They
// check the clock, the casts and who is aimed at; each mechanic's kills are FruP1ActionsTests', and a
// strat's clean runs are FruP1FatebreakerJpAiTests'.
public class FruP1FatebreakerScenarioTests
{
    private const float ClockTolerance = 0.1f;
    private const float EndOfFight = 165f;

    private static void Forced(FruP1FatebreakerStateOverrides o)
    {
        o.FirstCyclonicPairs = true;
        o.UtopianStack = true;
        o.BlastingImages = [0, 1, 2];
        o.ImageCyclonicPairs = false;
        o.LargeIsFire = true;
        o.FlameOnWest = true;
        o.BoundTargets = [PartyRole.MainTank, PartyRole.MeleeDpsA];
        o.FaithTargets = [PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.OffTank, PartyRole.PhysRangedDps];
        o.FaithFire = [true, false, true, false];
        o.ExplosionFire = true;
        o.TowersEast = false;
        o.TowerSoakers = [1, 2, 3];
    }

    [Test]
    public void EachBeatFirstLandsOnTheFightsClock()
    {
        (uint Id, float At, string What)[] beats =
        [
            (ActionId.CyclonicBreakBossStack, 13.7f, "boss Cyclonic Break"),
            (ActionId.CyclonicBreakFirst, 14.3f, "bait cones"),
            (ActionId.CyclonicBreakSinsmoke, 16.4f, "pairs"),
            (ActionId.CyclonicBreakRest, 16.4f, "cone wave 2"),
            (ActionId.PowderMarkTrail, 24.3f, "first Powder Mark"),
            (ActionId.UtopianSkyStack, 34.83f, "Utopian Sky"),
            (ActionId.BurnMark, 40.4f, "first Burn Mark"),
            (ActionId.BlastingZoneCast, 50.07f, "line bars"),
            (ActionId.BlastingZone, 49.13f, "lines"),
            (ActionId.SinboundFire, 49.93f, "Sinbound"),
            (ActionId.CyclonicBreakImageSpread, 55.6f, "image Cyclonic Break"),
            (ActionId.CyclonicBreakSinsmite, 58.27f, "spread"),
            (ActionId.TurnOfHeavensBurntStrikeLightning, 64.62f, "lightning strike"),
            (ActionId.TurnOfHeavensBurnout, 66.32f, "Burnout"),
            (ActionId.TurnOfHeavensFire, 63.63f, "Turn of the Heavens"),
            (ActionId.TurnOfHeavensBurntStrikeFire, 70.65f, "fire strike"),
            (ActionId.TurnOfHeavensBlastburn, 72.65f, "knockback"),
            (ActionId.BrightfireLarge, 74.68f, "halos"),
            (ActionId.BoundOfFaith, 74.63f, "Bound of Faith"),
            (ActionId.BoundOfFaithSinsmoke, 78.67f, "Bound stacks"),
            (ActionId.BurnishedGlory, 86.27f, "first raidwide"),
            (ActionId.FallOfFaithFire, 101.76f, "Fall of Faith"),
            (ActionId.FallOfFaithSolemnChargeFire, 104.44f, "bait 1"),
            (ActionId.FallOfFaithSolemnChargeLightning, 107.53f, "bait 2"),
            (ActionId.ExplosionBurntStrikeFire, 141.71f, "narrow line"),
            (ActionId.ExplosionBlastburn, 143.71f, "line knockback"),
            (ActionId.ExplosionFireOne, 145.71f, "towers"),
            (ActionId.FatebreakerEnrage, 160.87f, "enrage"),
        ];
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunFor(EndOfFight);
        Assert.Multiple(() =>
        {
            foreach (var beat in beats)
                Assert.That(duty.AbilityTimes(beat.Id).FirstOrDefault(float.NaN), Is.EqualTo(beat.At).Within(ClockTolerance), beat.What);
        });
    }

    [Test]
    public void TheWholePhaseRunsWithEveryCountOnOneFatebreakerToAnEnrageThatEndsIt()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunFor(EndOfFight);
        var spawns = duty.Fake.Timeline.Rows.Where(row => row["kind"] == "spawn").ToList();
        int Spawned(uint id) => spawns.Count(row => row["id"] == id.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(Spawned(BNpcBaseId.Fatebreaker), Is.EqualTo(1));
            Assert.That(Spawned(BNpcBaseId.FatebreakersImage), Is.EqualTo(17));
            Assert.That(Spawned(BNpcBaseId.HaloOfFlame), Is.EqualTo(4));
            Assert.That(Spawned(BNpcBaseId.HaloOfLevin), Is.EqualTo(4));
            Assert.That(duty.AbilityCount(ActionId.CyclonicBreakFirst), Is.EqualTo(16));
            Assert.That(duty.AbilityCount(ActionId.CyclonicBreakRest), Is.EqualTo(48));
            Assert.That(duty.AbilityCount(ActionId.PowderMarkTrail), Is.EqualTo(2));
            Assert.That(duty.AbilityCount(ActionId.BurnMark), Is.EqualTo(4));
            Assert.That(duty.AbilityCount(ActionId.BurnishedGlory), Is.EqualTo(2));
            Assert.That(duty.AbilityCount(ActionId.BlastingZone), Is.EqualTo(3));
            Assert.That(duty.AbilityCount(ActionId.SinboundFire), Is.EqualTo(2));
            Assert.That(duty.AbilityCount(ActionId.BoundOfFaithSinsmoke), Is.EqualTo(2));
            Assert.That(duty.AbilityCount(ActionId.FatebreakerEnrage), Is.EqualTo(1));
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Failed));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void TheHelperPoolHoldsThroughTheBusiestPull(int seed)
    {
        using var duty = FruP1Run.Start(godMode: true, longFight: true, seed: seed);
        var exhausted = new List<uint>();
        duty.Game.World.HelperPoolExhausted += exhausted.Add;
        duty.RunFor(EndOfFight);
        Assert.That(exhausted, Is.Empty);
    }

    [Test]
    public void TheBossTargetabilityFollowsTheDowntimeAndDamageIsRefusedThrough()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        var caster = duty.Member(PartyRole.CasterDps);
        var attack = DutyActions.Of(DutyCombat.JobOf(caster)!.Value).First(action => action.Kind == DutyActionKind.Damage);

        duty.RunTo(34.7f);
        var boss = duty.Combat.Boss!;
        Assert.That(boss.Targetable, Is.True);
        duty.RunTo(35.1f);
        Assert.That(boss.Targetable, Is.False);
        var hpInDowntime = duty.Combat.BossHp;
        Assert.That(duty.Combat.Validate(caster, attack, (ulong)boss.GameObjectId), Is.EqualTo(DutyActionRefusal.Untargetable));
        duty.RunTo(79.9f);
        Assert.Multiple(() =>
        {
            Assert.That(boss.Targetable, Is.False);
            Assert.That(duty.Combat.BossHp, Is.EqualTo(hpInDowntime));
        });
        duty.RunTo(80.2f);
        Assert.Multiple(() =>
        {
            Assert.That(boss.Targetable, Is.True);
            Assert.That(duty.Combat.Validate(caster, attack, (ulong)boss.GameObjectId), Is.Not.EqualTo(DutyActionRefusal.Untargetable));
        });
    }

    [Test]
    public void ResourcesAndTheBossCarryAcrossTheDowntime()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var player = duty.Player;
        combat.SetMp(player, 1_000);
        duty.RunTo(90f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Combat, Is.SameAs(combat));
            Assert.That(duty.Combat.Boss, Is.SameAs(boss));
            Assert.That(combat.Mp(player), Is.GreaterThan(1_000u).And.LessThan(combat.Tuning.MaxMp));
        });
    }

    [Test]
    public void TheRestConesRepeatTheBaitsThenTurnAnEighthOfAQuadrantEachWave()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        foreach (var (role, index) in PerRole.All.Select((role, index) => (role, index)))
        {
            var bearing = 0.2f + index * MathF.Tau / 8f;
            duty.Place(role, 10f * MathF.Sin(bearing), 10f * MathF.Cos(bearing));
        }
        duty.RunTo(21f);
        var eighth = 22.5f * MathF.PI / 180f;
        var baits = Wrapped(duty.AbilityRotationsAt(ActionId.CyclonicBreakFirst, 14.3f));
        Assert.Multiple(() =>
        {
            Assert.That(baits, Has.Count.EqualTo(8));
            Assert.That(Wrapped(duty.AbilityRotationsAt(ActionId.CyclonicBreakRest, 16.36f)), Is.EqualTo(baits).Within(0.02f));
            Assert.That(Wrapped(duty.AbilityRotationsAt(ActionId.CyclonicBreakRest, 18.42f).Select(r => r + eighth)), Is.EqualTo(baits).Within(0.02f));
            Assert.That(Wrapped(duty.AbilityRotationsAt(ActionId.CyclonicBreakRest, 20.48f).Select(r => r + 2f * eighth)), Is.EqualTo(baits).Within(0.02f));
        });
    }

    private static List<SimTether> ActiveTethers(DutyHarness duty)
        => duty.Game.World.Children.OfType<SimTether>().Where(tether => tether.IsActive).ToList();

    private static List<float> Wrapped(IEnumerable<float> rotations)
        => rotations.Select(r => (r % MathF.Tau + MathF.Tau) % MathF.Tau).Order().ToList();

    [Test]
    public void CyclonicPairsGoOnTheDpsByDefault()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(18f);
        var expected = PerRole.All.Where(role => role.IsDps()).Select(role => DutyHarness.Id(duty.Member(role))).Order();
        Assert.That(duty.AbilityRows(ActionId.CyclonicBreakSinsmoke).Select(row => row["target"]).Order(), Is.EqualTo(expected));
    }

    [TestCase(16.4f, true)]
    [TestCase(58.25f, false)]
    public void CyclonicSpreadIsReleasedFromAHelperAtEachTarget(float resolveAt, bool firstSplit)
    {
        using var duty = FruP1Run.Start(o => { Forced(o); o.FirstCyclonicPairs = !firstSplit; }, godMode: true, longFight: true);
        duty.RunTo(resolveAt - 0.3f);
        var roles = PerRole.All.ToList();
        for (var i = 0; i < roles.Count; i++)
        {
            var angle = i * MathF.Tau / roles.Count;
            duty.Place(roles[i], 15f * MathF.Sin(angle), 15f * MathF.Cos(angle));
        }
        duty.RunTo(resolveAt + 0.1f);
        var rows = duty.AbilityRows(ActionId.CyclonicBreakSinsmite)
            .Where(row => MathF.Abs(float.Parse(row["t"], CultureInfo.InvariantCulture) - resolveAt) < 0.1f).ToList();
        Assert.That(rows, Has.Count.EqualTo(8));
        Assert.That(rows.Select(row => row["actor"]).Distinct().Count(), Is.EqualTo(8));
        foreach (var role in roles)
        {
            var member = duty.Member(role);
            var row = rows.Single(r => r["target"] == DutyHarness.Id(member));
            var spot = duty.Game.World.Coordinates.ToGlobal(member.Position);
            Assert.Multiple(() =>
            {
                Assert.That(float.Parse(row["x"], CultureInfo.InvariantCulture), Is.EqualTo(spot.X).Within(0.02f));
                Assert.That(float.Parse(row["z"], CultureInfo.InvariantCulture), Is.EqualTo(spot.Z).Within(0.02f));
                Assert.That(row["detail"], Does.StartWith("hits=1"));
            });
        }
    }

    [Test]
    public void ADeadPairTargetIsStoodInForSoNoPairDisappears()
    {
        using var duty = FruP1Run.Start(Forced, longFight: true);
        var dead = duty.Member(PartyRole.MeleeDpsA);
        dead.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Game.GodMode = true;
        duty.RunTo(17f);
        var targets = duty.AbilityRows(ActionId.CyclonicBreakSinsmoke).Select(row => row["target"]).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(targets, Has.Count.EqualTo(4));
            Assert.That(targets, Does.Not.Contain(DutyHarness.Id(dead)));
        });
    }

    [Test]
    public void UtopianStacksGoOnTheHealersByDefault()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(51f);
        var targets = duty.AbilityRows(ActionId.SinboundFire).Select(row => row["target"]).Order();
        Assert.That(targets, Is.EqualTo(new[] { DutyHarness.Id(duty.Member(PartyRole.RegenHealer)), DutyHarness.Id(duty.Member(PartyRole.ShieldHealer)) }.Order()));
    }

    [Test]
    public void AutoAttacksSwingOnlyInUptimeAndNeverOverABossCast()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(90f);
        var swings = duty.AbilityTimes(ActionId.FatebreakerAutoAttack);
        Assert.Multiple(() =>
        {
            Assert.That(swings.Where(t => t < 7.2f), Is.Not.Empty);
            Assert.That(swings.Where(t => t is > 35f and < 80.4f), Is.Empty, "the boss is gone");
            Assert.That(swings.Where(t => t is > 7.3f and < 13.7f), Is.Empty, "on a bar");
            Assert.That(swings.Where(t => t > 87f), Is.Not.Empty);
        });
    }

    [Test]
    public void TheStratsBotCoTankProvokesTheBossOffAPowderMarkHolderAndBackAgain()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true, withStrat: true);
        var scenario = (FruP1FatebreakerScenario)duty.Scenario;
        duty.RunTo(20f);
        Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.MainTank)));
        duty.RunTo(25f);
        Assert.Multiple(() =>
        {
            Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.OffTank)));
            Assert.That(duty.AbilityCount(FruP1Enmity.ProvokeActionId), Is.EqualTo(1), "once, as a real action");
        });
        duty.RunTo(127f);
        Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.OffTank)));
        duty.RunTo(131f);
        Assert.Multiple(() =>
        {
            Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.MainTank)));
            Assert.That(duty.AbilityCount(FruP1Enmity.ProvokeActionId), Is.EqualTo(2));
        });
    }

    [Test]
    public void WithNoStratTheMechanicItselfNeverMovesTheBoss()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        var scenario = (FruP1FatebreakerScenario)duty.Scenario;
        duty.RunTo(26f);
        Assert.Multiple(() =>
        {
            Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.MainTank)));
            Assert.That(duty.AbilityCount(FruP1Enmity.ProvokeActionId), Is.Zero);
        });
    }

    [Test]
    public void AHumanCoTankHasToProvokeOrTheMarkedTankDies()
    {
        using var duty = new DutyHarness(typeof(FruP1FatebreakerScenario), PartyRole.OffTank, debugBot: false) { SelectedAi = null };
        var scenario = (FruP1FatebreakerScenario)duty.Scenario;
        scenario.Tuning = FruP1FatebreakerScenario.DefaultTuning with { BossMaxHp = 1_000_000_000 };
        duty.Start();
        duty.PlaceAll(0f, 0f);
        duty.Place(PartyRole.OffTank, 0f, 19f);
        duty.Place(PartyRole.MainTank, 19f, 0f);
        duty.Game.GodMode = true;
        duty.RunTo(25f);
        Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.MainTank)), "nobody pressed anything");
        duty.Game.GodMode = false;
        duty.RunTo(29f);
        Assert.That(duty.Member(PartyRole.MainTank).Dead, Is.True);
    }

    [Test]
    public void ARealProvokeMovesTheBossTarget()
    {
        using var duty = new DutyHarness(typeof(FruP1FatebreakerScenario), PartyRole.OffTank, debugBot: false) { SelectedAi = null };
        var scenario = (FruP1FatebreakerScenario)duty.Scenario;
        duty.Start();
        duty.RunFor(2f);
        Assert.That(scenario.BossTarget, Is.SameAs(duty.Member(PartyRole.MainTank)));
        duty.Press(7533, duty.Combat.Boss!);
        Assert.That(scenario.BossTarget, Is.SameAs(duty.Player));
        Assert.That(Natives.Data.ActionName(7533), Is.EqualTo("Provoke"));
    }

    [Test]
    public void ADeadBoundTargetIsReplacedOnItsOwnTether()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(64.8f);
        var first = duty.Member(PartyRole.MeleeDpsA);
        Assert.That(first.HasStatus(StatusId.FatedBurnMark), Is.True);
        duty.Game.GodMode = false;
        first.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Frame();
        var marked = PerRole.All.Select(duty.Member).Where(member => member.IsAlive() && member.HasStatus(StatusId.FatedBurnMark)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(marked, Has.Count.EqualTo(2));
            Assert.That(duty.AbilityCount(ActionId.FatedBurnMark), Is.EqualTo(1));
        });
    }

    [Test]
    public void TheUtopianSpreadTakesEveryoneInsteadOfStacking()
    {
        using var duty = FruP1Run.Start(o =>
        {
            Forced(o);
            o.UtopianStack = false;
        }, godMode: true, longFight: true);
        duty.RunTo(51f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityTimes(ActionId.UtopianSkySpread), Has.Count.EqualTo(1));
            Assert.That(duty.AbilityCount(ActionId.SinboundThunder), Is.EqualTo(8));
            Assert.That(duty.AbilityCount(ActionId.SinboundFire), Is.Zero);
        });
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void TheLargeHalosAreTheRolledElementOnTheRolledSide(bool largeIsFire, bool flameOnWest)
    {
        using var duty = FruP1Run.Start(o =>
        {
            Forced(o);
            o.LargeIsFire = largeIsFire;
            o.FlameOnWest = flameOnWest;
        }, godMode: true, longFight: true);
        duty.RunTo(70f);
        var large = duty.Enemies(largeIsFire ? BNpcBaseId.HaloOfFlame : BNpcBaseId.HaloOfLevin).ToList();
        var small = duty.Enemies(largeIsFire ? BNpcBaseId.HaloOfLevin : BNpcBaseId.HaloOfFlame).ToList();
        var state = ((FruP1FatebreakerScenario)duty.Scenario).State!;
        var largeIds = large.Select(DutyHarness.Id).Order();
        var smallIds = small.Select(DutyHarness.Id).Order();
        Assert.That(small.Average(halo => halo.Position.X) * state.SafeSideX, Is.GreaterThan(0f));
        duty.RunTo(75.5f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityRows(ActionId.BrightfireLarge).Select(row => row["actor"]).Order(), Is.EqualTo(largeIds));
            Assert.That(duty.AbilityRows(ActionId.BrightfireSmall).Select(row => row["actor"]).Order(), Is.EqualTo(smallIds));
            Assert.That(duty.AbilityCount(largeIsFire ? ActionId.TurnOfHeavensFire : ActionId.TurnOfHeavensLightning), Is.EqualTo(1));
            Assert.That(duty.AbilityCount(largeIsFire ? ActionId.TurnOfHeavensLightning : ActionId.TurnOfHeavensFire), Is.Zero);
        });
    }

    [Test]
    public void TheBoundTargetsHoldFireTethersAndFatedBurnMarksThenAreRooted()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(70f);
        var tank = duty.Member(PartyRole.MainTank);
        var dps = duty.Member(PartyRole.MeleeDpsA);
        var tethers = ActiveTethers(duty);
        Assert.Multiple(() =>
        {
            Assert.That(tethers, Has.Count.EqualTo(2));
            Assert.That(tethers.All(tether => tether.TetherId == TetherId.FatebreakerFire), Is.True);
            Assert.That(tank.HasStatus(StatusId.FatedBurnMark) && dps.HasStatus(StatusId.FatedBurnMark), Is.True);
            Assert.That(tank.HasStatus(StatusId.Prey) && dps.HasStatus(StatusId.Prey), Is.True);
            Assert.That(tank.MovementLocked, Is.False);
        });
        duty.RunTo(76.5f);
        Assert.That(tank.MovementLocked && dps.MovementLocked, Is.True);
        duty.RunTo(79.8f);
        Assert.Multiple(() =>
        {
            Assert.That(tank.MovementLocked || dps.MovementLocked, Is.False);
            Assert.That(tank.HasStatus(StatusId.FatedBurnMark) || dps.HasStatus(StatusId.FatedBurnMark), Is.False);
        });
    }

    [Test]
    public void FallOfFaithResolvesEachTetherAsItsElementInOrder()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(101.2f);
        Assert.Multiple(() =>
        {
            Assert.That(ActiveTethers(duty).Select(tether => tether.TetherId),
                Is.EqualTo(new[] { TetherId.FatebreakerFire, TetherId.FatebreakerLightning, TetherId.FatebreakerFire, TetherId.FatebreakerLightning }));
            Assert.That(duty.Member(PartyRole.RegenHealer).HasStatus(StatusId.Prey), Is.True);
            Assert.That(duty.Member(PartyRole.PhysRangedDps).HasStatus(StatusId.Prey), Is.True);
            Assert.That(duty.Member(PartyRole.MainTank).HasStatus(StatusId.Prey), Is.False);
        });
        duty.RunTo(115f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityTimes(ActionId.FallOfFaithSinblaze), Is.EqualTo(new[] { 105.81f, 111.35f }).Within(ClockTolerance));
            Assert.That(duty.AbilityTimes(ActionId.FallOfFaithSinsmite), Is.EqualTo(new[] { 108.83f, 113.87f }).Within(ClockTolerance));
            Assert.That(duty.AbilityCount(ActionId.FallOfFaithBowShock), Is.EqualTo(6));
            Assert.That(ActiveTethers(duty), Is.Empty);
            Assert.That(duty.Member(PartyRole.RegenHealer).HasStatus(StatusId.Prey), Is.False);
        });
    }

    [Test]
    public void UtopianSkyImagesAreConcealedUntilTheAttackingOnesRevealThemselves()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        List<SimEnemy> Images() => duty.Game.World.Children.OfType<SimEnemy>().Where(e => e.BNpcBaseId == BNpcBaseId.FatebreakersImage).ToList();
        duty.RunTo(38.1f);
        var concealedAtWarpIn = Images().Count(image => image.HasStatus(StatusId.Concealed));
        duty.RunTo(48.9f);
        var concealedBeforeReveal = Images().Count(image => image.HasStatus(StatusId.Concealed));
        duty.RunTo(49.25f);
        Assert.Multiple(() =>
        {
            Assert.That(concealedAtWarpIn, Is.EqualTo(8));
            Assert.That(concealedBeforeReveal, Is.EqualTo(8));
            Assert.That(Images().Count(image => image.HasStatus(StatusId.Concealed)), Is.EqualTo(5));
        });
    }

    [Test]
    public void FallOfFaithImagesWarpInAndOutAndChargeAtTheirReplayMoments()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunFor(EndOfFight);
        float At(Dictionary<string, string> row) => float.Parse(row["t"], CultureInfo.InvariantCulture);
        var rows = duty.Fake.Timeline.Rows.Where(row => At(row) is > 90f and < 120f).ToList();
        var sources = duty.AbilityRows(ActionId.FallOfFaithFire).Concat(duty.AbilityRows(ActionId.FallOfFaithLightning)).Select(row => row["actor"]).Distinct().ToList();
        var images = sources.Except(new[] { DutyHarness.Id(duty.Combat.Boss!) }).ToList();
        List<Dictionary<string, string>> Rows(string id, string p, IEnumerable<string> actors)
            => rows.Where(row => row["kind"].StartsWith("ac") && row["id"] == id && row["p"] == p && actors.Contains(row["actor"])).OrderBy(At).ToList();
        var charges = duty.AbilityRows(ActionId.FallOfFaithSolemnChargeFire).Concat(duty.AbilityRows(ActionId.FallOfFaithSolemnChargeLightning)).OrderBy(At).ToList();
        var casts = rows.Where(row => row["kind"] == "cast" && (row["id"] == ActionId.FallOfFaithFire.ToString() || row["id"] == ActionId.FallOfFaithLightning.ToString())).OrderBy(At).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(images, Has.Count.EqualTo(3));
            Assert.That(duty.AbilityTimes(ActionId.FallOfFaithFire).Concat(duty.AbilityTimes(ActionId.FallOfFaithLightning)).Order(),
                Is.EqualTo(new[] { 101.76f, 104.83f, 107.33f, 109.88f }).Within(ClockTolerance));
            Assert.That(casts.Select(At), Is.EqualTo(new[] { 92.74f, 95.82f, 98.32f, 100.87f }).Within(0.1f));
            Assert.That(Rows("0x197", $"0x{ActionTimelineId.WarpEnd:X}", images).Select(At), Is.EqualTo(new[] { 94.7f, 97.26f, 99.76f }).Within(0.1f));
            Assert.That(Rows("0x197", $"0x{ActionTimelineId.WarpStart:X}", images).Select(At), Is.EqualTo(new[] { 110.6f, 113.03f, 115.66f }).Within(0.1f));
            Assert.That(Rows("0x3F", "0x1C", sources).Select(At), Is.EqualTo(new[] { 102.91f, 105.94f, 108.45f, 110.97f }).Within(0.1f));
            Assert.That(charges.Select(At), Is.EqualTo(new[] { 104.44f, 107.51f, 110.01f, 112.52f }).Within(0.1f));
            Assert.That(charges.Select(row => row["anim_lock"]), Is.All.EqualTo("3.10"));
            Assert.That(duty.AbilityRows(ActionId.FloatingFetters).Select(row => row["anim_lock"]), Is.All.EqualTo("2.10"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TheExplosionLineAndTowersFollowTheRolledElement(bool fire)
    {
        using var duty = FruP1Run.Start(o =>
        {
            Forced(o);
            o.ExplosionFire = fire;
        }, godMode: true, longFight: true);
        duty.RunTo(140f);
        duty.PlaceAll(1f, 0f);
        duty.RunTo(148f);
        var (one, two, three) = fire
            ? (ActionId.ExplosionFireOne, ActionId.ExplosionFireTwo, ActionId.ExplosionFireThree)
            : (ActionId.ExplosionLightningOne, ActionId.ExplosionLightningTwo, ActionId.ExplosionLightningThree);
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityCount(one), Is.EqualTo(1));
            Assert.That(duty.AbilityCount(two), Is.EqualTo(1));
            Assert.That(duty.AbilityCount(three), Is.EqualTo(1));
            Assert.That(duty.AbilityCount(ActionId.ExplosionBlastburn), Is.EqualTo(fire ? 2 : 0));
            Assert.That(duty.AbilityCount(ActionId.ExplosionBurnout), Is.EqualTo(fire ? 0 : 1));
            Assert.That(duty.AbilityCount(fire ? ActionId.UnmitigatedExplosionFire : ActionId.UnmitigatedExplosionLightning), Is.EqualTo(3), "nobody soaked");
            Assert.That(duty.AbilityCount(fire ? ActionId.UnmitigatedExplosionLightning : ActionId.UnmitigatedExplosionFire), Is.Zero);
        });
    }

    [Test]
    public void BothPowderMarksTakeWhoeverIsNearestAtTheirOwnMoment()
    {
        using var duty = FruP1Run.Start(o =>
        {
            Forced(o);
            o.ExplosionFire = false;
        }, godMode: true, longFight: true);
        foreach (var role in PerRole.All) duty.Place(role, 0f, 15f);
        duty.Place(PartyRole.MainTank, 0f, -15f);
        duty.Place(PartyRole.MeleeDpsB, 0f, -10f);
        duty.RunTo(40f);
        var holder = duty.Member(PartyRole.MainTank);
        Assert.That(holder.HasStatus(StatusId.PowderMarkTrail), Is.True);
        duty.RunTo(41f);
        Assert.That(duty.AbilityRows(ActionId.BurnMark).Select(row => row["target"]),
            Is.EquivalentTo(new[] { DutyHarness.Id(holder), DutyHarness.Id(duty.Member(PartyRole.MeleeDpsB)) }));

        duty.RunTo(146f);
        var second = PerRole.All.Select(duty.Member).Single(member => member.HasStatus(StatusId.PowderMarkTrail));
        foreach (var role in PerRole.All) duty.Place(role, 0f, 15f);
        second.SetPosition(new System.Numerics.Vector3(0f, 0f, -15f));
        duty.Place(PartyRole.ShieldHealer, 0f, -10f);
        duty.RunTo(147f);
        Assert.That(duty.AbilityRows(ActionId.BurnMark).Skip(2).Select(row => row["target"]),
            Is.EquivalentTo(new[] { DutyHarness.Id(second), DutyHarness.Id(duty.Member(PartyRole.ShieldHealer)) }));
    }

    [Test]
    public void TheHpDefeatIsTheOnlyClearAndEndsTheQueueAtAnyPoint()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        var combat = duty.Combat;
        var boss = combat.Boss!;
        duty.RunFor(2f);
        Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Running));

        combat.DealBossDamage(combat.BossHp);
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Succeeded));
            Assert.That(boss.Dead, Is.True);
            Assert.That(combat.Closed, Is.True);
            Assert.That(duty.Game.World.Combat, Is.Null);
        });
        duty.RunFor(15f);
        Assert.That(duty.AbilityCount(ActionId.CyclonicBreakBossStack) + duty.AbilityCount(ActionId.CyclonicBreakBossSpread), Is.Zero, "nothing lands after the clear");
    }

    [Test]
    public void AKillDuringAnyMechanicCancelsWhatIsStillOnItsBar()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        duty.RunFor(10f);
        duty.Combat.DealBossDamage(duty.Combat.BossHp);
        duty.Frame();
        duty.RunFor(10f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Succeeded));
            Assert.That(duty.AbilityCount(ActionId.CyclonicBreakBossStack) + duty.AbilityCount(ActionId.CyclonicBreakBossSpread), Is.Zero);
            Assert.That(duty.AbilityCount(ActionId.CyclonicBreakFirst), Is.Zero);
        });
    }

    [Test]
    public void TheHumansDeathRestartsOnAFreshLedgerAndBoss()
    {
        using var duty = FruP1Run.Start();
        var (combat, boss) = (duty.Combat, duty.Combat.Boss);
        uint? hpAtFreshStart = null;
        duty.Game.RunStarted += (_, _) => hpAtFreshStart = duty.Game.World.Combat?.BossHp;
        duty.Player.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Frame();
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(combat.Closed, Is.True);
            Assert.That(duty.Combat, Is.Not.SameAs(combat));
            Assert.That(hpAtFreshStart, Is.EqualTo(duty.Combat.Tuning.BossMaxHp));
            Assert.That(duty.Combat.Boss, Is.Not.SameAs(boss));
        });
    }

    [Test]
    public void BotDeathsPlayOn()
    {
        using var duty = FruP1Run.Start();
        duty.KillAllBots();
        duty.RunFor(5f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Paused, Is.False);
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.RunningWithMistake));
        });
    }

    [Test]
    public void EndingTheRunTwiceIsSafe()
    {
        using var duty = FruP1Run.Start();
        var scenario = (IDutyScenario)duty.Scenario;
        scenario.OnRunEnded();
        Assert.DoesNotThrow(scenario.OnRunEnded);
        Assert.That(duty.Game.World.Combat, Is.Null);
    }

    [Test]
    public void ItIsListedOnceAsAnExperimentalSoloStageWithOneJpStrategy()
    {
        IScenario scenario = new FruP1FatebreakerScenario();
        Assert.Multiple(() =>
        {
            Assert.That(scenario.Phase, Is.SameAs(FruZone.P1));
            Assert.That(scenario.AiStrats.Select(ai => ai.Group), Is.EqualTo(new[] { "JP" }));
            Assert.That(scenario.SupportsMultiplayer, Is.False);
            Assert.That(scenario.Name, Does.Contain("experimental"));
            Assert.That(ScenarioCatalog.Create().Scenarios.OfType<FruP1FatebreakerScenario>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public void ThePhaseSetsTheReplayWeatherAndNoUnverifiedMusic()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FruZone.P1.Weather, Is.EqualTo(WeatherId.P1));
            Assert.That(FruZone.P1.Bgm, Is.Zero);
        });
    }

    [Test]
    public void UtopianSkyFogsTheArenaAndShowsItsImagesAtTheirReplayMoments()
    {
        using var duty = FruP1Run.Start(Forced, godMode: true, longFight: true);
        duty.RunTo(53f);
        var rows = duty.Fake.Timeline.Rows;
        float At(Dictionary<string, string> row) => float.Parse(row["t"], CultureInfo.InvariantCulture);
        var weather = rows.Where(row => row["kind"] == "weather" && At(row) > 1f).ToList();
        var sky = duty.AbilityRows(ActionId.BlastingZone).Select(row => row["actor"]).Distinct().ToList();
        var images = rows.Where(row => row["kind"] == "spawn" && row["id"] == BNpcBaseId.FatebreakersImage.ToString()).Take(8).Select(row => row["actor"]).ToList();
        List<Dictionary<string, string>> Timeline(ushort timelineId, IEnumerable<string> actors)
            => rows.Where(row => row["kind"].StartsWith("ac") && row["id"] == "0x197" && row["p"] == $"0x{timelineId:X}" && actors.Contains(row["actor"])).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(weather.Select(row => row["id"]), Is.EqualTo(new[] { WeatherId.P1Fog.ToString(), WeatherId.P1.ToString() }));
            Assert.That(weather.Select(At), Is.EqualTo(new[] { 34.8f, 51.53f }).Within(0.1f));
            Assert.That(Timeline(ActionTimelineId.WarpEnd, images).Select(At), Is.EqualTo(Enumerable.Repeat(37.96f, 8)).Within(0.1f));
            Assert.That(Timeline(4575, images).Count + Timeline(4576, images).Count, Is.EqualTo(8));
            Assert.That(Timeline(4576, images), Has.Count.EqualTo(3));
            Assert.That(Timeline(ActionTimelineId.WarpStart, images).Select(At), Is.EqualTo(Enumerable.Repeat(51.51f, 3)).Within(0.1f));
            Assert.That(rows.Where(row => row["kind"] == "cast" && row["id"] == ActionId.BlastingZoneCast.ToString()).Select(At).First(), Is.EqualTo(40.04f).Within(ClockTolerance));
            Assert.That(sky, Has.Count.EqualTo(3));
        });
    }
}
