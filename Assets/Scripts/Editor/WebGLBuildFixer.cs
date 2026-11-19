#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Ensures WebGL builds always include all custom & runtime-queried URP shaders
    /// (preventing bright magenta ground/mist on WebGL) and locks default resolution to 16:9 (1920x1080).
    /// </summary>
    [InitializeOnLoad]
    public static class WebGLBuildFixer
    {
        private static readonly string[] RequiredShaderPaths = new[]
        {
            "Assets/Shaders/WorldSpaceGroundShader.shader",
            "Assets/Shaders/ProceduralGroundMist.shader",
            "Assets/Shaders/AtmosphericDustMote.shader",
            "Assets/Shaders/NightDarknessOverlay.shader",
            "Assets/Shaders/DayNightObjectShader.shader",
            "Assets/Shaders/DayNightVignette.shader",
            "Assets/Shaders/VolumetricGodRayBeam.shader",
        };

        private static readonly string[] RequiredShaderNames = new[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "Sprites/Default",
        };

        static WebGLBuildFixer()
        {
            EditorApplication.delayCall += ApplyAllAuto;
        }

        private static void ApplyAllAuto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ApplyAll(false);
        }

        [MenuItem("Tools/The Last Empire/Fix WebGL Shaders & 16:9 Resolution")]
        public static void ApplyFromMenu()
        {
            ApplyAll(true);
        }

        public static void ApplyAll(bool showDialog)
        {
            bool changed = false;

            // 1. Lock default Desktop & WebGL resolution to 16:9 Full HD (1920x1080)
            if (PlayerSettings.defaultWebScreenWidth != 1920 || PlayerSettings.defaultWebScreenHeight != 1080)
            {
                PlayerSettings.defaultWebScreenWidth = 1920;
                PlayerSettings.defaultWebScreenHeight = 1080;
                changed = true;
            }
            if (PlayerSettings.defaultScreenWidth != 1920 || PlayerSettings.defaultScreenHeight != 1080)
            {
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                changed = true;
            }
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
                changed = true;
            }

            // 2. Ensure all custom & runtime-queried shaders are in Always Included Shaders
            GraphicsSettings graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings != null)
            {
                SerializedObject so = new SerializedObject(graphicsSettings);
                SerializedProperty prop = so.FindProperty("m_AlwaysIncludedShaders");
                if (prop != null && prop.isArray)
                {
                    HashSet<Shader> existing = new HashSet<Shader>();
                    for (int i = 0; i < prop.arraySize; i++)
                    {
                        Shader s = prop.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                        if (s != null) existing.Add(s);
                    }

                    List<Shader> toAdd = new List<Shader>();
                    foreach (string path in RequiredShaderPaths)
                    {
                        Shader s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                        if (s != null && !existing.Contains(s))
                        {
                            existing.Add(s);
                            toAdd.Add(s);
                        }
                    }
                    foreach (string sName in RequiredShaderNames)
                    {
                        Shader s = Shader.Find(sName);
                        if (s != null && !existing.Contains(s))
                        {
                            existing.Add(s);
                            toAdd.Add(s);
                        }
                    }

                    if (toAdd.Count > 0)
                    {
                        foreach (Shader s in toAdd)
                        {
                            int idx = prop.arraySize;
                            prop.InsertArrayElementAtIndex(idx);
                            prop.GetArrayElementAtIndex(idx).objectReferenceValue = s;
                        }
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(graphicsSettings);
                        changed = true;
                        Debug.Log($"<color=#50FA7B>[WebGLBuildFixer]</color> Added {toAdd.Count} shaders to Always Included Shaders & locked 16:9 (1920x1080)!");
                    }
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "WebGL Shaders & 16:9 Fixed",
                    "Locked WebGL/Desktop default resolution to 1920x1080 (16:9) and added all custom/URP shaders to Always Included Shaders!",
                    "OK");
            }
        }
    }
}
#endif
