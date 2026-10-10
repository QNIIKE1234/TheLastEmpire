using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public class FbxToPrefabConverterWindow : EditorWindow
    {
        public enum SourceMode
        {
            Folder,
            SpecificFiles,
            CurrentSelection
        }

        public enum DestinationMode
        {
            CustomFolder,
            SameAsFbxFolder
        }

        public enum ColliderOption
        {
            None,
            BoxCollider,
            CapsuleCollider,
            MeshCollider
        }

        public enum RotationHandling
        {
            PreserveModelRotation,
            ResetToZero,
            CustomOffset
        }

        // --- Window Settings ---
        [SerializeField] private SourceMode sourceMode = SourceMode.Folder;
        [SerializeField] private DefaultAsset sourceFolderAsset;
        [SerializeField] private string sourceFolderPath = "Assets";
        [SerializeField] private bool includeSubfolders = true;

        [SerializeField] private List<GameObject> specificFbxList = new List<GameObject>();

        [SerializeField] private DestinationMode destinationMode = DestinationMode.CustomFolder;
        [SerializeField] private DefaultAsset destinationFolderAsset;
        [SerializeField] private string destinationFolderPath = "Assets/Resources/Prefabs";

        // Prefab Setup
        [SerializeField] private string namePrefix = "";
        [SerializeField] private string nameSuffix = "";
        [SerializeField] private bool resetRootPositionAndScale = true;
        [SerializeField] private RotationHandling rotationHandling = RotationHandling.PreserveModelRotation;
        [SerializeField] private Vector3 customRotationEuler = new Vector3(-90f, 0f, 0f);
        [SerializeField] private bool unpackModelLink = false;
        [SerializeField] private bool removeBlenderCamerasAndLights = true;
        [SerializeField] private bool overwriteExisting = true;

        // Optional Components
        [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
        [SerializeField] private ColliderOption addCollider = ColliderOption.None;
        [SerializeField] private bool addRigidbody = false;

        // UI State
        private Vector2 _scrollPos;
        private bool _showPreviewFoldout = true;
        private bool _showComponentOptionsFoldout = false;

        [MenuItem("Tools/FBX to Prefabs Converter", false, 100)]
        [MenuItem("TheLastEmpire/FBX to Prefabs Converter", false, 100)]
        public static void OpenWindow()
        {
            FbxToPrefabConverterWindow window = GetWindow<FbxToPrefabConverterWindow>("FBX to Prefab");
            window.minSize = new Vector2(460, 580);
            window.Show();
        }

        [MenuItem("Assets/TheLastEmpire/Convert Selected FBX to Prefab(s)", false, 25)]
        private static void ConvertSelectedContext()
        {
            List<string> fbxPaths = GetFbxPathsFromSelection();
            if (fbxPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Convert FBX to Prefabs", "No FBX files found in current selection.", "OK");
                return;
            }

            FbxToPrefabConverterWindow window = GetWindow<FbxToPrefabConverterWindow>("FBX to Prefab");
            window.sourceMode = SourceMode.CurrentSelection;
            window.Show();
        }

        [MenuItem("Assets/TheLastEmpire/Convert Selected FBX to Prefab(s)", true)]
        private static bool ValidateConvertSelectedContext()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        private void OnEnable()
        {
            // Set sensible default folders if existing
            if (destinationFolderAsset == null)
            {
                if (AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                {
                    destinationFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Resources/Prefabs");
                    destinationFolderPath = "Assets/Resources/Prefabs";
                }
                else if (AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    destinationFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Prefabs");
                    destinationFolderPath = "Assets/Prefabs";
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            DrawHeader();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(6);
            DrawSourceSection();

            EditorGUILayout.Space(10);
            DrawDestinationSection();

            EditorGUILayout.Space(10);
            DrawPrefabOptionsSection();

            EditorGUILayout.Space(10);
            DrawPreviewSection();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            DrawBottomButtons();
            EditorGUILayout.Space(8);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField("FBX to Prefab Batch Converter", titleStyle);

                GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                EditorGUILayout.LabelField("Batch convert FBX 3D models into clean, ready-to-use Unity Prefabs", subStyle);
            }
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("1. Source Settings (FBX ขาเข้า)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                sourceMode = (SourceMode)EditorGUILayout.EnumPopup("Source Mode", sourceMode);

                if (sourceMode == SourceMode.Folder)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    sourceFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Source Folder", sourceFolderAsset, typeof(DefaultAsset), false);
                    if (EditorGUI.EndChangeCheck() && sourceFolderAsset != null)
                    {
                        string path = AssetDatabase.GetAssetPath(sourceFolderAsset);
                        if (AssetDatabase.IsValidFolder(path)) sourceFolderPath = path;
                    }

                    if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("Select Folder with FBX files", "Assets", "");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            string relative = MakeRelativePath(selected);
                            sourceFolderPath = relative;
                            sourceFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Folder Path: " + sourceFolderPath, EditorStyles.miniLabel);
                    includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
                }
                else if (sourceMode == SourceMode.SpecificFiles)
                {
                    EditorGUILayout.LabelField("Drag & Drop FBX Files Here:", EditorStyles.miniBoldLabel);

                    // Drag and Drop Area
                    Rect dropArea = GUILayoutUtility.GetRect(0.0f, 45.0f, GUILayout.ExpandWidth(true));
                    GUI.Box(dropArea, "Drop FBX Files or Folders Here", EditorStyles.helpBox);
                    HandleDragAndDrop(dropArea);

                    // List items
                    int removeIndex = -1;
                    for (int i = 0; i < specificFbxList.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        specificFbxList[i] = (GameObject)EditorGUILayout.ObjectField($"[{i + 1}]", specificFbxList[i], typeof(GameObject), false);
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            removeIndex = i;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (removeIndex >= 0)
                    {
                        specificFbxList.RemoveAt(removeIndex);
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+ Add File Slot"))
                    {
                        specificFbxList.Add(null);
                    }
                    if (GUILayout.Button("Clear All") && specificFbxList.Count > 0)
                    {
                        specificFbxList.Clear();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else // CurrentSelection
                {
                    int selCount = GetFbxPathsFromSelection().Count;
                    EditorGUILayout.HelpBox($"Currently selected in Project View: {selCount} FBX file(s).", MessageType.Info);
                }
            }
        }

        private void DrawDestinationSection()
        {
            EditorGUILayout.LabelField("2. Destination Settings (เป้าหมาย Result)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                destinationMode = (DestinationMode)EditorGUILayout.EnumPopup("Destination Mode", destinationMode);

                if (destinationMode == DestinationMode.CustomFolder)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    destinationFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Result Folder", destinationFolderAsset, typeof(DefaultAsset), false);
                    if (EditorGUI.EndChangeCheck() && destinationFolderAsset != null)
                    {
                        string path = AssetDatabase.GetAssetPath(destinationFolderAsset);
                        if (AssetDatabase.IsValidFolder(path)) destinationFolderPath = path;
                    }

                    if (GUILayout.Button("Browse...", GUILayout.Width(75)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("Select Output Prefab Folder", "Assets", "");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            string relative = MakeRelativePath(selected);
                            destinationFolderPath = relative;
                            destinationFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Result Path: " + destinationFolderPath, EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox("Prefabs will be saved directly inside the same folder as their corresponding FBX files.", MessageType.Info);
                }
            }
        }

        private void DrawPrefabOptionsSection()
        {
            EditorGUILayout.LabelField("3. Prefab Configurations & Cleanup", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Naming
                EditorGUILayout.BeginHorizontal();
                namePrefix = EditorGUILayout.TextField("Name Prefix", namePrefix);
                nameSuffix = EditorGUILayout.TextField("Suffix", nameSuffix);
                EditorGUILayout.EndHorizontal();

                resetRootPositionAndScale = EditorGUILayout.Toggle(new GUIContent("Reset Position & Scale", "Sets Position to (0,0,0) and Scale to (1,1,1)"), resetRootPositionAndScale);
                rotationHandling = (RotationHandling)EditorGUILayout.EnumPopup("Root Rotation Mode", rotationHandling);
                if (rotationHandling == RotationHandling.CustomOffset)
                {
                    customRotationEuler = EditorGUILayout.Vector3Field("Rotation Euler", customRotationEuler);
                }
                unpackModelLink = EditorGUILayout.Toggle(new GUIContent("Unpack Model Prefab", "Creates a standalone Prefab instead of maintaining model prefab connection"), unpackModelLink);
                removeBlenderCamerasAndLights = EditorGUILayout.Toggle(new GUIContent("Remove Cameras & Lights", "Cleans out unwanted Camera or Light objects exported from Blender"), removeBlenderCamerasAndLights);
                overwriteExisting = EditorGUILayout.Toggle(new GUIContent("Overwrite Existing Prefabs", "Overwrites existing .prefab files if they already exist"), overwriteExisting);

                // Optional components
                _showComponentOptionsFoldout = EditorGUILayout.Foldout(_showComponentOptionsFoldout, "Optional Components (Animator / Collider)", true);
                if (_showComponentOptionsFoldout)
                {
                    EditorGUI.indentLevel++;
                    defaultAnimatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Attach Animator Controller", defaultAnimatorController, typeof(RuntimeAnimatorController), false);
                    addCollider = (ColliderOption)EditorGUILayout.EnumPopup("Attach Collider", addCollider);
                    addRigidbody = EditorGUILayout.Toggle("Attach Rigidbody", addRigidbody);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawPreviewSection()
        {
            List<string> fbxList = CollectFbxPaths();
            _showPreviewFoldout = EditorGUILayout.Foldout(_showPreviewFoldout, $"Preview Target List ({fbxList.Count} FBX file(s) found)", true);
            if (_showPreviewFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (fbxList.Count == 0)
                    {
                        EditorGUILayout.LabelField("No FBX files found matching current settings.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        int maxShow = Mathf.Min(fbxList.Count, 15);
                        for (int i = 0; i < maxShow; i++)
                        {
                            string fbxPath = fbxList[i];
                            string targetPath = GetTargetPrefabPath(fbxPath);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(Path.GetFileName(fbxPath), EditorStyles.boldLabel, GUILayout.Width(180));
                            EditorGUILayout.LabelField("-> " + targetPath, EditorStyles.miniLabel);
                            EditorGUILayout.EndHorizontal();
                        }
                        if (fbxList.Count > maxShow)
                        {
                            EditorGUILayout.LabelField($"... and {fbxList.Count - maxShow} more files.", EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }

        private void DrawBottomButtons()
        {
            List<string> fbxList = CollectFbxPaths();
            bool canConvert = fbxList.Count > 0 && (destinationMode == DestinationMode.SameAsFbxFolder || !string.IsNullOrEmpty(destinationFolderPath));

            GUI.backgroundColor = canConvert ? new Color(0.25f, 0.85f, 0.45f) : Color.gray;
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 42
            };

            if (GUILayout.Button($"Convert {fbxList.Count} FBX to Prefab(s)", buttonStyle))
            {
                if (canConvert)
                {
                    ExecuteConversion(fbxList);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition)) return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (UnityEngine.Object dragged in DragAndDrop.objectReferences)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(dragged);
                            if (AssetDatabase.IsValidFolder(assetPath))
                            {
                                // Add all FBX in this folder
                                string[] found = Directory.GetFiles(assetPath, "*.fbx", SearchOption.AllDirectories);
                                foreach (string f in found)
                                {
                                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(NormalizePath(f));
                                    if (go != null && !specificFbxList.Contains(go)) specificFbxList.Add(go);
                                }
                            }
                            else if (dragged is GameObject go && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!specificFbxList.Contains(go)) specificFbxList.Add(go);
                            }
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        private List<string> CollectFbxPaths()
        {
            List<string> results = new List<string>();

            if (sourceMode == SourceMode.Folder)
            {
                if (AssetDatabase.IsValidFolder(sourceFolderPath))
                {
                    SearchOption opt = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    string fullPath = Path.Combine(Directory.GetCurrentDirectory(), sourceFolderPath);
                    if (Directory.Exists(fullPath))
                    {
                        string[] files = Directory.GetFiles(fullPath, "*.fbx", opt);
                        foreach (string f in files)
                        {
                            string relative = MakeRelativePath(f);
                            results.Add(NormalizePath(relative));
                        }
                    }
                }
            }
            else if (sourceMode == SourceMode.SpecificFiles)
            {
                foreach (GameObject go in specificFbxList)
                {
                    if (go == null) continue;
                    string path = AssetDatabase.GetAssetPath(go);
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!results.Contains(path)) results.Add(path);
                    }
                }
            }
            else // CurrentSelection
            {
                results = GetFbxPathsFromSelection();
            }

            return results;
        }

        private static List<string> GetFbxPathsFromSelection()
        {
            List<string> results = new List<string>();
            UnityEngine.Object[] selected = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);

            foreach (UnityEngine.Object obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
                    if (Directory.Exists(fullPath))
                    {
                        string[] files = Directory.GetFiles(fullPath, "*.fbx", SearchOption.AllDirectories);
                        foreach (string f in files)
                        {
                            string rel = MakeRelativePath(f);
                            if (!results.Contains(rel)) results.Add(rel);
                        }
                    }
                }
                else if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    if (!results.Contains(path)) results.Add(path);
                }
            }

            return results;
        }

        private string GetTargetPrefabPath(string fbxPath)
        {
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            string prefabName = $"{namePrefix}{fbxName}{nameSuffix}.prefab";

            string targetDir;
            if (destinationMode == DestinationMode.SameAsFbxFolder)
            {
                targetDir = Path.GetDirectoryName(fbxPath);
            }
            else
            {
                targetDir = destinationFolderPath;
            }

            return NormalizePath(Path.Combine(targetDir, prefabName));
        }

        private void ExecuteConversion(List<string> fbxPaths)
        {
            if (fbxPaths.Count == 0) return;

            // Ensure destination directory exists if custom folder
            if (destinationMode == DestinationMode.CustomFolder)
            {
                EnsureDirectoryExists(destinationFolderPath);
            }

            int successCount = 0;
            int skippedCount = 0;
            string lastCreatedPath = "";

            try
            {
                for (int i = 0; i < fbxPaths.Count; i++)
                {
                    string fbxPath = fbxPaths[i];
                    string targetPath = GetTargetPrefabPath(fbxPath);

                    EditorUtility.DisplayProgressBar("Converting FBX to Prefabs",
                        $"Processing ({i + 1}/{fbxPaths.Count}): {Path.GetFileName(fbxPath)}",
                        (float)i / fbxPaths.Count);

                    // Check overwrite
                    if (File.Exists(targetPath) && !overwriteExisting)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Ensure target directory
                    string targetFolder = Path.GetDirectoryName(targetPath);
                    EnsureDirectoryExists(targetFolder);

                    GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    if (fbxAsset == null)
                    {
                        Debug.LogWarning($"[FbxToPrefab] Failed to load FBX at {fbxPath}");
                        continue;
                    }

                    // Instantiate
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                    if (instance == null)
                    {
                        instance = UnityEngine.Object.Instantiate(fbxAsset);
                    }

                    instance.name = Path.GetFileNameWithoutExtension(targetPath);

                    // 1. Reset root transform
                    if (resetRootPositionAndScale)
                    {
                        instance.transform.position = Vector3.zero;
                        instance.transform.localScale = Vector3.one;
                    }

                    if (rotationHandling == RotationHandling.ResetToZero)
                    {
                        instance.transform.rotation = Quaternion.identity;
                    }
                    else if (rotationHandling == RotationHandling.CustomOffset)
                    {
                        instance.transform.rotation = Quaternion.Euler(customRotationEuler);
                    }
                    // If PreserveModelRotation, leave the FBX instance's rotation untouched!

                    // 2. Remove Blender Cameras and Lights
                    if (removeBlenderCamerasAndLights)
                    {
                        CleanBlenderArtifacts(instance);
                    }

                    // 3. Attach optional components
                    if (defaultAnimatorController != null)
                    {
                        Animator animator = instance.GetComponent<Animator>();
                        if (animator == null) animator = instance.AddComponent<Animator>();
                        animator.runtimeAnimatorController = defaultAnimatorController;
                    }

                    if (addCollider != ColliderOption.None)
                    {
                        AttachCollider(instance, addCollider);
                    }

                    if (addRigidbody)
                    {
                        if (instance.GetComponent<Rigidbody>() == null)
                        {
                            instance.AddComponent<Rigidbody>();
                        }
                    }

                    // 4. Unpack if requested
                    if (unpackModelLink)
                    {
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }

                    // 5. Save as Prefab
                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, targetPath, out bool success);
                    DestroyImmediate(instance);

                    if (success && savedPrefab != null)
                    {
                        successCount++;
                        lastCreatedPath = targetPath;
                    }
                    else
                    {
                        Debug.LogError($"[FbxToPrefab] Failed to save prefab to: {targetPath}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"<color=#00FF88>[FbxToPrefab]</color> Completed conversion! Created: <b>{successCount}</b> prefab(s), Skipped: <b>{skippedCount}</b>.");

            if (!string.IsNullOrEmpty(lastCreatedPath))
            {
                UnityEngine.Object lastAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(lastCreatedPath);
                if (lastAsset != null)
                {
                    EditorGUIUtility.PingObject(lastAsset);
                }
            }

            EditorUtility.DisplayDialog("FBX to Prefab Complete",
                $"Successfully converted {successCount} FBX file(s) into Prefabs!" +
                (skippedCount > 0 ? $"\nSkipped {skippedCount} existing file(s)." : ""),
                "OK");
        }

        private static void CleanBlenderArtifacts(GameObject root)
        {
            List<GameObject> toRemove = new List<GameObject>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;

                // Check camera or light components
                if (child.GetComponent<Camera>() != null || child.GetComponent<Light>() != null)
                {
                    toRemove.Add(child.gameObject);
                    continue;
                }

                // Check common blender object names
                string nameLower = child.name.ToLower();
                if (nameLower.StartsWith("camera") || nameLower.StartsWith("light") ||
                    nameLower.Contains("lamp") || nameLower.Contains("preview_"))
                {
                    toRemove.Add(child.gameObject);
                }
            }

            foreach (GameObject obj in toRemove)
            {
                if (obj != null) DestroyImmediate(obj);
            }
        }

        private static void AttachCollider(GameObject root, ColliderOption opt)
        {
            switch (opt)
            {
                case ColliderOption.BoxCollider:
                    if (root.GetComponent<BoxCollider>() == null)
                    {
                        BoxCollider bc = root.AddComponent<BoxCollider>();
                        // Calculate bounds from renderers
                        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            Bounds bounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                            bc.center = root.transform.InverseTransformPoint(bounds.center);
                            bc.size = bounds.size;
                        }
                    }
                    break;

                case ColliderOption.CapsuleCollider:
                    if (root.GetComponent<CapsuleCollider>() == null)
                    {
                        CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
                        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            Bounds bounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                            cc.center = root.transform.InverseTransformPoint(bounds.center);
                            cc.height = bounds.size.y;
                            cc.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                        }
                    }
                    break;

                case ColliderOption.MeshCollider:
                    foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
                    {
                        if (mf.GetComponent<MeshCollider>() == null && mf.sharedMesh != null)
                        {
                            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                        }
                    }
                    break;
            }
        }

        private static void EnsureDirectoryExists(string relativeAssetPath)
        {
            if (string.IsNullOrEmpty(relativeAssetPath)) return;
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativeAssetPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }
        }

        private static string MakeRelativePath(string fullPath)
        {
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
            string path = fullPath.Replace('\\', '/');
            if (path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(projectRoot.Length);
                if (path.StartsWith("/")) path = path.Substring(1);
            }
            return path;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
