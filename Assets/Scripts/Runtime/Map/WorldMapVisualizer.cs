using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorldMapVisualizer : MonoBehaviour
    {
        [SerializeField] private WorldMapGenerator mapGenerator;
        [SerializeField] private float pixelsPerUnit = 10f;

        private SpriteRenderer _spriteRenderer;

        public WorldMapGenerator MapGenerator
        {
            get { return mapGenerator; }
            set { mapGenerator = value; }
        }

        public float PixelsPerUnit
        {
            get { return pixelsPerUnit; }
            set { pixelsPerUnit = value; }
        }

        private void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (mapGenerator == null)
            {
                Debug.LogError("WorldMapVisualizer: Please assign a WorldMapGenerator ScriptableObject reference.");
                return;
            }

            mapGenerator.GenerateMap();
            Texture2D texture = mapGenerator.GeneratePreviewTexture();

            Sprite mapSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );

            _spriteRenderer.sprite = mapSprite;
            Debug.Log("WorldMapVisualizer: Successfully rendered the 64x64 Grid World Map.");
        }
    }
}
