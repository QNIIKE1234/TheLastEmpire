#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.EditorTools
{
    /// <summary>
    /// Creates the Resources/Materials/CharacterToonOutline.mat asset (so the shader is always included in builds)
    /// and optionally attaches CharacterToonOutline to all Character & NPC prefabs for live Edit Mode preview.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterOutlineSetupTool
    {
        private const string ShaderName  = "TheLastEmpire/CharacterToonOutline";
        private const string MatDir      = "Assets/Resources/Materials";
        private const string MatPath     = "Assets/Resources/Materials/CharacterToonOutline.mat";
        private const string AutoMatKey  = "TLE_ToonOutlineMat_Ensured_v2";

        static CharacterOutlineSetupTool()
        {
            EditorApplication.delayCall += EnsureOutlineMaterialAsset;
        }

        public static void EnsureOutlineMaterialAsset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(AutoMatKey, false) && File.Exists(MatPath)) return;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return;

            if (!Directory.Exists(MatDir))
            {
                Directory.CreateDirectory(MatDir);
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "CharacterToonOutline";
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.SetColor("_OutlineColor", new Color(0.04f, 0.03f, 0.05f, 1f));
            mat.SetFloat("_OutlineWidth", 0.010f);
            mat.SetFloat("_ScreenSpaceScale", 0.0016f);
            mat.SetFloat("_MinWorldWidth", 0.006f);
            mat.SetFloat("_MaxWorldWidth", 0.026f);
            mat.SetFloat("_ZBias", 0.0006f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SessionState.SetBool(AutoMatKey, true);
        }

        [MenuItem("Tools/TheLastEmpire/Apply Toon Outline to All Character & NPC Prefabs")]
        public static void ApplyToAllCharacterPrefabs()
        {
            EnsureOutlineMaterialAsset();

            string[] folders =
            {
                "Assets/Resources_Addressable/Prefabs/Character/Model",
                "Assets/Resources_Addressable/Prefabs/Character"
            };

            int updated = 0;
            for (int f = 0; f < folders.Length; f++)
            {
                if (!Directory.Exists(folders[f])) continue;
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folders[f] });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;
                    string fname = Path.GetFileNameWithoutExtension(path);
                    // Only target Chibi player models and NPC models (skip raw Enemy/Robot prefabs unless desired)
                    if (!fname.StartsWith("Chibi") && !fname.StartsWith("NPC_")) continue;

                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;
                    try
                    {
                        if (root.GetComponent<CharacterToonOutline>() == null)
                        {
                            root.AddComponent<CharacterToonOutline>();
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            updated++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterOutlineSetupTool] Attached CharacterToonOutline to {updated} Character/NPC prefabs!");
        }
    }
}
#endif
