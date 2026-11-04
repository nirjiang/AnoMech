using AnoMech.Scenarios;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using AnoMech.Scenarios.Fru.P2LightRampant;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using System.Numerics;

namespace AnoMech.Tests;

public class FruJpScenarioTests
{
    private static IEnumerable<TestCaseData> Strategies()
        => from scenario in ScenarioCatalog.Create().Scenarios
           where scenario.Phase.Zone is global::AnoMech.Scenarios.Fru.FruZone
           from entry in scenario.AiStrats.Select((ai, index) => (ai, index))
           where entry.ai.Group == "JP"
           select new TestCaseData(scenario.GetType(), entry.index)
               .SetName($"JP {scenario.DisplayName()} [{entry.ai.Name}]");

    [TestCaseSource(nameof(Strategies))]
    public void EverySeatClearsRandomizedMechanics(Type scenario, int strat)
    {
        for (var seed = 0; seed < 32; seed++)
        {
            var run = ScenarioRun.Execute(scenario, strat, seed, new ScenarioRunOptions { PlayerRole = (PartyRole)(seed % 8) });
            Assert.That(run.Passed, Is.True, run.ToString);
        }
    }

    [Test]
    public void DiamondDustCoversEveryIcicleMirrorKickAndMarkerCombination()
    {
        for (var icicle = 0; icicle < 4; icicle++)
            for (var mirror = 0; mirror < 8; mirror++)
                foreach (var axe in new[] { false, true })
                    foreach (var supports in new[] { false, true })
                    {
                        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustJpScenario), 0, icicle * 8 + mirror,
                            new ScenarioRunOptions { Overrides = o =>
                            {
                                var roll = (FruP2DiamondDustStateOverrides)o;
                                roll.FirstIcicle = icicle;
                                roll.MirrorOctant = mirror;
                                roll.Axe = axe;
                                roll.SupportsMarked = supports;
                            }});
                        Assert.That(run.Passed, Is.True, run.ToString);
                    }
    }

    [Test]
    public void LightRampantCoversEveryBaitPairAndBothOrbOrders()
    {
        for (var first = 0; first < 8; first++)
            for (var second = first + 1; second < 8; second++)
                foreach (var northFirst in new[] { false, true })
                {
                    var run = ScenarioRun.Execute(typeof(FruP2LightRampantJpScenario), 0, first * 8 + second,
                        new ScenarioRunOptions { Overrides = o =>
                        {
                            var roll = (FruP2LightRampantStateOverrides)o;
                            foreach (var role in PerRole.All)
                                roll.Duty[role] = (int)role == first || (int)role == second ? LightRampantDuty.Bait : LightRampantDuty.Tether;
                            roll.NorthOrbsFirst = northFirst;
                        }});
                    Assert.That(run.Passed, Is.True, run.ToString);
                }
    }

    [Test]
    public void CrystallizeTimeCoversEveryCornerHourglassAndSpiritTakerTarget()
    {
        foreach (var corner in Enum.GetValues<CrystallizeCorner>())
            foreach (var slowNorthwest in new[] { false, true })
                foreach (var target in PerRole.All)
                {
                    var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeJpScenario), 0, (int)corner * 8 + (int)target,
                        new ScenarioRunOptions { Overrides = o =>
                        {
                            var roll = (FruP4CrystallizeTimeStateOverrides)o;
                            roll.Corner = corner;
                            roll.SlowNorthwest = slowNorthwest;
                            roll.SpiritTakerTarget = target;
                        }});
                    Assert.That(run.Passed, Is.True, run.ToString);
                }
    }

    [Test]
    public void ParadiseCoversEveryTowerAndWingOrder()
    {
        for (var rotation = 0; rotation < 3; rotation++)
            foreach (var eastSecond in new[] { false, true })
                foreach (var darkFirst in new[] { false, true })
                {
                    var run = ScenarioRun.Execute(typeof(FruP5ParadiseRegainedJpScenario), 0, rotation,
                        new ScenarioRunOptions { Overrides = o =>
                        {
                            var roll = (FruP5ParadiseRegainedStateOverrides)o;
                            roll.Rotation = rotation;
                            roll.EastSecond = eastSecond;
                            roll.DarkFirst = darkFirst;
                        }});
                    Assert.That(run.Passed, Is.True, run.ToString);
                }
    }

    [TestCase(KnockbackImmunity.ArmsLength)]
    [TestCase(KnockbackImmunity.Surecast)]
    [TestCase((ushort)0)]
    public void KnockbackImmunityPreservesYReturnForTheFirstWing(ushort status)
    {
        Vector3? before = null, after = null;
        var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeJpScenario), 0, 0, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            StopAt = 55.7f,
            Probe = p =>
            {
                if (p.Crossed(53.8f))
                {
                    p.Member(PartyRole.MainTank)!.RemoveStatus(KnockbackImmunity.ArmsLength);
                    if (status != 0) p.Member(PartyRole.MainTank)!.AddStatus(status, 3f);
                }
                if (p.Crossed(55f)) before = p.Member(PartyRole.MainTank)!.Position;
                if (p.Crossed(55.6f)) after = p.Member(PartyRole.MainTank)!.Position;
            },
        });
        if (status == 0)
        {
            Assert.That(run.Deaths.Any(d => d.Role == PartyRole.MainTank) ||
                before.HasValue && after.HasValue && Vector3.Distance(before.Value, after.Value) > 5f, Is.True, run.ToString);
            return;
        }
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(before, Is.Not.Null);
        Assert.That(after, Is.Not.Null);
        Assert.That(Vector3.Distance(before!.Value, after!.Value), Is.LessThan(0.1f));
    }
}
