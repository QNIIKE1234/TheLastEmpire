using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// StorageCrateBuilding — หีบไม้เก็บสิ่งของ (BUILD0011_WoodenStorageCrate)
    /// คุณสมบัติ:
    /// - ใช้งานร่วมกับ LootContainer เพื่อเป็นคลังเก็บของในฐานทัพ (24 ช่อง)
    /// - ตรวจจับการเปิด-ปิดฝาหีบอย่างราบรื่น
    /// - แสดง Interaction Prompt [E] เปิดหีบเก็บของ
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    [RequireComponent(typeof(LootContainer))]
    public class StorageCrateBuilding : MonoBehaviour
    {
        [Header("Crate Visual Settings")]
        [Tooltip("Transform ของฝาหีบ (หากมี) เพื่อหมุนเปิดเมื่อใช้งาน")]
        [SerializeField] private Transform lidTransform;
        [SerializeField] private float openAngle = -60f;
        [SerializeField] private float animationSpeed = 8.0f;
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private LootContainer _lootContainer;
        private Transform _playerTransform;
        private bool _isPlayerNearby = false;
        private bool _isLidOpen = false;

        private Quaternion _closedLidRot;
        private Quaternion _openLidRot;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            _lootContainer = GetComponent<LootContainer>();

            if (lidTransform == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.IndexOf("lid", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("top", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lidTransform = child;
                        break;
                    }
                }
            }

            if (lidTransform != null)
            {
                _closedLidRot = lidTransform.localRotation;
                _openLidRot = _closedLidRot * Quaternion.Euler(openAngle, 0f, 0f);
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

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            // ตรวจสอบว่า UI คลังเก็บของกำลังเปิดอยู่หรือไม่
            bool isLootUIActive = LootUI.Instance != null && LootUI.Instance.IsOpen && LootUI.Instance.ActiveContainer == _lootContainer;
            _isLidOpen = isLootUIActive;

            if (lidTransform != null)
            {
                Quaternion targetRot = _isLidOpen ? _openLidRot : _closedLidRot;
                lidTransform.localRotation = Quaternion.Slerp(lidTransform.localRotation, targetRot, Time.deltaTime * animationSpeed);
            }
        }
    }
}

