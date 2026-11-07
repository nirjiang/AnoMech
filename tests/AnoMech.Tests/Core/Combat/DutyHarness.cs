using AnoMech.Core.Combat;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P5CombatPreview;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// The combat preview on a fake client, driven frame by frame across its restarts, which
// ScenarioRun's single-run loop and clock can't span.
internal sealed class DutyHarness : IDisposable
{
    public const float FrameSeconds = 1f / 60f;

    public FakeGame Fake { get; }
    public Game Game { get; }
    public IScenario Scenario { get; }
    public int Starts { get; private set; }
    public int RestartRequests { get; private set; }
    public List<PartyRole> Killed { get; } = [];
    public List<(PartyRole Role, uint? ActionId, float Time)> Deaths { get; } = [];
    public PartyRole Seat { get; }
    private bool disposed;

    public DutyHarness(PartyRole seat = PartyRole.CasterDps, bool debugBot = true, byte? job = null)
        : this(typeof(FruP5CombatPreviewScenario), seat, debugBot, job)
    {
    }

    // A scenario the catalog doesn't list yet runs on its own fresh instance.
    public DutyHarness(Type scenarioType, PartyRole seat = PartyRole.CasterDps, bool debugBot = true, byte? job = null)
    {
        Seat = seat;
        Fake = FakeGame.Install();
        Game? game = null;
        var log = TraceLog.Create(() => game?.World.Events.Elapsed ?? 0f);
        Fake.Timeline.Clock = () => game?.World.Events.Elapsed ?? 0f;
        Fake.Timeline.Source = () => log.LastFired;
        Fake.BattleCharas.Player.ClassJob = job ?? PartyPresets.Standard[(int)seat].ClassJob;
        DalamudServices.Install(nameof(Plugin.Log), log);
        DalamudServices.Install(nameof(Plugin.Config), new Configuration());
        DebugBotControl.Enabled = debugBot;
        PerRole.ForceSeats = true;
        game = new Game();
        Game = game;
        DalamudServices.Install(nameof(Plugin.Game), Game);
        Scenario = Game.Catalog.Scenarios.SingleOrDefault(scenario => scenario.GetType() == scenarioType)
                   ?? (IScenario)Activator.CreateInstance(scenarioType)!;
        Game.RunStarted += (_, _) => Starts++;
        Game.DutyRestartRequested += () => RestartRequests++;
        Game.PartyMemberKilled += (role, _, actionId, _) =>
        {
            Killed.Add(role);
            Deaths.Add((role, actionId, Game.World.Events.Elapsed));
        };
    }

    public int? SelectedAi { get; set; } = 0;

    public RunScenarioParams Params(int? seed) => new(Scenario, new LocalPartyProvider { RoleOverride = Seat }, SelectedAi, 0, seed);

    public void Start(SessionMode mode = SessionMode.Local, int? seed = 1)
    {
        Game.ZoneIn(Scenario.Phase.Zone, mode);
        Game.StartScenario(Params(seed));
        Frame();
    }

    public void Frame() => Fake.Frame(FrameSeconds, Game.Tick);

    public void RunFor(float seconds)
    {
        for (var i = 0; i < seconds / FrameSeconds; i++) Frame();
    }

    public void RunUntil(Func<bool> done, float maxSeconds, string what)
    {
        for (var t = 0f; !done(); t += FrameSeconds)
        {
            if (t > maxSeconds) Assert.Fail($"Gave up waiting for {what} after {maxSeconds}s.");
            Frame();
        }
    }

    public DutyCombat Combat => Game.World.Combat ?? throw new InvalidOperationException("No combat is running.");
    public SimCharacter Member(PartyRole role) => Game.World.Party.Get(role)!;
    public SimPlayer Player => Game.World.Party.Player!;

    // A press the player's own client resolved.
    public void Press(uint actionId, SimCharacter target, PlayerActionStep step = PlayerActionStep.Resolved)
        => Fake.UserActions.Act(new PlayerAction(step, ActionType.Action, actionId, (ulong)target.GameObjectId));

    public void KillAllBots()
    {
        foreach (var member in Game.World.Party.AllMembers().Where(m => m is not SimPlayer).ToList())
            member.Die(SimCharacterDeathExtensions.Environment, "test");
    }

    public int AbilityCount(uint actionId) => AbilityRows(actionId).Count();

    public IEnumerable<Dictionary<string, string>> AbilityRows(uint actionId)
        => Fake.Timeline.Rows.Where(row => row["kind"] == "ability" && row["id"] == actionId.ToString());

    public static string Id(SimCharacter character) => character.EntityId.ToString("X8");

    public static byte ShownShield(SimCharacter character) => ((FakeBattleChara)character.Proxy!).Shield;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DebugBotControl.Enabled = false;
        PerRole.ForceSeats = false;
        Game.Dispose();
    }
}
