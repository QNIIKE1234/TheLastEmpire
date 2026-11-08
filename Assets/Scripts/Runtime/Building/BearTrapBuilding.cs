using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// BearTrapBuilding — กับดักหมีฟันเหล็กสปริง (BUILD0019_SnapJawBearTrap)
    /// คุณสมบัติ:
    /// - ทำงานเมื่อศัตรูเดินเหยียบกับดัก (Trigger)
    /// - สร้างความเสียหายกายภาพฉับพลัน 120 หน่วย และตรึงศัตรูให้อยู่กับที่ (Root / Stun) นาน 3.5 วินาที
    /// - เมื่อสปริงทำงานแล้ว ผู้เล่นสามารถเดินมากด [E] เพื่อติดตั้ง (Rearm) กับดักใหม่ได้
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BearTrapBuilding : MonoBehaviour
    {
        [Header("Trap Settings")]
        [Tooltip("ความเสียหายเมื่อกับดักงับ")]
        [SerializeField] private float snapDamage = 120f;

        [Tooltip("ระยะเวลาตรึงศัตรูให้อยู่กับที่ (วินาที)")]
        [SerializeField] private float rootDuration = 3.5f;

        [Tooltip("สถานะว่ากับดักถูกดีดไปแล้วหรือไม่")]
        [SerializeField] private bool isSprung = false;

        [Header("Visual Elements")]
        [Tooltip("ชิ้นส่วนขากรรไกรกับดักเพื่อหุบหรือกาง")]
        [SerializeField] private Transform trapJawsTransform;
        [SerializeField] private float interactionDistance = 2.2f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private bool _isPlayerNearby = false;

        public bool IsSprung => isSprung;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (_buildingObject != null)
            {
                // กับดักวางบนพื้น ไม่ควรบล็อกทางเดินของ NavMesh จนศัตรูเดินอ้อมหนี
                _buildingObject.AutoAddNavMeshObstacle = false;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSprung) return;

            if (other.CompareTag("Enemy") || other.name.Contains("Alien") || other.name.Contains("Zombie") || other.name.Contains("Robot"))
            {
                TriggerTrap(other.gameObject);
            }
        }

        public void TriggerTrap(GameObject victim)
        {
            if (isSprung) return;
            isSprung = true;

            // หุบขากรรไกร (Visual)
            if (trapJawsTransform != null)
            {
                trapJawsTransform.localScale = new Vector3(0.3f, 1f, 1f);
            }

            // สร้างความเสียหาย
            IDamageable damageable = victim.GetComponent<IDamageable>() ?? victim.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(snapDamage, victim.transform.position + Vector3.up * 0.3f, true);
            }

            // ตรึงศัตรูให้อยู่กับที่
            BaseEnemyAI ai = victim.GetComponent<BaseEnemyAI>() ?? victim.GetComponentInParent<BaseEnemyAI>();
            if (ai != null)
            {
                ai.ApplyMeleeKnockback(Vector3.zero, 0f, rootDuration);
            }

            Rigidbody rb = victim.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 0.6f,
                    snapDamage,
                    true,
                    "SNAP! ROOTED"
                );
            }

            Debug.Log($"[BearTrap] Trap snapped on {victim.name}! Dealt {snapDamage} damage and rooted for {rootDuration}s.");
        }

        private void Update()
        {
            if (!isSprung) return;

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            if (_isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                RearmTrap();
            }
        }

        public void RearmTrap()
        {
            isSprung = false;

            if (trapJawsTransform != null)
            {
                trapJawsTransform.localScale = Vector3.one;
            }

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 0.6f,
                    0f,
                    false,
                    "TRAP RE-ARMED"
                );
            }

            Debug.Log("[BearTrap] Bear trap re-armed and ready for action.");
        }

        private void OnGUI()
        {
            if (isSprung && _isPlayerNearby)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.8f);
                if (screenPos.z > 0)
                {
                    float width = 230f;
                    float height = 36f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    GUI.color = new Color(1f, 0.9f, 0.3f, 0.95f);
                    GUI.Box(rect, "[E] ติดตั้งกับดักใหม่ (Rearm Trap)");
                }
            }
        }
    }
}

