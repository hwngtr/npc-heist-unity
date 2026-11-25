using System;
using System.Collections.Generic;

namespace HeistSim
{
    /// <summary>Integer tile position on the navigation grid.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        /// <summary>Creates a tile position at the tile centre.</summary>
        public GridPos(int x, int y)
            : this(x, y, 0d)
        {
        }

        /// <summary>
        /// Creates a tile position with a fractional shift along the I axis. Route waypoints use this to
        /// keep the browser game's exact continuous position without changing the integer tile grid.
        /// </summary>
        public GridPos(int x, int y, double offsetI)
        {
            X = x;
            Y = y;
            OffsetI = offsetI;
        }

        /// <summary>Tile column.</summary>
        public int X { get; }

        /// <summary>Tile row.</summary>
        public int Y { get; }

        /// <summary>Fractional shift from the tile centre along the I axis.</summary>
        public double OffsetI { get; }

        /// <summary>Value equality on both coordinates.</summary>
        public bool Equals(GridPos other)
        {
            // OffsetI is a presentation nudge, not part of tile identity.
            return X == other.X && Y == other.Y;
        }

        /// <summary>Value equality on both coordinates.</summary>
        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        /// <summary>Hash code from both coordinates.</summary>
        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        /// <summary>Coordinate pair for diagnostics.</summary>
        public override string ToString()
        {
            return X + "," + Y;
        }
    }

    /// <summary>Breadth-first navigation over a walkable tile grid.</summary>
    public sealed class NavGrid
    {
        private readonly Func<int, int, bool> _isWalkable;
        private readonly Func<int, int, int, int, bool> _canStep;
        private readonly bool[] _walkable;
        private readonly byte[] _stepMask;
        private readonly int[] _queue;
        private readonly int[] _parent;
        private readonly int[] _visitStamp;
        private int _epoch;

        /// <summary>Creates a grid from its bounds and a per-tile walkability predicate.</summary>
        public NavGrid(int width, int height, Func<int, int, bool> isWalkable)
            : this(width, height, isWalkable, null)
        {
        }

        /// <summary>Creates a grid from its bounds, a per-tile predicate, and an optional per-step predicate.</summary>
        public NavGrid(
            int width,
            int height,
            Func<int, int, bool> isWalkable,
            Func<int, int, int, int, bool> canStep)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
            }

            Width = width;
            Height = height;
            _isWalkable = isWalkable ?? throw new ArgumentNullException(nameof(isWalkable));
            _canStep = canStep;

            var count = width * height;
            _walkable = new bool[count];
            _stepMask = new byte[count];
            _queue = new int[count];
            _parent = new int[count];
            _visitStamp = new int[count];

            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    _walkable[row + x] = _isWalkable(x, y);
                }
            }

            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    byte mask = 0;
                    if (x + 1 < width && _walkable[row + x + 1] && (_canStep == null || _canStep(x, y, x + 1, y)))
                    {
                        mask |= 1;
                    }

                    if (x - 1 >= 0 && _walkable[row + x - 1] && (_canStep == null || _canStep(x, y, x - 1, y)))
                    {
                        mask |= 2;
                    }

                    if (y + 1 < height && _walkable[row + width + x] && (_canStep == null || _canStep(x, y, x, y + 1)))
                    {
                        mask |= 4;
                    }

                    if (y - 1 >= 0 && _walkable[row - width + x] && (_canStep == null || _canStep(x, y, x, y - 1)))
                    {
                        mask |= 8;
                    }

                    _stepMask[row + x] = mask;
                }
            }
        }

        /// <summary>Grid width in tiles.</summary>
        public int Width { get; }

        /// <summary>Grid height in tiles.</summary>
        public int Height { get; }

        /// <summary>Whether a tile lies inside the grid bounds.</summary>
        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        /// <summary>Whether a tile is inside the grid and walkable.</summary>
        public bool IsWalkable(int x, int y)
        {
            return InBounds(x, y) && _walkable[y * Width + x];
        }

        /// <summary>Whether a single orthogonal step into a tile is legal.</summary>
        public bool CanStep(int fromX, int fromY, int toX, int toY)
        {
            if (InBounds(fromX, fromY))
            {
                var dx = toX - fromX;
                var dy = toY - fromY;
                var idx = fromY * Width + fromX;
                if (dx == 1 && dy == 0) return (_stepMask[idx] & 1) != 0;
                if (dx == -1 && dy == 0) return (_stepMask[idx] & 2) != 0;
                if (dx == 0 && dy == 1) return (_stepMask[idx] & 4) != 0;
                if (dx == 0 && dy == -1) return (_stepMask[idx] & 8) != 0;
            }

            if (!IsWalkable(toX, toY))
            {
                return false;
            }

            return _canStep == null || _canStep(fromX, fromY, toX, toY);
        }

        /// <summary>Walkable orthogonal neighbours of a tile, in right, left, up, down order.</summary>
        public List<GridPos> Neighbors(int x, int y)
        {
            var result = new List<GridPos>(4);
            if (InBounds(x, y))
            {
                var mask = _stepMask[y * Width + x];
                if ((mask & 1) != 0) result.Add(new GridPos(x + 1, y));
                if ((mask & 2) != 0) result.Add(new GridPos(x - 1, y));
                if ((mask & 4) != 0) result.Add(new GridPos(x, y + 1));
                if ((mask & 8) != 0) result.Add(new GridPos(x, y - 1));
                return result;
            }

            AddNeighbour(result, x, y, 1, 0);
            AddNeighbour(result, x, y, -1, 0);
            AddNeighbour(result, x, y, 0, 1);
            AddNeighbour(result, x, y, 0, -1);
            return result;
        }

        /// <summary>Returns the shortest path from start to goal including both tiles, or null when unreachable.</summary>
        public List<GridPos> FindPath(GridPos start, GridPos goal)
        {
            if (start.Equals(goal))
            {
                return new List<GridPos> { goal };
            }

            if (!InBounds(start.X, start.Y) || !InBounds(goal.X, goal.Y))
            {
                return null;
            }

            var w = Width;
            var startIdx = start.Y * w + start.X;
            var goalIdx = goal.Y * w + goal.X;
            if (!_walkable[goalIdx])
            {
                return null;
            }

            if (++_epoch == int.MaxValue)
            {
                Array.Clear(_visitStamp, 0, _visitStamp.Length);
                _epoch = 1;
            }

            var epoch = _epoch;
            var head = 0;
            var tail = 0;
            _visitStamp[startIdx] = epoch;
            _parent[startIdx] = -1;
            _queue[tail++] = startIdx;

            while (head < tail)
            {
                var at = _queue[head++];
                var mask = _stepMask[at];
                if (mask == 0)
                {
                    continue;
                }

                if ((mask & 1) != 0)
                {
                    var next = at + 1;
                    if (_visitStamp[next] != epoch)
                    {
                        _visitStamp[next] = epoch;
                        _parent[next] = at;
                        if (next == goalIdx) return Rebuild(w, start, startIdx, goalIdx);
                        _queue[tail++] = next;
                    }
                }

                if ((mask & 2) != 0)
                {
                    var next = at - 1;
                    if (_visitStamp[next] != epoch)
                    {
                        _visitStamp[next] = epoch;
                        _parent[next] = at;
                        if (next == goalIdx) return Rebuild(w, start, startIdx, goalIdx);
                        _queue[tail++] = next;
                    }
                }

                if ((mask & 4) != 0)
                {
                    var next = at + w;
                    if (_visitStamp[next] != epoch)
                    {
                        _visitStamp[next] = epoch;
                        _parent[next] = at;
                        if (next == goalIdx) return Rebuild(w, start, startIdx, goalIdx);
                        _queue[tail++] = next;
                    }
                }

                if ((mask & 8) != 0)
                {
                    var next = at - w;
                    if (_visitStamp[next] != epoch)
                    {
                        _visitStamp[next] = epoch;
                        _parent[next] = at;
                        if (next == goalIdx) return Rebuild(w, start, startIdx, goalIdx);
                        _queue[tail++] = next;
                    }
                }
            }

            return null;
        }

        private void AddNeighbour(List<GridPos> result, int x, int y, int dx, int dy)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (CanStep(x, y, nx, ny))
            {
                result.Add(new GridPos(nx, ny));
            }
        }

        private List<GridPos> Rebuild(int w, GridPos start, int startIdx, int goalIdx)
        {
            var path = new List<GridPos>();
            var current = goalIdx;
            while (current != startIdx)
            {
                var cy = current / w;
                var cx = current - cy * w;
                path.Add(new GridPos(cx, cy));
                current = _parent[current];
            }

            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
