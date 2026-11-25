using System;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Outer HFSM macro-alertness mode inside the StateTree architecture.</summary>
    public enum GuardMacroState
    {
        /// <summary>Routine department patrol.</summary>
        Relaxed,
        /// <summary>Checking acoustic stimuli or tampered store fixtures.</summary>
        Suspicious,
        /// <summary>Direct sighting pursuit, close detention, or exit interception.</summary>
        Combat,
        /// <summary>Sweeping occupancy probability peaks after losing contact.</summary>
        Searching,
        /// <summary>Rejoining the nearest patrol waypoint after clearing an area.</summary>
        Returning,
    }

    /// <summary>Diagnostic and motor output from one StateTree controller tick.</summary>
    public readonly struct StateTreeTickResult
    {
        /// <summary>Creates a StateTree tick result.</summary>
        public StateTreeTickResult(
            GuardMacroState macroState,
            GuardTacticalRole role,
            string planName,
            string stepName,
            GuardIntent intent,
            GuardAction action)
        {
            MacroState = macroState;
            Role = role;
            PlanName = planName;
            StepName = stepName;
            Intent = intent;
            Action = action;
        }

        /// <summary>Active outer HFSM macro-state.</summary>
        public GuardMacroState MacroState { get; }

        /// <summary>Active tactical role assigned by the squad blackboard.</summary>
        public GuardTacticalRole Role { get; }

        /// <summary>Active HTN compound task or sub-tree plan name.</summary>
        public string PlanName { get; }

        /// <summary>Active primitive step or leaf name.</summary>
        public string StepName { get; }

        /// <summary>Emitted locomotion/action intent.</summary>
        public GuardIntent Intent { get; }

        /// <summary>Mapped GuardAction for engine locomotion and visuals.</summary>
        public GuardAction Action { get; }

        /// <summary>Formats a concise single-line readout for the editorial HUD telemetry strip.</summary>
        public string FormatCompact(string guardTag)
        {
            var code = MacroState switch
            {
                GuardMacroState.Combat => "CMB",
                GuardMacroState.Suspicious => "SUS",
                GuardMacroState.Searching => "SRC",
                GuardMacroState.Returning => "RET",
                _ => "PAT",
            };
            var plan = string.IsNullOrEmpty(PlanName) ? "Patrol" : PlanName;
            return guardTag + ":" + code + "/" + plan;
        }
    }

    /// <summary>
    /// Industry-grade StateTree guard brain: an outer HFSM manages alertness hysteresis without
    /// blackboard latch hacks, while each macro-state runs a targeted Behavior Sub-Tree powered
    /// by HTN tactical plans and the shared occupancy-grid blackboard.
    /// </summary>
    public sealed class GuardStateTree
    {
        private readonly Vector2[] _route;
        private int _lastSoundTick = -9999;
        private int _waypointsSinceCornerCheck;
        private int _nextCornerCheckThreshold;

        /// <summary>Creates a StateTree guard controller bound to a guard index and patrol route.</summary>
        public GuardStateTree(int guardId, Vector2[] route, GuardAction initial = GuardAction.Patrol)
        {
            GuardId = Math.Clamp(guardId, 0, 1);
            _route = route is { Length: > 0 } ? (Vector2[])route.Clone() : (Vector2[])GuardProfile.DefaultPatrolRoute.Clone();
            MacroState = MacroFromAction(initial);
            Action = initial;
            _nextCornerCheckThreshold = 1 + GuardId;
        }

        /// <summary>Zero-based guard index (0 or 1).</summary>
        public int GuardId { get; }

        /// <summary>Current outer HFSM macro-state.</summary>
        public GuardMacroState MacroState { get; private set; }

        /// <summary>Latest emitted GuardAction.</summary>
        public GuardAction Action { get; private set; }

        /// <summary>Current patrol route waypoint index.</summary>
        public int WpIndex { get; private set; }

        /// <summary>Active HTN plan, or null when executing direct sub-tree behavior.</summary>
        public HtnPlan ActivePlan { get; private set; }

        /// <summary>Most recent tick output.</summary>
        public StateTreeTickResult LastResult { get; private set; }

        /// <summary>Evaluates outer HFSM transitions and executes the active state's Behavior Sub-Tree.</summary>
        public StateTreeTickResult Tick(
            GuardPercepts percepts,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid)
        {
            var role = bb != null ? bb.AssignedRoles[GuardId] : GuardTacticalRole.Patrol;
            var prevMacro = MacroState;

            var playerVisible = GuardFsm.EvalGuard("player_visible", percepts, profile);
            var heardSound = GuardFsm.EvalGuard("heard_sound", percepts, profile);
            var recentRadioIntercept = role == GuardTacticalRole.ExitInterceptor
                && bb != null
                && bb.HasRecentSighting(percepts.Tick, GuardSquadBlackboard.RecentContactWindowTicks)
                && bb.LastConfirmedVelocity.sqrMagnitude >= 0.05f
                && bb.MaxProbability > 0.08f;
            var interceptorAlert = ((role == GuardTacticalRole.ExitInterceptor || role == GuardTacticalRole.PrimaryPursuer)
                && bb != null
                && (bb.AnyGuardSeesPlayer || bb.EasAlarmActive))
                || recentRadioIntercept;

            // Outer HFSM macro transitions (hysteresis lives natively in MacroState)
            if (playerVisible || interceptorAlert)
            {
                MacroState = GuardMacroState.Combat;
            }
            else if (MacroState == GuardMacroState.Combat)
            {
                var lostLong = GuardFsm.EvalGuard("lost_sight_long", percepts, profile);
                if (!percepts.PlayerVisible && lostLong)
                {
                    MacroState = GuardMacroState.Searching;
                }
            }
            else if (MacroState == GuardMacroState.Relaxed || MacroState == GuardMacroState.Returning)
            {
                if (heardSound || role == GuardTacticalRole.SabotageResponder)
                {
                    MacroState = GuardMacroState.Suspicious;
                }
                else if (MacroState == GuardMacroState.Returning
                    && (percepts.AtRouteStart || GuardPerception.Distance(percepts.GuardPos, _route[WpIndex]) < 0.85f))
                {
                    MacroState = GuardMacroState.Relaxed;
                }
                else if (role == GuardTacticalRole.SectorSweeper && bb != null && bb.MaxProbability > 0.03f)
                {
                    MacroState = GuardMacroState.Searching;
                }
            }
            else if (MacroState == GuardMacroState.Suspicious)
            {
                var runningSabotagePlan = ActivePlan != null
                    && !ActivePlan.IsComplete
                    && (ActivePlan.CompoundTaskName == "InvestigateCctvSabotage"
                        || ActivePlan.CompoundTaskName == "InvestigateForcedVitrine");
                if ((ActivePlan != null && ActivePlan.IsComplete)
                    || (!runningSabotagePlan && GuardFsm.EvalGuard("arrived_and_quiet", percepts, profile)))
                {
                    MacroState = GuardMacroState.Searching;
                }
            }
            else if (MacroState == GuardMacroState.Searching)
            {
                if (heardSound || role == GuardTacticalRole.SabotageResponder)
                {
                    MacroState = GuardMacroState.Suspicious;
                }
                else if (!percepts.PlayerVisible
                    && percepts.SearchTicks >= profile.search_duration_limit
                    && percepts.Alert <= profile.alert_calm_threshold
                    && (bb == null || bb.MaxProbability <= 0.03f || percepts.SearchTicks >= profile.search_duration_limit * 2))
                {
                    MacroState = GuardMacroState.Returning;
                }
            }

            if (MacroState != prevMacro)
            {
                ActivePlan = null;
                bb?.ReleaseClaim(GuardId);
                if (MacroState == GuardMacroState.Returning)
                {
                    WpIndex = NearestWaypointIndex(percepts.GuardPos);
                }
            }

            var result = MacroState switch
            {
                GuardMacroState.Combat => TickCombatSubTree(percepts, profile, bb, grid, role),
                GuardMacroState.Suspicious => TickSuspiciousSubTree(percepts, profile, bb, grid, role),
                GuardMacroState.Searching => TickSearchingSubTree(percepts, profile, bb, grid, role),
                GuardMacroState.Returning => TickReturningSubTree(profile, role),
                _ => TickRelaxedSubTree(percepts, profile, bb, grid, role),
            };

            Action = result.Action;
            LastResult = result;
            return result;
        }

        private StateTreeTickResult TickCombatSubTree(
            GuardPercepts percepts,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid,
            GuardTacticalRole role)
        {
            // Priority 1: Immediate detention inside attack range
            if (GuardFsm.EvalGuard("in_attack_range", percepts, profile))
            {
                ActivePlan = null;
                var target = percepts.PlayerPos ?? percepts.LastSeenPos;
                var intent = new GuardIntent(GuardAction.Attack, target, 0f);
                return new StateTreeTickResult(MacroState, role, "DirectEngage", "DetainThief", intent, GuardAction.Attack);
            }

            // Priority 2: Exit interceptor cuts off escape or flanks moving thief ahead of their heading
            if (role == GuardTacticalRole.ExitInterceptor && !percepts.PlayerVisible && percepts.LostSightTicks >= 6)
            {
                var wantAlarmPlan = bb != null && bb.EasAlarmActive;
                var wantFlankPlan = !wantAlarmPlan && bb != null && (bb.LastConfirmedVelocity.sqrMagnitude >= 0.05f || bb.HeatTier < 3);
                var expectedPlanName = wantAlarmPlan
                    ? "RespondToEasAlarm"
                    : (wantFlankPlan ? "FlankCutoff" : "CutOffEasExit");
                var staleFlank = wantFlankPlan
                    && ActivePlan != null
                    && ActivePlan.CompoundTaskName == "FlankCutoff"
                    && ActivePlan.StepElapsedTicks >= 28;
                if (ActivePlan == null || ActivePlan.IsComplete || ActivePlan.CompoundTaskName != expectedPlanName || staleFlank)
                {
                    ActivePlan = GuardHtnPlanner.PlanForRole(GuardId, role, percepts, profile, bb, grid, percepts.Tick);
                }

                if (ActivePlan != null && !ActivePlan.IsComplete)
                {
                    return ExecuteActivePlan(percepts, bb, role, GuardAction.Chase);
                }
            }

            // Priority 3: Direct pursuit when seen, or velocity-predicted escape interception after losing sight
            ActivePlan = null;
            var predictedPos = (!percepts.PlayerVisible && bb != null && bb.LastConfirmedPos.HasValue)
                ? (Vector2?)bb.PredictEscapePoint(4)
                : null;
            var chaseTarget = percepts.PlayerPos ?? predictedPos ?? percepts.LastSeenPos ?? bb?.LastConfirmedPos ?? _route[WpIndex];
            var chaseIntent = new GuardIntent(GuardAction.Chase, chaseTarget, profile.chase_speed);
            return new StateTreeTickResult(MacroState, role, "DirectPursuit", "PursueTarget", chaseIntent, GuardAction.Chase);
        }

        private StateTreeTickResult TickSuspiciousSubTree(
            GuardPercepts percepts,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid,
            GuardTacticalRole role)
        {
            var stalePostRestoreWalk = ActivePlan != null
                && !ActivePlan.IsComplete
                && ActivePlan.CompoundTaskName == "InvestigateCctvSabotage"
                && ActivePlan.CurrentStep.Name == "CheckSecurityExit"
                && bb != null
                && bb.CamerasDisabled;
            var needSabotageReplan = role == GuardTacticalRole.SabotageResponder
                && (ActivePlan == null
                    || stalePostRestoreWalk
                    || (ActivePlan.CompoundTaskName != "InvestigateCctvSabotage" && ActivePlan.CompoundTaskName != "InvestigateForcedVitrine"));

            var soundStamp = percepts.Sound.HasValue ? percepts.Tick - percepts.Sound.Value.AgeTicks : -9999;
            var needSoundReplan = percepts.Sound.HasValue
                && soundStamp != _lastSoundTick
                && (ActivePlan == null || ActivePlan.CompoundTaskName == "InvestigateSound");

            if (needSabotageReplan || needSoundReplan || ActivePlan == null || ActivePlan.IsComplete)
            {
                if (percepts.Sound.HasValue)
                {
                    _lastSoundTick = soundStamp;
                }

                ActivePlan = GuardHtnPlanner.PlanForRole(GuardId, role, percepts, profile, bb, grid, percepts.Tick);
            }

            if (ActivePlan != null && !ActivePlan.IsComplete)
            {
                return ExecuteActivePlan(percepts, bb, role, GuardAction.Investigate);
            }

            var target = percepts.Sound.HasValue
                ? percepts.Sound.Value.Origin
                : percepts.LastSeenPos ?? bb?.LastConfirmedPos ?? _route[WpIndex];
            var intent = new GuardIntent(GuardAction.Investigate, target, profile.investigate_speed);
            return new StateTreeTickResult(MacroState, role, "InvestigateFallback", "MoveToStimulus", intent, GuardAction.Investigate);
        }

        private StateTreeTickResult TickSearchingSubTree(
            GuardPercepts percepts,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid,
            GuardTacticalRole role)
        {
            if (ActivePlan != null
                && !ActivePlan.IsComplete
                && ActivePlan.CompoundTaskName == "SweepOccupancyPeak"
                && ActivePlan.CurrentStep.Kind == HtnPrimitiveKind.MoveToTarget
                && bb != null
                && bb.MaxProbability > 0.05f)
            {
                var tx = (int)MathF.Floor(ActivePlan.CurrentStep.Target.x);
                var ty = (int)MathF.Floor(ActivePlan.CurrentStep.Target.y);
                if (bb.ProbabilityAt(tx, ty) <= 0.01f)
                {
                    ActivePlan = null;
                    bb.ReleaseClaim(GuardId);
                }
            }

            if (ActivePlan == null || ActivePlan.IsComplete)
            {
                ActivePlan = GuardHtnPlanner.PlanForRole(GuardId, GuardTacticalRole.SectorSweeper, percepts, profile, bb, grid, percepts.Tick);
            }

            if (ActivePlan != null && !ActivePlan.IsComplete)
            {
                return ExecuteActivePlan(percepts, bb, role, GuardAction.Search);
            }

            var fallbackTarget = percepts.LastSeenPos ?? bb?.PeakProbabilityPos ?? percepts.GuardPos;
            var intent = new GuardIntent(GuardAction.Search, fallbackTarget, 0f);
            return new StateTreeTickResult(MacroState, role, "PerimeterScan", "LookAround", intent, GuardAction.Search);
        }

        private StateTreeTickResult TickReturningSubTree(GuardProfile profile, GuardTacticalRole role)
        {
            var intent = new GuardIntent(GuardAction.Return, _route[WpIndex], profile.patrol_speed);
            return new StateTreeTickResult(MacroState, role, "ReturnToBeat", "RejoinRoute", intent, GuardAction.Return);
        }

        private StateTreeTickResult TickRelaxedSubTree(
            GuardPercepts percepts,
            GuardProfile profile,
            GuardSquadBlackboard bb,
            NavGrid grid,
            GuardTacticalRole role)
        {
            if (ActivePlan != null)
            {
                if (ActivePlan.IsComplete || ActivePlan.StepElapsedTicks > 260)
                {
                    ActivePlan = null;
                    bb?.ReleaseClaim(GuardId);
                }
                else
                {
                    var planResult = ExecuteActivePlan(percepts, bb, role, GuardAction.Patrol);
                    if (ActivePlan.IsComplete)
                    {
                        ActivePlan = null;
                        bb?.ReleaseClaim(GuardId);
                    }

                    return planResult;
                }
            }

            var wp = _route[WpIndex];
            if (GuardPerception.Distance(percepts.GuardPos, wp) < 0.65f)
            {
                WpIndex = (WpIndex + 1) % _route.Length;
                wp = _route[WpIndex];
                _waypointsSinceCornerCheck++;
                if (bb != null && _waypointsSinceCornerCheck >= _nextCornerCheckThreshold)
                {
                    _waypointsSinceCornerCheck = 0;
                    _nextCornerCheckThreshold = bb.NextIntRange(1, 3);
                    var cornerPlan = GuardHtnPlanner.PlanCornerCheck(GuardId, percepts.GuardPos, profile, bb, grid, percepts.Tick);
                    if (cornerPlan != null && !cornerPlan.IsComplete)
                    {
                        ActivePlan = cornerPlan;
                        return ExecuteActivePlan(percepts, bb, role, GuardAction.Patrol);
                    }
                }
            }

            var intent = new GuardIntent(GuardAction.Patrol, wp, profile.patrol_speed);
            return new StateTreeTickResult(MacroState, role, "SectorPatrol", "PatrolWp[" + WpIndex + "]", intent, GuardAction.Patrol);
        }

        private StateTreeTickResult ExecuteActivePlan(
            GuardPercepts percepts,
            GuardSquadBlackboard bb,
            GuardTacticalRole role,
            GuardAction defaultAction)
        {
            var step = ActivePlan.CurrentStep;
            ActivePlan.StepElapsedTicks++;

            switch (step.Kind)
            {
                case HtnPrimitiveKind.MoveToTarget:
                    if (GuardPerception.Distance(percepts.GuardPos, step.Target) <= step.ArrivalRadius)
                    {
                        ActivePlan.AdvanceStep();
                        if (!ActivePlan.IsComplete)
                        {
                            step = ActivePlan.CurrentStep;
                        }
                    }

                    break;

                case HtnPrimitiveKind.ScanSpot:
                case HtnPrimitiveKind.HoldChokepoint:
                    if (ActivePlan.StepElapsedTicks >= step.HoldTicks)
                    {
                        ActivePlan.AdvanceStep();
                        if (!ActivePlan.IsComplete)
                        {
                            step = ActivePlan.CurrentStep;
                        }
                    }

                    break;

                case HtnPrimitiveKind.InspectSabotage:
                    if (ActivePlan.StepElapsedTicks >= step.HoldTicks)
                    {
                        if (step.SabotageTag == "panel")
                        {
                            bb?.MarkPanelInspected();
                        }
                        else if (step.SabotageTag == "vitrine")
                        {
                            bb?.MarkVitrineInspected();
                        }

                        ActivePlan.AdvanceStep();
                        if (!ActivePlan.IsComplete)
                        {
                            step = ActivePlan.CurrentStep;
                        }
                    }

                    break;

                case HtnPrimitiveKind.RestoreCameras:
                    var restoreRange = step.ArrivalRadius + 0.35f;
                    if (GuardPerception.Distance(percepts.GuardPos, step.Target) > restoreRange)
                    {
                        ActivePlan.StepElapsedTicks = Math.Max(0, ActivePlan.StepElapsedTicks - 1);
                        var approachSpeed = step.SpeedScale > 0.01f ? step.SpeedScale : 0.85f;
                        var approachIntent = new GuardIntent(GuardAction.Investigate, step.Target, approachSpeed);
                        return new StateTreeTickResult(MacroState, role, ActivePlan.CompoundTaskName, step.Name, approachIntent, GuardAction.Investigate);
                    }

                    if (bb != null)
                    {
                        bb.CctvRestoreInProgress = true;
                        bb.CctvRestoreProgress = Mathf.Clamp01((float)ActivePlan.StepElapsedTicks / Math.Max(1, step.HoldTicks));
                    }

                    if (ActivePlan.StepElapsedTicks >= step.HoldTicks)
                    {
                        bb?.RequestCamerasRestore();
                        ActivePlan.AdvanceStep();
                        if (!ActivePlan.IsComplete)
                        {
                            step = ActivePlan.CurrentStep;
                        }
                    }

                    break;
            }

            var holdAction = defaultAction == GuardAction.Patrol ? GuardAction.Patrol : GuardAction.Search;
            if (ActivePlan.IsComplete)
            {
                var doneTarget = step.Target;
                var doneIntent = new GuardIntent(holdAction, doneTarget, 0f);
                return new StateTreeTickResult(MacroState, role, ActivePlan.CompoundTaskName, step.Name, doneIntent, holdAction);
            }

            var action = step.Kind == HtnPrimitiveKind.MoveToTarget ? step.Action : holdAction;
            var speed = step.Kind == HtnPrimitiveKind.MoveToTarget ? step.SpeedScale : 0f;
            var intent = new GuardIntent(action, step.Target, speed);
            return new StateTreeTickResult(MacroState, role, ActivePlan.CompoundTaskName, step.Name, intent, action);
        }

        private int NearestWaypointIndex(Vector2 pos)
        {
            var bestIdx = 0;
            var bestDist = float.MaxValue;
            for (var i = 0; i < _route.Length; i++)
            {
                var d = GuardPerception.Distance(pos, _route[i]);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        private static GuardMacroState MacroFromAction(GuardAction action)
        {
            return action switch
            {
                GuardAction.Chase or GuardAction.Attack => GuardMacroState.Combat,
                GuardAction.Investigate => GuardMacroState.Suspicious,
                GuardAction.Search => GuardMacroState.Searching,
                GuardAction.Return => GuardMacroState.Returning,
                _ => GuardMacroState.Relaxed,
            };
        }
    }
}
