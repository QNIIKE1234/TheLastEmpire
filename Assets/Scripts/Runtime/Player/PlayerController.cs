using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
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

        // Public properties and events for HUD mapping
        public Health PlayerHealth => _health;
        public int CurrentMagazine => CurrentWeapon != null ? CurrentWeapon.currentMagazine : _currentMagazine;
        public int CurrentReserveAmmo => CurrentWeapon != null ? CurrentWeapon.currentReserveAmmo : _currentReserveAmmo;
        public bool IsReloading => _isReloading;
        public Weapon CurrentWeapon => (weapons != null && weapons.Count > 0 && _currentWeaponIndex >= 0 && _currentWeaponIndex < weapons.Count) ? weapons[_currentWeaponIndex] : null;
        public string CurrentWeaponName => CurrentWeapon != null ? CurrentWeapon.weaponName : "Pistol";
        public System.Collections.Generic.List<Weapon> WeaponsList => weapons;
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
        private float _meleeCooldownTimer = 0f;

        [Header("Gunshot Noise Settings")]
        [SerializeField] private float gunshotAlertRange = 5f;
        
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

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

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
            _health = GetComponent<Health>();
            if (_health == null)
            {
                _health = gameObject.AddComponent<Health>();
            }

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
            OnAmmoChanged?.Invoke();

            // SetupScreenBoundaries(); // Disabled: Using manual scene boundaries/walls instead
        }

        private void Update()
        {
            _isSprinting = Keyboard.current != null && Keyboard.current.shiftKey.isPressed && _moveInput.sqrMagnitude > 0.01f;
            UpdateHunger();
            // Allow opening/closing the inventory with I or Escape even when game timescale is paused
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame)
                {
                    ToggleInventoryMenu();
                    return;
                }
                if (Keyboard.current.escapeKey.wasPressedThisFrame && InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
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
                              (LootUI.Instance != null && LootUI.Instance.IsOpen);
            if (isMenuOpen)
            {
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
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
            if (_meleeCooldownTimer > 0f) _meleeCooldownTimer -= Time.deltaTime;

            CheckBoundaries();

            if (_isDashing)
            {
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0f)
                {
                    EndDash();
                }
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
        }

        private void FixedUpdate()
        {
            if (_isDashing)
            {
                _rb.linearVelocity = _dashDirection * dashSpeed;
            }
            else
            {
                // Normal 8-directional movement (1.5x speed if sprinting)
                float currentSpeed = _isSprinting ? (moveSpeed * 2.5f) : moveSpeed;
                _rb.linearVelocity = new Vector3(_moveInput.x * currentSpeed, _rb.linearVelocity.y, _moveInput.y * currentSpeed);
            }
        }

        private void UpdateAimDirection()
        {
            if (Camera.main == null) return;

            // Aim towards mouse position in world space using 3D raycast on X/Z plane
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float rayDistance))
            {
                Vector3 targetPoint = ray.GetPoint(rayDistance);
                Vector3 direction = (targetPoint - transform.position);
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
            // Direct input checks to ensure combat always works regardless of playerinput maps
            if (Mouse.current != null)
            {
                // Ignore attacks if clicking over a UI element
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                bool wantToShoot = false;
                if (CurrentWeapon != null && CurrentWeapon.isAutomatic)
                {
                    wantToShoot = Mouse.current.leftButton.isPressed;
                }
                else
                {
                    wantToShoot = Mouse.current.leftButton.wasPressedThisFrame;
                }

                if (wantToShoot && _fireCooldownTimer <= 0f)
                {
                    ShootWeapon();
                }
                if (Mouse.current.rightButton.wasPressedThisFrame && _meleeCooldownTimer <= 0f)
                {
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
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchToWeapon(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchToWeapon(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchToWeapon(2);

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
            }
        }

        // Called by PlayerInput component via SendMessages
        private void OnMove(InputValue value)
        {
            bool isMenuOpen = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || 
                              (LootUI.Instance != null && LootUI.Instance.IsOpen);
            if (isMenuOpen)
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

            bool isMenuOpen = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || 
                              (LootUI.Instance != null && LootUI.Instance.IsOpen);
            if (isMenuOpen) return;

            // Attack action maps to left click / shooting
            if (value.isPressed && _fireCooldownTimer <= 0f && !_isDashing)
            {
                ShootWeapon();
            }
        }

        private void OnInteract(InputValue value)
        {
            bool isMenuOpen = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || 
                              (LootUI.Instance != null && LootUI.Instance.IsOpen);
            if (isMenuOpen) return;

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
                else if (closestItem.ItemName == "Ammo")
                {
                    if (CurrentWeapon != null)
                    {
                        CurrentWeapon.currentReserveAmmo += closestItem.Quantity;
                        Debug.Log($"[PlayerController] Picked up {closestItem.Quantity} ammo for {CurrentWeapon.weaponName}. Total reserve: {CurrentWeapon.currentReserveAmmo}");
                    }
                    else
                    {
                        _currentReserveAmmo += closestItem.Quantity;
                        Debug.Log($"[PlayerController] Picked up {closestItem.Quantity} ammo. Total reserve: {_currentReserveAmmo}");
                    }
                    OnAmmoChanged?.Invoke();
                }
                else
                {
                    _inventory.AddItem(closestItem.ItemName, closestItem.Quantity);
                }
                closestItem.Collect();
            }
        }

        private void ShootWeapon()
        {
            if (_isReloading) return;

            Weapon activeWeapon = CurrentWeapon;
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
            _fireCooldownTimer = rate;

            // Alert nearby enemies to the gunshot noise
            AlertEnemiesToGunshot();

            // Spawning positions
            Vector3 spawnPos = transform.position + _aimDirection * 0.6f;
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

                Projectile proj = bullet.GetComponent<Projectile>();
                if (proj != null)
                {
                    if (activeWeapon != null)
                    {
                        proj.SetStats(activeWeapon.damage, activeWeapon.range, activeWeapon.canPierce);
                        Debug.Log($"[ShootWeapon] Fired {activeWeapon.weaponName}! Speed: {proj.Speed} m/s | Lifetime: {activeWeapon.range}s | Calculated Distance: {proj.Speed * activeWeapon.range} meters");
                    }
                    proj.Setup(bulletDir, gameObject);
                }
            }
        }

        private void MeleeAttack()
        {
            _meleeCooldownTimer = meleeRate;
            Vector3 attackPoint = transform.position + _aimDirection * 0.8f;

            // Visual swing debug
            Debug.Log("[PlayerController] Melee Swing Swung (Area Attack)!");

            Collider[] hitColliders = Physics.OverlapSphere(attackPoint, meleeRadius);
            foreach (var col in hitColliders)
            {
                if (col.gameObject == gameObject) continue;

                // 1. Deal damage
                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(meleeDamage);
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
                    enemy.ApplyMeleeKnockback(pushDir, 10f, 0.4f); // Strong push force (10f) and longer stagger duration (0.4s)
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

        private void TryStartReload()
        {
            Weapon activeWeapon = CurrentWeapon;
            int mag = activeWeapon != null ? activeWeapon.currentMagazine : _currentMagazine;
            int magSize = activeWeapon != null ? activeWeapon.magazineSize : magazineSize;
            int reserve = activeWeapon != null ? activeWeapon.currentReserveAmmo : _currentReserveAmmo;

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

            // Re-fetch current weapon status in case it swapped during reload (though we block swap, it's safer)
            activeWeapon = CurrentWeapon;
            if (activeWeapon != null)
            {
                int needed = activeWeapon.magazineSize - activeWeapon.currentMagazine;
                int toLoad = Mathf.Min(needed, activeWeapon.currentReserveAmmo);
                activeWeapon.currentMagazine += toLoad;
                activeWeapon.currentReserveAmmo -= toLoad;
            }
            else
            {
                int needed = magazineSize - _currentMagazine;
                int toLoad = Mathf.Min(needed, _currentReserveAmmo);
                _currentMagazine += toLoad;
                _currentReserveAmmo -= toLoad;
            }

            _isReloading = false;
            OnAmmoChanged?.Invoke();
        }

        private void UpdateHunger()
        {
            if (_health != null && _health.IsDead) return;

            // Sprinting depletes hunger at 4x rate (2.0f instead of 0.5f per second)
            float depletionRate = _isSprinting ? 2.0f : 0.5f;
            currentHunger = Mathf.Max(0f, currentHunger - Time.deltaTime * depletionRate);
            OnHungerChanged?.Invoke();

            if (currentHunger <= 0f)
            {
                _hungerDamageTimer += Time.deltaTime;
                if (_hungerDamageTimer >= 2f)
                {
                    _hungerDamageTimer = 0f;
                    if (_health != null)
                    {
                        _health.TakeDamage(5f); // Starving damage
                        Debug.Log("[PlayerController] Starving! Took 5 damage.");
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
            OnHungerChanged?.Invoke();
        }

        private void InitializeDefaultWeapons()
        {
            if (weapons == null)
            {
                weapons = new System.Collections.Generic.List<Weapon>();
            }

            if (weapons.Count == 0)
            {
                // 1. Pistol
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
                    isAutomatic = false
                };
                pistol.Initialize(startingReserveAmmo);
                weapons.Add(pistol);

                // 2. Rifle
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
                    isAutomatic = true
                };
                rifle.Initialize(120);
                weapons.Add(rifle);

                // 3. Shotgun
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
                    isAutomatic = false
                };
                shotgun.Initialize(24);
                weapons.Add(shotgun);
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

        public void SwitchToWeapon(int index)
        {
            if (weapons == null || weapons.Count == 0) return;
            if (index < 0 || index >= weapons.Count) return;
            if (_isReloading) return; // Block switching while reloading
            
            // Check weapon ownership from inventory
            string wName = weapons[index].weaponName;
            bool isPistol = (wName ?? "").ToLower().Trim().Contains("pist");
            bool hasWeapon = isPistol || (_inventory != null && _inventory.Items.Exists(x => (x ?? "").ToLower().Trim() == wName.ToLower().Trim()));

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
                bool isPistol = (wName ?? "").ToLower().Trim().Contains("pist");
                bool hasWeapon = isPistol || (_inventory != null && _inventory.Items.Exists(x => (x ?? "").ToLower().Trim() == wName.ToLower().Trim()));

                if (hasWeapon)
                {
                    SwitchToWeapon(nextIndex);
                    return;
                }
            }
        }

        private void AlertEnemiesToGunshot()
        {
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
            bool isMenuOpen = (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) || 
                              (LootUI.Instance != null && LootUI.Instance.IsOpen);
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
                LootUI.Instance.ShowPrompt("Search " + closestContainer.containerName);
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
                    string action = closestNpc.NpcType == NPCType.Shop ? "Shop" : "Talk";
                    LootUI.Instance.ShowPrompt($"{action} with {closestNpc.gameObject.name}");
                }
                else if (LootUI.Instance != null)
                {
                    LootUI.Instance.HidePrompt();
                }
            }
        }
    }

    [System.Serializable]
    public class Weapon
    {
        public string weaponName;
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

        [Header("Runtime State")]
        public int currentMagazine;
        public int currentReserveAmmo;

        public void Initialize(int startingReserve)
        {
            string lowerName = (weaponName ?? "").ToLower().Trim();
            bool isRifle = lowerName.Contains("rifl");
            bool isShotgun = lowerName.Contains("shot");
            bool isPistol = lowerName.Contains("pist") || (!isRifle && !isShotgun);

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
    }
}
