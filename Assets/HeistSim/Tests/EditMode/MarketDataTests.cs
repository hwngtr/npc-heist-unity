using System.Collections.Generic;
using HeistSim;
using NUnit.Framework;

namespace HeistSim.Tests
{
    /// <summary>Parity checks for the ported Frischemarkt level data, mirroring verify-market-collision.mjs.</summary>
    public sealed class MarketDataTests
    {
        private static readonly NavGrid Grid = MarketGrid.Build();

        [Test]
        public void LevelCountsMatchTheBrowserGame()
        {
            Assert.That(MarketData.Rooms.Length, Is.EqualTo(12), "house.rooms has 12 rooms");
            Assert.That(MarketData.Fixtures.Length, Is.EqualTo(78), "house.items has 78 placed fixtures");
            Assert.That(MarketData.Loot.Length, Is.EqualTo(16), "MARKET_LOOT has 16 targets");
            Assert.That(MarketData.Cameras.Length, Is.EqualTo(6), "MARKET_CAMERAS has 6 cameras");
            Assert.That(MarketData.MotionZones.Length, Is.EqualTo(3), "motionZones has 3 zones");
            Assert.That(MarketData.GuardRoute.Length, Is.EqualTo(12), "guardRoute has 12 waypoints");
            Assert.That(MarketData.StaffRoute.Length, Is.EqualTo(5), "staffRoute has 5 waypoints");
            Assert.That(MarketData.Quota, Is.EqualTo(190), "quota is 190");
            Assert.That(MarketData.BagCapacity, Is.EqualTo(6), "bagCap is 6");
        }

        [Test]
        public void EveryFixtureAndLootKindExists()
        {
            foreach (var fixture in MarketData.Fixtures)
            {
                Assert.That(MarketData.Kinds.ContainsKey(fixture.Kind), Is.True, $"{fixture.Id} kind {fixture.Kind} is defined");
            }

            foreach (var loot in MarketData.Loot)
            {
                Assert.That(MarketData.Kinds.ContainsKey(loot.Kind), Is.True, $"{loot.Id} kind {loot.Kind} is defined");
            }
        }

        [Test]
        public void EveryRouteWaypointIsWalkable()
        {
            foreach (var waypoint in MarketData.GuardRoute)
            {
                Assert.That(Grid.IsWalkable(waypoint.X, waypoint.Y), Is.True, $"guard waypoint {waypoint} is walkable");
            }

            foreach (var waypoint in MarketData.StaffRoute)
            {
                Assert.That(Grid.IsWalkable(waypoint.X, waypoint.Y), Is.True, $"staff waypoint {waypoint} is walkable");
            }
        }

        [Test]
        public void BothRoutesAreTraversableEndToEnd()
        {
            AssertRouteIsTraversable("guard", MarketData.GuardRoute);
            AssertRouteIsTraversable("staff", MarketData.StaffRoute);
        }

        [Test]
        public void EveryLootTileIsReachableFromTheEntrance()
        {
            var entrance = MarketGrid.TileOf(MarketData.Entrance.I, MarketData.Entrance.J);
            Assert.That(Grid.IsWalkable(entrance.X, entrance.Y), Is.True, "entrance tile is walkable");

            foreach (var loot in MarketData.Loot)
            {
                var fixture = MarketData.Fixture(loot.Id);
                var aisle = MarketGrid.AdjacentWalkable(Grid, fixture);
                Assert.That(aisle.Count, Is.GreaterThan(0), $"{loot.Id} has a walkable aisle");

                var reachable = false;
                foreach (var tile in aisle)
                {
                    if (Grid.FindPath(entrance, tile) != null)
                    {
                        reachable = true;
                        break;
                    }
                }

                Assert.That(reachable, Is.True, $"{loot.Id} in room {loot.Room} is reachable from the entrance");
            }
        }

        [Test]
        public void EveryRoomDoorwayStaysCrossable()
        {
            for (var boundary = 1; boundary < MarketData.Columns; boundary++)
            {
                for (var row = 0; row < MarketData.Rows; row++)
                {
                    var y = row * MarketData.RoomTiles + 4;
                    var left = boundary * MarketData.RoomTiles - 1;
                    var right = boundary * MarketData.RoomTiles;
                    Assert.That(
                        Grid.CanStep(left, y, right, y),
                        Is.True,
                        $"vertical doorway at x={boundary * MarketData.RoomTiles}, row {row} is crossable");
                    Assert.That(
                        Grid.CanStep(right, y, left, y),
                        Is.True,
                        $"vertical doorway at x={boundary * MarketData.RoomTiles}, row {row} is crossable backwards");
                }
            }

            for (var boundary = 1; boundary < MarketData.Rows; boundary++)
            {
                for (var column = 0; column < MarketData.Columns; column++)
                {
                    var x = column * MarketData.RoomTiles + 4;
                    var top = boundary * MarketData.RoomTiles - 1;
                    var bottom = boundary * MarketData.RoomTiles;
                    Assert.That(
                        Grid.CanStep(x, top, x, bottom),
                        Is.True,
                        $"horizontal doorway at y={boundary * MarketData.RoomTiles}, column {column} is crossable");
                    Assert.That(
                        Grid.CanStep(x, bottom, x, top),
                        Is.True,
                        $"horizontal doorway at y={boundary * MarketData.RoomTiles}, column {column} is crossable backwards");
                }
            }
        }

