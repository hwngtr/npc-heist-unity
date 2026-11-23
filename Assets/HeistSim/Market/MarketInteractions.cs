using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>What the action button would do at the current player position, with the hold time the sim uses.</summary>
    public readonly struct MarketActionHint
    {
        /// <summary>Creates an action hint.</summary>
        public MarketActionHint(bool found, string label, float hold, Vector2 target, string prompt)
        {
            Found = found;
            Label = label;
            Hold = hold;
            Target = target;
            Prompt = prompt;
        }

        /// <summary>Whether an interaction is in reach.</summary>
        public bool Found { get; }

        /// <summary>Short player facing label, for example Stealing.</summary>
        public string Label { get; }

        /// <summary>Hold time in seconds that the sim requires for this interaction.</summary>
        public float Hold { get; }

        /// <summary>Target position in tile coordinates, used to anchor the on-screen prompt.</summary>
        public Vector2 Target { get; }

        /// <summary>Prompt text for the in-range badge.</summary>
        public string Prompt { get; }

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
            if (!string.IsNullOrEmpty(snapshot.ChannelKind))
            {
                return MarketActionHint.None;
            }

            if (snapshot.PanelInRange)
            {
                var panel = new Vector2((float)MarketData.Panel.I, (float)MarketData.Panel.J);
                return new MarketActionHint(true, "Cutting the power", PowerHold, panel, "Hold E: disable CCTV");
            }

            if (snapshot.DetaggerInRange)
            {
                var detagger = new Vector2((float)MarketData.Detagger.I, (float)MarketData.Detagger.J);
                return new MarketActionHint(true, "Clearing the tags", DetagHold, detagger, "Hold E: de-tag");
            }

            var nearest = NearestRemainingLoot(snapshot, out var distance);
            if (nearest.HasValue && distance <= StealRange)
            {
                var loot = nearest.Value;
                var position = MarketLayout.ItemPosition(MarketData.Fixture(loot.Id));
                if (loot.Id == "m_spirits" && !snapshot.VitrineOpen)
                {
                    return new MarketActionHint(true, "Opening the vitrine", VitrineHold, position, "Hold E: force the vitrine");
                }

                var prompt = "Hold E: steal " + loot.Label + " (" + loot.Value + ")";
                return new MarketActionHint(true, "Stealing the goods", 1.4f + loot.Value / 70f * 2.2f, position, prompt);
            }

            if (snapshot.ExitInRange && !snapshot.ExitBlockedByTags)
            {
                var exit = new Vector2((float)MarketData.EasGate.I, (float)MarketData.EasGate.J);
                return new MarketActionHint(true, "Leaving", 0f, exit, "Hold E: leave");
            }

            return MarketActionHint.None;
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
