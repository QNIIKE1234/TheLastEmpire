using UnityEngine;

namespace TheLastEmpire
{
    public enum TrajectoryMode
    {
        Straight,
        Arced
    }

    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private float damage = 20;
        [SerializeField] private float decreseMulti = 0.1f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private string poolKey = "";

        [Header("Explosive Settings")]
        [SerializeField] private bool isExplosive = false;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private string explosionVfxPoolKey = "EnemyExplosion";
        [SerializeField] private float explosionVfxScaleMultiplier = 1.0f;

        [Header("Trajectory Settings")]
        [SerializeField] private TrajectoryMode trajectoryMode = TrajectoryMode.Straight;
        [SerializeField] private float arcHeight = 2.5f;

        [Header("Bullet Trail VFX")]
        [SerializeField] private bool useTrailRenderer = true;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.35f, 0.9f);
        [SerializeField] private float trailTime = 0.08f;
        [SerializeField] private float trailStartWidth = 0.15f;

        [Header("Impact Spark VFX")]
        [SerializeField] private string obstacleHitVfxPoolKey = "PistolFlash";

        private Rigidbody _rb;
        private GameObject _owner;
        private float _lifeTimer;
        private TrailRenderer _trail;

        // Arced trajectory variables
        private Vector3 _startPoint;
        private Vector3 _targetPoint;
        private float _flightDuration;
        private float _currentFlightTime;

        // Dynamic overrides set by Weapon stats
        private float _activeDamage;
        private float _activeLifetime;
        private bool _canPierce = false;
        private string _weaponName = "";
        private float _maxFalloffDistance = DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;
        private System.Collections.Generic.List<IDamageable> _hitTargets = new System.Collections.Generic.List<IDamageable>();

        public string PoolKey
        {
            get => poolKey;
            set => poolKey = value;
        }

        public float Speed => speed;
        public bool IsExplosive => isExplosive;
        public float ExplosionRadius => explosionRadius;
        public TrajectoryMode Trajectory => trajectoryMode;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            SetupTrailRenderer();

            // Set initial defaults
            _activeDamage = damage;
            _activeLifetime = lifetime;
        }

        private void SetupTrailRenderer()
        {
            if (!useTrailRenderer) return;

            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();
                _trail.time = trailTime;
                _trail.startWidth = trailStartWidth;
                _trail.endWidth = 0.0f;
                _trail.autodestruct = false;

                if (trailMaterial != null)
                {
                    _trail.material = trailMaterial;
                }
                else
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
                    if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                    if (shader != null)
                    {
                        _trail.material = new Material(shader);
                    }
                }

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(Color.white, 0.0f),
                        new GradientColorKey(trailColor, 0.35f),
                        new GradientColorKey(new Color(1f, 0.45f, 0.05f), 1.0f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.9f, 0.0f),
                        new GradientAlphaKey(0.7f, 0.5f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                _trail.colorGradient = gradient;
            }
        }

        private void OnEnable()
        {
            if (_trail != null)
            {
                _trail.Clear();
            }
            _lifeTimer = _activeLifetime;
            _currentFlightTime = 0f;
            _owner = null;
            if (_hitTargets == null)
            {
                _hitTargets = new System.Collections.Generic.List<IDamageable>();
            }
            _hitTargets.Clear();
        }

        private void Update()
        {
            if (trajectoryMode == TrajectoryMode.Arced)
            {
                _currentFlightTime += Time.deltaTime;
                float t = _currentFlightTime / _flightDuration;

                if (t >= 1f)
                {
                    // Reached the destination
                    transform.position = _targetPoint;
                    if (isExplosive) Explode();
                    DeactivateProjectile();
                    return;
                }

                // Calculate parabolic arc manually
                Vector3 currentPos = Vector3.Lerp(_startPoint, _targetPoint, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

                // Face the direction of movement
                Vector3 moveDir = (currentPos - transform.position);
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    transform.forward = moveDir.normalized;
                }

                transform.position = currentPos;
            }
            if (trajectoryMode != TrajectoryMode.Arced)
            {
                _lifeTimer -= Time.deltaTime;
                if (_lifeTimer <= 0f)
                {
                    if (isExplosive) Explode();
                    DeactivateProjectile();
                }
            }
        }

        public void Setup(Vector3 direction, Vector3 targetPoint, GameObject owner)
        {
            _owner = owner;
            _targetPoint = targetPoint;
            _startPoint = transform.position;

            if (trajectoryMode == TrajectoryMode.Arced)
            {
                _rb.linearVelocity = Vector3.zero; // Manual movement via Update
                float distance = Vector3.Distance(new Vector3(_startPoint.x, 0, _startPoint.z), new Vector3(_targetPoint.x, 0, _targetPoint.z));
                _flightDuration = distance / speed;
                
                // Ensure it doesn't instantly snap if distance is extremely close
                if (_flightDuration < 0.1f) _flightDuration = 0.1f;
            }
            else
            {
                _rb.linearVelocity = direction.normalized * speed;
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.forward = direction.normalized;
                }
            }
        }

        public void SetStats(float damageVal, float lifetimeVal, bool pierceVal, string weaponName = "", float maxFalloffRange = 0f)
        {
            _activeDamage = damageVal;
            _activeLifetime = lifetimeVal;
            _canPierce = pierceVal;
            _lifeTimer = lifetimeVal; // Update active timer
            _weaponName = weaponName;
            _maxFalloffDistance = (maxFalloffRange > 0f) ? maxFalloffRange : DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;
        }

        private void OnTriggerEnter(Collider collision)
        {
            if (collision == null) return;

            // Ignore owner, any child transforms of the owner, or same root hierarchy
            if (_owner != null)
            {
                if (collision.gameObject == _owner || collision.transform.IsChildOf(_owner.transform) || collision.transform.root == _owner.transform.root) return;
                if (_owner.CompareTag("Player") && collision.CompareTag("Player")) return;
            }
            else if (collision.CompareTag("Player"))
            {
                return; // Guard against frame-0 collisions before Setup assigns owner
            }

            // Ignore Item Drops, Ground, Floor, and Walkable Terrain
            if (collision.GetComponent<CollectibleItem>() != null) return;
            if (collision.CompareTag("Ground") || collision.gameObject.name.ToLower().Contains("ground") || collision.gameObject.name.ToLower().Contains("floor")) return;

            // If it's an arced projectile, it ignores walls mid-air and only explodes on landing (in Update)
            // However, if we want it to hit enemies mid-air or explode on impact:
            if (trajectoryMode == TrajectoryMode.Arced && collision.isTrigger) return;
            if (trajectoryMode == TrajectoryMode.Arced)
            {
                // Optionally allow exploding mid-air if it hits an enemy, but usually grenades fly over until landing
                // If you want it to bounce or pass through, we can just return here.
                // Let's make it explode early if it hits an enemy or wall directly.
            }

            IDamageable damageable = collision.GetComponent<IDamageable>();
            if (damageable != null)
            {
                if (isExplosive)
                {
                    Explode();
                    DeactivateProjectile();
                    return;
                }

                if (!_hitTargets.Contains(damageable))
                {
                    _hitTargets.Add(damageable);
                    Vector3 exactHitPoint = collision.ClosestPoint(transform.position);

                    // 🎯 โมดิฟายดาเมจ: ยิงไกลยิ่งเบา (ต่ำสุด 40%) + สุ่ม +-10 + Critical (20% -> 1.3x หรือตามคลาส)
                    float distanceTraveled = Vector3.Distance(_startPoint, exactHitPoint);
                    DamageResult dmgResult = DamageCalculator.CalculateRangedDamage(
                        _activeDamage,
                        distanceTraveled,
                        _owner,
                        _weaponName,
                        _maxFalloffDistance
                    );

                    damageable.TakeDamage(dmgResult.FinalDamage, exactHitPoint, dmgResult.IsCritical);
                }

                if (!_canPierce)
                {
                    DeactivateProjectile();
                }
                return;
            }

            // Hit solid wall or obstacle
            if (!collision.isTrigger)
            {
                if (isExplosive)
                {
                    Explode();
                }
                else if (!string.IsNullOrEmpty(obstacleHitVfxPoolKey) && ObjectPoolManager.Instance != null)
                {
                    Vector3 hitPt = collision.ClosestPoint(transform.position);
                    Vector3 normal = (transform.position - hitPt).normalized;
                    if (normal.sqrMagnitude < 0.01f) normal = -transform.forward;
                    ObjectPoolManager.Instance.SpawnFromPool(obstacleHitVfxPoolKey, hitPt, Quaternion.LookRotation(normal));
                }
                DeactivateProjectile();
            }
        }

        private void Explode()
        {
            Debug.Log($"[Projectile] Grenade exploded at {transform.position}!");

            // Play Explosion VFX
            if (!string.IsNullOrEmpty(explosionVfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                GameObject explosionVFX = ObjectPoolManager.Instance.SpawnFromPool(explosionVfxPoolKey, transform.position, Quaternion.identity);
                if (explosionVFX != null)
                {
                    PooledParticle pooledParticle = explosionVFX.GetComponent<PooledParticle>();
                    float multiplier = explosionVfxScaleMultiplier > 0 ? explosionVfxScaleMultiplier : 1.0f;

                    if (pooledParticle != null)
                    {
                        pooledParticle.SetPoolKey(explosionVfxPoolKey);
                        pooledParticle.ApplyScaleMultiplier(explosionRadius * multiplier);
                    }
                    else
                    {
                        explosionVFX.transform.localScale = Vector3.one * (explosionRadius * multiplier);
                    }
                }
            }

            // AOE Damage
            Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var target in targets)
            {
                if (_owner != null && target.gameObject == _owner) continue; // No friendly fire to owner
                
                // Deal damage to enemies and obstacles
                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float distFromCenter = Vector3.Distance(transform.position, target.transform.position);
                    DamageResult dmgResult = DamageCalculator.CalculateExplosionDamage(_activeDamage, distFromCenter, explosionRadius, _owner);
                    damageable.TakeDamage(dmgResult.FinalDamage, target.transform.position, dmgResult.IsCritical);
                }
            }
        }

        private void DeactivateProjectile()
        {
            if (_trail != null)
            {
                _trail.Clear();
            }

            if (!string.IsNullOrEmpty(poolKey) && ObjectPoolManager.Instance != null)
            {
                _rb.linearVelocity = Vector3.zero;
                ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
