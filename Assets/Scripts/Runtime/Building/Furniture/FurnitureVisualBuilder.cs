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
    /// Builds rich, stylized multi-part 3D furniture models at runtime for all 30 furniture types.
    /// Convention: Front face of every furniture piece points toward local -Z (directly toward the
    /// top-down camera when placed along the back +Z wall at rotY = 0!).
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

            // Rich stylized PBR material palette
            Material darkWood   = MakeMat(shader, new Color(0.28f, 0.18f, 0.11f), 0.28f);
            Material midWood    = MakeMat(shader, new Color(0.48f, 0.32f, 0.19f), 0.26f);
            Material lightWood  = MakeMat(shader, new Color(0.68f, 0.52f, 0.34f), 0.22f);
            Material steel      = MakeMat(shader, new Color(0.45f, 0.50f, 0.54f), 0.62f);
            Material darkMetal  = MakeMat(shader, new Color(0.18f, 0.20f, 0.23f), 0.52f);
            Material chrome     = MakeMat(shader, new Color(0.84f, 0.88f, 0.90f), 0.82f);
            Material whiteEnamel= MakeMat(shader, new Color(0.90f, 0.93f, 0.92f), 0.50f);
            Material mintRetro  = MakeMat(shader, new Color(0.76f, 0.89f, 0.85f), 0.55f);
            Material brass      = MakeMat(shader, new Color(0.86f, 0.68f, 0.26f), 0.74f);
            Material fabricRed  = MakeMat(shader, new Color(0.66f, 0.24f, 0.22f), 0.10f);
            Material fabricBlue = MakeMat(shader, new Color(0.24f, 0.44f, 0.58f), 0.12f);
            Material fabricGold = MakeMat(shader, new Color(0.78f, 0.62f, 0.26f), 0.12f);
            Material fabricGreen= MakeMat(shader, new Color(0.25f, 0.48f, 0.34f), 0.12f);
            Material leather    = MakeMat(shader, new Color(0.42f, 0.22f, 0.13f), 0.38f);
            Material leatherLit = MakeMat(shader, new Color(0.54f, 0.30f, 0.18f), 0.34f);
            Material cushion    = MakeMat(shader, new Color(0.92f, 0.89f, 0.82f), 0.08f);
            Material redCross   = MakeMat(shader, new Color(0.88f, 0.16f, 0.18f), 0.25f);
            Material glassBlue  = MakeMat(shader, new Color(0.58f, 0.80f, 0.88f), 0.80f);
            Material soil       = MakeMat(shader, new Color(0.22f, 0.14f, 0.09f), 0.05f);
            Material leafDark   = MakeMat(shader, new Color(0.18f, 0.48f, 0.20f), 0.15f);
            Material leafBright = MakeMat(shader, new Color(0.30f, 0.66f, 0.26f), 0.18f);
            Material terracotta = MakeMat(shader, new Color(0.72f, 0.36f, 0.22f), 0.18f);
            Material warmGlow   = MakeMat(shader, new Color(1.0f, 0.88f, 0.56f), 0.2f, new Color(1.0f, 0.76f, 0.32f) * 1.45f);
            Material emberGlow  = MakeMat(shader, new Color(1.0f, 0.42f, 0.12f), 0.2f, new Color(1.0f, 0.38f, 0.08f) * 1.85f);
            Material crtGlow    = MakeMat(shader, new Color(0.42f, 0.74f, 0.92f), 0.6f, new Color(0.22f, 0.50f, 0.72f) * 1.15f);
            Material dialGlow   = MakeMat(shader, new Color(0.36f, 0.96f, 0.58f), 0.4f, new Color(0.20f, 0.85f, 0.45f) * 1.25f);

            Transform t = root.transform;

            switch (furnitureKind)
            {
                case FurnitureKind.WoodenWardrobe:
                    // Feet + plinth
                    Box(t, "Plinth", new Vector3(0f, 0.09f, 0f), new Vector3(1.36f, 0.14f, 0.74f), darkWood);
                    Box(t, "Body", new Vector3(0f, 1.06f, 0.02f), new Vector3(1.34f, 1.82f, 0.70f), darkWood);
                    // Twin recessed paneled doors facing -Z
                    Box(t, "DoorL", new Vector3(-0.32f, 1.08f, -0.34f), new Vector3(0.58f, 1.68f, 0.06f), midWood);
                    Box(t, "DoorR", new Vector3(0.32f, 1.08f, -0.34f), new Vector3(0.58f, 1.68f, 0.06f), midWood);
                    Box(t, "PanelL_Top", new Vector3(-0.32f, 1.44f, -0.37f), new Vector3(0.44f, 0.62f, 0.03f), lightWood);
                    Box(t, "PanelR_Top", new Vector3(0.32f, 1.44f, -0.37f), new Vector3(0.44f, 0.62f, 0.03f), lightWood);
                    Box(t, "PanelL_Bot", new Vector3(-0.32f, 0.68f, -0.37f), new Vector3(0.44f, 0.62f, 0.03f), lightWood);
                    Box(t, "PanelR_Bot", new Vector3(0.32f, 0.68f, -0.37f), new Vector3(0.44f, 0.62f, 0.03f), lightWood);
                    // Brass handles + bottom drawer + carved crown molding
                    Cyl(t, "HandleL", new Vector3(-0.08f, 1.08f, -0.39f), new Vector3(0.05f, 0.14f, 0.05f), brass);
                    Cyl(t, "HandleR", new Vector3(0.08f, 1.08f, -0.39f), new Vector3(0.05f, 0.14f, 0.05f), brass);
                    Box(t, "CrownTrim", new Vector3(0f, 2.00f, 0f), new Vector3(1.48f, 0.11f, 0.84f), darkWood);
                    Box(t, "CrownCap", new Vector3(0f, 2.08f, 0f), new Vector3(1.38f, 0.06f, 0.76f), midWood);
                    break;

                case FurnitureKind.MetalLockerCabinet:
                    Box(t, "Base", new Vector3(0f, 0.07f, 0f), new Vector3(1.34f, 0.14f, 0.72f), darkMetal);
                    Box(t, "Body", new Vector3(0f, 1.08f, 0.02f), new Vector3(1.30f, 1.88f, 0.68f), steel);
                    Box(t, "DoorL", new Vector3(-0.31f, 1.08f, -0.33f), new Vector3(0.56f, 1.76f, 0.05f), darkMetal);
                    Box(t, "DoorR", new Vector3(0.31f, 1.08f, -0.33f), new Vector3(0.56f, 1.76f, 0.05f), darkMetal);
                    // Louvered vent slots (3 per door) + chrome padlock latch + hazard badge
                    for (int v = 0; v < 3; v++)
                    {
                        float vy = 1.62f + v * 0.09f;
                        Box(t, $"VentL_{v}", new Vector3(-0.31f, vy, -0.36f), new Vector3(0.38f, 0.035f, 0.03f), steel);
                        Box(t, $"VentR_{v}", new Vector3(0.31f, vy, -0.36f), new Vector3(0.38f, 0.035f, 0.03f), steel);
                    }
                    Box(t, "LatchL", new Vector3(-0.08f, 1.06f, -0.38f), new Vector3(0.06f, 0.22f, 0.06f), chrome);
                    Box(t, "LatchR", new Vector3(0.08f, 1.06f, -0.38f), new Vector3(0.06f, 0.22f, 0.06f), chrome);
                    Box(t, "Padlock", new Vector3(0f, 1.02f, -0.41f), new Vector3(0.11f, 0.13f, 0.05f), brass);
                    Box(t, "TopLip", new Vector3(0f, 2.04f, 0f), new Vector3(1.38f, 0.07f, 0.76f), darkMetal);
                    Box(t, "StripeBadge", new Vector3(0f, 1.94f, -0.36f), new Vector3(0.86f, 0.06f, 0.02f), fabricGold);
                    break;

                case FurnitureKind.CarpentryCraftTable:
                    // Planked butcher-block tabletop
                    Box(t, "TopBase", new Vector3(0f, 0.82f, 0f), new Vector3(1.92f, 0.12f, 0.94f), midWood);
                    Box(t, "TopTrim", new Vector3(0f, 0.89f, 0f), new Vector3(1.86f, 0.04f, 0.86f), lightWood);
                    Box(t, "LowerShelf", new Vector3(0f, 0.26f, 0f), new Vector3(1.66f, 0.07f, 0.72f), darkWood);
                    Box(t, "StackedWood1", new Vector3(-0.35f, 0.35f, 0f), new Vector3(0.74f, 0.11f, 0.36f), lightWood);
                    Box(t, "StackedWood2", new Vector3(0.40f, 0.34f, 0.04f), new Vector3(0.54f, 0.09f, 0.42f), midWood);
                    Legs(t, 1.72f, 0.76f, 0.80f, 0.11f, darkWood);
                    // Front bench vise + toolbox + mallet on tabletop
                    Box(t, "ViseBody", new Vector3(-0.74f, 0.82f, -0.50f), new Vector3(0.32f, 0.18f, 0.16f), darkMetal);
                    Cyl(t, "ViseScrew", new Vector3(-0.74f, 0.82f, -0.60f), new Vector3(0.06f, 0.16f, 0.06f), chrome, new Vector3(0f, 0f, 90f));
                    Box(t, "ToolBox", new Vector3(0.52f, 1.00f, 0.16f), new Vector3(0.56f, 0.20f, 0.30f), fabricRed);
                    Box(t, "ToolBoxHandle", new Vector3(0.52f, 1.13f, 0.16f), new Vector3(0.28f, 0.05f, 0.05f), chrome);
                    Box(t, "MalletHandle", new Vector3(-0.18f, 0.94f, -0.10f), new Vector3(0.36f, 0.05f, 0.05f), darkWood, new Vector3(0f, 25f, 0f));
                    Box(t, "MalletHead", new Vector3(-0.04f, 0.96f, -0.04f), new Vector3(0.11f, 0.11f, 0.18f), steel, new Vector3(0f, 25f, 0f));
                    break;

                case FurnitureKind.ElectronicsCraftDesk:
                    Box(t, "DeskTop", new Vector3(0f, 0.80f, 0f), new Vector3(1.90f, 0.10f, 0.92f), darkMetal);
                    Box(t, "RubberMat", new Vector3(0.10f, 0.86f, -0.06f), new Vector3(1.10f, 0.02f, 0.62f), fabricGreen);
                    Box(t, "PegBoard", new Vector3(0f, 1.34f, 0.42f), new Vector3(1.86f, 0.98f, 0.08f), steel);
                    Box(t, "PegTopShelf", new Vector3(0f, 1.78f, 0.28f), new Vector3(1.80f, 0.06f, 0.28f), darkMetal);
                    Legs(t, 1.74f, 0.76f, 0.78f, 0.09f, steel);
                    // Glowing oscilloscope monitor + keyboard + soldering box facing -Z
                    Box(t, "MonitorStand", new Vector3(-0.42f, 0.90f, 0.12f), new Vector3(0.22f, 0.12f, 0.18f), darkMetal);
                    Box(t, "MonitorCase", new Vector3(-0.42f, 1.16f, 0.14f), new Vector3(0.64f, 0.44f, 0.24f), darkMetal);
                    Box(t, "MonitorScreen", new Vector3(-0.42f, 1.16f, 0.01f), new Vector3(0.54f, 0.34f, 0.03f), dialGlow);
                    Box(t, "Keyboard", new Vector3(-0.36f, 0.88f, -0.22f), new Vector3(0.54f, 0.04f, 0.20f), chrome);
                    Box(t, "SolderStation", new Vector3(0.54f, 0.96f, 0.10f), new Vector3(0.42f, 0.22f, 0.30f), fabricBlue);
                    Box(t, "WorkLampBar", new Vector3(0f, 1.74f, 0.18f), new Vector3(1.10f, 0.05f, 0.08f), warmGlow);
                    break;

                case FurnitureKind.WoodenDiningChair:
                    Box(t, "SeatFrame", new Vector3(0f, 0.46f, 0f), new Vector3(0.62f, 0.07f, 0.60f), darkWood);
                    Box(t, "SeatCushion", new Vector3(0f, 0.52f, -0.02f), new Vector3(0.54f, 0.06f, 0.52f), fabricRed);
                    // Backrest at +Z so sitter faces -Z (toward camera!)
                    Box(t, "PostL", new Vector3(-0.26f, 0.82f, 0.25f), new Vector3(0.07f, 0.74f, 0.07f), darkWood);
                    Box(t, "PostR", new Vector3(0.26f, 0.82f, 0.25f), new Vector3(0.07f, 0.74f, 0.07f), darkWood);
                    Box(t, "TopRail", new Vector3(0f, 1.14f, 0.25f), new Vector3(0.58f, 0.11f, 0.07f), midWood);
                    Box(t, "Slat1", new Vector3(-0.11f, 0.82f, 0.25f), new Vector3(0.07f, 0.58f, 0.04f), lightWood);
                    Box(t, "Slat2", new Vector3(0.11f, 0.82f, 0.25f), new Vector3(0.07f, 0.58f, 0.04f), lightWood);
                    Legs(t, 0.50f, 0.48f, 0.46f, 0.065f, darkWood);
                    break;

                case FurnitureKind.ComfyArmchair:
                    Legs(t, 0.82f, 0.72f, 0.14f, 0.08f, darkWood);
                    Box(t, "BaseFrame", new Vector3(0f, 0.28f, -0.02f), new Vector3(0.98f, 0.26f, 0.88f), fabricRed);
                    Box(t, "SeatCushion", new Vector3(0f, 0.47f, -0.06f), new Vector3(0.72f, 0.18f, 0.70f), cushion);
                    Box(t, "BackRest", new Vector3(0f, 0.74f, 0.34f), new Vector3(0.96f, 0.72f, 0.24f), fabricRed);
                    Box(t, "BackPillow", new Vector3(0f, 0.76f, 0.20f), new Vector3(0.66f, 0.44f, 0.14f), cushion);
                    Box(t, "ArmL", new Vector3(-0.44f, 0.54f, 0f), new Vector3(0.20f, 0.44f, 0.86f), fabricRed);
                    Box(t, "ArmR", new Vector3(0.44f, 0.54f, 0f), new Vector3(0.20f, 0.44f, 0.86f), fabricRed);
                    Cyl(t, "RollL", new Vector3(-0.44f, 0.76f, 0f), new Vector3(0.20f, 0.43f, 0.20f), leather, new Vector3(90f, 0f, 0f));
                    Cyl(t, "RollR", new Vector3(0.44f, 0.76f, 0f), new Vector3(0.20f, 0.43f, 0.20f), leather, new Vector3(90f, 0f, 0f));
                    break;

                case FurnitureKind.WoodenSingleBed:
                    Legs(t, 1.32f, 1.98f, 0.20f, 0.10f, darkWood);
                    Box(t, "BedFrame", new Vector3(0f, 0.30f, 0f), new Vector3(1.38f, 0.22f, 2.08f), midWood);
                    Box(t, "Mattress", new Vector3(0f, 0.49f, 0f), new Vector3(1.26f, 0.22f, 1.94f), cushion);
                    // Cozy duvet blanket + folded top cuff + two fluffy pillows (headboard at +Z!)
                    Box(t, "Duvet", new Vector3(0f, 0.56f, -0.26f), new Vector3(1.30f, 0.16f, 1.34f), fabricBlue);
                    Box(t, "DuvetFold", new Vector3(0f, 0.62f, 0.32f), new Vector3(1.31f, 0.10f, 0.24f), cushion);
                    Box(t, "DuvetAccent", new Vector3(0f, 0.65f, -0.68f), new Vector3(1.31f, 0.04f, 0.22f), fabricGold);
                    Box(t, "PillowL", new Vector3(-0.32f, 0.66f, 0.68f), new Vector3(0.52f, 0.16f, 0.36f), cushion, new Vector3(-12f, 4f, 0f));
                    Box(t, "PillowR", new Vector3(0.32f, 0.66f, 0.68f), new Vector3(0.52f, 0.16f, 0.36f), cushion, new Vector3(-12f, -4f, 0f));
                    Box(t, "Headboard", new Vector3(0f, 0.72f, 1.00f), new Vector3(1.42f, 1.06f, 0.12f), darkWood);
                    Box(t, "HeadPanel", new Vector3(0f, 0.82f, 0.95f), new Vector3(1.22f, 0.58f, 0.05f), lightWood);
                    Box(t, "Footboard", new Vector3(0f, 0.48f, -1.00f), new Vector3(1.42f, 0.60f, 0.12f), darkWood);
                    break;

                case FurnitureKind.AnalogRadio:
                    // Vintage console table + detailed mid-century analog FM radio facing -Z
                    Box(t, "StandTop", new Vector3(0f, 0.68f, 0f), new Vector3(0.84f, 0.08f, 0.56f), midWood);
                    Box(t, "StandShelf", new Vector3(0f, 0.24f, 0f), new Vector3(0.72f, 0.06f, 0.44f), darkWood);
                    Legs(t, 0.70f, 0.44f, 0.66f, 0.07f, darkWood);
                    Box(t, "RadioCase", new Vector3(0f, 0.94f, 0f), new Vector3(0.68f, 0.40f, 0.36f), midWood);
                    Box(t, "RadioTrim", new Vector3(0f, 0.94f, -0.17f), new Vector3(0.64f, 0.35f, 0.04f), darkWood);
                    // Woven speaker grille with brass slats
                    Box(t, "SpeakerCloth", new Vector3(-0.15f, 0.94f, -0.19f), new Vector3(0.28f, 0.26f, 0.02f), fabricGold);
                    for (int g = -1; g <= 1; g++)
                        Box(t, $"GrillBar_{g}", new Vector3(-0.15f, 0.94f + g * 0.07f, -0.205f), new Vector3(0.28f, 0.018f, 0.015f), darkWood);
                    // Illuminated FM dial + red needle + dual brass knobs + tilted telescoping antenna
                    Box(t, "DialWindow", new Vector3(0.16f, 1.00f, -0.195f), new Vector3(0.24f, 0.11f, 0.02f), dialGlow);
                    Box(t, "DialNeedle", new Vector3(0.18f, 1.00f, -0.21f), new Vector3(0.02f, 0.10f, 0.015f), redCross);
                    Cyl(t, "KnobL", new Vector3(0.10f, 0.84f, -0.20f), new Vector3(0.075f, 0.04f, 0.075f), brass, new Vector3(90f, 0f, 0f));
                    Cyl(t, "KnobR", new Vector3(0.22f, 0.84f, -0.20f), new Vector3(0.075f, 0.04f, 0.075f), brass, new Vector3(90f, 0f, 0f));
                    Cyl(t, "Antenna", new Vector3(0.26f, 1.38f, 0.08f), new Vector3(0.024f, 0.34f, 0.024f), chrome, new Vector3(0f, 0f, -14f));
                    Sphere(t, "AntennaTip", new Vector3(0.34f, 1.70f, 0.08f), new Vector3(0.055f, 0.055f, 0.055f), chrome);
                    break;

                case FurnitureKind.DeskLamp:
                    // Bedside table with drawer + brass desk lamp with warm glowing shade
                    Box(t, "Nightstand", new Vector3(0f, 0.38f, 0f), new Vector3(0.68f, 0.68f, 0.60f), darkWood);
                    Box(t, "TopBoard", new Vector3(0f, 0.74f, 0f), new Vector3(0.74f, 0.06f, 0.66f), midWood);
                    Box(t, "Drawer", new Vector3(0f, 0.52f, -0.30f), new Vector3(0.56f, 0.22f, 0.04f), lightWood);
                    Sphere(t, "DrawerKnob", new Vector3(0f, 0.52f, -0.34f), new Vector3(0.06f, 0.06f, 0.06f), brass);
                    Cyl(t, "LampBase", new Vector3(0f, 0.80f, 0f), new Vector3(0.28f, 0.04f, 0.28f), brass);
                    Cyl(t, "LampStem", new Vector3(0f, 1.00f, 0f), new Vector3(0.06f, 0.20f, 0.06f), brass);
                    Cyl(t, "LampShadeOuter", new Vector3(0f, 1.24f, 0f), new Vector3(0.44f, 0.17f, 0.44f), warmGlow);
                    Cyl(t, "LampTopRim", new Vector3(0f, 1.41f, 0f), new Vector3(0.30f, 0.025f, 0.30f), brass);
                    break;

                case FurnitureKind.CookingPotStove:
                    Box(t, "StoveCabinet", new Vector3(0f, 0.42f, 0f), new Vector3(0.96f, 0.84f, 0.82f), darkMetal);
                    Box(t, "OvenDoor", new Vector3(0f, 0.38f, -0.42f), new Vector3(0.78f, 0.54f, 0.04f), steel);
                    Box(t, "OvenWindow", new Vector3(0f, 0.42f, -0.45f), new Vector3(0.54f, 0.28f, 0.02f), darkMetal);
                    Box(t, "OvenHandle", new Vector3(0f, 0.60f, -0.47f), new Vector3(0.58f, 0.04f, 0.05f), chrome);
                    Box(t, "CookTop", new Vector3(0f, 0.86f, 0f), new Vector3(0.98f, 0.06f, 0.84f), steel);
                    Cyl(t, "BurnerGlow", new Vector3(0f, 0.90f, 0f), new Vector3(0.52f, 0.02f, 0.52f), emberGlow);
                    // Heavy cast-iron cooking pot + brass lid knob + wooden ladle
                    Cyl(t, "PotBody", new Vector3(0f, 1.10f, 0f), new Vector3(0.56f, 0.20f, 0.56f), darkMetal);
                    Cyl(t, "PotRim", new Vector3(0f, 1.29f, 0f), new Vector3(0.60f, 0.03f, 0.60f), steel);
                    Cyl(t, "PotLid", new Vector3(0.06f, 1.33f, 0.04f), new Vector3(0.54f, 0.035f, 0.54f), steel);
                    Sphere(t, "LidKnob", new Vector3(0.06f, 1.39f, 0.04f), new Vector3(0.09f, 0.09f, 0.09f), brass);
                    Box(t, "HandleL", new Vector3(-0.34f, 1.18f, 0f), new Vector3(0.12f, 0.05f, 0.14f), brass);
                    Box(t, "HandleR", new Vector3(0.34f, 1.18f, 0f), new Vector3(0.12f, 0.05f, 0.14f), brass);
                    break;

                case FurnitureKind.PlanterBoxLime:
                case FurnitureKind.PlanterBoxPotato:
                case FurnitureKind.PlanterBoxTomato:
                case FurnitureKind.PlanterBoxCarrot:
                case FurnitureKind.PlanterBoxStrawberry:
                    BuildSquarePlanterVisual(t, shader, midWood, darkWood, darkMetal, soil, leafDark, leafBright);
                    break;

                case FurnitureKind.VintageRefrigerator:
                    // Retro mint-cream fridge facing -Z with upper freezer, chrome handles & silver badge
                    Box(t, "FridgeBase", new Vector3(0f, 0.07f, 0f), new Vector3(0.94f, 0.14f, 0.84f), darkMetal);
                    Box(t, "FridgeBody", new Vector3(0f, 1.00f, 0.02f), new Vector3(0.96f, 1.80f, 0.84f), mintRetro);
                    Box(t, "FreezerDoor", new Vector3(0f, 1.56f, -0.42f), new Vector3(0.92f, 0.62f, 0.06f), whiteEnamel);
                    Box(t, "MainDoor", new Vector3(0f, 0.68f, -0.42f), new Vector3(0.92f, 1.08f, 0.06f), whiteEnamel);
                    Box(t, "HandleFreezer", new Vector3(0.32f, 1.48f, -0.47f), new Vector3(0.06f, 0.30f, 0.06f), chrome);
                    Box(t, "HandleMain", new Vector3(0.32f, 0.94f, -0.47f), new Vector3(0.06f, 0.44f, 0.06f), chrome);
                    Box(t, "BrandBadge", new Vector3(-0.18f, 1.72f, -0.46f), new Vector3(0.28f, 0.06f, 0.02f), chrome);
                    Box(t, "TopCap", new Vector3(0f, 1.92f, 0f), new Vector3(0.92f, 0.06f, 0.80f), mintRetro);
                    break;

                case FurnitureKind.MedicalWallCabinet:
                    // Clean enameled clinic cabinet facing -Z with glass display shelf, medicine bottles & crisp Red Cross!
                    Legs(t, 1.06f, 0.56f, 0.14f, 0.07f, chrome);
                    Box(t, "CabBody", new Vector3(0f, 0.98f, 0.02f), new Vector3(1.18f, 1.74f, 0.62f), whiteEnamel);
                    Box(t, "UpperGlassFrame", new Vector3(0f, 1.38f, -0.30f), new Vector3(1.02f, 0.72f, 0.04f), steel);
                    Box(t, "UpperGlass", new Vector3(0f, 1.38f, -0.325f), new Vector3(0.90f, 0.60f, 0.02f), glassBlue);
                    // Prominent raised Red Cross on lower white door facing -Z
                    Box(t, "LowerDoor", new Vector3(0f, 0.56f, -0.30f), new Vector3(1.06f, 0.78f, 0.04f), whiteEnamel);
                    Box(t, "CrossH", new Vector3(0f, 0.58f, -0.34f), new Vector3(0.42f, 0.12f, 0.04f), redCross);
                    Box(t, "CrossV", new Vector3(0f, 0.58f, -0.34f), new Vector3(0.12f, 0.42f, 0.04f), redCross);
                    Box(t, "TopAccent", new Vector3(0f, 1.87f, 0f), new Vector3(1.22f, 0.06f, 0.66f), redCross);
                    break;

                case FurnitureKind.LeatherSofa:
                    Legs(t, 1.90f, 0.72f, 0.12f, 0.08f, darkWood);
                    Box(t, "SeatBase", new Vector3(0f, 0.28f, -0.02f), new Vector3(2.06f, 0.26f, 0.88f), leather);
                    Box(t, "CushionL", new Vector3(-0.48f, 0.47f, -0.06f), new Vector3(0.86f, 0.18f, 0.72f), leatherLit);
                    Box(t, "CushionR", new Vector3(0.48f, 0.47f, -0.06f), new Vector3(0.86f, 0.18f, 0.72f), leatherLit);
                    Box(t, "BackRest", new Vector3(0f, 0.74f, 0.34f), new Vector3(2.04f, 0.70f, 0.24f), leather);
                    Box(t, "ThrowPillowL", new Vector3(-0.68f, 0.66f, 0.14f), new Vector3(0.34f, 0.30f, 0.14f), fabricGold, new Vector3(-16f, 18f, 8f));
                    Box(t, "ThrowPillowR", new Vector3(0.68f, 0.66f, 0.14f), new Vector3(0.34f, 0.30f, 0.14f), fabricRed, new Vector3(-16f, -18f, -8f));
                    Box(t, "ArmL", new Vector3(-0.96f, 0.52f, 0f), new Vector3(0.20f, 0.46f, 0.86f), leather);
                    Box(t, "ArmR", new Vector3(0.96f, 0.52f, 0f), new Vector3(0.20f, 0.46f, 0.86f), leather);
                    break;

                case FurnitureKind.StandingFloorLamp:
                    Cyl(t, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.48f, 0.05f, 0.48f), darkMetal);
                    Cyl(t, "BaseCollar", new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.06f, 0.22f), brass);
                    Cyl(t, "Pole", new Vector3(0f, 0.94f, 0f), new Vector3(0.07f, 0.84f, 0.07f), brass);
                    Cyl(t, "ShadeBody", new Vector3(0f, 1.84f, 0f), new Vector3(0.58f, 0.24f, 0.58f), warmGlow);
                    Cyl(t, "ShadeTrimTop", new Vector3(0f, 2.08f, 0f), new Vector3(0.44f, 0.025f, 0.44f), brass);
                    Cyl(t, "ShadeTrimBot", new Vector3(0f, 1.60f, 0f), new Vector3(0.60f, 0.025f, 0.60f), brass);
                    break;

                case FurnitureKind.TallBookshelf:
                    // Open-front bookcase facing -Z with packed multi-color book rows!
                    Box(t, "BackPanel", new Vector3(0f, 1.00f, 0.26f), new Vector3(1.50f, 2.00f, 0.08f), darkWood);
                    Box(t, "SideL", new Vector3(-0.72f, 1.00f, 0f), new Vector3(0.10f, 2.00f, 0.58f), darkWood);
                    Box(t, "SideR", new Vector3(0.72f, 1.00f, 0f), new Vector3(0.10f, 2.00f, 0.58f), darkWood);
                    Box(t, "TopCrown", new Vector3(0f, 2.02f, 0f), new Vector3(1.58f, 0.08f, 0.64f), darkWood);
                    Material[] bookMats = { fabricRed, fabricBlue, fabricGold, fabricGreen, cushion, leatherLit };
                    for (int s = 0; s < 4; s++)
                    {
                        float sy = 0.18f + s * 0.46f;
                        Box(t, $"Shelf_{s}", new Vector3(0f, sy, 0f), new Vector3(1.40f, 0.06f, 0.54f), midWood);
                        for (int b = 0; b < 6; b++)
                        {
                            float bx = -0.52f + b * 0.20f;
                            float bh = 0.24f + ((s * 3 + b * 5) % 4) * 0.035f;
                            float tilt = (b == 5 && s % 2 == 0) ? 16f : 0f;
                            Box(t, $"Book_{s}_{b}", new Vector3(bx, sy + 0.03f + bh * 0.5f, -0.02f),
                                new Vector3(0.14f, bh, 0.34f), bookMats[(s + b) % bookMats.Length], new Vector3(0f, 0f, tilt));
                        }
                    }
                    break;

                case FurnitureKind.WoodenDiningTable:
                    Box(t, "TableTop", new Vector3(0f, 0.80f, 0f), new Vector3(1.86f, 0.10f, 1.16f), midWood);
                    Box(t, "TableRunner", new Vector3(0f, 0.86f, 0f), new Vector3(1.88f, 0.02f, 0.38f), fabricRed);
                    Box(t, "Apron", new Vector3(0f, 0.70f, 0f), new Vector3(1.62f, 0.12f, 0.92f), darkWood);
                    Legs(t, 1.62f, 0.92f, 0.76f, 0.10f, darkWood);
                    break;

                case FurnitureKind.LowCoffeeTable:
                    Box(t, "TableTop", new Vector3(0f, 0.44f, 0f), new Vector3(1.32f, 0.08f, 0.84f), lightWood);
                    Box(t, "LowerTray", new Vector3(0f, 0.18f, 0f), new Vector3(1.12f, 0.05f, 0.66f), midWood);
                    Legs(t, 1.12f, 0.66f, 0.42f, 0.075f, darkWood);
                    Box(t, "BookProp", new Vector3(-0.26f, 0.50f, 0.04f), new Vector3(0.32f, 0.05f, 0.24f), fabricBlue, new Vector3(0f, 18f, 0f));
                    Cyl(t, "MugProp", new Vector3(0.30f, 0.54f, -0.08f), new Vector3(0.12f, 0.07f, 0.12f), cushion);
                    break;

                case FurnitureKind.KitchenCounterSink:
                    Box(t, "CounterBody", new Vector3(0f, 0.46f, 0.02f), new Vector3(1.74f, 0.90f, 0.80f), midWood);
                    Box(t, "CabDoorL", new Vector3(-0.44f, 0.46f, -0.39f), new Vector3(0.76f, 0.74f, 0.04f), lightWood);
                    Box(t, "CabDoorR", new Vector3(0.44f, 0.46f, -0.39f), new Vector3(0.76f, 0.74f, 0.04f), lightWood);
                    Box(t, "KnobL", new Vector3(-0.14f, 0.58f, -0.43f), new Vector3(0.05f, 0.14f, 0.04f), brass);
                    Box(t, "KnobR", new Vector3(0.14f, 0.58f, -0.43f), new Vector3(0.05f, 0.14f, 0.04f), brass);
                    Box(t, "CounterTop", new Vector3(0f, 0.94f, 0f), new Vector3(1.80f, 0.08f, 0.88f), cushion);
                    Box(t, "SinkRim", new Vector3(0.30f, 0.99f, 0f), new Vector3(0.62f, 0.03f, 0.50f), chrome);
                    Box(t, "SinkWater", new Vector3(0.30f, 1.00f, 0f), new Vector3(0.50f, 0.02f, 0.38f), glassBlue);
                    Cyl(t, "FaucetPost", new Vector3(0.30f, 1.14f, 0.28f), new Vector3(0.06f, 0.16f, 0.06f), chrome);
                    Box(t, "FaucetSpout", new Vector3(0.30f, 1.26f, 0.18f), new Vector3(0.05f, 0.05f, 0.22f), chrome);
                    break;

                case FurnitureKind.FirstAidStation:
                    Box(t, "StandBase", new Vector3(0f, 0.56f, 0f), new Vector3(0.92f, 1.12f, 0.62f), whiteEnamel);
                    Box(t, "FrontStripe", new Vector3(0f, 0.82f, -0.32f), new Vector3(0.92f, 0.12f, 0.03f), redCross);
                    Box(t, "FrontCrossH", new Vector3(0f, 0.44f, -0.32f), new Vector3(0.34f, 0.10f, 0.03f), redCross);
                    Box(t, "FrontCrossV", new Vector3(0f, 0.44f, -0.32f), new Vector3(0.10f, 0.34f, 0.03f), redCross);
                    Box(t, "KitBox", new Vector3(-0.08f, 1.24f, 0f), new Vector3(0.58f, 0.24f, 0.42f), whiteEnamel);
                    Box(t, "TopCrossH", new Vector3(-0.08f, 1.37f, 0f), new Vector3(0.28f, 0.03f, 0.09f), redCross);
                    Box(t, "TopCrossV", new Vector3(-0.08f, 1.37f, 0f), new Vector3(0.09f, 0.03f, 0.28f), redCross);
                    Cyl(t, "MedBottle", new Vector3(0.32f, 1.22f, -0.06f), new Vector3(0.11f, 0.11f, 0.11f), dialGlow);
                    break;

                case FurnitureKind.WeaponDisplayRack:
                    Box(t, "RackBase", new Vector3(0f, 0.16f, 0f), new Vector3(1.58f, 0.32f, 0.64f), darkMetal);
                    Box(t, "BackBoard", new Vector3(0f, 1.06f, 0.24f), new Vector3(1.52f, 1.66f, 0.10f), darkWood);
                    Box(t, "FrameBorder", new Vector3(0f, 1.88f, 0.22f), new Vector3(1.58f, 0.08f, 0.16f), steel);
                    // Mounted 3D Rifle & Shotgun silhouettes facing -Z
                    Box(t, "RifleStock", new Vector3(-0.34f, 1.44f, 0.14f), new Vector3(0.36f, 0.12f, 0.08f), midWood);
                    Box(t, "RifleBody", new Vector3(0.06f, 1.46f, 0.14f), new Vector3(0.52f, 0.11f, 0.08f), darkMetal);
                    Cyl(t, "RifleBarrel", new Vector3(0.46f, 1.48f, 0.14f), new Vector3(0.045f, 0.26f, 0.045f), steel, new Vector3(0f, 0f, 90f));
                    Box(t, "ShotgunStock", new Vector3(-0.32f, 0.96f, 0.14f), new Vector3(0.34f, 0.11f, 0.08f), darkWood);
                    Box(t, "ShotgunBody", new Vector3(0.16f, 0.98f, 0.14f), new Vector3(0.68f, 0.09f, 0.08f), steel);
                    break;

                case FurnitureKind.WoodBurningStove:
                    Box(t, "HearthPad", new Vector3(0f, 0.04f, 0f), new Vector3(1.02f, 0.08f, 0.96f), steel);
                    Cyl(t, "StoveCore", new Vector3(0f, 0.62f, 0f), new Vector3(0.84f, 0.54f, 0.84f), darkMetal);
                    Cyl(t, "StoveTop", new Vector3(0f, 1.18f, 0f), new Vector3(0.90f, 0.05f, 0.90f), steel);
                    Cyl(t, "Chimney", new Vector3(0f, 1.66f, 0.12f), new Vector3(0.24f, 0.52f, 0.24f), darkMetal);
                    // Glowing fire grate facing -Z
                    Box(t, "FireDoorFrame", new Vector3(0f, 0.58f, -0.40f), new Vector3(0.46f, 0.38f, 0.08f), steel);
                    Box(t, "FireEmbers", new Vector3(0f, 0.58f, -0.43f), new Vector3(0.36f, 0.28f, 0.04f), emberGlow);
                    Box(t, "GrateBar1", new Vector3(-0.08f, 0.58f, -0.455f), new Vector3(0.025f, 0.28f, 0.02f), darkMetal);
                    Box(t, "GrateBar2", new Vector3(0.08f, 0.58f, -0.455f), new Vector3(0.025f, 0.28f, 0.02f), darkMetal);
                    break;

                case FurnitureKind.WallMountedTV:
                    // Console cabinet + Retro CRT Television with glowing screen & rabbit-ear antennas facing -Z
                    Box(t, "TVStand", new Vector3(0f, 0.34f, 0f), new Vector3(1.26f, 0.68f, 0.62f), darkWood);
                    Box(t, "StandShelf", new Vector3(0f, 0.32f, -0.30f), new Vector3(1.12f, 0.04f, 0.04f), midWood);
                    Box(t, "TVBody", new Vector3(0f, 1.04f, 0.02f), new Vector3(0.96f, 0.72f, 0.58f), darkMetal);
                    Box(t, "TVBezel", new Vector3(-0.08f, 1.04f, -0.28f), new Vector3(0.72f, 0.58f, 0.04f), steel);
                    Box(t, "TVScreen", new Vector3(-0.08f, 1.04f, -0.305f), new Vector3(0.64f, 0.50f, 0.02f), crtGlow);
                    Cyl(t, "Dial1", new Vector3(0.36f, 1.18f, -0.28f), new Vector3(0.08f, 0.04f, 0.08f), brass, new Vector3(90f, 0f, 0f));
                    Cyl(t, "Dial2", new Vector3(0.36f, 1.04f, -0.28f), new Vector3(0.08f, 0.04f, 0.08f), brass, new Vector3(90f, 0f, 0f));
                    Cyl(t, "AntL", new Vector3(-0.12f, 1.56f, 0.08f), new Vector3(0.025f, 0.26f, 0.025f), chrome, new Vector3(0f, 0f, 28f));
                    Cyl(t, "AntR", new Vector3(0.12f, 1.56f, 0.08f), new Vector3(0.025f, 0.26f, 0.025f), chrome, new Vector3(0f, 0f, -28f));
                    break;

                case FurnitureKind.WovenAreaRug:
                    Box(t, "RugOuter", new Vector3(0f, 0.018f, 0f), new Vector3(3.10f, 0.025f, 2.15f), fabricRed);
                    Box(t, "RugBorder", new Vector3(0f, 0.028f, 0f), new Vector3(2.68f, 0.018f, 1.76f), fabricGold);
                    Box(t, "RugInner", new Vector3(0f, 0.036f, 0f), new Vector3(2.36f, 0.016f, 1.46f), cushion);
                    Box(t, "RugCenter", new Vector3(0f, 0.044f, 0f), new Vector3(1.42f, 0.014f, 0.86f), fabricBlue);
                    break;

                case FurnitureKind.PottedIndoorFern:
                    Cyl(t, "PotBody", new Vector3(0f, 0.24f, 0f), new Vector3(0.50f, 0.24f, 0.50f), terracotta);
                    Cyl(t, "PotRim", new Vector3(0f, 0.48f, 0f), new Vector3(0.58f, 0.05f, 0.58f), terracotta);
                    Cyl(t, "Soil", new Vector3(0f, 0.49f, 0f), new Vector3(0.46f, 0.03f, 0.46f), soil);
                    // Fanning fern fronds at 6 directions
                    for (int f = 0; f < 6; f++)
                    {
                        float ang = f * 60f;
                        Box(t, $"Frond_{f}", new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.22f, 0.78f, Mathf.Cos(ang * Mathf.Deg2Rad) * 0.22f),
                            new Vector3(0.22f, 0.54f, 0.14f), f % 2 == 0 ? leafBright : leafDark, new Vector3(28f, ang, 0f));
                    }
                    Box(t, "CrownFern", new Vector3(0f, 0.96f, 0f), new Vector3(0.32f, 0.34f, 0.32f), leafBright, new Vector3(0f, 45f, 0f));
                    break;

                case FurnitureKind.TrashBinBarrel:
                    Cyl(t, "BinBody", new Vector3(0f, 0.36f, 0f), new Vector3(0.54f, 0.36f, 0.54f), steel);
                    Cyl(t, "BinBand1", new Vector3(0f, 0.24f, 0f), new Vector3(0.57f, 0.03f, 0.57f), darkMetal);
                    Cyl(t, "BinBand2", new Vector3(0f, 0.52f, 0f), new Vector3(0.57f, 0.03f, 0.57f), darkMetal);
                    Cyl(t, "BinRim", new Vector3(0f, 0.74f, 0f), new Vector3(0.59f, 0.035f, 0.59f), darkMetal);
                    Cyl(t, "BinInner", new Vector3(0f, 0.73f, 0f), new Vector3(0.48f, 0.02f, 0.48f), soil);
                    break;
            }
        }

        private void BuildSquarePlanterVisual(Transform t, Shader shader, Material wood, Material trim, Material metal, Material soil, Material leafDark, Material leafBright)
        {
            // Corner-bracketed timber raised planter bed (1.26 x 1.26)
            Box(t, "PlanterBox", new Vector3(0f, 0.24f, 0f), new Vector3(1.24f, 0.46f, 1.24f), wood);
            Box(t, "RimN", new Vector3(0f, 0.49f, 0.57f), new Vector3(1.32f, 0.08f, 0.12f), trim);
            Box(t, "RimS", new Vector3(0f, 0.49f, -0.57f), new Vector3(1.32f, 0.08f, 0.12f), trim);
            Box(t, "RimE", new Vector3(0.57f, 0.49f, 0f), new Vector3(0.12f, 0.08f, 1.10f), trim);
            Box(t, "RimW", new Vector3(-0.57f, 0.49f, 0f), new Vector3(0.12f, 0.08f, 1.10f), trim);
            // 4 steel corner posts
            float c = 0.60f;
            Box(t, "CornerFL", new Vector3(-c, 0.24f, -c), new Vector3(0.10f, 0.50f, 0.10f), metal);
            Box(t, "CornerFR", new Vector3(c, 0.24f, -c), new Vector3(0.10f, 0.50f, 0.10f), metal);
            Box(t, "CornerBL", new Vector3(-c, 0.24f, c), new Vector3(0.10f, 0.50f, 0.10f), metal);
            Box(t, "CornerBR", new Vector3(c, 0.24f, c), new Vector3(0.10f, 0.50f, 0.10f), metal);
            Box(t, "SoilBed", new Vector3(0f, 0.46f, 0f), new Vector3(1.06f, 0.06f, 1.06f), soil);

            GameObject foliageRoot = new GameObject("CropFoliageRoot");
            foliageRoot.transform.SetParent(t, false);
            foliageRoot.transform.localPosition = new Vector3(0f, 0.50f, 0f);

            Vector3[] offsets =
            {
                new Vector3(-0.27f, 0.16f, -0.27f),
                new Vector3(0.27f, 0.16f, -0.27f),
                new Vector3(-0.27f, 0.16f, 0.27f),
                new Vector3(0.27f, 0.16f, 0.27f)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                Box(foliageRoot.transform, $"LeafBase_{i}", offsets[i], new Vector3(0.38f, 0.24f, 0.38f), leafDark, new Vector3(0f, i * 22f, 0f));
                Box(foliageRoot.transform, $"LeafTop_{i}", offsets[i] + new Vector3(0f, 0.14f, 0f), new Vector3(0.28f, 0.20f, 0.28f), leafBright, new Vector3(0f, 45f + i * 18f, 0f));
            }

            Color fruitColor = furnitureKind switch
            {
                FurnitureKind.PlanterBoxLime => new Color(0.54f, 0.94f, 0.22f),
                FurnitureKind.PlanterBoxPotato => new Color(0.84f, 0.65f, 0.36f),
                FurnitureKind.PlanterBoxTomato => new Color(0.96f, 0.18f, 0.16f),
                FurnitureKind.PlanterBoxCarrot => new Color(0.98f, 0.50f, 0.10f),
                _ => new Color(0.96f, 0.16f, 0.36f) // Strawberry
            };
            Material fruitMat = MakeMat(shader, fruitColor, 0.38f, fruitColor * 0.28f);

            GameObject fruitRoot = new GameObject("CropFruitRoot");
            fruitRoot.transform.SetParent(t, false);
            fruitRoot.transform.localPosition = new Vector3(0f, 0.50f, 0f);

            Vector3 fruitScale = furnitureKind switch
            {
                FurnitureKind.PlanterBoxLime => new Vector3(0.18f, 0.22f, 0.18f),
                FurnitureKind.PlanterBoxPotato => new Vector3(0.24f, 0.16f, 0.19f),
                FurnitureKind.PlanterBoxCarrot => new Vector3(0.14f, 0.26f, 0.14f),
                FurnitureKind.PlanterBoxStrawberry => new Vector3(0.16f, 0.20f, 0.16f),
                _ => new Vector3(0.21f, 0.20f, 0.21f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 fp = offsets[i] + new Vector3(0f, 0.22f, 0f);
                Sphere(fruitRoot.transform, $"FruitA_{i}", fp, fruitScale, fruitMat);
                Sphere(fruitRoot.transform, $"FruitB_{i}", fp + new Vector3(0.10f, -0.05f, -0.08f), fruitScale * 0.78f, fruitMat);
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

        private static void Box(Transform p, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private static void Cyl(Transform p, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
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
