using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Automatically converts any .ttf / .otf font dropped into Assets/Font (or Assets/Fonts)
    /// into a high-quality Dynamic TextMeshPro SDF Font Asset (* SDF.asset), pre-populates
    /// ASCII glyphs so previews render immediately, pairs Regular + Italic typefaces,
    /// and provides a 1-click Global Font Switcher window under Tools/Fonts.
    /// </summary>
    [InitializeOnLoad]
    public static class TMProFontAssetAutoCreator
    {
        private static readonly string[] TargetFolders = new[] { "Assets/Font", "Assets/Fonts" };
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // Printable ASCII (32..126) + common UI symbols so Scene View / Inspector render immediately
        private const string PrepopulateAsciiChars =
            " !\"#$%&'()*+,-./0123456789:;<=>?@" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
            "abcdefghijklmnopqrstuvwxyz{|}~" +
            "•–—…×÷±°©™";

        private static bool _isConverting;

        static TMProFontAssetAutoCreator()
        {
            EditorApplication.delayCall += ConvertAllFontsInFontFolder;
        }

        [MenuItem("Tools/Convert Fonts to TMPro SDF Asset")]
        [MenuItem("Tools/Fonts/Convert All Fonts to TMPro SDF")]
        public static void ConvertAllFontsInFontFolder()
        {
            if (_isConverting) return;
            _isConverting = true;

            try
            {
                int convertedCount = 0;
                int populatedCount = 0;
                List<string> createdPaths = new List<string>();

                foreach (string folder in TargetFolders)
                {
                    if (!AssetDatabase.IsValidFolder(folder)) continue;

                    string[] fontGuids = AssetDatabase.FindAssets("t:Font", new[] { folder });
                    foreach (string guid in fontGuids)
                    {
                        string sourceFontFilePath = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(sourceFontFilePath)) continue;

                        string ext = Path.GetExtension(sourceFontFilePath).ToLowerInvariant();
                        if (ext != ".ttf" && ext != ".otf") continue;

                        Font font = AssetDatabase.LoadAssetAtPath<Font>(sourceFontFilePath);
                        if (font == null) continue;

                        string assetDirectory = (Path.GetDirectoryName(sourceFontFilePath) ?? folder).Replace('\\', '/');
                        string fontFileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFontFilePath);
                        string targetAssetPath = $"{assetDirectory}/{fontFileNameWithoutExt} SDF.asset";

                        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
                        if (existing != null)
                        {
                            // Pre-populate printable ASCII if an existing Dynamic SDF still has an empty 1x1 atlas
                            if (existing.atlasPopulationMode == AtlasPopulationMode.Dynamic &&
                                (existing.characterTable == null || existing.characterTable.Count == 0))
                            {
                                try
                                {
                                    existing.TryAddCharacters(PrepopulateAsciiChars, out _);
                                    EditorUtility.SetDirty(existing);
                                    populatedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[TMProFontAssetAutoCreator] Failed to pre-populate [{existing.name}]: {ex.Message}");
                                }
                            }
                            continue;
                        }

                        if (CreateTMPFontAsset(font, sourceFontFilePath, targetAssetPath))
                        {
                            convertedCount++;
                            createdPaths.Add(targetAssetPath);
                        }
                    }
                }

                int pairedCount = PairRegularAndItalicFonts();

                if (convertedCount > 0 || populatedCount > 0 || pairedCount > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    if (convertedCount > 0 || populatedCount > 0)
                    {
                        Debug.Log($"<color=#50FA7B>[TMProFontAssetAutoCreator]</color> Created {convertedCount}, pre-populated {populatedCount}, paired {pairedCount} TextMeshPro SDF Font Asset(s).");
                    }
                }
            }
            finally
            {
                _isConverting = false;
            }
        }

        public static bool CreateTMPFontAsset(Font font, string sourcePath, string targetPath)
        {
            if (font == null || string.IsNullOrEmpty(targetPath)) return false;

            targetPath = targetPath.Replace('\\', '/');
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);

            // High-res Dynamic SDFAA matching existing Teko SDF settings (90pt, 9px padding, 1024x1024 multi-atlas)
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogWarning($"[TMProFontAssetAutoCreator] Failed to create TMP_FontAsset for [{font.name}]. Ensure 'Include Font Data' is enabled in font import settings.", font);
                return false;
            }

            fontAsset.name = assetName + " SDF";

            // Pre-populate printable ASCII so Inspector & Scene View previews display immediately
            try
            {
                fontAsset.TryAddCharacters(PrepopulateAsciiChars, out _);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TMProFontAssetAutoCreator] Non-fatal warning pre-populating glyphs for [{assetName}]: {ex.Message}");
            }

            AssetDatabase.CreateAsset(fontAsset, targetPath);

            // Save atlas texture(s) & default SDF material as sub-assets inside the .asset container
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    Texture2D tex = fontAsset.atlasTextures[i];
                    if (tex != null)
                    {
                        tex.name = i == 0 ? $"{assetName} Atlas" : $"{assetName} Atlas {i}";
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

        /// <summary>
        /// Automatically links *-Italic SDF as the Regular italic typeface on its matching *-Regular SDF
        /// (e.g. ChangaOne-Regular SDF -> ChangaOne-Italic SDF) so TMP italic rich text works natively.
        /// </summary>
        private static int PairRegularAndItalicFonts()
        {
            int paired = 0;
            foreach (string folder in TargetFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !path.EndsWith("-Regular SDF.asset", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string italicPath = path.Substring(0, path.Length - "-Regular SDF.asset".Length) + "-Italic SDF.asset";
                    TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    TMP_FontAsset italicFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(italicPath);
                    if (regularFont == null || italicFont == null) continue;

                    if (regularFont.fontWeightTable != null && regularFont.fontWeightTable.Length > 4)
                    {
                        if (regularFont.fontWeightTable[4].italicTypeface != italicFont)
                        {
                            regularFont.fontWeightTable[4].italicTypeface = italicFont;
                            EditorUtility.SetDirty(regularFont);
                            paired++;
                        }
                    }
                }
            }
            return paired;
        }

        public static bool SetProjectDefaultTMPFont(TMP_FontAsset newFont)
        {
            if (newFont == null) return false;

            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
                if (guids.Length > 0)
                {
                    settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (settings == null)
            {
                Debug.LogError("[TMProFontAssetAutoCreator] Could not find TMP Settings.asset!");
                return false;
            }

            SerializedObject so = new SerializedObject(settings);
            SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
            if (prop != null)
            {
                prop.objectReferenceValue = newFont;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#50FA7B>[TMProFontAssetAutoCreator]</color> Set TMP Default Font Asset to <b>{newFont.name}</b>");
                return true;
            }

            return false;
        }

        public static int ReplaceFontAcrossProject(TMP_FontAsset newFont, bool includeScenes)
        {
            if (newFont == null) return 0;

            SetProjectDefaultTMPFont(newFont);
            int updatedComponents = 0;

            // 1. Update all prefabs under Assets/Resources_Addressable/Prefabs
            if (AssetDatabase.IsValidFolder("Assets/Resources_Addressable/Prefabs"))
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources_Addressable/Prefabs" });
                foreach (string guid in prefabGuids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(prefabPath)) continue;

                    GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (root == null) continue;

                    try
                    {
                        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                        bool modified = false;
                        foreach (TMP_Text t in texts)
                        {
                            if (t != null && t.font != newFont)
                            {
                                t.font = newFont;
                                if (newFont.material != null)
                                    t.fontSharedMaterial = newFont.material;
                                updatedComponents++;
                                modified = true;
                            }
                        }

                        if (modified)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            // 2. Optionally update scenes under Assets/Scenes
            if (includeScenes && AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                string originalScenePath = SceneManager.GetActiveScene().path;
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

                foreach (string guid in sceneGuids)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(scenePath)) continue;

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    bool modified = false;
                    foreach (GameObject rootObj in scene.GetRootGameObjects())
                    {
                        TMP_Text[] texts = rootObj.GetComponentsInChildren<TMP_Text>(true);
                        foreach (TMP_Text t in texts)
                        {
                            if (t != null && t.font != newFont)
                            {
                                t.font = newFont;
                                if (newFont.material != null)
                                    t.fontSharedMaterial = newFont.material;
                                updatedComponents++;
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }

                if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#50FA7B>[TMProFontAssetAutoCreator]</color> Applied <b>{newFont.name}</b> as default font and updated {updatedComponents} TMP_Text component(s).");
            return updatedComponents;
        }
    }

    /// <summary>
    /// Automatically triggers TMP SDF font generation immediately whenever a new .ttf/.otf file
    /// is imported into Assets/Font or Assets/Fonts (no manual menu click or recompile required).
    /// </summary>
    public class TMProFontAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool foundFont = false;
            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (IsProjectFontAsset(importedAssets[i]))
                {
                    foundFont = true;
                    break;
                }
            }

            if (!foundFont)
            {
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    if (IsProjectFontAsset(movedAssets[i]))
                    {
                        foundFont = true;
                        break;
                    }
                }
            }

            if (foundFont)
            {
                EditorApplication.delayCall += TMProFontAssetAutoCreator.ConvertAllFontsInFontFolder;
            }
        }

        private static bool IsProjectFontAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string norm = path.Replace('\\', '/');
            bool inFolder = norm.StartsWith("Assets/Font/", StringComparison.OrdinalIgnoreCase) ||
                            norm.StartsWith("Assets/Fonts/", StringComparison.OrdinalIgnoreCase);
            if (!inFolder) return false;
            return norm.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                   norm.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Simple 1-click Editor Window to preview all TMP SDF fonts in Assets/Font
    /// and apply any font as the project default (or replace across all UI prefabs & scenes).
    /// </summary>
    public class ProjectFontSwitcherWindow : EditorWindow
    {
        private Vector2 _scroll;
        private List<TMP_FontAsset> _fonts = new List<TMP_FontAsset>();

        [MenuItem("Tools/Fonts/Open Global Font Switcher")]
        public static void OpenWindow()
        {
            var win = GetWindow<ProjectFontSwitcherWindow>("Project Font Switcher");
            win.minSize = new Vector2(460, 320);
            win.RefreshFontList();
            win.Show();
        }

        private void OnEnable()
        {
            RefreshFontList();
        }

        private void RefreshFontList()
        {
            _fonts.Clear();
            string[] folders = AssetDatabase.IsValidFolder("Assets/Font")
                ? new[] { "Assets/Font" }
                : Array.Empty<string>();

            if (folders.Length == 0) return;

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", folders);
            foreach (string g in guids)
            {
                TMP_FontAsset f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
                if (f != null) _fonts.Add(f);
            }
            _fonts.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("The Last Empire — TextMeshPro Font Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "All .ttf/.otf fonts in Assets/Font are auto-converted into Dynamic TMP SDF assets.\n" +
                "Click 'Set Default' to change TMP Settings, or 'Apply All UI' to also swap existing Prefabs & Scenes.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Convert Missing .ttf -> SDF", GUILayout.Height(26)))
            {
                TMProFontAssetAutoCreator.ConvertAllFontsInFontFolder();
                RefreshFontList();
            }
            if (GUILayout.Button("Refresh List", GUILayout.Width(100), GUILayout.Height(26)))
            {
                RefreshFontList();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            TMP_FontAsset currentDefault = TMP_Settings.defaultFontAsset;
            EditorGUILayout.LabelField($"Current TMP Default Font: {(currentDefault != null ? currentDefault.name : "(None)")}", EditorStyles.miniBoldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (TMP_FontAsset font in _fonts)
            {
                if (font == null) continue;
                bool isCurrent = (font == currentDefault);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(isCurrent ? $"★ {font.name} (Default)" : font.name, isCurrent ? EditorStyles.boldLabel : EditorStyles.label);

                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(font);
                    Selection.activeObject = font;
                }

                if (GUILayout.Button("Set Default", GUILayout.Width(90)))
                {
                    TMProFontAssetAutoCreator.SetProjectDefaultTMPFont(font);
                }

                if (GUILayout.Button("Apply All UI", GUILayout.Width(95)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Apply Font Across Project",
                        $"Set '{font.name}' as TMP Default Font AND replace TMP_Text fonts across all Prefabs and Scenes in Assets/Scenes?",
                        "Apply Everywhere",
                        "Cancel"))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            TMProFontAssetAutoCreator.ReplaceFontAcrossProject(font, true);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
