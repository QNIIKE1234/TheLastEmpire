#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically configures import settings, matte URP materials, AnimatorControllers,
    /// and Prefab model replacements for the 3 rigged Chibi Zombie models:
    /// - Chibi_ZombieNormal_Rigged.fbx  -> ZombieNormal.prefab
    /// - Chibi_ZombieRunner_Rigged.fbx  -> ZombieRunner.prefab (Police Cop Runner)
    /// - Chibi_ZombieDog_Rigged.fbx     -> ZombieDog.prefab    (Undead Doberman)
    /// </summary>
    public class ChibiZombieAssetPostprocessor : AssetPostprocessor
    {
        private bool IsTargetZombieFbx()
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return assetPath.Contains("Chibi_ZombieNormal_Rigged") ||
                   assetPath.Contains("Chibi_ZombieRunner_Rigged") ||
                   assetPath.Contains("Chibi_ZombieDog_Rigged") ||
                   assetPath.Contains("Chibi_ZombieBoss_Rigged") ||
                   assetPath.Contains("Chibi_ZombieBoomer_Rigged") ||
                   assetPath.Contains("Chibi_ZombieSpitter_Rigged") ||
                   assetPath.Contains("Chibi_ZombieTank_Rigged");
        }

        private void OnPreprocessModel()
        {
            if (!IsTargetZombieFbx()) return;

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.resampleCurves = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsTargetZombieFbx()) return;

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }
            if (clips == null || clips.Length == 0) return;

            bool modified = false;
            for (int i = 0; i < clips.Length; i++)
            {
                string cName = clips[i].name;
                bool shouldLoop = cName.EndsWith("Idle") || cName.EndsWith("Walk") || cName.EndsWith("Run") || cName.EndsWith("Charge");
                if (clips[i].loopTime != shouldLoop)
                {
                    clips[i].loopTime = shouldLoop;
                    clips[i].loop = shouldLoop;
                    modified = true;
                }
            }

            if (modified)
            {
                importer.clipAnimations = clips;
            }
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (!IsTargetZombieFbx() || material == null) return;
            ChibiZombieModelBuilder.ApplyZombieMaterialFinish(material);
        }
    }

    [InitializeOnLoad]
    public static class ChibiZombieModelBuilder
    {
        private const string ZombieFbxDir = "Assets/GeneratedModels/Zombies";
        private const string AnimCtrlDir = "Assets/Resources_Addressable/Image/Animation";
        private const string CharacterPrefabDir = "Assets/Resources_Addressable/Prefabs/Character";
        private const string RunnerDeformedRunClipPath = AnimCtrlDir + "/Chibi_ZombieRunner_DeformedRun.anim";

        private struct ZombieSetupSpec
        {
            public string fbxPath;
            public string controllerPath;
            public string prefabPath;
            public string childRootName;
            public string[] legacyChildNamesToRemove;
            public Vector3 localPosition;
            public Vector3 localScale;
        }

        private static readonly ZombieSetupSpec[] Specs = new ZombieSetupSpec[]
        {
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieNormal_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieNormal_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieNormal.prefab",
                childRootName = "Chibi_ZombieNormal_Model",
                legacyChildNamesToRemove = new[] { "Zombie_01_Worker_Dummy", "Zombie_02_Civilian_Dummy", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieRunner_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieRunner_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieRunner.prefab",
                childRootName = "Chibi_ZombieRunner_Model",
                legacyChildNamesToRemove = new[] { "Zombie_03_Police_Dummy", "Zombie_01_Worker_Dummy", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieDog_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieDog_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieDog.prefab",
                childRootName = "Chibi_ZombieDog_Model",
                legacyChildNamesToRemove = new[] { "Zombie_Dog_Dummy", "Capsule" },
                localPosition = new Vector3(0f, -0.45f, 0f),
                localScale = Vector3.one
            },
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieBoss_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieBoss_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieLeader.prefab",
                childRootName = "Chibi_ZombieBoss_Model",
                legacyChildNamesToRemove = new[] { "Boss_Zombie_Leader", "Boss_Zombie_Tank", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(1.25f, 1.25f, 1.25f)
            },
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieBoomer_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieBoomer_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieBoomer.prefab",
                childRootName = "Chibi_ZombieBoomer_Model",
                legacyChildNamesToRemove = new[] { "Zombie_Fat_Dummy", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(1.12f, 1.12f, 1.12f)
            },
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieSpitter_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieSpitter_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieSpitter.prefab",
                childRootName = "Chibi_ZombieSpitter_Model",
                legacyChildNamesToRemove = new[] { "Zombie_01_Worker_Dummy", "Zombie_Spitter_Dummy", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new ZombieSetupSpec
            {
                fbxPath = $"{ZombieFbxDir}/Chibi_ZombieTank_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Chibi_ZombieTank_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/ZombieTank.prefab",
                childRootName = "Chibi_ZombieTank_Model",
                legacyChildNamesToRemove = new[] { "Boss_Zombie_Tank", "Zombie_Tank_Dummy", "Capsule" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = new Vector3(1.26f, 1.26f, 1.26f)
            }
        };

        static ChibiZombieModelBuilder()
        {
            EditorApplication.delayCall += EnsureZombieModelsBuiltAuto;
        }

        private static void EnsureZombieModelsBuiltAuto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Check if controllers or prefab models still need to be linked
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

        [MenuItem("Tools/The Last Empire/Apply 3 Chibi Zombie Models (Normal, Runner, Dog)")]
        public static void BuildFromMenu()
        {
            BuildAndApplyAll(true);
        }

        public static void BuildAndApplyAll(bool showDialog)
        {
            if (!Directory.Exists(AnimCtrlDir))
            {
                Directory.CreateDirectory(AnimCtrlDir);
            }

            int updatedCount = 0;
            foreach (var spec in Specs)
            {
                if (!File.Exists(spec.fbxPath))
                {
                    Debug.LogWarning($"[ChibiZombieModelBuilder] Missing FBX at {spec.fbxPath}");
                    continue;
                }

                EnsureFbxImportSettingsAndMaterials(spec.fbxPath);
                AnimatorController ctrl = CreateOrUpdateAnimatorController(spec.fbxPath, spec.controllerPath);
                if (ApplyModelToPrefab(spec, ctrl))
                {
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#50FA7B>[ChibiZombieModelBuilder]</color> Configured & linked {updatedCount}/3 rigged Chibi Zombie models (Normal, Runner Cop, Zombie Dog)!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Chibi Zombie Models Applied",
                    $"Successfully rigged, animated, and applied {updatedCount} Chibi Zombie models to ZombieNormal, ZombieRunner, and ZombieDog prefabs!",
                    "OK");
            }
        }

        public static void ApplyZombieMaterialFinish(Material mat)
        {
            if (mat == null) return;
            string n = mat.name;
            bool isGlossy = n.Contains("Glossy") || n.Contains("Eye") || n.Contains("Lens") || n.Contains("VisorBlack") || n.Contains("Boil_Glow") || n.Contains("Glasses") || n.Contains("AcidGlow");
            bool isMetal = n.Contains("Gold") || n.Contains("Silver") || n.Contains("Baton_Silver") || n.Contains("Buckle") ||
                           n.Contains("Steel") || n.Contains("Rebar") || n.Contains("Iron");

            float smoothness = isGlossy ? 0.85f : (isMetal ? 0.58f : 0.08f);
            float metallic = isMetal ? 0.68f : 0.0f;

            string mappedTexPath = null;
            if (n.Contains("Boomer_Face_Texture")) mappedTexPath = $"{ZombieFbxDir}/Textures/Chibi_ZombieBoomer_Face.png";
            else if (n.Contains("Boomer_Hair")) mappedTexPath = $"{ZombieFbxDir}/Textures/Chibi_ZombieBoomer_Hair.png";
            else if (n.Contains("Spitter_Face_Texture")) mappedTexPath = $"{ZombieFbxDir}/Textures/Chibi_ZombieSpitter_Face.png";
            else if (n.Contains("Spitter_Hair")) mappedTexPath = $"{ZombieFbxDir}/Textures/Chibi_ZombieSpitter_Hair.png";
            else if (n.Contains("Tank_Face_Texture")) mappedTexPath = $"{ZombieFbxDir}/Textures/Chibi_ZombieTank_Face.png";
            else if (n.Contains("Tank_TattooArm")) mappedTexPath = $"{ZombieFbxDir}/Textures/Chibi_ZombieTank_Tattoo.png";

            if (!string.IsNullOrEmpty(mappedTexPath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(mappedTexPath);
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                }
            }
            else if (n.Contains("Boil_Glow"))
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.72f, 0.08f, 1.0f) * 2.2f);
                }
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        }

        private static void EnsureFbxImportSettingsAndMaterials(string fbxPath)
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
                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }
                if (clips != null && clips.Length > 0)
                {
                    bool clipModified = false;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        string cName = clips[i].name;
                        bool shouldLoop = cName.EndsWith("Idle") || cName.EndsWith("Walk") || cName.EndsWith("Run") || cName.EndsWith("Charge");
                        if (clips[i].loopTime != shouldLoop)
                        {
                            clips[i].loopTime = shouldLoop;
                            clips[i].loop = shouldLoop;
                            clipModified = true;
                        }
                    }
                    if (clipModified)
                    {
                        importer.clipAnimations = clips;
                        reimportNeeded = true;
                    }
                }

                (string matName, string texPath)[] remaps = null;
                if (fbxPath.Contains("Chibi_ZombieBoomer_Rigged"))
                {
                    remaps = new (string matName, string texPath)[]
                    {
                        ("Mat_Boomer_Face_Texture", $"{ZombieFbxDir}/Textures/Chibi_ZombieBoomer_Face.png"),
                        ("Mat_Boomer_Hair", $"{ZombieFbxDir}/Textures/Chibi_ZombieBoomer_Hair.png"),
                    };
                }
                else if (fbxPath.Contains("Chibi_ZombieSpitter_Rigged"))
                {
                    remaps = new (string matName, string texPath)[]
                    {
                        ("Mat_Spitter_Face_Texture", $"{ZombieFbxDir}/Textures/Chibi_ZombieSpitter_Face.png"),
                        ("Mat_Spitter_Hair", $"{ZombieFbxDir}/Textures/Chibi_ZombieSpitter_Hair.png"),
                    };
                }
                else if (fbxPath.Contains("Chibi_ZombieTank_Rigged"))
                {
                    remaps = new (string matName, string texPath)[]
                    {
                        ("Mat_Tank_Face_Texture", $"{ZombieFbxDir}/Textures/Chibi_ZombieTank_Face.png"),
                        ("Mat_Tank_TattooArm", $"{ZombieFbxDir}/Textures/Chibi_ZombieTank_Tattoo.png"),
                    };
                }

                if (remaps != null)
                {
                    var extMap = importer.GetExternalObjectMap();
                    string matDir = $"{ZombieFbxDir}/Materials";
                    if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);

                    foreach (var rm in remaps)
                    {
                        if (!File.Exists(rm.texPath)) continue;
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rm.texPath);
                        if (tex == null)
                        {
                            AssetDatabase.ImportAsset(rm.texPath, ImportAssetOptions.ForceSynchronousImport);
                            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rm.texPath);
                        }
                        if (tex == null) continue;

                        string matPath = $"{matDir}/{rm.matName}.mat";
                        Material extMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (extMat == null)
                        {
                            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                            extMat = new Material(urpLit);
                            AssetDatabase.CreateAsset(extMat, matPath);
                        }
                        if (extMat.HasProperty("_BaseMap")) extMat.SetTexture("_BaseMap", tex);
                        if (extMat.HasProperty("_MainTex")) extMat.SetTexture("_MainTex", tex);
                        if (extMat.HasProperty("_BaseColor")) extMat.SetColor("_BaseColor", Color.white);
                        if (extMat.HasProperty("_Color")) extMat.SetColor("_Color", Color.white);
                        if (extMat.HasProperty("_Smoothness")) extMat.SetFloat("_Smoothness", 0.08f);
                        if (extMat.HasProperty("_Metallic")) extMat.SetFloat("_Metallic", 0f);
                        EditorUtility.SetDirty(extMat);

                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), rm.matName);
                        if (!extMap.TryGetValue(id, out Object cur) || cur != extMat)
                        {
                            importer.AddRemap(id, extMat);
                            reimportNeeded = true;
                        }
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
                        ApplyZombieMaterialFinish(mat);
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
        }

        private static AnimatorController CreateOrUpdateAnimatorController(string fbxPath, string controllerPath)
        {
            Dictionary<string, AnimationClip> clipsByShortName = new Dictionary<string, AnimationClip>();
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            Avatar modelAvatar = null;

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
                    else if (obj is Avatar av)
                    {
                        modelAvatar = av;
                    }
                }
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            // Ensure parameters exist
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsRunning", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsSitting", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = EnsureStateWithMotion(sm, "Idle", FindClip(clipsByShortName, "Idle"));
            EnsureStateWithMotion(sm, "Walk", FindClip(clipsByShortName, "Walk", "Run", "Idle"));

            Motion runMotion = FindClip(clipsByShortName, "Run", "Walk", "Idle");
            float runSpeed = 1.0f;
            if (controllerPath.Contains("ZombieRunner"))
            {
                AnimationClip deformedRun = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunnerDeformedRunClipPath);
                if (deformedRun != null)
                {
                    runMotion = deformedRun;
                    runSpeed = 1.25f;
                }
            }
            AnimatorState runState = EnsureStateWithMotion(sm, "Run", runMotion);
            if (runState != null)
            {
                runState.speed = runSpeed;
            }

            AnimatorState slashState = EnsureStateWithMotion(sm, "Slash1", FindClip(clipsByShortName, "Slash1", "Run", "Idle"));
            EnsureStateWithMotion(sm, "Death", FindClip(clipsByShortName, "Death", "Idle"));

            sm.defaultState = idleState;

            // Ensure Slash1 automatically returns to Idle/Run on completion
            EnsureExitToIdleTransition(slashState, idleState);

            // Boss Zombie additional combat & skill states
            if (clipsByShortName.ContainsKey("Slash2"))
            {
                AnimatorState slash2State = EnsureStateWithMotion(sm, "Slash2", FindClip(clipsByShortName, "Slash2", "Slash1"));
                EnsureExitToIdleTransition(slash2State, idleState);
            }
            if (clipsByShortName.ContainsKey("GroundSlam"))
            {
                AnimatorState slamState = EnsureStateWithMotion(sm, "GroundSlam", FindClip(clipsByShortName, "GroundSlam", "Slash1"));
                EnsureExitToIdleTransition(slamState, idleState);
            }
            if (clipsByShortName.ContainsKey("Charge"))
            {
                EnsureStateWithMotion(sm, "Charge", FindClip(clipsByShortName, "Charge", "Run"));
            }
            if (clipsByShortName.ContainsKey("Throw"))
            {
                AnimatorState throwState = EnsureStateWithMotion(sm, "Throw", FindClip(clipsByShortName, "Throw", "Slash2", "Slash1"));
                EnsureExitToIdleTransition(throwState, idleState);
            }
            if (clipsByShortName.ContainsKey("RageRoar"))
            {
                AnimatorState roarState = EnsureStateWithMotion(sm, "RageRoar", FindClip(clipsByShortName, "RageRoar", "Idle"));
                EnsureExitToIdleTransition(roarState, idleState);
            }
            if (clipsByShortName.ContainsKey("Explode"))
            {
                EnsureStateWithMotion(sm, "Explode", FindClip(clipsByShortName, "Explode", "Slash1"));
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
                exitTrans.exitTime = 0.90f;
                exitTrans.duration = 0.10f;
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
                if (dict.TryGetValue(c, out AnimationClip clip) && clip != null)
                {
                    return clip;
                }
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

        private static bool ApplyModelToPrefab(ZombieSetupSpec spec, AnimatorController controller)
        {
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.fbxPath);
            if (fbxPrefab == null || !File.Exists(spec.prefabPath)) return false;

            Avatar fbxAvatar = null;
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(spec.fbxPath);
            if (subAssets != null)
            {
                foreach (Object obj in subAssets)
                {
                    if (obj is Avatar av)
                    {
                        fbxAvatar = av;
                        break;
                    }
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(spec.prefabPath);
            if (root == null) return false;

            try
            {
                // Remove old static dummy meshes and previous instances of this child
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

                foreach (GameObject oldObj in toDestroy)
                {
                    Object.DestroyImmediate(oldObj);
                }

                // Instantiate the rigged FBX model under the Zombie prefab
                GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, root.transform);
                modelInst.name = spec.childRootName;
                modelInst.transform.localPosition = spec.localPosition;
                modelInst.transform.localRotation = Quaternion.identity;
                modelInst.transform.localScale = spec.localScale;

                Animator anim = modelInst.GetComponent<Animator>();
                if (anim == null)
                {
                    anim = modelInst.AddComponent<Animator>();
                }
                anim.runtimeAnimatorController = controller;
                if (fbxAvatar != null)
                {
                    anim.avatar = fbxAvatar;
                }
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
