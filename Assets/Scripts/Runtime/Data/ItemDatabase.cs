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
                    // Attempt to load from Addressables (exact path) with fallback
                    _instance = AddressableManager.LoadAsset<ItemDatabase>("Database/ItemDatabase");
                    if (_instance == null)
                    {
                        _instance = AddressableManager.LoadAsset<ItemDatabase>("ItemDatabase");
                    }
                    if (_instance == null)
                    {
                        // Fallback: search anywhere in Resources folders
                        ItemDatabase[] databases = Resources.LoadAll<ItemDatabase>("");
                        if (databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                    if (_instance == null)
                    {
                        // Fallback search in memory
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

        private static readonly Dictionary<string, string> ItemAliases = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Medium Ammo", "Rifle Ammo" },
            { "Rifle Ammo", "Medium Ammo" },
            { "Light Ammo", "Pistol Ammo" },
            { "Pistol Ammo", "Light Ammo" },
            { "Shotgun Ammo", "Ammo" },
            { "Ammo", "Shotgun Ammo" },
            { "Heavy Ammo", "Fuel Ammo" },
            { "Fuel Ammo", "Heavy Ammo" },
            { "Crossbow Bolt", "Bolts" },
            { "Bolt", "Bolts" },
            { "Water", "Water Bottle" },
            { "Bottled Water", "Water Bottle" },
            { "Clean Water", "Water Bottle" },
            { "ITEM0091_WaterBottle", "Water Bottle" }
        };

        /// <summary>
        /// Retrieves an ItemData definition by name (case-insensitive, trimmed, and alias-aware).
        /// </summary>
        public ItemData GetItemByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string trimmed = name.Trim();

            // 1. Direct search
            ItemData item = FindItemInternal(trimmed);
            if (item != null) return item;

            // 2. Alias search (e.g. "Medium Ammo" -> "Rifle Ammo", "Light Ammo" -> "Pistol Ammo")
            if (ItemAliases.TryGetValue(trimmed, out string alias) && !string.IsNullOrEmpty(alias))
            {
                item = FindItemInternal(alias);
                if (item != null) return item;
            }

            return null;
        }

        private ItemData FindItemInternal(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return null;
            string cleanTarget = targetName.Replace(" ", "").Replace("_", "").ToLowerInvariant();

            foreach (ItemData item in allItems)
            {
                if (item == null) continue;

                // Match explicit itemName
                if (!string.IsNullOrEmpty(item.itemName))
                {
                    if (string.Equals(item.itemName.Trim(), targetName, System.StringComparison.OrdinalIgnoreCase))
                        return item;

                    string cleanItemName = item.itemName.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                    if (cleanItemName == cleanTarget)
                        return item;
                }

                // Match asset name (e.g. ITEM0011_RifleAmmo)
                if (item.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return item;

                string cleanAssetName = item.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (cleanAssetName.Contains(cleanTarget) || cleanTarget.Contains(cleanAssetName))
                    return item;
            }

            // Fallback dynamic load: recursively load all ItemData assets from Addressables or Resources
            IList<ItemData> allInResources = AddressableManager.LoadAssetsByLabel<ItemData>("ItemData");
            if (allInResources == null || allInResources.Count == 0)
            {
                allInResources = Resources.LoadAll<ItemData>("");
            }

            if (allInResources != null)
            {
                foreach (ItemData item in allInResources)
                {
                    if (item == null) continue;

                    if (!string.IsNullOrEmpty(item.itemName))
                    {
                        if (string.Equals(item.itemName.Trim(), targetName, System.StringComparison.OrdinalIgnoreCase))
                            return item;

                        string cleanItemName = item.itemName.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                        if (cleanItemName == cleanTarget)
                            return item;
                    }

                    if (item.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return item;
                }
            }

            return null;
        }
    }
}

