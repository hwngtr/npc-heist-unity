using System.Collections.Generic;
using HeistSim;
using HeistSim.Market;
using HeistSim.Market.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeistSim.Tests
{
    /// <summary>
    /// Checks that composed detail stays inside the tiles the sim blocks, that the CCTV panel is reachable,
    /// that enriched props meet their target part counts and that signage adds no world space text.
    /// </summary>
    public sealed class ArtPlacementTests
    {
        /// <summary>
        /// Slack for authored product helpers that overhang a fixture edge by a few hundredths.
        /// The named fixture tests below still check whole tiles exactly.
        /// </summary>
        private const float EdgeTolerance = 0.1f;

        private static readonly NavGrid Grid = MarketGrid.Build();

        [TestCase("hand-basket", 7)]
        [TestCase("crate-tower", 9)]
        [TestCase("seasonal-display", 7)]
        [TestCase("floral-stand", 10)]
        [TestCase("bakery-rack", 17)]
        public void EnrichedKindsMeetTheirTargetPartCounts(string kind, int target)
        {
            var parts = FixtureShapeData.Fixture(kind);
            Assert.That(parts.Length, Is.EqualTo(target), kind + " authored part count");

            var root = DetailFactory.BuildFixture(kind);
            try
            {
                Assert.That(ChildCount(root), Is.GreaterThanOrEqualTo(target), kind + " rendered part count");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EverySolidFixtureRendersInsideItsBlockedFootprint()
        {
            var violations = new System.Text.StringBuilder();
            foreach (var fixture in MarketData.Fixtures)
            {
                if (!MarketGrid.IsSolidFixture(fixture))
                {
                    continue;
                }

                var tiles = MarketGrid.FootprintTiles(fixture);
                if (tiles.Count == 0)
                {
                    continue;
                }

                var bounds = RenderBounds(fixture);
                var rect = TileRect(tiles);
                if (bounds.min.x < rect.xMin - EdgeTolerance || bounds.max.x > rect.xMax + EdgeTolerance
                    || bounds.min.z < rect.yMin - EdgeTolerance || bounds.max.z > rect.yMax + EdgeTolerance)
                {
                    violations.AppendLine(fixture.Id + " bounds x " + bounds.min.x.ToString("0.00") + ".." + bounds.max.x.ToString("0.00")
                        + " z " + bounds.min.z.ToString("0.00") + ".." + bounds.max.z.ToString("0.00")
                        + " tiles x " + rect.xMin + ".." + rect.xMax + " z " + rect.yMin + ".." + rect.yMax);
                }
            }

            Assert.That(violations.ToString(), Is.Empty, "fixtures outside their blocked tiles");
        }

        [TestCase("m_gondola1")]
        [TestCase("m_gondola2")]
        [TestCase("m_checkout1")]
        public void NamedFixturesAlignToWholeBlockedTiles(string id)
        {
            var fixture = MarketData.Fixture(id);
            var bounds = RenderBounds(fixture);
            var rect = TileRect(MarketGrid.FootprintTiles(fixture));
            Assert.That(Mathf.FloorToInt(bounds.min.x), Is.EqualTo((int)rect.xMin), id + " min tile x");
            Assert.That(Mathf.CeilToInt(bounds.max.x), Is.EqualTo((int)rect.xMax), id + " max tile x");
            Assert.That(Mathf.FloorToInt(bounds.min.z), Is.EqualTo((int)rect.yMin), id + " min tile z");
            Assert.That(Mathf.CeilToInt(bounds.max.z), Is.EqualTo((int)rect.yMax), id + " max tile z");
        }

        [Test]
        public void CctvPanelIsApproachableOnFootFromTheEntrance()
        {
            var entrance = MarketGrid.TileOf(MarketData.Entrance.I, MarketData.Entrance.J);
            Assert.That(Grid.IsWalkable(entrance.X, entrance.Y), Is.True, "entrance tile is walkable");

            var reached = ReachableFrom(entrance);
            var best = new GridPos(entrance.X, entrance.Y);
            var bestDistance = TileDistance(best, MarketData.Panel.I, MarketData.Panel.J);
            foreach (var tile in reached)
            {
                var distance = TileDistance(tile, MarketData.Panel.I, MarketData.Panel.J);
                if (distance < bestDistance)
                {
                    best = tile;
                    bestDistance = distance;
                }
            }

            Assert.That(bestDistance, Is.LessThanOrEqualTo(2.0), "a reachable tile lies inside PanelRange");
            Assert.That(Grid.FindPath(entrance, best), Is.Not.Null, "a path reaches the panel approach tile " + best);
            Assert.That(
                new Vector2((float)MarketData.Panel.I, (float)MarketData.Panel.J),
                Is.EqualTo(new Vector2(29.0f, 25.5f)),
                "the panel point stays at its authored location");
        }

        [Test]
        public void PanelMoveBlocksNoWaypointOrDoorway()
        {
            var host = MarketData.Fixture("t6a");
            var footprint = new HashSet<GridPos>(MarketGrid.FootprintTiles(host));
            Assert.That(footprint, Is.EquivalentTo(new[]
            {
                new GridPos(27, 21),
                new GridPos(28, 21),
                new GridPos(29, 21),
            }), "the moved checkout lane covers exactly three tiles at row 21");

            foreach (var waypoint in MarketData.GuardRoute)
            {
                Assert.That(Grid.IsWalkable(waypoint.X, waypoint.Y), Is.True, "guard waypoint " + waypoint + " stays walkable");
                Assert.That(footprint.Contains(waypoint), Is.False, "guard waypoint " + waypoint + " stays clear of t6a");
            }

            foreach (var waypoint in MarketData.StaffRoute)
            {
                Assert.That(Grid.IsWalkable(waypoint.X, waypoint.Y), Is.True, "staff waypoint " + waypoint + " stays walkable");
                Assert.That(footprint.Contains(waypoint), Is.False, "staff waypoint " + waypoint + " stays clear of t6a");
            }

            for (var column = 1; column < MarketData.Columns; column++)
            {
                var seam = column * MarketData.RoomTiles;
                for (var row = 0; row < MarketData.Rows; row++)
                {
                    var y = row * MarketData.RoomTiles + 4;
                    Assert.That(Grid.CanStep(seam - 1, y, seam, y), Is.True, "vertical doorway c" + column + " r" + row);
                    Assert.That(Grid.CanStep(seam, y, seam - 1, y), Is.True, "vertical doorway back c" + column + " r" + row);
                }
            }

            for (var row = 1; row < MarketData.Rows; row++)
            {
                var seam = row * MarketData.RoomTiles;
                for (var column = 0; column < MarketData.Columns; column++)
                {
                    var x = column * MarketData.RoomTiles + 4;
                    Assert.That(Grid.CanStep(x, seam - 1, x, seam), Is.True, "horizontal doorway r" + row + " c" + column);
                    Assert.That(Grid.CanStep(x, seam, x, seam - 1), Is.True, "horizontal doorway back r" + row + " c" + column);
                }
            }
        }

        [Test]
        public void GuardWaypointZeroMatchesTheWebStartPoint()
        {
            var first = MarketData.GuardRoute[0];
            var point = new Vector2(first.X + 0.5f + (float)first.OffsetI, first.Y + 0.5f);
            Assert.That(point, Is.EqualTo(new Vector2(7.0f, 4.5f)), "the detective starts at web market.js line 1079");

            for (var i = 1; i < MarketData.GuardRoute.Length; i++)
            {
                Assert.That(MarketData.GuardRoute[i].OffsetI, Is.EqualTo(0d), "later guard waypoints stay at tile centres");
            }
        }

        [Test]
        public void RebuiltSceneHasNoTextOutsideTheHudCanvas()
        {
            if (!System.IO.File.Exists(MarketSceneSpec.ScenePath))
            {
                Assert.Ignore("Market scene not built yet, run Heist/Market/Build Level Scene.");
            }

            var scene = EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Assert.That(IsUnderHud(text.transform), Is.True, text.name + " is inside the HUD canvas");
                }

            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool IsUnderHud(Transform node)
        {
            for (var at = node; at != null; at = at.parent)
            {
                if (at.name == MarketSceneSpec.RootHud)
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds RenderBounds(Fixture fixture)
        {
            var root = DetailFactory.BuildFixture(fixture.Kind, fixture.Face);
            try
            {
                var position = MarketLayout.ItemPosition(fixture);
                root.transform.position = new Vector3(position.x, 0f, position.y);
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0), fixture.Id + " has renderers");
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Rect TileRect(List<GridPos> tiles)
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            foreach (var tile in tiles)
            {
                minX = Mathf.Min(minX, tile.X);
                maxX = Mathf.Max(maxX, tile.X);
                minY = Mathf.Min(minY, tile.Y);
                maxY = Mathf.Max(maxY, tile.Y);
            }

            return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }

        private static HashSet<GridPos> ReachableFrom(GridPos start)
        {
            var seen = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in Grid.Neighbors(at.X, at.Y))
                {
                    if (seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return seen;
        }

        private static double TileDistance(GridPos tile, double i, double j)
        {
            var dx = tile.X + 0.5 - i;
            var dy = tile.Y + 0.5 - j;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        private static int ChildCount(GameObject root)
        {
            var count = 0;
            foreach (Transform child in root.transform)
            {
                count++;
            }

            return count;
        }
    }
}
