using System;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Primitive task kind produced by HTN compound-task decomposition.</summary>
    public enum HtnPrimitiveKind
    {
        /// <summary>Navigate to a walkable tile target.</summary>
        MoveToTarget,
        /// <summary>Sweep vision left and right around a spot for a bounded duration.</summary>
        ScanSpot,
        /// <summary>Pin a strategic exit or doorway chokepoint.</summary>
        HoldChokepoint,
        /// <summary>Inspect tampered store infrastructure (power panel or vitrine) and mark it checked.</summary>
        InspectSabotage,
        /// <summary>Spend sustained time at the power panel to reboot disabled CCTV cameras.</summary>
        RestoreCameras,
    }

    /// <summary>One concrete step inside an HTN decomposed plan.</summary>
    public readonly struct HtnStep
    {
        /// <summary>Creates a primitive HTN step.</summary>
        public HtnStep(
            string name,
            HtnPrimitiveKind kind,
            GuardAction action,
            Vector2 target,
            float speedScale,
            float arrivalRadius,
            int holdTicks,
            string sabotageTag = null)
        {
            Name = name;
            Kind = kind;
            Action = action;
            Target = target;
            SpeedScale = speedScale;
            ArrivalRadius = arrivalRadius;
            HoldTicks = holdTicks;
            SabotageTag = sabotageTag;
        }

        /// <summary>Human-readable primitive task name.</summary>
        public string Name { get; }

        /// <summary>Primitive behavior kind.</summary>
        public HtnPrimitiveKind Kind { get; }

        /// <summary>Motor command associated with this step.</summary>
        public GuardAction Action { get; }

        /// <summary>Target tile coordinate in market space.</summary>
        public Vector2 Target { get; }

        /// <summary>Speed factor from the active guard profile.</summary>
        public float SpeedScale { get; }

        /// <summary>Completion radius for movement steps.</summary>
        public float ArrivalRadius { get; }

        /// <summary>Tick budget for stationary hold/scan/inspect steps.</summary>
        public int HoldTicks { get; }

        /// <summary>Optional sabotage tag ("panel" or "vitrine") cleared upon step completion.</summary>
        public string SabotageTag { get; }
    }

    /// <summary>Ordered sequence of primitive steps decomposed from a compound HTN task.</summary>
    public sealed class HtnPlan
    {
        /// <summary>Creates an HTN plan from a compound task name and ordered steps.</summary>
        public HtnPlan(string compoundTaskName, HtnStep[] steps, int createdTick)
        {
            CompoundTaskName = compoundTaskName ?? throw new ArgumentNullException(nameof(compoundTaskName));
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
            CreatedTick = createdTick;
            CurrentStepIndex = 0;
            StepElapsedTicks = 0;
        }

        /// <summary>Name of the compound tactical task being executed.</summary>
        public string CompoundTaskName { get; }

        /// <summary>Ordered primitive steps.</summary>
        public HtnStep[] Steps { get; }

        /// <summary>Zero-based index of the active step.</summary>
        public int CurrentStepIndex { get; private set; }

        /// <summary>Elapsed simulation ticks spent on the current step.</summary>
        public int StepElapsedTicks { get; set; }

        /// <summary>Simulation tick when the plan was constructed.</summary>
        public int CreatedTick { get; }

        /// <summary>True once all steps have completed.</summary>
        public bool IsComplete => CurrentStepIndex >= Steps.Length;

        /// <summary>Currently executing step.</summary>
        public HtnStep CurrentStep => Steps[Math.Min(CurrentStepIndex, Math.Max(0, Steps.Length - 1))];

        /// <summary>Advances to the next primitive step and resets step elapsed ticks.</summary>
        public void AdvanceStep()
        {
            CurrentStepIndex++;
            StepElapsedTicks = 0;
        }
    }

    /// <summary>
    /// Deterministic Hierarchical Task Network (HTN) planner that decomposes high-level
    /// security responses (EAS alarm lockdown, exit interception, sabotage checks, sound checks,
    /// and occupancy peak sweeps) into ordered primitive steps.
    /// </summary>
    public static class GuardHtnPlanner
    {
        /// <summary>Ticks a guard must spend actively rebooting the CCTV breaker after inspection (5.0s at 30 Hz).</summary>
        public const int CctvRebootTicks = 150;

        private static readonly Vector2 ExitChokepoint = new Vector2(30.5f, 21.5f);

        /// <summary>Decomposes the highest-priority valid compound task for the guard's role and world state.</summary>
        public static HtnPlan PlanForRole(
            int guardId,
            GuardTacticalRole role,
            GuardPercepts percepts,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid,
            int tick)
        {
            if (bb != null)
            {
                if (role == GuardTacticalRole.ExitInterceptor && bb.EasAlarmActive)
                {
                    var easWalkable = SnapWalkable(grid, new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J));
                    return new HtnPlan(
                        "RespondToEasAlarm",
                        new[]
                        {
                            new HtnStep("SprintToEasGate", HtnPrimitiveKind.MoveToTarget, GuardAction.Chase, easWalkable, profile.chase_speed, 1.1f, 0),
                            new HtnStep("LockdownEasGate", HtnPrimitiveKind.HoldChokepoint, GuardAction.Search, easWalkable, 0f, 1.1f, 180),
                        },
                        tick);
                }

                if (role == GuardTacticalRole.ExitInterceptor && (bb.AnyGuardSeesPlayer || bb.HeatTier >= 4 || bb.MaxProbability > 0.12f))
                {
                    var stationaryHighHeat = bb.HeatTier >= 3 && bb.LastConfirmedVelocity.sqrMagnitude < 0.05f;
                    if (stationaryHighHeat)
                    {
                        var choke = SnapWalkable(grid, ExitChokepoint);
                        return new HtnPlan(
                            "CutOffEasExit",
                            new[]
                            {
                                new HtnStep("SprintToChokepoint", HtnPrimitiveKind.MoveToTarget, GuardAction.Chase, choke, profile.chase_speed, 1.1f, 0),
                                new HtnStep("PinExitCorridor", HtnPrimitiveKind.HoldChokepoint, GuardAction.Search, choke, 0f, 1.1f, 150),
                            },
                            tick);
                    }

                    var targetCenter = bb.LastConfirmedPos ?? bb.PeakProbabilityPos ?? ExitChokepoint;
                    var flankTarget = SnapWalkable(grid, bb.ComputeFlankInterceptTarget(guardId, targetCenter, bb.LastConfirmedVelocity));
                    return new HtnPlan(
                        "FlankCutoff",
                        new[]
                        {
                            new HtnStep("SprintToFlank", HtnPrimitiveKind.MoveToTarget, GuardAction.Chase, flankTarget, profile.chase_speed, 1.0f, 0),
                            new HtnStep("SweepFlankChoke", HtnPrimitiveKind.ScanSpot, GuardAction.Search, flankTarget, 0f, 1.0f, 36),
                        },
                        tick);
                }

                if (role == GuardTacticalRole.SabotageResponder && bb.CamerasDisabled)
                {
                    var panelPos = SnapWalkable(grid, new Vector2((float)MarketData.Panel.I, (float)MarketData.Panel.J));
                    var easWalkable = SnapWalkable(grid, new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J));
                    if (!bb.PanelInspected)
                    {
                        return new HtnPlan(
                            "InvestigateCctvSabotage",
                            new[]
                            {
                                new HtnStep("MoveToPowerPanel", HtnPrimitiveKind.MoveToTarget, GuardAction.Investigate, panelPos, profile.investigate_speed, 1.4f, 0),
                                new HtnStep("InspectPowerPanel", HtnPrimitiveKind.InspectSabotage, GuardAction.Search, panelPos, 0f, 1.4f, 75, "panel"),
                                new HtnStep("RebootCctvBreaker", HtnPrimitiveKind.RestoreCameras, GuardAction.Search, panelPos, profile.investigate_speed, 1.4f, CctvRebootTicks, "panel"),
                                new HtnStep("CheckSecurityExit", HtnPrimitiveKind.MoveToTarget, GuardAction.Investigate, easWalkable, profile.investigate_speed, 1.4f, 0),
                            },
                            tick);
                    }

                    return new HtnPlan(
                        "InvestigateCctvSabotage",
                        new[]
                        {
                            new HtnStep("MoveToPowerPanel", HtnPrimitiveKind.MoveToTarget, GuardAction.Investigate, panelPos, profile.investigate_speed, 1.4f, 0),
                            new HtnStep("RebootCctvBreaker", HtnPrimitiveKind.RestoreCameras, GuardAction.Search, panelPos, profile.investigate_speed, 1.4f, CctvRebootTicks, "panel"),
                            new HtnStep("CheckSecurityExit", HtnPrimitiveKind.MoveToTarget, GuardAction.Investigate, easWalkable, profile.investigate_speed, 1.4f, 0),
                        },
                        tick);
                }

                if (role == GuardTacticalRole.SabotageResponder && bb.VitrineOpen && !bb.VitrineInspected)
                {
                    var vitrinePos = SnapWalkable(grid, MarketLayout.ItemPosition(MarketData.Fixture("m_spirits")));
                    return new HtnPlan(
                        "InvestigateForcedVitrine",
                        new[]
                        {
                            new HtnStep("MoveToVitrine", HtnPrimitiveKind.MoveToTarget, GuardAction.Investigate, vitrinePos, profile.investigate_speed, 1.5f, 0),
                            new HtnStep("InspectVitrine", HtnPrimitiveKind.InspectSabotage, GuardAction.Search, vitrinePos, 0f, 1.5f, 75, "vitrine"),
                            new HtnStep("SweepSpiritsAisle", HtnPrimitiveKind.ScanSpot, GuardAction.Search, vitrinePos, 0f, 1.5f, 60),
                        },
                        tick);
                }
            }

            if (percepts.Sound.HasValue)
            {
                var soundOrigin = SnapWalkable(grid, percepts.Sound.Value.Origin);
                return new HtnPlan(
                    "InvestigateSound",
                    new[]
                    {
                        new HtnStep("MoveToNoise", HtnPrimitiveKind.MoveToTarget, GuardAction.Investigate, soundOrigin, profile.investigate_speed, 0.9f, 0),
                        new HtnStep("ScanNoiseOrigin", HtnPrimitiveKind.ScanSpot, GuardAction.Search, soundOrigin, 0f, 0.9f, 75),
                    },
                    tick);
            }

            if (bb != null && bb.MaxProbability > 0.025f)
            {
                var peak = bb.ClaimBestSearchTarget(guardId, percepts.GuardPos);
                if (peak.HasValue)
                {
                    var sweepSpeed = MathF.Max(profile.investigate_speed * 1.15f, profile.chase_speed * 0.92f);
                    return new HtnPlan(
                        "SweepOccupancyPeak",
                        new[]
                        {
                            new HtnStep("AdvanceToProbPeak", HtnPrimitiveKind.MoveToTarget, GuardAction.Search, peak.Value, sweepSpeed, 0.9f, 0),
                            new HtnStep("SweepAisleSector", HtnPrimitiveKind.ScanSpot, GuardAction.Search, peak.Value, 0f, 0.9f, 28),
                        },
                        tick);
                }
            }

            return null;
        }

        /// <summary>
        /// Plans a brief calm patrol detour into a stale blind store corner and sweeps around it.
        /// Returns null when no separated corner spot is available.
        /// </summary>
        public static HtnPlan PlanCornerCheck(
            int guardId,
            Vector2 guardPos,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid,
            int tick)
        {
            if (bb == null || profile == null)
            {
                return null;
            }

            var cornerTarget = bb.PickStaleCornerTarget(guardId, guardPos, tick);
            if (!cornerTarget.HasValue)
            {
                return null;
            }

            var snapped = SnapWalkable(grid, cornerTarget.Value);
            return new HtnPlan(
                "InspectCorner",
                new[]
                {
                    new HtnStep("DetourToCorner", HtnPrimitiveKind.MoveToTarget, GuardAction.Patrol, snapped, profile.patrol_speed, 0.9f, 0),
                    new HtnStep("ScanCorner", HtnPrimitiveKind.ScanSpot, GuardAction.Patrol, snapped, 0f, 0.9f, bb.NextIntRange(30, 50)),
                },
                tick);
        }

        private static Vector2 SnapWalkable(NavGrid grid, Vector2 pt)
        {
            if (grid == null)
            {
                return pt;
            }

            var tx = (int)MathF.Floor(pt.x);
            var ty = (int)MathF.Floor(pt.y);
            if (grid.IsWalkable(tx, ty))
            {
                return pt;
            }

            var nearest = MarketGrid.NearestWalkable(grid, pt.x, pt.y);
            return nearest.HasValue ? new Vector2(nearest.Value.X + 0.5f, nearest.Value.Y + 0.5f) : pt;
        }
    }
}
