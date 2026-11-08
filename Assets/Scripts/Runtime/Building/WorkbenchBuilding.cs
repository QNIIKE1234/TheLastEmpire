using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WorkbenchBuilding — โต๊ะทำงานและคราฟต์ไอเทมผู้รอดชีวิต (BUILD0010_SurvivorWorkbench)
    /// คุณสมบัติ:
    /// - ผู้เล่นสามารถกด [E] เพื่อเปิดหน้าต่างสร้างไอเทม (Craft Menu)
    /// - มอบสถานะ "Near Workbench" ช่วยปลดล็อกสูตรคราฟต์ระดับสูงหรือเพิ่มความเร็ว
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WorkbenchBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Workbench Settings")]
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;

        public static WorkbenchBuilding ClosestWorkbench { get; private set; }
        public static bool IsPlayerNearWorkbench => ClosestWorkbench != null;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.5f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            return "<color=yellow><b>[E]</b></color> Crafting Table";
        }

        public void OnInteract()
        {
            OpenCraftStation();
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
            if (ClosestWorkbench == this)
            {
                ClosestWorkbench = null;
            }
        }

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
            bool isNearby = dist <= interactionDistance;

            if (isNearby)
            {
                ClosestWorkbench = this;
            }
            else if (ClosestWorkbench == this)
            {
                ClosestWorkbench = null;
            }
        }

        public void OpenCraftStation()
        {
            // ตรวจสอบทั้ง Workbench_HUD ภายใต้ BuildingUI_Canvas และ CraftController
            GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
            if (canvasGo != null)
            {
                Transform hud = canvasGo.transform.Find("Workbench_HUD");
                if (hud != null && hud.childCount > 0)
                {
                    hud.gameObject.SetActive(true);
                    Debug.Log("[WorkbenchBuilding] Opened Workbench_HUD.");
                    return;
                }
            }

            if (CraftController.Instance != null)
            {
                CraftController.Instance.OpenCraftMenu();
                Debug.Log("[WorkbenchBuilding] Opened Craft Menu at Survivor Workbench.");
            }
        }
    }
}
