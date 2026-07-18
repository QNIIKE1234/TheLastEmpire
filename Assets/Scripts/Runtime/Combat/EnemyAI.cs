using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float detectionRange = 15f;

        private Rigidbody2D _rb;
        private Transform _playerTransform;
        private Health _health;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            _health = GetComponent<Health>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<Health>();
            }

            // Register to destroy this enemy when health reaches 0
            _health.onDeath.AddListener(HandleDeath);

            FindPlayer();
        }

        private void FixedUpdate()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            if (distanceToPlayer <= detectionRange)
            {
                // Simple AI: Move directly towards player
                Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
                _rb.linearVelocity = direction * moveSpeed;

                // Rotate to face player (visual rotation)
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            else
            {
                // Stop moving if player is out of detection range
                _rb.linearVelocity = Vector2.zero;
            }
        }

        private void FindPlayer()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void HandleDeath()
        {
            Debug.Log($"[EnemyAI] Enemy {gameObject.name} has been defeated!");
            
            // Re-route clean-up (could spawn loot, trigger particles, etc.)
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.onDeath.RemoveListener(HandleDeath);
            }
        }
    }
}
