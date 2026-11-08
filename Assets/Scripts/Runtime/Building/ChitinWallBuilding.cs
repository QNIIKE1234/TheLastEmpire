using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// ChitinWallBuilding — กำแพงเปลือกเอเลี่ยน (BUILD0018_ChitinCarapaceWall)
    /// คุณสมบัติ:
    /// - ต้านทานความเสียหายกรดและการกัดกร่อนสูงถึง 80% (Acid / Chemical Immunity)
    /// - ระบบสะท้อนกรด (Caustic Acid Splash): เมื่อศัตรูโจมตีกำแพง จะสาดกรดสะท้อนใส่ผู้โจมตี 20 หน่วย
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class ChitinWallBuilding : MonoBehaviour
    {
        [Header("Chitin Thorns Settings")]
        [Tooltip("ความเสียหายกรดสะท้อนใส่ศัตรูเมื่อถูกโจมตี")]
        [SerializeField] private float acidSplashDamage = 20f;

        [Tooltip("คูลดาวน์การสะท้อนกรด (วินาที)")]
        [SerializeField] private float reflectCooldown = 0.5f;

        private BuildingObject _buildingObject;
        private float _lastReflectTime = 0f;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleReflect(collision.collider);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleReflect(collision.collider);
        }

        private void HandleReflect(Collider col)
        {
            if (col == null) return;
            if (Time.time - _lastReflectTime < reflectCooldown) return;

            if (col.CompareTag("Enemy") || col.name.Contains("Alien") || col.name.Contains("Zombie") || col.name.Contains("Robot"))
            {
                IDamageable damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    _lastReflectTime = Time.time;
                    damageable.TakeDamage(acidSplashDamage, col.bounds.center, false);

                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.SpawnDamageNumber(
                            col.bounds.center + Vector3.up * 1.0f,
                            acidSplashDamage,
                            false,
                            "ACID REFLECT"
                        );
                    }

                    Debug.Log($"[ChitinWall] Splashed {acidSplashDamage} acid back to attacker {col.gameObject.name}!");
                }
            }
        }
    }
}
