using UnityEngine;

namespace TheLastEmpire
{
    public class LootDropper : MonoBehaviour
    {
        [Header("Drop Prefabs")]
        [SerializeField] private CollectibleItem itemPrefab;
        [SerializeField] private CollectibleItem moneyPrefab;

        [Header("Drop Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float itemDropChance = 0.5f;
        [SerializeField] private bool canDropMoney = false;
        [Range(0f, 1f)]
        [SerializeField] private float moneyDropChance = 0.8f;

        [Header("Money Bounds")]
        [SerializeField] private int minMoney = 5;
        [SerializeField] private int maxMoney = 20;

        [Header("Boss Configs")]
        [SerializeField] private bool isBoss = false;

        public bool IsBoss { get => isBoss; set => isBoss = value; }

        private Health _health;

        private void Start()
        {
            _health = GetComponent<Health>();
            if (_health != null)
            {
                _health.onDeath.AddListener(DropLoot);
            }

            // Enforce: Only Zombies drop money. Check if this is a Zombie.
            ZombieAI zombieCheck = GetComponent<ZombieAI>();
            canDropMoney = (zombieCheck != null);
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
                // 1. Roll for Item Drop (Guaranteed Ammo, Potion, or ETC on kill)
                if (itemPrefab != null)
                {
                    float p = Random.value;
                    string dropName;
                    int dropQty;

                    if (p < 0.35f)
                    {
                        dropName = "Pistol Ammo"; // default
                        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                        if (player != null && player.CurrentWeapon != null)
                        {
                            string lowerName = (player.CurrentWeapon.weaponName ?? "").ToLower().Trim();
                            if (lowerName.Contains("rifl")) dropName = "Rifle Ammo";
                            else if (lowerName.Contains("shot")) dropName = "Shotgun Ammo";
                        }
                        dropQty = Random.Range(3, 8); // 3 to 7
                    }
                    else if (p < 0.70f)
                    {
                        dropName = "Potion";
                        dropQty = Random.Range(1, 3); // 1 to 2
                    }
                    else
                    {
                        dropName = "ETC";
                        dropQty = Random.Range(1, 6); // 1 to 5
                    }

                    // Spread the physical loot drops around the boss's coordinate
                    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f);
                    CollectibleItem spawned = Instantiate(itemPrefab, transform.position + offset, Quaternion.identity);
                    if (spawned != null)
                    {
                        spawned.SetItemDetails(dropName, dropQty);
                    }
                }

                // 2. Roll for Money Drop (Zombies only!)
                if (canDropMoney && moneyPrefab != null && Random.value <= moneyDropChance)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f);
                    CollectibleItem spawnedMoney = Instantiate(moneyPrefab, transform.position + offset, Quaternion.identity);
                    if (spawnedMoney != null)
                    {
                        int moneyAmount = Random.Range(minMoney, maxMoney + 1);
                        spawnedMoney.SetMoneyDetails(moneyAmount);
                    }
                }

                // 3. Roll for Bread Drop (25% chance for all enemies)
                if (itemPrefab != null && Random.value <= 0.25f)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f);
                    CollectibleItem spawnedBread = Instantiate(itemPrefab, transform.position + offset, Quaternion.identity);
                    if (spawnedBread != null)
                    {
                        spawnedBread.SetItemDetails("Bread", 1);
                    }
                }
            }
        }
    }
}
