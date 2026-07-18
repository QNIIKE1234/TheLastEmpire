using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Dash / Dodge Roll")]
        [SerializeField] private float dashSpeed = 15f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float dashCooldown = 0.8f;

        [Header("Combat - Ranged")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private float fireRate = 0.2f;

        [Header("Combat - Melee")]
        [SerializeField] private float meleeRadius = 1.2f;
        [SerializeField] private int meleeDamage = 35;
        [SerializeField] private float meleeRate = 0.4f;

        [Header("Screen Boundaries")]
        [SerializeField] private float xLimit = 8.5f;
        [SerializeField] private float yLimit = 5f;
        [SerializeField] private float entryOffset = 0.5f;

        private Rigidbody2D _rb;
        private Health _health;
        private Vector2 _moveInput;
        private Vector2 _aimDirection = Vector2.right;

        private float _startupDelay = 0.5f;
        private float _dashTimer = 0f;
        private float _dashCooldownTimer = 0f;
        private float _fireCooldownTimer = 0f;
        private float _meleeCooldownTimer = 0f;
        
        private bool _isDashing = false;
        private Vector2 _dashDirection;
        private Color _originalColor = Color.white;
        private SpriteRenderer _spriteRenderer;
        private PlayerInventory _inventory;

        private Collider2D _playerCollider;
        private BoxCollider2D _boundaryLeft;
        private BoxCollider2D _boundaryRight;
        private BoxCollider2D _boundaryTop;
        private BoxCollider2D _boundaryBottom;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

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
#if UNITY_EDITOR
            if (playerInput.actions == null)
            {
                InputActionAsset defaultActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                if (defaultActions != null)
                {
                    playerInput.actions = defaultActions;
                    playerInput.currentActionMap = defaultActions.FindActionMap("Player");
                    playerInput.currentActionMap?.Enable();
                    Debug.Log("PlayerController: Linked default InputActions asset.");
                }
            }
#endif
            SetupScreenBoundaries();
        }

        private void Update()
        {
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
        }

        private void FixedUpdate()
        {
            if (_isDashing)
            {
                _rb.linearVelocity = _dashDirection * dashSpeed;
            }
            else
            {
                // Normal 8-directional movement
                _rb.linearVelocity = _moveInput * moveSpeed;
            }
        }

        private void UpdateAimDirection()
        {
            if (Camera.main == null) return;

            // Aim towards mouse position in world space
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mousePos.z = 0f;

            Vector2 direction = ((Vector2)mousePos - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude > 0.01f)
            {
                _aimDirection = direction;

                // Rotate the player to face aiming direction (optional visual feedback)
                float angle = Mathf.Atan2(_aimDirection.y, _aimDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }

        private void HandleDirectInput()
        {
            // Direct input checks to ensure combat always works regardless of playerinput maps
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed && _fireCooldownTimer <= 0f)
                {
                    ShootWeapon();
                }
                if (Mouse.current.rightButton.wasPressedThisFrame && _meleeCooldownTimer <= 0f)
                {
                    MeleeAttack();
                }
            }

            if (Keyboard.current != null)
            {
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
            _moveInput = value.Get<Vector2>();
        }

        private void OnAttack(InputValue value)
        {
            // Attack action maps to left click / shooting
            if (value.isPressed && _fireCooldownTimer <= 0f && !_isDashing)
            {
                ShootWeapon();
            }
        }

        private void OnInteract(InputValue value)
        {
            if (value.isPressed)
            {
                TryInteract();
            }
        }

        private void TryInteract()
        {
            if (WorldMapManager.Instance == null) return;

            // Check if player is near any screen edge to transition stage coordinates
            Vector3 pos = transform.position;
            int playerX = WorldMapManager.Instance.CurrentPlayerX;
            int playerY = WorldMapManager.Instance.CurrentPlayerY;

            float calculatedXLimit = xLimit;
            float calculatedYLimit = yLimit;
            Vector3 camPos = Vector3.zero;

            Camera cam = Camera.main;
            if (cam != null)
            {
                camPos = cam.transform.position;
                if (cam.orthographic)
                {
                    calculatedYLimit = Mathf.Max(3f, cam.orthographicSize);
                    calculatedXLimit = Mathf.Max(4f, calculatedYLimit * cam.aspect);
                }
            }

            float relX = pos.x - camPos.x;
            float relY = pos.y - camPos.y;
            float interactThreshold = 0.6f; // check if close enough to boundary
            bool transitioned = false;

            // East (Exit Right)
            if (relX > calculatedXLimit - interactThreshold)
            {
                if (playerX < WorldMapGenerator.GridSize - 1)
                {
                    WorldMapManager.Instance.MovePlayer(playerX + 1, playerY);
                    pos.x = camPos.x - calculatedXLimit + entryOffset;
                    transitioned = true;
                }
            }
            // West (Exit Left)
            else if (relX < -calculatedXLimit + interactThreshold)
            {
                if (playerX > 0)
                {
                    WorldMapManager.Instance.MovePlayer(playerX - 1, playerY);
                    pos.x = camPos.x + calculatedXLimit - entryOffset;
                    transitioned = true;
                }
            }
            // North (Exit Up)
            else if (relY > calculatedYLimit - interactThreshold)
            {
                if (playerY < WorldMapGenerator.GridSize - 1)
                {
                    WorldMapManager.Instance.MovePlayer(playerX, playerY + 1);
                    pos.y = camPos.y - calculatedYLimit + entryOffset;
                    transitioned = true;
                }
            }
            // South (Exit Down)
            else if (relY < -calculatedYLimit + interactThreshold)
            {
                if (playerY > 0)
                {
                    WorldMapManager.Instance.MovePlayer(playerX, playerY - 1);
                    pos.y = camPos.y + calculatedYLimit - entryOffset;
                    transitioned = true;
                }
            }

            if (transitioned)
            {
                transform.position = pos;
                Debug.Log($"[PlayerController] Transitioned stage via E key at boundary!");
                return; // successfully transitioned!
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
                    _inventory.AddItem(closestItem.ItemName);
                }
                closestItem.Collect();
            }
        }

        private void ShootWeapon()
        {
            _fireCooldownTimer = fireRate;
            Vector2 spawnPos = (Vector2)transform.position + _aimDirection * 0.6f;

            GameObject bullet = null;
            if (projectilePrefab != null && !string.IsNullOrEmpty(projectilePrefab.PoolKey) && ObjectPoolManager.Instance != null)
            {
                bullet = ObjectPoolManager.Instance.SpawnFromPool(projectilePrefab.PoolKey, spawnPos, Quaternion.identity);
            }

            // Fallback if pool lookup fails, key does not exist, or manager is null
            if (bullet == null && projectilePrefab != null)
            {
                bullet = Instantiate(projectilePrefab.gameObject, spawnPos, Quaternion.identity);
            }

            // Final fallback: dynamic placeholder bullet if no prefab is assigned at all
            if (bullet == null)
            {
                bullet = new GameObject("DynamicBullet");
                bullet.transform.position = spawnPos;
                bullet.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

                SpriteRenderer sr = bullet.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                sr.color = Color.yellow;

                CircleCollider2D col = bullet.AddComponent<CircleCollider2D>();
                col.isTrigger = true;

                bullet.AddComponent<Projectile>();
            }

            Projectile proj = bullet.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Setup(_aimDirection, gameObject);
            }
        }

        private void MeleeAttack()
        {
            _meleeCooldownTimer = meleeRate;
            Vector2 attackPoint = (Vector2)transform.position + _aimDirection * 0.8f;

            // Visual swing debug
            Debug.Log("[PlayerController] Melee Swing Swung!");

            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPoint, meleeRadius);
            foreach (var col in hitColliders)
            {
                if (col.gameObject == gameObject) continue;

                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(meleeDamage);
                }
            }
        }

        private void StartDash()
        {
            _isDashing = true;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;

            // Dash towards movement input, or aim direction if standing still
            _dashDirection = _moveInput.sqrMagnitude > 0.01f ? _moveInput.normalized : _aimDirection;

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
            if (WorldMapManager.Instance == null) return;

            Vector3 pos = transform.position;
            float calculatedXLimit = xLimit;
            float calculatedYLimit = yLimit;
            Vector3 camPos = Vector3.zero;

            Camera cam = Camera.main;
            if (cam != null)
            {
                camPos = cam.transform.position;
                if (cam.orthographic)
                {
                    calculatedYLimit = Mathf.Max(3f, cam.orthographicSize);
                    calculatedXLimit = Mathf.Max(4f, calculatedYLimit * cam.aspect);
                }
            }

            float relX = pos.x - camPos.x;
            float relY = pos.y - camPos.y;

            // Constantly clamp player position inside screen limits
            float clampMargin = 0.25f;
            float maxX = calculatedXLimit - clampMargin;
            float maxY = calculatedYLimit - clampMargin;

            if (relX > maxX) pos.x = camPos.x + maxX;
            else if (relX < -maxX) pos.x = camPos.x - maxX;

            if (relY > maxY) pos.y = camPos.y + maxY;
            else if (relY < -maxY) pos.y = camPos.y - maxY;

            transform.position = pos;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw melee attack radius in editor
            Gizmos.color = Color.red;
            Vector2 attackPoint = (Vector2)transform.position + _aimDirection * 0.8f;
            Gizmos.DrawWireSphere(attackPoint, meleeRadius);
        }

        private void SetupScreenBoundaries()
        {
            _playerCollider = GetComponent<Collider2D>();
            if (_playerCollider == null) return;

            GameObject boundaryContainer = new GameObject("ScreenBoundaries");
            // Set parent to camera target or keep in scene root at position (0,0)
            boundaryContainer.transform.position = Vector3.zero;

            float calculatedYLimit = yLimit;
            float calculatedXLimit = xLimit;
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                calculatedYLimit = Mathf.Max(3f, cam.orthographicSize);
                calculatedXLimit = Mathf.Max(4f, calculatedYLimit * cam.aspect);
            }

            float thickness = 2f; // prevent tunneling

            // Left
            GameObject leftObj = new GameObject("Boundary_Left");
            leftObj.transform.parent = boundaryContainer.transform;
            _boundaryLeft = leftObj.AddComponent<BoxCollider2D>();
            _boundaryLeft.size = new Vector2(thickness, calculatedYLimit * 2 + thickness * 2);
            _boundaryLeft.offset = new Vector2(-calculatedXLimit - thickness / 2, 0);

            // Right
            GameObject rightObj = new GameObject("Boundary_Right");
            rightObj.transform.parent = boundaryContainer.transform;
            _boundaryRight = rightObj.AddComponent<BoxCollider2D>();
            _boundaryRight.size = new Vector2(thickness, calculatedYLimit * 2 + thickness * 2);
            _boundaryRight.offset = new Vector2(calculatedXLimit + thickness / 2, 0);

            // Top
            GameObject topObj = new GameObject("Boundary_Top");
            topObj.transform.parent = boundaryContainer.transform;
            _boundaryTop = topObj.AddComponent<BoxCollider2D>();
            _boundaryTop.size = new Vector2(calculatedXLimit * 2 + thickness * 2, thickness);
            _boundaryTop.offset = new Vector2(0, calculatedYLimit + thickness / 2);

            // Bottom
            GameObject bottomObj = new GameObject("Boundary_Bottom");
            bottomObj.transform.parent = boundaryContainer.transform;
            _boundaryBottom = bottomObj.AddComponent<BoxCollider2D>();
            _boundaryBottom.size = new Vector2(calculatedXLimit * 2 + thickness * 2, thickness);
            _boundaryBottom.offset = new Vector2(0, -calculatedYLimit - thickness / 2);
        }

        private void IgnoreCollisionWithBoundaries(bool ignore)
        {
            if (_playerCollider == null) return;
            if (_boundaryLeft != null) Physics2D.IgnoreCollision(_playerCollider, _boundaryLeft, ignore);
            if (_boundaryRight != null) Physics2D.IgnoreCollision(_playerCollider, _boundaryRight, ignore);
            if (_boundaryTop != null) Physics2D.IgnoreCollision(_playerCollider, _boundaryTop, ignore);
            if (_boundaryBottom != null) Physics2D.IgnoreCollision(_playerCollider, _boundaryBottom, ignore);
        }
    }
}
