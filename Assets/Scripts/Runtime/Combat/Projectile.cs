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

        private Rigidbody _rb;
        private GameObject _owner;
        private float _lifeTimer;

        // Arced trajectory variables
        private Vector3 _startPoint;
        private Vector3 _targetPoint;
        private float _flightDuration;
        private float _currentFlightTime;

        // Dynamic overrides set by Weapon stats
        private float _activeDamage;
        private float _activeLifetime;
        private bool _canPierce = false;
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

            // Set initial defaults
            _activeDamage = damage;
            _activeLifetime = lifetime;
        }

        private void OnEnable()
        {
            _lifeTimer = _activeLifetime;
            _currentFlightTime = 0f;
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

        public void SetStats(float damageVal, float lifetimeVal, bool pierceVal)
        {
            _activeDamage = damageVal;
            _activeLifetime = lifetimeVal;
            _canPierce = pierceVal;
            _lifeTimer = lifetimeVal; // Update active timer
        }

        private void OnTriggerEnter(Collider collision)
        {
            if (_owner != null && collision.gameObject == _owner) return;

            // Ignore Item Drops completely
            if (collision.GetComponent<CollectibleItem>() != null) return;

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
                    damageable.TakeDamage(_activeDamage, exactHitPoint);
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
                if (isExplosive) Explode();
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
                    // Use transform.position as the explosion center
                    damageable.TakeDamage(_activeDamage, transform.position);
                }
            }
        }

        private void DeactivateProjectile()
        {
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
