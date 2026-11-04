using System.Linq;
using AnoMech.Core.Game;

namespace AnoMech.Core.EnemyActions;

public static class ResistibleKnockback
{
    public static IEnemyActionEffect Create(uint knockbackId, float? delay = null)
        => new FilteredEffect([EnemyActionEffects.Knockback(knockbackId, delay)],
            (_, hits) => hits.Where(target => !KnockbackImmunity.Active(target)));
}
