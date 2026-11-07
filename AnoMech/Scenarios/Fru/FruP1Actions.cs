using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.SimObjects;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Scenarios.Fru;

// Cast times and shapes come from the Action sheet. UNVERIFIED throughout: no replay has calibrated
// these animation locks, damage delays or status durations unless a constant says otherwise, and every
// severity that punishes a wrong count or a clipped area is a practice stand-in for retail's numbers.
public static class FruP1Actions
{
    // UNVERIFIED angles: the sheet has none. Cyclonic Break's is a marked guess.
    private static readonly float CyclonicHalfAngle = 11.5f * MathF.PI / 180f;
    private const float BowShockHalfAngle = MathF.PI / 3f;
    private const float SinblazeHalfAngle = MathF.PI / 4f;
    private const float BlastburnPushDelay = CastSpec.ActionEffectOffset;

    public const float PowderMarkSeconds = 16.1f;
    public const float PowderVulnerabilitySeconds = 14f;
    public const float CyclonicVulnerabilitySeconds = 1.96f;
    public const float StackVulnerabilitySeconds = 1.96f;
    public const float FaithVulnerabilitySeconds = 4f;
    public const float BoundMarkSeconds = 15f;
    public const float FettersSeconds = 4f;
    public const float BleedSeconds = 15f;
    public const float MortalitySeconds = 30f;
    private const byte FettersParam = 0xC8;
    private const float InnerTurnLock = 2.44f;
    private const float UtopianSkyLock = 3.1f;
    private const float HeavensStrikeLock = 4.1f;
    private const float BrightfireLock = 2.1f;
    private const float BoundFettersLock = 2.1f;

    private const ushort HallowedGround = 82;
    private const ushort Holmgang = 409;
    private const ushort LivingDead = 810;
    private const ushort Superbolide = 1836;

    private static readonly VulnSpec VulnToMagic = VulnerableTo(StatusId.MagicVulnerabilityUp);
    private static readonly VulnSpec VulnToPhysicalUnlessInvulnerable = VulnerableTo(StatusId.PhysicalVulnerabilityUp)
        .ProtectedBy(HallowedGround).ProtectedBy(Holmgang).ProtectedBy(LivingDead).ProtectedBy(Superbolide);

