using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Radio backup scenarios ported from verify-market-backup.mjs.</summary>
    public sealed class HeistGameBackupTests
    {
        private const float Dt = 1f / 60f;
        private static readonly Vector2 KnownPosition = new Vector2(13.5f, 4.0f);

        private readonly List<GuardProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var profile in _profiles)
            {
                Object.DestroyImmediate(profile);
            }

            _profiles.Clear();
        }

        [Test]
        public void TierThreeNeverDispatchesEvenWithAKnownPosition()
        {
            var game = NewGame();
            var snapshot = WarmKnownPosition(game);

            Assert.That(snapshot.Tier, Is.EqualTo(3), $"0.6 heat stays tier 3 (heat {snapshot.Heat:0.00})");
            Assert.That(snapshot.HasBackup, Is.False, "tier 3 heat never dispatches a responder");
        }

        [Test]
        public void TierFourDispatchesToTheLastKnownPosition()
        {
            var game = NewGame();
            WarmKnownPosition(game);
            game.SetHeat(0.75f);
            Tick(game, 1);
            var snapshot = game.Snapshot();

            Assert.That(snapshot.BackupPhase, Is.EqualTo(BackupPhase.Respond), "tier 4 heat dispatches the responder");
            Assert.That(snapshot.BackupTarget.HasValue, Is.True, "the dispatch carries a target");
            Assert.That(Distance(snapshot.BackupTarget.Value, KnownPosition), Is.LessThan(0.05f),
                "the responder targets the last known position");
            Assert.That(Distance(snapshot.Staff, KnownPosition), Is.GreaterThan(0.6f),
                "the responder has not arrived on the dispatch tick");
        }

        [Test]
        public void ResponderClosesOnTheTargetAndSweeps()
        {
            var game = NewGame();
            var start = DispatchResponder(game).Staff;
            var respondTicks = 0;
            var snapshot = game.Snapshot();
            for (var i = 0; i < 600; i++)
            {
                Tick(game, 1);
                respondTicks += 1;
                snapshot = game.Snapshot();
                if (snapshot.BackupPhase == BackupPhase.Sweep)
                {
                    break;
                }
            }

            Assert.That(snapshot.BackupPhase, Is.EqualTo(BackupPhase.Sweep),
                $"the responder arrives and sweeps within the hard cap ({respondTicks} ticks)");
            var closed = Distance(start, KnownPosition) - Distance(snapshot.Staff, KnownPosition);
            Assert.That(closed, Is.GreaterThanOrEqualTo(1.0f), $"the responder moved at least 1 tile closer ({closed:0.00})");
            Assert.That(respondTicks, Is.LessThan(900), $"respond took fewer than 900 ticks ({respondTicks})");
        }

        [Test]
        public void ResponderRetargetsOnAFreshSighting()
        {
            var game = NewGame();
            DispatchResponder(game);
            game.SetHeat(0.75f);
            game.SetGuard(13.5f, 4.5f);
            game.TeleportPlayer(14.5f, 4.5f);
            Tick(game, 1);
            game.SetHeat(0.75f);
            Tick(game, 1);
            var snapshot = game.Snapshot();

            Assert.That(snapshot.Outcome, Is.EqualTo(HeistOutcome.None), "the run stays live through the retarget");
            Assert.That(snapshot.BackupTarget.HasValue, Is.True, "the responder is still responding");
            Assert.That(Distance(snapshot.BackupTarget.Value, new Vector2(14.5f, 4.5f)), Is.LessThan(0.05f),
                "the responder retargets to the fresh sighting");
        }

        [Test]
        public void SweepEndsWithinItsWindow()
        {
            var game = NewGame();
            DispatchResponder(game);
            var snapshot = game.Snapshot();
            for (var i = 0; i < 600 && snapshot.BackupPhase == BackupPhase.Respond; i++)
            {
                Tick(game, 1);
                snapshot = game.Snapshot();
            }

            var sweepTicks = 0;
            for (var i = 0; i < 300 && snapshot.BackupPhase != BackupPhase.None; i++)
            {
                Tick(game, 1);
                sweepTicks += 1;
                snapshot = game.Snapshot();
            }

            Assert.That(snapshot.HasBackup, Is.False, $"the sweep ends ({sweepTicks} ticks)");
            Assert.That(sweepTicks, Is.LessThanOrEqualTo(245), $"the sweep ends inside SWEEP_TICKS plus a few ticks ({sweepTicks})");
        }

        [Test]
        public void CooldownBlocksASecondDispatchAndReArmsAfterSixHundredTicks()
        {
            var game = NewGame();
            var dispatch = DispatchResponder(game);
            var dispatchTick = dispatch.Tick;
            RunUntilStandDown(game);
            game.SetGuard(35.5f, 26.5f);

            int? earlyTick = null;
            while (game.Snapshot().Tick - dispatchTick < 599)
            {
                game.SetHeat(0.75f);
                Tick(game, 1);
                if (game.Snapshot().HasBackup)
                {
                    earlyTick = game.Snapshot().Tick - dispatchTick;
                    break;
                }
            }

            Assert.That(earlyTick, Is.Null, "the radio cooldown blocks a dispatch before tick 600");

            int? redispatchTick = null;
            var snapshot = game.Snapshot();
            while (game.Snapshot().Tick - dispatchTick < 620)
            {
                game.SetHeat(0.75f);
                Tick(game, 1);
                snapshot = game.Snapshot();
                if (redispatchTick == null && snapshot.BackupPhase == BackupPhase.Respond)
                {
                    redispatchTick = snapshot.Tick - dispatchTick;
                }
            }

            Assert.That(redispatchTick, Is.Not.Null, "the cooldown expiry re-arms the dispatch");
            Assert.That(redispatchTick.Value, Is.GreaterThanOrEqualTo(600), $"re-arm waits for the full cooldown ({redispatchTick})");
            Assert.That(snapshot.BackupPhase, Is.EqualTo(BackupPhase.Respond), "a fresh responder is walking after the re-arm");
        }

        [Test]
        public void FrozenResponderReleasesAtTheHardCap()
        {
            var game = NewGame();
            var dispatch = DispatchResponder(game);
            var frozenTick = dispatch.Tick;
            var frozenStaff = dispatch.Staff;
            game.FrozenPatrol = true;
            var moved = false;
            int? releasedAt = null;
            for (var i = 0; i < 950; i++)
            {
                Tick(game, 1);
                var snapshot = game.Snapshot();
                if (Distance(snapshot.Staff, frozenStaff) > 1e-9f)
                {
                    moved = true;
                }

                if (releasedAt == null && !snapshot.HasBackup)
                {
                    releasedAt = snapshot.Tick - frozenTick;
                }
            }

            game.FrozenPatrol = false;
            Assert.That(moved, Is.False, "the frozen responder never moves");
            Assert.That(releasedAt, Is.Not.Null, "the responder is released");
            Assert.That(releasedAt.Value, Is.LessThanOrEqualTo(950), $"the release lands at or before the hard cap (tick {releasedAt})");
        }

        [Test]
        public void ResetMidRespondClearsTheDispatch()
        {
            var game = NewGame();
            DispatchResponder(game);
            game.Reset();

            Assert.That(game.Snapshot().HasBackup, Is.False, "reset clears a dispatch in flight");
        }

        [Test]
        public void StaffReturnsToTheShelfRouteAfterStandingDown()
        {
            var game = NewGame();
            DispatchResponder(game);
            RunUntilStandDown(game);

            var snapshot = game.Snapshot();
            var routeDistance0 = Distance(snapshot.Staff, RoutePoint(MarketData.StaffRoute[snapshot.StaffWaypoint]));
            Tick(game, 240);
            snapshot = game.Snapshot();
            var routeDistance1 = Distance(snapshot.Staff, RoutePoint(MarketData.StaffRoute[snapshot.StaffWaypoint]));

            Assert.That(routeDistance1, Is.LessThan(routeDistance0 - 0.5f),
                $"staff walks back toward the shelf route ({routeDistance0:0.00} -> {routeDistance1:0.00})");
        }

        private GuardProfile NewStandard()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _profiles.Add(profile);
            return profile;
        }

        private HeistGame NewGame()
        {
            return new HeistGame(NewStandard());
        }

        private static HeistSnapshot WarmKnownPosition(HeistGame game)
        {
            game.SetGuard(13.5f, 4.5f);
            game.TeleportPlayer(13.5f, 4.0f);
            Tick(game, 20);
            var snapshot = game.Snapshot();
            Assert.That(snapshot.Heat, Is.LessThan(HeistState.Tiers[4].Min),
                $"the sighting warmup stays below the tier 4 floor (heat {snapshot.Heat:0.00})");
            Assert.That(snapshot.Outcome, Is.EqualTo(HeistOutcome.None), "the warmup does not end the run");

            game.TeleportPlayer(2f, 2f);
            for (var i = 0; i < 60; i++)
            {
                game.SetHeat(0.6f);
                Tick(game, 1);
            }

            return game.Snapshot();
        }

        private static HeistSnapshot DispatchResponder(HeistGame game)
        {
            WarmKnownPosition(game);
            game.SetHeat(0.75f);
            Tick(game, 1);
            return game.Snapshot();
        }

        private static void RunUntilStandDown(HeistGame game)
        {
            for (var i = 0; i < 1200; i++)
            {
                if (!game.Snapshot().HasBackup)
                {
                    return;
                }

                Tick(game, 1);
            }
        }

        private static void Tick(HeistGame game, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                game.Step(Dt, HeistInput.None);
            }
        }

        private static float Distance(Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Vector2 RoutePoint(GridPos tile)
        {
            return new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        }
    }
}
