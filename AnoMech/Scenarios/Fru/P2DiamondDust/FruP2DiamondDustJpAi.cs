using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.P2DiamondDust.FruP2DiamondDustState;

namespace AnoMech.Scenarios.Fru.P2DiamondDust;

public sealed class FruP2DiamondDustJpAi : IScenarioAi<FruP2DiamondDustState>
{
    public string Name => "JP · Lily Doll / clockwise / colour groups";
    public string Group => "JP";

    private static readonly int[] Clocks = [0, 4, 6, 2, 5, 3, 7, 1];
    private static readonly int[] GroupOneOctants = [0, 1, 6, 7];
    private const float SlideDistance = 32f;
    private const float SlideSeconds = 1f;
    private const float PuddleRadius = 6f;
    private const float NeedleCircleRadius = 5f;
    private const float NeedleLineHalfWidth = 2.5f;
    private const float EdgeRadius = 19f;
    private const float EdgeSearchOctants = 25f / 45f;

    private FruP2DiamondDustState state = null!;
    private SimWorld world = null!;
    private readonly List<(int Wave, Vector3 Position)> puddles = [];

    public void Run(FruP2DiamondDustState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        puddles.Clear();
        var ai = new AiManager(world);
        ai.Move(7.1f, () => ByRole(role => Point(Clocks[(int)role], 4.5f)), jitter: 0f);
        ai.Move(15.3f, () => ByRole(KickSpot), arrivalTime: 20.6f);
        ai.Move(20.7f, () => ByRole(StoneSpot), arrivalTime: 23.3f);
        ai.Move(23.35f, () => ByRole(KnockbackSpot), jitter: 0f, arrivalTime: 27.4f);
        ai.Move(29.05f, () => ByRole(NeedleSafeEdgeSpot), jitter: 0f, arrivalTime: 30.05f);
        ai.Move(30.1f, () => ByRole(role => HolySpot(role, 0)), arrivalTime: 31.4f);
        for (var wave = 0; wave < HolyWaves; wave++)
        {
            var w = wave;
            world.Events.Add(HolyAt(w) - FruHolyPuddles.SnapshotBeforeHit, () => RememberHolyPuddles(w));
            ai.Move(HolyAt(w), () => ByRole(role => HolySpot(role, w + 1)), arrivalTime: HolyAt(w + 1));
        }
        ai.Move(HolyAt(HolyWaves), () => ByRole(IceSpot), arrivalTime: 39.9f);
        world.Events.Add(39.9f, FaceAwayFromGaze);
        world.Events.Add(IceStart + 0.2f, SlideAcrossWhereMirrorBackIsOutOfReach);
        world.Events.Add(FirstSlideAt, () => SlideBots(behind: state.Stillness));
        world.Events.Add(SecondSlideAt, () => SlideBots(behind: !state.Stillness));
        world.Events.Add(IceEnd, puddles.Clear);
        ai.Move(IceEnd + 0.3f, () => ByRole(_ => new Vector3(0, 0, 4)));
    }

    private const float FirstSlideAt = 45.05f;
    private const float SecondSlideAt = 47.4f;
    private const float MaxHolyStepOctants = 0.6f;
    private const float WalkingHolyStepOctants = 0.56f;
    private const float MinHolyStepOctants = 0.35f;
    private const float MaxWalkAfterLastHolyOctants = 1.2f;
    private const float StopApproachOctants = 2f / (17 * MathF.PI / 4);
    private const float StopApproachClearance = PuddleRadius + 1f;
    private const float PuddleClearanceOctants = 0.6f;

    private static bool GroupOne(PartyRole role) => ((int)role & 1) == 0;

    private int Clock(PartyRole role)
    {
        var original = Clocks[(int)role];
        var requiredParity = (state.FirstIcicle + (state.Marked(role) ? 1 : 0)) & 1;
        return (original + ((original & 1) == requiredParity ? 0 : 1)) % 8;
    }

    private Vector3? KickSpot(PartyRole role) => Point(Clock(role), (state.Axe ? 16 : 0) + (state.Marked(role) ? 3 : 1));

