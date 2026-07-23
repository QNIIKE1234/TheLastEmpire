using UnityEngine;

namespace TheLastEmpire
{
    public enum NPCType
    {
        Shop,
        QuestGiver,
        StoryNPC
    }

    [System.Serializable]
    public struct ShopItemConfig
    {
        public ItemData item;
        public int price;
    }

    public class NPCController : MonoBehaviour
    {
        [Header("NPC Configuration")]
        [SerializeField] private NPCType npcType = NPCType.Shop;
        [SerializeField] private float interactRange = 2.0f;

        [Header("Merchant Inventory Settings")]
        [SerializeField] private System.Collections.Generic.List<ShopItemConfig> shopItems = new System.Collections.Generic.List<ShopItemConfig>();
        [SerializeField] private bool isFree = false;

        public System.Collections.Generic.List<ShopItemConfig> ShopItems => shopItems;
        public NPCType NpcType => npcType;

        private Transform _playerTransform;

        private void Start()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }

            // Populate default merchant items if none are set
            if (npcType == NPCType.Shop && (shopItems == null || shopItems.Count == 0))
            {
                PopulateDefaultShopItems();
            }
        }

        private void PopulateDefaultShopItems()
        {
            if (shopItems == null)
            {
                shopItems = new System.Collections.Generic.List<ShopItemConfig>();
            }

            if (ItemDatabase.Instance != null)
            {
                AddDefaultShopItem("Rifle");
                AddDefaultShopItem("Shotgun");
                AddDefaultShopItem("Potion");
                AddDefaultShopItem("Bread");
                AddDefaultShopItem("Pistol Ammo");
                AddDefaultShopItem("Shotgun Ammo");
                AddDefaultShopItem("Rifle Ammo");
                AddDefaultShopItem("Knife");
                AddDefaultShopItem("Baseball Bat");
                AddDefaultShopItem("Machete");
            }
        }

        private void AddDefaultShopItem(string name, int price = -1)
        {
            ItemData data = ItemDatabase.Instance.GetItemByName(name);
            if (data != null)
            {
                int finalPrice = price;
                if (finalPrice < 0)
                {
                    finalPrice = ShopDatabase.Instance != null ? ShopDatabase.Instance.GetDefaultPrice(name) : 10;
                }
                shopItems.Add(new ShopItemConfig { item = data, price = finalPrice });
            }
        }

        public bool IsPlayerInRange(Vector3 playerPos)
        {
            return Vector3.Distance(transform.position, playerPos) <= interactRange;
        }

        public void SetNPCType(NPCType type)
        {
            npcType = type;
        }

        public void SetShopItems(System.Collections.Generic.List<ShopItemConfig> items)
        {
            shopItems = items;
        }

        public void Interact()
        {
            switch (npcType)
            {
                case NPCType.Shop:
                    if (ShopUI.Instance != null)
                    {
                        ShopUI.Instance.OpenShopMenu(shopItems, isFree);
                    }
                    else
                    {
                        Debug.LogWarning("[NPCController] ShopUI instance is null in the scene!");
                    }
                    break;
                default:
                    Debug.Log($"[NPCController] Interacted with NPC of type {npcType}, but no behavior is defined yet.");
                    break;
            }
        }
    }
}
