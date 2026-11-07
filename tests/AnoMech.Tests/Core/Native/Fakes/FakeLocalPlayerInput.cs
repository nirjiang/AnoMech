using System;
using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Tests;

// Nobody at the keyboard, unless a test holds a movement key.
internal sealed class FakeLocalPlayerInput : ILocalPlayerInput
{
    public event Action<ActionType, uint, ulong>? ActionExecuted { add { } remove { } }

    public bool MovementInputActive { get; set; }
    public bool IsJumping => false;
    public bool IsAutoAttacking => false;
    public bool PollActionUsed() => false;
    public bool PollCancelCast() => false;
    public IReadOnlyCollection<(uint ActionId, ActionType Type)> RecentActions => [];

    public System.Numerics.Vector2? SteerDirection { get; private set; }
    public float SteerSpeed { get; private set; }

    public void Steer(System.Numerics.Vector2? direction, float speed)
    {
        SteerDirection = direction is { } d && d.LengthSquared() > 0f ? System.Numerics.Vector2.Normalize(d) : null;
        SteerSpeed = speed;
    }

    // Records what the sim asked for. The lease's expiry and the walk it blocks live in the native
    // detour, which never runs here.
    public int MovementBlockRequests { get; private set; }
    public int MovementBlockReleases { get; private set; }
    public bool MovementBlocked { get; private set; }

    public void RefreshMovementBlock(bool blocked)
    {
        if (blocked) MovementBlockRequests++;
        else MovementBlockReleases++;
        MovementBlocked = blocked;
    }

    public Func<uint, uint?>? AdjustActionId { get; set; }
    public Func<ActionType, uint, int?>? AdjustCastTime { get; set; }
    public Func<ActionType, uint, bool, uint?>? AdjustActionStatus { get; set; }

    public void Dispose() { }
}
