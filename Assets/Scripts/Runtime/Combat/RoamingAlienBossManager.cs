using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Roaming World Boss controller for the Alien Overlord (AlienLeader).
    /// - Pre-rolls a valid walkable stage on the 128x128 world map on world creation and slowly roams across stages.
    /// - Persistent 50,000 HP that NEVER resets when warping away or when the player leaves the stage.
    /// - When encountered on its stage:
    ///     * Summons 2-3 Aliens every 10 seconds around the boss.
    ///     * After 30 seconds (if still alive), warps away with a violet portal VFX and relocates to a new random stage.
    /// - Automatically syncs in Co-op via GameManager.ActiveEnemies + NetworkEnemyBridge (Host authoritative).
    /// </summary>
    public class RoamingAlienBossManager : MonoBehaviour
    {
        public static RoamingAlienBossManager Instance { get; private set; }

        public const float BossMaxHealth = 50000f;
        public const float SummonIntervalSeconds = 10f;
        public const float EncounterWarpSeconds = 30f;
        public const float WorldRoamIntervalSeconds = 35f;
        public const float BossScale = 1.25f;

        private static readonly string[] SummonableAlienKeys =
        {
            "AlienNormal",
            "AlienCrawler",
            "AlienHook",
            "AlienSpitter",
            "AlienSmart",
            "AlienInvisible"
        };

        public int BossStageX { get; private set; } = -1;
        public int BossStageY { get; private set; } = -1;
        public float RemainingHealth { get; private set; } = BossMaxHealth;
        public bool IsDefeated { get; private set; } = false;
        public bool HasValidLocation => BossStageX >= 0 && BossStageY >= 0 && !IsDefeated;
        public bool IsBossCurrentlySpawned => _activeBossObj != null && _activeBossObj.activeInHierarchy;
        public float EncounterTimeRemaining => IsBossCurrentlySpawned ? Mathf.Max(0f, EncounterWarpSeconds - _encounterTimer) : 0f;
        public float NextSummonTimeRemaining => IsBossCurrentlySpawned ? Mathf.Max(0f, _summonTimer) : 0f;

        public event Action<int, int> OnBossLocationChanged;

        private GameObject _activeBossObj;
        private Health _activeBossHealth;
        private AlienAI _activeBossAI;

        private float _encounterTimer = 0f;
        private float _summonTimer = SummonIntervalSeconds;
        private float _worldRoamTimer = WorldRoamIntervalSeconds;
        private bool _isWarpingOut = false;
        private bool _subscribedWorldMap = false;

        // Top banner feedback
        private string _bannerText = "";
        private Color _bannerColor = new Color(0.82f, 0.32f, 1f, 1f);
        private float _bannerTimer = 0f;

        // Cached GUI styles
        private Texture2D _whiteTex;
        private GUIStyle _bossTitleStyle;
        private GUIStyle _bossHpTextStyle;
        private GUIStyle _warpBadgeStyle;
        private GUIStyle _bannerStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[RoamingAlienBossManager]");
                DontDestroyOnLoad(go);
                go.AddComponent<RoamingAlienBossManager>();
            }
        }

        public static bool IsBossObject(GameObject obj)
        {
            if (obj == null || Instance == null) return false;
            return obj == Instance._activeBossObj;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeWorldMap();
            if (_whiteTex != null)
            {
                Destroy(_whiteTex);
                _whiteTex = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _activeBossObj = null;
            _activeBossHealth = null;
            _activeBossAI = null;
            _isWarpingOut = false;
            TrySubscribeWorldMap();
        }

        private void TrySubscribeWorldMap()
        {
            if (_subscribedWorldMap || WorldMapManager.Instance == null) return;
            WorldMapManager.Instance.OnStageChanged -= HandleStageChanged;
            WorldMapManager.Instance.OnStageChanged += HandleStageChanged;
            _subscribedWorldMap = true;
        }

        private void UnsubscribeWorldMap()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleStageChanged;
            }
            _subscribedWorldMap = false;
        }

        // =========================================================================
        //  WORLD INITIALIZATION & PERSISTENT SAVE/LOAD
        // =========================================================================

        public void InitializeForNewWorld(WorldMapGenerator mapGen, int worldSeed)
        {
            DespawnActiveBossSilently(saveHealth: false);

            RemainingHealth = BossMaxHealth;
            IsDefeated = false;
            _encounterTimer = 0f;
            _summonTimer = SummonIntervalSeconds;
            _worldRoamTimer = WorldRoamIntervalSeconds;
            _isWarpingOut = false;

            TrySubscribeWorldMap();

            if (EmpireNetworkManager.IsOnlineClient)
            {
                BossStageX = -1;
                BossStageY = -1;
                return;
            }

            PickRandomStage(mapGen,
                avoidX: mapGen != null ? mapGen.spawnX : 64,
                avoidY: mapGen != null ? mapGen.spawnY : 64,
                centerX: mapGen != null ? mapGen.spawnX : 64,
                centerY: mapGen != null ? mapGen.spawnY : 64,
                minRadius: 3,
                maxRadius: 14,
                deterministicSeed: worldSeed ^ 0x5A17B055);

            Debug.Log($"<color=#D946EF>[RoamingAlienBoss]</color> World Boss initialized! Stage=({BossStageX}, {BossStageY}), HP={RemainingHealth:N0}/{BossMaxHealth:N0}");
        }

        public void RestoreFromSave(WorldMapGenerator mapGen, int stageX, int stageY, float remainingHp, bool defeated)
        {
            DespawnActiveBossSilently(saveHealth: false);

            IsDefeated = defeated || remainingHp <= 0f;
            RemainingHealth = IsDefeated ? 0f : Mathf.Clamp(remainingHp > 0f ? remainingHp : BossMaxHealth, 1f, BossMaxHealth);
            _encounterTimer = 0f;
            _summonTimer = SummonIntervalSeconds;
            _worldRoamTimer = WorldRoamIntervalSeconds;
            _isWarpingOut = false;

            TrySubscribeWorldMap();

            if (IsDefeated)
            {
                BossStageX = -1;
                BossStageY = -1;
                OnBossLocationChanged?.Invoke(BossStageX, BossStageY);
                return;
            }

            bool validSavedCoord = mapGen != null &&
                                   stageX >= 0 && stageX < WorldMapGenerator.GridSize &&
                                   stageY >= 0 && stageY < WorldMapGenerator.GridSize &&
                                   IsValidBossStage(mapGen.GetStage(stageX, stageY));

            if (validSavedCoord)
            {
                BossStageX = stageX;
                BossStageY = stageY;
                OnBossLocationChanged?.Invoke(BossStageX, BossStageY);
            }
            else if (mapGen != null)
            {
                PickRandomStage(mapGen, mapGen.spawnX, mapGen.spawnY, mapGen.spawnX, mapGen.spawnY, 3, 14, mapGen.seed ^ 0x5A17B055);
            }

            Debug.Log($"<color=#D946EF>[RoamingAlienBoss]</color> Restored from save: Stage=({BossStageX}, {BossStageY}), HP={RemainingHealth:N0}/{BossMaxHealth:N0}, Defeated={IsDefeated}");
        }

        // =========================================================================
        //  STAGE ENTRY & UPDATE LOOP
        // =========================================================================

        private void Update()
        {
            if (!_subscribedWorldMap && WorldMapManager.Instance != null)
            {
                TrySubscribeWorldMap();
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
            }

            if (EmpireNetworkManager.IsOnlineClient) return;
            if (IsDefeated) return;
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            // Only run inside Gameplay_3D (when a player exists)
            PlayerController localPlayer = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPlayer == null) return;

            // Ensure initial stage is always pre-rolled even if scene started directly in Editor
            if (BossStageX < 0 || BossStageY < 0)
            {
                InitializeForNewWorld(WorldMapManager.Instance.MapGenerator, WorldMapManager.Instance.MapGenerator.seed);
            }

            // Pause encounter timers while inside a building interior
            if (BuildingInteriorManager.IsInsideInterior) return;

            // 1. Active Encounter on current stage
            if (_activeBossObj != null)
            {
                if (_activeBossHealth != null)
                {
                    if (_activeBossHealth.IsDead)
                    {
                        HandleBossDefeated();
                        return;
                    }
                    RemainingHealth = _activeBossHealth.CurrentHealth;
                }

                if (_isWarpingOut) return;

                _encounterTimer += Time.deltaTime;
                _summonTimer -= Time.deltaTime;

                // Every 10 seconds: summon 2-3 Aliens around the boss!
                if (_summonTimer <= 0f && _encounterTimer < EncounterWarpSeconds - 0.5f)
                {
                    _summonTimer = SummonIntervalSeconds;
                    SummonAlienWave();
                }

                // After 30 seconds: warp out and relocate randomly across the world map!
                if (_encounterTimer >= EncounterWarpSeconds)
                {
                    StartCoroutine(WarpBossAwayRoutine());
                }
                return;
            }

            // 2. If player is currently on the boss's stage (e.g. just exited an interior or scene start), spawn the boss!
            if (WorldMapManager.Instance.CurrentPlayerX == BossStageX &&
                WorldMapManager.Instance.CurrentPlayerY == BossStageY)
            {
                SpawnBossOnCurrentStage();
                return;
            }

            // 3. Passive world-map roaming while player is on another stage (moves 1 walkable tile every ~35s)
            _worldRoamTimer -= Time.deltaTime;
            if (_worldRoamTimer <= 0f)
            {
                _worldRoamTimer = WorldRoamIntervalSeconds;
                StepRoamOneAdjacentStage();
            }
        }

        private void HandleStageChanged(int newX, int newY)
        {
            if (EmpireNetworkManager.IsOnlineClient || IsDefeated) return;

            // Save HP and clear old boss reference if somehow still present
            if (_activeBossObj != null)
            {
                DespawnActiveBossSilently(saveHealth: true);
            }

            if (newX == BossStageX && newY == BossStageY && !BuildingInteriorManager.IsInsideInterior)
            {
                StartCoroutine(SpawnBossDelayedOneFrame());
            }
        }

        private IEnumerator SpawnBossDelayedOneFrame()
        {
            // Wait 1 frame so EnvironmentManager finishes building obstacles before picking a clear spawn spot
            yield return null;
            if (EmpireNetworkManager.IsOnlineClient || IsDefeated) yield break;
            if (BuildingInteriorManager.IsInsideInterior) yield break;
            if (WorldMapManager.Instance == null) yield break;
            if (WorldMapManager.Instance.CurrentPlayerX != BossStageX || WorldMapManager.Instance.CurrentPlayerY != BossStageY) yield break;

            SpawnBossOnCurrentStage();
        }

        public void NotifyBossDespawnedOnStageLeave()
        {
            if (_activeBossHealth != null && !_activeBossHealth.IsDead)
            {
                RemainingHealth = Mathf.Clamp(_activeBossHealth.CurrentHealth, 1f, BossMaxHealth);
            }
            _activeBossObj = null;
            _activeBossHealth = null;
            _activeBossAI = null;
            _isWarpingOut = false;
        }

        // =========================================================================
        //  BOSS SPAWNING, 10s ALIEN SUMMONING & 30s WARP RELOCATION
        // =========================================================================

        private void SpawnBossOnCurrentStage()
        {
            if (_activeBossObj != null || IsDefeated || RemainingHealth <= 0f) return;

            GameObject bossPrefab = GameManager.Instance != null
                ? GameManager.Instance.GetPrefabByName("AlienLeader")
                : AddressableManager.LoadAsset<GameObject>("Prefabs/Character/AlienLeader");

            if (bossPrefab == null)
            {
                Debug.LogError("[RoamingAlienBoss] Could not load 'Prefabs/Character/AlienLeader'!");
                return;
            }

            PlayerController player = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            Vector3 spawnPos = GameManager.Instance != null
                ? GameManager.Instance.FindClearBossSpawnPosition(playerPos)
                : new Vector3(playerPos.x > 0f ? -7.5f : 7.5f, 0.25f, playerPos.z > 0f ? -3.5f : 3.5f);
            spawnPos.y = 0.25f;

            Vector3 lookDir = playerPos - spawnPos;
            lookDir.y = 0f;
            Quaternion spawnRot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir.normalized) : Quaternion.identity;

            GameObject bossObj = Instantiate(bossPrefab, spawnPos, spawnRot);
            // Keep exact prefab name so NetworkEnemyBridge spawns AlienLeader on Co-op clients!
            bossObj.name = "AlienLeader";
            bossObj.transform.localScale = Vector3.one * BossScale;

            AlienAI ai = bossObj.GetComponent<AlienAI>();
            if (ai != null)
            {
                ai.IsRoamingWorldBoss = true;
                ai.IsWarpingOut = false;
                ai.IsVomitingSummon = false;
                ai.SetPoolKey(""); // Never pool the persistent World Boss
                ai.SkipTransitionDelay();
            }

            if (bossObj.GetComponent<AlienBossVisualController>() == null)
            {
                bossObj.AddComponent<AlienBossVisualController>();
            }

            Health h = bossObj.GetComponent<Health>();
            if (h != null)
            {
                h.InitializePersistentHealth(RemainingHealth, BossMaxHealth);
                h.onHealthChanged.AddListener(HandleBossHealthChanged);
                h.onDeath.AddListener(HandleBossDefeated);
            }

            if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null && !GameManager.Instance.ActiveEnemies.Contains(bossObj))
            {
                GameManager.Instance.ActiveEnemies.Add(bossObj);
            }

            _activeBossObj = bossObj;
            _activeBossHealth = h;
            _activeBossAI = ai;
            _encounterTimer = 0f;
            _summonTimer = SummonIntervalSeconds;
            _isWarpingOut = false;

            SpawnWarpPortalVfx(spawnPos, new Color(0.75f, 0.22f, 1f, 0.85f), 2.1f, 1.0f);
            ShowBanner($"ALIEN OVERLORD DETECTED!  ({RemainingHealth:N0} / {BossMaxHealth:N0} HP)", new Color(0.86f, 0.30f, 1f, 1f), 4.0f);
            Debug.Log($"<color=#D946EF>[RoamingAlienBoss]</color> Spawned Alien Overlord on Stage ({BossStageX}, {BossStageY}) with {RemainingHealth:N0}/{BossMaxHealth:N0} HP!");
        }

        private void SummonAlienWave()
        {
            if (_activeBossObj == null || _activeBossHealth == null || _activeBossHealth.IsDead) return;
            if (_activeBossAI != null && _activeBossAI.IsVomitingSummon) return;

            StartCoroutine(VomitSummonAlienWaveRoutine());
        }

        private IEnumerator VomitSummonAlienWaveRoutine()
        {
            if (_activeBossObj == null || _activeBossHealth == null || _activeBossHealth.IsDead) yield break;

            AlienAI bossAi = _activeBossAI;
            if (bossAi != null) bossAi.IsVomitingSummon = true;

            AlienBossVisualController visual = _activeBossObj.GetComponent<AlienBossVisualController>();
            if (visual == null) visual = _activeBossObj.AddComponent<AlienBossVisualController>();

            bool vomitSpawned = false;
            Coroutine seq = visual.TriggerVomitSummonSequence((mouthPos, forwardDir) =>
            {
                vomitSpawned = true;
                SpawnVomitedAliensFromMouth(mouthPos, forwardDir);
            });

            if (seq != null)
            {
                yield return seq;
            }
            else if (!vomitSpawned)
            {
                SpawnVomitedAliensFromMouth(visual.GetMouthWorldPosition(), _activeBossObj.transform.forward);
            }

            if (bossAi != null) bossAi.IsVomitingSummon = false;
        }

        private void SpawnVomitedAliensFromMouth(Vector3 mouthPos, Vector3 forwardDir)
        {
            if (_activeBossObj == null || _activeBossHealth == null || _activeBossHealth.IsDead) return;

            int count = UnityEngine.Random.Range(2, 4); // 2 or 3 Aliens vomited out!
            int spawned = 0;

            Vector3 flatFwd = new Vector3(forwardDir.x, 0f, forwardDir.z);
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            flatFwd.Normalize();

            for (int i = 0; i < count; i++)
            {
                string alienKey = SummonableAlienKeys[UnityEngine.Random.Range(0, SummonableAlienKeys.Length)];
                GameObject prefab = GameManager.Instance != null
                    ? GameManager.Instance.GetPrefabByName(alienKey)
                    : AddressableManager.LoadAsset<GameObject>($"Prefabs/Character/{alienKey}");

                if (prefab == null) continue;

                // Fan landing spots out in front of the boss's open mouth!
                float fanOffset = count == 2 ? (i == 0 ? -28f : 28f) : ((i - 1) * 32f);
                float yawDeg = fanOffset + UnityEngine.Random.Range(-10f, 10f);
                Vector3 launchDir = Quaternion.Euler(0f, yawDeg, 0f) * flatFwd;
                float landDist = UnityEngine.Random.Range(2.4f, 4.0f);

                Vector3 landingPos = new Vector3(
                    Mathf.Clamp(mouthPos.x + launchDir.x * landDist, -12.5f, 12.5f),
                    0.25f,
                    Mathf.Clamp(mouthPos.z + launchDir.z * landDist, -6.5f, 6.5f));

                GameObject minion = GameManager.Instance != null
                    ? GameManager.Instance.SpawnEnemyInstanceAt(prefab, alienKey, mouthPos, Quaternion.LookRotation(launchDir))
                    : Instantiate(prefab, mouthPos, Quaternion.LookRotation(launchDir));

                if (minion != null)
                {
                    BaseEnemyAI minionAi = minion.GetComponent<BaseEnemyAI>();
                    if (minionAi != null)
                    {
                        minionAi.SetPoolKey(alienKey);
                        minionAi.SkipTransitionDelay();
                        minionAi.enabled = false;
                    }

                    Collider col = minion.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    // Launch alien right out of the boss's gaping mouth in a slimy vomit arc!
                    ThrownEntity thrown = minion.GetComponent<ThrownEntity>();
                    if (thrown == null) thrown = minion.AddComponent<ThrownEntity>();
                    Vector3 finalLand = landingPos;
                    thrown.Setup(finalLand, 0.58f + i * 0.08f, 2.35f, () =>
                    {
                        if (minion == null) return;
                        if (minionAi != null) minionAi.enabled = true;
                        if (col != null) col.enabled = true;
                        SpawnWarpPortalVfx(finalLand, new Color(0.65f, 0.96f, 0.30f, 0.78f), 1.05f, 0.55f);
                    });

                    spawned++;
                }
            }

            if (spawned > 0)
            {
                ShowBanner($"ALIEN OVERLORD VOMITED {spawned} ALIENS!", new Color(0.65f, 1f, 0.42f, 1f), 2.5f);
                Debug.Log($"<color=#D946EF>[RoamingAlienBoss]</color> Vomited {spawned} aliens at {_encounterTimer:F1}s!");
            }
        }

        private IEnumerator WarpBossAwayRoutine()
        {
            if (_isWarpingOut || _activeBossObj == null) yield break;
            _isWarpingOut = true;

            if (_activeBossAI != null)
            {
                _activeBossAI.IsWarpingOut = true;
            }

            Vector3 warpPos = _activeBossObj.transform.position;
            SpawnWarpPortalVfx(warpPos, new Color(0.82f, 0.25f, 1f, 0.92f), 2.6f, 1.2f);
            ShowBanner("ALIEN OVERLORD IS WARPING AWAY...", new Color(0.92f, 0.40f, 1f, 1f), 2.0f);

            // Brief shrink/spin telegraph while preserving damage taken during warp-out
            Vector3 startScale = _activeBossObj.transform.localScale;
            float elapsed = 0f;
            const float warpDuration = 1.0f;
            while (elapsed < warpDuration)
            {
                if (_activeBossObj == null || (_activeBossHealth != null && _activeBossHealth.IsDead))
                {
                    _isWarpingOut = false;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / warpDuration);
                _activeBossObj.transform.localScale = Vector3.Lerp(startScale, new Vector3(0.05f, startScale.y * 1.45f, 0.05f), t * t);
                _activeBossObj.transform.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
                yield return null;
            }

            // Save exact remaining HP (NEVER reset!)
            if (_activeBossHealth != null)
            {
                if (_activeBossHealth.IsDead)
                {
                    HandleBossDefeated();
                    yield break;
                }
                RemainingHealth = Mathf.Clamp(_activeBossHealth.CurrentHealth, 1f, BossMaxHealth);
            }

            int prevX = BossStageX;
            int prevY = BossStageY;

            DespawnActiveBossSilently(saveHealth: true);

            // Relocate to another random valid stage on the world map!
            WorldMapGenerator mapGen = WorldMapManager.Instance != null ? WorldMapManager.Instance.MapGenerator : null;
            if (mapGen != null)
            {
                PickRandomStage(mapGen,
                    avoidX: prevX,
                    avoidY: prevY,
                    centerX: WorldMapManager.Instance.CurrentPlayerX,
                    centerY: WorldMapManager.Instance.CurrentPlayerY,
                    minRadius: 3,
                    maxRadius: 16,
                    deterministicSeed: 0);
            }

            _worldRoamTimer = WorldRoamIntervalSeconds;
            ShowBanner($"ALIEN OVERLORD WARPED TO ({BossStageX}, {BossStageY})  |  HP SAVED: {RemainingHealth:N0}", new Color(0.86f, 0.42f, 1f, 1f), 4.0f);
            Debug.Log($"<color=#D946EF>[RoamingAlienBoss]</color> Warped away from ({prevX},{prevY}) -> ({BossStageX},{BossStageY}) with {RemainingHealth:N0}/{BossMaxHealth:N0} HP saved!");
        }

        private void HandleBossHealthChanged(float newHp)
        {
            if (_activeBossHealth == null) return;
            RemainingHealth = Mathf.Clamp(newHp, 0f, BossMaxHealth);
        }

        private void HandleBossDefeated()
        {
            if (IsDefeated) return;
            IsDefeated = true;
            RemainingHealth = 0f;
            BossStageX = -1;
            BossStageY = -1;
            _activeBossObj = null;
            _activeBossHealth = null;
            _activeBossAI = null;
            _isWarpingOut = false;

            OnBossLocationChanged?.Invoke(-1, -1);
            ShowBanner("ALIEN OVERLORD DEFEATED!", new Color(1f, 0.85f, 0.25f, 1f), 5.0f);
            Debug.Log("<color=#FFD700>[RoamingAlienBoss]</color> Alien Overlord has been DEFEATED!");
        }

        private void DespawnActiveBossSilently(bool saveHealth)
        {
            if (saveHealth && _activeBossHealth != null && !_activeBossHealth.IsDead)
            {
                RemainingHealth = Mathf.Clamp(_activeBossHealth.CurrentHealth, 1f, BossMaxHealth);
            }

            if (_activeBossObj != null)
            {
                if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
                {
                    GameManager.Instance.ActiveEnemies.Remove(_activeBossObj);
                }
                Destroy(_activeBossObj);
            }

            _activeBossObj = null;
            _activeBossHealth = null;
            _activeBossAI = null;
            _isWarpingOut = false;
        }

        // =========================================================================
        //  WORLD MAP STAGE SELECTION & ROAMING
        // =========================================================================

        private bool IsValidBossStage(StageData stage)
        {
            if (stage == null) return false;
            if (stage.biome == BiomeType.Waterways) return false;
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                if (stage.x == WorldMapManager.Instance.MapGenerator.spawnX &&
                    stage.y == WorldMapManager.Instance.MapGenerator.spawnY)
                {
                    return false; // Never camp inside the player's starting Safe Zone
                }
            }
            return true;
        }

        private void PickRandomStage(
            WorldMapGenerator mapGen,
            int avoidX,
            int avoidY,
            int centerX,
            int centerY,
            int minRadius,
            int maxRadius,
            int deterministicSeed)
        {
            if (mapGen == null || mapGen.gridData == null) return;

            System.Random rng = deterministicSeed != 0
                ? new System.Random(deterministicSeed)
                : new System.Random(Environment.TickCount ^ (avoidX * 73856093) ^ (avoidY * 19349663));

            List<StageData> nearbyCandidates = new List<StageData>();
            int minX = Mathf.Max(0, centerX - maxRadius);
            int maxX = Mathf.Min(WorldMapGenerator.GridSize - 1, centerX + maxRadius);
            int minY = Mathf.Max(0, centerY - maxRadius);
            int maxY = Mathf.Min(WorldMapGenerator.GridSize - 1, centerY + maxRadius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == avoidX && y == avoidY) continue;
                    int manhattan = Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY);
                    if (manhattan < minRadius) continue;

                    StageData st = mapGen.GetStage(x, y);
                    if (IsValidBossStage(st))
                    {
                        nearbyCandidates.Add(st);
                    }
                }
            }

            if (nearbyCandidates.Count > 0)
            {
                StageData chosen = nearbyCandidates[rng.Next(0, nearbyCandidates.Count)];
                BossStageX = chosen.x;
                BossStageY = chosen.y;
                OnBossLocationChanged?.Invoke(BossStageX, BossStageY);
                return;
            }

            // Fallback: pick any valid walkable stage on the 128x128 world map
            for (int attempt = 0; attempt < 400; attempt++)
            {
                int rx = rng.Next(0, WorldMapGenerator.GridSize);
                int ry = rng.Next(0, WorldMapGenerator.GridSize);
                if (rx == avoidX && ry == avoidY) continue;
                StageData st = mapGen.GetStage(rx, ry);
                if (IsValidBossStage(st))
                {
                    BossStageX = rx;
                    BossStageY = ry;
                    OnBossLocationChanged?.Invoke(BossStageX, BossStageY);
                    return;
                }
            }
        }

        private void StepRoamOneAdjacentStage()
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;
            WorldMapGenerator mapGen = WorldMapManager.Instance.MapGenerator;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            List<StageData> neighbors = new List<StageData>(4);

            for (int i = 0; i < 4; i++)
            {
                int nx = BossStageX + dx[i];
                int ny = BossStageY + dy[i];
                StageData st = mapGen.GetStage(nx, ny);
                if (IsValidBossStage(st))
                {
                    neighbors.Add(st);
                }
            }

            if (neighbors.Count == 0) return;

            StageData next = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
            BossStageX = next.x;
            BossStageY = next.y;
            OnBossLocationChanged?.Invoke(BossStageX, BossStageY);

            // If it wandered straight into the player's current stage, trigger the encounter!
            if (BossStageX == WorldMapManager.Instance.CurrentPlayerX &&
                BossStageY == WorldMapManager.Instance.CurrentPlayerY &&
                !BuildingInteriorManager.IsInsideInterior)
            {
                SpawnBossOnCurrentStage();
            }
        }

        // =========================================================================
        //  DEBUG / TEST HELPERS (Called from [F1] BossSpawnerRuntime)
        // =========================================================================

        public void ForceWarpBossToCurrentStage()
        {
            if (EmpireNetworkManager.IsOnlineClient || WorldMapManager.Instance == null) return;
            if (IsDefeated)
            {
                IsDefeated = false;
                RemainingHealth = BossMaxHealth;
            }

            DespawnActiveBossSilently(saveHealth: true);
            BossStageX = WorldMapManager.Instance.CurrentPlayerX;
            BossStageY = WorldMapManager.Instance.CurrentPlayerY;
            OnBossLocationChanged?.Invoke(BossStageX, BossStageY);

            if (!BuildingInteriorManager.IsInsideInterior)
            {
                SpawnBossOnCurrentStage();
            }
        }

        public void ResetAlienBossFull()
        {
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                InitializeForNewWorld(WorldMapManager.Instance.MapGenerator, Environment.TickCount);
            }
        }

        // =========================================================================
        //  VFX HELPER (Purple Bio-Warp Pillar)
        // =========================================================================

        private void SpawnWarpPortalVfx(Vector3 pos, Color color, float radius, float duration)
        {
            StartCoroutine(WarpPortalVfxRoutine(pos, color, radius, duration));
        }

        private static IEnumerator WarpPortalVfxRoutine(Vector3 pos, Color color, float radius, float duration)
        {
            GameObject root = new GameObject("AlienWarpVfx");
            root.transform.position = new Vector3(pos.x, 0.05f, pos.z);

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.SetParent(root.transform, false);
            Collider col = pillar.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : null;
            if (mat != null)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                Renderer r = pillar.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }

            GameObject lightObj = new GameObject("WarpLight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = Vector3.up * 1.6f;
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = radius * 3.5f;
            l.intensity = 4.5f;
            l.shadows = LightShadows.None;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                float rScale = Mathf.Lerp(radius * 0.25f, radius, ease) * (1f - t * 0.45f);
                float hScale = Mathf.Lerp(4.2f, 0.2f, t);
                pillar.transform.localScale = new Vector3(rScale, hScale, rScale);
                pillar.transform.localPosition = new Vector3(0f, hScale, 0f);

                if (mat != null)
                {
                    Color c = color;
                    c.a = color.a * (1f - t);
                    mat.color = c;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                }
                l.intensity = 4.5f * (1f - t);
                yield return null;
            }

            if (mat != null) Destroy(mat);
            Destroy(root);
        }

        // =========================================================================
        //  BOSS HEALTH BAR & STATUS BANNER (Top-Center HUD, English only)
        // =========================================================================

        private void ShowBanner(string text, Color color, float duration)
        {
            _bannerText = text;
            _bannerColor = color;
            _bannerTimer = duration;
        }

        private void EnsureGuiStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            if (_bossTitleStyle == null)
            {
                _bossTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                _bossTitleStyle.normal.textColor = new Color(0.95f, 0.72f, 1f, 1f);
            }

            if (_bossHpTextStyle == null)
            {
                _bossHpTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _bossHpTextStyle.normal.textColor = Color.white;
            }

            if (_warpBadgeStyle == null)
            {
                _warpBadgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                };
                _warpBadgeStyle.normal.textColor = new Color(1f, 0.85f, 0.35f, 1f);
            }

            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private bool TryGetDisplayedBossHealth(out float currentHp, out float maxHp, out bool showCountdown)
        {
            currentHp = 0f;
            maxHp = BossMaxHealth;
            showCountdown = false;

            if (IsBossCurrentlySpawned && _activeBossHealth != null && !_activeBossHealth.IsDead)
            {
                currentHp = _activeBossHealth.CurrentHealth;
                maxHp = _activeBossHealth.MaxHealth;
                showCountdown = true;
                return true;
            }

            // On Co-op Clients: detect replicated AlienLeader World Boss puppet from Host!
            if (EmpireNetworkManager.IsOnlineClient && GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                var enemies = GameManager.Instance.ActiveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject e = enemies[i];
                    if (e == null || !e.activeInHierarchy) continue;
                    if (e.name.IndexOf("AlienLeader", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Health h = e.GetComponent<Health>();
                    if (h != null && !h.IsDead && h.MaxHealth >= 10000f)
                    {
                        currentHp = h.CurrentHealth;
                        maxHp = h.MaxHealth;
                        showCountdown = false;
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnGUI()
        {
            bool hasBossBar = TryGetDisplayedBossHealth(out float curHp, out float maxHp, out bool showCountdown);
            bool hasBanner = _bannerTimer > 0f && !string.IsNullOrEmpty(_bannerText);
            if (!hasBossBar && !hasBanner) return;

            EnsureGuiStyles();
            Color prevColor = GUI.color;

            if (hasBossBar)
            {
                float barW = Mathf.Min(520f, Screen.width * 0.48f);
                float barH = 54f;
                float x = (Screen.width - barW) * 0.5f;
                float y = 16f;

                // Dark translucent backdrop + violet border
                GUI.color = new Color(0.06f, 0.03f, 0.10f, 0.86f);
                GUI.DrawTexture(new Rect(x - 6f, y - 4f, barW + 12f, barH + 8f), _whiteTex);
                GUI.color = new Color(0.75f, 0.25f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(x - 6f, y - 4f, barW + 12f, 2f), _whiteTex);
                GUI.DrawTexture(new Rect(x - 6f, y + barH + 2f, barW + 12f, 2f), _whiteTex);

                GUI.color = Color.white;
                GUI.Label(new Rect(x + 4f, y, barW * 0.62f, 22f), "ALIEN OVERLORD  (WORLD BOSS)", _bossTitleStyle);

                if (showCountdown)
                {
                    float remainWarp = EncounterTimeRemaining;
                    float remainSummon = NextSummonTimeRemaining;
                    string rightInfo = _isWarpingOut
                        ? "WARPING OUT..."
                        : $"SUMMON: {remainSummon:F0}s  |  WARP: {remainWarp:F1}s";
                    GUI.Label(new Rect(x + barW * 0.45f, y, barW * 0.55f - 4f, 22f), rightInfo, _warpBadgeStyle);
                }

                // HP bar track
                Rect trackRect = new Rect(x + 2f, y + 25f, barW - 4f, 22f);
                GUI.color = new Color(0.18f, 0.08f, 0.24f, 0.95f);
                GUI.DrawTexture(trackRect, _whiteTex);

                // HP bar fill
                float ratio = maxHp > 0f ? Mathf.Clamp01(curHp / maxHp) : 0f;
                Rect fillRect = new Rect(trackRect.x, trackRect.y, trackRect.width * ratio, trackRect.height);
                GUI.color = new Color(0.84f, 0.18f, 0.68f, 1f);
                GUI.DrawTexture(fillRect, _whiteTex);

                GUI.color = Color.white;
                GUI.Label(trackRect, $"{Mathf.CeilToInt(curHp):N0} / {Mathf.CeilToInt(maxHp):N0} HP  ({ratio * 100f:F1}%)", _bossHpTextStyle);
            }

            if (hasBanner)
            {
                float alpha = Mathf.Clamp01(_bannerTimer / 0.5f);
                float bw = Mathf.Min(560f, Screen.width * 0.56f);
                float bh = 32f;
                float bx = (Screen.width - bw) * 0.5f;
                float by = hasBossBar ? 82f : 24f;

                GUI.color = new Color(0.05f, 0.02f, 0.09f, 0.80f * alpha);
                GUI.DrawTexture(new Rect(bx, by, bw, bh), _whiteTex);

                _bannerStyle.normal.textColor = new Color(_bannerColor.r, _bannerColor.g, _bannerColor.b, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + 8f, by, bw - 16f, bh), _bannerText, _bannerStyle);
            }

            GUI.color = prevColor;
        }
    }
}
