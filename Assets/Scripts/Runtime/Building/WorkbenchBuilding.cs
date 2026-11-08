using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// WorkbenchBuilding — โต๊ะทำงานและคราฟต์ไอเทมผู้รอดชีวิต (BUILD0010_SurvivorWorkbench)
    /// คุณสมบัติ:
    /// - ผู้เล่นสามารถกด [E] เพื่อเปิดหน้าต่างสร้างไอเทม (Craft Menu)
    /// - มอบสถานะ "Near Workbench" ช่วยปลดล็อกสูตรคราฟต์ระดับสูงหรือเพิ่มความเร็ว
    /// - มีช่องเก็บของ 26 ช่องสำหรับสำรองวัตถุดิบคราฟต์ (LootContainer)
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WorkbenchBuilding : MonoBehaviour
    {
        [Header("Workbench Settings")]
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private bool _isPlayerNearby = false;

        public static WorkbenchBuilding ClosestWorkbench { get; private set; }
        public static bool IsPlayerNearWorkbench => ClosestWorkbench != null;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
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

            if (_isPlayerNearby)
            {
                ClosestWorkbench = this;

                // กด E เพื่อเปิดเมนูคราฟต์
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    OpenCraftStation();
                }
            }
            else if (ClosestWorkbench == this)
            {
                ClosestWorkbench = null;
            }
        }

        public void OpenCraftStation()
        {
            if (CraftController.Instance != null)
            {
                CraftController.Instance.OpenCraftMenu();
                Debug.Log("[WorkbenchBuilding] Opened Craft Menu at Survivor Workbench.");
            }
        }

        private void OnDestroy()
        {
            if (ClosestWorkbench == this)
            {
                ClosestWorkbench = null;
            }
        }

        private void OnGUI()
        {
            if (_isPlayerNearby)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
                if (screenPos.z > 0)
                {
                    float width = 230f;
                    float height = 38f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
                    GUI.Box(rect, "[E] ใช้โต๊ะคราฟต์ (Use Workbench)");
                }
            }
        }
    }
}
