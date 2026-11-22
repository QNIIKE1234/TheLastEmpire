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
    public class StorageCrateBuilding : MonoBehaviour, IBuildingInteractable
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

        public LootContainer Container => _lootContainer;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.4f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            return "[E] Storage";
        }

        public void OnInteract()
        {
            if (_lootContainer == null) _lootContainer = GetComponent<LootContainer>();
            if (_lootContainer == null) return;

            PlayerController pc = TheLastEmpire.Network.PlayerRegistry.LocalPlayer
                               ?? PlayerController.Instance
                               ?? Object.FindFirstObjectByType<PlayerController>();
            PlayerInventory inv = pc != null ? pc.GetComponent<PlayerInventory>() : PlayerInventory.Instance;

            if (StorageUI.Instance != null)
            {
                StorageUI.Instance.Open(_lootContainer, inv, this);
                return;
            }

            if (LootUI.Instance != null && inv != null)
            {
                LootUI.Instance.Open(_lootContainer, inv);
            }
        }

        public void OnSecondaryInteract()
        {
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

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
            bool isStorageOpen = StorageUI.HasInstance && StorageUI.Instance.IsOpen && StorageUI.Instance.CurrentContainer == _lootContainer;
            bool isLootUIActive = LootUI.HasInstance && LootUI.Instance.IsOpen && LootUI.Instance.CurrentContainer == _lootContainer;
            _isLidOpen = isStorageOpen || isLootUIActive;

            if (lidTransform != null)
            {
                Quaternion targetRot = _isLidOpen ? _openLidRot : _closedLidRot;
                lidTransform.localRotation = Quaternion.Slerp(lidTransform.localRotation, targetRot, Time.deltaTime * animationSpeed);
            }
        }
    }
}

