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
            InteractOutcome lastInteraction)
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
        private const float PanelRange = 2.0f;
        private const float DetaggerRange = 2.3f;
        private const float StealRange = 2.4f;
        private const float PowerHold = 2.4f;
        private const float DetagHold = 1.8f;
        private const float VitrineHold = 3.2f;
        private const float VitrineFallbackI = 17.2f;
        private const float VitrineFallbackJ = 1.0f;

        private readonly GuardProfile _profile;
        private readonly NavGrid _grid;
        private readonly List<BlockedBox> _blockedBoxes = new();
        private readonly List<SightBox> _sightBoxes = new();
        private readonly Vector2[] _guardRoute;
        private readonly Vector2[] _staffRoute;
        private readonly HashSet<string> _stolenIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _taggedIds = new(StringComparer.Ordinal);
        private readonly CameraState[] _cameras = new CameraState[MarketData.Cameras.Length];

        private GuardArchitecture _arch;
        private GuardFsm _fsm;
        private GuardBt _bt;
        private HeistState _state;
        private Vector2 _player;
        private Vector2 _guard;
        private Vector2 _staff;
        private float _guardAngle;
        private float _staffAngle;
        private int _guardWp;
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
        private Vector2? _lastSeenPos;
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
        private float? _targetI;
        private float? _targetJ;
        private string _pendingStealId;
        private HeistOutcome _outcome;
        private bool _frozenPatrol;
        private NavState _guardNav = new();
        private NavState _staffNav = new();
        private NavState _playerNav = new();

        /// <summary>Creates a deterministic market game with the given guard profile and controller.</summary>
        public HeistGame(GuardProfile profile, GuardArchitecture arch = GuardArchitecture.Fsm)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _arch = arch;
            _grid = MarketGrid.Build();
            BuildBlockedBoxes();
            BuildSightBoxes();
            _guardRoute = ToPoints(MarketData.GuardRoute);
            _staffRoute = ToPoints(MarketData.StaffRoute);
            Reset();
        }

        /// <summary>Active guard controller.</summary>
        public GuardArchitecture Arch => _arch;

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

        /// <summary>Run outcome.</summary>
        public HeistOutcome Outcome => _outcome;

        /// <summary>Player position in tiles.</summary>
        public Vector2 PlayerPosition => _player;

        /// <summary>Guard position in tiles.</summary>
        public Vector2 GuardPosition => _guard;

        /// <summary>Staff position in tiles.</summary>
        public Vector2 StaffPosition => _staff;

        /// <summary>Restores the browser game's start state without changing the controller choice.</summary>
        public void Reset()
        {
            _state = new HeistState(MarketData.BagCapacity, MarketData.ObjectiveIds);
            _player = PlayerStart;
            _guard = GuardStart;
            _staff = StaffStart;
            _guardAngle = 1.5f;
            _staffAngle = 1.2f;
            _guardWp = 0;
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
            _targetI = null;
            _targetJ = null;
            _pendingStealId = null;
            _outcome = HeistOutcome.None;
            for (var i = 0; i < _cameras.Length; i++)
            {
                var def = MarketData.Cameras[i];
                _cameras[i] = new CameraState(def, (float)def.BaseAngle, false, -999, 0);
            }

            _guardNav = new NavState();
            _staffNav = new NavState();
            _playerNav = new NavState();
            _fsm = new GuardFsm(GuardAction.Patrol, _guardRoute);
            _bt = new GuardBt(GuardFsm.EvalGuard, _guardRoute, "patrol");
            _guardAction = GuardAction.Patrol;
        }

        /// <summary>Toggles the guard controller like the browser game's Guard button, leaving the run untouched.</summary>
        public void SwitchArch()
        {
            if (_arch == GuardArchitecture.Fsm)
            {
                _arch = GuardArchitecture.Bt;
                _bt = new GuardBt(GuardFsm.EvalGuard, _guardRoute, StateName(_guardAction));
            }
            else
            {
                _arch = GuardArchitecture.Fsm;
                _fsm = new GuardFsm(_guardAction, _guardRoute);
            }
        }

        /// <summary>Moves the player without walking, mirroring the browser debug teleport.</summary>
        public void TeleportPlayer(float i, float j)
        {
            _player = new Vector2(i, j);
            _playerNav = new NavState();
        }

        /// <summary>Moves the guard without walking, mirroring the browser debug setGuard.</summary>
        public void SetGuard(float i, float j)
        {
            _guard = new Vector2(i, j);
            _guardNav = new NavState();
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
                _lastInteraction);
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
            var heardAge = 999;
            var heardI = 0f;
            var heardJ = 0f;
            if (_soundWave.HasValue)
            {
                _soundWave.Radius += dt * 5.2f;
                _soundWave.Age += 1;
                heardAge = _soundWave.Age;
                var soundDistance = Distance(_guard, _soundWave.Origin);
                var effective = (_soundWave.MaxRadius + 2.5f) * prof.hearing_scale;
                if (soundDistance <= effective && _soundWave.Age <= prof.sound_memory_ticks)
                {
                    heard = true;
                    heardI = _soundWave.Origin.x;
                    heardJ = _soundWave.Origin.y;
                    _lastSeenPos = _soundWave.Origin;
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

            var dx = _player.x - _guard.x;
            var dy = _player.y - _guard.y;
            var playerDistance = MathF.Sqrt(dx * dx + dy * dy);
            var rel = GuardPerception.NormalizeAngle(MathF.Atan2(dy, dx) - _guardAngle);
            var fov = prof.fov_distance * (1f + Math.Max(0, _state.Tier - 2) * 0.08f);
            var inCone = playerDistance <= fov && MathF.Abs(rel) <= prof.fov_half_angle;
            var close = playerDistance <= prof.close_proximity;
            var guardSees = (inCone || close) && LineOfSight(_guard, _player, null);
            _visible = guardSees || cameraSpot || (_staffAlert > 0 && _staffAlert > StaffReportVisibleFloor);
            _firstDetectionEdge = _visible && !_wasVisible;
            _wasVisible = _visible;

            var motion = moving && InMotionZone(_player);
            var staffReport = _staffAlert > StaffReportVisibleFloor && !_state.CamerasDisabled;
            var detection = Detection.Contribution(
                new DetectionInput(
                    guardSees,
                    playerDistance <= prof.close_proximity * 1.5f,
                    seenCameras,
                    staffReport,
                    input.Sprint && moving,
                    _taggedIds.Count,
                    motion,
                    input.Crouch,
                    _state.CamerasDisabled),
                dt);
            _state.AddHeat(detection);

            if (_visible)
            {
                _lastSeenPos = _player;
                _lostSightTicks = 0;
                _searchTicks = 0;
            }
            else
            {
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

            var percepts = new GuardPercepts(
                tick,
                _guard,
                _guardAngle,
                _visible,
                _visible ? _player : (Vector2?)null,
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

            if (_state.Tier >= 5 && guardSees && playerDistance < 2.4f)
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

            if (_arch == GuardArchitecture.Fsm)
            {
                _fsm.Tick(percepts, prof);
                _guardAction = _fsm.State;
            }
            else
            {
                var result = _bt.Tick(percepts, prof);
                _guardAction = ActionOf(result.ActiveLeaf);
            }

            if (StepGuard(dt))
            {
                return;
            }

            if (input.Action)
            {
                _lastInteraction = TryInteract();
            }
        }

        private bool StepGuard(float dt)
        {
            var prof = _profile;
            var gi = _guard.x;
            var gj = _guard.y;
            var speed = prof.patrol_speed * (1f + Math.Max(0, _state.Tier - 2) * 0.05f);
            switch (_guardAction)
            {
                case GuardAction.Patrol:
                    var waypoint = _guardRoute[_guardWp];
                    gi = waypoint.x;
                    gj = waypoint.y;
                    if (Distance(_guard, waypoint) < 0.65f)
                    {
                        _guardWp = (_guardWp + 1) % _guardRoute.Length;
                    }

                    break;
                case GuardAction.Investigate:
                    var investigate = _lastSeenPos ?? new Vector2(13.5f, 13.5f);
                    gi = investigate.x;
                    gj = investigate.y;
                    speed = prof.investigate_speed;
                    _arrivedAtTarget = Distance(_guard, investigate) < 0.8f;
                    break;
                case GuardAction.Chase:
                    var chase = _visible ? _player : _lastSeenPos ?? _player;
                    gi = chase.x;
                    gj = chase.y;
                    speed = prof.chase_speed;
                    _hp = Math.Min(100f, _hp + dt * 4f);
                    break;
                case GuardAction.Attack:
                    _guardAngle = MathF.Atan2(_player.y - _guard.y, _player.x - _guard.x);
                    _hp = Math.Max(0f, _hp - dt * 58f);
                    if (_hp <= 0f)
                    {
                        _outcome = HeistOutcome.Busted;
                        return true;
                    }

                    return false;
                case GuardAction.Search:
                    _guardAngle += dt * 2.5f;
                    return false;
                case GuardAction.Return:
                    var start = _guardRoute[0];
                    gi = start.x;
                    gj = start.y;
                    speed = prof.patrol_speed;
                    _atRouteStart = Distance(_guard, start) < 0.85f;
                    break;
                default:
                    return false;
            }

            var fdi = gi - _guard.x;
            var fdj = gj - _guard.y;
            var fd = MathF.Sqrt(fdi * fdi + fdj * fdj);
            if (!_frozenPatrol && fd > 0.08f && MoveToward(_guardNav, ref _guard, gi, gj, TileSpeed * speed * dt))
            {
                _guardAngle = MathF.Atan2(fdj, fdi);
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
                    * (input.Sprint ? SprintMultiplier : 1f)
                    * (input.Crouch ? CrouchMultiplier : 1f)
                    * dt;
                MoveEntity(ref _player, di / length, dj / length, step);
                if (input.Sprint && _tick % 28 == 0)
                {
                    EmitSound(_player.x, _player.y, 4.5f);
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
                    EmitSound((float)MarketData.Panel.I, (float)MarketData.Panel.J, 2.4f);
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

            _targetI = near.Position.x;
            _targetJ = near.Position.y;
            _pendingStealId = near.Id;
            return InteractOutcome.None;
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

        private bool MoveToward(NavState nav, ref Vector2 position, float goalI, float goalJ, float stepLength)
        {
            if (FollowPath(nav, ref position, goalI, goalJ, stepLength))
            {
                return true;
            }

            return MoveThroughDoorway(nav, ref position, goalI, goalJ, stepLength);
        }

        private bool FollowPath(NavState nav, ref Vector2 position, float goalI, float goalJ, float stepLength)
        {
            var goalX = FloorDiv(goalI, 1f);
            var goalY = FloorDiv(goalJ, 1f);
            if (!nav.HasGoal || nav.GoalX != goalX || nav.GoalY != goalY)
            {
                nav.Path = FindPathFallback(FloorDiv(position.x, 1f), FloorDiv(position.y, 1f), goalX, goalY);
                nav.Index = 0;
                nav.GoalX = goalX;
                nav.GoalY = goalY;
                nav.HasGoal = true;
            }

            if (nav.Path == null || nav.Path.Count == 0 || nav.Index >= nav.Path.Count)
            {
                nav.HasGoal = false;
                nav.Path = null;
                return false;
            }

            var tile = nav.Path[nav.Index];
            var wx = tile.X + 0.5f;
            var wy = tile.Y + 0.5f;
            var di = wx - position.x;
            var dj = wy - position.y;
            var d = MathF.Sqrt(di * di + dj * dj);
            if (d < 0.2f)
            {
                nav.Index += 1;
                return nav.Index < nav.Path.Count;
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
            var start = new GridPos(fromX, fromY);
            if (start.X == goalX && start.Y == goalY)
            {
                return new List<GridPos> { start };
            }

            var queue = new Queue<GridPos>();
            var previous = new Dictionary<GridPos, GridPos>();
            var visited = new HashSet<GridPos> { start };
            queue.Enqueue(start);
            GridPos best = start;
            var bestDistance = float.MaxValue;
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in Neighbours(at))
                {
                    if (!visited.Add(next))
                    {
                        continue;
                    }

                    previous[next] = at;
                    queue.Enqueue(next);
                    var ddx = next.X + 0.5f - goalX - 0.5f;
                    var ddy = next.Y + 0.5f - goalY - 0.5f;
                    var distance = ddx * ddx + ddy * ddy;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = next;
                    }
                }
            }

            var goal = new GridPos(goalX, goalY);
            if (!visited.Contains(goal))
            {
                goal = best;
                if (goal.Equals(start) && previous.Count == 0)
                {
                    return null;
                }
            }

            var path = new List<GridPos>();
            var current = goal;
            while (!current.Equals(start))
            {
                path.Add(current);
                if (!previous.TryGetValue(current, out current))
                {
                    return null;
                }
            }

            path.Add(start);
            path.Reverse();
            return path;
        }

        private IEnumerable<GridPos> Neighbours(GridPos at)
        {
            if (CanStep(at.X, at.Y, at.X + 1, at.Y))
            {
                yield return new GridPos(at.X + 1, at.Y);
            }

            if (CanStep(at.X, at.Y, at.X - 1, at.Y))
            {
                yield return new GridPos(at.X - 1, at.Y);
            }

            if (CanStep(at.X, at.Y, at.X, at.Y + 1))
            {
                yield return new GridPos(at.X, at.Y + 1);
            }

            if (CanStep(at.X, at.Y, at.X, at.Y - 1))
            {
                yield return new GridPos(at.X, at.Y - 1);
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
                points[i] = new Vector2(route[i].X + 0.5f, route[i].Y + 0.5f);
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
        }
    }
}
