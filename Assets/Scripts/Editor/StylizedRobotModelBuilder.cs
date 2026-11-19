#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically configures Generic rig, clip looping, matte URP + emissive finishes,
    /// AnimatorControllers, and prefab model replacement for all 8 Rigged Robot Enemies:
    /// RobotNormal, RobotBlade, RobotDrone, RobotGunner, RobotKamikaze, RobotLaser, RobotTanker, RobotLeader.
    /// </summary>
    public class StylizedRobotAssetPostprocessor : AssetPostprocessor
    {
        private bool IsTargetRobotFbx()
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return assetPath.Contains("Assets/GeneratedModels/Robots/Rigged/Robot_") &&
                   assetPath.EndsWith("_Rigged.fbx");
        }

        private void OnPreprocessModel()
        {
            if (!IsTargetRobotFbx()) return;

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.resampleCurves = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsTargetRobotFbx()) return;

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
            if (!IsTargetRobotFbx() || material == null) return;
            StylizedRobotModelBuilder.ApplyRobotMaterialFinish(material);
        }
    }

    [InitializeOnLoad]
    public static class StylizedRobotModelBuilder
    {
        private const string RobotFbxDir = "Assets/GeneratedModels/Robots/Rigged";
        private const string AnimCtrlDir = "Assets/Resources_Addressable/Image/Animation";
        private const string CharacterPrefabDir = "Assets/Resources_Addressable/Prefabs/Character";

        private struct RobotSetupSpec
        {
            public string fbxPath;
            public string controllerPath;
            public string prefabPath;
            public string childRootName;
            public string[] legacyChildNamesToRemove;
            public Vector3 localPosition;
            public Vector3 localScale;
        }

        private static readonly RobotSetupSpec[] Specs = new RobotSetupSpec[]
        {
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Normal_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Normal_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotNormal.prefab",
                childRootName = "Robot_Normal_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Normal", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Blade_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Blade_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotBlade.prefab",
                childRootName = "Robot_Blade_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Blade", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Drone_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Drone_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotDrone.prefab",
                childRootName = "Robot_Drone_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Drone", "Capsule", "default" },
                localPosition = new Vector3(0f, -0.5f, 0f),
                localScale = Vector3.one
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Gunner_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Gunner_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotGunner.prefab",
                childRootName = "Robot_Gunner_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Gunner", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Kamikaze_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Kamikaze_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotKamikaze.prefab",
                childRootName = "Robot_Kamikaze_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Kamikaze", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.25f, 0f),
                localScale = new Vector3(1.05f, 1.05f, 1.05f)
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Laser_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Laser_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotLaser.prefab",
                childRootName = "Robot_Laser_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Laser", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.0f, 0f),
                localScale = Vector3.one
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Tanker_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Tanker_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotTanker.prefab",
                childRootName = "Robot_Tanker_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Tanker", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.25f, 0f),
                localScale = new Vector3(1.08f, 1.08f, 1.08f)
            },
            new RobotSetupSpec
            {
                fbxPath = $"{RobotFbxDir}/Robot_Leader_Rigged.fbx",
                controllerPath = $"{AnimCtrlDir}/Robot_Leader_Controller.controller",
                prefabPath = $"{CharacterPrefabDir}/RobotLeader.prefab",
                childRootName = "Robot_Leader_Rigged_Model",
                legacyChildNamesToRemove = new[] { "Robot_Leader", "Capsule", "default" },
                localPosition = new Vector3(0f, -1.50f, 0f),
                localScale = new Vector3(1.18f, 1.18f, 1.18f)
            }
        };

        static StylizedRobotModelBuilder()
        {
            EditorApplication.delayCall += EnsureRobotModelsBuiltAuto;
        }

        private static void EnsureRobotModelsBuiltAuto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

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

        [MenuItem("Tools/The Last Empire/Apply All 8 Rigged Robot Models")]
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
                    Debug.LogWarning($"[StylizedRobotModelBuilder] Missing FBX at {spec.fbxPath}");
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

            Debug.Log($"<color=#50FA7B>[StylizedRobotModelBuilder]</color> Configured & linked {updatedCount}/{Specs.Length} rigged Robot Enemy models!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Rigged Robot Models Applied",
                    $"Successfully rigged, animated, and applied {updatedCount}/{Specs.Length} Robot models to all Robot prefabs!",
                    "OK");
            }
        }

        public static void ApplyRobotMaterialFinish(Material mat)
        {
            if (mat == null) return;
            string n = mat.name;

            // Strictly match README_Model_Color_Standard.md matte finish rules
            float smoothness = 0.08f;
            float metallic = 0.0f;

            if (n.Contains("GlowRed"))
            {
                smoothness = 0.45f;
                metallic = 0.0f;
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.12f, 0.08f, 1.0f) * 2.8f);
                }
            }
            else if (n.Contains("GlowAmber"))
            {
                smoothness = 0.45f;
                metallic = 0.0f;
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.56f, 0.06f, 1.0f) * 2.5f);
                }
            }
            else if (n.Contains("Steel") || n.Contains("Brass"))
            {
                smoothness = 0.48f;
                metallic = 0.70f;
            }
            else if (n.Contains("Graphite") || n.Contains("ArmorGrey"))
            {
                smoothness = 0.18f;
                metallic = 0.08f;
            }
            else
            {
                // Matte painted armor: Crimson, HazardYellow, Charcoal
                smoothness = 0.08f;
                metallic = 0.0f;
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_SpecColor") && metallic < 0.3f)
            {
                mat.SetColor("_SpecColor", new Color(0.05f, 0.05f, 0.05f, 1f));
            }
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

                if (reimportNeeded)
                {
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
                        ApplyRobotMaterialFinish(mat);
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
            AnimatorState slashState = EnsureStateWithMotion(sm, "Slash1", FindClip(clipsByShortName, "Slash1", "Attack", "Idle"));
            EnsureStateWithMotion(sm, "Death", FindClip(clipsByShortName, "Death", "Idle"));

            sm.defaultState = idleState;
            EnsureExitToIdleTransition(slashState, idleState);

            if (clipsByShortName.ContainsKey("Attack"))
            {
                AnimatorState atkState = EnsureStateWithMotion(sm, "Attack", FindClip(clipsByShortName, "Attack", "Slash1"));
                EnsureExitToIdleTransition(atkState, idleState);
            }
            if (clipsByShortName.ContainsKey("Charge"))
            {
                EnsureStateWithMotion(sm, "Charge", FindClip(clipsByShortName, "Charge", "Idle"));
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

        private static bool ApplyModelToPrefab(RobotSetupSpec spec, AnimatorController controller)
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
                List<GameObject> toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    Transform ch = root.transform.GetChild(i);
                    if (ch == null) continue;

                    // Preserve UI Canvas
                    if (ch.name == "Canvas") continue;

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
