using System.Numerics;
using System.Reflection;
using AnoMech.Core.Combat;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using Dalamud.Plugin.Services;

namespace AnoMech.Tests;

public class DutyRestartTests
{
    private const string Cause = "test";

    [Test]
    public void TheHumansDeathRestartsOnceOnAFreshLedger()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var oldCombat = duty.Combat;
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(oldCombat.Closed, Is.True);
            Assert.That(duty.Combat, Is.Not.SameAs(oldCombat));
            Assert.That(duty.Player.Dead, Is.False);
            Assert.That(duty.Combat.BossHp, Is.EqualTo(duty.Combat.Tuning.BossMaxHp));
        });

        duty.RunFor(1f);
        Assert.That(duty.Starts, Is.EqualTo(2));
    }

    [Test]
    public void BotDeathsPlayOnWithNoFreezeAndAKillStillClears()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        duty.KillAllBots();
        duty.RunFor(7f);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Paused, Is.False);
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.RunningWithMistake));
        });

        duty.Combat.DealBossDamage(duty.Combat.BossHp);
        duty.Frame();
        Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.CompletedWithMistake));
    }

    [Test]
    public void TheHumansDeathRestartsEvenIfTheBossFellTheSameFrame()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        combat.DealBossDamage(combat.BossHp);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Running));
            Assert.That(duty.Combat, Is.Not.SameAs(combat));
        });
    }

    [Test]
    public void ALethalSnapshotTakenBeforeTheBossFellStillKillsAndRestarts()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var player = duty.Player;

        combat.Boss!.CastFromHelper(HitDueNextFrame(Severity.Lethal), player);
        combat.DealBossDamage(combat.BossHp);
        Assert.That(player.Dead, Is.False, "still ahead of its impact");
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(duty.Deaths.Single(d => d.Role == duty.Seat).ActionId, Is.EqualTo(TestActionId));
            Assert.That(duty.Starts, Is.EqualTo(2));
        });
    }

    [Test]
    public void ANumericSnapshotTakenBeforeTheBossFellStillCommitsAndRestarts()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var player = duty.Player;
        combat.Commit(player, combat.MaxHp(player)!.Value - 1);

        combat.Boss!.CastFromHelper(HitDueNextFrame(Severity.Normal), player);
        combat.DealBossDamage(combat.BossHp);
        Assert.That(combat.Hp(player), Is.EqualTo(1u), "priced at the snapshot, taken at impact");
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(duty.Deaths.Single(d => d.Role == duty.Seat).ActionId, Is.EqualTo(TestActionId));
            Assert.That(duty.Starts, Is.EqualTo(2));
        });
    }

    [Test]
    public void NoFreshSnapshotHitsOnceTheBossHasFallen()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var player = duty.Player;
        var boss = combat.Boss!;
        var hp = combat.Hp(player);
        var barUpBeforeTheKill = HitDueNextFrame(Severity.Lethal) with { Cast = new() { CastSecondsOverride = 0.01f } };
        boss.CastFromHelper(barUpBeforeTheKill, player);

        combat.DealBossDamage(combat.BossHp);
        boss.CastFromHelper(HitDueNextFrame(Severity.Lethal), player);
        boss.CastFromHelper(HitDueNextFrame(Severity.Normal), player);
        duty.Game.World.Events.Add(0f, () =>
        {
            boss.CastFromHelper(HitDueNextFrame(Severity.Lethal), player);
            boss.CastFromHelper(HitDueNextFrame(Severity.Normal), player);
        });
        duty.Frame();

        Assert.Multiple(() =>
        {
            Assert.That(duty.Deaths, Is.Empty);
            Assert.That(player.Dead, Is.False);
            Assert.That(combat.Hp(player), Is.EqualTo(hp));
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Succeeded));
            Assert.That(boss.Dead, Is.True);
            Assert.That(combat.Closed, Is.True);
            Assert.That(duty.Game.World.Combat, Is.Null);
        });
    }

    private const uint TestActionId = AnoMech.Scenarios.Fru.FruConstants.ActionId.PandoraAutoAttack;

    // Shorter than a frame, so its impact falls in the next Events.Tick.
    private static EnemyAction HitDueNextFrame(Severity severity) => new(TestActionId)
    {
        Effects = [EnemyActionEffects.Damage(severity)],
        Timing = new() { DamageDelay = 0.01f },
    };

    [Test]
    public void AHostFullWipeAsksForTheRestartEvenIfTheBossFellTheSameFrame()
    {
        using var duty = new DutyHarness();
        duty.Start(SessionMode.Host);
        duty.Combat.DealBossDamage(duty.Combat.BossHp);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.KillAllBots();
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.RestartRequests, Is.EqualTo(1));
            Assert.That(duty.Game.Status, Is.Not.EqualTo(SessionStatus.Succeeded).And.Not.EqualTo(SessionStatus.CompletedWithMistake));
        });
    }

    [Test]
    public void AHostPartialWipeOnTheFrameTheBossFallsIsAClear()
    {
        using var duty = new DutyHarness();
        duty.Start(SessionMode.Host);
        duty.Combat.DealBossDamage(duty.Combat.BossHp);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.RestartRequests, Is.EqualTo(0));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.CompletedWithMistake));
        });
    }

    [Test]
    public void BotDeathsOnTheFrameTheBossFallsAreAClear()
    {
        using var duty = new DutyHarness(PartyRole.CasterDps, debugBot: false);
        duty.Start();
        duty.Combat.DealBossDamage(duty.Combat.BossHp);
        duty.KillAllBots();
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.CompletedWithMistake));
        });
    }

    private static void EnterAndWaitOutZoneIn(DutyHarness duty, DelayedFramework framework, Action<string?> resolved)
    {
        duty.Game.EnterAndStart(duty.Params(seed: 777), SessionMode.Local, resolved);
        framework.Pump(DutyHarness.FrameSeconds);
        Assert.That(duty.Game.Zone, Is.Not.Null);
    }

    [Test]
    public void AResetDuringTheStartDelayCallsTheStartOff()
    {
        using var duty = new DutyHarness();
        var framework = DelayedFramework.Install();
        string? refusal = null;
        EnterAndWaitOutZoneIn(duty, framework, r => refusal = r);

        duty.Game.Reset();
        framework.Pump(DutyHarness.FrameSeconds);
        framework.Pump(3f);
        framework.Pump(DutyHarness.FrameSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(0));
            Assert.That(duty.Game.ActiveScenario, Is.Null);
            Assert.That(refusal, Is.Not.Null);
        });
    }

    [Test]
    public void ALeaveDuringTheStartDelayCallsTheStartOff()
    {
        using var duty = new DutyHarness();
        var framework = DelayedFramework.Install();
        string? refusal = null;
        EnterAndWaitOutZoneIn(duty, framework, r => refusal = r);

        duty.Game.Leave();
        framework.Pump(DutyHarness.FrameSeconds);
        framework.Pump(3f);
        framework.Pump(DutyHarness.FrameSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(0));
            Assert.That(duty.Game.Zone, Is.Null);
            Assert.That(refusal, Is.Not.Null);
        });
    }

    [Test]
    public void DisposingDuringTheStartDelayCallsTheStartOff()
    {
        using var duty = new DutyHarness();
        var framework = DelayedFramework.Install();
        string? refusal = null;
        EnterAndWaitOutZoneIn(duty, framework, r => refusal = r);

        duty.Dispose();
        framework.Pump(3f);
        framework.Pump(DutyHarness.FrameSeconds);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(0));
            Assert.That(refusal, Is.Not.Null);
        });
    }

    [Test]
    public void GodmodeReportsTheDeathButDoesNotRestart()
    {
        using var duty = new DutyHarness();
        duty.Start();
        duty.Game.GodMode = true;
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.RunFor(1f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.LastRun!.Issues, Is.Not.Empty);
            Assert.That(duty.Player.Dead, Is.False);
        });
    }

    [Test]
    public void AHostRestartsOnlyOnAFullWipeAndOnlyAsksOnce()
    {
        using var duty = new DutyHarness();
        duty.Start(SessionMode.Host);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.RunFor(1f);
        Assert.That(duty.RestartRequests, Is.EqualTo(0), "the human alone");

        duty.Member(PartyRole.MainTank).Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.RunFor(1f);
        Assert.That(duty.RestartRequests, Is.EqualTo(0), "the bots still stand");

        duty.KillAllBots();
        duty.RunFor(1f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.RestartRequests, Is.EqualTo(1));
            Assert.That(duty.Starts, Is.EqualTo(1), "a host goes through the Start route, not a local restart");
        });
    }

    [Test]
    public void APeerNeverRestartsOrRunsTheFight()
    {
        using var duty = new DutyHarness();
        duty.Start(SessionMode.Peer);
        Assert.That(duty.Game.World.Combat, Is.Null);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.KillAllBots();
        duty.RunFor(1f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.RestartRequests, Is.EqualTo(0));
        });
    }

    [Test]
    public void AnEmptyPartyIsNotAWipe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyRestartPolicy.ShouldRestart(SessionMode.Local, new SimParty()), Is.False);
            Assert.That(DutyRestartPolicy.ShouldRestart(SessionMode.Host, new SimParty()), Is.False);
            Assert.That(DutyRestartPolicy.ShouldRestart(SessionMode.Peer, new SimParty()), Is.False);
        });
    }

    [Test]
    public void TheOldRunsEventsAndSubscriptionsDieWithIt()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var oldCombat = duty.Combat;
        var fired = false;
        duty.Game.World.Events.Add(100f, () => fired = true);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.Frame();

        var oldBossHp = oldCombat.BossHp;
        var newBossHp = duty.Combat.BossHp;
        duty.Frame();
        duty.Press(DutyActions.BurstShot, duty.Combat.Boss!);
        Assert.Multiple(() =>
        {
            Assert.That(oldCombat.BossHp, Is.EqualTo(oldBossHp));
            Assert.That(duty.Combat.BossHp, Is.LessThan(newBossHp));
        });

        duty.Game.World.Events.Advance(200f);
        duty.Frame();
        Assert.That(fired, Is.False);
    }

    [Test]
    public void ResetAndDisposeLeaveNoCombatBehind()
    {
        using var duty = new DutyHarness(PartyRole.PhysRangedDps, debugBot: false);
        duty.Start();
        var combat = duty.Combat;
        duty.Game.Reset();
        duty.Frame();
        var boss = combat.BossHp;
        Assert.Multiple(() =>
        {
            Assert.That(combat.Closed, Is.True);
            Assert.That(duty.Game.World.Combat, Is.Null);
        });

        duty.Fake.UserActions.Act(new PlayerAction(PlayerActionStep.Resolved, FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, DutyActions.BurstShot, 0));
        Assert.That(combat.BossHp, Is.EqualTo(boss));
    }

    [Test]
    public void AFixedSeedIsKeptAcrossRestarts()
    {
        using var duty = new DutyHarness();
        duty.Start(seed: 12345);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(duty.Game.LastRun!.Seed, Is.EqualTo(12345));
        });
    }

    [Test]
    public void ARandomSeedIsDrawnAgainOnRestart()
    {
        using var duty = new DutyHarness();
        duty.Start(seed: null);
        var seeds = new HashSet<int> { duty.Game.LastRun!.Seed };
        for (var i = 0; i < 3; i++)
        {
            duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
            duty.Frame();
            seeds.Add(duty.Game.LastRun!.Seed);
        }
        Assert.That(seeds.Count, Is.GreaterThan(1));
    }

    [Test]
    public void EnterAndStartsDrawnSeedDoesNotFixARandomRun()
    {
        using var duty = new DutyHarness();
        var framework = DelayedFramework.Install();
        duty.Game.EnterAndStart(duty.Params(seed: null), SessionMode.Local);
        Pump(duty, framework, () => duty.Starts == 1);

        var seeds = new HashSet<int> { duty.Game.LastRun!.Seed };
        for (var i = 0; i < 3; i++)
        {
            duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
            duty.Frame();
            seeds.Add(duty.Game.LastRun!.Seed);
        }
        Assert.That(seeds.Count, Is.GreaterThan(1));
    }

    [Test]
    public void EnterAndStartKeepsAFixedSeed()
    {
        using var duty = new DutyHarness();
        var framework = DelayedFramework.Install();
        duty.Game.EnterAndStart(duty.Params(seed: 777), SessionMode.Local);
        Pump(duty, framework, () => duty.Starts == 1);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.Frame();
        Assert.That(duty.Game.LastRun!.Seed, Is.EqualTo(777));
    }

    [Test]
    public void ADeferredStartIsDroppedIfTheStayMovedOn()
    {
        using var duty = new DutyHarness();
        var framework = DelayedFramework.Install();
        string? refusal = null;
        duty.Game.EnterAndStart(duty.Params(seed: 777), SessionMode.Local, r => refusal = r);
        for (var i = 0; i < 3 && duty.Game.Zone is null; i++) framework.Pump(DutyHarness.FrameSeconds);
        Assert.That(duty.Game.Zone, Is.Not.Null);

        duty.Game.StartScenario(duty.Params(seed: 1));
        framework.Pump(DutyHarness.FrameSeconds);
        Assert.That(duty.Starts, Is.EqualTo(1));

        framework.Pump(3f);
        framework.Pump(DutyHarness.FrameSeconds);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.LastRun!.Seed, Is.EqualTo(1));
            Assert.That(refusal, Is.Not.Null);
        });
    }

    [Test]
    public void ARefusedRestartStopsVisiblyWithoutRetrying()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        var zone = Natives.Zone;
        Natives.Zone = new BlockedZone(zone);
        duty.Player.Die(SimCharacterDeathExtensions.Environment, Cause);
        duty.RunFor(1f);
        Natives.Zone = zone;
        duty.RunFor(1f);

        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Frozen));
            Assert.That(combat.Closed, Is.True);
            Assert.That(duty.Game.World.Combat, Is.Null);
            Assert.That(duty.Game.LastRun!.Issues.Any(i => i.Description.Contains("Restart failed")), Is.True);
        });
    }

    [Test]
    public void AnUnknownFailureFreezesRatherThanWipesOrRestarts()
    {
        using var duty = new DutyHarness();
        duty.Start();
        duty.Game.World.Fail("engine trouble");
        duty.RunFor(6f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Frozen));
            Assert.That(duty.Starts, Is.EqualTo(1));
            Assert.That(duty.Killed, Is.Empty);
        });
    }

    [Test]
    public void TheEnrageWipesThroughTheNormalRestart()
    {
        using var duty = new DutyHarness();
        duty.Start();
        var combat = duty.Combat;
        duty.Game.World.Events.Advance(300f);
        duty.Frame();
        Assert.Multiple(() =>
        {
            Assert.That(duty.Starts, Is.EqualTo(2));
            Assert.That(combat.Closed, Is.True);
        });
    }

    [Test]
    public void AnEnrageGodmodeSparesEndsTheRunInsteadOfHanging()
    {
        using var duty = new DutyHarness();
        duty.Start();
        duty.Game.GodMode = true;
        duty.Game.World.Events.Advance(300f);
        duty.RunFor(6f);
        Assert.Multiple(() =>
        {
            Assert.That(duty.Game.Status, Is.EqualTo(SessionStatus.Frozen));
            Assert.That(duty.Starts, Is.EqualTo(1));
        });
    }

    [Test]
    public void ALegacyFragmentStillFreezesFiveSecondsAfterADeath()
    {
        var killedAt = float.NaN;
        var run = ScenarioRun.Execute(typeof(FruP5ParadiseRegainedJpScenario), 0, 1, new ScenarioRunOptions
        {
            WriteArtifactsOnFailure = false,
            Probe = p =>
            {
                Assert.That(p.World.Combat, Is.Null);
                if (!p.Crossed(1f)) return;
                killedAt = p.Time;
                p.World.Party.Get(PartyRole.MainTank)!.Die(SimCharacterDeathExtensions.Environment, Cause);
            },
        });
        Assert.Multiple(() =>
        {
            Assert.That(run.Failure, Is.Null);
            Assert.That(run.Deaths, Has.Count.EqualTo(1));
            Assert.That(run.Elapsed - killedAt, Is.EqualTo(5f).Within(0.1f));
        });
    }

    private static void Pump(DutyHarness duty, DelayedFramework framework, Func<bool> done)
    {
        for (var t = 0f; !done(); t += DutyHarness.FrameSeconds)
        {
            if (t > 5f) Assert.Fail("The deferred start never landed.");
            framework.Pump(DutyHarness.FrameSeconds);
            duty.Frame();
        }
    }

    // FakeFramework models Run alone; EnterAndStart's delayed start needs RunOnTick too.
    public class DelayedFramework : DispatchProxy
    {
        private readonly List<(float Due, Action Action)> pending = [];
        private float now;

        internal static DelayedFramework Install()
        {
            var framework = (DelayedFramework)(object)Create<IFramework, DelayedFramework>();
            DalamudServices.Install(nameof(Plugin.Framework), framework);
            return framework;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (args is [Action action, ..])
            {
                var delay = method?.Name == nameof(IFramework.RunOnTick) && args is [_, TimeSpan span, ..] ? (float)span.TotalSeconds : 0f;
                pending.Add((now + delay, action));
                return Task.CompletedTask;
            }
            throw new NotSupportedException($"DelayedFramework does not model IFramework.{method?.Name}");
        }

        internal void Pump(float deltaSeconds)
        {
            now += deltaSeconds;
            foreach (var due in pending.Where(p => p.Due <= now).ToList())
            {
                pending.Remove(due);
                due.Action();
            }
        }
    }

    private sealed class BlockedZone(IZoneControl inner) : IZoneControl
    {
        public bool IsActive => inner.IsActive;
        public bool IsInInn() => inner.IsInInn();

        public string? StartBlockedReason(out string? settling)
        {
            settling = null;
            return "blocked for the test";
        }

        public void EndSpeedBuffs() => inner.EndSpeedBuffs();
        public bool Enter(uint territoryId, Vector3 playerSpawn) => inner.Enter(territoryId, playerSpawn);
        public void Revert() => inner.Revert();
        public void ApplyWeather(byte weatherId) => inner.ApplyWeather(weatherId);
        public void SetWeather(byte weatherId, float transition = 0.5f) => inner.SetWeather(weatherId, transition);
        public void TickWeather() => inner.TickWeather();
        public void TickGuard() => inner.TickGuard();
        public bool InjectIncomingPacket(uint sourceEntityId, ushort opcode, ReadOnlySpan<byte> body, string what) => inner.InjectIncomingPacket(sourceEntityId, opcode, body, what);
        public void HoldSendFirewall(bool hold) => inner.HoldSendFirewall(hold);
        public uint MaxActionEffectCounter => inner.MaxActionEffectCounter;
        public void Dispose() => inner.Dispose();
    }
}
