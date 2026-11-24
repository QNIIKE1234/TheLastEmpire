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
        [SerializeField] private bool includeProceduralFoliageAndDebris = true;
        [SerializeField, Range(0f, 1f)] private float proceduralDecorMixChance = 0.7f;
        [SerializeField, Range(0.5f, 3.0f)] private float mapDensityMultiplier = 1.5f;
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

        [Header("Background Scenery Settings")]
        [Tooltip("If true, generates decorative buildings, skyline, and trees on the surrounding corner and edge grounds.")]
        [SerializeField] private bool generateBackgroundScenery = true;
        [SerializeField, Range(1, 10)] private int buildingsPerCorner = 5;
        [SerializeField, Range(0, 12)] private int propsPerCorner = 5;

        [Header("Enterable Buildings")]
        [Tooltip("If true, every playable building with a clear doorway in the stage can be entered. If false, only 1-2 random buildings per stage are unlocked.")]
        [SerializeField] private bool unlockAllEnterableBuildings = true;

        [Header("Road Styling")]
        [Tooltip("Replaces flat legacy Street tiles with textured post-apocalyptic asphalt, concrete curbs, dashed yellow lanes, crosswalks, and drains.")]
        [SerializeField] private bool useStyledAsphaltRoad = true;

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
        public GameObject CurrentEnvContainer => _currentEnvContainer;

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
                if (TransitionPortal.IsWorldPortal(portal))
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

            // Fetch player position early to avoid spawning obstacles directly on top of them or their warp-in point
            Vector3 playerPos = TransitionPortal.HasValidWarpHistory ? TransitionPortal.LastPlayerWarpInPos : Vector3.zero;
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerPos = player.transform.position;
            }

            // 3a. Handcrafted JSON Stage Template Override
            if (StageTemplateManager.Instance != null && StageTemplateManager.Instance.TryGetTemplateForStage(stage, out StageTemplateData template))
            {
                GenerateEnvironmentFromTemplate(template, stage);
                EnsurePortalsClear(playerPos);
                AttachEnterableBuildingDoors(stage);
                if (generateBackgroundScenery)
                {
                    GenerateSurroundingBackgroundScenery(stage, config);
                }
                return;
            }

            // 3b. Custom grid spawner override
            if (useInspectorGrid)
            {
                // Generate visual street tiles on cells that lie on portal paths
                GenerateVisualStreetTiles(config, activePathSegments, rows, cols, startX, startY, cellWidth, cellHeight);

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

                        bool isOnPath = IsPointOnWalkablePath(spawnPos, activePathSegments, GetWalkablePathThreshold(cellWidth, cellHeight));

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
                            Vector2 worldSize = GetPrefabWorldSize(prefab, rotation);
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

                            // Place model at the center of its multi-cell footprint
                            float centerX = startX + (c + spanCols * 0.5f) * cellWidth;
                            float centerZ = startY + (r + spanRows * 0.5f) * cellHeight;

                            if (DoesFootprintOverlapPortalOrPlayer(new Vector2(centerX, centerZ), worldSize, playerPos))
                            {
                                continue;
                            }

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

                EnsurePortalsClear(playerPos);
                AttachEnterableBuildingDoors(stage);
                if (generateBackgroundScenery)
                {
                    GenerateSurroundingBackgroundScenery(stage, config);
                }
                return; // Skip normal random spawning!
            }

            // 4. Init deterministic state using stage seed (offset slightly to not clash with enemy spawns)
            Random.InitState(stage.stageSeed + 12345);

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
            GenerateVisualStreetTiles(config, activePathSegments, rows, cols, startX, startY, cellWidth, cellHeight);

            // Initialize 2D Occupancy Grid and Path Grid
            bool[,] occupiedGrid = new bool[rows, cols];
            bool[,] isPathGrid = new bool[rows, cols];
            float safeRadius = Mathf.Max(2.8f, Mathf.Max(cellWidth, cellHeight) * 0.85f);

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
                    if (IsPointOnWalkablePath(cellCenter, activePathSegments, GetWalkablePathThreshold(cellWidth, cellHeight)))
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

            int obstacleCount = Mathf.RoundToInt(Random.Range(config.minObstacles, config.maxObstacles + 1) * mapDensityMultiplier);
            int decorationCount = Mathf.RoundToInt(Random.Range(config.minDecorations, config.maxDecorations + 1) * mapDensityMultiplier);

            int buildingSpawnCount = Mathf.Max(8, Mathf.RoundToInt(obstacleCount * 0.85f));
            int pathSpawnCount = Mathf.Max(2, obstacleCount - Mathf.RoundToInt(obstacleCount * 0.7f));
            int spawnDecorations = Mathf.Min(decorationCount, decorationSafeCells.Count);

            // Track exact world-space XZ bounding boxes of every spawned object to guarantee zero physical overlap
            List<Rect> placedWorldRects = new List<Rect>();
            // Allow 2.5m outer margin so multi-cell buildings along outer sidewalk rows are not rejected at the map edge
            Rect stageBoundsRect = new Rect(startX - 2.5f, startY - 2.5f, gridWidth + 5.0f, gridHeight + 5.0f);

            // Prepare shuffled off-path and on-path candidate cell lists deterministically
            List<Vector2Int> offPathCells = new List<Vector2Int>();
            List<Vector2Int> onPathCells = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (occupiedGrid[r, c]) continue;
                    if (isPathGrid[r, c]) onPathCells.Add(new Vector2Int(c, r));
                    else offPathCells.Add(new Vector2Int(c, r));
                }
            }
            ShuffleCellList(offPathCells);
            ShuffleCellList(onPathCells);

            // 5a. Generate off-path buildings (prioritize actual building prefabs; check all 4 rotations at each candidate position, then move position)
            for (int i = 0; i < buildingSpawnCount; i++)
            {
                GameObject prefab = GetRandomBuildingPrefab(config.obstaclePrefabs, config.decorationPrefabs) ?? GetRandomPrefab(config.obstaclePrefabs);
                if (prefab == null) continue;

                if (TryFindNonOverlappingPlacement(
                    prefab, offPathCells, occupiedGrid, isPathGrid, placedWorldRects,
                    rows, cols, startX, startY, cellWidth, cellHeight, stageBoundsRect, playerPos,
                    requireOffPath: true, requireOnPath: false, padding: 0.10f,
                    out Vector3 worldPos, out Quaternion rotation, out Vector2Int footprint, out Vector2Int anchor, out Rect placedRect))
                {
                    ReserveGridFootprint(occupiedGrid, anchor.x, anchor.y, footprint.x, footprint.y, rows, cols);
                    placedWorldRects.Add(placedRect);

                    GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                    obj.name = prefab.name;
                }
                else
                {
                    // Multi-cell prefab couldn't fit or wasn't available; place a compact 1x1 procedural house/building first
                    bool placedCompactBuilding = false;
                    for (int ci = 0; ci < offPathCells.Count; ci++)
                    {
                        Vector2Int cell = offPathCells[ci];
                        if (occupiedGrid[cell.y, cell.x] || isPathGrid[cell.y, cell.x]) continue;

                        float cx = startX + (cell.x + 0.5f) * cellWidth;
                        float cz = startY + (cell.y + 0.5f) * cellHeight;
                        Rect bRect = new Rect(cx - 1.35f, cz - 1.35f, 2.7f, 2.7f);

                        if (bRect.xMin < stageBoundsRect.xMin || bRect.xMax > stageBoundsRect.xMax ||
                            bRect.yMin < stageBoundsRect.yMin || bRect.yMax > stageBoundsRect.yMax)
                        {
                            continue;
                        }

                        if (!OverlapsPlacedRects(bRect, placedWorldRects) && !DoesRectOverlapPortalOrPlayer(bRect, playerPos))
                        {
                            occupiedGrid[cell.y, cell.x] = true;
                            placedWorldRects.Add(bRect);
                            ProceduralPropBuilder.SpawnBuilding(_currentEnvContainer.transform, new Vector3(cx, cz, 0f), snapToGrid, out _);
                            placedCompactBuilding = true;
                            break;
                        }
                    }

                    if (!placedCompactBuilding)
                    {
                        GameObject smallerPrefab = GetRandomSmallerPrefab(config.obstaclePrefabs);
                        if (smallerPrefab != null && TryFindNonOverlappingPlacement(
                            smallerPrefab, offPathCells, occupiedGrid, isPathGrid, placedWorldRects,
                            rows, cols, startX, startY, cellWidth, cellHeight, stageBoundsRect, playerPos,
                            requireOffPath: true, requireOnPath: false, padding: 0.20f,
                            out Vector3 sPos, out Quaternion sRot, out Vector2Int sFoot, out Vector2Int sAnchor, out Rect sRect))
                        {
                            ReserveGridFootprint(occupiedGrid, sAnchor.x, sAnchor.y, sFoot.x, sFoot.y, rows, cols);
                            placedWorldRects.Add(sRect);

                            GameObject obj = Instantiate(smallerPrefab, sPos, sRot, _currentEnvContainer.transform);
                            obj.name = smallerPrefab.name;
                        }
                    }
                }
            }

            // 5b. Generate on-path obstacles / vehicles (rotate-first, then move position to prevent vehicle-to-building/vehicle overlap)
            for (int i = 0; i < pathSpawnCount; i++)
            {
                GameObject prefab = GetRandomSmallerPrefab(config.obstaclePrefabs);
                if (prefab != null)
                {
                    if (TryFindNonOverlappingPlacement(
                        prefab, onPathCells, occupiedGrid, isPathGrid, placedWorldRects,
                        rows, cols, startX, startY, cellWidth, cellHeight, stageBoundsRect, playerPos,
                        requireOffPath: false, requireOnPath: true, padding: 0.25f,
                        out Vector3 worldPos, out Quaternion rotation, out Vector2Int footprint, out Vector2Int anchor, out Rect placedRect))
                    {
                        ReserveGridFootprint(occupiedGrid, anchor.x, anchor.y, footprint.x, footprint.y, rows, cols);
                        placedWorldRects.Add(placedRect);

                        GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                        obj.name = prefab.name;
                    }
                }
                else if (useProceduralFallbacks)
                {
                    for (int ci = 0; ci < onPathCells.Count; ci++)
                    {
                        Vector2Int cell = onPathCells[ci];
                        if (occupiedGrid[cell.y, cell.x]) continue;
                        float cx = startX + (cell.x + 0.5f) * cellWidth;
                        float cz = startY + (cell.y + 0.5f) * cellHeight;
                        Rect rRect = new Rect(cx - 0.7f, cz - 0.7f, 1.4f, 1.4f);
                        if (!OverlapsPlacedRects(rRect, placedWorldRects) && !DoesRectOverlapPortalOrPlayer(rRect, playerPos))
                        {
                            occupiedGrid[cell.y, cell.x] = true;
                            placedWorldRects.Add(rRect);
                            CreateProceduralObstacle(new Vector3(cx, cz, 0f), stage.biome, false);
                            break;
                        }
                    }
                }
            }

            // 6. Generate Decorations (supports both small props and large buildings with full rotate-then-move overlap checks)
            int spawnedDecorCount = 0;
            List<Vector2Int> decorCandidateCoords = new List<Vector2Int>(decorationSafeCells.Count);
            for (int i = 0; i < decorationSafeCells.Count; i++)
            {
                decorCandidateCoords.Add(new Vector2Int(decorationSafeCells[i].col, decorationSafeCells[i].row));
            }

            for (int i = 0; i < decorationSafeCells.Count && spawnedDecorCount < spawnDecorations; i++)
            {
                bool useProceduralProp = includeProceduralFoliageAndDebris && (config.decorationPrefabs == null || config.decorationPrefabs.Count == 0 || Random.value < proceduralDecorMixChance);
                GameObject prefab = useProceduralProp ? null : GetRandomPrefab(config.decorationPrefabs);
                if (prefab != null && prefab.name.Equals("Street", System.StringComparison.OrdinalIgnoreCase))
                {
                    prefab = null;
                }

                if (prefab != null)
                {
                    if (TryFindNonOverlappingPlacement(
                        prefab, decorCandidateCoords, occupiedGrid, isPathGrid, placedWorldRects,
                        rows, cols, startX, startY, cellWidth, cellHeight, stageBoundsRect, playerPos,
                        requireOffPath: true, requireOnPath: false, padding: 0.20f,
                        out Vector3 worldPos, out Quaternion rotation, out Vector2Int footprint, out Vector2Int anchor, out Rect placedRect))
                    {
                        ReserveGridFootprint(occupiedGrid, anchor.x, anchor.y, footprint.x, footprint.y, rows, cols);
                        placedWorldRects.Add(placedRect);

                        GameObject obj = Instantiate(prefab, worldPos, rotation, _currentEnvContainer.transform);
                        obj.name = prefab.name;
                        spawnedDecorCount++;
                    }
                }
                else if (useProceduralFallbacks || includeProceduralFoliageAndDebris)
                {
                    // Pass 6a focuses on urban debris/trash props; trees/bushes/grass are spawned in separate Pass 6b
                    ProceduralPropBuilder.PropCategory category = ProceduralPropBuilder.PropCategory.Debris;
                    float footprintSize = ProceduralPropBuilder.GetFootprintSize(category);
                    float half = footprintSize * 0.5f;

                    for (int ci = 0; ci < decorCandidateCoords.Count; ci++)
                    {
                        Vector2Int coord = decorCandidateCoords[(i + ci) % decorCandidateCoords.Count];
                        if (occupiedGrid[coord.y, coord.x] || isPathGrid[coord.y, coord.x]) continue;

                        float cx = startX + (coord.x + 0.5f) * cellWidth;
                        float cz = startY + (coord.y + 0.5f) * cellHeight;
                        Rect rRect = new Rect(cx - half, cz - half, footprintSize, footprintSize);

                        if (rRect.xMin < stageBoundsRect.xMin || rRect.xMax > stageBoundsRect.xMax ||
                            rRect.yMin < stageBoundsRect.yMin || rRect.yMax > stageBoundsRect.yMax)
                        {
                            continue;
                        }

                        if (!OverlapsPlacedRects(rRect, placedWorldRects) && !DoesRectOverlapPortalOrPlayer(rRect, playerPos))
                        {
                            ProceduralPropBuilder.SpawnProp(category, _currentEnvContainer.transform, new Vector3(cx, cz, 0f), propBaseMaterial, snapToGrid);
                            occupiedGrid[coord.y, coord.x] = true;
                            placedWorldRects.Add(rRect);
                            spawnedDecorCount++;
                            break;
                        }
                    }
                }
            }

            // 6b. Separate Foliage / Tree / Bush / Grass Pass:
            // Independent of building cell occupancy — fills open yards/edges around houses without overlapping buildings, cars, or portals
            if (includeProceduralFoliageAndDebris)
            {
                SpawnSeparateFoliagePass(stage.biome, isPathGrid, placedWorldRects, rows, cols, startX, startY, cellWidth, cellHeight, stageBoundsRect, playerPos);
            }

            // Final safety pass: ensure no spawned building/obstacle physically overlaps any active portal or the player
            EnsurePortalsClear(playerPos);

            // Attach interactive enterable doorways to a deterministic subset of spawned buildings/houses
            AttachEnterableBuildingDoors(stage);

            if (generateBackgroundScenery)
            {
                GenerateSurroundingBackgroundScenery(stage, config);
            }

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

        private float GetPortalSafeRadius()
        {
            float cellW = gridWidth / Mathf.Max(1, gridCols);
            float cellH = gridHeight / Mathf.Max(1, gridRows);
            return Mathf.Max(3.6f, Mathf.Max(cellW, cellH) * 0.9f);
        }

        private bool IsNearTransitionGates(Vector3 pos)
        {
            return IsNearTransitionGates(new Vector2(pos.x, pos.y));
        }

        private bool IsNearTransitionGates(Vector2 pos)
        {
            float portalSafeRadius = GetPortalSafeRadius();

            // Find all transition portals in the scene dynamically
            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            foreach (TransitionPortal portal in portals)
            {
                if (TransitionPortal.IsWorldPortal(portal))
                {
                    // Check if the portal is enabled (only check col.enabled in play mode, assume active in editor preview)
                    Collider col = portal.GetComponent<Collider>();
                    if (Application.isPlaying && col != null && !col.enabled)
                    {
                        continue; // Path is walled off, safe to spawn obstacles here
                    }

                    // Protect both the portal ring position AND its inward player spawn position (entryOffset)
                    Vector2 portalPos = new Vector2(portal.transform.position.x, portal.transform.position.z);
                    Vector3 inward3D = portal.GetInwardEntryPosition();
                    Vector2 entryPos = new Vector2(inward3D.x, inward3D.z);

                    if (DistanceToSegment(pos, portalPos, entryPos) < portalSafeRadius)
                    {
                        return true;
                    }
                }
            }

            // Fallback to static boundary checks if no portals are present in the scene
            if (portals.Length == 0)
            {
                if (Mathf.Abs(pos.x - gridCenterOffset.x) < 2.5f && pos.y > (gridHeight * 0.5f + gridCenterOffset.y - 2.0f)) return true;
                if (Mathf.Abs(pos.x - gridCenterOffset.x) < 2.5f && pos.y < (-gridHeight * 0.5f + gridCenterOffset.y + 2.0f)) return true;
                if (pos.x < (-gridWidth * 0.5f + gridCenterOffset.x + 2.0f) && Mathf.Abs(pos.y - gridCenterOffset.y) < 2.5f) return true;
                if (pos.x > (gridWidth * 0.5f + gridCenterOffset.x - 2.0f) && Mathf.Abs(pos.y - gridCenterOffset.y) < 2.5f) return true;
            }

            return false;
        }

        private static float DistancePointToRect(Vector2 pt, float minX, float maxX, float minZ, float maxZ)
        {
            float dx = Mathf.Max(minX - pt.x, 0f, pt.x - maxX);
            float dz = Mathf.Max(minZ - pt.y, 0f, pt.y - maxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Checks whether an object's 2D world footprint (centered at centerXZ with size sizeXZ)
        /// overlaps or comes too close to any active TransitionPortal's doorway/entry zone or the player.
        /// </summary>
        private bool DoesFootprintOverlapPortalOrPlayer(Vector2 centerXZ, Vector2 sizeXZ, Vector3 playerPos, float portalPadding = 2.6f, float playerPadding = 2.0f)
        {
            Rect rect = new Rect(
                centerXZ.x - sizeXZ.x * 0.5f,
                centerXZ.y - sizeXZ.y * 0.5f,
                sizeXZ.x,
                sizeXZ.y);
            return DoesRectOverlapPortalOrPlayer(rect, playerPos, portalPadding, playerPadding);
        }

        private bool DoesRectOverlapPortalOrPlayer(Rect rect, Vector3 playerPos, float portalPadding = 2.6f, float playerPadding = 2.0f)
        {
            float minX = rect.xMin;
            float maxX = rect.xMax;
            float minZ = rect.yMin;
            float maxZ = rect.yMax;

            Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);
            if (DistancePointToRect(playerXZ, minX, maxX, minZ, maxZ) < playerPadding)
            {
                return true;
            }

            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            foreach (TransitionPortal portal in portals)
            {
                if (!TransitionPortal.IsWorldPortal(portal)) continue;

                Collider col = portal.GetComponent<Collider>();
                if (Application.isPlaying && col != null && !col.enabled) continue;

                Vector2 portalPos = new Vector2(portal.transform.position.x, portal.transform.position.z);
                Vector3 inward3D = portal.GetInwardEntryPosition();
                Vector2 entryPos = new Vector2(inward3D.x, inward3D.z);
                Vector2 midPos = (portalPos + entryPos) * 0.5f;
                Vector2 inwardCorridorPos = entryPos + (entryPos - portalPos);

                if (DistancePointToRect(portalPos, minX, maxX, minZ, maxZ) < portalPadding ||
                    DistancePointToRect(midPos, minX, maxX, minZ, maxZ) < portalPadding ||
                    DistancePointToRect(entryPos, minX, maxX, minZ, maxZ) < portalPadding ||
                    DistancePointToRect(inwardCorridorPos, minX, maxX, minZ, maxZ) < portalPadding * 0.85f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsPlacedRects(Rect candidateRect, List<Rect> placedRects)
        {
            for (int i = 0; i < placedRects.Count; i++)
            {
                if (candidateRect.Overlaps(placedRects[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ShuffleCellList(List<Vector2Int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector2Int temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private static void ReserveGridFootprint(bool[,] occupiedGrid, int startC, int startR, int spanCols, int spanRows, int rows, int cols)
        {
            int endR = Mathf.Min(rows, startR + spanRows);
            int endC = Mathf.Min(cols, startC + spanCols);
            for (int r = Mathf.Max(0, startR); r < endR; r++)
            {
                for (int c = Mathf.Max(0, startC); c < endC; c++)
                {
                    occupiedGrid[r, c] = true;
                }
            }
        }

        private void SpawnSeparateFoliagePass(
            BiomeType biome,
            bool[,] isPathGrid,
            List<Rect> placedWorldRects,
            int rows,
            int cols,
            float startX,
            float startY,
            float cellWidth,
            float cellHeight,
            Rect stageBoundsRect,
            Vector3 playerPos)
        {
            List<Vector2Int> foliageCells = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!isPathGrid[r, c])
                    {
                        foliageCells.Add(new Vector2Int(c, r));
                    }
                }
            }
            ShuffleCellList(foliageCells);

            int targetFoliage = Mathf.RoundToInt(18 * mapDensityMultiplier);
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(40, foliageCells.Count * 3);

            while (spawned < targetFoliage && attempts < maxAttempts && foliageCells.Count > 0)
            {
                Vector2Int cell = foliageCells[attempts % foliageCells.Count];
                attempts++;

                float roll = Random.value;
                ProceduralPropBuilder.PropCategory category;
                if (biome == BiomeType.OvergrownForests)
                {
                    category = roll < 0.35f ? ProceduralPropBuilder.PropCategory.Tree :
                               roll < 0.65f ? ProceduralPropBuilder.PropCategory.Bush :
                               ProceduralPropBuilder.PropCategory.Grass;
                }
                else
                {
                    category = roll < 0.25f ? ProceduralPropBuilder.PropCategory.Tree :
                               roll < 0.55f ? ProceduralPropBuilder.PropCategory.Bush :
                               ProceduralPropBuilder.PropCategory.Grass;
                }

                float footprintSize = ProceduralPropBuilder.GetFootprintSize(category);
                float half = footprintSize * 0.5f;

                float jitterX = Random.Range(-cellWidth * 0.32f, cellWidth * 0.32f);
                float jitterZ = Random.Range(-cellHeight * 0.32f, cellHeight * 0.32f);
                float cx = startX + (cell.x + 0.5f) * cellWidth + jitterX;
                float cz = startY + (cell.y + 0.5f) * cellHeight + jitterZ;

                Rect fRect = new Rect(cx - half, cz - half, footprintSize, footprintSize);
                if (fRect.xMin < stageBoundsRect.xMin || fRect.xMax > stageBoundsRect.xMax ||
                    fRect.yMin < stageBoundsRect.yMin || fRect.yMax > stageBoundsRect.yMax)
                {
                    continue;
                }

                // Ensure foliage does not overlap buildings, houses, cars, debris, portals, or player
                if (OverlapsPlacedRects(fRect, placedWorldRects) || DoesRectOverlapPortalOrPlayer(fRect, playerPos))
                {
                    continue;
                }

                ProceduralPropBuilder.SpawnProp(category, _currentEnvContainer.transform, new Vector3(cx, cz, 0f), propBaseMaterial, snapToGrid);
                placedWorldRects.Add(fRect);
                spawned++;
            }
        }

        /// <summary>
        /// Searches candidate positions and tries all 4 cardinal rotations at each position first.
        /// If all rotations overlap existing objects, portals, player, or grid/stage constraints,
        /// moves to the next candidate position and repeats until a valid placement is found.
        /// </summary>
        private bool TryFindNonOverlappingPlacement(
            GameObject prefab,
            List<Vector2Int> candidateCells,
            bool[,] occupiedGrid,
            bool[,] isPathGrid,
            List<Rect> placedWorldRects,
            int rows,
            int cols,
            float startX,
            float startY,
            float cellWidth,
            float cellHeight,
            Rect stageBoundsRect,
            Vector3 playerPos,
            bool requireOffPath,
            bool requireOnPath,
            float padding,
            out Vector3 worldPos,
            out Quaternion rotation,
            out Vector2Int footprint,
            out Vector2Int anchor,
            out Rect placedRect)
        {
            worldPos = Vector3.zero;
            rotation = Quaternion.identity;
            footprint = Vector2Int.one;
            anchor = Vector2Int.zero;
            placedRect = default;

            if (prefab == null || candidateCells == null || candidateCells.Count == 0)
                return false;

            float[] yawAngles = { 0f, 90f, 180f, 270f };

            for (int cellIdx = 0; cellIdx < candidateCells.Count; cellIdx++)
            {
                Vector2Int cell = candidateCells[cellIdx];
                int c = cell.x;
                int r = cell.y;

                if (r < 0 || r >= rows || c < 0 || c >= cols)
                    continue;
                if (occupiedGrid[r, c])
                    continue;
                if (requireOffPath && isPathGrid[r, c])
                    continue;
                if (requireOnPath && !isPathGrid[r, c])
                    continue;

                // Shuffle the 4 rotations for visual variety while checking every rotation at this position first
                for (int i = yawAngles.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    float tmp = yawAngles[i];
                    yawAngles[i] = yawAngles[j];
                    yawAngles[j] = tmp;
                }

                for (int rotIdx = 0; rotIdx < yawAngles.Length; rotIdx++)
                {
                    Quaternion candidateRot = Quaternion.Euler(0f, yawAngles[rotIdx], 0f) * prefab.transform.rotation;
                    Vector2Int candidateFootprint = GetPrefabCellFootprint(prefab, candidateRot, cellWidth, cellHeight);
                    int spanC = Mathf.Max(1, candidateFootprint.x);
                    int spanR = Mathf.Max(1, candidateFootprint.y);

                    if (c + spanC > cols || r + spanR > rows)
                        continue;

                    bool gridBlocked = false;
                    for (int rr = r; rr < r + spanR && !gridBlocked; rr++)
                    {
                        for (int cc = c; cc < c + spanC; cc++)
                        {
                            if (occupiedGrid[rr, cc] || (requireOffPath && isPathGrid[rr, cc]))
                            {
                                gridBlocked = true;
                                break;
                            }
                        }
                    }
                    if (gridBlocked)
                        continue;

                    Vector3 candidatePos = new Vector3(
                        startX + (c + spanC * 0.5f) * cellWidth,
                        prefabSpawnOffsetHeight,
                        startY + (r + spanR * 0.5f) * cellHeight);

                    if (snapToGrid && !requireOnPath && spanC == 1 && spanR == 1)
                    {
                        candidatePos = new Vector3(
                            startX + (c + 0.5f) * cellWidth,
                            prefabSpawnOffsetHeight,
                            startY + (r + 0.5f) * cellHeight);
                    }

                    Rect candidateRect = GetPrefabWorldRect(prefab, candidatePos, candidateRot, padding);

                    if (candidateRect.xMin < stageBoundsRect.xMin ||
                        candidateRect.xMax > stageBoundsRect.xMax ||
                        candidateRect.yMin < stageBoundsRect.yMin ||
                        candidateRect.yMax > stageBoundsRect.yMax)
                    {
                        continue;
                    }

                    if (DoesRectOverlapPortalOrPlayer(candidateRect, playerPos))
                        continue;

                    if (OverlapsPlacedRects(candidateRect, placedWorldRects))
                        continue;

                    worldPos = candidatePos;
                    rotation = candidateRot;
                    footprint = new Vector2Int(spanC, spanR);
                    anchor = cell;
                    placedRect = candidateRect;
                    return true;
                }
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
            ProceduralPropBuilder.PropCategory category = ProceduralPropBuilder.PickCategoryForBiome(biome, out _);
            ProceduralPropBuilder.SpawnProp(category, _currentEnvContainer.transform, pos, propBaseMaterial, snapToGrid);
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

        private static float GetWalkablePathThreshold(float cellWidth, float cellHeight)
        {
            return Mathf.Min(2.0f, Mathf.Min(cellWidth, cellHeight) * 0.48f);
        }

        private void GenerateVisualStreetTiles(
            BiomeEnvConfig config,
            List<KeyValuePair<Vector2, Vector2>> activePathSegments,
            int rows,
            int cols,
            float startX,
            float startY,
            float cellWidth,
            float cellHeight)
        {
            float threshold = GetWalkablePathThreshold(cellWidth, cellHeight);
            bool[,] roadMask = new bool[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = startX + (c + 0.5f) * cellWidth;
                    float z = startY + (r + 0.5f) * cellHeight;
                    if (IsPointOnWalkablePath(new Vector2(x, z), activePathSegments, threshold))
                    {
                        roadMask[r, c] = true;
                    }
                }
            }

            Material fallbackRoadMat = null;
            const float seamOverlap = 1.015f;

            if (useStyledAsphaltRoad)
            {
                EnsureStyledRoadMaterials();
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!roadMask[r, c]) continue;

                    float x = startX + (c + 0.5f) * cellWidth;
                    float z = startY + (r + 0.5f) * cellHeight;

                    bool hasLeft = c > 0 && roadMask[r, c - 1];
                    bool hasRight = c < cols - 1 && roadMask[r, c + 1];
                    bool hasDown = r > 0 && roadMask[r - 1, c];
                    bool hasUp = r < rows - 1 && roadMask[r + 1, c];
                    bool isVerticalOnly = (hasUp || hasDown) && !(hasLeft || hasRight);

                    if (useStyledAsphaltRoad)
                    {
                        SpawnStyledAsphaltTile(x, z, cellWidth, cellHeight, seamOverlap, hasLeft, hasRight, hasUp, hasDown, r, c);
                        continue;
                    }

                    GameObject streetPrefab = GetRandomPrefab(config.streetPrefabs);
                    if (streetPrefab != null)
                    {
                        float spawnY = Mathf.Max(0.01f, streetPrefab.transform.position.y);
                        Vector3 worldPos = new Vector3(x, spawnY, z);
                        Quaternion streetRot = (isVerticalOnly ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity) * streetPrefab.transform.rotation;
                        GameObject tile = Instantiate(streetPrefab, worldPos, streetRot, _currentEnvContainer.transform);
                        tile.name = streetPrefab.name;

                        // Scale X/Z so the tile fills the grid cell seamlessly without gaps between adjacent road tiles
                        Vector2 unrotatedSize = GetPrefabWorldSize(streetPrefab, streetPrefab.transform.rotation);
                        float targetLocalX = (isVerticalOnly ? cellHeight : cellWidth) * seamOverlap;
                        float targetLocalZ = (isVerticalOnly ? cellWidth : cellHeight) * seamOverlap;
                        Vector3 baseScale = tile.transform.localScale;
                        tile.transform.localScale = new Vector3(
                            baseScale.x * (targetLocalX / Mathf.Max(0.1f, unrotatedSize.x)),
                            baseScale.y,
                            baseScale.z * (targetLocalZ / Mathf.Max(0.1f, unrotatedSize.y)));
                    }
                    else if (useProceduralFallbacks)
                    {
                        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        road.name = "ProceduralRoadTile";
                        road.transform.parent = _currentEnvContainer.transform;
                        road.transform.position = new Vector3(x, 0.02f, z);
                        road.transform.localScale = new Vector3(cellWidth * seamOverlap, 0.01f, cellHeight * seamOverlap);

                        Collider col = road.GetComponent<Collider>();
                        if (col != null)
                        {
                            if (Application.isPlaying) Destroy(col);
                            else DestroyImmediate(col);
                        }

                        Renderer rend = road.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            if (fallbackRoadMat == null)
                            {
                                fallbackRoadMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                fallbackRoadMat.color = new Color(0.18f, 0.18f, 0.2f);
                            }
                            rend.material = fallbackRoadMat;
                            ApplyCardboardTextureToChildren(road);
                        }
                    }
                }
            }
        }

        private static Texture2D s_AsphaltTex;
        private static Texture2D s_CurbStoneTex;
        private static Texture2D s_RecessShadowTex;
        private static Material s_AsphaltMat;
        private static Material s_CurbMat;
        private static Material s_CurbRimMat;
        private static Material s_CurbFaceMat;
        private static Material s_CurbGutterMat;
        private static Material s_RecessShadowMat;
        private static Material s_RecessShadowSoftMat;
        private static Material s_YellowPaintMat;
        private static Material s_WhitePaintMat;
        private static Material s_IronGrateMat;

        private static void EnsureStyledRoadMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader == null) return;

            if (s_AsphaltTex == null)
            {
                const int sz = 256;
                s_AsphaltTex = new Texture2D(sz, sz, TextureFormat.RGBA32, true);
                s_AsphaltTex.name = "ProceduralWeatheredAsphalt";
                s_AsphaltTex.wrapMode = TextureWrapMode.Repeat;
                s_AsphaltTex.filterMode = FilterMode.Bilinear;
                Color32[] pixels = new Color32[sz * sz];

                for (int y = 0; y < sz; y++)
                {
                    // Subtle darker tire-track bands along road width
                    float v = y / (float)sz;
                    float trackA = Mathf.Exp(-Mathf.Pow((v - 0.30f) / 0.10f, 2f));
                    float trackB = Mathf.Exp(-Mathf.Pow((v - 0.70f) / 0.10f, 2f));
                    float trackDarken = (trackA + trackB) * 0.042f;

                    for (int x = 0; x < sz; x++)
                    {
                        float u = x / (float)sz;
                        // Deterministic hash grain (asphalt aggregate speckles)
                        uint h = (uint)(x * 374761393 + y * 668265263);
                        h = (h ^ (h >> 13)) * 1274126177u;
                        float grain = ((h ^ (h >> 16)) & 255u) / 255f;

                        // Broad weathered blotches via Perlin noise
                        float blotch = Mathf.PerlinNoise(u * 5.0f + 1.3f, v * 5.0f + 4.7f);
                        // Hairline asphalt tar-crack pattern
                        float crackN = Mathf.Abs(Mathf.PerlinNoise(u * 12.0f + 9.2f, v * 12.0f + 2.1f) - 0.5f);
                        float crack = crackN < 0.014f ? 0.065f : 0f;

                        // Slightly darker & subtly warm-charcoal so road visually sinks below warm stone sidewalk
                        float baseVal = 0.168f + (blotch - 0.5f) * 0.048f + (grain - 0.5f) * 0.058f - trackDarken - crack;
                        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((baseVal * 1.04f) * 255f), 22, 84);
                        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((baseVal * 1.00f) * 255f), 21, 80);
                        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((baseVal * 0.94f) * 255f), 20, 76);
                        pixels[y * sz + x] = new Color32(r, g, b, 255);
                    }
                }
                s_AsphaltTex.SetPixels32(pixels);
                s_AsphaltTex.Apply(true, true);
            }

            if (s_CurbStoneTex == null)
            {
                // Warm sandstone curb-block texture matching cobblestone sidewalk tones
                const int sz = 128;
                s_CurbStoneTex = new Texture2D(sz, sz, TextureFormat.RGBA32, true);
                s_CurbStoneTex.name = "ProceduralSandstoneCurb";
                s_CurbStoneTex.wrapMode = TextureWrapMode.Repeat;
                s_CurbStoneTex.filterMode = FilterMode.Bilinear;
                Color32[] pixels = new Color32[sz * sz];
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        float u = x / (float)sz;
                        float v = y / (float)sz;
                        float n = Mathf.PerlinNoise(u * 8f + 2.1f, v * 8f + 5.3f);
                        // Curbstone joint lines every 32px along block length
                        bool isJoint = (x % 32) <= 1;
                        float shade = (isJoint ? 0.72f : 1.0f) * (0.90f + n * 0.18f);
                        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(0.64f * shade * 255f), 70, 210);
                        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(0.55f * shade * 255f), 60, 185);
                        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(0.44f * shade * 255f), 48, 155);
                        pixels[y * sz + x] = new Color32(r, g, b, 255);
                    }
                }
                s_CurbStoneTex.SetPixels32(pixels);
                s_CurbStoneTex.Apply(true, true);
            }

            if (s_RecessShadowTex == null)
            {
                // Smooth vertical alpha gradient: darkest at top (v=1, touching curb wall) -> 0 alpha at bottom (v=0, center road)
                const int w = 8;
                const int h = 64;
                s_RecessShadowTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                s_RecessShadowTex.name = "ProceduralRoadRecessShadow";
                s_RecessShadowTex.wrapMode = TextureWrapMode.Clamp;
                s_RecessShadowTex.filterMode = FilterMode.Bilinear;
                Color32[] pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float t = y / (float)(h - 1); // 0 = far edge, 1 = curb contact
                    // Crisp dark contact crease right at curb + smooth soft ambient drop shadow outward
                    float soft = Mathf.Pow(t, 2.2f);
                    float crease = Mathf.Pow(t, 9.0f) * 0.42f;
                    float alpha = Mathf.Clamp01(soft * 0.56f + crease);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 220);
                    for (int x = 0; x < w; x++)
                    {
                        pixels[y * w + x] = new Color32(16, 12, 9, a);
                    }
                }
                s_RecessShadowTex.SetPixels32(pixels);
                s_RecessShadowTex.Apply(false, true);
            }

            if (s_AsphaltMat == null)
            {
                s_AsphaltMat = new Material(litShader) { name = "Road_WeatheredAsphalt" };
                s_AsphaltMat.mainTexture = s_AsphaltTex;
                if (s_AsphaltMat.HasProperty("_BaseMap")) s_AsphaltMat.SetTexture("_BaseMap", s_AsphaltTex);
                if (s_AsphaltMat.HasProperty("_BaseColor")) s_AsphaltMat.SetColor("_BaseColor", Color.white);
                if (s_AsphaltMat.HasProperty("_Smoothness")) s_AsphaltMat.SetFloat("_Smoothness", 0.14f);
            }

            if (s_CurbMat == null)
            {
                s_CurbMat = new Material(litShader) { name = "Road_SandstoneCurbTop" };
                s_CurbMat.mainTexture = s_CurbStoneTex;
                if (s_CurbMat.HasProperty("_BaseMap")) s_CurbMat.SetTexture("_BaseMap", s_CurbStoneTex);
                if (s_CurbMat.HasProperty("_BaseColor")) s_CurbMat.SetColor("_BaseColor", Color.white);
                if (s_CurbMat.HasProperty("_Smoothness")) s_CurbMat.SetFloat("_Smoothness", 0.10f);
            }

            if (s_CurbRimMat == null)
            {
                // Sunlit chamfer highlight along the top inner step lip -> sharpens "elevated sidewalk" illusion
                s_CurbRimMat = new Material(litShader) { name = "Road_SandstoneCurbRim" };
                Color c = new Color(0.76f, 0.67f, 0.55f);
                s_CurbRimMat.color = c;
                if (s_CurbRimMat.HasProperty("_BaseColor")) s_CurbRimMat.SetColor("_BaseColor", c);
                if (s_CurbRimMat.HasProperty("_Smoothness")) s_CurbRimMat.SetFloat("_Smoothness", 0.16f);
            }

            if (s_CurbFaceMat == null)
            {
                // Shadowed vertical drop-wall of the elevated sidewalk curb stepping down to the sunken road
                s_CurbFaceMat = new Material(litShader) { name = "Road_SandstoneCurbDropFace" };
                Color c = new Color(0.30f, 0.25f, 0.20f);
                s_CurbFaceMat.color = c;
                if (s_CurbFaceMat.HasProperty("_BaseColor")) s_CurbFaceMat.SetColor("_BaseColor", c);
                if (s_CurbFaceMat.HasProperty("_Smoothness")) s_CurbFaceMat.SetFloat("_Smoothness", 0.08f);
            }

            if (s_CurbGutterMat == null)
            {
                s_CurbGutterMat = new Material(litShader) { name = "Road_GutterEdge" };
                Color c = new Color(0.11f, 0.10f, 0.09f);
                s_CurbGutterMat.color = c;
                if (s_CurbGutterMat.HasProperty("_BaseColor")) s_CurbGutterMat.SetColor("_BaseColor", c);
                if (s_CurbGutterMat.HasProperty("_Smoothness")) s_CurbGutterMat.SetFloat("_Smoothness", 0.08f);
            }

            if (s_RecessShadowMat == null)
            {
                s_RecessShadowMat = CreateTransparentShadowMat("Road_RecessShadowDeep", new Color(1f, 1f, 1f, 0.92f));
            }
            if (s_RecessShadowSoftMat == null)
            {
                s_RecessShadowSoftMat = CreateTransparentShadowMat("Road_RecessShadowSoft", new Color(1f, 1f, 1f, 0.56f));
            }

            if (s_YellowPaintMat == null)
            {
                s_YellowPaintMat = new Material(litShader) { name = "Road_YellowLanePaint" };
                Color c = new Color(0.78f, 0.58f, 0.17f);
                s_YellowPaintMat.color = c;
                if (s_YellowPaintMat.HasProperty("_BaseColor")) s_YellowPaintMat.SetColor("_BaseColor", c);
                if (s_YellowPaintMat.HasProperty("_Smoothness")) s_YellowPaintMat.SetFloat("_Smoothness", 0.10f);
            }

            if (s_WhitePaintMat == null)
            {
                s_WhitePaintMat = new Material(litShader) { name = "Road_WhiteStripePaint" };
                Color c = new Color(0.72f, 0.69f, 0.63f);
                s_WhitePaintMat.color = c;
                if (s_WhitePaintMat.HasProperty("_BaseColor")) s_WhitePaintMat.SetColor("_BaseColor", c);
                if (s_WhitePaintMat.HasProperty("_Smoothness")) s_WhitePaintMat.SetFloat("_Smoothness", 0.10f);
            }

            if (s_IronGrateMat == null)
            {
                s_IronGrateMat = new Material(litShader) { name = "Road_IronManhole" };
                Color c = new Color(0.20f, 0.21f, 0.22f);
                s_IronGrateMat.color = c;
                if (s_IronGrateMat.HasProperty("_BaseColor")) s_IronGrateMat.SetColor("_BaseColor", c);
                if (s_IronGrateMat.HasProperty("_Metallic")) s_IronGrateMat.SetFloat("_Metallic", 0.60f);
                if (s_IronGrateMat.HasProperty("_Smoothness")) s_IronGrateMat.SetFloat("_Smoothness", 0.30f);
            }
        }

        private static Material CreateTransparentShadowMat(string matName, Color tint)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            Material m = new Material(unlit) { name = matName };
            m.mainTexture = s_RecessShadowTex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", s_RecessShadowTex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 2950;
            return m;
        }

        private GameObject CreateRoadPart(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, Quaternion? localRot = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        private void SpawnRecessedCurbEdge(Transform parent, string suffix, Vector3 edgeCenter, float length, float yawDegrees, float faceDepth, float shadowDepth, bool deepShadow)
        {
            // Build an elevated sandstone sidewalk step-lip + sunlit chamfer rim + dark vertical drop-face + soft recessed drop-shadow on the asphalt!
            // In local edge space: +Z points toward sidewalk (outward), -Z points into the road (inward), X runs along the curb length.
            GameObject edgeRoot = new GameObject($"CurbEdge_{suffix}");
            edgeRoot.transform.SetParent(parent, false);
            edgeRoot.transform.localPosition = edgeCenter;
            edgeRoot.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            const float lipW = 0.24f;
            const float lipH = 0.088f;
            const float rimW = 0.055f;
            const float gutterW = 0.09f;

            // 1. Warm sandstone sidewalk curb lip (flush with cobblestone tone, slightly raised visually)
            CreateRoadPart(edgeRoot.transform, "CurbTop", PrimitiveType.Cube,
                new Vector3(0f, lipH * 0.48f, -lipW * 0.5f),
                new Vector3(length, lipH, lipW), s_CurbMat);

            // 2. Crisp sunlit bevel highlight right on the top inner edge of the step
            CreateRoadPart(edgeRoot.transform, "CurbRim", PrimitiveType.Cube,
                new Vector3(0f, lipH * 0.92f + 0.004f, -lipW + rimW * 0.5f),
                new Vector3(length, 0.012f, rimW), s_CurbRimMat);

            // 3. Dark sloped/stepped curb drop-wall visible from top-down camera (creates strong "sidewalk is higher!" read)
            CreateRoadPart(edgeRoot.transform, "CurbDropFace", PrimitiveType.Cube,
                new Vector3(0f, lipH * 0.30f, -lipW - faceDepth * 0.5f),
                new Vector3(length, lipH * 0.54f, faceDepth), s_CurbFaceMat);

            // 4. Deep narrow gutter seam at base of curb wall
            CreateRoadPart(edgeRoot.transform, "GutterCrease", PrimitiveType.Cube,
                new Vector3(0f, 0.014f, -lipW - faceDepth - gutterW * 0.5f),
                new Vector3(length, 0.008f, gutterW), s_CurbGutterMat);

            // 5. Soft alpha-gradient recessed drop shadow fading inward over the asphalt
            float shadowStartZ = -lipW - faceDepth;
            Quaternion flatUp = Quaternion.Euler(90f, 0f, 0f);
            CreateRoadPart(edgeRoot.transform, "RecessShadow", PrimitiveType.Quad,
                new Vector3(0f, 0.024f, shadowStartZ - shadowDepth * 0.5f),
                new Vector3(length, shadowDepth, 1f),
                deepShadow ? s_RecessShadowMat : s_RecessShadowSoftMat,
                flatUp);
        }

        private void SpawnStyledAsphaltTile(
            float x, float z,
            float cellWidth, float cellHeight,
            float seamOverlap,
            bool hasLeft, bool hasRight, bool hasUp, bool hasDown,
            int row, int col,
            bool allowCrosswalk = true)
        {
            GameObject tileRoot = new GameObject($"StreetAsphalt_{row}_{col}");
            tileRoot.transform.SetParent(_currentEnvContainer.transform, false);
            tileRoot.transform.position = new Vector3(x, 0.014f, z);

            float w = cellWidth * seamOverlap;
            float h = cellHeight * seamOverlap;
            float hw = cellWidth * 0.5f;
            float hh = cellHeight * 0.5f;

            // 1. Textured warm-charcoal weathered asphalt slab (sunken visually beneath sandstone curbs)
            CreateRoadPart(tileRoot.transform, "AsphaltSlab", PrimitiveType.Cube, Vector3.zero, new Vector3(w, 0.022f, h), s_AsphaltMat);

            // 2. Elevated sandstone curb lips + vertical drop-walls + recessed drop-shadows ONLY on edges touching sidewalk
            // North (+Z) and West (-X) get deeper shadows & wider visible drop-face matching camera/sun angle -> strong sunken-road illusion!
            const float shoulderInset = 0.62f;
            float horizShoulderLen = Mathf.Max(0.4f, w - ((!hasLeft ? 0.52f : 0f) + (!hasRight ? 0.52f : 0f)));
            float horizShoulderOffsetX = ((!hasLeft ? 0.26f : 0f) - (!hasRight ? 0.26f : 0f));
            float vertShoulderLen = Mathf.Max(0.4f, h - ((!hasUp ? 0.52f : 0f) + (!hasDown ? 0.52f : 0f)));
            float vertShoulderOffsetZ = ((!hasDown ? 0.26f : 0f) - (!hasUp ? 0.26f : 0f));

            if (!hasUp)
            {
                SpawnRecessedCurbEdge(tileRoot.transform, "N", new Vector3(0f, 0f, hh), w, 0f, faceDepth: 0.16f, shadowDepth: 0.88f, deepShadow: true);
                if (cellHeight > 1.8f)
                    CreateRoadPart(tileRoot.transform, "Shoulder_N", PrimitiveType.Cube, new Vector3(horizShoulderOffsetX, 0.016f, hh - shoulderInset), new Vector3(horizShoulderLen, 0.005f, 0.08f), s_WhitePaintMat);
            }
            if (!hasDown)
            {
                SpawnRecessedCurbEdge(tileRoot.transform, "S", new Vector3(0f, 0f, -hh), w, 180f, faceDepth: 0.07f, shadowDepth: 0.44f, deepShadow: false);
                if (cellHeight > 1.8f)
                    CreateRoadPart(tileRoot.transform, "Shoulder_S", PrimitiveType.Cube, new Vector3(horizShoulderOffsetX, 0.016f, -hh + shoulderInset), new Vector3(horizShoulderLen, 0.005f, 0.08f), s_WhitePaintMat);
            }
            if (!hasLeft)
            {
                SpawnRecessedCurbEdge(tileRoot.transform, "W", new Vector3(-hw, 0f, 0f), h, -90f, faceDepth: 0.12f, shadowDepth: 0.72f, deepShadow: true);
                if (cellWidth > 1.8f)
                    CreateRoadPart(tileRoot.transform, "Shoulder_W", PrimitiveType.Cube, new Vector3(-hw + shoulderInset, 0.016f, vertShoulderOffsetZ), new Vector3(0.08f, 0.005f, vertShoulderLen), s_WhitePaintMat);
            }
            if (!hasRight)
            {
                SpawnRecessedCurbEdge(tileRoot.transform, "E", new Vector3(hw, 0f, 0f), h, 90f, faceDepth: 0.10f, shadowDepth: 0.48f, deepShadow: false);
                if (cellWidth > 1.8f)
                    CreateRoadPart(tileRoot.transform, "Shoulder_E", PrimitiveType.Cube, new Vector3(hw - shoulderInset, 0.016f, vertShoulderOffsetZ), new Vector3(0.08f, 0.005f, vertShoulderLen), s_WhitePaintMat);
            }

            bool isSingleHoriz = (hasLeft || hasRight) && !hasUp && !hasDown;
            bool isSingleVert  = (hasUp || hasDown) && !hasLeft && !hasRight;
            bool isIsolatedWideHoriz = !hasLeft && !hasRight && !hasUp && !hasDown && cellWidth >= cellHeight;
            bool isIsolatedTallVert  = !hasLeft && !hasRight && !hasUp && !hasDown && cellHeight > cellWidth;
            bool isTopOfDoubleHoriz  = (hasLeft || hasRight) && hasDown && !hasUp;
            bool isLeftOfDoubleVert  = (hasUp || hasDown) && hasRight && !hasLeft;
            bool isOpenIntersection  = hasLeft && hasRight && hasUp && hasDown;

            // 3. Weathered yellow dashed centerlines (clamped cleanly inside dead-end curbs so dashes never hit end shoulders!)
            if (isSingleHoriz || isIsolatedWideHoriz)
            {
                float usableMinX = -hw + (!hasLeft ? 0.95f : 0.15f);
                float usableMaxX =  hw - (!hasRight ? 0.95f : 0.15f);
                int dashCount = Mathf.Max(1, Mathf.RoundToInt((usableMaxX - usableMinX) / 2.2f));
                float step = (usableMaxX - usableMinX) / dashCount;
                float dashLen = Mathf.Min(1.35f, step * 0.56f);
                for (int d = 0; d < dashCount; d++)
                {
                    float dx = usableMinX + (d + 0.5f) * step;
                    CreateRoadPart(tileRoot.transform, $"LaneDash_H_{d}", PrimitiveType.Cube, new Vector3(dx, 0.017f, 0f), new Vector3(dashLen, 0.005f, 0.13f), s_YellowPaintMat);
                }
                if ((col + row) % 3 == 1 && cellHeight >= 2.6f)
                {
                    CreateRoadPart(tileRoot.transform, "ManholeCover", PrimitiveType.Cylinder, new Vector3(0.15f, 0.015f, -hh * 0.42f), new Vector3(0.64f, 0.010f, 0.64f), s_IronGrateMat);
                }
            }
            else if (isTopOfDoubleHoriz)
            {
                float usableMinX = -hw + (!hasLeft ? 0.95f : 0.15f);
                float usableMaxX =  hw - (!hasRight ? 0.95f : 0.15f);
                int dashCount = Mathf.Max(1, Mathf.RoundToInt((usableMaxX - usableMinX) / 2.2f));
                float step = (usableMaxX - usableMinX) / dashCount;
                float dashLen = Mathf.Min(1.35f, step * 0.56f);
                for (int d = 0; d < dashCount; d++)
                {
                    float dx = usableMinX + (d + 0.5f) * step;
                    CreateRoadPart(tileRoot.transform, $"CenterDash_H_{d}", PrimitiveType.Cube, new Vector3(dx, 0.017f, -hh), new Vector3(dashLen, 0.005f, 0.15f), s_YellowPaintMat);
                }
                if (col % 3 == 1 && cellHeight >= 2.2f)
                {
                    CreateRoadPart(tileRoot.transform, "ManholeCover", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, hh * 0.30f), new Vector3(0.64f, 0.010f, 0.64f), s_IronGrateMat);
                }
            }
            else if (isSingleVert || isIsolatedTallVert)
            {
                float usableMinZ = -hh + (!hasDown ? 0.95f : 0.15f);
                float usableMaxZ =  hh - (!hasUp ? 0.95f : 0.15f);
                int dashCount = Mathf.Max(1, Mathf.RoundToInt((usableMaxZ - usableMinZ) / 2.2f));
                float step = (usableMaxZ - usableMinZ) / dashCount;
                float dashLen = Mathf.Min(1.35f, step * 0.56f);
                for (int d = 0; d < dashCount; d++)
                {
                    float dz = usableMinZ + (d + 0.5f) * step;
                    CreateRoadPart(tileRoot.transform, $"LaneDash_V_{d}", PrimitiveType.Cube, new Vector3(0f, 0.017f, dz), new Vector3(0.13f, 0.005f, dashLen), s_YellowPaintMat);
                }
            }
            else if (isLeftOfDoubleVert)
            {
                float usableMinZ = -hh + (!hasDown ? 0.95f : 0.15f);
                float usableMaxZ =  hh - (!hasUp ? 0.95f : 0.15f);
                int dashCount = Mathf.Max(1, Mathf.RoundToInt((usableMaxZ - usableMinZ) / 2.2f));
                float step = (usableMaxZ - usableMinZ) / dashCount;
                float dashLen = Mathf.Min(1.35f, step * 0.56f);
                for (int d = 0; d < dashCount; d++)
                {
                    float dz = usableMinZ + (d + 0.5f) * step;
                    CreateRoadPart(tileRoot.transform, $"CenterDash_V_{d}", PrimitiveType.Cube, new Vector3(hw, 0.017f, dz), new Vector3(0.15f, 0.005f, dashLen), s_YellowPaintMat);
                }
            }
            else if (isOpenIntersection && (row + col) % 2 == 0)
            {
                CreateRoadPart(tileRoot.transform, "CenterManhole", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.70f, 0.010f, 0.70f), s_IronGrateMat);
            }

            // Crisp faded crosswalk near stage entry/exit ends on horizontal streets
            if (allowCrosswalk && (col == 1 || col == gridCols - 2) && (isTopOfDoubleHoriz || isSingleHoriz))
            {
                float zOff = isTopOfDoubleHoriz ? -hh * 0.35f : 0f;
                for (int s = -1; s <= 1; s++)
                {
                    CreateRoadPart(tileRoot.transform, $"CrossStripe_{s}", PrimitiveType.Cube,
                        new Vector3(0f, 0.017f, zOff + s * 0.58f),
                        new Vector3(0.70f, 0.005f, 0.25f), s_WhitePaintMat);
                }
            }
        }

        private Vector2 SnapWorldToGridCellCenter(Vector2 worldXZ, int rows, int cols, float startX, float startY, float cellWidth, float cellHeight, out Vector2Int cell)
        {
            int c = Mathf.Clamp(Mathf.FloorToInt((worldXZ.x - startX) / cellWidth), 0, cols - 1);
            int r = Mathf.Clamp(Mathf.FloorToInt((worldXZ.y - startY) / cellHeight), 0, rows - 1);
            cell = new Vector2Int(c, r);
            return new Vector2(startX + (c + 0.5f) * cellWidth, startY + (r + 0.5f) * cellHeight);
        }

        private static void AddOrthogonalPath(List<KeyValuePair<Vector2, Vector2>> segments, Vector2 fromCell, Vector2 toCell, bool horizontalFirst)
        {
            if (Mathf.Approximately(fromCell.x, toCell.x) || Mathf.Approximately(fromCell.y, toCell.y))
            {
                segments.Add(new KeyValuePair<Vector2, Vector2>(fromCell, toCell));
                return;
            }

            Vector2 corner = horizontalFirst
                ? new Vector2(toCell.x, fromCell.y)
                : new Vector2(fromCell.x, toCell.y);
            segments.Add(new KeyValuePair<Vector2, Vector2>(fromCell, corner));
            segments.Add(new KeyValuePair<Vector2, Vector2>(corner, toCell));
        }

        private List<KeyValuePair<Vector2, Vector2>> GetActivePathSegments(StageData stage)
        {
            List<KeyValuePair<Vector2, Vector2>> segments = new List<KeyValuePair<Vector2, Vector2>>();

            int cols = Mathf.Max(1, gridCols);
            int rows = Mathf.Max(1, gridRows);
            float cellWidth = gridWidth / cols;
            float cellHeight = gridHeight / rows;
            float startX = gridCenterOffset.x - gridWidth * 0.5f;
            float startY = gridCenterOffset.y - gridHeight * 0.5f;

            // Deterministic seed matching for map consistency
            Random.State oldState = Random.state;
            Random.InitState(stage.stageSeed + 54321);

            // 1. Pick a central junction snapped to a grid cell so all road segments stay strictly orthogonal and connected
            int minCol = cols >= 3 ? 1 : 0;
            int maxCol = cols >= 3 ? cols - 2 : cols - 1;
            int minRow = rows >= 3 ? 1 : 0;
            int maxRow = rows >= 3 ? rows - 2 : rows - 1;
            int jCol = Mathf.Clamp(cols / 2 + Random.Range(-1, 2), minCol, maxCol);
            int jRow = Mathf.Clamp(rows / 2 + Random.Range(-1, 2), minRow, maxRow);
            Vector2 junctionPoint = new Vector2(
                startX + (jCol + 0.5f) * cellWidth,
                startY + (jRow + 0.5f) * cellHeight
            );

            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            List<TransitionPortal> activePortals = new List<TransitionPortal>();

            foreach (TransitionPortal portal in portals)
            {
                if (TransitionPortal.IsWorldPortal(portal))
                {
                    Collider col = portal.GetComponent<Collider>();
                    if (Application.isPlaying && col != null && !col.enabled)
                    {
                        continue;
                    }
                    activePortals.Add(portal);
                }
            }

            // 2. Connect each portal's grid cell to the central junction via orthogonal segments
            bool hasHorizontalRoad = false;
            bool hasVerticalRoad = false;

            foreach (TransitionPortal portal in activePortals)
            {
                Vector2 portalPos = new Vector2(portal.transform.position.x, portal.transform.position.z);
                Vector3 inward3D = portal.GetInwardEntryPosition();
                Vector2 entryPos = new Vector2(inward3D.x, inward3D.z);
                Vector2 inwardCorridorPos = entryPos + (entryPos - portalPos);

                Vector2 portalCell = SnapWorldToGridCellCenter(portalPos, rows, cols, startX, startY, cellWidth, cellHeight, out _);
                Vector2 inwardCell = SnapWorldToGridCellCenter(inwardCorridorPos, rows, cols, startX, startY, cellWidth, cellHeight, out _);

                bool isHorizontalPortal = Mathf.Abs(portal.transform.position.x - gridCenterOffset.x) > gridWidth * 0.35f;
                AddOrthogonalPath(segments, portalCell, inwardCell, isHorizontalPortal);
                AddOrthogonalPath(segments, inwardCell, junctionPoint, isHorizontalPortal);

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
                int targetRow = Random.value < 0.5f ? (rows - 1) : 0;
                Vector2 sideAlleyEnd = new Vector2(junctionPoint.x, startY + (targetRow + 0.5f) * cellHeight);
                segments.Add(new KeyValuePair<Vector2, Vector2>(junctionPoint, sideAlleyEnd));
            }
            // 4. Spawn a horizontal side-alley if there's only a vertical main road!
            else if (hasVerticalRoad && !hasHorizontalRoad && Random.value < 0.85f)
            {
                int targetCol = Random.value < 0.5f ? (cols - 1) : 0;
                Vector2 sideAlleyEnd = new Vector2(startX + (targetCol + 0.5f) * cellWidth, junctionPoint.y);
                segments.Add(new KeyValuePair<Vector2, Vector2>(junctionPoint, sideAlleyEnd));
            }
            // 5. Default crossroad fallback if no portals exist in the scene (e.g. testing)
            else if (activePortals.Count == 0)
            {
                Vector2 leftEnd = new Vector2(startX + 0.5f * cellWidth, junctionPoint.y);
                Vector2 rightEnd = new Vector2(startX + (cols - 0.5f) * cellWidth, junctionPoint.y);
                Vector2 bottomEnd = new Vector2(junctionPoint.x, startY + 0.5f * cellHeight);
                Vector2 topEnd = new Vector2(junctionPoint.x, startY + (rows - 0.5f) * cellHeight);
                segments.Add(new KeyValuePair<Vector2, Vector2>(leftEnd, rightEnd));
                segments.Add(new KeyValuePair<Vector2, Vector2>(bottomEnd, topEnd));
            }

            Random.state = oldState;
            return segments;
        }

        private struct RoadTileNode
        {
            public Vector2 centerXZ;
            public Vector2 halfExtentsXZ;
        }

        /// <summary>
        /// Finds the longest connected road path across street tiles in the current stage,
        /// ordered from the far end of that connected road network to the road endpoint closest to the player.
        /// </summary>
        public bool TryGetLongestConnectedRoadPath(Vector3 playerPos, out List<Vector3> waypoints)
        {
            waypoints = null;
            List<RoadTileNode> nodes = CollectRoadNodes();
            if (nodes.Count < 2)
            {
                return false;
            }

            List<List<KeyValuePair<int, float>>> adj = BuildRoadAdjacency(nodes);
            List<List<int>> components = FindConnectedRoadComponents(nodes.Count, adj);
            if (components.Count == 0)
            {
                return false;
            }

            Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);

            // Pick the connected road component with the longest continuous road span (diameter).
            List<int> bestComp = null;
            float bestSpan = -1f;
            float bestPlayerDist = float.MaxValue;

            foreach (List<int> comp in components)
            {
                if (comp.Count < 2) continue;
                float compSpan = MeasureComponentRoadSpan(comp, adj);
                float minDistToPlayer = float.MaxValue;
                foreach (int idx in comp)
                {
                    float d = Vector2.Distance(nodes[idx].centerXZ, playerXZ);
                    if (d < minDistToPlayer) minDistToPlayer = d;
                }

                bool betterSpan = compSpan > bestSpan + 1.0f;
                bool tiedCloser = Mathf.Abs(compSpan - bestSpan) <= 1.0f && minDistToPlayer < bestPlayerDist;
                if (betterSpan || tiedCloser)
                {
                    bestSpan = compSpan;
                    bestPlayerDist = minDistToPlayer;
                    bestComp = comp;
                }
            }

            if (bestComp == null || bestComp.Count < 2)
            {
                return false;
            }

            List<int> endpoints = FindRoadEndpointIndices(bestComp, nodes, adj);
            if (endpoints.Count < 2)
            {
                endpoints = bestComp;
            }

            // Road end closest to the player
            int endIdx = endpoints[0];
            float closestEndDist = float.MaxValue;
            foreach (int ep in endpoints)
            {
                float d = Vector2.Distance(nodes[ep].centerXZ, playerXZ);
                if (d < closestEndDist)
                {
                    closestEndDist = d;
                    endIdx = ep;
                }
            }

            // Find the connected road endpoint farthest along the road leading to endIdx
            RunRoadDijkstra(endIdx, nodes.Count, adj, out float[] distFromEnd, out int[] parentFromEnd);

            int startIdx = -1;
            float maxRoadDist = -1f;
            float maxDirectDist = -1f;
            foreach (int cand in endpoints)
            {
                if (cand == endIdx) continue;
                float rDist = distFromEnd[cand];
                if (float.IsInfinity(rDist)) continue;
                float dDist = Vector2.Distance(nodes[cand].centerXZ, playerXZ);
                if (rDist > maxRoadDist + 0.5f || (Mathf.Abs(rDist - maxRoadDist) <= 0.5f && dDist > maxDirectDist))
                {
                    maxRoadDist = rDist;
                    maxDirectDist = dDist;
                    startIdx = cand;
                }
            }

            if (startIdx < 0)
            {
                return false;
            }

            // Reconstruct path from startIdx -> endIdx (parentFromEnd points toward endIdx)
            List<Vector3> rawPoints = new List<Vector3>();
            int cur = startIdx;
            int safety = 0;
            while (cur >= 0 && safety++ <= nodes.Count)
            {
                Vector2 c = nodes[cur].centerXZ;
                rawPoints.Add(new Vector3(c.x, 1.0f, c.y));
                if (cur == endIdx) break;
                cur = parentFromEnd[cur];
            }

            if (rawPoints.Count < 2)
            {
                return false;
            }

            waypoints = SimplifyCollinearRoadWaypoints(rawPoints);
            return waypoints.Count >= 2;
        }

        private List<RoadTileNode> CollectRoadNodes()
        {
            List<RoadTileNode> nodes = new List<RoadTileNode>();
            if (_currentEnvContainer == null) return nodes;

            Transform container = _currentEnvContainer.transform;
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string childName = child.name;
                bool isRoad = childName.IndexOf("Street", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                              childName.IndexOf("RoadTile", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isRoad) continue;

                Vector3 pos = child.position;
                if (Mathf.Abs(pos.x) > 26f || Mathf.Abs(pos.z) > 14f) continue;

                Vector2 center = new Vector2(pos.x, pos.z);
                Vector2 half = new Vector2(2.5f, 2.5f);

                Renderer[] rends = child.GetComponentsInChildren<Renderer>(false);
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = new Bounds();
                    bool hasB = false;
                    foreach (Renderer r in rends)
                    {
                        if (r == null || !r.enabled) continue;
                        if (!hasB) { b = r.bounds; hasB = true; }
                        else { b.Encapsulate(r.bounds); }
                    }
                    if (hasB && b.size.x > 0.2f && b.size.z > 0.2f)
                    {
                        center = new Vector2(b.center.x, b.center.z);
                        half = new Vector2(b.extents.x, b.extents.z);
                    }
                }
                else
                {
                    half = new Vector2(
                        Mathf.Max(2.0f, Mathf.Abs(child.localScale.x) * 0.5f),
                        Mathf.Max(2.0f, Mathf.Abs(child.localScale.z) * 0.5f));
                }

                // Merge overlapping identical tiles to prevent duplicate nodes
                bool merged = false;
                for (int n = 0; n < nodes.Count; n++)
                {
                    if (Vector2.Distance(nodes[n].centerXZ, center) <= 0.9f)
                    {
                        RoadTileNode existing = nodes[n];
                        existing.halfExtentsXZ = Vector2.Max(existing.halfExtentsXZ, half);
                        nodes[n] = existing;
                        merged = true;
                        break;
                    }
                }
                if (!merged)
                {
                    nodes.Add(new RoadTileNode { centerXZ = center, halfExtentsXZ = half });
                }
            }

            return nodes;
        }

        private static List<List<KeyValuePair<int, float>>> BuildRoadAdjacency(List<RoadTileNode> nodes)
        {
            int count = nodes.Count;
            List<List<KeyValuePair<int, float>>> adj = new List<List<KeyValuePair<int, float>>>(count);
            for (int i = 0; i < count; i++) adj.Add(new List<KeyValuePair<int, float>>());

            const float touchTolerance = 0.85f;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    float dx = Mathf.Abs(nodes[i].centerXZ.x - nodes[j].centerXZ.x);
                    float dz = Mathf.Abs(nodes[i].centerXZ.y - nodes[j].centerXZ.y);
                    float maxSepX = nodes[i].halfExtentsXZ.x + nodes[j].halfExtentsXZ.x + touchTolerance;
                    float maxSepZ = nodes[i].halfExtentsXZ.y + nodes[j].halfExtentsXZ.y + touchTolerance;
                    float centerDist = Vector2.Distance(nodes[i].centerXZ, nodes[j].centerXZ);

                    bool sameRowBand = dz <= Mathf.Min(nodes[i].halfExtentsXZ.y, nodes[j].halfExtentsXZ.y) + 0.65f && dx <= maxSepX;
                    bool sameColBand = dx <= Mathf.Min(nodes[i].halfExtentsXZ.x, nodes[j].halfExtentsXZ.x) + 0.65f && dz <= maxSepZ;
                    if (sameRowBand || sameColBand)
                    {
                        adj[i].Add(new KeyValuePair<int, float>(j, centerDist));
                        adj[j].Add(new KeyValuePair<int, float>(i, centerDist));
                    }
                }
            }
            return adj;
        }

        private static List<List<int>> FindConnectedRoadComponents(int nodeCount, List<List<KeyValuePair<int, float>>> adj)
        {
            bool[] visited = new bool[nodeCount];
            List<List<int>> comps = new List<List<int>>();
            Queue<int> queue = new Queue<int>();

            for (int i = 0; i < nodeCount; i++)
            {
                if (visited[i]) continue;
                List<int> comp = new List<int>();
                visited[i] = true;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    comp.Add(cur);
                    foreach (var edge in adj[cur])
                    {
                        if (!visited[edge.Key])
                        {
                            visited[edge.Key] = true;
                            queue.Enqueue(edge.Key);
                        }
                    }
                }
                comps.Add(comp);
            }
            return comps;
        }

        private static float MeasureComponentRoadSpan(List<int> comp, List<List<KeyValuePair<int, float>>> adj)
        {
            int nodeCount = adj.Count;
            float maxSpan = 0f;
            foreach (int src in comp)
            {
                RunRoadDijkstra(src, nodeCount, adj, out float[] dist, out _);
                foreach (int dst in comp)
                {
                    if (!float.IsInfinity(dist[dst]) && dist[dst] > maxSpan)
                    {
                        maxSpan = dist[dst];
                    }
                }
            }
            return maxSpan;
        }

        private static List<int> FindRoadEndpointIndices(List<int> comp, List<RoadTileNode> nodes, List<List<KeyValuePair<int, float>>> adj)
        {
            List<int> leaves = new List<int>();
            foreach (int idx in comp)
            {
                if (adj[idx].Count <= 1)
                {
                    leaves.Add(idx);
                }
            }
            if (leaves.Count >= 2)
            {
                return leaves;
            }

            // Closed loop or wide multi-lane strip: pick outer perimeter extremes of the component
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (int idx in comp)
            {
                Vector2 c = nodes[idx].centerXZ;
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minZ) minZ = c.y;
                if (c.y > maxZ) maxZ = c.y;
            }

            List<int> perimeter = new List<int>();
            foreach (int idx in comp)
            {
                Vector2 c = nodes[idx].centerXZ;
                if (c.x <= minX + 1.5f || c.x >= maxX - 1.5f || c.y <= minZ + 1.5f || c.y >= maxZ - 1.5f)
                {
                    perimeter.Add(idx);
                }
            }
            return perimeter.Count >= 2 ? perimeter : new List<int>(comp);
        }

        private static void RunRoadDijkstra(int startIdx, int nodeCount, List<List<KeyValuePair<int, float>>> adj, out float[] dist, out int[] parent)
        {
            dist = new float[nodeCount];
            parent = new int[nodeCount];
            bool[] used = new bool[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                dist[i] = float.PositiveInfinity;
                parent[i] = -1;
            }
            dist[startIdx] = 0f;

            for (int step = 0; step < nodeCount; step++)
            {
                int best = -1;
                float bestD = float.PositiveInfinity;
                for (int i = 0; i < nodeCount; i++)
                {
                    if (!used[i] && dist[i] < bestD)
                    {
                        bestD = dist[i];
                        best = i;
                    }
                }
                if (best < 0 || float.IsInfinity(bestD)) break;
                used[best] = true;

                foreach (var edge in adj[best])
                {
                    float nd = bestD + edge.Value;
                    if (nd < dist[edge.Key])
                    {
                        dist[edge.Key] = nd;
                        parent[edge.Key] = best;
                    }
                }
            }
        }

        private static List<Vector3> SimplifyCollinearRoadWaypoints(List<Vector3> points)
        {
            if (points.Count <= 2) return points;
            List<Vector3> simplified = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 d1 = new Vector2(points[i].x - simplified[simplified.Count - 1].x, points[i].z - simplified[simplified.Count - 1].z).normalized;
                Vector2 d2 = new Vector2(points[i + 1].x - points[i].x, points[i + 1].z - points[i].z).normalized;
                if (Vector2.Dot(d1, d2) < 0.985f)
                {
                    simplified.Add(points[i]);
                }
            }
            simplified.Add(points[points.Count - 1]);
            return simplified;
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

        private static void EncapsulateLocalBoxInRootSpace(Transform root, Transform child, Vector3 localCenter, Vector3 localSize, ref Bounds rootBounds, ref bool hasBounds)
        {
            Vector3 half = localSize * 0.5f;
            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iy = -1; iy <= 1; iy += 2)
                {
                    for (int iz = -1; iz <= 1; iz += 2)
                    {
                        Vector3 cornerLocal = localCenter + new Vector3(half.x * ix, half.y * iy, half.z * iz);
                        Vector3 worldPt = child.TransformPoint(cornerLocal);
                        Vector3 rootLocalPt = Vector3.Scale(root.InverseTransformPoint(worldPt), root.localScale);
                        if (!hasBounds)
                        {
                            rootBounds = new Bounds(rootLocalPt, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            rootBounds.Encapsulate(rootLocalPt);
                        }
                    }
                }
            }
        }

        private bool TryGetPrefabLocalBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = new Bounds();
            if (prefab == null) return false;

            bool hasBounds = false;

            // 1. Check BoxColliders in prefab root space
            BoxCollider[] boxColliders = prefab.GetComponentsInChildren<BoxCollider>();
            if (boxColliders != null && boxColliders.Length > 0)
            {
                foreach (var box in boxColliders)
                {
                    if (box == null || box.isTrigger || !box.enabled) continue;
                    EncapsulateLocalBoxInRootSpace(prefab.transform, box.transform, box.center, box.size, ref bounds, ref hasBounds);
                }
            }

            // 2. Also encapsulate MeshFilter.sharedMesh.bounds (critical for prefabs without BoxColliders or with overhangs)
            MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
            if (meshFilters != null && meshFilters.Length > 0)
            {
                foreach (var mf in meshFilters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    Bounds mb = mf.sharedMesh.bounds;
                    if (mb.size.sqrMagnitude < 0.0001f) continue;
                    EncapsulateLocalBoxInRootSpace(prefab.transform, mf.transform, mb.center, mb.size, ref bounds, ref hasBounds);
                }
            }

            // 3. Fallback to active scene Colliders / Renderers (only valid if already instantiated in a scene)
            if (!hasBounds || bounds.size.sqrMagnitude < 0.001f)
            {
                Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    if (col == null || col.isTrigger) continue;
                    Bounds b = col.bounds;
                    if (b.size.sqrMagnitude < 0.001f) continue;
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

            return hasBounds && bounds.size.sqrMagnitude >= 0.001f;
        }

        private static Rect ProjectBoundsToWorldRect(Bounds localBounds, Vector3 worldPos, Quaternion rotation, float padding)
        {
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iy = -1; iy <= 1; iy += 2)
                {
                    for (int iz = -1; iz <= 1; iz += 2)
                    {
                        Vector3 localCorner = center + new Vector3(ix * extents.x, iy * extents.y, iz * extents.z);
                        Vector3 worldCorner = worldPos + rotation * localCorner;
                        if (worldCorner.x < minX) minX = worldCorner.x;
                        if (worldCorner.x > maxX) maxX = worldCorner.x;
                        if (worldCorner.z < minZ) minZ = worldCorner.z;
                        if (worldCorner.z > maxZ) maxZ = worldCorner.z;
                    }
                }
            }

            minX -= padding;
            maxX += padding;
            minZ -= padding;
            maxZ += padding;

            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>
        /// Computes the world-space XZ bounding rectangle of a prefab placed at worldPos with rotation,
        /// accounting for off-center local bounds and optional clearance padding.
        /// </summary>
        public Rect GetPrefabWorldRect(GameObject prefab, Vector3 worldPos, Quaternion rotation, float padding = 0f)
        {
            if (!TryGetPrefabLocalBounds(prefab, out Bounds bounds))
            {
                float half = 0.5f + padding;
                return new Rect(worldPos.x - half, worldPos.z - half, half * 2f, half * 2f);
            }

            return ProjectBoundsToWorldRect(bounds, worldPos, rotation, padding);
        }

        /// <summary>
        /// Calculates the world-space bounding size (X and Z) of a prefab after applying rotation.
        /// Works reliably on uninstantiated prefab assets by reading BoxCollider and MeshFilter.sharedMesh bounds.
        /// </summary>
        public Vector2 GetPrefabWorldSize(GameObject prefab, Quaternion rotation)
        {
            if (!TryGetPrefabLocalBounds(prefab, out Bounds bounds))
            {
                return new Vector2(1f, 1f);
            }

            Rect rect = ProjectBoundsToWorldRect(bounds, Vector3.zero, rotation, 0f);
            return new Vector2(Mathf.Max(0.2f, rect.width), Mathf.Max(0.2f, rect.height));
        }

        /// <summary>
        /// Compares the prefab's rotated physical dimensions with grid cell dimensions to determine how many cells it occupies.
        /// </summary>
        public Vector2Int GetPrefabCellFootprint(GameObject prefab, Quaternion rotation, float cellWidth, float cellHeight)
        {
            Vector2 size = GetPrefabWorldSize(prefab, rotation);
            // Use 0.92f factor so large buildings reserve the cells their physical mesh overhangs into
            int spanCols = Mathf.Max(1, Mathf.CeilToInt((size.x * 0.92f) / cellWidth));
            int spanRows = Mathf.Max(1, Mathf.CeilToInt((size.y * 0.92f) / cellHeight));
            return new Vector2Int(spanCols, spanRows);
        }

        private bool IsLargeBuilding(GameObject prefab)
        {
            if (prefab == null) return false;
            string name = prefab.name.ToLower();
            if (name.Contains("vehicle") || name.Contains("motorcycle") || name.Contains("scooter") ||
                name.Contains("sedan") || name.Contains("truck") || name.Contains("van") || name.Contains("bus") ||
                name.Contains("tree") || name.Contains("bush") || name.Contains("prop_") || name == "street")
            {
                return false;
            }
            if (name.Contains("house") || name.Contains("shophouse") || name.Contains("building") ||
                name.Contains("tower") || name.Contains("home") || name.Contains("structure") ||
                name.Contains("cottage") || name.Contains("townhouse") || name.Contains("chalet") ||
                name.Contains("canal") || name.Contains("diner") || name.Contains("brownstone") ||
                name.Contains("warehouse") || name.Contains("hangar") || name.Contains("gasstation") ||
                name.Contains("fountain") || name.Contains("gazebo") || name.Contains("subway") ||
                name.Contains("cornershop"))
            {
                return true;
            }
            Vector2 size = GetPrefabWorldSize(prefab, prefab.transform.rotation);
            if (size.x > 3.8f || size.y > 3.8f)
            {
                return true;
            }
            return false;
        }

        private GameObject GetRandomBuildingPrefab(List<GameObject> primaryPrefabs, List<GameObject> secondaryPrefabs = null)
        {
            List<GameObject> buildings = new List<GameObject>();
            if (primaryPrefabs != null)
            {
                foreach (var p in primaryPrefabs)
                {
                    if (p != null && IsLargeBuilding(p)) buildings.Add(p);
                }
            }
            if (secondaryPrefabs != null)
            {
                foreach (var p in secondaryPrefabs)
                {
                    if (p != null && IsLargeBuilding(p)) buildings.Add(p);
                }
            }

            if (buildings.Count > 0)
            {
                return buildings[Random.Range(0, buildings.Count)];
            }
            return null;
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

        private static Quaternion ResolveTemplateRotation(GameObject prefab, Vector3 eulerRotation)
        {
            // Guard against legacy Z-up Stage JSONs that stored X=270 (-90) or X=90 for now-upright Y-up prefabs
            if (prefab != null &&
                Mathf.Abs(Mathf.DeltaAngle(prefab.transform.eulerAngles.x, 0f)) < 1f &&
                (Mathf.Abs(Mathf.DeltaAngle(eulerRotation.x, 270f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(eulerRotation.x, 90f)) < 1f))
            {
                eulerRotation.x = 0f;
            }

            if (prefab != null && Mathf.Abs(eulerRotation.x) < 0.01f && Mathf.Abs(eulerRotation.z) < 0.01f)
            {
                return Quaternion.Euler(0f, eulerRotation.y, 0f) * prefab.transform.rotation;
            }
            return Quaternion.Euler(eulerRotation);
        }

        private void GenerateEnvironmentFromTemplate(StageTemplateData template, StageData stage)
        {
            if (template == null || _currentEnvContainer == null) return;

            Debug.Log($"[EnvironmentManager] Building handcrafted environment from template '{template.templateName}' (ID: {template.templateId}) for Stage ({stage.x}, {stage.y})");

            Vector3 playerPos = TransitionPortal.HasValidWarpHistory ? TransitionPortal.LastPlayerWarpInPos : Vector3.zero;
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null) playerPos = player.transform.position;

            // 1. Spawn Obstacles
            if (template.obstacles != null)
            {
                foreach (StagePlacedObject obj in template.obstacles)
                {
                    GameObject prefab = ResolveTemplatePrefab(obj.prefabName, obj.resourcePath);
                    if (prefab != null)
                    {
                        Quaternion rot = ResolveTemplateRotation(prefab, obj.rotation);
                        Vector2 worldSize = GetPrefabWorldSize(prefab, rot);
                        worldSize = new Vector2(worldSize.x * Mathf.Abs(obj.scale.x), worldSize.y * Mathf.Abs(obj.scale.z));
                        if (DoesFootprintOverlapPortalOrPlayer(new Vector2(obj.position.x, obj.position.z), worldSize, playerPos))
                        {
                            continue;
                        }
                        GameObject spawned = Instantiate(prefab, obj.position, rot, _currentEnvContainer.transform);
                        spawned.name = prefab.name;
                        if (obj.scale != Vector3.one || prefab.transform.localScale == Vector3.one)
                        {
                            spawned.transform.localScale = obj.scale;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[EnvironmentManager] Template obstacle prefab not found: {obj.prefabName} (Path: {obj.resourcePath})");
                    }
                }
            }

            // 2. Spawn Decorations (upgrade any handcrafted template "Street" tiles to match styled recessed asphalt roads!)
            List<Rect> templateStreetRects = new List<Rect>();
            if (template.decorations != null)
            {
                foreach (StagePlacedObject obj in template.decorations)
                {
                    GameObject prefab = ResolveTemplatePrefab(obj.prefabName, obj.resourcePath);
                    if (prefab != null)
                    {
                        Quaternion rot = ResolveTemplateRotation(prefab, obj.rotation);
                        bool isStreetTile = prefab.name.Equals("Street", System.StringComparison.OrdinalIgnoreCase);
                        if (isStreetTile && useStyledAsphaltRoad)
                        {
                            // Street.prefab is a unit Cube scaled by obj.scale (or prefab scale)
                            bool rotated90 = Mathf.Abs(Mathf.DeltaAngle(obj.rotation.y, 90f)) < 45f ||
                                             Mathf.Abs(Mathf.DeltaAngle(obj.rotation.y, 270f)) < 45f;
                            float sx = Mathf.Max(0.5f, Mathf.Abs(obj.scale.x));
                            float sz = Mathf.Max(0.5f, Mathf.Abs(obj.scale.z));
                            float worldW = rotated90 ? sz : sx;
                            float worldH = rotated90 ? sx : sz;
                            templateStreetRects.Add(new Rect(obj.position.x - worldW * 0.5f, obj.position.z - worldH * 0.5f, worldW, worldH));
                            continue;
                        }

                        if (!isStreetTile)
                        {
                            Vector2 worldSize = GetPrefabWorldSize(prefab, rot);
                            worldSize = new Vector2(worldSize.x * Mathf.Abs(obj.scale.x), worldSize.y * Mathf.Abs(obj.scale.z));
                            if (DoesFootprintOverlapPortalOrPlayer(new Vector2(obj.position.x, obj.position.z), worldSize, playerPos))
                            {
                                continue;
                            }
                        }
                        GameObject spawned = Instantiate(prefab, obj.position, rot, _currentEnvContainer.transform);
                        spawned.name = prefab.name;
                        spawned.transform.localScale = obj.scale;
                    }
                }
            }

            if (templateStreetRects.Count > 0)
            {
                EnsureStyledRoadMaterials();
                const float tol = 0.65f;
                for (int i = 0; i < templateStreetRects.Count; i++)
                {
                    Rect r = templateStreetRects[i];
                    bool hasLeft = false, hasRight = false, hasUp = false, hasDown = false;
                    for (int j = 0; j < templateStreetRects.Count; j++)
                    {
                        if (i == j) continue;
                        Rect o = templateStreetRects[j];
                        bool overlapZ = (r.yMin < o.yMax - 0.25f) && (r.yMax > o.yMin + 0.25f);
                        bool overlapX = (r.xMin < o.xMax - 0.25f) && (r.xMax > o.xMin + 0.25f);
                        if (overlapZ)
                        {
                            if (o.xMax >= r.xMin - tol && o.xMin < r.xMin) hasLeft = true;
                            if (o.xMin <= r.xMax + tol && o.xMax > r.xMax) hasRight = true;
                        }
                        if (overlapX)
                        {
                            if (o.yMax >= r.yMin - tol && o.yMin < r.yMin) hasDown = true;
                            if (o.yMin <= r.yMax + tol && o.yMax > r.yMax) hasUp = true;
                        }
                    }

                    SpawnStyledAsphaltTile(
                        r.center.x, r.center.y,
                        r.width, r.height,
                        1.01f,
                        hasLeft, hasRight, hasUp, hasDown,
                        row: i / 10, col: i % 10,
                        allowCrosswalk: false);
                }
            }
        }

        /// <summary>
        /// Post-generation safety verification: inspects all instantiated obstacles/buildings in _currentEnvContainer
        /// and removes any non-street object whose actual world collider/renderer bounds overlap an active TransitionPortal or the player.
        /// </summary>
        private void EnsurePortalsClear(Vector3 playerPos)
        {
            if (_currentEnvContainer == null) return;

            Physics.SyncTransforms();

            List<GameObject> toRemove = new List<GameObject>();
            int childCount = _currentEnvContainer.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = _currentEnvContainer.transform.GetChild(i);
                if (child == null) continue;

                string childName = child.name;
                if (childName.IndexOf("Street", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("RoadTile", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Bounds worldBounds = new Bounds();
                bool hasWorldBounds = false;

                Collider[] cols = child.GetComponentsInChildren<Collider>();
                if (cols != null)
                {
                    foreach (var c in cols)
                    {
                        if (c == null || c.isTrigger || !c.enabled) continue;
                        if (c.bounds.size.sqrMagnitude < 0.0001f) continue;
                        if (!hasWorldBounds)
                        {
                            worldBounds = c.bounds;
                            hasWorldBounds = true;
                        }
                        else
                        {
                            worldBounds.Encapsulate(c.bounds);
                        }
                    }
                }

                if (!hasWorldBounds)
                {
                    Renderer[] rends = child.GetComponentsInChildren<Renderer>();
                    if (rends != null)
                    {
                        foreach (var r in rends)
                        {
                            if (r == null || r is ParticleSystemRenderer || !r.enabled) continue;
                            if (r.bounds.size.sqrMagnitude < 0.0001f) continue;
                            if (!hasWorldBounds)
                            {
                                worldBounds = r.bounds;
                                hasWorldBounds = true;
                            }
                            else
                            {
                                worldBounds.Encapsulate(r.bounds);
                            }
                        }
                    }
                }

                if (!hasWorldBounds) continue;

                Vector2 centerXZ = new Vector2(worldBounds.center.x, worldBounds.center.z);
                Vector2 sizeXZ = new Vector2(worldBounds.size.x, worldBounds.size.z);

                if (DoesFootprintOverlapPortalOrPlayer(centerXZ, sizeXZ, playerPos, 2.2f, 1.5f))
                {
                    toRemove.Add(child.gameObject);
                }
            }

            foreach (GameObject go in toRemove)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        /// <summary>
        /// <summary>
        /// Configures ONLY buildings that explicitly have a BuildingDoorWarpTrigger component placed on them.
        /// Never guesses buildings by size and never auto-spawns fake door frames on props/fountains/stalls.
        /// </summary>
        private void AttachEnterableBuildingDoors(StageData stage)
        {
            if (_currentEnvContainer == null || stage == null || !Application.isPlaying) return;

            Physics.SyncTransforms();

            // Lock any DoorWarpTriggers placed on non-playable BackgroundScenery outside the main stage
            Transform bgScenery = _currentEnvContainer.transform.Find("BackgroundScenery");
            if (bgScenery != null)
            {
                BuildingDoorWarpTrigger[] bgDoors = bgScenery.GetComponentsInChildren<BuildingDoorWarpTrigger>(true);
                for (int k = 0; k < bgDoors.Length; k++)
                {
                    if (bgDoors[k] != null) bgDoors[k].SetLockedInStage();
                }
            }

            // Collect ONLY direct spawned buildings that explicitly carry a BuildingDoorWarpTrigger
            List<(Transform target, BuildingDoorWarpTrigger door, Bounds bounds)> candidates =
                new List<(Transform, BuildingDoorWarpTrigger, Bounds)>();

            int childCount = _currentEnvContainer.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = _currentEnvContainer.transform.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.name.Equals("BackgroundScenery", System.StringComparison.OrdinalIgnoreCase)) continue;

                BuildingDoorWarpTrigger[] doors = child.GetComponentsInChildren<BuildingDoorWarpTrigger>(true);
                if (doors == null || doors.Length == 0) continue;

                // Lock all first; eligible ones are unlocked deterministically below
                for (int k = 0; k < doors.Length; k++)
                {
                    if (doors[k] != null) doors[k].SetLockedInStage();
                }

                Bounds b = TryComputeObjectWorldBounds(child, out Bounds computed)
                    ? computed
                    : new Bounds(child.position, new Vector3(4f, 5f, 4f));

                for (int k = 0; k < doors.Length; k++)
                {
                    if (doors[k] != null)
                    {
                        candidates.Add((child, doors[k], b));
                    }
                }
            }

            if (candidates.Count == 0) return;

            // Deterministic shuffle using stageSeed so Host and Co-op Clients always assign identical IDs & seeds
            System.Random rng = new System.Random(stage.stageSeed ^ 0x51A7E29);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                var tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            int desiredCount = unlockAllEnterableBuildings
                ? candidates.Count
                : (candidates.Count <= 2 ? 1 : (rng.NextDouble() < 0.70 ? 2 : 1));
            int attachedCount = 0;

            float maxPlayableX = gridWidth * 0.50f;
            float maxPlayableZ = gridHeight * 0.49f;

            for (int i = 0; i < candidates.Count && attachedCount < desiredCount; i++)
            {
                Transform bldg = candidates[i].target;
                BuildingDoorWarpTrigger prefabDoor = candidates[i].door;
                Bounds b = candidates[i].bounds;
                if (prefabDoor == null) continue;

                Vector3 doorWorldPos = prefabDoor.transform.position;
                bool inBounds = Mathf.Abs(doorWorldPos.x) <= maxPlayableX && Mathf.Abs(doorWorldPos.z) <= maxPlayableZ;
                if (!inBounds && !prefabDoor.AlwaysEnterable) continue;

                Vector3 checkPoint = prefabDoor.ExteriorReturnPosition + Vector3.up * 0.35f;
                Collider[] hits = Physics.OverlapSphere(checkPoint, 0.42f, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int h = 0; h < hits.Length; h++)
                {
                    Collider col = hits[h];
                    if (col == null) continue;
                    if (col.transform.IsChildOf(bldg) || col.transform == bldg) continue;
                    if (IsWalkableGroundCollider(col)) continue;
                    if (col.GetComponentInParent<PlayerController>() != null) continue;
                    blocked = true;
                    break;
                }

                if (blocked && !prefabDoor.AlwaysEnterable) continue;

                attachedCount++;
                int bId = attachedCount;
                int seed = BuildingInteriorManager.ComputeInteriorSeed(stage.stageSeed, stage.x, stage.y, bId, bldg.name);
                prefabDoor.UnlockForStage(bId, stage.x, stage.y, seed, bldg.name, b.size.y);
            }
        }

        private static bool IsWalkableGroundCollider(Collider col)
        {
            if (col == null) return true;
            if (col.bounds.size.y <= 0.35f && col.bounds.max.y <= 0.30f) return true; // Thin flat floor/sidewalk/tile slab
            string colName = col.gameObject.name.ToLowerInvariant();
            return colName.Contains("ground") ||
                   colName.Contains("floor") ||
                   colName.Contains("street") ||
                   colName.Contains("road") ||
                   colName.Contains("sidewalk") ||
                   colName.Contains("pavement") ||
                   colName.Contains("asphalt") ||
                   colName.Contains("curb") ||
                   colName.Contains("terrain") ||
                   colName.Contains("plane") ||
                   colName.Contains("tile") ||
                   colName.Contains("grass") ||
                   colName.Contains("dirt");
        }

        private static bool TryComputeObjectWorldBounds(Transform root, out Bounds worldBounds)
        {
            worldBounds = new Bounds();
            bool hasBounds = false;

            Collider[] cols = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null || c.isTrigger || !c.enabled) continue;
                if (c.bounds.size.sqrMagnitude < 0.0001f) continue;
                if (!hasBounds) { worldBounds = c.bounds; hasBounds = true; }
                else worldBounds.Encapsulate(c.bounds);
            }

            if (!hasBounds)
            {
                Renderer[] rends = root.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < rends.Length; i++)
                {
                    Renderer r = rends[i];
                    if (r == null || r is ParticleSystemRenderer || !r.enabled) continue;
                    if (r.bounds.size.sqrMagnitude < 0.0001f) continue;
                    if (!hasBounds) { worldBounds = r.bounds; hasBounds = true; }
                    else worldBounds.Encapsulate(r.bounds);
                }
            }

            return hasBounds;
        }

        private static string GetUniqueBuildingDisplayName(InteriorTheme theme, int seed)
        {
            int idx = Mathf.Abs(seed) % 4;
            return theme switch
            {
                InteriorTheme.CornerShop => idx switch
                {
                    0 => "Corner Shop",
                    1 => "Mini Mart",
                    2 => "Supply Store",
                    _ => "General Store"
                },
                InteriorTheme.Clinic => idx switch
                {
                    0 => "Field Clinic",
                    1 => "Old Pharmacy",
                    2 => "Medical Room",
                    _ => "Outpost Clinic"
                },
                InteriorTheme.Warehouse => idx switch
                {
                    0 => "Warehouse",
                    1 => "Storage Depot",
                    2 => "Cargo Shed",
                    _ => "Workshop"
                },
                _ => idx switch
                {
                    0 => "Abandoned House",
                    1 => "Suburban Home",
                    2 => "Old Cottage",
                    _ => "Townhouse"
                }
            };
        }

        /// <summary>
        /// Generates decorative buildings, trees, and skyline props on the surrounding corner and edge grounds
        /// (TLGround, TRGround, BLGround, BRGround, TGround, BGround, LGround, RGround) to provide an immersive
        /// background view so the player never sees an empty void or bare green planes at the borders.
        /// </summary>
        private struct SurroundingGroundZone
        {
            public Transform transform;
            public Vector3 center;
            public bool isCorner;
            public int dx;
            public int dy;
            public string name;
        }

        /// <summary>
        /// Generates decorative buildings, trees, and skyline props on the surrounding corner and edge grounds
        /// (TLGround, TRGround, BLGround, BRGround, TGround, BGround, LGround, RGround) to provide an immersive
        /// background view matching the actual biomes of the neighboring stages on the World Map.
        /// </summary>
        private void GenerateSurroundingBackgroundScenery(StageData stage, BiomeEnvConfig config)
        {
            if (_currentEnvContainer == null) return;

            GameObject bgContainer = new GameObject("BackgroundScenery");
            bgContainer.transform.parent = _currentEnvContainer.transform;
            bgContainer.transform.position = Vector3.zero;

            Random.State oldState = Random.state;
            Random.InitState(stage.stageSeed + 88888);

            // 1. Discover surrounding ground areas dynamically from the scene hierarchy
            Transform groundParent = GameObject.Find("Grouind")?.transform ?? GameObject.Find("Ground")?.transform;

            List<SurroundingGroundZone> zones = new List<SurroundingGroundZone>();

            if (groundParent != null)
            {
                for (int i = 0; i < groundParent.childCount; i++)
                {
                    Transform child = groundParent.GetChild(i);
                    string nameUpper = child.name.ToUpper();
                    if (nameUpper.Contains("CGROUND") || nameUpper == "CENTER" || nameUpper == "CENTERGROUND") continue;

                    Vector3 pos = child.position;
                    int dx = 0;
                    int dy = 0;
                    bool isCorner = false;

                    if (nameUpper.Contains("TL")) { dx = -1; dy = 1; isCorner = true; }
                    else if (nameUpper.Contains("TR")) { dx = 1; dy = 1; isCorner = true; }
                    else if (nameUpper.Contains("BL")) { dx = -1; dy = -1; isCorner = true; }
                    else if (nameUpper.Contains("BR")) { dx = 1; dy = -1; isCorner = true; }
                    else if (nameUpper.Contains("T") || nameUpper.Contains("NORTH")) { dx = 0; dy = 1; isCorner = false; }
                    else if (nameUpper.Contains("B") || nameUpper.Contains("SOUTH")) { dx = 0; dy = -1; isCorner = false; }
                    else if (nameUpper.Contains("R") || nameUpper.Contains("EAST")) { dx = 1; dy = 0; isCorner = false; }
                    else if (nameUpper.Contains("L") || nameUpper.Contains("WEST")) { dx = -1; dy = 0; isCorner = false; }

                    zones.Add(new SurroundingGroundZone
                    {
                        transform = child,
                        center = pos,
                        isCorner = isCorner,
                        dx = dx,
                        dy = dy,
                        name = child.name
                    });
                }
            }

            // Fallback default zones if Grouind hierarchy is not found in the active scene
            if (zones.Count == 0)
            {
                zones.Add(new SurroundingGroundZone { center = new Vector3(-gridWidth, 0f, gridHeight * 0.85f), isCorner = true, dx = -1, dy = 1, name = "TLGround" });
                zones.Add(new SurroundingGroundZone { center = new Vector3(gridWidth, 0f, gridHeight * 0.85f), isCorner = true, dx = 1, dy = 1, name = "TRGround" });
                zones.Add(new SurroundingGroundZone { center = new Vector3(-gridWidth, 0f, -gridHeight * 0.85f), isCorner = true, dx = -1, dy = -1, name = "BLGround" });
                zones.Add(new SurroundingGroundZone { center = new Vector3(gridWidth, 0f, -gridHeight * 0.85f), isCorner = true, dx = 1, dy = -1, name = "BRGround" });

                zones.Add(new SurroundingGroundZone { center = new Vector3(0f, 0f, gridHeight * 0.85f), isCorner = false, dx = 0, dy = 1, name = "TGround" });
                zones.Add(new SurroundingGroundZone { center = new Vector3(0f, 0f, -gridHeight * 0.85f), isCorner = false, dx = 0, dy = -1, name = "BGround" });
                zones.Add(new SurroundingGroundZone { center = new Vector3(-gridWidth, 0f, 0f), isCorner = false, dx = -1, dy = 0, name = "LGround" });
                zones.Add(new SurroundingGroundZone { center = new Vector3(gridWidth, 0f, 0f), isCorner = false, dx = 1, dy = 0, name = "RGround" });
            }

            // 2. Populate each surrounding zone according to its specific neighboring stage biome
            foreach (var zone in zones)
            {
                int neighborX = stage.x + zone.dx;
                int neighborY = stage.y + zone.dy;
                StageData neighborStage = WorldMapManager.Instance?.MapGenerator?.GetStage(neighborX, neighborY);
                BiomeType zoneBiome = (neighborStage != null) ? neighborStage.biome : stage.biome;
                BiomeEnvConfig zoneConfig = GetConfigForBiome(zoneBiome);

                // Sync ground mesh renderer material to the zone's specific biome
                if (zone.transform != null)
                {
                    MeshRenderer mr = zone.transform.GetComponent<MeshRenderer>();
                    if (mr != null && GroundBiomeMaterialController.Instance != null)
                    {
                        GroundBiomeMaterialController.Instance.ApplyBiomeMaterial(zoneBiome, mr);
                    }
                }

                // Categorize large buildings vs props for this zone's biome
                List<GameObject> largeBuildings = new List<GameObject>();
                List<GameObject> allDecorations = new List<GameObject>();

                if (zoneConfig.obstaclePrefabs != null)
                {
                    foreach (var p in zoneConfig.obstaclePrefabs)
                    {
                        if (p == null) continue;
                        if (IsLargeBuilding(p)) largeBuildings.Add(p);
                        else allDecorations.Add(p);
                    }
                }
                if (zoneConfig.decorationPrefabs != null)
                {
                    foreach (var p in zoneConfig.decorationPrefabs)
                    {
                        if (p == null || p.name.Equals("Street", System.StringComparison.OrdinalIgnoreCase)) continue;
                        if (IsLargeBuilding(p)) largeBuildings.Add(p);
                        else allDecorations.Add(p);
                    }
                }

                if (zone.isCorner)
                {
                    // Skyline / Backdrop in corners
                    int bCount = Random.Range(buildingsPerCorner - 1, buildingsPerCorner + 2);
                    for (int b = 0; b < bCount; b++)
                    {
                        GameObject bPrefab = GetRandomPrefab(largeBuildings.Count > 0 ? largeBuildings : zoneConfig.obstaclePrefabs);
                        float offsetX = Random.Range(-18f, 18f);
                        float offsetZ = Random.Range(-8f, 8f);
                        Vector3 spawnPos = zone.center + new Vector3(offsetX, 0f, offsetZ);

                        if (bPrefab != null)
                        {
                            Quaternion rot = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) * bPrefab.transform.rotation;
                            float spawnY = bPrefab.transform.position.y + prefabSpawnOffsetHeight;
                            Vector3 worldPos = new Vector3(spawnPos.x, spawnY, spawnPos.z);
                            GameObject obj = Instantiate(bPrefab, worldPos, rot, bgContainer.transform);
                            obj.name = $"BG_{bPrefab.name}";
                            DisableCollidersOnBackgroundObject(obj);
                        }
                        else if (useProceduralFallbacks)
                        {
                            CreateProceduralObstacle(new Vector3(spawnPos.x, spawnPos.z, 0f), zoneBiome, true);
                        }
                    }

                    int pCount = Random.Range(propsPerCorner - 1, propsPerCorner + 2);
                    for (int p = 0; p < pCount; p++)
                    {
                        GameObject pPrefab = GetRandomPrefab(allDecorations.Count > 0 ? allDecorations : zoneConfig.decorationPrefabs);
                        float offsetX = Random.Range(-20f, 20f);
                        float offsetZ = Random.Range(-10f, 10f);
                        Vector3 spawnPos = zone.center + new Vector3(offsetX, 0f, offsetZ);

                        if (pPrefab != null)
                        {
                            Quaternion rot = Quaternion.Euler(0f, Random.Range(0, 360f), 0f) * pPrefab.transform.rotation;
                            float spawnY = pPrefab.transform.position.y + prefabSpawnOffsetHeight;
                            Vector3 worldPos = new Vector3(spawnPos.x, spawnY, spawnPos.z);
                            GameObject obj = Instantiate(pPrefab, worldPos, rot, bgContainer.transform);
                            obj.name = $"BG_{pPrefab.name}";
                            DisableCollidersOnBackgroundObject(obj);
                        }
                        else if (useProceduralFallbacks)
                        {
                            CreateProceduralDecoration(new Vector3(spawnPos.x, spawnPos.z, 0f), zoneBiome);
                        }
                    }
                }
                else
                {
                    // Edge flanking view (leave central portal line of sight clear)
                    bool isHorizontalEdge = Mathf.Abs(zone.center.z) > Mathf.Abs(zone.center.x); // Top or Bottom

                    int edgeBuildingCount = Random.Range(2, 4);
                    for (int b = 0; b < edgeBuildingCount; b++)
                    {
                        GameObject bPrefab = GetRandomPrefab(largeBuildings.Count > 0 ? largeBuildings : zoneConfig.obstaclePrefabs);
                        float offsetX = 0f;
                        float offsetZ = 0f;

                        if (isHorizontalEdge)
                        {
                            offsetX = Random.value < 0.5f ? Random.Range(-20f, -8f) : Random.Range(8f, 20f);
                            offsetZ = Random.Range(-8f, 8f);
                        }
                        else
                        {
                            offsetX = Random.Range(-8f, 8f);
                            offsetZ = Random.value < 0.5f ? Random.Range(-10f, -6f) : Random.Range(6f, 10f);
                        }

                        Vector3 spawnPos = zone.center + new Vector3(offsetX, 0f, offsetZ);
                        if (bPrefab != null)
                        {
                            Quaternion rot = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) * bPrefab.transform.rotation;
                            float spawnY = bPrefab.transform.position.y + prefabSpawnOffsetHeight;
                            Vector3 worldPos = new Vector3(spawnPos.x, spawnY, spawnPos.z);
                            GameObject obj = Instantiate(bPrefab, worldPos, rot, bgContainer.transform);
                            obj.name = $"BG_{bPrefab.name}";
                            DisableCollidersOnBackgroundObject(obj);
                        }
                    }
                }
            }

            Random.state = oldState;
        }

        private static void DisableCollidersOnBackgroundObject(GameObject obj)
        {
            if (obj == null) return;
            Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        private GameObject ResolveTemplatePrefab(string prefabName, string resourcePath)
        {
            // 1. Try direct resourcePath if provided via Addressables (with Resources fallback)
            if (!string.IsNullOrEmpty(resourcePath))
            {
                GameObject res = AddressableManager.LoadAsset<GameObject>(resourcePath);
                if (res != null) return res;
            }

            // 2. Try common Environment folders in Addressables (with Resources fallback)
            if (!string.IsNullOrEmpty(prefabName))
            {
                GameObject res = AddressableManager.LoadAsset<GameObject>($"Prefabs/Environment/{prefabName}");
                if (res != null) return res;

                res = AddressableManager.LoadAsset<GameObject>($"Prefabs/Environment/Building/{prefabName}");
                if (res != null) return res;

                res = AddressableManager.LoadAsset<GameObject>($"Prefabs/{prefabName}");
                if (res != null) return res;

                // Search in configured biomeConfigs
                if (biomeConfigs != null)
                {
                    foreach (var cfg in biomeConfigs)
                    {
                        if (cfg.obstaclePrefabs != null)
                        {
                            var found = cfg.obstaclePrefabs.Find(p => p != null && p.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase));
                            if (found != null) return found;
                        }
                        if (cfg.decorationPrefabs != null)
                        {
                            var found = cfg.decorationPrefabs.Find(p => p != null && p.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase));
                            if (found != null) return found;
                        }
                    }
                }
            }

            return null;
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
