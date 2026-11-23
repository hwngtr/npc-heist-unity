using UnityEngine;

namespace HeistSim
{
    /// <summary>Auditory stimulus perceived by a guard.</summary>
    public readonly struct SoundEvent
    {
        /// <summary>Creates a sound event at a world position.</summary>
        public SoundEvent(Vector2 origin, int ageTicks, float intensity = 1f)
        {
            Origin = origin;
            AgeTicks = ageTicks;
            Intensity = intensity;
        }

        /// <summary>World position the sound came from.</summary>
        public Vector2 Origin { get; }

        /// <summary>Age of the sound in ticks.</summary>
        public int AgeTicks { get; }

        /// <summary>Relative loudness of the sound.</summary>
        public float Intensity { get; }
    }

    /// <summary>Redacted sensor snapshot passed to a controller each tick.</summary>
    public readonly struct GuardPercepts
    {
        /// <summary>Creates a percepts snapshot.</summary>
        public GuardPercepts(
            int tick,
            Vector2 guardPos,
            float guardFacing,
            bool playerVisible,
            Vector2? playerPos,
            float playerDist,
            float playerAngle,
            SoundEvent? sound,
            float alert,
            Vector2? lastSeenPos,
            bool atRouteStart,
            bool arrivedAtTarget,
            int lostSightTicks,
            int searchTicks,
            int hp = 100)
        {
            Tick = tick;
            GuardPos = guardPos;
            GuardFacing = guardFacing;
            PlayerVisible = playerVisible;
            PlayerPos = playerPos;
            PlayerDist = playerDist;
            PlayerAngle = playerAngle;
            Sound = sound;
            Alert = alert;
            LastSeenPos = lastSeenPos;
            AtRouteStart = atRouteStart;
            ArrivedAtTarget = arrivedAtTarget;
            LostSightTicks = lostSightTicks;
            SearchTicks = searchTicks;
            Hp = hp;
        }

        /// <summary>Simulation tick number.</summary>
        public int Tick { get; }

        /// <summary>Guard position in tiles.</summary>
        public Vector2 GuardPos { get; }

        /// <summary>Guard facing in radians.</summary>
        public float GuardFacing { get; }

        /// <summary>Whether the player is visible this tick.</summary>
        public bool PlayerVisible { get; }

        /// <summary>Player position, null whenever the player is not visible.</summary>
        public Vector2? PlayerPos { get; }

        /// <summary>Distance to the player in tiles.</summary>
        public float PlayerDist { get; }

        /// <summary>Signed bearing to the player in radians.</summary>
        public float PlayerAngle { get; }

        /// <summary>Sensed sound for this tick, if any.</summary>
        public SoundEvent? Sound { get; }

        /// <summary>Current alert level in the range 0 to 1.</summary>
        public float Alert { get; }

        /// <summary>Last known player position, if any.</summary>
        public Vector2? LastSeenPos { get; }

        /// <summary>Whether the guard stands at the route start.</summary>
        public bool AtRouteStart { get; }

        /// <summary>Whether the guard arrived at its current target.</summary>
        public bool ArrivedAtTarget { get; }

        /// <summary>Ticks since the player was last seen.</summary>
        public int LostSightTicks { get; }

        /// <summary>Ticks spent in the search state.</summary>
        public int SearchTicks { get; }

        /// <summary>Guard hit points.</summary>
        public int Hp { get; }
    }
}
