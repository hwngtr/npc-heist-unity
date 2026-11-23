using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Field of view, occlusion, hearing, and alert dynamics.</summary>
    public sealed class PerceptionTests
    {
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
        public void FovConeAcceptsTargetWithinDistanceAndAngle()
        {
            var profile = NewProfile("standard");
            var (visible, dist, angle) = GuardPerception.InFovCone(
                new Vector2(4f, 4f),
                0f,
                new Vector2(7f, 4f),
                profile);

            Assert.That(visible, Is.True);
            Assert.That(dist, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(angle, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FovConeRejectsTargetOutsideAngle()
        {
            var profile = NewProfile("standard");
            var target = new Vector2(4f + 3f * Mathf.Cos(0.9f), 4f + 3f * Mathf.Sin(0.9f));
            var (visible, _, angle) = GuardPerception.InFovCone(new Vector2(4f, 4f), 0f, target, profile);

            Assert.That(visible, Is.False);
            Assert.That(Mathf.Abs(angle), Is.GreaterThan(profile.fov_half_angle));
        }

        [Test]
        public void FovConeRejectsTargetBeyondDistance()
        {
            var profile = NewProfile("standard");
            var (visible, dist, _) = GuardPerception.InFovCone(
                new Vector2(1f, 1f),
                0f,
                new Vector2(7f, 1f),
                profile);

            Assert.That(visible, Is.False);
            Assert.That(dist, Is.GreaterThan(profile.fov_distance));
        }

        [Test]
        public void FovConeAcceptsCloseTargetBehindGuard()
        {
            var profile = NewProfile("standard");
            var (visible, dist, _) = GuardPerception.InFovCone(
                new Vector2(4f, 4f),
                0f,
                new Vector2(4f, 3.2f),
                profile);

            Assert.That(visible, Is.True);
            Assert.That(dist, Is.LessThanOrEqualTo(profile.close_proximity));
        }

        [Test]
        public void OcclusionBlocksVisibleTarget()
        {
            var profile = NewProfile("standard");
            var guard = new Vector2(1f, 1f);
            var target = new Vector2(4f, 1f);
            var obstacles = new[] { new ObstacleBox(2.2f, 0.2f, 2.5f, 1.8f) };

            var (blocked, _, _) = GuardPerception.InFovCone(guard, 0f, target, profile, obstacles);
            var (clear, _, _) = GuardPerception.InFovCone(guard, 0f, target, profile);

            Assert.That(blocked, Is.False);
            Assert.That(clear, Is.True);
        }

        [Test]
        public void HearingRadiusUsesProfileScale()
        {
            var standard = NewProfile("standard");
            var observant = NewProfile("observant");
            var guard = new Vector2(0f, 0f);
            var source = new SoundSource(new Vector2(5f, 0f), 2f);

            Assert.That(GuardPerception.SenseSound(guard, source, standard), Is.Null);
            Assert.That(GuardPerception.SenseSound(guard, source, observant), Is.Not.Null);
            Assert.That(GuardPerception.SenseSound(guard, new SoundSource(new Vector2(5f, 0f), 3f), standard), Is.Not.Null);
        }

        [Test]
        public void SoundMemoryExpiresAfterProfileLimit()
        {
            var profile = NewProfile("standard");
            var guard = new Vector2(0f, 0f);

            var atLimit = GuardPerception.SenseSound(
                guard,
                new SoundSource(new Vector2(1f, 0f), 3f, profile.sound_memory_ticks),
                profile);
            var pastLimit = GuardPerception.SenseSound(
                guard,
                new SoundSource(new Vector2(1f, 0f), 3f, profile.sound_memory_ticks + 1),
                profile);

            Assert.That(atLimit, Is.Not.Null);
            Assert.That(pastLimit, Is.Null);
        }

        [Test]
        public void AlertRisesOnVisibleAndClampsToOne()
        {
            var profile = NewProfile("standard");
            var memory = new PerceptionMemory();
            var guard = new Vector2(4f, 4f);
            var player = new Vector2(5f, 4f);

            var first = GuardPerception.SamplePercepts(1, 1f / 60f, guard, 0f, player, memory, profile);
            Assert.That(first.Percepts.PlayerVisible, Is.True);
            Assert.That(first.Memory.Alert, Is.GreaterThan(memory.Alert));

            memory = first.Memory;
            for (var tick = 2; tick <= 240; tick++)
            {
                var sample = GuardPerception.SamplePercepts(tick, 1f / 60f, guard, 0f, player, memory, profile);
                memory = sample.Memory;
            }

            Assert.That(memory.Alert, Is.EqualTo(1f));
        }

        [Test]
        public void AlertDecaysWhenUnseen()
        {
            var profile = NewProfile("standard");
            var guard = new Vector2(1f, 1f);
            var player = new Vector2(1f, 8f);
            var memory = new PerceptionMemory(alert: 0.9f);

            var first = GuardPerception.SamplePercepts(1, 1f, guard, 0f, player, memory, profile);
            Assert.That(first.Percepts.PlayerVisible, Is.False);
            Assert.That(first.Memory.Alert, Is.EqualTo(0.76f).Within(0.0001f));
            Assert.That(first.Memory.LostSightTicks, Is.EqualTo(1));

            memory = first.Memory;
            for (var tick = 2; tick <= 40; tick++)
            {
                var sample = GuardPerception.SamplePercepts(tick, 1f, guard, 0f, player, memory, profile);
                memory = sample.Memory;
            }

            Assert.That(memory.Alert, Is.EqualTo(0.08f));
        }

        [Test]
        public void SoundRaisesAlertFloorWhenUnseen()
        {
            var profile = NewProfile("standard");
            var guard = new Vector2(1f, 1f);
            var player = new Vector2(8f, 8f);
            var memory = new PerceptionMemory(alert: 0.10f);
            var sound = new SoundSource(new Vector2(2f, 1f), 2f);

            var (percepts, updated) = GuardPerception.SamplePercepts(
                1,
                1f,
                guard,
                0f,
                player,
                memory,
                profile,
                soundSource: sound);

            Assert.That(percepts.PlayerVisible, Is.False);
            Assert.That(percepts.Sound, Is.Not.Null);
            Assert.That(updated.LastSeenPos, Is.EqualTo(new Vector2(2f, 1f)));
            Assert.That(updated.Alert, Is.GreaterThanOrEqualTo(0.42f));
            Assert.That(updated.Alert, Is.LessThanOrEqualTo(0.85f));
        }

        [Test]
        public void SearchTicksOnlyAdvanceWhileSearching()
        {
            var profile = NewProfile("standard");
            var guard = new Vector2(1f, 1f);
            var player = new Vector2(8f, 8f);
            var memory = new PerceptionMemory(alert: 0.5f, lostSightTicks: 3, searchTicks: 7);

            var patrol = GuardPerception.SamplePercepts(1, 1f / 60f, guard, 0f, player, memory, profile, currentState: "patrol");
            var search = GuardPerception.SamplePercepts(2, 1f / 60f, guard, 0f, player, memory, profile, currentState: "search");

            Assert.That(patrol.Memory.SearchTicks, Is.EqualTo(0));
            Assert.That(search.Memory.SearchTicks, Is.EqualTo(8));
            Assert.That(search.Memory.LostSightTicks, Is.EqualTo(4));
        }

        [Test]
        public void RouteStartAndArrivalFlagsUsePythonRadii()
        {
            var profile = NewProfile("standard");
            var memory = new PerceptionMemory();
            var guard = new Vector2(13.5f, 5.3f);

            var atStart = GuardPerception.SamplePercepts(1, 1f / 60f, guard, 0f, new Vector2(20f, 20f), memory, profile);
            var notAtStart = GuardPerception.SamplePercepts(1, 1f / 60f, new Vector2(13.5f, 5.4f), 0f, new Vector2(20f, 20f), memory, profile);
            var arrived = GuardPerception.SamplePercepts(
                1,
                1f / 60f,
                new Vector2(10f, 10f),
                0f,
                new Vector2(20f, 20f),
                memory,
                profile,
                targetPos: new Vector2(10.79f, 10f));

            Assert.That(atStart.Percepts.AtRouteStart, Is.True);
            Assert.That(notAtStart.Percepts.AtRouteStart, Is.False);
            Assert.That(arrived.Percepts.ArrivedAtTarget, Is.True);
        }

        private GuardProfile NewProfile(string id)
        {
            var profile = GuardProfile.CreateDefault(id);
            _profiles.Add(profile);
            return profile;
        }
    }
}
