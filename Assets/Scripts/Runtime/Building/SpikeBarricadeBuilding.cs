using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// SpikeBarricadeBuilding — รั้วหนามกั้นทางเดิน
    /// คุณสมบัติ:
    /// - กั้นการเดินของมอนสเตอร์ (บล็อก NavMesh)
    /// - มีระบบสะท้อนความเสียหายหนาม (Thorns Damage) เมื่อมอนสเตอร์เดินชนหรือโจมตี
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class SpikeBarricadeBuilding : MonoBehaviour
    {
        [Header("Thorns Combat Settings")]
        [Tooltip("ความเสียหายหนามที่สะท้อนใส่มอนสเตอร์เมื่อเดินชน")]
        [SerializeField] private float thornsDamage = 25f;

        [Tooltip("คูลดาวน์การสะท้อนหนามต่อศัตรูแต่ละตัว (วินาที)")]
        [SerializeField] private float damageCooldown = 0.8f;

        private float _lastDamageTime = 0f;

        private void OnCollisionStay(Collision collision)
        {
            if (Time.time - _lastDamageTime < damageCooldown) return;

            if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.name.Contains("Alien") || collision.gameObject.name.Contains("Zombie") || collision.gameObject.name.Contains("Robot"))
            {
                IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = collision.gameObject.GetComponentInParent<IDamageable>();
                }

                if (damageable != null && !damageable.IsDead)
                {
                    _lastDamageTime = Time.time;
                    Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
                    damageable.TakeDamage(thornsDamage, hitPoint, false);
                    Debug.Log($"[SpikeBarricade] Dealt {thornsDamage} thorns damage to {collision.gameObject.name}!");
                }
            }
        }
    }
}
