// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD]
// Prebuild SpriteAtlas V2 generator for Frischemarkt Market2D sprites.

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace HeistSim.Market2D.Editor
{
    /// <summary>
    /// Automatically builds and updates SpriteAtlas V2 assets for Market2D Fixtures, Actors,
    /// and Rooms before player builds and during scene rebuilds. Configured with Trilinear
    /// mipmaps and 8px padding so smooth zoom stays shimmer-free and silhouette highlight
    /// outlines never bleed across atlas neighbours.
    /// </summary>
    public sealed class Market2DSpriteAtlasPrebuild : IPreprocessBuildWithReport
    {
        /// <summary>Folder where generated SpriteAtlas V2 assets live.</summary>
        public const string AtlasFolder = "Assets/HeistSim/Market2D/Atlases";

        /// <summary>Path to the packed fixtures atlas.</summary>
        public const string FixturesAtlasPath = AtlasFolder + "/Market2D_Fixtures.spriteatlasv2";

        /// <summary>Path to the packed actors atlas.</summary>
        public const string ActorsAtlasPath = AtlasFolder + "/Market2D_Actors.spriteatlasv2";

        /// <summary>Execution priority for prebuild atlas generation.</summary>
        public int callbackOrder => 0;

        /// <summary>Regenerates Market2D SpriteAtlas V2 assets before any player build.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureAtlases(packForEditorPreview: false);
        }

        /// <summary>
        /// Ensures SpriteAtlas V2 packing is active and builds/updates the Market2D atlases.
        /// Uses V2 editor-only APIs (SpriteAtlasAsset + SpriteAtlasImporter).
        /// </summary>
        public static void EnsureAtlases(bool packForEditorPreview = true)
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            Directory.CreateDirectory(AtlasFolder);

            BuildFolderAtlas(
                "Assets/HeistSim/Market2D/Sprites/Fixtures",
                FixturesAtlasPath,
                maxTextureSize: 4096,
                padding: 8);

            BuildFolderAtlas(
                "Assets/HeistSim/Market2D/Sprites/Actors",
                ActorsAtlasPath,
                maxTextureSize: 2048,
                padding: 8);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (packForEditorPreview)
            {
                SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget, false);
            }
        }

        private static bool IsValidProjectSprite(string assetPath)
        {
            return assetPath.StartsWith("Assets/")
                && !assetPath.Contains("/Packages/");
        }

        private static void BuildFolderAtlas(string spriteFolder, string atlasPath, int maxTextureSize, int padding)
        {
            if (!Directory.Exists(spriteFolder))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolder });
            var sprites = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsValidProjectSprite)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .Cast<Object>()
                .ToArray();

            if (sprites.Length == 0)
            {
                return;
            }

            SpriteAtlasAsset atlasAsset;
            if (File.Exists(atlasPath))
            {
                atlasAsset = SpriteAtlasAsset.Load(atlasPath);
                var runtimeAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (runtimeAtlas != null)
                {
                    var existing = runtimeAtlas.GetPackables();
                    if (existing != null && existing.Length > 0)
                    {
                        atlasAsset.Remove(existing);
                    }
                }
            }
            else
            {
                atlasAsset = new SpriteAtlasAsset();
            }

            atlasAsset.Add(sprites);
            SpriteAtlasAsset.Save(atlasAsset, atlasPath);
            AssetDatabase.ImportAsset(atlasPath);

            var importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
            if (importer == null)
            {
                return;
            }

            importer.includeInBuild = true;

            var packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.enableAlphaDilation = true;
            packing.padding = padding;
            importer.packingSettings = packing;

            var tex = importer.textureSettings;
            tex.readable = false;
            tex.generateMipMaps = true;
            tex.sRGB = true;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            importer.textureSettings = tex;

            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = maxTextureSize;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformSettings(platform);

            importer.SaveAndReimport();
        }
    }
}
