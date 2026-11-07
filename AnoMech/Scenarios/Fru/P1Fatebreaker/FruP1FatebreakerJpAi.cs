using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.P1Fatebreaker;

public sealed class FruP1FatebreakerJpAi : IScenarioAi<FruP1FatebreakerState>
{
    public string Name => "JP · Lily Doll (HTD / ST-D4 swap / fixed H1-H2-D4 towers; experimental)";
    public string Group => "JP";

    private const float RunSpeed = 6.5f;
    private const float ReachedCentre = 0.5f;
    private const float BaitRadius = 7.5f;
    private const float PairRadius = 8.2f;
    private const float MeleeUptimeRadius = 3f;
    private const float RangedUptimeRadius = 9f;
    private const float OuterClockRadius = 19f;
    private const float RaisedPairRadius = 11.5f;
    private const float SafeInnerRadius = 13f;
    private const float SafeFlank = 0.4f;
    private const float MainTankLean = 0.2f;
    private const float HeavensLaneZ = 11.5f;
    private const float BoundLaneZ = 5.4f;
    private const float BeforeFireLineX = 6f;
    private const float BeforeKnockbackX = 3.2f;
    private const float BoundStackX = 11.5f;
    private const float FaithFrontX = 5.5f;
    private const float FaithBackX = 7.5f;
    private const float FaithFireFlexX = 8f;
    private const float FaithFireFlexZ = 0.5f;
    private const float FaithLightningFlexZ = 2f;
    private const float FaithLineZ = 3f;
    private const float FaithLineSpacing = 1.5f;
    private const float TowerPrepX = 6f;
    private const float TowerKnockbackX = 2f;
    private const float TankSpotX = 14.2f;
    private const float TankSpotZ = 5.5f;

