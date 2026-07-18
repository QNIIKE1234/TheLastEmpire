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

            // 8. Create Canvas and Minimap UI
            GameObject canvasObj = new GameObject("HUDCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create EventSystem if missing
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Create Minimap UI Container anchored to Top-Right
            GameObject minimapObj = new GameObject("MinimapUI");
            minimapObj.transform.SetParent(canvasObj.transform, false);

            RectTransform minimapRect = minimapObj.AddComponent<RectTransform>();
            minimapRect.anchorMin = new Vector2(1, 1);
            minimapRect.anchorMax = new Vector2(1, 1);
            minimapRect.pivot = new Vector2(1, 1);
            minimapRect.anchoredPosition = new Vector2(-20f, -20f); // 20px padding from top-right
            minimapRect.sizeDelta = new Vector2(180f, 180f); // 180x180 pixels

            // Add background border to the minimap
            UnityEngine.UI.RawImage borderImage = minimapObj.AddComponent<UnityEngine.UI.RawImage>();
            borderImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f); // Semi-transparent dark border

            // Create inner raw image for actual map texture
            GameObject mapTextureObj = new GameObject("MapTexture");
            mapTextureObj.transform.SetParent(minimapObj.transform, false);

            RectTransform mapTextureRect = mapTextureObj.AddComponent<RectTransform>();
            mapTextureRect.anchorMin = Vector2.zero;
            mapTextureRect.anchorMax = Vector2.one;
            mapTextureRect.sizeDelta = new Vector2(-10f, -10f); // 5px padding all around
            mapTextureRect.anchoredPosition = Vector2.zero;

            UnityEngine.UI.RawImage rawImage = mapTextureObj.AddComponent<UnityEngine.UI.RawImage>();
            
            // Add MinimapUI script and wire up the RawImage reference
            MinimapUI minimapUI = mapTextureObj.AddComponent<MinimapUI>();
            minimapUI.MinimapImage = rawImage;

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
