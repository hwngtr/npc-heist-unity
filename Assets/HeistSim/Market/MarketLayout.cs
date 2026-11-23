using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Placement math that maps market fixtures from tile space to world space, shared by the builder and the HUD.</summary>
    public static class MarketLayout
    {
        /// <summary>Whether the facing swaps the fixture footprint axes, matching the browser item placement.</summary>
        public static bool SwapsAxes(string face)
        {
            return face == "ne" || face == "sw";
        }

        /// <summary>Footprint width along the tile I axis and depth along the tile J axis, in tiles.</summary>
        public static void Footprint(FixtureKind kind, string face, out float widthI, out float depthJ)
        {
            if (SwapsAxes(face))
            {
                widthI = (float)kind.SizeI;
                depthJ = (float)kind.SizeJ;
            }
            else
            {
                widthI = (float)kind.SizeJ;
                depthJ = (float)kind.SizeI;
            }
        }

        /// <summary>Centre of a placed fixture in global tile coordinates, matching HeistGame item placement.</summary>
        public static Vector2 ItemPosition(Fixture fixture)
        {
            var kind = MarketData.Kind(fixture.Kind);
            Footprint(kind, fixture.Face, out var widthI, out var depthJ);
            var room = MarketData.Room(fixture.Room);
            return new Vector2(
                room.OriginI + (float)fixture.I + widthI * 0.5f,
                room.OriginJ + (float)fixture.J + depthJ * 0.5f);
        }

        /// <summary>Top surface height of a placed fixture in world units.</summary>
        public static float ItemHeight(Fixture fixture)
        {
            var kind = MarketData.Kind(fixture.Kind);
            return kind.Place == "surface" ? 0.95f + (float)kind.Height : (float)kind.Height;
        }

        /// <summary>Returns true when the snapshot bag already carries the given loot id.</summary>
        public static bool InBag(System.Collections.Generic.IReadOnlyList<string> bag, string id)
        {
            if (bag == null)
            {
                return false;
            }

            for (var i = 0; i < bag.Count; i++)
            {
                if (bag[i] == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
