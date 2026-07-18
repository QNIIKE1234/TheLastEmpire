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
            // 1. Roll for Item Drop
            if (itemPrefab != null && Random.value <= itemDropChance)
            {
                Instantiate(itemPrefab, transform.position, Quaternion.identity);
            }

            // 2. Roll for Money Drop (Zombies only!)
            if (canDropMoney && moneyPrefab != null && Random.value <= moneyDropChance)
            {
                CollectibleItem spawnedMoney = Instantiate(moneyPrefab, transform.position, Quaternion.identity);
                // Assign a randomized money value
                int moneyAmount = Random.Range(minMoney, maxMoney + 1);
                // We'll set the amount on the spawned money script
                // We should make sure the spawned money script is set to isMoney = true
            }
        }
    }
}
