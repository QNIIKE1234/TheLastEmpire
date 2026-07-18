using UnityEngine;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "WorldMapGenerator", menuName = "The Last Empire/World Map Generator", order = 1)]
    public class WorldMapGenerator : ScriptableObject
    {
        public const int GridSize = 128;

        [Header("Generation Settings")]
        public int seed = 1337;
        public float noiseScale = 20f;
        [Range(1, 6)] public int noiseOctaves = 3;
        [Range(0f, 1f)] public float persistence = 0.5f;
        public float lacunarity = 2.0f;
        public Vector2 noiseOffset = Vector2.zero;

        [Header("Biome Thresholds (Base Map)")]
        [Range(0f, 1f)] public float waterThreshold = 0.12f;
        [Range(0f, 1f)] public float forestThreshold = 0.32f;
        [Range(0f, 1f)] public float suburbanThreshold = 0.45f;
        [Range(0f, 1f)] public float urbanThreshold = 0.85f;

        [Header("Highway Winding Settings")]
        public float highwayScale = 12f;
        [Range(0.01f, 0.1f)] public float highwayWidth = 0.035f;

        [Header("Event Settings")]
        [Range(0f, 0.1f)] public float eventProbability = 0.015f;

        [Header("Flooded City Settings")]
        [Range(0f, 0.3f)] public float floodedCityChance = 0.08f;

        [HideInInspector]
        public StageData[] gridData;

        [Header("Starting Location (Generated)")]
        public int spawnX;
        public int spawnY;

        public void GenerateMap()
        {
            gridData = new StageData[GridSize * GridSize];
            System.Random rand = new System.Random(seed);

            float offsetX = noiseOffset.x + (float)(rand.NextDouble() * 200000 - 100000);
            float offsetY = noiseOffset.y + (float)(rand.NextDouble() * 200000 - 100000);

            // Generate separate offsets for winding highways
            float roadOffsetX = noiseOffset.x + (float)(rand.NextDouble() * 200000 - 100000);
            float roadOffsetY = noiseOffset.y + (float)(rand.NextDouble() * 200000 - 100000);

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    float noiseValue = GetOctaveNoise(x, y, offsetX, offsetY);
                    BiomeType biome = GetBiomeFromNoise(noiseValue);

                    // Roll for Flooded City (Urban Ruins in the middle of Waterways)
                    if (biome == BiomeType.Waterways && rand.NextDouble() < floodedCityChance)
                    {
                        biome = BiomeType.UrbanRuins;
                    }

                    // Overlay winding highways if the base biome is not Waterways
                    if (biome != BiomeType.Waterways)
                    {
                        float roadNoise = GetOctaveNoise(x, y, roadOffsetX, roadOffsetY, highwayScale, noiseOctaves, persistence, lacunarity);
                        if (Mathf.Abs(roadNoise - 0.5f) < highwayWidth)
                        {
                            biome = BiomeType.Highways;
                        }
                    }

                    // Sprinkle special events randomly on walkable ground
                    if (biome != BiomeType.Waterways && rand.NextDouble() < eventProbability)
                    {
                        biome = BiomeType.SpecialEvent;
                    }

                    int stageSeed = rand.Next();
                    gridData[x + y * GridSize] = new StageData(x, y, biome, stageSeed);
                }
            }

            CalculateSpawnLocation(seed);
        }

        private void CalculateSpawnLocation(int seed)
        {
            System.Random rand = new System.Random(seed);
            int attempts = 0;
            do
            {
                spawnX = rand.Next(0, GridSize);
                spawnY = rand.Next(0, GridSize);
                attempts++;

                StageData stage = GetStage(spawnX, spawnY);
                if (stage != null && stage.biome != BiomeType.Waterways && stage.biome != BiomeType.SpecialEvent)
                {
                    // Check neighbors to make sure we are not on a tiny island/flooded city surrounded by water
                    int waterNeighbors = 0;
                    int[] dx = { 0, 0, 1, -1 };
                    int[] dy = { 1, -1, 0, 0 };
                    for (int i = 0; i < 4; i++)
                    {
                        StageData neighbor = GetStage(spawnX + dx[i], spawnY + dy[i]);
                        if (neighbor != null && neighbor.biome == BiomeType.Waterways)
                        {
                            waterNeighbors++;
                        }
                    }

                    // Only spawn if at most 1 neighbor is water (ensures it is connected to main land mass)
                    if (waterNeighbors <= 1)
                    {
                        break;
                    }
                }
            } 
            while (attempts < 100);
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
            return GetOctaveNoise(x, y, offsetX, offsetY, noiseScale, noiseOctaves, persistence, lacunarity);
        }

        private float GetOctaveNoise(int x, int y, float offsetX, float offsetY, float scale, int octaves, float pers, float lacun)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;
            float maxPossibleHeight = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (x + offsetX) / scale * frequency;
                float sampleY = (y + offsetY) / scale * frequency;

                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                noiseHeight += perlinValue * amplitude;

                maxPossibleHeight += amplitude;
                amplitude *= pers;
                frequency *= lacun;
            }

            return Mathf.Clamp01(noiseHeight / maxPossibleHeight);
        }

        private BiomeType GetBiomeFromNoise(float val)
        {
            if (val < waterThreshold) return BiomeType.Waterways;
            if (val < forestThreshold) return BiomeType.OvergrownForests;
            if (val < suburbanThreshold) return BiomeType.SuburbanVillages;
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
                case BiomeType.Waterways: return new Color(0.12f, 0.56f, 1f);
                case BiomeType.OvergrownForests: return new Color(0.13f, 0.55f, 0.13f);
                case BiomeType.SuburbanVillages: return new Color(0.6f, 0.8f, 0.2f);
                case BiomeType.Highways: return Color.white;
                case BiomeType.UrbanRuins: return Color.black;
                case BiomeType.Highlands: return new Color(0.55f, 0.27f, 0.07f);
                case BiomeType.SpecialEvent: return new Color(0.6f, 0.1f, 0.8f); // Vibrant purple
                default: return Color.black;
            }
        }
    }
}
