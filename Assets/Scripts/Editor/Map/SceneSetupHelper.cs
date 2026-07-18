using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using TheLastEmpire.Runtime.Map;

namespace TheLastEmpire.Editor.Map
{
    public static class SceneSetupHelper
    {
        [MenuItem("The Last Empire/Setup Map Test Scene")]
        public static void SetupMapTestScene()
        {
            // 1. Create a new scene with default light and camera
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. Create the Visualizer GameObject in the scene
            GameObject visualizerObj = new GameObject("WorldMapVisualizer");
            WorldMapVisualizer visualizer = visualizerObj.AddComponent<WorldMapVisualizer>();
            
            // Configure SpriteRenderer (automatically added by RequireComponent)
            SpriteRenderer spriteRenderer = visualizerObj.GetComponent<SpriteRenderer>();
            spriteRenderer.drawMode = SpriteDrawMode.Simple;

            // 3. Find or Create default WorldMapGenerator asset
            string directory = "Assets/Settings";
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            string assetPath = "Assets/Settings/WorldMapGenerator_Default.asset";
            WorldMapGenerator generator = AssetDatabase.LoadAssetAtPath<WorldMapGenerator>(assetPath);
            if (generator == null)
            {
                generator = ScriptableObject.CreateInstance<WorldMapGenerator>();
                AssetDatabase.CreateAsset(generator, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created default WorldMapGenerator asset at: " + assetPath);
            }

            // Assign the generator to visualizer
            visualizer.MapGenerator = generator;
            visualizer.PixelsPerUnit = 10f; // 64 pixels wide will be 6.4 units in world space

            // Adjust Main Camera to focus on the map visualizer
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = 5f; // fits the 6.4 unit map well
                mainCamera.transform.position = new Vector3(0, 0, -10);
            }

            // Mark the visualizer dirty to ensure serialization
            EditorUtility.SetDirty(visualizerObj);

            // 4. Save the scene to the Assets/Scenes/ directory
            string sceneDir = "Assets/Scenes";
            if (!System.IO.Directory.Exists(sceneDir))
            {
                System.IO.Directory.CreateDirectory(sceneDir);
            }
            
            string scenePath = "Assets/Scenes/MapTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log("Successfully created and saved MapTestScene at: " + scenePath);

            // Select visualizer in Hierarchy
            Selection.activeGameObject = visualizerObj;
            
            // Refresh database to show changes
            AssetDatabase.Refresh();
        }
    }
}
