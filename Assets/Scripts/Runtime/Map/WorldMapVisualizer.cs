using UnityEngine;

namespace TheLastEmpire.Runtime.Map
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorldMapVisualizer : MonoBehaviour
    {
        [SerializeField] private TheLastEmpire.Runtime.Map.WorldMapGenerator mapGenerator;
        [SerializeField] private float pixelsPerUnit = 10f;

        private SpriteRenderer _spriteRenderer;

        public TheLastEmpire.Runtime.Map.WorldMapGenerator MapGenerator
        {
            get => mapGenerator;
            set => mapGenerator = value;
        }

        public float PixelsPerUnit
        {
            get => pixelsPerUnit;
            set => pixelsPerUnit = value;
        }

        private void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (mapGenerator == null)
            {
                Debug.LogError("WorldMapVisualizer: Please assign a WorldMapGenerator ScriptableObject reference.");
                return;
            }

            // Generate map data at runtime
            mapGenerator.GenerateMap();

            // Create a preview texture based on generated grid data
            Texture2D texture = mapGenerator.GeneratePreviewTexture();

            // Instantiate a new Sprite using the texture
            Sprite mapSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );

            _spriteRenderer.sprite = mapSprite;
            
            Debug.Log("WorldMapVisualizer: Successfully rendered the 64x64 Grid World Map at runtime.");
        }
    }
}
