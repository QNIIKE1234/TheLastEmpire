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
            Leader,
            Drone,
            Kamikaze
        }

        [Header("Robot Settings")]
        [SerializeField] private RobotType robotType = RobotType.Normal;
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Robot Gunner / Shooting Settings")]
        [SerializeField] private Projectile enemyBulletPrefab;
        [SerializeField] private float fireRate = 1.2f;
        [SerializeField] private float shootingDistance = 5f;
        [SerializeField] private int gunnerBurstCount = 5;
        [SerializeField] private float gunnerBurstShotInterval = 0.12f;
        [SerializeField] private float gunnerPauseMin = 1.0f;
        [SerializeField] private float gunnerPauseMax = 3.0f;
        [SerializeField] private float enemyProjectileSpeed = 50f;

        [Header("Robot Blade / Dash Settings")]
        [SerializeField] private float bladeDashSpeed = 15f;
        [SerializeField] private float bladeDashDuration = 0.25f;
        [SerializeField] private float bladeDashCooldown = 3f;

        [Header("Robot Kamikaze Settings")]
        [SerializeField] private float kamikazeExplosionRadius = 3f;
        [SerializeField] private int kamikazeDamage = 40;

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

        public override bool UseSmartObstacleAvoidance => true;

        protected override void Start()
        {
            useSmartObstacleAvoidance = true;
            base.Start();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (robotType == RobotType.Normal)
            {
                string lowerName = gameObject.name.ToLower();
                if (lowerName.Contains("tanker") || lowerName.Contains("tank")) robotType = RobotType.Tanker;
                else if (lowerName.Contains("blade")) robotType = RobotType.Blade;
                else if (lowerName.Contains("gunner") || lowerName.Contains("gun")) robotType = RobotType.Gunner;
                else if (lowerName.Contains("laser")) robotType = RobotType.Laser;
                else if (lowerName.Contains("leader") || lowerName.Contains("boss")) robotType = RobotType.Leader;
                else if (lowerName.Contains("drone")) robotType = RobotType.Drone;
                else if (lowerName.Contains("kamikaze") || lowerName.Contains("bomb") || lowerName.Contains("boomer")) robotType = RobotType.Kamikaze;
            }
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
                case RobotType.Drone:
                    moveSpeed = 5.2f;
                    detectionRange = 9f;
                    break;
                case RobotType.Kamikaze:
                    moveSpeed = 4.2f;
                    detectionRange = 8f;
                    break;
                case RobotType.Leader:
                    moveSpeed = 2.5f;
                    detectionRange = 8f;
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

            if (IsTargetDead())
            {
                currentState = AIState.Wander;
                WanderMovement();
                return;
            }

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
                    bool hasLineOfSight = HasLineOfSightToPlayer();

                    // Kamikaze Suicide Explosion (ต้องเห็นตัวผู้เล่นโดยไม่มีกำแพงกั้น หรือประชิดตัวมาก)
                    if (robotType == RobotType.Kamikaze && dist <= 2.2f && _actionCooldownTimer <= 0f && (hasLineOfSight || dist <= 1.4f))
                    {
                        StartCoroutine(ExecuteKamikazeExplode());
                        return;
                    }

                    // Gunner Robot Shooting Action (ยิงเฉพาะเมื่อไม่มีกำแพงหรือสิ่งกีดขวางบัง ถ้ามีให้เดินอ้อมก่อน)
                    if (robotType == RobotType.Gunner && dist <= shootingDistance && _actionCooldownTimer <= 0f && hasLineOfSight)
                    {
                        StartCoroutine(ExecuteGunnerShot());
                        return;
                    }

                    // Blade Robot Dash Slash Action (พุ่งฟันเฉพาะเมื่อไม่มีสิ่งกีดขวางกั้นกลาง)
                    if (robotType == RobotType.Blade && dist <= 3f && _actionCooldownTimer <= 0f && hasLineOfSight)
                    {
                        StartCoroutine(ExecuteBladeDash());
                        return;
                    }

                    // Laser Robot Laser Firing Action (ยิงเลเซอร์เฉพาะเมื่อมองเห็นผู้เล่นโดยไม่มีตึก/กำแพงบัง)
                    if (robotType == RobotType.Laser && dist <= laserRange && _actionCooldownTimer <= 0f && hasLineOfSight)
                    {
                        StartCoroutine(ExecuteLaserFire());
                        return;
                    }

                    // Normal pursuit movement (uses Smart Obstacle Avoidance & A* Pathfinding)
                    ChasePlayer();
                    break;
            }
        }

        private bool CheckRobotVision()
        {
            // หุ่นยนต์จะไม่โจมตีผู้เล่นก่อน จนกว่าจะถูกผู้เล่นโจมตีก่อน (Neutral / Self-defense only)
            if (!hasBeenAttacked) return false;

            if (playerTransform == null || IsTargetDead()) return false;

            // เมื่อถูกโจมตีแล้ว หุ่นยนต์จะล็อกเป้าหมายไล่ล่าผู้เล่นทันที (แม้ผู้เล่นจะหลบหลังกำแพง)
            return true;
        }

        // --- ROBOT ACTIONS ---

        private IEnumerator ExecuteGunnerShot()
        {
            _isActionActive = true;
            rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;

            yield return new WaitForSeconds(0.25f); // brief aim lock before 5-round burst

            int shotsToFire = Mathf.Max(1, gunnerBurstCount);
            float shotInterval = Mathf.Max(0.05f, gunnerBurstShotInterval);

            for (int shot = 0; shot < shotsToFire; shot++)
            {
                if (health != null && health.IsDead) break;
                if (playerTransform == null || IsTargetDead()) break;

                Vector3 dir = (playerTransform.position - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
                dir.Normalize();
                transform.forward = dir;

                PlayActionAnimation("Attack", shotInterval + 0.25f, 0.04f);

                Vector3 spawnPos = transform.position + dir * 0.6f + Vector3.up * 0.25f;
                GameObject bullet;

                if (enemyBulletPrefab != null)
                {
                    bullet = Instantiate(enemyBulletPrefab.gameObject, spawnPos, Quaternion.identity);
                }
                else
                {
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
                if (proj != null)
                {
                    proj.SetSpeed(enemyProjectileSpeed);
                    Vector3 targetPoint = playerTransform != null ? playerTransform.position : transform.position + dir * 10f;
                    proj.Setup(dir, targetPoint, gameObject);
                }

                if (shot < shotsToFire - 1)
                {
                    yield return new WaitForSeconds(shotInterval);
                }
            }

            // Random 1 - 3 second pause before the next 5-round burst!
            float minPause = Mathf.Max(0.2f, gunnerPauseMin);
            float maxPause = Mathf.Max(minPause, gunnerPauseMax);
            _actionCooldownTimer = Random.Range(minPause, maxPause);

            _isActionActive = false;
            currentState = AIState.Chase;
        }

        private IEnumerator ExecuteBladeDash()
        {
            _isActionActive = true;
            _actionCooldownTimer = bladeDashCooldown;
            rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;

            // Flash red warning + directional chevron hitbox telegraph (UI_Hitbox_Sprites_7)
            SpriteRenderer dashChevron = null;
            Vector3 previewDir = playerTransform != null ? (playerTransform.position - transform.position) : transform.forward;
            previewDir.y = 0f;
            if (previewDir.sqrMagnitude < 0.01f) previewDir = transform.forward;
            previewDir.Normalize();

            Sprite chevSp = HitboxSpriteLibrary.DirectionalChevrons;
            float dashDist = bladeDashSpeed * bladeDashDuration;
            if (chevSp != null)
            {
                dashChevron = HitboxSpriteLibrary.CreateGroundSpriteIndicator("BladeDashChevron", transform, chevSp, HitboxSpriteLibrary.DefaultOriginalColor, 6);
                dashChevron.transform.position = new Vector3(transform.position.x + previewDir.x * (dashDist * 0.5f), 0.06f, transform.position.z + previewDir.z * (dashDist * 0.5f));
                dashChevron.transform.rotation = HitboxSpriteLibrary.GetGroundChevronRotation(previewDir);
                float bw = chevSp.bounds.size.x;
                float bh = chevSp.bounds.size.y;
                if (bw > 0.001f && bh > 0.001f)
                    dashChevron.transform.localScale = new Vector3(dashDist / bw, 1.25f / bh, 1f);
            }

            TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                this,
                TheLastEmpire.Network.NetworkEnemyBridge.TelegraphRobotBladeDash,
                transform.position,
                previewDir,
                dashDist,
                0.32f);

            if (_spriteRenderer != null) _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.3f);
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
            if (dashChevron != null) Destroy(dashChevron.gameObject);

            // Dash forward
            PlayActionAnimation("Attack", bladeDashDuration + 0.25f, 0.05f);
            if (playerTransform != null)
            {
                Vector3 dashDir = (playerTransform.position - transform.position);
                dashDir.y = 0f;
                dashDir.Normalize();
                float dashTimer = bladeDashDuration;

                while (dashTimer > 0f && playerTransform != null)
                {
                    if (IsObstacleDirectlyAhead(dashDir, GetAgentRadius() + 0.35f))
                    {
                        break;
                    }
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
            PlayActionAnimation("Charge", laserChargeDuration, 0.08f);
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
            PlayActionAnimation("Slash1", laserFireDuration, 0.06f);
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
                    if (hit.collider != null && hit.collider.CompareTag("Player") && HasLineOfSightToPlayer(laserRange))
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

        public void TakeDamage(float damageAmount, UnityEngine.Vector3? hitPoint = null, bool isCrit = false)
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
                    
                    // Spawn "BLOCKED" floating feedback
                    Vector3 blockedPos = hitPoint ?? (transform.position + Vector3.up * 1.3f);
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.SpawnDamageNumber(blockedPos, 0f, false, "BLOCKED");
                    }

                    // Flash gray color to indicate blocked damage feedback
                    StartCoroutine(FlashColorFeedback(Color.gray, 0.15f));
                    return; // Damage blocked!
                }
            }

            // Normal damage forwarding to health
            if (health != null)
            {
                health.TakeDamage(damageAmount, hitPoint, isCrit);
            }
        }

        private IEnumerator FlashColorFeedback(Color color, float duration)
        {
            if (_spriteRenderer == null) yield break;
            _spriteRenderer.color = color;
            yield return new WaitForSeconds(duration);
            _spriteRenderer.color = _originalColor;
        }
    
        private IEnumerator ExecuteKamikazeExplode()
        {
            _isActionActive = true;
            _actionCooldownTimer = 99f;
            rb.linearVelocity = Vector3.zero;
            currentState = AIState.Telegraph;

            // Flash rapid warning + Hazard Circle hitbox (UI_Hitbox_Sprites_0)
            PlayActionAnimation("Explode", 0.75f, 0.05f);
            SpriteRenderer blastHint = null;
            Sprite hazardSp = HitboxSpriteLibrary.HazardCircle;
            if (hazardSp != null)
            {
                blastHint = HitboxSpriteLibrary.CreateGroundSpriteIndicator("KamikazeBlastHitbox", transform, hazardSp, HitboxSpriteLibrary.DefaultOriginalColor, 6);
                blastHint.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                HitboxSpriteLibrary.SetWorldDiameter(blastHint, kamikazeExplosionRadius * 2f);
            }

            TheLastEmpire.Network.NetworkEnemyBridge.Instance?.BroadcastEnemyTelegraph(
                this,
                TheLastEmpire.Network.NetworkEnemyBridge.TelegraphRobotKamikaze,
                transform.position,
                transform.forward,
                kamikazeExplosionRadius,
                0.7f);

            float timer = 0.7f;
            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.color = (Mathf.Repeat(timer * 15f, 1f) > 0.5f) ? Color.red : Color.yellow;
                }
                if (blastHint != null)
                {
                    blastHint.transform.position = new Vector3(transform.position.x, 0.06f, transform.position.z);
                    blastHint.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    blastHint.color = HitboxSpriteLibrary.DefaultOriginalColor;
                }
                yield return null;
            }

            if (blastHint != null) Destroy(blastHint.gameObject);

            // Explode
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.SpawnFromPool("EnemyExplosion", transform.position, Quaternion.identity);
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, kamikazeExplosionRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    IDamageable dmg = hit.GetComponent<IDamageable>();
                    if (dmg != null)
                    {
                        dmg.TakeDamage(kamikazeDamage, transform.position);
                    }
                }
            }

            if (health != null)
            {
                health.TakeDamage(99999f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
