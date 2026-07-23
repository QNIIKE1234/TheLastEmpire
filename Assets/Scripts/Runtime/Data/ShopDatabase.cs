using UnityEngine;
using System.Collections.Generic;

namespace TheLastEmpire
{
    [System.Serializable]
    public struct ShopPriceEntry
    {
        public string itemName;
        public int defaultPrice;
    }

    [CreateAssetMenu(fileName = "ShopDatabase", menuName = "TheLastEmpire/Shop Database")]
    public class ShopDatabase : ScriptableObject
    {
        private static ShopDatabase _instance;
        public static ShopDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ShopDatabase>("ShopDatabase");
                    if (_instance == null)
                    {
                        ShopDatabase[] databases = Resources.FindObjectsOfTypeAll<ShopDatabase>();
                        if (databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                }
                return _instance;
            }
        }

        public List<ShopPriceEntry> itemPrices = new List<ShopPriceEntry>();

        public int GetDefaultPrice(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return 0;
            string clean = itemName.Trim().ToLower();
            foreach (var entry in itemPrices)
            {
                if (entry.itemName != null && entry.itemName.Trim().ToLower() == clean)
                {
                    return entry.defaultPrice;
                }
            }
            return 0; // Free / unpriced fallback
        }
    }
}
