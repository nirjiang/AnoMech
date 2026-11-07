using System.Collections.Generic;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;

namespace AnoMech.Core.Combat;

// UNVERIFIED, every value: prototype units chosen to exercise the combat loop, never measured against
// the game. They apply only to a run that opted in; the practice scenarios' values stay their own.
internal sealed record DutyTuning
{
    public required uint BossMaxHp { get; init; }
    public uint TankMaxHp { get; init; } = 300_000;
    public uint MemberMaxHp { get; init; } = 200_000;
    public uint MaxMp { get; init; } = 10_000;
    public uint MpPerTick { get; init; } = 200;
    public float MpTickSeconds { get; init; } = 3f;
    public uint NormalHit { get; init; } = 12_000;
    public uint TankBusterHit { get; init; } = 60_000;
    // By action id, ahead of the severity's default.
    public IReadOnlyDictionary<uint, uint> HitOverrides { get; init; } = new Dictionary<uint, uint>();

    public uint MaxHp(PartyRole role) => role is PartyRole.MainTank or PartyRole.OffTank ? TankMaxHp : MemberMaxHp;

    public uint HitAmount(uint actionId, Severity severity)
    {
        if (severity.Kind == SeverityKind.Harmless) return 0;
        if (HitOverrides.TryGetValue(actionId, out var amount)) return amount;
        return severity.Kind == SeverityKind.TankBuster ? TankBusterHit : NormalHit;
    }
}
