using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public class BossSpawnerEditorWindow : EditorWindow
    {
        private enum SpawnLocationMode
        {
            InFrontOfPlayer,
            RandomNearPlayer,
            StageCenter,
            SceneViewCenter
        }

        private BossSpawnerRuntime.EnemySpawnType _selectedType = BossSpawnerRuntime.EnemySpawnType.BossLeader;
        private SpawnLocationMode _locationMode = SpawnLocationMode.InFrontOfPlayer;

        private float _spawnDistance = 3.5f;
        private float _bossHpMultiplier = 1.0f;
        private float _bossScaleMultiplier = 1.0f;
        private int _spawnCount = 1;

        private Vector2 _scrollPos;

        [MenuItem("TheLastEmpire/Boss Spawner Tool %&b", false, 50)]
        [MenuItem("Tools/Boss Spawner Tool", false, 50)]
        public static void OpenWindow()
        {
            BossSpawnerEditorWindow window = GetWindow<BossSpawnerEditorWindow>("Boss Spawner");
            window.minSize = new Vector2(340, 520);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            DrawStatusCard();
            DrawSpawnOptions();
            DrawActionButtons();
            DrawShortcutReference();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.82f, 0.2f);

            EditorGUILayout.LabelField("BOSS SPAWNER TOOL", titleStyle);
            EditorGUILayout.LabelField("เครื่องมือเสก Boss และมอนสเตอร์ สำหรับทดสอบระบบ", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(10);
        }

        private void DrawStatusCard()
        {
            EditorGUILayout.BeginVertical("helpbox");
            {
                bool isPlaying = EditorApplication.isPlaying;
                Color statusColor = isPlaying ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.7f, 0.1f);

                GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel);
                statusStyle.normal.textColor = statusColor;

                EditorGUILayout.LabelField("สถานะ:", isPlaying ? "[PLAY MODE - เล่นอยู่]" : "[EDIT MODE - กำลังแก้ไข]", statusStyle);

                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    EditorGUILayout.LabelField("ผู้เล่น:", $"พบตัวละครที่ {player.transform.position.ToString("F1")}", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("ผู้เล่น:", "ไม่พบ Player ในฉาก", EditorStyles.miniLabel);
                }

                if (isPlaying)
                {
                    int bossCount = CountActiveBosses();
                    int enemyCount = CountActiveEnemies();
                    EditorGUILayout.LabelField("ศัตรูในฉาก:", $"Boss: {bossCount} ตัว | รวมทั้งหมด: {enemyCount} ตัว", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawSpawnOptions()
        {
            EditorGUILayout.LabelField("ตัวเลือกการเสก", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            {
                _selectedType = (BossSpawnerRuntime.EnemySpawnType)EditorGUILayout.EnumPopup("ประเภทมอน:", _selectedType);
                _locationMode = (SpawnLocationMode)EditorGUILayout.EnumPopup("จุดที่เกิด:", _locationMode);

                if (_locationMode == SpawnLocationMode.InFrontOfPlayer || _locationMode == SpawnLocationMode.RandomNearPlayer)
                {
                    _spawnDistance = EditorGUILayout.Slider("ระยะห่างจากผู้เล่น (m):", _spawnDistance, 1.5f, 15f);
                }

                _spawnCount = EditorGUILayout.IntSlider("จำนวนที่เสก:", _spawnCount, 1, 5);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("ตัวคูณสถิติ (Stats Modifiers):", EditorStyles.miniBoldLabel);
                _bossHpMultiplier = EditorGUILayout.Slider("คูณเลือด (HP):", _bossHpMultiplier, 0.5f, 10f);
                _bossScaleMultiplier = EditorGUILayout.Slider("คูณขนาด (Scale):", _bossScaleMultiplier, 0.8f, 3.0f);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("คำสั่งเสก (Actions)", EditorStyles.boldLabel);

            // Primary Spawn Button
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.3f);
            string spawnLabel = _spawnCount == 1 ? $"เสก {_selectedType} ทันที!" : $"เสก {_selectedType} ({_spawnCount} ตัว)!";
            if (GUILayout.Button(spawnLabel, GUILayout.Height(38)))
            {
                ExecuteSpawn();
            }

            GUI.backgroundColor = new Color(0.95f, 0.55f, 0.1f);
            if (GUILayout.Button("เสก Boss Rush (3 ตัวล้อมกรอบ)", GUILayout.Height(28)))
            {
                if (EditorApplication.isPlaying && BossSpawnerRuntime.Instance != null)
                {
                    BossSpawnerRuntime.Instance.SpawnBossRush(3);
                }
                else
                {
                    _selectedType = BossSpawnerRuntime.EnemySpawnType.BossLeader;
                    _spawnCount = 3;
                    ExecuteSpawn();
                }
            }

            GUI.backgroundColor = new Color(1f, 0.25f, 0.15f);
            if (GUILayout.Button("บังคับ Boss เข้า Rage Mode (ลดเลือดเหลือ 25%)", GUILayout.Height(28)))
            {
                if (EditorApplication.isPlaying && BossSpawnerRuntime.Instance != null)
                {
                    BossSpawnerRuntime.Instance.TriggerRageOnAllBosses();
                }
                else
                {
                    var bosses = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
                    int count = 0;
                    foreach (var b in bosses)
                    {
                        if (b != null && b.IsBossUnit)
                        {
                            b.ForceTriggerRageMode();
                            count++;
                        }
                    }
                    Debug.Log($"[BossSpawnerEditor] Triggered Rage on {count} bosses.");
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("คำสั่งเคลียร์ฉาก", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                if (GUILayout.Button("ฆ่า Boss ทั้งหมด", GUILayout.Height(28)))
                {
                    if (EditorApplication.isPlaying && BossSpawnerRuntime.Instance != null)
                    {
                        BossSpawnerRuntime.Instance.KillAllBosses();
                    }
                    else
                    {
                        KillAllBossesInEditMode();
                    }
                }

                GUI.backgroundColor = new Color(0.65f, 0.15f, 0.15f);
                if (GUILayout.Button("ล้างศัตรูทั้งหมด", GUILayout.Height(28)))
                {
                    if (EditorApplication.isPlaying && BossSpawnerRuntime.Instance != null)
                    {
                        BossSpawnerRuntime.Instance.KillAllEnemies();
                    }
                    else
                    {
                        KillAllEnemiesInEditMode();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(10);
        }

        private void DrawShortcutReference()
        {
            EditorGUILayout.BeginVertical("helpbox");
            {
                EditorGUILayout.LabelField("ปุ่มลัดในขณะเล่น (Play Mode):", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("- กด [B] : เสก Boss ตรงหน้าผู้เล่นทันที", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("- กด [Shift + B] : เสก Boss Rush (3 ตัว)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("- กด [K] : ฆ่า Boss ทั้งหมดในฉาก", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("- กด [F1] : เปิด/ปิด หน้าต่างเมนูบนหน้าจอเกม", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void ExecuteSpawn()
        {
            for (int i = 0; i < _spawnCount; i++)
            {
                Vector3 spawnPos = CalculateSpawnPosition(i, _spawnCount);

                if (EditorApplication.isPlaying)
                {
                    if (BossSpawnerRuntime.Instance != null)
                    {
                        BossSpawnerRuntime.Instance.SpawnEnemy(_selectedType, spawnPos, _bossHpMultiplier, _bossScaleMultiplier);
                    }
                    else
                    {
                        SpawnDirectly(spawnPos);
                    }
                }
                else
                {
                    // Edit Mode Spawn
                    SpawnInEditMode(spawnPos);
                }
            }
        }

        private Vector3 CalculateSpawnPosition(int index, int total)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            Vector3 playerForward = player != null ? player.transform.forward : Vector3.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.01f) playerForward = Vector3.forward;

            switch (_locationMode)
            {
                case SpawnLocationMode.InFrontOfPlayer:
                    if (total > 1)
                    {
                        float offsetAngle = (index - (total - 1) * 0.5f) * 20f;
                        Vector3 dir = Quaternion.Euler(0f, offsetAngle, 0f) * playerForward;
                        return playerPos + dir * _spawnDistance;
                    }
                    return playerPos + playerForward * _spawnDistance;

                case SpawnLocationMode.RandomNearPlayer:
                    float randAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    return playerPos + new Vector3(Mathf.Cos(randAngle), 0f, Mathf.Sin(randAngle)) * _spawnDistance;

                case SpawnLocationMode.StageCenter:
                    Vector3 center = Vector3.zero;
                    if (total > 1)
                    {
                        float a = (360f / total) * index * Mathf.Deg2Rad;
                        return center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2f;
                    }
                    return center;

                case SpawnLocationMode.SceneViewCenter:
                    if (SceneView.lastActiveSceneView != null)
                    {
                        Vector3 pivot = SceneView.lastActiveSceneView.pivot;
                        pivot.y = 0f;
                        return pivot;
                    }
                    return Vector3.zero;

                default:
                    return playerPos + playerForward * _spawnDistance;
            }
        }

        private void SpawnDirectly(Vector3 pos)
        {
            string prefabPath = GetPrefabPathForType(_selectedType);
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
                ApplyModifiers(obj);
            }
        }

        private void SpawnInEditMode(Vector3 pos)
        {
            string prefabPath = GetPrefabPathForType(_selectedType);
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                obj.transform.position = pos;
                ApplyModifiers(obj);
                Undo.RegisterCreatedObjectUndo(obj, $"Spawn {_selectedType}");
                Selection.activeGameObject = obj;
                Debug.Log($"[BossSpawnerEditor] Spawned {obj.name} in Edit Mode at {pos}");
            }
        }

        private void ApplyModifiers(GameObject obj)
        {
            if (Mathf.Abs(_bossScaleMultiplier - 1f) > 0.01f)
            {
                obj.transform.localScale = Vector3.one * _bossScaleMultiplier;
            }

            if (obj.TryGetComponent<Health>(out var health) && _bossHpMultiplier > 0.01f)
            {
                health.SetMaxHealth(health.MaxHealth * _bossHpMultiplier, true);
            }
        }

        private string GetPrefabPathForType(BossSpawnerRuntime.EnemySpawnType type)
        {
            return type switch
            {
                BossSpawnerRuntime.EnemySpawnType.BossLeader => "Prefabs/Character/ZombieLeader",
                BossSpawnerRuntime.EnemySpawnType.Boomer => "Prefabs/Character/ZombieBoomer",
                BossSpawnerRuntime.EnemySpawnType.Dog => "Prefabs/Character/ZombieDog",
                BossSpawnerRuntime.EnemySpawnType.Runner => "Prefabs/Character/ZombieRunner",
                BossSpawnerRuntime.EnemySpawnType.Normal => "Prefabs/Character/ZombieNormal",
                _ => "Prefabs/Character/ZombieLeader"
            };
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

        private void KillAllBossesInEditMode()
        {
            var list = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var z in list)
            {
                if (z != null && z.IsBossUnit)
                {
                    Undo.DestroyObjectImmediate(z.gameObject);
                    count++;
                }
            }
            Debug.Log($"[BossSpawner] Removed {count} bosses in Edit Mode.");
        }

        private void KillAllEnemiesInEditMode()
        {
            var list = Object.FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var e in list)
            {
                if (e != null)
                {
                    Undo.DestroyObjectImmediate(e.gameObject);
                    count++;
                }
            }
            Debug.Log($"[BossSpawner] Removed {count} enemies in Edit Mode.");
        }
    }
}
