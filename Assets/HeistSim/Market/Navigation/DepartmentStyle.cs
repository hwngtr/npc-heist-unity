using System;
using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Ink signatures shared by the minimap cells and the department transition cue.</summary>
    public static class DepartmentStyle
    {
        /// <summary>Returns the ink signature assigned to a department room.</summary>
        public static Color Signature(RoomRect room)
        {
            return room.Type switch
            {
                "entrance" => MarketSceneSpec.InkBlue,
                "breakfast" => MarketSceneSpec.Sun,
                "dairy" => MarketSceneSpec.InkBlue2,
                "beverages" => MarketSceneSpec.CoralInk,
                "frozen" => MarketSceneSpec.InkBlue2,
                "checkout" => MarketSceneSpec.Coral,
                "floral" => MarketSceneSpec.Teal,
                "stockroom" => MarketSceneSpec.Black,
                "backshop" => MarketSceneSpec.Sun,
                "wet" => MarketSceneSpec.InkBlue,
                "wine" => MarketSceneSpec.CoralInk,
                "staff" => MarketSceneSpec.InkBlue2,
                _ => MarketSceneSpec.InkBlue2,
            };
        }

        /// <summary>Returns the room whose HUD zone name matches the given name, or null.</summary>
        public static RoomRect? RoomForZone(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName))
            {
                return null;
            }

            foreach (var room in MarketData.Rooms)
            {
                if (string.Equals(room.Name, zoneName, StringComparison.Ordinal))
                {
                    return room;
                }
            }

            return null;
        }

        /// <summary>Returns the ink signature for a HUD zone name.</summary>
        public static Color SignatureForZone(string zoneName)
        {
            var room = RoomForZone(zoneName);
            return room.HasValue ? Signature(room.Value) : MarketSceneSpec.InkBlue2;
        }
    }
}
