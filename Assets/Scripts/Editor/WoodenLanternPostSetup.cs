using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    [InitializeOnLoad]
    public static class WoodenLanternPostSetup
    {
        private const string ModelPath = "Assets/GeneratedModels/Environment/23_Wooden_Lantern_Post.obj";
        private const string PrefabPath = "Assets/Resources_Addressable/Prefabs/Environment/Craft/23_Wooden_Lantern_Post.prefab";
        private const string BuildingDataPath = "Assets/Resources_Addressable/Database/Buildings/BUILD0023_WoodenLanternPost.asset";
        private const string CraftingDbPath = "Assets/Resources_Addressable/Database/CraftingDatabase.asset";

        static WoodenLanternPostSetup()
        {
            EditorApplication.delayCall += EnsureLanternPostBuilt;
        }

        [MenuItem("Tools/The Last Empire/Setup Wooden Lantern Post Building", false, 115)]
        public static void EnsureLanternPostBuilt()
        {
            // 1. Ensure Model exists
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[WoodenLanternPostSetup] Model not found at '{ModelPath}'. Make sure OBJ is imported.");
                return;
            }

            // 2. Setup Prefab
            GameObject instance = Object.Instantiate(modelPrefab);
            instance.name = "23_Wooden_Lantern_Post";

            // Add BuildingObject
            BuildingObject bo = instance.GetComponent<BuildingObject>();
            if (bo == null) bo = instance.AddComponent<BuildingObject>();
            bo.AutoAddNavMeshObstacle = true;
            bo.CanBeDamagedByPlayer = false;

            // Add BoxCollider
            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 3.1f, 1.1f);
            col.center = new Vector3(0f, 1.55f, 0.42f);

            // Add Point Light child
            Transform existingLight = instance.transform.Find("Point Light");
            GameObject lightObj;
            if (existingLight == null)
            {
                lightObj = new GameObject("Point Light");
                lightObj.transform.SetParent(instance.transform, false);
            }
            else
            {
                lightObj = existingLight.gameObject;
            }
            lightObj.transform.localPosition = new Vector3(0f, 2.18f, 0.85f);

            Light ptLight = lightObj.GetComponent<Light>();
            if (ptLight == null) ptLight = lightObj.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = new Color(1.0f, 0.72f, 0.35f, 1.0f);
            ptLight.range = 12.0f;
            ptLight.intensity = 2.8f;
            ptLight.shadows = LightShadows.Soft;

            // Add WoodenLanternPostBuilding
            WoodenLanternPostBuilding lp = instance.GetComponent<WoodenLanternPostBuilding>();
            if (lp == null) lp = instance.AddComponent<WoodenLanternPostBuilding>();

            // Save Prefab
            string prefabDir = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);

            // 3. Setup BuildingData Asset
            BuildingData bData = AssetDatabase.LoadAssetAtPath<BuildingData>(BuildingDataPath);
            if (bData == null)
            {
                bData = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(bData, BuildingDataPath);
            }

            bData.buildingId = "BUILD0023_WoodenLanternPost";
            bData.buildingName = "Wooden Lantern Post";
            bData.category = CraftingCategory.Building;
            bData.description = "Rustic wooden lamp post with a hanging lantern. Casts a warm glowing perimeter that illuminates the dark and restores Sanity.";
            bData.prefab = savedPrefab;
            bData.footprintSize = new Vector3(1.0f, 3.1f, 1.2f);
            bData.buildRange = 6.5f;
            bData.maxHealth = 150f;
            bData.damageResistance = 0f;

            // Ingredients
            ItemData woodLog = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources_Addressable/Database/ItemData/ITEM0049_WoodLog.asset");
            ItemData plantFiber = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources_Addressable/Database/ItemData/ITEM0051_PlantFiber.asset");
            ItemData roughStone = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources_Addressable/Database/ItemData/ITEM0050_RoughStone.asset");
            ItemData charcoal = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources_Addressable/Database/ItemData/ITEM0060_CharcoalLump.asset");

            bData.ingredients = new List<CraftingIngredient>
            {
                new CraftingIngredient(woodLog, "Wood Log", 3),
                new CraftingIngredient(plantFiber, "Plant Fiber", 2),
                new CraftingIngredient(roughStone, "Rough Stone", 1),
                new CraftingIngredient(charcoal, "Charcoal Lump", 1)
            };

            EditorUtility.SetDirty(bData);

            // 4. Register in CraftingDatabase
            CraftingDatabase craftDb = AssetDatabase.LoadAssetAtPath<CraftingDatabase>(CraftingDbPath);
            if (craftDb != null)
            {
                CraftingRecipe existingRecipe = craftDb.recipes != null 
                    ? craftDb.recipes.Find(r => r != null && (r.recipeId == "CRAFT0076" || r.recipeName == "Wooden Lantern Post"))
                    : craftDb.GetRecipeById("CRAFT0076");

                if (existingRecipe == null)
                {
                    CraftingRecipe newRecipe = new CraftingRecipe
                    {
                        recipeId = "CRAFT0076",
                        recipeName = "Wooden Lantern Post",
                        category = CraftingCategory.Building,
                        resultItemName = "Wooden Lantern Post",
                        resultAmount = 1,
                        description = "Rustic wooden lamp post with a hanging lantern. Casts a warm glowing perimeter that illuminates the dark and restores Sanity.",
                        ingredients = new List<CraftingIngredient>
                        {
                            new CraftingIngredient(woodLog, "Wood Log", 3),
                            new CraftingIngredient(plantFiber, "Plant Fiber", 2),
                            new CraftingIngredient(roughStone, "Rough Stone", 1),
                            new CraftingIngredient(charcoal, "Charcoal Lump", 1)
                        }
                    };
                    craftDb.recipes.Add(newRecipe);
                    Debug.Log("[WoodenLanternPostSetup] Added 'Wooden Lantern Post' recipe (CRAFT0076) to CraftingDatabase.");
                }
                else
                {
                    existingRecipe.category = CraftingCategory.Building;
                    existingRecipe.resultItemName = "Wooden Lantern Post";
                    existingRecipe.resultAmount = 1;
                    existingRecipe.description = "Rustic wooden lamp post with a hanging lantern. Casts a warm glowing perimeter that illuminates the dark and restores Sanity.";
                    existingRecipe.ingredients = new List<CraftingIngredient>
                    {
                        new CraftingIngredient(woodLog, "Wood Log", 3),
                        new CraftingIngredient(plantFiber, "Plant Fiber", 2),
                        new CraftingIngredient(roughStone, "Rough Stone", 1),
                        new CraftingIngredient(charcoal, "Charcoal Lump", 1)
                    };
                }

                EditorUtility.SetDirty(craftDb);
            }

            // Also register in BuildingManager if present in scene
            BuildingManager bm = Object.FindFirstObjectByType<BuildingManager>();
            if (bm != null)
            {
                bm.RegisterBuildingData(bData);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WoodenLanternPostSetup] Setup completed successfully for Wooden Lantern Post!");
        }
    }
}
