using System.Collections.Generic;
using HeistSim.Market;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>New snapshot fields read from scripted market steps.</summary>
    public sealed class HeistSnapshotTests
    {
        private const float Dt = 1f / 60f;
        private readonly List<GuardProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var profile in _profiles)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }

            _profiles.Clear();
        }

        [Test]
        public void FreshSnapshotHasEmptySignals()
        {
            var snapshot = NewGame().Snapshot();

            Assert.That(snapshot.ChannelKind, Is.Null, "no channel at the start");
            Assert.That(snapshot.ChannelProgress, Is.EqualTo(0f), "no held seconds at the start");
            Assert.That(snapshot.ChannelHold, Is.EqualTo(0f), "no hold time at the start");
            Assert.That(snapshot.LastChannelResult, Is.EqualTo(ChannelResult.None), "no channel tick yet");
            Assert.That(snapshot.CameraSightings, Is.EqualTo(0), "no camera mask at the start");
            Assert.That(snapshot.CameraSightingCount, Is.EqualTo(0), "no camera count at the start");
            Assert.That(snapshot.LastSeenPos, Is.Null, "no sensor sighting yet");
            Assert.That(snapshot.ActiveTags, Is.EqualTo(0), "no tags yet");
            Assert.That(snapshot.EasAlarmTicks, Is.EqualTo(0), "no EAS alarm yet");
            Assert.That(snapshot.DetectedNow, Is.False, "the thief starts unseen");
            Assert.That(snapshot.FirstDetectionEdge, Is.False, "no detection edge yet");
            Assert.That(snapshot.PanelInRange, Is.False, "the panel is far away");
            Assert.That(snapshot.DetaggerInRange, Is.False, "the de-tagger is far away");
            Assert.That(snapshot.ExitInRange, Is.False, "the gate is far away");
            Assert.That(snapshot.ExitBlockedByTags, Is.False, "the gate is not blocked yet");
            Assert.That(snapshot.LastInteraction, Is.EqualTo(InteractOutcome.None), "no action attempt yet");
        }

        [Test]
        public void ScriptedStealExposesChannelProgressAndResult()
        {
            var game = ScriptedStealGame();
            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            for (var i = 0; i < 200 && game.Snapshot().ChannelKind == null; i++)
            {
                game.Step(Dt, hold);
            }

            var started = game.Snapshot();
            game.Step(Dt, hold);
            var running = game.Snapshot();
            Assert.That(running.ChannelKind, Is.EqualTo("steal"), "the started channel is a steal");
            Assert.That(running.ChannelHold, Is.GreaterThan(1f), "the steal requires a real hold time");
            Assert.That(running.ChannelProgress, Is.GreaterThan(0f), "the channel accumulates progress");
            Assert.That(running.ChannelProgress, Is.LessThan(running.ChannelHold), "the channel is still running");
            Assert.That(running.LastChannelResult, Is.EqualTo(ChannelResult.Running), "the last tick advanced the channel");
            Assert.That(started.LastInteraction, Is.EqualTo(InteractOutcome.Started), "the action button started the channel");

            for (var i = 0; i < 900 && game.Snapshot().BagCount == 0; i++)
            {
                game.Step(Dt, hold);
            }

            var done = game.Snapshot();
            Assert.That(done.LastChannelResult, Is.EqualTo(ChannelResult.Done), "the final channel tick reports Done");
            Assert.That(done.ChannelKind, Is.Null, "the completed channel is cleared");
            Assert.That(done.BagCount, Is.EqualTo(1), "the completed steal fills one slot");
        }

        [Test]
        public void MovingDuringAChannelReportsCancelled()
        {
            var game = ScriptedStealGame();
            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            for (var i = 0; i < 200 && game.Snapshot().ChannelKind == null; i++)
            {
                game.Step(Dt, hold);
            }

            Assert.That(game.Snapshot().ChannelKind, Is.EqualTo("steal"), "a channel is active before the interruption");

            var walk = new HeistInput(1f, 0f, action: false, crouch: true);
            game.Step(Dt, walk);

            var snapshot = game.Snapshot();
            Assert.That(snapshot.LastChannelResult, Is.EqualTo(ChannelResult.Cancelled), "movement cancels the channel");
            Assert.That(snapshot.ChannelKind, Is.Null, "a cancelled channel is cleared");
            Assert.That(snapshot.ChannelProgress, Is.EqualTo(0f), "a cancelled channel has no progress");
        }

        [Test]
        public void DetectionEdgeAndLastSeenComeFromTheGuardSighting()
        {
            var game = NewGame();
            game.TeleportPlayer(4.5f, 4.5f);
            game.SetGuard(5.2f, 4.5f);

            game.Step(Dt, HeistInput.None);
            var first = game.Snapshot();
            Assert.That(first.DetectedNow, Is.True, "the guard sees a thief 0.7 tiles away");
            Assert.That(first.FirstDetectionEdge, Is.True, "the first visible tick is an edge");
            Assert.That(first.LastSeenPos, Is.EqualTo(first.Player), "the sighting records the thief tile");

            game.Step(Dt, HeistInput.None);
            var second = game.Snapshot();
            Assert.That(second.FirstDetectionEdge, Is.False, "a second visible tick is not a new edge");
            Assert.That(second.DetectedNow, Is.True, "the guard still sees the thief");
        }

        [Test]
        public void CameraSightingsBuildABitMask()
        {
            var found = FindCameraSighting();
            Assert.That(found.HasValue, Is.True, "a scripted position enters at least one CCTV cone");
            Assert.That(found.Value.CameraSightingCount, Is.GreaterThan(0), "the mask has at least one set bit");

            var anyBit = false;
            for (var i = 0; i < MarketData.Cameras.Length; i++)
            {
                anyBit |= found.Value.CameraSees(i);
            }

            Assert.That(anyBit, Is.True, "one of the declared cameras owns the set bit");
            Assert.That(found.Value.CameraSees(99), Is.False, "an out of range camera index has no bit");
        }

        [Test]
        public void RangeFlagsFollowTheDeviceTiles()
        {
            var game = NewGame();

            game.TeleportPlayer((float)MarketData.Panel.I, (float)MarketData.Panel.J);
            var panel = game.Snapshot();
            Assert.That(panel.PanelInRange, Is.True, "the thief stands on the panel tile");
            Assert.That(panel.DetaggerInRange, Is.False, "the de-tagger is further away");

            game.TeleportPlayer((float)MarketData.Detagger.I, (float)MarketData.Detagger.J);
            var detagger = game.Snapshot();
            Assert.That(detagger.DetaggerInRange, Is.True, "the thief stands on the de-tagger tile");
            Assert.That(detagger.PanelInRange, Is.False, "the panel is further away");

            game.TeleportPlayer((float)MarketData.EasGate.I, (float)MarketData.EasGate.J);
            var exit = game.Snapshot();
            Assert.That(exit.ExitInRange, Is.True, "the thief stands in the gate radius");
            Assert.That(exit.ExitBlockedByTags, Is.False, "an empty bag does not block the gate");
        }

        [Test]
        public void RfTagsBlockTheExitAndArmTheAlarm()
        {
            var game = NewGame();
            game.SetGuard(1f, 16f);
            game.SetStaff(1f, 16f);
            game.FrozenPatrol = true;

            Assert.That(StealOneTaggedItem(game), Is.True, "a scripted steal picks up an RF tagged good");
            var tagged = game.Snapshot();
            Assert.That(tagged.ActiveTags, Is.GreaterThan(0), "the snapshot counts the active tag");

            game.TeleportPlayer((float)MarketData.EasGate.I, (float)MarketData.EasGate.J);
            game.Step(Dt, HeistInput.None);
            var exit = game.Snapshot();
            Assert.That(exit.ExitInRange, Is.True, "the thief is inside the gate radius");
            Assert.That(exit.ExitBlockedByTags, Is.True, "the tag blocks the exit");
            Assert.That(exit.EasAlarmTicks, Is.GreaterThan(0), "the blocked exit arms the alarm");
        }

        private HeistGame NewGame()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _profiles.Add(profile);
            return new HeistGame(profile);
        }

        private HeistGame ScriptedStealGame()
        {
            var game = NewGame();
            game.SetGuard(1f, 16f);
            game.SetStaff(1f, 16f);
            game.FrozenPatrol = true;
            game.TeleportPlayer(11.0f, 7.5f);
            return game;
        }

        private static bool StealOneTaggedItem(HeistGame game)
        {
            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            foreach (var loot in MarketData.Loot)
            {
                if (!loot.Tagged)
                {
                    continue;
                }

                var position = MarketLayout.ItemPosition(MarketData.Fixture(loot.Id));
                game.TeleportPlayer(position.x, position.y);
                for (var i = 0; i < 700 && game.Snapshot().ActiveTags == 0; i++)
                {
                    game.Step(Dt, hold);
                }

                if (game.Snapshot().ActiveTags > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private HeistSnapshot? FindCameraSighting()
        {
            var game = NewGame();
            game.SetGuard(1f, 1f);
            game.SetStaff(1f, 1f);
            game.FrozenPatrol = true;

            for (var camera = 0; camera < MarketData.Cameras.Length; camera++)
            {
                var def = MarketData.Cameras[camera];
                var angle = (float)def.BaseAngle;
                for (var distance = 1f; distance <= 4.5f; distance += 0.5f)
                {
                    for (var lateral = -1f; lateral <= 1f; lateral += 0.5f)
                    {
                        var px = (float)def.I + Mathf.Cos(angle) * distance - Mathf.Sin(angle) * lateral;
                        var py = (float)def.J + Mathf.Sin(angle) * distance + Mathf.Cos(angle) * lateral;
                        game.TeleportPlayer(px, py);
                        game.Step(Dt, HeistInput.None);
                        var snapshot = game.Snapshot();
                        if (snapshot.CameraSightingCount > 0)
                        {
                            return snapshot;
                        }
                    }
                }
            }

            return null;
        }
    }
}
