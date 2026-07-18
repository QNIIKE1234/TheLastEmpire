using UnityEngine;

namespace TheLastEmpire.Runtime.Map
{
    [CreateAssetMenu(fileName = "WorldMapGenerator", menuName = "The Last Empire/World Map Generator", order = 1)]
    public class WorldMapGenerator : ScriptableObject
    {
        public const int GridSize = 64;

        [Header("Generation Settings")]
        public int seed = 1337;
        public float noiseScale = 20f;
        [Range(1, 6)] public int noiseOctaves = 3;
        [Range(0f, 1f)] public float persistence = 0.5f;
        public float lacunarity = 2.0f;
        public Vector2 noiseOffset = Vector2.zero;

        [Header("Biome Thresholds")]
        [Range(0f, 1f)] public float waterThreshold = 0.15f;
        [Range(0f, 1f)] public float forestThreshold = 0.40f;
        [Range(0f, 1f)] public float suburbanThreshold = 0.55f;
        [Range(0f, 1f)] public float highwayThreshold = 0.68f;
        [Range(0f, 1f)] public float urbanThreshold = 0.85f;

        [HideInInspector]
        public StageData[] gridData;

        public void GenerateMap()
        {
            gridData = new StageData[GridSize * GridSize];
            System.Random rand = new System.Random(seed);

            // Generate offset based on seed or settings
            float offsetX = noiseOffset.x + (float)(rand.NextDouble() * 200000 - 100000);
            float offsetY = noiseOffset.y + (float)(rand.NextDouble() * 200000 - 100000);

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    float noiseValue = GetOctaveNoise(x, y, offsetX, offsetY);
                    BiomeType biome = GetBiomeFromNoise(noiseValue);
                    int stageSeed = rand.Next();

                    gridData[x + y * GridSize] = new StageData(x, y, biome, stageSeed);
                }
            }
        }

        public StageData GetStage(int x, int y)
        {
            if (gridData == null || gridData.Length != GridSize * GridSize)
            {
                return null;
            }

            if (x >= 0 && x < GridSize && y >= 0 && y < GridSize)
            {
                return gridData[x + y * GridSize];
            }
            return null;
        }

        private float GetOctaveNoise(int x, int y, float offsetX, float offsetY)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;
            float maxPossibleHeight = 0f;

            for (int i = 0; i < noiseOctaves; i++)
            {
                float sampleX = (x + offsetX) / noiseScale * frequency;
                float sampleY = (y + offsetY) / noiseScale * frequency;

                // Unity's Mathf.PerlinNoise returns 0.0 to 1.0
                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                noiseHeight += perlinValue * amplitude;

                maxPossibleHeight += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return Mathf.Clamp01(noiseHeight / maxPossibleHeight);
        }

        private BiomeType GetBiomeFromNoise(float val)
        {
            if (val < waterThreshold) return BiomeType.Waterways;
            if (val < forestThreshold) return BiomeType.OvergrownForests;
            if (val < suburbanThreshold) return BiomeType.SuburbanVillages;
            if (val < highwayThreshold) return BiomeType.Highways;
            if (val < urbanThreshold) return BiomeType.UrbanRuins;
            return BiomeType.Highlands;
        }

        public Texture2D GeneratePreviewTexture()
        {
            if (gridData == null || gridData.Length != GridSize * GridSize)
            {
                GenerateMap();
            }

            Texture2D texture = new Texture2D(GridSize, GridSize);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] colorMap = new Color[GridSize * GridSize];
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    StageData stage = gridData[x + y * GridSize];
                    colorMap[x + y * GridSize] = GetBiomeColor(stage.biome);
                }
            }

            texture.SetPixels(colorMap);
            texture.Apply();
            return texture;
        }

        public static Color GetBiomeColor(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Waterways: return new Color(0.12f, 0.56f, 1f); // #1E90FF (DodgeBlue)
                case BiomeType.OvergrownForests: return new Color(0.13f, 0.55f, 0.13f); // #228B22 (ForestGreen)
                case BiomeType.SuburbanVillages: return new Color(0.6f, 0.8f, 0.2f); // #9ACD32 (YellowGreen)
                case BiomeType.Highways: return new Color(0.18f, 0.31f, 0.31f); // #2F4F4F (DarkSlateGray)
                case BiomeType.UrbanRuins: return new Color(0.44f, 0.5f, 0.56f); // #708090 (SlateGray)
                case BiomeType.Highlands: return new Color(0.55f, 0.27f, 0.07f); // #8B4513 (SaddleBrown)
                default: return Color.black;
            }
        }
    }
}
