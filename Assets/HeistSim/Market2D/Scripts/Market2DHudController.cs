using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Runtime UI Toolkit controller for the split-screen Frischemarkt editorial HUD.
    /// Reads HeistSnapshot and drives smooth animated counters, Bayer heat meter,
    /// interactive 12-department minimap, off-screen objective compass, floating
    /// world gain popups, shift journal, and contextual left-viewport prompts.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Market2DHudController : MonoBehaviour
    {
        private const int LogCapacity = 3;
        private const float RefusalDuration = 1.6f;
        private const float AlertFlashDuration = 0.28f;
        private const float PopupLifetime = 0.88f;

        private sealed class FloatingPopup
        {
            public Label Element;
            public float StartX;
            public float StartY;
            public float Elapsed;
        }

        private readonly List<(string tag, string msg)> _logs = new();
        private readonly List<FloatingPopup> _popups = new();
        private readonly Dictionary<string, VisualElement> _roomCells = new();
        private readonly HashSet<string> _bagLookup = new(StringComparer.Ordinal);
        private readonly HashSet<string> _roomsWithLoot = new(StringComparer.Ordinal);

        private UIDocument _doc;
        private Market2DRunner _runner;
        private VisualElement _root;
        private VisualElement _viewportOverlay;
        private VisualElement _alertVignette;
        private VisualElement _popupLayer;
        private VisualElement _compassPill;
        private Label _compassArrow;
        private Label _compassLabel;

        private Label _zoneName;
        private Label _lootBig;
        private Label _lootQuota;
        private VisualElement _guardDot;
        private Label _guardChipText;
        private Label _statBag;
        private Label _statTags;
        private Label _statCctv;
        private Label _statRadio;
        private Label _heatTierLabel;
        private VisualElement _heatFill;

        private VisualElement[] _objBoxes;
        private Label[] _objTexts;

        private Button _btnSwitchAi;
        private Button _btnTrait;
        private Button _btnReset;
        private Button _btnZoomIn;
        private Button _btnZoomOut;
        private Button _btnZoomFit;
        private Button _btnMute;
        private Label _zoomReadout;
        private Label _aiTraceLine;

        private VisualElement _refusalBanner;
        private Label _refusalText;
        private VisualElement _promptWrap;
        private Label _promptLabel;
        private VisualElement _channelTrack;
        private VisualElement _channelFill;

        private VisualElement _outcomeOverlay;
        private Label _outcomeTitle;
        private Label _outcomeSub;

        private VisualElement _pipPlayer;
        private VisualElement _pipGuard;
        private VisualElement _pipStaff;
        private VisualElement _pipExit;

        private VisualElement[] _logRows;
        private Label[] _logTags;
        private Label[] _logMsgs;

        private float _refusalTimer;
        private float _alertFlashTimer;
        private float _displayLoot;
        private float _targetLoot;
        private float _displayHeatPct = 4f;
        private float _targetHeatPct = 4f;
        private string _activeRoomId;

        /// <summary>Root visual element for tests and capture tools.</summary>
        public VisualElement Root => _root;

        private void OnEnable()
        {
            BindElements();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;

            if (_refusalTimer > 0f)
            {
                _refusalTimer -= dt;
                if (_refusalTimer <= 0f && _refusalBanner != null)
                {
                    _refusalBanner.AddToClassList("is-hidden");
                }
            }

            if (_alertFlashTimer > 0f)
            {
                _alertFlashTimer = Mathf.Max(0f, _alertFlashTimer - dt);
                if (_alertVignette != null)
                {
                    if (_alertFlashTimer <= 0f)
                    {
                        _alertVignette.AddToClassList("is-hidden");
                        _alertVignette.style.opacity = 1f;
                    }
                    else
                    {
                        _alertVignette.style.opacity = Market2DFeel.EaseOutCubic(_alertFlashTimer / AlertFlashDuration);
                    }
                }
            }

            // Smooth loot counter count-up & smooth heat bar tween (craft-game-floors: numbers animate, never snap)
            if (Mathf.Abs(_displayLoot - _targetLoot) > 0.1f)
            {
                var stepT = 1f - Mathf.Exp(-14f * dt);
                _displayLoot = Mathf.Lerp(_displayLoot, _targetLoot, stepT);
                if (Mathf.Abs(_displayLoot - _targetLoot) < 0.35f)
                {
                    _displayLoot = _targetLoot;
                }

                if (_lootBig != null)
                {
                    _lootBig.text = "€" + Mathf.RoundToInt(_displayLoot);
                }
            }

            if (Mathf.Abs(_displayHeatPct - _targetHeatPct) > 0.05f)
            {
                var stepT = 1f - Mathf.Exp(-10f * dt);
                _displayHeatPct = Mathf.Lerp(_displayHeatPct, _targetHeatPct, stepT);
                if (_heatFill != null)
                {
                    _heatFill.style.width = Length.Percent(Mathf.Clamp(_displayHeatPct, 4f, 100f));
                }
            }

            // Advance rising floating world popups (T17: 46-tick hop + drift + last-6-tick blink-out)
            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                p.Elapsed += dt;
                var tick = Mathf.FloorToInt(p.Elapsed * 60f);
                if (tick >= 46)
                {
                    p.Element.RemoveFromHierarchy();
                    _popups.RemoveAt(i);
                    continue;
                }

                var hop = Mathf.Min(tick / 10f, 1f);
                var risePx = (24f * (1f - (1f - hop) * (1f - hop)))
                    + (hop < 1f ? 6f * Mathf.Sin(Mathf.PI * hop) : 0f)
                    + tick * 0.35f;
                p.Element.style.left = p.StartX;
                p.Element.style.top = p.StartY - Mathf.Round(risePx);
                var visible = tick < 40 || (tick % 2 == 0);
                p.Element.style.opacity = visible ? 1f : 0f;
            }
        }

        /// <summary>Connects button callbacks to the live 2D market runner.</summary>
        public void BindRunner(Market2DRunner runner)
        {
            _runner = runner;
            if (_root == null)
            {
                BindElements();
            }
        }

        /// <summary>Binds UXML elements and hooks up button callbacks. Safe to call in edit mode for captures.</summary>
        public void BindElements()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null)
            {
                return;
            }

            _root = _doc.rootVisualElement;
            _viewportOverlay = _root.Q<VisualElement>("viewport-overlay");
            _alertVignette = _root.Q<VisualElement>("alert-vignette");
            _popupLayer = _root.Q<VisualElement>("popup-layer");
            _compassPill = _root.Q<VisualElement>("compass-pill");
            _compassArrow = _root.Q<Label>("compass-arrow");
            _compassLabel = _root.Q<Label>("compass-label");

            _zoneName = _root.Q<Label>("zone-name");
            _lootBig = _root.Q<Label>("loot-big");
            _lootQuota = _root.Q<Label>("loot-quota");
            _guardDot = _root.Q<VisualElement>("guard-dot");
            _guardChipText = _root.Q<Label>("guard-chip-text");
            _statBag = _root.Q<Label>("stat-bag");
            _statTags = _root.Q<Label>("stat-tags");
            _statCctv = _root.Q<Label>("stat-cctv");
            _statRadio = _root.Q<Label>("stat-radio");
            _heatTierLabel = _root.Q<Label>("heat-tier-label");
            _heatFill = _root.Q<VisualElement>("heat-fill");

            _objBoxes = new VisualElement[5];
            _objTexts = new Label[5];
            for (var i = 0; i < 5; i++)
            {
                _objBoxes[i] = _root.Q<VisualElement>("obj-box-" + i);
                _objTexts[i] = _root.Q<Label>("obj-text-" + i);
            }

            _btnSwitchAi = _root.Q<Button>("btn-switch-ai");
            _btnTrait = _root.Q<Button>("btn-trait");
            _btnReset = _root.Q<Button>("btn-reset");
            _btnZoomIn = _root.Q<Button>("btn-zoom-in");
            _btnZoomOut = _root.Q<Button>("btn-zoom-out");
            _btnZoomFit = _root.Q<Button>("btn-zoom-fit");
            _btnMute = _root.Q<Button>("btn-mute");
            _zoomReadout = _root.Q<Label>("zoom-readout");
            _aiTraceLine = _root.Q<Label>("ai-trace-line");

            RebindButton(_btnSwitchAi, OnSwitchAi);
            RebindButton(_btnTrait, OnCycleTrait);
            RebindButton(_btnReset, OnReset);
            RebindButton(_btnZoomIn, OnZoomIn);
            RebindButton(_btnZoomOut, OnZoomOut);
            RebindButton(_btnZoomFit, OnZoomFit);
            RebindButton(_btnMute, OnToggleMute);

            _refusalBanner = _root.Q<VisualElement>("refusal-banner");
            _refusalText = _root.Q<Label>("refusal-text");
            _promptWrap = _root.Q<VisualElement>("prompt-wrap");
            _promptLabel = _root.Q<Label>("prompt-label");
            _channelTrack = _root.Q<VisualElement>("channel-track");
            _channelFill = _root.Q<VisualElement>("channel-fill");

            _outcomeOverlay = _root.Q<VisualElement>("outcome-overlay");
            _outcomeTitle = _root.Q<Label>("outcome-title");
            _outcomeSub = _root.Q<Label>("outcome-sub");

            _roomCells.Clear();
            foreach (var rm in MarketData.Rooms)
            {
                var cell = _root.Q<VisualElement>("mm-" + rm.Id);
                if (cell != null)
                {
                    _roomCells[rm.Id] = cell;
                    var capturedId = rm.Id;
                    cell.RegisterCallback<ClickEvent>(_ => _runner?.FocusRoom(capturedId));
                }
            }

            _pipPlayer = _root.Q<VisualElement>("pip-player");
            _pipGuard = _root.Q<VisualElement>("pip-guard");
            _pipStaff = _root.Q<VisualElement>("pip-staff");
            _pipExit = _root.Q<VisualElement>("pip-exit");

            _logRows = new VisualElement[LogCapacity];
            _logTags = new Label[LogCapacity];
            _logMsgs = new Label[LogCapacity];
            for (var i = 0; i < LogCapacity; i++)
            {
                _logRows[i] = _root.Q<VisualElement>("log-row-" + i);
                _logTags[i] = _root.Q<Label>("log-tag-" + i);
                _logMsgs[i] = _root.Q<Label>("log-msg-" + i);
            }

            if (_logs.Count == 0)
            {
                ResetFeed();
            }
            else
            {
                RenderLogs();
            }
        }

        /// <summary>Updates all HUD fields from the latest simulation snapshot and interaction probe.</summary>
        public void Refresh(in HeistSnapshot snapshot, in MarketActionHint hint)
        {
            if (_root == null)
            {
                BindElements();
                if (_root == null)
                {
                    return;
                }
            }

            var room = Iso2DMath.RoomAt(snapshot.Player);
            if (_zoneName != null)
            {
                _zoneName.text = room.Name;
            }

            _targetLoot = snapshot.StolenValue;
            if (!Application.isPlaying || snapshot.StolenValue < _displayLoot)
            {
                _displayLoot = _targetLoot;
                if (_lootBig != null)
                {
                    _lootBig.text = "€" + Mathf.RoundToInt(_displayLoot);
                }
            }

            if (_lootQuota != null)
            {
                _lootQuota.text = "stolen · quota €" + MarketData.Quota;
            }

            var archLabel = snapshot.Arch == GuardArchitecture.Fsm ? "FSM" : "BT";
            var stateUpper = string.IsNullOrEmpty(snapshot.GuardState) ? "PATROL" : snapshot.GuardState.ToUpperInvariant();
            if (_guardChipText != null)
            {
                _guardChipText.text = stateUpper + " · " + archLabel;
            }

            if (_guardDot != null)
            {
                _guardDot.RemoveFromClassList("guard-dot-warn");
                _guardDot.RemoveFromClassList("guard-dot-alert");
                if (snapshot.GuardState == "chase" || snapshot.GuardState == "attack")
                {
                    _guardDot.AddToClassList("guard-dot-alert");
                }
                else if (snapshot.GuardState == "investigate" || snapshot.GuardState == "search")
                {
                    _guardDot.AddToClassList("guard-dot-warn");
                }
            }

            if (_btnSwitchAi != null)
            {
                _btnSwitchAi.text = "AI: " + archLabel + " [Tab]";
            }

            if (_statBag != null)
            {
                _statBag.text = snapshot.BagCount + " / " + MarketData.BagCapacity;
            }

            if (_statTags != null)
            {
                _statTags.text = snapshot.ActiveTags > 0 ? snapshot.ActiveTags + " active" : "Clean";
                _statTags.EnableInClassList("stat-value-alert", snapshot.ActiveTags > 0);
            }

            if (_statCctv != null)
            {
                _statCctv.text = snapshot.CamerasDisabled ? "Disabled" : "Online";
            }

            var hasBackup = snapshot.BackupPhase != BackupPhase.None;
            if (_statRadio != null)
            {
                _statRadio.text = hasBackup ? "Backup!" : "Quiet";
                _statRadio.EnableInClassList("stat-value-alert", hasBackup);
            }

            var tierIdx = Mathf.Clamp(snapshot.Tier, 0, HeistState.Tiers.Length - 1);
            var tier = HeistState.Tiers[tierIdx];
            var heatPct = Mathf.Clamp(snapshot.Heat * 100f, 0f, 100f);
            if (_heatTierLabel != null)
            {
                _heatTierLabel.text = tier.Label + " (" + Mathf.RoundToInt(heatPct) + "%)";
            }

            _targetHeatPct = Mathf.Clamp(heatPct, 4f, 100f);
            if (!Application.isPlaying && _heatFill != null)
            {
                _displayHeatPct = _targetHeatPct;
                _heatFill.style.width = Length.Percent(_displayHeatPct);
            }

            var done = new[]
            {
                snapshot.CamerasDisabled,
                snapshot.VitrineOpen,
                snapshot.StolenValue >= MarketData.Quota,
                snapshot.StolenValue > 0f && snapshot.ActiveTags == 0,
                snapshot.Outcome == HeistOutcome.Escaped,
            };

            for (var i = 0; i < 5; i++)
            {
                if (_objBoxes[i] != null)
                {
                    _objBoxes[i].EnableInClassList("obj-box-done", done[i]);
                }

                if (_objTexts[i] != null)
                {
                    _objTexts[i].EnableInClassList("obj-text-done", done[i]);
                }
            }

            // Contextual prompt & real channel progress
            var channelFrac = snapshot.ChannelHold > 0.001f
                ? Mathf.Clamp01(snapshot.ChannelProgress / snapshot.ChannelHold)
                : 0f;
            var channeling = channelFrac > 0.001f;
            if (_promptWrap != null)
            {
                var showPrompt = (hint.Found && !string.IsNullOrEmpty(hint.Prompt)) || channeling;
                _promptWrap.EnableInClassList("is-hidden", !showPrompt);
                if (showPrompt && _promptLabel != null)
                {
                    _promptLabel.text = channeling
                        ? "Working · " + Mathf.RoundToInt(channelFrac * 100f) + "%"
                        : hint.Prompt;
                }

                if (_channelTrack != null && _channelFill != null)
                {
                    _channelTrack.EnableInClassList("is-hidden", !channeling);
                    _channelFill.style.width = Length.Percent(channelFrac * 100f);
                }
            }

            // Minimap highlight, remaining-loot room tags & actor pips
            if (_activeRoomId != room.Id)
            {
                if (!string.IsNullOrEmpty(_activeRoomId) && _roomCells.TryGetValue(_activeRoomId, out var prev))
                {
                    prev.RemoveFromClassList("minimap-cell-active");
                }

                if (_roomCells.TryGetValue(room.Id, out var cur))
                {
                    cur.AddToClassList("minimap-cell-active");
                }

                _activeRoomId = room.Id;
            }

            _bagLookup.Clear();
            if (snapshot.Bag != null)
            {
                foreach (var id in snapshot.Bag)
                {
                    _bagLookup.Add(id);
                }
            }

            _roomsWithLoot.Clear();
            foreach (var loot in MarketData.Loot)
            {
                if (!_bagLookup.Contains(loot.Id))
                {
                    _roomsWithLoot.Add(loot.Room);
                }
            }

            foreach (var kv in _roomCells)
            {
                kv.Value.EnableInClassList("minimap-cell-has-loot", _roomsWithLoot.Contains(kv.Key));
            }

            PlacePip(_pipPlayer, snapshot.Player.x, snapshot.Player.y);
            PlacePip(_pipGuard, snapshot.Guard.x, snapshot.Guard.y);
            PlacePip(_pipStaff, snapshot.Staff.x, snapshot.Staff.y);
            PlacePip(_pipExit, (float)MarketData.EasGate.I, (float)MarketData.EasGate.J);

            // Outcome receipt modal
            if (_outcomeOverlay != null)
            {
                var ended = snapshot.Outcome != HeistOutcome.None;
                _outcomeOverlay.EnableInClassList("is-hidden", !ended);
                if (ended)
                {
                    if (_outcomeTitle != null)
                    {
                        _outcomeTitle.text = snapshot.Outcome == HeistOutcome.Escaped ? "Escaped" : "Busted";
                    }

                    if (_outcomeSub != null)
                    {
                        _outcomeSub.text = snapshot.Outcome == HeistOutcome.Escaped
                            ? "Cleared Frischemarkt with €" + Mathf.RoundToInt(snapshot.StolenValue) + " in stolen goods. Press R for a new shift."
                            : "Caught by the store detective. Press R or click New Shift to try again.";
                    }
                }
            }
        }

        /// <summary>Updates the bag capacity tile with live haul-weight speed and stamina fatigue state.</summary>
        public void UpdateStamina(float stamina01, bool winded, int bagCount)
        {
            if (_statBag == null)
            {
                return;
            }

            var pct = Mathf.RoundToInt(Mathf.Clamp01(stamina01) * 100f);
            var spdPct = Mathf.RoundToInt(Market2DStamina.HaulSpeedMultiplier(bagCount) * 100f);
            _statBag.text = winded
                ? bagCount + " / " + MarketData.BagCapacity + " · WINDED"
                : bagCount + " / " + MarketData.BagCapacity + " · " + pct + "% STAM (" + spdPct + "% SPD)";
        }

        /// <summary>Updates the zoom readout pill beside the +/-/FIT controls.</summary>
        public void UpdateZoomReadout(int zoomPercent, bool fitMode)
        {
            if (_zoomReadout != null)
            {
                _zoomReadout.text = fitMode ? "FIT" : zoomPercent + "%";
            }
        }

        /// <summary>Updates the live FSM rule / Behavior Tree trace line and trait button label.</summary>
        public void UpdateAiBrainTelemetry(string traceText, string profileLabel)
        {
            if (_aiTraceLine != null)
            {
                _aiTraceLine.text = traceText;
            }

            if (_btnTrait != null)
            {
                _btnTrait.text = profileLabel + " [P]";
            }
        }

        /// <summary>Triggers a brief coral riso border flash around the isometric viewport.</summary>
        public void TriggerAlertFlash()
        {
            if (_alertVignette == null)
            {
                return;
            }

            _alertVignette.RemoveFromClassList("is-hidden");
            _alertVignette.style.opacity = 1f;
            _alertFlashTimer = AlertFlashDuration;
        }

        /// <summary>Spawns a rising floating riso badge projected from a world point.</summary>
        public void SpawnFloatingPopup(Vector3 worldPos, string text, bool rfAccent)
        {
            if (_popupLayer == null || _runner == null || _runner.mainCamera == null)
            {
                return;
            }

            var vp = _runner.mainCamera.WorldToViewportPoint(worldPos);
            var fullW = _root != null ? _root.resolvedStyle.width : _popupLayer.resolvedStyle.width;
            var w = _popupLayer.resolvedStyle.width;
            var h = _popupLayer.resolvedStyle.height;
            if (float.IsNaN(w) || w < 10f || float.IsNaN(h) || h < 10f)
            {
                return;
            }

            if (float.IsNaN(fullW) || fullW < w)
            {
                fullW = w;
            }

            var sx = Mathf.Clamp(vp.x * fullW - 34f, 16f, w - 120f);
            var sy = Mathf.Clamp((1f - vp.y) * h - 24f, 56f, h - 68f);

            var lbl = new Label(text) { pickingMode = PickingMode.Ignore };
            lbl.AddToClassList("float-popup");
            if (rfAccent)
            {
                lbl.AddToClassList("float-popup-rf");
            }

            // T18: Rarity-tiered border color for loot popups (Legendary Vitrine / Epic RF / Rare / Uncommon / Common)
            if (text != null && text.StartsWith("+€", StringComparison.Ordinal))
            {
                var borderCol = ResolveLootRarityBorder(text, rfAccent);
                lbl.style.borderTopColor = borderCol;
                lbl.style.borderRightColor = borderCol;
                lbl.style.borderBottomColor = borderCol;
                lbl.style.borderLeftColor = borderCol;
            }

            lbl.style.left = sx;
            lbl.style.top = sy;
            _popupLayer.Add(lbl);
            _popups.Add(new FloatingPopup { Element = lbl, StartX = sx, StartY = sy, Elapsed = 0f });
        }

        /// <summary>Maps a loot popup's value/RF-tag to SpriteCook T18 rarity border colors.</summary>
        public static Color ResolveLootRarityBorder(string text, bool rfTagged)
        {
            if (text != null && text.IndexOf("VITRINE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.851f, 0.643f, 0.251f, 1f); // Legendary #d9a440
            }

            if (rfTagged)
            {
                return new Color(0.427f, 0.227f, 0.659f, 1f); // Epic #6d3aa8
            }

            var val = 0;
            if (text != null)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    if (char.IsDigit(text[i]))
                    {
                        val = val * 10 + (text[i] - '0');
                    }
                    else if (val > 0)
                    {
                        break;
                    }
                }
            }

            if (val >= 50) return new Color(0.184f, 0.435f, 0.769f, 1f); // Rare #2f6fc4
            if (val >= 30) return new Color(0.227f, 0.490f, 0.227f, 1f); // Uncommon #3a7d3a
            return new Color(0.369f, 0.408f, 0.471f, 1f);                // Common #5e6878
        }

        /// <summary>Positions the directional objective compass along the viewport edge when off-screen.</summary>
        public void UpdateCompass(Camera cam, string label, Vector3 targetWorld)
        {
            if (_compassPill == null || _viewportOverlay == null || cam == null)
            {
                return;
            }

            var vp = cam.WorldToViewportPoint(targetWorld);
            var leftFrac = 1f - Market2DRunner.RightPanelFrac;
            var vx = vp.x / Mathf.Max(0.2f, leftFrac);
            var onScreen = vx >= 0.08f && vx <= 0.92f && vp.y >= 0.12f && vp.y <= 0.88f;
            if (onScreen)
            {
                _compassPill.AddToClassList("is-hidden");
                return;
            }

            var w = _viewportOverlay.resolvedStyle.width;
            var h = _viewportOverlay.resolvedStyle.height;
            if (float.IsNaN(w) || w < 100f || float.IsNaN(h) || h < 100f)
            {
                return;
            }

            var dir = new Vector2(vx - 0.5f, vp.y - 0.5f);
            if (dir.sqrMagnitude < 1e-4f)
            {
                _compassPill.AddToClassList("is-hidden");
                return;
            }

            dir.Normalize();
            var halfW = w * 0.5f - 82f;
            var halfH = h * 0.5f - 66f;
            var tx = Mathf.Abs(dir.x) > 1e-4f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            var ty = Mathf.Abs(dir.y) > 1e-4f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            var dist = Mathf.Min(tx, ty);

            var px = w * 0.5f + dir.x * dist - 52f;
            var py = h * 0.5f - dir.y * dist - 14f;

            _compassPill.RemoveFromClassList("is-hidden");
            _compassPill.style.left = Mathf.Clamp(px, 16f, w - 134f);
            _compassPill.style.top = Mathf.Clamp(py, 58f, h - 72f);

            if (_compassLabel != null)
            {
                _compassLabel.text = label;
            }

            if (_compassArrow != null)
            {
                var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _compassArrow.text = DirectionArrow(angle);
            }
        }

        /// <summary>Hides the off-screen objective compass pill.</summary>
        public void HideCompass()
        {
            _compassPill?.AddToClassList("is-hidden");
        }

        /// <summary>Displays a brief coral-ink refusal message above the interaction prompt.</summary>
        public void ShowRefusal(string message)
        {
            if (_refusalBanner == null || _refusalText == null)
            {
                return;
            }

            _refusalText.text = message;
            _refusalBanner.RemoveFromClassList("is-hidden");
            _refusalTimer = RefusalDuration;
        }

        /// <summary>Prepends a crisp entry to the shift journal.</summary>
        public void PushLog(string tag, string message)
        {
            _logs.Insert(0, (tag, message));
            if (_logs.Count > LogCapacity)
            {
                _logs.RemoveRange(LogCapacity, _logs.Count - LogCapacity);
            }

            RenderLogs();
        }

        /// <summary>Resets the shift journal to its opening line.</summary>
        public void ResetFeed()
        {
            _logs.Clear();
            _logs.Add(("START", "Entered Frischemarkt via north-west portal"));
            _displayLoot = 0f;
            _targetLoot = 0f;
            _displayHeatPct = 4f;
            _targetHeatPct = 4f;
            RenderLogs();
        }

        private void RenderLogs()
        {
            if (_logRows == null)
            {
                return;
            }

            for (var i = 0; i < LogCapacity; i++)
            {
                if (_logRows[i] == null)
                {
                    continue;
                }

                if (i < _logs.Count)
                {
                    _logRows[i].RemoveFromClassList("is-hidden");
                    var (tag, msg) = _logs[i];
                    if (_logTags[i] != null)
                    {
                        _logTags[i].text = tag;
                        _logTags[i].EnableInClassList("log-tag-alarm", tag == "ALARM" || tag == "CCTV");
                    }

                    if (_logMsgs[i] != null)
                    {
                        _logMsgs[i].text = msg;
                    }
                }
                else
                {
                    _logRows[i].AddToClassList("is-hidden");
                }
            }
        }

        private static string DirectionArrow(float deg)
        {
            if (deg >= -22.5f && deg < 22.5f) return "→";
            if (deg >= 22.5f && deg < 67.5f) return "↗";
            if (deg >= 67.5f && deg < 112.5f) return "↑";
            if (deg >= 112.5f && deg < 157.5f) return "↖";
            if (deg >= -67.5f && deg < -22.5f) return "↘";
            if (deg >= -112.5f && deg < -67.5f) return "↓";
            if (deg >= -157.5f && deg < -112.5f) return "↙";
            return "←";
        }

        private static void PlacePip(VisualElement pip, float tileI, float tileJ)
        {
            if (pip == null)
            {
                return;
            }

            var xPct = Mathf.Clamp(tileI / MarketData.WidthTiles * 100f, 1f, 96f);
            var yPct = Mathf.Clamp(tileJ / MarketData.HeightTiles * 100f, 2f, 92f);
            pip.style.left = Length.Percent(xPct);
            pip.style.top = Length.Percent(yPct);
        }

        private static void RebindButton(Button btn, Action handler)
        {
            if (btn == null)
            {
                return;
            }

            btn.clicked -= handler;
            btn.clicked += handler;
        }

        private void OnSwitchAi() => _runner?.ToggleGuardArchitecture();
        private void OnCycleTrait() => _runner?.CycleGuardPersonality();
        private void OnReset() => _runner?.ResetShift();
        private void OnZoomIn() => _runner?.ZoomIn();
        private void OnZoomOut() => _runner?.ZoomOut();
        private void OnZoomFit() => _runner?.ToggleFitOverview();

        private void OnToggleMute()
        {
            if (_runner == null)
            {
                return;
            }

            var muted = _runner.ToggleMute();
            if (_btnMute != null)
            {
                _btnMute.text = muted ? "MUTE" : "SFX";
                _btnMute.EnableInClassList("zoom-btn-muted", muted);
            }
        }
    }
}
