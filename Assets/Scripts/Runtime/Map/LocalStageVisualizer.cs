using UnityEngine;

namespace TheLastEmpire
{
    public class LocalStageVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private TextMesh infoText;

        public SpriteRenderer BackgroundRenderer
        {
            get { return backgroundRenderer; }
            set { backgroundRenderer = value; }
        }

        public TextMesh InfoText
        {
            get { return infoText; }
            set { infoText = value; }
        }

        private void Start()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += HandleStageChanged;
                // Initialize display with current coordinates
                HandleStageChanged(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
        }

        private void OnDestroy()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleStageChanged;
            }
        }

        private void HandleStageChanged(int gridX, int gridY)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(gridX, gridY);
            if (stage == null) return;

            // Update background color based on biome
            if (backgroundRenderer != null)
            {
                backgroundRenderer.color = WorldMapGenerator.GetBiomeColor(stage.biome);
            }

            // Update on-screen info text
            if (infoText != null)
            {
                infoText.text = $"Coordinates: ({stage.x}, {stage.y})\n" +
                                $"Biome: {stage.biome}\n" +
                                $"Seed: {stage.stageSeed}\n" +
                                $"Explored: {stage.isExplored} | Cleared: {stage.isCleared}";
            }
        }
    }
}
