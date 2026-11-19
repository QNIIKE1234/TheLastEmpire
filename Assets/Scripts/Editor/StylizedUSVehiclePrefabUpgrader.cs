#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    /// <summary>
    /// Upgrades all 9 existing vehicle prefabs in Prefabs/Environment to the Stylized American
    /// FBX vehicle set, creates the 4 new American emergency/city vehicles (NYC Taxi, Police SUV,
    /// Fire Truck, Police Motorcycle), and registers them in Addressables (Environment_Group).
    /// </summary>
    public static class StylizedUSVehiclePrefabUpgrader
    {
        private const string PrefabDir = "Assets/Resources_Addressable/Prefabs/Environment";
        private const string FbxDir = "Assets/GeneratedEnvironment";
        private const string AddressableGroupName = "Environment_Group";

        private struct VehicleMapping
        {
            public string PrefabName;
            public string FbxName;
        }

        private static readonly VehicleMapping[] Mappings =
        {
            // 9 Existing Vehicles (Remodeled)
            new VehicleMapping { PrefabName = "Vehicle_Sedan", FbxName = "Stylized_Vehicle_Sedan" },
            new VehicleMapping { PrefabName = "Vehicle_Police_Sedan", FbxName = "Stylized_Vehicle_Police_Sedan" },
            new VehicleMapping { PrefabName = "Vehicle_Motorcycle", FbxName = "Stylized_Vehicle_Motorcycle" },
            new VehicleMapping { PrefabName = "Apoc_Vehicle_01_Damaged_Van", FbxName = "Stylized_Apoc_Vehicle_01_Damaged_Van" },
            new VehicleMapping { PrefabName = "Apoc_Vehicle_02_Ruined_Truck", FbxName = "Stylized_Apoc_Vehicle_02_Ruined_Truck" },
            new VehicleMapping { PrefabName = "Apoc_Vehicle_03_Abandoned_Bus", FbxName = "Stylized_Apoc_Vehicle_03_Abandoned_Bus" },
            new VehicleMapping { PrefabName = "Apoc_Motorcycle_01_Rusted_Scrambler", FbxName = "Stylized_Apoc_Motorcycle_01_Rusted_Scrambler" },
            new VehicleMapping { PrefabName = "Apoc_Motorcycle_02_Smashed_Scooter", FbxName = "Stylized_Apoc_Motorcycle_02_Smashed_Scooter" },
            new VehicleMapping { PrefabName = "Apoc_Motorcycle_03_Abandoned_Dirt_Bike", FbxName = "Stylized_Apoc_Motorcycle_03_Abandoned_Dirt_Bike" },

            // 4 Brand-New American Vehicles
            new VehicleMapping { PrefabName = "Vehicle_Taxi_NYC", FbxName = "Stylized_Vehicle_Taxi_NYC" },
            new VehicleMapping { PrefabName = "Vehicle_Police_SUV", FbxName = "Stylized_Vehicle_Police_SUV" },
            new VehicleMapping { PrefabName = "Vehicle_FireTruck", FbxName = "Stylized_Vehicle_FireTruck" },
            new VehicleMapping { PrefabName = "Vehicle_Police_Motorcycle", FbxName = "Stylized_Vehicle_Police_Motorcycle" },
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

                UpgradeAllVehicles(silentIfAlreadyUpgraded: true);
            };
        }

        [MenuItem("Tools/TheLastEmpire/Upgrade & Add Stylized American Vehicles")]
        public static void UpgradeAllVehiclesMenu()
        {
            UpgradeAllVehicles(silentIfAlreadyUpgraded: false);
        }

        public static void UpgradeAllVehicles(bool silentIfAlreadyUpgraded)
        {
            int updatedPrefabs = 0;
            int registeredAddressables = 0;

            AddressableAssetSettings addrSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup envGroup = addrSettings != null ? addrSettings.FindGroup(AddressableGroupName) : null;

            foreach (VehicleMapping mapping in Mappings)
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
                    GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (existing == null || !AlreadyUsesMesh(existing, fbxFilter.sharedMesh))
                    {
                        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                        try
                        {
                            ConfigureVehicleRoot(contents, mapping.FbxName, fbxFilter.sharedMesh, fbxMats);
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
                        ConfigureVehicleRoot(tempRoot, mapping.FbxName, fbxFilter.sharedMesh, fbxMats);
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
                        string expectedAddress = $"Prefabs/Environment/{mapping.PrefabName}";
                        AddressableAssetEntry entry = addrSettings.FindAssetEntry(guid);
                        if (entry == null || entry.address != expectedAddress || entry.parentGroup != envGroup)
                        {
                            entry = addrSettings.CreateOrMoveEntry(guid, envGroup, readOnly: false, postEvent: false);
                            entry.address = expectedAddress;
                            entry.SetLabel("Environment", true, true, false);
                            registeredAddressables++;
                        }
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
                Debug.Log($"[StylizedUSVehiclePrefabUpgrader] Updated/created {updatedPrefabs} stylized American vehicle prefabs and registered {registeredAddressables} Addressables entries.");
            }
            else if (!silentIfAlreadyUpgraded)
            {
                Debug.Log("[StylizedUSVehiclePrefabUpgrader] All 13 stylized American vehicle prefabs are already up to date.");
            }
        }

        private static bool AlreadyUsesMesh(GameObject prefabRoot, Mesh expectedMesh)
        {
            MeshFilter[] filters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
            return filters.Length == 1 && filters[0].sharedMesh == expectedMesh && prefabRoot.transform.localRotation == Quaternion.identity;
        }

        private static void ConfigureVehicleRoot(GameObject root, string childName, Mesh mesh, Material[] materials)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            MeshRenderer rootMr = root.GetComponent<MeshRenderer>();
            if (rootMr != null) Object.DestroyImmediate(rootMr);
            MeshFilter rootMf = root.GetComponent<MeshFilter>();
            if (rootMf != null) Object.DestroyImmediate(rootMf);

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            GameObject visual = new GameObject(childName);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            MeshFilter mf = visual.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;

            BoxCollider col = root.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = root.AddComponent<BoxCollider>();
            }

            Bounds b = mesh.bounds;
            col.center = b.center;
            col.size = b.size;
        }
    }
}
#endif
