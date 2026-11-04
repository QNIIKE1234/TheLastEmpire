using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PlayerConfigSO playerConfig;

        [Header("Character Selection Integration")]
        [SerializeField] private CharacterData activeCharacterData;
        private IShiftAbility _activeShiftAbility;

        public CharacterData ActiveCharacterData => activeCharacterData;
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

        public event System.Action OnAmmoChanged;

        [Header("Hunger System")]
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float currentHunger = 100f;

        private float _hungerDamageTimer = 0f;

        public float MaxHunger => maxHunger;
        public float CurrentHunger => currentHunger;
        public event System.Action OnHungerChanged;

        private bool _isSprinting = false;
        public bool IsSprinting => _isSprinting;

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
                startingReserveAmmo = activeCharacterData.startingPistolAmmo;

                _health = GetComponent<Health>();
                if (_health != null)
                {
                    _health.SetMaxHealth(activeCharacterData.maxHealth, true);
                }

                InitShiftAbility();

                if (activeCharacterData.characterModelPrefab != null)
                {
                    Transform existingCapsule = transform.Find("Capsule");
                    if (existingCapsule != null) existingCapsule.gameObject.SetActive(false);
                    Transform existingModel = transform.Find("CharacterModelInstance");
                    if (existingModel == null)
                    {
                        GameObject modelInstance = Instantiate(activeCharacterData.characterModelPrefab, transform);
                        modelInstance.name = "CharacterModelInstance";
                        modelInstance.transform.localPosition = Vector3.zero;
                        modelInstance.transform.localRotation = Quaternion.identity;
                        _animator = modelInstance.GetComponentInChildren<Animator>();
                    }
                }
            }
            else if (playerConfig != null)
            {
                moveSpeed = playerConfig.moveSpeed;
                dashSpeed = playerConfig.dashSpeed;
                dashDuration = playerConfig.dashDuration;
                dashCooldown = playerConfig.dashCooldown;
                maxHunger = playerConfig.maxHunger;

                _health = GetComponent<Health>();
                if (_health != null)
                {
                    _health.SetMaxHealth(playerConfig.maxHealth, true);
                }
            }
        }

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
            currentHunger = Mathf.Max(0f, currentHunger - amount);
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

            _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
            InitMuzzleFlashLight();
            {
            }

            // Automatically attach CameraFollow and CameraObstacleFader components to main camera if not present
            Camera mainCam = Camera.main;
            if (mainCam != null)
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

            // Bind Inventory component or dynamically attach it
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null)
            {
                _inventory = gameObject.AddComponent<PlayerInventory>();
            }

            // Automatically setup PlayerInput component
            PlayerInput playerInput = GetComponent<PlayerInput>();
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

            _currentReserveAmmo = startingReserveAmmo;
            _currentMagazine = magazineSize;
            InitializeDefaultWeapons();
            InitializeDefaultMeleeWeapons();
            if (activeCharacterData != null)
            {
                ApplyStartingCharacterLoadout();
            }
            OnAmmoChanged?.Invoke();

            // SetupScreenBoundaries(); // Disabled: Using manual scene boundaries/walls instead
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.onDeath.RemoveListener(HandlePlayerDeath);
            }
        }

        private void HandlePlayerDeath()
        {
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
        }

        private void Update()
        {
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

            if (Keyboard.current != null)
            {
                if (!isShootingNow && Keyboard.current.shiftKey.wasPressedThisFrame)
                {
                    _activeShiftAbility?.OnShiftPressed();
                }
                if (!isShootingNow && Keyboard.current.shiftKey.isPressed)
                {
                    _activeShiftAbility?.OnShiftHeld();
                }
                if (Keyboard.current.shiftKey.wasReleasedThisFrame)
                {
                    _activeShiftAbility?.OnShiftReleased();
                }
            }
            _activeShiftAbility?.UpdateAbility();

            bool isAimingSlow = _activeShiftAbility is EagleEyeAbility && _activeShiftAbility.IsActive;
            _isSprinting = !isShootingNow && !isAimingSlow && Keyboard.current != null && Keyboard.current.shiftKey.isPressed && _moveInput.sqrMagnitude > 0.01f;
            UpdateHunger();
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
                    TryStartReload();
                }
            }

            bool isMenuOpen = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || 
                              (LootUI.Instance != null && LootUI.Instance.IsOpen) ||
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
            _animator.SetFloat("Speed", moveMagnitude);
            _animator.SetBool("IsRunning", _isSprinting && moveMagnitude > 0.1f);
            _animator.SetBool("HasGun", CurrentWeapon != null);
            if (_shootAnimLayerTimer > 0f) _shootAnimLayerTimer -= Time.deltaTime;
            if (_meleeAnimTimer > 0f) _meleeAnimTimer -= Time.deltaTime;
            if (_animator.layerCount > 1)
            {
                bool showUpperShoot = _shootAnimLayerTimer > 0f && _meleeAnimTimer <= 0f && !_isDashing;
                _animator.SetLayerWeight(1, showUpperShoot ? 1f : 0f);
            }
            _animator.SetBool("IsDashing", _isDashing);
        }

        private void UpdateIndicators()
        {
            bool isThrowing = CurrentWeapon != null && CurrentWeapon.projectilePrefab != null && CurrentWeapon.projectilePrefab.Trajectory == TrajectoryMode.Arced;

            if (isThrowing)
            {
                float throwRadius = CurrentWeapon.range;
                float aoeRadius = CurrentWeapon.projectilePrefab.ExplosionRadius;

                // 1. Throw Range Indicator (Around Player)
                if (throwRangeSprite != null && _throwRangeSpriteRenderer != null)
                {
                    if (!_throwRangeSpriteRenderer.gameObject.activeSelf) _throwRangeSpriteRenderer.gameObject.SetActive(true);
                    if (_throwRangeIndicator != null && _throwRangeIndicator.gameObject.activeSelf) _throwRangeIndicator.gameObject.SetActive(false);

                    _throwRangeSpriteRenderer.transform.position = new Vector3(transform.position.x, 0.04f, transform.position.z);
                    float baseWidth = _throwRangeSpriteRenderer.sprite != null ? _throwRangeSpriteRenderer.sprite.bounds.size.x : 1f;
                    if (baseWidth > 0f)
                    {
                        float scale = (throwRadius * 2f) / baseWidth;
                        _throwRangeSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                    }
                }
                else if (_throwRangeIndicator != null)
                {
                    if (!_throwRangeIndicator.gameObject.activeSelf) _throwRangeIndicator.gameObject.SetActive(true);
                    if (_throwRangeSpriteRenderer != null && _throwRangeSpriteRenderer.gameObject.activeSelf) _throwRangeSpriteRenderer.gameObject.SetActive(false);
                    DrawCircle(_throwRangeIndicator, transform.position, throwRadius);
                }

                // 2. AOE Radius Indicator (At Aim Target Point)
                if (aoeIndicatorSprite != null && _aoeSpriteRenderer != null)
                {
                    if (!_aoeSpriteRenderer.gameObject.activeSelf) _aoeSpriteRenderer.gameObject.SetActive(true);
                    if (_aoeIndicator != null && _aoeIndicator.gameObject.activeSelf) _aoeIndicator.gameObject.SetActive(false);

                    _aoeSpriteRenderer.transform.position = new Vector3(_aimTargetPoint.x, 0.05f, _aimTargetPoint.z);
                    float baseWidth = _aoeSpriteRenderer.sprite != null ? _aoeSpriteRenderer.sprite.bounds.size.x : 1f;
                    if (baseWidth > 0f)
                    {
                        float scale = (aoeRadius * 2f) / baseWidth;
                        _aoeSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                    }
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

            // 3. Eagle Eye Sniper Laser Sight Indicator
            bool isEagleEyeAiming = _activeShiftAbility is EagleEyeAbility eagle && eagle.IsActive;
            if (isEagleEyeAiming)
            {
                if (_laserSightIndicator == null)
                {
                    _laserSightIndicator = CreateLaserLineRenderer("LaserSightIndicator", new Color(1f, 0.15f, 0.15f, 0.95f), 0.045f);
                }
                if (_laserTargetDot == null)
                {
                    _laserTargetDot = CreateLineRenderer("LaserTargetDot", new Color(1f, 0.2f, 0.2f, 0.95f), 0.035f);
                }

                if (!_laserSightIndicator.gameObject.activeSelf) _laserSightIndicator.gameObject.SetActive(true);
                if (!_laserTargetDot.gameObject.activeSelf) _laserTargetDot.gameObject.SetActive(true);

                float targetRange = 25f;
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
                float rangeMult = (activeCharacterData != null) ? activeCharacterData.eagleEyeRangeMultiplier : 2.0f;
                targetRange *= rangeMult;
                targetRange = Mathf.Clamp(targetRange, 12f, 45f);

                Vector3 startPos = transform.position + Vector3.up * 0.15f + _aimDirection * 0.4f;
                Vector3 endPos = startPos + _aimDirection * targetRange;

                // Check obstacle collisions
                int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
                if (Physics.Raycast(startPos + Vector3.up * 0.35f, _aimDirection, out RaycastHit hit, targetRange, layerMask))
                {
                    endPos = new Vector3(hit.point.x, startPos.y, hit.point.z);
                }

                _laserSightIndicator.SetPosition(0, startPos);
                _laserSightIndicator.SetPosition(1, endPos);
                DrawCircle(_laserTargetDot, endPos, 0.22f);
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

            if (_isDashing)
            {
                _rb.linearVelocity = _dashDirection * dashSpeed;
            }
            else if (_activeShiftAbility != null && _activeShiftAbility.IsActive && (_activeShiftAbility is GrapplingHookAbility gha && gha.IsPulling || _activeShiftAbility is UnstoppableChargeAbility))
            {
                // Active ability handles linearVelocity override directly (Grappling Hook zip or Juggernaut Charge)
                return;
            }
            else
            {
                float baseSpeed = moveSpeed;
                float currentSpeed = baseSpeed;
                bool isShiftHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed && _moveInput.sqrMagnitude > 0.01f;

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

        // Eagle Eye Laser Sight Aim Indicators
        private LineRenderer _laserSightIndicator;
        private LineRenderer _laserTargetDot;

        private void UpdateAimDirection()
        {
            if (Camera.main == null) return;
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;

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
                    // Rotate the player to face aiming direction
                    transform.forward = _aimDirection;
                }
            }
        }

        private void HandleDirectInput()
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;
            if ((InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || (LootUI.Instance != null && LootUI.Instance.IsOpen)) return;

            // Direct input checks to ensure combat always works regardless of playerinput maps
            if (Mouse.current != null)
            {
                // Ignore attacks only if clicking over interactive UI elements (Buttons, Sliders, InputFields, Scrollbars)
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    {
                        position = Mouse.current.position.ReadValue()
                    };
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

                    bool hitInteractiveUI = false;
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
                    if (hitInteractiveUI) return;
                }

                // Buffer click input so rapid clicks or walking clicks near cooldown boundaries are never dropped
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    _fireBufferTimer = 0.18f; // 180ms responsive buffer window
                }

                bool isAuto = CurrentWeapon != null && CurrentWeapon.isAutomatic;
                bool wantToShoot = (isAuto && Mouse.current.leftButton.isPressed) || _fireBufferTimer > 0f;

                if (wantToShoot && _fireCooldownTimer <= 0f && _meleeAnimTimer <= 0f)
                {
                    _fireBufferTimer = 0f;
                    ShootWeapon();
                }
                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    _meleeBufferTimer = 0.22f;
                }
                if (_meleeBufferTimer > 0f && _meleeCooldownTimer <= 0f)
                {
                    _meleeBufferTimer = 0f;
                    MeleeAttack();
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

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchToQuickSlot(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchToQuickSlot(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchToQuickSlot(2);
                if (Keyboard.current.digit4Key.wasPressedThisFrame) SwitchToQuickSlot(3);
                if (Keyboard.current.digit5Key.wasPressedThisFrame) SwitchToQuickSlot(4);
                if (Keyboard.current.digit6Key.wasPressedThisFrame) SwitchToQuickSlot(5);
                if (Keyboard.current.digit7Key.wasPressedThisFrame) SwitchToQuickSlot(6);
                if (Keyboard.current.digit8Key.wasPressedThisFrame) SwitchToQuickSlot(7);
                if (Keyboard.current.digit9Key.wasPressedThisFrame) SwitchToQuickSlot(8);

                if (Keyboard.current.xKey.wasPressedThisFrame)
                {
                    CycleMeleeWeapon(1);
                }

                if (Keyboard.current.fKey.wasPressedThisFrame && _meleeCooldownTimer <= 0f)
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
            }
        }

        // Called by PlayerInput component via SendMessages
        private void OnMove(InputValue value)
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps)
            {
                _moveInput = Vector2.zero;
                return;
            }
            _moveInput = value.Get<Vector2>();
        }

        private void OnAttack(InputValue value)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;

            // Attack action maps to left click / shooting
            if (value.isPressed && _fireCooldownTimer <= 0f && !_isDashing)
            {
                ShootWeapon();
            }
        }

        private void OnInteract(InputValue value)
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;

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
            if (WorldMapManager.Instance == null) return;

            // 1. Check if standing in any TransitionPortal trigger zone
            TransitionPortal[] portals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            foreach (TransitionPortal portal in portals)
            {
                if (portal != null && portal.IsPlayerInRange())
                {
                    portal.TriggerTransition(this);
                    return; // successfully transitioned via portal, skip normal interactions!
                }
            }

            // 2. Check if standing near an NPCController
            NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            foreach (NPCController npc in npcs)
            {
                if (npc != null && npc.IsPlayerInRange(transform.position))
                {
                    npc.Interact();
                    return; // Interacted with NPC, skip other interactions!
                }
            }

            // 3. Check if standing near a LootContainer
            LootContainer[] containers = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
            LootContainer closestContainer = null;
            float minContainerDist = float.MaxValue;
            foreach (LootContainer container in containers)
            {
                if (container == null) continue;
                float dist = Vector3.Distance(transform.position, container.transform.position);
                if (dist <= container.interactionRadius && dist < minContainerDist)
                {
                    minContainerDist = dist;
                    closestContainer = container;
                }
            }
            if (closestContainer != null)
            {
                PlayerInventory inv = GetComponent<PlayerInventory>();
                if (inv != null && LootUI.Instance != null)
                {
                    LootUI.Instance.Open(closestContainer, inv);
                    return; // Opened Loot UI, skip other interactions!
                }
            }

            // Standard item collect fallback if not transitioning
            CollectibleItem[] collectibles = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            CollectibleItem closestItem = null;
            float closestDist = float.MaxValue;

            foreach (var item in collectibles)
            {
                if (item == null) continue;
                float dist = Vector2.Distance(transform.position, item.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestItem = item;
                }
            }

            if (closestItem != null && closestItem.IsPlayerInRange())
            {
                if (closestItem.IsMoney)
                {
                    _inventory.AddMoney(closestItem.MoneyAmount);
                }
                else
                {
                    _inventory.AddItem(closestItem.ItemName, closestItem.Quantity);
                }
                closestItem.Collect();
                OnAmmoChanged?.Invoke();
            }
        }

        private void ShootWeapon()
        {
            if (_isReloading) return;

            Weapon activeWeapon = CurrentWeapon;
            if (activeWeapon == null) return; // ถ้าไม่ใส่ปืน ห้ามยิง
            int magCount = activeWeapon != null ? activeWeapon.currentMagazine : _currentMagazine;

            if (magCount <= 0)
            {
                TryStartReload();
                return;
            }

            // Decrement ammo
            if (activeWeapon != null)
            {
                activeWeapon.currentMagazine--;
            }
            else
            {
                _currentMagazine--;
            }
            OnAmmoChanged?.Invoke();

            // Set cooldown
            float rate = activeWeapon != null ? activeWeapon.fireRate : fireRate;
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

            // ─── Helper methods for Muzzle Flash Point Light ───
            
            // 💥 เล่นเอฟเฟกต์ Muzzle Flash ถ้าระบุไว้ในอาวุธ
            string flashKey = activeWeapon != null ? activeWeapon.vfxPoolKey : null;
            if (string.IsNullOrEmpty(flashKey) || flashKey.Equals("PistalFlash", System.StringComparison.OrdinalIgnoreCase))
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

            int pellets = (activeWeapon != null && activeWeapon.pelletsPerShot > 0) ? activeWeapon.pelletsPerShot : 1;
            float spread = (activeWeapon != null) ? activeWeapon.spreadAngle : 0f;

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

                        if (ItemDatabase.Instance != null)
                        {
                            ItemData item = ItemDatabase.Instance.GetItemByName(activeWeapon.weaponName);
                            if (item != null)
                            {
                                itemDamage = item.damage;

                                // If attackRadius is configured in ItemData, calculate bullet lifetime based on projectile speed
                                if (item.attackRadius > 0.01f && proj.Speed > 0.01f)
                                {
                                    weaponRange = item.attackRadius / proj.Speed;
                                }
                            }
                        }
                        float finalDamage = basePlayerDamage + itemDamage;

                        // Effective falloff range (e.g. standard 14m, snipers extend further)
                        float effectiveFalloffDist = DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;
                        if (activeCharacterData != null && activeCharacterData.characterId == "MARKSMAN")
                        {
                            effectiveFalloffDist = 22f; // Marksman maintains high damage at sniper ranges
                        }

                        proj.SetStats(finalDamage, weaponRange, activeWeapon.canPierce, activeWeapon.weaponName, effectiveFalloffDist);
                        Debug.Log($"[ShootWeapon] Fired {activeWeapon.weaponName}! Total Damage: {finalDamage} (Base: {basePlayerDamage} + Weapon: {itemDamage}) | Lifetime: {weaponRange:F2}s | Speed: {proj.Speed} m/s");
                    }
                    
                    // Pass the aim target point for Arced trajectories
                    proj.Setup(bulletDir, _aimTargetPoint, gameObject);
                }
            }
        }

        private void MeleeAttack()
        {
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
            float activeDamage = basePlayerDamage + itemDamage;

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
                string slashState = "Slash" + _meleeComboStep;
                _animator.ResetTrigger("Slash1");
                _animator.ResetTrigger("Slash2");
                _animator.ResetTrigger("Slash3");

                if (_animator.layerCount > 1) _animator.Play("UpperBody_Empty", 1, 0f);
                _shootAnimLayerTimer = 0f;
                _meleeAnimTimer = 0.40f;
                _fireBufferTimer = 0f;
                _animator.ResetTrigger("Shoot");
                if (_animator.layerCount > 1)
                {
                    _animator.SetLayerWeight(1, 0f);
                    _animator.Play("UpperBody_Empty", 1, 0f);
                }
                _animator.Play(slashState, 0, 0f);
            }

            Collider[] hitColliders = Physics.OverlapSphere(attackPoint, activeRadius);
            int targetsHit = 0;
            int maxTargets = isKatana ? 4 : 999; // Katana strikes up to 4 targets in wide cleave

            foreach (var col in hitColliders)
            {
                if (col.gameObject == gameObject) continue;
                if (targetsHit >= maxTargets) break;

                // 1. Deal damage
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float hitDmg = meleeDmgResult.FinalDamage;
                    // Fire Axe: 25% chance for Bleed (extra 15 damage)
                    if (isFireAxe && Random.value < 0.25f)
                    {
                        hitDmg += 15f;
                        Debug.Log("[MeleeAttack] Fire Axe caused bleeding laceration (+15 damage)!");
                    }
                    bool wasDeadBefore = damageable.IsDead;
                    damageable.TakeDamage(hitDmg, col.transform.position, meleeDmgResult.IsCritical);
                    targetsHit++;
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

                    float finalStagger = isSledgehammer ? 0.8f : (isStunBaton ? 0.6f : activeStagger);
                    float finalKnockback = isSledgehammer ? 20f : activeKnockback;
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

        private void StartDash()
        {
            _isDashing = true;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;

            // Dash towards movement input, or aim direction if standing still
            _dashDirection = _moveInput.sqrMagnitude > 0.01f ? new Vector3(_moveInput.x, 0f, _moveInput.y).normalized : _aimDirection;

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
            if (clean == "fuel ammo") return "Heavy Ammo";
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
            
            int mag = activeWeapon != null ? activeWeapon.currentMagazine : _currentMagazine;
            int magSize = activeWeapon != null ? activeWeapon.magazineSize : magazineSize;
            
            string ammoType = "Pistol Ammo";
            if (activeWeapon != null && !string.IsNullOrEmpty(activeWeapon.ammoType))
            {
                ammoType = activeWeapon.ammoType;
            }
            int reserve = GetAmmoReserveCount(ammoType);

            if (_isReloading || mag == magSize || reserve <= 0)
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
            float duration = activeWeapon != null ? activeWeapon.reloadDuration : reloadDuration;

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
                int needed = activeWeapon.magazineSize - activeWeapon.currentMagazine;
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

            float baseRate = activeCharacterData != null ? activeCharacterData.baseHungerDepletionRate : 0.5f;
            float sprintMult = activeCharacterData != null ? activeCharacterData.sprintHungerMultiplier : 4.0f;
            float charMult = activeCharacterData != null ? activeCharacterData.hungerConsumptionMultiplier : 1.0f;
            float depletionRate = (_isSprinting ? (baseRate * sprintMult) : baseRate) * charMult;
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
                            for (int i = 0; i < ammoEntry.count; i++)
                            {
                                _inventory.AddItem(ammoEntry.ammoType);
                            }
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

                    for (int i = 0; i < activeCharacterData.startingPistolAmmo; i++)
                    {
                        _inventory.AddItem(fallbackAmmo);
                    }
                }

                // Grant starting survival rations (e.g. Bread)
                int rationCount = activeCharacterData.startingRations > 0 ? activeCharacterData.startingRations : 2;
                for (int i = 0; i < rationCount; i++)
                {
                    _inventory.AddItem("Bread");
                }

                // Grant starting extra items if configured
                if (activeCharacterData.startingExtraItems != null)
                {
                    foreach (var extra in activeCharacterData.startingExtraItems)
                    {
                        if (!string.IsNullOrEmpty(extra.itemName) && extra.count > 0)
                        {
                            for (int i = 0; i < extra.count; i++)
                            {
                                _inventory.AddItem(extra.itemName);
                            }
                        }
                    }
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
                    fireRate = 1.2f,
                    magazineSize = 5,
                    reloadDuration = 2.2f,
                    damage = 110f,
                    range = 6.0f,
                    canPierce = true,
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
                    fireRate = 0.1f,
                    magazineSize = 50,
                    reloadDuration = 2.5f,
                    damage = 10f,
                    range = 1.8f,
                    canPierce = true,
                    spreadAngle = 20f,
                    pelletsPerShot = 2,
                    isAutomatic = true,
                    ammoType = "Heavy Ammo",
                    vfxPoolKey = "PistolFlash"
                };
                flamethrower.Initialize(100);
                weapons.Add(flamethrower);

                // 8. Rocket Launcher
                Weapon rocketLauncher = new Weapon
                {
                    weaponName = "Rocket Launcher",
                    projectilePrefab = projectilePrefab,
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
                GameObject boltObj = Resources.Load<GameObject>("Prefabs/Item/BoltPrefabs");
                if (boltObj != null)
                {
                    Projectile loadedBolt = boltObj.GetComponent<Projectile>();
                    if (loadedBolt != null) boltProjPrefab = loadedBolt;
                }

                Weapon crossbow = new Weapon
                {
                    weaponName = "Crossbow",
                    projectilePrefab = boltProjPrefab,
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

            // Check if it's one of the active operative's starting weapons
            if (activeCharacterData != null && activeCharacterData.startingWeapons != null)
            {
                if (activeCharacterData.startingWeapons.Exists(sw => IsWeaponNameMatch(sw, weaponName))) return true;
            }

            // Check if player has it in inventory bag
            if (_inventory != null && _inventory.Items != null)
            {
                if (_inventory.Items.Exists(item => IsWeaponNameMatch(item, weaponName))) return true;
            }

            return false;
        }

        public bool HasMeleeWeaponAccess(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return false;

            // Check if it's one of the active operative's starting weapons
            if (activeCharacterData != null && activeCharacterData.startingWeapons != null)
            {
                if (activeCharacterData.startingWeapons.Exists(sw => IsWeaponNameMatch(sw, weaponName))) return true;
            }

            // Check if player has it in inventory bag
            if (_inventory != null && _inventory.Items != null)
            {
                if (_inventory.Items.Exists(item => IsWeaponNameMatch(item, weaponName))) return true;
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
            Debug.Log($"[PlayerController] Equipped melee weapon: {wName}");
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
            Debug.Log($"[PlayerController] Switched to weapon: {CurrentWeapon.weaponName}");
            
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
                return;
            }

            LootContainer[] containers = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
            LootContainer closestContainer = null;
            float minContainerDist = float.MaxValue;
            foreach (LootContainer container in containers)
            {
                if (container == null) continue;
                float dist = Vector3.Distance(transform.position, container.transform.position);
                if (dist <= container.interactionRadius && dist < minContainerDist)
                {
                    minContainerDist = dist;
                    closestContainer = container;
                }
            }

            if (closestContainer != null && LootUI.Instance != null)
            {
                LootUI.Instance.ShowPrompt("Search " + closestContainer.containerName, closestContainer.transform);
            }
            else
            {
                // Fallback to check if near an NPC Merchant to show interaction prompt!
                NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None);
                NPCController closestNpc = null;
                float minNpcDist = float.MaxValue;
                foreach (NPCController npc in npcs)
                {
                    if (npc != null && npc.IsPlayerInRange(transform.position))
                    {
                        float dist = Vector3.Distance(transform.position, npc.transform.position);
                        if (dist < minNpcDist)
                        {
                            minNpcDist = dist;
                            closestNpc = npc;
                        }
                    }
                }

                if (closestNpc != null && LootUI.Instance != null)
                {
                    if (closestNpc.HasShop && closestNpc.HasDialogue)
                    {
                        LootUI.Instance.ShowRawPrompt($"<color=yellow><b>[E]</b></color> Shop\n<color=#00e5ff><b>[Q]</b></color> Talk", closestNpc.transform);
                    }
                    else if (closestNpc.HasShop)
                    {
                        LootUI.Instance.ShowRawPrompt($"<color=yellow><b>[E]</b></color> Shop", closestNpc.transform);
                    }
                    else if (closestNpc.HasDialogue)
                    {
                        LootUI.Instance.ShowRawPrompt($"<color=#00e5ff><b>[Q]</b></color> Talk", closestNpc.transform);
                    }
                    else
                    {
                        string action = closestNpc.NpcType == NPCType.Shop ? "Shop" : "Interact";
                        LootUI.Instance.ShowRawPrompt($"<color=yellow><b>[E]</b></color> {action}", closestNpc.transform);
                    }
                }
                else if (LootUI.Instance != null)
                {
                    LootUI.Instance.HidePrompt();
                }
            }
        }

        private void TryTalk()
        {
            if (PopUpManager.Instance != null && PopUpManager.Instance.HasActivePopUps) return;

            NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            foreach (NPCController npc in npcs)
            {
                if (npc != null && npc.IsPlayerInRange(transform.position))
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

            // Sprite Indicators (flat on ground, facing top-down camera)
            GameObject throwRangeObj = new GameObject("ThrowRangeSprite");
            throwRangeObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _throwRangeSpriteRenderer = throwRangeObj.AddComponent<SpriteRenderer>();
            _throwRangeSpriteRenderer.sprite = throwRangeSprite;
            _throwRangeSpriteRenderer.color = throwRangeColor;
            throwRangeObj.SetActive(false);

            GameObject aoeObj = new GameObject("AOESprite");
            aoeObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _aoeSpriteRenderer = aoeObj.AddComponent<SpriteRenderer>();
            _aoeSpriteRenderer.sprite = aoeIndicatorSprite;
            _aoeSpriteRenderer.color = aoeIndicatorColor;
            aoeObj.SetActive(false);

            _laserSightIndicator = CreateLaserLineRenderer("LaserSightIndicator", new Color(1f, 0.15f, 0.15f, 0.95f), 0.045f);
            _laserTargetDot = CreateLineRenderer("LaserTargetDot", new Color(1f, 0.2f, 0.2f, 0.95f), 0.035f);
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
