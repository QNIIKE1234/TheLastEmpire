using UnityEngine;
using UnityEditor;

namespace TheLastEmpire
{
    public class CreateMaterialsEditor
    {
        [MenuItem("Tools/Create URP Materials")]
        public static void CreateURPMaterials()
        {
            string folderPath = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                Debug.LogWarning("Could not find 'Universal Render Pipeline/Lit' shader! Using fallback Standard shader.");
                urpShader = Shader.Find("Standard");
            }

            var colors = new[]
            {
                new { Name = "Red", Color = Color.red },
                new { Name = "Green", Color = Color.green },
                new { Name = "Blue", Color = Color.blue },
                new { Name = "Yellow", Color = new Color(1.0f, 0.92f, 0.0f) },
                new { Name = "Cyan", Color = Color.cyan },
                new { Name = "Magenta", Color = Color.magenta },
                new { Name = "Orange", Color = new Color(1.0f, 0.5f, 0.0f) },
                new { Name = "Purple", Color = new Color(0.5f, 0.0f, 0.5f) },
                new { Name = "Pink", Color = new Color(1.0f, 0.75f, 0.8f) },
                new { Name = "DarkGray", Color = new Color(0.25f, 0.25f, 0.25f) },
                new { Name = "White", Color = Color.white },
                new { Name = "Lime", Color = new Color(0.75f, 1.0f, 0.0f) }
            };

            foreach (var c in colors)
            {
                Material mat = new Material(urpShader);
                
                // Set color for URP Lit shader
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", c.Color);
                }
                
                // Set color for Standard shader fallback
                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", c.Color);
                }

                string assetPath = $"{folderPath}/{c.Name}.mat";
                
                // Overwrite if already exists
                AssetDatabase.CreateAsset(mat, assetPath);
                Debug.Log($"Created material: {assetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", "Created 12 materials successfully in Assets/Materials!", "OK");
        }
    }
}
