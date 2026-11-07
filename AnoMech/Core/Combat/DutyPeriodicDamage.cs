using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.Combat;

internal readonly record struct DutyDot(ushort StatusId, uint AmountPerTick, DamageKind Kind);

// Damage over time for a run that opted in by creating one: its owner ticks it and drops it with the
// run. Each pulse only moves the ledger and the native bars, with no ActionEffect.
// UNVERIFIED: the 3s default and the moment mitigation is read are prototype policy, and pulses count
// from this helper's creation rather than from any server tick.
internal sealed class DutyPeriodicDamage(DutyCombat combat, SimParty party, IReadOnlyList<DutyDot> dots, float interval = 3f)
{
    // Far past any real frame, so a corrupt delta can't hang the framework thread.
    private const int MaxPulsesPerTick = 1_000;

    private readonly float interval = interval > 0f && float.IsFinite(interval) ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
    private float elapsed;

    // Every pulse a long frame covered lands, so damage doesn't depend on the frame rate. Each reads
    // the statuses held now: one that expired during the frame isn't reconstructed.
    public void Tick(float deltaSeconds)
    {
        if (combat.ActionsOver || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0f) return;
        elapsed += deltaSeconds;
        for (var pulses = 0; elapsed >= interval && pulses < MaxPulsesPerTick; pulses++)
        {
            elapsed -= interval;
            foreach (var member in party.ActiveMembers().ToList())
                foreach (var dot in dots)
                    Pulse(member, dot);
            if (combat.ActionsOver) return;
        }
        elapsed %= interval;
    }

    private void Pulse(SimCharacter member, DutyDot dot)
    {
        // A pulse's own death can end the run partway through the party.
        if (combat.ActionsOver || !member.IsAlive()) return;
        var held = member.ActiveStatusSnapshot.Select(status => status.StatusId).ToList();
        if (!held.Contains(dot.StatusId)) return;
        var taken = Mitigation.TakenFraction(held, dot.Kind);
        combat.Commit(member, (uint)MathF.Round(dot.AmountPerTick * taken));
        combat.DieOfStatusIfAtZero(member, dot.StatusId);
    }
}
