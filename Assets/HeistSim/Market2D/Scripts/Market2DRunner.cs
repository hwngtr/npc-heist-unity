using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Live runtime controller for the 2D Riso Press Frischemarkt prototype.
    /// Steps the HeistGame simulation, depth-sorts 2D sprites by (i + j), drives
    /// animated character walk cycles, binds loot highlight & empty-state swaps directly
    /// onto real fixture GameObjects (zero detached stars), provides butter-smooth
    /// shimmer-free cursor-anchored zoom/pan, and wires pooled VFX + synthesized audio.
    /// </summary>
    public sealed class Market2DRunner : MonoBehaviour
    {
        /// <summary>Right-hand collapsible dossier drawer width in reference pixels at 1600px wide.</summary>
        public const float RightPanelWidthPx = 380f;

        /// <summary>Cooldown in unscaled seconds between alert sound/flash pulses so continuous detection never stutters.</summary>
        public const float AlertFeedbackCooldownSeconds = 2.4f;

        /// <summary>Cooldown in unscaled seconds between subtle camera alert shakes so tracking stays smooth.</summary>
        public const float AlertShakeCooldownSeconds = 3.0f;

        /// <summary>Base sprite world pixels per Unity unit (64 world px = 1 unit; 2x sprites = 128 PPU).</summary>
        public const float BaseWorldPpu = 64f;

        /// <summary>Minimum close-up orthographic size.</summary>
        public const float MinOrthoSize = 2.6f;

        /// <summary>Default gameplay orthographic half-height (shows full room + doorway sightlines cleanly).</summary>
        public const float DefaultOrthoSize = 4.2f;

        /// <summary>Fit-whole-market orthographic size.</summary>
        public const float FitOrthoSize = 10.2f;

        /// <summary>Live play base thief speed scale so sprinting feels risky and guards remain threatening.</summary>
        public const float LivePlayerSpeedScale = 0.84f;

        /// <summary>Live play guard speed scale giving pursuing security decisive closing speed.</summary>
        public const float LiveGuardSpeedScale = 1.22f;

        /// <summary>Maximum simulation delta clamp matching the browser game.</summary>
        public const float MaxDeltaTime = 0.05f;

        /// <summary>Player 2D sprite renderer.</summary>
        public SpriteRenderer playerSprite;

        /// <summary>Standing player sprite.</summary>
        public Sprite playerStandSprite;

        /// <summary>Standing player walk frame 1.</summary>
        public Sprite playerWalk1Sprite;

        /// <summary>Standing player walk frame 2.</summary>
        public Sprite playerWalk2Sprite;

        /// <summary>Crouched player sprite.</summary>
        public Sprite playerCrouchSprite;

        /// <summary>Crouched player walk frame 1.</summary>
        public Sprite playerCrouchWalk1Sprite;

        /// <summary>Crouched player walk frame 2.</summary>
        public Sprite playerCrouchWalk2Sprite;

        /// <summary>Guard 2D sprite renderer.</summary>
        public SpriteRenderer guardSprite;

        /// <summary>Standing guard sprite.</summary>
        public Sprite guardStandSprite;

        /// <summary>Guard walk frame 1.</summary>
        public Sprite guardWalk1Sprite;

        /// <summary>Guard walk frame 2.</summary>
        public Sprite guardWalk2Sprite;

        /// <summary>Guard alert/investigate speech bubble renderer.</summary>
        public SpriteRenderer guardBubbleSprite;

        /// <summary>Alert '!' riso bubble sprite.</summary>
        public Sprite alertBubbleSprite;

        /// <summary>Investigate '?' riso bubble sprite.</summary>
        public Sprite questionBubbleSprite;

        /// <summary>Staff 2D sprite renderer.</summary>
        public SpriteRenderer staffSprite;

        /// <summary>Standing staff sprite.</summary>
        public Sprite staffStandSprite;

        /// <summary>Staff walk frame 1.</summary>
        public Sprite staffWalk1Sprite;

        /// <summary>Staff walk frame 2.</summary>
        public Sprite staffWalk2Sprite;

        /// <summary>Guard floor-projected 2D vision cone.</summary>
        public Iso2DVisionCone guardVisionCone;

        /// <summary>Second security guard 2D sprite renderer.</summary>
        public SpriteRenderer guard2Sprite;

        /// <summary>Standing sprite for distinct Guard 2 officer.</summary>
        public Sprite guard2StandSprite;

        /// <summary>Second security guard alert/investigate speech bubble renderer.</summary>
        public SpriteRenderer guard2BubbleSprite;

        /// <summary>Second security guard floor-projected 2D vision cone.</summary>
        public Iso2DVisionCone guard2VisionCone;

        /// <summary>Smooth 6-frame standing walk sprites for Player.</summary>
        public Sprite[] playerWalkFrames = Array.Empty<Sprite>();

        /// <summary>Smooth 6-frame crouch walk sprites for Player.</summary>
        public Sprite[] playerCrouchWalkFrames = Array.Empty<Sprite>();

        /// <summary>Smooth 6-frame walk sprites for Guard 1.</summary>
        public Sprite[] guardWalkFrames = Array.Empty<Sprite>();

        /// <summary>Smooth 6-frame walk sprites for Guard 2.</summary>
        public Sprite[] guard2WalkFrames = Array.Empty<Sprite>();

        /// <summary>Smooth 6-frame walk sprites for Staff.</summary>
        public Sprite[] staffWalkFrames = Array.Empty<Sprite>();

        /// <summary>CCTV floor-projected 2D vision cones in MarketData.Cameras order.</summary>
        public Iso2DVisionCone[] cctvCones = Array.Empty<Iso2DVisionCone>();

        /// <summary>Unified single-model CCTV camera rigs (mount + rotating head + tether + floor cone) in MarketData.Cameras order.</summary>
        public Market2DCctvRig[] cctvRigs = Array.Empty<Market2DCctvRig>();

        /// <summary>Fake-3D sprite-stacked rotating CCTV turrets (T20) in MarketData.Cameras order.</summary>
        public SpriteStack2D[] cctvStacks = Array.Empty<SpriteStack2D>();

        /// <summary>Highlighted CCTV power panel sprite at MarketData.Panel so the player can spot and cut camera power.</summary>
        public SpriteRenderer cctvPanelSprite;

        /// <summary>Highlighted RF security tag detagger pad at MarketData.Detagger.</summary>
        public SpriteRenderer detaggerPadSprite;

        /// <summary>Transient acoustic decoy coin sprite rendered when tossed with Q.</summary>
        public SpriteRenderer decoyCoinSprite;

        /// <summary>Real loot-bearing fixture components (highlight + empty-state swap on the fixture itself).</summary>
        public LootableFixture2D[] lootFixtures = Array.Empty<LootableFixture2D>();

        /// <summary>Main orthographic 2D camera.</summary>
        public Camera mainCamera;

        /// <summary>UI Toolkit split-screen HUD controller.</summary>
        public Market2DHudController hud;

        /// <summary>Synthesized procedural audio floor.</summary>
        public Market2DAudio audioFloor;

        /// <summary>Pooled 2D VFX, channel progress arc, and micro-shake feel layer.</summary>
        public Market2DFeel feel;

        /// <summary>Bayer 4x4 dithered see-through controller for tall occluding shelves (T10).</summary>
        public Market2DSeeThrough seeThrough;

        /// <summary>Unified fixed-tick hit-stop, flash, knockback, spark, and whole-pixel shake controller (T17).</summary>
        public Market2DHitFeel hitFeel;

        /// <summary>World-anchored 2D trail canvas and fading isometric floor footprints (T01).</summary>
        public Market2DTrailSurface trailSurface;

        /// <summary>Camera post-processing controller for Heat Color LUT (T09), 8 Screen Transitions (T19), and CRT toggle (T22).</summary>
        public Market2DPostFx postFx;

        private static readonly string[] ProfileOrder = { "standard", "aggressive", "cautious", "observant" };

        private readonly Market2DCameraRig _cameraRig = new();
        private readonly Market2DStatusFrames _playerStatus = new();
        private readonly Market2DStatusFrames _guardStatus = new();
        private readonly Market2DStamina _stamina = new();
        private NavGrid _navGrid;
        private float _simAccum;
        private const float FixedSimStep = 1f / 30f;
        private Vector2 _prevSimPlayer;
        private Vector2 _currSimPlayer;
        private Vector2 _prevSimGuard;
        private Vector2 _currSimGuard;
        private Vector2 _prevSimGuard2;
        private Vector2 _currSimGuard2;
        private Vector2 _prevSimStaff;
        private Vector2 _currSimStaff;
        private float _prevSimGuardAngle = 1.5f;
        private float _currSimGuardAngle = 1.5f;
        private float _prevSimGuard2Angle = 3.14f;
        private float _currSimGuard2Angle = 3.14f;
        private float _guardFlipCooldown;
        private float _guard2FlipCooldown;
        private Vector2 _smoothedMove;
        private int _prevPlayerStrideStep = -1;
        private int _prevGuardStrideStep = -1;
        private int _prevGuard2StrideStep = -1;

        private readonly HashSet<string> _seenBagIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _bagLookup = new(StringComparer.Ordinal);
        private Dictionary<string, (LootEntry Entry, Vector2 FxTile)> _lootMeta;
        private MaterialPropertyBlock _panelMpb;
        private MaterialPropertyBlock _detaggerMpb;
        private float _hudAccum;
        private uint _shiftCounter;

        private HeistGame _game;
        private GuardProfile _profile;
        private GuardArchitecture _arch = GuardArchitecture.StateTree;
        private float _clock;
        private float _guardAngle = 1.5f;
        private float _guard2Angle = 3.14f;
        private Vector2 _lastPlayer;
        private Vector2 _lastGuard = HeistGame.GuardStart;
        private Vector2 _lastGuard2 = HeistGame.Guard2Start;
        private Vector2 _lastStaff;
        private float _playerStrideClock;
        private float _guardStrideClock;
        private float _guard2StrideClock;
        private float _staffStrideClock;
        private float _footstepCooldown;
        private float _decoyCooldown;
        private Vector2 _decoyTile;
        private float _decoyVisualTimer;
        private float _bubblePopTimer;
        private bool _bubbleWasShown;
        private float _bubble2PopTimer;
        private bool _bubble2WasShown;
        private int _profileIndex;
        private Vector2 _lastMoveDir = new(1f, 0f);

        private float _targetOrthoSize = DefaultOrthoSize;
        private bool _fitMode;
        private bool _hasManualPan;
        private Vector3 _manualPanTarget;
        private bool _draggingPan;
        private Vector2 _lastDragScreen;

        private bool _lastCrouching;
        private bool _lastSprinting;
        private bool _lastBackup;
        private bool _lastExitBlocked;
        private bool _lastVitrineOpen;
        private bool _lastCamerasDisabled;
        private bool _lastCctvRestoring;
        private int _lastCctvRestoreCount;
        private int _lastActiveTags;
        private int _lastCameraSightings;
        private string _lastGuardState = "patrol";
        private string _lastGuard2State = "patrol";
        private GuardTacticalRole _lastGuard1Role = GuardTacticalRole.Patrol;
        private GuardTacticalRole _lastGuard2Role = GuardTacticalRole.Patrol;
        private HeistOutcome _lastOutcome = HeistOutcome.None;
        private int _lastProcessedEventTick = -1;
        private float _lastAlertFeedbackTime = -99f;
        private float _lastAlertShakeTime = -99f;
        private float _lastAlertLogTime = -99f;
        private float _lastGuard1CalloutTime = -99f;
        private float _lastGuard2CalloutTime = -99f;
        private string _lastGuard1Plan;
        private string _lastGuard2Plan;
        private int _frame;
        private readonly Market2DSeeThrough.SeeThroughActor[] _seeThroughActors = new Market2DSeeThrough.SeeThroughActor[4];

        /// <summary>Active HeistGame simulation instance.</summary>
        public HeistGame Game => _game;

        /// <summary>Active guard personality profile label (Standard, Aggressive, Cautious, Observant).</summary>
        public string ActiveProfileLabel => _profile != null ? _profile.label : "Standard";

        /// <summary>Current zoom percentage relative to DefaultOrthoSize (100 = default gameplay view).</summary>
        public int ZoomPercent => Mathf.RoundToInt((DefaultOrthoSize / Mathf.Max(0.5f, _targetOrthoSize)) * 100f);

        /// <summary>True when whole-store overview fit mode is active.</summary>
        public bool IsFitMode => _fitMode;

        private void Awake()
        {
            _profile = GuardProfile.CreateDefault("standard");
            _game = new HeistGame(_profile, _arch, guardCount: 2);
            _game.GuardSpeedScale = LiveGuardSpeedScale;
            if (Application.isPlaying)
            {
                _game.ReseedSquad(unchecked((uint)Environment.TickCount ^ (++_shiftCounter * 2654435761u)));
            }
            _navGrid = MarketGrid.Build();
            var snap = _game.Snapshot();
            _lastPlayer = snap.Player;
            _lastGuard = snap.Guard;
            _lastGuard2 = snap.Guard2;
            _lastStaff = snap.Staff;
            _prevSimPlayer = _currSimPlayer = snap.Player;
            _prevSimGuard = _currSimGuard = snap.Guard;
            _prevSimGuard2 = _currSimGuard2 = snap.Guard2;
            _prevSimStaff = _currSimStaff = snap.Staff;
            _prevSimGuardAngle = _currSimGuardAngle = _game.GuardAngle;
            _prevSimGuard2Angle = _currSimGuard2Angle = _game.Guard2Angle;

            if (mainCamera == null)
            {
                mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<Market2DHudController>();
            }

            if (audioFloor == null)
            {
                audioFloor = GetComponent<Market2DAudio>() ?? gameObject.AddComponent<Market2DAudio>();
            }

            if (feel == null)
            {
                feel = GetComponent<Market2DFeel>() ?? gameObject.AddComponent<Market2DFeel>();
            }

            if (seeThrough == null)
            {
                seeThrough = GetComponent<Market2DSeeThrough>() ?? gameObject.AddComponent<Market2DSeeThrough>();
            }

            if (hitFeel == null)
            {
                hitFeel = GetComponent<Market2DHitFeel>() ?? gameObject.AddComponent<Market2DHitFeel>();
            }

            if (trailSurface == null)
            {
                trailSurface = GetComponent<Market2DTrailSurface>() ?? gameObject.AddComponent<Market2DTrailSurface>();
            }

            if (postFx == null && mainCamera != null)
            {
                postFx = mainCamera.GetComponent<Market2DPostFx>() ?? mainCamera.gameObject.AddComponent<Market2DPostFx>();
            }

            audioFloor.EnsureInitialized();
            feel.EnsureInitialized();
            hitFeel.EnsureInitialized(feel);
            trailSurface.EnsureInitialized();
            seeThrough.BindOccluders(transform.Find("Fixtures"), transform.Find("LowWalls"));
            postFx?.EnsureResources();

            if (lootFixtures == null || lootFixtures.Length == 0)
            {
                lootFixtures = GetComponentsInChildren<LootableFixture2D>(true);
            }

            if (hud != null)
            {
                hud.BindRunner(this);
            }

            ApplySplitViewport();
            _targetOrthoSize = DefaultOrthoSize;
            var startRoom = ResolveRoomAt(snap.Player.x, snap.Player.y);
            var startWorld = Iso2DMath.TileToWorld(snap.Player, 0.6f);
            _cameraRig.Snap(
                new Vector2(startWorld.x, startWorld.y),
                _targetOrthoSize,
                startRoom?.Id,
                startRoom.HasValue ? Iso2DMath.TileToWorld(startRoom.Value.OriginI + startRoom.Value.Width * 0.5f, startRoom.Value.OriginJ + startRoom.Value.Depth * 0.5f, 0.5f) : (Vector2?)null);
            SyncScene(snap, snapCamera: true);
            RefreshHud(snap);
        }

        private void Update()
        {
            _game ??= new HeistGame(_profile ??= GuardProfile.CreateDefault("standard"), _arch, guardCount: 2);
            ApplySplitViewport();

            hitFeel?.Advance(Time.unscaledDeltaTime);
            _playerStatus.Advance(Time.unscaledDeltaTime);
            _guardStatus.Advance(Time.unscaledDeltaTime);

            // Pause gameplay input & simulation while a screen transition covers/uncovers (T19)
            if (postFx != null && postFx.IsTransitioning)
            {
                return;
            }

            ReadInput(out var input, out var reset, out var toggleArch, out var cycleTrait, out var tossDecoy);
            if (reset)
            {
                ResetShift();
                return;
            }

            if (toggleArch)
            {
                ToggleGuardArchitecture();
            }

            if (cycleTrait)
            {
                CycleGuardPersonality();
            }

            if (_decoyCooldown > 0f)
            {
                _decoyCooldown = Mathf.Max(0f, _decoyCooldown - Time.deltaTime);
            }

            if (tossDecoy)
            {
                TossDecoyCoin();
            }

            // Moving the thief smoothly hands camera focus back from manual scout pan
            if (input.HasDirection)
            {
                _lastMoveDir = new Vector2(input.MoveI, input.MoveJ).normalized;
                _hasManualPan = false;
                if (_fitMode)
                {
                    _fitMode = false;
                    _targetOrthoSize = DefaultOrthoSize;
                }
            }

            var rawDt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            var timeScale = hitFeel != null ? hitFeel.SimTimeScale : 1f;
            var dt = rawDt * timeScale;

            // Smooth directional acceleration/deceleration so movement feels liquid and responsive
            var rawMove = new Vector2(input.MoveI, input.MoveJ);
            var rampRate = input.HasDirection ? 24f : 20f;
            _smoothedMove = Vector2.Lerp(_smoothedMove, rawMove, 1f - Mathf.Exp(-rampRate * rawDt));
            if (!input.HasDirection && _smoothedMove.sqrMagnitude < 0.0025f)
            {
                _smoothedMove = Vector2.zero;
            }

            var hasSmoothedMove = _smoothedMove.sqrMagnitude > 0.0025f;

            // Stamina fatigue & haul-weight encumbrance
            var currentBagCount = _game.BagCount;
            var (allowSprint, speedScale, justWinded) = _stamina.Step(
                input.Sprint,
                input.HasDirection,
                input.Crouch,
                currentBagCount,
                rawDt);
            var rampMag = hasSmoothedMove ? Mathf.Clamp01(_smoothedMove.magnitude) : 1f;
            _game.PlayerSpeedScale = LivePlayerSpeedScale * speedScale * (input.HasDirection ? Mathf.Lerp(0.45f, 1f, rampMag) : rampMag);
            var effectiveInput = new HeistInput(
                hasSmoothedMove ? _smoothedMove.x : 0f,
                hasSmoothedMove ? _smoothedMove.y : 0f,
                input.Action,
                input.Crouch,
                allowSprint);

            if (justWinded)
            {
                var pw = Iso2DMath.TileToWorld(_game.PlayerPosition);
                feel?.EmitFootstepPuff(pw);
                _playerStatus.Trigger(playerSprite, StatusFrameLook.SquashPulse);
                audioFloor?.PlayRefusal();
                hud?.ShowRefusal("WINDED — catch your breath");
            }

            // T17: During hit stop, freeze actor simulation while flash, spark, and shake keep running.
            // Step simulation at fixed 30 Hz and sub-tick interpolate visuals every render frame for butter-smooth motion.
            var simStepped = false;
            if (hitFeel == null || !hitFeel.IsHitStopped)
            {
                _simAccum = Mathf.Min(_simAccum + dt, FixedSimStep * 3f);
                while (_simAccum >= FixedSimStep)
                {
                    simStepped = true;
                    _simAccum -= FixedSimStep;
                    _prevSimPlayer = _currSimPlayer;
                    _prevSimGuard = _currSimGuard;
                    _prevSimGuard2 = _currSimGuard2;
                    _prevSimStaff = _currSimStaff;
                    _prevSimGuardAngle = _currSimGuardAngle;
                    _prevSimGuard2Angle = _currSimGuard2Angle;

                    if (_game.Outcome == HeistOutcome.None)
                    {
                        _clock += FixedSimStep;
                    }

                    _game.Step(FixedSimStep, effectiveInput);
                    _currSimPlayer = _game.PlayerPosition;
                    _currSimGuard = _game.GuardPosition;
                    _currSimGuard2 = _game.Guard2Position;
                    _currSimStaff = _game.StaffPosition;
                    _currSimGuardAngle = _game.GuardAngle;
                    _currSimGuard2Angle = _game.Guard2Angle;
                }
            }

            _lastCrouching = effectiveInput.Crouch;
            _lastSprinting = effectiveInput.Sprint;
            var snapshot = _game.Snapshot();
            var interpAlpha = Mathf.Clamp01(_simAccum / FixedSimStep);
            SyncScene(snapshot, snapCamera: false, dt: rawDt, interpAlpha: interpAlpha, updateCones: simStepped);

            var isNewSimTick = snapshot.Tick != _lastProcessedEventTick;
            var discreteChange = toggleArch
                || cycleTrait
                || snapshot.Outcome != _lastOutcome
                || (isNewSimTick && snapshot.FirstDetectionEdge)
                || (isNewSimTick && snapshot.LastInteraction != InteractOutcome.None)
                || snapshot.ExitBlockedByTags != _lastExitBlocked
                || snapshot.VitrineOpen != _lastVitrineOpen
                || snapshot.CamerasDisabled != _lastCamerasDisabled
                || snapshot.ActiveTags != _lastActiveTags
                || snapshot.BagCount != _seenBagIds.Count
                || (snapshot.CameraSightings != 0) != (_lastCameraSightings != 0)
                || (snapshot.BackupPhase != BackupPhase.None) != _lastBackup
                || snapshot.GuardState != _lastGuardState
                || snapshot.Guard2State != _lastGuard2State
                || snapshot.Guard1Role != _lastGuard1Role
                || snapshot.Guard2Role != _lastGuard2Role;
            _hudAccum += rawDt;
            if (discreteChange || _hudAccum >= 0.10f)
            {
                _hudAccum = 0f;
                RefreshHud(snapshot);
            }

            _frame++;
        }

        /// <summary>Starts a fresh Frischemarkt shift (plays Bayer 8x8 dither transition in Play Mode).</summary>
        public void ResetShift()
        {
            audioFloor?.PlayUiClick();
            if (Application.isPlaying && postFx != null && !postFx.IsTransitioning)
            {
                Vector2? centerUv = null;
                if (mainCamera != null && playerSprite != null)
                {
                    var vp = mainCamera.WorldToViewportPoint(playerSprite.transform.position);
                    centerUv = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
                }

                postFx.BeginTransition(ExecuteResetShift, centerUv);
                return;
            }

            ExecuteResetShift();
        }

        private void ExecuteResetShift()
        {
            _game = new HeistGame(_profile ??= GuardProfile.CreateDefault("standard"), _arch, guardCount: 2);
            _game.GuardSpeedScale = LiveGuardSpeedScale;
            if (Application.isPlaying)
            {
                _game.ReseedSquad(unchecked((uint)Environment.TickCount ^ (++_shiftCounter * 2654435761u)));
            }

            _hudAccum = 0f;
            _clock = 0f;
            _guardAngle = 1.5f;
            _guard2Angle = 3.14f;
            var snapshot = _game.Snapshot();
            _lastPlayer = snapshot.Player;
            _lastGuard = snapshot.Guard;
            _lastGuard2 = snapshot.Guard2;
            _lastStaff = snapshot.Staff;
            _prevSimPlayer = _currSimPlayer = snapshot.Player;
            _prevSimGuard = _currSimGuard = snapshot.Guard;
            _prevSimGuard2 = _currSimGuard2 = snapshot.Guard2;
            _prevSimStaff = _currSimStaff = snapshot.Staff;
            _prevSimGuardAngle = _currSimGuardAngle = _game.GuardAngle;
            _prevSimGuard2Angle = _currSimGuard2Angle = _game.Guard2Angle;
            _guardFlipCooldown = 0f;
            _guard2FlipCooldown = 0f;
            _smoothedMove = Vector2.zero;
            _prevPlayerStrideStep = -1;
            _prevGuardStrideStep = -1;
            _prevGuard2StrideStep = -1;
            _seenBagIds.Clear();
            _lastBackup = false;
            _lastExitBlocked = false;
            _lastVitrineOpen = false;
            _lastCamerasDisabled = false;
            _lastCctvRestoring = false;
            _lastCctvRestoreCount = 0;
            _lastActiveTags = 0;
            _lastCameraSightings = 0;
            _lastGuardState = "patrol";
            _lastGuard2State = "patrol";
            _lastGuard1Role = GuardTacticalRole.Patrol;
            _lastGuard2Role = GuardTacticalRole.Patrol;
            _lastOutcome = HeistOutcome.None;
            _lastProcessedEventTick = -1;
            _lastAlertFeedbackTime = -99f;
            _lastAlertShakeTime = -99f;
            _lastAlertLogTime = -99f;
            _lastGuard1CalloutTime = -99f;
            _lastGuard2CalloutTime = -99f;
            _lastGuard1Plan = null;
            _lastGuard2Plan = null;
            audioFloor?.ResetCooldowns();
            _fitMode = false;
            _hasManualPan = false;
            _targetOrthoSize = DefaultOrthoSize;

            hitFeel?.ResetAll();
            trailSurface?.ResetTrail();
            _stamina.Reset();
            _simAccum = 0f;
            _decoyVisualTimer = 0f;
            if (decoyCoinSprite != null)
            {
                decoyCoinSprite.enabled = false;
            }

            _playerStatus.Reset();
            _guardStatus.Reset();

            var startWorld = Iso2DMath.TileToWorld(snapshot.Player, 0.6f);
            var startRoom = ResolveRoomAt(snapshot.Player.x, snapshot.Player.y);
            _cameraRig.Snap(
                new Vector2(startWorld.x, startWorld.y),
                _targetOrthoSize,
                startRoom?.Id,
                startRoom.HasValue ? Iso2DMath.TileToWorld(startRoom.Value.OriginI + startRoom.Value.Width * 0.5f, startRoom.Value.OriginJ + startRoom.Value.Depth * 0.5f, 0.5f) : (Vector2?)null);

            if (lootFixtures != null)
            {
                foreach (var lf in lootFixtures)
                {
                    lf?.ResetForShift();
                }
            }

            hud?.ResetFeed();
            SyncScene(snapshot, snapCamera: true);
            RefreshHud(snapshot);
        }

        /// <summary>Cycles the two security guards live across StateTree + HTN, 7-State FSM, and Reactive BT.</summary>
        public void ToggleGuardArchitecture()
        {
            if (_game == null)
            {
                return;
            }

            audioFloor?.PlayUiClick();
            _game.CycleArch();
            _arch = _game.Arch;
            var label = _arch switch
            {
                GuardArchitecture.StateTree => "StateTree + HTN (2 Guards)",
                GuardArchitecture.Bt => "Reactive BT (2 Guards)",
                _ => "7-State FSM (2 Guards)",
            };
            hud?.PushLog("AI", "Switched security to " + label);
            RefreshHud(_game.Snapshot());
        }

        /// <summary>Cycles the guard personality live across Standard, Aggressive, Cautious, and Observant.</summary>
        public void CycleGuardPersonality()
        {
            if (_game == null)
            {
                return;
            }

            audioFloor?.PlayUiClick();
            _profileIndex = (_profileIndex + 1) % ProfileOrder.Length;
            _profile = GuardProfile.CreateDefault(ProfileOrder[_profileIndex]);
            _game.SetProfile(_profile);
            var snap = _game.Snapshot();
            if (guardSprite != null)
            {
                hud?.SpawnFloatingPopup(guardSprite.transform.position, "TRAIT: " + _profile.label.ToUpperInvariant(), rfAccent: false);
            }

            hud?.PushLog("AI", "Guard personality -> " + _profile.label + " (FOV " + _profile.fov_distance.ToString("0.0") + "t, Hear " + _profile.hearing_scale.ToString("0.00") + "x)");
            SyncScene(snap, snapCamera: false);
            RefreshHud(snap);
        }

        /// <summary>Tosses an acoustic decoy coin ahead of the thief to lure the Guard into Investigate.</summary>
        public void TossDecoyCoin()
        {
            if (_game == null || _game.Outcome != HeistOutcome.None)
            {
                return;
            }

            if (_decoyCooldown > 0.01f)
            {
                hud?.ShowRefusal("DECOY COOLDOWN (" + _decoyCooldown.ToString("0.0") + "s)");
                audioFloor?.PlayRefusal();
                return;
            }

            _decoyCooldown = 2.0f;
            var snap = _game.Snapshot();
            var dir = _lastMoveDir.sqrMagnitude > 0.01f ? _lastMoveDir.normalized : new Vector2(1f, 0f);
            var desired = snap.Player + dir * 3.4f;
            var landed = _game.TossDecoy(desired, maxRadius: 7.5f);
            _decoyTile = landed;
            _decoyVisualTimer = 2.6f;
            var world = Iso2DMath.TileToWorld(landed, 0.25f);
            audioFloor?.PlayUnlock();
            feel?.EmitPickupBurst(world, rfTagged: false);
            hud?.SpawnFloatingPopup(world, "DECOY COIN [Q]", rfAccent: false);
            hud?.PushLog("LURE", "Tossed decoy coin at (" + landed.x.ToString("0.0") + ", " + landed.y.ToString("0.0") + ")");
        }

        /// <summary>Smoothly zooms the 2D isometric camera closer (zero pixel shimmer).</summary>
        public void ZoomIn()
        {
            audioFloor?.PlayUiClick();
            _fitMode = false;
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize * 0.80f, MinOrthoSize, FitOrthoSize);
            hud?.UpdateZoomReadout(ZoomPercent, _fitMode);
        }

        /// <summary>Smoothly zooms the 2D isometric camera further out.</summary>
        public void ZoomOut()
        {
            audioFloor?.PlayUiClick();
            _fitMode = false;
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize * 1.25f, MinOrthoSize, FitOrthoSize);
            hud?.UpdateZoomReadout(ZoomPercent, _fitMode);
        }

        /// <summary>Toggles fit-whole-supermarket overview zoom.</summary>
        public void ToggleFitOverview()
        {
            audioFloor?.PlayUiClick();
            _fitMode = !_fitMode;
            _hasManualPan = false;
            _targetOrthoSize = _fitMode ? FitOrthoSize : DefaultOrthoSize;
            hud?.UpdateZoomReadout(ZoomPercent, _fitMode);
        }

        /// <summary>Pans the camera smoothly to inspect a specific department clicked on the minimap.</summary>
        public void FocusRoom(string roomId)
        {
            try
            {
                var room = MarketData.Room(roomId);
                audioFloor?.PlayUiClick();
                _fitMode = false;
                _hasManualPan = true;
                var ci = room.OriginI + room.Width * 0.5f;
                var cj = room.OriginJ + room.Depth * 0.5f;
                _manualPanTarget = Iso2DMath.TileToWorld(ci, cj, 0.5f);
                _manualPanTarget.x += SplitCenterOffsetX();
                _manualPanTarget.z = -10f;
                _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, 3.6f, 5.2f);
                hud?.UpdateZoomReadout(ZoomPercent, _fitMode);
            }
            catch (ArgumentException)
            {
                // Ignore unknown room ids.
            }
        }

        /// <summary>Toggles synthesized audio mute.</summary>
        public bool ToggleMute()
        {
            if (audioFloor == null)
            {
                return false;
            }

            audioFloor.Muted = !audioFloor.Muted;
            if (!audioFloor.Muted)
            {
                audioFloor.PlayUiClick();
            }

            return audioFloor.Muted;
        }

        /// <summary>Computes a clean orthographic size for a given zoom multiplier (kept for tests/compatibility).</summary>
        public static float ComputePixelPerfectOrthoSize(float zoomMultiplier, int viewportHeight = 0)
        {
            var h = viewportHeight > 0 ? viewportHeight : Mathf.Max(360, Screen.height);
            return h / (2f * BaseWorldPpu * Mathf.Max(0.25f, zoomMultiplier));
        }

        /// <summary>Fraction of the reference 1600px width occupied by the right editorial panel.</summary>
        public const float RightPanelFrac = RightPanelWidthPx / 1600f;

        private void ApplySplitViewport()
        {
            if (mainCamera == null)
            {
                return;
            }

            // Render across the full framebuffer so there is NEVER a black seam beside the
            // resolution-scaled right editorial panel; camera target offsets right so the
            // subject sits dead-center inside the visible left viewport.
            mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }

        private float SplitCenterOffsetX()
        {
            return 0f;
        }

        private void SyncScene(in HeistSnapshot snapshot, bool snapCamera, float dt = 0.016f, float interpAlpha = 1f, bool updateCones = true)
        {
            // Sub-tick interpolated actor positions eliminate 30 Hz step judder at 60-240 FPS
            var renderPlayer = snapCamera ? snapshot.Player : Vector2.Lerp(_prevSimPlayer, _currSimPlayer, interpAlpha);
            var renderGuard = snapCamera ? snapshot.Guard : Vector2.Lerp(_prevSimGuard, _currSimGuard, interpAlpha);
            var renderGuard2 = snapCamera ? snapshot.Guard2 : Vector2.Lerp(_prevSimGuard2, _currSimGuard2, interpAlpha);
            var renderStaff = snapCamera ? snapshot.Staff : Vector2.Lerp(_prevSimStaff, _currSimStaff, interpAlpha);

            // Player animation, facing flip, idle breathing, sprint footstep puffs, T01 floor trail footprints
            var playerDelta = renderPlayer - _lastPlayer;
            var playerSpeed = dt > 1e-5f ? playerDelta.magnitude / dt : 0f;
            var playerMoving = playerSpeed > 0.12f;
            var playerBootContact = false;
            var playerOrder = Iso2DMath.DepthOrder(renderPlayer.x, renderPlayer.y, 1);
            var playerWorldFeet = Iso2DMath.TileToWorld(renderPlayer);
            if (playerSprite != null)
            {
                var pWorld = playerWorldFeet;
                var breathSin = playerMoving ? 0f : Mathf.Sin(_clock * 3.1f);
                var breathX = 1f - breathSin * 0.011f;
                var breathY = 1f + breathSin * 0.022f;
                var jitterX = _playerStatus.JitterOffsetPx / BaseWorldPpu;
                var knock = hitFeel != null ? hitFeel.KnockbackOffsetWorld : Vector2.zero;
                playerSprite.transform.position = new Vector3(pWorld.x + jitterX + knock.x, pWorld.y + knock.y, 0f);
                playerSprite.transform.localScale = new Vector3(_playerStatus.ScaleFactor.x * breathX, _playerStatus.ScaleFactor.y * breathY, 1f);
                playerSprite.sortingOrder = playerOrder;

                if (playerMoving)
                {
                    var screenDx = playerDelta.x - playerDelta.y;
                    if (Mathf.Abs(screenDx) > 0.002f)
                    {
                        playerSprite.flipX = screenDx < 0f;
                    }

                    var activeFrames = _lastCrouching ? playerCrouchWalkFrames : playerWalkFrames;
                    var hasMultiFrames = activeFrames != null && activeFrames.Length > 2;
                    var strideRate = hasMultiFrames
                        ? (_lastSprinting ? 13.5f : (_lastCrouching ? 7.2f : 10.0f))
                        : (_lastSprinting ? 10.5f : (_lastCrouching ? 4.8f : 7.4f));
                    _playerStrideClock += dt * strideRate;
                    var strideInt = Mathf.FloorToInt(_playerStrideClock);
                    var footStep = hasMultiFrames ? (strideInt / 3) : strideInt;
                    if (footStep != _prevPlayerStrideStep)
                    {
                        _prevPlayerStrideStep = footStep;
                        playerBootContact = true;
                        if (!_lastCrouching)
                        {
                            var moveAngle = Mathf.Atan2(playerDelta.y - playerDelta.x, playerDelta.x + playerDelta.y);
                            trailSurface?.StampFootprint(pWorld, moveAngle, _lastSprinting, isGuard: false);
                        }
                    }

                    playerSprite.sprite = _lastCrouching
                        ? PickWalkSprite(playerCrouchWalkFrames, strideInt, playerCrouchWalk1Sprite, playerCrouchWalk2Sprite, playerCrouchSprite ?? playerStandSprite)
                        : PickWalkSprite(playerWalkFrames, strideInt, playerWalk1Sprite, playerWalk2Sprite, playerStandSprite);

                    _footstepCooldown -= dt;
                    if (_footstepCooldown <= 0f)
                    {
                        _footstepCooldown = _lastSprinting ? 0.24f : (_lastCrouching ? 0.50f : 0.36f);
                        if (_lastSprinting)
                        {
                            feel?.EmitFootstepPuff(pWorld);
                            audioFloor?.PlayStep(sprint: true);
                        }
                        else if (!_lastCrouching)
                        {
                            audioFloor?.PlayStep(sprint: false);
                        }
                    }
                }
                else
                {
                    playerSprite.sprite = _lastCrouching
                        ? (playerCrouchSprite ?? playerStandSprite)
                        : playerStandSprite;
                    _footstepCooldown = 0.05f;
                }
            }

            _lastPlayer = renderPlayer;

            // T01: Advance scrolling world-anchored 2D trail canvas & floor footprints
            trailSurface?.Step(renderPlayer, playerBootContact, dt);

            // T09: Heat-tier Color LUT atmosphere shift
            postFx?.SetHeatMood(snapshot.Heat, snapshot.GuardState);

            // Diegetic world channeling ring + stamina arc around thief
            if (feel != null)
            {
                var pWorld = playerWorldFeet;
                var channeling = !string.IsNullOrEmpty(snapshot.ChannelKind) && snapshot.ChannelHold > 0.01f;
                var pct = channeling ? Mathf.Clamp01(snapshot.ChannelProgress / snapshot.ChannelHold) : 0f;
                var isHazard = snapshot.ChannelKind == "vitrine" || snapshot.ChannelKind == "power";
                feel.UpdateChannelRing(pWorld, channeling, pct, isHazard);
                feel.UpdateStaminaArc(pWorld, _stamina.Normalized, _stamina.IsWinded);
            }

            // Guard 1 & Guard 2 + walk cycle + damped facing angle + hysteresis flipX + animated alert bubble + vision cones
            var targetGuardAngle = snapCamera
                ? (_game != null ? _game.GuardAngle : _guardAngle)
                : Mathf.LerpAngle(_prevSimGuardAngle * Mathf.Rad2Deg, _currSimGuardAngle * Mathf.Rad2Deg, interpAlpha) * Mathf.Deg2Rad;
            SyncGuardVisual(
                guardSprite,
                guardStandSprite,
                guardWalkFrames,
                guardBubbleSprite,
                guardVisionCone,
                renderGuard,
                ref _lastGuard,
                targetGuardAngle,
                ref _guardAngle,
                ref _guardFlipCooldown,
                ref _guardStrideClock,
                ref _prevGuardStrideStep,
                ref _bubblePopTimer,
                ref _bubbleWasShown,
                snapshot.GuardState,
                depthOffset: 2,
                breathPhase: 1.1f,
                snapCamera,
                dt,
                snapshot.Tier,
                updateCones);

            if (snapshot.HasSecondGuard)
            {
                var targetGuard2Angle = snapCamera
                    ? (_game != null ? _game.Guard2Angle : _guard2Angle)
                    : Mathf.LerpAngle(_prevSimGuard2Angle * Mathf.Rad2Deg, _currSimGuard2Angle * Mathf.Rad2Deg, interpAlpha) * Mathf.Deg2Rad;
                SyncGuardVisual(
                    guard2Sprite,
                    guard2StandSprite ?? guardStandSprite,
                    guard2WalkFrames,
                    guard2BubbleSprite,
                    guard2VisionCone,
                    renderGuard2,
                    ref _lastGuard2,
                    targetGuard2Angle,
                    ref _guard2Angle,
                    ref _guard2FlipCooldown,
                    ref _guard2StrideClock,
                    ref _prevGuard2StrideStep,
                    ref _bubble2PopTimer,
                    ref _bubble2WasShown,
                    snapshot.Guard2State,
                    depthOffset: 3,
                    breathPhase: 2.7f,
                    snapCamera,
                    dt,
                    snapshot.Tier,
                    updateCones);
            }
            else
            {
                if (guard2Sprite != null) guard2Sprite.enabled = false;
                if (guard2BubbleSprite != null) guard2BubbleSprite.enabled = false;
                if (guard2VisionCone != null) guard2VisionCone.gameObject.SetActive(false);
            }

            // Staff + walk cycle
            var deltaStaff = renderStaff - _lastStaff;
            var staffMoving = dt > 1e-5f && (deltaStaff.magnitude / dt) > 0.12f;
            var staffOrder = Iso2DMath.DepthOrder(renderStaff.x, renderStaff.y, 0);
            if (staffSprite != null)
            {
                var sWorld = Iso2DMath.TileToWorld(renderStaff);
                staffSprite.transform.position = sWorld;
                staffSprite.sortingOrder = staffOrder;

                if (staffMoving)
                {
                    var screenDx = deltaStaff.x - deltaStaff.y;
                    if (Mathf.Abs(screenDx) > 0.001f)
                    {
                        staffSprite.flipX = screenDx < 0f;
                    }

                    var staffRate = (staffWalkFrames != null && staffWalkFrames.Length > 2) ? 8.5f : 6.2f;
                    _staffStrideClock += dt * staffRate;
                    staffSprite.sprite = PickWalkSprite(
                        staffWalkFrames,
                        Mathf.FloorToInt(_staffStrideClock),
                        staffWalk1Sprite,
                        staffWalk2Sprite,
                        staffStandSprite);
                }
                else if (staffStandSprite != null)
                {
                    staffSprite.sprite = staffStandSprite;
                }
            }

            _lastStaff = renderStaff;

            if (seeThrough != null)
            {
                var actorCount = 0;
                if (playerSprite != null && playerSprite.enabled)
                {
                    _seeThroughActors[actorCount++] = new Market2DSeeThrough.SeeThroughActor(renderPlayer, playerWorldFeet, playerOrder);
                }

                if (guardSprite != null && guardSprite.enabled)
                {
                    _seeThroughActors[actorCount++] = new Market2DSeeThrough.SeeThroughActor(
                        renderGuard,
                        Iso2DMath.TileToWorld(renderGuard),
                        Iso2DMath.DepthOrder(renderGuard.x, renderGuard.y, 2));
                }

                if (snapshot.HasSecondGuard && guard2Sprite != null && guard2Sprite.enabled)
                {
                    _seeThroughActors[actorCount++] = new Market2DSeeThrough.SeeThroughActor(
                        renderGuard2,
                        Iso2DMath.TileToWorld(renderGuard2),
                        Iso2DMath.DepthOrder(renderGuard2.x, renderGuard2.y, 3));
                }

                if (staffSprite != null && staffSprite.enabled)
                {
                    _seeThroughActors[actorCount++] = new Market2DSeeThrough.SeeThroughActor(
                        renderStaff,
                        Iso2DMath.TileToWorld(renderStaff),
                        staffOrder);
                }

                seeThrough.StepActors(_seeThroughActors, actorCount, dt);
            }

            // Acoustic sound wave ring & guard investigation/chase target reticle
            if (feel != null && _game != null)
            {
                feel.UpdateAcousticWave(_game.ActiveSoundOrigin, _game.ActiveSoundRadius, _game.ActiveSoundMaxRadius);
                var g1Hunting = snapshot.GuardState == "investigate" || snapshot.GuardState == "chase" || snapshot.GuardState == "search";
                var g2Hunting = snapshot.HasSecondGuard && (snapshot.Guard2State == "investigate" || snapshot.Guard2State == "chase" || snapshot.Guard2State == "search");
                if (g1Hunting || !g2Hunting)
                {
                    var targetTile = _game.LastIntent.Target ?? snapshot.LastSeenPos;
                    feel.UpdateGuardTargetReticle(snapshot.Guard, targetTile, snapshot.GuardState);
                }
                else
                {
                    var targetTile = _game.LastGuard2Intent.Target ?? snapshot.LastSeenPos;
                    feel.UpdateGuardTargetReticle(snapshot.Guard2, targetTile, snapshot.Guard2State);
                }
            }

            // Synchronized CCTV rigs & floor V-cones driven by HeistGame's authoritative camera angles
            var nav = _navGrid ??= MarketGrid.Build();
            for (var i = 0; i < MarketData.Cameras.Length; i++)
            {
                var hasRig = cctvRigs != null && i < cctvRigs.Length && cctvRigs[i] != null;
                var cone = !hasRig && cctvCones != null && i < cctvCones.Length ? cctvCones[i] : null;
                if (!hasRig && cone == null)
                {
                    continue;
                }

                if (!snapshot.CamerasDisabled && !updateCones && !snapCamera)
                {
                    continue;
                }

                var def = MarketData.Cameras[i];
                var sweepBoost = 1f + Math.Max(0, snapshot.Tier - 2) * 0.14f;
                var angle = _game != null
                    ? _game.GetCameraAngle(i)
                    : (float)(def.BaseAngle + Math.Sin(_clock * def.SweepSpeed * sweepBoost + def.I) * def.SweepAmp);
                var sighting = (snapshot.CameraSightings & (1 << i)) != 0;

                if (hasRig)
                {
                    cctvRigs[i].Sync(
                        (float)def.I,
                        (float)def.J,
                        angle,
                        (float)def.HalfAngle,
                        (float)def.Range,
                        !snapshot.CamerasDisabled,
                        sighting,
                        nav);
                }
                else
                {
                    cone.gameObject.SetActive(!snapshot.CamerasDisabled);
                    if (!snapshot.CamerasDisabled)
                    {
                        var color = sighting
                            ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.50f)
                            : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.28f);
                        var outline = sighting
                            ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.98f)
                            : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.88f);
                        cone.SetFan((float)def.I, (float)def.J, angle, (float)def.HalfAngle, (float)def.Range, color, nav, outline, ignoreOriginWall: true);
                    }
                }
            }

            // Highlight the CCTV Power Panel in r6 while cameras are online so the player can find and cut power easily
            if (cctvPanelSprite != null)
            {
                _panelMpb ??= new MaterialPropertyBlock();
                cctvPanelSprite.GetPropertyBlock(_panelMpb);
                if (snapshot.CctvRestoring)
                {
                    var rPhase = 0.5f + 0.5f * Mathf.Sin(_clock * 11.5f);
                    var rCol = Color.Lerp(RisoPalette.Sun, RisoPalette.Coral, rPhase);
                    _panelMpb.SetColor("_HighlightColor", rCol);
                    _panelMpb.SetFloat("_HighlightAmount", 1f);
                    _panelMpb.SetFloat("_OutlineWidth", 8.2f);
                    cctvPanelSprite.color = Color.white;
                }
                else if (snapshot.CamerasDisabled)
                {
                    _panelMpb.SetFloat("_HighlightAmount", 0f);
                    cctvPanelSprite.color = new Color(0.62f, 0.64f, 0.68f, 1f);
                }
                else
                {
                    var pPhase = 0.5f + 0.5f * Mathf.Sin(_clock * 5.2f);
                    var pCol = Color.Lerp(RisoPalette.Coral, RisoPalette.Sun, pPhase);
                    _panelMpb.SetColor("_HighlightColor", pCol);
                    _panelMpb.SetFloat("_HighlightAmount", snapshot.PanelInRange ? 1f : Mathf.Lerp(0.82f, 0.98f, pPhase));
                    _panelMpb.SetFloat("_OutlineWidth", snapshot.PanelInRange ? 7.5f : 5.8f);
                    cctvPanelSprite.color = Color.white;
                }

                cctvPanelSprite.SetPropertyBlock(_panelMpb);
            }

            SyncDetaggerVisual(snapshot);
            SyncDecoyVisual(dt);

            // Cohesive lootable fixtures: highlight directly on the real fixture & swap to empty sprite once stolen!
            EnsureLootMeta();
            _bagLookup.Clear();
            if (snapshot.Bag != null)
            {
                foreach (var id in snapshot.Bag)
                {
                    _bagLookup.Add(id);
                }
            }

            if (lootFixtures != null)
            {
                foreach (var lf in lootFixtures)
                {
                    if (lf == null || string.IsNullOrEmpty(lf.fixtureId))
                    {
                        continue;
                    }

                    var stolen = _bagLookup.Contains(lf.fixtureId);
                    var hasMeta = _lootMeta.TryGetValue(lf.fixtureId, out var meta);
                    var inRange = false;
                    if (!stolen && hasMeta)
                    {
                        inRange = Vector2.Distance(snapshot.Player, meta.FxTile) <= 2.7f;
                    }

                    var justStolen = lf.SyncState(stolen, snapshot.VitrineOpen, inRange, _clock, dt);
                    if (justStolen)
                    {
                        var pos = lf.transform.position;
                        var tagged = hasMeta && meta.Entry.Tagged;
                        var dir = (Vector2)(pos - playerWorldFeet);
                        hitFeel?.TriggerHit(lf.spriteRenderer, pos + new Vector3(0f, 0.25f, 0f), dir, isKill: false, rfAccent: tagged, customStopTicks: 3);
                        feel?.EmitPickupBurst(pos, tagged);
                        audioFloor?.PlayPickup(tagged);
                        if (hasMeta)
                        {
                            var txt = "+€" + meta.Entry.Value
                                + (meta.Entry.VitrineGated ? " [VITRINE]" : (tagged ? " [RF]" : string.Empty));
                            hud?.SpawnFloatingPopup(pos, txt, tagged);
                        }
                    }
                }
            }

            // T15: Smooth dead-zone + velocity look-ahead camera rig / manual scout pan / fit overview + T17 whole-pixel shake
            if (mainCamera != null)
            {
                var pFocus = Iso2DMath.TileToWorld(renderPlayer, 0.6f);
                var focusGuard = (snapshot.HasSecondGuard && Vector2.Distance(renderPlayer, renderGuard2) < Vector2.Distance(renderPlayer, renderGuard))
                    ? renderGuard2
                    : renderGuard;
                var gFocus = Iso2DMath.TileToWorld(focusGuard, 0.6f);
                var activeRoom = ResolveRoomAt(renderPlayer.x, renderPlayer.y);
                Vector2? roomCenterWorld = activeRoom.HasValue
                    ? Iso2DMath.TileToWorld(activeRoom.Value.OriginI + activeRoom.Value.Width * 0.5f, activeRoom.Value.OriginJ + activeRoom.Value.Depth * 0.5f, 0.5f)
                    : (Vector2?)null;

                var rigPos = _cameraRig.Step(
                    new Vector2(pFocus.x, pFocus.y),
                    dt,
                    _targetOrthoSize,
                    activeRoom?.Id,
                    roomCenterWorld,
                    pointsOfInterest: null,
                    aimOffsetFromCenter: null,
                    secondTargetWorld: new Vector2(gFocus.x, gFocus.y));

                Vector3 target;
                var effectiveOrtho = _targetOrthoSize;
                if (_fitMode)
                {
                    target = Iso2DMath.TileToWorld(MarketData.WidthTiles * 0.5f, MarketData.HeightTiles * 0.5f, 0.5f);
                    target.x += SplitCenterOffsetX();
                }
                else if (_hasManualPan)
                {
                    target = _manualPanTarget;
                }
                else
                {
                    target = new Vector3(rigPos.x + SplitCenterOffsetX(), rigPos.y, -10f);
                    if (_cameraRig.Mode == CameraFollowMode.TwoTargets)
                    {
                        effectiveOrtho = _cameraRig.OrthoSize;
                    }
                }

                target.z = -10f;
                var shake = (feel != null ? feel.SampleShakeOffset(dt) : Vector3.zero)
                    + (hitFeel != null ? hitFeel.ShakeOffsetWorld : Vector3.zero);

                if (snapCamera)
                {
                    mainCamera.transform.position = target;
                    mainCamera.orthographicSize = effectiveOrtho;
                }
                else
                {
                    var posT = 1f - Mathf.Exp(-12f * dt);
                    var zoomT = 1f - Mathf.Exp(-12f * dt);
                    var basePos = (_fitMode || _hasManualPan)
                        ? Vector3.Lerp(mainCamera.transform.position, target, posT)
                        : target;
                    mainCamera.transform.position = new Vector3(basePos.x + shake.x, basePos.y + shake.y, -10f);
                    mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, effectiveOrtho, zoomT);
                }

                feel?.SyncAmbientCenter(mainCamera.transform.position);
            }
        }

        private void RefreshHud(in HeistSnapshot snapshot)
        {
            if (hud == null)
            {
                return;
            }

            var hint = MarketInteractions.Probe(snapshot);
            hud.Refresh(snapshot, hint);
            hud.UpdateStamina(_stamina.Normalized, _stamina.IsWinded, snapshot.BagCount);
            hud.UpdateZoomReadout(ZoomPercent, _fitMode);
            var archLabel = snapshot.Arch switch
            {
                GuardArchitecture.StateTree => "ST+HTN",
                GuardArchitecture.Bt => "BT",
                _ => "FSM",
            };
            hud.UpdateSquadTelemetry(
                archLabel,
                FormatGuardLine(snapshot, 0),
                FormatGuardLine(snapshot, 1),
                snapshot.HasSecondGuard,
                ActiveProfileLabel);
            hud.SetDetectedSustained(snapshot.Outcome == HeistOutcome.None && (snapshot.Visible || snapshot.CameraSightings != 0));

            // Compute next off-screen objective compass target
            if (mainCamera != null && snapshot.Outcome == HeistOutcome.None)
            {
                _bagLookup.Clear();
                if (snapshot.Bag != null)
                {
                    foreach (var id in snapshot.Bag)
                    {
                        _bagLookup.Add(id);
                    }
                }

                var (objLabel, objTile) = ResolveNextObjectiveTarget(snapshot, _bagLookup);
                hud.UpdateCompass(mainCamera, objLabel, Iso2DMath.TileToWorld(objTile, 0.6f));
            }
            else
            {
                hud.HideCompass();
            }

            // Gate all discrete event SFX, shakes, popups, and logs to happen at most once per simulation tick
            if (snapshot.Tick == _lastProcessedEventTick)
            {
                return;
            }

            _lastProcessedEventTick = snapshot.Tick;

            if (snapshot.ExitBlockedByTags && !_lastExitBlocked)
            {
                hud.ShowRefusal("EAS ALARM: de-tag stolen items first");
                TriggerDebouncedDetectionFeedback("ALARM", "EAS gate blocked by active RF tags", allowShake: true, urgent: true);
            }

            switch (snapshot.LastInteraction)
            {
                case InteractOutcome.BagFull:
                    hud.ShowRefusal("BAG FULL (6/6 items)");
                    audioFloor?.PlayRefusal();
                    break;
                case InteractOutcome.BlockedLineOfSight:
                    hud.ShowRefusal("BLOCKED (no line of sight)");
                    audioFloor?.PlayRefusal();
                    break;
                case InteractOutcome.PanelOutOfReach:
                    hud.ShowRefusal("Move closer to CCTV panel");
                    audioFloor?.PlayRefusal();
                    break;
                case InteractOutcome.NoTarget:
                    if (snapshot.ExitInRange && !snapshot.ExitBlockedByTags && snapshot.StolenValue < MarketData.Quota)
                    {
                        hud.ShowRefusal("NEED €" + (MarketData.Quota - snapshot.StolenValue) + " MORE LOOT TO EXIT");
                        audioFloor?.PlayRefusal();
                    }
                    break;
            }

            if (snapshot.VitrineOpen && !_lastVitrineOpen)
            {
                audioFloor?.PlayUnlock();
                feel?.TriggerShake(0.065f, 0.14f);
                var spRoom = MarketData.Room("r4");
                var spPos = Iso2DMath.TileToWorld(spRoom.OriginI + 7.2f, spRoom.OriginJ + 1.0f, 1.0f);
                feel?.EmitPickupBurst(spPos, rfTagged: false);
                hud.SpawnFloatingPopup(spPos, "VITRINE OPEN", false);
                hud.PushLog("OBJ", "Forced spirits vitrine lock");
                hud.MarkDrawerAttention();
            }

            if (snapshot.CamerasDisabled && !_lastCamerasDisabled)
            {
                audioFloor?.PlayUnlock();
                var pPos = Iso2DMath.TileToWorld((float)MarketData.Panel.I, (float)MarketData.Panel.J, 1.0f);
                feel?.EmitPickupBurst(pPos, rfTagged: false);
                hud.SpawnFloatingPopup(pPos, "CCTV OFFLINE", false);
                hud.PushLog("OBJ", "Cut CCTV power panel");
                hud.MarkDrawerAttention();
            }

            if (snapshot.CctvRestoring && !_lastCctvRestoring)
            {
                var pPos = Iso2DMath.TileToWorld((float)MarketData.Panel.I, (float)MarketData.Panel.J, 1.0f);
                hud.SpawnFloatingPopup(pPos, "REBOOTING CCTV...", rfAccent: true);
                hud.PushLog("CCTV", "Guard rebooting CCTV power panel");
                hud.MarkDrawerAttention();
            }

            if ((!snapshot.CamerasDisabled && _lastCamerasDisabled) || snapshot.CctvRestoreCount > _lastCctvRestoreCount)
            {
                audioFloor?.PlayAlert(force: false);
                var pPos = Iso2DMath.TileToWorld((float)MarketData.Panel.I, (float)MarketData.Panel.J, 1.0f);
                feel?.EmitPickupBurst(pPos, rfTagged: true);
                hud.SpawnFloatingPopup(pPos, "CCTV BACK ONLINE", rfAccent: true);
                hud.PushLog("CCTV", "Security restored CCTV power grid");
                hud.MarkDrawerAttention();
            }

            if (_lastActiveTags > 0 && snapshot.ActiveTags == 0 && snapshot.BagCount > 0)
            {
                audioFloor?.PlayUnlock();
                var dPos = Iso2DMath.TileToWorld((float)MarketData.Detagger.I, (float)MarketData.Detagger.J, 1.0f);
                feel?.EmitPickupBurst(dPos, rfTagged: false);
                hud.SpawnFloatingPopup(dPos, "RF TAGS CLEARED", false);
                hud.PushLog("OBJ", "De-tagged all RF items");
                hud.MarkDrawerAttention();
            }

            if (snapshot.FirstDetectionEdge)
            {
                TriggerDebouncedDetectionFeedback("ALARM", "Detective spotted you", allowShake: true, urgent: false, flashPlayerRed: true);
            }
            else if ((snapshot.GuardState == "chase" || snapshot.GuardState == "attack")
                     && _lastGuardState != "chase" && _lastGuardState != "attack")
            {
                TriggerDebouncedDetectionFeedback("ALARM", "Guard 1 entered " + snapshot.GuardState, allowShake: false, urgent: false, flashPlayerRed: false);
            }

            if (snapshot.HasSecondGuard
                && (snapshot.Guard2State == "chase" || snapshot.Guard2State == "attack")
                && _lastGuard2State != "chase" && _lastGuard2State != "attack")
            {
                TriggerDebouncedDetectionFeedback("ALARM", "Guard 2 entered " + snapshot.Guard2State, allowShake: false, urgent: false, flashPlayerRed: false);
            }

            if (snapshot.Arch == GuardArchitecture.StateTree)
            {
                if (snapshot.Guard1Role != _lastGuard1Role
                    && (snapshot.Guard1Role == GuardTacticalRole.ExitInterceptor || snapshot.Guard1Role == GuardTacticalRole.SabotageResponder))
                {
                    hud.PushLog("AI", "Guard 1 -> " + snapshot.Guard1Role + " (" + (snapshot.Guard1Plan ?? "Direct") + ")");
                }

                if (snapshot.HasSecondGuard
                    && snapshot.Guard2Role != _lastGuard2Role
                    && (snapshot.Guard2Role == GuardTacticalRole.ExitInterceptor || snapshot.Guard2Role == GuardTacticalRole.SabotageResponder))
                {
                    hud.PushLog("AI", "Guard 2 -> " + snapshot.Guard2Role + " (" + (snapshot.Guard2Plan ?? "Direct") + ")");
                }

                var g1Plan = snapshot.Guard1Plan;
                var g2Plan = snapshot.Guard2Plan;
                if (g1Plan != _lastGuard1Plan && Time.unscaledTime - _lastGuard1CalloutTime >= 4.0f)
                {
                    var callout = CalloutLabelForPlan(g1Plan);
                    if (!string.IsNullOrEmpty(callout) && guardSprite != null)
                    {
                        _lastGuard1CalloutTime = Time.unscaledTime;
                        hud.SpawnFloatingPopup(guardSprite.transform.position + new Vector3(0f, 0.35f, 0f), callout, rfAccent: true);
                    }
                }

                if (snapshot.HasSecondGuard && g2Plan != _lastGuard2Plan && Time.unscaledTime - _lastGuard2CalloutTime >= 4.0f)
                {
                    var callout = CalloutLabelForPlan(g2Plan);
                    if (!string.IsNullOrEmpty(callout) && guard2Sprite != null)
                    {
                        _lastGuard2CalloutTime = Time.unscaledTime;
                        hud.SpawnFloatingPopup(guard2Sprite.transform.position + new Vector3(0f, 0.35f, 0f), callout, rfAccent: true);
                    }
                }

                _lastGuard1Plan = g1Plan;
                _lastGuard2Plan = g2Plan;
            }

            if (snapshot.CameraSightings != 0 && _lastCameraSightings == 0)
            {
                TriggerDebouncedDetectionFeedback("CCTV", "Picked up on security camera", allowShake: false, urgent: false, flashPlayerRed: true);
            }

            var hasBackup = snapshot.BackupPhase != BackupPhase.None;
            if (hasBackup && !_lastBackup)
            {
                TriggerDebouncedDetectionFeedback("RADIO", "Staff called security backup", allowShake: false, urgent: false, flashPlayerRed: false);
            }

            if (snapshot.Bag != null)
            {
                foreach (var id in snapshot.Bag)
                {
                    if (_seenBagIds.Add(id))
                    {
                        var entry = MarketData.LootFor(id);
                        hud.PushLog("LOOT", "Pocketed " + (entry.HasValue ? entry.Value.Label : id));
                    }
                }
            }

            if (snapshot.Outcome != HeistOutcome.None && _lastOutcome == HeistOutcome.None)
            {
                var escaped = snapshot.Outcome == HeistOutcome.Escaped;
                audioFloor?.PlayOutcome(escaped);
                if (escaped)
                {
                    feel?.EmitPickupBurst(Iso2DMath.TileToWorld(snapshot.Player, 0.8f), rfTagged: false);
                }
                else
                {
                    var pWorld = Iso2DMath.TileToWorld(snapshot.Player, 0.5f);
                    var gWorld = Iso2DMath.TileToWorld(snapshot.Guard, 0.5f);
                    hitFeel?.TriggerHit(playerSprite, pWorld, (Vector2)(pWorld - gWorld), isKill: true, rfAccent: true);
                    feel?.TriggerShake(0.09f, 0.22f);
                    hud.TriggerAlertFlash();
                }
            }

            _lastExitBlocked = snapshot.ExitBlockedByTags;
            _lastVitrineOpen = snapshot.VitrineOpen;
            _lastCamerasDisabled = snapshot.CamerasDisabled;
            _lastCctvRestoring = snapshot.CctvRestoring;
            _lastCctvRestoreCount = snapshot.CctvRestoreCount;
            _lastActiveTags = snapshot.ActiveTags;
            _lastCameraSightings = snapshot.CameraSightings;
            _lastBackup = hasBackup;
            _lastGuardState = snapshot.GuardState;
            _lastGuard2State = snapshot.Guard2State;
            _lastGuard1Role = snapshot.Guard1Role;
            _lastGuard2Role = snapshot.Guard2Role;
            _lastOutcome = snapshot.Outcome;
        }

        private void TriggerDebouncedDetectionFeedback(
            string logTag,
            string logMsg,
            bool allowShake,
            bool urgent = false,
            bool flashPlayerRed = true)
        {
            var now = Time.unscaledTime;
            if (urgent || (now - _lastAlertLogTime) >= 3.0f)
            {
                _lastAlertLogTime = now;
                hud?.PushLog(logTag, logMsg);
                hud?.MarkDrawerAttention();
            }

            if (urgent || (now - _lastAlertFeedbackTime) >= AlertFeedbackCooldownSeconds)
            {
                _lastAlertFeedbackTime = now;
                audioFloor?.PlayAlert(urgent);
                hud?.TriggerAlertFlash();
                _playerStatus.Trigger(playerSprite, flashPlayerRed ? StatusFrameLook.FlashRed : StatusFrameLook.Outline);
            }

            if (allowShake && (urgent || (now - _lastAlertShakeTime) >= AlertShakeCooldownSeconds))
            {
                _lastAlertShakeTime = now;
                feel?.TriggerShake(urgent ? 0.09f : 0.052f, urgent ? 0.20f : 0.13f);
            }
        }

        private string FormatGuardLine(in HeistSnapshot snapshot, int guardIndex)
        {
            var state = (guardIndex == 0 ? snapshot.GuardState : snapshot.Guard2State) ?? "patrol";
            if (_game != null && snapshot.Arch == GuardArchitecture.StateTree)
            {
                var st = guardIndex == 0 ? _game.LastStateTree1 : _game.LastStateTree2;
                var plan = string.IsNullOrEmpty(st.PlanName) ? "Patrol" : st.PlanName;
                return state.ToUpperInvariant() + " · " + plan;
            }

            return state.ToUpperInvariant() + " · " + ActiveProfileLabel;
        }

        private static string CalloutLabelForPlan(string plan)
        {
            return plan switch
            {
                "FlankCutoff" => "FLANKING!",
                "CutOffEasExit" => "CUTTING EXIT!",
                "RespondToEasAlarm" => "LOCKDOWN!",
                "InvestigateCctvSabotage" => "CHECK PANEL!",
                "InvestigateForcedVitrine" => "CHECK VITRINE!",
                _ => null,
            };
        }

        private static Sprite PickWalkSprite(Sprite[] frames, int step, Sprite fallbackWalk1, Sprite fallbackWalk2, Sprite stand)
        {
            if (frames != null && frames.Length > 2)
            {
                var idx = ((step % frames.Length) + frames.Length) % frames.Length;
                if (frames[idx] != null)
                {
                    return frames[idx];
                }
            }

            return (step % 2 == 0) ? (fallbackWalk1 ?? stand) : (fallbackWalk2 ?? stand);
        }

        private void SyncDetaggerVisual(in HeistSnapshot snapshot)
        {
            if (detaggerPadSprite == null)
            {
                return;
            }

            _detaggerMpb ??= new MaterialPropertyBlock();
            detaggerPadSprite.GetPropertyBlock(_detaggerMpb);
            if (snapshot.ActiveTags > 0)
            {
                var phase = 0.5f + 0.5f * Mathf.Sin(_clock * 5.6f);
                var col = Color.Lerp(RisoPalette.Teal, RisoPalette.Sun, phase);
                _detaggerMpb.SetColor("_HighlightColor", col);
                _detaggerMpb.SetFloat("_HighlightAmount", snapshot.DetaggerInRange ? 1f : Mathf.Lerp(0.75f, 0.96f, phase));
                _detaggerMpb.SetFloat("_OutlineWidth", snapshot.DetaggerInRange ? 7.5f : 5.6f);
                detaggerPadSprite.color = Color.white;
            }
            else
            {
                _detaggerMpb.SetFloat("_HighlightAmount", 0f);
                detaggerPadSprite.color = new Color(0.90f, 0.93f, 0.94f, 1f);
            }

            detaggerPadSprite.SetPropertyBlock(_detaggerMpb);
        }

        private void SyncDecoyVisual(float dt)
        {
            if (decoyCoinSprite == null)
            {
                return;
            }

            if (_decoyVisualTimer <= 0f)
            {
                decoyCoinSprite.enabled = false;
                return;
            }

            _decoyVisualTimer = Mathf.Max(0f, _decoyVisualTimer - dt);
            if (_decoyVisualTimer <= 0f)
            {
                decoyCoinSprite.enabled = false;
                return;
            }

            var elapsed = 2.6f - _decoyVisualTimer;
            var bounceZ = Mathf.Abs(Mathf.Sin(elapsed * 9.5f)) * Mathf.Exp(-elapsed * 3.8f) * 0.35f;
            var alpha = _decoyVisualTimer < 0.6f ? Mathf.Clamp01(_decoyVisualTimer / 0.6f) : 1f;
            decoyCoinSprite.enabled = true;
            decoyCoinSprite.transform.position = Iso2DMath.TileToWorld(_decoyTile.x, _decoyTile.y, bounceZ);
            decoyCoinSprite.sortingOrder = Iso2DMath.DepthOrder(_decoyTile.x, _decoyTile.y, 6);
            decoyCoinSprite.color = new Color(1f, 1f, 1f, alpha);
        }

        private void SyncGuardVisual(
            SpriteRenderer sprite,
            Sprite standSprite,
            Sprite[] walkFrames,
            SpriteRenderer bubbleSprite,
            Iso2DVisionCone visionCone,
            Vector2 renderPos,
            ref Vector2 lastPos,
            float targetAngle,
            ref float smoothAngle,
            ref float flipCooldown,
            ref float strideClock,
            ref int prevStrideStep,
            ref float bubblePopTimer,
            ref bool bubbleWasShown,
            string state,
            int depthOffset,
            float breathPhase,
            bool snapCamera,
            float dt,
            int tier,
            bool updateCones)
        {
            var delta = renderPos - lastPos;
            var moving = dt > 1e-5f && (delta.magnitude / dt) > 0.12f;
            if (snapCamera)
            {
                smoothAngle = targetAngle;
                flipCooldown = 0f;
            }
            else
            {
                var turnT = 1f - Mathf.Exp(-10f * dt);
                smoothAngle = Mathf.LerpAngle(smoothAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg, turnT) * Mathf.Deg2Rad;
                flipCooldown = Mathf.Max(0f, flipCooldown - dt);
            }

            if (delta.sqrMagnitude > 1e-6f)
            {
                lastPos = renderPos;
            }

            if (sprite != null)
            {
                sprite.enabled = true;
                var world = Iso2DMath.TileToWorld(renderPos);
                var breathSin = moving ? 0f : Mathf.Sin(_clock * 2.6f + breathPhase);
                var breathX = 1f - breathSin * 0.010f;
                var breathY = 1f + breathSin * 0.020f;
                sprite.transform.position = new Vector3(world.x, world.y, 0f);
                sprite.transform.localScale = new Vector3(breathX, breathY, 1f);
                var order = Iso2DMath.DepthOrder(renderPos.x, renderPos.y, depthOffset);
                sprite.sortingOrder = order;

                if (moving)
                {
                    var screenVx = dt > 1e-4f ? (delta.x - delta.y) / dt : 0f;
                    if (flipCooldown <= 0f && Mathf.Abs(screenVx) > 0.50f)
                    {
                        var wantFlip = screenVx < 0f;
                        if (sprite.flipX != wantFlip)
                        {
                            sprite.flipX = wantFlip;
                            flipCooldown = 0.24f;
                        }
                    }

                    var hasMultiFrames = walkFrames != null && walkFrames.Length > 2;
                    strideClock += dt * (hasMultiFrames ? 10.0f : 7.0f);
                    var stride = Mathf.FloorToInt(strideClock);
                    var footStep = hasMultiFrames ? (stride / 3) : stride;
                    if (footStep != prevStrideStep)
                    {
                        prevStrideStep = footStep;
                        if (state == "chase" || state == "investigate")
                        {
                            var angle = Mathf.Atan2(delta.y - delta.x, delta.x + delta.y);
                            trailSurface?.StampFootprint(world, angle, state == "chase", isGuard: true);
                        }
                    }

                    sprite.sprite = PickWalkSprite(walkFrames, stride, guardWalk1Sprite, guardWalk2Sprite, standSprite ?? guardStandSprite);
                }
                else
                {
                    var coneScreenX = Mathf.Cos(smoothAngle) - Mathf.Sin(smoothAngle);
                    if (flipCooldown <= 0f && Mathf.Abs(coneScreenX) > 0.35f)
                    {
                        var wantFlip = coneScreenX < 0f;
                        if (sprite.flipX != wantFlip)
                        {
                            sprite.flipX = wantFlip;
                            flipCooldown = 0.24f;
                        }
                    }

                    var resolvedStand = standSprite ?? guardStandSprite;
                    if (resolvedStand != null)
                    {
                        sprite.sprite = resolvedStand;
                    }
                }

                if (bubbleSprite != null)
                {
                    bubbleSprite.sortingOrder = order + 60;
                    var isAlert = state == "chase" || state == "attack";
                    var isSearch = state == "investigate" || state == "search";
                    var showBubble = isAlert || isSearch;
                    bubbleSprite.enabled = showBubble;

                    if (showBubble && !bubbleWasShown)
                    {
                        bubblePopTimer = 0.24f;
                    }

                    bubbleWasShown = showBubble;
                    if (bubblePopTimer > 0f)
                    {
                        bubblePopTimer = Mathf.Max(0f, bubblePopTimer - dt);
                        var u = 1f - (bubblePopTimer / 0.24f);
                        bubbleSprite.transform.localScale = Vector3.one * Market2DFeel.EaseOutBack(u);
                    }
                    else
                    {
                        bubbleSprite.transform.localScale = Vector3.one;
                    }

                    if (isAlert && alertBubbleSprite != null)
                    {
                        bubbleSprite.sprite = alertBubbleSprite;
                    }
                    else if (isSearch && questionBubbleSprite != null)
                    {
                        bubbleSprite.sprite = questionBubbleSprite;
                    }
                }
            }

            if (visionCone != null)
            {
                visionCone.gameObject.SetActive(true);
                if (updateCones || snapCamera)
                {
                    var coneColor = state switch
                    {
                        "chase" or "attack" => new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.36f),
                        "investigate" or "search" => new Color(RisoPalette.Sun.r, RisoPalette.Sun.g, RisoPalette.Sun.b, 0.36f),
                        _ => new Color(RisoPalette.Teal.r, RisoPalette.Teal.g, RisoPalette.Teal.b, 0.26f),
                    };
                    var baseRange = _profile != null ? _profile.fov_distance : 5.4f;
                    var range = baseRange * (1f + Mathf.Max(0, tier - 2) * 0.08f);
                    var halfAngle = _profile != null ? _profile.fov_half_angle : 0.60f;
                    visionCone.SetFan(renderPos.x, renderPos.y, smoothAngle, halfAngle, range, coneColor, _navGrid ??= MarketGrid.Build());
                }
            }
        }

        private void EnsureLootMeta()
        {
            if (_lootMeta != null)
            {
                return;
            }

            _lootMeta = new Dictionary<string, (LootEntry Entry, Vector2 FxTile)>(MarketData.Loot.Length, StringComparer.Ordinal);
            foreach (var entry in MarketData.Loot)
            {
                var room = MarketData.Room(entry.Room);
                var fxTile = new Vector2(
                    (float)(room.OriginI + entry.I + 0.8),
                    (float)(room.OriginJ + entry.J + 0.6));
                _lootMeta[entry.Id] = (entry, fxTile);
            }
        }

        private static RoomRect? ResolveRoomAt(float i, float j)
        {
            foreach (var rm in MarketData.Rooms)
            {
                if (i >= rm.OriginI && i < rm.OriginI + rm.Width && j >= rm.OriginJ && j < rm.OriginJ + rm.Depth)
                {
                    return rm;
                }
            }

            return null;
        }

        private static (string Label, Vector2 Tile) ResolveNextObjectiveTarget(in HeistSnapshot snapshot, HashSet<string> carried)
        {
            if (snapshot.StolenValue >= MarketData.Quota)
            {
                if (snapshot.ActiveTags > 0)
                {
                    return ("DE-TAGGER", new Vector2((float)MarketData.Detagger.I, (float)MarketData.Detagger.J));
                }

                return ("EAS EXIT", new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J));
            }

            LootEntry? best = null;
            var bestDist = float.MaxValue;
            foreach (var loot in MarketData.Loot)
            {
                if (carried.Contains(loot.Id))
                {
                    continue;
                }

                var room = MarketData.Room(loot.Room);
                var pt = new Vector2((float)(room.OriginI + loot.I + 0.5), (float)(room.OriginJ + loot.J + 0.5));
                var d = Vector2.Distance(snapshot.Player, pt);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = loot;
                }
            }

            if (best.HasValue)
            {
                var rm = MarketData.Room(best.Value.Room);
                var pt = new Vector2((float)(rm.OriginI + best.Value.I + 0.5), (float)(rm.OriginJ + best.Value.J + 0.5));
                return ("€" + best.Value.Value + " LOOT", pt);
            }

            return ("EAS EXIT", new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J));
        }

        private void ReadInput(
            out HeistInput input,
            out bool reset,
            out bool toggleArch,
            out bool cycleTrait,
            out bool tossDecoy)
        {
            var right = 0f;
            var up = 0f;
            if (Held(Key.D, KeyCode.D) || Held(Key.RightArrow, KeyCode.RightArrow)) right += 1f;
            if (Held(Key.A, KeyCode.A) || Held(Key.LeftArrow, KeyCode.LeftArrow)) right -= 1f;
            if (Held(Key.W, KeyCode.W) || Held(Key.UpArrow, KeyCode.UpArrow)) up += 1f;
            if (Held(Key.S, KeyCode.S) || Held(Key.DownArrow, KeyCode.DownArrow)) up -= 1f;

            var gridMove = Iso2DMath.ScreenInputToGrid(right, up);
            var sprint = Held(Key.LeftShift, KeyCode.LeftShift) || Held(Key.RightShift, KeyCode.RightShift);
            var crouch = Held(Key.C, KeyCode.C);
            var interact = Held(Key.E, KeyCode.E) || Held(Key.Space, KeyCode.Space);

            input = new HeistInput(gridMove.x, gridMove.y, interact, crouch, sprint);
            reset = Pressed(Key.R, KeyCode.R);
            toggleArch = Pressed(Key.Tab, KeyCode.Tab) || Pressed(Key.B, KeyCode.B);
            cycleTrait = Pressed(Key.P, KeyCode.P);
            tossDecoy = Pressed(Key.Q, KeyCode.Q);

            // Keyboard zoom shortcuts (+ / - / 0)
            if (Pressed(Key.Equals, KeyCode.Equals) || Pressed(Key.NumpadPlus, KeyCode.Plus)) ZoomIn();
            if (Pressed(Key.Minus, KeyCode.Minus) || Pressed(Key.NumpadMinus, KeyCode.Minus)) ZoomOut();
            if (Pressed(Key.Digit0, KeyCode.Alpha0)) ToggleFitOverview();

            // SpriteCook technique live toggle hotkeys: V (camera), O (see-through), T (transition), F8 (CRT)
            if (Pressed(Key.V, KeyCode.V))
            {
                var modeName = _cameraRig.CycleMode();
                hud?.PushLog("CAM", "Camera mode -> " + modeName);
                audioFloor?.PlayUiClick();
            }

            if (Pressed(Key.O, KeyCode.O) && seeThrough != null)
            {
                var stName = seeThrough.CycleMode();
                hud?.PushLog("FX", "See-through -> " + stName);
                audioFloor?.PlayUiClick();
            }

            if (Pressed(Key.T, KeyCode.T) && postFx != null)
            {
                var trName = postFx.CycleTransitionStyle();
                hud?.PushLog("FX", "Transition -> " + trName);
                audioFloor?.PlayUiClick();
            }

            if (Pressed(Key.F8, KeyCode.F8) && postFx != null)
            {
                var on = postFx.ToggleCrt();
                hud?.PushLog("FX", "CRT monitor pass -> " + (on ? "ON" : "OFF"));
                audioFloor?.PlayUiClick();
            }

            if (Pressed(Key.I, KeyCode.I) || Pressed(Key.M, KeyCode.M))
            {
                hud?.ToggleDrawer();
                audioFloor?.PlayUiClick();
            }

            if (Pressed(Key.Escape, KeyCode.Escape) && hud != null && hud.IsDrawerOpen)
            {
                hud.SetDrawerOpen(false);
                audioFloor?.PlayUiClick();
            }

            HandleMouseCamera();
        }

        private void HandleMouseCamera()
        {
            var mouse = Mouse.current;
            if (mouse == null || mainCamera == null)
            {
                return;
            }

            var mousePos = mouse.position.ReadValue();
            var overViewport = mousePos.x >= 0f
                && mousePos.x <= Screen.width
                && (hud == null || !hud.IsScreenPointOverDrawer(mousePos));

            // Smooth continuous scroll-wheel zoom (preserves cursor focus in viewport)
            if (overViewport)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _fitMode = false;
                    var factor = scroll > 0f ? 0.88f : 1.136f;
                    var prevTarget = _targetOrthoSize;
                    _targetOrthoSize = Mathf.Clamp(_targetOrthoSize * factor, MinOrthoSize, FitOrthoSize);

                    // Gently shift manual pan anchor if scouting so zoom feels anchored around cursor
                    if (_hasManualPan && Mathf.Abs(_targetOrthoSize - prevTarget) > 1e-4f)
                    {
                        var cursorWorld = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));
                        var ratio = 1f - (_targetOrthoSize / Mathf.Max(0.1f, prevTarget));
                        _manualPanTarget.x += (cursorWorld.x - _manualPanTarget.x) * ratio * 0.45f;
                        _manualPanTarget.y += (cursorWorld.y - _manualPanTarget.y) * ratio * 0.45f;
                    }

                    hud?.UpdateZoomReadout(ZoomPercent, _fitMode);
                }
            }

            // Right-drag or middle-drag to scout/pan around the supermarket
            var panHeld = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (panHeld && overViewport)
            {
                if (!_draggingPan)
                {
                    _draggingPan = true;
                    _lastDragScreen = mousePos;
                    if (!_hasManualPan)
                    {
                        _hasManualPan = true;
                        _manualPanTarget = mainCamera.transform.position;
                    }
                }
                else
                {
                    var deltaPx = mousePos - _lastDragScreen;
                    _lastDragScreen = mousePos;
                    var unitsPerPx = (mainCamera.orthographicSize * 2f) / Mathf.Max(1f, mainCamera.pixelHeight);
                    _manualPanTarget.x -= deltaPx.x * unitsPerPx;
                    _manualPanTarget.y -= deltaPx.y * unitsPerPx;
                    _fitMode = false;
                }
            }
            else
            {
                _draggingPan = false;
            }
        }

        private static bool Held(Key key, KeyCode code)
        {
            var kb = Keyboard.current;
            if (kb != null && kb[key].isPressed)
            {
                return true;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(code);
#else
            return false;
#endif
        }

        private static bool Pressed(Key key, KeyCode code)
        {
            var kb = Keyboard.current;
            if (kb != null && kb[key].wasPressedThisFrame)
            {
                return true;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(code);
#else
            return false;
#endif
        }
    }
}
