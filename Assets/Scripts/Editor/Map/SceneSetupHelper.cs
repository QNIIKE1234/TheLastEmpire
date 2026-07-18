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

            // 1. Create WorldMapGenerator Asset if it doesn't exist
            string settingsDir = "Assets/Settings";
            if (!System.IO.Directory.Exists(settingsDir))
            {
                System.IO.Directory.CreateDirectory(settingsDir);
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

            // 2. Create WorldMapManager
            GameObject managerObj = new GameObject("WorldMapManager");
            WorldMapManager manager = managerObj.AddComponent<WorldMapManager>();
            manager.MapGenerator = generator;

            // 3. Create a dynamic 16x16 white sprite texture for placeholders
            Texture2D whiteTexture = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    whiteTexture.SetPixel(x, y, Color.white);
                }
            }
            whiteTexture.Apply();

            // Save the texture to an asset so we can reference it in Editor
            string texturePath = "Assets/Settings/PlaceholderWhite.png";
            byte[] bytes = whiteTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(texturePath, bytes);
            AssetDatabase.ImportAsset(texturePath);
            Sprite placeholderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);

            // 4. Create Background (LocalStageVisualizer)
            GameObject bgObj = new GameObject("BackgroundVisualizer");
            bgObj.transform.position = Vector3.zero;
            bgObj.transform.localScale = new Vector3(20f, 12f, 1f); // Cover orthographic camera viewport
            
            SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = placeholderSprite;
            bgRenderer.color = Color.gray;
            bgRenderer.sortingOrder = -10; // Draw in background

            LocalStageVisualizer stageVisualizer = bgObj.AddComponent<LocalStageVisualizer>();
            stageVisualizer.BackgroundRenderer = bgRenderer;

            // 5. Create UI Text overlay
            GameObject textObj = new GameObject("StageInfoText");
            textObj.transform.position = new Vector3(-8.5f, 4.5f, 0f);
            
            TMPro.TextMeshPro textMesh = textObj.AddComponent<TMPro.TextMeshPro>();
            textMesh.fontSize = 6;
            textMesh.color = Color.yellow;
            textMesh.text = "Loading Stage...";
            
            stageVisualizer.InfoText = textMesh;

            // 6. Create Player
            GameObject playerObj = new GameObject("Player");
            playerObj.transform.position = Vector3.zero;

            SpriteRenderer playerRenderer = playerObj.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = placeholderSprite;
            playerRenderer.color = Color.red; // Red square player
            playerRenderer.sortingOrder = 5;  // Draw on top of background

            // Add Collider & Rigidbody
            BoxCollider2D playerCollider = playerObj.AddComponent<BoxCollider2D>();
            playerCollider.size = Vector2.one;

            Rigidbody2D playerRb = playerObj.GetComponent<Rigidbody2D>();
            playerRb.bodyType = RigidbodyType2D.Dynamic;
            playerRb.gravityScale = 0f;
            playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;

            playerObj.AddComponent<PlayerController>();

            // 7. Configure Camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = 5f;
                mainCamera.transform.position = new Vector3(0, 0, -10);
            }

            // Save Scene
            string sceneDir = "Assets/Scenes";
            if (!System.IO.Directory.Exists(sceneDir))
            {
                System.IO.Directory.CreateDirectory(sceneDir);
            }
            
            string scenePath = "Assets/Scenes/MapTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log("Successfully created and saved MapTestScene at: " + scenePath);

            Selection.activeGameObject = playerObj;
            AssetDatabase.Refresh();
        }
    }
}
