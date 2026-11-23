using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>What the action button would do at the current player position, with the hold time the sim uses.</summary>
    public readonly struct MarketActionHint
    {
        /// <summary>Creates an action hint.</summary>
        public MarketActionHint(bool found, string label, float hold)
        {
            Found = found;
            Label = label;
            Hold = hold;
        }

        /// <summary>Whether an interaction is in reach.</summary>
        public bool Found { get; }

        /// <summary>Short player facing label, for example Stealing.</summary>
        public string Label { get; }

        /// <summary>Hold time in seconds that the sim requires for this interaction.</summary>
        public float Hold { get; }

        /// <summary>No interaction in reach.</summary>
        public static MarketActionHint None => default;
    }

    /// <summary>Mirrors the market interaction ranges so the HUD can show a truthful hold progress line.</summary>
    public static class MarketInteractions
    {
        private const float PanelRange = 2.0f;
        private const float DetaggerRange = 2.3f;
        private const float StealRange = 2.4f;
        private const float PowerHold = 2.4f;
        private const float DetagHold = 1.8f;
        private const float VitrineHold = 3.2f;

        /// <summary>Returns the interaction the action button would start from the snapshot player position.</summary>
        public static MarketActionHint Probe(in HeistSnapshot snapshot)
        {
            var player = snapshot.Player;

            var panel = new Vector2((float)MarketData.Panel.I, (float)MarketData.Panel.J);
            if (!snapshot.CamerasDisabled && Vector2.Distance(player, panel) <= PanelRange)
            {
                return new MarketActionHint(true, "Cutting the power", PowerHold);
            }

            var detagger = new Vector2((float)MarketData.Detagger.I, (float)MarketData.Detagger.J);
            if (Vector2.Distance(player, detagger) <= DetaggerRange)
            {
                return new MarketActionHint(true, "Clearing the tags", DetagHold);
            }

            var nearest = NearestRemainingLoot(snapshot, out var distance);
            if (!nearest.HasValue || distance > StealRange)
            {
                return MarketActionHint.None;
            }

            var loot = nearest.Value;
            if (loot.Id == "m_spirits" && !snapshot.VitrineOpen)
            {
                return new MarketActionHint(true, "Opening the vitrine", VitrineHold);
            }

            return new MarketActionHint(true, "Stealing the goods", 1.4f + loot.Value / 70f * 2.2f);
        }

        /// <summary>Fraction of the hold reached, clamped to 0 to 1.</summary>
        public static float Progress(float heldSeconds, in MarketActionHint hint)
        {
            if (!hint.Found || hint.Hold <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(heldSeconds / hint.Hold);
        }

        private static LootEntry? NearestRemainingLoot(in HeistSnapshot snapshot, out float distance)
        {
            distance = float.MaxValue;
            LootEntry? best = null;
            foreach (var loot in MarketData.Loot)
            {
                if (MarketLayout.InBag(snapshot.Bag, loot.Id))
                {
                    continue;
                }

                var position = MarketLayout.ItemPosition(MarketData.Fixture(loot.Id));
                var candidate = Vector2.Distance(snapshot.Player, position);
                if (candidate < distance)
                {
                    distance = candidate;
                    best = loot;
                }
            }

            return best;
        }
    }
}
