using UnityEngine;

namespace TheLastEmpire
{
    public enum FurnitureKind
    {
        WoodenWardrobe = 0,
        MetalLockerCabinet = 1,
        CarpentryCraftTable = 2,
        ElectronicsCraftDesk = 3,
        WoodenDiningChair = 4,
        ComfyArmchair = 5,
        WoodenSingleBed = 6,
        AnalogRadio = 7,
        DeskLamp = 8,
        CookingPotStove = 9,
        PlanterBoxLime = 10,
        PlanterBoxPotato = 11,
        PlanterBoxTomato = 12,
        PlanterBoxCarrot = 13,
        PlanterBoxStrawberry = 14,
        VintageRefrigerator = 15,
        MedicalWallCabinet = 16,
        LeatherSofa = 17,
        StandingFloorLamp = 18,
        TallBookshelf = 19,
        WoodenDiningTable = 20,
        LowCoffeeTable = 21,
        KitchenCounterSink = 22,
        FirstAidStation = 23,
        WeaponDisplayRack = 24,
        WoodBurningStove = 25,
        WallMountedTV = 26,
        WovenAreaRug = 27,
        PottedIndoorFern = 28,
        TrashBinBarrel = 29
    }

    /// <summary>
    /// Procedurally constructs crisp stylized 3D multi-part furniture visuals at runtime
    /// so every one of the 30 furniture prefabs renders with clean silhouette, materials,
    /// lighting accents, and crop stages immediately (zero pink/missing meshes).
    /// </summary>
    public class FurnitureVisualBuilder : MonoBehaviour
    {
        [SerializeField] private FurnitureKind furnitureKind = FurnitureKind.WoodenWardrobe;
        public FurnitureKind Kind => furnitureKind;

        private bool _built = false;

