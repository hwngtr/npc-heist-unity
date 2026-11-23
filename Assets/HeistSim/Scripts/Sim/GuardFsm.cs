using System;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Transition recorded by the FSM when a rule fires.</summary>
    public readonly struct FsmTransition
    {
        /// <summary>Creates a transition record.</summary>
        public FsmTransition(GuardAction from, GuardAction to, string guard)
        {
            From = from;
            To = to;
            Guard = guard;
        }

        /// <summary>State before the transition.</summary>
        public GuardAction From { get; }

        /// <summary>State after the transition.</summary>
        public GuardAction To { get; }

        /// <summary>Name of the guard predicate that fired.</summary>
        public string Guard { get; }

        /// <summary>Whether this value records an actual transition.</summary>
        public bool HasValue => Guard != null;
    }

    /// <summary>Deterministic table-driven seven-state guard FSM with waypoint patrol.</summary>
    public sealed class GuardFsm
    {
        /// <summary>Priority-ordered transition table mirroring FSM_TABLE.</summary>
        public static readonly (GuardAction Src, GuardAction Dst, string Guard)[] Rules =
        {
            (GuardAction.Idle, GuardAction.Chase, "player_visible"),
            (GuardAction.Idle, GuardAction.Investigate, "heard_sound"),
            (GuardAction.Idle, GuardAction.Patrol, "route_assigned"),
            (GuardAction.Patrol, GuardAction.Chase, "player_visible"),
            (GuardAction.Patrol, GuardAction.Investigate, "heard_sound"),
            (GuardAction.Investigate, GuardAction.Chase, "player_visible"),
            (GuardAction.Investigate, GuardAction.Search, "arrived_and_quiet"),
            (GuardAction.Chase, GuardAction.Attack, "in_attack_range"),
            (GuardAction.Chase, GuardAction.Search, "lost_sight_long"),
            (GuardAction.Attack, GuardAction.Chase, "out_of_range"),
            (GuardAction.Attack, GuardAction.Search, "lost_sight_long"),
            (GuardAction.Search, GuardAction.Chase, "player_visible"),
            (GuardAction.Search, GuardAction.Investigate, "heard_sound"),
            (GuardAction.Search, GuardAction.Return, "search_expired"),
            (GuardAction.Return, GuardAction.Chase, "player_visible"),
            (GuardAction.Return, GuardAction.Investigate, "heard_sound"),
            (GuardAction.Return, GuardAction.Patrol, "at_route_start"),
        };

        private readonly Vector2[] _route;
        private int _wpIndex;

        /// <summary>Creates an FSM at the given state with an optional patrol route.</summary>
        public GuardFsm(GuardAction initial = GuardAction.Idle, Vector2[] route = null)
        {
            State = initial;
            _route = route is { Length: > 0 } ? (Vector2[])route.Clone() : (Vector2[])GuardProfile.DefaultPatrolRoute.Clone();
        }

        /// <summary>Current FSM state.</summary>
        public GuardAction State { get; private set; }

        /// <summary>Most recent transition, or a default value if none has fired.</summary>
        public FsmTransition LastTransition { get; private set; }

        /// <summary>Patrol route in tiles.</summary>
        public Vector2[] Route => _route;

        /// <summary>Index of the current patrol waypoint.</summary>
        public int WpIndex => _wpIndex;

        /// <summary>Evaluates a named symbolic guard predicate against percepts and profile.</summary>
        public static bool EvalGuard(string guardName, GuardPercepts p, GuardProfile profile)
        {
            switch (guardName)
            {
                case "route_assigned":
                    return true;
                case "player_visible":
                    return p.PlayerVisible && p.Alert >= profile.alert_detect_threshold;
                case "heard_sound":
                    var hasRecentSound = p.Sound.HasValue && p.Sound.Value.AgeTicks <= profile.sound_memory_ticks;
                    var fleetingGlimpse = p.PlayerVisible && p.Alert < profile.alert_detect_threshold;
                    return hasRecentSound || fleetingGlimpse;
                case "arrived_and_quiet":
                    return p.ArrivedAtTarget && !p.PlayerVisible;
                case "in_attack_range":
                    return p.PlayerVisible && p.PlayerDist <= profile.attack_range;
                case "out_of_range":
                    return (p.PlayerVisible && p.PlayerDist > profile.attack_range) ||
                           (!p.PlayerVisible && p.LostSightTicks < profile.lost_sight_limit);
                case "lost_sight_long":
                    return !p.PlayerVisible && p.LostSightTicks >= profile.lost_sight_limit;
                case "search_expired":
                    return !p.PlayerVisible &&
                           p.SearchTicks >= profile.search_duration_limit &&
                           p.Alert <= profile.alert_calm_threshold;
                case "at_route_start":
                    return p.AtRouteStart;
                default:
                    throw new ArgumentException($"Unknown FSM guard predicate: {guardName}", nameof(guardName));
            }
        }

        /// <summary>Evaluates guards in priority order, transitions at most once, and emits an intent.</summary>
        public (GuardIntent Intent, FsmTransition Transition) Tick(GuardPercepts percepts, GuardProfile profile)
        {
            foreach (var rule in Rules)
            {
                if (rule.Src != State || !EvalGuard(rule.Guard, percepts, profile))
                {
                    continue;
                }

                if (rule.Dst != State)
                {
                    LastTransition = new FsmTransition(State, rule.Dst, rule.Guard);
                    State = rule.Dst;
                }

                break;
            }

            return (IntentForState(percepts, profile), LastTransition);
        }

        private GuardIntent IntentForState(GuardPercepts p, GuardProfile profile)
        {
            switch (State)
            {
                case GuardAction.Idle:
                    return new GuardIntent(GuardAction.Idle, null, 0f);
                case GuardAction.Patrol:
                    var wp = _route[_wpIndex];
                    if (GuardPerception.Distance(p.GuardPos, wp) < 0.65f)
                    {
                        _wpIndex = (_wpIndex + 1) % _route.Length;
                        wp = _route[_wpIndex];
                    }

                    return new GuardIntent(GuardAction.Patrol, wp, profile.patrol_speed);
                case GuardAction.Investigate:
                    var investigateTarget = p.Sound.HasValue
                        ? p.Sound.Value.Origin
                        : p.LastSeenPos ?? _route[0];
                    return new GuardIntent(GuardAction.Investigate, investigateTarget, profile.investigate_speed);
                case GuardAction.Chase:
                    var chaseTarget = p.PlayerPos ?? p.LastSeenPos ?? _route[0];
                    return new GuardIntent(GuardAction.Chase, chaseTarget, profile.chase_speed);
                case GuardAction.Attack:
                    return new GuardIntent(GuardAction.Attack, p.PlayerPos ?? p.LastSeenPos, 0f);
                case GuardAction.Search:
                    return new GuardIntent(GuardAction.Search, p.LastSeenPos ?? p.GuardPos, 0f);
                case GuardAction.Return:
                    return new GuardIntent(GuardAction.Return, _route[0], profile.patrol_speed);
                default:
                    throw new ArgumentOutOfRangeException(nameof(State), State, "Unhandled guard state");
            }
        }
    }
}
