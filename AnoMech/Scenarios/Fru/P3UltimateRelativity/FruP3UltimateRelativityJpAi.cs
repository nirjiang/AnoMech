using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P3UltimateRelativity.FruP3UltimateRelativityState;

namespace AnoMech.Scenarios.Fru.P3UltimateRelativity;

public sealed class FruP3UltimateRelativityJpAi : IScenarioAi<FruP3UltimateRelativityState>
{
    public string Name => "JP · Atobe modified / yellow Y north";
    public string Group => "JP";

    private const float S = 1f / 2.358f;

    private static readonly PartyRole[] DpsPriority = [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];
    private static readonly PartyRole[] SupportPriority = [PartyRole.RegenHealer, PartyRole.ShieldHealer, PartyRole.MainTank, PartyRole.OffTank];

    private FruP3UltimateRelativityState state = null!;

    public void Run(FruP3UltimateRelativityState stateParam, SimWorld world)
    {
        state = stateParam;
        var ai = new AiManager(world);
        ai.Move(1f, () => ByRole(CenterSpot));
        ai.Automarker(12f, RelativityMarkers);
        ai.Move(19.2f, () => ByRole(role => FireSpot(role, 0)), arrivalTime: FireAt(0));
        ai.Move(23.9f, () => ByRole(role => BaitSpot(role, 0)), arrivalTime: ReturnAt(earlyReturn: true));
        LeaveLaserNowOthersArriveBy(ai, 28.6f, 0, role => FireSpot(role, 1, intermediate: true), arrivalTime: 30.6f);
        ai.Move(30.6f, () => ByRole(role => FireSpot(role, 1)), jitter: 0f, arrivalTime: FireAt(1));
        ai.Move(34f, () => ByRole(role => BaitSpot(role, 1)), arrivalTime: ReturnAt(earlyReturn: false));
        LeaveLaserNowOthersArriveBy(ai, 38.7f, 1, role => FireSpot(role, 2), arrivalTime: FireAt(2));
        ai.Move(45f, () => ByRole(role => BaitSpot(role, 2)), arrivalTime: LaserHitAt(2, 0));
        LeaveLaserNowOthersArriveBy(ai, 49.1f, 2, CenterSpot, arrivalTime: 51f);
        world.Events.Add(51f, () => FaceOutwardForShadoweye(world));
        ai.Move(StunEndAt + 0.1f, () => ByRole(role => Direction(role) * 0.5f), arrivalTime: 59.2f);
    }

    private Dictionary<PartyRole, Sign> RelativityMarkers()
        => Enum.GetValues<PartyRole>().ToDictionary(role => role, role => Clock(role) switch
        {
            3 => Sign.Attack1,
            5 => Sign.Attack2,
            0 => Sign.Attack3,
            2 => Sign.Ignore1,
            6 => Sign.Ignore2,
            1 => Sign.Bind1,
            7 => Sign.Bind2,
            _ => Sign.Bind3,
        });

    private void LeaveLaserNowOthersArriveBy(AiManager ai, float time, int laserWave, Func<PartyRole, Vector3?> at, float arrivalTime)
    {
        ai.Move(time, () => ByRole(role => BaitedLaser(role, laserWave) ? at(role) : null));
        ai.Move(time, () => ByRole(role => BaitedLaser(role, laserWave) ? null : at(role)), arrivalTime: arrivalTime);
    }

    private bool BaitedLaser(PartyRole role, int wave) => Wave(Clock(role)) == wave;

    private void FaceOutwardForShadoweye(SimWorld world)
    {
        foreach (var role in Enum.GetValues<PartyRole>())
            if (world.Party.Get(role) is { } member && member.IsAlive() && world.Party.IsBotDriven(member))
            {
                member.StopMoving();
                member.Face(member.Position + Direction(role) * 100f);
            }
    }

    private int Clock(PartyRole role)
    {
        var debuff = state.Debuff(role);
        return debuff switch
        {
            RelativityDebuff.ShortDps => IsFirstOfPair(role, DpsPriority) ? 3 : 5,
            RelativityDebuff.ShortSupport => 0,
            RelativityDebuff.MediumDps => 6,
            RelativityDebuff.MediumSupport => 2,
            RelativityDebuff.LongSupport => IsFirstOfPair(role, SupportPriority) ? 1 : 7,
            _ => 4,
        };
    }

    private bool IsFirstOfPair(PartyRole role, PartyRole[] priority)
        => priority.First(r => state.Debuff(r) == state.Debuff(role)) == role;

    private Vector3 Direction(PartyRole role) => state.Direction(Clock(role));
    private Vector3 At(PartyRole role, float radius) => Direction(role) * radius;

    private Vector3 Bait(PartyRole role)
    {
        var direction = Direction(role);
        var tangent = new Vector3(-direction.Z, 0f, direction.X);
        return direction * (23f * S) + tangent * (state.Clockwise(Clock(role)) ? -1f : 1f) * (4.1f * S);
    }

    private Vector3? FireSpot(PartyRole role, int wave, bool intermediate = false)
    {
        var fireWave = FruP3UltimateRelativityState.FireWave(state.Debuff(role));
        if (role == state.IceHolder) return At(role, 3f * S);
        return At(role, fireWave == wave ? (intermediate ? 20.5f : 32.5f) * S : 3f * S);
    }

    private Vector3? BaitSpot(PartyRole role, int wave)
    {
        if (BaitedLaser(role, wave)) return Bait(role);
        if (wave == 0) return At(role, state.DarkEruption(role) ? 20.5f * S : 3f * S);
        if (wave == 1 && state.Debuff(role) is RelativityDebuff.MediumDps or RelativityDebuff.MediumSupport) return At(role, 10.25f * S);
        return At(role, 3f * S);
    }

    private Vector3? CenterSpot(PartyRole role) => At(role, 3f * S);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
