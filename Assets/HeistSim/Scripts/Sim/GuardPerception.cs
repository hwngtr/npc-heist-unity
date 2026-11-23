using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Axis-aligned obstacle or wall box that blocks line of sight.</summary>
    public readonly struct ObstacleBox
    {
        /// <summary>Creates a box from opposite corners.</summary>
        public ObstacleBox(float x0, float y0, float x1, float y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        /// <summary>Minimum x of the box.</summary>
        public float X0 { get; }

        /// <summary>Minimum y of the box.</summary>
        public float Y0 { get; }

        /// <summary>Maximum x of the box.</summary>
        public float X1 { get; }

        /// <summary>Maximum y of the box.</summary>
        public float Y1 { get; }

        /// <summary>Returns true if the sampled segment passes through this box.</summary>
        public bool IntersectsSegment(Vector2 a, Vector2 b, int samples = 16)
        {
            for (var step = 1; step < samples; step++)
            {
                var u = (float)step / samples;
                var px = a.x + (b.x - a.x) * u;
                var py = a.y + (b.y - a.y) * u;
                if (X0 <= px && px <= X1 && Y0 <= py && py <= Y1)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>World-emitted sound wave before guard auditory filtering.</summary>
    public readonly struct SoundSource
    {
        /// <summary>Creates a sound source at a world position.</summary>
        public SoundSource(Vector2 origin, float radius, int ageTicks = 0, float intensity = 1f)
        {
            Origin = origin;
            Radius = radius;
            AgeTicks = ageTicks;
            Intensity = intensity;
        }

        /// <summary>World position of the sound.</summary>
        public Vector2 Origin { get; }

        /// <summary>Emitted hearing radius in tiles.</summary>
        public float Radius { get; }

        /// <summary>Age of the sound in ticks.</summary>
        public int AgeTicks { get; }

        /// <summary>Emitted loudness.</summary>
        public float Intensity { get; }
    }

    /// <summary>Persistent sensor memory carried by a guard across ticks.</summary>
    public readonly struct PerceptionMemory
    {
        /// <summary>Creates sensor memory.</summary>
        public PerceptionMemory(float alert = 0.12f, Vector2? lastSeenPos = null, int lostSightTicks = 0, int searchTicks = 0)
        {
            Alert = alert;
            LastSeenPos = lastSeenPos;
            LostSightTicks = lostSightTicks;
            SearchTicks = searchTicks;
        }

        /// <summary>Current alert level.</summary>
        public float Alert { get; }

        /// <summary>Last known player position.</summary>
        public Vector2? LastSeenPos { get; }

        /// <summary>Ticks since the player was last seen.</summary>
        public int LostSightTicks { get; }

        /// <summary>Ticks spent searching.</summary>
        public int SearchTicks { get; }
    }

    /// <summary>Field of view, occlusion, hearing, and alert dynamics for guards.</summary>
    public static class GuardPerception
    {
        /// <summary>Side length of one room grid cell in tiles.</summary>
        public const float RoomSize = 9f;

        /// <summary>Euclidean distance between two points.</summary>
        public static float Distance(Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Wraps an angle in radians into the range minus pi to pi.</summary>
        public static float NormalizeAngle(float radians)
        {
            return PositiveModulo(radians + MathF.PI, 2f * MathF.PI) - MathF.PI;
        }

        /// <summary>Returns the signed bearing from guard facing to a target.</summary>
        public static float RelativeBearing(Vector2 guardPos, float guardFacing, Vector2 targetPos)
        {
            var dx = targetPos.x - guardPos.x;
            var dy = targetPos.y - guardPos.y;
            var worldAngle = MathF.Atan2(dy, dx);
            return NormalizeAngle(worldAngle - guardFacing);
        }

        /// <summary>Checks obstacle boxes and the nine by nine room and doorway grid for line of sight.</summary>
        public static bool HasRoomLineOfSight(
            Vector2 guardPos,
            Vector2 targetPos,
            IReadOnlyList<ObstacleBox> obstacles = null,
            bool roomGridEnabled = true)
        {
            if (obstacles != null)
            {
                foreach (var box in obstacles)
                {
                    if (box.IntersectsSegment(guardPos, targetPos))
                    {
                        return false;
                    }
                }
            }

            if (!roomGridEnabled)
            {
                return true;
            }

            var gxRoom = MathF.Floor(guardPos.x / RoomSize);
            var gyRoom = MathF.Floor(guardPos.y / RoomSize);
            var txRoom = MathF.Floor(targetPos.x / RoomSize);
            var tyRoom = MathF.Floor(targetPos.y / RoomSize);
            if (gxRoom == txRoom && gyRoom == tyRoom)
            {
                return true;
            }

            if (MathF.Abs(gxRoom - txRoom) + MathF.Abs(gyRoom - tyRoom) != 1f)
            {
                return false;
            }

            var localGx = PositiveModulo(guardPos.x, RoomSize);
            var localGy = PositiveModulo(guardPos.y, RoomSize);
            var localTx = PositiveModulo(targetPos.x, RoomSize);
            var localTy = PositiveModulo(targetPos.y, RoomSize);
            if (gxRoom != txRoom)
            {
                return InBand(localGy) && InBand(localTy);
            }

            return InBand(localGx) && InBand(localTx);
        }

        /// <summary>Evaluates distance, bearing, and visibility for a target.</summary>
        public static (bool Visible, float Distance, float RelAngle) InFovCone(
            Vector2 guardPos,
            float guardFacing,
            Vector2 targetPos,
            GuardProfile profile,
            IReadOnlyList<ObstacleBox> obstacles = null,
            bool roomGridEnabled = true)
        {
            var dist = Distance(guardPos, targetPos);
            var relAngle = RelativeBearing(guardPos, guardFacing, targetPos);
            var withinCone = dist <= profile.fov_distance && MathF.Abs(relAngle) <= profile.fov_half_angle;
            var withinProximity = dist <= profile.close_proximity;
            if (!(withinCone || withinProximity))
            {
                return (false, dist, relAngle);
            }

            var los = HasRoomLineOfSight(guardPos, targetPos, obstacles, roomGridEnabled);
            return (los, dist, relAngle);
        }

        /// <summary>Filters a world sound source through the guard hearing radius and memory limit.</summary>
        public static SoundEvent? SenseSound(Vector2 guardPos, SoundSource? soundSource, GuardProfile profile)
        {
            if (!soundSource.HasValue)
            {
                return null;
            }

            var source = soundSource.Value;
            if (source.AgeTicks > profile.sound_memory_ticks)
            {
                return null;
            }

            var effectiveRadius = (source.Radius + 2.5f) * profile.hearing_scale;
            var dist = Distance(guardPos, source.Origin);
            if (dist > effectiveRadius)
            {
                return null;
            }

            var attenuation = MathF.Max(0.2f, 1f - (dist / MathF.Max(0.1f, effectiveRadius)));
            return new SoundEvent(
                source.Origin,
                source.AgeTicks,
                (float)Math.Round(source.Intensity * attenuation, 3));
        }

        /// <summary>Samples world state into redacted percepts and updated guard memory.</summary>
        public static (GuardPercepts Percepts, PerceptionMemory Memory) SamplePercepts(
            int tick,
            float dt,
            Vector2 guardPos,
            float guardFacing,
            Vector2 playerPos,
            PerceptionMemory memory,
            GuardProfile profile,
            SoundSource? soundSource = null,
            string currentState = "patrol",
            Vector2? targetPos = null,
            Vector2? routeStart = null,
            IReadOnlyList<ObstacleBox> obstacles = null,
            bool roomGridEnabled = true)
        {
            var (visible, dist, relAngle) = InFovCone(
                guardPos,
                guardFacing,
                playerPos,
                profile,
                obstacles,
                roomGridEnabled);
            var sensedSound = SenseSound(guardPos, soundSource, profile);

            var alert = memory.Alert;
            var lastSeen = memory.LastSeenPos;
            var lostSightTicks = memory.LostSightTicks;
            var searchTicks = memory.SearchTicks;

            if (visible)
            {
                lastSeen = playerPos;
                lostSightTicks = 0;
                searchTicks = 0;
                var proximityBoost = dist <= profile.close_proximity * 1.5f ? 1.35f : 1f;
                alert = MathF.Min(1f, MathF.Max(alert, profile.alert_detect_threshold) + dt * profile.alert_gain_rate * proximityBoost);
            }
            else
            {
                lostSightTicks += 1;
                if (currentState == "search")
                {
                    searchTicks += 1;
                }
                else
                {
                    searchTicks = 0;
                }

                if (sensedSound.HasValue)
                {
                    lastSeen = sensedSound.Value.Origin;
                    var soundFloor = MathF.Min(0.85f, MathF.Max(0.42f, profile.alert_detect_threshold + 0.22f * sensedSound.Value.Intensity));
                    alert = MathF.Max(alert, soundFloor);
                }
                else
                {
                    alert = MathF.Max(0.08f, alert - dt * profile.alert_decay_rate);
                }
            }

            var arrivedAtTarget = targetPos.HasValue && Distance(guardPos, targetPos.Value) <= 0.80f;
            var atRouteStart = Distance(guardPos, routeStart ?? new Vector2(13.5f, 4.5f)) <= 0.85f;

            var newMemory = new PerceptionMemory(
                (float)Math.Round(alert, 4),
                lastSeen,
                lostSightTicks,
                searchTicks);

            var percepts = new GuardPercepts(
                tick,
                guardPos,
                guardFacing,
                visible,
                visible ? playerPos : null,
                dist,
                relAngle,
                sensedSound,
                newMemory.Alert,
                lastSeen,
                atRouteStart,
                arrivedAtTarget,
                lostSightTicks,
                searchTicks);

            return (percepts, newMemory);
        }

        private static bool InBand(float value)
        {
            return 2.6f < value && value < 6.4f;
        }

        private static float PositiveModulo(float value, float modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }
    }
}
