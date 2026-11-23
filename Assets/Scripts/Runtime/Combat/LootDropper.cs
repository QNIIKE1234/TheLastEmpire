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
            // ในโหมด Online Co-op ให้เฉพาะ Host เป็นคนสุ่มและเสกของดรอป (แล้ว CollectibleItem.Start() จะซิงค์ให้ Client ทุกเครื่องอัตโนมัติ)
            if (TheLastEmpire.Network.EmpireNetworkManager.IsOnlineClient)
            {
                return;
            }

            int dropCount = isBoss ? 5 : 1;

            for (int i = 0; i < dropCount; i++)
            {
                if (customLootTable != null && customLootTable.Count > 0)
                {
                    bool droppedAmmoBoxThisRoll = false;
                    foreach (var entry in customLootTable)
                    {
                        if (string.IsNullOrEmpty(entry.itemName)) continue;
                        if (Random.value <= entry.probability)
                        {
                            string dropItemName = entry.itemName;
                            int qty = Random.Range(entry.minQuantity, Mathf.Max(entry.minQuantity, entry.maxQuantity + 1));

                            // กล่องกระสุนที่ดรอปจากมอน: แปลงเป็นกล่องสุ่ม "Ammo" จำนวน 1-3 กล่อง (เปิดสุ่มกระสุน 30% ของแม็กตอนเก็บเข้าตัว)
                            if (PlayerInventory.IsAmmoDropItem(dropItemName))
                            {
                                if (droppedAmmoBoxThisRoll) continue;
                                droppedAmmoBoxThisRoll = true;
                                dropItemName = "Ammo";
                                qty = Random.Range(1, 4); // 1 - 3 กล่องแบบสุ่ม
                            }

                            Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0.1f, Random.Range(-0.5f, 0.5f));

                            // Special case for Money prefab vs standard Item prefab
                            CollectibleItem prefabToUse = (dropItemName.Trim().ToLower() == "money" && moneyPrefab != null) ? moneyPrefab : itemPrefab;
                            if (prefabToUse != null)
                            {
                                Vector3 dropPos = new Vector3(transform.position.x + offset.x, 0.4f, transform.position.z + offset.z);
                                CollectibleItem spawned = Instantiate(prefabToUse, dropPos, Quaternion.identity);
                                if (spawned != null)
                                {
                                    if (dropItemName.Trim().ToLower() == "money")
                                    {
                                        spawned.SetMoneyDetails(qty);
                                    }
                                    else
                                    {
                                        spawned.SetItemDetails(dropItemName, qty);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // ระบบสุ่มดรอป Weapon Artifact:
            // - บอส: โอกาส 50% (ภายใน 50%: Common 50% / Rare 30% / Legendary 20%)
            // - มอนทั่วไป: โอกาส 5% (ภายใน 5%: Common 80% / Rare 20%)
            if (itemPrefab != null)
            {
                ItemData rolledArtifact = WeaponArtifactManager.RollEnemyDropArtifact(isBoss);
                if (rolledArtifact != null && !string.IsNullOrEmpty(rolledArtifact.itemName))
                {
                    Vector3 artOffset = new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, Random.Range(-0.6f, 0.6f));
                    Vector3 artDropPos = new Vector3(transform.position.x + artOffset.x, 0.4f, transform.position.z + artOffset.z);
                    CollectibleItem artSpawned = Instantiate(itemPrefab, artDropPos, Quaternion.identity);
                    if (artSpawned != null)
                    {
                        artSpawned.SetItemDetails(rolledArtifact.itemName, 1);
                        ArtifactRarity artRarity = rolledArtifact.artifactConfig != null ? rolledArtifact.artifactConfig.rarity : ArtifactRarity.Common;
                        Debug.Log($"[LootDropper] {(isBoss ? "BOSS" : "Enemy")} dropped [{artRarity}] Artifact: {rolledArtifact.itemName}!");
                    }
                }
            }
        }
    }
}
