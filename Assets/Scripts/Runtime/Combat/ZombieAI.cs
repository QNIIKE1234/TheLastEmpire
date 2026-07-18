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

        [Header("Leader Settings")]
        [SerializeField] private float buffRadius = 6f;

        private float _leapCooldownTimer = 0f;
        private float _leapActiveTimer = 0f;
        private bool _isLeaping = false;
        private Vector2 _leapDirection;

        private bool _isExploding = false;
        private float _fuseTimer = 0f;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        private float _leaderBuffCheckTimer = 0f;
        private bool _hasLeaderBuff = false;

        protected override void Start()
        {
            base.Start();

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
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
                rb.linearVelocity = Vector2.zero;
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

            // Apply leader speed buff if Leader Zombie is nearby
            ApplyLeaderBuffChecks();

            base.FixedUpdate();
        }

        protected override void UpdateAIBehavior()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool detected = IsPlayerInDetectionRange();

            float finalMoveSpeed = moveSpeed * (_hasLeaderBuff ? 1.3f : 1f);

            if (detected)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

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

                currentState = AIState.Chase;
                ChasePlayer(_hasLeaderBuff ? 1.3f : 1f);
            }
            else
            {
                if (isNight)
                {
                    // Zombie will not wander during the night (stands still / idle)
                    currentState = AIState.Idle;
                    rb.linearVelocity = Vector2.zero;
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
                _leapDirection = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            }
            else
            {
                _leapDirection = transform.right;
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

            // Perform circular overlap check for damage targets
            Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
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

            // Kill ourselves cleanly (we skip returning to pool or trigger it as pool return)
            _isExploding = false;
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            if (health != null)
            {
                health.TakeDamage(health.MaxHealth * 2); // deal fatal damage to trigger clean death
            }
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
                    float dist = Vector2.Distance(transform.position, zombie.transform.position);
                    if (dist <= buffRadius)
                    {
                        _hasLeaderBuff = true;
                        break;
                    }
                }
            }
        }

        protected override void HandleDeath()
        {
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
            }
        }
    }
}
