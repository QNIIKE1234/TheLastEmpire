using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody2D))]
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

        protected Rigidbody2D rb;
        protected Transform playerTransform;
        protected Health health;
        protected AIState currentState = AIState.Idle;

        protected float stateTimer = 0f;
        protected Vector2 wanderDirection;

        public string PoolKey => poolKey;
        public AIState CurrentState => currentState;

        protected virtual void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            health = GetComponent<Health>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
            }
            health.onDeath.AddListener(HandleDeath);

            FindPlayer();
            ChooseNextWanderState();
        }

        protected virtual void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

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
                    float angle = Mathf.Atan2(wanderDirection.y, wanderDirection.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        private void ChooseNextWanderState()
        {
            if (Random.value > 0.4f)
            {
                currentState = AIState.Wander;
                stateTimer = Random.Range(wanderTime * 0.5f, wanderTime * 1.5f);
                wanderDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            }
            else
            {
                currentState = AIState.Idle;
                stateTimer = Random.Range(idleTime * 0.5f, idleTime * 1.5f);
                wanderDirection = Vector2.zero;
            }
        }

        protected void ChasePlayer(float speedMultiplier = 1f)
        {
            if (playerTransform == null)
            {
                FindPlayer();
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * (moveSpeed * speedMultiplier);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        protected bool IsPlayerInDetectionRange()
        {
            if (playerTransform == null) return false;
            return Vector2.Distance(transform.position, playerTransform.position) <= detectionRange;
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
            }
        }
    }
}
