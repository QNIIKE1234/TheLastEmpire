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
    /// Builds stylized multi-part 3D furniture models matching the official 30-piece icon set.
    /// Runs in both Edit Mode (live preview in Scene/Prefab view) and Play Mode, and supports baking
    /// persistent VisualRoot meshes into prefabs for Project window 3D thumbnails.
    /// Convention: Front face of every furniture piece points toward local -Z (directly toward the
    /// top-down camera when placed along the back +Z wall at rotY = 0!).
    /// </summary>
    [ExecuteAlways]
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
            if (Application.isPlaying)
            {
                BuildIfNeeded();
            }
        }

        private void OnEnable()
        {
            BuildIfNeeded();
        }

        private void OnDisable()
        {
            // Clean up temporary non-saved Edit Mode preview when closing Prefab Mode / disabling
            if (!Application.isPlaying)
            {
                Transform existing = transform.Find("VisualRoot");
                if (existing != null && (existing.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                {
                    DestroyImmediate(existing.gameObject);
                    _built = false;
                }
            }
        }

        public void BuildIfNeeded()
        {
            Transform existing = transform.Find("VisualRoot");
            if (existing != null)
            {
                _built = true;
                return;
            }
            if (_built && Application.isPlaying) return;

            bool isEditorPreview = !Application.isPlaying;
            BuildVisualInternal(isEditorPreview, null);
        }

        /// <summary>
        /// Used by Editor bake tool to permanently save VisualRoot meshes + .mat assets into furniture prefabs.
        /// </summary>
        public void BuildPersistentForBake(System.Func<string, Color, float, Color?, Material> materialResolver)
        {
            Transform existing = transform.Find("VisualRoot");
            while (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
                existing = transform.Find("VisualRoot");
            }
            _built = false;
            BuildVisualInternal(false, materialResolver);
        }

        private void BuildVisualInternal(bool markAsEditorPreview, System.Func<string, Color, float, Color?, Material> materialResolver)
        {
            _built = true;

            GameObject root = new GameObject("VisualRoot");
            root.transform.SetParent(transform, false);
            if (markAsEditorPreview)
            {
                root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material GetMat(string key, Color color, float smoothness, Color? emission = null)
            {
                if (materialResolver != null)
                {
                    return materialResolver(key, color, smoothness, emission);
                }
                return MakeMat(shader, color, smoothness, emission);
            }

            // Stylized vibrant palette matching the official 30-icon sheet
            Material honeyWood    = GetMat("HoneyWood",    new Color(0.82f, 0.56f, 0.22f), 0.28f);
            Material warmOak      = GetMat("WarmOak",      new Color(0.68f, 0.43f, 0.16f), 0.26f);
            Material darkWood     = GetMat("DarkWood",     new Color(0.36f, 0.21f, 0.10f), 0.24f);
            Material caramelWood  = GetMat("CaramelWood",  new Color(0.56f, 0.31f, 0.13f), 0.30f);

            Material lockerSteel  = GetMat("LockerSteel",  new Color(0.60f, 0.65f, 0.70f), 0.58f);
            Material silverMetal  = GetMat("SilverMetal",  new Color(0.76f, 0.80f, 0.84f), 0.72f);
            Material darkIron     = GetMat("DarkIron",     new Color(0.22f, 0.24f, 0.27f), 0.50f);
            Material chrome       = GetMat("Chrome",       new Color(0.88f, 0.91f, 0.94f), 0.84f);
            Material oliveMetal   = GetMat("OliveMetal",   new Color(0.47f, 0.50f, 0.41f), 0.46f);

            Material goldYellow   = GetMat("GoldYellow",   new Color(0.98f, 0.78f, 0.14f), 0.65f);
            Material lampYellow   = GetMat("LampYellow",   new Color(1.00f, 0.84f, 0.22f), 0.35f, new Color(0.95f, 0.72f, 0.12f) * 0.55f);
            Material bulbGlow     = GetMat("BulbGlow",     new Color(1.00f, 0.96f, 0.74f), 0.20f, new Color(1.00f, 0.88f, 0.40f) * 1.90f);

            Material plushRed     = GetMat("PlushRed",     new Color(0.86f, 0.16f, 0.16f), 0.20f);
            Material deepRed      = GetMat("DeepRed",      new Color(0.68f, 0.11f, 0.12f), 0.18f);
            Material redCross     = GetMat("RedCross",     new Color(0.92f, 0.14f, 0.15f), 0.28f);

            Material skyBlue      = GetMat("SkyBlue",      new Color(0.24f, 0.66f, 0.98f), 0.22f);
            Material deepBlue     = GetMat("DeepBlue",     new Color(0.16f, 0.44f, 0.82f), 0.22f);
            Material tvBlueGlow   = GetMat("TvBlueGlow",   new Color(0.20f, 0.64f, 0.98f), 0.72f, new Color(0.16f, 0.56f, 0.94f) * 1.15f);
            Material gasBlueFlame = GetMat("GasBlueFlame", new Color(0.26f, 0.78f, 1.00f), 0.30f, new Color(0.16f, 0.66f, 1.00f) * 2.10f);
            Material fireGlow     = GetMat("FireGlow",     new Color(1.00f, 0.54f, 0.12f), 0.20f, new Color(1.00f, 0.42f, 0.08f) * 2.10f);
            Material fireCore     = GetMat("FireCore",     new Color(1.00f, 0.88f, 0.24f), 0.20f, new Color(1.00f, 0.82f, 0.18f) * 1.90f);

            Material mintFridge   = GetMat("MintFridge",   new Color(0.62f, 0.90f, 0.76f), 0.64f);
            Material warmWhite    = GetMat("WarmWhite",    new Color(0.95f, 0.94f, 0.90f), 0.42f);
            Material crispWhite   = GetMat("CrispWhite",   new Color(0.97f, 0.98f, 0.99f), 0.35f);
            Material bandageTan   = GetMat("BandageTan",   new Color(0.90f, 0.72f, 0.44f), 0.16f);

            Material leatherBrown = GetMat("LeatherBrown", new Color(0.62f, 0.34f, 0.16f), 0.40f);
            Material leatherDark  = GetMat("LeatherDark",  new Color(0.42f, 0.22f, 0.10f), 0.36f);

            Material pcbGreen     = GetMat("PcbGreen",     new Color(0.22f, 0.76f, 0.36f), 0.42f);
            Material ledGreen     = GetMat("LedGreen",     new Color(0.32f, 0.98f, 0.44f), 0.30f, new Color(0.20f, 0.90f, 0.36f) * 1.40f);
            Material bookPurple   = GetMat("BookPurple",   new Color(0.56f, 0.26f, 0.72f), 0.20f);
            Material bookGreen    = GetMat("BookGreen",    new Color(0.24f, 0.68f, 0.32f), 0.20f);

            Material soil         = GetMat("Soil",         new Color(0.28f, 0.17f, 0.10f), 0.06f);
            Material leafDark     = GetMat("LeafDark",     new Color(0.18f, 0.56f, 0.20f), 0.18f);
            Material leafBright   = GetMat("LeafBright",   new Color(0.44f, 0.80f, 0.22f), 0.22f);
            Material terracotta   = GetMat("Terracotta",   new Color(0.82f, 0.38f, 0.18f), 0.22f);

            Transform t = root.transform;

            switch (furnitureKind)
            {
                // 1. Wooden Wardrobe — warm golden-oak, arched stepped cornice, twin doors + 2 yellow knobs
                case FurnitureKind.WoodenWardrobe:
                    // Short rounded feet + base plinth
                    Legs(t, 1.18f, 0.60f, 0.12f, 0.12f, warmOak);
                    Box(t, "BasePlinth", new Vector3(0f, 0.14f, 0f), new Vector3(1.36f, 0.12f, 0.74f), honeyWood);
                    Box(t, "MainBody", new Vector3(0f, 1.02f, 0.02f), new Vector3(1.30f, 1.66f, 0.68f), warmOak);
                    // Twin wooden doors with inset frames facing -Z
                    Box(t, "DoorL", new Vector3(-0.31f, 1.02f, -0.34f), new Vector3(0.57f, 1.54f, 0.06f), honeyWood);
                    Box(t, "DoorR", new Vector3(0.31f, 1.02f, -0.34f), new Vector3(0.57f, 1.54f, 0.06f), honeyWood);
                    Box(t, "DoorPanelL", new Vector3(-0.31f, 1.02f, -0.37f), new Vector3(0.42f, 1.32f, 0.025f), warmOak);
                    Box(t, "DoorPanelR", new Vector3(0.31f, 1.02f, -0.37f), new Vector3(0.42f, 1.32f, 0.025f), warmOak);
                    // Center seam + two round yellow-gold knobs
                    Box(t, "CenterSeam", new Vector3(0f, 1.02f, -0.36f), new Vector3(0.03f, 1.54f, 0.04f), darkWood);
                    Sphere(t, "KnobL", new Vector3(-0.09f, 0.98f, -0.41f), new Vector3(0.11f, 0.11f, 0.09f), goldYellow);
                    Sphere(t, "KnobR", new Vector3(0.09f, 0.98f, -0.41f), new Vector3(0.11f, 0.11f, 0.09f), goldYellow);
                    // Signature arched stepped crown top
                    Box(t, "CrownBase", new Vector3(0f, 1.89f, 0f), new Vector3(1.40f, 0.10f, 0.78f), honeyWood);
                    Box(t, "CrownArchMid", new Vector3(0f, 1.98f, 0f), new Vector3(1.06f, 0.10f, 0.74f), honeyWood);
                    Box(t, "CrownArchTop", new Vector3(0f, 2.05f, 0f), new Vector3(0.68f, 0.08f, 0.70f), honeyWood);
                    break;

                // 2. Metal Locker Cabinet — cool silver-grey locker, 3 top vent slits, right hinges, yellow padlock on left
                case FurnitureKind.MetalLockerCabinet:
                    Legs(t, 0.86f, 0.56f, 0.10f, 0.10f, darkIron);
                    Box(t, "LockerBody", new Vector3(0f, 1.04f, 0.02f), new Vector3(1.02f, 1.84f, 0.70f), lockerSteel);
                    Box(t, "LockerTopCap", new Vector3(0f, 1.98f, 0.02f), new Vector3(0.96f, 0.08f, 0.66f), silverMetal);
                    Box(t, "LockerDoor", new Vector3(0f, 1.04f, -0.34f), new Vector3(0.88f, 1.70f, 0.05f), silverMetal);
                    // 3 crisp horizontal air vent slits near top
                    for (int v = 0; v < 3; v++)
                    {
                        Box(t, $"Vent_{v}", new Vector3(0.02f, 1.68f - v * 0.12f, -0.37f), new Vector3(0.46f, 0.045f, 0.025f), darkIron);
                    }
                    // Right-edge hinges
                    Box(t, "HingeTop", new Vector3(0.46f, 1.54f, -0.36f), new Vector3(0.08f, 0.16f, 0.06f), darkIron);
                    Box(t, "HingeBot", new Vector3(0.46f, 0.56f, -0.36f), new Vector3(0.08f, 0.16f, 0.06f), darkIron);
                    // Prominent yellow padlock + silver U-shackle on left latch
                    Box(t, "HaspPlate", new Vector3(-0.30f, 1.12f, -0.38f), new Vector3(0.10f, 0.18f, 0.05f), darkIron);
                    Cyl(t, "ShackleL", new Vector3(-0.35f, 1.04f, -0.43f), new Vector3(0.04f, 0.09f, 0.04f), chrome);
                    Cyl(t, "ShackleR", new Vector3(-0.25f, 1.04f, -0.43f), new Vector3(0.04f, 0.09f, 0.04f), chrome);
                    Box(t, "ShackleTop", new Vector3(-0.30f, 1.12f, -0.43f), new Vector3(0.14f, 0.04f, 0.04f), chrome);
                    Box(t, "PadlockBody", new Vector3(-0.30f, 0.89f, -0.43f), new Vector3(0.24f, 0.24f, 0.10f), goldYellow);
                    Box(t, "PadlockKeyhole", new Vector3(-0.30f, 0.88f, -0.49f), new Vector3(0.04f, 0.09f, 0.02f), darkIron);
                    break;

                // 3. Carpentry Craft Table — thick honey-oak workbench with lower beam, yellow claw hammer & hand saw
                case FurnitureKind.CarpentryCraftTable:
                    Legs(t, 1.34f, 0.72f, 0.84f, 0.12f, warmOak);
                    Box(t, "LowerShelf", new Vector3(0f, 0.26f, 0f), new Vector3(1.30f, 0.08f, 0.56f), warmOak);
                    Box(t, "TableApron", new Vector3(0f, 0.76f, 0f), new Vector3(1.36f, 0.12f, 0.74f), warmOak);
                    Box(t, "BenchTop", new Vector3(0f, 0.89f, 0f), new Vector3(1.56f, 0.14f, 0.90f), honeyWood);
                    // Yellow-handled claw hammer on left
                    Box(t, "HammerHandle", new Vector3(-0.36f, 1.00f, -0.06f), new Vector3(0.46f, 0.06f, 0.07f), goldYellow, new Vector3(0f, -24f, 0f));
                    Box(t, "HammerHead", new Vector3(-0.16f, 1.03f, 0.04f), new Vector3(0.11f, 0.10f, 0.28f), silverMetal, new Vector3(0f, -24f, 0f));
                    Box(t, "HammerClaw", new Vector3(-0.12f, 1.03f, -0.08f), new Vector3(0.08f, 0.08f, 0.10f), darkIron, new Vector3(0f, -24f, 0f));
                    // Silver hand saw with wooden handle angled across right
                    Box(t, "SawBlade", new Vector3(0.24f, 0.99f, -0.08f), new Vector3(0.64f, 0.03f, 0.18f), silverMetal, new Vector3(0f, 22f, 0f));
                    Box(t, "SawTeeth", new Vector3(0.26f, 0.985f, -0.17f), new Vector3(0.62f, 0.025f, 0.03f), darkIron, new Vector3(0f, 22f, 0f));
                    Box(t, "SawHandle", new Vector3(0.58f, 1.02f, 0.06f), new Vector3(0.20f, 0.08f, 0.20f), warmOak, new Vector3(0f, 22f, 0f));
                    Box(t, "SawHandleHole", new Vector3(0.59f, 1.025f, 0.06f), new Vector3(0.09f, 0.09f, 0.09f), darkWood, new Vector3(0f, 22f, 0f));
                    break;

                // 4. Electronics Craft Desk — wood desk with 2 right drawers, green PCB mat, blue CRT monitor, cables, soldering iron
                case FurnitureKind.ElectronicsCraftDesk:
                    // Left legs + right 2-drawer cabinet pedestal
                    Box(t, "LegFL", new Vector3(-0.68f, 0.42f, -0.34f), new Vector3(0.10f, 0.84f, 0.10f), warmOak);
                    Box(t, "LegBL", new Vector3(-0.68f, 0.42f, 0.34f), new Vector3(0.10f, 0.84f, 0.10f), warmOak);
                    Box(t, "DrawerUnit", new Vector3(0.46f, 0.42f, 0f), new Vector3(0.52f, 0.82f, 0.78f), warmOak);
                    Box(t, "DrawerTop", new Vector3(0.46f, 0.62f, -0.40f), new Vector3(0.44f, 0.26f, 0.04f), honeyWood);
                    Box(t, "DrawerBot", new Vector3(0.46f, 0.26f, -0.40f), new Vector3(0.44f, 0.26f, 0.04f), honeyWood);
                    Box(t, "PullTop", new Vector3(0.46f, 0.62f, -0.43f), new Vector3(0.16f, 0.04f, 0.03f), darkWood);
                    Box(t, "PullBot", new Vector3(0.46f, 0.26f, -0.43f), new Vector3(0.16f, 0.04f, 0.03f), darkWood);
                    Box(t, "DeskTop", new Vector3(0f, 0.87f, 0f), new Vector3(1.62f, 0.10f, 0.88f), honeyWood);
                    // Bright green PCB work mat
                    Box(t, "PcbMat", new Vector3(-0.08f, 0.93f, -0.10f), new Vector3(0.68f, 0.025f, 0.42f), pcbGreen);
                    Box(t, "PcbChip", new Vector3(-0.08f, 0.95f, -0.10f), new Vector3(0.14f, 0.03f, 0.14f), darkIron);
                    // Chunky grey retro monitor with bright blue screen
                    Box(t, "MonitorStand", new Vector3(-0.24f, 0.97f, 0.18f), new Vector3(0.24f, 0.10f, 0.20f), darkIron);
                    Box(t, "MonitorCase", new Vector3(-0.24f, 1.24f, 0.20f), new Vector3(0.58f, 0.46f, 0.24f), lockerSteel);
                    Box(t, "MonitorScreen", new Vector3(-0.24f, 1.25f, 0.07f), new Vector3(0.48f, 0.36f, 0.03f), tvBlueGlow);
                    // Red & dark coiled cables hanging on left edge
                    Cyl(t, "CableRed1", new Vector3(-0.56f, 0.74f, -0.34f), new Vector3(0.06f, 0.18f, 0.06f), plushRed, new Vector3(14f, 0f, 18f));
                    Cyl(t, "CableDark", new Vector3(-0.48f, 0.78f, -0.36f), new Vector3(0.05f, 0.15f, 0.05f), darkIron, new Vector3(10f, 0f, -15f));
                    // Mini grey oscilloscope box with green LEDs
                    Box(t, "ScopeBox", new Vector3(0.24f, 1.04f, 0.18f), new Vector3(0.24f, 0.24f, 0.22f), silverMetal);
                    Sphere(t, "Led1", new Vector3(0.19f, 1.09f, 0.06f), new Vector3(0.05f, 0.05f, 0.04f), ledGreen);
                    Sphere(t, "Led2", new Vector3(0.28f, 1.09f, 0.06f), new Vector3(0.05f, 0.05f, 0.04f), ledGreen);
                    // Yellow soldering iron in silver stand on right
                    Box(t, "SolderBase", new Vector3(0.54f, 0.95f, -0.02f), new Vector3(0.18f, 0.06f, 0.18f), silverMetal);
                    Cyl(t, "SolderHandle", new Vector3(0.58f, 1.12f, -0.02f), new Vector3(0.06f, 0.16f, 0.06f), goldYellow, new Vector3(0f, 0f, -24f));
                    Cyl(t, "SolderTip", new Vector3(0.51f, 0.99f, -0.02f), new Vector3(0.025f, 0.09f, 0.025f), chrome, new Vector3(0f, 0f, -24f));
                    break;

                // 5. Wooden Dining Chair — bright golden-oak, sturdy legs, twin horizontal back slats
                case FurnitureKind.WoodenDiningChair:
                    Legs(t, 0.54f, 0.52f, 0.48f, 0.08f, warmOak);
                    Box(t, "Seat", new Vector3(0f, 0.51f, 0f), new Vector3(0.68f, 0.09f, 0.64f), honeyWood);
                    // Twin tall rear posts + 2 wide horizontal back slats (+Z back, sits facing -Z)
                    Box(t, "PostL", new Vector3(-0.27f, 0.88f, 0.26f), new Vector3(0.085f, 0.74f, 0.085f), warmOak);
                    Box(t, "PostR", new Vector3(0.27f, 0.88f, 0.26f), new Vector3(0.085f, 0.74f, 0.085f), warmOak);
                    Box(t, "BackSlatTop", new Vector3(0f, 1.12f, 0.26f), new Vector3(0.54f, 0.18f, 0.06f), honeyWood);
                    Box(t, "BackSlatMid", new Vector3(0f, 0.84f, 0.26f), new Vector3(0.54f, 0.16f, 0.06f), honeyWood);
                    break;

                // 6. Comfy Armchair — ALL vibrant crimson-red tufted armchair with rolled armrests & arched backrest
                case FurnitureKind.ComfyArmchair:
                    Legs(t, 0.84f, 0.74f, 0.12f, 0.09f, darkWood);
                    Box(t, "BasePlinth", new Vector3(0f, 0.24f, -0.02f), new Vector3(1.02f, 0.24f, 0.92f), deepRed);
                    Box(t, "SeatCushion", new Vector3(0f, 0.44f, -0.08f), new Vector3(0.74f, 0.20f, 0.72f), plushRed);
                    // Rounded high crimson backrest with 2 dark-red diamond tufts
                    Box(t, "BackRestMain", new Vector3(0f, 0.78f, 0.32f), new Vector3(0.92f, 0.72f, 0.26f), plushRed);
                    Box(t, "BackRestCrown", new Vector3(0f, 1.16f, 0.32f), new Vector3(0.72f, 0.14f, 0.24f), plushRed);
                    Sphere(t, "TuftL", new Vector3(-0.18f, 0.86f, 0.18f), new Vector3(0.08f, 0.08f, 0.05f), deepRed);
                    Sphere(t, "TuftR", new Vector3(0.18f, 0.86f, 0.18f), new Vector3(0.08f, 0.08f, 0.05f), deepRed);
                    // Thick rolled crimson armrests
                    Box(t, "ArmL", new Vector3(-0.46f, 0.50f, -0.02f), new Vector3(0.24f, 0.42f, 0.88f), plushRed);
                    Box(t, "ArmR", new Vector3(0.46f, 0.50f, -0.02f), new Vector3(0.24f, 0.42f, 0.88f), plushRed);
                    Cyl(t, "ArmRollL", new Vector3(-0.48f, 0.71f, -0.02f), new Vector3(0.26f, 0.44f, 0.26f), plushRed, new Vector3(90f, 0f, 0f));
                    Cyl(t, "ArmRollR", new Vector3(0.48f, 0.71f, -0.02f), new Vector3(0.26f, 0.44f, 0.26f), plushRed, new Vector3(90f, 0f, 0f));
                    break;

                // 7. Wooden Single Bed — honey-oak 4-post bed, white mattress & pillow, vibrant sky-blue cuffed blanket
                case FurnitureKind.WoodenSingleBed:
                    // 4 chunky corner posts with rounded finials
                    Box(t, "PostBL", new Vector3(-0.68f, 0.54f, 0.98f), new Vector3(0.13f, 1.08f, 0.13f), warmOak);
                    Box(t, "PostBR", new Vector3(0.68f, 0.54f, 0.98f), new Vector3(0.13f, 1.08f, 0.13f), warmOak);
                    Sphere(t, "FinialBL", new Vector3(-0.68f, 1.11f, 0.98f), new Vector3(0.15f, 0.13f, 0.15f), honeyWood);
                    Sphere(t, "FinialBR", new Vector3(0.68f, 1.11f, 0.98f), new Vector3(0.15f, 0.13f, 0.15f), honeyWood);
                    Box(t, "PostFL", new Vector3(-0.68f, 0.40f, -0.98f), new Vector3(0.13f, 0.80f, 0.13f), warmOak);
                    Box(t, "PostFR", new Vector3(0.68f, 0.40f, -0.98f), new Vector3(0.13f, 0.80f, 0.13f), warmOak);
                    Sphere(t, "FinialFL", new Vector3(-0.68f, 0.83f, -0.98f), new Vector3(0.15f, 0.13f, 0.15f), honeyWood);
                    Sphere(t, "FinialFR", new Vector3(0.68f, 0.83f, -0.98f), new Vector3(0.15f, 0.13f, 0.15f), honeyWood);
                    // Headboard, footboard & side rails
                    Box(t, "Headboard", new Vector3(0f, 0.68f, 0.98f), new Vector3(1.26f, 0.66f, 0.09f), honeyWood);
                    Box(t, "Footboard", new Vector3(0f, 0.44f, -0.98f), new Vector3(1.26f, 0.46f, 0.09f), honeyWood);
                    Box(t, "BedRail", new Vector3(0f, 0.28f, 0f), new Vector3(1.34f, 0.22f, 1.92f), honeyWood);
                    // White mattress + fluffy white pillow + sky-blue blanket with deeper blue cuff
                    Box(t, "Mattress", new Vector3(0f, 0.48f, 0f), new Vector3(1.24f, 0.22f, 1.88f), crispWhite);
                    Box(t, "Pillow", new Vector3(0f, 0.65f, 0.62f), new Vector3(0.84f, 0.18f, 0.44f), crispWhite);
                    Box(t, "BlanketBody", new Vector3(0f, 0.54f, -0.34f), new Vector3(1.28f, 0.18f, 1.24f), skyBlue);
                    Box(t, "BlanketCuff", new Vector3(0f, 0.61f, 0.18f), new Vector3(1.30f, 0.14f, 0.26f), deepBlue);
                    break;

                // 8. Analog Radio — rounded retro mahogany radio, cream face, 4 left grille louvers, round right dial w/ red needle, yellow-tip antenna
                case FurnitureKind.AnalogRadio:
                    Box(t, "Cabinet", new Vector3(0f, 0.34f, 0f), new Vector3(1.06f, 0.62f, 0.44f), caramelWood);
                    Box(t, "TopArch", new Vector3(0f, 0.67f, 0f), new Vector3(0.96f, 0.08f, 0.40f), honeyWood);
                    Box(t, "FrontFace", new Vector3(0f, 0.34f, -0.22f), new Vector3(0.92f, 0.48f, 0.04f), warmWhite);
                    // 4 horizontal dark speaker louvers on left
                    for (int g = 0; g < 4; g++)
                    {
                        Box(t, $"Louver_{g}", new Vector3(-0.18f, 0.48f - g * 0.095f, -0.25f), new Vector3(0.42f, 0.042f, 0.025f), darkWood);
                    }
                    // Circular analog dial with red needle on right
                    Cyl(t, "DialRim", new Vector3(0.24f, 0.41f, -0.245f), new Vector3(0.28f, 0.02f, 0.28f), darkWood, new Vector3(90f, 0f, 0f));
                    Cyl(t, "DialFace", new Vector3(0.24f, 0.41f, -0.255f), new Vector3(0.23f, 0.02f, 0.23f), crispWhite, new Vector3(90f, 0f, 0f));
                    Box(t, "DialNeedle", new Vector3(0.27f, 0.44f, -0.27f), new Vector3(0.025f, 0.11f, 0.015f), redCross, new Vector3(0f, 0f, -32f));
                    // 3 small knobs underneath dial
                    for (int k = 0; k < 3; k++)
                    {
                        Cyl(t, $"Knob_{k}", new Vector3(0.14f + k * 0.10f, 0.19f, -0.255f), new Vector3(0.07f, 0.04f, 0.07f), honeyWood, new Vector3(90f, 0f, 0f));
                    }
                    // Long tilted silver antenna with bright yellow tip
                    Cyl(t, "AntennaRod", new Vector3(0.10f, 0.90f, 0.08f), new Vector3(0.03f, 0.46f, 0.03f), chrome, new Vector3(0f, 0f, -54f));
                    Sphere(t, "AntennaTip", new Vector3(0.46f, 1.15f, 0.08f), new Vector3(0.10f, 0.10f, 0.10f), goldYellow);
                    break;

                // 9. Desk Lamp — bright golden-yellow articulated swing-arm desk lamp with glowing bulb
                case FurnitureKind.DeskLamp:
                    Cyl(t, "BaseDisc", new Vector3(0f, 0.06f, 0f), new Vector3(0.48f, 0.06f, 0.48f), goldYellow);
                    Sphere(t, "BaseDome", new Vector3(0f, 0.12f, 0f), new Vector3(0.26f, 0.14f, 0.26f), goldYellow);
                    // Lower angled arm -> elbow joint -> upper angled arm
                    Cyl(t, "ArmLower", new Vector3(-0.14f, 0.34f, 0.06f), new Vector3(0.07f, 0.24f, 0.07f), goldYellow, new Vector3(-12f, 0f, 34f));
                    Sphere(t, "ElbowJoint", new Vector3(-0.26f, 0.54f, 0.10f), new Vector3(0.13f, 0.13f, 0.13f), goldYellow);
                    Cyl(t, "ArmUpper", new Vector3(-0.08f, 0.72f, 0.02f), new Vector3(0.07f, 0.22f, 0.07f), goldYellow, new Vector3(16f, 0f, -44f));
                    // Tilted yellow bell lampshade + glowing bulb pointing down-front (-Z)
                    Cyl(t, "ShadeCap", new Vector3(0.08f, 0.84f, -0.06f), new Vector3(0.22f, 0.09f, 0.22f), goldYellow, new Vector3(24f, 0f, -34f));
                    Cyl(t, "ShadeBell", new Vector3(0.18f, 0.71f, -0.14f), new Vector3(0.46f, 0.16f, 0.46f), lampYellow, new Vector3(24f, 0f, -34f));
                    Sphere(t, "GlowBulb", new Vector3(0.23f, 0.62f, -0.18f), new Vector3(0.24f, 0.22f, 0.24f), bulbGlow);
                    break;

                // 10. Cooking Pot Stove — compact silver gas burner, glowing BLUE gas flame, silver pot w/ handles & domed lid
                case FurnitureKind.CookingPotStove:
                    // Compact silver single-burner stove base
                    Box(t, "StoveBase", new Vector3(0f, 0.22f, 0f), new Vector3(0.98f, 0.36f, 0.86f), silverMetal);
                    Box(t, "StoveTopPlate", new Vector3(0f, 0.42f, 0f), new Vector3(0.92f, 0.06f, 0.80f), lockerSteel);
                    // Front circular dial knob
                    Cyl(t, "ControlKnob", new Vector3(0f, 0.22f, -0.45f), new Vector3(0.18f, 0.04f, 0.18f), darkIron, new Vector3(90f, 0f, 0f));
                    Box(t, "ControlKnobBar", new Vector3(0f, 0.22f, -0.49f), new Vector3(0.04f, 0.12f, 0.02f), chrome);
                    // Black burner grate + bright glowing BLUE gas flame ring
                    Box(t, "GrateX", new Vector3(0f, 0.48f, 0f), new Vector3(0.72f, 0.06f, 0.08f), darkIron);
                    Box(t, "GrateZ", new Vector3(0f, 0.48f, 0f), new Vector3(0.08f, 0.06f, 0.72f), darkIron);
                    Cyl(t, "BlueFlameRing", new Vector3(0f, 0.51f, 0f), new Vector3(0.46f, 0.04f, 0.46f), gasBlueFlame);
                    // Polished silver cooking pot + side handles + domed lid
                    Cyl(t, "PotBody", new Vector3(0f, 0.74f, 0f), new Vector3(0.64f, 0.22f, 0.64f), silverMetal);
                    Cyl(t, "PotRim", new Vector3(0f, 0.96f, 0f), new Vector3(0.68f, 0.03f, 0.68f), chrome);
                    Sphere(t, "PotLidDome", new Vector3(0f, 0.99f, 0f), new Vector3(0.62f, 0.16f, 0.62f), silverMetal);
                    Cyl(t, "LidHandle", new Vector3(0f, 1.09f, 0f), new Vector3(0.16f, 0.05f, 0.16f), darkIron);
                    Box(t, "PotHandleL", new Vector3(-0.37f, 0.84f, 0f), new Vector3(0.14f, 0.07f, 0.14f), chrome);
                    Box(t, "PotHandleR", new Vector3(0.37f, 0.84f, 0f), new Vector3(0.14f, 0.07f, 0.14f), chrome);
                    break;

                // 11–15. Five Square Wooden Crop Planter Boxes (Lime, Potato, Tomato, Carrot, Strawberry)
                case FurnitureKind.PlanterBoxLime:
                case FurnitureKind.PlanterBoxPotato:
                case FurnitureKind.PlanterBoxTomato:
                case FurnitureKind.PlanterBoxCarrot:
                case FurnitureKind.PlanterBoxStrawberry:
                    BuildSquarePlanterVisual(t, shader, honeyWood, warmOak, soil, leafDark, leafBright, crispWhite, goldYellow, materialResolver);
                    break;

                // 16. Vintage Refrigerator — rounded pastel mint-green 1950s fridge with horizontal chrome handles on left
                case FurnitureKind.VintageRefrigerator:
                    Legs(t, 0.78f, 0.66f, 0.10f, 0.10f, darkIron);
                    // Rounded mint body + top rounded cap
                    Box(t, "FridgeBody", new Vector3(0f, 0.96f, 0.02f), new Vector3(1.04f, 1.70f, 0.86f), mintFridge);
                    Box(t, "FridgeTopDome", new Vector3(0f, 1.84f, 0.02f), new Vector3(0.94f, 0.12f, 0.78f), mintFridge);
                    // Upper freezer door + lower fridge door + crisp dividing seam
                    Box(t, "FreezerDoor", new Vector3(0f, 1.44f, -0.43f), new Vector3(0.96f, 0.62f, 0.07f), mintFridge);
                    Box(t, "MainDoor", new Vector3(0f, 0.60f, -0.43f), new Vector3(0.96f, 0.96f, 0.07f), mintFridge);
                    Box(t, "DoorSeam", new Vector3(0f, 1.11f, -0.44f), new Vector3(1.02f, 0.04f, 0.07f), darkIron);
                    // Signature chunky horizontal white/chrome curved handles on left
                    Box(t, "HandleUpper", new Vector3(-0.28f, 1.32f, -0.50f), new Vector3(0.28f, 0.09f, 0.07f), chrome);
                    Box(t, "HandleLower", new Vector3(-0.28f, 0.88f, -0.50f), new Vector3(0.28f, 0.09f, 0.07f), chrome);
                    break;

                // 17. Medical Wall Cabinet — warm white rounded cabinet with bold red cross, left handle & right hinges
                case FurnitureKind.MedicalWallCabinet:
                    Box(t, "CabBody", new Vector3(0f, 1.24f, 0.08f), new Vector3(0.98f, 1.06f, 0.42f), warmWhite);
                    Box(t, "CabDoor", new Vector3(0f, 1.24f, -0.14f), new Vector3(0.88f, 0.94f, 0.05f), crispWhite);
                    // Bold centered red medical cross
                    Box(t, "CrossV", new Vector3(0f, 1.24f, -0.18f), new Vector3(0.14f, 0.46f, 0.03f), redCross);
                    Box(t, "CrossH", new Vector3(0f, 1.24f, -0.18f), new Vector3(0.46f, 0.14f, 0.03f), redCross);
                    // Left handle + right hinges
                    Box(t, "DoorHandle", new Vector3(-0.34f, 1.24f, -0.19f), new Vector3(0.05f, 0.22f, 0.06f), lockerSteel);
                    Box(t, "HingeTop", new Vector3(0.46f, 1.54f, -0.15f), new Vector3(0.06f, 0.14f, 0.05f), lockerSteel);
                    Box(t, "HingeBot", new Vector3(0.46f, 0.94f, -0.15f), new Vector3(0.06f, 0.14f, 0.05f), lockerSteel);
                    break;

                // 18. Leather Sofa — caramel-brown 2-seater leather couch with twin seat/back cushions & rolled arms
                case FurnitureKind.LeatherSofa:
                    Legs(t, 1.88f, 0.74f, 0.12f, 0.09f, darkWood);
                    Box(t, "SofaBase", new Vector3(0f, 0.24f, -0.02f), new Vector3(2.04f, 0.22f, 0.88f), leatherDark);
                    // Twin plump caramel seat cushions
                    Box(t, "SeatL", new Vector3(-0.44f, 0.44f, -0.06f), new Vector3(0.84f, 0.20f, 0.72f), leatherBrown);
                    Box(t, "SeatR", new Vector3(0.44f, 0.44f, -0.06f), new Vector3(0.84f, 0.20f, 0.72f), leatherBrown);
                    // Twin tall rounded leather backrest cushions
                    Box(t, "BackL", new Vector3(-0.44f, 0.80f, 0.30f), new Vector3(0.84f, 0.62f, 0.26f), leatherBrown);
                    Box(t, "BackR", new Vector3(0.44f, 0.80f, 0.30f), new Vector3(0.84f, 0.62f, 0.26f), leatherBrown);
                    Box(t, "BackDivider", new Vector3(0f, 0.78f, 0.28f), new Vector3(0.04f, 0.58f, 0.28f), leatherDark);
                    // Rolled armrests
                    Box(t, "ArmL", new Vector3(-0.96f, 0.48f, -0.02f), new Vector3(0.24f, 0.42f, 0.86f), leatherBrown);
                    Box(t, "ArmR", new Vector3(0.96f, 0.48f, -0.02f), new Vector3(0.24f, 0.42f, 0.86f), leatherBrown);
                    Cyl(t, "ArmRollL", new Vector3(-0.98f, 0.69f, -0.02f), new Vector3(0.26f, 0.43f, 0.26f), leatherBrown, new Vector3(90f, 0f, 0f));
                    Cyl(t, "ArmRollR", new Vector3(0.98f, 0.69f, -0.02f), new Vector3(0.26f, 0.43f, 0.26f), leatherBrown, new Vector3(90f, 0f, 0f));
                    break;

                // 19. Standing Floor Lamp — slender silver pole, round base, bright yellow tapered shade + hanging pull-chain
                case FurnitureKind.StandingFloorLamp:
                    Cyl(t, "BaseFoot", new Vector3(0f, 0.05f, 0f), new Vector3(0.56f, 0.05f, 0.56f), silverMetal);
                    Cyl(t, "BaseCollar", new Vector3(0f, 0.12f, 0f), new Vector3(0.26f, 0.06f, 0.26f), lockerSteel);
                    Cyl(t, "Stem", new Vector3(0f, 0.78f, 0f), new Vector3(0.085f, 0.68f, 0.085f), silverMetal);
                    // Tapered yellow lampshade (stacked concentric cylinders for classic trapezoid silhouette)
                    Cyl(t, "ShadeLower", new Vector3(0f, 1.54f, 0f), new Vector3(0.76f, 0.16f, 0.76f), lampYellow);
                    Cyl(t, "ShadeUpper", new Vector3(0f, 1.76f, 0f), new Vector3(0.56f, 0.14f, 0.56f), lampYellow);
                    Cyl(t, "ShadeRim", new Vector3(0f, 1.90f, 0f), new Vector3(0.46f, 0.03f, 0.46f), goldYellow);
                    Sphere(t, "GlowBulb", new Vector3(0f, 1.44f, 0f), new Vector3(0.22f, 0.20f, 0.22f), bulbGlow);
                    // Cute yellow hanging pull-chain with ball tassel
                    Cyl(t, "PullChain", new Vector3(0.18f, 1.24f, -0.10f), new Vector3(0.02f, 0.18f, 0.02f), goldYellow);
                    Sphere(t, "PullBall", new Vector3(0.18f, 1.06f, -0.10f), new Vector3(0.09f, 0.09f, 0.09f), goldYellow);
                    break;

                // 20. Tall Bookshelf — honey-oak 3-tier bookcase packed with colorful books across all 3 shelves
                case FurnitureKind.TallBookshelf:
                    Box(t, "BackPanel", new Vector3(0f, 1.04f, 0.24f), new Vector3(1.36f, 2.02f, 0.06f), darkWood);
                    Box(t, "SideL", new Vector3(-0.65f, 1.04f, 0f), new Vector3(0.10f, 2.04f, 0.56f), warmOak);
                    Box(t, "SideR", new Vector3(0.65f, 1.04f, 0f), new Vector3(0.10f, 2.04f, 0.56f), warmOak);
                    Box(t, "TopCornice", new Vector3(0f, 2.08f, 0f), new Vector3(1.46f, 0.12f, 0.62f), honeyWood);
                    Box(t, "BottomBase", new Vector3(0f, 0.08f, 0f), new Vector3(1.44f, 0.14f, 0.62f), honeyWood);
                    Material[] bookPalette = { plushRed, skyBlue, goldYellow, bookGreen, bookPurple, deepBlue, plushRed };
                    for (int s = 0; s < 3; s++)
                    {
                        float sy = 0.20f + s * 0.62f;
                        if (s > 0) Box(t, $"Shelf_{s}", new Vector3(0f, sy - 0.04f, -0.02f), new Vector3(1.24f, 0.07f, 0.50f), honeyWood);
                        for (int b = 0; b < 7; b++)
                        {
                            float bx = -0.49f + b * 0.162f;
                            float bh = 0.42f + ((b + s) % 3) * 0.045f;
                            Box(t, $"Book_{s}_{b}", new Vector3(bx, sy + bh * 0.5f, -0.05f), new Vector3(0.135f, bh, 0.36f), bookPalette[(b + s * 2) % bookPalette.Length]);
                        }
                    }
                    break;

                // 21. Wooden Dining Table — warm golden-oak rectangular table with apron and 4 chunky legs
                case FurnitureKind.WoodenDiningTable:
                    Legs(t, 1.54f, 0.88f, 0.78f, 0.12f, warmOak);
                    Box(t, "TableApron", new Vector3(0f, 0.72f, 0f), new Vector3(1.56f, 0.12f, 0.90f), warmOak);
                    Box(t, "TableTop", new Vector3(0f, 0.84f, 0f), new Vector3(1.78f, 0.12f, 1.12f), honeyWood);
                    break;

                // 22. Low Coffee Table — compact golden-oak coffee table with lower shelf + white coffee mug
                case FurnitureKind.LowCoffeeTable:
                    Legs(t, 1.18f, 0.62f, 0.46f, 0.10f, warmOak);
                    Box(t, "LowerShelf", new Vector3(0f, 0.18f, 0f), new Vector3(1.16f, 0.07f, 0.54f), warmOak);
                    Box(t, "TableTop", new Vector3(0f, 0.50f, 0f), new Vector3(1.36f, 0.10f, 0.78f), honeyWood);
                    // White ceramic coffee mug with dark coffee & side handle
                    Cyl(t, "MugBody", new Vector3(0.28f, 0.67f, -0.06f), new Vector3(0.20f, 0.12f, 0.20f), crispWhite);
                    Cyl(t, "MugCoffee", new Vector3(0.28f, 0.785f, -0.06f), new Vector3(0.16f, 0.015f, 0.16f), darkWood);
                    Box(t, "MugHandle", new Vector3(0.41f, 0.67f, -0.06f), new Vector3(0.08f, 0.12f, 0.04f), crispWhite);
                    break;

                // 23. Kitchen Counter Sink — honey-oak 2-door cabinet + yellow knobs + grey counter + left sink + gooseneck chrome faucet
                case FurnitureKind.KitchenCounterSink:
                    Box(t, "CabinetBase", new Vector3(0f, 0.42f, 0.02f), new Vector3(1.44f, 0.80f, 0.78f), warmOak);
                    Box(t, "DoorL", new Vector3(-0.34f, 0.42f, -0.38f), new Vector3(0.62f, 0.66f, 0.05f), honeyWood);
                    Box(t, "DoorR", new Vector3(0.34f, 0.42f, -0.38f), new Vector3(0.62f, 0.66f, 0.05f), honeyWood);
                    Sphere(t, "KnobL", new Vector3(-0.09f, 0.44f, -0.43f), new Vector3(0.09f, 0.09f, 0.07f), goldYellow);
                    Sphere(t, "KnobR", new Vector3(0.09f, 0.44f, -0.43f), new Vector3(0.09f, 0.09f, 0.07f), goldYellow);
                    // Silver-grey countertop + recessed sink basin on left
                    Box(t, "CounterTop", new Vector3(0f, 0.87f, 0f), new Vector3(1.56f, 0.11f, 0.88f), silverMetal);
                    Box(t, "SinkBasin", new Vector3(-0.24f, 0.91f, -0.02f), new Vector3(0.66f, 0.06f, 0.50f), lockerSteel);
                    Box(t, "SinkInner", new Vector3(-0.24f, 0.935f, -0.02f), new Vector3(0.56f, 0.03f, 0.40f), darkIron);
                    // Curved gooseneck chrome faucet + twin tap knobs on right/rear
                    Cyl(t, "FaucetBase", new Vector3(0.32f, 0.97f, 0.12f), new Vector3(0.12f, 0.06f, 0.12f), chrome);
                    Cyl(t, "FaucetNeck", new Vector3(0.32f, 1.18f, 0.12f), new Vector3(0.07f, 0.22f, 0.07f), chrome);
                    Box(t, "FaucetArch", new Vector3(0.18f, 1.38f, 0.12f), new Vector3(0.30f, 0.07f, 0.07f), chrome);
                    Cyl(t, "FaucetSpout", new Vector3(0.05f, 1.30f, 0.12f), new Vector3(0.065f, 0.09f, 0.065f), chrome);
                    Sphere(t, "TapKnobL", new Vector3(0.18f, 0.99f, 0.22f), new Vector3(0.09f, 0.07f, 0.09f), chrome);
                    Sphere(t, "TapKnobR", new Vector3(0.46f, 0.99f, 0.22f), new Vector3(0.09f, 0.07f, 0.09f), chrome);
                    break;

                // 24. First Aid Station — bright red carry medkit box, white front panel with red cross, top handle, bandage roll & band-aids peeking out
                case FurnitureKind.FirstAidStation:
                    // Red kit shell + white front face with red cross
                    Box(t, "KitBaseRed", new Vector3(0f, 0.42f, 0.04f), new Vector3(1.12f, 0.72f, 0.64f), plushRed);
                    Box(t, "KitBackLid", new Vector3(0f, 0.94f, 0.26f), new Vector3(1.10f, 0.42f, 0.18f), plushRed);
                    Box(t, "KitHandleTop", new Vector3(0f, 1.24f, 0.26f), new Vector3(0.44f, 0.08f, 0.08f), plushRed);
                    Box(t, "KitHandleL", new Vector3(-0.18f, 1.18f, 0.26f), new Vector3(0.08f, 0.12f, 0.08f), plushRed);
                    Box(t, "KitHandleR", new Vector3(0.18f, 1.18f, 0.26f), new Vector3(0.08f, 0.12f, 0.08f), plushRed);
                    Box(t, "FrontWhiteBand", new Vector3(0f, 0.42f, -0.29f), new Vector3(1.00f, 0.46f, 0.05f), crispWhite);
                    Box(t, "CrossV", new Vector3(0f, 0.42f, -0.33f), new Vector3(0.11f, 0.34f, 0.03f), redCross);
                    Box(t, "CrossH", new Vector3(0f, 0.42f, -0.33f), new Vector3(0.34f, 0.11f, 0.03f), redCross);
                    // Rolled white gauze bandage + 2 tan adhesive bandages peeking out the open top
                    Cyl(t, "GauzeRoll", new Vector3(-0.22f, 0.84f, -0.02f), new Vector3(0.34f, 0.24f, 0.34f), warmWhite);
                    Cyl(t, "GauzeCore", new Vector3(-0.22f, 0.85f, -0.02f), new Vector3(0.14f, 0.25f, 0.14f), bandageTan);
                    Box(t, "BandAid1", new Vector3(0.16f, 0.88f, 0.02f), new Vector3(0.16f, 0.38f, 0.08f), bandageTan, new Vector3(0f, 0f, -14f));
                    Box(t, "BandAid2", new Vector3(0.34f, 0.84f, -0.04f), new Vector3(0.16f, 0.36f, 0.08f), bandageTan, new Vector3(0f, 0f, -24f));
                    break;

                // 25. Weapon Display Rack — rustic slatted golden-oak wall board with 2 vertical bars, brown rifle + silver tactical axe
                case FurnitureKind.WeaponDisplayRack:
                    // Vertical wall rail posts + horizontal slatted wooden planks
                    Box(t, "RailL", new Vector3(-0.56f, 1.18f, 0.14f), new Vector3(0.14f, 1.24f, 0.10f), warmOak);
                    Box(t, "RailR", new Vector3(0.56f, 1.18f, 0.14f), new Vector3(0.14f, 1.24f, 0.10f), warmOak);
                    Box(t, "PlankTop", new Vector3(0f, 1.48f, 0.10f), new Vector3(1.56f, 0.30f, 0.07f), honeyWood);
                    Box(t, "PlankMid", new Vector3(0f, 1.16f, 0.10f), new Vector3(1.56f, 0.30f, 0.07f), warmOak);
                    Box(t, "PlankBot", new Vector3(0f, 0.84f, 0.10f), new Vector3(1.56f, 0.30f, 0.07f), honeyWood);
                    // Classic hunting/combat rifle across upper pegs
                    Box(t, "RifleStock", new Vector3(-0.40f, 1.38f, 0.01f), new Vector3(0.54f, 0.14f, 0.09f), caramelWood, new Vector3(0f, 0f, 10f));
                    Box(t, "RifleReceiver", new Vector3(-0.02f, 1.44f, 0.01f), new Vector3(0.38f, 0.12f, 0.09f), darkIron);
                    Cyl(t, "RifleBarrel", new Vector3(0.36f, 1.46f, 0.01f), new Vector3(0.06f, 0.36f, 0.06f), darkIron, new Vector3(0f, 0f, 90f));
                    // Wooden-handled silver axe across lower pegs
                    Box(t, "AxeHandle", new Vector3(-0.08f, 0.94f, 0.01f), new Vector3(0.68f, 0.09f, 0.07f), goldYellow, new Vector3(0f, 0f, 12f));
                    Box(t, "AxeHead", new Vector3(0.28f, 0.92f, 0.00f), new Vector3(0.26f, 0.32f, 0.08f), silverMetal, new Vector3(0f, 0f, 12f));
                    Box(t, "AxeEdge", new Vector3(0.30f, 0.77f, 0.00f), new Vector3(0.28f, 0.05f, 0.085f), chrome, new Vector3(0f, 0f, 12f));
                    break;

                // 26. Wood Burning Stove — chunky dark cast-iron stove, chimney pipe, arched fire door w/ glowing flames & logs
                case FurnitureKind.WoodBurningStove:
                    Legs(t, 0.86f, 0.70f, 0.18f, 0.11f, darkIron);
                    Box(t, "BaseRim", new Vector3(0f, 0.22f, 0f), new Vector3(1.12f, 0.10f, 0.92f), lockerSteel);
                    Box(t, "StoveBody", new Vector3(0f, 0.66f, 0f), new Vector3(1.02f, 0.80f, 0.84f), darkIron);
                    Box(t, "TopLip", new Vector3(0f, 1.09f, 0f), new Vector3(1.14f, 0.10f, 0.94f), lockerSteel);
                    // Short cylindrical chimney pipe + collar on top
                    Cyl(t, "FlueCollar", new Vector3(0f, 1.18f, 0.04f), new Vector3(0.34f, 0.05f, 0.34f), lockerSteel);
                    Cyl(t, "ChimneyPipe", new Vector3(0f, 1.46f, 0.04f), new Vector3(0.26f, 0.28f, 0.26f), darkIron);
                    Cyl(t, "ChimneyRim", new Vector3(0f, 1.74f, 0.04f), new Vector3(0.30f, 0.03f, 0.30f), lockerSteel);
                    // Arched glowing firebox window facing -Z with logs & flames
                    Box(t, "DoorFrame", new Vector3(0f, 0.64f, -0.43f), new Vector3(0.76f, 0.60f, 0.05f), lockerSteel);
                    Box(t, "FireboxBack", new Vector3(0f, 0.62f, -0.45f), new Vector3(0.62f, 0.48f, 0.03f), darkWood);
                    Box(t, "FireOuter", new Vector3(0f, 0.64f, -0.47f), new Vector3(0.46f, 0.34f, 0.03f), fireGlow);
                    Box(t, "FireInner", new Vector3(0f, 0.58f, -0.48f), new Vector3(0.28f, 0.22f, 0.03f), fireCore);
                    Box(t, "Log1", new Vector3(-0.10f, 0.44f, -0.49f), new Vector3(0.26f, 0.08f, 0.06f), warmOak, new Vector3(0f, 0f, 12f));
                    Box(t, "Log2", new Vector3(0.10f, 0.44f, -0.49f), new Vector3(0.26f, 0.08f, 0.06f), warmOak, new Vector3(0f, 0f, -12f));
                    Sphere(t, "DoorLatch", new Vector3(0.42f, 0.64f, -0.47f), new Vector3(0.08f, 0.08f, 0.06f), goldYellow);
                    break;

                // 27. Wall Mounted TV — sleek charcoal flat-screen TV with glossy sky-blue glowing screen
                case FurnitureKind.WallMountedTV:
                    Box(t, "WallMount", new Vector3(0f, 1.24f, 0.14f), new Vector3(0.64f, 0.38f, 0.12f), darkIron);
                    Box(t, "TvFrame", new Vector3(0f, 1.24f, 0.04f), new Vector3(1.56f, 0.96f, 0.11f), darkIron);
                    Box(t, "TvInnerBezel", new Vector3(0f, 1.24f, -0.015f), new Vector3(1.44f, 0.84f, 0.03f), lockerSteel);
                    Box(t, "TvScreen", new Vector3(0f, 1.24f, -0.032f), new Vector3(1.38f, 0.78f, 0.02f), tvBlueGlow);
                    Box(t, "TvStandNub", new Vector3(0f, 0.72f, 0.04f), new Vector3(0.22f, 0.08f, 0.10f), darkIron);
                    break;

                // 28. Woven Area Rug — vibrant crimson-red rug with golden border, stepped diamond center & chunky golden fringes
                case FurnitureKind.WovenAreaRug:
                    Box(t, "RugRedBase", new Vector3(0f, 0.02f, 0f), new Vector3(2.84f, 0.035f, 2.04f), plushRed);
                    // Golden inner border strips
                    Box(t, "BorderN", new Vector3(0f, 0.038f, 0.80f), new Vector3(2.44f, 0.015f, 0.12f), goldYellow);
                    Box(t, "BorderS", new Vector3(0f, 0.038f, -0.80f), new Vector3(2.44f, 0.015f, 0.12f), goldYellow);
                    Box(t, "BorderW", new Vector3(-1.16f, 0.038f, 0f), new Vector3(0.12f, 0.015f, 1.72f), goldYellow);
                    Box(t, "BorderE", new Vector3(1.16f, 0.038f, 0f), new Vector3(0.12f, 0.015f, 1.72f), goldYellow);
                    // Stepped diamond medallion in center
                    Box(t, "DiamondOuter", new Vector3(0f, 0.042f, 0f), new Vector3(0.86f, 0.015f, 0.86f), goldYellow, new Vector3(0f, 45f, 0f));
                    Box(t, "DiamondMid", new Vector3(0f, 0.046f, 0f), new Vector3(0.54f, 0.015f, 0.54f), plushRed, new Vector3(0f, 45f, 0f));
                    Box(t, "DiamondCore", new Vector3(0f, 0.050f, 0f), new Vector3(0.24f, 0.015f, 0.24f), goldYellow, new Vector3(0f, 45f, 0f));
                    // Chunky golden tassels along left (-X) and right (+X) ends
                    for (int f = 0; f < 7; f++)
                    {
                        float fz = -0.84f + f * 0.28f;
                        Box(t, $"FringeL_{f}", new Vector3(-1.54f, 0.02f, fz), new Vector3(0.24f, 0.025f, 0.12f), goldYellow);
                        Box(t, $"FringeR_{f}", new Vector3(1.54f, 0.02f, fz), new Vector3(0.24f, 0.025f, 0.12f), goldYellow);
                    }
                    break;

                // 29. Potted Indoor Fern — terracotta clay pot with wide rim + lush radial arching lime/emerald fronds
                case FurnitureKind.PottedIndoorFern:
                    Cyl(t, "PotBase", new Vector3(0f, 0.22f, 0f), new Vector3(0.46f, 0.22f, 0.46f), terracotta);
                    Cyl(t, "PotRim", new Vector3(0f, 0.46f, 0f), new Vector3(0.56f, 0.06f, 0.56f), terracotta);
                    Cyl(t, "PotSoil", new Vector3(0f, 0.49f, 0f), new Vector3(0.48f, 0.02f, 0.48f), soil);
                    // Lush multi-layer radial arching fern fronds
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = i * 36f;
                        float rad = ang * Mathf.Deg2Rad;
                        Vector3 posOuter = new Vector3(Mathf.Sin(rad) * 0.34f, 0.66f, Mathf.Cos(rad) * 0.34f);
                        Box(t, $"FrondOuter_{i}", posOuter, new Vector3(0.16f, 0.05f, 0.52f), (i % 2 == 0) ? leafBright : leafDark, new Vector3(26f, ang, 0f));
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float ang = i * 60f + 18f;
                        float rad = ang * Mathf.Deg2Rad;
                        Vector3 posInner = new Vector3(Mathf.Sin(rad) * 0.18f, 0.84f, Mathf.Cos(rad) * 0.18f);
                        Box(t, $"FrondInner_{i}", posInner, new Vector3(0.14f, 0.05f, 0.40f), leafBright, new Vector3(-28f, ang, 0f));
                    }
                    break;

                // 30. Trash Bin Barrel — ribbed olive-grey metal barrel with bands, handles & slightly tilted lid
                case FurnitureKind.TrashBinBarrel:
                    Cyl(t, "BinBody", new Vector3(0f, 0.38f, 0f), new Vector3(0.56f, 0.38f, 0.56f), oliveMetal);
                    Cyl(t, "BinBand1", new Vector3(0f, 0.24f, 0f), new Vector3(0.60f, 0.03f, 0.60f), darkIron);
                    Cyl(t, "BinBand2", new Vector3(0f, 0.54f, 0f), new Vector3(0.60f, 0.03f, 0.60f), darkIron);
                    Cyl(t, "BinRim", new Vector3(0f, 0.77f, 0f), new Vector3(0.62f, 0.035f, 0.62f), darkIron);
                    Box(t, "HandleL", new Vector3(-0.31f, 0.56f, 0f), new Vector3(0.08f, 0.06f, 0.16f), darkIron);
                    Box(t, "HandleR", new Vector3(0.31f, 0.56f, 0f), new Vector3(0.08f, 0.06f, 0.16f), darkIron);
                    // Slightly tilted corrugated lid + top loop handle
                    Cyl(t, "BinLid", new Vector3(0.04f, 0.83f, 0f), new Vector3(0.64f, 0.045f, 0.64f), oliveMetal, new Vector3(0f, 0f, -10f));
                    Box(t, "LidHandle", new Vector3(0.06f, 0.90f, 0f), new Vector3(0.18f, 0.05f, 0.05f), darkIron, new Vector3(0f, 0f, -10f));
                    break;
            }
        }

        private void BuildSquarePlanterVisual(
            Transform t,
            Shader shader,
            Material honeyWood,
            Material warmOak,
            Material soil,
            Material leafDark,
            Material leafBright,
            Material crispWhite,
            Material goldYellow,
            System.Func<string, Color, float, Color?, Material> materialResolver)
        {
            Material ResolveCropMat(string key, Color color, float smoothness, Color? emission = null)
            {
                return materialResolver != null
                    ? materialResolver(key, color, smoothness, emission)
                    : MakeMat(shader, color, smoothness, emission);
            }

            // Warm slatted wooden crate matching the 5 planter icons
            Box(t, "PlanterBox", new Vector3(0f, 0.22f, 0f), new Vector3(1.20f, 0.42f, 1.20f), honeyWood);
            // Slatted wood seam groove around middle
            Box(t, "SlatGroove", new Vector3(0f, 0.22f, 0f), new Vector3(1.22f, 0.03f, 1.22f), warmOak);
            // 4 wooden corner posts + top lip
            float c = 0.58f;
            Box(t, "CornerFL", new Vector3(-c, 0.23f, -c), new Vector3(0.12f, 0.46f, 0.12f), warmOak);
            Box(t, "CornerFR", new Vector3(c, 0.23f, -c), new Vector3(0.12f, 0.46f, 0.12f), warmOak);
            Box(t, "CornerBL", new Vector3(-c, 0.23f, c), new Vector3(0.12f, 0.46f, 0.12f), warmOak);
            Box(t, "CornerBR", new Vector3(c, 0.23f, c), new Vector3(0.12f, 0.46f, 0.12f), warmOak);
            Box(t, "RimN", new Vector3(0f, 0.45f, 0.56f), new Vector3(1.28f, 0.08f, 0.11f), warmOak);
            Box(t, "RimS", new Vector3(0f, 0.45f, -0.56f), new Vector3(1.28f, 0.08f, 0.11f), warmOak);
            Box(t, "RimE", new Vector3(0.56f, 0.45f, 0f), new Vector3(0.11f, 0.08f, 1.08f), warmOak);
            Box(t, "RimW", new Vector3(-0.56f, 0.45f, 0f), new Vector3(0.11f, 0.08f, 1.08f), warmOak);
            Box(t, "SoilBed", new Vector3(0f, 0.43f, 0f), new Vector3(1.06f, 0.06f, 1.06f), soil);

            // Preserve CropFoliageRoot & CropFruitRoot names for CropPlanterFurniture growth stages!
            GameObject foliageRoot = new GameObject("CropFoliageRoot");
            foliageRoot.transform.SetParent(t, false);
            foliageRoot.transform.localPosition = new Vector3(0f, 0.46f, 0f);

            GameObject fruitRoot = new GameObject("CropFruitRoot");
            fruitRoot.transform.SetParent(t, false);
            fruitRoot.transform.localPosition = new Vector3(0f, 0.46f, 0f);

            switch (furnitureKind)
            {
                case FurnitureKind.PlanterBoxLime:
                {
                    Material limeMat = ResolveCropMat("Crop_Lime", new Color(0.56f, 0.92f, 0.18f), 0.40f, new Color(0.28f, 0.50f, 0.08f) * 0.30f);
                    // Lush citrus bush + plump oval green limes
                    Cyl(foliageRoot.transform, "MainStem", new Vector3(0f, 0.18f, 0f), new Vector3(0.08f, 0.20f, 0.08f), warmOak);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Box(foliageRoot.transform, $"Leaf_{i}", new Vector3(Mathf.Sin(a) * 0.28f, 0.34f + (i % 2) * 0.08f, Mathf.Cos(a) * 0.28f), new Vector3(0.26f, 0.07f, 0.36f), (i % 2 == 0) ? leafDark : leafBright, new Vector3(18f, i * 60f, 0f));
                    }
                    Sphere(fruitRoot.transform, "Lime_1", new Vector3(-0.22f, 0.28f, -0.18f), new Vector3(0.24f, 0.20f, 0.20f), limeMat);
                    Sphere(fruitRoot.transform, "Lime_2", new Vector3(0.22f, 0.30f, 0.10f), new Vector3(0.24f, 0.20f, 0.20f), limeMat);
                    Sphere(fruitRoot.transform, "Lime_3", new Vector3(0.04f, 0.42f, -0.10f), new Vector3(0.22f, 0.18f, 0.18f), limeMat);
                    Sphere(fruitRoot.transform, "Lime_4", new Vector3(-0.14f, 0.24f, 0.22f), new Vector3(0.22f, 0.19f, 0.19f), limeMat);
                    break;
                }

                case FurnitureKind.PlanterBoxPotato:
                {
                    Material potatoMat = ResolveCropMat("Crop_Potato", new Color(0.86f, 0.66f, 0.34f), 0.22f);
                    // Bushy green stems + cute white blossoms w/ yellow centers + golden potatoes resting on soil
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Box(foliageRoot.transform, $"Leaf_{i}", new Vector3(Mathf.Sin(a) * 0.26f, 0.24f, Mathf.Cos(a) * 0.26f), new Vector3(0.28f, 0.10f, 0.30f), (i % 2 == 0) ? leafBright : leafDark, new Vector3(14f, i * 60f, 0f));
                    }
                    Sphere(foliageRoot.transform, "Flower1", new Vector3(-0.14f, 0.44f, 0.06f), new Vector3(0.16f, 0.07f, 0.16f), crispWhite);
                    Sphere(foliageRoot.transform, "Flower1Core", new Vector3(-0.14f, 0.47f, 0.06f), new Vector3(0.07f, 0.06f, 0.07f), goldYellow);
                    Sphere(foliageRoot.transform, "Flower2", new Vector3(0.16f, 0.46f, -0.04f), new Vector3(0.16f, 0.07f, 0.16f), crispWhite);
                    Sphere(foliageRoot.transform, "Flower2Core", new Vector3(0.16f, 0.49f, -0.04f), new Vector3(0.07f, 0.06f, 0.07f), goldYellow);
                    Sphere(fruitRoot.transform, "Potato_1", new Vector3(-0.22f, 0.08f, -0.22f), new Vector3(0.28f, 0.18f, 0.22f), potatoMat);
                    Sphere(fruitRoot.transform, "Potato_2", new Vector3(0.20f, 0.08f, -0.18f), new Vector3(0.30f, 0.18f, 0.22f), potatoMat);
                    Sphere(fruitRoot.transform, "Potato_3", new Vector3(0.02f, 0.08f, 0.22f), new Vector3(0.26f, 0.17f, 0.20f), potatoMat);
                    break;
                }

                case FurnitureKind.PlanterBoxTomato:
                {
                    Material tomatoMat = ResolveCropMat("Crop_Tomato", new Color(0.95f, 0.16f, 0.14f), 0.48f, new Color(0.55f, 0.08f, 0.06f) * 0.25f);
                    // Lush tomato vine + big glossy bright-red tomatoes
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Box(foliageRoot.transform, $"Leaf_{i}", new Vector3(Mathf.Sin(a) * 0.28f, 0.34f + (i % 2) * 0.08f, Mathf.Cos(a) * 0.28f), new Vector3(0.28f, 0.08f, 0.32f), (i % 2 == 0) ? leafDark : leafBright, new Vector3(12f, i * 60f, 0f));
                    }
                    Sphere(fruitRoot.transform, "Tomato_1", new Vector3(-0.24f, 0.22f, -0.18f), new Vector3(0.26f, 0.23f, 0.26f), tomatoMat);
                    Sphere(fruitRoot.transform, "Tomato_2", new Vector3(0.22f, 0.28f, -0.16f), new Vector3(0.26f, 0.23f, 0.26f), tomatoMat);
                    Sphere(fruitRoot.transform, "Tomato_3", new Vector3(0.04f, 0.40f, 0.06f), new Vector3(0.24f, 0.22f, 0.24f), tomatoMat);
                    Sphere(fruitRoot.transform, "Tomato_4", new Vector3(-0.14f, 0.22f, 0.22f), new Vector3(0.24f, 0.22f, 0.24f), tomatoMat);
                    break;
                }

                case FurnitureKind.PlanterBoxCarrot:
                {
                    Material carrotMat = ResolveCropMat("Crop_Carrot", new Color(0.98f, 0.48f, 0.08f), 0.32f, new Color(0.55f, 0.24f, 0.04f) * 0.20f);
                    // 3 plump bright-orange carrots sticking out of soil + tall feathery green leafy tops
                    Vector3[] cPos = { new Vector3(-0.26f, 0f, -0.14f), new Vector3(0.04f, 0f, 0.16f), new Vector3(0.26f, 0f, -0.14f) };
                    for (int i = 0; i < cPos.Length; i++)
                    {
                        Vector3 p = cPos[i];
                        Cyl(fruitRoot.transform, $"CarrotBody_{i}", p + new Vector3(0f, 0.12f, 0f), new Vector3(0.20f, 0.13f, 0.20f), carrotMat);
                        Sphere(fruitRoot.transform, $"CarrotTop_{i}", p + new Vector3(0f, 0.22f, 0f), new Vector3(0.21f, 0.12f, 0.21f), carrotMat);
                        // Feathery green carrot tops
                        Box(foliageRoot.transform, $"CarrotLeafA_{i}", p + new Vector3(-0.04f, 0.42f, 0f), new Vector3(0.10f, 0.34f, 0.10f), leafBright, new Vector3(0f, 20f, 16f));
                        Box(foliageRoot.transform, $"CarrotLeafB_{i}", p + new Vector3(0.05f, 0.44f, 0.02f), new Vector3(0.10f, 0.36f, 0.10f), leafDark, new Vector3(12f, -15f, -18f));
                    }
                    break;
                }

                default: // PlanterBoxStrawberry
                {
                    Material berryMat = ResolveCropMat("Crop_Strawberry", new Color(0.96f, 0.14f, 0.26f), 0.42f, new Color(0.55f, 0.06f, 0.14f) * 0.25f);
                    // Dark green strawberry leaves + white/yellow flowers + plump red strawberries
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        Box(foliageRoot.transform, $"Leaf_{i}", new Vector3(Mathf.Sin(a) * 0.28f, 0.26f, Mathf.Cos(a) * 0.28f), new Vector3(0.28f, 0.08f, 0.32f), (i % 2 == 0) ? leafDark : leafBright, new Vector3(16f, i * 60f, 0f));
                    }
                    Sphere(foliageRoot.transform, "Flower1", new Vector3(-0.08f, 0.40f, 0.04f), new Vector3(0.16f, 0.07f, 0.16f), crispWhite);
                    Sphere(foliageRoot.transform, "Flower1Core", new Vector3(-0.08f, 0.43f, 0.04f), new Vector3(0.07f, 0.06f, 0.07f), goldYellow);
                    Sphere(foliageRoot.transform, "Flower2", new Vector3(0.14f, 0.38f, 0.10f), new Vector3(0.15f, 0.07f, 0.15f), crispWhite);
                    Sphere(foliageRoot.transform, "Flower2Core", new Vector3(0.14f, 0.41f, 0.10f), new Vector3(0.06f, 0.06f, 0.06f), goldYellow);
                    Sphere(fruitRoot.transform, "Berry_1", new Vector3(-0.24f, 0.18f, -0.22f), new Vector3(0.20f, 0.25f, 0.20f), berryMat);
                    Sphere(fruitRoot.transform, "Berry_2", new Vector3(0.22f, 0.18f, -0.20f), new Vector3(0.20f, 0.25f, 0.20f), berryMat);
                    Sphere(fruitRoot.transform, "Berry_3", new Vector3(0.02f, 0.22f, -0.28f), new Vector3(0.18f, 0.22f, 0.18f), berryMat);
                    Sphere(fruitRoot.transform, "Berry_4", new Vector3(-0.12f, 0.18f, 0.24f), new Vector3(0.19f, 0.23f, 0.19f), berryMat);
                    break;
                }
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

        private static void SafeDestroyCollider(Collider c)
        {
            if (c == null) return;
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        private static void Box(Transform p, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = scale;
            SafeDestroyCollider(go.GetComponent<Collider>());
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
            SafeDestroyCollider(go.GetComponent<Collider>());
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
            SafeDestroyCollider(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }
    }
}
