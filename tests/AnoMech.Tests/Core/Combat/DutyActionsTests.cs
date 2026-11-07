using AnoMech.Core.Combat;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;

namespace AnoMech.Tests;

public class DutyActionsTests
{
    [TestCase(DutyActions.HeavySwing, "Heavy Swing")]
    [TestCase(DutyActions.FastBlade, "Fast Blade")]
    [TestCase(DutyActions.Rampart, "Rampart")]
    [TestCase(DutyActions.GlareIII, "Glare III")]
    [TestCase(DutyActions.CureII, "Cure II")]
    [TestCase(DutyActions.Medica, "Medica")]
    [TestCase(DutyActions.BroilIV, "Broil IV")]
    [TestCase(DutyActions.Adloquium, "Adloquium")]
    [TestCase(DutyActions.Succor, "Succor")]
    [TestCase(DutyActions.TrueThrust, "True Thrust")]
    [TestCase(DutyActions.LeapingOpo, "Leaping Opo")]
    [TestCase(DutyActions.Bootshine, "Bootshine")]
    [TestCase(DutyActions.BurstShot, "Burst Shot")]
    [TestCase(DutyActions.Fire, "Fire")]
    public void EveryPrototypeActionIsTheNamedPlayerActionOnTheSheet(uint actionId, string name)
    {
        FakeGame.Install();
        var row = Natives.Data.Action(actionId);
        Assert.That(row, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(row!.Name, Is.EqualTo(name));
            Assert.That(row.IsPlayerAction, Is.True);
        });
    }

    [Test]
    public void ShieldActionsApplyGalvanizeOnTheSheet()
    {
        FakeGame.Install();
        foreach (var action in DutyActions.All.Where(a => a.ShieldAmount > 0))
            Assert.That(Natives.Data.StatusName(action.ShieldStatusId), Is.EqualTo("Galvanize"));
    }

    [Test]
    public void EveryJobOfTheDefaultPartyHasADamageAction()
    {
        foreach (var preset in PartyPresets.Standard)
            Assert.That(DutyActions.Of((JobId)preset.ClassJob).Any(a => a.Kind == DutyActionKind.Damage), Is.True, preset.Name);
    }

    [Test]
    public void HealersHaveHealingAndTanksMitigation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyActions.Of(JobId.WhiteMage).Any(a => a.Kind == DutyActionKind.SingleHeal), Is.True);
            Assert.That(DutyActions.Of(JobId.WhiteMage).Any(a => a.Kind == DutyActionKind.PartyHeal), Is.True);
            Assert.That(DutyActions.Of(JobId.Scholar).Any(a => a.Kind == DutyActionKind.SingleShield), Is.True);
            Assert.That(DutyActions.Of(JobId.Scholar).Any(a => a.Kind == DutyActionKind.PartyShield), Is.True);
            Assert.That(DutyActions.Of(JobId.Warrior).Any(a => a.Kind == DutyActionKind.SelfMitigation), Is.True);
            Assert.That(DutyActions.Of(JobId.Paladin).Any(a => a.Kind == DutyActionKind.SelfMitigation), Is.True);
        });
    }

    [Test]
    public void AJobOutsideThePrototypeIsNotGivenAnotherJobsKit()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyActions.Supports(JobId.Samurai), Is.False);
            Assert.That(DutyActions.Find(JobId.Samurai, DutyActions.TrueThrust), Is.Null);
            Assert.That(DutyActions.Find(JobId.Monk, DutyActions.TrueThrust), Is.Null);
        });
    }
}
