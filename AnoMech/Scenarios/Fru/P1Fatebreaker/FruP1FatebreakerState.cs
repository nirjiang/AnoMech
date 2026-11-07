using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P1Fatebreaker;

// One pull's rolls, the same whichever strat plays it. Spots are clock positions: 0 = north, then
// clockwise in eighths; the four axes through the centre are spots 0-3 and their opposites. Which
// branches retail favours is UNVERIFIED: each legal one is equally likely here.
public sealed class FruP1FatebreakerState
{
    // UNVERIFIED: read off a diagram, or chosen where only the direction is known.
    public const float EdgeRadius = 20f;
    public const float HaloRadius = 15f;
    public const float InnerNorthImageRadius = 7.5f;

    public const int PartySize = 8;
    public const int FaithTetherCount = 4;
    public const int TowerSoakerTotal = 6;

    public static readonly IReadOnlyList<int> WestHaloSpots = [0, 7, 6, 5];
    public static readonly IReadOnlyList<int> EastHaloSpots = [1, 2, 3, 4];

    public const int NorthSpot = 0;
    public const int NorthEastSpot = 1;
    public const int SouthEastSpot = 3;
    public const int SouthWestSpot = 5;
    public const int WestSpot = 6;
    public const int NorthWestSpot = 7;

    // Each north to south: 2-2-2, 1-2-3 and 1-1-4 are the only sets, in any order.
    public static readonly IReadOnlyList<IReadOnlyList<int>> LegalTowerSoakers =
    [
        [2, 2, 2],
        [1, 2, 3],
        [1, 3, 2],
        [2, 1, 3],
        [2, 3, 1],
        [3, 1, 2],
        [3, 2, 1],
        [1, 1, 4],
        [1, 4, 1],
        [4, 1, 1],
    ];

    public bool FirstCyclonicPairs { get; }
    public bool UtopianStack { get; }
    public IReadOnlyList<int> BlastingImages { get; }
    public bool ImageCyclonicPairs { get; }
    public bool LargeIsFire { get; }
    public bool FlameOnWest { get; }
    public IReadOnlyList<PartyRole> BoundTargets { get; }
    public IReadOnlyList<PartyRole> FaithTargets { get; }
    public IReadOnlyList<bool> FaithFire { get; }
    public bool ExplosionFire { get; }
    public bool TowersEast { get; }
    public IReadOnlyList<int> TowerSoakers { get; }

    // Set by the scenario for its run; the one way a strat's tank takes the boss.
    internal FruP1Enmity? Targeting { get; set; }

    public FruP1FatebreakerState(Rng rng, FruP1FatebreakerStateOverrides overrides)
    {
        FirstCyclonicPairs = overrides.FirstCyclonicPairs ?? rng.NextBool();
        UtopianStack = overrides.UtopianStack ?? rng.NextBool();
        BlastingImages = Checked(overrides.BlastingImages, IsLegalBlastingImages) ?? RollBlastingImages(rng);
        ImageCyclonicPairs = overrides.ImageCyclonicPairs ?? rng.NextBool();
        LargeIsFire = overrides.LargeIsFire ?? rng.NextBool();
        FlameOnWest = overrides.FlameOnWest ?? rng.NextBool();
        BoundTargets = Checked(overrides.BoundTargets, targets => IsDistinctRoles(targets, 2)) ?? RollRoles(rng, 2);
        FaithTargets = Checked(overrides.FaithTargets, targets => IsDistinctRoles(targets, FaithTetherCount)) ?? RollRoles(rng, FaithTetherCount);
        FaithFire = Checked(overrides.FaithFire, IsLegalFaithFire) ?? RollFaithFire(rng);
        ExplosionFire = overrides.ExplosionFire ?? rng.NextBool();
        TowersEast = overrides.TowersEast ?? rng.NextBool();
        TowerSoakers = Checked(overrides.TowerSoakers, IsLegalTowerSoakers) ?? LegalTowerSoakers[rng.Next(LegalTowerSoakers.Count)];
    }

    // The side of the halos that cast small: everywhere on the other side is under a large one.
    public float SafeSideX => FlameOnWest == LargeIsFire ? 1f : -1f;

    public int SafeAxis => Enumerable.Range(0, 4).Single(axis => BlastingImages.All(spot => spot % 4 != axis));

    public IReadOnlyList<int> FlameSpots => FlameOnWest ? WestHaloSpots : EastHaloSpots;
    public IReadOnlyList<int> LevinSpots => FlameOnWest ? EastHaloSpots : WestHaloSpots;

    public static float Bearing(int spot) => MathF.PI - spot * MathF.PI / 4f;

    public static Vector3 AtSpot(int spot, float radius)
    {
        var bearing = Bearing(spot);
        return new Vector3(MathF.Sin(bearing), 0f, MathF.Cos(bearing)) * radius;
    }

    public static Placement EdgeImage(int spot) => new(AtSpot(spot, EdgeRadius), Bearing(spot) + MathF.PI);

    // UNVERIFIED: positions read off a diagram; the lane order and the side are the observed part.
    public Vector3 TowerPosition(int tower) => new((TowersEast ? 1f : -1f) * (tower == 1 ? 13f : 11f), 0f, (tower - 1) * 8.5f);

    public static bool IsLegalBlastingImages(IReadOnlyList<int> spots)
        => spots.Count == 3 && spots.All(spot => spot is >= 0 and < 8) && spots.Select(spot => spot % 4).Distinct().Count() == 3;

    public static bool IsDistinctRoles(IReadOnlyList<PartyRole> roles, int count)
        => roles.Count == count && roles.Distinct().Count() == count && roles.All(role => (int)role is >= 0 and < PartySize);

    public static bool IsLegalFaithFire(IReadOnlyList<bool> fire) => fire.Count == FaithTetherCount;

    public static bool IsLegalTowerSoakers(IReadOnlyList<int> soakers)
        => LegalTowerSoakers.Any(legal => legal.SequenceEqual(soakers));

    private static T? Checked<T>(T? value, Func<T, bool> isLegal) where T : class
    {
        if (value is not null && !isLegal(value)) throw new ArgumentException($"Illegal P1 override: {value}.");
        return value;
    }

    private static IReadOnlyList<int> RollBlastingImages(Rng rng)
    {
        var safeAxis = rng.Next(4);
        return Enumerable.Range(0, 4)
            .Where(axis => axis != safeAxis)
            .Select(axis => axis + (rng.NextBool() ? 4 : 0))
            .ToList();
    }

    private static IReadOnlyList<PartyRole> RollRoles(Rng rng, int count)
    {
        var pool = Enumerable.Range(0, PartySize).ToList();
        var picked = new List<PartyRole>(count);
        for (var i = 0; i < count; i++)
        {
            var index = rng.Next(pool.Count);
            picked.Add((PartyRole)pool[index]);
            pool.RemoveAt(index);
        }
        return picked;
    }

    private static IReadOnlyList<bool> RollFaithFire(Rng rng)
        => Enumerable.Range(0, FaithTetherCount).Select(_ => rng.NextBool()).ToList();
}
