using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Combat;

internal enum DutyActionRefusal
{
    None,
    Closed,
    CasterDead,
    ActionsLocked,
    NoTarget,
    TargetDead,
    OutOfRange,
    OnCooldown,
    NotEnoughMana,
    AlreadyCasting,
    WrongJob,
    Untargetable,
}

// One run's authoritative HP, MP, shields and cooldowns, for a scenario that opted in through
// SimWorld.Combat. What an action hits is still the enemy action's own area and effects: this only
// says what a survivable hit costs. Living on one client, it does not yet reach peers.
internal sealed class DutyCombat : IDisposable
{
    // Network and frame slack on a press the client itself already allowed.
    private const float CooldownTolerance = 0.1f;
    private const float MovedDistance = 0.1f;
    private const float ShieldDisplayScale = 100f;
    private const float ReleaseAnimationLock = 0.6f;

    private readonly SimWorld world;
    // Handed to the JobActions and ActionTargets paths; the run's own stream, so a seed replays them.
    private readonly Random actionRng;
    private readonly Dictionary<SimCharacter, Vitals> vitals = [];
    private readonly Dictionary<SimCharacter, PendingCast> casts = [];
    private readonly Dictionary<SimCharacter, Vector3> lastPositions = [];
    private readonly HashSet<SimCharacter> moving = [];
    private readonly DutyBotRotation bots;
    private float mpTick;
    private uint bossHp;
    private bool subscribed;

    public DutyTuning Tuning { get; }
    public SimEnemy? Boss { get; private set; }
    public uint BossHp => bossHp;

    // The run is over, by a reported clear or its end: nothing further lands.
    public bool Closed { get; private set; }

    // No more actions from here. Hits already due this frame still land, as the session judges the
    // frame's deaths before it believes the kill.
    public bool BossDefeated { get; private set; }

    public bool ActionsOver => Closed || BossDefeated;

    public static JobId? JobOf(SimCharacter member) => member switch
    {
        SimPartyNpc bot => (JobId)bot.ClassJob,
        SimNetworkPuppet puppet => (JobId)puppet.ClassJob,
        SimPlayer => (JobId)Natives.BattleCharas.LocalPlayer.ClassJob,
        _ => null,
    };

    public DutyCombat(SimWorld world, DutyTuning tuning)
    {
        this.world = world;
        actionRng = new Random(world.Stream("duty-actions").Seed);
        Tuning = tuning;
        bossHp = tuning.BossMaxHp;
        bots = new DutyBotRotation(this, world.Party);
        foreach (var member in world.Party.AllMembers())
            if (member is ISimPartyMember slot)
                vitals[member] = new Vitals(tuning.MaxHp(slot.Role), tuning.MaxMp);
        Natives.UserActions.Acted += OnPlayerActed;
        subscribed = true;
        foreach (var member in vitals.Keys) Show(member);
    }

    public void BindBoss(SimEnemy boss)
    {
        Boss = boss;
        ShowBoss();
    }

    public void Dispose()
    {
        Closed = true;
        CancelAllCasts();
        if (!subscribed) return;
        subscribed = false;
        Natives.UserActions.Acted -= OnPlayerActed;
    }

    // Null for what the ledger doesn't track. A dead member has no HP whichever path killed it.
    public uint? Hp(SimCharacter member) => vitals.TryGetValue(member, out var v) ? (member.Dead ? 0u : v.Hp) : null;
    public uint? MaxHp(SimCharacter member) => vitals.TryGetValue(member, out var v) ? v.MaxHp : null;
    public uint? Mp(SimCharacter member) => vitals.TryGetValue(member, out var v) ? v.Mp : null;
    public uint Shield(SimCharacter member) => vitals.TryGetValue(member, out var v) ? v.ShieldTotal : 0u;
    public bool Tracks(SimCharacter member) => vitals.ContainsKey(member);
    public bool IsCasting(SimCharacter member) => casts.ContainsKey(member);
    public bool IsMoving(SimCharacter member) => moving.Contains(member);
    public float Cooldown(SimCharacter member, DutyAction action)
        => vitals.TryGetValue(member, out var v) ? CooldownOf(v, action) : 0f;

