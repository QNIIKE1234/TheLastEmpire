using System.Collections.Generic;
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

        [System.Serializable]
        public struct MinionSpawnOption
        {
            [Tooltip("Pool Key ใน ObjectPoolManager (เช่น Zombie001, ZombieDog, ZombieRunner, ZombieBoomer)")]
            public string poolKey;
            [Tooltip("น้ำหนักโอกาสเกิด (ค่ายิ่งเยอะ ยิ่งมีโอกาสสุ่มได้สูง เช่น 50, 30, 20)")]
            [Range(1, 100)] public int weight;
        }

        [Header("Leader Settings")]
        [SerializeField] private float buffRadius = 6f;
        [SerializeField] private float spawnCooldown = 10f;
        [SerializeField] private int maxChildSpawns = 3;

        [Header("Leader Minion Spawn Pool (สุ่มเรียกลูกน้องหลายประเภท)")]
        [Tooltip("รายการซอมบี้ลูกน้องที่ต้องการให้สุ่มเสก หากมีข้อมูลในนี้ บอสจะสุ่มเรียกลูกน้องตามโอกาสที่กำหนด")]
        [SerializeField] private List<MinionSpawnOption> minionSpawnPool = new List<MinionSpawnOption>();

        [Header("Fallback Single Minion (ใช้กรณีไม่ได้ใส่ข้อมูลใน List ด้านบน)")]
        [SerializeField] private GameObject childZombiePrefab;
        [SerializeField] private string childPoolKey = "Zombie001";

        [Header("Leader Throw Settings")]
        [SerializeField] private bool throwMinionAtPlayer = true;

        [Header("Boss / Leader Charge Settings")]
        [SerializeField] private float chargeCooldown = 6f;
        [SerializeField] private float chargeSpeed = 22f;
        [SerializeField] private float chargeDuration = 0.45f;
        [SerializeField] private float chargeWindupDuration = 0.65f;
        [SerializeField] private float minChargeDistance = 5.0f;
        [SerializeField] private float maxChargeDistance = 12.5f;
        [SerializeField] private int chargeContactDamage = 25;
        [SerializeField] private float chargeContactKnockback = 10f;
        [SerializeField] private float chargePathWidth = 1.8f;
        [SerializeField] private Color chargeTelegraphColor = new Color(1f, 0.15f, 0.15f, 0.45f);

        [Header("Boss / Leader Stomp AOE Settings")]
        [SerializeField] private float stompAttackRange = 4.8f;
        [SerializeField] private float stompRadius = 4.5f;
        [SerializeField] private int stompDamage = 45;
        [SerializeField] private float stompTelegraphDuration = 1.0f;
        [SerializeField] private float stompCooldown = 5.0f;
        [SerializeField] private float stompKnockbackForce = 13f;
        [SerializeField] private string stompVfxPoolKey = "EnemyExplosion";
        [Tooltip("Custom sprite for Boss AOE warning circle (optional)")]
        [SerializeField] private Sprite stompTelegraphSprite;
        [SerializeField] private Color stompTelegraphColor = new Color(1f, 0.15f, 0.15f, 0.45f);

        [Header("Boss Rage Mode Settings")]
        [Tooltip("เกณฑ์พลังชีวิตที่จะเปิดใช้งาน Rage Mode (0.30 = เลือดต่ำกว่า 30%)")]
        [SerializeField] private float rageHealthThreshold = 0.30f;
        [Tooltip("ตัวคูณความเร็ววิ่งเมื่อเข้าสู่ Rage Mode (2.0 = วิ่งเร็วขึ้น 2 เท่า)")]
        [SerializeField] private float rageSpeedMultiplier = 2.0f;
        [Tooltip("ตัวคูณคูลดาวน์สกิล (0.5 = สกิลวนมาใช้ไวขึ้น 2 เท่า)")]
        [SerializeField] private float rageSkillCooldownMultiplier = 0.5f;
        [Tooltip("ตัวคูณเวลาชาร์จก่อนปล่อยสกิล (0.7 = ชาร์จไวขึ้น 30%)")]
        [SerializeField] private float rageTelegraphSpeedMultiplier = 0.7f;
        [Tooltip("ตัวคูณความเร็วในการพุ่งกระแทก Charge")]
        [SerializeField] private float rageChargeSpeedMultiplier = 1.35f;
        [Tooltip("แสงออร่าสีแดงเมื่ออยู่ในโหมดคลั่ง")]
        [SerializeField] private Color rageTint = new Color(1f, 0.22f, 0.15f, 1f);

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
        private Renderer[] _allMeshRenderers;
        private readonly Dictionary<Renderer, Color> _originalRendererColors = new Dictionary<Renderer, Color>();

        private float _leaderBuffCheckTimer = 0f;
        private bool _hasLeaderBuff = false;
        private bool _hasTriggeredRageMode = false;

        // Boss Charge State
        private float _chargeCooldownTimer = 0f;
        private float _chargeWindupTimer = 0f;
        private float _chargeActiveTimer = 0f;
        private bool _isCharging = false;
        private bool _isChargeWindingUp = false;
        private Vector3 _chargeDirection;
        private LineRenderer _chargePathLineRenderer;
        private SpriteRenderer _chargePathSpriteRenderer;

        // Boss Stomp AOE State
        private float _stompCooldownTimer = 0f;
        private float _stompTelegraphTimer = 0f;
        private bool _isStompTelegraphing = false;
        private LineRenderer _stompCircleRenderer;
        private SpriteRenderer _stompSpriteRenderer;

        public bool IsBossUnit => (zombieType == ZombieType.Leader || (TryGetComponent<LootDropper>(out var ld) && ld.IsBoss));
        public bool IsRageMode => IsBossUnit && health != null && !health.IsDead && health.MaxHealth > 0f && ((health.CurrentHealth / health.MaxHealth) <= rageHealthThreshold);

        protected override void OnEnable()
        {
            base.OnEnable();
            HideBossTelegraphVisual();
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            _hasTriggeredRageMode = false;
            ResetRageVisuals();
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

            _allMeshRenderers = GetComponentsInChildren<Renderer>(true);
            if (_allMeshRenderers != null)
            {
                foreach (var r in _allMeshRenderers)
                {
                    if (r == null || r is LineRenderer) continue;
                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                    {
                        _originalRendererColors[r] = r.sharedMaterial.color;
                    }
                }
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

            // Check Boss Rage Mode (HP <= 30%)
            if (IsBossUnit)
            {
                CheckRageModeState();
            }

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
                if (IsTargetDead())
                {
                    _isExploding = false;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Wander;
                    return;
                }

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

            // Cooldown rate multiplier (Skills tick down faster during Rage Mode!)
            float cooldownSpeedMult = IsRageMode ? (1f / Mathf.Max(0.1f, rageSkillCooldownMultiplier)) : 1f;

            // Handle Boss / Leader Stomp AOE Telegraph
            if (_stompCooldownTimer > 0f) _stompCooldownTimer -= Time.fixedDeltaTime * cooldownSpeedMult;
            if (_isStompTelegraphing)
            {
                if (IsTargetDead())
                {
                    _isStompTelegraphing = false;
                    HideBossTelegraphVisual();
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Wander;
                    return;
                }

                rb.linearVelocity = Vector3.zero;
                _stompTelegraphTimer -= Time.fixedDeltaTime;

                UpdateBossTelegraphVisual(stompRadius);

                // Flash boss sprite red/orange during telegraph windup (faster pulse in Rage Mode)
                if (_spriteRenderer != null)
                {
                    float flashFreq = Mathf.PingPong(Time.time * (IsRageMode ? 18f : 12f), 1f);
                    _spriteRenderer.color = Color.Lerp(_originalColor, new Color(1f, 0.25f, 0.1f), flashFreq);
                }

                if (_stompTelegraphTimer <= 0f)
                {
                    PerformGroundStomp();
                }
                return;
            }

            // Handle Boss / Leader Charge
            if (_chargeCooldownTimer > 0f) _chargeCooldownTimer -= Time.fixedDeltaTime * cooldownSpeedMult;
            if (_isChargeWindingUp)
            {
                if (IsTargetDead())
                {
                    _isChargeWindingUp = false;
                    HideBossChargeTelegraphVisual();
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Wander;
                    return;
                }

                rb.linearVelocity = Vector3.zero;
                _chargeWindupTimer -= Time.fixedDeltaTime;

                float totalWindup = IsRageMode ? (chargeWindupDuration * rageTelegraphSpeedMultiplier) : chargeWindupDuration;
                // Track player during initial 60% of windup, then lock direction so player can dodge
                if (_chargeWindupTimer > totalWindup * 0.4f && playerTransform != null)
                {
                    Vector3 toPlayer = (playerTransform.position - transform.position);
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.01f)
                    {
                        _chargeDirection = toPlayer.normalized;
                        transform.forward = _chargeDirection;
                    }
                }

                float currentChargeDist = chargeSpeed * (IsRageMode ? rageChargeSpeedMultiplier : 1f) * chargeDuration;
                UpdateBossChargeTelegraphVisual(_chargeDirection, currentChargeDist, chargePathWidth);

                if (_spriteRenderer != null)
                {
                    float flashFreq = Mathf.PingPong(Time.time * (IsRageMode ? 22f : 15f), 1f);
                    _spriteRenderer.color = Color.Lerp(_originalColor, Color.yellow, flashFreq);
                }

                if (_chargeWindupTimer <= 0f)
                {
                    _isChargeWindingUp = false;
                    HideBossChargeTelegraphVisual();
                    _isCharging = true;
                    _chargeActiveTimer = chargeDuration;
                }
                return;
            }

            if (_isCharging)
            {
                _chargeActiveTimer -= Time.fixedDeltaTime;
                float currentChargeSpeed = chargeSpeed * (IsRageMode ? rageChargeSpeedMultiplier : 1f);
                rb.linearVelocity = _chargeDirection * currentChargeSpeed;

                if (_chargeDirection.sqrMagnitude > 0.01f)
                {
                    transform.forward = _chargeDirection;
                }

                // Check contact damage with player during rush (1.6m ensures reliable hit at high dash speed)
                if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= 1.6f)
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
                    _chargeCooldownTimer = IsRageMode ? (chargeCooldown * rageSkillCooldownMultiplier) : chargeCooldown;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Chase;
                    return;
                }

                if (_chargeActiveTimer <= 0f)
                {
                    _isCharging = false;
                    _chargeCooldownTimer = IsRageMode ? (chargeCooldown * rageSkillCooldownMultiplier) : chargeCooldown;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Chase;
                }
                return;
            }

            // Apply leader speed buff if Leader Zombie is nearby
            ApplyLeaderBuffChecks();

            // Handle Leader minion spawning
            if (zombieType == ZombieType.Leader && health != null && !health.IsDead && !IsTargetDead())
            {
                if (_spawnCooldownTimer > 0f) _spawnCooldownTimer -= Time.fixedDeltaTime * cooldownSpeedMult;
                HandleLeaderSpawning();
            }

            base.FixedUpdate();
        }

        protected override void UpdateAIBehavior()
        {
            if (IsTargetDead())
            {
                bool night = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
                if (night)
                {
                    currentState = AIState.Idle;
                    rb.linearVelocity = Vector3.zero;
                }
                else
                {
                    WanderMovement();
                }
                return;
            }

            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool detected = IsPlayerInDetectionRange();

            float finalMoveSpeed = moveSpeed * (_hasLeaderBuff ? 1.3f : 1f);
            if (IsRageMode)
            {
                finalMoveSpeed *= rageSpeedMultiplier;
            }

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
                float speedMultiplier = (_hasLeaderBuff ? 1.3f : 1f);
                if (IsRageMode)
                {
                    speedMultiplier *= rageSpeedMultiplier;
                }
                ChasePlayer(speedMultiplier);
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
            _hasTriggeredRageMode = false;
            ResetRageVisuals();
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
            _hasTriggeredRageMode = false;
            ResetRageVisuals();
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
            _stompTelegraphTimer = IsRageMode ? (stompTelegraphDuration * rageTelegraphSpeedMultiplier) : stompTelegraphDuration;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            UpdateBossTelegraphVisual(stompRadius);
        }

        private void PerformGroundStomp()
        {
            _isStompTelegraphing = false;
            _stompCooldownTimer = IsRageMode ? (stompCooldown * rageSkillCooldownMultiplier) : stompCooldown;

            HideBossTelegraphVisual();
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            Debug.Log($"[ZombieAI] Boss performed Ground Stomp AOE with radius {stompRadius}m! (Rage: {IsRageMode})");

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

            // 2. Camera Shake (stronger in Rage Mode)
            CameraFollow.TriggerShake(IsRageMode ? 0.45f : 0.3f, IsRageMode ? 0.6f : 0.45f);

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
            _chargeWindupTimer = IsRageMode ? (chargeWindupDuration * rageTelegraphSpeedMultiplier) : chargeWindupDuration;
            _chargeCooldownTimer = IsRageMode ? (chargeCooldown * rageSkillCooldownMultiplier) : chargeCooldown;
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

            float currentChargeDist = chargeSpeed * (IsRageMode ? rageChargeSpeedMultiplier : 1f) * chargeDuration;
            UpdateBossChargeTelegraphVisual(_chargeDirection, currentChargeDist, chargePathWidth);
        }

        #region Boss Rage Mode System

        private void CheckRageModeState()
        {
            if (IsRageMode)
            {
                if (!_hasTriggeredRageMode)
                {
                    _hasTriggeredRageMode = true;
                    OnEnterRageMode();
                }
                UpdateRageVisuals();
            }
            else
            {
                if (_hasTriggeredRageMode)
                {
                    _hasTriggeredRageMode = false;
                    OnExitRageMode();
                }
            }
        }

        private void OnEnterRageMode()
        {
            // Instantly ready skills so boss attacks immediately on rage
            _stompCooldownTimer = 0f;
            _chargeCooldownTimer = 0f;
            _spawnCooldownTimer = 0f;

            // Dramatic screen shake
            CameraFollow.TriggerShake(0.45f, 0.55f);

            // Spawn rage shockwave / burst effect
            if (!string.IsNullOrEmpty(stompVfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.SpawnFromPool(stompVfxPoolKey.Trim(), transform.position, Quaternion.identity);
            }

            // Update UI healthbar name to display [RAGE]
            if (nameText != null)
            {
                nameText.text = $"<color=#FF1100>🔥 [RAGE] {enemyName}</color>";
            }

            Debug.Log($"<color=#FF0000>[ZombieAI] 🔥🔥🔥 {gameObject.name} ENTERED RAGE MODE! (HP <= {rageHealthThreshold * 100:F0}%) Speed x{rageSpeedMultiplier}, Skills Faster!</color>");
        }

        private void OnExitRageMode()
        {
            ResetRageVisuals();
            if (nameText != null)
            {
                nameText.text = enemyName;
            }
        }

        private void UpdateRageVisuals()
        {
            float pulse = Mathf.PingPong(Time.time * 6f, 1f);
            Color targetColor = Color.Lerp(_originalColor, rageTint, 0.55f + 0.45f * pulse);

            if (_spriteRenderer != null && !_isStompTelegraphing && !_isChargeWindingUp && !_isExploding)
            {
                _spriteRenderer.color = targetColor;
            }

            if (_allMeshRenderers != null)
            {
                foreach (var r in _allMeshRenderers)
                {
                    if (r == null || r == _stompCircleRenderer || r == _stompSpriteRenderer || r == _chargePathLineRenderer || r == _chargePathSpriteRenderer) continue;
                    if (r is SpriteRenderer || r is LineRenderer) continue;
                    if (r.material != null && r.material.HasProperty("_Color"))
                    {
                        r.material.color = targetColor;
                    }
                }
            }
        }

        private void ResetRageVisuals()
        {
            if (_spriteRenderer != null && _originalColor != default)
            {
                _spriteRenderer.color = _originalColor;
            }

            if (_allMeshRenderers != null && _originalRendererColors != null)
            {
                foreach (var r in _allMeshRenderers)
                {
                    if (r == null || r == _stompCircleRenderer || r == _stompSpriteRenderer || r == _chargePathLineRenderer || r == _chargePathSpriteRenderer) continue;
                    if (r is SpriteRenderer || r is LineRenderer) continue;
                    if (_originalRendererColors.TryGetValue(r, out Color orig) && r.material != null && r.material.HasProperty("_Color"))
                    {
                        r.material.color = orig;
                    }
                }
            }
        }

        /// <summary>
        /// Force reduce health to trigger Rage Mode (useful for debugging/testing).
        /// </summary>
        public void ForceTriggerRageMode()
        {
            if (health != null && health.MaxHealth > 0f)
            {
                float targetHp = health.MaxHealth * (rageHealthThreshold - 0.05f);
                float damageNeeded = health.CurrentHealth - targetHp;
                if (damageNeeded > 0f)
                {
                    health.TakeDamage(damageNeeded);
                }
            }
        }

        #endregion

        private void InitBossIndicators()
        {
            if (_stompCircleRenderer != null) return;

            // 1. LineRenderer Warning Ring (AOE stomp boundary)
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

            // 2. Ground Decal Disc (AOE stomp danger zone)
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

            // 3. LineRenderer Charge Corridor Box (rectangular outline)
            GameObject chargeLineObj = new GameObject("BossChargePathLine");
            chargeLineObj.transform.SetParent(transform);
            chargeLineObj.transform.localPosition = Vector3.zero;

            _chargePathLineRenderer = chargeLineObj.AddComponent<LineRenderer>();
            _chargePathLineRenderer.startWidth = 0.08f;
            _chargePathLineRenderer.endWidth = 0.08f;
            _chargePathLineRenderer.useWorldSpace = true;
            _chargePathLineRenderer.loop = true;
            _chargePathLineRenderer.positionCount = 4;

            _chargePathLineRenderer.material = new Material(lineMat);
            _chargePathLineRenderer.startColor = new Color(1f, 0.25f, 0.1f, 0.9f);
            _chargePathLineRenderer.endColor = new Color(1f, 0.25f, 0.1f, 0.9f);
            chargeLineObj.SetActive(false);

            // 4. Ground Decal Charge Strip (rectangular danger zone)
            GameObject chargeDiscObj = new GameObject("BossChargePathDecal");
            chargeDiscObj.transform.SetParent(transform);
            chargeDiscObj.transform.localPosition = Vector3.zero;
            chargeDiscObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _chargePathSpriteRenderer = chargeDiscObj.AddComponent<SpriteRenderer>();

            int w = 32;
            int h = 64;
            Texture2D chargeTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            chargeTex.wrapMode = TextureWrapMode.Clamp;
            Color[] fillColors = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float yNorm = (float)y / (h - 1);
                float yGlow = Mathf.Lerp(0.9f, 0.5f, yNorm);
                for (int x = 0; x < w; x++)
                {
                    float xNorm = Mathf.Abs(((float)x / (w - 1)) - 0.5f) * 2f;
                    float edgeFade = Mathf.Clamp01(1f - xNorm * xNorm);
                    fillColors[y * w + x] = new Color(1f, 0.15f, 0.1f, edgeFade * yGlow);
                }
            }
            chargeTex.SetPixels(fillColors);
            chargeTex.Apply();
            _chargePathSpriteRenderer.sprite = Sprite.Create(chargeTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
            _chargePathSpriteRenderer.color = chargeTelegraphColor;
            chargeDiscObj.SetActive(false);
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

        private void UpdateBossChargeTelegraphVisual(Vector3 direction, float distance, float width)
        {
            if (_chargePathLineRenderer == null && IsBossUnit)
            {
                InitBossIndicators();
            }

            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            direction.y = 0f;
            direction.Normalize();

            Vector3 origin = transform.position;
            Vector3 forward = direction;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float halfWidth = width * 0.5f;

            if (_chargePathLineRenderer != null)
            {
                if (!_chargePathLineRenderer.gameObject.activeSelf) _chargePathLineRenderer.gameObject.SetActive(true);

                Vector3 p0 = origin - right * halfWidth;
                Vector3 p1 = origin + right * halfWidth;
                Vector3 p2 = origin + right * halfWidth + forward * distance;
                Vector3 p3 = origin - right * halfWidth + forward * distance;

                p0.y = 0.05f;
                p1.y = 0.05f;
                p2.y = 0.05f;
                p3.y = 0.05f;

                _chargePathLineRenderer.SetPosition(0, p0);
                _chargePathLineRenderer.SetPosition(1, p1);
                _chargePathLineRenderer.SetPosition(2, p2);
                _chargePathLineRenderer.SetPosition(3, p3);

                // Pulsate line brightness
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 18f);
                Color c = new Color(1f, 0.25f, 0.1f, pulse);
                _chargePathLineRenderer.startColor = c;
                _chargePathLineRenderer.endColor = c;
            }

            if (_chargePathSpriteRenderer != null)
            {
                if (!_chargePathSpriteRenderer.gameObject.activeSelf) _chargePathSpriteRenderer.gameObject.SetActive(true);

                _chargePathSpriteRenderer.transform.position = new Vector3(origin.x, 0.04f, origin.z);

                float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                _chargePathSpriteRenderer.transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

                float baseW = _chargePathSpriteRenderer.sprite != null ? _chargePathSpriteRenderer.sprite.bounds.size.x : 1f;
                float baseH = _chargePathSpriteRenderer.sprite != null ? _chargePathSpriteRenderer.sprite.bounds.size.y : 2f;
                if (baseW > 0f && baseH > 0f)
                {
                    _chargePathSpriteRenderer.transform.localScale = new Vector3(width / baseW, distance / baseH, 1f);
                }

                float pulseAlpha = Mathf.Lerp(0.3f, 0.65f, Mathf.PingPong(Time.time * 8f, 1f));
                Color sc = chargeTelegraphColor;
                sc.a = pulseAlpha;
                _chargePathSpriteRenderer.color = sc;
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
            HideBossChargeTelegraphVisual();
        }

        private void HideBossChargeTelegraphVisual()
        {
            if (_chargePathLineRenderer != null && _chargePathLineRenderer.gameObject.activeSelf)
            {
                _chargePathLineRenderer.gameObject.SetActive(false);
            }
            if (_chargePathSpriteRenderer != null && _chargePathSpriteRenderer.gameObject.activeSelf)
            {
                _chargePathSpriteRenderer.gameObject.SetActive(false);
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

        private MinionSpawnOption GetRandomMinionOption()
        {
            if (minionSpawnPool != null && minionSpawnPool.Count > 0)
            {
                int totalWeight = 0;
                foreach (var opt in minionSpawnPool)
                {
                    totalWeight += Mathf.Max(1, opt.weight > 0 ? opt.weight : 1);
                }

                int roll = Random.Range(0, totalWeight);
                int cumulative = 0;
                foreach (var opt in minionSpawnPool)
                {
                    cumulative += Mathf.Max(1, opt.weight > 0 ? opt.weight : 1);
                    if (roll < cumulative)
                    {
                        return opt;
                    }
                }

                return minionSpawnPool[Random.Range(0, minionSpawnPool.Count)];
            }

            // Fallback to single configured minion
            return new MinionSpawnOption
            {
                poolKey = childPoolKey,
                weight = 1
            };
        }

        private void HandleLeaderSpawning()
        {
            if (_spawnCooldownTimer > 0f || _currentChildSpawns >= maxChildSpawns) return;
            if (!IsPlayerInDetectionRange()) return; // only spawn when player is near!

            _spawnCooldownTimer = IsRageMode ? (spawnCooldown * rageSkillCooldownMultiplier) : spawnCooldown;

            Vector3 spawnOffset = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            Vector3 spawnPos = transform.position + spawnOffset;

            // Pick a random minion pool key
            MinionSpawnOption minionOption = GetRandomMinionOption();
            string targetPoolKey = !string.IsNullOrEmpty(minionOption.poolKey) ? minionOption.poolKey.Trim() : (!string.IsNullOrEmpty(childPoolKey) ? childPoolKey.Trim() : "");

            GameObject babyZombie = null;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(targetPoolKey))
            {
                babyZombie = ObjectPoolManager.Instance.SpawnFromPool(targetPoolKey, spawnPos, Quaternion.identity);
            }
            
            // Fallback to single childZombiePrefab if pool wasn't registered
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
                ZombieAI babyAI = babyZombie.GetComponent<ZombieAI>();
                if (babyAI != null)
                {
                    // Ensure the spawned minion remembers its pool key for returning to pool on death
                    if (!string.IsNullOrEmpty(targetPoolKey))
                    {
                        babyAI.SetPoolKey(targetPoolKey);
                    }

                    babyAI.SkipTransitionDelay();
                    babyAI.AlertToPlayer();

                    // Ensure spawned zombie is never a Leader to prevent infinite leader proliferation
                    if (babyAI.zombieType == ZombieType.Leader)
                    {
                        babyAI.zombieType = ZombieType.Normal;
                    }
                }

                _currentChildSpawns++;

                // Track child death to clear counter slot cleanly
                Health babyHealth = babyZombie.GetComponent<Health>();
                if (babyHealth != null)
                {
                    babyHealth.onDeath.RemoveListener(OnChildMinionDefeated);
                    babyHealth.onDeath.AddListener(OnChildMinionDefeated);
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

        private void OnChildMinionDefeated()
        {
            if (this != null)
            {
                _currentChildSpawns = Mathf.Max(0, _currentChildSpawns - 1);
            }
        }
    }
}
