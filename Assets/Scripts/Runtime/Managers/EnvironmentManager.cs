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
            [Tooltip("Prefabs that have colliders and block movement (e.g. Rocks, Trees, Fences).")]
            public List<GameObject> obstaclePrefabs;
            [Tooltip("Prefabs that are purely visual decoration without colliders (e.g. grass patches, dirt, debris, pebbles).")]
            public List<GameObject> decorationPrefabs;

            [Range(0, 15)] public int minObstacles;
            [Range(0, 15)] public int maxObstacles;

            [Range(0, 25)] public int minDecorations;
            [Range(0, 25)] public int maxDecorations;
        }

        [Header("Biome Environment Layouts")]
        [SerializeField] private List<BiomeEnvConfig> biomeConfigs;

        [Header("Default Fallback Settings (if no prefabs assigned)")]
        [SerializeField] private bool useProceduralFallbacks = true;

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

        /// <summary>
        /// Clears and generates a deterministic environment layout for the current stage.
        /// </summary>
        public void GenerateStageEnvironment(StageData stage)
        {
            if (stage == null) return;

            // 1. Clean up old stage environment props
            if (_currentEnvContainer != null)
            {
                Destroy(_currentEnvContainer);
            }

            // 2. Create parent container
            _currentEnvContainer = new GameObject("GeneratedEnvironment");
            _currentEnvContainer.transform.position = Vector3.zero;

            // 3. Find matching config for this biome
            BiomeEnvConfig config = GetConfigForBiome(stage.biome);

            // 4. Init deterministic state using stage seed (offset slightly to not clash with enemy spawns)
            Random.InitState(stage.stageSeed + 12345);

            // Fetch player position to avoid spawning obstacles directly on top of them
            Vector3 playerPos = Vector3.zero;
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerPos = player.transform.position;
            }

            // Define grid parameters (6 rows x 9 columns)
            int rows = 6;
            int cols = 9;
            float totalWidth = 14f;  // X from -7 to 7
            float totalHeight = 8f;  // Y from -4 to 4
            float cellWidth = totalWidth / cols;
            float cellHeight = totalHeight / rows;

            List<GridCell> allCells = new List<GridCell>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = -7f + c * cellWidth + cellWidth * 0.5f;
                    float y = -4f + r * cellHeight + cellHeight * 0.5f;
                    allCells.Add(new GridCell { row = r, col = c, center = new Vector3(x, y, 0f) });
                }
            }

            // Filter cells to only include safe cells
            List<GridCell> safeCells = new List<GridCell>();
            float safeRadius = 1.8f;
            foreach (GridCell cell in allCells)
            {
                // Check player safety zone
                if (Vector3.Distance(cell.center, playerPos) < safeRadius)
                {
                    continue;
                }

                // Check transition gate safety zone
                if (IsNearTransitionGates(cell.center))
                {
                    continue;
                }

                safeCells.Add(cell);
            }

            // Shuffle safe cells deterministically using stage seed
            for (int i = 0; i < safeCells.Count; i++)
            {
                GridCell temp = safeCells[i];
                int randomIndex = Random.Range(i, safeCells.Count);
                safeCells[i] = safeCells[randomIndex];
                safeCells[randomIndex] = temp;
            }

            int obstacleCount = Random.Range(config.minObstacles, config.maxObstacles + 1);
            int decorationCount = Random.Range(config.minDecorations, config.maxDecorations + 1);

            int totalToSpawn = Mathf.Min(obstacleCount + decorationCount, safeCells.Count);
            int spawnObstacles = Mathf.Min(obstacleCount, totalToSpawn);
            int spawnDecorations = totalToSpawn - spawnObstacles;

            int cellIndex = 0;

            // 5. Generate Obstacles (solid blockers)
            for (int i = 0; i < spawnObstacles; i++)
            {
                GridCell cell = safeCells[cellIndex++];
                
                // Add organic jitter within cell boundaries (up to 20% cell size)
                float jitterX = Random.Range(-cellWidth * 0.2f, cellWidth * 0.2f);
                float jitterY = Random.Range(-cellHeight * 0.2f, cellHeight * 0.2f);
                Vector3 spawnPos = cell.center + new Vector3(jitterX, jitterY, 0f);

                GameObject prefab = GetRandomPrefab(config.obstaclePrefabs);
                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity, _currentEnvContainer.transform);
                    obj.name = prefab.name;
                }
                else if (useProceduralFallbacks)
                {
                    CreateProceduralObstacle(spawnPos, stage.biome);
                }
            }

            // 6. Generate Decorations (visuals only)
            for (int i = 0; i < spawnDecorations; i++)
            {
                GridCell cell = safeCells[cellIndex++];

                // Add organic jitter within cell boundaries
                float jitterX = Random.Range(-cellWidth * 0.2f, cellWidth * 0.2f);
                float jitterY = Random.Range(-cellHeight * 0.2f, cellHeight * 0.2f);
                Vector3 spawnPos = cell.center + new Vector3(jitterX, jitterY, 0f);

                GameObject prefab = GetRandomPrefab(config.decorationPrefabs);
                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity, _currentEnvContainer.transform);
                    obj.name = prefab.name;
                }
                else if (useProceduralFallbacks)
                {
                    CreateProceduralDecoration(spawnPos, stage.biome);
                }
            }

            Debug.Log($"[EnvironmentManager] Grid generated {spawnObstacles} obstacles and {spawnDecorations} decorations (out of safe cells: {safeCells.Count}) for Biome {stage.biome} using seed {stage.stageSeed}");
        }

        private BiomeEnvConfig GetConfigForBiome(BiomeType biome)
        {
            if (biomeConfigs != null)
            {
                foreach (var config in biomeConfigs)
                {
                    if (config.biome == biome) return config;
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

        private Vector3 GetRandomSafePosition(Vector3 playerPos, float safeRadius)
        {
            int maxAttempts = 25; // Increase attempts to find a safe layout
            Vector3 spawnPos = Vector3.zero;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Select a position inside the screen limits (with boundary margins)
                float x = Random.Range(-7f, 7f);
                float y = Random.Range(-4f, 4f);
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
            // Top Gate Safe Zone: X is near 0, Y is near the top edge
            if (Mathf.Abs(pos.x) < 1.8f && pos.y > 2.8f) return true;

            // Bottom Gate Safe Zone: X is near 0, Y is near the bottom edge
            if (Mathf.Abs(pos.x) < 1.8f && pos.y < -2.8f) return true;

            // Left Gate Safe Zone: X is near the left edge, Y is near 0
            if (pos.x < -5.8f && Mathf.Abs(pos.y) < 1.5f) return true;

            // Right Gate Safe Zone: X is near the right edge, Y is near 0
            if (pos.x > 5.8f && Mathf.Abs(pos.y) < 1.5f) return true;

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
            GameObject obstacle = new GameObject("ProceduralObstacle");
            obstacle.transform.parent = _currentEnvContainer.transform;
            obstacle.transform.position = pos;

            // Add SpriteRenderer
            SpriteRenderer sr = obstacle.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            sr.sortingOrder = 3; // Render on top of background and decorations

            // Setup sizes and colors depending on biome
            float scaleX = Random.Range(0.6f, 1.2f);
            float scaleY = Random.Range(0.6f, 1.2f);
            obstacle.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            switch (biome)
            {
                case BiomeType.UrbanRuins:
                    sr.color = new Color(0.35f, 0.35f, 0.35f); // Gray Concrete Ruin
                    obstacle.name = "ConcreteRuin";
                    break;
                case BiomeType.Highways:
                    sr.color = new Color(0.7f, 0.25f, 0.05f); // Rusted orange barrier
                    obstacle.name = "RoadBarrier";
                    break;
                case BiomeType.OvergrownForests:
                    sr.color = new Color(0.12f, 0.28f, 0.08f); // Deep forest green pine tree
                    obstacle.name = "ForestPineTree";
                    break;
                case BiomeType.Highlands:
                    sr.color = new Color(0.48f, 0.44f, 0.4f); // Mountain stone boulder
                    obstacle.name = "Boulder";
                    break;
                case BiomeType.Waterways:
                    sr.color = new Color(0.2f, 0.4f, 0.35f); // Mossy water rock
                    obstacle.name = "MossyRock";
                    break;
                case BiomeType.SuburbanVillages:
                    sr.color = new Color(0.55f, 0.38f, 0.22f); // Wooden box crate
                    obstacle.name = "WoodCrate";
                    break;
                default:
                    sr.color = new Color(0.4f, 0.4f, 0.4f); // General rock
                    obstacle.name = "StoneObstacle";
                    break;
            }

            // Add collider to block physics
            BoxCollider2D col = obstacle.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        private void CreateProceduralDecoration(Vector3 pos, BiomeType biome)
        {
            GameObject decor = new GameObject("ProceduralDecoration");
            decor.transform.parent = _currentEnvContainer.transform;
            decor.transform.position = pos;

            // Add SpriteRenderer
            SpriteRenderer sr = decor.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            sr.sortingOrder = 1; // Under player and obstacles

            // Random rotation and sizing
            decor.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float size = Random.Range(0.2f, 0.5f);
            decor.transform.localScale = new Vector3(size, size, 1f);

            switch (biome)
            {
                case BiomeType.UrbanRuins:
                    sr.color = new Color(0.2f, 0.2f, 0.2f, 0.6f); // Dust/debris ash pile
                    decor.name = "AshDebris";
                    break;
                case BiomeType.OvergrownForests:
                    sr.color = new Color(0.4f, 0.8f, 0.2f, 0.7f); // Bright wild leaf prop
                    decor.name = "WildGrass";
                    break;
                case BiomeType.Waterways:
                    sr.color = new Color(0.1f, 0.6f, 0.8f, 0.4f); // Water puddle
                    decor.name = "Puddle";
                    break;
                default:
                    sr.color = new Color(0.45f, 0.4f, 0.35f, 0.7f); // Pebble / Sand patch
                    decor.name = "Pebble";
                    break;
            }
        }
    }
}
