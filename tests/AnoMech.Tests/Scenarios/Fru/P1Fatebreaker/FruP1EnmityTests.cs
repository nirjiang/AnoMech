using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruP1EnmityTests
{
    private static DutyHarness Start(PartyRole seat, bool godMode = false)
    {
        var duty = new DutyHarness(typeof(FruP1FatebreakerScenario), seat, debugBot: false) { SelectedAi = null };
        duty.Game.GodMode = godMode;
        duty.Start();
        duty.RunFor(1.2f);
        return duty;
    }

    private static FruP1Enmity Targeting(DutyHarness duty) => ((FruP1FatebreakerScenario)duty.Scenario).State!.Targeting!;

    [Test]
    public void TheSheetAgreesWithProvokesThirtySecondRecast()
    {
        using var duty = Start(PartyRole.OffTank);
        Assert.That(Natives.Data.ActionName(FruP1Enmity.ProvokeActionId), Is.EqualTo("Provoke"));
        Assert.That(FruP1Enmity.ProvokeRecastSeconds, Is.EqualTo(30f));
    }

    [Test]
    public void ATanksRealProvokeTakesTheBossWithNoBotActionShown()
    {
        using var duty = Start(PartyRole.OffTank);
        duty.Press(FruP1Enmity.ProvokeActionId, duty.Combat.Boss!);
        Assert.Multiple(() =>
        {
            Assert.That(Targeting(duty).Top, Is.SameAs(duty.Player));
            Assert.That(duty.AbilityCount(FruP1Enmity.ProvokeActionId), Is.Zero, "the client plays its own press");
        });
    }

    [Test]
    public void ADpsSeatsPressOfProvokeIsRefused()
    {
        using var duty = Start(PartyRole.MeleeDpsA);
        duty.Press(FruP1Enmity.ProvokeActionId, duty.Combat.Boss!);
        Assert.That(Targeting(duty).Top, Is.SameAs(duty.Member(PartyRole.MainTank)));
    }

    [Test]
    public void AProvokeAimedAtAnythingButTheBossIsRefused()
    {
        using var duty = Start(PartyRole.OffTank);
        duty.Press(FruP1Enmity.ProvokeActionId, duty.Member(PartyRole.MeleeDpsA));
        Assert.That(Targeting(duty).Top, Is.SameAs(duty.Member(PartyRole.MainTank)));
    }

    [Test]
    public void AnUnresolvedPressMovesNothing()
    {
        using var duty = Start(PartyRole.OffTank);
        duty.Press(FruP1Enmity.ProvokeActionId, duty.Combat.Boss!, PlayerActionStep.CastStarted);
        Assert.That(Targeting(duty).Top, Is.SameAs(duty.Member(PartyRole.MainTank)));
    }

    [Test]
    public void ProvokeRefusesOutOfRangeAndADeadCaster()
    {
        using var duty = Start(PartyRole.CasterDps);
        var targeting = Targeting(duty);
        var coTank = duty.Member(PartyRole.OffTank);

        coTank.SetPosition(new Vector3(0f, 0f, 60f));
        Assert.That(targeting.TryProvoke(coTank), Is.False, "out of range");
        coTank.SetPosition(new Vector3(0f, 0f, 5f));
        coTank.Die(SimCharacterDeathExtensions.Environment, "test");
        Assert.That(targeting.TryProvoke(coTank), Is.False, "dead");
    }

    [Test]
    public void ProvokeIsOnItsOwnThirtySecondRecastPerCaster()
    {
        using var duty = Start(PartyRole.CasterDps, godMode: true);
        var targeting = Targeting(duty);
        var (mainTank, offTank) = (duty.Member(PartyRole.MainTank), duty.Member(PartyRole.OffTank));
        duty.PlaceAll(0f, 5f);

        Assert.That(targeting.TryProvoke(offTank), Is.True);
        Assert.That(targeting.TryProvoke(mainTank), Is.True, "its own recast");
        Assert.That(targeting.TryProvoke(offTank), Is.False, "still recasting");
        duty.RunFor(29f);
        Assert.That(targeting.TryProvoke(offTank), Is.False, "still recasting");
    }

    [Test]
    public void ABotsProvokeShowsOnceAsANativeAction()
    {
        using var duty = Start(PartyRole.CasterDps);
        Assert.That(Targeting(duty).TryProvoke(duty.Member(PartyRole.OffTank)), Is.True);
        Assert.That(duty.AbilityCount(FruP1Enmity.ProvokeActionId), Is.EqualTo(1));
    }

    [Test]
    public void ProvokeRefusesAnUntargetableBoss()
    {
        using var duty = Start(PartyRole.CasterDps, godMode: true);
        duty.RunTo(36f);
        var offTank = duty.Member(PartyRole.OffTank);
        Assert.That(Targeting(duty).TryProvoke(offTank), Is.False);
    }

    [Test]
    public void ProvokeRefusesOnceTheFightIsOver()
    {
        using var duty = Start(PartyRole.CasterDps);
        var targeting = Targeting(duty);
        duty.Combat.DealBossDamage(duty.Combat.BossHp);
        Assert.That(targeting.TryProvoke(duty.Member(PartyRole.OffTank)), Is.False);
    }

    [Test]
    public void TheBossFallsBackOnlyWhenItsTargetDies()
    {
        using var duty = Start(PartyRole.CasterDps);
        var targeting = Targeting(duty);
        var (mainTank, offTank) = (duty.Member(PartyRole.MainTank), duty.Member(PartyRole.OffTank));
        Assert.That(targeting.CurrentTarget(), Is.SameAs(mainTank));

        mainTank.AddStatus(StatusId.PhysicalVulnerabilityUp, 5f);
        mainTank.AddStatus(StatusId.PowderMarkTrail, 5f);
        Assert.That(targeting.CurrentTarget(), Is.SameAs(mainTank), "a debuff is no reason to leave");

        mainTank.Die(SimCharacterDeathExtensions.Environment, "test");
        Assert.That(targeting.CurrentTarget(), Is.SameAs(offTank));
    }

    [Test]
    public void TheNextRunGetsAFreshHelperWithNoRecastCarried()
    {
        using var duty = Start(PartyRole.CasterDps);
        var first = Targeting(duty);
        Assert.That(first.TryProvoke(duty.Member(PartyRole.OffTank)), Is.True);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Frame();
        duty.RunFor(1.2f);
        var second = Targeting(duty);
        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.TryProvoke(duty.Member(PartyRole.OffTank)), Is.True);
        });
    }
}
