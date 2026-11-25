using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Stores authoritative floorplan tile bounds and baseline sorting order for a tall
    /// fixture or low divider wall segment so Market2DSeeThrough can keep characters
    /// sorted behind walls/fixtures while carving readable Bayer cutouts.
    /// </summary>
    public sealed class Iso2DOccluderBounds : MonoBehaviour
    {
        /// <summary>Minimum global tile I coordinate covered by this occluder.</summary>
        public float minI;

        /// <summary>Minimum global tile J coordinate covered by this occluder.</summary>
        public float minJ;

        /// <summary>Maximum global tile I coordinate covered by this occluder.</summary>
        public float maxI;

        /// <summary>Maximum global tile J coordinate covered by this occluder.</summary>
        public float maxJ;

        /// <summary>Baseline static painter sorting order assigned at build time.</summary>
        public int baseSortingOrder;

        /// <summary>True when this occluder belongs to a segmented interior LowWall partition.</summary>
        public bool isLowWall;
    }
}
