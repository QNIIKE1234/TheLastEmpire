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
    /// ShopUI — ใช้ pattern เดียวกับ InventoryUI ทุกอย่าง
    /// Pool, Pagination, InventoryItemSlot เหมือนกันหมด แค่เปลี่ยน Logic เป็น Buy
    ///
    /// Setup:
    ///   1. Dup Panel จาก Inventory มาแล้วลาก Script นี้ใส่
    ///   2. Assign ช่องใน Inspector
    ///   3. เพิ่ม ShopItem[] ใน Inspector
    ///   4. เรียก ShopUI.Instance.OpenShop() จาก NPC / Trigger
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
        [Tooltip("Content GO ที่มี InventoryItemSlot ลูก — เหมือน Inventory")]
        [SerializeField] private GameObject itemSlotsContainer;

        [Header("Pagination")]
        [SerializeField] private int slotsPerPage = 8;
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
        //  Internal Pool (same pattern as InventoryUI)
        // =====================================================

        private readonly List<InventoryItemSlot> _slotPool = new List<InventoryItemSlot>();
        private int _activeSlotCount = 0;
        private int _currentPage     = 0;
        private int _totalPages      = 1;

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

                if (shopPanel != null)
                {
                    ScanExistingSlots();
                    if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);
                    if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
                    
                    if (tabBuyButton != null) tabBuyButton.onClick.AddListener(() => SwitchTab(ShopMode.Buy));
                    if (tabSellButton != null) tabSellButton.onClick.AddListener(() => SwitchTab(ShopMode.Sell));

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
        //  Slot Pool (identical to InventoryUI)
        // =====================================================

        private void ScanExistingSlots()
        {
            _slotPool.Clear();
            if (itemSlotsContainer == null) return;

            foreach (Transform child in itemSlotsContainer.transform)
            {
                InventoryItemSlot slot = child.GetComponent<InventoryItemSlot>();
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    _slotPool.Add(slot);
                }
            }
        }

        private InventoryItemSlot GetSlot()
        {
            if (_activeSlotCount >= _slotPool.Count)
            {
                if (_slotPool.Count == 0)
                {
                    Debug.LogWarning("[ShopUI] No InventoryItemSlot found in current container.");
                    return null;
                }
                InventoryItemSlot clone = Instantiate(_slotPool[0], itemSlotsContainer.transform);
                clone.name = $"ShopSlot_{_slotPool.Count:00}";
                _slotPool.Add(clone);
            }
            InventoryItemSlot s = _slotPool[_activeSlotCount++];
            s.gameObject.SetActive(true);
            return s;
        }

        private void PoolBeginFrame() => _activeSlotCount = 0;

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

            _isOpen      = true;
            SwitchTab(ShopMode.Buy);

            if (shopPanel != null) shopPanel.SetActive(true);
            // ไม่หยุดเวลาแล้ว

            if (PopUpManager.Instance != null) PopUpManager.Instance.Push(this);
        }

        public void SwitchTab(ShopMode mode)
        {
            _currentMode = mode;
            _currentPage = 0;

            bool isBuy = mode == ShopMode.Buy;

            if (tabBuyButton != null) tabBuyButton.interactable = !isBuy;
            if (tabSellButton != null) tabSellButton.interactable = isBuy;

            ScanExistingSlots();
            RefreshUI();
        }

        /// <summary>
        /// ปิดร้านค้า
        /// </summary>
        public void CloseShop()
        {
            _isOpen = false;
            if (shopPanel != null) shopPanel.SetActive(false);
            // ไม่คืนค่าเวลาแล้ว

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
            if (itemSlotsContainer == null) return;

            if (walletText != null && _playerInventory != null)
                walletText.text = $"Wallet: <color=yellow>${_playerInventory.Money}</color>";

            int totalItems = 0;
            List<KeyValuePair<string, int>> sellItems = null;

            if (_currentMode == ShopMode.Buy)
            {
                totalItems = shopItems.Count;
            }
            else
            {
                if (_playerInventory != null)
                {
                    sellItems = new List<KeyValuePair<string, int>>(_playerInventory.GetItemQuantities());
                    totalItems = sellItems.Count;
                }
            }

            int pageSize = Mathf.Max(1, slotsPerPage);
            _totalPages  = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _totalPages - 1);

            bool multiPage = _totalPages > 1;
            if (pageIndicatorText != null)
            {
                pageIndicatorText.gameObject.SetActive(multiPage);
                if (multiPage) pageIndicatorText.text = $"{_currentPage + 1} / {_totalPages}";
            }
            if (prevPageButton != null) { prevPageButton.gameObject.SetActive(multiPage); prevPageButton.interactable = _currentPage > 0; }
            if (nextPageButton != null) { nextPageButton.gameObject.SetActive(multiPage); nextPageButton.interactable = _currentPage < _totalPages - 1; }

            int startIdx = _currentPage * pageSize;
            int endIdx   = Mathf.Min(startIdx + pageSize, totalItems);

            PoolBeginFrame();

            if (totalItems == 0)
            {
                InventoryItemSlot empty = GetSlot();
                if (empty != null) empty.SetData("- No Items -", 0, null, false, null);
            }
            else
            {
                for (int i = startIdx; i < endIdx; i++)
                {
                    InventoryItemSlot slot = GetSlot();
                    if (slot == null) break;

                    if (_currentMode == ShopMode.Buy)
                    {
                        ShopItemConfig item = shopItems[i];
                        if (item.item == null) continue;

                        int displayPrice = _isFree ? 0 : item.price;
                        bool canAfford  = _playerInventory != null && _playerInventory.CanAfford(displayPrice);

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
                        var pair = sellItems[i];
                        string itemName = pair.Key;
                        int currentQty = pair.Value;

                        ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
                        Sprite icon = itemData != null ? itemData.icon : null;
                        
                        // สมมติราคาขาย = 50% ของราคาซื้อ ถ้าหาเจอใน shopItems, ถ้าไม่เจอให้ขาย 10 บาท
                        int sellPrice = 10;
                        var shopItem = shopItems.Find(s => s.item != null && s.item.itemName == itemName);
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
            }

            PoolEndFrame();
        }

        // =====================================================
        //  Buy / Sell Logic
        // =====================================================

        private void BuyItem(string realItemName)
        {
            if (_playerInventory == null) return;

            ShopItemConfig item = shopItems.Find(s => s.item != null && s.item.itemName == realItemName);
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
