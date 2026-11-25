using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using HeistSim.Market2D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HeistSim.PlayModeTests
{
    /// <summary>
    /// Live PlayMode verification for the 2D Riso Press Frischemarkt prototype:
    /// verifies default hidden Shift Dossier drawer, minimalist fullscreen HUD bounds,
    /// zero text overflow when the collapsible drawer opens with worst-case strings,
    /// and captures Market2D.png, Market2DClose.png, and Market2DDrawer.png.
    /// </summary>
    public sealed class Market2DPlayModeCaptureTests
    {
        private const string ScenePath = "Assets/HeistSim/Market2D/Market2D.unity";
        private const string OutputPath = "Assets/HeistSim/Market2D/Captures/Market2D.png";
        private const string CloseZoomOutputPath = "Assets/HeistSim/Market2D/Captures/Market2DClose.png";
        private const string DrawerOutputPath = "Assets/HeistSim/Market2D/Captures/Market2DDrawer.png";
        private const int Width = 1600;
        private const int Height = 900;

        [UnityTest]
        public IEnumerator RendersSplitScreenWorldAndUiToolkitHud()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("Market2D");
#endif
            yield return null;
            yield return null;

            var runner = UnityEngine.Object.FindAnyObjectByType<Market2DRunner>();
            var hud = UnityEngine.Object.FindAnyObjectByType<Market2DHudController>();
            var doc = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            var cam = runner != null ? runner.mainCamera : Camera.main;

            Assert.That(runner, Is.Not.Null, "Market2DRunner missing");
            Assert.That(hud, Is.Not.Null, "Market2DHudController missing");
            Assert.That(doc, Is.Not.Null, "UIDocument missing");
            Assert.That(cam, Is.Not.Null, "Main Camera missing");
            Assert.That(doc.panelSettings, Is.Not.Null, "PanelSettings missing");

            var uiClosedRt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            uiClosedRt.Create();
            var uiDrawerRt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            uiDrawerRt.Create();

            var ps = doc.panelSettings;
            var prevTarget = ps.targetTexture;
            var prevClear = ps.clearColor;
            var prevClearCol = ps.colorClearValue;

            ps.clearColor = true;
            ps.colorClearValue = new Color(0f, 0f, 0f, 0f);
            ps.targetTexture = uiClosedRt;

            runner.enabled = false;
            hud.SetDrawerOpen(false);
            var snap = runner.Game.Snapshot();
            var hint = new MarketActionHint(true, "Stealing", 1.8f, snap.Player, "Hold E · Steal 18yr Islay Single Malt [RF-TAG] (€68)");
            hud.PushLog("LOOT", "Cased Entrance · Obst & Gemüse");
            hud.PushLog("CCTV", "CAM-01 sweeping north-east aisle");
            hud.PushLog("AI", "Guard 2 flanking east expressway");
            hud.Refresh(snap, hint);
            hud.UpdateStamina(0.73f, false, 4);

            for (var i = 0; i < 4; i++)
            {
                yield return null;
            }

            RenderPanel(doc);

            var root = doc.rootVisualElement;
            Assert.That(root, Is.Not.Null);
            var sidePanel = root.Q<VisualElement>("side-panel");
            var topStrip = root.Q<VisualElement>("top-hud-strip");
            var toggleBtn = root.Q<Button>("btn-toggle-drawer");
            var zoneName = root.Q<Label>("zone-name");
            var promptWrap = root.Q<VisualElement>("prompt-wrap");
            var promptLabel = root.Q<Label>("prompt-label");
            var lootBig = root.Q<Label>("loot-big");
            var minimap = root.Q<VisualElement>("minimap-board");

            Assert.That(sidePanel, Is.Not.Null);
            Assert.That(hud.IsDrawerOpen, Is.False, "Shift Dossier must start hidden by default");
            Assert.That(sidePanel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "Side drawer should be DisplayStyle.None by default");
            Assert.That(topStrip, Is.Not.Null);
            Assert.That(topStrip.layout.width, Is.GreaterThan(220f), "Top-left compact HUD bar did not lay out");
            Assert.That(toggleBtn, Is.Not.Null);
            Assert.That(toggleBtn.layout.width, Is.GreaterThan(50f), "Top-right BRIEF toggle button collapsed");
            Assert.That(zoneName, Is.Not.Null);
            Assert.That(zoneName.layout.height, Is.GreaterThan(10f));
            Assert.That(promptWrap, Is.Not.Null);
            Assert.That(promptWrap.layout.width, Is.GreaterThan(150f), "Contextual prompt collapsed");
            Assert.That(promptLabel, Is.Not.Null);
            Assert.That(promptLabel.worldBound.xMax, Is.LessThanOrEqualTo(promptWrap.worldBound.xMax + 1f),
                "Long prompt label overflowed its prompt card bounds");

            var uiClosedTex = ReadRt(uiClosedRt);
            var tacticalTex = RenderComposite(cam, uiClosedTex, Width, Market2DRunner.ComputePixelPerfectOrthoSize(1.0f, Height));
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, EncodeRgbPng(tacticalTex));

            var closeTex = RenderComposite(cam, uiClosedTex, Width, Market2DRunner.ComputePixelPerfectOrthoSize(2.0f, Height));
            File.WriteAllBytes(CloseZoomOutputPath, EncodeRgbPng(closeTex));

            // Phase B: open the Shift Dossier drawer and verify layout + zero horizontal overflow across all cards
            ps.targetTexture = uiDrawerRt;
            hud.SetDrawerOpen(true);
            hud.UpdateStamina(0.73f, false, 4);
            hud.UpdateSquadTelemetry("ST+HTN", "COMBAT · DirectPursuit", "COMBAT · FlankCutoff", true, "Observant");

            for (var i = 0; i < 4; i++)
            {
                yield return null;
            }

            RenderPanel(doc);

            Assert.That(hud.IsDrawerOpen, Is.True);
            Assert.That(sidePanel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(sidePanel.layout.width, Is.InRange(355f, 405f), "Collapsible right drawer width out of expected range");
            Assert.That(lootBig, Is.Not.Null);
            Assert.That(lootBig.layout.height, Is.GreaterThan(18f), "Loot display headline collapsed");
            Assert.That(minimap, Is.Not.Null);
            Assert.That(minimap.layout.height, Is.GreaterThanOrEqualTo(74f), "Minimap collapsed below redesigned height");

            string[] checkedIds =
            {
                "stat-bag", "stat-bag-sub", "stat-stamina", "stat-stamina-sub",
                "stat-tags", "stat-cctv", "stat-radio",
                "g1-ai-line", "g2-ai-line",
                "btn-switch-ai", "btn-trait", "btn-reset",
            };
            foreach (var id in checkedIds)
            {
                var el = root.Q<VisualElement>(id);
                Assert.That(el, Is.Not.Null, "Missing drawer element " + id);
                Assert.That(el.worldBound.width, Is.GreaterThan(5f), id + " collapsed");
                Assert.That(el.worldBound.xMin, Is.GreaterThanOrEqualTo(sidePanel.worldBound.xMin - 1f),
                    id + " clipped past left drawer edge");
                Assert.That(el.worldBound.xMax, Is.LessThanOrEqualTo(sidePanel.worldBound.xMax + 1f),
                    id + " overflowed past right drawer edge");
            }

            var log0 = root.Q<VisualElement>("log-row-0");
            var log1 = root.Q<VisualElement>("log-row-1");
            var zoomGroup = root.Q<VisualElement>("zoom-group");
            Assert.That(log0, Is.Not.Null);
            Assert.That(log1, Is.Not.Null);
            Assert.That(log1.layout.y, Is.GreaterThanOrEqualTo(log0.layout.yMax - 0.5f), "Shift journal rows overlapped");
            Assert.That(log0.worldBound.yMax, Is.LessThanOrEqualTo(sidePanel.worldBound.yMax + 2f),
                "First Shift Journal row clipped below 900px drawer viewport");
            if (zoomGroup != null)
            {
                Assert.That(promptWrap.worldBound.xMax, Is.LessThanOrEqualTo(zoomGroup.worldBound.xMin - 4f),
                    "Contextual prompt card overlapped shifted zoom controls when drawer opened");
            }

            var uiDrawerTex = ReadRt(uiDrawerRt);
            var drawerCompositeTex = RenderComposite(cam, uiDrawerTex, Width, Market2DRunner.ComputePixelPerfectOrthoSize(1.0f, Height));
            File.WriteAllBytes(DrawerOutputPath, EncodeRgbPng(drawerCompositeTex));

            var uiClosedPixels = CountAlphaPixels(uiClosedTex);
            var uiDrawerPixels = CountAlphaPixels(uiDrawerTex);
            Debug.Log("Market2D PlayMode captures done: closedPx=" + uiClosedPixels
                + ", drawerPx=" + uiDrawerPixels
                + ", sidePanel=" + sidePanel.layout);

            ps.targetTexture = prevTarget;
            ps.clearColor = prevClear;
            ps.colorClearValue = prevClearCol;

            uiClosedRt.Release();
            uiDrawerRt.Release();
            UnityEngine.Object.Destroy(uiClosedRt);
            UnityEngine.Object.Destroy(uiDrawerRt);
            UnityEngine.Object.Destroy(uiClosedTex);
            UnityEngine.Object.Destroy(uiDrawerTex);
            UnityEngine.Object.Destroy(tacticalTex);
            UnityEngine.Object.Destroy(closeTex);
            UnityEngine.Object.Destroy(drawerCompositeTex);

            Assert.That(uiClosedPixels, Is.GreaterThan(25000), "Fullscreen minimal HUD did not paint visible pixels");
            Assert.That(uiDrawerPixels, Is.GreaterThan(200000), "Open Shift Dossier drawer did not paint visible pixels");
        }

        private static void RenderPanel(UIDocument doc)
        {
            var panel = doc.rootVisualElement != null ? doc.rootVisualElement.panel : null;
            if (panel == null)
            {
                return;
            }

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var pt = panel.GetType();
            pt.GetMethod("ValidateLayout", flags)?.Invoke(panel, null);
            var renderMethod = pt.GetMethod("Render", flags, null, Type.EmptyTypes, null);
            if (renderMethod != null)
            {
                renderMethod.Invoke(panel, null);
            }
            else
            {
                var utilType = typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
                var renderPanel = utilType?.GetMethod("RenderPanel", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                renderPanel?.Invoke(null, new object[] { panel, false });
            }
        }

        private static Texture2D RenderComposite(Camera cam, Texture2D uiTex, int worldWidth, float orthoSize)
        {
            var worldRt = new RenderTexture(worldWidth, Height, 24, RenderTextureFormat.ARGB32);
            worldRt.Create();

            var prevTarget = cam.targetTexture;
            var prevRect = cam.rect;
            var prevOrtho = cam.orthographicSize;

            cam.targetTexture = worldRt;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.orthographicSize = orthoSize;
            cam.Render();

            cam.targetTexture = prevTarget;
            cam.rect = prevRect;
            cam.orthographicSize = prevOrtho;

            var leftTex = ReadRt(worldRt);
            worldRt.Release();
            UnityEngine.Object.Destroy(worldRt);

            var full = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var paper32 = (Color32)RisoPalette.Paper;
            var dst = new Color32[Width * Height];
            for (var i = 0; i < dst.Length; i++)
            {
                dst[i] = paper32;
            }

            var leftPixels = leftTex.GetPixels32();
            for (var y = 0; y < Height; y++)
            {
                var srcRow = y * worldWidth;
                var dstRow = y * Width;
                for (var x = 0; x < worldWidth; x++)
                {
                    dst[dstRow + x] = leftPixels[srcRow + x];
                }
            }

            UnityEngine.Object.Destroy(leftTex);

            var uiPixels = uiTex.GetPixels32();
            for (var i = 0; i < dst.Length && i < uiPixels.Length; i++)
            {
                var a = uiPixels[i].a;
                if (a == 0)
                {
                    continue;
                }

                if (a == 255)
                {
                    dst[i] = uiPixels[i];
                }
                else
                {
                    var af = a / 255f;
                    var inv = 1f - af;
                    dst[i] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(uiPixels[i].r * af + dst[i].r * inv), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(uiPixels[i].g * af + dst[i].g * inv), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(uiPixels[i].b * af + dst[i].b * inv), 0, 255),
                        255);
                }
            }

            full.SetPixels32(dst);
            full.Apply();
            return full;
        }

        private static int CountAlphaPixels(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            var count = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 10)
                {
                    count++;
                }
            }

            return count;
        }

        private static Texture2D ReadRt(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static byte[] EncodeRgbPng(Texture2D texture)
        {
            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels32();
            var stride = width * 3 + 1;
            var raw = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var srcRow = (height - 1 - y) * width;
                var dstRow = y * stride;
                raw[dstRow] = 0;
                for (var x = 0; x < width; x++)
                {
                    var p = pixels[srcRow + x];
                    raw[dstRow + 1 + x * 3] = p.r;
                    raw[dstRow + 2 + x * 3] = p.g;
                    raw[dstRow + 3 + x * 3] = p.b;
                }
            }

            using var output = new MemoryStream();
            output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            var ihdr = new byte[13];
            WriteBe32(ihdr, 0, (uint)width);
            WriteBe32(ihdr, 4, (uint)height);
            ihdr[8] = 8;
            ihdr[9] = 2;
            WritePngChunk(output, "IHDR", ihdr);
            WritePngChunk(output, "IDAT", CompressZlib(raw));
            WritePngChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        private static byte[] CompressZlib(byte[] raw)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte(0x9C);
            using (var def = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
            {
                def.Write(raw, 0, raw.Length);
            }

            uint s1 = 1, s2 = 0;
            for (var i = 0; i < raw.Length; i++)
            {
                s1 = (s1 + raw[i]) % 65521;
                s2 = (s2 + s1) % 65521;
            }

            var adler = new byte[4];
            WriteBe32(adler, 0, (s2 << 16) | s1);
            ms.Write(adler, 0, 4);
            return ms.ToArray();
        }

        private static void WritePngChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBe32(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var tag = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tag, 0, 4);
            if (data.Length > 0)
            {
                s.Write(data, 0, data.Length);
            }

            var crc = 0xFFFFFFFFu;
            crc = UpdateCrc(crc, tag);
            crc = UpdateCrc(crc, data);
            var crcBytes = new byte[4];
            WriteBe32(crcBytes, 0, crc ^ 0xFFFFFFFFu);
            s.Write(crcBytes, 0, 4);
        }

        private static uint UpdateCrc(uint crc, byte[] buf)
        {
            for (var i = 0; i < buf.Length; i++)
            {
                crc ^= buf[i];
                for (var b = 0; b < 8; b++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }

            return crc;
        }

        private static void WriteBe32(byte[] buf, int offset, uint val)
        {
            buf[offset + 0] = (byte)((val >> 24) & 0xFF);
            buf[offset + 1] = (byte)((val >> 16) & 0xFF);
            buf[offset + 2] = (byte)((val >> 8) & 0xFF);
            buf[offset + 3] = (byte)(val & 0xFF);
        }
    }
}
