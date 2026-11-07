using System.Globalization;
using System.Numerics;
using AnoMech.Core.Combat;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Fru.P1Fatebreaker;

namespace AnoMech.Tests;

// P1 on the duty harness with no strat running: the party stands where a test puts it.
internal static class FruP1Run
{
    private static readonly DutyTuning LongFight = FruP1FatebreakerScenario.DefaultTuning with { BossMaxHp = 1_000_000_000 };

    public static DutyHarness Start(Action<FruP1FatebreakerStateOverrides>? overrides = null, bool godMode = false, bool longFight = false, int seed = 1, bool withStrat = false)
    {
        var duty = new DutyHarness(typeof(FruP1FatebreakerScenario), PartyRole.CasterDps, debugBot: false) { SelectedAi = withStrat ? 0 : null };
        var scenario = (FruP1FatebreakerScenario)duty.Scenario;
        overrides?.Invoke(scenario.Overrides);
        if (longFight) scenario.Tuning = LongFight;
        duty.Game.GodMode = godMode;
        duty.Start(seed: seed);
        return duty;
    }

    public static float Now(this DutyHarness duty) => duty.Game.World.Events.Elapsed;

    public static void RunTo(this DutyHarness duty, float time)
        => duty.RunUntil(() => duty.Now() >= time, time + 5f, $"t={time}");

    public static void Place(this DutyHarness duty, PartyRole role, float x, float z)
        => duty.Member(role).SetPosition(new Vector3(x, 0f, z));

    public static void PlaceAll(this DutyHarness duty, float x, float z)
    {
        foreach (var role in PerRole.All) duty.Place(role, x, z);
    }

    public static IReadOnlyList<float> AbilityTimes(this DutyHarness duty, uint actionId)
        => duty.AbilityRows(actionId).Select(row => float.Parse(row["t"], CultureInfo.InvariantCulture)).ToList();

    public static IReadOnlyList<float> AbilityRotationsAt(this DutyHarness duty, uint actionId, float time)
        => duty.AbilityRows(actionId)
            .Where(row => MathF.Abs(float.Parse(row["t"], CultureInfo.InvariantCulture) - time) < 0.1f)
            .Select(row => float.Parse(row["rot"], CultureInfo.InvariantCulture))
            .ToList();

    public static IEnumerable<SimEnemy> Enemies(this DutyHarness duty, uint baseId)
        => duty.Game.World.Children.OfType<SimEnemy>().Where(enemy => enemy.BNpcBaseId == baseId);
}
