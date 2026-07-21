using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private float damage = 20;
        [SerializeField] private float decreseMulti = 0.1f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private string poolKey = "";

        private Rigidbody _rb;
        private GameObject _owner;
        private float _lifeTimer;

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
            if (_hitTargets == null)
            {
                _hitTargets = new System.Collections.Generic.List<IDamageable>();
            }
            _hitTargets.Clear();
        }

        private void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                DeactivateProjectile();
            }
        }

        public void Setup(Vector3 direction, GameObject owner)
        {
            _owner = owner;
            _rb.linearVelocity = direction.normalized * speed;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.forward = direction.normalized;
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

            // Check if object is damageable
            IDamageable damageable = collision.GetComponent<IDamageable>();
            if (damageable != null)
            {
                if (!_hitTargets.Contains(damageable))
                {
                    _hitTargets.Add(damageable);
                    damageable.TakeDamage(_activeDamage);
                }

                if (!_canPierce)
                {
                    DeactivateProjectile();
                }
                return;
            }

            // Hit solid wall or obstacle (except triggers)
            if (!collision.isTrigger)
            {
                DeactivateProjectile();
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
