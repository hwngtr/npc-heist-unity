using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market.Editor
{
    /// <summary>Primitive used by one authored part of a market fixture.</summary>
    public enum DetailPrimitive
    {
        /// <summary>Axis aligned solid box.</summary>
        Box,
        /// <summary>Upright solid cylinder.</summary>
        Cylinder,
        /// <summary>Flat vertical panel in the local XY plane.</summary>
        Quad,
        /// <summary>Thin upright cylinder used for dots and halos.</summary>
        Disc,
        /// <summary>Unit cone used for carton gable tops, bottle shoulders and carrot tips.</summary>
        Cone,
        /// <summary>Unit ball used for heaped fruit and round tubs.</summary>
        Sphere,
    }

    /// <summary>Riso ink roles shared with the LookD post pass, extended by the two half tone drums.</summary>
    public enum InkRole
    {
        /// <summary>Paper stock, #f3ebdd.</summary>
        Paper,
        /// <summary>Card stock, #fbf6ec.</summary>
        Card,
        /// <summary>Midnight key plate, #344a80.</summary>
        Key,
        /// <summary>Half shade ink, #56628a.</summary>
        InkBlue2,
        /// <summary>Teal drum, including glass and green tints.</summary>
        Teal,
        /// <summary>Sun drum, including yellow, oak and brass tints.</summary>
        Sun,
        /// <summary>Coral drum, including red and walnut tints.</summary>
        Coral,
        /// <summary>Deep coral rule, #a8402d.</summary>
        CoralInk,
        /// <summary>Bot black, including charcoal tints.</summary>
        Black,
    }

    /// <summary>One composed primitive part in a fixture own tile space, centred on the footprint.</summary>
    public readonly struct DetailPart
    {
        /// <summary>Creates one part.</summary>
        public DetailPart(DetailPrimitive primitive, Vector3 offset, Vector3 size, InkRole ink, bool tiny, bool front = false)
        {
            Primitive = primitive;
            Offset = offset;
            Size = size;
            Ink = ink;
            Tiny = tiny;
            Front = front;
        }

        /// <summary>Primitive that renders this part.</summary>
        public DetailPrimitive Primitive { get; }

        /// <summary>Local position of the part centre, in tiles.</summary>
        public Vector3 Offset { get; }

        /// <summary>Local width, height and depth of the part, in tiles.</summary>
        public Vector3 Size { get; }

        /// <summary>Ink role that colours this part.</summary>
        public InkRole Ink { get; }

        /// <summary>True for a dot, ruled line or other tiny accent.</summary>
        public bool Tiny { get; }

        /// <summary>True for a product placed on the camera facing side of its fixture.</summary>
        public bool Front { get; }
    }

    /// <summary>
    /// Transcribes the web game shape lists for the 33 market fixture kinds and the three actor roles.
    /// Helper arguments use the web own space: u along the back, v out from it, z up;
    /// each helper stores a centred offset and a width, height, depth size in tiles.
    /// Source: web/js/market.js MARKET_KINDS, web/js/data.js KINDS_DATA, web/js/robot.js drawBot.
    /// </summary>
    public static class FixtureShapeData
    {
        private static DetailPart B(float u, float v, float z, float w, float d, float h, InkRole ink, bool tiny = false)
            => new DetailPart(DetailPrimitive.Box, new Vector3(u + w * 0.5f, z + h * 0.5f, v + d * 0.5f), new Vector3(w, h, d), ink, tiny);

        private static DetailPart C(float u, float v, float z, float r, float h, InkRole ink, bool tiny = false)
            => new DetailPart(DetailPrimitive.Cylinder, new Vector3(u, z + h * 0.5f, v), new Vector3(r * 2f, h, r * 2f), ink, tiny);

        private static DetailPart S(float u, float v, float z, float r, InkRole ink)
            => new DetailPart(DetailPrimitive.Cylinder, new Vector3(u, z, v), new Vector3(r * 2f, r * 2f, r * 2f), ink, false);

        private static DetailPart P(float u0, float v0, float z0, float u1, float v1, float z1, float t, InkRole ink, bool tiny = false)
            => new DetailPart(
                DetailPrimitive.Box,
                new Vector3((u0 + u1) * 0.5f, (z0 + z1) * 0.5f, (v0 + v1) * 0.5f),
                new Vector3(Mathf.Abs(u1 - u0) + t, Mathf.Abs(z1 - z0) + t, Mathf.Abs(v1 - v0) + t),
                ink, tiny);

        private static DetailPart Q(float u, float v, float z, float w, float h, InkRole ink)
            => new DetailPart(DetailPrimitive.Quad, new Vector3(u + w * 0.5f, z + h * 0.5f, v), new Vector3(w, h, 1f), ink, false);

        private static DetailPart D(float u, float v, float z, float r, InkRole ink)
        {
            var diameter = Mathf.Clamp(r * 0.07f, 0.06f, 0.24f);
            return new DetailPart(DetailPrimitive.Disc, new Vector3(u, z, v), new Vector3(diameter, 0.02f, diameter), ink, true);
        }

        /// <summary>
        /// Creates an upright cylinder with the given actual radius and height. The Unity cylinder primitive is two
        /// units tall, so the stored height is halved before DetailFactory applies it as a local scale.
        /// </summary>
        private static DetailPart Cyl(float u, float v, float bottom, float radius, float height, InkRole ink)
            => new DetailPart(
                DetailPrimitive.Cylinder,
                new Vector3(u, bottom + height * 0.5f, v),
                new Vector3(radius * 2f, height * 0.5f, radius * 2f),
                ink,
                false);

        // Product helpers use the same corner based u, v, z arguments as B. Each marks the part as Front
        // so DetailFactory lifts it clear of the fixture shell. Sizes stay in the 0.3 to 0.5 tile band
        // that reads at gameplay zoom.

        /// <summary>Camera facing carton or tray with a distinct box silhouette.</summary>
        private static DetailPart Prod(float u, float v, float bottom, float w, float d, float h, InkRole ink)
            => new DetailPart(
                DetailPrimitive.Box,
                new Vector3(u + w * 0.5f, bottom + h * 0.5f, v + d * 0.5f),
                new Vector3(w, h, d),
                ink,
                false,
                true);

        /// <summary>Camera facing tin, can or yoghurt tub. The diameter is w.</summary>
        private static DetailPart Tin(float u, float v, float bottom, float w, float h, InkRole ink)
            => new DetailPart(
                DetailPrimitive.Cylinder,
                new Vector3(u + w * 0.5f, bottom + h * 0.5f, v + w * 0.5f),
                new Vector3(w, h * 0.5f, w),
                ink,
                false,
                true);

        /// <summary>Camera facing cone for a gable top, bottle shoulder or carrot tip.</summary>
        private static DetailPart Cone(float u, float v, float bottom, float w, float h, InkRole ink)
            => new DetailPart(
                DetailPrimitive.Cone,
                new Vector3(u + w * 0.5f, bottom + h * 0.5f, v + w * 0.5f),
                new Vector3(w, h, w),
                ink,
                false,
                true);

        /// <summary>Camera facing ball of fruit. The diameter is w.</summary>
        private static DetailPart Ball(float u, float v, float bottom, float w, InkRole ink)
            => new DetailPart(
                DetailPrimitive.Sphere,
                new Vector3(u + w * 0.5f, bottom + w * 0.5f, v + w * 0.5f),
                new Vector3(w, w, w),
                ink,
                false,
                true);

        /// <summary>Authored part lists for every fixture kind in MarketData.Kinds.</summary>
        public static readonly IReadOnlyDictionary<string, DetailPart[]> Fixtures = new Dictionary<string, DetailPart[]>(StringComparer.Ordinal)
        {
            // web/js/market.js line 6
            ["gondola-shelf"] = new[]
            {
                // frame: plinth, spine, end caps, three shelves, two sun price strips and the sign
                B(0f, 0.04f, 0f, 2.8f, 0.84f, 0.14f, InkRole.Black),
                B(0.04f, 0.42f, 0.14f, 2.72f, 0.08f, 1.34f, InkRole.Black),
                B(0f, 0.04f, 0.14f, 0.06f, 0.84f, 1.34f, InkRole.Card),
                B(2.74f, 0.04f, 0.14f, 0.06f, 0.84f, 1.34f, InkRole.Card),
                B(0.06f, 0.04f, 0.54f, 2.68f, 0.84f, 0.05f, InkRole.Black),
                B(0.06f, 0.84f, 0.52f, 2.68f, 0.04f, 0.06f, InkRole.Sun),
                B(0.06f, 0.08f, 1.06f, 2.68f, 0.76f, 0.05f, InkRole.Black),
                B(0.06f, 0.8f, 1.04f, 2.68f, 0.04f, 0.06f, InkRole.Sun),
                B(0f, 0.08f, 1.46f, 2.8f, 0.76f, 0.05f, InkRole.Card),
                B(0.02f, 0.36f, 1.5f, 2.76f, 0.14f, 0.08f, InkRole.Card),
                P(1.4f, 0.5f, 1.51f, 1.4f, 0.5f, 2.05f, 0.0625f, InkRole.Black, false),
                B(0.74f, 0.42f, 1.82f, 1.32f, 0.16f, 0.3f, InkRole.Sun),
                // bottom row: carton, tin, capped drink bottle, cereal box, tin, snack box
                Prod(0.12f, 0.5f, 0.14f, 0.36f, 0.34f, 0.4f, InkRole.Sun),
                Tin(0.55f, 0.5f, 0.14f, 0.36f, 0.42f, InkRole.Coral),
                Tin(0.98f, 0.5f, 0.14f, 0.34f, 0.3f, InkRole.Teal),
                Cone(0.98f, 0.5f, 0.44f, 0.34f, 0.14f, InkRole.Teal),
                Tin(1.09f, 0.61f, 0.55f, 0.12f, 0.1f, InkRole.Coral),
                Prod(1.41f, 0.5f, 0.14f, 0.36f, 0.34f, 0.38f, InkRole.Card),
                Tin(1.84f, 0.5f, 0.14f, 0.36f, 0.4f, InkRole.Sun),
                Prod(2.27f, 0.5f, 0.14f, 0.32f, 0.34f, 0.36f, InkRole.Coral),
                // second row: can, box, box, tin, box, capped bottle
                Tin(0.12f, 0.5f, 0.6f, 0.34f, 0.38f, InkRole.Teal),
                Prod(0.55f, 0.5f, 0.6f, 0.34f, 0.32f, 0.4f, InkRole.Sun),
                Prod(0.98f, 0.5f, 0.6f, 0.34f, 0.32f, 0.42f, InkRole.Card),
                Tin(1.41f, 0.5f, 0.6f, 0.34f, 0.4f, InkRole.Coral),
                Prod(1.84f, 0.5f, 0.6f, 0.34f, 0.32f, 0.38f, InkRole.Sun),
                Tin(2.27f, 0.5f, 0.6f, 0.32f, 0.28f, InkRole.Coral),
                Cone(2.27f, 0.5f, 0.88f, 0.32f, 0.12f, InkRole.Coral),
                Tin(2.37f, 0.6f, 0.96f, 0.12f, 0.1f, InkRole.Black),
                // upper row: box, tin, box, tin, box
                Prod(0.18f, 0.5f, 1.12f, 0.4f, 0.32f, 0.34f, InkRole.Card),
                Tin(0.74f, 0.5f, 1.12f, 0.38f, 0.36f, InkRole.Sun),
                Prod(1.28f, 0.5f, 1.12f, 0.4f, 0.32f, 0.36f, InkRole.Coral),
                Tin(1.82f, 0.5f, 1.12f, 0.38f, 0.34f, InkRole.Teal),
                Prod(2.32f, 0.5f, 1.12f, 0.3f, 0.32f, 0.32f, InkRole.Sun),
                // top of the fixture: tall cartons and a bottle that breaks the skyline
                Prod(0.2f, 0.36f, 1.52f, 0.42f, 0.4f, 0.4f, InkRole.Card),
                Tin(0.9f, 0.36f, 1.52f, 0.34f, 0.34f, InkRole.Teal),
                Cone(0.9f, 0.36f, 1.86f, 0.34f, 0.14f, InkRole.Teal),
                Tin(0.99f, 0.45f, 1.96f, 0.14f, 0.1f, InkRole.Coral),
                Prod(1.5f, 0.36f, 1.52f, 0.42f, 0.4f, 0.38f, InkRole.Sun),
                Tin(2.1f, 0.36f, 1.52f, 0.4f, 0.4f, InkRole.Coral),
            },
            // web/js/market.js line 45
            ["chilled-case"] = new[]
            {
                // shell: white body, blue interior, two glass shelves and three front mullions
                B(0f, 0f, 0f, 2.5f, 0.84f, 2.08f, InkRole.Paper),
                B(0.08f, 0.12f, 0.28f, 2.34f, 0.7f, 1.56f, InkRole.Teal),
                B(0.08f, 0.14f, 0.78f, 2.34f, 0.66f, 0.05f, InkRole.Key),
                B(0.08f, 0.14f, 1.32f, 2.34f, 0.66f, 0.05f, InkRole.Key),
                B(0.1f, 0.84f, 0.28f, 0.045f, 0.05f, 1.56f, InkRole.Teal),
                B(1.24f, 0.84f, 0.28f, 0.045f, 0.05f, 1.56f, InkRole.Teal),
                B(2.38f, 0.84f, 0.28f, 0.045f, 0.05f, 1.56f, InkRole.Teal),
                B(1.18f, 0.85f, 0.95f, 0.14f, 0.05f, 0.06f, InkRole.Key),
                B(0f, 0f, 1.88f, 2.5f, 0.88f, 0.2f, InkRole.Key),
                B(0f, 0.85f, 1.84f, 2.5f, 0.04f, 0.07f, InkRole.Sun),
                // bottom shelf: gable-top milk, tubs and cheese, then a second row behind them
                Prod(0.18f, 0.52f, 0.34f, 0.4f, 0.26f, 0.3f, InkRole.Paper),
                Cone(0.18f, 0.52f, 0.64f, 0.4f, 0.14f, InkRole.Paper),
                Tin(0.72f, 0.52f, 0.34f, 0.38f, 0.3f, InkRole.Card),
                Prod(1.24f, 0.52f, 0.34f, 0.44f, 0.26f, 0.3f, InkRole.Sun),
                Prod(1.78f, 0.52f, 0.34f, 0.4f, 0.26f, 0.3f, InkRole.Paper),
                Cone(1.78f, 0.52f, 0.64f, 0.4f, 0.14f, InkRole.Paper),
                Tin(2.26f, 0.52f, 0.34f, 0.24f, 0.3f, InkRole.Card),
                Prod(0.3f, 0.62f, 0.34f, 0.36f, 0.2f, 0.4f, InkRole.Card),
                Tin(0.96f, 0.62f, 0.34f, 0.34f, 0.36f, InkRole.Coral),
                Prod(1.62f, 0.62f, 0.34f, 0.36f, 0.2f, 0.34f, InkRole.Sun),
                // middle shelf: coral meat trays, cheese and a shorter milk carton
                Prod(0.18f, 0.52f, 0.84f, 0.44f, 0.26f, 0.24f, InkRole.Coral),
                Prod(0.78f, 0.52f, 0.84f, 0.42f, 0.26f, 0.3f, InkRole.Sun),
                Prod(1.38f, 0.52f, 0.84f, 0.44f, 0.26f, 0.24f, InkRole.Coral),
                Prod(1.96f, 0.52f, 0.84f, 0.4f, 0.26f, 0.28f, InkRole.Paper),
                Cone(1.96f, 0.52f, 1.12f, 0.4f, 0.12f, InkRole.Paper),
                Prod(0.3f, 0.62f, 0.84f, 0.36f, 0.2f, 0.3f, InkRole.Card),
                Prod(0.96f, 0.62f, 0.84f, 0.36f, 0.2f, 0.28f, InkRole.Coral),
                Tin(1.62f, 0.62f, 0.84f, 0.34f, 0.3f, InkRole.Sun),
                // top shelf: cheese, yoghurt and meat read over the glass mullions
                Prod(0.22f, 0.52f, 1.38f, 0.42f, 0.26f, 0.34f, InkRole.Sun),
                Tin(0.8f, 0.52f, 1.38f, 0.38f, 0.28f, InkRole.Card),
                Prod(1.34f, 0.52f, 1.38f, 0.44f, 0.26f, 0.24f, InkRole.Coral),
                Prod(1.9f, 0.52f, 1.38f, 0.42f, 0.26f, 0.34f, InkRole.Sun),
                Prod(0.4f, 0.62f, 1.38f, 0.36f, 0.2f, 0.3f, InkRole.Card),
                Tin(1.1f, 0.62f, 1.38f, 0.34f, 0.28f, InkRole.Coral),
            },
            // web/js/market.js line 82
            ["produce-island"] = new[]
            {
                // base, four oak and sun crates, two sun price tags
                B(0f, 0f, 0f, 1.9f, 1.25f, 0.58f, InkRole.Card),
                B(0.06f, 0.06f, 0.58f, 0.86f, 0.54f, 0.28f, InkRole.Card),
                B(0.98f, 0.06f, 0.58f, 0.86f, 0.54f, 0.28f, InkRole.Card),
                B(0.06f, 0.65f, 0.58f, 0.86f, 0.54f, 0.28f, InkRole.Sun),
                B(0.98f, 0.65f, 0.58f, 0.86f, 0.54f, 0.28f, InkRole.Card),
                B(0.2f, 0.14f, 0.6f, 0.3f, 0.04f, 0.04f, InkRole.Sun),
                B(1.1f, 0.14f, 0.6f, 0.3f, 0.04f, 0.04f, InkRole.Sun),
                // first layer over the front crate rims: lemons, tomatoes and oranges
                Ball(0.1f, 0.1f, 0.86f, 0.38f, InkRole.Sun),
                Ball(0.5f, 0.1f, 0.86f, 0.38f, InkRole.Sun),
                Ball(1.04f, 0.1f, 0.86f, 0.38f, InkRole.Coral),
                Ball(1.44f, 0.1f, 0.86f, 0.38f, InkRole.Sun),
                // first layer over the back crate rims: green and coral produce
                Ball(0.1f, 0.7f, 0.86f, 0.36f, InkRole.Teal),
                Ball(0.5f, 0.7f, 0.86f, 0.36f, InkRole.Sun),
                Ball(1.04f, 0.7f, 0.86f, 0.36f, InkRole.Coral),
                Ball(1.44f, 0.7f, 0.86f, 0.36f, InkRole.Teal),
                // second heaped layer clears the rim and reads from the front
                Ball(0.22f, 0.3f, 1.14f, 0.34f, InkRole.Sun),
                Ball(0.66f, 0.3f, 1.14f, 0.34f, InkRole.Sun),
                Ball(1.2f, 0.3f, 1.14f, 0.34f, InkRole.Coral),
                Ball(0.24f, 0.88f, 1.14f, 0.32f, InkRole.Teal),
                Ball(1.24f, 0.88f, 1.14f, 0.32f, InkRole.Coral),
                // carrot tips poking out of the heap
                Cone(0.6f, 0.2f, 1.1f, 0.16f, 0.3f, InkRole.Coral),
                Cone(1.56f, 0.78f, 1.1f, 0.16f, 0.3f, InkRole.Coral),
            },
            // web/js/market.js line 106
            ["apple-crate"] = new[]
            {
                // slatted crate, corner poles and green price strip with paper price dots
                B(0f, 0f, 0f, 0.9f, 0.65f, 0.12f, InkRole.Card),
                B(0f, 0.6f, 0.12f, 0.9f, 0.05f, 0.18f, InkRole.Card),
                B(0f, 0f, 0.12f, 0.9f, 0.05f, 0.18f, InkRole.Card),
                B(0f, 0.05f, 0.12f, 0.05f, 0.55f, 0.18f, InkRole.Card),
                B(0.85f, 0.05f, 0.12f, 0.05f, 0.55f, 0.18f, InkRole.Card),
                P(0.025f, 0.025f, 0f, 0.025f, 0.025f, 0.36f, 0.0625f, InkRole.Card, false),
                P(0.875f, 0.025f, 0f, 0.875f, 0.025f, 0.36f, 0.0625f, InkRole.Card, false),
                P(0.025f, 0.625f, 0f, 0.025f, 0.625f, 0.36f, 0.0625f, InkRole.Card, false),
                P(0.875f, 0.625f, 0f, 0.875f, 0.625f, 0.36f, 0.0625f, InkRole.Card, false),
                Q(0.1f, 0.65f, 0.14f, 0.7f, 0.1f, InkRole.Teal),
                D(0.26f, 0.655f, 0.19f, 1.1f, InkRole.Paper),
                D(0.36f, 0.655f, 0.19f, 1.1f, InkRole.Paper),
                D(0.6f, 0.655f, 0.19f, 1.1f, InkRole.Paper),
                // first layer of apples inside the crate
                Ball(0.1f, 0.1f, 0.3f, 0.22f, InkRole.Coral),
                Ball(0.36f, 0.1f, 0.3f, 0.22f, InkRole.Coral),
                Ball(0.62f, 0.1f, 0.3f, 0.22f, InkRole.Coral),
                Ball(0.22f, 0.3f, 0.3f, 0.22f, InkRole.Coral),
                Ball(0.5f, 0.3f, 0.3f, 0.22f, InkRole.Coral),
                // second layer heaps over the rim, with one sun golden apple
                Ball(0.14f, 0.14f, 0.56f, 0.24f, InkRole.Coral),
                Ball(0.42f, 0.14f, 0.56f, 0.24f, InkRole.Coral),
                Ball(0.68f, 0.14f, 0.56f, 0.24f, InkRole.Coral),
                Ball(0.28f, 0.34f, 0.56f, 0.24f, InkRole.Coral),
                Ball(0.56f, 0.34f, 0.56f, 0.24f, InkRole.Coral),
                Ball(0.4f, 0.4f, 0.52f, 0.2f, InkRole.Sun),
            },
            // web/js/market.js line 139
            ["bakery-rack"] = new[]
            {
                B(0f, 0f, 0f, 2f, 0.82f, 1.82f, InkRole.Card),
                B(0f, 0f, 1.62f, 2f, 0.86f, 0.22f, InkRole.Sun),
                B(0f, 0.86f, 1.6f, 2f, 0.04f, 0.06f, InkRole.Sun),
                B(0.12f, 0.25f, 0.45f, 0.45f, 0.42f, 0.18f, InkRole.Card),
                B(0.72f, 0.25f, 0.45f, 0.42f, 0.42f, 0.18f, InkRole.Coral),
                B(1.32f, 0.25f, 0.45f, 0.42f, 0.42f, 0.18f, InkRole.Card),
                B(0.12f, 0.25f, 0.98f, 0.45f, 0.42f, 0.18f, InkRole.Coral),
                B(0.72f, 0.25f, 0.98f, 0.45f, 0.42f, 0.18f, InkRole.Sun),
                B(1.32f, 0.25f, 0.98f, 0.45f, 0.42f, 0.18f, InkRole.Card),
                B(0.16f, 0.22f, 1.34f, 0.4f, 0.06f, 0.04f, InkRole.Sun),
                B(0.76f, 0.22f, 1.34f, 0.4f, 0.06f, 0.04f, InkRole.Sun),
                // six cone topped buns, three on each lower tray tier
                Cone(0.18f, 0.3f, 0.63f, 0.24f, 0.16f, InkRole.Sun),
                Cone(0.78f, 0.3f, 0.63f, 0.24f, 0.16f, InkRole.Coral),
                Cone(1.38f, 0.3f, 0.63f, 0.24f, 0.16f, InkRole.Sun),
                Cone(0.18f, 0.3f, 1.16f, 0.24f, 0.16f, InkRole.Coral),
                Cone(0.78f, 0.3f, 1.16f, 0.24f, 0.16f, InkRole.Sun),
                Cone(1.38f, 0.3f, 1.16f, 0.24f, 0.16f, InkRole.CoralInk),
            },
            // web/js/market.js line 159
            ["action-table"] = new[]
            {
                B(0f, 0f, 0f, 2.1f, 1.15f, 0.18f, InkRole.Card),
                B(0.05f, 0.05f, 0.18f, 2f, 1.05f, 0.52f, InkRole.Key),
                B(0.05f, 0.05f, 0.66f, 2f, 1.05f, 0.05f, InkRole.Black),
                B(0.14f, 0.16f, 0.71f, 0.48f, 0.38f, 0.34f, InkRole.Card),
                B(0.72f, 0.16f, 0.71f, 0.48f, 0.38f, 0.34f, InkRole.Sun),
                B(1.3f, 0.16f, 0.71f, 0.48f, 0.38f, 0.34f, InkRole.Card),
                B(0.14f, 0.62f, 0.71f, 0.48f, 0.38f, 0.34f, InkRole.Sun),
                B(0.72f, 0.62f, 0.71f, 0.48f, 0.38f, 0.34f, InkRole.Card),
                P(1.05f, 0.55f, 0.73f, 1.05f, 0.55f, 1.54f, 0.0625f, InkRole.Black, false),
                B(0.45f, 0.45f, 1.32f, 1.2f, 0.16f, 0.38f, InkRole.Sun),
                B(0.5f, 0.47f, 1.12f, 1.1f, 0.05f, 0.08f, InkRole.Coral),
                // cartons and capped bottles stacked in the discount bin
                Prod(0.4f, 0.85f, 0.16f, 0.42f, 0.28f, 0.4f, InkRole.Sun),
                Tin(0.3f, 0.75f, 1.0f, 0.34f, 0.3f, InkRole.Teal),
                Cone(0.3f, 0.75f, 1.3f, 0.34f, 0.14f, InkRole.Teal),
                Tin(0.39f, 0.84f, 1.4f, 0.12f, 0.1f, InkRole.Coral),
                Prod(0.9f, 0.78f, 1.0f, 0.4f, 0.3f, 0.38f, InkRole.Card),
                Tin(1.5f, 0.75f, 1.0f, 0.34f, 0.3f, InkRole.Coral),
                Cone(1.5f, 0.75f, 1.3f, 0.34f, 0.14f, InkRole.Coral),
                Tin(1.59f, 0.84f, 1.4f, 0.12f, 0.1f, InkRole.Black),
            },
            // web/js/market.js line 180
            ["liquor-cabinet"] = new[]
            {
                B(0f, 0f, 0f, 1.8f, 0.75f, 2.12f, InkRole.Black),
                B(0.08f, 0.1f, 0.25f, 1.64f, 0.6f, 1.65f, InkRole.Card),
                B(0.08f, 0.12f, 0.8f, 1.64f, 0.56f, 0.05f, InkRole.Sun),
                B(0.08f, 0.12f, 1.35f, 1.64f, 0.56f, 0.05f, InkRole.Sun),
                C(0.36f, 0.38f, 0.28f, 0.11f, 0.44f, InkRole.Teal),
                C(0.78f, 0.38f, 0.28f, 0.11f, 0.44f, InkRole.Coral),
                C(1.2f, 0.38f, 0.28f, 0.11f, 0.44f, InkRole.Sun),
                C(0.46f, 0.38f, 0.85f, 0.11f, 0.44f, InkRole.Coral),
                C(0.96f, 0.38f, 0.85f, 0.11f, 0.44f, InkRole.Key),
                C(1.42f, 0.38f, 0.85f, 0.11f, 0.44f, InkRole.Sun),
                B(0.22f, 0.22f, 1.42f, 0.52f, 0.36f, 0.34f, InkRole.Coral),
                B(0.96f, 0.22f, 1.42f, 0.58f, 0.36f, 0.34f, InkRole.Key),
                B(0.88f, 0.72f, 0.25f, 0.05f, 0.05f, 1.65f, InkRole.Teal),
                D(1.7f, 0.74f, 1.05f, 2.8f, InkRole.Sun),
                B(0f, 0f, 1.9f, 1.8f, 0.8f, 0.22f, InkRole.Black),
                B(0.12f, 0.02f, 1.92f, 1.56f, 0.72f, 0.16f, InkRole.Coral),
            },
            // web/js/market.js line 209
            ["checkout-lane"] = new[]
            {
                B(0f, 0f, 0f, 2.6f, 0.96f, 0.86f, InkRole.Key),
                B(0.08f, 0.1f, 0.86f, 1.65f, 0.76f, 0.06f, InkRole.Black),
                B(0.32f, 0.28f, 0.92f, 0.32f, 0.3f, 0.26f, InkRole.Sun),
                B(0.9f, 0.28f, 0.92f, 0.3f, 0.3f, 0.24f, InkRole.Coral),
                B(1.78f, 0.14f, 0.86f, 0.42f, 0.68f, 0.06f, InkRole.Teal),
                B(1.84f, 0.18f, 0.92f, 0.54f, 0.5f, 0.66f, InkRole.Key),
                B(1.9f, 0.2f, 1.32f, 0.42f, 0.44f, 0.06f, InkRole.Sun),
                P(2.06f, 0.26f, 0.92f, 2.06f, 0.26f, 1.52f, 0.0625f, InkRole.Black, false),
                B(1.82f, 0.16f, 1.46f, 0.48f, 0.3f, 0.22f, InkRole.Sun),
                B(2.24f, 0.08f, 0.86f, 0.34f, 0.8f, 0.08f, InkRole.Key),
                B(1.66f, 0.5f, 1.58f, 0.74f, 0.14f, 0.28f, InkRole.Sun),
            },
            // web/js/market.js line 230
            ["eas-gate"] = new[]
            {
                B(0.05f, 0.02f, 0f, 0.28f, 0.14f, 1.42f, InkRole.Key),
                B(0.08f, 0.04f, 0.18f, 0.22f, 0.1f, 1.05f, InkRole.Paper),
                D(0.19f, 0.09f, 1.44f, 3.2f, InkRole.Coral),
                B(0.05f, 1.42f, 0f, 0.28f, 0.14f, 1.42f, InkRole.Key),
                B(0.08f, 1.44f, 0.18f, 0.22f, 0.1f, 1.05f, InkRole.Paper),
                D(0.19f, 1.49f, 1.44f, 3.2f, InkRole.Coral),
                B(0.05f, 0.1f, 1.4f, 0.28f, 1.4f, 0.12f, InkRole.Key),
                B(0.05f, 0.1f, 1.42f, 0.28f, 1.36f, 0.04f, InkRole.Sun),
            },
            // web/js/market.js line 247
            ["pallet-stack"] = new[]
            {
                B(0f, 0f, 0f, 1.3f, 1.1f, 0.14f, InkRole.Card),
                B(0.06f, 0.06f, 0.14f, 0.58f, 0.48f, 0.52f, InkRole.Card),
                B(0.66f, 0.06f, 0.14f, 0.58f, 0.48f, 0.52f, InkRole.Sun),
                B(0.06f, 0.56f, 0.14f, 0.58f, 0.48f, 0.52f, InkRole.Sun),
                B(0.66f, 0.56f, 0.14f, 0.58f, 0.48f, 0.52f, InkRole.Card),
                B(0.18f, 0.18f, 0.66f, 0.92f, 0.72f, 0.52f, InkRole.Teal),
                // second tier of cartons and cans above the pallet
                Prod(0.2f, 0.2f, 1.08f, 0.42f, 0.36f, 0.4f, InkRole.Card),
                Prod(0.72f, 0.2f, 1.08f, 0.42f, 0.36f, 0.4f, InkRole.Sun),
                Tin(0.3f, 0.66f, 1.08f, 0.34f, 0.4f, InkRole.Coral),
                Tin(0.78f, 0.66f, 1.08f, 0.34f, 0.4f, InkRole.Teal),
            },
            // web/js/market.js line 262
            ["shopping-cart"] = new[]
            {
                // wire frame, four wheels, basket floor and rim, push handle and coral grip
                C(0.16f, 0.16f, 0f, 0.08f, 0.14f, InkRole.Black),
                C(0.94f, 0.16f, 0f, 0.08f, 0.14f, InkRole.Black),
                C(0.16f, 1.44f, 0f, 0.08f, 0.14f, InkRole.Black),
                C(0.94f, 1.44f, 0f, 0.08f, 0.14f, InkRole.Black),
                B(0.18f, 0.18f, 0.14f, 0.06f, 0.06f, 0.24f, InkRole.Key, true),
                B(0.86f, 0.18f, 0.14f, 0.06f, 0.06f, 0.24f, InkRole.Key, true),
                B(0.18f, 1.36f, 0.14f, 0.06f, 0.06f, 0.24f, InkRole.Key, true),
                B(0.86f, 1.36f, 0.14f, 0.06f, 0.06f, 0.24f, InkRole.Key, true),
                B(0.13f, 0.13f, 0.16f, 0.84f, 0.05f, 0.04f, InkRole.Key),
                B(0.13f, 1.42f, 0.16f, 0.84f, 0.05f, 0.04f, InkRole.Key),
                B(0.13f, 0.13f, 0.16f, 0.05f, 1.34f, 0.04f, InkRole.Key),
                B(0.92f, 0.13f, 0.16f, 0.05f, 1.34f, 0.04f, InkRole.Key),
                B(0.06f, 0.06f, 0.38f, 0.98f, 1.44f, 0.07f, InkRole.Key),
                B(0.11f, 0.11f, 0.45f, 0.06f, 0.06f, 0.43f, InkRole.Key, true),
                B(0.93f, 0.11f, 0.45f, 0.06f, 0.06f, 0.43f, InkRole.Key, true),
                B(0.11f, 1.39f, 0.45f, 0.06f, 0.06f, 0.43f, InkRole.Key, true),
                B(0.93f, 1.39f, 0.45f, 0.06f, 0.06f, 0.43f, InkRole.Key, true),
                B(0.06f, 0.06f, 0.58f, 0.98f, 0.05f, 0.05f, InkRole.Key),
                B(0.06f, 1.45f, 0.58f, 0.98f, 0.05f, 0.05f, InkRole.Key),
                B(0.06f, 0.06f, 0.58f, 0.05f, 1.44f, 0.05f, InkRole.Key),
                B(0.99f, 0.06f, 0.58f, 0.05f, 1.44f, 0.05f, InkRole.Key),
                B(0.06f, 0.06f, 0.82f, 0.98f, 0.06f, 0.06f, InkRole.Key),
                B(0.06f, 1.44f, 0.82f, 0.98f, 0.06f, 0.06f, InkRole.Key),
                B(0.06f, 0.06f, 0.82f, 0.06f, 1.44f, 0.06f, InkRole.Key),
                B(0.98f, 0.06f, 0.82f, 0.06f, 1.44f, 0.06f, InkRole.Key),
                B(0.06f, 1.04f, 0.58f, 0.98f, 0.04f, 0.28f, InkRole.Key),
                P(0.12f, 1.47f, 0.45f, 0.12f, 1.54f, 1f, 0.0625f, InkRole.Key, false),
                P(0.98f, 1.47f, 0.45f, 0.98f, 1.54f, 1f, 0.0625f, InkRole.Key, false),
                B(0.08f, 1.48f, 0.97f, 0.94f, 0.1f, 0.08f, InkRole.Coral),
                // groceries in the basket: cartons, cans, a capped bottle and a ball of fruit
                Prod(0.16f, 0.48f, 0.44f, 0.34f, 0.3f, 0.4f, InkRole.Sun),
                Tin(0.56f, 0.48f, 0.44f, 0.32f, 0.42f, InkRole.Coral),
                Prod(0.16f, 0.86f, 0.44f, 0.32f, 0.3f, 0.38f, InkRole.Card),
                Tin(0.58f, 0.86f, 0.44f, 0.3f, 0.34f, InkRole.Teal),
                Cone(0.58f, 0.86f, 0.78f, 0.3f, 0.12f, InkRole.Teal),
                Tin(0.67f, 0.95f, 0.86f, 0.12f, 0.12f, InkRole.Coral),
                Prod(0.2f, 1.16f, 0.44f, 0.3f, 0.26f, 0.44f, InkRole.Sun),
                Ball(0.6f, 1.18f, 0.66f, 0.28f, InkRole.Coral),
                Prod(0.3f, 0.6f, 0.86f, 0.34f, 0.26f, 0.3f, InkRole.Card),
                Tin(0.66f, 0.6f, 0.86f, 0.28f, 0.3f, InkRole.Sun),
            },
            // web/js/market.js line 546
            ["hand-basket"] = new[]
            {
                B(0f, 0f, 0f, 0.7f, 0.5f, 0.24f, InkRole.Sun),
                B(0f, 0f, 0.24f, 0.7f, 0.5f, 0.04f, InkRole.Black),
                P(0.16f, 0.25f, 0.28f, 0.3f, 0.25f, 0.42f, 0.05f, InkRole.Black, false),
                P(0.54f, 0.25f, 0.28f, 0.4f, 0.25f, 0.42f, 0.05f, InkRole.Black, false),
                // groceries inside the basket: paper milk carton, teal tin and a sun baguette
                Prod(0.05f, 0.08f, 0.28f, 0.2f, 0.16f, 0.2f, InkRole.Paper),
                Tin(0.29f, 0.08f, 0.28f, 0.16f, 0.22f, InkRole.Teal),
                Prod(0.49f, 0.1f, 0.28f, 0.16f, 0.22f, 0.16f, InkRole.Sun),
            },
            // web/js/market.js line 559
            ["pfand-machine"] = new[]
            {
                B(0f, 0f, 0f, 1.2f, 0.8f, 1.92f, InkRole.Sun),
                B(0.1f, 0.78f, 0.3f, 1f, 0.05f, 1.05f, InkRole.Black),
                B(0.22f, 0.8f, 1.3f, 0.76f, 0.03f, 0.26f, InkRole.Teal),
                B(0.3f, 0.8f, 0.52f, 0.6f, 0.03f, 0.14f, InkRole.Black),
                C(0.4f, 0.4f, 0.62f, 0.13f, 0.42f, InkRole.Teal),
                D(1.02f, 0.82f, 0.8f, 2.4f, InkRole.Teal),
                B(0f, 0f, 1.92f, 1.2f, 0.84f, 0.12f, InkRole.Sun),
            },
            // web/js/market.js line 575
            ["aisle-sign"] = new[]
            {
                P(0f, 0f, 0f, 0f, 0f, 2.35f, 0.0625f, InkRole.Key, false),
                B(0f, 0f, 2.1f, 1.3f, 0.1f, 0.46f, InkRole.Sun),
                B(0f, 0f, 2.04f, 1.3f, 0.12f, 0.06f, InkRole.Key),
                // department signage reads as ink bars and a key icon, no floating text
                B(0.08f, 0.055f, 2.2f, 0.24f, 0.02f, 0.24f, InkRole.Key),
                B(0.42f, 0.055f, 2.2f, 0.76f, 0.02f, 0.05f, InkRole.CoralInk),
                B(0.42f, 0.055f, 2.32f, 0.6f, 0.02f, 0.05f, InkRole.InkBlue2),
                B(0.42f, 0.055f, 2.44f, 0.42f, 0.02f, 0.05f, InkRole.Teal),
            },
            // web/js/market.js line 587
            ["store-sign"] = new[]
            {
                B(0f, 0f, 0f, 1.6f, 0.06f, 0.62f, InkRole.Coral),
                C(0.5f, 0.03f, 0.12f, 0.2f, 0.4f, InkRole.Paper),
                B(0.86f, 0.03f, 0.18f, 0.56f, 0.03f, 0.07f, InkRole.Sun),
                B(0.86f, 0.03f, 0.34f, 0.44f, 0.03f, 0.07f, InkRole.Sun),
            },
            // web/js/market.js line 599
            ["rewe-arch"] = new[]
            {
                B(0f, 0f, 0f, 0.22f, 0.5f, 2.5f, InkRole.Coral),
                B(2.38f, 0f, 0f, 0.22f, 0.5f, 2.5f, InkRole.Coral),
                B(0f, 0.05f, 2.35f, 2.6f, 0.4f, 0.28f, InkRole.Coral),
                B(0.1f, 0.05f, 2.2f, 2.4f, 0.34f, 0.08f, InkRole.Sun),
                B(0.3f, 0.06f, 2.4f, 2f, 0.3f, 0.1f, InkRole.Paper),
                B(0.55f, 0.02f, 1.92f, 1.5f, 0.12f, 0.3f, InkRole.Paper),
            },
            // web/js/market.js line 615
            ["coffee-counter"] = new[]
            {
                B(0f, 0f, 0f, 1.4f, 0.8f, 1.05f, InkRole.Coral),
                B(0f, 0f, 1.05f, 1.4f, 0.8f, 0.08f, InkRole.Paper),
                B(0.18f, 0.18f, 1.13f, 0.52f, 0.44f, 0.42f, InkRole.Black),
                B(0.26f, 0.24f, 1.3f, 0.36f, 0.28f, 0.06f, InkRole.Sun),
                C(0.95f, 0.28f, 1.13f, 0.09f, 0.2f, InkRole.Card),
                C(1.18f, 0.28f, 1.13f, 0.09f, 0.2f, InkRole.Card),
                B(0.18f, 0.2f, 1.48f, 1.04f, 0.12f, 0.2f, InkRole.Sun),
                P(1.1f, 0.4f, 1.13f, 1.1f, 0.4f, 1.55f, 0.05f, InkRole.Black, false),
            },
            // web/js/market.js line 632
            ["service-counter"] = new[]
            {
                B(0f, 0f, 0f, 2f, 0.9f, 1.12f, InkRole.Paper),
                B(0.08f, 0.82f, 0.3f, 1.84f, 0.05f, 0.72f, InkRole.Teal),
                B(0.16f, 0.2f, 1.12f, 0.5f, 0.46f, 0.1f, InkRole.Card),
                B(0.76f, 0.2f, 1.12f, 0.5f, 0.46f, 0.1f, InkRole.Card),
                B(1.36f, 0.2f, 1.12f, 0.5f, 0.46f, 0.1f, InkRole.Card),
                B(0.2f, 0.26f, 1.22f, 0.42f, 0.32f, 0.12f, InkRole.Coral),
                B(0.8f, 0.26f, 1.22f, 0.42f, 0.32f, 0.12f, InkRole.Sun),
                B(1.4f, 0.26f, 1.22f, 0.42f, 0.32f, 0.12f, InkRole.Key),
                P(0.06f, 0.04f, 0.6f, 0.06f, 0.04f, 1.52f, 0.0563f, InkRole.Black, false),
                P(1.94f, 0.04f, 0.6f, 1.94f, 0.04f, 1.52f, 0.0563f, InkRole.Black, false),
                B(0f, 0.04f, 1.44f, 0.5f, 0.86f, 0.07f, InkRole.Coral),
                B(0.5f, 0.04f, 1.44f, 0.5f, 0.86f, 0.07f, InkRole.Paper),
                B(1f, 0.04f, 1.44f, 0.5f, 0.86f, 0.07f, InkRole.Coral),
                B(1.5f, 0.04f, 1.44f, 0.5f, 0.86f, 0.07f, InkRole.Paper),
            },
            // web/js/market.js line 656
            ["self-checkout"] = new[]
            {
                B(0f, 0f, 0f, 0.72f, 0.9f, 1.32f, InkRole.Key),
                B(0.04f, 0.06f, 0.78f, 0.64f, 0.06f, 0.5f, InkRole.Black),
                P(0.36f, 0.2f, 1.32f, 0.36f, 0.2f, 1.76f, 0.0625f, InkRole.Black, false),
                B(0.1f, 0.08f, 1.56f, 0.52f, 0.16f, 0.34f, InkRole.Teal),
                B(0.2f, 0.1f, 1.42f, 0.32f, 0.05f, 0.08f, InkRole.Sun),
                B(0.8f, 0.02f, 0f, 0.1f, 0.1f, 1.42f, InkRole.Key),
                B(0.8f, 0.82f, 0f, 0.1f, 0.1f, 1.42f, InkRole.Key),
                B(0.8f, 0.02f, 1.36f, 0.1f, 0.9f, 0.08f, InkRole.Key),
                D(0.86f, 0.07f, 1.4f, 2.6f, InkRole.Sun),
            },
            // web/js/market.js line 674
            ["impulse-shelf"] = new[]
            {
                B(0f, 0f, 0f, 0.9f, 0.45f, 0.12f, InkRole.Black),
                B(0.04f, 0.06f, 0.36f, 0.82f, 0.34f, 0.05f, InkRole.Black),
                B(0.04f, 0.06f, 0.86f, 0.82f, 0.34f, 0.05f, InkRole.Black),
                B(0.04f, 0.38f, 0.34f, 0.82f, 0.03f, 0.05f, InkRole.Sun),
                B(0.04f, 0.38f, 0.84f, 0.82f, 0.03f, 0.05f, InkRole.Sun),
                B(0.1f, 0.1f, 0.42f, 0.14f, 0.24f, 0.26f, InkRole.Coral),
                B(0.32f, 0.1f, 0.42f, 0.14f, 0.24f, 0.26f, InkRole.Coral),
                B(0.54f, 0.1f, 0.42f, 0.14f, 0.24f, 0.26f, InkRole.Sun),
                B(0.74f, 0.1f, 0.42f, 0.1f, 0.24f, 0.26f, InkRole.Teal),
                B(0.1f, 0.1f, 0.92f, 0.18f, 0.24f, 0.24f, InkRole.Coral),
                B(0.38f, 0.1f, 0.92f, 0.18f, 0.24f, 0.24f, InkRole.Coral),
                B(0.66f, 0.1f, 0.92f, 0.18f, 0.24f, 0.24f, InkRole.Sun),
            },
            // web/js/market.js line 696
            ["freezer-chest"] = new[]
            {
                B(0f, 0f, 0f, 1.6f, 0.95f, 0.82f, InkRole.Key),
                B(0.06f, 0.08f, 0.82f, 1.48f, 0.79f, 0.06f, InkRole.Teal),
                B(0.14f, 0.2f, 0.88f, 0.42f, 0.3f, 0.12f, InkRole.Sun),
                B(0.66f, 0.2f, 0.88f, 0.42f, 0.3f, 0.12f, InkRole.Coral),
                B(1.1f, 0.2f, 0.88f, 0.38f, 0.3f, 0.12f, InkRole.Sun),
                B(0f, 0f, 0.98f, 1.6f, 0.95f, 0.07f, InkRole.Teal),
                B(0f, 0f, 0.9f, 1.6f, 0.06f, 0.1f, InkRole.Key),
                B(0f, 0.89f, 0.9f, 1.6f, 0.06f, 0.1f, InkRole.Key),
                // frozen boxes and tubs standing on the glass lid
                Prod(0.2f, 0.2f, 1.06f, 0.4f, 0.3f, 0.34f, InkRole.Paper),
                Tin(0.72f, 0.2f, 1.06f, 0.36f, 0.32f, InkRole.Card),
                Prod(1.16f, 0.2f, 1.06f, 0.36f, 0.3f, 0.34f, InkRole.Sun),
            },
            // web/js/market.js line 714
            ["beverage-shelf"] = new[]
            {
                B(0f, 0.04f, 0f, 2.2f, 0.8f, 0.14f, InkRole.Black),
                B(0f, 0.04f, 0.62f, 2.2f, 0.8f, 0.05f, InkRole.Black),
                B(0f, 0.8f, 0.6f, 2.2f, 0.04f, 0.06f, InkRole.Sun),
                B(0f, 0.04f, 1.2f, 2.2f, 0.8f, 0.05f, InkRole.Black),
                B(0f, 0.8f, 1.18f, 2.2f, 0.04f, 0.06f, InkRole.Sun),
                B(0f, 0.04f, 1.78f, 2.2f, 0.8f, 0.06f, InkRole.Black),
                C(0.18f, 0.38f, 0.14f, 0.11f, 0.44f, InkRole.Teal),
                C(0.62f, 0.38f, 0.14f, 0.11f, 0.44f, InkRole.Black),
                C(1.06f, 0.38f, 0.14f, 0.11f, 0.44f, InkRole.Sun),
                C(1.5f, 0.38f, 0.14f, 0.11f, 0.44f, InkRole.Sun),
                C(0.26f, 0.38f, 0.68f, 0.11f, 0.48f, InkRole.Coral),
                C(0.78f, 0.38f, 0.68f, 0.11f, 0.48f, InkRole.Teal),
                C(1.3f, 0.38f, 0.68f, 0.11f, 0.48f, InkRole.Coral),
                C(1.82f, 0.38f, 0.68f, 0.11f, 0.48f, InkRole.Sun),
                B(0.2f, 0.3f, 1.26f, 0.4f, 0.34f, 0.42f, InkRole.Sun),
                B(0.8f, 0.3f, 1.26f, 0.4f, 0.34f, 0.42f, InkRole.Coral),
                B(1.4f, 0.3f, 1.26f, 0.4f, 0.34f, 0.42f, InkRole.Teal),
            },
            // web/js/market.js line 741
            ["bistro-counter"] = new[]
            {
                B(0f, 0f, 0f, 1.8f, 0.9f, 1.12f, InkRole.Coral),
                B(0f, 0f, 1.12f, 1.8f, 0.9f, 0.08f, InkRole.Paper),
                B(0.16f, 0.16f, 1.2f, 0.44f, 0.4f, 0.38f, InkRole.Black),
                C(0.86f, 0.28f, 1.2f, 0.09f, 0.2f, InkRole.Card),
                C(1.14f, 0.28f, 1.2f, 0.09f, 0.2f, InkRole.Card),
                B(0.2f, 0.2f, 1.44f, 1.4f, 0.12f, 0.22f, InkRole.Sun),
                C(0.4f, 0.7f, 0f, 0.2f, 0.62f, InkRole.Card),
                C(1.3f, 0.7f, 0f, 0.2f, 0.62f, InkRole.Card),
            },
            // web/js/market.js line 759
            ["deli-counter"] = new[]
            {
                B(0f, 0f, 0f, 1.8f, 0.86f, 1.4f, InkRole.Paper),
                B(0.08f, 0.1f, 0.88f, 1.64f, 0.64f, 0.48f, InkRole.Teal),
                B(0f, 0.86f, 0.86f, 1.8f, 0.06f, 0.1f, InkRole.Coral),
                B(0.18f, 0.18f, 1.42f, 0.38f, 0.4f, 0.28f, InkRole.Coral),
                B(0.66f, 0.18f, 1.42f, 0.38f, 0.4f, 0.28f, InkRole.Sun),
                B(1.14f, 0.18f, 1.42f, 0.38f, 0.4f, 0.28f, InkRole.Key),
            },
            // web/js/market.js line 773
            ["wine-rack"] = new[]
            {
                B(0f, 0f, 0f, 1.6f, 0.58f, 1.88f, InkRole.Black),
                B(0.06f, 0.06f, 0.52f, 1.48f, 0.06f, 0.1f, InkRole.Sun),
                B(0.06f, 0.06f, 1.06f, 1.48f, 0.06f, 0.1f, InkRole.Sun),
                C(0.22f, 0.14f, 0.58f, 0.07f, 0.44f, InkRole.Coral),
                C(0.52f, 0.14f, 0.58f, 0.07f, 0.44f, InkRole.Sun),
                C(0.82f, 0.14f, 0.58f, 0.07f, 0.44f, InkRole.Coral),
                C(1.12f, 0.14f, 0.58f, 0.07f, 0.44f, InkRole.Coral),
                C(0.22f, 0.14f, 1.12f, 0.07f, 0.44f, InkRole.Sun),
                C(0.52f, 0.14f, 1.12f, 0.07f, 0.44f, InkRole.Coral),
                C(0.82f, 0.14f, 1.12f, 0.07f, 0.44f, InkRole.Coral),
                C(1.12f, 0.14f, 1.12f, 0.07f, 0.44f, InkRole.Sun),
            },
            // web/js/market.js line 792
            ["freezer-wall"] = new[]
            {
                B(0f, 0f, 0f, 2.4f, 0.66f, 2f, InkRole.Key),
                B(0.08f, 0.08f, 0.3f, 2.24f, 0.5f, 1.54f, InkRole.Teal),
                B(0.1f, 0.1f, 0.84f, 2.2f, 0.46f, 0.06f, InkRole.Teal),
                B(0.8f, 0.1f, 0.3f, 0.06f, 0.46f, 1.54f, InkRole.Key),
                B(1.56f, 0.1f, 0.3f, 0.06f, 0.46f, 1.54f, InkRole.Key),
                B(0.18f, 0.2f, 0.42f, 0.44f, 0.3f, 0.34f, InkRole.Card),
                B(1f, 0.2f, 0.42f, 0.44f, 0.3f, 0.34f, InkRole.Card),
                B(1.76f, 0.2f, 0.42f, 0.44f, 0.3f, 0.34f, InkRole.Card),
            },
            // web/js/market.js line 808
            ["pallet-cage"] = new[]
            {
                B(0f, 0f, 0f, 1.2f, 1.16f, 0.14f, InkRole.Black),
                B(0f, 0f, 0.16f, 0.06f, 1.16f, 1.04f, InkRole.Key),
                B(1.14f, 0f, 0.16f, 0.06f, 1.16f, 1.04f, InkRole.Key),
                B(0f, 0f, 1.1f, 1.2f, 1.16f, 0.06f, InkRole.Key),
                B(0.1f, 0.6f, 0.2f, 1f, 0.1f, 0.9f, InkRole.Teal),
                B(0.22f, 0.12f, 0.28f, 0.72f, 0.48f, 0.66f, InkRole.Card),
            },
            // web/js/market.js line 822
            ["crate-tower"] = new[]
            {
                B(0f, 0f, 0f, 1.3f, 1.06f, 0.1f, InkRole.Card),
                B(0.06f, 0.1f, 0.12f, 1.18f, 0.44f, 0.9f, InkRole.Key),
                B(0.06f, 0.56f, 0.12f, 1.18f, 0.44f, 0.9f, InkRole.Key),
                B(0.2f, 0.24f, 1.02f, 0.88f, 0.1f, 0.06f, InkRole.Sun),
                B(0.2f, 0.7f, 1.02f, 0.88f, 0.1f, 0.06f, InkRole.Sun),
                // bottle caps standing on the upper crate lip, one per ink drum
                Tin(0.22f, 0.22f, 1.08f, 0.12f, 0.08f, InkRole.Teal),
                Tin(0.5f, 0.22f, 1.08f, 0.12f, 0.08f, InkRole.Sun),
                Tin(0.78f, 0.22f, 1.08f, 0.12f, 0.08f, InkRole.Coral),
                Tin(1.05f, 0.22f, 1.08f, 0.12f, 0.08f, InkRole.CoralInk),
            },
            // web/js/market.js line 835
            ["floral-stand"] = new[]
            {
                C(0.4f, 0.4f, 0.4f, 0.32f, 0.52f, InkRole.Card),
                P(0.4f, 0.5f, 0.4f, 0.4f, 0.5f, 1.24f, 0.0469f, InkRole.Teal, false),
                C(0.4f, 0.5f, 1.24f, 0.22f, 0.14f, InkRole.Coral),
                B(0.44f, 0.2f, 0.72f, 0.28f, 0.16f, 0.04f, InkRole.Sun),
                // three cut stems, each with its own bloom above the bucket rim
                Cyl(0.2f, 0.18f, 0.5f, 0.02f, 0.56f, InkRole.Teal),
                Ball(0.2f, 0.18f, 1.02f, 0.16f, InkRole.Coral),
                Cyl(0.4f, 0.14f, 0.5f, 0.02f, 0.6f, InkRole.Teal),
                Ball(0.4f, 0.14f, 1.06f, 0.16f, InkRole.Sun),
                Cyl(0.58f, 0.18f, 0.5f, 0.02f, 0.58f, InkRole.Teal),
                Ball(0.58f, 0.18f, 1.04f, 0.16f, InkRole.CoralInk),
            },
            // web/js/market.js line 847
            ["seasonal-display"] = new[]
            {
                B(0f, 0f, 0f, 1.8f, 0.66f, 1.14f, InkRole.Card),
                B(0.14f, 0.66f, 0.16f, 1.52f, 0.18f, 0.82f, InkRole.Coral),
                B(0.34f, 0.84f, 0.3f, 1.12f, 0.18f, 0.6f, InkRole.Sun),
                B(0.58f, 1.02f, 0.46f, 0.64f, 0.2f, 0.34f, InkRole.Teal),
                // heaped produce balls on the upper tiers: sun, coral and teal
                Ball(0.4f, 0.3f, 1.16f, 0.3f, InkRole.Sun),
                Ball(0.82f, 0.3f, 1.16f, 0.3f, InkRole.Coral),
                Ball(1.24f, 0.3f, 1.16f, 0.3f, InkRole.Teal),
            },
            // web/js/market.js line 859
            ["dump-bin"] = new[]
            {
                B(0f, 0f, 0f, 1.4f, 0.84f, 0.86f, InkRole.Key),
                B(0.12f, 0.84f, 0.1f, 1.16f, 0.1f, 0.66f, InkRole.Card),
                B(0.24f, 0.94f, 0.18f, 0.92f, 0.1f, 0.5f, InkRole.Card),
                B(0.2f, 0.44f, 0.86f, 1f, 0.18f, 0.04f, InkRole.Sun),
            },
            // web/js/market.js line 871
            ["hand-wash"] = new[]
            {
                B(0f, 0f, 0f, 0.7f, 0.86f, 0.56f, InkRole.Paper),
                B(0.1f, 0.12f, 0.1f, 0.5f, 0.62f, 0.08f, InkRole.Teal),
                C(0.35f, 0.7f, 0.4f, 0.05f, 0.3f, InkRole.Key),
                C(0.5f, 0.7f, 0.46f, 0.09f, 0.12f, InkRole.Coral),
            },
            // web/js/data.js line 659
            ["server-rack"] = new[]
            {
                B(0.02f, 0.02f, 0f, 0.36f, 0.26f, 0.03f, InkRole.Key),
                B(0.05f, 0.04f, 0.03f, 0.3f, 0.2f, 0.1f, InkRole.Black),
                B(0.05f, 0.04f, 0.15f, 0.3f, 0.2f, 0.1f, InkRole.Black),
                B(0.05f, 0.04f, 0.27f, 0.3f, 0.2f, 0.1f, InkRole.Black),
                B(0.05f, 0.04f, 0.39f, 0.3f, 0.2f, 0.09f, InkRole.Black),
                B(0.07f, 0.07f, 0.03f, 0.035f, 0.035f, 0.46f, InkRole.Key, true),
                B(0.295f, 0.07f, 0.03f, 0.035f, 0.035f, 0.46f, InkRole.Key, true),
                B(0.07f, 0.195f, 0.03f, 0.035f, 0.035f, 0.46f, InkRole.Key, true),
                B(0.295f, 0.195f, 0.03f, 0.035f, 0.035f, 0.46f, InkRole.Key, true),
                B(0.02f, 0.02f, 0.49f, 0.36f, 0.26f, 0.03f, InkRole.Key),
                D(0.09f, 0.24f, 0.08f, 1.4f, InkRole.Teal),
                D(0.13f, 0.24f, 0.08f, 1.4f, InkRole.Teal),
                D(0.09f, 0.24f, 0.2f, 1.4f, InkRole.Teal),
                D(0.09f, 0.24f, 0.32f, 1.4f, InkRole.Teal),
                D(0.13f, 0.24f, 0.32f, 1.4f, InkRole.Teal),
                D(0.09f, 0.24f, 0.43f, 1.4f, InkRole.Teal),
                P(0.2f, 0.24f, 0.08f, 0.32f, 0.24f, 0.08f, 0.035f, InkRole.Key, true),
                P(0.2f, 0.24f, 0.2f, 0.32f, 0.24f, 0.2f, 0.035f, InkRole.Key, true),
                P(0.2f, 0.24f, 0.32f, 0.32f, 0.24f, 0.32f, 0.035f, InkRole.Key, true),
                P(0.2f, 0.24f, 0.43f, 0.32f, 0.24f, 0.43f, 0.035f, InkRole.Key, true),
            },
        };

        /// <summary>
        /// Bot parts from web/js/robot.js lines 32-43 and the role marks from web/js/market-app.js lines 1039-1051.
        /// The black base disc comes from the Unity polish round, commit ef49549.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, DetailPart[]> Actors = new Dictionary<string, DetailPart[]>(StringComparer.Ordinal)
        {
            // player role: the web teal thief bot, enlarged to read at gameplay zoom. Guard and staff parts stay untouched.
            ["player"] = new[]
            {
                // sun floor ring, rendered as a Disc and left hollow by the black base above it
                new DetailPart(DetailPrimitive.Disc, new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.02f, 1.5f), InkRole.Sun, false),
                B(-0.5f, -0.5f, 0.04f, 1f, 1f, 0.04f, InkRole.Black), // centred base disc under the ring centre
                Cyl(0f, 0f, 0.06f, 0.34f, 1.1f, InkRole.Teal), // bright teal body, radius 0.34 and height 1.1
                B(-0.28f, -0.34f, 0.55f, 0.56f, 0.1f, 0.46f, InkRole.Black), // black face plate on the front
                D(-0.12f, -0.4f, 0.82f, 2.4f, InkRole.Paper), // robot.js line 83 left paper eye
                D(0.12f, -0.4f, 0.82f, 2.4f, InkRole.Paper), // robot.js line 83 right paper eye
                B(-0.22f, 0.3f, 0.62f, 0.44f, 0.3f, 0.42f, InkRole.Coral), // coral loot bag on the back
                Cyl(0f, 0f, 1.06f, 0.52f, 0.06f, InkRole.Sun), // sun cap brim
                Cyl(0f, 0f, 1.12f, 0.34f, 0.14f, InkRole.Sun), // sun cap crown
            },
            // guard role: bot parts from robot.js, mark from market-app.js as cited.
            ["guard"] = new[]
            {
                B(0f, 0f, 0.02f, 1f, 1f, 0.04f, InkRole.Black), // polish round base disc
                S(0f, 0f, 0.52f, 0.52f, InkRole.Black), // robot.js line 34 black ball
                D(-0.13f, -0.46f, 0.58f, 2.4f, InkRole.Paper), // robot.js line 83 left paper eye
                D(0.13f, -0.46f, 0.58f, 2.4f, InkRole.Paper), // robot.js line 83 right paper eye
                C(0f, 0f, 1.12f, 0.38f, 0.08f, InkRole.Key), // market-app.js line 1046 detective blue cap
                new DetailPart(DetailPrimitive.Disc, new Vector3(0f, 1.17f, 0f), new Vector3(0.38f, 0.02f, 0.38f), InkRole.Sun, false), // enlarged gold badge
            },
            // staff role: bot parts from robot.js, mark from market-app.js as cited.
            ["staff"] = new[]
            {
                B(0f, 0f, 0.02f, 1f, 1f, 0.04f, InkRole.Black), // polish round base disc
                S(0f, 0f, 0.52f, 0.52f, InkRole.Black), // robot.js line 34 black ball
                D(-0.13f, -0.46f, 0.58f, 2.4f, InkRole.Paper), // robot.js line 83 left paper eye
                D(0.13f, -0.46f, 0.58f, 2.4f, InkRole.Paper), // robot.js line 83 right paper eye
                C(0f, 0f, 1.12f, 0.38f, 0.08f, InkRole.Sun), // market-app.js line 1046 staff sun cap
                new DetailPart(DetailPrimitive.Disc, new Vector3(0f, 1.17f, 0f), new Vector3(0.38f, 0.02f, 0.38f), InkRole.Coral, false), // enlarged coral S mark
            },
        };

        /// <summary>
        /// Small loot items: bottle from web/js/market.js line 568, box from web/js/data.js line 617,
        /// basket from web/js/market.js lines 551-554.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, DetailPart[]> Loot = new Dictionary<string, DetailPart[]>(StringComparer.Ordinal)
        {
            ["bottle"] = new[]
            {
                C(0f, 0f, 0f, 0.13f, 0.42f, InkRole.Teal), // market.js line 568 glass bottle
            },
            ["box"] = new[]
            {
                B(0f, 0f, 0f, 0.8f, 0.8f, 0.52f, InkRole.Card), // data.js line 617 tan carton
                B(0.38f, 0f, 0.52f, 0.04f, 0.8f, 0.02f, InkRole.Sun, true), // data.js line 617 tape line
            },
            ["bag"] = new[]
            {
                B(0f, 0f, 0f, 0.7f, 0.5f, 0.24f, InkRole.Sun), // market.js line 551 basket body
                B(0f, 0f, 0.24f, 0.7f, 0.5f, 0.04f, InkRole.Black), // market.js line 552 rim
                P(0.16f, 0.25f, 0.28f, 0.30f, 0.25f, 0.42f, 0.05f, InkRole.Black), // market.js line 553 handle
                P(0.54f, 0.25f, 0.28f, 0.40f, 0.25f, 0.42f, 0.05f, InkRole.Black), // market.js line 554 handle
            },
        };

        /// <summary>Maps a carrying fixture kind to its small loot shape.</summary>
        private static readonly Dictionary<string, string> LootKinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["liquor-cabinet"] = "bottle",
            ["wine-rack"] = "bottle",
            ["gondola-shelf"] = "bag",
            ["produce-island"] = "bag",
            ["checkout-lane"] = "bag",
            ["action-table"] = "box",
            ["pallet-stack"] = "box",
            ["chilled-case"] = "box",
            ["seasonal-display"] = "box",
            ["pallet-cage"] = "box",
            ["deli-counter"] = "box",
            ["freezer-wall"] = "box",
            ["crate-tower"] = "box",
        };

        /// <summary>Returns the authored parts for a fixture kind, or throws for an unknown id.</summary>
        public static DetailPart[] Fixture(string kindId)
        {
            if (!Fixtures.TryGetValue(kindId, out var parts))
            {
                throw new ArgumentException("Unknown fixture shape kind: " + kindId, nameof(kindId));
            }

            return parts;
        }

        /// <summary>Returns the authored parts for an actor role, or throws for an unknown role.</summary>
        public static DetailPart[] Actor(string role)
        {
            if (!Actors.TryGetValue(role, out var parts))
            {
                throw new ArgumentException("Unknown actor role: " + role, nameof(role));
            }

            return parts;
        }

        /// <summary>Returns the loot shape key carried by a fixture kind.</summary>
        public static string LootKindFor(string kindId)
            => LootKinds.TryGetValue(kindId, out var key) ? key : "box";

        /// <summary>Returns the authored parts for a loot shape key, or throws for an unknown key.</summary>
        public static DetailPart[] LootShape(string key)
        {
            if (!Loot.TryGetValue(key, out var parts))
            {
                throw new ArgumentException("Unknown loot shape key: " + key, nameof(key));
            }

            return parts;
        }
    }
}
