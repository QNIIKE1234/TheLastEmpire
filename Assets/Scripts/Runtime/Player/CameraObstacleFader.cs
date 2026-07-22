using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class CameraObstacleFader : MonoBehaviour
    {
        [Header("Fading Settings")]
        [SerializeField] private float targetAlpha = 0.25f; // Fade down to 25% opacity
        [SerializeField] private float fadeSpeed = 5f;       // Transition speed
        [SerializeField] private LayerMask checkLayers = ~0; // Cast against all layers

        private Transform _playerTransform;
        private Camera _camera;
        private Dictionary<Renderer, FadedObject> _fadedObjects = new Dictionary<Renderer, FadedObject>();

        private void Start()
        {
            _camera = GetComponent<Camera>();
            FindPlayer();
        }

        private void FindPlayer()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            // Raycast from camera to player
            Vector3 camPos = transform.position;
            Vector3 playerPos = _playerTransform.position + new Vector3(0f, 0.8f, 0f); // Target torso level
            Vector3 dir = playerPos - camPos;
            float distance = dir.magnitude;

            // Reset targeted state on all registered objects
            foreach (var pair in _fadedObjects)
            {
                if (pair.Value != null)
                {
                    pair.Value.isTargeted = false;
                }
            }

            // Perform raycast
            RaycastHit[] hits = Physics.RaycastAll(camPos, dir.normalized, distance, checkLayers);
            foreach (RaycastHit hit in hits)
            {
                // Skip player, triggers, and ground/water
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Ground")) continue;

                // Find all renderers in the hit object or its parent/children
                Transform rootHit = hit.transform;
                
                // If it is part of procedural generation, find the high-level parent under GeneratedEnvironment
                if (rootHit.parent != null && rootHit.parent.name == "GeneratedEnvironment")
                {
                    // Keep rootHit as is
                }
                else if (rootHit.parent != null && rootHit.parent.parent != null && rootHit.parent.parent.name == "GeneratedEnvironment")
                {
                    rootHit = rootHit.parent;
                }

                Renderer[] renderers = rootHit.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer rend in renderers)
                {
                    if (rend == null || !rend.enabled) continue;
                    
                    // Register if not already tracked
                    if (!_fadedObjects.ContainsKey(rend))
                    {
                        _fadedObjects[rend] = new FadedObject(rend);
                    }
                    _fadedObjects[rend].isTargeted = true;
                }
            }

            // Clean up and update fade values
            List<Renderer> toRemove = new List<Renderer>();
            List<Renderer> activeRenderers = new List<Renderer>(_fadedObjects.Keys);

            foreach (Renderer rend in activeRenderers)
            {
                FadedObject faded = _fadedObjects[rend];
                if (rend == null || faded == null)
                {
                    toRemove.Add(rend);
                    continue;
                }

                // Interpolate alpha
                float target = faded.isTargeted ? targetAlpha : 1.0f;
                float newAlpha = Mathf.MoveTowards(faded.currentAlpha, target, Time.deltaTime * fadeSpeed);

                if (Mathf.Abs(faded.currentAlpha - newAlpha) > 0.001f)
                {
                    faded.ApplyAlpha(newAlpha);
                }
                else if (!faded.isTargeted && Mathf.Abs(faded.currentAlpha - 1.0f) < 0.01f)
                {
                    // Fully restored to opaque, clean up tracking
                    faded.ApplyAlpha(1.0f);
                    toRemove.Add(rend);
                }
            }

            // Remove untracked objects
            foreach (Renderer rend in toRemove)
            {
                _fadedObjects.Remove(rend);
            }
        }

        private void OnDestroy()
        {
            // Restore everything on destroy
            foreach (var pair in _fadedObjects)
            {
                if (pair.Value != null)
                {
                    pair.Value.CleanUp();
                }
            }
            _fadedObjects.Clear();
        }

        private class FadedObject
        {
            public Renderer renderer;
            public List<Material> materials = new List<Material>();
            public List<Color> originalColors = new List<Color>();
            public List<float> originalSurfaces = new List<float>();
            public List<int> originalSrcBlends = new List<int>();
            public List<int> originalDstBlends = new List<int>();
            public List<int> originalZWrites = new List<int>();
            public List<int> originalQueues = new List<int>();
            public List<string[]> originalKeywords = new List<string[]>();

            public float currentAlpha = 1.0f;
            public bool isTargeted = false;

            public FadedObject(Renderer rend)
            {
                renderer = rend;
                foreach (Material mat in rend.materials)
                {
                    materials.Add(mat);
                    originalColors.Add(mat.HasProperty("_Color") ? mat.color : (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white));
                    originalSurfaces.Add(mat.HasProperty("_Surface") ? mat.GetFloat("_Surface") : 0f);
                    originalSrcBlends.Add(mat.HasProperty("_SrcBlend") ? mat.GetInt("_SrcBlend") : (int)UnityEngine.Rendering.BlendMode.One);
                    originalDstBlends.Add(mat.HasProperty("_DstBlend") ? mat.GetInt("_DstBlend") : (int)UnityEngine.Rendering.BlendMode.Zero);
                    originalZWrites.Add(mat.HasProperty("_ZWrite") ? mat.GetInt("_ZWrite") : 1);
                    originalQueues.Add(mat.renderQueue);
                    originalKeywords.Add(mat.shaderKeywords);
                }
            }

            public void ApplyAlpha(float alpha)
            {
                currentAlpha = alpha;
                for (int i = 0; i < materials.Count; i++)
                {
                    Material mat = materials[i];
                    if (mat == null) continue;

                    if (alpha < 0.99f)
                    {
                        mat.SetFloat("_Surface", 1); // Transparent
                        mat.SetFloat("_Blend", 0); // Alpha blend
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 100;
                    }
                    else
                    {
                        mat.SetFloat("_Surface", originalSurfaces[i]);
                        mat.SetInt("_SrcBlend", originalSrcBlends[i]);
                        mat.SetInt("_DstBlend", originalDstBlends[i]);
                        mat.SetInt("_ZWrite", originalZWrites[i]);
                        mat.renderQueue = originalQueues[i];
                        
                        foreach (string kw in originalKeywords[i])
                        {
                            mat.EnableKeyword(kw);
                        }
                        if (originalSurfaces[i] == 0f)
                        {
                            mat.DisableKeyword("_ALPHABLEND_ON");
                        }
                    }

                    Color col = originalColors[i];
                    col.a = originalColors[i].a * alpha;
                    if (mat.HasProperty("_Color"))
                    {
                        mat.color = col;
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", col);
                    }
                }
            }

            public void CleanUp()
            {
                ApplyAlpha(1.0f);
            }
        }
    }
}
