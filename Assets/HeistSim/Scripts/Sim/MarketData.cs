using System;
using System.Collections.Generic;

namespace HeistSim
{
    /// <summary>Axis aligned room rectangle on the Frischemarkt floorplan, in tiles.</summary>
    public readonly struct RoomRect
    {
        /// <summary>Creates a room rectangle.</summary>
        public RoomRect(string id, string type, string name, int atI, int atJ, int width, int depth)
        {
            Id = id;
            Type = type;
            Name = name;
            AtI = atI;
            AtJ = atJ;
            Width = width;
            Depth = depth;
        }

        /// <summary>Stable room id, for example r1.</summary>
        public string Id { get; }

        /// <summary>Department type used by the browser game, for example entrance.</summary>
        public string Type { get; }

        /// <summary>Human readable room name.</summary>
        public string Name { get; }

        /// <summary>Room origin on the global tile grid, horizontal.</summary>
        public int AtI { get; }

        /// <summary>Room origin on the global tile grid, vertical.</summary>
        public int AtJ { get; }

        /// <summary>Room width in tiles.</summary>
        public int Width { get; }

        /// <summary>Room depth in tiles.</summary>
        public int Depth { get; }

        /// <summary>Global tile column where this room starts.</summary>
        public int OriginI => AtI * MarketData.RoomTiles;

        /// <summary>Global tile row where this room starts.</summary>
        public int OriginJ => AtJ * MarketData.RoomTiles;
    }

    /// <summary>Fixture kind definition with its footprint size, height and render layer.</summary>
    public readonly struct FixtureKind
    {
        /// <summary>Creates a fixture kind.</summary>
        public FixtureKind(string name, string label, double sizeI, double sizeJ, double height, string place, bool against)
        {
            Name = name;
            Label = label;
            SizeI = sizeI;
            SizeJ = sizeJ;
            Height = height;
            Place = place;
            Against = against;
        }

        /// <summary>Kind identifier as written in the item list.</summary>
        public string Name { get; }

        /// <summary>Browser game display label.</summary>
        public string Label { get; }

        /// <summary>Footprint width in tiles before facing swaps the axes.</summary>
        public double SizeI { get; }

        /// <summary>Footprint depth in tiles before facing swaps the axes.</summary>
        public double SizeJ { get; }

        /// <summary>Fixture height in tiles, used for solidity and rendering.</summary>
        public double Height { get; }

        /// <summary>Placement layer: floor, surface, wall or rug.</summary>
        public string Place { get; }

        /// <summary>Whether the fixture stands against a wall.</summary>
        public bool Against { get; }
    }

    /// <summary>One placed fixture on the floorplan.</summary>
    public readonly struct Fixture
    {
        /// <summary>Creates a placed fixture.</summary>
        public Fixture(string id, string kind, string name, string room, double i, double j, string face, int price)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Room = room;
            I = i;
            J = j;
            Face = face;
            Price = price;
        }

        /// <summary>Stable fixture id.</summary>
        public string Id { get; }

        /// <summary>Fixture kind id.</summary>
        public string Kind { get; }

        /// <summary>Human readable fixture name.</summary>
        public string Name { get; }

        /// <summary>Room id that owns the fixture.</summary>
        public string Room { get; }

        /// <summary>Room local placement column, fractional as in the browser game.</summary>
        public double I { get; }

        /// <summary>Room local placement row, fractional as in the browser game.</summary>
        public double J { get; }

        /// <summary>Facing direction: ne, nw, sw or se.</summary>
        public string Face { get; }

