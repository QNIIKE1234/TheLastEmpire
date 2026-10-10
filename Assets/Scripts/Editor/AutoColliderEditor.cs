using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    [CustomEditor(typeof(AutoCollider))]
    [CanEditMultipleObjects]
    public class AutoColliderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            EditorGUILayout.Space(12);

            AutoCollider autoCol = (AutoCollider)target;

            // Display bounding box info
            Bounds b = autoCol.CalculateAccurateLocalBounds();
            if (b.size != Vector3.zero)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Calculated Local Bounds Info", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Local Center: ({b.center.x:F2}, {b.center.y:F2}, {b.center.z:F2})");
                    EditorGUILayout.LabelField($"Local Size:   ({b.size.x:F2}, {b.size.y:F2}, {b.size.z:F2})");
                }
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f);
            if (GUILayout.Button("Fit / Generate Collider", GUILayout.Height(36)))
            {
                foreach (Object obj in targets)
                {
                    if (obj is AutoCollider col)
                    {
                        Undo.RecordObject(col.gameObject, "Generate Collider");
                        col.GenerateColliders();
                        EditorUtility.SetDirty(col.gameObject);
                    }
                }
            }

            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
            if (GUILayout.Button("Remove Colliders", GUILayout.Height(36), GUILayout.Width(130)))
            {
                foreach (Object obj in targets)
                {
                    if (obj is AutoCollider col)
                    {
                        Undo.RecordObject(col.gameObject, "Remove Colliders");
                        col.RemoveGeneratedColliders();
                        EditorUtility.SetDirty(col.gameObject);
                    }
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        // ==========================================
        // Hierarchy Context Menus (Right-click in Scene / Hierarchy)
        // ==========================================
        [MenuItem("GameObject/TheLastEmpire/Add Auto Box Collider", false, 10)]
        private static void AddBoxColliderToSelection()
        {
            ApplyAutoColliderToSelection(AutoCollider.ColliderType.Box);
        }

        [MenuItem("GameObject/TheLastEmpire/Add Auto Capsule Collider", false, 11)]
        private static void AddCapsuleColliderToSelection()
        {
            ApplyAutoColliderToSelection(AutoCollider.ColliderType.Capsule);
        }

        [MenuItem("GameObject/TheLastEmpire/Add Auto Mesh Collider(s)", false, 12)]
        private static void AddMeshColliderToSelection()
        {
            ApplyAutoColliderToSelection(AutoCollider.ColliderType.PerChildMesh);
        }

        private static void ApplyAutoColliderToSelection(AutoCollider.ColliderType type)
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                Undo.RecordObject(go, "Add Auto Collider");
                AutoCollider ac = go.GetComponent<AutoCollider>();
                if (ac == null) ac = go.AddComponent<AutoCollider>();

                SerializedObject so = new SerializedObject(ac);
                so.FindProperty("colliderType").enumValueIndex = (int)type;
                so.ApplyModifiedProperties();

                ac.GenerateColliders();
                EditorUtility.SetDirty(go);
            }
        }

        // ==========================================
        // Project Window Context Menus (Right-click on Prefabs)
        // ==========================================
        [MenuItem("Assets/TheLastEmpire/Auto Add Box Collider to Prefab(s)", false, 30)]
        private static void AddBoxColliderToPrefabs()
        {
            BatchProcessPrefabs(AutoCollider.ColliderType.Box);
        }

        [MenuItem("Assets/TheLastEmpire/Auto Add Capsule Collider to Prefab(s)", false, 31)]
        private static void AddCapsuleColliderToPrefabs()
        {
            BatchProcessPrefabs(AutoCollider.ColliderType.Capsule);
        }

        [MenuItem("Assets/TheLastEmpire/Auto Add Mesh Collider to Prefab(s)", false, 32)]
        private static void AddMeshColliderToPrefabs()
        {
            BatchProcessPrefabs(AutoCollider.ColliderType.PerChildMesh);
        }

        [MenuItem("Assets/TheLastEmpire/Auto Add Box Collider to Prefab(s)", true)]
        [MenuItem("Assets/TheLastEmpire/Auto Add Capsule Collider to Prefab(s)", true)]
        [MenuItem("Assets/TheLastEmpire/Auto Add Mesh Collider to Prefab(s)", true)]
        private static bool ValidatePrefabContext()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        private static void BatchProcessPrefabs(AutoCollider.ColliderType type)
        {
            int modifiedCount = 0;
            List<string> processedPaths = new List<string>();

            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                // Handle folders or prefabs
                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                    foreach (string guid in prefabGuids)
                    {
                        string pPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (ProcessSinglePrefab(pPath, type))
                        {
                            modifiedCount++;
                            processedPaths.Add(pPath);
                        }
                    }
                }
                else if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (ProcessSinglePrefab(path, type))
                    {
                        modifiedCount++;
                        processedPaths.Add(path);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FF88>[AutoCollider]</color> Successfully fitted <b>{type}</b> colliders to <b>{modifiedCount}</b> Prefab(s)!");
            EditorUtility.DisplayDialog("Auto Collider Complete",
                $"Successfully attached {type} Collider(s) to {modifiedCount} Prefab(s)!", "OK");
        }

        private static bool ProcessSinglePrefab(string prefabPath, AutoCollider.ColliderType type)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return false;

            try
            {
                AutoCollider ac = prefabRoot.GetComponent<AutoCollider>();
                if (ac == null) ac = prefabRoot.AddComponent<AutoCollider>();

                SerializedObject so = new SerializedObject(ac);
                so.FindProperty("colliderType").enumValueIndex = (int)type;
                so.ApplyModifiedProperties();

                ac.GenerateColliders();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
