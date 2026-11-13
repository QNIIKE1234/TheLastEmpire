using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class BaseEnemyAI : MonoBehaviour
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

        protected Rigidbody rb;
        protected Transform playerTransform;
        protected Health playerHealth;
        protected Health health;
        protected AIState currentState = AIState.Idle;

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
            _phasingGroundY = transform.position.y;
            _isChaserPhasing = true;

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
            transitionDelayTimer = 2.0f; // Lock actions for 2s on spawn/activation
            hasBeenAttacked = false;
        }

        protected virtual void OnDisable()
        {
            DisableChaserPhasing();
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
            ChooseNextWanderState();
        }

        protected virtual void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

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
                return;
            }

            UpdateAIBehavior();
        }

        protected abstract void UpdateAIBehavior();

        protected virtual void FindPlayer()
        {
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
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
                rb.linearVelocity = wanderDirection * (moveSpeed * speedMultiplier * 0.5f);
                // Rotate visual to wander direction
                if (wanderDirection.sqrMagnitude > 0.01f)
                {
                    transform.forward = wanderDirection;
                }
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        private void ChooseNextWanderState()
        {
            if (Random.value > 0.4f)
            {
                currentState = AIState.Wander;
                stateTimer = Random.Range(wanderTime * 0.5f, wanderTime * 1.5f);
                wanderDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
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

            Vector3 direction = (playerTransform.position - transform.position);
            direction.y = 0f;
            direction.Normalize();
            rb.linearVelocity = new Vector3(direction.x * (moveSpeed * speedMultiplier), rb.linearVelocity.y, direction.z * (moveSpeed * speedMultiplier));

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.forward = direction;
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
        }
    }
}
