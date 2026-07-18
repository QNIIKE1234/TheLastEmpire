using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace TheLastEmpire
{
    public static class SceneSetupHelper
    {
        [MenuItem("The Last Empire/Setup Map Test Scene")]
        public static void SetupMapTestScene()
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject visualizerObj = new GameObject("WorldMapVisualizer");
            WorldMapVisualizer visualizer = visualizerObj.AddComponent<WorldMapVisualizer>();
            
            SpriteRenderer spriteRenderer = visualizerObj.GetComponent<SpriteRenderer>();
            spriteRenderer.drawMode = SpriteDrawMode.Simple;

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

            visualizer.MapGenerator = generator;
            visualizer.PixelsPerUnit = 10f;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = 5f;
                mainCamera.transform.position = new Vector3(0, 0, -10);
            }

            EditorUtility.SetDirty(visualizerObj);

            string sceneDir = "Assets/Scenes";
            if (!System.IO.Directory.Exists(sceneDir))
            {
                System.IO.Directory.CreateDirectory(sceneDir);
            }
            
            string scenePath = "Assets/Scenes/MapTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log("Successfully created and saved MapTestScene at: " + scenePath);

            Selection.activeGameObject = visualizerObj;
            AssetDatabase.Refresh();
        }
    }
}
