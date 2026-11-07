using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using static AnoMech.Scenarios.Fru.P1Fatebreaker.FruP1FatebreakerState;

namespace AnoMech.Tests;

public class FruP1FatebreakerStateTests
{
    private static FruP1FatebreakerState Roll(int seed, FruP1FatebreakerStateOverrides? overrides = null) => new(new Rng(seed), overrides ?? new());

    [Test]
    public void EverySeedRollsOnlyLegalBranches()
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var state = Roll(seed);
            Assert.Multiple(() =>
            {
                Assert.That(IsLegalBlastingImages(state.BlastingImages), Is.True);
                Assert.That(IsDistinctRoles(state.BoundTargets, 2), Is.True);
                Assert.That(IsDistinctRoles(state.FaithTargets, FaithTetherCount), Is.True);
                Assert.That(IsLegalFaithFire(state.FaithFire), Is.True);
                Assert.That(IsLegalTowerSoakers(state.TowerSoakers), Is.True);
                Assert.That(state.TowerSoakers.Sum(), Is.EqualTo(TowerSoakerTotal));
            });
        }
    }

    [Test]
    public void TheSameSeedRollsTheSamePull()
    {
        var (a, b) = (Roll(77), Roll(77));
        Assert.Multiple(() =>
        {
            Assert.That(b.BlastingImages, Is.EqualTo(a.BlastingImages));
            Assert.That(b.BoundTargets, Is.EqualTo(a.BoundTargets));
            Assert.That(b.FaithTargets, Is.EqualTo(a.FaithTargets));
            Assert.That(b.FaithFire, Is.EqualTo(a.FaithFire));
            Assert.That(b.TowerSoakers, Is.EqualTo(a.TowerSoakers));
            Assert.That(b.LargeIsFire, Is.EqualTo(a.LargeIsFire));
            Assert.That(b.ExplosionFire, Is.EqualTo(a.ExplosionFire));
        });
    }

    [Test]
    public void SeedsReachEveryLegalBranch()
    {
        var states = Enumerable.Range(0, 2000).Select(seed => Roll(seed)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(states.Select(s => string.Join(",", s.TowerSoakers)).Distinct().Count(), Is.EqualTo(LegalTowerSoakers.Count));
            Assert.That(states.Select(s => s.SafeAxis).Distinct().Count(), Is.EqualTo(4));
            Assert.That(states.Select(s => string.Join(",", s.FaithFire)).Distinct().Count(), Is.EqualTo(16));
            Assert.That(states.Select(s => (s.LargeIsFire, s.FlameOnWest)).Distinct().Count(), Is.EqualTo(4));
            Assert.That(states.Select(s => (s.FirstCyclonicPairs, s.ImageCyclonicPairs)).Distinct().Count(), Is.EqualTo(4));
            Assert.That(states.Select(s => s.UtopianStack).Distinct().Count(), Is.EqualTo(2));
            Assert.That(states.Select(s => (s.ExplosionFire, s.TowersEast)).Distinct().Count(), Is.EqualTo(4));
        });
    }

    private static IEnumerable<TestCaseData> IllegalOverrides()
    {
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { BlastingImages = [0, 4, 1] }).SetName("Two lines on one axis");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { BlastingImages = [0, 1] }).SetName("Two lines");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { BlastingImages = [0, 1, 8] }).SetName("A spot off the clock");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { BoundTargets = [PartyRole.MainTank, PartyRole.MainTank] }).SetName("One Bound target twice");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { FaithTargets = [PartyRole.MainTank, PartyRole.OffTank, PartyRole.CasterDps] }).SetName("Three tethers");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { FaithFire = [true, true, true] }).SetName("Three elements");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { TowerSoakers = [2, 2, 3] }).SetName("Seven soakers");
        yield return new TestCaseData(new FruP1FatebreakerStateOverrides { TowerSoakers = [3, 3, 0] }).SetName("An empty tower");
    }

    [TestCaseSource(nameof(IllegalOverrides))]
    public void AnIllegalOverrideIsRefused(FruP1FatebreakerStateOverrides overrides)
        => Assert.Throws<ArgumentException>(() => Roll(1, overrides));

    [Test]
    public void EverySixteenFaithSequenceIsAccepted()
    {
        for (var bits = 0; bits < 16; bits++)
        {
            var mask = bits;
            var fire = Enumerable.Range(0, FaithTetherCount).Select(i => ((mask >> i) & 1) == 1).ToList();
            Assert.That(Roll(1, new FruP1FatebreakerStateOverrides { FaithFire = fire }).FaithFire, Is.EqualTo(fire));
        }
    }

    [TestCase(true, true, 1f)]
    [TestCase(true, false, -1f)]
    [TestCase(false, true, -1f)]
    [TestCase(false, false, 1f)]
    public void TheSafeSideIsWhereTheSmallHalosAre(bool largeIsFire, bool flameOnWest, float expectedSide)
    {
        var state = Roll(1, new FruP1FatebreakerStateOverrides { LargeIsFire = largeIsFire, FlameOnWest = flameOnWest });
        var smallSpots = largeIsFire ? state.LevinSpots : state.FlameSpots;
        var averageX = smallSpots.Average(spot => AtSpot(spot, HaloRadius).X);
        Assert.Multiple(() =>
        {
            Assert.That(state.SafeSideX, Is.EqualTo(expectedSide));
            Assert.That(MathF.Sign(averageX), Is.EqualTo(MathF.Sign(expectedSide)));
        });
    }

    [Test]
    public void TheHalosFillEveryClockSpotInTwoRunsOfFour()
    {
        var spots = WestHaloSpots.Concat(EastHaloSpots).OrderBy(spot => spot);
        Assert.That(spots, Is.EqualTo(Enumerable.Range(0, 8)));
    }

    [TestCase(true, 1f)]
    [TestCase(false, -1f)]
    public void TheTowersStandInThreeLanesOnOneSide(bool east, float side)
    {
        var state = Roll(1, new FruP1FatebreakerStateOverrides { TowersEast = east });
        var towers = Enumerable.Range(0, 3).Select(state.TowerPosition).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(towers.All(tower => MathF.Sign(tower.X) == MathF.Sign(side)), Is.True);
            Assert.That(towers.Select(tower => tower.Z), Is.Ordered);
            Assert.That(towers.All(tower => tower.Length() + 4f <= 20f), Is.True, "a whole tower fits in the arena");
        });
    }

    [Test]
    public void TheSafeAxisIsTheOneNoLineRunsAlong()
    {
        var state = Roll(1, new FruP1FatebreakerStateOverrides { BlastingImages = [4, 1, 6] });
        Assert.That(state.SafeAxis, Is.EqualTo(3));
    }

    [TestCase(0, 0f, -20f)]
    [TestCase(2, 20f, 0f)]
    [TestCase(4, 0f, 20f)]
    [TestCase(6, -20f, 0f)]
    public void ClockSpotsRunClockwiseFromNorthAndImagesFaceTheCentre(int spot, float x, float z)
    {
        var image = EdgeImage(spot);
        Assert.Multiple(() =>
        {
            Assert.That(image.Position.X, Is.EqualTo(x).Within(0.01f));
            Assert.That(image.Position.Z, Is.EqualTo(z).Within(0.01f));
            Assert.That(image.Position.X + MathF.Sin(image.Rotation) * EdgeRadius, Is.EqualTo(0f).Within(0.01f));
            Assert.That(image.Position.Z + MathF.Cos(image.Rotation) * EdgeRadius, Is.EqualTo(0f).Within(0.01f));
        });
    }
}
