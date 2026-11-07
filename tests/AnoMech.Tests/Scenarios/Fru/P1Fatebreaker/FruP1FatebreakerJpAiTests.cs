using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruP1FatebreakerJpAiTests
{
    private const uint LongFight = 1_000_000_000;
    private const float CastBegins = 31f;

    private static ScenarioRun Run(int seed, Action<FruP1FatebreakerStateOverrides> overrides, float stopAt, Action<ScenarioProbe>? probe = null, PartyRole? seat = null)
        => ScenarioRun.Execute(typeof(FruP1FatebreakerScenario), 0, seed, new ScenarioRunOptions
        {
            StopAt = stopAt,
            PlayerRole = seat,
            Probe = probe,
            Overrides = o =>
            {
                var forced = (FruP1FatebreakerStateOverrides)o;
                forced.BossMaxHp = LongFight;
                overrides(forced);
            },
        });

    private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    [TestCase(PartyRole.MainTank)]
    [TestCase(PartyRole.OffTank)]
    [TestCase(PartyRole.RegenHealer)]
    [TestCase(PartyRole.ShieldHealer)]
    [TestCase(PartyRole.MeleeDpsA)]
    [TestCase(PartyRole.MeleeDpsB)]
    [TestCase(PartyRole.PhysRangedDps)]
    [TestCase(PartyRole.CasterDps)]
    public void EverySeatClearsAPullWithNoDeathsAtTheDefaultTuning(PartyRole seat)
    {
        var run = ScenarioRun.Execute(typeof(FruP1FatebreakerScenario), 0, 100 + (int)seat, new ScenarioRunOptions { PlayerRole = seat });
        Assert.That(run.Passed, Is.True, run.ToString);
    }

    private static IEnumerable<TestCaseData> FaithSequences()
    {
        for (var bits = 0; bits < 16; bits++)
        {
            var fire = Enumerable.Range(0, 4).Select(i => ((bits >> i) & 1) == 1).ToArray();
            yield return new TestCaseData((object)fire).SetName($"Faith {string.Concat(fire.Select(f => f ? 'F' : 'L'))}");
        }
    }

    [TestCaseSource(nameof(FaithSequences))]
    public void EveryFaithSequenceClearsWithNoDeaths(bool[] fire)
    {
        var run = Run(7, o => o.FaithFire = fire, 116f);
        Assert.That(run.Passed, Is.True, run.ToString);
    }

    private static IEnumerable<TestCaseData> TowerSets()
        => from soakers in FruP1FatebreakerState.LegalTowerSoakers
           from fire in new[] { true, false }
           from east in new[] { true, false }
           select new TestCaseData(soakers.ToArray(), fire, east).SetName($"Towers {string.Join("-", soakers)} {(fire ? "fire" : "lightning")} {(east ? "east" : "west")}");

    [TestCaseSource(nameof(TowerSets))]
    public void EveryTowerSetIsSoakedExactlyWithNoDeaths(int[] soakers, bool fire, bool east)
    {
        var run = Run(11, o =>
        {
            o.TowerSoakers = soakers;
            o.ExplosionFire = fire;
            o.TowersEast = east;
        }, 148f);
        Assert.That(run.Passed, Is.True, run.ToString);
    }

    [TestCase(true, true, true, true)]
    [TestCase(true, false, false, true)]
    [TestCase(false, true, true, false)]
    [TestCase(false, false, false, false)]
    public void EveryCyclonicAndUtopianBranchClearsWithNoDeaths(bool firstPairs, bool stack, bool imagePairs, bool largeFire)
    {
        foreach (var flameWest in new[] { true, false })
            for (var safeAxis = 0; safeAxis < 4; safeAxis++)
            {
                var axis = safeAxis;
                var run = Run(13, o =>
                {
                    o.FirstCyclonicPairs = firstPairs;
                    o.UtopianStack = stack;
                    o.ImageCyclonicPairs = imagePairs;
                    o.LargeIsFire = largeFire;
                    o.FlameOnWest = flameWest;
                    o.BlastingImages = Enumerable.Range(0, 4).Where(a => a != axis).ToList();
                }, 82f);
                Assert.That(run.Passed, Is.True, run.ToString);
            }
    }

    private static IEnumerable<TestCaseData> BoundPairs()
        => from a in Enumerable.Range(0, 8)
           from b in Enumerable.Range(a + 1, 7 - a)
           select new TestCaseData((PartyRole)a, (PartyRole)b).SetName($"Bound on {(PartyRole)a} and {(PartyRole)b}");

    [TestCaseSource(nameof(BoundPairs))]
    public void EveryBoundPairStacksFourAndFourWithNoMortality(PartyRole first, PartyRole second)
    {
        var mortality = false;
        var run = Run(17, o => o.BoundTargets = [first, second], 80f, p =>
        {
            if (PerRole.All.Any(role => p.Member(role)?.HasStatus(StatusId.MarkOfMortality) == true)) mortality = true;
        });
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(mortality, Is.False);
        });
    }

    [Test]
    public void TheRootedBoundTargetHoldsThenWalksBackAfterItsRootEnds()
    {
        var (rooted, afterRoot, walkedBack) = (Vector3.Zero, Vector3.Zero, Vector3.Zero);
        var run = Run(21, o => o.BoundTargets = [PartyRole.MeleeDpsA, PartyRole.RegenHealer], 83f, p =>
        {
            var holder = p.Member(PartyRole.MeleeDpsA)!;
            if (p.Crossed(76.3f)) rooted = holder.Position;
            if (p.Crossed(78.9f)) afterRoot = holder.Position;
            if (p.Crossed(82f)) walkedBack = holder.Position;
        }, PartyRole.CasterDps);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(Flat(rooted, afterRoot), Is.LessThan(0.01f), "rooted");
            Assert.That(Flat(afterRoot, walkedBack), Is.GreaterThan(1f), "a return order issued after the root still lands");
        });
    }

    [Test]
    public void AFaithHolderHoldsRootedThenSwapsBehindTheNextHolder()
    {
        var (rooted, atBait, swapped) = (Vector3.Zero, Vector3.Zero, Vector3.Zero);
        var (first, third) = (PartyRole.MeleeDpsA, PartyRole.RegenHealer);
        var run = Run(23, o =>
        {
            o.FaithTargets = [first, PartyRole.MainTank, third, PartyRole.OffTank];
            o.FaithFire = [false, false, false, false];
        }, 112.5f, p =>
        {
            var holder = p.Member(first)!;
            if (p.Crossed(103.5f)) rooted = holder.Position;
            if (p.Crossed(106.1f)) atBait = holder.Position;
            if (p.Crossed(108.5f)) swapped = holder.Position;
        }, PartyRole.CasterDps);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(Flat(rooted, atBait), Is.LessThan(0.01f), "rooted");
            Assert.That(Flat(atBait, swapped), Is.GreaterThan(1f), "the swap order issued after the root still lands");
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    public void TheUtopianPartyLeavesAtTheCastGoesThroughTheCentreAndReachesItsClockEarly(int seed)
    {
        var atCast = new Dictionary<PartyRole, Vector3>();
        var leftBy = new Dictionary<PartyRole, float>();
        var closest = PerRole.All.ToDictionary(role => role, _ => float.MaxValue);
        var closestAt = new Dictionary<PartyRole, float>();
        var outAt = new Dictionary<PartyRole, float>();
        var run = Run(seed, _ => { }, 40f, p =>
        {
            if (p.Time < CastBegins) return;
            foreach (var role in PerRole.All)
            {
                var at = p.Member(role)!.Position;
                if (!atCast.ContainsKey(role)) atCast[role] = at;
                if (!leftBy.ContainsKey(role) && Flat(at, atCast[role]) > 0.1f) leftBy[role] = p.Time;
                var fromCentre = Flat(at, Vector3.Zero);
                if (fromCentre < closest[role])
                {
                    closest[role] = fromCentre;
                    closestAt[role] = p.Time;
                }
                if (!outAt.ContainsKey(role) && Flat(at, FruP1FatebreakerJpAi.UtopianOuterSpot(role)) < 0.3f) outAt[role] = p.Time;
            }
        }, PartyRole.CasterDps);

        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.Multiple(() =>
        {
            foreach (var role in PerRole.All)
            {
                Assert.That(leftBy[role], Is.LessThan(CastBegins + 0.3f), $"{role} departs as the cast begins");
                Assert.That(closest[role], Is.LessThan(0.6f), $"{role} crosses the centre");
                Assert.That(outAt[role], Is.GreaterThan(closestAt[role]), $"{role} reaches the clock after the centre");
                Assert.That(outAt[role], Is.LessThan(38f), $"{role} is out well ahead of the tell");
            }
        });
    }

    [Test]
    public void TheEarlyWalkOutCannotKnowTheSafeAxis()
    {
        Dictionary<PartyRole, Vector3> Before(int axis)
        {
            var seen = new Dictionary<PartyRole, Vector3>();
            Run(3, o => o.BlastingImages = Enumerable.Range(0, 4).Where(a => a != axis).ToList(), 40.1f, p =>
            {
                if (p.Time < 40f) foreach (var role in PerRole.All) seen[role] = p.Member(role)!.Position;
            }, PartyRole.CasterDps);
            return seen;
        }

        var (a, b) = (Before(0), Before(2));
        foreach (var role in PerRole.All)
            Assert.That(Flat(a[role], b[role]), Is.LessThan(0.01f), role.ToString());
    }

    [Test]
    public void TheMainTanksLeanMakesTheOffTankItsNearestAtTheBurnMark()
    {
        SimCharacter? nearest = null;
        SimCharacter? offTank = null;
        var run = Run(5, _ => { }, 41f, p =>
        {
            if (!p.Crossed(40.3f)) return;
            offTank = p.Member(PartyRole.OffTank);
            var mainTank = p.Member(PartyRole.MainTank)!;
            nearest = p.World.Party.Find.Closest(mainTank.Position, mainTank);
        }, PartyRole.CasterDps);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(nearest, Is.SameAs(offTank));
        });
    }

    [TestCase(0, PartyRole.MainTank, 0, PartyRole.OffTank, 4)]
    [TestCase(1, PartyRole.RegenHealer, 5, PartyRole.ShieldHealer, 1)]
    [TestCase(2, PartyRole.MeleeDpsA, 6, PartyRole.MeleeDpsB, 2)]
    [TestCase(3, PartyRole.PhysRangedDps, 7, PartyRole.CasterDps, 3)]
    public void EachGroupTakesTheEndOfTheSafeAxisOnItsOwnHalf(int axis, PartyRole west, int westEnd, PartyRole east, int eastEnd)
    {
        var (westAt, eastAt) = (Vector3.Zero, Vector3.Zero);
        var run = Run(9, o =>
        {
            o.BlastingImages = Enumerable.Range(0, 4).Where(a => a != axis).ToList();
            o.UtopianStack = true;
        }, 49f, p =>
        {
            if (!p.Crossed(48.5f)) return;
            westAt = p.Member(west)!.Position;
            eastAt = p.Member(east)!.Position;
        }, PartyRole.CasterDps);
        Assert.Multiple(() =>
        {
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(Flat(westAt, FruP1FatebreakerState.AtSpot(westEnd, 13f)), Is.LessThan(0.5f));
            Assert.That(Flat(eastAt, FruP1FatebreakerState.AtSpot(eastEnd, 13f)), Is.LessThan(0.5f));
        });
    }
}
