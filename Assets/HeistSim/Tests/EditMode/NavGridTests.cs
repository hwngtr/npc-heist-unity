using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;

namespace HeistSim.Tests
{
    /// <summary>Grid BFS parity: reachability, obstacle avoidance, optimality and the doorway band.</summary>
    public sealed class NavGridTests
    {
        [Test]
        public void PathExistsBetweenTwoOpenTiles()
        {
            var grid = Grid(5, 5);
            var path = grid.FindPath(new GridPos(0, 0), new GridPos(4, 4));

            Assert.That(path, Is.Not.Null);
            Assert.That(path[0], Is.EqualTo(new GridPos(0, 0)));
            Assert.That(path[path.Count - 1], Is.EqualTo(new GridPos(4, 4)));
            AssertPathIsOrthogonalAndOpen(grid, path);
        }

        [Test]
        public void PathAvoidsABlockedTile()
        {
            var grid = Grid(
                5,
                5,
                new GridPos(2, 0),
                new GridPos(2, 1),
                new GridPos(2, 2),
                new GridPos(2, 3));

            var path = grid.FindPath(new GridPos(0, 0), new GridPos(4, 0));

            Assert.That(path, Is.Not.Null);
            foreach (var tile in path)
            {
                Assert.That(tile.X == 2 && tile.Y < 4, Is.False, $"path crossed blocked tile {tile}");
            }

            Assert.That(path.Contains(new GridPos(2, 4)), Is.True);
            AssertPathIsOrthogonalAndOpen(grid, path);
        }

        [Test]
        public void NoPathWhenGoalIsWalledOff()
        {
            var grid = Grid(5, 5, new GridPos(3, 4), new GridPos(4, 3));

            var path = grid.FindPath(new GridPos(0, 0), new GridPos(4, 4));

            Assert.That(path, Is.Null);
        }

        [Test]
        public void PathLengthMatchesBfsOptimalityOnAnOpenGrid()
        {
            var grid = Grid(5, 5);

            var path = grid.FindPath(new GridPos(0, 0), new GridPos(4, 4));

            Assert.That(path.Count, Is.EqualTo(9));
        }

        [Test]
        public void PathLengthMatchesBfsOptimalityAroundAWall()
        {
            var grid = Grid(
                5,
                5,
                new GridPos(2, 0),
                new GridPos(2, 1),
                new GridPos(2, 2),
                new GridPos(2, 3));

            var path = grid.FindPath(new GridPos(0, 0), new GridPos(4, 0));

            Assert.That(path.Count, Is.EqualTo(13));
        }

        [Test]
        public void SameStartAndGoalReturnsSingleTile()
        {
            var grid = Grid(5, 5);

            var path = grid.FindPath(new GridPos(2, 2), new GridPos(2, 2));

            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path[0], Is.EqualTo(new GridPos(2, 2)));
        }

        [Test]
        public void DoorwayBandIsTheOnlyCrossingOfTheWall()
        {
            var blocked = new List<GridPos>();
            for (var y = 0; y < 5; y++)
            {
                if (y != 2)
                {
                    blocked.Add(new GridPos(4, y));
                }
            }

            var grid = Grid(9, 5, blocked.ToArray());
            var path = grid.FindPath(new GridPos(2, 1), new GridPos(7, 1));

            Assert.That(path, Is.Not.Null);
            Assert.That(path.Contains(new GridPos(4, 2)), Is.True);
            Assert.That(path.Count, Is.EqualTo(8));
            AssertPathIsOrthogonalAndOpen(grid, path);
        }

        [Test]
        public void EdgePredicateRestrictsCrossingToTheDoorwayBand()
        {
            var grid = new NavGrid(
                6,
                5,
                (x, y) => true,
                (fromX, _, toX, fromY) =>
                {
                    if (fromX == 3 && toX == 4)
                    {
                        return fromY == 2;
                    }

                    if (fromX == 4 && toX == 3)
                    {
                        return fromY == 2;
                    }

                    return true;
                });

            var path = grid.FindPath(new GridPos(1, 1), new GridPos(5, 1));

            Assert.That(path, Is.Not.Null);
            Assert.That(path.Contains(new GridPos(3, 2)), Is.True);
            Assert.That(path.Contains(new GridPos(4, 2)), Is.True);
            Assert.That(path.Count, Is.EqualTo(7));
        }

        [Test]
        public void NeighborsAreExposedInRightLeftUpDownOrder()
        {
            var grid = Grid(3, 3, new GridPos(1, 0));

            var neighbours = grid.Neighbors(1, 1);

            Assert.That(neighbours, Is.EqualTo(new[]
            {
                new GridPos(2, 1),
                new GridPos(0, 1),
                new GridPos(1, 2),
            }));
        }

        [Test]
        public void CornerTileHasOnlyTwoNeighbours()
        {
            var grid = Grid(3, 3);

            Assert.That(grid.Neighbors(0, 0).Count, Is.EqualTo(2));
        }

        private static NavGrid Grid(int width, int height, params GridPos[] blocked)
        {
            var wall = new HashSet<GridPos>(blocked);
            return new NavGrid(width, height, (x, y) => !wall.Contains(new GridPos(x, y)));
        }

        private static void AssertPathIsOrthogonalAndOpen(NavGrid grid, List<GridPos> path)
        {
            for (var i = 0; i < path.Count; i++)
            {
                Assert.That(grid.IsWalkable(path[i].X, path[i].Y), Is.True, $"tile {path[i]} not walkable");
                if (i == 0)
                {
                    continue;
                }

                var dx = System.Math.Abs(path[i].X - path[i - 1].X);
                var dy = System.Math.Abs(path[i].Y - path[i - 1].Y);
                Assert.That(dx + dy, Is.EqualTo(1), $"step {path[i - 1]} to {path[i]} is not orthogonal");
            }
        }
    }
}
