using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastEmpire
{
    /// <summary>
    /// Adds a crisp cartoon/anime black ink silhouette outline to any rigged (SkinnedMeshRenderer)
    /// or static (MeshRenderer) 3D character without replacing any existing materials.
    /// Automatically bakes position-welded smooth averaged normals into UV3 (cached per Mesh)
    /// so low-poly/hard-edge character meshes have zero cracks in their outline.
    /// </summary>
    [ExecuteAlways]
    public class CharacterToonOutline : MonoBehaviour
    {
        public static bool GlobalEnabled = true;

        [Header("Ink Outline Style")]
        [SerializeField] private Color outlineColor = new Color(0.04f, 0.03f, 0.05f, 1f);
        [SerializeField, Range(0.002f, 0.06f)] private float outlineWidth = 0.010f;
        [SerializeField, Range(0.0f, 0.012f)] private float screenSpaceScale = 0.0016f;
        [SerializeField, Range(0.002f, 0.04f)] private float minWorldWidth = 0.006f;
        [SerializeField, Range(0.01f, 0.08f)] private float maxWorldWidth = 0.026f;
        [SerializeField, Range(0.0f, 0.006f)] private float depthBias = 0.0006f;

        [Header("Scope")]
        [Tooltip("Only outline static MeshRenderers if enabled (off by default to prevent outlining hidden collider capsules/aura meshes).")]
        [SerializeField] private bool includeStaticAccessories = false;

        private const string ShellPrefix = "_ToonOutlineShell_";

        private static readonly Dictionary<int, Mesh> s_SmoothMeshCache = new Dictionary<int, Mesh>();
        private static Shader s_CachedShader;

        private Material _instanceMaterial;
        private readonly List<GameObject> _shells = new List<GameObject>();
        private bool _built = false;

        /// <summary>
        /// Convenience helper to attach or refresh the cartoon outline on any spawned/swapped character model.
        /// </summary>
        public static CharacterToonOutline AttachOrRefresh(GameObject targetRoot, float width = 0.010f)
        {
            if (targetRoot == null || !GlobalEnabled) return null;
            CharacterToonOutline comp = targetRoot.GetComponent<CharacterToonOutline>();
            if (comp == null)
            {
                comp = targetRoot.AddComponent<CharacterToonOutline>();
                comp.outlineWidth = width;
            }
            comp.RebuildOutline();
            return comp;
        }

        private void OnEnable()
        {
            if (GlobalEnabled)
            {
                RebuildOutline();
            }
        }

        private void OnDisable()
        {
            CleanupShells();
        }

        private void OnDestroy()
        {
            CleanupShells();
            if (_instanceMaterial != null)
            {
                if (Application.isPlaying) Destroy(_instanceMaterial);
                else DestroyImmediate(_instanceMaterial);
                _instanceMaterial = null;
            }
        }

        private void OnValidate()
        {
            ApplyMaterialProperties();
        }

        [ContextMenu("Rebuild Toon Outline")]
        public void RebuildOutline()
        {
            CleanupShells();
            if (!isActiveAndEnabled || !GlobalEnabled) return;

            Material mat = EnsureMaterial();
            if (mat == null) return;

            // 1. Outline all SkinnedMeshRenderers (head, hair, body, clothes, limbs)
            SkinnedMeshRenderer[] skinnedList = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedList.Length; i++)
            {
                SkinnedMeshRenderer src = skinnedList[i];
                if (src == null || src.sharedMesh == null) continue;
                if (src.gameObject.name.StartsWith(ShellPrefix)) continue;
                if (ShouldSkipRenderer(src)) continue;

                Mesh smoothMesh = GetOrCreateSmoothNormalMesh(src.sharedMesh);
                if (smoothMesh == null) continue;

                GameObject shellObj = new GameObject(ShellPrefix + src.gameObject.name);
                shellObj.transform.SetParent(src.transform, false);
                shellObj.transform.localPosition = Vector3.zero;
                shellObj.transform.localRotation = Quaternion.identity;
                shellObj.transform.localScale = Vector3.one;
                shellObj.layer = src.gameObject.layer;

                if (!Application.isPlaying)
                {
                    shellObj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                }

                SkinnedMeshRenderer dst = shellObj.AddComponent<SkinnedMeshRenderer>();
                dst.sharedMesh = smoothMesh;
                dst.bones = src.bones;
                dst.rootBone = src.rootBone;
                dst.localBounds = src.localBounds;
                dst.updateWhenOffscreen = src.updateWhenOffscreen;
                dst.quality = src.quality;
                dst.shadowCastingMode = ShadowCastingMode.Off;
                dst.receiveShadows = false;
                dst.lightProbeUsage = LightProbeUsage.Off;
                dst.reflectionProbeUsage = ReflectionProbeUsage.Off;

                int subCount = Mathf.Max(1, smoothMesh.subMeshCount);
                Material[] mats = new Material[subCount];
                for (int s = 0; s < subCount; s++) mats[s] = mat;
                dst.sharedMaterials = mats;

                _shells.Add(shellObj);
            }

            // 2. Optionally outline static accessory MeshRenderers (hats, backpacks, props worn on bones)
            if (includeStaticAccessories)
            {
                MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    MeshRenderer src = meshRenderers[i];
                    if (src == null) continue;
                    if (src.gameObject.name.StartsWith(ShellPrefix)) continue;
                    if (ShouldSkipRenderer(src)) continue;

                    MeshFilter mf = src.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    Mesh smoothMesh = GetOrCreateSmoothNormalMesh(mf.sharedMesh);
                    if (smoothMesh == null) continue;

                    GameObject shellObj = new GameObject(ShellPrefix + src.gameObject.name);
                    shellObj.transform.SetParent(src.transform, false);
                    shellObj.transform.localPosition = Vector3.zero;
                    shellObj.transform.localRotation = Quaternion.identity;
                    shellObj.transform.localScale = Vector3.one;
                    shellObj.layer = src.gameObject.layer;

                    if (!Application.isPlaying)
                    {
                        shellObj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    }

                    MeshFilter dstMf = shellObj.AddComponent<MeshFilter>();
                    dstMf.sharedMesh = smoothMesh;

                    MeshRenderer dstMr = shellObj.AddComponent<MeshRenderer>();
                    dstMr.shadowCastingMode = ShadowCastingMode.Off;
                    dstMr.receiveShadows = false;
                    dstMr.lightProbeUsage = LightProbeUsage.Off;
                    dstMr.reflectionProbeUsage = ReflectionProbeUsage.Off;

                    int subCount = Mathf.Max(1, smoothMesh.subMeshCount);
                    Material[] mats = new Material[subCount];
                    for (int s = 0; s < subCount; s++) mats[s] = mat;
                    dstMr.sharedMaterials = mats;

                    _shells.Add(shellObj);
                }
            }

            _built = true;
            ApplyMaterialProperties();
        }

        private bool ShouldSkipRenderer(Renderer r)
        {
            if (r == null) return true;
            // CRITICAL: Never outline disabled or inactive renderers (e.g. hidden placeholder capsules/spheres on Player/NPC roots!)
            if (!r.enabled || !r.gameObject.activeInHierarchy) return true;
            // Never outline particles, trails, canvases, shadow quads, or weapon FX
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return true;

            // Skip transparent / overlay / particle materials
            Material[] mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return true;
            bool hasOpaqueMat = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                if (m.renderQueue < 2900)
                {
                    hasOpaqueMat = true;
                    break;
                }
            }
            if (!hasOpaqueMat) return true;

            // Skip Unity primitive placeholder meshes (Capsule/Sphere/Cube/Quad/Plane) on character roots
            Mesh msh = null;
            if (r is SkinnedMeshRenderer smr) msh = smr.sharedMesh;
            else if (r is MeshRenderer)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null) msh = mf.sharedMesh;
            }
            if (msh != null)
            {
                string mn = msh.name;
                if (mn == "Capsule" || mn == "Sphere" || mn == "Cube" || mn == "Cylinder" || mn == "Quad" || mn == "Plane")
                {
                    return true;
                }
            }

            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("shadow") || n.Contains("vfx") || n.Contains("effect") || n.Contains("muzzle") ||
                n.Contains("flash") || n.Contains("trail") || n.Contains("flame") || n.Contains("glow") ||
                n.Contains("ring") || n.Contains("marker") || n.Contains("indicator") || n.Contains("beam") ||
                n.Contains("canvas") || n.Contains("text") || n.Contains("collider") || n.Contains("trigger") ||
                n.Contains("range") || n.Contains("aura") || n.Contains("capsule"))
            {
                return true;
            }

            // Skip weapon holder subtrees (keep crisp outline on character body/head/clothes/accessories)
            Transform cur = r.transform;
            while (cur != null && cur != transform)
            {
                string cn = cur.name.ToLowerInvariant();
                if (cn == "malee" || cn == "melee" || cn == "range" || cn == "ranged" || cn.Contains("vfx") || cn.Contains("canvas"))
                {
                    return true;
                }
                cur = cur.parent;
            }

            return false;
        }

        private Material EnsureMaterial()
        {
            if (_instanceMaterial != null)
            {
                ApplyMaterialProperties();
                return _instanceMaterial;
            }

            // Load pre-referenced material from Resources first so Unity never strips the shader in builds!
            Material template = Resources.Load<Material>("Materials/CharacterToonOutline");
            if (template != null)
            {
                _instanceMaterial = new Material(template);
            }
            else
            {
                if (s_CachedShader == null)
                {
                    s_CachedShader = Shader.Find("TheLastEmpire/CharacterToonOutline");
                }
                if (s_CachedShader == null) return null;
                _instanceMaterial = new Material(s_CachedShader);
            }

            _instanceMaterial.name = "CharacterToonOutline_Inst";
            if (!Application.isPlaying)
            {
                _instanceMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            ApplyMaterialProperties();
            return _instanceMaterial;
        }

        private void ApplyMaterialProperties()
        {
            if (_instanceMaterial == null) return;
            _instanceMaterial.SetColor("_OutlineColor", outlineColor);
            _instanceMaterial.SetFloat("_OutlineWidth", outlineWidth);
            _instanceMaterial.SetFloat("_ScreenSpaceScale", screenSpaceScale);
            _instanceMaterial.SetFloat("_MinWorldWidth", minWorldWidth);
            _instanceMaterial.SetFloat("_MaxWorldWidth", maxWorldWidth);
            _instanceMaterial.SetFloat("_ZBias", depthBias);
        }

        private void CleanupShells()
        {
            for (int i = 0; i < _shells.Count; i++)
            {
                if (_shells[i] != null)
                {
                    if (Application.isPlaying) Destroy(_shells[i]);
                    else DestroyImmediate(_shells[i]);
                }
            }
            _shells.Clear();

            // Also sweep any leftover shell objects from previous Edit Mode reloads
            List<GameObject> leftovers = new List<GameObject>();
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != transform && all[i].name.StartsWith(ShellPrefix))
                {
                    leftovers.Add(all[i].gameObject);
                }
            }
            for (int i = 0; i < leftovers.Count; i++)
            {
                if (leftovers[i] != null)
                {
                    if (Application.isPlaying) Destroy(leftovers[i]);
                    else DestroyImmediate(leftovers[i]);
                }
            }
            _built = false;
        }

        /// <summary>
        /// Clones the mesh ONCE (cached globally by source InstanceID) and writes position-averaged
        /// smooth normals into UV3 (TEXCOORD3) so inverted-hull extrusion never splits at hard edges/UV seams.
        /// </summary>
        private static Mesh GetOrCreateSmoothNormalMesh(Mesh src)
        {
            if (src == null) return null;
            int id = src.GetInstanceID();
            if (s_SmoothMeshCache.TryGetValue(id, out Mesh cached) && cached != null)
            {
                return cached;
            }

            if (!src.isReadable)
            {
                // Non-readable mesh falls back to its own normals in shader gracefully
                return src;
            }

            Mesh clone = Instantiate(src);
            clone.name = src.name + "_SmoothOutline";
            clone.hideFlags = HideFlags.DontSave;

            Vector3[] verts = clone.vertices;
            Vector3[] norms = clone.normals;
            if (verts != null && norms != null && verts.Length == norms.Length && verts.Length > 0)
            {
                // Quantize vertex positions to 0.0005m buckets to weld hard-edge & UV-seam splits
                var accum = new Dictionary<Vector3Int, Vector3>(verts.Length);
                const float invStep = 2000f;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 v = verts[i];
                    Vector3Int key = new Vector3Int(
                        Mathf.RoundToInt(v.x * invStep),
                        Mathf.RoundToInt(v.y * invStep),
                        Mathf.RoundToInt(v.z * invStep));
                    if (accum.TryGetValue(key, out Vector3 sum))
                        accum[key] = sum + norms[i];
                    else
                        accum[key] = norms[i];
                }

                Vector3[] smoothNormals = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 v = verts[i];
                    Vector3Int key = new Vector3Int(
                        Mathf.RoundToInt(v.x * invStep),
                        Mathf.RoundToInt(v.y * invStep),
                        Mathf.RoundToInt(v.z * invStep));
                    Vector3 avg = accum[key];
                    smoothNormals[i] = avg.sqrMagnitude > 1e-6f ? avg.normalized : norms[i];
                }

                // Write welded smooth normals directly into NORMAL of the dedicated outline clone
                // so SkinnedMeshRenderer automatically transforms them with every animated bone!
                clone.normals = smoothNormals;
            }

            s_SmoothMeshCache[id] = clone;
            return clone;
        }
    }
}
