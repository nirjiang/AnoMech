using System.Collections.Generic;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.P1Fatebreaker;

internal enum FruP1Drill
{
    CyclonicBreakBoss,
    UtopianSky,
    TurnOfTheHeavens,
    FallOfFaith,
    Explosion,
}

// One large P1 guide group on its own, played by the P1 fight itself. Each entry is its own type because the
// catalog's consumers pick a scenario by runtime type.
public abstract class FruP1PracticeScenario : IDutyScenario
{
    private readonly FruP1FatebreakerScenario fight;

    private protected FruP1PracticeScenario(string name, FruP1Drill drill)
    {
        Name = name;
        fight = new FruP1FatebreakerScenario(drill);
    }

    public string Name { get; }
    public IPhase Phase => fight.Phase;
    public IReadOnlyList<IScenarioAi> AiStrats => fight.AiStrats;
    public object SettingsOverrides => fight.SettingsOverrides;
    public bool VictoryReached => fight.VictoryReached;

    public void DrawSettings() => fight.DrawSettings();
    public void Run(SimWorld world, int? selectedAi) => fight.Run(world, selectedAi);
    public void Tick(float delta, float elapsed) => fight.Tick(delta, elapsed);
    public void OnActionResolved(ActionResolved resolved) => fight.OnActionResolved(resolved);
    public void OnStatusLost(StatusLost lost) => fight.OnStatusLost(lost);
    public void OnVictory() => fight.OnVictory();
    public void OnRunEnded() => fight.OnRunEnded();
}

public sealed class FruP1CyclonicBreakBossPractice() : FruP1PracticeScenario("Cyclonic Break (Boss)", FruP1Drill.CyclonicBreakBoss);

public sealed class FruP1UtopianSkyPractice() : FruP1PracticeScenario("Utopian Sky", FruP1Drill.UtopianSky);

public sealed class FruP1TurnOfTheHeavensPractice() : FruP1PracticeScenario("Turn of the Heavens", FruP1Drill.TurnOfTheHeavens);

public sealed class FruP1FallOfFaithPractice() : FruP1PracticeScenario("Fall of Faith", FruP1Drill.FallOfFaith);

public sealed class FruP1ExplosionPractice() : FruP1PracticeScenario("Burnt Strike / Explosion", FruP1Drill.Explosion);
