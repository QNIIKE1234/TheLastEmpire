using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastEmpire
{
    /// <summary>
    /// Unity Editor Window for fine-tuning Golden Hour sun angles,
    /// Volumetric God Ray Beams (Sun Shafts), and Atmospheric Fog in URP.
    /// </summary>
    public class AtmosphericLightingEditorWindow : EditorWindow
    {
        [MenuItem("Tools/The Last Empire/Atmospheric Lighting & God Rays", false, 26)]
        [MenuItem("The Last Empire/Atmospheric Lighting & God Rays", false, 26)]
        public static void ShowWindow()
        {
            var window = GetWindow<AtmosphericLightingEditorWindow>("Atmosphere & God Rays");
            window.minSize = new Vector2(400, 560);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("CINEMATIC ATMOSPHERE & GOD RAYS", titleStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Controls for URP Cinematic Golden Hour Lighting:\n" +
                "- Directional Light: Low pitch (20-30 deg) for diagonal sunbeams\n" +
                "- Volumetric God Rays: Animated dust motes & soft depth intersection\n" +
                "- ExponentialSquared Fog: Warm golden atmospheric mist\n" +
                "- Post-Processing: ACES Tonemapping, Warm Bloom, and SSAO",
                MessageType.Info);

            EditorGUILayout.Space(10);

            AtmosphericLightingController controller = AtmosphericLightingController.Instance;
            if (controller == null)
            {
                controller = Object.FindFirstObjectByType<AtmosphericLightingController>();
            }

            if (controller == null)
            {
                EditorGUILayout.HelpBox("No AtmosphericLightingController found in the open scene.", MessageType.Warning);
                if (GUILayout.Button("Create Atmospheric Lighting Controller in Scene", GUILayout.Height(36)))
                {
                    GameObject go = new GameObject("AtmosphericLightingManager");
                    controller = go.AddComponent<AtmosphericLightingController>();
                    Undo.RegisterCreatedObjectUndo(go, "Create Atmospheric Lighting Manager");
                    EditorUtility.SetDirty(go);
                }
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            so.Update();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("GOLDEN HOUR SUN (DIRECTIONAL LIGHT)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("sunLight"));
            EditorGUILayout.PropertyField(so.FindProperty("sunPitchAngle"));
            EditorGUILayout.PropertyField(so.FindProperty("sunYawAngle"));
            EditorGUILayout.PropertyField(so.FindProperty("goldenSunColor"));
            EditorGUILayout.PropertyField(so.FindProperty("sunIntensity"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("ATMOSPHERIC MIST & HEIGHT FOG", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("enableFog"));
            EditorGUILayout.PropertyField(so.FindProperty("goldenFogColor"));
            EditorGUILayout.PropertyField(so.FindProperty("duskFogColor"));
            EditorGUILayout.PropertyField(so.FindProperty("nightFogColor"));
            EditorGUILayout.PropertyField(so.FindProperty("fogDensity"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("VOLUMETRIC GOD RAY BEAMS (SUN SHAFTS)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("enableGodRays"));
            EditorGUILayout.PropertyField(so.FindProperty("godRayMaterial"));
            EditorGUILayout.PropertyField(so.FindProperty("beamCount"));
            EditorGUILayout.PropertyField(so.FindProperty("beamLength"));
            EditorGUILayout.PropertyField(so.FindProperty("beamWidth"));
            EditorGUILayout.PropertyField(so.FindProperty("godRayIntensity"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            so.ApplyModifiedProperties();

            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.4f);
            if (GUILayout.Button("Apply Atmosphere & Regenerate Beams", GUILayout.Height(36)))
            {
                controller.ApplyAtmosphere();
                EditorUtility.SetDirty(controller);
            }
        }
    }
}
