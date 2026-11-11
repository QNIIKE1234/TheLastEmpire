using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [System.Serializable]
    public class InventorySlot
    {
        public string itemName;
        public int quantity;

        public InventorySlot()
        {
            this.itemName = string.Empty;
            this.quantity = 0;
        }

        public InventorySlot(string itemName, int quantity)
        {
            this.itemName = itemName;
            this.quantity = quantity;
        }
    }

    public class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        [Header("Inventory Status")]
        [SerializeField] private int money = 0;
        [SerializeField] private List<string> items = new List<string>();
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        [Header("Inventory Capacity")]
        [Tooltip("ความจุช่องกระเป๋าเก็บของ (ขั้นต่ำ 18 ช่อง, มากสุด 30 ช่อง)")]
        [Range(18, 30)]
        [SerializeField] private int maxSlots = 20;

        [Header("Starting Loadout")]
        [SerializeField] private PlayerConfigSO playerConfig;
        
        [Tooltip("รายการไอเทมที่จะได้รับตอนเริ่มเกม (ถ้าไม่ใช้ Config)")]
        [SerializeField] private List<string> defaultStartingItems = new List<string> { "Pistol", "Knife" };
        [SerializeField] private int defaultPistolAmmo = 60;

        public int Money => money;
        public List<string> Items => items;
        public List<InventorySlot> Slots => slots;
        public int MaxSlots => maxSlots;
        public int UsedSlots => slots.Count;
        public bool IsInventoryFull => UsedSlots >= maxSlots;

        public event System.Action<int> OnMoneyChanged;
        public event System.Action<int> OnMoneyAdded;
        public event System.Action<string> OnItemCollected;
        public event System.Action<string, int> OnItemAdded;
        public event System.Action OnInventoryChanged;

        public bool SuppressNotificationEvents { get; set; } = false;
        private bool _isInitializingStartingLoadout = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static int GetMaxStackSize(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return 99;
            ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
            if (data != null) return data.MaxStackSize;
            return 99;
        }

        private void Start()
        {
            _isInitializingStartingLoadout = true;
            if (slots == null) slots = new List<InventorySlot>();
            if (items == null) items = new List<string>();
            items.RemoveAll(string.IsNullOrWhiteSpace);

            if (slots.Count == 0)
            {
                if (items.Count > 0)
                {
                    var initItems = new List<string>(items);
                    items.Clear();
                    foreach (var it in initItems)
                    {
                        AddItem(it, 1);
                    }
                }
                else
                {
                    List<string> startItems = playerConfig != null ? playerConfig.startingItems : defaultStartingItems;
                    int startAmmo = playerConfig != null ? playerConfig.startingPistolAmmo : defaultPistolAmmo;
                    
                    if (playerConfig != null)
                    {
                        money = playerConfig.startingMoney;
                        if (playerConfig.maxInventorySlots >= 18)
                        {
                            maxSlots = Mathf.Clamp(playerConfig.maxInventorySlots, 18, 30);
                        }
                    }

                    if (startItems != null)
                    {
                        foreach (string item in startItems)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                                AddItem(item.Trim(), 1);
                        }
                    }

                    if (startAmmo > 0)
                    {
                        AddItem("Pistol Ammo", startAmmo);
                    }
                }
            }
            SyncItemsFlatList();
            _isInitializingStartingLoadout = false;
        }

        public void AddMoney(int amount)
        {
            money += amount;
            Debug.Log($"[PlayerInventory] Earned ${amount}! Total Wallet: ${money}");
            OnMoneyChanged?.Invoke(money);
            if (amount > 0 && !_isInitializingStartingLoadout && !SuppressNotificationEvents)
            {
                OnMoneyAdded?.Invoke(amount);
            }
            OnInventoryChanged?.Invoke();
        }

        public bool CanAfford(int price) => money >= price;

        public bool SpendMoney(int amount)
        {
            if (money < amount)
            {
                Debug.LogWarning($"[PlayerInventory] Not enough money! Need ${amount}, have ${money}");
                return false;
            }
            money -= amount;
            Debug.Log($"[PlayerInventory] Spent ${amount}. Remaining: ${money}");
            OnMoneyChanged?.Invoke(money);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// กำหนดความจุช่องกระเป๋าเก็บของให้ตัวละคร (บังคับอยู่ในช่วง 18 - 30 ช่อง)
        /// </summary>
        public void SetMaxSlots(int slots)
        {
            maxSlots = Mathf.Clamp(slots, 18, 30);
            Debug.Log($"[PlayerInventory] Max inventory slots configured to: {maxSlots}");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// ตรวจสอบว่าในกระเป๋ามีไอเทมชนิดนี้อยู่แล้วหรือไม่ (Case-insensitive)
        /// </summary>
        public bool HasItem(string itemName)
        {
            return GetItemCount(itemName) > 0;
        }

        /// <summary>
        /// แปลงชื่อไอเทมทั่วไป เช่น "Ammo" ให้เป็นชนิดกระสุนที่ตรงกับอาวุธของผู้เล่น
        /// </summary>
        public string ResolveItemName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return string.Empty;
            if (string.Equals(itemName, "Ammo", System.StringComparison.OrdinalIgnoreCase))
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && player.CurrentWeapon != null)
                {
                    string lowerName = (player.CurrentWeapon.weaponName ?? "").ToLower().Trim();
                    if (lowerName.Contains("rifl")) return "Rifle Ammo";
                    else if (lowerName.Contains("shot")) return "Shotgun Ammo";
                }
                return "Pistol Ammo";
            }
            return itemName.Trim();
        }

        /// <summary>
        /// ตรวจสอบว่าสามารถเก็บไอเทมชนิดนี้ลงกระเป๋าได้หรือไม่ ตามกฎ Max Stack Size:
        /// - กระสุน (Ammo): stack ได้สูงสุด 9999
        /// - อาวุธ (Weapons): stack ได้ 1 ชิ้นต่อ 1 ช่อง
        /// - ของอื่นๆ เช่น อาหาร, ยา, วัตถุดิบ: stack ได้ 99 ชิ้นต่อ 1 ช่อง
        /// </summary>
        public bool CanAddItem(string itemName, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0) return false;
            string resolved = ResolveItemName(itemName);
            int maxStack = GetMaxStackSize(resolved);

            if (maxStack <= 1)
            {
                // อาวุธ stack ได้ 1 ชิ้นต่อ 1 ช่อง
                return (slots.Count + quantity) <= maxSlots;
            }

            // ไอเทมที่ stack ได้: ตรวจสอบที่ว่างในช่องเดิมที่มีอยู่
            int remaining = quantity;
            string clean = resolved.Trim().ToLowerInvariant();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && !string.IsNullOrEmpty(slots[i].itemName) && slots[i].itemName.Trim().ToLowerInvariant() == clean)
                {
                    int space = maxStack - slots[i].quantity;
                    if (space > 0)
                    {
                        remaining -= space;
                        if (remaining <= 0) return true; // พอดีในช่องเดิม ไม่กินช่องใหม่
                    }
                }
            }

            // จำนวนช่องใหม่ที่ต้องใช้
            int newSlotsNeeded = Mathf.CeilToInt((float)remaining / maxStack);
            return (slots.Count + newSlotsNeeded) <= maxSlots;
        }

        public bool CanAddItem(string itemName) => CanAddItem(itemName, 1);

        public bool AddItem(string itemName, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0) return false;

            string resolved = ResolveItemName(itemName);
            int maxStack = GetMaxStackSize(resolved);

            if (!CanAddItem(resolved, quantity))
            {
                Debug.LogWarning($"[PlayerInventory] Cannot collect '{resolved}' x{quantity}: Inventory is full ({UsedSlots}/{maxSlots} slots used)!");
                return false;
            }

            int remaining = quantity;
            string clean = resolved.Trim().ToLowerInvariant();

            // 1. เติมลงในช่องเดิมที่มีอยู่ก่อน ถ้ายังไม่เต็ม MaxStack
            if (maxStack > 1)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && !string.IsNullOrEmpty(slots[i].itemName) && slots[i].itemName.Trim().ToLowerInvariant() == clean)
                    {
                        int space = maxStack - slots[i].quantity;
                        if (space > 0)
                        {
                            int toAdd = Mathf.Min(space, remaining);
                            slots[i].quantity += toAdd;
                            remaining -= toAdd;
                            if (remaining <= 0) break;
                        }
                    }
                }
            }

            // 2. ถ้ายังมีเหลือ ให้เปิดช่องใหม่ทีละก้อนตาม MaxStack
            while (remaining > 0)
            {
                int toAdd = Mathf.Min(remaining, maxStack);
                slots.Add(new InventorySlot(resolved, toAdd));
                remaining -= toAdd;
            }

            SyncItemsFlatList();
            Debug.Log($"[PlayerInventory] Picked up item: {resolved} x{quantity}! Slots used: {UsedSlots}/{maxSlots}");
            OnItemCollected?.Invoke(resolved);
            if (!_isInitializingStartingLoadout && !SuppressNotificationEvents)
            {
                OnItemAdded?.Invoke(resolved, quantity);
            }
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(string itemName, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0 || slots == null) return false;
            if (GetItemCount(itemName) < quantity) return false;

            string clean = itemName.Trim().ToLowerInvariant();
            int needed = quantity;

            // หักออกจากช่องท้ายสุดก่อน เพื่อให้ช่องที่มีอยู่เรียงตัวกระชับ
            for (int i = slots.Count - 1; i >= 0 && needed > 0; i--)
            {
                if (slots[i] != null && !string.IsNullOrEmpty(slots[i].itemName) && slots[i].itemName.Trim().ToLowerInvariant() == clean)
                {
                    int take = Mathf.Min(slots[i].quantity, needed);
                    slots[i].quantity -= take;
                    needed -= take;

                    if (slots[i].quantity <= 0)
                    {
                        slots.RemoveAt(i);
                    }
                }
            }

            SyncItemsFlatList();
            Debug.Log($"[PlayerInventory] Removed item: {itemName} x{quantity}! Slots remaining: {slots.Count}");
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(string itemName) => RemoveItem(itemName, 1);

        public int GetItemCount(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || slots == null) return 0;
            string clean = itemName.Trim().ToLowerInvariant();
            int total = 0;
            foreach (var slot in slots)
            {
                if (slot != null && !string.IsNullOrEmpty(slot.itemName) && slot.itemName.Trim().ToLowerInvariant() == clean)
                {
                    total += slot.quantity;
                }
            }
            return total;
        }

        private void SyncItemsFlatList()
        {
            if (items == null) items = new List<string>();
            items.Clear();
            foreach (var s in slots)
            {
                if (s != null && !string.IsNullOrEmpty(s.itemName))
                {
                    items.Add(s.itemName);
                }
            }
        }

        public List<KeyValuePair<string, int>> GetSlotPairs()
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
            foreach (var s in slots)
            {
                if (s != null && !string.IsNullOrEmpty(s.itemName) && s.quantity > 0)
                {
                    list.Add(new KeyValuePair<string, int>(s.itemName, s.quantity));
                }
            }
            return list;
        }

        public bool UseItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return false;

            ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
            if (itemData == null)
            {
                Debug.LogWarning($"[PlayerInventory] Item '{itemName}' not found in ItemDatabase!");
                return false;
            }

            if (itemData.type == ItemType.RangedWeapon || itemData.type == ItemType.ThrowingWeapon)
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && !player.PlayerHealth.IsDead)
                {
                    string cleanItem = itemName.Trim().ToLower();
                    int idx = player.WeaponsList.FindIndex(w => 
                    {
                        if (string.IsNullOrEmpty(w.weaponName)) return false;
                        string cleanWName = w.weaponName.Trim().ToLower();
                        return cleanWName.Contains(cleanItem) || cleanItem.Contains(cleanWName);
                    });
                    if (idx < 0)
                    {
                        // Dynamically add the weapon
                        Weapon newWeapon = new Weapon(itemData);
                        player.WeaponsList.Add(newWeapon);
                        idx = player.WeaponsList.Count - 1;
                        Debug.Log($"[PlayerInventory] Dynamically added weapon '{itemName}' to loadout.");
                    }

                    if (player.CurrentWeapon != null && player.CurrentWeaponName != null &&
                        player.WeaponsList[idx].weaponName.Equals(player.CurrentWeaponName, StringComparison.OrdinalIgnoreCase))
                    {
                        player.SwitchToWeapon(-1); // Unequip
                    }
                    else
                    {
                        player.SwitchToWeapon(idx);
                    }
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
            else if (itemData.type == ItemType.MeleeWeapon)
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && !player.PlayerHealth.IsDead)
                {
                    string cleanItem = itemName.Trim().ToLower();
                    int idx = player.MeleeWeaponsList.FindIndex(w => 
                    {
                        if (string.IsNullOrEmpty(w.weaponName)) return false;
                        string cleanWName = w.weaponName.Trim().ToLower();
                        return cleanWName.Contains(cleanItem) || cleanItem.Contains(cleanWName);
                    });
                    if (idx < 0)
                    {
                        // Dynamically add the melee weapon
                        MeleeWeapon newMelee = new MeleeWeapon(itemData);
                        player.MeleeWeaponsList.Add(newMelee);
                        idx = player.MeleeWeaponsList.Count - 1;
                        Debug.Log($"[PlayerInventory] Dynamically added melee weapon '{itemName}' to loadout.");
                    }

                    if (player.CurrentMeleeWeapon != null && player.CurrentMeleeWeaponName != null &&
                        player.MeleeWeaponsList[idx].weaponName.Equals(player.CurrentMeleeWeaponName, StringComparison.OrdinalIgnoreCase))
                    {
                        player.SwitchToMeleeWeapon(-1); // Unequip
                    }
                    else
                    {
                        player.SwitchToMeleeWeapon(idx);
                    }
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
            else if (itemData.type == ItemType.Potion ||
                     itemData.type == ItemType.Bread ||
                     itemData.type == ItemType.Water ||
                     itemData.restorationValue > 0f ||
                     itemData.waterRestorationValue > 0f ||
                     itemData.sanityRestorationValue > 0f)
            {
                Health health = GetComponent<Health>();
                PlayerController player = GetComponent<PlayerController>();
                if (health != null && health.IsDead) return false;
                if (player != null && player.PlayerHealth != null && player.PlayerHealth.IsDead) return false;

                string lowerName = (!string.IsNullOrEmpty(itemData.itemName) ? itemData.itemName : itemName).ToLowerInvariant();
                bool isCoffee = lowerName.Contains("coffee");
                bool isTea = lowerName.Contains("tea");

                float healAmount = 0f;
                float hungerAmount = 0f;
                float waterAmount = itemData.waterRestorationValue;
                float sanityAmount = itemData.sanityRestorationValue;

                if (itemData.type == ItemType.Potion && !isCoffee && !isTea)
                {
                    if (itemData.restorationValue > 0f)
                    {
                        healAmount = itemData.restorationValue;
                    }
                    else if (sanityAmount <= 0f && waterAmount <= 0f)
                    {
                        healAmount = 100f;
                    }
                }
                else if (itemData.type == ItemType.Bread || isCoffee || isTea || (itemData.type == ItemType.Water && itemData.restorationValue > 0f))
                {
                    if (itemData.restorationValue > 0f)
                    {
                        hungerAmount = itemData.restorationValue;
                    }
                    else if (itemData.type == ItemType.Bread && !isCoffee && !isTea)
                    {
                        hungerAmount = 25f;
                    }
                }

                if (itemData.type == ItemType.Water && waterAmount <= 0f)
                {
                    waterAmount = 40f;
                }

                // Ensure Coffee and Tea always restore Water, Hunger, and Sanity even if asset wasn't reloaded yet
                if (isCoffee)
                {
                    if (waterAmount <= 0f) waterAmount = 30f;
                    if (sanityAmount <= 0f) sanityAmount = 35f;
                    if (hungerAmount <= 0f) hungerAmount = 10f;
                }
                else if (isTea)
                {
                    if (waterAmount <= 0f) waterAmount = 35f;
                    if (sanityAmount <= 0f) sanityAmount = 25f;
                    if (hungerAmount <= 0f) hungerAmount = 15f;
                }

                bool canHeal = healAmount > 0f && health != null && health.CurrentHealth < health.MaxHealth;
                bool canEat = hungerAmount > 0f && player != null && player.CurrentHunger < player.MaxHunger;
                bool canDrink = waterAmount > 0f && player != null && player.CurrentWater < player.MaxWater;
                bool canRestoreSanity = sanityAmount > 0f && player != null && player.CurrentSanity < player.MaxSanity;

                // Pure medkits require missing HP; food/drinks/sanity items can be consumed whenever any restored stat is below max (or always for Coffee/Tea/Drinks/Food)
                bool isPureMedkit = healAmount > 0f && hungerAmount <= 0f && waterAmount <= 0f && sanityAmount <= 0f;
                bool canConsume = isPureMedkit ? canHeal : (canHeal || canEat || canDrink || canRestoreSanity || isCoffee || isTea || waterAmount > 0f || hungerAmount > 0f);

                if (canConsume)
                {
                    if (RemoveItem(itemName))
                    {
                        if (healAmount > 0f && health != null)
                        {
                            health.Heal(healAmount);
                        }
                        if (hungerAmount > 0f && player != null)
                        {
                            player.EatBread(hungerAmount);
                        }
                        if (waterAmount > 0f && player != null)
                        {
                            player.DrinkWater(waterAmount);
                        }
                        if (sanityAmount > 0f && player != null)
                        {
                            player.RestoreSanity(sanityAmount);
                        }
                        Debug.Log($"[PlayerInventory] Consumed '{itemName}'! (+HP:{healAmount}, +Hunger:{hungerAmount}, +Water:{waterAmount}, +Sanity:{sanityAmount})");
                        return true;
                    }
                }
                else
                {
                    Debug.Log($"[PlayerInventory] Cannot use '{itemName}': stats are already full.");
                }
            }

            return false;
        }

        public void ClearInventory()
        {
            if (slots != null) slots.Clear();
            if (items != null) items.Clear();
            money = 0;
            OnInventoryChanged?.Invoke();
        }

        public Dictionary<string, int> GetItemQuantities()
        {
            Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (slot == null || string.IsNullOrEmpty(slot.itemName) || slot.quantity <= 0) continue;
                    if (quantities.ContainsKey(slot.itemName))
                        quantities[slot.itemName] += slot.quantity;
                    else
                        quantities[slot.itemName] = slot.quantity;
                }
            }
            return quantities;
        }
    }
}
