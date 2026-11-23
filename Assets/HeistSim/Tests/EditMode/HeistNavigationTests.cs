using System;
using System.Collections.Generic;
using HeistSim.Market;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Mapping, arrow, minimap dot and camera smoothing checks for the market navigation aids.</summary>
    public sealed class HeistNavigationTests
    {
        private static readonly Vector2 Screen = new Vector2(1600f, 900f);
        private readonly List<Camera> _cameras = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var camera in _cameras)
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }

            _cameras.Clear();
        }

        [Test]
        public void EveryRoomMapsToItsOwnMinimapCell()
        {
            var cells = new HashSet<int>();
            for (var i = 0; i < MarketData.Rooms.Length; i++)
            {
                var room = MarketData.Rooms[i];
                var centre = new Vector2(
                    room.OriginI + MarketData.RoomTiles * 0.5f,
                    room.OriginJ + MarketData.RoomTiles * 0.5f);

                Assert.That(MinimapMapping.RoomIndexForTile(centre), Is.EqualTo(i), "centre of " + room.Id);
                Assert.That(MinimapMapping.CellColumn(room), Is.EqualTo(room.AtI), "column of " + room.Id);
                Assert.That(MinimapMapping.CellRow(room), Is.EqualTo(room.AtJ), "row of " + room.Id);
                cells.Add(MinimapMapping.CellColumn(room) * MarketData.Rows + MinimapMapping.CellRow(room));
            }

            Assert.That(cells.Count, Is.EqualTo(MarketData.Rooms.Length), "twelve distinct cells");
        }

        [Test]
        public void RoomCellRectsStayInsideTheMinimapGrid()
        {
            for (var i = 0; i < MarketData.Rooms.Length; i++)
            {
                var rect = MinimapMapping.CellRect(i);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0f), "cell " + i + " left");
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0f), "cell " + i + " bottom");
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(1f), "cell " + i + " right");
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(1f), "cell " + i + " top");
                Assert.That(rect.width, Is.GreaterThan(0f), "cell " + i + " width");
                Assert.That(rect.height, Is.GreaterThan(0f), "cell " + i + " height");
            }
        }

        [Test]
        public void TileOnARoomEdgeBelongsToTheAdjacentRoom()
        {
            var lower = MinimapMapping.RoomIndexForTile(new Vector2(9f, 9f));
            var origin = MarketData.Rooms[lower];
            Assert.That(origin.AtI, Is.EqualTo(1), "tile 9 crosses into column 1");
            Assert.That(origin.AtJ, Is.EqualTo(1), "tile 9 crosses into row 1");

            var first = MarketData.Rooms[MinimapMapping.RoomIndexForTile(Vector2.zero)];
            Assert.That(first.AtI, Is.EqualTo(0));
            Assert.That(first.AtJ, Is.EqualTo(0));

            var last = MarketData.Rooms[MinimapMapping.RoomIndexForTile(
                new Vector2(MarketData.WidthTiles - 0.001f, MarketData.HeightTiles - 0.001f))];
            Assert.That(last.AtI, Is.EqualTo(MarketData.Columns - 1));
            Assert.That(last.AtJ, Is.EqualTo(MarketData.Rows - 1));
        }

        [Test]
        public void TilesOutsideTheFloorplanClampToEdgeCells()
        {
            var below = MarketData.Rooms[MinimapMapping.ClampedRoomIndexForTile(new Vector2(-5f, -5f))];
            Assert.That(below.AtI, Is.EqualTo(0));
            Assert.That(below.AtJ, Is.EqualTo(0));

            var above = MarketData.Rooms[MinimapMapping.ClampedRoomIndexForTile(new Vector2(100f, 100f))];
            Assert.That(above.AtI, Is.EqualTo(MarketData.Columns - 1));
            Assert.That(above.AtJ, Is.EqualTo(MarketData.Rows - 1));

            var side = MarketData.Rooms[MinimapMapping.ClampedRoomIndexForTile(new Vector2(100f, 13.5f))];
            Assert.That(side.AtI, Is.EqualTo(MarketData.Columns - 1));
            Assert.That(side.AtJ, Is.EqualTo(1));
        }

        [Test]
        public void ActorDotsUseTheKnownSpawnTiles()
        {
            AssertDot(HeistGame.PlayerStart, 0, 0);
            AssertDot(HeistGame.GuardStart, 1, 0);
            AssertDot(HeistGame.StaffStart, 2, 0);
        }

        [Test]
        public void DeviceDotsUseTheCheckoutRoomCell()
        {
            AssertDot(Point(MarketData.Panel), MarketData.Columns - 1, MarketData.Rows - 1);
            AssertDot(Point(MarketData.Detagger), MarketData.Columns - 1, MarketData.Rows - 1);
            AssertDot(Point(MarketData.EasGate), MarketData.Columns - 1, MarketData.Rows - 1);
        }

        [Test]
        public void PowerArrowPointsTowardThePanel()
        {
            var player = HeistGame.PlayerStart;
            var target = Point(MarketData.Panel);
            var snapshot = Snapshot(player, "power");
            var camera = MakeCamera(player);

            Assert.That(ObjectiveTargets.Next(snapshot).Value.Label, Is.EqualTo("POWER"));
            AssertArrow(snapshot, camera, player, target, "POWER");
        }

        [Test]
        public void VitrineArrowPointsTowardTheSpiritsCabinet()
        {
            var player = Point(MarketData.EasGate);
            var target = MinimapMapping.FixtureTile("m_spirits");
            var snapshot = Snapshot(player, "vitrine");
            var camera = MakeCamera(player);

            Assert.That(ObjectiveTargets.Next(snapshot).Value.Tile, Is.EqualTo(target));
            AssertArrow(snapshot, camera, player, target, "VITRINE");
        }

        [Test]
        public void LootArrowSelectsTheRichestUnbaggedGood()
        {
            var player = Point(MarketData.EasGate);
            var first = Snapshot(player, "quota", Array.Empty<string>());
            var firstTarget = ObjectiveTargets.Next(first).Value;
            Assert.That(firstTarget.Label, Is.EqualTo("LOOT"));
            Assert.That(firstTarget.Tile, Is.EqualTo(MinimapMapping.FixtureTile("m_spirits")));
            AssertArrow(first, MakeCamera(player), player, firstTarget.Tile, "LOOT");

            var carried = new[] { "m_spirits" };
            var second = Snapshot(player, "quota", carried);
            var secondTarget = ObjectiveTargets.Next(second).Value;
            Assert.That(secondTarget.Tile, Is.EqualTo(MinimapMapping.FixtureTile("m_aktion1")));
        }

        [Test]
        public void ExitArrowPointsTowardTheEasGate()
        {
            var player = HeistGame.PlayerStart;
            var target = Point(MarketData.EasGate);
            var snapshot = Snapshot(player, "escape");
            var camera = MakeCamera(player);

            Assert.That(ObjectiveTargets.RequiredDone(snapshot), Is.True, "only escape remains");
            AssertArrow(snapshot, camera, player, target, "EXIT");
        }

        [Test]
        public void NextObjectiveTakesTheFirstIncompleteId()
        {
            var snapshot = SnapshotFor(HeistGame.PlayerStart, new[] { "vitrine", "quota" });
            var target = ObjectiveTargets.Next(snapshot);
            Assert.That(target.HasValue, Is.True);
            Assert.That(target.Value.Id, Is.EqualTo("vitrine"));
        }

        [Test]
        public void OnScreenObjectiveHidesTheArrow()
        {
            var player = Point(MarketData.Panel);
            var camera = MakeCamera(player);
            var snapshot = Snapshot(player, "power");
            var target = ObjectiveTargets.Next(snapshot).Value;

            var state = ObjectiveArrowMath.Evaluate(camera, player, target.Tile, Screen, target.Label);
            Assert.That(state.Visible, Is.False, "the panel is at the screen centre");
        }

        [Test]
        public void CameraSmoothingIsFiniteForExtremeDeltaTime()
        {
            var current = new Vector3(1f, 2f, 3f);
            var target = current + MarketRunner.CameraOffset;
            float[] deltas = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f, float.MaxValue, 1e30f };

            foreach (var dt in deltas)
            {
                var result = CameraFollowMath.Smooth(current, target, dt, MarketRunner.CameraSmoothing, MarketRunner.MaxDeltaTime);
                AssertFinite(result, "dt " + dt);
                Assert.That(result.x, Is.InRange(current.x, target.x), "x for dt " + dt);
                Assert.That(result.y, Is.InRange(current.y, target.y), "y for dt " + dt);
                Assert.That(result.z, Is.InRange(current.z, target.z), "z for dt " + dt);
            }

            Assert.That(CameraFollowMath.Smooth(current, target, float.NaN, 9f, 0.05f), Is.EqualTo(current));
            Assert.That(CameraFollowMath.Smooth(current, target, -2f, 9f, 0.05f), Is.EqualTo(current));
        }

        [Test]
        public void LerpFactorStaysBoundedForExtremeInputs()
        {
            float[] deltas = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f, float.MaxValue };
            foreach (var dt in deltas)
            {
                var factor = CameraFollowMath.LerpFactor(dt, MarketRunner.CameraSmoothing, MarketRunner.MaxDeltaTime);
                Assert.That(float.IsNaN(factor) || float.IsInfinity(factor), Is.False, "finite factor for dt " + dt);
                Assert.That(factor, Is.InRange(0f, 1f), "bounded factor for dt " + dt);
            }
        }

        [Test]
        public void CameraSmoothingConvergesWithoutOvershoot()
        {
            var current = Vector3.zero;
            var target = MarketRunner.CameraOffset;
            var previous = float.PositiveInfinity;
            for (var i = 0; i < 240; i++)
            {
                current = CameraFollowMath.Smooth(current, target, 0.2f, MarketRunner.CameraSmoothing, MarketRunner.MaxDeltaTime);
                var distance = Vector3.Distance(current, target);
                Assert.That(distance, Is.LessThanOrEqualTo(previous + 1e-6f), "distance decreases at step " + i);
                previous = distance;
            }

            Assert.That(Vector3.Distance(current, target), Is.LessThan(0.05f), "the camera settles on the target");
        }

        private void AssertArrow(HeistSnapshot snapshot, Camera camera, Vector2 player, Vector2 target, string label)
        {
            var state = ObjectiveArrowMath.Evaluate(camera, player, target, Screen, label);
            var viewport = camera.WorldToViewportPoint(new Vector3(target.x, 1f, target.y));
            var delta = new Vector2((viewport.x - 0.5f) * Screen.x, (viewport.y - 0.5f) * Screen.y);
            var expectedAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Assert.That(state.Visible, Is.True, label + " target is off screen");
            Assert.That(state.Label, Is.EqualTo(label));
            Assert.That(state.DistanceTiles, Is.EqualTo(Vector2.Distance(player, target)).Within(1e-5f), "tile distance");
            Assert.That(AngleDelta(state.AngleDegrees, expectedAngle), Is.LessThan(0.5f), label + " arrow angle");
            Assert.That(state.ScreenPosition.x, Is.InRange(0f, Screen.x), "arrow x on screen");
            Assert.That(state.ScreenPosition.y, Is.InRange(0f, Screen.y), "arrow y on screen");
        }

        private Camera MakeCamera(Vector2 player)
        {
            var cameraObject = new GameObject("Navigation Test Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = MarketRunner.FollowOrthographicSize;
            camera.aspect = Screen.x / Screen.y;
            camera.transform.position = new Vector3(player.x, 0f, player.y) + MarketRunner.CameraOffset;
            camera.transform.rotation = Quaternion.LookRotation(-MarketRunner.CameraOffset.normalized, Vector3.up);
            _cameras.Add(camera);
            return camera;
        }

        private static HeistSnapshot Snapshot(Vector2 player, string activeId, IReadOnlyList<string> bag = null)
        {
            return SnapshotFor(player, new[] { activeId }, bag);
        }

        private static HeistSnapshot SnapshotFor(Vector2 player, string[] activeIds, IReadOnlyList<string> bag = null)
        {
            var objectives = new List<HeistObjective>();
            foreach (var id in MarketData.ObjectiveIds)
            {
                objectives.Add(Objective(id, Array.IndexOf(activeIds, id) < 0));
            }

            return new HeistSnapshot(
                0,
                player,
                HeistGame.GuardStart,
                HeistGame.StaffStart,
                "patrol",
                GuardArchitecture.Fsm,
                0f,
                0,
                "case",
                0,
                0f,
                bag ?? Array.Empty<string>(),
                objectives,
                BackupPhase.None,
                null,
                HeistOutcome.None,
                false,
                0,
                0,
                false,
                false,
                0,
                0,
                null,
                0f,
                0f,
                ChannelResult.None,
                0,
                null,
                false,
                false,
                false,
                false,
                false,
                false,
                InteractOutcome.None);
        }

        private static HeistObjective Objective(string id, bool done)
        {
            var objective = new HeistObjective(id);
            if (done)
            {
                typeof(HeistObjective).GetProperty(nameof(HeistObjective.Done))?.SetValue(objective, true);
            }

            return objective;
        }

        private static void AssertDot(Vector2 tile, int column, int row)
        {
            var normalized = MinimapMapping.Normalized(tile);
            Assert.That(normalized.x, Is.EqualTo(tile.x / (float)MarketData.WidthTiles).Within(1e-6f), "dot x for " + tile);
            Assert.That(normalized.y, Is.EqualTo(tile.y / (float)MarketData.HeightTiles).Within(1e-6f), "dot y for " + tile);
            var room = MarketData.Rooms[MinimapMapping.RoomIndexForTile(tile)];
            Assert.That(room.AtI, Is.EqualTo(column), "dot column for " + tile);
            Assert.That(room.AtJ, Is.EqualTo(row), "dot row for " + tile);
        }

        private static Vector2 Point(LevelPoint point)
        {
            return new Vector2((float)point.I, (float)point.J);
        }

        private static void AssertFinite(Vector3 value, string context)
        {
            Assert.That(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z), Is.False, "no NaN for " + context);
            Assert.That(float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z), Is.False, "no infinity for " + context);
        }

        private static float AngleDelta(float a, float b)
        {
            var delta = Mathf.Abs(a - b) % 360f;
            return delta > 180f ? 360f - delta : delta;
        }
    }
}
