using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        [System.Serializable]
        public struct BiomeEnvConfig
        {
            public BiomeType biome;
            [Tooltip("Addressable Label (e.g. 'Building' or 'Urban_Obstacles'). If set, assets with this label will be loaded dynamically via Addressables!")]
            public string addressableObstacleLabel;
            [Tooltip("Addressable Label for decorations (e.g. 'Urban_Decorations').")]
            public string addressableDecorationLabel;

            [Tooltip("Prefabs that have colliders and block movement (e.g. Rocks, Trees, Fences).")]
            public List<GameObject> obstaclePrefabs;
            [Tooltip("Prefabs that are purely visual decoration without colliders (e.g. grass patches, dirt, debris, pebbles).")]
            public List<GameObject> decorationPrefabs;
            [Tooltip("Walkway or road tile prefabs that will be spawned along portal paths (e.g. asphalt tiles, dirt trails, wood bridge tiles).")]
            public List<GameObject> streetPrefabs;

            [Range(0, 15)] public int minObstacles;
            [Range(0, 15)] public int maxObstacles;

            [Range(0, 25)] public int minDecorations;
            [Range(0, 25)] public int maxDecorations;
        }

        [Header("Biome Environment Layouts")]
        [SerializeField] private List<BiomeEnvConfig> biomeConfigs;

        [Header("Default Fallback Settings (if no prefabs assigned)")]
        [SerializeField] private bool useProceduralFallbacks = true;
        [SerializeField] private Material propBaseMaterial;
        [SerializeField] private bool snapToGrid = true; // Snaps positions to cell centers and rotations to cardinal 90-degree angles

        [Header("Grid Area Customization")]
        [SerializeField] private float gridWidth = 32f;   // Total grid width in world units
        [SerializeField] private float gridHeight = 18f;  // Total grid height in world units
        [SerializeField] private Vector2 gridCenterOffset = Vector2.zero; // Offsets the grid relative to scene origin
        [SerializeField, Min(1)] private int gridRows = 6;  // Total rows in the spawner grid
        [SerializeField, Min(1)] private int gridCols = 9;  // Total columns in the spawner grid
        [SerializeField] private float prefabSpawnOffsetHeight = 0.0f; // Customize spawning Y height for user prefabs (use 1.0f for a Center-pivoted 2.0-tall Cube)
        [SerializeField] private bool clearMiddleStreet = true; // If true, solid obstacles (buildings/props) will only spawn on the top/bottom sidewalk rows, leaving the center street clear

        [System.Serializable]
        public struct GridRow
        {
            [Tooltip("Columns 0 to 8")]
            public bool[] columns;
        }

        [Header("Custom Grid Spawner (If enabled, ignores min/max counts and player proximity)")]
        [SerializeField] private bool useInspectorGrid = false;
        [SerializeField] private GridRow[] customGrid = new GridRow[6];

#if UNITY_EDITOR
        [Header("Editor Preview Settings")]
        [SerializeField] private BiomeType previewBiome = BiomeType.UrbanRuins;
        public BiomeType PreviewBiome => previewBiome;
