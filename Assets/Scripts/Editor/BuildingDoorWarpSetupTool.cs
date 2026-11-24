#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    /// <summary>
    /// Unity Editor helper to automatically attach or update a 1.5-unit-deep DoorWarpTrigger
    /// (BoxCollider isTrigger=true + BuildingDoorWarpTrigger) on building prefabs.
    /// </summary>
    public static class BuildingDoorWarpSetupTool
    {
        private const float DoorWidth = 2.2f;
        private const float DoorHeight = 2.4f;
        private const float DoorDepth = 1.5f;

        [MenuItem("Tools/TheLastEmpire/Building Doors/Auto-Setup Door Triggers (1.5u) on All Buildings")]
        public static void SetupAllBuildingPrefabs()
        {
            string folder = "Assets/Resources_Addressable/Prefabs/Environment/Building";
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            int updated = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!IsEnterableBuildingPrefabName(fileName)) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    if (EnsureDoorTriggerOnRoot(root))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        updated++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuildingDoorWarpSetupTool] Updated {updated} building prefab(s) with 1.5u DoorWarpTrigger.");
        }

        [MenuItem("Tools/TheLastEmpire/Building Doors/Add 1.5u Door Trigger to Selected GameObject")]
        public static void AddToSelectedGameObject()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[BuildingDoorWarpSetupTool] Please select a building GameObject first.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(selected, "Add Building DoorWarpTrigger");
            EnsureDoorTriggerOnRoot(selected);
            EditorUtility.SetDirty(selected);
        }

        private static bool IsEnterableBuildingPrefabName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Apoc_Building") ||
                   name.StartsWith("Apoc_House") ||
                   name.StartsWith("Building_") ||
                   name.StartsWith("House_Suburban") ||
                   name.StartsWith("Landmark_Warehouse") ||
                   name.StartsWith("Landmark_Diner") ||
                   name.StartsWith("Landmark_Brownstone");
        }

        private static bool EnsureDoorTriggerOnRoot(GameObject root)
        {
            BoxCollider rootCol = root.GetComponent<BoxCollider>();
            Vector3 center = rootCol != null ? rootCol.center : new Vector3(0f, 2f, 0f);
            Vector3 size = rootCol != null ? rootCol.size : new Vector3(5f, 4f, 5f);

            Transform existing = root.transform.Find("DoorWarpTrigger");
            GameObject doorObj;
            if (existing != null)
            {
                doorObj = existing.gameObject;
            }
            else
            {
                doorObj = new GameObject("DoorWarpTrigger");
                doorObj.transform.SetParent(root.transform, false);
            }

            float frontZ = center.z + size.z * 0.5f;
            doorObj.transform.localPosition = new Vector3(center.x, DoorHeight * 0.5f, frontZ + 0.55f);
            doorObj.transform.localRotation = Quaternion.identity;
            doorObj.transform.localScale = Vector3.one;

            BoxCollider triggerBox = doorObj.GetComponent<BoxCollider>();
            if (triggerBox == null) triggerBox = doorObj.AddComponent<BoxCollider>();
            triggerBox.isTrigger = true;
            triggerBox.center = Vector3.zero;
            triggerBox.size = new Vector3(DoorWidth, DoorHeight, DoorDepth);

            if (doorObj.GetComponent<BuildingDoorWarpTrigger>() == null)
            {
                doorObj.AddComponent<BuildingDoorWarpTrigger>();
            }

            return true;
        }
    }
}
#endif
