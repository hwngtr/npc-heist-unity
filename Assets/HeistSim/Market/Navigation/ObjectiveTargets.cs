using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>One active objective and the known tile the arrow points at.</summary>
    public readonly struct ObjectiveTarget
    {
        /// <summary>Creates an objective target.</summary>
        public ObjectiveTarget(string id, string label, Vector2 tile)
        {
            Id = id;
            Label = label;
            Tile = tile;
        }

        /// <summary>Stable objective id.</summary>
        public string Id { get; }

        /// <summary>Short label shown beside the arrow.</summary>
        public string Label { get; }

        /// <summary>Global tile of the target.</summary>
        public Vector2 Tile { get; }
    }

    /// <summary>Resolves objective ids to the fixed points and loot positions of the level.</summary>
    public static class ObjectiveTargets
    {
        /// <summary>Returns the first incomplete objective and its target, or null when all are done.</summary>
        public static ObjectiveTarget? Next(in HeistSnapshot snapshot)
        {
            if (snapshot.Objectives == null)
            {
                return null;
            }

            foreach (var objective in snapshot.Objectives)
            {
                if (objective == null || objective.Done)
                {
                    continue;
                }

                var target = For(objective.Id, snapshot);
                if (target.HasValue)
                {
                    return target;
                }
            }

            return null;
        }

        /// <summary>Returns the target for one objective id, or null when the id is unknown.</summary>
        public static ObjectiveTarget? For(string id, in HeistSnapshot snapshot)
        {
            switch (id)
            {
                case "power":
                    return new ObjectiveTarget(id, "POWER", Point(MarketData.Panel));
                case "vitrine":
                    return new ObjectiveTarget(id, "VITRINE", MinimapMapping.FixtureTile("m_spirits"));
                case "quota":
                    return Quota(snapshot);
                case "detag":
                    return new ObjectiveTarget(id, "DETAG", Point(MarketData.Detagger));
                case "escape":
                    return new ObjectiveTarget(id, "EXIT", Point(MarketData.EasGate));
                default:
                    return null;
            }
        }

        /// <summary>Returns whether every objective except escape is complete.</summary>
        public static bool RequiredDone(in HeistSnapshot snapshot)
        {
            if (snapshot.Objectives == null)
            {
                return false;
            }

            foreach (var objective in snapshot.Objectives)
            {
                if (objective != null && objective.Id != "escape" && !objective.Done)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 Point(LevelPoint point)
        {
            return new Vector2((float)point.I, (float)point.J);
        }

        private static ObjectiveTarget Quota(in HeistSnapshot snapshot)
        {
            LootEntry? best = null;
            foreach (var entry in MarketData.Loot)
            {
                if (InBag(snapshot.Bag, entry.Id))
                {
                    continue;
                }

                if (!best.HasValue || entry.Value > best.Value.Value)
                {
                    best = entry;
                }
            }

            var chosen = best ?? MarketData.Loot[0];
            var room = MarketData.Room(chosen.Room);
            return new ObjectiveTarget("quota", "LOOT", MinimapMapping.GlobalTile(room, chosen.I, chosen.J));
        }

        private static bool InBag(IReadOnlyList<string> bag, string id)
        {
            if (bag == null)
            {
                return false;
            }

            for (var i = 0; i < bag.Count; i++)
            {
                if (string.Equals(bag[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
