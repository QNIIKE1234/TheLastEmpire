using UnityEngine;
using DG.Tweening;

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

        [Header("Random Portal Layout Settings")]
        [SerializeField] private float minSpawnOffset = -5.0f;
        [SerializeField] private float maxSpawnOffset = 5.0f;

        [Header("Transition UI (Optional)")]
        [SerializeField] private GameObject transitionScreen;

        private bool _playerInRange = false;

        public bool IsPlayerInRange() => _playerInRange;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = true;
                Debug.Log($"[TransitionPortal] Player entered range of {direction} portal.");
            }
        }

        private void OnTriggerExit(Collider other)
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
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
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
                sr.color = Color.clear; // Make invisible so it doesn't block the view
                sr.sortingOrder = 2; // Render above background

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

        public void UpdatePortalVisibility(int x, int y)
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

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = pathExists;
            }

            // 3. Randomize this portal's own position along the secondary axis based on current stage seed
            if (pathExists)
            {
                StageData currentStage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
                if (currentStage != null)
                {
                    Random.State oldState = Random.state;
                    Random.InitState(currentStage.stageSeed + (int)direction * 999);

                    float randomVal = Random.Range(minSpawnOffset, maxSpawnOffset);
                    Vector3 localPos = transform.localPosition;

                    if (direction == TransitionDirection.North || direction == TransitionDirection.South)
                    {
                        localPos.x = randomVal;
                    }
                    else
                    {
                        localPos.z = randomVal;
                    }

                    transform.localPosition = localPos;
                    Random.state = oldState;
                }
            }

            Debug.Log($"[TransitionPortal] {direction} portal visibility set to: {pathExists} (Stage: {x}, {y})");
        }

        public void TriggerTransition(PlayerController player)
        {
            if (WorldMapManager.Instance == null) return;

            if (transitionScreen != null)
            {
                transitionScreen.SetActive(true);
                UnityEngine.UI.Image img = transitionScreen.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    // Ensure the screen starts completely transparent
                    Color c = img.color;
                    c.a = 0f;
                    img.color = c;

                    // Fade in to solid color (black) over 0.5 seconds
                    img.DOFade(1f, 0.5f).OnComplete(() =>
                    {
                        // Teleport the player while screen is black
                        ExecuteTeleport(player);

                        // Fade out back to transparent over 0.5 seconds
                        img.DOFade(0f, 0.5f).OnComplete(() =>
                        {
                            transitionScreen.SetActive(false);
                        });
                    });
                }
                else
                {
                    // Fallback if no Image component is present
                    ExecuteTeleport(player);
                    transitionScreen.SetActive(false);
                }
            }
            else
            {
                // Fallback if no transition screen is assigned
                ExecuteTeleport(player);
            }
        }

        private void ExecuteTeleport(PlayerController player)
        {
            int playerX = WorldMapManager.Instance.CurrentPlayerX;
            int playerY = WorldMapManager.Instance.CurrentPlayerY;
            
            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            
            // Get screen bounds (used as fallback if target portal is not found in the scene)
            float calculatedYLimit = 5f;
            float calculatedXLimit = 8.5f;

            if (cam != null && cam.orthographic)
            {
                calculatedYLimit = cam.orthographicSize;
                calculatedXLimit = calculatedYLimit * cam.aspect;
            }

            Vector3 pos = player.transform.position;
            // Ensure player spawn Y height is safe above the ground (minimum 0.5) to avoid clipping under the floor
            pos.y = Mathf.Max(pos.y, 0.5f);
            bool transitioned = false;

            TransitionDirection oppositeDir = GetOppositeDirection(direction);

            switch (direction)
            {
                case TransitionDirection.North:
                    if (playerY < WorldMapGenerator.GridSize - 1)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX, playerY + 1);
                        transitioned = true;
                    }
                    break;

                case TransitionDirection.South:
                    if (playerY > 0)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX, playerY - 1);
                        transitioned = true;
                    }
                    break;

                case TransitionDirection.East:
                    if (playerX < WorldMapGenerator.GridSize - 1)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX + 1, playerY);
                        transitioned = true;
                    }
                    break;

                case TransitionDirection.West:
                    if (playerX > 0)
                    {
                        WorldMapManager.Instance.MovePlayer(playerX - 1, playerY);
                        transitioned = true;
                    }
                    break;
            }

            if (transitioned)
            {
                // Try to find the target portal in the newly generated room to spawn directly at it
                TransitionPortal[] targetPortals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
                TransitionPortal targetPortal = null;
                foreach (var tp in targetPortals)
                {
                    if (tp != null && tp.direction == oppositeDir)
                    {
                        targetPortal = tp;
                        break;
                    }
                }

                if (targetPortal != null)
                {
                    // Spawn player relative to the target portal's actual position
                    Vector3 portalPos = targetPortal.transform.position;
                    pos.y = Mathf.Max(portalPos.y, 0.5f);

                    switch (oppositeDir)
                    {
                        case TransitionDirection.North: // Player entered from North, spawn slightly south of North portal
                            pos.x = portalPos.x;
                            pos.z = portalPos.z - entryOffset;
                            break;
                        case TransitionDirection.South: // Player entered from South, spawn slightly north of South portal
                            pos.x = portalPos.x;
                            pos.z = portalPos.z + entryOffset;
                            break;
                        case TransitionDirection.East:  // Player entered from East, spawn slightly west of East portal
                            pos.x = portalPos.x - entryOffset;
                            pos.z = portalPos.z;
                            break;
                        case TransitionDirection.West:  // Player entered from West, spawn slightly east of West portal
                            pos.x = portalPos.x + entryOffset;
                            pos.z = portalPos.z;
                            break;
                    }
                }
                else
                {
                    // Fallback to screen boundary calculation if portal not found
                    switch (direction)
                    {
                        case TransitionDirection.North:
                            pos.z = -calculatedYLimit + entryOffset;
                            break;
                        case TransitionDirection.South:
                            pos.z = calculatedYLimit - entryOffset;
                            break;
                        case TransitionDirection.East:
                            pos.x = -calculatedXLimit + entryOffset;
                            break;
                        case TransitionDirection.West:
                            pos.x = calculatedXLimit - entryOffset;
                            break;
                    }
                }

                player.transform.position = pos;

                // Sync Rigidbody position explicitly to prevent Unity's physics engine from overriding the teleportation
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerRb.position = pos;
                    playerRb.linearVelocity = Vector3.zero;
                }
                
                Physics.SyncTransforms(); // Force immediate transform update to colliders
                Debug.Log($"[TransitionPortal] Transitioned stage {direction}. Teleported player to target portal: {pos}");
            }
        }

        private TransitionDirection GetOppositeDirection(TransitionDirection dir)
        {
            switch (dir)
            {
                case TransitionDirection.North: return TransitionDirection.South;
                case TransitionDirection.South: return TransitionDirection.North;
                case TransitionDirection.East: return TransitionDirection.West;
                case TransitionDirection.West: return TransitionDirection.East;
                default: return dir;
            }
        }
    }
}
