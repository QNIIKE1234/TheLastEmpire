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
        private StageData _currentStage;

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

        private void Start()
        {
            // Register to listen to the stage change event
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += OnPlayerEnteredNewStage;
                
                // Trigger initial setup for the starting stage
                OnPlayerEnteredNewStage(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
            else
            {
                Debug.LogWarning("GameManager: WorldMapManager.Instance is null at startup!");
            }

            // Register to listen to the Day/Night manager to reset cleared rooms at new day phase
            if (DayNightManager.Instance != null)
            {
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
                            posY = item.transform.position.y
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
                        Vector3 pos = new Vector3(itemData.posX, itemData.posY, 0f);
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
            if (stage.isCleared)
            {
                Debug.Log($"[GameManager] Stage ({stage.x}, {stage.y}) is already cleared today. Skipping spawn.");
                return;
            }

            // 4. GDD: Check if stage has previously saved remaining enemies
            if (stage.remainingEnemyPrefabNames != null)
            {
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

        private void SpawnEnemyInstance(GameObject prefab, string pKey)
        {
            // Select a random position within viewport limits (padding borders)
            float spawnX = Random.Range(-6f, 6f);
            float spawnY = Random.Range(-3.5f, 3.5f);

            // Prevent spawning directly on top of the player at (0, 0)
            if (Mathf.Abs(spawnX) < 1.5f && Mathf.Abs(spawnY) < 1.5f)
            {
                spawnX += Mathf.Sign(spawnX) * 2f;
                spawnY += Mathf.Sign(spawnY) * 2f;
            }

            Vector3 spawnPos = new Vector3(spawnX, spawnY, 0f);

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

        private void PlayBiomeBackgroundMusic(BiomeType biome)
        {
            // Placeholder: Switch audio tracks based on current biome type
            // e.g. SoundManager.Instance.PlayMusic(biomeMusicDictionary[biome]);
        }
    }
}
