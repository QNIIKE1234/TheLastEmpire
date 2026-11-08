using System;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastEmpire
{
    /// <summary>
    /// BuildingObject — Component หลักที่ติดอยู่กับสิ่งปลูกสร้างจริงทุกชิ้นในฉาก
    /// ควบคุม:
    /// - เลือด HP และการรับความเสียหาย (IDamageable)
    /// - NavMeshObstacle อัตโนมัติ เพื่อให้ศัตรูไม่เดินทะลุ
    /// - การซ่อมแซม (Repair) และการรื้อถอน (Dismantle) คืนวัตถุดิบ 50%
    /// </summary>
    [SelectionBase]
    public class BuildingObject : MonoBehaviour, IDamageable
    {
        [Header("Building Data")]
        [SerializeField] private BuildingData data;
        [SerializeField] private float currentHealth = 250f;

        [Header("NavMesh Settings")]
        [Tooltip("เพิ่ม NavMeshObstacle อัตโนมัติหรือไม่ เพื่อให้มอนสเตอร์เดินอ้อมสิ่งปลูกสร้างนี้")]
        [SerializeField] private bool autoAddNavMeshObstacle = true;

        [Header("Dismantle Settings")]
        [Tooltip("เปอร์เซ็นต์วัตถุดิบที่จะได้คืนเมื่อผู้เล่นรื้อถอน (ค่าเริ่มต้น 50%)")]
        [Range(0f, 1f)]
        [SerializeField] private float dismantleRefundRatio = 0.5f;

        [Header("Audio & VFX")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private GameObject destroyVfxPrefab;

        private NavMeshObstacle _navObstacle;
        private bool _isDestroyed = false;

        public BuildingData Data => data;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => data != null ? data.maxHealth : 250f;
        public bool IsDead => _isDestroyed || currentHealth <= 0f;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action OnDestroyed;

        public void Initialize(BuildingData buildingData)
        {
            data = buildingData;
            if (data != null)
            {
                currentHealth = data.maxHealth;
            }
        }

        private void Awake()
        {
            if (data != null && currentHealth <= 0f)
            {
                currentHealth = data.maxHealth;
            }

            SetupNavMeshObstacle();
        }

        private void Start()
        {
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.TrackBuilding(this);
            }
        }

        private void OnDestroy()
        {
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.UntrackBuilding(this);
            }
        }

        private void SetupNavMeshObstacle()
        {
            if (!autoAddNavMeshObstacle) return;

            _navObstacle = GetComponent<NavMeshObstacle>();
            if (_navObstacle == null)
            {
                _navObstacle = gameObject.AddComponent<NavMeshObstacle>();
            }

            _navObstacle.carving = true;
            _navObstacle.carveOnlyStationary = false;
            _navObstacle.timeToStationary = 0.1f;

            if (data != null)
            {
                _navObstacle.size = data.footprintSize;
                _navObstacle.center = new Vector3(0, data.footprintSize.y * 0.5f, 0);
            }
            else
            {
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    _navObstacle.size = col.bounds.size;
                    _navObstacle.center = col.bounds.center - transform.position;
                }
            }
        }

        // =========================================================================
        //  IDamageable Implementation
        // =========================================================================
        public void TakeDamage(float damageAmount, Vector3? hitPoint = null, bool isCrit = false)
        {
            if (IsDead) return;

            float resistance = data != null ? data.damageResistance : 0f;
            float actualDamage = Mathf.Max(1f, damageAmount * (1f - resistance));

            currentHealth = Mathf.Max(0f, currentHealth - actualDamage);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            Vector3 spawnPos = hitPoint ?? (transform.position + Vector3.up * 1.0f);

            // แสดงตัวเลขดาเมจบนจอ
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(actualDamage, spawnPos, isCrit);
            }

            if (hitVfxPrefab != null)
            {
                Instantiate(hitVfxPrefab, spawnPos, Quaternion.identity);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Repair(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private void Die()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            if (destroyVfxPrefab != null)
            {
                Instantiate(destroyVfxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            OnDestroyed?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>
        /// รื้อถอนสิ่งปลูกสร้าง และคืนวัตถุดิบ 50% เข้ากระเป๋าผู้เล่น
        /// </summary>
        public void Dismantle(PlayerInventory playerInventory)
        {
            if (IsDead) return;

            if (playerInventory != null && data != null && data.ingredients != null)
            {
                foreach (CraftingIngredient ing in data.ingredients)
                {
                    int refundAmount = Mathf.Max(1, Mathf.FloorToInt(ing.amount * dismantleRefundRatio));
                    playerInventory.AddItem(ing.ResolvedItemName, refundAmount);
                    Debug.Log($"[BuildingObject] Dismantled '{data.buildingName}': Refunded '{ing.ResolvedItemName}' x{refundAmount}");
                }
            }

            Die();
        }
    }
}
