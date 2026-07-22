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
        protected Health health;
        protected AIState currentState = AIState.Idle;

        protected float stateTimer = 0f;
        protected Vector3 wanderDirection;
        protected bool hasBeenAttacked = false;

        public string PoolKey => poolKey;
        public AIState CurrentState => currentState;

        protected float transitionDelayTimer = 2.0f;

        [Header("UI Reference")]
        [SerializeField] protected string enemyName = "Enemy";
        [SerializeField] protected TMP_Text nameText;
        [SerializeField] protected TMP_Text healthText;
        [SerializeField] protected UnityEngine.UI.Slider healthSlider;

        protected virtual void OnEnable()
        {
            transitionDelayTimer = 2.0f; // Lock actions for 2s on spawn/activation
            hasBeenAttacked = false;
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
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        protected void WanderMovement()
        {
            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f)
            {
                ChooseNextWanderState();
            }

            if (currentState == AIState.Wander)
            {
                rb.linearVelocity = wanderDirection * (moveSpeed * 0.5f);
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
            if (hasBeenAttacked) return true; // Aggro locked on player after taking damage
            return Vector3.Distance(transform.position, playerTransform.position) <= detectionRange;
        }

        protected virtual void HandleDeath()
        {
            Debug.Log($"[BaseEnemyAI] Defeated {gameObject.name}");

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
    }
}
