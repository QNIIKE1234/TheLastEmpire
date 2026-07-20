using UnityEngine;
using System.Collections.Generic;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "TheLastEmpire/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        private static ItemDatabase _instance;
        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Attempt to load from Resources
                    _instance = Resources.Load<ItemDatabase>("ItemDatabase");
                    if (_instance == null)
                    {
                        // Fallback search in memory/database
                        ItemDatabase[] databases = Resources.FindObjectsOfTypeAll<ItemDatabase>();
                        if (databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("Global Database Registry")]
        public List<ItemData> allItems = new List<ItemData>();

        /// <summary>
        /// Retrieves an ItemData definition by name (case-insensitive and trimmed).
        /// </summary>
        public ItemData GetItemByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string trimmed = name.Trim();
            foreach (ItemData item in allItems)
            {
                if (item != null && string.Equals(item.itemName.Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
            return null;
        }
    }
}
