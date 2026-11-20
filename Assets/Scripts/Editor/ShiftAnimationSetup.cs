#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically configures Assets/GeneratedModels/Chibi_Shift_Animations.fbx as Generic Rig,
    /// extracts all 7 character-specific Shift Ability AnimationClips into Assets/Resources_Addressable/Image/Animation/,
    /// and wires them cleanly into Chibi_Player_Controller.controller (IsShiftActive + ShiftType 0..6).
    /// </summary>
    public class ShiftAnimationFbxPostprocessor : AssetPostprocessor
    {
        private const string ShiftFbxPath = "Assets/GeneratedModels/Chibi_Shift_Animations.fbx";

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Equals(ShiftFbxPath, StringComparison.OrdinalIgnoreCase)) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
        }

        private static bool _queued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (_queued || importedAssets == null) return;
            foreach (string path in importedAssets)
            {
                if (path.Replace('\\', '/').Equals(ShiftFbxPath, StringComparison.OrdinalIgnoreCase))
                {
                    _queued = true;
                    EditorApplication.delayCall += () =>
                    {
                        _queued = false;
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        ShiftAnimationSetup.InstallShiftAnimations(forceReinstall: false, showDialog: false);
                    };
                    break;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class ShiftAnimationSetup
    {
        private const string ShiftFbxPath = "Assets/GeneratedModels/Chibi_Shift_Animations.fbx";
        private const string AnimOutDir = "Assets/Resources_Addressable/Image/Animation";
        private const string ControllerPath = "Assets/Resources_Addressable/Image/Animation/Chibi_Player_Controller.controller";

        private readonly struct ShiftAnimSpec
        {
            public readonly int ShiftTypeInt;
            public readonly string StateName;
            public readonly string ActionKey;
            public readonly bool Loop;
            public readonly float Speed;

            public ShiftAnimSpec(int shiftTypeInt, string stateName, string actionKey, bool loop, float speed)
            {
                ShiftTypeInt = shiftTypeInt;
                StateName = stateName;
                ActionKey = actionKey;
                Loop = loop;
                Speed = speed;
            }
        }

        // Exact 1-to-1 mapping with ShiftAbilityType enum (0..6)
        private static readonly ShiftAnimSpec[] Specs = new[]
        {
            new ShiftAnimSpec(0, "Shift_AdrenalineRush",    "Shift_AdrenalineRush",    true,  1.45f), // Vanguard
            new ShiftAnimSpec(1, "Shift_UnstoppableCharge", "Shift_UnstoppableCharge", true,  1.50f), // Juggernaut
            new ShiftAnimSpec(2, "Shift_ShadowStep",        "Shift_ShadowStep",        true,  2.05f), // Phantom (+50% faster foot stride!)
            new ShiftAnimSpec(3, "Shift_EagleEye",          "Shift_EagleEye",          true,  1.00f), // Marksman
            new ShiftAnimSpec(4, "Shift_GrapplingHook",     "Shift_GrapplingHook",     true,  1.25f), // Scavenger
            new ShiftAnimSpec(5, "Shift_CycloneSwing",      "Shift_CycloneSwing",      true,  1.70f), // Rebel
            new ShiftAnimSpec(6, "Shift_SatchelVault",      "Shift_SatchelVault",      false, 1.85f), // Demolitionist
        };

        static ShiftAnimationSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                InstallShiftAnimations(forceReinstall: false, showDialog: false);
            };
        }

        [MenuItem("Tools/The Last Empire/Install 7 Character Shift Animations")]
        public static void InstallFromMenu()
        {
            InstallShiftAnimations(forceReinstall: true, showDialog: true);
        }

        public static void InstallShiftAnimations(bool forceReinstall, bool showDialog)
        {
            if (!File.Exists(ShiftFbxPath) || !File.Exists(ControllerPath)) return;

            ModelImporter importer = AssetImporter.GetAtPath(ShiftFbxPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(ShiftFbxPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(ShiftFbxPath) as ModelImporter;
            }
            if (importer != null &&
                (importer.animationType != ModelImporterAnimationType.Generic ||
                 importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.layers.Length == 0) return;

            AnimatorStateMachine baseSm = controller.layers[0].stateMachine;
            bool alreadyInstalled = FindState(baseSm, "Shift_SatchelVault") != null && HasParam(controller, "IsShiftActive");
            if (alreadyInstalled && !forceReinstall) return;

            // 1. Extract all 7 clips from Chibi_Shift_Animations.fbx into standalone .anim assets
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ShiftFbxPath);
            if (subAssets == null || subAssets.Length == 0) return;

            if (!Directory.Exists(AnimOutDir)) Directory.CreateDirectory(AnimOutDir);

            // Ensure Animator parameters
            EnsureParam(controller, "IsShiftActive", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "ShiftType", AnimatorControllerParameterType.Int);

            AnimatorState idleState = FindState(baseSm, "Idle") ?? baseSm.defaultState;
            int wiredCount = 0;

            for (int i = 0; i < Specs.Length; i++)
            {
                ShiftAnimSpec spec = Specs[i];
                AnimationClip srcClip = FindClipByActionKey(subAssets, spec.ActionKey);
                if (srcClip == null)
                {
                    Debug.LogWarning($"[ShiftAnimationSetup] Could not find clip '{spec.ActionKey}' inside {ShiftFbxPath}");
                    continue;
                }

                string animPath = $"{AnimOutDir}/Chibi_Base_Rig_{spec.StateName}.anim";
                AnimationClip extractedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                if (extractedClip == null)
                {
                    extractedClip = new AnimationClip();
                    EditorUtility.CopySerialized(srcClip, extractedClip);
                    extractedClip.name = $"Chibi_Base_Rig_{spec.StateName}";
                    ApplyLoopFlag(extractedClip, spec.Loop);
                    AssetDatabase.CreateAsset(extractedClip, animPath);
                }
                else if (forceReinstall)
                {
                    EditorUtility.CopySerialized(srcClip, extractedClip);
                    extractedClip.name = $"Chibi_Base_Rig_{spec.StateName}";
                    ApplyLoopFlag(extractedClip, spec.Loop);
                    EditorUtility.SetDirty(extractedClip);
                }

                // Add or update state in Base Layer
                AnimatorState state = FindState(baseSm, spec.StateName);
                if (state == null)
                {
                    state = baseSm.AddState(spec.StateName, new Vector3(820f, -140f + i * 68f, 0f));
                }
                state.motion = extractedClip;
                state.speed = spec.Speed;
                state.writeDefaultValues = true;

                // Ensure AnyState -> Shift state transition
                AnimatorStateTransition anyTr = FindAnyStateTransitionTo(baseSm, state);
                if (anyTr == null)
                {
                    anyTr = baseSm.AddAnyStateTransition(state);
                }
                anyTr.hasExitTime = false;
                anyTr.duration = 0.08f;
                anyTr.canTransitionToSelf = false;
                anyTr.conditions = new[]
                {
                    new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsShiftActive", threshold = 0f },
                    new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "ShiftType", threshold = spec.ShiftTypeInt },
                    new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsDashing", threshold = 0f },
                };

                // Ensure Exit transition back to Idle when IsShiftActive becomes false
                if (idleState != null)
                {
                    AnimatorStateTransition exitTr = FindTransitionTo(state, idleState);
                    if (exitTr == null)
                    {
                        exitTr = state.AddTransition(idleState);
                    }
                    exitTr.hasExitTime = false;
                    exitTr.duration = 0.10f;
                    exitTr.conditions = new[]
                    {
                        new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsShiftActive", threshold = 0f }
                    };
                }

                wiredCount++;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#50FA7B>[ShiftAnimationSetup]</color> Installed & wired {wiredCount}/7 unique character Shift animations into Chibi_Player_Controller!");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Shift Animations Installed",
                    $"ติดตั้ง Animation ท่า Shift ประจำตัวครบทั้ง {wiredCount} ตัวละครลงใน Chibi_Player_Controller เรียบร้อยแล้ว!",
                    "OK"
                );
            }
        }

        private static AnimationClip FindClipByActionKey(UnityEngine.Object[] assets, string actionKey)
        {
            foreach (UnityEngine.Object obj in assets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    if (clip.name.IndexOf(actionKey, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return clip;
                    }
                }
            }
            return null;
        }

        private static void ApplyLoopFlag(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static bool HasParam(AnimatorController ctrl, string name)
        {
            foreach (var p in ctrl.parameters)
                if (p.name == name) return true;
            return false;
        }

        private static void EnsureParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            if (!HasParam(ctrl, name))
                ctrl.AddParameter(name, type);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var child in sm.states)
                if (child.state != null && child.state.name == name)
                    return child.state;
            return null;
        }

        private static AnimatorStateTransition FindAnyStateTransitionTo(AnimatorStateMachine sm, AnimatorState dst)
        {
            foreach (var tr in sm.anyStateTransitions)
                if (tr != null && tr.destinationState == dst)
                    return tr;
            return null;
        }

        private static AnimatorStateTransition FindTransitionTo(AnimatorState src, AnimatorState dst)
        {
            foreach (var tr in src.transitions)
                if (tr != null && tr.destinationState == dst)
                    return tr;
            return null;
        }
    }
}
#endif
