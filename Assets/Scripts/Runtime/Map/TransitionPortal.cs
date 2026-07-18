using UnityEngine;

namespace TheLastEmpire
{
    public class TransitionPortal : MonoBehaviour
    {
        public enum TransitionDirection
        {
            North,
            South,
            East,
            West
        }

        [Header("Portal Settings")]
        [SerializeField] private TransitionDirection direction;
        
        [Tooltip("How far the player is spawned from the center on entry in the new stage.")]
        [SerializeField] private float entryOffset = 1.5f;

        private bool _playerInRange = false;

        public bool IsPlayerInRange() => _playerInRange;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = true;
                Debug.Log($"[TransitionPortal] Player entered range of {direction} portal.");
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = false;
                Debug.Log($"[TransitionPortal] Player exited range of {direction} portal.");
            }
        }

        private void Start()
        {
            // 1. Ensure we have a collider trigger zone
            Collider2D col = GetComponent<Collider2D>();
            if (col == null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
            }

            // 2. Procedural placeholder visualization if there is no sprite asset
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
            }
            if (sr.sprite == null)
            {
                sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                sr.color = new Color(0.5f, 0.5f, 0.5f, 0.35f); // Faint semi-transparent gray
                sr.sortingOrder = 2; // Render above background

                // Set default rectangular scales based on direction
                if (transform.localScale == Vector3.one)
                {
                    if (direction == TransitionDirection.North || direction == TransitionDirection.South)
                    {
                        transform.localScale = new Vector3(2.2f, 0.6f, 1f); // horizontal portal block
                    }
                    else
                    {
                        transform.localScale = new Vector3(0.6f, 2.2f, 1f); // vertical portal block
                    }
                }
            }

            // 3. Register to stage change updates
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += UpdatePortalVisibility;
                UpdatePortalVisibility(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
        }

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= UpdatePortalVisibility;
            }
        }

        private void UpdatePortalVisibility(int x, int y)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            bool pathExists = false;
            switch (direction)
            {
                case TransitionDirection.North:
                    pathExists = y < WorldMapGenerator.GridSize - 1 && WorldMapManager.Instance.MapGenerator.GetStage(x, y + 1) != null;
                    break;
                case TransitionDirection.South:
                    pathExists = y > 0 && WorldMapManager.Instance.MapGenerator.GetStage(x, y - 1) != null;
                    break;
                case TransitionDirection.East:
                    pathExists = x < WorldMapGenerator.GridSize - 1 && WorldMapManager.Instance.MapGenerator.GetStage(x + 1, y) != null;
                    break;
                case TransitionDirection.West:
                    pathExists = x > 0 && WorldMapManager.Instance.MapGenerator.GetStage(x - 1, y) != null;
                    break;
            }

            // Clean up: Disable visual sprite renderer and collision trigger so player cannot see/enter invalid directions
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = pathExists;
            }

            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = pathExists;
            }

            Debug.Log($"[TransitionPortal] {direction} portal visibility set to: {pathExists} (Stage: {x}, {y})");
        }

        public void TriggerTransition(PlayerController player)
        {
            if (WorldMapManager.Instance == null) return;

            int playerX = WorldMapManager.Instance.CurrentPlayerX;
            int playerY = WorldMapManager.Instance.CurrentPlayerY;

            switch (direction)
            {
                case TransitionDirection.North:
                    if (playerY < WorldMapGenerator.GridSize - 1)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX, playerY + 1);
                        Debug.Log($"[TransitionPortal] Move player North to Stage ({playerX}, {playerY + 1})");
                    }
                    break;

                case TransitionDirection.South:
                    if (playerY > 0)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX, playerY - 1);
                        Debug.Log($"[TransitionPortal] Move player South to Stage ({playerX}, {playerY - 1})");
                    }
                    break;

                case TransitionDirection.East:
                    if (playerX < WorldMapGenerator.GridSize - 1)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX + 1, playerY);
                        Debug.Log($"[TransitionPortal] Move player East to Stage ({playerX + 1}, {playerY})");
                    }
                    break;

                case TransitionDirection.West:
                    if (playerX > 0)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX - 1, playerY);
                        Debug.Log($"[TransitionPortal] Move player West to Stage ({playerX - 1}, {playerY})");
                    }
                    break;
            }
        }
    }
}
