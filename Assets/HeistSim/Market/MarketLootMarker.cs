using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Marks a loot disc so the runner can hide it once the good is in the bag.</summary>
    public sealed class MarketLootMarker : MonoBehaviour
    {
        /// <summary>Fixture id of the loot carried by this marker.</summary>
        public string lootId;
    }
}
