using UnityEngine;
using UnityEditor;

namespace TheLastEmpire
{
    public class CreateItemMaterialsEditor
    {
        [MenuItem("Tools/Create Item Materials")]
        public static void CreateItemMaterials()
        {
            string folderPath = "Assets/Materials/Items";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                // Ensure parent exists
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets", "Materials");
                }
                AssetDatabase.CreateFolder("Assets/Materials", "Items");
            }

            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                Debug.LogWarning("Could not find 'Universal Render Pipeline/Lit' shader! Using fallback Standard shader.");
                urpShader = Shader.Find("Standard");
            }

            var items = new[]
            {
                new { Name = "Ammo", TexturePath = "Assets/Textures/AmmoCrate_Texture.png" },
                new { Name = "Potion", TexturePath = "Assets/Textures/PotionCrate_Texture.png" },
                new { Name = "Bread", TexturePath = "Assets/Textures/BreadCrate_Texture.png" },
                new { Name = "ETC", TexturePath = "Assets/Textures/ETCCrate_Texture.png" },
                new { Name = "Money", TexturePath = "Assets/Textures/MoneyCrate_Texture.png" }
            };

            foreach (var item in items)
            {
                Material mat = new Material(urpShader);
                
                // Load Texture
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(item.TexturePath);
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", tex);
                    }
                    if (mat.HasProperty("_MainTex"))
                    {
                        mat.SetTexture("_MainTex", tex);
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find texture at: {item.TexturePath}. Make sure Unity finished importing it.");
                }

                // Clean color to white so texture displays correctly without tint
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", Color.white);
                }
                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", Color.white);
                }

                string assetPath = $"{folderPath}/{item.Name}_Material.mat";
                AssetDatabase.CreateAsset(mat, assetPath);
                Debug.Log($"Created item material: {assetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", "Created item materials in Assets/Materials/Items!", "OK");
        }
    }
}
