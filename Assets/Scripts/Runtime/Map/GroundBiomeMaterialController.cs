using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(MeshRenderer))]
    public class GroundBiomeMaterialController : MonoBehaviour
    {
        [Header("Biome Materials")]
        [SerializeField] private Material urbanRuinsMaterial;
        [SerializeField] private Material highwaysMaterial;
        [SerializeField] private Material overgrownForestsMaterial;
        [SerializeField] private Material highlandsMaterial;
        [SerializeField] private Material waterwaysMaterial;
        [SerializeField] private Material suburbanVillagesMaterial;
        [SerializeField] private Material specialEventMaterial;
        [SerializeField] private Material defaultMaterial;

        private MeshRenderer _meshRenderer;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Start()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += HandleStageChanged;
                // Initialize with current room biome
                HandleStageChanged(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
        }

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleStageChanged;
            }
        }

        private void HandleStageChanged(int gridX, int gridY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(gridX, gridY);
            if (stage == null) return;

            ApplyBiomeMaterial(stage.biome);
        }

        public void ApplyBiomeMaterial(BiomeType biome)
        {
            Material targetMaterial = defaultMaterial;

            switch (biome)
            {
                case BiomeType.UrbanRuins:
                    targetMaterial = urbanRuinsMaterial;
                    break;
                case BiomeType.Highways:
                    targetMaterial = highwaysMaterial;
                    break;
                case BiomeType.OvergrownForests:
                    targetMaterial = overgrownForestsMaterial;
                    break;
                case BiomeType.Highlands:
                    targetMaterial = highlandsMaterial;
                    break;
                case BiomeType.Waterways:
                    targetMaterial = waterwaysMaterial;
                    break;
                case BiomeType.SuburbanVillages:
                    targetMaterial = suburbanVillagesMaterial;
                    break;
                case BiomeType.SpecialEvent:
                    targetMaterial = specialEventMaterial;
                    break;
            }

            // Fallback if the specific biome material is not assigned in the inspector
            if (targetMaterial == null)
            {
                targetMaterial = defaultMaterial;
            }

            if (targetMaterial != null && _meshRenderer != null)
            {
                _meshRenderer.sharedMaterial = targetMaterial;
            }
        }
    }
}
