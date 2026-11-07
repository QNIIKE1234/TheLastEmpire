using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// ข้อมูลสินค้าในร้าน — กำหนดผ่าน Inspector ของ ShopUI
    /// </summary>
    [Serializable]
    public class ShopItem
    {
        [Tooltip("ชื่อไอเทม ต้องตรงกับชื่อที่ใช้ใน PlayerInventory.AddItem()")]
        public string itemName;
        [Tooltip("ราคา (หน่วยเดียวกับ PlayerInventory.Money)")]
        public int price;
        [Tooltip("จำนวนสต็อก (-1 = ไม่จำกัด)")]
        public int stock = -1;
        [Tooltip("Icon แสดงใน Slot (optional)")]
        public Sprite icon;
    }

    /// <summary>
    /// ShopUI — จัดการหน้าร้านค้า ซื้อ/ขาย
    /// ใช้ Pattern เดียวกับ CraftController:
    /// - อ้างอิงแถว InventoryItemSlot เดิมที่มีอยู่แล้วใน UI (itemSlots)
    /// - วน SetData ซ้ำ ไม่ Instantiate ใหม่ ไม่สร้างขยะใน Memory
    /// - ระบบ Pagination อิงตามจำนวน Slots ในหน้าอัตโนมัติ
    /// </summary>
    public class ShopUI : MonoBehaviour, IPopUp
    {
        public static ShopUI Instance { get; private set; }

        // =====================================================
        //  Inspector Fields
        // =====================================================

        [Header("UI References")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private TMP_Text walletText;
        [SerializeField] private TMP_Text shopTitleText;
        [Tooltip("Content GO ที่มี InventoryItemSlot ลูก (Optional: หากไม่ลาก itemSlots ระบบจะดึงลูกจาก Container นี้ให้อัตโนมัติ)")]
        [SerializeField] private GameObject itemSlotsContainer;

        [Header("Item Slots (รายการแถวสินค้า)")]
        [Tooltip("ลากแถว InventoryItemSlot ทั้งหมดที่วางอยู่ในหน้าต่าง UI ใส่ที่นี่ (เช่น 7-8 แถว) เพื่อวนเซตข้อมูลซ้ำ ไม่สร้างใหม่")]
        [SerializeField] private List<InventoryItemSlot> itemSlots = new List<InventoryItemSlot>();

        [Header("Pagination")]
        [Tooltip("จำนวนแถวต่อ 1 หน้า (ถ้าลาก itemSlots ไว้จะอิงตามจำนวนช่องใน List อัตโนมัติ)")]
        [SerializeField] private int slotsPerPage = 7;
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMP_Text pageIndicatorText;

        [Header("Tab Navigation")]
        [SerializeField] private Button tabBuyButton;
        [SerializeField] private Button tabSellButton;

        [Header("Shop Stock")]
        [Tooltip("รายการสินค้าในร้านนี้ (เผื่ออยากตั้ง default ไว้)")]
        [SerializeField] private System.Collections.Generic.List<ShopItemConfig> shopItems = new System.Collections.Generic.List<ShopItemConfig>();
        private bool _isFree = false;

        // =====================================================
        //  Internal State
        // =====================================================

        private int _currentPage = 0;
        private int _totalPages  = 1;

        public enum ShopMode { Buy, Sell }
        private ShopMode _currentMode = ShopMode.Buy;

        private PlayerInventory _playerInventory;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        // =====================================================
        //  Unity Lifecycle
        // =====================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                ScanExistingSlots();

                if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);
                if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
                
                if (tabBuyButton != null) tabBuyButton.onClick.AddListener(() => SwitchTab(ShopMode.Buy));
                if (tabSellButton != null) tabSellButton.onClick.AddListener(() => SwitchTab(ShopMode.Sell));

                if (shopPanel != null)
                {
                    shopPanel.SetActive(false);
                }
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start() => FindInventory();

        private void OnDestroy()
        {
            if (_playerInventory != null)
                _playerInventory.OnMoneyChanged -= OnWalletChanged;
        }

        // =====================================================
        //  Slots Setup (Same as CraftController)
        // =====================================================

        public void ScanExistingSlots()
        {
            if (itemSlots == null)
            {
                itemSlots = new List<InventoryItemSlot>();
            }

            // ถ้าใน Inspector ยังไม่ได้ลากใส่ ให้ดึงจากลูกของ itemSlotsContainer อัตโนมัติ
            if (itemSlots.Count == 0 && itemSlotsContainer != null)
            {
                foreach (Transform child in itemSlotsContainer.transform)
                {
                    InventoryItemSlot slot = child.GetComponent<InventoryItemSlot>();
                    if (slot == null) slot = child.GetComponentInChildren<InventoryItemSlot>(true);
                    if (slot != null && !itemSlots.Contains(slot))
                    {
                        itemSlots.Add(slot);
                    }
                }
            }
        }

        // =====================================================
        //  Public API
        // =====================================================

        /// <summary>
        /// เปิดร้านค้า — เรียกจาก NPCController
        /// </summary>
        public void OpenShopMenu(System.Collections.Generic.List<ShopItemConfig> items = null, bool isFree = false)
        {
            if (_playerInventory == null) FindInventory();

            if (items != null)
            {
                shopItems = items;
                _isFree = isFree;
            }

            ScanExistingSlots();

            _isOpen = true;
            SwitchTab(ShopMode.Buy);

            if (shopPanel != null) shopPanel.SetActive(true);

            if (PopUpManager.Instance != null) PopUpManager.Instance.Push(this);
        }

        public void SwitchTab(ShopMode mode)
        {
            _currentMode = mode;
            _currentPage = 0;

            bool isBuy = mode == ShopMode.Buy;

            UpdateTabButton(tabBuyButton, isBuy);
            UpdateTabButton(tabSellButton, !isBuy);

            RefreshUI();
        }

        private void UpdateTabButton(Button btn, bool isSelected)
        {
            if (btn == null) return;
            btn.interactable = !isSelected;

            ColorBlock cb = btn.colors;
            // แท็บที่เลือกอยู่: ให้แสดงสว่างเต็ม 100%
            cb.disabledColor = new Color(1f, 1f, 1f, 1f);
            // แท็บที่ไม่ได้เลือก: ให้แสดงสีมืดลง/จางลง (60%)
            cb.normalColor = new Color(0.65f, 0.65f, 0.65f, 0.6f);
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            btn.colors = cb;

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = isSelected ? Color.black : new Color(0.2f, 0.2f, 0.2f, 0.65f);
            }
        }

        /// <summary>
        /// ปิดร้านค้า
        /// </summary>
        public void CloseShop()
        {
            _isOpen = false;
            if (shopPanel != null) shopPanel.SetActive(false);

            if (PopUpManager.Instance != null) PopUpManager.Instance.Remove(this);
        }

        public void ClosePopUp()
        {
            if (_isOpen)
            {
                CloseShop();
            }
        }

        public void NextPage()
        {
            if (_currentPage < _totalPages - 1) { _currentPage++; RefreshUI(); }
        }

        public void PrevPage()
        {
            if (_currentPage > 0) { _currentPage--; RefreshUI(); }
        }

        // =====================================================
        //  Refresh UI
        // =====================================================

        public void RefreshUI()
        {
            if (itemSlots == null || itemSlots.Count == 0)
            {
                ScanExistingSlots();
            }

            if (itemSlots == null || itemSlots.Count == 0) return;

            if (walletText != null && _playerInventory != null)
                walletText.text = $"Wallet: <color=yellow>${_playerInventory.Money}</color>";

            int totalItems = 0;
            List<KeyValuePair<string, int>> sellItems = null;

            if (_currentMode == ShopMode.Buy)
            {
                totalItems = shopItems != null ? shopItems.Count : 0;
            }
            else
            {
                if (_playerInventory != null)
                {
                    sellItems = new List<KeyValuePair<string, int>>(_playerInventory.GetItemQuantities());
                    totalItems = sellItems.Count;
                }
            }

            // ถ้ามี slot อยู่ใน List จะใช้จำนวน slot นั้นเป็นขนาดต่อหน้าทันที (เช่น 7-8 ช่องตาม UI)
            int pageSize = itemSlots.Count > 0 ? itemSlots.Count : Mathf.Max(1, slotsPerPage);
            _totalPages  = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _totalPages - 1);

            bool multiPage = _totalPages > 1;
            if (pageIndicatorText != null)
            {
                pageIndicatorText.gameObject.SetActive(multiPage);
                if (multiPage) pageIndicatorText.text = $"{_currentPage + 1} / {_totalPages}";
            }
            if (prevPageButton != null)
            {
                prevPageButton.gameObject.SetActive(multiPage);
                prevPageButton.interactable = _currentPage > 0;
            }
            if (nextPageButton != null)
            {
                nextPageButton.gameObject.SetActive(multiPage);
                nextPageButton.interactable = _currentPage < _totalPages - 1;
            }

            int startIdx = _currentPage * pageSize;

            if (totalItems == 0)
            {
                // ถ้าไม่มีไอเทม ให้ช่องแรกแสดงข้อความว่าไม่มีของ และซ่อนช่องที่เหลือ
                for (int i = 0; i < itemSlots.Count; i++)
                {
                    if (itemSlots[i] == null) continue;
                    if (i == 0)
                    {
                        itemSlots[i].gameObject.SetActive(true);
                        itemSlots[i].SetData("- No Items -", 0, null, false, null);
                    }
                    else
                    {
                        itemSlots[i].gameObject.SetActive(false);
                    }
                }
                return;
            }

            // วนเซตข้อมูลลงในช่องที่มีอยู่เดิม ไม่ instantiate ใหม่ ไม่สร้างขยะใน memory (แบบเดียวกับ CraftController)
            for (int i = 0; i < itemSlots.Count; i++)
            {
                InventoryItemSlot slot = itemSlots[i];
                if (slot == null) continue;

                int itemIndex = startIdx + i;
                if (itemIndex < totalItems)
                {
                    slot.gameObject.SetActive(true);

                    if (_currentMode == ShopMode.Buy)
                    {
                        ShopItemConfig item = shopItems[itemIndex];
                        if (item.item == null)
                        {
                            slot.gameObject.SetActive(false);
                            continue;
                        }

                        int displayPrice = _isFree ? 0 : item.price;
                        bool canAfford   = _playerInventory != null && _playerInventory.CanAfford(displayPrice);

                        // ชื่อ + จำนวนที่จะได้รับ เช่น Ammo x30
                        string displayName = item.item.defaultQuantity > 1 
                            ? $"{item.item.itemName} <color=#90A4AE>x{item.item.defaultQuantity}</color>" 
                            : item.item.itemName;

                        slot.SetData(
                            itemName:   displayName,
                            quantity:   displayPrice, // Quantity field = ราคา
                            icon:       item.item.icon,
                            isEquipped: false,
                            onClickUse: canAfford ? (name) => BuyItem(item.item.itemName) : (Action<string>)null,
                            customButtonText: "Buy",
                            customQuantityText: $"<color=yellow>${displayPrice}</color>"
                        );
                    }
                    else // Sell Mode
                    {
                        var pair = sellItems[itemIndex];
                        string itemName = pair.Key;
                        int currentQty = pair.Value;

                        ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
                        Sprite icon = itemData != null ? itemData.icon : null;
                        
                        // สมมติราคาขาย = 50% ของราคาซื้อ ถ้าหาเจอใน shopItems, ถ้าไม่เจอให้ขาย 10 บาท
                        int sellPrice = 10;
                        var shopItem = shopItems != null ? shopItems.Find(s => s.item != null && s.item.itemName == itemName) : default;
                        if (shopItem.item != null) sellPrice = Mathf.Max(1, shopItem.price / 2);

                        // ชื่อไอเทม + x จำนวนที่มีในกระเป๋า (เพื่อให้รู้ว่ากำลังจะขายอะไร)
                        string displayName = $"{itemName} <color=#90A4AE>(Owned: {currentQty})</color>";

                        slot.SetData(
                            itemName:   displayName,
                            quantity:   sellPrice, // Quantity = ราคาที่จะได้เงิน
                            icon:       icon,
                            isEquipped: false,
                            onClickUse: (name) => SellItem(itemName, sellPrice),
                            customButtonText: "Sell",
                            customQuantityText: $"<color=yellow>${sellPrice}</color>"
                        );
                    }
                }
                else
                {
                    // ช่องเกินจากจำนวนสินค้าในหน้านี้ ให้ซ่อนไป
                    slot.gameObject.SetActive(false);
                }
            }
        }

        // =====================================================
        //  Buy / Sell Logic
        // =====================================================

        private void BuyItem(string realItemName)
        {
            if (_playerInventory == null) return;

            ShopItemConfig item = shopItems != null ? shopItems.Find(s => s.item != null && s.item.itemName == realItemName) : default;
            if (item.item == null) return;

            int price = _isFree ? 0 : item.price;

            if (!_playerInventory.SpendMoney(price)) return;

            // AddItem ให้ตามจำนวน defaultQuantity ที่ตั้งไว้ใน ItemData
            _playerInventory.AddItem(item.item.itemName, item.item.defaultQuantity);

            Debug.Log($"[ShopUI] Bought: {item.item.itemName} x{item.item.defaultQuantity} for ${price}.");
            RefreshUI();
        }

        private void SellItem(string itemName, int sellPrice)
        {
            if (_playerInventory == null) return;

            // ถอน 1 ชิ้นจาก Inventory (จำเป็นต้องมีระบบ RemoveItem ใน PlayerInventory)
            bool removed = _playerInventory.RemoveItem(itemName, 1);
            if (removed)
            {
                _playerInventory.AddMoney(sellPrice);
                Debug.Log($"[ShopUI] Sold: {itemName} for ${sellPrice}.");
                RefreshUI();
            }
        }

        // =====================================================
        //  Helpers
        // =====================================================

        private void FindInventory()
        {
            if (_playerInventory != null) return;
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerInventory = player.GetComponent<PlayerInventory>();
                if (_playerInventory != null)
                    _playerInventory.OnMoneyChanged += OnWalletChanged;
            }
        }

        private void OnWalletChanged(int newAmount)
        {
            if (_isOpen) RefreshUI();
        }
    }
}
