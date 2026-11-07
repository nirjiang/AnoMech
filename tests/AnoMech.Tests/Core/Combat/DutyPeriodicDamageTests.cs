using AnoMech.Core.Combat;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

// Driven by direct Tick calls with no frames between them, so nothing else moves HP.
public class DutyPeriodicDamageTests
{
    private const ushort DotStatus = 3078;
    private const ushort DarkMind = 746;
    private const float Interval = 3f;
    private const float StatusSeconds = 60f;
    private const uint Amount = 10_000;
    private const uint Overkill = 10_000_000;

    private static readonly DutyDot Dot = new(DotStatus, Amount, DamageKind.Typeless);

    private static DutyPeriodicDamage Periodic(DutyHarness duty, params DutyDot[] dots)
        => new(duty.Combat, duty.Game.World.Party, dots);

    private static SimCharacter Holder(DutyHarness duty)
    {
        var member = duty.Member(PartyRole.MeleeDpsA);
        member.AddStatus(DotStatus, StatusSeconds);
        return member;
    }

    [Test]
    public void DamageAccumulatesOnTheIntervalAndOnlyOnHolders()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var other = duty.Member(PartyRole.MeleeDpsB);
        var maxHp = combat.MaxHp(holder)!.Value;
        var periodic = Periodic(duty, Dot);

