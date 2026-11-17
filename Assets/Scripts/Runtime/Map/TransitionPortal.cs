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
        public TransitionDirection Direction => direction;
        
        [Tooltip("How far the player is spawned from the center on entry in the new stage.")]
        [SerializeField] private float entryOffset = 1.5f;
        public float EntryOffset => entryOffset;

        public Vector3 GetInwardEntryPosition()
        {
            Vector3 portalPos = transform.position;
            Vector3 entryPos = portalPos;
            entryPos.y = Mathf.Max(portalPos.y, 0.5f);
            switch (direction)
            {
                case TransitionDirection.North:
                    entryPos.z -= entryOffset;
                    break;
                case TransitionDirection.South:
                    entryPos.z += entryOffset;
                    break;
                case TransitionDirection.East:
                    entryPos.x -= entryOffset;
                    break;
                case TransitionDirection.West:
                    entryPos.x += entryOffset;
                    break;
            }
            return entryPos;
        }

        [Header("Random Portal Layout Settings")]
        [SerializeField] private float minSpawnOffset = -5.0f;
        [SerializeField] private float maxSpawnOffset = 5.0f;

        [Header("Transition UI (Optional)")]
        [SerializeField] private GameObject transitionScreen;

        public static TransitionDirection LastTransitionEntryDirection { get; private set; } = TransitionDirection.South;
        public static Vector3 LastPlayerWarpInPos { get; private set; } = new Vector3(0, 0.5f, -6f);
        public static bool HasValidWarpHistory { get; private set; } = false;

        private bool _playerInRange = false;

        public bool IsPlayerInRange() => _playerInRange;

        public static bool IsWorldPortal(TransitionPortal portal)
        {
            if (portal == null) return false;
            if (portal.GetComponent<RectTransform>() != null) return false;
            if (portal.gameObject.layer == 5) return false;
            return true;
        }

        private void OnDisable()
        {
            // Portal hidden/disabled (no path in this stage) -> never keep a stale "in range" flag
            _playerInRange = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsWorldPortal(this)) return;

            if (other.CompareTag("Player"))
            {
                _playerInRange = true;
                Debug.Log($"[TransitionPortal] Player entered range of {direction} portal.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsWorldPortal(this)) return;

            if (other.CompareTag("Player"))
            {
                _playerInRange = false;
                Debug.Log($"[TransitionPortal] Player exited range of {direction} portal.");
            }
        }

        private void Start()
        {
            if (!IsWorldPortal(this))
            {
                // UI / Canvas element with legacy TransitionPortal - disable component to prevent interference
                enabled = false;
                return;
            }

            // 1. Ensure we have a collider trigger zone
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }

            // 2. Register to stage change updates
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

        private void GetTargetCoordinates(int originX, int originY, out int targetX, out int targetY)
        {
            targetX = originX;
            targetY = originY;
            switch (direction)
            {
                case TransitionDirection.North: targetY = originY + 1; break;
                case TransitionDirection.South: targetY = originY - 1; break;
                case TransitionDirection.East: targetX = originX + 1; break;
                case TransitionDirection.West: targetX = originX - 1; break;
            }
        }

        private static bool IsWalkableDestinationStage(int targetX, int targetY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return false;
            if (targetX < 0 || targetX >= WorldMapGenerator.GridSize ||
                targetY < 0 || targetY >= WorldMapGenerator.GridSize)
            {
                return false;
            }

            StageData targetStage = WorldMapManager.Instance.MapGenerator.GetStage(targetX, targetY);
            if (targetStage == null) return false;

            // Waterways biome is currently closed — block portals leading to water zones
            if (targetStage.biome == BiomeType.Waterways) return false;

            return true;
        }

        public void UpdatePortalVisibility(int x, int y)
        {
            if (!IsWorldPortal(this)) return;
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            GetTargetCoordinates(x, y, out int targetX, out int targetY);
            bool pathExists = IsWalkableDestinationStage(targetX, targetY);

            // Clean up: Disable visual renderers (supporting both 3D MeshRenderers, SpriteRenderers, etc. in children)
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = pathExists;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = pathExists;
            }
            if (!pathExists)
            {
                _playerInRange = false; // disabling a collider does not fire OnTriggerExit
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

            GetTargetCoordinates(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY, out int checkX, out int checkY);
            if (!IsWalkableDestinationStage(checkX, checkY))
            {
                _playerInRange = false;
                return;
            }

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

                    // Fade in to solid color (black) over 0.5 seconds using safe DOTween.To
                    DOTween.To(() => img.color, x => img.color = x, new Color(c.r, c.g, c.b, 1f), 0.5f).OnComplete(() =>
                    {
                        // Teleport the player while screen is black
                        ExecuteTeleport(player);

                        // Fade out back to transparent over 0.5 seconds after a 0.5s delay
                        DOTween.To(() => img.color, x => img.color = x, new Color(c.r, c.g, c.b, 0f), 0.5f)
                            .SetDelay(0.5f)
                            .OnComplete(() =>
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

            GetTargetCoordinates(playerX, playerY, out int targetX, out int targetY);
            bool transitioned = IsWalkableDestinationStage(targetX, targetY);
            if (!transitioned) return;

            TransitionDirection oppositeDir = GetOppositeDirection(direction);
            LastTransitionEntryDirection = oppositeDir;
            HasValidWarpHistory = true;

            if (transitioned)
            {
                // Pre-update world portals for the target stage BEFORE MovePlayer so that targetPortal's
                // randomized position and player's new spawn position are ready when GenerateStageEnvironment runs.
                TransitionPortal[] targetPortals = UnityEngine.Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
                TransitionPortal targetPortal = null;
                foreach (var tp in targetPortals)
                {
                    if (IsWorldPortal(tp))
                    {
                        tp.UpdatePortalVisibility(targetX, targetY);
                        if (tp.direction == oppositeDir)
                        {
                            targetPortal = tp;
                        }
                    }
                }

                if (targetPortal != null)
                {
                    pos = targetPortal.GetInwardEntryPosition();
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

                // Set player arrival position BEFORE MovePlayer so EnvironmentManager & GameManager
                // protect the new arrival portal position rather than the old exit portal position.
                player.transform.position = pos;
                LastTransitionEntryDirection = oppositeDir;
                LastPlayerWarpInPos = pos;
                HasValidWarpHistory = true;

                WorldMapManager.Instance.MovePlayer(targetX, targetY);

                // Re-apply position and sync Rigidbody after stage transition callbacks complete
                player.transform.position = pos;

                // Sync Rigidbody position explicitly to prevent Unity's physics engine from overriding the teleportation
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerRb.position = pos;
                    playerRb.linearVelocity = Vector3.zero;
                }
                
                Physics.SyncTransforms(); // Force immediate transform update to colliders

                // Reset _playerInRange on all portals in scene to prevent phantom interactions
                TransitionPortal[] allPortals = UnityEngine.Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
                foreach (var p in allPortals)
                {
                    if (p != null) p._playerInRange = false;
                }

                Debug.Log($"[TransitionPortal] Transitioned stage {direction}. Teleported player to target portal: {pos}");
            }
        }

        public static TransitionDirection GetOppositeDirection(TransitionDirection dir)
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
