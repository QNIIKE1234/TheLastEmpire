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
        private static bool IsMatteBuildingFbx(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.Contains("Assets/GeneratedBaseItems/") ||
                   normalized.Contains("Assets/GeneratedEnvironment/");
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
            else if (IsMatteBuildingFbx(assetPath))
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

        private const string WeatheredAlbedoTexPath = "Assets/GeneratedBaseItems/Craft_Weathered_Detail_Albedo.png";
        private static Texture2D _cachedDetailTex;

        private static Texture2D GetOrCreateWeatheredDetailTexture()
        {
            if (_cachedDetailTex != null) return _cachedDetailTex;

            if (File.Exists(WeatheredAlbedoTexPath))
            {
                _cachedDetailTex = AssetDatabase.LoadAssetAtPath<Texture2D>(WeatheredAlbedoTexPath);
                if (_cachedDetailTex != null) return _cachedDetailTex;
            }

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Seamless tileable multi-octave noise matching Blender's ShaderNodeTexNoise + ColorRamp (0.80..1.00)
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n1 = TileableNoise(u, v, 6f);
                    float n2 = TileableNoise(u + 0.37f, v + 0.61f, 14f);
                    float n3 = TileableNoise(u + 0.73f, v + 0.19f, 28f);
                    float n = n1 * 0.50f + n2 * 0.32f + n3 * 0.18f;
                    float t = Mathf.Clamp01((n - 0.25f) / 0.53f);
                    float shade = Mathf.Lerp(0.80f, 1.0f, t);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(shade * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            if (Directory.Exists("Assets/GeneratedBaseItems"))
            {
                File.WriteAllBytes(WeatheredAlbedoTexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(WeatheredAlbedoTexPath, ImportAssetOptions.ForceSynchronousImport);
                _cachedDetailTex = AssetDatabase.LoadAssetAtPath<Texture2D>(WeatheredAlbedoTexPath);
                return _cachedDetailTex;
            }

            _cachedDetailTex = tex;
            return _cachedDetailTex;
        }

        private static float TileableNoise(float u, float v, float period)
        {
            float x = u * period;
            float y = v * period;
            float p = period;
            return (
                Mathf.PerlinNoise(x, y) * (p - (x % p)) * (p - (y % p)) +
                Mathf.PerlinNoise((x % p) - p, y) * (x % p) * (p - (y % p)) +
                Mathf.PerlinNoise((x % p) - p, (y % p) - p) * (x % p) * (y % p) +
                Mathf.PerlinNoise(x, (y % p) - p) * (p - (x % p)) * (y % p)
            ) / (p * p);
        }

        private static void ResolveBuildingMaterialFinish(string matName, out float smoothness, out float metallic, out bool isMatte)
        {
            bool isGlassOrWater = matName.Contains("Glass") || matName.Contains("Water") ||
                                  matName.Contains("Plasma") || matName.Contains("Ember") ||
                                  matName.Contains("Signal") || matName.Contains("Fungus");
            bool isSilver = matName.Contains("bright bevel") || matName.Contains("scraped edges");
            bool isArmor = matName.Contains("graphite armor") || matName.Contains("dark forged steel") ||
                           matName.Contains("Metal | oxidized");

            smoothness = isGlassOrWater ? 0.45f : (isSilver ? 0.48f : (isArmor ? 0.18f : 0.08f));
            metallic = isSilver ? 0.70f : (isArmor ? 0.08f : 0.0f);
            isMatte = !isGlassOrWater && !isSilver;
        }

        public static void ApplyCraftBuildingMatteSettings(Material material)
        {
            if (material == null) return;

            ResolveBuildingMaterialFinish(material.name, out float smoothness, out float metallic, out bool isMatte);

            // Exact same shader parameters as ApplyTacticalGirlMaterialSettings on Chibi_TacticalGirl_Rigged.fbx
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_SpecColor") && isMatte)
                material.SetColor("_SpecColor", new Color(0.05f, 0.05f, 0.05f, 1f));
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", null);
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

                string[] fbxFolders = new string[] {
                    "Assets/GeneratedBaseItems",
                    "Assets/GeneratedEnvironment"
                };
                foreach (string folder in fbxFolders)
                {
                    if (!Directory.Exists(folder)) continue;

                    string[] fbxFiles = Directory.GetFiles(folder, "*.fbx");
                    foreach (string rawPath in fbxFiles)
                    {
                        string path = rawPath.Replace('\\', '/');
                        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                        if (subAssets == null || subAssets.Length == 0) continue;

                        foreach (Object obj in subAssets)
                        {
                            if (obj is Material mat)
                            {
                                ResolveBuildingMaterialFinish(mat.name, out float targetSmooth, out _, out _);
                                bool needsSmooth = mat.HasProperty("_Smoothness") && Mathf.Abs(mat.GetFloat("_Smoothness") - targetSmooth) > 0.02f;
                                bool hasTex = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
                                if (needsSmooth || hasTex)
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
