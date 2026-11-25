using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;

namespace HeistSim.Tests
{
    /// <summary>Walkable grid checks for the ported Frischemarkt floorplan.</summary>
    public sealed class MarketGridTests
    {
        private static readonly NavGrid Grid = MarketGrid.Build();

        [Test]
        public void GridMatchesTheFloorplanSize()
        {
            Assert.That(Grid.Width, Is.EqualTo(36));
            Assert.That(Grid.Height, Is.EqualTo(27));
        }

        [Test]
        public void OuterWallTilesAreBlocked()
        {
            Assert.That(Grid.IsWalkable(0, 13), Is.False, "outer wall on the entrance side blocks");
            Assert.That(Grid.IsWalkable(35, 13), Is.False, "outer wall on the checkout side blocks");
            Assert.That(Grid.IsWalkable(13, 0), Is.False, "outer wall on the top side blocks");
            Assert.That(Grid.IsWalkable(13, 26), Is.False, "outer wall on the bottom side blocks");
        }

        [Test]
        public void KnownAisleTilesAreOpen()
        {
            Assert.That(Grid.IsWalkable(1, 4), Is.True, "entrance aisle tile is open");
            Assert.That(Grid.IsWalkable(7, 4), Is.True, "first guard waypoint tile is open");
            Assert.That(Grid.IsWalkable(10, 2), Is.True, "aisle beside the r2 gondola is open");
            Assert.That(Grid.IsWalkable(12, 2), Is.False, "the r2 gondola tile itself is solid");
        }

        [Test]
        public void BlockedTileCountMatchesTheBrowserGame()
        {
            Assert.That(MarketGrid.BlockedTiles().Count, Is.EqualTo(199), "199 tiles are covered by solid fixtures after quadrant expressway reorganization");
        }

