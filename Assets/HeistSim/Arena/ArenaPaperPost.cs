using UnityEngine;

namespace HeistSim.Arena
{
    /// <summary>Applies the shared paper grain pass to the camera output in play and edit mode.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaPaperPost : MonoBehaviour
    {
        /// <summary>Print pass material, shader HeistSim/PaperGrainPost.</summary>
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
