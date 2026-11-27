using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    [System.Serializable]
    public class CharacterModelEntry
    {
        [Tooltip("ลากไฟล์ CharacterData (เช่น CHAR_01_Vanguard) มาใส่เพื่อจับคู่ตัวละคร")]
        public CharacterData characterData;

        [Tooltip("หรือระบุ Character ID แทนได้ (เช่น VANGUARD, JUGGERNAUT, PHANTOM, MARKSMAN, SCAVENGER, REBEL, DEMOLITIONIST)")]
        public string characterId;

        [Tooltip("ลาก GameObject โมเดลลูกใต้ Model (หรือ Prefab ก็ได้) ที่จะใช้แสดงเมื่อเลือกตัวละครนี้")]
        public GameObject modelObject;
    }

    [RequireComponent(typeof(Rigidbody))]
    public partial class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private PlayerConfigSO playerConfig;

        [Header("Character Selection Integration")]
        [SerializeField] private CharacterData activeCharacterData;
        [Tooltip("Transform ที่รวมโมเดลตัวละครทั้งหมด (เช่น 'Model') — จะเปิดเฉพาะตัวที่เลือกและปิดตัวอื่นให้อัตโนมัติ")]
        [SerializeField] private Transform characterModelRoot;
        [Tooltip("List สำหรับใส่โมเดลของแต่ละตัวละครที่เลือกเล่น (รองรับทั้ง GameObject ลูกใน PlayerPrefabs/Model และ Prefab Asset)")]
        [SerializeField] private List<CharacterModelEntry> characterModels = new List<CharacterModelEntry>();
        private IShiftAbility _activeShiftAbility;
        private GameObject _activeCharacterModel;

        public CharacterData ActiveCharacterData => activeCharacterData;
        public IReadOnlyList<CharacterModelEntry> CharacterModels => characterModels;
        public GameObject ActiveCharacterModel => _activeCharacterModel;
        public IShiftAbility ActiveShiftAbility => _activeShiftAbility;
        public Vector2 MoveInput => _moveInput;
        public Vector3 AimDirection => _aimDirection;
        public bool HasGuaranteedCrit { get; set; } = false;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Ammo & Reloading")]
        [SerializeField] private int startingReserveAmmo = 120;
        [SerializeField] private int magazineSize = 12;
        [SerializeField] private float reloadDuration = 1.0f;

        [Header("Dash / Dodge Roll")]
        [SerializeField] private float dashSpeed = 15f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float dashCooldown = 0.8f;

        [Header("Combat - Ranged")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private float fireRate = 0.2f;
        [SerializeField] private System.Collections.Generic.List<Weapon> weapons = new System.Collections.Generic.List<Weapon>();

        [Header("Combat - Melee")]
        [SerializeField] private float meleeRadius = 1.2f;
        [SerializeField] private int meleeDamage = 35;
        [SerializeField] private float meleeRate = 0.4f;

        [Header("Screen Boundaries")]
        [SerializeField] private float xLimit = 8.5f;
        [SerializeField] private float yLimit = 5f;
        [SerializeField] private float entryOffset = 0.5f;

        [Header("Visual & Animation")]
        [SerializeField] private Animator animator;
        private Animator _animator;

        [Header("Weapon Visual References (Malee / Range)")]
        [Tooltip("Reference สำหรับ GameObject อาวุธระยะประชิด (เช่น GameObject 'Malee')")]
        [SerializeField] private GameObject meleeWeapon;
        [Tooltip("Reference สำหรับ GameObject อาวุธระยะไกล (เช่น GameObject 'Range')")]
        [SerializeField] private GameObject rangeWeapon;

        [Header("UI System References")]
        [Tooltip("Reference ไปยัง QuickSlotController ใน Scene (ตั้งค่าผ่าน Inspector หรือ Baker)")]
        [SerializeField] private QuickSlotController quickSlotController;
        public QuickSlotController QuickSlot => quickSlotController != null ? quickSlotController : QuickSlotController.Instance;

        [Header("Muzzle Flash Light")]
        [SerializeField] private Light muzzleFlashLight;
        [SerializeField] private Color muzzleLightColor = new Color(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private float muzzleLightIntensity = 10f;
        [SerializeField] private float muzzleLightRange = 6f;
        [SerializeField] private float muzzleLightDuration = 0.08f;
        private float _muzzleLightTimer = 0f;

        private Rigidbody _rb;
        private Health _health;
        private Vector2 _moveInput;
        private Vector3 _aimDirection = Vector3.right;

        private float _startupDelay = 0.5f;
        private float _dashTimer = 0f;

        private int _currentMagazine;
        private int _currentReserveAmmo;
        private bool _isReloading = false;
        private int _currentWeaponIndex = 0;

        [Header("Combat - Melee Weapons")]
        [SerializeField] private System.Collections.Generic.List<MeleeWeapon> meleeWeapons = new System.Collections.Generic.List<MeleeWeapon>();
        private int _currentMeleeWeaponIndex = 0;

        // Public properties and events for HUD mapping
        public Health PlayerHealth => _health;
        public int CurrentMagazine => CurrentWeapon != null ? CurrentWeapon.currentMagazine : _currentMagazine;
        
        public int CurrentReserveAmmo
        {
            get
            {
                if (_inventory == null) return 0;
                string ammoType = "Pistol Ammo";
                if (CurrentWeapon != null && !string.IsNullOrEmpty(CurrentWeapon.ammoType))
                {
                    ammoType = CurrentWeapon.ammoType;
                }
                return GetAmmoReserveCount(ammoType);
            }
        }

        public bool IsReloading => _isReloading;
        public Weapon CurrentWeapon => (weapons != null && weapons.Count > 0 && _currentWeaponIndex >= 0 && _currentWeaponIndex < weapons.Count) ? weapons[_currentWeaponIndex] : null;
        public string CurrentWeaponName => CurrentWeapon != null ? CurrentWeapon.weaponName : "";
        public System.Collections.Generic.List<Weapon> WeaponsList => weapons;

        public MeleeWeapon CurrentMeleeWeapon => (meleeWeapons != null && meleeWeapons.Count > 0 && _currentMeleeWeaponIndex >= 0 && _currentMeleeWeaponIndex < meleeWeapons.Count) ? meleeWeapons[_currentMeleeWeaponIndex] : null;
        public string CurrentMeleeWeaponName => CurrentMeleeWeapon != null ? CurrentMeleeWeapon.weaponName : "";
        public System.Collections.Generic.List<MeleeWeapon> MeleeWeaponsList => meleeWeapons;

        public GameObject MeleeWeaponVisual => meleeWeapon;
        public GameObject RangeWeaponVisual => rangeWeapon;

        public enum HeldItemType
        {
            None,
            Ranged,
            Melee,
            Consumable
        }

        private HeldItemType _heldItemType = HeldItemType.Ranged;
        private string _heldConsumableName = "";

        private enum TargetInteractableType { None, Building, Container, NPC }
        private TargetInteractableType _bestInteractableType = TargetInteractableType.None;
        private IBuildingInteractable _closestBuilding;
        private LootContainer _closestContainer;
        private NPCController _closestNpc;

        public HeldItemType CurrentHeldType => _heldItemType;
        public bool IsHoldingRanged => _heldItemType == HeldItemType.Ranged && CurrentWeapon != null;
        public bool IsHoldingMelee => _heldItemType == HeldItemType.Melee && CurrentMeleeWeapon != null;
        public bool IsHoldingConsumable => _heldItemType == HeldItemType.Consumable && !string.IsNullOrEmpty(_heldConsumableName);
        public string HeldConsumableName => _heldConsumableName;

        public string ActiveHeldItemName
        {
            get
            {
                if (IsHoldingRanged) return CurrentWeaponName;
                if (IsHoldingMelee) return CurrentMeleeWeaponName;
                if (IsHoldingConsumable) return _heldConsumableName;
                return "";
            }
        }

        public void SetHeldRangedWeapon(string weaponName)
        {
            _heldItemType = HeldItemType.Ranged;
            _heldConsumableName = "";
            int idx = -1;
            if (weapons != null)
            {
                idx = weapons.FindIndex(w => w.weaponName.Equals(weaponName, System.StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                {
                    idx = weapons.FindIndex(w => w.weaponName.IndexOf(weaponName, System.StringComparison.OrdinalIgnoreCase) >= 0 || weaponName.IndexOf(w.weaponName, System.StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            // ถ้าไม่มีใน weapons ให้สร้าง Weapon object ขึ้นมาอัตโนมัติจาก ItemDatabase (เช่น Grenade หรือปืนที่เก็บได้)
            if (idx < 0 && ItemDatabase.Instance != null && weapons != null)
            {
                ItemData data = ItemDatabase.Instance.GetItemByName(weaponName);
                if (data != null && (data.type == ItemType.RangedWeapon || data.type == ItemType.ThrowingWeapon))
                {
                    Weapon newWeapon = new Weapon(data);
                    if (string.IsNullOrEmpty(newWeapon.projectileVisualId))
                    {
                        newWeapon.projectileVisualId = ResolveProjectileVisualId(newWeapon);
                    }
                    if (newWeapon.projectilePrefab == null)
                    {
                        if (data.type == ItemType.ThrowingWeapon || weaponName.IndexOf("grenade", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            newWeapon.projectilePrefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/ThrowingWeaponPrefabs")?.GetComponent<Projectile>();
                        }
                        if (newWeapon.projectilePrefab == null) newWeapon.projectilePrefab = projectilePrefab;
                    }
                    weapons.Add(newWeapon);
                    idx = weapons.Count - 1;
                }
            }

            if (idx >= 0)
            {
                SwitchToWeapon(idx);
            }
            else
            {
                ShowRangedWeaponVisual();
                OnAmmoChanged?.Invoke();
            }
        }

        public void SetHeldMeleeWeapon(string weaponName)
        {
            _heldItemType = HeldItemType.Melee;
            _heldConsumableName = "";
            int idx = -1;
            if (meleeWeapons != null)
            {
                idx = meleeWeapons.FindIndex(w => w.weaponName.Equals(weaponName, System.StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                {
                    idx = meleeWeapons.FindIndex(w => w.weaponName.IndexOf(weaponName, System.StringComparison.OrdinalIgnoreCase) >= 0 || weaponName.IndexOf(w.weaponName, System.StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
            if (idx >= 0)
            {
                SwitchToMeleeWeapon(idx);
            }
            else
            {
                ShowMeleeWeaponVisual();
                OnAmmoChanged?.Invoke();
            }
        }

        public void SetHeldConsumable(string consumableName)
        {
            _heldItemType = HeldItemType.Consumable;
            _heldConsumableName = consumableName;

            EnsureWeaponVisualReferences();
            if (rangeWeapon != null) rangeWeapon.SetActive(false);
            if (meleeWeapon != null) meleeWeapon.SetActive(false);

            OnAmmoChanged?.Invoke();
        }

        public void NotifyWeaponChanged()
        {
            OnAmmoChanged?.Invoke();
        }

        public event System.Action OnAmmoChanged;

        [Header("Hunger System")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float currentHunger = 100f;
        [Tooltip("ตัวคูณอัตราการลดของพลังงาน/ความหิว (0.5 = ลดช้าลงเท่าตัว หรืออึดขึ้น 2 เท่า)")]
        [SerializeField] private float hungerDepletionMultiplier = 0.5f;

        private float _hungerDamageTimer = 0f;

        public float MaxHunger => maxHunger;
        public float CurrentHunger => currentHunger;
        public event System.Action OnHungerChanged;

        [Header("Water / Hydration System")]
        [SerializeField] private float maxWater = 100f;
        [SerializeField] private float currentWater = 100f;
        [Tooltip("ตัวคูณอัตราการลดของค่าน้ำโดยรวม")]
        [SerializeField] private float waterDepletionMultiplier = 1.0f;

        private float _waterDamageTimer = 0f;

        public float MaxWater => maxWater;
        public float CurrentWater => currentWater;
        public bool IsDehydrated => currentWater <= 0f;
        public event System.Action OnWaterChanged;

        [Header("Sanity System (ค่าสติ - Brain / Eye Bar)")]
        [SerializeField] private float maxSanity = 100f;
        [SerializeField] private float currentSanity = 100f;
        [Tooltip("ตัวคูณอัตราการลดของค่าสติโดยรวม (1.0 = ปกติ, 0.5 = ลดช้าลงเท่าตัว)")]
        [SerializeField] private float sanityDepletionMultiplier = 1.0f;
        [Tooltip("อัตราการลดของค่าสติต่อวินาทีในตอนกลางคืน (เช่น 0.5f/วิ)")]
        [SerializeField] private float nightSanityDepletionRate = 0.5f;
        [Tooltip("ตัวคูณการลดค่าสติเมื่ออยู่ใน Combat ตอนกลางคืน (เช่น 2.0 = ลดเร็วขึ้น 2 เท่า)")]
        [SerializeField] private float combatNightSanityMultiplier = 2.0f;
        [Tooltip("อัตราการลดของค่าสติต่อวินาทีเมื่ออยู่ใน Combat ตอนกลางวัน")]
        [SerializeField] private float combatDaySanityDepletionRate = 0.3f;
        [Tooltip("เปอร์เซ็นต์ของความเสียหายที่ได้รับที่จะถูกหักออกจากค่าสติ (0.10 = 10% ของดาเมจที่โดนกัด/โจมตี)")]
        [SerializeField] private float sanityDamageTakenPercent = 0.10f;
        [Tooltip("อัตราการฟื้นฟูค่าสติต่อวินาทีเมื่ออยู่อย่างปลอดภัยในตอนกลางวัน (ไม่อยู่ใน Combat)")]
        [SerializeField] private float daySanityRecoveryRate = 0.2f;
        [Tooltip("ระยะเวลาคงสถานะ Combat หลังจากโจมตีหรือโดนโจมตี (วินาที)")]
        [SerializeField] private float combatDuration = 5.0f;

        private float _combatTimer = 0f;
        private float _sanityDamageTimer = 0f;

        public float MaxSanity => maxSanity;
        public float CurrentSanity => currentSanity;
        public bool IsInCombat => _combatTimer > 0f;
        public event System.Action OnSanityChanged;

        private bool _isSprinting = false;
        public bool IsSprinting => _isSprinting;

        private bool _isSitting = false;
        public bool IsSitting => _isSitting;
        private bool _isSleeping = false;
        public bool IsSleeping => _isSleeping;

        private float _dashCooldownTimer = 0f;
        private float _fireCooldownTimer = 0f;
        private float _fireBufferTimer = 0f;
        private float _meleeCooldownTimer = 0f;
        private float _meleeBufferTimer = 0f;
        private int _meleeComboStep = 0;
        private float _lastMeleeSwingTime = -10f;
        private const float MeleeComboResetWindow = 1.0f;
        private float _shootAnimLayerTimer = 0f;
        private float _shootSprintLockTimer = 0f;
        private float _meleeAnimTimer = 0f;

        [Header("Gunshot Noise Settings")]
        [SerializeField] private float gunshotAlertRange = 5f;

        [Header("Flamethrower Cone VFX & Legendary Fireball Skill")]
        [Tooltip("ลาก VFX_FlamethrowerConeFire ที่วางไว้ใน PlayerPrefabs มาใส่ หรือปล่อยว่างให้ค้นหาอัตโนมัติ")]
        [SerializeField] private FlamethrowerConeVfx flamethrowerConeVfx;
        [Tooltip("คีย์ใน ObjectPoolManager สำหรับลูกบอลเพลิงของสกิลคลิกขวา Legendary Flamethrower")]
        [SerializeField] private string fireballPoolKey = "FlamethrowerFireball";
        [SerializeField] private float fireballSpeed = 25f;

        private float _flamethrowerVfxTimer = 0f;
        private float _rmbSkillCooldownTimer = 0f;
        private bool _isChargingRmbSkill = false;
        private float _rmbChargeTimer = 0f;
        private float _rmbChargeTargetDuration = 2.0f;
        private bool _rmbSkillFiredThisHold = false;
        private string _rmbChargeWeaponName = "";

        [Header("Grenade / AOE Indicators")]
        [Tooltip("Custom sprite for the Grenade AOE circle (optional). If unassigned, uses the red LineRenderer circle.")]
        [SerializeField] private Sprite aoeIndicatorSprite;
        [SerializeField] private Color aoeIndicatorColor = new Color(1f, 0.2f, 0.2f, 0.7f);

        [Tooltip("Custom sprite for the player throw range circle (optional). If unassigned, uses the white LineRenderer circle.")]
        [SerializeField] private Sprite throwRangeSprite;
        [SerializeField] private Color throwRangeColor = new Color(1f, 1f, 1f, 0.35f);
        
        private bool _isDashing = false;
        private Vector3 _dashDirection;
        private Color _originalColor = Color.white;
        private SpriteRenderer _spriteRenderer;
        private PlayerInventory _inventory;

        private Collider _playerCollider;
        private BoxCollider _boundaryLeft;
        private BoxCollider _boundaryRight;
        private BoxCollider _boundaryTop;
        private BoxCollider _boundaryBottom;
        private GameObject _boundaryContainer;

        private void Awake()
        {
            if (NextInstantiatedIsRemote)
            {
                IsRemotePlayer = true;
            }
            if (!IsRemotePlayer)
            {
                Instance = this;
            }
            TheLastEmpire.Network.PlayerRegistry.Register(this);
            ApplyPlayerConfig();
            InitIndicators();
        }

        private void ApplyPlayerConfig()
        {
            if (activeCharacterData == null)
            {
                activeCharacterData = CharacterManager.GetSelectedCharacter();
            }

            if (activeCharacterData != null)
            {
                moveSpeed = activeCharacterData.moveSpeed;
                dashSpeed = activeCharacterData.dashSpeed;
                dashDuration = activeCharacterData.dashDuration;
                dashCooldown = activeCharacterData.dashCooldown;
                maxHunger = activeCharacterData.maxHunger;
                currentHunger = maxHunger;
                if (activeCharacterData.maxWater > 0f) maxWater = activeCharacterData.maxWater;
                currentWater = maxWater;
                if (activeCharacterData.waterConsumptionMultiplier > 0f) waterDepletionMultiplier = activeCharacterData.waterConsumptionMultiplier;
                OnWaterChanged?.Invoke();
                if (activeCharacterData.maxSanity > 0f) maxSanity = activeCharacterData.maxSanity;
                currentSanity = maxSanity;
                if (activeCharacterData.sanityDepletionMultiplier > 0f) sanityDepletionMultiplier = activeCharacterData.sanityDepletionMultiplier;
                if (activeCharacterData.sanityDamageTakenPercent > 0f) sanityDamageTakenPercent = activeCharacterData.sanityDamageTakenPercent;
                if (activeCharacterData.nightSanityDepletionRate > 0f) nightSanityDepletionRate = activeCharacterData.nightSanityDepletionRate;
                if (activeCharacterData.daySanityRecoveryRate > 0f) daySanityRecoveryRate = activeCharacterData.daySanityRecoveryRate;
                OnSanityChanged?.Invoke();
                startingReserveAmmo = activeCharacterData.startingPistolAmmo;

                _health = GetComponent<Health>();
                if (_health != null)
                {
                    _health.SetMaxHealth(activeCharacterData.maxHealth, true);
                }

                if (_inventory != null && activeCharacterData.maxInventorySlots >= 18)
                {
                    _inventory.SetMaxSlots(activeCharacterData.maxInventorySlots);
                }

                InitShiftAbility();

                ApplySelectedCharacterModel(activeCharacterData);
            }
            else if (playerConfig != null)
            {
                moveSpeed = playerConfig.moveSpeed;
                dashSpeed = playerConfig.dashSpeed;
                dashDuration = playerConfig.dashDuration;
                dashCooldown = playerConfig.dashCooldown;
                maxHunger = playerConfig.maxHunger;
                if (playerConfig.maxSanity > 0f) maxSanity = playerConfig.maxSanity;
                currentSanity = maxSanity;
                if (playerConfig.sanityDepletionMultiplier > 0f) sanityDepletionMultiplier = playerConfig.sanityDepletionMultiplier;
                if (playerConfig.sanityDamageTakenPercent > 0f) sanityDamageTakenPercent = playerConfig.sanityDamageTakenPercent;
                if (playerConfig.nightSanityDepletionRate > 0f) nightSanityDepletionRate = playerConfig.nightSanityDepletionRate;
                if (playerConfig.daySanityRecoveryRate > 0f) daySanityRecoveryRate = playerConfig.daySanityRecoveryRate;
                OnSanityChanged?.Invoke();

                _health = GetComponent<Health>();
                if (_health != null)
                {
                    _health.SetMaxHealth(playerConfig.maxHealth, true);
                }
            }
        }

        public void SwitchCharacterModel(CharacterData data)
        {
            if (data != null)
            {
                activeCharacterData = data;
            }
            ApplySelectedCharacterModel(activeCharacterData);
        }

        private static readonly Vector3 SpawnedModelLocalPosition = new Vector3(0f, -0.6f, 0f);
        private static readonly Vector3 SpawnedModelLocalScale = new Vector3(0.7f, 0.7f, 0.7f);

        private void ApplySelectedCharacterModel(CharacterData data)
        {
            Transform modelRoot = characterModelRoot != null ? characterModelRoot : transform.Find("Model");
            if (modelRoot == null)
            {
                GameObject modelRootObj = new GameObject("Model");
                modelRoot = modelRootObj.transform;
                modelRoot.SetParent(transform, false);
                characterModelRoot = modelRoot;
            }

            // Preserve any existing AnimatorController (including from Chibi_Base_Rigged template) before hiding/swapping
            RuntimeAnimatorController fallbackController = _animator != null ? _animator.runtimeAnimatorController : null;
            if (fallbackController == null && animator != null)
            {
                fallbackController = animator.runtimeAnimatorController;
            }

            // Auto-locate master Malee / Range weapon holders under Model if Inspector refs are unset
            if (meleeWeapon == null)
            {
                Transform m = modelRoot.Find("Malee") ?? modelRoot.Find("Melee") ?? transform.Find("Malee");
                if (m != null) meleeWeapon = m.gameObject;
            }
            if (rangeWeapon == null)
            {
                Transform r = modelRoot.Find("Range") ?? modelRoot.Find("Ranged") ?? transform.Find("Range");
                if (r != null) rangeWeapon = r.gameObject;
            }

            // Rescue master meleeWeapon / rangeWeapon onto PlayerPrefabs root before cleaning up old model instances
            if (meleeWeapon != null && meleeWeapon.transform.IsChildOf(modelRoot))
            {
                meleeWeapon.transform.SetParent(transform, false);
            }
            if (rangeWeapon != null && rangeWeapon.transform.IsChildOf(modelRoot))
            {
                rangeWeapon.transform.SetParent(transform, false);
            }

            // Hide template/base model sitting directly under PlayerPrefabs (e.g. Chibi_Base_Rigged)
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child == modelRoot) continue;
                if (meleeWeapon != null && child == meleeWeapon.transform) continue;
                if (rangeWeapon != null && child == rangeWeapon.transform) continue;

                bool isTemplateModel = child.name.Equals("Chibi_Base_Rigged", System.StringComparison.OrdinalIgnoreCase)
                                    || child.GetComponent<Animator>() != null
                                    || child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                if (isTemplateModel)
                {
                    if (fallbackController == null)
                    {
                        Animator tplAnim = child.GetComponentInChildren<Animator>(true);
                        if (tplAnim != null && tplAnim.runtimeAnimatorController != null)
                        {
                            fallbackController = tplAnim.runtimeAnimatorController;
                        }
                    }
                    child.gameObject.SetActive(false);
                }
            }

            // Deactivate all legacy child models registered in the list and under Model root
            if (characterModels != null)
            {
                for (int i = 0; i < characterModels.Count; i++)
                {
                    GameObject mo = characterModels[i]?.modelObject;
                    if (mo != null && mo.transform.IsChildOf(transform) && mo != meleeWeapon && mo != rangeWeapon)
                    {
                        mo.SetActive(false);
                    }
                }
            }

            for (int i = 0; i < modelRoot.childCount; i++)
            {
                Transform ch = modelRoot.GetChild(i);
                if (ch != null) ch.gameObject.SetActive(false);
            }

            // Clean up any previously spawned prefab instance
            Transform oldInstance = modelRoot.Find("CharacterModelInstance")
                                    ?? transform.Find("CharacterModelInstance");
            if (oldInstance != null)
            {
                oldInstance.name = "CharacterModelInstance_Old";
                Destroy(oldInstance.gameObject);
            }

            // Priority 1: Always spawn directly from CharacterData ScriptableObject (characterModelPrefab) into PlayerPrefabs/Model
            GameObject activeModel = null;
            if (data != null && data.characterModelPrefab != null)
            {
                activeModel = Instantiate(data.characterModelPrefab, modelRoot);
                activeModel.name = "CharacterModelInstance";
                activeModel.transform.localPosition = SpawnedModelLocalPosition;
                activeModel.transform.localRotation = Quaternion.identity;
                activeModel.transform.localScale = SpawnedModelLocalScale;
                activeModel.SetActive(true);
            }
            else
            {
                // Fallback if CharacterData SO has no characterModelPrefab assigned
                GameObject targetSource = FindModelForCharacter(data, modelRoot);
                if (targetSource != null)
                {
                    if (targetSource.transform.IsChildOf(transform))
                    {
                        activeModel = targetSource;
                        activeModel.SetActive(true);
                    }
                    else
                    {
                        activeModel = Instantiate(targetSource, modelRoot);
                        activeModel.name = "CharacterModelInstance";
                        activeModel.transform.localPosition = SpawnedModelLocalPosition;
                        activeModel.transform.localRotation = Quaternion.identity;
                        activeModel.transform.localScale = SpawnedModelLocalScale;
                        activeModel.SetActive(true);
                    }
                }
                else if (modelRoot.childCount > 0)
                {
                    activeModel = modelRoot.GetChild(0).gameObject;
                    activeModel.SetActive(true);
                }
            }

            if (activeModel != null)
            {
                _activeCharacterModel = activeModel;

                Transform existingCapsule = transform.Find("Capsule");
                if (existingCapsule != null) existingCapsule.gameObject.SetActive(false);

                RebindModelAnimatorAndWeapons(activeModel, fallbackController);
            }
        }

        private GameObject FindModelForCharacter(CharacterData data, Transform modelRoot = null)
        {
            if (data == null) return null;

            // 1. If CharacterData.characterModelPrefab is assigned and a matching child exists under Model root
            //    (e.g. user assigned NPC_Elena_Medic_Rigged in CHAR_04_Marksman.asset and placed it under PlayerPrefabs/Model),
            //    prefer that matching child so swapping CharacterData.characterModelPrefab works immediately!
            if (data.characterModelPrefab != null && modelRoot != null)
            {
                string desiredName = data.characterModelPrefab.name;
                for (int i = 0; i < modelRoot.childCount; i++)
                {
                    Transform ch = modelRoot.GetChild(i);
                    if (ch != null && ch.name.Equals(desiredName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return ch.gameObject;
                    }
                }
            }

            if (characterModels == null || characterModels.Count == 0) return null;

            string targetId = data.characterId;

            // 2. Exact CharacterData ScriptableObject match in characterModels list
            for (int i = 0; i < characterModels.Count; i++)
            {
                CharacterModelEntry entry = characterModels[i];
                if (entry != null && entry.modelObject != null && entry.characterData == data)
                {
                    return entry.modelObject;
                }
            }

            // 3. Character ID match (either from entry.characterData.characterId or entry.characterId)
            if (!string.IsNullOrEmpty(targetId))
            {
                for (int i = 0; i < characterModels.Count; i++)
                {
                    CharacterModelEntry entry = characterModels[i];
                    if (entry == null || entry.modelObject == null) continue;

                    string entryId = entry.characterData != null && !string.IsNullOrEmpty(entry.characterData.characterId)
                        ? entry.characterData.characterId
                        : entry.characterId;

                    if (!string.IsNullOrEmpty(entryId) &&
                        entryId.Equals(targetId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.modelObject;
                    }
                }
            }

            return null;
        }

        private void RebindModelAnimatorAndWeapons(GameObject activeModel, RuntimeAnimatorController fallbackController = null)
        {
            if (activeModel == null) return;

            // Apply crisp cartoon ink silhouette outline to the active player character model
            CharacterToonOutline.AttachOrRefresh(activeModel);

            Animator modelAnim = activeModel.GetComponentInChildren<Animator>(true);
            if (modelAnim != null)
            {
                if (modelAnim.runtimeAnimatorController == null)
                {
                    modelAnim.runtimeAnimatorController = fallbackController ?? FindFallbackAnimatorControllerFromDatabase();
                }
                _animator = modelAnim;
                animator = modelAnim;
                _animator.applyRootMotion = false;
                _animator.Rebind();
            }

            // Check if the spawned character model already has baked Malee / Range weapon holders
            Transform ownMelee = FindDeepChild(activeModel.transform, "Malee")
                              ?? FindDeepChild(activeModel.transform, "Melee")
                              ?? FindDeepChild(activeModel.transform, "MeleeWeapon");
            Transform ownRange = FindDeepChild(activeModel.transform, "Range")
                              ?? FindDeepChild(activeModel.transform, "Ranged")
                              ?? FindDeepChild(activeModel.transform, "RangeWeapon");

            Transform activeRightHand = FindDeepChild(activeModel.transform, "RightHand")
                                     ?? FindDeepChild(activeModel.transform, "Hand_R")
                                     ?? FindDeepChild(activeModel.transform, "mixamorig:RightHand");

            // Prefer PlayerPrefabs' master Malee / Range weapon holders (full 18-weapon set) and move them cleanly
            // into the spawned character's RightHand bone with clean RightHand-local identity transform!
            if (meleeWeapon != null && activeRightHand != null)
            {
                if (ownMelee != null && ownMelee.gameObject != meleeWeapon)
                {
                    ownMelee.gameObject.SetActive(false);
                }
                meleeWeapon.transform.SetParent(activeRightHand, false);
                meleeWeapon.transform.localPosition = Vector3.zero;
                meleeWeapon.transform.localRotation = Quaternion.identity;
                meleeWeapon.transform.localScale = Vector3.one;
            }
            else if (ownMelee != null)
            {
                meleeWeapon = ownMelee.gameObject;
            }
            else if (activeRightHand != null)
            {
                meleeWeapon = CloneWeaponHolderFromDatabase("Malee", activeRightHand);
            }

            if (rangeWeapon != null && activeRightHand != null)
            {
                if (ownRange != null && ownRange.gameObject != rangeWeapon)
                {
                    ownRange.gameObject.SetActive(false);
                }
                rangeWeapon.transform.SetParent(activeRightHand, false);
                rangeWeapon.transform.localPosition = Vector3.zero;
                rangeWeapon.transform.localRotation = Quaternion.identity;
                rangeWeapon.transform.localScale = Vector3.one;
            }
            else if (ownRange != null)
            {
                rangeWeapon = ownRange.gameObject;
            }
            else if (activeRightHand != null)
            {
                rangeWeapon = CloneWeaponHolderFromDatabase("Range", activeRightHand);
            }
        }

        private static RuntimeAnimatorController FindFallbackAnimatorControllerFromDatabase()
        {
            if (CharacterDatabase.Instance == null || CharacterDatabase.Instance.Characters == null) return null;
            var all = CharacterDatabase.Instance.Characters;
            for (int i = 0; i < all.Count; i++)
            {
                GameObject prefab = all[i]?.characterModelPrefab;
                if (prefab == null) continue;
                Animator anim = prefab.GetComponentInChildren<Animator>(true);
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    return anim.runtimeAnimatorController;
                }
            }
            return null;
        }

        private GameObject CloneWeaponHolderFromDatabase(string holderName, Transform targetHand)
        {
            if (targetHand == null || CharacterDatabase.Instance == null || CharacterDatabase.Instance.Characters == null) return null;
            var all = CharacterDatabase.Instance.Characters;
            Transform bestTemplate = null;
            int bestChildCount = -1;

            // Prefer the template prefab with the most calibrated weapons (e.g. Chibi_PunkGirl_Rigged with all 18 weapons)
            for (int i = 0; i < all.Count; i++)
            {
                GameObject prefab = all[i]?.characterModelPrefab;
                if (prefab == null) continue;
                Transform found = FindDeepChild(prefab.transform, holderName);
                if (found != null && found.childCount > bestChildCount)
                {
                    bestTemplate = found;
                    bestChildCount = found.childCount;
                }
            }

            if (bestTemplate == null) return null;

            GameObject cloned = Instantiate(bestTemplate.gameObject, targetHand, false);
            cloned.name = holderName;
            cloned.transform.localPosition = bestTemplate.localPosition;
            cloned.transform.localRotation = bestTemplate.localRotation;
            cloned.transform.localScale = bestTemplate.localScale;
            cloned.SetActive(false);
            return cloned;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Populate Character Models List")]
        private void EditorAutoPopulateCharacterModels()
        {
            if (characterModelRoot == null)
            {
                characterModelRoot = transform.Find("Model");
            }

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets/Resources_Addressable/Database/Characters" });
            System.Array.Sort(guids, (a, b) => string.Compare(
                UnityEditor.AssetDatabase.GUIDToAssetPath(a),
                UnityEditor.AssetDatabase.GUIDToAssetPath(b),
                System.StringComparison.OrdinalIgnoreCase));

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                CharacterData cd = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (cd == null) continue;

                bool exists = characterModels.Exists(e => e != null && (e.characterData == cd ||
                    (!string.IsNullOrEmpty(e.characterId) && e.characterId.Equals(cd.characterId, System.StringComparison.OrdinalIgnoreCase))));
                if (exists) continue;

                GameObject matchedChild = null;
                if (characterModelRoot != null)
                {
                    string hint = cd.characterId.ToUpperInvariant() switch
                    {
                        "VANGUARD" => "TacticalGirl",
                        "JUGGERNAUT" => "HeavyStriker",
                        "PHANTOM" => "BlueOperative",
                        "MARKSMAN" => "CurlyGirl",
                        "REBEL" => "PunkGirl",
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(hint))
                    {
                        for (int i = 0; i < characterModelRoot.childCount; i++)
                        {
                            Transform ch = characterModelRoot.GetChild(i);
                            if (ch != null && ch.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matchedChild = ch.gameObject;
                                break;
                            }
                        }
                    }
                }

                characterModels.Add(new CharacterModelEntry
                {
                    characterData = cd,
                    characterId = cd.characterId,
                    modelObject = matchedChild != null ? matchedChild : cd.characterModelPrefab
                });
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void InitShiftAbility()
        {
            if (activeCharacterData == null) return;

            switch (activeCharacterData.shiftAbilityType)
            {
                case ShiftAbilityType.AdrenalineRush:
                    _activeShiftAbility = new AdrenalineRushAbility();
                    break;
                case ShiftAbilityType.UnstoppableCharge:
                    _activeShiftAbility = new UnstoppableChargeAbility();
                    break;
                case ShiftAbilityType.ShadowStep:
                    _activeShiftAbility = new ShadowStepAbility();
                    break;
                case ShiftAbilityType.EagleEye:
                    _activeShiftAbility = new EagleEyeAbility();
                    break;
                case ShiftAbilityType.GrapplingHook:
                    _activeShiftAbility = new GrapplingHookAbility();
                    break;
                case ShiftAbilityType.CycloneSwing:
                    _activeShiftAbility = new CycloneSwingAbility();
                    break;
                case ShiftAbilityType.SatchelVault:
                    _activeShiftAbility = new SatchelVaultAbility();
                    break;
                default:
                    _activeShiftAbility = new AdrenalineRushAbility();
                    break;
            }

            _activeShiftAbility.Initialize(this);
        }

        public void DrainHunger(float amount)
        {
            float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.SurvivalDrainMultiplier : 1f;
            currentHunger = Mathf.Max(0f, currentHunger - amount * hungerDepletionMultiplier * weatherMult);
            OnHungerChanged?.Invoke();
        }

        public void RestoreHunger(float amount)
        {
            float maxH = (activeCharacterData != null && activeCharacterData.maxHunger > 0f) ? activeCharacterData.maxHunger : 100f;
            currentHunger = Mathf.Min(maxH, currentHunger + amount);
            OnHungerChanged?.Invoke();
        }

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (_animator == null)
            {
                _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            }
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
            InitMuzzleFlashLight();
            {
            }

            // Automatically attach CameraFollow and CameraObstacleFader components to main camera if not present (LocalPlayer only)
            Camera mainCam = Camera.main;
            if (!IsRemotePlayer && mainCam != null)
            {
                if (mainCam.GetComponent<CameraFollow>() == null)
                {
                    mainCam.gameObject.AddComponent<CameraFollow>();
                    Debug.Log("[PlayerController] Dynamically added CameraFollow component to Main Camera.");
                }
                if (mainCam.GetComponent<CameraObstacleFader>() == null)
                {
                    mainCam.gameObject.AddComponent<CameraObstacleFader>();
                    Debug.Log("[PlayerController] Dynamically added CameraObstacleFader component to Main Camera.");
                }
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            // Bind Health component or dynamically attach it
            if (_health == null)
            {
                _health = GetComponent<Health>();
                if (_health == null)
                {
                    _health = gameObject.AddComponent<Health>();
                }
            }
            _health.onDeath.AddListener(HandlePlayerDeath);
            _health.onDamageTaken.AddListener(HandleDamageTaken);

            // Bind Inventory component or dynamically attach it
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null)
            {
                _inventory = gameObject.AddComponent<PlayerInventory>();
            }

            // Automatically setup PlayerInput component (LocalPlayer only)
            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (IsRemotePlayer)
            {
                if (playerInput != null) playerInput.enabled = false;
            }
            else
            {
                if (playerInput == null)
                {
                    playerInput = gameObject.AddComponent<PlayerInput>();
                }

                // Set notification behavior to SendMessages
                playerInput.notificationBehavior = PlayerNotifications.SendMessages;

                // Load and link the default Input Actions asset
                if (playerInput.actions == null)
                {
                    if (inputActions != null)
                    {
                        playerInput.actions = inputActions;
                    }
                    else
                    {
                        // Fallback to loading from Resources (for builds)
                        playerInput.actions = Resources.Load<InputActionAsset>("InputSystem_Actions");
                    }

#if UNITY_EDITOR
                    if (playerInput.actions == null)
                    {
                        InputActionAsset defaultActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                        if (defaultActions != null)
                        {
                            playerInput.actions = defaultActions;
                            inputActions = defaultActions;
                        }
                    }
#endif
                }

                if (playerInput.actions != null)
                {
                    playerInput.currentActionMap = playerInput.actions.FindActionMap("Player");
                    playerInput.currentActionMap?.Enable();
                    Debug.Log($"PlayerController: Linked InputActions asset: {playerInput.actions.name}");
                }
                else
                {
                    Debug.LogError("PlayerController: InputActions asset is NOT linked! Player input will not work in standalone builds. Please assign inputActions in the Inspector or place the asset in a Resources folder.");
                }
            }

            _currentReserveAmmo = startingReserveAmmo;
            _currentMagazine = magazineSize;
            InitializeDefaultWeapons();
            InitializeDefaultMeleeWeapons();
            if (activeCharacterData != null)
            {
                ApplyStartingCharacterLoadout();
            }
            OnAmmoChanged?.Invoke();

            EnsureWeaponVisualReferences();
            if (CurrentWeapon != null)
            {
                ShowRangedWeaponVisual();
            }
            else if (CurrentMeleeWeapon != null)
            {
                ShowMeleeWeaponVisual();
            }
            else
            {
                if (meleeWeapon != null) meleeWeapon.SetActive(false);
                if (rangeWeapon != null) rangeWeapon.SetActive(false);
            }

            // เตรียมความพร้อม QuickSlot ตั้งแต่เริ่มเกม แม้ว่า Inventory จะยังไม่ได้เปิด
            if (QuickSlot != null)
            {
                QuickSlot.EnsureInitialized();
            }

            // SetupScreenBoundaries(); // Disabled: Using manual scene boundaries/walls instead
        }

        private void OnDestroy()
        {
            TheLastEmpire.Network.PlayerRegistry.Unregister(this);
            if (Instance == this)
            {
                Instance = null;
            }

            if (_health != null)
            {
                _health.onDeath.RemoveListener(HandlePlayerDeath);
                _health.onDamageTaken.RemoveListener(HandleDamageTaken);
            }

            _activeShiftAbility?.OnDestroy();
            if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.gameObject != null)
                Destroy(_throwRangeSpriteRenderer.gameObject);
            if (_aoeSpriteRenderer != null && _aoeSpriteRenderer.gameObject != null)
                Destroy(_aoeSpriteRenderer.gameObject);
            if (_sniperReticleSprite != null && _sniperReticleSprite.gameObject != null)
                Destroy(_sniperReticleSprite.gameObject);
            if (_sniperChevronSprite != null && _sniperChevronSprite.gameObject != null)
                Destroy(_sniperChevronSprite.gameObject);
        }

        private void HandlePlayerDeath()
        {
            // Co-op Downed & Revive System (Section 4.7): Enter 30s Downed state if party has teammates alive
            if (TryEnterCoopDownedState())
            {
                return;
            }
            // 1. Freeze corpse physics completely so enemies cannot push or slide it around
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            // 2. Ignore collisions between player and all enemies so enemies pass through corpse freely
            Collider[] playerColliders = GetComponentsInChildren<Collider>();
            BaseEnemyAI[] enemies = Object.FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
            foreach (BaseEnemyAI enemy in enemies)
            {
                if (enemy == null) continue;
                Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>();
                foreach (Collider pCol in playerColliders)
                {
                    foreach (Collider eCol in enemyColliders)
                    {
                        if (pCol != null && eCol != null)
                        {
                            Physics.IgnoreCollision(pCol, eCol, true);
                        }
                    }
                }
            }

            // 3. Turn off muzzle flash light if active and play 1.5s dramatic Death animation
            if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
            _muzzleLightTimer = 0f;

            if (_animator == null)
                _animator = animator != null ? animator : GetComponentInChildren<Animator>();

            if (_animator != null)
            {
                _isSitting = false;
                _animator.SetBool("IsSitting", false);
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsRunning", false);
                _animator.SetBool("IsDashing", false);
                if (_animator.layerCount > 1)
                {
                    _animator.Play("UpperBody_Empty", 1, 0f);
                }
                _animator.ResetTrigger("Die");
                _animator.SetTrigger("Die");
                _animator.CrossFadeInFixedTime("Death", 0.06f, 0, 0f);
            }

            // 4. Hide any active aiming or throwing indicators
            if (_throwRangeIndicator != null && _throwRangeIndicator.gameObject.activeSelf) _throwRangeIndicator.gameObject.SetActive(false);
            if (_aoeIndicator != null && _aoeIndicator.gameObject.activeSelf) _aoeIndicator.gameObject.SetActive(false);
            if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.gameObject.activeSelf) _throwRangeSpriteRenderer.gameObject.SetActive(false);
            if (_aoeSpriteRenderer != null && _aoeSpriteRenderer.gameObject.activeSelf) _aoeSpriteRenderer.gameObject.SetActive(false);
            if (_laserSightIndicator != null && _laserSightIndicator.gameObject.activeSelf) _laserSightIndicator.gameObject.SetActive(false);
            if (_laserTargetDot != null && _laserTargetDot.gameObject.activeSelf) _laserTargetDot.gameObject.SetActive(false);
            if (_sniperReticleSprite != null && _sniperReticleSprite.gameObject.activeSelf) _sniperReticleSprite.gameObject.SetActive(false);
            if (_sniperChevronSprite != null && _sniperChevronSprite.gameObject.activeSelf) _sniperChevronSprite.gameObject.SetActive(false);
        }

        public bool IsControlLocked { get; private set; } = false;

        public void SetControlLocked(bool locked)
        {
            IsControlLocked = locked;
            _moveInput = Vector2.zero;
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsRunning", false);
            }
            _activeShiftAbility?.OnShiftReleased();
            _isSprinting = false;
            _fireBufferTimer = 0f;
        }

        private void Update()
        {
            if (IsRemotePlayer)
            {
                UpdateRemotePlayerVisuals();
                return;
            }

            UpdateCoopDownedAndRevive();
            if (IsDowned)
            {
                UpdateAnimation();
                return;
            }

            if (IsControlLocked)
            {
                _moveInput = Vector2.zero;
                if (_rb != null)
                {
                    _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                }
                if (_animator != null)
                {
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetBool("IsRunning", false);
                }
                return;
            }

            if (_health != null && _health.IsDead)
            {
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.constraints = RigidbodyConstraints.FreezeAll;
                }
                return;
            }

            if (_shootSprintLockTimer > 0f) _shootSprintLockTimer -= Time.deltaTime;
            bool isShootingNow = _shootSprintLockTimer > 0f || _shootAnimLayerTimer > 0f || (Mouse.current != null && Mouse.current.leftButton.isPressed && CurrentWeapon != null && !_isReloading);
            if (isShootingNow) _activeShiftAbility?.OnShiftReleased();

            bool isShiftOnCooldown = _activeShiftAbility != null && _activeShiftAbility.CooldownRemaining > 0f;
            bool isDehydrated = currentWater <= 0f;

            if (Keyboard.current != null)
            {
                if (!isShootingNow && !isShiftOnCooldown && !isDehydrated && Keyboard.current.shiftKey.wasPressedThisFrame)
                {
                    _activeShiftAbility?.OnShiftPressed();
                }
                if (!isShootingNow && !isShiftOnCooldown && !isDehydrated && Keyboard.current.shiftKey.isPressed)
                {
                    _activeShiftAbility?.OnShiftHeld();
                }
                if (Keyboard.current.shiftKey.wasReleasedThisFrame || isDehydrated)
                {
                    _activeShiftAbility?.OnShiftReleased();
                }
            }
            _activeShiftAbility?.UpdateAbility();

            bool isAimingSlow = _activeShiftAbility is EagleEyeAbility && _activeShiftAbility.IsActive;
            bool canSprint = !isShiftOnCooldown && !isDehydrated;
            _isSprinting = canSprint && !isShootingNow && !isAimingSlow && Keyboard.current != null && Keyboard.current.shiftKey.isPressed && _moveInput.sqrMagnitude > 0.01f;
            UpdateHunger();
            UpdateWater();
            UpdateSanity();
            // Allow opening/closing the inventory with I or Escape even when game timescale is paused
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame)
                {
                    ToggleInventoryMenu();
                    return;
                }

                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    // ถ้ากำลังอยู่ในโหมดวางสิ่งปลูกสร้าง (Build Mode) ปุ่ม R จะใช้หมุนโมเดลสิ่งปลูกสร้าง ห้ามรีโหลดปืน
                    if (BuildingPlacer.Instance == null || !BuildingPlacer.Instance.IsInBuildMode)
                    {
                        TryStartReload();
                    }
                }
            }

            bool isMenuOpen = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || 
                              (LootUI.HasInstance && LootUI.Instance.IsOpen) ||
                              (StorageUI.HasInstance && StorageUI.Instance.IsOpen) ||
                              (WeaponUpgradeUI.HasInstance && WeaponUpgradeUI.Instance.IsOpen) ||
                              (TheLastEmpire.UI.DialogueUI.Instance != null && TheLastEmpire.UI.DialogueUI.Instance.IsDialogueActive);
            if (isMenuOpen)
            {
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                }
                if (_animator != null)
                {
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetBool("IsRunning", false);
                }
                if (LootUI.Instance != null)
                {
                    LootUI.Instance.HidePrompt();
                }
                return; // Suppress normal movements, aiming, and updates when inventory or loot panel is open
            }

            if (_startupDelay > 0f)
            {
                _startupDelay -= Time.deltaTime;
                return;
            }

            // Reduce cooldown timers
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
            if (_fireCooldownTimer > 0f) _fireCooldownTimer -= Time.deltaTime;
            UpdateMuzzleFlashLight();
            if (_fireBufferTimer > 0f) _fireBufferTimer -= Time.deltaTime;
            if (_meleeCooldownTimer > 0f) _meleeCooldownTimer -= Time.deltaTime;
            if (_meleeBufferTimer > 0f) _meleeBufferTimer -= Time.deltaTime;
            if (_meleeComboStep > 0 && Time.time - _lastMeleeSwingTime > MeleeComboResetWindow)
            {
                _meleeComboStep = 0;
            }

            CheckBoundaries();

            if (_isVaulting)
            {
                UpdateVehicleVault();
                UpdateAnimation();
                return;
            }

            if (_isDashing)
            {
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0f)
                {
                    EndDash();
                }
                UpdateAnimation();
                return; // Disable normal actions and movement while dashing
            }

            UpdateAimDirection();
            HandleDirectInput();

            // Sync unrotated screen boundaries container to camera position on the X/Z plane
            if (_boundaryContainer != null && Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                _boundaryContainer.transform.position = new Vector3(camPos.x, 0f, camPos.z);
            }

            UpdateInteractionPrompt();
            UpdateIndicators();
            UpdateAnimation();
        }

        private void InitMuzzleFlashLight()
        {
            if (muzzleFlashLight == null)
            {
                Transform existing = transform.Find("MuzzleFlashLight");
                if (existing != null)
                {
                    muzzleFlashLight = existing.GetComponent<Light>();
                }
            }

            if (muzzleFlashLight == null)
            {
                GameObject lightObj = new GameObject("MuzzleFlashLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.35f, 0.75f);
                muzzleFlashLight = lightObj.AddComponent<Light>();
            }

            muzzleFlashLight.type = LightType.Point;
            muzzleFlashLight.color = muzzleLightColor;
            muzzleFlashLight.range = muzzleLightRange;
            muzzleFlashLight.intensity = 0f;
            muzzleFlashLight.shadows = LightShadows.Soft;
            muzzleFlashLight.renderMode = LightRenderMode.ForcePixel;
            muzzleFlashLight.enabled = false;
        }

        private void TriggerMuzzleFlashLight()
        {
            if (muzzleFlashLight == null) InitMuzzleFlashLight();
            if (muzzleFlashLight == null) return;

            muzzleFlashLight.transform.position = transform.position + _aimDirection * 0.75f + Vector3.up * 0.35f;
            muzzleFlashLight.color = muzzleLightColor;
            muzzleFlashLight.range = muzzleLightRange;
            muzzleFlashLight.intensity = muzzleLightIntensity;
            muzzleFlashLight.enabled = true;
            _muzzleLightTimer = muzzleLightDuration;
        }

        private void UpdateMuzzleFlashLight()
        {
            if (_muzzleLightTimer > 0f)
            {
                _muzzleLightTimer -= Time.deltaTime;
                if (muzzleFlashLight != null)
                {
                    float t = Mathf.Clamp01(_muzzleLightTimer / Mathf.Max(0.001f, muzzleLightDuration));
                    muzzleFlashLight.intensity = muzzleLightIntensity * t;
                    muzzleFlashLight.transform.position = transform.position + _aimDirection * 0.75f + Vector3.up * 0.35f;
                    if (_muzzleLightTimer <= 0f)
                    {
                        muzzleFlashLight.intensity = 0f;
                        muzzleFlashLight.enabled = false;
                    }
                }
            }
        }

        private void UpdateAnimation()
        {
            if (_animator == null)
            {
                _animator = animator != null ? animator : GetComponentInChildren<Animator>();
                if (_animator == null) return;
            }

            float moveMagnitude = _moveInput.magnitude;
            if (_isSitting && moveMagnitude > 0.1f)
            {
                SetSitting(false);
            }
            if (_isSleeping && moveMagnitude > 0.1f)
            {
                SetSleeping(false);
            }
            if (_isSleeping)
            {
                _animator.SetFloat("Speed", 0f);
                _animator.SetBool("IsRunning", false);
                _animator.SetBool("IsSitting", false);
                _animator.SetBool("HasGun", false);
                if (_animator.layerCount > 1) _animator.SetLayerWeight(1, 0f);
                return;
            }
            _animator.SetFloat("Speed", moveMagnitude);
            _animator.SetBool("IsRunning", _isSprinting && moveMagnitude > 0.1f);
            _animator.SetBool("IsSitting", _isSitting);
            _animator.SetBool("HasGun", CurrentWeapon != null && !_isSitting);

            // Drive unique per-character Shift Ability animations (0..6 matching ShiftAbilityType)
            bool isShiftActive = _activeShiftAbility != null && _activeShiftAbility.IsActive && !_isSitting;
            int shiftTypeIndex = activeCharacterData != null ? (int)activeCharacterData.shiftAbilityType : 0;
            if (HasAnimatorParameter(_animator, "IsShiftActive"))
                _animator.SetBool("IsShiftActive", isShiftActive);
            if (HasAnimatorParameter(_animator, "ShiftType"))
                _animator.SetInteger("ShiftType", shiftTypeIndex);

            // Full-body action Shift abilities suppress UpperBody shooting layer; AdrenalineRush(0) & EagleEye(3) allow shooting
            bool isFullBodyShift = isShiftActive && shiftTypeIndex != (int)ShiftAbilityType.AdrenalineRush && shiftTypeIndex != (int)ShiftAbilityType.EagleEye;

            if (_shootAnimLayerTimer > 0f) _shootAnimLayerTimer -= Time.deltaTime;
            if (_meleeAnimTimer > 0f) _meleeAnimTimer -= Time.deltaTime;
            bool isSatchelRolling = _activeShiftAbility is SatchelVaultAbility satchelAbility && satchelAbility.IsVaulting;
            if (_animator.layerCount > 1)
            {
                bool showUpperShoot = _shootAnimLayerTimer > 0f && _meleeAnimTimer <= 0f && !_isDashing && !_isVaulting && !isSatchelRolling && !_isSitting && !isFullBodyShift;
                _animator.SetLayerWeight(1, showUpperShoot ? 1f : 0f);
            }
            _animator.SetBool("IsDashing", _isDashing || _isVaulting || isSatchelRolling);
        }

        private static bool HasAnimatorParameter(Animator anim, string paramName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            var ps = anim.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == paramName) return true;
            }
            return false;
        }

        public void ToggleSit()
        {
            SetSitting(!_isSitting);
        }

        public void SetSitting(bool sit)
        {
            if (sit && _isSleeping) SetSleeping(false);
            if (_isSitting == sit) return;
            _isSitting = sit;

            if (_animator == null)
                _animator = animator != null ? animator : GetComponentInChildren<Animator>();

            if (_isSitting)
            {
                _moveInput = Vector2.zero;
                _isSprinting = false;
                _shootAnimLayerTimer = 0f;
                _meleeAnimTimer = 0f;
                if (_rb != null) _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                if (meleeWeapon != null) meleeWeapon.SetActive(false);
                if (rangeWeapon != null) rangeWeapon.SetActive(false);
                if (_animator != null)
                {
                    _animator.SetBool("IsSitting", true);
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetBool("IsRunning", false);
                    if (_animator.layerCount > 1)
                    {
                        _animator.SetLayerWeight(1, 0f);
                        _animator.Play("UpperBody_Empty", 1, 0f);
                    }
                    _animator.CrossFadeInFixedTime("Sit", 0.18f, 0);
                }
            }
            else
            {
                if (_animator != null)
                {
                    _animator.SetBool("IsSitting", false);
                }
                if (CurrentWeapon != null)
                {
                    ShowRangedWeaponVisual();
                }
                else if (CurrentMeleeWeapon != null)
                {
                    ShowMeleeWeaponVisual();
                }
            }
        }

        public void SetSleeping(bool sleep)
        {
            if (sleep && _isSitting) SetSitting(false);
            if (_isSleeping == sleep) return;
            _isSleeping = sleep;

            if (_animator == null)
                _animator = animator != null ? animator : GetComponentInChildren<Animator>();

            if (_isSleeping)
            {
                _moveInput = Vector2.zero;
                _isSprinting = false;
                _shootAnimLayerTimer = 0f;
                _meleeAnimTimer = 0f;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                if (meleeWeapon != null) meleeWeapon.SetActive(false);
                if (rangeWeapon != null) rangeWeapon.SetActive(false);
                if (_animator != null)
                {
                    _animator.SetBool("IsSitting", false);
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetBool("IsRunning", false);
                    if (_animator.layerCount > 1)
                    {
                        _animator.SetLayerWeight(1, 0f);
                        _animator.Play("UpperBody_Empty", 1, 0f);
                    }
                    int sleepHash = Animator.StringToHash("Sleep");
                    if (_animator.HasState(0, sleepHash))
                    {
                        _animator.CrossFadeInFixedTime(sleepHash, 0.18f, 0);
                    }
                    else
                    {
                        _animator.CrossFadeInFixedTime("Sit", 0.18f, 0);
                    }
                }
            }
            else
            {
                if (_animator != null)
                {
                    _animator.CrossFadeInFixedTime("Idle", 0.15f, 0);
                }
                if (CurrentWeapon != null)
                {
                    ShowRangedWeaponVisual();
                }
                else if (CurrentMeleeWeapon != null)
                {
                    ShowMeleeWeaponVisual();
                }
            }
        }

        private void UpdateIndicators()
        {
            if (IsRemotePlayer) return;

            bool isThrowing = _heldItemType == HeldItemType.Ranged &&
                              CurrentWeapon != null && CurrentWeapon.projectilePrefab != null &&
                              CurrentWeapon.projectilePrefab.Trajectory == TrajectoryMode.Arced;

            if (isThrowing)
            {
                float throwRadius = CurrentWeapon.range;
                float aoeRadius = CurrentWeapon.projectilePrefab.ExplosionRadius;

                // Ensure new UI_Hitbox_Sprites are bound even on runtime-spawned or older prefabs
                if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.sprite == null)
                {
                    Sprite rangeSp = throwRangeSprite != null ? throwRangeSprite : HitboxSpriteLibrary.DashedCrosshair;
                    if (rangeSp != null) _throwRangeSpriteRenderer.sprite = rangeSp;
                }
                if (_aoeSpriteRenderer != null && _aoeSpriteRenderer.sprite == null)
                {
                    Sprite aoeSp = aoeIndicatorSprite != null ? aoeIndicatorSprite : HitboxSpriteLibrary.HazardCircle;
                    if (aoeSp != null) _aoeSpriteRenderer.sprite = aoeSp;
                }

                // 1. Throw Range Indicator (Around Player — original sprite color, no spin)
                if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.sprite != null)
                {
                    if (!_throwRangeSpriteRenderer.gameObject.activeSelf) _throwRangeSpriteRenderer.gameObject.SetActive(true);
                    if (_throwRangeIndicator != null && _throwRangeIndicator.gameObject.activeSelf) _throwRangeIndicator.gameObject.SetActive(false);

                    _throwRangeSpriteRenderer.transform.position = new Vector3(transform.position.x, 0.04f, transform.position.z);
                    _throwRangeSpriteRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    _throwRangeSpriteRenderer.color = HitboxSpriteLibrary.DefaultOriginalColor;
                    HitboxSpriteLibrary.SetWorldDiameter(_throwRangeSpriteRenderer, throwRadius * 2f);
                }
                else if (_throwRangeIndicator != null)
                {
                    if (!_throwRangeIndicator.gameObject.activeSelf) _throwRangeIndicator.gameObject.SetActive(true);
                    if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.gameObject.activeSelf) _throwRangeSpriteRenderer.gameObject.SetActive(false);
                    DrawCircle(_throwRangeIndicator, transform.position, throwRadius);
                }

                // 2. AOE Radius Indicator (At Aim Target Point — UI_Hitbox_Sprites_0 original color, no spin)
                if (_aoeSpriteRenderer != null && _aoeSpriteRenderer.sprite != null)
                {
                    if (!_aoeSpriteRenderer.gameObject.activeSelf) _aoeSpriteRenderer.gameObject.SetActive(true);
                    if (_aoeIndicator != null && _aoeIndicator.gameObject.activeSelf) _aoeIndicator.gameObject.SetActive(false);

                    _aoeSpriteRenderer.transform.position = new Vector3(_aimTargetPoint.x, 0.06f, _aimTargetPoint.z);
                    _aoeSpriteRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    HitboxSpriteLibrary.SetWorldDiameter(_aoeSpriteRenderer, aoeRadius * 2f);
                    _aoeSpriteRenderer.color = HitboxSpriteLibrary.DefaultOriginalColor;
                }
                else if (_aoeIndicator != null)
                {
                    if (!_aoeIndicator.gameObject.activeSelf) _aoeIndicator.gameObject.SetActive(true);
                    if (_aoeSpriteRenderer != null && _aoeSpriteRenderer.gameObject.activeSelf) _aoeSpriteRenderer.gameObject.SetActive(false);
                    DrawCircle(_aoeIndicator, _aimTargetPoint, aoeRadius);
                }
            }
            else
            {
                if (_throwRangeIndicator != null && _throwRangeIndicator.gameObject.activeSelf) _throwRangeIndicator.gameObject.SetActive(false);
                if (_aoeIndicator != null && _aoeIndicator.gameObject.activeSelf) _aoeIndicator.gameObject.SetActive(false);
                if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.gameObject.activeSelf) _throwRangeSpriteRenderer.gameObject.SetActive(false);
                if (_aoeSpriteRenderer != null && _aoeSpriteRenderer.gameObject.activeSelf) _aoeSpriteRenderer.gameObject.SetActive(false);
            }

            // 3. Sniper Aiming & Eagle Eye RED Laser Sight + UI_Hitbox_Sprites_2 Red Crosshair Reticle
            bool isEagleEyeAiming = _activeShiftAbility is EagleEyeAbility eagle && eagle.IsActive;
            bool isHoldingSniperRifle = _heldItemType == HeldItemType.Ranged &&
                                        CurrentWeapon != null &&
                                        !string.IsNullOrEmpty(CurrentWeapon.weaponName) &&
                                        CurrentWeapon.weaponName.IndexOf("sniper", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool showSniperRedAim = isEagleEyeAiming || isHoldingSniperRifle;

            if (showSniperRedAim && !_isSitting && !_isSleeping)
            {
                Color sniperLaserRed = new Color(1f, 0.08f, 0.06f, isEagleEyeAiming ? 0.98f : 0.85f);
                if (_laserSightIndicator == null)
                {
                    _laserSightIndicator = CreateLaserLineRenderer("LaserSightIndicator", sniperLaserRed, 0.05f);
                }
                if (_sniperReticleSprite == null)
                {
                    _sniperReticleSprite = HitboxSpriteLibrary.CreateGroundSpriteIndicator(
                        "SniperReticleSprite", null, HitboxSpriteLibrary.DashedCrosshair, HitboxSpriteLibrary.SniperRed, 8);
                    _sniperReticleSprite.gameObject.SetActive(false);
                }
                else if (_sniperReticleSprite.sprite == null)
                {
                    _sniperReticleSprite.sprite = HitboxSpriteLibrary.DashedCrosshair;
                }

                if (_sniperChevronSprite == null)
                {
                    _sniperChevronSprite = HitboxSpriteLibrary.CreateGroundSpriteIndicator(
                        "SniperChevronSprite", null, HitboxSpriteLibrary.DirectionalChevrons, HitboxSpriteLibrary.SniperRed, 7);
                    _sniperChevronSprite.gameObject.SetActive(false);
                }
                else if (_sniperChevronSprite.sprite == null)
                {
                    _sniperChevronSprite.sprite = HitboxSpriteLibrary.DirectionalChevrons;
                }

                if (!_laserSightIndicator.gameObject.activeSelf) _laserSightIndicator.gameObject.SetActive(true);
                if (_laserTargetDot != null && _laserTargetDot.gameObject.activeSelf) _laserTargetDot.gameObject.SetActive(false);

                float targetRange = 26f;
                if (CurrentWeapon != null)
                {
                    targetRange = CurrentWeapon.range;
                    if (ItemDatabase.Instance != null)
                    {
                        ItemData item = ItemDatabase.Instance.GetItemByName(CurrentWeapon.weaponName);
                        if (item != null && item.attackRadius > 0.01f)
                        {
                            targetRange = item.attackRadius;
                        }
                    }
                    if (CurrentWeapon.projectilePrefab != null && CurrentWeapon.projectilePrefab.Speed > 0.01f)
                    {
                        targetRange = Mathf.Max(targetRange, CurrentWeapon.range * CurrentWeapon.projectilePrefab.Speed);
                    }
                }
                if (isEagleEyeAiming)
                {
                    float rangeMult = (activeCharacterData != null) ? activeCharacterData.eagleEyeRangeMultiplier : 2.0f;
                    targetRange *= rangeMult;
                }
                targetRange = Mathf.Clamp(targetRange, 14f, 45f);

                Vector3 startPos = transform.position + Vector3.up * 0.16f + _aimDirection * 0.45f;
                Vector3 endPos = startPos + _aimDirection * targetRange;
                bool lockedOnEnemy = false;

                int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
                if (Physics.Raycast(startPos + Vector3.up * 0.35f, _aimDirection, out RaycastHit hit, targetRange, layerMask, QueryTriggerInteraction.Ignore))
                {
                    endPos = new Vector3(hit.point.x, startPos.y, hit.point.z);
                    lockedOnEnemy = hit.collider != null && (hit.collider.CompareTag("Enemy") || hit.collider.GetComponentInParent<BaseEnemyAI>() != null);
                }

                // Bright RED Sniper Laser Line
                _laserSightIndicator.startWidth = isEagleEyeAiming ? 0.06f : 0.042f;
                _laserSightIndicator.endWidth = isEagleEyeAiming ? 0.035f : 0.025f;
                _laserSightIndicator.startColor = sniperLaserRed;
                _laserSightIndicator.endColor = new Color(1f, 0.06f, 0.06f, lockedOnEnemy ? 0.95f : 0.50f);
                if (_laserSightIndicator.material != null) _laserSightIndicator.material.color = sniperLaserRed;
                _laserSightIndicator.SetPosition(0, startPos);
                _laserSightIndicator.SetPosition(1, endPos);

                // Crisp RED Sniper Crosshair Reticle (UI_Hitbox_Sprites_2) at target point (no spin, 25% alpha)
                if (_sniperReticleSprite.sprite != null)
                {
                    if (!_sniperReticleSprite.gameObject.activeSelf) _sniperReticleSprite.gameObject.SetActive(true);
                    _sniperReticleSprite.transform.position = new Vector3(endPos.x, 0.09f, endPos.z);
                    _sniperReticleSprite.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    float reticleDiameter = lockedOnEnemy ? 1.35f : (isEagleEyeAiming ? 1.20f : 0.95f);
                    HitboxSpriteLibrary.SetWorldDiameter(_sniperReticleSprite, reticleDiameter);
                    _sniperReticleSprite.color = HitboxSpriteLibrary.SniperRed;
                }
                else if (_laserTargetDot != null)
                {
                    if (!_laserTargetDot.gameObject.activeSelf) _laserTargetDot.gameObject.SetActive(true);
                    DrawCircle(_laserTargetDot, endPos, 0.25f);
                }

                // Red directional chevron indicator ahead of the sniper barrel (25% alpha)
                if (_sniperChevronSprite.sprite != null && Vector3.Distance(startPos, endPos) > 1.8f)
                {
                    if (!_sniperChevronSprite.gameObject.activeSelf) _sniperChevronSprite.gameObject.SetActive(true);
                    Vector3 chevPos = startPos + _aimDirection * 1.45f;
                    _sniperChevronSprite.transform.position = new Vector3(chevPos.x, 0.08f, chevPos.z);
                    _sniperChevronSprite.transform.rotation = HitboxSpriteLibrary.GetGroundChevronRotation(_aimDirection);
                    float baseW = _sniperChevronSprite.sprite.bounds.size.x;
                    float baseH = _sniperChevronSprite.sprite.bounds.size.y;
                    if (baseW > 0.001f && baseH > 0.001f)
                    {
                        _sniperChevronSprite.transform.localScale = new Vector3(1.15f / baseW, 0.58f / baseH, 1f);
                    }
                    _sniperChevronSprite.color = HitboxSpriteLibrary.SniperRed;
                }
                else if (_sniperChevronSprite != null && _sniperChevronSprite.gameObject.activeSelf)
                {
                    _sniperChevronSprite.gameObject.SetActive(false);
                }
            }
            else
            {
                if (_laserSightIndicator != null && _laserSightIndicator.gameObject.activeSelf)
                {
                    _laserSightIndicator.gameObject.SetActive(false);
                }
                if (_laserTargetDot != null && _laserTargetDot.gameObject.activeSelf)
                {
                    _laserTargetDot.gameObject.SetActive(false);
                }
                if (_sniperReticleSprite != null && _sniperReticleSprite.gameObject.activeSelf)
                {
                    _sniperReticleSprite.gameObject.SetActive(false);
                }
                if (_sniperChevronSprite != null && _sniperChevronSprite.gameObject.activeSelf)
                {
                    _sniperChevronSprite.gameObject.SetActive(false);
                }
            }
        }

        private float _knockbackTimer = 0f;
        private Vector3 _knockbackVelocity = Vector3.zero;

        /// <summary>
        /// Applies an external knockback force to the player (e.g. from boss stomp or heavy attacks).
        /// </summary>
        public void ApplyKnockback(Vector3 force, float duration = 0.35f)
        {
            _knockbackTimer = duration;
            _knockbackVelocity = force;
        }

        private void FixedUpdate()
        {
            if (IsRemotePlayer)
            {
                FixedUpdateRemotePlayer();
                return;
            }

            if (IsDowned)
            {
                if (_rb != null) _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                return;
            }

            if (_health != null && _health.IsDead)
            {
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.constraints = RigidbodyConstraints.FreezeAll;
                }
                return;
            }

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.fixedDeltaTime;
                _rb.linearVelocity = new Vector3(_knockbackVelocity.x, _rb.linearVelocity.y, _knockbackVelocity.z);
                _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
                return;
            }

            if (_isVaulting)
            {
                if (_rb != null) _rb.linearVelocity = Vector3.zero;
                return;
            }

            if (_isDashing)
            {
                _rb.linearVelocity = _dashDirection * dashSpeed;
            }
            else if (_activeShiftAbility != null && _activeShiftAbility.IsActive &&
                     ((_activeShiftAbility is GrapplingHookAbility gha && gha.IsPulling) ||
                      _activeShiftAbility is UnstoppableChargeAbility ||
                      (_activeShiftAbility is SatchelVaultAbility sva && sva.IsVaulting)))
            {
                // Active ability handles linearVelocity override directly (Grappling Hook zip, Juggernaut Charge, or Demolitionist Satchel Forward Roll)
                return;
            }
            else
            {
                float baseSpeed = moveSpeed;
                float currentSpeed = baseSpeed;
                bool isShiftOnCooldown = _activeShiftAbility != null && _activeShiftAbility.CooldownRemaining > 0f;
                bool isShiftHeld = !isShiftOnCooldown && Keyboard.current != null && Keyboard.current.shiftKey.isPressed && _moveInput.sqrMagnitude > 0.01f;

                if (_shootSprintLockTimer > 0f)
                {
                    // เงื่อนไข: ถ้าวิ่งแล้วกดยิง (หรือยิงปืนภายใน 1.0 วินาที) ความเร็วจะเป็นเท่าเดิน * 0.75 จนกว่าจะพ้น 1.0 วินาที
                    currentSpeed = baseSpeed * 0.75f;
                }
                else if (isShiftHeld)
                {
                    // ถ้าวิ่งแล้วไม่ได้ยิง (หรือหมดช่วง 1.0 วินาทีแล้ว) ความเร็วจะกลายเป็นวิ่งเร็ว
                    float sprintMult = (activeCharacterData != null && activeCharacterData.sprintSpeedMultiplier > 1f)
                        ? activeCharacterData.sprintSpeedMultiplier
                        : 1.5f;

                    // ตรวจสอบโบนัสความเร็วจาก Shift Ability เพิ่มเติม (เช่น Adrenaline Rush 1.75x)
                    if (_activeShiftAbility != null && _activeShiftAbility.IsActive)
                    {
                        if (_activeShiftAbility is AdrenalineRushAbility)
                        {
                            float adrMult = (activeCharacterData != null) ? activeCharacterData.adrenalineSpeedMultiplier : 1.75f;
                            sprintMult = Mathf.Max(sprintMult, adrMult);
                        }
                        else if (_activeShiftAbility is ShadowStepAbility)
                        {
                            float shadowMult = (activeCharacterData != null) ? activeCharacterData.shadowSpeedMultiplier : 1.40f;
                            sprintMult = Mathf.Max(sprintMult, shadowMult);
                        }
                        else if (_activeShiftAbility is EagleEyeAbility)
                        {
                            // สกิล Eagle Eye ของสไนเปอร์: ซูมจอกว้างขึ้นและเดินย่องเล็งยิงอย่างประณีต (0.35x ตามที่ตั้งค่าใน CharacterData)
                            float eagleMult = (activeCharacterData != null) ? activeCharacterData.eagleEyeMoveSpeedMultiplier : 0.35f;
                            sprintMult = eagleMult;
                        }
                    }

                    currentSpeed = baseSpeed * sprintMult;
                }
                else
                {
                    // เดินปกติ
                    currentSpeed = baseSpeed;
                }

                _rb.linearVelocity = new Vector3(_moveInput.x * currentSpeed, _rb.linearVelocity.y, _moveInput.y * currentSpeed);
            }
        }

        private Vector3 _aimTargetPoint;
        
        // Grenade Aim Indicators
        private LineRenderer _throwRangeIndicator;
        private LineRenderer _aoeIndicator;
        private SpriteRenderer _throwRangeSpriteRenderer;
        private SpriteRenderer _aoeSpriteRenderer;

        // Eagle Eye & Sniper RED Laser Sight Aim Indicators
        private LineRenderer _laserSightIndicator;
        private LineRenderer _laserTargetDot;
        private SpriteRenderer _sniperReticleSprite;
        private SpriteRenderer _sniperChevronSprite;

        private void UpdateAimDirection()
        {
            if (Camera.main == null) return;
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;
            if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.ShouldBlockCombat) return;

            // Aim towards mouse position in world space using 3D raycast on X/Z plane
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float rayDistance))
            {
                _aimTargetPoint = ray.GetPoint(rayDistance);

                // Clamp throwing range if using a Grenade
                if (CurrentWeapon != null && CurrentWeapon.projectilePrefab != null)
                {
                    if (CurrentWeapon.projectilePrefab.Trajectory == TrajectoryMode.Arced)
                    {
                        float distance = Vector3.Distance(transform.position, _aimTargetPoint);
                        if (distance > CurrentWeapon.range)
                        {
                            Vector3 dirToTarget = (_aimTargetPoint - transform.position).normalized;
                            _aimTargetPoint = transform.position + dirToTarget * CurrentWeapon.range;
                        }
                    }
                }

                Vector3 direction = (_aimTargetPoint - transform.position);
                direction.y = 0f;
                direction.Normalize();

                if (direction.sqrMagnitude > 0.01f)
                {
                    _aimDirection = direction;
                    // Rotate the player to face aiming direction (unless sitting or sleeping)
                    if (!_isSitting && !_isSleeping)
                    {
                        transform.forward = _aimDirection;
                    }
                }
            }
        }

        private void HandleDirectInput()
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;
            if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.ShouldBlockCombat)
            {
                _fireBufferTimer = 0f;
                return;
            }
            if ((InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) ||
                (LootUI.HasInstance && LootUI.Instance.IsOpen) ||
                (StorageUI.HasInstance && StorageUI.Instance.IsOpen) ||
                (WeaponUpgradeUI.HasInstance && WeaponUpgradeUI.Instance.IsOpen)) return;

            // Direct input checks to ensure combat always works regardless of playerinput maps
            if (Mouse.current != null)
            {
                // Ignore attacks only if clicking over interactive UI elements (Buttons, Sliders, InputFields, Scrollbars)
                bool hitInteractiveUI = false;
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    {
                        position = Mouse.current.position.ReadValue()
                    };
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

                    foreach (var r in results)
                    {
                        if (r.gameObject != null && (
                            r.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null ||
                            r.gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>() != null))
                        {
                            hitInteractiveUI = true;
                            break;
                        }
                    }
                }

                if (!hitInteractiveUI)
                {
                    // Buffer click input so rapid clicks or walking clicks near cooldown boundaries are never dropped
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        _fireBufferTimer = 0.18f; // 180ms responsive buffer window
                    }

                    bool artifactAuto = CurrentWeapon != null && WeaponArtifactManager.GetAggregatedModifiers(CurrentWeapon.weaponName).grantAutomatic;
                    bool isAuto = CurrentWeapon != null && (CurrentWeapon.isAutomatic || artifactAuto) && IsHoldingRanged;
                    bool wantToAttack = (isAuto && Mouse.current.leftButton.isPressed) || _fireBufferTimer > 0f;

                    if (wantToAttack && _meleeAnimTimer <= 0f)
                    {
                        if (IsHoldingRanged && _fireCooldownTimer <= 0f && !_isReloading)
                        {
                            _fireBufferTimer = 0f;
                            ShootWeapon();
                        }
                        else if (IsHoldingMelee && _meleeCooldownTimer <= 0f)
                        {
                            _fireBufferTimer = 0f;
                            MeleeAttack();
                        }
                        else if (IsHoldingConsumable)
                        {
                            _fireBufferTimer = 0f;
                            QuickSlot?.UseActiveHeldItem();
                        }
                    }

                    // สกิลคลิกขวาของ Artifact ระดับ Legendary (เช่น Flamethrower: ชาร์จ 2 วิ ใช้แก๊ส 10 ยิงบอลเพลิงตรง 20 เมตร)
                    UpdateLegendaryRightClickSkill();
                }
                else
                {
                    CancelLegendaryRmbCharge();
                }

                // Scroll wheel weapon cycling
                Vector2 scrollValue = Mouse.current.scroll.ReadValue();
                if (scrollValue.y > 0.1f)
                {
                    CycleWeapon(-1);
                }
                else if (scrollValue.y < -0.1f)
                {
                    CycleWeapon(1);
                }
            }

            UpdateFlamethrowerConeVfx();

            if (Keyboard.current != null)
            {
                bool isUIBlocked = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) ||
                                   (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) ||
                                   (BuildingPlacer.Instance != null && BuildingPlacer.Instance.IsInBuildMode);

                if (!isUIBlocked)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) QuickSlot?.SelectSlot(0);
                    if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) QuickSlot?.SelectSlot(1);
                    if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) QuickSlot?.SelectSlot(2);
                    if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) QuickSlot?.SelectSlot(3);
                    if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) QuickSlot?.SelectSlot(4);
                    if (Keyboard.current.digit6Key.wasPressedThisFrame) SwitchToQuickSlot(5);
                    if (Keyboard.current.digit7Key.wasPressedThisFrame) SwitchToQuickSlot(6);
                    if (Keyboard.current.digit8Key.wasPressedThisFrame) SwitchToQuickSlot(7);
                    if (Keyboard.current.digit9Key.wasPressedThisFrame) SwitchToQuickSlot(8);
                }

                if (Keyboard.current.xKey.wasPressedThisFrame)
                {
                    CycleMeleeWeapon(1);
                }

                if (Keyboard.current.fKey.wasPressedThisFrame && IsHoldingMelee && _meleeCooldownTimer <= 0f)
                {
                    MeleeAttack();
                }
                if (Keyboard.current.spaceKey.wasPressedThisFrame && _dashCooldownTimer <= 0f)
                {
                    StartDash();
                }
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    TryInteract();
                }
                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    TryTalk();
                }
                if (Keyboard.current.zKey.wasPressedThisFrame && !_isDashing)
                {
                    if (_bestInteractableType == TargetInteractableType.Building && _closestBuilding != null)
                    {
                        _closestBuilding.OnSecondaryInteract();
                    }
                    else
                    {
                        ToggleSit();
                    }
                }
            }
        }

        // Called by PlayerInput component via SendMessages
        private void OnMove(InputValue value)
        {
            if (IsRemotePlayer || IsDowned) return;
            if (IsControlLocked)
            {
                _moveInput = Vector2.zero;
                return;
            }
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps)
            {
                _moveInput = Vector2.zero;
                return;
            }
            _moveInput = value.Get<Vector2>();
        }

        private void OnAttack(InputValue value)
        {
            if (IsRemotePlayer || IsDowned) return;
            if (IsControlLocked) return;
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;
            if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.ShouldBlockCombat)
            {
                _fireBufferTimer = 0f;
                return;
            }

            // Attack action ส่งต่อไปยังระบบ Buffer เดียวกันใน Update() เพื่อให้เลือกใช้ตามไอเทมที่ถืออยู่จริง (มีด/หอก/ปืน/ของกิน)
            // ป้องกันบัคที่เมื่อถืออาวุธประชิด (เช่น มีด หอก) แล้วระบบเผลอสั่งยิงปืนซ้ำซ้อน
            if (value.isPressed && !_isDashing)
            {
                _fireBufferTimer = 0.18f;
            }
        }

        private void OnInteract(InputValue value)
        {
            if (IsControlLocked) return;
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;
            if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.ShouldBlockCombat)
            {
                _fireBufferTimer = 0f;
                return;
            }

            if (value.isPressed)
            {
                TryInteract();
            }
        }

        private void ToggleInventoryMenu()
        {
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.ToggleInventory();
            }
        }

        private void TryInteract()
        {
            // 1. Check if standing in any TransitionPortal trigger zone (only active if WorldMapManager exists)
            if (WorldMapManager.Instance != null)
            {
                TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
                foreach (TransitionPortal portal in portals)
                {
                    if (TransitionPortal.IsWorldPortal(portal) && portal.IsPlayerInRange(this))
                    {
                        portal.TriggerTransition(this);
                        return; // successfully transitioned via portal, skip normal interactions!
                    }
                }
            }

            // 2. Interacting with the SINGLE closest target detected by UpdateInteractionPrompt!
            if (_bestInteractableType == TargetInteractableType.Building && _closestBuilding != null && _closestBuilding.CanInteract)
            {
                _closestBuilding.OnInteract();
                return;
            }

            if (_bestInteractableType == TargetInteractableType.Container && _closestContainer != null)
            {
                PlayerInventory inv = GetComponent<PlayerInventory>();
                if (inv == null) inv = _inventory;

                StorageCrateBuilding crate = _closestContainer.GetComponent<StorageCrateBuilding>();
                if (crate != null && StorageUI.Instance != null)
                {
                    StorageUI.Instance.Open(_closestContainer, inv, crate);
                    return;
                }

                if (LootUI.Instance != null && inv != null)
                {
                    LootUI.Instance.Open(_closestContainer, inv);
                    return; // Opened Loot UI, skip other interactions!
                }
            }

            if (_bestInteractableType == TargetInteractableType.NPC && _closestNpc != null)
            {
                _closestNpc.Interact();
                return; // Interacted with NPC, skip other interactions!
            }

            // 3. Fallback to CollectibleItem in case no prompt object is active
            CollectibleItem[] collectibles = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            CollectibleItem closestItem = null;
            float closestDist = float.MaxValue;

            foreach (var item in collectibles)
            {
                if (item == null || item.IsCollected) continue;
                float dist = Vector3.Distance(transform.position, item.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestItem = item;
                }
            }

            if (closestItem != null && closestItem.IsPlayerInRange())
            {
                CollectItem(closestItem);
            }
        }

        private float _lastInventoryFullNoticeTime = -10f;

        public void ShowInventoryFullFeedback()
        {
            if (Time.unscaledTime - _lastInventoryFullNoticeTime < 1.2f) return;
            _lastInventoryFullNoticeTime = Time.unscaledTime;

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 2.0f,
                    0f,
                    false,
                    "INVENTORY FULL!"
                );
            }
        }

        public void CollectItem(CollectibleItem item)
        {
            if (item == null || item.IsCollected || _inventory == null) return;

            // ตรวจสอบความจุกระเป๋าก่อนส่ง Network Claim เสมอ เพื่อไม่ให้เก็บ/ลบไอเทมเมื่อกระเป๋าเต็ม
            if (!item.IsMoney)
            {
                int qty = Mathf.Max(1, item.Quantity);
                if (!_inventory.CanAddItem(item.ItemName, qty))
                {
                    ShowInventoryFullFeedback();
                    Debug.LogWarning($"[PlayerController] Cannot collect '{item.ItemName}' x{qty}: Inventory full ({_inventory.UsedSlots}/{_inventory.MaxSlots} slots used)!");
                    return;
                }
            }

            if (TryNetworkClaimCollectible(item)) return;
            if (item.IsMoney)
            {
                _inventory.AddMoney(item.MoneyAmount);
                item.Collect();
            }
            else
            {
                int qty = Mathf.Max(1, item.Quantity);
                if (!_inventory.AddItem(item.ItemName, qty))
                {
                    ShowInventoryFullFeedback();
                    return;
                }
                item.Collect();
            }
            OnAmmoChanged?.Invoke();
        }

        private void ShootWeapon(bool isEchoBurst = false)
        {
            if (_isSitting) SetSitting(false);
            if (_isSleeping) SetSleeping(false);
            ShowRangedWeaponVisual();
            TriggerCombatState();
            if (_isReloading) return;

            Weapon activeWeapon = CurrentWeapon;
            if (activeWeapon == null) return; // ถ้าไม่ใส่ปืน ห้ามยิง
            int magCount = activeWeapon != null ? activeWeapon.currentMagazine : _currentMagazine;

            if (!isEchoBurst && magCount <= 0)
            {
                TryStartReload();
                return;
            }

            WeaponArtifactModifiers artifactMods = activeWeapon != null
                ? WeaponArtifactManager.GetAggregatedModifiers(activeWeapon.weaponName)
                : default;

            if (!isEchoBurst)
            {
                // Decrement ammo (Artifact ammoSaveChance มีโอกาสไม่เสียกระสุน)
                bool savedAmmo = artifactMods.ammoSaveChance > 0f && Random.value < artifactMods.ammoSaveChance;
                if (!savedAmmo)
                {
                    if (activeWeapon != null)
                    {
                        activeWeapon.currentMagazine--;
                    }
                    else
                    {
                        _currentMagazine--;
                    }
                }
                OnAmmoChanged?.Invoke();

                // Set cooldown
                float rate = activeWeapon != null ? activeWeapon.fireRate : fireRate;
                if (artifactMods.fireRateMultiplier > 0f)
                {
                    rate /= (1f + artifactMods.fireRateMultiplier);
                }
                if (_activeShiftAbility != null && _activeShiftAbility.IsActive && _activeShiftAbility is AdrenalineRushAbility)
                {
                    float atkBonus = activeCharacterData != null ? activeCharacterData.adrenalineAttackSpeedBonus : 0.30f;
                    rate /= (1f + atkBonus);
                }
                if (activeCharacterData != null && activeCharacterData.rebelFrenzyAttackSpeedBonus > 0f && _health != null && (_health.CurrentHealth / _health.MaxHealth <= activeCharacterData.rebelFrenzyHpThreshold))
                {
                    rate /= (1f + activeCharacterData.rebelFrenzyAttackSpeedBonus);
                }
                _fireCooldownTimer = rate;
            }

            // Alert nearby enemies to the gunshot noise
            AlertEnemiesToGunshot();

            // ล็อกความเร็วให้เหลือเดิน * 0.75 ทันทีที่กดยิงปืน เป็นเวลา 1.0 วินาที
            _shootSprintLockTimer = activeCharacterData != null ? activeCharacterData.shootSprintLockDuration : 1.0f;
            _activeShiftAbility?.OnShiftReleased();
            _isSprinting = false;

            if (_animator == null) _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.ResetTrigger("Shoot");
                _animator.SetTrigger("Shoot");
                int shootLayer = _animator.layerCount > 1 ? 1 : 0;
                _animator.SetBool("IsRunning", false);
                _shootAnimLayerTimer = 0.42f;
                if (shootLayer == 1) _animator.SetLayerWeight(1, 1f);
                _animator.Play("Shoot", shootLayer, 0f);
            }

            // 📳 สั่นหน้าจอเบาๆ ตามแรงถีบของปืน (Screen Shake Recoil)
            float shakeDur = 0.05f;
            float shakeMag = 0.08f;
            if (activeWeapon != null)
            {
                string lowerName = activeWeapon.weaponName.ToLower();
                if (lowerName.Contains("shotgun"))
                {
                    shakeDur = 0.12f;
                    shakeMag = 0.25f;
                }
                else if (lowerName.Contains("sniper") || lowerName.Contains("revolver"))
                {
                    shakeDur = 0.10f;
                    shakeMag = 0.20f;
                }
                else if (lowerName.Contains("rocket") || lowerName.Contains("grenade"))
                {
                    shakeDur = 0.18f;
                    shakeMag = 0.40f;
                }
                else if (lowerName.Contains("smg"))
                {
                    shakeDur = 0.035f;
                    shakeMag = 0.05f;
                }
                else if (lowerName.Contains("rifle"))
                {
                    shakeDur = 0.06f;
                    shakeMag = 0.10f;
                }
            }
            CameraFollow.TriggerShake(shakeDur, shakeMag);

            // Spawning positions (offset 0.95m forward to prevent overlapping player's 0.5m radius capsule when walking)
            Vector3 spawnPos = transform.position + _aimDirection * 0.95f;
            TriggerMuzzleFlashLight();

            // เปิด Particle Effect เปลวไฟทรงกรวยอัตโนมัติเมื่อยิง Flamethrower
            bool isFlamethrowerWeapon = activeWeapon != null && !string.IsNullOrEmpty(activeWeapon.weaponName) &&
                                        activeWeapon.weaponName.IndexOf("Flamethrower", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isFlamethrowerWeapon)
            {
                _flamethrowerVfxTimer = 0.22f;
                FlamethrowerConeVfx coneVfx = ResolveFlamethrowerConeVfx();
                if (coneVfx != null)
                {
                    coneVfx.UpdateAimTransform(transform.position, _aimDirection);
                    coneVfx.SetFiring(true);
                }
            }

            // ─── Helper methods for Muzzle Flash Point Light ───
            
            // 💥 เล่นเอฟเฟกต์ Muzzle Flash ถ้าระบุไว้ในอาวุธ (ยกเว้น Flamethrower ที่ใช้เปลวไฟทรงกรวยอยู่แล้ว)
            string flashKey = isFlamethrowerWeapon ? null : (activeWeapon != null ? activeWeapon.vfxPoolKey : null);
            if (!isFlamethrowerWeapon && (string.IsNullOrEmpty(flashKey) || flashKey.Equals("PistalFlash", System.StringComparison.OrdinalIgnoreCase)))
            {
                flashKey = "PistolFlash";
            }

            if (!string.IsNullOrEmpty(flashKey) && ObjectPoolManager.Instance != null)
            {
                GameObject muzzleFlash = ObjectPoolManager.Instance.SpawnFromPool(flashKey, spawnPos, Quaternion.LookRotation(_aimDirection));
                if (muzzleFlash != null)
                {
                    PooledParticle pooledParticle = muzzleFlash.GetComponent<PooledParticle>();
                    if (pooledParticle != null)
                    {
                        pooledParticle.SetPoolKey(flashKey);
                    }
                }
            }

            Projectile weaponProjectilePrefab = activeWeapon != null ? activeWeapon.projectilePrefab : projectilePrefab;

            int pellets = ((activeWeapon != null && activeWeapon.pelletsPerShot > 0) ? activeWeapon.pelletsPerShot : 1) + Mathf.Max(0, artifactMods.bonusPellets);
            float spread = (activeWeapon != null) ? activeWeapon.spreadAngle : 0f;
            if (isFlamethrowerWeapon)
            {
                // ยิงเป็นรูปกรวยกว้างเพื่อให้ระยะโจมตีตรงกับ Particle Effect เปลวไฟทรงกรวย
                pellets = Mathf.Max(3, pellets);
                spread = Mathf.Max(22f, spread);
            }
            if (artifactMods.extraSpreadAngle > 0f)
            {
                spread += artifactMods.extraSpreadAngle;
            }
            else if (artifactMods.bonusPellets > 0 && spread < 2.5f)
            {
                spread = 3.0f; // มุมแยกเล็กน้อยเมื่อ Artifact เพิ่มลูกกระสุน (เช่น สไนเปอร์ยิงออกทีละ 2 ลูก)
            }

            for (int i = 0; i < pellets; i++)
            {
                // Calculate direction with spread (around Vector3.up axis)
                Vector3 bulletDir = _aimDirection;
                if (spread > 0f && pellets > 1)
                {
                    // Map pellets across the spread arc symmetrically
                    float angleOffset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, (float)i / (pellets - 1));
                    bulletDir = Quaternion.Euler(0f, angleOffset, 0f) * _aimDirection;
                }
                else if (spread > 0f)
                {
                    // Random spread for small rifle recoil
                    float randomAngle = Random.Range(-spread * 0.5f, spread * 0.5f);
                    bulletDir = Quaternion.Euler(0f, randomAngle, 0f) * _aimDirection;
                }

                GameObject bullet = null;
                if (weaponProjectilePrefab != null && !string.IsNullOrEmpty(weaponProjectilePrefab.PoolKey) && ObjectPoolManager.Instance != null)
                {
                    bullet = ObjectPoolManager.Instance.SpawnFromPool(weaponProjectilePrefab.PoolKey, spawnPos, Quaternion.LookRotation(bulletDir));
                }

                if (bullet == null && weaponProjectilePrefab != null)
                {
                    bullet = Instantiate(weaponProjectilePrefab.gameObject, spawnPos, Quaternion.LookRotation(bulletDir));
                }

                if (bullet == null)
                {
                    // Dynamic bullet fallback
                    bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bullet.name = "DynamicBullet";
                    bullet.transform.position = spawnPos;
                    bullet.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

                    Collider col = bullet.GetComponent<Collider>();
                    if (col != null) col.isTrigger = true;

                    Renderer rend = bullet.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material.color = Color.yellow;
                    }

                    bullet.AddComponent<Projectile>();
                }

                if (bullet != null)
                {
                    if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
                    Collider bulletCol = bullet.GetComponent<Collider>();
                    if (bulletCol != null && _playerCollider != null)
                    {
                        Physics.IgnoreCollision(bulletCol, _playerCollider, true);
                    }
                }

                Projectile proj = bullet.GetComponent<Projectile>();
                if (proj != null)
                {
                    if (activeWeapon != null)
                    {
                        // Base player damage from active operative or fallback 10
                        float basePlayerDamage = (activeCharacterData != null && activeCharacterData.baseRangedDamage > 0) ? activeCharacterData.baseRangedDamage : 10f;
                        int itemDamage = 0;
                        float weaponRange = activeWeapon.range; // fallback to default weapon range/lifetime

                        bool isRocketLauncher = !string.IsNullOrEmpty(activeWeapon.weaponName) &&
                                                activeWeapon.weaponName.IndexOf("Rocket", System.StringComparison.OrdinalIgnoreCase) >= 0;

                        if (ItemDatabase.Instance != null)
                        {
                            ItemData item = ItemDatabase.Instance.GetItemByName(activeWeapon.weaponName);
                            if (item != null)
                            {
                                itemDamage = item.damage;

                                // If attackRadius is configured in ItemData, calculate bullet lifetime based on projectile speed
                                if (isRocketLauncher)
                                {
                                    // Slow heavy rocket with very long flight range (~34m at 13 m/s)
                                    const float rocketSpeed = 13f;
                                    const float rocketMaxDistance = 34f;
                                    weaponRange = rocketMaxDistance / rocketSpeed;
                                }
                                else if (item.type == ItemType.ThrowingWeapon || (proj != null && proj.Trajectory == TrajectoryMode.Arced))
                                {
                                    weaponRange = 5.0f; // Give sufficient flight time for arced throwing weapons
                                }
                                else if (item.attackRadius > 0.01f && proj.Speed > 0.01f)
                                {
                                    weaponRange = item.attackRadius / proj.Speed;
                                }
                            }
                        }
                        float finalDamage = (basePlayerDamage + itemDamage + artifactMods.bonusDamage) * (1f + artifactMods.damageMultiplier);
                        weaponRange += artifactMods.bonusRange;
                        bool effectivePierce = activeWeapon.canPierce || artifactMods.grantPierce;

                        // Effective falloff range (e.g. standard 14m, snipers extend further)
                        float effectiveFalloffDist = DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;
                        if (activeCharacterData != null && activeCharacterData.characterId == "MARKSMAN")
                        {
                            effectiveFalloffDist = 22f; // Marksman maintains high damage at sniper ranges
                        }

                        string visualId = isFlamethrowerWeapon ? "FlameCone" : ResolveProjectileVisualId(activeWeapon);
                        proj.SetStats(finalDamage, weaponRange, effectivePierce, activeWeapon.weaponName, effectiveFalloffDist, visualId);
                        proj.SetArtifactImpactModifiers(artifactMods.bonusKnockback, artifactMods.bonusStagger, artifactMods.lifestealOnHit, artifactMods.projectileScaleBonus);

                        if (isRocketLauncher)
                        {
                            // Match Grenade's exact explosion radius & Explosion VFX, flying slow & very far
                            float grenadeRadius = 2.5f;
                            string grenadeVfxKey = "Explosion";
                            if (weapons != null)
                            {
                                for (int wIdx = 0; wIdx < weapons.Count; wIdx++)
                                {
                                    if (weapons[wIdx] != null && weapons[wIdx].weaponName != null &&
                                        weapons[wIdx].weaponName.IndexOf("Grenade", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                        weapons[wIdx].projectilePrefab != null)
                                    {
                                        if (weapons[wIdx].projectilePrefab.ExplosionRadius > 0.1f)
                                            grenadeRadius = weapons[wIdx].projectilePrefab.ExplosionRadius;
                                        if (!string.IsNullOrEmpty(weapons[wIdx].projectilePrefab.ExplosionVfxPoolKey))
                                            grenadeVfxKey = weapons[wIdx].projectilePrefab.ExplosionVfxPoolKey;
                                        break;
                                    }
                                }
                            }
                            proj.ConfigureExplosiveOverride(13f, grenadeRadius, grenadeVfxKey);
                        }

                        Debug.Log($"[ShootWeapon] Fired {activeWeapon.weaponName}! Visual ID: {visualId} | Total Damage: {finalDamage} (Base: {basePlayerDamage} + Weapon: {itemDamage}) | Lifetime: {weaponRange:F2}s | Speed: {proj.Speed} m/s");
                    }
                    
                    // Pass the aim target point for Arced trajectories (fan out landing points for multi-throw Grenade artifacts)
                    string arcVisualId = isFlamethrowerWeapon ? "FlameCone" : (activeWeapon != null ? ResolveProjectileVisualId(activeWeapon) : "");
                    Vector3 shotTargetPoint = _aimTargetPoint;
                    if (proj.Trajectory == TrajectoryMode.Arced && pellets > 1)
                    {
                        float throwDist = Vector3.Distance(new Vector3(spawnPos.x, 0f, spawnPos.z), new Vector3(_aimTargetPoint.x, 0f, _aimTargetPoint.z));
                        Vector3 flatDir = new Vector3(bulletDir.x, 0f, bulletDir.z);
                        if (flatDir.sqrMagnitude > 0.001f)
                        {
                            flatDir.Normalize();
                            shotTargetPoint = new Vector3(spawnPos.x + flatDir.x * throwDist, _aimTargetPoint.y, spawnPos.z + flatDir.z * throwDist);
                        }
                    }
                    proj.Setup(bulletDir, shotTargetPoint, gameObject, arcVisualId);

                    if (i == 0 && activeWeapon != null)
                    {
                        bool isRocket = !string.IsNullOrEmpty(activeWeapon.weaponName) &&
                                        activeWeapon.weaponName.IndexOf("Rocket", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        float baseNetDmg = (activeCharacterData != null && activeCharacterData.baseRangedDamage > 0 ? activeCharacterData.baseRangedDamage : 10f);
                        float itemNetDmg = 0f;
                        if (ItemDatabase.Instance != null)
                        {
                            ItemData wItem = ItemDatabase.Instance.GetItemByName(activeWeapon.weaponName);
                            if (wItem != null) itemNetDmg = wItem.damage;
                        }
                        float netDmg = (baseNetDmg + itemNetDmg + artifactMods.bonusDamage) * (1f + artifactMods.damageMultiplier);
                        float netRange = activeWeapon.range + artifactMods.bonusRange;
                        bool netPierce = activeWeapon.canPierce || artifactMods.grantPierce;
                        NotifyNetworkShoot(spawnPos, _aimDirection, _aimTargetPoint, activeWeapon.weaponName, netDmg, netRange, isRocket, pellets, spread, netPierce, arcVisualId);
                    }
                }
            }

            // Double-Tap / Echo Burst Artifact: กดยิง 1 ที ยิงซ้ำตามอัตโนมัติ (เช่น Double-Tap Trigger -> ออก 2 ที)
            if (!isEchoBurst && artifactMods.bonusBurstShots > 0 && activeWeapon != null)
            {
                StartCoroutine(FireEchoBurstRoutine(activeWeapon.weaponName, artifactMods.bonusBurstShots, 0.085f));
            }
        }

        private System.Collections.IEnumerator FireEchoBurstRoutine(string expectedWeaponName, int extraShots, float delaySeconds)
        {
            for (int b = 0; b < extraShots; b++)
            {
                yield return new WaitForSeconds(delaySeconds);
                if (!gameObject.activeInHierarchy || !enabled) yield break;
                if (_health != null && _health.IsDead) yield break;
                if (_isReloading) yield break;
                Weapon cur = CurrentWeapon;
                if (cur == null || !string.Equals(cur.weaponName, expectedWeaponName, System.StringComparison.OrdinalIgnoreCase)) yield break;
                ShootWeapon(isEchoBurst: true);
            }
        }

        // =========================================================================
        //  Flamethrower Cone Particle Effect & Legendary Right-Click Fireball Skill
        // =========================================================================

        private FlamethrowerConeVfx ResolveFlamethrowerConeVfx()
        {
            if (flamethrowerConeVfx != null) return flamethrowerConeVfx;

            flamethrowerConeVfx = GetComponentInChildren<FlamethrowerConeVfx>(true);
            if (flamethrowerConeVfx != null) return flamethrowerConeVfx;

            // เผื่อผู้เล่นลาก ParticleSystem เปล่าชื่อ VFX_FlamethrowerConeFire เข้ามาใน PlayerPrefabs
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;
                string n = children[i].name;
                if (n.IndexOf("FlamethrowerConeFire", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("FlamethrowerFire", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    flamethrowerConeVfx = children[i].GetComponent<FlamethrowerConeVfx>()
                                          ?? children[i].gameObject.AddComponent<FlamethrowerConeVfx>();
                    return flamethrowerConeVfx;
                }
            }
            return null;
        }

        public void TriggerRemoteFlamethrowerConeVfx()
        {
            _flamethrowerVfxTimer = 0.28f;
            FlamethrowerConeVfx coneVfx = ResolveFlamethrowerConeVfx();
            if (coneVfx != null)
            {
                coneVfx.SetFiring(true);
            }
        }

        private void UpdateFlamethrowerConeVfx()
        {
            if (_rmbSkillCooldownTimer > 0f)
            {
                _rmbSkillCooldownTimer = Mathf.Max(0f, _rmbSkillCooldownTimer - Time.deltaTime);
            }

            bool holdingFlamethrower = IsRemotePlayer
                ? true
                : (IsHoldingRanged && CurrentWeapon != null &&
                   !string.IsNullOrEmpty(CurrentWeapon.weaponName) &&
                   CurrentWeapon.weaponName.IndexOf("Flamethrower", System.StringComparison.OrdinalIgnoreCase) >= 0);

            bool canKeepFiring = holdingFlamethrower && !_isReloading && (_health == null || !_health.IsDead) && !IsDowned;

            if (_flamethrowerVfxTimer > 0f)
            {
                _flamethrowerVfxTimer -= Time.deltaTime;
            }

            FlamethrowerConeVfx coneVfx = ResolveFlamethrowerConeVfx();
            if (coneVfx != null)
            {
                bool shouldEmit = canKeepFiring && _flamethrowerVfxTimer > 0f;
                if (shouldEmit)
                {
                    Vector3 aim = IsRemotePlayer ? _networkTargetAimDir : _aimDirection;
                    coneVfx.UpdateAimTransform(transform.position, aim);
                }
                coneVfx.SetFiring(shouldEmit);
            }
        }

        private void CancelLegendaryRmbCharge()
        {
            _isChargingRmbSkill = false;
            _rmbChargeTimer = 0f;
            _rmbChargeWeaponName = string.Empty;
        }

        private void UpdateLegendaryRightClickSkill()
        {
            if (Mouse.current == null)
            {
                CancelLegendaryRmbCharge();
                return;
            }

            bool rmbHeld = Mouse.current.rightButton.isPressed;
            bool rmbDown = Mouse.current.rightButton.wasPressedThisFrame;

            if (!rmbHeld)
            {
                _rmbSkillFiredThisHold = false;
                CancelLegendaryRmbCharge();
                return;
            }

            if (_rmbSkillFiredThisHold) return;

            if (_isReloading || (_health != null && _health.IsDead) || IsDowned)
            {
                CancelLegendaryRmbCharge();
                return;
            }

            // 1. กรณีถืออาวุธประชิด (Melee) ที่มีสกิลคลิกขวา Legendary (Knife Hook / Bat Boomerang / Katana RipperCombo)
            if (IsHoldingMelee)
            {
                string meleeName = CurrentMeleeWeaponName;
                if (string.IsNullOrEmpty(meleeName)) return;

                WeaponArtifactModifiers meleeMods = WeaponArtifactManager.GetAggregatedModifiers(meleeName);
                if (!meleeMods.HasRightClickSkill) return;

                if (!rmbDown) return;

                if (_rmbSkillCooldownTimer > 0f)
                {
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.SpawnDamageNumber(
                            transform.position + Vector3.up * 2.1f,
                            0f,
                            false,
                            $"SKILL CD {_rmbSkillCooldownTimer:F1}s"
                        );
                    }
                    return;
                }

                float cd = meleeMods.rightClickCooldown > 0f ? meleeMods.rightClickCooldown : 5.0f;
                _rmbSkillCooldownTimer = cd;
                _rmbSkillFiredThisHold = true;
                ExecuteLegendaryMeleeRightClickSkill(meleeName, meleeMods);
                return;
            }

            // 2. กรณีถือปืน (Ranged) ที่มีสกิลคลิกขวา Legendary (เช่น Flamethrower Fireball)
            if (!IsHoldingRanged || CurrentWeapon == null)
            {
                CancelLegendaryRmbCharge();
                return;
            }

            Weapon activeWeapon = CurrentWeapon;
            WeaponArtifactModifiers mods = WeaponArtifactManager.GetAggregatedModifiers(activeWeapon.weaponName);
            if (!mods.HasRightClickSkill)
            {
                CancelLegendaryRmbCharge();
                return;
            }

            if (_rmbSkillCooldownTimer > 0f)
            {
                if (rmbDown && DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 2.1f,
                        0f,
                        false,
                        $"SKILL CD {_rmbSkillCooldownTimer:F1}s"
                    );
                }
                CancelLegendaryRmbCharge();
                return;
            }

            int requiredAmmo = Mathf.Max(0, mods.rightClickAmmoCost);
            string ammoType = !string.IsNullOrEmpty(activeWeapon.ammoType) ? activeWeapon.ammoType : "Gas Canister";
            int totalAvailableAmmo = Mathf.Max(0, activeWeapon.currentMagazine) + GetAmmoReserveCount(ammoType);

            if (requiredAmmo > 0 && totalAvailableAmmo < requiredAmmo)
            {
                if (rmbDown && DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 2.1f,
                        0f,
                        false,
                        $"NEED {requiredAmmo} {ammoType.ToUpperInvariant()}!"
                    );
                }
                CancelLegendaryRmbCharge();
                return;
            }

            float targetCharge = Mathf.Max(0f, mods.rightClickChargeDuration);
            if (targetCharge <= 0.05f)
            {
                if (rmbDown)
                {
                    _rmbSkillCooldownTimer = mods.rightClickCooldown > 0f ? mods.rightClickCooldown : 5.0f;
                    ExecuteLegendaryRightClickSkill(activeWeapon, mods, ammoType, requiredAmmo);
                    _rmbSkillFiredThisHold = true;
                }
                return;
            }

            if (!_isChargingRmbSkill || !string.Equals(_rmbChargeWeaponName, activeWeapon.weaponName, System.StringComparison.OrdinalIgnoreCase))
            {
                _isChargingRmbSkill = true;
                _rmbChargeTimer = 0f;
                _rmbChargeTargetDuration = targetCharge;
                _rmbChargeWeaponName = activeWeapon.weaponName;
            }

            _rmbChargeTimer += Time.deltaTime;
            if (_rmbChargeTimer >= _rmbChargeTargetDuration)
            {
                _rmbSkillCooldownTimer = mods.rightClickCooldown > 0f ? mods.rightClickCooldown : 5.0f;
                ExecuteLegendaryRightClickSkill(activeWeapon, mods, ammoType, requiredAmmo);
                _rmbSkillFiredThisHold = true;
                CancelLegendaryRmbCharge();
            }
        }

        private float ComputeCurrentMeleeSkillBaseDamage(string wName, WeaponArtifactModifiers meleeMods, out float activeRadius, out float activeKnockback, out float activeStagger)
        {
            float basePlayerDamage = (activeCharacterData != null && activeCharacterData.baseMeleeDamage > 0) ? activeCharacterData.baseMeleeDamage : 10f;
            float itemDamage = 0f;
            MeleeWeapon curMelee = CurrentMeleeWeapon;
            activeRadius = curMelee != null ? curMelee.attackRadius : meleeRadius;
            activeKnockback = curMelee != null ? curMelee.knockbackForce : 10f;
            activeStagger = curMelee != null ? curMelee.staggerDuration : 0.4f;

            ItemData activeMelee = (ItemDatabase.Instance != null && !string.IsNullOrEmpty(wName))
                ? ItemDatabase.Instance.GetItemByName(wName)
                : null;
            if (activeMelee != null)
            {
                itemDamage = activeMelee.damage;
                if (activeMelee.attackRadius > 0f) activeRadius = activeMelee.attackRadius;
                if (activeMelee.knockbackForce > 0f) activeKnockback = activeMelee.knockbackForce;
                if (activeMelee.staggerDuration > 0f) activeStagger = activeMelee.staggerDuration;
            }

            activeRadius += meleeMods.bonusRange;
            activeKnockback += meleeMods.bonusKnockback;
            activeStagger += meleeMods.bonusStagger;
            return (basePlayerDamage + itemDamage + meleeMods.bonusDamage) * (1f + meleeMods.damageMultiplier);
        }

        private static bool IsBossEnemyTarget(Component target)
        {
            if (target == null) return false;
            LootDropper ld = target.GetComponentInParent<LootDropper>();
            if (ld != null && ld.IsBoss) return true;
            ZombieAI zai = target.GetComponentInParent<ZombieAI>();
            if (zai != null && zai.IsBossUnit) return true;
            string n = target.transform.root != null ? target.transform.root.name : target.name;
            return n.IndexOf("Leader", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ExecuteLegendaryMeleeRightClickSkill(string weaponName, WeaponArtifactModifiers mods)
        {
            if (_isSitting) SetSitting(false);
            if (_isSleeping) SetSleeping(false);
            ShowMeleeWeaponVisual();
            TriggerCombatState();

            Vector3 dir = _aimDirection.sqrMagnitude > 0.01f ? _aimDirection.normalized : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();

            string skillId = mods.rightClickSkillId ?? string.Empty;
            if (skillId.Equals("Knife_Hook", System.StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ExecuteKnifeHookRoutine(weaponName, mods, dir));
            }
            else if (skillId.Equals("Bat_Boomerang", System.StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ExecuteBatBoomerangRoutine(weaponName, mods, dir));
            }
            else if (skillId.Equals("Katana_RipperCombo", System.StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(ExecuteKatanaRipperComboRoutine(weaponName, mods, dir));
            }
        }

        /// <summary>
        /// Legend Knife — Knife Hook (CD 5 วิ): ขว้างมีดติดเชือกใส่ศัตรูข้างหน้าแล้วดึงพุ่งกลับมาหาเรา (ยกเว้นบอส)
        /// </summary>
        private System.Collections.IEnumerator ExecuteKnifeHookRoutine(string weaponName, WeaponArtifactModifiers mods, Vector3 dir)
        {
            float baseDmg = ComputeCurrentMeleeSkillBaseDamage(weaponName, mods, out _, out _, out float activeStagger);
            float skillDmg = mods.rightClickUseFlatDamage ? Mathf.Max(1f, mods.rightClickDamage) : baseDmg * Mathf.Max(0.5f, mods.rightClickDamage);
            float maxRange = mods.rightClickRange > 1f ? mods.rightClickRange : 10f;

            PlayMeleeSlashAnim("Slash3", 0.32f);

            Vector3 startPos = transform.position + Vector3.up * 0.65f + dir * 0.6f;
            NotifyNetworkMelee(startPos, dir, weaponName, 101, skillDmg, maxRange);

            GameObject knifeVis = CreateHookKnifeVisual(startPos, dir);
            LineRenderer rope = CreateHookRopeVisual(startPos);

            Transform hookedEnemy = null;
            BaseEnemyAI hookedAi = null;
            bool hitBossImmune = false;

            float speed = 28f;
            float traveled = 0f;
            Vector3 currentPos = startPos;

            // 1. ขาพุ่งออกไปข้างหน้า
            while (traveled < maxRange && hookedEnemy == null && !hitBossImmune)
            {
                float step = speed * Time.deltaTime;
                traveled += step;
                currentPos += dir * step;

                if (knifeVis != null)
                {
                    knifeVis.transform.position = currentPos;
                    knifeVis.transform.Rotate(Vector3.forward * 1080f * Time.deltaTime, Space.Self);
                }
                UpdateHookRope(rope, transform.position + Vector3.up * 0.65f, currentPos);

                Collider[] hits = Physics.OverlapSphere(currentPos, 0.85f);
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i];
                    if (col == null || col.gameObject == gameObject || col.transform.root == transform.root) continue;

                    IDamageable dmgTarget = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                    if (dmgTarget == null || dmgTarget.IsDead) continue;

                    BuildingObject building = col.GetComponentInParent<BuildingObject>();
                    if (building != null && !building.CanBeDamagedByPlayer) continue;

                    DamageResult res = DamageCalculator.CalculateMeleeDamage(skillDmg, gameObject, weaponName);
                    if (col.CompareTag("Player"))
                    {
                        TheLastEmpire.Network.NetworkPlayerBridge.TryApplyFriendlyFireDamage(gameObject, col.gameObject, res.FinalDamage, currentPos, res.IsCritical);
                        break;
                    }
                    dmgTarget.TakeDamage(res.FinalDamage, currentPos, res.IsCritical);
                    if (mods.lifestealOnHit > 0.01f && _health != null && !_health.IsDead)
                    {
                        _health.Heal(mods.lifestealOnHit);
                    }

                    if (IsBossEnemyTarget(col))
                    {
                        hitBossImmune = true;
                        if (DamageNumberManager.Instance != null)
                        {
                            DamageNumberManager.Instance.SpawnDamageNumber(col.transform.position + Vector3.up * 2.0f, 0f, false, "BOSS IMMUNE");
                        }
                    }
                    else
                    {
                        hookedAi = col.GetComponent<BaseEnemyAI>() ?? col.GetComponentInParent<BaseEnemyAI>();
                        hookedEnemy = hookedAi != null ? hookedAi.transform : col.transform;
                        if (hookedAi != null)
                        {
                            hookedAi.ApplyMeleeKnockback(-dir, 2f, Mathf.Max(0.65f, activeStagger));
                        }
                    }
                    break;
                }

                yield return null;
            }

            // 2. ขาดึงกลับเข้าหาผู้เล่น (ดึงศัตรูธรรมดาพุ่งกลับมาหาเราด้วย!)
            float pullDuration = 0.26f;
            float elapsed = 0f;
            Vector3 returnStart = currentPos;
            Vector3 enemyStart = hookedEnemy != null ? hookedEnemy.position : currentPos;

            while (elapsed < pullDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pullDuration);
                Vector3 handPos = transform.position + Vector3.up * 0.65f;
                Vector3 pullDest = transform.position + dir * 1.35f;

                Vector3 knifePos = Vector3.Lerp(returnStart, handPos, t);
                if (knifeVis != null) knifeVis.transform.position = knifePos;
                UpdateHookRope(rope, handPos, knifePos);

                if (hookedEnemy != null && (hookedAi == null || (hookedAi.GetComponent<Health>() == null || !hookedAi.GetComponent<Health>().IsDead)))
                {
                    Vector3 targetPos = new Vector3(
                        Mathf.Lerp(enemyStart.x, pullDest.x, t),
                        enemyStart.y,
                        Mathf.Lerp(enemyStart.z, pullDest.z, t)
                    );
                    Rigidbody enemyRb = hookedEnemy.GetComponent<Rigidbody>();
                    if (enemyRb != null && !enemyRb.isKinematic)
                    {
                        enemyRb.MovePosition(targetPos);
                    }
                    else
                    {
                        hookedEnemy.position = targetPos;
                    }
                }
                yield return null;
            }

            if (knifeVis != null) Destroy(knifeVis);
            if (rope != null) Destroy(rope.gameObject);
        }

        /// <summary>
        /// Legend Baseball Bat — Bat Boomerang (CD 5 วิ): ขว้างไม้เบสบอลหมุนวนเป็นวงกลมข้างหน้า (ระยะตี x 2.5)
        /// </summary>
        private System.Collections.IEnumerator ExecuteBatBoomerangRoutine(string weaponName, WeaponArtifactModifiers mods, Vector3 dir)
        {
            float baseDmg = ComputeCurrentMeleeSkillBaseDamage(weaponName, mods, out float activeRadius, out float activeKnockback, out float activeStagger);
            float skillDmg = mods.rightClickUseFlatDamage ? Mathf.Max(1f, mods.rightClickDamage) : baseDmg * Mathf.Max(0.5f, mods.rightClickDamage);
            float rangeMult = mods.rightClickRange > 0.5f ? mods.rightClickRange : 2.5f;
            float circleDiameter = Mathf.Max(2.5f, activeRadius * rangeMult);
            float orbitRadius = circleDiameter * 0.5f;

            PlayMeleeSlashAnim("Slash2", 0.35f);

            Vector3 origin = transform.position + Vector3.up * 0.65f;
            NotifyNetworkMelee(origin, dir, weaponName, 102, skillDmg, circleDiameter);

            Vector3 rightDir = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 circleCenter = origin + dir * orbitRadius;

            GameObject batVis = CreateBoomerangBatVisual(origin, dir);
            var hitSet = new System.Collections.Generic.HashSet<IDamageable>();

            float duration = 0.72f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // มุมเริ่มจากฝั่งผู้เล่น (PI) หมุนกวาดเต็มวงกลม 360° ด้านหน้าผู้เล่นแล้ววนกลับเข้ามือ
                float angle = Mathf.PI + (t * Mathf.PI * 2f);
                Vector3 curOrigin = transform.position + Vector3.up * 0.65f;
                Vector3 dynamicCenter = Vector3.Lerp(circleCenter, curOrigin + dir * orbitRadius, 0.35f);
                Vector3 pos = dynamicCenter + (dir * Mathf.Cos(angle) + rightDir * Mathf.Sin(angle)) * orbitRadius;
                pos.y = origin.y;

                if (batVis != null)
                {
                    batVis.transform.position = pos;
                    batVis.transform.Rotate(Vector3.up * 1600f * Time.deltaTime, Space.World);
                }

                Collider[] cols = Physics.OverlapSphere(pos, 1.15f);
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider col = cols[i];
                    if (col == null || col.gameObject == gameObject || col.transform.root == transform.root) continue;

                    IDamageable dmgTarget = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                    if (dmgTarget == null || dmgTarget.IsDead || hitSet.Contains(dmgTarget)) continue;

                    BuildingObject building = col.GetComponentInParent<BuildingObject>();
                    if (building != null && !building.CanBeDamagedByPlayer) continue;

                    hitSet.Add(dmgTarget);
                    DamageResult res = DamageCalculator.CalculateMeleeDamage(skillDmg, gameObject, weaponName);
                    if (col.CompareTag("Player"))
                    {
                        TheLastEmpire.Network.NetworkPlayerBridge.TryApplyFriendlyFireDamage(gameObject, col.gameObject, res.FinalDamage, col.transform.position, res.IsCritical);
                        continue;
                    }
                    dmgTarget.TakeDamage(res.FinalDamage, col.transform.position, res.IsCritical);
                    if (mods.lifestealOnHit > 0.01f && _health != null && !_health.IsDead)
                    {
                        _health.Heal(mods.lifestealOnHit);
                    }

                    BaseEnemyAI enemy = col.GetComponent<BaseEnemyAI>() ?? col.GetComponentInParent<BaseEnemyAI>();
                    if (enemy != null)
                    {
                        Vector3 pushDir = (enemy.transform.position - pos);
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude < 0.01f) pushDir = dir;
                        enemy.ApplyMeleeKnockback(pushDir.normalized, Mathf.Max(10f, activeKnockback * 0.6f), Mathf.Max(0.55f, activeStagger));
                    }
                }

                yield return null;
            }

            if (batVis != null) Destroy(batVis);
        }

        /// <summary>
        /// Legend Katana — RipperCombo (CD 5 วิ): ฟันคาตานะต่อเนื่องเป็นชุด 3 Hit สุดรุนแรง
        /// </summary>
        private System.Collections.IEnumerator ExecuteKatanaRipperComboRoutine(string weaponName, WeaponArtifactModifiers mods, Vector3 dir)
        {
            float baseDmg = ComputeCurrentMeleeSkillBaseDamage(weaponName, mods, out float activeRadius, out float activeKnockback, out float activeStagger);
            float perHitMult = mods.rightClickDamage > 0.1f ? mods.rightClickDamage : 1.25f;

            for (int step = 1; step <= 3; step++)
            {
                if (_health != null && _health.IsDead) yield break;

                Vector3 curDir = _aimDirection.sqrMagnitude > 0.01f ? _aimDirection.normalized : dir;
                curDir.y = 0f;
                if (curDir.sqrMagnitude < 0.001f) curDir = dir;
                curDir.Normalize();

                bool isFinisher = (step == 3);
                float hitRadius = activeRadius * (isFinisher ? 1.35f : 1.05f);
                float hitDmgBase = baseDmg * perHitMult * (isFinisher ? 1.45f : 1.0f);
                Vector3 attackPoint = transform.position + curDir * (isFinisher ? 1.05f : 0.85f);

                PlayMeleeSlashAnim("Slash" + step, 0.20f);
                NotifyNetworkMelee(attackPoint, curDir, weaponName, step, hitDmgBase, hitRadius);

                if (ObjectPoolManager.Instance != null)
                {
                    GameObject slashVfx = ObjectPoolManager.Instance.SpawnFromPool("SwordSlash", attackPoint, Quaternion.LookRotation(curDir));
                    if (slashVfx != null)
                    {
                        PooledParticle pp = slashVfx.GetComponent<PooledParticle>();
                        if (pp != null)
                        {
                            pp.SetPoolKey("SwordSlash");
                            pp.ApplyScaleMultiplier(hitRadius * (isFinisher ? 1.25f : 0.95f));
                        }
                        else
                        {
                            slashVfx.transform.localScale = Vector3.one * (hitRadius * (isFinisher ? 1.25f : 0.95f));
                        }
                    }
                }

                Collider[] hitCols = Physics.OverlapSphere(attackPoint, hitRadius);
                for (int i = 0; i < hitCols.Length; i++)
                {
                    Collider col = hitCols[i];
                    if (col == null || col.gameObject == gameObject || col.transform.root == transform.root) continue;

                    IDamageable dmgTarget = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                    if (dmgTarget == null || dmgTarget.IsDead) continue;

                    BuildingObject building = col.GetComponentInParent<BuildingObject>();
                    if (building != null && !building.CanBeDamagedByPlayer) continue;

                    DamageResult res = DamageCalculator.CalculateMeleeDamage(hitDmgBase, gameObject, weaponName);
                    if (col.CompareTag("Player"))
                    {
                        TheLastEmpire.Network.NetworkPlayerBridge.TryApplyFriendlyFireDamage(gameObject, col.gameObject, res.FinalDamage, col.transform.position, res.IsCritical);
                        continue;
                    }
                    dmgTarget.TakeDamage(res.FinalDamage, col.transform.position, res.IsCritical);
                    if (mods.lifestealOnHit > 0.01f && _health != null && !_health.IsDead)
                    {
                        _health.Heal(mods.lifestealOnHit);
                    }

                    BaseEnemyAI enemy = col.GetComponent<BaseEnemyAI>() ?? col.GetComponentInParent<BaseEnemyAI>();
                    if (enemy != null)
                    {
                        Vector3 push = (enemy.transform.position - transform.position);
                        push.y = 0f;
                        if (push.sqrMagnitude < 0.01f) push = curDir;
                        enemy.ApplyMeleeKnockback(push.normalized, isFinisher ? activeKnockback * 1.4f : activeKnockback * 0.6f, activeStagger);
                    }
                }

                CameraFollow.TriggerShake(isFinisher ? 0.11f : 0.05f, isFinisher ? 0.22f : 0.09f);

                if (step < 3)
                {
                    yield return new WaitForSeconds(0.14f);
                }
            }
        }

        private void PlayMeleeSlashAnim(string slashState, float lockDuration)
        {
            if (_animator == null) _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (_animator == null) return;

            int stateHash = Animator.StringToHash(slashState);
            if (!_animator.HasState(0, stateHash)) slashState = "Slash1";

            if (_animator.layerCount > 1)
            {
                _animator.SetLayerWeight(1, 0f);
                _animator.Play("UpperBody_Empty", 1, 0f);
            }
            _meleeAnimTimer = lockDuration;
            _animator.Play(slashState, 0, 0f);
        }

        private static GameObject CreateHookKnifeVisual(Vector3 pos, Vector3 dir)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "KnifeHook_Visual";
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(dir);
            go.transform.localScale = new Vector3(0.14f, 0.08f, 0.72f);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (s != null)
                {
                    Material m = new Material(s);
                    m.color = new Color(0.88f, 0.94f, 1f, 1f);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.88f, 0.94f, 1f, 1f));
                    r.sharedMaterial = m;
                }
            }
            return go;
        }

        private static LineRenderer CreateHookRopeVisual(Vector3 startPos)
        {
            GameObject ropeGo = new GameObject("KnifeHook_Rope");
            LineRenderer lr = ropeGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.04f;
            Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null) lr.material = new Material(s);
            lr.startColor = new Color(0.95f, 0.85f, 0.35f, 1f);
            lr.endColor = new Color(1f, 1f, 1f, 0.9f);
            lr.SetPosition(0, startPos);
            lr.SetPosition(1, startPos);
            return lr;
        }

        private static void UpdateHookRope(LineRenderer lr, Vector3 p0, Vector3 p1)
        {
            if (lr == null) return;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);
        }

        private static GameObject CreateBoomerangBatVisual(Vector3 pos, Vector3 dir)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "BatBoomerang_Visual";
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.22f, 0.65f, 0.22f);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (s != null)
                {
                    Material m = new Material(s);
                    m.color = new Color(0.92f, 0.68f, 0.25f, 1f);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.92f, 0.68f, 0.25f, 1f));
                    r.sharedMaterial = m;
                }
            }
            TrailRenderer tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.22f;
            tr.startWidth = 0.32f;
            tr.endWidth = 0f;
            Shader ts = Shader.Find("Sprites/Default");
            if (ts != null) tr.material = new Material(ts);
            tr.startColor = new Color(1f, 0.85f, 0.3f, 0.85f);
            tr.endColor = new Color(1f, 0.5f, 0.1f, 0f);
            return go;
        }

        // --- Network Visual Replay & Host Authoritative Pull Helpers ---

        public void SpawnRemoteVisualKnifeHook(Vector3 origin, Vector3 dir, float maxRange)
        {
            StartCoroutine(RemoteVisualKnifeHookRoutine(origin, dir, maxRange > 1f ? maxRange : 10f));
        }

        private System.Collections.IEnumerator RemoteVisualKnifeHookRoutine(Vector3 origin, Vector3 dir, float maxRange)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            GameObject knifeVis = CreateHookKnifeVisual(origin, dir);
            LineRenderer rope = CreateHookRopeVisual(origin);

            float elapsed = 0f;
            float halfDur = Mathf.Clamp(maxRange / 28f, 0.15f, 0.35f);
            Vector3 farPos = origin + dir * Mathf.Min(maxRange, 7.5f);

            while (elapsed < halfDur * 2f)
            {
                elapsed += Time.deltaTime;
                Vector3 handPos = transform.position + Vector3.up * 0.65f;
                Vector3 pos = elapsed < halfDur
                    ? Vector3.Lerp(handPos, farPos, elapsed / halfDur)
                    : Vector3.Lerp(farPos, handPos, (elapsed - halfDur) / halfDur);

                if (knifeVis != null)
                {
                    knifeVis.transform.position = pos;
                    knifeVis.transform.Rotate(Vector3.forward * 1080f * Time.deltaTime, Space.Self);
                }
                UpdateHookRope(rope, handPos, pos);
                yield return null;
            }

            if (knifeVis != null) Destroy(knifeVis);
            if (rope != null) Destroy(rope.gameObject);
        }

        public void SpawnRemoteVisualBatBoomerang(Vector3 origin, Vector3 dir, float circleDiameter)
        {
            StartCoroutine(RemoteVisualBatBoomerangRoutine(origin, dir, circleDiameter > 1f ? circleDiameter : 5f));
        }

        private System.Collections.IEnumerator RemoteVisualBatBoomerangRoutine(Vector3 origin, Vector3 dir, float circleDiameter)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            float orbitRadius = circleDiameter * 0.5f;
            Vector3 rightDir = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 center = origin + dir * orbitRadius;
            GameObject batVis = CreateBoomerangBatVisual(origin, dir);

            float duration = 0.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float angle = Mathf.PI + (t * Mathf.PI * 2f);
                Vector3 pos = center + (dir * Mathf.Cos(angle) + rightDir * Mathf.Sin(angle)) * orbitRadius;
                pos.y = origin.y;
                if (batVis != null)
                {
                    batVis.transform.position = pos;
                    batVis.transform.Rotate(Vector3.up * 1600f * Time.deltaTime, Space.World);
                }
                yield return null;
            }

            if (batVis != null) Destroy(batVis);
        }

        public static void ExecuteHostAuthoritativeKnifeHookPull(Vector3 attackerOrigin, Vector3 dir, float maxRange)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();
            float range = maxRange > 1f ? maxRange : 10f;

            RaycastHit[] hits = Physics.SphereCastAll(attackerOrigin, 0.9f, dir, range);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.CompareTag("Player")) continue;
                BaseEnemyAI enemy = col.GetComponent<BaseEnemyAI>() ?? col.GetComponentInParent<BaseEnemyAI>();
                if (enemy == null) continue;
                Health h = enemy.GetComponent<Health>();
                if (h != null && h.IsDead) continue;
                if (IsBossEnemyTarget(enemy)) return; // ยกเว้นบอส!

                Vector3 pullDest = attackerOrigin + dir * 1.35f;
                pullDest.y = enemy.transform.position.y;
                enemy.ApplyMeleeKnockback(-dir, 2f, 0.65f);
                Rigidbody rb = enemy.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic) rb.MovePosition(pullDest);
                else enemy.transform.position = pullDest;
                return;
            }
        }

        private void ExecuteLegendaryRightClickSkill(Weapon activeWeapon, WeaponArtifactModifiers mods, string ammoType, int ammoCost)
        {
            if (activeWeapon == null) return;

            // หักกระสุน 10 นัด (หักจากแม็กก่อน ถ้าไม่พอค่อยดึงจากกระสุนสำรองในกระเป๋า)
            int fromMag = Mathf.Min(Mathf.Max(0, activeWeapon.currentMagazine), ammoCost);
            activeWeapon.currentMagazine -= fromMag;
            int remCost = ammoCost - fromMag;
            if (remCost > 0)
            {
                ConsumeAmmo(ammoType, remCost);
            }
            OnAmmoChanged?.Invoke();

            // คำนวณดาเมจลูกบอลเพลิง (ค่าเริ่มต้น 8.5x ดาเมจปืน หรือค่าตรง 8.5 หากตั้ง rightClickUseFlatDamage = true)
            float basePlayerDamage = (activeCharacterData != null && activeCharacterData.baseRangedDamage > 0) ? activeCharacterData.baseRangedDamage : 10f;
            float itemDamage = 0f;
            if (ItemDatabase.Instance != null)
            {
                ItemData wItem = ItemDatabase.Instance.GetItemByName(activeWeapon.weaponName);
                if (wItem != null) itemDamage = wItem.damage;
            }
            float normalWeaponDmg = (basePlayerDamage + itemDamage + mods.bonusDamage) * (1f + mods.damageMultiplier);
            float fireballDamage = mods.rightClickUseFlatDamage
                ? Mathf.Max(1f, mods.rightClickDamage)
                : Mathf.Max(1f, normalWeaponDmg * Mathf.Max(0.1f, mods.rightClickDamage));

            float skillRange = mods.rightClickRange > 0.1f ? mods.rightClickRange : 20f;
            float speed = fireballSpeed > 1f ? fireballSpeed : 25f;
            float lifetime = skillRange / speed; // เส้นตรงยาว 20 เมตร!

            Vector3 dir = _aimDirection.sqrMagnitude > 0.01f ? _aimDirection.normalized : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 spawnPos = transform.position + dir * 1.05f + Vector3.up * 0.55f;
            GameObject fireballObj = null;

            if (ObjectPoolManager.Instance != null)
            {
                fireballObj = ObjectPoolManager.Instance.SpawnFromPool(fireballPoolKey, spawnPos, Quaternion.LookRotation(dir));
            }
            if (fireballObj == null)
            {
                GameObject fallbackPrefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/FlamethrowerFireballPrefabs");
                if (fallbackPrefab != null)
                {
                    fireballObj = Instantiate(fallbackPrefab, spawnPos, Quaternion.LookRotation(dir));
                }
            }

            if (fireballObj != null)
            {
                if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
                Collider fbCol = fireballObj.GetComponent<Collider>();
                if (fbCol != null && _playerCollider != null)
                {
                    Physics.IgnoreCollision(fbCol, _playerCollider, true);
                }

                Projectile proj = fireballObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.PoolKey = fireballPoolKey;
                    proj.SetStats(fireballDamage, lifetime, true, activeWeapon.weaponName, skillRange, "");
                    proj.SetSpeed(speed);
                    proj.SetArtifactImpactModifiers(mods.bonusKnockback + 4f, mods.bonusStagger + 0.35f, mods.lifestealOnHit, mods.projectileScaleBonus);
                    proj.Setup(dir, spawnPos + dir * skillRange, gameObject, "");
                }
            }

            CameraFollow.TriggerShake(0.16f, 0.32f);
            TriggerMuzzleFlashLight();
            AlertEnemiesToGunshot();
            NotifyNetworkShoot(spawnPos, dir, spawnPos + dir * skillRange, activeWeapon.weaponName, fireballDamage, lifetime, false, 1, 0f, true, "FlamethrowerFireball");
            Debug.Log($"[LegendarySkill] Fired {mods.rightClickSkillId}! Damage={fireballDamage:F1}, Range={skillRange:F0}m, Cost={ammoCost} {ammoType}");
        }

        private void OnGUI()
        {
            if (IsRemotePlayer || !_isChargingRmbSkill || _rmbChargeTargetDuration <= 0.01f) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.25f);
            if (screenPos.z <= 0f) return;

            float progress = Mathf.Clamp01(_rmbChargeTimer / _rmbChargeTargetDuration);
            float barW = 110f;
            float barH = 12f;
            float x = screenPos.x - barW * 0.5f;
            float y = Screen.height - screenPos.y - barH * 0.5f;

            Color prevColor = GUI.color;
            GUI.color = new Color(0.08f, 0.05f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, barW + 4f, barH + 4f), Texture2D.whiteTexture);

            GUI.color = Color.Lerp(new Color(1f, 0.55f, 0.1f, 1f), new Color(1f, 0.2f, 0.05f, 1f), progress);
            GUI.DrawTexture(new Rect(x, y, barW * progress, barH), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(x - 25f, y - 18f, barW + 50f, 16f), $"FIREBALL {_rmbChargeTimer:F1}/{_rmbChargeTargetDuration:F1}s", labelStyle);
            GUI.color = prevColor;
        }

        /// <summary>
        /// คำนวณหา ID ของโมเดลกระสุน/ระเบิดสำหรับเปิดตาใน Projectile (เช่น Bullet, Arrow, Grenade)
        /// แยกประเภทชัดเจน: หน้าไม้/ธนู -> "Arrow", ปืนทุกชนิด -> "Bullet", ระเบิด -> "Grenade"
        /// </summary>
        public string ResolveProjectileVisualId(Weapon weapon)
        {
            if (weapon == null) return "Bullet";
            if (!string.IsNullOrEmpty(weapon.projectileVisualId)) return weapon.projectileVisualId;

            string wName = weapon.weaponName != null ? weapon.weaponName.Trim() : "";
            string aType = weapon.ammoType != null ? weapon.ammoType.Trim() : "";
            string lowerW = wName.ToLower();
            string lowerA = aType.ToLower();

            // 1. ธนู / หน้าไม้ / ลูกธนู (Bow / Crossbow / Bolts / Arrow) -> Arrow
            if (lowerW.Contains("crossbow") || lowerW.Contains("bow") || 
                lowerA.Contains("bolt") || lowerA.Contains("arrow"))
            {
                return "Arrow";
            }

            // 2. ระเบิด (Grenade / Bomb) -> Grenade
            if (lowerW.Contains("grenade") || lowerA.Contains("grenade") || lowerW.Contains("bomb"))
            {
                return "Grenade";
            }

            // 3. ตรวจสอบจาก ItemData ของอาวุธใน ItemDatabase
            if (ItemDatabase.Instance != null && !string.IsNullOrEmpty(wName))
            {
                ItemData wData = ItemDatabase.Instance.GetItemByName(wName);
                if (wData != null)
                {
                    if (wData.weaponConfig != null && !string.IsNullOrEmpty(wData.weaponConfig.projectileVisualId))
                    {
                        return wData.weaponConfig.projectileVisualId;
                    }
                    if (!string.IsNullOrEmpty(wData.name))
                    {
                        string cleanAssetName = wData.name.ToLower();
                        if (cleanAssetName.Contains("crossbow") || cleanAssetName.Contains("bow") || cleanAssetName.Contains("arrow") || cleanAssetName.Contains("bolt"))
                            return "Arrow";
                        if (cleanAssetName.Contains("grenade") || cleanAssetName.Contains("bomb"))
                            return "Grenade";
                        if (cleanAssetName.Contains("bullet") || cleanAssetName.Contains("ammo"))
                            return "Bullet";
                    }
                }
            }

            // 4. ตรวจสอบจาก Ammo ItemData
            if (ItemDatabase.Instance != null && !string.IsNullOrEmpty(aType))
            {
                ItemData aData = ItemDatabase.Instance.GetItemByName(aType);
                if (aData != null && !string.IsNullOrEmpty(aData.name))
                {
                    string cleanAmmoName = aData.name.ToLower();
                    if (cleanAmmoName.Contains("arrow") || cleanAmmoName.Contains("bolt"))
                        return "Arrow";
                    if (cleanAmmoName.Contains("grenade") || cleanAmmoName.Contains("bomb"))
                        return "Grenade";
                    if (cleanAmmoName.Contains("bullet") || cleanAmmoName.Contains("ammo"))
                        return "Bullet";
                }
            }

            // 5. ปืนทุกชนิด (Pistol, Rifle, Shotgun, SMG, Sniper, Revolver ฯลฯ) -> เป็น Bullet
            return "Bullet";
        }

        private static string ResolveMeleeAttackState(string weaponName, int comboStep, out float animLockDuration)
        {
            string clean = (weaponName ?? "").Replace(" ", "").Replace("_", "").ToLowerInvariant();

            if (clean.Contains("knife"))
            {
                animLockDuration = 0.24f;
                return "Melee_Knife";
            }
            if (clean.Contains("baseball") || clean.Contains("bat") && !clean.Contains("baton"))
            {
                animLockDuration = 0.44f;
                return "Melee_BaseballBat";
            }
            if (clean.Contains("machete"))
            {
                animLockDuration = 0.48f;
                return "Melee_Machete";
            }
            if (clean.Contains("axe"))
            {
                animLockDuration = 0.56f;
                return "Melee_FireAxe";
            }
            if (clean.Contains("katana"))
            {
                animLockDuration = 0.36f;
                return "Melee_Katana";
            }
            if (clean.Contains("sledge") || clean.Contains("hammer"))
            {
                animLockDuration = 0.72f;
                return "Melee_Sledgehammer";
            }
            if (clean.Contains("spear"))
            {
                animLockDuration = 0.40f;
                return "Melee_SurvivalSpear";
            }
            if (clean.Contains("baton") || clean.Contains("stun"))
            {
                animLockDuration = 0.38f;
                return "Melee_StunBaton";
            }
            if (clean.Contains("chainsaw"))
            {
                animLockDuration = 0.14f;
                return "Melee_Chainsaw";
            }

            animLockDuration = 0.40f;
            return "Slash" + comboStep;
        }

        private void MeleeAttack()
        {
            if (_isSitting) SetSitting(false);
            if (_isSleeping) SetSleeping(false);
            ShowMeleeWeaponVisual();
            TriggerCombatState();
            MeleeWeapon activeMelee = CurrentMeleeWeapon;
            float activeRadius = activeMelee != null ? activeMelee.attackRadius : meleeRadius;
            float activeRate = activeMelee != null ? activeMelee.attackRate : meleeRate;
            float activeKnockback = activeMelee != null ? activeMelee.knockbackForce : 10f;
            float activeStagger = activeMelee != null ? activeMelee.staggerDuration : 0.4f;

            // Base player damage from active operative or fallback 10
            float basePlayerDamage = (activeCharacterData != null && activeCharacterData.baseMeleeDamage > 0) ? activeCharacterData.baseMeleeDamage : 10f;
            int itemDamage = 0;
            string wName = CurrentMeleeWeaponName ?? "";
            if (ItemDatabase.Instance != null)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(wName);
                if (item != null)
                {
                    itemDamage = item.damage;
                    
                    // Override stats dynamically from ScriptableObject if configured
                    if (item.attackRadius > 0.01f) activeRadius = item.attackRadius;
                    if (item.attackRate > 0.01f) activeRate = item.attackRate;
                    if (item.knockbackForce > 0.01f) activeKnockback = item.knockbackForce;
                    if (item.staggerDuration > 0.01f) activeStagger = item.staggerDuration;
                }
            }
            WeaponArtifactModifiers meleeArtMods = !string.IsNullOrEmpty(wName)
                ? WeaponArtifactManager.GetAggregatedModifiers(wName)
                : default;
            activeRadius += meleeArtMods.bonusRange;
            activeKnockback += meleeArtMods.bonusKnockback;
            activeStagger += meleeArtMods.bonusStagger;
            if (meleeArtMods.fireRateMultiplier > 0f)
            {
                activeRate /= (1f + meleeArtMods.fireRateMultiplier);
            }
            float activeDamage = (basePlayerDamage + itemDamage + meleeArtMods.bonusDamage) * (1f + meleeArtMods.damageMultiplier);

            // --- Weapon Special Effects (from Weapon GDD) ---
            bool isKatana = wName.IndexOf("Katana", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFireAxe = wName.IndexOf("Axe", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSledgehammer = wName.IndexOf("Hammer", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isStunBaton = wName.IndexOf("Baton", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // ⚔️ คำนวณดาเมจระยะประชิดด้วย DamageCalculator (มีสุ่ม +-10, อัตราคริ 20% / 1.3x หรือ Katana/Phantom 2.0x)
            DamageResult meleeDmgResult = DamageCalculator.CalculateMeleeDamage(activeDamage, gameObject, wName);

            if (activeCharacterData != null && activeCharacterData.rebelFrenzyAttackSpeedBonus > 0f && _health != null && (_health.CurrentHealth / _health.MaxHealth <= activeCharacterData.rebelFrenzyHpThreshold))
            {
                activeRate /= (1f + activeCharacterData.rebelFrenzyAttackSpeedBonus);
            }
            _meleeCooldownTimer = activeRate;
            Vector3 attackPoint = transform.position + _aimDirection * 0.8f;

            // ⚔️ เล่นเอฟเฟกต์ฟัน ถ้าระบุไว้ในอาวุธ
            if (activeMelee != null && !string.IsNullOrEmpty(activeMelee.vfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                GameObject slashVFX = ObjectPoolManager.Instance.SpawnFromPool(activeMelee.vfxPoolKey, attackPoint, Quaternion.LookRotation(_aimDirection));
                if (slashVFX != null)
                {
                    PooledParticle pooledParticle = slashVFX.GetComponent<PooledParticle>();
                    if (pooledParticle != null)
                    {
                        // อัปเดต Pool Key ให้ตรงกัน
                        pooledParticle.SetPoolKey(activeMelee.vfxPoolKey);

                        // ขยายขนาด VFX โดยอิงจาก Scale ต้นฉบับของ Prefab
                        float multiplier = activeMelee.vfxScaleMultiplier > 0 ? activeMelee.vfxScaleMultiplier : 1.0f;
                        pooledParticle.ApplyScaleMultiplier(activeRadius * multiplier);
                    }
                    else
                    {
                        // Fallback กรณีไม่ได้ใส่สคริปต์ PooledParticle
                        float multiplier = activeMelee.vfxScaleMultiplier > 0 ? activeMelee.vfxScaleMultiplier : 1.0f;
                        slashVFX.transform.localScale = Vector3.one * (activeRadius * multiplier);
                    }
                }
            }

            // Sledgehammer: Ground Slam shockwave visual
            if (isSledgehammer && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.SpawnFromPool("EnemyExplosion", attackPoint, Quaternion.identity);
            }

            // Visual swing debug
            Debug.Log($"[PlayerController] Melee Swing Swung using {CurrentMeleeWeaponName}!");

            // 3-step melee combo on Right-Click: resets to Slash1 if idle > 1.0s
            if (Time.time - _lastMeleeSwingTime > MeleeComboResetWindow)
            {
                _meleeComboStep = 0;
            }
            _meleeComboStep = (_meleeComboStep % 3) + 1;
            _lastMeleeSwingTime = Time.time;

            if (_animator == null) _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                string slashState = ResolveMeleeAttackState(wName, _meleeComboStep, out float lockDuration);
                int stateHash = Animator.StringToHash(slashState);
                if (!_animator.HasState(0, stateHash))
                {
                    slashState = "Slash" + _meleeComboStep;
                    lockDuration = 0.40f;
                }

                _animator.ResetTrigger("Slash1");
                _animator.ResetTrigger("Slash2");
                _animator.ResetTrigger("Slash3");

                if (_animator.layerCount > 1) _animator.Play("UpperBody_Empty", 1, 0f);
                _shootAnimLayerTimer = 0f;
                _meleeAnimTimer = Mathf.Min(lockDuration, Mathf.Max(0.08f, activeRate * 0.92f));
                _fireBufferTimer = 0f;
                _animator.ResetTrigger("Shoot");
                if (_animator.layerCount > 1)
                {
                    _animator.SetLayerWeight(1, 0f);
                    _animator.Play("UpperBody_Empty", 1, 0f);
                }
                _animator.Play(slashState, 0, 0f);
            }

            NotifyNetworkMelee(attackPoint, _aimDirection, wName, _meleeComboStep, meleeDmgResult.FinalDamage, activeRadius);

            Collider[] hitColliders = Physics.OverlapSphere(attackPoint, activeRadius);
            int targetsHit = 0;
            int maxTargets = isKatana ? 4 : 999; // Katana strikes up to 4 targets in wide cleave
            var hitTeammates = new System.Collections.Generic.HashSet<PlayerController>();

            foreach (var col in hitColliders)
            {
                if (col.gameObject == gameObject || col.transform.root == transform.root) continue;
                if (targetsHit >= maxTargets) break;

                // 1. Deal damage
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    // ข้ามสิ่งปลูกสร้างของฝ่ายผู้เล่น ไม่ให้การกดตีทั่วไปทำลายบ้านหรือกองไฟตัวเอง
                    BuildingObject building = col.GetComponentInParent<BuildingObject>();
                    if (building != null && !building.CanBeDamagedByPlayer)
                    {
                        continue;
                    }
                    float hitDmg = meleeDmgResult.FinalDamage;
                    // Fire Axe: 25% chance for Bleed (extra 15 damage)
                    if (isFireAxe && Random.value < 0.25f)
                    {
                        hitDmg += 15f;
                        Debug.Log("[MeleeAttack] Fire Axe caused bleeding laceration (+15 damage)!");
                    }

                    // Multiplayer Friendly Fire: hitting a teammate deals 20% synced damage (no enemy lifesteal/kill hunger)
                    PlayerController ally = col.GetComponent<PlayerController>() ?? col.GetComponentInParent<PlayerController>();
                    if (ally != null && ally != this)
                    {
                        if (hitTeammates.Add(ally) &&
                            TheLastEmpire.Network.NetworkPlayerBridge.TryApplyFriendlyFireDamage(
                                gameObject, col.gameObject, hitDmg, col.transform.position, meleeDmgResult.IsCritical))
                        {
                            targetsHit++;
                        }
                        continue;
                    }

                    bool wasDeadBefore = damageable.IsDead;
                    damageable.TakeDamage(hitDmg, col.transform.position, meleeDmgResult.IsCritical);
                    targetsHit++;
                    if (meleeArtMods.lifestealOnHit > 0.01f && _health != null && !_health.IsDead)
                    {
                        _health.Heal(meleeArtMods.lifestealOnHit);
                    }
                    if (!wasDeadBefore && damageable.IsDead && activeCharacterData != null && activeCharacterData.rebelMeleeKillHungerRestore > 0f)
                    {
                        RestoreHunger(activeCharacterData.rebelMeleeKillHungerRestore);
                    }
                }

                // 2. Apply high-impact melee knockback to enemies
                BaseEnemyAI enemy = col.GetComponent<BaseEnemyAI>();
                if (enemy != null)
                {
                    Vector3 pushDir = (enemy.transform.position - transform.position);
                    pushDir.y = 0f;
                    pushDir.Normalize();
                    if (pushDir.sqrMagnitude < 0.01f)
                    {
                        pushDir = _aimDirection;
                    }

                    float finalStagger = (isSledgehammer ? 0.8f + meleeArtMods.bonusStagger : (isStunBaton ? 0.6f + meleeArtMods.bonusStagger : activeStagger));
                    float finalKnockback = isSledgehammer ? (20f + meleeArtMods.bonusKnockback) : activeKnockback;
                    enemy.ApplyMeleeKnockback(pushDir, finalKnockback, finalStagger);

                    // Stun Baton: 30% chance to chain shock surrounding enemies
                    if (isStunBaton && Random.value < 0.30f)
                    {
                        Collider[] nearby = Physics.OverlapSphere(enemy.transform.position, 2.5f);
                        foreach (var nCol in nearby)
                        {
                            if (nCol.gameObject != enemy.gameObject && nCol.gameObject != gameObject)
                            {
                                BaseEnemyAI chainEnemy = nCol.GetComponent<BaseEnemyAI>();
                                if (chainEnemy != null)
                                {
                                    chainEnemy.ApplyMeleeKnockback((chainEnemy.transform.position - enemy.transform.position).normalized, 4f, 0.5f);
                                    IDamageable cDmg = chainEnemy.GetComponent<IDamageable>();
                                    if (cDmg != null) cDmg.TakeDamage(10, chainEnemy.transform.position);
                                }
                            }
                        }
                    }
                }
            }
        }

        private enum VehicleVaultKind
        {
            None,
            Motorcycle,
            Car
        }

        private bool _isVaulting = false;
        private VehicleVaultKind _vaultKind = VehicleVaultKind.None;
        private float _vaultElapsed = 0f;
        private float _vaultDuration = 0.45f;
        private Vector3 _vaultStartPos;
        private Vector3 _vaultEndPos;
        private float _vaultSurfaceY;
        private bool _vaultPrevUseGravity = true;
        private readonly List<Collider> _vaultIgnoredColliders = new List<Collider>();

        public bool IsVaulting => _isVaulting;

        private void StartDash()
        {
            if (_isSitting) SetSitting(false);
            if (_isSleeping) SetSleeping(false);

            // Dash towards movement input, or aim direction if standing still
            Vector3 desiredDir = _moveInput.sqrMagnitude > 0.01f ? new Vector3(_moveInput.x, 0f, _moveInput.y).normalized : _aimDirection;
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude < 0.001f) desiredDir = transform.forward;
            desiredDir.Normalize();

            // Context-sensitive Vehicle Parkour:
            // - Blocked by Motorcycle -> Hurdle Jump over it (Vault_MotoJump)
            // - Blocked by Car/SUV/Taxi/Van -> Hood/Roof Slide across it (Vault_CarSlide)
            if (TryStartVehicleVault(desiredDir))
            {
                return;
            }

            _isDashing = true;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;
            _dashDirection = desiredDir;

            // Trigger Invulnerability Frames (I-Frames)
            if (_health != null)
            {
                _health.TriggerInvulnerability(dashDuration);
            }

            // Change sprite color to indicate active dash/invulnerability
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(0.3f, 0.8f, 1f, 0.6f); // Light blue tint
            }

            IgnoreCollisionWithBoundaries(true);

            if (_dashDirection.sqrMagnitude > 0.001f)
            {
                transform.forward = _dashDirection;
            }
            if (_animator == null) _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.SetBool("IsDashing", true);
                _animator.ResetTrigger("DashRoll");
                _animator.SetTrigger("DashRoll");
                if (_animator.layerCount > 1) _animator.Play("UpperBody_Empty", 1, 0f);
                _shootAnimLayerTimer = 0f;
                _meleeAnimTimer = 0f;
                if (_animator.layerCount > 1)
                {
                    _animator.SetLayerWeight(1, 0f);
                    _animator.Play("UpperBody_Empty", 1, 0f);
                }
                _animator.Play("DashRoll", 0, 0f);
            }
        }

        private void EndDash()
        {
            _isDashing = false;
            
            // Restore original sprite color
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalColor;
            }

            IgnoreCollisionWithBoundaries(false);
            if (_animator != null)
            {
                _animator.SetBool("IsDashing", false);
            }
        }

        private bool CanVehicleVaultAhead(out Transform vehicleTarget)
        {
            vehicleTarget = null;
            if (_isVaulting || _isDashing || _dashCooldownTimer > 0f) return false;
            if (_playerCollider == null) _playerCollider = GetComponent<Collider>();

            Vector3 dir = _moveInput.sqrMagnitude > 0.01f
                ? new Vector3(_moveInput.x, 0f, _moveInput.y).normalized
                : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;

            Vector3 origin = transform.position + Vector3.up * 0.65f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.42f, dir, 1.85f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Collider targetCol = null;
            RaycastHit targetHit = default;
            VehicleVaultKind kind = VehicleVaultKind.None;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (c.transform.root == transform.root) continue;
                if (c.CompareTag("Player") || c.CompareTag("Enemy")) continue;
                if (c.GetComponent<BaseEnemyAI>() != null || c.GetComponent<CollectibleItem>() != null) continue;
                if (hits[i].normal.y > 0.72f) continue;

                VehicleVaultKind detected = ClassifyVehicleVaultKind(c.transform);
                if (detected != VehicleVaultKind.None)
                {
                    targetCol = c;
                    targetHit = hits[i];
                    kind = detected;
                    break;
                }
            }

            if (targetCol == null || kind == VehicleVaultKind.None) return false;

            Vector3 startPos = transform.position;
            Vector3 probeOrigin = new Vector3(targetHit.point.x, targetCol.bounds.center.y, targetHit.point.z) + dir * 6.5f;
            Vector3 exitPoint;
            if (targetCol.Raycast(new Ray(probeOrigin, -dir), out RaycastHit exitHit, 7.5f))
            {
                exitPoint = exitHit.point;
            }
            else
            {
                Bounds b = targetCol.bounds;
                float proj = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
                exitPoint = new Vector3(b.center.x, startPos.y, b.center.z) + dir * proj;
            }

            Vector3 landingPos = new Vector3(exitPoint.x, startPos.y, exitPoint.z) + dir * 0.78f;
            float span = Vector3.Distance(new Vector3(startPos.x, 0f, startPos.z), new Vector3(landingPos.x, 0f, landingPos.z));
            if (span < 0.85f || span > 7.4f) return false;

            Transform vehicleRoot = FindVehicleRoot(targetCol.transform);
            if (IsVaultLandingBlocked(landingPos, vehicleRoot))
            {
                Vector3 altLanding = new Vector3(exitPoint.x, startPos.y, exitPoint.z) + dir * 1.25f;
                if (IsVaultLandingBlocked(altLanding, vehicleRoot))
                {
                    return false;
                }
            }

            vehicleTarget = vehicleRoot != null ? vehicleRoot : targetCol.transform;
            return true;
        }

        private bool TryStartVehicleVault(Vector3 dir)
        {
            if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
            Vector3 origin = transform.position + Vector3.up * 0.65f;

            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.42f, dir, 1.85f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Collider targetCol = null;
            RaycastHit targetHit = default;
            VehicleVaultKind kind = VehicleVaultKind.None;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (c.transform.root == transform.root) continue;
                if (c.CompareTag("Player") || c.CompareTag("Enemy")) continue;
                if (c.GetComponent<BaseEnemyAI>() != null || c.GetComponent<CollectibleItem>() != null) continue;
                if (hits[i].normal.y > 0.72f) continue; // Ignore flat ground

                VehicleVaultKind detected = ClassifyVehicleVaultKind(c.transform);
                if (detected != VehicleVaultKind.None)
                {
                    targetCol = c;
                    targetHit = hits[i];
                    kind = detected;
                    break;
                }
            }

            if (targetCol == null || kind == VehicleVaultKind.None) return false;

            // Find exact exit edge on the opposite side of the vehicle along dir
            Vector3 startPos = transform.position;
            Vector3 probeOrigin = new Vector3(targetHit.point.x, targetCol.bounds.center.y, targetHit.point.z) + dir * 6.5f;
            Vector3 exitPoint;
            if (targetCol.Raycast(new Ray(probeOrigin, -dir), out RaycastHit exitHit, 7.5f))
            {
                exitPoint = exitHit.point;
            }
            else
            {
                // Fallback to bounding-box projection along dir
                Bounds b = targetCol.bounds;
                float proj = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
                exitPoint = new Vector3(b.center.x, startPos.y, b.center.z) + dir * proj;
            }

            Vector3 landingPos = new Vector3(exitPoint.x, startPos.y, exitPoint.z) + dir * 0.78f;
            float span = Vector3.Distance(new Vector3(startPos.x, 0f, startPos.z), new Vector3(landingPos.x, 0f, landingPos.z));
            if (span < 0.85f || span > 7.4f) return false;

            // Ensure landing spot isn't buried inside a solid building wall
            Transform vehicleRoot = FindVehicleRoot(targetCol.transform);
            if (IsVaultLandingBlocked(landingPos, vehicleRoot))
            {
                Vector3 altLanding = new Vector3(exitPoint.x, startPos.y, exitPoint.z) + dir * 1.25f;
                if (IsVaultLandingBlocked(altLanding, vehicleRoot))
                {
                    return false;
                }
                landingPos = altLanding;
                span = Vector3.Distance(new Vector3(startPos.x, 0f, startPos.z), new Vector3(landingPos.x, 0f, landingPos.z));
            }

            // Compute exact top surface Y relative to the player's foot-to-root offset
            float footToRoot = _playerCollider != null ? Mathf.Max(0.1f, startPos.y - _playerCollider.bounds.min.y) : 0.65f;
            float vehicleTopY = targetCol.bounds.max.y;

            _isVaulting = true;
            _vaultKind = kind;
            _vaultElapsed = 0f;
            _vaultStartPos = startPos;
            _vaultEndPos = landingPos;
            _dashDirection = dir;
            _dashCooldownTimer = dashCooldown;

            if (kind == VehicleVaultKind.Motorcycle)
            {
                _vaultDuration = Mathf.Clamp(0.36f + span * 0.04f, 0.38f, 0.50f);
                _vaultSurfaceY = Mathf.Clamp(vehicleTopY + footToRoot + 0.18f, startPos.y + 0.90f, startPos.y + 2.35f);
            }
            else
            {
                _vaultDuration = Mathf.Clamp(0.42f + span * 0.055f, 0.48f, 0.72f);
                // Vault_CarSlide lowers Hips onto the car roof, so position player root cleanly on top of roof surface
                _vaultSurfaceY = Mathf.Clamp(vehicleTopY + footToRoot - 0.08f, startPos.y + 0.85f, startPos.y + 2.75f);
            }

            if (_health != null) _health.TriggerInvulnerability(_vaultDuration + 0.08f);
            IgnoreCollisionWithBoundaries(true);

            // Temporarily ignore collisions with all colliders on this vehicle while vaulting/sliding over it
            _vaultIgnoredColliders.Clear();
            if (_playerCollider != null && vehicleRoot != null)
            {
                Collider[] vCols = vehicleRoot.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < vCols.Length; i++)
                {
                    if (vCols[i] != null && !vCols[i].isTrigger)
                    {
                        Physics.IgnoreCollision(_playerCollider, vCols[i], true);
                        _vaultIgnoredColliders.Add(vCols[i]);
                    }
                }
            }

            if (_rb != null)
            {
                _vaultPrevUseGravity = _rb.useGravity;
                _rb.useGravity = false;
                _rb.linearVelocity = Vector3.zero;
            }

            transform.forward = dir;

            if (_animator == null) _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                string stateName = kind == VehicleVaultKind.Motorcycle ? "Vault_MotoJump" : "Vault_CarSlide";
                if (!_animator.HasState(0, Animator.StringToHash(stateName)))
                {
                    stateName = "DashRoll";
                }
                _shootAnimLayerTimer = 0f;
                _meleeAnimTimer = 0f;
                if (_animator.layerCount > 1)
                {
                    _animator.SetLayerWeight(1, 0f);
                    _animator.Play("UpperBody_Empty", 1, 0f);
                }
                _animator.SetBool("IsDashing", true);
                _animator.Play(stateName, 0, 0f);
            }

            Debug.Log($"[PlayerController] Vehicle Parkour: {(kind == VehicleVaultKind.Motorcycle ? "Motorcycle Hurdle Jump" : "Car Roof Slide")} over '{vehicleRoot?.name}' (span={span:F2}m, topY={vehicleTopY:F2}m)");
            return true;
        }

        private void UpdateVehicleVault()
        {
            _vaultElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(_vaultElapsed / Mathf.Max(0.05f, _vaultDuration));

            // Smooth ease-in-out horizontal progress across vehicle
            float horizT = u * u * (3f - 2f * u);
            Vector3 pos = Vector3.Lerp(_vaultStartPos, _vaultEndPos, horizT);

            if (_vaultKind == VehicleVaultKind.Motorcycle)
            {
                // Parabolic hurdle jump arc cleanly clearing the motorcycle
                float arc = 4f * u * (1f - u);
                pos.y = Mathf.Lerp(_vaultStartPos.y, _vaultSurfaceY, arc);
            }
            else
            {
                // 3-phase Hollywood Car Roof/Hood Slide:
                // 0.00..0.20: spring up onto car roof | 0.20..0.78: flat glide across roof | 0.78..1.00: crisp drop-off landing
                if (u < 0.20f)
                {
                    float t = u / 0.20f;
                    pos.y = Mathf.Lerp(_vaultStartPos.y, _vaultSurfaceY, Mathf.Sin(t * Mathf.PI * 0.5f));
                }
                else if (u <= 0.78f)
                {
                    pos.y = _vaultSurfaceY;
                }
                else
                {
                    float t = (u - 0.78f) / 0.22f;
                    pos.y = Mathf.Lerp(_vaultSurfaceY, _vaultStartPos.y, t * t);
                }
            }

            transform.position = pos;
            if (_dashDirection.sqrMagnitude > 0.001f)
            {
                transform.forward = _dashDirection;
            }

            if (u >= 1f)
            {
                EndVehicleVault();
            }
        }

        private void EndVehicleVault()
        {
            if (!_isVaulting) return;
            _isVaulting = false;
            _vaultKind = VehicleVaultKind.None;

            transform.position = _vaultEndPos;

            if (_rb != null)
            {
                _rb.useGravity = _vaultPrevUseGravity;
                _rb.linearVelocity = Vector3.zero;
            }

            if (_playerCollider != null)
            {
                for (int i = 0; i < _vaultIgnoredColliders.Count; i++)
                {
                    if (_vaultIgnoredColliders[i] != null)
                    {
                        Physics.IgnoreCollision(_playerCollider, _vaultIgnoredColliders[i], false);
                    }
                }
            }
            _vaultIgnoredColliders.Clear();
            IgnoreCollisionWithBoundaries(false);

            if (_animator != null)
            {
                _animator.SetBool("IsDashing", false);
            }
        }

        private static Transform FindVehicleRoot(Transform t)
        {
            Transform cur = t;
            Transform best = t;
            while (cur != null)
            {
                if (ClassifyVehicleVaultKindSingle(cur.name) != VehicleVaultKind.None)
                {
                    best = cur;
                }
                if (cur.parent != null && cur.parent.name.IndexOf("GeneratedEnvironment", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }
                cur = cur.parent;
            }
            return best;
        }

        private static VehicleVaultKind ClassifyVehicleVaultKind(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                VehicleVaultKind k = ClassifyVehicleVaultKindSingle(cur.name);
                if (k != VehicleVaultKind.None) return k;
                if (cur.parent != null && cur.parent.name.IndexOf("GeneratedEnvironment", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }
                cur = cur.parent;
            }
            return VehicleVaultKind.None;
        }

        private static VehicleVaultKind ClassifyVehicleVaultKindSingle(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return VehicleVaultKind.None;
            string n = rawName.ToLowerInvariant();

            // Exclude non-vehicle props whose name might contain "bus" or similar (e.g. Bus_Stop)
            if (n.Contains("bus_stop") || n.Contains("busstop") || n.Contains("prop_") ||
                n.Contains("building") || n.Contains("house") || n.Contains("wall") || n.Contains("fence"))
            {
                return VehicleVaultKind.None;
            }

            // 1. Motorcycles -> Hurdle Jump!
            if (n.Contains("motorcycle") || n.Contains("scooter") || n.Contains("dirt_bike") ||
                n.Contains("dirtbike") || n.Contains("scrambler") || n.Contains("motorbike") ||
                n.Contains("moto_") || n.Contains("_bike"))
            {
                return VehicleVaultKind.Motorcycle;
            }

            // 2. Cars / SUVs / Taxis / Vans / Trucks / Buses -> Hood/Roof Slide!
            if (n.Contains("sedan") || n.Contains("suv") || n.Contains("taxi") ||
                n.Contains("van") || n.Contains("truck") || n.Contains("firetruck") ||
                n.Contains("vehicle") || n.Contains("car_") || n.Contains("_car") ||
                n.Contains("jeep") || n.Contains("pickup") || n.Contains("ambulance") ||
                n.Contains("abandoned_bus"))
            {
                return VehicleVaultKind.Car;
            }

            return VehicleVaultKind.None;
        }

        private bool IsVaultLandingBlocked(Vector3 pos, Transform ignoredVehicleRoot)
        {
            Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * 0.85f, 0.32f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null) continue;
                if (c.transform.root == transform.root) continue;
                if (ignoredVehicleRoot != null && c.transform.IsChildOf(ignoredVehicleRoot)) continue;
                if (c.CompareTag("Player") || c.CompareTag("Enemy")) continue;
                if (c.GetComponent<BaseEnemyAI>() != null || c.GetComponent<CollectibleItem>() != null) continue;
                if (c.gameObject.name.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.gameObject.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.gameObject.name.IndexOf("Road", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private void CheckBoundaries()
        {
            // Clamping is now handled physically by the screen boundary colliders generated in SetupScreenBoundaries()!
        }

        private void OnDrawGizmosSelected()
        {
            // Draw melee attack radius in editor
            Gizmos.color = Color.red;
            Vector3 attackPoint = transform.position + _aimDirection * 0.8f;
            Gizmos.DrawWireSphere(attackPoint, meleeRadius);
        }

        private void SetupScreenBoundaries()
        {
            _playerCollider = GetComponent<Collider>();
            if (_playerCollider == null) return;

            Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            _boundaryContainer = new GameObject("ScreenBoundaries");
            
            // Set unrotated World Space position matching camera X/Z
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            _boundaryContainer.transform.position = new Vector3(camPos.x, 0f, camPos.z);
            _boundaryContainer.transform.rotation = Quaternion.identity;

            // Fallback to safe defaults if yLimit or xLimit is set to 0 in the Inspector
            float calculatedYLimit = yLimit > 0.1f ? yLimit : 5f;
            float calculatedXLimit = xLimit > 0.1f ? xLimit : 8.5f;

            if (cam != null && cam.orthographic)
            {
                calculatedYLimit = Mathf.Max(3f, cam.orthographicSize);
                calculatedXLimit = Mathf.Max(4f, calculatedYLimit * cam.aspect);
            }

            float thickness = 2f; // prevent tunneling

            // Left
            GameObject leftObj = new GameObject("Boundary_Left");
            leftObj.transform.parent = _boundaryContainer.transform;
            _boundaryLeft = leftObj.AddComponent<BoxCollider>();
            _boundaryLeft.center = new Vector3(-calculatedXLimit - thickness / 2, 0, 0);
            _boundaryLeft.size = new Vector3(thickness, 10f, calculatedYLimit * 2 + thickness * 2);

            // Right
            GameObject rightObj = new GameObject("Boundary_Right");
            rightObj.transform.parent = _boundaryContainer.transform;
            _boundaryRight = rightObj.AddComponent<BoxCollider>();
            _boundaryRight.center = new Vector3(calculatedXLimit + thickness / 2, 0, 0);
            _boundaryRight.size = new Vector3(thickness, 10f, calculatedYLimit * 2 + thickness * 2);

            // Top
            GameObject topObj = new GameObject("Boundary_Top");
            topObj.transform.parent = _boundaryContainer.transform;
            _boundaryTop = topObj.AddComponent<BoxCollider>();
            _boundaryTop.center = new Vector3(0, 0, calculatedYLimit + thickness / 2);
            _boundaryTop.size = new Vector3(calculatedXLimit * 2 + thickness * 2, 10f, thickness);

            // Bottom
            GameObject bottomObj = new GameObject("Boundary_Bottom");
            bottomObj.transform.parent = _boundaryContainer.transform;
            _boundaryBottom = bottomObj.AddComponent<BoxCollider>();
            _boundaryBottom.center = new Vector3(0, 0, -calculatedYLimit - thickness / 2);
            _boundaryBottom.size = new Vector3(calculatedXLimit * 2 + thickness * 2, 10f, thickness);

            // Log diagnostic configuration values
            Debug.LogWarning($"[SETUP SCREEN BOUNDARIES] Cam: {cam != null}, CamName: {(cam != null ? cam.gameObject.name : "null")}, orthographicSize: {(cam != null ? cam.orthographicSize : 0f)}, aspect: {(cam != null ? cam.aspect : 0f)}");
            Debug.LogWarning($"[SETUP SCREEN BOUNDARIES] calculatedYLimit: {calculatedYLimit}, calculatedXLimit: {calculatedXLimit}");
        }

        private void IgnoreCollisionWithBoundaries(bool ignore)
        {
            if (_playerCollider == null) return;
            if (_boundaryLeft != null) Physics.IgnoreCollision(_playerCollider, _boundaryLeft, ignore);
            if (_boundaryRight != null) Physics.IgnoreCollision(_playerCollider, _boundaryRight, ignore);
            if (_boundaryTop != null) Physics.IgnoreCollision(_playerCollider, _boundaryTop, ignore);
            if (_boundaryBottom != null) Physics.IgnoreCollision(_playerCollider, _boundaryBottom, ignore);
        }

        public int GetAmmoReserveCount(string ammoType)
        {
            int count = 0;
            if (_inventory != null && !string.IsNullOrEmpty(ammoType))
            {
                count = _inventory.GetItemCount(ammoType);
                string alias = GetAmmoAlias(ammoType);
                if (!string.IsNullOrEmpty(alias) && !alias.Equals(ammoType, System.StringComparison.OrdinalIgnoreCase))
                {
                    count += _inventory.GetItemCount(alias);
                }
            }

            // Fallback to internal weapon reserve if inventory has none (ensures weapons like Sniper Rifle / Crossbow work even if not yet picked up from loot)
            if (count == 0 && CurrentWeapon != null && CurrentWeapon.currentReserveAmmo > 0)
            {
                count = CurrentWeapon.currentReserveAmmo;
            }

            return count;
        }

        public string GetAmmoAlias(string ammoType)
        {
            if (string.IsNullOrEmpty(ammoType)) return null;
            string clean = ammoType.Trim().ToLower();
            if (clean == "light ammo") return "Pistol Ammo";
            if (clean == "pistol ammo") return "Light Ammo";
            if (clean == "medium ammo") return "Rifle Ammo";
            if (clean == "rifle ammo") return "Medium Ammo";
            if (clean.Contains("sniper")) return "Medium Ammo";
            if (clean == "heavy ammo") return "Fuel Ammo";
            if (clean == "gas canister" || clean == "gas" || clean == "fuel ammo") return "Gas Canister";
            if (clean == "shotgun ammo") return "Ammo";
            if (clean == "ammo") return "Shotgun Ammo";
            if (clean == "bolts" || clean == "bolt") return "Bolts";
            return null;
        }

        public void ConsumeAmmo(string ammoType, int amount)
        {
            if (amount <= 0) return;

            int invMain = (_inventory != null && !string.IsNullOrEmpty(ammoType)) ? _inventory.GetItemCount(ammoType) : 0;
            string alias = GetAmmoAlias(ammoType);
            int invAlias = (!string.IsNullOrEmpty(alias) && _inventory != null) ? _inventory.GetItemCount(alias) : 0;

            if (invMain + invAlias > 0 && _inventory != null)
            {
                int fromMain = Mathf.Min(amount, invMain);
                for (int i = 0; i < fromMain; i++) _inventory.RemoveItem(ammoType);

                int remaining = amount - fromMain;
                if (remaining > 0 && !string.IsNullOrEmpty(alias))
                {
                    int fromAlias = Mathf.Min(remaining, invAlias);
                    for (int i = 0; i < fromAlias; i++) _inventory.RemoveItem(alias);
                }
            }
            else if (CurrentWeapon != null && CurrentWeapon.currentReserveAmmo > 0)
            {
                CurrentWeapon.currentReserveAmmo = Mathf.Max(0, CurrentWeapon.currentReserveAmmo - amount);
            }
        }

        private void TryStartReload()
        {
            Weapon activeWeapon = CurrentWeapon;
            if (activeWeapon == null) return; // ถ้าไม่ใส่ปืน ห้ามรีโหลด
            
            WeaponArtifactModifiers reloadMods = WeaponArtifactManager.GetAggregatedModifiers(activeWeapon.weaponName);
            int mag = activeWeapon != null ? activeWeapon.currentMagazine : _currentMagazine;
            int magSize = (activeWeapon != null ? activeWeapon.magazineSize : magazineSize) + Mathf.Max(0, reloadMods.bonusMagazineSize);
            
            string ammoType = "Pistol Ammo";
            if (activeWeapon != null && !string.IsNullOrEmpty(activeWeapon.ammoType))
            {
                ammoType = activeWeapon.ammoType;
            }
            int reserve = GetAmmoReserveCount(ammoType);

            if (_isReloading || mag >= magSize || reserve <= 0)
            {
                return;
            }
            StartCoroutine(ReloadCoroutine());
        }

        private System.Collections.IEnumerator ReloadCoroutine()
        {
            _isReloading = true;
            OnAmmoChanged?.Invoke();

            Weapon activeWeapon = CurrentWeapon;
            WeaponArtifactModifiers reloadMods = activeWeapon != null
                ? WeaponArtifactManager.GetAggregatedModifiers(activeWeapon.weaponName)
                : default;
            float duration = activeWeapon != null ? activeWeapon.reloadDuration : reloadDuration;
            if (reloadMods.reloadSpeedMultiplier > 0f)
            {
                duration /= (1f + reloadMods.reloadSpeedMultiplier);
            }

            yield return new WaitForSeconds(duration);

            // Re-fetch current weapon status
            activeWeapon = CurrentWeapon;
            string ammoType = "Pistol Ammo";
            if (activeWeapon != null && !string.IsNullOrEmpty(activeWeapon.ammoType))
            {
                ammoType = activeWeapon.ammoType;
            }

            if (activeWeapon != null)
            {
                WeaponArtifactModifiers postMods = WeaponArtifactManager.GetAggregatedModifiers(activeWeapon.weaponName);
                int effectiveMagSize = activeWeapon.magazineSize + Mathf.Max(0, postMods.bonusMagazineSize);
                int needed = Mathf.Max(0, effectiveMagSize - activeWeapon.currentMagazine);
                int reserve = GetAmmoReserveCount(ammoType);
                int toLoad = Mathf.Min(needed, reserve);
                activeWeapon.currentMagazine += toLoad;
                ConsumeAmmo(ammoType, toLoad);
            }
            else
            {
                int needed = magazineSize - _currentMagazine;
                int reserve = GetAmmoReserveCount(ammoType);
                int toLoad = Mathf.Min(needed, reserve);
                _currentMagazine += toLoad;
                ConsumeAmmo(ammoType, toLoad);
            }

            _isReloading = false;
            OnAmmoChanged?.Invoke();
        }

        private void UpdateHunger()
        {
            if (_health != null && _health.IsDead) return;

            // อาหารลดช้าลงอีกเท่าตัว (2x slower); ฝนตกนอกบ้านลดเร็วขึ้น 2 เท่า (2x faster)
            float baseRate = (activeCharacterData != null ? activeCharacterData.baseHungerDepletionRate : 0.5f) * 0.5f;
            float sprintMult = activeCharacterData != null ? activeCharacterData.sprintHungerMultiplier : 4.0f;
            float charMult = activeCharacterData != null ? activeCharacterData.hungerConsumptionMultiplier : 1.0f;
            float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.SurvivalDrainMultiplier : 1.0f;
            float depletionRate = (_isSprinting ? (baseRate * sprintMult) : baseRate) * charMult * hungerDepletionMultiplier * weatherMult;
            currentHunger = Mathf.Max(0f, currentHunger - Time.deltaTime * depletionRate);
            OnHungerChanged?.Invoke();

            if (currentHunger <= 0f)
            {
                _hungerDamageTimer += Time.deltaTime;
                float interval = activeCharacterData != null ? activeCharacterData.starvationInterval : 2.0f;
                if (_hungerDamageTimer >= interval)
                {
                    _hungerDamageTimer = 0f;
                    if (_health != null && !_health.IsDead)
                    {
                        float dmg = activeCharacterData != null ? activeCharacterData.starvationDamage : 5.0f;
                        _health.TakeDamage(dmg);
                        Debug.Log($"[PlayerController] Starving! Took {dmg} damage.");
                    }
                }
            }
            else
            {
                _hungerDamageTimer = 0f;
            }
        }

        private void UpdateWater()
        {
            if (_health != null && _health.IsDead) return;

            // น้ำจะลดเร็วกว่าอาหาร 3 เท่า (โดยอิงจากอัตราอาหารใหม่ที่ลดช้าลงเท่าตัว); ฝนตกนอกบ้านลดเร็วขึ้น 2 เท่า
            float baseHungerRate = (activeCharacterData != null ? activeCharacterData.baseHungerDepletionRate : 0.5f) * 0.5f;
            float baseWaterRate = baseHungerRate * 3.0f; // น้ำลดเร็วกว่าอาหาร 3 เท่า
            float sprintMult = activeCharacterData != null ? activeCharacterData.sprintHungerMultiplier : 4.0f;
            float charMult = activeCharacterData != null ? activeCharacterData.waterConsumptionMultiplier : 1.0f;
            float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.SurvivalDrainMultiplier : 1.0f;
            float depletionRate = (_isSprinting ? (baseWaterRate * sprintMult) : baseWaterRate) * charMult * waterDepletionMultiplier * weatherMult;
            currentWater = Mathf.Max(0f, currentWater - Time.deltaTime * depletionRate);
            OnWaterChanged?.Invoke();

            if (currentWater <= 0f)
            {
                _waterDamageTimer += Time.deltaTime;
                float interval = activeCharacterData != null ? activeCharacterData.starvationInterval : 2.0f;
                if (_waterDamageTimer >= interval)
                {
                    _waterDamageTimer = 0f;
                    if (_health != null && !_health.IsDead)
                    {
                        // เลือดค่อยๆ ลดให้น้อยกว่าตอนหิวครึ่งหนึ่ง (Half of starvation damage)
                        float baseStarveDmg = activeCharacterData != null ? activeCharacterData.starvationDamage : 5.0f;
                        float dehydrationDmg = baseStarveDmg * 0.5f;
                        _health.TakeDamage(dehydrationDmg);
                        Debug.Log($"[PlayerController] Dehydrated! Took {dehydrationDmg} damage.");
                    }
                }
            }
            else
            {
                _waterDamageTimer = 0f;
            }
        }

        public void DrinkWater(float amount)
        {
            currentWater = Mathf.Clamp(currentWater + amount, 0f, maxWater);
            Debug.Log($"[PlayerController] Drank water! Restored {amount} water. Current water: {currentWater}/{maxWater}");
            OnWaterChanged?.Invoke();
        }

        public void EatBread(float amount)
        {
            currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
            Debug.Log($"[PlayerController] Ate bread! Restored {amount} hunger. Current hunger: {currentHunger}");
            
            // Bonus healing on eating food (e.g. Scavenger perk or configured food heal bonus)
            if (activeCharacterData != null && activeCharacterData.foodHealBonus > 0f && _health != null)
            {
                _health.Heal(activeCharacterData.foodHealBonus);
                Debug.Log($"[PlayerController] Passive food heal bonus: Restored +{activeCharacterData.foodHealBonus} HP from eating bread!");
            }

            OnHungerChanged?.Invoke();
        }

        private void UpdateSanity()
        {
            if (_health != null && _health.IsDead) return;

            if (_combatTimer > 0f)
            {
                _combatTimer -= Time.deltaTime;
            }

            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            bool inCombat = _combatTimer > 0f;
            bool exposedToRain = WeatherManager.Instance != null && WeatherManager.Instance.IsPlayerExposedToRain;
            float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.SurvivalDrainMultiplier : 1.0f;

            if (isNight)
            {
                // กลางคืน: ค่าสติค่อยๆ ลดลงตามเวลา (ฝนตกนอกบ้านลดเร็วขึ้น 2 เท่า)
                float rate = nightSanityDepletionRate;
                if (inCombat)
                {
                    // ถ้ากำลังต่อสู้ (Combat) ตอนกลางคืน ลดเร็วขึ้นตามตัวคูณ
                    rate *= combatNightSanityMultiplier;
                }
                float nightLightMult = NightCombatItemManager.Instance != null
                    ? NightCombatItemManager.Instance.GetNightSanityDrainMultiplier()
                    : 1f;
                float delta = rate * sanityDepletionMultiplier * nightLightMult * weatherMult * Time.deltaTime;
                currentSanity = Mathf.Max(0f, currentSanity - delta);
                OnSanityChanged?.Invoke();
            }
            else if (inCombat)
            {
                // กลางวันแต่ติด Combat (ฝนตกนอกบ้านลดเร็วขึ้น 2 เท่า)
                float delta = combatDaySanityDepletionRate * sanityDepletionMultiplier * weatherMult * Time.deltaTime;
                currentSanity = Mathf.Max(0f, currentSanity - delta);
                OnSanityChanged?.Invoke();
            }
            else if (exposedToRain)
            {
                // ตากฝนกลางแจ้งตอนกลางวัน: ค่าสติลดลง 2 เท่า (หลบเข้าบ้านเพื่อหยุดการลด)
                float rainSanityRate = Mathf.Max(0.25f, nightSanityDepletionRate);
                float delta = rainSanityRate * sanityDepletionMultiplier * weatherMult * Time.deltaTime;
                currentSanity = Mathf.Max(0f, currentSanity - delta);
                OnSanityChanged?.Invoke();
            }
            else
            {
                // กลางวันและปลอดภัย: ค่อยๆ ฟื้นฟูสติกลับคืนมา
                if (currentSanity < maxSanity && daySanityRecoveryRate > 0f)
                {
                    currentSanity = Mathf.Min(maxSanity, currentSanity + (daySanityRecoveryRate * Time.deltaTime));
                    OnSanityChanged?.Invoke();
                }
            }

            // ฟื้นฟูสติจากรัศมีแคมป์ไฟ / Safe Zone / Light Aura (ทำงานต่อเนื่องแม้ในตอนกลางคืน)
            if (_activeSanityAuraBonus > 0f && currentSanity < maxSanity)
            {
                currentSanity = Mathf.Min(maxSanity, currentSanity + (_activeSanityAuraBonus * Time.deltaTime));
                OnSanityChanged?.Invoke();
            }

            // เมื่อค่าสติหมด (0) จะเกิดอาการหลอน / เสียขวัญ (Mental Breakdown) โดนดาเมจเป็นระยะ
            if (currentSanity <= 0f)
            {
                _sanityDamageTimer += Time.deltaTime;
                if (_sanityDamageTimer >= 3.0f)
                {
                    _sanityDamageTimer = 0f;
                    if (_health != null && !_health.IsDead)
                    {
                        _health.TakeDamage(5f);
                        Debug.Log("[PlayerController] Mental breakdown! Took 5 damage due to 0 Sanity.");
                    }
                }
            }
            else
            {
                _sanityDamageTimer = 0f;
            }
        }

        private void HandleDamageTaken(float damageAmount)
        {
            TriggerCombatState(combatDuration);

            // ลดค่าสติทีละ 10% ของดาเมจที่ได้รับเมื่อโดนกัด/โดนโจมตี
            float sanityLoss = damageAmount * sanityDamageTakenPercent * sanityDepletionMultiplier;
            if (sanityLoss > 0f)
            {
                DrainSanity(sanityLoss);
                Debug.Log($"[PlayerController] Took {damageAmount} damage! Sanity dropped by {sanityLoss:F1} (Current Sanity: {currentSanity:F1}/{maxSanity})");
            }
        }

        public void TriggerCombatState(float duration = -1f)
        {
            float dur = duration > 0f ? duration : combatDuration;
            _combatTimer = Mathf.Max(_combatTimer, dur);
        }

        public void DrainSanity(float amount)
        {
            if (amount <= 0f) return;
            float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.SurvivalDrainMultiplier : 1f;
            currentSanity = Mathf.Max(0f, currentSanity - (amount * sanityDepletionMultiplier * weatherMult));
            OnSanityChanged?.Invoke();
        }

        public void RestoreSanity(float amount)
        {
            if (amount <= 0f) return;
            currentSanity = Mathf.Min(maxSanity, currentSanity + amount);
            OnSanityChanged?.Invoke();
        }

        [Header("Combat Sanity Recovery")]
        [Tooltip("ค่าสติที่ฟื้นฟูเมื่อกำจัดศัตรูทั่วไป (Normal Kill)")]
        [SerializeField] private float sanityOnKillNormal = 4.0f;
        [Tooltip("ค่าสติที่ฟื้นฟูเมื่อกำจัดศัตรูระดับสูง/Elite/Boss (เลือด >= 150)")]
        [SerializeField] private float sanityOnKillElite = 20.0f;

        private float _activeSanityAuraBonus = 0f;

        /// <summary>
        /// Registers a safe zone / light / campfire sanity recovery aura.
        /// </summary>
        public void AddSanityAura(float regenRatePerSec)
        {
            _activeSanityAuraBonus += regenRatePerSec;
        }

        /// <summary>
        /// Unregisters a safe zone / light / campfire sanity recovery aura.
        /// </summary>
        public void RemoveSanityAura(float regenRatePerSec)
        {
            _activeSanityAuraBonus = Mathf.Max(0f, _activeSanityAuraBonus - regenRatePerSec);
        }

        /// <summary>
        /// Called when an enemy is defeated in combat to reward sanity.
        /// </summary>
        public void OnEnemyDefeated(BaseEnemyAI enemy)
        {
            if (enemy == null || (_health != null && _health.IsDead)) return;

            Health eHealth = enemy.GetComponent<Health>();
            float maxHp = eHealth != null ? eHealth.MaxHealth : 50f;
            string eName = enemy.gameObject.name;
            bool isElite = maxHp >= 150f || eName.Contains("Boss") || eName.Contains("Titan") || eName.Contains("Abomination");
            float gain = isElite ? sanityOnKillElite : sanityOnKillNormal;

            if (activeCharacterData != null && activeCharacterData.rebelMeleeKillHungerRestore > 0f)
            {
                gain += 2.0f;
            }

            RestoreSanity(gain);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterEnemyKilled();
            }
            Debug.Log($"[PlayerController] Defeated {eName}! Regained {gain:F1} Sanity. ({currentSanity:F1}/{maxSanity}) | Total Kills: {(GameManager.Instance != null ? GameManager.Instance.TotalEnemiesDefeated : 1)}");
        }

        private void ApplyStartingCharacterLoadout()
        {
            if (activeCharacterData == null) return;

            // Equip matching primary starting weapon
            if (activeCharacterData.startingWeapons != null && weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (activeCharacterData.startingWeapons.Exists(w => w.Equals(weapons[i].weaponName, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        _currentWeaponIndex = i;
                        break;
                    }
                }
            }

            // Equip matching starting melee weapon
            if (activeCharacterData.startingWeapons != null && meleeWeapons != null)
            {
                for (int i = 0; i < meleeWeapons.Count; i++)
                {
                    if (activeCharacterData.startingWeapons.Exists(w => w.Equals(meleeWeapons[i].weaponName, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        _currentMeleeWeaponIndex = i;
                        break;
                    }
                }
            }

            // Grant starting inventory supplies
            if (_inventory != null)
            {
                bool prevSuppress = _inventory.SuppressNotificationEvents;
                _inventory.SuppressNotificationEvents = true;
                try
                {
                    if (activeCharacterData.maxInventorySlots >= 18)
                    {
                        _inventory.SetMaxSlots(activeCharacterData.maxInventorySlots);
                    }

                    if (activeCharacterData.startingMoney > 0)
                    {
                        _inventory.AddMoney(activeCharacterData.startingMoney);
                    }

                    // Add starting weapons into inventory list so player ownership is registered!
                    if (activeCharacterData.startingWeapons != null)
                    {
                        foreach (var sw in activeCharacterData.startingWeapons)
                        {
                            if (!_inventory.Items.Exists(x => IsWeaponNameMatch(x, sw)))
                            {
                                _inventory.AddItem(sw);
                            }
                        }
                    }

                    // Grant starting ammo from CharacterData ScriptableObject
                    if (activeCharacterData.startingAmmo != null && activeCharacterData.startingAmmo.Count > 0)
                    {
                        foreach (var ammoEntry in activeCharacterData.startingAmmo)
                        {
                            if (!string.IsNullOrEmpty(ammoEntry.ammoType) && ammoEntry.count > 0)
                            {
                                _inventory.AddItem(ammoEntry.ammoType, ammoEntry.count);
                            }
                        }
                    }
                    else if (activeCharacterData.startingPistolAmmo > 0)
                    {
                        // Fallback to role-tailored ammo if startingAmmo list is unassigned
                        string fallbackAmmo = "Light Ammo";
                        if (activeCharacterData.characterId == "VANGUARD" || activeCharacterData.characterId == "MARKSMAN")
                            fallbackAmmo = "Medium Ammo";
                        else if (activeCharacterData.characterId == "JUGGERNAUT")
                            fallbackAmmo = "Shotgun Ammo";
                        else if (activeCharacterData.characterId == "SCAVENGER")
                            fallbackAmmo = "Bolts";

                        _inventory.AddItem(fallbackAmmo, activeCharacterData.startingPistolAmmo);
                    }

                    // Grant starting survival rations (e.g. Bread)
                    int rationCount = activeCharacterData.startingRations > 0 ? activeCharacterData.startingRations : 2;
                    if (rationCount > 0)
                    {
                        _inventory.AddItem("Bread", rationCount);
                    }

                    // Grant starting water bottles (3x Water Bottle)
                    int waterCount = activeCharacterData.startingWater > 0 ? activeCharacterData.startingWater : 3;
                    if (waterCount > 0)
                    {
                        _inventory.AddItem("Water Bottle", waterCount);
                    }

                    // Grant starting extra items if configured
                    if (activeCharacterData.startingExtraItems != null)
                    {
                        foreach (var extra in activeCharacterData.startingExtraItems)
                        {
                            if (!string.IsNullOrEmpty(extra.itemName) && extra.count > 0)
                            {
                                _inventory.AddItem(extra.itemName, extra.count);
                            }
                        }
                    }
                }
                finally
                {
                    _inventory.SuppressNotificationEvents = prevSuppress;
                }
            }
        }

        private void InitializeDefaultWeapons()
        {
            if (weapons == null)
            {
                weapons = new System.Collections.Generic.List<Weapon>();
            }

            if (weapons.Count == 0)
            {
                // 1. 9mm Pistol
                Weapon pistol = new Weapon
                {
                    weaponName = "Pistol",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = fireRate > 0f ? fireRate : 0.4f,
                    magazineSize = magazineSize > 0 ? magazineSize : 12,
                    reloadDuration = reloadDuration > 0f ? reloadDuration : 1.0f,
                    damage = 15f,
                    range = 1.5f,
                    canPierce = false,
                    spreadAngle = 0f,
                    pelletsPerShot = 1,
                    isAutomatic = false,
                    ammoType = "Light Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                pistol.Initialize(startingReserveAmmo);
                weapons.Add(pistol);

                // 2. Assault Rifle
                Weapon rifle = new Weapon
                {
                    weaponName = "Rifle",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 0.12f,
                    magazineSize = 30,
                    reloadDuration = 1.5f,
                    damage = 25f,
                    range = 3f,
                    canPierce = false,
                    spreadAngle = 5f,
                    pelletsPerShot = 1,
                    isAutomatic = true,
                    ammoType = "Medium Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                rifle.Initialize(120);
                weapons.Add(rifle);

                // 3. Pump Shotgun
                Weapon shotgun = new Weapon
                {
                    weaponName = "Shotgun",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 0.7f,
                    magazineSize = 6,
                    reloadDuration = 2.0f,
                    damage = 12f,
                    range = 0.8f,
                    canPierce = true,
                    spreadAngle = 15f,
                    pelletsPerShot = 5,
                    isAutomatic = false,
                    ammoType = "Shotgun Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                shotgun.Initialize(24);
                weapons.Add(shotgun);

                // 4. SMG (Submachine Gun)
                Weapon smg = new Weapon
                {
                    weaponName = "SMG",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 0.08f,
                    magazineSize = 35,
                    reloadDuration = 1.2f,
                    damage = 16f,
                    range = 2.0f,
                    canPierce = false,
                    spreadAngle = 8f,
                    pelletsPerShot = 1,
                    isAutomatic = true,
                    ammoType = "Light Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                smg.Initialize(70);
                weapons.Add(smg);

                // 5. Hunting Sniper Rifle
                Weapon sniper = new Weapon
                {
                    weaponName = "Sniper Rifle",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 1.2f,
                    magazineSize = 5,
                    reloadDuration = 2.2f,
                    damage = 110f,
                    range = 6.0f,
                    canPierce = false,
                    spreadAngle = 0f,
                    pelletsPerShot = 1,
                    isAutomatic = false,
                    ammoType = "Medium Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                sniper.Initialize(15);
                weapons.Add(sniper);

                // 6. Heavy Revolver
                Weapon revolver = new Weapon
                {
                    weaponName = "Revolver",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 0.65f,
                    magazineSize = 6,
                    reloadDuration = 1.8f,
                    damage = 55f,
                    range = 2.5f,
                    canPierce = false,
                    spreadAngle = 2f,
                    pelletsPerShot = 1,
                    isAutomatic = false,
                    ammoType = "Light Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                revolver.Initialize(24);
                weapons.Add(revolver);

                // 7. Flamethrower
                Weapon flamethrower = new Weapon
                {
                    weaponName = "Flamethrower",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 0.1f,
                    magazineSize = 50,
                    reloadDuration = 2.5f,
                    damage = 10f,
                    range = 1.8f,
                    canPierce = true,
                    spreadAngle = 22f,
                    pelletsPerShot = 3,
                    isAutomatic = true,
                    ammoType = "Gas Canister",
                    vfxPoolKey = "PistolFlash"
                };
                flamethrower.Initialize(100);
                weapons.Add(flamethrower);

                // 8. Rocket Launcher
                Weapon rocketLauncher = new Weapon
                {
                    weaponName = "Rocket Launcher",
                    projectilePrefab = projectilePrefab,
                    projectileVisualId = "Bullet",
                    fireRate = 1.5f,
                    magazineSize = 1,
                    reloadDuration = 2.5f,
                    damage = 150f,
                    range = 4.0f,
                    canPierce = false,
                    spreadAngle = 0f,
                    pelletsPerShot = 1,
                    isAutomatic = false,
                    ammoType = "Heavy Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                rocketLauncher.Initialize(5);
                weapons.Add(rocketLauncher);

                // 9. Hunting Crossbow
                Projectile boltProjPrefab = projectilePrefab;
                GameObject boltObj = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/BoltPrefabs");
                if (boltObj != null)
                {
                    Projectile loadedBolt = boltObj.GetComponent<Projectile>();
                    if (loadedBolt != null) boltProjPrefab = loadedBolt;
                }

                Weapon crossbow = new Weapon
                {
                    weaponName = "Crossbow",
                    projectilePrefab = boltProjPrefab,
                    projectileVisualId = "Arrow",
                    fireRate = 1.0f,
                    magazineSize = 1,
                    reloadDuration = 1.2f,
                    damage = 70f,
                    range = 3.5f,
                    canPierce = false,
                    spreadAngle = 0f,
                    pelletsPerShot = 1,
                    isAutomatic = false,
                    ammoType = "Bolts",
                    vfxPoolKey = ""
                };
                crossbow.Initialize(10);
                weapons.Add(crossbow);

                // 10. Grenade (Throwing Weapon)
                Projectile grenadeProjPrefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/ThrowingWeaponPrefabs")?.GetComponent<Projectile>();
                if (grenadeProjPrefab == null)
                {
                    grenadeProjPrefab = projectilePrefab;
                }

                Weapon grenade = new Weapon
                {
                    weaponName = "Grenade",
                    projectilePrefab = grenadeProjPrefab,
                    projectileVisualId = "Grenade",
                    fireRate = 0.6f,
                    magazineSize = 1,
                    reloadDuration = 0.4f,
                    damage = 200f,
                    range = 15f,
                    canPierce = false,
                    spreadAngle = 0f,
                    pelletsPerShot = 1,
                    isAutomatic = false,
                    ammoType = "Grenade",
                    vfxPoolKey = "Explosion"
                };
                grenade.Initialize(0); // กระสุนสำรองดึงตามจำนวนใน Inventory
                weapons.Add(grenade);
            }
            else
            {
                // Initialize custom inspector configured weapons
                foreach (var w in weapons)
                {
                    w.Initialize(w.currentReserveAmmo > 0 ? w.currentReserveAmmo : startingReserveAmmo);
                }
            }
        }

        private void InitializeDefaultMeleeWeapons()
        {
            if (meleeWeapons == null)
            {
                meleeWeapons = new System.Collections.Generic.List<MeleeWeapon>();
            }

            if (meleeWeapons.Count == 0)
            {
                // 1. Knife
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Knife",
                    damage = 25f,
                    attackRate = 0.25f,
                    attackRadius = 1.0f,
                    knockbackForce = 5f,
                    staggerDuration = 0.2f,
                    vfxPoolKey = "SwordSlash"
                });

                // 2. Baseball Bat
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Baseball Bat",
                    damage = 40f,
                    attackRate = 0.5f,
                    attackRadius = 1.4f,
                    knockbackForce = 15f,
                    staggerDuration = 0.5f,
                    vfxPoolKey = "SwordSlash"
                });

                // 3. Machete
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Machete",
                    damage = 55f,
                    attackRate = 0.6f,
                    attackRadius = 1.3f,
                    knockbackForce = 10f,
                    staggerDuration = 0.3f,
                    vfxPoolKey = "SwordSlash"
                });

                // 4. Fire Axe
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Fire Axe",
                    damage = 65f,
                    attackRate = 0.7f,
                    attackRadius = 1.35f,
                    knockbackForce = 12f,
                    staggerDuration = 0.4f,
                    vfxPoolKey = "SwordSlash"
                });

                // 5. Katana
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Katana",
                    damage = 50f,
                    attackRate = 0.4f,
                    attackRadius = 1.6f,
                    knockbackForce = 8f,
                    staggerDuration = 0.3f,
                    vfxPoolKey = "SwordSlash"
                });

                // 6. Sledgehammer
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Sledgehammer",
                    damage = 90f,
                    attackRate = 0.95f,
                    attackRadius = 1.4f,
                    knockbackForce = 20f,
                    staggerDuration = 0.8f,
                    vfxPoolKey = "SwordSlash"
                });

                // 7. Survival Spear
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Survival Spear",
                    damage = 45f,
                    attackRate = 0.45f,
                    attackRadius = 2.2f,
                    knockbackForce = 10f,
                    staggerDuration = 0.3f,
                    vfxPoolKey = "SwordSlash"
                });

                // 8. Stun Baton
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Stun Baton",
                    damage = 35f,
                    attackRate = 0.45f,
                    attackRadius = 1.2f,
                    knockbackForce = 6f,
                    staggerDuration = 0.6f,
                    vfxPoolKey = "SwordSlash"
                });

                // 9. Chainsaw
                meleeWeapons.Add(new MeleeWeapon
                {
                    weaponName = "Chainsaw",
                    damage = 20f,
                    attackRate = 0.1f,
                    attackRadius = 1.2f,
                    knockbackForce = 3f,
                    staggerDuration = 0.1f,
                    vfxPoolKey = "SwordSlash"
                });
            }
        }

        public bool IsWeaponNameMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string cleanA = a.Trim().ToLower();
            string cleanB = b.Trim().ToLower();
            if (cleanA == cleanB) return true;
            if (cleanA.Replace(" ", "") == cleanB.Replace(" ", "")) return true;
            if (cleanA.Contains(cleanB) || cleanB.Contains(cleanA)) return true;
            return false;
        }

        public bool HasRangedWeaponAccess(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return false;

            // Check if player has it in inventory bag (or has stack count > 0, e.g. Grenade)
            if (_inventory != null)
            {
                if (_inventory.GetItemCount(weaponName) > 0) return true;
                if (_inventory.Items != null && _inventory.Items.Exists(item => IsWeaponNameMatch(item, weaponName))) return true;
            }

            // Check if it's one of the active operative's starting weapons
            if (activeCharacterData != null && activeCharacterData.startingWeapons != null)
            {
                if (activeCharacterData.startingWeapons.Exists(sw => IsWeaponNameMatch(sw, weaponName))) return true;
            }

            return false;
        }

        public bool HasMeleeWeaponAccess(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return false;

            // Check if player has it in inventory bag
            if (_inventory != null)
            {
                if (_inventory.GetItemCount(weaponName) > 0) return true;
                if (_inventory.Items != null && _inventory.Items.Exists(item => IsWeaponNameMatch(item, weaponName))) return true;
            }

            // Check if it's one of the active operative's starting weapons
            if (activeCharacterData != null && activeCharacterData.startingWeapons != null)
            {
                if (activeCharacterData.startingWeapons.Exists(sw => IsWeaponNameMatch(sw, weaponName))) return true;
            }

            return false;
        }

        public void SwitchToMeleeWeapon(int index)
        {
            if (meleeWeapons == null || meleeWeapons.Count == 0) return;

            if (index < 0)
            {
                _currentMeleeWeaponIndex = -1;
                Debug.Log($"[PlayerController] Unequipped melee weapon.");
                if (meleeWeapon != null) meleeWeapon.SetActive(false);
                if (CurrentWeapon != null)
                {
                    _heldItemType = HeldItemType.Ranged;
                    ShowRangedWeaponVisual();
                }
                else
                {
                    _heldItemType = HeldItemType.None;
                }
                OnAmmoChanged?.Invoke();
                return;
            }

            if (index >= meleeWeapons.Count) return;
            
            string wName = meleeWeapons[index].weaponName;
            bool hasWeapon = HasMeleeWeaponAccess(wName);

            if (!hasWeapon)
            {
                Debug.LogWarning($"[PlayerController] Cannot switch to {wName}. You do not own this melee weapon!");
                return;
            }

            _currentMeleeWeaponIndex = index;
            _heldItemType = HeldItemType.Melee;
            _heldConsumableName = "";
            ShowMeleeWeaponVisual();

            Debug.Log($"[PlayerController] Equipped melee weapon: {wName}");
            if (meleeWeapon != null && meleeWeapon.activeSelf)
            {
                UpdateChildVisual(meleeWeapon, CurrentMeleeWeaponName);
            }
            OnAmmoChanged?.Invoke();
        }

        public void CycleMeleeWeapon(int direction)
        {
            if (meleeWeapons == null || meleeWeapons.Count == 0) return;

            int nextIndex = _currentMeleeWeaponIndex;
            for (int i = 0; i < meleeWeapons.Count; i++)
            {
                nextIndex = (nextIndex + direction + meleeWeapons.Count) % meleeWeapons.Count;
                string wName = meleeWeapons[nextIndex].weaponName;
                bool hasWeapon = HasMeleeWeaponAccess(wName);

                if (hasWeapon)
                {
                    SwitchToMeleeWeapon(nextIndex);
                    return;
                }
            }
        }

        public void UseQuickSlotOrWeapon(int slotIndex)
        {
            if (QuickSlot != null && QuickSlot.HasSlot(slotIndex))
            {
                QuickSlot.SelectSlot(slotIndex);
            }
            else
            {
                SwitchToQuickSlot(slotIndex);
            }
        }

        public void SwitchToQuickSlot(int slotIndex)
        {
            if (weapons == null || weapons.Count == 0) return;
            if (_isReloading) return;

            // 1. Collect all ranged weapons that the player currently owns / has access to
            List<int> ownedWeaponIndices = new List<int>();
            for (int i = 0; i < weapons.Count; i++)
            {
                if (HasRangedWeaponAccess(weapons[i].weaponName))
                {
                    ownedWeaponIndices.Add(i);
                }
            }

            // 2. If slotIndex maps to an owned weapon (e.g. 1st owned gun for Key 1, 2nd for Key 2)
            if (slotIndex >= 0 && slotIndex < ownedWeaponIndices.Count)
            {
                SwitchToWeapon(ownedWeaponIndices[slotIndex]);
                return;
            }

            // 3. Fallback: if player pressed a number matching a catalog slot they actually own
            if (slotIndex >= 0 && slotIndex < weapons.Count && HasRangedWeaponAccess(weapons[slotIndex].weaponName))
            {
                SwitchToWeapon(slotIndex);
                return;
            }

            Debug.Log($"[PlayerController] QuickSlot {slotIndex + 1} is empty or weapon not owned.");
        }

        public void SwitchToWeapon(int index)
        {
            if (weapons == null || weapons.Count == 0) return;
            if (_isReloading) return; // Block switching while reloading

            if (index < 0)
            {
                _currentWeaponIndex = -1;
                Debug.Log($"[PlayerController] Unequipped weapon.");
                if (rangeWeapon != null) rangeWeapon.SetActive(false);
                if (CurrentMeleeWeapon != null)
                {
                    _heldItemType = HeldItemType.Melee;
                    ShowMeleeWeaponVisual();
                }
                else
                {
                    _heldItemType = HeldItemType.None;
                }
                OnAmmoChanged?.Invoke();
                return;
            }

            if (index >= weapons.Count) return;
            
            string wName = weapons[index].weaponName;
            bool hasWeapon = HasRangedWeaponAccess(wName);

            if (!hasWeapon)
            {
                Debug.LogWarning($"[PlayerController] Cannot switch to {wName}. You do not own this weapon!");
                return;
            }

            _currentWeaponIndex = index;
            _heldItemType = HeldItemType.Ranged;
            _heldConsumableName = "";
            ShowRangedWeaponVisual();
            Debug.Log($"[PlayerController] Switched to weapon: {CurrentWeapon.weaponName}");
            if (rangeWeapon != null && rangeWeapon.activeSelf)
            {
                UpdateChildVisual(rangeWeapon, CurrentWeaponName);
            }
            
            // ถ้ากระสุนในแม็กกาซีนเป็น 0 (เช่น เพิ่งสลับมาระเบิด) ให้ดึงจากกระสุนสำรองมาบรรจุทันทีถ้ามี
            if (weapons[index].currentMagazine <= 0)
            {
                int reserve = GetAmmoReserveCount(weapons[index].ammoType);
                if (reserve > 0)
                {
                    int toLoad = Mathf.Min(weapons[index].magazineSize, reserve);
                    weapons[index].currentMagazine = toLoad;
                    ConsumeAmmo(weapons[index].ammoType, toLoad);
                }
            }

            // Short equip delay
            _fireCooldownTimer = 0.15f; 
            
            OnAmmoChanged?.Invoke();
        }

        private void CycleWeapon(int direction)
        {
            if (weapons == null || weapons.Count == 0) return;
            if (_isReloading) return;

            int nextIndex = _currentWeaponIndex;
            for (int i = 0; i < weapons.Count; i++)
            {
                nextIndex = (nextIndex + direction + weapons.Count) % weapons.Count;
                string wName = weapons[nextIndex].weaponName;
                bool hasWeapon = HasRangedWeaponAccess(wName);

                if (hasWeapon)
                {
                    SwitchToWeapon(nextIndex);
                    return;
                }
            }
        }

        #region Weapon Visual Management (Malee / Range)
        public void ShowMeleeWeaponVisual()
        {
            EnsureWeaponVisualReferences();
            if (meleeWeapon != null)
            {
                meleeWeapon.SetActive(true);
                UpdateChildVisual(meleeWeapon, CurrentMeleeWeaponName);
            }
            if (rangeWeapon != null)
            {
                rangeWeapon.SetActive(false);
            }
        }

        public void ShowRangedWeaponVisual()
        {
            EnsureWeaponVisualReferences();
            if (rangeWeapon != null)
            {
                rangeWeapon.SetActive(true);
                UpdateChildVisual(rangeWeapon, CurrentWeaponName);
            }
            if (meleeWeapon != null)
            {
                meleeWeapon.SetActive(false);
            }
        }

        private void EnsureWeaponVisualReferences()
        {
            if (meleeWeapon == null)
            {
                Transform t = FindDeepChild(transform, "Malee") ?? FindDeepChild(transform, "Melee") ?? FindDeepChild(transform, "MeleeWeapon");
                if (t != null) meleeWeapon = t.gameObject;
            }
            if (rangeWeapon == null)
            {
                Transform t = FindDeepChild(transform, "Range") ?? FindDeepChild(transform, "Ranged") ?? FindDeepChild(transform, "RangeWeapon");
                if (t != null) rangeWeapon = t.gameObject;
            }
        }

        private Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            foreach (Transform child in parent)
            {
                if (child.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void UpdateChildVisual(GameObject parentObj, string targetWeaponName)
        {
            if (parentObj == null) return;
            int childCount = parentObj.transform.childCount;
            if (childCount == 0) return;

            string cleanTarget = (targetWeaponName ?? "").Replace(" ", "").Replace("_", "").ToLowerInvariant();
            int bestIndex = -1;

            if (!string.IsNullOrEmpty(cleanTarget))
            {
                // Pass 1: Exact match first (prevents "Sniper Rifle" from falsely matching "Weapon_Rifle"!)
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = parentObj.transform.GetChild(i);
                    string childClean = child.name.Replace("Weapon_", "").Replace("Weapon", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
                    if (childClean == cleanTarget)
                    {
                        bestIndex = i;
                        break;
                    }
                }

                // Pass 2: Longest substring match fallback (only if no exact match exists)
                if (bestIndex < 0)
                {
                    int bestLen = -1;
                    for (int i = 0; i < childCount; i++)
                    {
                        Transform child = parentObj.transform.GetChild(i);
                        string childClean = child.name.Replace("Weapon_", "").Replace("Weapon", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
                        if (!string.IsNullOrEmpty(childClean) &&
                            (childClean.Contains(cleanTarget) || cleanTarget.Contains(childClean)))
                        {
                            if (childClean.Length > bestLen)
                            {
                                bestLen = childClean.Length;
                                bestIndex = i;
                            }
                        }
                    }
                }
            }

            // Apply visibility cleanly
            if (bestIndex >= 0)
            {
                for (int i = 0; i < childCount; i++)
                {
                    parentObj.transform.GetChild(i).gameObject.SetActive(i == bestIndex);
                }
                return;
            }

            // Fallback: If no child matched and none are currently active, activate the first child
            bool anyMatched = false;
            if (!anyMatched && childCount > 0)
            {
                bool hasAnyActive = false;
                for (int i = 0; i < childCount; i++)
                {
                    if (parentObj.transform.GetChild(i).gameObject.activeSelf)
                    {
                        hasAnyActive = true;
                        break;
                    }
                }
                if (!hasAnyActive)
                {
                    parentObj.transform.GetChild(0).gameObject.SetActive(true);
                }
            }
        }
        #endregion

        private void AlertEnemiesToGunshot()
        {
            if (CurrentWeapon != null && !string.IsNullOrEmpty(CurrentWeapon.weaponName))
            {
                if (CurrentWeapon.weaponName.IndexOf("Crossbow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return; // Crossbow is completely silent and does not alert enemies!
                }
            }
            BaseEnemyAI[] enemies = Object.FindObjectsByType<BaseEnemyAI>(FindObjectsSortMode.None);
            foreach (BaseEnemyAI enemy in enemies)
            {
                if (enemy == null) continue;
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist <= gunshotAlertRange)
                {
                    enemy.AlertToPlayer();
                }
            }
        }

        public void AddReserveAmmo(int amount)
        {
            if (CurrentWeapon != null)
            {
                CurrentWeapon.currentReserveAmmo += amount;
            }
            else
            {
                _currentReserveAmmo += amount;
            }
            OnAmmoChanged?.Invoke();
            Debug.Log($"[PlayerController] Added {amount} reserve ammo. Total: {(CurrentWeapon != null ? CurrentWeapon.currentReserveAmmo : _currentReserveAmmo)}");
        }

        private void UpdateInteractionPrompt()
        {
            bool isMenuOpen = PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps;
            if (isMenuOpen)
            {
                if (LootUI.Instance != null) LootUI.Instance.HidePrompt();
                _bestInteractableType = TargetInteractableType.None;
                _closestBuilding = null;
                _closestContainer = null;
                _closestNpc = null;
                return;
            }

            // 1. Scan Loot Containers
            LootContainer[] containers = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
            LootContainer closestContainer = null;
            float minContainerDist = float.MaxValue;
            foreach (LootContainer container in containers)
            {
                if (container == null) continue;
                float dist = container.GetDistanceToPoint(transform.position);
                float radius = container.interactionRadius > 0f ? container.interactionRadius : 2.5f;
                if (dist <= radius && dist < minContainerDist)
                {
                    minContainerDist = dist;
                    closestContainer = container;
                }
            }

            // 2. Scan NPC Controllers
            NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            NPCController closestNpc = null;
            float minNpcDist = float.MaxValue;
            foreach (NPCController npc in npcs)
            {
                if (npc != null && npc.CanInteract && npc.IsPlayerInRange(transform.position))
                {
                    float dist = Vector3.Distance(transform.position, npc.transform.position);
                    if (dist < minNpcDist)
                    {
                        minNpcDist = dist;
                        closestNpc = npc;
                    }
                }
            }

            // 3. Scan Building Interactables
            IBuildingInteractable closestBuilding = null;
            float minBuildingDist = float.MaxValue;
            for (int i = 0; i < BuildingInteractionRegistry.ActiveInteractables.Count; i++)
            {
                var building = BuildingInteractionRegistry.ActiveInteractables[i];
                if (building == null || !building.CanInteract || building.InteractionTransform == null) continue;

                float dist = Vector3.Distance(transform.position, building.InteractionTransform.position);
                if (dist <= building.InteractionDistance && dist < minBuildingDist)
                {
                    minBuildingDist = dist;
                    closestBuilding = building;
                }
            }

            // 4. Find the SINGLE CLOSEST target across all types!
            float minOverallDist = float.MaxValue;
            _bestInteractableType = TargetInteractableType.None;
            _closestBuilding = null;
            _closestContainer = null;
            _closestNpc = null;

            if (closestBuilding != null && minBuildingDist < minOverallDist)
            {
                minOverallDist = minBuildingDist;
                _bestInteractableType = TargetInteractableType.Building;
                _closestBuilding = closestBuilding;
            }

            if (closestContainer != null && minContainerDist < minOverallDist)
            {
                minOverallDist = minContainerDist;
                _bestInteractableType = TargetInteractableType.Container;
                _closestContainer = closestContainer;
                _closestBuilding = null;
            }

            if (closestNpc != null && minNpcDist < minOverallDist)
            {
                minOverallDist = minNpcDist;
                _bestInteractableType = TargetInteractableType.NPC;
                _closestNpc = closestNpc;
                _closestBuilding = null;
                _closestContainer = null;
            }

            // 5. Display compact key-only hint via LootUI (only one prompt at any given time)
            if (LootUI.Instance == null) return;

            // Priority -1: Downed Co-op teammate nearby -> show "E" (Hold E to revive)
            if (!IsDowned && TheLastEmpire.Network.PlayerRegistry.GetTotalPlayerCount() > 1)
            {
                PlayerController downedAlly = FindNearestDownedTeammate(ReviveInteractRadius);
                if (downedAlly != null)
                {
                    LootUI.Instance.ShowKeyHint("E", downedAlly.transform, Vector3.up * 2.0f);
                    return;
                }
            }

            // Priority 0: TransitionPortal warp zone -> show "E"
            if (WorldMapManager.Instance != null)
            {
                TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
                for (int i = 0; i < portals.Length; i++)
                {
                    TransitionPortal portal = portals[i];
                    if (TransitionPortal.IsWorldPortal(portal) && portal.IsPlayerInRange(this))
                    {
                        LootUI.Instance.ShowKeyHint("E", transform, Vector3.up * 2.1f);
                        return;
                    }
                }
            }

            // Check if player can vault/jump over a vehicle ahead ("SPACE")
            bool canVault = CanVehicleVaultAhead(out Transform vaultVehicle);

            // Check if a manual-pickup dropped item is in range ("E")
            CollectibleItem closestManualItem = null;
            if (_bestInteractableType == TargetInteractableType.None)
            {
                CollectibleItem[] droppedItems = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
                float minDropDist = float.MaxValue;
                for (int i = 0; i < droppedItems.Length; i++)
                {
                    CollectibleItem item = droppedItems[i];
                    if (item == null || !item.RequireManualPickup || !item.IsPlayerInRange()) continue;
                    float d = Vector3.Distance(transform.position, item.transform.position);
                    if (d < minDropDist)
                    {
                        minDropDist = d;
                        closestManualItem = item;
                    }
                }
            }

            switch (_bestInteractableType)
            {
                case TargetInteractableType.Building:
                {
                    string keyLabel = LootUI.ExtractKeyOnlyLabel(_closestBuilding.GetInteractionPrompt());
                    if (canVault && !keyLabel.Contains("SPACE")) keyLabel += ",SPACE";
                    LootUI.Instance.ShowKeyHint(keyLabel, _closestBuilding.InteractionTransform, _closestBuilding.PromptWorldOffset);
                    break;
                }

                case TargetInteractableType.Container:
                {
                    string keyLabel = canVault ? "E,SPACE" : "E";
                    LootUI.Instance.ShowKeyHint(keyLabel, _closestContainer.transform);
                    break;
                }

                case TargetInteractableType.NPC:
                {
                    string keyLabel;
                    if (_closestNpc.HasShop && _closestNpc.HasDialogue) keyLabel = "E,Q";
                    else if (_closestNpc.HasShop) keyLabel = "E";
                    else if (_closestNpc.HasDialogue) keyLabel = "Q";
                    else keyLabel = "E";
                    LootUI.Instance.ShowKeyHint(keyLabel, _closestNpc.transform);
                    break;
                }

                default:
                    if (closestManualItem != null)
                    {
                        string keyLabel = canVault ? "E,SPACE" : "E";
                        LootUI.Instance.ShowKeyHint(keyLabel, closestManualItem.transform, Vector3.up * 1.1f);
                    }
                    else if (canVault)
                    {
                        LootUI.Instance.ShowKeyHint("SPACE", transform, Vector3.up * 2.1f);
                    }
                    else
                    {
                        LootUI.Instance.HidePrompt();
                    }
                    break;
            }
        }

        private void TryTalk()
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;
            if (BuildingPlacer.Instance != null && BuildingPlacer.Instance.ShouldBlockCombat)
            {
                _fireBufferTimer = 0f;
                return;
            }

            NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            foreach (NPCController npc in npcs)
            {
                if (npc != null && npc.CanInteract && npc.IsPlayerInRange(transform.position))
                {
                    if (npc.HasDialogue)
                    {
                        npc.Talk();
                        return; // Talked with NPC, skip other interactions!
                    }
                }
            }
        }

        private void InitIndicators()
        {
            _throwRangeIndicator = CreateLineRenderer("ThrowRangeIndicator", Color.white, 0.05f);
            _aoeIndicator = CreateLineRenderer("AOEIndicator", Color.red, 0.05f);

            // Sprite Indicators (flat on ground, facing top-down camera) — defaults to UI_Hitbox_Sprites
            Sprite resolvedRangeSprite = throwRangeSprite != null ? throwRangeSprite : HitboxSpriteLibrary.DashedCrosshair;
            Sprite resolvedAoeSprite = aoeIndicatorSprite != null ? aoeIndicatorSprite : HitboxSpriteLibrary.HazardCircle;

            GameObject throwRangeObj = new GameObject("ThrowRangeSprite");
            throwRangeObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _throwRangeSpriteRenderer = throwRangeObj.AddComponent<SpriteRenderer>();
            _throwRangeSpriteRenderer.sprite = resolvedRangeSprite;
            _throwRangeSpriteRenderer.color = HitboxSpriteLibrary.DefaultOriginalColor;
            _throwRangeSpriteRenderer.sortingOrder = 4;
            throwRangeObj.SetActive(false);

            GameObject aoeObj = new GameObject("AOESprite");
            aoeObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _aoeSpriteRenderer = aoeObj.AddComponent<SpriteRenderer>();
            _aoeSpriteRenderer.sprite = resolvedAoeSprite;
            _aoeSpriteRenderer.color = HitboxSpriteLibrary.DefaultOriginalColor;
            _aoeSpriteRenderer.sortingOrder = 6;
            aoeObj.SetActive(false);

            _laserSightIndicator = CreateLaserLineRenderer("LaserSightIndicator", new Color(1f, 0.08f, 0.06f, 0.95f), 0.05f);
            _laserTargetDot = CreateLineRenderer("LaserTargetDot", new Color(1f, 0.1f, 0.08f, 0.95f), 0.035f);

            _sniperReticleSprite = HitboxSpriteLibrary.CreateGroundSpriteIndicator(
                "SniperReticleSprite", null, HitboxSpriteLibrary.DashedCrosshair, HitboxSpriteLibrary.SniperRed, 8);
            _sniperReticleSprite.gameObject.SetActive(false);

            _sniperChevronSprite = HitboxSpriteLibrary.CreateGroundSpriteIndicator(
                "SniperChevronSprite", null, HitboxSpriteLibrary.DirectionalChevrons, HitboxSpriteLibrary.SniperRed, 7);
            _sniperChevronSprite.gameObject.SetActive(false);
        }

        private LineRenderer CreateLaserLineRenderer(string objName, Color color, float width)
        {
            GameObject obj = new GameObject(objName);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.startWidth = width;
            lr.endWidth = width * 0.65f;
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.positionCount = 2;

            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.45f);

            lr.gameObject.SetActive(false);
            return lr;
        }

        private LineRenderer CreateLineRenderer(string objName, Color color, float width)
        {
            GameObject obj = new GameObject(objName);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 36;
            
            // Basic material setup
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            
            lr.gameObject.SetActive(false);
            return lr;
        }

        private void DrawCircle(LineRenderer lr, Vector3 center, float radius)
        {
            if (lr == null) return;
            int segments = lr.positionCount;
            float angle = 0f;
            for (int i = 0; i < segments; i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
                float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
                lr.SetPosition(i, new Vector3(center.x + x, 0.1f, center.z + z)); // slightly above ground
                angle += (360f / segments);
            }
        }
    }

    [System.Serializable]
    public class Weapon
    {
        public string weaponName;
        [Tooltip("Weapon icon sprite (optional, falls back to ItemDatabase)")]
        public Sprite icon;
        public Projectile projectilePrefab;
        public float fireRate = 0.2f;
        public int magazineSize = 12;
        public float reloadDuration = 1.0f;
        public float damage = 20f;
        public float range = 3f; // Projectile lifetime (range)
        public bool canPierce = false; // Bullet penetration
        
        [Header("Spread & Pellets (Shotgun)")]
        public float spreadAngle = 0f;
        public int pelletsPerShot = 1;
        public bool isAutomatic = false;
        public string ammoType;

        [Header("Runtime State")]
        public int currentMagazine;
        public int currentReserveAmmo;

        [Header("Visuals")]
        [Tooltip("ชื่อ Pool Key สำหรับ Muzzle Flash (ตอนยิง)")]
        public string vfxPoolKey;

        [Tooltip("ID ของโมเดลกระสุน/ระเบิดสำหรับเปิดตาใน Projectile เช่น ITEM0008_Grenade, ITEM0009_Bullet, ITEM0010_Arrow")]
        public string projectileVisualId;

        public void Initialize(int startingReserve)
        {
            string lowerName = (weaponName ?? "").ToLower().Trim();
            bool isRifle = lowerName.Contains("rifl");
            bool isShotgun = lowerName.Contains("shot");
            bool isPistol = lowerName.Contains("pist") || (!isRifle && !isShotgun);

            if (string.IsNullOrEmpty(ammoType))
            {
                if (isRifle) ammoType = "Rifle Ammo";
                else if (isShotgun) ammoType = "Shotgun Ammo";
                else ammoType = "Pistol Ammo";
            }

            // Apply default fallbacks if stats are unassigned (0) in Inspector
            if (fireRate <= 0f) fireRate = isRifle ? 0.12f : (isShotgun ? 0.7f : 0.4f);
            if (magazineSize <= 0) magazineSize = isRifle ? 30 : (isShotgun ? 6 : 12);
            if (reloadDuration <= 0f) reloadDuration = isRifle ? 1.5f : (isShotgun ? 2.0f : 1.0f);
            if (damage <= 0f) damage = isRifle ? 25f : (isShotgun ? 12f : 15f);
            if (range <= 0f) range = isRifle ? 3.0f : (isShotgun ? 0.8f : 1.5f);
            if (pelletsPerShot <= 0) pelletsPerShot = isShotgun ? 5 : 1;

            currentMagazine = magazineSize;
            currentReserveAmmo = startingReserve;
        }

        public Weapon() {}

        public Weapon(ItemData data)
        {
            if (data == null) return;
            weaponName = data.itemName;
            damage = data.damage;
            icon = data.icon;
            
            if (data.weaponConfig != null)
            {
                projectilePrefab = data.weaponConfig.projectilePrefab;
                fireRate = data.weaponConfig.fireRate;
                magazineSize = data.weaponConfig.magazineSize;
                reloadDuration = data.weaponConfig.reloadDuration;
                range = data.weaponConfig.range;
                canPierce = data.weaponConfig.canPierce;
                spreadAngle = data.weaponConfig.spreadAngle;
                pelletsPerShot = data.weaponConfig.pelletsPerShot;
                isAutomatic = data.weaponConfig.isAutomatic;
                ammoType = data.weaponConfig.ammoType;
                vfxPoolKey = data.weaponConfig.vfxPoolKey;
                projectileVisualId = data.weaponConfig.projectileVisualId;
                if (string.IsNullOrEmpty(vfxPoolKey) || vfxPoolKey.Equals("PistalFlash", System.StringComparison.OrdinalIgnoreCase))
                {
                    vfxPoolKey = "PistolFlash";
                }
            }
            
            Initialize(0);
        }
    }

    [System.Serializable]
    public class MeleeWeapon
    {
        public string weaponName;
        [Tooltip("Melee icon sprite (optional, falls back to ItemDatabase)")]
        public Sprite icon;
        public float damage = 35f;
        public float attackRate = 0.4f;
        public float attackRadius = 1.2f;
        public float knockbackForce = 10f;
        public float staggerDuration = 0.4f;

        [Header("Visuals")]
        [Tooltip("ชื่อ Pool Key สำหรับเอฟเฟกต์ตอนฟัน (เช่น MeleeSlash)")]
        public string vfxPoolKey;
        [Tooltip("ตัวคูณขนาดของ VFX")]
        public float vfxScaleMultiplier = 1.0f;

        public MeleeWeapon() {}

        public MeleeWeapon(ItemData data)
        {
            if (data == null) return;
            weaponName = data.itemName;
            damage = data.damage;
            icon = data.icon;
            attackRate = data.attackRate;
            attackRadius = data.attackRadius;
            knockbackForce = data.knockbackForce;
            staggerDuration = data.staggerDuration;
            
            // Default VFX if not set
            vfxPoolKey = "MeleeSlash";
            vfxScaleMultiplier = 1.0f;
        }
    }
}
