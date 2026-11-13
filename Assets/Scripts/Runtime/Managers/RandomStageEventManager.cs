using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    public class RandomStageEventManager : MonoBehaviour
    {
        public static RandomStageEventManager Instance { get; private set; }

        [Header("Event Chance Settings")]
        [Tooltip("Chance (0.0 - 1.0) of encountering a random event when entering any normal room")]
        [Range(0f, 1f)]
        [SerializeField] private float eventChance = 0.25f;

        [Tooltip("Enable or disable random stage events globally")]
        [SerializeField] private bool enableRandomEvents = true;

        [Tooltip("Allow events to trigger in rooms that have already been cleared")]
        [SerializeField] private bool allowEventsInClearedRooms = false;

        [Header("Event Atmosphere & FX")]
        [SerializeField] private Color falloutFogColor = new Color(0.2f, 0.45f, 0.15f, 1f);
        [SerializeField] private Color bloodSurgeFogColor = new Color(0.55f, 0.05f, 0.08f, 1f);

        private CanvasGroup _activeBannerGroup;
        private GameObject _activeBannerObj;
        private Coroutine _bannerCoroutine;
        private Color _originalAmbientColor;
        private bool _isAtmosphereAltered = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                _originalAmbientColor = RenderSettings.ambientLight;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Evaluates and triggers a random encounter event when the player enters a stage.
        /// Called from GameManager.SpawnLocalStageContent.
        /// </summary>
        public void OnStageEntered(StageData stage)
        {
            if (!enableRandomEvents || stage == null) return;

            // Reset any previous atmospheric overrides from past room
            ResetAtmosphere();

            // 1. Skip start / spawn room
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                if (stage.x == WorldMapManager.Instance.MapGenerator.spawnX &&
                    stage.y == WorldMapManager.Instance.MapGenerator.spawnY)
                {
                    return;
                }
            }

            // 2. Skip dedicated shop rooms
            if (stage.isShopStage) return;

            // 3. Skip already cleared rooms unless explicitly allowed
            if (stage.isCleared && !allowEventsInClearedRooms) return;

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
                return;
            }

            double roll = rng.NextDouble();
            if (roll > eventChance)
            {
                stage.randomEventType = StageEventType.None;
                stage.hasEventTriggered = true;
                return;
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

            // Execute the event
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

            // Spawn 3-5 rapid ambush rushers
            int ambushCount = rng.Next(3, 6);
            string[] rushers = new string[] { "ZombieRunner", "RobotKamikaze", "AlienCrawler", "ZombieDog" };

            StartCoroutine(SpawnAmbushWaveRoutine(rushers, ambushCount, rng));
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
                            ai.MoveSpeed *= 1.30f;
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

            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

            // คำนวณทิศทาง: Player วาปเข้ามาจากทิศไหน?
            // NPC จะเกิดที่ทิศตรงข้าม แล้ววิ่งตัดผ่านห้องไปยังทิศที่ Player วาปเข้ามา
            Vector3 npcSpawnPos;
            Vector3 escapeTargetPos;

            TransitionPortal.TransitionDirection entryDir = TransitionPortal.TransitionDirection.South;
            if (TransitionPortal.HasValidWarpHistory)
            {
                entryDir = TransitionPortal.LastTransitionEntryDirection;
            }
            else
            {
                // Fallback ตามตำแหน่งตัว Player เทียบกับจุดศูนย์กลางห้อง
                if (playerPos.z < -2f) entryDir = TransitionPortal.TransitionDirection.South;
                else if (playerPos.z > 2f) entryDir = TransitionPortal.TransitionDirection.North;
                else if (playerPos.x < -3f) entryDir = TransitionPortal.TransitionDirection.West;
                else if (playerPos.x > 3f) entryDir = TransitionPortal.TransitionDirection.East;
            }

            switch (entryDir)
            {
                case TransitionPortal.TransitionDirection.South:
                    // Player เข้ามาจากทิศใต้ -> NPC เกิดทิศเหนือ วิ่งลงทิศใต้
                    npcSpawnPos = new Vector3(Mathf.Clamp(playerPos.x * 0.5f, -4f, 4f), 0.5f, 7.5f);
                    escapeTargetPos = new Vector3(playerPos.x, 0.5f, -9.5f);
                    break;

                case TransitionPortal.TransitionDirection.North:
                    // Player เข้ามาจากทิศเหนือ -> NPC เกิดทิศใต้ วิ่งขึ้นทิศเหนือ
                    npcSpawnPos = new Vector3(Mathf.Clamp(playerPos.x * 0.5f, -4f, 4f), 0.5f, -7.5f);
                    escapeTargetPos = new Vector3(playerPos.x, 0.5f, 9.5f);
                    break;

                case TransitionPortal.TransitionDirection.West:
                    // Player เข้ามาจากทิศตะวันตก -> NPC เกิดทิศตะวันออก วิ่งไปตะวันตก
                    npcSpawnPos = new Vector3(14.5f, 0.5f, Mathf.Clamp(playerPos.z * 0.5f, -3f, 3f));
                    escapeTargetPos = new Vector3(-16.5f, 0.5f, playerPos.z);
                    break;

                case TransitionPortal.TransitionDirection.East:
                default:
                    // Player เข้ามาจากทิศตะวันออก -> NPC เกิดทิศตะวันตก วิ่งไปตะวันออก
                    npcSpawnPos = new Vector3(-14.5f, 0.5f, Mathf.Clamp(playerPos.z * 0.5f, -3f, 3f));
                    escapeTargetPos = new Vector3(16.5f, 0.5f, playerPos.z);
                    break;
            }

            // สุ่มเบี่ยงพิกัดเกิดเล็กน้อยเพื่อให้ดูเป็นธรรมชาติ
            Vector3 perpendicular = Vector3.Cross((escapeTargetPos - npcSpawnPos).normalized, Vector3.up);
            float randomJitter = (float)(rng.NextDouble() * 1.6 - 0.8);
            npcSpawnPos += perpendicular * randomJitter;

            // Spawn NPC Maya (Scout) วิ่งหนี
            GameObject npcPrefab = GameManager.Instance != null ? GameManager.Instance.NPCPrefab : null;
            if (npcPrefab != null)
            {
                Vector3 runDirection = (escapeTargetPos - npcSpawnPos).normalized;
                GameObject npcObj = Instantiate(npcPrefab, npcSpawnPos, Quaternion.LookRotation(runDirection));

                // แปลงโมเดลและข้อมูลเป็น Maya (Scout) จาก NPCDatabase
                NPCController ctrl = npcObj.GetComponent<NPCController>();
                if (ctrl != null && NPCDatabase.Instance != null)
                {
                    NPCDataSO mayaData = NPCDatabase.Instance.GetNPCById("watcher_maya");
                    if (mayaData != null)
                    {
                        ctrl.ApplyData(mayaData);
                    }
                }

                FleeingSurvivorRunner runner = npcObj.AddComponent<FleeingSurvivorRunner>();
                runner.Initialize(escapeTargetPos, null, speed: 8.5f);
            }

            // ปล่อยฝูงศัตรูที่วิ่งไล่กวดตามหลัง NPC มา
            StartCoroutine(SpawnChasingEnemyPackRoutine(npcSpawnPos, escapeTargetPos, rng));
        }

        private IEnumerator SpawnChasingEnemyPackRoutine(Vector3 spawnCenter, Vector3 targetDir, System.Random rng)
        {
            // หน่วงเวลาเล็กน้อย 0.35 วินาที เพื่อให้ NPC วิ่งนำหน้าออกไปก่อน
            yield return new WaitForSeconds(0.35f);

            if (GameManager.Instance == null) yield break;

            // สุ่มจำนวนศัตรู 3-5 ตัว
            int mobCount = rng.Next(3, 6);
            string[] rushers = new string[] { "ZombieRunner", "AlienCrawler", "ZombieDog", "RobotKamikaze", "AlienSpitter", "ZombieNormal" };

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
                    // กระจายตำแหน่งเกิดแถวหลัง NPC ในระยะกระจัด ±1.8 เมตร
                    float sideOffset = (float)(rng.NextDouble() * 3.6 - 1.8);
                    float rearOffset = -(float)(rng.NextDouble() * 1.5 + 0.5);
                    Vector3 enemySpawnPos = spawnCenter + (perpDir * sideOffset) + (runDir * rearOffset);

                    // Clamp ให้อยู่ในฉาก
                    enemySpawnPos.x = Mathf.Clamp(enemySpawnPos.x, -14f, 14f);
                    enemySpawnPos.z = Mathf.Clamp(enemySpawnPos.z, -7f, 7f);

                    string poolKey = GameManager.Instance.GetPoolKeyForPrefab(prefab);
                    GameObject enemyObj = GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, enemySpawnPos, Quaternion.LookRotation(runDir));

                    if (enemyObj != null)
                    {
                        BaseEnemyAI ai = enemyObj.GetComponent<BaseEnemyAI>();
                        if (ai != null)
                        {
                            // บัฟระยะมองเห็นและความเร็วในการวิ่งกวด เพื่อให้วิ่งกวดมาหาผู้เล่นทันที
                            ai.DetectionRange = 50f;
                            ai.MoveSpeed *= 1.25f;
                            enemyObj.transform.forward = runDir;
                        }
                    }
                }

                // หน่วงระยะห่างระหว่างการกวดของแต่ละตัวเล็กน้อย
                yield return new WaitForSeconds(0.18f);
            }
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
        // SLEEK GLASSMORPHISM HUD EVENT BANNER
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
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) yield break;

            if (_activeBannerObj == null)
            {
                _activeBannerObj = new GameObject("DynamicEventBanner_HUD");
                _activeBannerObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = _activeBannerObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -25f);
                rect.sizeDelta = new Vector2(680f, 95f);

                _activeBannerGroup = _activeBannerObj.AddComponent<CanvasGroup>();
                _activeBannerGroup.blocksRaycasts = false;

                // Translucent slate background
                UnityEngine.UI.Image bg = _activeBannerObj.AddComponent<UnityEngine.UI.Image>();
                bg.color = new Color(0.04f, 0.06f, 0.10f, 0.92f);
                bg.raycastTarget = false;

                // Top Accent Stripe
                GameObject stripe = new GameObject("AccentStripe");
                stripe.transform.SetParent(_activeBannerObj.transform, false);
                RectTransform sRect = stripe.AddComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0f, 1f);
                sRect.anchorMax = new Vector2(1f, 1f);
                sRect.pivot = new Vector2(0.5f, 1f);
                sRect.anchoredPosition = Vector2.zero;
                sRect.sizeDelta = new Vector2(0f, 4f);
                UnityEngine.UI.Image sImg = stripe.AddComponent<UnityEngine.UI.Image>();
                sImg.color = accentColor;
                sImg.raycastTarget = false;

                // Title Text
                GameObject titleObj = new GameObject("TitleText");
                titleObj.transform.SetParent(_activeBannerObj.transform, false);
                RectTransform tRect = titleObj.AddComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0f, 0.5f);
                tRect.anchorMax = new Vector2(1f, 1f);
                tRect.anchoredPosition = new Vector2(25f, -12f);
                tRect.sizeDelta = new Vector2(-50f, 32f);
                TextMeshProUGUI tText = titleObj.AddComponent<TextMeshProUGUI>();
                tText.text = title;
                tText.fontSize = 17;
                tText.fontStyle = FontStyles.Bold;
                tText.color = accentColor;
                tText.alignment = TextAlignmentOptions.MidlineLeft;

                // Subtitle Text
                GameObject subObj = new GameObject("SubtitleText");
                subObj.transform.SetParent(_activeBannerObj.transform, false);
                RectTransform sbRect = subObj.AddComponent<RectTransform>();
                sbRect.anchorMin = new Vector2(0f, 0f);
                sbRect.anchorMax = new Vector2(1f, 0.5f);
                sbRect.anchoredPosition = new Vector2(25f, 6f);
                sbRect.sizeDelta = new Vector2(-50f, 28f);
                TextMeshProUGUI sbText = subObj.AddComponent<TextMeshProUGUI>();
                sbText.text = subtitle;
                sbText.fontSize = 12;
                sbText.color = new Color(0.85f, 0.88f, 0.92f, 0.90f);
                sbText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                // Update existing banner
                _activeBannerObj.SetActive(true);
                Transform stripe = _activeBannerObj.transform.Find("AccentStripe");
                if (stripe != null)
                {
                    UnityEngine.UI.Image sImg = stripe.GetComponent<UnityEngine.UI.Image>();
                    if (sImg != null) sImg.color = accentColor;
                }
                Transform tObj = _activeBannerObj.transform.Find("TitleText");
                if (tObj != null)
                {
                    TextMeshProUGUI tText = tObj.GetComponent<TextMeshProUGUI>();
                    if (tText != null)
                    {
                        tText.text = title;
                        tText.color = accentColor;
                    }
                }
                Transform sbObj = _activeBannerObj.transform.Find("SubtitleText");
                if (sbObj != null)
                {
                    TextMeshProUGUI sbText = sbObj.GetComponent<TextMeshProUGUI>();
                    if (sbText != null) sbText.text = subtitle;
                }
            }

            // Fade In
            float elapsed = 0f;
            while (elapsed < 0.35f)
            {
                elapsed += Time.deltaTime;
                _activeBannerGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.35f);
                yield return null;
            }
            _activeBannerGroup.alpha = 1f;

            // Display for 4.5 seconds
            yield return new WaitForSeconds(4.5f);

            // Fade Out
            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                _activeBannerGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
                yield return null;
            }
            _activeBannerGroup.alpha = 0f;
            _activeBannerObj.SetActive(false);
        }
    }
}

