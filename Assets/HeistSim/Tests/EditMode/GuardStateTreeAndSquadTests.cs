using System.Collections.Generic;
using System.Diagnostics;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>Verifies StateTree macro transitions, HTN sabotage/intercept planning, occupancy search, and two-guard coordination.</summary>
    public sealed class GuardStateTreeAndSquadTests
    {
        private readonly List<GuardProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var profile in _profiles)
            {
                Object.DestroyImmediate(profile);
            }

            _profiles.Clear();
        }

        [Test]
        public void Guard2RouteIsWalkableAndTraversable()
        {
            var grid = MarketGrid.Build();
            var route = MarketData.Guard2Route;
            Assert.That(route.Length, Is.GreaterThanOrEqualTo(4), "Guard 2 route should cover multiple departments");

            foreach (var wp in route)
            {
                Assert.That(grid.IsWalkable(wp.X, wp.Y), Is.True, $"Guard 2 waypoint {wp} must be walkable");
            }

            for (var i = 0; i < route.Length; i++)
            {
                var from = route[i];
                var to = route[(i + 1) % route.Length];
                var path = grid.FindPath(from, to);
                Assert.That(path, Is.Not.Null, $"Guard 2 route segment {from} -> {to} must be traversable");
                Assert.That(path.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void StateTreeMacroTransitionsCoverRelaxedSuspiciousCombatSearchReturn()
        {
            var profile = NewStandard();
            var grid = MarketGrid.Build();
            var bb = new GuardSquadBlackboard(grid);
            var route = new[] { new Vector2(13.5f, 4.5f), new Vector2(20.5f, 4.5f) };
            var tree = new GuardStateTree(0, route, GuardAction.Patrol);

            // 1. Relaxed patrol
            var pRelaxed = new GuardPercepts(1, route[0], 0f, false, null, 10f, 0f, null, 0.1f, null, false, false, 100, 0);
            var r1 = tree.Tick(pRelaxed, profile, bb, grid);
            Assert.That(r1.MacroState, Is.EqualTo(GuardMacroState.Relaxed));
            Assert.That(r1.Action, Is.EqualTo(GuardAction.Patrol));

            // 2. Heard sound -> Suspicious
            var sound = new SoundEvent(new Vector2(16.5f, 4.5f), 1, 1f);
            var pSound = new GuardPercepts(2, route[0], 0f, false, null, 10f, 0f, sound, 0.2f, sound.Origin, false, false, 100, 0);
            var r2 = tree.Tick(pSound, profile, bb, grid);
            Assert.That(r2.MacroState, Is.EqualTo(GuardMacroState.Suspicious));
            Assert.That(r2.Action, Is.EqualTo(GuardAction.Investigate));

            // 3. Arrived and quiet -> Searching
            var pArrived = new GuardPercepts(3, sound.Origin, 0f, false, null, 10f, 0f, null, 0.2f, sound.Origin, false, true, 100, 0);
            var r3 = tree.Tick(pArrived, profile, bb, grid);
            Assert.That(r3.MacroState, Is.EqualTo(GuardMacroState.Searching));

            // 4. Spotted player -> Combat
            var pSeen = new GuardPercepts(4, route[0], 0f, true, new Vector2(15.5f, 4.5f), 2.0f, 0f, null, 0.5f, new Vector2(15.5f, 4.5f), false, false, 0, 0);
            var r4 = tree.Tick(pSeen, profile, bb, grid);
            Assert.That(r4.MacroState, Is.EqualTo(GuardMacroState.Combat));
            Assert.That(r4.Action, Is.EqualTo(GuardAction.Chase));

            // 5. Lost sight long -> Searching
            var pLost = new GuardPercepts(5, route[0], 0f, false, null, 8f, 0f, null, 0.5f, new Vector2(15.5f, 4.5f), false, false, profile.lost_sight_limit, 0);
            var r5 = tree.Tick(pLost, profile, bb, grid);
            Assert.That(r5.MacroState, Is.EqualTo(GuardMacroState.Searching));

            // 6. Search expired & calm -> Returning
            var pReturn = new GuardPercepts(6, new Vector2(16.5f, 4.5f), 0f, false, null, 8f, 0f, null, profile.alert_calm_threshold * 0.5f, null, false, false, 200, profile.search_duration_limit);
            var r6 = tree.Tick(pReturn, profile, bb, grid);
            Assert.That(r6.MacroState, Is.EqualTo(GuardMacroState.Returning));
            Assert.That(r6.Action, Is.EqualTo(GuardAction.Return));

            // 7. Reached route start -> Relaxed
            var pHome = new GuardPercepts(7, route[0], 0f, false, null, 8f, 0f, null, 0.1f, null, true, false, 200, 0);
            var r7 = tree.Tick(pHome, profile, bb, grid);
            Assert.That(r7.MacroState, Is.EqualTo(GuardMacroState.Relaxed));
        }

        [Test]
        public void OccupancyGridDiffusesAndClearsVisibleConeTiles()
        {
            var grid = MarketGrid.Build();
            var bb = new GuardSquadBlackboard(grid);

            bb.SeedSighting(new Vector2(13.5f, 4.5f), 10);
            Assert.That(bb.ProbabilityAt(13, 4), Is.EqualTo(1.0f).Within(0.001f));

            for (var i = 0; i < 4; i++)
            {
                bb.DiffuseStep();
            }

            Assert.That(bb.ProbabilityAt(14, 4), Is.GreaterThan(0.05f), "Probability should diffuse east down the walkable aisle");
            Assert.That(bb.ProbabilityAt(13, 4), Is.LessThan(1.0f), "Peak density spreads across neighbors");

            bb.ClearVisionCone(new Vector2(11.5f, 4.5f), 0f, 0.7f, 6.0f, (_, _) => true);
            Assert.That(bb.ProbabilityAt(13, 4), Is.EqualTo(0f), "Tiles inside an unobstructed vision cone must be cleared");
            Assert.That(bb.ProbabilityAt(14, 4), Is.EqualTo(0f), "Tiles along the cleared east corridor must also be cleared");
        }

        [Test]
        public void TwoStateTreeGuardsCoordinatePursuerAndExitInterceptorRoles()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetGuard(7.5f, 4.5f);
            game.SetGuard2(24.5f, 22.5f);
            game.TeleportPlayer(8.5f, 4.5f);
            game.SetHeat(0.60f);

            var g2Start = game.Guard2Position;
            for (var i = 0; i < 20; i++)
            {
                game.Step(1f / 30f, HeistInput.None);
            }

            var snap = game.Snapshot();
            Assert.That(snap.HasSecondGuard, Is.True);
            Assert.That(snap.Guard1Role, Is.EqualTo(GuardTacticalRole.PrimaryPursuer));
            Assert.That(snap.Guard2Role, Is.EqualTo(GuardTacticalRole.ExitInterceptor));
            Assert.That(snap.Guard1Plan, Is.EqualTo("DirectPursuit").Or.EqualTo("DirectEngage"));
            Assert.That(snap.Guard2Plan, Is.EqualTo("CutOffEasExit").Or.EqualTo("RespondToEasAlarm"));

            var easChoke = new Vector2(30.5f, 21.5f);
            Assert.That(
                Vector2.Distance(snap.Guard2, easChoke),
                Is.LessThan(Vector2.Distance(g2Start, easChoke)),
                "Guard 2 should move toward the EAS exit chokepoint to cut off escape");
        }

        [Test]
        public void HtnPlannerGeneratesAndCompletesSabotageInspectionPlans()
        {
            var profile = NewStandard();
            var grid = MarketGrid.Build();
            var bb = new GuardSquadBlackboard(grid) { CamerasDisabled = true };
            bb.AssignedRoles[0] = GuardTacticalRole.SabotageResponder;

            var panelPos = new Vector2((float)MarketData.Panel.I, (float)MarketData.Panel.J);
            var route = new[] { new Vector2(24.5f, 22.5f), new Vector2(31.5f, 22.5f) };
            var tree = new GuardStateTree(0, route, GuardAction.Patrol);

            var pPanel = new GuardPercepts(1, new Vector2(29.0f, 24.5f), 0f, false, null, 15f, 0f, null, 0.35f, null, false, false, 100, 0);
            var first = tree.Tick(pPanel, profile, bb, grid);
            Assert.That(first.PlanName, Is.EqualTo("InvestigateCctvSabotage"));
            Assert.That(bb.PanelInspected, Is.False);

            // Step while standing at panel until MoveToPowerPanel + InspectPowerPanel complete
            for (var t = 2; t <= 90; t++)
            {
                var p = new GuardPercepts(t, panelPos, 0f, false, null, 15f, 0f, null, 0.35f, null, false, false, 100 + t, 0);
                tree.Tick(p, profile, bb, grid);
                if (bb.PanelInspected)
                {
                    break;
                }
            }

            Assert.That(bb.PanelInspected, Is.True, "Completing InspectPowerPanel step marks CCTV panel inspected");
        }

        [Test]
        public void SecondGuardIndependentlyDetectsAndBustsPlayer()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetGuard(1.5f, 16.5f);
            game.SetGuard2(24.5f, 22.5f);
            game.TeleportPlayer(23.0f, 22.5f);
            game.SetHeat(0.95f);

            game.Step(1f / 60f, HeistInput.None);

            var snap = game.Snapshot();
            Assert.That(snap.HasSecondGuard, Is.True);
            Assert.That(snap.Visible, Is.True, "Guard 2 should detect the player within its west-facing cone");
            Assert.That(snap.Outcome, Is.EqualTo(HeistOutcome.Busted), "Tier 5 sight by Guard 2 under 2.4 tiles busts the thief");
        }

        [Test]
        public void CycleArchCyclesAcrossStateTreeFsmAndBtKeepingState()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetHeat(0.45f);
            game.TeleportPlayer(5.0f, 5.0f);
            game.Step(1f / 60f, HeistInput.None);
            var before = game.Snapshot();

            Assert.That(before.Arch, Is.EqualTo(GuardArchitecture.StateTree));

            game.CycleArch();
            var fsmSnap = game.Snapshot();
            Assert.That(fsmSnap.Arch, Is.EqualTo(GuardArchitecture.Fsm));
            Assert.That(fsmSnap.Heat, Is.EqualTo(before.Heat));
            Assert.That(fsmSnap.Guard, Is.EqualTo(before.Guard));
            Assert.That(fsmSnap.Guard2, Is.EqualTo(before.Guard2));

            game.CycleArch();
            Assert.That(game.Snapshot().Arch, Is.EqualTo(GuardArchitecture.Bt));

            game.CycleArch();
            Assert.That(game.Snapshot().Arch, Is.EqualTo(GuardArchitecture.StateTree));
            Assert.That(game.Snapshot().HasSecondGuard, Is.True);
        }

        [Test]
        public void TwoGuardStateTreeSimulationRunsDeterministicallyAndFast()
        {
            var profile = NewStandard();
            var g1 = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            var g2 = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++)
            {
                var input = i < 200
                    ? new HeistInput(1f, 0f, crouch: true)
                    : (i % 120 == 0 ? new HeistInput(0f, 0f, sprint: true) : HeistInput.None);
                g1.Step(1f / 30f, input);
                g2.Step(1f / 30f, input);
            }

            sw.Stop();
            var s1 = g1.Snapshot();
            var s2 = g2.Snapshot();

            Assert.That(s1.Guard, Is.EqualTo(s2.Guard), "Guard 1 position must be deterministic");
            Assert.That(s1.Guard2, Is.EqualTo(s2.Guard2), "Guard 2 position must be deterministic");
            Assert.That(s1.Heat, Is.EqualTo(s2.Heat), "Heat must be deterministic");
            Assert.That(s1.Guard1Role, Is.EqualTo(s2.Guard1Role), "Guard 1 tactical role must be deterministic");
            Assert.That(s1.Guard2Role, Is.EqualTo(s2.Guard2Role), "Guard 2 tactical role must be deterministic");
            Assert.That(sw.Elapsed.TotalSeconds, Is.LessThan(5.0), "1000 dual-guard StateTree steps should run well within 5s");
            TestContext.WriteLine($"TwoGuardStateTree 2x1000 ticks elapsed: {sw.Elapsed.TotalMilliseconds:0.1} ms");
        }

        [Test]
        public void GuardsRandomlyDetourToInspectUnvisitedStoreCorners()
        {
            var profile = NewStandard();
            var game = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            game.TeleportPlayer(1.5f, 4.5f);

            Assert.That(game.SquadBlackboard.CornerSpotCount, Is.GreaterThanOrEqualTo(12),
                "Blackboard should discover blind corner spots across the store");

            var g1Corners = 0;
            var g2Corners = 0;
            var distinctTargets = new HashSet<Vector2>();

            for (var i = 0; i < 900; i++)
            {
                game.Step(1f / 30f, HeistInput.None);
                if (game.LastStateTree1.PlanName == "InspectCorner")
                {
                    g1Corners++;
                    if (game.LastStateTree1.Intent.Target.HasValue)
                    {
                        distinctTargets.Add(game.LastStateTree1.Intent.Target.Value);
                    }
                }

                if (game.LastStateTree2.PlanName == "InspectCorner")
                {
                    g2Corners++;
                    if (game.LastStateTree2.Intent.Target.HasValue)
                    {
                        distinctTargets.Add(game.LastStateTree2.Intent.Target.Value);
                    }
                }
            }

            Assert.That(g1Corners, Is.GreaterThan(0), "Guard 1 should periodically detour to inspect a stale store corner");
            Assert.That(g2Corners, Is.GreaterThan(0), "Guard 2 should periodically detour to inspect a stale store corner");
            Assert.That(distinctTargets.Count, Is.GreaterThanOrEqualTo(2), "Guards should inspect multiple distinct store corners");

            // Reseeding with identical seed is deterministic; different seeds produce varied patrol detours
            var a1 = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            var a2 = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            var b1 = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            a1.ReseedSquad(0x11112222u);
            a2.ReseedSquad(0x11112222u);
            b1.ReseedSquad(0x99998888u);

            var diverged = false;
            for (var i = 0; i < 650; i++)
            {
                a1.Step(1f / 30f, HeistInput.None);
                a2.Step(1f / 30f, HeistInput.None);
                b1.Step(1f / 30f, HeistInput.None);
                Assert.That(a1.GuardPosition, Is.EqualTo(a2.GuardPosition), "Identical seed must keep Guard 1 deterministic");
                Assert.That(a1.Guard2Position, Is.EqualTo(a2.Guard2Position), "Identical seed must keep Guard 2 deterministic");
                if (Vector2.Distance(a1.GuardPosition, b1.GuardPosition) > 0.2f || Vector2.Distance(a1.Guard2Position, b1.Guard2Position) > 0.2f)
                {
                    diverged = true;
                }
            }

            Assert.That(diverged, Is.True, "Different PRNG seeds should vary which stale store corners guards inspect");
        }

        [Test]
        public void DirectionalDiffusionAndPredictiveFlankingTrackFleeingThief()
        {
            var grid = MarketGrid.Build();
            var bb = new GuardSquadBlackboard(grid);
            bb.SeedSighting(new Vector2(11.5f, 13.5f), new Vector2(3.6f, 0f), 10);
            for (var t = 11; t <= 18; t++)
            {
                bb.DiffuseStep(tick: t);
            }

            Assert.That(bb.ProbabilityAt(13, 13), Is.GreaterThan(bb.ProbabilityAt(9, 13)),
                "Directional occupancy diffusion should bias probability mass ahead along the thief's escape vector");
            var predicted = bb.PredictEscapePoint(4);
            Assert.That(predicted.x, Is.GreaterThan(11.5f),
                "Escape prediction should project ahead of the last confirmed tile along confirmed velocity");

            var profile = NewStandard();
            var game = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            game.PlayerSpeedScale = 0.84f;
            game.GuardSpeedScale = 1.22f;
            game.SetGuard(13.5f, 12.2f);
            game.SetGuard2(22.5f, 16.5f);
            game.TeleportPlayer(13.5f, 14.5f);

            var flankSeen = false;
            for (var i = 0; i < 24; i++)
            {
                game.Step(1f / 30f, new HeistInput(1f, 0f, false, false, false));
                if (game.LastStateTree1.PlanName == "FlankCutoff" || game.LastStateTree2.PlanName == "FlankCutoff")
                {
                    flankSeen = true;
                }
            }

            Assert.That(game.SquadBlackboard.LastConfirmedVelocity.x, Is.GreaterThan(1.0f),
                "Guard sight should record positive eastward thief velocity");

            for (var i = 0; i < 90; i++)
            {
                var move = i < 45
                    ? new HeistInput(1f, 0f, false, false, false)
                    : new HeistInput(0f, -1f, false, false, false);
                game.Step(1f / 30f, move);
                if (game.LastStateTree1.PlanName == "FlankCutoff" || game.LastStateTree2.PlanName == "FlankCutoff")
                {
                    flankSeen = true;
                }
            }

            var minDist = Mathf.Min(
                Vector2.Distance(game.GuardPosition, game.PlayerPosition),
                Vector2.Distance(game.Guard2Position, game.PlayerPosition));
            Assert.That(flankSeen, Is.True, "Second guard should coordinate a FlankCutoff intercept against a fleeing thief");
            Assert.That(minDist, Is.LessThan(5.0f), "Live speed rebalance and predictive intercept should keep pressure close on a walking thief");
        }

        [Test]
        public void PathAwareFlankingAndRadioRelayContainFleeingThiefThroughDoorways()
        {
            var grid = MarketGrid.Build();
            var bb = new GuardSquadBlackboard(grid);
            bb.GuardPositions[0] = new Vector2(10.5f, 13.5f);
            bb.GuardPositions[1] = new Vector2(21.5f, 22.5f);

            var target = new Vector2(13.5f, 13.5f);
            var vel = new Vector2(3.5f, 0f);
            bb.SeedSighting(target, vel, 100);

            var flank = bb.ComputeFlankInterceptTarget(1, target, vel);
            var fx = (int)Mathf.Floor(flank.x);
            var fy = (int)Mathf.Floor(flank.y);
            Assert.That(grid.IsWalkable(fx, fy), Is.True, "Computed flank target must land on a walkable tile");
            Assert.That(grid.FindPath(new GridPos(21, 22), new GridPos(fx, fy)), Is.Not.Null,
                "Path-aware flank cutoff must be reachable from the flanking guard via doorways");
            Assert.That(flank.x, Is.GreaterThan(target.x),
                "Flank intercept must lead ahead down the thief's escape lane rather than stalling behind");

            // Radio relay preserves pursuer/interceptor coordination briefly after sight breaks
            bb.UpdateSquadRoles(tick: 120, activeGuardCount: 2, g0Sees: false, g1Sees: false, playerPos: new Vector2(14.5f, 13.5f));
            Assert.That(bb.HasRecentSighting(120, GuardSquadBlackboard.RecentContactWindowTicks), Is.True);
            Assert.That(bb.AssignedRoles[0], Is.EqualTo(GuardTacticalRole.PrimaryPursuer));
            Assert.That(bb.AssignedRoles[1], Is.EqualTo(GuardTacticalRole.ExitInterceptor));
        }

        [Test]
        public void PowerCutTakesLongerAndEmitsArcAndTripNoise()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetGuard(1.5f, 16.5f);
            game.SetGuard2(13.5f, 4.5f);
            game.FrozenPatrol = true;
            game.TeleportPlayer((float)MarketData.Panel.I, (float)MarketData.Panel.J);

            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            game.Step(1f / 30f, hold);

            var startSnap = game.Snapshot();
            Assert.That(startSnap.ChannelKind, Is.EqualTo("power"));
            Assert.That(startSnap.ChannelHold, Is.EqualTo(HeistGame.PowerHold).Within(0.01f));
            Assert.That(HeistGame.PowerHold, Is.GreaterThanOrEqualTo(4.5f), "Disabling CCTV must take meaningful time");

            var sawMidArc = false;
            var initialHeat = startSnap.Heat;
            for (var i = 0; i < 240 && !game.Snapshot().CamerasDisabled; i++)
            {
                game.Step(1f / 30f, hold);
                if (game.ActiveSoundOrigin.HasValue && game.ActiveSoundMaxRadius >= 5.5f && !game.Snapshot().CamerasDisabled)
                {
                    sawMidArc = true;
                }
            }

            var endSnap = game.Snapshot();
            Assert.That(sawMidArc, Is.True, "Sawing the CCTV panel must emit loud mid-channel electrical arc warnings");
            Assert.That(endSnap.CamerasDisabled, Is.True, "Completing the hold disables all CCTV cameras");
            Assert.That(endSnap.Heat, Is.GreaterThan(initialHeat + 0.10f), "Breaker trip must noticeably spike suspicion heat");
            Assert.That(game.ActiveSoundMaxRadius, Is.GreaterThanOrEqualTo(9.0f), "Breaker trip must emit a wide alarm sound");
        }

        [Test]
        public void GuardRestoresDisabledCamerasAfterTimedReboot()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetGuard(1.5f, 16.5f);
            game.SetGuard2(29.0f, 24.5f);
            CutPowerAndSlipOut(game);

            var observedRestoring = false;
            var restoringTicks = 0;
            for (var i = 0; i < 420; i++)
            {
                game.Step(1f / 30f, new HeistInput(0f, 0f, crouch: true));
                var s = game.Snapshot();
                if (s.CctvRestoring)
                {
                    observedRestoring = true;
                    restoringTicks++;
                }

                if (!s.CamerasDisabled && s.CctvRestoreCount > 0)
                {
                    break;
                }
            }

            var finalSnap = game.Snapshot();
            Assert.That(observedRestoring, Is.True, "Guard must visibly enter the timed CCTV reboot phase");
            Assert.That(restoringTicks, Is.GreaterThanOrEqualTo(120), "Rebooting CCTV must take sustained guard time");
            Assert.That(finalSnap.CamerasDisabled, Is.False, "Security guard must bring CCTV cameras back online");
            Assert.That(finalSnap.CctvRestoreCount, Is.EqualTo(1));
        }

        [Test]
        public void InterruptingGuardDuringCctvRebootLeavesCamerasOffline()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetGuard(1.5f, 16.5f);
            game.SetGuard2(29.0f, 24.5f);
            CutPowerAndSlipOut(game);

            var reachedReboot = false;
            for (var i = 0; i < 200; i++)
            {
                game.Step(1f / 30f, new HeistInput(0f, 0f, crouch: true));
                if (game.Snapshot().CctvRestoring)
                {
                    reachedReboot = true;
                    break;
                }
            }

            Assert.That(reachedReboot, Is.True, "Guard should reach the panel reboot step before interruption");

            // Reveal the thief directly inside Guard 2's close proximity / corridor so Combat preempts the reboot
            game.TeleportPlayer(31.2f, 23.0f);
            game.SetGuard2(31.2f, 23.8f);
            for (var i = 0; i < 6; i++)
            {
                game.Step(1f / 30f, HeistInput.None);
            }

            var interruptedSnap = game.Snapshot();
            Assert.That(interruptedSnap.Guard2State, Is.EqualTo("chase").Or.EqualTo("attack"));
            Assert.That(interruptedSnap.CctvRestoring, Is.False, "Pursuing the thief interrupts the CCTV panel reboot");
            Assert.That(interruptedSnap.CamerasDisabled, Is.True, "Interrupted reboot must leave CCTV cameras offline");
        }

        [Test]
        public void RepeatCctvSabotageTriggersSecondGuardRestoration()
        {
            var game = new HeistGame(NewStandard(), GuardArchitecture.StateTree, guardCount: 2);
            game.SetGuard(1.5f, 16.5f);
            game.SetGuard2(29.0f, 24.5f);
            CutPowerAndSlipOut(game);

            for (var i = 0; i < 420 && game.Snapshot().CamerasDisabled; i++)
            {
                game.Step(1f / 30f, new HeistInput(0f, 0f, crouch: true));
            }

            Assert.That(game.Snapshot().CamerasDisabled, Is.False);
            Assert.That(game.Snapshot().CctvRestoreCount, Is.EqualTo(1));

            // Cut power a second time and verify security dispatches and restores it again
            CutPowerAndSlipOut(game);
            Assert.That(game.Snapshot().CamerasDisabled, Is.True, "Second sabotage cuts cameras again");

            for (var i = 0; i < 480 && game.Snapshot().CamerasDisabled; i++)
            {
                game.Step(1f / 30f, new HeistInput(0f, 0f, crouch: true));
            }

            var secondSnap = game.Snapshot();
            Assert.That(secondSnap.CamerasDisabled, Is.False, "Guards must restore CCTV after repeat sabotage too");
            Assert.That(secondSnap.CctvRestoreCount, Is.EqualTo(2));
        }

        [Test]
        public void ChaseMovementHeadsStraightWithoutBacktrackOrStall()
        {
            var profile = NewStandard();
            var game = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 1);
            game.PlayerSpeedScale = 0.86f;
            game.SetGuard(11.5f, 13.5f);
            game.TeleportPlayer(14.2f, 13.5f);

            // Step along the open East doorway corridor (j=13.5) until guard spots thief and enters chase state
            var chasing = false;
            for (var i = 0; i < 20; i++)
            {
                game.Step(1f / 30f, new HeistInput(1f, 0f, sprint: true));
                if (game.Snapshot().GuardState == "chase")
                {
                    chasing = true;
                    break;
                }
            }

            Assert.That(chasing, Is.True, "Guard should spot thief in the same open aisle");

            // Continue fleeing East along clear corridor j=13.5 through doorway i=18.0 into r12 -> chase must move straight with zero spin or stall
            var prevPos = game.GuardPosition;
            var prevDir = Vector2.zero;
            var prevAngle = game.GuardAngle;
            var minStepDist = float.MaxValue;
            var minDirDot = 1f;
            var maxAngleJumpDeg = 0f;

            for (var i = 0; i < 30; i++)
            {
                game.Step(1f / 30f, new HeistInput(1f, 0f, sprint: true));
                Assert.That(game.Snapshot().GuardState, Is.EqualTo("chase"), "Guard should remain in active chase along open corridor");
                var pos = game.GuardPosition;
                var delta = pos - prevPos;
                var stepDist = delta.magnitude;
                if (stepDist < minStepDist)
                {
                    minStepDist = stepDist;
                }

                if (stepDist > 1e-4f)
                {
                    var dir = delta / stepDist;
                    if (prevDir != Vector2.zero)
                    {
                        var dot = Vector2.Dot(dir, prevDir);
                        if (dot < minDirDot)
                        {
                            minDirDot = dot;
                        }
                    }

                    prevDir = dir;
                }

                var angleJump = Mathf.Abs(Mathf.DeltaAngle(game.GuardAngle * Mathf.Rad2Deg, prevAngle * Mathf.Rad2Deg));
                if (angleJump > maxAngleJumpDeg)
                {
                    maxAngleJumpDeg = angleJump;
                }

                prevPos = pos;
                prevAngle = game.GuardAngle;
            }

            Assert.That(minStepDist, Is.GreaterThan(0.04f), "Guard must not stall at tile centers during chase");
            Assert.That(minDirDot, Is.GreaterThan(0.55f), "Guard chase path must not reverse or zigzag back and forth");
            Assert.That(maxAngleJumpDeg, Is.LessThan(35f), "Guard facing angle must turn smoothly without 1-tick spins");
        }

        [Test]
        public void TwoGuardsPassEachOtherSmoothlyWithoutBlockingOrRoleThrashing()
        {
            var profile = NewStandard();
            var game = new HeistGame(profile, GuardArchitecture.StateTree, guardCount: 2);
            game.PlayerSpeedScale = 0.86f;
            // Start both guards 0.22 tiles apart behind player along the open j=13.5 corridor to test close pass-through & role hysteresis
            game.SetGuard(11.2f, 13.45f);
            game.SetGuard2(11.4f, 13.55f);
            game.TeleportPlayer(14.4f, 13.5f);

            var pursuerFlips = 0;
            var prevPursuer = -1;
            var minSep = float.MaxValue;
            var minGuardStep = float.MaxValue;
            var measuredSteps = 0;
            var prevG1 = game.GuardPosition;
            var prevG2 = game.Guard2Position;

            for (var i = 0; i < 36; i++)
            {
                game.Step(1f / 30f, new HeistInput(1f, 0f, sprint: true));
                var snap = game.Snapshot();
                var roles = game.SquadBlackboard.AssignedRoles;
                var pursuer = roles[0] == GuardTacticalRole.PrimaryPursuer
                    ? 0
                    : (roles[1] == GuardTacticalRole.PrimaryPursuer ? 1 : -1);
                if (prevPursuer >= 0 && pursuer >= 0 && pursuer != prevPursuer)
                {
                    pursuerFlips++;
                }

                if (pursuer >= 0)
                {
                    prevPursuer = pursuer;
                }

                var sep = Vector2.Distance(game.GuardPosition, game.Guard2Position);
                if (sep < minSep)
                {
                    minSep = sep;
                }

                var s1 = Vector2.Distance(game.GuardPosition, prevG1);
                var s2 = Vector2.Distance(game.Guard2Position, prevG2);
                if (i >= 3
                    && snap.Outcome == HeistOutcome.None
                    && snap.GuardState != "attack"
                    && snap.Guard2State != "attack"
                    && (snap.GuardState == "chase" || snap.GuardState == "investigate")
                    && (snap.Guard2State == "chase" || snap.Guard2State == "investigate"))
                {
                    minGuardStep = Mathf.Min(minGuardStep, Mathf.Min(s1, s2));
                    measuredSteps++;
                }

                prevG1 = game.GuardPosition;
                prevG2 = game.Guard2Position;
            }

            Assert.That(minSep, Is.LessThan(0.80f), "Close-quarters pass-through threshold must engage during test");
            Assert.That(measuredSteps, Is.GreaterThanOrEqualTo(15), "Both guards must actively pursue along the shared corridor");
            Assert.That(pursuerFlips, Is.LessThanOrEqualTo(1), "Equidistant guards must keep a sticky pursuer assignment instead of role thrashing");
            Assert.That(minGuardStep, Is.GreaterThan(0.04f), "Converging guards must steer cleanly past each other without blocking");
        }

        private static void CutPowerAndSlipOut(HeistGame game)
        {
            game.SetHeat(0.12f);
            game.TeleportPlayer((float)MarketData.Panel.I, (float)MarketData.Panel.J);
            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            for (var i = 0; i < 240 && !game.Snapshot().CamerasDisabled; i++)
            {
                // Keep both guards clear during the hold (outside LOS down the x=29 aisle) so only post-cut reboot runs under test.
                game.SetGuard(1.5f, 16.5f);
                game.SetGuard2(13.5f, 4.5f);
                game.Step(1f / 30f, hold);
            }

            Assert.That(game.Snapshot().CamerasDisabled, Is.True, "Helper should disable CCTV cameras");
            game.TeleportPlayer(2.0f, 4.5f);
            game.SetGuard2(31.5f, 23.5f);
        }

        private GuardProfile NewStandard()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _profiles.Add(profile);
            return profile;
        }
    }
}
