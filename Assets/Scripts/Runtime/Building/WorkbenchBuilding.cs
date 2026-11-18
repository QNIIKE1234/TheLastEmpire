using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WorkbenchBuilding — โต๊ะทำงานและคราฟต์ไอเทมผู้รอดชีวิต (BUILD0010_SurvivorWorkbench)
    /// คุณสมบัติ:
    /// - ผู้เล่นสามารถกด [E] เพื่อเปิดหน้าต่างสร้างไอเทม (Craft Menu)
    /// - หากมีโต๊ะคราฟต์วางอยู่ในแมพ (Stage) เดียวกับผู้เล่น จะปลดล็อกฟังก์ชันโต๊ะคราฟต์ (หมวด Building / Defense) ทันทีโดยไม่จำกัดระยะเป็นเมตร
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WorkbenchBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Workbench Settings")]
        [SerializeField] private float interactionDistance = 2.5f;

        private static readonly List<WorkbenchBuilding> _activeWorkbenches = new List<WorkbenchBuilding>();

        private BuildingObject _buildingObject;

        public static WorkbenchBuilding ClosestWorkbench { get; private set; }
        public static bool IsPlayerNearWorkbench => CheckPlayerNearAnyWorkbench();

        /// <summary>
        /// ตรวจสอบว่าโต๊ะคราฟต์ตัวนี้อยู่ในแมพ (Stage) เดียวกับผู้เล่นและยังไม่พังหรือไม่
        /// </summary>
        public bool IsInCurrentMap()
        {
            if (this == null || !isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
            if (gameObject.name.StartsWith("[Ghost]")) return false;
            if (_buildingObject == null) _buildingObject = GetComponent<BuildingObject>();
            if (_buildingObject != null && _buildingObject.IsDead) return false;

            if (WorldMapManager.Instance != null && _buildingObject != null)
            {
                return _buildingObject.StageX == WorldMapManager.Instance.CurrentPlayerX &&
                       _buildingObject.StageY == WorldMapManager.Instance.CurrentPlayerY;
            }

            return true;
        }

        public static bool CheckPlayerNearAnyWorkbench()
        {
            if (ClosestWorkbench != null && ClosestWorkbench.IsInCurrentMap())
            {
                return true;
            }

            ClosestWorkbench = null;

            for (int i = _activeWorkbenches.Count - 1; i >= 0; i--)
            {
                WorkbenchBuilding bench = _activeWorkbenches[i];
                if (bench == null)
                {
                    _activeWorkbenches.RemoveAt(i);
                    continue;
                }

                if (bench.IsInCurrentMap())
                {
                    ClosestWorkbench = bench;
                    return true;
                }
            }

            WorkbenchBuilding[] allBenches = Object.FindObjectsByType<WorkbenchBuilding>(FindObjectsSortMode.None);
            foreach (var bench in allBenches)
            {
                if (bench != null && bench.IsInCurrentMap())
                {
                    if (!_activeWorkbenches.Contains(bench))
                    {
                        _activeWorkbenches.Add(bench);
                    }
                    ClosestWorkbench = bench;
                    return true;
                }
            }
            return false;
        }

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
            if (!_activeWorkbenches.Contains(this))
            {
                _activeWorkbenches.Add(this);
            }
            BuildingInteractionRegistry.Register(this);
            if (IsInCurrentMap())
            {
                ClosestWorkbench = this;
            }
        }

        private void OnDisable()
        {
            _activeWorkbenches.Remove(this);
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

        private void Update()
        {
            if (IsInCurrentMap())
            {
                if (ClosestWorkbench == null || !ClosestWorkbench.IsInCurrentMap())
                {
                    ClosestWorkbench = this;
                }
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
                CraftController.Instance.OpenCraftMenu(true);
                Debug.Log("[WorkbenchBuilding] Opened Craft Menu at Survivor Workbench.");
            }
        }
    }
}
