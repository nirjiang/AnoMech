using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantJpState;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public sealed class FruP2LightRampantJpAi : IScenarioAi<FruP2LightRampantJpState>
{
    public string Name => "JP · Lily Doll HT / ST-D4 adjust / clockwise puddles";
    public string Group => "JP";

    private const float S = 1f / 2.358f;

    private static readonly PartyRole[] LineupPriority =
        [PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank,
         PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
    private static readonly int[] LineupTowers = [0, 1, 2, 5, 4, 3, 6, 7];

    private FruP2LightRampantJpState state = null!;
    private SimWorld world = null!;
    private readonly int[] slots = new int[8];

    public void Run(FruP2LightRampantJpState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        AssignLineup();
        var ai = new AiManager(world);
        ai.Move(3f, () => ByRole(Preposition), arrivalTime: 8.75f);
        ai.Move(9.75f, () => ByRole(role => state.BaitsHammer(role) ? Lineup(role) : null), arrivalTime: HammerSnapshotAt(0));
        ai.Move(10.75f, () => ByRole(role => state.BaitsHammer(role) ? null : Lineup(role)), arrivalTime: 12.3f);
        ai.Move(12.3f, () => ByRole(role => state.BaitsHammer(role) ? null : Tower(Index(role))), jitter: 0f, arrivalTime: CursesAt);
        for (var wave = 0; wave < HammerWaves - 1; wave++)
        {
            var dropped = wave + 1;
            ai.Move(HammerSnapshotAt(wave) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? HammerSpot(role, dropped) : null), jitter: 0f,
                arrivalTime: HammerSnapshotAt(wave + 1));
        }
        ai.Move(HammerSnapshotAt(HammerWaves - 1) + 0.05f, () => ByRole(role => state.BaitsHammer(role) ? Intermediate(role) : null),
            arrivalTime: PowerfulLightAt);
        ai.Move(19.1f, () => ByRole(role => state.BaitsHammer(role) ? null : GroupSpot(role)), arrivalTime: 23.3f);
        ai.Move(23.3f, () => ByRole(role => state.BaitsHammer(role) ? null : Intermediate(role)), arrivalTime: PowerfulLightAt);
        ai.Move(PowerfulLightAt + 0.05f, () => ByRole(role => OrbSafeSpot(role, first: true)), jitter: 0f, arrivalTime: 27.1f);
        ai.Move(27.2f, () => ByRole(role => OrbSafeSpot(role, first: false)), jitter: 0f, arrivalTime: 30.1f);
        ai.Move(30.15f, () => ByRole(role => Stacks(role) == 2 ? Vector3.Zero : MiddleWait(role)), arrivalTime: 33.8f);
        ai.Move(33.85f, () => ByRole(BanishSpot), arrivalTime: 37.1f);
        ai.Move(37.15f, () => ByRole(ClockSpot), arrivalTime: 45.9f);
    }

    private static float HammerSnapshotAt(int wave) => HammerAt(wave) - FruHolyPuddles.SnapshotBeforeHit;

    private int Stacks(PartyRole role) => world.Party.Get(role)?.FindStatus(StatusId.Lightsteeped)?.Stacks ?? 0;

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private void AssignLineup()
    {
        var north = LineupPriority.Take(4).Where(r => !state.BaitsHammer(r)).ToList();
        var south = LineupPriority.Skip(4).Where(r => !state.BaitsHammer(r)).ToList();
        if (north.Count == 4) { south.Add(north[^1]); north.RemoveAt(3); }
        if (south.Count == 4) { north.Add(south[^1]); south.RemoveAt(3); }
        var order = north.Concat(south).Concat(LineupPriority.Where(state.BaitsHammer)).ToArray();
        for (var i = 0; i < 8; i++) slots[(int)order[i]] = LineupTowers[i];
    }

    private int Index(PartyRole role) => slots[(int)role];
    private bool NorthGroup(PartyRole role) => Index(role) % 2 == 0;

    private static Vector3? Preposition(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(14.5f, 4.5f),
        PartyRole.OffTank => FromSim(11, 11),
        PartyRole.RegenHealer => FromSim(11, -11),
        PartyRole.ShieldHealer => FromSim(14.5f, -4.5f),
        PartyRole.MeleeDpsA => FromSim(-11, -11),
        PartyRole.MeleeDpsB => FromSim(-14.5f, -4.5f),
        PartyRole.PhysRangedDps => FromSim(-14.5f, 4.5f),
        _ => FromSim(-11, 11),
    };

    private Vector3? Lineup(PartyRole role) => state.BaitsHammer(role) ? HammerSpot(role, 0) : Index(role) switch
    {
        0 => FromSim(7, 13.7f),
        1 => FromSim(16.1f, 0),
        2 => FromSim(7, -13.7f),
        3 => FromSim(-7, -13.7f),
        4 => FromSim(-16.1f, 0),
        5 => FromSim(-7, 13.7f),
        6 => FromSim(0, -45),
        _ => FromSim(0, 45),
    };

    private Vector3? HammerSpot(PartyRole role, int dropped)
    {
        var step = dropped switch
        {
            0 => 0f,
            1 => 1f,
            2 => 1.7f,
            3 => 2f,
            _ => 2.3f,
        };
        var octant = (Index(role) == 6 ? 5 : 1) + step;
        var radius = dropped switch
        {
            < 3 => 8.5f,
            3 => 12f,
            _ => 18f,
        };
        return new Vector3(MathF.Sin(octant * MathF.PI / 4), 0, -MathF.Cos(octant * MathF.PI / 4)) * radius;
    }

    private Vector3? GroupSpot(PartyRole role) => FromSim(NorthGroup(role) ? 45 : -45, 0);

    private Vector3? Intermediate(PartyRole role) => FromSim(43, 10.8f) * (NorthGroup(role) ? 1 : -1);

    private Vector3? OrbSafeSpot(PartyRole role, bool first)
    {
        var wideNorth = first == state.NorthOrbsFirst;
        return NorthGroup(role)
            ? FromSim(wideNorth ? 36.2f : 42, wideNorth ? 26.6f : 16.7f)
            : FromSim(wideNorth ? -42 : -36.2f, wideNorth ? -16.7f : -26.6f);
    }

    private Vector3 MiddleWait(PartyRole role) => NorthGroup(role)
        ? state.NorthOrbsFirst ? FromSim(16.1f, 0) : FromSim(7, 12)
        : state.NorthOrbsFirst ? FromSim(-7, -12) : FromSim(-16.1f, 0);

    private Vector3? BanishSpot(PartyRole role) => state.Pairs && (int)role < 4
        ? ClockSpot(role switch
        {
            PartyRole.MainTank => PartyRole.PhysRangedDps,
            PartyRole.OffTank => PartyRole.MeleeDpsB,
            PartyRole.RegenHealer => PartyRole.MeleeDpsA,
            _ => PartyRole.CasterDps,
        })
        : ClockSpot(role);

    private static Vector3? ClockSpot(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(18, 0),
        PartyRole.OffTank => FromSim(-18, 0),
        PartyRole.RegenHealer => FromSim(0, -18),
        PartyRole.ShieldHealer => FromSim(0, 18),
        PartyRole.MeleeDpsA => FromSim(-12, -12),
        PartyRole.MeleeDpsB => FromSim(-12, 12),
        PartyRole.PhysRangedDps => FromSim(12, -12),
        _ => FromSim(12, 12),
    };

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
