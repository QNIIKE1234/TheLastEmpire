#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically imports Assets/GeneratedModels/Chibi_Melee_Animations.fbx as Generic Rig,
    /// extracts all 9 weapon-specific melee attack AnimationClips into Assets/Resources_Addressable/Image/Animation/,
    /// and wires all 9 attack states cleanly into Chibi_Player_Controller.controller.
    /// </summary>
    public class MeleeWeaponAnimationFbxPostprocessor : AssetPostprocessor
    {
        private const string MeleeFbxPath = "Assets/GeneratedModels/Chibi_Melee_Animations.fbx";

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Equals(MeleeFbxPath, StringComparison.OrdinalIgnoreCase)) return;
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
                if (path.Replace('\\', '/').Equals(MeleeFbxPath, StringComparison.OrdinalIgnoreCase))
                {
                    _queued = true;
                    EditorApplication.delayCall += () =>
                    {
                        _queued = false;
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        MeleeWeaponAnimationSetup.InstallMeleeAnimations(forceReinstall: true, showDialog: false);
                    };
                    break;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class MeleeWeaponAnimationSetup
    {
        private const string MeleeFbxPath = "Assets/GeneratedModels/Chibi_Melee_Animations.fbx";
        private const string AnimOutDir = "Assets/Resources_Addressable/Image/Animation";
        private const string ControllerPath = "Assets/Resources_Addressable/Image/Animation/Chibi_Player_Controller.controller";

        private readonly struct MeleeAnimSpec
        {
            public readonly string StateName;
            public readonly float Speed;

            public MeleeAnimSpec(string stateName, float speed)
            {
                StateName = stateName;
                Speed = speed;
            }
        }

        private static readonly MeleeAnimSpec[] Specs = new[]
        {
            new MeleeAnimSpec("Melee_Knife",         1.65f),
            new MeleeAnimSpec("Melee_BaseballBat",   1.35f),
            new MeleeAnimSpec("Melee_Machete",       1.30f),
            new MeleeAnimSpec("Melee_FireAxe",       1.30f),
            new MeleeAnimSpec("Melee_Katana",        1.55f),
            new MeleeAnimSpec("Melee_Sledgehammer",  1.15f),
            new MeleeAnimSpec("Melee_SurvivalSpear", 1.45f),
            new MeleeAnimSpec("Melee_StunBaton",     1.45f),
            new MeleeAnimSpec("Melee_Chainsaw",      1.60f),
        };

        static MeleeWeaponAnimationSetup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) return;
                InstallMeleeAnimations(forceReinstall: false, showDialog: false);
            };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
            {
                if (IsFbxNewerThanExtractedAnims())
                {
                    InstallMeleeAnimations(forceReinstall: true, showDialog: false);
                }
            }
        }

        [MenuItem("Tools/The Last Empire/Install 9 Melee Weapon Attack Animations")]
        public static void InstallFromMenu()
        {
            InstallMeleeAnimations(forceReinstall: true, showDialog: true);
        }

        public static void InstallMeleeAnimations(bool forceReinstall, bool showDialog)
        {
            if (!File.Exists(MeleeFbxPath) || !File.Exists(ControllerPath)) return;

            ModelImporter importer = AssetImporter.GetAtPath(MeleeFbxPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(MeleeFbxPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(MeleeFbxPath) as ModelImporter;
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
            bool alreadyInstalled = FindState(baseSm, "Melee_Chainsaw") != null && FindState(baseSm, "Melee_Knife") != null;
            bool fbxIsNewer = IsFbxNewerThanExtractedAnims();
            if (alreadyInstalled && !forceReinstall && !fbxIsNewer) return;
            bool shouldOverwrite = forceReinstall || fbxIsNewer;

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(MeleeFbxPath);
            if (subAssets == null || subAssets.Length == 0) return;

            if (!Directory.Exists(AnimOutDir)) Directory.CreateDirectory(AnimOutDir);

            AnimatorState idleState = FindState(baseSm, "Idle") ?? baseSm.defaultState;
            AnimatorState walkState = FindState(baseSm, "Walk");
            AnimatorState runState = FindState(baseSm, "Run");

            int wiredCount = 0;
            var updatedAnimPaths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Specs.Length; i++)
            {
                MeleeAnimSpec spec = Specs[i];
                AnimationClip srcClip = FindClipByKey(subAssets, spec.StateName);
                if (srcClip == null)
                {
                    Debug.LogWarning($"[MeleeWeaponAnimationSetup] Missing clip '{spec.StateName}' in {MeleeFbxPath}");
                    continue;
                }

                string animPath = $"{AnimOutDir}/Chibi_Base_Rig_{spec.StateName}.anim";
                AnimationClip extractedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                if (extractedClip == null)
                {
                    extractedClip = new AnimationClip();
                    EditorUtility.CopySerialized(srcClip, extractedClip);
                    extractedClip.name = $"Chibi_Base_Rig_{spec.StateName}";
                    ApplyNoLoop(extractedClip);
                    AssetDatabase.CreateAsset(extractedClip, animPath);
                }
                else if (shouldOverwrite)
                {
                    EditorUtility.CopySerialized(srcClip, extractedClip);
                    extractedClip.name = $"Chibi_Base_Rig_{spec.StateName}";
                    ApplyNoLoop(extractedClip);
                    EditorUtility.SetDirty(extractedClip);
                    updatedAnimPaths.Add(animPath);
                }

                AnimatorState state = FindState(baseSm, spec.StateName);
                if (state == null)
                {
                    state = baseSm.AddState(spec.StateName, new Vector3(1120f, -200f + i * 62f, 0f));
                }
                state.motion = extractedClip;
                state.speed = spec.Speed;
                state.writeDefaultValues = true;

                // Clean exit transitions matching Slash1/2/3 (blend smoothly to Run / Walk / Idle at end of swing)
                if (runState != null)
                {
                    AnimatorStateTransition tRun = FindTransitionTo(state, runState) ?? state.AddTransition(runState);
                    tRun.hasExitTime = true;
                    tRun.exitTime = 0.86f;
                    tRun.duration = 0.09f;
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
                    tWalk.exitTime = 0.86f;
                    tWalk.duration = 0.09f;
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
                    tIdle.exitTime = 0.88f;
                    tIdle.duration = 0.10f;
                    tIdle.conditions = Array.Empty<AnimatorCondition>();
                }

                wiredCount++;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            if (updatedAnimPaths.Count > 0)
            {
                AssetDatabase.ForceReserializeAssets(updatedAnimPaths);
            }
            Debug.Log($"<color=#50FA7B>[MeleeWeaponAnimationSetup]</color> Installed & wired {wiredCount}/9 unique melee weapon attack animations (updated {updatedAnimPaths.Count} clips) into Chibi_Player_Controller!");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Melee Weapon Animations Installed",
                    $"ติดตั้ง Animation ท่าตีใกล้ครบทั้ง {wiredCount} อาวุธลงใน Chibi_Player_Controller เรียบร้อยแล้ว!",
                    "OK"
                );
            }
        }

        private static bool IsFbxNewerThanExtractedAnims()
        {
            if (!File.Exists(MeleeFbxPath)) return false;
            DateTime fbxUtc = File.GetLastWriteTimeUtc(MeleeFbxPath);
            for (int i = 0; i < Specs.Length; i++)
            {
                string animPath = $"{AnimOutDir}/Chibi_Base_Rig_{Specs[i].StateName}.anim";
                if (!File.Exists(animPath)) return true;
                if (fbxUtc > File.GetLastWriteTimeUtc(animPath).AddSeconds(1))
                    return true;
            }
            return false;
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
