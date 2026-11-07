using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;

namespace AnoMech.Core.Combat;

// A priority list per role, through the same validator, costs and effects a player's press takes. A
// strat's movement always wins: a bot starts no hard cast on the move, and moving loses the cast.
internal sealed class DutyBotRotation(DutyCombat combat, SimParty party)
{
    private const float MitigateBelow = 0.85f;
    private const float PartyHealBelow = 0.8f;
    private const float SingleHealBelow = 0.75f;
    private const int PartyHealCount = 3;

    public void Tick()
    {
        var members = party.AllMembers().ToList();
        foreach (var member in members)
        {
            if (combat.ActionsOver) return;
            if (!IsDrivenHere(member) || !member.IsAlive() || combat.IsCasting(member)) continue;
            if (DutyCombat.JobOf(member) is not { } job || !DutyActions.Supports(job)) continue;
            Act(member, job, members);
        }
    }

    // A remote seat's bot presses on its own client.
    private bool IsDrivenHere(SimCharacter member)
        => member is SimPartyNpc or SimPlayer && party.IsBotDriven(member);

    private void Act(SimCharacter member, JobId job, IReadOnlyList<SimCharacter> members)
    {
        var kit = DutyActions.Of(job).ToList();
        UseMitigationIfHurt(member, kit);
        if (TryHeal(member, kit, members)) return;
        if (kit.FirstOrDefault(a => a.Kind == DutyActionKind.Damage) is { } attack) TryUse(member, attack, combat.Boss);
    }

    private void UseMitigationIfHurt(SimCharacter member, List<DutyAction> kit)
    {
        if (kit.FirstOrDefault(a => a.Kind == DutyActionKind.SelfMitigation) is { } mitigation && Fraction(member) < MitigateBelow)
            TryUse(member, mitigation, member);
    }

    private bool TryHeal(SimCharacter member, List<DutyAction> kit, IReadOnlyList<SimCharacter> members)
    {
        var alive = members.Where(m => m.IsAlive() && combat.Tracks(m)).ToList();
        if (kit.FirstOrDefault(a => a.Kind is DutyActionKind.PartyHeal or DutyActionKind.PartyShield) is { } partyHeal
            && alive.Count(m => Fraction(m) < PartyHealBelow) >= PartyHealCount
            && TryUse(member, partyHeal, member))
            return true;
        if (kit.FirstOrDefault(a => a.Kind is DutyActionKind.SingleHeal or DutyActionKind.SingleShield) is not { } single) return false;
        var lowest = alive.MinBy(Fraction);
        return lowest is not null && Fraction(lowest) < SingleHealBelow && TryUse(member, single, lowest);
    }

    private bool TryUse(SimCharacter member, DutyAction action, SimCharacter? target)
    {
        if (target is null) return false;
        var hardCast = (Natives.Data.Action(action.ActionId)?.CastSeconds ?? 0f) > 0f;
        if (hardCast && combat.IsMoving(member)) return false;
        return combat.BotUse(member, action, target) == DutyActionRefusal.None;
    }

    private float Fraction(SimCharacter member)
        => combat.Hp(member) is { } hp && combat.MaxHp(member) is { } max ? (float)hp / max : 1f;
}
