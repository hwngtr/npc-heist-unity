using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Return status of a behavior tree node tick.</summary>
    public enum BtStatus
    {
        /// <summary>Node completed successfully.</summary>
        Success,
        /// <summary>Node precondition failed.</summary>
        Failure,
        /// <summary>Node is still executing.</summary>
        Running,
    }

    /// <summary>Trace kind recorded for a behavior tree node.</summary>
    public static class BtNodeKind
    {
        /// <summary>Condition leaf.</summary>
        public const string Condition = "Cond";
        /// <summary>Action leaf.</summary>
        public const string Action = "Act";
        /// <summary>Sequence composite.</summary>
        public const string Sequence = "Seq";
        /// <summary>Fallback selector composite.</summary>
        public const string Selector = "Sel";
        /// <summary>Reactive priority selector composite.</summary>
        public const string ReactiveSelector = "ReactiveSelector";
    }

    /// <summary>One behavior tree trace record captured during a tick.</summary>
    public readonly struct BtTraceEntry
    {
        /// <summary>Creates a trace record.</summary>
        public BtTraceEntry(string node, string kind, BtStatus status)
        {
            Node = node;
            Kind = kind;
            Status = status;
        }

        /// <summary>Node name.</summary>
        public string Node { get; }

        /// <summary>Node kind, one of the BtNodeKind constants.</summary>
        public string Kind { get; }

        /// <summary>Node status after the tick.</summary>
        public BtStatus Status { get; }

        /// <summary>Status rendered with the shipped JavaScript spelling.</summary>
        public string StatusName => BtStatusNames.Of(Status);
    }

    /// <summary>Trace spelling for behavior tree statuses.</summary>
    public static class BtStatusNames
    {
        /// <summary>Returns SUCCESS, FAILURE, or RUNNING.</summary>
        public static string Of(BtStatus status)
        {
            return status switch
            {
                BtStatus.Success => "SUCCESS",
                BtStatus.Failure => "FAILURE",
                _ => "RUNNING",
            };
        }
    }

    /// <summary>Per-agent memory shared across behavior tree nodes.</summary>
    public sealed class BtBlackboard
    {
        /// <summary>Current percepts.</summary>
        public GuardPercepts Percepts;

        /// <summary>Active guard profile.</summary>
        public GuardProfile Profile;

        /// <summary>Patrol route in tiles.</summary>
        public IReadOnlyList<Vector2> Route;

        /// <summary>Last leaf state reached by an action node.</summary>
        public string LatchState = "patrol";

        /// <summary>Current patrol waypoint index.</summary>
        public int WpIndex;

        /// <summary>Intent written by the active action node.</summary>
        public GuardIntent ActiveIntent;

        /// <summary>Active action leaf name.</summary>
        public string ActiveLeaf = "idle";

        /// <summary>Trace records for this tick.</summary>
        public List<BtTraceEntry> Trace = new();

        /// <summary>Nodes aborted during this tick.</summary>
        public List<string> AbortedNodes = new();
    }

    /// <summary>Behavior tree node contract.</summary>
    public abstract class BtNode
    {
        /// <summary>Creates a node with a name.</summary>
        protected BtNode(string name)
        {
            Name = name;
        }

        /// <summary>Node name.</summary>
        public string Name { get; }

        /// <summary>Evaluates this node against the blackboard and records its status.</summary>
        public abstract BtStatus Tick(BtBlackboard bb);

        /// <summary>Cleans up running state when preempted by a higher priority branch.</summary>
        public virtual void Abort(BtBlackboard bb)
        {
        }
    }

    /// <summary>Leaf node that tests a boolean predicate on the blackboard.</summary>
    public sealed class Condition : BtNode
    {
        private readonly Func<BtBlackboard, bool> _predicate;

        /// <summary>Creates a condition leaf.</summary>
        public Condition(string name, Func<BtBlackboard, bool> predicate) : base(name)
        {
            _predicate = predicate;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtBlackboard bb)
        {
            var status = _predicate(bb) ? BtStatus.Success : BtStatus.Failure;
            bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.Condition, status));
            return status;
        }
    }

    /// <summary>Leaf node that writes an intent to the blackboard.</summary>
    public sealed class ActionNode : BtNode
    {
        private readonly Func<BtBlackboard, (BtStatus Status, GuardIntent Intent)> _fn;

        /// <summary>Creates an action leaf.</summary>
        public ActionNode(string name, Func<BtBlackboard, (BtStatus Status, GuardIntent Intent)> fn) : base(name)
        {
            _fn = fn;
        }

        /// <summary>Whether this leaf is currently running.</summary>
        public bool IsRunning { get; private set; }

        /// <inheritdoc />
        public override BtStatus Tick(BtBlackboard bb)
        {
            var (status, intent) = _fn(bb);
            IsRunning = status == BtStatus.Running;
            bb.ActiveIntent = intent;
            bb.ActiveLeaf = Name;
            bb.LatchState = Name;
            bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.Action, status));
            return status;
        }

        /// <inheritdoc />
        public override void Abort(BtBlackboard bb)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            bb.AbortedNodes.Add(Name);
        }
    }

    /// <summary>Composite node that ticks children in order until one fails or runs.</summary>
    public sealed class Sequence : BtNode
    {
        private BtNode _runningChild;

        /// <summary>Creates a sequence node.</summary>
        public Sequence(string name, params BtNode[] children) : base(name)
        {
            Children = children;
        }

        /// <summary>Child nodes in tick order.</summary>
        public IReadOnlyList<BtNode> Children { get; }

        /// <inheritdoc />
        public override BtStatus Tick(BtBlackboard bb)
        {
            foreach (var child in Children)
            {
                var status = child.Tick(bb);
                if (status != BtStatus.Success)
                {
                    if (_runningChild != null && !ReferenceEquals(_runningChild, child))
                    {
                        _runningChild.Abort(bb);
                    }

                    _runningChild = status == BtStatus.Running ? child : null;
                    bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.Sequence, status));
                    return status;
                }
            }

            if (_runningChild != null)
            {
                _runningChild.Abort(bb);
                _runningChild = null;
            }

            bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.Sequence, BtStatus.Success));
            return BtStatus.Success;
        }

        /// <inheritdoc />
        public override void Abort(BtBlackboard bb)
        {
            if (_runningChild == null)
            {
                return;
            }

            _runningChild.Abort(bb);
            _runningChild = null;
            bb.AbortedNodes.Add(Name);
        }
    }

    /// <summary>Fallback selector that ticks children until one succeeds or runs.</summary>
    public sealed class Selector : BtNode
    {
        /// <summary>Creates a selector node.</summary>
        public Selector(string name, params BtNode[] children) : base(name)
        {
            Children = children;
        }

        /// <summary>Child nodes in priority order.</summary>
        public IReadOnlyList<BtNode> Children { get; }

        /// <inheritdoc />
        public override BtStatus Tick(BtBlackboard bb)
        {
            foreach (var child in Children)
            {
                var status = child.Tick(bb);
                if (status != BtStatus.Failure)
                {
                    bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.Selector, status));
                    return status;
                }
            }

            bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.Selector, BtStatus.Failure));
            return BtStatus.Failure;
        }

        /// <inheritdoc />
        public override void Abort(BtBlackboard bb)
        {
            foreach (var child in Children)
            {
                child.Abort(bb);
            }
        }
    }

    /// <summary>Priority selector that re-evaluates from child 0 every tick and aborts preempted branches.</summary>
    public sealed class ReactiveSelector : BtNode
    {
        /// <summary>Creates a reactive selector node.</summary>
        public ReactiveSelector(string name, params BtNode[] children) : base(name)
        {
            Children = children;
        }

        /// <summary>Child nodes in priority order.</summary>
        public IReadOnlyList<BtNode> Children { get; }

        /// <summary>Index of the running child, or null when idle.</summary>
        public int? ActiveIndex { get; private set; }

        /// <inheritdoc />
        public override BtStatus Tick(BtBlackboard bb)
        {
            for (var idx = 0; idx < Children.Count; idx++)
            {
                var child = Children[idx];
                var status = child.Tick(bb);
                if (status != BtStatus.Failure)
                {
                    if (ActiveIndex.HasValue && ActiveIndex.Value != idx)
                    {
                        Children[ActiveIndex.Value].Abort(bb);
                    }

                    ActiveIndex = status == BtStatus.Running ? idx : null;
                    bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.ReactiveSelector, status));
                    return status;
                }
            }

            if (ActiveIndex.HasValue)
            {
                Children[ActiveIndex.Value].Abort(bb);
                ActiveIndex = null;
            }

            bb.Trace.Add(new BtTraceEntry(Name, BtNodeKind.ReactiveSelector, BtStatus.Failure));
            return BtStatus.Failure;
        }

        /// <inheritdoc />
        public override void Abort(BtBlackboard bb)
        {
            if (!ActiveIndex.HasValue)
            {
                return;
            }

            Children[ActiveIndex.Value].Abort(bb);
            ActiveIndex = null;
            bb.AbortedNodes.Add(Name);
        }
    }

    /// <summary>Result of one guard behavior tree tick.</summary>
    public sealed class BtTickResult
    {
        /// <summary>Creates a tick result.</summary>
        public BtTickResult(string activeLeaf, GuardIntent intent, IReadOnlyList<BtTraceEntry> trace, IReadOnlyList<string> abortedNodes)
        {
            ActiveLeaf = activeLeaf;
            Intent = intent;
            Trace = trace;
            AbortedNodes = abortedNodes;
        }

        /// <summary>Active action leaf name.</summary>
        public string ActiveLeaf { get; }

        /// <summary>Intent written by the active action node.</summary>
        public GuardIntent Intent { get; }

        /// <summary>Trace records for this tick.</summary>
        public IReadOnlyList<BtTraceEntry> Trace { get; }

        /// <summary>Nodes aborted during this tick.</summary>
        public IReadOnlyList<string> AbortedNodes { get; }
    }

    /// <summary>Reactive behavior tree guard controller with persistent blackboard state.</summary>
    public sealed class GuardBt
    {
        private readonly BtNode _root;
        private readonly Vector2[] _route;

        /// <summary>Creates a guard tree with the canonical branch order and an injected guard evaluator.</summary>
        public GuardBt(Func<string, GuardPercepts, GuardProfile, bool> evalGuard, Vector2[] route = null, string initialLatch = "patrol")
        {
            if (evalGuard == null)
            {
                throw new ArgumentNullException(nameof(evalGuard));
            }

            _route = route is { Length: > 0 } ? (Vector2[])route.Clone() : (Vector2[])GuardProfile.DefaultPatrolRoute.Clone();
            LatchState = initialLatch;
            _root = BuildGuardTree(evalGuard);
        }

        /// <summary>Root node of the guard tree.</summary>
        public BtNode Root => _root;

        /// <summary>Last leaf state reached by an action node.</summary>
        public string LatchState { get; private set; }

        /// <summary>Current patrol waypoint index.</summary>
        public int WpIndex { get; private set; }

        /// <summary>Ticks the tree once and returns the active leaf, intent, trace, and aborted nodes.</summary>
        public BtTickResult Tick(GuardPercepts percepts, GuardProfile profile)
        {
            var bb = new BtBlackboard
            {
                Percepts = percepts,
                Profile = profile,
                Route = _route,
                LatchState = LatchState,
                WpIndex = WpIndex,
            };
            _root.Tick(bb);
            LatchState = bb.LatchState;
            WpIndex = bb.WpIndex;
            return new BtTickResult(bb.ActiveLeaf, bb.ActiveIntent, bb.Trace, bb.AbortedNodes);
        }

        /// <summary>Builds the canonical 21-node, seven-branch guard tree.</summary>
        public static BtNode BuildGuardTree(Func<string, GuardPercepts, GuardProfile, bool> evalGuard)
        {
            var attackSeq = new Sequence(
                "seq_attack",
                new Condition("cond_visible", bb => evalGuard("player_visible", bb.Percepts, bb.Profile)),
                new Condition("cond_in_range", bb => evalGuard("in_attack_range", bb.Percepts, bb.Profile)),
                new ActionNode("attack", bb => (BtStatus.Running, new GuardIntent(
                    GuardAction.Attack,
                    bb.Percepts.PlayerPos ?? bb.Percepts.LastSeenPos,
                    0f))));

            var chaseSeq = new Sequence(
                "seq_chase",
                new Condition("cond_visible_or_pursuing", bb => ShouldChase(evalGuard, bb)),
                new ActionNode("chase", bb => (BtStatus.Running, new GuardIntent(
                    GuardAction.Chase,
                    bb.Percepts.PlayerPos ?? bb.Percepts.LastSeenPos ?? bb.Route[0],
                    bb.Profile.chase_speed))));

            var investigateSeq = new Sequence(
                "seq_investigate",
                new Condition("cond_heard_sound", bb => ShouldInvestigate(evalGuard, bb)),
                new ActionNode("investigate", bb => (BtStatus.Running, new GuardIntent(
                    GuardAction.Investigate,
                    bb.Percepts.Sound.HasValue
                        ? bb.Percepts.Sound.Value.Origin
                        : bb.Percepts.LastSeenPos ?? bb.Route[0],
                    bb.Profile.investigate_speed))));

            var searchSeq = new Sequence(
                "seq_search",
                new Condition("cond_lost_or_arrived", bb => ShouldSearch(evalGuard, bb)),
                new ActionNode("search", bb => (BtStatus.Running, new GuardIntent(
                    GuardAction.Search,
                    bb.Percepts.LastSeenPos ?? bb.Percepts.GuardPos,
                    0f))));

            var returnSeq = new Sequence(
                "seq_return",
                new Condition("cond_search_expired", bb => ShouldReturn(evalGuard, bb)),
                new ActionNode("return", bb => (BtStatus.Running, new GuardIntent(
                    GuardAction.Return,
                    bb.Route[0],
                    bb.Profile.patrol_speed))));

            var patrolSeq = new Sequence(
                "seq_patrol",
                new Condition("cond_route_assigned", bb => evalGuard("route_assigned", bb.Percepts, bb.Profile)),
                new ActionNode("patrol", ActPatrol));

            var idleAct = new ActionNode("idle", _ => (BtStatus.Running, new GuardIntent(GuardAction.Idle, null, 0f)));

            return new ReactiveSelector(
                "root_selector",
                attackSeq,
                chaseSeq,
                investigateSeq,
                searchSeq,
                returnSeq,
                patrolSeq,
                idleAct);
        }

        /// <summary>Formats the active branch chain the way the shipped JavaScript runtime does.</summary>
        public static string FormatActiveTrace(BtTickResult result)
        {
            var composites = new List<string>();
            foreach (var entry in result.Trace)
            {
                if ((entry.Kind == BtNodeKind.ReactiveSelector || entry.Kind == BtNodeKind.Selector || entry.Kind == BtNodeKind.Sequence)
                    && entry.Status != BtStatus.Failure)
                {
                    composites.Add($"{entry.Kind}[{entry.Node}]");
                }
            }

            composites.Reverse();

            var activeLeaves = new List<BtTraceEntry>();
            foreach (var entry in result.Trace)
            {
                if ((entry.Kind == BtNodeKind.Condition || entry.Kind == BtNodeKind.Action) && entry.Status != BtStatus.Failure)
                {
                    activeLeaves.Add(entry);
                }
            }

            var leaves = new List<string>();
            for (var i = Math.Max(0, activeLeaves.Count - 2); i < activeLeaves.Count; i++)
            {
                var entry = activeLeaves[i];
                leaves.Add(entry.Kind == BtNodeKind.Action
                    ? $"Act:{entry.Node} ({entry.StatusName})"
                    : $"Cond[{entry.Node}]");
            }

            var chain = string.Join(" > ", composites) + (composites.Count > 0 && leaves.Count > 0 ? " > " : string.Empty) +
                        string.Join(" > ", leaves);
            var abortSuffix = result.AbortedNodes.Count > 0
                ? " \u00b7 preempted(" + string.Join(",", result.AbortedNodes) + ")"
                : string.Empty;
            return chain + abortSuffix;
        }

        /// <summary>Counts composite and leaf nodes in a tree.</summary>
        public static int CountNodes(BtNode node)
        {
            var total = 1;
            switch (node)
            {
                case Sequence seq:
                    foreach (var child in seq.Children)
                    {
                        total += CountNodes(child);
                    }

                    break;
                case Selector sel:
                    foreach (var child in sel.Children)
                    {
                        total += CountNodes(child);
                    }

                    break;
                case ReactiveSelector reactive:
                    foreach (var child in reactive.Children)
                    {
                        total += CountNodes(child);
                    }

                    break;
            }

            return total;
        }

        private static bool ShouldChase(Func<string, GuardPercepts, GuardProfile, bool> evalGuard, BtBlackboard bb)
        {
            var p = bb.Percepts;
            if (evalGuard("player_visible", p, bb.Profile))
            {
                return true;
            }

            return (bb.LatchState == "chase" || bb.LatchState == "attack") &&
                   !evalGuard("lost_sight_long", p, bb.Profile);
        }

        private static bool ShouldInvestigate(Func<string, GuardPercepts, GuardProfile, bool> evalGuard, BtBlackboard bb)
        {
            var p = bb.Percepts;
            if (evalGuard("heard_sound", p, bb.Profile))
            {
                return true;
            }

            return bb.LatchState == "investigate" && !evalGuard("arrived_and_quiet", p, bb.Profile);
        }

        private static bool ShouldSearch(Func<string, GuardPercepts, GuardProfile, bool> evalGuard, BtBlackboard bb)
        {
            var p = bb.Percepts;
            if ((bb.LatchState == "chase" || bb.LatchState == "attack") && evalGuard("lost_sight_long", p, bb.Profile))
            {
                return true;
            }

            if (bb.LatchState == "investigate" && evalGuard("arrived_and_quiet", p, bb.Profile))
            {
                return true;
            }

            return bb.LatchState == "search" && !evalGuard("search_expired", p, bb.Profile);
        }

        private static bool ShouldReturn(Func<string, GuardPercepts, GuardProfile, bool> evalGuard, BtBlackboard bb)
        {
            var p = bb.Percepts;
            if (bb.LatchState == "search" && evalGuard("search_expired", p, bb.Profile))
            {
                return true;
            }

            return bb.LatchState == "return" && !evalGuard("at_route_start", p, bb.Profile);
        }

        private static (BtStatus Status, GuardIntent Intent) ActPatrol(BtBlackboard bb)
        {
            var wp = bb.Route[bb.WpIndex % bb.Route.Count];
            if (GuardPerception.Distance(bb.Percepts.GuardPos, wp) < 0.65f)
            {
                bb.WpIndex = (bb.WpIndex + 1) % bb.Route.Count;
                wp = bb.Route[bb.WpIndex];
            }

            return (BtStatus.Running, new GuardIntent(GuardAction.Patrol, wp, bb.Profile.patrol_speed));
        }
    }
}
