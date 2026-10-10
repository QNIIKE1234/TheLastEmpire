using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    [InitializeOnLoad]
    public static class ChibiArmPoseFixer
    {
        private const string RunPath = "Assets/GeneratedModels/Chibi_Base_Rig_Run.anim";
        private const string WalkPath = "Assets/GeneratedModels/Chibi_Base_Rig_Walk.anim";
        private const string IdlePath = "Assets/GeneratedModels/Chibi_Base_Rig_Idle.anim";

        static ChibiArmPoseFixer()
        {
            EditorApplication.delayCall += AutoFixOnce;
        }

        private static void AutoFixOnce()
        {
            // Run automatically on compile/load
            FixAllAnimations();
        }

        [MenuItem("TheLastEmpire/Fix All Chibi Arm Poses (Idle, Walk, Run)")]
        public static void FixAllAnimations()
        {
            FixClip(IdlePath, AnimType.Idle);
            FixClip(WalkPath, AnimType.Walk);
            FixClip(RunPath, AnimType.Run);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[ChibiArmPoseFixer]</color> Successfully removed all Hand IK curves and fixed natural Arm Poses for Idle, Walk, and Run!");
        }

        private enum AnimType { Idle, Walk, Run }

        private static void FixClip(string path, AnimType type)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[ChibiArmPoseFixer] Clip not found at: {path}");
                return;
            }

            Undo.RecordObject(clip, "Fix Chibi Arm Pose");

            // 1. Remove all Hand IK Translation and Rotation curves (LeftHandT, LeftHandQ, RightHandT, RightHandQ)
            // Hand IK forces Two-Bone IK to collapse wide chibi arms inwards, resulting in awkward 'chicken wings' / crab arms.
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var b in bindings)
            {
                if (b.propertyName.StartsWith("LeftHandT") || b.propertyName.StartsWith("LeftHandQ") ||
                    b.propertyName.StartsWith("RightHandT") || b.propertyName.StartsWith("RightHandQ"))
                {
                    AnimationUtility.SetEditorCurve(clip, b, null);
                }
            }

            // 2. Reset Shoulder curves to 0 to prevent hunching / pinching
            SetConstantCurve(clip, "Left Shoulder Down-Up", 0f);
            SetConstantCurve(clip, "Right Shoulder Down-Up", 0f);
            SetConstantCurve(clip, "Left Shoulder Front-Back", 0f);
            SetConstantCurve(clip, "Right Shoulder Front-Back", 0f);

            // 3. Reset Twist curves to 0 (eliminates axial bone rotation)
            SetConstantCurve(clip, "Left Arm Twist In-Out", 0f);
            SetConstantCurve(clip, "Right Arm Twist In-Out", 0f);
            SetConstantCurve(clip, "Left Forearm Twist In-Out", 0f);
            SetConstantCurve(clip, "Right Forearm Twist In-Out", 0f);

            float length = clip.length > 0.001f ? clip.length : 1.0f;

            // 4. Configure clean FK muscle curves based on movement type
            if (type == AnimType.Idle)
            {
                // Idle: Arms hanging down relaxed beside hips
                SetConstantCurve(clip, "Left Arm Down-Up", -0.65f);
                SetConstantCurve(clip, "Right Arm Down-Up", -0.65f);
                SetConstantCurve(clip, "Left Arm Front-Back", 0.0f);
                SetConstantCurve(clip, "Right Arm Front-Back", 0.0f);
                SetConstantCurve(clip, "Left Forearm Stretch", 0.12f);
                SetConstantCurve(clip, "Right Forearm Stretch", 0.12f);
            }
            else if (type == AnimType.Walk)
            {
                // Walk: Arms down, natural alternating swing matching legs
                SetConstantCurve(clip, "Left Arm Down-Up", -0.60f);
                SetConstantCurve(clip, "Right Arm Down-Up", -0.60f);
                SetConstantCurve(clip, "Left Forearm Stretch", 0.18f);
                SetConstantCurve(clip, "Right Forearm Stretch", 0.18f);

                AnimationCurve leftFrontBack = new AnimationCurve();
                leftFrontBack.AddKey(new Keyframe(0f, -0.22f));
                leftFrontBack.AddKey(new Keyframe(length * 0.5f, 0.22f));
                leftFrontBack.AddKey(new Keyframe(length, -0.22f));
                SmoothCurve(leftFrontBack);
                SetCurve(clip, "Left Arm Front-Back", leftFrontBack);

                AnimationCurve rightFrontBack = new AnimationCurve();
                rightFrontBack.AddKey(new Keyframe(0f, 0.22f));
                rightFrontBack.AddKey(new Keyframe(length * 0.5f, -0.22f));
                rightFrontBack.AddKey(new Keyframe(length, 0.22f));
                SmoothCurve(rightFrontBack);
                SetCurve(clip, "Right Arm Front-Back", rightFrontBack);
            }
            else if (type == AnimType.Run)
            {
                // Run: Arms bent ~80-90 degrees, dynamic alternating swing
                SetConstantCurve(clip, "Left Arm Down-Up", -0.55f);
                SetConstantCurve(clip, "Right Arm Down-Up", -0.55f);
                SetConstantCurve(clip, "Left Forearm Stretch", 0.35f);
                SetConstantCurve(clip, "Right Forearm Stretch", 0.35f);

                // Alternating swing (Left vs Right in opposition)
                AnimationCurve leftFrontBack = new AnimationCurve();
                leftFrontBack.AddKey(new Keyframe(0f, -0.40f));
                leftFrontBack.AddKey(new Keyframe(length * 0.5f, 0.45f));
                leftFrontBack.AddKey(new Keyframe(length, -0.40f));
                SmoothCurve(leftFrontBack);
                SetCurve(clip, "Left Arm Front-Back", leftFrontBack);

                AnimationCurve rightFrontBack = new AnimationCurve();
                rightFrontBack.AddKey(new Keyframe(0f, 0.45f));
                rightFrontBack.AddKey(new Keyframe(length * 0.5f, -0.40f));
                rightFrontBack.AddKey(new Keyframe(length, 0.45f));
                SmoothCurve(rightFrontBack);
                SetCurve(clip, "Right Arm Front-Back", rightFrontBack);
            }

            EditorUtility.SetDirty(clip);
        }

        private static void SetConstantCurve(AnimationClip clip, string propName, float value)
        {
            float len = clip.length > 0.001f ? clip.length : 1f;
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, value),
                new Keyframe(len, value)
            );
            SetCurve(clip, propName, curve);
        }

        private static void SetCurve(AnimationClip clip, string propName, AnimationCurve curve)
        {
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(Animator),
                propertyName = propName
            };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static void SmoothCurve(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
        }
    }
}
