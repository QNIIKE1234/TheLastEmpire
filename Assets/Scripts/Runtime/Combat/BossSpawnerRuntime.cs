using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// Runtime Boss & Enemy Spawner Tool.
    /// Automatically initializes in Play Mode and provides hotkeys + in-game overlay to spawn bosses.
    /// </summary>
    public class BossSpawnerRuntime : MonoBehaviour
    {
        public static BossSpawnerRuntime Instance { get; private set; }

        public enum EnemySpawnType
        {
            BossLeader,
            Boomer,
            Dog,
            Runner,
            Normal
        }

        [Header("Quick Settings")]
        [SerializeField] private float defaultSpawnDistance = 3.5f;
        [SerializeField] private float bossHealthMultiplier = 1.0f;
        [SerializeField] private float bossScaleMultiplier = 1.0f;

        private bool _showDebugGui = false;
        private int _selectedTab = 0; // 0 = Boss Spawner, 1 = Time Cheat
        private string _toastMessage = "";
        private float _toastTimer = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[BossSpawner_RuntimeDebugger]");
                DontDestroyOnLoad(go);
                go.AddComponent<BossSpawnerRuntime>();
            }
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
            }
        }

        private void Update()
        {
            // เปิดเฉพาะ F1 สำหรับเปิดเมนูเสกบอส (คงปิดคีย์ลัด T, N, K ไว้ตามเดิม)
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _showDebugGui = !_showDebugGui;
            }

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f)
                {
                    _toastMessage = "";
                }
            }
        }

        private void CycleTimeScale()
        {
            float current = Time.timeScale;
            float next = current switch
            {
                < 1.2f => 2.0f,
                < 2.5f => 5.0f,
                < 5.5f => 10.0f,
                _ => 1.0f
            };
            Time.timeScale = next;
            ShowToast($"Game Speed: {next:F0}x [T]");
        }

        private void SkipToNextPhase()
        {
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.AdvancePhase();
                ShowToast($"Phase: {DayNightManager.Instance.CurrentPhase} (Day {DayNightManager.Instance.DayCount}) [N]");
            }
            else
            {
                ShowToast("DayNightManager not found in scene");
            }
        }

        private void TogglePlayerLight()
        {
            if (NightDarknessController.Instance != null)
            {
                NightDarknessController.Instance.ToggleTorchLight();
                ShowToast($"Flashlight: {(NightDarknessController.Instance.HasTorchLight ? "ON" : "OFF")} [L/F]");
            }
        }

        #region Spawning Logic

        public GameObject SpawnBossInFrontOfPlayer(float hpMultiplier = 1f, float scaleMultiplier = 1f)
        {
            Vector3 spawnPos = GetPositionInFrontOfPlayer(defaultSpawnDistance);
            return SpawnEnemy(EnemySpawnType.BossLeader, spawnPos, hpMultiplier, scaleMultiplier);
        }

        public void SpawnBossRush(int count = 3)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 center = player != null ? player.transform.position : Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                float dist = defaultSpawnDistance + Random.Range(1f, 3f);
                Vector3 spawnPos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                SpawnEnemy(EnemySpawnType.BossLeader, spawnPos, bossHealthMultiplier, bossScaleMultiplier);
            }

            ShowToast($"BOSS RUSH! Spawned {count} Bosses around player!");
        }

        public GameObject SpawnEnemy(EnemySpawnType type, Vector3 position, float hpMultiplier = 1f, float scaleMultiplier = 1f)
        {
            string poolKey = GetPoolKeyForType(type);
            string prefabResourcePath = GetPrefabPathForType(type);

            GameObject enemy = null;

            // 1. Try ObjectPoolManager first
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolKey))
            {
                enemy = ObjectPoolManager.Instance.SpawnFromPool(poolKey, position, Quaternion.identity);
            }

            // 2. Fallback to AddressableManager (with Resources fallback) if pool didn't spawn
            if (enemy == null && !string.IsNullOrEmpty(prefabResourcePath))
            {
                GameObject prefab = AddressableManager.LoadAsset<GameObject>(prefabResourcePath);
                if (prefab != null)
                {
                    enemy = Instantiate(prefab, position, Quaternion.identity);
                }
            }

            if (enemy != null)
            {
                // Align rotation towards player
                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    Vector3 lookDir = player.transform.position - position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        enemy.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }

                // Apply Scale Multiplier
                if (Mathf.Abs(scaleMultiplier - 1f) > 0.01f)
                {
                    enemy.transform.localScale = Vector3.one * scaleMultiplier;
                }

                // Apply Health Multiplier
                if (enemy.TryGetComponent<Health>(out var health) && hpMultiplier > 0.01f)
                {
                    float newMax = health.MaxHealth * hpMultiplier;
                    health.SetMaxHealth(newMax, true);
                }

                string typeName = type == EnemySpawnType.BossLeader ? "Boss (ZombieLeader)" : type.ToString();
                ShowToast($"Spawned {typeName} at ({position.x:F1}, {position.z:F1})");
                Debug.Log($"<color=#FFD700>[BossSpawner]</color> Spawned {type} at {position} (HP x{hpMultiplier}, Scale x{scaleMultiplier})");
            }
            else
            {
                Debug.LogError($"[BossSpawner] Failed to spawn {type}! Check poolKey '{poolKey}' or Resources path '{prefabResourcePath}'");
            }

            return enemy;
        }

        private Vector3 GetPositionInFrontOfPlayer(float distance)
        {
            PlayerController player = TheLastEmpire.Network.PlayerRegistry.LocalPlayer ?? Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                return player.transform.position + forward.normalized * distance;
            }

            return new Vector3(0f, 0f, distance);
        }

        private string GetPoolKeyForType(EnemySpawnType type)
        {
            return type switch
            {
                EnemySpawnType.BossLeader => "ZombieLeader",
                EnemySpawnType.Boomer => "ZombieBoomer",
                EnemySpawnType.Dog => "ZombieDog",
                EnemySpawnType.Runner => "ZombieRunner",
                EnemySpawnType.Normal => "ZombieNormal",
                _ => "ZombieLeader"
            };
        }

        private string GetPrefabPathForType(EnemySpawnType type)
        {
            return type switch
            {
                EnemySpawnType.BossLeader => "Prefabs/Character/ZombieLeader",
                EnemySpawnType.Boomer => "Prefabs/Character/ZombieBoomer",
                EnemySpawnType.Dog => "Prefabs/Character/ZombieDog",
                EnemySpawnType.Runner => "Prefabs/Character/ZombieRunner",
                EnemySpawnType.Normal => "Prefabs/Character/ZombieNormal",
                _ => "Prefabs/Character/ZombieLeader"
            };
        }

        #endregion

        #region Clear & Kill Utilities

        public void KillAllBosses()
        {
            ZombieAI[] allZombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var zombie in allZombies)
            {
                if (zombie != null && zombie.gameObject.activeInHierarchy && zombie.IsBossUnit)
                {
                    if (zombie.TryGetComponent<Health>(out var health) && !health.IsDead)
                    {
                        health.TakeDamage(999999f);
                        count++;
                    }
                }
            }

            ShowToast($"Killed {count} Bosses!");
        }

        public void KillAllEnemies()
        {
            BaseEnemyAI[] allEnemies = Object.FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var enemy in allEnemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                {
                    if (enemy.TryGetComponent<Health>(out var health) && !health.IsDead)
                    {
                        health.TakeDamage(999999f);
                        count++;
                    }
                }
            }

            ShowToast($"Cleared all enemies in stage ({count})");
        }

        #endregion

        #region In-Game GUI Overlay

        private void ShowToast(string message)
        {
            _toastMessage = message;
            _toastTimer = 2.5f;
        }

        private void OnGUI()
        {
            // 1. Toast Notification Banner
            if (!string.IsNullOrEmpty(_toastMessage) && _toastTimer > 0f)
            {
                GUIStyle toastStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                toastStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

                float width = 460f;
                float height = 45f;
                float x = (Screen.width - width) * 0.5f;
                float y = 25f;

                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
                GUI.Box(new Rect(x, y, width, height), _toastMessage, toastStyle);
            }

            // 2. Small Toggle Button in Top-Right
            float toggleX = Screen.width - 160f;
            float toggleY = 10f;
            GUI.backgroundColor = _showDebugGui ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(toggleX, toggleY, 150f, 32f), _showDebugGui ? "[-] Close Debug [F1]" : "[+] Cheat Menu [F1]"))
            {
                _showDebugGui = !_showDebugGui;
            }

            if (!_showDebugGui) return;

            // 3. Spawner / Cheat Panel
            float panelWidth = 350f;
            float panelHeight = 615f;
            float panelX = Screen.width - panelWidth - 10f;
            float panelY = 50f;

            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.96f);
            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
            {
                // Tab Selection Toolbar
                string[] tabNames = new string[] { "Boss Spawner", "Time [T/N]", "Free / Shop", "Weather / Events" };
                _selectedTab = GUILayout.Toolbar(_selectedTab, tabNames, GUILayout.Height(30));

                GUILayout.Space(6);

                if (_selectedTab == 0)
                {
                    DrawBossSpawnerTab();
                }
                else if (_selectedTab == 1)
                {
                    DrawTimeCheatTab();
                }
                else if (_selectedTab == 2)
                {
                    DrawCraftCheatTab();
                }
                else
                {
                    DrawEventsCheatTab();
                }
            }
            GUILayout.EndArea();
        }

        private void DrawEventsCheatTab()
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.65f, 0.15f);
            GUILayout.Label("STAGE EVENT CHEATS", titleStyle);

            bool isQueued = RandomStageEventManager.Instance != null && RandomStageEventManager.Instance.IsFleeingEventQueued;
            GUILayout.Label($"Next Stage Queue: {(isQueued ? "<color=#00FF66>Maya Queued</color>" : "None")}", EditorStylesBold());

            GUILayout.Space(8);

            GUI.backgroundColor = new Color(1f, 0.45f, 0.1f);
            if (GUILayout.Button("Spawn Fleeing Maya Now [Shift+F9]", GUILayout.Height(38)))
            {
                if (RandomStageEventManager.Instance != null)
                {
                    RandomStageEventManager.Instance.ForceTriggerFleeingSurvivor();
                    ShowToast("Spawned fleeing Maya (Scout)!");
                }
            }

            GUILayout.Space(6);

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.9f);
            if (GUILayout.Button("Queue Next Stage: Fleeing Maya [F9]", GUILayout.Height(34)))
            {
                if (RandomStageEventManager.Instance != null)
                {
                    RandomStageEventManager.Instance.QueueNextStageFleeingSurvivor();
                    ShowToast("Queued fleeing Maya for next stage!");
                }
            }

            GUILayout.Space(10);
            GUI.backgroundColor = Color.white;
            GUILayout.Label("Notes:", EditorStylesBold());
            GUILayout.Label("• Instant Spawn: Maya spawns at opposite gate and sprints through at 5.0 m/s.", EditorStylesMini());
            GUILayout.Label("• Maya uses trigger colliders to pass smoothly through obstacles.", EditorStylesMini());
            GUILayout.Label("• Stage transitions clean up runners automatically.", EditorStylesMini());

            GUILayout.Space(10);
            GUIStyle weatherTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            weatherTitle.normal.textColor = new Color(0.42f, 0.85f, 1f);
            GUILayout.Label("SEASON & WEATHER (CO-OP SYNC)", weatherTitle);

            var wm = WeatherManager.Instance;
            if (wm != null)
            {
                string weatherStateStr = wm.CurrentWeather == WeatherType.Clear
                    ? $"CLEAR (Next {wm.RainChance * 100f:F0}% roll: {wm.NextRollRemainingSeconds:F0}s)"
                    : $"{wm.CurrentWeather.ToString().ToUpper()} ({WeatherManager.FormatWeatherDuration(wm.WeatherRemainingSeconds)} left)";
                GUILayout.Label($"Season: {wm.CurrentSeason.ToString().ToUpper()} (Day {wm.DayInCurrentSeason}/{wm.DaysPerSeason})", EditorStylesBold());
                GUILayout.Label($"Weather: {weatherStateStr}", EditorStylesBold());
                GUILayout.Label($"Ground Wet: {wm.GroundWetness * 100f:F0}% | Snow Cover: {wm.GroundSnowAmount * 100f:F0}%", EditorStylesMini());

                GUILayout.Space(4);
                GUI.backgroundColor = Color.white;
                GUILayout.Label("Set Season (Co-op Synced):", EditorStylesBold());
                GUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = wm.CurrentSeason == SeasonType.Autumn ? new Color(0.95f, 0.62f, 0.20f) : new Color(0.48f, 0.36f, 0.22f);
                    if (GUILayout.Button("AUTUMN", GUILayout.Height(26))) { wm.SetSeasonOverride(SeasonType.Autumn); ShowToast("Season -> AUTUMN (Synced)"); }

                    GUI.backgroundColor = wm.CurrentSeason == SeasonType.Rainy ? new Color(0.25f, 0.65f, 1f) : new Color(0.22f, 0.38f, 0.55f);
                    if (GUILayout.Button("RAINY", GUILayout.Height(26))) { wm.SetSeasonOverride(SeasonType.Rainy); ShowToast("Season -> RAINY (Synced)"); }

                    GUI.backgroundColor = wm.CurrentSeason == SeasonType.Winter ? new Color(0.75f, 0.92f, 1f) : new Color(0.38f, 0.48f, 0.58f);
                    if (GUILayout.Button("WINTER", GUILayout.Height(26))) { wm.SetSeasonOverride(SeasonType.Winter); ShowToast("Season -> WINTER (Synced)"); }

                    GUI.backgroundColor = wm.CurrentSeason == SeasonType.Summer ? new Color(1f, 0.82f, 0.22f) : new Color(0.55f, 0.46f, 0.20f);
                    if (GUILayout.Button("SUMMER", GUILayout.Height(26))) { wm.SetSeasonOverride(SeasonType.Summer); ShowToast("Season -> SUMMER (Synced)"); }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                GUI.backgroundColor = Color.white;
                GUILayout.Label("Trigger Weather (1-6 min, Co-op Synced):", EditorStylesBold());
                GUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = new Color(0.30f, 0.72f, 0.36f);
                    if (GUILayout.Button("CLEAR", GUILayout.Height(30))) { wm.SetWeather(WeatherType.Clear); ShowToast("Weather -> CLEAR (Synced)"); }

                    GUI.backgroundColor = new Color(0.24f, 0.60f, 1f);
                    if (GUILayout.Button("RAIN", GUILayout.Height(30))) { wm.SetWeather(WeatherType.Rain); ShowToast("Weather -> RAIN 1-6m (Synced)"); }

                    GUI.backgroundColor = new Color(0.38f, 0.42f, 0.95f);
                    if (GUILayout.Button("STORM", GUILayout.Height(30))) { wm.SetWeather(WeatherType.Thunderstorm); ShowToast("Weather -> THUNDERSTORM 1-6m (Synced)"); }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = new Color(0.70f, 0.88f, 1f);
                    if (GUILayout.Button("SNOW", GUILayout.Height(30))) { wm.SetWeather(WeatherType.Snow); ShowToast("Weather -> SNOW 1-6m (Synced)"); }

                    GUI.backgroundColor = new Color(0.50f, 0.78f, 0.96f);
                    if (GUILayout.Button("BLIZZARD", GUILayout.Height(30))) { wm.SetWeather(WeatherType.Blizzard); ShowToast("Weather -> BLIZZARD 1-6m (Synced)"); }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = new Color(0.95f, 0.82f, 0.22f);
                    if (GUILayout.Button("Strike Lightning", GUILayout.Height(26)))
                    {
                        wm.TriggerDebugLightningStrikeNow();
                        ShowToast("Lightning Strike incoming (Synced)!");
                    }

                    GUI.backgroundColor = new Color(0.25f, 0.78f, 0.72f);
                    if (GUILayout.Button("10% Roll Now", GUILayout.Height(26)))
                    {
                        wm.ForceRollNow();
                        ShowToast(wm.CurrentWeather != WeatherType.Clear ? $"Roll HIT -> {wm.CurrentWeather}!" : "Roll missed -> Clear.");
                    }
                }
                GUILayout.EndHorizontal();

                GUI.backgroundColor = Color.white;
                GUILayout.Label($"Auto-roll: {wm.RainChance * 100f:F0}% every {wm.RollIntervalSeconds:F0}s -> lasts {wm.MinWeatherDurationSeconds / 60f:F0}-{wm.MaxWeatherDurationSeconds / 60f:F0} min (5d/season).", EditorStylesMini());
                GUILayout.Label("Outdoors: Rain/Storm/Blizzard = x2 Drain; Storm & Blizzard lock [Shift].", EditorStylesMini());
                GUILayout.Label("Blizzard outdoors: -3 HP/s & -18% Speed (safe inside house or by Campfire).", EditorStylesMini());
            }
            else
            {
                GUILayout.Label("WeatherManager not initialized.", EditorStylesMini());
            }
        }

        private void DrawBossSpawnerTab()
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            GUILayout.Label("BOSS SPAWNER TOOL", titleStyle);

            // Stats Info
            int bossCount = CountActiveBosses();
            int enemyCount = CountActiveEnemies();
            GUILayout.Label($"Enemies: {enemyCount} | Bosses: {bossCount}", EditorStylesMini());

            GUILayout.Space(8);

            // Quick Spawns
            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
            if (GUILayout.Button("Spawn Boss (Single)", GUILayout.Height(34)))
            {
                SpawnBossInFrontOfPlayer(bossHealthMultiplier, bossScaleMultiplier);
            }

            GUI.backgroundColor = new Color(0.85f, 0.35f, 0.1f);
            if (GUILayout.Button("Spawn Boss Rush (x3)", GUILayout.Height(28)))
            {
                SpawnBossRush(3);
            }

            // Roaming Alien World Boss controls
            GUILayout.Space(6);
            GUI.backgroundColor = Color.white;
            if (RoamingAlienBossManager.Instance != null)
            {
                var ab = RoamingAlienBossManager.Instance;
                string abStatus = ab.IsDefeated
                    ? "Alien World Boss: DEFEATED"
                    : $"Alien Boss @ ({ab.BossStageX},{ab.BossStageY}) | HP: {ab.RemainingHealth:N0}/50,000";
                GUILayout.Label(abStatus, EditorStylesMini());

                GUI.backgroundColor = new Color(0.72f, 0.25f, 0.95f);
                if (GUILayout.Button("Warp Alien World Boss Here", GUILayout.Height(28)))
                {
                    ab.ForceWarpBossToCurrentStage();
                    ShowToast($"Alien World Boss warped to current stage ({ab.RemainingHealth:N0} HP)!");
                }

                GUI.backgroundColor = new Color(0.48f, 0.22f, 0.72f);
                if (GUILayout.Button("Reset Alien Boss (50k HP + Re-roll Map)", GUILayout.Height(24)))
                {
                    ab.ResetAlienBossFull();
                    ShowToast($"Alien Boss reset @ ({ab.BossStageX},{ab.BossStageY})!");
                }
            }

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.8f);
            if (GUILayout.Button("Spawn Boomer"))
            {
                SpawnEnemy(EnemySpawnType.Boomer, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            if (GUILayout.Button("Spawn Dog"))
            {
                SpawnEnemy(EnemySpawnType.Dog, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            if (GUILayout.Button("Spawn Runner"))
            {
                SpawnEnemy(EnemySpawnType.Runner, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            if (GUILayout.Button("Spawn Normal Zombie"))
            {
                SpawnEnemy(EnemySpawnType.Normal, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(1f, 0.2f, 0.1f);
            if (GUILayout.Button("Force Boss Rage Mode (25% HP)", GUILayout.Height(26)))
            {
                TriggerRageOnAllBosses();
            }

            GUILayout.Space(8);

            // Modifiers
            GUI.backgroundColor = Color.white;
            GUILayout.Label($"Boss HP Multiplier: {bossHealthMultiplier:F1}x", EditorStylesBold());
            bossHealthMultiplier = GUILayout.HorizontalSlider(bossHealthMultiplier, 0.5f, 5.0f);

            GUILayout.Label($"Boss Scale Multiplier: {bossScaleMultiplier:F1}x", EditorStylesBold());
            bossScaleMultiplier = GUILayout.HorizontalSlider(bossScaleMultiplier, 0.8f, 2.5f);

            GUILayout.Space(8);

            // Kill Buttons
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            if (GUILayout.Button("Kill All Bosses [K]", GUILayout.Height(26)))
            {
                KillAllBosses();
            }

            GUI.backgroundColor = new Color(0.6f, 0.1f, 0.1f);
            if (GUILayout.Button("Kill All Enemies in Stage", GUILayout.Height(26)))
            {
                KillAllEnemies();
            }
        }

        private void DrawTimeCheatTab()
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.4f, 0.85f, 1f);
            GUILayout.Label("TIME & DAY/NIGHT CHEAT", titleStyle);

            // 1. Current Phase Status Display
            if (DayNightManager.Instance != null)
            {
                var dm = DayNightManager.Instance;
                Color phaseColor = dm.CurrentPhase switch
                {
                    DayNightManager.TimePhase.Day => new Color(1f, 0.85f, 0.2f),
                    DayNightManager.TimePhase.Dusk => new Color(1f, 0.55f, 0.2f),
                    DayNightManager.TimePhase.Night => new Color(0.4f, 0.8f, 1f),
                    _ => Color.white
                };

                GUIStyle badgeStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                badgeStyle.normal.textColor = phaseColor;

                GUILayout.Box($"PHASE: {dm.CurrentPhase.ToString().ToUpper()} | Day: {dm.DayCount}\nRemaining: {dm.RemainingPhaseTime:F0}s / {dm.CurrentPhaseDuration:F0}s ({dm.PhaseProgress * 100f:F0}%)", badgeStyle);
            }
            else
            {
                GUILayout.Label("DayNightManager not found", EditorStylesMini());
            }

            GUILayout.Space(6);

            // 2. Global Game TimeScale Multipliers
            GUI.backgroundColor = Color.white;
            GUILayout.Label($"Game Speed (TimeScale): {Time.timeScale:F1}x [T]", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("0x (Pause)")) { Time.timeScale = 0f; ShowToast("Paused (0x)"); }
                if (GUILayout.Button("1x")) { Time.timeScale = 1f; ShowToast("Normal Speed (1x)"); }
                if (GUILayout.Button("2x")) { Time.timeScale = 2f; ShowToast("Speed 2x"); }
                if (GUILayout.Button("5x")) { Time.timeScale = 5f; ShowToast("Speed 5x"); }
                if (GUILayout.Button("10x")) { Time.timeScale = 10f; ShowToast("Max Speed 10x"); }
            }
            GUILayout.EndHorizontal();

            float newTimeScale = GUILayout.HorizontalSlider(Time.timeScale, 0.0f, 10f);
            if (Mathf.Abs(newTimeScale - Time.timeScale) > 0.05f)
            {
                Time.timeScale = Mathf.Round(newTimeScale * 10f) / 10f;
            }

            GUILayout.Space(6);

            // 3. Fast-Forward Time (+Seconds)
            GUILayout.Label("Fast-Forward Day/Night Clock", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("+5s")) { DayNightManager.Instance?.FastForward(5f); ShowToast("+5s"); }
                if (GUILayout.Button("+15s")) { DayNightManager.Instance?.FastForward(15f); ShowToast("+15s"); }
                if (GUILayout.Button("+30s")) { DayNightManager.Instance?.FastForward(30f); ShowToast("+30s"); }
                if (GUILayout.Button("+60s")) { DayNightManager.Instance?.FastForward(60f); ShowToast("+60s"); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 4. Instant Phase Jump Buttons
            GUILayout.Label("Set Phase Immediately [N]", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
                if (GUILayout.Button("DAY", GUILayout.Height(30)))
                {
                    DayNightManager.Instance?.SetPhase(DayNightManager.TimePhase.Day, true);
                    ShowToast("Switched to DAY");
                }

                GUI.backgroundColor = new Color(1f, 0.55f, 0.2f);
                if (GUILayout.Button("DUSK", GUILayout.Height(30)))
                {
                    DayNightManager.Instance?.SetPhase(DayNightManager.TimePhase.Dusk, true);
                    ShowToast("Switched to DUSK");
                }

                GUI.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
                if (GUILayout.Button("NIGHT", GUILayout.Height(30)))
                {
                    if (NightDarknessController.Instance != null)
                    {
                        NightDarknessController.Instance.DisableDarknessOverlay = false;
                    }
                    DayNightManager.Instance?.SetPhase(DayNightManager.TimePhase.Night, true);
                    ShowToast("Switched to NIGHT");
                }
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);
            if (GUILayout.Button("Next Phase [N]", GUILayout.Height(28)))
            {
                SkipToNextPhase();
            }

            GUILayout.Space(6);

            // 5. Day Navigation Controls
            GUI.backgroundColor = Color.white;
            GUILayout.Label("Day Count Controls", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("< Prev Day")) { DayNightManager.Instance?.AddDays(-1); ShowToast($"Day {DayNightManager.Instance?.DayCount}"); }
                if (GUILayout.Button("Next Day >")) { DayNightManager.Instance?.AddDays(1); ShowToast($"Day {DayNightManager.Instance?.DayCount}"); }
                if (GUILayout.Button("Reset Day 1")) { DayNightManager.Instance?.ResetDayCount(); ShowToast("Reset to Day 1"); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 6. Light & Darkness Cheats
            GUILayout.Label("Flashlight & Darkness", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                bool lightOn = NightDarknessController.Instance != null && NightDarknessController.Instance.HasTorchLight;
                GUI.backgroundColor = lightOn ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUILayout.Button($"Flashlight: {(lightOn ? "ON" : "OFF")} [L/F]"))
                {
                    TogglePlayerLight();
                }

                bool darkDisabled = NightDarknessController.Instance != null && NightDarknessController.Instance.DisableDarknessOverlay;
                GUI.backgroundColor = darkDisabled ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.2f, 0.8f, 0.3f);
                if (GUILayout.Button($"Darkness Mask: {(darkDisabled ? "OFF" : "ON")}"))
                {
                    if (NightDarknessController.Instance != null)
                    {
                        NightDarknessController.Instance.DisableDarknessOverlay = !NightDarknessController.Instance.DisableDarknessOverlay;
                        ShowToast($"Darkness Mask: {(NightDarknessController.Instance.DisableDarknessOverlay ? "OFF" : "ON")}");
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (NightDarknessController.Instance != null)
            {
                var ctrl = NightDarknessController.Instance;
                GUILayout.Space(4);
                GUI.backgroundColor = Color.white;
                GUILayout.Label($"Darkness Level: {ctrl.maxDarkness * 100f:F0}%", EditorStylesBold());
                float newDark = GUILayout.HorizontalSlider(ctrl.maxDarkness, 0.4f, 0.98f);
                if (Mathf.Abs(newDark - ctrl.maxDarkness) > 0.01f)
                {
                    ctrl.maxDarkness = Mathf.Round(newDark * 100f) / 100f;
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Atmosphere (Fog & God Rays)", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                bool fogOn = RenderSettings.fog;
                GUI.backgroundColor = fogOn ? new Color(0.2f, 0.8f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUILayout.Button($"Fog: {(fogOn ? "ON" : "OFF")}"))
                {
                    RenderSettings.fog = !RenderSettings.fog;
                    ShowToast($"Fog: {(RenderSettings.fog ? "ON" : "OFF")}");
                }

                GUI.backgroundColor = new Color(0.9f, 0.7f, 0.2f);
                if (GUILayout.Button("Reset Atmosphere"))
                {
                    if (AtmosphericLightingController.Instance != null)
                    {
                        AtmosphericLightingController.Instance.ApplyAtmosphere();
                        AtmosphericLightingController.Instance.DestroyLegacyGodRayContainers();
                        AtmosphericLightingController.Instance.SetupDustMotes();
                    }
                    ShowToast("Reset atmosphere lighting & dust motes");
                }
            }
            GUILayout.EndHorizontal();
        }

        public void TriggerRageOnAllBosses()
        {
            var list = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var z in list)
            {
                if (z != null && z.gameObject.activeInHierarchy && z.IsBossUnit)
                {
                    z.ForceTriggerRageMode();
                    count++;
                }
            }
            ShowToast(count > 0 ? $"Triggered Rage Mode on {count} Bosses!" : "No Boss found in stage");
        }

        private int CountActiveBosses()
        {
            int count = 0;
            var list = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            foreach (var z in list)
            {
                if (z != null && z.gameObject.activeInHierarchy && z.IsBossUnit) count++;
            }
            return count;
        }

        private int CountActiveEnemies()
        {
            int count = 0;
            var list = Object.FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
            foreach (var e in list)
            {
                if (e != null && e.gameObject.activeInHierarchy) count++;
            }
            return count;
        }

        private GUIStyle EditorStylesBold()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            s.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            return s;
        }

        private GUIStyle EditorStylesMini()
        {
            GUIStyle s = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            s.normal.textColor = Color.gray;
            return s;
        }

        private void ToggleFreeCraftMode()
        {
            if (CraftController.Instance != null)
            {
                CraftController.Instance.ToggleFreeCraft();
            }
            else
            {
                CraftingDatabase.IsFreeCraft = !CraftingDatabase.IsFreeCraft;
            }
            bool isFree = (CraftController.Instance != null && CraftController.Instance.FreeCraftMode) || CraftingDatabase.IsFreeCraft;
            ShowToast(isFree ? "Free Craft: ON (No materials required)" : "Free Craft: OFF (Normal materials)");
        }

        private void ToggleFreeShopMode()
        {
            bool next = !ShopUI.GlobalFreeShop;
            ShopUI.SetGlobalFreeShop(next);
            ShowToast(next
                ? "Free Shop ($0): ON"
                : "Free Shop: OFF (Normal prices)");
        }

        private void ToggleUnlockLegendaryArtifactCraft()
        {
            bool next = !CraftingDatabase.UnlockLegendaryArtifactCraft;
            CraftingDatabase.UnlockLegendaryArtifactCraft = next;
            if (CraftController.Instance != null)
            {
                CraftController.Instance.RefreshRecipesFromDatabase();
            }
            ShowToast(next
                ? "Unlock Legend Artifact Craft: ON"
                : "Unlock Legend Artifact Craft: OFF");
        }

        private void DrawCraftCheatTab()
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            GUILayout.Label("CRAFT & SHOP CHEATS", titleStyle);
            GUILayout.Space(8);

            // 1. Free Craft Toggle
            bool isFreeCraft = (CraftController.Instance != null && CraftController.Instance.FreeCraftMode) || CraftingDatabase.IsFreeCraft;
            GUI.backgroundColor = isFreeCraft ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.85f, 0.3f, 0.3f);
            string craftBtnText = isFreeCraft ? "Free Craft: [ON]" : "Free Craft: [OFF]";
            if (GUILayout.Button(craftBtnText, GUILayout.Height(40)))
            {
                ToggleFreeCraftMode();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Label(isFreeCraft
                ? "• Craft any recipe immediately without materials"
                : "• Normal mode: requires recipe materials", EditorStylesMini());

            GUILayout.Space(10);

            // 2. Free Shop (Is Free) Toggle
            bool isFreeShop = ShopUI.GlobalFreeShop;
            GUI.backgroundColor = isFreeShop ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.85f, 0.3f, 0.3f);
            string shopBtnText = isFreeShop ? "Free Shop ($0): [ON]" : "Free Shop ($0): [OFF]";
            if (GUILayout.Button(shopBtnText, GUILayout.Height(40)))
            {
                ToggleFreeShopMode();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Label(isFreeShop
                ? "• Buy any item from NPC shops for $0"
                : "• Normal mode: standard NPC prices", EditorStylesMini());

            GUILayout.Space(10);

            // 3. Unlock Legendary Artifact Craft Toggle
            bool isUnlockLegend = CraftingDatabase.UnlockLegendaryArtifactCraft;
            GUI.backgroundColor = isUnlockLegend ? new Color(1f, 0.72f, 0.1f) : new Color(0.85f, 0.3f, 0.3f);
            string legendBtnText = isUnlockLegend
                ? "Unlock Legend Artifact Craft: [ON]"
                : "Unlock Legend Artifact Craft: [OFF]";
            if (GUILayout.Button(legendBtnText, GUILayout.Height(40)))
            {
                ToggleUnlockLegendaryArtifactCraft();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Label(isUnlockLegend
                ? "• Shows all Legendary Artifacts at top of Artifact tab"
                : "• Normal mode: Common & Rare only (Legend drops from Boss)", EditorStylesMini());

            GUILayout.Space(14);
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            if (GUILayout.Button("Toggle Craft Menu [C]", GUILayout.Height(32)))
            {
                if (CraftController.Instance != null)
                {
                    CraftController.Instance.ToggleCraftMenu();
                }
            }
        }

        #endregion
    }
}
