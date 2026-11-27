using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// HUDNavigationManager — ตัวจัดการรวมสำหรับปุ่มควบคุม HUD หลักทั้ง 4 ปุ่ม (Upgrade [U], Inventory [I], Craft/Build [C], Setting [ESC])
    /// รองรับ Auto-Initialize ในฉาก (รวมถึงตอนย้ายซีนจาก MainMenu -> Gameplay_3D) + คลิกซ้ำหรือกดคีย์ลัดเพื่อสลับเปิด/ปิดได้สมบูรณ์ + สลับหน้าต่างไม่ซ้อนทับกัน
    /// </summary>
    public class HUDNavigationManager : MonoBehaviour
    {
        public static HUDNavigationManager Instance { get; private set; }
        private static bool _sceneHookRegistered = false;

        [Header("Bottom HUD Buttons (4 ปุ่มมุมขวาล่าง: U, I, C, ESC)")]
        [Tooltip("ปุ่มเปิด/ปิดหน้าต่างแต่งอาวุธ Artifact (ปุ่ม [U])")]
        [SerializeField] private Button upgradeButton;

        [Tooltip("ปุ่มเปิด/ปิดหน้าต่างกระเป๋า Inventory (รูปกระเป๋า [i])")]
        [SerializeField] private Button inventoryButton;

        [Tooltip("ปุ่มเปิด/ปิดหน้าต่างสร้าง / คราฟต์ (รูปประแจ [C])")]
        [SerializeField] private Button buildingButton;

        [Tooltip("ปุ่มเปิด/ปิดหน้าต่างตั้งค่า Setting (รูปฟันเฟือง [ESC])")]
        [SerializeField] private Button settingButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterAndEnsureInScene()
        {
            if (!_sceneHookRegistered)
            {
                _sceneHookRegistered = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            EnsureInScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInScene();
        }

        private static void EnsureInScene()
        {
            if (Instance != null)
            {
                Instance.AutoWireButtons();
                return;
            }
            HUDNavigationManager existing = UnityEngine.Object.FindFirstObjectByType<HUDNavigationManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.AutoWireButtons();
                return;
            }

            GameObject menuHud = GameObject.Find("Menu_HUD");
            if (menuHud != null)
            {
                HUDNavigationManager nav = menuHud.AddComponent<HUDNavigationManager>();
                nav.AutoWireButtons();
            }
            else
            {
                // ค้นหาจาก Canvas ทั้งหมดเผื่อ Menu_HUD ยังไม่ได้ active หรืออยู่ใต้ลูกหลาน
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] == null) continue;
                    Transform[] children = canvases[i].GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < children.Length; j++)
                    {
                        if (children[j] != null && children[j].name == "Menu_HUD")
                        {
                            HUDNavigationManager nav = children[j].gameObject.AddComponent<HUDNavigationManager>();
                            nav.AutoWireButtons();
                            return;
                        }
                    }
                }
            }
        }

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            AutoWireButtons();
        }

        private void Update()
        {
            // ปุ่มลัด [U] เปิด/ปิดหน้าต่าง Weapon Upgrade / Artifact
            bool uPressed = Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.U)) uPressed = true;
