namespace AnoMech.Scenarios;

// A fight played to its own end rather than to the end of its queue. ScenarioSession gives it the
// duty result policy: a kill is the only clear, and individual deaths neither freeze nor fail the run.
public interface IDutyScenario : IScenario
{
    bool VictoryReached { get; }

    // At a stable point of the frame, with the queue already empty. The session reports the result next.
    void OnVictory();

    // Safe to call more than once: restart, reset, leave, refused or failed start, plugin unload.
    void OnRunEnded();
}
