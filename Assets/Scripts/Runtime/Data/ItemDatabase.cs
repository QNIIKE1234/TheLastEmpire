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
                if (item != null)
                {
                    // 1. Match by explicit itemName field in Inspector (exact match)
                    if (!string.IsNullOrEmpty(item.itemName) && string.Equals(item.itemName.Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                    
                    // 2. Fallback: Match if the asset file name contains the target name (e.g., "ITEM0001_Potion" contains "Potion")
                    if (item.name.IndexOf(trimmed, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return item;
                    }
                }
            }

            // 3. Fallback dynamic load from Resources/database
            ItemData loadedDirectly = Resources.Load<ItemData>("database/" + trimmed);
            if (loadedDirectly != null) return loadedDirectly;

            ItemData[] allInFolder = Resources.LoadAll<ItemData>("database");
            foreach (ItemData item in allInFolder)
            {
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(item.itemName) && string.Equals(item.itemName.Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                    if (item.name.IndexOf(trimmed, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return item;
                    }
                }
            }

            return null;
        }
    }
}

