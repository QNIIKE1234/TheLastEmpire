using System.Collections;
using UnityEngine;

namespace TheLastEmpire
{
    public class RobotAI : BaseEnemyAI, IDamageable
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

        [Header("Robot Gunner / Shooting Settings")]
        [SerializeField] private Projectile enemyBulletPrefab;
        [SerializeField] private float fireRate = 1.2f;
        [SerializeField] private float shootingDistance = 5f;

        [Header("Robot Blade / Dash Settings")]
        [SerializeField] private float bladeDashSpeed = 15f;
        [SerializeField] private float bladeDashDuration = 0.25f;
        [SerializeField] private float bladeDashCooldown = 3f;

        [Header("Robot Laser Settings")]
        [SerializeField] private float laserChargeDuration = 1.5f;
        [SerializeField] private float laserFireDuration = 1f;
        [SerializeField] private float laserRange = 10f;
        [SerializeField] private int laserDamage = 5; // Damage per tick

        private float _telegraphTimer = 0f;
        private float _actionCooldownTimer = 0f;
        private bool _isActionActive = false;

        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private LineRenderer _laserLine;

        public bool IsDead => health != null && health.IsDead;

        protected override void Start()
        {
            base.Start();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            // Programmatically add LineRenderer for laser types
            if (robotType == RobotType.Laser)
            {
                _laserLine = gameObject.AddComponent<LineRenderer>();
                _laserLine.startWidth = 0.05f;
                _laserLine.endWidth = 0.05f;
                _laserLine.material = new Material(Shader.Find("Sprites/Default"));
                _laserLine.startColor = Color.red;
                _laserLine.endColor = Color.red;
                _laserLine.positionCount = 2;
                _laserLine.enabled = false;
            }

            // Define Robot stats
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

        protected override void FixedUpdate()
        {
            if (health != null && health.IsDead) return;

            if (_actionCooldownTimer > 0f) _actionCooldownTimer -= Time.fixedDeltaTime;

            if (_isActionActive)
            {
                return; // Let active action coroutines handle movement/physics
            }

            base.FixedUpdate();
        }

        protected override void UpdateAIBehavior()
        {
            if (_isActionActive) return;

            bool playerDetected = CheckRobotVision();

            switch (currentState)
            {
                case AIState.Idle:
                case AIState.Wander:
                    if (playerDetected)
                    {
                        currentState = AIState.Telegraph;
                        _telegraphTimer = telegraphDuration;
                        rb.linearVelocity = Vector3.zero;

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
                    rb.linearVelocity = Vector3.zero;
                    _telegraphTimer -= Time.fixedDeltaTime;

                    if (_telegraphTimer <= 0f)
                    {
                        if (_spriteRenderer != null)
                        {
                            _spriteRenderer.color = _originalColor;
                        }
                        currentState = AIState.Chase;
                    }
                    break;

                case AIState.Chase:
                    if (playerTransform == null)
                    {
                        currentState = AIState.Idle;
                        return;
                    }

                    float dist = Vector3.Distance(transform.position, playerTransform.position);

                    // Gunner Robot Shooting Action
                    if (robotType == RobotType.Gunner && dist <= shootingDistance && _actionCooldownTimer <= 0f)
                    {
                        StartCoroutine(ExecuteGunnerShot());
                        return;
                    }

                    // Blade Robot Dash Slash Action
                    if (robotType == RobotType.Blade && dist <= 3f && _actionCooldownTimer <= 0f)
                    {
                        StartCoroutine(ExecuteBladeDash());
                        return;
                    }

                    // Laser Robot Laser Firing Action
                    if (robotType == RobotType.Laser && dist <= laserRange && _actionCooldownTimer <= 0f)
                    {
                        StartCoroutine(ExecuteLaserFire());
                        return;
                    }

                    // Normal pursuit movement
                    ChasePlayer();
                    break;
            }
        }

        private bool CheckRobotVision()
        {
            if (playerTransform == null) return false;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist > detectionRange) return false;

            // Detects player ONLY when player is currently moving
            Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
            if (playerRb == null || playerRb.linearVelocity.sqrMagnitude < 0.05f)
            {
                return false;
            }

            // Raycast line of sight check
            Vector3 facingDir = transform.forward;
            Vector3 toPlayer = (playerTransform.position - transform.position);
            toPlayer.y = 0f;
            toPlayer.Normalize();

            float dot = Vector3.Dot(facingDir, toPlayer);
            if (dot < 0.5f) return false; // FOV limit

            RaycastHit hit;
            if (Physics.Raycast(transform.position, toPlayer, out hit, detectionRange, ~obstacleLayer))
            {
                if (hit.collider != null && hit.collider.CompareTag("Player"))
                {
                    return true;
                }
            }

            return false;
        }

        // --- ROBOT ACTIONS ---

        private IEnumerator ExecuteGunnerShot()
        {
            _isActionActive = true;
            _actionCooldownTimer = fireRate;
            rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;

            // Lock rotation towards player
            Vector3 dir = (playerTransform.position - transform.position);
            dir.y = 0f;
            dir.Normalize();
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.forward = dir;
            }

            yield return new WaitForSeconds(0.4f); // brief aim delay

            if (playerTransform != null)
            {
                Vector3 spawnPos = transform.position + dir * 0.6f;
                GameObject bullet;

                if (enemyBulletPrefab != null)
                {
                    bullet = Instantiate(enemyBulletPrefab.gameObject, spawnPos, Quaternion.identity);
                }
                else
                {
                    // Fallback procedural projectile
                    bullet = new GameObject("RobotBullet");
                    bullet.transform.position = spawnPos;
                    bullet.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

                    SpriteRenderer sr = bullet.AddComponent<SpriteRenderer>();
                    sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                    sr.color = Color.red;

                    SphereCollider col = bullet.AddComponent<SphereCollider>();
                    col.isTrigger = true;

                    bullet.AddComponent<Projectile>();
                }

                Projectile proj = bullet.GetComponent<Projectile>();
                proj.Setup(dir, gameObject);
            }

            _isActionActive = false;
            currentState = AIState.Chase;
        }