        periodic.Tick(Interval - 0.1f);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));

        periodic.Tick(0.2f);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - Amount));

        periodic.Tick(Interval);
        Assert.Multiple(() =>
        {
            Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - 2 * Amount));
            Assert.That(combat.Hp(other), Is.EqualTo(combat.MaxHp(other)));
        });
    }

    [Test]
    public void ALongFrameLandsEveryPulseItCoveredAndKeepsTheFraction()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;
        var periodic = Periodic(duty, Dot);

        periodic.Tick(9.5f);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - 3 * Amount));

        periodic.Tick(2f);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - 3 * Amount), "0.5s carried over, 2.5s of 3s");

        periodic.Tick(1f);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - 4 * Amount));
    }

    [Test]
    public void CaughtUpPulsesDrainAShieldBeforeHp()
    {
        using var duty = new DutyHarness(PartyRole.ShieldHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;
        duty.Press(DutyActions.Adloquium, holder);
        var shield = combat.Shield(holder);
        var periodic = Periodic(duty, Dot);
        var pulsesToSpendIt = (int)(shield / Amount);

        periodic.Tick(Interval * pulsesToSpendIt);
        Assert.Multiple(() =>
        {
            Assert.That(combat.Shield(holder), Is.EqualTo(shield % Amount));
            Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));
        });

        periodic.Tick(Interval * 2f);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - (2 * Amount - shield % Amount)));
    }

    [Test]
    public void ALongFrameKillsOnceAndLandsNothingAfterTheDeath()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var holder = Holder(duty);
        var maxHp = duty.Combat.MaxHp(holder)!.Value;
        var deaths = new List<(PartyRole Role, uint? ActionId, ushort? StatusId)>();
        duty.Game.PartyMemberKilled += (role, _, actionId, statusId) => deaths.Add((role, actionId, statusId));

        Periodic(duty, new DutyDot(DotStatus, maxHp / 2, DamageKind.Typeless)).Tick(Interval * 5f);

        Assert.Multiple(() =>
        {
            Assert.That(holder.Dead, Is.True);
            Assert.That(deaths, Is.EqualTo(new[] { (PartyRole.MeleeDpsA, (uint?)null, (ushort?)DotStatus) }));
        });
    }

    [Test]
    public void ALongFrameKillingThePlayerReportsOneDeathAcrossTheRestart()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        player.AddStatus(DotStatus, StatusSeconds);

        Periodic(duty, new DutyDot(DotStatus, Overkill, DamageKind.Typeless)).Tick(Interval * 5f);

        Assert.That(duty.Killed, Is.EqualTo(new[] { duty.Seat }));
    }

    [Test]
    public void GodmodeHoldsOneHpThroughALongFrame()
    {
        using var duty = new DutyHarness();
        duty.Start();
        duty.Game.GodMode = true;
        var combat = duty.Combat;
        var holder = Holder(duty);

        Periodic(duty, new DutyDot(DotStatus, Overkill, DamageKind.Typeless)).Tick(Interval * 5f);

        Assert.Multiple(() =>
        {
            Assert.That(holder.Dead, Is.False);
            Assert.That(combat.Hp(holder), Is.EqualTo(1u));
        });
    }

    [Test]
    public void ALongFrameStopsOnceTheBossIsDefeated()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;
        combat.DealBossDamage(combat.BossHp);

        Periodic(duty, Dot).Tick(Interval * 5f);

        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    [TestCase(-1f)]
    [TestCase(0f)]
    public void AnUnusableDeltaIsIgnoredAndLeavesTheClockIntact(float delta)
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;
        var periodic = Periodic(duty, Dot);

        periodic.Tick(delta);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));

        periodic.Tick(Interval);
        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - Amount));
    }

    [Test]
    public void AnAbsurdlyLongFrameStillReturns()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;

        Periodic(duty, new DutyDot(DotStatus, 1, DamageKind.Typeless)).Tick(float.MaxValue);

        Assert.Multiple(() =>
        {
            Assert.That(holder.Dead, Is.False);
            Assert.That(combat.Hp(holder), Is.LessThan(maxHp));
        });
    }

    [Test]
    public void AShieldAbsorbsAPulse()
    {
        using var duty = new DutyHarness(PartyRole.ShieldHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;
        duty.Press(DutyActions.Adloquium, holder);
        var shield = combat.Shield(holder);
        Assert.That(shield, Is.GreaterThan(Amount));

        Periodic(duty, Dot).Tick(Interval);

        Assert.Multiple(() =>
        {
            Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));
            Assert.That(combat.Shield(holder), Is.EqualTo(shield - Amount));
        });
    }

    [TestCase(DamageKind.Magic, 8_000u)]
    [TestCase(DamageKind.Physical, 9_000u)]
    [TestCase(DamageKind.Typeless, 10_000u)]
    public void MitigationOfTheMatchingKindReducesAPulse(DamageKind kind, uint expected)
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        holder.AddStatus(DarkMind, StatusSeconds);
        var maxHp = combat.MaxHp(holder)!.Value;

        Periodic(duty, new DutyDot(DotStatus, Amount, kind)).Tick(Interval);

        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp - expected));
    }

    [Test]
    public void LosingTheStatusStopsThePulses()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;

        holder.RemoveStatus(DotStatus);
        Periodic(duty, Dot).Tick(Interval);

        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));
    }

    [Test]
    public void ALethalPulseKillsWithTheStatusAsTheCauseAndThenStops()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var holder = Holder(duty);
        var deaths = new List<(PartyRole Role, uint? ActionId, ushort? StatusId)>();
        duty.Game.PartyMemberKilled += (role, _, actionId, statusId) => deaths.Add((role, actionId, statusId));
        var periodic = Periodic(duty, new DutyDot(DotStatus, Overkill, DamageKind.Typeless));

        periodic.Tick(Interval);
        periodic.Tick(Interval);

        Assert.Multiple(() =>
        {
            Assert.That(holder.Dead, Is.True);
            Assert.That(deaths, Is.EqualTo(new[] { (PartyRole.MeleeDpsA, (uint?)null, (ushort?)DotStatus) }));
        });
    }

    [Test]
    public void GodmodeLeavesOneHpThroughEveryLethalPulse()
    {
        using var duty = new DutyHarness();
        duty.Start();
        duty.Game.GodMode = true;
        var combat = duty.Combat;
        var holder = Holder(duty);
        var periodic = Periodic(duty, new DutyDot(DotStatus, Overkill, DamageKind.Typeless));

        periodic.Tick(Interval);
        periodic.Tick(Interval);

        Assert.Multiple(() =>
        {
            Assert.That(holder.Dead, Is.False);
            Assert.That(combat.Hp(holder), Is.EqualTo(1u));
            Assert.That(holder.Proxy!.Health, Is.GreaterThan(0u));
        });
    }

    [Test]
    public void APulseSendsNoActionEffectEvenWhenItKills()
    {
        using var duty = new DutyHarness();
        duty.Start();
        Holder(duty);
        var before = duty.Fake.Timeline.Rows.Count(row => row["kind"] == "ability");

        Periodic(duty, Dot).Tick(Interval * 3f);
        Periodic(duty, new DutyDot(DotStatus, Overkill, DamageKind.Typeless)).Tick(Interval * 3f);

        Assert.That(duty.Fake.Timeline.Rows.Count(row => row["kind"] == "ability"), Is.EqualTo(before));
    }

    [Test]
    public void NothingLandsAfterTheRunCloses()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;

        combat.Dispose();
        Periodic(duty, new DutyDot(DotStatus, Overkill, DamageKind.Typeless)).Tick(Interval);

        Assert.Multiple(() =>
        {
            Assert.That(holder.Dead, Is.False);
            Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));
        });
    }

    [Test]
    public void NothingLandsOnceTheBossIsDefeated()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var holder = Holder(duty);
        var maxHp = combat.MaxHp(holder)!.Value;

        combat.DealBossDamage(combat.BossHp);
        Assert.That(combat.BossDefeated, Is.True);
        Periodic(duty, Dot).Tick(Interval);

        Assert.That(combat.Hp(holder), Is.EqualTo(maxHp));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void AnIntervalMustBePositiveAndFinite(float interval)
    {
        using var duty = new DutyHarness();
        duty.Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DutyPeriodicDamage(duty.Combat, duty.Game.World.Party, [Dot], interval));
    }

    [Test]
    public void TheStatusAloneChangesNoVitalsWithoutAHelper()
    {
        var plain = PreviewVitals(holdDot: false);
        var holding = PreviewVitals(holdDot: true);

        Assert.That(holding, Is.EqualTo(plain));
    }

    private static Dictionary<PartyRole, (uint? Hp, uint? Mp)> PreviewVitals(bool holdDot)
    {
        using var duty = new DutyHarness();
        duty.Start();
        if (holdDot) Holder(duty);
        duty.RunFor(Interval * 3f);
        return duty.Game.World.Party.AllMembers()
            .ToDictionary(member => ((ISimPartyMember)member).Role, member => (duty.Combat.Hp(member), duty.Combat.Mp(member)));
    }
}
