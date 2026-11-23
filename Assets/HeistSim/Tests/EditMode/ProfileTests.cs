using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Exact numeric parity for the four named guard profiles.</summary>
    public sealed class ProfileTests
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
        public void StandardCarriesPythonValues()
        {
            var p = NewProfile("standard");
            Assert.That(p.id, Is.EqualTo("standard"));
            Assert.That(p.label, Is.EqualTo("Standard"));
            Assert.That(p.summary, Is.EqualTo("Balanced patrol guard (5.4t FOV, 69\u00b0 cone, 1.38t tackle)."));
            Assert.That(p.fov_distance, Is.EqualTo(5.4f));
            Assert.That(p.fov_half_angle, Is.EqualTo(0.60f));
            Assert.That(p.close_proximity, Is.EqualTo(1.25f));
            Assert.That(p.hearing_scale, Is.EqualTo(1.00f));
            Assert.That(p.sound_memory_ticks, Is.EqualTo(30));
            Assert.That(p.attack_range, Is.EqualTo(1.38f));
            Assert.That(p.lost_sight_limit, Is.EqualTo(45));
            Assert.That(p.search_duration_limit, Is.EqualTo(90));
            Assert.That(p.alert_detect_threshold, Is.EqualTo(0.25f));
            Assert.That(p.alert_calm_threshold, Is.EqualTo(0.35f));
            Assert.That(p.alert_gain_rate, Is.EqualTo(0.85f));
            Assert.That(p.alert_decay_rate, Is.EqualTo(0.14f));
            Assert.That(p.patrol_speed, Is.EqualTo(0.82f));
            Assert.That(p.investigate_speed, Is.EqualTo(1.05f));
            Assert.That(p.chase_speed, Is.EqualTo(1.24f));
        }

        [Test]
        public void AggressiveCarriesPythonValues()
        {
            var p = NewProfile("aggressive");
            Assert.That(p.id, Is.EqualTo("aggressive"));
            Assert.That(p.label, Is.EqualTo("Aggressive"));
            Assert.That(p.summary, Is.EqualTo("Fast pursuit, hair-trigger alert (0.15), wide 1.85t tackle, persistent chase."));
            Assert.That(p.fov_distance, Is.EqualTo(5.2f));
            Assert.That(p.fov_half_angle, Is.EqualTo(0.58f));
            Assert.That(p.close_proximity, Is.EqualTo(1.45f));
            Assert.That(p.hearing_scale, Is.EqualTo(1.05f));
            Assert.That(p.sound_memory_ticks, Is.EqualTo(36));
            Assert.That(p.attack_range, Is.EqualTo(1.85f));
            Assert.That(p.lost_sight_limit, Is.EqualTo(65));
            Assert.That(p.search_duration_limit, Is.EqualTo(115));
            Assert.That(p.alert_detect_threshold, Is.EqualTo(0.15f));
            Assert.That(p.alert_calm_threshold, Is.EqualTo(0.25f));
            Assert.That(p.alert_gain_rate, Is.EqualTo(1.20f));
            Assert.That(p.alert_decay_rate, Is.EqualTo(0.09f));
            Assert.That(p.patrol_speed, Is.EqualTo(0.92f));
            Assert.That(p.investigate_speed, Is.EqualTo(1.18f));
            Assert.That(p.chase_speed, Is.EqualTo(1.42f));
        }

        [Test]
        public void CautiousCarriesPythonValues()
        {
            var p = NewProfile("cautious");
            Assert.That(p.id, Is.EqualTo("cautious"));
            Assert.That(p.label, Is.EqualTo("Cautious"));
            Assert.That(p.summary, Is.EqualTo("Verifies noises before committing (0.40 alert gate), shorter chase, careful search."));
            Assert.That(p.fov_distance, Is.EqualTo(4.8f));
            Assert.That(p.fov_half_angle, Is.EqualTo(0.52f));
            Assert.That(p.close_proximity, Is.EqualTo(1.15f));
            Assert.That(p.hearing_scale, Is.EqualTo(1.15f));
            Assert.That(p.sound_memory_ticks, Is.EqualTo(40));
            Assert.That(p.attack_range, Is.EqualTo(1.20f));
            Assert.That(p.lost_sight_limit, Is.EqualTo(32));
            Assert.That(p.search_duration_limit, Is.EqualTo(60));
            Assert.That(p.alert_detect_threshold, Is.EqualTo(0.40f));
            Assert.That(p.alert_calm_threshold, Is.EqualTo(0.40f));
            Assert.That(p.alert_gain_rate, Is.EqualTo(0.65f));
            Assert.That(p.alert_decay_rate, Is.EqualTo(0.20f));
            Assert.That(p.patrol_speed, Is.EqualTo(0.76f));
            Assert.That(p.investigate_speed, Is.EqualTo(0.95f));
            Assert.That(p.chase_speed, Is.EqualTo(1.12f));
        }

        [Test]
        public void ObservantCarriesPythonValues()
        {
            var p = NewProfile("observant");
            Assert.That(p.id, Is.EqualTo("observant"));
            Assert.That(p.label, Is.EqualTo("Observant"));
            Assert.That(p.summary, Is.EqualTo("Long 7.2t eagle-eye vision, wide 96\u00b0 FOV cone, 1.40x acute hearing."));
            Assert.That(p.fov_distance, Is.EqualTo(7.2f));
            Assert.That(p.fov_half_angle, Is.EqualTo(0.84f));
            Assert.That(p.close_proximity, Is.EqualTo(1.60f));
            Assert.That(p.hearing_scale, Is.EqualTo(1.40f));
            Assert.That(p.sound_memory_ticks, Is.EqualTo(45));
            Assert.That(p.attack_range, Is.EqualTo(1.38f));
            Assert.That(p.lost_sight_limit, Is.EqualTo(55));
            Assert.That(p.search_duration_limit, Is.EqualTo(105));
            Assert.That(p.alert_detect_threshold, Is.EqualTo(0.18f));
            Assert.That(p.alert_calm_threshold, Is.EqualTo(0.30f));
            Assert.That(p.alert_gain_rate, Is.EqualTo(1.10f));
            Assert.That(p.alert_decay_rate, Is.EqualTo(0.11f));
            Assert.That(p.patrol_speed, Is.EqualTo(0.84f));
            Assert.That(p.investigate_speed, Is.EqualTo(1.10f));
            Assert.That(p.chase_speed, Is.EqualTo(1.26f));
        }

        [Test]
        public void DefaultPatrolRouteMatchesPython()
        {
            var route = GuardProfile.DefaultPatrolRoute;
            Assert.That(route.Length, Is.EqualTo(5));
            Assert.That(route[0], Is.EqualTo(new Vector2(13.5f, 4.5f)));
            Assert.That(route[1], Is.EqualTo(new Vector2(13.5f, 13.5f)));
            Assert.That(route[2], Is.EqualTo(new Vector2(4.5f, 13.5f)));
            Assert.That(route[3], Is.EqualTo(new Vector2(5.5f, 4.5f)));
            Assert.That(route[4], Is.EqualTo(new Vector2(21.5f, 21.5f)));
        }

        [Test]
        public void UnknownProfileIdThrows()
        {
            Assert.That(() => GuardProfile.CreateDefault("ghost"), Throws.ArgumentException);
        }

        private GuardProfile NewProfile(string id)
        {
            var profile = GuardProfile.CreateDefault(id);
            _profiles.Add(profile);
            return profile;
        }
    }
}
