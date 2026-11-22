using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// WeaponUpgradeUI — ควบคุมหน้าต่างปรับแต่งอาวุธและติดตั้ง Artifact (WeaponUpgrade_HUD)
    /// - ค้นหารายการ Weapon ที่มีอยู่ในกระเป๋า (PlayerInventory) ให้กดเลื่อนซ้าย-ขวาเลือกอาวุธได้
    /// - ค้นหารายการ Artifact ที่มีอยู่ในกระเป๋า (PlayerInventory) มาแสดงฝั่งซ้ายพร้อมแบ่งหน้า (Pagination)
    /// - 1 อาวุธติดตั้ง Artifact ได้สูงสุด 4 ช่อง (A1, A2, A3, A4)
    /// - กดปุ่ม EQUIP หรือลากวาง (Drag & Drop) Artifact ไปฝั่งขวาเพื่อติดตั้ง / กดที่ช่อง A1..A4 เพื่อถอดคืนกระเป๋า
    /// - Auto-Bind อ้างอิงทุกชิ้นจาก WeaponUpgrade_HUD อัตโนมัติ (รองรับทั้งชื่อ ArtifactPanel และ ARTIFACT)
    /// </summary>
    public class WeaponUpgradeUI : MonoBehaviour, IPopUp
    {
        public static WeaponUpgradeUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindExistingOrBindInScene();
                }
                return _instance;
            }
        }
        private static WeaponUpgradeUI _instance;
        public static bool HasInstance => _instance != null;

        [Header("Root & Controls")]
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Button closeButton;

        [Header("Weapon Panel (Right - UpgradePanel)")]
        [SerializeField] private RectTransform upgradePanelRect;
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private Button weaponPrevButton;
        [SerializeField] private Button weaponNextButton;
        [SerializeField] private Image weaponIconImage;
        [SerializeField] private TMP_Text weaponDescriptionText;

        [Header("Artifact Attach Slots (A1 - A4)")]
        [SerializeField] private Button[] attachSlotButtons = new Button[WeaponArtifactManager.MaxArtifactSlots];
        [SerializeField] private Image[] attachSlotIcons = new Image[WeaponArtifactManager.MaxArtifactSlots];

        [Header("Artifact List Panel (Left - ArtifactPanel / ARTIFACT)")]
        [SerializeField] private TMP_Text artifactTitleText;
        [SerializeField] private Transform artifactSlotsContainer;
        [SerializeField] private List<InventoryItemSlot> artifactSlots = new List<InventoryItemSlot>();
        [SerializeField] private Button artifactPrevButton;
        [SerializeField] private Button artifactNextButton;
        [SerializeField] private TMP_Text artifactPageText;
        [SerializeField] private int defaultSlotsPerPage = 6;

        private PlayerInventory _playerInventory;
        private PlayerController _playerController;

        private readonly List<string> _ownedWeapons = new List<string>();
        private readonly List<InventorySlot> _ownedArtifacts = new List<InventorySlot>();
        private readonly Dictionary<InventoryItemSlot, string> _slotArtifactMap = new Dictionary<InventoryItemSlot, string>();

        private int _selectedWeaponIndex = 0;
        private int _artifactPage = 0;
        private bool _isOpen = false;
        private int _justOpenedFrame = -1;

        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLockState;

        public bool IsOpen => _isOpen;
        public string SelectedWeaponName => (_selectedWeaponIndex >= 0 && _selectedWeaponIndex < _ownedWeapons.Count) ? _ownedWeapons[_selectedWeaponIndex] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHUDInScene()
        {
            WeaponUpgradeUI ui = FindExistingOrBindInScene();
            if (ui != null)
            {
                ui.AutoBindReferences();
                ui.HideImmediate();
            }
        }

        private static WeaponUpgradeUI FindExistingOrBindInScene()
        {
            WeaponUpgradeUI existing = Object.FindFirstObjectByType<WeaponUpgradeUI>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            GameObject hudGo = FindHudGameObject();
            if (hudGo != null)
            {
                return hudGo.GetComponent<WeaponUpgradeUI>() ?? hudGo.AddComponent<WeaponUpgradeUI>();
            }
            return null;
        }

        private static GameObject FindHudGameObject()
        {
            GameObject activeFind = GameObject.Find("WeaponUpgrade_HUD");
            if (activeFind != null) return activeFind;

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null) continue;
                Transform found = canvases[i].transform.Find("WeaponUpgrade_HUD");
                if (found != null) return found.gameObject;

                Transform[] children = canvases[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j] != null && children[j].name == "WeaponUpgrade_HUD")
                    {
                        return children[j].gameObject;
                    }
                }
            }
            return null;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(this);
                return;
            }

            AutoBindReferences();
            HideImmediate();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void HideImmediate()
        {
            _isOpen = false;
            if (hudRoot != null) hudRoot.SetActive(false);
            else gameObject.SetActive(false);
        }

        public void AutoBindReferences()
        {
            if (hudRoot == null) hudRoot = gameObject;
            Transform root = hudRoot.transform;

            // 1. Close / BACK button
            if (closeButton == null)
            {
                Transform closeT = root.Find("Close") ?? root.Find("Back") ?? root.Find("CloseButton");
                if (closeT != null) closeButton = closeT.GetComponent<Button>();
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            // 2. Right Side: UpgradePanel
            Transform upgPanel = root.Find("UpgradePanel");
            if (upgPanel != null)
            {
                if (upgradePanelRect == null) upgradePanelRect = upgPanel as RectTransform;

                Transform top = upgPanel.Find("Top");
                if (top != null)
                {
                    if (weaponNameText == null)
                        weaponNameText = top.Find("Page_Text")?.GetComponent<TMP_Text>();
                    if (weaponPrevButton == null)
                        weaponPrevButton = top.Find("Pre_Button")?.GetComponent<Button>();
                    if (weaponNextButton == null)
                        weaponNextButton = top.Find("Next_Button")?.GetComponent<Button>();
                }

                if (weaponIconImage == null)
                {
                    Transform iconT = upgPanel.Find("WeaponThumbnails/Icon") ?? upgPanel.Find("WeaponThumbnails/icon");
                    if (iconT != null) weaponIconImage = iconT.GetComponent<Image>();
                }

                if (weaponDescriptionText == null)
                {
                    Transform descT = upgPanel.Find("Descriiption") ?? upgPanel.Find("Description");
                    if (descT != null) weaponDescriptionText = descT.GetComponent<TMP_Text>();
                }

                Transform attachPanel = upgPanel.Find("AttachPanel");
                if (attachPanel != null)
                {
                    if (attachSlotButtons == null || attachSlotButtons.Length != WeaponArtifactManager.MaxArtifactSlots)
                        attachSlotButtons = new Button[WeaponArtifactManager.MaxArtifactSlots];
                    if (attachSlotIcons == null || attachSlotIcons.Length != WeaponArtifactManager.MaxArtifactSlots)
                        attachSlotIcons = new Image[WeaponArtifactManager.MaxArtifactSlots];

                    for (int i = 0; i < WeaponArtifactManager.MaxArtifactSlots; i++)
                    {
                        Transform slotT = attachPanel.Find($"A{i + 1}");
                        if (slotT == null && i < attachPanel.childCount) slotT = attachPanel.GetChild(i);
                        if (slotT == null) continue;

                        if (attachSlotButtons[i] == null)
                        {
                            Button btn = slotT.GetComponent<Button>();
                            if (btn == null) btn = slotT.gameObject.AddComponent<Button>();
                            attachSlotButtons[i] = btn;
                        }

                        if (attachSlotIcons[i] == null)
                        {
                            Transform iconChild = slotT.Find("icon") ?? slotT.Find("Icon");
                            if (iconChild != null) attachSlotIcons[i] = iconChild.GetComponent<Image>();
                        }
                    }
                }
            }

            // 3. Left Side: ArtifactPanel / ARTIFACT
            Transform artPanel = root.Find("ArtifactPanel") ?? root.Find("ARTIFACT");
            if (artPanel != null)
            {
                if (artifactTitleText == null)
                {
                    Transform nameT = artPanel.Find("Name/NameText") ?? artPanel.Find("Name");
                    if (nameT != null) artifactTitleText = nameT.GetComponentInChildren<TMP_Text>(true);
                }

                if (artifactSlotsContainer == null)
                {
                    artifactSlotsContainer = artPanel.Find("Scroll View/Viewport/Content")
                                          ?? artPanel.Find("Content");
                }

                if ((artifactSlots == null || artifactSlots.Count == 0) && artifactSlotsContainer != null)
                {
                    artifactSlots = new List<InventoryItemSlot>();
                    for (int i = 0; i < artifactSlotsContainer.childCount; i++)
                    {
                        InventoryItemSlot slot = artifactSlotsContainer.GetChild(i).GetComponent<InventoryItemSlot>();
                        if (slot != null) artifactSlots.Add(slot);
                    }
                    if (artifactSlots.Count == 0)
                    {
                        artifactSlots.AddRange(artifactSlotsContainer.GetComponentsInChildren<InventoryItemSlot>(true));
                    }
                }

                Transform bottom = artPanel.Find("StorageBottom_HUD") ?? artPanel.Find("ArtifactBottom_HUD");
                if (bottom != null)
                {
                    if (artifactPrevButton == null)
                        artifactPrevButton = bottom.Find("Pre_Button")?.GetComponent<Button>();
                    if (artifactNextButton == null)
                        artifactNextButton = bottom.Find("Next_Button")?.GetComponent<Button>();
                    if (artifactPageText == null)
                        artifactPageText = bottom.Find("Page_Text")?.GetComponent<TMP_Text>();
                }
            }

            WireButtons();
        }

        private void WireButtons()
        {
            if (weaponPrevButton != null)
            {
                weaponPrevButton.onClick.RemoveListener(PrevWeapon);
                weaponPrevButton.onClick.AddListener(PrevWeapon);
            }
            if (weaponNextButton != null)
            {
                weaponNextButton.onClick.RemoveListener(NextWeapon);
                weaponNextButton.onClick.AddListener(NextWeapon);
            }
            if (artifactPrevButton != null)
            {
                artifactPrevButton.onClick.RemoveListener(PrevArtifactPage);
                artifactPrevButton.onClick.AddListener(PrevArtifactPage);
            }
            if (artifactNextButton != null)
            {
                artifactNextButton.onClick.RemoveListener(NextArtifactPage);
                artifactNextButton.onClick.AddListener(NextArtifactPage);
            }

            if (attachSlotButtons != null)
            {
                for (int i = 0; i < attachSlotButtons.Length; i++)
                {
                    if (attachSlotButtons[i] == null) continue;
                    int slotIdx = i;
                    attachSlotButtons[i].onClick.RemoveAllListeners();
                    attachSlotButtons[i].onClick.AddListener(() => OnAttachSlotClicked(slotIdx));
                }
            }
        }

        // =========================================================================
        //  Open / Close / Toggle
        // =========================================================================

        public void Open(PlayerInventory explicitInventory = null)
        {
            ResolvePlayerReferences(explicitInventory);
            if (_playerInventory == null)
            {
                Debug.LogWarning("[WeaponUpgradeUI] Cannot open: PlayerInventory not found.");
                return;
            }

            AutoBindReferences();

            _isOpen = true;
            _justOpenedFrame = Time.frameCount;
            _artifactPage = 0;

            if (LootUI.HasInstance)
            {
                LootUI.Instance.HidePrompt();
            }

            if (hudRoot != null) hudRoot.SetActive(true);
            SubscribeEvents();

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }

            _savedCursorVisible = Cursor.visible;
            _savedCursorLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // ถ้าผู้เล่นกำลังถือปืน/อาวุธอยู่ ให้เลือกอาวุธชิ้นนั้นเป็นค่าเริ่มต้น
            RebuildOwnedListsFromInventory();
            SelectCurrentlyEquippedWeaponIfPresent();
            RefreshUI();
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            UnsubscribeEvents();

            if (hudRoot != null) hudRoot.SetActive(false);

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLockState;

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void ClosePopUp()
        {
            if (_isOpen) Close();
        }

        private void Update()
        {
            if (!_isOpen || Time.frameCount <= _justOpenedFrame) return;

            bool escPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;
#endif
            if (escPressed)
            {
                Close();
            }
        }

        // =========================================================================
        //  Scan Weapons & Artifacts From PlayerInventory
        // =========================================================================

        private void RebuildOwnedListsFromInventory()
        {
            string prevSelected = SelectedWeaponName;
            _ownedWeapons.Clear();
            _ownedArtifacts.Clear();

            if (_playerInventory == null || _playerInventory.Slots == null) return;

            var seenWeapons = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _playerInventory.Slots.Count; i++)
            {
                InventorySlot slot = _playerInventory.Slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.itemName) || slot.quantity <= 0) continue;

                if (WeaponArtifactManager.IsWeaponItem(slot.itemName))
                {
                    if (seenWeapons.Add(slot.itemName))
                    {
                        _ownedWeapons.Add(slot.itemName);
                    }
                }
                else if (WeaponArtifactManager.IsArtifactItem(slot.itemName))
                {
                    _ownedArtifacts.Add(slot);
                }
            }

            if (!string.IsNullOrEmpty(prevSelected))
            {
                int foundIdx = _ownedWeapons.FindIndex(w => string.Equals(w, prevSelected, System.StringComparison.OrdinalIgnoreCase));
                _selectedWeaponIndex = foundIdx >= 0 ? foundIdx : 0;
            }
            else
            {
                _selectedWeaponIndex = Mathf.Clamp(_selectedWeaponIndex, 0, Mathf.Max(0, _ownedWeapons.Count - 1));
            }
        }

        private void SelectCurrentlyEquippedWeaponIfPresent()
        {
            if (_playerController == null || _ownedWeapons.Count == 0) return;

            string activeName = !string.IsNullOrEmpty(_playerController.CurrentWeaponName)
                ? _playerController.CurrentWeaponName
                : _playerController.CurrentMeleeWeaponName;

            if (string.IsNullOrEmpty(activeName)) return;

            int idx = _ownedWeapons.FindIndex(w => string.Equals(w, activeName, System.StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _selectedWeaponIndex = idx;
        }

        // =========================================================================
        //  UI Rendering
        // =========================================================================

        public void RefreshUI()
        {
            if (!_isOpen || _playerInventory == null) return;

            RebuildOwnedListsFromInventory();
            RefreshWeaponPanel();
            RefreshArtifactListPanel();
        }

        private void RefreshWeaponPanel()
        {
            string currentWeapon = SelectedWeaponName;
            bool hasWeapon = !string.IsNullOrEmpty(currentWeapon);

            if (weaponNameText != null)
            {
                weaponNameText.text = hasWeapon ? currentWeapon : "NO WEAPON";
            }

            if (weaponPrevButton != null) weaponPrevButton.interactable = _ownedWeapons.Count > 1;
            if (weaponNextButton != null) weaponNextButton.interactable = _ownedWeapons.Count > 1;

            if (weaponIconImage != null)
            {
                Sprite icon = ResolveItemIcon(currentWeapon);
                weaponIconImage.sprite = icon;
                weaponIconImage.enabled = icon != null;
            }

            if (weaponDescriptionText != null)
            {
                weaponDescriptionText.text = hasWeapon
                    ? WeaponArtifactManager.BuildWeaponSummaryText(currentWeapon)
                    : "No weapons found in your inventory.";
            }

            // อัปเดตช่อง Artifact A1 - A4
            string[] equippedSlots = hasWeapon
                ? WeaponArtifactManager.GetEquippedSlots(currentWeapon)
                : new string[WeaponArtifactManager.MaxArtifactSlots];

            for (int i = 0; i < WeaponArtifactManager.MaxArtifactSlots; i++)
            {
                string artName = (equippedSlots != null && i < equippedSlots.Length) ? equippedSlots[i] : null;
                bool occupied = !string.IsNullOrEmpty(artName);

                if (attachSlotIcons != null && i < attachSlotIcons.Length && attachSlotIcons[i] != null)
                {
                    Sprite artIcon = occupied ? ResolveItemIcon(artName) : null;
                    attachSlotIcons[i].sprite = artIcon;
                    attachSlotIcons[i].enabled = occupied && artIcon != null;
                    attachSlotIcons[i].color = occupied ? Color.white : new Color(1f, 1f, 1f, 0f);
                }

                if (attachSlotButtons != null && i < attachSlotButtons.Length && attachSlotButtons[i] != null)
                {
                    attachSlotButtons[i].interactable = hasWeapon && occupied;
                }
            }
        }

        private void RefreshArtifactListPanel()
        {
            string currentWeapon = SelectedWeaponName;

            // กรอง Artifact ที่เข้ากันได้กับอาวุธที่เลือกอยู่ขึ้นมาแสดงก่อน (และแสดงเฉพาะที่เข้ากันได้ถ้ามีอาวุธเลือกอยู่)
            List<InventorySlot> displayList = new List<InventorySlot>();
            for (int i = 0; i < _ownedArtifacts.Count; i++)
            {
                InventorySlot s = _ownedArtifacts[i];
                if (s == null || string.IsNullOrEmpty(s.itemName) || s.quantity <= 0) continue;
                if (string.IsNullOrEmpty(currentWeapon) || WeaponArtifactManager.IsArtifactCompatibleWithWeapon(s.itemName, currentWeapon))
                {
                    displayList.Add(s);
                }
            }

            if (artifactTitleText != null)
            {
                artifactTitleText.text = "ARTIFACT";
            }

            int perPage = (artifactSlots != null && artifactSlots.Count > 0) ? artifactSlots.Count : Mathf.Max(1, defaultSlotsPerPage);
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)displayList.Count / perPage));
            _artifactPage = Mathf.Clamp(_artifactPage, 0, totalPages - 1);

            _slotArtifactMap.Clear();
            int startIdx = _artifactPage * perPage;

            for (int i = 0; i < artifactSlots.Count; i++)
            {
                InventoryItemSlot uiSlot = artifactSlots[i];
                if (uiSlot == null) continue;

                int dataIdx = startIdx + i;
                if (dataIdx < displayList.Count)
                {
                    InventorySlot artSlot = displayList[dataIdx];
                    string artName = artSlot.itemName;
                    Sprite icon = ResolveItemIcon(artName);
                    bool canEquip = !string.IsNullOrEmpty(currentWeapon) &&
                                    WeaponArtifactManager.GetFirstEmptySlotIndex(currentWeapon) >= 0;

                    _slotArtifactMap[uiSlot] = artName;
                    uiSlot.gameObject.SetActive(true);
                    uiSlot.SetData(
                        itemName: artName,
                        quantity: artSlot.quantity,
                        icon: icon,
                        isEquipped: false,
                        onClickUse: canEquip ? (_) => EquipArtifactToSelectedWeapon(artName) : null,
                        customButtonText: "EQUIP"
                    );
                }
                else
                {
                    uiSlot.Clear();
                    uiSlot.gameObject.SetActive(false);
                }
            }

            if (artifactPageText != null) artifactPageText.text = $"{_artifactPage + 1}/{totalPages}";
            if (artifactPrevButton != null) artifactPrevButton.interactable = _artifactPage > 0;
            if (artifactNextButton != null) artifactNextButton.interactable = _artifactPage < totalPages - 1;
        }

        // =========================================================================
        //  Actions & Drag-Drop Support
        // =========================================================================

        public void PrevWeapon()
        {
            if (_ownedWeapons.Count <= 1) return;
            _selectedWeaponIndex = (_selectedWeaponIndex - 1 + _ownedWeapons.Count) % _ownedWeapons.Count;
            _artifactPage = 0;
            RefreshUI();
        }

        public void NextWeapon()
        {
            if (_ownedWeapons.Count <= 1) return;
            _selectedWeaponIndex = (_selectedWeaponIndex + 1) % _ownedWeapons.Count;
            _artifactPage = 0;
            RefreshUI();
        }

        public void PrevArtifactPage()
        {
            if (_artifactPage > 0)
            {
                _artifactPage--;
                RefreshArtifactListPanel();
            }
        }

        public void NextArtifactPage()
        {
            _artifactPage++;
            RefreshArtifactListPanel();
        }

        private void EquipArtifactToSelectedWeapon(string artifactItemName, int targetSlotIndex = -1)
        {
            string weaponName = SelectedWeaponName;
            if (string.IsNullOrEmpty(weaponName) || _playerInventory == null) return;

            if (!WeaponArtifactManager.EquipArtifact(weaponName, artifactItemName, _playerInventory, targetSlotIndex, out string failReason))
            {
                if (!string.IsNullOrEmpty(failReason)) ShowFeedback(failReason);
                return;
            }

            RefreshUI();
        }

        private void OnAttachSlotClicked(int slotIndex)
        {
            string weaponName = SelectedWeaponName;
            if (string.IsNullOrEmpty(weaponName) || _playerInventory == null) return;

            if (!WeaponArtifactManager.UnequipArtifact(weaponName, slotIndex, _playerInventory, out string failReason))
            {
                if (!string.IsNullOrEmpty(failReason)) ShowFeedback(failReason);
                return;
            }

            RefreshUI();
        }

        public bool IsSlotOwnedByWeaponUpgradeUI(InventoryItemSlot slot)
        {
            if (slot == null) return false;
            if (_slotArtifactMap.ContainsKey(slot)) return true;
            if (artifactSlots != null && artifactSlots.Contains(slot)) return true;
            return artifactSlotsContainer != null && slot.transform.IsChildOf(artifactSlotsContainer);
        }

        public bool TryGetDragPreview(InventoryItemSlot sourceSlot, Vector2 screenPos, Camera uiCam, out string badgeText, out Color badgeColor)
        {
            badgeText = string.Empty;
            badgeColor = Color.white;
            if (!_isOpen || sourceSlot == null || string.IsNullOrEmpty(SelectedWeaponName)) return false;
            if (!IsSlotOwnedByWeaponUpgradeUI(sourceSlot)) return false;
            if (upgradePanelRect == null || !RectTransformUtility.RectangleContainsScreenPoint(upgradePanelRect, screenPos, uiCam)) return false;

            badgeText = "EQUIP ARTIFACT";
            badgeColor = new Color(0.82f, 0.45f, 1f, 1f);
            return true;
        }

        public bool TryHandleArtifactDrop(InventoryItemSlot sourceSlot, Vector2 screenPos, Camera uiCam)
        {
            if (!_isOpen || sourceSlot == null || string.IsNullOrEmpty(SelectedWeaponName)) return false;
            if (!_slotArtifactMap.TryGetValue(sourceSlot, out string artName)) return false;
            if (upgradePanelRect == null || !RectTransformUtility.RectangleContainsScreenPoint(upgradePanelRect, screenPos, uiCam)) return false;

            // เช็กว่าปล่อยทับช่อง A1..A4 ช่องไหนโดยตรงหรือเปล่า
            int targetSlot = -1;
            if (attachSlotButtons != null)
            {
                for (int i = 0; i < attachSlotButtons.Length; i++)
                {
                    if (attachSlotButtons[i] == null) continue;
                    RectTransform slotRt = attachSlotButtons[i].GetComponent<RectTransform>();
                    if (slotRt != null && RectTransformUtility.RectangleContainsScreenPoint(slotRt, screenPos, uiCam))
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            EquipArtifactToSelectedWeapon(artName, targetSlot);
            return true;
        }

        // =========================================================================
        //  Helpers
        // =========================================================================

        private void ResolvePlayerReferences(PlayerInventory explicitInventory)
        {
            _playerController = TheLastEmpire.Network.PlayerRegistry.LocalPlayer
                             ?? PlayerController.Instance
                             ?? Object.FindFirstObjectByType<PlayerController>();

            if (explicitInventory != null)
            {
                _playerInventory = explicitInventory;
            }
            else if (_playerController != null)
            {
                _playerInventory = _playerController.GetComponent<PlayerInventory>();
            }

            if (_playerInventory == null)
            {
                _playerInventory = PlayerInventory.Instance ?? Object.FindFirstObjectByType<PlayerInventory>();
            }
        }

        private void SubscribeEvents()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= HandleStateChanged;
                _playerInventory.OnInventoryChanged += HandleStateChanged;
            }
            WeaponArtifactManager.OnWeaponArtifactsChanged -= HandleWeaponArtifactsChanged;
            WeaponArtifactManager.OnWeaponArtifactsChanged += HandleWeaponArtifactsChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= HandleStateChanged;
            }
            WeaponArtifactManager.OnWeaponArtifactsChanged -= HandleWeaponArtifactsChanged;
        }

        private void HandleStateChanged()
        {
            if (_isOpen) RefreshUI();
        }

        private void HandleWeaponArtifactsChanged(string weaponName)
        {
            if (_isOpen) RefreshUI();
        }

        private static Sprite ResolveItemIcon(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || ItemDatabase.Instance == null) return null;
            ItemData data = ItemDatabase.Instance.GetItemByName(itemName);
            return data != null ? data.icon : null;
        }

        private void ShowFeedback(string message)
        {
            if (_playerController != null && DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    _playerController.transform.position + Vector3.up * 2.0f,
                    0f,
                    false,
                    message
                );
            }
        }
    }
}
