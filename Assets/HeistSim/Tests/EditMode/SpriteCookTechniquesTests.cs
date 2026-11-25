using HeistSim.Market2D;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>
    /// Verifies the exact SpriteCook technique invariants implemented in Market2D:
    /// T01 Trail Canvas, T09 Mood LUT, T10 See-Through Bayer Dither, T13 2D Shadowcasting,
    /// T14 Status/I-Frames, T15 8-Mode Camera Rig, T17 Heavy Hit Feel, T18 Loot Rarity UI,
    /// and T19 84-Tick Screen Transitions.
    /// </summary>
    public sealed class SpriteCookTechniquesTests
    {
        [Test]
        public void T01_TrailCanvasBootDotStaysWorldAnchoredAndFadesIn85Frames()
        {
            var go = new GameObject("TestTrail");
            try
            {
                var trail = go.AddComponent<Market2DTrailSurface>();
                trail.EnsureInitialized();

                // Frame 0: stamp boot contact at (100, 50) + offset (3, 0) -> world (103, 50)
                trail.StepCanvasTick(new Vector2Int(100, 50), bootContact: true, new Vector2Int(3, 0));
                Assert.That(trail.SampleWorldCell(103, 50), Is.EqualTo(255), "Boot dot should start at full 255");

                // Walk away so canvas scrolls; world cell (103, 50) must stay anchored and fade by 3/tick
                for (var f = 1; f <= 20; f++)
                {
                    trail.StepCanvasTick(new Vector2Int(100 + f, 50), bootContact: false, Vector2Int.zero);
                }

                var expectedAfter20 = 255 - 20 * Market2DTrailSurface.FadePerTick;
                Assert.That(trail.SampleWorldCell(103, 50), Is.EqualTo(expectedAfter20),
                    "Boot dot must stay at its world cell while canvas scrolls");

                // Finish fading to frame 85 -> must be 0
                for (var f = 21; f <= 85; f++)
                {
                    trail.StepCanvasTick(new Vector2Int(125, 50), bootContact: false, Vector2Int.zero);
                }

                Assert.That(trail.SampleWorldCell(103, 50), Is.EqualTo(0), "Boot dot must be 0 after 85 frames");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T09_RisoMoodLutRow0IsIdentityAndRowsShiftCleanly()
        {
            var tex = Market2DPostFx.BuildRisoMoodLutTexture();
            try
            {
                Assert.That(tex.width, Is.EqualTo(32));
                Assert.That(tex.height, Is.EqualTo(4));
                for (var x = 0; x < tex.width; x++)
                {
                    var p0 = tex.GetPixel(x, 0);
                    Assert.That(p0.r, Is.EqualTo(1f).Within(0.01f));
                    Assert.That(p0.g, Is.EqualTo(1f).Within(0.01f));
                    Assert.That(p0.b, Is.EqualTo(1f).Within(0.01f));
                }

                // Lockdown shadow (row 3, x=0) shifts cooler/darker than daylight
                var shadowLockdown = tex.GetPixel(0, 3);
                Assert.That(shadowLockdown.b, Is.GreaterThan(shadowLockdown.r));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void T10_SeeThroughEaseAndBayer4x4DitheredFadeMatchSpec()
        {
            // Linear ease over 0.2s = 12 frames at 60fps
            var amt = 0f;
            for (var i = 0; i < 6; i++) amt = Market2DSeeThrough.MoveAmount(amt, 1f, 1f / 60f, 0.2f);
            Assert.That(amt, Is.EqualTo(0.5f).Within(0.001f));
            for (var i = 0; i < 6; i++) amt = Market2DSeeThrough.MoveAmount(amt, 1f, 1f / 60f, 0.2f);
            Assert.That(amt, Is.EqualTo(1f).Within(0.001f));

            // DitheredFade (keep = 0.375) inside clear radius must keep EXACTLY 6 of 16 cells in a 4x4 Bayer block
            var kept = 0;
            var center = new Vector2(2f, 2f);
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    if (Market2DSeeThrough.ShouldKeepPixel(x, y, center, 1f, 18f, 16f, 0.375f))
                    {
                        kept++;
                    }
                }
            }

            Assert.That(kept, Is.EqualTo(6), "DitheredFade keep=0.375 must keep exactly 6 of 16 Bayer cells");

            // Pure Dither (keep = 0): inside clear radius keeps 0, outside radius+soft keeps all
            Assert.That(Market2DSeeThrough.ShouldKeepPixel(2, 2, center, 1f, 18f, 16f, 0f), Is.False);
            Assert.That(Market2DSeeThrough.ShouldKeepPixel(50, 2, center, 1f, 18f, 16f, 0f), Is.True);
        }

        [Test]
        public void T13_ShadowcastMatchesExactSpriteCookCellCountsAndWallBlocking()
        {
            static int CountSeen(bool[,] seen)
            {
                var c = 0;
                for (var y = 0; y < seen.GetLength(0); y++)
                    for (var x = 0; x < seen.GetLength(1); x++)
                        if (seen[y, x]) c++;
                return c;
            }

            var empty11 = new bool[11, 11];
            Assert.That(CountSeen(Shadowcast2D.Cast(empty11, 11, 11, 5, 5, 3)), Is.EqualTo(25));
            var r4 = Shadowcast2D.Cast(empty11, 11, 11, 5, 5, 4);
            Assert.That(CountSeen(r4), Is.EqualTo(45));
            Assert.That(r4[5, 8], Is.True);
            Assert.That(r4[5, 9], Is.False);
            Assert.That(CountSeen(Shadowcast2D.Cast(empty11, 11, 11, 5, 5, 5)), Is.EqualTo(69));

            // One wall at (7,5), radius 6 -> 106 cells, (7,5) seen, (8..10,5) blocked
            var oneWall = new bool[11, 11];
            oneWall[5, 7] = true;
            var sWall = Shadowcast2D.Cast(oneWall, 11, 11, 5, 5, 6);
            Assert.That(CountSeen(sWall), Is.EqualTo(106));
            Assert.That(sWall[5, 7], Is.True);
            Assert.That(sWall[5, 8], Is.False);
            Assert.That(sWall[5, 9], Is.False);
            Assert.That(sWall[5, 10], Is.False);

            // 7x7 all walls, origin (3,3), radius 3 -> exactly 9 cells
            var allWalls = new bool[7, 7];
            for (var y = 0; y < 7; y++)
                for (var x = 0; x < 7; x++)
                    allWalls[y, x] = true;
            Assert.That(CountSeen(Shadowcast2D.Cast(allWalls, 7, 7, 3, 3, 3)), Is.EqualTo(9));

            // 6x6 empty, origin (0,0), radius 4 -> 15 cells, no out-of-bounds
            Assert.That(CountSeen(Shadowcast2D.Cast(new bool[6, 6], 6, 6, 0, 0, 4)), Is.EqualTo(15));

            // Wall down column 5 blocks (2,5) -> (8,5); opening (5,5) restores sight
            var colWall = new bool[11, 11];
            for (var y = 0; y < 11; y++) colWall[y, 5] = true;
            Assert.That(Shadowcast2D.Cast(colWall, 11, 11, 2, 5, 10)[5, 8], Is.False);
            colWall[5, 5] = false;
            Assert.That(Shadowcast2D.Cast(colWall, 11, 11, 2, 5, 10)[5, 8], Is.True);

            // Fog-of-war explored never loses a cell
            var explored = new bool[11, 11];
            Shadowcast2D.UpdateExplored(explored, r4);
            var before = CountSeen(explored);
            Shadowcast2D.UpdateExplored(explored, new bool[11, 11]);
            Assert.That(CountSeen(explored), Is.EqualTo(before));
        }

        [Test]
        public void T14_StatusFramesWindowIgnoresMidWindowRetriggerAndRestoresOnTick90()
        {
            var go = new GameObject("TestStatus");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.9f, 0.8f, 0.7f, 1f);
                var sf = new Market2DStatusFrames { DurationTicks = 90 };

                Assert.That(sf.Trigger(sr, StatusFrameLook.Blink), Is.True);
                Assert.That(sf.IsActive, Is.True);

                for (var i = 0; i < 30; i++) sf.StepTick();
                Assert.That(sf.Trigger(sr, StatusFrameLook.Blink), Is.False, "Hit inside 90-tick window must not restart");

                for (var i = 30; i < 90; i++) sf.StepTick();
                Assert.That(sf.IsActive, Is.False, "Window must end on tick 90");
                Assert.That(sr.enabled, Is.True, "Visibility must be restored");
                Assert.That(sr.color, Is.EqualTo(new Color(0.9f, 0.8f, 0.7f, 1f)));

                Assert.That(sf.Trigger(sr, StatusFrameLook.FlashRed), Is.True, "Hit on tick 90 must land");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T15_CameraRigDeadZoneLookAheadAndSmoothMatchSpec()
        {
            var rig = new Market2DCameraRig
            {
                Mode = CameraFollowMode.DeadZone,
                DeadZoneHalfSize = new Vector2(22f, 18f),
            };
            rig.Snap(Vector2.zero, 4.2f);
            Assert.That(rig.Step(new Vector2(20f, 0f), 1f / 60f, 4.2f), Is.EqualTo(Vector2.zero));
            Assert.That(rig.Step(new Vector2(40f, 40f), 1f / 60f, 4.2f), Is.EqualTo(new Vector2(18f, 22f)));

            // Look-ahead after 1s of walking right -> 32 * (1 - exp(-3.4)) ~= 30.93
            rig.Mode = CameraFollowMode.LookAhead;
            rig.LookAheadDistance = 32f;
            rig.LookAheadRate = 3.4f;
            rig.Snap(Vector2.zero, 4.2f);
            var pos = Vector2.zero;
            for (var i = 1; i <= 60; i++)
            {
                pos = rig.Step(new Vector2(i, 0f), 1f / 60f, 4.2f);
            }

            Assert.That(pos.x - 60f, Is.InRange(30.0f, 32.0f));

            // Rate 0 snaps immediately, never freezes
            Assert.That(Market2DCameraRig.ExpAlpha(0f, 1f / 60f), Is.EqualTo(1f));
        }

        [Test]
        public void T17_HitFeelShakeKnockbackHitStopAndKillSlowMoMatchSpec()
        {
            // Shake x sequence: 3, -2, 2, -1, 1, -1, 0, 0, 0, 0
            int[] expectedX = { 3, -2, 2, -1, 1, -1, 0, 0, 0, 0 };
            for (var k = 0; k < expectedX.Length; k++)
            {
                Assert.That(Market2DHitFeel.ComputeShakePx(k, 3f, 10, 0f).x, Is.EqualTo(expectedX[k]), "Shake tick " + k);
            }

            // Knockback over 8 ticks sums to 12.0 px, first tick ~3.96 px
            var sum = 0f;
            var first = Market2DHitFeel.KnockbackStepPx(1, 8, 12f);
            Assert.That(first, Is.EqualTo(3.9609f).Within(0.01f));
            for (var k = 1; k <= 8; k++) sum += Market2DHitFeel.KnockbackStepPx(k, 8, 12f);
            Assert.That(sum, Is.EqualTo(12f).Within(0.001f));

            // Killing blow: 9-tick hit stop, then 0.3x slow-mo for 12 ticks, then 1.0x
            var go = new GameObject("TestHitFeel");
            try
            {
                var hf = go.AddComponent<Market2DHitFeel>();
                hf.TriggerHit(null, Vector3.zero, Vector2.right, isKill: true);
                Assert.That(hf.IsHitStopped, Is.True);
                for (var t = 1; t <= 9; t++)
                {
                    hf.StepTick();
                    Assert.That(hf.IsHitStopped, Is.True, "Should remain frozen on tick " + t);
                }

                // Tick 10 (stop + 1): unfreezes and enters 0.3x slow-mo
                hf.StepTick();
                Assert.That(hf.IsHitStopped, Is.False);
                Assert.That(hf.SimTimeScale, Is.EqualTo(0.3f).Within(0.001f));

                for (var s = 0; s < 12; s++) hf.StepTick();
                Assert.That(hf.SimTimeScale, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T18_LootRarityBorderColorsMatchTiers()
        {
            Assert.That(Market2DHudController.ResolveLootRarityBorder("+€18", false).r, Is.EqualTo(0.369f).Within(0.01f));
            Assert.That(Market2DHudController.ResolveLootRarityBorder("+€34", false).g, Is.EqualTo(0.490f).Within(0.01f));
            Assert.That(Market2DHudController.ResolveLootRarityBorder("+€54", false).b, Is.EqualTo(0.769f).Within(0.01f));
            Assert.That(Market2DHudController.ResolveLootRarityBorder("+€54 [RF]", true).r, Is.EqualTo(0.427f).Within(0.01f));
            Assert.That(Market2DHudController.ResolveLootRarityBorder("+€95 [VITRINE]", true).r, Is.EqualTo(0.851f).Within(0.01f));
        }

        [Test]
        public void T19_ScreenTransitionSwapsAtTick34UnderBlackAndEndsAtTick84()
        {
            var go = new GameObject("TestPostFx");
            try
            {
                go.AddComponent<Camera>();
                var fx = go.AddComponent<Market2DPostFx>();
                var swappedAt = -1;

                Assert.That(fx.BeginTransition(() => swappedAt = fx.TransitionTick), Is.True);
                Assert.That(fx.BeginTransition(() => { }), Is.False, "Duplicate request must be ignored");

                for (var t = 1; t <= Market2DPostFx.TotalTransitionTicks; t++)
                {
                    fx.StepTransitionTick();
                    if (t == 34)
                    {
                        Assert.That(swappedAt, Is.EqualTo(34));
                        Assert.That(fx.TransitionProgress, Is.EqualTo(1f).Within(0.001f));
                    }
                }

                Assert.That(fx.IsTransitioning, Is.False);
                Assert.That(fx.TransitionProgress, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void T20_SpriteStackFootprintLiftAndSliceOrderingMatchSpec()
        {
            // Reference 15 slices of 24x43: squash 1, turn 0 -> width 24, height 43 + 14 = 57
            var straight = SpriteStack2D.ComputeFootprintPx(24, 43, 15, turnDeg: 0f, squash: 1f, spacing: 1f);
            Assert.That(straight.x, Is.EqualTo(24f).Within(0.01f));
            Assert.That(straight.y, Is.EqualTo(57f).Within(0.01f));

            // Squash 0.75 is shorter than squash 1
            var squashed = SpriteStack2D.ComputeFootprintPx(24, 43, 15, turnDeg: 0f, squash: 0.75f, spacing: 1f);
            Assert.That(squashed.y, Is.LessThan(straight.y));

            // Turned 90 degrees: width equals slice height (43)
            var turned = SpriteStack2D.ComputeFootprintPx(24, 43, 15, turnDeg: 90f, squash: 0.75f, spacing: 1f);
            Assert.That(turned.x, Is.EqualTo(43f).Within(0.01f));

            // Spacing 2 taller than spacing 1; spacing 0 & squash 1 draws flat (height == 43)
            var tall = SpriteStack2D.ComputeFootprintPx(24, 43, 15, turnDeg: 0f, squash: 1f, spacing: 2f);
            var flat = SpriteStack2D.ComputeFootprintPx(24, 43, 15, turnDeg: 0f, squash: 1f, spacing: 0f);
            Assert.That(tall.y, Is.GreaterThan(straight.y));
            Assert.That(flat.y, Is.EqualTo(43f).Within(0.01f));

            // Runtime slice builder creates exact slice count with monotonically increasing sortingOrder
            var go = new GameObject("TestStack");
            Texture2D sheet = null;
            try
            {
                sheet = SpriteStack2D.BuildRisoCctvSliceSheet(out var count);
                var stack = go.AddComponent<SpriteStack2D>();
                stack.sliceSheet = sheet;
                stack.sliceCount = count;
                stack.baseSortingOrder = 3000;
                stack.RebuildSlices();
                Assert.That(stack.ActiveSliceCount, Is.EqualTo(count));
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (sheet != null) Object.DestroyImmediate(sheet);
            }
        }

        [Test]
        public void StaminaFatigueAndHaulWeightScaleWithBagCount()
        {
            var stamina = new Market2DStamina();
            Assert.That(Market2DStamina.HaulSpeedMultiplier(0), Is.EqualTo(1f).Within(0.001f));
            Assert.That(Market2DStamina.HaulSpeedMultiplier(6), Is.LessThan(Market2DStamina.HaulSpeedMultiplier(3)));
            Assert.That(Market2DStamina.HaulSpeedMultiplier(6), Is.GreaterThanOrEqualTo(0.72f), "Heavy bag slows gently, never sluggish");
            Assert.That(Market2DStamina.SprintDrainPerSecond(6), Is.GreaterThan(Market2DStamina.SprintDrainPerSecond(0)));

            // Drain to 0 -> becomes Winded and blocks sprint until >= 25%
            var windedTriggered = false;
            for (var i = 0; i < 60; i++)
            {
                var (_, _, justWinded) = stamina.Step(wantsSprint: true, moving: true, crouching: false, bagCount: 4, dt: 0.1f);
                if (justWinded) windedTriggered = true;
            }

            Assert.That(windedTriggered, Is.True);
            Assert.That(stamina.IsWinded, Is.True);
            var (allowWhileWinded, _, _) = stamina.Step(wantsSprint: true, moving: true, crouching: false, bagCount: 4, dt: 0.1f);
            Assert.That(allowWhileWinded, Is.False);

            // Rest 1.5s -> recovers past 25% and clears Winded
            for (var i = 0; i < 15; i++)
            {
                stamina.Step(wantsSprint: false, moving: false, crouching: false, bagCount: 4, dt: 0.1f);
            }

            Assert.That(stamina.IsWinded, Is.False);
        }

        [Test]
        public void UnseenPlayerWithMultipleRfTagsCoolsDownFrom100AndGuardExitsSearch()
        {
            var profile = GuardProfile.CreateDefault("standard");
            var game = new HeistGame(profile, GuardArchitecture.Fsm);

            // Simulate worst-case deadlock: heat spiked to 100% (Exposure)
            game.SetHeat(1.0f);
            Assert.That(game.Snapshot().Heat, Is.GreaterThanOrEqualTo(0.99f));

            // Stand still out of sight for 20 seconds (600 ticks at 30 Hz)
            var idle = new HeistInput(0f, 0f, false, false, false);
            for (var i = 0; i < 600; i++)
            {
                game.Step(1f / 30f, idle);
            }

            var after = game.Snapshot();
            Assert.That(after.Heat, Is.LessThan(profile.alert_calm_threshold),
                "Exposure must decay below alert_calm_threshold when standing still out of sight");
            Assert.That(after.GuardState, Is.Not.EqualTo("search"),
                "Guard must not remain deadlocked in Search once player is unseen and heat cools");
        }

        [Test]
        public void AllSixWallMountedCctvCamerasCastVisibleVisionCones()
        {
            foreach (var cam in MarketData.Cameras)
            {
                var maxDist = 0f;
                var sumDist = 0f;
                const int rays = 11;
                for (var r = 0; r < rays; r++)
                {
                    var t = r / (float)(rays - 1);
                    var a = (float)(cam.BaseAngle - cam.HalfAngle + cam.HalfAngle * 2.0 * t);
                    var d = Shadowcast2D.CastCctvRayDistance((float)cam.I, (float)cam.J, a, (float)cam.Range);
                    if (d > maxDist) maxDist = d;
                    sumDist += d;
                }

                Assert.That(maxDist, Is.GreaterThanOrEqualTo(3.0f), cam.Id + " fan must reach across the aisle");
                Assert.That(sumDist / rays, Is.GreaterThanOrEqualTo(1.5f), cam.Id + " average fan depth must be clearly visible");
            }
        }
    }
}
