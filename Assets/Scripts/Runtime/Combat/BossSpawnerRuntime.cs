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
            if (Keyboard.current == null) return;

            // F1: Toggle In-Game Debug UI
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _showDebugGui = !_showDebugGui;
            }

            // T key: Cycle Game Speed (TimeScale)
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                CycleTimeScale();
            }

            // N key: Next Day/Night Phase
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                SkipToNextPhase();
            }

            // L key: Toggle Flashlight / Torch
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                TogglePlayerLight();
            }

            // B key: Spawn Boss
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                bool isShift = Keyboard.current.shiftKey.isPressed;
                if (isShift)
                {
                    // Shift + B: Boss Rush (3 Bosses)
                    SpawnBossRush(3);
                }
                else
                {
                    // B: Single Boss in front of player
                    SpawnBossInFrontOfPlayer(bossHealthMultiplier, bossScaleMultiplier);
                }
            }

            // K key (or Ctrl + K): Kill all bosses/enemies
            if (Keyboard.current.kKey.wasPressedThisFrame)
            {
                KillAllBosses();
            }

            // Toast message countdown
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
            ShowToast($"เร่งความเร็วเกมเป็น {next:F0}x [กด T]");
        }

        private void SkipToNextPhase()
        {
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.AdvancePhase();
                ShowToast($"ข้ามช่วงเวลาเป็น {DayNightManager.Instance.CurrentPhase} (วันที่ {DayNightManager.Instance.DayCount}) [กด N]");
            }
            else
            {
                ShowToast("ไม่พบ DayNightManager ในฉาก");
            }
        }

        private void TogglePlayerLight()
        {
            if (NightDarknessController.Instance != null)
            {
                NightDarknessController.Instance.ToggleTorchLight();
                ShowToast($"ไฟฉาย: {(NightDarknessController.Instance.HasTorchLight ? "ON" : "OFF")} [กด L]");
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

            ShowToast($"BOSS RUSH! เสก Boss {count} ตัวล้อมรอบผู้เล่น!");
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

            // 2. Fallback to direct Resources.Load if pool didn't spawn
            if (enemy == null && !string.IsNullOrEmpty(prefabResourcePath))
            {
                GameObject prefab = Resources.Load<GameObject>(prefabResourcePath);
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
                ShowToast($"เสก {typeName} สำเร็จที่ ({position.x:F1}, {position.z:F1})");
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
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
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

            ShowToast($"กำจัด Boss ไปแล้ว {count} ตัว!");
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

            ShowToast($"กำจัดศัตรูทั้งหมดในฉากแล้ว ({count} ตัว)");
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
            if (GUI.Button(new Rect(toggleX, toggleY, 150f, 32f), _showDebugGui ? "[-] ปิด Debug [F1]" : "[+] Cheat Menu [F1]"))
            {
                _showDebugGui = !_showDebugGui;
            }

            if (!_showDebugGui) return;

            // 3. Spawner / Cheat Panel
            float panelWidth = 320f;
            float panelHeight = 535f;
            float panelX = Screen.width - panelWidth - 10f;
            float panelY = 50f;

            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.96f);
            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
            {
                // Tab Selection Toolbar
                string[] tabNames = new string[] { "Boss Spawner [B]", "Time Cheat [T/N]" };
                _selectedTab = GUILayout.Toolbar(_selectedTab, tabNames, GUILayout.Height(30));

                GUILayout.Space(6);

                if (_selectedTab == 0)
                {
                    DrawBossSpawnerTab();
                }
                else
                {
                    DrawTimeCheatTab();
                }
            }
            GUILayout.EndArea();
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
            GUILayout.Label($"ศัตรูในฉาก: {enemyCount} | Boss: {bossCount}", EditorStylesMini());

            GUILayout.Space(8);

            // Quick Spawns
            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
            if (GUILayout.Button("เสก Boss [กด B]", GUILayout.Height(34)))
            {
                SpawnBossInFrontOfPlayer(bossHealthMultiplier, bossScaleMultiplier);
            }

            GUI.backgroundColor = new Color(0.85f, 0.35f, 0.1f);
            if (GUILayout.Button("เสก Boss Rush (3 ตัว) [Shift+B]", GUILayout.Height(28)))
            {
                SpawnBossRush(3);
            }

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.8f);
            if (GUILayout.Button("เสก Boomer (ตัวระเบิด)"))
            {
                SpawnEnemy(EnemySpawnType.Boomer, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            if (GUILayout.Button("เสก Dog (หมาพุ่งกัด)"))
            {
                SpawnEnemy(EnemySpawnType.Dog, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            if (GUILayout.Button("เสก Runner (วิ่งไว)"))
            {
                SpawnEnemy(EnemySpawnType.Runner, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            if (GUILayout.Button("เสก Normal (ซอมบี้ทั่วไป)"))
            {
                SpawnEnemy(EnemySpawnType.Normal, GetPositionInFrontOfPlayer(defaultSpawnDistance));
            }

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(1f, 0.2f, 0.1f);
            if (GUILayout.Button("บังคับเข้า Rage Mode (ลดเลือดเหลือ 25%)", GUILayout.Height(26)))
            {
                TriggerRageOnAllBosses();
            }

            GUILayout.Space(8);

            // Modifiers
            GUI.backgroundColor = Color.white;
            GUILayout.Label($"คูณเลือด Boss: {bossHealthMultiplier:F1}x", EditorStylesBold());
            bossHealthMultiplier = GUILayout.HorizontalSlider(bossHealthMultiplier, 0.5f, 5.0f);

            GUILayout.Label($"คูณขนาดตัว Boss: {bossScaleMultiplier:F1}x", EditorStylesBold());
            bossScaleMultiplier = GUILayout.HorizontalSlider(bossScaleMultiplier, 0.8f, 2.5f);

            GUILayout.Space(8);

            // Kill Buttons
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            if (GUILayout.Button("ฆ่า Boss ทั้งหมด [กด K]", GUILayout.Height(26)))
            {
                KillAllBosses();
            }

            GUI.backgroundColor = new Color(0.6f, 0.1f, 0.1f);
            if (GUILayout.Button("ล้างศัตรูทั้งหมดในฉาก", GUILayout.Height(26)))
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

                GUILayout.Box($"PHASE: {dm.CurrentPhase.ToString().ToUpper()} | วันที่: {dm.DayCount}\nเหลืออีก: {dm.RemainingPhaseTime:F0}s / {dm.CurrentPhaseDuration:F0}s ({dm.PhaseProgress * 100f:F0}%)", badgeStyle);
            }
            else
            {
                GUILayout.Label("ไม่พบ DayNightManager ในฉาก", EditorStylesMini());
            }

            GUILayout.Space(6);

            // 2. Global Game TimeScale Multipliers
            GUI.backgroundColor = Color.white;
            GUILayout.Label($"ความเร็วทั้งเกม (TimeScale): {Time.timeScale:F1}x [กด T]", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("0x (หยุด)")) { Time.timeScale = 0f; ShowToast("หยุดเวลาชั่วคราว (Pause)"); }
                if (GUILayout.Button("1x")) { Time.timeScale = 1f; ShowToast("ความเร็วปกติ 1x"); }
                if (GUILayout.Button("2x")) { Time.timeScale = 2f; ShowToast("เร่งความเร็ว 2x"); }
                if (GUILayout.Button("5x")) { Time.timeScale = 5f; ShowToast("เร่งความเร็ว 5x"); }
                if (GUILayout.Button("10x")) { Time.timeScale = 10f; ShowToast("เร่งความเร็วสูงสุด 10x"); }
            }
            GUILayout.EndHorizontal();

            float newTimeScale = GUILayout.HorizontalSlider(Time.timeScale, 0.0f, 10f);
            if (Mathf.Abs(newTimeScale - Time.timeScale) > 0.05f)
            {
                Time.timeScale = Mathf.Round(newTimeScale * 10f) / 10f;
            }

            GUILayout.Space(6);

            // 3. Fast-Forward Time (+Seconds)
            GUILayout.Label("เร่งเวลานาฬิกา Day-Night (+วินาที)", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("+5s")) { DayNightManager.Instance?.FastForward(5f); ShowToast("+5 วินาที"); }
                if (GUILayout.Button("+15s")) { DayNightManager.Instance?.FastForward(15f); ShowToast("+15 วินาที"); }
                if (GUILayout.Button("+30s")) { DayNightManager.Instance?.FastForward(30f); ShowToast("+30 วินาที"); }
                if (GUILayout.Button("+60s")) { DayNightManager.Instance?.FastForward(60f); ShowToast("+60 วินาที"); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 4. Instant Phase Jump Buttons
            GUILayout.Label("เปลี่ยนช่วงเวลาทันที [กด N]", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
                if (GUILayout.Button("DAY (กลางวัน)"))
                {
                    DayNightManager.Instance?.SetPhase(DayNightManager.TimePhase.Day, true);
                    ShowToast("สลับเป็นช่วง กลางวัน (Day)");
                }

                GUI.backgroundColor = new Color(1f, 0.55f, 0.2f);
                if (GUILayout.Button("DUSK (เย็น)"))
                {
                    DayNightManager.Instance?.SetPhase(DayNightManager.TimePhase.Dusk, true);
                    ShowToast("สลับเป็นช่วง พลบค่ำ (Dusk)");
                }

                GUI.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
                if (GUILayout.Button("NIGHT (กลางคืน)"))
                {
                    DayNightManager.Instance?.SetPhase(DayNightManager.TimePhase.Night, true);
                    ShowToast("สลับเป็นช่วง กลางคืน (Night)");
                }
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);
            if (GUILayout.Button("ข้ามไปช่วงถัดไป (Next Phase) [กด N]", GUILayout.Height(28)))
            {
                SkipToNextPhase();
            }

            GUILayout.Space(6);

            // 5. Day Navigation Controls
            GUI.backgroundColor = Color.white;
            GUILayout.Label("ควบคุมวันที่ (Day Count)", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("◀ วันก่อนหน้า")) { DayNightManager.Instance?.AddDays(-1); ShowToast($"วันที่ {DayNightManager.Instance?.DayCount}"); }
                if (GUILayout.Button("วันถัดไป ▶")) { DayNightManager.Instance?.AddDays(1); ShowToast($"วันที่ {DayNightManager.Instance?.DayCount}"); }
                if (GUILayout.Button("รีเซ็ตเป็นวันที่ 1")) { DayNightManager.Instance?.ResetDayCount(); ShowToast("รีเซ็ตเป็นวันที่ 1"); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 6. Light & Darkness Cheats
            GUILayout.Label("ไฟฉาย & แสงสว่าง", EditorStylesBold());
            GUILayout.BeginHorizontal();
            {
                bool lightOn = NightDarknessController.Instance != null && NightDarknessController.Instance.HasTorchLight;
                GUI.backgroundColor = lightOn ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUILayout.Button($"ไฟฉาย: {(lightOn ? "เปิด [ON]" : "ปิด [OFF]")} [L]"))
                {
                    TogglePlayerLight();
                }

                bool darkDisabled = NightDarknessController.Instance != null && NightDarknessController.Instance.DisableDarknessOverlay;
                GUI.backgroundColor = darkDisabled ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.2f, 0.6f, 0.9f);
                if (GUILayout.Button($"Darkness Mask: {(darkDisabled ? "ซ่อน" : "แสดง")}"))
                {
                    if (NightDarknessController.Instance != null)
                    {
                        NightDarknessController.Instance.DisableDarknessOverlay = !NightDarknessController.Instance.DisableDarknessOverlay;
                        ShowToast($"Darkness Mask: {(NightDarknessController.Instance.DisableDarknessOverlay ? "ซ่อนความมืด (สว่าง)" : "แสดงความมืด")}");
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (NightDarknessController.Instance != null)
            {
                var ctrl = NightDarknessController.Instance;
                GUILayout.Space(4);
                GUI.backgroundColor = Color.white;
                GUILayout.Label($"ระดับความมืด (Darkness): {ctrl.maxDarkness * 100f:F0}%", EditorStylesBold());
                float newDark = GUILayout.HorizontalSlider(ctrl.maxDarkness, 0.4f, 0.98f);
                if (Mathf.Abs(newDark - ctrl.maxDarkness) > 0.01f)
                {
                    ctrl.maxDarkness = Mathf.Round(newDark * 100f) / 100f;
                }
            }
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
            ShowToast(count > 0 ? $"สั่ง Boss {count} ตัวเข้าสู่ Rage Mode สำเร็จ!" : "ไม่พบบอสในฉาก");
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

        #endregion
    }
}
