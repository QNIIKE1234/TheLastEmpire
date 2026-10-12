using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [System.Flags]
        public enum BiomeGroup
        {
            None = 0,
            UrbanRuins = 1 << 0,
            Highways = 1 << 1,
            OvergrownForests = 1 << 2,
            Highlands = 1 << 3,
            Waterways = 1 << 4,
            SuburbanVillages = 1 << 5,
            SpecialEvent = 1 << 6,
            All = ~0
        }

        [System.Serializable]
        public struct BiomeSpawnConfig
        {
            [Tooltip("Select one or more biomes where this enemy type can spawn (Flags dropdown).")]
            public BiomeGroup biomes;
            public GameObject enemyPrefab;
            public string poolKey;
        }

        [Header("Procedural Spawning Configurations")]
        [SerializeField] private List<BiomeSpawnConfig> spawnConfigs;

        [Header("Default Fallback Spawner (Spawns on unconfigured biomes)")]
        [SerializeField] private GameObject defaultEnemyPrefab;
        [SerializeField] private string defaultPoolKey;

        [Header("Item Drop Prefabs")]
        [SerializeField] private CollectibleItem itemDropPrefab;
        [SerializeField] private CollectibleItem moneyDropPrefab;

        private List<GameObject> _activeEnemies = new List<GameObject>();
        private readonly HashSet<int> _usedSectorIndices = new HashSet<int>();
        private StageData _currentStage;

        [Header("NPC Spawning Settings")]
        [SerializeField] private GameObject npcPrefab;
        private GameObject _activeNPC;

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

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Clear any active enemies and stale references from the previous scene
            _currentStage = null;
            _activeEnemies.Clear();
            _usedSectorIndices.Clear();
            _activeNPC = null;

            // Trigger local stage content setup for the current stage coordinates
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= OnPlayerEnteredNewStage;
                WorldMapManager.Instance.OnStageChanged += OnPlayerEnteredNewStage;

                if (WorldMapManager.Instance.MapGenerator != null &&
                    WorldMapManager.Instance.MapGenerator.gridData != null &&
                    WorldMapManager.Instance.MapGenerator.gridData.Length > 0)
                {
                    OnPlayerEnteredNewStage(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
                }
            }

            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.OnTimePhaseChanged -= OnTimePhaseChanged;
                DayNightManager.Instance.OnTimePhaseChanged += OnTimePhaseChanged;
            }
        }

        private void Start()
        {
            // Register to listen to the stage change event
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= OnPlayerEnteredNewStage;
                WorldMapManager.Instance.OnStageChanged += OnPlayerEnteredNewStage;
                
                // Trigger initial setup for the starting stage if not already initialized
                if (_currentStage == null && WorldMapManager.Instance.MapGenerator != null && WorldMapManager.Instance.MapGenerator.gridData != null)
                {
                    OnPlayerEnteredNewStage(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
                }
            }
            else
            {
                Debug.LogWarning("GameManager: WorldMapManager.Instance is null at startup!");
            }

            // Register to listen to the Day/Night manager to reset cleared rooms at new day phase
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.OnTimePhaseChanged -= OnTimePhaseChanged;
                DayNightManager.Instance.OnTimePhaseChanged += OnTimePhaseChanged;
            }
        }

        private void OnDestroy()
        {
            // Unregister to prevent memory leaks
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= OnPlayerEnteredNewStage;
            }
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.OnTimePhaseChanged -= OnTimePhaseChanged;
            }
        }

        private void Update()
        {
            CheckStageClearedStatus();
        }

        // Called automatically whenever the player transitions coordinates
        private void OnPlayerEnteredNewStage(int x, int y)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
            if (stage == null) return;

            Debug.Log($"[GameManager] Player entered stage ({x}, {y}) | Biome: {stage.biome} | Seed: {stage.stageSeed}");

            // Execute local stage gameplay spawns and audio setups
            SpawnLocalStageContent(stage);
            PlayBiomeBackgroundMusic(stage.biome);
        }

        private void SpawnLocalStageContent(StageData stage)
        {
            // Clear active NPC if it exists
            if (_activeNPC != null)
            {
                Destroy(_activeNPC);
                _activeNPC = null;
            }

            // 1. Save the remaining alive enemies and items from the PREVIOUS stage before clearing
            if (_currentStage != null)
            {
                // Save remaining dropped items on the ground
                _currentStage.droppedItems.Clear();
                CollectibleItem[] itemsInScene = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
                foreach (CollectibleItem item in itemsInScene)
                {
                    if (item != null && item.gameObject.activeInHierarchy)
                    {
                        DroppedItemData itemData = new DroppedItemData
                        {
                            itemName = item.ItemName,
                            quantity = item.Quantity,
                            isMoney = item.IsMoney,
                            moneyAmount = item.MoneyAmount,
                            posX = item.transform.position.x,
                            posY = item.transform.position.z
                        };
                        _currentStage.droppedItems.Add(itemData);
                    }
                }
                Debug.Log($"[GameManager] Saved {_currentStage.droppedItems.Count} dropped items on the ground for previous Stage ({_currentStage.x}, {_currentStage.y})");

                if (_currentStage.isCleared)
                {
                    _currentStage.remainingEnemyPrefabNames = new List<string>();
                }
                else
                {
                    _currentStage.remainingEnemyPrefabNames = new List<string>();
                    foreach (GameObject enemy in _activeEnemies)
                    {
                        if (enemy != null)
                        {
                            Health enemyHealth = enemy.GetComponent<Health>();
                            // If the enemy is alive and active in the scene, preserve it!
                            if (enemyHealth != null && !enemyHealth.IsDead && enemy.activeSelf)
                            {
                                string cleanName = enemy.name.Replace("(Clone)", "").Trim();
                                _currentStage.remainingEnemyPrefabNames.Add(cleanName);
                            }
                        }
                    }
                    Debug.Log($"[GameManager] Saved {_currentStage.remainingEnemyPrefabNames.Count} remaining enemies for previous Stage ({_currentStage.x}, {_currentStage.y})");
                }
            }

            // 2. Clear any active enemies from the previous stage dynamically
            foreach (GameObject enemy in _activeEnemies)
            {
                if (enemy == null) continue;

                BaseEnemyAI ai = enemy.GetComponent<BaseEnemyAI>();
                string activePoolKey = ai != null ? ai.PoolKey : "";

                if (!string.IsNullOrEmpty(activePoolKey) && ObjectPoolManager.Instance != null)
                {
                    // Reset its health before returning it to the pool
                    Health enemyHealth = enemy.GetComponent<Health>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.ResetHealth();
                    }

                    ObjectPoolManager.Instance.ReturnToPool(activePoolKey, enemy);
                }
                else
                {
                    Destroy(enemy);
                }
            }
            _activeEnemies.Clear();
            _usedSectorIndices.Clear();

            // Clear any active collectible items from the previous stage dynamically
            CollectibleItem[] oldItems = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            foreach (CollectibleItem item in oldItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            // Set current stage pointer
            _currentStage = stage;

            // Generate stage-specific environment obstacles and props dynamically
            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.GenerateStageEnvironment(stage);
            }

            // Spawn saved dropped items back into the scene
            if (stage.droppedItems != null && stage.droppedItems.Count > 0)
            {
                Debug.Log($"[GameManager] Respawning {stage.droppedItems.Count} dropped items for Stage ({stage.x}, {stage.y})");
                foreach (DroppedItemData itemData in stage.droppedItems)
                {
                    CollectibleItem prefabToUse = itemData.isMoney ? moneyDropPrefab : itemDropPrefab;
                    if (prefabToUse != null)
                    {
                        Vector3 pos = new Vector3(itemData.posX, 0.5f, itemData.posY);
                        CollectibleItem spawnedItem = Instantiate(prefabToUse, pos, Quaternion.identity);
                        if (spawnedItem != null)
                        {
                            if (itemData.isMoney)
                            {
                                spawnedItem.SetMoneyDetails(itemData.moneyAmount);
                            }
                            else
                            {
                                spawnedItem.SetItemDetails(itemData.itemName, itemData.quantity);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[GameManager] Cannot respawn dropped item {itemData.itemName} because appropriate prefab is not assigned.");
                    }
                }
                // Clear the list after spawning to avoid duplicating if we exit again
                stage.droppedItems.Clear();
            }

            // 3. GDD: If this biome/stage has already been cleared today, DO NOT spawn enemies!
            if (stage.biome == BiomeType.SpecialEvent && stage.isShopStage)
            {
                SpawnNPCInstance(NPCType.Shop, new Vector3(0f, 2.0f, 0f));
                return; // Skip spawning enemies entirely!
            }

            // For instant testing/debugging: Spawn the Shop NPC in the starting room
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                int startX = WorldMapManager.Instance.MapGenerator.spawnX;
                int startY = WorldMapManager.Instance.MapGenerator.spawnY;
                if (stage.x == startX && stage.y == startY)
                {
                    SpawnNPCInstance(NPCType.Shop, new Vector3(0f, 2.0f, 3f));
                }
            }

            if (stage.isCleared)
            {
                Debug.Log($"[GameManager] Stage ({stage.x}, {stage.y}) is already cleared today. Skipping spawn.");
                return;
            }

            // 4. GDD: Check if stage has previously saved remaining enemies
            if (stage.remainingEnemyPrefabNames != null)
            {
                Random.InitState(stage.stageSeed + 777);
                _usedSectorIndices.Clear();
                Debug.Log($"[GameManager] Respawning {stage.remainingEnemyPrefabNames.Count} remaining saved enemies for Stage ({stage.x}, {stage.y})");
                foreach (string prefabName in stage.remainingEnemyPrefabNames)
                {
                    GameObject prefab = GetPrefabByName(prefabName);
                    if (prefab != null)
                    {
                        string pKey = GetPoolKeyForPrefab(prefab);
                        SpawnEnemyInstance(prefab, pKey);
                    }
                }
                return; // Skip new random spawning since we restored previous state!
            }

            // 5. Reset Unity's random state using the deterministic stageSeed
            // This guarantees the exact same items/enemies spawn if player returns to this stage coordinates
            Random.InitState(stage.stageSeed);

            // 6. Map stage BiomeType to BiomeGroup Flags
            BiomeGroup stageGroup = stage.biome switch
            {
                BiomeType.UrbanRuins => BiomeGroup.UrbanRuins,
                BiomeType.Highways => BiomeGroup.Highways,
                BiomeType.OvergrownForests => BiomeGroup.OvergrownForests,
                BiomeType.Highlands => BiomeGroup.Highlands,
                BiomeType.Waterways => BiomeGroup.Waterways,
                BiomeType.SuburbanVillages => BiomeGroup.SuburbanVillages,
                BiomeType.SpecialEvent => BiomeGroup.SpecialEvent,
                _ => BiomeGroup.None
            };

            // 7. Find all matching configurations (including 'All' flag or direct match)
            List<BiomeSpawnConfig> configsToSpawn = new List<BiomeSpawnConfig>();

            if (spawnConfigs != null)
            {
                foreach (BiomeSpawnConfig config in spawnConfigs)
                {
                    if (config.enemyPrefab != null && (config.biomes & stageGroup) != 0)
                    {
                        configsToSpawn.Add(config);
                    }
                }
            }

            // Fall back to default spawner if no matching configurations exist
            if (configsToSpawn.Count == 0 && defaultEnemyPrefab != null)
            {
                BiomeSpawnConfig fallbackConfig = new BiomeSpawnConfig
                {
                    biomes = stageGroup,
                    enemyPrefab = defaultEnemyPrefab,
                    poolKey = defaultPoolKey
                };
                configsToSpawn.Add(fallbackConfig);
            }

            // 8. Day/Night phase
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;

            // Initialize remaining enemies list to start tracking
            stage.remainingEnemyPrefabNames = new List<string>();
            _usedSectorIndices.Clear();

            // 9. Spawn enemies for each matching configuration
            foreach (BiomeSpawnConfig currentConfig in configsToSpawn)
            {
                // Day/Night Spawn Chance Check (Aliens: Night 80%, Day 20%)
                AlienAI alienCheck = currentConfig.enemyPrefab.GetComponent<AlienAI>();
                if (alienCheck != null)
                {
                    float spawnProbability = isNight ? 0.8f : 0.2f;
                    if (Random.value > spawnProbability)
                    {
                        Debug.Log($"[GameManager] Skipped spawning Aliens due to day/night probability (Is Night: {isNight})");
                        continue; // Skip this particular enemy type, keep others!
                    }
                }

                int spawnCount = Random.Range(0, 4); // Randomly generates 0, 1, 2, or 3

                // If this is a Boss, limit spawn to exactly 1
                LootDropper loot = currentConfig.enemyPrefab.GetComponent<LootDropper>();
                if (loot != null && loot.IsBoss)
                {
                    spawnCount = 1;
                }

                Debug.Log($"[GameManager] Spawning {spawnCount} enemies of type '{currentConfig.enemyPrefab.name}' on {stage.biome} stage using seed {stage.stageSeed}. (Is Night: {isNight})");

                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnEnemyInstance(currentConfig.enemyPrefab, currentConfig.poolKey);
                    
                    // Add prefab name to tracking list
                    stage.remainingEnemyPrefabNames.Add(currentConfig.enemyPrefab.name);
                }
            }
        }

        private struct SpawnSector
        {
            public string name;
            public float minX;
            public float maxX;
            public float minZ;
            public float maxZ;
            public Vector3 center;

            public SpawnSector(string name, float minX, float maxX, float minZ, float maxZ)
            {
                this.name = name;
                this.minX = minX;
                this.maxX = maxX;
                this.minZ = minZ;
                this.maxZ = maxZ;
                this.center = new Vector3((minX + maxX) * 0.5f, 0.2f, (minZ + maxZ) * 0.5f);
            }
        }

        // 12 distinct spatial sectors across the 32x18m stage ensuring enemies spawn widely dispersed across corners & edges
        private static readonly SpawnSector[] StageSectors = new SpawnSector[]
        {
            // Row 0: North / Top side (Z: 3.2 to 6.2)
            new SpawnSector("Top-Left (NW)",       -12.0f, -6.5f,   3.2f,  6.2f),
            new SpawnSector("Top-Mid-Left",         -6.0f, -1.0f,   3.2f,  6.2f),
            new SpawnSector("Top-Mid-Right",         1.0f,  6.0f,   3.2f,  6.2f),
            new SpawnSector("Top-Right (NE)",        6.5f, 12.0f,   3.2f,  6.2f),

            // Row 1: Middle band (Z: -1.8 to 1.8)
            new SpawnSector("Far-West",            -12.0f, -7.0f,  -1.8f,  1.8f),
            new SpawnSector("Inner-West",           -6.0f, -1.5f,  -1.8f,  1.8f),
            new SpawnSector("Inner-East",            1.5f,  6.0f,  -1.8f,  1.8f),
            new SpawnSector("Far-East",              7.0f, 12.0f,  -1.8f,  1.8f),

            // Row 2: South / Bottom side (Z: -6.2 to -3.2)
            new SpawnSector("Bottom-Left (SW)",    -12.0f, -6.5f,  -6.2f, -3.2f),
            new SpawnSector("Bottom-Mid-Left",      -6.0f, -1.0f,  -6.2f, -3.2f),
            new SpawnSector("Bottom-Mid-Right",      1.0f,  6.0f,  -6.2f, -3.2f),
            new SpawnSector("Bottom-Right (SE)",     6.5f, 12.0f,  -6.2f, -3.2f),
        };

        private void SpawnEnemyInstance(GameObject prefab, string pKey)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

            // Remove null or inactive enemies from tracking list
            _activeEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);

            // Sync physics transforms so newly instantiated obstacles have up-to-date colliders
            Physics.SyncTransforms();

            Vector3 spawnPos = FindDistributedSpawnPosition(playerPos);

            GameObject enemy;
            if (!string.IsNullOrEmpty(pKey) && ObjectPoolManager.Instance != null)
            {
                enemy = ObjectPoolManager.Instance.SpawnFromPool(pKey, spawnPos, Quaternion.identity);
            }
            else
            {
                enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            }

            if (enemy != null)
            {
                _activeEnemies.Add(enemy);
            }
        }

        private Vector3 FindDistributedSpawnPosition(Vector3 playerPos)
        {
            // 1. Gather all sectors that haven't been claimed yet in this stage
            List<int> availableSectorIndices = new List<int>();
            for (int i = 0; i < StageSectors.Length; i++)
            {
                if (!_usedSectorIndices.Contains(i))
                {
                    availableSectorIndices.Add(i);
                }
            }

            // If all sectors were used (e.g. >12 enemies), reset so all sectors are available again
            if (availableSectorIndices.Count == 0)
            {
                _usedSectorIndices.Clear();
                for (int i = 0; i < StageSectors.Length; i++)
                {
                    availableSectorIndices.Add(i);
                }
            }

            // 2. Prioritize sectors that maintain generous distance from player (>= 4.5m) and existing enemies (>= 4.5m)
            List<int> preferredSectors = new List<int>();
            List<int> acceptableSectors = new List<int>();

            foreach (int idx in availableSectorIndices)
            {
                SpawnSector sector = StageSectors[idx];
                float distToPlayer = Vector3.Distance(sector.center, playerPos);

                float minDistToEnemy = float.MaxValue;
                foreach (GameObject enemy in _activeEnemies)
                {
                    if (enemy != null && enemy.activeInHierarchy)
                    {
                        float d = Vector3.Distance(sector.center, enemy.transform.position);
                        if (d < minDistToEnemy) minDistToEnemy = d;
                    }
                }

                if (distToPlayer >= 4.5f && minDistToEnemy >= 4.5f)
                {
                    preferredSectors.Add(idx);
                }
                else if (distToPlayer >= 3.2f && minDistToEnemy >= 3.2f)
                {
                    acceptableSectors.Add(idx);
                }
            }

            List<int> candidateSectors = preferredSectors.Count > 0 ? preferredSectors :
                                        (acceptableSectors.Count > 0 ? acceptableSectors : availableSectorIndices);

            // Shuffle candidates so enemy choice is randomized across available zones
            ShuffleList(candidateSectors);

            // 3. Try finding an obstacle-free point inside each candidate sector
            foreach (int sectorIdx in candidateSectors)
            {
                SpawnSector sector = StageSectors[sectorIdx];

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float x = Random.Range(sector.minX + 0.3f, sector.maxX - 0.3f);
                    float z = Random.Range(sector.minZ + 0.3f, sector.maxZ - 0.3f);
                    Vector3 candidatePos = new Vector3(x, 0.2f, z);

                    if (Vector3.Distance(candidatePos, playerPos) < 3.8f) continue;

                    bool tooCloseToEnemy = false;
                    foreach (GameObject enemy in _activeEnemies)
                    {
                        if (enemy != null && enemy.activeInHierarchy && Vector3.Distance(candidatePos, enemy.transform.position) < 4.2f)
                        {
                            tooCloseToEnemy = true;
                            break;
                        }
                    }
                    if (tooCloseToEnemy) continue;

                    if (IsPositionSafeFromObstacles(candidatePos))
                    {
                        _usedSectorIndices.Add(sectorIdx);
                        return candidatePos;
                    }
                }
            }

            // 4. Fallback: Find the globally most isolated point on map maximizing distance from all existing enemies
            return FindFurthestIsolatedPoint(playerPos);
        }

        private bool IsPositionSafeFromObstacles(Vector3 candidatePos)
        {
            Collider[] colliders = Physics.OverlapSphere(candidatePos, 0.65f);
            foreach (Collider col in colliders)
            {
                if (col == null || col.isTrigger) continue;
                if (IsIgnorableGroundOrWalkable(col)) continue;

                // Hit a solid building, wall, prop, or blocker
                return false;
            }
            return true;
        }

        private bool IsIgnorableGroundOrWalkable(Collider col)
        {
            if (col == null || col.isTrigger) return true;
            if (col.CompareTag("Ground")) return true;

            string colName = col.gameObject.name.ToLowerInvariant();
            if (colName.Contains("ground") || 
                colName.Contains("floor") || 
                colName.Contains("road") || 
                colName.Contains("street") || 
                colName.Contains("terrain") ||
                colName.Contains("plane"))
            {
                return true;
            }

            // Flat floor colliders (height <= 0.25m) are safe walkable surfaces
            if (col.bounds.max.y <= 0.25f && col.bounds.size.y <= 0.5f)
            {
                return true;
            }

            return false;
        }

        private Vector3 FindFurthestIsolatedPoint(Vector3 playerPos)
        {
            float maxSpawnX = 12.0f;
            float maxSpawnZ = 6.0f;

            Vector3 bestPos = Vector3.zero;
            float bestScore = -1f;

            // Sample a 5x4 grid across the stage
            for (float x = -maxSpawnX; x <= maxSpawnX; x += 6.0f)
            {
                for (float z = -maxSpawnZ; z <= maxSpawnZ; z += 4.0f)
                {
                    Vector3 candidatePos = new Vector3(x, 0.2f, z);

                    if (Vector3.Distance(candidatePos, playerPos) < 3.2f) continue;
                    if (!IsPositionSafeFromObstacles(candidatePos)) continue;

                    float minDistToEnemy = float.MaxValue;
                    foreach (GameObject enemy in _activeEnemies)
                    {
                        if (enemy != null && enemy.activeInHierarchy)
                        {
                            float d = Vector3.Distance(candidatePos, enemy.transform.position);
                            if (d < minDistToEnemy) minDistToEnemy = d;
                        }
                    }

                    float score = minDistToEnemy == float.MaxValue ? 100f : minDistToEnemy;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candidatePos;
                    }
                }
            }

            if (bestScore > 0f)
            {
                return bestPos;
            }

            // Absolute fallback: pick opposite corner from player
            float oppX = playerPos.x > 0 ? -9.5f : 9.5f;
            float oppZ = playerPos.z > 0 ? -4.5f : 4.5f;
            return new Vector3(oppX, 0.2f, oppZ);
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private GameObject GetPrefabByName(string prefabName)
        {
            if (defaultEnemyPrefab != null && defaultEnemyPrefab.name == prefabName)
            {
                return defaultEnemyPrefab;
            }
            if (spawnConfigs != null)
            {
                foreach (BiomeSpawnConfig config in spawnConfigs)
                {
                    if (config.enemyPrefab != null && config.enemyPrefab.name == prefabName)
                    {
                        return config.enemyPrefab;
                    }
                }
            }
            return null;
        }

        private string GetPoolKeyForPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            if (defaultEnemyPrefab != null && defaultEnemyPrefab == prefab)
            {
                return defaultPoolKey;
            }
            if (spawnConfigs != null)
            {
                foreach (BiomeSpawnConfig config in spawnConfigs)
                {
                    if (config.enemyPrefab == prefab)
                    {
                        return config.poolKey;
                    }
                }
            }
            return null;
        }

        private void CheckStageClearedStatus()
        {
            // If we have active enemies spawned but no player coordinates, skip
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;
            if (_activeEnemies.Count == 0) return;

            // Check if current stage is already cleared to avoid redundant calls
            int x = WorldMapManager.Instance.CurrentPlayerX;
            int y = WorldMapManager.Instance.CurrentPlayerY;
            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
            if (stage == null || stage.isCleared) return;

            // Check if all spawned enemies in the current room are defeated
            bool allEnemiesDead = true;
            foreach (GameObject enemy in _activeEnemies)
            {
                if (enemy != null && enemy.activeSelf)
                {
                    Health enemyHealth = enemy.GetComponent<Health>();
                    if (enemyHealth != null && !enemyHealth.IsDead)
                    {
                        allEnemiesDead = false;
                        break;
                    }
                }
            }

            if (allEnemiesDead)
            {
                stage.isCleared = true;
                if (stage.remainingEnemyPrefabNames != null)
                {
                    stage.remainingEnemyPrefabNames.Clear();
                }
                Debug.Log($"[GameManager] Stage ({x}, {y}) has been successfully CLEARED! Enemies will not respawn until a new Day begins.");
            }
        }

        private void OnTimePhaseChanged(bool isNight)
        {
            // Transitioning from Night to Day (isNight is false) indicates a "new Day starts"!
            if (!isNight && WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                Debug.Log("[GameManager] A new Day has started! Resetting all cleared stages, remaining enemy counts, and dropped items.");
                
                // Reset isCleared status, clear saved enemy lists, and clear persistent dropped items of all map stages to allow fresh respawning
                foreach (StageData stage in WorldMapManager.Instance.MapGenerator.gridData)
                {
                    if (stage != null)
                    {
                        stage.isCleared = false;
                        stage.remainingEnemyPrefabNames = null; // resets to trigger fresh spawning
                        stage.droppedItems.Clear(); // clear any items that were left on the ground
                    }
                }
            }
        }

        private void SpawnNPCInstance(NPCType type, Vector3 spawnPos)
        {
            if (npcPrefab != null)
            {
                _activeNPC = Instantiate(npcPrefab, spawnPos, Quaternion.identity);
                NPCController npc = _activeNPC.GetComponent<NPCController>();
                if (npc == null)
                {
                    npc = _activeNPC.AddComponent<NPCController>();
                }
                npc.SetNPCType(type);

                if (type == NPCType.Shop)
                {
                    npc.SetDialogueId("Merchant_Intro");
                    if (_currentStage != null)
                    {
                        if (_currentStage.savedShopItems != null && _currentStage.savedShopItems.Count > 0)
                        {
                            npc.SetShopItems(_currentStage.savedShopItems);
                        }
                        else
                        {
                            _currentStage.savedShopItems = npc.ShopItems;
                        }
                    }
                }
            }
            else
            {
                // Fallback: Create a clean programmatic capsule for NPC
                _activeNPC = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _activeNPC.name = $"{type}NPC_Fallback";
                _activeNPC.transform.position = spawnPos;
                _activeNPC.transform.localScale = new Vector3(1f, 1f, 1f);

                // Add NPCController component
                NPCController npc = _activeNPC.AddComponent<NPCController>();
                npc.SetNPCType(type);

                if (type == NPCType.Shop)
                {
                    npc.SetDialogueId("Merchant_Intro");
                    if (_currentStage != null)
                    {
                        if (_currentStage.savedShopItems != null && _currentStage.savedShopItems.Count > 0)
                        {
                            npc.SetShopItems(_currentStage.savedShopItems);
                        }
                        else
                        {
                            _currentStage.savedShopItems = npc.ShopItems;
                        }
                    }
                }

                // Color it a nice cyan to distinguish it
                Renderer rend = _activeNPC.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    rend.material.color = new Color(0f, 0.9f, 1f); // Cool cyan
                }
            }
        }

        private void PlayBiomeBackgroundMusic(BiomeType biome)
        {
            // Placeholder: Switch audio tracks based on current biome type
            // e.g. SoundManager.Instance.PlayMusic(biomeMusicDictionary[biome]);
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize 12 spawn sectors in Unity Scene view for easy debugging
            Color[] colors = new Color[]
            {
                new Color(0f, 1f, 0.5f, 0.25f),
                new Color(0f, 0.7f, 1f, 0.25f),
                new Color(1f, 0.8f, 0.2f, 0.25f),
                new Color(1f, 0.3f, 0.5f, 0.25f)
            };

            for (int i = 0; i < StageSectors.Length; i++)
            {
                var s = StageSectors[i];
                Gizmos.color = colors[i % colors.Length];
                Vector3 size = new Vector3(s.maxX - s.minX, 0.4f, s.maxZ - s.minZ);
                Gizmos.DrawCube(s.center, size);
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(s.center, size);
            }
        }
    }
}
