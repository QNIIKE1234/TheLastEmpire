using UnityEngine;

namespace TheLastEmpire
{
    public class DamageDealer : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int damageAmount = 10;
        [SerializeField] private bool destroyOnHit = false;

        [Header("Faction Filter")]
        [Tooltip("If the hit target has this tag, it will not receive damage (prevents friendly fire).")]
        [SerializeField] private string tagToIgnore = "";

        public int DamageAmount
        {
            get => damageAmount;
            set => damageAmount = value;
        }

        public string TagToIgnore
        {
            get => tagToIgnore;
            set => tagToIgnore = value;
        }

        private void OnTriggerEnter(Collider collision)
        {
            HandleDamage(collision.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleDamage(collision.gameObject);
        }

        private void HandleDamage(GameObject target)
        {
            // Skip damage if the target belongs to the ignored faction/tag
            if (!string.IsNullOrEmpty(tagToIgnore) && target.CompareTag(tagToIgnore))
            {
                return;
            }

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damageAmount);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
