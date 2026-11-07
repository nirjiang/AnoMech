using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.P5CombatPreview;

public sealed class FruP5CombatPreviewAi : IScenarioAi
{
    public string Name => "JP · Nukemaru mechanics + prototype actions (experimental)";
    public string Group => "JP";

    private const float UptimeRadius = 2f;

    public static void CloseOnBossAfterMechanics(SimWorld world, float time)
        => new AiManager(world).Move(time, RingAroundBoss);

    private static IAiMove RingAroundBoss()
        => AiMove.Create(Enumerable.Range(0, 8).Select(seat => (Vector2?)SpotOnRing(seat)).ToArray()).NaturalOrder();

    private static Vector2 SpotOnRing(int seat)
    {
        var angle = seat * MathF.Tau / 8f;
        return new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * UptimeRadius;
    }
}