    public void SetMp(SimCharacter member, uint mp)
    {
        if (!vitals.TryGetValue(member, out var v)) return;
        v.Mp = Math.Min(mp, v.MaxMp);
        Show(member);
    }

    // The amount a survivable hit lands for, percentage mitigation taken at the snapshot. A shield
    // absorbs later, at Commit, from what it holds by then.
    public uint SnapshotHit(SimCharacter target, EnemyAction action, Severity severity, DamageKind kind)
    {
        var taken = Mitigation.TakenFraction(target.ActiveStatusSnapshot.Select(s => s.StatusId), kind);
        return (uint)MathF.Round(Tuning.HitAmount(action.ActionId, severity) * taken);
    }

    public void Commit(SimCharacter target, uint amount)
    {
        if (Closed || target.Dead || !vitals.TryGetValue(target, out var v)) return;
        v.Hp -= Math.Min(v.Hp, Absorb(target, v, amount));
        Show(target);
    }

    // At the same time as the Commit that took the member to 0, after the client saw the hit.
    public void DieIfAtZero(SimCharacter target, EnemyAction action)
    {
        if (Closed || target.Dead || !vitals.TryGetValue(target, out var v) || v.Hp != 0) return;
        if (target.Die(action.ActionId, action.DeathExplanation)) return;
        // Godmode spared them: alive at 0 HP would have no way back.
        v.Hp = 1;
        Show(target);
    }

    // DieIfAtZero for damage no action dealt.
    public void DieOfStatusIfAtZero(SimCharacter target, ushort statusId)
    {
        if (Closed || target.Dead || !vitals.TryGetValue(target, out var v) || v.Hp != 0) return;
        if (target.DieOfStatus(statusId)) return;
        v.Hp = 1;
        Show(target);
    }

    public void DealBossDamage(uint amount)
    {
        if (ActionsOver || Boss is not { } boss || boss.Dead) return;
        bossHp -= Math.Min(bossHp, amount);
        ShowBoss();
        if (bossHp > 0) return;
        BossDefeated = true;
        CancelAllCasts();
    }

    private void CancelAllCasts()
    {
        foreach (var (member, cast) in casts.ToList()) CancelCast(member, cast);
    }

    public void Tick(float deltaSeconds)
    {
        if (ActionsOver) return;
        foreach (var (member, v) in vitals)
        {
            foreach (var key in v.Cooldowns.Keys.ToList())
                v.Cooldowns[key] = MathF.Max(0f, v.Cooldowns[key] - deltaSeconds);
            ExpireShields(member, v);
            TrackMovement(member);
        }
        TickMana(deltaSeconds);
        TickCasts(deltaSeconds);
        bots.Tick();
    }

    private void TrackMovement(SimCharacter member)
    {
        var position = member.Position;
        if (lastPositions.TryGetValue(member, out var last) && Vector3.DistanceSquared(position, last) > 0f) moving.Add(member);
        else moving.Remove(member);
        lastPositions[member] = position;
    }

    private void TickMana(float deltaSeconds)
    {
        mpTick += deltaSeconds;
        if (mpTick < Tuning.MpTickSeconds) return;
        // Every tick a long frame covered, so regen doesn't depend on the frame rate.
        var ticks = (uint)(mpTick / Tuning.MpTickSeconds);
        mpTick -= ticks * Tuning.MpTickSeconds;
        foreach (var (member, v) in vitals)
        {
            if (member.Dead || v.Mp == v.MaxMp) continue;
            v.Mp = (uint)Math.Min(v.MaxMp, v.Mp + (long)Tuning.MpPerTick * ticks);
            Show(member);
        }
    }

