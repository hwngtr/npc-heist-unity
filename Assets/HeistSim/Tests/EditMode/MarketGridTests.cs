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
            Assert.That(MarketGrid.BlockedTiles().Count, Is.EqualTo(295), "295 tiles are covered by solid fixtures after the panel lane move");
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