#endif
            if (uPressed && !IsTypingInInputField())
            {
                ToggleWeaponUpgrade();
            }
        }

        public static bool IsTypingInInputField()
        {
            if (CraftController.Instance != null && CraftController.Instance.IsSearchFocused)
                return true;

            if (EventSystem.current == null) return false;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return false;

            TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused) return true;

            InputField legacyInput = selected.GetComponentInParent<InputField>();
            if (legacyInput != null && legacyInput.isFocused) return true;

            return false;
        }

        public static bool TryUnfocusActiveInputField()
        {
            if (!IsTypingInInputField()) return false;

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
                if (tmpInput != null && tmpInput.isFocused)
                {
                    tmpInput.DeactivateInputField();
                }
                else
                {
                    InputField legacyInput = selected.GetComponentInParent<InputField>();
                    if (legacyInput != null && legacyInput.isFocused)
                    {
                        legacyInput.DeactivateInputField();
                    }
                }
                EventSystem.current.SetSelectedGameObject(null);
            }
            return true;
        }

        private void AutoWireButtons()
        {
            // ค้นหาทั้งในลูกหลานและทั้งฉาก (กรณีแนบไว้คนละ GameObject กับ Menu_HUD)
            if (upgradeButton == null || buildingButton == null || inventoryButton == null || settingButton == null)
            {
                Button[] localButtons = GetComponentsInChildren<Button>(true);
                MatchButtons(localButtons);

                if (upgradeButton == null || buildingButton == null || inventoryButton == null || settingButton == null)
                {
                    GameObject menuHud = GameObject.Find("Menu_HUD");
                    if (menuHud != null)
                    {
                        MatchButtons(menuHud.GetComponentsInChildren<Button>(true));
                    }
                }
            }

            BindToggleButton(upgradeButton, ToggleWeaponUpgrade);
            BindToggleButton(inventoryButton, ToggleInventory);
            BindToggleButton(buildingButton, ToggleBuilding);
            BindToggleButton(settingButton, ToggleSetting);
        }

        private void MatchButtons(Button[] buttons)
        {
            if (buttons == null) return;
            foreach (Button btn in buttons)
            {
                if (btn == null) continue;
                string bName = btn.gameObject.name.ToLowerInvariant();
                if (upgradeButton == null && (bName.Contains("upgrade") || bName.Contains("artifact")))
                {
                    upgradeButton = btn;
                }
                else if (buildingButton == null && (bName.Contains("craft") || bName.Contains("build") || bName.Contains("wrench")))
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

        private static void BindToggleButton(Button btn, UnityEngine.Events.UnityAction action)
        {
            if (btn == null || action == null) return;

            // ล้าง persistent SetActive(true) ที่อาจหลงเหลือใน Scene/Prefab เพื่อไม่ให้บังคับเปิดค้างจนปิดไม่ได้
            bool hasLegacySetActive = false;
            int persistentCount = btn.onClick.GetPersistentEventCount();
            for (int i = 0; i < persistentCount; i++)
            {
                if (btn.onClick.GetPersistentMethodName(i) == "SetActive")
                {
                    hasLegacySetActive = true;
                    break;
                }
            }
            if (hasLegacySetActive)
            {
                btn.onClick = new Button.ButtonClickedEvent();
            }

            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
        }

        // =========================================================================
        //  1. Building / Crafting Panel [C]
        // =========================================================================

        public void OpenBuilding()
        {
            CloseOtherMainPanels(keepBuilding: true);
            CraftController craft = GetCraftController();
            if (craft != null)
            {
                craft.OpenCraftMenu();
            }
        }

        public void CloseBuilding()
        {
            CraftController craft = GetCraftController();
            if (craft != null)
            {
                craft.CloseCraftMenu();
            }
        }

        public void ToggleBuilding()
        {
            CraftController craft = GetCraftController();
            if (craft == null) return;
            if (craft.IsOpen)
            {
                craft.CloseCraftMenu();
            }
            else
            {
                CloseOtherMainPanels(keepBuilding: true);
                craft.OpenCraftMenu();
            }
        }

        // =========================================================================
        //  2. Inventory Panel [I]
        // =========================================================================

        public void OpenInventory()
        {
            CloseOtherMainPanels(keepInventory: true);
            InventoryUI inv = GetInventoryUI();
            if (inv != null)
            {
                inv.OpenInventory();
            }
        }

        public void CloseInventory()
        {
            InventoryUI inv = GetInventoryUI();
            if (inv != null)
            {
                inv.CloseInventory();
            }
        }

        public void ToggleInventory()
        {
            InventoryUI inv = GetInventoryUI();
            if (inv == null) return;
            if (inv.IsOpen)
            {
                inv.CloseInventory();
            }
            else
            {
                CloseOtherMainPanels(keepInventory: true);
                inv.OpenInventory();
            }
        }

        // =========================================================================
        //  3. Setting Panel [ESC]
        // =========================================================================

        public void OpenSetting()
        {
            CloseOtherMainPanels(keepSetting: true);
            SettingManager set = GetSettingManager();
            if (set != null)
            {
                set.OpenSettingsModal();
            }
        }

        public void CloseSetting()
        {
            SettingManager set = GetSettingManager();
            if (set != null)
            {
                set.CloseSettingsModal();
            }
        }

        public void ToggleSetting()
        {
            SettingManager set = GetSettingManager();
            if (set == null) return;
            if (set.IsOpen)
            {
                set.CloseSettingsModal();
            }
            else
            {
                CloseOtherMainPanels(keepSetting: true);
                set.OpenSettingsModal();
            }
        }

        // =========================================================================
        //  3.5 Weapon Upgrade / Artifact Panel [U]
        // =========================================================================

        public void OpenWeaponUpgrade()
        {
            CloseOtherMainPanels(keepUpgrade: true);
            WeaponUpgradeUI.Instance?.Open();
        }

        public void CloseWeaponUpgrade()
        {
            if (WeaponUpgradeUI.HasInstance) WeaponUpgradeUI.Instance.Close();
        }

        public void ToggleWeaponUpgrade()
        {
            WeaponUpgradeUI ui = WeaponUpgradeUI.Instance;
            if (ui == null) return;
            if (ui.IsOpen)
            {
                ui.Close();
            }
            else
            {
                CloseOtherMainPanels(keepUpgrade: true);
                ui.Open();
            }
        }

        private void CloseOtherMainPanels(bool keepBuilding = false, bool keepInventory = false, bool keepSetting = false, bool keepUpgrade = false)
        {
            if (!keepBuilding)
            {
                CraftController craft = GetCraftController();
                if (craft != null && craft.IsOpen) craft.CloseCraftMenu();
            }
            if (!keepInventory)
            {
                InventoryUI inv = GetInventoryUI();
                if (inv != null && inv.IsOpen) inv.CloseInventory();
            }
            if (!keepSetting)
            {
                SettingManager set = GetSettingManager();
                if (set != null && set.IsOpen) set.CloseSettingsModal();
            }
            if (!keepUpgrade && WeaponUpgradeUI.HasInstance && WeaponUpgradeUI.Instance.IsOpen)
            {
                WeaponUpgradeUI.Instance.Close();
            }
        }

        // =========================================================================
        //  4. Back Button & Close All Helper
        // =========================================================================

        public void OnBackButtonClicked()
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps)
            {
                PopUpManager.Instance.Pop();
                return;
            }

            CloseAll();
        }

        public void CloseAll()
        {
            CloseBuilding();
            CloseInventory();
            CloseSetting();
            CloseWeaponUpgrade();
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

