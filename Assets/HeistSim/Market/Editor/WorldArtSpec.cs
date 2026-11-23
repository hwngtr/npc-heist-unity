using System;
using System.Collections.Generic;
using HeistSim.Arena;
using UnityEngine;

namespace HeistSim.Market.Editor
{
    /// <summary>Floor pattern vocabulary from web/js/market.js lines 888 to 917.</summary>
    public enum FloorPattern
    {
        /// <summary>Small square floor tiles.</summary>
        Tiles,
        /// <summary>Long boards laid across the room.</summary>
        Planks,
        /// <summary>Broad concrete slabs with ruled seams.</summary>
        Concrete,
        /// <summary>Alternating square floor checks.</summary>
        Checker,
    }

    /// <summary>One department visual signature: pattern, wall ink, floor ink and aisle icon ink.</summary>
    public readonly struct RoomSignature
    {
        /// <summary>Creates a room signature.</summary>
        public RoomSignature(string room, FloorPattern pattern, InkRole wall, InkRole floor, InkRole accent)
        {
            Room = room;
            Pattern = pattern;
            Wall = wall;
            Floor = floor;
            Accent = accent;
        }

        /// <summary>Room id from MarketData.Rooms.</summary>
        public string Room { get; }

        /// <summary>Floor pattern applied inside the room.</summary>
        public FloorPattern Pattern { get; }

        /// <summary>Ink wash painted on the back wall.</summary>
        public InkRole Wall { get; }

        /// <summary>Ink used by the floor pattern.</summary>
        public InkRole Floor { get; }

        /// <summary>Ink used by the aisle sign icon and the wall accent rule.</summary>
        public InkRole Accent { get; }
    }

    /// <summary>
    /// The twelve department identities. Each row pairs one of the four web floor patterns with a wall ink,
    /// a floor ink and an icon ink. The four values together stay unique across all twelve rooms, so a player
    /// can name a department from its pattern and ink mix without reading a label.
    /// </summary>
    public static class WorldArtSpec
    {
        /// <summary>Signature for every room in MarketData.Rooms, in the same order.</summary>
        public static readonly RoomSignature[] Signatures =
        {
            new RoomSignature("r1", FloorPattern.Tiles, InkRole.Coral, InkRole.Teal, InkRole.Sun),
            new RoomSignature("r2", FloorPattern.Planks, InkRole.Sun, InkRole.Sun, InkRole.Coral),
            new RoomSignature("r3", FloorPattern.Tiles, InkRole.Key, InkRole.InkBlue2, InkRole.Teal),
            new RoomSignature("r4", FloorPattern.Concrete, InkRole.InkBlue2, InkRole.Key, InkRole.CoralInk),
            new RoomSignature("r5", FloorPattern.Tiles, InkRole.Teal, InkRole.Teal, InkRole.InkBlue2),
            new RoomSignature("r6", FloorPattern.Checker, InkRole.Coral, InkRole.Sun, InkRole.CoralInk),
            new RoomSignature("r7", FloorPattern.Planks, InkRole.Coral, InkRole.Sun, InkRole.Teal),
            new RoomSignature("r8", FloorPattern.Concrete, InkRole.Teal, InkRole.InkBlue2, InkRole.Sun),
            new RoomSignature("r9", FloorPattern.Planks, InkRole.Sun, InkRole.Sun, InkRole.CoralInk),
            new RoomSignature("r10", FloorPattern.Tiles, InkRole.Key, InkRole.InkBlue2, InkRole.Coral),
            new RoomSignature("r11", FloorPattern.Concrete, InkRole.InkBlue2, InkRole.Key, InkRole.Sun),
            new RoomSignature("r12", FloorPattern.Tiles, InkRole.Teal, InkRole.Teal, InkRole.Coral),
        };

        private static readonly Dictionary<string, RoomSignature> ByRoom = BuildIndex();

        /// <summary>Returns the signature for a room id, or throws when the id is unknown.</summary>
        public static RoomSignature For(string room)
        {
            if (!ByRoom.TryGetValue(room, out var signature))
            {
                throw new ArgumentException("Unknown room id: " + room, nameof(room));
            }

            return signature;
        }

        /// <summary>Maps an ink role to the generated material key used by the level builder.</summary>
        public static string MaterialKey(InkRole role)
        {
            return role switch
            {
                InkRole.Paper => "wall",
                InkRole.Card => "floor",
                InkRole.Key => "inkBlue",
                InkRole.InkBlue2 => "inkBlue2",
                InkRole.Teal => "teal",
                InkRole.Sun => "sun",
                InkRole.Coral => "coral",
                InkRole.CoralInk => "coralInk",
                InkRole.Black => "black",
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ink role"),
            };
        }

        /// <summary>Returns the ArenaSpec colour carried by an ink role.</summary>
        public static Color InkColor(InkRole role)
        {
            return role switch
            {
                InkRole.Paper => ArenaSpec.Paper,
                InkRole.Card => ArenaSpec.Card,
                InkRole.Key => ArenaSpec.InkBlue,
                InkRole.InkBlue2 => ArenaSpec.InkBlue2,
                InkRole.Teal => ArenaSpec.Teal,
                InkRole.Sun => ArenaSpec.Sun,
                InkRole.Coral => ArenaSpec.Coral,
                InkRole.CoralInk => ArenaSpec.CoralInk,
                InkRole.Black => ArenaSpec.Black,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ink role"),
            };
        }

        private static Dictionary<string, RoomSignature> BuildIndex()
        {
            var index = new Dictionary<string, RoomSignature>(StringComparer.Ordinal);
            foreach (var signature in Signatures)
            {
                index.Add(signature.Room, signature);
            }

            return index;
        }
    }
}
