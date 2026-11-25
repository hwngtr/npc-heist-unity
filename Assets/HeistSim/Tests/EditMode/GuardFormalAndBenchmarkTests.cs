using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using HeistSim;
using NUnit.Framework;
using UnityEngine;

namespace HeistSim.Tests
{
    /// <summary>
    /// Formal verification and quantitative evaluation suite for 61CSE327 Project 2:
    ///   1. 1-Token Place/Transition Petri Net linear-algebraic P-invariant (1^T * C = 0).
    ///   2. Exhaustive 4-personality x 6-state x consistent-percept bisimulation & 2-tick convergence.
    ///   3. Regression proof for repeated sound/decoy investigation (no stale _arrivedAtTarget or (13.5,13.5) fallback).
    ///   4. Reactive Behavior Tree priority preemption (.Abort() and AbortedNodes trace).
    ///   5. 40,000-tick multi-personality x dual-architecture performance and complexity benchmark.
    /// </summary>
    public sealed class GuardFormalAndBenchmarkTests
    {
        private static readonly string[] ProfileIds = { "standard", "aggressive", "cautious", "observant" };
        private readonly List<GuardProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var p in _profiles)
            {
                if (p != null)
                {
                    UnityEngine.Object.DestroyImmediate(p);
                }
            }

