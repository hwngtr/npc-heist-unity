using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>Camera follow modes from SpriteCook Technique 15 plus the StealthComposite default.</summary>
    public enum CameraFollowMode
    {
        StealthComposite = 0,
        Locked = 1,
        Smooth = 2,
        DeadZone = 3,
        Rooms = 4,
        LookAhead = 5,
        PointsOfInterest = 6,
        AimOffset = 7,
        TwoTargets = 8,
    }

    /// <summary>
    /// Frame-rate-independent 2D follow camera implementing all eight SpriteCook camera techniques
    /// (locked, smooth, dead zone, room-by-room smoothstep, velocity look-ahead, points of interest,
    /// aim offset, and two-target dynamic framing) plus a stealth-tuned DeadZone + LookAhead composite.
    /// </summary>
    public sealed class Market2DCameraRig
    {
        /// <summary>Active camera follow mode.</summary>
        public CameraFollowMode Mode { get; set; } = CameraFollowMode.StealthComposite;

        /// <summary>Exponential damping rate per second for Smooth mode (0 snaps immediately).</summary>
        public float SmoothRate { get; set; } = 6.5f;

        /// <summary>Dead-zone half-extents around camera center in world units.</summary>
        public Vector2 DeadZoneHalfSize { get; set; } = new(0.65f, 0.45f);

        /// <summary>Room-by-room smoothstep slide duration in seconds (0.55 s = 33 ticks at 60 fps).</summary>
        public float RoomSlideDuration { get; set; } = 0.55f;

        /// <summary>Maximum look-ahead lead distance in world units.</summary>
        public float LookAheadDistance { get; set; } = 0.85f;

        /// <summary>Look-ahead exponential easing rate per second (3.4 from T15).</summary>
        public float LookAheadRate { get; set; } = 3.4f;

        /// <summary>Points-of-interest outer falloff radius (weight 0).</summary>
        public float PoiOuterRadius { get; set; } = 3.2f;

        /// <summary>Points-of-interest inner full-weight radius (weight 1).</summary>
        public float PoiInnerRadius { get; set; } = 1.2f;

        /// <summary>Points-of-interest pull share (0.6 from T15).</summary>
        public float PoiPullShare { get; set; } = 0.45f;

        /// <summary>Aim-offset cursor pull share (0.45 from T15).</summary>
        public float AimShare { get; set; } = 0.35f;

        /// <summary>Two-target framing padding in world units.</summary>
        public float TwoTargetPadding { get; set; } = 2.6f;

        /// <summary>Current camera center in world XY (before split-viewport offset and shake).</summary>
        public Vector2 Position { get; private set; }

        /// <summary>Current suggested orthographic size (modified by TwoTargets mode).</summary>
        public float OrthoSize { get; private set; } = Market2DRunner.DefaultOrthoSize;

        private Vector2 _lastTargetPos;
        private Vector2 _lead;
        private Vector2 _roomFrom;
        private Vector2 _roomTo;
        private string _lastRoomId;
        private float _roomSlideElapsed;
        private bool _hasSnap;

        /// <summary>Immediately snaps the camera rig to targetWorld without damping.</summary>
        public void Snap(Vector2 targetWorld, float orthoSize, string roomId = null, Vector2? roomCenterWorld = null)
        {
            Position = targetWorld;
            OrthoSize = orthoSize;
            _lastTargetPos = targetWorld;
            _lead = Vector2.zero;
            _lastRoomId = roomId;
            _roomFrom = roomCenterWorld ?? targetWorld;
            _roomTo = _roomFrom;
            _roomSlideElapsed = RoomSlideDuration;
            _hasSnap = true;
        }

        /// <summary>Cycles to the next follow mode and returns its display label.</summary>
        public string CycleMode()
        {
            Mode = (CameraFollowMode)(((int)Mode + 1) % 9);
            return Mode.ToString();
        }

        /// <summary>
        /// Steps the camera rig by dt seconds and returns the updated world XY focus position.
        /// </summary>
        public Vector2 Step(
            Vector2 playerWorld,
            float dt,
            float baseOrthoSize,
            string currentRoomId = null,
            Vector2? currentRoomCenter = null,
            IReadOnlyList<Vector2> pointsOfInterest = null,
            Vector2? aimOffsetFromCenter = null,
            Vector2? secondTargetWorld = null)
        {
            if (!_hasSnap || dt <= 0f)
            {
                Snap(playerWorld, baseOrthoSize, currentRoomId, currentRoomCenter);
                return Position;
            }

            OrthoSize = baseOrthoSize;
            var moved = playerWorld - _lastTargetPos;
            _lastTargetPos = playerWorld;

            switch (Mode)
            {
                case CameraFollowMode.Locked:
                    Position = playerWorld;
                    break;

                case CameraFollowMode.Smooth:
                    Position = Damp(Position, playerWorld, SmoothRate, dt);
                    break;

                case CameraFollowMode.DeadZone:
                    Position = ApplyDeadZone(Position, playerWorld, DeadZoneHalfSize);
                    break;

                case CameraFollowMode.Rooms:
                    var roomCenter = currentRoomCenter ?? playerWorld;
                    if (!string.Equals(currentRoomId, _lastRoomId, StringComparison.Ordinal))
                    {
                        _lastRoomId = currentRoomId;
                        _roomFrom = Position;
                        _roomTo = roomCenter;
                        _roomSlideElapsed = 0f;
                    }

                    _roomSlideElapsed += dt;
                    var u = RoomSlideDuration > 0.001f ? Mathf.Clamp01(_roomSlideElapsed / RoomSlideDuration) : 1f;
                    var s = SmoothStep01(u);
                    Position = Vector2.Lerp(_roomFrom, _roomTo, s);
                    break;

                case CameraFollowMode.LookAhead:
                {
                    var leadGoal = moved.sqrMagnitude > 1e-6f ? moved.normalized * LookAheadDistance : Vector2.zero;
                    _lead = Damp(_lead, leadGoal, LookAheadRate, dt);
                    Position = playerWorld + _lead;
                    break;
                }

                case CameraFollowMode.PointsOfInterest:
                {
                    var goal = playerWorld;
                    if (pointsOfInterest != null && PoiPullShare > 0f && PoiOuterRadius > PoiInnerRadius)
                    {
                        for (var i = 0; i < pointsOfInterest.Count; i++)
                        {
                            var poi = pointsOfInterest[i];
                            var dist = Vector2.Distance(poi, playerWorld);
                            var w = SmoothStepBetween(PoiOuterRadius, PoiInnerRadius, dist);
                            goal += (poi - playerWorld) * (w * PoiPullShare);
                        }
                    }

                    Position = Damp(Position, goal, 3.5f, dt);
                    break;
                }

                case CameraFollowMode.AimOffset:
                {
                    var offset = aimOffsetFromCenter ?? Vector2.zero;
                    var goal = playerWorld + offset * AimShare;
                    Position = Damp(Position, goal, 8.0f, dt);
                    break;
                }

                case CameraFollowMode.TwoTargets:
                {
                    if (secondTargetWorld.HasValue)
                    {
                        var q = secondTargetWorld.Value;
                        var mid = (playerWorld + q) * 0.5f;
                        Position = Damp(Position, mid, 4.5f, dt);
                        var span = Mathf.Max(Mathf.Abs(q.x - playerWorld.x) * 0.55f, Mathf.Abs(q.y - playerWorld.y)) + TwoTargetPadding;
                        var targetOrtho = Mathf.Clamp(span, baseOrthoSize, Market2DRunner.FitOrthoSize);
                        OrthoSize = Mathf.Lerp(OrthoSize, targetOrtho, ExpAlpha(3.5f, dt));
                    }
                    else
                    {
                        Position = Damp(Position, playerWorld, 4.5f, dt);
                    }
                    break;
                }

                case CameraFollowMode.StealthComposite:
                default:
                {
                    var leadGoal = moved.sqrMagnitude > 1e-6f ? moved.normalized * (LookAheadDistance * 0.65f) : Vector2.zero;
                    _lead = Damp(_lead, leadGoal, LookAheadRate, dt);
                    var clamped = ApplyDeadZone(Position, playerWorld, DeadZoneHalfSize * 0.55f);
                    var goal = clamped + _lead;
                    Position = Damp(Position, goal, 8.5f, dt);
                    break;
                }
            }

            return Position;
        }

        /// <summary>Applies rectangular dead-zone clamping around target.</summary>
        public static Vector2 ApplyDeadZone(Vector2 current, Vector2 target, Vector2 halfSize)
        {
            return new Vector2(
                Mathf.Clamp(current.x, target.x - halfSize.x, target.x + halfSize.x),
                Mathf.Clamp(current.y, target.y - halfSize.y, target.y + halfSize.y));
        }

        /// <summary>Frame-rate-independent exponential smoothing factor 1 - exp(-rate * dt). Rate 0 snaps (returns 1).</summary>
        public static float ExpAlpha(float rate, float dt)
        {
            if (rate <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-rate * Mathf.Max(0f, dt));
        }

        /// <summary>Exponentially damps current toward goal at per-second rate.</summary>
        public static Vector2 Damp(Vector2 current, Vector2 goal, float rate, float dt)
        {
            return Vector2.Lerp(current, goal, ExpAlpha(rate, dt));
        }

        /// <summary>Classic cubic smoothstep t * t * (3 - 2 * t).</summary>
        public static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smoothstep mapping value from edge0 (0) to edge1 (1), supporting decreasing edges.</summary>
        public static float SmoothStepBetween(float edge0, float edge1, float value)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return value >= edge1 ? 1f : 0f;
            }

            var t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return SmoothStep01(t);
        }
    }
}