#endif

        private GameObject _currentEnvContainer;

        private struct GridCell
        {
            public int row;
            public int col;
            public Vector3 center;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (propBaseMaterial == null)
            {
                propBaseMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PropCardboard.mat");
            }

            // Ensure customGrid matches gridRows and gridCols dynamically while preserving values
            if (customGrid == null || customGrid.Length != gridRows)
            {
                System.Array.Resize(ref customGrid, gridRows);
            }
            for (int r = 0; r < gridRows; r++)
            {
                if (customGrid[r].columns == null || customGrid[r].columns.Length != gridCols)
                {
                    bool[] oldCols = customGrid[r].columns;
                    customGrid[r].columns = new bool[gridCols];
                    if (oldCols != null)
                    {
                        for (int c = 0; c < Mathf.Min(oldCols.Length, gridCols); c++)
                        {
                            customGrid[r].columns[c] = oldCols[c];
                        }
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Clears and generates a deterministic environment layout for the current stage.
        /// </summary>
        public void GenerateStageEnvironment(StageData stage)
        {
            if (stage == null) return;

            // Force update all portals in the scene to align with the new stage coordinate before environment calculations
            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            foreach (TransitionPortal portal in portals)
            {
                if (portal != null)
                {
                    portal.UpdatePortalVisibility(stage.x, stage.y);
                }
            }

            // 1. Find matching config for this biome first (used in both custom and normal spawner)
            BiomeEnvConfig config = GetConfigForBiome(stage.biome);

            // Grid variables (used in both custom and normal spawner)
            int rows = gridRows;
            int cols = gridCols;
            float totalWidth = gridWidth;
            float totalHeight = gridHeight;
            float cellWidth = totalWidth / cols;
            float cellHeight = totalHeight / rows;
            float startX = -totalWidth * 0.5f + gridCenterOffset.x;
            float startY = -totalHeight * 0.5f + gridCenterOffset.y;

            // 2. Clean up old stage environment props safely in both Play and Edit modes
            var activePathSegments = GetActivePathSegments(stage);
            if (_currentEnvContainer != null)
            {
                if (Application.isPlaying) Destroy(_currentEnvContainer);
                else DestroyImmediate(_currentEnvContainer);
            }

            // 3. Create parent container
            _currentEnvContainer = new GameObject("GeneratedEnvironment");
            _currentEnvContainer.transform.position = Vector3.zero;

            // 3b. Custom grid spawner override
            if (useInspectorGrid)
            {
                // Generate visual street tiles on cells that lie on portal paths
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        float x = startX + c * cellWidth + cellWidth * 0.5f;
                        float y = startY + r * cellHeight + cellHeight * 0.5f;
                        Vector2 cellCenter = new Vector2(x, y);

                        if (IsPointOnWalkablePath(cellCenter, activePathSegments, 2.0f))
                        {
                            GameObject streetPrefab = GetRandomPrefab(config.streetPrefabs);
                            if (streetPrefab != null)
                            {
                                float spawnY = streetPrefab.transform.position.y;
                                Vector3 worldPos = new Vector3(cellCenter.x, spawnY, cellCenter.y);
                                Quaternion streetRot = streetPrefab.transform.rotation;
                                GameObject tile = Instantiate(streetPrefab, worldPos, streetRot, _currentEnvContainer.transform);
                                tile.name = streetPrefab.name;
                            }
                            else if (useProceduralFallbacks)
                            {
                                GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                road.name = "ProceduralRoadTile";
                                road.transform.parent = _currentEnvContainer.transform;
                                road.transform.position = new Vector3(cellCenter.x, 0.02f, cellCenter.y);
                                road.transform.localScale = new Vector3(cellWidth * 0.98f, 0.01f, cellHeight * 0.98f);
                                
                                Collider col = road.GetComponent<Collider>();
                                if (col != null)
                                {
                                    if (Application.isPlaying) Destroy(col);
                                    else DestroyImmediate(col);
                                }

                                Renderer rend = road.GetComponent<Renderer>();
                                if (rend != null)
                                {
                                    rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                    rend.material.color = new Color(0.18f, 0.18f, 0.2f);
                                    ApplyCardboardTextureToChildren(road);
                                }
                            }
                        }
                    }
                }

                bool[,] customOccupied = new bool[rows, cols];

                for (int r = 0; r < rows; r++)
                {
                    if (customGrid == null || r >= customGrid.Length) continue;
                    var row = customGrid[r];
                    if (row.columns == null) continue;

                    for (int c = 0; c < cols; c++)
                    {
                        if (c >= row.columns.Length) continue;
                        if (!row.columns[c]) continue; // Cell not checked in inspector
                        if (customOccupied[r, c]) continue; // Already occupied by a multi-cell model!

                        float cellCenterX = startX + c * cellWidth + cellWidth * 0.5f;
                        float cellCenterY = startY + r * cellHeight + cellHeight * 0.5f;
                        Vector2 spawnPos = new Vector2(cellCenterX, cellCenterY);

                        // Skip spawning obstacles near active transition portals to keep doorway exits clear
                        if (IsNearTransitionGates(spawnPos))
                        {
                            continue;
                        }

                        bool isOnPath = IsPointOnWalkablePath(spawnPos, activePathSegments, 2.0f);

                        GameObject prefab = GetRandomPrefab(config.obstaclePrefabs);
                        if (isOnPath && IsLargeBuilding(prefab))
                        {
                            // Swap with a smaller prefab if on a street path
                            prefab = GetRandomSmallerPrefab(config.obstaclePrefabs);
                        }

                        if (prefab != null)
                        {
                            if (isOnPath && IsLargeBuilding(prefab))
                            {
                                // If still a building, skip it or fallback to a smaller procedural obstacle
                                if (useProceduralFallbacks)
                                {
                                    CreateProceduralObstacle(new Vector3(spawnPos.x, spawnPos.y, 0.0f), stage.biome, false);
                                    customOccupied[r, c] = true;
                                }
                                continue;
                            }

                            Quaternion randomYaw = snapToGrid ? Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) : Quaternion.identity;
                            Quaternion rotation = randomYaw * prefab.transform.rotation;

                            // Calculate actual footprint based on rotated model bounds vs cell size
                            Vector2Int footprint = GetPrefabCellFootprint(prefab, rotation, cellWidth, cellHeight);

                            // Check if the multi-cell space is available
                            bool canSpan = true;
                            if (r + footprint.y > rows || c + footprint.x > cols)
                            {
                                canSpan = false;
                            }
                            else
                            {
                                for (int dr = 0; dr < footprint.y; dr++)
                                {
                                    for (int dc = 0; dc < footprint.x; dc++)
                                    {
                                        if (customOccupied[r + dr, c + dc])
                                        {
                                            canSpan = false;
                                            break;
                                        }
                                        float chkX = startX + (c + dc) * cellWidth + cellWidth * 0.5f;
                                        float chkY = startY + (r + dr) * cellHeight + cellHeight * 0.5f;
                                        if (IsNearTransitionGates(new Vector2(chkX, chkY)))
                                        {
                                            canSpan = false;
                                            break;
                                        }
                                    }
                                    if (!canSpan) break;
                                }
                            }

                            int spanCols = canSpan ? footprint.x : 1;
                            int spanRows = canSpan ? footprint.y : 1;

                            // Mark all cells covered by this object as occupied
                            for (int dr = 0; dr < spanRows; dr++)
                            {
                                for (int dc = 0; dc < spanCols; dc++)
                                {
                                    if (r + dr < rows && c + dc < cols)
                                    {
                                        customOccupied[r + dr, c + dc] = true;
                                    }
                                }
                            }

                            // Place model at the center of its multi-cell footprint
                            float centerX = startX + (c + spanCols * 0.5f) * cellWidth;
                            float centerZ = startY + (r + spanRows * 0.5f) * cellHeight;

                            float spawnY = prefab.transform.position.y + prefabSpawnOffsetHeight;
                            Vector3 worldPos = new Vector3(centerX, spawnY, centerZ);
                            GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                            obj.name = prefab.name;
                        }
                        else if (useProceduralFallbacks)
                        {
                            CreateProceduralObstacle(new Vector3(spawnPos.x, spawnPos.y, 0.0f), stage.biome, !isOnPath);
                            customOccupied[r, c] = true;
                        }
                    }
                }

                return; // Skip normal random spawning!
            }

            // 4. Init deterministic state using stage seed (offset slightly to not clash with enemy spawns)
            Random.InitState(stage.stageSeed + 12345);

            // Fetch player position to avoid spawning obstacles directly on top of them
            Vector3 playerPos = Vector3.zero;
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerPos = player.transform.position;
            }

            List<GridCell> allCells = new List<GridCell>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = startX + c * cellWidth + cellWidth * 0.5f;
                    float y = startY + r * cellHeight + cellHeight * 0.5f;
                    allCells.Add(new GridCell { row = r, col = c, center = new Vector3(x, y, 0f) });
                }
            }

            // 4b. Generate visual street tiles on cells that lie on portal paths
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = startX + c * cellWidth + cellWidth * 0.5f;
                    float y = startY + r * cellHeight + cellHeight * 0.5f;
                    Vector2 cellCenter = new Vector2(x, y);

                    if (IsPointOnWalkablePath(cellCenter, activePathSegments, 2.0f))
                    {
                        GameObject streetPrefab = GetRandomPrefab(config.streetPrefabs);
                        if (streetPrefab != null)
                        {
                            float spawnY = streetPrefab.transform.position.y;
                            Vector3 worldPos = new Vector3(cellCenter.x, spawnY, cellCenter.y);
                            Quaternion streetRot = streetPrefab.transform.rotation;
                            GameObject tile = Instantiate(streetPrefab, worldPos, streetRot, _currentEnvContainer.transform);
                            tile.name = streetPrefab.name;
                        }
                        else if (useProceduralFallbacks)
                        {
                            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            road.name = "ProceduralRoadTile";
                            road.transform.parent = _currentEnvContainer.transform;
                            road.transform.position = new Vector3(cellCenter.x, 0.02f, cellCenter.y);
                            road.transform.localScale = new Vector3(cellWidth * 0.98f, 0.01f, cellHeight * 0.98f);
                            
                            Collider col = road.GetComponent<Collider>();
                            if (col != null)
                            {
                                if (Application.isPlaying) Destroy(col);
                                else DestroyImmediate(col);
                            }

                            Renderer rend = road.GetComponent<Renderer>();
                            if (rend != null)
                            {
                                rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                rend.material.color = new Color(0.18f, 0.18f, 0.2f);
                                ApplyCardboardTextureToChildren(road);
                            }
                        }
                    }
                }
            }

            // Initialize 2D Occupancy Grid and Path Grid
            bool[,] occupiedGrid = new bool[rows, cols];
            bool[,] isPathGrid = new bool[rows, cols];
            float safeRadius = 1.8f;

            List<GridCell> decorationSafeCells = new List<GridCell>();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = startX + c * cellWidth + cellWidth * 0.5f;
                    float y = startY + r * cellHeight + cellHeight * 0.5f;
                    Vector2 cellCenter = new Vector2(x, y);

                    // Check player safety zone mapping to 3D ground plane coords (X and Z)
                    Vector3 cellWorldPos = new Vector3(x, 0f, y);
                    Vector3 playerWorldPos = new Vector3(playerPos.x, 0f, playerPos.z);
                    if (Vector3.Distance(cellWorldPos, playerWorldPos) < safeRadius || IsNearTransitionGates(cellCenter))
                    {
                        occupiedGrid[r, c] = true; // Mark as blocked/safe zone
                        continue;
                    }

                    // Check if the cell lies on a street path
                    if (IsPointOnWalkablePath(cellCenter, activePathSegments, 2.0f))
                    {
                        isPathGrid[r, c] = true;
                    }
                    else
                    {
                        decorationSafeCells.Add(new GridCell { row = r, col = c, center = new Vector3(x, y, 0f) });
                    }
                }
            }

            // Shuffle decoration cells deterministically using stage seed
            for (int i = 0; i < decorationSafeCells.Count; i++)
            {
                GridCell temp = decorationSafeCells[i];
                int randomIndex = Random.Range(i, decorationSafeCells.Count);
                decorationSafeCells[i] = decorationSafeCells[randomIndex];
                decorationSafeCells[randomIndex] = temp;
            }

            int obstacleCount = Random.Range(config.minObstacles, config.maxObstacles + 1);
            int decorationCount = Random.Range(config.minDecorations, config.maxDecorations + 1);

            int buildingSpawnCount = Mathf.RoundToInt(obstacleCount * 0.7f);
            int pathSpawnCount = obstacleCount - buildingSpawnCount;
            int spawnDecorations = Mathf.Min(decorationCount, decorationSafeCells.Count);

            // 5a. Generate off-path buildings using multi-cell footprint occupancy
            for (int i = 0; i < buildingSpawnCount; i++)
            {
                GameObject prefab = GetRandomPrefab(config.obstaclePrefabs);
                if (prefab == null) continue;

                Quaternion randomYaw = snapToGrid ? Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) : Quaternion.identity;
                Quaternion rotation = randomYaw * prefab.transform.rotation;

                // Calculate footprint in cells (X cols x Z rows)
                Vector2Int footprint = GetPrefabCellFootprint(prefab, rotation, cellWidth, cellHeight);

                // Find all valid (row, col) anchor positions where this multi-cell footprint fits completely
                List<Vector2Int> validPositions = new List<Vector2Int>();
                for (int r = 0; r <= rows - footprint.y; r++)
                {
                    for (int c = 0; c <= cols - footprint.x; c++)
                    {
                        bool canPlace = true;
                        for (int dr = 0; dr < footprint.y; dr++)
                        {
                            for (int dc = 0; dc < footprint.x; dc++)
                            {
                                if (occupiedGrid[r + dr, c + dc] || isPathGrid[r + dr, c + dc])
                                {
                                    canPlace = false;
                                    break;
                                }
                            }
                            if (!canPlace) break;
                        }

                        if (canPlace)
                        {
                            validPositions.Add(new Vector2Int(c, r));
                        }
                    }
                }

                if (validPositions.Count > 0)
                {
                    // Randomly select one of the valid positions
                    Vector2Int chosen = validPositions[Random.Range(0, validPositions.Count)];
                    int startC = chosen.x;
                    int startR = chosen.y;

                    // Reserve all cells in the footprint
                    for (int dr = 0; dr < footprint.y; dr++)
                    {
                        for (int dc = 0; dc < footprint.x; dc++)
                        {
                            occupiedGrid[startR + dr, startC + dc] = true;
                        }
                    }

                    // Calculate center position across the multi-cell span
                    float centerX = startX + (startC + footprint.x * 0.5f) * cellWidth;
                    float centerZ = startY + (startR + footprint.y * 0.5f) * cellHeight;

                    float jitterX = snapToGrid ? 0f : Random.Range(-cellWidth * 0.1f, cellWidth * 0.1f);
                    float jitterZ = snapToGrid ? 0f : Random.Range(-cellHeight * 0.1f, cellHeight * 0.1f);

                    float spawnY = prefab.transform.position.y + prefabSpawnOffsetHeight;
                    Vector3 worldPos = new Vector3(centerX + jitterX, spawnY, centerZ + jitterZ);

                    GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                    obj.name = prefab.name;
                }
                else
                {
                    // Multi-cell footprint couldn't fit; fallback to a 1x1 smaller prefab
                    GameObject smallerPrefab = GetRandomSmallerPrefab(config.obstaclePrefabs);
                    List<Vector2Int> singleCells = new List<Vector2Int>();
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            if (!occupiedGrid[r, c] && !isPathGrid[r, c])
                            {
                                singleCells.Add(new Vector2Int(c, r));
                            }
                        }
                    }

                    if (singleCells.Count > 0)
                    {
                        Vector2Int singleChosen = singleCells[Random.Range(0, singleCells.Count)];
                        occupiedGrid[singleChosen.y, singleChosen.x] = true;

                        float centerX = startX + (singleChosen.x + 0.5f) * cellWidth;
                        float centerZ = startY + (singleChosen.y + 0.5f) * cellHeight;

                        if (smallerPrefab != null)
                        {
                            float spawnY = smallerPrefab.transform.position.y + prefabSpawnOffsetHeight;
                            Vector3 worldPos = new Vector3(centerX, spawnY, centerZ);
                            GameObject obj = Instantiate(smallerPrefab, worldPos, rotation, _currentEnvContainer.transform);
                            obj.name = smallerPrefab.name;
                        }
                        else if (useProceduralFallbacks)
                        {
                            CreateProceduralObstacle(new Vector3(centerX, centerZ, 0f), stage.biome, true);
                        }
                    }
                }
            }

            // 5b. Generate on-path small obstacles
            for (int i = 0; i < pathSpawnCount; i++)
            {
                List<Vector2Int> pathCandidates = new List<Vector2Int>();
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (isPathGrid[r, c] && !occupiedGrid[r, c])
                        {
                            pathCandidates.Add(new Vector2Int(c, r));
                        }
                    }
                }

                if (pathCandidates.Count == 0) break;

                Vector2Int chosen = pathCandidates[Random.Range(0, pathCandidates.Count)];
                occupiedGrid[chosen.y, chosen.x] = true;

                float x = startX + (chosen.x + 0.5f) * cellWidth;
                float y = startY + (chosen.y + 0.5f) * cellHeight;

                float jitterX = snapToGrid ? 0f : Random.Range(-cellWidth * 0.2f, cellWidth * 0.2f);
                float jitterY = snapToGrid ? 0f : Random.Range(-cellHeight * 0.2f, cellHeight * 0.2f);
                Vector3 spawnPos = new Vector3(x + jitterX, y + jitterY, 0f);

                GameObject prefab = GetRandomSmallerPrefab(config.obstaclePrefabs);
                if (prefab != null)
                {
                    float spawnY = prefab.transform.position.y + prefabSpawnOffsetHeight;
                    Vector3 worldPos = new Vector3(spawnPos.x, spawnY, spawnPos.y);
                    Quaternion randomYaw = snapToGrid ? Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) : Quaternion.identity;
                    Quaternion rotation = randomYaw * prefab.transform.rotation;
                    GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                    obj.name = prefab.name;
                }
                else if (useProceduralFallbacks)
                {
                    CreateProceduralObstacle(spawnPos, stage.biome, false);
                }
            }

            // 6. Generate Decorations (visuals only)
            int spawnedDecorCount = 0;
            for (int i = 0; i < decorationSafeCells.Count && spawnedDecorCount < spawnDecorations; i++)
            {
                GridCell cell = decorationSafeCells[i];

                // Do not spawn decorations on cells that are already occupied by buildings, vehicles, or obstacles!
                if (occupiedGrid[cell.row, cell.col])
                {
                    continue;
                }

                // Add organic jitter within cell boundaries (only if not snapped to grid)
                float jitterX = snapToGrid ? 0f : Random.Range(-cellWidth * 0.2f, cellWidth * 0.2f);
                float jitterY = snapToGrid ? 0f : Random.Range(-cellHeight * 0.2f, cellHeight * 0.2f);
                Vector3 spawnPos = cell.center + new Vector3(jitterX, jitterY, 0f);

                GameObject prefab = GetRandomPrefab(config.decorationPrefabs);
                if (prefab != null)
                {
                    // Map 2D grid coordinates to 3D world space (X/Z plane), inheriting prefab's original Y height plus offset
                    float spawnY = prefab.transform.position.y + prefabSpawnOffsetHeight;
                    Vector3 worldPos = new Vector3(spawnPos.x, spawnY, spawnPos.y);
                    Quaternion randomYaw = snapToGrid ? Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) : Quaternion.identity;
                    Quaternion rotation = randomYaw * prefab.transform.rotation;
                    GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                    obj.name = prefab.name;

                    occupiedGrid[cell.row, cell.col] = true;
                    spawnedDecorCount++;
                }
                else if (useProceduralFallbacks)
                {
                    CreateProceduralDecoration(spawnPos, stage.biome);
                    occupiedGrid[cell.row, cell.col] = true;
                    spawnedDecorCount++;
                }
            }

            // Skip global texture application to preserve user prefab materials

            Debug.Log($"[EnvironmentManager] Grid generated {buildingSpawnCount + pathSpawnCount} obstacles and {spawnDecorations} decorations (out of safe cells: {decorationSafeCells.Count}) for Biome {stage.biome} using seed {stage.stageSeed}");
        }

        private BiomeEnvConfig GetConfigForBiome(BiomeType biome)
        {
            if (biomeConfigs != null)
            {
                // 1. Try to find the exact matching biome config first
                foreach (var config in biomeConfigs)
                {
                    if (config.biome == biome) return config;
                }
                // 2. Wildcard fallback: check if a general 'All' configuration exists
                foreach (var config in biomeConfigs)
                {
                    if (config.biome == BiomeType.All) return config;
                }
            }

            // Return a default config mapping if nothing is configured in inspector
            return new BiomeEnvConfig
            {
                biome = biome,
                obstaclePrefabs = new List<GameObject>(),
                decorationPrefabs = new List<GameObject>(),
                minObstacles = 4,
                maxObstacles = 8,
                minDecorations = 6,
                maxDecorations = 12
            };
        }

        /// <summary>
        /// Asynchronously preloads all Addressable prefabs for this biome using its configured label (e.g. 'Building').
        /// </summary>
        public async System.Threading.Tasks.Task PreloadBiomeAddressablesAsync(BiomeType biome)
        {
            if (biomeConfigs == null) return;
            int index = biomeConfigs.FindIndex(b => b.biome == biome);
            if (index < 0) index = biomeConfigs.FindIndex(b => b.biome == BiomeType.All);
            if (index < 0) return;

            BiomeEnvConfig config = biomeConfigs[index];

            if (!string.IsNullOrEmpty(config.addressableObstacleLabel))
            {
                IList<GameObject> loadedObstacles = await AddressableManager.LoadAssetsByLabelAsync<GameObject>(config.addressableObstacleLabel);
                if (loadedObstacles != null && loadedObstacles.Count > 0)
                {
                    if (config.obstaclePrefabs == null) config.obstaclePrefabs = new List<GameObject>();
                    foreach (var go in loadedObstacles)
                    {
                        if (go != null && !config.obstaclePrefabs.Contains(go))
                        {
                            config.obstaclePrefabs.Add(go);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(config.addressableDecorationLabel))
            {
                IList<GameObject> loadedDecos = await AddressableManager.LoadAssetsByLabelAsync<GameObject>(config.addressableDecorationLabel);
                if (loadedDecos != null && loadedDecos.Count > 0)
                {
                    if (config.decorationPrefabs == null) config.decorationPrefabs = new List<GameObject>();
                    foreach (var go in loadedDecos)
                    {
                        if (go != null && !config.decorationPrefabs.Contains(go))
                        {
                            config.decorationPrefabs.Add(go);
                        }
                    }
                }
            }

            biomeConfigs[index] = config;
        }

        private Vector3 GetRandomSafePosition(Vector3 playerPos, float safeRadius)
        {
            int maxAttempts = 25; // Increase attempts to find a safe layout
            Vector3 spawnPos = Vector3.zero;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Select a position inside the screen limits (with boundary margins)
                float x = Random.Range(-gridWidth * 0.5f + gridCenterOffset.x, gridWidth * 0.5f + gridCenterOffset.x);
                float y = Random.Range(-gridHeight * 0.5f + gridCenterOffset.y, gridHeight * 0.5f + gridCenterOffset.y);
                spawnPos = new Vector3(x, y, 0f);

                // Check distance to player
                if (Vector3.Distance(spawnPos, playerPos) < safeRadius)
                {
                    continue;
                }

                // Check if position blocks any of the 4 transition gate areas
                if (IsNearTransitionGates(spawnPos))
                {
                    continue;
                }

                break;
            }
            return spawnPos;
        }

        private bool IsNearTransitionGates(Vector3 pos)
        {
            return IsNearTransitionGates(new Vector2(pos.x, pos.y));
        }

        private bool IsNearTransitionGates(Vector2 pos)
        {
            // Find all transition portals in the scene dynamically
            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            foreach (TransitionPortal portal in portals)
            {
                if (portal != null)
                {
                    // Check if the portal is enabled (only check col.enabled in play mode, assume active in editor preview)
                    Collider col = portal.GetComponent<Collider>();
                    if (Application.isPlaying && col != null && !col.enabled)
                    {
                        continue; // Path is walled off, safe to spawn obstacles here
                    }

                    // Check distance on the X/Z plane (portal's 3D transform Z coordinate corresponds to pos.y in 2D)
                    Vector2 portalPos = new Vector2(portal.transform.position.x, portal.transform.position.z);
                    if (Vector2.Distance(pos, portalPos) < 2.0f) // Maintain a safe 2.0-meter clear zone around active portals
                    {
                        return true;
                    }
                }
            }

            // Fallback to static boundary checks if no portals are present in the scene
            if (portals.Length == 0)
            {
                if (Mathf.Abs(pos.x - gridCenterOffset.x) < 1.8f && pos.y > (gridHeight * 0.5f + gridCenterOffset.y - 1.2f)) return true;
                if (Mathf.Abs(pos.x - gridCenterOffset.x) < 1.8f && pos.y < (-gridHeight * 0.5f + gridCenterOffset.y + 1.2f)) return true;
                if (pos.x < (-gridWidth * 0.5f + gridCenterOffset.x + 1.2f) && Mathf.Abs(pos.y - gridCenterOffset.y) < 1.5f) return true;
                if (pos.x > (gridWidth * 0.5f + gridCenterOffset.x - 1.2f) && Mathf.Abs(pos.y - gridCenterOffset.y) < 1.5f) return true;
            }

            return false;
        }

        private GameObject GetRandomPrefab(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0) return null;
            return prefabs[Random.Range(0, prefabs.Count)];
        }

        // --- PROCEDURAL VISUAL FALLBACK CREATORS ---

        private void CreateProceduralObstacle(Vector3 pos, BiomeType biome)
        {
            CreateProceduralObstacle(pos, biome, true);
        }

        private void CreateProceduralObstacle(Vector3 pos, BiomeType biome, bool allowBuildings)
        {
            // Spawn unique Bangkok-themed 3D miniature obstacles in urban biomes
            if (biome == BiomeType.UrbanRuins || biome == BiomeType.Highways || biome == BiomeType.SuburbanVillages)
            {
                // In Bangkok biomes:
                // r = 0: TukTuk (small cover)
                // r = 1: SpiritHouse (small cover)
                // r = 2: FoodCart (small cover)
                // r = 3: UtilityPole (small cover)
                // r = 4: Shophouse (large building)
                // r = 5: ThaiWoodenHouse (large building)
                int r = allowBuildings ? Random.Range(0, 6) : Random.Range(0, 4);
                Vector3 worldPos = new Vector3(pos.x, 0.0f, pos.y); // Spawn at Y = 0.0f so custom procedural structures sit flush with floor
                if (r == 0)
                {
                    CreateProceduralTukTuk(worldPos);
                    return;
                }
                else if (r == 1)
                {
                    CreateProceduralSpiritHouse(worldPos);
                    return;
                }
                else if (r == 2)
                {
                    CreateProceduralStreetFoodCart(worldPos);
                    return;
                }
                else if (r == 3)
                {
                    CreateProceduralUtilityPole(worldPos);
                    return;
                }
                else if (r == 4)
                {
                    CreateProceduralShophouse(worldPos);
                    return;
                }
                else if (r == 5)
                {
                    CreateProceduralThaiHouse(worldPos);
                    return;
                }
            }

            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.parent = _currentEnvContainer.transform;
            obstacle.transform.rotation = Quaternion.Euler(0f, snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f), 0f);
            
            float height = allowBuildings ? 2.0f : 0.6f; // Flat crate/box if on a road corridor
            float scaleX = allowBuildings ? Random.Range(0.6f, 1.2f) : Random.Range(0.6f, 0.9f);
            float scaleZ = allowBuildings ? Random.Range(0.6f, 1.2f) : Random.Range(0.6f, 0.9f);
            
            obstacle.transform.localScale = new Vector3(scaleX, height, scaleZ);
            
            // Adjust position Y so that the bottom of the Cube sits flush on Y = 0.0f
            float spawnY = height * 0.5f; 
            obstacle.transform.position = new Vector3(pos.x, spawnY, pos.y);

            Renderer rend = obstacle.GetComponent<Renderer>();
            Color themeColor = Color.gray;

            if (!allowBuildings)
            {
                themeColor = new Color(0.85f, 0.65f, 0.1f); // Yellow obstacle block
                obstacle.name = "YellowObstacleCrate";
            }
            else
            {
                switch (biome)
                {
                    case BiomeType.UrbanRuins:
                        themeColor = new Color(0.35f, 0.35f, 0.35f); // Grey concrete rubble
                        obstacle.name = "ConcreteDebris";
                        break;
                    case BiomeType.OvergrownForests:
                        themeColor = new Color(0.2f, 0.35f, 0.15f); // Mossy log wood
                        obstacle.name = "MossyLog";
                        break;
                    case BiomeType.Highlands:
                        themeColor = new Color(0.48f, 0.44f, 0.4f); // Mountain stone boulder
                        obstacle.name = "Boulder";
                        break;
                    case BiomeType.Waterways:
                        themeColor = new Color(0.2f, 0.4f, 0.35f); // Mossy water rock
                        obstacle.name = "MossyRock";
                        break;
                    case BiomeType.SuburbanVillages:
                        themeColor = new Color(0.55f, 0.38f, 0.22f); // Wooden box crate
                        obstacle.name = "WoodCrate";
                        break;
                    default:
                        themeColor = new Color(0.4f, 0.4f, 0.4f); // General rock
                        obstacle.name = "StoneObstacle";
                        break;
                }
            }

            if (rend != null)
            {
                rend.material.color = themeColor;
                ApplyCardboardTextureToChildren(obstacle);
            }
        }

        private void CreateProceduralDecoration(Vector3 pos, BiomeType biome)
        {
            GameObject decor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decor.transform.parent = _currentEnvContainer.transform;
            
            float size = Random.Range(0.2f, 0.5f);
            decor.transform.localScale = new Vector3(size, 0.02f, size);
            decor.transform.position = new Vector3(pos.x, 0.01f, pos.y);

            Collider col = decor.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            decor.transform.rotation = Quaternion.Euler(0f, snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f), 0f);

            Renderer rend = decor.GetComponent<Renderer>();
            Color themeColor = Color.gray;

            switch (biome)
            {
                case BiomeType.UrbanRuins:
                    themeColor = new Color(0.2f, 0.2f, 0.2f, 0.6f); // Dust/debris ash pile
                    decor.name = "AshDebris";
                    break;
                case BiomeType.OvergrownForests:
                    themeColor = new Color(0.4f, 0.8f, 0.2f, 0.7f); // Bright wild leaf prop
                    decor.name = "WildGrass";
                    break;
                case BiomeType.Waterways:
                    themeColor = new Color(0.1f, 0.6f, 0.8f, 0.4f); // Water puddle
                    decor.name = "Puddle";
                    break;
                default:
                    themeColor = new Color(0.45f, 0.4f, 0.35f, 0.7f); // Pebble / Sand patch
                    decor.name = "Pebble";
                    break;
            }

            if (rend != null)
            {
                rend.material.color = themeColor;
                ApplyCardboardTextureToChildren(decor);
            }
        }

        private void CreateProceduralTukTuk(Vector3 pos)
        {
            GameObject tuktuk = new GameObject("RuinedTukTuk");
            tuktuk.transform.parent = _currentEnvContainer.transform;
            tuktuk.transform.position = pos;
            tuktuk.transform.rotation = Quaternion.Euler(0f, snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f), 0f);

            BoxCollider col = tuktuk.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.2f, 1.0f, 1.8f);

            // Cab / Main Body (Blue metal)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.parent = tuktuk.transform;
            body.transform.localPosition = new Vector3(0f, 0.4f, -0.2f);
            body.transform.localScale = new Vector3(1.0f, 0.6f, 1.2f);
            body.GetComponent<Renderer>().material.color = new Color(0.1f, 0.35f, 0.65f); // blue

            // Front nose (Yellow trim)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.parent = tuktuk.transform;
            nose.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
            nose.transform.localScale = new Vector3(0.6f, 0.4f, 0.5f);
            nose.GetComponent<Renderer>().material.color = new Color(0.75f, 0.65f, 0.1f); // yellow

            // Roof (Yellow canopy)
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.transform.parent = tuktuk.transform;
            roof.transform.localPosition = new Vector3(0f, 0.9f, -0.2f);
            roof.transform.localScale = new Vector3(1.05f, 0.1f, 1.4f);
            roof.GetComponent<Renderer>().material.color = new Color(0.85f, 0.75f, 0.1f); // yellow

            // Windshield frame (Black)
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.transform.parent = tuktuk.transform;
            frame.transform.localPosition = new Vector3(0f, 0.7f, 0.4f);
            frame.transform.localScale = new Vector3(0.9f, 0.5f, 0.05f);
            frame.GetComponent<Renderer>().material.color = Color.black;

            // Wheels (3 cylinders)
            GameObject wFront = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wFront.GetComponent<Collider>());
            wFront.transform.parent = tuktuk.transform;
            wFront.transform.localPosition = new Vector3(0f, 0.15f, 0.6f);
            wFront.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
            wFront.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wFront.GetComponent<Renderer>().material.color = Color.black;

            GameObject wBackL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wBackL.GetComponent<Collider>());
            wBackL.transform.parent = tuktuk.transform;
            wBackL.transform.localPosition = new Vector3(-0.45f, 0.2f, -0.6f);
            wBackL.transform.localScale = new Vector3(0.4f, 0.06f, 0.4f);
            wBackL.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wBackL.GetComponent<Renderer>().material.color = Color.black;

            GameObject wBackR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wBackR.GetComponent<Collider>());
            wBackR.transform.parent = tuktuk.transform;
            wBackR.transform.localPosition = new Vector3(0.45f, 0.2f, -0.6f);
            wBackR.transform.localScale = new Vector3(0.4f, 0.06f, 0.4f);
            wBackR.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wBackR.GetComponent<Renderer>().material.color = Color.black;

            ApplyCardboardTextureToChildren(tuktuk);
        }

        private void CreateProceduralSpiritHouse(Vector3 pos)
        {
            GameObject house = new GameObject("ThaiSpiritHouse");
            house.transform.parent = _currentEnvContainer.transform;
            house.transform.position = pos;
            house.transform.rotation = Quaternion.Euler(0f, snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f), 0f);

            BoxCollider col = house.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.75f, 0f);
            col.size = new Vector3(0.8f, 1.5f, 0.8f);

            // Pillar
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pillar.GetComponent<Collider>());
            pillar.transform.parent = house.transform;
            pillar.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            pillar.transform.localScale = new Vector3(0.2f, 0.4f, 0.2f);
            pillar.GetComponent<Renderer>().material.color = new Color(0.45f, 0.35f, 0.25f);

            // Platform
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(platform.GetComponent<Collider>());
            platform.transform.parent = house.transform;
            platform.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            platform.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
            platform.GetComponent<Renderer>().material.color = new Color(0.75f, 0.15f, 0.15f);

            // Sanctuary
            GameObject sanctuary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(sanctuary.GetComponent<Collider>());
            sanctuary.transform.parent = house.transform;
            sanctuary.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            sanctuary.transform.localScale = new Vector3(0.45f, 0.4f, 0.45f);
            sanctuary.GetComponent<Renderer>().material.color = new Color(0.85f, 0.68f, 0.1f);

            // Pointed Roof
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.transform.parent = house.transform;
            roof.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            roof.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            roof.GetComponent<Renderer>().material.color = new Color(0.75f, 0.15f, 0.15f);

            ApplyCardboardTextureToChildren(house);
        }

        private void CreateProceduralStreetFoodCart(Vector3 pos)
        {
            GameObject cart = new GameObject("StreetFoodCart");
            cart.transform.parent = _currentEnvContainer.transform;
            cart.transform.position = pos;
            cart.transform.rotation = Quaternion.Euler(0f, snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f), 0f);

            BoxCollider col = cart.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.1f, 1.2f, 1.3f);

            // Table
            GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(counter.GetComponent<Collider>());
            counter.transform.parent = cart.transform;
            counter.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            counter.transform.localScale = new Vector3(0.8f, 0.6f, 1.2f);
            counter.GetComponent<Renderer>().material.color = new Color(0.6f, 0.6f, 0.62f);

            // Wheels
            GameObject wL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wL.GetComponent<Collider>());
            wL.transform.parent = cart.transform;
            wL.transform.localPosition = new Vector3(-0.42f, 0.2f, 0f);
            wL.transform.localScale = new Vector3(0.4f, 0.04f, 0.4f);
            wL.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wL.GetComponent<Renderer>().material.color = Color.black;

            GameObject wR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wR.GetComponent<Collider>());
            wR.transform.parent = cart.transform;
            wR.transform.localPosition = new Vector3(0.42f, 0.2f, 0f);
            wR.transform.localScale = new Vector3(0.4f, 0.04f, 0.4f);
            wR.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wR.GetComponent<Renderer>().material.color = Color.black;

            // Umbrella Stand
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.parent = cart.transform;
            pole.transform.localPosition = new Vector3(0f, 1.0f, 0.4f);
            pole.transform.localScale = new Vector3(0.04f, 0.5f, 0.04f);
            pole.GetComponent<Renderer>().material.color = Color.grey;

            // Canopy
            GameObject umbrella = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(umbrella.GetComponent<Collider>());
            umbrella.transform.parent = cart.transform;
            umbrella.transform.localPosition = new Vector3(0f, 1.5f, 0.4f);
            umbrella.transform.localScale = new Vector3(1.3f, 0.02f, 1.3f);
            umbrella.GetComponent<Renderer>().material.color = new Color(0.8f, 0.15f, 0.15f);

            ApplyCardboardTextureToChildren(cart);
        }

        private void CreateProceduralUtilityPole(Vector3 pos)
        {
            GameObject pole = new GameObject("BangkokUtilityPole");
            pole.transform.parent = _currentEnvContainer.transform;
            pole.transform.position = pos;

            BoxCollider col = pole.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.5f, 0f);
            col.size = new Vector3(0.6f, 3.0f, 0.6f);

            // Pole
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(column.GetComponent<Collider>());
            column.transform.parent = pole.transform;
            column.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            column.transform.localScale = new Vector3(0.2f, 1.5f, 0.2f);
            column.GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.5f);

            // Cables (4 loops)
            for (int i = 0; i < 4; i++)
            {
                GameObject cables = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(cables.GetComponent<Collider>());
                cables.transform.parent = pole.transform;
                cables.transform.localPosition = new Vector3(0f, 1.0f + i * 0.4f, 0f);
                cables.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
                cables.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0, 360), Random.Range(-5f, 5f));
                cables.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.1f);
            }

            // Crossbars
            GameObject bar1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bar1.GetComponent<Collider>());
            bar1.transform.parent = pole.transform;
            bar1.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            bar1.transform.localScale = new Vector3(0.9f, 0.08f, 0.15f);
            bar1.GetComponent<Renderer>().material.color = new Color(0.3f, 0.25f, 0.2f);

            ApplyCardboardTextureToChildren(pole);
        }

        private void CreateProceduralShophouse(Vector3 pos)
        {
            GameObject shophouse = new GameObject("BangkokShophouse");
            shophouse.transform.parent = _currentEnvContainer.transform;
            shophouse.transform.position = pos;
            shophouse.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f); // Cardinal rotations

            BoxCollider col = shophouse.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.size = new Vector3(1.3f, 2.2f, 1.3f);

            // Main Building Body (Beige / faded yellow concrete)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.parent = shophouse.transform;
            body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            body.transform.localScale = new Vector3(1.2f, 2.0f, 1.2f);
            Color[] bodyColors = { new Color(0.82f, 0.75f, 0.65f), new Color(0.7f, 0.75f, 0.72f), new Color(0.78f, 0.68f, 0.62f) };
            body.GetComponent<Renderer>().material.color = bodyColors[Random.Range(0, bodyColors.Length)];

            // Steel Rolling Shutter Gate
            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(gate.GetComponent<Collider>());
            gate.transform.parent = shophouse.transform;
            gate.transform.localPosition = new Vector3(0f, 0.4f, 0.61f);
            gate.transform.localScale = new Vector3(0.9f, 0.8f, 0.05f);
            gate.GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.52f);

            // Balcony window
            GameObject balcony = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(balcony.GetComponent<Collider>());
            balcony.transform.parent = shophouse.transform;
            balcony.transform.localPosition = new Vector3(0f, 1.3f, 0.62f);
            balcony.transform.localScale = new Vector3(0.8f, 0.4f, 0.1f);
            balcony.GetComponent<Renderer>().material.color = new Color(0.25f, 0.2f, 0.15f);

            // Awning
            GameObject awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(awning.GetComponent<Collider>());
            awning.transform.parent = shophouse.transform;
            awning.transform.localPosition = new Vector3(0f, 0.85f, 0.75f);
            awning.transform.localScale = new Vector3(1.0f, 0.05f, 0.4f);
            awning.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            awning.GetComponent<Renderer>().material.color = new Color(0.15f, 0.45f, 0.25f);

            ApplyCardboardTextureToChildren(shophouse);
        }

        private void CreateProceduralThaiHouse(Vector3 pos)
        {
            GameObject house = new GameObject("ThaiWoodenHouse");
            house.transform.parent = _currentEnvContainer.transform;
            house.transform.position = pos;
            house.transform.rotation = Quaternion.Euler(0f, snapToGrid ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f), 0f);

            BoxCollider col = house.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(1.3f, 1.6f, 1.3f);

            Color woodColor = new Color(0.35f, 0.24f, 0.16f);
            Color roofColor = new Color(0.55f, 0.2f, 0.15f);

            float offset = 0.45f;
            float pillarHeight = 0.5f;
            Vector3[] pillarOffsets = {
                new Vector3(-offset, pillarHeight / 2f, -offset),
                new Vector3(offset, pillarHeight / 2f, -offset),
                new Vector3(-offset, pillarHeight / 2f, offset),
                new Vector3(offset, pillarHeight / 2f, offset)
            };

            foreach (Vector3 pOffset in pillarOffsets)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(pillar.GetComponent<Collider>());
                pillar.transform.parent = house.transform;
                pillar.transform.localPosition = pOffset;
                pillar.transform.localScale = new Vector3(0.1f, pillarHeight / 2f, 0.1f);
                pillar.GetComponent<Renderer>().material.color = woodColor;
            }

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(platform.GetComponent<Collider>());
            platform.transform.parent = house.transform;
            platform.transform.localPosition = new Vector3(0f, pillarHeight, 0f);
            platform.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f);
            platform.GetComponent<Renderer>().material.color = woodColor;

            GameObject walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(walls.GetComponent<Collider>());
            walls.transform.parent = house.transform;
            walls.transform.localPosition = new Vector3(0f, pillarHeight + 0.35f, 0f);
            walls.transform.localScale = new Vector3(1.0f, 0.6f, 1.0f);
            walls.GetComponent<Renderer>().material.color = new Color(woodColor.r * 1.2f, woodColor.g * 1.2f, woodColor.b * 1.2f);

            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.transform.parent = house.transform;
            roof.transform.localPosition = new Vector3(0f, pillarHeight + 0.8f, 0f);
            roof.transform.localScale = new Vector3(0.9f, 0.9f, 1.1f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            roof.GetComponent<Renderer>().material.color = roofColor;

            ApplyCardboardTextureToChildren(house);
        }

        private void ApplyCardboardTextureToChildren(GameObject root)
        {
            if (root == null) return;
            
            // Resolve default texture
            Texture2D defaultTex = null;
            if (propBaseMaterial != null)
            {
                defaultTex = (Texture2D)propBaseMaterial.GetTexture("_BaseMap");
                if (defaultTex == null)
                {
                    defaultTex = (Texture2D)propBaseMaterial.GetTexture("_MainTex");
                }
            }

#if UNITY_EDITOR
            if (defaultTex == null)
            {
                defaultTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Biomes/Default.png");
            }
#endif

            if (defaultTex == null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer rend in renderers)
            {
                if (rend != null && rend.material != null)
                {
                    Texture currentTex = rend.material.GetTexture("_BaseMap");
                    if (currentTex == null || currentTex.name == "white" || currentTex.name == "DefaultTexture-256")
                    {
                        rend.material.SetTexture("_BaseMap", defaultTex);
                        rend.material.SetTexture("_MainTex", defaultTex);
                    }
                }
            }
        }

        public void ClearEnvironmentInEditor()
        {
            // Find and destroy any generated preview container in Edit mode
            GameObject existing = GameObject.Find("GeneratedEnvironment");
            while (existing != null)
            {
                DestroyImmediate(existing);
                existing = GameObject.Find("GeneratedEnvironment");
            }
            _currentEnvContainer = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw the 6x9 spawner grid bounds in the Scene View for easy level design
            int rows = gridRows;
            int cols = gridCols;
            float totalWidth = gridWidth;
            float totalHeight = gridHeight;
            float cellWidth = totalWidth / cols;
            float cellHeight = totalHeight / rows;
            float gridY = 0.05f; // Draw grid lines slightly above floor to prevent Z-fighting
            float startX = -totalWidth * 0.5f + gridCenterOffset.x;
            float startZ = -totalHeight * 0.5f + gridCenterOffset.y;
            float endX = totalWidth * 0.5f + gridCenterOffset.x;
            float endZ = totalHeight * 0.5f + gridCenterOffset.y;

            // Set grid drawing color (semi-transparent cyan)
            Gizmos.color = new Color(0f, 0.75f, 1f, 0.45f);

            // 1. Draw horizontal lines (along X axis at Z increments)
            for (int r = 0; r <= rows; r++)
            {
                float z = startZ + r * cellHeight;
                Vector3 start = new Vector3(startX, gridY, z);
                Vector3 end = new Vector3(endX, gridY, z);
                Gizmos.DrawLine(start, end);
            }

            // 2. Draw vertical lines (along Z axis at X increments)
            for (int c = 0; c <= cols; c++)
            {
                float x = startX + c * cellWidth;
                Vector3 start = new Vector3(x, gridY, startZ);
                Vector3 end = new Vector3(x, gridY, endZ);
                Gizmos.DrawLine(start, end);
            }

            // 3. Draw small spheres at cell centers and transparent cubes for checked cells
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = startX + c * cellWidth + cellWidth * 0.5f;
                    float z = startZ + r * cellHeight + cellHeight * 0.5f;
                    Vector3 cellCenter = new Vector3(x, gridY, z);

                    // Tiny dot at cell center
                    Gizmos.color = new Color(0f, 0.75f, 1f, 0.25f);
                    Gizmos.DrawSphere(cellCenter, 0.08f);

                    // If cell is checked in customGrid, draw a transparent box preview!
                    if (useInspectorGrid && customGrid != null && r < customGrid.Length)
                    {
                        var row = customGrid[r];
                        if (row.columns != null && c < row.columns.Length && row.columns[c])
                        {
                            // Gold wireframe and semi-transparent cube representing the 2.0-tall spawned prop
                            Gizmos.color = new Color(0.85f, 0.65f, 0.1f, 0.2f);
                            Gizmos.DrawCube(cellCenter + new Vector3(0f, 1.0f, 0f), new Vector3(1.2f, 2.0f, 1.2f));
                            Gizmos.color = new Color(0.85f, 0.65f, 0.1f, 0.6f);
                            Gizmos.DrawWireCube(cellCenter + new Vector3(0f, 1.0f, 0f), new Vector3(1.2f, 2.0f, 1.2f));
                        }
                    }
                }
            }
        }
