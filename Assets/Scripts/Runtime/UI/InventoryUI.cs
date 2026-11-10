using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheLastEmpire
{
    public class InventoryUI : MonoBehaviour, IPopUp
    {
        public static InventoryUI Instance => _instance;
        private static InventoryUI _instance;

        [Header("UI Customization")]
        [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.94f);
        [SerializeField] private Color accentColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        [Header("External References")]
        [Tooltip("Reference ไปยัง PlayerController ใน Scene (ตั้งค่าผ่าน Inspector หรือ Baker)")]
        [SerializeField] private PlayerController player;

        [Tooltip("Reference ไปยัง PlayerInventory ใน Scene (ตั้งค่าผ่าน Inspector หรือ Baker)")]
        [SerializeField] private PlayerInventory playerInventory;

        [Tooltip("Reference ไปยัง QuickSlotController ใน Scene (ตั้งค่าผ่าน Inspector หรือ Baker)")]
        [SerializeField] private QuickSlotController quickSlotController;
        public QuickSlotController QuickSlot => quickSlotController != null ? quickSlotController : QuickSlotController.Instance;

        [Header("UI References (Optional)")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private TMPro.TMP_Text moneyTextHUD;
        [SerializeField] private TMPro.TMP_Text healthTextHUD;
        [Tooltip("Content GameObject — วาง InventoryItemSlot ลูกไว้ข้างในได้เลย")]
        [SerializeField] private GameObject itemSlotsContainer;

        [Header("Item Slots (รายการช่องกระเป๋า)")]
        [Tooltip("ลากแถว InventoryItemSlot ทั้งหมดที่วางอยู่ในหน้าต่างกระเป๋าใส่ที่นี่ (เช่น 7 ช่อง) เพื่อวนเซตข้อมูลซ้ำ ไม่ Instantiate ใหม่")]
        [SerializeField] private List<InventoryItemSlot> itemSlots = new List<InventoryItemSlot>();

        [Header("Back / Close Button")]
        [Tooltip("ปุ่ม Back สำหรับปิดหน้าต่างกระเป๋า (Optional: หากเว้นว่างระบบจะค้นหาปุ่มชื่อ Back หรือ Close ในแผงให้อัตโนมัติ)")]
        [UnityEngine.Serialization.FormerlySerializedAs("closeButton")]
        [SerializeField] private UnityEngine.UI.Button backButton;

        [Header("Pagination")]
        [Tooltip("จำนวน Slot ต่อหน้า (ถ้าลาก itemSlots ไว้จะอิงตามจำนวนช่องใน List อัตโนมัติ)")]
        [SerializeField] private int slotsPerPage = 7;
        [Tooltip("ปุ่มไปหน้าก่อนหน้า")]
        [SerializeField] private UnityEngine.UI.Button prevPageButton;
        [Tooltip("ปุ่มไปหน้าถัดไป")]
        [SerializeField] private UnityEngine.UI.Button nextPageButton;
        [Tooltip("TMP_Text แสดง '1 / 3'")]
        [SerializeField] private TMPro.TMP_Text pageIndicatorText;

        // ===== Pagination State =====
        private int _currentPage = 0;   // 0-indexed
        private int _totalPages  = 1;



        private GameObject _canvasObject;
        private GameObject _panelObject;
        private TMP_Text _moneyText;
        private TMP_Text _healthText;
        private GameObject _itemContainer;
        private PlayerInventory _playerInventory;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        // =====================================================
        //  Unity Lifecycle
        // =====================================================

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);

                if (inventoryPanel != null)
                {
                    _canvasObject  = inventoryPanel;
                    _moneyText     = moneyTextHUD;
                    _healthText    = healthTextHUD;
                    _itemContainer = itemSlotsContainer;

                    // Auto-discover InventoryItemSlot children ที่วางไว้ใน Editor แล้วใส่ Pool
                    if (_itemContainer != null)
                        ScanExistingSlots();

                    // Hook pagination buttons
                    if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);
                    if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);

                    // Hook close / back buttons
                    HookCloseButtons();

                    _canvasObject.SetActive(false);
                }
                else
                {
                    CreateProceduralUI();
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindInventory();
        }

        private void OnDestroy()
        {
            if (_playerInventory != null)
                _playerInventory.OnInventoryChanged -= RefreshUI;
        }

        private void Update()
        {
            if (!_isOpen) return;

            // ตรวจสอบ Hover + กดเลข 1 - 5 เพื่อ Assign ของลง Quick Slot ทันที (ดักเฉพาะไอเทมที่ใช้ได้ ไม่เอากระสุน/วัสดุ)
            if (InventoryItemSlot.HoveredSlot != null && !string.IsNullOrEmpty(InventoryItemSlot.HoveredSlot.CurrentItemName))
            {
                string hoveredItem = InventoryItemSlot.HoveredSlot.CurrentItemName;
                if (QuickSlotController.IsItemQuickSlotEligible(hoveredItem) && Keyboard.current != null)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                        QuickSlot?.AssignSlot(0, hoveredItem);
                    else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                        QuickSlot?.AssignSlot(1, hoveredItem);
                    else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                        QuickSlot?.AssignSlot(2, hoveredItem);
                    else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
                        QuickSlot?.AssignSlot(3, hoveredItem);
                    else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
                        QuickSlot?.AssignSlot(4, hoveredItem);
                }
            }
        }

        // =====================================================
        //  Item Slots Scanner
        // =====================================================

        /// <summary>
        /// สแกนหา InventoryItemSlot ที่วางอยู่ใน Editor
        /// หากผู้ใช้ยังไม่ได้ลาก itemSlots ใน Inspector จะดึงจากลูกของ _itemContainer / itemSlotsContainer ให้อัตโนมัติ
        /// </summary>
        public void ScanExistingSlots()
        {
            if (itemSlots == null)
            {
                itemSlots = new List<InventoryItemSlot>();
            }

            if (itemSlots.Count == 0)
            {
                Transform containerT = _itemContainer != null ? _itemContainer.transform : (itemSlotsContainer != null ? itemSlotsContainer.transform : null);
                if (containerT != null)
                {
                    foreach (Transform child in containerT)
                    {
                        InventoryItemSlot slot = child.GetComponent<InventoryItemSlot>();
                        if (slot != null && !itemSlots.Contains(slot))
                        {
                            itemSlots.Add(slot);
                        }
                    }
                }
            }
        }

        // =====================================================
        //  Public API
        // =====================================================

        public void SetReferences(PlayerController p, PlayerInventory inv = null, QuickSlotController qs = null)
        {
            player = p;
            playerInventory = inv != null ? inv : (p != null ? p.GetComponent<PlayerInventory>() : null);
            quickSlotController = qs;
            _playerInventory = playerInventory;
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= RefreshUI;
                _playerInventory.OnInventoryChanged += RefreshUI;
            }
        }

        private void FindInventory()
        {
            if (_playerInventory == null)
            {
                if (playerInventory != null)
                {
                    _playerInventory = playerInventory;
                }
                else if (player != null)
                {
                    _playerInventory = player.GetComponent<PlayerInventory>();
                }

                if (_playerInventory != null)
                {
                    _playerInventory.OnInventoryChanged -= RefreshUI;
                    _playerInventory.OnInventoryChanged += RefreshUI;
                }
            }
        }

        /// <summary>
        /// Toggles the inventory visibility state, updating timescale and refreshing content.
        /// </summary>
        public void ToggleInventory()
        {
            if (_playerInventory == null) FindInventory();

            _isOpen = !_isOpen;

            if (_canvasObject != null)
                _canvasObject.SetActive(_isOpen);

            // เอา Time.timeScale ออกตามที่ผู้เล่นต้องการ ไม่ให้หยุดเวลา

            if (_isOpen)
            {
                _currentPage = 0;
                
                if (_itemContainer != null)
                {
                    ScanExistingSlots();
                }
                RefreshUI();

                if (PopUpManager.Instance != null) PopUpManager.Instance.Push(this);
            }
            else
            {
                if (PopUpManager.Instance != null) PopUpManager.Instance.Remove(this);
            }
        }

        public void ClosePopUp()
        {
            if (_isOpen)
            {
                ToggleInventory();
            }
        }

        /// <summary>
        /// เปิดหน้าต่าง Inventory (สำหรับผูกกับ OnClick ของ Button)
        /// </summary>
        public void OpenInventory()
        {
            if (!_isOpen)
            {
                ToggleInventory();
            }
        }

        /// <summary>
        /// ปิดหน้าต่าง Inventory (สำหรับผูกกับ OnClick ของ Button หรือปุ่ม Back)
        /// </summary>
        public void CloseInventory()
        {
            if (_isOpen)
            {
                ToggleInventory();
            }
        }

        /// <summary>
        /// ปุ่ม Back สำหรับปิดหน้าต่างกระเป๋า
        /// </summary>
        public void OnBackButtonClicked()
        {
            CloseInventory();
        }

        public void Close() => CloseInventory();
        public void Open() => OpenInventory();

        /// <summary>
        /// ค้นหาและผูก Event ให้กับปุ่ม Close / Back อัตโนมัติ
        /// </summary>
        public void HookCloseButtons()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(CloseInventory);
                backButton.onClick.AddListener(CloseInventory);
            }

            // Auto-discover buttons named Close, Back, Cancel under inventoryPanel
            Transform container = inventoryPanel != null ? inventoryPanel.transform : (_panelObject != null ? _panelObject.transform : transform);
            if (container != null)
            {
                Button[] allBtns = container.GetComponentsInChildren<Button>(true);
                foreach (Button btn in allBtns)
                {
                    if (btn == prevPageButton || btn == nextPageButton) continue;
                    string btnName = btn.gameObject.name.ToLowerInvariant();
                    TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>(true);
                    string textContent = btnText != null ? btnText.text.ToLowerInvariant() : "";

                    if (btnName == "close" || btnName == "back" || btnName.Contains("closebtn") || btnName.Contains("backbtn") ||
                        textContent == "back" || textContent == "close" || textContent == "x")
                    {
                        btn.onClick.RemoveListener(CloseInventory);
                        btn.onClick.AddListener(CloseInventory);
                    }
                }
            }
        }




        /// <summary>ไปหน้าถัดไป</summary>
        public void NextPage()
        {
            if (_currentPage < _totalPages - 1)
            {
                _currentPage++;
                RefreshUI();
            }
        }

        /// <summary>ไปหน้าก่อนหน้า</summary>
        public void PrevPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshUI();
            }
        }

        /// <summary>
        /// Rebuilds the item lists and currency fields.
        /// </summary>
        public void RefreshUI()
        {
            if (_playerInventory == null) return;

            // 1. Update currency, health status, and backpack slot capacity
            if (_moneyText != null)
            {
                string bagColor = _playerInventory.IsInventoryFull ? "#FF5555" : "#FFA834";
                string bagStatus = _playerInventory.IsInventoryFull ? " (FULL)" : "";
                _moneyText.text = $"Wallet: <color=yellow>${_playerInventory.Money}</color>   |   Bag: <color={bagColor}><b>{_playerInventory.UsedSlots}/{_playerInventory.MaxSlots}</b></color>{bagStatus}";
            }

            if (_healthText != null)
            {
                Health health = _playerInventory.GetComponent<Health>();
                if (health != null)
                    _healthText.text = $"HP: <color=#1bff33>{Mathf.RoundToInt(health.CurrentHealth)}</color> / {Mathf.RoundToInt(health.MaxHealth)}";
            }

            ScanExistingSlots();

            if (itemSlots == null || itemSlots.Count == 0) return;

            // รวบ item แต่ละ slot (แยกตาม stack rules) เพื่อ slice ตาม page
            var itemList = _playerInventory.GetSlotPairs();

            // คำนวณ totalPages
            int pageSize = itemSlots.Count > 0 ? itemSlots.Count : Mathf.Max(1, slotsPerPage);
            _totalPages  = Mathf.Max(1, Mathf.CeilToInt((float)itemList.Count / pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _totalPages - 1);

            // ซ่อน indicator และปุ่มทั้งหมดถ้ามีแค่หน้าเดียว
            bool multiPage = _totalPages > 1;
            if (pageIndicatorText != null)
            {
                pageIndicatorText.gameObject.SetActive(multiPage);
                if (multiPage) pageIndicatorText.text = $"{_currentPage + 1} / {_totalPages}";
            }
            if (prevPageButton != null) { prevPageButton.gameObject.SetActive(multiPage); prevPageButton.interactable = _currentPage > 0; }
            if (nextPageButton != null) { nextPageButton.gameObject.SetActive(multiPage); nextPageButton.interactable = _currentPage < _totalPages - 1; }

            int startIdx = _currentPage * pageSize;

            PlayerController player = _playerInventory.GetComponent<PlayerController>();
            string equippedWeapon = player != null ? (player.CurrentWeaponName  ?? "").ToLower().Trim() : "";
            string equippedMelee  = player != null ? (player.CurrentMeleeWeaponName ?? "").ToLower().Trim() : "";

            if (itemList.Count == 0)
            {
                // ถ้าไม่มีไอเทม ให้ช่องแรกแสดงข้อความว่าว่าง และซ่อนช่องที่เหลือ
                for (int i = 0; i < itemSlots.Count; i++)
                {
                    if (itemSlots[i] == null) continue;
                    if (i == 0)
                    {
                        itemSlots[i].gameObject.SetActive(true);
                        itemSlots[i].SetData("- Empty -", 0, null, false, null);
                    }
                    else
                    {
                        itemSlots[i].gameObject.SetActive(false);
                    }
                }
                return;
            }

            // วนเซตข้อมูลลงในช่องที่มีอยู่เดิม ไม่ instantiate ใหม่ ไม่สร้างขยะใน memory (แบบเดียวกับ CraftController และ ShopUI)
            for (int i = 0; i < itemSlots.Count; i++)
            {
                InventoryItemSlot slot = itemSlots[i];
                if (slot == null) continue;

                int itemIndex = startIdx + i;
                if (itemIndex < itemList.Count)
                {
                    slot.gameObject.SetActive(true);

                    var pair    = itemList[itemIndex];
                    string key      = pair.Key;
                    string cleanKey = (key ?? "").ToLower().Trim();

                    ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(key) : null;
                    
                    bool isWeapon = false;
                    bool isUsable = false;

                    if (itemData != null)
                    {
                        isWeapon = itemData.type == ItemType.RangedWeapon || 
                                   itemData.type == ItemType.MeleeWeapon || 
                                   itemData.type == ItemType.ThrowingWeapon;
                        
                        isUsable = isWeapon || itemData.type == ItemType.Potion || itemData.type == ItemType.Bread || itemData.type == ItemType.Water || itemData.waterRestorationValue > 0f;
                    }

                    bool isEquipped = false;
                    if (isWeapon)
                    {
                        bool eqW = !string.IsNullOrEmpty(equippedWeapon) && 
                                   (cleanKey.Contains(equippedWeapon) || equippedWeapon.Contains(cleanKey) || 
                                   (cleanKey.Contains("pist") && equippedWeapon.Contains("pist")));
                        
                        bool eqM = !string.IsNullOrEmpty(equippedMelee) && 
                                   (cleanKey.Contains(equippedMelee) || equippedMelee.Contains(cleanKey));
                        
                        isEquipped = eqW || eqM;
                    }

                    Sprite icon = itemData != null ? itemData.icon : null;

                    slot.SetData(
                        itemName:   key,
                        quantity:   pair.Value,
                        icon:       icon,
                        isEquipped: isEquipped,
                        onClickUse: isUsable
                            ? (name) => { _playerInventory.UseItem(name); RefreshUI(); }
                            : (System.Action<string>)null
                    );
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        // =====================================================
        //  Procedural UI (fallback เมื่อไม่ assign inventoryPanel)
        // =====================================================

        private void CreateProceduralUI()
        {
            _canvasObject = new GameObject("InventoryCanvas");
            DontDestroyOnLoad(_canvasObject);
            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasObject.AddComponent<GraphicRaycaster>();

            _panelObject = new GameObject("InventoryPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            Image panelImage = _panelObject.AddComponent<Image>();
            panelImage.color = panelColor;

            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot     = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 680f);

            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(_panelObject.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.3f);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            borderObj.transform.SetAsFirstSibling();

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_panelObject.transform, false);
            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text      = "SURVIVAL GEAR / INVENTORY";
            titleText.fontSize  = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color     = accentColor;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin        = new Vector2(0.5f, 1f);
            titleRect.anchorMax        = new Vector2(0.5f, 1f);
            titleRect.pivot            = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector3(0f, -40f, 0f);
            titleRect.sizeDelta        = new Vector2(500f, 50f);

            GameObject moneyObj = new GameObject("MoneyText");
            moneyObj.transform.SetParent(_panelObject.transform, false);
            _moneyText           = moneyObj.AddComponent<TextMeshProUGUI>();
            _moneyText.text      = "Wallet: <color=yellow>$0</color>";
            _moneyText.fontSize  = 24;
            _moneyText.alignment = TextAlignmentOptions.Left;
            RectTransform moneyRect = moneyObj.GetComponent<RectTransform>();
            moneyRect.anchorMin        = new Vector2(0f, 1f);
            moneyRect.anchorMax        = new Vector2(0.5f, 1f);
            moneyRect.pivot            = new Vector2(0f, 1f);
            moneyRect.anchoredPosition = new Vector3(30f, -95f, 0f);
            moneyRect.sizeDelta        = new Vector2(250f, 40f);

            GameObject healthObj = new GameObject("HealthText");
            healthObj.transform.SetParent(_panelObject.transform, false);
            _healthText           = healthObj.AddComponent<TextMeshProUGUI>();
            _healthText.text      = "HP: <color=red>100/100</color>";
            _healthText.fontSize  = 24;
            _healthText.alignment = TextAlignmentOptions.Right;
            RectTransform healthRect = healthObj.GetComponent<RectTransform>();
            healthRect.anchorMin        = new Vector2(0.5f, 1f);
            healthRect.anchorMax        = new Vector2(1f, 1f);
            healthRect.pivot            = new Vector2(1f, 1f);
            healthRect.anchoredPosition = new Vector3(-30f, -95f, 0f);
            healthRect.sizeDelta        = new Vector2(250f, 40f);

            GameObject scrollObj = new GameObject("ItemScrollView");
            scrollObj.transform.SetParent(_panelObject.transform, false);
            RectTransform scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            scrollRectTrans.anchorMin        = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchorMax        = new Vector2(0.5f, 0.5f);
            scrollRectTrans.pivot            = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchoredPosition = new Vector3(0f, -60f, 0f);
            scrollRectTrans.sizeDelta        = new Vector2(500f, 440f);

            _itemContainer = new GameObject("ItemListContent");
            _itemContainer.transform.SetParent(scrollObj.transform, false);
            VerticalLayoutGroup layout = _itemContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing              = 8f;
            layout.childControlHeight   = false;
            layout.childControlWidth    = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth  = true;
            RectTransform containerRect = _itemContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(_panelObject.transform, false);
            TMP_Text hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text      = "Press [ I ] or [ ESC ] to Close";
            hintText.fontSize  = 20;
            hintText.color     = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            hintText.alignment = TextAlignmentOptions.Center;
            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin        = new Vector2(0.5f, 0f);
            hintRect.anchorMax        = new Vector2(0.5f, 0f);
            hintRect.pivot            = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector3(0f, 25f, 0f);
            hintRect.sizeDelta        = new Vector2(500f, 30f);

            _canvasObject.SetActive(false);
        }
    }
}
