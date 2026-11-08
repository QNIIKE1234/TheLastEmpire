using UnityEngine;
using UnityEngine.UI;

namespace TheLastEmpire
{
    /// <summary>
    /// HUDNavigationManager — ตัวจัดการรวมสำหรับปุ่มควบคุม HUD หลัก (Building, Inventory, Setting)
    /// สามารถนำไปแนบไว้ที่ Game_HUD หรือ Canvas แล้วลากฟังก์ชันใส่ OnClick ของปุ่ม UI ใน Unity Inspector ได้ทันที
    /// </summary>
    public class HUDNavigationManager : MonoBehaviour
    {
        public static HUDNavigationManager Instance { get; private set; }

        [Header("Bottom HUD Buttons (Optional: ลากปุ่มมุมขวาล่างมาใส่ หรือปล่อยว่างให้ค้นหาอัตโนมัติ)")]
        [Tooltip("ปุ่มเปิดหน้าต่างสร้าง / คราฟต์ (รูปประแจ [C])")]
        [SerializeField] private Button buildingButton;

        [Tooltip("ปุ่มเปิดหน้าต่างกระเป๋า Inventory (รูปกระเป๋า [i])")]
        [SerializeField] private Button inventoryButton;

        [Tooltip("ปุ่มเปิดหน้าต่างตั้งค่า Setting (รูปฟันเฟือง [ESC])")]
        [SerializeField] private Button settingButton;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            AutoWireButtons();
        }

        private void Start()
        {
            AutoWireButtons();
        }

        private void AutoWireButtons()
        {
            // ค้นหาปุ่มอัตโนมัติจากลูกหลานหากยังไม่ได้ลากใส่ใน Inspector
            if (buildingButton == null || inventoryButton == null || settingButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (Button btn in buttons)
                {
                    string bName = btn.gameObject.name.ToLowerInvariant();
                    if (buildingButton == null && (bName.Contains("craft") || bName.Contains("build") || bName.Contains("wrench")))
                    {
                        buildingButton = btn;
                    }
                    else if (inventoryButton == null && (bName.Contains("inv") || bName.Contains("bag") || bName.Contains("backpack")))
                    {
                        inventoryButton = btn;
                    }
                    else if (settingButton == null && (bName.Contains("set") || bName.Contains("gear") || bName.Contains("cog") || bName.Contains("option")))
                    {
                        settingButton = btn;
                    }
                }
            }

            if (buildingButton != null)
            {
                buildingButton.onClick.RemoveListener(ToggleBuilding);
                buildingButton.onClick.AddListener(ToggleBuilding);
            }

            if (inventoryButton != null)
            {
                inventoryButton.onClick.RemoveListener(ToggleInventory);
                inventoryButton.onClick.AddListener(ToggleInventory);
            }

            if (settingButton != null)
            {
                settingButton.onClick.RemoveListener(ToggleSetting);
                settingButton.onClick.AddListener(ToggleSetting);
            }
        }

        // =========================================================================
        //  1. Building / Crafting Panel
        // =========================================================================

        /// <summary>
        /// เปิดหน้าต่างสร้าง / คราฟต์
        /// </summary>
        public void OpenBuilding()
        {
            CraftController craft = GetCraftController();
            if (craft != null)
            {
                craft.OpenCraftMenu();
            }
        }

        /// <summary>
        /// ปิดหน้าต่างสร้าง / คราฟต์ (สำหรับปุ่ม Back หรือ Close)
        /// </summary>
        public void CloseBuilding()
        {
            CraftController craft = GetCraftController();
            if (craft != null)
            {
                craft.CloseCraftMenu();
            }
        }

        /// <summary>
        /// สลับเปิด/ปิดหน้าต่างสร้าง
        /// </summary>
        public void ToggleBuilding()
        {
            CraftController craft = GetCraftController();
            if (craft != null)
            {
                craft.ToggleCraftMenu();
            }
        }

        // =========================================================================
        //  2. Inventory Panel
        // =========================================================================

        /// <summary>
        /// เปิดหน้าต่างกระเป๋า Inventory
        /// </summary>
        public void OpenInventory()
        {
            InventoryUI inv = GetInventoryUI();
            if (inv != null)
            {
                inv.OpenInventory();
            }
        }

        /// <summary>
        /// ปิดหน้าต่างกระเป๋า Inventory (สำหรับปุ่ม Back หรือ Close)
        /// </summary>
        public void CloseInventory()
        {
            InventoryUI inv = GetInventoryUI();
            if (inv != null)
            {
                inv.CloseInventory();
            }
        }

        /// <summary>
        /// สลับเปิด/ปิดหน้าต่างกระเป๋า Inventory
        /// </summary>
        public void ToggleInventory()
        {
            InventoryUI inv = GetInventoryUI();
            if (inv != null)
            {
                inv.ToggleInventory();
            }
        }

        // =========================================================================
        //  3. Setting Panel
        // =========================================================================

        /// <summary>
        /// เปิดหน้าต่างตั้งค่า Setting
        /// </summary>
        public void OpenSetting()
        {
            SettingManager set = GetSettingManager();
            if (set != null)
            {
                set.OpenSettingsModal();
            }
        }

        /// <summary>
        /// ปิดหน้าต่างตั้งค่า Setting (สำหรับปุ่ม Back หรือ Close)
        /// </summary>
        public void CloseSetting()
        {
            SettingManager set = GetSettingManager();
            if (set != null)
            {
                set.CloseSettingsModal();
            }
        }

        /// <summary>
        /// สลับเปิด/ปิดหน้าต่างตั้งค่า Setting
        /// </summary>
        public void ToggleSetting()
        {
            SettingManager set = GetSettingManager();
            if (set != null)
            {
                set.ToggleSettingsModal();
            }
        }

        // =========================================================================
        //  4. Back Button & Close All Helper
        // =========================================================================

        /// <summary>
        /// กดปุ่ม Back ทั่วไป: ปิด PopUp บนสุด หรือปิดหน้าต่างที่เปิดอยู่
        /// </summary>
        public void OnBackButtonClicked()
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps)
            {
                PopUpManager.Instance.Pop();
                return;
            }

            CloseBuilding();
            CloseInventory();
            CloseSetting();
        }

        /// <summary>
        /// ปิดหน้าต่างหลักทั้ง 3 หน้าต่างพร้อมกัน
        /// </summary>
        public void CloseAll()
        {
            CloseBuilding();
            CloseInventory();
            CloseSetting();
        }

        // =========================================================================
        //  Helper Methods (ค้นหา Component แม้ GameObject เริ่มต้นด้วย Inactive)
        // =========================================================================

        private CraftController GetCraftController()
        {
            if (CraftController.Instance != null) return CraftController.Instance;
            return UnityEngine.Object.FindFirstObjectByType<CraftController>(FindObjectsInactive.Include);
        }

        private InventoryUI GetInventoryUI()
        {
            if (InventoryUI.Instance != null) return InventoryUI.Instance;
            return UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        }

        private SettingManager GetSettingManager()
        {
            if (SettingManager.Instance != null) return SettingManager.Instance;
            return UnityEngine.Object.FindFirstObjectByType<SettingManager>(FindObjectsInactive.Include);
        }
    }
}
