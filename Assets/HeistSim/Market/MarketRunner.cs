using System;
using System.Collections.Generic;
using HeistSim.Arena;
using TMPro;
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

        /// <summary>Orthographic half height at gameplay zoom, showing about 18 tiles vertically.</summary>
        public const float FollowOrthographicSize = 9f;

        /// <summary>Smallest orthographic half height allowed by the mouse wheel.</summary>
        public const float MinOrthographicSize = 8f;

        /// <summary>Largest orthographic half height allowed by the mouse wheel.</summary>
        public const float MaxOrthographicSize = 24f;

        /// <summary>Fixed isometric offset from the player to the camera. Positive X and Z show product fronts.</summary>
        public static readonly Vector3 CameraOffset = new Vector3(13f, 16f, 13f);

        /// <summary>Exponential smoothing rate used when the camera closes on the player.</summary>
        public const float CameraSmoothing = 9f;

        /// <summary>Keys the IMGUI reader consumes, independent of Active Input Handling.</summary>
        private static readonly HashSet<KeyCode> TrackedGuiKeys = new HashSet<KeyCode>
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.E, KeyCode.Space, KeyCode.C,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.R, KeyCode.Tab, KeyCode.B,
        };

        private readonly HashSet<KeyCode> _guiKeys = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, int> _guiKeyDownFrame = new Dictionary<KeyCode, int>();
        private readonly HashSet<string> _lastBagIds = new HashSet<string>(StringComparer.Ordinal);

        private HeistGame _game;
        private GuardProfile _profile;
        private MarketLootMarker[] _lootMarkers = Array.Empty<MarketLootMarker>();
        private GuardArchitecture _arch = GuardArchitecture.Fsm;

        private int _frame;
        private float _clock;
        private float _guardAngle = 1.5f;
        private Vector2 _lastGuard = HeistGame.GuardStart;
        private int _lastBagCount;
        private bool _lastCamerasDisabled;
        private bool _lastVitrineOpen;
        private int _lastActiveTags;
        private bool _lastBackup;
        private bool _lastExitBlocked;
        private int _lastEasAlarm;
        private int _lastCameraSightings;
        private string _lastChannelKind;
        private ChannelResult _lastChannelResult;
        private string _lastGuardState = "patrol";
        private bool _inputFallbackWarned;
        private bool _legacyInputWarned;
        private bool _imguiActiveLogged;
        private Camera _camera;
        private float _orthographicSize = FollowOrthographicSize;
        private TMP_Text _guardBubble;
        private LineRenderer _lastSeenRing;
        private Material[] _coneBaseMaterials = Array.Empty<Material>();
        private Material[] _coneAlertMaterials = Array.Empty<Material>();
        private NavigationRuntime _navigation;

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
            EnsureGuardBubble();
            EnsureLastSeenRing();
            SnapCameraCones();
            ResolveCamera();
            SnapCameraToPlayer();
            EnsureNavigation();
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
            SyncActors(snapshot);
            FollowCamera(dt);
            _navigation?.Refresh(snapshot, _camera, dt);

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
            _lastBagCount = 0;
            _lastCamerasDisabled = false;
            _lastVitrineOpen = false;
            _lastActiveTags = 0;
            _lastBackup = false;
            _lastExitBlocked = false;
            _lastEasAlarm = 0;
            _lastCameraSightings = 0;
            _lastBagIds.Clear();
            _lastChannelKind = null;
            _lastChannelResult = ChannelResult.None;
            _lastGuardState = "patrol";
            _orthographicSize = FollowOrthographicSize;
            if (hud != null)
            {
                hud.ClearFeed();
                hud.HidePrompt();
            }

            SyncActors(_game.Snapshot());
            SnapCameraToPlayer();
            _navigation?.ResetState();
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
            hud.Refresh(snapshot, hint, 0f);
            ShowPrompt(snapshot, hint);
            TrackSignals(snapshot);
        }

        private void ShowPrompt(in HeistSnapshot snapshot, in MarketActionHint hint)
        {
            if (!hint.Found || _camera == null || string.IsNullOrEmpty(hint.Prompt))
            {
                hud.HidePrompt();
                return;
            }

            var world = new Vector3(hint.Target.x, 1.1f, hint.Target.y);
            var screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                hud.HidePrompt();
                return;
            }

            hud.ShowPrompt(screen, hint.Prompt);
        }

        private void TrackSignals(in HeistSnapshot snapshot)
        {
            if (snapshot.ExitBlockedByTags && !_lastExitBlocked)
            {
                hud.ShowRefusal("EAS ALARM de-tag first");
            }

            switch (snapshot.LastInteraction)
            {
                case InteractOutcome.BagFull:
                    hud.ShowRefusal("BAG FULL");
                    break;
                case InteractOutcome.BlockedLineOfSight:
                    hud.ShowRefusal("BLOCKED (LOS)");
                    break;
                case InteractOutcome.PanelOutOfReach:
                    hud.ShowRefusal("PANEL OUT OF REACH");
                    break;
            }

            if (snapshot.FirstDetectionEdge)
            {
                hud.FlashDetection();
                hud.PushFeed("ALARM", "Spotted");
            }

            var alarmedGuard = snapshot.GuardState == "chase" || snapshot.GuardState == "attack";
            var wasAlarmed = _lastGuardState == "chase" || _lastGuardState == "attack";
            if (alarmedGuard && !wasAlarmed)
            {
                hud.PushFeed("ALARM", "Guard is alert");
            }

            if (snapshot.EasAlarmTicks > 0 && _lastEasAlarm == 0)
            {
                hud.PushFeed("ALARM", "EAS alarm, de-tag first");
            }

            if (snapshot.CameraSightings != 0 && _lastCameraSightings == 0)
            {
                hud.PushFeed("CCTV", "Camera sighting");
            }

            var hasBackup = snapshot.HasBackup;
            if (hasBackup && !_lastBackup)
            {
                hud.PushFeed("BACKUP", "Backup dispatched");
            }

            if (snapshot.BagCount > _lastBagCount && snapshot.Bag != null)
            {
                for (var i = 0; i < snapshot.Bag.Count; i++)
                {
                    var id = snapshot.Bag[i];
                    if (_lastBagIds.Contains(id))
                    {
                        continue;
                    }

                    var loot = MarketData.LootFor(id);
                    hud.PushFeed("STEAL", "Stolen " + (loot.HasValue ? loot.Value.Label : id));
                }
            }

            if (_lastChannelResult == ChannelResult.Running && snapshot.LastChannelResult == ChannelResult.Cancelled)
            {
                var kind = string.IsNullOrEmpty(_lastChannelKind) ? "action" : _lastChannelKind;
                hud.PushFeed("ALARM", "Channel cancelled: " + kind);
            }

            if (_lastCamerasDisabled != snapshot.CamerasDisabled && snapshot.CamerasDisabled)
            {
                hud.PushFeed("CCTV", "CCTV power cut");
            }

            if (_lastVitrineOpen != snapshot.VitrineOpen && snapshot.VitrineOpen)
            {
                hud.PushFeed("STEAL", "Vitrine open");
            }

            if (_lastActiveTags > 0 && snapshot.ActiveTags == 0)
            {
                hud.PushFeed("CCTV", "Tags cleared");
            }

            _lastBagCount = snapshot.BagCount;
            _lastBagIds.Clear();
            if (snapshot.Bag != null)
            {
                foreach (var id in snapshot.Bag)
                {
                    _lastBagIds.Add(id);
                }
            }

            _lastCamerasDisabled = snapshot.CamerasDisabled;
            _lastVitrineOpen = snapshot.VitrineOpen;
            _lastActiveTags = snapshot.ActiveTags;
            _lastEasAlarm = snapshot.EasAlarmTicks;
            _lastBackup = hasBackup;
            _lastExitBlocked = snapshot.ExitBlockedByTags;
            _lastCameraSightings = snapshot.CameraSightings;
            _lastChannelKind = snapshot.ChannelKind;
            _lastChannelResult = snapshot.LastChannelResult;
            _lastGuardState = snapshot.GuardState;
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

            var state = snapshot.GuardState;
            var alarmed = state == "chase" || state == "attack";
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
                visionFan.Apply(_guardAngle, _profile.fov_half_angle, _profile.fov_distance, state);
            }

            UpdateGuardBubble(snapshot);
            SyncCameraCones(snapshot);
            SyncLoot(snapshot);
            _lastGuard = snapshot.Guard;
        }

        private void SyncCameraCones(in HeistSnapshot snapshot)
        {
            if (cameraCones == null)
            {
                return;
            }

            EnsureConeMaterials();
            var count = Mathf.Min(cameraCones.Length, MarketData.Cameras.Length);
            for (var i = 0; i < count; i++)
            {
                var cone = cameraCones[i];
                if (cone == null)
                {
                    continue;
                }

                var def = MarketData.Cameras[i];
                var boost = 1f + Mathf.Max(0, snapshot.Tier - 1) * 0.08f;
                var angle = def.BaseAngle + Math.Sin(_clock * def.SweepSpeed * boost + def.I) * def.SweepAmp;
                cone.rotation = Quaternion.Euler(0f, -(float)angle * Mathf.Rad2Deg, 0f);
                cone.localScale = new Vector3((float)def.Range, 1f, (float)def.Range);

                if (i < _coneBaseMaterials.Length)
                {
                    var renderer = cone.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = snapshot.CameraSees(i)
                            ? _coneAlertMaterials[i]
                            : _coneBaseMaterials[i];
                    }
                }
            }

            UpdateLastSeenRing(snapshot.LastSeenPos);
        }

        private void EnsureConeMaterials()
        {
            if (_coneBaseMaterials.Length == cameraCones.Length)
            {
                return;
            }

            _coneBaseMaterials = new Material[cameraCones.Length];
            _coneAlertMaterials = new Material[cameraCones.Length];
            for (var i = 0; i < cameraCones.Length; i++)
            {
                var cone = cameraCones[i];
                if (cone == null)
                {
                    continue;
                }

                var renderer = cone.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                _coneBaseMaterials[i] = renderer.sharedMaterial;
                var source = renderer.sharedMaterial != null
                    ? renderer.sharedMaterial
                    : new Material(Shader.Find("Sprites/Default"));
                _coneAlertMaterials[i] = new Material(source)
                {
                    name = "MarketConeCoral (Instance)",
                    color = ArenaSpec.Coral,
                };
            }
        }

        private void EnsureGuardBubble()
        {
            if (_guardBubble != null || guardRoot == null)
            {
                return;
            }

            var bubble = new GameObject("Guard Alert Bubble");
            bubble.transform.SetParent(guardRoot, false);
            bubble.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            var label = bubble.AddComponent<TextMeshPro>();
            var font = Resources.Load<TMP_FontAsset>("MarketSans");
            if (font != null)
            {
                label.font = font;
            }

            label.fontSize = 2.6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = ArenaSpec.Sun;
            label.text = string.Empty;
            label.rectTransform.sizeDelta = new Vector2(2.6f, 1.4f);
            label.sortingOrder = 20;
            label.gameObject.SetActive(false);
            _guardBubble = label;
        }

        private void UpdateGuardBubble(in HeistSnapshot snapshot)
        {
            if (_guardBubble == null)
            {
                return;
            }

            var state = snapshot.GuardState;
            string mark;
            switch (state)
            {
                case "investigate":
                case "search":
                    mark = "?";
                    break;
                case "chase":
                    mark = "!";
                    break;
                case "attack":
                    mark = "!!";
                    break;
                default:
                    mark = string.Empty;
                    break;
            }

            var visible = mark.Length > 0;
            if (_guardBubble.gameObject.activeSelf != visible)
            {
                _guardBubble.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            _guardBubble.text = mark;
            var tint = GuardVisionFan.TintFor(state);
            _guardBubble.color = tint;
            if (_camera != null)
            {
                _guardBubble.transform.rotation = _camera.transform.rotation;
            }
        }

        private void EnsureLastSeenRing()
        {
            if (_lastSeenRing != null)
            {
                return;
            }

            var ring = new GameObject("Last Seen Ring").AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = false;
            ring.positionCount = 24;
            ring.startWidth = 0.09f;
            ring.endWidth = 0.09f;
            ring.material = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Last Seen Ring (Instance)",
                color = ArenaSpec.CoralInk,
            };
            ring.startColor = ArenaSpec.CoralInk;
            ring.endColor = ArenaSpec.CoralInk;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
            _lastSeenRing = ring;
        }

        private void UpdateLastSeenRing(Vector2? position)
        {
            EnsureLastSeenRing();
            if (!position.HasValue)
            {
                _lastSeenRing.enabled = false;
                return;
            }

            const int dashes = 12;
            const float radius = 0.8f;
            var centre = new Vector3(position.Value.x, 0.1f, position.Value.y);
            _lastSeenRing.positionCount = dashes * 2;
            for (var dash = 0; dash < dashes; dash++)
            {
                var start = dash / (float)dashes * Mathf.PI * 2f;
                var end = start + Mathf.PI * 2f / dashes * 0.6f;
                _lastSeenRing.SetPosition(dash * 2, centre + new Vector3(Mathf.Cos(start) * radius, 0f, Mathf.Sin(start) * radius));
                _lastSeenRing.SetPosition(dash * 2 + 1, centre + new Vector3(Mathf.Cos(end) * radius, 0f, Mathf.Sin(end) * radius));
            }

            _lastSeenRing.enabled = true;
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

                var loot = MarketData.LootFor(marker.lootId);
                var tagged = loot.HasValue && loot.Value.Tagged && snapshot.ActiveTags > 0 && !stolen;
                marker.SetTagVisible(tagged);
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

        private void ResolveCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                _camera = FindAnyObjectByType<Camera>();
            }
        }

        private void SnapCameraToPlayer()
        {
            if (_camera == null || playerRoot == null)
            {
                return;
            }

            _camera.orthographic = true;
            _camera.orthographicSize = _orthographicSize;
            _camera.transform.position = playerRoot.position + CameraOffset;
            _camera.transform.rotation = Quaternion.LookRotation(-CameraOffset.normalized, Vector3.up);
        }

        private void FollowCamera(float dt)
        {
            if (_camera == null || playerRoot == null)
            {
                return;
            }

            var target = playerRoot.position + CameraOffset;
            _camera.transform.position = CameraFollowMath.Smooth(
                _camera.transform.position, target, dt, CameraSmoothing, MaxDeltaTime);
            _camera.transform.rotation = Quaternion.LookRotation(-CameraOffset.normalized, Vector3.up);
        }

        private void EnsureNavigation()
        {
            _navigation ??= new NavigationRuntime(transform);
        }

        private void OnDestroy()
        {
            _navigation?.Dispose();
            _navigation = null;
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

        /// <summary>Collects key events through IMGUI, which works in either Active Input Handling mode.</summary>
        private void OnGUI()
        {
            var current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.ScrollWheel)
            {
                var wheel = Mathf.Clamp(current.delta.y, -3f, 3f);
                _orthographicSize = Mathf.Clamp(
                    _orthographicSize + wheel * 0.5f, MinOrthographicSize, MaxOrthographicSize);
                if (_camera != null)
                {
                    _camera.orthographicSize = _orthographicSize;
                }

                current.Use();
                return;
            }

            if (current.type == EventType.KeyDown)
            {
                var key = current.keyCode;
                if (!IsTrackedGuiKey(key))
                {
                    return;
                }

                if (_guiKeys.Add(key))
                {
                    _guiKeyDownFrame[key] = Time.frameCount;
                }

                current.Use();
                return;
            }

            if (current.type == EventType.KeyUp)
            {
                var key = current.keyCode;
                if (!IsTrackedGuiKey(key))
                {
                    return;
                }

                _guiKeys.Remove(key);
                current.Use();
            }
        }

        private static bool IsTrackedGuiKey(KeyCode key)
        {
            return TrackedGuiKeys.Contains(key);
        }

        private bool GuiHeld(KeyCode key)
        {
            return _guiKeys.Contains(key);
        }

        private bool GuiPressed(KeyCode key)
        {
            return _guiKeyDownFrame.TryGetValue(key, out var frame) && frame >= Time.frameCount - 1;
        }

        private bool HasRecentGuiEdge()
        {
            return GuiPressed(KeyCode.R) || GuiPressed(KeyCode.Tab) || GuiPressed(KeyCode.B);
        }

        /// <summary>Maps screen-relative WASD onto world tile movement using the camera ground axes.</summary>
        public static Vector2 MapCameraInput(Vector2 cameraRight, Vector2 cameraUp, float right, float forward)
        {
            var r = cameraRight.sqrMagnitude < 1e-6f ? new Vector2(1f, -1f).normalized : cameraRight.normalized;
            var u = cameraUp.sqrMagnitude < 1e-6f ? new Vector2(-1f, -1f).normalized : cameraUp.normalized;
            var v = r * right + u * forward;
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        private Vector2 CameraInput(float right, float forward)
        {
            if (_camera == null)
            {
                return MapCameraInput(default, default, right, forward);
            }

            return MapCameraInput(
                new Vector2(_camera.transform.right.x, _camera.transform.right.z),
                new Vector2(_camera.transform.up.x, _camera.transform.up.z),
                right,
                forward);
        }

        private void ReadKeys(out HeistInput input, out bool reset, out bool toggle)
        {
            if (_guiKeys.Count > 0 || HasRecentGuiEdge())
            {
                var right = (GuiHeld(KeyCode.D) || GuiHeld(KeyCode.RightArrow) ? 1f : 0f)
                    - (GuiHeld(KeyCode.A) || GuiHeld(KeyCode.LeftArrow) ? 1f : 0f);
                var forward = (GuiHeld(KeyCode.W) || GuiHeld(KeyCode.UpArrow) ? 1f : 0f)
                    - (GuiHeld(KeyCode.S) || GuiHeld(KeyCode.DownArrow) ? 1f : 0f);
                var dir = CameraInput(right, forward);
                input = new HeistInput(
                    dir.x,
                    dir.y,
                    GuiHeld(KeyCode.E) || GuiHeld(KeyCode.Space),
                    GuiHeld(KeyCode.C),
                    GuiHeld(KeyCode.LeftShift) || GuiHeld(KeyCode.RightShift));
                reset = GuiPressed(KeyCode.R);
                toggle = GuiPressed(KeyCode.Tab) || GuiPressed(KeyCode.B);
                if (!_imguiActiveLogged)
                {
                    _imguiActiveLogged = true;
                    Debug.Log("MarketRunner: using IMGUI keyboard input. Click the Game view once to give it focus before using WASD.");
                }

                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var right = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                var forward = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                var dir = CameraInput(right, forward);
                input = new HeistInput(
                    dir.x,
                    dir.y,
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
                var right = (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                var forward = (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                var dir = CameraInput(right, forward);
                input = new HeistInput(
                    dir.x,
                    dir.y,
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
