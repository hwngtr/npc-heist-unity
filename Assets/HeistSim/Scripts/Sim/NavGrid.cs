using System;
using System.Collections.Generic;

namespace HeistSim
{
    /// <summary>Integer tile position on the navigation grid.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        /// <summary>Creates a tile position.</summary>
        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Tile column.</summary>
        public int X { get; }

        /// <summary>Tile row.</summary>
        public int Y { get; }

        /// <summary>Value equality on both coordinates.</summary>
        public bool Equals(GridPos other)
        {
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
            return InBounds(x, y) && _isWalkable(x, y);
        }

        /// <summary>Whether a single orthogonal step into a tile is legal.</summary>
        public bool CanStep(int fromX, int fromY, int toX, int toY)
        {
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
            AddNeighbour(result, x, y, 1, 0);
            AddNeighbour(result, x, y, -1, 0);
            AddNeighbour(result, x, y, 0, 1);
            AddNeighbour(result, x, y, 0, -1);
            return result;
        }

        /// <summary>Returns the shortest path from start to goal including both tiles, or null when unreachable.</summary>
        public List<GridPos> FindPath(GridPos start, GridPos goal)
        {
            var startKey = new GridPos(start.X, start.Y);
            if (startKey.Equals(goal))
            {
                return new List<GridPos> { goal };
            }

            var queue = new Queue<GridPos>();
            var previous = new Dictionary<GridPos, GridPos>();
            var visited = new HashSet<GridPos> { startKey };
            queue.Enqueue(startKey);

            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in Neighbors(at.X, at.Y))
                {
                    if (!visited.Add(next))
                    {
                        continue;
                    }

                    previous[next] = at;
                    if (next.Equals(goal))
                    {
                        return Rebuild(previous, startKey, next);
                    }

                    queue.Enqueue(next);
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

        private static List<GridPos> Rebuild(Dictionary<GridPos, GridPos> previous, GridPos start, GridPos goal)
        {
            var path = new List<GridPos>();
            var current = goal;
            while (!current.Equals(start))
            {
                path.Add(current);
                current = previous[current];
            }

            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
