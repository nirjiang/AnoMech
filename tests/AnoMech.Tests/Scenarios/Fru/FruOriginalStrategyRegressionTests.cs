using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruOriginalStrategyRegressionTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void DarklitNaKeepsCenterBaitsAndOriginalRoleSpreads(int strat)
    {
        Vector2[] expected = [new(4.28f, -6.96f), new(6.96f, -4.28f), new(-6.96f, -4.28f), new(-4.28f, -6.96f),
            new(4.28f, 6.96f), new(6.96f, 4.28f), new(-6.96f, 4.28f), new(-4.28f, 6.96f)];
        foreach (var player in PerRole.All)
        {
            var sawBaits = false;
            var sawSpreads = false;
            var run = ScenarioRun.Execute(typeof(FruP4DarklitDragonsongScenario), strat, (int)player,
                new ScenarioRunOptions
                {
                    PlayerRole = player,
                    Probe = p =>
                    {
                        if (p.Crossed(6.5f))
                        {
                            var shiva = p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == BNpcBaseId.UsurperP4);
                            Assert.That(shiva.Position, Is.EqualTo(Vector3.Zero));
                            foreach (var role in PerRole.All)
                                Assert.That(p.Member(role)!.Position.Length(), Is.LessThan(1f));
                            sawBaits = true;
                        }
                        if (p.Crossed(8.9f))
                        {
                            foreach (var role in PerRole.All)
                            {
                                var position = p.Member(role)!.Position;
                                Assert.That(Vector2.Distance(new(position.X, position.Z), expected[(int)role]), Is.LessThan(0.4f));
                            }
                            sawSpreads = true;
                        }
                    },
                });
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(sawBaits && sawSpreads, Is.True);
        }
    }

    [TestCase(KnockbackImmunity.ArmsLength)]
    [TestCase(KnockbackImmunity.Surecast)]
    public void CrystallizeNaKeepsItsOriginalUnresistibleWing(ushort status)
    {
        Vector3? before = null, after = null;
        var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeScenario), 0, 0, new ScenarioRunOptions
        {
            StopAt = 55.7f,
            Probe = p =>
            {
                if (p.Crossed(53.8f)) p.Member(PartyRole.MainTank)!.AddStatus(status, 3f);
                if (p.Crossed(55f)) before = p.Member(PartyRole.MainTank)!.Position;
                if (p.Crossed(55.6f)) after = p.Member(PartyRole.MainTank)!.Position;
            },
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(before, Is.Not.Null);
        Assert.That(after, Is.Not.Null);
        Assert.That(Vector3.Distance(before!.Value, after!.Value), Is.GreaterThan(5f));
    }
}
