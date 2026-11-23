using System;
using System.Collections.Generic;

namespace HeistSim
{
    /// <summary>Source flags passed to one detection contribution step.</summary>
    public readonly struct DetectionInput
    {
        /// <summary>Creates a detection input snapshot.</summary>
        public DetectionInput(
            bool guardSees = false,
            bool close = false,
            int cameras = 0,
            bool staffReport = false,
            bool sprint = false,
            int tags = 0,
            bool motion = false,
            bool crouched = false,
            bool camerasDisabled = false)
        {
            GuardSees = guardSees;
            Close = close;
            Cameras = cameras;
            StaffReport = staffReport;
            Sprint = sprint;
            Tags = tags;
            Motion = motion;
            Crouched = crouched;
            CamerasDisabled = camerasDisabled;
        }

        /// <summary>Whether a guard has the thief in sight.</summary>
        public bool GuardSees { get; }

        /// <summary>Whether the sighting is at close range.</summary>
        public bool Close { get; }

        /// <summary>Number of cameras that see the thief.</summary>
        public int Cameras { get; }

        /// <summary>Whether staff are reporting the thief.</summary>
        public bool StaffReport { get; }

        /// <summary>Whether the thief is sprinting.</summary>
        public bool Sprint { get; }

        /// <summary>Number of active RF tags in the bag.</summary>
        public int Tags { get; }

        /// <summary>Whether the thief is in an armed motion zone.</summary>
        public bool Motion { get; }

        /// <summary>Whether the thief is crouched.</summary>
        public bool Crouched { get; }

        /// <summary>Whether the cameras are disabled.</summary>
        public bool CamerasDisabled { get; }
    }

    /// <summary>Circular motion sensor zone in tile space.</summary>
    public readonly struct MotionZone
    {
        /// <summary>Creates a motion zone with an optional radius.</summary>
        public MotionZone(float i, float j, float r = 1.6f)
        {
            I = i;
            J = j;
            R = r;
        }

        /// <summary>Zone centre x in tiles.</summary>
        public float I { get; }

        /// <summary>Zone centre y in tiles.</summary>
        public float J { get; }

        /// <summary>Zone radius in tiles.</summary>
        public float R { get; }
    }

    /// <summary>Pure heat model that turns sight, sound and behaviour into a per-tick heat delta.</summary>
    public static class Detection
    {
        /// <summary>Heat per second inside a guard field of view with line of sight.</summary>
        public const float GuardVision = 0.52f;

        /// <summary>Heat per second from a guard at close range.</summary>
        public const float GuardClose = 0.82f;

        /// <summary>Heat per second per camera that sees the thief.</summary>
        public const float Camera = 0.17f;

        /// <summary>Heat per second while staff report the thief.</summary>
        public const float Staff = 0.13f;

        /// <summary>Heat per second while sprinting.</summary>
        public const float Sprint = 0.05f;

        /// <summary>Heat per second per active RF tag.</summary>
        public const float Tag = 0.035f;

        /// <summary>Heat per second while crossing an armed motion zone.</summary>
        public const float Motion = 0.12f;

        /// <summary>One-shot heat added when a loud action fires.</summary>
        public const float SoundSpike = 0.16f;

        /// <summary>Heat decay per second while detected.</summary>
        public const float Decay = 0.07f;

        /// <summary>Heat decay per second while crouched and unseen.</summary>
        public const float DecayHidden = 0.12f;

        /// <summary>Multiplier on vision based sources while crouched.</summary>
        public const float CrouchScale = 0.45f;

        /// <summary>Cameras scale factor when disabled.</summary>
        public const float CamerasDisabledScale = 0.15f;

        /// <summary>Returns the heat delta for one time step.</summary>
        public static float Contribution(DetectionInput ctx, float dt)
        {
            var scale = ctx.Crouched ? CrouchScale : 1f;
            var delta = 0f;
            var detected = false;

            if (ctx.GuardSees)
            {
                var rate = ctx.Close ? GuardClose : GuardVision;
                delta += rate * scale * dt;
                detected = true;
            }

            if (ctx.Cameras > 0)
            {
                var factor = ctx.CamerasDisabled ? CamerasDisabledScale : 1f;
                delta += Camera * ctx.Cameras * factor * scale * dt;
                detected = true;
            }

            if (ctx.StaffReport)
            {
                delta += Staff * scale * dt;
                detected = true;
            }

            if (ctx.Sprint)
            {
                delta += Sprint * dt;
            }

            if (ctx.Tags > 0)
            {
                delta += Tag * ctx.Tags * dt;
            }

            if (ctx.Motion)
            {
                delta += Motion * dt;
                detected = true;
            }

            var decayRate = !detected && ctx.Crouched ? DecayHidden : Decay;
            return delta - decayRate * dt;
        }

        /// <summary>Returns the first motion zone within range of a tile position, or null.</summary>
        public static MotionZone? InMotionZone(IReadOnlyList<MotionZone> zones, float i, float j)
        {
            if (zones == null)
            {
                return null;
            }

            foreach (var zone in zones)
            {
                var dx = i - zone.I;
                var dy = j - zone.J;
                if (Math.Sqrt(dx * dx + dy * dy) <= zone.R)
                {
                    return zone;
                }
            }

            return null;
        }
    }
}
