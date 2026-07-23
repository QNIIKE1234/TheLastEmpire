using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Inventory Status")]
        [SerializeField] private int money = 0;
        [SerializeField] private List<string> items = new List<string>();

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
            if (items.Count == 0)
            {
                items.Add("Pistol");
                items.Add("Knife");
            }
        }

        public void AddMoney(int amount)
        {
            money += amount;
            Debug.Log($"[PlayerInventory] Earned ${amount}! Total Wallet: ${money}");
            OnMoneyChanged?.Invoke(money);
            OnInventoryChanged?.Invoke();
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
            string cleanItem = (itemName ?? "").ToLower().Trim();
            bool isRangedWeapon = cleanItem.Contains("rifl") || cleanItem.Contains("shot") || cleanItem.Contains("pist");
            bool isMeleeWeapon = cleanItem.Contains("knife") || cleanItem.Contains("bat") || cleanItem.Contains("machete");

            if (isRangedWeapon)
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && !player.PlayerHealth.IsDead)
                {
                    int idx = player.WeaponsList.FindIndex(w => {
                        string wName = (w.weaponName ?? "").ToLower().Trim();
                        return wName.Contains(cleanItem) || cleanItem.Contains(wName) || (wName.Contains("pist") && cleanItem.Contains("pist"));
                    });
                    if (idx >= 0)
                    {
                        player.SwitchToWeapon(idx);
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerInventory] Weapon '{itemName}' is not defined on the player!");
                    }
                }
            }
            else if (isMeleeWeapon)
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && !player.PlayerHealth.IsDead)
                {
                    int idx = player.MeleeWeaponsList.FindIndex(w => {
                        string wName = (w.weaponName ?? "").ToLower().Trim();
                        return wName.Contains(cleanItem) || cleanItem.Contains(wName);
                    });
                    if (idx >= 0)
                    {
                        player.SwitchToMeleeWeapon(idx);
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerInventory] Melee weapon '{itemName}' is not defined on the player!");
                    }
                }
            }
            else if (itemName == "Potion")
            {
                Health health = GetComponent<Health>();
                if (health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth)
                {
                    if (RemoveItem(itemName))
                    {
                        health.Heal(100f);
                        Debug.Log($"[PlayerInventory] Used Potion! Healed 100 HP. Current Health: {health.CurrentHealth}");
                        return true;
                    }
                }
                else
                {
                    Debug.Log("[PlayerInventory] Health is already full or player is dead.");
                }
            }
            else if (itemName == "Bread")
            {
                PlayerController player = GetComponent<PlayerController>();
                if (player != null && !player.PlayerHealth.IsDead && player.CurrentHunger < player.MaxHunger)
                {
                    if (RemoveItem(itemName))
                    {
                        player.EatBread(25f);
                        return true;
                    }
                }
                else
                {
                    Debug.Log("[PlayerInventory] Hunger is already full or player is dead.");
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
