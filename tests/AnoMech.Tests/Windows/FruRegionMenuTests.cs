using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using AnoMech.Windows;
using static AnoMech.Scenarios.Fru.FruConstants;

namespace AnoMech.Tests;

public class FruRegionMenuTests
{
    private static IEnumerable<TestCaseData> RegionPairs()
        => ScenarioCatalog.Create().Scenarios.OfType<IScenarioRegionVariant>()
            .Select(variant => new TestCaseData(variant.OriginalScenarioType, variant.GetType()));

    [TestCaseSource(nameof(RegionPairs))]
    public void BothRegionsShareOneMechanicAndRouteToTheirOwnImplementation(Type originalType, Type jpType)
    {
        var catalog = ScenarioCatalog.Create();
        var menu = new ScenarioRegionMenu(catalog);
        var original = catalog.Scenarios.Single(scenario => scenario.GetType() == originalType);
        var jp = catalog.Scenarios.Single(scenario => scenario.GetType() == jpType);
        var phaseMenu = menu.ScenariosOf(original.Phase);

        Assert.That(phaseMenu.Count(scenario => scenario == original), Is.EqualTo(1));
        Assert.That(phaseMenu, Does.Not.Contain(jp));
        Assert.That(menu.Primary(jp), Is.SameAs(original));
        Assert.That(menu.Groups(original), Is.EqualTo(new[] { "NA", "JP" }));
        Assert.That(menu.Groups(jp), Is.EqualTo(menu.Groups(original)));
        Assert.That(menu.DefaultRegion(original), Is.EqualTo("NA"));
        Assert.That(menu.DefaultRegion(jp), Is.EqualTo("JP"));
        Assert.That(menu.Resolve(original, "JP"), Is.SameAs(jp));
        Assert.That(menu.Resolve(jp, "NA"), Is.SameAs(original));
        Assert.That(menu.Resolve(original, null), Is.SameAs(original));
        Assert.That(original.AiStrats.All(ai => ai.Group is null or "NA"), Is.True);
        Assert.That(jp.AiStrats.All(ai => ai.Group == "JP"), Is.True);
    }

    [Test]
    public void FruPhasesKeepTheEightOriginalMechanicButtons()
    {
        var catalog = ScenarioCatalog.Create();
        var menu = new ScenarioRegionMenu(catalog);
        var originals = catalog.Scenarios.Where(scenario => scenario.Phase.Zone is FruZone && scenario is not IScenarioRegionVariant).ToArray();
        Assert.That(originals, Has.Length.EqualTo(8));
        foreach (var phase in catalog.PhasesOf(FruZone.Instance))
            Assert.That(menu.ScenariosOf(phase), Is.EqualTo(originals.Where(scenario => scenario.Phase == phase).ToArray()));
    }

    [Test]
    public void OtherEncountersKeepTheirOriginalMenusAndGroups()
    {
        var catalog = ScenarioCatalog.Create();
        var menu = new ScenarioRegionMenu(catalog);
        foreach (var zone in catalog.Zones.Where(zone => zone is not FruZone))
            foreach (var phase in catalog.PhasesOf(zone))
            {
                Assert.That(menu.ScenariosOf(phase), Is.EqualTo(catalog.ScenariosOf(phase)));
                foreach (var scenario in catalog.ScenariosOf(phase))
                {
                    Assert.That(menu.Groups(scenario), Is.EqualTo(scenario.AiStrats.Select(ai => ai.Group).OfType<string>().Distinct().ToArray()));
                    foreach (var ai in scenario.AiStrats)
                        Assert.That(menu.Region(scenario, ai), Is.EqualTo(ai.Group));
                }
            }
    }

    [TestCase("NA")]
    [TestCase("JP")]
    public void DarklitRegionSelectionRunsTheMatchingOpening(string region)
    {
        var catalog = ScenarioCatalog.Create();
        var menu = new ScenarioRegionMenu(catalog);
        var mechanic = catalog.Scenarios.Single(scenario => scenario.GetType() == typeof(FruP4DarklitDragonsongScenario));
        var selected = menu.Resolve(mechanic, region);
        var observed = false;
        var run = ScenarioRun.Execute(selected.GetType(), 0, 0, new ScenarioRunOptions
        {
            Probe = p =>
            {
                if (!p.Crossed(6.5f)) return;
                var shiva = p.World.Children.OfType<SimEnemy>().Single(enemy => enemy.BNpcBaseId == BNpcBaseId.UsurperP4);
                Assert.That(shiva.Position.Z, Is.EqualTo(region == "NA" ? 0f : 12f));
                foreach (var role in PerRole.All)
                {
                    var member = p.Member(role)!;
                    if (region == "NA") Assert.That(member.Position.Length(), Is.LessThan(1f));
                    else Assert.That(MathF.Abs(member.Position.X - shiva.Position.X), Is.GreaterThan(9f));
                }
                observed = true;
            },
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(observed, Is.True);
    }
}
