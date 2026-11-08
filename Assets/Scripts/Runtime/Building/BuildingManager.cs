using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// BuildingManager — ผู้จัดการส่วนกลางของระบบสิ่งปลูกสร้างทั้งหมด
    /// ควบคุม:
    /// - ทะเบียน BuildingData ทั้งหมดในโปรเจกต์
    /// - การค้นหา BuildingData ตามสูตรคราฟต์หรือชื่อ
    /// - การติดตามสิ่งปลูกสร้างที่วางอยู่ในฉากทั้งหมด
    /// - AutoInitialize ให้ระบบพร้อมใช้งานเสมอ
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [Header("Building Database")]
        [Tooltip("รายการ BuildingData ทั้งหมดที่รองรับในเกม")]
        [SerializeField] private List<BuildingData> allBuildings = new List<BuildingData>();

        private readonly List<BuildingObject> _activeBuildings = new List<BuildingObject>();

        public IReadOnlyList<BuildingObject> ActiveBuildings => _activeBuildings;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureBuildingPlacerAttached();
            LoadDefaultBuildingDataIfEmpty();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("BuildingManager");
                Instance = obj.AddComponent<BuildingManager>();
                obj.AddComponent<BuildingPlacer>();
                DontDestroyOnLoad(obj);
            }
        }

        private void EnsureBuildingPlacerAttached()
        {
            if (GetComponent<BuildingPlacer>() == null)
            {
                gameObject.AddComponent<BuildingPlacer>();
            }
        }

        public void RegisterBuildingData(BuildingData data)
        {
            if (data != null && !allBuildings.Contains(data))
            {
                allBuildings.Add(data);
            }
        }

        public BuildingData GetBuildingDataByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (var b in allBuildings)
            {
                if (b == null) continue;
                if (string.Equals(b.buildingName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.buildingId, name, StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }
            return null;
        }

        public BuildingData GetBuildingDataForRecipe(CraftingRecipe recipe)
        {
            if (recipe == null) return null;

            // 1. ค้นหาตามชื่อตรงกัน หรือรหัสสูตรคราฟต์
            foreach (var b in allBuildings)
            {
                if (b == null) continue;
                if (string.Equals(b.buildingName, recipe.recipeName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.buildingId, recipe.recipeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.buildingName, recipe.ResolvedResultName, StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }

            // 2. ถ้าเป็นหมวดหมู่ Building หรือ Defense แต่ยังไม่มี BuildingData ให้สร้าง Fallback BuildingData อัตโนมัติทันที
            if (recipe.category == CraftingCategory.Building || recipe.category == CraftingCategory.Defense)
            {
                BuildingData fallback = CreateFallbackBuildingData(recipe);
                allBuildings.Add(fallback);
                return fallback;
            }

            return null;
        }

        private BuildingData CreateFallbackBuildingData(CraftingRecipe recipe)
        {
            BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
            data.buildingId = !string.IsNullOrEmpty(recipe.recipeId) ? recipe.recipeId : $"BUILD_{recipe.recipeName.Replace(" ", "")}";
            data.buildingName = recipe.recipeName;
            data.category = recipe.category;
            data.description = recipe.description;
            data.CopyFromRecipe(recipe);

            string rName = recipe.recipeName.ToLowerInvariant();
            Vector3 size = new Vector3(2f, 1.5f, 2f);
            if (rName.Contains("floor") || rName.Contains("foundation"))
            {
                size = new Vector3(3f, 0.25f, 3f);
            }
            else if (rName.Contains("wall") || rName.Contains("door") || rName.Contains("window"))
            {
                size = new Vector3(3f, 2.5f, 0.4f);
            }
            else if (rName.Contains("barricade"))
            {
                size = new Vector3(2.5f, 1.2f, 0.8f);
            }

            data.footprintSize = size;
            data.maxHealth = 300f;
            data.buildRange = 6.5f;

            GameObject fallbackObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackObj.name = $"Prefab_{data.buildingName}";
            fallbackObj.transform.localScale = size;
            fallbackObj.AddComponent<BuildingObject>();
            fallbackObj.SetActive(false);
            DontDestroyOnLoad(fallbackObj);

            data.prefab = fallbackObj;
            return data;
        }

        public bool HasBuildingForRecipe(CraftingRecipe recipe)
        {
            return GetBuildingDataForRecipe(recipe) != null;
        }

        public void TrackBuilding(BuildingObject building)
        {
            if (building != null && !_activeBuildings.Contains(building))
            {
                _activeBuildings.Add(building);
            }
        }

        public void UntrackBuilding(BuildingObject building)
        {
            if (building != null)
            {
                _activeBuildings.Remove(building);
            }
        }

        private void LoadDefaultBuildingDataIfEmpty()
        {
            if (allBuildings == null || allBuildings.Count == 0)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BuildingData");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    BuildingData bd = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                    if (bd != null && !allBuildings.Contains(bd))
                    {
                        allBuildings.Add(bd);
                    }
                }
#endif
                if (allBuildings.Count == 0)
                {
                    IList<BuildingData> addrList = AddressableManager.LoadAssetsByLabel<BuildingData>("BuildingData");
                    if (addrList != null && addrList.Count > 0)
                    {
                        allBuildings.AddRange(addrList);
                    }
                }

                if (allBuildings.Count == 0)
                {
                    BuildingData[] loaded = Resources.LoadAll<BuildingData>("");
                    if (loaded != null && loaded.Length > 0)
                    {
                        allBuildings.AddRange(loaded);
                    }
                }
            }
        }
    }
}