#endif

        private bool IsInMiddleStreet(int row, int totalRows)
        {
            if (!clearMiddleStreet) return false;
            if (totalRows <= 2) return false; // Too small to clear anything

            // For standard grids, clear the middle 33% of the rows to form the street corridor
            int middleMin = totalRows / 2 - 1;
            int middleMax = totalRows / 2;
            if (totalRows % 2 != 0)
            {
                return row == totalRows / 2;
            }
            return (row == middleMin || row == middleMax);
        }

        private List<KeyValuePair<Vector2, Vector2>> GetActivePathSegments(StageData stage)
        {
            List<KeyValuePair<Vector2, Vector2>> segments = new List<KeyValuePair<Vector2, Vector2>>();
            
            // Deterministic seed matching for map consistency
            Random.State oldState = Random.state;
            Random.InitState(stage.stageSeed + 54321);

            // 1. Randomize the junction point in the center area to create diagonal bends/curves
            Vector2 junctionPoint = new Vector2(
                gridCenterOffset.x + Random.Range(-4f, 4f),
                gridCenterOffset.y + Random.Range(-2f, 2f)
            );

            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            List<TransitionPortal> activePortals = new List<TransitionPortal>();

            foreach (TransitionPortal portal in portals)
            {
                if (portal != null)
                {
                    Collider col = portal.GetComponent<Collider>();
                    if (Application.isPlaying && col != null && !col.enabled)
                    {
                        continue;
                    }
                    activePortals.Add(portal);
                }
            }

            // 2. Connect portals to the central junction
            bool hasHorizontalRoad = false;
            bool hasVerticalRoad = false;

            foreach (TransitionPortal portal in activePortals)
            {
                Vector2 portalPos = new Vector2(portal.transform.position.x, portal.transform.position.z);
                segments.Add(new KeyValuePair<Vector2, Vector2>(portalPos, junctionPoint));

                if (Mathf.Abs(portal.transform.position.x) > gridWidth * 0.4f)
                {
                    hasHorizontalRoad = true;
                }
                else
                {
                    hasVerticalRoad = true;
                }
            }

            // 3. Spawn a vertical side-alley if there's only a horizontal main road (T-junction!)
            if (hasHorizontalRoad && !hasVerticalRoad && Random.value < 0.85f)
            {
                float targetZ = Random.value < 0.5f ? (gridHeight * 0.43f) : (-gridHeight * 0.43f);
                Vector2 sideAlleyEnd = new Vector2(junctionPoint.x, targetZ);
                segments.Add(new KeyValuePair<Vector2, Vector2>(junctionPoint, sideAlleyEnd));
            }
            // 4. Spawn a horizontal side-alley if there's only a vertical main road!
            else if (hasVerticalRoad && !hasHorizontalRoad && Random.value < 0.85f)
            {
                float targetX = Random.value < 0.5f ? (gridWidth * 0.43f) : (-gridWidth * 0.43f);
                Vector2 sideAlleyEnd = new Vector2(targetX, junctionPoint.y);
                segments.Add(new KeyValuePair<Vector2, Vector2>(junctionPoint, sideAlleyEnd));
            }
            // 5. Default crossroad fallback if no portals exist in the scene (e.g. testing)
            else if (activePortals.Count == 0)
            {
                segments.Add(new KeyValuePair<Vector2, Vector2>(new Vector2(-gridWidth * 0.45f, 0f), new Vector2(gridWidth * 0.45f, 0f)));
                segments.Add(new KeyValuePair<Vector2, Vector2>(new Vector2(0f, -gridHeight * 0.45f), new Vector2(0f, gridHeight * 0.45f)));
            }

            Random.state = oldState;
            return segments;
        }

        private bool IsPointOnWalkablePath(Vector2 pt, List<KeyValuePair<Vector2, Vector2>> segments, float pathWidth = 2.0f)
        {
            if (segments.Count == 0) return false;

            foreach (var segment in segments)
            {
                if (DistanceToSegment(pt, segment.Key, segment.Value) < pathWidth)
                {
                    return true;
                }
            }
            return false;
        }

        private float DistanceToSegment(Vector2 t, Vector2 p, Vector2 c)
        {
            Vector2 pc = c - p;
            Vector2 pt = t - p;
            float pcLengthSq = pc.sqrMagnitude;
            if (pcLengthSq == 0f) return pt.magnitude;

            float projection = Vector2.Dot(pt, pc) / pcLengthSq;
            float tParam = Mathf.Clamp01(projection);
            Vector2 projectionPoint = p + tParam * pc;
            return Vector2.Distance(t, projectionPoint);
        }

        /// <summary>
        /// Calculates the world-space bounding size (X and Z) of a prefab after applying rotation.
        /// </summary>
        public Vector2 GetPrefabWorldSize(GameObject prefab, Quaternion rotation)
        {
            if (prefab == null) return new Vector2(1f, 1f);

            Bounds bounds = new Bounds();
            bool hasBounds = false;

            // 1. Check BoxColliders first (accurate for props & structures)
            BoxCollider[] boxColliders = prefab.GetComponentsInChildren<BoxCollider>();
            if (boxColliders != null && boxColliders.Length > 0)
            {
                foreach (var box in boxColliders)
                {
                    if (box.isTrigger) continue;
                    Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                    Vector3 center = box.center;
                    Bounds b = new Bounds(center, size);
                    if (!hasBounds)
                    {
                        bounds = b;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }
            }

            // 2. Check general Colliders
            if (!hasBounds)
            {
                Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    if (col.isTrigger) continue;
                    Bounds b = col.bounds;
                    if (!hasBounds)
                    {
                        bounds = b;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }
            }

            // 3. Fallback to Renderers if no Colliders found
            if (!hasBounds)
            {
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
                foreach (var rend in renderers)
                {
                    if (rend is ParticleSystemRenderer) continue;
                    Bounds b = rend.bounds;
                    if (!hasBounds)
                    {
                        bounds = b;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }
            }

            if (!hasBounds || bounds.size.sqrMagnitude < 0.001f)
            {
                return new Vector2(1f, 1f);
            }

            // Rotate all 8 corners by the target rotation to find projected bounds on the ground plane (X and Z)
            Vector3 extents = bounds.extents;
            Vector3[] corners = new Vector3[8]
            {
                rotation * new Vector3(-extents.x, -extents.y, -extents.z),
                rotation * new Vector3( extents.x, -extents.y, -extents.z),
                rotation * new Vector3(-extents.x,  extents.y, -extents.z),
                rotation * new Vector3( extents.x,  extents.y, -extents.z),
                rotation * new Vector3(-extents.x, -extents.y,  extents.z),
                rotation * new Vector3( extents.x, -extents.y,  extents.z),
                rotation * new Vector3(-extents.x,  extents.y,  extents.z),
                rotation * new Vector3( extents.x,  extents.y,  extents.z)
            };

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                if (corners[i].x < minX) minX = corners[i].x;
                if (corners[i].x > maxX) maxX = corners[i].x;
                if (corners[i].z < minZ) minZ = corners[i].z;
                if (corners[i].z > maxZ) maxZ = corners[i].z;
            }

            float worldWidth = Mathf.Max(0.2f, maxX - minX);
            float worldDepth = Mathf.Max(0.2f, maxZ - minZ);

            return new Vector2(worldWidth, worldDepth);
        }

        /// <summary>
        /// Compares the prefab's rotated physical dimensions with grid cell dimensions to determine how many cells it occupies.
        /// </summary>
        /// <param name="prefab">Target prefab</param>
        /// <param name="rotation">Spawn rotation</param>
        /// <param name="cellWidth">Width of one grid cell (X axis)</param>
        /// <param name="cellHeight">Height of one grid cell (Z axis)</param>
        /// <returns>Vector2Int where x = columns spanned, y = rows spanned</returns>
        public Vector2Int GetPrefabCellFootprint(GameObject prefab, Quaternion rotation, float cellWidth, float cellHeight)
        {
            Vector2 size = GetPrefabWorldSize(prefab, rotation);
            // 0.82f margin prevents small model overshoots from unnecessarily snapping to extra cells
            int spanCols = Mathf.Max(1, Mathf.CeilToInt((size.x * 0.82f) / cellWidth));
            int spanRows = Mathf.Max(1, Mathf.CeilToInt((size.y * 0.82f) / cellHeight));
            return new Vector2Int(spanCols, spanRows);
        }

        private bool IsLargeBuilding(GameObject prefab)
        {
            if (prefab == null) return false;
            string name = prefab.name.ToLower();
            if (name.Contains("house") || name.Contains("shophouse") || name.Contains("building") || name.Contains("tower") || name.Contains("home") || name.Contains("structure"))
            {
                return true;
            }
            Vector2 size = GetPrefabWorldSize(prefab, Quaternion.identity);
            if (size.x > 1.8f || size.y > 1.8f)
            {
                return true;
            }
            return false;
        }

        private GameObject GetRandomSmallerPrefab(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0) return null;
            
            List<GameObject> smallerPrefabs = new List<GameObject>();
            foreach (var p in prefabs)
            {
                if (!IsLargeBuilding(p))
                {
                    smallerPrefabs.Add(p);
                }
            }
            
            if (smallerPrefabs.Count > 0)
            {
                return smallerPrefabs[Random.Range(0, smallerPrefabs.Count)];
            }
            return null; // Return null if all are large
        }
    }
}

