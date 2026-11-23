using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HeistSim.Market.Editor
{
    /// <summary>
    /// Batch capture that proves the HUD renders real glyphs. It opens the built scene, builds
    /// the runtime widget tree at edit time, fills representative text, then renders the world
    /// and the overlay HUD into one 1600 by 900 PNG using the live display path.
    /// </summary>
    public static class MarketHudTextCapture
    {
        /// <summary>File name of the HUD text verification capture.</summary>
        public const string HudCaptureFileName = "MarketHud.png";

        private const int UiLayer = 5;
        private const int CaptureWidth = MarketSceneSpec.CaptureWidth;
        private const int CaptureHeight = MarketSceneSpec.CaptureHeight;

        private static readonly Dictionary<string, string> Samples = new()
        {
            ["Status Kicker"] = "SHIFT BRIEF",
            ["Headline"] = "Frischemarkt",
            ["Heat Kicker"] = "HEAT",
            ["Heat Tier"] = "Hot",
            ["Loot Kicker"] = "LOOT / QUOTA",
            ["Loot"] = "Loot: 190 / 190",
            ["Bag Kicker"] = "BAG",
            ["Bag"] = "Bag: 4 / 5",
            ["Objectives Kicker"] = "OBJECTIVES",
            ["Objective 1"] = "Cut the power",
            ["Objective 2"] = "Open the vitrine",
            ["Objective 3"] = "Bag 190 in goods",
            ["Objective 4"] = "Kill the tags",
            ["Objective 5"] = "Reach the gate",
            ["Radio Kicker"] = "RADIO",
            ["Backup"] = "Backup: on the way",
            ["Tags Kicker"] = "RF TAGS",
            ["Tags"] = "RF tags: 2",
            ["Glance Kicker"] = "GUARD GLANCE",
            ["Guard"] = "Chase",
            ["Controller Badge Label"] = "BT",
            ["Zone Kicker"] = "DEPARTMENT",
            ["Zone"] = "Zone: Frischemarkt",
            ["Feed Kicker"] = "ACTIVITY FEED",
            ["Feed Badge 0"] = "ALARM",
            ["Feed Line 0"] = "Guard spotted you",
            ["Feed Badge 1"] = "STEAL",
            ["Feed Line 1"] = "Bag secured, 190 in goods",
            ["Feed Badge 2"] = "CCTV",
            ["Feed Line 2"] = "Camera sighting at the gate",
            ["Feed Badge 3"] = "BACKUP",
            ["Feed Line 3"] = "Backup responding",
            ["Feed Badge 4"] = "STEAL",
            ["Feed Line 4"] = "Vitrine opened",
            ["Prompt Key"] = "E",
            ["Prompt Label"] = "open the vitrine",
            ["Action"] = "Stealing 65%",
            ["Hint"] = "Click Game view | WASD move | E act | C crouch | Shift run | R reset | Tab guard",
            ["Zoom Note"] = "Wheel zoom 8 to 24 tiles",
            ["Refusal"] = "BAG FULL",
            ["Banner"] = "ESCAPED\nShift complete, R to run again",
        };

        /// <summary>Opens the market scene and writes the HUD text verification capture.</summary>
        [MenuItem("Heist/Market/Capture HUD Text")]
        public static void CaptureHudText()
        {
            if (!File.Exists(MarketSceneSpec.ScenePath))
            {
                throw new InvalidOperationException("Market scene missing, run Heist/Market/Build Level Scene first.");
            }

            EnsureCapturesFolder();
            EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Single);

            var hud = UnityEngine.Object.FindAnyObjectByType<MarketHUD>();
            if (hud == null)
            {
                var hudObject = new GameObject(MarketSceneSpec.RootHud, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                hud = hudObject.AddComponent<MarketHUD>();
            }

            var awake = typeof(MarketHUD).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake == null)
            {
                throw new InvalidOperationException("MarketHUD.Awake was not found.");
            }

            awake.Invoke(hud, null);
            Canvas.ForceUpdateCanvases();
            var font = Resources.Load<TMP_FontAsset>("MarketSans");
            if (font == null)
            {
                throw new InvalidOperationException("MarketSans font asset did not load from Resources.");
            }

            PopulateText(hud, font);
            var matched = VerifyGlyphs(hud, font);

            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("No camera in " + MarketSceneSpec.ScenePath);
            }

            var texture = RenderWorldAndHud(hud, camera);
            try
            {
                var bytes = MarketPngWriter.Encode(texture);
                var outputPath = MarketSceneSpec.CapturesDir + "/" + HudCaptureFileName;
                File.WriteAllBytes(outputPath, bytes);

                var colors = UniqueColorCount(texture);
                ValidatePng(bytes);
                AssetDatabase.Refresh();

                Debug.Log("Market HUD text capture wrote " + Path.GetFullPath(outputPath)
                    + " (" + bytes.Length + " bytes, " + colors + " unique colors, "
                    + matched.Item1 + " of " + matched.Item2 + " distinct characters matched in MarketSans)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Headless entry point that captures the HUD text, exits non zero on failure.</summary>
        public static void CaptureBatch()
        {
            try
            {
                CaptureHudText();
                Debug.Log("Market HUD text capture finished.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Market HUD text capture failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureCapturesFolder()
        {
            if (!AssetDatabase.IsValidFolder(MarketSceneSpec.CapturesDir))
            {
                AssetDatabase.CreateFolder(MarketSceneSpec.MarketDir, "Captures");
            }
        }

        private static void PopulateText(MarketHUD hud, TMP_FontAsset font)
        {
            foreach (var text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                if (Samples.TryGetValue(text.name, out var sample))
                {
                    text.text = sample;
                }
                else if (string.IsNullOrEmpty(text.text))
                {
                    text.text = "SAMPLE";
                }

                text.font = font;
            }

            hud.promptRect.gameObject.SetActive(true);
            hud.refusalText.gameObject.SetActive(true);
            hud.refusalText.transform.parent.gameObject.SetActive(true);
            hud.actionBarRoot.SetActive(true);
            hud.actionFill.anchorMax = new Vector2(0.65f, 1f);

            var marks = typeof(MarketHUD).GetField("_objectiveMarks", BindingFlags.Instance | BindingFlags.NonPublic);
            var objectiveMarks = marks?.GetValue(hud) as Image[];
            if (objectiveMarks != null && objectiveMarks.Length > 0 && objectiveMarks[0] != null)
            {
                objectiveMarks[0].color = MarketSceneSpec.Teal;
            }

            var guard = GameObject.Find("Market/Actors/Guard");
            var sceneCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (guard != null && GameObject.Find("Guard Alert Bubble") == null)
            {
                var bubble = new GameObject("Guard Alert Bubble", typeof(TextMeshPro));
                bubble.transform.SetParent(guard.transform, false);
                bubble.transform.localPosition = new Vector3(-12f, 5f, 0f);
                bubble.transform.rotation = sceneCamera != null ? sceneCamera.transform.rotation : Quaternion.identity;
                var label = bubble.GetComponent<TextMeshPro>();
                label.font = font;
                label.fontSize = 3.2f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = MarketSceneSpec.Sun;
                label.text = "!";
                label.rectTransform.sizeDelta = new Vector2(2.6f, 1.4f);
                label.sortingOrder = 20;
                label.ForceMeshUpdate(true);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static Tuple<int, int> VerifyGlyphs(MarketHUD hud, TMP_FontAsset font)
        {
            var baked = new HashSet<int>();
            foreach (var character in font.characterTable)
            {
                baked.Add((int)character.unicode);
            }

            var used = new HashSet<int>();
            foreach (var text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                foreach (var c in text.text)
                {
                    if (c >= 32)
                    {
                        used.Add(c);
                    }
                }
            }

            var guardBubble = GameObject.Find("Guard Alert Bubble");
            if (guardBubble != null)
            {
                var label = guardBubble.GetComponent<TMP_Text>();
                if (label != null)
                {
                    foreach (var c in label.text)
                    {
                        if (c >= 32)
                        {
                            used.Add(c);
                        }
                    }
                }
            }

            var matched = used.Count(baked.Contains);
            if (matched != used.Count)
            {
                var missing = string.Concat(used.Where(c => !baked.Contains(c)).Select(c => (char)c));
                throw new InvalidOperationException("MarketSans is missing these displayed characters: " + missing);
            }

            return Tuple.Create(matched, used.Count);
        }

        private static Texture2D RenderWorldAndHud(MarketHUD hud, Camera camera)
        {
            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            var previousTarget = camera.targetTexture;
            camera.targetTexture = renderTexture;
            camera.Render();
            camera.targetTexture = previousTarget;

            var canvas = hud.GetComponent<Canvas>();
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var layers = new List<int>();
            foreach (var transform in hud.GetComponentsInChildren<Transform>(true))
            {
                layers.Add(transform.gameObject.layer);
                transform.gameObject.layer = UiLayer;
            }

            GameObject uiCameraObject = null;
            try
            {
                uiCameraObject = new GameObject("HUD Capture Camera", typeof(Camera));
                var uiCamera = uiCameraObject.GetComponent<Camera>();
                uiCamera.CopyFrom(camera);
                uiCamera.cullingMask = 1 << UiLayer;
                uiCamera.clearFlags = CameraClearFlags.Nothing;
                uiCamera.targetTexture = renderTexture;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = 1f;
                canvas.worldCamera = uiCamera;
                Canvas.ForceUpdateCanvases();
                uiCamera.Render();

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
                texture.Apply();
                RenderTexture.active = previousActive;
                return texture;
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                var index = 0;
                foreach (var transform in hud.GetComponentsInChildren<Transform>(true))
                {
                    if (index < layers.Count)
                    {
                        transform.gameObject.layer = layers[index];
                        index++;
                    }
                }

                if (uiCameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(uiCameraObject);
                }

                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static int UniqueColorCount(Texture2D texture)
        {
            var seen = new HashSet<int>();
            foreach (var pixel in texture.GetPixels32())
            {
                seen.Add((pixel.r << 16) | (pixel.g << 8) | pixel.b);
            }

            return seen.Count;
        }

        private static void ValidatePng(byte[] bytes)
        {
            var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            for (var i = 0; i < signature.Length; i++)
            {
                if (bytes[i] != signature[i])
                {
                    throw new InvalidOperationException("Capture PNG has an invalid signature.");
                }
            }

            var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            if (width != CaptureWidth || height != CaptureHeight)
            {
                throw new InvalidOperationException("Capture PNG has the wrong dimensions: " + width + " by " + height);
            }

            var decoded = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!ImageConversion.LoadImage(decoded, bytes) || decoded.width != CaptureWidth || decoded.height != CaptureHeight)
                {
                    throw new InvalidOperationException("Capture PNG failed to decode.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }
        }
    }
}