    public static readonly EnemyAction PowderMarkTrail = new(ActionId.PowderMarkTrail)
    {
        Effects =
        [
            Damage(TankBuster),
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, PowderVulnerabilitySeconds),
            ApplyStatus(StatusId.PowderMarkTrail, PowderMarkSeconds),
        ],
    };

    public static readonly EnemyAction BurnMark = new(ActionId.BurnMark)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(TankBuster)],
    };

    // UNVERIFIED cadence and amounts: autos are a stand-in for the boss's swings at its target.
    public static readonly EnemyAction AutoAttack = new(ActionId.FatebreakerAutoAttack)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage()],
    };

    public static readonly EnemyAction AutoAttackOnMarkedTank = new(ActionId.FatebreakerAutoAttack)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(VulnToPhysicalUnlessInvulnerable)],
        DeathExplanation = "the boss still held its Powder Mark target: no tank swap",
    };

    // UNVERIFIED that this is the row retail applies: only the 15s bleed itself was observed.
    public static readonly EnemyAction BurnishedGlory = new(ActionId.BurnishedGlory)
    {
        Effects = [Damage(), ApplyStatus(StatusId.Bleeding, BleedSeconds)],
    };

    public static readonly EnemyAction CyclonicBreakFirst = new(ActionId.CyclonicBreakFirst)
    {
        Area = new() { Size = CyclonicHalfAngle },
        Effects = [Damage(split: Exactly(1)), ApplyStatus(StatusId.PhysicalVulnerabilityUp, CyclonicVulnerabilitySeconds)],
    };

    public static readonly EnemyAction CyclonicBreakRest = new(ActionId.CyclonicBreakRest)
    {
        Area = new() { Size = CyclonicHalfAngle },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction CyclonicBreakSinsmoke = new(ActionId.CyclonicBreakSinsmoke)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(VulnToMagic, split: Exactly(2)), ApplyStatus(StatusId.MagicVulnerabilityUp, StackVulnerabilitySeconds)],
    };

    public static readonly EnemyAction CyclonicBreakSinsmite = new(ActionId.CyclonicBreakSinsmite)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(split: Exactly(1))],
    };

    public static readonly EnemyAction UtopianSkyStack = new(ActionId.UtopianSkyStack)
    {
        Cast = new() { AnimationLock = UtopianSkyLock, IncludeCasterInEffectTargets = true },
    };

    public static readonly EnemyAction UtopianSkySpread = new(ActionId.UtopianSkySpread)
    {
        Cast = new() { AnimationLock = UtopianSkyLock, IncludeCasterInEffectTargets = true },
    };

    public static readonly EnemyAction BlastingZone = new(ActionId.BlastingZone)
    {
        Cast = new() { AnimationLock = InnerTurnLock },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction SinboundFire = new(ActionId.SinboundFire)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(VulnToMagic, split: Exactly(4)), ApplyStatus(StatusId.MagicVulnerabilityUp, StackVulnerabilitySeconds)],
    };

    public static readonly EnemyAction SinboundThunder = new(ActionId.SinboundThunder)
    {
        Cast = new() { FaceTarget = false },
        Effects = [Damage(split: Exactly(1))],
    };

    public static readonly EnemyAction TurnOfHeavensFire = new(ActionId.TurnOfHeavensFire)
    {
        Cast = new() { AnimationLock = InnerTurnLock, IncludeCasterInEffectTargets = true },
    };

    public static readonly EnemyAction TurnOfHeavensLightning = new(ActionId.TurnOfHeavensLightning)
    {
        Cast = new() { AnimationLock = InnerTurnLock, IncludeCasterInEffectTargets = true },
    };

    public static readonly EnemyAction BoundOfFaith = new(ActionId.BoundOfFaith)
    {
        Cast = new() { IncludeCasterInEffectTargets = true },
    };

    public static readonly EnemyAction TurnOfHeavensBurntStrikeLightning = Line(ActionId.TurnOfHeavensBurntStrikeLightning) with { Cast = new() { AnimationLock = HeavensStrikeLock } };
    public static readonly EnemyAction TurnOfHeavensBurnout = Line(ActionId.TurnOfHeavensBurnout);
    public static readonly EnemyAction TurnOfHeavensBurntStrikeFire = Line(ActionId.TurnOfHeavensBurntStrikeFire) with { Cast = new() { AnimationLock = HeavensStrikeLock } };
    public static readonly EnemyAction TurnOfHeavensBlastburnEast = BlastburnEast(ActionId.TurnOfHeavensBlastburn);
    public static readonly EnemyAction TurnOfHeavensBlastburnWest = BlastburnWest(ActionId.TurnOfHeavensBlastburn);

    public static readonly EnemyAction ExplosionBurntStrikeFire = Line(ActionId.ExplosionBurntStrikeFire);
    public static readonly EnemyAction ExplosionBurntStrikeLightning = Line(ActionId.ExplosionBurntStrikeLightning);
    public static readonly EnemyAction ExplosionBurnout = Line(ActionId.ExplosionBurnout);
    public static readonly EnemyAction ExplosionBlastburnEast = BlastburnEast(ActionId.ExplosionBlastburn);
    public static readonly EnemyAction ExplosionBlastburnWest = BlastburnWest(ActionId.ExplosionBlastburn);

    public static readonly EnemyAction BrightfireSmall = new(ActionId.BrightfireSmall)
    {
        Cast = new() { AnimationLock = BrightfireLock },
        Effects = [Damage(Lethal)],
    };

    public static readonly EnemyAction BrightfireLarge = new(ActionId.BrightfireLarge)
    {
        Cast = new() { AnimationLock = BrightfireLock },
        Effects = [Damage(Lethal)],
    };

    // Its sheet row locks nothing, so the root is this cast's own. UNVERIFIED in game for a human.
    public static readonly EnemyAction FloatingFetters = new(ActionId.FloatingFetters)
    {
        Effects = [ApplyRootingStatus(StatusId.FloatingFetters, FettersSeconds, entryParam: FettersParam)],
    };

    public static readonly EnemyAction BoundFloatingFetters = FloatingFetters with { Cast = new() { AnimationLock = BoundFettersLock } };

    // Short of four, it lands anyway and marks those it reached, as retail's Mark of Mortality does.
    public static readonly EnemyAction BoundOfFaithSinsmoke = new(ActionId.BoundOfFaithSinsmoke)
    {
        Cast = new() { FaceTarget = false },
        Effects =
        [
            Damage(VulnToMagic, split: Stack(4, understacked: Normal)),
            new MortalityWhenShort(4),
            ApplyStatus(StatusId.MagicVulnerabilityUp, StackVulnerabilitySeconds),
        ],
    };

    public static readonly EnemyAction FatedBurnMark = new(ActionId.FatedBurnMark)
    {
        Effects = [Damage()],
        DeathExplanation = "a Fated Burn Mark holder died",
    };

    public static readonly EnemyAction FallOfFaithSinsmite = new(ActionId.FallOfFaithSinsmite)
    {
        Effects = [Damage(VulnToMagic), ApplyStatus(StatusId.MagicVulnerabilityUp, FaithVulnerabilitySeconds)],
    };

    // Each cone is for its one bait; the tether holder stands at its apex, clear of it.
    public static readonly EnemyAction FallOfFaithBowShock = new(ActionId.FallOfFaithBowShock)
    {
        Area = new() { Size = BowShockHalfAngle, ExcludeCaster = true },
        Effects = [Damage(VulnToMagic, split: Exactly(1)), ApplyStatus(StatusId.MagicVulnerabilityUp, FaithVulnerabilitySeconds)],
    };

    // The tether holder at its apex counts toward the stack.
    public static readonly EnemyAction FallOfFaithSinblaze = new(ActionId.FallOfFaithSinblaze)
    {
        Area = new() { Size = SinblazeHalfAngle },
        Effects = [Damage(VulnToMagic, split: Exactly(4)), ApplyStatus(StatusId.MagicVulnerabilityUp, FaithVulnerabilitySeconds)],
    };

    private static readonly EnemyAction ExplosionFireOne = Tower(ActionId.ExplosionFireOne);
    private static readonly EnemyAction ExplosionFireTwo = Tower(ActionId.ExplosionFireTwo);
    private static readonly EnemyAction ExplosionFireThree = Tower(ActionId.ExplosionFireThree);
    private static readonly EnemyAction ExplosionFireFour = Tower(ActionId.ExplosionFireFour);
    private static readonly EnemyAction ExplosionLightningOne = Tower(ActionId.ExplosionLightningOne);
    private static readonly EnemyAction ExplosionLightningTwo = Tower(ActionId.ExplosionLightningTwo);
    private static readonly EnemyAction ExplosionLightningThree = Tower(ActionId.ExplosionLightningThree);
    private static readonly EnemyAction ExplosionLightningFour = Tower(ActionId.ExplosionLightningFour);

    public static readonly EnemyAction UnmitigatedExplosionFire = TowerFailure(ActionId.UnmitigatedExplosionFire);
    public static readonly EnemyAction UnmitigatedExplosionLightning = TowerFailure(ActionId.UnmitigatedExplosionLightning);

    public static readonly EnemyAction Enrage = new(ActionId.FatebreakerEnrage)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "enrage",
    };

    public static EnemyAction Explosion(bool fire, int soakers) => (fire, soakers) switch
    {
        (true, 1) => ExplosionFireOne,
        (true, 2) => ExplosionFireTwo,
        (true, 3) => ExplosionFireThree,
        (true, 4) => ExplosionFireFour,
        (false, 1) => ExplosionLightningOne,
        (false, 2) => ExplosionLightningTwo,
        (false, 3) => ExplosionLightningThree,
        (false, 4) => ExplosionLightningFour,
        _ => throw new ArgumentOutOfRangeException(nameof(soakers)),
    };

    public static int? ExplosionSoakers(uint actionId) => actionId switch
    {
        ActionId.ExplosionFireOne or ActionId.ExplosionLightningOne => 1,
        ActionId.ExplosionFireTwo or ActionId.ExplosionLightningTwo => 2,
        ActionId.ExplosionFireThree or ActionId.ExplosionLightningThree => 3,
        ActionId.ExplosionFireFour or ActionId.ExplosionLightningFour => 4,
        _ => null,
    };

    public static EnemyAction UnmitigatedExplosion(uint explosionId) => explosionId
        is ActionId.ExplosionFireOne or ActionId.ExplosionFireTwo or ActionId.ExplosionFireThree or ActionId.ExplosionFireFour
        ? UnmitigatedExplosionFire
        : UnmitigatedExplosionLightning;

    private static EnemyAction Line(uint actionId) => new(actionId)
    {
        Effects = [Damage(Lethal)],
    };

    private static EnemyAction Tower(uint actionId) => new(actionId)
    {
        Effects = [Damage()],
    };

    private static EnemyAction TowerFailure(uint actionId) => new(actionId)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a tower went under-soaked",
    };

    // A Direction 3 knockback row pushes along the caster's facing, and retail pushes each player
    // away from the line on their own side. So one cast per side: the helper faces the push and only
    // its area keeps pointing down the line, from a helper standing at the north edge.
    private static EnemyAction BlastburnEast(uint actionId) => new(actionId)
    {
        Area = new()
        {
            Rotation = -MathF.PI / 2f,
            AdjustTargets = (_, hits) => hits.Where(hit => hit.Position.X > 0f).ToList(),
        },
        Effects = [Knockback(KnockbackId.Blastburn, BlastburnPushDelay)],
    };

    private static EnemyAction BlastburnWest(uint actionId) => new(actionId)
    {
        Area = new()
        {
            Rotation = MathF.PI / 2f,
            AdjustTargets = (_, hits) => hits.Where(hit => hit.Position.X <= 0f).ToList(),
        },
        Effects = [Knockback(KnockbackId.Blastburn, BlastburnPushDelay)],
    };

    private sealed class MortalityWhenShort(int needed) : IEnemyActionEffect
    {
        public void Apply(EnemyActionContext ctx, IReadOnlyList<SimCharacter> hits)
        {
            if (hits.Count >= needed) return;
            foreach (var hit in hits)
                if (!ctx.IsKilled(hit) && hit.IsAlive())
                    ctx.AddStatus(hit, StatusId.MarkOfMortality, MortalitySeconds);
        }
    }
}
