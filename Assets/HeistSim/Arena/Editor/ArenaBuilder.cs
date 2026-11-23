using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace HeistSim.Arena.Editor
{
    /// <summary>Builds the four arena look scenes from one shared content spec.</summary>
    public static class ArenaBuilder
    {
        private const string ArenaDir = "Assets/HeistSim/Arena";
        private const string GeneratedDir = ArenaDir + "/Generated";
        private const string MaterialsDir = GeneratedDir + "/Materials";
        private const string SpritesDir = GeneratedDir + "/Sprites";
        private const string MeshesDir = GeneratedDir + "/Meshes";
        private const string TilesDir = GeneratedDir + "/Tiles";

        private static readonly Color FlatLight = ArenaSpec.Card;
        private static readonly Color ShadowInk = ArenaSpec.Rgb(0x1A1A20);

        private static Material s_risoMaterial;
        private static Material s_coneMaterial;
        private static Material s_coneFlatMaterial;
        private static Material s_spriteMaterial;
        private static Material s_blobMaterial;
        private static Material s_postMaterial;
        private static Material s_ditherMaterial;
        private static Sprite s_squareSprite;
        private static Sprite s_coneSprite;
        private static Mesh s_coneMesh;
        private static readonly Dictionary<int, Material> RisoByColor = new Dictionary<int, Material>();

        /// <summary>Rebuilds all four look scenes and saves them over the existing assets.</summary>
        [MenuItem("Heist/Arena/Build All Looks")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();
            RecreateGeneratedAssets();
            CreateSharedAssets();

            BuildLookA();
            BuildLookB();
            BuildLookC();
            BuildLookD();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Arena build complete: " + string.Join(", ", ArenaSpec.ScenePaths));
        }

        /// <summary>Headless entry point for batch mode, exits non zero on failure.</summary>
        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
                Debug.Log("Arena batch build finished.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Arena batch build failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void RecreateGeneratedAssets()
        {
            RisoByColor.Clear();
            if (AssetDatabase.IsValidFolder(GeneratedDir))
            {
                AssetDatabase.DeleteAsset(GeneratedDir);
            }

            EnsureFolder(ArenaDir);
            EnsureFolder(GeneratedDir);
            EnsureFolder(MaterialsDir);
            EnsureFolder(SpritesDir);
            EnsureFolder(MeshesDir);
            EnsureFolder(TilesDir);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                var name = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void CreateSharedAssets()
        {
            s_squareSprite = CreateSquareSprite();
            s_coneSprite = CreateConeSprite(out var coneTexture);

            var risoShader = RequireShader("HeistSim/RisoDither");
            s_risoMaterial = NewMaterial("RisoDither", risoShader);
            ApplyRisoInks(s_risoMaterial, Color.white);
            s_risoMaterial.SetTexture("_MainTex", s_squareSprite.texture);
            EditorUtility.SetDirty(s_risoMaterial);

            s_spriteMaterial = NewMaterial("SpritePaper", RequireShader("Sprites/Default"));
            s_blobMaterial = NewMaterial("BlobShadow", RequireShader("Unlit/Color"));
            s_blobMaterial.SetColor("_Color", ShadowInk);
            EditorUtility.SetDirty(s_blobMaterial);

            s_postMaterial = NewMaterial("PaperGrainPost", RequireShader("HeistSim/PaperGrainPost"));
            s_postMaterial.SetColor("_PaperColor", ArenaSpec.Paper);
            s_postMaterial.SetColor("_InkColor", ArenaSpec.InkBlue);
            EditorUtility.SetDirty(s_postMaterial);

            s_ditherMaterial = NewMaterial("IsoDitherPost", RequireShader("HeistSim/IsoDitherPost"));
            s_ditherMaterial.SetColor("_PaperColor", ArenaSpec.Paper);
            s_ditherMaterial.SetColor("_KeyInkColor", ArenaSpec.InkBlue);
            s_ditherMaterial.SetColor("_TealColor", ArenaSpec.Teal);
            s_ditherMaterial.SetColor("_SunColor", ArenaSpec.Sun);
            s_ditherMaterial.SetColor("_CoralColor", ArenaSpec.Coral);
            s_ditherMaterial.SetColor("_BotBlackColor", ArenaSpec.Black);
            EditorUtility.SetDirty(s_ditherMaterial);

            s_coneMaterial = NewMaterial("ConeInk", risoShader);
            s_coneMaterial.SetTexture("_MainTex", coneTexture);
            ApplyRisoInks(s_coneMaterial, ArenaSpec.Teal);
            EditorUtility.SetDirty(s_coneMaterial);

            s_coneFlatMaterial = NewMaterial("ConeFlat", s_spriteMaterial.shader);
            s_coneFlatMaterial.SetColor("_Color", new Color(ArenaSpec.Teal.r, ArenaSpec.Teal.g, ArenaSpec.Teal.b, 0.4f));
            EditorUtility.SetDirty(s_coneFlatMaterial);

            s_coneMesh = CreateConeMesh();
        }

        private static Material NewMaterial(string name, Shader shader)
        {
            var material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, MaterialsDir + "/" + name + ".mat");
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

        private static Scene BeginScene(out GameObject content, out Camera camera)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ArenaSpec.OrthographicSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ArenaSpec.Paper;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            content = new GameObject("Content");
            new GameObject("Arena Settings");
            return scene;
        }

        private static void SaveScene(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException("Failed to save scene " + path);
            }
        }

        private static void BuildLookA()
        {
            var scene = BeginScene(out var contentRoot, out var camera);
            var content = contentRoot.transform;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            var background = NewGroup(content, "Background");
            var shadows = NewGroup(content, "Hard Shadows");
            var fixtures = NewGroup(content, "Fixtures");
            var actors = NewGroup(content, "Actors");
            var depth = new int[4];

            foreach (var part in FlatParts())
            {
                var parent = part.Layer switch
                {
                    0 => background,
                    1 => shadows,
                    2 => fixtures,
                    _ => actors,
                };
                var z = 1.9f - part.Layer * 0.4f - depth[part.Layer]++ * 0.01f;
                FlatQuad(parent, part, RisoMaterial(part.Color), z);
            }

            FlatQuad(content, ConePart, s_coneMaterial, 1.25f);

            SaveScene(scene, ArenaSpec.ScenePaths[0]);
        }

        private static void BuildLookB()
        {
            var scene = BeginScene(out var contentRoot, out var camera);
            BuildFlatIsoContent(contentRoot.transform, camera);
            SaveScene(scene, ArenaSpec.ScenePaths[1]);
        }

        private static void BuildLookD()
        {
            var scene = BeginScene(out var contentRoot, out var camera);
            BuildFlatIsoContent(contentRoot.transform, camera, ArenaSpec.Card, ArenaSpec.Paper);
            var dither = camera.gameObject.AddComponent<IsoDitherPost>();
            dither.postMaterial = s_ditherMaterial;
            dither.bayerScale = 2f;
            SaveScene(scene, ArenaSpec.ScenePaths[3]);
        }

        /// <summary>Builds the shared isometric content used by LookB and LookD, with optional ground and wall tones.</summary>
        private static void BuildFlatIsoContent(Transform content, Camera camera, Color? groundTone = null, Color? wallTone = null)
        {
            camera.orthographicSize = ArenaSpec.IsoOrthographicSize;
            camera.transform.position = new Vector3(10f, 10f, -10f);
            camera.transform.LookAt(new Vector3(0f, 1f, 1f));
            camera.backgroundColor = ArenaSpec.Rgb(0xE9E1D2);

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(content, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 0.95f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.40f, 0.37f);

            var floorMaterial = FlatMaterial("FloorInk", ArenaSpec.InkBlue);
            var groundMaterial = groundTone.HasValue ? FlatMaterial("GroundCard", groundTone.Value) : floorMaterial;
            var cardMaterial = wallTone.HasValue
                ? FlatMaterial("WallPaper", wallTone.Value)
                : FlatMaterial("WallCard", FlatLight);
            var coralMaterial = FlatMaterial("ShelfCoral", ArenaSpec.Coral);
            var coralInkMaterial = FlatMaterial("CoralRule", ArenaSpec.CoralInk);
            var tealMaterial = FlatMaterial("FreezerTeal", ArenaSpec.Teal);
            var sunMaterial = FlatMaterial("TableSun", ArenaSpec.Sun);
            var halfInkMaterial = FlatMaterial("TrolleyInk", ArenaSpec.InkBlue2);
            var blackMaterial = FlatMaterial("BotBlack", ArenaSpec.Black);
            var playerMaterial = FlatMaterial("PlayerTeal", ArenaSpec.Teal);

            Box(content, "Floor Slab", new Vector3(0f, -0.2f, 0.8f), new Vector3(15f, 0.4f, 6f), groundMaterial);
            Box(content, "Wall Slab", new Vector3(0f, 1.6f, 4.2f), new Vector3(15f, 3.2f, 0.4f), cardMaterial);
            Box(content, "Wall Rule", new Vector3(0f, 0.35f, 3.98f), new Vector3(15f, 0.3f, 0.05f), coralMaterial);

            var shelf = NewGroup(content, "Shelf Row");
            Box(shelf, "Back", new Vector3(-5f, 0.9f, 2.6f), new Vector3(2.6f, 1.8f, 0.7f), coralMaterial);
            Box(shelf, "Bar Low", new Vector3(-5f, 0.45f, 2.24f), new Vector3(2.5f, 0.12f, 0.06f), coralInkMaterial);
            Box(shelf, "Bar High", new Vector3(-5f, 1.25f, 2.24f), new Vector3(2.5f, 0.12f, 0.06f), coralInkMaterial);

            var freezer = NewGroup(content, "Chest Freezer");
            Box(freezer, "Body", new Vector3(-1.8f, 0.5f, 2.4f), new Vector3(2.2f, 1f, 1.4f), tealMaterial);
            Box(freezer, "Lid", new Vector3(-1.8f, 1.03f, 2.4f), new Vector3(2.3f, 0.08f, 1.5f), floorMaterial);
            Box(freezer, "Handle", new Vector3(-1.8f, 0.92f, 1.68f), new Vector3(0.8f, 0.1f, 0.06f), sunMaterial);

            var table = NewGroup(content, "Action Table");
            Box(table, "Top", new Vector3(1.4f, 0.85f, 2.4f), new Vector3(2.2f, 0.12f, 1.2f), sunMaterial);
            Box(table, "Leg A", new Vector3(0.45f, 0.4f, 1.9f), new Vector3(0.12f, 0.8f, 0.12f), halfInkMaterial);
            Box(table, "Leg B", new Vector3(2.35f, 0.4f, 1.9f), new Vector3(0.12f, 0.8f, 0.12f), halfInkMaterial);
            Box(table, "Leg C", new Vector3(0.45f, 0.4f, 2.9f), new Vector3(0.12f, 0.8f, 0.12f), halfInkMaterial);
            Box(table, "Leg D", new Vector3(2.35f, 0.4f, 2.9f), new Vector3(0.12f, 0.8f, 0.12f), halfInkMaterial);
            Box(table, "Crate", new Vector3(1f, 1.16f, 2.3f), new Vector3(0.6f, 0.5f, 0.6f), coralMaterial);

            var trolley = NewGroup(content, "Shopping Trolley");
            Box(trolley, "Basket", new Vector3(4.6f, 0.75f, 2.4f), new Vector3(1.3f, 0.9f, 0.9f), halfInkMaterial);
            Box(trolley, "Handle", new Vector3(3.85f, 1.05f, 2.4f), new Vector3(0.1f, 0.9f, 0.8f), floorMaterial);
            Disc(trolley, "Wheel A", new Vector3(4.15f, 0.16f, 2.4f), 0.3f, blackMaterial);
            Disc(trolley, "Wheel B", new Vector3(5.05f, 0.16f, 2.4f), 0.3f, blackMaterial);

            var guard = NewGroup(content, "Guard Robot");
            Capsule(guard, "Body", new Vector3(-3.6f, 0.66f, 0.4f), new Vector3(0.6f, 0.65f, 0.6f), floorMaterial);
            Disc(guard, "Cap", new Vector3(-3.6f, 1.34f, 0.4f), 0.34f, coralMaterial);

            var player = NewGroup(content, "Player");
            Capsule(player, "Body", new Vector3(2.8f, 0.66f, 0.2f), new Vector3(0.55f, 0.65f, 0.55f), playerMaterial);
            Disc(player, "Cap", new Vector3(2.8f, 1.32f, 0.2f), 0.31f, sunMaterial);

            Disc(content, "Guard Shadow", new Vector3(-3.6f, 0.008f, 0.4f), 0.75f, s_blobMaterial);
            Disc(content, "Player Shadow", new Vector3(2.8f, 0.008f, 0.2f), 0.7f, s_blobMaterial);

            var cctv = NewGroup(content, "CCTV");
            Box(cctv, "Mount", new Vector3(-6.8f, 2.6f, 3.9f), new Vector3(0.4f, 0.5f, 0.3f), floorMaterial);
            Box(cctv, "Arm", new Vector3(-6.45f, 2.55f, 3.8f), new Vector3(0.45f, 0.14f, 0.14f), halfInkMaterial);
            var cone = new GameObject("Cone");
            cone.transform.SetParent(cctv, false);
            cone.transform.position = new Vector3(-6.2f, 2.5f, 3.7f);
            cone.transform.rotation = Quaternion.Euler(-18f, 18f, 0f);
            cone.transform.localScale = new Vector3(1.5f, 1.1f, 1.5f);
            var coneFilter = cone.AddComponent<MeshFilter>();
            coneFilter.sharedMesh = s_coneMesh;
            var coneRenderer = cone.AddComponent<MeshRenderer>();
            coneRenderer.sharedMaterial = s_coneFlatMaterial;
        }

        private static void BuildLookC()
        {
            var scene = BeginScene(out var contentRoot, out var camera);
            var content = contentRoot.transform;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.gameObject.AddComponent<ArenaPaperPost>().postMaterial = s_postMaterial;

            var gridObject = new GameObject("Floor Grid");
            gridObject.transform.SetParent(content, false);
            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            var tileObject = new GameObject("Floor Tilemap");
            tileObject.transform.SetParent(gridObject.transform, false);
            var tilemap = tileObject.AddComponent<Tilemap>();
            var tilemapRenderer = tileObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.sharedMaterial = s_spriteMaterial;
            tilemapRenderer.sortingOrder = -10;

            var tileCard = CreateTile("TileCard", ArenaSpec.Card);
            var tileHalf = CreateTile("TileHalf", ArenaSpec.InkBlue2);
            var tileRule = CreateTile("TileRule", ArenaSpec.Coral);

            for (var x = -9; x <= 9; x++)
            {
                for (var y = -5; y <= -1; y++)
                {
                    var tile = y == -1 ? tileRule : ((x + y) & 1) == 0 ? tileCard : tileHalf;
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }

            var background = NewGroup(content, "Background");
            var shadows = NewGroup(content, "Hard Shadows");
            var fixtures = NewGroup(content, "Fixtures");
            var actors = NewGroup(content, "Actors");
            var depth = new int[4];

            foreach (var part in FlatParts())
            {
                if (part.Name.StartsWith("Floor"))
                {
                    continue;
                }

                var parent = part.Layer switch
                {
                    0 => background,
                    1 => shadows,
                    2 => fixtures,
                    _ => actors,
                };
                var sprite = SpritePart(parent, part, s_spriteMaterial);
                sprite.sortingOrder = 10 + part.Layer * 10 + depth[part.Layer]++;
            }

            var cone = new GameObject("Cone");
            cone.transform.SetParent(content, false);
            cone.transform.localPosition = new Vector3(-8.15f, -2.5f, 0f);
            cone.transform.localScale = new Vector3(4.75f, 5.05f, 1f);
            var coneRenderer = cone.AddComponent<SpriteRenderer>();
            coneRenderer.sprite = s_coneSprite;
            coneRenderer.sharedMaterial = s_spriteMaterial;
            coneRenderer.color = new Color(ArenaSpec.Teal.r, ArenaSpec.Teal.g, ArenaSpec.Teal.b, 0.45f);
            coneRenderer.sortingOrder = 18;

            SaveScene(scene, ArenaSpec.ScenePaths[2]);
        }

        private static readonly FlatPart ConePart = new FlatPart("Cone", -5.775f, 0.025f, 4.75f, 5.05f, ArenaSpec.Teal, 1);

        private static List<FlatPart> FlatParts()
        {
            return new List<FlatPart>
            {
                new FlatPart("Floor", 0f, -2.55f, 19f, 5.1f, ArenaSpec.Card, 0),
                new FlatPart("Wall", 0f, 2f, 19f, 4f, ArenaSpec.Card, 0),
                new FlatPart("Wall Rule", 0f, 1.3f, 19f, 0.25f, ArenaSpec.CoralInk, 0),
                new FlatPart("Floor Edge", 0f, -0.05f, 19f, 0.3f, ArenaSpec.CoralInk, 0),
                new FlatPart("Floor Rule A", 0f, -3.1f, 19f, 0.08f, ArenaSpec.InkBlue, 0),
                new FlatPart("Floor Rule B", 0f, -4.4f, 19f, 0.08f, ArenaSpec.InkBlue, 0),

                new FlatPart("Shadow Shelf", -6f, -2.62f, 3.2f, 0.45f, ShadowInk, 1),
                new FlatPart("Shadow Freezer", -2.4f, -2.5f, 2.6f, 0.4f, ShadowInk, 1),
                new FlatPart("Shadow Table", 1.6f, -2.5f, 2.5f, 0.4f, ShadowInk, 1),
                new FlatPart("Shadow Trolley", 5.3f, -2.6f, 1.9f, 0.35f, ShadowInk, 1),
                new FlatPart("Shadow Guard", -4f, -2.78f, 1.1f, 0.3f, ShadowInk, 1),
                new FlatPart("Shadow Player", 3.3f, -2.74f, 1f, 0.3f, ShadowInk, 1),

                new FlatPart("Shelf Back", -6.2f, -1.3f, 3f, 2.1f, ArenaSpec.Coral, 2),
                new FlatPart("Shelf Bar Low", -6.2f, -2.1f, 3.1f, 0.16f, ArenaSpec.CoralInk, 2),
                new FlatPart("Shelf Bar Mid", -6.2f, -1.25f, 3.1f, 0.16f, ArenaSpec.CoralInk, 2),
                new FlatPart("Shelf Bar High", -6.2f, -0.4f, 3.1f, 0.16f, ArenaSpec.CoralInk, 2),
                new FlatPart("Shelf Upright L", -7.65f, -1.3f, 0.16f, 2.1f, ArenaSpec.CoralInk, 2),
                new FlatPart("Shelf Upright R", -4.75f, -1.3f, 0.16f, 2.1f, ArenaSpec.CoralInk, 2),

                new FlatPart("Freezer Body", -2.6f, -1.55f, 2.4f, 1.5f, ArenaSpec.Teal, 2),
                new FlatPart("Freezer Lid", -2.6f, -0.95f, 2.45f, 0.3f, ArenaSpec.InkBlue, 2),
                new FlatPart("Freezer Handle", -2.6f, -1.15f, 0.8f, 0.1f, ArenaSpec.Sun, 2),
                new FlatPart("Freezer Label", -2.6f, -1.95f, 0.7f, 0.4f, ArenaSpec.Coral, 2),

                new FlatPart("Table Top", 1.4f, -1.15f, 2.6f, 0.25f, ArenaSpec.Sun, 2),
                new FlatPart("Table Leg L", 0.35f, -2.15f, 0.18f, 1.8f, ArenaSpec.InkBlue2, 2),
                new FlatPart("Table Leg R", 2.45f, -2.15f, 0.18f, 1.8f, ArenaSpec.InkBlue2, 2),
                new FlatPart("Table Crate", 0.9f, -1.62f, 0.75f, 0.7f, ArenaSpec.Coral, 2),
                new FlatPart("Table Crate Lid", 0.9f, -1.3f, 0.8f, 0.12f, ArenaSpec.CoralInk, 2),

                new FlatPart("Trolley Basket", 5.2f, -1.75f, 1.8f, 1.15f, ArenaSpec.InkBlue2, 2),
                new FlatPart("Trolley Bar Low", 5.2f, -2f, 1.65f, 0.1f, ArenaSpec.InkBlue, 2),
                new FlatPart("Trolley Bar High", 5.2f, -1.5f, 1.65f, 0.1f, ArenaSpec.InkBlue, 2),
                new FlatPart("Trolley Handle", 4.35f, -1.15f, 0.14f, 1.1f, ArenaSpec.InkBlue, 2),
                new FlatPart("Trolley Grip", 4.35f, -0.62f, 0.5f, 0.12f, ArenaSpec.Sun, 2),
                new FlatPart("Trolley Wheel L", 4.6f, -2.5f, 0.35f, 0.35f, ArenaSpec.Black, 2),
                new FlatPart("Trolley Wheel R", 5.8f, -2.5f, 0.35f, 0.35f, ArenaSpec.Black, 2),

                new FlatPart("Guard Body", -4f, -1.85f, 0.85f, 1.5f, ArenaSpec.InkBlue, 3),
                new FlatPart("Guard Cap", -4f, -0.98f, 0.95f, 0.34f, ArenaSpec.Coral, 3),
                new FlatPart("Guard Visor", -4f, -1.18f, 0.55f, 0.12f, ArenaSpec.Sun, 3),

                new FlatPart("Player Body", 3.3f, -1.9f, 0.8f, 1.4f, ArenaSpec.Teal, 3),
                new FlatPart("Player Cap", 3.3f, -1.08f, 0.9f, 0.3f, ArenaSpec.Sun, 3),
                new FlatPart("Player Pack", 3.3f, -1.55f, 0.5f, 0.5f, ArenaSpec.Coral, 3),

                new FlatPart("Cctv Mount", -8.3f, 2.9f, 0.5f, 0.4f, ArenaSpec.InkBlue, 2),
                new FlatPart("Cctv Arm", -8.05f, 2.9f, 0.4f, 0.14f, ArenaSpec.InkBlue2, 2),
                new FlatPart("Cctv Lens", -8.25f, 2.65f, 0.26f, 0.26f, ArenaSpec.Sun, 2),
            };
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject FlatQuad(Transform parent, FlatPart part, Material material, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = part.Name;
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(part.X, part.Y, z);
            quad.transform.localScale = new Vector3(part.W, part.H, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return quad;
        }

        private static SpriteRenderer SpritePart(Transform parent, FlatPart part, Material material)
        {
            var spriteObject = new GameObject(part.Name);
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.localPosition = new Vector3(part.X, part.Y, 0f);
            spriteObject.transform.localScale = new Vector3(part.W, part.H, 1f);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = s_squareSprite;
            renderer.sharedMaterial = material;
            renderer.color = part.Color;
            return renderer;
        }

        private static Material RisoMaterial(Color color)
        {
            var key = Mathf.RoundToInt(color.r * 255f) << 16 | Mathf.RoundToInt(color.g * 255f) << 8 | Mathf.RoundToInt(color.b * 255f);
            if (RisoByColor.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var material = NewMaterial("Riso_" + key.ToString("X6"), s_risoMaterial.shader);
            material.SetTexture("_MainTex", s_squareSprite.texture);
            ApplyRisoInks(material, color);
            EditorUtility.SetDirty(material);
            RisoByColor[key] = material;
            return material;
        }

        private static void ApplyRisoInks(Material material, Color color)
        {
            material.SetColor("_Color", color);
            material.SetColor("_PaperColor", ArenaSpec.Paper);
            material.SetColor("_InkTeal", ArenaSpec.Teal);
            material.SetColor("_InkSun", ArenaSpec.Sun);
            material.SetColor("_InkCoral", ArenaSpec.Coral);
            material.SetFloat("_DitherPixel", 3f);
            material.SetFloat("_DitherStrength", 0.14f);
        }

        private static Material FlatMaterial(string name, Color color)
        {
            var path = MaterialsDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = NewMaterial(name, RequireShader("Standard"));
            }

            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", 0f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Primitive(parent, name, PrimitiveType.Cube, position, scale, material);
        }

        private static GameObject Capsule(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Primitive(parent, name, PrimitiveType.Capsule, position, scale, material);
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

        private static GameObject Disc(Transform parent, string name, Vector3 position, float radius, Material material)
        {
            var disc = Primitive(parent, name, PrimitiveType.Cylinder, position, new Vector3(radius * 2f, 0.006f, radius * 2f), material);
            return disc;
        }

        private static Sprite CreateSquareSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, SpritesDir + "/SquareTexture.asset");

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            AssetDatabase.CreateAsset(sprite, SpritesDir + "/Square.asset");
            return sprite;
        }

        private static Sprite CreateConeSprite(out Texture2D texture)
        {
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var apex = new Vector2(0f, 1f);
            var right = new Vector2(1f, 0.515f);
            var left = new Vector2(0.242f, 0f);
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var point = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    var inside = InsideTriangle(point, apex, right, left);
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, SpritesDir + "/ConeTexture.asset");

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.zero, size);
            AssetDatabase.CreateAsset(sprite, SpritesDir + "/Cone.asset");
            return sprite;
        }

        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Sign(point, a, b);
            var d2 = Sign(point, b, c);
            var d3 = Sign(point, c, a);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static Mesh CreateConeMesh()
        {
            const int segments = 16;
            const float radius = 1f;
            const float height = 2.2f;
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -height, Mathf.Sin(angle) * radius));
            }

            for (var i = 1; i <= segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            var baseCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -height, 0f));
            for (var i = 1; i <= segments; i++)
            {
                triangles.Add(baseCenter);
                triangles.Add(i + 1);
                triangles.Add(i);
            }

            var mesh = new Mesh { name = "CctvCone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshesDir + "/CctvCone.asset");
            return mesh;
        }

        private static Tile CreateTile(string name, Color color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = s_squareSprite;
            tile.color = color;
            AssetDatabase.CreateAsset(tile, TilesDir + "/" + name + ".asset");
            return tile;
        }

        private readonly struct FlatPart
        {
            public FlatPart(string name, float x, float y, float w, float h, Color color, int layer)
            {
                Name = name;
                X = x;
                Y = y;
                W = w;
                H = h;
                Color = color;
                Layer = layer;
            }

            public string Name { get; }

            public float X { get; }

            public float Y { get; }

            public float W { get; }

            public float H { get; }

            public Color Color { get; }

            public int Layer { get; }
        }
    }
}
