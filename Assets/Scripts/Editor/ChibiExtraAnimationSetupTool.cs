#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    /// <summary>
    /// Extracts the 12 new Chibi_Base_Rig animations (Sleep, Talk, and 10 Character Cool Poses)
    /// from Assets/GeneratedModels/Chibi_Sleep_Talk_Poses.fbx into standalone .anim assets
    /// under Assets/Resources_Addressable/Image/Animation/ and registers states in Chibi_Player_Controller.controller.
    /// </summary>
    [InitializeOnLoad]
    public static class ChibiExtraAnimationSetupTool
    {
        private const string FbxPath        = "Assets/GeneratedModels/Chibi_Sleep_Talk_Poses.fbx";
        private const string AnimOutFolder  = "Assets/Resources_Addressable/Image/Animation";
        private const string ControllerPath = "Assets/Resources_Addressable/Image/Animation/Chibi_Player_Controller.controller";
        private const string AutoKey        = "TLE_ChibiExtraAnims_Extracted_v1";

        static ChibiExtraAnimationSetupTool()
        {
            EditorApplication.delayCall += TryAutoExtract;
        }

        private static void TryAutoExtract()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(AutoKey, false)) return;
            if (!File.Exists(FbxPath)) return;

            bool sleepMissing = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimOutFolder}/Chibi_Base_Rig_Sleep.anim") == null;
            bool pose10Missing = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimOutFolder}/Chibi_Base_Rig_Pose_10_BlueOperative.anim") == null;
            if (sleepMissing || pose10Missing)
            {
                SessionState.SetBool(AutoKey, true);
                ExtractAndWireAnimations();
            }
            else
            {
                SessionState.SetBool(AutoKey, true);
            }
        }

        [MenuItem("Tools/TheLastEmpire/Extract & Wire Chibi Sleep, Talk & 10 Cool Poses")]
        public static void ExtractAndWireAnimations()
        {
            AssetDatabase.Refresh();
            if (!File.Exists(FbxPath))
            {
                Debug.LogError($"[ChibiExtraAnim] Missing FBX: {FbxPath}");
                return;
            }

            // Ensure all clips in the FBX are set to Loop Time cleanly
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine sm = controller != null && controller.layers.Length > 0
                ? controller.layers[0].stateMachine
                : null;

            int extracted = 0;
            int addedStates = 0;

            for (int i = 0; i < subAssets.Length; i++)
            {
                AnimationClip srcClip = subAssets[i] as AnimationClip;
                if (srcClip == null || srcClip.name.StartsWith("__preview__")) continue;

                // Normalize name: "Chibi_Base_Rig|Sleep" -> "Sleep" & file "Chibi_Base_Rig_Sleep.anim"
                string cleanStateName = srcClip.name;
                int pipeIdx = cleanStateName.IndexOf('|');
                if (pipeIdx >= 0 && pipeIdx + 1 < cleanStateName.Length)
                {
                    cleanStateName = cleanStateName.Substring(pipeIdx + 1);
                }
                string fileName = $"Chibi_Base_Rig_{cleanStateName}.anim";
                string outPath = $"{AnimOutFolder}/{fileName}";

                AnimationClip copy = Object.Instantiate(srcClip);
                copy.name = $"Chibi_Base_Rig_{cleanStateName}";
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(copy, settings);

                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(copy, existing);
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(copy);
                    copy = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(copy, outPath);
                }
                extracted++;

                // Add/update state in Chibi_Player_Controller so Play/CrossFade works by state name!
                if (sm != null)
                {
                    AnimatorState found = null;
                    for (int s = 0; s < sm.states.Length; s++)
                    {
                        if (sm.states[s].state != null && sm.states[s].state.name == cleanStateName)
                        {
                            found = sm.states[s].state;
                            break;
                        }
                    }

                    if (found == null)
                    {
                        Vector3 pos = new Vector3(560f + (addedStates % 3) * 240f, -120f + (addedStates / 3) * 70f, 0f);
                        found = sm.AddState(cleanStateName, pos);
                        addedStates++;
                    }
                    found.motion = copy;
                }
            }

            if (controller != null) EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChibiExtraAnim] Extracted {extracted} .anim clips to {AnimOutFolder} and wired {addedStates} new states into Chibi_Player_Controller!");
        }
    }
}
#endif
