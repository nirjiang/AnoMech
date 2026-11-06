using AnoMech.Scenarios;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using AnoMech.Scenarios.Fru.P2LightRampant;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using System.Numerics;

namespace AnoMech.Tests;

public class FruJpScenarioTests
{
    private static IEnumerable<TestCaseData> Strategies()
        => from scenario in ScenarioCatalog.Create().Scenarios
           where scenario.Phase.Zone is global::AnoMech.Scenarios.Fru.FruZone
           from entry in scenario.AiStrats.Select((ai, index) => (ai, index))
           where entry.ai.Group == "JP"
           select new TestCaseData(scenario.GetType(), entry.index)
               .SetName($"JP {scenario.DisplayName()} [{entry.ai.Name}]");

    [TestCaseSource(nameof(Strategies))]
    public void EverySeatClearsRandomizedMechanics(Type scenario, int strat)
    {
        for (var seed = 0; seed < 32; seed++)
        {
            var run = ScenarioRun.Execute(scenario, strat, seed, new ScenarioRunOptions { PlayerRole = (PartyRole)(seed % 8) });
            Assert.That(run.Passed, Is.True, run.ToString);
        }
    }

    [Test]
    public void DiamondDustStartsInAssignedClockSpots()
    {
        const float radius = 4.5f;
        var diagonal = radius / MathF.Sqrt(2f);
        Vector3[] expected =
        [
            new(0, 0, -radius), new(0, 0, radius),
            new(-radius, 0, 0), new(radius, 0, 0),
            new(-diagonal, 0, diagonal), new(diagonal, 0, diagonal),
            new(-diagonal, 0, -diagonal), new(diagonal, 0, -diagonal),
        ];
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustJpScenario), 0, 0, new ScenarioRunOptions
        {
            StopAt = 9.1f,
            Probe = p =>
            {
                if (!p.Crossed(9f)) return;
                foreach (var role in PerRole.All)
                    Assert.That(Vector3.Distance(p.Member(role)!.Position, expected[(int)role]), Is.LessThan(0.12f), role.ToString());
            },
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(run.Elapsed, Is.GreaterThanOrEqualTo(9f));
    }

    [Test]
    public void DiamondDustCoversEveryIcicleMirrorKickAndMarkerCombination()
    {
        for (var icicle = 0; icicle < 4; icicle++)
            for (var mirror = 0; mirror < 8; mirror++)
                foreach (var axe in new[] { false, true })
                    foreach (var supports in new[] { false, true })
                        foreach (var stillness in new[] { false, true })
                        {
                            var run = ScenarioRun.Execute(typeof(FruP2DiamondDustJpScenario), 0, icicle * 8 + mirror,
                                new ScenarioRunOptions { Overrides = o =>
                                {
                                    var roll = (FruP2DiamondDustStateOverrides)o;
                                    roll.FirstIcicle = icicle;
                                    roll.MirrorOctant = mirror;
                                    roll.Axe = axe;
                                    roll.SupportsMarked = supports;
                                    roll.Stillness = stillness;
                                }});
                            Assert.That(run.Passed, Is.True, run.ToString);
                        }
    }

