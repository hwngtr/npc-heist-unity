using System;
using System.Collections.Generic;
using System.IO;
using HeistSim.Arena;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HeistSim.Market.Editor
{
    /// <summary>Builds the playable Frischemarkt scene from MarketData with the LookD post look.</summary>
    public static class MarketLevelBuilder
    {
        private const string FontAssetPath = MarketSceneSpec.ResourcesDir + "/MarketSans.asset";
        private const string FontSourcePath = MarketSceneSpec.TextMeshProDir + "/Fonts/LiberationSans.ttf";
        private const string LeadingCharactersPath = MarketSceneSpec.ResourcesDir + "/LineBreaking Leading Characters.txt";
        private const string FollowingCharactersPath = MarketSceneSpec.ResourcesDir + "/LineBreaking Following Characters.txt";
        private const string TmpSettingsPath = MarketSceneSpec.ResourcesDir + "/TMP Settings.asset";

        /// <summary>Gap kept between a camera visual and the floor edge, in tiles.</summary>
        private const float CameraEdgeInset = 0.5f;

        /// <summary>Perimeter inset used by MarketGrid.IsInsideBuilding, mirrored by the divider stubs.</summary>
        private const float OuterInset = 0.75f;

        /// <summary>Group that holds one thin floor panel per MarketData.Rooms entry.</summary>
        private const string RoomFloorsGroup = "Room Floors";

        /// <summary>Group that holds the seam stubs between adjacent rooms.</summary>
        private const string RoomDividersGroup = "Room Dividers";

        /// <summary>Rebuilds the market scene and saves it over the existing asset.</summary>
        [MenuItem("Heist/Market/Build Level Scene")]
        public static void BuildLevel()
        {
            AssetDatabase.Refresh();
            EnsureFolders();

            var materials = CreateMaterials();
            var font = EnsureFontAsset();
            var coneMesh = EnsureConeMesh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera(materials);
            BuildLight();
            BuildMarket(materials, coneMesh, font, camera);

            if (!EditorSceneManager.SaveScene(scene, MarketSceneSpec.ScenePath))
            {
                throw new InvalidOperationException("Failed to save scene " + MarketSceneSpec.ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Market level build complete: "
                + MarketData.Fixtures.Length + " fixtures, "
                + MarketData.Loot.Length + " loot markers, "
                + MarketData.Cameras.Length + " cameras, scene " + MarketSceneSpec.ScenePath);
        }

        /// <summary>Headless entry point for batch mode, exits non zero on failure.</summary>
        public static void BuildLevelBatch()
        {
            try
            {
                BuildLevel();
                Debug.Log("Market batch build finished.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Market batch build failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(MarketSceneSpec.MarketDir);
            EnsureFolder(MarketSceneSpec.GeneratedDir);
            EnsureFolder(MarketSceneSpec.MaterialsDir);
            EnsureFolder(MarketSceneSpec.MeshDir);
            EnsureFolder(MarketSceneSpec.CapturesDir);
            EnsureFolder(MarketSceneSpec.ResourcesDir);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["floor"] = FlatMaterial("MarketFloor", MarketSceneSpec.Card),
                ["wall"] = FlatMaterial("MarketWall", MarketSceneSpec.Paper),
                ["zoneMuted"] = FlatMaterial("MarketZoneMuted", MarketSceneSpec.Rgb(0xD6CFC0)),
                ["coral"] = FlatMaterial("MarketCoral", MarketSceneSpec.Coral),
                ["coralInk"] = FlatMaterial("MarketCoralInk", MarketSceneSpec.CoralInk),
                ["sun"] = FlatMaterial("MarketSun", MarketSceneSpec.Sun),
                ["teal"] = FlatMaterial("MarketTeal", MarketSceneSpec.Teal),
                ["inkBlue"] = FlatMaterial("MarketInkBlue", MarketSceneSpec.InkBlue),
                ["inkBlue2"] = FlatMaterial("MarketInkBlue2", MarketSceneSpec.InkBlue2),
                ["black"] = FlatMaterial("MarketBlack", MarketSceneSpec.Black),
                ["cone"] = TransparentMaterial("MarketConeTeal", MarketSceneSpec.Teal, 0.34f),
                ["post"] = DitherPostMaterial(),
            };
        }

        internal static Material FlatMaterial(string name, Color color)
        {
            var path = MarketSceneSpec.MaterialsDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(RequireShader("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", 0f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TransparentMaterial(string name, Color color, float alpha)
        {
            var path = MarketSceneSpec.MaterialsDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(RequireShader("Sprites/Default")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_Color", new Color(color.r, color.g, color.b, alpha));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material DitherPostMaterial()
        {
            var path = MarketSceneSpec.MaterialsDir + "/MarketDitherPost.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(RequireShader("HeistSim/IsoDitherPost")) { name = "MarketDitherPost" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_PaperColor", MarketSceneSpec.Paper);
            material.SetColor("_KeyInkColor", MarketSceneSpec.InkBlue);
            material.SetColor("_TealColor", MarketSceneSpec.Teal);
            material.SetColor("_SunColor", MarketSceneSpec.Sun);
            material.SetColor("_CoralColor", MarketSceneSpec.Coral);
            material.SetColor("_BotBlackColor", MarketSceneSpec.Black);
            material.SetFloat("_BayerScale", MarketSceneSpec.BayerScale);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader RequireShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                throw new InvalidOperationException("Shader not found: " + name);
            }

            return shader;
        }

        private static Camera BuildCamera(Dictionary<string, Material> materials)
        {
            var cameraObject = new GameObject(MarketSceneSpec.RootCamera) { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = MarketRunner.FollowOrthographicSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MarketSceneSpec.Paper;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var playerStart = new Vector3(HeistGame.PlayerStart.x, 0f, HeistGame.PlayerStart.y);
            cameraObject.transform.position = playerStart + MarketRunner.CameraOffset;
            cameraObject.transform.rotation = Quaternion.LookRotation(-MarketRunner.CameraOffset.normalized, Vector3.up);

            var dither = cameraObject.AddComponent<IsoDitherPost>();
            dither.postMaterial = materials["post"];
            dither.bayerScale = MarketSceneSpec.BayerScale;
            return camera;
        }

        private static void BuildLight()
        {
            var lightObject = new GameObject(MarketSceneSpec.RootLight);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 0.95f;
            light.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.40f, 0.37f);
        }

        private static void BuildMarket(Dictionary<string, Material> materials, Mesh coneMesh, TMP_FontAsset font, Camera camera)
        {
            var market = new GameObject(MarketSceneSpec.RootMarket);
            var runner = market.AddComponent<MarketRunner>();
            var content = market.transform;

            var floor = NewGroup(content, "Floor");
            Box(floor, "Floor Slab", new Vector3(18f, -0.12f, 13.5f), new Vector3(36f, 0.2f, 27f), materials["floor"]);
            BuildRoomFloors(content, materials);
            BuildDividers(content, materials);

            var walls = NewGroup(content, "Walls");
            Box(walls, "Back Wall", new Vector3(18f, 1.1f, 27.15f), new Vector3(36f, 2.2f, 0.3f), materials["wall"]);
            Box(walls, "Baseboard", new Vector3(18f, 0.17f, 26.97f), new Vector3(36f, 0.34f, 0.06f), materials["coral"]);

            BuildFixtures(content);
            BuildCameras(content, materials, coneMesh, out var cones);
            BuildLoot(content, materials);
            BuildDevices(content, materials);

            var actors = NewGroup(content, MarketSceneSpec.ActorsGroup);
            BuildActors(actors, materials, out var player, out var guard, out var staff, out var fan);

            var hud = BuildHud(camera, font);

            runner.playerRoot = player;
            runner.guardRoot = guard;
            runner.staffRoot = staff;
            runner.visionFan = fan;
            runner.hud = hud;
            runner.cameraCones = cones;
        }

        /// <summary>
        /// Paints one thin panel per room cell. Paper and card alternate across the room grid; checkout and
        /// stockroom use a muted third tone so the two service zones read apart from the sales floor.
        /// </summary>
        private static void BuildRoomFloors(Transform content, Dictionary<string, Material> materials)
        {
            var rooms = NewGroup(content, RoomFloorsGroup);
            foreach (var room in MarketData.Rooms)
            {
                var key = room.Type == "checkout" || room.Type == "stockroom"
                    ? "zoneMuted"
                    : (room.AtI + room.AtJ) % 2 == 0 ? "wall" : "floor";
                var centre = new Vector3(
                    room.OriginI + MarketData.RoomTiles * 0.5f,
                    0.01f,
                    room.OriginJ + MarketData.RoomTiles * 0.5f);
                Box(
                    rooms,
                    "Room Panel " + room.Id,
                    centre,
                    new Vector3(MarketData.RoomTiles - 0.02f, 0.02f, MarketData.RoomTiles - 0.02f),
                    materials[key]);
            }
        }

        /// <summary>
        /// Builds low seam stubs outside the doorway band. MarketGrid.IsDoorwayStep at
        /// Scripts/Sim/MarketGrid.cs line 135 admits a crossing when the room local tile centre sits between
        /// DoorwayBandMin and DoorwayBandMax, so the same band is left empty here. The perimeter ends use the
        /// same 0.75 inset as MarketGrid.IsInsideBuilding at line 117.
        /// </summary>
        private static void BuildDividers(Transform content, Dictionary<string, Material> materials)
        {
            var dividers = NewGroup(content, RoomDividersGroup);
            var dark = materials["black"];
            var low = (float)MarketData.DoorwayBandMin;
            var high = (float)MarketData.DoorwayBandMax;

            for (var column = 1; column < MarketData.Columns; column++)
            {
                var seamX = column * MarketData.RoomTiles;
                for (var row = 0; row < MarketData.Rows; row++)
                {
                    var origin = row * MarketData.RoomTiles;
                    var from = row == 0 ? OuterInset : origin;
                    var to = row == MarketData.Rows - 1 ? MarketData.HeightTiles - OuterInset : origin + MarketData.RoomTiles;
                    AddStub(dividers, "Divider V c" + column + " r" + row + " low", seamX, from, origin + low, origin, true, dark);
                    AddStub(dividers, "Divider V c" + column + " r" + row + " high", seamX, origin + high, to, origin, true, dark);
                }
            }

            for (var row = 1; row < MarketData.Rows; row++)
            {
                var seamZ = row * MarketData.RoomTiles;
                for (var column = 0; column < MarketData.Columns; column++)
                {
                    var origin = column * MarketData.RoomTiles;
                    var from = column == 0 ? OuterInset : origin;
                    var to = column == MarketData.Columns - 1 ? MarketData.WidthTiles - OuterInset : origin + MarketData.RoomTiles;
                    AddStub(dividers, "Divider H r" + row + " c" + column + " low", seamZ, from, origin + low, origin, false, dark);
                    AddStub(dividers, "Divider H r" + row + " c" + column + " high", seamZ, origin + high, to, origin, false, dark);
                }
            }
        }

        /// <summary>Adds one seam segment when its midpoint lies outside the grid doorway band.</summary>
        private static void AddStub(
            Transform parent,
            string name,
            float seam,
            float from,
            float to,
            float roomOrigin,
            bool vertical,
            Material material)
        {
            if (to - from <= 0.01f || IsDoorway((from + to) * 0.5f - roomOrigin))
            {
                return;
            }

            var centre = vertical
                ? new Vector3(seam, 0.3f, (from + to) * 0.5f)
                : new Vector3((from + to) * 0.5f, 0.3f, seam);
            var size = vertical
                ? new Vector3(0.12f, 0.6f, to - from)
                : new Vector3(to - from, 0.6f, 0.12f);
            Box(parent, name, centre, size, material);
        }

        /// <summary>Mirrors the room local doorway check in MarketGrid.IsDoorwayStep.</summary>
        private static bool IsDoorway(float localAlong)
            => localAlong > MarketData.DoorwayBandMin && localAlong < MarketData.DoorwayBandMax;

        /// <summary>
        /// Adds a sun chevron above the player. It is parented to the player root, so the existing
        /// MarketRunner position sync carries it with no runtime or HUD change.
        /// </summary>
        private static void AttachYouMarker(Transform player, Material sun)
        {
            var marker = new GameObject("YOU Marker");
            marker.transform.SetParent(player, false);
            marker.transform.localPosition = new Vector3(0f, 1.56f, 0f);

            var left = Box(marker.transform, "Chevron Left", new Vector3(-0.13f, 0.13f, 0f), new Vector3(0.38f, 0.08f, 0.06f), sun);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, 135f);
            var right = Box(marker.transform, "Chevron Right", new Vector3(0.13f, 0.13f, 0f), new Vector3(0.38f, 0.08f, 0.06f), sun);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void BuildFixtures(Transform content)
        {
            var fixtures = NewGroup(content, MarketSceneSpec.FixturesGroup);
            foreach (var fixture in MarketData.Fixtures)
            {
                var detail = DetailFactory.BuildFixture(fixture.Kind, fixture.Face);
                detail.name = fixture.Id;
                detail.transform.SetParent(fixtures, false);
                var position = MarketLayout.ItemPosition(fixture);
                detail.transform.position = new Vector3(position.x, 0f, position.y);
            }
        }

        private static void BuildCameras(Transform content, Dictionary<string, Material> materials, Mesh coneMesh, out Transform[] cones)
        {
            var cameras = NewGroup(content, MarketSceneSpec.CamerasGroup);
            cones = new Transform[MarketData.Cameras.Length];
            for (var i = 0; i < MarketData.Cameras.Length; i++)
            {
                var def = MarketData.Cameras[i];
                var rig = DetailFactory.BuildCamera();
                rig.name = def.Id;
                rig.transform.SetParent(cameras, false);
                var tileX = Mathf.Clamp((float)def.I, CameraEdgeInset, MarketData.WidthTiles - CameraEdgeInset);
                var tileZ = Mathf.Clamp((float)def.J, CameraEdgeInset, MarketData.HeightTiles - CameraEdgeInset);
                rig.transform.position = new Vector3(tileX, 0f, tileZ);

                var coneObject = new GameObject(MarketSceneSpec.ConeChild);
                coneObject.transform.SetParent(rig.transform, false);
                coneObject.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                coneObject.transform.localRotation = Quaternion.Euler(0f, -(float)def.BaseAngle * Mathf.Rad2Deg, 0f);
                coneObject.transform.localScale = new Vector3((float)def.Range, 1f, (float)def.Range);
                var filter = coneObject.AddComponent<MeshFilter>();
                filter.sharedMesh = coneMesh;
                var renderer = coneObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials["cone"];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                cones[i] = coneObject.transform;
            }
        }

        private static void BuildLoot(Transform content, Dictionary<string, Material> materials)
        {
            var lootRoot = NewGroup(content, MarketSceneSpec.LootGroup);
            foreach (var loot in MarketData.Loot)
            {
                var fixture = MarketData.Fixture(loot.Id);
                var position = MarketLayout.ItemPosition(fixture);
                var height = MarketLayout.ItemHeight(fixture);
                var marker = DetailFactory.BuildLoot(loot.Kind);
                marker.name = loot.Id;
                marker.transform.SetParent(lootRoot, false);
                marker.transform.position = new Vector3(position.x, height + 0.04f, position.y);
                marker.AddComponent<MarketLootMarker>().lootId = loot.Id;
            }
        }

        private static void BuildDevices(Transform content, Dictionary<string, Material> materials)
        {
            var devices = NewGroup(content, MarketSceneSpec.DevicesGroup);
            Box(
                devices,
                "CCTV Panel",
                new Vector3((float)MarketData.Panel.I, 1.25f, (float)MarketData.Panel.J),
                new Vector3(0.5f, 0.7f, 0.18f),
                materials["inkBlue"]);
            Box(
                devices,
                "De-tagger",
                new Vector3((float)MarketData.Detagger.I, 0.55f, (float)MarketData.Detagger.J),
                new Vector3(0.42f, 1.1f, 0.32f),
                materials["black"]);
        }

        private static void BuildActors(
            Transform actors,
            Dictionary<string, Material> materials,
            out Transform player,
            out Transform guard,
            out Transform staff,
            out GuardVisionFan fan)
        {
            player = AttachActor(actors, "Player", DetailFactory.BuildActor("player"), HeistGame.PlayerStart, 0f);
            AttachYouMarker(player, materials["sun"]);
            guard = AttachActor(actors, "Guard", DetailFactory.BuildActor("guard"), HeistGame.GuardStart, -1.5f * Mathf.Rad2Deg);
            staff = AttachActor(actors, "Staff", DetailFactory.BuildActor("staff"), HeistGame.StaffStart, 0f);

            var fanObject = new GameObject("Guard Vision Fan");
            fanObject.transform.SetParent(actors, false);
            fanObject.transform.position = new Vector3(HeistGame.GuardStart.x, 0.06f, HeistGame.GuardStart.y);
            fan = fanObject.AddComponent<GuardVisionFan>();
            fan.fanMaterial = materials["cone"];
            fan.Rebuild(0.6f);
        }

        private static MarketHUD BuildHud(Camera camera, TMP_FontAsset font)
        {
            var hudObject = new GameObject(MarketSceneSpec.RootHud);
            var canvas = hudObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;

            var scaler = hudObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            hudObject.AddComponent<GraphicRaycaster>();
            var hud = hudObject.AddComponent<MarketHUD>();
            hud.CreateWidgets(font);
            return hud;
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = EnsureTmpSettings();
            var sdfShader = RequireShader("TextMeshPro/Mobile/Distance Field");
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException("Missing font source at " + FontSourcePath);
            }

            if (sdfShader == null)
            {
                throw new InvalidOperationException("TextMeshPro mobile SDF shader missing");
            }

            // Forces the settings singleton so TMP reads the shipped settings asset.
            if (TMP_Settings.instance == null)
            {
                throw new InvalidOperationException("TMP Settings asset did not load from " + TmpSettingsPath);
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                throw new InvalidOperationException("TMP could not build a font asset from " + FontSourcePath);
            }

            fontAsset.name = "MarketSans";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            var atlas = fontAsset.atlasTextures[0];
            atlas.name = "MarketSans Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);

            var material = fontAsset.material;
            material.name = "MarketSans Material";
            AssetDatabase.AddObjectToAsset(material, fontAsset);

            fontAsset.atlasTextures[0] = atlas;
            fontAsset.material = material;
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            TMP_Settings.defaultFontAsset = fontAsset;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Market font asset written to " + FontAssetPath);
            return fontAsset;
        }

        private static TMP_Settings EnsureTmpSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            var leading = AssetDatabase.LoadAssetAtPath<TextAsset>(LeadingCharactersPath);
            var following = AssetDatabase.LoadAssetAtPath<TextAsset>(FollowingCharactersPath);

            var settings = ScriptableObject.CreateInstance<TMP_Settings>();
            settings.name = "TMP Settings";
            var serialized = new SerializedObject(settings);
            SetString(serialized, "assetVersion", "2");
            SetString(serialized, "m_defaultFontAssetPath", MarketSceneSpec.ResourcesDir + "/");
            SetFloat(serialized, "m_defaultFontSize", 20f);
            SetInt(serialized, "m_TextWrappingMode", 0);
            SetBool(serialized, "m_EnableRaycastTarget", false);
            SetObject(serialized, "m_leadingCharacters", leading);
            SetObject(serialized, "m_followingCharacters", following);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(settings, TmpSettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Market TMP Settings written to " + TmpSettingsPath);
            return settings;
        }

        private static void SetString(SerializedObject serialized, string property, string value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.stringValue = value;
            }
        }

        private static void SetFloat(SerializedObject serialized, string property, float value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.floatValue = value;
            }
        }

        private static void SetInt(SerializedObject serialized, string property, int value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.intValue = value;
            }
        }

        private static void SetBool(SerializedObject serialized, string property, bool value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.boolValue = value;
            }
        }

        private static void SetObject(SerializedObject serialized, string property, UnityEngine.Object value)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                found.objectReferenceValue = value;
            }
        }

        private static Mesh EnsureConeMesh()
        {
            var path = MarketSceneSpec.MeshDir + "/ConeFan.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            var halfAngle = 0f;
            foreach (var camera in MarketData.Cameras)
            {
                halfAngle = Mathf.Max(halfAngle, (float)camera.HalfAngle);
            }

            var mesh = BuildFanMesh(Mathf.Max(0.1f, halfAngle), "ConeFan");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh BuildFanMesh(float half, string name)
        {
            const int segments = 24;
            var vertices = new List<Vector3>(segments + 2) { Vector3.zero };
            var triangles = new List<int>(segments * 3);
            for (var i = 0; i <= segments; i++)
            {
                var angle = -half + 2f * half * (i / (float)segments);
                vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            for (var i = 1; i <= segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform AttachActor(Transform parent, string name, GameObject detail, Vector2 start, float yaw)
        {
            detail.name = name;
            detail.transform.SetParent(parent, false);
            detail.transform.position = new Vector3(start.x, 0f, start.y);
            detail.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return detail.transform;
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Primitive(parent, name, PrimitiveType.Cube, position, scale, material);
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Primitive(parent, name, PrimitiveType.Cylinder, position, scale, material);
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
            return primitive;
        }
    }
}
