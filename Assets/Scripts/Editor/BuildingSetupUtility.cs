#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// BuildingSetupUtility — กำหนดค่าและเชื่อมโยงโมเดลสิ่งปลูกสร้างทั้ง 22 รายการ
    /// ที่สร้างไว้ใน Assets/Resources_Addressable/Prefabs/Environment/Craft/
    /// เข้ากับ BuildingData, BuildingObject, Colliders, Storage, และ CraftingDatabase อัตโนมัติ
    /// </summary>
    public static class BuildingSetupUtility
    {
        private const string CraftPrefabFolder = "Assets/Resources_Addressable/Prefabs/Environment/Craft";
        private const string BuildingPrefabFolder = "Assets/Resources_Addressable/Prefabs/Building";
        private const string DatabaseFolder = "Assets/Resources_Addressable/Database/Buildings";

        public struct CraftBuildConfig
        {
            public string id;
            public string recipeId;
            public string name;
            public string prefabName;
            public CraftingCategory category;
            public Vector3 footprint; // X = width, Y = height, Z = depth
            public float maxHp;
            public float damageResistance;
            public float buildRange;
            public int storageSlots;
            public bool isWalkableFloor;
            public bool isTrapTrigger;
            public string desc;

            public CraftBuildConfig(string id, string recipeId, string name, string prefabName, CraftingCategory category,
                Vector3 footprint, float maxHp, float damageResistance, float buildRange, int storageSlots, 
                bool isWalkableFloor, bool isTrapTrigger, string desc)
            {
                this.id = id;
                this.recipeId = recipeId;
                this.name = name;
                this.prefabName = prefabName;
                this.category = category;
                this.footprint = footprint;
                this.maxHp = maxHp;
                this.damageResistance = damageResistance;
                this.buildRange = buildRange;
                this.storageSlots = storageSlots;
                this.isWalkableFloor = isWalkableFloor;
                this.isTrapTrigger = isTrapTrigger;
                this.desc = desc;
            }
        }

        private static readonly CraftBuildConfig[] AllConfigs = new CraftBuildConfig[]
        {
            // --- หมวดที่ 1: สิ่งปลูกสร้างและโครงสร้าง (Building) ---
            new CraftBuildConfig("BUILD0001_WoodenFloorFoundation", "CRAFT0001", "Wooden Floor Foundation", "01_Wooden_Floor_Foundation.prefab",
                CraftingCategory.Building, new Vector3(3.0f, 0.25f, 3.0f), 200f, 0f, 8.0f, 0, true, false, "Basic wooden floor foundation for building survivor shelters."),
            
            new CraftBuildConfig("BUILD0002_WoodenWallPanel", "CRAFT0002", "Wooden Wall Panel", "02_Wooden_Wall_Panel.prefab",
                CraftingCategory.Building, new Vector3(3.0f, 2.5f, 0.4f), 300f, 0f, 7.0f, 0, false, false, "Sturdy timber wall panel reinforced with nails and plant fiber."),
            
            new CraftBuildConfig("BUILD0003_WoodenWindowWall", "CRAFT0003", "Wooden Window Wall", "03_Wooden_Window_Wall.prefab",
                CraftingCategory.Building, new Vector3(3.0f, 2.5f, 0.4f), 250f, 0f, 7.0f, 0, false, false, "Wooden wall section with a salvaged glass window slot for firing and visibility."),
            
            new CraftBuildConfig("BUILD0004_ReinforcedWoodenDoor", "CRAFT0004", "Reinforced Wooden Door", "04_Reinforced_Wooden_Door.prefab",
                CraftingCategory.Building, new Vector3(2.0f, 2.5f, 0.4f), 350f, 0f, 7.0f, 0, false, false, "Lockable survivor door with spring hinges and scrap metal plating."),
            
            new CraftBuildConfig("BUILD0005_StoneAndBrickWall", "CRAFT0005", "Stone & Brick Wall", "05_Stone_and_Brick_Wall.prefab",
                CraftingCategory.Building, new Vector3(3.0f, 2.5f, 0.5f), 600f, 0.15f, 7.0f, 0, false, false, "Heavy masonry wall built from rough stone, ceramic shards, and resin mortar."),
            
            new CraftBuildConfig("BUILD0006_ScrapMetalWall", "CRAFT0006", "Scrap Metal Wall", "06_Scrap_Metal_Wall.prefab",
                CraftingCategory.Building, new Vector3(3.0f, 2.5f, 0.4f), 500f, 0.10f, 7.0f, 0, false, false, "Corrugated scrap metal wall braced with steel pipes."),
            
            new CraftBuildConfig("BUILD0007_ArmoredTitaniumWall", "CRAFT0007", "Armored Titanium Wall", "07_Armored_Titanium_Wall.prefab",
                CraftingCategory.Building, new Vector3(3.0f, 2.5f, 0.5f), 1200f, 0.40f, 7.0f, 0, false, false, "Military-grade titanium bunker wall capable of withstanding boss attacks."),
            
            new CraftBuildConfig("BUILD0008_MotorizedBlastDoor", "CRAFT0008", "Motorized Blast Door", "08_Motorized_Blast_Door.prefab",
                CraftingCategory.Building, new Vector3(2.5f, 2.5f, 0.5f), 1000f, 0.35f, 7.0f, 0, false, false, "Automated heavy bunker door powered by overdrive servos and logic chips."),

            // --- หมวดที่ 2: สาธารณูปโภคและแคมป์ (Utility) ---
            new CraftBuildConfig("BUILD0009_SurvivorCampfire", "CRAFT0009", "Survivor Campfire", "09_Survivor_Campfire.prefab",
                CraftingCategory.Utility, new Vector3(1.5f, 1.2f, 1.5f), 150f, 0f, 6.5f, 20, false, false, "Placeable campfire with cooking pot. Restores Sanity when sitting nearby (Press Z)."),
            
            new CraftBuildConfig("BUILD0010_SurvivorWorkbench", "CRAFT0010", "Survivor Workbench", "10_Survivor_Workbench.prefab",
                CraftingCategory.Utility, new Vector3(2.0f, 1.2f, 1.2f), 250f, 0f, 6.0f, 26, false, false, "Crafting station equipped with tools for assembling weapons and base structures."),
            
            new CraftBuildConfig("BUILD0011_WoodenStorageCrate", "CRAFT0011", "Wooden Storage Crate", "11_Wooden_Storage_Crate.prefab",
                CraftingCategory.Utility, new Vector3(1.2f, 1.0f, 1.2f), 300f, 0f, 6.0f, 24, false, false, "Reinforced wooden chest for storing extra weapons, ammo, and crafting materials."),
            
            new CraftBuildConfig("BUILD0012_WaterPurifierStation", "CRAFT0012", "Water Purifier Station", "12_Water_Purifier_Station.prefab",
                CraftingCategory.Utility, new Vector3(1.5f, 2.0f, 1.5f), 250f, 0f, 6.0f, 22, false, false, "Filtration rig that distills contaminated water into clean drinking water."),
            
            new CraftBuildConfig("BUILD0013_SurvivorSleepingBedroll", "CRAFT0013", "Survivor Sleeping Bedroll", "13_Survivor_Sleeping_Bedroll.prefab",
                CraftingCategory.Utility, new Vector3(1.2f, 0.4f, 2.2f), 150f, 0f, 6.0f, 18, false, false, "Insulated leather and cloth bedroll for resting and setting your respawn point."),
            
            new CraftBuildConfig("BUILD0014_SanityBeaconLantern", "CRAFT0014", "Sanity Beacon Lantern", "14_Sanity_Beacon_Lantern.prefab",
                CraftingCategory.Utility, new Vector3(1.0f, 2.2f, 1.0f), 200f, 0f, 6.5f, 0, false, false, "Warm high-power camp lamp that wards off darkness and restores Sanity in a wide zone."),
            
            new CraftBuildConfig("BUILD0015_CompactFusionGenerator", "CRAFT0015", "Compact Fusion Generator", "15_Compact_Fusion_Generator.prefab",
                CraftingCategory.Utility, new Vector3(2.0f, 2.2f, 2.0f), 500f, 0.20f, 6.0f, 30, false, false, "High-output reactor station that powers base defenses and advanced workbenches."),

            // --- หมวดที่ 3: แนวป้องกันและกับดัก (Defense) ---
            new CraftBuildConfig("BUILD0016_WoodenSpikeBarricade", "CRAFT0016", "Wooden Spike Barricade", "16_Wooden_Spike_Barricade.prefab",
                CraftingCategory.Defense, new Vector3(2.5f, 1.2f, 0.8f), 400f, 0f, 6.0f, 0, false, false, "Defensive timber barricade lined with sharpened bone claws to impale zombies."),
            
            new CraftBuildConfig("BUILD0017_RazorWireBarricade", "CRAFT0017", "Razor Wire Barricade", "17_Razor_Wire_Barricade.prefab",
                CraftingCategory.Defense, new Vector3(2.5f, 1.0f, 1.0f), 350f, 0f, 6.0f, 0, false, false, "Entangling copper and steel wire barrier studded with infected fangs."),
            
            new CraftBuildConfig("BUILD0018_ChitinCarapaceWall", "CRAFT0018", "Chitin Carapace Wall", "18_Chitin_Carapace_Wall.prefab",
                CraftingCategory.Defense, new Vector3(3.0f, 2.5f, 0.6f), 800f, 0.25f, 7.0f, 0, false, false, "Acid-resistant defensive wall plated with alien chitin and hardened spine segments."),
            
            new CraftBuildConfig("BUILD0019_SnapJawBearTrap", "CRAFT0019", "Snap-Jaw Bear Trap", "19_Snap_Jaw_Bear_Trap.prefab",
                CraftingCategory.Defense, new Vector3(1.2f, 0.3f, 1.2f), 150f, 0f, 6.0f, 0, false, true, "Heavy spring-loaded floor trap that immobilizes and bleeds approaching enemies."),
            
            new CraftBuildConfig("BUILD0020_CorrosiveBileLandmine", "CRAFT0020", "Corrosive Bile Landmine", "20_Corrosive_Bile_Landmine.prefab",
                CraftingCategory.Defense, new Vector3(1.0f, 0.3f, 1.0f), 100f, 0f, 6.0f, 0, false, true, "Proximity mine packed with toxic bile and alien acid blood."),
            
            new CraftBuildConfig("BUILD0021_AutomatedSentryTurret", "CRAFT0021", "Automated Sentry Turret", "21_Automated_Sentry_Turret.prefab",
                CraftingCategory.Defense, new Vector3(1.5f, 2.0f, 1.5f), 450f, 0.15f, 6.0f, 28, false, false, "AI-controlled perimeter turret that automatically targets hostile enemies."),
            
            new CraftBuildConfig("BUILD0022_XenocrystalShockTower", "CRAFT0022", "Xenocrystal Shock Tower", "22_Xenocrystal_Shock_Tower.prefab",
                CraftingCategory.Defense, new Vector3(1.5f, 3.0f, 1.5f), 600f, 0.20f, 6.0f, 0, false, false, "High-voltage alien defense pylon that chains bio-plasma lightning across hordes.")
        };

        [InitializeOnLoadMethod]
        [MenuItem("Tools/The Last Empire/Setup All 22 Craft Buildings and Data", false, 110)]
        public static void SetupAllBuildings()
        {
            EnsureDirectories();

            const string iconSheetPath = "Assets/Resources_Addressable/Image/UI/BUILDING1TO22.png";
            
            // ตรวจสอบและตัดรูป BUILDING1TO22.png
            SliceAndLinkBuildingSprites.ProcessBackgroundTransparency();
            SliceAndLinkBuildingSprites.SliceSpriteSheet();

            UnityEngine.Object[] spriteAssets = AssetDatabase.LoadAllAssetsAtPath(iconSheetPath);
            Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (spriteAssets != null)
            {
                foreach (var obj in spriteAssets)
                {
                    if (obj is Sprite s)
                    {
                        spriteMap[s.name] = s;
                    }
                }
            }

            CraftingDatabase db = AssetDatabase.LoadAssetAtPath<CraftingDatabase>("Assets/Resources_Addressable/Database/CraftingDatabase.asset");

            List<BuildingData> createdBuildings = new List<BuildingData>();

            for (int i = 0; i < AllConfigs.Length; i++)
            {
                CraftBuildConfig cfg = AllConfigs[i];
                GameObject configuredPrefab = SetupCraftBuildingPrefab(cfg);
                if (configuredPrefab != null)
                {
                    BuildingData bData = SetupBuildingData(cfg, configuredPrefab, db, spriteMap);
                    if (bData != null)
                    {
                        createdBuildings.Add(bData);
                    }
                }
            }

            // Also configure standalone campfire prefabs so inspector view is identical everywhere
            SetupCampfirePrefabAtPath("Assets/Resources_Addressable/Prefabs/Environment/Apoc_Prop_Campfire.prefab");
            SetupCampfirePrefabAtPath("Assets/Resources_Addressable/Prefabs/Environment/Craft/Apoc_Prop_Campfire.prefab");
            SetupCampfirePrefabAtPath("Assets/Resources_Addressable/Prefabs/Building/Building_Campfire.prefab");

            // Sync legacy assets so existing scenes/references maintain compatibility
            SyncLegacyBuildingData(createdBuildings);

            if (db != null)
            {
                EditorUtility.SetDirty(db);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuildingSetupUtility] Successfully configured and referenced ALL {createdBuildings.Count} Craft Building Prefabs and BuildingData assets with icons!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(CraftPrefabFolder)) Directory.CreateDirectory(CraftPrefabFolder);
            if (!Directory.Exists(BuildingPrefabFolder)) Directory.CreateDirectory(BuildingPrefabFolder);
            if (!Directory.Exists(DatabaseFolder)) Directory.CreateDirectory(DatabaseFolder);
        }

        public static GameObject SetupCraftBuildingPrefab(CraftBuildConfig cfg)
        {
            string prefabPath = $"{CraftPrefabFolder}/{cfg.prefabName}";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
            {
                Debug.LogWarning($"[BuildingSetupUtility] Craft prefab not found: {prefabPath}");
                return null;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 1. BuildingObject component
            BuildingObject bo = root.GetComponent<BuildingObject>();
            if (bo == null) bo = root.AddComponent<BuildingObject>();
            bo.AutoAddNavMeshObstacle = !cfg.isWalkableFloor && !cfg.isTrapTrigger;
            bo.CanBeDamagedByPlayer = false;

            // 2. BoxCollider
            // Root has localRotation X = -90 (Blender FBX axis convention)
            // Local X = World X (width), Local Y = World -Z (depth), Local Z = World UP (height)
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();

            box.size = new Vector3(cfg.footprint.x, cfg.footprint.z, cfg.footprint.y);
            box.center = new Vector3(0f, 0f, cfg.footprint.y * 0.5f);
            box.isTrigger = cfg.isTrapTrigger;

            // 3. Special components
            if (cfg.name.Contains("Campfire"))
            {
                ConfigureCampfireOnGameObject(root);
            }
            else if (cfg.name.Contains("Lantern"))
            {
                Transform lightTr = root.transform.Find("Point Light");
                GameObject lightObj;
                if (lightTr == null)
                {
                    lightObj = new GameObject("Point Light");
                    lightObj.transform.SetParent(root.transform, false);
                }
                else
                {
                    lightObj = lightTr.gameObject;
                }

                lightObj.transform.localPosition = new Vector3(0f, 0f, 1.5f);
                Light lanternLight = lightObj.GetComponent<Light>();
                if (lanternLight == null) lanternLight = lightObj.AddComponent<Light>();
                lanternLight.type = LightType.Point;
                lanternLight.color = new Color(0.35f, 0.85f, 1.0f, 1f); // Calm blue-cyan beacon
                lanternLight.range = 12.0f;
                lanternLight.intensity = 2.5f;
                lanternLight.shadows = LightShadows.Soft;
            }
            else if (cfg.name.Contains("Barricade"))
            {
                SpikeBarricadeBuilding spike = root.GetComponent<SpikeBarricadeBuilding>();
                if (spike == null) spike = root.AddComponent<SpikeBarricadeBuilding>();
            }

            if (cfg.storageSlots > 0)
            {
                LootContainer container = root.GetComponent<LootContainer>();
                if (container == null) container = root.AddComponent<LootContainer>();
                container.containerName = cfg.name;
                container.maxStorageSlots = cfg.storageSlots;
                container.randomizeLoot = false;
                container.interactionRadius = 2.5f;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);
            return savedPrefab;
        }

        private static void ConfigureCampfireOnGameObject(GameObject root)
        {
            CampfireBuilding cb = root.GetComponent<CampfireBuilding>();
            if (cb == null) cb = root.AddComponent<CampfireBuilding>();

            BuildingObject bo = root.GetComponent<BuildingObject>();
            if (bo == null) bo = root.AddComponent<BuildingObject>();
            bo.AutoAddNavMeshObstacle = true;
            bo.CanBeDamagedByPlayer = false;

            Transform lightTr = root.transform.Find("Point Light");
            if (lightTr == null) lightTr = root.transform.Find("Campfire_PointLight");
            GameObject lightObj;
            if (lightTr == null)
            {
                lightObj = new GameObject("Point Light");
                lightObj.transform.SetParent(root.transform, false);
            }
            else
            {
                lightObj = lightTr.gameObject;
                lightObj.name = "Point Light";
            }

            // Find flame mesh in children to get exact geometric center
            Renderer fireRenderer = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t != root.transform && t != lightObj.transform)
                {
                    if (t.name.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("Flame", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Renderer r = t.GetComponent<Renderer>();
                        if (r != null)
                        {
                            fireRenderer = r;
                            break;
                        }
                    }
                }
            }

            if (fireRenderer != null)
            {
                lightObj.transform.position = fireRenderer.bounds.center;
            }
            else
            {
                // Fallback: 0.45m above the campfire base in world up direction
                lightObj.transform.position = root.transform.position + Vector3.up * 0.45f;
            }

            Light fireLight = lightObj.GetComponent<Light>();
            if (fireLight == null) fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.62f, 0.22f, 1f);
            fireLight.range = 10.0f;
            fireLight.intensity = 3.0f;
            fireLight.shadows = LightShadows.Soft;

            cb.FireLight = fireLight;
        }

        private static void SetupCampfirePrefabAtPath(string path)
        {
            if (!File.Exists(path)) return;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return;

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            ConfigureCampfireOnGameObject(root);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            GameObject.DestroyImmediate(root);
        }

        private static BuildingData SetupBuildingData(CraftBuildConfig cfg, GameObject prefab, CraftingDatabase db, Dictionary<string, Sprite> spriteMap)
        {
            string assetPath = $"{DatabaseFolder}/{cfg.id}.asset";
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.buildingId = cfg.id;
            data.buildingName = cfg.name;
            data.category = cfg.category;
            data.prefab = prefab;
            data.footprintSize = cfg.footprint;
            data.maxHealth = cfg.maxHp;
            data.damageResistance = cfg.damageResistance;
            data.buildRange = cfg.buildRange;
            data.storageSlots = cfg.storageSlots;
            data.description = cfg.desc;

            if (spriteMap != null && spriteMap.TryGetValue(cfg.id, out Sprite iconSprite))
            {
                data.icon = iconSprite;
            }

            // Sync from recipe
            if (db != null && db.recipes != null)
            {
                CraftingRecipe recipe = db.recipes.Find(r => r != null && 
                    (r.recipeName.Equals(cfg.name, StringComparison.OrdinalIgnoreCase) ||
                     r.recipeId.Equals(cfg.recipeId, StringComparison.OrdinalIgnoreCase)));
                if (recipe != null)
                {
                    data.CopyFromRecipe(recipe);
                    data.buildingName = cfg.name;
                    data.prefab = prefab;
                    data.footprintSize = cfg.footprint;
                    data.storageSlots = cfg.storageSlots;

                    if (spriteMap != null && spriteMap.TryGetValue(cfg.id, out Sprite spr))
                    {
                        data.icon = spr;
                        recipe.icon = spr;
                        recipe.resultItem = null; // Clear out accidental Wood Log pointer
                    }
                }
            }

            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SyncLegacyBuildingData(List<BuildingData> createdBuildings)
        {
            BuildingData campfire = createdBuildings.Find(b => b.buildingName == "Survivor Campfire");
            BuildingData barricade = createdBuildings.Find(b => b.buildingName == "Wooden Spike Barricade");
            BuildingData crate = createdBuildings.Find(b => b.buildingName == "Wooden Storage Crate");

            if (campfire != null)
            {
                BuildingData legacyCamp = AssetDatabase.LoadAssetAtPath<BuildingData>($"{DatabaseFolder}/BUILD0001_Campfire.asset");
                if (legacyCamp != null)
                {
                    legacyCamp.prefab = campfire.prefab;
                    legacyCamp.storageSlots = campfire.storageSlots;
                    legacyCamp.icon = campfire.icon;
                    EditorUtility.SetDirty(legacyCamp);
                }
            }

            if (barricade != null)
            {
                BuildingData legacyBarr = AssetDatabase.LoadAssetAtPath<BuildingData>($"{DatabaseFolder}/BUILD0002_WoodenSpikeBarricade.asset");
                if (legacyBarr != null)
                {
                    legacyBarr.prefab = barricade.prefab;
                    legacyBarr.icon = barricade.icon;
                    EditorUtility.SetDirty(legacyBarr);
                }
            }

            if (crate != null)
            {
                BuildingData legacyCrate = AssetDatabase.LoadAssetAtPath<BuildingData>($"{DatabaseFolder}/BUILD0003_WoodenStorageCrate.asset");
                if (legacyCrate != null)
                {
                    legacyCrate.prefab = crate.prefab;
                    legacyCrate.storageSlots = crate.storageSlots;
                    legacyCrate.icon = crate.icon;
                    EditorUtility.SetDirty(legacyCrate);
                }
            }
        }
    }
}
#endif
