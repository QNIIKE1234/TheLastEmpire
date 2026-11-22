#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Generates 6 seamless 512x512 procedural PBR Detail Albedo + Normal texture pairs
    /// (Organic Flesh, Alien Chitin, Fur/Hair, Worn Cloth, Brushed Metal, Bone/Claw)
    /// and upgrades EVERY Enemy model material (Aliens, Zombies, Robots) to the triplanar
    /// `TheLastEmpire/URP/EnemyDetailedLit` shader so enemies no longer look like flat Play-Doh/clay!
    /// </summary>
    [InitializeOnLoad]
    public static class EnemyPbrMaterialUpgrader
    {
        private const string ShaderName = "TheLastEmpire/URP/EnemyDetailedLit";
        private const string SharedTexDir = "Assets/GeneratedModels/SharedPbrTextures";
        private const string EnemyMatsDir = "Assets/GeneratedModels/EnemyPbrMaterials";
        private const string SentinelPath = SharedTexDir + "/PBR_OrganicFlesh_Normal.png";

        private static bool _isUpgrading;

        private enum SurfaceCategory
        {
            OrganicFlesh,
            ChitinBio,
            FurHair,
            WornCloth,
            BrushedMetal,
            BoneClaw,
            GlossyEyeOrGlass,
            TransparentGlassSkip
        }

        private struct PbrTexPair
        {
            public Texture2D detailAlbedo;
            public Texture2D detailNormal;
        }

        static EnemyPbrMaterialUpgrader()
        {
            EditorApplication.delayCall += EnsureUpgradedAuto;
        }

        private static void EnsureUpgradedAuto()
        {
            if (_isUpgrading || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(SentinelPath) || !Directory.Exists(EnemyMatsDir))
            {
                UpgradeAllEnemyMaterials(false);
            }
        }

        [MenuItem("Tools/The Last Empire/Upgrade All Enemy PBR Materials (Fix Clay Look)")]
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
                Shader detailShader = Shader.Find(ShaderName);
                if (detailShader == null)
                {
                    Debug.LogWarning($"[EnemyPbrMaterialUpgrader] Shader '{ShaderName}' not ready yet — will retry after shader import.");
                    return;
                }

                Dictionary<SurfaceCategory, PbrTexPair> texLibrary = EnsureSharedPbrTextures();
                if (!Directory.Exists(EnemyMatsDir)) Directory.CreateDirectory(EnemyMatsDir);

                string[] searchDirs = new[]
                {
                    "Assets/GeneratedModels/Aliens",
                    "Assets/GeneratedModels/Zombies",
                    "Assets/GeneratedModels/Robots"
                };

                int modelsProcessed = 0;
                int materialsUpgraded = 0;

                // 1. Upgrade every FBX & OBJ model inside Aliens, Zombies, and Robots by remapping to detailed PBR external materials
                foreach (string dir in searchDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    string[] guids = AssetDatabase.FindAssets("t:Model", new[] { dir });
                    foreach (string guid in guids)
                    {
                        string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(modelPath)) continue;
                        if (modelPath.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)) continue;

                        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                        if (importer == null) continue;

                        string modelSlug = Path.GetFileNameWithoutExtension(modelPath);
                        bool isRobot = modelPath.IndexOf("/Robots/", StringComparison.OrdinalIgnoreCase) >= 0;

                        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                        var extMap = importer.GetExternalObjectMap();
                        bool needsReimport = false;

                        if (subAssets != null)
                        {
                            foreach (UnityEngine.Object obj in subAssets)
                            {
                                if (obj is Material embeddedMat)
                                {
                                    SurfaceCategory cat = ClassifyMaterial(embeddedMat.name, isRobot);
                                    if (cat == SurfaceCategory.TransparentGlassSkip) continue;

                                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedMat.name);
                                    Material targetMat = null;
                                    if (extMap.TryGetValue(id, out UnityEngine.Object existingObj) && existingObj is Material existingExtMat)
                                    {
                                        targetMat = existingExtMat;
                                    }
                                    else
                                    {
                                        string safeMatName = SanitizeFileName($"{modelSlug}__{embeddedMat.name}");
                                        string matPath = $"{EnemyMatsDir}/{safeMatName}.mat";
                                        targetMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                                        if (targetMat == null)
                                        {
                                            targetMat = new Material(embeddedMat);
                                            AssetDatabase.CreateAsset(targetMat, matPath);
                                        }
                                        importer.AddRemap(id, targetMat);
                                        needsReimport = true;
                                    }

                                    ApplyDetailedPbrMaterial(targetMat, embeddedMat, detailShader, texLibrary, cat);
                                    EditorUtility.SetDirty(targetMat);
                                    materialsUpgraded++;
                                }
                            }
                        }

                        if (needsReimport)
                        {
                            AssetDatabase.SaveAssets();
                            importer.SaveAndReimport();
                        }
                        modelsProcessed++;
                    }
                }

                // 2. Also upgrade any standalone enemy .mat files in Aliens/Materials, Zombies/Materials, Robots/Materials
                foreach (string dir in searchDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    bool isRobot = dir.IndexOf("/Robots", StringComparison.OrdinalIgnoreCase) >= 0;
                    string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { dir });
                    foreach (string guid in matGuids)
                    {
                        string matPath = AssetDatabase.GUIDToAssetPath(guid);
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (mat == null) continue;
                        SurfaceCategory cat = ClassifyMaterial(mat.name, isRobot);
                        if (cat == SurfaceCategory.TransparentGlassSkip) continue;
                        ApplyDetailedPbrMaterial(mat, mat, detailShader, texLibrary, cat);
                        EditorUtility.SetDirty(mat);
                        materialsUpgraded++;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"<color=#50FA7B>[EnemyPbrMaterialUpgrader]</color> Upgraded {materialsUpgraded} materials across {modelsProcessed} Enemy models (Zombies, Aliens, Robots) with triplanar PBR detail + normal maps!");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Enemy PBR Materials Upgraded",
                        $"อัปเกรด Texture & Normal Map (แก้ผิวดินน้ำมัน) ครบ {materialsUpgraded} Materials บนโมเดลศัตรู {modelsProcessed} ตัวเรียบร้อยแล้วครับ!", "OK");
                }
            }
            finally
            {
                _isUpgrading = false;
            }
        }

        private static SurfaceCategory ClassifyMaterial(string matName, bool isRobot)
        {
            string n = (matName ?? "").ToLowerInvariant();
            if (n.Contains("glassdome") || n.Contains("transparent")) return SurfaceCategory.TransparentGlassSkip;

            if (n.Contains("cyclopseye") || n.Contains("eyecore") || n.Contains("eyewhite") ||
                n.Contains("eyeglossy") || n.Contains("pupil") || n.Contains("glasses") ||
                n.Contains("lens") || n.Contains("visor") || n.Contains("saliva"))
            {
                return SurfaceCategory.GlossyEyeOrGlass;
            }

            if (n.Contains("fang") || n.Contains("claw") || n.Contains("nail") || n.Contains("bone") || n.Contains("tooth") || n.Contains("horn"))
            {
                return SurfaceCategory.BoneClaw;
            }

            if (n.Contains("silver") || n.Contains("gold") || n.Contains("metal") || n.Contains("steel") ||
                n.Contains("iron") || n.Contains("chrome") || n.Contains("buckle") || n.Contains("shackle") ||
                n.Contains("baton") || n.Contains("rebar") || n.Contains("blade") || n.Contains("gun") ||
                (isRobot && !n.Contains("glow") && !n.Contains("eye") && !n.Contains("core")))
            {
                return SurfaceCategory.BrushedMetal;
            }

            if (n.Contains("fur") || n.Contains("hair"))
            {
                return SurfaceCategory.FurHair;
            }

            if (n.Contains("vest") || n.Contains("shirt") || n.Contains("tank") || n.Contains("pants") ||
                n.Contains("hat") || n.Contains("scarf") || n.Contains("sash") || n.Contains("cloth") ||
                n.Contains("coat") || n.Contains("cape") || n.Contains("hood") || n.Contains("boot") ||
                n.Contains("leather") || n.Contains("band") || n.Contains("belt") || n.Contains("uniform") ||
                n.Contains("jeans") || n.Contains("shoe") || n.Contains("fabric") || n.Contains("print"))
            {
                return SurfaceCategory.WornCloth;
            }

            if (n.Contains("obsidian") || n.Contains("shadow") || n.Contains("chitin") || n.Contains("cranium") ||
                n.Contains("aliensmart") || n.Contains("alieninv") || n.Contains("purple") || n.Contains("indigo") ||
                n.Contains("elecblue") || n.Contains("void"))
            {
                return SurfaceCategory.ChitinBio;
            }

            return SurfaceCategory.OrganicFlesh;
        }

        private static void ApplyDetailedPbrMaterial(
            Material targetMat,
            Material sourceMat,
            Shader detailShader,
            Dictionary<SurfaceCategory, PbrTexPair> texLib,
            SurfaceCategory cat)
        {
            if (targetMat == null || detailShader == null) return;

            // Preserve original base color, custom UV texture, and emission before switching shader
            Color baseColor = Color.white;
            if (sourceMat != null)
            {
                if (sourceMat.HasProperty("_BaseColor")) baseColor = sourceMat.GetColor("_BaseColor");
                else if (sourceMat.HasProperty("_Color")) baseColor = sourceMat.GetColor("_Color");
            }

            Texture baseTex = null;
            if (targetMat.HasProperty("_BaseMap")) baseTex = targetMat.GetTexture("_BaseMap");
            if (baseTex == null && sourceMat != null && sourceMat.HasProperty("_BaseMap")) baseTex = sourceMat.GetTexture("_BaseMap");
            if (baseTex == null && sourceMat != null && sourceMat.HasProperty("_MainTex")) baseTex = sourceMat.GetTexture("_MainTex");

            Color emissionColor = Color.black;
            bool hadEmission = false;
            if (targetMat.HasProperty("_EmissionColor") && targetMat.IsKeywordEnabled("_EMISSION"))
            {
                emissionColor = targetMat.GetColor("_EmissionColor");
                hadEmission = emissionColor.maxColorComponent > 0.01f;
            }
            if (!hadEmission && sourceMat != null && sourceMat.HasProperty("_EmissionColor"))
            {
                Color srcEmit = sourceMat.GetColor("_EmissionColor");
                if (srcEmit.maxColorComponent > 0.01f)
                {
                    emissionColor = srcEmit;
                    hadEmission = true;
                }
            }

            // Switch to our Triplanar PBR Detail Lit shader
            targetMat.shader = detailShader;
            targetMat.SetColor("_BaseColor", baseColor);
            targetMat.SetColor("_Color", baseColor);

            bool hasPaintedBaseMap = baseTex != null;
            targetMat.SetFloat("_UseBaseMap", hasPaintedBaseMap ? 1f : 0f);
            if (hasPaintedBaseMap)
            {
                targetMat.SetTexture("_BaseMap", baseTex);
            }

            // Re-apply glowing emission accents
            string lowerName = targetMat.name.ToLowerInvariant();
            if (!hadEmission)
            {
                if (lowerName.Contains("alieninv_cyclopseye")) { emissionColor = new Color(0.95f, 0.25f, 1.0f) * 2.6f; hadEmission = true; }
                else if (lowerName.Contains("alieninv_eyecore")) { emissionColor = new Color(1.0f, 0.90f, 1.0f) * 3.4f; hadEmission = true; }
                else if (lowerName.Contains("alieninv_void")) { emissionColor = new Color(0.58f, 0.10f, 0.92f) * 1.3f; hadEmission = true; }
                else if (lowerName.Contains("aliensmart_cyclopseye")) { emissionColor = new Color(0.98f, 0.94f, 0.14f) * 2.0f; hadEmission = true; }
                else if (lowerName.Contains("brainglow")) { emissionColor = new Color(0.98f, 0.22f, 0.68f) * 1.5f; hadEmission = true; }
                else if (lowerName.Contains("brainvein") || lowerName.Contains("neonlime")) { emissionColor = new Color(0.58f, 0.98f, 0.16f) * 1.2f; hadEmission = true; }
                else if (lowerName.Contains("boil_glow") || lowerName.Contains("acidglow")) { emissionColor = new Color(0.95f, 0.72f, 0.10f) * 1.6f; hadEmission = true; }
            }
            targetMat.SetColor("_EmissionColor", hadEmission ? emissionColor : Color.black);

            // Configure Triplanar Detail Textures, Normal Strength, Smoothness, Metallic per material class
            SurfaceCategory texCat = (cat == SurfaceCategory.GlossyEyeOrGlass) ? SurfaceCategory.OrganicFlesh : cat;
            if (texLib.TryGetValue(texCat, out PbrTexPair pair))
            {
                targetMat.SetTexture("_DetailMap", pair.detailAlbedo);
                targetMat.SetTexture("_DetailBumpMap", pair.detailNormal);
            }

            switch (cat)
            {
                case SurfaceCategory.GlossyEyeOrGlass:
                    targetMat.SetFloat("_DetailScale", 2.0f);
                    targetMat.SetFloat("_DetailStrength", 0.08f);
                    targetMat.SetFloat("_BumpScale", 0.0f);
                    targetMat.SetFloat("_CavityStrength", 0.0f);
                    targetMat.SetFloat("_Smoothness", 0.92f);
                    targetMat.SetFloat("_Metallic", lowerName.Contains("glasses") ? 0.25f : 0.0f);
                    break;

                case SurfaceCategory.BrushedMetal:
                    targetMat.SetFloat("_DetailScale", 3.6f);
                    targetMat.SetFloat("_DetailStrength", hasPaintedBaseMap ? 0.28f : 0.65f);
                    targetMat.SetFloat("_BumpScale", 0.85f);
                    targetMat.SetFloat("_CavityStrength", 0.24f);
                    targetMat.SetFloat("_Smoothness", 0.64f);
                    targetMat.SetFloat("_Metallic", 0.78f);
                    break;

                case SurfaceCategory.FurHair:
                    targetMat.SetFloat("_DetailScale", 5.2f);
                    targetMat.SetFloat("_DetailStrength", hasPaintedBaseMap ? 0.25f : 0.85f);
                    targetMat.SetFloat("_BumpScale", 1.35f);
                    targetMat.SetFloat("_CavityStrength", 0.32f);
                    targetMat.SetFloat("_Smoothness", 0.10f);
                    targetMat.SetFloat("_Metallic", 0.0f);
                    break;

                case SurfaceCategory.WornCloth:
                    targetMat.SetFloat("_DetailScale", 4.8f);
                    targetMat.SetFloat("_DetailStrength", hasPaintedBaseMap ? 0.22f : 0.75f);
                    targetMat.SetFloat("_BumpScale", 1.15f);
                    targetMat.SetFloat("_CavityStrength", 0.28f);
                    targetMat.SetFloat("_Smoothness", lowerName.Contains("leather") || lowerName.Contains("boot") || lowerName.Contains("snake") ? 0.34f : 0.12f);
                    targetMat.SetFloat("_Metallic", 0.0f);
                    break;

                case SurfaceCategory.ChitinBio:
                    targetMat.SetFloat("_DetailScale", 4.0f);
                    targetMat.SetFloat("_DetailStrength", hasPaintedBaseMap ? 0.22f : 0.68f);
                    targetMat.SetFloat("_BumpScale", 1.10f);
                    targetMat.SetFloat("_CavityStrength", 0.26f);
                    targetMat.SetFloat("_Smoothness", 0.48f);
                    targetMat.SetFloat("_Metallic", 0.12f);
                    break;

                case SurfaceCategory.BoneClaw:
                    targetMat.SetFloat("_DetailScale", 5.0f);
                    targetMat.SetFloat("_DetailStrength", 0.62f);
                    targetMat.SetFloat("_BumpScale", 0.95f);
                    targetMat.SetFloat("_CavityStrength", 0.25f);
                    targetMat.SetFloat("_Smoothness", 0.56f);
                    targetMat.SetFloat("_Metallic", 0.05f);
                    break;

                default: // OrganicFlesh (Zombies, AlienNormal, AlienHook, Parasite)
                    bool isWetFlesh = lowerName.Contains("parasite") || lowerName.Contains("tongue") || lowerName.Contains("gumlip") || lowerName.Contains("flesh");
                    targetMat.SetFloat("_DetailScale", 4.2f);
                    targetMat.SetFloat("_DetailStrength", hasPaintedBaseMap ? 0.20f : 0.76f);
                    targetMat.SetFloat("_BumpScale", 1.18f);
                    targetMat.SetFloat("_CavityStrength", 0.30f);
                    targetMat.SetFloat("_Smoothness", isWetFlesh ? 0.46f : 0.24f);
                    targetMat.SetFloat("_Metallic", 0.0f);
                    break;
            }
        }

        private static Dictionary<SurfaceCategory, PbrTexPair> EnsureSharedPbrTextures()
        {
            if (!Directory.Exists(SharedTexDir)) Directory.CreateDirectory(SharedTexDir);
            var dict = new Dictionary<SurfaceCategory, PbrTexPair>();

            dict[SurfaceCategory.OrganicFlesh] = EnsureTexturePair("PBR_OrganicFlesh", SurfaceCategory.OrganicFlesh, 512);
            dict[SurfaceCategory.ChitinBio]    = EnsureTexturePair("PBR_ChitinBio",    SurfaceCategory.ChitinBio,    512);
            dict[SurfaceCategory.FurHair]      = EnsureTexturePair("PBR_FurHair",      SurfaceCategory.FurHair,      512);
            dict[SurfaceCategory.WornCloth]    = EnsureTexturePair("PBR_WornCloth",    SurfaceCategory.WornCloth,    512);
            dict[SurfaceCategory.BrushedMetal] = EnsureTexturePair("PBR_BrushedMetal", SurfaceCategory.BrushedMetal, 512);
            dict[SurfaceCategory.BoneClaw]     = EnsureTexturePair("PBR_BoneClaw",     SurfaceCategory.BoneClaw,     512);

            return dict;
        }

        private static PbrTexPair EnsureTexturePair(string baseName, SurfaceCategory cat, int size)
        {
            string albedoPath = $"{SharedTexDir}/{baseName}_Detail.png";
            string normalPath = $"{SharedTexDir}/{baseName}_Normal.png";

            if (!File.Exists(albedoPath) || !File.Exists(normalPath))
            {
                float[,] h = GenerateTileableHeightMap(cat, size);
                WriteAlbedoFromHeight(h, cat, size, albedoPath);
                WriteNormalFromHeight(h, size, normalPath, GetNormalStrengthForCategory(cat));

                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureImporter(albedoPath, isNormal: false);
            ConfigureImporter(normalPath, isNormal: true);

            return new PbrTexPair
            {
                detailAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
                detailNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)
            };
        }

        private static void ConfigureImporter(string assetPath, bool isNormal)
        {
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;
            bool changed = false;
            TextureImporterType desiredType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != desiredType) { ti.textureType = desiredType; changed = true; }
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; changed = true; }
            if (ti.anisoLevel < 4) { ti.anisoLevel = 4; changed = true; }
            if (changed) ti.SaveAndReimport();
        }

        private static float GetNormalStrengthForCategory(SurfaceCategory cat)
        {
            switch (cat)
            {
                case SurfaceCategory.FurHair: return 4.2f;
                case SurfaceCategory.WornCloth: return 3.6f;
                case SurfaceCategory.ChitinBio: return 4.0f;
                case SurfaceCategory.BrushedMetal: return 2.8f;
                case SurfaceCategory.BoneClaw: return 3.2f;
                default: return 3.5f;
            }
        }

        private static float[,] GenerateTileableHeightMap(SurfaceCategory cat, int size)
        {
            float[,] map = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float h = 0.5f;

                    switch (cat)
                    {
                        case SurfaceCategory.OrganicFlesh:
                        {
                            // Multi-octave tileable organic skin mottling + fine pores + vein creases
                            float f1 = TileableFbm(u, v, 6, 4, 11);
                            float pores = TileableFbm(u, v, 32, 2, 37);
                            float ridge = 1f - Mathf.Abs(TileableFbm(u, v, 10, 3, 73) * 2f - 1f);
                            h = Mathf.Clamp01(0.45f * f1 + 0.30f * pores + 0.25f * ridge * ridge);
                            break;
                        }
                        case SurfaceCategory.ChitinBio:
                        {
                            // Cellular hexagonal/organic alien bio-scales + ridged veins
                            float cells = TileableWorley(u, v, 12, 19);
                            float fine = TileableFbm(u, v, 24, 3, 53);
                            h = Mathf.Clamp01(0.65f * (1f - cells) + 0.35f * fine);
                            break;
                        }
                        case SurfaceCategory.FurHair:
                        {
                            // High-frequency directional hair/fur strands + clumping
                            float strands = TileableFbmAniso(u, v, 48, 6, 3, 29);
                            float clumps = TileableFbmAniso(u, v, 14, 3, 2, 61);
                            h = Mathf.Clamp01(0.62f * strands + 0.38f * clumps);
                            break;
                        }
                        case SurfaceCategory.WornCloth:
                        {
                            // Crisp cross-hatch woven fabric + subtle battle grime wear
                            float weaveX = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 44f);
                            float weaveY = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 44f);
                            float checker = ((Mathf.FloorToInt(u * 44f) + Mathf.FloorToInt(v * 44f)) & 1) == 0 ? weaveX : weaveY;
                            float wear = TileableFbm(u, v, 8, 3, 89);
                            h = Mathf.Clamp01(0.55f * checker + 0.45f * wear);
                            break;
                        }
                        case SurfaceCategory.BrushedMetal:
                        {
                            // Brushed metallic alloy streaks + subtle micro-scratches & panel variation
                            float brush = TileableFbmAniso(u, v, 6, 48, 3, 41);
                            float grit = TileableFbm(u, v, 16, 2, 97);
                            float seam = (Mathf.Abs((u * 4f) % 1f - 0.5f) > 0.485f || Mathf.Abs((v * 4f) % 1f - 0.5f) > 0.485f) ? 0.18f : 0.55f;
                            h = Mathf.Clamp01(0.52f * brush + 0.28f * grit + 0.20f * seam);
                            break;
                        }
                        case SurfaceCategory.BoneClaw:
                        {
                            // Longitudinal keratin/bone striations
                            float striation = TileableFbmAniso(u, v, 36, 4, 3, 107);
                            float cracks = TileableFbm(u, v, 12, 2, 131);
                            h = Mathf.Clamp01(0.68f * striation + 0.32f * cracks);
                            break;
                        }
                    }

                    map[x, y] = h;
                }
            }
            return map;
        }

        private static void WriteAlbedoFromHeight(float[,] h, SurfaceCategory cat, int size, string path)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Center around 0.5 (128) so multiplying by 2.0 in shader modulates cleanly without darkening/brightening average color!
                    float v = h[x, y];
                    float contrast = (cat == SurfaceCategory.BrushedMetal) ? 0.44f : 0.58f;
                    float g = Mathf.Clamp01(0.5f + (v - 0.5f) * contrast);
                    byte b = (byte)Mathf.RoundToInt(g * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void WriteNormalFromHeight(float[,] h, int size, string path, float strength)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;
                    float dx = (h[xp, y] - h[xm, y]) * strength;
                    float dy = (h[x, yp] - h[x, ym]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(r, g, b, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // --- Seamless Periodic Value / Worley Noise Helpers ---
        private static float Hash2D(int x, int y, int periodX, int periodY, int seed)
        {
            int px = ((x % periodX) + periodX) % periodX;
            int py = ((y % periodY) + periodY) % periodY;
            unchecked
            {
                int n = px * 374761393 + py * 668265263 + seed * 1442695041;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= (n >> 16);
                return (n & 0x7fffffff) / 2147483647f;
            }
        }

        private static float TileableValueNoise(float u, float v, int periodX, int periodY, int seed)
        {
            float fx = u * periodX;
            float fy = v * periodY;
            int ix = Mathf.FloorToInt(fx);
            int iy = Mathf.FloorToInt(fy);
            float tx = fx - ix;
            float ty = fy - iy;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            float a = Hash2D(ix, iy, periodX, periodY, seed);
            float b = Hash2D(ix + 1, iy, periodX, periodY, seed);
            float c = Hash2D(ix, iy + 1, periodX, periodY, seed);
            float d = Hash2D(ix + 1, iy + 1, periodX, periodY, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float TileableFbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            return TileableFbmAniso(u, v, basePeriod, basePeriod, octaves, seed);
        }

        private static float TileableFbmAniso(float u, float v, int px, int py, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += TileableValueNoise(u, v, px << o, py << o, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        private static float TileableWorley(float u, float v, int cells, int seed)
        {
            float fx = u * cells;
            float fy = v * cells;
            int ix = Mathf.FloorToInt(fx);
            int iy = Mathf.FloorToInt(fy);
            float minDist = 10f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx;
                    int cy = iy + dy;
                    float rx = cx + Hash2D(cx, cy, cells, cells, seed);
                    float ry = cy + Hash2D(cx, cy, cells, cells, seed + 59);
                    float d = Mathf.Sqrt((fx - rx) * (fx - rx) + (fy - ry) * (fy - ry));
                    if (d < minDist) minDist = d;
                }
            }
            return Mathf.Clamp01(minDist);
        }

        private static string SanitizeFileName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }
            return raw.Replace(' ', '_');
        }
    }
}
#endif