    private Vector3? StoneSpot(PartyRole role)
        => Point(Clock(role), state.Marked(role) ? state.Axe ? 19 : 8 : state.Axe ? 4 : 1);

    private int KnockbackOctant(PartyRole role)
    {
        var groupOne = GroupOneOctants[state.FirstIcicle];
        return (groupOne + (GroupOne(role) ? 0 : 4)) % 8;
    }

    private Vector3? KnockbackSpot(PartyRole role) => Point(NeedleSafeEdgeOctant(role), 3);

    private Vector3? NeedleSafeEdgeSpot(PartyRole role) => Point(NeedleSafeEdgeOctant(role), EdgeRadius);

    private float NeedleSafeEdgeOctant(PartyRole role)
    {
        var holyOctant = KnockbackOctant(role);
        var stars = Enum.GetValues<PartyRole>().Where(state.Marked).Select(r => StoneSpot(r)!.Value).ToList();
        return Enumerable.Range(-50, 101)
            .Select(step => holyOctant + step / 50f * EdgeSearchOctants)
            .OrderByDescending(octant => NeedleClearance(Point(octant, EdgeRadius), stars))
            .ThenBy(octant => MathF.Abs(octant - holyOctant))
            .First();
    }

    private static float NeedleClearance(Vector3 spot, List<Vector3> stars)
    {
        var clearance = float.PositiveInfinity;
        foreach (var star in stars)
        {
            var offset = spot - star;
            clearance = MathF.Min(clearance, offset.Length() - NeedleCircleRadius);
            for (var line = 0; line < 4; line++)
            {
                var along = Point(line, 1);
                var across = MathF.Abs(offset.X * along.Z - offset.Z * along.X);
                clearance = MathF.Min(clearance, across - NeedleLineHalfWidth);
            }
        }
        return clearance;
    }

    private int MirrorOctantsClockwise(PartyRole role) => (state.MirrorOctant - KnockbackOctant(role) + 8) % 8;

    private int RotationAroundRim(PartyRole role) => MirrorOctantsClockwise(role) == 1 ? -1 : 1;

    private float RimTravelOctants(PartyRole role) => (MirrorOctantsClockwise(role) % 4) switch
    {
        0 => 4 - PuddleClearanceOctants,
        1 => 2,
        3 => state.FirstIcicle == 3 && state.MirrorOctant == 2 ? 2 : 3,
        var octants => octants,
    };

    private int GazeOctantsAlongRoute(PartyRole role)
        => ((state.GazeOctant - KnockbackOctant(role)) * RotationAroundRim(role) + 16) % 8;

    private float HolyStepOctants(PartyRole role, float rimTravel)
    {
        var full = MathF.Min(rimTravel == 3 ? WalkingHolyStepOctants : MaxHolyStepOctants, (rimTravel - PuddleClearanceOctants) / 3);
        var gaze = GazeOctantsAlongRoute(role);
        var gazeOnRoute = gaze >= PuddleClearanceOctants && gaze <= rimTravel;
        var shortest = MathF.Min(full, MathF.Max(MinHolyStepOctants, (rimTravel - MaxWalkAfterLastHolyOctants) / 3));
        var longest = gazeOnRoute ? MathF.Max(shortest, full * 0.8f) : full;
        return Enumerable.Range(0, 11).Select(i => longest - i / 10f * (longest - shortest)).Distinct()
            .OrderByDescending(step => gazeOnRoute ? Enumerable.Range(1, HolyWaves - 1).Min(wave => MathF.Abs(wave * step - gaze)) : 0)
            .ThenByDescending(step => step)
            .First(step => KeepsStopApproachesClear(role, step));
    }

    private bool KeepsStopApproachesClear(PartyRole role, float step)
        => new[] { PartyRole.MainTank, PartyRole.OffTank }.All(other =>
        {
            var stop = IceSpot(other)!.Value;
            var approach = Point(KnockbackOctant(other) + RotationAroundRim(other) * (RimTravelOctants(other) - StopApproachOctants), 17);
            return Enumerable.Range(0, HolyWaves).All(wave =>
                DistanceToSegment(Point(KnockbackOctant(role) + RotationAroundRim(role) * wave * step, 18), approach, stop) >= StopApproachClearance);
        });

