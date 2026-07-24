using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    public class InventoryUI : MonoBehaviour, IPopUp
    {
        public static InventoryUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("InventoryUIManager");
                    _instance = go.AddComponent<InventoryUI>();
                }
                return _instance;
            }
        }
        private static InventoryUI _instance;

        [Header("UI Customization")]
        [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.94f);
        [SerializeField] private Color accentColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        [Header("UI References (Optional)")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private TMPro.TMP_Text moneyTextHUD;
        [SerializeField] private TMPro.TMP_Text healthTextHUD;
        [Tooltip("Content GameObject — วาง InventoryItemSlot ลูกไว้ข้างในได้เลย")]
        [SerializeField] private GameObject itemSlotsContainer;

        [Header("Pagination")]
        [Tooltip("จำนวน Slot ต่อหน้า — ควรตรงกับจำนวน InventoryItemSlot ที่วางใน Editor")]
        [SerializeField] private int slotsPerPage = 8;
        [Tooltip("ปุ่มไปหน้าก่อนหน้า")]
        [SerializeField] private UnityEngine.UI.Button prevPageButton;
        [Tooltip("ปุ่มไปหน้าถัดไป")]
        [SerializeField] private UnityEngine.UI.Button nextPageButton;
        [Tooltip("TMP_Text แสดง '1 / 3'")]
        [SerializeField] private TMPro.TMP_Text pageIndicatorText;



        // ===== Internal Pool (built-in) =====
        private readonly List<InventoryItemSlot> _slotPool = new List<InventoryItemSlot>();
        private int _activeSlotCount = 0;

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

        // =====================================================
        //  Slot Pool — Built-in, ไม่ต้องแยก Component
        // =====================================================

        /// <summary>
        /// สแกน InventoryItemSlot ที่มีอยู่แล้วใน itemSlotsContainer ใส่เข้า Pool
        /// วางหลาย ๆ ตัวไว้ใน Editor → Pool จะ reuse พวกมันแทน Instantiate
        /// </summary>
        private void ScanExistingSlots()
        {
            _slotPool.Clear();
            foreach (Transform child in _itemContainer.transform)
            {
                InventoryItemSlot slot = child.GetComponent<InventoryItemSlot>();
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    _slotPool.Add(slot);
                }
            }
        }

        /// <summary>
        /// ดึง Slot ถัดไปจาก Pool ถ้าหมดจะ Instantiate ตัวแรกใน pool มาเพิ่ม (expand ครั้งเดียว)
        /// </summary>
        private InventoryItemSlot GetSlot()
        {
            if (_activeSlotCount >= _slotPool.Count)
            {
                // Pool หมด — clone จาก slot แรกที่มีอยู่ (ถ้ามี) หรือ bail
                if (_slotPool.Count == 0)
                {
                    Debug.LogWarning("[InventoryUI] No InventoryItemSlot found in container. Place at least one in the Editor.");
                    return null;
                }
                InventoryItemSlot clone = Instantiate(_slotPool[0], _itemContainer.transform);
                clone.name = $"ItemSlot_{_slotPool.Count:00}";
                _slotPool.Add(clone);
                Debug.LogWarning($"[InventoryUI] Slot pool expanded to {_slotPool.Count}. Consider placing more slots in the Editor.");
            }

            InventoryItemSlot s = _slotPool[_activeSlotCount];
            s.gameObject.SetActive(true);
            _activeSlotCount++;
            return s;
        }

        /// <summary>
        /// เรียกก่อน Refresh ทุกครั้ง — reset ตัวนับ
        /// </summary>
        private void PoolBeginFrame() => _activeSlotCount = 0;

        /// <summary>
        /// เรียกหลัง Refresh ทุกครั้ง — ซ่อน Slot ที่ไม่ได้ใช้
        /// </summary>
        private void PoolEndFrame()
        {
            for (int i = _activeSlotCount; i < _slotPool.Count; i++)
            {
                if (_slotPool[i].gameObject.activeSelf)
                {
                    _slotPool[i].Clear();
                    _slotPool[i].gameObject.SetActive(false);
                }
            }
        }

        // =====================================================
        //  Public API
        // =====================================================

        private void FindInventory()
        {
            if (_playerInventory == null)
            {
                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    _playerInventory = player.GetComponent<PlayerInventory>();
                    if (_playerInventory != null)
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

            // 1. Update currency and health status
            if (_moneyText != null)
                _moneyText.text = $"Wallet: <color=yellow>${_playerInventory.Money}</color>";

            if (_healthText != null)
            {
                Health health = _playerInventory.GetComponent<Health>();
                if (health != null)
                    _healthText.text = $"HP: <color=#1bff33>{Mathf.RoundToInt(health.CurrentHealth)}</color> / {Mathf.RoundToInt(health.MaxHealth)}";
            }

            if (_itemContainer == null) return;

            // รวบ item ทั้งหมดเป็น list เพื่อ slice ตาม page
            Dictionary<string, int> quantities = _playerInventory.GetItemQuantities();
            var itemList = new List<KeyValuePair<string, int>>(quantities);

            // คำนวณ totalPages
            int pageSize = Mathf.Max(1, slotsPerPage);
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

            // slice items ตาม page
            int startIdx = _currentPage * pageSize;
            int endIdx   = Mathf.Min(startIdx + pageSize, itemList.Count);

            // 2. ใช้ Pool path ถ้ามี slot อยู่ใน pool, fallback ถ้าไม่มี
            if (_slotPool.Count > 0)
            {
                // ===== Pool Path (Zero GC) =====
                PlayerController player = _playerInventory.GetComponent<PlayerController>();
                string equippedWeapon = player != null ? (player.CurrentWeaponName  ?? "").ToLower().Trim() : "";
                string equippedMelee  = player != null ? (player.CurrentMeleeWeaponName ?? "").ToLower().Trim() : "";

                PoolBeginFrame();

                if (itemList.Count == 0)
                {
                    InventoryItemSlot emptySlot = GetSlot();
                    if (emptySlot != null)
                        emptySlot.SetData("- Empty -", 0, null, false, null);
                }
                else
                {
                    // แสดงเฉพาะ item ในหน้านี้
                    for (int i = startIdx; i < endIdx; i++)
                    {
                        var pair    = itemList[i];
                        string key      = pair.Key;
                        string cleanKey = (key ?? "").ToLower().Trim();

                        bool isWeapon = !cleanKey.Contains("ammo") &&
                                        (cleanKey.Contains("rifl")  || cleanKey.Contains("shot") ||
                                         cleanKey.Contains("pist")  || cleanKey.Contains("knife") ||
                                         cleanKey.Contains("bat")   || cleanKey.Contains("machete"));

                        bool isUsable = key == "Potion" || key == "Bread" || isWeapon;

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

                        ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(key) : null;
                        Sprite icon = itemData != null ? itemData.icon : null;

                        InventoryItemSlot slot = GetSlot();
                        if (slot == null) break;

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
                }

                PoolEndFrame();
            }
            else
            {
                // ===== Fallback Path =====
                Debug.LogWarning("[InventoryUI] ไม่พบ InventoryItemSlot ใน _itemContainer เลย! กรุณาเพิ่มสคริปต์ InventoryItemSlot ใส่ UI ของคุณ หรือเช็คว่าลาก Content มาถูกช่องหรือไม่");
            }
        }

        // =====================================================
        //  Legacy fallback row builder (ใช้เมื่อไม่มี slot ใน pool)
        // =====================================================

        private void CreateItemRow(string itemName, string textContent, Color textColor, bool isUsable)
        {
            GameObject row = new GameObject("ItemRow");
            row.transform.SetParent(_itemContainer.transform, false);

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.04f);

            RectTransform rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500f, 50f);

            if (isUsable)
            {
                Button btn = row.AddComponent<Button>();
                ColorBlock colors = btn.colors;
                colors.normalColor      = new Color(1f, 1f, 1f, 0.04f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
                colors.pressedColor     = new Color(1f, 1f, 1f, 0.2f);
                colors.selectedColor    = new Color(1f, 1f, 1f, 0.04f);
                btn.colors = colors;
                btn.onClick.AddListener(() =>
                {
                    if (_playerInventory != null && _playerInventory.UseItem(itemName))
                        RefreshUI();
                });
            }

            bool hasIcon = false;
            ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
            if (data != null && data.icon != null)
            {
                GameObject iconObj = new GameObject("ItemIcon");
                iconObj.transform.SetParent(row.transform, false);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = data.icon;
                iconImg.preserveAspect = true;

                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot     = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(12f, 0f);
                iconRect.sizeDelta = new Vector2(32f, 32f);
                hasIcon = true;
            }

            GameObject textObj = new GameObject("ItemText");
            textObj.transform.SetParent(row.transform, false);

            TMP_Text rowText = textObj.AddComponent<TextMeshProUGUI>();
            rowText.text      = textContent;
            rowText.fontSize  = 24;
            rowText.color     = textColor;
            rowText.alignment = TextAlignmentOptions.Left;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(hasIcon ? 56f : 15f, 5f);
            textRect.offsetMax = new Vector2(-15f, -5f);
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
