using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public static class ChibiGhostOperatorBuilder
    {
        private const string ModelAssetPath = "Assets/GeneratedModels/ChibiGhostOperator/ChibiGhostOperator.obj";
        private const string MatFolderPath = "Assets/Materials/ChibiGhostOperator";
        private const string TexFolderPath = "Assets/Materials/ChibiGhostOperator/Textures";
        private const string PrefabOutputPath = "Assets/Resources_Addressable/Prefabs/Character/Model/ChibiGhostOperator.prefab";

        [MenuItem("Tools/The Last Empire/Build Chibi Ghost Operator (Materials + Prefab)")]
        public static void BuildFromMenu()
        {
            BuildMaterialsAndPrefab(true);
        }

        public static void BuildMaterialsAndPrefab(bool showDialog)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Texture2D flagTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolderPath}/Tex_USFlagPatch.png");
            Texture2D scarfTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolderPath}/Tex_ScarfShemagh.png");
            Texture2D corduraTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolderPath}/Tex_CorduraWeave.png");

            var matSpecs = new[]
            {
                new { Name = "Mat_SkullBone",     Col = new Color(0.95f, 0.93f, 0.90f, 1f), Smooth = 0.36f, Metal = 0.00f, Tex = (Texture2D)null },
                new { Name = "Mat_Balaclava",     Col = new Color(0.08f, 0.08f, 0.10f, 1f), Smooth = 0.14f, Metal = 0.00f, Tex = (Texture2D)null },
                new { Name = "Mat_GlossyEye",     Col = new Color(0.02f, 0.02f, 0.03f, 1f), Smooth = 0.96f, Metal = 0.05f, Tex = (Texture2D)null },
                new { Name = "Mat_HelmetTan",     Col = new Color(0.82f, 0.73f, 0.61f, 1f), Smooth = 0.24f, Metal = 0.00f, Tex = corduraTex },
                new { Name = "Mat_VestTan",       Col = new Color(0.78f, 0.68f, 0.55f, 1f), Smooth = 0.22f, Metal = 0.00f, Tex = corduraTex },
                new { Name = "Mat_FoamDark",      Col = new Color(0.14f, 0.11f, 0.09f, 1f), Smooth = 0.12f, Metal = 0.00f, Tex = (Texture2D)null },
                new { Name = "Mat_HeadsetOlive",  Col = new Color(0.31f, 0.29f, 0.22f, 1f), Smooth = 0.42f, Metal = 0.12f, Tex = (Texture2D)null },
                new { Name = "Mat_ClothCharcoal", Col = new Color(0.26f, 0.27f, 0.29f, 1f), Smooth = 0.18f, Metal = 0.00f, Tex = corduraTex },
                new { Name = "Mat_ScarfWoven",    Col = new Color(0.88f, 0.88f, 0.88f, 1f), Smooth = 0.16f, Metal = 0.00f, Tex = scarfTex },
                new { Name = "Mat_FlagPatch",     Col = new Color(1.00f, 1.00f, 1.00f, 1f), Smooth = 0.20f, Metal = 0.00f, Tex = flagTex },
                new { Name = "Mat_PolymerBlack",  Col = new Color(0.11f, 0.12f, 0.14f, 1f), Smooth = 0.46f, Metal = 0.08f, Tex = (Texture2D)null },
                new { Name = "Mat_MetalDark",     Col = new Color(0.22f, 0.24f, 0.27f, 1f), Smooth = 0.68f, Metal = 0.72f, Tex = (Texture2D)null },
                new { Name = "Mat_SkinForearm",   Col = new Color(0.87f, 0.75f, 0.63f, 1f), Smooth = 0.30f, Metal = 0.00f, Tex = (Texture2D)null },
                new { Name = "Mat_BootTan",       Col = new Color(0.78f, 0.67f, 0.53f, 1f), Smooth = 0.20f, Metal = 0.00f, Tex = (Texture2D)null },
                new { Name = "Mat_BootSole",      Col = new Color(0.68f, 0.57f, 0.44f, 1f), Smooth = 0.28f, Metal = 0.00f, Tex = (Texture2D)null }
            };

            Dictionary<string, Material> loadedMats = new Dictionary<string, Material>();
            foreach (var s in matSpecs)
            {
                string matPath = $"{MatFolderPath}/{s.Name}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(urpLit);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else if (urpLit != null && mat.shader != urpLit)
                {
                    mat.shader = urpLit;
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", s.Col);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", s.Col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", s.Smooth);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", s.Smooth);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", s.Metal);

                if (s.Tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", s.Tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", s.Tex);
                }

                EditorUtility.SetDirty(mat);
                loadedMats[s.Name] = mat;
            }

            AssetDatabase.SaveAssets();

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
            if (modelAsset == null)
            {
                AssetDatabase.ImportAsset(ModelAssetPath, ImportAssetOptions.ForceUpdate);
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
            }

            if (modelAsset != null)
            {
                string dir = Path.GetDirectoryName(PrefabOutputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                try
                {
                    instance.name = "ChibiGhostOperator";
                    instance.transform.position = Vector3.zero;
                    instance.transform.rotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;

                    foreach (MeshRenderer mr in instance.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Material[] shared = mr.sharedMaterials;
                        for (int i = 0; i < shared.Length; i++)
                        {
                            if (shared[i] != null)
                            {
                                string cleanName = shared[i].name.Replace(" (Instance)", "").Trim();
                                if (loadedMats.TryGetValue(cleanName, out Material mapped))
                                {
                                    shared[i] = mapped;
                                }
                            }
                        }
                        mr.sharedMaterials = shared;
                    }

                    CapsuleCollider col = instance.GetComponent<CapsuleCollider>();
                    if (col == null) col = instance.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, 1.0f, 0f);
                    col.height = 2.0f;
                    col.radius = 0.42f;

                    PrefabUtility.SaveAsPrefabAsset(instance, PrefabOutputPath);
                }
                finally
                {
                    if (instance != null)
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Chibi Ghost Operator Built",
                    "Successfully generated and linked 15 URP Materials and saved ChibiGhostOperator.prefab!",
                    "OK");
            }
        }
    }
}

