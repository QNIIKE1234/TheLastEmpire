#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Standardizes EVERY Enemy material (Aliens, Zombies, Robots) to native Unity URP/Lit
    /// with the exact crisp matte Toy-Chibi finish used by Chibi_PunkGirl & Player/NPC models
    /// (Smoothness = 0.08, Metallic = 0 on skin/cloth/fur/flesh; crisp metal on robots/buckles;
    /// glossy 0.85 on eyes/glasses) — eliminating both the waxy clay sheen and dark custom-shader artifacts!
    /// </summary>
    [InitializeOnLoad]
    public static class EnemyPbrMaterialUpgrader
    {
        private const string EnemyMatsDir = "Assets/GeneratedModels/EnemyPbrMaterials";
        private const string StyleVersionKey = "TheLastEmpire_EnemyMatStyle_ChibiMatte_v2";

        private static bool _isUpgrading;

        static EnemyPbrMaterialUpgrader()
        {
            EditorApplication.delayCall += EnsureUpgradedAuto;
        }

        private static void EnsureUpgradedAuto()
        {
            if (_isUpgrading || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!SessionState.GetBool(StyleVersionKey, false))
            {
                UpgradeAllEnemyMaterials(false);
            }
        }

        [MenuItem("Tools/The Last Empire/Apply Chibi-Matte URP Lit Style to All Enemies")]
        public static void UpgradeFromMenu()
        {
            UpgradeAllEnemyMaterials(true);
        }

        public static void UpgradeAllEnemyMaterials(bool showDialog)
        {
            if (_isUpgrading) return;
            _isUpgrading = true;
            try
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (urpLit == null)
                {
                    Debug.LogError("[EnemyPbrMaterialUpgrader] Could not find Universal Render Pipeline/Lit shader!");
                    return;
                }

                string[] matDirs = new[]
                {
                    EnemyMatsDir,
                    "Assets/GeneratedModels/Aliens",
                    "Assets/GeneratedModels/Zombies",
                    "Assets/GeneratedModels/Robots"
                };

                int matsUpdated = 0;
                foreach (string dir in matDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    bool isRobotDir = dir.IndexOf("/Robots", StringComparison.OrdinalIgnoreCase) >= 0;
                    string[] guids = AssetDatabase.FindAssets("t:Material", new[] { dir });
                    foreach (string guid in guids)
                    {
                        string matPath = AssetDatabase.GUIDToAssetPath(guid);
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (mat == null) continue;
                        if (mat.name.IndexOf("GlassDome", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        bool isRobotMat = isRobotDir || mat.name.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0;
                        ApplyChibiMatteUrpLit(mat, urpLit, isRobotMat);
                        EditorUtility.SetDirty(mat);
                        matsUpdated++;
                    }
                }

                // Also update embedded sub-asset materials on Enemy FBX/OBJ models that aren't externally remapped
                string[] modelDirs = new[]
                {
                    "Assets/GeneratedModels/Aliens",
                    "Assets/GeneratedModels/Zombies",
                    "Assets/GeneratedModels/Robots"
                };
                foreach (string dir in modelDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    bool isRobotDir = dir.IndexOf("/Robots", StringComparison.OrdinalIgnoreCase) >= 0;
                    string[] guids = AssetDatabase.FindAssets("t:Model", new[] { dir });
                    foreach (string guid in guids)
                    {
                        string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(modelPath) || modelPath.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)) continue;
                        UnityEngine.Object[] subs = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                        if (subs == null) continue;
                        foreach (UnityEngine.Object obj in subs)
                        {
                            if (obj is Material m && m.name.IndexOf("GlassDome", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                ApplyChibiMatteUrpLit(m, urpLit, isRobotDir);
                                EditorUtility.SetDirty(m);
                            }
                        }
                    }
                }

                SessionState.SetBool(StyleVersionKey, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"<color=#50FA7B>[EnemyPbrMaterialUpgrader]</color> Applied clean Chibi-Matte URP/Lit finish (matching Chibi_PunkGirl) to {matsUpdated} Enemy materials!");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Enemy Materials Updated",
                        $"ปรับ Material ศัตรูทั้งหมด {matsUpdated} ชิ้นให้เป็นสไตล์ Matte URP/Lit แบบ Chibi_PunkGirl เรียบร้อยแล้วครับ!", "OK");
                }
            }
            finally
            {
                _isUpgrading = false;
            }
        }

        public static void ApplyChibiMatteUrpLit(Material mat, Shader urpLit, bool isRobot)
        {
            if (mat == null || urpLit == null) return;

            // Preserve original color, painted BaseMap (faces/hair/tattoos), and emission!
            Color baseColor = Color.white;
            if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color")) baseColor = mat.GetColor("_Color");

            Texture baseMap = null;
            bool hadBaseMap = true;
            if (mat.HasProperty("_UseBaseMap")) hadBaseMap = mat.GetFloat("_UseBaseMap") > 0.5f;
            if (hadBaseMap)
            {
                if (mat.HasProperty("_BaseMap")) baseMap = mat.GetTexture("_BaseMap");
                if (baseMap == null && mat.HasProperty("_MainTex")) baseMap = mat.GetTexture("_MainTex");
            }

            Color emissionColor = Color.black;
            if (mat.HasProperty("_EmissionColor")) emissionColor = mat.GetColor("_EmissionColor");
            bool hasEmission = emissionColor.maxColorComponent > 0.02f;

            // Switch cleanly to standard URP/Lit
            if (mat.shader != urpLit)
            {
                mat.shader = urpLit;
            }

            // Ensure Opaque URP/Lit setup
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseMap);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseMap);
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");

            // Exact Chibi_PunkGirl / Toy-Chibi material finish!
            string n = mat.name.ToLowerInvariant();
            bool isGlossy = n.Contains("eye") || n.Contains("pupil") || n.Contains("glasses") ||
                            n.Contains("lens") || n.Contains("visor") || n.Contains("saliva") ||
                            n.Contains("glossy") || n.Contains("boil_glow") || n.Contains("acidglow");
            bool isMetal = n.Contains("silver") || n.Contains("gold") || n.Contains("metal") ||
                           n.Contains("steel") || n.Contains("iron") || n.Contains("chrome") ||
                           n.Contains("buckle") || n.Contains("shackle") || n.Contains("baton") ||
                           n.Contains("rebar") || n.Contains("blade") || n.Contains("gun") ||
                           (isRobot && !isGlossy && !n.Contains("glow") && !n.Contains("core"));

            float smoothness = isGlossy ? 0.85f : (isMetal ? 0.56f : 0.08f);
            float metallic = isMetal ? 0.62f : 0.0f;

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", isMetal || isGlossy ? 1f : 0.35f);

            if (hasEmission)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
#endif
