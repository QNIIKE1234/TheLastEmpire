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

        public void AddMoney(int amount)
        {
            money += amount;
            Debug.Log($"[PlayerInventory] Earned ${amount}! Total Wallet: ${money}");
            OnMoneyChanged?.Invoke(money);
        }

        public void AddItem(string itemName)
        {
            items.Add(itemName);
            Debug.Log($"[PlayerInventory] Picked up item: {itemName}! Inventory size: {items.Count}");
            OnItemCollected?.Invoke(itemName);
        }

        public bool RemoveItem(string itemName)
        {
            if (items.Contains(itemName))
            {
                items.Remove(itemName);
                return true;
            }
            return false;
        }

        public void ClearInventory()
        {
            items.Clear();
            money = 0;
        }
    }
}
