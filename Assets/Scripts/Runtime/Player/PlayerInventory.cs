using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Inventory Status")]
        [SerializeField] private int money = 0;
        [SerializeField] private List<string> items = new List<string>();
        [Header("Starting Loadout")]
        [SerializeField] private PlayerConfigSO playerConfig;
        
        [Tooltip("รายการไอเทมที่จะได้รับตอนเริ่มเกม (ถ้าไม่ใช้ Config)")]
        [SerializeField] private List<string> defaultStartingItems = new List<string> { "Pistol", "Knife" };
        [SerializeField] private int defaultPistolAmmo = 60;

        public int Money => money;
        public List<string> Items => items;

        public event System.Action<int> OnMoneyChanged;
        public event System.Action<string> OnItemCollected;
        public event System.Action OnInventoryChanged;

        private void Start()
        {
            if (items == null)
            {
                items = new List<string>();
            }
            
            // ล้างค่า empty string ที่อาจเผลอกด + ค้างไว้ใน Inspector ออกก่อน
            items.RemoveAll(string.IsNullOrWhiteSpace);

            if (items.Count == 0)
            {
                // ดึงจากค่า PlayerConfigSO ถ้ามีการเชื่อมไว้
                List<string> startItems = playerConfig != null ? playerConfig.startingItems : defaultStartingItems;
                int startAmmo = playerConfig != null ? playerConfig.startingPistolAmmo : defaultPistolAmmo;
                
                if (playerConfig != null) money = playerConfig.startingMoney;

                foreach (string item in startItems)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                        items.Add(item.Trim());
                }
                
                // กระสุนปืนพกเริ่มต้น
                for (int i = 0; i < startAmmo; i++)
                {
                    items.Add("Pistol Ammo");
                }
            }
        }

        public void AddMoney(int amount)
        {
            money += amount;
            Debug.Log($"[PlayerInventory] Earned ${amount}! Total Wallet: ${money}");
            OnMoneyChanged?.Invoke(money);
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


        public void AddItem(string itemName, int quantity = 1)
        {
            string finalName = itemName;
            if (string.Equals(itemName, "Ammo", System.StringComparison.OrdinalIgnoreCase))
            {
                // Auto-resolve generic ammo into weapon specific ammo
                finalName = "Pistol Ammo"; // default fallback
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && player.CurrentWeapon != null)
                {
                    string lowerName = (player.CurrentWeapon.weaponName ?? "").ToLower().Trim();
                    if (lowerName.Contains("rifl")) finalName = "Rifle Ammo";
                    else if (lowerName.Contains("shot")) finalName = "Shotgun Ammo";
                }
            }

            for (int i = 0; i < quantity; i++)
            {
                items.Add(finalName);
            }
            Debug.Log($"[PlayerInventory] Picked up item: {finalName} x{quantity}! Inventory size: {items.Count}");
            OnItemCollected?.Invoke(finalName);
            OnInventoryChanged?.Invoke();
        }

        public bool RemoveItem(string itemName, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemName) || items == null) return false;

            int currentCount = GetItemCount(itemName);
            if (currentCount < quantity) return false;

            string cleanTarget = itemName.Trim().ToLower();
            int removed = 0;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] != null && items[i].Trim().ToLower() == cleanTarget)
                {
                    items.RemoveAt(i);
                    removed++;
                    if (removed >= quantity) break;
                }
            }

            Debug.Log($"[PlayerInventory] Removed item: {itemName} x{removed}! Inventory size: {items.Count}");
            OnInventoryChanged?.Invoke();
            return true;
        }

        public int GetItemCount(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || items == null) return 0;
            string cleanTarget = itemName.Trim().ToLower();
            int count = 0;
            foreach (string item in items)
            {
                if (item != null && item.Trim().ToLower() == cleanTarget)
                {
                    count++;
                }
            }
            return count;
        }

        public bool RemoveItem(string itemName)
        {
            if (items.Contains(itemName))
            {
                items.Remove(itemName);
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
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
            else if (itemData.type == ItemType.Potion)
            {
                Health health = GetComponent<Health>();
                PlayerController player = GetComponent<PlayerController>();
                bool canHeal = health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth;
                bool canRestoreSanity = player != null && itemData.sanityRestorationValue > 0f && player.CurrentSanity < player.MaxSanity;

                if (canHeal || canRestoreSanity)
                {
                    if (RemoveItem(itemName))
                    {
                        if (canHeal)
                        {
                            float healAmount = itemData.restorationValue > 0 ? itemData.restorationValue : 100f;
                            health.Heal(healAmount);
                        }
                        if (itemData.sanityRestorationValue > 0f && player != null)
                        {
                            player.RestoreSanity(itemData.sanityRestorationValue);
                        }
                        Debug.Log($"[PlayerInventory] Used {itemName}! HP: {health?.CurrentHealth}, Sanity: {player?.CurrentSanity}");
                        return true;
                    }
                }
                else
                {
                    Debug.Log("[PlayerInventory] Health and Sanity are already full or player is dead.");
                }
            }
            else if (itemData.type == ItemType.Bread)
            {
                PlayerController player = GetComponent<PlayerController>();
                bool canEat = player != null && !player.PlayerHealth.IsDead && player.CurrentHunger < player.MaxHunger;
                bool canRestoreSanity = player != null && itemData.sanityRestorationValue > 0f && player.CurrentSanity < player.MaxSanity;

                if (canEat || canRestoreSanity)
                {
                    if (RemoveItem(itemName))
                    {
                        if (canEat)
                        {
                            float restoreAmount = itemData.restorationValue > 0 ? itemData.restorationValue : 25f;
                            player.EatBread(restoreAmount);
                        }
                        if (itemData.sanityRestorationValue > 0f)
                        {
                            player.RestoreSanity(itemData.sanityRestorationValue);
                        }
                        Debug.Log($"[PlayerInventory] Consumed {itemName}! Hunger: {player.CurrentHunger}, Sanity: {player.CurrentSanity}");
                        return true;
                    }
                }
                else
                {
                    Debug.Log("[PlayerInventory] Hunger and Sanity are already full or player is dead.");
                }
            }
            else if (itemData.sanityRestorationValue > 0f)
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && !player.PlayerHealth.IsDead && player.CurrentSanity < player.MaxSanity)
                {
                    if (RemoveItem(itemName))
                    {
                        player.RestoreSanity(itemData.sanityRestorationValue);
                        Debug.Log($"[PlayerInventory] Used {itemName}! Restored {itemData.sanityRestorationValue} Sanity. Current: {player.CurrentSanity}");
                        return true;
                    }
                }
                else
                {
                    Debug.Log("[PlayerInventory] Sanity is already full or player is dead.");
                }
            }

            return false;
        }

        public void ClearInventory()
        {
            items.Clear();
            money = 0;
            OnInventoryChanged?.Invoke();
        }

        public Dictionary<string, int> GetItemQuantities()
        {
            Dictionary<string, int> quantities = new Dictionary<string, int>();
            foreach (string item in items)
            {
                if (string.IsNullOrEmpty(item)) continue;
                if (quantities.ContainsKey(item))
                {
                    quantities[item]++;
                }
                else
                {
                    quantities[item] = 1;
                }
            }
            return quantities;
        }
    }
}
