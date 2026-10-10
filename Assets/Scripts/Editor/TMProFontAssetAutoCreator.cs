using System.IO;
using UnityEngine;
using UnityEditor;
using TMPro;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class TMProFontAssetAutoCreator
    {
        static TMProFontAssetAutoCreator()
        {
            EditorApplication.delayCall += ConvertAllFontsInFontFolder;
        }

        [MenuItem("Tools/Convert Fonts to TMPro SDF Asset")]
        public static void ConvertAllFontsInFontFolder()
        {
            string[] targetFolders = new[] { "Assets/Font", "Assets/Fonts" };
            int convertedCount = 0;

            foreach (string folder in targetFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                string[] fontGuids = AssetDatabase.FindAssets("t:Font", new[] { folder });
                foreach (string guid in fontGuids)
                {
                    string sourceFontFilePath = AssetDatabase.GUIDToAssetPath(guid);
                    Font font = AssetDatabase.LoadAssetAtPath<Font>(sourceFontFilePath);
                    if (font == null) continue;

                    string assetDirectory = Path.GetDirectoryName(sourceFontFilePath);
                    string fontFileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFontFilePath);
                    string targetAssetPath = $"{assetDirectory}/{fontFileNameWithoutExt} SDF.asset";

                    // Check if already created
                    if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath) != null)
                    {
                        continue;
                    }

                    if (CreateTMPFontAsset(font, sourceFontFilePath, targetAssetPath))
                    {
                        convertedCount++;
                    }
                }
            }

            if (convertedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"<color=#50FA7B>[TMProFontAssetAutoCreator]</color> Successfully converted {convertedCount} font(s) to TextMesh Pro SDF Assets!");
            }
        }

        public static bool CreateTMPFontAsset(Font font, string sourcePath, string targetPath)
        {
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);

            // Use TextMesh Pro's built-in factory method which handles FontEngine,
            // FaceInfo, Material, Texture2D, and Dynamic atlas setup properly across Unity versions
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[TMProFontAssetAutoCreator] Failed to create TMP_FontAsset for [{font.name}]. Ensure font data is included in import settings.", font);
                return false;
            }

            fontAsset.name = assetName + " SDF";

            AssetDatabase.CreateAsset(fontAsset, targetPath);

            // Save sub-assets (Texture & Material) into the .asset file container
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    Texture2D tex = fontAsset.atlasTextures[i];
                    if (tex != null)
                    {
                        tex.name = $"{assetName} Atlas";
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                    }
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{assetName} Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            Debug.Log($"<color=#66D9EF>[TMProFontAssetAutoCreator]</color> Created TMP Font Asset: <b>{targetPath}</b>");
            return true;
        }
    }
}
