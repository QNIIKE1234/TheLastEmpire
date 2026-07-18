using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Screen Boundaries")]
        [SerializeField] private float xLimit = 8.5f;
        [SerializeField] private float yLimit = 5f;
        [SerializeField] private float entryOffset = 0.5f;

        private Rigidbody2D _rb;
        private Vector2 _moveInput;

        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

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
                    // Enable the default Player action map
                    playerInput.currentActionMap = defaultActions.FindActionMap("Player");
                    playerInput.currentActionMap?.Enable();
                    Debug.Log("PlayerController: Automatically linked InputSystem_Actions asset.");
                }
                else
                {
                    Debug.LogWarning("PlayerController: Could not find default InputSystem_Actions asset at 'Assets/InputSystem_Actions.inputactions'. Please assign it manually in the PlayerInput component.");
                }
            }
#endif
        }

        private void Update()
        {
            CheckBoundaries();
        }

        private void FixedUpdate()
        {
            // Apply movement
            _rb.linearVelocity = _moveInput * moveSpeed;
        }

        // Called automatically by PlayerInput component (via SendMessages) when movement input changes
        private void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        private void CheckBoundaries()
        {
            if (WorldMapManager.Instance == null) return;

            Vector3 pos = transform.position;
            int playerX = WorldMapManager.Instance.CurrentPlayerX;
            int playerY = WorldMapManager.Instance.CurrentPlayerY;

            bool transitioned = false;

            // East boundary (Exit Right)
            if (pos.x > xLimit)
            {
                if (playerX < WorldMapGenerator.GridSize - 1)
                {
                    WorldMapManager.Instance.MovePlayer(playerX + 1, playerY);
                    pos.x = -xLimit + entryOffset;
                    transitioned = true;
                }
                else
                {
                    pos.x = xLimit;
                }
            }
            // West boundary (Exit Left)
            else if (pos.x < -xLimit)
            {
                if (playerX > 0)
                {
                    WorldMapManager.Instance.MovePlayer(playerX - 1, playerY);
                    pos.x = xLimit - entryOffset;
                    transitioned = true;
                }
                else
                {
                    pos.x = -xLimit;
                }
            }

            // North boundary (Exit Up)
            if (pos.y > yLimit)
            {
                if (playerY < WorldMapGenerator.GridSize - 1)
                {
                    WorldMapManager.Instance.MovePlayer(playerX, playerY + 1);
                    pos.y = -yLimit + entryOffset;
                    transitioned = true;
                }
                else
                {
                    pos.y = yLimit;
                }
            }
            // South boundary (Exit Down)
            else if (pos.y < -yLimit)
            {
                if (playerY > 0)
                {
                    WorldMapManager.Instance.MovePlayer(playerX, playerY - 1);
                    pos.y = yLimit - entryOffset;
                    transitioned = true;
                }
                else
                {
                    pos.y = -yLimit;
                }
            }

            if (transitioned)
            {
                transform.position = pos;
            }
        }
    }
}
