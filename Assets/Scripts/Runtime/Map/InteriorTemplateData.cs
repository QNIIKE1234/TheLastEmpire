using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// JSON-serializable multi-floor building interior pattern loaded from Resources/Interiors/*.json.
    /// Edit any JSON file in Assets/Resources/Interiors/ to customize floor count (1-4), room sizes,
    /// wall colors, stair positions, Building prefabs, lootable containers, and enemy spawn points.
    /// </summary>
    [Serializable]
    public class InteriorTemplateData
    {
        public string templateId = "INT_01_Default";
        public string displayName = "Abandoned House";
        public string theme = "House"; // House, CornerShop, Clinic, Warehouse
        public int floorCount = 2;
        public List<string> matchingBuildingKeywords = new List<string>();
        public float ambushChance = 0.55f;
        public List<InteriorFloorData> floors = new List<InteriorFloorData>();

        public InteriorTheme GetThemeEnum()
        {
            if (Enum.TryParse(theme, true, out InteriorTheme parsed))
            {
                return parsed;
            }
            return InteriorTheme.House;
        }

        public InteriorFloorData GetFloor(int floorIndex)
        {
            if (floors == null || floors.Count == 0) return null;
            for (int i = 0; i < floors.Count; i++)
            {
                if (floors[i] != null && floors[i].floorIndex == floorIndex)
                {
                    return floors[i];
                }
            }
            int clamped = Mathf.Clamp(floorIndex - 1, 0, floors.Count - 1);
            return floors[clamped];
        }
    }

    [Serializable]
    public class InteriorFloorData
    {
        public int floorIndex = 1;
        public string floorName = "Ground Floor";
        public float width = 16f;
        public float depth = 11f;
        public float wallHeight = 3.0f;
        public string floorColorHex = "#6B4A30";
        public string wallColorHex = "#C2B8A1";
        public string trimColorHex = "#38291F";
        public string accentColorHex = "#9E3D38";

        public float exitDoorX = 0f;

        public bool hasStairsUp = false;
        public float stairsUpX = 5.5f;
        public float stairsUpZ = 3.5f;

        public bool hasStairsDown = false;
        public float stairsDownX = -5.5f;
        public float stairsDownZ = -3.2f;

        public List<InteriorPlacedObjectData> objects = new List<InteriorPlacedObjectData>();
        public List<InteriorEnemySpawnData> enemySpawnPoints = new List<InteriorEnemySpawnData>();
    }

    [Serializable]
    public class InteriorPlacedObjectData
    {
        /// <summary>
        /// Existing BuildingData ID/name (e.g. BUILD0011_WoodenStorageCrate, BUILD0010_SurvivorWorkbench,
        /// BUILD0013_SurvivorSleepingBedroll, BUILD0002_WoodenWallPanel, BUILD0005_StoneAndBrickWall,
        /// BUILD0006_ScrapMetalWall, BUILD0012_WaterPurifierStation, BUILD0015_CompactFusionGenerator,
        /// BUILD0023_WoodenLanternPost, BUILD0014_SanityBeaconLantern, BUILD0009_SurvivorCampfire, etc.)
        /// OR an Environment/Building prefab name.
        /// </summary>
        public string buildingId = "BUILD0011_WoodenStorageCrate";
        public float x = 0f;
        public float y = 0f;
        public float z = 0f;
        public float rotY = 0f;
        public float scale = 1f;
        public bool isLootable = false;
        public string containerName = "";
    }

    [Serializable]
    public class InteriorEnemySpawnData
    {
        public float x = 0f;
        public float z = 0f;
        public string preferredEnemy = ""; // Optional: ZombieNormal, ZombieRunner, ZombieCrawler, ZombieSpitter
    }

    /// <summary>
    /// Loads and caches all interior pattern JSONs from Assets/Resources/Interiors/.
    /// Deterministically selects the best matching multi-floor template based on the exterior building model and seed.
    /// </summary>
    public static class InteriorTemplateLibrary
    {
        private static List<InteriorTemplateData> _cachedTemplates;
        private static bool _loaded = false;

        public static IReadOnlyList<InteriorTemplateData> GetAllTemplates()
        {
            if (!_loaded)
            {
                ReloadTemplates();
            }
            return _cachedTemplates;
        }

        public static void ReloadTemplates()
        {
            _cachedTemplates = new List<InteriorTemplateData>();
            _loaded = true;

            TextAsset[] assets = Resources.LoadAll<TextAsset>("Interiors");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[InteriorTemplateLibrary] No JSON templates found in Resources/Interiors.");
                return;
            }

            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset ta = assets[i];
                if (ta == null || string.IsNullOrWhiteSpace(ta.text)) continue;

                try
                {
                    InteriorTemplateData data = JsonUtility.FromJson<InteriorTemplateData>(ta.text);
                    if (data != null && data.floors != null && data.floors.Count > 0)
                    {
                        if (string.IsNullOrWhiteSpace(data.templateId)) data.templateId = ta.name;
                        data.floorCount = Mathf.Clamp(data.floors.Count, 1, 4);
                        _cachedTemplates.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InteriorTemplateLibrary] Failed to parse '{ta.name}.json': {ex.Message}");
                }
            }

            Debug.Log($"[InteriorTemplateLibrary] Loaded {_cachedTemplates.Count} interior JSON templates from Resources/Interiors.");
        }

        /// <summary>
        /// Deterministically selects an interior JSON pattern matching the exterior building model & height.
        /// </summary>
        public static InteriorTemplateData SelectTemplateForBuilding(string prefabName, float buildingHeight, int interiorSeed)
        {
            var all = GetAllTemplates();
            if (all == null || all.Count == 0) return null;

            string lowerName = (prefabName ?? "").ToLowerInvariant();
            List<InteriorTemplateData> keywordMatches = new List<InteriorTemplateData>();

            for (int i = 0; i < all.Count; i++)
            {
                InteriorTemplateData tpl = all[i];
                if (tpl.matchingBuildingKeywords == null) continue;
                for (int k = 0; k < tpl.matchingBuildingKeywords.Count; k++)
                {
                    string kw = tpl.matchingBuildingKeywords[k];
                    if (!string.IsNullOrEmpty(kw) && lowerName.Contains(kw.ToLowerInvariant()))
                    {
                        keywordMatches.Add(tpl);
                        break;
                    }
                }
            }

            if (keywordMatches.Count > 0)
            {
                int idx = Mathf.Abs(interiorSeed) % keywordMatches.Count;
                return keywordMatches[idx];
            }

            // Fallback: match by estimated floor count from exterior 3D model height
            int desiredMinFloors = buildingHeight >= 9.0f ? 3 : 2;
            int desiredMaxFloors = buildingHeight >= 9.0f ? 4 : (buildingHeight >= 5.5f ? 3 : 2);

            List<InteriorTemplateData> heightMatches = new List<InteriorTemplateData>();
            for (int i = 0; i < all.Count; i++)
            {
                int fc = all[i].floorCount;
                if (fc >= desiredMinFloors && fc <= desiredMaxFloors)
                {
                    heightMatches.Add(all[i]);
                }
            }

            if (heightMatches.Count > 0)
            {
                int idx = Mathf.Abs(interiorSeed) % heightMatches.Count;
                return heightMatches[idx];
            }

            return all[Mathf.Abs(interiorSeed) % all.Count];
        }
    }
}
