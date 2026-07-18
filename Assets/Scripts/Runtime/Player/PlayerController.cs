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
        }

        private void Update()
        {
            // Gather input using the New Input System direct API
            Vector2 move = Vector2.zero;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            }

            if (Gamepad.current != null)
            {
                move += Gamepad.current.leftStick.ReadValue();
            }

            _moveInput = move.normalized;

            CheckBoundaries();
        }

        private void FixedUpdate()
        {
            // Apply movement
            _rb.linearVelocity = _moveInput * moveSpeed;
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
