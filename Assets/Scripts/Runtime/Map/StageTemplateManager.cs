using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages handcrafted Stage Templates loaded from JSON (under Resources/Stages/).
    /// Supports a hybrid workflow: automatically rolls whether to generate a stage procedurally
    /// or load an authored template matching the stage's BiomeType.
    /// </summary>
    public class StageTemplateManager : MonoBehaviour
    {
        public static StageTemplateManager Instance { get; private set; }

        [Header("Template Settings")]
        [Tooltip("If enabled, authored JSON templates will be rolled and applied during stage transitions.")]
        [SerializeField] private bool enableTemplates = true;

        [Range(0f, 1f)]
        [Tooltip("Probability (0.0 to 1.0) of selecting an authored template when available for the current Biome. (e.g., 0.65 = 65% template, 35% procedural).")]
        [SerializeField] private float templateSelectionChance = 0.65f;

        private readonly Dictionary<BiomeType, List<StageTemplateData>> _templatesByBiome = new Dictionary<BiomeType, List<StageTemplateData>>();
        private readonly Dictionary<string, StageTemplateData> _templatesById = new Dictionary<string, StageTemplateData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<StageTemplateData>> _districts = new Dictionary<string, List<StageTemplateData>>(StringComparer.OrdinalIgnoreCase);
        private int _lastStampedSeed = int.MinValue;
        private bool _isInitialized = false;

        public bool EnableTemplates
        {
            get => enableTemplates;
            set => enableTemplates = value;
        }

        public float TemplateSelectionChance
        {
            get => templateSelectionChance;
            set => templateSelectionChance = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitializeTemplates();
        }

        public void InitializeTemplates()
        {
            _templatesByBiome.Clear();
            _templatesById.Clear();
            _districts.Clear();

            List<TextAsset> combinedAssets = new List<TextAsset>();

            IList<TextAsset> addressableAssets = AddressableManager.LoadAssetsByLabel<TextAsset>("Stage");
            if (addressableAssets != null)
            {
                for (int i = 0; i < addressableAssets.Count; i++)
                {
                    if (addressableAssets[i] != null) combinedAssets.Add(addressableAssets[i]);
                }
            }

            // Always also load JSONs from Assets/Resources/Stages/ (used by StageTemplateDesignerWindow and runtime)
            TextAsset[] resourceAssets = Resources.LoadAll<TextAsset>("Stages");
            if (resourceAssets != null)
            {
                for (int i = 0; i < resourceAssets.Length; i++)
                {
                    if (resourceAssets[i] != null && !combinedAssets.Contains(resourceAssets[i]))
                    {
                        combinedAssets.Add(resourceAssets[i]);
                    }
                }
            }

            if (combinedAssets.Count == 0)
            {
                Debug.Log("[StageTemplateManager] No JSON templates found under Addressables (label 'Stage') or Resources/Stages/.");
                _isInitialized = true;
                return;
            }

            int count = 0;
            foreach (TextAsset asset in combinedAssets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.text)) continue;

                StageTemplateData data = StageTemplateData.FromJson(asset.text);
                if (data != null && !string.IsNullOrEmpty(data.templateId))
                {
                    if (_templatesById.ContainsKey(data.templateId))
                    {
                        continue; // Avoid duplicates if loaded from both Addressables and Resources
                    }

                    _templatesById[data.templateId] = data;

                    if (!string.IsNullOrEmpty(data.districtId))
                    {
                        if (!_districts.ContainsKey(data.districtId))
                        {
                            _districts[data.districtId] = new List<StageTemplateData>();
                        }
                        _districts[data.districtId].Add(data);
                    }
                    else
                    {
                        if (!_templatesByBiome.ContainsKey(data.biome))
                        {
                            _templatesByBiome[data.biome] = new List<StageTemplateData>();
                        }
                        _templatesByBiome[data.biome].Add(data);
                    }
                    count++;
                }
            }

            Debug.Log($"[StageTemplateManager] Successfully loaded {count} stage templates ({_districts.Count} multi-stage 3x3 districts, {_templatesByBiome.Count} biomes).");
            _isInitialized = true;
        }

        /// <summary>
        /// Stamps coherent 3x3 City/Landmark Districts across walkable regions of the 128x128 WorldMapGenerator grid
        /// (plus guarantees one iconic 3x3 American Landmark zone within walking distance of the player's starting stage).
        /// </summary>
        public void StampDistrictsOntoWorldMap(WorldMapGenerator mapGen, bool force = false)
        {
            if (mapGen == null || mapGen.gridData == null) return;
            if (!_isInitialized) InitializeTemplates();
            if (_districts.Count == 0) return;
            if (!force && _lastStampedSeed == mapGen.seed) return;

            _lastStampedSeed = mapGen.seed;
            System.Random rand = new System.Random(mapGen.seed + 31337);
            HashSet<long> reservedCoords = new HashSet<long>();

            // Separate City 3x3 Districts from Event-only 3x3 Camp Districts
            List<string> cityDistrictKeys = new List<string>();
            List<string> eventDistrictKeys = new List<string>();
            foreach (string key in _districts.Keys)
            {
                if (key.StartsWith("District_EVT", StringComparison.OrdinalIgnoreCase))
                    eventDistrictKeys.Add(key);
                else
                    cityDistrictKeys.Add(key);
            }

            cityDistrictKeys.Sort((a, b) =>
            {
                bool aLiberty = a.Contains("Liberty") || a.Contains("NYC");
                bool bLiberty = b.Contains("Liberty") || b.Contains("NYC");
                if (aLiberty != bLiberty) return aLiberty ? -1 : 1;
                bool aUS = a.StartsWith("District_US", StringComparison.OrdinalIgnoreCase);
                bool bUS = b.StartsWith("District_US", StringComparison.OrdinalIgnoreCase);
                if (aUS != bUS) return aUS ? -1 : 1;
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            eventDistrictKeys.Sort((a, b) =>
            {
                bool aSurv = a.Contains("SurvivorCamp");
                bool bSurv = b.Contains("SurvivorCamp");
                if (aSurv != bSurv) return aSurv ? -1 : 1;
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            bool IsFootprintValid(int centerX, int centerY)
            {
                if (centerX < 2 || centerX >= WorldMapGenerator.GridSize - 2 ||
                    centerY < 2 || centerY >= WorldMapGenerator.GridSize - 2)
                {
                    return false;
                }

                int waterCount = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int tx = centerX + dx;
                        int ty = centerY + dy;
                        if (tx == mapGen.spawnX && ty == mapGen.spawnY) return false; // Never overwrite safe spawn tile

                        long key = ((long)tx << 16) | (uint)ty;
                        if (reservedCoords.Contains(key)) return false;

                        StageData st = mapGen.GetStage(tx, ty);
                        if (st == null || st.biome == BiomeType.SpecialEvent) return false;
                        if (st.biome == BiomeType.Waterways) waterCount++;
                    }
                }
                return waterCount <= 3;
            }

            void ApplyDistrictStamp(int centerX, int centerY, List<StageTemplateData> tiles)
            {
                foreach (StageTemplateData tpl in tiles)
                {
                    int tx = centerX + tpl.districtOffsetX;
                    int ty = centerY + tpl.districtOffsetY;
                    StageData st = mapGen.GetStage(tx, ty);
                    if (st != null)
                    {
                        st.assignedTemplateId = tpl.templateId;
                        st.biome = tpl.biome;
                        long key = ((long)tx << 16) | (uint)ty;
                        reservedCoords.Add(key);
                    }
                }
            }

            bool TryPlaceDistrictInRing(string districtKey, int minDist, int maxDist)
            {
                for (int dist = minDist; dist <= maxDist; dist++)
                {
                    List<Vector2Int> candidates = new List<Vector2Int>();
                    for (int dy = -dist; dy <= dist; dy++)
                    {
                        for (int dx = -dist; dx <= dist; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != dist) continue;
                            candidates.Add(new Vector2Int(mapGen.spawnX + dx, mapGen.spawnY + dy));
                        }
                    }

                    for (int i = candidates.Count - 1; i > 0; i--)
                    {
                        int j = rand.Next(0, i + 1);
                        Vector2Int tmp = candidates[i];
                        candidates[i] = candidates[j];
                        candidates[j] = tmp;
                    }

                    foreach (Vector2Int c in candidates)
                    {
                        if (IsFootprintValid(c.x, c.y))
                        {
                            ApplyDistrictStamp(c.x, c.y, _districts[districtKey]);
                            Debug.Log($"[StageTemplateManager] Placed 3x3 district '{districtKey}' at ({c.x}, {c.y}) near spawn ({mapGen.spawnX}, {mapGen.spawnY}).");
                            return true;
                        }
                    }
                }
                return false;
            }

            int stampedCount = 0;

            // 1a. Place flagship US 3x3 City District (Statue of Liberty / NYC) 3..12 tiles from spawn
            if (cityDistrictKeys.Count > 0 && TryPlaceDistrictInRing(cityDistrictKeys[0], 3, 12))
            {
                stampedCount++;
            }

            // 1b. Place 1 Event-only 3x3 Survivor Camp District 6..18 tiles from spawn
            if (eventDistrictKeys.Count > 0 && TryPlaceDistrictInRing(eventDistrictKeys[0], 6, 18))
            {
                stampedCount++;
            }

            // 2. Stamp 3x3 City & Event Camp Districts across 16x16 macro-sectors over the entire 128x128 World Map
            const int sectorSize = 16;
            int sectorCount = WorldMapGenerator.GridSize / sectorSize;
            int cityRoundRobin = 1;
            int eventRoundRobin = 0;

            for (int sy = 0; sy < sectorCount; sy++)
            {
                for (int sx = 0; sx < sectorCount; sx++)
                {
                    if (rand.NextDouble() > 0.65) continue;

                    // ~25% of stamped districts are Event 3x3 Camp Districts, ~75% are City 3x3 Districts
                    string chosenKey;
                    if (eventDistrictKeys.Count > 0 && (cityDistrictKeys.Count == 0 || rand.NextDouble() < 0.28))
                    {
                        chosenKey = eventDistrictKeys[eventRoundRobin % eventDistrictKeys.Count];
                        eventRoundRobin++;
                    }
                    else
                    {
                        chosenKey = cityDistrictKeys[cityRoundRobin % cityDistrictKeys.Count];
                        cityRoundRobin++;
                    }

                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        int cx = sx * sectorSize + rand.Next(3, sectorSize - 3);
                        int cy = sy * sectorSize + rand.Next(3, sectorSize - 3);
                        if (IsFootprintValid(cx, cy))
                        {
                            ApplyDistrictStamp(cx, cy, _districts[chosenKey]);
                            stampedCount++;
                            break;
                        }
                    }
                }
            }

            Debug.Log($"[StageTemplateManager] Stamped {stampedCount} connected 3x3 city & event camp districts across the 128x128 world map.");
        }

        /// <summary>
        /// Attempts to get or assign a StageTemplateData for the given StageData.
        /// Uses deterministic pseudo-random roll based on stage.stageSeed so that visiting
        /// the same room coordinates consistently spawns the same layout.
        /// </summary>
        public bool TryGetTemplateForStage(StageData stage, out StageTemplateData template)
        {
            template = null;
            if (!enableTemplates || stage == null) return false;

            if (!_isInitialized)
            {
                InitializeTemplates();
            }

            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                StampDistrictsOntoWorldMap(WorldMapManager.Instance.MapGenerator);
            }

            // 1. If this stage was already assigned a template or explicitly marked procedural
            if (!string.IsNullOrEmpty(stage.assignedTemplateId))
            {
                if (stage.assignedTemplateId == "__PROCEDURAL__" || stage.assignedTemplateId == "__NONE__")
                {
                    return false;
                }
                if (_templatesById.TryGetValue(stage.assignedTemplateId, out template))
                {
                    // Safety rule: Survivor Camps with NPCs are strictly restricted to SpecialEvent stages
                    if (template.npcSpawns != null && template.npcSpawns.Count > 0 && stage.biome != BiomeType.SpecialEvent)
                    {
                        return false;
                    }
                    return true;
                }
                return false;
            }

            // 2. Check if any templates exist for this stage's biome
            if (!_templatesByBiome.TryGetValue(stage.biome, out List<StageTemplateData> biomeList) || biomeList == null || biomeList.Count == 0)
            {
                stage.assignedTemplateId = "__NONE__";
                return false;
            }

            // 3. Deterministic roll using stage.stageSeed + 7192
            System.Random rand = new System.Random(stage.stageSeed + 7192);
            double roll = rand.NextDouble();

            if (roll > templateSelectionChance)
            {
                // Decided to use standard procedural generation
                stage.assignedTemplateId = "__PROCEDURAL__";
                return false;
            }

            // 4. Weighted selection among available templates for this biome
            float totalWeight = 0f;
            foreach (var t in biomeList)
            {
                totalWeight += Mathf.Max(0.01f, t.selectionWeight);
            }

            double randomWeight = rand.NextDouble() * totalWeight;
            float currentSum = 0f;

            foreach (var t in biomeList)
            {
                currentSum += Mathf.Max(0.01f, t.selectionWeight);
                if (randomWeight <= currentSum)
                {
                    template = t;
                    break;
                }
            }

            if (template == null)
            {
                template = biomeList[0];
            }

            stage.assignedTemplateId = template.templateId;
            Debug.Log($"[StageTemplateManager] Stage ({stage.x}, {stage.y}) assigned template: '{template.templateName}' (ID: {template.templateId})");
            return true;
        }

        public StageTemplateData GetTemplateById(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;
            if (!_isInitialized) InitializeTemplates();
            _templatesById.TryGetValue(templateId, out StageTemplateData template);
            return template;
        }

        public List<StageTemplateData> GetTemplatesForBiome(BiomeType biome)
        {
            if (!_isInitialized) InitializeTemplates();
            if (_templatesByBiome.TryGetValue(biome, out List<StageTemplateData> list))
            {
                return new List<StageTemplateData>(list);
            }
            return new List<StageTemplateData>();
        }

        public List<StageTemplateData> GetAllTemplates()
        {
            if (!_isInitialized) InitializeTemplates();
            return new List<StageTemplateData>(_templatesById.Values);
        }
    }
}
