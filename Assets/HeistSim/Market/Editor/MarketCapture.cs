using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeistSim.Market.Editor
{
    /// <summary>Renders the built market scene to one PNG for review, using the live display path of the arena capture.</summary>
    public static class MarketCapture
    {
        /// <summary>Opens the market scene and writes the review capture.</summary>
        [MenuItem("Heist/Market/Capture Market")]
        public static void CaptureMarket()
        {
            if (!File.Exists(MarketSceneSpec.ScenePath))
            {
                throw new InvalidOperationException("Market scene missing, run Heist/Market/Build Level Scene first.");
            }

            EnsureCapturesFolder();
            EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("No camera in " + MarketSceneSpec.ScenePath);
            }

            var texture = Render(camera);
            var bytes = MarketPngWriter.Encode(texture);
            var outputPath = MarketSceneSpec.CapturesDir + "/" + MarketSceneSpec.CaptureFileName;
            File.WriteAllBytes(outputPath, bytes);

            var unique = UniqueColorCount(texture);
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.Refresh();
            Debug.Log("Market capture wrote " + Path.GetFullPath(outputPath) + " (" + bytes.Length + " bytes, " + unique + " unique colors)");
        }

        /// <summary>Headless entry point that captures the built scene, exits non zero on failure.</summary>
        public static void CaptureBatch()
        {
            try
            {
                CaptureMarket();
                Debug.Log("Market capture finished.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("Market capture failed: " + error);
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

        private static Texture2D Render(Camera camera)
        {
            var width = MarketSceneSpec.CaptureWidth;
            var height = MarketSceneSpec.CaptureHeight;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            return texture;
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
    }
}