    private static readonly PartyRole[] FaithPriority =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];

    private readonly FruP1Drill? drill;
    private readonly Dictionary<SimCharacter, Vector3> returningThroughCentre = [];
    private FruP1FatebreakerState state = null!;

    public FruP1FatebreakerJpAi() : this(null)
    {
    }

    internal FruP1FatebreakerJpAi(FruP1Drill? drill) => this.drill = drill;

    public void Run(FruP1FatebreakerState stateParam, SimWorld world)
    {
        state = stateParam;
        returningThroughCentre.Clear();
        var ai = new AiManager(world);

        if (drill is { } practiced)
        {
            MoveForDrill(ai, world, practiced);
            return;
        }

        MoveThroughBossCyclonicBreak(ai);

        MoveThroughUtopianSky(ai, world);

        MoveThroughImageCyclonicBreak(ai);

        MoveThroughTheHeavens(ai, world);

        MoveThroughFallOfFaith(ai);

        MoveThroughExplosion(ai);
    }

    private void MoveForDrill(AiManager ai, SimWorld world, FruP1Drill practiced)
    {
        switch (practiced)
        {
            case FruP1Drill.CyclonicBreakBoss:
                MoveThroughBossCyclonicBreak(ai);
                break;
            case FruP1Drill.UtopianSky:
                ai.Move(0f, () => ByRole(Uptime), jitter: 0f);
                MoveThroughUtopianSky(ai, world);
                MoveThroughImageCyclonicBreak(ai);
                break;
            case FruP1Drill.TurnOfTheHeavens:
                ai.Move(0f, () => ByRole(role => NearestLaneClearOfTheLightning(world, role)), jitter: 0f);
                MoveThroughTheHeavens(ai, world);
                break;
            case FruP1Drill.FallOfFaith:
                ai.Move(0f, () => ByRole(Uptime), jitter: 0f);
                MoveThroughFallOfFaith(ai);
                break;
            case FruP1Drill.Explosion:
                ai.Move(0f, () => ByRole(Uptime), jitter: 0f);
                MoveThroughExplosion(ai);
                break;
        }
    }

    private void MoveThroughBossCyclonicBreak(AiManager ai)
    {
        ai.Move(2f, () => ByRole(CyclonicBait), jitter: 0f);
        MoveThroughCyclonicWaves(ai, 14.35f, () => state.FirstCyclonicPairs);
        ai.Move(21f, () => ByRole(Uptime), jitter: 0f);
    }

    private void MoveThroughUtopianSky(AiManager ai, SimWorld world)
    {
        world.Events.Add(30.8f, () => StartReturningThroughCentre(world));
        ai.Move(40.26f, () => ByRole(RaisedPairStepsIn), jitter: 0f);
        ai.Move(42.76f, () => ByRole(UtopianSafeSpot), jitter: 0f);
    }

    private void MoveThroughImageCyclonicBreak(AiManager ai)
    {
        ai.Move(50.13f, () => ByRole(CyclonicBait), jitter: 0f);
        MoveThroughCyclonicWaves(ai, 56.21f, () => state.ImageCyclonicPairs);
    }

    private void MoveThroughTheHeavens(AiManager ai, SimWorld world)
    {
        ai.Move(62.43f, () => ByRole(role => NearestLaneClearOfTheLightning(world, role)), jitter: 0f);
        ai.Move(66.47f, () => ByRole(role => InBoundLane(role, BeforeFireLineX)), jitter: 0f);
        ai.Move(70.65f, () => ByRole(role => InBoundLane(role, BeforeKnockbackX)), jitter: 0f);
        ai.Move(74.76f, () => ByRole(role => InBoundLane(role, BoundStackX)), jitter: 0f);
        ai.Move(79.74f, () => ByRole(Uptime), jitter: 0f);
    }

    private void MoveThroughFallOfFaith(AiManager ai)
    {
        ai.Move(81.26f, () => ByRole(FaithLineSpot), jitter: 0f);
        ai.Move(99.18f, () => ByRole(role => FaithSpotInRound(role, 0) ?? FaithSpotInRound(role, 1)), jitter: 0f);
        ai.Move(106.78f, () => ByRole(role => FaithSpotInRound(role, 2)), jitter: 0f);
        ai.Move(109.88f, () => ByRole(role => FaithSpotInRound(role, 3)), jitter: 0f);
        ai.Move(114.89f, () => ByRole(Uptime), jitter: 0f);
    }

    private void MoveThroughExplosion(AiManager ai)
    {
        ai.Move(135.59f, () => ByRole(ExplosionPrepSpot), jitter: 0f);
        ai.Move(141.74f, () => ByRole(ExplosionKnockbackSpot), jitter: 0f);
        ai.Move(144.29f, () => ByRole(ExplosionFinalSpot), jitter: 0f);
        ai.Move(146.59f, () => ByRole(Uptime), jitter: 0f);
    }

    public void Tick(FruP1FatebreakerState stateParam, SimWorld world, float delta)
    {
        TakeTheBossOffAMarkedTank(stateParam, world);
        foreach (var (member, outerSpot) in returningThroughCentre.ToList())
        {
            if (!member.IsAlive())
            {
                returningThroughCentre.Remove(member);
                continue;
            }
            if (FlatDistance(member.Position, Vector3.Zero) > ReachedCentre) continue;
            returningThroughCentre.Remove(member);
            member.MoveTo(outerSpot, speed: RunSpeed);
        }
    }

    private static void TakeTheBossOffAMarkedTank(FruP1FatebreakerState run, SimWorld world)
    {
        if (run.Targeting is not { Top: { } top } targeting || !top.HasStatus(FruConstants.StatusId.PowderMarkTrail)) return;
        var coTank = world.Party.Get(ReferenceEquals(top, world.Party.Get(PartyRole.MainTank)) ? PartyRole.OffTank : PartyRole.MainTank);
        if (coTank is not null && coTank.IsAlive() && world.Party.IsBotDriven(coTank)) targeting.TryProvoke(coTank);
    }

    private void StartReturningThroughCentre(SimWorld world)
    {
        foreach (PartyRole role in Enum.GetValues<PartyRole>())
        {
            if (world.Party.Get(role) is not { } member || !member.IsAlive() || !world.Party.IsBotDriven(member)) continue;
            returningThroughCentre[member] = UtopianOuterSpot(role);
            member.MoveTo(Vector3.Zero, speed: RunSpeed);
        }
    }

    internal static Vector3 UtopianOuterSpot(PartyRole role) => AtClock(UtopianClock(role), OuterClockRadius);

    private static float UtopianClock(PartyRole role) => role switch
    {
        PartyRole.MainTank => MainTankLean,
        PartyRole.OffTank => 1f,
        PartyRole.ShieldHealer => 2f,
        PartyRole.MeleeDpsB => 3f,
        PartyRole.CasterDps => 4f,
        PartyRole.MeleeDpsA => 5f,
        PartyRole.RegenHealer => 6f,
        _ => 7f,
    };

    private Vector3? RaisedPairStepsIn(PartyRole role)
    {
        var clock = UtopianClock(role);
        var nearestSpot = (int)MathF.Round(clock) % 8;
        return state.BlastingImages.Any(spot => spot % 4 == nearestSpot % 4) ? AtClock(clock, RaisedPairRadius) : null;
    }

    private Vector3 UtopianSafeSpot(PartyRole role)
    {
        var axis = state.SafeAxis;
        float end = IsWesternGroup(role) ? (axis == 0 ? 0 : axis + 4) : (axis == 0 ? 4 : axis);
        if (state.UtopianStack) return AtClock(end, SafeInnerRadius);
        return role switch
        {
            PartyRole.RegenHealer or PartyRole.ShieldHealer => AtClock(end, SafeInnerRadius),
            PartyRole.MainTank or PartyRole.OffTank => AtClock(end, OuterClockRadius),
            PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => AtClock(end + SafeFlank, OuterClockRadius),
            _ => AtClock(end - SafeFlank, OuterClockRadius),
        };
    }

    private static bool IsWesternGroup(PartyRole role)
        => role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;

    private void MoveThroughCyclonicWaves(AiManager ai, float start, Func<bool> pairs)
    {
        ai.Move(start, () => ByRole(role => CyclonicGap(role, pairs())), jitter: 0f);
        ai.Move(start + 2.15f, () => ByRole(role => CyclonicBaitAngle(role, pairs())), jitter: 0f);
        ai.Move(start + 4.25f, () => ByRole(role => CyclonicGap(role, pairs())), jitter: 0f);
    }

    private static Vector3 CyclonicBait(PartyRole role) => AtClock(BaitClock(role), BaitRadius);

    private static float BaitClock(PartyRole role) => role switch
    {
        PartyRole.MainTank => 0f,
        PartyRole.CasterDps => 1f,
        PartyRole.ShieldHealer => 2f,
        PartyRole.MeleeDpsB => 3f,
        PartyRole.OffTank => 4f,
        PartyRole.MeleeDpsA => 5f,
        PartyRole.RegenHealer => 6f,
        _ => 7f,
    };

    private static Vector3 CyclonicGap(PartyRole role, bool pairs)
    {
        if (!pairs) return AtClock(BaitClock(role) + 0.5f, SpreadRadius(role));
        var turn = IsTankOrHealer(role) ? -0.5f : 0.5f;
        return AtClock(BaitClock(role) + turn, PairRadius);
    }

    private static Vector3 CyclonicBaitAngle(PartyRole role, bool pairs)
        => AtClock(BaitClock(role), pairs ? PairRadius : SpreadRadius(role));

    private static float SpreadRadius(PartyRole role) => role switch
    {
        PartyRole.MainTank or PartyRole.MeleeDpsA or PartyRole.MeleeDpsB => 6.6f,
        PartyRole.OffTank => 10.5f,
        _ => 13f,
    };

    private static Vector3 Uptime(PartyRole role)
    {
        var melee = role is PartyRole.MainTank or PartyRole.OffTank or PartyRole.MeleeDpsA or PartyRole.MeleeDpsB;
        return AtClock(BaitClock(role), melee ? MeleeUptimeRadius : RangedUptimeRadius);
    }

    private static bool IsTankOrHealer(PartyRole role) => !role.IsDps();

    private static Vector3? NearestLaneClearOfTheLightning(SimWorld world, PartyRole role)
        => world.Party.Get(role) is { } member
            ? new Vector3(member.Position.X, 0f, member.Position.Z < 0f ? -HeavensLaneZ : HeavensLaneZ)
            : null;

    private Vector3 InBoundLane(PartyRole role, float x)
        => new(state.SafeSideX * x, 0f, IsInNorthBoundLane(role) ? -BoundLaneZ : BoundLaneZ);

    private bool IsInNorthBoundLane(PartyRole role)
    {
        var targets = state.BoundTargets;
        var lower = targets.MaxBy(target => Array.IndexOf(FaithPriority, target));
        if (targets.All(IsTankOrHealer))
        {
            if (role == lower) return false;
            if (role == PartyRole.MeleeDpsA) return true;
        }
        else if (!targets.Any(IsTankOrHealer))
        {
            if (role == lower) return true;
            if (role == PartyRole.MainTank) return false;
        }
        return IsTankOrHealer(role);
    }

    private static Vector3 FaithLineSpot(PartyRole role)
        => new((Array.IndexOf(FaithPriority, role) - (FaithPriority.Length - 1) / 2f) * FaithLineSpacing, 0f, FaithLineZ);

    private Vector3? FaithSpotInRound(PartyRole role, int round)
    {
        var inWest = round % 2 == 0;
        if (IsInWesternFaithGroup(role) != inWest) return null;
        var side = inWest ? -1f : 1f;
        if (role == state.FaithTargets[round]) return new Vector3(side * FaithFrontX, 0f, 0f);
        if (role == state.FaithTargets[(round + 2) % state.FaithTargets.Count]) return new Vector3(side * FaithBackX, 0f, 0f);
        var northern = FaithFlex().Where(flex => IsInWesternFaithGroup(flex) == inWest).First() == role;
        var offset = northern ? -1f : 1f;
        return state.FaithFire[round]
            ? new Vector3(side * FaithFireFlexX, 0f, offset * FaithFireFlexZ)
            : new Vector3(side * FaithFrontX, 0f, offset * FaithLightningFlexZ);
    }

    private bool IsInWesternFaithGroup(PartyRole role)
    {
        var tether = state.FaithTargets.ToList().IndexOf(role);
        if (tether >= 0) return tether % 2 == 0;
        return FaithFlex().ToList().IndexOf(role) < 2;
    }

    private IEnumerable<PartyRole> FaithFlex() => FaithPriority.Where(role => !state.FaithTargets.Contains(role));

    private Vector3 ExplosionPrepSpot(PartyRole role)
    {
        if (role is PartyRole.MainTank or PartyRole.OffTank)
            return state.ExplosionFire ? TankLane(role, TowerPrepX) : TankLane(role, TankSpotX);
        var tower = state.TowerPosition(TowerIndexOf(role));
        return state.ExplosionFire ? new Vector3(TowerSide * TowerPrepX, 0f, tower.Z) : tower;
    }

    private Vector3? ExplosionKnockbackSpot(PartyRole role)
    {
        if (!state.ExplosionFire) return null;
        if (role is PartyRole.MainTank or PartyRole.OffTank) return TankLane(role, TowerKnockbackX);
        return new Vector3(TowerSide * TowerKnockbackX, 0f, state.TowerPosition(TowerIndexOf(role)).Z);
    }

    private Vector3 ExplosionFinalSpot(PartyRole role)
        => role is PartyRole.MainTank or PartyRole.OffTank ? TankLane(role, TankSpotX) : state.TowerPosition(TowerIndexOf(role));

    private float TowerSide => state.TowersEast ? 1f : -1f;

    private Vector3 TankLane(PartyRole role, float x)
        => new(-TowerSide * x, 0f, role == PartyRole.MainTank ? -TankSpotZ : TankSpotZ);

    private int TowerIndexOf(PartyRole role)
    {
        switch (role)
        {
            case PartyRole.RegenHealer: return 0;
            case PartyRole.ShieldHealer: return 1;
            case PartyRole.CasterDps: return 2;
        }
        var flexOrder = role switch
        {
            PartyRole.MeleeDpsA => 0,
            PartyRole.MeleeDpsB => 1,
            _ => 2,
        };
        for (var tower = 0; tower < state.TowerSoakers.Count; tower++)
        {
            var open = state.TowerSoakers[tower] - 1;
            if (flexOrder < open) return tower;
            flexOrder -= open;
        }
        return state.TowerSoakers.Count - 1;
    }

    private static Vector3 AtClock(float clock, float radius)
    {
        var bearing = MathF.PI - clock * MathF.PI / 4f;
        return new Vector3(MathF.Sin(bearing), 0f, MathF.Cos(bearing)) * radius;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    private static IAiMove ByRole(Func<PartyRole, Vector3?> spot)
        => AiMove.Create(Enumerable.Range(0, 8).Select(slot => spot((PartyRole)slot) is { } at ? new Vector2(at.X, at.Z) : (Vector2?)null).ToArray()).NaturalOrder();

    private static IAiMove ByRole(Func<PartyRole, Vector3> spot) => ByRole(role => (Vector3?)spot(role));
}
