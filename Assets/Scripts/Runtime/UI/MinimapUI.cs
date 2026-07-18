using UnityEngine;
using UnityEngine.UI;

namespace TheLastEmpire
{
    public class MinimapUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RawImage minimapImage;

        [Header("Settings")]
        [SerializeField] private Color fogColor = new Color(0.08f, 0.08f, 0.08f, 1f); // Dark gray fog
        [SerializeField] private Color playerIndicatorColor = Color.red;

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
                InitTexture();
                // Perform initial draw
                UpdateMinimap(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
            else
            {
                Debug.LogWarning("MinimapUI: WorldMapManager.Instance is null at startup!");
            }
        }

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= UpdateMinimap;
            }
        }

        private void InitTexture()
        {
            _minimapTexture = new Texture2D(WorldMapGenerator.GridSize, WorldMapGenerator.GridSize);
            _minimapTexture.filterMode = FilterMode.Point;
            _minimapTexture.wrapMode = TextureWrapMode.Clamp;
            _pixelColors = new Color[WorldMapGenerator.GridSize * WorldMapGenerator.GridSize];

            if (minimapImage != null)
            {
                minimapImage.texture = _minimapTexture;
            }
        }

        private void UpdateMinimap(int playerX, int playerY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null || _minimapTexture == null) return;

            WorldMapGenerator generator = WorldMapManager.Instance.MapGenerator;

            for (int y = 0; y < WorldMapGenerator.GridSize; y++)
            {
                for (int x = 0; x < WorldMapGenerator.GridSize; x++)
                {
                    int index = x + y * WorldMapGenerator.GridSize;
                    StageData stage = generator.GetStage(x, y);

                    if (stage == null)
                      {
                          _pixelColors[index] = fogColor;
                          continue;
                      }

                      // Draw player red dot
                      if (x == playerX && y == playerY)
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
            }

            _minimapTexture.SetPixels(_pixelColors);
            _minimapTexture.Apply();
        }
    }
}
