using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeistSim.Market
{
    /// <summary>Owns one market game, reads the keyboard, steps the sim and syncs the scene transforms and HUD.</summary>
    public sealed class MarketRunner : MonoBehaviour
    {
        /// <summary>Player actor root, moved from the snapshot.</summary>
        public Transform playerRoot;

        /// <summary>Store detective actor root, moved and turned from the snapshot.</summary>
        public Transform guardRoot;

        /// <summary>Staff actor root, moved and turned from the snapshot.</summary>
        public Transform staffRoot;

        /// <summary>Field of view fan that follows the store detective.</summary>
        public GuardVisionFan visionFan;

        /// <summary>HUD that renders the shift state.</summary>
        public MarketHUD hud;

        /// <summary>Vision cones of the six CCTV rigs, in camera declaration order.</summary>
        public Transform[] cameraCones = Array.Empty<Transform>();

        /// <summary>Maximum simulation step in seconds, matching the browser game clamp.</summary>
        public const float MaxDeltaTime = 0.05f;

        /// <summary>HUD refresh interval in frames.</summary>
        public const int HudFrameInterval = 6;

        private HeistGame _game;
        private GuardProfile _profile;
        private MarketLootMarker[] _lootMarkers = Array.Empty<MarketLootMarker>();
        private GuardArchitecture _arch = GuardArchitecture.Fsm;

        private int _frame;
        private float _clock;
        private float _guardAngle = 1.5f;
        private Vector2 _lastGuard = HeistGame.GuardStart;
        private float _actionHold;
        private int _lastBagCount;
        private bool _lastCamerasDisabled;
        private bool _lastVitrineOpen;
        private bool _inputFallbackWarned;
        private bool _legacyInputWarned;

        /// <summary>The live game, one per shift.</summary>
        public HeistGame Game => _game;

        /// <summary>Latest snapshot of the live game.</summary>
        public HeistSnapshot Snapshot => _game.Snapshot();

        private void Awake()
        {
            _profile = GuardProfile.CreateDefault("standard");
            _game = new HeistGame(_profile, GuardArchitecture.Fsm);
            _arch = _game.Arch;
            _lastGuard = _game.Snapshot().Guard;

            ResolveBindings();
            SnapCameraCones();
        }

        private void Update()
        {
            _game ??= CreateGame();
            ReadKeys(out var input, out var reset, out var toggle);

            if (reset)
            {
                ResetShift();
                RefreshHud();
                return;
            }

            if (toggle)
            {
                _game.SwitchArch();
                _arch = _game.Arch;
            }

            var dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            if (_game.Outcome == HeistOutcome.None && dt > 0f)
            {
                _clock += dt;
            }

            _game.Step(dt, input);
            var snapshot = _game.Snapshot();
            TrackAction(dt, input, snapshot);
            SyncActors(snapshot);

            if (_frame % HudFrameInterval == 0 || toggle)
            {
                RefreshHud();
            }

            _frame++;
        }

        /// <summary>Restores the start state with a fresh game, matching the browser reset.</summary>
        public void ResetShift()
        {
            _game = CreateGame();
            _arch = _game.Arch;
            _clock = 0f;
            _guardAngle = 1.5f;
            _lastGuard = _game.Snapshot().Guard;
            _actionHold = 0f;
            _lastBagCount = 0;
            _lastCamerasDisabled = false;
            _lastVitrineOpen = false;
            SyncActors(_game.Snapshot());
        }

        private HeistGame CreateGame()
        {
            _profile ??= GuardProfile.CreateDefault("standard");
            return new HeistGame(_profile, HeistArchitecture());
        }

        private GuardArchitecture HeistArchitecture()
        {
            return _arch;
        }

        private void RefreshHud()
        {
            if (hud == null)
            {
                return;
            }

            var snapshot = _game.Snapshot();
            var hint = MarketInteractions.Probe(snapshot);
            hud.Refresh(snapshot, hint, MarketInteractions.Progress(_actionHold, hint));
        }

        private void TrackAction(float dt, in HeistInput input, in HeistSnapshot snapshot)
        {
            if (input.Action && snapshot.Outcome == HeistOutcome.None)
            {
                _actionHold += dt;
            }
            else
            {
                _actionHold = 0f;
            }

            if (snapshot.BagCount != _lastBagCount
                || snapshot.CamerasDisabled != _lastCamerasDisabled
                || snapshot.VitrineOpen != _lastVitrineOpen)
            {
                _actionHold = 0f;
                _lastBagCount = snapshot.BagCount;
                _lastCamerasDisabled = snapshot.CamerasDisabled;
                _lastVitrineOpen = snapshot.VitrineOpen;
            }
        }

        private void SyncActors(in HeistSnapshot snapshot)
        {
            if (playerRoot != null)
            {
                playerRoot.position = new Vector3(snapshot.Player.x, 0f, snapshot.Player.y);
            }

            if (staffRoot != null)
            {
                staffRoot.position = new Vector3(snapshot.Staff.x, 0f, snapshot.Staff.y);
            }

            var alarmed = snapshot.GuardState == "chase" || snapshot.GuardState == "attack";
            if (alarmed)
            {
                _guardAngle = Mathf.Atan2(snapshot.Player.y - snapshot.Guard.y, snapshot.Player.x - snapshot.Guard.x);
            }
            else
            {
                var delta = snapshot.Guard - _lastGuard;
                if (delta.sqrMagnitude > 0.000001f)
                {
                    _guardAngle = Mathf.Atan2(delta.y, delta.x);
                }
            }

            if (guardRoot != null)
            {
                guardRoot.position = new Vector3(snapshot.Guard.x, 0f, snapshot.Guard.y);
                guardRoot.rotation = Quaternion.Euler(0f, -_guardAngle * Mathf.Rad2Deg, 0f);
            }

            if (visionFan != null)
            {
                visionFan.transform.position = new Vector3(snapshot.Guard.x, 0.06f, snapshot.Guard.y);
                visionFan.Apply(_guardAngle, _profile.fov_half_angle, _profile.fov_distance, alarmed);
            }

            SyncCameraCones(snapshot.Tier);
            SyncLoot(snapshot);
            _lastGuard = snapshot.Guard;
        }

        private void SyncCameraCones(int tier)
        {
            if (cameraCones == null)
            {
                return;
            }

            var count = Mathf.Min(cameraCones.Length, MarketData.Cameras.Length);
            for (var i = 0; i < count; i++)
            {
                var cone = cameraCones[i];
                if (cone == null)
                {
                    continue;
                }

                var def = MarketData.Cameras[i];
                var boost = 1f + Mathf.Max(0, tier - 1) * 0.08f;
                var angle = def.BaseAngle + Math.Sin(_clock * def.SweepSpeed * boost + def.I) * def.SweepAmp;
                cone.rotation = Quaternion.Euler(0f, -(float)angle * Mathf.Rad2Deg, 0f);
                cone.localScale = new Vector3((float)def.Range, 1f, (float)def.Range);
            }
        }

        private void SyncLoot(in HeistSnapshot snapshot)
        {
            foreach (var marker in _lootMarkers)
            {
                if (marker == null)
                {
                    continue;
                }

                var stolen = MarketLayout.InBag(snapshot.Bag, marker.lootId);
                if (marker.gameObject.activeSelf == stolen)
                {
                    marker.gameObject.SetActive(!stolen);
                }
            }
        }

        private void ResolveBindings()
        {
            if (playerRoot == null)
            {
                playerRoot = FindDeep(transform, "Player");
            }

            if (guardRoot == null)
            {
                guardRoot = FindDeep(transform, "Guard");
            }

            if (staffRoot == null)
            {
                staffRoot = FindDeep(transform, "Staff");
            }

            if (visionFan == null)
            {
                visionFan = GetComponentInChildren<GuardVisionFan>(true);
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<MarketHUD>();
            }

            _lootMarkers = FindObjectsByType<MarketLootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void SnapCameraCones()
        {
            if (cameraCones != null && cameraCones.Length > 0)
            {
                return;
            }

            var group = FindDeep(transform, MarketSceneSpec.CamerasGroup);
            if (group == null)
            {
                return;
            }

            var cones = new Transform[MarketData.Cameras.Length];
            for (var i = 0; i < cones.Length; i++)
            {
                if (i < group.childCount)
                {
                    cones[i] = group.GetChild(i).Find(MarketSceneSpec.ConeChild);
                }
            }

            cameraCones = cones;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void ReadKeys(out HeistInput input, out bool reset, out bool toggle)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var i = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                var j = (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f);
                input = new HeistInput(
                    i,
                    j,
                    keyboard.eKey.isPressed || keyboard.spaceKey.isPressed,
                    keyboard.cKey.isPressed,
                    keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                reset = keyboard.rKey.wasPressedThisFrame;
                toggle = keyboard.tabKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame;
                return;
            }

            if (!_inputFallbackWarned)
            {
                _inputFallbackWarned = true;
                Debug.LogWarning(
                    "MarketRunner: no Input System keyboard device. Trying the legacy key reader. "
                    + "If this repeats with an exception, set Active Input Handling to Both in Player Settings.");
            }

            try
            {
                var li = (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                var lj = (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow) ? 1f : 0f)
                    - (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow) ? 1f : 0f);
                input = new HeistInput(
                    li,
                    lj,
                    UnityEngine.Input.GetKey(KeyCode.E) || UnityEngine.Input.GetKey(KeyCode.Space),
                    UnityEngine.Input.GetKey(KeyCode.C),
                    UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift));
                reset = UnityEngine.Input.GetKeyDown(KeyCode.R);
                toggle = UnityEngine.Input.GetKeyDown(KeyCode.Tab) || UnityEngine.Input.GetKeyDown(KeyCode.B);
            }
            catch (InvalidOperationException)
            {
                if (!_legacyInputWarned)
                {
                    _legacyInputWarned = true;
                    Debug.LogWarning(
                        "MarketRunner: legacy input is disabled in Player Settings. No keyboard input is available. "
                        + "Set Active Input Handling to Both and restart the editor.");
                }

                input = default;
                reset = false;
                toggle = false;
            }
        }
    }
}
