using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public static class AssignGroundShader
    {
        [MenuItem("Tools/The Last Empire/Setup Ground Materials Shader (Day & Night)", false, 20)]
        [MenuItem("The Last Empire/Setup Ground Materials Shader")]
        public static void SetupMaterials()
        {
            Shader customShader = Shader.Find("Custom/WorldSpaceGroundShader");
            if (customShader == null)
            {
                Debug.LogError("AssignGroundShader: Custom/WorldSpaceGroundShader was not found! Please let Unity compile it first.");
                return;
            }

            string[] materialNames = { "UrbanRuins", "Highways", "OvergrownForests", "Highlands", "Waterways", "SuburbanVillages", "SpecialEvent", "Default" };
            string materialsPath = "Assets/Materials";

            foreach (string name in materialNames)
            {
                string path = $"{materialsPath}/{name}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    mat.shader = customShader;
                    // Set default tiling value for crisp world space mapping
                    mat.SetFloat("_Tiling", 0.1f);
                    
                    // Day/Night and Flashlight defaults
                    mat.SetColor("_DayColor", new Color(1.0f, 0.98f, 0.92f, 1f));
                    mat.SetColor("_DuskColor", new Color(0.95f, 0.55f, 0.35f, 1f));
                    mat.SetColor("_NightColor", new Color(0.06f, 0.09f, 0.18f, 1f));
                    mat.SetFloat("_NightDarkness", 0.85f);
                    mat.SetColor("_FlashlightColor", new Color(1.0f, 0.95f, 0.82f, 1f));
                    mat.SetFloat("_FlashlightRange", 11.0f);
                    mat.SetFloat("_FlashlightAngle", 35.0f);
                    mat.SetFloat("_FlashlightIntensity", 2.2f);
                    mat.SetFloat("_PlayerLightRadius", 2.4f);

                    EditorUtility.SetDirty(mat);
                    Debug.Log($"AssignGroundShader: Assigned Day/Night world-space shader to Material: {name}");
                }
            }

            // Setup a universal prop material using the URP Lit shader and Default texture
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader != null)
            {
                string propMatPath = $"{materialsPath}/PropCardboard.mat";
                Material propMat = AssetDatabase.LoadAssetAtPath<Material>(propMatPath);
                if (propMat == null)
                {
                    propMat = new Material(urpLitShader);
                    AssetDatabase.CreateAsset(propMat, propMatPath);
                }
                else
                {
                    propMat.shader = urpLitShader;
                }

                Texture2D defaultTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Biomes/Default.png");
                if (defaultTex != null)
                {
                    propMat.SetTexture("_BaseMap", defaultTex);
                    propMat.SetTexture("_MainTex", defaultTex);
                }
                EditorUtility.SetDirty(propMat);
                Debug.Log("AssignGroundShader: Created/Updated PropCardboard Material!");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("AssignGroundShader: Successfully updated all ground and prop materials!");
        }
    }
}