#if UNITY_EDITOR
namespace TheLastEmpire
{
    [UnityEditor.CustomPropertyDrawer(typeof(EnvironmentManager.BiomeEnvConfig))]
    public class BiomeEnvConfigDrawer : UnityEditor.PropertyDrawer
    {
        public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return UnityEditor.EditorGUIUtility.singleLineHeight;
            }

            float totalHeight = UnityEditor.EditorGUIUtility.singleLineHeight + 4f;
            UnityEditor.SerializedProperty prop = property.Copy();
            UnityEditor.SerializedProperty end = property.GetEndProperty();

            if (prop.NextVisible(true))
            {
                while (!UnityEditor.SerializedProperty.EqualContents(prop, end))
                {
                    totalHeight += UnityEditor.EditorGUI.GetPropertyHeight(prop, true) + 2f;
                    if (!prop.NextVisible(false)) break;
                }
            }

            return totalHeight + 4f;
        }

        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            UnityEditor.EditorGUI.BeginProperty(position, label, property);

            // Fetch the current Biome enum display text
            var biomeProp = property.FindPropertyRelative("biome");
            string headerTitle = label.text;
            if (biomeProp != null && biomeProp.enumValueIndex >= 0 && biomeProp.enumValueIndex < biomeProp.enumDisplayNames.Length)
            {
                headerTitle = biomeProp.enumDisplayNames[biomeProp.enumValueIndex];
            }

            Rect foldoutRect = new Rect(position.x, position.y, position.width, UnityEditor.EditorGUIUtility.singleLineHeight);
            property.isExpanded = UnityEditor.EditorGUI.Foldout(foldoutRect, property.isExpanded, headerTitle, true, UnityEditor.EditorStyles.foldoutHeader);

            if (property.isExpanded)
            {
                UnityEditor.EditorGUI.indentLevel++;
                float y = position.y + UnityEditor.EditorGUIUtility.singleLineHeight + 4f;

                UnityEditor.SerializedProperty prop = property.Copy();
                UnityEditor.SerializedProperty end = property.GetEndProperty();

                if (prop.NextVisible(true))
                {
                    while (!UnityEditor.SerializedProperty.EqualContents(prop, end))
                    {
                        float h = UnityEditor.EditorGUI.GetPropertyHeight(prop, true);
                        Rect fieldRect = new Rect(position.x, y, position.width, h);
                        UnityEditor.EditorGUI.PropertyField(fieldRect, prop, true);
                        y += h + 2f;
                        if (!prop.NextVisible(false)) break;
                    }
                }

                UnityEditor.EditorGUI.indentLevel--;
            }

            UnityEditor.EditorGUI.EndProperty();
        }
    }

    [UnityEditor.CustomEditor(typeof(EnvironmentManager))]
    public class EnvironmentManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EnvironmentManager manager = (EnvironmentManager)target;

            GUILayout.Space(15);
            GUILayout.Label("Editor Spawner Preview Options", UnityEditor.EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Preview in Scene View (Edit Mode)"))
            {
                // Clear any existing preview first
                manager.ClearEnvironmentInEditor();

                // Create a dummy stage with selected biome for preview
                StageData dummyStage = new StageData(0, 0, manager.PreviewBiome, 99999);
                
                // Temporarily disable warnings during edit mode generation
                manager.GenerateStageEnvironment(dummyStage);
                
                UnityEditor.SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Generated Preview"))
            {
                manager.ClearEnvironmentInEditor();
                UnityEditor.SceneView.RepaintAll();
            }
        }
    }
}
#endif
