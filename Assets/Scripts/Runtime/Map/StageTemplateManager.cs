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
        [Tooltip("Probability (0.0 to 1.0) of selecting an authored template when available for the current Biome. (e.g., 0.5 = 50% template, 50% procedural).")]
        [SerializeField] private float templateSelectionChance = 0.50f;

        private readonly Dictionary<BiomeType, List<StageTemplateData>> _templatesByBiome = new Dictionary<BiomeType, List<StageTemplateData>>();
        private readonly Dictionary<string, StageTemplateData> _templatesById = new Dictionary<string, StageTemplateData>(StringComparer.OrdinalIgnoreCase);
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

            TextAsset[] templateAssets = Resources.LoadAll<TextAsset>("Stages");
            if (templateAssets == null || templateAssets.Length == 0)
            {
                Debug.Log("[StageTemplateManager] No JSON templates found under Resources/Stages/.");
                _isInitialized = true;
                return;
            }

            int count = 0;
            foreach (TextAsset asset in templateAssets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.text)) continue;

                StageTemplateData data = StageTemplateData.FromJson(asset.text);
                if (data != null && !string.IsNullOrEmpty(data.templateId))
                {
                    _templatesById[data.templateId] = data;

                    if (!_templatesByBiome.ContainsKey(data.biome))
                    {
                        _templatesByBiome[data.biome] = new List<StageTemplateData>();
                    }
                    _templatesByBiome[data.biome].Add(data);
                    count++;
                }
            }

            Debug.Log($"[StageTemplateManager] Successfully loaded {count} stage templates across {_templatesByBiome.Count} biomes.");
            _isInitialized = true;
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

            // 1. If this stage was already assigned a template or explicitly marked procedural
            if (!string.IsNullOrEmpty(stage.assignedTemplateId))
            {
                if (stage.assignedTemplateId == "__PROCEDURAL__" || stage.assignedTemplateId == "__NONE__")
                {
                    return false;
                }
                if (_templatesById.TryGetValue(stage.assignedTemplateId, out template))
                {
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
