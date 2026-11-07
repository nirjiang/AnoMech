using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

// Every test resolves before the timeline's first hit, at 14.3s.
public class FruP1ActionsTests
{
    private const float ToImpact = 1.2f;
    private const float Halo = 2f;
    private const uint NormalHit = 12_000;
    private const uint TankBusterHit = 60_000;
    private const uint MemberMaxHp = 200_000;
    private const uint TankMaxHp = 300_000;

    private static readonly Placement SouthFromCentre = new(Vector3.Zero, 0f);
    private static readonly Placement SouthFromEdge = FruP1FatebreakerState.EdgeImage(FruP1FatebreakerState.NorthSpot);

    [TestCase(ActionId.PowderMarkTrail, 5f, CastType.SingleTarget, 0f, 0f)]
    [TestCase(ActionId.BurnMark, 0f, CastType.Circle, 10f, 0f)]
    [TestCase(ActionId.BurnishedGlory, 5f, CastType.Circle, 40f, 0f)]
    [TestCase(ActionId.FatebreakerEnrage, 10f, CastType.Circle, 40f, 0f)]
    [TestCase(ActionId.CyclonicBreakFirst, 0f, CastType.Cone, 60f, 0f)]
    [TestCase(ActionId.CyclonicBreakRest, 0f, CastType.Cone, 50f, 0f)]
    [TestCase(ActionId.CyclonicBreakSinsmoke, 0f, CastType.Circle, 6f, 0f)]
    [TestCase(ActionId.CyclonicBreakSinsmite, 0f, CastType.Circle, 6f, 0f)]
    [TestCase(ActionId.BlastingZone, 0f, CastType.Rectangle, 50f, 16f)]
    [TestCase(ActionId.BlastingZoneCast, 10f, CastType.SingleTarget, 0f, 0f)]
    [TestCase(ActionId.SinboundFire, 0f, CastType.Circle, 6f, 0f)]
    [TestCase(ActionId.SinboundThunder, 0f, CastType.Circle, 5f, 0f)]
    [TestCase(ActionId.TurnOfHeavensBurntStrikeLightning, 8f, CastType.Rectangle, 80f, 10f)]
    [TestCase(ActionId.TurnOfHeavensBurnout, 9.7f, CastType.Rectangle, 80f, 20f)]
    [TestCase(ActionId.TurnOfHeavensBurntStrikeFire, 8f, CastType.Rectangle, 80f, 10f)]
    [TestCase(ActionId.TurnOfHeavensBlastburn, 10f, CastType.Rectangle, 80f, 50f)]
    [TestCase(ActionId.BrightfireSmall, 8f, CastType.Circle, 5f, 0f)]
    [TestCase(ActionId.BrightfireLarge, 8f, CastType.Circle, 10f, 0f)]
    [TestCase(ActionId.BoundOfFaith, 10f, CastType.SingleTarget, 0f, 0f)]
    [TestCase(ActionId.BoundOfFaithSinsmoke, 0f, CastType.Circle, 6f, 0f)]
    [TestCase(ActionId.FatedBurnMark, 0f, CastType.Circle, 100f, 0f)]
    [TestCase(ActionId.FallOfFaithBowShock, 0f, CastType.Cone, 60f, 0f)]
    [TestCase(ActionId.FallOfFaithSinblaze, 0f, CastType.Cone, 60f, 0f)]
    [TestCase(ActionId.ExplosionBurntStrikeFire, 6.5f, CastType.Rectangle, 80f, 10f)]
    [TestCase(ActionId.ExplosionBurntStrikeLightning, 6.5f, CastType.Rectangle, 80f, 10f)]
    [TestCase(ActionId.ExplosionBlastburn, 8.5f, CastType.Rectangle, 80f, 50f)]
    [TestCase(ActionId.ExplosionBurnout, 8.2f, CastType.Rectangle, 80f, 20f)]
    [TestCase(ActionId.ExplosionFireOne, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionFireTwo, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionFireThree, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionFireFour, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionLightningOne, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionLightningTwo, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionLightningThree, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.ExplosionLightningFour, 10.5f, CastType.Circle, 4f, 0f)]
    [TestCase(ActionId.UnmitigatedExplosionFire, 0f, CastType.Circle, 40f, 0f)]
    [TestCase(ActionId.UnmitigatedExplosionLightning, 0f, CastType.Circle, 40f, 0f)]
    public void TheSheetAgreesWithWhatTheFightAssumes(uint actionId, float castSeconds, CastType shape, float range, float width)
    {
        using var duty = FruP1Run.Start();
        var row = Natives.Data.Action(actionId);
        Assert.Multiple(() =>
        {
            Assert.That(row, Is.Not.Null);
            Assert.That(row!.CastSeconds, Is.EqualTo(castSeconds).Within(0.01f));
            Assert.That(row.CastType, Is.EqualTo(shape));
            Assert.That(row.EffectRange, Is.EqualTo(range).Within(0.01f));
            Assert.That(row.XAxisModifier, Is.EqualTo(width).Within(0.01f));
        });
    }

