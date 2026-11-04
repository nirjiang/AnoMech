using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P5ParadiseRegained.FruP5ParadiseRegainedState;

namespace AnoMech.Scenarios.Fru.P5ParadiseRegained;

public sealed class FruP5ParadiseRegainedJpAi : IScenarioAi<FruP5ParadiseRegainedState>
{
    public string Name => "JP · Nukemaru / H then melee-ranged towers / T-M-R-H";
    public string Group => "JP";

    private FruP5ParadiseRegainedState state = null!;

    public void Run(FruP5ParadiseRegainedState stateParam, SimWorld world)
    {
        state = stateParam;
        var ai = new AiManager(world);
        ai.Move(2f, () => ByRole(StackUnderFirstTower), jitter: 0f, arrivalTime: 8.3f);
        ai.Move(10f, () => ByRole(role => state.Rotate(FirstWingSpot(role))), jitter: 0f, arrivalTime: TowerHitAt(0));
        ai.Move(16.5f, () => ByRole(role => state.Rotate(SecondWingSpot(role))), jitter: 0f, arrivalTime: TowerHitAt(1));
        ai.Move(25.5f, () => ByRole(PolarizingPreposition), jitter: 0f, arrivalTime: 30f);
        ai.Move(30f, () => ByRole(role => LineSpot(role, 0, dodged: false)), jitter: 0f, arrivalTime: LinesAimedAt(0));
        for (var round = 0; round < 4; round++)
        {
            var r = round;
            if (r > 0) ai.Move(LinesAt(r) - 1.4f, () => ByRole(role => LineSpot(role, r, dodged: false)), jitter: 0f, arrivalTime: LinesAimedAt(r));
            ai.Move(LinesAt(r) + 1.2f, () => ByRole(role => LineSpot(role, r, dodged: true)), jitter: 0f, arrivalTime: EchoAt(r));
        }
    }

    private static float LinesAimedAt(int round) => LinesAt(round) - 0.1f;

    private Vector3? StackUnderFirstTower(PartyRole role) => state.Rotate(new Vector3(((int)role - 3.5f) * 0.35f, 0f, 7f));

    private float FirstWingSide => state.DarkFirst ? -1f : 1f;

    private Vector3 FirstWingSpot(PartyRole role) => role switch
    {
        PartyRole.MainTank => new(0f, 0f, -7f),
        PartyRole.OffTank => state.DarkFirst ? new(-1f, 0f, 0f) : new(10f, 0f, 10f),
        PartyRole.RegenHealer or PartyRole.ShieldHealer => new(FirstWingSide * (0.6f + ((int)role % 2) * 0.4f), 0f, 7f),
        _ => new(FirstWingSide * 3.5f, 0f, 9.5f),
    };

    private Vector3 SecondWingSpot(PartyRole role) => role switch
    {
        PartyRole.MainTank => new(0f, 0f, state.DarkFirst ? -14f : -1f),
        PartyRole.OffTank => new(state.DarkFirst ? -6.0622f : 6.0622f, 0f, 3.5f),
        PartyRole.RegenHealer => new(-0.4f, 0f, -7.5f),
        PartyRole.ShieldHealer => new(0.4f, 0f, -7.5f),
        PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => new(state.DarkFirst ? 5f : -5f, 0f, -4.5f),
        _ => new(state.DarkFirst ? -5f : 5f, 0f, -4.5f),
    };

    private static int BaitOrder(PartyRole role) => role switch
    {
        PartyRole.MainTank or PartyRole.OffTank => 0,
        PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => 1,
        PartyRole.PhysRangedDps or PartyRole.CasterDps => 2,
        _ => 3,
    };

    private static bool StartsWest(PartyRole role)
        => role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;

    private static Vector3? PolarizingPreposition(PartyRole role)
        => BaitOrder(role) == 0 ? new Vector3(0f, 0f, -3.5f) : LineSpot(role, 0, dodged: false);

    private static Vector3? LineSpot(PartyRole role, int round, bool dodged)
    {
        var order = BaitOrder(role);
        var crossed = dodged ? order <= round : order < round;
        var sign = StartsWest(role) != crossed ? -1f : 1f;
        if (!dodged)
        {
            var depth = order == round ? 4f : 6f;
            return new Vector3(sign * depth, 0f, depth);
        }
        var next = order == Math.Min(round + 1, 3);
        return new Vector3(sign * (next ? 0.6f : 1.8f), 0f, next ? 6.2f : 8.5f);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
