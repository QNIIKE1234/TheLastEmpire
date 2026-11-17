using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Procedural low-poly / cardboard-style prop and foliage builder for stage decoration.
    /// Provides 5 grass variants, 5 bush variants, 5 tree variants, and 10 debris/trash props.
    /// </summary>
    public static class ProceduralPropBuilder
    {
        public enum PropCategory
        {
            Grass,
            Bush,
            Tree,
            Debris
        }

        private static readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();
        private static Shader _cachedUrpLit;

        /// <summary>
        /// Picks a category weighted by biome and returns the expected XZ footprint size for overlap checks.
        /// </summary>
        public static PropCategory PickCategoryForBiome(BiomeType biome, out float footprintSize)
        {
            float roll = Random.value;
            PropCategory category;

            switch (biome)
            {
                case BiomeType.OvergrownForests:
                    if (roll < 0.35f) category = PropCategory.Grass;
                    else if (roll < 0.65f) category = PropCategory.Bush;
                    else if (roll < 0.88f) category = PropCategory.Tree;
                    else category = PropCategory.Debris;
                    break;

                case BiomeType.UrbanRuins:
                case BiomeType.SuburbanVillages:
                    if (roll < 0.25f) category = PropCategory.Grass;
                    else if (roll < 0.45f) category = PropCategory.Bush;
                    else if (roll < 0.60f) category = PropCategory.Tree;
                    else category = PropCategory.Debris;
                    break;

                default:
                    if (roll < 0.30f) category = PropCategory.Grass;
                    else if (roll < 0.55f) category = PropCategory.Bush;
                    else if (roll < 0.72f) category = PropCategory.Tree;
                    else category = PropCategory.Debris;
                    break;
            }

            footprintSize = GetFootprintSize(category);
            return category;
        }

        public static float GetFootprintSize(PropCategory category)
        {
            switch (category)
            {
                case PropCategory.Tree: return 2.4f;
                case PropCategory.Bush: return 1.5f;
                case PropCategory.Debris: return 1.5f;
                default: return 1.1f;
            }
        }

        private static float GetScaleMultiplier(PropCategory category)
        {
            switch (category)
            {
                case PropCategory.Tree: return 1.85f;
                case PropCategory.Bush: return 1.65f;
                case PropCategory.Debris: return 1.55f;
                default: return 1.75f;
            }
        }

        /// <summary>
        /// Spawns a procedural prop of the given category at worldPos (ground Y = 0).
        /// </summary>
        public static GameObject SpawnProp(
            PropCategory category,
            Transform parent,
            Vector3 worldXZPos,
            Material baseMaterial,
            bool snapToGrid)
        {
            GameObject root;
            switch (category)
            {
                case PropCategory.Grass:
                    root = BuildGrass(Random.Range(0, 5), parent, worldXZPos, baseMaterial, snapToGrid);
                    break;
                case PropCategory.Bush:
                    root = BuildBush(Random.Range(0, 5), parent, worldXZPos, baseMaterial, snapToGrid);
                    break;
                case PropCategory.Tree:
                    root = BuildTree(Random.Range(0, 5), parent, worldXZPos, baseMaterial, snapToGrid);
                    break;
                default:
                    root = BuildDebris(Random.Range(0, 10), parent, worldXZPos, baseMaterial, snapToGrid);
                    break;
            }

            if (root != null)
            {
                root.transform.localScale = Vector3.one * GetScaleMultiplier(category);
            }
            return root;
        }

        private static GameObject CreateRoot(string name, Transform parent, Vector3 worldXZPos, bool snapToGrid)
        {
            GameObject root = new GameObject(name);
            root.transform.parent = parent;
            root.transform.position = new Vector3(worldXZPos.x, 0f, worldXZPos.y);
            float yaw = snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return root;
        }

        private static Material GetCachedMaterial(Material baseMaterial, Color color)
        {
            // Brighten low-poly colors slightly so they never look muddy under stage lighting
            Color brightColor = new Color(
                Mathf.Clamp01(color.r * 1.35f + 0.06f),
                Mathf.Clamp01(color.g * 1.35f + 0.06f),
                Mathf.Clamp01(color.b * 1.35f + 0.06f),
                1f);

            Color32 c32 = brightColor;
            int key = c32.r | (c32.g << 8) | (c32.b << 16) | (c32.a << 24);

            if (_materialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            if (_cachedUrpLit == null)
            {
                _cachedUrpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }

            Material mat = new Material(_cachedUrpLit);
            mat.SetColor("_BaseColor", brightColor);
            mat.color = brightColor;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.22f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            _materialCache[key] = mat;
            return mat;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType type,
            string partName,
            Vector3 localPos,
            Vector3 localScale,
            Vector3 localEuler,
            Color color,
            Material baseMat,
            bool keepCollider = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localEuler);

            if (!keepCollider)
            {
                Collider col = part.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Object.Destroy(col);
                    else Object.DestroyImmediate(col);
                }
            }

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = GetCachedMaterial(baseMat, color);
            }

            return part;
        }

        // =====================================================================
        // 5 GRASS VARIANTS
        // =====================================================================
        private static GameObject BuildGrass(int variant, Transform parent, Vector3 pos, Material baseMat, bool snapToGrid)
        {
            string[] names = { "Grass_TuftCluster", "Grass_TallWild", "Grass_DrySavanna", "Grass_CloverPatch", "Grass_OvergrownReed" };
            GameObject root = CreateRoot(names[variant], parent, pos, snapToGrid);

            switch (variant)
            {
                case 0: // Tuft cluster
                {
                    Color c = new Color(0.28f, 0.58f, 0.22f);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * 90f + 20f;
                        Vector3 offset = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.08f, 0.14f, 0f);
                        CreatePart(root.transform, PrimitiveType.Cube, "Blade", offset, new Vector3(0.06f, 0.28f, 0.04f), new Vector3(0f, ang, 18f), c, baseMat);
                    }
                    break;
                }
                case 1: // Tall wild grass
                {
                    Color c = new Color(0.22f, 0.50f, 0.18f);
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * 72f;
                        Vector3 offset = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.10f, 0.22f, 0f);
                        CreatePart(root.transform, PrimitiveType.Cube, "TallBlade", offset, new Vector3(0.05f, 0.45f, 0.04f), new Vector3(0f, ang, 14f), c, baseMat);
                    }
                    break;
                }
                case 2: // Dry savanna grass
                {
                    Color c = new Color(0.68f, 0.56f, 0.26f);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * 90f + 10f;
                        Vector3 offset = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.09f, 0.16f, 0f);
                        CreatePart(root.transform, PrimitiveType.Cube, "DryBlade", offset, new Vector3(0.05f, 0.34f, 0.04f), new Vector3(0f, ang, 24f), c, baseMat);
                    }
                    break;
                }
                case 3: // Clover patch
                {
                    Color c1 = new Color(0.24f, 0.55f, 0.24f);
                    Color c2 = new Color(0.32f, 0.64f, 0.28f);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * 90f;
                        Vector3 offset = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.14f, 0.02f, 0.05f);
                        CreatePart(root.transform, PrimitiveType.Cylinder, "Clover", offset, new Vector3(0.24f, 0.02f, 0.24f), Vector3.zero, (i % 2 == 0) ? c1 : c2, baseMat);
                    }
                    break;
                }
                default: // Overgrown reed
                {
                    Color stalk = new Color(0.32f, 0.48f, 0.20f);
                    Color tip = new Color(0.54f, 0.42f, 0.24f);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * 90f + 15f;
                        Vector3 p = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.11f, 0f, 0f);
                        CreatePart(root.transform, PrimitiveType.Cylinder, "Reed", p + new Vector3(0f, 0.3f, 0f), new Vector3(0.04f, 0.3f, 0.04f), new Vector3(0f, ang, 8f), stalk, baseMat);
                        CreatePart(root.transform, PrimitiveType.Sphere, "ReedTip", p + new Vector3(-0.06f, 0.6f, 0f), new Vector3(0.08f, 0.12f, 0.08f), Vector3.zero, tip, baseMat);
                    }
                    break;
                }
            }

            return root;
        }

        // =====================================================================
        // 5 BUSH VARIANTS
        // =====================================================================
        private static GameObject BuildBush(int variant, Transform parent, Vector3 pos, Material baseMat, bool snapToGrid)
        {
            string[] names = { "Bush_RoundGreen", "Bush_BerryShrub", "Bush_TrimmedHedge", "Bush_SpikyDesert", "Bush_WitheredAutumn" };
            GameObject root = CreateRoot(names[variant], parent, pos, snapToGrid);

            switch (variant)
            {
                case 0: // Round green bush
                {
                    Color c = new Color(0.20f, 0.46f, 0.18f);
                    Color c2 = new Color(0.25f, 0.54f, 0.22f);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Main", new Vector3(0f, 0.32f, 0f), new Vector3(0.75f, 0.62f, 0.75f), Vector3.zero, c, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "SideL", new Vector3(-0.26f, 0.24f, 0.12f), new Vector3(0.48f, 0.45f, 0.48f), Vector3.zero, c2, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "SideR", new Vector3(0.25f, 0.26f, -0.10f), new Vector3(0.45f, 0.42f, 0.45f), Vector3.zero, c2, baseMat);
                    break;
                }
                case 1: // Berry shrub
                {
                    Color leaf = new Color(0.18f, 0.44f, 0.20f);
                    Color berry = new Color(0.82f, 0.16f, 0.18f);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.34f, 0f), new Vector3(0.72f, 0.64f, 0.72f), Vector3.zero, leaf, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Side", new Vector3(0.22f, 0.26f, 0.18f), new Vector3(0.46f, 0.42f, 0.46f), Vector3.zero, leaf, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Berry1", new Vector3(-0.24f, 0.48f, 0.20f), Vector3.one * 0.10f, Vector3.zero, berry, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Berry2", new Vector3(0.18f, 0.52f, -0.18f), Vector3.one * 0.10f, Vector3.zero, berry, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Berry3", new Vector3(0.30f, 0.34f, 0.26f), Vector3.one * 0.09f, Vector3.zero, berry, baseMat);
                    break;
                }
                case 2: // Trimmed urban hedge
                {
                    Color c = new Color(0.18f, 0.42f, 0.19f);
                    CreatePart(root.transform, PrimitiveType.Cube, "HedgeBase", new Vector3(0f, 0.32f, 0f), new Vector3(0.95f, 0.64f, 0.52f), Vector3.zero, c, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "HedgeTop", new Vector3(0f, 0.66f, 0f), new Vector3(0.85f, 0.12f, 0.44f), Vector3.zero, new Color(0.24f, 0.50f, 0.23f), baseMat);
                    break;
                }
                case 3: // Spiky desert shrub
                {
                    Color c = new Color(0.42f, 0.48f, 0.22f);
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * 72f;
                        CreatePart(root.transform, PrimitiveType.Cube, "Spike", new Vector3(0f, 0.26f, 0f), new Vector3(0.22f, 0.58f, 0.22f), new Vector3(28f, ang, 18f), c, baseMat);
                    }
                    break;
                }
                default: // Withered autumn bush
                {
                    Color c = new Color(0.55f, 0.34f, 0.16f);
                    Color c2 = new Color(0.42f, 0.26f, 0.14f);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Withered1", new Vector3(0f, 0.28f, 0f), new Vector3(0.65f, 0.52f, 0.65f), Vector3.zero, c, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Withered2", new Vector3(-0.22f, 0.22f, -0.14f), new Vector3(0.44f, 0.38f, 0.44f), Vector3.zero, c2, baseMat);
                    break;
                }
            }

            return root;
        }

        // =====================================================================
        // 5 TREE VARIANTS
        // =====================================================================
        private static GameObject BuildTree(int variant, Transform parent, Vector3 pos, Material baseMat, bool snapToGrid)
        {
            string[] names = { "Tree_Pine", "Tree_Oak", "Tree_DeadTwisted", "Tree_UrbanSapling", "Tree_Birch" };
            GameObject root = CreateRoot(names[variant], parent, pos, snapToGrid);

            Color bark = new Color(0.36f, 0.24f, 0.14f);

            switch (variant)
            {
                case 0: // Pine tree (3 tiered layers)
                {
                    Color pine = new Color(0.14f, 0.36f, 0.18f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.55f, 0f), new Vector3(0.28f, 0.55f, 0.28f), Vector3.zero, bark, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cube, "Tier1", new Vector3(0f, 1.25f, 0f), new Vector3(1.25f, 0.55f, 1.25f), new Vector3(0f, 45f, 0f), pine, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Tier2", new Vector3(0f, 1.75f, 0f), new Vector3(0.95f, 0.52f, 0.95f), new Vector3(0f, 0f, 0f), pine * 1.1f, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Tier3", new Vector3(0f, 2.22f, 0f), new Vector3(0.58f, 0.50f, 0.58f), new Vector3(0f, 45f, 0f), pine * 1.2f, baseMat);
                    break;
                }
                case 1: // Oak tree (clustered canopy)
                {
                    Color oak = new Color(0.22f, 0.48f, 0.18f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.65f, 0f), new Vector3(0.36f, 0.65f, 0.36f), Vector3.zero, bark, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Sphere, "CanopyMain", new Vector3(0f, 1.65f, 0f), new Vector3(1.45f, 1.15f, 1.45f), Vector3.zero, oak, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "CanopyL", new Vector3(-0.45f, 1.45f, 0.25f), new Vector3(0.85f, 0.78f, 0.85f), Vector3.zero, oak * 1.08f, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "CanopyR", new Vector3(0.45f, 1.50f, -0.20f), new Vector3(0.82f, 0.75f, 0.82f), Vector3.zero, oak * 0.92f, baseMat);
                    break;
                }
                case 2: // Dead twisted tree
                {
                    Color deadBark = new Color(0.28f, 0.22f, 0.18f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.75f, 0f), new Vector3(0.30f, 0.75f, 0.30f), new Vector3(4f, 0f, -5f), deadBark, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "BranchL", new Vector3(-0.28f, 1.35f, 0.06f), new Vector3(0.12f, 0.45f, 0.12f), new Vector3(10f, 0f, 42f), deadBark, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "BranchR", new Vector3(0.26f, 1.25f, -0.08f), new Vector3(0.10f, 0.38f, 0.10f), new Vector3(-12f, 0f, -38f), deadBark, baseMat);
                    break;
                }
                case 3: // Urban sapling in planter
                {
                    Color concrete = new Color(0.55f, 0.55f, 0.53f);
                    Color leaf = new Color(0.26f, 0.56f, 0.22f);
                    CreatePart(root.transform, PrimitiveType.Cube, "Planter", new Vector3(0f, 0.15f, 0f), new Vector3(0.85f, 0.30f, 0.85f), Vector3.zero, concrete, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.70f, 0f), new Vector3(0.16f, 0.55f, 0.16f), Vector3.zero, bark, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Crown", new Vector3(0f, 1.45f, 0f), new Vector3(0.95f, 0.95f, 0.95f), Vector3.zero, leaf, baseMat);
                    break;
                }
                default: // Birch tree
                {
                    Color birchBark = new Color(0.82f, 0.80f, 0.76f);
                    Color birchLeaf = new Color(0.48f, 0.64f, 0.22f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.80f, 0f), new Vector3(0.24f, 0.80f, 0.24f), Vector3.zero, birchBark, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cube, "Stripe1", new Vector3(0f, 0.55f, 0f), new Vector3(0.26f, 0.08f, 0.26f), Vector3.zero, new Color(0.2f, 0.2f, 0.2f), baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Canopy1", new Vector3(0f, 1.75f, 0f), new Vector3(1.15f, 1.05f, 1.15f), Vector3.zero, birchLeaf, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Canopy2", new Vector3(0.30f, 1.95f, 0.15f), new Vector3(0.75f, 0.70f, 0.75f), Vector3.zero, birchLeaf * 1.1f, baseMat);
                    break;
                }
            }

            return root;
        }

        // =====================================================================
        // 10 DEBRIS & TRASH PROPS
        // =====================================================================
        private static GameObject BuildDebris(int variant, Transform parent, Vector3 pos, Material baseMat, bool snapToGrid)
        {
            string[] names =
            {
                "Prop_TrashCan", "Prop_Dumpster", "Prop_TrashBagPile", "Prop_BrokenCrate", "Prop_RubblePile",
                "Prop_TrafficCone", "Prop_RustyBarrel", "Prop_TireStack", "Prop_CardboardBoxPile", "Prop_ScrapMetalPile"
            };
            GameObject root = CreateRoot(names[variant], parent, pos, snapToGrid);

            switch (variant)
            {
                case 0: // Trash Can
                {
                    Color binColor = new Color(0.22f, 0.36f, 0.28f);
                    Color lidColor = new Color(0.18f, 0.20f, 0.22f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "BinBody", new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.35f, 0.45f), Vector3.zero, binColor, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "BinLid", new Vector3(0f, 0.72f, 0f), new Vector3(0.49f, 0.04f, 0.49f), Vector3.zero, lidColor, baseMat);
                    break;
                }
                case 1: // Dumpster
                {
                    Color body = new Color(0.18f, 0.38f, 0.26f);
                    Color lid = new Color(0.12f, 0.14f, 0.16f);
                    CreatePart(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.42f, 0f), new Vector3(1.10f, 0.84f, 0.72f), Vector3.zero, body, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cube, "Lid", new Vector3(0f, 0.86f, -0.04f), new Vector3(1.14f, 0.08f, 0.76f), new Vector3(-6f, 0f, 0f), lid, baseMat);
                    break;
                }
                case 2: // Trash Bag Pile
                {
                    Color bag = new Color(0.14f, 0.14f, 0.16f);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Bag1", new Vector3(-0.16f, 0.18f, 0.08f), new Vector3(0.42f, 0.36f, 0.42f), Vector3.zero, bag, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Bag2", new Vector3(0.18f, 0.16f, -0.06f), new Vector3(0.38f, 0.32f, 0.38f), Vector3.zero, bag * 1.15f, baseMat);
                    CreatePart(root.transform, PrimitiveType.Sphere, "Bag3", new Vector3(0.02f, 0.34f, 0.02f), new Vector3(0.32f, 0.28f, 0.32f), Vector3.zero, bag, baseMat);
                    break;
                }
                case 3: // Broken Crate
                {
                    Color wood = new Color(0.56f, 0.39f, 0.22f);
                    CreatePart(root.transform, PrimitiveType.Cube, "CrateBase", new Vector3(0f, 0.28f, 0f), new Vector3(0.62f, 0.56f, 0.62f), new Vector3(0f, 12f, 0f), wood, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cube, "BrokenPlank", new Vector3(0.18f, 0.45f, 0.16f), new Vector3(0.12f, 0.55f, 0.05f), new Vector3(22f, 15f, -35f), wood * 0.85f, baseMat);
                    break;
                }
                case 4: // Rubble Pile
                {
                    Color concrete = new Color(0.46f, 0.45f, 0.43f);
                    Color brick = new Color(0.55f, 0.30f, 0.24f);
                    CreatePart(root.transform, PrimitiveType.Cube, "Chunk1", new Vector3(-0.14f, 0.12f, 0.08f), new Vector3(0.36f, 0.24f, 0.30f), new Vector3(12f, 25f, -8f), concrete, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Chunk2", new Vector3(0.16f, 0.10f, -0.10f), new Vector3(0.30f, 0.20f, 0.26f), new Vector3(-15f, 40f, 10f), concrete * 0.9f, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Brick", new Vector3(0.02f, 0.22f, 0.04f), new Vector3(0.22f, 0.12f, 0.14f), new Vector3(8f, -20f, 18f), brick, baseMat);
                    break;
                }
                case 5: // Traffic Cone
                {
                    Color orange = new Color(0.92f, 0.36f, 0.08f);
                    Color white = new Color(0.92f, 0.92f, 0.92f);
                    CreatePart(root.transform, PrimitiveType.Cube, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.42f, 0.06f, 0.42f), Vector3.zero, orange, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Lower", new Vector3(0f, 0.18f, 0f), new Vector3(0.26f, 0.14f, 0.26f), Vector3.zero, orange, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Stripe", new Vector3(0f, 0.34f, 0f), new Vector3(0.20f, 0.06f, 0.20f), Vector3.zero, white, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Top", new Vector3(0f, 0.46f, 0f), new Vector3(0.13f, 0.08f, 0.13f), Vector3.zero, orange, baseMat);
                    break;
                }
                case 6: // Rusty Barrel
                {
                    Color rust = new Color(0.62f, 0.26f, 0.14f);
                    Color rim = new Color(0.32f, 0.22f, 0.18f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Drum", new Vector3(0f, 0.42f, 0f), new Vector3(0.48f, 0.42f, 0.48f), Vector3.zero, rust, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "RimTop", new Vector3(0f, 0.80f, 0f), new Vector3(0.51f, 0.03f, 0.51f), Vector3.zero, rim, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "RimBot", new Vector3(0f, 0.06f, 0f), new Vector3(0.51f, 0.03f, 0.51f), Vector3.zero, rim, baseMat);
                    break;
                }
                case 7: // Tire Stack
                {
                    Color rubber = new Color(0.16f, 0.16f, 0.18f);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Tire1", new Vector3(0f, 0.11f, 0f), new Vector3(0.58f, 0.11f, 0.58f), Vector3.zero, rubber, baseMat, keepCollider: true);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Tire2", new Vector3(0.04f, 0.32f, -0.02f), new Vector3(0.56f, 0.11f, 0.56f), new Vector3(0f, 15f, 4f), rubber * 1.1f, baseMat);
                    break;
                }
                case 8: // Cardboard Box Pile
                {
                    Color cardboard = new Color(0.66f, 0.50f, 0.32f);
                    CreatePart(root.transform, PrimitiveType.Cube, "Box1", new Vector3(-0.12f, 0.18f, 0.05f), new Vector3(0.44f, 0.36f, 0.40f), new Vector3(0f, 14f, 0f), cardboard, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Box2", new Vector3(0.18f, 0.14f, -0.08f), new Vector3(0.36f, 0.28f, 0.34f), new Vector3(0f, -18f, 0f), cardboard * 0.92f, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Box3", new Vector3(-0.08f, 0.46f, 0.04f), new Vector3(0.30f, 0.22f, 0.28f), new Vector3(0f, -8f, 0f), cardboard * 1.05f, baseMat);
                    break;
                }
                default: // Scrap Metal Pile
                {
                    Color steel = new Color(0.38f, 0.40f, 0.44f);
                    Color rust = new Color(0.50f, 0.30f, 0.20f);
                    CreatePart(root.transform, PrimitiveType.Cube, "Plate1", new Vector3(-0.08f, 0.04f, 0.06f), new Vector3(0.64f, 0.04f, 0.42f), new Vector3(6f, 22f, -4f), steel, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cube, "Plate2", new Vector3(0.12f, 0.08f, -0.06f), new Vector3(0.48f, 0.04f, 0.36f), new Vector3(-10f, -30f, 12f), rust, baseMat);
                    CreatePart(root.transform, PrimitiveType.Cylinder, "Pipe", new Vector3(0f, 0.12f, 0f), new Vector3(0.08f, 0.34f, 0.08f), new Vector3(84f, 35f, 0f), steel * 0.85f, baseMat);
                    break;
                }
            }

            return root;
        }

        // =====================================================================
        // 4 COMPACT HOUSE & BUILDING VARIANTS (fit 1x1 grid cells cleanly)
        // =====================================================================
        public static GameObject SpawnBuilding(Transform parent, Vector3 worldXZPos, bool snapToGrid, out Vector2 footprintXZ)
        {
            int variant = Random.Range(0, 4);
            string[] names = { "Building_CottageHouse", "Building_Townhouse", "Building_CornerShop", "Building_SmallApartment" };
            GameObject root = CreateRoot(names[variant], parent, worldXZPos, snapToGrid);

            BoxCollider col = root.AddComponent<BoxCollider>();
            Vector2 sizeXZ;

            switch (variant)
            {
                case 0: // Cottage House (warm cream walls + terracotta pitched roof)
                {
                    sizeXZ = new Vector2(2.6f, 2.4f);
                    Color wall = new Color(0.86f, 0.80f, 0.68f);
                    Color roof = new Color(0.72f, 0.30f, 0.22f);
                    Color door = new Color(0.45f, 0.28f, 0.16f);
                    Color win = new Color(0.45f, 0.72f, 0.88f);

                    CreatePart(root.transform, PrimitiveType.Cube, "Walls", new Vector3(0f, 1.0f, 0f), new Vector3(2.4f, 2.0f, 2.2f), Vector3.zero, wall, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "RoofL", new Vector3(-0.55f, 2.25f, 0f), new Vector3(1.55f, 0.18f, 2.45f), new Vector3(0f, 0f, 28f), roof, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "RoofR", new Vector3(0.55f, 2.25f, 0f), new Vector3(1.55f, 0.18f, 2.45f), new Vector3(0f, 0f, -28f), roof, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "Door", new Vector3(0f, 0.55f, 1.12f), new Vector3(0.55f, 1.1f, 0.08f), Vector3.zero, door, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "WinL", new Vector3(-0.70f, 1.15f, 1.12f), new Vector3(0.45f, 0.45f, 0.08f), Vector3.zero, win, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "WinR", new Vector3(0.70f, 1.15f, 1.12f), new Vector3(0.45f, 0.45f, 0.08f), Vector3.zero, win, null);
                    col.center = new Vector3(0f, 1.2f, 0f);
                    col.size = new Vector3(2.4f, 2.4f, 2.2f);
                    break;
                }
                case 1: // 2-Story Townhouse
                {
                    sizeXZ = new Vector2(2.5f, 2.5f);
                    Color wall = new Color(0.78f, 0.72f, 0.66f);
                    Color trim = new Color(0.42f, 0.46f, 0.52f);
                    Color win = new Color(0.50f, 0.75f, 0.90f);

                    CreatePart(root.transform, PrimitiveType.Cube, "Floor1", new Vector3(0f, 1.0f, 0f), new Vector3(2.3f, 2.0f, 2.3f), Vector3.zero, wall, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "Floor2", new Vector3(0f, 2.65f, 0f), new Vector3(2.2f, 1.3f, 2.2f), Vector3.zero, wall * 0.95f, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "RoofTrim", new Vector3(0f, 3.38f, 0f), new Vector3(2.4f, 0.18f, 2.4f), Vector3.zero, trim, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "Door", new Vector3(0f, 0.55f, 1.17f), new Vector3(0.6f, 1.1f, 0.08f), Vector3.zero, trim, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "WinUp1", new Vector3(-0.55f, 2.65f, 1.12f), new Vector3(0.45f, 0.55f, 0.08f), Vector3.zero, win, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "WinUp2", new Vector3(0.55f, 2.65f, 1.12f), new Vector3(0.45f, 0.55f, 0.08f), Vector3.zero, win, null);
                    col.center = new Vector3(0f, 1.7f, 0f);
                    col.size = new Vector3(2.3f, 3.4f, 2.3f);
                    break;
                }
                case 2: // Corner Shop with Awning
                {
                    sizeXZ = new Vector2(2.8f, 2.4f);
                    Color wall = new Color(0.84f, 0.84f, 0.80f);
                    Color awning = new Color(0.20f, 0.55f, 0.78f);
                    Color glass = new Color(0.58f, 0.82f, 0.94f);

                    CreatePart(root.transform, PrimitiveType.Cube, "ShopBody", new Vector3(0f, 1.25f, 0f), new Vector3(2.6f, 2.5f, 2.2f), Vector3.zero, wall, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "RoofCap", new Vector3(0f, 2.58f, 0f), new Vector3(2.75f, 0.20f, 2.35f), Vector3.zero, new Color(0.35f, 0.38f, 0.42f), null);
                    CreatePart(root.transform, PrimitiveType.Cube, "Awning", new Vector3(0f, 1.48f, 1.32f), new Vector3(2.4f, 0.14f, 0.65f), new Vector3(18f, 0f, 0f), awning, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "Storefront", new Vector3(0f, 0.75f, 1.12f), new Vector3(1.8f, 1.1f, 0.08f), Vector3.zero, glass, null);
                    col.center = new Vector3(0f, 1.3f, 0f);
                    col.size = new Vector3(2.6f, 2.6f, 2.2f);
                    break;
                }
                default: // Small Brick Apartment
                {
                    sizeXZ = new Vector2(2.6f, 2.6f);
                    Color brick = new Color(0.72f, 0.42f, 0.34f);
                    Color roof = new Color(0.32f, 0.34f, 0.38f);
                    Color win = new Color(0.55f, 0.78f, 0.88f);

                    CreatePart(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 1.9f, 0f), new Vector3(2.4f, 3.8f, 2.4f), Vector3.zero, brick, null);
                    CreatePart(root.transform, PrimitiveType.Cube, "Parapet", new Vector3(0f, 3.9f, 0f), new Vector3(2.55f, 0.24f, 2.55f), Vector3.zero, roof, null);
                    for (int floor = 0; floor < 2; floor++)
                    {
                        float wy = 1.4f + floor * 1.4f;
                        CreatePart(root.transform, PrimitiveType.Cube, "WinL", new Vector3(-0.6f, wy, 1.22f), new Vector3(0.46f, 0.56f, 0.06f), Vector3.zero, win, null);
                        CreatePart(root.transform, PrimitiveType.Cube, "WinR", new Vector3(0.6f, wy, 1.22f), new Vector3(0.46f, 0.56f, 0.06f), Vector3.zero, win, null);
                    }
                    col.center = new Vector3(0f, 1.95f, 0f);
                    col.size = new Vector3(2.4f, 3.9f, 2.4f);
                    break;
                }
            }

            footprintXZ = sizeXZ;
            return root;
        }
    }
}
