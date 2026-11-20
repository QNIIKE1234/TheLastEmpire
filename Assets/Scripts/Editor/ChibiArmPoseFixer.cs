#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
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

        private static readonly string[] ChibiHeroNames = new string[]
        {
            "Chibi_TacticalGirl",
            "Chibi_HeavyStriker",
            "Chibi_BlueOperative",
            "Chibi_PunkGirl",
            "Chibi_CurlyGirl",
            "Chibi_WhiteRonin",
            "Chibi_DreadBrawler"
        };

        private static bool IsChibiHeroFbx(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (string hero in ChibiHeroNames)
            {
                if (path.Contains(hero + "_Rigged")) return true;
            }
            return false;
        }

        private static string ResolveChibiTexturePath(string fbxPath, string matName)
        {
            if (string.IsNullOrEmpty(fbxPath) || string.IsNullOrEmpty(matName)) return null;

            string hero = null;
            foreach (string h in ChibiHeroNames)
            {
                if (fbxPath.Contains(h))
                {
                    hero = h;
                    break;
                }
            }
            if (hero == null) return null;

            if (matName.Contains("Face_Texture"))
                return $"Assets/GeneratedModels/Textures/{hero}_Face.png";
            if (matName.Contains("Undercut"))
                return $"Assets/GeneratedModels/Textures/{hero}_Undercut.png";
            if (matName.Contains("Hair_Blonde"))
                return $"Assets/GeneratedModels/Textures/{hero}_HairBlonde.png";
            if (matName.Contains("Hair_Pink"))
                return $"Assets/GeneratedModels/Textures/{hero}_HairPink.png";
            if (matName.Contains("Hair_Purple") || matName.Contains("Hair_Indigo") || matName.Contains("Hair_Blue") || matName.Contains("Hair_Berry") || matName.Contains("Hair_White") || matName.Contains("Hair_Dread"))
                return $"Assets/GeneratedModels/Textures/{hero}_Hair.png";

            return null;
        }

        private void OnPreprocessModel()
        {
            if (!IsChibiHeroFbx(assetPath)) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (string.IsNullOrEmpty(assetPath) || material == null)
            {
                return;
            }

            if (IsChibiHeroFbx(assetPath))
            {
                ApplyTacticalGirlMaterialSettings(material, assetPath);
            }
            else if (IsMatteBuildingFbx(assetPath))
            {
                ApplyCraftBuildingMatteSettings(material);
            }
        }

        public static void ApplyTacticalGirlMaterialSettings(Material material, string fbxPath = null)
        {
            if (material == null) return;

            string matName = material.name;
            bool isEye = matName.Contains("Glossy_Black") ||
                         matName.Contains("Eye_Amber") ||
                         matName.Contains("Eye_Gold_Hi") ||
                         matName.Contains("Eye_White") ||
                         matName.Contains("Eye_Red") ||
                         matName.Contains("Lens_Cobalt") ||
                         matName.Contains("Visor_Red");
            bool isSilver = matName.Contains("Metal_Silver") || matName.Contains("Zipper_Teeth") || matName.Contains("Bat_Nail");
            bool isArmor = matName.Contains("Armor_Grey") || matName.Contains("Suit_Plate");

            float smoothness = isEye ? 0.88f : (isSilver ? 0.48f : (isArmor ? 0.18f : 0.08f));
            float metallic = isSilver ? 0.70f : (isArmor ? 0.08f : 0.0f);

            string texPath = ResolveChibiTexturePath(fbxPath, matName);
            if (!string.IsNullOrEmpty(texPath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", tex);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                }
            }

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

        private const string ChibiExternalMatDir = "Assets/GeneratedModels/Materials";

        [MenuItem("Tools/TheLastEmpire/Repair All Chibi Hero Face & Hair Textures")]
        public static void RepairAllChibiHeroTexturesMenu()
        {
            EnsureChibiHeroesRemappedAndSynced(forceReimport: true);
        }

        private static Material GetOrCreatePersistentTexturedMat(string matAssetName, string texPath)
        {
            if (string.IsNullOrEmpty(texPath) || !File.Exists(texPath)) return null;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }
            if (tex == null) return null;

            if (!Directory.Exists(ChibiExternalMatDir))
            {
                Directory.CreateDirectory(ChibiExternalMatDir);
            }

            string matPath = $"{ChibiExternalMatDir}/{matAssetName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool created = false;
            if (mat == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
                created = true;
            }

            bool dirty = created;
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != tex)
            {
                mat.SetTexture("_BaseMap", tex);
                dirty = true;
            }
            if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != tex)
            {
                mat.SetTexture("_MainTex", tex);
                dirty = true;
            }
            if (mat.HasProperty("_BaseColor") && mat.GetColor("_BaseColor") != Color.white)
            {
                mat.SetColor("_BaseColor", Color.white);
                dirty = true;
            }
            if (mat.HasProperty("_Color") && mat.GetColor("_Color") != Color.white)
            {
                mat.SetColor("_Color", Color.white);
                dirty = true;
            }
            if (mat.HasProperty("_Smoothness") && Mathf.Abs(mat.GetFloat("_Smoothness") - 0.08f) > 0.01f)
            {
                mat.SetFloat("_Smoothness", 0.08f);
                dirty = true;
            }
            if (mat.HasProperty("_Metallic") && Mathf.Abs(mat.GetFloat("_Metallic")) > 0.01f)
            {
                mat.SetFloat("_Metallic", 0f);
                dirty = true;
            }
            if (mat.HasProperty("_SpecColor"))
            {
                mat.SetColor("_SpecColor", new Color(0.05f, 0.05f, 0.05f, 1f));
            }

            if (dirty)
            {
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static readonly (string hero, string fbxMatName, string texFile)[] ChibiHeroTextureRemaps =
        {
            ("Chibi_TacticalGirl",  "Mat_Face_Texture", "Chibi_TacticalGirl_Face.png"),
            ("Chibi_TacticalGirl",  "Mat_Hair_Purple",  "Chibi_TacticalGirl_Hair.png"),

            ("Chibi_HeavyStriker",  "Mat_Face_Texture", "Chibi_HeavyStriker_Face.png"),
            ("Chibi_HeavyStriker",  "Mat_Hair_Indigo",  "Chibi_HeavyStriker_Hair.png"),
            ("Chibi_HeavyStriker",  "Mat_Undercut",     "Chibi_HeavyStriker_Undercut.png"),

            ("Chibi_BlueOperative", "Mat_Face_Texture", "Chibi_BlueOperative_Face.png"),
            ("Chibi_BlueOperative", "Mat_Hair_Blue",    "Chibi_BlueOperative_Hair.png"),

            ("Chibi_PunkGirl",      "Mat_Face_Texture", "Chibi_PunkGirl_Face.png"),
            ("Chibi_PunkGirl",      "Mat_Hair_Blonde",  "Chibi_PunkGirl_HairBlonde.png"),
            ("Chibi_PunkGirl",      "Mat_Hair_Pink",    "Chibi_PunkGirl_HairPink.png"),

            ("Chibi_CurlyGirl",     "Mat_Face_Texture", "Chibi_CurlyGirl_Face.png"),
            ("Chibi_CurlyGirl",     "Mat_Hair_Berry",   "Chibi_CurlyGirl_Hair.png"),

            ("Chibi_WhiteRonin",    "Mat_Face_Texture", "Chibi_WhiteRonin_Face.png"),
            ("Chibi_WhiteRonin",    "Mat_Hair_White",   "Chibi_WhiteRonin_Hair.png"),

            ("Chibi_DreadBrawler",  "Mat_Face_Texture", "Chibi_DreadBrawler_Face.png"),
            ("Chibi_DreadBrawler",  "Mat_Hair_Dread",   "Chibi_DreadBrawler_Hair.png"),
            ("Chibi_DreadBrawler",  "Mat_Undercut",     "Chibi_DreadBrawler_Undercut.png"),
        };

        private static bool _postImportQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (_postImportQueued || importedAssets == null) return;
            foreach (string path in importedAssets)
            {
                if (IsChibiHeroFbx(path))
                {
                    _postImportQueued = true;
                    EditorApplication.delayCall += () =>
                    {
                        _postImportQueued = false;
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        if (EnsureChibiHeroesRemappedAndSynced(forceReimport: false))
                        {
                            AssetDatabase.SaveAssets();
                        }
                    };
                    break;
                }
            }
        }

        private static bool EnsureChibiHeroesRemappedAndSynced(bool forceReimport)
        {
            bool changed = false;
            foreach (string hero in ChibiHeroNames)
            {
                string fbxPath = $"Assets/GeneratedModels/{hero}_Rigged.fbx";
                if (!File.Exists(fbxPath)) continue;

                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                }
                bool needsReimport = forceReimport;

                if (importer != null)
                {
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                    {
                        importer.animationType = ModelImporterAnimationType.Generic;
                        needsReimport = true;
                        changed = true;
                    }
                    if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        needsReimport = true;
                        changed = true;
                    }

                    var externalMap = importer.GetExternalObjectMap();
                    foreach (var entry in ChibiHeroTextureRemaps)
                    {
                        if (entry.hero != hero) continue;

                        string texPath = $"Assets/GeneratedModels/Textures/{entry.texFile}";
                        string extMatName = $"{hero}_{entry.fbxMatName}";
                        Material extMat = GetOrCreatePersistentTexturedMat(extMatName, texPath);
                        if (extMat == null) continue;

                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.fbxMatName);
                        if (!externalMap.TryGetValue(id, out Object mappedObj) || mappedObj != extMat)
                        {
                            importer.AddRemap(id, extMat);
                            needsReimport = true;
                            changed = true;
                        }
                    }

                    if (needsReimport)
                    {
                        AssetDatabase.SaveAssets();
                        importer.SaveAndReimport();
                        changed = true;
                    }
                }

                string prefabPath = $"Assets/Resources_Addressable/Prefabs/Character/Model/{hero}_Rigged.prefab";
                if (!File.Exists(prefabPath))
                {
                    if (CreateChibiHeroPrefab(fbxPath, prefabPath, hero + "_Rigged"))
                    {
                        changed = true;
                    }
                }
                else if (SyncChibiPrefabSkinnedMeshes(fbxPath, prefabPath))
                {
                    changed = true;
                }

                // Ensure all 5 Chibi Hero prefabs are registered in Addressables (Characters_Group)
                AddressableAssetSettings addrSettings = AddressableAssetSettingsDefaultObject.Settings;
                AddressableAssetGroup charGroup = addrSettings != null ? addrSettings.FindGroup("Characters_Group") : null;
                if (addrSettings != null && charGroup != null && File.Exists(prefabPath))
                {
                    string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        string expectedAddress = $"Prefabs/Character/Model/{hero}_Rigged";
                        AddressableAssetEntry entry = addrSettings.FindAssetEntry(guid);
                        if (entry == null || entry.address != expectedAddress || entry.parentGroup != charGroup)
                        {
                            entry = addrSettings.CreateOrMoveEntry(guid, charGroup, readOnly: false, postEvent: false);
                            entry.address = expectedAddress;
                            entry.SetLabel("Character", true, true, false);
                            EditorUtility.SetDirty(charGroup);
                            EditorUtility.SetDirty(addrSettings);
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        [InitializeOnLoadMethod]
        private static void EnsureMaterialsUpdated()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                bool changed = EnsureChibiHeroesRemappedAndSynced(forceReimport: false);

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

        private const string PlayerAnimatorControllerPath = "Assets/Resources_Addressable/Image/Animation/Chibi_Player_Controller.controller";

        private static bool CreateChibiHeroPrefab(string fbxPath, string prefabPath, string rootName)
        {
            if (!File.Exists(fbxPath)) return false;

            GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxRoot == null) return false;

            string dir = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            GameObject instance = Object.Instantiate(fbxRoot);
            instance.name = rootName;
            try
            {
                Animator anim = instance.GetComponent<Animator>();
                if (anim == null) anim = instance.AddComponent<Animator>();

                RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorControllerPath);
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;

                Avatar fbxAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);
                if (fbxAvatar != null) anim.avatar = fbxAvatar;

                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"[ChibiTacticalGirlMaterialPostprocessor] Auto-created Chibi Hero prefab: {prefabPath}");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static bool SyncChibiPrefabSkinnedMeshes(string fbxPath, string prefabPath)
        {
            if (!File.Exists(fbxPath) || !File.Exists(prefabPath)) return false;

            GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (fbxRoot == null || prefabAsset == null) return false;

            SkinnedMeshRenderer[] fbxRenderers = fbxRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer[] prefabRenderers = prefabAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (fbxRenderers.Length == 0 || prefabRenderers.Length == 0) return false;

            bool needsUpdate = false;
            foreach (SkinnedMeshRenderer pSmr in prefabRenderers)
            {
                foreach (SkinnedMeshRenderer fSmr in fbxRenderers)
                {
                    if (pSmr.name != fSmr.name) continue;

                    if (pSmr.sharedMesh != fSmr.sharedMesh)
                    {
                        needsUpdate = true;
                        break;
                    }

                    Material[] pMats = pSmr.sharedMaterials;
                    Material[] fMats = fSmr.sharedMaterials;
                    if (pMats.Length != fMats.Length)
                    {
                        needsUpdate = true;
                        break;
                    }

                    for (int i = 0; i < fMats.Length; i++)
                    {
                        if (pMats[i] != fMats[i])
                        {
                            needsUpdate = true;
                            break;
                        }
                    }
                }
                if (needsUpdate) break;
            }

            if (!needsUpdate) return false;

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SkinnedMeshRenderer[] cRenderers = contents.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (SkinnedMeshRenderer cSmr in cRenderers)
                {
                    foreach (SkinnedMeshRenderer fSmr in fbxRenderers)
                    {
                        if (cSmr.name != fSmr.name) continue;
                        cSmr.sharedMesh = fSmr.sharedMesh;
                        cSmr.sharedMaterials = fSmr.sharedMaterials;
                        break;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log($"[ChibiTacticalGirlMaterialPostprocessor] Synced SkinnedMeshRenderer materials on {prefabPath} from {fbxPath}.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
#endif
