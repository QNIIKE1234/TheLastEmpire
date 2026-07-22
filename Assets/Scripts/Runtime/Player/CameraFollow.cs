using UnityEngine;

namespace TheLastEmpire
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private bool useCurrentOffset = true;

        [Header("Follow Settings")]
        [SerializeField] private float smoothTime = 0.15f;
        [SerializeField] private bool followX = true;
        [SerializeField] private bool followY = false;
        [SerializeField] private bool followZ = true;

        [Header("Bounds Settings")]
        [SerializeField] private bool clampToLimits = true;
        [SerializeField] private SpriteRenderer boundsRenderer;
        
        [Tooltip("Fallback half-width of the stage if boundsRenderer is not assigned/found.")]
        [SerializeField] private float fallbackHalfWidth = 10f;
        [Tooltip("Fallback half-height of the stage if boundsRenderer is not assigned/found.")]
        [SerializeField] private float fallbackHalfHeight = 6f;

        private Vector3 _velocity = Vector3.zero;

        private void Start()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayerTarget();
            }

            if (target != null && useCurrentOffset)
            {
                offset = transform.position - target.position;
            }

            if (clampToLimits && boundsRenderer == null)
            {
                FindBoundsRenderer();
            }
        }

        private void FindPlayerTarget()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void FindBoundsRenderer()
        {
            LocalStageVisualizer visualizer = Object.FindFirstObjectByType<LocalStageVisualizer>();
            if (visualizer != null)
            {
                boundsRenderer = visualizer.BackgroundRenderer;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (autoFindPlayer)
                {
                    FindPlayerTarget();
                }
                return;
            }

            // Calculate desired position based on player movement
            Vector3 targetPos = target.position;
            Vector3 desiredPosition = transform.position;

            if (followX) desiredPosition.x = targetPos.x + offset.x;
            if (followY) desiredPosition.y = targetPos.y + offset.y;
            else desiredPosition.y = transform.position.y; // Keep current camera Y
            
            if (followZ) desiredPosition.z = targetPos.z + offset.z;

            // Apply clamping if enabled to prevent showing the void outside the room
            if (clampToLimits)
            {
                if (boundsRenderer == null)
                {
                    FindBoundsRenderer();
                }

                Camera cam = GetComponent<Camera>();
                if (cam != null && cam.orthographic)
                {
                    float camHalfHeight = cam.orthographicSize;
                    float camHalfWidth = camHalfHeight * cam.aspect;

                    float bgHalfWidth = fallbackHalfWidth;
                    float bgHalfHeight = fallbackHalfHeight;
                    Vector3 bgCenter = Vector3.zero;

                    if (boundsRenderer != null)
                    {
                        bgCenter = boundsRenderer.transform.position;

                        if (boundsRenderer.sprite != null)
                        {
                            float spriteWidth = boundsRenderer.sprite.rect.width / boundsRenderer.sprite.pixelsPerUnit;
                            float spriteHeight = boundsRenderer.sprite.rect.height / boundsRenderer.sprite.pixelsPerUnit;
                            bgHalfWidth = spriteWidth * boundsRenderer.transform.lossyScale.x * 0.5f;
                            bgHalfHeight = spriteHeight * boundsRenderer.transform.lossyScale.y * 0.5f;
                        }
                        else
                        {
                            bgHalfWidth = boundsRenderer.transform.lossyScale.x * 0.5f;
                            bgHalfHeight = boundsRenderer.transform.lossyScale.y * 0.5f;
                        }
                    }

                    // The limit determines how far the camera center can travel
                    // before its edges cross the background boundaries.
                    float limitX = Mathf.Max(0f, bgHalfWidth - camHalfWidth);
                    float limitZ = Mathf.Max(0f, bgHalfHeight - camHalfHeight);

                    if (followZ)
                    {
                        desiredPosition.x = Mathf.Clamp(desiredPosition.x, bgCenter.x - limitX, bgCenter.x + limitX);
                        desiredPosition.z = Mathf.Clamp(desiredPosition.z, bgCenter.z - limitZ, bgCenter.z + limitZ);
                    }
                    else if (followY)
                    {
                        desiredPosition.x = Mathf.Clamp(desiredPosition.x, bgCenter.x - limitX, bgCenter.x + limitX);
                        desiredPosition.y = Mathf.Clamp(desiredPosition.y, bgCenter.y - limitZ, bgCenter.y + limitZ);
                    }
                }
            }

            // Smoothly interpolate to target position
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, smoothTime);
        }
    }
}
