#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    [InitializeOnLoad]
    public static class FurniturePrefabBakeTool
    {
        private const string FurnitureFolder = "Assets/Resources_Addressable/Prefabs/Furniture";
        private const string FbxFolder       = "Assets/GeneratedModels/Furniture";
        private const string MaterialFolder  = "Assets/Resources_Addressable/Prefabs/Furniture/Materials";
        private const string AutoAppliedKey  = "TLE_FurnitureLowPolyFBX_Applied_v3";

        static FurniturePrefabBakeTool()
        {
            // Automatically apply the 30 Low-Poly FBX models into Assets/Resources_Addressable/Prefabs/Furniture/
            // once Unity finishes importing Assets/GeneratedModels/Furniture/*.fbx (no manual menu click needed!)
            EditorApplication.delayCall += TryAutoApplyLowPolyFbx;
        }

        private static void TryAutoApplyLowPolyFbx()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(AutoAppliedKey, false)) return;

            // Check whether any prefab in Prefabs/Furniture is still missing a real FBX VisualRoot
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FurnitureFolder });
            bool needsBake = false;
            int foundFbx = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

                string baseName = Path.GetFileNameWithoutExtension(path);
                string fbxPath = $"{FbxFolder}/{baseName}.fbx";
                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxAsset == null) continue;
                foundFbx++;

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null) continue;

                Transform vis = prefabAsset.transform.Find("VisualRoot");
                bool hasMesh = vis != null && vis.GetComponentInChildren<MeshRenderer>(true) != null;
                bool isFbxInstance = vis != null && PrefabUtility.GetCorrespondingObjectFromSource(vis.gameObject) == fbxAsset;
                if (!hasMesh || !isFbxInstance)
                {
                    needsBake = true;
                    break;
                }
            }

            if (foundFbx > 0)
            {
                SessionState.SetBool(AutoAppliedKey, true);
                if (needsBake)
                {
                    BakeAllFurniturePrefabs();
                }
            }
        }

        [MenuItem("Tools/TheLastEmpire/Bake Furniture Prefab Visuals (Low-Poly FBX & 3D Thumbnails)")]
        public static void BakeAllFurniturePrefabs()
        {
            if (!Directory.Exists(FurnitureFolder))
            {
                Debug.LogWarning($"[FurniturePrefabBakeTool] Folder not found: {FurnitureFolder}");
                return;
            }

            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(FurnitureFolder, "Materials");
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Dictionary<string, Material> matCache = new Dictionary<string, Material>();

            Material ResolveMaterial(string key, Color color, float smoothness, Color? emission)
            {
                if (matCache.TryGetValue(key, out Material cached) && cached != null)
                {
                    return cached;
                }

                string matPath = $"{MaterialFolder}/Mat_Furniture_{key}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    mat.name = $"Mat_Furniture_{key}";
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (emission.HasValue && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission.Value);
                }
                EditorUtility.SetDirty(mat);
                matCache[key] = mat;
                return mat;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FurnitureFolder });
            int fbxApplied = 0;
            int proceduralBaked = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

                string baseName = Path.GetFileNameWithoutExtension(path);
                string fbxPath = $"{FbxFolder}/{baseName}.fbx";
                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    if (fbxAsset != null)
                    {
                        Transform oldVis = root.transform.Find("VisualRoot");
                        while (oldVis != null)
                        {
                            Object.DestroyImmediate(oldVis.gameObject);
                            oldVis = root.transform.Find("VisualRoot");
                        }

                        GameObject visInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, root.transform);
                        visInstance.name = "VisualRoot";
                        visInstance.transform.localPosition = Vector3.zero;
                        // Preserve FBX native upright rotation & scale (Blender Z-up -> Unity Y-up -90° X!)
                        visInstance.transform.localRotation = fbxAsset.transform.localRotation;
                        visInstance.transform.localScale = fbxAsset.transform.localScale;

                        Collider[] childCols = visInstance.GetComponentsInChildren<Collider>(true);
                        for (int c = 0; c < childCols.Length; c++)
                        {
                            if (childCols[c] != null) Object.DestroyImmediate(childCols[c]);
                        }

                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        fbxApplied++;
                    }
                    else
                    {
                        FurnitureVisualBuilder builder = root.GetComponent<FurnitureVisualBuilder>();
                        if (builder != null)
                        {
                            builder.BuildPersistentForBake(ResolveMaterial);
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            proceduralBaked++;
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FurniturePrefabBakeTool] Applied Low-Poly FBX to {fbxApplied} prefabs in {FurnitureFolder} (procedural fallback: {proceduralBaked})!");
        }
    }
}
#endif
