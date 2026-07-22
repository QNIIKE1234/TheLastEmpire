using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public static class AssignGroundShader
    {
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
                    // Set default tiling value for world space mapping
                    mat.SetFloat("_Tiling", 0.05f); // default scale for smooth seamless mapping
                    EditorUtility.SetDirty(mat);
                    Debug.Log($"AssignGroundShader: Assigned custom world-space shader to Material: {name}");
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
