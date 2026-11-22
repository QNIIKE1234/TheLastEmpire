#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically imports rigged Alien FBXs (Alien_Smart_Rigged, Alien_Hook_Rigged),
    /// configures URP Transparent Glass (for AlienSmart's lightbulb brain dome) + Emissive bio-materials,
    /// builds AnimatorControllers (Idle, Walk, Run, Slash1, Hook, Death),
    /// and replaces the legacy static OBJ meshes inside AlienSmart.prefab & AlienHook.prefab.
    /// </summary>
    public class AlienAssetPostprocessor : AssetPostprocessor
    {
        private bool IsTargetAlienFbx()
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return assetPath.Contains("Alien_Smart_Rigged") ||
                   assetPath.Contains("Alien_Hook_Rigged") ||
                   assetPath.Contains("Alien_Normal_Rigged") ||
                   assetPath.Contains("Alien_Invisible_Rigged") ||
                   assetPath.Contains("Alien_Crawler_Rigged");
        }

        private void OnPreprocessModel()
        {
            if (!IsTargetAlienFbx()) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.resampleCurves = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsTargetAlienFbx()) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool modified = false;
            for (int i = 0; i < clips.Length; i++)
            {
                string cName = clips[i].name;
                bool shouldLoop = cName.EndsWith("Idle") || cName.EndsWith("Walk") || cName.EndsWith("Run");
                if (clips[i].loopTime != shouldLoop)
                {
                    clips[i].loopTime = shouldLoop;
                    clips[i].loop = shouldLoop;
                    modified = true;
                }
            }
            if (modified) importer.clipAnimations = clips;
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (!IsTargetAlienFbx() || material == null) return;
            AlienModelBuilder.ApplyAlienMaterialFinish(material);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null) return;
            foreach (string path in importedAssets)
            {
                if (!string.IsNullOrEmpty(path) &&
                    (path.Contains("Alien_Smart_Rigged.fbx") ||
                     path.Contains("Alien_Hook_Rigged.fbx") ||
                     path.Contains("Alien_Normal_Rigged.fbx") ||
                     path.Contains("Alien_Invisible_Rigged.fbx") ||
                     path.Contains("Alien_Crawler_Rigged.fbx")))
                {
                    EditorApplication.delayCall += AlienModelBuilder.EnsureAlienModelsBuiltAuto;
                    break;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class AlienModelBuilder
    {
        private static bool _isBuilding;
        private const string AlienFbxDir = "Assets/GeneratedModels/Aliens";
        private const string AnimCtrlDir = "Assets/Resources_Addressable/Image/Animation";
        private const string CharacterPrefabDir = "Assets/Resources_Addressable/Prefabs/Character";

        private struct AlienSetupSpec
        {
            public string fbxPath;
            public string controllerPath;
            public string prefabPath;
            public string childRootName;
            public string[] legacyChildNamesToRemove;
            public Vector3 localPosition;
            public Vector3 localScale;
        }

        private static readonly AlienSetupSpec[] Specs = new[]
        {
            new AlienSetupSpec
            {
                fbxPath = $"{AlienFbxDir}/Alien_Smart_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Alien_Smart_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/AlienSmart.prefab",
                childRootName = "Alien_Smart_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Alien_Smart", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(0.95f, 0.95f, 0.95f)
            },
            new AlienSetupSpec
            {
                fbxPath = $"{AlienFbxDir}/Alien_Hook_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Alien_Hook_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/AlienHook.prefab",
                childRootName = "Alien_Hook_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Alien_Hook", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(0.95f, 0.95f, 0.95f)
            },
            new AlienSetupSpec
            {
                fbxPath = $"{AlienFbxDir}/Alien_Normal_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Alien_Normal_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/AlienNormal.prefab",
                childRootName = "Alien_Normal_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Alien_Normal", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(0.95f, 0.95f, 0.95f)
            },
            new AlienSetupSpec
            {
                fbxPath = $"{AlienFbxDir}/Alien_Invisible_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Alien_Invisible_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/AlienInvisible.prefab",
                childRootName = "Alien_Invisible_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Alien_Invisible", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(1.0f, 1.0f, 1.0f)
            },
            new AlienSetupSpec
            {
                fbxPath = $"{AlienFbxDir}/Alien_Crawler_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Alien_Crawler_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/AlienCrawler.prefab",
                childRootName = "Alien_Crawler_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Alien_Crawler", "Capsule" },
                localPosition = new Vector3(0f, -0.85f, 0f),
                localScale = new Vector3(1.0f, 1.0f, 1.0f)
            }
        };

        static AlienModelBuilder()
        {
            EditorApplication.delayCall += EnsureAlienModelsBuiltAuto;
        }

        public static void EnsureAlienModelsBuiltAuto()
        {
            if (_isBuilding || EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool needsBuild = false;
            foreach (var spec in Specs)
            {
                if (!File.Exists(spec.fbxPath)) continue;
                if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(spec.controllerPath) == null)
                {
                    needsBuild = true;
                    break;
                }
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath);
                if (prefab != null && prefab.transform.Find(spec.childRootName) == null)
                {
                    needsBuild = true;
                    break;
                }
            }

            if (needsBuild)
            {
                BuildAndApplyAll(false);
            }
        }

        [MenuItem("Tools/The Last Empire/Apply Rigged Alien Models (Smart, Hook, Normal & Invisible)")]
        public static void BuildFromMenu()
        {
            BuildAndApplyAll(true);
        }

        public static void BuildAndApplyAll(bool showDialog)
        {
            if (_isBuilding) return;
            _isBuilding = true;
            try
            {
                if (!Directory.Exists(AnimCtrlDir)) Directory.CreateDirectory(AnimCtrlDir);

                int updated = 0;
                foreach (var spec in Specs)
                {
                    if (!File.Exists(spec.fbxPath)) continue;
                    EnsureFbxSettingsAndMaterials(spec.fbxPath);
                    AnimatorController ctrl = CreateOrUpdateAnimatorController(spec.fbxPath, spec.controllerPath);
                    if (ApplyModelToPrefab(spec, ctrl)) updated++;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"<color=#50FA7B>[AlienModelBuilder]</color> Configured & linked {updated}/{Specs.Length} rigged Alien models (AlienSmart, AlienHook, AlienNormal)!");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Alien Models Updated",
                        $"อัปเดตโมเดล + Material + Animator ของ AlienSmart, AlienHook และ AlienNormal เรียบร้อยแล้ว ({updated}/{Specs.Length})!", "OK");
                }
            }
            finally
            {
                _isBuilding = false;
            }
        }

        public static void ApplyAlienMaterialFinish(Material mat)
        {
            if (mat == null) return;
            string n = mat.name;

            if (n.Contains("GlassDome"))
            {
                // Translucent glowing cyan-violet glass lightbulb dome so the pulsing brain inside is clearly visible!
                ConfigureUrpTransparentGlass(mat, new Color(0.32f, 0.88f, 0.98f, 0.28f), new Color(0.16f, 0.55f, 0.90f) * 0.45f);
                return;
            }

            if (n.Contains("AlienInv_CyclopsEye"))
            {
                // Iconic glowing Enderman purple-magenta single cyclops eye!
                SetEmission(mat, new Color(0.95f, 0.25f, 1.0f) * 2.6f, smoothness: 0.95f);
            }
            else if (n.Contains("AlienInv_EyeCore"))
            {
                SetEmission(mat, new Color(1.0f, 0.90f, 1.0f) * 3.4f, smoothness: 0.98f);
            }
            else if (n.Contains("AlienInv_VoidPulse") || n.Contains("AlienInv_VoidMouth"))
            {
                SetEmission(mat, new Color(0.58f, 0.10f, 0.92f) * 1.4f, smoothness: 0.72f);
            }
            else if (n.Contains("AlienInv_Obsidian") || n.Contains("AlienInv_ShadowTrim"))
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.56f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.14f);
            }
            else if (n.Contains("AlienHook_"))
            {
                // Panoptes horror-flesh palette (warm peach-tan flesh, jet-black hair/crest, crimson accents, ghostly white eyeball)
                if (n.Contains("CyclopsEye"))
                {
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.94f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
                }
                else if (n.Contains("BioGlow"))
                {
                    SetEmission(mat, new Color(0.85f, 0.20f, 0.22f) * 0.75f, smoothness: 0.72f);
                }
                else if (n.Contains("CraniumBio") || n.Contains("Purple") || n.Contains("Indigo"))
                {
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.68f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.06f);
                }
                else
                {
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.48f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.02f);
                }
            }
            else if (n.Contains("AlienCrawler_"))
            {
                if (n.Contains("EyeWhite") || n.Contains("Tongue"))
                {
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.86f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
                }
                else if (n.Contains("Fur"))
                {
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.14f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
                }
                else
                {
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.46f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.02f);
                }
            }
            else if (n.Contains("CyclopsEye"))
            {
                SetEmission(mat, new Color(0.98f, 0.94f, 0.14f) * 2.0f, smoothness: 0.92f);
            }
            else if (n.Contains("BrainGlow"))
            {
                SetEmission(mat, new Color(0.98f, 0.22f, 0.68f) * 1.65f, smoothness: 0.62f);
            }
            else if (n.Contains("BrainVein") || n.Contains("BioGlow") || n.Contains("NeonLime"))
            {
                SetEmission(mat, new Color(0.58f, 0.98f, 0.16f) * 1.35f, smoothness: 0.55f);
            }
            else if (n.Contains("CraniumBio"))
            {
                SetEmission(mat, new Color(0.46f, 0.18f, 0.88f) * 0.85f, smoothness: 0.82f);
            }
            else if (n.Contains("ElecBlue"))
            {
                SetEmission(mat, new Color(0.12f, 0.52f, 0.96f) * 0.65f, smoothness: 0.58f);
            }
            else if (n.Contains("Pupil"))
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
            }
            else if (n.Contains("AlienNormal_Flesh") || n.Contains("AlienNormal_BloodVein"))
            {
                // Glossy wet fleshy worm skin finish
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.66f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.02f);
            }
            else if (n.Contains("AlienNormal_GumLip") || n.Contains("AlienNormal_Tongue") || n.Contains("AlienNormal_Saliva"))
            {
                // Extra wet glossy mouth/tongue/saliva
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
            }
            else if (n.Contains("AlienNormal_BoneFang"))
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.72f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            }
            else
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.10f);
            }
        }

        private static void SetEmission(Material mat, Color hdrColor, float smoothness)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", hdrColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        }

        private static void ConfigureUrpTransparentGlass(Material mat, Color baseColor, Color emissionColor)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.94f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void EnsureFbxSettingsAndMaterials(string fbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer != null)
            {
                bool reimportNeeded = false;
                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    reimportNeeded = true;
                }

                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    bool clipMod = false;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        string cName = clips[i].name;
                        bool shouldLoop = cName.EndsWith("Idle") || cName.EndsWith("Walk") || cName.EndsWith("Run");
                        if (clips[i].loopTime != shouldLoop)
                        {
                            clips[i].loopTime = shouldLoop;
                            clips[i].loop = shouldLoop;
                            clipMod = true;
                        }
                    }
                    if (clipMod)
                    {
                        importer.clipAnimations = clips;
                        reimportNeeded = true;
                    }
                }

                // Remap AlienSmart's glass dome to an external URP Transparent Glass material so transparency always persists cleanly
                if (fbxPath.Contains("Alien_Smart_Rigged"))
                {
                    string matDir = $"{AlienFbxDir}/Materials";
                    if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);
                    string glassMatPath = $"{matDir}/Mat_AlienSmart_GlassDome.mat";
                    Material glassMat = AssetDatabase.LoadAssetAtPath<Material>(glassMatPath);
                    if (glassMat == null)
                    {
                        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        glassMat = new Material(urpLit);
                        AssetDatabase.CreateAsset(glassMat, glassMatPath);
                    }
                    ApplyAlienMaterialFinish(glassMat);
                    EditorUtility.SetDirty(glassMat);

                    var extMap = importer.GetExternalObjectMap();
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Mat_AlienSmart_GlassDome");
                    if (!extMap.TryGetValue(id, out Object cur) || cur != glassMat)
                    {
                        importer.AddRemap(id, glassMat);
                        reimportNeeded = true;
                    }
                }

                if (reimportNeeded)
                {
                    AssetDatabase.SaveAssets();
                    importer.SaveAndReimport();
                }
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (subAssets != null)
            {
                foreach (Object sub in subAssets)
                {
                    if (sub is Material mat)
                    {
                        ApplyAlienMaterialFinish(mat);
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
        }

        private static AnimatorController CreateOrUpdateAnimatorController(string fbxPath, string controllerPath)
        {
            Dictionary<string, AnimationClip> clipsByShortName = new Dictionary<string, AnimationClip>();
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (subAssets != null)
            {
                foreach (Object obj in subAssets)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        string shortName = clip.name;
                        int pipeIdx = shortName.LastIndexOf('|');
                        if (pipeIdx >= 0 && pipeIdx < shortName.Length - 1)
                        {
                            shortName = shortName.Substring(pipeIdx + 1);
                        }
                        clipsByShortName[shortName] = clip;
                    }
                }
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsRunning", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsSitting", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = EnsureStateWithMotion(sm, "Idle", FindClip(clipsByShortName, "Idle"));
            EnsureStateWithMotion(sm, "Walk", FindClip(clipsByShortName, "Walk", "Run", "Idle"));
            EnsureStateWithMotion(sm, "Run", FindClip(clipsByShortName, "Run", "Walk", "Idle"));
            AnimatorState slashState = EnsureStateWithMotion(sm, "Slash1", FindClip(clipsByShortName, "Slash1", "Run", "Idle"));
            EnsureStateWithMotion(sm, "Death", FindClip(clipsByShortName, "Death", "Idle"));

            sm.defaultState = idleState;
            EnsureExitToIdleTransition(slashState, idleState);

            if (clipsByShortName.ContainsKey("Hook"))
            {
                AnimatorState hookState = EnsureStateWithMotion(sm, "Hook", FindClip(clipsByShortName, "Hook", "Slash1"));
                EnsureExitToIdleTransition(hookState, idleState);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureExitToIdleTransition(AnimatorState actionState, AnimatorState idleState)
        {
            if (actionState != null && idleState != null && actionState.transitions.Length == 0)
            {
                AnimatorStateTransition exitTrans = actionState.AddTransition(idleState);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = 0.92f;
                exitTrans.duration = 0.08f;
            }
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }
            controller.AddParameter(paramName, type);
        }

        private static AnimationClip FindClip(Dictionary<string, AnimationClip> dict, params string[] candidates)
        {
            foreach (string c in candidates)
            {
                if (dict.TryGetValue(c, out AnimationClip clip) && clip != null) return clip;
            }
            return null;
        }

        private static AnimatorState EnsureStateWithMotion(AnimatorStateMachine sm, string stateName, Motion motion)
        {
            foreach (var child in sm.states)
            {
                if (child.state != null && child.state.name == stateName)
                {
                    child.state.motion = motion;
                    return child.state;
                }
            }
            AnimatorState newState = sm.AddState(stateName);
            newState.motion = motion;
            return newState;
        }

        private static bool ApplyModelToPrefab(AlienSetupSpec spec, AnimatorController controller)
        {
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.fbxPath);
            if (fbxPrefab == null || !File.Exists(spec.prefabPath)) return false;

            Avatar fbxAvatar = null;
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(spec.fbxPath);
            if (subAssets != null)
            {
                foreach (Object obj in subAssets)
                {
                    if (obj is Avatar av) { fbxAvatar = av; break; }
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(spec.prefabPath);
            if (root == null) return false;

            try
            {
                List<GameObject> toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    Transform ch = root.transform.GetChild(i);
                    if (ch == null) continue;
                    if (ch.name == spec.childRootName)
                    {
                        toDestroy.Add(ch.gameObject);
                        continue;
                    }
                    foreach (string legacyName in spec.legacyChildNamesToRemove)
                    {
                        if (ch.name.Contains(legacyName))
                        {
                            toDestroy.Add(ch.gameObject);
                            break;
                        }
                    }
                }
                foreach (GameObject oldObj in toDestroy) Object.DestroyImmediate(oldObj);

                GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, root.transform);
                modelInst.name = spec.childRootName;
                modelInst.transform.localPosition = spec.localPosition;
                modelInst.transform.localRotation = Quaternion.identity;
                modelInst.transform.localScale = spec.localScale;

                Animator anim = modelInst.GetComponent<Animator>();
                if (anim == null) anim = modelInst.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                if (fbxAvatar != null) anim.avatar = fbxAvatar;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(root, spec.prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
