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

        protected override void Start()
        {
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
                    moveSpeed = 2.5f;
                    detectionRange = 7f;
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
            if (IsTargetDead())
            {
                WanderMovement();
                return;
            }

            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool detected = IsPlayerInDetectionRange();

            // Force detect at night (Alien always charges player at night)
            if (isNight) detected = true;

            if (detected && playerTransform != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

                // Alien Hook: periodic grapple pull
                // Alien Spitter bio-plasma projectile
                if (alienType == AlienType.Spitter && _spitCooldownTimer <= 0f && distanceToPlayer >= 2.5f && distanceToPlayer <= spitRange)
                {
                    StartCoroutine(ExecuteAlienSpit());
                    return;
                }

                if (alienType == AlienType.Hook && _hookCooldownTimer <= 0f && distanceToPlayer <= hookRange && !_isPullingPlayer)
                {
                    StartCoroutine(ExecuteHookPull());
                    return;
                }

                if (alienType == AlienType.Smart)
                {
                    // Alien Smart: strafe / circle around player
                    currentState = AIState.Chase;
                    Vector3 toPlayer = (playerTransform.position - transform.position);
                    toPlayer.y = 0f;
                    toPlayer.Normalize();
                    // Circle vector (perpendicular to player direction on X/Z plane)
                    Vector3 strafeDirection = new Vector3(-toPlayer.z, 0f, toPlayer.x);
                    
                    // Blend walking towards player and walking sideways
                    Vector3 finalDirection = (toPlayer * 0.4f + strafeDirection * 0.6f).normalized;
                    rb.linearVelocity = new Vector3(finalDirection.x * moveSpeed, rb.linearVelocity.y, finalDirection.z * moveSpeed);

                    if (toPlayer.sqrMagnitude > 0.01f)
                    {
                        transform.forward = toPlayer;
                    }
                }
                else
                {
                    // Normal chasing
                    currentState = AIState.Chase;
                    ChasePlayer(isNight ? 1.2f : 1f);
                }
            }
            else
            {
                if (isNight)
                {
                    // At night, keep pursuing player position
                    currentState = AIState.Chase;
                    ChasePlayer(1.2f);
                }
                else
                {
                    WanderMovement();
                }
            }
        }

        private void HandleInvisibilityFade()
        {
            if (alienType != AlienType.Invisible || _spriteRenderer == null) return;

            if (playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);
                float targetAlpha = dist <= fadeDistance ? 1f : 0.08f; // nearly invisible if far
                
                Color col = _spriteRenderer.color;
                col.a = Mathf.MoveTowards(col.a, targetAlpha, Time.fixedDeltaTime * 2f);
                _spriteRenderer.color = col;
            }
        }

        private IEnumerator ExecuteHookPull()
        {
            _isPullingPlayer = true;
            _hookCooldownTimer = hookCooldown;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            Debug.Log($"[AlienAI] {gameObject.name} casted hook on player!");

            // Hold pull for 0.8s
            yield return new WaitForSeconds(0.8f);

            _isPullingPlayer = false;
            currentState = AIState.Chase;
        }

        private void HandleLeaderSpawns()
        {
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

            if (playerTransform != null)
            {
                Vector3 dir = (playerTransform.position - transform.position);
                dir.y = 0f;
                dir.Normalize();

                Vector3 spawnPos = transform.position + dir * 0.6f + Vector3.up * 0.3f;
                GameObject spit = new GameObject("AlienBioPlasma");
                spit.transform.position = spawnPos;
                spit.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

                SpriteRenderer sr = spit.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                sr.color = new Color(0.85f, 0.1f, 0.95f); // magenta / violet plasma

                SphereCollider col = spit.AddComponent<SphereCollider>();
                col.isTrigger = true;

                Projectile proj = spit.AddComponent<Projectile>();
                proj.Setup(dir, playerTransform.position, gameObject);
            }

            currentState = AIState.Chase;
        }
}
}
