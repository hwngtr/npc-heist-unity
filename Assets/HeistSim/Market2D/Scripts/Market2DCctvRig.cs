using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Unified single-model 2D CCTV camera rig: couples the floor-anchored mounting stanchion,
    /// 8-direction rotating turret head, optical lens guide rays, and Shadowcast2D floor V-cone
    /// under a single authoritative tile origin and sweep angle.
    /// </summary>
    public sealed class Market2DCctvRig : MonoBehaviour
    {
        /// <summary>Floor-anchored stanchion mount renderer.</summary>
        public SpriteRenderer mountRenderer;

        /// <summary>Rotating 8-direction camera turret head renderer.</summary>
        public SpriteRenderer headRenderer;

        /// <summary>Eight directional camera head sprites ordered CCW in 45 degree Unity screen steps.</summary>
        public Sprite[] headFrames = new Sprite[8];

        /// <summary>Child isometric floor vision cone clipped by Shadowcast2D.</summary>
        public Iso2DVisionCone visionCone;

        /// <summary>Subtle optical beam tether lines linking the physical lens down to the floor V-cone.</summary>
        public LineRenderer beamTether;

        /// <summary>Local vertical offset from the base tile origin to the gimbal lens center in Unity world units.</summary>
        public float lensHeightWorld = 0.56f;

        private bool _lastOnline = true;
        private bool _lastSighting;
        private int _lastDirIdx = -1;

        /// <summary>
        /// Synchronizes turret facing sprite, online/offline visuals, floor V-cone fan, and lens tether rays
        /// directly from the simulation's authoritative camera angle.
        /// </summary>
        public void Sync(
            float originI,
            float originJ,
            float angleRad,
            float halfAngle,
            float range,
            bool online,
            bool sighting,
            NavGrid grid)
        {
            if (visionCone != null)
            {
                visionCone.gameObject.SetActive(online);
            }

            if (beamTether != null)
            {
                beamTether.enabled = online;
            }

            if (online != _lastOnline || sighting != _lastSighting)
            {
                _lastOnline = online;
                _lastSighting = sighting;

                if (headRenderer != null)
                {
                    headRenderer.color = !online
                        ? new Color(0.60f, 0.63f, 0.68f, 1f)
                        : (sighting ? new Color(1f, 0.88f, 0.85f, 1f) : Color.white);
                }

                if (mountRenderer != null)
                {
                    mountRenderer.color = online ? Color.white : new Color(0.68f, 0.70f, 0.74f, 1f);
                }
            }

            // Select the closest of the 8 screen-space directional turret heads from the world-projected aim vector
            if (headRenderer != null && headFrames != null && headFrames.Length >= 8)
            {
                var originWorld = Iso2DMath.TileToWorld(originI, originJ, 0f);
                var tipWorld = Iso2DMath.TileToWorld(originI + Mathf.Cos(angleRad), originJ + Mathf.Sin(angleRad), 0f);
                var dir = tipWorld - originWorld;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    var deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    var dirIdx = ((Mathf.RoundToInt(deg / 45f) % 8) + 8) % 8;
                    if (dirIdx != _lastDirIdx && headFrames[dirIdx] != null)
                    {
                        _lastDirIdx = dirIdx;
                        headRenderer.sprite = headFrames[dirIdx];
                    }
                }
            }

            if (!online || visionCone == null)
            {
                return;
            }

            var fillColor = sighting
                ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.50f)
                : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.28f);
            var outlineColor = sighting
                ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.98f)
                : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.88f);

            visionCone.SetFan(originI, originJ, angleRad, halfAngle, range, fillColor, grid, outlineColor, ignoreOriginWall: true);

            if (beamTether != null)
            {
                beamTether.positionCount = 3;
                beamTether.SetPosition(0, visionCone.LeftRayLocal * 0.42f);
                beamTether.SetPosition(1, new Vector3(0f, lensHeightWorld, 0f));
                beamTether.SetPosition(2, visionCone.RightRayLocal * 0.42f);

                var tetherCol = sighting
                    ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.46f)
                    : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.26f);
                beamTether.startColor = tetherCol;
                beamTether.endColor = tetherCol;
            }
        }
    }
}
