using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    public class DamageDealer : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int damageAmount = 10;
        [SerializeField] private bool destroyOnHit = false;
        [Tooltip("Seconds between repeated contact/melee hits while touching a stationary target")]
        [SerializeField] private float repeatDamageInterval = 0.85f;
        [Tooltip("Extra melee reach check (meters) so standing players keep getting attacked even if PhysX contact sleeps")]
        [SerializeField] private float standingMeleeReach = 1.25f;

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

        private BaseEnemyAI _selfEnemyAi;
        private Health _selfHealth;
        private Collider _selfCollider;
        private float _nextDamageTime = 0f;

        private void Awake()
        {
            _selfEnemyAi = GetComponent<BaseEnemyAI>();
            _selfHealth = GetComponent<Health>();
            _selfCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            _nextDamageTime = 0f;
        }

        private void FixedUpdate()
        {
            // Fallback proximity melee tick for enemy units so standing completely still never stops incoming hits
            if (destroyOnHit || _selfEnemyAi == null) return;
            if (Time.time < _nextDamageTime) return;
            if (_selfHealth != null && _selfHealth.IsDead) return;
            if (_selfEnemyAi.IsStaggered) return;

            PlayerController localPc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPc == null || !localPc.gameObject.activeInHierarchy || localPc.IsRemotePlayer || localPc.IsDowned) return;

            Vector3 myPos = transform.position;
            Vector3 pPos = localPc.transform.position;
            Vector3 flatDiff = new Vector3(pPos.x - myPos.x, 0f, pPos.z - myPos.z);
            float flatDist = flatDiff.magnitude;

            bool withinReach = flatDist <= standingMeleeReach;
            if (!withinReach && _selfCollider != null)
            {
                Vector3 closest = _selfCollider.ClosestPoint(pPos);
                Vector3 edgeDiff = new Vector3(pPos.x - closest.x, 0f, pPos.z - closest.z);
                if (edgeDiff.magnitude <= 0.65f)
                {
                    withinReach = true;
                }
            }

            if (withinReach)
            {
                HandleDamage(localPc.gameObject);
            }
        }

        private void OnTriggerEnter(Collider collision)
        {
            HandleDamage(collision.gameObject);
        }

        private void OnTriggerStay(Collider collision)
        {
            HandleDamage(collision.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleDamage(collision.gameObject);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleDamage(collision.gameObject);
        }

        private void HandleDamage(GameObject target)
        {
            if (target == null) return;

            // Skip damage if the target belongs to the ignored faction/tag
            if (!string.IsNullOrEmpty(tagToIgnore) && target.CompareTag(tagToIgnore))
            {
                return;
            }

            if (!destroyOnHit)
            {
                if (Time.time < _nextDamageTime) return;
                if (_selfHealth != null && _selfHealth.IsDead) return;
                if (_selfEnemyAi != null && _selfEnemyAi.IsStaggered) return;
            }

            PlayerController pc = target.GetComponent<PlayerController>();
            if (pc != null)
            {
                // Each player owns their own HP in Co-op; ignore remote player avatars and already-downed players
                if (pc.IsRemotePlayer || pc.IsDowned) return;
            }

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                if (!destroyOnHit)
                {
                    _nextDamageTime = Time.time + Mathf.Max(0.15f, repeatDamageInterval);
                }

                if (_selfEnemyAi != null)
                {
                    _selfEnemyAi.TriggerAttackAnimation();
                }

                damageable.TakeDamage(damageAmount);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
