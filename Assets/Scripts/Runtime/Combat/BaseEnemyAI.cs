using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract partial class BaseEnemyAI : MonoBehaviour
    {
        public enum AIState
        {
            Idle,
            Wander,
            Chase,
            Telegraph,
            Stagger
        }

        [Header("Base AI Settings")]
        [SerializeField] protected float moveSpeed = 3f;
        [SerializeField] protected float detectionRange = 5f;
        [SerializeField] protected string poolKey = "";

        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
        public float DetectionRange { get => detectionRange; set => detectionRange = value; }

        [Header("Wandering Settings")]
        [SerializeField] protected float wanderTime = 2f;
        [SerializeField] protected float idleTime = 1.5f;

        [Header("Stagger Settings")]
        [SerializeField] protected float staggerDuration = 0.3f;
        [SerializeField] protected float knockbackForce = 6f;
        protected float staggerTimer = 0f;
        protected bool isStaggered = false;

        public bool IsStaggered => isStaggered;

        [Header("Obstacle Avoidance / Smart Pathfinding")]
        [Tooltip("เปิดใช้งานระบบเดินอ้อมสิ่งกีดขวางอัจฉริยะ (A* Pathfinding + Context Steering)")]
        [SerializeField] protected bool useSmartObstacleAvoidance = false;

        public virtual bool UseSmartObstacleAvoidance
        {
            get => useSmartObstacleAvoidance;
            set => useSmartObstacleAvoidance = value;
        }

        protected Rigidbody rb;
        protected Transform playerTransform;
        protected Health playerHealth;
        protected Health health;
        protected Animator enemyAnimator;
        protected AIState currentState = AIState.Idle;

        private float _attackAnimCooldown = 0f;
        private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
        private static readonly int AnimIsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int AnimIsSittingHash = Animator.StringToHash("IsSitting");

        protected float stateTimer = 0f;
        protected Vector3 wanderDirection;
        protected bool hasBeenAttacked = false;

        public string PoolKey => poolKey;
        public AIState CurrentState => currentState;

        public void SetPoolKey(string key)
        {
            poolKey = key;
        }

        public void SkipTransitionDelay()
        {
            transitionDelayTimer = 0f;
        }

        protected float transitionDelayTimer = 2.0f;

        [Header("UI Reference")]
        [SerializeField] protected string enemyName = "Enemy";
        [SerializeField] protected TMP_Text nameText;
        [SerializeField] protected TMP_Text healthText;
        [SerializeField] protected UnityEngine.UI.Slider healthSlider;

        // ------------------------------------------------------------------
        // Temporary stat overrides (used by stage events, e.g. Fleeing Survivor chasers / Blood Moon).
        // Pooled enemies are reused across rooms, so event changes must NEVER permanently modify
        // moveSpeed / detectionRange. Natural stats are cached and restored when the enemy is re-enabled.
        // ------------------------------------------------------------------
        private bool _hasNaturalStats = false;
        private bool _hasTemporaryOverride = false;
        private float _naturalMoveSpeed;
        private float _naturalDetectionRange;
        private Coroutine _overrideRoutine;

        // ------------------------------------------------------------------
        // Chaser Phasing (used by Fleeing Survivor event to glide through obstacles)
        // ------------------------------------------------------------------
        private readonly System.Collections.Generic.Dictionary<Collider, bool> _originalColliderTriggerStates = new System.Collections.Generic.Dictionary<Collider, bool>();
        private bool _isChaserPhasing = false;
        private float _phasingGroundY = 0f;
        private Transform _phasingTargetPlayer = null;
        private float _phasingRestoreDistance = 4.5f;
        private Coroutine _phasingRoutine = null;

        public bool IsChaserPhasing => _isChaserPhasing;

        /// <summary>
        /// เปิดโหมดวิ่งทะลุสิ่งกีดขวาง (Trigger Collider) จนกว่าจะเข้าใกล้ผู้เล่นในระยะ restoreDistance (เช่น 4.5m)
        /// ป้องกันปัญหาศัตรูวิ่งติดตึก/กำแพง/กล่องในอีเวนต์วิ่งไล่ล่า
        /// </summary>
        public void EnableChaserPhasing(Transform targetPlayer, float restoreDistance = 4.5f)
        {
            if (!isActiveAndEnabled) return;

            DisableChaserPhasing();

            _phasingTargetPlayer = targetPlayer != null ? targetPlayer : playerTransform;
            _phasingRestoreDistance = restoreDistance;

            // คำนวณความสูงพื้นดินที่เหมาะสมจาก Collider ของศัตรูแต่ละชนิดโดยอัตโนมัติ (เช่น Humanoid = 1.0f, หมา = 0.5f)
            // เพื่อให้เท้าแตะพื้นระนาบ Y=0 พอดี ไม่จมดินและไม่ลอย
            float naturalGroundY = 1.0f;
            CapsuleCollider cap = GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                naturalGroundY = Mathf.Max(0.4f, -(cap.center.y - (cap.height * 0.5f) * transform.localScale.y));
            }
            else
            {
                Collider col = GetComponent<Collider>();
                if (col is BoxCollider box)
                {
                    naturalGroundY = Mathf.Max(0.4f, -(box.center.y - (box.size.y * 0.5f) * transform.localScale.y));
                }
                else if (col is SphereCollider sph)
                {
                    naturalGroundY = Mathf.Max(0.4f, -(sph.center.y - (sph.radius * transform.localScale.y)));
                }
            }

            _phasingGroundY = naturalGroundY;
            _isChaserPhasing = true;

            // ปรับตำแหน่ง Y ของศัตรูให้แนบพอดีกับพื้นทันที ไม่ลอยและไม่จมดิน
            Vector3 groundPos = transform.position;
            groundPos.y = _phasingGroundY;
            transform.position = groundPos;

            // บันทึกสถานะ Trigger เดิมของ Collider ทุกตัว แล้วเปลี่ยนเป็น Trigger เพื่อทะลุสิ่งกีดขวาง
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (!_originalColliderTriggerStates.ContainsKey(c))
                {
                    _originalColliderTriggerStates[c] = c.isTrigger;
                }
                c.isTrigger = true;
            }

            if (rb != null)
            {
                rb.useGravity = false;
                Vector3 v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(v.x, 0f, v.z);
            }

            _phasingRoutine = StartCoroutine(ChaserPhasingMonitorRoutine());
        }

        public void DisableChaserPhasing()
        {
            if (!_isChaserPhasing && _originalColliderTriggerStates.Count == 0) return;
            _isChaserPhasing = false;

            if (_phasingRoutine != null)
            {
                StopCoroutine(_phasingRoutine);
                _phasingRoutine = null;
            }

            // คืนค่า Trigger กลับสู่สภาพปกติของ Collider ทุกตัว
            foreach (var kvp in _originalColliderTriggerStates)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.isTrigger = kvp.Value;
                }
            }
            _originalColliderTriggerStates.Clear();

            if (rb != null)
            {
                rb.useGravity = true;
            }
        }

        private System.Collections.IEnumerator ChaserPhasingMonitorRoutine()
        {
            float elapsed = 0f;
            while (_isChaserPhasing)
            {
                elapsed += Time.deltaTime;

                // ล็อคระดับความสูง Y ให้อยู่ระดับพื้นเดิม ไม่ให้ตกแมพ
                Vector3 pos = transform.position;
                if (Mathf.Abs(pos.y - _phasingGroundY) > 0.01f)
                {
                    pos.y = _phasingGroundY;
                    transform.position = pos;
                }

                if (rb != null && rb.linearVelocity.y != 0f)
                {
                    Vector3 v = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(v.x, 0f, v.z);
                }

                if (_phasingTargetPlayer == null)
                {
                    if (playerTransform != null)
                    {
                        _phasingTargetPlayer = playerTransform;
                    }
                    else
                    {
                        PlayerController pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                        if (pc != null)
                        {
                            _phasingTargetPlayer = pc.transform;
                            playerTransform = pc.transform;
                        }
                    }
                }

                if (_phasingTargetPlayer != null)
                {
                    float dist = Vector3.Distance(transform.position, _phasingTargetPlayer.position);
                    if (dist <= _phasingRestoreDistance)
                    {
                        DisableChaserPhasing();
                        yield break;
                    }
                }

                // หมดเวลา Safe Timeout 20 วินาที คืนค่า Collision อัตโนมัติ
                if (elapsed >= 20f)
                {
                    DisableChaserPhasing();
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Apply event-only stats. speed/detection &lt; 0 = keep natural value. speedMultiplier is applied on top.
        /// Applied one frame later so subclass Start() stat setup cannot overwrite it.
        /// </summary>
        public void ApplyTemporaryStats(float speed = -1f, float detection = -1f, float speedMultiplier = 1f)
        {
            if (!isActiveAndEnabled) return;
            if (_overrideRoutine != null) StopCoroutine(_overrideRoutine);
            _overrideRoutine = StartCoroutine(ApplyTemporaryStatsRoutine(speed, detection, speedMultiplier));
        }

        private System.Collections.IEnumerator ApplyTemporaryStatsRoutine(float speed, float detection, float speedMultiplier)
        {
            yield return null; // wait until Start() (and subclass stat setup) has run

            if (!_hasNaturalStats || !_hasTemporaryOverride)
            {
                _naturalMoveSpeed = moveSpeed;
                _naturalDetectionRange = detectionRange;
                _hasNaturalStats = true;
            }

            moveSpeed = (speed >= 0f ? speed : _naturalMoveSpeed) * speedMultiplier;
            detectionRange = detection >= 0f ? detection : _naturalDetectionRange;
            _hasTemporaryOverride = true;
            _overrideRoutine = null;
        }

        private void RestoreNaturalStats()
        {
            DisableChaserPhasing();
            if (_overrideRoutine != null)
            {
                StopCoroutine(_overrideRoutine);
                _overrideRoutine = null;
            }
            if (_hasTemporaryOverride && _hasNaturalStats)
            {
                moveSpeed = _naturalMoveSpeed;
                detectionRange = _naturalDetectionRange;
            }
            _hasTemporaryOverride = false;
        }

        protected virtual void OnEnable()
        {
            RestoreNaturalStats(); // pooled enemy reused -> drop any event stats from the previous room
            DisableChaserPhasing();
            ResetSmartNavigationState();
            transitionDelayTimer = 2.0f; // Lock actions for 2s on spawn/activation
            hasBeenAttacked = false;
        }

        protected virtual void OnDisable()
        {
            DisableChaserPhasing();
            ResetSmartNavigationState();
        }

        protected virtual void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            health = GetComponent<Health>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
            }
            health.onDeath.AddListener(HandleDeath);
            health.onDamageTaken.AddListener(OnDamageTaken);

            if (nameText != null)
            {
                nameText.text = enemyName;
            }

            health.onHealthChanged.AddListener((h) => UpdateHealthUI());
            UpdateHealthUI();

            FindPlayer();
            enemyAnimator = GetComponentInChildren<Animator>();
            if (enemyAnimator != null)
            {
                enemyAnimator.applyRootMotion = false;
            }
            ChooseNextWanderState();
        }

        protected virtual void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

            if (IsNetworkPuppet)
            {
                FixedUpdateNetworkPuppet();
                return;
            }

            UpdateMultiplayerTargetAggro();

            // Chaser Phasing: ensure the enemy stays on ground level and does not drift vertically
            if (_isChaserPhasing)
            {
                Vector3 cPos = transform.position;
                if (Mathf.Abs(cPos.y - _phasingGroundY) > 0.01f)
                {
                    cPos.y = _phasingGroundY;
                    transform.position = cPos;
                }
                if (rb != null && rb.linearVelocity.y != 0f)
                {
                    Vector3 v = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(v.x, 0f, v.z);
                }
            }

            // Fall-out-of-world safeguard: teleport back to center of stage if they fall too low
            if (transform.position.y < -5.0f)
            {
                Debug.LogWarning($"[BaseEnemyAI] {gameObject.name} fell out of the world! Teleporting back to safe ground.");
                transform.position = new Vector3(Random.Range(-5f, 5f), 0.2f, Random.Range(-3f, 3f));
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }

            if (isStaggered)
            {
                staggerTimer -= Time.fixedDeltaTime;
                // Apply friction decay to make knockback slide smoothly to a halt
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
                if (staggerTimer <= 0f)
                {
                    isStaggered = false;
                    rb.linearVelocity = Vector3.zero;
                    currentState = AIState.Chase;
                }
                return;
            }

            if (transitionDelayTimer > 0f)
            {
                transitionDelayTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = Vector3.zero;
                currentState = AIState.Idle;
                UpdateEnemyAnimator();
                return;
            }

            UpdateAIBehavior();
            UpdateEnemyAnimator();
        }

        private bool _useSecondSlashAttack = false;
        private static readonly int Slash2StateHash = Animator.StringToHash("Slash2");

        public void PlayActionAnimation(string stateName, float lockDuration = 0.75f, float crossFade = 0.06f)
        {
            if (enemyAnimator == null) enemyAnimator = GetComponentInChildren<Animator>();
            if (enemyAnimator == null || !enemyAnimator.isActiveAndEnabled || enemyAnimator.runtimeAnimatorController == null) return;

            int stateHash = Animator.StringToHash(stateName);
            if (!enemyAnimator.HasState(0, stateHash)) return;

            _attackAnimCooldown = Mathf.Max(_attackAnimCooldown, lockDuration);
            enemyAnimator.CrossFadeInFixedTime(stateName, crossFade, 0);
        }

        public void ClearActionAnimationLock()
        {
            _attackAnimCooldown = 0f;
        }

        public void TriggerAttackAnimation()
        {
            if (enemyAnimator == null) enemyAnimator = GetComponentInChildren<Animator>();
            if (enemyAnimator == null || !enemyAnimator.isActiveAndEnabled || enemyAnimator.runtimeAnimatorController == null) return;

            string attackState = "Slash1";
            if (enemyAnimator.HasState(0, Slash2StateHash))
            {
                attackState = _useSecondSlashAttack ? "Slash2" : "Slash1";
                _useSecondSlashAttack = !_useSecondSlashAttack;
            }

            _attackAnimCooldown = 0.72f;
            enemyAnimator.CrossFadeInFixedTime(attackState, 0.06f, 0);
        }

        protected virtual void UpdateEnemyAnimator()
        {
            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponentInChildren<Animator>();
                if (enemyAnimator != null) enemyAnimator.applyRootMotion = false;
            }
            if (enemyAnimator == null || !enemyAnimator.isActiveAndEnabled || enemyAnimator.runtimeAnimatorController == null) return;

            if (_attackAnimCooldown > 0f)
            {
                _attackAnimCooldown -= Time.fixedDeltaTime;
            }

            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            float horizSpeed = new Vector2(vel.x, vel.z).magnitude;
            bool isMoving = horizSpeed > 0.12f;
            bool isRunning = isMoving && (currentState == AIState.Chase || currentState == AIState.Telegraph || horizSpeed > moveSpeed * 0.65f);

            // Auto-trigger melee attack swing when in close contact range with the player
            if (_attackAnimCooldown <= 0f && playerTransform != null && !IsTargetDead())
            {
                float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                if (distToPlayer <= 1.45f)
                {
                    TriggerAttackAnimation();
                    return;
                }
            }

            if (_attackAnimCooldown > 0.15f)
            {
                return;
            }

            AnimatorStateInfo stateInfo = enemyAnimator.GetCurrentAnimatorStateInfo(0);
            string desiredState = !isMoving ? "Idle" : (isRunning ? "Run" : "Walk");
            if (!stateInfo.IsName(desiredState) && !enemyAnimator.IsInTransition(0))
            {
                enemyAnimator.CrossFadeInFixedTime(desiredState, 0.10f, 0);
            }
        }

        protected abstract void UpdateAIBehavior();

        protected virtual void FindPlayer()
        {
            PlayerController player = TheLastEmpire.Network.PlayerRegistry.GetClosestAlivePlayer(transform.position)
                                   ?? UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
                playerHealth = player.PlayerHealth ?? player.GetComponent<Health>();
            }
        }

        protected bool IsTargetDead()
        {
            if (playerHealth == null && playerTransform != null)
            {
                playerHealth = playerTransform.GetComponent<Health>();
            }
            if (playerHealth != null && playerHealth.IsDead)
            {
                hasBeenAttacked = false; // Drop aggro when target dies
                return true;
            }
            return false;
        }

        protected void WanderMovement(float speedMultiplier = 1f)
        {
            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f)
            {
                ChooseNextWanderState();
            }

            if (currentState == AIState.Wander)
            {
                if (UseSmartObstacleAvoidance && !_isChaserPhasing && wanderDirection.sqrMagnitude > 0.01f)
                {
                    // ถ้าระหว่างเดินลาดตระเวน (Wander) เจอกำแพงหรือสิ่งกีดขวางตรงหน้า ให้เบี่ยงทิศทางเดินอ้อมทันที
                    if (IsObstacleDirectlyAhead(wanderDirection, GetAgentRadius() + 0.75f))
                    {
                        wanderDirection = ComputeLocalSteeredDirection(wanderDirection, GetAgentRadius());
                    }
                }

                float spd = moveSpeed * speedMultiplier * 0.5f;
                rb.linearVelocity = new Vector3(wanderDirection.x * spd, rb.linearVelocity.y, wanderDirection.z * spd);
                // Rotate visual to wander direction
                if (wanderDirection.sqrMagnitude > 0.01f)
                {
                    transform.forward = wanderDirection;
                }
            }
            else
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }

        private void ChooseNextWanderState()
        {
            if (Random.value > 0.4f)
            {
                currentState = AIState.Wander;
                stateTimer = Random.Range(wanderTime * 0.5f, wanderTime * 1.5f);
                wanderDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;

                if (UseSmartObstacleAvoidance && wanderDirection.sqrMagnitude > 0.01f)
                {
                    wanderDirection = ComputeLocalSteeredDirection(wanderDirection, GetAgentRadius());
                }
            }
            else
            {
                currentState = AIState.Idle;
                stateTimer = Random.Range(idleTime * 0.5f, idleTime * 1.5f);
                wanderDirection = Vector3.zero;
            }
        }

        protected void ChasePlayer(float speedMultiplier = 1f)
        {
            if (playerTransform == null)
            {
                FindPlayer();
                rb.linearVelocity = Vector3.zero;
                return;
            }

            // Immediately break chase and start wandering if player is dead
            if (IsTargetDead())
            {
                rb.linearVelocity = Vector3.zero;
                currentState = AIState.Wander;
                return;
            }

            Vector3 direction;
            if (UseSmartObstacleAvoidance && !_isChaserPhasing)
            {
                direction = ComputeSmartChaseDirection(playerTransform.position);
            }
            else
            {
                direction = (playerTransform.position - transform.position);
                direction.y = 0f;
                direction.Normalize();
            }

            float finalSpeed = moveSpeed * speedMultiplier;
            rb.linearVelocity = new Vector3(direction.x * finalSpeed, rb.linearVelocity.y, direction.z * finalSpeed);

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.forward = direction;
            }
        }

        /// <summary>
        /// เคลื่อนที่ไปตามทิศทางที่กำหนดพร้อมระบบหลบหลีกสิ่งกีดขวางเฉพาะจุด (เช่น กรณี Alien Smart เดินวนรอบผู้เล่น)
        /// </summary>
        protected void MoveInSmartDirection(Vector3 desiredDirection, float speedMultiplier = 1f, Vector3? lookDirectionOverride = null)
        {
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.0001f)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }
            desiredDirection.Normalize();

            Vector3 moveDir = (UseSmartObstacleAvoidance && !_isChaserPhasing)
                ? ApplyWallSlideFilter(ComputeLocalSteeredDirection(desiredDirection, GetAgentRadius()))
                : desiredDirection;

            float finalSpeed = moveSpeed * speedMultiplier;
            rb.linearVelocity = new Vector3(moveDir.x * finalSpeed, rb.linearVelocity.y, moveDir.z * finalSpeed);

            Vector3 lookDir = lookDirectionOverride ?? moveDir;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.forward = lookDir.normalized;
            }
        }

        protected bool IsPlayerInDetectionRange()
        {
            if (playerTransform == null) return false;
            if (IsTargetDead()) return false; // Completely ignore corpse
            if (hasBeenAttacked) return true; // Aggro locked on player after taking damage
            return Vector3.Distance(transform.position, playerTransform.position) <= detectionRange;
        }

        protected virtual void HandleDeath()
        {
            DisableChaserPhasing();
            Debug.Log($"[BaseEnemyAI] Defeated {gameObject.name}");

            // Reward sanity to the player upon kill
            if (playerTransform != null)
            {
                PlayerController pc = playerTransform.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.OnEnemyDefeated(this);
                }
            }

            if (!string.IsNullOrEmpty(poolKey) && ObjectPoolManager.Instance != null)
            {
                health.ResetHealth();
                ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (health != null)
            {
                health.onDeath.RemoveListener(HandleDeath);
                health.onDamageTaken.RemoveListener(OnDamageTaken);
            }
        }

        protected virtual void OnDamageTaken(float damageAmount)
        {
            if (health != null && health.IsDead) return;

            DisableChaserPhasing();
            hasBeenAttacked = true;
            isStaggered = true;
            staggerTimer = staggerDuration;
            currentState = AIState.Stagger;

            // Apply knockback force away from the player's position
            if (playerTransform != null)
            {
                Vector3 knockbackDir = (transform.position - playerTransform.position);
                knockbackDir.y = 0f;
                knockbackDir.Normalize();
                rb.linearVelocity = knockbackDir * knockbackForce;
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        public void AlertToPlayer()
        {
            if (health != null && health.IsDead) return;
            hasBeenAttacked = true;
            currentState = AIState.Chase;
        }

        public void ApplyMeleeKnockback(Vector3 direction, float force, float duration)
        {
            if (health != null && health.IsDead) return;

            hasBeenAttacked = true;
            isStaggered = true;
            staggerTimer = duration;
            currentState = AIState.Stagger;

            if (rb != null)
            {
                rb.linearVelocity = direction * force;
            }
            Debug.Log($"[BaseEnemyAI] Melee Knockback applied: {direction * force}");
        }

        public void ApplyDamageKnockback(Vector3 knockbackVector)
        {
            if (knockbackVector.sqrMagnitude < 0.001f) return;
            ApplyMeleeKnockback(knockbackVector.normalized, knockbackVector.magnitude, staggerDuration);
        }

        public void ApplyDamageKnockback(Vector3 direction, float force)
        {
            ApplyMeleeKnockback(direction.normalized, force, staggerDuration);
        }

        protected void UpdateHealthUI()
        {
            if (health == null) return;

            if (healthText != null)
            {
                if (health.IsDead)
                {
                    healthText.text = "";
                }
                else
                {
                    healthText.text = $"{Mathf.RoundToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}";
                }
            }

            if (healthSlider != null)
            {
                if (health.IsDead)
                {
                    healthSlider.gameObject.SetActive(false);
                }
                else
                {
                    healthSlider.gameObject.SetActive(true);
                    healthSlider.maxValue = health.MaxHealth;
                    healthSlider.value = health.CurrentHealth;
                }
            }
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.GetComponent<CollectibleItem>() != null)
            {
                Collider myCol = GetComponent<Collider>();
                if (myCol != null && collision.collider != null)
                {
                    Physics.IgnoreCollision(myCol, collision.collider, true);
                }
            }
            else if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<PlayerController>() != null)
            {
                Health pHealth = collision.gameObject.GetComponent<Health>();
                if (pHealth != null && pHealth.IsDead)
                {
                    Collider myCol = GetComponent<Collider>();
                    if (myCol != null && collision.collider != null)
                    {
                        Physics.IgnoreCollision(myCol, collision.collider, true);
                    }
                }
            }

            RecordObstacleContact(collision);
        }

        protected virtual void OnCollisionStay(Collision collision)
        {
            RecordObstacleContact(collision);
        }

        // =====================================================================
        // SMART OBSTACLE AVOIDANCE & DYNAMIC 2D GRID A* PATHFINDING
        // =====================================================================

        private const int AStarCols = 56;
        private const int AStarRows = 36;
        private const int AStarTotalNodes = AStarCols * AStarRows;
        private const float AStarCellSize = 0.68f;

        // Static reusable buffers for zero-GC A* and physics queries on main thread
        private static readonly int[] s_gCost = new int[AStarTotalNodes];
        private static readonly int[] s_fCost = new int[AStarTotalNodes];
        private static readonly int[] s_parentIndex = new int[AStarTotalNodes];
        private static readonly byte[] s_nodeState = new byte[AStarTotalNodes]; // 0 = unvisited, 1 = open, 2 = closed
        private static readonly byte[] s_walkableCache = new byte[AStarTotalNodes]; // 0 = unknown, 1 = walkable, 2 = blocked
        private static readonly int[] s_openHeap = new int[AStarTotalNodes];
        private static readonly Collider[] s_overlapBuffer = new Collider[32];
        private static readonly RaycastHit[] s_rayHitBuffer = new RaycastHit[32];

        private static readonly float[] s_steerAngles = new float[]
        {
            0f,
            20f, -20f,
            40f, -40f,
            62f, -62f,
            85f, -85f,
            112f, -112f,
            140f, -140f,
            165f, -165f
        };

        private readonly System.Collections.Generic.List<Vector3> _navPath = new System.Collections.Generic.List<Vector3>(32);
        private int _navPathIndex = 0;
        private float _nextPathRepathTime = 0f;
        private Vector3 _lastPathTargetPos = new Vector3(9999f, 9999f, 9999f);
        private Vector3 _smoothedMoveDir = Vector3.zero;
        private float _committedSteerSign = 0f;
        private float _committedSteerExpireTime = 0f;
        private float _cachedAgentRadius = -1f;

        // Stuck detection & recovery
        private Vector3 _lastStuckCheckPos;
        private float _stuckCheckTimer = 0f;
        private float _unstuckActiveTimer = 0f;
        private Vector3 _unstuckMoveDir = Vector3.zero;

        // Active wall contact normal for frictionless wall sliding
        private Vector3 _lastObstacleNormal = Vector3.zero;
        private float _lastObstacleContactTime = -10f;

        protected void ResetSmartNavigationState()
        {
            _navPath.Clear();
            _navPathIndex = 0;
            _nextPathRepathTime = 0f;
            _lastPathTargetPos = new Vector3(9999f, 9999f, 9999f);
            _smoothedMoveDir = Vector3.zero;
            _committedSteerSign = 0f;
            _committedSteerExpireTime = 0f;
            _stuckCheckTimer = 0f;
            _unstuckActiveTimer = 0f;
            _lastStuckCheckPos = transform.position;
            _lastObstacleContactTime = -10f;
        }

        protected float GetAgentRadius()
        {
            if (_cachedAgentRadius > 0.01f) return _cachedAgentRadius;

            float r = 0.45f;
            if (TryGetComponent<CapsuleCollider>(out var cap))
            {
                float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
                r = cap.radius * scaleXZ;
            }
            else if (TryGetComponent<SphereCollider>(out var sph))
            {
                float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
                r = sph.radius * scaleXZ;
            }
            else if (TryGetComponent<BoxCollider>(out var box))
            {
                float scaleX = Mathf.Abs(transform.lossyScale.x) * box.size.x;
                float scaleZ = Mathf.Abs(transform.lossyScale.z) * box.size.z;
                r = Mathf.Max(scaleX, scaleZ) * 0.5f;
            }
            else if (TryGetComponent<Collider>(out var col))
            {
                r = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }

            _cachedAgentRadius = Mathf.Clamp(r, 0.30f, 1.10f);
            return _cachedAgentRadius;
        }

        private float GetProbeHeight(float sphereRadius)
        {
            // Keep bottom of sphere at >= 0.35m above ground (y=0) so flat ground/road colliders are never touched,
            // while catching both low obstacles (crates, rocks, cars, fences) and tall walls/buildings.
            return Mathf.Max(0.85f, sphereRadius + 0.38f);
        }

        protected bool IsSolidObstacleCollider(Collider col, bool includeOtherEnemies = false)
        {
            if (col == null || !col.enabled || col.isTrigger) return false;

            // 1. Ignore self
            if (col.transform == transform || col.transform.IsChildOf(transform)) return false;

            // 2. Ignore Player
            if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null) return false;

            // 3. Ignore other Enemies (unless checking local separation)
            if (col.CompareTag("Enemy") || col.GetComponentInParent<BaseEnemyAI>() != null)
            {
                return includeOtherEnemies;
            }

            // 4. Ignore Collectibles & Projectiles
            if (col.GetComponentInParent<CollectibleItem>() != null || col.GetComponentInParent<Projectile>() != null)
            {
                return false;
            }

            // 5. Ignore Ground, Terrain, Walkable Floors, Passable Traps, and Open Doors
            if (col.CompareTag("Ground")) return false;
            if (col.GetComponentInParent<Terrain>() != null) return false;
            if (col.GetComponentInParent<FloorBuilding>() != null) return false;
            if (col.GetComponentInParent<BearTrapBuilding>() != null) return false;
            if (col.GetComponentInParent<BileLandmineBuilding>() != null) return false;
            if (col.GetComponentInParent<RazorWireBuilding>() != null) return false;

            DoorBuilding door = col.GetComponentInParent<DoorBuilding>();
            if (door != null && door.IsOpen) return false;

            string colName = col.gameObject.name.ToLowerInvariant();
            if (colName.Contains("ground") ||
                colName.Contains("floor") ||
                colName.Contains("road") ||
                colName.Contains("street") ||
                colName.Contains("terrain") ||
                colName.Contains("plane") ||
                colName.Contains("sidewalk") ||
                colName.Contains("walkway"))
            {
                return false;
            }

            // Flat floor/tile colliders near ground level are walkable surfaces
            Bounds b = col.bounds;
            if (b.max.y <= 0.30f && b.size.y <= 0.50f)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// ตรวจสอบว่ามีแนวสายตา/ทางโล่งตรงไปยังผู้เล่นหรือไม่ (ไม่มีกำแพงหรือสิ่งกีดขวางกั้นกลาง)
        /// </summary>
        protected bool HasLineOfSightToPlayer(float maxCheckDist = -1f)
        {
            if (playerTransform == null) return false;

            Vector3 from = transform.position;
            Vector3 to = playerTransform.position;
            Vector3 diff = to - from;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= 0.25f) return true;
            if (maxCheckDist > 0f && dist > maxCheckDist) dist = maxCheckDist;

            Vector3 dir = diff / dist;
            float probeRadius = Mathf.Clamp(GetAgentRadius() * 0.55f, 0.20f, 0.45f);
            float probeY = GetProbeHeight(probeRadius);
            Vector3 origin = new Vector3(from.x, probeY, from.z);
            float castDist = Mathf.Max(0.1f, dist - 0.35f);

            int hitCount = Physics.SphereCastNonAlloc(origin, probeRadius, dir, s_rayHitBuffer, castDist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsSolidObstacleCollider(s_rayHitBuffer[i].collider, false))
                {
                    return false;
                }
            }

            int rayCount = Physics.RaycastNonAlloc(origin, dir, s_rayHitBuffer, castDist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayCount; i++)
            {
                if (IsSolidObstacleCollider(s_rayHitBuffer[i].collider, false))
                {
                    return false;
                }
            }

            return true;
        }

        protected bool IsObstacleDirectlyAhead(Vector3 direction, float checkDistance, float radiusScale = 0.75f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();

            float probeRadius = Mathf.Clamp(GetAgentRadius() * radiusScale, 0.22f, 0.65f);
            float probeY = GetProbeHeight(probeRadius);
            Vector3 origin = new Vector3(transform.position.x, probeY, transform.position.z);

            int hitCount = Physics.SphereCastNonAlloc(origin, probeRadius, direction, s_rayHitBuffer, checkDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsSolidObstacleCollider(s_rayHitBuffer[i].collider, false))
                {
                    return true;
                }
            }

            int rayCount = Physics.RaycastNonAlloc(origin, direction, s_rayHitBuffer, checkDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayCount; i++)
            {
                if (IsSolidObstacleCollider(s_rayHitBuffer[i].collider, false))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasClearCorridor(Vector3 from, Vector3 to, float clearanceRadius, bool stopShortForTarget = false)
        {
            Vector3 diff = to - from;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= 0.20f) return true;

            Vector3 dir = diff / dist;
            float castDist = stopShortForTarget ? Mathf.Max(0.1f, dist - (GetAgentRadius() + 0.30f)) : dist;
            float probeRadius = Mathf.Clamp(clearanceRadius, 0.22f, 0.75f);
            float probeY = GetProbeHeight(probeRadius);
            Vector3 origin = new Vector3(from.x, probeY, from.z);

            int hitCount = Physics.SphereCastNonAlloc(origin, probeRadius, dir, s_rayHitBuffer, castDist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsSolidObstacleCollider(s_rayHitBuffer[i].collider, false))
                {
                    return false;
                }
            }

            int rayCount = Physics.RaycastNonAlloc(origin, dir, s_rayHitBuffer, castDist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayCount; i++)
            {
                if (IsSolidObstacleCollider(s_rayHitBuffer[i].collider, false))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsWorldPointWalkable(float worldX, float worldZ, float clearanceRadius)
        {
            float probeY = GetProbeHeight(clearanceRadius);
            Vector3 pos = new Vector3(worldX, probeY, worldZ);
            int count = Physics.OverlapSphereNonAlloc(pos, clearanceRadius, s_overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsSolidObstacleCollider(s_overlapBuffer[i], false))
                {
                    return false;
                }
            }
            return true;
        }

        protected Vector3 ComputeSmartChaseDirection(Vector3 targetPos)
        {
            Vector3 currentPos = transform.position;
            Vector3 toTarget = targetPos - currentPos;
            toTarget.y = 0f;
            float distToTarget = toTarget.magnitude;
            if (distToTarget < 0.05f) return transform.forward;

            Vector3 directToTarget = toTarget / distToTarget;
            float agentRadius = GetAgentRadius();
            float corridorRadius = Mathf.Clamp(agentRadius * 0.80f, 0.32f, 0.62f);

            // 1. Stuck Monitor: หากเดินติดซอกกำแพงจนตำแหน่งไม่ขยับ ให้ถอย/สไลด์ออกทางที่โล่งที่สุดทันที
            UpdateStuckMonitor(currentPos, targetPos, agentRadius);
            if (_unstuckActiveTimer > 0f)
            {
                _unstuckActiveTimer -= Time.fixedDeltaTime;
                Vector3 unstuckSteered = ApplyWallSlideFilter(_unstuckMoveDir);
                _smoothedMoveDir = Vector3.Slerp(_smoothedMoveDir.sqrMagnitude > 0.01f ? _smoothedMoveDir : unstuckSteered, unstuckSteered, Time.fixedDeltaTime * 14f).normalized;
                return _smoothedMoveDir.sqrMagnitude > 0.01f ? _smoothedMoveDir : directToTarget;
            }

            Vector3 desiredDir = directToTarget;

            // 2. หากมีทางตรงโล่งถึงตัวผู้เล่น เดินตรงเข้าหาได้ทันทีโดยไม่ต้องใช้ A* Waypoints
            if (HasClearCorridor(currentPos, targetPos, corridorRadius, true))
            {
                _navPath.Clear();
                _navPathIndex = 0;
                desiredDir = directToTarget;
            }
            else
            {
                // 3. มีสิ่งกีดขวางกั้นกลาง -> คำนวณเส้นทางอ้อมด้วย 2D Grid A* Pathfinding
                bool targetMovedSignificantly = (targetPos - _lastPathTargetPos).sqrMagnitude > 0.75f * 0.75f;
                bool needRepath = Time.time >= _nextPathRepathTime || _navPath.Count == 0 || _navPathIndex >= _navPath.Count || targetMovedSignificantly;

                if (needRepath)
                {
                    _nextPathRepathTime = Time.time + Random.Range(0.22f, 0.32f);
                    _lastPathTargetPos = targetPos;
                    BuildAStarPath(currentPos, targetPos, corridorRadius);
                }

                // String-Pulling & Waypoint Advancement: ข้ามไปยัง Waypoint ถัดไปทันทีหากเดินถึงแล้วหรือมองเห็นทางโล่งถึงจุดถัดไป
                while (_navPathIndex < _navPath.Count)
                {
                    Vector3 wp = _navPath[_navPathIndex];
                    float dWp = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(wp.x, wp.z));
                    if (dWp <= 0.55f)
                    {
                        _navPathIndex++;
                        continue;
                    }

                    if (_navPathIndex + 1 < _navPath.Count &&
                        HasClearCorridor(currentPos, _navPath[_navPathIndex + 1], corridorRadius * 0.85f, false))
                    {
                        _navPathIndex++;
                        continue;
                    }
                    break;
                }

                if (_navPathIndex < _navPath.Count)
                {
                    Vector3 activeWaypoint = _navPath[_navPathIndex];
                    Vector3 toWp = activeWaypoint - currentPos;
                    toWp.y = 0f;
                    if (toWp.sqrMagnitude > 0.001f)
                    {
                        desiredDir = toWp.normalized;
                    }
                }
            }

            // 4. Local Context Steering: ตรวจวัดระยะรอบตัวหลายมุมเพื่อเลี้ยวพ้นขอบตึก/มุมกำแพง/เพื่อนร่วมทีมอย่างนุ่มนวล
            Vector3 steeredDir = ComputeLocalSteeredDirection(desiredDir, agentRadius);

            // 5. Wall Slide Filter: หากตัวชนสัมผัสกำแพงอยู่ ให้ตัดแรงที่ดันอัดเข้าหากำแพงออกเพื่อไถลไปตามแนวกำแพงได้ลื่นไหล
            steeredDir = ApplyWallSlideFilter(steeredDir);

            if (_smoothedMoveDir.sqrMagnitude < 0.01f)
            {
                _smoothedMoveDir = steeredDir;
            }
            else
            {
                _smoothedMoveDir = Vector3.Slerp(_smoothedMoveDir, steeredDir, Time.fixedDeltaTime * 14f);
                _smoothedMoveDir.y = 0f;
                if (_smoothedMoveDir.sqrMagnitude > 0.001f)
                {
                    _smoothedMoveDir.Normalize();
                }
                else
                {
                    _smoothedMoveDir = steeredDir;
                }
            }

            return _smoothedMoveDir;
        }

        private void UpdateStuckMonitor(Vector3 currentPos, Vector3 targetPos, float agentRadius)
        {
            _stuckCheckTimer += Time.fixedDeltaTime;
            if (_stuckCheckTimer < 0.38f) return;

            float elapsed = _stuckCheckTimer;
            _stuckCheckTimer = 0f;

            float movedDist = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(_lastStuckCheckPos.x, _lastStuckCheckPos.z));
            _lastStuckCheckPos = currentPos;

            float distToTarget = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(targetPos.x, targetPos.z));
            float expectedMinMove = Mathf.Min(0.22f, moveSpeed * elapsed * 0.18f);

            // หากอยู่ห่างจากผู้เล่นเกินระยะประชิด แต่ตำแหน่งแทบไม่ขยับเลย แปลว่าติดมุมอับ/ขอบกำแพง
            if (distToTarget > (agentRadius + 1.15f) && movedDist < expectedMinMove)
            {
                _nextPathRepathTime = 0f; // บังคับคำนวณ A* ใหม่ทันที
                _committedSteerSign = -_committedSteerSign;

                // ค้นหาทิศทางที่โล่งที่สุดรอบตัว 360 องศาเพื่อหลุดออกจากมุมอับ
                Vector3 toTarget = (targetPos - currentPos);
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.01f) toTarget = transform.forward;
                toTarget.Normalize();

                Vector3 bestEscape = -toTarget;
                float bestScore = -999f;
                float probeRadius = Mathf.Clamp(agentRadius * 0.65f, 0.25f, 0.50f);
                float probeY = GetProbeHeight(probeRadius);
                Vector3 origin = new Vector3(currentPos.x, probeY, currentPos.z);

                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 30f;
                    Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * toTarget;
                    float freeDist = GetDirectionClearance(origin, candidate, probeRadius, 2.8f, true);
                    if (freeDist < 0.65f) continue;

                    // ให้คะแนนทิศทางที่โล่งและทำมุมเฉียงออกด้านข้าง (60-135 องศา) เพื่ออ้อมสิ่งกีดขวาง
                    float dot = Vector3.Dot(candidate, toTarget);
                    float flankBonus = 1f - Mathf.Abs(dot - 0.15f);
                    float score = freeDist * 1.5f + flankBonus;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestEscape = candidate;
                    }
                }

                _unstuckMoveDir = bestEscape.normalized;
                _unstuckActiveTimer = 0.35f;
            }
        }

        protected Vector3 ComputeLocalSteeredDirection(Vector3 desiredDir, float agentRadius)
        {
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude < 0.0001f) return transform.forward;
            desiredDir.Normalize();

            float lookAhead = Mathf.Clamp(agentRadius * 2.2f + 0.75f, 1.35f, 2.6f);
            float feelerRadius = Mathf.Clamp(agentRadius * 0.68f, 0.24f, 0.55f);
            float probeY = GetProbeHeight(feelerRadius);
            Vector3 origin = new Vector3(transform.position.x, probeY, transform.position.z);

            // ถ้าทิศทางที่ต้องการไปโล่งตลอดระยะ lookAhead ให้เดินไปทางนั้นได้เลย
            float forwardClearance = GetDirectionClearance(origin, desiredDir, feelerRadius, lookAhead, true);
            if (forwardClearance >= lookAhead * 0.96f)
            {
                return desiredDir;
            }

            if (Time.time > _committedSteerExpireTime)
            {
                _committedSteerSign = 0f;
            }

            Vector3 bestDir = desiredDir;
            float bestScore = -9999f;

            for (int i = 0; i < s_steerAngles.Length; i++)
            {
                float angle = s_steerAngles[i];
                Vector3 candidateDir = Quaternion.Euler(0f, angle, 0f) * desiredDir;
                float clearance = GetDirectionClearance(origin, candidateDir, feelerRadius, lookAhead, true);
                float normalizedClearance = clearance / lookAhead;

                // หากทางข้างหน้าตันในระยะใกล้มาก ให้ตัดคะแนนอย่างหนัก
                if (clearance < agentRadius + 0.25f)
                {
                    normalizedClearance -= 2.0f;
                }

                float alignment = Vector3.Dot(candidateDir, desiredDir); // 1.0 at 0 deg, 0.0 at 90 deg, -1.0 at 180 deg
                float continuity = (_smoothedMoveDir.sqrMagnitude > 0.01f) ? Vector3.Dot(candidateDir, _smoothedMoveDir) * 0.35f : 0f;

                float sideSign = Mathf.Sign(angle);
                float commitmentBonus = 0f;
                if (Mathf.Abs(angle) > 1f && Mathf.Abs(_committedSteerSign) > 0.1f && sideSign == _committedSteerSign)
                {
                    commitmentBonus = 0.40f;
                }

                float score = (normalizedClearance * 2.4f) + (alignment * 0.85f) + continuity + commitmentBonus;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = candidateDir;
                }
            }

            // บันทึกฝั่งที่ตัดสินใจเลี้ยว (ซ้าย/ขวา) ชั่วคราวเพื่อไม่ให้เดินส่ายไปมาหน้าสิ่งกีดขวาง
            float chosenCrossY = Vector3.Cross(desiredDir, bestDir).y;
            if (Mathf.Abs(chosenCrossY) > 0.08f)
            {
                _committedSteerSign = Mathf.Sign(chosenCrossY);
                _committedSteerExpireTime = Time.time + 0.65f;
            }

            return bestDir.normalized;
        }

        private float GetDirectionClearance(Vector3 origin, Vector3 direction, float radius, float maxDistance, bool includeOtherEnemies)
        {
            float minHitDist = maxDistance;

            int sphereHits = Physics.SphereCastNonAlloc(origin, radius, direction, s_rayHitBuffer, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < sphereHits; i++)
            {
                RaycastHit hit = s_rayHitBuffer[i];
                if (hit.distance < minHitDist && IsSolidObstacleCollider(hit.collider, includeOtherEnemies))
                {
                    minHitDist = hit.distance;
                }
            }

            int rayHits = Physics.RaycastNonAlloc(origin, direction, s_rayHitBuffer, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayHits; i++)
            {
                RaycastHit hit = s_rayHitBuffer[i];
                if (hit.distance < minHitDist && IsSolidObstacleCollider(hit.collider, includeOtherEnemies))
                {
                    minHitDist = hit.distance;
                }
            }

            return minHitDist;
        }

        private void RecordObstacleContact(Collision collision)
        {
            if (!UseSmartObstacleAvoidance || _isChaserPhasing || collision == null) return;
            if (!IsSolidObstacleCollider(collision.collider, false)) return;

            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint cp = collision.GetContact(i);
                Vector3 n = cp.normal;
                if (Mathf.Abs(n.y) < 0.45f)
                {
                    n.y = 0f;
                    if (n.sqrMagnitude > 0.001f)
                    {
                        _lastObstacleNormal = n.normalized;
                        _lastObstacleContactTime = Time.time;
                        break;
                    }
                }
            }
        }

        private Vector3 ApplyWallSlideFilter(Vector3 moveDir)
        {
            if (Time.time - _lastObstacleContactTime > 0.12f || _lastObstacleNormal.sqrMagnitude < 0.01f)
            {
                return moveDir;
            }

            float intoWallDot = Vector3.Dot(moveDir, _lastObstacleNormal);
            if (intoWallDot < -0.05f)
            {
                // ตัดแรงที่พุ่งอัดเข้าหากำแพงออก เพื่อให้ไถลขนานไปตามกำแพงโดยไม่ติดแรงเสียดทาน
                Vector3 projected = moveDir - _lastObstacleNormal * intoWallDot + _lastObstacleNormal * 0.08f;
                projected.y = 0f;
                if (projected.sqrMagnitude > 0.001f)
                {
                    return projected.normalized;
                }
            }

            return moveDir;
        }

        private void BuildAStarPath(Vector3 startPos, Vector3 targetPos, float clearanceRadius)
        {
            _navPath.Clear();
            _navPathIndex = 0;

            // Try primary clearance first; if unreachable (e.g. narrow gap), retry with tighter clearance
            bool reached = RunAStarSearch(startPos, targetPos, clearanceRadius, true);
            if (!reached && clearanceRadius > 0.33f)
            {
                RunAStarSearch(startPos, targetPos, 0.30f, false);
            }
        }

        private bool RunAStarSearch(Vector3 startPos, Vector3 targetPos, float clearanceRadius, bool allowRetryIfPartial)
        {
            float gridWidth = AStarCols * AStarCellSize;
            float gridHeight = AStarRows * AStarCellSize;

            float midX = Mathf.Floor(((startPos.x + targetPos.x) * 0.5f) / AStarCellSize) * AStarCellSize;
            float midZ = Mathf.Floor(((startPos.z + targetPos.z) * 0.5f) / AStarCellSize) * AStarCellSize;

            float originX = midX - gridWidth * 0.5f;
            float originZ = midZ - gridHeight * 0.5f;

            int startCol = Mathf.Clamp(Mathf.FloorToInt((startPos.x - originX) / AStarCellSize), 0, AStarCols - 1);
            int startRow = Mathf.Clamp(Mathf.FloorToInt((startPos.z - originZ) / AStarCellSize), 0, AStarRows - 1);
            int targetCol = Mathf.Clamp(Mathf.FloorToInt((targetPos.x - originX) / AStarCellSize), 0, AStarCols - 1);
            int targetRow = Mathf.Clamp(Mathf.FloorToInt((targetPos.z - originZ) / AStarCellSize), 0, AStarRows - 1);

            int startIndex = startRow * AStarCols + startCol;
            int targetIndex = targetRow * AStarCols + targetCol;

            if (startIndex == targetIndex)
            {
                _navPath.Add(new Vector3(targetPos.x, startPos.y, targetPos.z));
                return true;
            }

            System.Array.Clear(s_nodeState, 0, AStarTotalNodes);
            System.Array.Clear(s_walkableCache, 0, AStarTotalNodes);

            // Start & Target cells are always valid endpoints
            s_walkableCache[startIndex] = 1;
            s_walkableCache[targetIndex] = 1;

            int heapCount = 0;
            s_gCost[startIndex] = 0;
            int startH = ComputeOctileHeuristic(startCol, startRow, targetCol, targetRow);
            s_fCost[startIndex] = startH;
            s_parentIndex[startIndex] = -1;
            s_nodeState[startIndex] = 1;
            HeapPush(ref heapCount, startIndex);

            int closestNodeIndex = startIndex;
            int closestNodeH = startH;
            int expandedNodes = 0;
            const int maxExpandedNodes = 1100;

            bool CheckCellWalkable(int col, int row)
            {
                int idx = row * AStarCols + col;
                byte cached = s_walkableCache[idx];
                if (cached != 0) return cached == 1;

                float wx = originX + (col + 0.5f) * AStarCellSize;
                float wz = originZ + (row + 0.5f) * AStarCellSize;

                // Allow cells very close to the player if they have clear line to player
                float distToTargetSq = (wx - targetPos.x) * (wx - targetPos.x) + (wz - targetPos.z) * (wz - targetPos.z);
                if (distToTargetSq <= 0.75f * 0.75f && HasClearCorridor(new Vector3(wx, startPos.y, wz), targetPos, 0.25f, false))
                {
                    s_walkableCache[idx] = 1;
                    return true;
                }

                bool walkable = IsWorldPointWalkable(wx, wz, clearanceRadius);
                if (!walkable)
                {
                    // If immediately adjacent to start cell (e.g. enemy started pressed against a wall), allow stepping out if tighter radius is clear
                    int dc = Mathf.Abs(col - startCol);
                    int dr = Mathf.Abs(row - startRow);
                    if (dc <= 1 && dr <= 1 && IsWorldPointWalkable(wx, wz, Mathf.Min(0.26f, clearanceRadius * 0.6f)))
                    {
                        walkable = true;
                    }
                }

                s_walkableCache[idx] = walkable ? (byte)1 : (byte)2;
                return walkable;
            }

            bool foundExact = false;

            while (heapCount > 0 && expandedNodes < maxExpandedNodes)
            {
                int currentIdx = HeapPop(ref heapCount);
                if (s_nodeState[currentIdx] == 2) continue;
                s_nodeState[currentIdx] = 2;
                expandedNodes++;

                if (currentIdx == targetIndex)
                {
                    foundExact = true;
                    closestNodeIndex = targetIndex;
                    break;
                }

                int curCol = currentIdx % AStarCols;
                int curRow = currentIdx / AStarCols;
                int curG = s_gCost[currentIdx];

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;

                        int nCol = curCol + dx;
                        int nRow = curRow + dz;
                        if (nCol < 0 || nCol >= AStarCols || nRow < 0 || nRow >= AStarRows) continue;

                        int nIdx = nRow * AStarCols + nCol;
                        if (s_nodeState[nIdx] == 2) continue;

                        if (!CheckCellWalkable(nCol, nRow)) continue;

                        // Prevent diagonal corner-cutting through obstacle edges
                        if (dx != 0 && dz != 0)
                        {
                            if (!CheckCellWalkable(curCol + dx, curRow) || !CheckCellWalkable(curCol, curRow + dz))
                            {
                                continue;
                            }
                        }

                        int stepCost = (dx != 0 && dz != 0) ? 14 : 10;
                        int tentativeG = curG + stepCost;

                        if (s_nodeState[nIdx] != 1 || tentativeG < s_gCost[nIdx])
                        {
                            s_gCost[nIdx] = tentativeG;
                            int h = ComputeOctileHeuristic(nCol, nRow, targetCol, targetRow);
                            s_fCost[nIdx] = tentativeG + h;
                            s_parentIndex[nIdx] = currentIdx;

                            if (h < closestNodeH)
                            {
                                closestNodeH = h;
                                closestNodeIndex = nIdx;
                            }

                            s_nodeState[nIdx] = 1;
                            HeapPush(ref heapCount, nIdx);
                        }
                    }
                }
            }

            if (!foundExact && allowRetryIfPartial)
            {
                return false;
            }

            int traceIdx = foundExact ? targetIndex : closestNodeIndex;
            if (traceIdx == startIndex)
            {
                _navPath.Add(new Vector3(targetPos.x, startPos.y, targetPos.z));
                return false;
            }

            while (traceIdx != -1 && traceIdx != startIndex && _navPath.Count < 128)
            {
                if (traceIdx == targetIndex)
                {
                    _navPath.Add(new Vector3(targetPos.x, startPos.y, targetPos.z));
                }
                else
                {
                    int c = traceIdx % AStarCols;
                    int r = traceIdx / AStarCols;
                    float wx = originX + (c + 0.5f) * AStarCellSize;
                    float wz = originZ + (r + 0.5f) * AStarCellSize;
                    _navPath.Add(new Vector3(wx, startPos.y, wz));
                }
                traceIdx = s_parentIndex[traceIdx];
            }

            _navPath.Reverse();
            return foundExact;
        }

        private static int ComputeOctileHeuristic(int c0, int r0, int c1, int r1)
        {
            int dx = Mathf.Abs(c0 - c1);
            int dz = Mathf.Abs(r0 - r1);
            int minD = dx < dz ? dx : dz;
            int maxD = dx > dz ? dx : dz;
            return 14 * minD + 10 * (maxD - minD);
        }

        private static void HeapPush(ref int count, int nodeIndex)
        {
            int i = count++;
            s_openHeap[i] = nodeIndex;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (s_fCost[s_openHeap[i]] < s_fCost[s_openHeap[parent]])
                {
                    int tmp = s_openHeap[i];
                    s_openHeap[i] = s_openHeap[parent];
                    s_openHeap[parent] = tmp;
                    i = parent;
                }
                else
                {
                    break;
                }
            }
        }

        private static int HeapPop(ref int count)
        {
            int root = s_openHeap[0];
            int last = s_openHeap[--count];
            if (count > 0)
            {
                s_openHeap[0] = last;
                int i = 0;
                while (true)
                {
                    int left = (i << 1) + 1;
                    if (left >= count) break;
                    int right = left + 1;
                    int smallest = (right < count && s_fCost[s_openHeap[right]] < s_fCost[s_openHeap[left]]) ? right : left;
                    if (s_fCost[s_openHeap[smallest]] < s_fCost[s_openHeap[i]])
                    {
                        int tmp = s_openHeap[i];
                        s_openHeap[i] = s_openHeap[smallest];
                        s_openHeap[smallest] = tmp;
                        i = smallest;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return root;
        }
    }
}
