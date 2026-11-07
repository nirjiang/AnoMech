using System.Collections.Generic;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P1Fatebreaker;

// Forced rolls for FruP1FatebreakerState; null leaves a field randomized. An illegal list is refused.
public sealed class FruP1FatebreakerStateOverrides
{
    public bool? FirstCyclonicPairs { get; set; }
    public bool? UtopianStack { get; set; }
    public IReadOnlyList<int>? BlastingImages { get; set; }
    public bool? ImageCyclonicPairs { get; set; }
    public bool? LargeIsFire { get; set; }
    public bool? FlameOnWest { get; set; }
    public IReadOnlyList<PartyRole>? BoundTargets { get; set; }
    public IReadOnlyList<PartyRole>? FaithTargets { get; set; }
    public IReadOnlyList<bool>? FaithFire { get; set; }
    public bool? ExplosionFire { get; set; }
    public bool? TowersEast { get; set; }
    public IReadOnlyList<int>? TowerSoakers { get; set; }

    // For a run that has to reach the later mechanics whatever the party's damage.
    public uint? BossMaxHp { get; set; }
}
