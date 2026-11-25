using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim
{
    /// <summary>Guard controller selected by the game, matching the browser game's Guard button.</summary>
    public enum GuardArchitecture
    {
        /// <summary>Table driven seven state FSM with waypoint patrol.</summary>
        Fsm,
        /// <summary>Reactive priority behavior tree.</summary>
        Bt,
        /// <summary>Hierarchical StateTree with HTN tactical planner and occupancy-grid search.</summary>
        StateTree,
    }

    /// <summary>Final result of a market run.</summary>
    public enum HeistOutcome
    {
        /// <summary>The run is still live.</summary>
        None,
        /// <summary>The store detective detained the thief.</summary>
        Busted,
        /// <summary>The thief left through the EAS gate with every objective met.</summary>
        Escaped,
    }

    /// <summary>Phase of the staff radio backup machine.</summary>
    public enum BackupPhase
    {
        /// <summary>No responder is active.</summary>
        None,
        /// <summary>A responder is walking to the last known position.</summary>
        Respond,
        /// <summary>The responder arrived and is sweeping the area.</summary>
        Sweep,
    }

    /// <summary>Outcome of one action button attempt, reported so the HUD can explain a silent return.</summary>
    public enum InteractOutcome
    {
        /// <summary>No attempt was made this tick, or the run is not accepting input.</summary>
        None,
        /// <summary>An interaction channel started.</summary>
        Started,
        /// <summary>The bag has no free slot for the nearby good.</summary>
        BagFull,
        /// <summary>The target is close enough but the view to it is blocked.</summary>
        BlockedLineOfSight,
        /// <summary>The power panel is close enough but the view to it is blocked.</summary>
        PanelOutOfReach,
        /// <summary>No interaction target is available from here.</summary>
        NoTarget,
    }

    /// <summary>Read only state record returned by the debug snapshot helper.</summary>
    public readonly struct HeistSnapshot
    {
        /// <summary>Creates a snapshot record.</summary>
        public HeistSnapshot(
            int tick,
            Vector2 player,
            Vector2 guard,
            Vector2 staff,
            string guardState,
            GuardArchitecture arch,
            float heat,
            int tier,
            string phase,
            int bagCount,
            float stolenValue,
            IReadOnlyList<string> bag,
            IReadOnlyList<HeistObjective> objectives,
            BackupPhase backupPhase,
            Vector2? backupTarget,
            HeistOutcome outcome,
            bool visible,
            int guardWaypoint,
            int staffWaypoint,
            bool camerasDisabled,
            bool vitrineOpen,
            int activeTags,
            int easAlarmTicks,
            string channelKind,
            float channelProgress,
            float channelHold,
            ChannelResult lastChannelResult,
            int cameraSightings,
            Vector2? lastSeenPos,
            bool detectedNow,
            bool firstDetectionEdge,
            bool panelInRange,
            bool detaggerInRange,
            bool exitInRange,
            bool exitBlockedByTags,
            InteractOutcome lastInteraction,
            bool hasSecondGuard = false,
            Vector2 guard2 = default,
            string guard2State = "idle",
            int guard2Waypoint = 0,
            string guard1Plan = null,
            string guard2Plan = null,
            GuardTacticalRole guard1Role = GuardTacticalRole.Patrol,
            GuardTacticalRole guard2Role = GuardTacticalRole.Patrol,
            bool cctvRestoring = false,
            float cctvRestoreProgress = 0f,
            int cctvRestoreCount = 0)
        {
            Tick = tick;
            Player = player;
            Guard = guard;
            Staff = staff;
            GuardState = guardState;
            Arch = arch;
            Heat = heat;
            Tier = tier;
            Phase = phase;
            BagCount = bagCount;
            StolenValue = stolenValue;
            Bag = bag;
            Objectives = objectives;
            BackupPhase = backupPhase;
            BackupTarget = backupTarget;
            Outcome = outcome;
            Visible = visible;
            GuardWaypoint = guardWaypoint;
            StaffWaypoint = staffWaypoint;
            CamerasDisabled = camerasDisabled;
            VitrineOpen = vitrineOpen;
            ActiveTags = activeTags;
            EasAlarmTicks = easAlarmTicks;
            ChannelKind = channelKind;
            ChannelProgress = channelProgress;
            ChannelHold = channelHold;
            LastChannelResult = lastChannelResult;
            CameraSightings = cameraSightings;
            LastSeenPos = lastSeenPos;
            DetectedNow = detectedNow;
            FirstDetectionEdge = firstDetectionEdge;
            PanelInRange = panelInRange;
            DetaggerInRange = detaggerInRange;
            ExitInRange = exitInRange;
            ExitBlockedByTags = exitBlockedByTags;
            LastInteraction = lastInteraction;
            HasSecondGuard = hasSecondGuard;
            Guard2 = guard2;
            Guard2State = guard2State;
            Guard2Waypoint = guard2Waypoint;
            Guard1Plan = guard1Plan;
            Guard2Plan = guard2Plan;
            Guard1Role = guard1Role;
            Guard2Role = guard2Role;
            CctvRestoring = cctvRestoring;
            CctvRestoreProgress = cctvRestoreProgress;
            CctvRestoreCount = cctvRestoreCount;
        }

        /// <summary>Simulation tick number.</summary>
        public int Tick { get; }

        /// <summary>Player position in tiles.</summary>
        public Vector2 Player { get; }

        /// <summary>Guard position in tiles.</summary>
        public Vector2 Guard { get; }

        /// <summary>Staff position in tiles.</summary>
        public Vector2 Staff { get; }

        /// <summary>Guard state name, lower case.</summary>
        public string GuardState { get; }

        /// <summary>Active controller.</summary>
        public GuardArchitecture Arch { get; }

        /// <summary>Current heat in the range 0 to 1.</summary>
        public float Heat { get; }

        /// <summary>Alert tier index for the current heat.</summary>
        public int Tier { get; }

        /// <summary>Coarse heist phase label.</summary>
        public string Phase { get; }

        /// <summary>Number of goods in the bag.</summary>
        public int BagCount { get; }

        /// <summary>Total euro value of the stolen goods.</summary>
        public float StolenValue { get; }

        /// <summary>Ids of the goods in the bag.</summary>
        public IReadOnlyList<string> Bag { get; }

        /// <summary>Objectives in declaration order.</summary>
        public IReadOnlyList<HeistObjective> Objectives { get; }

        /// <summary>Current radio backup phase.</summary>
        public BackupPhase BackupPhase { get; }

        /// <summary>Responder target when one is active.</summary>
        public Vector2? BackupTarget { get; }

        /// <summary>Run outcome.</summary>
        public HeistOutcome Outcome { get; }

        /// <summary>Whether the thief was visible to any sensor this tick.</summary>
        public bool Visible { get; }

        /// <summary>Current guard patrol waypoint index.</summary>
        public int GuardWaypoint { get; }

        /// <summary>Current staff patrol waypoint index.</summary>
        public int StaffWaypoint { get; }

        /// <summary>Whether the CCTV power panel has been cut.</summary>
        public bool CamerasDisabled { get; }

        /// <summary>Whether the spirits vitrine has been forced open.</summary>
        public bool VitrineOpen { get; }

        /// <summary>Number of active RF tags in the bag.</summary>
        public int ActiveTags { get; }

        /// <summary>Remaining ticks of the EAS gate alarm warning.</summary>
        public int EasAlarmTicks { get; }

        /// <summary>Kind of the active channel, or null when idle.</summary>
        public string ChannelKind { get; }

        /// <summary>Seconds accumulated by the active channel.</summary>
        public float ChannelProgress { get; }

        /// <summary>Seconds the active channel must hold to finish.</summary>
        public float ChannelHold { get; }

        /// <summary>Result of the most recent channel tick.</summary>
        public ChannelResult LastChannelResult { get; }

        /// <summary>Bit mask of CCTV rigs that see the thief, one bit per camera declaration index.</summary>
        public int CameraSightings { get; }

        /// <summary>Last position reported by a sensor, or null before the first sighting.</summary>
        public Vector2? LastSeenPos { get; }

        /// <summary>Whether any sensor sees the thief on this tick.</summary>
        public bool DetectedNow { get; }

        /// <summary>Whether visibility first went true on this tick.</summary>
        public bool FirstDetectionEdge { get; }

        /// <summary>Whether the CCTV panel is close enough and still live.</summary>
        public bool PanelInRange { get; }

        /// <summary>Whether the de-tagger is close enough to use.</summary>
        public bool DetaggerInRange { get; }

        /// <summary>Whether the thief stands inside the EAS gate radius.</summary>
        public bool ExitInRange { get; }

        /// <summary>Whether the exit is blocked because RF tags are still in the bag.</summary>
        public bool ExitBlockedByTags { get; }

        /// <summary>Outcome of the most recent action button attempt.</summary>
        public InteractOutcome LastInteraction { get; }

        /// <summary>Whether a second coordinated security guard is active in this shift.</summary>
        public bool HasSecondGuard { get; }

        /// <summary>Second guard position in tiles.</summary>
        public Vector2 Guard2 { get; }

        /// <summary>Second guard state name, lower case.</summary>
        public string Guard2State { get; }

        /// <summary>Current second guard patrol waypoint index.</summary>
        public int Guard2Waypoint { get; }

        /// <summary>Active StateTree/HTN plan name for Guard 1, if running StateTree.</summary>
        public string Guard1Plan { get; }

        /// <summary>Active StateTree/HTN plan name for Guard 2, if running StateTree.</summary>
        public string Guard2Plan { get; }

        /// <summary>Active squad blackboard tactical role for Guard 1.</summary>
        public GuardTacticalRole Guard1Role { get; }

        /// <summary>Active squad blackboard tactical role for Guard 2.</summary>
        public GuardTacticalRole Guard2Role { get; }

        /// <summary>True while a security guard is actively rebooting the disabled CCTV breaker panel.</summary>
        public bool CctvRestoring { get; }

        /// <summary>Normalized progress [0, 1] of an active guard CCTV panel reboot.</summary>
        public float CctvRestoreProgress { get; }

        /// <summary>Number of times security guards have restored disabled CCTV power during this shift.</summary>
        public int CctvRestoreCount { get; }

        /// <summary>Number of CCTV rigs that see the thief on this tick.</summary>
        public int CameraSightingCount
        {
            get
            {
                var count = 0;
                var bits = CameraSightings;
                while (bits != 0)
                {
                    count += bits & 1;
                    bits >>= 1;
                }

                return count;
            }
        }

        /// <summary>Whether the CCTV rig at the given declaration index sees the thief.</summary>
        public bool CameraSees(int index)
        {
            return index >= 0 && index < 32 && (CameraSightings & (1 << index)) != 0;
        }

        /// <summary>Whether a radio responder is active.</summary>
        public bool HasBackup => BackupPhase != BackupPhase.None;
    }

    /// <summary>Deterministic port of the browser game market loop: movement, sensors, staff, heist and guard.</summary>
    public sealed class HeistGame
    {
        /// <summary>Tile speed constant from market-app.js line 16.</summary>
        public const float TileSpeed = 3.1f;

        /// <summary>Sprint speed multiplier from market-app.js line 643.</summary>
        public const float SprintMultiplier = 1.48f;

        /// <summary>Crouch speed multiplier from market-app.js line 643.</summary>
        public const float CrouchMultiplier = 0.6f;

        /// <summary>Camera spot relay cooldown in ticks from market-app.js line 719.</summary>
        public const int CameraPingCooldown = 150;

        /// <summary>Heat tier that arms the staff radio, from market-app.js line 55.</summary>
        public const int BackupRadioTier = 4;

        /// <summary>Radio cooldown in ticks from market-app.js line 54.</summary>
        public const int RadioCooldownTicks = 600;

        /// <summary>Responder sweep length in ticks from market-app.js line 54.</summary>
        public const int SweepTicks = 240;

        /// <summary>Responder hard cap in ticks from market-app.js line 54.</summary>
        public const int RespondHardCapTicks = 900;

        /// <summary>Responder speed factor from market-app.js line 55.</summary>
        public const float BackupSpeedFactor = 0.95f;

        /// <summary>Distance at which a responder starts sweeping, from market-app.js line 55.</summary>
        public const float BackupArrival = 0.6f;

        /// <summary>Staff shelf patrol speed factor from market-app.js line 735.</summary>
        public const float StaffPatrolSpeedFactor = 0.72f;

        /// <summary>EAS gate alarm radius in tiles from market-app.js line 677.</summary>
        public const float EasGateRadius = 1.7f;

        /// <summary>EAS gate alarm recovery ticks from market-app.js line 683.</summary>
        public const int EasAlarmTicks = 420;

        /// <summary>Player spawn used by resetHeist, market-app.js line 500.</summary>
        public static readonly Vector2 PlayerStart = new Vector2(1.3f, 4.5f);

        /// <summary>Guard spawn from market-app.js line 82.</summary>
        public static readonly Vector2 GuardStart = new Vector2(13.5f, 4.5f);

        /// <summary>Second security guard spawn covering the south/east departments.</summary>
        public static readonly Vector2 Guard2Start = new Vector2(24.5f, 22.5f);

        /// <summary>Staff spawn from market-app.js line 83.</summary>
        public static readonly Vector2 StaffStart = new Vector2(21.5f, 4.5f);

        private const float RoomSize = 9f;
        private const float RoomInset = 0.75f;
        private const float WallClearance = 0.45f;
        private const float DoorwayClearance = 0.85f;
        private const float MoveBandMin = 3.55f;
        private const float MoveBandMax = 5.45f;
        private const int StaffReportTicks = 260;
        private const int StaffReportVisibleFloor = 200;
        private const float StaffSightRange = 4.2f;
        private const float StaffSightHalfAngle = 0.45f;
        /// <summary>Seconds required to saw through and trip the main CCTV breaker panel.</summary>
        public const float PowerHold = 5.2f;

        /// <summary>Interaction reach to the CCTV breaker panel in tiles.</summary>
        public const float PanelRange = 1.65f;

        private const float DetaggerRange = 2.3f;
        private const float StealRange = 2.4f;
        private const float DetagHold = 1.8f;
        private const float VitrineHold = 3.2f;
        private const float VitrineFallbackI = 17.2f;
        private const float VitrineFallbackJ = 1.0f;

        private GuardProfile _profile;
        private readonly NavGrid _grid;
        private readonly List<BlockedBox> _blockedBoxes = new();
        private readonly List<SightBox> _sightBoxes = new();
        private readonly Vector2[] _guardRoute;
        private readonly Vector2[] _guard2Route;
        private readonly Vector2[] _staffRoute;
        private readonly GuardSquadBlackboard _squad;
        private readonly int _guardCount;
        private readonly byte[] _navStepMask;
        private readonly int[] _bfsQueue;
        private readonly int[] _bfsParent;
        private readonly int[] _bfsStamp;
        private int _bfsEpoch;
        private readonly Func<Vector2, Vector2, bool> _losCheck;
        private readonly HashSet<string> _stolenIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _taggedIds = new(StringComparer.Ordinal);
        private readonly CameraState[] _cameras = new CameraState[MarketData.Cameras.Length];

        private GuardArchitecture _arch;
        private GuardFsm _fsm;
        private GuardBt _bt;
        private GuardStateTree _stateTree1;
        private GuardFsm _fsm2;
        private GuardBt _bt2;
        private GuardStateTree _stateTree2;
        private HeistState _state;
        private Vector2 _player;
        private Vector2 _guard;
        private Vector2 _guard2;
        private Vector2 _staff;
        private float _guardAngle;
        private float _guard2Angle;
        private float _staffAngle;
        private int _guardWp;
        private int _guard2Wp;
        private int _staffWp;
        private int _tick;
        private float _clock;
        private float _stolenValue;
        private float _hp;
        private int _staffAlert;
        private bool _visible;
        private bool _wasVisible;
        private bool _firstDetectionEdge;
        private ChannelResult _lastChannelResult;
        private InteractOutcome _lastInteraction;
        private GuardAction _guardAction;
        private GuardAction _guard2Action;
        private Vector2? _lastSeenPos;
        private Vector2 _observedPlayerVelocity;
        private SoundWave _soundWave;
        private int _easAlarm;
        private BackupPhase _backupPhase;
        private Vector2? _backupTarget;
        private int _backupDispatchedTick;
        private int _backupSweepUntil;
        private int _backupLastRadioTick;
        private int _lostSightTicks;
        private int _searchTicks;
        private bool _arrivedAtTarget;
        private bool _atRouteStart;
        private int _guard2LostSightTicks;
        private int _guard2SearchTicks;
        private bool _guard2ArrivedAtTarget;
        private bool _guard2AtRouteStart;
        private float? _targetI;
        private float? _targetJ;
        private string _pendingStealId;
        private int _powerArcStage;
        private int _cctvRestoreCount;
        private HeistOutcome _outcome;
        private bool _frozenPatrol;
        private NavState _guardNav = new();
        private NavState _guard2Nav = new();
        private NavState _staffNav = new();
        private NavState _playerNav = new();
        private GuardPercepts _lastPercepts;
        private GuardPercepts _lastGuard2Percepts;
        private GuardIntent _lastGuardIntent;
        private GuardIntent _lastGuard2Intent;
        private FsmTransition _lastFsmTransition;
        private IReadOnlyList<BtTraceEntry> _lastBtTrace = Array.Empty<BtTraceEntry>();
        private IReadOnlyList<string> _lastBtAborted = Array.Empty<string>();
        private StateTreeTickResult _lastStateTree1;
        private StateTreeTickResult _lastStateTree2;

        /// <summary>Creates a deterministic market game with the given guard profile, controller, and guard count.</summary>
        public HeistGame(GuardProfile profile, GuardArchitecture arch = GuardArchitecture.Fsm, int guardCount = 1)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _arch = arch;
            _guardCount = Math.Clamp(guardCount, 1, 2);
            _grid = MarketGrid.Build();
            BuildBlockedBoxes();
            BuildSightBoxes();
            var navCount = _grid.Width * _grid.Height;
            _navStepMask = new byte[navCount];
            _bfsQueue = new int[navCount];
            _bfsParent = new int[navCount];
            _bfsStamp = new int[navCount];
            BuildNavStepMask();
            _losCheck = (a, b) => LineOfSight(a, b, null);
            _guardRoute = ToPoints(MarketData.GuardRoute);
            _guard2Route = ToPoints(MarketData.Guard2Route);
            _staffRoute = ToPoints(MarketData.StaffRoute);
            _squad = new GuardSquadBlackboard(_grid);
            Reset();
        }

        /// <summary>Number of stolen items currently carried in the player's bag.</summary>
        public int BagCount => _state.BagCount;

        /// <summary>Active guard controller.</summary>
        public GuardArchitecture Arch => _arch;

        /// <summary>Number of active security guards in this shift (1 or 2).</summary>
        public int GuardCount => _guardCount;

        /// <summary>True when a second coordinated security guard is active.</summary>
        public bool HasSecondGuard => _guardCount >= 2;

        /// <summary>Active guard personality profile.</summary>
        public GuardProfile Profile => _profile;

        /// <summary>Authoritative guard facing angle in radians.</summary>
        public float GuardAngle => _guardAngle;

        /// <summary>Authoritative second guard facing angle in radians.</summary>
        public float Guard2Angle => _guard2Angle;

        /// <summary>Authoritative staff facing angle in radians.</summary>
        public float StaffAngle => _staffAngle;

        /// <summary>Total number of CCTV cameras in the level.</summary>
        public int CameraCount => _cameras.Length;

        /// <summary>Authoritative current sweep angle in radians for the specified CCTV camera.</summary>
        public float GetCameraAngle(int index)
        {
            return index >= 0 && index < _cameras.Length ? _cameras[index].Angle : 0f;
        }

        /// <summary>Most recent sensor percepts evaluated by Guard 1.</summary>
        public GuardPercepts LastPercepts => _lastPercepts;

        /// <summary>Most recent sensor percepts evaluated by Guard 2.</summary>
        public GuardPercepts LastGuard2Percepts => _lastGuard2Percepts;

        /// <summary>Most recent locomotion/action intent emitted by Guard 1.</summary>
        public GuardIntent LastIntent => _lastGuardIntent;

        /// <summary>Most recent locomotion/action intent emitted by Guard 2.</summary>
        public GuardIntent LastGuard2Intent => _lastGuard2Intent;

        /// <summary>Most recent FSM transition rule that fired.</summary>
        public FsmTransition LastFsmTransition => _lastFsmTransition;

        /// <summary>Most recent Behavior Tree evaluation trace.</summary>
        public IReadOnlyList<BtTraceEntry> LastBtTrace => _lastBtTrace;

        /// <summary>Nodes preempted via Abort() during the most recent Behavior Tree tick.</summary>
        public IReadOnlyList<string> LastBtAborted => _lastBtAborted;

        /// <summary>Most recent StateTree tick result for Guard 1.</summary>
        public StateTreeTickResult LastStateTree1 => _lastStateTree1;

        /// <summary>Most recent StateTree tick result for Guard 2.</summary>
        public StateTreeTickResult LastStateTree2 => _lastStateTree2;

        /// <summary>Shared squad blackboard and occupancy grid.</summary>
        public GuardSquadBlackboard SquadBlackboard => _squad;

        /// <summary>True while a security guard is actively working on rebooting the disabled CCTV panel.</summary>
        public bool CctvRestoring => _squad.CctvRestoreInProgress;

        /// <summary>Normalized progress [0, 1] of the active guard CCTV reboot.</summary>
        public float CctvRestoreProgress => _squad.CctvRestoreProgress;

        /// <summary>Count of completed guard CCTV breaker restorations in the current shift.</summary>
        public int CctvRestoreCount => _cctvRestoreCount;

        /// <summary>Origin of the currently expanding acoustic wave, or null when quiet.</summary>
        public Vector2? ActiveSoundOrigin => _soundWave.HasValue ? _soundWave.Origin : (Vector2?)null;

        /// <summary>Current radius of the expanding acoustic wave in tiles.</summary>
        public float ActiveSoundRadius => _soundWave.HasValue ? _soundWave.Radius : 0f;

        /// <summary>Maximum radius of the expanding acoustic wave in tiles.</summary>
        public float ActiveSoundMaxRadius => _soundWave.HasValue ? _soundWave.MaxRadius : 0f;

        /// <summary>Whether guard and staff movement is frozen for deterministic tests.</summary>
        public bool FrozenPatrol
        {
            get => _frozenPatrol;
            set => _frozenPatrol = value;
        }

        /// <summary>Current heat in the range 0 to 1.</summary>
        public float Heat => _state.Heat;

        /// <summary>Current alert tier index.</summary>
        public int Tier => _state.Tier;

        /// <summary>Optional external speed multiplier (e.g. haul weight / fatigue); defaults to 1 for parity.</summary>
        public float PlayerSpeedScale { get; set; } = 1f;

        /// <summary>Optional external guard speed multiplier used in live play; defaults to 1 for parity.</summary>
        public float GuardSpeedScale { get; set; } = 1f;

        /// <summary>Run outcome.</summary>
        public HeistOutcome Outcome => _outcome;

        /// <summary>Player position in tiles.</summary>
        public Vector2 PlayerPosition => _player;

        /// <summary>Guard position in tiles.</summary>
        public Vector2 GuardPosition => _guard;

        /// <summary>Second guard position in tiles.</summary>
        public Vector2 Guard2Position => _guard2;

        /// <summary>Second guard state name, lower case.</summary>
        public string Guard2State => StateName(_guard2Action);

        /// <summary>Staff position in tiles.</summary>
        public Vector2 StaffPosition => _staff;

        /// <summary>Restores the browser game's start state without changing the controller choice.</summary>
        public void Reset()
        {
            _state = new HeistState(MarketData.BagCapacity, MarketData.ObjectiveIds);
            _player = PlayerStart;
            _guard = GuardStart;
            _guard2 = Guard2Start;
            _staff = StaffStart;
            _guardAngle = 1.5f;
            _guard2Angle = 3.14f;
            _staffAngle = 1.2f;
            _guardWp = 0;
            _guard2Wp = 0;
            _staffWp = 0;
            _tick = 0;
            _clock = 0f;
            _stolenIds.Clear();
            _taggedIds.Clear();
            _stolenValue = 0f;
            _hp = 100f;
            _staffAlert = 0;
            _visible = false;
            _wasVisible = false;
            _firstDetectionEdge = false;
            _lastChannelResult = ChannelResult.None;
            _lastInteraction = InteractOutcome.None;
            _lastSeenPos = null;
            _observedPlayerVelocity = Vector2.zero;
            _soundWave = default;
            _easAlarm = 0;
            _backupPhase = BackupPhase.None;
            _backupTarget = null;
            _backupDispatchedTick = -9999;
            _backupSweepUntil = 0;
            _backupLastRadioTick = -9999;
            _lostSightTicks = 0;
            _searchTicks = 0;
            _arrivedAtTarget = false;
            _atRouteStart = false;
            _guard2LostSightTicks = 0;
            _guard2SearchTicks = 0;
            _guard2ArrivedAtTarget = false;
            _guard2AtRouteStart = false;
            _targetI = null;
            _targetJ = null;
            _pendingStealId = null;
            _powerArcStage = 0;
            _cctvRestoreCount = 0;
            _outcome = HeistOutcome.None;
            for (var i = 0; i < _cameras.Length; i++)
            {
                var def = MarketData.Cameras[i];
                _cameras[i] = new CameraState(def, (float)def.BaseAngle, false, -999, 0);
            }

            _guardNav = new NavState();
            _guard2Nav = new NavState();
            _staffNav = new NavState();
            _playerNav = new NavState();
            _fsm = new GuardFsm(GuardAction.Patrol, _guardRoute);
            _bt = new GuardBt(GuardFsm.EvalGuard, _guardRoute, "patrol");
            _stateTree1 = new GuardStateTree(0, _guardRoute, GuardAction.Patrol);
            _fsm2 = new GuardFsm(GuardAction.Patrol, _guard2Route);
            _bt2 = new GuardBt(GuardFsm.EvalGuard, _guard2Route, "patrol");
            _stateTree2 = new GuardStateTree(1, _guard2Route, GuardAction.Patrol);
            _guardAction = GuardAction.Patrol;
            _guard2Action = GuardAction.Patrol;
            _lastGuard2Intent = default;
            _lastGuard2Percepts = default;
            _lastStateTree1 = default;
            _lastStateTree2 = default;
            _squad.Reset();
            _squad.GuardPositions[0] = _guard;
            _squad.GuardPositions[1] = _guard2;
        }

        /// <summary>Toggles between FSM and BT controllers, preserving compatibility with the parity suite.</summary>
        public void SwitchArch()
        {
            SetArch(_arch == GuardArchitecture.Fsm ? GuardArchitecture.Bt : GuardArchitecture.Fsm);
        }

        /// <summary>Switches the active guard controller architecture mid-run while preserving actor state.</summary>
        public void SetArch(GuardArchitecture arch)
        {
            _arch = arch;
            switch (arch)
            {
                case GuardArchitecture.Bt:
                    _bt = new GuardBt(GuardFsm.EvalGuard, _guardRoute, StateName(_guardAction));
                    _bt2 = new GuardBt(GuardFsm.EvalGuard, _guard2Route, StateName(_guard2Action));
                    break;
                case GuardArchitecture.StateTree:
                    _stateTree1 = new GuardStateTree(0, _guardRoute, _guardAction);
                    _stateTree2 = new GuardStateTree(1, _guard2Route, _guard2Action);
                    break;
                default:
                    _fsm = new GuardFsm(_guardAction, _guardRoute);
                    _fsm2 = new GuardFsm(_guard2Action, _guard2Route);
                    break;
            }
        }

        /// <summary>Cycles the guard architecture across StateTree, FSM, and BT without resetting the run.</summary>
        public void CycleArch()
        {
            var next = _arch switch
            {
                GuardArchitecture.StateTree => GuardArchitecture.Fsm,
                GuardArchitecture.Fsm => GuardArchitecture.Bt,
                _ => GuardArchitecture.StateTree,
            };
            SetArch(next);
        }

        /// <summary>Hot-swaps the active guard personality profile mid-run without resetting positions.</summary>
        public void SetProfile(GuardProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _profile = profile;
        }

        /// <summary>Reseeds the squad blackboard PRNG used for randomized corner-inspection detours.</summary>
        public void ReseedSquad(uint seed)
        {
            _squad.Reseed(seed);
        }

        /// <summary>Emits a decoy coin acoustic wave at the nearest walkable tile to lure the guard.</summary>
        public Vector2 TossDecoy(Vector2 target, float maxRadius = 7.5f)
        {
            var clampedX = Math.Clamp(target.x, 1.2f, MarketData.WidthTiles - 1.2f);
            var clampedY = Math.Clamp(target.y, 1.2f, MarketData.HeightTiles - 1.2f);
            var walk = MarketGrid.NearestWalkable(_grid, clampedX, clampedY);
            var origin = walk.HasValue
                ? new Vector2(walk.Value.X + 0.5f, walk.Value.Y + 0.5f)
                : new Vector2(clampedX, clampedY);
            EmitSound(origin.x, origin.y, maxRadius);
            return origin;
        }

        /// <summary>Moves the player without walking, mirroring the browser debug teleport.</summary>
        public void TeleportPlayer(float i, float j)
        {
            _player = new Vector2(i, j);
            _playerNav = new NavState();
            _observedPlayerVelocity = Vector2.zero;
        }

        /// <summary>Moves the guard without walking, mirroring the browser debug setGuard.</summary>
        public void SetGuard(float i, float j)
        {
            _guard = new Vector2(i, j);
            _guardNav = new NavState();
        }

        /// <summary>Moves the second security guard without walking for deterministic tests.</summary>
        public void SetGuard2(float i, float j)
        {
            _guard2 = new Vector2(i, j);
            _guard2Nav = new NavState();
        }

        /// <summary>Moves the staff member without walking, mirroring the browser debug setStaff.</summary>
        public void SetStaff(float i, float j)
        {
            _staff = new Vector2(i, j);
            _staffNav = new NavState();
        }

        /// <summary>Sets heat directly for scenario tests.</summary>
        public void SetHeat(float heat)
        {
            _state.SetHeat(heat);
        }

        /// <summary>Returns an allocation light state record for tests.</summary>
        public HeistSnapshot Snapshot()
        {
            return new HeistSnapshot(
                _tick,
                _player,
                _guard,
                _staff,
                StateName(_guardAction),
                _arch,
                _state.Heat,
                _state.Tier,
                _state.Phase,
                _state.BagCount,
                _stolenValue,
                _state.Bag,
                _state.Objectives,
                _backupPhase,
                _backupTarget,
                _outcome,
                _visible,
                _guardWp,
                _staffWp,
                _state.CamerasDisabled,
                _state.VitrineOpen,
                _taggedIds.Count,
                _easAlarm,
                _state.Channel?.Kind,
                _state.Channel?.Progress ?? 0f,
                _state.Channel?.Hold ?? 0f,
                _lastChannelResult,
                SightingBits(),
                _lastSeenPos,
                _visible,
                _firstDetectionEdge,
                Distance(_player, ToVector(MarketData.Panel.I, MarketData.Panel.J)) <= PanelRange && !_state.CamerasDisabled,
                Distance(_player, ToVector(MarketData.Detagger.I, MarketData.Detagger.J)) <= DetaggerRange,
                Distance(_player, ToVector(MarketData.EasGate.I, MarketData.EasGate.J)) <= EasGateRadius,
                Distance(_player, ToVector(MarketData.EasGate.I, MarketData.EasGate.J)) <= EasGateRadius && _taggedIds.Count > 0,
                _lastInteraction,
                HasSecondGuard,
                _guard2,
                StateName(_guard2Action),
                _guard2Wp,
                _arch == GuardArchitecture.StateTree ? _lastStateTree1.PlanName : null,
                _arch == GuardArchitecture.StateTree && HasSecondGuard ? _lastStateTree2.PlanName : null,
                _squad.AssignedRoles[0],
                _squad.AssignedRoles[1],
                _squad.CctvRestoreInProgress,
                _squad.CctvRestoreProgress,
                _cctvRestoreCount);
        }

        /// <summary>Builds the CCTV sighting mask from the persisted camera states.</summary>
        private int SightingBits()
        {
            var bits = 0;
            if (_state.CamerasDisabled)
            {
                return bits;
            }

            for (var i = 0; i < _cameras.Length && i < 31; i++)
            {
                if (_cameras[i].Sees)
                {
                    bits |= 1 << i;
                }
            }

            return bits;
        }

        /// <summary>Advances the whole market simulation by one deterministic tick.</summary>
        public void Step(float dt, HeistInput input)
        {
            if (_outcome != HeistOutcome.None || dt <= 0f)
            {
                return;
            }

            var prof = _profile;
            _lastInteraction = InteractOutcome.None;
            _firstDetectionEdge = false;
            _tick += 1;
            var tick = _tick;
            _clock += dt;
            _state.Elapsed += dt;

            var prevPlayer = _player;
            StepPlayer(dt, input, out var moving);

            var exit = ToVector(MarketData.EasGate.I, MarketData.EasGate.J);
            if (Distance(_player, exit) <= EasGateRadius)
            {
                if (_taggedIds.Count > 0)
                {
                    if (_easAlarm <= 0)
                    {
                        _state.SetHeat(1f);
                        _lastSeenPos = _player;
                        EmitSound(exit.x, exit.y, 9.0f);
                        _easAlarm = EasAlarmTicks;
                    }

                    _easAlarm = Math.Max(0, _easAlarm - 1);
                    return;
                }

                if (_stolenValue >= MarketData.Quota && _state.RequiredMet())
                {
                    _state.CompleteObjective("escape");
                    _outcome = HeistOutcome.Escaped;
                    return;
                }
            }
            else if (_easAlarm > 0)
            {
                _easAlarm = Math.Max(0, _easAlarm - 1);
            }

            var heard = false;
            var heard2 = false;
            var heardAge = 999;
            var heardI = 0f;
            var heardJ = 0f;
            var waveOrigin = default(Vector2);
            if (_soundWave.HasValue)
            {
                _soundWave.Radius += dt * 5.2f;
                _soundWave.Age += 1;
                heardAge = _soundWave.Age;
                waveOrigin = _soundWave.Origin;
                var effective = (_soundWave.MaxRadius + 2.5f) * prof.hearing_scale;
                if (_soundWave.Age <= prof.sound_memory_ticks)
                {
                    if (Distance(_guard, waveOrigin) <= effective)
                    {
                        heard = true;
                        heardI = waveOrigin.x;
                        heardJ = waveOrigin.y;
                        _lastSeenPos = waveOrigin;
                    }

                    if (HasSecondGuard && Distance(_guard2, waveOrigin) <= effective)
                    {
                        heard2 = true;
                        _lastSeenPos = waveOrigin;
                    }
                }

                if (_soundWave.Radius > _soundWave.MaxRadius)
                {
                    _soundWave = default;
                }
            }

            var cameraSpot = false;
            var sweepBoost = 1f + Math.Max(0, _state.Tier - 1) * 0.08f;
            var seenCameras = 0;
            for (var i = 0; i < _cameras.Length; i++)
            {
                var camera = _cameras[i];
                camera.Angle = (float)(camera.Def.BaseAngle
                    + Math.Sin(_clock * camera.Def.SweepSpeed * sweepBoost + camera.Def.I) * camera.Def.SweepAmp);
                camera.Sees = CameraSees(camera);
                if (camera.Sees)
                {
                    seenCameras += 1;
                }

                camera.PingCooldown = Math.Max(0, camera.PingCooldown - 1);
                if (camera.Sees && !_state.CamerasDisabled)
                {
                    cameraSpot = true;
                    camera.SeenAt = tick;
                    if (camera.PingCooldown <= 0)
                    {
                        camera.PingCooldown = CameraPingCooldown;
                        _lastSeenPos = _player;
                    }
                }

                _cameras[i] = camera;
            }

            StepStaff(dt, tick);

            var guardSees = EvaluateGuardSight(_guard, _guardAngle, prof, out var playerDistance, out var rel);
            var dist2 = float.MaxValue;
            var rel2 = 0f;
            var guard2Sees = HasSecondGuard && EvaluateGuardSight(_guard2, _guard2Angle, prof, out dist2, out rel2);
            var anyGuardSees = guardSees || guard2Sees;
            var minGuardDist = HasSecondGuard ? MathF.Min(playerDistance, dist2) : playerDistance;

            _visible = anyGuardSees || cameraSpot || (_staffAlert > 0 && _staffAlert > StaffReportVisibleFloor);
            _firstDetectionEdge = _visible && !_wasVisible;
            _wasVisible = _visible;

            var invDt = dt > 1e-4f ? 1f / dt : 0f;
            var instantVel = (_player - prevPlayer) * invDt;
            if (_visible)
            {
                _observedPlayerVelocity = Vector2.Lerp(_observedPlayerVelocity, instantVel, 0.45f);
            }
            else
            {
                _observedPlayerVelocity *= 0.94f;
            }

            var motion = moving && InMotionZone(_player);
            var staffReport = _staffAlert > StaffReportVisibleFloor && !_state.CamerasDisabled;
            var detection = Detection.Contribution(
                new DetectionInput(
                    anyGuardSees,
                    minGuardDist <= prof.close_proximity * 1.5f,
                    seenCameras,
                    staffReport,
                    input.Sprint && moving,
                    _taggedIds.Count,
                    motion,
                    input.Crouch,
                    _state.CamerasDisabled),
                dt);

            // Prevent passive RF tags in the bag from pinning Exposure at 100% forever when completely unseen:
            // while hidden out of sight and not sprinting/tripping motion, heat must always cool down steadily.
            if (!_visible && !motion && !(input.Sprint && moving))
            {
                var minCooldown = -(input.Crouch ? 0.09f : 0.055f) * dt;
                if (detection > minCooldown)
                {
                    detection = minCooldown;
                }
            }

            _state.AddHeat(detection);

            var g1PerceptVisible = _arch == GuardArchitecture.StateTree ? guardSees : _visible;
            if (g1PerceptVisible || (_arch != GuardArchitecture.StateTree && _visible))
            {
                _lastSeenPos = _player;
                _lostSightTicks = 0;
                _searchTicks = 0;
            }
            else
            {
                if (_visible)
                {
                    _lastSeenPos = _player;
                }

                _lostSightTicks += 1;
                if (_guardAction == GuardAction.Search)
                {
                    _searchTicks += 1;
                }
                else
                {
                    _searchTicks = 0;
                }
            }

            SoundEvent? sensedSound = null;
            if (heard)
            {
                sensedSound = new SoundEvent(new Vector2(heardI, heardJ), heardAge, 1f);
            }

            // Recompute arrival flags fresh every tick so they never latch true across later investigations or returns.
            var activeInvestigateTarget = sensedSound.HasValue
                ? sensedSound.Value.Origin
                : (_lastGuardIntent.Target ?? _lastSeenPos);
            _arrivedAtTarget = _guardAction == GuardAction.Investigate
                && activeInvestigateTarget.HasValue
                && Distance(_guard, activeInvestigateTarget.Value) < 0.8f;

            var activeReturnTarget = _guardRoute[_guardWp];
            _atRouteStart = _guardAction == GuardAction.Return
                && Distance(_guard, activeReturnTarget) < 0.85f;

            var percepts = new GuardPercepts(
                tick,
                _guard,
                _guardAngle,
                g1PerceptVisible,
                g1PerceptVisible ? _player : (Vector2?)null,
                playerDistance,
                rel,
                sensedSound,
                _state.Heat,
                _lastSeenPos,
                _atRouteStart,
                _arrivedAtTarget,
                _lostSightTicks,
                _searchTicks,
                (int)_hp);
            _lastPercepts = percepts;

            if (_state.Tier >= 5 && ((guardSees && playerDistance < 2.4f) || (guard2Sees && dist2 < 2.4f)))
            {
                _outcome = HeistOutcome.Busted;
                return;
            }

            var active = _state.Channel;
            var channelResult = _state.TickChannel(dt, moving || _visible || _state.Tier >= 5);
            _lastChannelResult = channelResult;
            if (channelResult == ChannelResult.Done && active != null)
            {
                ApplyChannel(active);
            }
            else if (channelResult == ChannelResult.Running && active != null && active.Kind == "power")
            {
                var frac = active.Progress / MathF.Max(0.01f, active.Hold);
                var panelPos = ToVector(MarketData.Panel.I, MarketData.Panel.J);
                if (frac >= 0.35f && _powerArcStage < 1)
                {
                    _powerArcStage = 1;
                    EmitSound(panelPos.x, panelPos.y, 6.0f);
                    _squad.InjectSound(panelPos, 0.70f);
                    _state.AddHeat(0.04f);
                }
                else if (frac >= 0.72f && _powerArcStage < 2)
                {
                    _powerArcStage = 2;
                    EmitSound(panelPos.x, panelPos.y, 7.2f);
                    _squad.InjectSound(panelPos, 0.85f);
                    _state.AddHeat(0.05f);
                }
            }
            else if (_state.Channel == null)
            {
                _powerArcStage = 0;
            }

            if (_stolenValue >= MarketData.Quota)
            {
                _state.CompleteObjective("quota");
            }

            if (_state.HadTags && _taggedIds.Count == 0)
            {
                _state.CompleteObjective("detag");
            }

            if (_state.Elapsed > 2f && _state.Phase == "case")
            {
                _state.Phase = "infiltrate";
            }

            if (_state.Channel != null && _state.Phase == "infiltrate")
            {
                _state.Phase = "execute";
            }

            if (_stolenValue >= MarketData.Quota && _state.Phase == "execute")
            {
                _state.Phase = "exfil";
            }

            // Update shared squad tactical blackboard & occupancy probability wave
            _squad.CamerasDisabled = _state.CamerasDisabled;
            _squad.VitrineOpen = _state.VitrineOpen;
            _squad.EasAlarmActive = _easAlarm > 0;
            _squad.Heat = _state.Heat;
            _squad.HeatTier = _state.Tier;
            _squad.GuardPositions[0] = _guard;
            _squad.GuardActions[0] = _guardAction;
            _squad.GuardPositions[1] = _guard2;
            _squad.GuardActions[1] = _guard2Action;

            if (_visible)
            {
                _squad.SeedSighting(_player, _observedPlayerVelocity, tick);
            }
            else if ((heard || heard2) && heardAge == 1)
            {
                _squad.InjectSound(waveOrigin);
            }

            if (tick % 6 == 0)
            {
                var cornerRange = prof.fov_distance * (1f + Math.Max(0, _state.Tier - 2) * 0.08f);
                _squad.RecordVisionStaleness(_guard, _guardAngle, prof.fov_half_angle, cornerRange, tick, _losCheck);
                if (HasSecondGuard)
                {
                    _squad.RecordVisionStaleness(_guard2, _guard2Angle, prof.fov_half_angle, cornerRange, tick, _losCheck);
                }
            }

            if (!_visible && tick % 4 == 0)
            {
                _squad.DiffuseStep(tick: tick);
                var fovRange = prof.fov_distance * (1f + Math.Max(0, _state.Tier - 2) * 0.08f);
                _squad.ClearVisionCone(_guard, _guardAngle, prof.fov_half_angle, fovRange, _losCheck);
                if (HasSecondGuard)
                {
                    _squad.ClearVisionCone(_guard2, _guard2Angle, prof.fov_half_angle, fovRange, _losCheck);
                }

                if (!_state.CamerasDisabled)
                {
                    for (var i = 0; i < _cameras.Length; i++)
                    {
                        var cam = _cameras[i];
                        _squad.ClearVisionCone(
                            new Vector2((float)cam.Def.I, (float)cam.Def.J),
                            cam.Angle,
                            (float)cam.Def.HalfAngle,
                            (float)cam.Def.Range,
                            _losCheck);
                    }
                }
            }

            _squad.UpdateSquadRoles(tick, _guardCount, guardSees, guard2Sees, _player);
            _squad.CctvRestoreInProgress = false;
            _squad.CctvRestoreProgress = 0f;

            if (_arch == GuardArchitecture.Fsm)
            {
                var (intent, transition) = _fsm.Tick(percepts, prof);
                _lastGuardIntent = intent;
                _lastFsmTransition = transition;
                _guardAction = _fsm.State;
            }
            else if (_arch == GuardArchitecture.Bt)
            {
                var result = _bt.Tick(percepts, prof);
                _lastGuardIntent = result.Intent;
                _lastBtTrace = result.Trace;
                _lastBtAborted = result.AbortedNodes;
                _guardAction = ActionOf(result.ActiveLeaf);
            }
            else
            {
                _lastStateTree1 = _stateTree1.Tick(percepts, prof, _squad, _grid);
                _lastGuardIntent = _lastStateTree1.Intent;
                _guardAction = _lastStateTree1.Action;
                _guardWp = _stateTree1.WpIndex;
            }

            if (StepGuardAgent(
                ref _guard,
                ref _guardAngle,
                ref _guardWp,
                _guardRoute,
                _guardNav,
                _guardAction,
                _lastGuardIntent,
                ref _arrivedAtTarget,
                ref _atRouteStart,
                dt,
                otherGuardPos: HasSecondGuard ? (Vector2?)_guard2 : null,
                guardPassSign: 1))
            {
                return;
            }

            if (HasSecondGuard)
            {
                var g2PerceptVisible = _arch == GuardArchitecture.StateTree ? guard2Sees : _visible;
                if (g2PerceptVisible)
                {
                    _guard2LostSightTicks = 0;
                    _guard2SearchTicks = 0;
                }
                else
                {
                    _guard2LostSightTicks += 1;
                    if (_guard2Action == GuardAction.Search)
                    {
                        _guard2SearchTicks += 1;
                    }
                    else
                    {
                        _guard2SearchTicks = 0;
                    }
                }

                SoundEvent? g2Sound = heard2 ? new SoundEvent(waveOrigin, heardAge, 1f) : (SoundEvent?)null;
                var g2InvestigateTarget = g2Sound.HasValue
                    ? g2Sound.Value.Origin
                    : (_lastGuard2Intent.Target ?? _lastSeenPos);
                _guard2ArrivedAtTarget = _guard2Action == GuardAction.Investigate
                    && g2InvestigateTarget.HasValue
                    && Distance(_guard2, g2InvestigateTarget.Value) < 0.8f;
                _guard2AtRouteStart = _guard2Action == GuardAction.Return
                    && Distance(_guard2, _guard2Route[_guard2Wp]) < 0.85f;

                var percepts2 = new GuardPercepts(
                    tick,
                    _guard2,
                    _guard2Angle,
                    g2PerceptVisible,
                    g2PerceptVisible ? _player : (Vector2?)null,
                    dist2,
                    rel2,
                    g2Sound,
                    _state.Heat,
                    _lastSeenPos,
                    _guard2AtRouteStart,
                    _guard2ArrivedAtTarget,
                    _guard2LostSightTicks,
                    _guard2SearchTicks,
                    (int)_hp);
                _lastGuard2Percepts = percepts2;

                if (_arch == GuardArchitecture.Fsm)
                {
                    var (intent2, _) = _fsm2.Tick(percepts2, prof);
                    _lastGuard2Intent = intent2;
                    _guard2Action = _fsm2.State;
                }
                else if (_arch == GuardArchitecture.Bt)
                {
                    var result2 = _bt2.Tick(percepts2, prof);
                    _lastGuard2Intent = result2.Intent;
                    _guard2Action = ActionOf(result2.ActiveLeaf);
                }
                else
                {
                    _lastStateTree2 = _stateTree2.Tick(percepts2, prof, _squad, _grid);
                    _lastGuard2Intent = _lastStateTree2.Intent;
                    _guard2Action = _lastStateTree2.Action;
                    _guard2Wp = _stateTree2.WpIndex;
                }

                if (StepGuardAgent(
                    ref _guard2,
                    ref _guard2Angle,
                    ref _guard2Wp,
                    _guard2Route,
                    _guard2Nav,
                    _guard2Action,
                    _lastGuard2Intent,
                    ref _guard2ArrivedAtTarget,
                    ref _guard2AtRouteStart,
                    dt,
                    otherGuardPos: _guard,
                    guardPassSign: -1))
                {
                    return;
                }
            }

            if (_squad.ConsumeCamerasRestoreRequest())
            {
                _state.CamerasDisabled = false;
                _squad.CamerasDisabled = false;
                _cctvRestoreCount++;
                _powerArcStage = 0;
                EmitSound((float)MarketData.Panel.I, (float)MarketData.Panel.J, 2.2f);
            }

            if (input.Action)
            {
                _lastInteraction = TryInteract();
            }
        }

        private bool EvaluateGuardSight(Vector2 guardPos, float guardAngle, GuardProfile prof, out float playerDistance, out float rel)
        {
            var dx = _player.x - guardPos.x;
            var dy = _player.y - guardPos.y;
            playerDistance = MathF.Sqrt(dx * dx + dy * dy);
            rel = GuardPerception.NormalizeAngle(MathF.Atan2(dy, dx) - guardAngle);
            var fov = prof.fov_distance * (1f + Math.Max(0, _state.Tier - 2) * 0.08f);
            var inCone = playerDistance <= fov && MathF.Abs(rel) <= prof.fov_half_angle;
            var close = playerDistance <= prof.close_proximity;
            return (inCone || close) && LineOfSight(guardPos, _player, null);
        }

        private bool StepGuardAgent(
            ref Vector2 guardPos,
            ref float guardAngle,
            ref int guardWp,
            Vector2[] route,
            NavState nav,
            GuardAction action,
            GuardIntent intent,
            ref bool arrivedAtTarget,
            ref bool atRouteStart,
            float dt,
            Vector2? otherGuardPos = null,
            int guardPassSign = 1)
        {
            var prof = _profile;
            var gi = guardPos.x;
            var gj = guardPos.y;
            var speed = prof.patrol_speed * (1f + Math.Max(0, _state.Tier - 2) * 0.05f);
            switch (action)
            {
                case GuardAction.Patrol:
                    if (_arch == GuardArchitecture.StateTree && intent.Target.HasValue)
                    {
                        gi = intent.Target.Value.x;
                        gj = intent.Target.Value.y;
                        if (intent.Speed <= 0.01f)
                        {
                            guardAngle += dt * 2.1f;
                            return false;
                        }
                    }
                    else
                    {
                        var waypoint = route[guardWp];
                        gi = waypoint.x;
                        gj = waypoint.y;
                        if (Distance(guardPos, waypoint) < 0.65f)
                        {
                            guardWp = (guardWp + 1) % route.Length;
                        }
                    }

                    break;
                case GuardAction.Investigate:
                    var investigate = intent.Target ?? _lastSeenPos ?? route[guardWp];
                    gi = investigate.x;
                    gj = investigate.y;
                    speed = intent.Speed > 0.01f ? intent.Speed : prof.investigate_speed;
                    arrivedAtTarget = Distance(guardPos, investigate) < 0.8f;
                    break;
                case GuardAction.Chase:
                    var chase = intent.Target ?? (_visible ? _player : _lastSeenPos ?? _player);
                    gi = chase.x;
                    gj = chase.y;
                    speed = intent.Speed > 0.01f ? intent.Speed : prof.chase_speed;
                    _hp = Math.Min(100f, _hp + dt * 4f);
                    break;
                case GuardAction.Attack:
                    guardAngle = MathF.Atan2(_player.y - guardPos.y, _player.x - guardPos.x);
                    _hp = Math.Max(0f, _hp - dt * 58f);
                    if (_hp <= 0f)
                    {
                        _outcome = HeistOutcome.Busted;
                        return true;
                    }

                    return false;
                case GuardAction.Search:
                    var searchTarget = intent.Target ?? _lastSeenPos;
                    if (searchTarget.HasValue && Distance(guardPos, searchTarget.Value) > 0.75f)
                    {
                        gi = searchTarget.Value.x;
                        gj = searchTarget.Value.y;
                        speed = prof.investigate_speed;
                        break;
                    }

                    // Once at the target spot, sweep head side-to-side to check around corners
                    guardAngle += dt * 2.8f;
                    return false;
                case GuardAction.Return:
                    var start = (_arch == GuardArchitecture.StateTree && intent.Target.HasValue)
                        ? intent.Target.Value
                        : route[guardWp];
                    gi = start.x;
                    gj = start.y;
                    speed = prof.patrol_speed;
                    atRouteStart = Distance(guardPos, start) < 0.85f;
                    break;
                default:
                    return false;
            }

            var fdi = gi - guardPos.x;
            var fdj = gj - guardPos.y;
            var fd = MathF.Sqrt(fdi * fdi + fdj * fdj);
            var prevGuard = guardPos;
            var scaledSpeed = speed * Math.Clamp(GuardSpeedScale, 0.5f, 1.8f);
            if (!_frozenPatrol && fd > 0.08f && MoveToward(nav, ref guardPos, gi, gj, TileSpeed * scaledSpeed * dt))
            {
                var stepDx = guardPos.x - prevGuard.x;
                var stepDy = guardPos.y - prevGuard.y;
                var stepMagSq = stepDx * stepDx + stepDy * stepDy;
                if (otherGuardPos.HasValue && stepMagSq > 1e-6f)
                {
                    var sepX = guardPos.x - otherGuardPos.Value.x;
                    var sepY = guardPos.y - otherGuardPos.Value.y;
                    var sepDist = MathF.Sqrt(sepX * sepX + sepY * sepY);
                    if (sepDist < 0.80f)
                    {
                        var stepLen = MathF.Sqrt(stepMagSq);
                        var perpX = -stepDy / stepLen;
                        var perpY = stepDx / stepLen;
                        var lateralDot = sepX * perpX + sepY * perpY;
                        float side;
                        if (MathF.Abs(lateralDot) > 0.03f)
                        {
                            side = lateralDot >= 0f ? 1f : -1f;
                        }
                        else if (sepDist < 0.25f)
                        {
                            side = guardPassSign;
                        }
                        else
                        {
                            side = 1f;
                        }

                        var latX = perpX * side;
                        var latY = perpY * side;
                        if (sepDist > 0.02f)
                        {
                            var ax = sepX / sepDist;
                            var ay = sepY / sepDist;
                            var bx = latX * 0.78f + ax * 0.22f;
                            var by = latY * 0.78f + ay * 0.22f;
                            var bMag = MathF.Sqrt(bx * bx + by * by);
                            if (bMag > 1e-4f)
                            {
                                latX = bx / bMag;
                                latY = by / bMag;
                            }
                        }

                        var nudge = MathF.Min(0.35f * stepLen, (0.80f - sepDist) * 0.28f);
                        var nx = guardPos.x + latX * nudge;
                        var ny = guardPos.y + latY * nudge;
                        if (CanWalk(guardPos.x, guardPos.y, nx, ny))
                        {
                            guardPos = new Vector2(nx, ny);
                            stepDx = guardPos.x - prevGuard.x;
                            stepDy = guardPos.y - prevGuard.y;
                            stepMagSq = stepDx * stepDx + stepDy * stepDy;
                        }
                    }
                }

                float desiredAngle;
                if (action == GuardAction.Chase && LineOfSight(guardPos, new Vector2(gi, gj), null))
                {
                    desiredAngle = MathF.Atan2(gj - guardPos.y, gi - guardPos.x);
                }
                else if (stepMagSq > 1e-6f)
                {
                    desiredAngle = MathF.Atan2(stepDy, stepDx);
                }
                else
                {
                    desiredAngle = MathF.Atan2(fdj, fdi);
                }

                var angleDiff = GuardPerception.NormalizeAngle(desiredAngle - guardAngle);
                var maxTurn = 11.5f * dt;
                guardAngle = GuardPerception.NormalizeAngle(guardAngle + Math.Clamp(angleDiff, -maxTurn, maxTurn));

                if (_arch == GuardArchitecture.StateTree && action == GuardAction.Search)
                {
                    guardAngle += MathF.Sin(_clock * 5.2f) * 0.34f;
                }
            }

            return false;
        }

        private void StepStaff(float dt, int tick)
        {
            var waypoint = _staffRoute[_staffWp];
            if (_backupPhase == BackupPhase.None)
            {
                var sdi = waypoint.x - _staff.x;
                var sdj = waypoint.y - _staff.y;
                var sd = MathF.Sqrt(sdi * sdi + sdj * sdj);
                if (sd <= 0.25f)
                {
                    _staffWp = (_staffWp + 1) % _staffRoute.Length;
                }
                else if (!_frozenPatrol && MoveToward(_staffNav, ref _staff, waypoint.x, waypoint.y, TileSpeed * StaffPatrolSpeedFactor * dt))
                {
                    _staffAngle = MathF.Atan2(sdj, sdi);
                }

                if (_state.Tier >= BackupRadioTier
                    && _lastSeenPos.HasValue
                    && tick - _backupLastRadioTick >= RadioCooldownTicks)
                {
                    _backupTarget = _lastSeenPos;
                    _backupPhase = BackupPhase.Respond;
                    _backupDispatchedTick = tick;
                    _backupLastRadioTick = tick;
                }
            }
            else if (_backupPhase == BackupPhase.Respond)
            {
                if (_state.Tier >= BackupRadioTier && _lastSeenPos.HasValue)
                {
                    _backupTarget = _lastSeenPos;
                }

                var target = _backupTarget ?? _staff;
                var bdi = target.x - _staff.x;
                var bdj = target.y - _staff.y;
                var bd = MathF.Sqrt(bdi * bdi + bdj * bdj);
                if (bd <= BackupArrival)
                {
                    _backupPhase = BackupPhase.Sweep;
                    _backupSweepUntil = tick + SweepTicks;
                }
                else if (tick - _backupDispatchedTick > RespondHardCapTicks)
                {
                    _backupPhase = BackupPhase.None;
                }
                else if (!_frozenPatrol
                    && MoveToward(_staffNav, ref _staff, target.x, target.y, TileSpeed * BackupSpeedFactor * dt))
                {
                    _staffAngle = MathF.Atan2(bdj, bdi);
                }
            }
            else
            {
                if (!_frozenPatrol)
                {
                    _staffAngle += dt * 2.2f;
                }

                if (tick >= _backupSweepUntil)
                {
                    _backupPhase = BackupPhase.None;
                }
            }

            if (StaffSeesThief() && !_state.CamerasDisabled)
            {
                if (_staffAlert <= 0)
                {
                    _staffAlert = StaffReportTicks;
                    _lastSeenPos = _player;
                }
            }

            if (_staffAlert > 0)
            {
                _staffAlert -= 1;
            }
        }

        private void StepPlayer(float dt, HeistInput input, out bool moving)
        {
            var di = input.MoveI;
            var dj = input.MoveJ;
            var hasDirection = input.HasDirection;
            moving = hasDirection || (_targetI.HasValue && _targetJ.HasValue);

            if (hasDirection)
            {
                _targetI = null;
                _targetJ = null;
                var length = MathF.Sqrt(di * di + dj * dj);
                if (length <= 0f)
                {
                    return;
                }

                var step = TileSpeed
                    * Math.Clamp(PlayerSpeedScale, 0.25f, 1.5f)
                    * (input.Sprint ? SprintMultiplier : 1f)
                    * (input.Crouch ? CrouchMultiplier : 1f)
                    * dt;
                MoveEntity(ref _player, di / length, dj / length, step);
                if (input.Sprint && _tick % 20 == 0)
                {
                    EmitSound(_player.x, _player.y, 6.0f);
                }
                else if (!input.Crouch && !input.Sprint && _tick % 18 == 0 && IsAlertGuardNearPlayer(2.8f))
                {
                    _squad.InjectSound(_player, 0.55f);
                }
            }
            else if (_targetI.HasValue && _targetJ.HasValue)
            {
                var tdi = _targetI.Value - _player.x;
                var tdj = _targetJ.Value - _player.y;
                var td = MathF.Sqrt(tdi * tdi + tdj * tdj);
                if (td > 0.45f
                    && MoveToward(_playerNav, ref _player, _targetI.Value, _targetJ.Value, TileSpeed * 1.08f * dt))
                {
                    return;
                }

                _targetI = null;
                _targetJ = null;
                if (!string.IsNullOrEmpty(_pendingStealId) && _state.Channel == null)
                {
                    TryInteract();
                }

                _pendingStealId = null;
            }
        }

        private bool IsAlertGuardNearPlayer(float maxRange)
        {
            var g1Alert = _guardAction == GuardAction.Investigate || _guardAction == GuardAction.Search || _guardAction == GuardAction.Chase;
            if (g1Alert && Distance(_guard, _player) <= maxRange)
            {
                return true;
            }

            if (!HasSecondGuard)
            {
                return false;
            }

            var g2Alert = _guard2Action == GuardAction.Investigate || _guard2Action == GuardAction.Search || _guard2Action == GuardAction.Chase;
            return g2Alert && Distance(_guard2, _player) <= maxRange;
        }

        private void ApplyChannel(ChannelState active)
        {
            switch (active.Kind)
            {
                case "steal":
                    var loot = MarketData.LootFor(active.Id);
                    if (!loot.HasValue || _stolenIds.Contains(active.Id))
                    {
                        return;
                    }

                    var position = ItemTilePosition(MarketData.Fixture(active.Id));
                    if (LineOfSight(_player, position, active.Id))
                    {
                        StealItem(loot.Value, position);
                    }

                    return;
                case "detag":
                    DetagAll();
                    return;
                case "power":
                    _state.CamerasDisabled = true;
                    _state.CompleteObjective("power");
                    _powerArcStage = 0;
                    var panelPos = ToVector(MarketData.Panel.I, MarketData.Panel.J);
                    EmitSound(panelPos.x, panelPos.y, 9.5f);
                    _squad.InjectSound(panelPos, 0.95f);
                    _state.AddHeat(0.14f);
                    return;
                case "vitrine":
                    _state.VitrineOpen = true;
                    _state.CompleteObjective("vitrine");
                    var spirits = MarketData.LootFor("m_spirits");
                    var vitrine = spirits.HasValue
                        ? ItemTilePosition(MarketData.Fixture("m_spirits"))
                        : new Vector2(VitrineFallbackI, VitrineFallbackJ);
                    EmitSound(vitrine.x, vitrine.y, 4.5f);
                    return;
            }
        }

        private void StealItem(LootEntry loot, Vector2 position)
        {
            if (_stolenIds.Contains(loot.Id))
            {
                return;
            }

            if (loot.Id == "m_spirits" && !_state.VitrineOpen)
            {
                return;
            }

            if (!_state.CanCarry)
            {
                return;
            }

            _state.PutInBag(loot.Id);
            _stolenIds.Add(loot.Id);
            _stolenValue += loot.Value;
            if (loot.Tagged)
            {
                _taggedIds.Add(loot.Id);
                _state.HadTags = true;
            }

            EmitSound(position.x, position.y, (float)loot.Noise + 2.2f);
        }

        private void DetagAll()
        {
            if (_taggedIds.Count == 0)
            {
                return;
            }

            _taggedIds.Clear();
            EmitSound((float)MarketData.Detagger.I, (float)MarketData.Detagger.J, 2.0f);
        }

        private InteractOutcome TryInteract()
        {
            if (_outcome != HeistOutcome.None || _state.Channel != null)
            {
                return InteractOutcome.None;
            }

            var panel = ToVector(MarketData.Panel.I, MarketData.Panel.J);
            if (Distance(_player, panel) <= PanelRange && !_state.CamerasDisabled)
            {
                if (!LineOfSight(_player, panel, null))
                {
                    return InteractOutcome.PanelOutOfReach;
                }

                _powerArcStage = 0;
                _state.StartChannel("power", null, PowerHold);
                return InteractOutcome.Started;
            }

            var detagger = ToVector(MarketData.Detagger.I, MarketData.Detagger.J);
            if (Distance(_player, detagger) <= DetaggerRange)
            {
                if (!LineOfSight(_player, detagger, null))
                {
                    return InteractOutcome.BlockedLineOfSight;
                }

                if (_taggedIds.Count > 0)
                {
                    _state.StartChannel("detag", null, DetagHold);
                    return InteractOutcome.Started;
                }

                DetagAll();
                return InteractOutcome.NoTarget;
            }

            var near = NearestLoot();
            if (!near.Found)
            {
                return InteractOutcome.NoTarget;
            }

            if (near.Distance <= StealRange)
            {
                if (!LineOfSight(_player, near.Position, near.Id))
                {
                    return InteractOutcome.BlockedLineOfSight;
                }

                if (near.Id == "m_spirits" && !_state.VitrineOpen)
                {
                    _state.StartChannel("vitrine", null, VitrineHold);
                    return InteractOutcome.Started;
                }

                return StartStealChannel(near.Loot) ? InteractOutcome.Started : InteractOutcome.BagFull;
            }

            // Never hijack the player into auto-running across the store when pressing E near the EAS exit gate
            var easExit = ToVector(MarketData.EasGate.I, MarketData.EasGate.J);
            if (Distance(_player, easExit) <= EasGateRadius + 0.8f)
            {
                return InteractOutcome.NoTarget;
            }

            return InteractOutcome.NoTarget;
        }

        private bool StartStealChannel(LootEntry loot)
        {
            if (!_state.CanCarry)
            {
                return false;
            }

            _state.StartChannel("steal", loot.Id, StealHold(loot));
            return true;
        }

        private static float StealHold(LootEntry loot)
        {
            return 1.4f + loot.Value / 70f * 2.2f;
        }

        private NearLoot NearestLoot()
        {
            var best = default(NearLoot);
            var bestDistance = float.MaxValue;
            foreach (var loot in MarketData.Loot)
            {
                if (_stolenIds.Contains(loot.Id))
                {
                    continue;
                }

                var position = ItemTilePosition(MarketData.Fixture(loot.Id));
                var distance = Distance(_player, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new NearLoot(true, loot, position, distance);
                }
            }

            return best;
        }

        private void EmitSound(float i, float j, float maxRadius)
        {
            _soundWave = new SoundWave(new Vector2(i, j), 0.25f, maxRadius, 0, true);
            _state.AddHeat(Detection.SoundSpike);
        }

        private bool CameraSees(CameraState camera)
        {
            var dx = _player.x - (float)camera.Def.I;
            var dy = _player.y - (float)camera.Def.J;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > camera.Def.Range)
            {
                return false;
            }

            var rel = GuardPerception.NormalizeAngle(MathF.Atan2(dy, dx) - camera.Angle);
            if (MathF.Abs(rel) > camera.Def.HalfAngle)
            {
                return false;
            }

            return LineOfSight(new Vector2((float)camera.Def.I, (float)camera.Def.J), _player, null);
        }

        private bool StaffSeesThief()
        {
            var dx = _player.x - _staff.x;
            var dy = _player.y - _staff.y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > StaffSightRange)
            {
                return false;
            }

            var rel = GuardPerception.NormalizeAngle(MathF.Atan2(dy, dx) - _staffAngle);
            if (MathF.Abs(rel) > StaffSightHalfAngle)
            {
                return false;
            }

            return LineOfSight(_staff, _player, null);
        }

        private bool InMotionZone(Vector2 position)
        {
            foreach (var zone in MarketData.MotionZones)
            {
                var dx = position.x - (float)zone.I;
                var dy = position.y - (float)zone.J;
                if (MathF.Sqrt(dx * dx + dy * dy) <= (float)zone.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool LineOfSight(Vector2 from, Vector2 to, string ignoreId)
        {
            if (!RoomLineOfSight(from, to))
            {
                return false;
            }

            foreach (var box in _sightBoxes)
            {
                if (ignoreId != null && box.Id == ignoreId)
                {
                    continue;
                }

                for (var sample = 1; sample <= 6; sample++)
                {
                    var u = sample / 7f;
                    var pi = from.x + (to.x - from.x) * u;
                    var pj = from.y + (to.y - from.y) * u;
                    if (pi > box.X0 && pi < box.X1 && pj > box.Y0 && pj < box.Y1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool RoomLineOfSight(Vector2 a, Vector2 b)
        {
            var ax = FloorDiv(a.x, RoomSize);
            var ay = FloorDiv(a.y, RoomSize);
            var bx = FloorDiv(b.x, RoomSize);
            var by = FloorDiv(b.y, RoomSize);
            if (ax == bx && ay == by)
            {
                return true;
            }

            if (Math.Abs(ax - bx) + Math.Abs(ay - by) != 1)
            {
                return false;
            }

            var aiLocal = PositiveModulo(a.x, RoomSize);
            var ajLocal = PositiveModulo(a.y, RoomSize);
            var biLocal = PositiveModulo(b.x, RoomSize);
            var bjLocal = PositiveModulo(b.y, RoomSize);
            return ax != bx
                ? InSightBand(ajLocal) && InSightBand(bjLocal)
                : InSightBand(aiLocal) && InSightBand(biLocal);
        }

        private bool CanWalk(float fromI, float fromJ, float toI, float toJ)
        {
            if (toI < RoomInset
                || toI > MarketData.WidthTiles - RoomInset
                || toJ < RoomInset
                || toJ > MarketData.HeightTiles - RoomInset)
            {
                return false;
            }

            var localI = toI - FloorDiv(toI, RoomSize) * RoomSize;
            var localJ = toJ - FloorDiv(toJ, RoomSize) * RoomSize;
            if ((localI < WallClearance || localI > RoomSize - WallClearance)
                && MathF.Abs(localJ - 4f) > DoorwayClearance)
            {
                return false;
            }

            if ((localJ < WallClearance || localJ > RoomSize - WallClearance)
                && MathF.Abs(localI - 4f) > DoorwayClearance)
            {
                return false;
            }

            if (FloorDiv(fromI, RoomSize) != FloorDiv(toI, RoomSize)
                || FloorDiv(fromJ, RoomSize) != FloorDiv(toJ, RoomSize))
            {
                var along = MathF.Abs(toI - fromI) >= MathF.Abs(toJ - fromJ) ? toJ : toI;
                var local = PositiveModulo(along, RoomSize);
                if (local < MoveBandMin || local > MoveBandMax)
                {
                    return false;
                }
            }

            return !IsBlocked(toI, toJ);
        }

        private bool IsBlocked(float i, float j)
        {
            foreach (var box in _blockedBoxes)
            {
                if (i > box.X0 && i < box.X1 && j > box.Y0 && j < box.Y1)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MoveEntity(ref Vector2 position, float ux, float uy, float stepLength)
        {
            var ni = position.x + ux * stepLength;
            var nj = position.y + uy * stepLength;
            if (CanWalk(position.x, position.y, ni, nj))
            {
                position = new Vector2(ni, nj);
                return true;
            }

            if (ux != 0f && CanWalk(position.x, position.y, ni, position.y))
            {
                position = new Vector2(ni, position.y);
                return true;
            }

            if (uy != 0f && CanWalk(position.x, position.y, position.x, nj))
            {
                position = new Vector2(position.x, nj);
                return true;
            }

            return false;
        }

        private bool HasClearWalkLine(Vector2 from, Vector2 to)
        {
            var di = to.x - from.x;
            var dj = to.y - from.y;
            var dist = MathF.Sqrt(di * di + dj * dj);
            if (dist <= 0.05f)
            {
                return true;
            }

            var steps = Math.Max(2, (int)MathF.Ceiling(dist / 0.24f));
            var prevX = from.x;
            var prevY = from.y;
            for (var s = 1; s <= steps; s++)
            {
                var u = (float)s / steps;
                var nextX = from.x + di * u;
                var nextY = from.y + dj * u;
                if (!CanWalk(prevX, prevY, nextX, nextY))
                {
                    return false;
                }

                prevX = nextX;
                prevY = nextY;
            }

            return true;
        }

        private bool MoveToward(NavState nav, ref Vector2 position, float goalI, float goalJ, float stepLength)
        {
            var goal = new Vector2(goalI, goalJ);
            if (HasClearWalkLine(position, goal))
            {
                nav.HasGoal = false;
                nav.Path = null;
                nav.RepathCooldown = 0;
                nav.LastPathFailed = false;
                var di = goalI - position.x;
                var dj = goalJ - position.y;
                var d = MathF.Sqrt(di * di + dj * dj);
                if (d > 1e-4f && MoveEntity(ref position, di / d, dj / d, MathF.Min(stepLength, d)))
                {
                    return true;
                }
            }

            if (FollowPath(nav, ref position, goalI, goalJ, stepLength))
            {
                return true;
            }

            return MoveThroughDoorway(nav, ref position, goalI, goalJ, stepLength);
        }

        private bool FollowPath(NavState nav, ref Vector2 position, float goalI, float goalJ, float stepLength)
        {
            if (nav.RepathCooldown > 0)
            {
                nav.RepathCooldown--;
            }

            var curX = FloorDiv(position.x, 1f);
            var curY = FloorDiv(position.y, 1f);
            var goalX = FloorDiv(goalI, 1f);
            var goalY = FloorDiv(goalJ, 1f);
            if (curX == goalX && curY == goalY)
            {
                nav.HasGoal = false;
                nav.Path = null;
                nav.LastPathFailed = false;
                var sdi = goalI - position.x;
                var sdj = goalJ - position.y;
                var sd = MathF.Sqrt(sdi * sdi + sdj * sdj);
                return sd > 1e-4f && MoveEntity(ref position, sdi / sd, sdj / sd, MathF.Min(stepLength, sd));
            }

            var goalChanged = !nav.HasGoal || nav.GoalX != goalX || nav.GoalY != goalY;
            var goalJump = Math.Abs(nav.GoalX - goalX) + Math.Abs(nav.GoalY - goalY);
            var shouldRepath = (!nav.HasGoal || nav.Path == null)
                ? (!nav.LastPathFailed || nav.GoalX != goalX || nav.GoalY != goalY || nav.RepathCooldown <= 0)
                : (goalChanged && (nav.RepathCooldown <= 0 || goalJump > 3));

            if (shouldRepath)
            {
                nav.Path = FindPathFallback(curX, curY, goalX, goalY);
                nav.Index = nav.Path != null && nav.Path.Count > 1 ? 1 : 0;
                nav.GoalX = goalX;
                nav.GoalY = goalY;
                nav.LastPathFailed = nav.Path == null || nav.Path.Count == 0;
                nav.HasGoal = !nav.LastPathFailed;
                nav.RepathCooldown = nav.LastPathFailed ? 6 : 5;
            }

            while (nav.Path != null && nav.Index < nav.Path.Count)
            {
                var isFinal = nav.Index == nav.Path.Count - 1;
                var curTile = nav.Path[nav.Index];
                var tx = isFinal ? goalI : curTile.X + 0.5f;
                var ty = isFinal ? goalJ : curTile.Y + 0.5f;
                var cdi = tx - position.x;
                var cdj = ty - position.y;
                var threshSq = isFinal ? 0.0064f : 0.0625f;
                if (cdi * cdi + cdj * cdj >= threshSq)
                {
                    break;
                }

                nav.Index++;
            }

            if (nav.Path == null || nav.Path.Count == 0 || nav.Index >= nav.Path.Count)
            {
                nav.HasGoal = false;
                nav.Path = null;
                if (nav.RepathCooldown <= 0)
                {
                    nav.RepathCooldown = 6;
                }

                return false;
            }

            // String-pull lookahead: cut diagonally across clear visible chords instead of 4-way tile steps
            var maxLook = Math.Min(nav.Path.Count - 1, nav.Index + 6);
            for (var k = maxLook; k > nav.Index; k--)
            {
                var isFinal = k == nav.Path.Count - 1;
                var cand = isFinal
                    ? new Vector2(goalI, goalJ)
                    : new Vector2(nav.Path[k].X + 0.5f, nav.Path[k].Y + 0.5f);
                if (HasClearWalkLine(position, cand))
                {
                    var ldi = cand.x - position.x;
                    var ldj = cand.y - position.y;
                    var ld = MathF.Sqrt(ldi * ldi + ldj * ldj);
                    if (ld > 1e-4f && MoveEntity(ref position, ldi / ld, ldj / ld, MathF.Min(stepLength, ld)))
                    {
                        nav.Index = k;
                        return true;
                    }
                }
            }

            var isLast = nav.Index == nav.Path.Count - 1;
            var tile = nav.Path[nav.Index];
            var wx = isLast ? goalI : tile.X + 0.5f;
            var wy = isLast ? goalJ : tile.Y + 0.5f;
            var di = wx - position.x;
            var dj = wy - position.y;
            var d = MathF.Sqrt(di * di + dj * dj);
            if (d <= 1e-4f)
            {
                return false;
            }

            return MoveEntity(ref position, di / d, dj / d, MathF.Min(stepLength, d));
        }

        private bool MoveThroughDoorway(NavState nav, ref Vector2 position, float goalI, float goalJ, float stepLength)
        {
            var waypoint = DoorwayTarget(position, goalI, goalJ);
            var di = waypoint.x - position.x;
            var dj = waypoint.y - position.y;
            var d = MathF.Sqrt(di * di + dj * dj);
            if (d < 0.04f)
            {
                return false;
            }

            if (MoveEntity(ref position, di / d, dj / d, MathF.Min(stepLength, d)))
            {
                return true;
            }

            if (MathF.Abs(di) > 0.01f && MoveEntity(ref position, MathF.Sign(di), 0f, stepLength))
            {
                return true;
            }

            if (MathF.Abs(dj) > 0.01f && MoveEntity(ref position, 0f, MathF.Sign(dj), stepLength))
            {
                return true;
            }

            nav.HasGoal = false;
            nav.Path = null;
            return false;
        }

        private static Vector2 DoorwayTarget(Vector2 position, float goalI, float goalJ)
        {
            var ex = FloorDiv(position.x, RoomSize);
            var ey = FloorDiv(position.y, RoomSize);
            var gx = FloorDiv(goalI, RoomSize);
            var gy = FloorDiv(goalJ, RoomSize);
            if (ex != gx)
            {
                var boundary = (ex < gx ? ex + 1 : ex) * RoomSize;
                return new Vector2(boundary + (ex < gx ? 0.4f : -0.4f), ey * RoomSize + 4.0f);
            }

            if (ey != gy)
            {
                var boundary = (ey < gy ? ey + 1 : ey) * RoomSize;
                return new Vector2(ex * RoomSize + 4.0f, boundary + (ey < gy ? 0.4f : -0.4f));
            }

            return new Vector2(goalI, goalJ);
        }

        private List<GridPos> FindPathFallback(int fromX, int fromY, int goalX, int goalY)
        {
            if (fromX == goalX && fromY == goalY)
            {
                return new List<GridPos> { new GridPos(fromX, fromY) };
            }

            var w = _grid.Width;
            var h = _grid.Height;
            if (fromX < 0 || fromY < 0 || fromX >= w || fromY >= h)
            {
                return null;
            }

            if (++_bfsEpoch == int.MaxValue)
            {
                Array.Clear(_bfsStamp, 0, _bfsStamp.Length);
                _bfsEpoch = 1;
            }

            var epoch = _bfsEpoch;
            var startIdx = fromY * w + fromX;
            var goalInBounds = goalX >= 0 && goalY >= 0 && goalX < w && goalY < h;
            var goalIdx = goalInBounds ? goalY * w + goalX : -1;
            var head = 0;
            var tail = 0;
            _bfsStamp[startIdx] = epoch;
            _bfsParent[startIdx] = -1;
            _bfsQueue[tail++] = startIdx;

            var bestIdx = startIdx;
            var bestDistance = float.MaxValue;
            var foundGoal = false;

            while (head < tail)
            {
                var at = _bfsQueue[head++];
                var mask = _navStepMask[at];
                if (mask == 0)
                {
                    continue;
                }

                if ((mask & 1) != 0 && ProcessBfsNeighbor(at, at + 1, w, goalX, goalY, goalIdx, epoch, ref tail, ref bestIdx, ref bestDistance, ref foundGoal)) break;
                if ((mask & 2) != 0 && ProcessBfsNeighbor(at, at - 1, w, goalX, goalY, goalIdx, epoch, ref tail, ref bestIdx, ref bestDistance, ref foundGoal)) break;
                if ((mask & 4) != 0 && ProcessBfsNeighbor(at, at + w, w, goalX, goalY, goalIdx, epoch, ref tail, ref bestIdx, ref bestDistance, ref foundGoal)) break;
                if ((mask & 8) != 0 && ProcessBfsNeighbor(at, at - w, w, goalX, goalY, goalIdx, epoch, ref tail, ref bestIdx, ref bestDistance, ref foundGoal)) break;
            }

            var targetIdx = foundGoal ? goalIdx : bestIdx;
            if (targetIdx == startIdx)
            {
                return null;
            }

            var path = new List<GridPos>();
            var current = targetIdx;
            while (current != startIdx)
            {
                var cy = current / w;
                var cx = current - cy * w;
                path.Add(new GridPos(cx, cy));
                current = _bfsParent[current];
            }

            path.Add(new GridPos(fromX, fromY));
            path.Reverse();
            return path;
        }

        private bool ProcessBfsNeighbor(
            int fromIdx,
            int nextIdx,
            int w,
            int goalX,
            int goalY,
            int goalIdx,
            int epoch,
            ref int tail,
            ref int bestIdx,
            ref float bestDistance,
            ref bool foundGoal)
        {
            if (_bfsStamp[nextIdx] == epoch)
            {
                return false;
            }

            _bfsStamp[nextIdx] = epoch;
            _bfsParent[nextIdx] = fromIdx;
            if (nextIdx == goalIdx)
            {
                foundGoal = true;
                return true;
            }

            _bfsQueue[tail++] = nextIdx;
            var ny = nextIdx / w;
            var nx = nextIdx - ny * w;
            var ddx = (float)(nx - goalX);
            var ddy = (float)(ny - goalY);
            var distance = ddx * ddx + ddy * ddy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIdx = nextIdx;
            }

            return false;
        }

        private void BuildNavStepMask()
        {
            var w = _grid.Width;
            var h = _grid.Height;
            for (var y = 0; y < h; y++)
            {
                var row = y * w;
                for (var x = 0; x < w; x++)
                {
                    byte mask = 0;
                    if (CanStep(x, y, x + 1, y)) mask |= 1;
                    if (CanStep(x, y, x - 1, y)) mask |= 2;
                    if (CanStep(x, y, x, y + 1)) mask |= 4;
                    if (CanStep(x, y, x, y - 1)) mask |= 8;
                    _navStepMask[row + x] = mask;
                }
            }
        }

        private bool CanStep(int fromX, int fromY, int toX, int toY)
        {
            if (!_grid.IsWalkable(toX, toY))
            {
                return false;
            }

            return CanWalk(fromX + 0.5f, fromY + 0.5f, toX + 0.5f, toY + 0.5f);
        }

        private void BuildBlockedBoxes()
        {
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

                float minX = int.MaxValue;
                float maxX = int.MinValue;
                float minY = int.MaxValue;
                float maxY = int.MinValue;
                foreach (var tile in tiles)
                {
                    minX = Math.Min(minX, tile.X);
                    maxX = Math.Max(maxX, tile.X);
                    minY = Math.Min(minY, tile.Y);
                    maxY = Math.Max(maxY, tile.Y);
                }

                _blockedBoxes.Add(new BlockedBox(minX - 0.02f, minY - 0.02f, maxX + 1.02f, maxY + 1.02f));
            }
        }

        private void BuildSightBoxes()
        {
            foreach (var fixture in MarketData.Fixtures)
            {
                var kind = MarketData.Kind(fixture.Kind);
                if (kind.Place == "wall" || kind.Place == "rug" || kind.Height < 1.4)
                {
                    continue;
                }

                var position = ItemTilePosition(fixture);
                _sightBoxes.Add(new SightBox(
                    fixture.Id,
                    position.x - 0.2f,
                    position.y - 0.2f,
                    position.x + (float)kind.SizeI * 0.7f,
                    position.y + (float)kind.SizeJ * 0.7f));
            }
        }

        private static Vector2 ItemTilePosition(Fixture fixture)
        {
            var kind = MarketData.Kind(fixture.Kind);
            var swapped = fixture.Face == "ne" || fixture.Face == "sw";
            var li = fixture.I + (swapped ? kind.SizeI / 2 : kind.SizeJ / 2);
            var lj = fixture.J + (swapped ? kind.SizeJ / 2 : kind.SizeI / 2);
            var room = MarketData.Room(fixture.Room);
            li = Math.Min(8.2, Math.Max(0.8, li));
            lj = Math.Min(8.2, Math.Max(0.8, lj));
            return new Vector2((float)(room.OriginI + li), (float)(room.OriginJ + lj));
        }

        private static Vector2[] ToPoints(GridPos[] route)
        {
            var points = new Vector2[route.Length];
            for (var i = 0; i < route.Length; i++)
            {
                points[i] = new Vector2(
                    route[i].X + 0.5f + (float)route[i].OffsetI,
                    route[i].Y + 0.5f);
            }

            return points;
        }

        private static Vector2 ToVector(double i, double j)
        {
            return new Vector2((float)i, (float)j);
        }

        private static float Distance(Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private static bool InSightBand(float value)
        {
            return value > 2.6f && value < 6.4f;
        }

        private static int FloorDiv(float value, float divisor)
        {
            return (int)MathF.Floor(value / divisor);
        }

        private static float PositiveModulo(float value, float modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }

        private static string StateName(GuardAction action)
        {
            return action switch
            {
                GuardAction.Idle => "idle",
                GuardAction.Patrol => "patrol",
                GuardAction.Investigate => "investigate",
                GuardAction.Chase => "chase",
                GuardAction.Attack => "attack",
                GuardAction.Search => "search",
                GuardAction.Return => "return",
                _ => "idle",
            };
        }

        private static GuardAction ActionOf(string leaf)
        {
            return leaf switch
            {
                "patrol" => GuardAction.Patrol,
                "investigate" => GuardAction.Investigate,
                "chase" => GuardAction.Chase,
                "attack" => GuardAction.Attack,
                "search" => GuardAction.Search,
                "return" => GuardAction.Return,
                _ => GuardAction.Idle,
            };
        }

        private struct CameraState
        {
            public CameraState(CameraDef def, float angle, bool sees, int seenAt, int pingCooldown)
            {
                Def = def;
                Angle = angle;
                Sees = sees;
                SeenAt = seenAt;
                PingCooldown = pingCooldown;
            }

            public CameraDef Def;
            public float Angle;
            public bool Sees;
            public int SeenAt;
            public int PingCooldown;
        }

        private struct SoundWave
        {
            public SoundWave(Vector2 origin, float radius, float maxRadius, int age, bool hasValue)
            {
                Origin = origin;
                Radius = radius;
                MaxRadius = maxRadius;
                Age = age;
                HasValue = hasValue;
            }

            public Vector2 Origin;
            public float Radius;
            public float MaxRadius;
            public int Age;
            public bool HasValue;
        }

        private readonly struct BlockedBox
        {
            public BlockedBox(float x0, float y0, float x1, float y1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public readonly float X0;
            public readonly float Y0;
            public readonly float X1;
            public readonly float Y1;
        }

        private readonly struct SightBox
        {
            public SightBox(string id, float x0, float y0, float x1, float y1)
            {
                Id = id;
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public readonly string Id;
            public readonly float X0;
            public readonly float Y0;
            public readonly float X1;
            public readonly float Y1;
        }

        private readonly struct NearLoot
        {
            public NearLoot(bool found, LootEntry loot, Vector2 position, float distance)
            {
                Found = found;
                Loot = loot;
                Position = position;
                Distance = distance;
                Id = loot.Id;
            }

            public readonly bool Found;
            public readonly LootEntry Loot;
            public readonly Vector2 Position;
            public readonly float Distance;
            public readonly string Id;
        }

        private sealed class NavState
        {
            public bool HasGoal;
            public int GoalX;
            public int GoalY;
            public List<GridPos> Path;
            public int Index;
            public int RepathCooldown;
            public bool LastPathFailed;
        }
    }
}
