using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Tactical role assigned by the squad director to coordinate multiple security guards.</summary>
    public enum GuardTacticalRole
    {
        /// <summary>Standard department patrol beat.</summary>
        Patrol,
        /// <summary>Direct pursuit of a spotted thief.</summary>
        PrimaryPursuer,
        /// <summary>Cut off or hold the EAS exit corridor during alerts or alarms.</summary>
        ExitInterceptor,
        /// <summary>Investigate physical sabotage such as cut CCTV power or forced vitrine.</summary>
        SabotageResponder,
        /// <summary>Sweep non-overlapping occupancy probability peaks across aisles.</summary>
        SectorSweeper,
    }

    /// <summary>
    /// Shared squad tactical blackboard and zero-allocation 2D occupancy probability grid.
    /// Tracks suspect probability waves across walkable NavGrid tiles, clears tiles seen by
    /// guards and online CCTV cones, and assigns complementary roles across up to two guards.
    /// </summary>
    public sealed class GuardSquadBlackboard
    {
        /// <summary>Ticks after last sighting during which the squad maintains coordinated intercept roles over radio.</summary>
        public const int RecentContactWindowTicks = 75;

        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        private readonly NavGrid _grid;
        private readonly bool[] _walkable;
        private readonly byte[] _neighborMask;
        private readonly float[] _confidence;
        private readonly float[] _connectivity;
        private readonly int[] _strategicChokeCells;
        private readonly Vector2[] _cornerSpots;
        private readonly int[] _cornerLastSeenTick;
        private readonly int[] _bfsDistTarget;
        private readonly int[] _bfsDistGuard;
        private readonly int[] _bfsQueue;
        private float[] _prob;
        private float[] _probScratch;
        private int _lastPursuerGuardId = -1;
        private uint _initialSeed = 0x51A7E123u;
        private uint _rngState = 0x51A7E123u;

        /// <summary>Creates a squad blackboard and caches walkability, connectivity, and topological choke maps.</summary>
        public GuardSquadBlackboard(NavGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Width = grid.Width;
            Height = grid.Height;

            var count = Width * Height;
            _walkable = new bool[count];
            _neighborMask = new byte[count];
            _confidence = new float[count];
            _connectivity = new float[count];
            _prob = new float[count];
            _probScratch = new float[count];
            _bfsDistTarget = new int[count];
            _bfsDistGuard = new int[count];
            _bfsQueue = new int[count];
            var chokes = new List<int>(64);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var idx = y * Width + x;
                    var walk = grid.IsWalkable(x, y);
                    _walkable[idx] = walk;
                    if (!walk)
                    {
                        continue;
                    }

                    byte mask = 0;
                    for (var d = 0; d < 4; d++)
                    {
                        var nx = x + Dx[d];
                        var ny = y + Dy[d];
                        if (grid.CanStep(x, y, nx, ny))
                        {
                            mask |= (byte)(1 << d);
                        }
                    }

                    _neighborMask[idx] = mask;
                    var lx = x % MarketData.RoomTiles;
                    var ly = y % MarketData.RoomTiles;
                    var isDoorAdj = ((lx == 0 || lx == MarketData.RoomTiles - 1) && ly >= 3 && ly <= 5)
                        || ((ly == 0 || ly == MarketData.RoomTiles - 1) && lx >= 3 && lx <= 5);
                    var isHub = lx >= 3 && lx <= 5 && ly >= 3 && ly <= 5;
                    var conn = (CountBits(mask) * 0.15f) + (isDoorAdj ? 0.45f : 0f) + (isHub ? 0.25f : 0f);
                    _connectivity[idx] = conn;
                    if (isDoorAdj || isHub)
                    {
                        chokes.Add(idx);
                    }
                }
            }

            _strategicChokeCells = chokes.ToArray();
            _cornerSpots = BuildCornerSpots();
            _cornerLastSeenTick = new int[_cornerSpots.Length];
            Reset();
        }

        /// <summary>Grid width in tiles.</summary>
        public int Width { get; }

        /// <summary>Grid height in tiles.</summary>
        public int Height { get; }

        /// <summary>Whether the CCTV power grid has been cut.</summary>
        public bool CamerasDisabled { get; set; }

        /// <summary>Whether the spirits vitrine has been forced open.</summary>
        public bool VitrineOpen { get; set; }

        /// <summary>Whether the EAS exit alarm is currently active.</summary>
        public bool EasAlarmActive { get; set; }

        /// <summary>Whether a guard has already inspected the disabled CCTV panel.</summary>
        public bool PanelInspected { get; private set; }

        /// <summary>Whether a guard has already inspected the forced spirits vitrine.</summary>
        public bool VitrineInspected { get; private set; }

        /// <summary>True when a guard finished rebooting the CCTV panel and the sim should bring cameras back online.</summary>
        public bool CamerasRestoreRequested { get; private set; }

        /// <summary>True while a security guard is actively working on rebooting the disabled CCTV panel.</summary>
        public bool CctvRestoreInProgress { get; set; }

        /// <summary>Normalized progress [0, 1] of an active guard CCTV panel reboot.</summary>
        public float CctvRestoreProgress { get; set; }

        /// <summary>Current normalized store suspicion in [0, 1].</summary>
        public float Heat { get; set; }

        /// <summary>Current alert tier index.</summary>
        public int HeatTier { get; set; }

        /// <summary>Last confirmed thief position reported by any sensor.</summary>
        public Vector2? LastConfirmedPos { get; private set; }

        /// <summary>Smoothed velocity vector of the thief when last spotted by any sensor.</summary>
        public Vector2 LastConfirmedVelocity { get; private set; }

        /// <summary>Simulation tick of the last confirmed sighting.</summary>
        public int LastConfirmedTick { get; private set; }

        /// <summary>True when at least one active guard currently has direct line-of-sight to the thief.</summary>
        public bool AnyGuardSeesPlayer { get; private set; }

        /// <summary>Index of the guard closest to the spotted player, or -1 when no guard sees them.</summary>
        public int SpotterGuardId { get; private set; } = -1;

        /// <summary>Highest probability density currently in the occupancy grid.</summary>
        public float MaxProbability { get; private set; }

        /// <summary>Tile center with the highest suspect probability, or null when the grid is flat.</summary>
        public Vector2? PeakProbabilityPos { get; private set; }

        /// <summary>Latest tile positions for Guard 0 and Guard 1.</summary>
        public Vector2[] GuardPositions { get; } = new Vector2[2];

        /// <summary>Latest discrete actions for Guard 0 and Guard 1.</summary>
        public GuardAction[] GuardActions { get; } = new GuardAction[2];

        /// <summary>Current tactical role assigned to Guard 0 and Guard 1.</summary>
        public GuardTacticalRole[] AssignedRoles { get; } = new GuardTacticalRole[2];

        /// <summary>Active claimed search tile per guard so two guards do not stack on the same spot.</summary>
        public Vector2?[] ClaimedTargets { get; } = new Vector2?[2];

        /// <summary>Number of tracked blind corner inspection spots across all rooms.</summary>
        public int CornerSpotCount => _cornerSpots.Length;

        /// <summary>Seeds the deterministic PRNG used for randomized corner-check weighting.</summary>
        public void Reseed(uint seed)
        {
            _initialSeed = seed == 0u ? 0x51A7E123u : seed;
            _rngState = _initialSeed;
        }

        /// <summary>Returns a seeded pseudo-random integer in [minInclusive, maxInclusive].</summary>
        public int NextIntRange(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            var span = (uint)(maxInclusive - minInclusive + 1);
            return minInclusive + (int)(NextUInt32() % span);
        }

        /// <summary>Clears all occupancy probabilities, sabotage flags, corner staleness, and squad assignments.</summary>
        public void Reset()
        {
            Array.Clear(_prob, 0, _prob.Length);
            Array.Clear(_probScratch, 0, _probScratch.Length);
            if (_confidence != null)
            {
                Array.Clear(_confidence, 0, _confidence.Length);
            }

            if (_cornerLastSeenTick != null)
            {
                Array.Fill(_cornerLastSeenTick, -9999);
            }

            _rngState = _initialSeed;
            CamerasDisabled = false;
            VitrineOpen = false;
            EasAlarmActive = false;
            PanelInspected = false;
            VitrineInspected = false;
            CamerasRestoreRequested = false;
            CctvRestoreInProgress = false;
            CctvRestoreProgress = 0f;
            Heat = 0.12f;
            HeatTier = 1;
            LastConfirmedPos = null;
            LastConfirmedVelocity = Vector2.zero;
            LastConfirmedTick = -9999;
            AnyGuardSeesPlayer = false;
            SpotterGuardId = -1;
            _lastPursuerGuardId = -1;
            MaxProbability = 0f;
            PeakProbabilityPos = null;

            for (var i = 0; i < 2; i++)
            {
                GuardPositions[i] = default;
                GuardActions[i] = GuardAction.Patrol;
                AssignedRoles[i] = GuardTacticalRole.Patrol;
                ClaimedTargets[i] = null;
            }
        }

        /// <summary>Marks the disabled CCTV power panel as inspected by security.</summary>
        public void MarkPanelInspected()
        {
            PanelInspected = true;
        }

        /// <summary>Marks the forced spirits vitrine as inspected by security.</summary>
        public void MarkVitrineInspected()
        {
            VitrineInspected = true;
        }

        /// <summary>Records that a guard completed rebooting the CCTV breaker panel.</summary>
        public void RequestCamerasRestore()
        {
            CamerasRestoreRequested = true;
            CamerasDisabled = false;
            PanelInspected = false;
            CctvRestoreInProgress = false;
            CctvRestoreProgress = 0f;
        }

        /// <summary>Consumes and clears a pending CCTV restoration request.</summary>
        public bool ConsumeCamerasRestoreRequest()
        {
            if (!CamerasRestoreRequested)
            {
                return false;
            }

            CamerasRestoreRequested = false;
            return true;
        }

        /// <summary>True when any sensor confirmed the thief's position within the specified tick window.</summary>
        public bool HasRecentSighting(int tick, int windowTicks = RecentContactWindowTicks)
        {
            return LastConfirmedPos.HasValue && (tick - LastConfirmedTick) <= windowTicks;
        }

        /// <summary>Returns the suspect probability density at integer tile coordinates.</summary>
        public float ProbabilityAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return 0f;
            }

            return _prob[y * Width + x];
        }

        /// <summary>Seeds a fresh confirmed sighting at a world tile position, resetting stale probability mass.</summary>
        public void SeedSighting(Vector2 pos, int tick)
        {
            SeedSighting(pos, Vector2.zero, tick);
        }

        /// <summary>
        /// Seeds a confirmed sighting with thief velocity so probabilistic diffusion biases forward
        /// along their escape heading and clears confidence around the newly observed area.
        /// </summary>
        public void SeedSighting(Vector2 pos, Vector2 velocity, int tick)
        {
            Array.Clear(_prob, 0, _prob.Length);
            for (var i = 0; i < _confidence.Length; i++)
            {
                _confidence[i] *= 0.25f;
            }

            var cell = ResolveWalkableCell(pos);
            if (cell.HasValue)
            {
                var c = cell.Value;
                var idx = c.Y * Width + c.X;
                _prob[idx] = 1.0f;
                _confidence[idx] = 0f;

                var mask = _neighborMask[idx];
                for (var d = 0; d < 4; d++)
                {
                    if ((mask & (1 << d)) == 0)
                    {
                        continue;
                    }

                    var nIdx = (c.Y + Dy[d]) * Width + (c.X + Dx[d]);
                    _prob[nIdx] = MathF.Max(_prob[nIdx], 0.45f);
                    _confidence[nIdx] = 0f;
                }

                if (velocity.sqrMagnitude > 0.04f)
                {
                    var velDir = velocity.normalized;
                    var ahead = ResolveWalkableCell(pos + velDir * 1.15f);
                    if (ahead.HasValue)
                    {
                        var aIdx = ahead.Value.Y * Width + ahead.Value.X;
                        _prob[aIdx] = MathF.Max(_prob[aIdx], 0.58f);
                        _confidence[aIdx] = 0f;
                    }
                }

                RecomputePeak();
            }
            else
            {
                MaxProbability = 0f;
                PeakProbabilityPos = null;
            }

            LastConfirmedPos = pos;
            LastConfirmedVelocity = velocity;
            LastConfirmedTick = tick;
        }

        /// <summary>Injects acoustic disturbance likelihood around a walkable sound origin.</summary>
        public void InjectSound(Vector2 origin, float weight = 0.70f)
        {
            var clampedWeight = Math.Clamp(weight, 0f, 1f);
            var cell = ResolveWalkableCell(origin);
            if (!cell.HasValue)
            {
                return;
            }

            var c = cell.Value;
            var idx = c.Y * Width + c.X;
            _prob[idx] = MathF.Max(_prob[idx], clampedWeight);

            var mask = _neighborMask[idx];
            var neighborWeight = clampedWeight * 0.5f;
            for (var d = 0; d < 4; d++)
            {
                if ((mask & (1 << d)) == 0)
                {
                    continue;
                }

                var nIdx = (c.Y + Dy[d]) * Width + (c.X + Dx[d]);
                _prob[nIdx] = MathF.Max(_prob[nIdx], neighborWeight);
            }

            RecomputePeak();
        }

        /// <summary>Zeroes occupancy tiles visible inside a guard or camera vision cone.</summary>
        public void ClearVisionCone(
            Vector2 origin,
            float angle,
            float halfAngle,
            float range,
            Func<Vector2, Vector2, bool> hasLos)
        {
            if (MaxProbability <= 0.002f || range <= 0f)
            {
                return;
            }

            var minX = Math.Max(0, (int)MathF.Floor(origin.x - range));
            var maxX = Math.Min(Width - 1, (int)MathF.Ceiling(origin.x + range));
            var minY = Math.Max(0, (int)MathF.Floor(origin.y - range));
            var maxY = Math.Min(Height - 1, (int)MathF.Ceiling(origin.y + range));
            var rangeSq = range * range;
            var clearedAny = false;

            for (var y = minY; y <= maxY; y++)
            {
                var cy = y + 0.5f;
                var dy = cy - origin.y;
                var dySq = dy * dy;
                var rowOffset = y * Width;

                for (var x = minX; x <= maxX; x++)
                {
                    var idx = rowOffset + x;
                    if (_prob[idx] <= 0f)
                    {
                        continue;
                    }

                    var cx = x + 0.5f;
                    var dx = cx - origin.x;
                    if (dx * dx + dySq > rangeSq)
                    {
                        continue;
                    }

                    var rel = GuardPerception.NormalizeAngle(MathF.Atan2(dy, dx) - angle);
                    if (MathF.Abs(rel) > halfAngle)
                    {
                        continue;
                    }

                    if (hasLos != null && !hasLos(origin, new Vector2(cx, cy)))
                    {
                        continue;
                    }

                    _prob[idx] = 0f;
                    _confidence[idx] = 1.0f;
                    clearedAny = true;
                }
            }

            if (clearedAny)
            {
                RecomputePeak();
            }
        }

        /// <summary>
        /// Diffuses suspect probability across doorway-connected tiles with Isla directional momentum
        /// along LastConfirmedVelocity, while decaying cleared-sector Confidence (arXiv:2508.18527).
        /// </summary>
        public void DiffuseStep(float spreadRate = 0.24f, float decay = 0.992f, int tick = -1)
        {
            if (MaxProbability <= 0.005f)
            {
                return;
            }

            var spread = Math.Clamp(spreadRate, 0f, 1f);
            var retain = 1f - spread;
            Array.Clear(_probScratch, 0, _probScratch.Length);

            var hasMomentum = LastConfirmedVelocity.sqrMagnitude > 0.04f
                && (tick < 0 || tick - LastConfirmedTick <= 150);
            var velDir = hasMomentum ? LastConfirmedVelocity.normalized : Vector2.zero;
            var momentumFade = hasMomentum
                ? (tick < 0 ? 1.0f : Math.Clamp(1f - (tick - LastConfirmedTick) / 150f, 0.15f, 1f))
                : 0f;

            for (var y = 0; y < Height; y++)
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var idx = rowOffset + x;
                    var p = _prob[idx];
                    if (!_walkable[idx] || p <= 0.0005f)
                    {
                        continue;
                    }

                    var mask = _neighborMask[idx];
                    if (mask == 0)
                    {
                        _probScratch[idx] += p;
                        continue;
                    }

                    _probScratch[idx] += p * retain;
                    var totalW = 0f;
                    float w0 = 0f, w1 = 0f, w2 = 0f, w3 = 0f;

                    for (var d = 0; d < 4; d++)
                    {
                        if ((mask & (1 << d)) == 0)
                        {
                            continue;
                        }

                        var w = hasMomentum
                            ? MathF.Max(0.25f, 1f + 1.45f * momentumFade * (Dx[d] * velDir.x + Dy[d] * velDir.y))
                            : 1f;
                        totalW += w;
                        if (d == 0) w0 = w;
                        else if (d == 1) w1 = w;
                        else if (d == 2) w2 = w;
                        else w3 = w;
                    }

                    if (totalW <= 0f)
                    {
                        _probScratch[idx] += p * spread;
                        continue;
                    }

                    var norm = p * (spread / totalW);
                    if (w0 > 0f) _probScratch[idx + 1] += norm * w0;
                    if (w1 > 0f) _probScratch[idx - 1] += norm * w1;
                    if (w2 > 0f) _probScratch[idx + Width] += norm * w2;
                    if (w3 > 0f) _probScratch[idx - Width] += norm * w3;
                }
            }

            var maxP = 0f;
            Vector2? peakPos = null;
            for (var y = 0; y < Height; y++)
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var idx = rowOffset + x;
                    _confidence[idx] *= 0.965f;
                    var v = _probScratch[idx] * decay;
                    if (v < 0.002f)
                    {
                        v = 0f;
                    }

                    _probScratch[idx] = v;
                    if (v > maxP)
                    {
                        maxP = v;
                        peakPos = new Vector2(x + 0.5f, y + 0.5f);
                    }
                }
            }

            (_prob, _probScratch) = (_probScratch, _prob);
            MaxProbability = maxP;
            PeakProbabilityPos = peakPos;
        }

        /// <summary>
        /// Projects up to maxSteps walkable corridor tiles forward from LastConfirmedPos along LastConfirmedVelocity
        /// so pursuing guards anticipate where a fleeing thief exits sight instead of stalling at stale LKPs.
        /// </summary>
        public Vector2 PredictEscapePoint(int maxSteps = 4)
        {
            if (!LastConfirmedPos.HasValue)
            {
                return Vector2.zero;
            }

            var pos = LastConfirmedPos.Value;
            if (LastConfirmedVelocity.sqrMagnitude <= 0.04f)
            {
                return pos;
            }

            var start = ResolveWalkableCell(pos);
            if (!start.HasValue)
            {
                return pos;
            }

            var velDir = LastConfirmedVelocity.normalized;
            var cx = start.Value.X;
            var cy = start.Value.Y;
            var prevX = -1;
            var prevY = -1;

            for (var step = 0; step < maxSteps; step++)
            {
                var idx = cy * Width + cx;
                var mask = _neighborMask[idx];
                var bestD = -1;
                var bestAlign = 0.05f;
                for (var d = 0; d < 4; d++)
                {
                    if ((mask & (1 << d)) == 0)
                    {
                        continue;
                    }

                    var nx = cx + Dx[d];
                    var ny = cy + Dy[d];
                    if (nx == prevX && ny == prevY)
                    {
                        continue;
                    }

                    var nIdx = ny * Width + nx;
                    var align = Dx[d] * velDir.x + Dy[d] * velDir.y + _connectivity[nIdx] * 0.20f;
                    if (align > bestAlign)
                    {
                        bestAlign = align;
                        bestD = d;
                    }
                }

                if (bestD < 0)
                {
                    break;
                }

                prevX = cx;
                prevY = cy;
                cx += Dx[bestD];
                cy += Dy[bestD];
            }

            return new Vector2(cx + 0.5f, cy + 0.5f);
        }

        /// <summary>
        /// Computes a path-aware flanking cutoff waypoint on high-connectivity doorway chokes ahead of targetVel,
        /// favoring room-exit containment opposite the partner pursuer where guard path travel time is feasible.
        /// </summary>
        public Vector2 ComputeFlankInterceptTarget(int guardId, Vector2 targetPos, Vector2 targetVel)
        {
            var id = Math.Clamp(guardId, 0, 1);
            var guardPos = GuardPositions[id];
            var otherPos = GuardPositions[1 - id];
            var easPos = new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J);

            var velDir = targetVel.sqrMagnitude > 0.04f
                ? targetVel.normalized
                : ((easPos - targetPos).sqrMagnitude > 0.04f ? (easPos - targetPos).normalized : Vector2.right);
            var toOther = (otherPos - targetPos).sqrMagnitude > 0.01f
                ? (otherPos - targetPos).normalized
                : -velDir;

            var targetCell = ResolveWalkableCell(targetPos);
            var guardCell = ResolveWalkableCell(guardPos);
            var hasBfs = targetCell.HasValue && guardCell.HasValue;
            if (hasBfs)
            {
                ComputeGridDistances(targetCell.Value.X, targetCell.Value.Y, _bfsDistTarget);
                ComputeGridDistances(guardCell.Value.X, guardCell.Value.Y, _bfsDistGuard);
            }

            var targetRoomX = targetCell.HasValue ? targetCell.Value.X / MarketData.RoomTiles : (int)MathF.Floor(targetPos.x) / MarketData.RoomTiles;
            var targetRoomY = targetCell.HasValue ? targetCell.Value.Y / MarketData.RoomTiles : (int)MathF.Floor(targetPos.y) / MarketData.RoomTiles;

            var bestScore = float.MinValue;
            Vector2? best = null;
            Vector2? anyFallback = null;
            var anyFallbackScore = float.MinValue;

            for (var i = 0; i < _strategicChokeCells.Length; i++)
            {
                var idx = _strategicChokeCells[i];
                var dTargetPath = hasBfs ? _bfsDistTarget[idx] : -1;
                var dGuardPath = hasBfs ? _bfsDistGuard[idx] : -1;
                if (hasBfs && (dTargetPath < 0 || dGuardPath < 0))
                {
                    continue;
                }

                var tx = idx % Width;
                var ty = idx / Width;
                var cy = ty + 0.5f;
                var cx = tx + 0.5f;
                var dx = cx - targetPos.x;
                var dy = cy - targetPos.y;
                var dTarget = MathF.Sqrt(dx * dx + dy * dy);
                if (dTarget < 2.0f)
                {
                    continue;
                }

                var ux = dx / dTarget;
                var uy = dy / dTarget;
                var forward = ux * velDir.x + uy * velDir.y;
                var pincer = -(ux * toOther.x + uy * toOther.y);

                var pathTarget = hasBfs ? (float)dTargetPath : dTarget;
                var gdx = cx - guardPos.x;
                var gdy = cy - guardPos.y;
                var pathGuard = hasBfs ? (float)dGuardPath : MathF.Sqrt(gdx * gdx + gdy * gdy);

                var lx = tx % MarketData.RoomTiles;
                var ly = ty % MarketData.RoomTiles;
                var isDoorAdj = ((lx == 0 || lx == MarketData.RoomTiles - 1) && ly >= 3 && ly <= 5)
                    || ((ly == 0 || ly == MarketData.RoomTiles - 1) && lx >= 3 && lx <= 5);
                var roomDiff = Math.Abs(tx / MarketData.RoomTiles - targetRoomX) + Math.Abs(ty / MarketData.RoomTiles - targetRoomY);
                var containmentBonus = (isDoorAdj && roomDiff <= 1 && pathTarget >= 2f && pathTarget <= 8f && pincer > 0.1f)
                    ? 1.35f
                    : 0f;
                var interceptBonus = pathGuard <= pathTarget * 1.25f
                    ? 1.1f
                    : -0.18f * (pathGuard - pathTarget * 1.25f);

                var score = 2.1f * forward + 1.35f * pincer + containmentBonus + interceptBonus + 0.4f * _connectivity[idx] - 0.05f * pathGuard;

                if (score > anyFallbackScore)
                {
                    anyFallbackScore = score;
                    anyFallback = new Vector2(cx, cy);
                }

                if (pathTarget >= 3.0f && pathTarget <= 18.0f && score > bestScore)
                {
                    bestScore = score;
                    best = new Vector2(cx, cy);
                }
            }

            if (best.HasValue)
            {
                return best.Value;
            }

            return anyFallback ?? (targetPos + velDir * 3.5f);
        }

        private void ComputeGridDistances(int startX, int startY, int[] distOut)
        {
            Array.Fill(distOut, -1);
            if (startX < 0 || startY < 0 || startX >= Width || startY >= Height)
            {
                return;
            }

            var startIdx = startY * Width + startX;
            if (!_walkable[startIdx])
            {
                return;
            }

            distOut[startIdx] = 0;
            var head = 0;
            var tail = 0;
            _bfsQueue[tail++] = startIdx;

            while (head < tail)
            {
                var idx = _bfsQueue[head++];
                var nextDist = distOut[idx] + 1;
                var mask = _neighborMask[idx];
                var x = idx % Width;
                var y = idx / Width;

                for (var d = 0; d < 4; d++)
                {
                    if ((mask & (1 << d)) == 0)
                    {
                        continue;
                    }

                    var nIdx = (y + Dy[d]) * Width + (x + Dx[d]);
                    if (distOut[nIdx] >= 0)
                    {
                        continue;
                    }

                    distOut[nIdx] = nextDist;
                    _bfsQueue[tail++] = nIdx;
                }
            }
        }

        /// <summary>
        /// Claims the highest-scoring Composite Potential Field peak (Information × inverse Confidence + Connectivity)
        /// for a guard while maintaining separation from the partner guard's sector and claimed target.
        /// </summary>
        public Vector2? ClaimBestSearchTarget(int guardId, Vector2 guardPos, float minSeparation = 5.5f)
        {
            var id = Math.Clamp(guardId, 0, 1);
            var otherClaim = ClaimedTargets[1 - id];
            var otherPos = GuardPositions[1 - id];
            var hasOtherPos = otherPos.sqrMagnitude > 0.01f;
            var minSepSq = minSeparation * minSeparation;
            var minPosSepSq = minSepSq * 0.55f;

            var bestSeparatedScore = -1f;
            Vector2? bestSeparated = null;
            var bestGlobalScore = -1f;
            Vector2? bestGlobal = null;

            for (var y = 0; y < Height; y++)
            {
                var rowOffset = y * Width;
                var cy = y + 0.5f;
                for (var x = 0; x < Width; x++)
                {
                    var idx = rowOffset + x;
                    var p = _prob[idx];
                    if (!_walkable[idx] || p <= 0.02f)
                    {
                        continue;
                    }

                    var cx = x + 0.5f;
                    var dx = cx - guardPos.x;
                    var dy = cy - guardPos.y;
                    var dist = MathF.Sqrt(dx * dx + dy * dy);
                    var cpf = p * (1f - 0.65f * _confidence[idx]) + 0.08f * _connectivity[idx];
                    var score = cpf / (1f + 0.035f * dist);

                    if (score > bestGlobalScore)
                    {
                        bestGlobalScore = score;
                        bestGlobal = new Vector2(cx, cy);
                    }

                    if (otherClaim.HasValue)
                    {
                        var odx = cx - otherClaim.Value.x;
                        var ody = cy - otherClaim.Value.y;
                        if (odx * odx + ody * ody < minSepSq)
                        {
                            continue;
                        }
                    }

                    if (hasOtherPos)
                    {
                        var pdx = cx - otherPos.x;
                        var pdy = cy - otherPos.y;
                        if (pdx * pdx + pdy * pdy < minPosSepSq)
                        {
                            continue;
                        }
                    }

                    if (score > bestSeparatedScore)
                    {
                        bestSeparatedScore = score;
                        bestSeparated = new Vector2(cx, cy);
                    }
                }
            }

            var chosen = bestSeparated ?? bestGlobal;
            ClaimedTargets[id] = chosen;
            return chosen;
        }

        /// <summary>Updates last-seen tick timestamps on corner spots visible to the given observer cone.</summary>
        public void RecordVisionStaleness(
            Vector2 origin,
            float angle,
            float halfAngle,
            float range,
            int tick,
            Func<Vector2, Vector2, bool> hasLineOfSight)
        {
            if (_cornerSpots.Length == 0 || range <= 0.1f)
            {
                return;
            }

            var maxRangeSq = range * range;
            for (var i = 0; i < _cornerSpots.Length; i++)
            {
                var spot = _cornerSpots[i];
                var dx = spot.x - origin.x;
                var dy = spot.y - origin.y;
                var d2 = dx * dx + dy * dy;
                if (d2 > maxRangeSq)
                {
                    continue;
                }

                if (d2 > 0.04f)
                {
                    var diff = MathF.Atan2(dy, dx) - angle;
                    while (diff > MathF.PI) diff -= 2f * MathF.PI;
                    while (diff < -MathF.PI) diff += 2f * MathF.PI;
                    if (MathF.Abs(diff) > halfAngle)
                    {
                        continue;
                    }
                }

                if (hasLineOfSight == null || hasLineOfSight(origin, spot))
                {
                    _cornerLastSeenTick[i] = tick;
                }
            }
        }

        /// <summary>
        /// Samples a blind corner inspection spot weighted by observation staleness and proximity,
        /// avoiding the partner guard's sector and claimed target.
        /// </summary>
        public Vector2? PickStaleCornerTarget(int guardId, Vector2 guardPos, int tick, float minGuardSeparation = 6.5f)
        {
            if (_cornerSpots.Length == 0)
            {
                return null;
            }

            var id = Math.Clamp(guardId, 0, 1);
            var otherPos = GuardPositions[1 - id];
            var hasOtherPos = otherPos.sqrMagnitude > 0.01f;
            var otherClaim = ClaimedTargets[1 - id];
            var minSepSq = minGuardSeparation * minGuardSeparation;

            var bandIdx0 = -1;
            var bandIdx1 = -1;
            var bandIdx2 = -1;
            var bandIdx3 = -1;
            var bandScore0 = -1f;
            var bandScore1 = -1f;
            var bandScore2 = -1f;
            var bandScore3 = -1f;

            var anyIdx0 = -1;
            var anyIdx1 = -1;
            var anyIdx2 = -1;
            var anyIdx3 = -1;
            var anyScore0 = -1f;
            var anyScore1 = -1f;
            var anyScore2 = -1f;
            var anyScore3 = -1f;

            for (var i = 0; i < _cornerSpots.Length; i++)
            {
                var spot = _cornerSpots[i];
                if (hasOtherPos)
                {
                    var pdx = spot.x - otherPos.x;
                    var pdy = spot.y - otherPos.y;
                    if (pdx * pdx + pdy * pdy < minSepSq)
                    {
                        continue;
                    }
                }

                if (otherClaim.HasValue)
                {
                    var cdx = spot.x - otherClaim.Value.x;
                    var cdy = spot.y - otherClaim.Value.y;
                    if (cdx * cdx + cdy * cdy < minSepSq)
                    {
                        continue;
                    }
                }

                var dx = spot.x - guardPos.x;
                var dy = spot.y - guardPos.y;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                var staleness = Math.Max(60, tick - _cornerLastSeenTick[i]);
                var score = staleness / (1f + 0.08f * dist);

                InsertTop4(i, score, ref anyIdx0, ref anyIdx1, ref anyIdx2, ref anyIdx3, ref anyScore0, ref anyScore1, ref anyScore2, ref anyScore3);
                if (dist >= 3.0f && dist <= 16.0f)
                {
                    InsertTop4(i, score, ref bandIdx0, ref bandIdx1, ref bandIdx2, ref bandIdx3, ref bandScore0, ref bandScore1, ref bandScore2, ref bandScore3);
                }
            }

            var chosenIdx = bandIdx0 >= 0
                ? SampleTop4(bandIdx0, bandIdx1, bandIdx2, bandIdx3, bandScore0, bandScore1, bandScore2, bandScore3)
                : SampleTop4(anyIdx0, anyIdx1, anyIdx2, anyIdx3, anyScore0, anyScore1, anyScore2, anyScore3);

            if (chosenIdx < 0)
            {
                return null;
            }

            var chosen = _cornerSpots[chosenIdx];
            ClaimedTargets[id] = chosen;
            _cornerLastSeenTick[chosenIdx] = Math.Max(_cornerLastSeenTick[chosenIdx], tick - 120);
            return chosen;
        }

        /// <summary>Releases any search target claim held by the specified guard.</summary>
        public void ReleaseClaim(int guardId)
        {
            var id = Math.Clamp(guardId, 0, 1);
            ClaimedTargets[id] = null;
        }

        /// <summary>Assigns complementary tactical roles across one or two active security guards.</summary>
        public void UpdateSquadRoles(
            int tick,
            int activeGuardCount,
            bool g0Sees,
            bool g1Sees,
            Vector2 playerPos)
        {
            var twoGuards = activeGuardCount >= 2;
            AnyGuardSeesPlayer = g0Sees || (twoGuards && g1Sees);

            if (g0Sees && (!twoGuards || !g1Sees))
            {
                SpotterGuardId = 0;
            }
            else if (twoGuards && g1Sees && !g0Sees)
            {
                SpotterGuardId = 1;
            }
            else if (twoGuards && g0Sees && g1Sees)
            {
                var d0 = GuardPerception.Distance(GuardPositions[0], playerPos);
                var d1 = GuardPerception.Distance(GuardPositions[1], playerPos);
                SpotterGuardId = SelectCloserWithHysteresis(_lastPursuerGuardId, d0, d1);
            }
            else
            {
                SpotterGuardId = -1;
            }

            var hasSabotage = CamerasDisabled || (VitrineOpen && !VitrineInspected);

            if (!twoGuards)
            {
                AssignedRoles[1] = GuardTacticalRole.Patrol;
                if (g0Sees)
                {
                    AssignedRoles[0] = GuardTacticalRole.PrimaryPursuer;
                    _lastPursuerGuardId = 0;
                }
                else if (EasAlarmActive)
                {
                    AssignedRoles[0] = GuardTacticalRole.ExitInterceptor;
                    _lastPursuerGuardId = -1;
                }
                else if (hasSabotage)
                {
                    AssignedRoles[0] = GuardTacticalRole.SabotageResponder;
                    _lastPursuerGuardId = -1;
                }
                else if (MaxProbability > 0.03f || HeatTier >= 3)
                {
                    AssignedRoles[0] = GuardTacticalRole.SectorSweeper;
                }
                else
                {
                    AssignedRoles[0] = GuardTacticalRole.Patrol;
                    _lastPursuerGuardId = -1;
                }

                return;
            }

            if (AnyGuardSeesPlayer && SpotterGuardId >= 0)
            {
                var spotter = SpotterGuardId;
                _lastPursuerGuardId = spotter;
                var other = 1 - spotter;
                AssignedRoles[spotter] = GuardTacticalRole.PrimaryPursuer;
                var otherDist = GuardPerception.Distance(GuardPositions[other], playerPos);
                AssignedRoles[other] = otherDist > 3.5f
                    && (EasAlarmActive || HeatTier >= 3 || LastConfirmedVelocity.sqrMagnitude >= 0.05f || otherDist > 6.0f)
                    ? GuardTacticalRole.ExitInterceptor
                    : GuardTacticalRole.PrimaryPursuer;
                return;
            }

            if (HasRecentSighting(tick, RecentContactWindowTicks) && MaxProbability > 0.08f && !EasAlarmActive && LastConfirmedPos.HasValue)
            {
                var lkp = LastConfirmedPos.Value;
                var d0 = GuardPerception.Distance(GuardPositions[0], lkp);
                var d1 = GuardPerception.Distance(GuardPositions[1], lkp);
                var pursuer = SelectCloserWithHysteresis(_lastPursuerGuardId, d0, d1);
                _lastPursuerGuardId = pursuer;
                var flanker = 1 - pursuer;
                AssignedRoles[pursuer] = GuardTacticalRole.PrimaryPursuer;
                AssignedRoles[flanker] = GuardPerception.Distance(GuardPositions[flanker], lkp) > 3.0f
                    ? GuardTacticalRole.ExitInterceptor
                    : GuardTacticalRole.PrimaryPursuer;
                return;
            }

            if (EasAlarmActive)
            {
                _lastPursuerGuardId = -1;
                var eas = new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J);
                var d0 = GuardPerception.Distance(GuardPositions[0], eas);
                var d1 = GuardPerception.Distance(GuardPositions[1], eas);
                var interceptor = d0 <= d1 ? 0 : 1;
                AssignedRoles[interceptor] = GuardTacticalRole.ExitInterceptor;
                AssignedRoles[1 - interceptor] = GuardTacticalRole.SectorSweeper;
                return;
            }

            if (hasSabotage)
            {
                _lastPursuerGuardId = -1;
                var sabotagePos = CamerasDisabled
                    ? new Vector2((float)MarketData.Panel.I, (float)MarketData.Panel.J)
                    : MarketLayout.ItemPosition(MarketData.Fixture("m_spirits"));
                var d0 = GuardPerception.Distance(GuardPositions[0], sabotagePos);
                var d1 = GuardPerception.Distance(GuardPositions[1], sabotagePos);
                var responder = d0 <= d1 ? 0 : 1;
                AssignedRoles[responder] = GuardTacticalRole.SabotageResponder;
                AssignedRoles[1 - responder] = (MaxProbability > 0.03f || HeatTier >= 3)
                    ? GuardTacticalRole.SectorSweeper
                    : GuardTacticalRole.Patrol;
                return;
            }

            if (MaxProbability > 0.03f)
            {
                AssignedRoles[0] = GuardTacticalRole.SectorSweeper;
                AssignedRoles[1] = GuardTacticalRole.SectorSweeper;
                return;
            }

            _lastPursuerGuardId = -1;
            AssignedRoles[0] = GuardTacticalRole.Patrol;
            AssignedRoles[1] = GuardTacticalRole.Patrol;
        }

        private static int SelectCloserWithHysteresis(int preferredId, float d0, float d1, float hysteresis = 0.85f)
        {
            if (preferredId == 0)
            {
                return (d1 + hysteresis) < d0 ? 1 : 0;
            }

            if (preferredId == 1)
            {
                return (d0 + hysteresis) < d1 ? 0 : 1;
            }

            return d0 <= d1 ? 0 : 1;
        }

        private GridPos? ResolveWalkableCell(Vector2 pos)
        {
            var tx = (int)MathF.Floor(pos.x);
            var ty = (int)MathF.Floor(pos.y);
            if (tx >= 0 && ty >= 0 && tx < Width && ty < Height && _walkable[ty * Width + tx])
            {
                return new GridPos(tx, ty);
            }

            return MarketGrid.NearestWalkable(_grid, pos.x, pos.y);
        }

        private void RecomputePeak()
        {
            var maxP = 0f;
            Vector2? peakPos = null;
            for (var y = 0; y < Height; y++)
            {
                var rowOffset = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    var v = _prob[rowOffset + x];
                    if (v > maxP)
                    {
                        maxP = v;
                        peakPos = new Vector2(x + 0.5f, y + 0.5f);
                    }
                }
            }

            MaxProbability = maxP;
            PeakProbabilityPos = peakPos;
        }

        private static int CountBits(byte mask)
        {
            var count = 0;
            for (var d = 0; d < 4; d++)
            {
                if ((mask & (1 << d)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private uint NextUInt32()
        {
            var x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x;
            return x;
        }

        private int SampleTop4(int i0, int i1, int i2, int i3, float s0, float s1, float s2, float s3)
        {
            var total = 0f;
            if (i0 >= 0) total += s0;
            if (i1 >= 0) total += s1;
            if (i2 >= 0) total += s2;
            if (i3 >= 0) total += s3;
            if (total <= 0f)
            {
                return i0;
            }

            var roll = (NextUInt32() / 4294967296f) * total;
            if (i0 >= 0 && roll <= s0) return i0;
            roll -= i0 >= 0 ? s0 : 0f;
            if (i1 >= 0 && roll <= s1) return i1;
            roll -= i1 >= 0 ? s1 : 0f;
            if (i2 >= 0 && roll <= s2) return i2;
            return i3 >= 0 ? i3 : i0;
        }

        private static void InsertTop4(
            int idx,
            float score,
            ref int i0,
            ref int i1,
            ref int i2,
            ref int i3,
            ref float s0,
            ref float s1,
            ref float s2,
            ref float s3)
        {
            if (score > s0)
            {
                i3 = i2; s3 = s2;
                i2 = i1; s2 = s1;
                i1 = i0; s1 = s0;
                i0 = idx; s0 = score;
            }
            else if (score > s1)
            {
                i3 = i2; s3 = s2;
                i2 = i1; s2 = s1;
                i1 = idx; s1 = score;
            }
            else if (score > s2)
            {
                i3 = i2; s3 = s2;
                i2 = idx; s2 = score;
            }
            else if (score > s3)
            {
                i3 = idx; s3 = score;
            }
        }

        private Vector2[] BuildCornerSpots()
        {
            var spots = new List<Vector2>(36);
            var roomCandidates = new List<(Vector2 Pos, int CornerScore, int Degree, int Idx)>(32);

            foreach (var room in MarketData.Rooms)
            {
                roomCandidates.Clear();
                for (var ly = 0; ly < room.Depth; ly++)
                {
                    for (var lx = 0; lx < room.Width; lx++)
                    {
                        if ((lx > 2 && lx < room.Width - 3) || (ly > 2 && ly < room.Depth - 3))
                        {
                            continue;
                        }

                        var gx = room.OriginI + lx;
                        var gy = room.OriginJ + ly;
                        if (gx < 0 || gy < 0 || gx >= Width || gy >= Height)
                        {
                            continue;
                        }

                        var idx = gy * Width + gx;
                        if (!_walkable[idx])
                        {
                            continue;
                        }

                        var deg = CountBits(_neighborMask[idx]);
                        if (deg < 2)
                        {
                            continue;
                        }

                        var pos = new Vector2(gx + 0.5f, gy + 0.5f);
                        if (IsNearRoute(pos, MarketData.GuardRoute, 4.0f) || IsNearRoute(pos, MarketData.Guard2Route, 4.0f))
                        {
                            continue;
                        }

                        var cornerScore = Math.Min(lx, room.Width - 1 - lx) + Math.Min(ly, room.Depth - 1 - ly);
                        roomCandidates.Add((pos, cornerScore, deg, idx));
                    }
                }

                roomCandidates.Sort((a, b) =>
                {
                    var cmp = a.CornerScore.CompareTo(b.CornerScore);
                    if (cmp != 0) return cmp;
                    cmp = b.Degree.CompareTo(a.Degree);
                    return cmp != 0 ? cmp : a.Idx.CompareTo(b.Idx);
                });

                var addedInRoom = 0;
                var startCount = spots.Count;
                foreach (var c in roomCandidates)
                {
                    var tooClose = false;
                    for (var k = startCount; k < spots.Count; k++)
                    {
                        var dx = c.Pos.x - spots[k].x;
                        var dy = c.Pos.y - spots[k].y;
                        if (dx * dx + dy * dy < 9.0f)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                    {
                        continue;
                    }

                    spots.Add(c.Pos);
                    addedInRoom++;
                    if (addedInRoom >= 3)
                    {
                        break;
                    }
                }
            }

            return spots.ToArray();
        }

        private static bool IsNearRoute(Vector2 pos, GridPos[] route, float minDistSq)
        {
            if (route == null)
            {
                return false;
            }

            for (var i = 0; i < route.Length; i++)
            {
                var wx = route[i].X + 0.5f + (float)route[i].OffsetI;
                var wy = route[i].Y + 0.5f;
                var dx = pos.x - wx;
                var dy = pos.y - wy;
                if (dx * dx + dy * dy < minDistSq)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
