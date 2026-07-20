using UnityEngine;
using UnityEditor;
using System.IO;

namespace TheLastEmpire
{
    public class SpriteProcessor : EditorWindow
    {
        [MenuItem("Tools/Make Sprite Backgrounds Transparent")]
        public static void MakeBackgroundsTransparent()
        {
            string[] files = {
                "Assets/Resources/image/character/zombie_spritesheet.png",
                "Assets/Resources/image/character/player_directional_spritesheet.png",
                "Assets/Resources/image/items/items_icons_spritesheet.png"
            };

            int processedCount = 0;

            foreach (string file in files)
            {
                if (!File.Exists(file))
                {
                    Debug.LogWarning($"[SpriteProcessor] File not found: {file}");
                    continue;
                }

                // 1. Force the texture importer to be Read/Write enabled and uncompressed so we can access pixels
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                // 2. Load texture pixels directly from the file
                byte[] fileData = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(fileData);

                Color[] pixels = tex.GetPixels();
                if (pixels.Length == 0) continue;

                // Sample the background color from the bottom-left pixel (0, 0)
                Color bgColor = pixels[0]; 

                // Threshold difference (handling slight color compression noise)
                float threshold = 0.18f;

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    float diff = Mathf.Abs(c.r - bgColor.r) + Mathf.Abs(c.g - bgColor.g) + Mathf.Abs(c.b - bgColor.b);
                    if (diff < threshold)
                    {
                        pixels[i] = Color.clear; // Key out to transparency
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();

                // 3. Write back to PNG format
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(file, bytes);

                // 4. Force Unity AssetDatabase to refresh and compile the transparent image
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[SpriteProcessor] Processed background transparency for: {file}");
                processedCount++;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Processed backgrounds for {processedCount} spritesheets successfully!", "OK");
        }
    }
}
