using AnoMech.Core.Combat;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.P5CombatPreview;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruP5CombatPreviewScenarioTests
{
    private static List<Dictionary<string, string>> Abilities(SimTimeline timeline)
        => timeline.Rows.Where(row => row["kind"] == "ability").ToList();

    private static int Count(IEnumerable<Dictionary<string, string>> rows, uint actionId)
        => rows.Count(row => row["id"] == actionId.ToString());

    [TestCase(11)]
    [TestCase(22)]
    [TestCase(33)]
    public void BothMechanicsPlayInFullOnOnePandoraToAKill(int seed)
    {
        SimTimeline? timeline = null;
        var run = ScenarioRun.Execute(typeof(FruP5CombatPreviewScenario), 0, seed, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            Probe = _ => timeline ??= ((FakeBattleCharas)Natives.BattleCharas).Timeline,
        });
        Assert.That(run.Passed, Is.True, run.ToString);

        var abilities = Abilities(timeline!);
        var order = abilities.Select(row => row["id"]).ToList();
        uint[] expected =
        [
            ActionId.FulgentBlade, ActionId.PathOfLightFirst, ActionId.AkhMornPandoraLeft,
            ActionId.ParadiseRegained, ActionId.ParadiseTowerExplosion, ActionId.PolarizingStrikes,
            ActionId.CruelPathOfLight, ActionId.CruelPathOfLightEcho,
        ];
        var firsts = expected.Select(id => order.IndexOf(id.ToString())).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(firsts, Has.None.EqualTo(-1), "an action never went out");
            Assert.That(firsts, Is.Ordered, "out of order");
            Assert.That(Count(abilities, ActionId.AkhMornPandora), Is.EqualTo(1));
            Assert.That(Count(abilities, ActionId.ParadiseTowerExplosion), Is.EqualTo(3));
            Assert.That(Count(abilities, ActionId.CruelPathOfLight), Is.EqualTo(4));
            Assert.That(Count(abilities, ActionId.CruelPathOfDarkness), Is.EqualTo(4));
            Assert.That(Count(abilities, ActionId.CruelPathOfLightEcho), Is.EqualTo(4));
            Assert.That(Count(abilities, ActionId.CruelPathOfDarknessEcho), Is.EqualTo(4));
            Assert.That(timeline!.Rows.Count(row => row["kind"] == "spawn" && row["id"] == BNpcBaseId.Pandora.ToString()), Is.EqualTo(1));
        });
    }

    [Test]
    public void HealersActuallyHealInTheFullFight()
    {
        SimTimeline? timeline = null;
        var run = ScenarioRun.Execute(typeof(FruP5CombatPreviewScenario), 0, 7, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            Probe = _ => timeline ??= ((FakeBattleCharas)Natives.BattleCharas).Timeline,
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        var abilities = Abilities(timeline!);
        var heals = Count(abilities, DutyActions.CureII) + Count(abilities, DutyActions.Medica)
                    + Count(abilities, DutyActions.Adloquium) + Count(abilities, DutyActions.Succor);
        Assert.That(heals, Is.GreaterThan(0));
    }

    [Test]
    public void TheLedgerAndPandoraCarryAcrossTheMechanics()
    {
        DutyCombat? before = null;
        SimEnemy? pandora = null;
        uint bossBefore = 0;
        var checkedAfter = false;
        var run = ScenarioRun.Execute(typeof(FruP5CombatPreviewScenario), 0, 5, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            Probe = p =>
            {
                if (p.Crossed(37.5f))
                {
                    before = p.World.Combat;
                    pandora = before!.Boss;
                    bossBefore = before.BossHp;
                }
                if (p.Crossed(45f))
                {
                    Assert.That(p.World.Combat, Is.SameAs(before));
                    Assert.That(p.World.Combat!.Boss, Is.SameAs(pandora));
                    Assert.That(p.World.Children.OfType<SimEnemy>().Count(e => e.BNpcBaseId == BNpcBaseId.Pandora), Is.EqualTo(1));
                    Assert.That(p.World.Combat.BossHp, Is.LessThan(bossBefore));
                    checkedAfter = true;
                }
            },
        });
        Assert.That(run.Failure, Is.Null, run.ToString);
        Assert.That(checkedAfter, Is.True);
    }

    [Test]
    public void AnEarlyKillEndsTheFightCleanly()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps);
        duty.Start();
        duty.RunFor(20f);
        var combat = duty.Combat;
        var pandora = combat.Boss!;
        combat.DealBossDamage(combat.BossHp);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(pandora.Dead, Is.True);
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Succeeded));
            Assert.That(duty.Game.World.Combat, Is.Null);
        });

        var killedBefore = duty.Killed.Count;
        duty.RunFor(120f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Killed, Has.Count.EqualTo(killedBefore));
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Succeeded));
            Assert.That(duty.AbilityCount(ActionId.ParadiseRegained), Is.EqualTo(0), "the rest of the timeline went out");
        });
    }

    private const string TowerShown = "flags=0x20001";
    private const string TowerHidden = "flags=0x80004";

    private static List<string> MapEffectSlots(DutyHarness duty, string flags)
        => duty.Fake.Timeline.Rows.Where(row => row["kind"] == "map_effect" && row["detail"].EndsWith(flags)).Select(row => row["id"]).ToList();

    private static int MapEffectRows(DutyHarness duty)
        => duty.Fake.Timeline.Rows.Count(row => row["kind"].StartsWith("map_effect"));

    [Test]
    public void AKillWithTowersUpTakesThemDownAndNothingElse()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps);
        duty.Start();
        duty.Fake.MapEffects.Loaded = true;
        duty.RunFor(38f + 11f);
        var shown = MapEffectSlots(duty, TowerShown);
        Assert.That(shown, Has.Count.EqualTo(2), "two towers up, none resolved");
        Assert.That(MapEffectSlots(duty, TowerHidden), Is.Empty);

        var combat = duty.Combat;
        combat.DealBossDamage(combat.BossHp);
        duty.Frame();

        Assert.That(MapEffectSlots(duty, TowerHidden), Is.EquivalentTo(shown));
        var rowsAfterKill = MapEffectRows(duty);
        duty.RunFor(5f);
        Assert.That(MapEffectRows(duty), Is.EqualTo(rowsAfterKill), "a tower still ticking, or the arena touched");
    }

    [Test]
    public void ARestartWithTowersUpTakesThemDownToo()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps);
        duty.Start();
        duty.Fake.MapEffects.Loaded = true;
        duty.RunFor(38f + 11f);
        var shown = MapEffectSlots(duty, TowerShown);

        duty.Player.Die(SimCharacterDeathExtensions.Environment, "test");
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(MapEffectSlots(duty, TowerHidden), Is.EquivalentTo(shown));
        });
    }

    [Test]
    public void AnEmptyQueueIsNotAClear()
    {
        using var duty = new DutyHarness();
        duty.Start();
        duty.Game.World.Events.Clear();
        duty.RunFor(3f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Game.World.Events.IsEmpty, Is.True);
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Running));
        });
    }
}
