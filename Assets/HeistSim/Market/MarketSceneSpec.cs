using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Shared paths, root names, capture settings and palette values for the playable market scene.</summary>
    public static class MarketSceneSpec
    {
        /// <summary>Scene asset written by the level builder and opened by tests and captures.</summary>
        public const string ScenePath = "Assets/HeistSim/Market/Market.unity";

        /// <summary>Folder that owns every market asset.</summary>
        public const string MarketDir = "Assets/HeistSim/Market";

        /// <summary>Folder for build generated assets.</summary>
        public const string GeneratedDir = MarketDir + "/Generated";

        /// <summary>Folder for the shared flat materials.</summary>
        public const string MaterialsDir = GeneratedDir + "/Materials";

        /// <summary>Folder for the shared flat meshes.</summary>
        public const string MeshDir = GeneratedDir + "/Meshes";

        /// <summary>Folder for the vendored TextMeshPro shader and font resources.</summary>
        public const string TextMeshProDir = GeneratedDir + "/TextMeshPro";

        /// <summary>Folder for the runtime resources the HUD loads by name.</summary>
        public const string ResourcesDir = MarketDir + "/Resources";

        /// <summary>Folder for the review capture.</summary>
        public const string CapturesDir = MarketDir + "/Captures";

        /// <summary>Capture file name inside the captures folder.</summary>
        public const string CaptureFileName = "Market.png";

        /// <summary>Capture width in pixels, matching the arena captures.</summary>
        public const int CaptureWidth = 1600;

        /// <summary>Capture height in pixels, matching the arena captures.</summary>
        public const int CaptureHeight = 900;

        /// <summary>Bayer cell size used by the LookD post pass.</summary>
        public const float BayerScale = 2f;

        /// <summary>Name of the camera root object.</summary>
        public const string RootCamera = "Main Camera";

        /// <summary>Name of the root object that carries the runner.</summary>
        public const string RootMarket = "Market";

        /// <summary>Name of the root object that carries the HUD canvas.</summary>
        public const string RootHud = "HUD";

        /// <summary>Name of the root object that carries the directional light.</summary>
        public const string RootLight = "Market Light";

        /// <summary>Every root object the builder writes, in creation order.</summary>
        public static readonly string[] RootNames = { RootCamera, RootMarket, RootHud, RootLight };

        /// <summary>Number of root objects in the built scene.</summary>
        public const int RootObjectCount = 4;

        /// <summary>Group that holds one child per placed fixture.</summary>
        public const string FixturesGroup = "Fixtures";

        /// <summary>Group that holds the loot markers.</summary>
        public const string LootGroup = "Loot";

        /// <summary>Group that holds the six CCTV rigs.</summary>
        public const string CamerasGroup = "Cameras";

        /// <summary>Group that holds the power panel and the de-tagger.</summary>
        public const string DevicesGroup = "Devices";

        /// <summary>Group that holds the player, guard, staff and guard vision fan.</summary>
        public const string ActorsGroup = "Actors";

        /// <summary>Child name of a CCTV vision cone.</summary>
        public const string ConeChild = "Cone";

        /// <summary>Orthographic half height that frames the 36 by 27 tile floorplan.</summary>
        public const float OrthographicSize = 16.5f;

        /// <summary>Camera position that looks down the isometric diagonal of the floorplan.</summary>
        public static readonly Vector3 CameraPosition = new Vector3(-4f, 24f, -8.5f);

        /// <summary>Camera look target at the centre of the floorplan.</summary>
        public static readonly Vector3 CameraTarget = new Vector3(18f, 0f, 13.5f);

        /// <summary>Paper stock, #f3ebdd from web/style.css line 9.</summary>
        public static readonly Color Paper = Rgb(0xF3EBDD);

        /// <summary>Card stock, #fbf6ec from web/style.css line 10.</summary>
        public static readonly Color Card = Rgb(0xFBF6EC);

        /// <summary>Key plate ink, #344a80 from web/style.css line 11.</summary>
        public static readonly Color InkBlue = Rgb(0x344A80);

        /// <summary>Half shade ink, #56628a from web/style.css line 12.</summary>
        public static readonly Color InkBlue2 = Rgb(0x56628A);

        /// <summary>Coral drum, #ee6852 from web/style.css line 14.</summary>
        public static readonly Color Coral = Rgb(0xEE6852);

        /// <summary>Deep coral rule, #a8402d from web/style.css line 15.</summary>
        public static readonly Color CoralInk = Rgb(0xA8402D);

        /// <summary>Sun drum, #ffd428 from web/style.css line 16.</summary>
        public static readonly Color Sun = Rgb(0xFFD428);

        /// <summary>Teal drum, #008c8a from web/style.css line 17.</summary>
        public static readonly Color Teal = Rgb(0x008C8A);

        /// <summary>Bot black, #242226 from web/js/press.js line 118.</summary>
        public static readonly Color Black = Rgb(0x242226);

        /// <summary>Converts a packed 0xRRGGBB value to an opaque colour.</summary>
        public static Color Rgb(int hex)
        {
            return new Color(
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >> 8) & 0xFF) / 255f,
                (hex & 0xFF) / 255f,
                1f);
        }
    }
}