        private IEnumerator ExecuteBladeDash()
        {
            _isActionActive = true;
            _actionCooldownTimer = bladeDashCooldown;
            rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;

            // Flash red warning
            if (_spriteRenderer != null) _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.3f);
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

            // Dash forward
            if (playerTransform != null)
            {
                Vector3 dashDir = (playerTransform.position - transform.position);
                dashDir.y = 0f;
                dashDir.Normalize();
                float dashTimer = bladeDashDuration;

                while (dashTimer > 0f && playerTransform != null)
                {
                    dashTimer -= Time.deltaTime;
                    rb.linearVelocity = dashDir * bladeDashSpeed;
                    yield return null;
                }
            }

            rb.linearVelocity = Vector3.zero;
            _isActionActive = false;
            currentState = AIState.Chase;
        }

        private IEnumerator ExecuteLaserFire()
        {
            _isActionActive = true;
            _actionCooldownTimer = 3f;
            rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;

            // Enable Warning Laser Guide
            if (_laserLine != null)
            {
                _laserLine.enabled = true;
                _laserLine.startWidth = 0.03f;
                _laserLine.endWidth = 0.03f;
                _laserLine.startColor = new Color(1f, 0f, 0f, 0.3f); // faint red
            }

            float chargeTimer = laserChargeDuration;
            while (chargeTimer > 0f && playerTransform != null)
            {
                chargeTimer -= Time.deltaTime;
                Vector3 dir = (playerTransform.position - transform.position);
                dir.y = 0f;
                dir.Normalize();
                
                // Align rotation to player
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.forward = dir;
                }

                if (_laserLine != null)
                {
                    _laserLine.SetPosition(0, transform.position);
                    _laserLine.SetPosition(1, transform.position + dir * laserRange);
                }
                yield return null;
            }

            // Fire Laser Beam
            if (_laserLine != null)
            {
                _laserLine.startWidth = 0.25f; // thick beam
                _laserLine.endWidth = 0.25f;
                _laserLine.startColor = Color.red;
                _laserLine.endColor = Color.red;
            }

            float fireTimer = laserFireDuration;
            while (fireTimer > 0f && playerTransform != null)
            {
                fireTimer -= Time.deltaTime;
                Vector3 dir = transform.forward;

                if (_laserLine != null)
                {
                    _laserLine.SetPosition(0, transform.position);
                    _laserLine.SetPosition(1, transform.position + dir * laserRange);
                }

                // Laser Raycast Damage Tick check
                RaycastHit hit;
                if (Physics.Raycast(transform.position, dir, out hit, laserRange, ~obstacleLayer))
                {
                    if (hit.collider != null && hit.collider.CompareTag("Player"))
                    {
                        IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                        if (damageable != null)
                        {
                            damageable.TakeDamage(laserDamage);
                        }
                    }
                }
                yield return null;
            }

            if (_laserLine != null) _laserLine.enabled = false;
            _isActionActive = false;
            currentState = AIState.Chase;
        }

        // --- IDamageable Implementation for Frontal Shield ---

        public void TakeDamage(float damageAmount)
        {
            if (IsDead) return;

            // Tanker Frontal Shield logic
            if (robotType == RobotType.Tanker && playerTransform != null)
            {
                Vector3 facingDir = transform.forward;
                Vector3 toPlayer = (playerTransform.position - transform.position);
                toPlayer.y = 0f;
                toPlayer.Normalize();

                // Dot product > 0 means the player (instigator) is standing in front of the shield!
                float dot = Vector3.Dot(facingDir, toPlayer);
                if (dot > 0f)
                {
                    Debug.Log($"[RobotAI] Frontal Shield blocked {damageAmount} damage!");
                    
                    // Flash gray color to indicate blocked damage feedback
                    StartCoroutine(FlashColorFeedback(Color.gray, 0.15f));
                    return; // Damage blocked!
                }
            }

            // Normal damage forwarding to health
            if (health != null)
            {
                health.TakeDamage(damageAmount);
            }
        }

        private IEnumerator FlashColorFeedback(Color color, float duration)
        {
            if (_spriteRenderer == null) yield break;
            _spriteRenderer.color = color;
            yield return new WaitForSeconds(duration);
            _spriteRenderer.color = _originalColor;
        }
    }
}