        [Test]
        public void EveryStandLayerFixtureIsSolidAndPassablePortalsStayOpen()
        {
            var blocked = MarketGrid.BlockedTiles();
            foreach (var fixture in MarketData.Fixtures)
            {
                var tiles = MarketGrid.FootprintTiles(fixture);
                if (tiles.Count == 0)
                {
                    continue;
                }

                if (MarketGrid.IsSolidFixture(fixture))
                {
                    Assert.That(
                        blocked.Contains(tiles[0]),
                        Is.True,
                        $"{fixture.Id} ({fixture.Kind}) is solid at footprint centre {tiles[0]}");
                }
                else
                {
                    Assert.That(
                        blocked.Contains(tiles[0]),
                        Is.False,
                        $"{fixture.Id} ({fixture.Kind}) is a passable portal");
                }
            }
        }

        [Test]
        public void PassableFixtureSetMatchesTheBrowserGame()
        {
            Assert.That(MarketData.PassableFixtures.Count, Is.EqualTo(4));
            Assert.That(MarketData.PassableFixtures.Contains("eas-gate"), Is.True);
            Assert.That(MarketData.PassableFixtures.Contains("rewe-arch"), Is.True);
            Assert.That(MarketData.PassableFixtures.Contains("aisle-sign"), Is.True);
            Assert.That(MarketData.PassableFixtures.Contains("store-sign"), Is.True);
        }

        [Test]
        public void EveryCameraSitsInsideItsRoomRectangle()
        {
            foreach (var camera in MarketData.Cameras)
            {
                var room = MarketData.Room(camera.Room);
                Assert.That(camera.I, Is.GreaterThanOrEqualTo(room.OriginI), $"{camera.Id} I is inside {camera.Room}");
                Assert.That(camera.I, Is.LessThan(room.OriginI + MarketData.RoomTiles), $"{camera.Id} I is inside {camera.Room}");
                Assert.That(camera.J, Is.GreaterThanOrEqualTo(room.OriginJ), $"{camera.Id} J is inside {camera.Room}");
                Assert.That(camera.J, Is.LessThan(room.OriginJ + MarketData.RoomTiles), $"{camera.Id} J is inside {camera.Room}");

                var cell = MarketGrid.TileOf(camera.I, camera.J);
                var cellRoom = RoomIdAt(cell.X, cell.Y);
                Assert.That(camera.Room, Is.EqualTo(cellRoom), $"{camera.Id} room matches the room cell under its lens");
                Assert.That(camera.HalfAngle, Is.GreaterThan(0.0));
                Assert.That(camera.Range, Is.GreaterThan(0.0));
            }
        }

        [Test]
        public void EntranceDetaggerAndEasGateAreUsable()
        {
            var entrance = MarketGrid.TileOf(MarketData.Entrance.I, MarketData.Entrance.J);
            Assert.That(Grid.IsWalkable(entrance.X, entrance.Y), Is.True, "the entrance arch opening is walkable");

            var eas = MarketGrid.TileOf(MarketData.EasGate.I, MarketData.EasGate.J);
            Assert.That(Grid.IsWalkable(eas.X, eas.Y), Is.True, "the EAS gate walkthrough is passable");

            var detagger = MarketGrid.NearestWalkable(Grid, MarketData.Detagger.I, MarketData.Detagger.J);
            Assert.That(detagger.HasValue, Is.True, "the de-tagger has a walkable tile nearby");
            var detaggerDistance = DistanceTo(detagger.Value, MarketData.Detagger.I, MarketData.Detagger.J);
            Assert.That(detaggerDistance, Is.LessThanOrEqualTo(2.3), "the de-tagger is inside the 2.3 tile interaction range");
        }