    private void ExpireShields(SimCharacter member, Vitals v)
    {
        foreach (var statusId in v.Shields.Keys.ToList())
        {
            if (member.HasStatus(statusId)) continue;
            v.Shields.Remove(statusId);
            Show(member);
        }
    }

    private uint Absorb(SimCharacter target, Vitals v, uint amount)
    {
        foreach (var statusId in v.Shields.Keys.ToList())
        {
            if (amount == 0) break;
            var absorbed = Math.Min(amount, v.Shields[statusId]);
            amount -= absorbed;
            v.Shields[statusId] -= absorbed;
            if (v.Shields[statusId] > 0) continue;
            v.Shields.Remove(statusId);
            target.RemoveStatus(statusId);
        }
        return amount;
    }

    private void OnPlayerActed(PlayerAction acted)
    {
        if (ActionsOver || acted.Step != PlayerActionStep.Resolved || acted.ActionType != ActionType.Action) return;
        if (world.Party.Player is not { } player) return;
        if (DutyActions.Find((JobId)Natives.BattleCharas.LocalPlayer.ClassJob, acted.ActionId) is not { } action) return;
        // The client ran the cast bar itself, so only what is left of the recast remains.
        var castSeconds = CastSeconds(action);
        TryResolve(player, action, acted.TargetId, recastSpent: castSeconds);
    }

    // A bot's press: instant now, a hard cast when its bar completes unmoved.
    public DutyActionRefusal BotUse(SimCharacter member, DutyAction action, SimCharacter? target)
    {
        var targetId = (ulong)(target ?? member).GameObjectId;
        var refusal = Validate(member, action, targetId);
        if (refusal != DutyActionRefusal.None) return refusal;
        if (casts.ContainsKey(member)) return DutyActionRefusal.AlreadyCasting;

        var castSeconds = CastSeconds(action);
        if (castSeconds <= 0f)
        {
            ShowRelease(member, action, ResolveValidated(member, action, targetId, recastSpent: 0f));
            return DutyActionRefusal.None;
        }
        // The GCD runs from the press and is handed back if the cast is lost.
        StartCooldown(vitals[member], action, 0f);
        casts[member] = new PendingCast(action, targetId, member.Position, castSeconds);
        if (member is SimPartyNpc)
            member.Casting.NativeCast(action.ActionId, ActionType.Action, 0f, castSeconds, interruptible: true, targetId: (target ?? member).GameObjectId);
        return DutyActionRefusal.None;
    }

    private void TickCasts(float deltaSeconds)
    {
        foreach (var member in casts.Keys.ToList())
        {
            var cast = casts[member];
            if (!member.IsAlive() || Vector3.Distance(member.Position, cast.From) > MovedDistance)
            {
                CancelCast(member, cast);
                continue;
            }
            cast.Remaining -= deltaSeconds;
            if (cast.Remaining > 0f) continue;
            casts.Remove(member);
            // Checked again: the target may have died and the MP gone since the press.
            if (ValidateAtRelease(member, cast.Action, cast.TargetId) != DutyActionRefusal.None) continue;
            Spend(member, cast.Action);
            ShowRelease(member, cast.Action, ApplyEffect(member, cast.Action, cast.TargetId));
            if (ActionsOver) return;
        }
    }

    private void CancelCast(SimCharacter member, PendingCast cast)
    {
        casts.Remove(member);
        if (vitals.TryGetValue(member, out var v) && cast.Action.OnGcd) v.Cooldowns[GcdKey] = 0f;
        if (member is SimPartyNpc && member.IsActive) member.Casting.Cancel(CastCancelReason.Interrupted);
    }

    private void TryResolve(SimCharacter member, DutyAction action, ulong targetId, float recastSpent)
    {
        if (Validate(member, action, targetId) == DutyActionRefusal.None)
            ResolveValidated(member, action, targetId, recastSpent);
    }

    private IReadOnlyList<SimCharacter> ResolveValidated(SimCharacter member, DutyAction action, ulong targetId, float recastSpent)
    {
        StartCooldown(vitals[member], action, recastSpent);
        Spend(member, action);
        return ApplyEffect(member, action, targetId);
    }

