using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [System.Serializable]
        public struct BiomeSpawnConfig
        {
            public BiomeType biome;
            public GameObject enemyPrefab;
            public string poolKey;
        }

        [Header("Procedural Spawning Configurations")]
        [SerializeField] private List<BiomeSpawnConfig> spawnConfigs;

        [Header("Default Fallback Spawner (Spawns on unconfigured biomes)")]
        [SerializeField] private GameObject defaultEnemyPrefab;
        [SerializeField] private string defaultPoolKey;

        private List<GameObject> _activeEnemies = new List<GameObject>();

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
        }

        private void OnDestroy()
        {
            // Unregister to prevent memory leaks
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= OnPlayerEnteredNewStage;
            }
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
            // 1. Clear any active enemies from the previous stage dynamically
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

            // 2. Reset Unity's random state using the deterministic stageSeed
            // This guarantees the exact same items/enemies spawn if player returns to this stage coordinates
            Random.InitState(stage.stageSeed);

            // 3. Find the spawn configuration for the current biome
            BiomeSpawnConfig currentConfig = new BiomeSpawnConfig();
            bool configFound = false;

            if (spawnConfigs != null)
            {
                foreach (BiomeSpawnConfig config in spawnConfigs)
                {
                    if (config.biome == stage.biome)
                    {
                        currentConfig = config;
                        configFound = true;
                        break;
                    }
                }
            }

            // Fall back to default spawner if no biome configuration exists
            if (!configFound && defaultEnemyPrefab != null)
            {
                currentConfig.enemyPrefab = defaultEnemyPrefab;
                currentConfig.poolKey = defaultPoolKey;
                configFound = true;
            }

            // 4. Day/Night Spawn Chance Check (Aliens: Night 80%, Day 20%)
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            if (configFound && currentConfig.enemyPrefab != null)
            {
                AlienAI alienCheck = currentConfig.enemyPrefab.GetComponent<AlienAI>();
                if (alienCheck != null)
                {
                    float spawnProbability = isNight ? 0.8f : 0.2f;
                    if (Random.value > spawnProbability)
                    {
                        Debug.Log($"[GameManager] Skipped spawning Aliens due to day/night probability (Is Night: {isNight})");
                        return; // Skip spawning for this stage
                    }
                }
            }

            // 5. Spawning local stage enemies (0-3 Enemies) based on configuration
            if (configFound && currentConfig.enemyPrefab != null)
            {
                int spawnCount = Random.Range(0, 4); // Randomly generates 0, 1, 2, or 3
                Debug.Log($"[GameManager] Spawning {spawnCount} enemies of type '{currentConfig.enemyPrefab.name}' on {stage.biome} stage using seed {stage.stageSeed}. (Is Night: {isNight})");

                for (int i = 0; i < spawnCount; i++)
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
                    if (!string.IsNullOrEmpty(currentConfig.poolKey) && ObjectPoolManager.Instance != null)
                    {
                        enemy = ObjectPoolManager.Instance.SpawnFromPool(currentConfig.poolKey, spawnPos, Quaternion.identity);
                    }
                    else
                    {
                        enemy = Instantiate(currentConfig.enemyPrefab, spawnPos, Quaternion.identity);
                    }

                    if (enemy != null)
                    {
                        _activeEnemies.Add(enemy);
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
