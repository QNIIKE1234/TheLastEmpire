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
            Runner,
            Spitter,
            Tank
        }

        [Header("Zombie Settings")]
        [SerializeField] private ZombieType zombieType = ZombieType.Normal;

        [Header("Dog / Leap Settings")]
        [SerializeField] private float leapCooldown = 4f;
        [SerializeField] private float leapSpeed = 12f;
        [SerializeField] private float leapDuration = 0.3f;

        [Header("Spitter Settings")]
        [SerializeField] private float spitCooldown = 3.5f;
        [SerializeField] private float spitRange = 8.5f;
        private float _spitCooldownTimer = 0f;

        [Header("Boomer / Explosion Settings")]
        [SerializeField] private float explosionRadius = 2.25f;
        [SerializeField] private int explosionDamage = 45;
        [SerializeField] private float fuseDuration = 1.0f;
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

        [Header("Boss Leap Attack Settings (ZombieLeader)")]
        [SerializeField] private float bossLeapCooldown = 5.5f;
        [SerializeField] private float bossLeapMinRange = 3.5f;
        [SerializeField] private float bossLeapMaxRange = 11.0f;
        [SerializeField] private float bossLeapDuration = 0.65f;
        [SerializeField] private float bossLeapArcHeight = 3.2f;
        [SerializeField] private float bossLeapLandRadius = 3.5f;
        [SerializeField] private int bossLeapLandDamage = 40;
        [SerializeField] private float bossLeapKnockback = 11f;

        [Header("Enemy Projectile Speed")]
        [SerializeField] private float acidProjectileSpeed = 50f;

        private float _spawnCooldownTimer = 0f;
        private int _currentChildSpawns = 0;

        private float _leapCooldownTimer = 0f;
        private float _leapActiveTimer = 0f;
        private bool _isLeaping = false;
        private Vector3 _leapDirection;

        // Boss Parabolic Leap State (ZombieLeader jumps onto player instead of charging!)
        private bool _isBossLeaping = false;
        private float _bossLeapElapsed = 0f;
        private Vector3 _bossLeapStartPos;
        private Vector3 _bossLeapTargetPos;
        private bool _isSpitting = false;

        private bool _isExploding = false;
        private float _fuseTimer = 0f;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private Renderer[] _allMeshRenderers;
        private readonly Dictionary<Renderer, Color> _originalRendererColors = new Dictionary<Renderer, Color>();

        private float _leaderBuffCheckTimer = 0f;
        private bool _hasLeaderBuff = false;
        private bool _hasTriggeredRageMode = false;
        private float _rageRoarTimer = 0f;

        // Boss Charge State
        private float _chargeCooldownTimer = 0f;
        private float _chargeWindupTimer = 0f;
        private float _chargeActiveTimer = 0f;
        private bool _isCharging = false;
        private bool _isChargeWindingUp = false;
        private Vector3 _chargeDirection;
        private LineRenderer _chargePathLineRenderer;
        private SpriteRenderer _chargePathSpriteRenderer;
        private SpriteRenderer[] _chargeChevronSegments;

        // Boss Stomp AOE State
        private float _stompCooldownTimer = 0f;
        private float _stompTelegraphTimer = 0f;
        private bool _isStompTelegraphing = false;
        private LineRenderer _stompCircleRenderer;
        private SpriteRenderer _stompSpriteRenderer;

        // Boomer Explosion Telegraph Indicators
        private LineRenderer _boomerCircleRenderer;
        private SpriteRenderer _boomerDecalRenderer;

        public bool IsBossUnit => (zombieType == ZombieType.Leader || (TryGetComponent<LootDropper>(out var ld) && ld.IsBoss));
        // ZombieTank inherits former ZombieLeader's heavy combat kit (Charge + Ground Stomp) WITHOUT minion spawning or Rage Mode!
        public bool HasHeavyBossKit => IsBossUnit || zombieType == ZombieType.Tank;
        // Rage Mode ใช้เฉพาะบอสเท่านั้น (ไม่รวม ZombieTank)
        public bool IsRageMode => IsBossUnit && health != null && !health.IsDead && health.MaxHealth > 0f && ((health.CurrentHealth / health.MaxHealth) <= rageHealthThreshold);

        // ซอมบี้ระดับบอสและ Tank เปิดใช้งานระบบเดินอ้อมสิ่งกีดขวางอัจฉริยะ
        public override bool UseSmartObstacleAvoidance => useSmartObstacleAvoidance || HasHeavyBossKit;

        protected override void OnEnable()
        {
            base.OnEnable();
            HideBossTelegraphVisual();
            HideBoomerTelegraphVisual();
            _isExploding = false;
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            _isBossLeaping = false;
            _isSpitting = false;
            _hasTriggeredRageMode = false;
            _rageRoarTimer = 0f;
            ResetRageVisuals();
            _chargeCooldownTimer = 2.0f;
            _stompCooldownTimer = 2.0f;
            _leapCooldownTimer = 2.0f;
            _spitCooldownTimer = 2.5f;
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

            // Auto-correct ZombieType based on prefab/GameObject name if left as default/Normal
            if (zombieType == ZombieType.Normal)
            {
                string lowerName = gameObject.name.ToLower();
                if (lowerName.Contains("boomer"))
                {
                    zombieType = ZombieType.Boomer;
                    Debug.Log($"[ZombieAI] Auto-detected type as Boomer from GameObject name: {gameObject.name}");
                }
                else if (lowerName.Contains("leader") || lowerName.Contains("boss"))
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
                else if (lowerName.Contains("spitter") || lowerName.Contains("acid"))
                {
                    zombieType = ZombieType.Spitter;
                    Debug.Log($"[ZombieAI] Auto-detected type as Spitter from GameObject name: {gameObject.name}");
                }
                else if (lowerName.Contains("tank") || lowerName.Contains("brute"))
                {
                    zombieType = ZombieType.Tank;
                    Debug.Log($"[ZombieAI] Auto-detected type as Tank from GameObject name: {gameObject.name}");
                }
            }

            if (HasHeavyBossKit)
            {
                useSmartObstacleAvoidance = true;
                InitBossIndicators();
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
                    detectionRange = 9f;
                    break;
                case ZombieType.Boomer:
                    moveSpeed = 2f;
                    break;
                case ZombieType.Spitter:
                    moveSpeed = 2.5f;
                    detectionRange = 9f;
                    break;
                case ZombieType.Tank:
                    moveSpeed = 2.4f;
                    detectionRange = 8f;
                    staggerDuration = 0.1f;
                    knockbackForce = 12f;
                    break;
            }
        }

        protected override void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

            // Check Boss Rage Mode (HP <= 30% — Boss only, not ZombieTank)
            if (IsBossUnit)
            {
                CheckRageModeState();
                if (_rageRoarTimer > 0f)
                {
                    _rageRoarTimer -= Time.fixedDeltaTime;
                    if (rb != null) rb.linearVelocity = Vector3.zero;
                    return;
                }
            }

            // Cooldown rate multiplier (Skills tick down faster during Rage Mode!)
            float cooldownSpeedMult = IsRageMode ? (1f / Mathf.Max(0.1f, rageSkillCooldownMultiplier)) : 1f;

            // Handle spit & leap countdown timers
            if (_spitCooldownTimer > 0f) _spitCooldownTimer -= Time.fixedDeltaTime * cooldownSpeedMult;
            if (_leapCooldownTimer > 0f) _leapCooldownTimer -= Time.fixedDeltaTime * cooldownSpeedMult;

            if (_isSpitting)
            {
                if (rb != null) rb.linearVelocity = Vector3.zero;
                return;
            }

            // Handle Boss Parabolic Leap Attack (ZombieLeader jumps in high arc onto player!)
            if (_isBossLeaping)
            {
                _bossLeapElapsed += Time.fixedDeltaTime;
                float duration = Mathf.Max(0.25f, bossLeapDuration);
                float t = Mathf.Clamp01(_bossLeapElapsed / duration);
                Vector3 flatPos = Vector3.Lerp(_bossLeapStartPos, _bossLeapTargetPos, t);
                float arcY = 4f * bossLeapArcHeight * t * (1f - t);
                Vector3 nextPos = new Vector3(flatPos.x, Mathf.Lerp(_bossLeapStartPos.y, _bossLeapTargetPos.y, t) + arcY, flatPos.z);

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }
                transform.position = nextPos;

                // Show 1st hitbox sprite (UI_Hitbox_Sprites_0) at Boss Leap landing zone!
                UpdateBossTelegraphVisualAt(_bossLeapTargetPos, bossLeapLandRadius);

                if (t >= 1f)
                {
                    FinishBossLeapSlam();
                }
                return;
            }
            
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

            // Handle Boomer Fuse Countdown (1.0s delay with visible danger telegraph)
            if (_isExploding)
            {
                if (IsTargetDead())
                {
                    _isExploding = false;
                    HideBoomerTelegraphVisual();
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    currentState = AIState.Wander;
                    return;
                }

                rb.linearVelocity = Vector3.zero;
                _fuseTimer -= Time.fixedDeltaTime;

                UpdateBoomerTelegraphVisual(explosionRadius);

                // Flash red color visually to indicate fuse explosion warning (blinking faster as fuse burns down)
                if (_spriteRenderer != null)
                {
                    float fuseProgress = 1f - Mathf.Clamp01(_fuseTimer / Mathf.Max(0.1f, fuseDuration));
                    float speed = Mathf.Lerp(10f, 30f, fuseProgress);
                    float flashFreq = Mathf.PingPong(Time.time * speed, 1f);
                    _spriteRenderer.color = Color.Lerp(_originalColor, Color.red, flashFreq);
                }

                if (_fuseTimer <= 0f)
                {
                    ExplodeAndDie();
                }
                return;
            }

            // Handle Boss / Tank Stomp AOE Telegraph
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

                // หากพุ่งชนกำแพงหรือสิ่งกีดขวางตรงหน้า ให้หยุดพุ่งทันทีเพื่อไม่ให้ติดค้างกับกำแพง
                if (IsObstacleDirectlyAhead(_chargeDirection, GetAgentRadius() + 0.35f))
                {
                    _isCharging = false;
                    _chargeCooldownTimer = IsRageMode ? (chargeCooldown * rageSkillCooldownMultiplier) : chargeCooldown;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    ClearActionAnimationLock();
                    currentState = AIState.Chase;
                    return;
                }

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
                    ClearActionAnimationLock();
                    currentState = AIState.Chase;
                    return;
                }

                if (_chargeActiveTimer <= 0f)
                {
                    _isCharging = false;
                    _chargeCooldownTimer = IsRageMode ? (chargeCooldown * rageSkillCooldownMultiplier) : chargeCooldown;
                    rb.linearVelocity = Vector3.zero;
                    if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
                    ClearActionAnimationLock();
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
                // Spitter periodically spits poison/acid projectile at player
                if (zombieType == ZombieType.Spitter && _spitCooldownTimer <= 0f && distanceToPlayer >= 3f && distanceToPlayer <= spitRange)
                {
                    StartCoroutine(ExecuteSpitterSpit());
                    return;
                }

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

                // Boss (ZombieLeader) Special Attacks: Minion Spawn (in FixedUpdate) + Parabolic Leap Slam (NOT Charge!) + Acid Spit + Close Stomp
                if (IsBossUnit)
                {
                    bool hasLineOfSight = HasLineOfSightToPlayer();

                    // 1. Ground Stomp AOE up close
                    if (_stompCooldownTimer <= 0f && distanceToPlayer <= stompAttackRange && hasLineOfSight && !_isStompTelegraphing && !_isBossLeaping && !_isSpitting)
                    {
                        StartStompTelegraph();
                        return;
                    }

                    // 2. Boss Parabolic Leap Slam onto player (กระโดดใส่ ไม่ใช่พุ่ง!)
                    if (_leapCooldownTimer <= 0f && distanceToPlayer >= bossLeapMinRange && distanceToPlayer <= bossLeapMaxRange && hasLineOfSight && !_isBossLeaping && !_isStompTelegraphing && !_isSpitting)
                    {
                        StartBossLeap();
                        return;
                    }

                    // 3. Boss Acid Spit (พ่นกรดใส่ผู้เล่น!)
                    if (_spitCooldownTimer <= 0f && distanceToPlayer >= 3.0f && distanceToPlayer <= Mathf.Max(spitRange, 10.5f) && hasLineOfSight && !_isBossLeaping && !_isStompTelegraphing && !_isSpitting)
                    {
                        StartCoroutine(ExecuteSpitterSpit());
                        return;
                    }
                }
                // ZombieTank inherits former Boss behaviors (Ground Stomp AOE + Charge Rush) EXCEPT Minion Spawning & Rage Mode!
                else if (zombieType == ZombieType.Tank)
                {
                    bool hasLineOfSight = HasLineOfSightToPlayer();

                    // 1. Ground Stomp AOE
                    if (_stompCooldownTimer <= 0f && distanceToPlayer <= stompAttackRange && hasLineOfSight && !_isStompTelegraphing && !_isCharging && !_isChargeWindingUp)
                    {
                        StartStompTelegraph();
                        return;
                    }

                    // 2. Tank Charge Rush (อดีตท่าพุ่งของบอสซอมบี้!)
                    if (_chargeCooldownTimer <= 0f && distanceToPlayer >= minChargeDistance && distanceToPlayer <= maxChargeDistance && hasLineOfSight && !_isCharging && !_isChargeWindingUp && !_isStompTelegraphing)
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
                // ตอนกลางคืนซอมบี้จะช้าลง 50% (50% slower at night)
                if (isNight)
                {
                    speedMultiplier *= 0.5f;
                }
                ChasePlayer(speedMultiplier);
            }
            else
            {
                // ซอมบี้เดินช้าลง 50% ตอนกลางคืน
                WanderMovement(isNight ? 0.5f : 1.0f);
            }
        }

        private void StartBossLeap()
        {
            _isBossLeaping = true;
            _bossLeapElapsed = 0f;
            _leapCooldownTimer = IsRageMode ? (bossLeapCooldown * rageSkillCooldownMultiplier) : bossLeapCooldown;
            currentState = AIState.Telegraph;
            if (rb != null) rb.linearVelocity = Vector3.zero;

            _bossLeapStartPos = transform.position;
            if (playerTransform != null)
            {
                Vector3 toPlayer = playerTransform.position - transform.position;
                toPlayer.y = 0f;
                float dist = Mathf.Clamp(toPlayer.magnitude, bossLeapMinRange * 0.7f, bossLeapMaxRange);
                Vector3 dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;
                transform.forward = dir;
                _bossLeapTargetPos = new Vector3(_bossLeapStartPos.x + dir.x * dist, _bossLeapStartPos.y, _bossLeapStartPos.z + dir.z * dist);
            }
            else
            {
                _bossLeapTargetPos = _bossLeapStartPos + transform.forward * 5f;
            }

            PlayActionAnimationWithFallback(bossLeapDuration + 0.15f, 0.05f, "Leap", "GroundSlam", "Charge", "Slash1");
            TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                this,
                TheLastEmpire.Network.NetworkEnemyBridge.TelegraphBossLeap,
                _bossLeapTargetPos,
                transform.forward,
                bossLeapLandRadius,
                bossLeapDuration);
            Debug.Log($"[ZombieAI] Boss {gameObject.name} leaped towards {_bossLeapTargetPos}!");
        }

        private void FinishBossLeapSlam()
        {
            _isBossLeaping = false;
            HideBossTelegraphVisual();
            if (rb != null) rb.linearVelocity = Vector3.zero;

            // Landing VFX + Camera Shake + AOE Impact
            if (!string.IsNullOrEmpty(stompVfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                GameObject vfx = ObjectPoolManager.Instance.SpawnFromPool(stompVfxPoolKey.Trim(), transform.position, Quaternion.identity);
                if (vfx != null)
                {
                    PooledParticle pp = vfx.GetComponent<PooledParticle>();
                    if (pp != null)
                    {
                        pp.SetPoolKey(stompVfxPoolKey.Trim());
                        pp.ApplyScaleMultiplier(bossLeapLandRadius * 0.85f);
                    }
                }
            }
            CameraFollow.TriggerShake(IsRageMode ? 0.35f : 0.22f, 0.45f);

            Collider[] targets = Physics.OverlapSphere(transform.position, bossLeapLandRadius);
            foreach (var target in targets)
            {
                if (target.gameObject == gameObject || target.CompareTag("Enemy")) continue;
                IDamageable dmg = target.GetComponent<IDamageable>();
                if (dmg != null) dmg.TakeDamage(bossLeapLandDamage);

                PlayerController pc = target.GetComponent<PlayerController>();
                if (pc != null)
                {
                    Vector3 pushDir = pc.transform.position - transform.position;
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.forward;
                    pc.ApplyKnockback(pushDir.normalized * bossLeapKnockback, 0.3f);
                }
            }

            ClearActionAnimationLock();
            PlayActionAnimationWithFallback(0.45f, 0.04f, "GroundSlam", "Slash2", "Slash1");
            currentState = AIState.Chase;
        }

        private void StartDogLeap()
        {
            _isLeaping = true;
            _leapCooldownTimer = leapCooldown;
            _leapActiveTimer = leapDuration;
            currentState = AIState.Telegraph;
            TriggerAttackAnimation();
            
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
            _fuseTimer = Mathf.Max(1.0f, fuseDuration);
            currentState = AIState.Telegraph;
            PlayActionAnimation("Explode", _fuseTimer + 0.35f, 0.05f);
            UpdateBoomerTelegraphVisual(explosionRadius);
            TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                this,
                TheLastEmpire.Network.NetworkEnemyBridge.TelegraphZombieBoomer,
                transform.position,
                transform.forward,
                explosionRadius,
                _fuseTimer);
        }

        private void ExplodeAndDie()
        {
            Debug.Log($"[ZombieAI] Boomer exploded at {transform.position}!");
            HideBoomerTelegraphVisual();

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

            // Boss & Tank units are immune to stagger and interruption during Stomp Telegraph, Charge, or Boss Leap!
            if (HasHeavyBossKit && (_isStompTelegraphing || _isCharging || _isChargeWindingUp || _isBossLeaping))
            {
                hasBeenAttacked = true;
                return;
            }

            base.OnDamageTaken(damageAmount);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            HideBossTelegraphVisual();
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            _hasTriggeredRageMode = false;
            _rageRoarTimer = 0f;
            ResetRageVisuals();
            if (_spriteRenderer != null && _originalColor != default)
            {
                _spriteRenderer.color = _originalColor;
            }
        }

        protected override void HandleDeath()
        {
            HideBossTelegraphVisual();
            HideBoomerTelegraphVisual();
            _isStompTelegraphing = false;
            _isCharging = false;
            _isChargeWindingUp = false;
            _hasTriggeredRageMode = false;
            _rageRoarTimer = 0f;
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
            HideBoomerTelegraphVisual();
            base.OnDestroy();
        }

        private void StartStompTelegraph()
        {
            _isStompTelegraphing = true;
            _stompTelegraphTimer = IsRageMode ? (stompTelegraphDuration * rageTelegraphSpeedMultiplier) : stompTelegraphDuration;
            currentState = AIState.Telegraph;
            rb.linearVelocity = Vector3.zero;

            PlayActionAnimation("GroundSlam", _stompTelegraphTimer + 0.35f, 0.06f);
            UpdateBossTelegraphVisual(stompRadius);
            TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                this,
                TheLastEmpire.Network.NetworkEnemyBridge.TelegraphBossStomp,
                transform.position,
                transform.forward,
                stompRadius,
                _stompTelegraphTimer);
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

            PlayActionAnimation("Charge", _chargeWindupTimer + chargeDuration + 0.20f, 0.06f);
            float currentChargeDist = chargeSpeed * (IsRageMode ? rageChargeSpeedMultiplier : 1f) * chargeDuration;
            UpdateBossChargeTelegraphVisual(_chargeDirection, currentChargeDist, chargePathWidth);
            TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                this,
                TheLastEmpire.Network.NetworkEnemyBridge.TelegraphBossCharge,
                transform.position,
                _chargeDirection,
                currentChargeDist,
                _chargeWindupTimer);
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

            // Play double-bicep flex power roar animation (ท่าคำรามเบ่งพลัง)
            if (!_isStompTelegraphing && !_isCharging && !_isChargeWindingUp)
            {
                _rageRoarTimer = 0.85f;
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }
            PlayActionAnimation("RageRoar", 1.10f, 0.05f);

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
                nameText.text = $"<color=#FF1100>[RAGE] {enemyName}</color>";
            }

            Debug.Log($"<color=#FF0000>[ZombieAI] {gameObject.name} ENTERED RAGE MODE! (HP <= {rageHealthThreshold * 100:F0}%) Speed x{rageSpeedMultiplier}, Skills Faster!</color>");
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
                    if (r == null || r == _stompCircleRenderer || r == _stompSpriteRenderer || r == _chargePathLineRenderer || r == _chargePathSpriteRenderer || r == _boomerCircleRenderer || r == _boomerDecalRenderer) continue;
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
                    if (r == null || r == _stompCircleRenderer || r == _stompSpriteRenderer || r == _chargePathLineRenderer || r == _chargePathSpriteRenderer || r == _boomerCircleRenderer || r == _boomerDecalRenderer) continue;
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

            // 2. Ground Decal Disc (AOE stomp danger zone — 1st sprite: UI_Hitbox_Sprites_0 Hazard Circle)
            GameObject discObj = new GameObject("BossAOEDisc");
            discObj.transform.SetParent(transform);
            discObj.transform.localPosition = Vector3.zero;
            discObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _stompSpriteRenderer = discObj.AddComponent<SpriteRenderer>();
            _stompSpriteRenderer.sprite = stompTelegraphSprite != null ? stompTelegraphSprite : HitboxSpriteLibrary.HazardCircle;
            _stompSpriteRenderer.color = new Color(1f, 0.14f, 0.08f, 0.85f);
            _stompSpriteRenderer.sortingOrder = 6;
            _stompSpriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _stompSpriteRenderer.receiveShadows = false;

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

            // 3. LineRenderer Charge Corridor Box (fallback if sprite sheet missing)
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

            // 4. Boss Charge Directional Chevrons (Last sprite: UI_Hitbox_Sprites_7 >>>>)
            _chargeChevronSegments = new SpriteRenderer[4];
            Sprite chevronSprite = HitboxSpriteLibrary.DirectionalChevrons;
            for (int i = 0; i < _chargeChevronSegments.Length; i++)
            {
                SpriteRenderer seg = HitboxSpriteLibrary.CreateGroundSpriteIndicator(
                    $"BossChargeChevron_{i}", transform, chevronSprite, HitboxSpriteLibrary.DangerRed, 6);
                seg.gameObject.SetActive(false);
                _chargeChevronSegments[i] = seg;
            }

            // Legacy procedural fallback strip (only used if UI_Hitbox_Sprites_7 fails to load)
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
            UpdateBossTelegraphVisualAt(transform.position, currentRadius);
        }

        private void UpdateBossTelegraphVisualAt(Vector3 center, float currentRadius)
        {
            if (_stompCircleRenderer == null && HasHeavyBossKit)
            {
                InitBossIndicators();
            }

            // Ensure 1st sprite (UI_Hitbox_Sprites_0 Hazard Circle) is assigned for Boss Stomp / Ground Slam
            Sprite hazardSprite = stompTelegraphSprite != null ? stompTelegraphSprite : HitboxSpriteLibrary.HazardCircle;
            bool usingHitboxSprite = hazardSprite != null && _stompSpriteRenderer != null;
            if (usingHitboxSprite && _stompSpriteRenderer.sprite != hazardSprite)
            {
                _stompSpriteRenderer.sprite = hazardSprite;
            }

            if (usingHitboxSprite)
            {
                // Hide old crude LineRenderer circle when crisp UI_Hitbox_Sprites_0 is active (original color, no spin)
                if (_stompCircleRenderer != null && _stompCircleRenderer.gameObject.activeSelf)
                    _stompCircleRenderer.gameObject.SetActive(false);

                if (!_stompSpriteRenderer.gameObject.activeSelf) _stompSpriteRenderer.gameObject.SetActive(true);
                _stompSpriteRenderer.transform.position = new Vector3(center.x, 0.06f, center.z);
                _stompSpriteRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                HitboxSpriteLibrary.SetWorldDiameter(_stompSpriteRenderer, currentRadius * 2f);
                _stompSpriteRenderer.color = HitboxSpriteLibrary.DefaultOriginalColor;
                return;
            }

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
                HitboxSpriteLibrary.SetWorldDiameter(_stompSpriteRenderer, currentRadius * 2f);

                float pulseAlpha = Mathf.Lerp(0.3f, 0.6f, Mathf.PingPong(Time.time * 6f, 1f));
                Color sc = stompTelegraphColor;
                sc.a = pulseAlpha;
                _stompSpriteRenderer.color = sc;
            }
        }

        private void UpdateBossChargeTelegraphVisual(Vector3 direction, float distance, float width)
        {
            if (_chargePathLineRenderer == null && HasHeavyBossKit)
            {
                InitBossIndicators();
            }

            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            direction.y = 0f;
            direction.Normalize();

            Vector3 origin = transform.position;
            Vector3 forward = direction;

            // Primary: Last sprite (UI_Hitbox_Sprites_7 Directional Chevrons >>>>) arranged cleanly along charge path!
            Sprite chevronSprite = HitboxSpriteLibrary.DirectionalChevrons;
            if (chevronSprite != null && _chargeChevronSegments != null && _chargeChevronSegments.Length > 0)
            {
                if (_chargePathLineRenderer != null && _chargePathLineRenderer.gameObject.activeSelf)
                    _chargePathLineRenderer.gameObject.SetActive(false);
                if (_chargePathSpriteRenderer != null && _chargePathSpriteRenderer.gameObject.activeSelf)
                    _chargePathSpriteRenderer.gameObject.SetActive(false);

                int segCount = Mathf.Clamp(Mathf.RoundToInt(distance / Mathf.Max(1.8f, width * 1.65f)), 1, _chargeChevronSegments.Length);
                float segLen = distance / segCount;
                Quaternion rot = HitboxSpriteLibrary.GetGroundChevronRotation(forward);
                float baseW = chevronSprite.bounds.size.x;
                float baseH = chevronSprite.bounds.size.y;

                for (int i = 0; i < _chargeChevronSegments.Length; i++)
                {
                    SpriteRenderer seg = _chargeChevronSegments[i];
                    if (seg == null) continue;

                    if (i >= segCount)
                    {
                        if (seg.gameObject.activeSelf) seg.gameObject.SetActive(false);
                        continue;
                    }

                    if (seg.sprite != chevronSprite) seg.sprite = chevronSprite;
                    if (!seg.gameObject.activeSelf) seg.gameObject.SetActive(true);

                    float distAlong = (i + 0.5f) * segLen;
                    Vector3 pos = origin + forward * distAlong;
                    seg.transform.position = new Vector3(pos.x, 0.06f, pos.z);
                    seg.transform.rotation = rot;
                    if (baseW > 0.001f && baseH > 0.001f)
                    {
                        seg.transform.localScale = new Vector3((segLen * 0.94f) / baseW, (width * 0.92f) / baseH, 1f);
                    }

                    // Original sprite color (no color tint, 25% alpha)
                    seg.color = HitboxSpriteLibrary.DefaultOriginalColor;
                }
                return;
            }

            // Fallback if sprite sheet unavailable
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
            if (_chargeChevronSegments != null)
            {
                for (int i = 0; i < _chargeChevronSegments.Length; i++)
                {
                    if (_chargeChevronSegments[i] != null && _chargeChevronSegments[i].gameObject.activeSelf)
                    {
                        _chargeChevronSegments[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        #region Boomer Explosion Telegraph

        private void InitBoomerIndicators()
        {
            if (_boomerCircleRenderer != null) return;

            // 1. Fallback LineRenderer Warning Ring (hidden when UI_Hitbox_Sprites_0 is available)
            GameObject circleObj = new GameObject("BoomerExplosionCircle");
            circleObj.transform.SetParent(transform);
            circleObj.transform.localPosition = Vector3.zero;

            _boomerCircleRenderer = circleObj.AddComponent<LineRenderer>();
            _boomerCircleRenderer.startWidth = 0.08f;
            _boomerCircleRenderer.endWidth = 0.08f;
            _boomerCircleRenderer.useWorldSpace = true;
            _boomerCircleRenderer.loop = true;
            _boomerCircleRenderer.positionCount = 48;

            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            _boomerCircleRenderer.material = lineMat;
            _boomerCircleRenderer.startColor = new Color(1f, 0.15f, 0.1f, 0.95f);
            _boomerCircleRenderer.endColor = new Color(1f, 0.15f, 0.1f, 0.95f);
            circleObj.SetActive(false);

            // 2. Ground Decal Disc (UI_Hitbox_Sprites_0 Hazard Circle)
            GameObject discObj = new GameObject("BoomerExplosionDecal");
            discObj.transform.SetParent(transform);
            discObj.transform.localPosition = Vector3.zero;
            discObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _boomerDecalRenderer = discObj.AddComponent<SpriteRenderer>();
            _boomerDecalRenderer.sortingOrder = 6;
            _boomerDecalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _boomerDecalRenderer.receiveShadows = false;

            Sprite hazardSprite = HitboxSpriteLibrary.HazardCircle;
            if (hazardSprite != null)
            {
                _boomerDecalRenderer.sprite = hazardSprite;
                _boomerDecalRenderer.color = HitboxSpriteLibrary.DangerRed;
            }
            else
            {
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
                            colors[y * size + x] = new Color(1f, 0.12f, 0.08f, alpha);
                        }
                        else
                        {
                            colors[y * size + x] = Color.clear;
                        }
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                _boomerDecalRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
                _boomerDecalRenderer.color = new Color(1f, 0.15f, 0.1f, 0.35f);
            }
            discObj.SetActive(false);
        }

        private void UpdateBoomerTelegraphVisual(float currentRadius)
        {
            if (_boomerCircleRenderer == null)
            {
                InitBoomerIndicators();
            }

            Vector3 center = transform.position;
            float fuseProgress = 1f - Mathf.Clamp01(_fuseTimer / Mathf.Max(0.1f, fuseDuration));
            Sprite hazardSprite = HitboxSpriteLibrary.HazardCircle;
            bool usingHitboxSprite = hazardSprite != null && _boomerDecalRenderer != null;

            if (usingHitboxSprite)
            {
                if (_boomerDecalRenderer.sprite != hazardSprite) _boomerDecalRenderer.sprite = hazardSprite;
                if (_boomerCircleRenderer != null && _boomerCircleRenderer.gameObject.activeSelf)
                    _boomerCircleRenderer.gameObject.SetActive(false);

                if (!_boomerDecalRenderer.gameObject.activeSelf) _boomerDecalRenderer.gameObject.SetActive(true);
                _boomerDecalRenderer.transform.position = new Vector3(center.x, 0.06f, center.z);
                _boomerDecalRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                HitboxSpriteLibrary.SetWorldDiameter(_boomerDecalRenderer, currentRadius * 2f);
                _boomerDecalRenderer.color = HitboxSpriteLibrary.DefaultOriginalColor;
                return;
            }

            if (_boomerCircleRenderer != null)
            {
                if (!_boomerCircleRenderer.gameObject.activeSelf) _boomerCircleRenderer.gameObject.SetActive(true);
                int segments = _boomerCircleRenderer.positionCount;
                float angle = 0f;
                for (int i = 0; i < segments; i++)
                {
                    float x = Mathf.Sin(Mathf.Deg2Rad * angle) * currentRadius;
                    float z = Mathf.Cos(Mathf.Deg2Rad * angle) * currentRadius;
                    _boomerCircleRenderer.SetPosition(i, new Vector3(center.x + x, 0.05f, center.z + z));
                    angle += (360f / segments);
                }

                float pulseSpeed = Mathf.Lerp(10f, 30f, fuseProgress);
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * pulseSpeed);
                Color c = new Color(1f, 0.15f, 0.1f, pulse);
                _boomerCircleRenderer.startColor = c;
                _boomerCircleRenderer.endColor = c;
            }

            if (_boomerDecalRenderer != null)
            {
                if (!_boomerDecalRenderer.gameObject.activeSelf) _boomerDecalRenderer.gameObject.SetActive(true);
                _boomerDecalRenderer.transform.position = new Vector3(center.x, 0.04f, center.z);
                _boomerDecalRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                HitboxSpriteLibrary.SetWorldDiameter(_boomerDecalRenderer, currentRadius * 2f);

                float pulseAlpha = Mathf.Lerp(0.25f, 0.65f, fuseProgress) + 0.15f * Mathf.PingPong(Time.time * 8f, 1f);
                _boomerDecalRenderer.color = new Color(1f, 0.12f, 0.08f, Mathf.Clamp01(pulseAlpha));
            }
        }

        private void HideBoomerTelegraphVisual()
        {
            if (_boomerCircleRenderer != null && _boomerCircleRenderer.gameObject.activeSelf)
            {
                _boomerCircleRenderer.gameObject.SetActive(false);
            }
            if (_boomerDecalRenderer != null && _boomerDecalRenderer.gameObject.activeSelf)
            {
                _boomerDecalRenderer.gameObject.SetActive(false);
            }
        }

        #endregion

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
                    Vector3 toPlayer = playerTransform.position - transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.01f)
                    {
                        transform.forward = toPlayer.normalized;
                    }
                    PlayActionAnimation("Throw", 0.85f, 0.06f);

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
    
        private System.Collections.IEnumerator ExecuteSpitterSpit()
        {
            _isSpitting = true;
            _spitCooldownTimer = IsRageMode ? (spitCooldown * rageSkillCooldownMultiplier) : spitCooldown;
            if (rb != null) rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;
            if (_spriteRenderer != null) _spriteRenderer.color = new Color(0.2f, 0.9f, 0.2f);

            // Lock firing direction at start of telegraph so players can read & dodge the ground hitbox lane!
            Vector3 lockedBaseDir = transform.forward;
            if (playerTransform != null)
            {
                Vector3 faceDir = playerTransform.position - transform.position;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.001f) lockedBaseDir = faceDir.normalized;
            }
            lockedBaseDir.y = 0f;
            if (lockedBaseDir.sqrMagnitude < 0.001f) lockedBaseDir = Vector3.forward;
            lockedBaseDir.Normalize();
            transform.forward = lockedBaseDir;

            // Boss spits a 3-way toxic spread when enraged, or single heavy acid blast normally
            float[] spreadAngles = (IsBossUnit && IsRageMode) ? new[] { -14f, 0f, 14f } : new[] { 0f };
            float projScale = IsBossUnit ? 1.35f : 1.0f;
            const float windupDuration = 0.48f;
            float laneLength = IsBossUnit ? 13.5f : 11.5f;
            float laneWidth = 1.05f * projScale;

            Vector3 telegraphOrigin = transform.position;
            foreach (float angleDeg in spreadAngles)
            {
                Vector3 dir = Quaternion.Euler(0f, angleDeg, 0f) * lockedBaseDir;
                SpitterProjectileVisual.SpawnSpitAimTelegraph(
                    SpitProjectileStyle.ZombieAcid,
                    telegraphOrigin + dir * 0.65f,
                    dir,
                    laneLength,
                    laneWidth,
                    windupDuration,
                    transform);
                TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                    this,
                    TheLastEmpire.Network.NetworkEnemyBridge.TelegraphZombieSpitAim,
                    telegraphOrigin + dir * 0.65f,
                    dir,
                    laneLength,
                    windupDuration);
            }

            PlayActionAnimationWithFallback(0.70f, 0.05f, IsBossUnit ? "Spit" : "Slash1", "RageRoar", "Throw", "Slash1");
            yield return new WaitForSeconds(windupDuration);

            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            if (health == null || !health.IsDead)
            {
                transform.forward = lockedBaseDir;

                foreach (float angleDeg in spreadAngles)
                {
                    Vector3 dir = Quaternion.Euler(0f, angleDeg, 0f) * lockedBaseDir;
                    Vector3 spawnPos = transform.position + dir * 0.85f + Vector3.up * (IsBossUnit ? 1.05f : 0.80f);
                    SpitterProjectileVisual.CreateSpitProjectile(
                        SpitProjectileStyle.ZombieAcid,
                        spawnPos,
                        dir,
                        acidProjectileSpeed,
                        gameObject,
                        projScale);
                    TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                        this,
                        TheLastEmpire.Network.NetworkEnemyBridge.TelegraphZombieSpit,
                        spawnPos,
                        dir,
                        projScale,
                        acidProjectileSpeed);
                }
            }

            _isSpitting = false;
            currentState = AIState.Chase;
        }
}
}
