using System;
using HeistSim.Market.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HeistSim.Market.Editor
{
    /// <summary>
    /// Rebuilds the shipped MarketSans font asset as a static SDF atlas. It keeps the asset
    /// GUID and its material and atlas subasset file IDs, so saved scenes stay bound.
    /// </summary>
    public static class MarketFontBaker
    {
        /// <summary>Path of the baked font asset under Resources.</summary>
        public const string AssetPath = MarketSceneSpec.ResourcesDir + "/MarketSans.asset";

        /// <summary>Path of the vendored TrueType source font.</summary>
        public const string SourcePath = MarketSceneSpec.TextMeshProDir + "/Fonts/LiberationSans.ttf";

        private const int AtlasSize = 1024;
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;

        /// <summary>Rebakes the ASCII glyph set into the existing font asset and saves it.</summary>
        [MenuItem("Heist/Market/Bake Font Atlas")]
        public static void Bake()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (font == null)
            {
                throw new InvalidOperationException("Missing font asset at " + AssetPath);
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
            {
                throw new InvalidOperationException("Missing source font at " + SourcePath);
            }

            Material material = null;
            Texture2D atlas = null;
            foreach (var subasset in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
            {
                if (subasset is Material foundMaterial)
                {
                    material = foundMaterial;
                }
                else if (subasset is Texture2D foundAtlas)
                {
                    atlas = foundAtlas;
                }
            }

            if (material == null || atlas == null)
            {
                throw new InvalidOperationException("Font asset has no material or atlas subasset at " + AssetPath);
            }

            // Clear the old glyph tables while the asset is still dynamic, because TMP refuses
            // to add characters to a static asset.
            var serialized = new SerializedObject(font);
            SetObject(serialized, "m_SourceFontFile", source);
            SetInt(serialized, "m_AtlasPopulationMode", (int)AtlasPopulationMode.Dynamic);
            SetInt(serialized, "m_AtlasWidth", AtlasSize);
            SetInt(serialized, "m_AtlasHeight", AtlasSize);
            SetInt(serialized, "m_AtlasPadding", AtlasPadding);
            SetInt(serialized, "m_AtlasRenderMode", (int)GlyphRenderMode.SDFAA);
            SetBool(serialized, "m_IsMultiAtlasTexturesEnabled", false);
            ClearArray(serialized, "m_CharacterTable");
            ClearArray(serialized, "m_GlyphTable");
            SetSingleFreeRect(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            font.ReadFontAssetDefinition();
            if (atlas.width != AtlasSize || atlas.height != AtlasSize)
            {
                atlas.Reinitialize(AtlasSize, AtlasSize);
            }

            var blank = new Color32[AtlasSize * AtlasSize];
            atlas.SetPixels32(blank);
            atlas.Apply(false, false);
            font.atlasTextures = new[] { atlas };
            font.material = material;
            material.SetTexture("_MainTex", atlas);
            material.SetFloat("_TextureWidth", AtlasSize);
            material.SetFloat("_TextureHeight", AtlasSize);
            material.SetFloat("_GradientScale", AtlasPadding + 1);

            if (!font.TryAddCharacters(HudWidgets.AsciiCharacterSet(), out var missing, false))
            {
                Debug.LogWarning("Market font baker: TMP reported missing characters: " + missing);
            }

            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.name = "MarketSans";
            material.name = "MarketSans Material";
            atlas.name = "MarketSans Atlas";
            atlas.Apply();

            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Market font baker: " + font.characterTable.Count + " characters, "
                + font.glyphTable.Count + " glyphs, static " + AtlasSize + " by " + AtlasSize + " atlas.");
        }

        private static void SetObject(SerializedObject serialized, string property, UnityEngine.Object value)
        {
            var found = serialized.FindProperty(property);
            if (found == null)
            {
                throw new InvalidOperationException("Missing serialized property " + property);
            }

            found.objectReferenceValue = value;
        }

        private static void SetInt(SerializedObject serialized, string property, int value)
        {
            var found = serialized.FindProperty(property);
            if (found == null)
            {
                throw new InvalidOperationException("Missing serialized property " + property);
            }

            found.intValue = value;
        }

        private static void SetBool(SerializedObject serialized, string property, bool value)
        {
            var found = serialized.FindProperty(property);
            if (found == null)
            {
                throw new InvalidOperationException("Missing serialized property " + property);
            }

            found.boolValue = value;
        }

        private static void ClearArray(SerializedObject serialized, string property)
        {
            var found = serialized.FindProperty(property);
            if (found == null)
            {
                throw new InvalidOperationException("Missing serialized property " + property);
            }

            found.arraySize = 0;
        }

        private static void SetSingleFreeRect(SerializedObject serialized)
        {
            var found = serialized.FindProperty("m_FreeGlyphRects");
            if (found == null)
            {
                throw new InvalidOperationException("Missing serialized property m_FreeGlyphRects");
            }

            found.arraySize = 1;
            var rect = found.GetArrayElementAtIndex(0);
            rect.FindPropertyRelative("m_X").intValue = 0;
            rect.FindPropertyRelative("m_Y").intValue = 0;
            rect.FindPropertyRelative("m_Width").intValue = AtlasSize - 1;
            rect.FindPropertyRelative("m_Height").intValue = AtlasSize - 1;
        }
    }
}
