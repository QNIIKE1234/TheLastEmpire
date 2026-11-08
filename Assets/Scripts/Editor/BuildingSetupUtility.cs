using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// BuildingSetupUtility — สร้าง Prefab และ ScriptableObject เริ่มต้น 3 รายการ:
    /// 1. Survivor Campfire (กองไฟ + แสง + ออร่าฮีลสติ)
    /// 2. Wooden Spike Barricade (รั้วไม้หนาม + สะท้อนดาเมจ + กั้น NavMesh)
    /// 3. Wooden Storage Crate (กล่องเก็บของ + LootContainer)
    /// พร้อมเชื่อมโยงกับสูตรใน CraftingDatabase อัตโนมัติ
    /// </summary>
    public static class BuildingSetupUtility
    {
        private const string PrefabFolder = "Assets/Resources_Addressable/Prefabs/Building";
        private const string DatabaseFolder = "Assets/Resources_Addressable/Database/Buildings";

        [InitializeOnLoadMethod]
        [MenuItem("Tools/The Last Empire/Setup Starter Building Prefabs and Data", false, 110)]
        public static void SetupStarterBuildings()
        {
            EnsureDirectories();

            // 1. Setup Campfire (Both Building and Environment prefabs)
            GameObject campfirePrefab = SetupCampfirePrefab();
            SetupEnvironmentCampfirePrefab();
            BuildingData campfireData = SetupBuildingData("BUILD0001_Campfire", "Survivor Campfire", CraftingCategory.Utility, 
                campfirePrefab, new Vector3(1.5f, 1.2f, 1.5f), 150f, 6.5f, "Placeable campfire with cooking pot. Restores Sanity when sitting nearby.");

            // 2. Setup Barricade
            GameObject barricadePrefab = SetupBarricadePrefab();
            BuildingData barricadeData = SetupBuildingData("BUILD0002_WoodenSpikeBarricade", "Wooden Spike Barricade", CraftingCategory.Defense, 
                barricadePrefab, new Vector3(2.5f, 1.2f, 0.8f), 400f, 6.0f, "Heavy defensive barricade blocking enemy pathways and reflecting thorns damage.");

            // 3. Setup Storage Crate
            GameObject cratePrefab = SetupStorageCratePrefab();
            BuildingData crateData = SetupBuildingData("BUILD0003_WoodenStorageCrate", "Wooden Storage Crate", CraftingCategory.Utility, 
                cratePrefab, new Vector3(1.2f, 1.0f, 1.2f), 300f, 6.0f, "Reinforced storage chest for storing extra weapons, ammo, and crafting materials.");
            crateData.storageSlots = 24;

            // Sync with CraftingDatabase recipes
            SyncWithCraftingDatabase(campfireData, barricadeData, crateData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildingSetupUtility] Successfully configured all 3 Starter Building Prefabs and BuildingData assets!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabFolder)) Directory.CreateDirectory(PrefabFolder);
            if (!Directory.Exists(DatabaseFolder)) Directory.CreateDirectory(DatabaseFolder);
        }

        private static GameObject SetupCampfirePrefab()
        {
            string targetPath = $"{PrefabFolder}/Building_Campfire.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            GameObject root;

            if (existing != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(existing);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            else
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources_Addressable/Prefabs/Environment/Apoc_Prop_Campfire.prefab");
                if (source != null)
                {
                    root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                else
                {
                    root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    root.transform.localScale = new Vector3(1.2f, 0.3f, 1.2f);
                }
            }

            root.name = "Building_Campfire";

            BuildingObject bo = root.GetComponent<BuildingObject>();
            if (bo == null) bo = root.AddComponent<BuildingObject>();

            CampfireBuilding cb = root.GetComponent<CampfireBuilding>();
            if (cb == null) cb = root.AddComponent<CampfireBuilding>();

            // Ensure collider exists with correct local center (Local Z is World UP!)
            Collider col = root.GetComponent<Collider>();
            if (col == null)
            {
                SphereCollider sc = root.AddComponent<SphereCollider>();
                sc.radius = 0.8f;
                sc.center = new Vector3(0f, 0f, 0.4f);
            }
            else if (col is SphereCollider existingSc)
            {
                existingSc.center = new Vector3(0f, 0f, 0.4f);
                existingSc.radius = 0.8f;
                existingSc.isTrigger = false;
            }

            // Ensure Point Light exists directly inside the Prefab hierarchy at the flame center
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

            // Local coordinates: root is rotated -90 on X, so Local Z is World UP!
            lightObj.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            lightObj.transform.localRotation = Quaternion.identity;

            Light fireLight = lightObj.GetComponent<Light>();
            if (fireLight == null) fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.62f, 0.22f, 1f); // Warm flame orange
            fireLight.range = 10.0f;
            fireLight.intensity = 3.0f;
            fireLight.shadows = LightShadows.Soft;

            // Link fireLight directly on CampfireBuilding component in the prefab
            SerializedObject so = new SerializedObject(cb);
            SerializedProperty lightProp = so.FindProperty("fireLight");
            if (lightProp != null)
            {
                lightProp.objectReferenceValue = fireLight;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            GameObject.DestroyImmediate(root);
            return savedPrefab;
        }

        private static void SetupEnvironmentCampfirePrefab()
        {
            string targetPath = "Assets/Resources_Addressable/Prefabs/Environment/Apoc_Prop_Campfire.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (existing == null) return;

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(existing);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

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

            lightObj.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            lightObj.transform.localRotation = Quaternion.identity;

            Light fireLight = lightObj.GetComponent<Light>();
            if (fireLight == null) fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.62f, 0.22f, 1f);
            fireLight.range = 10.0f;
            fireLight.intensity = 3.0f;
            fireLight.shadows = LightShadows.Soft;

            PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            GameObject.DestroyImmediate(root);
        }

        private static GameObject SetupBarricadePrefab()
        {
            string targetPath = $"{PrefabFolder}/Building_Barricade.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (existing != null) return existing;

            // Base source: Apoc_Prop_Concrete_Barricade
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources_Addressable/Prefabs/Environment/Building/Apoc_Prop_Concrete_Barricade.prefab");
            GameObject root;
            if (source != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                root.transform.localScale = new Vector3(2.5f, 1.2f, 0.6f);
            }

            root.name = "Building_Barricade";

            if (root.GetComponent<BuildingObject>() == null)
            {
                root.AddComponent<BuildingObject>();
            }

            if (root.GetComponent<SpikeBarricadeBuilding>() == null)
            {
                root.AddComponent<SpikeBarricadeBuilding>();
            }

            // Ensure box collider exists for physical blocking
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(2.5f, 1.2f, 0.8f);
                box.center = new Vector3(0, 0.6f, 0);
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            GameObject.DestroyImmediate(root);
            return savedPrefab;
        }

        private static GameObject SetupStorageCratePrefab()
        {
            string targetPath = $"{PrefabFolder}/Building_StorageCrate.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            if (existing != null) return existing;

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Building_StorageCrate";
            root.transform.localScale = new Vector3(1.2f, 0.9f, 1.0f);

            // Add BuildingObject
            if (root.GetComponent<BuildingObject>() == null)
            {
                root.AddComponent<BuildingObject>();
            }

            // Add LootContainer
            LootContainer container = root.GetComponent<LootContainer>();
            if (container == null)
            {
                container = root.AddComponent<LootContainer>();
            }
            container.containerName = "Survivor Storage Crate";
            container.maxStorageSlots = 24;
            container.randomizeLoot = false;
            container.interactionRadius = 2.5f;

            // Load wood texture if possible
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/BreadCrate_Texture.png") ??
                            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AmmoCrate_Texture.png");
            if (tex != null)
            {
                Renderer r = root.GetComponent<Renderer>();
                if (r != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.mainTexture = tex;
                    r.material = mat;
                }
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            GameObject.DestroyImmediate(root);
            return savedPrefab;
        }

        private static BuildingData SetupBuildingData(string id, string name, CraftingCategory category, GameObject prefab, Vector3 footprint, float maxHp, float range, string desc)
        {
            string assetPath = $"{DatabaseFolder}/{id}.asset";
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.buildingId = id;
            data.buildingName = name;
            data.category = category;
            data.prefab = prefab;
            data.footprintSize = footprint;
            data.maxHealth = maxHp;
            data.buildRange = range;
            data.description = desc;

            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SyncWithCraftingDatabase(BuildingData campfire, BuildingData barricade, BuildingData crate)
        {
            CraftingDatabase db = AssetDatabase.LoadAssetAtPath<CraftingDatabase>("Assets/Resources_Addressable/Database/CraftingDatabase.asset");
            if (db == null || db.recipes == null) return;

            foreach (var recipe in db.recipes)
            {
                if (recipe == null) continue;

                if (recipe.recipeName.Contains("Campfire") && campfire != null)
                {
                    campfire.CopyFromRecipe(recipe);
                    campfire.buildingName = recipe.recipeName;
                    EditorUtility.SetDirty(campfire);
                }
                else if (recipe.recipeName.Contains("Barricade") && barricade != null)
                {
                    barricade.CopyFromRecipe(recipe);
                    barricade.buildingName = recipe.recipeName;
                    EditorUtility.SetDirty(barricade);
                }
                else if (recipe.recipeName.Contains("Storage Crate") && crate != null)
                {
                    crate.CopyFromRecipe(recipe);
                    crate.buildingName = recipe.recipeName;
                    EditorUtility.SetDirty(crate);
                }
            }
        }
    }
}
