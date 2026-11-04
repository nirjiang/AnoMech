using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4DarklitDragonsong.FruP4DarklitDragonsongJpState;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongJpAi(bool playerBaitsSomberDance, bool jp = true) : IScenarioAi<FruP4DarklitDragonsongJpState>
{
    public string Name => jp
        ? playerBaitsSomberDance ? "JP · Nukemaru HT / adjust before fans / you take Somber Dance" : "JP · Nukemaru HT / adjust before fans"
        : playerBaitsSomberDance ? "NA · you take Somber Dance" : "NA";
    public string Group => jp ? "JP" : "NA";

    private const float S = 1f / 2.358f;

    private FruP4DarklitDragonsongJpState state = null!;
    private SimWorld world = null!;
    private PartyRole[] swapped = [];
    private PartyRole[] lineup = [];
    private PartyRole somberTank;
    private bool baitEast;

    public void Run(FruP4DarklitDragonsongJpState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        lineup = jp ? JpLineup() : state.LineupOrder.ToArray();
        swapped = SwapFreeWaterToItsGroup();
        somberTank = ChooseSomberTank();
        var ai = new AiManager(world);
        ai.Move(2f, () => ByRole(OpeningBait), jitter: 0.15f, arrivalTime: 6.6f);
        ai.Move(6.65f, () => ByRole(OpeningSpread), arrivalTime: 8.8f);
        if (jp)
            ai.Move(8.9f, () => ByRole(JpLineupSpot), arrivalTime: 12f);
        else
            ai.Move(14.5f, () => ByRole(Lineup), arrivalTime: 24.2f);
        ai.Move(24.2f, () => ByRole(role => Bowtie(role, lineup)), arrivalTime: 28.2f);
        ai.Move(28.2f, () => ByRole(role => Bowtie(role, swapped)), arrivalTime: TowersAt);
        ai.Move(33.1f, () => ByRole(SpiritTakerSpread), arrivalTime: 35.6f);
        ai.Move(35.65f, () => ByRole(WaterStack), arrivalTime: 40f);
        world.Events.Add(40.5f, BaitSomberDanceAwayFromOracle);
        ai.Move(40.6f, () => ByRole(role => role == somberTank ? FromSim(0, baitEast ? 43 : -43) : null), arrivalTime: 43.8f);
        ai.Move(42.4f, () => ByRole(role => role == somberTank ? null : DanceParty(role)), arrivalTime: 43.8f);
        ai.Move(45f, () => ByRole(role => role == somberTank ? JustPastOracleFromCenter() : null), jitter: 0f, arrivalTime: 47f);
        ai.Move(50.15f, () => ByRole(AkhMorn), arrivalTime: 57.7f);
    }

    private PartyRole[] SwapFreeWaterToItsGroup()
    {
        var after = lineup.ToArray();
        var tether = Array.IndexOf(after, state.TetherWater);
        var free = Array.IndexOf(after, state.FreeWater) - 4;
        if ((tether < 2) == (free < 2))
            (after[4 + free], after[4 + 3 - free]) = (after[4 + 3 - free], after[4 + free]);
        return after;
    }

    private PartyRole[] JpLineup()
    {
        var chained = state.LineupOrder.Take(4).ToArray();
        var healer = chained.Single(r => r is PartyRole.RegenHealer or PartyRole.ShieldHealer);
        var tank = chained.Single(r => r.IsTank());
        var dps = chained.Where(r => r.IsDps()).OrderBy(r => r).ToArray();
        var free = Enum.GetValues<PartyRole>().Except(chained).ToArray();
        var freeDps = free.Where(r => r.IsDps()).OrderBy(r => r).ToArray();
        PartyRole[] order = [healer, tank, dps[1], dps[0], free.Single(r => r is PartyRole.RegenHealer or PartyRole.ShieldHealer),
            free.Single(r => r.IsTank()), freeDps[1], freeDps[0]];
        var neighbors = Enumerable.Range(0, 4).Select(state.ChainLink)
            .Where(link => link.Item1 == healer || link.Item2 == healer)
            .Select(link => link.Item1 == healer ? link.Item2 : link.Item1).ToArray();
        var opposite = chained.Single(r => r != healer && !neighbors.Contains(r));
        var index = Array.IndexOf(order, opposite);
        (order[1], order[index]) = (order[index], order[1]);
        return order;
    }

    private PartyRole ChooseSomberTank()
    {
        var player = world.Party.PlayerRole;
        if (playerBaitsSomberDance && player.IsTank()) return player;
        return world.Party.Get(PartyRole.MainTank) is { } mainTank && world.Party.IsBotDriven(mainTank) ? PartyRole.MainTank : PartyRole.OffTank;
    }

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP4)?.Position ?? Vector3.Zero;

    private Vector3 JustPastOracleFromCenter()
    {
        var oracle = OraclePosition();
        return oracle.LengthSquared() > 0.01f ? oracle + Vector3.Normalize(oracle) * 2f : oracle;
    }

    private void BaitSomberDanceAwayFromOracle()
    {
        var x = OraclePosition().X;
        baitEast = x < -4f * S || MathF.Abs(x) < 4f * S && state.EastWing;
    }

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private static Vector3 Quadrant(int index, float north, float east)
        => FromSim(index % 4 < 2 ? north : -north, index % 4 is 1 or 2 ? east : -east);

    private int Index(PartyRole role) => Array.IndexOf(swapped, role);
    private bool North(PartyRole role) => Index(role) % 4 < 2;

    private static Vector3? Middle(PartyRole role) => FromSim((int)role % 2 * 0.3f, (int)role * 0.08f);

    private Vector3 OpeningBossPosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.UsurperP4)?.Position
           ?? state.UsurperOpeningPosition;

    private Vector3? OpeningBait(PartyRole role)
        => OpeningBossPosition() + new Vector3((int)role % 2 == 0 ? -10f : 10f, 0, 0);

    private Vector3? OpeningSpread(PartyRole role)
        => OpeningBossPosition() + new Vector3((int)role % 2 == 0 ? -3f : 3f, 0, state.UsurperNorth ? 8f : -8f);

    private static Vector3? Lineup(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(4.5f, 5),
        PartyRole.OffTank => FromSim(0.2f, 11.3f),
        PartyRole.RegenHealer => FromSim(0.2f, -11.3f),
        PartyRole.ShieldHealer => FromSim(4.5f, -5),
        PartyRole.MeleeDpsA => FromSim(-25.5f, 5),
        PartyRole.MeleeDpsB => FromSim(-21.2f, 11.3f),
        PartyRole.PhysRangedDps => FromSim(-21.2f, -11.3f),
        _ => FromSim(-25.5f, -5),
    };

    private static Vector3? JpLineupSpot(PartyRole role) => role switch
    {
        PartyRole.RegenHealer => FromSim(10.1f, -16.4f),
        PartyRole.ShieldHealer => FromSim(16.4f, -10.1f),
        PartyRole.MainTank => FromSim(16.4f, 10.1f),
        PartyRole.OffTank => FromSim(10.1f, 16.4f),
        PartyRole.MeleeDpsA => FromSim(-10.1f, -16.4f),
        PartyRole.MeleeDpsB => FromSim(-16.4f, -10.1f),
        PartyRole.PhysRangedDps => FromSim(-16.4f, 10.1f),
        _ => FromSim(-10.1f, 16.4f),
    };

    private static Vector3? Bowtie(PartyRole role, PartyRole[] order)
    {
        var index = Array.IndexOf(order, role);
        return index < 4 ? Quadrant(index, 19, 5.2f) : Quadrant(index, 6.7f, 14.6f);
    }

    private Vector3? SpiritTakerSpread(PartyRole role)
    {
        var index = Index(role);
        if (index < 4) return Quadrant(index, 19, 17.2f);
        var west = index is 4 or 7;
        return FromSim(0, west ? ((int)role < 4 ? -38 : -19) : ((int)role < 4 ? 19 : 0));
    }

    private Vector3? WaterStack(PartyRole role)
        => FromSim(North(role) ? 17.3f : -17.3f, state.EastWing ? -15.5f : 15.5f) + Middle(role)!.Value * 0.3f;

    private Vector3? DanceParty(PartyRole role) => FromSim(North(role) ? 15 : -15, 0) + Middle(role)!.Value * 0.3f;

    private static Vector3? AkhMorn(PartyRole role) => role == PartyRole.MainTank ? new Vector3(-9.3f, 0, 0) : Vector3.Zero;

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