    private Vector3 HolySpot(PartyRole role, int step)
    {
        var rimTravel = RimTravelOctants(role);
        var holyStep = HolyStepOctants(role, rimTravel);
        var along = step < HolyWaves
            ? step * holyStep
            : MathF.Min(rimTravel, 3 * holyStep + (rimTravel == 3 ? holyStep : PuddleClearanceOctants));
        return Point(KnockbackOctant(role) + RotationAroundRim(role) * along, 18);
    }

    private Vector3? IceSpot(PartyRole role)
        => Point(KnockbackOctant(role) + RotationAroundRim(role) * RimTravelOctants(role), 17);

    private void RememberHolyPuddles(int wave)
    {
        foreach (var healer in new[] { PartyRole.RegenHealer, PartyRole.ShieldHealer })
            if (world.Party.Get(healer) is { } member && member.IsAlive()) puddles.Add((wave, member.Position));
    }

    private IEnumerable<Vector3> LivePuddles(float at)
        => puddles.Where(p => PuddleFadesAt(p.Wave) > at).Select(p => p.Position);

    private IEnumerable<SimCharacter> Bots()
        => world.Party.ActiveMembers().Where(world.Party.IsBotDriven).ToList();

    private void FaceAwayFromGaze()
    {
        foreach (var bot in Bots())
        {
            bot.StopMoving();
            bot.Face(2 * bot.Position - state.GazePosition);
        }
    }

    private void SlideBots(bool behind)
    {
        foreach (var bot in Bots())
            if (BehindMirror(bot.Position) != behind && SlideDestination(bot.Position, behind, world.Events.Elapsed) is { } destination)
                bot.IceSlideTo(destination, SlideSeconds);
    }

    private void SlideAcrossWhereMirrorBackIsOutOfReach()
    {
        var backSlideAt = state.Stillness ? FirstSlideAt : SecondSlideAt;
        foreach (var bot in Bots())
            if (!BehindMirror(bot.Position) && SlideDestination(bot.Position, true, backSlideAt) == null
                && SlideDestination(bot.Position, false, world.Events.Elapsed, backSlideAt) is { } destination)
                bot.IceSlideTo(destination, SlideSeconds);
    }

    private bool BehindMirror(Vector3 position)
    {
        var offset = position - state.MirrorPosition;
        return offset.LengthSquared() > 0.001f
            && Vector3.Dot(Vector3.Normalize(offset), Vector3.Normalize(state.MirrorPosition)) > MathF.Sqrt(0.5f);
    }

    private Vector3? SlideDestination(Vector3 from, bool behind, float at, float? thenBehindAt = null)
    {
        Vector3? best = null;
        var bestMargin = float.NegativeInfinity;
        var live = LivePuddles(at).ToList();
        for (var degree = 0; degree < 720; degree++)
        {
            var end = from + Point(degree / 90f, SlideDistance);
            if (end.Length() > 19 || BehindMirror(end) != behind) continue;
            var clearance = live.Select(p => DistanceToSegment(p, from, end) - PuddleRadius).DefaultIfEmpty(20).Min();
            if (clearance < 0.35f) continue;
            var relative = end - state.MirrorPosition;
            var backDot = Vector3.Dot(Vector3.Normalize(relative), Vector3.Normalize(state.MirrorPosition));
            var coneMargin = (behind ? backDot - MathF.Sqrt(0.5f) : MathF.Sqrt(0.5f) - backDot) * relative.Length();
            var margin = MathF.Min(MathF.Min(20 - end.Length(), clearance), coneMargin);
            if (margin > bestMargin && (thenBehindAt is not { } next || SlideDestination(end, true, next) != null))
            {
                bestMargin = margin;
                best = end;
            }
        }
        return best;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var line = to - from;
        var t = line.LengthSquared() > 0 ? Math.Clamp(Vector3.Dot(point - from, line) / line.LengthSquared(), 0, 1) : 0;
        return Vector3.Distance(point, from + t * line);
    }

    private static Vector3 Point(float octant, float radius) => FruP2DiamondDustState.Point(octant, radius);

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
