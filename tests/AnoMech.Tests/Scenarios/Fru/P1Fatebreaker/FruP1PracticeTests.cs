using System.Globalization;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruP1PracticeTests
{
    private const float LongestDrill = 65f;

    private static readonly Dictionary<Type, uint[]> Signatures = new()
    {
        [typeof(FruP1CyclonicBreakBossPractice)] = [ActionId.CyclonicBreakBossStack, ActionId.CyclonicBreakFirst, ActionId.CyclonicBreakRest, ActionId.CyclonicBreakSinsmoke, ActionId.PowderMarkTrail, ActionId.FatebreakerAutoAttack],
        [typeof(FruP1UtopianSkyPractice)] = [ActionId.PowderMarkTrail, ActionId.BurnMark, ActionId.FatebreakerAutoAttack, ActionId.UtopianSkyStack, ActionId.BlastingZone, ActionId.SinboundFire, ActionId.CyclonicBreakImageSpread, ActionId.CyclonicBreakFirst, ActionId.CyclonicBreakRest, ActionId.CyclonicBreakSinsmite],
        [typeof(FruP1TurnOfTheHeavensPractice)] = [ActionId.TurnOfHeavensBurntStrikeLightning, ActionId.TurnOfHeavensBurnout, ActionId.TurnOfHeavensFire, ActionId.TurnOfHeavensBurntStrikeFire, ActionId.TurnOfHeavensBlastburn, ActionId.BrightfireLarge, ActionId.BrightfireSmall, ActionId.BoundOfFaith, ActionId.TurnOfHeavensSolemnCharge, ActionId.BoundOfFaithSinsmoke, ActionId.FatebreakerAutoAttack],
        [typeof(FruP1FallOfFaithPractice)] = [ActionId.BurnishedGlory, ActionId.FatebreakerAutoAttack, ActionId.FallOfFaithFire, ActionId.FallOfFaithSolemnChargeFire, ActionId.FallOfFaithSolemnChargeLightning, ActionId.FallOfFaithSinblaze, ActionId.FallOfFaithSinsmite, ActionId.FallOfFaithBowShock],
        [typeof(FruP1ExplosionPractice)] = [ActionId.PowderMarkTrail, ActionId.BurnMark, ActionId.FatebreakerAutoAttack, ActionId.ExplosionBurntStrikeFire, ActionId.ExplosionBlastburn, ActionId.ExplosionFireOne, ActionId.ExplosionFireTwo, ActionId.ExplosionFireThree],
    };

    private static IEnumerable<Type> Drills => ScenarioCatalog.Create().Scenarios.OfType<FruP1PracticeScenario>().Select(drill => drill.GetType());

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

    private static DutyHarness Start(Type drill, bool withStrat = true)
    {
        var duty = new DutyHarness(drill, PartyRole.CasterDps, debugBot: withStrat) { SelectedAi = withStrat ? 0 : null };
        Forced((FruP1FatebreakerStateOverrides)duty.Scenario.SettingsOverrides!);
        duty.Game.GodMode = !withStrat;
        duty.Start();
        return duty;
    }

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    [Test]
    public void EveryDrillHasItsOwnSignature()
        => Assert.That(Signatures.Keys, Is.EquivalentTo(Drills));

    [TestCaseSource(nameof(Drills))]
    public void ADrillPlaysOnlyItsOwnMechanicAndClearsAfterItsLastHit(Type drill)
    {
        using var duty = Start(drill);
        var boss = duty.Combat.Boss!;
        duty.RunUntil(() => duty.Game.Status == SessionStatus.Succeeded, LongestDrill, drill.Name);

        var own = Signatures[drill];
        var foreign = Signatures.Values.SelectMany(ids => ids).Append(ActionId.FatebreakerEnrage).Except(own).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Killed, Is.Empty);
            Assert.That(boss.Dead, Is.False, "no kill just to report the clear");
            foreach (var id in own) Assert.That(duty.AbilityCount(id), Is.Positive, $"own action {id}");
            foreach (var id in foreign) Assert.That(duty.AbilityCount(id), Is.Zero, $"foreign action {id}");
        });
    }

    [TestCase(typeof(FruP1CyclonicBreakBossPractice), 0f, 27.65f)]
    [TestCase(typeof(FruP1UtopianSkyPractice), 16.2f, 63.48f)]
    [TestCase(typeof(FruP1TurnOfTheHeavensPractice), 53.6f, 81.82f)]
    [TestCase(typeof(FruP1FallOfFaithPractice), 78.26f, 137.61f)]
    [TestCase(typeof(FruP1ExplosionPractice), 122.11f, 148.75f)]
    public void AGroupStartsAndClearsOnTheFightsClock(Type drill, float start, float clearAt)
    {
        using var duty = Start(drill);
        Assert.That(duty.Now(), Is.EqualTo(start).Within(0.1f));
        var last = duty.Now();
        duty.RunUntil(() =>
        {
            if (duty.Game.Status == SessionStatus.Succeeded) return true;
            last = duty.Now();
            return false;
        }, LongestDrill, drill.Name);
        Assert.That(last, Is.EqualTo(clearAt).Within(0.1f));
    }

    [Test]
    public void TheOpeningGroupEndsAfterTheFirstTankSwapWithoutTheBurnMark()
    {
        using var duty = Start(typeof(FruP1CyclonicBreakBossPractice));
        duty.RunUntil(() => duty.Game.Status == SessionStatus.Succeeded, LongestDrill, "the clear");
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityCount(ActionId.PowderMarkTrail), Is.EqualTo(1));
            Assert.That(duty.AbilityRows(ActionId.FatebreakerAutoAttack).Last()["target"], Is.Not.EqualTo(duty.AbilityRows(ActionId.PowderMarkTrail).Single()["target"]));
        });
    }

    private static (IReadOnlyList<float> Powder, IReadOnlyList<float> Burn, IReadOnlyList<float> Autos) PowderTimes(Type scenario)
    {
        using var duty = Start(scenario);
        duty.RunTo(41f);
        return (duty.AbilityTimes(ActionId.PowderMarkTrail), duty.AbilityTimes(ActionId.BurnMark),
            duty.AbilityTimes(ActionId.FatebreakerAutoAttack).Where(time => time >= 16.3f).ToList());
    }

    [Test]
    public void TheUtopianGroupsPowderMarkMatchesTheFullFight()
    {
        var full = PowderTimes(typeof(FruP1FatebreakerScenario));
        var group = PowderTimes(typeof(FruP1UtopianSkyPractice));
        Assert.Multiple(() =>
        {
            Assert.That(group.Powder, Is.EqualTo(full.Powder).Within(0.05f));
            Assert.That(group.Burn, Is.EqualTo(full.Burn).Within(0.05f));
            Assert.That(group.Autos, Is.EqualTo(full.Autos).Within(0.05f));
            Assert.That(group.Autos[0], Is.EqualTo(17.8f).Within(0.05f));
        });
    }

    [Test]
    public void TheUtopianDrillLeavesAtTheCastThroughTheCentreToTheClock()
    {
        const float castBegins = 31f;
        using var duty = Start(typeof(FruP1UtopianSkyPractice));
        var member = duty.Member(PartyRole.OffTank);
        duty.RunTo(castBegins - 0.05f);
        var atCast = member.Position;
        var leftInTime = false;
        var reachedCentre = false;
        duty.RunUntil(() =>
        {
            leftInTime |= duty.Now() < castBegins + 0.3f && Flat(member.Position, atCast) > 0.1f;
            reachedCentre |= Flat(member.Position, Vector3.Zero) < 0.6f;
            return reachedCentre && Flat(member.Position, FruP1FatebreakerJpAi.UtopianOuterSpot(PartyRole.OffTank)) < 0.3f;
        }, 7f, "the outer clock after the centre");
        Assert.Multiple(() =>
        {
            Assert.That(leftInTime, Is.True, "departs as the cast begins");
            Assert.That(duty.Now(), Is.LessThan(38f));
        });
    }

    [TestCase(typeof(FruP1FallOfFaithPractice))]
    [TestCase(typeof(FruP1FatebreakerScenario))]
    public void TheFaithPartyLinesUpWestToEastSouthOfTheBossInPriorityOrderDuringBurnishedGlory(Type scenario)
    {
        PartyRole[] order =
        [
            PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank,
            PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
        ];
        using var duty = Start(scenario);
        duty.RunTo(85.5f);
        Assert.Multiple(() =>
        {
            for (var i = 0; i < order.Length; i++)
            {
                var at = duty.Member(order[i]).Position;
                Assert.That(at.Z, Is.EqualTo(3f).Within(0.3f), $"{order[i]} z");
                Assert.That(at.X, Is.EqualTo((i - 3.5f) * 1.5f).Within(0.3f), $"{order[i]} x");
            }
        });
    }

    [Test]
    public void ASinblazeIsSelfTargetedFromTheHolderAndAimedAtTheNearestNotTheFarthest()
    {
        using var duty = Start(typeof(FruP1FatebreakerScenario), withStrat: false);
        duty.RunTo(100.5f);
        duty.PlaceAll(15f, 0f);
        duty.Place(PartyRole.RegenHealer, -10f, 0f);
        duty.Place(PartyRole.MeleeDpsA, -14f, 0f);
        duty.RunTo(106.5f);

        var holder = duty.Game.World.Coordinates.ToGlobal(duty.Member(PartyRole.RegenHealer).Position);
        var row = duty.AbilityRows(ActionId.FallOfFaithSinblaze).First();
        Assert.Multiple(() =>
        {
            Assert.That(row["target"], Is.EqualTo(row["actor"]), "self-targeted");
            Assert.That(float.Parse(row["x"], CultureInfo.InvariantCulture), Is.EqualTo(holder.X).Within(0.1f));
            Assert.That(float.Parse(row["z"], CultureInfo.InvariantCulture), Is.EqualTo(holder.Z).Within(0.1f));
            Assert.That(float.Parse(row["rot"], CultureInfo.InvariantCulture), Is.EqualTo(-MathF.PI / 2f).Within(0.05f), "aims west at the nearest");
        });
    }

    [Test]
    public void TheHeavensGroupShowsItsImagesAndTheBossOnlyAtTheirReplayMoments()
    {
        using var duty = Start(typeof(FruP1TurnOfTheHeavensPractice));
        var boss = duty.Combat.Boss!;
        var awayAtStart = !boss.Targetable;
        duty.RunUntil(() => duty.Game.Status == SessionStatus.Succeeded, LongestDrill, "the clear");

        var rows = duty.Fake.Timeline.Rows;
        float At(Dictionary<string, string> row) => float.Parse(row["t"], CultureInfo.InvariantCulture);
        List<Dictionary<string, string>> Timeline(ushort timelineId)
            => rows.Where(row => row["kind"].StartsWith("ac") && row["id"] == "0x197" && row["p"] == $"0x{timelineId:X}").ToList();
        var bossId = DutyHarness.Id(boss);
        var bossSpawn = rows.Single(row => row["kind"] == "spawn" && row["actor"] == bossId);
        var bossSpawnX = float.Parse(bossSpawn["x"], CultureInfo.InvariantCulture);
        var bossSpawnZ = float.Parse(bossSpawn["z"], CultureInfo.InvariantCulture);
        var bound = duty.AbilityRows(ActionId.BoundOfFaith).Select(row => row["actor"]).Distinct().ToList();
        var boundSpawns = rows.Where(row => row["kind"] == "spawn" && bound.Contains(row["actor"])).ToList();
        var halos = rows.Where(row => row["kind"] == "spawn" && row["id"] is var id
            && (id == BNpcBaseId.HaloOfFlame.ToString() || id == BNpcBaseId.HaloOfLevin.ToString())).ToList();
        var boundStates = rows.Where(row => row["kind"].StartsWith("ac") && row["id"] == "0x3F" && bound.Contains(row["actor"])).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(awayAtStart, Is.True);
            Assert.That(rows.Single(row => row["kind"] == "spawn" && row["id"] == BNpcBaseId.Fatebreaker.ToString())["detail"],
                Does.Contain("HiddenUntilShown").And.Contain("targetable=0"));
            Assert.That(Timeline(ActionTimelineId.WarpEnd).Where(row => row["actor"] == bossId).Select(At), Is.EqualTo(new[] { 80.4f }).Within(0.1f));
            Assert.That(boss.Targetable, Is.True);
            Assert.That(Timeline(ActionTimelineId.WarpEnd).Where(row => row["actor"] != bossId).Select(At),
                Is.EqualTo(new[] { 55.8f, 55.8f, 61.8f, 63.8f, 63.8f }).Within(0.1f));
            Assert.That(Timeline(ActionTimelineId.WarpStart).Select(At), Is.EqualTo(new[] { 66.3f, 69f, 75f, 80.6f, 80.6f }).Within(0.1f));

            var selfCasts = duty.AbilityRows(ActionId.TurnOfHeavensFire).Concat(duty.AbilityRows(ActionId.BoundOfFaith)).ToList();
            Assert.That(selfCasts, Has.Count.EqualTo(3));
            Assert.That(selfCasts.All(row => row["actor"] == row["target"] && row["actor"] == row["targets"]), Is.True);

            Assert.That(bound, Has.Count.EqualTo(2));
            Assert.That(boundSpawns.Select(row => float.Parse(row["x"], CultureInfo.InvariantCulture) - bossSpawnX).Order(),
                Is.EqualTo(new[] { -14.14f, 14.14f }).Within(0.05f));
            Assert.That(boundSpawns.Select(row => float.Parse(row["z"], CultureInfo.InvariantCulture) - bossSpawnZ),
                Is.All.EqualTo(14.14f).Within(0.05f));
            Assert.That(duty.AbilityRows(ActionId.FloatingFetters).Select(row => row["actor"]), Is.EquivalentTo(bound));
            Assert.That(duty.AbilityRows(ActionId.TurnOfHeavensSolemnCharge).Select(row => row["actor"]), Is.EquivalentTo(bound));
            Assert.That(duty.AbilityRows(ActionId.FloatingFetters).Select(row => row["anim_lock"]), Is.All.EqualTo("2.10"));
            Assert.That(duty.AbilityRows(ActionId.TurnOfHeavensSolemnCharge).Select(row => row["anim_lock"]), Is.All.EqualTo("3.10"));
            Assert.That(duty.AbilityTimes(ActionId.BoundOfFaith).First(), Is.LessThan(duty.AbilityTimes(ActionId.FloatingFetters).First()));
            Assert.That(duty.AbilityTimes(ActionId.FloatingFetters).First(), Is.LessThan(duty.AbilityTimes(ActionId.TurnOfHeavensSolemnCharge).First()));
            Assert.That(duty.AbilityTimes(ActionId.TurnOfHeavensSolemnCharge).First(), Is.LessThanOrEqualTo(duty.AbilityTimes(ActionId.BoundOfFaithSinsmoke).First()));
            Assert.That(boundStates.Select(row => row["p"]), Is.EqualTo(new[] { "0x1C", "0x1C", "", "" }));
            Assert.That(boundStates.Select(At), Is.EqualTo(new[] { 76.5f, 76.5f, 79.1f, 79.1f }).Within(0.1f));

            Assert.That(halos, Has.Count.EqualTo(8));
            Assert.That(halos.Select(At), Is.All.EqualTo(64f).Within(0.05f));
            foreach (var halo in halos)
                Assert.That(halo["detail"], Does.Contain($"layout=0x{(halo["id"] == BNpcBaseId.HaloOfFlame.ToString() ? LayoutId.HaloOfFlame : LayoutId.HaloOfLevin):X} "));
            Assert.That(rows.Where(row => row["kind"].StartsWith("ac") && (row["id"] == "0x0" || row["id"] == "0x4") && halos.Any(halo => halo["actor"] == row["actor"])), Is.Empty);
            Assert.That(duty.AbilityRows(ActionId.BrightfireLarge).Concat(duty.AbilityRows(ActionId.BrightfireSmall)).Select(row => row["anim_lock"]), Is.All.EqualTo("2.10"));
            Assert.That(rows.Where(row => row["kind"] == "despawn" && halos.Any(halo => halo["actor"] == row["actor"])).Select(At), Is.All.GreaterThan(75f));
        });
    }

    [Test]
    public void TheBoundDrillKeepsTheFightsClockForAReplacedMark()
    {
        using var duty = Start(typeof(FruP1TurnOfTheHeavensPractice), withStrat: false);
        duty.RunTo(64.5f);
        duty.PlaceAll(0f, 15f);
        duty.Place(PartyRole.MeleeDpsA, 0f, -19f);
        duty.Game.GodMode = false;
        duty.Member(PartyRole.MeleeDpsA).Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Frame();
        duty.Game.GodMode = true;
        var replacement = PerRole.All.Select(duty.Member).Single(member => member.IsAlive() && member.HasStatus(StatusId.FatedBurnMark) && !ReferenceEquals(member, duty.Member(PartyRole.MainTank)));
        duty.RunTo(78.5f);
        Assert.That(replacement.HasStatus(StatusId.FatedBurnMark), Is.True);
        duty.RunTo(79.5f);
        Assert.That(replacement.HasStatus(StatusId.FatedBurnMark), Is.False);
    }

    [Test]
    public void TheFaithGroupOutlastsBothGloryBleeds()
    {
        using var duty = Start(typeof(FruP1FallOfFaithPractice));
        duty.RunTo(100f);
        var caster = duty.Member(PartyRole.CasterDps);
        Assert.That(caster.HasStatus(StatusId.Bleeding), Is.True);
        duty.RunTo(130f);
        Assert.That(caster.HasStatus(StatusId.Bleeding), Is.True);
        Assert.That(duty.AbilityCount(ActionId.BurnishedGlory), Is.EqualTo(2));
        Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Running));
        duty.RunUntil(() => duty.Game.Status == SessionStatus.Succeeded, 10f, "the clear");
        Assert.That(caster.HasStatus(StatusId.Bleeding), Is.False);
    }

    [Test]
    public void AnUnsoakedTowersPenaltyLandsBeforeTheClear()
    {
        using var duty = Start(typeof(FruP1ExplosionPractice), withStrat: false);
        duty.RunTo(140f);
        duty.PlaceAll(1f, 0f);
        duty.RunUntil(() => duty.Game.Status is SessionStatus.Succeeded or SessionStatus.CompletedWithMistake, LongestDrill, "the clear");
        Assert.That(duty.AbilityCount(ActionId.UnmitigatedExplosionFire), Is.EqualTo(3));
    }

    [Test]
    public void TheHumansDeathRetriesTheSameDrillFromItsOwnStart()
    {
        using var duty = Start(typeof(FruP1UtopianSkyPractice));
        duty.RunTo(20f);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Frame();
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(duty.Now(), Is.LessThan(17f));
        });
        duty.RunUntil(() => duty.Game.Status == SessionStatus.Succeeded, LongestDrill, "the retry's clear");
        Assert.That(duty.AbilityCount(ActionId.BurnMark), Is.EqualTo(2));
    }
}
