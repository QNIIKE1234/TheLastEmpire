using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// RazorWireBuilding — ลวดหนามขวางทาง (BUILD0017_RazorWireBarricade)
    /// คุณสมบัติ:
    /// - ชะลอความเร็วของศัตรูที่สัมผัสลง 60% (Entanglement Slow)
    /// - สร้างความเสียหายเลือดไหล (Bleed Damage) 15 หน่วย/วินาที แก่ศัตรูที่พยายามบุกทะลวง
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class RazorWireBuilding : MonoBehaviour
    {
        [Header("Razor Wire Combat Settings")]
        [Tooltip("ความเสียหายเลือดไหลต่อวินาที")]
        [SerializeField] private float bleedDamagePerSecond = 15f;

        [Tooltip("เปอร์เซ็นต์การชะลอความเร็วของศัตรู (0.60 = ลด 60%)")]
        [SerializeField] private float slowMultiplier = 0.40f;

        [Tooltip("ช่วงความถี่ในการคำนวณดาเมจ (วินาที)")]
        [SerializeField] private float tickRate = 0.5f;

        private BuildingObject _buildingObject;
        private float _lastTickTime = 0f;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
        }

        private void OnCollisionStay(Collision collision)
        {
            ApplyRazorWireEffect(collision.collider);
        }

        private void OnTriggerStay(Collider other)
        {
            ApplyRazorWireEffect(other);
        }

        private void ApplyRazorWireEffect(Collider col)
        {
            if (col == null) return;

            // ตรวจสอบว่าเป็นศัตรูหรือไม่
            if (col.CompareTag("Enemy") || col.name.Contains("Alien") || col.name.Contains("Zombie") || col.name.Contains("Robot"))
            {
                // ชะลอความเร็วการเคลื่อนที่
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x * slowMultiplier, rb.linearVelocity.y, rb.linearVelocity.z * slowMultiplier);
                }

                if (Time.time - _lastTickTime >= tickRate)
                {
                    _lastTickTime = Time.time;
                    float dmg = bleedDamagePerSecond * tickRate;

                    IDamageable damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        damageable.TakeDamage(dmg, col.bounds.center, false);

                        if (DamageNumberManager.Instance != null)
                        {
                            DamageNumberManager.Instance.SpawnDamageNumber(
                                col.bounds.center + Vector3.up * 0.8f,
                                dmg,
                                false,
                                "BLEED"
                            );
                        }
                    }
                }
            }
        }
    }
}

