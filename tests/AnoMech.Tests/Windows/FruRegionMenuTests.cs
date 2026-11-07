using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Fru.P1Fatebreaker;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using AnoMech.Scenarios.Fru.P5CombatPreview;
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
        var originals = catalog.Scenarios.Where(scenario => scenario.Phase.Zone is FruZone && scenario is not IScenarioRegionVariant and not IDutyScenario).ToArray();
        Assert.That(originals, Has.Length.EqualTo(8));
        foreach (var phase in catalog.PhasesOf(FruZone.Instance))
            Assert.That(menu.ScenariosOf(phase).Where(scenario => scenario is not IDutyScenario), Is.EqualTo(originals.Where(scenario => scenario.Phase == phase).ToArray()));
    }

    [Test]
    public void TheCombatPreviewIsOneMoreButtonOnP5Only()
    {
        var catalog = ScenarioCatalog.Create();
        var menu = new ScenarioRegionMenu(catalog);
        var preview = catalog.Scenarios.OfType<FruP5CombatPreviewScenario>().Cast<IDutyScenario>().Single();
        Assert.That(preview.Name, Does.Contain("preview").And.Contain("experimental"));
        Assert.That(preview.Phase, Is.SameAs(FruZone.P5));
        Assert.That(preview.SupportsMultiplayer, Is.False);
        Assert.That(preview, Is.Not.InstanceOf<IScenarioRegionVariant>());
        Assert.That(menu.ScenariosOf(FruZone.P5).Count(scenario => ReferenceEquals(scenario, preview)), Is.EqualTo(1));
        foreach (var phase in catalog.PhasesOf(FruZone.Instance).Where(phase => phase != FruZone.P5 && phase != FruZone.P1))
            Assert.That(menu.ScenariosOf(phase), Has.None.InstanceOf<IDutyScenario>());
    }

    [Test]
    public void TheP1StageLeadsItsOwnFirstPhaseAheadOfItsFiveGuideGroups()
    {
        var catalog = ScenarioCatalog.Create();
        var menu = new ScenarioRegionMenu(catalog);
        var stage = catalog.Scenarios.OfType<FruP1FatebreakerScenario>().Cast<IDutyScenario>().Single();
        Assert.That(stage.Name, Does.Contain("experimental"));
        Assert.That(stage.Phase, Is.SameAs(FruZone.P1));
        Assert.That(stage.SupportsMultiplayer, Is.False);
        Assert.That(stage, Is.Not.InstanceOf<IScenarioRegionVariant>());
        Assert.That(stage.AiStrats.All(ai => ai.Group == "JP"), Is.True);
        var drills = catalog.Scenarios.OfType<FruP1PracticeScenario>().ToArray();
        Assert.That(drills, Has.Length.EqualTo(5));
        Assert.That(menu.ScenariosOf(FruZone.P1), Is.EqualTo(new IScenario[] { stage }.Concat(drills)));
        Assert.That(drills.Select(drill => drill.FullName()).Distinct().Count(), Is.EqualTo(drills.Length));
        Assert.That(drills.All(drill => menu.DefaultRegion(drill) == "JP" && !((IScenario)drill).SupportsMultiplayer), Is.True);
        Assert.That(catalog.PhasesOf(FruZone.Instance)[0], Is.SameAs(FruZone.P1));
        Assert.That(catalog.Scenarios.OfType<IDutyScenario>().Count(), Is.EqualTo(2 + drills.Length));
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
