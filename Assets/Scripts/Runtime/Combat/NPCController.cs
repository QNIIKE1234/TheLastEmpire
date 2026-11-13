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
        [Header("Data-Driven NPC Definition")]
        [Tooltip("Assign NPCDataSO to automatically populate all settings from data")]
        [SerializeField] private NPCDataSO npcData;
        [Tooltip("Optional transform slot for swapping 3D character models dynamically")]
        [SerializeField] private Transform modelRoot;

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
        public NPCDataSO NPCData => npcData;

        private void Awake()
        {
            if (npcData != null)
            {
                ApplyData(npcData);
            }
        }

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

        [Header("Sitting Behavior")]
        [SerializeField] private bool canSit = true;
        [SerializeField] private float standDetectionRadius = 3.5f;
        [SerializeField] private float sitCooldownDuration = 5.0f; // ดีเลย์ 5 วินาทีก่อนกลับไปนั่ง

        private Animator _animator;
        private bool _isSitting = false;
        private float _leaveTimer = 0f;
        private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");

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

            CacheAnimator();

            // เริ่มต้นให้นั่งถ้าเปิดใช้งาน canSit
            if (canSit)
            {
                SetSittingState(true);
            }

            // Populate default merchant items if none are set
            if (npcType == NPCType.Shop && (shopItems == null || shopItems.Count == 0))
            {
                PopulateDefaultShopItems();
            }
        }

        private void Update()
        {
            if (!canSit) return;

            if (_playerTransform == null)
            {
                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null) _playerTransform = player.transform;
            }

            if (_playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool playerNearby = dist <= standDetectionRadius;

            if (playerNearby)
            {
                _leaveTimer = 0f; // รีเซ็ตตัวนับเวลาตราบใดที่ Player ยังอยู่ใกล้ๆ
                // ผู้เล่นเข้าใกล้ -> ลุกขึ้นยืน
                if (_isSitting)
                {
                    SetSittingState(false);
                }
            }
            else
            {
                // ผู้เล่นเดินห่างออกไป -> เริ่มนับถอยหลังดีเลย์ 5 วิก่อนกลับไปนั่ง
                if (!_isSitting)
                {
                    _leaveTimer += Time.deltaTime;
                    if (_leaveTimer >= sitCooldownDuration)
                    {
                        SetSittingState(true);
                        _leaveTimer = 0f;
                    }
                }
            }
        }

        public void SetSittingState(bool sit, bool instant = false)
        {
            _isSitting = sit;
            if (_animator == null) CacheAnimator();
            if (_animator != null)
            {
                _animator.SetBool(IsSittingHash, sit);
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsRunning", false);

                if (instant)
                {
                    _animator.Play(sit ? "Sit" : "Idle", 0, 0f);
                }
                else
                {
                    // เปลี่ยนท่าแบบนุ่มนวล (CrossFade 0.25 วินาที) เหมือนของ PlayerController
                    _animator.CrossFadeInFixedTime(sit ? "Sit" : "Idle", 0.25f, 0);
                }
            }
        }

        private void CacheAnimator()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
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

                // Explosives & Tactical
                AddDefaultShopItem("Grenade");

                // Medicine (4 Types)
                AddDefaultShopItem("Bandage");
                AddDefaultShopItem("First Aid Kit");
                AddDefaultShopItem("Potion");
                AddDefaultShopItem("Adrenaline Shot");

                // Food & Rations (4 Types) + Water
                AddDefaultShopItem("Bread");
                AddDefaultShopItem("Energy Bar");
                AddDefaultShopItem("Canned Food");
                AddDefaultShopItem("Cooked Meat");
                AddDefaultShopItem("Water Bottle", 20);

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

        public void ApplyData(NPCDataSO data)
        {
            if (data == null) return;
            npcData = data;
            npcName = data.npcName;
            npcType = data.npcType;
            interactRange = data.interactRange;
            dialogueId = data.dialogueId;
            dialogueTree = data.dialogueTree;
            npcPortrait = data.npcPortrait;
            isFree = data.isFree;

            if (data.shopItems != null)
            {
                shopItems = new System.Collections.Generic.List<ShopItemConfig>(data.shopItems);
            }

            // Update Name HUD
            TMPro.TMP_Text textToUpdate = nameTextHUD != null ? nameTextHUD : GetComponentInChildren<TMPro.TMP_Text>();
            if (textToUpdate != null)
            {
                textToUpdate.text = npcName;
            }

            // Visual Model Replacement
            if (data.customModelPrefab != null)
            {
                // Deactivate all old/baked-in models on the NPC prefab (e.g. MegaphoneGirl, Chibi_Base)
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.GetComponent<Canvas>() != null || child.GetComponent<Collider>() != null) continue;
                    if (child.name.Contains("Megaphone") || child.name.Contains("Chibi") || child.name.Contains("Rigged"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                if (modelRoot != null && (modelRoot.name.Contains("Megaphone") || modelRoot.name.Contains("Rigged") || modelRoot.name.Contains("Chibi")))
                {
                    modelRoot.gameObject.SetActive(false);
                }

                Transform targetRoot = transform.Find("ModelRoot");
                if (targetRoot == null)
                {
                    GameObject newRoot = new GameObject("ModelRoot");
                    newRoot.transform.SetParent(transform, false);
                    targetRoot = newRoot.transform;
                }
                else
                {
                    for (int i = targetRoot.childCount - 1; i >= 0; i--)
                    {
                        Destroy(targetRoot.GetChild(i).gameObject);
                    }
                }

                if (targetRoot != null)
                {
                    // ตั้งค่า Transform ของ ModelRoot ตามค่ามาตรฐาน: Pos (0, -0.9, 0), Rot (0, 180, 0), Scale (0.7, 0.7, 0.7)
                    targetRoot.localPosition = new Vector3(0f, -0.9f, 0f);
                    targetRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    targetRoot.localScale = new Vector3(0.7f, 0.7f, 0.7f);

                    GameObject model = Instantiate(data.customModelPrefab, targetRoot);
                    model.name = data.customModelPrefab.name;
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;

                    // Ensure Animator inherits or loads the Controller so character does NOT stand in a T-pose
                    Animator anim = model.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.applyRootMotion = false;
                        if (anim.runtimeAnimatorController == null)
                        {
                            RuntimeAnimatorController ctrl = Resources.Load<RuntimeAnimatorController>("Image/Animation/Chibi_Player_Controller");
                            if (ctrl == null) ctrl = AddressableManager.LoadAsset<RuntimeAnimatorController>("Animation/Chibi_Player_Controller");
                            if (ctrl == null)
                            {
                                Animator existingAnim = GetComponentInChildren<Animator>();
                                if (existingAnim != null && existingAnim != anim) ctrl = existingAnim.runtimeAnimatorController;
                            }
                            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                        }

                        if (anim.runtimeAnimatorController != null)
                        {
                            _animator = anim;
                            if (canSit)
                            {
                                SetSittingState(true);
                            }
                            else
                            {
                                anim.Play("Idle", 0, 0f);
                            }
                        }
                    }
                }
            }

            // Visual Tint
            if (data.tintColor != Color.white)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (var rend in renderers)
                {
                    if (rend is MeshRenderer || rend is SkinnedMeshRenderer)
                    {
                        if (rend.material != null)
                        {
                            rend.material.color = data.tintColor;
                        }
                    }
                }
            }
        }

        public void SetData(NPCDataSO data) => ApplyData(data);

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
