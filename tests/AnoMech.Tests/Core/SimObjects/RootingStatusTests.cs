using System.Numerics;
using AnoMech.Core.Combat;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios.Fru;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;

namespace AnoMech.Tests;

// Sim wiring only. Whether a blocked client really stands still is decided by the native detour, which
// the fake input doesn't run.
public class RootingStatusTests
{
    private const ushort NoSheetLockStatus = 2304;
    private const ushort StunStatus = 2;
    private const float RootSeconds = 1f;
    private const float ResolveWithin = 2f;

    private static readonly EnemyAction RootingHit = FruActions.PandoraAutoAttack with
    {
        Effects = [ApplyRootingStatus(NoSheetLockStatus, RootSeconds)],
    };

    private static readonly EnemyAction LethalRootingHit = FruActions.PandoraAutoAttack with
    {
        Effects = [Damage(Severity.Lethal), ApplyRootingStatus(NoSheetLockStatus, RootSeconds)],
    };

    private static void Root(DutyHarness duty, SimCharacter target)
    {
        duty.Combat.Boss!.CastFromHelper(RootingHit, target);
        duty.RunUntil(() => target.HasMovementRoot, ResolveWithin, "the root to land");
    }

    [Test]
    public void ARootLocksMovementButLeavesActionsAndAfflictionsAlone()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var player = duty.Player;