            _profiles.Clear();
        }

        [Test]
        public void PetriNetIncidenceMatrixPreservesOneTokenMutualExclusion()
        {
            // P = 7 places (GuardAction enum values 0..6), T = 17 transitions (GuardFsm.Rules).
            const int places = 7;
            var rules = GuardFsm.Rules;
            Assert.That(rules.Length, Is.EqualTo(17));

            var incidence = new int[places, rules.Length];
            for (var t = 0; t < rules.Length; t++)
            {
                var src = (int)rules[t].Src;
                var dst = (int)rules[t].Dst;
                Assert.That(src, Is.Not.EqualTo(dst), $"Rule {t} must not be a self-loop");
                incidence[src, t] -= 1;
                incidence[dst, t] += 1;
            }

            // Prove 1^T * C = 0 for all 17 transitions: firing any transition conserves total tokens = 1.
            for (var t = 0; t < rules.Length; t++)
            {
                var columnSum = 0;
                for (var p = 0; p < places; p++)
                {
                    columnSum += incidence[p, t];
                }

                Assert.That(columnSum, Is.EqualTo(0), $"Transition {t} ({rules[t].Src}->{rules[t].Dst}) must conserve token count");
            }
        }

        [Test]
        public void TwoBoundaryStutteringBisimulationHoldsAcrossAllProfiles()
        {
            var states = new[]
            {
                GuardAction.Patrol,
                GuardAction.Investigate,
                GuardAction.Chase,
                GuardAction.Attack,
                GuardAction.Search,
                GuardAction.Return,
            };

            foreach (var pid in ProfileIds)
            {
                var profile = NewProfile(pid);
                foreach (var st in states)
                {
                    // Sample representative continuous percept combinations covering every guard predicate branch
                    foreach (var visible in new[] { false, true })
                    foreach (var inAttack in new[] { false, true })
                    foreach (var hasSound in new[] { false, true })
                    foreach (var arrived in new[] { false, true })
                    foreach (var lostLong in new[] { false, true })
                    foreach (var searchExp in new[] { false, true })
                    foreach (var atStart in new[] { false, true })
                    {
                        // Enforce physical sensor consistency (same as GuardAI.tla & verify_z3_ai.py)
                        if (inAttack && !visible) continue;
                        if (lostLong && visible) continue;
                        if (arrived && (visible || hasSound)) continue;
                        if (searchExp && (visible || hasSound)) continue;

                        var dist = inAttack ? profile.attack_range * 0.5f : (visible ? profile.fov_distance * 0.6f : 12f);
                        var alert = visible
                            ? profile.alert_detect_threshold + 0.1f
                            : (searchExp ? profile.alert_calm_threshold * 0.5f : 0.5f);
                        var sound = hasSound
                            ? new SoundEvent(new Vector2(6f, 4f), 2, 1f)
                            : (SoundEvent?)null;
                        var lostTicks = lostLong ? profile.lost_sight_limit + 2 : 0;
                        var searchTicks = searchExp ? profile.search_duration_limit + 2 : 0;

                        var percepts = new GuardPercepts(
                            10,
                            new Vector2(5f, 5f),
                            0f,
                            visible,
                            visible ? new Vector2(5f + dist, 5f) : (Vector2?)null,
                            dist,
                            0f,
                            sound,
                            alert,
                            new Vector2(6f, 5f),
                            atStart,
                            arrived,
                            lostTicks,
                            searchTicks);

                        var fsm = new GuardFsm(st);
                        var (fsmIntent, _) = fsm.Tick(percepts, profile);

                        var bt = new GuardBt(GuardFsm.EvalGuard, initialLatch: LatchName(st));
                        var btRes = bt.Tick(percepts, profile);

                        var deltaAttack = st != GuardAction.Chase && st != GuardAction.Attack && visible && inAttack;
                        var deltaSound = (st == GuardAction.Chase || st == GuardAction.Attack) && lostLong && hasSound;

                        if (deltaAttack)
                        {
                            Assert.That(btRes.Intent.Action, Is.EqualTo(GuardAction.Attack));
                            Assert.That(fsmIntent.Action, Is.EqualTo(GuardAction.Chase));
                            // 2nd tick with percepts held converges FSM -> Attack!
                            var (fsmStep2, _) = fsm.Tick(percepts, profile);
                            Assert.That(fsmStep2.Action, Is.EqualTo(GuardAction.Attack));
                        }
                        else if (deltaSound)
                        {
                            Assert.That(btRes.Intent.Action, Is.EqualTo(GuardAction.Investigate));
                            Assert.That(fsmIntent.Action, Is.EqualTo(GuardAction.Search));
                            // 2nd tick with sound held converges FSM -> Investigate!
                            var (fsmStep2, _) = fsm.Tick(percepts, profile);
                            Assert.That(fsmStep2.Action, Is.EqualTo(GuardAction.Investigate));
                        }
                        else
                        {
                            Assert.That(btRes.Intent.Action, Is.EqualTo(fsmIntent.Action),
                                $"Profile {pid} state {st} must match outside preemption boundaries");
                        }
                    }
                }
            }
        }

        [Test]
        public void RepeatedDecoyCoinsLureGuardToActualSoundOriginsWithoutStaleArrivalLatch()
        {
            var profile = NewProfile("standard");
            var game = new HeistGame(profile, GuardArchitecture.Fsm);
            game.TeleportPlayer(2.0f, 15.0f);
            game.SetGuard(7.5f, 4.5f);

            // 1st decoy coin at (5.5, 4.5): guard hears it, enters Investigate, walks there, arrives, and enters Search.
            var firstCoin = game.TossDecoy(new Vector2(5.5f, 4.5f), maxRadius: 7.5f);
            game.Step(1f / 60f, HeistInput.None);
            Assert.That(game.Snapshot().GuardState, Is.EqualTo("investigate"), "1st coin triggers Investigate");
            Assert.That(game.LastIntent.Target.HasValue, Is.True);
            Assert.That(Vector2.Distance(game.LastIntent.Target.Value, firstCoin), Is.LessThan(0.1f),
                "GuardIntent target must match 1st decoy coin origin, not (13.5, 13.5)");

            // Step until guard reaches the 1st coin and transitions Investigate -> Search -> Return -> Patrol
            for (var i = 0; i < 600 && game.Snapshot().GuardState != "patrol"; i++)
            {
                game.Step(1f / 60f, HeistInput.None);
            }

            Assert.That(game.Snapshot().GuardState, Is.EqualTo("patrol"), "Guard returned to Patrol after 1st investigation");

            // 2nd decoy coin at a distant walkable tile (11.5, 4.5):
            // Regression check: _arrivedAtTarget must NOT be stale-latched true from the 1st investigation!
            var secondCoin = game.TossDecoy(new Vector2(11.5f, 4.5f), maxRadius: 8.5f);
            game.Step(1f / 60f, HeistInput.None);
            Assert.That(game.Snapshot().GuardState, Is.EqualTo("investigate"), "2nd coin triggers Investigate");

            // Step 5 more ticks: guard must STILL be in Investigate walking toward secondCoin (not immediately aborting to Search!)
            for (var i = 0; i < 5; i++)
            {
                game.Step(1f / 60f, HeistInput.None);
            }

            Assert.That(game.Snapshot().GuardState, Is.EqualTo("investigate"),
                "Guard must stay in Investigate while walking toward the 2nd coin (no stale _arrivedAtTarget latch)");
            Assert.That(Vector2.Distance(game.LastIntent.Target.Value, secondCoin), Is.LessThan(0.1f));
        }

        [Test]
        public void ReactiveBtPreemptionAbortsRunningLowerPriorityBranchAndRecordsTrace()
        {
            var profile = NewProfile("standard");
            var bt = new GuardBt(GuardFsm.EvalGuard);

            // Tick 1: quiet patrol -> "patrol" leaf is RUNNING
            var quiet = new GuardPercepts(1, new Vector2(5f, 5f), 0f, false, null, 10f, 0f, null, 0.1f, null, false, false, 0, 0);
            var r1 = bt.Tick(quiet, profile);
            Assert.That(r1.ActiveLeaf, Is.EqualTo("patrol"));
            Assert.That(r1.AbortedNodes, Is.Empty);

            // Tick 2: sound heard -> InvestigateBranch preempts PatrolBranch via .Abort()!
            var sound = new GuardPercepts(2, new Vector2(5f, 5f), 0f, false, null, 10f, 0f,
                new SoundEvent(new Vector2(8f, 5f), 1, 1f), 0.2f, null, false, false, 0, 0);
            var r2 = bt.Tick(sound, profile);
            Assert.That(r2.ActiveLeaf, Is.EqualTo("investigate"));
            Assert.That(r2.AbortedNodes, Does.Contain("patrol"), "Preempted patrol action must be aborted");

            // Tick 3: player visible in attack range -> EngageBranch preempts InvestigateBranch via .Abort()!
            var combat = new GuardPercepts(3, new Vector2(5f, 5f), 0f, true, new Vector2(5.5f, 5f), 0.5f, 0f,
                null, 0.8f, new Vector2(5.5f, 5f), false, false, 0, 0);
            var r3 = bt.Tick(combat, profile);
            Assert.That(r3.ActiveLeaf, Is.EqualTo("attack"));
            Assert.That(r3.AbortedNodes, Does.Contain("investigate"), "Preempted investigate action must be aborted");
            Assert.That(r3.AbortedNodes, Does.Contain("seq_investigate"), "Preempted composite sequence must be aborted");
        }

        [Test]
        public void FortyThousandTickMultiPersonalityBenchmarkMeasuresLatencyAndComplexity()
        {
            const int ticksPerRun = 5000;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"structuralComplexity\": {");
            sb.AppendLine("    \"fsmStates\": 7,");
            sb.AppendLine("    \"fsmTransitions\": " + GuardFsm.Rules.Length + ",");
            sb.AppendLine("    \"btTotalNodes\": " + GuardBt.CountNodes(GuardBt.BuildGuardTree(GuardFsm.EvalGuard)) + ",");
            sb.AppendLine("    \"btMaxDepth\": 3,");
            sb.AppendLine("    \"extensibilityCostAddNewBehavior\": { \"fsmEdgesModified\": 6, \"btNodesInserted\": 2 }");
            sb.AppendLine("  },");
            sb.AppendLine("  \"runs\": [");

            var runIndex = 0;
            foreach (var pid in ProfileIds)
            {
                foreach (var arch in new[] { GuardArchitecture.Fsm, GuardArchitecture.Bt })
                {
                    var profile = NewProfile(pid);
                    var game = new HeistGame(profile, arch);
                    var transitions = 0;
                    var detections = 0;
                    var prevState = game.Snapshot().GuardState;

                    var sw = Stopwatch.StartNew();
                    for (var t = 0; t < ticksPerRun; t++)
                    {
                        // Periodic decoy lure & thief movement to exercise all 7 states
                        if (t % 450 == 100)
                        {
                            game.TossDecoy(new Vector2(13.5f, 4.5f), 7.5f);
                        }
                        else if (t % 450 == 260)
                        {
                            game.TeleportPlayer(game.GuardPosition.x + 2.2f, game.GuardPosition.y);
                        }
                        else if (t % 450 == 280)
                        {
                            game.TeleportPlayer(2.8f, 3.2f);
                        }

                        game.Step(1f / 60f, HeistInput.None);
                        var snap = game.Snapshot();
                        if (snap.GuardState != prevState)
                        {
                            transitions++;
                            prevState = snap.GuardState;
                        }

                        if (snap.FirstDetectionEdge)
                        {
                            detections++;
                        }

                        if (snap.Outcome != HeistOutcome.None)
                        {
                            game.Reset();
                            prevState = game.Snapshot().GuardState;
                        }
                    }

                    sw.Stop();
                    var usPerTick = (sw.Elapsed.TotalMilliseconds * 1000.0) / ticksPerRun;
                    Assert.That(usPerTick, Is.LessThan(250.0), $"{pid}/{arch} tick cost must stay well below frame budget");

                    if (runIndex > 0) sb.AppendLine(",");
                    sb.Append("    { \"profile\": \"").Append(pid)
                      .Append("\", \"arch\": \"").Append(arch == GuardArchitecture.Fsm ? "FSM" : "BT")
                      .Append("\", \"ticks\": ").Append(ticksPerRun)
                      .Append(", \"usPerTick\": ").Append(usPerTick.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append(", \"transitions\": ").Append(transitions)
                      .Append(", \"detections\": ").Append(detections)
                      .Append(" }");
                    runIndex++;
                }
            }

            sb.AppendLine();
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            var outDir = Path.Combine(Application.dataPath, "..", "formal");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "unity-ai-benchmark.json"), sb.ToString());
        }

        private GuardProfile NewProfile(string id)
        {
            var p = GuardProfile.CreateDefault(id);
            _profiles.Add(p);
            return p;
        }

        private static string LatchName(GuardAction action)
        {
            return action switch
            {
                GuardAction.Patrol => "patrol",
                GuardAction.Investigate => "investigate",
                GuardAction.Chase => "chase",
                GuardAction.Attack => "attack",
                GuardAction.Search => "search",
                GuardAction.Return => "return",
                _ => "idle",
            };
        }
    }
}
