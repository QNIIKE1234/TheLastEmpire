using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Editor Window for testing, previewing, and managing the dynamic Day/Dusk/Night cycle,
    /// screen darkness distance falloff, and player personal light perimeter.
    /// </summary>
    public class DayNightLightingEditorWindow : EditorWindow
    {
        [MenuItem("Tools/The Last Empire/Day-Night & Lighting Controller", false, 25)]
        [MenuItem("The Last Empire/Day-Night & Lighting Controller", false, 25)]
        public static void ShowWindow()
        {
            DayNightLightingEditorWindow window = GetWindow<DayNightLightingEditorWindow>("Day/Night & Lighting");
            window.minSize = new Vector2(390, 520);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Header
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("DAY / NIGHT & DARKNESS CONTROLLER", headerStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Atmospheric Survival Cycle:\n" +
                "1. DAY: Normal clear daylight.\n" +
                "2. DUSK: Screen warms into amber sunset fade.\n" +
                "3. NIGHT: Screen fades smoothly over distance from bright center to dim dark environment.\n" +
                "- Environment remains visible in the dark (no sharp pitch-black cutout).\n" +
                "- Press 'F' in-game to toggle player personal light.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            DayNightManager dayNightMgr = DayNightManager.Instance;
            NightDarknessController darknessCtrl = NightDarknessController.Instance;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("CURRENT CYCLE STATUS", EditorStyles.boldLabel);

            if (dayNightMgr != null)
            {
                EditorGUILayout.LabelField("Day Count:", $"Day {dayNightMgr.DayCount}");
                EditorGUILayout.LabelField("Current Phase:", $"{dayNightMgr.CurrentPhase.ToString().ToUpper()}");
                EditorGUILayout.LabelField("Phase Progress:", $"{Mathf.RoundToInt(dayNightMgr.PhaseProgress * 100f)}%");
            }
            else
            {
                EditorGUILayout.LabelField("DayNightManager:", "Not active in scene (Start Play Mode to test)");
            }

            if (darknessCtrl != null)
            {
                EditorGUILayout.LabelField("Player Personal Light:", darknessCtrl.HasTorchLight ? "ON (Active - Press F)" : "OFF (Dim Night)");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Realtime Lighting Falloff Controls
            if (darknessCtrl != null)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("NIGHT DISTANCE FALLOFF & VISIBILITY", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                darknessCtrl.innerRadius = EditorGUILayout.Slider("Inner Bright Radius:", darknessCtrl.innerRadius, 0.05f, 0.40f);
                darknessCtrl.outerRadius = EditorGUILayout.Slider("Outer Fade Radius:", darknessCtrl.outerRadius, 0.20f, 1.20f);
                darknessCtrl.maxDarkness = EditorGUILayout.Slider("Max Darkness (Env Visibility):", darknessCtrl.maxDarkness, 0.40f, 0.95f);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(darknessCtrl);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // 1-Click Phase Jump Buttons
            EditorGUILayout.LabelField("TEST PHASES (1-CLICK JUMP)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(1.0f, 0.95f, 0.7f);
            if (GUILayout.Button("DAY\n(Clear Sun)", GUILayout.Height(40)))
            {
                if (dayNightMgr != null) dayNightMgr.SetPhase(DayNightManager.TimePhase.Day);
                else SetManualPreview(0f, 0f, Color.white);
            }

            GUI.backgroundColor = new Color(1.0f, 0.65f, 0.35f);
            if (GUILayout.Button("DUSK\n(Sunset Fade)", GUILayout.Height(40)))
            {
                if (dayNightMgr != null) dayNightMgr.SetPhase(DayNightManager.TimePhase.Dusk);
                else SetManualPreview(0f, 1f, new Color(0.95f, 0.70f, 0.45f, 1f));
            }

            GUI.backgroundColor = new Color(0.2f, 0.35f, 0.7f);
            if (GUILayout.Button("NIGHT\n(Distance Fade)", GUILayout.Height(40)))
            {
                if (dayNightMgr != null) dayNightMgr.SetPhase(DayNightManager.TimePhase.Night);
                else SetManualPreview(1f, 0f, new Color(0.15f, 0.18f, 0.25f, 1f));
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Toggle Light Button
            GUI.backgroundColor = new Color(0.95f, 0.85f, 0.4f);
            if (GUILayout.Button("TOGGLE PLAYER LIGHT (Press F)", GUILayout.Height(36)))
            {
                if (darknessCtrl != null)
                {
                    darknessCtrl.ToggleTorchLight();
                }
                else
                {
                    float current = Shader.GetGlobalFloat("_GlobalHasTorchLight");
                    Shader.SetGlobalFloat("_GlobalHasTorchLight", current > 0.5f ? 0f : 1f);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12);

            // Setup Ground Materials
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.5f);
            if (GUILayout.Button("Re-verify Ground Materials (Assign Lightweight Shader)", GUILayout.Height(32)))
            {
                AssignGroundShader.SetupMaterials();
                ShowNotification(new GUIContent("Assigned Lightweight Ground Shader!"));
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Close Window"))
            {
                Close();
            }
        }

        private void SetManualPreview(float darkAlpha, float duskAlpha, Color ambient)
        {
            Shader.SetGlobalFloat("_GlobalDarknessAlpha", darkAlpha);
            Shader.SetGlobalFloat("_GlobalDuskAlpha", duskAlpha);
            Shader.SetGlobalColor("_GlobalAmbientTint", ambient);
            Shader.SetGlobalFloat("_GlobalHasTorchLight", 1f);
            SceneView.RepaintAll();
        }
    }
}
