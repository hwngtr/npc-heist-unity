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
        /// <summary>Right-hand editorial panel width in reference pixels at 1600px wide.</summary>
        public const float RightPanelWidthPx = 420f;

        /// <summary>Base sprite world pixels per Unity unit (64 world px = 1 unit; 2x sprites = 128 PPU).</summary>
        public const float BaseWorldPpu = 64f;

        /// <summary>Minimum close-up orthographic size.</summary>
        public const float MinOrthoSize = 2.6f;

        /// <summary>Default gameplay orthographic half-height (shows full room + doorway sightlines cleanly).</summary>
        public const float DefaultOrthoSize = 4.2f;

        /// <summary>Fit-whole-market orthographic size.</summary>
        public const float FitOrthoSize = 10.2f;

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

        /// <summary>CCTV floor-projected 2D vision cones in MarketData.Cameras order.</summary>
        public Iso2DVisionCone[] cctvCones = Array.Empty<Iso2DVisionCone>();

        /// <summary>Fake-3D sprite-stacked rotating CCTV turrets (T20) in MarketData.Cameras order.</summary>
        public SpriteStack2D[] cctvStacks = Array.Empty<SpriteStack2D>();

        /// <summary>Highlighted CCTV power panel sprite at MarketData.Panel so the player can spot and cut camera power.</summary>
        public SpriteRenderer cctvPanelSprite;

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

        private static readonly HashSet<KeyCode> TrackedKeys = new()
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.E, KeyCode.Space, KeyCode.C,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.R, KeyCode.Tab, KeyCode.B,
            KeyCode.P, KeyCode.Q, KeyCode.V, KeyCode.O, KeyCode.T, KeyCode.F8,
            KeyCode.Equals, KeyCode.Plus, KeyCode.Minus, KeyCode.Alpha0,
        };

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
        private Vector2 _prevSimStaff;
        private Vector2 _currSimStaff;
        private float _prevSimGuardAngle = 1.5f;
        private float _currSimGuardAngle = 1.5f;
        private float _guardFlipCooldown;
        private Vector2 _smoothedMove;
        private int _prevPlayerStrideStep = -1;
        private int _prevGuardStrideStep = -1;

        private readonly HashSet<KeyCode> _guiKeys = new();
        private readonly Dictionary<KeyCode, int> _guiKeyDownFrame = new();
        private readonly HashSet<string> _seenBagIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _bagLookup = new(StringComparer.Ordinal);

        private HeistGame _game;
        private GuardProfile _profile;
        private GuardArchitecture _arch = GuardArchitecture.Fsm;
        private float _clock;
        private float _guardAngle = 1.5f;
        private Vector2 _lastPlayer;
        private Vector2 _lastGuard = HeistGame.GuardStart;
        private Vector2 _lastStaff;
        private float _playerStrideClock;
        private float _guardStrideClock;
        private float _staffStrideClock;
        private float _footstepCooldown;
        private float _decoyCooldown;
        private float _bubblePopTimer;
        private bool _bubbleWasShown;
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
        private int _lastActiveTags;
        private int _lastCameraSightings;
        private string _lastGuardState = "patrol";
        private HeistOutcome _lastOutcome = HeistOutcome.None;
        private int _frame;

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
            _game = new HeistGame(_profile, _arch);
            _navGrid = MarketGrid.Build();
            var snap = _game.Snapshot();
            _lastPlayer = snap.Player;
            _lastGuard = snap.Guard;
            _lastStaff = snap.Staff;
            _prevSimPlayer = _currSimPlayer = snap.Player;
            _prevSimGuard = _currSimGuard = snap.Guard;
            _prevSimStaff = _currSimStaff = snap.Staff;
            _prevSimGuardAngle = _currSimGuardAngle = _game.GuardAngle;

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
            seeThrough.BindFixtures(transform.Find("Fixtures"));
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
            _game ??= new HeistGame(_profile ??= GuardProfile.CreateDefault("standard"), _arch);
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
            var currentBagCount = _game.Snapshot().BagCount;
            var (allowSprint, speedScale, justWinded) = _stamina.Step(
                input.Sprint,
                input.HasDirection,
                input.Crouch,
                currentBagCount,
                rawDt);
            var rampMag = hasSmoothedMove ? Mathf.Clamp01(_smoothedMove.magnitude) : 1f;
            _game.PlayerSpeedScale = speedScale * (input.HasDirection ? Mathf.Lerp(0.45f, 1f, rampMag) : rampMag);
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
            if (hitFeel == null || !hitFeel.IsHitStopped)
            {
                _simAccum = Mathf.Min(_simAccum + dt, FixedSimStep * 3f);
                while (_simAccum >= FixedSimStep)
                {
                    _simAccum -= FixedSimStep;
                    _prevSimPlayer = _currSimPlayer;
                    _prevSimGuard = _currSimGuard;
                    _prevSimStaff = _currSimStaff;
                    _prevSimGuardAngle = _currSimGuardAngle;

                    if (_game.Outcome == HeistOutcome.None)
                    {
                        _clock += FixedSimStep;
                    }

                    _game.Step(FixedSimStep, effectiveInput);
                    var stepped = _game.Snapshot();
                    _currSimPlayer = stepped.Player;
                    _currSimGuard = stepped.Guard;
                    _currSimStaff = stepped.Staff;
                    _currSimGuardAngle = _game.GuardAngle;
                }
            }

            _lastCrouching = effectiveInput.Crouch;
            _lastSprinting = effectiveInput.Sprint;
            var snapshot = _game.Snapshot();
            var interpAlpha = Mathf.Clamp01(_simAccum / FixedSimStep);
            SyncScene(snapshot, snapCamera: false, dt: rawDt, interpAlpha: interpAlpha);

            if (_frame % 2 == 0 || toggleArch || snapshot.Outcome != _lastOutcome)
            {
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
            _game = new HeistGame(_profile ??= GuardProfile.CreateDefault("standard"), _arch);
            _clock = 0f;
            _guardAngle = 1.5f;
            var snapshot = _game.Snapshot();
            _lastPlayer = snapshot.Player;
            _lastGuard = snapshot.Guard;
            _lastStaff = snapshot.Staff;
            _prevSimPlayer = _currSimPlayer = snapshot.Player;
            _prevSimGuard = _currSimGuard = snapshot.Guard;
            _prevSimStaff = _currSimStaff = snapshot.Staff;
            _prevSimGuardAngle = _currSimGuardAngle = _game.GuardAngle;
            _smoothedMove = Vector2.zero;
            _prevPlayerStrideStep = -1;
            _prevGuardStrideStep = -1;
            _seenBagIds.Clear();
            _lastBackup = false;
            _lastExitBlocked = false;
            _lastVitrineOpen = false;
            _lastCamerasDisabled = false;
            _lastActiveTags = 0;
            _lastCameraSightings = 0;
            _lastGuardState = "patrol";
            _lastOutcome = HeistOutcome.None;
            _fitMode = false;
            _hasManualPan = false;
            _targetOrthoSize = DefaultOrthoSize;

            hitFeel?.ResetAll();
            trailSurface?.ResetTrail();
            _stamina.Reset();
            _simAccum = 0f;
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

        /// <summary>Switches the store detective live between FSM and reactive BT.</summary>
        public void ToggleGuardArchitecture()
        {
            if (_game == null)
            {
                return;
            }

            audioFloor?.PlayUiClick();
            _game.SwitchArch();
            _arch = _game.Arch;
            hud?.PushLog("AI", "Switched guard to " + (_arch == GuardArchitecture.Fsm ? "7-State FSM" : "Reactive BT"));
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
            if (mainCamera == null)
            {
                return 0f;
            }

            var visibleHalfHeight = Mathf.Max(1f, _targetOrthoSize);
            var fullWorldWidth = 2f * visibleHalfHeight * Mathf.Max(1f, mainCamera.aspect);
            return fullWorldWidth * RightPanelFrac * 0.5f;
        }

        private void SyncScene(in HeistSnapshot snapshot, bool snapCamera, float dt = 0.016f, float interpAlpha = 1f)
        {
            // Sub-tick interpolated actor positions eliminate 30 Hz step judder at 60-240 FPS
            var renderPlayer = snapCamera ? snapshot.Player : Vector2.Lerp(_prevSimPlayer, _currSimPlayer, interpAlpha);
            var renderGuard = snapCamera ? snapshot.Guard : Vector2.Lerp(_prevSimGuard, _currSimGuard, interpAlpha);
            var renderStaff = snapCamera ? snapshot.Staff : Vector2.Lerp(_prevSimStaff, _currSimStaff, interpAlpha);

            // Player animation, facing flip, idle breathing, sprint footstep puffs, T01 floor trail footprints
            var playerDelta = renderPlayer - _lastPlayer;
            var playerSpeed = dt > 1e-5f ? playerDelta.magnitude / dt : 0f;
            var playerMoving = playerSpeed > 0.12f;
            var playerBootContact = false;
            var playerOrder = Iso2DMath.DepthOrder(renderPlayer.x, renderPlayer.y, 12);
            var playerWorldFeet = Iso2DMath.TileToWorld(renderPlayer);
            if (playerSprite != null)
            {
                var pWorld = playerWorldFeet;
                var breath = playerMoving ? 0f : Mathf.Sin(_clock * 3.1f) * 0.018f;
                var jitterX = _playerStatus.JitterOffsetPx / BaseWorldPpu;
                var knock = hitFeel != null ? hitFeel.KnockbackOffsetWorld : Vector2.zero;
                playerSprite.transform.position = new Vector3(pWorld.x + jitterX + knock.x, pWorld.y + breath + knock.y, 0f);
                playerSprite.transform.localScale = new Vector3(_playerStatus.ScaleFactor.x, _playerStatus.ScaleFactor.y, 1f);
                playerSprite.sortingOrder = playerOrder;

                if (playerMoving)
                {
                    var screenDx = playerDelta.x - playerDelta.y;
                    if (Mathf.Abs(screenDx) > 0.002f)
                    {
                        playerSprite.flipX = screenDx < 0f;
                    }

                    var strideRate = _lastSprinting ? 10.5f : (_lastCrouching ? 4.8f : 7.4f);
                    _playerStrideClock += dt * strideRate;
                    var strideInt = Mathf.FloorToInt(_playerStrideClock);
                    var stepFrame = strideInt % 2;
                    if (strideInt != _prevPlayerStrideStep)
                    {
                        _prevPlayerStrideStep = strideInt;
                        playerBootContact = true;
                        if (!_lastCrouching)
                        {
                            var moveAngle = Mathf.Atan2(playerDelta.y - playerDelta.x, playerDelta.x + playerDelta.y);
                            trailSurface?.StampFootprint(pWorld, moveAngle, _lastSprinting, isGuard: false);
                        }
                    }

                    if (_lastCrouching)
                    {
                        playerSprite.sprite = stepFrame == 0
                            ? (playerCrouchWalk1Sprite ?? playerCrouchSprite)
                            : (playerCrouchWalk2Sprite ?? playerCrouchSprite);
                    }
                    else
                    {
                        playerSprite.sprite = stepFrame == 0
                            ? (playerWalk1Sprite ?? playerStandSprite)
                            : (playerWalk2Sprite ?? playerStandSprite);
                    }

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

            // T10: Bayer 4x4 see-through cutout on tall fixtures occluding the thief
            seeThrough?.Step(playerWorldFeet, playerOrder, dt);

            // T09: Heat-tier Color LUT atmosphere shift
            postFx?.SetHeatMood(snapshot.Heat, snapshot.GuardState);

            // Diegetic world channeling ring + stamina arc around thief
            if (feel != null)
            {
                var pWorld = playerWorldFeet;
                var channeling = !string.IsNullOrEmpty(snapshot.ChannelKind) && snapshot.ChannelHold > 0.01f;
                var pct = channeling ? Mathf.Clamp01(snapshot.ChannelProgress / snapshot.ChannelHold) : 0f;
                var isHazard = snapshot.ChannelKind == "vitrine" || snapshot.ChannelKind == "panel";
                feel.UpdateChannelRing(pWorld, channeling, pct, isHazard);
                feel.UpdateStaminaArc(pWorld, _stamina.Normalized, _stamina.IsWinded);
            }

            // Guard + walk cycle + damped facing angle + hysteresis flipX + animated alert bubble
            var deltaGuard = renderGuard - _lastGuard;
            var guardMoving = dt > 1e-5f && (deltaGuard.magnitude / dt) > 0.12f;
            var targetGuardAngle = snapCamera
                ? (_game != null ? _game.GuardAngle : _guardAngle)
                : Mathf.LerpAngle(_prevSimGuardAngle * Mathf.Rad2Deg, _currSimGuardAngle * Mathf.Rad2Deg, interpAlpha) * Mathf.Deg2Rad;
            if (snapCamera)
            {
                _guardAngle = targetGuardAngle;
                _guardFlipCooldown = 0f;
            }
            else
            {
                var turnT = 1f - Mathf.Exp(-10f * dt);
                _guardAngle = Mathf.LerpAngle(_guardAngle * Mathf.Rad2Deg, targetGuardAngle * Mathf.Rad2Deg, turnT) * Mathf.Deg2Rad;
                _guardFlipCooldown = Mathf.Max(0f, _guardFlipCooldown - dt);
            }

            if (deltaGuard.sqrMagnitude > 1e-6f)
            {
                _lastGuard = renderGuard;
            }

            if (guardSprite != null)
            {
                var gWorld = Iso2DMath.TileToWorld(renderGuard);
                var gBreath = guardMoving ? 0f : Mathf.Sin(_clock * 2.6f + 1.1f) * 0.016f;
                guardSprite.transform.position = new Vector3(gWorld.x, gWorld.y + gBreath, 0f);
                var guardOrder = Iso2DMath.DepthOrder(renderGuard.x, renderGuard.y, 14);
                guardSprite.sortingOrder = guardOrder;

                if (guardMoving)
                {
                    var screenDx = deltaGuard.x - deltaGuard.y;
                    if (_guardFlipCooldown <= 0f && Mathf.Abs(screenDx) > 0.015f)
                    {
                        var wantFlip = screenDx < 0f;
                        if (guardSprite.flipX != wantFlip)
                        {
                            guardSprite.flipX = wantFlip;
                            _guardFlipCooldown = 0.18f;
                        }
                    }

                    _guardStrideClock += dt * 7.0f;
                    var gStride = Mathf.FloorToInt(_guardStrideClock);
                    if (gStride != _prevGuardStrideStep)
                    {
                        _prevGuardStrideStep = gStride;
                        if (snapshot.GuardState == "chase" || snapshot.GuardState == "investigate")
                        {
                            var gAngle = Mathf.Atan2(deltaGuard.y - deltaGuard.x, deltaGuard.x + deltaGuard.y);
                            trailSurface?.StampFootprint(gWorld, gAngle, snapshot.GuardState == "chase", isGuard: true);
                        }
                    }

                    guardSprite.sprite = (gStride % 2 == 0)
                        ? (guardWalk1Sprite ?? guardStandSprite)
                        : (guardWalk2Sprite ?? guardStandSprite);
                }
                else
                {
                    // When stationary (e.g. Search sweep), face the damped vision cone cleanly with deadband hysteresis
                    var coneScreenX = Mathf.Cos(_guardAngle) - Mathf.Sin(_guardAngle);
                    if (_guardFlipCooldown <= 0f && Mathf.Abs(coneScreenX) > 0.35f)
                    {
                        var wantFlip = coneScreenX < 0f;
                        if (guardSprite.flipX != wantFlip)
                        {
                            guardSprite.flipX = wantFlip;
                            _guardFlipCooldown = 0.18f;
                        }
                    }

                    if (guardStandSprite != null)
                    {
                        guardSprite.sprite = guardStandSprite;
                    }
                }

                if (guardBubbleSprite != null)
                {
                    guardBubbleSprite.sortingOrder = guardOrder + 60;
                    var isAlert = snapshot.GuardState == "chase" || snapshot.GuardState == "attack";
                    var isSearch = snapshot.GuardState == "investigate" || snapshot.GuardState == "search";
                    var showBubble = isAlert || isSearch;
                    guardBubbleSprite.enabled = showBubble;

                    if (showBubble && !_bubbleWasShown)
                    {
                        _bubblePopTimer = 0.24f;
                    }

                    _bubbleWasShown = showBubble;
                    if (_bubblePopTimer > 0f)
                    {
                        _bubblePopTimer = Mathf.Max(0f, _bubblePopTimer - dt);
                        var u = 1f - (_bubblePopTimer / 0.24f);
                        guardBubbleSprite.transform.localScale = Vector3.one * Market2DFeel.EaseOutBack(u);
                    }
                    else
                    {
                        guardBubbleSprite.transform.localScale = Vector3.one;
                    }

                    if (isAlert && alertBubbleSprite != null)
                    {
                        guardBubbleSprite.sprite = alertBubbleSprite;
                    }
                    else if (isSearch && questionBubbleSprite != null)
                    {
                        guardBubbleSprite.sprite = questionBubbleSprite;
                    }
                }
            }

            // Staff + walk cycle
            var deltaStaff = renderStaff - _lastStaff;
            var staffMoving = dt > 1e-5f && (deltaStaff.magnitude / dt) > 0.12f;
            if (staffSprite != null)
            {
                var sWorld = Iso2DMath.TileToWorld(renderStaff);
                staffSprite.transform.position = sWorld;
                staffSprite.sortingOrder = Iso2DMath.DepthOrder(renderStaff.x, renderStaff.y, 10);

                if (staffMoving)
                {
                    var screenDx = deltaStaff.x - deltaStaff.y;
                    if (Mathf.Abs(screenDx) > 0.001f)
                    {
                        staffSprite.flipX = screenDx < 0f;
                    }

                    _staffStrideClock += dt * 6.2f;
                    staffSprite.sprite = (Mathf.FloorToInt(_staffStrideClock) % 2 == 0)
                        ? (staffWalk1Sprite ?? staffStandSprite)
                        : (staffWalk2Sprite ?? staffStandSprite);
                }
                else if (staffStandSprite != null)
                {
                    staffSprite.sprite = staffStandSprite;
                }
            }

            _lastStaff = renderStaff;

            // Guard vision fan (matches HeistGame's tier-boosted FOV and active personality profile!)
            if (guardVisionCone != null)
            {
                var coneColor = snapshot.GuardState switch
                {
                    "chase" or "attack" => new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.36f),
                    "investigate" or "search" => new Color(RisoPalette.Sun.r, RisoPalette.Sun.g, RisoPalette.Sun.b, 0.36f),
                    _ => new Color(RisoPalette.Teal.r, RisoPalette.Teal.g, RisoPalette.Teal.b, 0.26f),
                };
                var baseRange = _profile != null ? _profile.fov_distance : 5.4f;
                var range = baseRange * (1f + Mathf.Max(0, snapshot.Tier - 2) * 0.08f);
                var halfAngle = _profile != null ? _profile.fov_half_angle : 0.60f;
                guardVisionCone.SetFan(renderGuard.x, renderGuard.y, _guardAngle, halfAngle, range, coneColor, _navGrid ??= MarketGrid.Build());
            }

            // Acoustic sound wave ring & guard investigation/chase target reticle
            if (feel != null && _game != null)
            {
                feel.UpdateAcousticWave(_game.ActiveSoundOrigin, _game.ActiveSoundRadius, _game.ActiveSoundMaxRadius);
                var targetTile = _game.LastIntent.Target ?? snapshot.LastSeenPos;
                feel.UpdateGuardTargetReticle(snapshot.Guard, targetTile, snapshot.GuardState);
            }

            // CCTV sweeps
            for (var i = 0; i < cctvCones.Length && i < MarketData.Cameras.Length; i++)
            {
                var cone = cctvCones[i];
                if (cone == null)
                {
                    continue;
                }

                cone.gameObject.SetActive(!snapshot.CamerasDisabled);
                if (snapshot.CamerasDisabled)
                {
                    continue;
                }

                var def = MarketData.Cameras[i];
                var angle = (float)(def.BaseAngle + Math.Sin(_clock * def.SweepSpeed) * def.SweepAmp);
                var sighting = (snapshot.CameraSightings & (1 << i)) != 0;
                var color = sighting
                    ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.50f)
                    : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.28f);
                var outline = sighting
                    ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.98f)
                    : new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.88f);
                cone.SetFan((float)def.I, (float)def.J, angle, (float)def.HalfAngle, (float)def.Range, color, _navGrid ??= MarketGrid.Build(), outline, ignoreOriginWall: true);

                if (cctvStacks != null && i < cctvStacks.Length && cctvStacks[i] != null)
                {
                    var dirWorld = Iso2DMath.TileToWorld((float)def.I + Mathf.Cos(angle), (float)def.J + Mathf.Sin(angle), 0f)
                        - Iso2DMath.TileToWorld((float)def.I, (float)def.J, 0f);
                    cctvStacks[i].SetTurn(SpriteStack2D.VelocityToTurnDegrees(new Vector2(dirWorld.x, dirWorld.y)));
                }
            }

            // Highlight the CCTV Power Panel in r6 while cameras are online so the player can find and cut power easily
            if (cctvPanelSprite != null)
            {
                var panelMpb = new MaterialPropertyBlock();
                cctvPanelSprite.GetPropertyBlock(panelMpb);
                if (snapshot.CamerasDisabled)
                {
                    panelMpb.SetFloat("_HighlightAmount", 0f);
                    cctvPanelSprite.color = new Color(0.62f, 0.64f, 0.68f, 1f);
                }
                else
                {
                    var pPhase = 0.5f + 0.5f * Mathf.Sin(_clock * 5.2f);
                    var pCol = Color.Lerp(RisoPalette.Coral, RisoPalette.Sun, pPhase);
                    panelMpb.SetColor("_HighlightColor", pCol);
                    panelMpb.SetFloat("_HighlightAmount", snapshot.PanelInRange ? 1f : Mathf.Lerp(0.82f, 0.98f, pPhase));
                    panelMpb.SetFloat("_OutlineWidth", snapshot.PanelInRange ? 7.5f : 5.8f);
                    cctvPanelSprite.color = Color.white;
                }

                cctvPanelSprite.SetPropertyBlock(panelMpb);
            }

            // Cohesive lootable fixtures: highlight directly on the real fixture & swap to empty sprite once stolen!
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
                    var lootDef = MarketData.LootFor(lf.fixtureId);
                    var inRange = false;
                    if (!stolen && lootDef.HasValue)
                    {
                        var room = MarketData.Room(lootDef.Value.Room);
                        var fxTile = new Vector2(
                            (float)(room.OriginI + lootDef.Value.I + 0.8),
                            (float)(room.OriginJ + lootDef.Value.J + 0.6));
                        inRange = Vector2.Distance(snapshot.Player, fxTile) <= 2.7f;
                    }

                    var justStolen = lf.SyncState(stolen, snapshot.VitrineOpen, inRange, _clock, dt);
                    if (justStolen)
                    {
                        var pos = lf.transform.position;
                        var tagged = lootDef.HasValue && lootDef.Value.Tagged;
                        var dir = (Vector2)(pos - playerWorldFeet);
                        hitFeel?.TriggerHit(lf.spriteRenderer, pos + new Vector3(0f, 0.25f, 0f), dir, isKill: false, rfAccent: tagged, customStopTicks: 3);
                        feel?.EmitPickupBurst(pos, tagged);
                        audioFloor?.PlayPickup(tagged);
                        if (lootDef.HasValue)
                        {
                            var txt = "+€" + lootDef.Value.Value
                                + (lootDef.Value.VitrineGated ? " [VITRINE]" : (tagged ? " [RF]" : string.Empty));
                            hud?.SpawnFloatingPopup(pos, txt, tagged);
                        }
                    }
                }
            }

            // T15: Smooth dead-zone + velocity look-ahead camera rig / manual scout pan / fit overview + T17 whole-pixel shake
            if (mainCamera != null)
            {
                var pFocus = Iso2DMath.TileToWorld(renderPlayer, 0.6f);
                var gFocus = Iso2DMath.TileToWorld(renderGuard, 0.6f);
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
            hud.UpdateAiBrainTelemetry(FormatAiBrainTelemetry(snapshot), ActiveProfileLabel);

            // Compute next off-screen objective compass target
            if (mainCamera != null && snapshot.Outcome == HeistOutcome.None)
            {
                var (objLabel, objTile) = ResolveNextObjectiveTarget(snapshot);
                hud.UpdateCompass(mainCamera, objLabel, Iso2DMath.TileToWorld(objTile, 0.6f));
            }
            else
            {
                hud.HideCompass();
            }

            if (snapshot.ExitBlockedByTags && !_lastExitBlocked)
            {
                hud.ShowRefusal("EAS ALARM: de-tag stolen items first");
                hud.PushLog("ALARM", "EAS gate blocked by active RF tags");
                audioFloor?.PlayAlert();
                feel?.TriggerShake(0.12f, 0.25f);
                hud.TriggerAlertFlash();
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
                feel?.TriggerShake(0.08f, 0.16f);
                var spRoom = MarketData.Room("r4");
                var spPos = Iso2DMath.TileToWorld(spRoom.OriginI + 7.2f, spRoom.OriginJ + 1.0f, 1.0f);
                feel?.EmitPickupBurst(spPos, rfTagged: false);
                hud.SpawnFloatingPopup(spPos, "VITRINE OPEN", false);
                hud.PushLog("OBJ", "Forced spirits vitrine lock");
            }

            if (snapshot.CamerasDisabled && !_lastCamerasDisabled)
            {
                audioFloor?.PlayUnlock();
                var pPos = Iso2DMath.TileToWorld((float)MarketData.Panel.I, (float)MarketData.Panel.J, 1.0f);
                feel?.EmitPickupBurst(pPos, rfTagged: false);
                hud.SpawnFloatingPopup(pPos, "CCTV OFFLINE", false);
                hud.PushLog("OBJ", "Cut CCTV power panel");
            }

            if (_lastActiveTags > 0 && snapshot.ActiveTags == 0 && snapshot.BagCount > 0)
            {
                audioFloor?.PlayUnlock();
                var dPos = Iso2DMath.TileToWorld((float)MarketData.Detagger.I, (float)MarketData.Detagger.J, 1.0f);
                feel?.EmitPickupBurst(dPos, rfTagged: false);
                hud.SpawnFloatingPopup(dPos, "RF TAGS CLEARED", false);
                hud.PushLog("OBJ", "De-tagged all RF items");
            }

            if (snapshot.FirstDetectionEdge)
            {
                hud.PushLog("ALARM", "Detective spotted you");
                audioFloor?.PlayAlert();
                feel?.TriggerShake(0.11f, 0.22f);
                _playerStatus.Trigger(playerSprite, StatusFrameLook.FlashRed);
                hud.TriggerAlertFlash();
            }
            else if ((snapshot.GuardState == "chase" || snapshot.GuardState == "attack")
                     && _lastGuardState != "chase" && _lastGuardState != "attack")
            {
                hud.PushLog("ALARM", "Guard entered " + snapshot.GuardState);
                audioFloor?.PlayAlert();
                feel?.TriggerShake(0.09f, 0.18f);
                _playerStatus.Trigger(playerSprite, StatusFrameLook.Outline);
            }

            if (snapshot.CameraSightings != 0 && _lastCameraSightings == 0)
            {
                hud.PushLog("CCTV", "Picked up on security camera");
                audioFloor?.PlayAlert();
                _playerStatus.Trigger(playerSprite, StatusFrameLook.FlashRed);
                hud.TriggerAlertFlash();
            }

            var hasBackup = snapshot.BackupPhase != BackupPhase.None;
            if (hasBackup && !_lastBackup)
            {
                hud.PushLog("RADIO", "Staff called security backup");
                audioFloor?.PlayAlert();
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
                    feel?.TriggerShake(0.16f, 0.35f);
                    hud.TriggerAlertFlash();
                }
            }

            _lastExitBlocked = snapshot.ExitBlockedByTags;
            _lastVitrineOpen = snapshot.VitrineOpen;
            _lastCamerasDisabled = snapshot.CamerasDisabled;
            _lastActiveTags = snapshot.ActiveTags;
            _lastCameraSightings = snapshot.CameraSightings;
            _lastBackup = hasBackup;
            _lastGuardState = snapshot.GuardState;
            _lastOutcome = snapshot.Outcome;
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

        private static (string Label, Vector2 Tile) ResolveNextObjectiveTarget(in HeistSnapshot snapshot)
        {
            if (snapshot.StolenValue >= MarketData.Quota)
            {
                if (snapshot.ActiveTags > 0)
                {
                    return ("DE-TAGGER", new Vector2((float)MarketData.Detagger.I, (float)MarketData.Detagger.J));
                }

                return ("EAS EXIT", new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J));
            }

            var carried = new HashSet<string>(snapshot.Bag ?? Array.Empty<string>(), StringComparer.Ordinal);
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

        private void OnGUI()
        {
            var e = Event.current;
            if (e == null || !TrackedKeys.Contains(e.keyCode))
            {
                return;
            }

            if (e.type == EventType.KeyDown)
            {
                if (_guiKeys.Add(e.keyCode))
                {
                    _guiKeyDownFrame[e.keyCode] = Time.frameCount;
                }
            }
            else if (e.type == EventType.KeyUp)
            {
                _guiKeys.Remove(e.keyCode);
            }
        }

        private string FormatAiBrainTelemetry(in HeistSnapshot snapshot)
        {
            if (_game == null)
            {
                return "FSM · Standard (5.4t) | Patrol [route_assigned]";
            }

            var prof = _profile ?? _game.Profile;
            var fovStr = prof != null ? prof.fov_distance.ToString("0.0") + "t" : "5.4t";

            if (snapshot.Arch == GuardArchitecture.Fsm)
            {
                var tr = _game.LastFsmTransition;
                var ruleStr = tr.HasValue
                    ? tr.From + " -[" + tr.Guard + "]-> " + tr.To
                    : "Patrol -[route_assigned]-> Patrol";
                return "FSM · " + ActiveProfileLabel + " (" + fovStr + ") | " + ruleStr;
            }
            else
            {
                var activeLeaf = string.IsNullOrEmpty(snapshot.GuardState) ? "patrol" : snapshot.GuardState;
                var branch = "Root ▸ " + activeLeaf.ToUpperInvariant() + " [RUN]";
                var aborted = _game.LastBtAborted;
                if (aborted != null && aborted.Count > 0)
                {
                    branch += " ⚡Abort:" + aborted[0];
                }

                return "BT · " + ActiveProfileLabel + " (" + fovStr + ") | " + branch;
            }
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
            var overViewport = mousePos.x >= 0f && mousePos.x <= Screen.width * (1f - RightPanelFrac);

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

        private bool Held(Key key, KeyCode code)
        {
            if (_guiKeys.Contains(code))
            {
                return true;
            }

            var kb = Keyboard.current;
            return kb != null && kb[key].isPressed;
        }

        private bool Pressed(Key key, KeyCode code)
        {
            if (_guiKeyDownFrame.TryGetValue(code, out var frame) && frame == Time.frameCount)
            {
                return true;
            }

            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }
    }
}
