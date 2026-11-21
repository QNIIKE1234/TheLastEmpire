#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically creates/updates URP Lit materials for the 3 custom Chibi Anime NPCs
    /// (NPC_Elena_Medic_Rigged, NPC_Maya_Scout_Rigged, NPC_Roy_Veteran_Rigged), remaps their
    /// Face & Hair textures on the FBX importers, and rebuilds their Addressable prefabs in-place
    /// while preserving existing Prefab GUIDs, transform offsets, and AnimatorController.
    /// </summary>
    [InitializeOnLoad]
    public static class NpcCustomModelsAutoSync
    {
        private const string SyncVersionKey = "TLE_Custom3NpcChibiSync_v3";
        private const string MatDir = "Assets/GeneratedModels/Materials";
        private const string AnimatorControllerGuid = "2e7b3de4c1c44d742b1795abbffe89b4"; // Chibi_Player_Controller

        private struct NpcSpec
        {
            public string Name;
            public string FbxPath;
            public string PrefabPath;
            public string FaceMatName;
            public string FaceTexPath;
            public string HairMatName;
            public string HairTexPath;
        }

        private static readonly NpcSpec[] Specs = new[]
        {
            new NpcSpec
            {
                Name = "NPC_Elena_Medic_Rigged",
                FbxPath = "Assets/GeneratedModels/NPCs/NPC_Elena_Medic_Rigged.fbx",
                PrefabPath = "Assets/Resources_Addressable/Prefabs/Character/NPC_Elena_Medic_Rigged.prefab",
                FaceMatName = "Elena_Face_Tex",
                FaceTexPath = "Assets/GeneratedModels/Textures/NPC_Elena_Face.png",
                HairMatName = "Elena_Hair",
                HairTexPath = "Assets/GeneratedModels/Textures/NPC_Elena_Hair.png"
            },
            new NpcSpec
            {
                Name = "NPC_Maya_Scout_Rigged",
                FbxPath = "Assets/GeneratedModels/NPCs/NPC_Maya_Scout_Rigged.fbx",
                PrefabPath = "Assets/Resources_Addressable/Prefabs/Character/NPC_Maya_Scout_Rigged.prefab",
                FaceMatName = "Maya_Face_Tex",
                FaceTexPath = "Assets/GeneratedModels/Textures/NPC_Maya_Face.png",
                HairMatName = "Maya_Hair",
                HairTexPath = "Assets/GeneratedModels/Textures/NPC_Maya_Hair.png"
            },
            new NpcSpec
            {
                Name = "NPC_Roy_Veteran_Rigged",
                FbxPath = "Assets/GeneratedModels/NPCs/NPC_Roy_Veteran_Rigged.fbx",
                PrefabPath = "Assets/Resources_Addressable/Prefabs/Character/NPC_Roy_Veteran_Rigged.prefab",
                FaceMatName = "Roy_Face_Tex",
                FaceTexPath = "Assets/GeneratedModels/Textures/NPC_Roy_Face.png",
                HairMatName = "Roy_Hair",
                HairTexPath = "Assets/GeneratedModels/Textures/NPC_Roy_Hair.png"
            }
        };

        static NpcCustomModelsAutoSync()
        {
            EditorApplication.delayCall += RunAutoSyncOnce;
        }

        private static void RunAutoSyncOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetBool(SyncVersionKey, false)) return;

            bool ok = SyncAll(showDialog: false);
            if (ok)
            {
                EditorPrefs.SetBool(SyncVersionKey, true);
            }
        }

        [MenuItem("Tools/The Last Empire/Rebuild 3 Custom Chibi NPC Prefabs (Elena, Maya, Roy)")]
        public static void SyncFromMenu()
        {
            EditorPrefs.DeleteKey(SyncVersionKey);
            SyncAll(showDialog: true);
            EditorPrefs.SetBool(SyncVersionKey, true);
        }

        public static bool SyncAll(bool showDialog)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            if (urpLit == null)
            {
                Debug.LogWarning("[NpcCustomModelsAutoSync] Could not find URP Lit or Standard shader.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                Directory.CreateDirectory(MatDir);
                AssetDatabase.Refresh();
            }

            string ctrlPath = AssetDatabase.GUIDToAssetPath(AnimatorControllerGuid);
            RuntimeAnimatorController animatorController = !string.IsNullOrEmpty(ctrlPath)
                ? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath)
                : null;

            int updatedCount = 0;

            foreach (NpcSpec spec in Specs)
            {
                if (!File.Exists(spec.FbxPath))
                {
                    Debug.LogWarning($"[NpcCustomModelsAutoSync] FBX missing: {spec.FbxPath}");
                    continue;
                }

                // 1. Ensure Face & Hair URP Lit materials exist and link to PNG textures
                Material faceMat = CreateOrUpdateTexturedMaterial(
                    $"{MatDir}/{spec.Name}_{spec.FaceMatName}.mat",
                    spec.FaceTexPath,
                    urpLit,
                    smoothness: 0.12f);

                Material hairMat = CreateOrUpdateTexturedMaterial(
                    $"{MatDir}/{spec.Name}_{spec.HairMatName}.mat",
                    spec.HairTexPath,
                    urpLit,
                    smoothness: 0.22f);

                // 2. Remap Face & Hair materials on the FBX ModelImporter
                ModelImporter importer = AssetImporter.GetAtPath(spec.FbxPath) as ModelImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                        changed = true;
                    }

                    if (faceMat != null)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.FaceMatName), faceMat);
                        changed = true;
                    }
                    if (hairMat != null)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.HairMatName), hairMat);
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }

                // 3. Rebuild the Addressable NPC prefab cleanly over the existing prefab path (preserving GUID!)
                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
                if (fbxAsset == null)
                {
                    Debug.LogWarning($"[NpcCustomModelsAutoSync] Could not load FBX asset: {spec.FbxPath}");
                    continue;
                }

                // Preserve existing transform offsets & controller if prefab already exists
                Vector3 targetLocalPos = new Vector3(0f, -0.7f, 0f);
                Vector3 targetLocalScale = new Vector3(0.7f, 0.7f, 0.7f);
                Quaternion targetLocalRot = Quaternion.identity;
                RuntimeAnimatorController existingCtrl = animatorController;

                GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                if (existingPrefab != null)
                {
                    targetLocalPos = existingPrefab.transform.localPosition;
                    targetLocalRot = existingPrefab.transform.localRotation;
                    targetLocalScale = existingPrefab.transform.localScale;
                    Animator existingAnim = existingPrefab.GetComponent<Animator>();
                    if (existingAnim != null && existingAnim.runtimeAnimatorController != null)
                    {
                        existingCtrl = existingAnim.runtimeAnimatorController;
                    }
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                try
                {
                    // Unpack so we save a standalone prefab matching the original NPC prefab structure
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    instance.name = spec.Name;
                    instance.transform.localPosition = targetLocalPos;
                    instance.transform.localRotation = targetLocalRot;
                    instance.transform.localScale = targetLocalScale;

                    Animator anim = instance.GetComponent<Animator>();
                    if (anim == null) anim = instance.AddComponent<Animator>();
                    anim.runtimeAnimatorController = existingCtrl;
                    anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    anim.applyRootMotion = false;

                    string prefabDir = Path.GetDirectoryName(spec.PrefabPath);
                    if (!string.IsNullOrEmpty(prefabDir) && !Directory.Exists(prefabDir))
                    {
                        Directory.CreateDirectory(prefabDir);
                    }

                    PrefabUtility.SaveAsPrefabAsset(instance, spec.PrefabPath);
                    updatedCount++;
                    Debug.Log($"<color=#50FA7B>[NpcCustomModelsAutoSync]</color> Updated prefab & materials: {spec.PrefabPath}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Custom Chibi NPCs Updated",
                    $"อัปเดตโมเดล + Texture Material + Prefab ของ NPC ครบ {updatedCount}/3 ตัวแล้ว!\n\n- NPC_Elena_Medic_Rigged\n- NPC_Maya_Scout_Rigged\n- NPC_Roy_Veteran_Rigged",
                    "OK");
            }

            return updatedCount == Specs.Length;
        }

        private static Material CreateOrUpdateTexturedMaterial(string matPath, string texPath, Shader shader, float smoothness)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
