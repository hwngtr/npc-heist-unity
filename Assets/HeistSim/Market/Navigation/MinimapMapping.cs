using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Pure tile to minimap mapping used by the minimap and by its tests.</summary>
    public static class MinimapMapping
    {
        /// <summary>Returns the grid column that owns a room cell.</summary>
        public static int CellColumn(RoomRect room)
        {
            return Mathf.Clamp(room.AtI, 0, MarketData.Columns - 1);
        }

        /// <summary>Returns the grid row that owns a room cell.</summary>
        public static int CellRow(RoomRect room)
        {
            return Mathf.Clamp(room.AtJ, 0, MarketData.Rows - 1);
        }

        /// <summary>Returns the index in MarketData.Rooms that owns a tile, or -1 when no room owns it.</summary>
        public static int RoomIndexForTile(Vector2 tile)
        {
            for (var i = 0; i < MarketData.Rooms.Length; i++)
            {
                var room = MarketData.Rooms[i];
                if (tile.x >= room.OriginI && tile.x < room.OriginI + MarketData.RoomTiles
                    && tile.y >= room.OriginJ && tile.y < room.OriginJ + MarketData.RoomTiles)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Returns the room cell index for a tile, clamping outside points into the floorplan.</summary>
        public static int ClampedRoomIndexForTile(Vector2 tile)
        {
            var exact = RoomIndexForTile(tile);
            if (exact >= 0)
            {
                return exact;
            }

            var column = Mathf.Clamp(Mathf.FloorToInt(tile.x / (float)MarketData.RoomTiles), 0, MarketData.Columns - 1);
            var row = Mathf.Clamp(Mathf.FloorToInt(tile.y / (float)MarketData.RoomTiles), 0, MarketData.Rows - 1);
            for (var i = 0; i < MarketData.Rooms.Length; i++)
            {
                var room = MarketData.Rooms[i];
                if (room.AtI == column && room.AtJ == row)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Returns the 0 to 1 position of a tile inside the whole floorplan.</summary>
        public static Vector2 Normalized(Vector2 tile)
        {
            return new Vector2(
                Mathf.Clamp01(tile.x / (float)MarketData.WidthTiles),
                Mathf.Clamp01(tile.y / (float)MarketData.HeightTiles));
        }

        /// <summary>Returns the inset cell rectangle in normalized minimap grid space.</summary>
        public static Rect CellRect(int roomIndex)
        {
            var room = MarketData.Rooms[roomIndex];
            const float inset = 0.012f;
            var width = 1f / MarketData.Columns;
            var height = 1f / MarketData.Rows;
            return new Rect(
                CellColumn(room) * width + inset,
                CellRow(room) * height + inset,
                width - inset * 2f,
                height - inset * 2f);
        }

        /// <summary>Converts a room local point into a global tile position.</summary>
        public static Vector2 GlobalTile(RoomRect room, double localI, double localJ)
        {
            return new Vector2((float)(room.OriginI + localI), (float)(room.OriginJ + localJ));
        }

        /// <summary>Returns the global tile of a fixture from its room local placement.</summary>
        public static Vector2 FixtureTile(string fixtureId)
        {
            var fixture = MarketData.Fixture(fixtureId);
            return GlobalTile(MarketData.Room(fixture.Room), fixture.I, fixture.J);
        }
    }
}
