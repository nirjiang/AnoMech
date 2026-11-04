using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Ai;
using AnoMech.Scenarios;

namespace AnoMech.Windows;

internal sealed class ScenarioRegionMenu
{
    private readonly ScenarioCatalog catalog;
    private readonly Dictionary<IScenario, IScenario> primaryByScenario;
    private readonly Dictionary<IScenario, IReadOnlyList<IScenario>> membersByPrimary;

    public ScenarioRegionMenu(ScenarioCatalog catalog)
    {
        this.catalog = catalog;
        primaryByScenario = catalog.Scenarios.ToDictionary(scenario => scenario, scenario => scenario);
        foreach (var scenario in catalog.Scenarios)
            if (scenario is IScenarioRegionVariant variant)
            {
                var original = catalog.Scenarios.Single(candidate => candidate.GetType() == variant.OriginalScenarioType);
                if (!ReferenceEquals(original.Phase, scenario.Phase))
                    throw new System.InvalidOperationException("Region variants must share the original scenario's phase.");
                primaryByScenario[scenario] = original;
            }
        membersByPrimary = catalog.Scenarios.GroupBy(Primary)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<IScenario>)group.ToArray());
    }

    public IScenario Primary(IScenario scenario) => primaryByScenario[scenario];

    public bool HasVariants(IScenario scenario) => membersByPrimary[Primary(scenario)].Count > 1;

    public IReadOnlyList<IScenario> ScenariosOf(IPhase phase)
        => catalog.ScenariosOf(phase).Select(Primary).Distinct().ToArray();

    public string? Region(IScenario scenario, IScenarioAi strat)
        => strat.Group ?? (HasVariants(scenario) ? "NA" : null);

    public string? DefaultRegion(IScenario scenario)
        => scenario.AiStrats.Select(strat => Region(scenario, strat)).FirstOrDefault(region => region is not null);

    public IReadOnlyList<string> Groups(IScenario scenario)
        => membersByPrimary[Primary(scenario)]
            .SelectMany(member => member.AiStrats.Select(strat => Region(member, strat)))
            .OfType<string>().Distinct().ToArray();

    public IScenario Resolve(IScenario scenario, string? region)
    {
        var primary = Primary(scenario);
        return membersByPrimary[primary]
            .FirstOrDefault(member => member.AiStrats.Any(strat => Region(member, strat) == region)) ?? primary;
    }
}
