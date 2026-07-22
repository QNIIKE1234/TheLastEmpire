using UnityEngine;

namespace TheLastEmpire
{
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

        [Header("Floor Mesh Renderers (Optional)")]
        [SerializeField] private MeshRenderer centerMeshRenderer;
        [SerializeField] private MeshRenderer northMeshRenderer;
        [SerializeField] private MeshRenderer southMeshRenderer;
        [SerializeField] private MeshRenderer eastMeshRenderer;
        [SerializeField] private MeshRenderer westMeshRenderer;

        private void Start()
        {
            // Auto-detect Center renderer if not manually assigned (default to the current GameObject's MeshRenderer)
            if (centerMeshRenderer == null)
            {
                centerMeshRenderer = GetComponent<MeshRenderer>() ?? FindMeshRendererByName("Center");
            }

            // Auto-detect neighbors if they are not manually assigned in Inspector
            if (northMeshRenderer == null) northMeshRenderer = FindMeshRendererByName("North");
            if (southMeshRenderer == null) southMeshRenderer = FindMeshRendererByName("South");
            if (eastMeshRenderer == null) eastMeshRenderer = FindMeshRendererByName("East");
            if (westMeshRenderer == null) westMeshRenderer = FindMeshRendererByName("West");

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += HandleStageChanged;
                // Initialize with current room biome
                HandleStageChanged(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            string materialsPath = "Assets/Materials";
            if (urbanRuinsMaterial == null) urbanRuinsMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/UrbanRuins.mat");
            if (highwaysMaterial == null) highwaysMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/Highways.mat");
            if (overgrownForestsMaterial == null) overgrownForestsMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/OvergrownForests.mat");
            if (highlandsMaterial == null) highlandsMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/Highlands.mat");
            if (waterwaysMaterial == null) waterwaysMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/Waterways.mat");
            if (suburbanVillagesMaterial == null) suburbanVillagesMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/SuburbanVillages.mat");
            if (specialEventMaterial == null) specialEventMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/SpecialEvent.mat");
            if (defaultMaterial == null) defaultMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{materialsPath}/Default.mat");
        }
#endif

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleStageChanged;
            }
        }

        private MeshRenderer FindMeshRendererByName(string nameKeyword)
        {
            // Look for siblings first
            if (transform.parent != null)
            {
                foreach (Transform child in transform.parent)
                {
                    if (child.name.ToLower().Contains(nameKeyword.ToLower()) && child.GetComponent<MeshRenderer>() != null)
                    {
                        return child.GetComponent<MeshRenderer>();
                    }
                }
            }

            // Look in the active scene root as fallback
            GameObject obj = GameObject.Find("Ground_" + nameKeyword) ?? 
                             GameObject.Find("Floor_" + nameKeyword) ??
                             GameObject.Find(nameKeyword);
            if (obj != null)
            {
                return obj.GetComponent<MeshRenderer>();
            }

            return null;
        }

        private void HandleStageChanged(int gridX, int gridY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            // Update main floor (Center)
            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(gridX, gridY);
            if (stage != null)
            {
                if (centerMeshRenderer != null)
                {
                    centerMeshRenderer.gameObject.SetActive(true);
                    ApplyBiomeMaterial(stage.biome, centerMeshRenderer);
                }
            }
            else
            {
                if (centerMeshRenderer != null)
                {
                    centerMeshRenderer.gameObject.SetActive(false);
                }
            }

            // Update neighboring floors (North: gridY + 1, South: gridY - 1, East: gridX + 1, West: gridX - 1)
            UpdateNeighborFloor(gridX, gridY + 1, northMeshRenderer);
            UpdateNeighborFloor(gridX, gridY - 1, southMeshRenderer);
            UpdateNeighborFloor(gridX + 1, gridY, eastMeshRenderer);
            UpdateNeighborFloor(gridX - 1, gridY, westMeshRenderer);
        }

        private void UpdateNeighborFloor(int x, int y, MeshRenderer neighborRenderer)
        {
            if (neighborRenderer == null) return;

            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            StageData neighborStage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
            if (neighborStage != null)
            {
                // Enable neighbor floor mesh and apply its correct biome material
                neighborRenderer.gameObject.SetActive(true);
                ApplyBiomeMaterial(neighborStage.biome, neighborRenderer);
            }
            else
            {
                // Disable neighbor floor mesh if there is no room in that direction
                neighborRenderer.gameObject.SetActive(false);
            }
        }

        public Material GetBiomeMaterial(BiomeType biome)
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

            if (targetMaterial == null)
            {
                targetMaterial = defaultMaterial;
            }

            return targetMaterial;
        }

        public void ApplyBiomeMaterial(BiomeType biome, MeshRenderer renderer)
        {
            if (renderer == null) return;
            Material mat = GetBiomeMaterial(biome);
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        public void ApplyBiomeMaterial(BiomeType biome)
        {
            ApplyBiomeMaterial(biome, centerMeshRenderer);
        }
    }
}