        Root(duty, player);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(player.MovementLocked, Is.True);
            Assert.That(player.ActionsLocked, Is.False);
            Assert.That(duty.Fake.PlayerConditions.StatusAffliction, Is.False);
            Assert.That(duty.Fake.PlayerConditions.ControlAffliction, Is.False);
        });
    }

    [Test]
    public void ARootedPlayerCanStillResolveAnAction()
    {
        using var duty = new DutyHarness(PartyRole.RegenHealer, debugBot: false);
        duty.Start();
        var player = duty.Player;
        var combat = duty.Combat;
        Root(duty, player);
        var maxHp = combat.MaxHp(player);
        combat.Commit(player, 10_000);
        var cure = DutyActions.Find(JobId.WhiteMage, DutyActions.CureII)!;

        Assert.That(combat.Validate(player, cure, (ulong)player.GameObjectId), Is.EqualTo(DutyActionRefusal.None));
        duty.Press(DutyActions.CureII, player);
        Assert.That(combat.Hp(player), Is.EqualTo(maxHp));
    }

    [Test]
    public void ThePlayersBlockIsRenewedEveryTickWhileRooted()
    {
        using var duty = new DutyHarness();
        duty.Start();
        Root(duty, duty.Player);
        duty.Frame();
        var input = duty.Fake.PlayerInput;
        Assert.That(input.MovementBlocked, Is.True);

        var before = input.MovementBlockRequests;
        for (var frame = 0; frame < 3; frame++) duty.Frame();

        Assert.That(input.MovementBlockRequests, Is.EqualTo(before + 3));
    }

    [Test]
    public void ExpiryEndsTheRootAndReleasesTheBlock()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        Root(duty, player);
        duty.Frame();

        duty.RunUntil(() => !player.HasMovementRoot, RootSeconds + ResolveWithin, "the root to expire");
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(player.MovementLocked, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlocked, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlockReleases, Is.EqualTo(1));
        });
    }

    [Test]
    public void RemovingTheStatusReleasesTheBlockOnTheNextTick()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        Root(duty, player);
        duty.Frame();

        player.RemoveStatus(NoSheetLockStatus);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(player.MovementLocked, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlocked, Is.False);
        });
    }

    [Test]
    public void DeathReleasesTheBlockAtOnce()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        Root(duty, player);
        duty.Frame();

        player.Die(SimCharacterDeathExtensions.Environment, "test");

        Assert.Multiple(() =>
        {
            Assert.That(player.Dead, Is.True);
            Assert.That(player.HasMovementRoot, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlocked, Is.False);
        });
    }

    [Test]
    public void EndingTheRunReleasesTheBlockAtOnce()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        Root(duty, player);
        duty.Frame();

        duty.Game.World.Party.EndRun();

        Assert.Multiple(() =>
        {
            Assert.That(player.HasMovementRoot, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlocked, Is.False);
        });
    }

    [Test]
    public void DespawnReleasesTheBlockOnceHoweverOftenItRuns()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        Root(duty, player);
        duty.Frame();

        player.Despawn();
        player.Despawn();

        Assert.Multiple(() =>
        {
            Assert.That(duty.Fake.PlayerInput.MovementBlocked, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlockReleases, Is.EqualTo(1));
        });
    }

    [Test]
    public void ABotDropsMoveOrdersWhileRooted()
    {
        using var duty = new DutyHarness(debugBot: false);
        duty.Start();
        var bot = duty.Member(PartyRole.MeleeDpsA);
        Root(duty, bot);
        duty.Frame();
        var rootedAt = bot.Position;

        bot.MoveTo(rootedAt + new Vector3(5f, 0f, 0f));
        duty.RunFor(0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(bot.MoveDestination, Is.Null);
            Assert.That(Vector3.Distance(bot.Position, rootedAt), Is.LessThan(0.01f));
        });
    }

    [Test]
    public void ADebugDrivenPlayersWalkInProgressStopsWhenRooted()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        player.MoveTo(player.Position + new Vector3(5f, 0f, 0f));
        duty.Frame();

        Root(duty, player);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(player.MoveDestination, Is.Null);
            Assert.That(duty.Fake.PlayerInput.SteerDirection, Is.Null);
        });
    }

    [Test]
    public void ForcedMovementStillMovesARootedBot()
    {
        using var duty = new DutyHarness(debugBot: false);
        duty.Start();
        var bot = duty.Member(PartyRole.MeleeDpsA);
        bot.SetPosition(new Vector3(1f, 0f, 1f));
        duty.Frame();
        Root(duty, bot);
        duty.Frame();
        var rootedAt = bot.Position;

        bot.Push(0f, 3f, 0.25f);
        duty.RunFor(0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(bot.MovementLocked, Is.True);
            Assert.That(Vector3.Distance(bot.Position, rootedAt), Is.GreaterThan(1f));
        });
    }

    // Only that the push reaches the player's motion: the client's own slide under a block is
    // UNVERIFIED until checked in game.
    [Test]
    public void ForcedMovementStillReachesARootedPlayer()
    {
        using var duty = new DutyHarness(debugBot: false);
        duty.Start();
        var player = duty.Player;
        Root(duty, player);
        duty.Frame();

        player.Push(0f, 1f, 0.25f);

        Assert.Multiple(() =>
        {
            Assert.That(player.MovementLocked, Is.True);
            Assert.That(player.ConfusedChaseHeld, Is.True);
        });
    }

    [Test]
    public void AHitThatKillsABotLeavesNoRoot()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var bot = duty.Member(PartyRole.MeleeDpsA);

        duty.Combat.Boss!.CastFromHelper(LethalRootingHit, bot);
        duty.RunUntil(() => bot.Dead, ResolveWithin, "the hit to kill");

        Assert.Multiple(() =>
        {
            Assert.That(bot.HasMovementRoot, Is.False);
            Assert.That(bot.HasStatus(NoSheetLockStatus), Is.False);
        });
    }

    // The player's death restarts the run at once, so it is read as it happens.
    [Test]
    public void AHitThatKillsThePlayerArmsNoBlock()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;
        bool? rootedAtDeath = null;
        duty.Game.PartyMemberKilled += (role, _, _, _) =>
        {
            if (role == duty.Seat) rootedAtDeath = player.HasMovementRoot;
        };

        duty.Combat.Boss!.CastFromHelper(LethalRootingHit, player);
        duty.RunUntil(() => rootedAtDeath is not null, ResolveWithin, "the hit to kill");

        Assert.Multiple(() =>
        {
            Assert.That(rootedAtDeath, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlockRequests, Is.EqualTo(0));
        });
    }

    [Test]
    public void TheSameStatusAppliedPlainlyDoesNotRoot()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;

        player.AddStatus(NoSheetLockStatus, RootSeconds);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(player.HasStatus(NoSheetLockStatus), Is.True);
            Assert.That(player.MovementLocked, Is.False);
            Assert.That(duty.Fake.PlayerInput.MovementBlockRequests, Is.EqualTo(0));
        });
    }

    [Test]
    public void ASheetLockStillUsesTheAfflictionsAndNeverTheBlock()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var player = duty.Player;

        player.AddStatus(StunStatus, RootSeconds);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(player.MovementLocked, Is.True);
            Assert.That(player.ActionsLocked, Is.True);
            Assert.That(player.HasMovementRoot, Is.False);
            Assert.That(duty.Fake.PlayerConditions.StatusAffliction, Is.True);
            Assert.That(duty.Fake.PlayerInput.MovementBlockRequests, Is.EqualTo(0));
        });
    }

    [Test]
    public void ARunWithNoRootNeverCallsTheBlock()
    {
        using var duty = new DutyHarness();
        duty.Start();

        duty.RunFor(10f);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Fake.PlayerInput.MovementBlockRequests, Is.EqualTo(0));
            Assert.That(duty.Fake.PlayerInput.MovementBlockReleases, Is.EqualTo(0));
            Assert.That(duty.Game.World.Party.AllMembers().Any(member => member.MovementLocked), Is.False);
        });
    }
}
