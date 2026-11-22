using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// StorageUI — ควบคุมหน้าต่างคลังเก็บของแบบ 2 ฝั่ง (ซ้าย: Inventory ผู้เล่น / ขวา: Storage กล่องเก็บของ)
    /// รองรับ:
    /// - ฝากของ (DEPOSIT) จาก Inventory -> Storage และถอนของ (WITHDRAW) จาก Storage -> Inventory
    /// - คลิกปกติย้ายทั้งกองในช่อง / กด Shift ค้าง + คลิกเพื่อย้ายทีละ 1 ชิ้น
    /// - รวม Stack อัตโนมัติตาม MaxStackSize และตรวจความจุช่องทั้งสองฝั่ง
    /// - แบ่งหน้า (Pagination) แยกอิสระทั้งฝั่ง Inventory และฝั่ง Storage (ใช้แถว InventoryItemSlot เดิมใน Scene)
    /// - ซิงค์สถานะกล่องผ่าน NetworkLootManager ในโหมด Co-op
    /// - Auto-Bind อ้างอิงทุกชิ้นจากโครงสร้าง Storage_HUD อัตโนมัติ (และปรับแก้ได้ผ่าน Inspector)
    /// </summary>
    public class StorageUI : MonoBehaviour, IPopUp
    {
        public static StorageUI Instance
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
        private static StorageUI _instance;
        public static bool HasInstance => _instance != null;

        [Header("Root & Controls")]
        [Tooltip("Root GameObject ของ Storage HUD (Storage_HUD)")]
        [SerializeField] private GameObject hudRoot;
        [Tooltip("Panel หลัก (Storage_Panel)")]
        [SerializeField] private GameObject panelObject;
        [Tooltip("ปุ่ม BACK / Close สีแดงด้านซ้ายบน")]
        [SerializeField] private Button closeButton;

        [Header("Inventory Side (Left)")]
        [SerializeField] private TMP_Text inventoryTitleText;
        [SerializeField] private Transform inventorySlotsContainer;
        [SerializeField] private List<InventoryItemSlot> inventorySlots = new List<InventoryItemSlot>();
        [SerializeField] private Button inventoryPrevButton;
        [SerializeField] private Button inventoryNextButton;
        [SerializeField] private TMP_Text inventoryPageText;

        [Header("Storage Side (Right)")]
        [SerializeField] private TMP_Text storageTitleText;
        [SerializeField] private Transform storageSlotsContainer;
        [SerializeField] private List<InventoryItemSlot> storageSlots = new List<InventoryItemSlot>();
        [SerializeField] private Button storagePrevButton;
        [SerializeField] private Button storageNextButton;
        [SerializeField] private TMP_Text storagePageText;

        [Header("Pagination Settings")]
        [SerializeField] private int defaultSlotsPerPage = 7;

        private LootContainer _currentContainer;
        private StorageCrateBuilding _currentCrate;
        private PlayerInventory _playerInventory;
        private PlayerController _playerController;

        private int _inventoryPage = 0;
        private int _storagePage = 0;
        private bool _isOpen = false;
        private int _justOpenedFrame = -1;

        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLockState;

        public bool IsOpen => _isOpen;
        public LootContainer CurrentContainer => _currentContainer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHUDInScene()
        {
            StorageUI ui = FindExistingOrBindInScene();
            if (ui != null)
            {
                ui.AutoBindReferences();
                ui.HideImmediate();
            }
        }

        private static StorageUI FindExistingOrBindInScene()
        {
            // ค้นหาแม้ GameObject จะปิด (Inactive) อยู่
            StorageUI existing = Object.FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            GameObject hudGo = FindStorageHudGameObject();
            if (hudGo != null)
            {
                StorageUI attached = hudGo.GetComponent<StorageUI>() ?? hudGo.AddComponent<StorageUI>();
                return attached;
            }
            return null;
        }

        private static GameObject FindStorageHudGameObject()
        {
            GameObject activeFind = GameObject.Find("Storage_HUD");
            if (activeFind != null) return activeFind;

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null) continue;
                Transform found = canvases[i].transform.Find("Storage_HUD");
                if (found != null) return found.gameObject;

                Transform[] allChildren = canvases[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < allChildren.Length; j++)
                {
                    if (allChildren[j] != null && allChildren[j].name == "Storage_HUD")
                    {
                        return allChildren[j].gameObject;
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
            UnsubscribeInventoryEvents();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void HideImmediate()
        {
            _isOpen = false;
            if (panelObject != null && panelObject != gameObject)
            {
                panelObject.SetActive(false);
            }
            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// เชื่อมต่อ UI References อัตโนมัติจาก Hierarchy ของ Storage_HUD (หากยังไม่ได้ลากใส่ใน Inspector)
        /// </summary>
        public void AutoBindReferences()
        {
            if (hudRoot == null) hudRoot = gameObject;

            if (panelObject == null)
            {
                Transform panelT = transform.Find("Storage_Panel");
                panelObject = panelT != null ? panelT.gameObject : gameObject;
            }

            Transform root = panelObject != null ? panelObject.transform : transform;

            // 1. ปุ่ม Close / BACK
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

            // 2. ฝั่งซ้าย: Inventory
            Transform invPanel = root.Find("Inventory") ?? root.Find("Inventory_Panel");
            if (invPanel != null)
            {
                if (inventoryTitleText == null)
                {
                    Transform nameT = invPanel.Find("Name/NameText") ?? invPanel.Find("Name");
                    if (nameT != null) inventoryTitleText = nameT.GetComponentInChildren<TMP_Text>(true);
                }

                if (inventorySlotsContainer == null)
                {
                    inventorySlotsContainer = invPanel.Find("Scroll View/Viewport/Content")
                                           ?? invPanel.Find("Content");
                }

                if ((inventorySlots == null || inventorySlots.Count == 0) && inventorySlotsContainer != null)
                {
                    inventorySlots = CollectDirectSlots(inventorySlotsContainer);
                }

                Transform invBottom = invPanel.Find("StorageBottom_HUD") ?? invPanel.Find("InventoryBottom_HUD");
                if (invBottom != null)
                {
                    if (inventoryPrevButton == null)
                        inventoryPrevButton = invBottom.Find("Pre_Button")?.GetComponent<Button>();
                    if (inventoryNextButton == null)
                        inventoryNextButton = invBottom.Find("Next_Button")?.GetComponent<Button>();
                    if (inventoryPageText == null)
                        inventoryPageText = invBottom.Find("Page_Text")?.GetComponent<TMP_Text>();
                }
            }

            // 3. ฝั่งขวา: Storage
            Transform storPanel = root.Find("Storage") ?? root.Find("Storage_SubPanel");
            if (storPanel != null)
            {
                if (storageTitleText == null)
                {
                    Transform nameT = storPanel.Find("Name/NameText") ?? storPanel.Find("Name");
                    if (nameT != null) storageTitleText = nameT.GetComponentInChildren<TMP_Text>(true);
                }

                if (storageSlotsContainer == null)
                {
                    storageSlotsContainer = storPanel.Find("Scroll View/Viewport/Content")
                                         ?? storPanel.Find("Content");
                }

                if ((storageSlots == null || storageSlots.Count == 0) && storageSlotsContainer != null)
                {
                    storageSlots = CollectDirectSlots(storageSlotsContainer);
                }

                Transform storBottom = storPanel.Find("StorageBottom_HUD");
                if (storBottom != null)
                {
                    if (storagePrevButton == null)
                        storagePrevButton = storBottom.Find("Pre_Button")?.GetComponent<Button>();
                    if (storageNextButton == null)
                        storageNextButton = storBottom.Find("Next_Button")?.GetComponent<Button>();
                    if (storagePageText == null)
                        storagePageText = storBottom.Find("Page_Text")?.GetComponent<TMP_Text>();
                }
            }

            WirePaginationButtons();
        }

        private static List<InventoryItemSlot> CollectDirectSlots(Transform container)
        {
            var list = new List<InventoryItemSlot>();
            if (container == null) return list;

            for (int i = 0; i < container.childCount; i++)
            {
                InventoryItemSlot slot = container.GetChild(i).GetComponent<InventoryItemSlot>();
                if (slot != null) list.Add(slot);
            }
            if (list.Count == 0)
            {
                list.AddRange(container.GetComponentsInChildren<InventoryItemSlot>(true));
            }
            return list;
        }

        private void WirePaginationButtons()
        {
            if (inventoryPrevButton != null)
            {
                inventoryPrevButton.onClick.RemoveListener(PrevInventoryPage);
                inventoryPrevButton.onClick.AddListener(PrevInventoryPage);
            }
            if (inventoryNextButton != null)
            {
                inventoryNextButton.onClick.RemoveListener(NextInventoryPage);
                inventoryNextButton.onClick.AddListener(NextInventoryPage);
            }
            if (storagePrevButton != null)
            {
                storagePrevButton.onClick.RemoveListener(PrevStoragePage);
                storagePrevButton.onClick.AddListener(PrevStoragePage);
            }
            if (storageNextButton != null)
            {
                storageNextButton.onClick.RemoveListener(NextStoragePage);
                storageNextButton.onClick.AddListener(NextStoragePage);
            }
        }

        // =========================================================================
        //  Open / Close
        // =========================================================================

        public void Open(LootContainer container, PlayerInventory inventory = null, StorageCrateBuilding crate = null)
        {
            if (container == null) return;

            ResolvePlayerReferences(inventory);
            if (_playerInventory == null)
            {
                Debug.LogWarning("[StorageUI] Cannot open StorageUI: PlayerInventory not found.");
                return;
            }

            AutoBindReferences();

            _currentContainer = container;
            _currentCrate = crate != null ? crate : container.GetComponent<StorageCrateBuilding>();
            _inventoryPage = 0;
            _storagePage = 0;
            _isOpen = true;
            _justOpenedFrame = Time.frameCount;

            if (LootUI.HasInstance)
            {
                LootUI.Instance.HidePrompt();
            }

            if (hudRoot != null) hudRoot.SetActive(true);
            if (panelObject != null) panelObject.SetActive(true);

            SubscribeInventoryEvents();

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }

            _savedCursorVisible = Cursor.visible;
            _savedCursorLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            RefreshUI();
            Debug.Log($"[StorageUI] Opened storage '{container.containerName}' ({container.UsedSlots}/{container.maxStorageSlots} slots).");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            UnsubscribeInventoryEvents();

            if (panelObject != null && panelObject != hudRoot)
            {
                panelObject.SetActive(false);
            }
            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLockState;

            _currentContainer = null;
            _currentCrate = null;

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }

            Debug.Log("[StorageUI] Closed.");
        }

        public void ClosePopUp()
        {
            if (_isOpen) Close();
        }

        private void Update()
        {
            if (!_isOpen) return;

            // 1. ปิดถ้ากล่องถูกทำลายไปแล้ว
            if (_currentContainer == null || !_currentContainer.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            // 2. กด E หรือ Escape เพื่อปิด (ข้ามเฟรมเดียวกับที่เพิ่งกด E เปิด)
            if (Time.frameCount > _justOpenedFrame)
            {
                bool closePressed = false;
                if (Keyboard.current != null &&
                    (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    closePressed = true;
                }
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                {
                    closePressed = true;
                }
#endif
                if (closePressed)
                {
                    Close();
                    return;
                }
            }

            // 3. ปิดอัตโนมัติถ้าผู้เล่นเดินห่างออกจากกล่อง
            if (_playerController != null)
            {
                float maxDist = (_currentCrate != null ? _currentCrate.InteractionDistance : _currentContainer.interactionRadius);
                if (maxDist <= 0f) maxDist = 2.5f;
                float dist = Vector3.Distance(_playerController.transform.position, _currentContainer.transform.position);
                if (dist > maxDist + 1.5f)
                {
                    Close();
                }
            }
        }

        // =========================================================================
        //  UI Rendering & Pagination
        // =========================================================================

        public void RefreshUI()
        {
            if (!_isOpen || _currentContainer == null || _playerInventory == null) return;

            RefreshInventorySide();
            RefreshStorageSide();
        }

        private void RefreshInventorySide()
        {
            List<InventorySlot> invList = _playerInventory.Slots;
            int totalItems = invList != null ? invList.Count : 0;
            int perPage = GetPageCapacity(inventorySlots);
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / perPage));
            _inventoryPage = Mathf.Clamp(_inventoryPage, 0, totalPages - 1);

            if (inventoryTitleText != null)
            {
                inventoryTitleText.text = $"INVENTORY ({totalItems}/{_playerInventory.MaxSlots})";
            }

            int startIdx = _inventoryPage * perPage;
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                InventoryItemSlot uiSlot = inventorySlots[i];
                if (uiSlot == null) continue;

                int dataIdx = startIdx + i;
                if (invList != null && dataIdx < totalItems && invList[dataIdx] != null &&
                    !string.IsNullOrEmpty(invList[dataIdx].itemName) && invList[dataIdx].quantity > 0)
                {
                    InventorySlot slotData = invList[dataIdx];
                    string itemName = slotData.itemName;
                    int qty = slotData.quantity;
                    int capturedIndex = dataIdx;

                    Sprite icon = ResolveItemIcon(itemName);
                    bool isEquipped = IsItemEquippedByPlayer(itemName);

                    uiSlot.gameObject.SetActive(true);
                    uiSlot.SetData(
                        itemName: itemName,
                        quantity: qty,
                        icon: icon,
                        isEquipped: isEquipped,
                        onClickUse: (_) => DepositFromInventorySlot(capturedIndex),
                        customButtonText: "DEPOSIT"
                    );
                }
                else
                {
                    uiSlot.Clear();
                    uiSlot.gameObject.SetActive(false);
                }
            }

            UpdatePaginationControls(inventoryPageText, inventoryPrevButton, inventoryNextButton, _inventoryPage, totalPages);
        }

        private void RefreshStorageSide()
        {
            List<LootSlot> storList = _currentContainer.itemsInside;
            int totalItems = storList != null ? storList.Count : 0;
            int perPage = GetPageCapacity(storageSlots);
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / perPage));
            _storagePage = Mathf.Clamp(_storagePage, 0, totalPages - 1);

            if (storageTitleText != null)
            {
                storageTitleText.text = $"STORAGE ({totalItems}/{_currentContainer.maxStorageSlots})";
            }

            int startIdx = _storagePage * perPage;
            for (int i = 0; i < storageSlots.Count; i++)
            {
                InventoryItemSlot uiSlot = storageSlots[i];
                if (uiSlot == null) continue;

                int dataIdx = startIdx + i;
                if (storList != null && dataIdx < totalItems &&
                    !string.IsNullOrEmpty(storList[dataIdx].itemName) && storList[dataIdx].quantity > 0)
                {
                    LootSlot slotData = storList[dataIdx];
                    string itemName = slotData.itemName;
                    int qty = slotData.quantity;
                    int capturedIndex = dataIdx;

                    Sprite icon = ResolveItemIcon(itemName);

                    uiSlot.gameObject.SetActive(true);
                    uiSlot.SetData(
                        itemName: itemName,
                        quantity: qty,
                        icon: icon,
                        isEquipped: false,
                        onClickUse: (_) => WithdrawFromStorageSlot(capturedIndex),
                        customButtonText: "WITHDRAW"
                    );
                }
                else
                {
                    uiSlot.Clear();
                    uiSlot.gameObject.SetActive(false);
                }
            }

            UpdatePaginationControls(storagePageText, storagePrevButton, storageNextButton, _storagePage, totalPages);
        }

        private int GetPageCapacity(List<InventoryItemSlot> slots)
        {
            if (slots != null && slots.Count > 0) return slots.Count;
            return Mathf.Max(1, defaultSlotsPerPage);
        }

        private static void UpdatePaginationControls(TMP_Text pageText, Button prevBtn, Button nextBtn, int currentPage, int totalPages)
        {
            if (pageText != null)
            {
                pageText.text = $"{currentPage + 1}/{totalPages}";
            }
            if (prevBtn != null)
            {
                prevBtn.interactable = currentPage > 0;
            }
            if (nextBtn != null)
            {
                nextBtn.interactable = currentPage < totalPages - 1;
            }
        }

        public void PrevInventoryPage()
        {
            if (_inventoryPage > 0)
            {
                _inventoryPage--;
                RefreshInventorySide();
            }
        }

        public void NextInventoryPage()
        {
            int perPage = GetPageCapacity(inventorySlots);
            int totalItems = _playerInventory != null && _playerInventory.Slots != null ? _playerInventory.Slots.Count : 0;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / perPage));
            if (_inventoryPage < totalPages - 1)
            {
                _inventoryPage++;
                RefreshInventorySide();
            }
        }

        public void PrevStoragePage()
        {
            if (_storagePage > 0)
            {
                _storagePage--;
                RefreshStorageSide();
            }
        }

        public void NextStoragePage()
        {
            int perPage = GetPageCapacity(storageSlots);
            int totalItems = _currentContainer != null && _currentContainer.itemsInside != null ? _currentContainer.itemsInside.Count : 0;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / perPage));
            if (_storagePage < totalPages - 1)
            {
                _storagePage++;
                RefreshStorageSide();
            }
        }

        // =========================================================================
        //  Deposit (Inventory -> Storage) & Withdraw (Storage -> Inventory)
        // =========================================================================

        private static bool IsShiftHeld()
        {
            if (Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
            {
                return true;
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                return true;
            }
#endif
            return false;
        }

        private void DepositFromInventorySlot(int slotIndex)
        {
            if (_currentContainer == null || _playerInventory == null) return;
            if (_playerInventory.Slots == null || slotIndex < 0 || slotIndex >= _playerInventory.Slots.Count) return;

            InventorySlot slot = _playerInventory.Slots[slotIndex];
            if (slot == null || string.IsNullOrEmpty(slot.itemName) || slot.quantity <= 0) return;

            string itemName = slot.itemName;
            int desiredQty = IsShiftHeld() ? 1 : slot.quantity;

            // ตรวจสอบที่ว่างในคลัง (สามารถฝากบางส่วนได้ถ้าคลังใกล้เต็ม)
            int space = _currentContainer.GetSpaceForItem(itemName);
            if (space <= 0)
            {
                ShowFeedbackText("STORAGE FULL!");
                Debug.LogWarning($"[StorageUI] Cannot deposit '{itemName}': Storage is full ({_currentContainer.UsedSlots}/{_currentContainer.maxStorageSlots}).");
                return;
            }

            int moveQty = Mathf.Min(desiredQty, space);
            if (moveQty <= 0) return;

            if (!_playerInventory.RemoveItem(itemName, moveQty)) return;

            int added = _currentContainer.AddItem(itemName, moveQty);
            if (added < moveQty)
            {
                // คืนส่วนที่ใส่ไม่ลงกลับกระเป๋า (safety rollback)
                bool prevSuppress = _playerInventory.SuppressNotificationEvents;
                _playerInventory.SuppressNotificationEvents = true;
                _playerInventory.AddItem(itemName, moveQty - added);
                _playerInventory.SuppressNotificationEvents = prevSuppress;
            }

            // หากฝากอาวุธที่กำลังถืออยู่จนไม่เหลือในกระเป๋า ให้ปลดอาวุธออกจากมืออัตโนมัติ
            UnequipIfRemovedFromInventory(itemName);

            TheLastEmpire.Network.NetworkLootManager.Instance?.BroadcastContainerState(_currentContainer);
            RefreshUI();
        }

        private void WithdrawFromStorageSlot(int slotIndex)
        {
            if (_currentContainer == null || _playerInventory == null) return;
            if (_currentContainer.itemsInside == null || slotIndex < 0 || slotIndex >= _currentContainer.itemsInside.Count) return;

            LootSlot slot = _currentContainer.itemsInside[slotIndex];
            if (string.IsNullOrEmpty(slot.itemName) || slot.quantity <= 0) return;

            string itemName = slot.itemName;
            int desiredQty = IsShiftHeld() ? 1 : slot.quantity;

            // คำนวณจำนวนที่ใส่ลงกระเป๋าผู้เล่นได้จริง
            int moveQty = CalculateInventoryFitQuantity(itemName, desiredQty);
            if (moveQty <= 0)
            {
                ShowFeedbackText("INVENTORY FULL!");
                Debug.LogWarning($"[StorageUI] Cannot withdraw '{itemName}': Player inventory is full ({_playerInventory.UsedSlots}/{_playerInventory.MaxSlots}).");
                return;
            }

            bool prevSuppress = _playerInventory.SuppressNotificationEvents;
            _playerInventory.SuppressNotificationEvents = true;
            bool addedToInv = _playerInventory.AddItem(itemName, moveQty);
            _playerInventory.SuppressNotificationEvents = prevSuppress;

            if (!addedToInv) return;

            _currentContainer.RemoveSlotAt(slotIndex, moveQty);
            TheLastEmpire.Network.NetworkLootManager.Instance?.BroadcastContainerState(_currentContainer);
            RefreshUI();
        }

        private int CalculateInventoryFitQuantity(string itemName, int desiredQty)
        {
            if (_playerInventory == null || desiredQty <= 0) return 0;

            string resolved = _playerInventory.ResolveItemName(itemName);
            int maxStack = PlayerInventory.GetMaxStackSize(resolved);
            int freeSlots = Mathf.Max(0, _playerInventory.MaxSlots - _playerInventory.UsedSlots);

            if (maxStack <= 1)
            {
                return Mathf.Min(desiredQty, freeSlots);
            }

            int totalSpace = freeSlots * maxStack;
            string clean = resolved.Trim().ToLowerInvariant();
            if (_playerInventory.Slots != null)
            {
                for (int i = 0; i < _playerInventory.Slots.Count; i++)
                {
                    InventorySlot s = _playerInventory.Slots[i];
                    if (s != null && !string.IsNullOrEmpty(s.itemName) &&
                        s.itemName.Trim().ToLowerInvariant() == clean)
                    {
                        totalSpace += Mathf.Max(0, maxStack - s.quantity);
                    }
                }
            }

            return Mathf.Min(desiredQty, totalSpace);
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

        private void SubscribeInventoryEvents()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= HandleInventoryChanged;
                _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }
        }

        private void UnsubscribeInventoryEvents()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            if (_isOpen)
            {
                RefreshUI();
            }
        }

        private Sprite ResolveItemIcon(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || ItemDatabase.Instance == null) return null;
            ItemData data = ItemDatabase.Instance.GetItemByName(itemName);
            return data != null ? data.icon : null;
        }

        private bool IsItemEquippedByPlayer(string itemName)
        {
            if (_playerController == null || string.IsNullOrEmpty(itemName)) return false;

            string clean = itemName.Trim().ToLowerInvariant();
            if (_playerController.CurrentWeapon != null && !string.IsNullOrEmpty(_playerController.CurrentWeaponName))
            {
                string cur = _playerController.CurrentWeaponName.Trim().ToLowerInvariant();
                if (cur == clean || cur.Contains(clean) || clean.Contains(cur)) return true;
            }
            if (_playerController.CurrentMeleeWeapon != null && !string.IsNullOrEmpty(_playerController.CurrentMeleeWeaponName))
            {
                string cur = _playerController.CurrentMeleeWeaponName.Trim().ToLowerInvariant();
                if (cur == clean || cur.Contains(clean) || clean.Contains(cur)) return true;
            }
            return false;
        }

        private void UnequipIfRemovedFromInventory(string itemName)
        {
            if (_playerController == null || _playerInventory == null || string.IsNullOrEmpty(itemName)) return;
            if (_playerInventory.GetItemCount(itemName) > 0) return;

            string clean = itemName.Trim().ToLowerInvariant();
            if (_playerController.CurrentWeapon != null && !string.IsNullOrEmpty(_playerController.CurrentWeaponName))
            {
                string cur = _playerController.CurrentWeaponName.Trim().ToLowerInvariant();
                if (cur == clean || cur.Contains(clean) || clean.Contains(cur))
                {
                    _playerController.SwitchToWeapon(-1);
                }
            }
            if (_playerController.CurrentMeleeWeapon != null && !string.IsNullOrEmpty(_playerController.CurrentMeleeWeaponName))
            {
                string cur = _playerController.CurrentMeleeWeaponName.Trim().ToLowerInvariant();
                if (cur == clean || cur.Contains(clean) || clean.Contains(cur))
                {
                    _playerController.SwitchToMeleeWeapon(-1);
                }
            }
        }

        private void ShowFeedbackText(string message)
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