    [Test]
    public void TheFightsActorsAreTheSheetsFatebreakerAndHalos()
    {
        using var duty = FruP1Run.Start();
        Assert.Multiple(() =>
        {
            Assert.That(Natives.Data.BNpcName(BNpcNameId.Fatebreaker), Does.Contain("Fatebreaker"));
            Assert.That(Natives.Data.BNpcName(BNpcNameId.FatebreakersImage), Does.Contain("image"));
            Assert.That(Natives.Data.BNpcName(BNpcNameId.HaloOfFlame), Does.Contain("flame"));
            Assert.That(Natives.Data.BNpcName(BNpcNameId.HaloOfLevin), Does.Contain("levin"));
        });
    }

    [Test]
    public void ACyclonicBaitConeHitsItsBaitAloneInsideItsAngle()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (bait, clear) = (PartyRole.MeleeDpsA, PartyRole.MeleeDpsB);
        duty.Place(bait, 0f, 10f);
        duty.Place(clear, 10f * MathF.Sin(0.3f), 10f * MathF.Cos(0.3f));

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.CyclonicBreakFirst, duty.Member(bait), at: SouthFromCentre);
        duty.RunFor(ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Combat.Hp(duty.Member(bait)), Is.EqualTo(MemberMaxHp - NormalHit));
            Assert.That(duty.Combat.Hp(duty.Member(clear)), Is.EqualTo(MemberMaxHp));
        });
    }

    [Test]
    public void TwoPlayersInOneCyclonicBaitConeBothDie()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (bait, clip) = (PartyRole.MeleeDpsA, PartyRole.MeleeDpsB);
        duty.Place(bait, 0f, 10f);
        duty.Place(clip, 10f * MathF.Sin(0.1f), 10f * MathF.Cos(0.1f));

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.CyclonicBreakFirst, duty.Member(bait), at: SouthFromCentre);
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(bait).Dead && duty.Member(clip).Dead, Is.True);
    }

    [TestCase(0.15f, true)]
    [TestCase(0.25f, false)]
    public void ARestConeKillsInsideItsNarrowAngle(float offAxis, bool dies)
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        duty.Place(PartyRole.MeleeDpsA, 10f * MathF.Sin(offAxis), 10f * MathF.Cos(offAxis));

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.CyclonicBreakRest, at: SouthFromCentre);
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(PartyRole.MeleeDpsA).Dead, Is.EqualTo(dies));
    }

    private static IEnumerable<TestCaseData> Stacks()
    {
        yield return new TestCaseData(FruP1Actions.CyclonicBreakSinsmoke, 2).SetName("Cyclonic pairs");
        yield return new TestCaseData(FruP1Actions.SinboundFire, 4).SetName("Sinbound Fire");
    }

    [TestCaseSource(nameof(Stacks))]
    public void AnExactStackLivesAndAShortOrCrowdedOneDies(EnemyAction stack, int needed)
    {
        foreach (var soakers in new[] { needed, needed - 1, needed + 1 })
        {
            using var duty = FruP1Run.Start();
            duty.PlaceAll(0f, -18f);
            var inStack = new[] { PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.RegenHealer, PartyRole.ShieldHealer }.Take(soakers).ToList();
            foreach (var role in inStack) duty.Place(role, 0f, 10f);

            duty.Combat.Boss!.CastFromHelper(stack, duty.Member(inStack[0]));
            duty.RunFor(ToImpact);

            Assert.That(inStack.All(role => duty.Member(role).Dead), Is.EqualTo(soakers != needed), $"{soakers} in the stack");
        }
    }

    [Test]
    public void AShortBoundStackLandsAndMarksItsSoakersWithMortality()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (target, other) = (PartyRole.MeleeDpsA, PartyRole.MeleeDpsB);
        duty.Place(target, 0f, 10f);
        duty.Place(other, 0f, 10f);

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.BoundOfFaithSinsmoke, duty.Member(target));
        duty.RunFor(ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(target).Dead || duty.Member(other).Dead, Is.False);
            Assert.That(duty.Member(target).HasStatus(StatusId.MarkOfMortality), Is.True);
            Assert.That(duty.Member(PartyRole.PhysRangedDps).HasStatus(StatusId.MarkOfMortality), Is.False);
        });
    }

    [Test]
    public void AFullBoundStackLeavesNoMortality()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var inStack = new[] { PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.RegenHealer };
        foreach (var role in inStack) duty.Place(role, 0f, 10f);

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.BoundOfFaithSinsmoke, duty.Member(inStack[0]));
        duty.RunFor(ToImpact);

        Assert.That(inStack.Any(role => duty.Member(role).HasStatus(StatusId.MarkOfMortality)), Is.False);
    }

    [Test]
    public void AllEightOverlappingBothBoundStacksIsNoFreePass()
    {
        using var duty = FruP1Run.Start(godMode: false);
        duty.PlaceAll(0f, 10f);
        duty.Place(PartyRole.CasterDps, 0f, -18f);
        var boss = duty.Combat.Boss!;

        boss.CastFromHelper(FruP1Actions.BoundOfFaithSinsmoke, duty.Member(PartyRole.MeleeDpsA));
        boss.CastFromHelper(FruP1Actions.BoundOfFaithSinsmoke, duty.Member(PartyRole.MeleeDpsB));
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(PartyRole.MeleeDpsA).Dead && duty.Member(PartyRole.RegenHealer).Dead, Is.True);
    }

    [Test]
    public void ConsecutiveFallOfFaithHitsWithinTheVulnerabilityKill()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (holder, bait) = (PartyRole.RegenHealer, PartyRole.MeleeDpsA);
        duty.Place(holder, 0f, 0f);
        duty.Place(bait, 0f, 5f);
        var boss = duty.Combat.Boss!;

        boss.CastFromHelper(FruP1Actions.FallOfFaithBowShock, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(bait).Dead, Is.False);
            Assert.That(duty.Member(bait).HasStatus(StatusId.MagicVulnerabilityUp), Is.True);
        });

        boss.CastFromHelper(FruP1Actions.FallOfFaithBowShock, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);
        Assert.That(duty.Member(bait).Dead, Is.True);
    }

    [Test]
    public void FallOfFaithHitsMoreThanFourSecondsApartBothLand()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (holder, bait) = (PartyRole.RegenHealer, PartyRole.MeleeDpsA);
        duty.Place(holder, 0f, 0f);
        duty.Place(bait, 0f, 5f);
        var boss = duty.Combat.Boss!;

        boss.CastFromHelper(FruP1Actions.FallOfFaithBowShock, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(FruP1Actions.FaithVulnerabilitySeconds + 0.3f);
        boss.CastFromHelper(FruP1Actions.FallOfFaithBowShock, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(bait).Dead, Is.False);
    }

    [Test]
    public void CyclonicBaitsLeaveAShortPhysicalVulnerabilityAndPowderALongOne()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        duty.Place(PartyRole.MeleeDpsA, 0f, 10f);
        duty.Place(PartyRole.MainTank, 10f, 0f);
        var boss = duty.Combat.Boss!;

        boss.CastFromHelper(FruP1Actions.CyclonicBreakFirst, duty.Member(PartyRole.MeleeDpsA), at: SouthFromCentre);
        duty.Frame();
        Assert.That(duty.Member(PartyRole.MeleeDpsA).HasStatus(StatusId.PhysicalVulnerabilityUp), Is.True);
        duty.RunFor(FruP1Actions.CyclonicVulnerabilitySeconds + 0.2f);
        Assert.That(duty.Member(PartyRole.MeleeDpsA).HasStatus(StatusId.PhysicalVulnerabilityUp), Is.False);

        boss.CastFromHelper(FruP1Actions.PowderMarkTrail, duty.Member(PartyRole.MainTank));
        duty.RunFor(5.2f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(PartyRole.MainTank).HasStatus(StatusId.PhysicalVulnerabilityUp), Is.True);
            Assert.That(duty.Member(PartyRole.MainTank).HasStatus(StatusId.PowderMarkTrail), Is.True);
        });
    }

    private static IEnumerable<TestCaseData> Spreads()
    {
        yield return new TestCaseData(FruP1Actions.CyclonicBreakSinsmite, 6f).SetName("Cyclonic spread");
        yield return new TestCaseData(FruP1Actions.SinboundThunder, 5f).SetName("Sinbound Thunder");
    }

    [TestCaseSource(nameof(Spreads))]
    public void ASpreadSharedWithAnyoneKillsAndAClearOneDoesNot(EnemyAction spread, float radius)
    {
        foreach (var gap in new[] { radius - 0.5f, radius + 0.5f })
        {
            using var duty = FruP1Run.Start();
            duty.PlaceAll(0f, -18f);
            var (target, other) = (PartyRole.MeleeDpsA, PartyRole.MeleeDpsB);
            duty.Place(target, 0f, 5f);
            duty.Place(other, gap, 5f);

            duty.Combat.Boss!.CastFromHelper(spread, duty.Member(target));
            duty.RunFor(ToImpact);

            Assert.That(duty.Member(other).Dead, Is.EqualTo(gap < radius), $"{gap}y away");
            Assert.That(duty.Member(target).Dead, Is.EqualTo(gap < radius), $"{gap}y away");
        }
    }

    private static IEnumerable<TestCaseData> Lines()
    {
        yield return new TestCaseData(FruP1Actions.BlastingZone, 8f).SetName("Blasting Zone");
        yield return new TestCaseData(FruP1Actions.TurnOfHeavensBurntStrikeLightning, 5f).SetName("Heavens lightning strike");
        yield return new TestCaseData(FruP1Actions.TurnOfHeavensBurntStrikeFire, 5f).SetName("Heavens fire strike");
        yield return new TestCaseData(FruP1Actions.TurnOfHeavensBurnout, 10f).SetName("Heavens Burnout");
        yield return new TestCaseData(FruP1Actions.ExplosionBurntStrikeFire, 5f).SetName("Explosion fire strike");
        yield return new TestCaseData(FruP1Actions.ExplosionBurntStrikeLightning, 5f).SetName("Explosion lightning strike");
        yield return new TestCaseData(FruP1Actions.ExplosionBurnout, 10f).SetName("Explosion Burnout");
    }

    [TestCaseSource(nameof(Lines))]
    public void ALineFromTheEdgeKillsAcrossTheWholeArenaWithinItsWidth(EnemyAction line, float halfWidth)
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(18f, 0f);
        var (inside, farEnd, outside) = (PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps);
        duty.Place(inside, halfWidth - 0.2f, 0f);
        duty.Place(farEnd, 0f, 18f);
        duty.Place(outside, halfWidth + 0.2f, 0f);

        duty.Combat.Boss!.CastFromHelper(line, at: SouthFromEdge);
        duty.RunFor((Natives.Data.Action(line.ActionId)?.CastSeconds ?? 0f) + ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(inside).Dead, Is.True);
            Assert.That(duty.Member(farEnd).Dead, Is.True);
            Assert.That(duty.Member(outside).Dead, Is.False);
        });
    }

    [Test]
    public void AKillLandsAtItsDamageDelayNotAtTheSnapshot()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(18f, 0f);
        duty.Place(PartyRole.MeleeDpsA, 0f, 0f);

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.BlastingZone, at: SouthFromEdge);
        duty.RunFor(0.5f);
        Assert.That(duty.Member(PartyRole.MeleeDpsA).Dead, Is.False);
        duty.RunFor(0.7f);
        Assert.That(duty.Member(PartyRole.MeleeDpsA).Dead, Is.True);
    }

    private static IEnumerable<TestCaseData> Circles()
    {
        yield return new TestCaseData(FruP1Actions.BrightfireSmall, 5f).SetName("Small halo");
        yield return new TestCaseData(FruP1Actions.BrightfireLarge, 10f).SetName("Large halo");
    }

    [TestCaseSource(nameof(Circles))]
    public void AHaloKillsWithinItsOwnRadius(EnemyAction brightfire, float radius)
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        duty.Place(PartyRole.MeleeDpsA, radius - 0.2f, 0f);
        duty.Place(PartyRole.MeleeDpsB, radius + 0.2f, 0f);

        duty.Combat.Boss!.CastFromHelper(brightfire, at: SouthFromCentre);
        duty.RunFor(8f + ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(PartyRole.MeleeDpsA).Dead, Is.True);
            Assert.That(duty.Member(PartyRole.MeleeDpsB).Dead, Is.False);
        });
    }

    [Test]
    public void BlastburnPushesEachSideAwayFromTheLine()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(1f, 0f);
        var (east, west, onLine) = (PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps);
        duty.Place(east, 3f, -5f);
        duty.Place(west, -3f, 5f);
        duty.Place(onLine, 0f, 0f);

        var edge = SouthFromEdge.Position;
        duty.Combat.Boss!.CastFromHelper(FruP1Actions.ExplosionBlastburnEast, at: new Placement(edge, MathF.PI / 2f));
        duty.Combat.Boss!.CastFromHelper(FruP1Actions.ExplosionBlastburnWest, at: new Placement(edge, -MathF.PI / 2f));
        duty.RunFor(8.5f + 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(east).Position.X, Is.EqualTo(18f).Within(0.5f));
            Assert.That(duty.Member(east).Position.Z, Is.EqualTo(-5f).Within(0.5f));
            Assert.That(duty.Member(west).Position.X, Is.EqualTo(-18f).Within(0.5f));
            Assert.That(duty.Member(west).Position.Z, Is.EqualTo(5f).Within(0.5f));
            Assert.That(duty.Member(onLine).Position.X, Is.EqualTo(-15f).Within(0.5f));
        });
    }

    [Test]
    public void APowderMarkTakesItsTargetAndTheNearestAllyWhoeverThatIs()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        var (holder, coTank) = (PartyRole.MainTank, PartyRole.OffTank);
        duty.Place(holder, 0f, -15f);
        duty.Place(coTank, 0f, -4f);

        duty.Member(holder).AddStatus(StatusId.PowderMarkTrail, Halo);
        duty.RunFor(Halo + ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityCount(ActionId.BurnMark), Is.EqualTo(2));
            Assert.That(duty.Combat.Hp(duty.Member(holder)), Is.EqualTo(TankMaxHp - TankBusterHit));
            Assert.That(duty.Combat.Hp(duty.Member(coTank)), Is.EqualTo(TankMaxHp - TankBusterHit));
            Assert.That(duty.Combat.Hp(duty.Member(PartyRole.MeleeDpsA)), Is.EqualTo(MemberMaxHp));
        });
    }

    [Test]
    public void APowderMarkTakesANonTankWhoStandsNearestInsteadOfTheCoTank()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        var (holder, coTank, nearest) = (PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA);
        duty.Place(holder, 0f, -15f);
        duty.Place(nearest, 0f, -12f);
        duty.Place(coTank, 0f, -1f);

        duty.Member(holder).AddStatus(StatusId.PowderMarkTrail, Halo);
        duty.RunFor(Halo + ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(nearest).Dead, Is.True);
            Assert.That(duty.Combat.Hp(duty.Member(coTank)), Is.EqualTo(TankMaxHp));
        });
    }

    [Test]
    public void AHolderKilledBeforeThePowderMarkRunsOutSetsItOffAtOnce()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        duty.Place(PartyRole.MainTank, 0f, -15f);
        duty.Place(PartyRole.OffTank, 0f, -4f);
        duty.Member(PartyRole.MainTank).AddStatus(StatusId.PowderMarkTrail, Halo * 2f);
        duty.Member(PartyRole.MainTank).Die(SimCharacterDeathExtensions.Environment, "test");
        duty.RunFor(ToImpact);
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityCount(ActionId.BurnMark), Is.EqualTo(2));
            Assert.That(duty.Combat.Hp(duty.Member(PartyRole.OffTank)), Is.EqualTo(TankMaxHp - TankBusterHit));
        });
    }

    [Test]
    public void BurnishedGlorysBleedTicksOnTheLedgerUntilItRunsOut()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        var member = duty.Member(PartyRole.MeleeDpsA);
        duty.Combat.Boss!.CastFromHelper(FruP1Actions.BurnishedGlory, at: SouthFromCentre);
        duty.RunFor(5f + ToImpact);
        Assert.That(member.HasStatus(StatusId.Bleeding), Is.True);
        var afterHit = duty.Combat.Hp(member)!.Value;
        duty.RunFor(3.2f);
        var afterTick = duty.Combat.Hp(member)!.Value;
        Assert.That(afterTick, Is.LessThan(afterHit));
    }

    [Test]
    public void TheFatedBurnMarkGoesOffOnlyOnItsHoldersDeath()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        duty.Member(PartyRole.MeleeDpsA).AddStatus(StatusId.FatedBurnMark, Halo);
        duty.RunFor(Halo + ToImpact);
        Assert.That(duty.AbilityCount(ActionId.FatedBurnMark), Is.Zero, "ran out");

        duty.Member(PartyRole.MeleeDpsB).AddStatus(StatusId.FatedBurnMark, Halo);
        duty.Member(PartyRole.MeleeDpsB).Die(SimCharacterDeathExtensions.Environment, "test");
        duty.RunFor(ToImpact);
        Assert.Multiple(() =>
        {
            Assert.That(duty.AbilityCount(ActionId.FatedBurnMark), Is.EqualTo(1));
            Assert.That(duty.Combat.Hp(duty.Member(PartyRole.PhysRangedDps)), Is.LessThan(MemberMaxHp), "the bot healers may already be at work");
        });
    }

    [TestCase(true, 1)]
    [TestCase(true, 2)]
    [TestCase(true, 3)]
    [TestCase(true, 4)]
    [TestCase(false, 1)]
    [TestCase(false, 2)]
    [TestCase(false, 3)]
    [TestCase(false, 4)]
    public void ATowerTakesExactlyItsNumberAndPunishesInItsOwnElement(bool fire, int soakers)
    {
        var (punishment, other) = fire
            ? (ActionId.UnmitigatedExplosionFire, ActionId.UnmitigatedExplosionLightning)
            : (ActionId.UnmitigatedExplosionLightning, ActionId.UnmitigatedExplosionFire);
        foreach (var inTower in new[] { soakers, soakers - 1 })
        {
            using var duty = FruP1Run.Start(godMode: true);
            duty.PlaceAll(15f, 0f);
            foreach (var role in new[] { PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.RegenHealer }.Take(inTower))
                duty.Place(role, -11f, 0f);

            duty.Combat.Boss!.CastFromHelper(FruP1Actions.Explosion(fire, soakers), at: new Placement(new Vector3(-11f, 0f, 0f), 0f));
            duty.RunFor(10.5f + ToImpact);

            Assert.Multiple(() =>
            {
                Assert.That(duty.AbilityCount(punishment), Is.EqualTo(inTower < soakers ? 1 : 0), $"{inTower} soaking");
                Assert.That(duty.AbilityCount(other), Is.Zero);
            });
        }
    }

    [Test]
    public void FloatingFettersRootForTheirDuration()
    {
        using var duty = FruP1Run.Start();
        var target = duty.Member(PartyRole.MeleeDpsA);
        // An instant action releases as it is cast, so the root runs from there for its full duration.
        duty.Combat.Boss!.Cast(FruP1Actions.FloatingFetters, target);
        Assert.That(target.MovementLocked, Is.True);
        duty.RunFor(FruP1Actions.FettersSeconds - 0.1f);
        Assert.That(target.MovementLocked, Is.True);
        duty.RunFor(0.2f);
        Assert.That(target.MovementLocked, Is.False);
    }

    [Test]
    public void TheFettersStopABotOrderWhileTheyHold()
    {
        using var duty = FruP1Run.Start();
        var target = duty.Member(PartyRole.MeleeDpsA);
        target.SetPosition(new Vector3(5f, 0f, 5f));
        duty.Combat.Boss!.Cast(FruP1Actions.FloatingFetters, target);
        target.MoveTo(new Vector3(5f, 0f, 15f));
        duty.RunFor(1f);
        Assert.That(target.Position.Z, Is.EqualTo(5f).Within(0.01f));
    }

    [Test]
    public void AnAutoAttackOnAMarkedTankKillsUnprotectedAndIsSparedByAnInvuln()
    {
        const ushort holmgang = 409;
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, 15f);
        var (exposed, covered) = (duty.Member(PartyRole.MainTank), duty.Member(PartyRole.OffTank));
        exposed.AddStatus(StatusId.PhysicalVulnerabilityUp, Halo);
        covered.AddStatus(StatusId.PhysicalVulnerabilityUp, Halo);
        covered.AddStatus(holmgang, Halo);
        var boss = duty.Combat.Boss!;

        boss.CastFromHelper(FruP1Actions.AutoAttackOnMarkedTank, exposed);
        boss.CastFromHelper(FruP1Actions.AutoAttackOnMarkedTank, covered);
        duty.RunFor(ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(exposed.Dead, Is.True);
            Assert.That(covered.Dead, Is.False);
        });
    }

    [Test]
    public void APhysicalVulnerabilityDoesNotMakeAMagicHitLethal()
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (holder, bait) = (PartyRole.RegenHealer, PartyRole.MeleeDpsA);
        duty.Place(holder, 0f, 0f);
        duty.Place(bait, 0f, 5f);
        duty.Member(bait).AddStatus(StatusId.PhysicalVulnerabilityUp, Halo);

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.FallOfFaithBowShock, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(bait).Dead, Is.False);
    }

    [TestCase(0.9f, true)]
    [TestCase(1.15f, false)]
    public void ABowShockConeReachesSixtyDegreesEitherWayAndSparesItsApex(float offAxis, bool clips)
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (holder, bait, other) = (PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB);
        duty.Place(holder, 0f, 0f);
        duty.Place(bait, 0f, 5f);
        duty.Place(other, 5f * MathF.Sin(offAxis), 5f * MathF.Cos(offAxis));

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.FallOfFaithBowShock, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Member(holder).Dead, Is.False);
            Assert.That(duty.Combat.Hp(duty.Member(holder)), Is.EqualTo(MemberMaxHp));
            Assert.That(duty.Member(bait).Dead, Is.EqualTo(clips));
            Assert.That(duty.Member(other).Dead, Is.EqualTo(clips));
        });
    }

    [TestCase(4, false)]
    [TestCase(3, true)]
    [TestCase(5, true)]
    public void ASinblazeStackCountsItsTetherHolderAtTheApex(int total, bool dies)
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var holder = PartyRole.RegenHealer;
        duty.Place(holder, 0f, 0f);
        var others = new[] { PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.ShieldHealer }.Take(total - 1).ToList();
        foreach (var role in others) duty.Place(role, 0f, 5f);

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.FallOfFaithSinblaze, duty.Member(others[0]), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(holder).Dead, Is.EqualTo(dies));
    }

    [TestCase(0.7f, true)]
    [TestCase(0.85f, false)]
    public void ASinblazeConeReachesFortyFiveDegreesEitherWay(float offAxis, bool reaches)
    {
        using var duty = FruP1Run.Start();
        duty.PlaceAll(0f, -18f);
        var (holder, bait, other, third) = (PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps);
        duty.Place(holder, 0f, 0f);
        duty.Place(bait, 0f, 5f);
        duty.Place(third, 0f, 6f);
        duty.Place(other, 5f * MathF.Sin(offAxis), 5f * MathF.Cos(offAxis));

        duty.Combat.Boss!.CastFromHelper(FruP1Actions.FallOfFaithSinblaze, duty.Member(bait), at: duty.Member(holder).Placement());
        duty.RunFor(ToImpact);

        Assert.That(duty.Member(bait).Dead, Is.EqualTo(!reaches), "the fourth in the stack is the one at the edge");
    }
}
