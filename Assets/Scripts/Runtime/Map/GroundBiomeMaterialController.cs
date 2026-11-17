using UnityEngine;

namespace TheLastEmpire
{
    public class GroundBiomeMaterialController : MonoBehaviour
    {
        public static GroundBiomeMaterialController Instance { get; private set; }

        [Header("Biome Materials")]
        [SerializeField] private Material urbanRuinsMaterial;
        [SerializeField] private Material highwaysMaterial;
        [SerializeField] private Material overgrownForestsMaterial;
        [SerializeField] private Material highlandsMaterial;
        [SerializeField] private Material waterwaysMaterial;
        [SerializeField] private Material suburbanVillagesMaterial;
        [SerializeField] private Material specialEventMaterial;
        [SerializeField] private Material defaultMaterial;

        [Header("Floor Mesh Renderers")]
        [SerializeField] private MeshRenderer centerMeshRenderer;
        [SerializeField] private MeshRenderer northMeshRenderer;
        [SerializeField] private MeshRenderer southMeshRenderer;
        [SerializeField] private MeshRenderer eastMeshRenderer;
        [SerializeField] private MeshRenderer westMeshRenderer;

        [Header("Corner Floor Mesh Renderers")]
        [SerializeField] private MeshRenderer northEastMeshRenderer;
        [SerializeField] private MeshRenderer northWestMeshRenderer;
        [SerializeField] private MeshRenderer southEastMeshRenderer;
        [SerializeField] private MeshRenderer southWestMeshRenderer;

        [Header("Display Settings")]
        [Tooltip("If true, disables neighbor floor meshes when outside the map bounds. If false, blends them with the current room's biome to prevent a black void.")]
        [SerializeField] private bool hideMissingNeighbors = false;
        [Tooltip("World-space width of the soft ground transition where neighboring biomes differ.")]
        [SerializeField, Min(0f)] private float biomeTransitionWidth = 3f;

        private static readonly int BiomeEdgeBlendId = Shader.PropertyToID("_BiomeEdgeBlend");
        private static readonly int GroundBoundsId = Shader.PropertyToID("_GroundBounds");
        private static readonly int BiomeFadeWidthId = Shader.PropertyToID("_BiomeFadeWidth");
        private static readonly int BiomeNeighborTilingId = Shader.PropertyToID("_BiomeNeighborTiling");
        private static readonly int BiomeNeighborSaturationId = Shader.PropertyToID("_BiomeNeighborSaturation");
        private static readonly int[] NeighborBiomeMapIds =
        {
            Shader.PropertyToID("_NorthBiomeMap"),
            Shader.PropertyToID("_EastBiomeMap"),
            Shader.PropertyToID("_SouthBiomeMap"),
            Shader.PropertyToID("_WestBiomeMap")
        };
        private static readonly int[] NeighborBiomeColorIds =
        {
            Shader.PropertyToID("_NorthBiomeColor"),
            Shader.PropertyToID("_EastBiomeColor"),
            Shader.PropertyToID("_SouthBiomeColor"),
            Shader.PropertyToID("_WestBiomeColor")
        };
        private MaterialPropertyBlock groundPropertyBlock;

        private MaterialPropertyBlock GetGroundPropertyBlock()
        {
            if (groundPropertyBlock == null)
            {
                groundPropertyBlock = new MaterialPropertyBlock();
            }
            return groundPropertyBlock;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            GetGroundPropertyBlock();
            AutoDetectAllRenderers();
        }

