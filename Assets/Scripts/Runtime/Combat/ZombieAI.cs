using UnityEngine;

namespace TheLastEmpire
{
    public class ZombieAI : BaseEnemyAI
    {
        public enum ZombieType
        {
            Normal,
            Dog,
            Boomer,
            Leader,
            Runner
        }

        [Header("Zombie Settings")]
        [SerializeField] private ZombieType zombieType = ZombieType.Normal;

        [Header("Dog / Leap Settings")]
        [SerializeField] private float leapCooldown = 4f;
        [SerializeField] private float leapSpeed = 12f;
        [SerializeField] private float leapDuration = 0.3f;

        [Header("Boomer / Explosion Settings")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private int explosionDamage = 45;
        [SerializeField] private float fuseDuration = 0.8f;
        [SerializeField] private string explosionVfxPoolKey = "EnemyExplosion";
        [Tooltip("ตัวคูณขนาดของ VFX ระเบิด")]
        [SerializeField] private float explosionVfxScaleMultiplier = 1.0f;

        [Header("Leader Settings")]
        [SerializeField] private float buffRadius = 6f;
        [SerializeField] private GameObject childZombiePrefab;
        [SerializeField] private float spawnCooldown = 10f;
        [SerializeField] private int maxChildSpawns = 3;
        [SerializeField] private string childPoolKey = "Zombie001";

        [Header("Leader Throw Settings")]
        [SerializeField] private bool throwMinionAtPlayer = true;

        [Header("Boss / Leader Charge Settings")]
        [SerializeField] private float chargeCooldown = 6f;
        [SerializeField] private float chargeSpeed = 12f;
        [SerializeField] private float chargeDuration = 0.5f;
        [SerializeField] private float chargeWindupDuration = 0.3f;
        [SerializeField] private float minChargeDistance = 3.5f;
        [SerializeField] private float maxChargeDistance = 10f;
        [SerializeField] private int chargeContactDamage = 20;
        [SerializeField] private float chargeContactKnockback = 8f;

        [Header("Boss / Leader Stomp AOE Settings")]
        [SerializeField] private float stompAttackRange = 3.2f;
        [SerializeField] private float stompRadius = 3.0f;
        [SerializeField] private int stompDamage = 40;
        [SerializeField] private float stompTelegraphDuration = 1.0f;
        [SerializeField] private float stompCooldown = 5.0f;
        [SerializeField] private float stompKnockbackForce = 12f;
        [SerializeField] private string stompVfxPoolKey = "EnemyExplosion";
        [Tooltip("Custom sprite for Boss AOE warning circle (optional)")]
        [SerializeField] private Sprite stompTelegraphSprite;
        [SerializeField] private Color stompTelegraphColor = new Color(1f, 0.15f, 0.15f, 0.45f);

        private float _spawnCooldownTimer = 0f;
        private int _currentChildSpawns = 0;

        private float _leapCooldownTimer = 0f;
        private float _leapActiveTimer = 0f;
        private bool _isLeaping = false;
        private Vector3 _leapDirection;

        private bool _isExploding = false;
        private float _fuseTimer = 0f;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        private float _leaderBuffCheckTimer = 0f;
        private bool _hasLeaderBuff = false;

        // Boss Charge State
        private float _chargeCooldownTimer = 0f;
        private float _chargeWindupTimer = 0f;
        private float _chargeActiveTimer = 0f;
        private bool _isCharging = false;
        private bool _isChargeWindingUp = false;
        private Vector3 _chargeDirection;

        // Boss Stomp AOE State
        private float _stompCooldownTimer = 0f;
        private float _stompTelegraphTimer = 0f;
        private bool _isStompTelegraphing = false;
        private LineRenderer _stompCircleRenderer;
        private SpriteRenderer _stompSpriteRenderer;

        public bool IsBossUnit => (zombieType == ZombieType.Leader || (TryGetComponent<LootDropper>(out var ld) && ld.IsBoss));

        protected override void OnEnable()
        {
            base.OnEnable();
            HideBossTelegraphVisual();
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            _chargeCooldownTimer = 2.0f;
            _stompCooldownTimer = 2.0f;
            if (_spriteRenderer != null && _originalColor != default)
            {
                _spriteRenderer.color = _originalColor;
            }
        }

        protected override void Start()
        {
            base.Start();

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            if (IsBossUnit)
            {
                InitBossIndicators();
            }

            // Auto-correct ZombieType based on prefab/GameObject name if left as default/Normal
            if (zombieType == ZombieType.Normal)
            {
                string lowerName = gameObject.name.ToLower();
                if (lowerName.Contains("boomer"))
                {
                    zombieType = ZombieType.Boomer;
                    Debug.Log($"[ZombieAI] Auto-detected type as Boomer from GameObject name: {gameObject.name}");
                }
                else if (lowerName.Contains("leader"))
                {
                    zombieType = ZombieType.Leader;
                    Debug.Log($"[ZombieAI] Auto-detected type as Leader from GameObject name: {gameObject.name}");
                }
                else if (lowerName.Contains("dog") || lowerName.Contains("leap"))
                {
                    zombieType = ZombieType.Dog;
                    Debug.Log($"[ZombieAI] Auto-detected type as Dog from GameObject name: {gameObject.name}");
                }
                else if (lowerName.Contains("runner") || lowerName.Contains("fast"))
                {
                    zombieType = ZombieType.Runner;
                    Debug.Log($"[ZombieAI] Auto-detected type as Runner from GameObject name: {gameObject.name}");
                }
            }

            // Define stats depending on ZombieType
            switch (zombieType)
            {
                case ZombieType.Dog:
                    moveSpeed = 4.5f;
                    detectionRange = 7f;
                    break;
                case ZombieType.Runner:
                    moveSpeed = 4f;
                    break;
                case ZombieType.Leader:
                    moveSpeed = 2.5f;
                    detectionRange = 6f;
                    break;
                case ZombieType.Boomer:
                    moveSpeed = 2f;
                    break;
            }
        }

        protected override void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

            // Handle dog leap countdown timers
            if (_leapCooldownTimer > 0f) _leapCooldownTimer -= Time.fixedDeltaTime;
            
            if (_isLeaping)
            {
                _leapActiveTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = _leapDirection * leapSpeed;
                if (_leapActiveTimer <= 0f)
                {
                    _isLeaping = false;
                    currentState = AIState.Chase;
                }
                return;
            }

            // Handle Boomer Fuse Countdown
            if (_isExploding)
            {
                rb.linearVelocity = Vector3.zero;
                _fuseTimer -= Time.fixedDeltaTime;

                // Flash red color visually to indicate fuse explosion warning
                if (_spriteRenderer != null)
                {
                    float flashFreq = Mathf.PingPong(Time.time * 10f, 1f);
                    _spriteRenderer.color = Color.Lerp(_originalColor, Color.red, flashFreq);
                }

                if (_fuseTimer <= 0f)
                {
                    ExplodeAndDie();
                }
                return;
            }

            // Handle Boss / Leader Stomp AOE Telegraph (1.0 second visual warning before impact)
            if (_stompCooldownTimer > 0f) _stompCooldownTimer -= Time.fixedDeltaTime;
            if (_isStompTelegraphing)
            {
                rb.linearVelocity = Vector3.zero;
                _stompTelegraphTimer -= Time.fixedDeltaTime;

                UpdateBossTelegraphVisual(stompRadius);

                // Flash boss sprite red/orange during telegraph windup
                if (_spriteRenderer != null)
                {
                    float flashFreq = Mathf.PingPong(Time.time * 12f, 1f);
                    _spriteRenderer.color = Color.Lerp(_originalColor, new Color(1f, 0.25f, 0.1f), flashFreq);
                }

                if (_stompTelegraphTimer <= 0f)
                {
                    PerformGroundStomp();
                }
                return;
            }

            // Handle Boss / Leader Charge
            if (_chargeCooldownTimer > 0f) _chargeCooldownTimer -= Time.fixedDeltaTime;
            if (_isChargeWindingUp)
            {
                rb.linearVelocity = Vector3.zero;
                _chargeWindupTimer -= Time.fixedDeltaTime;

                if (_spriteRenderer != null)
                {
                    float flashFreq = Mathf.PingPong(Time.time * 15f, 1f);
                    _spriteRenderer.color = Color.Lerp(_originalColor, Color.yellow, flashFreq);
                }

                if (_chargeWindupTimer <= 0f)
                {
                    _isChargeWindingUp = false;
                    _isCharging = true;
                    _chargeActiveTimer = chargeDuration;
                }
                return;
            }

            if (_isCharging)
            {
                _chargeActiveTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = _chargeDirection * chargeSpeed;

                if (_chargeDirection.sqrMagnitude > 0.01f)
                {
                    transform.forward = _chargeDirection;
                }

                // Check contact damage with player during rush
                if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= 1.3f)
                {
                    IDamageable damageable = playerTransform.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(chargeContactDamage);
                    }

                    PlayerController pc = playerTransform.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.ApplyKnockback(_chargeDirection * chargeContactKnockback, 0.25f);
                    }

