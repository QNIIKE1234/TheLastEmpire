using System;
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
        [SerializeField] private bool canInteract = true;

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
        public bool CanInteract { get => canInteract && isActiveAndEnabled; set => canInteract = value; }

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

        [Header("UI Reference")]
        [SerializeField] private TMPro.TMP_Text nameTextHUD;

        [Header("Floating Speech / Dialog Bubble")]
        [Tooltip("กล่อง Panel บทพูดลอยเหนือหัว (ลาก DIalogPanel จากใต้ Canvas มาใส่)")]
        [SerializeField] private GameObject dialogPanel;
        [Tooltip("Text (TMP) สำหรับแสดงข้อความในกล่องบทพูดลอย (ลาก Text (TMP) มาใส่)")]
        [SerializeField] private TMPro.TMP_Text dialogText;
        [Tooltip("Image สำหรับแสดงไอคอนตัวละครในกล่องบทพูดลอย (ลาก Image ไอคอน/กระเป๋ามาใส่)")]
        [SerializeField] private UnityEngine.UI.Image dialogIcon;

        public GameObject DialogPanel => dialogPanel;
        public TMPro.TMP_Text DialogText => dialogText;
        public UnityEngine.UI.Image DialogIcon => dialogIcon;
        public Sprite CharacterImage => npcData != null ? npcData.characterImage : null;
        public Sprite NpcIcon => npcData != null ? npcData.npcIcon : null;

        [Header("Sitting & Cool Stand Pose Behavior")]
        [Tooltip("When true, NPC stands in a cool signature pose instead of sitting on the ground, and switches to Talk when the player approaches.")]
        [SerializeField] private bool useCoolStandPose = true;
        [Tooltip("Optional override Animator state name (e.g. Pose_03_TacticalReach, Pose_09_RoninCrossed). Leave empty to auto-match by NPC name.")]
        [SerializeField] private string coolStandPoseState = "";
        [SerializeField] private bool playTalkAnimWhenPlayerNear = true;
        [SerializeField] private bool canSit = false;
        [SerializeField] private float standDetectionRadius = 3.5f;
        [SerializeField] private float sitCooldownDuration = 5.0f; // ดีเลย์ 5 วินาทีก่อนกลับไปนั่ง

        [Header("NPC Speech (DialogHint)")]
        [SerializeField] private bool speakOnPlayerApproach = true;
        [SerializeField] private string[] approachSpeechLines;
        [SerializeField] private float speechDuration = 3.2f;
        [SerializeField] private float speechCooldown = 12f;

        private Animator _animator;
        private bool _isSitting = false;
        private bool _isTalkingPose = false;
        private string _resolvedCoolPoseState;
        private Quaternion _initialRotation;
        private float _leaveTimer = 0f;
        private bool _wasPlayerNearby = false;
        private float _nextSpeechAllowedTime = 0f;
        private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");

        private void Start()
        {
            // Auto-update the 3D floating text above the NPC if it exists
            TMPro.TMP_Text textToUpdate = nameTextHUD != null ? nameTextHUD : GetComponentInChildren<TMPro.TMP_Text>();
            if (textToUpdate != null)
            {
                textToUpdate.text = npcName;
            }

            PlayerController player = TheLastEmpire.Network.PlayerRegistry.LocalPlayer
                                   ?? PlayerController.Instance
                                   ?? UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null && !player.IsRemotePlayer)
            {
                _playerTransform = player.transform;
            }

            CacheAnimator();
            _initialRotation = transform.rotation;
            _resolvedCoolPoseState = ResolveCoolPoseStateName();

            // Apply crisp cartoon ink silhouette outline to NPC characters
            CharacterToonOutline.AttachOrRefresh(gameObject);

            if (useCoolStandPose)
            {
                PlayStandingPose(false, instant: true);
            }
            else if (canSit)
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
            PlayerController localPlayer = TheLastEmpire.Network.PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPlayer != null && !localPlayer.IsRemotePlayer)
            {
                _playerTransform = localPlayer.transform;
            }
            else if (_playerTransform == null)
            {
                PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (player != null && !player.IsRemotePlayer) _playerTransform = player.transform;
            }

            // For cool stand pose & Talk animation, react to the closest visible player (local OR co-op teammate!)
            PlayerController closestVisible = TheLastEmpire.Network.PlayerRegistry.GetClosestAlivePlayer(transform.position);
            Transform poseTarget = (closestVisible != null && closestVisible.gameObject.activeInHierarchy)
                ? closestVisible.transform
                : _playerTransform;

            if (poseTarget == null) return;

            float poseDist = Vector3.Distance(transform.position, poseTarget.position);
            bool anyPlayerNearby = poseDist <= standDetectionRadius;
            float localDist = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : float.MaxValue;
            bool playerNearby = localDist <= standDetectionRadius;

            if (useCoolStandPose)
            {
                if (anyPlayerNearby)
                {
                    if (playTalkAnimWhenPlayerNear && !_isTalkingPose)
                    {
                        PlayStandingPose(true, instant: false);
                    }
                    // Gently turn toward the nearest approaching player while talking
                    Vector3 toPlayer = poseTarget.position - transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.04f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5.5f);
                    }
                }
                else
                {
                    if (_isTalkingPose)
                    {
                        PlayStandingPose(false, instant: false);
                    }
                    transform.rotation = Quaternion.Slerp(transform.rotation, _initialRotation, Time.deltaTime * 3.0f);
                }
            }
            else if (canSit)
            {
                if (anyPlayerNearby)
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

            if (speakOnPlayerApproach && canInteract)
            {
                bool hasPopUp = PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps;
                if (playerNearby && !_wasPlayerNearby && !hasPopUp && Time.time >= _nextSpeechAllowedTime)
                {
                    Speak(GetRandomApproachLine(), speechDuration);
                    _nextSpeechAllowedTime = Time.time + speechCooldown;
                }
                else if (!playerNearby && _wasPlayerNearby)
                {
                    if (LootUI.Instance != null)
                    {
                        LootUI.Instance.HideDialogHint(transform);
                    }
                }
            }

            _wasPlayerNearby = playerNearby;
        }

        /// <summary>
        /// Displays an NPC speech line above this NPC via LootUI's DialogHint panel.
        /// </summary>
        public void Speak(string message, float duration = 3.2f)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (LootUI.Instance != null && LootUI.Instance.HasDialogHintUI)
            {
                LootUI.Instance.ShowDialogHint(message, transform, Vector3.up * 2.25f, duration);
            }
        }

        private string GetRandomApproachLine()
        {
            if (approachSpeechLines != null && approachSpeechLines.Length > 0)
            {
                string picked = approachSpeechLines[UnityEngine.Random.Range(0, approachSpeechLines.Length)];
                if (!string.IsNullOrWhiteSpace(picked)) return picked;
            }

            if (HasShop)
            {
                return $"{npcName}: Need supplies? Take a look.";
            }
            if (HasDialogue)
            {
                return $"{npcName}: Hey, got a minute?";
            }
            return $"{npcName}: Stay safe out there.";
        }

        private void OnDisable()
        {
            if (LootUI.HasInstance)
            {
                LootUI.Instance.HideDialogHint(transform);
            }
        }

        private static readonly string[] DefaultCoolPoses =
        {
            "Pose_01_AssassinKnife",
            "Pose_02_BatShoulder",
            "Pose_03_TacticalReach",
            "Pose_04_HeadphonePop",
            "Pose_05_CyanSassy",
            "Pose_06_DreadGrab",
            "Pose_07_CurlyHips",
            "Pose_08_StrikerScan",
            "Pose_09_RoninCrossed",
            "Pose_10_BlueOperative"
        };

        private string ResolveCoolPoseStateName()
        {
            if (!string.IsNullOrWhiteSpace(coolStandPoseState))
                return coolStandPoseState.Trim();

            string key = (npcName + " " + gameObject.name).ToLowerInvariant();
            if (key.Contains("zack") || key.Contains("tactical")) return "Pose_03_TacticalReach";
            if (key.Contains("maya") || key.Contains("punk") || key.Contains("scout")) return "Pose_02_BatShoulder";
            if (key.Contains("elena") || key.Contains("curly") || key.Contains("medic")) return "Pose_07_CurlyHips";
            if (key.Contains("otto") || key.Contains("striker") || key.Contains("gunsmith")) return "Pose_08_StrikerScan";
            if (key.Contains("roy") || key.Contains("ronin") || key.Contains("veteran")) return "Pose_09_RoninCrossed";
            if (key.Contains("blue") || key.Contains("operative")) return "Pose_10_BlueOperative";
            if (key.Contains("dread") || key.Contains("brawler")) return "Pose_06_DreadGrab";

            int hash = Mathf.Abs((npcName ?? gameObject.name).GetHashCode());
            return DefaultCoolPoses[hash % DefaultCoolPoses.Length];
        }

        private void PlayStandingPose(bool talking, bool instant)
        {
            _isSitting = false;
            _isTalkingPose = talking;
            if (_animator == null) CacheAnimator();
            if (_animator == null) return;

            _animator.SetBool(IsSittingHash, false);
            _animator.SetFloat("Speed", 0f);
            _animator.SetBool("IsRunning", false);

            string desired = talking ? "Talk" : (!string.IsNullOrEmpty(_resolvedCoolPoseState) ? _resolvedCoolPoseState : "Idle");
            int hash = Animator.StringToHash(desired);
            if (!_animator.HasState(0, hash))
            {
                desired = "Idle";
            }

            if (instant)
                _animator.Play(desired, 0, 0f);
            else
                _animator.CrossFadeInFixedTime(desired, 0.24f, 0);
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

                // Explosives & Tactical Night Combat Gear
                AddDefaultShopItem("Grenade");
                AddDefaultShopItem("Tactical Flashlight", 120);
                AddDefaultShopItem("Night Vision Goggles", 380);
                AddDefaultShopItem("Signal Flare", 95);
                AddDefaultShopItem("Thermal Bio-Visor", 420);
                AddDefaultShopItem("UV Flashbang", 140);

                // Medicine (4 Types)
                AddDefaultShopItem("Bandage");
                AddDefaultShopItem("First Aid Kit");
                AddDefaultShopItem("Potion");
                AddDefaultShopItem("Adrenaline Shot");

                // Food & Rations (4 Types) + Water
                AddDefaultShopItem("Bread");
                AddDefaultShopItem("Water Bottle", 20);
                AddDefaultShopItem("Energy Bar");
                AddDefaultShopItem("Canned Food");
                AddDefaultShopItem("Cooked Meat");

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
            if (!isActiveAndEnabled || !canInteract) return false;
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
                EnsureWaterBottleInShop();
            }

            // Update Name HUD
            TMPro.TMP_Text textToUpdate = nameTextHUD != null ? nameTextHUD : GetComponentInChildren<TMPro.TMP_Text>();
            if (textToUpdate != null)
            {
                textToUpdate.text = npcName;
            }

            // Update Icon in Dialogue Bubble if assigned
            if (dialogIcon != null)
            {
                Sprite iconToUse = data.npcIcon != null ? data.npcIcon : data.npcPortrait;
                if (iconToUse != null)
                {
                    dialogIcon.sprite = iconToUse;
                    dialogIcon.gameObject.SetActive(true);
                }
            }

            // Visual Model Replacement
            if (data.customModelPrefab != null)
            {
                ApplyCustomModelPrefab(data.customModelPrefab);
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

        /// <summary>
        /// Applies per-spawn overrides from a JSON StageTemplateData NPC entry (name, role, model prefab, pose, speech lines).
        /// </summary>
        public void ApplyTemplateOverride(StageNPCSpawnPoint cfg)
        {
            if (cfg == null) return;

            npcType = cfg.npcType;
            if (npcType != NPCType.Shop)
            {
                shopItems = new System.Collections.Generic.List<ShopItemConfig>();
            }

            if (!string.IsNullOrWhiteSpace(cfg.npcName))
            {
                npcName = cfg.npcName.Trim();
                gameObject.name = $"NPC_{npcName.Replace(" ", "_")}";
                TMPro.TMP_Text textToUpdate = nameTextHUD != null ? nameTextHUD : GetComponentInChildren<TMPro.TMP_Text>();
                if (textToUpdate != null)
                {
                    textToUpdate.text = npcName;
                }
            }

            if (!string.IsNullOrWhiteSpace(cfg.dialogueId))
            {
                dialogueId = cfg.dialogueId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(cfg.coolPoseState))
            {
                coolStandPoseState = cfg.coolPoseState.Trim();
            }

            if (cfg.approachLines != null && cfg.approachLines.Length > 0)
            {
                approachSpeechLines = cfg.approachLines;
            }

            _initialRotation = transform.rotation;

            if (!string.IsNullOrWhiteSpace(cfg.modelPrefab))
            {
                GameObject modelPrefab = LoadCharacterModelPrefab(cfg.modelPrefab.Trim());
                if (modelPrefab != null)
                {
                    ApplyCustomModelPrefab(modelPrefab);
                    CharacterToonOutline.AttachOrRefresh(gameObject);
                }
                else
                {
                    Debug.LogWarning($"[NPCController] Could not resolve modelPrefab '{cfg.modelPrefab}' for NPC '{npcName}'.");
                }
            }
            else if (_animator != null && useCoolStandPose)
            {
                _resolvedCoolPoseState = ResolveCoolPoseStateName();
                PlayStandingPose(false, instant: true);
            }
        }

        private static GameObject LoadCharacterModelPrefab(string modelPathOrName)
        {
            if (string.IsNullOrWhiteSpace(modelPathOrName)) return null;

            GameObject prefab = AddressableManager.LoadAsset<GameObject>(modelPathOrName);
            if (prefab != null) return prefab;

            if (!modelPathOrName.Contains("/"))
            {
                prefab = AddressableManager.LoadAsset<GameObject>($"Prefabs/Character/Model/{modelPathOrName}");
                if (prefab != null) return prefab;

                prefab = AddressableManager.LoadAsset<GameObject>($"Prefabs/Character/{modelPathOrName}");
                if (prefab != null) return prefab;
            }

            prefab = Resources.Load<GameObject>(modelPathOrName);
            if (prefab != null) return prefab;

            if (!modelPathOrName.Contains("/"))
            {
                prefab = Resources.Load<GameObject>($"Prefabs/Character/Model/{modelPathOrName}");
                if (prefab != null) return prefab;

                prefab = Resources.Load<GameObject>($"Prefabs/Character/{modelPathOrName}");
                if (prefab != null) return prefab;
            }

            return null;
        }

        public void ApplyCustomModelPrefab(GameObject customModelPrefab)
        {
            if (customModelPrefab == null) return;

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

                GameObject model = Instantiate(customModelPrefab, targetRoot);
                model.name = customModelPrefab.name;
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
                        RuntimeAnimatorController ctrl = AddressableManager.LoadAsset<RuntimeAnimatorController>("Image/Animation/Chibi_Player_Controller");
                        if (ctrl == null) ctrl = AddressableManager.LoadAsset<RuntimeAnimatorController>("Animation/Chibi_Player_Controller");
                        if (ctrl == null) ctrl = Resources.Load<RuntimeAnimatorController>("Image/Animation/Chibi_Player_Controller");
                        if (ctrl == null && PlayerController.Instance != null)
                        {
                            Animator pAnim = PlayerController.Instance.GetComponentInChildren<Animator>();
                            if (pAnim != null) ctrl = pAnim.runtimeAnimatorController;
                        }
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
                        _resolvedCoolPoseState = ResolveCoolPoseStateName();
                        if (useCoolStandPose)
                        {
                            PlayStandingPose(false, instant: true);
                        }
                        else if (canSit)
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

        private void EnsureWaterBottleInShop()
        {
            if (shopItems == null)
            {
                shopItems = new System.Collections.Generic.List<ShopItemConfig>();
            }

            bool hasWater = false;
            int breadIdx = -1;
            for (int i = 0; i < shopItems.Count; i++)
            {
                if (shopItems[i].item != null)
                {
                    string n = shopItems[i].item.itemName;
                    if (string.Equals(n, "Water Bottle", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n, "Water", StringComparison.OrdinalIgnoreCase) ||
                        shopItems[i].item.name.IndexOf("WaterBottle", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasWater = true;
                        break;
                    }
                    if (string.Equals(n, "Bread", StringComparison.OrdinalIgnoreCase) ||
                        shopItems[i].item.name.IndexOf("Bread", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        breadIdx = i;
                    }
                }
            }

            if (!hasWater && ItemDatabase.Instance != null)
            {
                ItemData waterData = ItemDatabase.Instance.GetItemByName("Water Bottle");
                if (waterData != null)
                {
                    int insertIdx = (breadIdx >= 0) ? breadIdx + 1 : shopItems.Count;
                    shopItems.Insert(insertIdx, new ShopItemConfig { item = waterData, price = 20 });
                }
            }

            // Ensure Backpack Expansion (+7 slots, $1500, max 70 slots) is always stocked in merchant shops
            bool hasBagExpansion = false;
            for (int i = 0; i < shopItems.Count; i++)
            {
                if (ShopUI.IsBackpackExpansionItem(shopItems[i].item))
                {
                    hasBagExpansion = true;
                    break;
                }
            }
            if (!hasBagExpansion && ItemDatabase.Instance != null)
            {
                ItemData bagData = ItemDatabase.Instance.GetItemByName(ShopUI.BackpackExpansionItemName);
                if (bagData != null)
                {
                    shopItems.Add(new ShopItemConfig { item = bagData, price = ShopUI.BackpackExpansionDefaultPrice });
                }
            }
        }

        public void Interact()
        {
            // If the NPC has a shop, open the shop menu with [E]
            if (HasShop)
            {
                EnsureWaterBottleInShop();
                if (ShopUI.Instance != null)
                {
                    Sprite shopNpcSprite = CharacterImage != null
                        ? CharacterImage
                        : (npcPortrait != null ? npcPortrait : NpcIcon);
                    ShopUI.Instance.OpenShopMenu(shopItems, isFree, shopNpcSprite, npcName);
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
                Speak(GetRandomApproachLine(), 3.6f);
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
                Speak(GetRandomApproachLine(), 3.6f);
            }
        }
    }
}
