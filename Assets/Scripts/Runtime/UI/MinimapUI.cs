using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    public class MinimapUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RawImage minimapImage;

        [Header("Settings")]
        [SerializeField] private Color fogColor = new Color(0.08f, 0.08f, 0.08f, 1f); // Dark gray fog
        [SerializeField] private Color playerIndicatorColor = Color.red;

        // Zoom size presets: 8x8 (very close), 16x16 (medium), 32x32 (far), 64x64 (world overview)
        private readonly int[] _zoomPresets = { 8, 16, 32, 64 };
        private int _currentZoomIndex = 0; // Starts at 8x8

        private Texture2D _minimapTexture;
        private Color[] _pixelColors;

        public RawImage MinimapImage
        {
            get { return minimapImage; }
            set { minimapImage = value; }
        }

        private void Start()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += UpdateMinimap;
                SetZoomIndex(0); // Initialize with first zoom preset
            }
            else
            {
                Debug.LogWarning("MinimapUI: WorldMapManager.Instance is null at startup!");
            }
        }

        private void Update()
        {
            // Keyboard shortcuts using the New Input System direct API
            if (Keyboard.current != null)
            {
                // Plus key or Equals key to Zoom In
                if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame)
                {
                    ZoomIn();
                }
                // Minus key to Zoom Out
                if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
                {
                    ZoomOut();
                }
            }
        }

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= UpdateMinimap;
            }

            if (_minimapTexture != null)
            {
                Destroy(_minimapTexture);
            }
        }

        public void ZoomIn()
        {
            SetZoomIndex(_currentZoomIndex - 1);
        }

        public void ZoomOut()
        {
            SetZoomIndex(_currentZoomIndex + 1);
        }

        public void SetZoomIndex(int index)
        {
            _currentZoomIndex = Mathf.Clamp(index, 0, _zoomPresets.Length - 1);
            int currentSize = _zoomPresets[_currentZoomIndex];

            // Re-allocate texture to match current zoom scale size
            if (_minimapTexture != null)
            {
                Destroy(_minimapTexture);
            }

            _minimapTexture = new Texture2D(currentSize, currentSize);
            _minimapTexture.filterMode = FilterMode.Point;
            _minimapTexture.wrapMode = TextureWrapMode.Clamp;
            _pixelColors = new Color[currentSize * currentSize];

            if (minimapImage != null)
            {
                minimapImage.texture = _minimapTexture;
            }

            // Trigger redraw using current player coordinates
            if (WorldMapManager.Instance != null)
            {
                UpdateMinimap(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
        }

        private void UpdateMinimap(int playerX, int playerY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null || _minimapTexture == null) return;

            WorldMapGenerator generator = WorldMapManager.Instance.MapGenerator;
            int currentSize = _zoomPresets[_currentZoomIndex];
            int halfSize = currentSize / 2;

            for (int localY = 0; localY < currentSize; localY++)
            {
                for (int localX = 0; localX < currentSize; localX++)
                {
                    int index = localX + localY * currentSize;
                    
                    // Center the player on the current view size viewport
                    int worldX = playerX - halfSize + localX;
                    int worldY = playerY - halfSize + localY;

                    if (worldX >= 0 && worldX < WorldMapGenerator.GridSize && worldY >= 0 && worldY < WorldMapGenerator.GridSize)
                    {
                        StageData stage = generator.GetStage(worldX, worldY);
                        if (stage == null)
                        {
                            _pixelColors[index] = fogColor;
                            continue;
                        }

                        // Draw player red dot in the center coordinate
                        if (worldX == playerX && worldY == playerY)
                        {
                            _pixelColors[index] = playerIndicatorColor;
                        }
                        // Show biome color if stage has been explored
                        else if (stage.isExplored)
                        {
                            _pixelColors[index] = WorldMapGenerator.GetBiomeColor(stage.biome);
                        }
                        // Keep locked behind Fog of War
                        else
                        {
                            _pixelColors[index] = fogColor;
                        }
                    }
                    else
                    {
                        // Out of overworld map boundaries is painted as fog
                        _pixelColors[index] = fogColor;
                    }
                }
            }

            _minimapTexture.SetPixels(_pixelColors);
            _minimapTexture.Apply();
        }
    }
}
