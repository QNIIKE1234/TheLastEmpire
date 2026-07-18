using UnityEngine;

namespace TheLastEmpire
{
    public class RobotAI : BaseEnemyAI
    {
        public enum RobotType
        {
            Normal,
            Tanker,
            Blade,
            Gunner,
            Laser,
            Leader
        }

        [Header("Robot Settings")]
        [SerializeField] private RobotType robotType = RobotType.Normal;
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private LayerMask obstacleLayer;

        private float _telegraphTimer = 0f;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        protected override void Start()
        {
            base.Start();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            // Specialize Robot stats
            switch (robotType)
            {
                case RobotType.Tanker:
                    moveSpeed = 2f;
                    detectionRange = 6f;
                    break;
                case RobotType.Blade:
                    moveSpeed = 4.5f;
                    detectionRange = 8f;
                    break;
                case RobotType.Gunner:
                case RobotType.Laser:
                    moveSpeed = 3f;
                    detectionRange = 10f;
                    break;
            }
        }

        protected override void UpdateAIBehavior()
        {
            // Robot Vision Check: Detects player using Raycast in facing direction (transform.right)
            // and ONLY if player is currently moving!
            bool playerDetected = CheckRobotVision();

            switch (currentState)
            {
                case AIState.Idle:
                case AIState.Wander:
                    if (playerDetected)
                    {
                        // Transition to Telegraph state (stagger/charging)
                        currentState = AIState.Telegraph;
                        _telegraphTimer = telegraphDuration;
                        rb.linearVelocity = Vector2.zero;
                        Debug.Log($"[RobotAI] {gameObject.name} spotted player! Charging attack...");

                        // Flash Red color for visual telegraph warning
                        if (_spriteRenderer != null)
                        {
                            _spriteRenderer.color = Color.red;
                        }
                    }
                    else
                    {
                        WanderMovement();
                    }
                    break;

                case AIState.Telegraph:
                    rb.linearVelocity = Vector2.zero;
                    _telegraphTimer -= Time.fixedDeltaTime;

                    if (_telegraphTimer <= 0f)
                    {
                        // Restore color and transition to Chase
                        if (_spriteRenderer != null)
                        {
                            _spriteRenderer.color = _originalColor;
                        }
                        currentState = AIState.Chase;
                    }
                    break;

                case AIState.Chase:
                    // Once in chase mode, it keeps chasing the player
                    ChasePlayer();
                    break;
            }
        }

        private bool CheckRobotVision()
        {
            if (playerTransform == null) return false;

            // 1. Distance check
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist > detectionRange) return false;

            // 2. Check if player is moving
            Rigidbody2D playerRb = playerTransform.GetComponent<Rigidbody2D>();
            if (playerRb == null || playerRb.linearVelocity.sqrMagnitude < 0.05f)
            {
                // Player is standing still - Robot doesn't see them!
                return false;
            }

            // 3. Raycast in facing direction to see if player is in sight
            // (Uses transform.right since rotation points positive X axis to targets)
            Vector2 facingDir = transform.right;
            Vector2 toPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;

            // Check if player is generally in front of the robot (FOV check ~60 degrees)
            float dotProduct = Vector2.Dot(facingDir, toPlayer);
            if (dotProduct < 0.5f) return false; // Not in front

            // Check for line of sight blockages (walls/obstacles)
            RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer, detectionRange, ~obstacleLayer);
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                return true;
            }

            return false;
        }
    }
}
