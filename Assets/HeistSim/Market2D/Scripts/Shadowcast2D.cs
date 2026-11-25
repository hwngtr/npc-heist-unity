using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// 2D grid recursive shadowcasting (8-octant slope scan), Fog-of-War explored tracker,
    /// and wall-clipped raycasting for isometric Guard and CCTV vision cones.
    /// Implements the SpriteCook 2D Shadowcasting technique.
    /// </summary>
    public static class Shadowcast2D
    {
        private readonly struct CctvSightBox
        {
            public CctvSightBox(float x0, float y0, float x1, float y1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public readonly float X0;
            public readonly float Y0;
            public readonly float X1;
            public readonly float Y1;
        }

        private static readonly CctvSightBox[] CctvSightBoxes = BuildCctvSightBoxes();

        private static readonly (int Xx, int Xy, int Yx, int Yy)[] Octants =
        {
            ( 1,  0,  0,  1),
            ( 0,  1,  1,  0),
            ( 0, -1,  1,  0),
            (-1,  0,  0,  1),
            (-1,  0,  0, -1),
            ( 0, -1, -1,  0),
            ( 0,  1, -1,  0),
            ( 1,  0,  0, -1),
        };

        /// <summary>
        /// Computes all grid cells visible from (cx, cy) within radius using 8-octant recursive shadowcasting.
        /// Cells at distance strictly less than radius are visible; walls block light behind them while remaining lit.
        /// </summary>
        public static bool[,] Cast(bool[,] walls, int width, int height, int cx, int cy, int radius)
        {
            var seen = new bool[height, width];
            if (walls == null || radius <= 0 || cx < 0 || cx >= width || cy < 0 || cy >= height)
            {
                return seen;
            }

            seen[cy, cx] = true;
            var rSq = radius * radius;
            var stack = new Stack<(int Row, float Start, float End)>(32);

            for (var o = 0; o < Octants.Length; o++)
            {
                var (xx, xy, yx, yy) = Octants[o];
                stack.Clear();
                stack.Push((1, 1.0f, 0.0f));

                while (stack.Count > 0)
                {
                    var (row, start, end) = stack.Pop();
                    if (start < end)
                    {
                        continue;
                    }

                    var newStart = 0.0f;
                    for (var j = row; j <= radius; j++)
                    {
                        var dy = -j;
                        var blocked = false;

                        for (var dx = -j; dx <= 0; dx++)
                        {
                            var left = (dx - 0.5f) / (dy + 0.5f);
                            var right = (dx + 0.5f) / (dy - 0.5f);
                            if (start < right)
                            {
                                continue;
                            }

                            if (end > left)
                            {
                                break;
                            }

                            var x = cx + dx * xx + dy * xy;
                            var y = cy + dx * yx + dy * yy;
                            var inside = x >= 0 && x < width && y >= 0 && y < height;

                            if (inside && dx * dx + dy * dy < rSq)
                            {
                                seen[y, x] = true;
                            }

                            var wall = !inside || walls[y, x];
                            if (blocked)
                            {
                                if (wall)
                                {
                                    newStart = right;
                                }
                                else
                                {
                                    blocked = false;
                                    start = newStart;
                                }
                            }
                            else if (wall && j < radius)
                            {
                                blocked = true;
                                stack.Push((j + 1, start, left));
                                newStart = right;
                            }
                        }

                        if (blocked)
                        {
                            break;
                        }
                    }
                }
            }

            return seen;
        }

        /// <summary>
        /// Accumulates newly seen cells into the persistent Fog-of-War explored grid (never clears previously explored cells).
        /// </summary>
        public static void UpdateExplored(bool[,] explored, bool[,] seen)
        {
            if (explored == null || seen == null)
            {
                return;
            }

            var h = Math.Min(explored.GetLength(0), seen.GetLength(0));
            var w = Math.Min(explored.GetLength(1), seen.GetLength(1));
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (seen[y, x])
                    {
                        explored[y, x] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Computes 6-band quantized radial falloff (power 0.8) matching the SpriteCook shadowcasting light falloff.
        /// </summary>
        public static float BandedFalloff(float distance, float radius, int bands = 6)
        {
            if (radius <= 0f || distance >= radius)
            {
                return 0f;
            }

            var t = Mathf.Pow(Mathf.Clamp01(1f - distance / radius), 0.8f);
            if (bands <= 1)
            {
                return t;
            }

            return Mathf.Ceil(t * bands) / bands;
        }

        /// <summary>
        /// Builds a boolean light-blocking wall grid from the supermarket NavGrid for shadowcasting.
        /// </summary>
        public static bool[,] BuildWallMask(NavGrid grid)
        {
            if (grid == null)
            {
                return new bool[0, 0];
            }

            var w = grid.Width;
            var h = grid.Height;
            var walls = new bool[h, w];
            for (var j = 0; j < h; j++)
            {
                for (var i = 0; i < w; i++)
                {
                    walls[j, i] = !grid.IsWalkable(i, j);
                }
            }

            return walls;
        }

        /// <summary>
        /// Marches a continuous tile-space ray matching HeistGame.LineOfSight for ceiling-mounted CCTV cameras:
        /// sees over waist-high fixtures, stops cleanly at room partition walls (outside doorways) and tall
        /// sight-blocking fixtures (Height >= 1.5).
        /// </summary>
        public static float CastCctvRayDistance(float originI, float originJ, float angleRad, float maxRange)
        {
            if (maxRange <= 0f)
            {
                return 0f;
            }

            const float step = 0.22f;
            var cos = Mathf.Cos(angleRad);
            var sin = Mathf.Sin(angleRad);
            var originCi = Mathf.Clamp(Mathf.FloorToInt(originI), 0, MarketData.WidthTiles - 1);
            var originCj = Mathf.Clamp(Mathf.FloorToInt(originJ), 0, MarketData.HeightTiles - 1);
            var prevCi = originCi;
            var prevCj = originCj;
            var dist = step;

            while (dist < maxRange)
            {
                var ti = originI + cos * dist;
                var tj = originJ + sin * dist;
                if (ti < 0f || ti >= MarketData.WidthTiles || tj < 0f || tj >= MarketData.HeightTiles)
                {
                    return dist;
                }

                var ci = Mathf.FloorToInt(ti);
                var cj = Mathf.FloorToInt(tj);

                if (ci != prevCi || cj != prevCj)
                {
                    if (!MarketGrid.SameRoom(prevCi, prevCj, ci, cj) && !MarketGrid.IsDoorwayStep(prevCi, prevCj, ci, cj))
                    {
                        return dist;
                    }
                }

                if (dist > 0.85f && HitsTallSightBox(ti, tj, originI, originJ))
                {
                    return dist;
                }

                prevCi = ci;
                prevCj = cj;
                dist += step;
            }

            return maxRange;
        }

        private static bool HitsTallSightBox(float ti, float tj, float originI, float originJ)
        {
            for (var i = 0; i < CctvSightBoxes.Length; i++)
            {
                var box = CctvSightBoxes[i];
                if (ti <= box.X0 || ti >= box.X1 || tj <= box.Y0 || tj >= box.Y1)
                {
                    continue;
                }

                // Do not self-occlude against a cabinet adjacent to the camera mount
                if (originI > box.X0 - 0.85f && originI < box.X1 + 0.85f
                    && originJ > box.Y0 - 0.85f && originJ < box.Y1 + 0.85f)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static CctvSightBox[] BuildCctvSightBoxes()
        {
            var list = new List<CctvSightBox>(32);
            foreach (var fixture in MarketData.Fixtures)
            {
                var kind = MarketData.Kind(fixture.Kind);
                if (kind.Place == "wall" || kind.Place == "rug" || kind.Height < 1.4)
                {
                    continue;
                }

                var swapped = fixture.Face == "ne" || fixture.Face == "sw";
                var li = fixture.I + (swapped ? kind.SizeI / 2 : kind.SizeJ / 2);
                var lj = fixture.J + (swapped ? kind.SizeJ / 2 : kind.SizeI / 2);
                var room = MarketData.Room(fixture.Room);
                li = Math.Min(8.2, Math.Max(0.8, li));
                lj = Math.Min(8.2, Math.Max(0.8, lj));
                var px = (float)(room.OriginI + li);
                var py = (float)(room.OriginJ + lj);
                list.Add(new CctvSightBox(
                    px - 0.2f,
                    py - 0.2f,
                    px + (float)kind.SizeI * 0.7f,
                    py + (float)kind.SizeJ * 0.7f));
            }

            return list.ToArray();
        }

        /// <summary>
        /// Marches a continuous tile-space ray from (originI, originJ) along angle up to maxRange,
        /// returning the clipped distance where it first hits a room partition wall or solid fixture.
        /// Set ignoreOriginWall=true for wall-mounted CCTV cameras so rays clear their own wall mount before clipping.
        /// </summary>
        public static float CastRayDistance(
            NavGrid grid,
            float originI,
            float originJ,
            float angleRad,
            float maxRange,
            bool ignoreOriginWall = false)
        {
            if (grid == null || maxRange <= 0f)
            {
                return Mathf.Max(0f, maxRange);
            }

            const float step = 0.22f;
            var cos = Mathf.Cos(angleRad);
            var sin = Mathf.Sin(angleRad);
            var originCi = Mathf.FloorToInt(originI);
            var originCj = Mathf.FloorToInt(originJ);
            var prevCi = originCi;
            var prevCj = originCj;
            var clearedMount = !ignoreOriginWall || (grid.InBounds(originCi, originCj) && grid.IsWalkable(originCi, originCj));
            var dist = step;

            while (dist < maxRange)
            {
                var ti = originI + cos * dist;
                var tj = originJ + sin * dist;
                var ci = Mathf.FloorToInt(ti);
                var cj = Mathf.FloorToInt(tj);

                if (!clearedMount)
                {
                    if (grid.InBounds(ci, cj) && grid.IsWalkable(ci, cj))
                    {
                        clearedMount = true;
                        prevCi = ci;
                        prevCj = cj;
                    }
                    else if (dist > 1.25f)
                    {
                        return dist;
                    }

                    dist += step;
                    continue;
                }

                var blocked = !grid.InBounds(ci, cj) || !grid.IsWalkable(ci, cj);
                if (!blocked && (ci != prevCi || cj != prevCj))
                {
                    if (!MarketGrid.SameRoom(prevCi, prevCj, ci, cj) && !MarketGrid.IsDoorwayStep(prevCi, prevCj, ci, cj))
                    {
                        blocked = true;
                    }
                }

                if (blocked)
                {
                    var lo = Mathf.Max(0f, dist - step);
                    var hi = dist;
                    for (var b = 0; b < 4; b++)
                    {
                        var mid = (lo + hi) * 0.5f;
                        var mi = Mathf.FloorToInt(originI + cos * mid);
                        var mj = Mathf.FloorToInt(originJ + sin * mid);
                        var midBlocked = !grid.InBounds(mi, mj) || !grid.IsWalkable(mi, mj)
                            || (!MarketGrid.SameRoom(prevCi, prevCj, mi, mj) && !MarketGrid.IsDoorwayStep(prevCi, prevCj, mi, mj));
                        if (midBlocked)
                        {
                            hi = mid;
                        }
                        else
                        {
                            lo = mid;
                        }
                    }

                    return hi;
                }

                prevCi = ci;
                prevCj = cj;
                dist += step;
            }

            return maxRange;
        }
    }
}
