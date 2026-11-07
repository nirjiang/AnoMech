using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.Combat;

internal enum DutyActionKind
{
    Damage,
    SingleHeal,
    PartyHeal,
    SingleShield,
    PartyShield,
    SelfMitigation,
}

// Amount, ShieldAmount, ManaCost and Recast are UNVERIFIED prototype values. Cast times come from the
// Action sheet. A mitigation's status comes from JobActions, so a press applies it once.
internal sealed record DutyAction(
    uint ActionId,
    JobId Job,
    DutyActionKind Kind,
    float Range,
    uint Amount = 0,
    uint ShieldAmount = 0,
    ushort ShieldStatusId = 0,
    float ShieldSeconds = 0f,
    uint ManaCost = 0,
    float Recast = DutyActions.GcdSeconds,
    bool OnGcd = true);

// A small named kit for each job of the default party, not a rotation. A job missing here has no
// combat actions in the preview, and its player is told so rather than given another job's.
internal static class DutyActions
{
    public const float GcdSeconds = 2.5f;

    private const float MeleeRange = 3f;
    private const float RangedRange = 25f;
    private const float HealRange = 30f;
    private const uint WeaponskillHit = 1_000;

    private const ushort Galvanize = 297;
    private const float GalvanizeSeconds = 30f;

    public const uint HeavySwing = 31;
    public const uint FastBlade = 9;
    public const uint Rampart = 7531;
    public const uint GlareIII = 25859;
    public const uint CureII = 135;
    public const uint Medica = 124;
    public const uint BroilIV = 25865;
    public const uint Adloquium = 185;
    public const uint Succor = 186;
    public const uint TrueThrust = 75;
    public const uint LeapingOpo = 36945;
    public const uint Bootshine = 53;
    public const uint BurstShot = 16495;
    public const uint Fire = 141;

    public static readonly IReadOnlyList<DutyAction> All =
    [
        new(HeavySwing, JobId.Warrior, DutyActionKind.Damage, MeleeRange, WeaponskillHit),
        new(Rampart, JobId.Warrior, DutyActionKind.SelfMitigation, 0f, Recast: 90f, OnGcd: false),
        new(FastBlade, JobId.Paladin, DutyActionKind.Damage, MeleeRange, WeaponskillHit),
        new(Rampart, JobId.Paladin, DutyActionKind.SelfMitigation, 0f, Recast: 90f, OnGcd: false),
        new(GlareIII, JobId.WhiteMage, DutyActionKind.Damage, RangedRange, WeaponskillHit, ManaCost: 100),
        new(CureII, JobId.WhiteMage, DutyActionKind.SingleHeal, HealRange, 60_000, ManaCost: 1_000),
        new(Medica, JobId.WhiteMage, DutyActionKind.PartyHeal, 0f, 30_000, ManaCost: 1_000),
        new(BroilIV, JobId.Scholar, DutyActionKind.Damage, RangedRange, WeaponskillHit, ManaCost: 100),
        new(Adloquium, JobId.Scholar, DutyActionKind.SingleShield, HealRange, 40_000, 40_000, Galvanize, GalvanizeSeconds, 1_000),
        new(Succor, JobId.Scholar, DutyActionKind.PartyShield, 0f, 20_000, 25_000, Galvanize, GalvanizeSeconds, 1_000),
        new(TrueThrust, JobId.Dragoon, DutyActionKind.Damage, MeleeRange, WeaponskillHit),
        // Bootshine's upgrade at the preview's level, listed first for the bots; a press of either lands.
        new(LeapingOpo, JobId.Monk, DutyActionKind.Damage, MeleeRange, WeaponskillHit),
        new(Bootshine, JobId.Monk, DutyActionKind.Damage, MeleeRange, WeaponskillHit),
        new(BurstShot, JobId.Bard, DutyActionKind.Damage, RangedRange, WeaponskillHit),
        new(Fire, JobId.BlackMage, DutyActionKind.Damage, RangedRange, WeaponskillHit, ManaCost: 200),
    ];

    private static readonly Dictionary<(JobId Job, uint ActionId), DutyAction> ByJobAndId = All.ToDictionary(a => (a.Job, a.ActionId));

    public static DutyAction? Find(JobId job, uint actionId) => ByJobAndId.GetValueOrDefault((job, actionId));

    public static IEnumerable<DutyAction> Of(JobId job) => All.Where(a => a.Job == job);

    public static bool Supports(JobId job) => All.Any(a => a.Job == job);
}
