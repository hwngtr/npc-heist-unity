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

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (postMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            Graphics.Blit(source, destination, postMaterial);
        }
    }
}
