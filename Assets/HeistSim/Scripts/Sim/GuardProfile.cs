using UnityEngine;

namespace HeistSim
{
    /// <summary>Inspector-editable perception and decision thresholds for a guard personality.</summary>
    [CreateAssetMenu(menuName = "Heist/Guard Profile", fileName = "GuardProfile")]
    public sealed class GuardProfile : ScriptableObject
    {
        /// <summary>Canonical patrol route in tiles, route start first.</summary>
        public static readonly Vector2[] DefaultPatrolRoute =
        {
            new Vector2(13.5f, 4.5f),
            new Vector2(13.5f, 13.5f),
            new Vector2(4.5f, 13.5f),
            new Vector2(5.5f, 4.5f),
            new Vector2(21.5f, 21.5f),
        };

        [Tooltip("Stable profile identifier.")]
        public string id = "standard";

        [Tooltip("Human readable profile name.")]
        public string label = "Standard";

        [Tooltip("One line description of the personality.")]
        public string summary = "Balanced patrol guard (5.4t FOV, 69° cone, 1.38t tackle).";

        [Tooltip("Maximum sight distance in tiles.")]
        public float fov_distance = 5.4f;

        [Tooltip("Half of the field of view cone in radians.")]
        public float fov_half_angle = 0.60f;

        [Tooltip("Radius where the guard notices a target regardless of angle.")]
        public float close_proximity = 1.25f;

        [Tooltip("Multiplier applied to the hearing radius.")]
        public float hearing_scale = 1.00f;

        [Tooltip("Ticks a sound stays in memory.")]
        public int sound_memory_ticks = 30;

        [Tooltip("Distance at which an attack lands, in tiles.")]
        public float attack_range = 1.38f;

        [Tooltip("Ticks without sight before a chase becomes a search.")]
        public int lost_sight_limit = 45;

        [Tooltip("Ticks a search lasts before returning.")]
        public int search_duration_limit = 90;

        [Tooltip("Alert level needed to treat a glimpse as detection.")]
        public float alert_detect_threshold = 0.25f;

        [Tooltip("Alert level at or below which a search may end.")]
        public float alert_calm_threshold = 0.35f;

        [Tooltip("Alert gained per tick while the player is visible.")]
        public float alert_gain_rate = 0.85f;

        [Tooltip("Alert lost per tick while the player is unseen.")]
        public float alert_decay_rate = 0.14f;

        [Tooltip("Patrol movement speed in tiles per tick.")]
        public float patrol_speed = 0.82f;

        [Tooltip("Investigate movement speed in tiles per tick.")]
        public float investigate_speed = 1.05f;

        [Tooltip("Chase movement speed in tiles per tick.")]
        public float chase_speed = 1.24f;

        /// <summary>Creates a profile asset with the values of one of the four named Python profiles.</summary>
        public static GuardProfile CreateDefault(string id)
        {
            var profile = CreateInstance<GuardProfile>();
            profile.name = id;
            switch (id)
            {
                case "standard":
                    profile.id = "standard";
                    profile.label = "Standard";
                    profile.summary = "Balanced patrol guard (5.4t FOV, 69° cone, 1.38t tackle).";
                    profile.fov_distance = 5.4f;
                    profile.fov_half_angle = 0.60f;
                    profile.close_proximity = 1.25f;
                    profile.hearing_scale = 1.00f;
                    profile.sound_memory_ticks = 30;
                    profile.attack_range = 1.38f;
                    profile.lost_sight_limit = 45;
                    profile.search_duration_limit = 90;
                    profile.alert_detect_threshold = 0.25f;
                    profile.alert_calm_threshold = 0.35f;
                    profile.alert_gain_rate = 0.85f;
                    profile.alert_decay_rate = 0.14f;
                    profile.patrol_speed = 0.82f;
                    profile.investigate_speed = 1.05f;
                    profile.chase_speed = 1.24f;
                    return profile;
                case "aggressive":
                    profile.id = "aggressive";
                    profile.label = "Aggressive";
                    profile.summary = "Fast pursuit, hair-trigger alert (0.15), wide 1.85t tackle, persistent chase.";
                    profile.fov_distance = 5.2f;
                    profile.fov_half_angle = 0.58f;
                    profile.close_proximity = 1.45f;
                    profile.hearing_scale = 1.05f;
                    profile.sound_memory_ticks = 36;
                    profile.attack_range = 1.85f;
                    profile.lost_sight_limit = 65;
                    profile.search_duration_limit = 115;
                    profile.alert_detect_threshold = 0.15f;
                    profile.alert_calm_threshold = 0.25f;
                    profile.alert_gain_rate = 1.20f;
                    profile.alert_decay_rate = 0.09f;
                    profile.patrol_speed = 0.92f;
                    profile.investigate_speed = 1.18f;
                    profile.chase_speed = 1.42f;
                    return profile;
                case "cautious":
                    profile.id = "cautious";
                    profile.label = "Cautious";
                    profile.summary = "Verifies noises before committing (0.40 alert gate), shorter chase, careful search.";
                    profile.fov_distance = 4.8f;
                    profile.fov_half_angle = 0.52f;
                    profile.close_proximity = 1.15f;
                    profile.hearing_scale = 1.15f;
                    profile.sound_memory_ticks = 40;
                    profile.attack_range = 1.20f;
                    profile.lost_sight_limit = 32;
                    profile.search_duration_limit = 60;
                    profile.alert_detect_threshold = 0.40f;
                    profile.alert_calm_threshold = 0.40f;
                    profile.alert_gain_rate = 0.65f;
                    profile.alert_decay_rate = 0.20f;
                    profile.patrol_speed = 0.76f;
                    profile.investigate_speed = 0.95f;
                    profile.chase_speed = 1.12f;
                    return profile;
                case "observant":
                    profile.id = "observant";
                    profile.label = "Observant";
                    profile.summary = "Long 7.2t eagle-eye vision, wide 96° FOV cone, 1.40x acute hearing.";
                    profile.fov_distance = 7.2f;
                    profile.fov_half_angle = 0.84f;
                    profile.close_proximity = 1.60f;
                    profile.hearing_scale = 1.40f;
                    profile.sound_memory_ticks = 45;
                    profile.attack_range = 1.38f;
                    profile.lost_sight_limit = 55;
                    profile.search_duration_limit = 105;
                    profile.alert_detect_threshold = 0.18f;
                    profile.alert_calm_threshold = 0.30f;
                    profile.alert_gain_rate = 1.10f;
                    profile.alert_decay_rate = 0.11f;
                    profile.patrol_speed = 0.84f;
                    profile.investigate_speed = 1.10f;
                    profile.chase_speed = 1.26f;
                    return profile;
                default:
                    DestroyImmediate(profile);
                    throw new System.ArgumentException($"Unknown guard profile id: {id}", nameof(id));
            }
        }
    }
}
