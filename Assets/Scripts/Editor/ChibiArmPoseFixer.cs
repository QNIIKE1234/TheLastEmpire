#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public static class ChibiArmPoseFixer
    {
    }

    /// <summary>
    /// Ensures Chibi_TacticalGirl_Rigged.fbx, Chibi_PunkGirl_Rigged.fbx, and all Craft Building FBX models
    /// in Assets/GeneratedBaseItems/ use a clean matte finish (like the player character) instead of a shiny clay sheen.
    /// </summary>
    public class ChibiTacticalGirlMaterialPostprocessor : AssetPostprocessor
    {
        private static bool IsCraftBuildingFbx(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.Replace('\\', '/').Contains("Assets/GeneratedBaseItems/") &&
                   path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (string.IsNullOrEmpty(assetPath) || material == null)
            {
                return;
            }

            if (assetPath.Contains("Chibi_TacticalGirl_Rigged") || assetPath.Contains("Chibi_PunkGirl_Rigged"))
            {
                ApplyTacticalGirlMaterialSettings(material);
            }
            else if (IsCraftBuildingFbx(assetPath))
            {
                ApplyCraftBuildingMatteSettings(material);
            }
        }

        public static void ApplyTacticalGirlMaterialSettings(Material material)
        {
            if (material == null) return;

            string matName = material.name;
            bool isEye = matName.Contains("Glossy_Black") ||
                         matName.Contains("Eye_Amber") ||
                         matName.Contains("Eye_Gold_Hi") ||
                         matName.Contains("Eye_White") ||
                         matName.Contains("Eye_Red");
            bool isSilver = matName.Contains("Metal_Silver") || matName.Contains("Zipper_Teeth") || matName.Contains("Bat_Nail");
            bool isArmor = matName.Contains("Armor_Grey");

            float smoothness = isEye ? 0.90f : (isSilver ? 0.48f : (isArmor ? 0.18f : 0.08f));
            float metallic = isSilver ? 0.70f : (isArmor ? 0.08f : 0.0f);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_SpecColor") && !isEye && !isSilver)
                material.SetColor("_SpecColor", new Color(0.05f, 0.05f, 0.05f, 1f));
        }

        public static void ApplyCraftBuildingMatteSettings(Material material)
        {
            if (material == null) return;

            string matName = material.name;
            bool isGlassOrWater = matName.Contains("Glass") || matName.Contains("Water") || matName.Contains("Plasma");

            // Pure stylized Chibi matte finish matching player character (zero clay/plastic specular shine)
            float smoothness = isGlassOrWater ? 0.25f : 0.04f;
            float metallic = 0.0f;

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_SpecColor"))
                material.SetColor("_SpecColor", new Color(0.02f, 0.02f, 0.02f, 1f));
            if (material.HasProperty("_SpecularHighlights") && !isGlassOrWater)
                material.SetFloat("_SpecularHighlights", 0f);
            if (material.HasProperty("_EnvironmentReflections") && !isGlassOrWater)
                material.SetFloat("_EnvironmentReflections", 0f);
        }

        [InitializeOnLoadMethod]
        private static void EnsureMaterialsUpdated()
        {
            EditorApplication.delayCall += () =>
            {
                string[] fbxPaths = new string[] {
                    "Assets/GeneratedModels/Chibi_TacticalGirl_Rigged.fbx",
                    "Assets/GeneratedModels/Chibi_PunkGirl_Rigged.fbx"
                };

                bool changed = false;
                foreach (string path in fbxPaths)
                {
                    Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    if (subAssets == null || subAssets.Length == 0) continue;

                    foreach (Object obj in subAssets)
                    {
                        if (obj is Material mat)
                        {
                            bool isEye = mat.name.Contains("Glossy_Black") ||
                                         mat.name.Contains("Eye_Amber") ||
                                         mat.name.Contains("Eye_Gold_Hi") ||
                                         mat.name.Contains("Eye_White") ||
                                         mat.name.Contains("Eye_Red");
                            float targetSmooth = isEye ? 0.90f : (mat.name.Contains("Metal_Silver") || mat.name.Contains("Zipper_Teeth") ? 0.48f : (mat.name.Contains("Armor_Grey") ? 0.18f : 0.08f));
                            if (mat.HasProperty("_Smoothness") && Mathf.Abs(mat.GetFloat("_Smoothness") - targetSmooth) > 0.02f)
                            {
                                ApplyTacticalGirlMaterialSettings(mat);
                                EditorUtility.SetDirty(mat);
                                changed = true;
                            }
                        }
                    }
                }

                if (Directory.Exists("Assets/GeneratedBaseItems"))
                {
                    string[] craftFbxFiles = Directory.GetFiles("Assets/GeneratedBaseItems", "*.fbx");
                    foreach (string rawPath in craftFbxFiles)
                    {
                        string path = rawPath.Replace('\\', '/');
                        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                        if (subAssets == null || subAssets.Length == 0) continue;

                        foreach (Object obj in subAssets)
                        {
                            if (obj is Material mat)
                            {
                                bool isGlassOrWater = mat.name.Contains("Glass") || mat.name.Contains("Water") || mat.name.Contains("Plasma");
                                float targetSmooth = isGlassOrWater ? 0.25f : 0.04f;
                                if (mat.HasProperty("_Smoothness") && Mathf.Abs(mat.GetFloat("_Smoothness") - targetSmooth) > 0.02f)
                                {
                                    ApplyCraftBuildingMatteSettings(mat);
                                    EditorUtility.SetDirty(mat);
                                    changed = true;
                                }
                            }
                        }
                    }
                }

                if (changed)
                {
                    AssetDatabase.SaveAssets();
                }
            };
        }
    }
}
#endif
