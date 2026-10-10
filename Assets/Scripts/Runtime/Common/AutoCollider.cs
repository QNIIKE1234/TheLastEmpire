using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class AutoCollider : MonoBehaviour
    {
        public enum ColliderType
        {
            Box,
            Capsule,
            PerChildMesh,
            PerChildBox
        }

        public enum CapsuleAxis
        {
            X_Axis = 0,
            Y_Axis = 1,
            Z_Axis = 2
        }

        [Header("Collider Settings")]
        [SerializeField] private ColliderType colliderType = ColliderType.Box;
        [SerializeField] private bool isTrigger = false;
        [SerializeField] private PhysicsMaterial physicMaterial = null;

        [Header("Adjustment & Offset")]
        [SerializeField] private Vector3 sizeMultiplier = Vector3.one;
        [SerializeField] private Vector3 centerOffset = Vector3.zero;

        [Header("Capsule Settings (if Capsule chosen)")]
        [SerializeField] private CapsuleAxis capsuleDirection = CapsuleAxis.Y_Axis;

        [Header("Mesh Collider Settings")]
        [SerializeField] private bool convexMeshCollider = false;

        [Header("Filters")]
        [SerializeField] private bool includeInactive = false;
        [SerializeField] private bool ignoreTriggerColliders = true;

        [Header("Runtime")]
        [SerializeField] private bool fitOnAwake = false;

        public ColliderType Type => colliderType;

        private void Awake()
        {
            if (Application.isPlaying && fitOnAwake)
            {
                GenerateColliders();
            }
        }

        private void Reset()
        {
            GenerateColliders();
        }

        [ContextMenu("Generate / Fit Collider")]
        public void GenerateColliders()
        {
            RemoveGeneratedColliders();

            switch (colliderType)
            {
                case ColliderType.Box:
                    CreateRootBoxCollider();
                    break;

                case ColliderType.Capsule:
                    CreateRootCapsuleCollider();
                    break;

                case ColliderType.PerChildMesh:
                    CreatePerChildMeshColliders();
                    break;

                case ColliderType.PerChildBox:
                    CreatePerChildBoxColliders();
                    break;
            }
        }

        [ContextMenu("Remove Colliders")]
        public void RemoveGeneratedColliders()
        {
            Collider[] existing = GetComponents<Collider>();
            for (int i = existing.Length - 1; i >= 0; i--)
            {
                DestroyObjectSmart(existing[i]);
            }

            // Also check children if per-child mode was used
            if (colliderType == ColliderType.PerChildMesh || colliderType == ColliderType.PerChildBox)
            {
                Collider[] childColliders = GetComponentsInChildren<Collider>(true);
                foreach (Collider col in childColliders)
                {
                    if (col.gameObject != gameObject)
                    {
                        DestroyObjectSmart(col);
                    }
                }
            }
        }

        private void CreateRootBoxCollider()
        {
            Bounds localBounds = CalculateAccurateLocalBounds();
            if (localBounds.size == Vector3.zero)
            {
                Debug.LogWarning($"[AutoCollider] No valid renderers found on '{name}' or its children to calculate bounds.");
                return;
            }

            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = localBounds.center + centerOffset;
            bc.size = Vector3.Scale(localBounds.size, sizeMultiplier);
            bc.isTrigger = isTrigger;
            bc.sharedMaterial = physicMaterial;
        }

        private void CreateRootCapsuleCollider()
        {
            Bounds localBounds = CalculateAccurateLocalBounds();
            if (localBounds.size == Vector3.zero)
            {
                Debug.LogWarning($"[AutoCollider] No valid renderers found on '{name}' or its children to calculate bounds.");
                return;
            }

            CapsuleCollider cc = gameObject.AddComponent<CapsuleCollider>();
            cc.direction = (int)capsuleDirection;
            cc.center = localBounds.center + centerOffset;

            Vector3 adjustedSize = Vector3.Scale(localBounds.size, sizeMultiplier);

            switch (capsuleDirection)
            {
                case CapsuleAxis.X_Axis:
                    cc.height = adjustedSize.x;
                    cc.radius = Mathf.Max(adjustedSize.y, adjustedSize.z) * 0.5f;
                    break;
                case CapsuleAxis.Y_Axis:
                    cc.height = adjustedSize.y;
                    cc.radius = Mathf.Max(adjustedSize.x, adjustedSize.z) * 0.5f;
                    break;
                case CapsuleAxis.Z_Axis:
                    cc.height = adjustedSize.z;
                    cc.radius = Mathf.Max(adjustedSize.x, adjustedSize.y) * 0.5f;
                    break;
            }

            cc.isTrigger = isTrigger;
            cc.sharedMaterial = physicMaterial;
        }

        private void CreatePerChildMeshColliders()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactive);
            int addedCount = 0;

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();

                mc.sharedMesh = mf.sharedMesh;
                mc.convex = convexMeshCollider;
                mc.isTrigger = isTrigger;
                mc.sharedMaterial = physicMaterial;
                addedCount++;
            }

            if (addedCount == 0)
            {
                // Fallback to Box collider if no mesh filters found
                CreateRootBoxCollider();
            }
        }

        private void CreatePerChildBoxColliders()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactive);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                BoxCollider bc = mf.gameObject.GetComponent<BoxCollider>();
                if (bc == null) bc = mf.gameObject.AddComponent<BoxCollider>();

                Bounds b = mf.sharedMesh.bounds;
                bc.center = b.center + centerOffset;
                bc.size = Vector3.Scale(b.size, sizeMultiplier);
                bc.isTrigger = isTrigger;
                bc.sharedMaterial = physicMaterial;
            }
        }

        /// <summary>
        /// Calculates accurate bounds in the local coordinate space of this GameObject.
        /// This eliminates distortions caused by root or child rotations that inflate world AABB.
        /// </summary>
        public Bounds CalculateAccurateLocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

            Bounds localBounds = new Bounds();
            bool hasValidBounds = false;

            Transform rootTransform = transform;

            foreach (Renderer r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;

                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // Most accurate: transform mesh local bounding box corners into root's local space
                    Mesh mesh = mf.sharedMesh;
                    Bounds meshBounds = mesh.bounds;
                    Transform childTransform = mf.transform;

                    Vector3[] corners = GetBoundsCorners(meshBounds);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 worldCorner = childTransform.TransformPoint(corners[i]);
                        Vector3 rootLocalCorner = rootTransform.InverseTransformPoint(worldCorner);

                        if (!hasValidBounds)
                        {
                            localBounds = new Bounds(rootLocalCorner, Vector3.zero);
                            hasValidBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(rootLocalCorner);
                        }
                    }
                }
                else
                {
                    // Fallback using Renderer world bounds corners
                    Bounds worldBounds = r.bounds;
                    Vector3[] corners = GetBoundsCorners(worldBounds);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 rootLocalCorner = rootTransform.InverseTransformPoint(corners[i]);
                        if (!hasValidBounds)
                        {
                            localBounds = new Bounds(rootLocalCorner, Vector3.zero);
                            hasValidBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(rootLocalCorner);
                        }
                    }
                }
            }

            return localBounds;
        }

        private static Vector3[] GetBoundsCorners(Bounds b)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;

            return new Vector3[8]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private static void DestroyObjectSmart(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
