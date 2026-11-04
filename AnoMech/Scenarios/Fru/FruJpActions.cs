using AnoMech.Core.EnemyActions;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru;

public static class FruJpActions
{
    // UNVERIFIED: the 30% threshold approximates surviving a repeated front hit for movement practice.
    public static readonly EnemyAction HallowedWingsKnockback = new(ActionId.HallowedWingsKnockback)
    {
        Cast = new() { AnimationLock = 2.1f },
        Effects =
        [
            OnFront(4,
                Damage(VulnerableTo(StatusId.MagicVulnerabilityUp, requiredMitigation: 0.30f), split: WildCharge(front: 1, frontHit: TankBuster)),
                ApplyStatus(StatusId.MagicVulnerabilityUp, 6f, delayToEffectResult: true)),
            OnBehindFront(4, Damage()),
            ResistibleKnockback.Create(KnockbackId.HallowedWings, 1.36f),
        ],
        Timing = new() { DamageDelay = 1.8f },
    };
}
