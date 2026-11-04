using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using System.Numerics;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruP4DarklitDragonsongJpScenarioTests
{
    private static IEnumerable<TestCaseData> OpeningLayouts()
        => from north in new[] { false, true }
           from strat in Enumerable.Range(0, new FruP4DarklitDragonsongJpScenario().AiStrats.Count)
           select new TestCaseData(north, strat);

    [TestCaseSource(nameof(OpeningLayouts))]
    public void AkhRhaiBaitsAndEscapeFollowShivasOpeningSide(bool north, int strat)
    {
        foreach (var player in PerRole.All)
        {
            var baits = new List<Vector3>();
            var observedBaits = false;
            var observedEscape = false;
            var observedLineup = false;
            var observedChainsLineup = false;
            var run = ScenarioRun.Execute(typeof(FruP4DarklitDragonsongJpScenario), strat, (int)player,
                new ScenarioRunOptions
                {
                    PlayerRole = player,
                    Overrides = o => ((FruP4DarklitDragonsongJpStateOverrides)o).UsurperNorth = north,
                    Probe = p =>
                    {
                        if (p.Crossed(6.5f))
                        {
                            var shiva = p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == BNpcBaseId.UsurperP4);
                            var fragment = p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == BNpcBaseId.FragmentOfFate);
                            var wyrm = p.World.Children.OfType<SimEnemy>().Single(e => e.BNpcBaseId == BNpcBaseId.GreatWyrm);
                            Assert.That(fragment.Position.Z, Is.LessThan(0));
                            Assert.That(shiva.Position.Z, north ? Is.LessThan(0) : Is.GreaterThan(0));
                            Assert.That(Vector3.Distance(wyrm.Position, shiva.Position), Is.LessThan(0.1f));
                            foreach (var role in PerRole.All)
                            {
                                var position = p.Member(role)!.Position;
                                var offset = position - shiva.Position;
                                Assert.That(offset.X, (int)role % 2 == 0 ? Is.LessThan(-5f) : Is.GreaterThan(5f));
                                Assert.That(MathF.Abs(offset.Z), Is.LessThan(0.3f));
                                baits.Add(position);
                            }
                            observedBaits = true;
                        }
                        if (p.Crossed(8.9f))
                        {
                            foreach (var role in PerRole.All)
                            {
                                var position = p.Member(role)!.Position;
                                Assert.That(baits.Min(b => Vector3.Distance(b, position)), Is.GreaterThan(6f));
                                Assert.That(MathF.Abs(position.Z), Is.LessThan(5f));
                            }
                            observedEscape = true;
                        }
                        if (p.Crossed(12.1f) || p.Crossed(21.95f))
                        {
                            PartyRole[] northOrder = [RegenHealer, ShieldHealer, MainTank, OffTank];
                            PartyRole[] southOrder = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
                            foreach (var order in new[] { northOrder, southOrder })
                            {
                                var positions = order.Select(role => p.Member(role)!.Position).ToArray();
                                Assert.That(positions.Select(position => position.X), Is.Ordered.Ascending);
                                Assert.That(positions.All(position => order == northOrder ? position.Z < -3.5f : position.Z > 3.5f), Is.True);
                                Assert.That(MathF.Abs(positions[1].Z), Is.GreaterThan(MathF.Abs(positions[0].Z) + 1.5f));
                                Assert.That(MathF.Abs(positions[2].Z), Is.GreaterThan(MathF.Abs(positions[3].Z) + 1.5f));
                                foreach (var position in positions)
                                    Assert.That(baits.Min(b => Vector3.Distance(b, position)), Is.GreaterThan(6f));
                            }
                            if (p.Crossed(12.1f)) observedLineup = true;
                            if (p.Crossed(21.95f)) observedChainsLineup = true;
                        }
                    },
                });
            Assert.That(run.Passed, Is.True, run.ToString);
            Assert.That(observedBaits && observedEscape, Is.True);
            Assert.That(observedLineup && observedChainsLineup, Is.True);
        }
    }

}
