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
        [SerializeField] private string npcName = "Merchant";
        [SerializeField] private NPCType npcType = NPCType.Shop;
        [SerializeField] private float interactRange = 2.0f;

        [Header("Story Settings")]
        [Tooltip("Dialogue ID to fetch from DialogueDatabase (e.g. 'Merchant_Intro'). If assigned, fetches from database.")]
        [SerializeField] private string dialogueId;
        [Tooltip("Direct reference to DialogueTreeSO asset (overrides dialogueId if assigned)")]
        [SerializeField] private TheLastEmpire.Story.DialogueTreeSO dialogueTree;
        [Tooltip("Character portrait sprite for dialogues (optional)")]
        [SerializeField] private Sprite npcPortrait;

        [Header("Merchant Inventory Settings")]
        [SerializeField] private System.Collections.Generic.List<ShopItemConfig> shopItems = new System.Collections.Generic.List<ShopItemConfig>();
        [SerializeField] private bool isFree = false;

        public System.Collections.Generic.List<ShopItemConfig> ShopItems => shopItems;
        public NPCType NpcType => npcType;
        public string NpcName => npcName;
        public string DialogueId => dialogueId;
        public Sprite NpcPortrait => npcPortrait;

        public TheLastEmpire.Story.DialogueTreeSO GetEffectiveDialogueTree()
        {
            if (dialogueTree != null) return dialogueTree;
            if (!string.IsNullOrEmpty(dialogueId) && TheLastEmpire.Story.DialogueDatabase.Instance != null)
            {
                return TheLastEmpire.Story.DialogueDatabase.Instance.GetDialogueTree(dialogueId);
            }
            return null;
        }

        public bool HasDialogue
        {
            get
            {
                var tree = GetEffectiveDialogueTree();
                return tree != null && tree.dialogueLines != null && tree.dialogueLines.Count > 0;
            }
        }
        public bool HasShop => npcType == NPCType.Shop || (shopItems != null && shopItems.Count > 0);

        private Transform _playerTransform;

        [SerializeField] private TMPro.TMP_Text nameTextHUD;

        private void Start()
        {
            // Auto-update the 3D floating text above the NPC if it exists
            TMPro.TMP_Text textToUpdate = nameTextHUD != null ? nameTextHUD : GetComponentInChildren<TMPro.TMP_Text>();
            if (textToUpdate != null)
            {
                textToUpdate.text = npcName;
            }

            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
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
                // Weapons - Ranged
                AddDefaultShopItem("Pistol");
                AddDefaultShopItem("Revolver");
                AddDefaultShopItem("SMG");
                AddDefaultShopItem("Shotgun");
                AddDefaultShopItem("Rifle");
                AddDefaultShopItem("Sniper Rifle");
                AddDefaultShopItem("Crossbow");
                AddDefaultShopItem("Flamethrower");
                AddDefaultShopItem("Rocket Launcher");

                // Weapons - Melee
                AddDefaultShopItem("Knife");
                AddDefaultShopItem("Baseball Bat");
                AddDefaultShopItem("Machete");
                AddDefaultShopItem("Fire Axe");
                AddDefaultShopItem("Katana");
                AddDefaultShopItem("Sledgehammer");
                AddDefaultShopItem("Survival Spear");
                AddDefaultShopItem("Stun Baton");
                AddDefaultShopItem("Chainsaw");

                // Explosives & Consumables
                AddDefaultShopItem("Grenade");
                AddDefaultShopItem("Potion");
                AddDefaultShopItem("Bread");

                // Ammunition
                AddDefaultShopItem("Pistol Ammo");
                AddDefaultShopItem("Shotgun Ammo");
                AddDefaultShopItem("Rifle Ammo");
                AddDefaultShopItem("Heavy Ammo");
                AddDefaultShopItem("Bolts");
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

        public void SetDialogueId(string id)
        {
            dialogueId = id;
        }

        public void Interact()
        {
            // If the NPC has a shop, open the shop menu with [E]
            if (HasShop)
            {
                if (ShopUI.Instance != null)
                {
                    ShopUI.Instance.OpenShopMenu(shopItems, isFree);
                }
                else
                {
                    Debug.LogWarning("[NPCController] ShopUI instance is null in the scene!");
                }
            }
            else if (HasDialogue)
            {
                // If it doesn't have a shop, [E] also talks to the NPC
                Talk();
            }
            else
            {
                Debug.Log($"[NPCController] NPC {npcName} has no interactable shop or dialogue.");
            }
        }

        public void Talk()
        {
            TheLastEmpire.Story.DialogueTreeSO treeToPlay = GetEffectiveDialogueTree();
            if (treeToPlay != null)
            {
                if (TheLastEmpire.UI.DialogueUI.Instance != null)
                {
                    TheLastEmpire.UI.DialogueUI.Instance.StartDialogue(treeToPlay, npcPortrait);
                }
                else
                {
                    Debug.LogWarning("[NPCController] DialogueUI instance is null in the scene!");
                }
            }
            else
            {
                Debug.Log($"[NPCController] NPC {npcName} has no dialogue tree assigned or found in DialogueDatabase.");
            }
        }
    }
}
