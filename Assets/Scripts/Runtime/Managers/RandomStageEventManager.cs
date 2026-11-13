using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    public class RandomStageEventManager : MonoBehaviour
    {
        private static RandomStageEventManager _instance;
        public static RandomStageEventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<RandomStageEventManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("RandomStageEventManager");
                        _instance = go.AddComponent<RandomStageEventManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (_instance == null)
            {
                var inst = Instance;
            }
        }

        [Header("Event Chance Settings")]
        [Tooltip("Chance (0.0 - 1.0) of encountering a random event when entering any normal room")]
        [Range(0f, 1f)]
        [SerializeField] private float eventChance = 0.25f;

        [Tooltip("Enable or disable random stage events globally")]
        [SerializeField] private bool enableRandomEvents = true;

        [Tooltip("Allow events to trigger in rooms that have already been cleared")]
        [SerializeField] private bool allowEventsInClearedRooms = false;

        [Header("Debug & Testing")]
        [Tooltip("เปิดใช้งานเพื่อบังคับให้เกิด Event นี้ 100% ทุกครั้งที่ก้าวเปลี่ยนห้อง")]
        [SerializeField] private bool forceEventForTesting = false;
        [SerializeField] private StageEventType forcedEventType = StageEventType.FleeingSurvivor;

        [Header("Event Atmosphere & FX")]
        [SerializeField] private Color falloutFogColor = new Color(0.2f, 0.45f, 0.15f, 1f);
        [SerializeField] private Color bloodSurgeFogColor = new Color(0.55f, 0.05f, 0.08f, 1f);

        private CanvasGroup _activeBannerGroup;
        private GameObject _activeBannerObj;
        private Coroutine _bannerCoroutine;
        private Color _originalAmbientColor;
        private bool _isAtmosphereAltered = false;

        private StageEventType _queuedNextStageEvent = StageEventType.None;
        private Coroutine _activeChaserCoroutine;
        private Coroutine _activeAmbushCoroutine;
        private Coroutine _stageEnterCoroutine;
        private GameObject _activeRunnerInstance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                _originalAmbientColor = RenderSettings.ambientLight;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            bool triggerF9 = false;
            bool triggerShiftF9 = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed)
                {
                    triggerShiftF9 = true;
                }
                else
                {
                    triggerF9 = true;
                }
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        triggerShiftF9 = true;
                    }
                    else
                    {
                        triggerF9 = true;
                    }
                }
            }
            catch {}

            if (triggerShiftF9)
            {
                Debug.Log("<color=#FF5500>[RandomStageEventManager]</color> [Shift+F9] Pressed: Triggering Maya Fleeing Event IMMEDIATELY in current room!");
                ForceTriggerFleeingSurvivor();
            }
            else if (triggerF9)
            {
                QueueNextStageFleeingSurvivor();
            }
        }

        public void QueueNextStageFleeingSurvivor()
        {
            _queuedNextStageEvent = StageEventType.FleeingSurvivor;
            Debug.Log("<color=#FF5500>[RandomStageEventManager]</color> [F9] Pressed: Next stage queued to trigger Maya Fleeing Event!");
            ShowEventBanner(
                "EVENT QUEUED: PANICKED SCOUT [F9]",
                "Walk through any portal to trigger Maya fleeing from a mob!",
                new Color(1.0f, 0.65f, 0.15f)
            );
        }

        public bool IsFleeingEventQueued => _queuedNextStageEvent == StageEventType.FleeingSurvivor;

        [ContextMenu("Force Trigger Fleeing Survivor Event Now")]
        public void ForceTriggerFleeingSurvivor()
        {
            CleanupActiveEvent();
            StageData currentStage = null;
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                currentStage = WorldMapManager.Instance.MapGenerator.GetStage(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
            ExecuteFleeingSurvivor(currentStage, new System.Random());
        }

        /// <summary>
        /// เคลียร์สถานะอีเวนต์ก่อนหน้าเมื่อเปลี่ยนห้อง (หยุด Coroutine สปอว์นมอน, ทำลาย NPC Maya ที่อาจค้างอยู่)
        /// </summary>
        public void CleanupActiveEvent()
        {
            if (_activeChaserCoroutine != null)
            {
                StopCoroutine(_activeChaserCoroutine);
                _activeChaserCoroutine = null;
            }

            if (_activeAmbushCoroutine != null)
            {
                StopCoroutine(_activeAmbushCoroutine);
                _activeAmbushCoroutine = null;
            }

            if (_stageEnterCoroutine != null)
            {
                StopCoroutine(_stageEnterCoroutine);
                _stageEnterCoroutine = null;
            }

            if (_activeRunnerInstance != null)
            {
                Destroy(_activeRunnerInstance);
                _activeRunnerInstance = null;
            }

            FleeingSurvivorRunner[] activeRunners = UnityEngine.Object.FindObjectsByType<FleeingSurvivorRunner>(FindObjectsSortMode.None);
            foreach (var r in activeRunners)
            {
                if (r != null) Destroy(r.gameObject);
            }

            ResetAtmosphere();
        }

        /// <summary>
        /// Evaluates and triggers a random encounter event when the player enters a stage.
        /// Called from GameManager.SpawnLocalStageContent.
        /// </summary>
        public void OnStageEntered(StageData stage)
        {
            if (!enableRandomEvents || stage == null) return;

            // 1. เคลียร์อีเวนต์/Coroutine/NPC ของห้องเดิมทันที เพื่อไม่ให้รั่วข้ามห้อง
            CleanupActiveEvent();

            // 2. หน่วง 1 เฟรม เพื่อรอให้ TransitionPortal วาร์ป Player ไปยังตำแหน่งประตูของห้องใหม่เรียบร้อย 100%
            _stageEnterCoroutine = StartCoroutine(OnStageEnteredRoutine(stage));
        }

        private IEnumerator OnStageEnteredRoutine(StageData stage)
        {
            yield return null; // รอ 1 เฟรมให้ตำแหน่ง Player ซิงค์กับประตูใหม่สมบูรณ์
            _stageEnterCoroutine = null;
            if (stage == null) yield break;

            // 0. Check if next stage event was queued via F9 hotkey
            if (_queuedNextStageEvent != StageEventType.None)
            {
                StageEventType queued = _queuedNextStageEvent;
                _queuedNextStageEvent = StageEventType.None; // Consume queue

                System.Random testRng = new System.Random();
                stage.randomEventType = queued;
                stage.hasEventTriggered = true;
                Debug.Log($"<color=#FF5500>[RandomStageEventManager]</color> TRIGGERING QUEUED EVENT: {queued} at Stage ({stage.x}, {stage.y})");
                ExecuteEvent(queued, stage, testRng);
                yield break;
            }

            if (forceEventForTesting)
            {
                System.Random testRng = new System.Random();
                stage.randomEventType = forcedEventType;
                stage.hasEventTriggered = true;
                Debug.Log($"<color=#FF5500>[RandomStageEventManager]</color> FORCED EVENT TRIGGERED: {forcedEventType}");
                ExecuteEvent(forcedEventType, stage, testRng);
                yield break;
            }

            // 1. Skip start / spawn room
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                if (stage.x == WorldMapManager.Instance.MapGenerator.spawnX &&
                    stage.y == WorldMapManager.Instance.MapGenerator.spawnY)
                {
                    yield break;
                }
            }

            // 2. Skip dedicated shop rooms
            if (stage.isShopStage) yield break;

            // 3. Skip already cleared rooms unless explicitly allowed
            if (stage.isCleared && !allowEventsInClearedRooms) yield break;

            // 4. Deterministic roll based on stage seed + constant salt
            System.Random rng = new System.Random(stage.stageSeed + 4242);

            // If already rolled for this stage
            if (stage.hasEventTriggered)
            {
                if (stage.randomEventType != StageEventType.None && !stage.isCleared)
                {
                    // Re-apply visual atmosphere if revisiting an ongoing event
                    ApplyEventAtmosphere(stage.randomEventType);
                }
                yield break;
            }

            double roll = rng.NextDouble();
            if (roll > eventChance)
            {
                stage.randomEventType = StageEventType.None;
                stage.hasEventTriggered = true;
                yield break;
            }

            // Pick an event from the pool
            StageEventType[] pool = new StageEventType[]
            {
                StageEventType.SupplyAirdrop,
                StageEventType.HostileAmbush,
                StageEventType.NomadTrader,
                StageEventType.RadioactiveFallout,
                StageEventType.BloodMoonSurge,
                StageEventType.ScavengerCache,
                StageEventType.FleeingSurvivor
            };

            StageEventType chosenEvent = pool[rng.Next(0, pool.Length)];
            stage.randomEventType = chosenEvent;
            stage.hasEventTriggered = true;

            Debug.Log($"<color=#FFD700>[RandomStageEventManager]</color> Triggered Dynamic Event: {chosenEvent} at Stage ({stage.x}, {stage.y})");
            ExecuteEvent(chosenEvent, stage, rng);
        }

        private void ExecuteEvent(StageEventType eventType, StageData stage, System.Random rng)
        {
            switch (eventType)
            {
                case StageEventType.SupplyAirdrop:
                    ExecuteSupplyAirdrop(stage, rng);
                    break;

                case StageEventType.HostileAmbush:
                    ExecuteHostileAmbush(stage, rng);
                    break;

                case StageEventType.NomadTrader:
                    ExecuteNomadTrader(stage, rng);
                    break;

                case StageEventType.RadioactiveFallout:
                    ExecuteRadioactiveFallout(stage);
                    break;

                case StageEventType.BloodMoonSurge:
                    ExecuteBloodMoonSurge(stage);
                    break;

                case StageEventType.ScavengerCache:
                    ExecuteScavengerCache(stage, rng);
                    break;

                case StageEventType.FleeingSurvivor:
                    ExecuteFleeingSurvivor(stage, rng);
                    break;
            }
        }

        // ==============================================================================
        // 1. SUPPLY AIRDROP EVENT
        // ==============================================================================
        private void ExecuteSupplyAirdrop(StageData stage, System.Random rng)
        {
            ShowEventBanner(
                "SUPPLY AIRDROP DETECTED",
                "A military cargo container has landed nearby with weapons and ammo.",
                new Color(1.0f, 0.78f, 0.15f) // Vibrant Amber Gold
            );

            // Spawn high-value loot in the room
            Vector3 dropPos = new Vector3(
                (float)(rng.NextDouble() * 6.0 - 3.0),
                0.2f,
                (float)(rng.NextDouble() * 4.0 - 2.0)
            );

            // Spawn 3-4 high value drops (High-tier weapon/ammo + money)
            string[] bonusWeapons = new string[] { "Sniper Rifle", "Rocket Launcher", "Katana", "Shotgun", "Adrenaline Shot" };
            string chosenWeapon = bonusWeapons[rng.Next(0, bonusWeapons.Length)];

            SpawnGroundItem(chosenWeapon, 1, dropPos);
            SpawnGroundItem("Rifle Ammo", rng.Next(30, 60), dropPos + new Vector3(0.8f, 0, 0.5f));
            SpawnGroundMoney(rng.Next(250, 600), dropPos + new Vector3(-0.8f, 0, -0.4f));
            SpawnGroundItem("First Aid Kit", 1, dropPos + new Vector3(0.4f, 0, -0.7f));
        }

        // ==============================================================================
        // 2. HOSTILE AMBUSH EVENT
        // ==============================================================================
        private void ExecuteHostileAmbush(StageData stage, System.Random rng)
        {
            ShowEventBanner(
                "WARNING: HOSTILE AMBUSH",
                "Enemy swarm detected! An aggressive assault wave is charging your position.",
                new Color(1.0f, 0.20f, 0.25f) // Danger Crimson
            );

            // Spawn 3-5 rapid ambush rushers (ห้ามเกิดเอเลี่ยนตอนกลางวันเด็ดขาด)
            int ambushCount = rng.Next(3, 6);
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            List<string> rusherList = new List<string> { "ZombieRunner", "RobotKamikaze", "ZombieDog" };
            if (isNight)
            {
                rusherList.Add("AlienCrawler");
            }
            string[] rushers = rusherList.ToArray();

            _activeAmbushCoroutine = StartCoroutine(SpawnAmbushWaveRoutine(rushers, ambushCount, rng));
        }

        private IEnumerator SpawnAmbushWaveRoutine(string[] rushers, int count, System.Random rng)
        {
            yield return new WaitForSeconds(1.2f);

            if (GameManager.Instance == null) yield break;

            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Vector3 pPos = player != null ? player.transform.position : Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                string enemyName = rushers[rng.Next(0, rushers.Length)];
                GameObject prefab = GameManager.Instance.GetPrefabByName(enemyName);
                if (prefab == null && GameManager.Instance.DefaultEnemyPrefab != null)
                {
                    prefab = GameManager.Instance.DefaultEnemyPrefab;
                }

                if (prefab != null)
                {
                    float angle = (float)(i * (2.0 * Math.PI / count));
                    float dist = (float)(rng.NextDouble() * 3.0 + 7.5);
                    Vector3 spawnPos = pPos + new Vector3(Mathf.Cos(angle) * dist, 0.2f, Mathf.Sin(angle) * dist);

                    // Clamp within room bounds (32x18m)
                    spawnPos.x = Mathf.Clamp(spawnPos.x, -13f, 13f);
                    spawnPos.z = Mathf.Clamp(spawnPos.z, -6.5f, 6.5f);

                    string poolKey = GameManager.Instance.GetPoolKeyForPrefab(prefab);
                    GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, spawnPos, Quaternion.identity);
                }
            }
        }

        // ==============================================================================
        // 3. NOMAD TRADER EVENT
        // ==============================================================================
        private void ExecuteNomadTrader(StageData stage, System.Random rng)
        {
            ShowEventBanner(
                "WANDERING NOMAD CARAVAN",
                "A travelling wasteland merchant has set up a temporary camp here.",
                new Color(0.15f, 0.85f, 1.0f) // Cyan Teal
            );

            // Choose a random traveling merchant from the database
            string[] nomads = new string[] { "doctor_elena", "mechanic_otto", "watcher_maya", "survivor_roy" };
            string chosenNomad = nomads[rng.Next(0, nomads.Length)];

            Vector3 campPos = new Vector3(
                (float)(rng.NextDouble() * 6.0 - 3.0),
                0f,
                (float)(rng.NextDouble() * 3.0 - 1.5f)
            );

            // Spawn the NPC
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpawnNPCInstance(NPCType.Shop, campPos + Vector3.up * 0.1f, chosenNomad);
            }
        }

        // ==============================================================================
        // 4. RADIOACTIVE FALLOUT EVENT
        // ==============================================================================
        private void ExecuteRadioactiveFallout(StageData stage)
        {
            ShowEventBanner(
                "HAZARD: RADIOACTIVE FALLOUT",
                "Toxic radiation storm detected! Sanity & Hunger drain faster in this zone.",
                new Color(0.45f, 1.0f, 0.15f) // Toxic Neon Green
            );

            ApplyEventAtmosphere(StageEventType.RadioactiveFallout);

            // Slightly drain sanity & accelerate hunger while in this room
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.DrainSanity(10f);
            }
        }

        // ==============================================================================
        // 5. BLOOD MOON SURGE EVENT
        // ==============================================================================
        private void ExecuteBloodMoonSurge(StageData stage)
        {
            ShowEventBanner(
                "ANOMALY: BLOOD MOON SURGE",
                "Cosmic frenzy detected! Enemies have +30% Speed but drop 2x Gold.",
                new Color(1.0f, 0.10f, 0.20f) // Blood Red
            );

            ApplyEventAtmosphere(StageEventType.BloodMoonSurge);

            // Buff all spawned enemies in this stage
            if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                foreach (var enemy in GameManager.Instance.ActiveEnemies)
                {
                    if (enemy != null)
                    {
                        BaseEnemyAI ai = enemy.GetComponent<BaseEnemyAI>();
                        if (ai != null)
                        {
                            ai.ApplyTemporaryStats(speedMultiplier: 1.30f);
                        }
                    }
                }
            }
        }

        // ==============================================================================
        // 6. SCAVENGER CACHE EVENT
        // ==============================================================================
        private void ExecuteScavengerCache(StageData stage, System.Random rng)
        {
            ShowEventBanner(
                "SCAVENGER CACHE LOCATED",
                "Concealed survivor loot crates and ammunition stashes discovered.",
                new Color(1.0f, 0.55f, 0.15f) // Scavenger Bronze
            );

            // Scatter 4-6 drops around corners
            for (int i = 0; i < 4; i++)
            {
                Vector3 pos = new Vector3(
                    (float)(rng.NextDouble() * 16.0 - 8.0),
                    0.2f,
                    (float)(rng.NextDouble() * 8.0 - 4.0)
                );

                if (i % 2 == 0)
                {
                    SpawnGroundMoney(rng.Next(80, 220), pos);
                }
                else
                {
                    string[] items = new string[] { "Bread", "Bandage", "Pistol Ammo", "Shotgun Ammo", "Grenade" };
                    SpawnGroundItem(items[rng.Next(0, items.Length)], rng.Next(1, 3), pos);
                }
            }
        }

        // ==============================================================================
        // 7. FLEEING SURVIVOR EVENT (NPC วิ่งลากฝูงศัตรูหนีตายผ่านตัวเรา)
        // ==============================================================================
        private void ExecuteFleeingSurvivor(StageData stage, System.Random rng)
        {
            ShowEventBanner(
                "PANICKED SCOUT DETECTED!",
                "Maya the Scout is sprinting for her life, dragging a hostile mob behind her!",
                new Color(1.0f, 0.45f, 0.15f) // High Alert Orange-Red
            );

            // เคลียร์ศัตรูเดิมที่อาจสุ่มเกิดค้างอยู่ในห้อง เพื่อไม่ให้ศัตรูเกิดมั่วซั่วในฉาก
            // ให้มีเฉพาะฝูงที่กำลังวิ่งไล่กวดตามหลัง Maya เท่านั้น
            if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                for (int i = GameManager.Instance.ActiveEnemies.Count - 1; i >= 0; i--)
                {
                    GameObject existingEnemy = GameManager.Instance.ActiveEnemies[i];
                    if (existingEnemy != null)
                    {
                        BaseEnemyAI ai = existingEnemy.GetComponent<BaseEnemyAI>();
                        if (ai != null && !string.IsNullOrEmpty(ai.PoolKey) && ObjectPoolManager.Instance != null)
                        {
                            Health h = existingEnemy.GetComponent<Health>();
                            if (h != null) h.ResetHealth();
                            ObjectPoolManager.Instance.ReturnToPool(ai.PoolKey, existingEnemy);
                        }
                        else
                        {
                            Destroy(existingEnemy);
                        }
                    }
                }
                GameManager.Instance.ActiveEnemies.Clear();
            }

            if (stage != null)
            {
                stage.remainingEnemyPrefabNames = new List<string>();
            }

            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            float floorY = (player != null && player.transform.position.y > 0.8f) ? player.transform.position.y : 2.0f;

            // คำนวณตำแหน่งเกิดตรงกันข้ามกับ Player:
            // Get ตำแหน่งจริงของ Player แล้วเสก Maya ที่ฝั่งตรงข้าม วิ่งตรงเข้าหา Player และทะลุออกประตูที่เราเข้ามา
            Vector3 npcSpawnPos;
            Vector3 escapeTargetPos;

            if (Mathf.Abs(playerPos.z) >= Mathf.Abs(playerPos.x))
            {
                if (playerPos.z < 0)
                {
                    // Player อยู่ฝั่งล่าง (South) -> Maya เกิดฝั่งตรงข้ามบน (North: z=+9.5) วิ่งลงมาหาเรา แล้วทะลุออกประตูล่าง (z=-18)
                    npcSpawnPos = new Vector3(playerPos.x, floorY, 9.5f);
                    escapeTargetPos = new Vector3(playerPos.x, floorY, -18.0f);
                }
                else
                {
                    // Player อยู่ฝั่งบน (North) -> Maya เกิดฝั่งตรงข้ามล่าง (South: z=-9.5) วิ่งขึ้นมาหาเรา แล้วทะลุออกประตูบน (z=+18)
                    npcSpawnPos = new Vector3(playerPos.x, floorY, -9.5f);
                    escapeTargetPos = new Vector3(playerPos.x, floorY, 18.0f);
                }
            }
            else
            {
                if (playerPos.x < 0)
                {
                    // Player อยู่ฝั่งซ้าย (West) -> Maya เกิดฝั่งตรงข้ามขวา (East: x=+15.5) วิ่งมาหาเรา แล้วทะลุออกประตูซ้าย (x=-25)
                    npcSpawnPos = new Vector3(15.5f, floorY, playerPos.z);
                    escapeTargetPos = new Vector3(-25.0f, floorY, playerPos.z);
                }
                else
                {
                    // Player อยู่ฝั่งขวา (East) -> Maya เกิดฝั่งตรงข้ามซ้าย (West: x=-15.5) วิ่งมาหาเรา แล้วทะลุออกประตูขวา (x=+25)
                    npcSpawnPos = new Vector3(-15.5f, floorY, playerPos.z);
                    escapeTargetPos = new Vector3(25.0f, floorY, playerPos.z);
                }
            }

            npcSpawnPos.y = floorY;
            escapeTargetPos.y = floorY;
            Debug.Log($"<color=#FF5500>[RandomStageEventManager]</color> Fleeing Survivor Course: Spawn {npcSpawnPos} (Opposite of Player {playerPos}) -> Run towards Player -> Exit at {escapeTargetPos}");

            // Spawn NPC Maya (Scout) วิ่งหนี
            GameObject npcPrefab = GameManager.Instance != null ? GameManager.Instance.NPCPrefab : null;
            if (npcPrefab == null)
            {
                npcPrefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Character/NPCPrefabs");
            }
            if (npcPrefab != null)
            {
                Vector3 runDirection = (playerPos - npcSpawnPos).normalized;
                runDirection.y = 0f;
                if (runDirection.sqrMagnitude < 0.001f)
                {
                    runDirection = (escapeTargetPos - npcSpawnPos).normalized;
                }

                GameObject npcObj = Instantiate(npcPrefab, npcSpawnPos, Quaternion.LookRotation(runDirection));
                _activeRunnerInstance = npcObj;

                // แปลงโมเดลและข้อมูลเป็น Maya (Scout) จาก NPCDatabase
                NPCController ctrl = npcObj.GetComponent<NPCController>();
                if (ctrl != null && NPCDatabase.Instance != null)
                {
                    NPCDataSO mayaData = NPCDatabase.Instance.GetNPCById("watcher_maya");
                    if (mayaData != null)
                    {
                        ctrl.ApplyData(mayaData);
                    }
                    ctrl.CanInteract = false;
                }

                FleeingSurvivorRunner runner = npcObj.AddComponent<FleeingSurvivorRunner>();
                runner.Initialize(player != null ? player.transform : null, escapeTargetPos, null, speed: 5.0f);
            }

            // ปล่อยฝูงศัตรูที่วิ่งไล่กวดตามหลัง NPC มา
            _activeChaserCoroutine = StartCoroutine(SpawnChasingEnemyPackRoutine(npcSpawnPos, escapeTargetPos, rng, stage));
        }

        private IEnumerator SpawnChasingEnemyPackRoutine(Vector3 spawnCenter, Vector3 targetDir, System.Random rng, StageData stage)
        {
            // หน่วงเวลาเล็กน้อย 0.4 วินาที เพื่อให้ Maya วิ่งนำหน้าออกไปก่อนในระยะกำลังสวย
            yield return new WaitForSeconds(0.40f);

            if (GameManager.Instance == null) yield break;

            // สุ่มจำนวนศัตรู 4-5 ตัว (ห้ามเกิดเอเลี่ยนตอนกลางวันเด็ดขาด)
            int mobCount = rng.Next(4, 6);
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            List<string> rusherList = new List<string> { "ZombieRunner", "ZombieDog", "RobotKamikaze", "ZombieNormal" };
            if (isNight)
            {
                rusherList.Add("AlienCrawler");
                rusherList.Add("AlienSpitter");
            }
            string[] rushers = rusherList.ToArray();

            Vector3 runDir = (targetDir - spawnCenter).normalized;
            Vector3 perpDir = Vector3.Cross(runDir, Vector3.up);

            for (int i = 0; i < mobCount; i++)
            {
                string enemyName = rushers[rng.Next(0, rushers.Length)];
                GameObject prefab = GameManager.Instance.GetPrefabByName(enemyName);
                if (prefab == null && GameManager.Instance.DefaultEnemyPrefab != null)
                {
                    prefab = GameManager.Instance.DefaultEnemyPrefab;
                }

                if (prefab != null)
                {
                    // วิ่งเรียงแถวตามหลัง Maya บนถนนเส้นหลัก (กระจายข้างแคบๆ ±0.5m ไม่ให้หลุดเข้าไปในตึก)
                    float sideOffset = (float)(rng.NextDouble() * 1.0 - 0.5);
                    float rearOffset = -(float)(rng.NextDouble() * 1.2 + 0.8) - (i * 0.8f);
                    Vector3 enemySpawnPos = spawnCenter + (perpDir * sideOffset) + (runDir * rearOffset);
                    enemySpawnPos.y = spawnCenter.y; // อยู่บนระดับพื้นห้องเสมอ ไม่จมดิน

                    // Clamp ให้อยู่ในฉาก
                    enemySpawnPos.x = Mathf.Clamp(enemySpawnPos.x, -14f, 14f);
                    enemySpawnPos.z = Mathf.Clamp(enemySpawnPos.z, -7f, 7f);

                    string poolKey = GameManager.Instance.GetPoolKeyForPrefab(prefab);
                    GameObject enemyObj = GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, enemySpawnPos, Quaternion.LookRotation(runDir));

                    if (enemyObj != null)
                    {
                        if (stage != null && stage.remainingEnemyPrefabNames != null)
                        {
                            stage.remainingEnemyPrefabNames.Add(prefab.name);
                        }

                        BaseEnemyAI ai = enemyObj.GetComponent<BaseEnemyAI>();
                        if (ai != null)
                        {
                            // ความเร็วไล่กวด 4.5 m/s (ช้ากว่า Maya 5 m/s เล็กน้อย ไม่แซง) + มองเห็นไกล 50m
                            // ใช้ ApplyTemporaryStats เพื่อไม่ให้ค่าติดไปกับศัตรูใน Pool ที่ถูกนำไปใช้ซ้ำในห้องอื่น
                            ai.ApplyTemporaryStats(speed: 4.5f, detection: 50f);
                            ai.SkipTransitionDelay(); // วิ่งไล่ทันที ไม่ยืนนิ่ง 2 วินาทีตอนเกิด
                            enemyObj.transform.forward = runDir;
                        }
                    }
                }

                // หน่วงระยะห่างระหว่างการกวดของแต่ละตัวเล็กน้อย
                yield return new WaitForSeconds(0.22f);
            }

            _activeChaserCoroutine = null;
        }

        // ==============================================================================
        // ATMOSPHERIC LIGHTING & FOG OVERRIDES
        // ==============================================================================
        private void ApplyEventAtmosphere(StageEventType eventType)
        {
            if (eventType == StageEventType.RadioactiveFallout)
            {
                RenderSettings.ambientLight = falloutFogColor;
                _isAtmosphereAltered = true;
            }
            else if (eventType == StageEventType.BloodMoonSurge)
            {
                RenderSettings.ambientLight = bloodSurgeFogColor;
                _isAtmosphereAltered = true;
            }
        }

        private void ResetAtmosphere()
        {
            if (_isAtmosphereAltered)
            {
                RenderSettings.ambientLight = _originalAmbientColor;
                _isAtmosphereAltered = false;
            }
        }

        // ==============================================================================
        // ITEM SPAWNING HELPERS
        // ==============================================================================
        private void SpawnGroundItem(string itemName, int count, Vector3 pos)
        {
            if (GameManager.Instance == null || GameManager.Instance.ItemDropPrefab == null) return;
            CollectibleItem spawned = Instantiate(GameManager.Instance.ItemDropPrefab, pos, Quaternion.identity);
            if (spawned != null)
            {
                spawned.SnapToGround();
                spawned.SetItemDetails(itemName, count);
            }
        }

        private void SpawnGroundMoney(int amount, Vector3 pos)
        {
            if (GameManager.Instance == null || GameManager.Instance.MoneyDropPrefab == null) return;
            CollectibleItem spawned = Instantiate(GameManager.Instance.MoneyDropPrefab, pos, Quaternion.identity);
            if (spawned != null)
            {
                spawned.SnapToGround();
                spawned.SetMoneyDetails(amount);
            }
        }

        // ==============================================================================
        // HUD EVENT BANNER & NOTIFICATION INTEGRATION
        // ==============================================================================
        public void ShowEventBanner(string title, string subtitle, Color accentColor)
        {
            if (_bannerCoroutine != null)
            {
                StopCoroutine(_bannerCoroutine);
            }
            _bannerCoroutine = StartCoroutine(ShowBannerRoutine(title, subtitle, accentColor));
        }

        private IEnumerator ShowBannerRoutine(string title, string subtitle, Color accentColor)
        {
            // ทำลายแผง procedural ชั่วคราวเดิมทิ้งถ้ามีค้างอยู่
            if (_activeBannerObj != null)
            {
                Destroy(_activeBannerObj);
                _activeBannerObj = null;
            }

            // ค้นหาแผง Notification_HUD ที่มีอยู่เดิมใน Scene
            GameObject notiHud = GameObject.Find("Notification_HUD");
            if (notiHud == null)
            {
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in canvases)
                {
                    Transform t = c.transform.Find("Player_HUD/Notification_HUD") 
                               ?? c.transform.Find("Notification_HUD");
                    if (t != null)
                    {
                        notiHud = t.gameObject;
                        break;
                    }
                }
            }

            if (notiHud != null)
            {
                notiHud.SetActive(true);

                // ค้นหากล่องข้อความ Name_text (หัวข้อ) และ Notification_text (เนื้อหา)
                TMP_Text titleText = notiHud.transform.Find("NamePanel/Name_text")?.GetComponent<TMP_Text>()
                                  ?? notiHud.transform.Find("Panel/NamePanel/Name_text")?.GetComponent<TMP_Text>();
                TMP_Text msgText = notiHud.transform.Find("NotificationPanel/Notification_text")?.GetComponent<TMP_Text>()
                                ?? notiHud.transform.Find("Panel/NotificationPanel/Notification_text")?.GetComponent<TMP_Text>();

                if (titleText == null || msgText == null)
                {
                    TMP_Text[] allTexts = notiHud.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var t in allTexts)
                    {
                        string lower = t.gameObject.name.ToLower();
                        if (titleText == null && (lower.Contains("name") || lower.Contains("title"))) titleText = t;
                        else if (msgText == null && (lower.Contains("noti") || lower.Contains("msg") || lower.Contains("text") || lower.Contains("desc"))) msgText = t;
                    }
                }

                if (titleText != null)
                {
                    titleText.text = title;
                    titleText.color = accentColor;
                }
                if (msgText != null)
                {
                    msgText.text = subtitle;
                }

                CanvasGroup bannerGroup = notiHud.GetComponent<CanvasGroup>();
                if (bannerGroup == null) bannerGroup = notiHud.AddComponent<CanvasGroup>();
                bannerGroup.blocksRaycasts = false;

                // Smooth Fade In
                float elapsed = 0f;
                float fadeIn = 0.35f;
                while (elapsed < fadeIn)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bannerGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeIn);
                    yield return null;
                }
                bannerGroup.alpha = 1f;

                // Display
                yield return new WaitForSecondsRealtime(3.5f);

                // Smooth Fade Out
                elapsed = 0f;
                float fadeOut = 0.5f;
                while (elapsed < fadeOut)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bannerGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOut);
                    yield return null;
                }
                bannerGroup.alpha = 0f;
                notiHud.SetActive(false);
            }
        }
    }
}

