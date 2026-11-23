using UnityEngine;

namespace HeistSim.Arena
{
    /// <summary>Applies the screen space palette dither pass to the camera output in play and edit mode.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class IsoDitherPost : MonoBehaviour
    {
        /// <summary>Dither pass material, shader HeistSim/IsoDitherPost.</summary>
        public Material postMaterial;

        /// <summary>Integer cell size in screen pixels per Bayer texel, 1 keeps the fine grid.</summary>
        [Range(1f, 8f)]
        public float bayerScale = 1f;

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (postMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (postMaterial.HasProperty("_BayerScale"))
            {
                postMaterial.SetFloat("_BayerScale", Mathf.Max(1f, bayerScale));
            }

            Graphics.Blit(source, destination, postMaterial);
        }
    }
}
