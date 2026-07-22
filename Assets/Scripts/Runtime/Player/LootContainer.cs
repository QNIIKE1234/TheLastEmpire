using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [System.Serializable]
    public struct LootSlot
    {
        public string itemName;
        public int quantity;

        public LootSlot(string name, int qty)
        {
            itemName = name;
            quantity = qty;
        }
    }

    [System.Serializable]
    public struct RandomLootEntry
    {
        public string itemName;
        [Tooltip("Minimum quantity spawned if this item rolls successfully.")]
        public int minQuantity;
        [Tooltip("Maximum quantity spawned if this item rolls successfully.")]
        public int maxQuantity;
        [Range(0f, 1f)]
        [Tooltip("Drop probability (0.0 = 0%, 1.0 = 100%).")]
        public float dropProbability;

        public RandomLootEntry(string name, int min, int max, float prob)
        {
            itemName = name;
            minQuantity = min;
            maxQuantity = max;
            dropProbability = prob;
        }
    }

    public class LootContainer : MonoBehaviour
    {
        [Header("Loot Container Details")]
        [Tooltip("Name of the container displayed in the UI (e.g. Wooden Crate, Locker, Dead Corpse).")]
        public string containerName = "Wood Crate";
        
        [Tooltip("The amount of money contained inside (overridden if randomizeLoot is enabled).")]
        public int moneyAmount = 0;

        [Tooltip("Items inside this container (overridden if randomizeLoot is enabled).")]
        public List<LootSlot> itemsInside = new List<LootSlot>();

        [Header("Random Loot Configuration")]
        [Tooltip("If checked, moneyAmount and itemsInside will be randomized at startup using possibleLoot settings.")]
        public bool randomizeLoot = false;

        [Tooltip("Minimum random cash spawned.")]
        public int minRandomCash = 0;
        
        [Tooltip("Maximum random cash spawned.")]
        public int maxRandomCash = 30;

        [Tooltip("The list of possible items that can drop from this container.")]
        public List<RandomLootEntry> possibleLoot = new List<RandomLootEntry>();

        [Header("Status")]
        public bool isSearched = false;

        [Header("Interaction Radius")]
        public float interactionRadius = 2.0f;

        private void Start()
        {
            if (randomizeLoot)
            {
                // Clear any manual editor items to prevent duplication
                itemsInside.Clear();
                moneyAmount = 0;

                // Setup deterministic seed using map stage seed + coordinate hash to keep rolls consistent on re-entry
                int seed = 0;
                if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
                {
                    int x = WorldMapManager.Instance.CurrentPlayerX;
                    int y = WorldMapManager.Instance.CurrentPlayerY;
                    StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
                    if (stage != null)
                    {
                        seed = stage.stageSeed;
                    }
                }

                Random.State oldState = Random.state;
                // Add unique coordinate offset so different containers in the same stage get different rolls
                int positionHash = Mathf.RoundToInt(transform.position.x * 100f) ^ Mathf.RoundToInt(transform.position.z * 100f);
                Random.InitState(seed + positionHash + 777);

                // 1. Roll cash
                moneyAmount = Random.Range(minRandomCash, maxRandomCash + 1);

                // 2. Roll items
                foreach (RandomLootEntry entry in possibleLoot)
                {
                    if (string.IsNullOrEmpty(entry.itemName)) continue;

                    if (Random.value <= entry.dropProbability)
                    {
                        int qty = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                        if (qty > 0)
                        {
                            itemsInside.Add(new LootSlot(entry.itemName, qty));
                        }
                    }
                }

                Random.state = oldState;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw a yellow sphere indicating the interactable range in the editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
