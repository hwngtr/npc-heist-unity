using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeistSim.Market
{
    /// <summary>
    /// Builds the minimap, objective arrow, exit beacon and department cue at runtime so a rebuilt scene needs no extra assets.
    /// </summary>
    public sealed class NavigationRuntime
    {
        private const float FlashSeconds = 1f;
        private const float FlashAlpha = 0.6f;
        private static readonly Vector2 MinimapSize = new Vector2(296f, 232f);
        private static readonly Vector2 GridSize = new Vector2(MinimapSize.x - 28f, MinimapSize.y - 46f);

        private readonly Transform _host;
        private readonly Image[] _cells = new Image[MarketData.Rooms.Length];
        private readonly Image[] _cameraPips = new Image[MarketData.Cameras.Length];

        private GameObject _canvasRoot;
        private RectTransform _grid;
        private Image _currentFrame;
        private Image _playerDot;
        private Image _guardDot;
        private Image _staffDot;
        private Image _exitMarker;
        private RectTransform _arrowRoot;
        private RectTransform _arrowLabelPanel;
        private TMP_Text _arrowLabel;
        private Image _flash;
        private GameObject _beaconRoot;
        private LineRenderer[] _beaconLines;
        private TMP_FontAsset _font;
        private string _currentZone;
        private float _flashTimer;

        /// <summary>Creates the navigation canvas and the exit beacon under the given host.</summary>
        public NavigationRuntime(Transform host)
        {
            _host = host;
            Build();
        }

        /// <summary>Refreshes every navigation aid from the current snapshot.</summary>
        public void Refresh(in HeistSnapshot snapshot, Camera camera, float dt)
        {
            RefreshRooms(snapshot);
            RefreshActors(snapshot);
            RefreshCameras(snapshot);
            RefreshArrow(snapshot, camera);
            RefreshBeacon(snapshot);
            RefreshFlash(dt);
        }

        /// <summary>Clears transient navigation state when a new shift starts.</summary>
        public void ResetState()
        {
            _currentZone = null;
            _flashTimer = 0f;
            if (_flash != null)
            {
                _flash.gameObject.SetActive(false);
            }

            if (_arrowRoot != null)
            {
                _arrowRoot.gameObject.SetActive(false);
            }

            if (_arrowLabelPanel != null)
            {
                _arrowLabelPanel.gameObject.SetActive(false);
            }
        }

        /// <summary>Destroys the runtime navigation objects.</summary>
        public void Dispose()
        {
            if (_canvasRoot != null)
            {
                UnityEngine.Object.Destroy(_canvasRoot);
                _canvasRoot = null;
            }

            if (_beaconRoot != null)
            {
                UnityEngine.Object.Destroy(_beaconRoot);
                _beaconRoot = null;
            }
        }

        private void Build()
        {
            _font = Resources.Load<TMP_FontAsset>("MarketSans");
            BuildCanvas();
            BuildBeacon();
        }

        private void BuildCanvas()
        {
            _canvasRoot = new GameObject("Minimap Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(MarketSceneSpec.CaptureWidth, MarketSceneSpec.CaptureHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var rootRect = (RectTransform)_canvasRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var panel = CreateImage("Minimap Panel", rootRect, Opaque(MarketSceneSpec.Card, 0.94f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), MinimapSize);
            CreateText("Minimap Kicker", panel.rectTransform, "MARKET MAP", 11f, MarketSceneSpec.InkBlue2,
                TextAlignmentOptions.TopLeft, new Vector2(14f, -8f), new Vector2(MinimapSize.x - 28f, 18f));

            _grid = new GameObject("Minimap Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            _grid.SetParent(panel.rectTransform, false);
            _grid.anchorMin = new Vector2(0f, 0f);
            _grid.anchorMax = new Vector2(0f, 0f);
            _grid.pivot = new Vector2(0f, 0f);
            _grid.anchoredPosition = new Vector2(14f, 12f);
            _grid.sizeDelta = GridSize;

            for (var i = 0; i < MarketData.Rooms.Length; i++)
            {
                var rect = MinimapMapping.CellRect(i);
                var cell = CreateImage("Room " + MarketData.Rooms[i].Id, _grid,
                    Opaque(DepartmentStyle.Signature(MarketData.Rooms[i]), 0.62f),
                    new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.yMax), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                _cells[i] = cell;
            }

            var frameRect = MinimapMapping.CellRect(0);
            _currentFrame = CreateImage("Current Room Frame", _grid, new Color(1f, 1f, 1f, 0.01f),
                new Vector2(frameRect.x, frameRect.y), new Vector2(frameRect.xMax, frameRect.yMax), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var outline = _currentFrame.gameObject.AddComponent<Outline>();
            outline.effectColor = MarketSceneSpec.Card;
            outline.effectDistance = new Vector2(2f, -2f);

            _playerDot = MakePip("Player Dot", _grid, NavigationSprites.Disc, 12f, MarketSceneSpec.Teal);
            _guardDot = MakePip("Guard Dot", _grid, NavigationSprites.Disc, 11f, MarketSceneSpec.Coral);
            _staffDot = MakePip("Staff Dot", _grid, NavigationSprites.Disc, 10f, MarketSceneSpec.Sun);

            for (var i = 0; i < MarketData.Cameras.Length; i++)
            {
                _cameraPips[i] = MakePip("Camera Pip " + i, _grid, NavigationSprites.Disc, 6f, MarketSceneSpec.InkBlue2);
                var def = MarketData.Cameras[i];
                PlacePip(_cameraPips[i].rectTransform, new Vector2((float)def.I, (float)def.J));
            }

            _exitMarker = MakePip("EAS Exit Marker", _grid, null, 13f, MarketSceneSpec.CoralInk);
            _exitMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            PlacePip(_exitMarker.rectTransform, Point(MarketData.EasGate));

            BuildArrow(rootRect);
            BuildFlash(rootRect);
        }

        private void BuildArrow(RectTransform rootRect)
        {
            var arrow = CreateImage("Objective Arrow", rootRect, MarketSceneSpec.CoralInk,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 28f));
            arrow.sprite = NavigationSprites.Triangle;
            _arrowRoot = arrow.rectTransform;
            _arrowRoot.gameObject.SetActive(false);

            _arrowLabelPanel = CreateImage("Objective Label Panel", rootRect, Opaque(MarketSceneSpec.Card, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(172f, 26f)).rectTransform;
            _arrowLabel = CreateText("Objective Label", _arrowLabelPanel, string.Empty, 12f, MarketSceneSpec.InkBlue,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(164f, 24f));
            _arrowLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowLabel.rectTransform.anchoredPosition = Vector2.zero;
            _arrowLabelPanel.gameObject.SetActive(false);
        }

        private void BuildFlash(RectTransform rootRect)
        {
            _flash = CreateImage("Department Flash", rootRect, new Color(MarketSceneSpec.InkBlue.r, MarketSceneSpec.InkBlue.g, MarketSceneSpec.InkBlue.b, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 12f), new Vector2(184f, 12f));
            _flash.gameObject.SetActive(false);
        }

        private void BuildBeacon()
        {
            var gate = Point(MarketData.EasGate);
            _beaconRoot = new GameObject("EAS Exit Beacon");
            _beaconRoot.transform.SetParent(_host, false);
            _beaconRoot.transform.position = new Vector3(gate.x, 0f, gate.y);

            var ring = MakeLine("Beacon Ring", _beaconRoot.transform, true);
            ring.positionCount = 24;
            for (var i = 0; i < 24; i++)
            {
                var angle = i / 24f * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.95f, 0.12f, Mathf.Sin(angle) * 0.95f));
            }

            var stem = MakeLine("Beacon Stem", _beaconRoot.transform, false);
            stem.positionCount = 2;
            stem.SetPosition(0, new Vector3(0f, 0.18f, 0f));
            stem.SetPosition(1, new Vector3(0f, 2.5f, 0f));

            var crown = MakeLine("Beacon Crown", _beaconRoot.transform, true);
            crown.positionCount = 4;
            crown.SetPosition(0, new Vector3(0.55f, 2.65f, 0f));
            crown.SetPosition(1, new Vector3(0f, 2.65f, 0.55f));
            crown.SetPosition(2, new Vector3(-0.55f, 2.65f, 0f));
            crown.SetPosition(3, new Vector3(0f, 2.65f, -0.55f));

            _beaconLines = new[] { ring, stem, crown };
        }

        private void RefreshRooms(in HeistSnapshot snapshot)
        {
            var zone = MarketHUD.ZoneName(snapshot.Player);
            if (zone == _currentZone)
            {
                return;
            }

            _currentZone = zone;
            var room = DepartmentStyle.RoomForZone(zone);
            var roomIndex = room.HasValue
                ? Array.IndexOf(MarketData.Rooms, room.Value)
                : MinimapMapping.ClampedRoomIndexForTile(snapshot.Player);
            _flashTimer = FlashSeconds;
            var signature = DepartmentStyle.SignatureForZone(zone);
            _flash.color = new Color(signature.r, signature.g, signature.b, FlashAlpha);
            _flash.gameObject.SetActive(true);
            SetRect(MinimapMapping.CellRect(roomIndex), _currentFrame.rectTransform);

            for (var i = 0; i < _cells.Length; i++)
            {
                var tint = DepartmentStyle.Signature(MarketData.Rooms[i]);
                _cells[i].color = Opaque(tint, i == roomIndex ? 0.95f : 0.62f);
            }
        }

        private void RefreshActors(in HeistSnapshot snapshot)
        {
            PlacePip(_playerDot.rectTransform, snapshot.Player);
            PlacePip(_guardDot.rectTransform, snapshot.Guard);
            PlacePip(_staffDot.rectTransform, snapshot.Staff);
        }

        private void RefreshCameras(in HeistSnapshot snapshot)
        {
            for (var i = 0; i < _cameraPips.Length; i++)
            {
                var seeing = (snapshot.CameraSightings & (1 << i)) != 0;
                var pip = _cameraPips[i];
                pip.color = Opaque(seeing ? MarketSceneSpec.Coral : MarketSceneSpec.InkBlue2, seeing ? 1f : 0.5f);
                pip.rectTransform.sizeDelta = new Vector2(seeing ? 10f : 6f, seeing ? 10f : 6f);
            }
        }

        private void RefreshArrow(in HeistSnapshot snapshot, Camera camera)
        {
            var target = ObjectiveTargets.Next(snapshot);
            if (!target.HasValue || camera == null)
            {
                _arrowRoot.gameObject.SetActive(false);
                _arrowLabelPanel.gameObject.SetActive(false);
                return;
            }

            var screen = ScreenSize();
            var state = ObjectiveArrowMath.Evaluate(camera, snapshot.Player, target.Value.Tile, screen, target.Value.Label);
            if (!state.Visible)
            {
                _arrowRoot.gameObject.SetActive(false);
                _arrowLabelPanel.gameObject.SetActive(false);
                return;
            }

            if (TryScreenPoint(state.ScreenPosition, out var local))
            {
                _arrowRoot.anchoredPosition = local;
                _arrowRoot.localRotation = Quaternion.Euler(0f, 0f, state.AngleDegrees);
            }

            var towardsCentre = (screen * 0.5f - state.ScreenPosition).normalized;
            var labelPoint = state.ScreenPosition + towardsCentre * 104f;
            if (TryScreenPoint(labelPoint, out var labelLocal))
            {
                _arrowLabelPanel.anchoredPosition = labelLocal;
            }

            _arrowLabel.text = state.Label + "  " + Mathf.RoundToInt(state.DistanceTiles) + " tiles";
            _arrowRoot.gameObject.SetActive(true);
            _arrowLabelPanel.gameObject.SetActive(true);
        }

        private void RefreshBeacon(in HeistSnapshot snapshot)
        {
            var ready = ObjectiveTargets.RequiredDone(snapshot);
            var tint = ready ? MarketSceneSpec.Coral : MarketSceneSpec.CoralInk;
            var alpha = ready ? 0.95f : 0.45f;
            var width = ready ? 0.12f : 0.07f;
            for (var i = 0; i < _beaconLines.Length; i++)
            {
                var line = _beaconLines[i];
                line.startColor = Opaque(tint, alpha);
                line.endColor = Opaque(tint, alpha);
                line.startWidth = width;
                line.endWidth = width;
            }

            var crown = _beaconLines[2].transform;
            crown.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2f) * 0.08f, 0f);
        }

        private void RefreshFlash(float dt)
        {
            if (_flashTimer <= 0f)
            {
                return;
            }

            _flashTimer = Mathf.Max(0f, _flashTimer - Mathf.Max(0f, dt));
            var color = _flash.color;
            color.a = FlashAlpha * (_flashTimer / FlashSeconds);
            _flash.color = color;
            if (_flashTimer <= 0f)
            {
                _flash.gameObject.SetActive(false);
            }
        }

        private bool TryScreenPoint(Vector2 screenPoint, out Vector2 local)
        {
            if (_canvasRoot != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvasRoot.transform, screenPoint, null, out local))
            {
                return true;
            }

            var screen = ScreenSize();
            local = screenPoint - screen * 0.5f;
            return true;
        }

        private static Vector2 ScreenSize()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return new Vector2(MarketSceneSpec.CaptureWidth, MarketSceneSpec.CaptureHeight);
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private static Vector2 Point(LevelPoint point)
        {
            return new Vector2((float)point.I, (float)point.J);
        }

        private static void PlacePip(RectTransform rect, Vector2 tile)
        {
            var normalized = MinimapMapping.Normalized(tile);
            rect.anchorMin = normalized;
            rect.anchorMax = normalized;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetRect(Rect normalized, RectTransform rect)
        {
            rect.anchorMin = new Vector2(normalized.x, normalized.y);
            rect.anchorMax = new Vector2(normalized.xMax, normalized.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image MakePip(string name, Transform parent, Sprite sprite, float size, Color color)
        {
            var image = CreateImage(name, parent, color, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            return image;
        }

        private static LineRenderer MakeLine(string name, Transform parent, bool loop)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.material = new Material(Shader.Find("Sprites/Default"))
            {
                name = name + " (Runtime)",
            };
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private TMP_Text CreateText(string name, Transform parent, string content, float size, Color color,
            TextAlignmentOptions alignment, Vector2 position, Vector2 box)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static Color Opaque(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}
