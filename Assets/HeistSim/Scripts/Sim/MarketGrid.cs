using System;
using System.Collections.Generic;

namespace HeistSim
{
    /// <summary>Walkable tile grid built from the Frischemarkt fixture data.</summary>
    public static class MarketGrid
    {
        /// <summary>Builds the navigation grid for the whole floorplan.</summary>
        public static NavGrid Build()
        {
            var blocked = BlockedTiles();
            return new NavGrid(
                MarketData.WidthTiles,
                MarketData.HeightTiles,
                (x, y) => IsInsideBuilding(x, y) && !blocked.Contains(new GridPos(x, y)),
                IsDoorwayStep);
        }

        /// <summary>Returns every tile blocked by a solid stand layer fixture, as a rectangle per fixture.</summary>
        public static HashSet<GridPos> BlockedTiles()
        {
            var blocked = new HashSet<GridPos>();
            foreach (var fixture in MarketData.Fixtures)
            {
                if (!IsSolidFixture(fixture))
                {
                    continue;
                }

                var tiles = FootprintTiles(fixture);
                if (tiles.Count == 0)
                {
                    continue;
                }

                var minX = int.MaxValue;
                var maxX = int.MinValue;
                var minY = int.MaxValue;
                var maxY = int.MinValue;
                foreach (var tile in tiles)
                {
                    minX = Math.Min(minX, tile.X);
                    maxX = Math.Max(maxX, tile.X);
                    minY = Math.Min(minY, tile.Y);
                    maxY = Math.Max(maxY, tile.Y);
                }

                for (var x = minX; x <= maxX; x++)
                {
                    for (var y = minY; y <= maxY; y++)
                    {
                        blocked.Add(new GridPos(x, y));
                    }
                }
            }

            return blocked;
        }

        /// <summary>Whether a fixture blocks its footprint: a stand layer object above zero height that is not a passable portal.</summary>
        public static bool IsSolidFixture(Fixture fixture)
        {
            var kind = MarketData.Kind(fixture.Kind);
            if (kind.Place == "wall" || kind.Place == "rug")
            {
                return false;
            }

            if (MarketData.PassableFixtures.Contains(fixture.Kind))
            {
                return false;
            }

            return kind.Height >= 0.01;
        }

        /// <summary>Returns the global tiles covered by a fixture footprint, mirroring the browser game whole tile rule.</summary>
        public static List<GridPos> FootprintTiles(Fixture fixture)
        {
            var kind = MarketData.Kind(fixture.Kind);
            var tiles = new List<GridPos>();
            if (kind.Place == "wall" || kind.Place == "rug")
            {
                return tiles;
            }

            var swapped = fixture.Face == "nw" || fixture.Face == "se";
            var width = swapped ? kind.SizeJ : kind.SizeI;
            var depth = swapped ? kind.SizeI : kind.SizeJ;
            var room = MarketData.Room(fixture.Room);
            var startI = (int)Math.Floor(fixture.I + 1e-6);
            var endI = (int)Math.Ceiling(fixture.I + width - 1e-6);
            var startJ = (int)Math.Floor(fixture.J + 1e-6);
            var endJ = (int)Math.Ceiling(fixture.J + depth - 1e-6);
            for (var i = startI; i < endI; i++)
            {
                for (var j = startJ; j < endJ; j++)
                {
                    tiles.Add(new GridPos(room.OriginI + i, room.OriginJ + j));
                }
            }

            return tiles;
        }

        /// <summary>Whether a tile centre lies inside the outer building inset, mirroring the browser game perimeter rule.</summary>
        public static bool IsInsideBuilding(int x, int y)
        {
            if (x < 0 || y < 0 || x >= MarketData.WidthTiles || y >= MarketData.HeightTiles)
            {
                return false;
            }

            var centreI = x + 0.5;
            var centreJ = y + 0.5;
            return centreI > 0.75
                && centreI < MarketData.WidthTiles - 0.75
                && centreJ > 0.75
                && centreJ < MarketData.HeightTiles - 0.75;
        }

        /// <summary>Whether an orthogonal step may cross between two rooms at this doorway band position.</summary>
        public static bool IsDoorwayStep(int fromX, int fromY, int toX, int toY)
        {
            if (RoomColumn(fromX) == RoomColumn(toX) && RoomRow(fromY) == RoomRow(toY))
            {
                return true;
            }

            var deltaX = Math.Abs(toX - fromX);
            var deltaY = Math.Abs(toY - fromY);
            var along = deltaX >= deltaY ? toY : toX;
            var local = ((along + 0.5) % MarketData.RoomTiles + MarketData.RoomTiles) % MarketData.RoomTiles;
            return local > MarketData.DoorwayBandMin && local < MarketData.DoorwayBandMax;
        }

        /// <summary>Whether two tiles belong to the same floorplan room cell.</summary>
        public static bool SameRoom(int x, int y, int x2, int y2)
        {
            return RoomColumn(x) == RoomColumn(x2) && RoomRow(y) == RoomRow(y2);
        }

        /// <summary>Room column of a global tile.</summary>
        public static int RoomColumn(int x)
        {
            return FloorDiv(x, MarketData.RoomTiles);
        }

        /// <summary>Room row of a global tile.</summary>
        public static int RoomRow(int y)
        {
            return FloorDiv(y, MarketData.RoomTiles);
        }

        /// <summary>Converts a continuous floorplan point to the tile that contains it.</summary>
        public static GridPos TileOf(double i, double j)
        {
            return new GridPos((int)Math.Floor(i), (int)Math.Floor(j));
        }

        /// <summary>Returns the walkable tile whose centre is closest to a continuous point, or null when the grid has none.</summary>
        public static GridPos? NearestWalkable(NavGrid grid, double i, double j)
        {
            GridPos? best = null;
            var bestDistance = double.MaxValue;
            for (var x = 0; x < grid.Width; x++)
            {
                for (var y = 0; y < grid.Height; y++)
                {
                    if (!grid.IsWalkable(x, y))
                    {
                        continue;
                    }

                    var dx = x + 0.5 - i;
                    var dy = y + 0.5 - j;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = new GridPos(x, y);
                    }
                }
            }

            return best;
        }

        /// <summary>Returns the walkable tiles orthogonally touching a fixture footprint, which form its aisle.</summary>
        public static List<GridPos> AdjacentWalkable(NavGrid grid, Fixture fixture)
        {
            var footprint = new HashSet<GridPos>(FootprintTiles(fixture));
            var aisle = new List<GridPos>();
            var seen = new HashSet<GridPos>();
            foreach (var tile in footprint)
            {
                AddIfAisle(grid, footprint, seen, aisle, tile.X + 1, tile.Y);
                AddIfAisle(grid, footprint, seen, aisle, tile.X - 1, tile.Y);
                AddIfAisle(grid, footprint, seen, aisle, tile.X, tile.Y + 1);
                AddIfAisle(grid, footprint, seen, aisle, tile.X, tile.Y - 1);
            }

            return aisle;
        }

        private static void AddIfAisle(
            NavGrid grid,
            HashSet<GridPos> footprint,
            HashSet<GridPos> seen,
            List<GridPos> aisle,
            int x,
            int y)
        {
            var tile = new GridPos(x, y);
            if (footprint.Contains(tile) || !grid.IsWalkable(x, y))
            {
                return;
            }

            if (seen.Add(tile))
            {
                aisle.Add(tile);
            }
        }

        private static int FloorDiv(int value, int divisor)
        {
            var quotient = value / divisor;
            if (value < 0 && value % divisor != 0)
            {
                quotient--;
            }

            return quotient;
        }
    }
}
