using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeistSim.Market2D.Editor
{
    /// <summary>
    /// Configures baked 2D Riso Press sprites with exact isometric pivots, builds
    /// the playable Market2D.unity prototype scene, and captures 1600x900 visual proof.
    /// </summary>
    public static class Market2DBuilder
    {
        /// <summary>Path to the generated 2D Riso prototype scene.</summary>
        public const string ScenePath = "Assets/HeistSim/Market2D/Market2D.unity";

        /// <summary>Path to the sprite manifest emitted by tools/riso-press/press_cli.py.</summary>
        public const string ManifestPath = "Assets/HeistSim/Market2D/Sprites/sprite-manifest.json";

        /// <summary>Path to the UI Toolkit UXML layout.</summary>
        public const string UxmlPath = "Assets/HeistSim/Market2D/UI/Market2DHud.uxml";

        /// <summary>Path to the UI Toolkit Theme Style Sheet.</summary>
        public const string ThemePath = "Assets/HeistSim/Market2D/UI/Market2DTheme.tss";

        /// <summary>Path to the UI Toolkit PanelSettings asset.</summary>
        public const string PanelSettingsPath = "Assets/HeistSim/Market2D/UI/Market2DPanelSettings.asset";

        /// <summary>Directory for Market2D visual review captures.</summary>
        public const string CapturesDir = "Assets/HeistSim/Market2D/Captures";

        /// <summary>Default review capture width in pixels.</summary>
        public const int CaptureWidth = 1600;

        /// <summary>Default review capture height in pixels.</summary>
        public const int CaptureHeight = 900;

        private readonly struct SpriteMeta
        {
            public SpriteMeta(string path, float pivotX, float pivotY, float ppu)
            {
                Path = path;
                PivotX = pivotX;
                PivotY = pivotY;
                PixelsPerUnit = ppu;
            }

            public string Path { get; }
            public float PivotX { get; }
            public float PivotY { get; }
            public float PixelsPerUnit { get; }
        }

        /// <summary>Imports all baked riso sprites with exact pivots and builds Market2D.unity.</summary>
        [MenuItem("Heist/Market 2D/Build Prototype Scene")]
        public static void BuildPrototype()
        {
            AssetDatabase.Refresh();
            EnsureFolder(CapturesDir);

            var spriteMetas = ReadManifest();
            ConfigureSpriteImporters(spriteMetas);
            ConfigureUiDitherTextures();
            ConfigureSpriteStackTextures();
            ConfigureSpriteCookPropsAndIcons();

            AssetDatabase.Refresh();
            Market2DSpriteAtlasPrebuild.EnsureAtlases(packForEditorPreview: true);

            var panelSettings = EnsurePanelSettings();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                throw new InvalidOperationException("Missing UXML asset at " + UxmlPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Market2D");
            var runner = root.AddComponent<Market2DRunner>();
            runner.audioFloor = root.AddComponent<Market2DAudio>();
            runner.feel = root.AddComponent<Market2DFeel>();
            runner.seeThrough = root.AddComponent<Market2DSeeThrough>();
            runner.hitFeel = root.AddComponent<Market2DHitFeel>();
            runner.trailSurface = root.AddComponent<Market2DTrailSurface>();

            var camera = BuildCamera();
            var postFx = camera.gameObject.AddComponent<Market2DPostFx>();
            postFx.postMaterial = EnsurePostFxMaterial();
            runner.mainCamera = camera;
            runner.postFx = postFx;

            var coneMat = EnsureConeMaterial();
            var highlightMat = EnsureHighlightMaterial();
            var previewGrid = MarketGrid.Build();
            BuildRooms(root.transform);
            BuildLowWalls(root.transform, highlightMat);
            BuildFixtures(root.transform, runner, highlightMat);
            BuildCctvRigs(root.transform, coneMat, runner, previewGrid);
            BuildActors(root.transform, coneMat, highlightMat, runner, previewGrid);
            BuildHud(panelSettings, uxml, runner);

            // Frame the camera cleanly at player spawn centered inside the left split viewport.
            var startPos = Iso2DMath.TileToWorld((float)MarketData.PlayerSpawn.I, (float)MarketData.PlayerSpawn.J, 0.6f);
            camera.transform.position = new Vector3(startPos.x + 1.9f, startPos.y, -10f);
            camera.orthographicSize = Market2DRunner.DefaultOrthoSize;
            camera.rect = new Rect(0f, 0f, 1f, 1f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save scene " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Market2D build complete: " + MarketData.Rooms.Length + " rooms, "
                + MarketData.Fixtures.Length + " fixtures, scene " + ScenePath);
        }

        /// <summary>Batch-mode entry point for building the 2D prototype scene.</summary>
        public static void BuildPrototypeBatch()
        {
            try
            {
                BuildPrototype();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Market2D batch build failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Captures the 2D prototype world and split-screen UI Toolkit HUD at 1600x900.</summary>
        [MenuItem("Heist/Market 2D/Capture Prototype")]
        public static void CapturePrototype()
        {
            if (!File.Exists(ScenePath))
            {
                BuildPrototype();
            }

            EnsureFolder(CapturesDir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("No camera in " + ScenePath);
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var doc = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            var hudController = UnityEngine.Object.FindAnyObjectByType<Market2DHudController>();

            // Populate representative mid-heist snapshot state for visual QA of both HUD and world.
            if (hudController != null && doc != null && doc.rootVisualElement != null)
            {
                hudController.BindElements();
                var sampleProfile = GuardProfile.CreateDefault("standard");
                var sampleGame = new HeistGame(sampleProfile, GuardArchitecture.StateTree, guardCount: 2);
                var snap = sampleGame.Snapshot();
                var sampleHint = new MarketActionHint(true, "Stealing", 1.8f, snap.Player, "Hold E · Steal Bio Bananas & Carrots (€22)");
                hudController.PushLog("LOOT", "Cased Entrance · Obst & Gemüse");
                hudController.PushLog("CCTV", "CAM-01 sweeping north aisle");
                hudController.Refresh(snap, sampleHint);
            }

            // 1. Render the left isometric 2D world onto a cream-paper 1600x900 RenderTexture.
            var worldRt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            worldRt.Create();

            var prevActive = RenderTexture.active;
            RenderTexture.active = worldRt;
            GL.Clear(true, true, RisoPalette.Paper);

            var prevTarget = camera.targetTexture;
            var prevRect = camera.rect;
            camera.targetTexture = worldRt;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.Render();
            camera.targetTexture = prevTarget;
            camera.rect = prevRect;

            var worldTex = ReadRt(worldRt);
            File.WriteAllBytes(CapturesDir + "/Market2DWorld.png", EncodeRgbPng(worldTex));

            // 2. Render UI Toolkit UIDocument into an alpha-transparent RenderTexture and composite.
            var uiNonTransparent = 0;
            if (panelSettings != null && doc != null)
            {
                var uiRt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
                uiRt.Create();

                var prevUiRt = panelSettings.targetTexture;
                var prevClear = panelSettings.clearColor;
                var prevClearCol = panelSettings.colorClearValue;
                try
                {
                    panelSettings.clearColor = true;
                    panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
                    panelSettings.targetTexture = uiRt;

                    // Force layout + repaint on the runtime panel in batch mode.
                    RepaintRuntimePanel(doc, uiRt);

                    var uiTex = ReadRt(uiRt);
                    try
                    {
                        uiNonTransparent = CompositeAlphaOver(worldTex, uiTex);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(uiTex);
                    }
                }
                finally
                {
                    panelSettings.targetTexture = prevUiRt;
                    panelSettings.clearColor = prevClear;
                    panelSettings.colorClearValue = prevClearCol;
                    uiRt.Release();
                    UnityEngine.Object.DestroyImmediate(uiRt);
                }
            }

            var outPath = CapturesDir + "/Market2D.png";
            File.WriteAllBytes(outPath, EncodeRgbPng(worldTex));

            RenderTexture.active = prevActive;
            worldRt.Release();
            UnityEngine.Object.DestroyImmediate(worldRt);
            UnityEngine.Object.DestroyImmediate(worldTex);
            AssetDatabase.Refresh();

            Debug.Log("Market2D capture wrote " + Path.GetFullPath(outPath)
                + " (uiPixels=" + uiNonTransparent + ")");
        }

        /// <summary>Batch-mode entry point for capturing the 2D prototype screenshot.</summary>
        public static void CapturePrototypeBatch()
        {
            try
            {
                CapturePrototype();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Market2D batch capture failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void RepaintRuntimePanel(UIDocument doc, RenderTexture target)
        {
            var root = doc.rootVisualElement;
            if (root == null)
            {
                return;
            }

            var panel = root.panel;
            if (panel == null)
            {
                return;
            }

            var panelType = panel.GetType();
            var prevActive = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));

            // Call ValidateLayout + Repaint via reflection on BaseVisualElementPanel / RuntimePanel.
            var validateLayout = panelType.GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            validateLayout?.Invoke(panel, null);

            var updateAnimations = panelType.GetMethod("UpdateAnimations", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            updateAnimations?.Invoke(panel, null);

            var updateBindings = panelType.GetMethod("UpdateBindings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            updateBindings?.Invoke(panel, null);

            var repaint = panelType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Event) }, null);
            if (repaint != null)
            {
                repaint.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
            }

            RenderTexture.active = prevActive;
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

        private static int CompositeAlphaOver(Texture2D dst, Texture2D src)
        {
            var dp = dst.GetPixels32();
            var sp = src.GetPixels32();
            var count = 0;
            for (var i = 0; i < dp.Length && i < sp.Length; i++)
            {
                var a = sp[i].a;
                if (a == 0)
                {
                    continue;
                }

                count++;
                if (a == 255)
                {
                    dp[i] = sp[i];
                }
                else
                {
                    var af = a / 255f;
                    var inv = 1f - af;
                    dp[i] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(sp[i].r * af + dp[i].r * inv), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(sp[i].g * af + dp[i].g * inv), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(sp[i].b * af + dp[i].b * inv), 0, 255),
                        255);
                }
            }

            dst.SetPixels32(dp);
            dst.Apply();
            return count;
        }

        private static List<SpriteMeta> ReadManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                throw new InvalidOperationException("Missing sprite manifest at " + ManifestPath
                    + ". Run `python3 tools/riso-press/press_cli.py export-unity` first.");
            }

            var json = File.ReadAllText(ManifestPath);
            var list = new List<SpriteMeta>();
            var regex = new Regex(
                @"\{[^{}]*""pivotX""\s*:\s*([-0-9.eE]+)[^{}]*""pivotY""\s*:\s*([-0-9.eE]+)[^{}]*""pixelsPerUnit""\s*:\s*([-0-9.eE]+)[^{}]*""path""\s*:\s*""([^""]+)""",
                RegexOptions.Singleline);

            foreach (Match m in regex.Matches(json))
            {
                var px = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var py = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var ppu = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                var path = m.Groups[4].Value;
                list.Add(new SpriteMeta(path, px, py, ppu));
            }

            if (list.Count == 0)
            {
                throw new InvalidOperationException("Parsed zero sprite entries from " + ManifestPath);
            }

            return list;
        }

        private static void ConfigureSpriteImporters(List<SpriteMeta> metas)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var meta in metas)
                {
                    var importer = AssetImporter.GetAtPath(meta.Path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    var changed = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }

                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }

                    if (Mathf.Abs(importer.spritePixelsPerUnit - meta.PixelsPerUnit) > 0.01f)
                    {
                        importer.spritePixelsPerUnit = meta.PixelsPerUnit;
                        changed = true;
                    }

                    if (importer.filterMode != FilterMode.Trilinear)
                    {
                        importer.filterMode = FilterMode.Trilinear;
                        changed = true;
                    }

                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        changed = true;
                    }

                    if (!importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = true;
                        changed = true;
                    }

                    if (importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter)
                    {
                        importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                        changed = true;
                    }

                    if (Mathf.Abs(importer.mipMapBias - (-0.35f)) > 0.01f)
                    {
                        importer.mipMapBias = -0.35f;
                        changed = true;
                    }

                    if (importer.anisoLevel != 4)
                    {
                        importer.anisoLevel = 4;
                        changed = true;
                    }

                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        changed = true;
                    }

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (settings.spriteAlignment != (int)SpriteAlignment.Custom
                        || Mathf.Abs(settings.spritePivot.x - meta.PivotX) > 0.0005f
                        || Mathf.Abs(settings.spritePivot.y - meta.PivotY) > 0.0005f)
                    {
                        settings.spriteAlignment = (int)SpriteAlignment.Custom;
                        settings.spritePivot = new Vector2(meta.PivotX, meta.PivotY);
                        importer.SetTextureSettings(settings);
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void ConfigureUiDitherTextures()
        {
            var tiles = new[]
            {
                "Assets/HeistSim/Market2D/Sprites/UI/dither_coral.png",
                "Assets/HeistSim/Market2D/Sprites/UI/dither_sun.png",
                "Assets/HeistSim/Market2D/Sprites/UI/dither_teal.png",
                "Assets/HeistSim/Market2D/Sprites/UI/dither_blue.png",
            };

            foreach (var path in tiles)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureSpriteStackTextures()
        {
            var sheets = new[]
            {
                "Assets/HeistSim/Market2D/Sprites/SpriteStack/cctv_stack_slices.png",
                "Assets/HeistSim/Market2D/Sprites/SpriteStack/cart_stack_slices.png",
            };

            foreach (var path in sheets)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureSpriteCookPropsAndIcons()
        {
            var uiPivot = new Vector2(0.5f, 0.5f);
            var specs = new (string Path, float Ppu, Vector2 Pivot, FilterMode Filter, bool Mipmaps)[]
            {
                ("Assets/HeistSim/Market2D/Sprites/UI/role_patrol.png", 64f, uiPivot, FilterMode.Bilinear, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/role_pursuer.png", 64f, uiPivot, FilterMode.Bilinear, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/role_interceptor.png", 64f, uiPivot, FilterMode.Bilinear, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/role_sabotage.png", 64f, uiPivot, FilterMode.Bilinear, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/role_sweeper.png", 64f, uiPivot, FilterMode.Bilinear, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/icon_spirits_trophy.png", 64f, uiPivot, FilterMode.Bilinear, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/icon_dossier.png", 32f, uiPivot, FilterMode.Point, false),
                ("Assets/HeistSim/Market2D/Sprites/UI/icon_loot_bag.png", 32f, uiPivot, FilterMode.Point, false),
            };

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var s in specs)
                {
                    ConfigureSingleSpriteImporter(s.Path, s.Ppu, s.Pivot, s.Filter, s.Mipmaps);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void ConfigureSingleSpriteImporter(
            string path,
            float ppu,
            Vector2 pivot,
            FilterMode filter,
            bool mipmaps)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (Mathf.Abs(importer.spritePixelsPerUnit - ppu) > 0.01f)
            {
                importer.spritePixelsPerUnit = ppu;
                changed = true;
            }

            if (importer.filterMode != filter)
            {
                importer.filterMode = filter;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.mipmapEnabled != mipmaps)
            {
                importer.mipmapEnabled = mipmaps;
                changed = true;
            }

            if (mipmaps)
            {
                if (importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter)
                {
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    changed = true;
                }

                if (Mathf.Abs(importer.mipMapBias - (-0.35f)) > 0.01f)
                {
                    importer.mipMapBias = -0.35f;
                    changed = true;
                }

                if (importer.anisoLevel != 4)
                {
                    importer.anisoLevel = 4;
                    changed = true;
                }
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom
                || Mathf.Abs(settings.spritePivot.x - pivot.x) > 0.0005f
                || Mathf.Abs(settings.spritePivot.y - pivot.y) > 0.0005f)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(CaptureWidth, CaptureHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearDepthStencil = true;
            settings.clearColor = false;

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null)
            {
                settings.themeStyleSheet = theme;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static Material EnsureConeMaterial()
        {
            var dir = "Assets/HeistSim/Market2D/Generated";
            EnsureFolder(dir);
            var path = dir + "/Iso2DCone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default")) { name = "Iso2DCone" };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureHighlightMaterial()
        {
            var dir = "Assets/HeistSim/Market2D/Generated";
            EnsureFolder(dir);
            var path = dir + "/RisoSpriteHighlight.mat";
            var shader = Shader.Find("HeistSim/RisoSpriteHighlight") ?? Shader.Find("Sprites/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "RisoSpriteHighlight" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_HighlightAmount", 0f);
            mat.SetFloat("_OutlineWidth", 3.0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsurePostFxMaterial()
        {
            var dir = "Assets/HeistSim/Market2D/Generated";
            EnsureFolder(dir);
            var path = dir + "/Market2DPostFx.mat";
            var shader = Shader.Find("HeistSim/Market2DPostFx");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null && shader != null)
            {
                mat = new Material(shader) { name = "Market2DPostFx" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat != null && shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat != null)
            {
                mat.SetFloat("_CrtEnabled", 0f);
                mat.SetFloat("_TransProgress", 0f);
                mat.SetFloat("_MoodPhase", 0f);
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Market2DRunner.DefaultOrthoSize;
            cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RisoPalette.Paper;
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 50f;
            return cam;
        }

        private static void BuildRooms(Transform parent)
        {
            var group = new GameObject("Rooms").transform;
            group.SetParent(parent, false);

            foreach (var rm in MarketData.Rooms)
            {
                var sprite = LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/" + rm.Id + ".png");
                var go = new GameObject("Room_" + rm.Id + "_" + rm.Type);
                go.transform.SetParent(group, false);
                go.transform.position = Iso2DMath.TileToWorld(rm.OriginI, rm.OriginJ, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = Iso2DMath.RoomOrder(rm.AtI, rm.AtJ);
            }
        }

        private static void BuildLowWalls(Transform parent, Material highlightMat)
        {
            var group = new GameObject("LowWalls").transform;
            group.SetParent(parent, false);

            var spritesNe = new[]
            {
                LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/lowwall_ne_start.png"),
                LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/lowwall_ne_mid.png"),
                LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/lowwall_ne_end.png"),
            };
            var spritesNw = new[]
            {
                LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/lowwall_nw_start.png"),
                LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/lowwall_nw_mid.png"),
                LoadSprite("Assets/HeistSim/Market2D/Sprites/Rooms/lowwall_nw_end.png"),
            };

            const int segmentsPerWing = 5;
            const float segmentLength = 3.55f / segmentsPerWing;

            var coords = new HashSet<(int, int)>();
            foreach (var rm in MarketData.Rooms)
            {
                coords.Add((rm.AtI, rm.AtJ));
            }

            foreach (var rm in MarketData.Rooms)
            {
                if (coords.Contains((rm.AtI, rm.AtJ - 1)))
                {
                    PlaceLowWallWing(group, spritesNe, highlightMat, rm.Id + "_ne_a", rm.OriginI, rm.OriginJ, alongI: true, segmentsPerWing, segmentLength);
                    PlaceLowWallWing(group, spritesNe, highlightMat, rm.Id + "_ne_b", rm.OriginI + 5.45f, rm.OriginJ, alongI: true, segmentsPerWing, segmentLength);
                }

                if (coords.Contains((rm.AtI - 1, rm.AtJ)))
                {
                    PlaceLowWallWing(group, spritesNw, highlightMat, rm.Id + "_nw_a", rm.OriginI, rm.OriginJ, alongI: false, segmentsPerWing, segmentLength);
                    PlaceLowWallWing(group, spritesNw, highlightMat, rm.Id + "_nw_b", rm.OriginI, rm.OriginJ + 5.45f, alongI: false, segmentsPerWing, segmentLength);
                }
            }
        }

        private static void PlaceLowWallWing(
            Transform parent,
            Sprite[] partSprites,
            Material highlightMat,
            string wingName,
            float startI,
            float startJ,
            bool alongI,
            int segmentCount,
            float segmentLength)
        {
            for (var s = 0; s < segmentCount; s++)
            {
                var partIdx = s == 0 ? 0 : (s == segmentCount - 1 ? 2 : 1);
                var minI = alongI ? startI + s * segmentLength : startI;
                var maxI = alongI ? minI + segmentLength : startI;
                var minJ = alongI ? startJ : startJ + s * segmentLength;
                var maxJ = alongI ? startJ : minJ + segmentLength;
                var midI = (minI + maxI) * 0.5f;
                var midJ = (minJ + maxJ) * 0.5f;

                var go = new GameObject("LowWall_" + wingName + "_" + s);
                go.transform.SetParent(parent, false);
                go.transform.position = Iso2DMath.TileToWorld(minI, minJ, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = partSprites[partIdx];
                sr.sharedMaterial = highlightMat;
                sr.sortingOrder = Iso2DMath.DepthOrder(midI, midJ, 0);

                var bounds = go.AddComponent<Iso2DOccluderBounds>();
                bounds.minI = minI;
                bounds.minJ = minJ;
                bounds.maxI = maxI;
                bounds.maxJ = maxJ;
                bounds.baseSortingOrder = sr.sortingOrder;
                bounds.isLowWall = true;
            }
        }

        private static void BuildFixtures(Transform parent, Market2DRunner runner, Material highlightMat)
        {
            var group = new GameObject("Fixtures").transform;
            group.SetParent(parent, false);

            var lootList = new List<LootableFixture2D>();

            foreach (var fx in MarketData.Fixtures)
            {
                if (string.Equals(fx.Kind, "aisle-sign", StringComparison.Ordinal))
                {
                    continue;
                }

                var room = MarketData.Room(fx.Room);
                var kind = MarketData.Kind(fx.Kind);
                var gi = (float)(room.OriginI + fx.I);
                var gj = (float)(room.OriginJ + fx.J);
                var face = fx.Face == "nw" ? "nw" : "ne";
                var basePath = "Assets/HeistSim/Market2D/Sprites/Fixtures/" + fx.Kind + "__" + face;
                var sprite = LoadSprite(basePath + ".png");

                MarketLayout.Footprint(kind, fx.Face, out var wi, out var dj);
                var go = new GameObject(fx.Id + "_" + fx.Kind);
                go.transform.SetParent(group, false);
                go.transform.position = Iso2DMath.TileToWorld(gi, gj, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = highlightMat;
                sr.sortingOrder = Iso2DMath.DepthOrder(gi + wi * 0.5f, gj + dj * 0.5f, 0);
                if (kind.Height >= 0.75)
                {
                    var ob = go.AddComponent<Iso2DOccluderBounds>();
                    ob.minI = gi;
                    ob.minJ = gj;
                    ob.maxI = gi + wi;
                    ob.maxJ = gj + dj;
                    ob.baseSortingOrder = sr.sortingOrder;
                    ob.isLowWall = false;
                }

                var lootDef = MarketData.LootFor(fx.Id);
                if (lootDef.HasValue)
                {
                    var lf = go.AddComponent<LootableFixture2D>();
                    lf.fixtureId = fx.Id;
                    lf.isRfTagged = lootDef.Value.Tagged;
                    lf.isVitrineGated = lootDef.Value.VitrineGated;
                    lf.stockedSprite = sprite;
                    lf.emptySprite = LoadSprite(basePath + "__empty.png");
                    if (lootDef.Value.VitrineGated)
                    {
                        lf.unlockedSprite = LoadSprite(basePath + "__open.png");
                    }

                    lf.spriteRenderer = sr;
                    lootList.Add(lf);
                }
            }

            runner.lootFixtures = lootList.ToArray();
        }

        private static void BuildCctvRigs(Transform parent, Material coneMat, Market2DRunner runner, NavGrid grid)
        {
            var group = new GameObject("Cameras").transform;
            group.SetParent(parent, false);

            var mountSprite = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/cctv_mount.png");
            var headFrames = LoadSpriteSequence("Assets/HeistSim/Market2D/Sprites/Actors/cctv_head_", 8);
            var rigs = new Market2DCctvRig[MarketData.Cameras.Length];
            var cones = new Iso2DVisionCone[MarketData.Cameras.Length];

            for (var i = 0; i < MarketData.Cameras.Length; i++)
            {
                var def = MarketData.Cameras[i];
                var go = new GameObject(def.Id);
                go.transform.SetParent(group, false);
                go.transform.position = Iso2DMath.TileToWorld((float)def.I, (float)def.J, 0f);

                var rig = go.AddComponent<Market2DCctvRig>();
                rig.headFrames = headFrames;
                rig.lensHeightWorld = 0.56f;

                var mountGo = new GameObject("Mount");
                mountGo.transform.SetParent(go.transform, false);
                mountGo.transform.localPosition = Vector3.zero;
                var mountSr = mountGo.AddComponent<SpriteRenderer>();
                mountSr.sprite = mountSprite;
                mountSr.sortingOrder = Iso2DMath.DepthOrder((float)def.I, (float)def.J, 28);
                rig.mountRenderer = mountSr;

                var headGo = new GameObject("TurretHead");
                headGo.transform.SetParent(go.transform, false);
                headGo.transform.localPosition = new Vector3(0f, rig.lensHeightWorld, 0f);
                var headSr = headGo.AddComponent<SpriteRenderer>();
                headSr.sprite = headFrames[0];
                headSr.sortingOrder = Iso2DMath.DepthOrder((float)def.I, (float)def.J, 32);
                rig.headRenderer = headSr;

                var tetherGo = new GameObject("BeamTether");
                tetherGo.transform.SetParent(go.transform, false);
                tetherGo.transform.localPosition = Vector3.zero;
                var tether = tetherGo.AddComponent<LineRenderer>();
                tether.useWorldSpace = false;
                tether.loop = false;
                tether.widthMultiplier = 0.024f;
                tether.numCornerVertices = 2;
                tether.numCapVertices = 2;
                tether.sharedMaterial = coneMat;
                tether.sortingOrder = Iso2DMath.ConeSortingOrder + 1;
                rig.beamTether = tether;

                var coneObj = new GameObject(def.Id + "_Cone");
                coneObj.transform.SetParent(go.transform, false);
                coneObj.transform.localPosition = Vector3.zero;
                var cone = coneObj.AddComponent<Iso2DVisionCone>();
                cone.EnsureMesh(coneMat);
                rig.visionCone = cone;

                rig.Sync((float)def.I, (float)def.J, (float)def.BaseAngle, (float)def.HalfAngle, (float)def.Range, online: true, sighting: false, grid);
                rigs[i] = rig;
                cones[i] = cone;
            }

            // Place a visible Security Power Panel fixture at MarketData.Panel (29.0, 25.5 in r6) so the player can find & cut CCTV power
            var highlightMat = EnsureHighlightMaterial();
            var panelSprite = LoadSprite("Assets/HeistSim/Market2D/Sprites/Props/prop_cctv_panel.png")
                ?? LoadSprite("Assets/HeistSim/Market2D/Sprites/Fixtures/server-rack__ne.png");
            if (panelSprite != null)
            {
                var panelGo = new GameObject("CctvPowerPanel");
                panelGo.transform.SetParent(parent, false);
                panelGo.transform.position = Iso2DMath.TileToWorld((float)MarketData.Panel.I, (float)MarketData.Panel.J, 0f);
                panelGo.transform.localScale = Vector3.one;
                var panelSr = panelGo.AddComponent<SpriteRenderer>();
                panelSr.sprite = panelSprite;
                panelSr.sharedMaterial = highlightMat;
                panelSr.sortingOrder = Iso2DMath.DepthOrder((float)MarketData.Panel.I, (float)MarketData.Panel.J, 4);
                runner.cctvPanelSprite = panelSr;
            }

            // Place the unified Riso RF De-Tagger pad at MarketData.Detagger
            var detaggerSprite = LoadSprite("Assets/HeistSim/Market2D/Sprites/Props/prop_detagger.png");
            var detaggerGo = new GameObject("RfDetaggerPad");
            detaggerGo.transform.SetParent(parent, false);
            detaggerGo.transform.position = Iso2DMath.TileToWorld((float)MarketData.Detagger.I, (float)MarketData.Detagger.J, 0f);
            detaggerGo.transform.localScale = Vector3.one;
            var detaggerSr = detaggerGo.AddComponent<SpriteRenderer>();
            detaggerSr.sprite = detaggerSprite;
            detaggerSr.sharedMaterial = highlightMat;
            detaggerSr.sortingOrder = Iso2DMath.DepthOrder((float)MarketData.Detagger.I, (float)MarketData.Detagger.J, 4);
            runner.detaggerPadSprite = detaggerSr;

            // Create hidden reusable acoustic decoy coin visual for TossDecoyCoin [Q]
            var decoyGo = new GameObject("DecoyCoin");
            decoyGo.transform.SetParent(parent, false);
            decoyGo.transform.localScale = new Vector3(0.78f, 0.78f, 1f);
            var decoySr = decoyGo.AddComponent<SpriteRenderer>();
            decoySr.sprite = LoadSprite("Assets/HeistSim/Market2D/Sprites/Props/prop_decoy_coin.png");
            decoySr.enabled = false;
            decoySr.sortingOrder = Iso2DMath.DepthOrder(0f, 0f, 6);
            runner.decoyCoinSprite = decoySr;

            runner.cctvRigs = rigs;
            runner.cctvCones = cones;
            runner.cctvStacks = Array.Empty<SpriteStack2D>();
        }

        private static void BuildActors(Transform parent, Material coneMat, Material actorMat, Market2DRunner runner, NavGrid grid)
        {
            var group = new GameObject("Actors").transform;
            group.SetParent(parent, false);

            var playerStand = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/player.png");
            var playerWalk1 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/player_walk1.png");
            var playerWalk2 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/player_walk2.png");
            var playerCrouch = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/player_crouch.png");
            var playerCrouchWalk1 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/player_crouch_walk1.png");
            var playerCrouchWalk2 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/player_crouch_walk2.png");
            var guardSp = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/guard.png");
            var guardWalk1 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/guard_walk1.png");
            var guardWalk2 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/guard_walk2.png");
            var guard2Sp = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/guard2.png");
            var staffSp = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/staff.png");
            var staffWalk1 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/staff_walk1.png");
            var staffWalk2 = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/staff_walk2.png");
            var alertSp = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/bubble_alert.png");
            var questionSp = LoadSprite("Assets/HeistSim/Market2D/Sprites/Actors/bubble_question.png");

            // Player
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(group, false);
            playerGo.transform.position = Iso2DMath.TileToWorld((float)MarketData.PlayerSpawn.I, (float)MarketData.PlayerSpawn.J);
            var playerSr = playerGo.AddComponent<SpriteRenderer>();
            playerSr.sprite = playerStand;
            playerSr.sharedMaterial = actorMat;
            playerSr.sortingOrder = Iso2DMath.DepthOrder((float)MarketData.PlayerSpawn.I, (float)MarketData.PlayerSpawn.J, 1);

            runner.playerSprite = playerSr;
            runner.playerStandSprite = playerStand;
            runner.playerWalk1Sprite = playerWalk1;
            runner.playerWalk2Sprite = playerWalk2;
            runner.playerCrouchSprite = playerCrouch;
            runner.playerCrouchWalk1Sprite = playerCrouchWalk1;
            runner.playerCrouchWalk2Sprite = playerCrouchWalk2;
            runner.playerWalkFrames = LoadSpriteSequence("Assets/HeistSim/Market2D/Sprites/Actors/player_walk_", 6);
            runner.playerCrouchWalkFrames = LoadSpriteSequence("Assets/HeistSim/Market2D/Sprites/Actors/player_crouch_walk_", 6);

            // Guard
            var guardPos = HeistGame.GuardStart;
            var guardGo = new GameObject("Guard");
            guardGo.transform.SetParent(group, false);
            guardGo.transform.position = Iso2DMath.TileToWorld(guardPos);
            var guardSr = guardGo.AddComponent<SpriteRenderer>();
            guardSr.sprite = guardSp;
            guardSr.sharedMaterial = actorMat;
            guardSr.sortingOrder = Iso2DMath.DepthOrder(guardPos.x, guardPos.y, 2);

            var bubbleGo = new GameObject("GuardBubble");
            bubbleGo.transform.SetParent(guardGo.transform, false);
            bubbleGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var bubbleSr = bubbleGo.AddComponent<SpriteRenderer>();
            bubbleSr.sprite = alertSp;
            bubbleSr.enabled = false;
            bubbleSr.sortingOrder = guardSr.sortingOrder + 60;

            var coneGo = new GameObject("GuardVisionCone");
            coneGo.transform.SetParent(group, false);
            var guardCone = coneGo.AddComponent<Iso2DVisionCone>();
            guardCone.EnsureMesh(coneMat);
            guardCone.SetFan(guardPos.x, guardPos.y, 1.5f, 0.60f, 5.4f,
                new Color(RisoPalette.Teal.r, RisoPalette.Teal.g, RisoPalette.Teal.b, 0.22f), grid);

            runner.guardSprite = guardSr;
            runner.guardStandSprite = guardSp;
            runner.guardWalk1Sprite = guardWalk1;
            runner.guardWalk2Sprite = guardWalk2;
            runner.guardWalkFrames = LoadSpriteSequence("Assets/HeistSim/Market2D/Sprites/Actors/guard_walk_", 6);
            runner.guardBubbleSprite = bubbleSr;
            runner.alertBubbleSprite = alertSp;
            runner.questionBubbleSprite = questionSp;
            runner.guardVisionCone = guardCone;

            // Guard 2 (distinct SpriteCook officer covering south/east wing)
            var guard2Pos = HeistGame.Guard2Start;
            var guard2Go = new GameObject("Guard2");
            guard2Go.transform.SetParent(group, false);
            guard2Go.transform.position = Iso2DMath.TileToWorld(guard2Pos);
            var guard2Sr = guard2Go.AddComponent<SpriteRenderer>();
            guard2Sr.sprite = guard2Sp;
            guard2Sr.sharedMaterial = actorMat;
            guard2Sr.color = Color.white;
            guard2Sr.sortingOrder = Iso2DMath.DepthOrder(guard2Pos.x, guard2Pos.y, 3);

            var bubble2Go = new GameObject("Guard2Bubble");
            bubble2Go.transform.SetParent(guard2Go.transform, false);
            bubble2Go.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var bubble2Sr = bubble2Go.AddComponent<SpriteRenderer>();
            bubble2Sr.sprite = alertSp;
            bubble2Sr.enabled = false;
            bubble2Sr.sortingOrder = guard2Sr.sortingOrder + 60;

            var cone2Go = new GameObject("Guard2VisionCone");
            cone2Go.transform.SetParent(group, false);
            var guard2Cone = cone2Go.AddComponent<Iso2DVisionCone>();
            guard2Cone.EnsureMesh(coneMat);
            guard2Cone.SetFan(guard2Pos.x, guard2Pos.y, 3.14f, 0.60f, 5.4f,
                new Color(RisoPalette.Teal.r, RisoPalette.Teal.g, RisoPalette.Teal.b, 0.22f), grid);

            runner.guard2Sprite = guard2Sr;
            runner.guard2StandSprite = guard2Sp;
            runner.guard2WalkFrames = LoadSpriteSequence("Assets/HeistSim/Market2D/Sprites/Actors/guard2_walk_", 6);
            runner.guard2BubbleSprite = bubble2Sr;
            runner.guard2VisionCone = guard2Cone;

            // Staff
            var staffStart = MarketData.StaffRoute[0];
            var staffPos = new Vector2(staffStart.X + 0.5f + (float)staffStart.OffsetI, staffStart.Y + 0.5f);
            var staffGo = new GameObject("Staff");
            staffGo.transform.SetParent(group, false);
            staffGo.transform.position = Iso2DMath.TileToWorld(staffPos);
            var staffSr = staffGo.AddComponent<SpriteRenderer>();
            staffSr.sprite = staffSp;
            staffSr.sortingOrder = Iso2DMath.DepthOrder(staffPos.x, staffPos.y, 0);

            runner.staffSprite = staffSr;
            runner.staffStandSprite = staffSp;
            runner.staffWalk1Sprite = staffWalk1;
            runner.staffWalk2Sprite = staffWalk2;
            runner.staffWalkFrames = LoadSpriteSequence("Assets/HeistSim/Market2D/Sprites/Actors/staff_walk_", 6);
        }

        private static void BuildHud(PanelSettings panelSettings, VisualTreeAsset uxml, Market2DRunner runner)
        {
            // Reload from AssetDatabase after NewScene so references are guaranteed valid persistent assets.
            panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Debug.Log("BuildHud assets: ps=" + (panelSettings != null ? panelSettings.name : "NULL")
                + " uxml=" + (uxml != null ? uxml.name : "NULL"));

            var hudGo = new GameObject("Market2D HUD");
            hudGo.SetActive(false);
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;

            var so = new SerializedObject(doc);
            so.Update();
            var psProp = so.FindProperty("m_PanelSettings");
            var srcProp = so.FindProperty("sourceAsset");
            if (psProp != null)
            {
                psProp.objectReferenceValue = panelSettings;
            }

            if (srcProp != null)
            {
                srcProp.objectReferenceValue = uxml;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            hudGo.SetActive(true);
            EditorUtility.SetDirty(doc);

            Debug.Log("BuildHud bound: doc.ps=" + (doc.panelSettings != null ? doc.panelSettings.name : "NULL")
                + " doc.uxml=" + (doc.visualTreeAsset != null ? doc.visualTreeAsset.name : "NULL")
                + " psProp=" + (psProp != null) + " srcProp=" + (srcProp != null));

            if (doc.panelSettings == null || doc.visualTreeAsset == null)
            {
                throw new InvalidOperationException("Failed to bind PanelSettings or VisualTreeAsset on UIDocument.");
            }

            var controller = hudGo.AddComponent<Market2DHudController>();
            controller.rolePatrolIcon = LoadSprite("Assets/HeistSim/Market2D/Sprites/UI/role_patrol.png");
            controller.rolePursuerIcon = LoadSprite("Assets/HeistSim/Market2D/Sprites/UI/role_pursuer.png");
            controller.roleInterceptorIcon = LoadSprite("Assets/HeistSim/Market2D/Sprites/UI/role_interceptor.png");
            controller.roleSabotageIcon = LoadSprite("Assets/HeistSim/Market2D/Sprites/UI/role_sabotage.png");
            controller.roleSweeperIcon = LoadSprite("Assets/HeistSim/Market2D/Sprites/UI/role_sweeper.png");
            controller.spiritsTrophyIcon = LoadSprite("Assets/HeistSim/Market2D/Sprites/UI/icon_spirits_trophy.png");
            controller.dossierIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/HeistSim/Market2D/Sprites/UI/icon_dossier.png");
            controller.lootBagIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/HeistSim/Market2D/Sprites/UI/icon_loot_bag.png");
            EditorUtility.SetDirty(controller);
            runner.hud = controller;
        }

        private static Sprite[] LoadSpriteSequence(string pathPrefix, int count)
        {
            var seq = new Sprite[count];
            for (var i = 0; i < count; i++)
            {
                seq[i] = LoadSprite(pathPrefix + i + ".png");
            }

            return seq;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("Failed to load 2D sprite at " + path);
            }

            return sprite;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
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