    public DutyActionRefusal Validate(SimCharacter member, DutyAction action, ulong targetId)
    {
        var refusal = ValidateAtRelease(member, action, targetId);
        if (refusal != DutyActionRefusal.None) return refusal;
        return CooldownOf(vitals[member], action) > CooldownTolerance ? DutyActionRefusal.OnCooldown : DutyActionRefusal.None;
    }

    private DutyActionRefusal ValidateAtRelease(SimCharacter member, DutyAction action, ulong targetId)
    {
        if (ActionsOver) return DutyActionRefusal.Closed;
        if (!vitals.TryGetValue(member, out var v) || !member.IsAlive()) return DutyActionRefusal.CasterDead;
        if (JobOf(member) != action.Job) return DutyActionRefusal.WrongJob;
        if (member.ActionsLocked) return DutyActionRefusal.ActionsLocked;
        if (v.Mp < action.ManaCost) return DutyActionRefusal.NotEnoughMana;
        switch (action.Kind)
        {
            case DutyActionKind.Damage:
                if (Boss is not { IsActive: true } boss || (ulong)boss.GameObjectId != targetId) return DutyActionRefusal.NoTarget;
                if (boss.Dead) return DutyActionRefusal.TargetDead;
                if (!boss.Targetable) return DutyActionRefusal.Untargetable;
                return InRange(member, boss, action.Range) ? DutyActionRefusal.None : DutyActionRefusal.OutOfRange;
            case DutyActionKind.SingleHeal or DutyActionKind.SingleShield:
                if (vitals.Keys.FirstOrDefault(m => m.IsActive && (ulong)m.GameObjectId == targetId) is not { } friend) return DutyActionRefusal.NoTarget;
                if (friend.Dead) return DutyActionRefusal.TargetDead;
                return InRange(member, friend, action.Range) ? DutyActionRefusal.None : DutyActionRefusal.OutOfRange;
            default:
                return DutyActionRefusal.None;
        }
    }

    private static bool InRange(SimCharacter caster, SimCharacter target, float range)
    {
        var delta = target.Position - caster.Position;
        var reach = range + target.HitboxRadius;
        return delta.X * delta.X + delta.Z * delta.Z <= reach * reach;
    }

    private void Spend(SimCharacter member, DutyAction action)
    {
        var v = vitals[member];
        if (action.ManaCost == 0) return;
        v.Mp -= action.ManaCost;
        Show(member);
    }

    // Who it landed on.
    private IReadOnlyList<SimCharacter> ApplyEffect(SimCharacter caster, DutyAction action, ulong targetId)
    {
        switch (action.Kind)
        {
            case DutyActionKind.Damage:
                var boss = Boss!;
                DealBossDamage(action.Amount);
                return [boss];
            case DutyActionKind.SelfMitigation:
                if (!AppliedByJobActionHandler(caster))
                    JobActions.ApplyEffects(caster, action.ActionId, (ulong)caster.GameObjectId, actionRng);
                return [caster];
            default:
                var friends = Friendly(caster, action, targetId);
                foreach (var friend in friends) HealAndShield(friend, action);
                return friends;
        }
    }

    // That handler runs only for a real press, and only with the UserActions module on.
    private bool AppliedByJobActionHandler(SimCharacter caster)
        => caster is SimPlayer && !world.Party.IsBotDriven(caster) && Natives.UserActions.Enabled;

    private IReadOnlyList<SimCharacter> Friendly(SimCharacter caster, DutyAction action, ulong targetId)
        => ActionTargets.Friendly(new ActionContext(action.ActionId, targetId, caster, actionRng)).Where(vitals.ContainsKey).ToList();

