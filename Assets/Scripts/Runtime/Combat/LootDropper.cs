using UnityEngine;

namespace TheLastEmpire
{
    [System.Serializable]
    public struct CustomLootEntry
    {
        public string itemName;
        public int minQuantity;
        public int maxQuantity;
        [Range(0f, 1f)] public float probability;
    }

    public class LootDropper : MonoBehaviour
    {
        [Header("Drop Prefabs")]
        [SerializeField] private CollectibleItem itemPrefab;
        [SerializeField] private CollectibleItem moneyPrefab;

        [Header("Boss Configs")]
        [SerializeField] private bool isBoss = false;

        [Header("Custom Drop Table (Required)")]
        [SerializeField] private System.Collections.Generic.List<CustomLootEntry> customLootTable = new System.Collections.Generic.List<CustomLootEntry>();

        public bool IsBoss { get => isBoss; set => isBoss = value; }

        private Health _health;

        private void Start()
        {
            _health = GetComponent<Health>();
            if (_health != null)
            {
                _health.onDeath.AddListener(DropLoot);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.onDeath.RemoveListener(DropLoot);
            }
        }

        private void DropLoot()
        {
            int dropCount = isBoss ? 5 : 1;

            for (int i = 0; i < dropCount; i++)
            {
                if (customLootTable != null && customLootTable.Count > 0)
                {
                    foreach (var entry in customLootTable)
                    {
                        if (string.IsNullOrEmpty(entry.itemName)) continue;
                        if (Random.value <= entry.probability)
                        {
                            int qty = Random.Range(entry.minQuantity, Mathf.Max(entry.minQuantity, entry.maxQuantity + 1));
                            Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.1f, Random.Range(-0.5f, 0.5f));

                            // Special case for Money prefab vs standard Item prefab
                            CollectibleItem prefabToUse = (entry.itemName.Trim().ToLower() == "money" && moneyPrefab != null) ? moneyPrefab : itemPrefab;
                            if (prefabToUse != null)
                            {
                                CollectibleItem spawned = Instantiate(prefabToUse, transform.position + offset, Quaternion.identity);
                                if (spawned != null)
                                {
                                    if (entry.itemName.Trim().ToLower() == "money")
                                    {
                                        spawned.SetMoneyDetails(qty);
                                    }
                                    else
                                    {
                                        spawned.SetItemDetails(entry.itemName, qty);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