        private void Start()
        {
            AutoDetectAllRenderers();

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

            AutoDetectAllRenderers();
        }
#endif

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleStageChanged;
            }
        }

        public void AutoDetectAllRenderers()
        {
            if (centerMeshRenderer == null) centerMeshRenderer = GetComponent<MeshRenderer>() ?? FindMeshRendererByKeywords("CGround", "Center", "CenterGround");
            if (northMeshRenderer == null) northMeshRenderer = FindMeshRendererByKeywords("TGround", "North", "Top");
            if (southMeshRenderer == null) southMeshRenderer = FindMeshRendererByKeywords("BGround", "South", "Bottom");
            if (eastMeshRenderer == null) eastMeshRenderer = FindMeshRendererByKeywords("RGround", "East", "Right");
            if (westMeshRenderer == null) westMeshRenderer = FindMeshRendererByKeywords("LGround", "West", "Left");

            if (northEastMeshRenderer == null) northEastMeshRenderer = FindMeshRendererByKeywords("TRGround", "TR", "NorthEast", "TopRight");
            if (northWestMeshRenderer == null) northWestMeshRenderer = FindMeshRendererByKeywords("TLGround", "TL", "NorthWest", "TopLeft");
            if (southEastMeshRenderer == null) southEastMeshRenderer = FindMeshRendererByKeywords("BRGround", "BR", "SouthEast", "BottomRight");
            if (southWestMeshRenderer == null) southWestMeshRenderer = FindMeshRendererByKeywords("BLGround", "BL", "SouthWest", "BottomLeft");
        }

        private MeshRenderer FindMeshRendererByKeywords(params string[] keywords)
        {
            // 1. Look under transform.parent if available (e.g. MapData/Grouind)
            if (transform.parent != null)
            {
                MeshRenderer found = SearchInTransform(transform.parent, keywords);
                if (found != null) return found;
            }

            // 2. Look under "Grouind" or "Ground" root object in active scene
            GameObject groundGroup = GameObject.Find("Grouind") ?? GameObject.Find("Ground");
            if (groundGroup != null)
            {
                MeshRenderer found = SearchInTransform(groundGroup.transform, keywords);
                if (found != null) return found;
            }

            // 3. Fallback search by GameObject name
            foreach (string kw in keywords)
            {
                GameObject obj = GameObject.Find(kw) ?? 
                                 GameObject.Find("Ground_" + kw) ?? 
                                 GameObject.Find("Floor_" + kw);
                if (obj != null && obj.GetComponent<MeshRenderer>() != null)
                {
                    return obj.GetComponent<MeshRenderer>();
                }
            }

            return null;
        }

        private MeshRenderer SearchInTransform(Transform parent, string[] keywords)
        {
            // First pass: exact name match
            foreach (Transform child in parent)
            {
                string childName = child.name;
                foreach (string kw in keywords)
                {
                    if (childName.Equals(kw, System.StringComparison.OrdinalIgnoreCase))
                    {
                        MeshRenderer rend = child.GetComponent<MeshRenderer>();
                        if (rend != null) return rend;
                    }
                }
            }

            // Second pass: contains keyword
            foreach (Transform child in parent)
            {
                string childNameUpper = child.name.ToUpper();
                foreach (string kw in keywords)
                {
                    string kwUpper = kw.ToUpper();
                    if (childNameUpper.Contains(kwUpper))
                    {
                        MeshRenderer rend = child.GetComponent<MeshRenderer>();
                        if (rend != null) return rend;
                    }
                }
            }

            return null;
        }

        public void HandleStageChanged(int gridX, int gridY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            AutoDetectAllRenderers();

            // Update main floor (Center)
            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(gridX, gridY);
            BiomeType currentBiome = stage != null ? stage.biome : BiomeType.UrbanRuins;

            if (centerMeshRenderer != null)
            {
                centerMeshRenderer.gameObject.SetActive(true);
                ApplyStageMaterial(gridX, gridY, centerMeshRenderer, currentBiome);
            }

            // Update 4 cardinal neighbors (North: Y+1, South: Y-1, East: X+1, West: X-1)
            UpdateNeighborFloor(gridX, gridY + 1, northMeshRenderer, currentBiome);
            UpdateNeighborFloor(gridX, gridY - 1, southMeshRenderer, currentBiome);
            UpdateNeighborFloor(gridX + 1, gridY, eastMeshRenderer, currentBiome);
            UpdateNeighborFloor(gridX - 1, gridY, westMeshRenderer, currentBiome);

            // Update 4 diagonal corner neighbors
            UpdateNeighborFloor(gridX + 1, gridY + 1, northEastMeshRenderer, currentBiome); // TR
            UpdateNeighborFloor(gridX - 1, gridY + 1, northWestMeshRenderer, currentBiome); // TL
            UpdateNeighborFloor(gridX + 1, gridY - 1, southEastMeshRenderer, currentBiome); // BR
            UpdateNeighborFloor(gridX - 1, gridY - 1, southWestMeshRenderer, currentBiome); // BL
        }

        private void UpdateNeighborFloor(int x, int y, MeshRenderer neighborRenderer, BiomeType fallbackBiome)
        {
            if (neighborRenderer == null) return;

            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            StageData neighborStage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
            if (neighborStage != null)
            {
                // Enable neighbor floor mesh and apply its correct biome material
                neighborRenderer.gameObject.SetActive(true);
                ApplyStageMaterial(x, y, neighborRenderer, fallbackBiome);
            }
            else
            {
                if (hideMissingNeighbors)
                {
                    // Disable neighbor floor mesh if outside map bounds
                    neighborRenderer.gameObject.SetActive(false);
                }
                else
                {
                    // Keep visible with current room biome fallback to prevent an empty void
                    neighborRenderer.gameObject.SetActive(true);
                    ApplyBiomeMaterial(fallbackBiome, neighborRenderer);
                    ClearBiomeBlendProperties(neighborRenderer);
                }
            }
        }

        private void ApplyStageMaterial(int x, int y, MeshRenderer renderer, BiomeType fallbackBiome)
        {
            if (renderer == null) return;

            StageData stage = WorldMapManager.Instance?.MapGenerator?.GetStage(x, y);
            if (stage == null)
            {
                ApplyBiomeMaterial(fallbackBiome, renderer);
                ClearBiomeBlendProperties(renderer);
                return;
            }

            ApplyBiomeMaterial(stage.biome, renderer);
            SetBiomeBlendProperties(renderer, x, y, stage.biome);
        }

        private void SetBiomeBlendProperties(MeshRenderer renderer, int x, int y, BiomeType biome)
        {
            WorldMapGenerator generator = WorldMapManager.Instance?.MapGenerator;
            if (renderer == null || generator == null)
            {
                ClearBiomeBlendProperties(renderer);
                return;
            }

            MaterialPropertyBlock propBlock = GetGroundPropertyBlock();
            renderer.GetPropertyBlock(propBlock);
            Bounds bounds = renderer.bounds;
            propBlock.SetVector(GroundBoundsId, new Vector4(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z));
            propBlock.SetFloat(BiomeFadeWidthId, biomeTransitionWidth);

            // These four values keep the renderer's local texture mix symmetric across
            // the exact same world-space edge on both neighboring floor meshes.
            StageData[] neighbors =
            {
                generator.GetStage(x, y + 1),
                generator.GetStage(x + 1, y),
                generator.GetStage(x, y - 1),
                generator.GetStage(x - 1, y)
            };
            Vector4 edgeBlend = Vector4.zero;
            Vector4 neighborTiling = new Vector4(0.1f, 0.1f, 0.1f, 0.1f);
            Vector4 neighborSaturation = Vector4.one;

            for (int i = 0; i < neighbors.Length; i++)
            {
                float tiling;
                float saturation;
                edgeBlend[i] = SetNeighborBiomeProperties(
                    groundPropertyBlock,
                    neighbors[i],
                    biome,
                    NeighborBiomeMapIds[i],
                    NeighborBiomeColorIds[i],
                    out tiling,
                    out saturation);
                neighborTiling[i] = tiling;
                neighborSaturation[i] = saturation;
            }

            groundPropertyBlock.SetVector(BiomeEdgeBlendId, edgeBlend);
            groundPropertyBlock.SetVector(BiomeNeighborTilingId, neighborTiling);
            groundPropertyBlock.SetVector(BiomeNeighborSaturationId, neighborSaturation);

            renderer.SetPropertyBlock(groundPropertyBlock);
        }

        private float SetNeighborBiomeProperties(
            MaterialPropertyBlock propertyBlock,
            StageData neighborStage,
            BiomeType currentBiome,
            int mapPropertyId,
            int colorPropertyId,
            out float tiling,
            out float saturation)
        {
            tiling = 0.1f;
            saturation = 1f;
            if (neighborStage == null || neighborStage.biome == currentBiome)
            {
                return 0f;
            }

            Material neighborMaterial = GetBiomeMaterial(neighborStage.biome);
            if (neighborMaterial == null)
            {
                return 0f;
            }

            Texture neighborMap = neighborMaterial.GetTexture("_BaseMap");
            if (neighborMap == null)
            {
                return 0f;
            }

            propertyBlock.SetTexture(mapPropertyId, neighborMap);
            propertyBlock.SetColor(colorPropertyId, neighborMaterial.GetColor("_BaseColor"));
            tiling = neighborMaterial.GetFloat("_Tiling");
            saturation = neighborMaterial.GetFloat("_Saturation");
            return 1f;
        }

        private void ClearBiomeBlendProperties(MeshRenderer renderer)
        {
            if (renderer == null) return;

            MaterialPropertyBlock propBlock = GetGroundPropertyBlock();
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetVector(BiomeEdgeBlendId, Vector4.zero);
            renderer.SetPropertyBlock(propBlock);
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
