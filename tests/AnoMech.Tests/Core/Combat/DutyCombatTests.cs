using System.Numerics;
using AnoMech.Core.Combat;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class DutyCombatTests
{
    private const uint MemberMaxHp = 200_000;
    private const uint Weaponskill = 1_000;

    private static DutyAction ActionOf(JobId job, uint id) => DutyActions.Find(job, id)!;

    [Test]
    public void HitsAccumulateOnTheLedgerAndTheNativeBar()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var member = duty.Member(PartyRole.MeleeDpsA);

        duty.Combat.Commit(member, 10_000);
        duty.Combat.Commit(member, 10_000);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Combat.Hp(member), Is.EqualTo(MemberMaxHp - 20_000));
            Assert.That((float)member.Proxy!.Health / member.Proxy.MaxHealth, Is.EqualTo(0.9f).Within(0.001f));
        });
    }

    [Test]
    public void DamageIsNotTakenAtTheSnapshotAndAHealBeforeImpactCounts()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var member = duty.Member(PartyRole.MeleeDpsA);
        member.SetPosition(new Vector3(12f, 0f, 0f));
        duty.Frame();
        Assert.That(member.IsAlive(), Is.True, "the spot has to be inside the arena");
        var combat = duty.Combat;
        combat.Commit(member, 40_000);
        var before = combat.Hp(member)!.Value;

        combat.Boss!.CastFromHelper(FruActions.PandoraAutoAttack, member);
        Assert.That(combat.Hp(member), Is.EqualTo(before), "taken at the snapshot");

        duty.Press(DutyActions.CureII, member);
        Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp));

        duty.RunFor(1f);
        Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp - 8_000u));
    }

    [Test]
    public void ShieldsAbsorbNumericallyAndGoWhenSpent()
    {
        using var duty = new DutyHarness(PartyRole.ShieldHealer, debugBot: false);
        duty.Start();
        var member = duty.Member(PartyRole.MeleeDpsA);
        member.SetPosition(new Vector3(1f, 0f, 1f));
        duty.Frame();
        var combat = duty.Combat;
        duty.Press(DutyActions.Adloquium, member);
        const ushort galvanize = 297;
        Assert.Multiple(() =>
        {
            Assert.That(combat.Shield(member), Is.EqualTo(40_000u));
            Assert.That(member.HasStatus(galvanize), Is.True);
        });

        combat.Commit(member, 10_000);
        Assert.Multiple(() =>
        {
            Assert.That(combat.Shield(member), Is.EqualTo(30_000u));
            Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp));
            Assert.That(member.HasStatus(galvanize), Is.True);
        });

        combat.Commit(member, 50_000);
        Assert.Multiple(() =>
        {
            Assert.That(combat.Shield(member), Is.EqualTo(0u));
            Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp - 20_000));
            Assert.That(member.HasStatus(galvanize), Is.False);
        });
    }

    [Test]
    public void RecastingTheSameShieldReplacesRatherThanStacks()
    {
        using var duty = new DutyHarness(PartyRole.ShieldHealer, debugBot: false);
        duty.Start();
        var member = duty.Member(PartyRole.MeleeDpsA);
        var combat = duty.Combat;
        duty.Press(DutyActions.Adloquium, member);
        duty.RunUntil(() => combat.Cooldown(duty.Player, ActionOf(JobId.Scholar, DutyActions.Adloquium)) <= 0f, 5f, "the GCD");
        duty.Press(DutyActions.Adloquium, member);
        Assert.That(combat.Shield(member), Is.EqualTo(40_000u));
    }

    [Test]
    public void OverhealStopsAtMaxAndAHealCannotRaise()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var healthy = duty.Member(PartyRole.MeleeDpsA);
        duty.Press(DutyActions.CureII, healthy);
        Assert.That(combat.Hp(healthy), Is.EqualTo(MemberMaxHp));

        var dead = duty.Member(PartyRole.MeleeDpsB);
        dead.Die(SimCharacterDeathExtensions.Environment, "test");
        var refusal = combat.Validate(duty.Player, ActionOf(JobId.WhiteMage, DutyActions.CureII), (ulong)dead.GameObjectId);
        Assert.Multiple(() =>
        {
            Assert.That(refusal, Is.EqualTo(DutyActionRefusal.TargetDead));
            Assert.That(combat.Hp(dead), Is.EqualTo(0u));
            Assert.That(dead.Dead, Is.True);
        });
    }

    [Test]
    public void DeadAliveAndUntrackedReadDifferently()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp));
        member.Die(SimCharacterDeathExtensions.Environment, "test");
        Assert.Multiple(() =>
        {
            Assert.That(combat.Hp(member), Is.EqualTo(0u));
            Assert.That(combat.Hp(combat.Boss!), Is.Null);
        });
    }

    [Test]
    public void PercentageMitigationShrinksTheSnapshotAndBotsGetNoExemption()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var tank = duty.Member(PartyRole.OffTank);
        var plain = combat.SnapshotHit(tank, FruActions.PandoraAutoAttack, Severity.Normal, DamageKind.Physical);
        tank.AddStatus(1191, 20f);
        var mitigated = combat.SnapshotHit(tank, FruActions.PandoraAutoAttack, Severity.Normal, DamageKind.Physical);
        Assert.Multiple(() =>
        {
            Assert.That(plain, Is.EqualTo(8_000u));
            Assert.That(mitigated, Is.EqualTo(6_400u));
        });
    }

    [Test]
    public void MechanicsStillKillAtFullHp()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        member.SetPosition(new Vector3(12f, 0f, 0f));
        duty.Frame();
        Assert.That(member.IsAlive(), Is.True, "the spot has to be inside the arena");
        Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp));
        var lethal = new EnemyAction(ActionId.PandoraAutoAttack) { Effects = [Damage(Severity.Lethal)], Timing = new() { DamageDelay = 0.5f } };
        var castAt = duty.Game.World.Events.Elapsed;

        combat.Boss!.CastFromHelper(lethal, member);
        Assert.That(member.Dead, Is.False, "the verdict lands at impact, not the snapshot");
        duty.RunFor(1f);

        var death = duty.Deaths.Single(d => d.Role == PartyRole.MeleeDpsA);
        Assert.Multiple(() =>
        {
            Assert.That(member.Dead, Is.True);
            Assert.That(death.ActionId, Is.EqualTo(ActionId.PandoraAutoAttack));
            Assert.That(death.Time - castAt, Is.EqualTo(0.5f).Within(0.05f));
        });
    }

    [Test]
    public void ACorpseKeepsZeroThroughShieldExpiryAndManaTicks()
    {
        using var duty = new DutyHarness(PartyRole.ShieldHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        duty.Press(DutyActions.Adloquium, member);
        combat.SetMp(member, 1_000);
        Assert.That(combat.Shield(member), Is.EqualTo(40_000u));

        member.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.RunFor(4f);

        var cure = duty.Combat.Validate(duty.Player, ActionOf(JobId.Scholar, DutyActions.Adloquium), (ulong)member.GameObjectId);
        Assert.Multiple(() =>
        {
            Assert.That(member.Dead, Is.True);
            Assert.That(combat.Hp(member), Is.EqualTo(0u));
            Assert.That(member.Proxy!.Health, Is.EqualTo(0u));
            Assert.That(member.Proxy.Mana, Is.EqualTo(0u));
            Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(0));
            Assert.That(cure, Is.EqualTo(DutyActionRefusal.TargetDead));
        });
    }

    [Test]
    public void TheShieldOverlayFollowsTheLedgerThroughLaterStatusChanges()
    {
        using var duty = new DutyHarness(PartyRole.ShieldHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        const ushort galvanize = 297;
        const ushort divineVeil = 1362;

        duty.Press(DutyActions.Adloquium, member);
        Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(20), "40k of 200k");

        member.AddStatus(1191, 10f);
        duty.Frame();
        Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(20), "a later status flush");

        member.AddStatus(divineVeil, 10f);
        duty.Frame();
        Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(20), "the practice table's shield isn't on the ledger");

        combat.Commit(member, 10_000);
        duty.Frame();
        Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(15), "after absorbing");

        member.RemoveStatus(galvanize);
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(combat.Shield(member), Is.EqualTo(0u));
            Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(0), "after it expired");
        });

        duty.Game.Reset();
        duty.Frame();
        Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(0), "at the run's end");

        member.AddStatus(divineVeil, 10f);
        duty.Frame();
        Assert.That(DutyHarness.ShownShield(member), Is.EqualTo(10), "the practice table's own value again");
    }

    [Test]
    public void AMonkPressOfLeapingOpoOrBootshineLands()
    {
        using var duty = new DutyHarness(PartyRole.MeleeDpsB, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var opo = ActionOf(JobId.Monk, DutyActions.LeapingOpo);
        duty.Player.SetPosition(new Vector3(2f, 0f, 0f));
        duty.Frame();

        var before = combat.BossHp;
        duty.Press(DutyActions.LeapingOpo, boss);
        Assert.That(combat.BossHp, Is.EqualTo(before - Weaponskill), "Leaping Opo");

        before = combat.BossHp;
        duty.Press(DutyActions.LeapingOpo, boss);
        Assert.That(combat.BossHp, Is.EqualTo(before), "on the GCD");

        duty.RunUntil(() => combat.Cooldown(duty.Player, opo) <= 0f, 5f, "the GCD");
        before = combat.BossHp;
        duty.Press(DutyActions.Bootshine, boss);
        Assert.That(combat.BossHp, Is.EqualTo(before - Weaponskill), "Bootshine");
    }

    [Test]
    public void AnotherJobsPressOfLeapingOpoDoesNothing()
    {
        using var duty = new DutyHarness(PartyRole.MeleeDpsA, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        duty.Player.SetPosition(new Vector3(2f, 0f, 0f));
        duty.Frame();

        var before = combat.BossHp;
        duty.Press(DutyActions.LeapingOpo, boss);

        Assert.Multiple(() =>
        {
            Assert.That(combat.BossHp, Is.EqualTo(before));
            Assert.That(combat.Validate(duty.Player, ActionOf(JobId.Monk, DutyActions.LeapingOpo), (ulong)boss.GameObjectId), Is.EqualTo(DutyActionRefusal.WrongJob));
        });
    }

    [Test]
    public void AMonkBotAttacksWithLeapingOpo()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var monk = duty.Member(PartyRole.MeleeDpsB);
        monk.SetPosition(new Vector3(2f, 0f, 0f));
        duty.RunUntil(() => duty.AbilityCount(DutyActions.LeapingOpo) > 0, 4f, "the bot's Leaping Opo");

        var row = duty.AbilityRows(DutyActions.LeapingOpo).First();
        Assert.Multiple(() =>
        {
            Assert.That(row["actor"], Is.EqualTo(DutyHarness.Id(monk)));
            Assert.That(row["target"], Is.EqualTo(DutyHarness.Id(combat.Boss!)));
            Assert.That(duty.AbilityCount(DutyActions.Bootshine), Is.EqualTo(0));
        });
    }

    [Test]
    public void ABotAttackAimsAtTheBoss()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var mage = duty.Member(PartyRole.CasterDps);
        duty.RunUntil(() => duty.AbilityCount(DutyActions.Fire) > 0, 6f, "the bot's Fire");

        var row = duty.AbilityRows(DutyActions.Fire).First();
        Assert.Multiple(() =>
        {
            Assert.That(row["actor"], Is.EqualTo(DutyHarness.Id(mage)));
            Assert.That(row["target"], Is.EqualTo(DutyHarness.Id(combat.Boss!)));
            Assert.That(row["targets"], Is.EqualTo(DutyHarness.Id(combat.Boss!)));
        });
    }

    [Test]
    public void ABotHealAimsAtItsFriendAndNotItself()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var hurt = duty.Member(PartyRole.MeleeDpsA);
        combat.Commit(hurt, 60_000);
        IEnumerable<Dictionary<string, string>> Heals() => duty.AbilityRows(DutyActions.CureII).Concat(duty.AbilityRows(DutyActions.Adloquium));
        duty.RunUntil(() => Heals().Any(), 8f, "a heal");

        var row = Heals().First();
        Assert.Multiple(() =>
        {
            Assert.That(row["target"], Is.EqualTo(DutyHarness.Id(hurt)));
            Assert.That(row["actor"], Is.Not.EqualTo(DutyHarness.Id(hurt)));
            Assert.That(row["targets"], Is.EqualTo(DutyHarness.Id(hurt)));
        });
    }

    [Test]
    public void APartyHealAnimatesOnItsCasterAndListsWhoItReached()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        SimCharacter[] hurt = [duty.Member(PartyRole.MeleeDpsA), duty.Member(PartyRole.MeleeDpsB), duty.Member(PartyRole.PhysRangedDps)];
        foreach (var member in hurt) combat.Commit(member, 50_000);
        IEnumerable<Dictionary<string, string>> Heals() => duty.AbilityRows(DutyActions.Medica).Concat(duty.AbilityRows(DutyActions.Succor));
        duty.RunUntil(() => Heals().Any(), 8f, "a party heal");

        var row = Heals().First();
        Assert.Multiple(() =>
        {
            Assert.That(row["target"], Is.EqualTo(row["actor"]));
            foreach (var member in hurt)
                Assert.That(row["targets"].Split(' '), Does.Contain(DutyHarness.Id(member)));
        });
    }

    [Test]
    public void ABotMitigationAimsAtItselfAndAppliesOnce()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        var tank = duty.Member(PartyRole.OffTank);
        duty.Combat.Commit(tank, 60_000);
        duty.RunUntil(() => duty.AbilityCount(DutyActions.Rampart) > 0, 2f, "the bot's Rampart");

        var row = duty.AbilityRows(DutyActions.Rampart).Single();
        Assert.Multiple(() =>
        {
            Assert.That(row["target"], Is.EqualTo(DutyHarness.Id(tank)));
            Assert.That(tank.ActiveStatusSnapshot.Count(s => s.StatusId == 1191), Is.EqualTo(1));
        });
    }

    [Test]
    public void AManualMitigationAppliesOnceWithTheModuleOff()
    {
        using var duty = new DutyHarness(PartyRole.OffTank, debugBot: false);
        duty.Start();
        var rampart = ActionOf(JobId.Paladin, DutyActions.Rampart);
        Assert.That(duty.Fake.UserActions.Enabled, Is.False);

        duty.Press(DutyActions.Rampart, duty.Player);
        duty.Press(DutyActions.Rampart, duty.Player);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Player.ActiveStatusSnapshot.Count(s => s.StatusId == 1191), Is.EqualTo(1));
            Assert.That(duty.Combat.Cooldown(duty.Player, rampart), Is.EqualTo(rampart.Recast));
            Assert.That(duty.Fake.UserActions.Enabled, Is.False, "never turned on for the player");
        });
    }

    [Test]
    public void AManualMitigationIsLeftToJobActionHandlerWithTheModuleOn()
    {
        using var duty = new DutyHarness(PartyRole.OffTank, debugBot: false);
        duty.Start();
        duty.Fake.UserActions.Enabled = true;
        var rampart = ActionOf(JobId.Paladin, DutyActions.Rampart);

        duty.Press(DutyActions.Rampart, duty.Player);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Player.HasStatus(1191), Is.False, "the handler's to apply, and the fake has none");
            Assert.That(duty.Combat.Cooldown(duty.Player, rampart), Is.EqualTo(rampart.Recast));
        });
    }

    [Test]
    public void TheCommonValidatorRefusesAnotherJobsAction()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var healer = duty.Member(PartyRole.RegenHealer);
        var mana = combat.Mp(healer);

        Assert.Multiple(() =>
        {
            Assert.That(combat.Validate(duty.Player, ActionOf(JobId.BlackMage, DutyActions.Fire), (ulong)boss.GameObjectId), Is.EqualTo(DutyActionRefusal.WrongJob));
            Assert.That(combat.BotUse(healer, ActionOf(JobId.BlackMage, DutyActions.Fire), boss), Is.EqualTo(DutyActionRefusal.WrongJob));
            Assert.That(combat.Mp(healer), Is.EqualTo(mana));
        });
    }

    [Test]
    public void AnUntargetableBossTakesNoPress()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var before = combat.BossHp;
        boss.SetTargetable(false);

        duty.Press(DutyActions.BurstShot, boss);

        Assert.Multiple(() =>
        {
            Assert.That(combat.Validate(duty.Player, ActionOf(JobId.Bard, DutyActions.BurstShot), (ulong)boss.GameObjectId), Is.EqualTo(DutyActionRefusal.Untargetable));
            Assert.That(combat.BossHp, Is.EqualTo(before));
        });
    }

    [Test]
    public void ADespawnedBossTakesNoPress()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var id = (ulong)boss.GameObjectId;
        var before = combat.BossHp;
        boss.Despawn();

        Assert.Multiple(() =>
        {
            Assert.That(combat.Validate(duty.Player, ActionOf(JobId.Bard, DutyActions.BurstShot), id), Is.EqualTo(DutyActionRefusal.NoTarget));
            Assert.That(combat.BossHp, Is.EqualTo(before));
        });
    }

    [Test]
    public void AHealNeedsAFriend()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var cure = ActionOf(JobId.WhiteMage, DutyActions.CureII);
        Assert.Multiple(() =>
        {
            Assert.That(combat.Validate(duty.Player, cure, (ulong)combat.Boss!.GameObjectId), Is.EqualTo(DutyActionRefusal.NoTarget));
            Assert.That(combat.Validate(duty.Player, cure, 0), Is.EqualTo(DutyActionRefusal.NoTarget));
        });
    }

    [Test]
    public void TheSameSeedReplaysTheSameLedger()
    {
        static List<uint?> Play()
        {
            using var duty = new DutyHarness();
            duty.Start(seed: 99);
            duty.RunFor(20f);
            return [duty.Combat.BossHp, .. PerRole.All.Select(role => duty.Combat.Hp(duty.Member(role))), .. PerRole.All.Select(role => duty.Combat.Mp(duty.Member(role)))];
        }

        var baseline = Play();
        var replay = Play();
        Assert.That(replay, Is.EqualTo(baseline));
    }

    [Test]
    public void TheMechanicVerdictReadsVulnsAndTankBustersAndSpendsNothing()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var melee = duty.Member(PartyRole.MeleeDpsA);
        var tank = duty.Member(PartyRole.MainTank);
        var party = duty.Game.World.Party;
        var vuln = VulnerableTo(StatusId.MagicVulnerabilityUp);
        Assert.Multiple(() =>
        {
            Assert.That(DamageCheck.MechanicCause(melee, VulnSpec.None, Severity.Normal, DamageKind.Magic, party), Is.Null);
            Assert.That(DamageCheck.MechanicCause(melee, VulnSpec.None, Severity.TankBuster, DamageKind.Magic, party), Is.EqualTo("tank buster"));
            Assert.That(DamageCheck.MechanicCause(tank, VulnSpec.None, Severity.TankBuster, DamageKind.Magic, party), Is.Null);
            Assert.That(DamageCheck.MechanicCause(melee, VulnSpec.None, Severity.Lethal, DamageKind.Magic, party), Is.EqualTo(""));
        });
        melee.AddStatus(StatusId.MagicVulnerabilityUp, 10f);
        Assert.That(DamageCheck.MechanicCause(melee, vuln, Severity.Normal, DamageKind.Magic, party), Is.EqualTo("had vuln up debuff"));
    }

    [Test]
    public void AVulnThatNeedsMitigationStillDecidesAHumansNumericHit()
    {
        using var duty = new DutyHarness(debugBot: false);
        duty.Start();
        duty.Fake.UserActions.Enabled = true;
        var party = duty.Game.World.Party;
        var vuln = VulnerableTo(StatusId.MagicVulnerabilityUp, requiredMitigation: 0.30f);
        duty.Player.AddStatus(StatusId.MagicVulnerabilityUp, 10f);
        var bot = duty.Member(PartyRole.MeleeDpsA);
        bot.AddStatus(StatusId.MagicVulnerabilityUp, 10f);
        Assert.Multiple(() =>
        {
            Assert.That(DamageCheck.MechanicCause(duty.Player, vuln, Severity.Normal, DamageKind.Magic, party), Is.EqualTo("not enough mitigation for a hit with vuln up"));
            Assert.That(DamageCheck.MechanicCause(bot, vuln, Severity.Normal, DamageKind.Magic, party), Is.Null, "a bot always passes");
        });
    }

    [Test]
    public void ALongFrameGrantsEveryManaTickItCovered()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var member = duty.Member(PartyRole.MeleeDpsA);
        duty.Combat.SetMp(member, 0);

        duty.Combat.Tick(2f * duty.Combat.Tuning.MpTickSeconds + 0.5f);

        Assert.That(duty.Combat.Mp(member), Is.EqualTo(2 * duty.Combat.Tuning.MpPerTick));
    }

    [Test]
    public void ALethalNumericHitKillsAndGodmodeLeavesAWayBack()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        var action = FruActions.PandoraAutoAttack;

        duty.Game.GodMode = true;
        combat.Commit(member, MemberMaxHp * 2);
        combat.DieIfAtZero(member, action);
        Assert.Multiple(() =>
        {
            Assert.That(member.Dead, Is.False);
            Assert.That(combat.Hp(member), Is.EqualTo(1u));
            Assert.That(member.Proxy!.Health, Is.GreaterThan(0u));
        });

        duty.Game.GodMode = false;
        combat.Commit(member, 1);
        combat.DieIfAtZero(member, action);
        Assert.Multiple(() =>
        {
            Assert.That(member.Dead, Is.True);
            Assert.That(member.Proxy!.Health, Is.EqualTo(0u));
        });
    }

    [Test]
    public void APressLandsOnceAndOnlyWhenResolved()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var before = combat.BossHp;

        duty.Press(DutyActions.BurstShot, boss, PlayerActionStep.CastStarted);
        duty.Press(DutyActions.BurstShot, boss, PlayerActionStep.Interrupted);
        Assert.That(combat.BossHp, Is.EqualTo(before), "a step short of resolving");

        duty.Press(DutyActions.BurstShot, boss);
        Assert.That(combat.BossHp, Is.EqualTo(before - Weaponskill));

        duty.Press(DutyActions.BurstShot, boss);
        Assert.That(combat.BossHp, Is.EqualTo(before - Weaponskill), "on its recast");
        Assert.That(duty.AbilityCount(DutyActions.BurstShot), Is.EqualTo(0), "the player's own client plays its animation");
    }

    [Test]
    public void APressNeedsAValidTargetAndRange()
    {
        using var duty = new DutyHarness(PartyRole.MeleeDpsA, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var thrust = ActionOf(JobId.Dragoon, DutyActions.TrueThrust);

        Assert.That(combat.Validate(duty.Player, thrust, (ulong)duty.Member(PartyRole.MainTank).GameObjectId), Is.EqualTo(DutyActionRefusal.NoTarget));

        duty.Player.SetPosition(new Vector3(0f, 0f, 15f));
        duty.Frame();
        Assert.That(combat.Validate(duty.Player, thrust, (ulong)boss.GameObjectId), Is.EqualTo(DutyActionRefusal.OutOfRange));
        var before = combat.BossHp;
        duty.Press(DutyActions.TrueThrust, boss);
        Assert.That(combat.BossHp, Is.EqualTo(before));
    }

    [Test]
    public void APressCostsItsManaAndIsRefusedWithoutIt()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var boss = combat.Boss!;
        var player = duty.Player;

        combat.SetMp(player, 100);
        var before = combat.BossHp;
        duty.Press(DutyActions.Fire, boss);
        Assert.Multiple(() =>
        {
            Assert.That(combat.BossHp, Is.EqualTo(before));
            Assert.That(combat.Mp(player), Is.EqualTo(100u));
        });

        combat.SetMp(player, 1_000);
        duty.Press(DutyActions.Fire, boss);
        Assert.Multiple(() =>
        {
            Assert.That(combat.BossHp, Is.EqualTo(before - Weaponskill));
            Assert.That(combat.Mp(player), Is.EqualTo(800u));
        });
    }

    [Test]
    public void AJobWithoutAKitDoesNothingButPlaysOn()
    {
        const byte samurai = (byte)JobId.Samurai;
        using var duty = new DutyHarness(PartyRole.MeleeDpsA, debugBot: false, job: samurai);
        duty.Start();
        var combat = duty.Combat;
        var before = combat.BossHp;
        duty.Press(DutyActions.TrueThrust, combat.Boss!);
        Assert.That(combat.BossHp, Is.EqualTo(before));
    }

    [Test]
    public void BotDamageIsNamedActionsNotAPassiveTick()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        duty.RunFor(8f);

        var lost = combat.Tuning.BossMaxHp - combat.BossHp;
        var pressed = DutyActions.All.Where(a => a.Kind == DutyActionKind.Damage).Sum(a => duty.AbilityCount(a.ActionId));
        Assert.Multiple(() =>
        {
            Assert.That(lost, Is.GreaterThan(0u));
            Assert.That(lost, Is.EqualTo((uint)pressed * Weaponskill));
        });
    }

    [Test]
    public void AHumanIsNeverPressedFor()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var shot = ActionOf(JobId.Bard, DutyActions.BurstShot);
        duty.RunFor(6f);
        Assert.Multiple(() =>
        {
            Assert.That(combat.Cooldown(duty.Player, shot), Is.EqualTo(0f));
            Assert.That(duty.AbilityCount(DutyActions.BurstShot), Is.EqualTo(0));
        });
    }

    [Test]
    public void MovingLosesAHardCastAndItsCostButNotTheGcd()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var mage = duty.Member(PartyRole.CasterDps);
        var fire = ActionOf(JobId.BlackMage, DutyActions.Fire);
        duty.RunUntil(() => combat.IsCasting(mage), 5f, "the bot's cast");
        var mana = combat.Mp(mage);

        mage.SetPosition(mage.Position + new Vector3(3f, 0f, 0f));
        combat.Tick(DutyHarness.FrameSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(combat.IsCasting(mage), Is.False);
            Assert.That(combat.Mp(mage), Is.EqualTo(mana));
            Assert.That(combat.Cooldown(mage, fire), Is.EqualTo(0f));
        });
    }

    [Test]
    public void AFinishedHardCastCostsAndLands()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var mage = duty.Member(PartyRole.CasterDps);
        var mana = combat.Mp(mage)!.Value;
        duty.RunUntil(() => combat.Mp(mage) < mana, 6f, "the cast to land");
        Assert.That(duty.AbilityCount(DutyActions.Fire), Is.GreaterThan(0));
    }

    [Test]
    public void NothingLandsOnceTheCombatHasClosed()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        combat.DealBossDamage(combat.BossHp);
        duty.Frame();

        combat.Commit(member, 50_000);

        Assert.Multiple(() =>
        {
            Assert.That(combat.Closed, Is.True);
            Assert.That(combat.BossDefeated, Is.True);
            Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp));
        });
    }

    [Test]
    public void NoActionLandsOnceTheBossHasFallen()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        var member = duty.Member(PartyRole.MeleeDpsA);
        combat.Commit(member, 40_000);
        combat.DealBossDamage(combat.BossHp);

        duty.Press(DutyActions.CureII, member);

        Assert.Multiple(() =>
        {
            Assert.That(combat.ActionsOver, Is.True);
            Assert.That(combat.Closed, Is.False);
            Assert.That(combat.Hp(member), Is.EqualTo(MemberMaxHp - 40_000));
        });
    }
}
