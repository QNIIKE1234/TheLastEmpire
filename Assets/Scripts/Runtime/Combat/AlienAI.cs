using System.Collections;
using UnityEngine;

namespace TheLastEmpire
{
    public class AlienAI : BaseEnemyAI
    {
        public enum AlienType
        {
            Normal,
            Hook,
            Invisible,
            Smart,
            Leader,
            Spitter,
            Crawler
        }

        [Header("Alien Settings")]
        [SerializeField] private AlienType alienType = AlienType.Normal;

        [Header("Hook Settings")]
        [SerializeField] private float hookCooldown = 4f;
        [SerializeField] private float hookRange = 8f;
        [SerializeField] private float pullForce = 8f;

        [Header("Invisible Settings")]
        [SerializeField] private float fadeDistance = 4f;

        [Header("Alien Spitter Settings")]
        [SerializeField] private float spitCooldown = 3.5f;
        [SerializeField] private float spitRange = 8f;
        [SerializeField] private float spitProjectileSpeed = 50f;
        private float _spitCooldownTimer = 0f;

        [Header("Leader Settings")]
        [SerializeField] private GameObject childAlienPrefab;
        [SerializeField] private float spawnCooldown = 7f;
        [SerializeField] private int maxChildSpawns = 2;
        [SerializeField] private string childPoolKey = "Alien001";

        [Header("Leader Throw Settings")]
        [SerializeField] private bool throwMinionAtPlayer = true;

        private float _hookCooldownTimer = 0f;
        private float _spawnCooldownTimer = 0f;
        private int _currentChildSpawns = 0;

        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private bool _isPullingPlayer = false;
        private float _smartStrafeSign = 1f;

        /// <summary>
        /// True when controlled by RoamingAlienBossManager (50,000 persistent HP, 10s minion waves, 30s warp).
        /// </summary>
        public bool IsRoamingWorldBoss { get; set; } = false;
        public bool IsWarpingOut { get; set; } = false;
        public bool IsVomitingSummon { get; set; } = false;

        public override bool UseSmartObstacleAvoidance => true;

        protected override void Start()
        {
            useSmartObstacleAvoidance = true;
            base.Start();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (alienType == AlienType.Normal)
            {
                string lowerName = gameObject.name.ToLower();
                if (lowerName.Contains("hook")) alienType = AlienType.Hook;
                else if (lowerName.Contains("invisible") || lowerName.Contains("stealth")) alienType = AlienType.Invisible;
                else if (lowerName.Contains("smart")) alienType = AlienType.Smart;
                else if (lowerName.Contains("leader") || lowerName.Contains("boss")) alienType = AlienType.Leader;
                else if (lowerName.Contains("spitter") || lowerName.Contains("acid")) alienType = AlienType.Spitter;
                else if (lowerName.Contains("crawler") || lowerName.Contains("runner")) alienType = AlienType.Crawler;
            }
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            // Define Alien stats
            switch (alienType)
            {
                case AlienType.Hook:
                    moveSpeed = 3.5f;
                    detectionRange = 8f;
                    break;
                case AlienType.Invisible:
                    moveSpeed = 3.5f;
                    detectionRange = 4f;
                    break;
                case AlienType.Smart:
                    moveSpeed = 4f;
                    detectionRange = 9f;
                    break;
                case AlienType.Leader:
                    moveSpeed = IsRoamingWorldBoss ? 3.2f : 2.5f;
                    detectionRange = IsRoamingWorldBoss ? 28f : 7f;
                    break;
                case AlienType.Spitter:
                    moveSpeed = 3.0f;
                    detectionRange = 8f;
                    break;
                case AlienType.Crawler:
                    moveSpeed = 5.0f;
                    detectionRange = 7f;
                    break;
            }
        }

        protected override void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

            // Cooldown timers
            if (_hookCooldownTimer > 0f) _hookCooldownTimer -= Time.fixedDeltaTime;
            if (_spawnCooldownTimer > 0f) _spawnCooldownTimer -= Time.fixedDeltaTime;
            if (_spitCooldownTimer > 0f) _spitCooldownTimer -= Time.fixedDeltaTime;

            // Handle Invisible fade logic
            HandleInvisibilityFade();

            // Handle Leader spawning logic
            HandleLeaderSpawns();

            // Handle player pulling force
            if (_isPullingPlayer && playerTransform != null)
            {
                Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    Vector3 pullDirection = (transform.position - playerTransform.position);
                    pullDirection.y = 0f;
                    pullDirection.Normalize();
                    playerRb.linearVelocity = new Vector3(pullDirection.x * pullForce, playerRb.linearVelocity.y, pullDirection.z * pullForce);
                }
            }

