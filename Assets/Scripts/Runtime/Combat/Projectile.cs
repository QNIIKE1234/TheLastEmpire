using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private int damage = 20;
        [SerializeField] private float lifetime = 3f;

        private Rigidbody2D _rb;
        private GameObject _owner;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            // Ensure projectile uses trigger and has no gravity
            _rb.gravityScale = 0f;
            
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Start()
        {
            // Destroy after lifetime ends
            Destroy(gameObject, lifetime);
        }

        public void Setup(Vector2 direction, GameObject owner)
        {
            _owner = owner;
            _rb.linearVelocity = direction.normalized * speed;

            // Rotate projectile to match flight direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Ignore owner collisions (so shooter doesn't hit themselves)
            if (_owner != null && collision.gameObject == _owner) return;

            // Check if object is damageable
            IDamageable damageable = collision.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            // Hit solid wall or obstacle (except triggers)
            if (!collision.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
