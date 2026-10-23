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
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f)
                {
                    _toastMessage = "";
                }
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
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                toastStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

                float width = 450f;
                float height = 45f;
                float x = (Screen.width - width) * 0.5f;
                float y = 25f;

                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
                GUI.Box(new Rect(x, y, width, height), _toastMessage, toastStyle);
            }

            // 2. Small Toggle Button in Top-Right
            float toggleX = Screen.width - 150f;
            float toggleY = 10f;
            GUI.backgroundColor = _showDebugGui ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(toggleX, toggleY, 140f, 32f), _showDebugGui ? "[-] ปิด Boss Tool [F1]" : "[+] Boss Tool [F1]"))
            {
                _showDebugGui = !_showDebugGui;
            }

            if (!_showDebugGui) return;

            // 3. Spawner Panel
            float panelWidth = 280f;
            float panelHeight = 440f;
            float panelX = Screen.width - panelWidth - 10f;
            float panelY = 50f;

            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
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
                if (GUILayout.Button("บังคับเข้า Rage Mode (ลดเลือดเหลือ 25%)", GUILayout.Height(28)))
                {
                    TriggerRageOnAllBosses();
                }

                GUILayout.Space(8);

                // Modifiers
                GUILayout.Label($"คูณเลือด Boss: {bossHealthMultiplier:F1}x");
                bossHealthMultiplier = GUILayout.HorizontalSlider(bossHealthMultiplier, 0.5f, 5.0f);

                GUILayout.Label($"คูณขนาดตัว Boss: {bossScaleMultiplier:F1}x");
                bossScaleMultiplier = GUILayout.HorizontalSlider(bossScaleMultiplier, 0.8f, 2.5f);

                GUILayout.Space(10);

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
            GUILayout.EndArea();
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
