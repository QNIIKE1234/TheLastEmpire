#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    /// <summary>
    /// Upgrades all 21 house & building prefabs in Prefabs/Environment/Building to the
    /// Avatar-Star-inspired Stylized American FBX models while preserving prefab GUIDs,
    /// Addressables entries, and clean BoxCollider bounds.
    /// </summary>
    public static class StylizedUSBuildingPrefabUpgrader
    {
        private const string PrefabDir = "Assets/Resources_Addressable/Prefabs/Environment/Building";
        private const string FbxDir = "Assets/GeneratedEnvironment";

        private struct BuildingMapping
        {
            public string PrefabName;
            public string FbxName;
        }

        private static readonly BuildingMapping[] Mappings =
        {
            // 8 Houses & Main Street Shops
            new BuildingMapping { PrefabName = "Building_CottageHouse", FbxName = "Stylized_Building_CottageHouse" },
            new BuildingMapping { PrefabName = "Building_Townhouse", FbxName = "Stylized_Building_Townhouse" },
            new BuildingMapping { PrefabName = "Building_CornerShop", FbxName = "Stylized_Building_CornerShop" },
            new BuildingMapping { PrefabName = "Building_CanalHouse", FbxName = "Stylized_Building_CanalHouse" },
            new BuildingMapping { PrefabName = "Building_AlpineChalet", FbxName = "Stylized_Building_AlpineChalet" },
            new BuildingMapping { PrefabName = "House_Suburban_A", FbxName = "Stylized_House_Suburban_A" },
            new BuildingMapping { PrefabName = "House_Suburban_B", FbxName = "Stylized_House_Suburban_B" },
            new BuildingMapping { PrefabName = "House_Suburban_C", FbxName = "Stylized_House_Suburban_C" },

            // 5 Weathered American Houses + Corner Loft Block
            new BuildingMapping { PrefabName = "Apoc_House_01_Brick_Rowhouse", FbxName = "Stylized_Apoc_House_01_Brick_Rowhouse" },
            new BuildingMapping { PrefabName = "Apoc_House_02_Concrete_Duplex", FbxName = "Stylized_Apoc_House_02_Concrete_Duplex" },
            new BuildingMapping { PrefabName = "Apoc_House_03_Salvaged_Tin_Shack", FbxName = "Stylized_Apoc_House_03_Salvaged_Tin_Shack" },
            new BuildingMapping { PrefabName = "Apoc_House_04_Ruined_Corner_Shop", FbxName = "Stylized_Apoc_House_04_Ruined_Corner_Shop" },
            new BuildingMapping { PrefabName = "Apoc_House_05_Collapsed_Suburban_Home", FbxName = "Stylized_Apoc_House_05_Collapsed_Suburban_Home" },
            new BuildingMapping { PrefabName = "building", FbxName = "Stylized_Building_Block" },

            // 7 Stylized American City Midrises
            new BuildingMapping { PrefabName = "Apoc_Building_01_Residential_Ruin", FbxName = "Stylized_Apoc_Building_01_Residential_Ruin" },
            new BuildingMapping { PrefabName = "Apoc_Building_02_Office_Glass_Ruin", FbxName = "Stylized_Apoc_Building_02_Office_Glass_Ruin" },
            new BuildingMapping { PrefabName = "Apoc_Building_03_Narrow_Signal_Tower", FbxName = "Stylized_Apoc_Building_03_Narrow_Signal_Tower" },
            new BuildingMapping { PrefabName = "Apoc_Building_04_Industrial_Hospital_Ruin", FbxName = "Stylized_Apoc_Building_04_Industrial_Hospital_Ruin" },
            new BuildingMapping { PrefabName = "Apoc_Building_05_Shattered_Hotel_Midrise", FbxName = "Stylized_Apoc_Building_05_Shattered_Hotel_Midrise" },
            new BuildingMapping { PrefabName = "Apoc_Building_06_Ruined_Clinic_Lowrise", FbxName = "Stylized_Apoc_Building_06_Ruined_Clinic_Lowrise" },
            new BuildingMapping { PrefabName = "Apoc_Building_07_Collapsed_Depot_Midrise", FbxName = "Stylized_Apoc_Building_07_Collapsed_Depot_Midrise" },

            // 17 Core Stylized American Landmarks
            new BuildingMapping { PrefabName = "Landmark_WarehouseHangar", FbxName = "Stylized_Landmark_WarehouseHangar" },
            new BuildingMapping { PrefabName = "Landmark_CafePatio", FbxName = "Stylized_Landmark_CafePatio" },
            new BuildingMapping { PrefabName = "Landmark_MarketStall_Red", FbxName = "Stylized_Landmark_MarketStall_Red" },
            new BuildingMapping { PrefabName = "Landmark_MarketStall_Blue", FbxName = "Stylized_Landmark_MarketStall_Blue" },
            new BuildingMapping { PrefabName = "Landmark_BrownstoneRow", FbxName = "Stylized_Landmark_BrownstoneRow" },
            new BuildingMapping { PrefabName = "Landmark_CampTentGreen", FbxName = "Stylized_Landmark_CampTentGreen" },
            new BuildingMapping { PrefabName = "Landmark_CampTentTan", FbxName = "Stylized_Landmark_CampTentTan" },
            new BuildingMapping { PrefabName = "Landmark_GuardWatchtower", FbxName = "Stylized_Landmark_GuardWatchtower" },
            new BuildingMapping { PrefabName = "Landmark_ParkGazebo", FbxName = "Stylized_Landmark_ParkGazebo" },
            new BuildingMapping { PrefabName = "Landmark_SubwayEntrance", FbxName = "Stylized_Landmark_SubwayEntrance" },
            new BuildingMapping { PrefabName = "Landmark_BillboardSign", FbxName = "Stylized_Landmark_BillboardSign" },
            new BuildingMapping { PrefabName = "Landmark_GasStationCanopy", FbxName = "Stylized_Landmark_GasStationCanopy" },
            new BuildingMapping { PrefabName = "Landmark_ClockTower", FbxName = "Stylized_Landmark_ClockTower" },
            new BuildingMapping { PrefabName = "Landmark_FountainPlaza", FbxName = "Stylized_Landmark_FountainPlaza" },
            new BuildingMapping { PrefabName = "Landmark_WaterTower", FbxName = "Stylized_Landmark_WaterTower" },
            new BuildingMapping { PrefabName = "Landmark_DinerRetro", FbxName = "Stylized_Landmark_DinerRetro" },
            new BuildingMapping { PrefabName = "Landmark_WoodenPierDock", FbxName = "Stylized_Landmark_WoodenPierDock" },

            // 7 Iconic American Monuments
            new BuildingMapping { PrefabName = "Landmark_StatueOfLiberty", FbxName = "Stylized_Landmark_StatueOfLiberty" },
            new BuildingMapping { PrefabName = "Landmark_CapitolDome", FbxName = "Stylized_Landmark_CapitolDome" },
            new BuildingMapping { PrefabName = "Landmark_LincolnMonument", FbxName = "Stylized_Landmark_LincolnMonument" },
            new BuildingMapping { PrefabName = "Landmark_GoldenGateTower", FbxName = "Stylized_Landmark_GoldenGateTower" },
            new BuildingMapping { PrefabName = "Landmark_SpaceNeedle", FbxName = "Stylized_Landmark_SpaceNeedle" },
            new BuildingMapping { PrefabName = "Landmark_HollywoodSign", FbxName = "Stylized_Landmark_HollywoodSign" },
            new BuildingMapping { PrefabName = "Landmark_SearsTower", FbxName = "Stylized_Landmark_SearsTower" },

            // 8 Stylized American Street Props (incl. Low-Poly BoulderCluster)
            new BuildingMapping { PrefabName = "Prop_BoulderCluster", FbxName = "Stylized_Prop_BoulderCluster" },
            new BuildingMapping { PrefabName = "Prop_Streetlight", FbxName = "Stylized_Prop_Streetlight" },
            new BuildingMapping { PrefabName = "Prop_Bench", FbxName = "Stylized_Prop_Bench" },
            new BuildingMapping { PrefabName = "Prop_Mailbox", FbxName = "Stylized_Prop_Mailbox" },
            new BuildingMapping { PrefabName = "Prop_TrafficCone", FbxName = "Stylized_Prop_TrafficCone" },
            new BuildingMapping { PrefabName = "Prop_Dumpster", FbxName = "Stylized_Prop_Dumpster" },
            new BuildingMapping { PrefabName = "Prop_WoodenFence", FbxName = "Stylized_Prop_WoodenFence" },
            new BuildingMapping { PrefabName = "Prop_TrashCan", FbxName = "Stylized_Prop_TrashCan" },
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

                UpgradeAllBuildings(silentIfAlreadyUpgraded: true);
            };
        }

        [MenuItem("Tools/TheLastEmpire/Upgrade Buildings to Stylized American Style")]
        public static void UpgradeAllBuildingsMenu()
        {
            UpgradeAllBuildings(silentIfAlreadyUpgraded: false);
        }

        public static void UpgradeAllBuildings(bool silentIfAlreadyUpgraded)
        {
            int updated = 0;

            foreach (BuildingMapping mapping in Mappings)
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
                if (!File.Exists(prefabPath))
                {
                    continue;
                }

                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing != null && AlreadyUsesMesh(existing, fbxFilter.sharedMesh))
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    // Ensure upright identity root transform (replaces old +90 X Apoc rotation)
                    contents.transform.localPosition = Vector3.zero;
                    contents.transform.localRotation = Quaternion.identity;
                    contents.transform.localScale = Vector3.one;

                    // Remove legacy MeshFilter/MeshRenderer directly on root if any
                    MeshRenderer rootMr = contents.GetComponent<MeshRenderer>();
                    if (rootMr != null) Object.DestroyImmediate(rootMr);
                    MeshFilter rootMf = contents.GetComponent<MeshFilter>();
                    if (rootMf != null) Object.DestroyImmediate(rootMf);

                    // Clear old children
                    for (int i = contents.transform.childCount - 1; i >= 0; i--)
                    {
                        Object.DestroyImmediate(contents.transform.GetChild(i).gameObject);
                    }

                    // Attach clean upright visual child
                    GameObject visual = new GameObject(mapping.FbxName);
                    visual.transform.SetParent(contents.transform, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;

                    MeshFilter mf = visual.AddComponent<MeshFilter>();
                    mf.sharedMesh = fbxFilter.sharedMesh;

                    MeshRenderer mr = visual.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = fbxMats;

                    // Fit root BoxCollider accurately to the new stylized building footprint
                    BoxCollider col = contents.GetComponent<BoxCollider>();
                    if (col == null)
                    {
                        col = contents.AddComponent<BoxCollider>();
                    }

                    Bounds b = fbxFilter.sharedMesh.bounds;
                    col.center = b.center;
                    col.size = b.size;

                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    updated++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            if (updated > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[StylizedUSBuildingPrefabUpgrader] Upgraded {updated} building prefabs to Stylized American Chibi models.");
            }
            else if (!silentIfAlreadyUpgraded)
            {
                Debug.Log("[StylizedUSBuildingPrefabUpgrader] All 21 stylized American building prefabs are already up to date.");
            }
        }

        private static bool AlreadyUsesMesh(GameObject prefabRoot, Mesh expectedMesh)
        {
            MeshFilter[] filters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
            return filters.Length == 1 && filters[0].sharedMesh == expectedMesh && prefabRoot.transform.localRotation == Quaternion.identity;
        }
    }
}
#endif