            base.FixedUpdate();
        }

        protected override void UpdateAIBehavior()
        {
            if (IsWarpingOut || IsVomitingSummon)
            {
                if (IsVomitingSummon) currentState = AIState.Telegraph;
                if (rb != null) rb.linearVelocity = Vector3.zero;
                return;
            }

            if (IsTargetDead())
            {
                WanderMovement();
                return;
            }

            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool detected = IsRoamingWorldBoss || IsPlayerInDetectionRange();

            // Force detect at night (Alien always charges player at night)
            if (isNight) detected = true;

            if (detected && playerTransform != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                bool hasLineOfSight = HasLineOfSightToPlayer();

                // Alien Spitter bio-plasma projectile (ยิงเฉพาะเมื่อไม่มีกำแพงหรือสิ่งกีดขวางกั้น)
                if (alienType == AlienType.Spitter && _spitCooldownTimer <= 0f && distanceToPlayer >= 2.5f && distanceToPlayer <= spitRange && hasLineOfSight)
                {
                    StartCoroutine(ExecuteAlienSpit());
                    return;
                }

                // Alien Hook: periodic grapple pull (ดึงเฉพาะเมื่อมีแนวสายตาโล่ง ไม่ติดกำแพง)
                if (alienType == AlienType.Hook && _hookCooldownTimer <= 0f && distanceToPlayer <= hookRange && !_isPullingPlayer && hasLineOfSight)
                {
                    StartCoroutine(ExecuteHookPull());
                    return;
                }

                // ความเร็วเอเลี่ยน: World Boss เดินเต็มสปีดตลอดวัน; เอเลี่ยนปกติกลางคืน 100%, กลางวัน 20%
                float speedMult = (isNight || IsRoamingWorldBoss) ? 1.0f : 0.20f;

                if (alienType == AlienType.Smart)
                {
                    currentState = AIState.Chase;

                    // หากมีตึกหรือสิ่งกีดขวางบัง หรือยังอยู่ไกลเกินระยะเดินวน ให้ใช้ระบบหาเส้นทาง A* เดินอ้อมสิ่งกีดขวางเข้าหาผู้เล่นก่อน
                    if (!hasLineOfSight || distanceToPlayer > 5.5f)
                    {
                        ChasePlayer(speedMult);
                    }
                    else
                    {
                        // Alien Smart: strafe / circle around player with obstacle avoidance
                        Vector3 toPlayer = (playerTransform.position - transform.position);
                        toPlayer.y = 0f;
                        toPlayer.Normalize();

                        Vector3 strafeDirection = new Vector3(-toPlayer.z, 0f, toPlayer.x) * _smartStrafeSign;
                        Vector3 finalDirection = (toPlayer * 0.45f + strafeDirection * 0.55f).normalized;

                        // หากฝั่งที่กำลังเดินวนมีกำแพงขวาง ให้สลับฝั่งเดินวนทันทีเพื่อไม่ให้เดินชนกำแพง
                        if (IsObstacleDirectlyAhead(finalDirection, GetAgentRadius() + 0.85f))
                        {
                            _smartStrafeSign = -_smartStrafeSign;
                            strafeDirection = -strafeDirection;
                            finalDirection = (toPlayer * 0.45f + strafeDirection * 0.55f).normalized;
                        }

                        MoveInSmartDirection(finalDirection, speedMult, toPlayer);
                    }
                }
                else
                {
                    // Normal chasing (uses Smart Obstacle Avoidance & A* Pathfinding)
                    currentState = AIState.Chase;
                    ChasePlayer(speedMult);
                }
            }
            else
            {
                float speedMult = isNight ? 1.0f : 0.20f;
                if (isNight)
                {
                    // At night, keep pursuing player position
                    currentState = AIState.Chase;
                    ChasePlayer(speedMult);
                }
                else
                {
                    WanderMovement(speedMult);
                }
            }
        }

        private Renderer[] _invisRenderers;
        private float _invisCurrentAlpha = 1f;
        private bool _invisTransparentMode;
        private float _forceRevealTimer = 0f;

        /// <summary>
        /// Forces AlienInvisible out of stealth (triggered by Tactical Flashlight, Red Signal Flare, Thermal Visor, or UV Flashbang).
        /// </summary>
        public void ForceRevealFromStealth(float duration)
        {
            _forceRevealTimer = Mathf.Max(_forceRevealTimer, duration);
        }

        private void HandleInvisibilityFade()
        {
            if (alienType != AlienType.Invisible || playerTransform == null) return;

            if (_forceRevealTimer > 0f)
            {
                _forceRevealTimer = Mathf.Max(0f, _forceRevealTimer - Time.fixedDeltaTime);
            }

            bool forcedVisible = _forceRevealTimer > 0f ||
                                 (NightCombatItemManager.Instance != null &&
                                  NightCombatItemManager.Instance.ShouldForceRevealInvisibleAlien(transform.position));

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            float targetAlpha = (forcedVisible || dist <= fadeDistance) ? 1f : 0.10f; // nearly invisible ghost shadow when far
            _invisCurrentAlpha = Mathf.MoveTowards(_invisCurrentAlpha, targetAlpha, Time.fixedDeltaTime * (forcedVisible ? 8.0f : 2.2f));

            if (_spriteRenderer != null)
            {
                Color col = _spriteRenderer.color;
                col.a = _invisCurrentAlpha;
                _spriteRenderer.color = col;
            }

            if (_invisRenderers == null || _invisRenderers.Length == 0)
            {
                _invisRenderers = GetComponentsInChildren<Renderer>(true);
            }

            bool wantTransparent = _invisCurrentAlpha < 0.98f;
            for (int i = 0; i < _invisRenderers.Length; i++)
            {
                Renderer r = _invisRenderers[i];
                if (r == null || r is SpriteRenderer) continue;
                Material[] mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;
                    bool isEye = mat.name.Contains("CyclopsEye") || mat.name.Contains("EyeCore");
                    // Keep the glowing Enderman cyclops eye eerily glinting faintly even while cloaked!
                    float matAlpha = isEye ? Mathf.Lerp(0.45f, 1f, _invisCurrentAlpha) : _invisCurrentAlpha;

                    if (_invisTransparentMode != wantTransparent)
                    {
                        SetUrpMaterialTransparency(mat, wantTransparent);
                    }
                    if (mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor");
                        c.a = matAlpha;
                        mat.SetColor("_BaseColor", c);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.GetColor("_Color");
                        c.a = matAlpha;
                        mat.SetColor("_Color", c);
                    }
                }
            }
            _invisTransparentMode = wantTransparent;
        }

        private static void SetUrpMaterialTransparency(Material mat, bool transparent)
        {
            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = -1;
            }
        }

        private IEnumerator ExecuteHookPull()
        {
            _isPullingPlayer = true;
            _hookCooldownTimer = hookCooldown;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            // Play the extendable giant left-hand Hook stretch animation!
            PlayActionAnimation("Hook", 0.85f, 0.05f);

            Debug.Log($"[AlienAI] {gameObject.name} casted hook on player!");

            // Hold pull for 0.8s
            yield return new WaitForSeconds(0.8f);

            _isPullingPlayer = false;
            currentState = AIState.Chase;
        }

        private void HandleLeaderSpawns()
        {
            // World Boss uses RoamingAlienBossManager's 10s 2-3 alien wave summon system instead
            if (IsRoamingWorldBoss) return;
            if (alienType != AlienType.Leader || _spawnCooldownTimer > 0f) return;
            if (_currentChildSpawns >= maxChildSpawns) return;

            _spawnCooldownTimer = spawnCooldown;

            // Instantiate baby Alien Normal unit nearby
            Vector3 spawnOffset = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            Vector3 spawnPos = transform.position + spawnOffset;

            GameObject babyAlien;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(childPoolKey))
            {
                // Can spawn child from the same pool
                babyAlien = ObjectPoolManager.Instance.SpawnFromPool(childPoolKey, spawnPos, Quaternion.identity);
            }
            else if (childAlienPrefab != null)
            {
                babyAlien = Instantiate(childAlienPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Procedural baby alien fallback
                babyAlien = new GameObject("SpawnedBabyAlien");
                babyAlien.transform.position = spawnPos;
                
                SpriteRenderer sr = babyAlien.AddComponent<SpriteRenderer>();
                sr.sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
                sr.color = Color.green;
                
                SphereCollider col = babyAlien.AddComponent<SphereCollider>();
                babyAlien.AddComponent<AlienAI>();
            }

            if (babyAlien != null)
            {
                // Force spawned alien to be Normal type so it doesn't duplicate leaders
                AlienAI babyAI = babyAlien.GetComponent<AlienAI>();
                if (babyAI != null)
                {
                    babyAI.alienType = AlienType.Normal;
                }

                _currentChildSpawns++;
                // Track child's life to decrement counter when it dies
                Health babyHealth = babyAlien.GetComponent<Health>();
                if (babyHealth != null)
                {
                    babyHealth.onDeath.AddListener(() => _currentChildSpawns = Mathf.Max(0, _currentChildSpawns - 1));
                }

                // Throw mechanic
                if (throwMinionAtPlayer && playerTransform != null)
                {
                    if (babyAI != null) babyAI.enabled = false;
                    Collider col = babyAlien.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    ThrownEntity thrown = babyAlien.AddComponent<ThrownEntity>();
                    thrown.Setup(playerTransform.position, 1.2f, 4f, () => 
                    {
                        if (babyAI != null) babyAI.enabled = true;
                        if (col != null) col.enabled = true;
                    });
                }
            }
        }
    
        private IEnumerator ExecuteAlienSpit()
        {
            _spitCooldownTimer = spitCooldown;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            if (_spriteRenderer != null) _spriteRenderer.color = new Color(0.8f, 0.2f, 1f); // Neon purple

            yield return new WaitForSeconds(0.35f);

            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            if (playerTransform != null && (health == null || !health.IsDead))
            {
                Vector3 dir = (playerTransform.position - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
                dir.Normalize();
                transform.forward = dir;

                Vector3 spawnPos = transform.position + dir * 0.85f + Vector3.up * 0.80f;
                SpitterProjectileVisual.CreateSpitProjectile(
                    SpitProjectileStyle.AlienPlasma,
                    spawnPos,
                    dir,
                    spitProjectileSpeed,
                    gameObject,
                    1.0f);
                TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                    this,
                    TheLastEmpire.Network.NetworkEnemyBridge.TelegraphAlienSpit,
                    spawnPos,
                    dir,
                    1.0f,
                    spitProjectileSpeed);
            }

            currentState = AIState.Chase;
        }
}
}