    private void HealAndShield(SimCharacter target, DutyAction action)
    {
        if (target.Dead) return;
        var v = vitals[target];
        v.Hp = Math.Min(v.MaxHp, v.Hp + action.Amount);
        if (action.ShieldAmount > 0)
        {
            target.RemoveStatus(action.ShieldStatusId);
            target.AddStatusParam(action.ShieldStatusId, 0, action.ShieldSeconds);
            v.Shields[action.ShieldStatusId] = action.ShieldAmount;
        }
        Show(target);
    }

    private static float CastSeconds(DutyAction action) => Natives.Data.Action(action.ActionId)?.CastSeconds ?? 0f;

    private const uint GcdKey = uint.MaxValue;

    private static float CooldownOf(Vitals v, DutyAction action)
        => v.Cooldowns.GetValueOrDefault(action.OnGcd ? GcdKey : action.ActionId);

    private static void StartCooldown(Vitals v, DutyAction action, float recastSpent)
        => v.Cooldowns[action.OnGcd ? GcdKey : action.ActionId] = MathF.Max(0f, action.Recast - recastSpent);

    // Only for a bot: a player's own client already played its action. A party-wide action animates
    // on its caster and lists whoever it reached; any other aims at its one recipient.
    private static void ShowRelease(SimCharacter member, DutyAction action, IReadOnlyList<SimCharacter> recipients)
    {
        if (member is not SimPartyNpc || recipients.Count == 0) return;
        var aim = action.Kind is DutyActionKind.PartyHeal or DutyActionKind.PartyShield ? member : recipients[0];
        // One outside CharacterManager null-derefs the client's ApplyAll.
        var targets = recipients.Where(r => Natives.BattleCharas.IsInCharacterManager(r.GameObjectId.ObjectId))
            .Select(r => new ActionEffectTarget(r.GameObjectId)).ToList();
        var rotation = ReferenceEquals(aim, member) ? member.Rotation : member.Placement().Face(aim.Position).Rotation;
        member.Casting.NativeActionEffect(action.ActionId, ReleaseAnimationLock, (ushort)action.ActionId, 0, ActionType.Action, 0,
            rotation: rotation, position: aim.Position, animationTargetId: aim.GameObjectId, targets: targets);
    }

    private void Show(SimCharacter member)
    {
        // A death already sent its own zeros; this would stand the bar back up on a corpse.
        if (member.Dead || !vitals.TryGetValue(member, out var v) || member.Proxy is not { Exists: true } chara) return;
        var shield = (byte)Math.Clamp(ShieldDisplayScale * v.ShieldTotal / v.MaxHp, 0f, ShieldDisplayScale);
        member.ShowVitals(Scaled(v.Hp, v.MaxHp, chara.MaxHealth), Scaled(v.Mp, v.MaxMp, chara.MaxMana), shield);
    }

    private void ShowBoss()
    {
        if (Boss is { } boss && boss.Proxy is { Exists: true } chara)
            boss.ShowVitals(Scaled(bossHp, Tuning.BossMaxHp, chara.MaxHealth), chara.Mana, 0);
    }

    // The native bar is only a view of the ledger, in its own max. Rounded up so a living member
    // never reads 0.
    private static uint Scaled(uint value, uint max, uint nativeMax)
        => max == 0 ? 0 : (uint)Math.Ceiling((double)value / max * nativeMax);

    private sealed class Vitals(uint maxHp, uint maxMp)
    {
        public uint MaxHp { get; } = maxHp;
        public uint MaxMp { get; } = maxMp;
        public uint Hp { get; set; } = maxHp;
        public uint Mp { get; set; } = maxMp;
        public Dictionary<ushort, uint> Shields { get; } = [];
        public Dictionary<uint, float> Cooldowns { get; } = [];
        public uint ShieldTotal => (uint)Shields.Values.Sum(s => (long)s);
    }

    private sealed class PendingCast(DutyAction action, ulong targetId, Vector3 from, float remaining)
    {
        public DutyAction Action { get; } = action;
        public ulong TargetId { get; } = targetId;
        public Vector3 From { get; } = from;
        public float Remaining { get; set; } = remaining;
    }
}
