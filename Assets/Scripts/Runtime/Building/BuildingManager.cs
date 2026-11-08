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

            // ค้นหาตามชื่อตรงกัน หรือรหัสสูตรคราฟต์
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
            return null;
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
                BuildingData[] loaded = Resources.LoadAll<BuildingData>("");
                if (loaded != null && loaded.Length > 0)
                {
                    allBuildings.AddRange(loaded);
                }
            }
        }
    }
}