                    // Impact landed, conclude dash
                    _isCharging = false;
                    _chargeCooldownTimer = chargeCooldown;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Chase;
                    return;
                }

                if (_chargeActiveTimer <= 0f)
                {
                    _isCharging = false;
                    _chargeCooldownTimer = chargeCooldown;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Chase;
                }
                return;
            }

            // Apply leader speed buff if Leader Zombie is nearby
            ApplyLeaderBuffChecks();

            // Handle Leader minion spawning
            if (zombieType == ZombieType.Leader && health != null && !health.IsDead)
            {
                if (_spawnCooldownTimer > 0f) _spawnCooldownTimer -= Time.fixedDeltaTime;
                HandleLeaderSpawning();
            }

            base.FixedUpdate();
        }

        protected override void UpdateAIBehavior()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool detected = IsPlayerInDetectionRange();

            float finalMoveSpeed = moveSpeed * (_hasLeaderBuff ? 1.3f : 1f);

            if (detected)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

                // Boomer triggers explosion when very close to player
                if (zombieType == ZombieType.Boomer && distanceToPlayer <= 2f && !_isExploding)
                {
                    TriggerBoomerFuse();
                    return;
                }

                // Dog periodically leaps towards player
                if (zombieType == ZombieType.Dog && _leapCooldownTimer <= 0f && distanceToPlayer <= 4f && !_isLeaping)
                {
                    StartDogLeap();
                    return;
                }

                // Boss / Leader Special Attacks
                if (IsBossUnit)
                {
                    // 1. Ground Stomp AOE: If player is in attack range (<= 3.2m) and cooldown is ready
                    if (_stompCooldownTimer <= 0f && distanceToPlayer <= stompAttackRange && !_isStompTelegraphing && !_isCharging && !_isChargeWindingUp)
                    {
                        StartStompTelegraph();
                        return;
                    }

                    // 2. Boss Charge: If player is at medium-long distance (3.5m - 10m) and cooldown is ready
                    if (_chargeCooldownTimer <= 0f && distanceToPlayer >= minChargeDistance && distanceToPlayer <= maxChargeDistance && !_isCharging && !_isChargeWindingUp && !_isStompTelegraphing)
                    {
                        StartBossCharge();
                        return;
                    }
                }

                currentState = AIState.Chase;
                ChasePlayer(_hasLeaderBuff ? 1.3f : 1f);
            }
            else
            {
                if (isNight)
                {
                    // Zombie will not wander during the night (stands still / idle)
                    currentState = AIState.Idle;
                    rb.linearVelocity = Vector3.zero;
                }
                else
                {
                    WanderMovement();
                }
            }
        }

        private void StartDogLeap()
        {
            _isLeaping = true;
            _leapCooldownTimer = leapCooldown;
            _leapActiveTimer = leapDuration;
            currentState = AIState.Telegraph;
            
            if (playerTransform != null)
            {
                _leapDirection = (playerTransform.position - transform.position);
                _leapDirection.y = 0f;
                _leapDirection.Normalize();
            }
            else
            {
                _leapDirection = transform.forward;
            }
        }

        private void TriggerBoomerFuse()
        {
            _isExploding = true;
            _fuseTimer = fuseDuration;
            currentState = AIState.Telegraph;
        }

        private void ExplodeAndDie()
        {
            Debug.Log($"[ZombieAI] Boomer exploded at {transform.position}!");

            // 💥 เสกเอฟเฟกต์ระเบิด
            if (!string.IsNullOrEmpty(explosionVfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                GameObject explosionVFX = ObjectPoolManager.Instance.SpawnFromPool(explosionVfxPoolKey, transform.position, Quaternion.identity);
                if (explosionVFX != null)
                {
                    PooledParticle pooledParticle = explosionVFX.GetComponent<PooledParticle>();
                    if (pooledParticle != null)
                    {
                        // อัปเดตชื่อ Pool Key ป้องกันลืมทางกลับ Pool
                        pooledParticle.SetPoolKey(explosionVfxPoolKey);

                        // ถ้าอยากให้ขนาดระเบิดสอดคล้องกับรัศมีดาเมจ ก็จับคูณได้เลย
                        pooledParticle.ApplyScaleMultiplier(explosionRadius);
                    }
                    else
                    {
                        // Fallback กรณีไม่ได้แปะสคริปต์
                        explosionVFX.transform.localScale = Vector3.one * explosionRadius;
                    }
                }
            }

            // Perform circular overlap check for damage targets
            Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var target in targets)
            {
                // Boomer explosion damages player and obstacles (ignores other enemies)
                if (target.CompareTag("Enemy") && target.gameObject != gameObject) continue;

                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(explosionDamage);
                }
            }

            // Kill ourselves cleanly
            _isExploding = false;
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            base.HandleDeath();
        }

        private void ApplyLeaderBuffChecks()
        {
            _leaderBuffCheckTimer -= Time.fixedDeltaTime;
            if (_leaderBuffCheckTimer > 0f) return;
            _leaderBuffCheckTimer = 0.5f; // check every 0.5s to preserve performance

            if (zombieType == ZombieType.Leader) return; // Leaders cannot buff themselves

            // Find if any Zombie Leaders are close
            ZombieAI[] allZombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            _hasLeaderBuff = false;

            foreach (var zombie in allZombies)
            {
                if (zombie != null && zombie.zombieType == ZombieType.Leader && zombie != this)
                {
                    float dist = Vector3.Distance(transform.position, zombie.transform.position);
                    if (dist <= buffRadius)
                    {
                        _hasLeaderBuff = true;
                        break;
                    }
                }
            }
        }

        protected override void OnDamageTaken(float damageAmount)
        {
            if (health != null && health.IsDead) return;

            // Boss units are immune to stagger and interruption during Stomp Telegraph or Charge!
            if (IsBossUnit && (_isStompTelegraphing || _isCharging || _isChargeWindingUp))
            {
                hasBeenAttacked = true;
                return;
            }

            base.OnDamageTaken(damageAmount);
        }

        protected void OnDisable()
        {
            HideBossTelegraphVisual();
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            if (_spriteRenderer != null && _originalColor != default)
            {
                _spriteRenderer.color = _originalColor;
            }
        }

        protected override void HandleDeath()
        {
            HideBossTelegraphVisual();
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            if (_spriteRenderer != null && _originalColor != default)
            {
                _spriteRenderer.color = _originalColor;
            }

            // If Boomer dies from bullets/damage before exploding, it triggers a chain explosion!
            if (zombieType == ZombieType.Boomer && !_isExploding)
            {
                TriggerBoomerFuse();
                _fuseTimer = 0f; // explodes instantly!
                ExplodeAndDie();
                return;
            }

            base.HandleDeath();
        }

        protected override void OnDestroy()
        {
            HideBossTelegraphVisual();
            base.OnDestroy();
        }

        private void StartStompTelegraph()
        {
            _isStompTelegraphing = true;
            _stompTelegraphTimer = stompTelegraphDuration;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            UpdateBossTelegraphVisual(stompRadius);
        }

        private void PerformGroundStomp()
        {
            _isStompTelegraphing = false;
            _stompCooldownTimer = stompCooldown;

            HideBossTelegraphVisual();
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            Debug.Log($"[ZombieAI] Boss performed Ground Stomp AOE with radius {stompRadius}m!");

            // 1. Spawn ground slam / explosion VFX
            if (!string.IsNullOrEmpty(stompVfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                GameObject stompVFX = ObjectPoolManager.Instance.SpawnFromPool(stompVfxPoolKey.Trim(), transform.position, Quaternion.identity);
                if (stompVFX != null)
                {
                    PooledParticle pooledParticle = stompVFX.GetComponent<PooledParticle>();
                    if (pooledParticle != null)
                    {
                        pooledParticle.SetPoolKey(stompVfxPoolKey.Trim());
                        pooledParticle.ApplyScaleMultiplier(stompRadius);
                    }
                    else
                    {
                        stompVFX.transform.localScale = Vector3.one * stompRadius;
                    }
                }
            }

            // 2. Camera Shake
            CameraFollow.TriggerShake(0.3f, 0.45f);

            // 3. OverlapSphere AOE Damage & Knockback within 3 meters
            Collider[] targets = Physics.OverlapSphere(transform.position, stompRadius);
            foreach (var target in targets)
            {
                if (target.gameObject == gameObject) continue;
                if (target.CompareTag("Enemy")) continue; // Don't hurt minion zombies

                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(stompDamage);
                }

                // Knockback player away from boss
                PlayerController pc = target.GetComponent<PlayerController>();
                if (pc != null)
                {
                    Vector3 knockDir = (pc.transform.position - transform.position);
                    knockDir.y = 0f;
                    if (knockDir.sqrMagnitude < 0.001f) knockDir = transform.forward;
                    knockDir.Normalize();
                    pc.ApplyKnockback(knockDir * stompKnockbackForce, 0.35f);
                }
            }

            currentState = AIState.Chase;
        }

        private void StartBossCharge()
        {
            _isChargeWindingUp = true;
            _chargeWindupTimer = chargeWindupDuration;
            _chargeCooldownTimer = chargeCooldown;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            if (playerTransform != null)
            {
                _chargeDirection = (playerTransform.position - transform.position);
                _chargeDirection.y = 0f;
                _chargeDirection.Normalize();
            }
            else
            {
                _chargeDirection = transform.forward;
            }

            if (_chargeDirection.sqrMagnitude > 0.01f)
            {
                transform.forward = _chargeDirection;
            }
        }

        private void InitBossIndicators()
        {
            if (_stompCircleRenderer != null) return;

            // 1. LineRenderer Warning Ring (crisp 3m boundary)
            GameObject circleObj = new GameObject("BossAOECircle");
            circleObj.transform.SetParent(transform);
            circleObj.transform.localPosition = Vector3.zero;

            _stompCircleRenderer = circleObj.AddComponent<LineRenderer>();
            _stompCircleRenderer.startWidth = 0.08f;
            _stompCircleRenderer.endWidth = 0.08f;
            _stompCircleRenderer.useWorldSpace = true;
            _stompCircleRenderer.loop = true;
            _stompCircleRenderer.positionCount = 48;

            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            _stompCircleRenderer.material = lineMat;
            _stompCircleRenderer.startColor = new Color(1f, 0.2f, 0.1f, 0.9f);
            _stompCircleRenderer.endColor = new Color(1f, 0.2f, 0.1f, 0.9f);
            circleObj.SetActive(false);

            // 2. Ground Decal Disc (tinted danger zone)
            GameObject discObj = new GameObject("BossAOEDisc");
            discObj.transform.SetParent(transform);
            discObj.transform.localPosition = Vector3.zero;
            discObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _stompSpriteRenderer = discObj.AddComponent<SpriteRenderer>();
            _stompSpriteRenderer.sprite = stompTelegraphSprite;
            _stompSpriteRenderer.color = stompTelegraphColor;

            if (_stompSpriteRenderer.sprite == null)
            {
                // Procedural soft circular texture fallback
                int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                float radius = size * 0.5f;
                Color[] colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                        if (dist <= 1f)
                        {
                            float alpha = Mathf.Clamp01(1f - dist * dist);
                            colors[y * size + x] = new Color(1f, 0.1f, 0.1f, alpha);
                        }
                        else
                        {
                            colors[y * size + x] = Color.clear;
                        }
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                _stompSpriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            }

            discObj.SetActive(false);
        }

        private void UpdateBossTelegraphVisual(float currentRadius)
        {
            if (_stompCircleRenderer == null && IsBossUnit)
            {
                InitBossIndicators();
            }

            Vector3 center = transform.position;

            if (_stompCircleRenderer != null)
            {
                if (!_stompCircleRenderer.gameObject.activeSelf) _stompCircleRenderer.gameObject.SetActive(true);
                int segments = _stompCircleRenderer.positionCount;
                float angle = 0f;
                for (int i = 0; i < segments; i++)
                {
                    float x = Mathf.Sin(Mathf.Deg2Rad * angle) * currentRadius;
                    float z = Mathf.Cos(Mathf.Deg2Rad * angle) * currentRadius;
                    _stompCircleRenderer.SetPosition(i, new Vector3(center.x + x, 0.05f, center.z + z));
                    angle += (360f / segments);
                }

                // Pulsate line brightness
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 15f);
                Color c = new Color(1f, 0.15f, 0.1f, pulse);
                _stompCircleRenderer.startColor = c;
                _stompCircleRenderer.endColor = c;
            }

            if (_stompSpriteRenderer != null)
            {
                if (!_stompSpriteRenderer.gameObject.activeSelf) _stompSpriteRenderer.gameObject.SetActive(true);
                _stompSpriteRenderer.transform.position = new Vector3(center.x, 0.04f, center.z);
                _stompSpriteRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                float baseWidth = _stompSpriteRenderer.sprite != null ? _stompSpriteRenderer.sprite.bounds.size.x : 1f;
                if (baseWidth > 0f)
                {
                    float scale = (currentRadius * 2f) / baseWidth;
                    _stompSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                }

                // Pulsate fill opacity
                float pulseAlpha = Mathf.Lerp(0.3f, 0.6f, Mathf.PingPong(Time.time * 6f, 1f));
                Color sc = stompTelegraphColor;
                sc.a = pulseAlpha;
                _stompSpriteRenderer.color = sc;
            }
        }

        private void HideBossTelegraphVisual()
        {
            if (_stompCircleRenderer != null && _stompCircleRenderer.gameObject.activeSelf)
            {
                _stompCircleRenderer.gameObject.SetActive(false);
            }
            if (_stompSpriteRenderer != null && _stompSpriteRenderer.gameObject.activeSelf)
            {
                _stompSpriteRenderer.gameObject.SetActive(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (zombieType == ZombieType.Boomer)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, explosionRadius);
            }
            if (zombieType == ZombieType.Leader)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, buffRadius);

                // Boss Stomp AOE radius gizmo
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, stompRadius);
            }
        }

        private void HandleLeaderSpawning()
        {
            if (_spawnCooldownTimer > 0f || _currentChildSpawns >= maxChildSpawns) return;
            if (!IsPlayerInDetectionRange()) return; // only spawn when player is near!

            _spawnCooldownTimer = spawnCooldown;

            Vector3 spawnOffset = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            Vector3 spawnPos = transform.position + spawnOffset;

            GameObject babyZombie = null;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(childPoolKey))
            {
                babyZombie = ObjectPoolManager.Instance.SpawnFromPool(childPoolKey.Trim(), spawnPos, Quaternion.identity);
            }
            
            if (babyZombie == null && childZombiePrefab != null)
            {
                babyZombie = Instantiate(childZombiePrefab, spawnPos, Quaternion.identity);
            }
            
            if (babyZombie == null)
            {
                // Procedural fallback
                babyZombie = new GameObject("SpawnedZombieNormal");
                babyZombie.transform.position = spawnPos;

                SpriteRenderer sr = babyZombie.AddComponent<SpriteRenderer>();
                sr.sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
                sr.color = Color.white;

                SphereCollider col = babyZombie.AddComponent<SphereCollider>();
                
                // Set tag and setup component
                babyZombie.tag = "Enemy";
                babyZombie.AddComponent<ZombieAI>();
            }

            if (babyZombie != null)
            {
                // Force spawned zombie to be a Normal type so it doesn't spawn more leaders!
                ZombieAI babyAI = babyZombie.GetComponent<ZombieAI>();
                if (babyAI != null)
                {
                    babyAI.zombieType = ZombieType.Normal;
                }

                _currentChildSpawns++;

                // Track child death to clear counter slot
                Health babyHealth = babyZombie.GetComponent<Health>();
                if (babyHealth != null)
                {
                    babyHealth.onDeath.AddListener(() => _currentChildSpawns = Mathf.Max(0, _currentChildSpawns - 1));
                }

                // Throw mechanic
                if (throwMinionAtPlayer && playerTransform != null)
                {
                    if (babyAI != null) babyAI.enabled = false;
                    Collider col = babyZombie.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    ThrownEntity thrown = babyZombie.AddComponent<ThrownEntity>();
                    thrown.Setup(playerTransform.position, 1.2f, 4f, () => 
                    {
                        if (babyAI != null) babyAI.enabled = true;
                        if (col != null) col.enabled = true;
                    });
                }
            }
        }
    }
}
