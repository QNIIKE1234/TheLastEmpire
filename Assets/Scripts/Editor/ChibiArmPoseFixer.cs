#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public static class ChibiArmPoseFixer
    {
    }

    /// <summary>
    /// Ensures Chibi_TacticalGirl_Rigged.fbx materials use a clean matte finish
    /// (like ChibiGhostOperator) while keeping glossy highlights only on the eyes.
    /// </summary>
    public class ChibiTacticalGirlMaterialPostprocessor : AssetPostprocessor
    {
        private void OnPostprocessMaterial(Material material)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.Contains("Chibi_TacticalGirl_Rigged"))
            {
                return;
            }

            ApplyTacticalGirlMaterialSettings(material);
        }

        public static void ApplyTacticalGirlMaterialSettings(Material material)
        {
            if (material == null) return;

            string matName = material.name;
            bool isEye = matName.Contains("Glossy_Black") ||
                         matName.Contains("Eye_Amber") ||
                         matName.Contains("Eye_Gold_Hi") ||
                         matName.Contains("Eye_White");
            bool isSilver = matName.Contains("Metal_Silver");
            bool isArmor = matName.Contains("Armor_Grey");

            float smoothness = isEye ? 0.90f : (isSilver ? 0.48f : (isArmor ? 0.18f : 0.08f));
            float metallic = isSilver ? 0.55f : (isArmor ? 0.08f : 0.0f);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_SpecColor") && !isEye && !isSilver)
                material.SetColor("_SpecColor", new Color(0.05f, 0.05f, 0.05f, 1f));
        }

        [InitializeOnLoadMethod]
        private static void EnsureMaterialsUpdated()
        {
            EditorApplication.delayCall += () =>
            {
                const string fbxPath = "Assets/GeneratedModels/Chibi_TacticalGirl_Rigged.fbx";
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                if (subAssets == null || subAssets.Length == 0) return;

                bool changed = false;
                foreach (Object obj in subAssets)
                {
                    if (obj is Material mat)
                    {
                        bool isEye = mat.name.Contains("Glossy_Black") ||
                                     mat.name.Contains("Eye_Amber") ||
                                     mat.name.Contains("Eye_Gold_Hi") ||
                                     mat.name.Contains("Eye_White");
                        float targetSmooth = isEye ? 0.90f : (mat.name.Contains("Metal_Silver") ? 0.48f : (mat.name.Contains("Armor_Grey") ? 0.18f : 0.08f));
                        if (mat.HasProperty("_Smoothness") && Mathf.Abs(mat.GetFloat("_Smoothness") - targetSmooth) > 0.02f)
                        {
                            ApplyTacticalGirlMaterialSettings(mat);
                            EditorUtility.SetDirty(mat);
                            changed = true;
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