        [Test]
        public void EveryWalkableInteriorTileIsReachableAndDoorwayVestibulesStayClear()
        {
            var blocked = MarketGrid.BlockedTiles();
            foreach (var room in MarketData.Rooms)
            {
                for (var ly = 0; ly < room.Depth; ly++)
                {
                    for (var lx = 0; lx < room.Width; lx++)
                    {
                        var gx = room.OriginI + lx;
                        var gy = room.OriginJ + ly;
                        var touchesDoorway = (lx == 0 && gx > 0 && ly >= 3 && ly <= 5)
                            || (lx == room.Width - 1 && gx + 1 < Grid.Width && ly >= 3 && ly <= 5)
                            || (ly == 0 && gy > 0 && lx >= 3 && lx <= 5)
                            || (ly == room.Depth - 1 && gy + 1 < Grid.Height && lx >= 3 && lx <= 5);
                        if (touchesDoorway)
                        {
                            Assert.That(blocked.Contains(new GridPos(gx, gy)), Is.False,
                                $"{room.Id} doorway vestibule ({gx},{gy}) must not be blocked by a solid fixture");
                        }
                    }
                }
            }

            var start = MarketGrid.TileOf(MarketData.Entrance.I, MarketData.Entrance.J);
            var reachable = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var next in Grid.Neighbors(cur.X, cur.Y))
                {
                    if (reachable.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            for (var y = 0; y < Grid.Height; y++)
            {
                for (var x = 0; x < Grid.Width; x++)
                {
                    if (Grid.IsWalkable(x, y))
                    {
                        Assert.That(reachable.Contains(new GridPos(x, y)), Is.True,
                            $"Walkable tile ({x},{y}) must be reachable from Entrance (0 trapped pockets)");
                    }
                }
            }
        }

        [Test]
        public void EveryRoomDoorwayPairIsConnectedInsideItsRoom()
        {
            foreach (var room in MarketData.Rooms)
            {
                var oi = room.OriginI;
                var oj = room.OriginJ;
                var doors = new List<(string Name, GridPos Entry, GridPos StepIn)>();
                if (room.AtI > 0) doors.Add(("W", new GridPos(oi + 0, oj + 4), new GridPos(oi + 1, oj + 4)));
                if (room.AtI < MarketData.Columns - 1) doors.Add(("E", new GridPos(oi + 8, oj + 4), new GridPos(oi + 7, oj + 4)));
                if (room.AtJ > 0) doors.Add(("N", new GridPos(oi + 4, oj + 0), new GridPos(oi + 4, oj + 1)));
                if (room.AtJ < MarketData.Rows - 1) doors.Add(("S", new GridPos(oi + 4, oj + 8), new GridPos(oi + 4, oj + 7)));

                foreach (var door in doors)
                {
                    Assert.That(Grid.IsWalkable(door.Entry.X, door.Entry.Y), Is.True,
                        $"{room.Id} {door.Name} doorway tile {door.Entry} must be open");
                    Assert.That(Grid.IsWalkable(door.StepIn.X, door.StepIn.Y), Is.True,
                        $"{room.Id} {door.Name} step-in tile {door.StepIn} must be open (2-tile doorway clearance)");
                }

                for (var a = 0; a < doors.Count; a++)
                {
                    var visited = new HashSet<GridPos> { doors[a].Entry };
                    var queue = new Queue<GridPos>();
                    queue.Enqueue(doors[a].Entry);
                    while (queue.Count > 0)
                    {
                        var cur = queue.Dequeue();
                        var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                        foreach (var (dx, dy) in dirs)
                        {
                            var nx = cur.X + dx;
                            var ny = cur.Y + dy;
                            if (nx < oi || nx >= oi + MarketData.RoomTiles || ny < oj || ny >= oj + MarketData.RoomTiles)
                            {
                                continue;
                            }

                            var next = new GridPos(nx, ny);
                            if (Grid.IsWalkable(nx, ny) && visited.Add(next))
                            {
                                queue.Enqueue(next);
                            }
                        }
                    }

                    for (var b = a + 1; b < doors.Count; b++)
                    {
                        Assert.That(visited.Contains(doors[b].Entry), Is.True,
                            $"{room.Id} must connect door {doors[a].Name} to door {doors[b].Name} inside the room");
                    }
                }
            }
        }

        [Test]
        public void CrossRoomStepsOnlyHappenThroughTheDoorwayBand()
        {
            Assert.That(Grid.CanStep(8, 4, 9, 4), Is.True, "doorway at local tile 4 allows crossing");
            Assert.That(Grid.CanStep(9, 4, 8, 4), Is.True, "doorway at local tile 4 allows crossing backwards");
            Assert.That(Grid.CanStep(8, 7, 9, 7), Is.False, "solid wall blocks crossing outside the doorway band");
            Assert.That(Grid.CanStep(8, 5, 9, 5), Is.False, "edge of the doorway band at local 5.5 stays solid");
            Assert.That(Grid.CanStep(8, 3, 9, 3), Is.False, "edge of the doorway band at local 3.5 stays solid");
            Assert.That(Grid.CanStep(4, 8, 4, 9), Is.True, "horizontal doorway at local tile 4 allows crossing");
            Assert.That(Grid.CanStep(7, 8, 7, 9), Is.False, "horizontal wall blocks crossing outside the doorway band");
        }

        [Test]
        public void ReasonableWalkStaysInsideTheDoorwayBands()
        {
            var start = MarketGrid.TileOf(MarketData.Entrance.I, MarketData.Entrance.J);
            var goal = new GridPos(31, 25);
            var path = Grid.FindPath(start, goal);
            Assert.That(path, Is.Not.Null, "the entrance connects to the far checkout corner");
            AssertPathUsesOnlyDoorwayCrossings(path);
            Assert.That(path.Count, Is.GreaterThan(40), "the path crosses several rooms");
        }

        [Test]
        public void PathToTheFarthestLootExistsAndAvoidsFixtures()
        {
            var blocked = MarketGrid.BlockedTiles();
            var start = MarketGrid.TileOf(MarketData.Entrance.I, MarketData.Entrance.J);
            LootEntry farthest = default;
            GridPos farthestTile = default;
            var longest = -1;

            foreach (var loot in MarketData.Loot)
            {
                var fixture = MarketData.Fixture(loot.Id);
                foreach (var tile in MarketGrid.AdjacentWalkable(Grid, fixture))
                {
                    var path = Grid.FindPath(start, tile);
                    if (path == null)
                    {
                        continue;
                    }

                    if (path.Count > longest)
                    {
                        longest = path.Count;
                        farthest = loot;
                        farthestTile = tile;
                    }
                }
            }

            Assert.That(longest, Is.GreaterThan(0), "at least one loot aisle is reachable");
            var best = Grid.FindPath(start, farthestTile);
            Assert.That(best, Is.Not.Null, $"path to the farthest loot {farthest.Id} exists");
            foreach (var tile in best)
            {
                Assert.That(blocked.Contains(tile), Is.False, $"path to {farthest.Id} avoids fixture tile {tile}");
                Assert.That(Grid.IsWalkable(tile.X, tile.Y), Is.True, $"path tile {tile} is walkable");
            }

            AssertPathUsesOnlyDoorwayCrossings(best);
        }

        [Test]
        public void AllHorizontalAndVerticalExpresswaysAndCentralHubsAreCompletelyClear()
        {
            var blocked = MarketGrid.BlockedTiles();
            int[] horizontalExpressways = { 4, 13, 22 };
            foreach (var y in horizontalExpressways)
            {
                for (var x = 1; x < Grid.Width - 1; x++)
                {
                    Assert.That(blocked.Contains(new GridPos(x, y)), Is.False,
                        $"Horizontal expressway y={y} must not be blocked at ({x},{y})");
                    Assert.That(Grid.IsWalkable(x, y), Is.True,
                        $"Horizontal expressway tile ({x},{y}) must be walkable");
                }
            }

            int[] verticalExpressways = { 4, 13, 22, 31 };
            foreach (var x in verticalExpressways)
            {
                for (var y = 1; y < Grid.Height - 1; y++)
                {
                    Assert.That(blocked.Contains(new GridPos(x, y)), Is.False,
                        $"Vertical expressway x={x} must not be blocked at ({x},{y})");
                    Assert.That(Grid.IsWalkable(x, y), Is.True,
                        $"Vertical expressway tile ({x},{y}) must be walkable");
                }
            }

            foreach (var room in MarketData.Rooms)
            {
                for (var ly = 3; ly <= 5; ly++)
                {
                    for (var lx = 3; lx <= 5; lx++)
                    {
                        var gx = room.OriginI + lx;
                        var gy = room.OriginJ + ly;
                        Assert.That(blocked.Contains(new GridPos(gx, gy)), Is.False,
                            $"{room.Id} central hub tile ({gx},{gy}) must remain clear");
                    }
                }
            }
        }

        private static void AssertPathUsesOnlyDoorwayCrossings(List<GridPos> path)
        {
            for (var i = 0; i < path.Count - 1; i++)
            {
                var from = path[i];
                var to = path[i + 1];
                Assert.That(System.Math.Abs(from.X - to.X) + System.Math.Abs(from.Y - to.Y), Is.EqualTo(1), "path steps are orthogonal");
                if (!MarketGrid.SameRoom(from.X, from.Y, to.X, to.Y))
                {
                    Assert.That(
                        MarketGrid.IsDoorwayStep(from.X, from.Y, to.X, to.Y),
                        Is.True,
                        $"room crossing {from} to {to} uses the doorway band");
                }
            }
        }
    }
}