    // First icicle defaults to east-west, so the MT group lands west and the ST group east.
    [TestCase(4, true, 1, 0f, 1, 180f, false, TestName = "DiamondDustRimRoute mirror south, both clockwise")]
    [TestCase(3, false, 1, 0f, -1, 0f, false, TestName = "DiamondDustRimRoute mirror southeast, ST group counterclockwise to meet north")]
    [TestCase(6, true, 1, 63f, 1, 243f, true, TestName = "DiamondDustRimRoute mirror west Stillness, long way round")]
    [TestCase(6, false, 1, 63f, 1, 243f, true, TestName = "DiamondDustRimRoute mirror west Silence, long way round")]
    [TestCase(7, true, 1, 135f, 1, 315f, false, 0, true, TestName = "DiamondDustRimRoute north-south mirror northwest, both clockwise 135")]
    public void DiamondDustRimRouteFollowsMirror(int mirror, bool stillness, int groupOneTurn, float groupOneEnd,
        int groupTwoTurn, float groupTwoEnd, bool groupTwoSlidesAcrossFirst, int firstIcicle = 2, bool walksWholeRoute = false)
    {
        PartyRole[] groupOne = [PartyRole.MainTank, PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.PhysRangedDps];
        PartyRole[] groupTwo = [PartyRole.OffTank, PartyRole.ShieldHealer, PartyRole.MeleeDpsB, PartyRole.CasterDps];
        var mirrorAt = FruP2DiamondDustState.Point(mirror, 10);
        var holyBearings = new List<(float One, float Two)>();
        var before = new Dictionary<PartyRole, Vector3>();
        var behindAtCleaves = new List<bool[]>();
        static float Bearing(Vector3 p) => (MathF.Atan2(p.X, -p.Z) * 180f / MathF.PI + 360f) % 360f;
        static float Turn(float from, float to) => ((to - from) % 360f + 540f) % 360f - 180f;
        bool Behind(Vector3 p) => Vector3.Dot(Vector3.Normalize(p - mirrorAt), Vector3.Normalize(mirrorAt)) > MathF.Sqrt(0.5f);
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustJpScenario), 0, mirror, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            Overrides = o =>
            {
                var roll = (FruP2DiamondDustStateOverrides)o;
                roll.FirstIcicle = firstIcicle;
                roll.MirrorOctant = mirror;
                roll.Stillness = stillness;
            },
            Probe = p =>
            {
                if (walksWholeRoute && p.Time is > 30.1f and < 40f)
                    foreach (var role in PerRole.All)
                        Assert.That(p.Member(role)!.HasStatus(SprintHandler.SprintStatusId), Is.False, $"{role} sprints at {p.Time:F2}");
                for (var wave = 0; wave < FruP2DiamondDustState.HolyWaves; wave++)
                    if (p.Crossed(FruP2DiamondDustState.HolyAt(wave)))
                        holyBearings.Add((Bearing(p.Member(PartyRole.RegenHealer)!.Position), Bearing(p.Member(PartyRole.ShieldHealer)!.Position)));
                if (p.Crossed(40f))
                    foreach (var role in PerRole.All)
                    {
                        var at = p.Member(role)!.Position;
                        before[role] = at;
                        var end = groupOne.Contains(role) ? groupOneEnd : groupTwoEnd;
                        Assert.That(MathF.Abs(Turn(Bearing(at), end)), Is.LessThan(6f), $"{role} ends at {Bearing(at):F0}°");
                    }
                if (p.Crossed(42f))
                    foreach (var role in groupTwo)
                    {
                        var at = p.Member(role)!.Position;
                        if (groupTwoSlidesAcrossFirst)
                            Assert.That(Vector3.Dot(at, mirrorAt), Is.LessThan(0f), $"{role} slid across before the first cleave");
                        else
                            Assert.That(Vector3.Distance(at, before[role]), Is.LessThan(0.5f), $"{role} held still");
                    }
                if (p.Crossed(46.2f) || p.Crossed(48.6f))
                    behindAtCleaves.Add(PerRole.All.Select(r => Behind(p.Member(r)!.Position)).ToArray());
            },
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(holyBearings, Has.Count.EqualTo(4));
        for (var wave = 1; wave < 4; wave++)
        {
            Assert.That(Turn(holyBearings[wave - 1].One, holyBearings[wave].One) * groupOneTurn, Is.GreaterThan(12f), $"MT group, Holy {wave + 1}");
            Assert.That(Turn(holyBearings[wave - 1].Two, holyBearings[wave].Two) * groupTwoTurn, Is.GreaterThan(12f), $"ST group, Holy {wave + 1}");
        }
        Assert.That(behindAtCleaves, Has.Count.EqualTo(2));
        Assert.That(behindAtCleaves[0], Is.All.EqualTo(stillness), "first cleave");
        Assert.That(behindAtCleaves[1], Is.All.EqualTo(!stillness), "second cleave");
    }

    [Test]
    public void DiamondDustRimRouteFirstIcicleSoutheastMirrorEastStopsTwoOctantsClockwise()
    {
        static float Bearing(Vector3 p) => (MathF.Atan2(p.X, -p.Z) * 180f / MathF.PI + 360f) % 360f;
        static float Turn(float from, float to) => ((to - from) % 360f + 540f) % 360f - 180f;
        PartyRole[] groupOne = [PartyRole.MainTank, PartyRole.RegenHealer, PartyRole.MeleeDpsA, PartyRole.PhysRangedDps];
        var holyBearings = new List<(float One, float Two)>();
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustJpScenario), 0, 0, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.MeleeDpsA,
            Overrides = o =>
            {
                var roll = (FruP2DiamondDustStateOverrides)o;
                roll.FirstIcicle = 3;
                roll.MirrorOctant = 2;
                roll.Axe = true;
                roll.SupportsMarked = true;
                roll.Stillness = true;
                roll.GazeOctant = 4;
            },
            Probe = p =>
            {
                for (var wave = 0; wave < FruP2DiamondDustState.HolyWaves; wave++)
                    if (p.Crossed(FruP2DiamondDustState.HolyAt(wave)))
                        holyBearings.Add((Bearing(p.Member(PartyRole.RegenHealer)!.Position), Bearing(p.Member(PartyRole.ShieldHealer)!.Position)));
                if (p.Crossed(40f))
                    foreach (var role in PerRole.All)
                    {
                        var at = Bearing(p.Member(role)!.Position);
                        Assert.That(MathF.Abs(Turn(at, groupOne.Contains(role) ? 45f : 225f)), Is.LessThan(6f), $"{role} ends at {at:F0}°");
                    }
            },
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(holyBearings, Has.Count.EqualTo(4));
        Assert.That(MathF.Abs(Turn(holyBearings[0].One, 315f)), Is.LessThan(6f), "MT group starts northwest");
        Assert.That(MathF.Abs(Turn(holyBearings[0].Two, 135f)), Is.LessThan(6f), "ST group starts southeast");
        for (var wave = 1; wave < 4; wave++)
        {
            Assert.That(Turn(holyBearings[wave - 1].One, holyBearings[wave].One), Is.GreaterThan(12f), $"MT group, Holy {wave + 1}");
            Assert.That(Turn(holyBearings[wave - 1].Two, holyBearings[wave].Two), Is.GreaterThan(12f), $"ST group, Holy {wave + 1}");
        }
    }

    // Mirror south, both groups clockwise; the gaze one octant into the MT group's route (7) or the ST group's (3).
    [TestCase(7, true, TestName = "DiamondDustHolyPace gaze on MT route, MT group steps shorter")]
    [TestCase(3, false, TestName = "DiamondDustHolyPace gaze on ST route, ST group steps shorter")]
    [TestCase(0, true, TestName = "DiamondDustHolyPace gaze north over the MT stop, ST stop opposite")]
    public void DiamondDustHolyPaceFollowsGaze(int gaze, bool groupOneNear)
    {
        static float Bearing(Vector3 p) => (MathF.Atan2(p.X, -p.Z) * 180f / MathF.PI + 360f) % 360f;
        static float Turn(float from, float to) => ((to - from) % 360f + 540f) % 360f - 180f;
        PartyRole[] healers = [PartyRole.RegenHealer, PartyRole.ShieldHealer];
        var puddles = healers.ToDictionary(h => h, _ => new List<Vector3>());
        var stops = new Dictionary<PartyRole, Vector3>();
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustJpScenario), 0, gaze, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            Overrides = o =>
            {
                var roll = (FruP2DiamondDustStateOverrides)o;
                roll.FirstIcicle = 2;
                roll.MirrorOctant = 4;
                roll.GazeOctant = gaze;
            },
            Probe = p =>
            {
                for (var wave = 0; wave < FruP2DiamondDustState.HolyWaves; wave++)
                    if (p.Crossed(FruP2DiamondDustState.HolyAt(wave) - global::AnoMech.Scenarios.Fru.FruHolyPuddles.SnapshotBeforeHit))
                        foreach (var h in healers) puddles[h].Add(p.Member(h)!.Position);
                if (p.Crossed(40f))
                    foreach (var h in healers) stops[h] = p.Member(h)!.Position;
            },
        });
        Assert.That(run.Passed, Is.True, run.ToString);
        float Pace(PartyRole h) => Turn(Bearing(puddles[h][0]), Bearing(puddles[h][3])) / 3;
        var (near, far) = groupOneNear ? (healers[0], healers[1]) : (healers[1], healers[0]);
        Assert.That(Pace(near), Is.GreaterThan(12f), "near group still clockwise");
        Assert.That(Pace(far) - Pace(near), Is.GreaterThan(3f), $"pace near {Pace(near):F1}° far {Pace(far):F1}°");
        static float ToSegment(Vector3 p, Vector3 a, Vector3 b)
            => Vector3.Distance(p, a + Math.Clamp(Vector3.Dot(p - a, b - a) / (b - a).LengthSquared(), 0, 1) * (b - a));
        if (gaze == 0)
        {
            Assert.That(MathF.Abs(Turn(Bearing(stops[healers[0]]), 0f)), Is.LessThan(6f), "MT group stops in front of the boss");
            Assert.That(MathF.Abs(Turn(Bearing(stops[healers[1]]), 180f)), Is.LessThan(6f), "ST group stops opposite");
        }
        foreach (var stop in healers)
        {
            var at = stops[stop];
            var approach = FruP2DiamondDustState.Point(Bearing(at) / 45f - 2f / (at.Length() * MathF.PI / 4), at.Length());
            foreach (var h in healers)
                for (var wave = 0; wave < FruP2DiamondDustState.HolyWaves; wave++)
                    Assert.That(ToSegment(puddles[h][wave], approach, at), Is.GreaterThan(6.5f), $"{h} puddle {wave + 1} vs {stop} stop approach");
        }
    }

    [Test]
    public void LightRampantCoversEveryBaitPairAndBothOrbOrders()
    {
        for (var first = 0; first < 8; first++)
            for (var second = first + 1; second < 8; second++)
                foreach (var northFirst in new[] { false, true })
                {
                    var run = ScenarioRun.Execute(typeof(FruP2LightRampantJpScenario), 0, first * 8 + second,
                        new ScenarioRunOptions { Overrides = o =>
                        {
                            var roll = (FruP2LightRampantStateOverrides)o;
                            foreach (var role in PerRole.All)
                                roll.Duty[role] = (int)role == first || (int)role == second ? LightRampantDuty.Bait : LightRampantDuty.Tether;
                            roll.NorthOrbsFirst = northFirst;
                        }});
                    Assert.That(run.Passed, Is.True, run.ToString);
                }
    }

    [Test]
    public void CrystallizeTimeCoversEveryCornerHourglassAndSpiritTakerTarget()
    {
        foreach (var corner in Enum.GetValues<CrystallizeCorner>())
            foreach (var slowNorthwest in new[] { false, true })
                foreach (var target in PerRole.All)
                {
                    var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeJpScenario), 0, (int)corner * 8 + (int)target,
                        new ScenarioRunOptions { Overrides = o =>
                        {
                            var roll = (FruP4CrystallizeTimeStateOverrides)o;
                            roll.Corner = corner;
                            roll.SlowNorthwest = slowNorthwest;
                            roll.SpiritTakerTarget = target;
                        }});
                    Assert.That(run.Passed, Is.True, run.ToString);
                }
    }

    [Test]
    public void ParadiseCoversEveryTowerAndWingOrder()
    {
        for (var rotation = 0; rotation < 3; rotation++)
            foreach (var eastSecond in new[] { false, true })
                foreach (var darkFirst in new[] { false, true })
                {
                    var run = ScenarioRun.Execute(typeof(FruP5ParadiseRegainedJpScenario), 0, rotation,
                        new ScenarioRunOptions { Overrides = o =>
                        {
                            var roll = (FruP5ParadiseRegainedStateOverrides)o;
                            roll.Rotation = rotation;
                            roll.EastSecond = eastSecond;
                            roll.DarkFirst = darkFirst;
                        }});
                    Assert.That(run.Passed, Is.True, run.ToString);
                }
    }

    [TestCase(KnockbackImmunity.ArmsLength)]
    [TestCase(KnockbackImmunity.Surecast)]
    [TestCase((ushort)0)]
    public void KnockbackImmunityPreservesYReturnForTheFirstWing(ushort status)
    {
        Vector3? before = null, after = null;
        var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeJpScenario), 0, 0, new ScenarioRunOptions
        {
            PlayerRole = PartyRole.CasterDps,
            StopAt = 55.7f,
            Probe = p =>
            {
                if (p.Crossed(53.8f))
                {
                    p.Member(PartyRole.MainTank)!.RemoveStatus(KnockbackImmunity.ArmsLength);
                    if (status != 0) p.Member(PartyRole.MainTank)!.AddStatus(status, 3f);
                }
                if (p.Crossed(55f)) before = p.Member(PartyRole.MainTank)!.Position;
                if (p.Crossed(55.6f)) after = p.Member(PartyRole.MainTank)!.Position;
            },
        });
        if (status == 0)
        {
            Assert.That(run.Deaths.Any(d => d.Role == PartyRole.MainTank) ||
                before.HasValue && after.HasValue && Vector3.Distance(before.Value, after.Value) > 5f, Is.True, run.ToString);
            return;
        }
        Assert.That(run.Passed, Is.True, run.ToString);
        Assert.That(before, Is.Not.Null);
        Assert.That(after, Is.Not.Null);
        Assert.That(Vector3.Distance(before!.Value, after!.Value), Is.LessThan(0.1f));
    }
}