        public void SetKind(FurnitureKind kind)
        {
            furnitureKind = kind;
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void BuildIfNeeded()
        {
            if (_built) return;
            if (transform.Find("VisualRoot") != null)
            {
                _built = true;
                return;
            }
            _built = true;

            GameObject root = new GameObject("VisualRoot");
            root.transform.SetParent(transform, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material darkWood = MakeMat(shader, new Color(0.32f, 0.21f, 0.13f), 0.22f);
            Material midWood = MakeMat(shader, new Color(0.50f, 0.35f, 0.21f), 0.24f);
            Material lightWood = MakeMat(shader, new Color(0.68f, 0.54f, 0.36f), 0.20f);
            Material steel = MakeMat(shader, new Color(0.42f, 0.46f, 0.50f), 0.58f);
            Material darkMetal = MakeMat(shader, new Color(0.20f, 0.22f, 0.24f), 0.48f);
            Material whiteMetal = MakeMat(shader, new Color(0.86f, 0.90f, 0.89f), 0.45f);
            Material brass = MakeMat(shader, new Color(0.82f, 0.66f, 0.26f), 0.68f);
            Material fabricWarm = MakeMat(shader, new Color(0.64f, 0.26f, 0.24f), 0.10f);
            Material fabricBlue = MakeMat(shader, new Color(0.26f, 0.45f, 0.58f), 0.12f);
            Material leather = MakeMat(shader, new Color(0.40f, 0.22f, 0.14f), 0.35f);
            Material cushion = MakeMat(shader, new Color(0.88f, 0.85f, 0.78f), 0.08f);
            Material redCross = MakeMat(shader, new Color(0.86f, 0.18f, 0.20f), 0.25f);
            Material soil = MakeMat(shader, new Color(0.24f, 0.16f, 0.11f), 0.05f);
            Material leaf = MakeMat(shader, new Color(0.24f, 0.58f, 0.24f), 0.15f);
            Material warmGlow = MakeMat(shader, new Color(1.0f, 0.86f, 0.52f), 0.2f, new Color(1.0f, 0.76f, 0.32f) * 1.4f);

            Transform t = root.transform;

            switch (furnitureKind)
            {
                case FurnitureKind.WoodenWardrobe:
                    Box(t, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(1.38f, 2.0f, 0.76f), darkWood);
                    Box(t, "DoorL", new Vector3(-0.33f, 1.0f, 0.39f), new Vector3(0.60f, 1.80f, 0.05f), midWood);
                    Box(t, "DoorR", new Vector3(0.33f, 1.0f, 0.39f), new Vector3(0.60f, 1.80f, 0.05f), midWood);
                    Box(t, "HandleL", new Vector3(-0.08f, 1.02f, 0.43f), new Vector3(0.05f, 0.22f, 0.05f), brass);
                    Box(t, "HandleR", new Vector3(0.08f, 1.02f, 0.43f), new Vector3(0.05f, 0.22f, 0.05f), brass);
                    Box(t, "Crown", new Vector3(0f, 2.03f, 0f), new Vector3(1.48f, 0.10f, 0.84f), darkWood);
                    break;

                case FurnitureKind.MetalLockerCabinet:
                    Box(t, "Body", new Vector3(0f, 1.02f, 0f), new Vector3(1.32f, 2.04f, 0.72f), steel);
                    Box(t, "DoorL", new Vector3(-0.31f, 1.02f, 0.37f), new Vector3(0.58f, 1.86f, 0.04f), darkMetal);
                    Box(t, "DoorR", new Vector3(0.31f, 1.02f, 0.37f), new Vector3(0.58f, 1.86f, 0.04f), darkMetal);
                    Box(t, "VentL", new Vector3(-0.31f, 1.65f, 0.40f), new Vector3(0.34f, 0.16f, 0.02f), steel);
                    Box(t, "VentR", new Vector3(0.31f, 1.65f, 0.40f), new Vector3(0.34f, 0.16f, 0.02f), steel);
                    Box(t, "Latch", new Vector3(0f, 1.05f, 0.41f), new Vector3(0.14f, 0.20f, 0.05f), brass);
                    break;

                case FurnitureKind.CarpentryCraftTable:
                    Box(t, "Top", new Vector3(0f, 0.84f, 0f), new Vector3(1.90f, 0.14f, 0.92f), lightWood);
                    Box(t, "Shelf", new Vector3(0f, 0.26f, 0f), new Vector3(1.66f, 0.08f, 0.72f), midWood);
                    Legs(t, 1.70f, 0.74f, 0.80f, 0.11f, darkWood);
                    Box(t, "Vise", new Vector3(-0.76f, 0.86f, 0.48f), new Vector3(0.28f, 0.16f, 0.14f), darkMetal);
                    Box(t, "ToolTray", new Vector3(0.45f, 0.96f, -0.18f), new Vector3(0.62f, 0.10f, 0.30f), fabricWarm);
                    break;

                case FurnitureKind.ElectronicsCraftDesk:
                    Box(t, "Top", new Vector3(0f, 0.82f, 0f), new Vector3(1.88f, 0.12f, 0.90f), darkMetal);
                    Box(t, "BackBoard", new Vector3(0f, 1.32f, -0.42f), new Vector3(1.84f, 0.92f, 0.08f), steel);
                    Legs(t, 1.72f, 0.74f, 0.78f, 0.09f, steel);
                    Box(t, "Monitor", new Vector3(-0.35f, 1.14f, -0.14f), new Vector3(0.62f, 0.42f, 0.12f), darkMetal);
                    Box(t, "Screen", new Vector3(-0.35f, 1.14f, -0.07f), new Vector3(0.54f, 0.34f, 0.03f), MakeMat(shader, new Color(0.18f, 0.78f, 0.64f), 0.4f, new Color(0.10f, 0.55f, 0.45f)));
                    Box(t, "SolderUnit", new Vector3(0.52f, 0.98f, -0.10f), new Vector3(0.42f, 0.22f, 0.32f), fabricBlue);
                    break;

                case FurnitureKind.WoodenDiningChair:
                    Box(t, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(0.62f, 0.08f, 0.60f), midWood);
                    Box(t, "BackRest", new Vector3(0f, 0.86f, -0.26f), new Vector3(0.60f, 0.68f, 0.08f), darkWood);
                    Legs(t, 0.50f, 0.48f, 0.46f, 0.07f, darkWood);
                    break;

                case FurnitureKind.ComfyArmchair:
                    Box(t, "Base", new Vector3(0f, 0.28f, 0.04f), new Vector3(0.96f, 0.36f, 0.88f), fabricWarm);
                    Box(t, "Cushion", new Vector3(0f, 0.48f, 0.08f), new Vector3(0.70f, 0.16f, 0.70f), cushion);
                    Box(t, "Back", new Vector3(0f, 0.74f, -0.34f), new Vector3(0.94f, 0.68f, 0.24f), fabricWarm);
                    Box(t, "ArmL", new Vector3(-0.44f, 0.52f, 0.02f), new Vector3(0.18f, 0.42f, 0.84f), darkWood);
                    Box(t, "ArmR", new Vector3(0.44f, 0.52f, 0.02f), new Vector3(0.18f, 0.42f, 0.84f), darkWood);
                    break;

                case FurnitureKind.WoodenSingleBed:
                    Box(t, "Frame", new Vector3(0f, 0.26f, 0f), new Vector3(1.36f, 0.36f, 2.08f), darkWood);
                    Box(t, "Mattress", new Vector3(0f, 0.48f, 0f), new Vector3(1.24f, 0.20f, 1.94f), cushion);
                    Box(t, "Blanket", new Vector3(0f, 0.54f, 0.28f), new Vector3(1.28f, 0.16f, 1.32f), fabricBlue);
                    Box(t, "Pillow", new Vector3(0f, 0.63f, -0.66f), new Vector3(0.86f, 0.16f, 0.38f), cushion);
                    Box(t, "Headboard", new Vector3(0f, 0.64f, -1.00f), new Vector3(1.40f, 1.02f, 0.12f), darkWood);
                    Box(t, "Footboard", new Vector3(0f, 0.44f, 1.00f), new Vector3(1.40f, 0.62f, 0.12f), darkWood);
                    break;

                case FurnitureKind.AnalogRadio:
                    // Side table stand + retro analog radio on top so it sits at readable height
                    Box(t, "StandTop", new Vector3(0f, 0.68f, 0f), new Vector3(0.78f, 0.08f, 0.54f), darkWood);
                    Legs(t, 0.64f, 0.42f, 0.66f, 0.07f, darkWood);
                    Box(t, "RadioCase", new Vector3(0f, 0.92f, 0f), new Vector3(0.64f, 0.38f, 0.34f), midWood);
                    Box(t, "SpeakerGrill", new Vector3(-0.14f, 0.92f, 0.18f), new Vector3(0.28f, 0.26f, 0.03f), darkMetal);
                    Box(t, "TuningDial", new Vector3(0.16f, 0.96f, 0.18f), new Vector3(0.22f, 0.12f, 0.03f), MakeMat(shader, new Color(0.30f, 0.95f, 0.55f), 0.3f, new Color(0.18f, 0.85f, 0.42f)));
                    Box(t, "KnobA", new Vector3(0.10f, 0.82f, 0.19f), new Vector3(0.07f, 0.07f, 0.05f), brass);
                    Box(t, "KnobB", new Vector3(0.22f, 0.82f, 0.19f), new Vector3(0.07f, 0.07f, 0.05f), brass);
                    Cyl(t, "Antenna", new Vector3(0.24f, 1.32f, -0.08f), new Vector3(0.03f, 0.28f, 0.03f), steel);
                    break;

                case FurnitureKind.DeskLamp:
                    Box(t, "Nightstand", new Vector3(0f, 0.36f, 0f), new Vector3(0.68f, 0.72f, 0.60f), darkWood);
                    Cyl(t, "LampBase", new Vector3(0f, 0.76f, 0f), new Vector3(0.26f, 0.04f, 0.26f), brass);
                    Cyl(t, "LampNeck", new Vector3(0f, 0.96f, 0f), new Vector3(0.06f, 0.20f, 0.06f), brass);
                    Cyl(t, "LampShade", new Vector3(0f, 1.18f, 0f), new Vector3(0.42f, 0.16f, 0.42f), warmGlow);
                    break;

                case FurnitureKind.CookingPotStove:
                    Box(t, "StoveBody", new Vector3(0f, 0.42f, 0f), new Vector3(0.96f, 0.84f, 0.82f), darkMetal);
                    Box(t, "BurnerPlate", new Vector3(0f, 0.86f, 0f), new Vector3(0.84f, 0.06f, 0.70f), steel);
                    Cyl(t, "PotBody", new Vector3(0f, 1.06f, 0f), new Vector3(0.56f, 0.20f, 0.56f), darkMetal);
                    Cyl(t, "PotLid", new Vector3(0f, 1.27f, 0f), new Vector3(0.58f, 0.04f, 0.58f), steel);
                    Box(t, "PotKnob", new Vector3(0f, 1.34f, 0f), new Vector3(0.10f, 0.08f, 0.10f), brass);
                    Box(t, "HandleL", new Vector3(-0.34f, 1.12f, 0f), new Vector3(0.12f, 0.06f, 0.12f), brass);
                    Box(t, "HandleR", new Vector3(0.34f, 1.12f, 0f), new Vector3(0.12f, 0.06f, 0.12f), brass);
                    break;

                case FurnitureKind.PlanterBoxLime:
                case FurnitureKind.PlanterBoxPotato:
                case FurnitureKind.PlanterBoxTomato:
                case FurnitureKind.PlanterBoxCarrot:
                case FurnitureKind.PlanterBoxStrawberry:
                    BuildSquarePlanterVisual(t, shader, midWood, darkWood, soil, leaf);
                    break;

                case FurnitureKind.VintageRefrigerator:
                    Box(t, "FridgeBody", new Vector3(0f, 0.96f, 0f), new Vector3(0.96f, 1.92f, 0.88f), whiteMetal);
                    Box(t, "SplitLine", new Vector3(0f, 1.32f, 0.45f), new Vector3(0.92f, 0.04f, 0.03f), steel);
                    Box(t, "HandleTop", new Vector3(0.34f, 1.56f, 0.47f), new Vector3(0.06f, 0.28f, 0.07f), steel);
                    Box(t, "HandleBot", new Vector3(0.34f, 0.96f, 0.47f), new Vector3(0.06f, 0.44f, 0.07f), steel);
                    break;

                case FurnitureKind.MedicalWallCabinet:
                    Box(t, "CabBody", new Vector3(0f, 0.92f, 0f), new Vector3(1.16f, 1.84f, 0.64f), whiteMetal);
                    Box(t, "CrossH", new Vector3(0f, 1.36f, 0.34f), new Vector3(0.42f, 0.12f, 0.04f), redCross);
                    Box(t, "CrossV", new Vector3(0f, 1.36f, 0.34f), new Vector3(0.12f, 0.42f, 0.04f), redCross);
                    break;

                case FurnitureKind.LeatherSofa:
                    Box(t, "SeatBase", new Vector3(0f, 0.28f, 0.06f), new Vector3(2.05f, 0.36f, 0.88f), leather);
                    Box(t, "CushionL", new Vector3(-0.48f, 0.48f, 0.08f), new Vector3(0.88f, 0.16f, 0.72f), leather);
                    Box(t, "CushionR", new Vector3(0.48f, 0.48f, 0.08f), new Vector3(0.88f, 0.16f, 0.72f), leather);
                    Box(t, "Back", new Vector3(0f, 0.72f, -0.34f), new Vector3(2.05f, 0.66f, 0.24f), leather);
                    Box(t, "ArmL", new Vector3(-0.96f, 0.50f, 0.02f), new Vector3(0.20f, 0.44f, 0.86f), darkWood);
                    Box(t, "ArmR", new Vector3(0.96f, 0.50f, 0.02f), new Vector3(0.20f, 0.44f, 0.86f), darkWood);
                    break;

                case FurnitureKind.StandingFloorLamp:
                    Cyl(t, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.05f, 0.46f), darkMetal);
                    Cyl(t, "Pole", new Vector3(0f, 0.92f, 0f), new Vector3(0.08f, 0.88f, 0.08f), brass);
                    Cyl(t, "Shade", new Vector3(0f, 1.84f, 0f), new Vector3(0.56f, 0.24f, 0.56f), warmGlow);
                    break;

                case FurnitureKind.TallBookshelf:
                    Box(t, "BackPanel", new Vector3(0f, 1.0f, -0.26f), new Vector3(1.50f, 2.0f, 0.08f), darkWood);
                    Box(t, "SideL", new Vector3(-0.72f, 1.0f, 0f), new Vector3(0.10f, 2.0f, 0.58f), darkWood);
                    Box(t, "SideR", new Vector3(0.72f, 1.0f, 0f), new Vector3(0.10f, 2.0f, 0.58f), darkWood);
                    for (int s = 0; s < 4; s++)
                    {
                        float sy = 0.25f + s * 0.52f;
                        Box(t, $"Shelf_{s}", new Vector3(0f, sy, 0f), new Vector3(1.40f, 0.07f, 0.54f), midWood);
                        if (s < 3)
                        {
                            Box(t, $"BooksA_{s}", new Vector3(-0.34f, sy + 0.18f, 0.02f), new Vector3(0.48f, 0.28f, 0.34f), s % 2 == 0 ? fabricWarm : fabricBlue);
                            Box(t, $"BooksB_{s}", new Vector3(0.26f, sy + 0.17f, 0.02f), new Vector3(0.54f, 0.26f, 0.32f), s == 1 ? lightWood : brass);
                        }
                    }
                    break;

                case FurnitureKind.WoodenDiningTable:
                    Box(t, "TableTop", new Vector3(0f, 0.80f, 0f), new Vector3(1.85f, 0.12f, 1.15f), midWood);
                    Legs(t, 1.60f, 0.90f, 0.76f, 0.11f, darkWood);
                    break;

                case FurnitureKind.LowCoffeeTable:
                    Box(t, "TableTop", new Vector3(0f, 0.44f, 0f), new Vector3(1.30f, 0.09f, 0.82f), lightWood);
                    Legs(t, 1.10f, 0.64f, 0.42f, 0.08f, darkWood);
                    break;

                case FurnitureKind.KitchenCounterSink:
                    Box(t, "CounterBase", new Vector3(0f, 0.46f, 0f), new Vector3(1.72f, 0.90f, 0.82f), midWood);
                    Box(t, "CounterTop", new Vector3(0f, 0.94f, 0f), new Vector3(1.78f, 0.08f, 0.88f), cushion);
                    Box(t, "SinkBasin", new Vector3(0.26f, 0.97f, 0f), new Vector3(0.58f, 0.05f, 0.48f), steel);
                    Box(t, "Faucet", new Vector3(0.26f, 1.14f, -0.28f), new Vector3(0.08f, 0.26f, 0.16f), steel);
                    break;

                case FurnitureKind.FirstAidStation:
                    Box(t, "Stand", new Vector3(0f, 0.56f, 0f), new Vector3(0.92f, 1.12f, 0.62f), whiteMetal);
                    Box(t, "KitBox", new Vector3(0f, 1.24f, 0f), new Vector3(0.68f, 0.26f, 0.46f), whiteMetal);
                    Box(t, "CrossH", new Vector3(0f, 1.38f, 0f), new Vector3(0.30f, 0.04f, 0.10f), redCross);
                    Box(t, "CrossV", new Vector3(0f, 1.38f, 0f), new Vector3(0.10f, 0.04f, 0.30f), redCross);
                    break;

                case FurnitureKind.WeaponDisplayRack:
                    Box(t, "RackBase", new Vector3(0f, 0.16f, 0f), new Vector3(1.56f, 0.32f, 0.62f), darkMetal);
                    Box(t, "BackBoard", new Vector3(0f, 1.06f, -0.24f), new Vector3(1.50f, 1.62f, 0.10f), darkWood);
                    Box(t, "RifleBar1", new Vector3(0f, 1.38f, -0.14f), new Vector3(1.12f, 0.12f, 0.10f), darkMetal);
                    Box(t, "RifleBar2", new Vector3(0f, 0.92f, -0.14f), new Vector3(1.02f, 0.12f, 0.10f), steel);
                    break;

                case FurnitureKind.WoodBurningStove:
                    Cyl(t, "StoveCore", new Vector3(0f, 0.56f, 0f), new Vector3(0.84f, 0.56f, 0.84f), darkMetal);
                    Cyl(t, "Chimney", new Vector3(0f, 1.56f, -0.14f), new Vector3(0.24f, 0.62f, 0.24f), darkMetal);
                    Box(t, "FireWindow", new Vector3(0f, 0.52f, 0.40f), new Vector3(0.36f, 0.28f, 0.06f), warmGlow);
                    break;

                case FurnitureKind.WallMountedTV:
                    Box(t, "TVStand", new Vector3(0f, 0.34f, 0f), new Vector3(1.24f, 0.68f, 0.62f), darkWood);
                    Box(t, "TVBody", new Vector3(0f, 1.04f, 0f), new Vector3(0.94f, 0.72f, 0.58f), darkMetal);
                    Box(t, "TVScreen", new Vector3(0f, 1.06f, 0.30f), new Vector3(0.76f, 0.54f, 0.03f), MakeMat(shader, new Color(0.45f, 0.68f, 0.85f), 0.5f, new Color(0.28f, 0.48f, 0.65f)));
                    break;

                case FurnitureKind.WovenAreaRug:
                    Box(t, "RugOuter", new Vector3(0f, 0.02f, 0f), new Vector3(3.10f, 0.03f, 2.15f), fabricWarm);
                    Box(t, "RugInner", new Vector3(0f, 0.032f, 0f), new Vector3(2.45f, 0.02f, 1.55f), cushion);
                    Box(t, "RugCenter", new Vector3(0f, 0.042f, 0f), new Vector3(1.50f, 0.02f, 0.90f), fabricBlue);
                    break;

                case FurnitureKind.PottedIndoorFern:
                    Cyl(t, "Pot", new Vector3(0f, 0.26f, 0f), new Vector3(0.54f, 0.26f, 0.54f), fabricWarm);
                    Cyl(t, "Soil", new Vector3(0f, 0.50f, 0f), new Vector3(0.46f, 0.04f, 0.46f), soil);
                    Box(t, "BushA", new Vector3(0f, 0.82f, 0f), new Vector3(0.68f, 0.48f, 0.68f), leaf);
                    Box(t, "BushB", new Vector3(0f, 1.06f, 0f), new Vector3(0.46f, 0.36f, 0.46f), leaf);
                    break;

                case FurnitureKind.TrashBinBarrel:
                    Cyl(t, "Bin", new Vector3(0f, 0.38f, 0f), new Vector3(0.56f, 0.38f, 0.56f), steel);
                    Cyl(t, "Rim", new Vector3(0f, 0.76f, 0f), new Vector3(0.60f, 0.04f, 0.60f), darkMetal);
                    break;
            }
        }

        private void BuildSquarePlanterVisual(Transform t, Shader shader, Material wood, Material trim, Material soil, Material leaf)
        {
            // Square wooden planter box (1.25 x 1.25)
            Box(t, "PlanterBox", new Vector3(0f, 0.24f, 0f), new Vector3(1.26f, 0.48f, 1.26f), wood);
            Box(t, "RimN", new Vector3(0f, 0.50f, 0.58f), new Vector3(1.32f, 0.08f, 0.12f), trim);
            Box(t, "RimS", new Vector3(0f, 0.50f, -0.58f), new Vector3(1.32f, 0.08f, 0.12f), trim);
            Box(t, "RimE", new Vector3(0.58f, 0.50f, 0f), new Vector3(0.12f, 0.08f, 1.12f), trim);
            Box(t, "RimW", new Vector3(-0.58f, 0.50f, 0f), new Vector3(0.12f, 0.08f, 1.12f), trim);
            Box(t, "SoilBed", new Vector3(0f, 0.46f, 0f), new Vector3(1.06f, 0.06f, 1.06f), soil);

            // Plant Foliage & Fruit Container managed dynamically by CropPlanterFurniture!
            GameObject foliageRoot = new GameObject("CropFoliageRoot");
            foliageRoot.transform.SetParent(t, false);
            foliageRoot.transform.localPosition = new Vector3(0f, 0.50f, 0f);

            // 4 leafy plant clusters inside the square bed
            Vector3[] offsets =
            {
                new Vector3(-0.26f, 0.18f, -0.26f),
                new Vector3(0.26f, 0.18f, -0.26f),
                new Vector3(-0.26f, 0.18f, 0.26f),
                new Vector3(0.26f, 0.18f, 0.26f)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                Box(foliageRoot.transform, $"Leaf_{i}", offsets[i], new Vector3(0.36f, 0.32f, 0.36f), leaf);
            }

            Color fruitColor = furnitureKind switch
            {
                FurnitureKind.PlanterBoxLime => new Color(0.56f, 0.94f, 0.24f),
                FurnitureKind.PlanterBoxPotato => new Color(0.84f, 0.66f, 0.38f),
                FurnitureKind.PlanterBoxTomato => new Color(0.95f, 0.20f, 0.18f),
                FurnitureKind.PlanterBoxCarrot => new Color(0.98f, 0.52f, 0.12f),
                _ => new Color(0.96f, 0.18f, 0.36f) // Strawberry
            };
            Material fruitMat = MakeMat(shader, fruitColor, 0.35f, fruitColor * 0.28f);

            GameObject fruitRoot = new GameObject("CropFruitRoot");
            fruitRoot.transform.SetParent(t, false);
            fruitRoot.transform.localPosition = new Vector3(0f, 0.50f, 0f);

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 fp = offsets[i] + new Vector3(0f, 0.20f, 0f);
                Sphere(fruitRoot.transform, $"Fruit_{i}", fp, new Vector3(0.20f, 0.20f, 0.20f), fruitMat);
            }
        }

        private static Material MakeMat(Shader shader, Color color, float smoothness, Color? emission = null)
        {
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }

        private static void Legs(Transform p, float spanX, float spanZ, float h, float thick, Material mat)
        {
            float hx = spanX * 0.5f;
            float hz = spanZ * 0.5f;
            Box(p, "LegFL", new Vector3(-hx, h * 0.5f, hz), new Vector3(thick, h, thick), mat);
            Box(p, "LegFR", new Vector3(hx, h * 0.5f, hz), new Vector3(thick, h, thick), mat);
            Box(p, "LegBL", new Vector3(-hx, h * 0.5f, -hz), new Vector3(thick, h, thick), mat);
            Box(p, "LegBR", new Vector3(hx, h * 0.5f, -hz), new Vector3(thick, h, thick), mat);
        }

        private static void Box(Transform p, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private static void Cyl(Transform p, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private static void Sphere(Transform p, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }
    }
}