        /// <summary>Display price in euro, as carried by the browser game.</summary>
        public int Price { get; }
    }

    /// <summary>One shoplifting target with its value and RF tag state.</summary>
    public readonly struct LootEntry
    {
        /// <summary>Creates a loot entry.</summary>
        public LootEntry(
            string id,
            string kind,
            string room,
            double i,
            double j,
            int value,
            double noise,
            bool tagged,
            bool vitrineGated,
            string label,
            string roomName)
        {
            Id = id;
            Kind = kind;
            Room = room;
            I = i;
            J = j;
            Value = value;
            Noise = noise;
            Tagged = tagged;
            VitrineGated = vitrineGated;
            Label = label;
            RoomName = roomName;
        }

        /// <summary>Fixture id that carries this loot.</summary>
        public string Id { get; }

        /// <summary>Fixture kind id that carries this loot.</summary>
        public string Kind { get; }

        /// <summary>Room id where the carrying fixture stands.</summary>
        public string Room { get; }

        /// <summary>Room local placement column of the carrying fixture.</summary>
        public double I { get; }

        /// <summary>Room local placement row of the carrying fixture.</summary>
        public double J { get; }

        /// <summary>Loot value in euro.</summary>
        public int Value { get; }

        /// <summary>Noise emitted when the loot is taken.</summary>
        public double Noise { get; }

        /// <summary>Whether the loot carries an active RF tag.</summary>
        public bool Tagged { get; }

        /// <summary>Whether the loot needs the spirits vitrine forced open first.</summary>
        public bool VitrineGated { get; }

        /// <summary>Browser game display label.</summary>
        public string Label { get; }

        /// <summary>Room name carried by the browser game loot table.</summary>
        public string RoomName { get; }
    }

    /// <summary>One CCTV camera with its sweep parameters.</summary>
    public readonly struct CameraDef
    {
        /// <summary>Creates a camera.</summary>
        public CameraDef(
            string id,
            string label,
            string room,
            double i,
            double j,
            double baseAngle,
            double sweepAmp,
            double sweepSpeed,
            double range,
            double halfAngle)
        {
            Id = id;
            Label = label;
            Room = room;
            I = i;
            J = j;
            BaseAngle = baseAngle;
            SweepAmp = sweepAmp;
            SweepSpeed = sweepSpeed;
            Range = range;
            HalfAngle = halfAngle;
        }

        /// <summary>Stable camera id.</summary>
        public string Id { get; }

        /// <summary>Browser game display label.</summary>
        public string Label { get; }

        /// <summary>Room id whose rectangle contains the camera point.</summary>
        public string Room { get; }

        /// <summary>Global tile column of the camera lens.</summary>
        public double I { get; }

        /// <summary>Global tile row of the camera lens.</summary>
        public double J { get; }

        /// <summary>Facing angle at the centre of the sweep, in radians.</summary>
        public double BaseAngle { get; }

        /// <summary>Sweep amplitude in radians.</summary>
        public double SweepAmp { get; }

        /// <summary>Sweep speed in radians per tick.</summary>
        public double SweepSpeed { get; }

        /// <summary>Sight range in tiles.</summary>
        public double Range { get; }

        /// <summary>Half of the camera cone in radians.</summary>
        public double HalfAngle { get; }
    }

    /// <summary>Circular camera motion detection zone.</summary>
    public readonly struct MarketMotionZone
    {
        /// <summary>Creates a motion zone.</summary>
        public MarketMotionZone(double i, double j, double radius, string label)
        {
            I = i;
            J = j;
            Radius = radius;
            Label = label;
        }

        /// <summary>Global tile column of the zone centre.</summary>
        public double I { get; }

        /// <summary>Global tile row of the zone centre.</summary>
        public double J { get; }

        /// <summary>Detection radius in tiles.</summary>
        public double Radius { get; }

        /// <summary>Browser game zone label.</summary>
        public string Label { get; }
    }

    /// <summary>Named point on the floorplan, for example the entrance or the power panel.</summary>
    public readonly struct LevelPoint
    {
        /// <summary>Creates a named point.</summary>
        public LevelPoint(double i, double j, string room)
        {
            I = i;
            J = j;
            Room = room;
        }

        /// <summary>Global tile column of the point.</summary>
        public double I { get; }

        /// <summary>Global tile row of the point.</summary>
        public double J { get; }

        /// <summary>Room id that owns the point.</summary>
        public string Room { get; }
    }

    /// <summary>Static Frischemarkt level data ported from the browser game MARKET_CONFIG.</summary>
    public static class MarketData
    {
        /// <summary>Room side length in tiles.</summary>
        public const int RoomTiles = 9;

        /// <summary>Floorplan width in rooms.</summary>
        public const int Columns = 4;

        /// <summary>Floorplan depth in rooms.</summary>
        public const int Rows = 3;

        /// <summary>Floorplan width in tiles.</summary>
        public const int WidthTiles = Columns * RoomTiles;

        /// <summary>Floorplan depth in tiles.</summary>
        public const int HeightTiles = Rows * RoomTiles;

        /// <summary>Loot value in euro needed to satisfy the quota objective.</summary>
        public const int Quota = 190;

        /// <summary>Bag capacity in item slots.</summary>
        public const int BagCapacity = 6;

        /// <summary>Doorway band start in room local tiles, mirroring the browser game gap.</summary>
        public const double DoorwayBandMin = 3.55;

        /// <summary>Doorway band end in room local tiles, mirroring the browser game gap.</summary>
        public const double DoorwayBandMax = 5.45;

        /// <summary>Objective ids in browser game declaration order.</summary>
        public static readonly string[] ObjectiveIds = { "power", "vitrine", "quota", "detag", "escape" };

        /// <summary>Room rectangles in browser game declaration order.</summary>
        public static readonly RoomRect[] Rooms =
        {
            new RoomRect("r1", "entrance", "Entrance · Obst & Gemüse · Pfand", 0, 0, 9, 9),
            new RoomRect("r2", "breakfast", "Frühstück, Backwaren & Trockenwaren", 1, 0, 9, 9),
            new RoomRect("r3", "dairy", "Kühlregal & Frischetheken", 2, 0, 9, 9),
            new RoomRect("r4", "beverages", "Getränkemarkt & Wein (Back)", 0, 1, 9, 9),
            new RoomRect("r5", "frozen", "Drogerie & Tiefkühl", 1, 1, 9, 9),
            new RoomRect("r6", "checkout", "Kassenzone · SCO · Vorkassen-Bistro", 3, 2, 9, 9),
            new RoomRect("r7", "floral", "Floral & Saison", 3, 0, 9, 9),
            new RoomRect("r8", "stockroom", "Lager & BOH", 3, 1, 9, 9),
            new RoomRect("r9", "backshop", "Backshop", 0, 2, 9, 9),
            new RoomRect("r10", "wet", "Fleisch & Fisch Vorbereitung", 1, 2, 9, 9),
            new RoomRect("r11", "wine", "Getränke & Wein Annexe", 2, 2, 9, 9),
            new RoomRect("r12", "staff", "Personal & Pausenraum", 2, 1, 9, 9),
        };

        /// <summary>Fixture kinds used by the market, with their footprint and height.</summary>
        public static readonly Dictionary<string, FixtureKind> Kinds = new Dictionary<string, FixtureKind>(StringComparer.Ordinal)
        {
            ["gondola-shelf"] = new FixtureKind("gondola-shelf", "low open sightline grocery shelf", 2.8, 0.92, 1.72, "floor", false),
            ["chilled-case"] = new FixtureKind("chilled-case", "glass-door refrigerated cabinet", 2.5, 0.88, 2.12, "floor", true),
            ["produce-island"] = new FixtureKind("produce-island", "fresh produce crate island", 1.9, 1.25, 0.98, "floor", false),
            ["apple-crate"] = new FixtureKind("apple-crate", "low wooden crate of red apples", 0.9, 0.65, 0.52, "floor", false),
            ["bakery-rack"] = new FixtureKind("bakery-rack", "SB-Backwaren bread station", 2.0, 0.85, 1.92, "floor", true),
            ["action-table"] = new FixtureKind("action-table", "Aktionsware discount bin", 2.1, 1.15, 1.70, "floor", false),
            ["liquor-cabinet"] = new FixtureKind("liquor-cabinet", "locked spirits and electronics vitrine", 1.8, 0.75, 2.22, "floor", true),
            ["checkout-lane"] = new FixtureKind("checkout-lane", "checkout conveyor and register", 2.6, 1.0, 1.85, "floor", false),
            ["eas-gate"] = new FixtureKind("eas-gate", "EAS anti-theft security gate", 0.4, 1.6, 1.52, "floor", false),
            ["pallet-stack"] = new FixtureKind("pallet-stack", "Euro-pallet wholesale stack", 1.3, 1.1, 1.25, "floor", false),
            ["shopping-cart"] = new FixtureKind("shopping-cart", "wire shopping trolley", 1.1, 1.6, 1.05, "floor", false),
            ["hand-basket"] = new FixtureKind("hand-basket", "yellow shopping basket", 0.7, 0.5, 0.42, "floor", false),
            ["pfand-machine"] = new FixtureKind("pfand-machine", "reverse vending bottle return", 1.2, 0.8, 1.95, "floor", true),
            ["aisle-sign"] = new FixtureKind("aisle-sign", "hanging aisle number sign", 0.1, 0.1, 2.5, "floor", false),
            ["store-sign"] = new FixtureKind("store-sign", "market wall sign", 1.6, 0.08, 0.0, "wall", false),
            ["rewe-arch"] = new FixtureKind("rewe-arch", "illuminated red entrance portal", 2.6, 0.5, 2.8, "floor", false),
            ["coffee-counter"] = new FixtureKind("coffee-counter", "Coffee-to-Go station", 1.4, 0.8, 1.65, "floor", false),
            ["service-counter"] = new FixtureKind("service-counter", "staffed fresh counter with striped awning", 2.0, 0.9, 1.62, "floor", false),
            ["self-checkout"] = new FixtureKind("self-checkout", "self-checkout terminal with receipt gate", 1.1, 0.95, 1.95, "floor", false),
            ["impulse-shelf"] = new FixtureKind("impulse-shelf", "checkout impulse module", 0.9, 0.45, 1.25, "floor", false),
            ["freezer-chest"] = new FixtureKind("freezer-chest", "chest freezer with glass lid", 1.6, 0.95, 1.05, "floor", false),
            ["beverage-shelf"] = new FixtureKind("beverage-shelf", "beverage and wine aisle", 2.2, 0.85, 1.9, "floor", false),
            ["bistro-counter"] = new FixtureKind("bistro-counter", "Vorkassenzone snack bistro", 1.8, 0.9, 1.55, "floor", false),
            ["deli-counter"] = new FixtureKind("deli-counter", "staffed deli and cold cuts counter", 1.8, 0.9, 1.52, "floor", false),
            ["wine-rack"] = new FixtureKind("wine-rack", "tall wine and prosecco rack", 1.6, 0.6, 1.95, "floor", true),
            ["freezer-wall"] = new FixtureKind("freezer-wall", "full-height glass-door freezer wall", 2.4, 0.7, 2.05, "floor", true),
            ["pallet-cage"] = new FixtureKind("pallet-cage", "wire mesh back-of-house pallet cage", 1.2, 1.2, 1.7, "floor", false),
            ["crate-tower"] = new FixtureKind("crate-tower", "stacked returnable beverage crates", 1.3, 1.1, 1.62, "floor", false),
            ["floral-stand"] = new FixtureKind("floral-stand", "bucket of cut flowers", 0.8, 0.8, 1.3, "floor", false),
            ["seasonal-display"] = new FixtureKind("seasonal-display", "seasonal octabin promotion", 1.8, 1.2, 1.28, "floor", false),
            ["dump-bin"] = new FixtureKind("dump-bin", "cardboard discard bin", 1.4, 0.9, 1.1, "floor", false),
            ["hand-wash"] = new FixtureKind("hand-wash", "hygiene and hand wash station", 0.7, 0.6, 1.6, "floor", true),
            ["server-rack"] = new FixtureKind("server-rack", "mini server rack", 0.4, 0.3, 0.52, "surface", false),
        };

        /// <summary>Every placed fixture on the decongested Frischemarkt floorplan.</summary>
        public static readonly Fixture[] Fixtures =
        {
            new Fixture("d_arch", "rewe-arch", "Illuminated red REWE entrance arch", "r1", 0.3, 3.2, "nw", 0),
            new Fixture("m_produce1", "produce-island", "REWE Bio Bananas & Carrots Crate", "r1", 2.8, 1.2, "ne", 22),
            new Fixture("d_pfand", "pfand-machine", "Pfand Bottle Return Machine", "r1", 0.3, 6.6, "nw", 0),
            new Fixture("d_cart1", "shopping-cart", "Shopping Trolley", "r1", 5.6, 2.0, "ne", 0),
            new Fixture("m_bakery", "bakery-rack", "SB-Backwaren: Bio Krustenbrot & Brezeln", "r2", 0.2, 0.6, "ne", 14),
            new Fixture("m_gondola1", "gondola-shelf", "Frühstück: REWE Bio Müsli & Lavazza Kaffee", "r2", 2.2, 2.2, "ne", 28),
            new Fixture("m_gondola2", "gondola-shelf", "Trockenware: REWE Bio Fusilli & Spaghetti", "r2", 2.2, 5.4, "ne", 32),
            new Fixture("m_endcap1", "pallet-stack", "Energy Drink Endcap Display", "r2", 6.8, 1.4, "nw", 36),
            new Fixture("m_chill1", "chilled-case", "Frischetheke: Dry-Aged Black Angus Ribeye", "r3", 5.6, 0.0, "nw", 46),
            new Fixture("m_chill2", "chilled-case", "Kühlregal: Wild Smoked Salmon & Caviar", "r3", 5.6, 5.4, "nw", 34),
            new Fixture("m_cheese", "produce-island", "REWE Bio Joghurt & Parmigiano Island", "r3", 2.0, 5.0, "ne", 29),
            new Fixture("d_meat", "service-counter", "Bedientheke Fleisch & Wurst", "r3", 0.4, 1.0, "ne", 0),
            new Fixture("d_fish", "service-counter", "Fischtheke mit Marktdach", "r3", 0.4, 7.0, "ne", 0),
            new Fixture("d_bev1", "beverage-shelf", "Wasser, Cola & Säfte", "r4", 0.6, 0.0, "ne", 0),
            new Fixture("m_spirits", "liquor-cabinet", "Spirituosen Vitrine: 18yr Single Malt & Champagne", "r4", 7.2, 1.0, "nw", 68),
            new Fixture("m_pallet1", "pallet-stack", "Wholesale Champagne Case", "r4", 2.4, 2.0, "ne", 54),
            new Fixture("d_freezer1", "freezer-chest", "Tiefkühl Pizza & Eis", "r5", 0.6, 1.4, "ne", 0),
            new Fixture("m_aktion1", "action-table", "Aktionsware: 20V Cordless Hammer Drill", "r5", 5.2, 1.4, "ne", 58),
            new Fixture("m_aktion2", "action-table", "Aktionsware: ANC Wireless Headphones", "r5", 5.2, 6.0, "ne", 44),
            new Fixture("m_checkout1", "checkout-lane", "Kasse 1 & Razor Blade Display", "r6", 0.8, 1.2, "ne", 38),
            new Fixture("m_checkout2", "checkout-lane", "Kasse 2 Express Lane", "r6", 0.8, 6.0, "ne", 25),
            new Fixture("d_sco1", "self-checkout", "Self-Checkout Terminal 1", "r6", 5.4, 1.2, "ne", 0),
            new Fixture("m_eas1", "eas-gate", "RF Anti-Theft Exit Scanner Gate", "r6", 7.2, 3.2, "ne", 0),
            new Fixture("m_server", "server-rack", "Security Office: EAS Deactivator & CCTV DVR", "r6", 7.2, 6.8, "ne", 0),
            new Fixture("f7a", "floral-stand", "Schnittblumen Eimer", "r7", 0.6, 1.2, "ne", 0),
            new Fixture("f7b", "floral-stand", "Schnittblumen Eimer", "r7", 0.6, 5.6, "ne", 0),
            new Fixture("m_season", "seasonal-display", "Advent Deko & Kerzen Set", "r7", 3.2, 1.4, "ne", 24),
            new Fixture("f7e", "pallet-cage", "Gitterbox Tannen & Topfe", "r7", 5.8, 5.6, "nw", 0),
            new Fixture("d_cage1", "pallet-cage", "Gitterbox Restposten", "r8", 0.7, 1.2, "ne", 0),
            new Fixture("d_cage2", "pallet-cage", "Gitterbox Leergut", "r8", 0.7, 6.4, "ne", 0),
            new Fixture("m_cage", "pallet-cage", "Gitterbox Charcoal BBQ & Grills", "r8", 5.2, 5.6, "ne", 51),
            new Fixture("d_shelf8", "gondola-shelf", "Palette Restposten", "r8", 6.8, 1.0, "nw", 0),
            new Fixture("d_br2a", "bakery-rack", "SB-Backwaren Mischbrot", "r9", 0.5, 1.2, "ne", 0),
            new Fixture("d_fr9", "freezer-wall", "TK Backwaren Wand", "r9", 6.4, 7.0, "ne", 0),
            new Fixture("d_deli1", "deli-counter", "Wursttheke Am Kostanteil", "r10", 0.6, 1.2, "ne", 0),
            new Fixture("m_ham", "deli-counter", "Truffelschinken Theke", "r10", 0.6, 6.0, "ne", 39),
            new Fixture("d_fish10", "service-counter", "Fischtheke Nebentheke", "r10", 5.6, 1.2, "ne", 0),
            new Fixture("m_lango", "freezer-wall", "TK Langoustinen Wand", "r10", 4.0, 7.2, "ne", 36),
            new Fixture("d_hw10", "hand-wash", "Hygienebecken", "r10", 7.6, 6.4, "nw", 0),
            new Fixture("m_prosecco", "wine-rack", "Prosecco Superiore Rack", "r11", 6.6, 1.4, "nw", 42),
            new Fixture("d_wine1", "wine-rack", "Weinregal Rotwein", "r11", 0.6, 1.2, "ne", 0),
            new Fixture("m_kist", "crate-tower", "Espresso 6er Kist", "r11", 3.4, 6.2, "ne", 33),
            new Fixture("d_hw12", "hand-wash", "Personalhygiene Becken", "r12", 0.7, 2.0, "ne", 0),
            new Fixture("d_brk", "bistro-counter", "Personal Break Theke", "r12", 6.6, 1.0, "nw", 0),
            new Fixture("d_srv2", "server-rack", "BOH DVR Rack", "r12", 7.0, 6.6, "nw", 0),
            new Fixture("t1a", "floral-stand", "Schnittblumen am Eingang", "r1", 0.8, 1.2, "ne", 0),
            new Fixture("t4a", "beverage-shelf", "Mineralwasser & Säfte", "r4", 0.6, 4.2, "ne", 0),
            new Fixture("t4b", "beverage-shelf", "Bier & Alkoholfrei", "r4", 0.6, 7.2, "ne", 0),
            new Fixture("t5d", "dump-bin", "Leergut Karton", "r5", 0.8, 6.2, "nw", 0),
            // One row above the r5 to r6 doorway band so the crossing at y=22 stays open.
            new Fixture("t6a", "checkout-lane", "Kasse 3", "r6", 0.4, 3.0, "ne", 0),
            new Fixture("t9a", "bakery-rack", "Backshop Brotkorb", "r9", 0.5, 5.4, "ne", 0),
            new Fixture("t11a", "wine-rack", "Weinregal: Merlot", "r11", 0.6, 6.0, "ne", 0),
            new Fixture("t12a", "bistro-counter", "Personalkuche Theke", "r12", 1.2, 5.8, "ne", 0),
            new Fixture("d_sign7", "aisle-sign", "Aisle 7 · Saison", "r7", 4.5, 0.6, "ne", 0),
            new Fixture("d_sign8", "aisle-sign", "Aisle 8 · Lager", "r8", 4.5, 0.6, "ne", 0),
            new Fixture("d_sign9", "aisle-sign", "Aisle 9 · Backshop", "r9", 4.5, 0.6, "ne", 0),
            new Fixture("d_sign10", "aisle-sign", "Aisle 10 · Fleisch & Fisch", "r10", 4.5, 0.6, "ne", 0),
            new Fixture("d_sign11", "aisle-sign", "Aisle 11 · Wein", "r11", 4.5, 0.6, "ne", 0),
            new Fixture("d_sign12", "aisle-sign", "Aisle 12 · Personal", "r12", 4.5, 0.6, "ne", 0),
            new Fixture("d_bin2", "dump-bin", "SB Kaffee Pallette", "r2", 6.6, 7.0, "nw", 0),
            new Fixture("d_bs1", "hand-basket", "Korbstapel Eingang", "r1", 6.0, 6.6, "ne", 0),
            new Fixture("d_hw3", "hand-wash", "Hygienebecken Kuhlregal", "r3", 7.2, 7.0, "nw", 0),
        };

        /// <summary>Every shoplifting target, keyed by the fixture that carries it.</summary>
        public static readonly LootEntry[] Loot =
        {
            new LootEntry("m_spirits", "liquor-cabinet", "r4", 7.2, 1.0, 68, 3.4, true, true, "18yr Islay Single Malt [RF-TAG]", "Drogerie & Tiefkühl"),
            new LootEntry("m_aktion1", "action-table", "r5", 5.2, 1.4, 58, 3.8, true, false, "Parkside 20V Cordless Drill [RF-TAG]", "Drogerie & Tiefkühl"),
            new LootEntry("m_pallet1", "pallet-stack", "r4", 2.4, 2.0, 54, 3.2, true, false, "Wholesale Champagne Box [RF-TAG]", "Getränkemarkt & Wein"),
            new LootEntry("m_chill1", "chilled-case", "r3", 5.6, 0.0, 46, 2.4, true, false, "Dry-Aged Wagyu Ribeye Pack [RF-TAG]", "Kühlregal & Frischetheken"),
            new LootEntry("m_aktion2", "action-table", "r5", 5.2, 6.0, 44, 2.8, true, false, "Silvercrest ANC Headphones [RF-TAG]", "Drogerie & Tiefkühl"),
            new LootEntry("m_checkout1", "checkout-lane", "r6", 0.8, 1.2, 38, 2.6, true, false, "Gillette 12-Blade Razor Pack [RF-TAG]", "Kassenzone & EAS Exit"),
            new LootEntry("m_chill2", "chilled-case", "r3", 5.6, 5.4, 34, 2.0, false, false, "Norwegian Smoked Salmon & Roe", "Kühlregal & Frischetheken"),
            new LootEntry("m_gondola2", "gondola-shelf", "r2", 2.2, 5.4, 32, 2.2, false, false, "Truffle Extra-Virgin Olive Oil", "Frühstück, Backwaren & Trockenwaren"),
            new LootEntry("m_cheese", "produce-island", "r3", 2.0, 5.0, 29, 1.9, false, false, "24-Month Parmigiano Wedge", "Kühlregal & Frischetheken"),
            new LootEntry("m_gondola1", "gondola-shelf", "r2", 2.2, 2.2, 28, 2.1, false, false, "Lavazza Crema Beans 1kg Bag", "Frühstück, Backwaren & Trockenwaren"),
            new LootEntry("m_season", "seasonal-display", "r7", 3.2, 1.4, 24, 1.6, true, false, "Advent Deko & Kerzen Set [RF-TAG]", "Floral & Saison"),
            new LootEntry("m_cage", "pallet-cage", "r8", 5.2, 5.6, 51, 3.0, true, false, "Charcoal BBQ Kettle [RF-TAG]", "Lager & BOH"),
            new LootEntry("m_prosecco", "wine-rack", "r11", 6.6, 1.4, 42, 2.6, true, false, "Prosecco Superiore 6-Pack [RF-TAG]", "Getranke & Wein Annexe"),
            new LootEntry("m_ham", "deli-counter", "r10", 0.6, 6.0, 39, 2.4, true, false, "Black Forest Truffle Ham [RF-TAG]", "Fleisch & Fisch Vorbereitung"),
            new LootEntry("m_lango", "freezer-wall", "r10", 4.0, 7.2, 36, 2.2, false, false, "Frozen Langoustine Box", "Fleisch & Fisch Vorbereitung"),
            new LootEntry("m_kist", "crate-tower", "r11", 3.4, 6.2, 33, 2.0, false, false, "Espresso 6er Kist", "Getranke & Wein Annexe"),
        };

        /// <summary>Every CCTV camera on the floorplan.</summary>
        public static readonly CameraDef[] Cameras =
        {
            new CameraDef("CAM-01", "CCTV 01 · Obst & Gemüse", "r2", 11.7, 0.35, Math.PI / 2, 0.70, 0.85, 5.0, 0.42),
            new CameraDef("CAM-02", "CCTV 02 · Frischetheken", "r3", 26.65, 1.7, Math.PI, 0.68, 0.72, 5.2, 0.42),
            new CameraDef("CAM-03", "CCTV 03 · Kassenzone & EAS Exit", "r6", 35.65, 19.7, Math.PI, 0.75, 0.90, 5.2, 0.42),
            new CameraDef("CAM-04", "CCTV 04 · Lager & BOH", "r8", 35.65, 10.7, Math.PI, 0.62, 0.80, 5.2, 0.42),
            new CameraDef("CAM-05", "CCTV 05 · Getranke Annexe", "r11", 20.7, 26.65, -Math.PI / 2, 0.70, 0.78, 5.2, 0.42),
            new CameraDef("CAM-06", "CCTV 06 · Personal & Saison", "r7", 35.65, 1.7, Math.PI, 0.66, 0.75, 5.4, 0.42),
        };

        /// <summary>Camera motion detection zones.</summary>
        public static readonly MarketMotionZone[] MotionZones =
        {
            new MarketMotionZone(4.5, 13.5, 1.6, "back aisle"),
            new MarketMotionZone(29.0, 21.0, 1.6, "checkout corridor"),
            new MarketMotionZone(31.5, 22.5, 1.6, "staff corridor"),
        };

        /// <summary>Store detective patrol route as tile positions, route start first.</summary>
        public static readonly GridPos[] GuardRoute =
        {
            // web/js/market.js line 1079 starts the route at 7.0,4.5, half a tile left of tile centre.
            new GridPos(7, 4, -0.5),
            new GridPos(13, 4),
            new GridPos(20, 4),
            new GridPos(31, 6),
            new GridPos(31, 13),
            new GridPos(20, 13),
            new GridPos(13, 13),
            new GridPos(4, 13),
            new GridPos(4, 22),
            new GridPos(13, 22),
            new GridPos(24, 22),
            new GridPos(31, 22),
        };

        /// <summary>Staff route as tile positions, route start first.</summary>
        public static readonly GridPos[] StaffRoute =
        {
            new GridPos(20, 4),
            new GridPos(31, 6),
            new GridPos(29, 13),
            new GridPos(13, 13),
            new GridPos(6, 4),
        };

        /// <summary>Player entrance point, beside the red arch.</summary>
        public static readonly LevelPoint Entrance = new LevelPoint(1.3, 4.5, "r1");

        /// <summary>Player spawn point inside the entrance room.</summary>
        public static readonly LevelPoint PlayerSpawn = new LevelPoint(2.8, 3.2, "r1");

        /// <summary>EAS exit gate point.</summary>
        public static readonly LevelPoint EasGate = new LevelPoint(32.4, 21.2, "r6");

        /// <summary>RF de-tagger point in the security office.</summary>
        public static readonly LevelPoint Detagger = new LevelPoint(32.2, 24.8, "r6");

        /// <summary>CCTV power panel point.</summary>
        public static readonly LevelPoint Panel = new LevelPoint(29.0, 25.5, "r6");

        /// <summary>Fixture kinds that stay passable despite being stand layer objects.</summary>
        public static readonly HashSet<string> PassableFixtures = new HashSet<string>(StringComparer.Ordinal)
        {
            "eas-gate",
            "rewe-arch",
            "aisle-sign",
            "store-sign",
        };

        private static readonly Dictionary<string, RoomRect> RoomsById = BuildRoomIndex();

        /// <summary>Returns the room rectangle for a room id, or throws when the id is unknown.</summary>
        public static RoomRect Room(string id)
        {
            if (!RoomsById.TryGetValue(id, out var room))
            {
                throw new ArgumentException($"Unknown room id: {id}", nameof(id));
            }

            return room;
        }

        /// <summary>Returns the kind definition for a kind id, or throws when the id is unknown.</summary>
        public static FixtureKind Kind(string id)
        {
            if (!Kinds.TryGetValue(id, out var kind))
            {
                throw new ArgumentException($"Unknown fixture kind: {id}", nameof(id));
            }

            return kind;
        }

        /// <summary>Returns the loot entry for a fixture id, or null when the fixture carries no loot.</summary>
        public static LootEntry? LootFor(string fixtureId)
        {
            foreach (var entry in Loot)
            {
                if (entry.Id == fixtureId)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Returns the fixture with the given id, or throws when the id is unknown.</summary>
        public static Fixture Fixture(string id)
        {
            foreach (var fixture in Fixtures)
            {
                if (fixture.Id == id)
                {
                    return fixture;
                }
            }

            throw new ArgumentException($"Unknown fixture id: {id}", nameof(id));
        }

        /// <summary>Returns the number of loot entries that carry an active RF tag.</summary>
        public static int RfTagCount()
        {
            var count = 0;
            foreach (var entry in Loot)
            {
                if (entry.Tagged)
                {
                    count++;
                }
            }

            return count;
        }

        private static Dictionary<string, RoomRect> BuildRoomIndex()
        {
            var index = new Dictionary<string, RoomRect>(StringComparer.Ordinal);
            foreach (var room in Rooms)
            {
                index.Add(room.Id, room);
            }

            return index;
        }
    }
}
