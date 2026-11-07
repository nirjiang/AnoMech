using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Combat;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Scenarios.Fru.P1Fatebreaker;

// Who the boss is on for one P1 pull: not an enmity table. Only a valid Provoke moves it, or its
// holder's death. No threat is counted, and Shirk isn't supported.
internal sealed class FruP1Enmity(SimWorld world, DutyCombat combat)
{
    public const uint ProvokeActionId = 7533;
    public const float ProvokeRecastSeconds = 30f;
    public const float ProvokeRange = 25f;
    private const float ReleaseAnimationLock = 0.6f;

    private readonly Dictionary<SimCharacter, float> provokeReadyAt = [];
    private SimEnemy? boss;

    public SimCharacter? Top { get; private set; }

    public void BindBoss(SimEnemy fatebreaker) => boss = fatebreaker;

    public SimCharacter? CurrentTarget()
    {
        if (Top.IsAlive()) return Top;
        Top = NextInLine();
        ShowTarget();
        return Top;
    }

    public void ShowTarget() => boss?.SetTarget(Top, follow: false);

    // The one way to take the boss, for a bot's intent and a real press alike.
    public bool TryProvoke(SimCharacter caster)
    {
        if (!CanProvoke(caster)) return false;
        provokeReadyAt[caster] = world.Events.Elapsed + ProvokeRecastSeconds;
        Top = caster;
        ShowTarget();
        ShowBotProvoke(caster);
        return true;
    }

    private bool CanProvoke(SimCharacter caster)
    {
        if (combat.ActionsOver || !caster.IsAlive() || caster.ActionsLocked || ReferenceEquals(caster, Top)) return false;
        if (caster is not ISimPartyMember { Role: PartyRole.MainTank or PartyRole.OffTank }) return false;
        if (DutyCombat.JobOf(caster) is not (JobId.Paladin or JobId.Warrior or JobId.DarkKnight or JobId.Gunbreaker)) return false;
        if (boss is not { Dead: false, Targetable: true, IsActive: true } fatebreaker) return false;
        if (provokeReadyAt.TryGetValue(caster, out var readyAt) && world.Events.Elapsed < readyAt) return false;
        var delta = fatebreaker.Position - caster.Position;
        var reach = ProvokeRange + fatebreaker.HitboxRadius;
        return delta.X * delta.X + delta.Z * delta.Z <= reach * reach;
    }

    private void ShowBotProvoke(SimCharacter caster)
    {
        if (caster is not SimPartyNpc || boss is not { } fatebreaker) return;
        IReadOnlyList<ActionEffectTarget> targets = Natives.BattleCharas.IsInCharacterManager(fatebreaker.GameObjectId.ObjectId)
            ? [new ActionEffectTarget(fatebreaker.GameObjectId)]
            : [];
        caster.Casting.NativeActionEffect(ProvokeActionId, ReleaseAnimationLock, (ushort)ProvokeActionId, 0, ActionType.Action, 0,
            rotation: caster.Placement().Face(fatebreaker.Position).Rotation, position: fatebreaker.Position,
            animationTargetId: fatebreaker.GameObjectId, targets: targets);
    }

    private SimCharacter? NextInLine()
    {
        var alive = world.Party.ActiveMembers().ToList();
        return alive.FirstOrDefault(member => member is ISimPartyMember { Role: PartyRole.MainTank })
               ?? alive.FirstOrDefault(member => member is ISimPartyMember { Role: PartyRole.OffTank })
               ?? (boss is { } fatebreaker ? world.Party.Find.Closest(fatebreaker.Position) : null);
    }
}