        [Test]
        public void PanelSitsOnTheCheckoutLaneWithAWalkableAisle()
        {
            var panelTile = MarketGrid.TileOf(MarketData.Panel.I, MarketData.Panel.J);
            var host = MarketData.Fixture("t6a");
            var footprint = MarketGrid.FootprintTiles(host);
            Assert.That(footprint.Contains(panelTile), Is.True, "the panel is mounted on checkout lane t6a");
            Assert.That(MarketGrid.BlockedTiles().Contains(panelTile), Is.True, "the panel point is inside a solid fixture");

            var aisle = MarketGrid.AdjacentWalkable(Grid, host);
            Assert.That(aisle.Count, Is.GreaterThan(0), "the panel host has a walkable aisle");

            var nearest = MarketGrid.NearestWalkable(Grid, MarketData.Panel.I, MarketData.Panel.J);
            Assert.That(nearest.HasValue, Is.True, "the panel has a walkable tile nearby");
            var distance = DistanceTo(nearest.Value, MarketData.Panel.I, MarketData.Panel.J);
            Assert.That(distance, Is.EqualTo(2.5).Within(1e-9), "the nearest walkable tile centre is 2.5 tiles from the panel");
        }

        [Test]
        public void RfTagCountAndQuotaMatchTheBrowserGame()
        {
            Assert.That(MarketData.Quota, Is.EqualTo(190));
            Assert.That(MarketData.RfTagCount(), Is.EqualTo(10), "ten loot entries are RF tagged");
            Assert.That(MarketData.Loot.Length - MarketData.RfTagCount(), Is.EqualTo(6), "six loot entries are untagged");

            var taggedIds = new HashSet<string>();
            foreach (var loot in MarketData.Loot)
            {
                if (loot.Tagged)
                {
                    taggedIds.Add(loot.Id);
                }
            }

            foreach (var id in new[] { "m_spirits", "m_aktion1", "m_pallet1", "m_chill1", "m_aktion2", "m_checkout1", "m_season", "m_cage", "m_prosecco", "m_ham" })
            {
                Assert.That(taggedIds.Contains(id), Is.True, $"{id} carries an RF tag");
            }
        }

        [Test]
        public void VitrineIsTheOnlyGatedLoot()
        {
            var gated = new List<string>();
            foreach (var loot in MarketData.Loot)
            {
                if (loot.VitrineGated)
                {
                    gated.Add(loot.Id);
                }
            }

            Assert.That(gated.Count, Is.EqualTo(1), "exactly one loot entry is vitrine gated");
            Assert.That(gated[0], Is.EqualTo("m_spirits"));
            var spirits = MarketData.LootFor("m_spirits");
            Assert.That(spirits.HasValue, Is.True);
            Assert.That(spirits.Value.Kind, Is.EqualTo("liquor-cabinet"));
            Assert.That(spirits.Value.Room, Is.EqualTo("r4"));
            Assert.That(spirits.Value.RoomName, Is.EqualTo("Drogerie & Tiefkühl"), "the loot table keeps its own room name for the spirits");
        }

        [Test]
        public void EveryLootRoomMatchesItsCarryingFixture()
        {
            foreach (var loot in MarketData.Loot)
            {
                var fixture = MarketData.Fixture(loot.Id);
                Assert.That(loot.Room, Is.EqualTo(fixture.Room), $"{loot.Id} room matches its fixture");
                Assert.That(loot.I, Is.EqualTo(fixture.I), $"{loot.Id} I matches its fixture");
                Assert.That(loot.J, Is.EqualTo(fixture.J), $"{loot.Id} J matches its fixture");
                Assert.That(loot.Kind, Is.EqualTo(fixture.Kind), $"{loot.Id} kind matches its fixture");
                Assert.That(loot.Value, Is.EqualTo(fixture.Price), $"{loot.Id} value matches the fixture price");
            }
        }

        private static void AssertRouteIsTraversable(string name, GridPos[] route)
        {
            for (var i = 0; i < route.Length - 1; i++)
            {
                var path = Grid.FindPath(route[i], route[i + 1]);
                Assert.That(path, Is.Not.Null, $"{name} route leg {i} {route[i]} to {route[i + 1]} is traversable");
            }

            var closure = Grid.FindPath(route[route.Length - 1], route[0]);
            Assert.That(closure, Is.Not.Null, $"{name} route loops back to its start");
        }

        private static double DistanceTo(GridPos tile, double i, double j)
        {
            var dx = tile.X + 0.5 - i;
            var dy = tile.Y + 0.5 - j;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        private static string RoomIdAt(int x, int y)
        {
            foreach (var room in MarketData.Rooms)
            {
                if (x >= room.OriginI && x < room.OriginI + MarketData.RoomTiles
                    && y >= room.OriginJ && y < room.OriginJ + MarketData.RoomTiles)
                {
                    return room.Id;
                }
            }

            return string.Empty;
        }
    }
}
