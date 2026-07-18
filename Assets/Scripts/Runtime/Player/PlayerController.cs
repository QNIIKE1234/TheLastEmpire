using UnityEngine;

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
            // Gather input
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(moveX, moveY).normalized;

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
