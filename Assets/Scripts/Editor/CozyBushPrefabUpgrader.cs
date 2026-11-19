#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    /// <summary>
    /// Upgrades legacy bush prefabs to the Cozy Low-Poly FBX bush set, creates prefabs
    /// for all 5 Cozy Bushes and all 9 Cozy Trees in Prefabs/Environment/Building,
    /// and ensures they are registered in Addressables (Environment_Group).
    /// </summary>
    public static class CozyBushPrefabUpgrader
    {
        private const string PrefabDir = "Assets/Resources_Addressable/Prefabs/Environment/Building";
        private const string FbxDir = "Assets/GeneratedEnvironment";
        private const string AddressableGroupName = "Environment_Group";

        private enum ColliderMode
        {
            None,
            FitMeshBounds,
            TrunkBox,
        }

        private struct AssetMapping
        {
            public string PrefabName;
            public string FbxName;
            public ColliderMode Collider;
            public Vector3 TrunkSize;
        }

        private static readonly AssetMapping[] Mappings =
        {
            // 5 Cozy Low-Poly Bushes
            new AssetMapping { PrefabName = "Prop_BushRound_New", FbxName = "Cozy_Bush_01_RoundCluster", Collider = ColliderMode.None },
            new AssetMapping { PrefabName = "Prop_BushBerry_New", FbxName = "Cozy_Bush_02_BerryShrub", Collider = ColliderMode.None },
            new AssetMapping { PrefabName = "Prop_BushFlowering_New", FbxName = "Cozy_Bush_03_FloweringBush", Collider = ColliderMode.None },
            new AssetMapping { PrefabName = "Prop_BushHedge_New", FbxName = "Cozy_Bush_04_LongHedge", Collider = ColliderMode.FitMeshBounds },
            new AssetMapping { PrefabName = "Prop_BushAutumnRock_New", FbxName = "Cozy_Bush_05_AutumnRockBush", Collider = ColliderMode.None },

            // 9 Cozy Low-Poly Trees (3 Large, 3 Medium, 3 Small)
            new AssetMapping { PrefabName = "Cozy_Tree_Large_01_GiantOak", FbxName = "Cozy_Tree_Large_01_GiantOak", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.80f, 2.8f, 0.80f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Large_02_TallSpruce", FbxName = "Cozy_Tree_Large_02_TallSpruce", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.70f, 2.8f, 0.70f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Large_03_AutumnMaple", FbxName = "Cozy_Tree_Large_03_AutumnMaple", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.75f, 2.8f, 0.75f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Medium_01_MeadowOak", FbxName = "Cozy_Tree_Medium_01_MeadowOak", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.55f, 2.1f, 0.55f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Medium_02_ForestPine", FbxName = "Cozy_Tree_Medium_02_ForestPine", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.52f, 2.1f, 0.52f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Medium_03_GoldenBirch", FbxName = "Cozy_Tree_Medium_03_GoldenBirch", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.46f, 2.1f, 0.46f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Small_01_RoundSapling", FbxName = "Cozy_Tree_Small_01_RoundSapling", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.38f, 1.4f, 0.38f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Small_02_BabyPine", FbxName = "Cozy_Tree_Small_02_BabyPine", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.38f, 1.4f, 0.38f) },
            new AssetMapping { PrefabName = "Cozy_Tree_Small_03_AutumnMini", FbxName = "Cozy_Tree_Small_03_AutumnMini", Collider = ColliderMode.TrunkBox, TrunkSize = new Vector3(0.38f, 1.4f, 0.38f) },
        };

        [InitializeOnLoadMethod]
        private static void AutoUpgradeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                UpgradeAllFoliagePrefabs(silentIfAlreadyUpgraded: true);
            };
        }

        [MenuItem("Tools/TheLastEmpire/Upgrade Bush & Tree Prefabs (Cozy Low-Poly)")]
        public static void UpgradeAllFoliagePrefabsMenu()
        {
            UpgradeAllFoliagePrefabs(silentIfAlreadyUpgraded: false);
        }

        public static void UpgradeAllFoliagePrefabs(bool silentIfAlreadyUpgraded)
        {
            int updatedPrefabs = 0;
            int registeredAddressables = 0;

            AddressableAssetSettings addrSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup envGroup = addrSettings != null ? addrSettings.FindGroup(AddressableGroupName) : null;

            foreach (AssetMapping mapping in Mappings)
            {
                string fbxPath = $"{FbxDir}/{mapping.FbxName}.fbx";
                GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxRoot == null)
                {
                    continue;
                }

                MeshFilter fbxFilter = fbxRoot.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer fbxRenderer = fbxRoot.GetComponentInChildren<MeshRenderer>(true);
                if (fbxFilter == null || fbxFilter.sharedMesh == null || fbxRenderer == null)
                {
                    continue;
                }

                Material[] fbxMats = fbxRenderer.sharedMaterials;
                if (fbxMats != null)
                {
                    foreach (Material mat in fbxMats)
                    {
                        if (mat != null)
                        {
                            ChibiTacticalGirlMaterialPostprocessor.ApplyCraftBuildingMatteSettings(mat);
                            EditorUtility.SetDirty(mat);
                        }
                    }
                }

                string prefabPath = $"{PrefabDir}/{mapping.PrefabName}.prefab";
                bool exists = File.Exists(prefabPath);

                if (exists)
                {
                    GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (existingPrefab == null || !AlreadyUsesMesh(existingPrefab, fbxFilter.sharedMesh))
                    {
                        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                        try
                        {
                            for (int i = contents.transform.childCount - 1; i >= 0; i--)
                            {
                                Object.DestroyImmediate(contents.transform.GetChild(i).gameObject);
                            }

                            AttachVisualChild(contents, mapping.FbxName, fbxFilter.sharedMesh, fbxMats);
                            ConfigureCollider(contents, mapping, fbxFilter.sharedMesh);

                            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                            updatedPrefabs++;
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(contents);
                        }
                    }
                }
                else
                {
                    GameObject tempRoot = new GameObject(mapping.PrefabName);
                    try
                    {
                        AttachVisualChild(tempRoot, mapping.FbxName, fbxFilter.sharedMesh, fbxMats);
                        ConfigureCollider(tempRoot, mapping, fbxFilter.sharedMesh);

                        PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
                        updatedPrefabs++;
                    }
                    finally
                    {
                        Object.DestroyImmediate(tempRoot);
                    }
                }

                if (addrSettings != null && envGroup != null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        string expectedAddress = $"Prefabs/Environment/Building/{mapping.PrefabName}";
                        AddressableAssetEntry entry = addrSettings.FindAssetEntry(guid);
                        if (entry == null || entry.address != expectedAddress || entry.parentGroup != envGroup)
                        {
                            entry = addrSettings.CreateOrMoveEntry(guid, envGroup, readOnly: false, postEvent: false);
                            entry.address = expectedAddress;
                            entry.SetLabel("Building", true, true, false);
                            entry.SetLabel("Environment", true, true, false);
                            registeredAddressables++;
                        }
                    }
                }
            }

            if (addrSettings != null && envGroup != null)
            {
                string lanternPath = "Assets/Resources_Addressable/Prefabs/Environment/Craft/23_Wooden_Lantern_Post.prefab";
                string lanternGuid = AssetDatabase.AssetPathToGUID(lanternPath);
                if (!string.IsNullOrEmpty(lanternGuid))
                {
                    string expectedAddress = "Prefabs/Environment/Craft/23_Wooden_Lantern_Post";
                    AddressableAssetEntry entry = addrSettings.FindAssetEntry(lanternGuid);
                    if (entry == null || entry.address != expectedAddress || entry.parentGroup != envGroup)
                    {
                        entry = addrSettings.CreateOrMoveEntry(lanternGuid, envGroup, readOnly: false, postEvent: false);
                        entry.address = expectedAddress;
                        entry.SetLabel("Building", true, true, false);
                        entry.SetLabel("Craft", true, true, false);
                        entry.SetLabel("Environment", true, true, false);
                        registeredAddressables++;
                    }
                }
            }

            if (updatedPrefabs > 0 || registeredAddressables > 0)
            {
                if (addrSettings != null)
                {
                    EditorUtility.SetDirty(addrSettings);
                    if (envGroup != null) EditorUtility.SetDirty(envGroup);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[CozyBushPrefabUpgrader] Updated/created {updatedPrefabs} Cozy Low-Poly foliage prefabs and registered {registeredAddressables} Addressables entries.");
            }
            else if (!silentIfAlreadyUpgraded)
            {
                Debug.Log("[CozyBushPrefabUpgrader] All 5 Cozy Bushes and 9 Cozy Trees are already up to date.");
            }
        }

        private static bool AlreadyUsesMesh(GameObject prefabRoot, Mesh expectedMesh)
        {
            MeshFilter[] filters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
            return filters.Length == 1 && filters[0].sharedMesh == expectedMesh;
        }

        private static void ConfigureCollider(GameObject root, AssetMapping mapping, Mesh mesh)
        {
            if (mapping.Collider == ColliderMode.None)
            {
                return;
            }

            BoxCollider col = root.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = root.AddComponent<BoxCollider>();
            }

            if (mapping.Collider == ColliderMode.FitMeshBounds)
            {
                Bounds b = mesh.bounds;
                col.center = b.center;
                col.size = b.size;
            }
            else if (mapping.Collider == ColliderMode.TrunkBox)
            {
                col.size = mapping.TrunkSize;
                col.center = new Vector3(0f, mapping.TrunkSize.y * 0.5f, 0f);
            }
        }

        private static void AttachVisualChild(GameObject parent, string childName, Mesh mesh, Material[] materials)
        {
            GameObject visual = new GameObject(childName);
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            MeshFilter mf = visual.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
        }
    }
}
#endif
