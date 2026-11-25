using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Exact isometric projection, depth sorting, and camera-relative input mapping
    /// shared between the tools/riso-press sprite baker and the Unity 2D scene.
    /// Matches press.py: TW = 64, TH = 32, TZ = 32 at 64 world px per Unity unit.
    /// </summary>
    public static class Iso2DMath
    {
        /// <summary>Tile width in Unity world units (64 world px / 64 ppu-base).</summary>
        public const float TileWidth = 1.0f;

        /// <summary>Tile height in Unity world units (32 world px / 64 ppu-base).</summary>
        public const float TileHeight = 0.5f;

        /// <summary>Vertical height scale in Unity world units per z unit.</summary>
        public const float HeightScale = 0.5f;

        /// <summary>Sorting order base for room floor and back-wall shells.</summary>
        public const int RoomBaseOrder = -20000;

        /// <summary>Sorting order for floor-projected vision cones and CCTV sweeps.</summary>
        public const int ConeSortingOrder = -5000;

        /// <summary>Projects global tile coordinates (i, j, z) into Unity 2D XY world space.</summary>
        public static Vector3 TileToWorld(float i, float j, float z = 0f)
        {
            var x = (i - j) * (TileWidth * 0.5f);
            var y = -(i + j) * (TileHeight * 0.5f) + z * HeightScale;
            return new Vector3(x, y, 0f);
        }

        /// <summary>Projects a 2D sim coordinate onto the Unity 2D isometric floor plane.</summary>
        public static Vector3 TileToWorld(Vector2 pos, float z = 0f)
        {
            return TileToWorld(pos.x, pos.y, z);
        }

        /// <summary>Computes the isometric painter's depth order for an entity at (i, j).</summary>
        public static int DepthOrder(float i, float j, int bias = 0)
        {
            return Mathf.RoundToInt((i + j) * 100f) + bias;
        }

        /// <summary>Computes the isometric painter's depth order for a room floor shell.</summary>
        public static int RoomOrder(int atI, int atJ)
        {
            return RoomBaseOrder + (atI + atJ) * 10 + atI;
        }

        /// <summary>
        /// Converts screen-space WASD input (x = right, y = up) into tile-grid (i, j) movement
        /// so W moves straight toward screen-up and D moves straight toward screen-right.
        /// </summary>
        public static Vector2 ScreenInputToGrid(float right, float up)
        {
            // Screen +X aligns with (+i, -j); Screen +Y aligns with (-i, -j).
            var di = right - up;
            var dj = -right - up;
            var v = new Vector2(di, dj);
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
        }

        /// <summary>Finds the room containing the given global tile position, or r1 as fallback.</summary>
        public static RoomRect RoomAt(Vector2 pos)
        {
            foreach (var room in MarketData.Rooms)
            {
                if (pos.x >= room.OriginI && pos.x < room.OriginI + room.Width
                    && pos.y >= room.OriginJ && pos.y < room.OriginJ + room.Depth)
                {
                    return room;
                }
            }

            return MarketData.Rooms[0];
        }
    }
}
