#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically imports Assets/GeneratedModels/Chibi_Vault_Animations.fbx as Generic Rig,
    /// extracts Vault_MotoJump (motorcycle hurdle jump) and Vault_CarSlide (car roof/hood slide),
    /// and wires both states cleanly into Chibi_Player_Controller.controller.
    /// </summary>
    public class VehicleVaultAnimationFbxPostprocessor : AssetPostprocessor
    {
        private const string VaultFbxPath = "Assets/GeneratedModels/Chibi_Vault_Animations.fbx";

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Equals(VaultFbxPath, StringComparison.OrdinalIgnoreCase)) return;
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
                if (path.Replace('\\', '/').Equals(VaultFbxPath, StringComparison.OrdinalIgnoreCase))
                {
                    _queued = true;
                    EditorApplication.delayCall += () =>
                    {
                        _queued = false;
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        VehicleVaultAnimationSetup.InstallVaultAnimations(forceReinstall: false, showDialog: false);
                    };
                    break;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class VehicleVaultAnimationSetup
    {
        private const string VaultFbxPath = "Assets/GeneratedModels/Chibi_Vault_Animations.fbx";
        private const string AnimOutDir = "Assets/Resources_Addressable/Image/Animation";
        private const string ControllerPath = "Assets/Resources_Addressable/Image/Animation/Chibi_Player_Controller.controller";

        private static readonly (string stateName, float speed)[] Specs =
        {
            ("Vault_MotoJump", 1.75f), // Crisp ~0.46s motorcycle hurdle jump
            ("Vault_CarSlide", 1.75f), // Sleek ~0.55s car roof/hood slide
        };

        static VehicleVaultAnimationSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                InstallVaultAnimations(forceReinstall: false, showDialog: false);
            };
        }

        [MenuItem("Tools/The Last Empire/Install Vehicle Vault & Roof Slide Animations")]
        public static void InstallFromMenu()
        {
            InstallVaultAnimations(forceReinstall: true, showDialog: true);
        }

        public static void InstallVaultAnimations(bool forceReinstall, bool showDialog)
        {
            if (!File.Exists(VaultFbxPath) || !File.Exists(ControllerPath)) return;

            ModelImporter importer = AssetImporter.GetAtPath(VaultFbxPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(VaultFbxPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(VaultFbxPath) as ModelImporter;
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
            bool alreadyInstalled = FindState(baseSm, "Vault_MotoJump") != null && FindState(baseSm, "Vault_CarSlide") != null;
            if (alreadyInstalled && !forceReinstall) return;

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(VaultFbxPath);
            if (subAssets == null || subAssets.Length == 0) return;

            if (!Directory.Exists(AnimOutDir)) Directory.CreateDirectory(AnimOutDir);

            AnimatorState idleState = FindState(baseSm, "Idle") ?? baseSm.defaultState;
            AnimatorState walkState = FindState(baseSm, "Walk");
            AnimatorState runState = FindState(baseSm, "Run");

            int wiredCount = 0;
            for (int i = 0; i < Specs.Length; i++)
            {
                var (stateName, speed) = Specs[i];
                AnimationClip srcClip = FindClipByKey(subAssets, stateName);
                if (srcClip == null) continue;

                string animPath = $"{AnimOutDir}/Chibi_Base_Rig_{stateName}.anim";
                AnimationClip extractedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                if (extractedClip == null)
                {
                    extractedClip = new AnimationClip();
                    EditorUtility.CopySerialized(srcClip, extractedClip);
                    extractedClip.name = $"Chibi_Base_Rig_{stateName}";
                    ApplyNoLoop(extractedClip);
                    AssetDatabase.CreateAsset(extractedClip, animPath);
                }
                else if (forceReinstall)
                {
                    EditorUtility.CopySerialized(srcClip, extractedClip);
                    extractedClip.name = $"Chibi_Base_Rig_{stateName}";
                    ApplyNoLoop(extractedClip);
                    EditorUtility.SetDirty(extractedClip);
                }

                AnimatorState state = FindState(baseSm, stateName);
                if (state == null)
                {
                    state = baseSm.AddState(stateName, new Vector3(820f, 360f + i * 68f, 0f));
                }
                state.motion = extractedClip;
                state.speed = speed;
                state.writeDefaultValues = true;

                if (runState != null)
                {
                    AnimatorStateTransition tRun = FindTransitionTo(state, runState) ?? state.AddTransition(runState);
                    tRun.hasExitTime = true;
                    tRun.exitTime = 0.88f;
                    tRun.duration = 0.10f;
                    tRun.conditions = new[]
                    {
                        new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f },
                        new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsRunning", threshold = 0f },
                    };
                }
                if (walkState != null)
                {
                    AnimatorStateTransition tWalk = FindTransitionTo(state, walkState) ?? state.AddTransition(walkState);
                    tWalk.hasExitTime = true;
                    tWalk.exitTime = 0.88f;
                    tWalk.duration = 0.10f;
                    tWalk.conditions = new[]
                    {
                        new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f },
                        new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsRunning", threshold = 0f },
                    };
                }
                if (idleState != null)
                {
                    AnimatorStateTransition tIdle = FindTransitionTo(state, idleState) ?? state.AddTransition(idleState);
                    tIdle.hasExitTime = true;
                    tIdle.exitTime = 0.90f;
                    tIdle.duration = 0.10f;
                    tIdle.conditions = Array.Empty<AnimatorCondition>();
                }

                wiredCount++;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#50FA7B>[VehicleVaultAnimationSetup]</color> Installed & wired {wiredCount}/2 vehicle vault animations (Vault_MotoJump, Vault_CarSlide)!");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Vehicle Vault Animations Installed",
                    $"ติดตั้ง Animation กระโดดข้ามมอเตอร์ไซค์ (Vault_MotoJump) และ สไลด์หลังคารถยนต์ (Vault_CarSlide) เรียบร้อยแล้ว!",
                    "OK"
                );
            }
        }

        private static AnimationClip FindClipByKey(UnityEngine.Object[] assets, string key)
        {
            foreach (UnityEngine.Object obj in assets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    if (clip.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return clip;
                }
            }
            return null;
        }

        private static void ApplyNoLoop(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var child in sm.states)
                if (child.state != null && child.state.name == name)
                    return child.state;
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
