using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Combat;

internal static class DutyRestartPolicy
{
    // Reads Dead, not IsAlive: an actor gone from the world in a zone transit has not died.
    public static bool ShouldRestart(SessionMode mode, SimParty party) => mode switch
    {
        SessionMode.Local => party.Player is { Dead: true },
        SessionMode.Host => party.AllMembers().ToList() is { Count: > 0 } members && members.All(m => m.Dead),
        _ => false,
    };
}
