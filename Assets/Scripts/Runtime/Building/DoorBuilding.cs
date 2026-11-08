using UnityEngine;
using UnityEngine.AI;

namespace TheLastEmpire
{
    /// <summary>
    /// DoorBuilding — ประตูเข้า-ออกฐานที่มั่น
    /// รองรับ:
    /// - BUILD0004_ReinforcedWoodenDoor (ประตูไม้เสริมเหล็ก)
    /// - BUILD0008_MotorizedBlastDoor (ประตูบังเกอร์กลไกไฟฟ้า)
    /// 
    /// คุณสมบัติ:
    /// - ผู้เล่นสามารถกด [E] เพื่อเปิดหรือปิดประตู (ทำงานร่วมกับ Loot_HUD รวมศูนย์)
    /// - เมื่อประตูเปิด: ปิด NavMeshObstacle และ Collider ทางผ่านเพื่อให้ผู้เล่นเดินผ่านได้
    /// - เมื่อประตูปิด: เปิด NavMeshObstacle Carving เพื่อกั้นศัตรูไม่ให้บุกเข้ามา
    /// - ประตูมอเตอร์ (Motorized) รองรับการตั้งเวลาปิดอัตโนมัติ (Auto Close)
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class DoorBuilding : MonoBehaviour, IBuildingInteractable
    {
        public enum DoorType
        {
            HingeSwing,    // หมุนเปิด-ปิด 90 องศา (ประตูไม้)
            VerticalSlide  // เลื่อนขึ้น-ลงแนวดิ่ง (ประตูกลไก Blast Door)
        }

        [Header("Door Mechanism")]
        [SerializeField] private DoorType doorType = DoorType.HingeSwing;
        [SerializeField] private bool isOpen = false;
        [SerializeField] private float animationSpeed = 5.0f;
        [SerializeField] private float interactionDistance = 2.5f;

        [Header("Motorized Settings")]
        [Tooltip("เปิดระบบปิดประตูอัตโนมัติหรือไม่ (สำหรับ Blast Door)")]
        [SerializeField] private bool autoClose = false;
        [Tooltip("เวลาก่อนประตูปิดอัตโนมัติ (วินาที)")]
        [SerializeField] private float autoCloseDelay = 8.0f;

        [Header("Transforms & Colliders")]
        [Tooltip("ชิ้นส่วนบานประตูที่ขยับ (หากไม่ใส่จะใช้ตัวแม่หรือจัดการทางผ่าน)")]
        [SerializeField] private Transform doorLeaf;
        [Tooltip("Collider ที่ใช้กั้นทางเดิน (จะถูกปิดหรือสลับเมื่อเปิดประตู)")]
        [SerializeField] private Collider doorBlockerCollider;

        private BuildingObject _buildingObject;
        private NavMeshObstacle _navObstacle;
        private float _autoCloseTimer = 0f;

        private Quaternion _closedRotation;
        private Quaternion _openRotation;
        private Vector3 _closedPosition;
        private Vector3 _openPosition;

        public bool IsOpen => isOpen;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.6f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            return isOpen 
                ? "<color=yellow><b>[E]</b></color> ปิดประตู (Close Door)" 
                : "<color=yellow><b>[E]</b></color> เปิดประตู (Open Door)";
        }

        public void OnInteract()
        {
            ToggleDoor();
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
            _navObstacle = GetComponent<NavMeshObstacle>();

            if (doorBlockerCollider == null)
            {
                doorBlockerCollider = GetComponent<BoxCollider>();
            }

            // หาบานประตูจากลูก ถ้าไม่ได้กำหนด
            if (doorLeaf == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("leaf", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("mesh", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        doorLeaf = child;
                        break;
                    }
                }
            }

            if (doorLeaf != null)
            {
                _closedRotation = doorLeaf.localRotation;
                _openRotation = _closedRotation * Quaternion.Euler(0f, 90f, 0f);

                _closedPosition = doorLeaf.localPosition;
                _openPosition = _closedPosition + Vector3.up * 2.5f;
            }

            UpdateDoorState(isOpen, true);
        }

        private void Update()
        {
            // จัดการระบบปิดอัตโนมัติ (สำหรับ Blast Door)
            if (isOpen && autoClose)
            {
                _autoCloseTimer += Time.deltaTime;
                if (_autoCloseTimer >= autoCloseDelay)
                {
                    CloseDoor();
                }
            }

            AnimateDoor();
        }

        public void ToggleDoor()
        {
            SetDoorState(!isOpen);
        }

        public void OpenDoor()
        {
            SetDoorState(true);
        }

        public void CloseDoor()
        {
            SetDoorState(false);
        }

        public void SetDoorState(bool open)
        {
            if (isOpen == open) return;
            isOpen = open;
            _autoCloseTimer = 0f;
            UpdateDoorState(isOpen, false);

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 1.8f,
                    0f,
                    false,
                    isOpen ? "DOOR OPENED" : "DOOR CLOSED"
                );
            }

            Debug.Log($"[DoorBuilding] {gameObject.name} is now {(isOpen ? "OPEN" : "CLOSED")}.");
        }

        private void UpdateDoorState(bool open, bool immediate)
        {
            // จัดการ NavMeshObstacle: เมื่อเปิด ให้ยกเลิกการบล็อกทางเดิน
            if (_navObstacle != null)
            {
                _navObstacle.carving = !open;
                _navObstacle.enabled = !open;
            }

            // จัดการ Blocker Collider: เมื่อเปิดให้ทะลุได้
            if (doorBlockerCollider != null)
            {
                doorBlockerCollider.isTrigger = open;
            }
        }

        private void AnimateDoor()
        {
            if (doorLeaf == null) return;

            if (doorType == DoorType.HingeSwing)
            {
                Quaternion targetRot = isOpen ? _openRotation : _closedRotation;
                doorLeaf.localRotation = Quaternion.Slerp(doorLeaf.localRotation, targetRot, Time.deltaTime * animationSpeed);
            }
            else if (doorType == DoorType.VerticalSlide)
            {
                Vector3 targetPos = isOpen ? _openPosition : _closedPosition;
                doorLeaf.localPosition = Vector3.Lerp(doorLeaf.localPosition, targetPos, Time.deltaTime * animationSpeed);
            }
        }
    }
}
