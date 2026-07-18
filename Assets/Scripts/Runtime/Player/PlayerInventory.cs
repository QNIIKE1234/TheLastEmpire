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

        public void AddMoney(int amount)
        {
            money += amount;
            Debug.Log($"[PlayerInventory] Earned ${amount}! Total Wallet: ${money}");
            OnMoneyChanged?.Invoke(money);
            OnInventoryChanged?.Invoke();
        }

        public void AddItem(string itemName)
        {
            items.Add(itemName);
            Debug.Log($"[PlayerInventory] Picked up item: {itemName}! Inventory size: {items.Count}");
            OnItemCollected?.Invoke(itemName);
            OnInventoryChanged?.Invoke();
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
