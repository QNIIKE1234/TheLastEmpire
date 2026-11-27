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

            // Pure 32-bit integer hash offsets (bit-exact across Intel, AMD, ARM, Mono & IL2CPP — zero float/FMA drift)
            int offsetX = Mathf.RoundToInt(noiseOffset.x) + (int)(Hash32((uint)seed, 101u, 202u) % 200000u) - 100000;
            int offsetY = Mathf.RoundToInt(noiseOffset.y) + (int)(Hash32((uint)seed, 303u, 404u) % 200000u) - 100000;

            int roadOffsetX = Mathf.RoundToInt(noiseOffset.x) + (int)(Hash32((uint)seed, 505u, 606u) % 200000u) - 100000;
            int roadOffsetY = Mathf.RoundToInt(noiseOffset.y) + (int)(Hash32((uint)seed, 707u, 808u) % 200000u) - 100000;

            uint floodedThreshold = (uint)Mathf.Clamp(Mathf.RoundToInt(floodedCityChance * 1000000f), 0, 1000000);
            uint eventRollThreshold = (uint)Mathf.Clamp(Mathf.RoundToInt(eventProbability * 1000000f), 0, 1000000);
            int highwayHalfWidthFp = Mathf.RoundToInt(highwayWidth * 65536f);

            int waterThresholdFp = Mathf.RoundToInt(waterThreshold * 65536f);
            int forestThresholdFp = Mathf.RoundToInt(forestThreshold * 65536f);
            int suburbanThresholdFp = Mathf.RoundToInt(suburbanThreshold * 65536f);
            int urbanThresholdFp = Mathf.RoundToInt(urbanThreshold * 65536f);

            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int noiseFp = GetDeterministicOctaveNoiseFp(x, y, offsetX, offsetY, noiseScale, noiseOctaves, persistence, lacunarity);
                    BiomeType biome = noiseFp < waterThresholdFp ? BiomeType.Waterways
                                    : noiseFp < forestThresholdFp ? BiomeType.OvergrownForests
                                    : noiseFp < suburbanThresholdFp ? BiomeType.SuburbanVillages
                                    : noiseFp < urbanThresholdFp ? BiomeType.UrbanRuins
                                    : BiomeType.Highlands;

                    // Independent per-tile integer hash rolls (never shift neighbor tiles' RNG state!)
                    uint floodedRoll = Hash32((uint)seed ^ 0x1A2B3C4Du, (uint)x, (uint)y) % 1000000u;
                    if (biome == BiomeType.Waterways && floodedRoll < floodedThreshold)
                    {
                        biome = BiomeType.UrbanRuins;
                    }

                    if (biome != BiomeType.Waterways)
                    {
                        int roadNoiseFp = GetDeterministicOctaveNoiseFp(x, y, roadOffsetX, roadOffsetY, highwayScale, noiseOctaves, persistence, lacunarity);
                        if (System.Math.Abs(roadNoiseFp - 32768) < highwayHalfWidthFp)
                        {
                            biome = BiomeType.Highways;
                        }
                    }

                    uint eventRoll = Hash32((uint)seed ^ 0x5E6F7A8Bu, (uint)x, (uint)y) % 1000000u;
                    if (biome != BiomeType.Waterways && eventRoll < eventRollThreshold)
                    {
                        biome = BiomeType.SpecialEvent;
                    }

                    // Independent per-tile bit-exact stageSeed (masked to positive 31-bit int)
                    int stageSeed = (int)(Hash32((uint)seed ^ 0x9E3779B9u, (uint)x, (uint)y) & 0x7FFFFFFFu);
                    StageData stage = new StageData(x, y, biome, stageSeed);
                    if (biome == BiomeType.SpecialEvent)
                    {
                        stage.isShopStage = (Hash32((uint)seed ^ 0xC2B2AE35u, (uint)x, (uint)y) & 1u) == 0u;
                    }
                    gridData[x + y * GridSize] = stage;
                }
            }

            CalculateSpawnLocation(seed);
        }

        /// <summary>
        /// Bit-exact 32-bit integer hash (Murmur3/SplitMix finalizer) — identical on Intel, AMD, and ARM CPUs.
        /// </summary>
        public static uint Hash32(uint a, uint b, uint c)
        {
            uint h = a ^ 0x9E3779B9u;
            h = (h ^ b) * 0x85EBCA6Bu;
            h = (h ^ (h >> 13)) ^ c;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        /// <summary>
        /// Pure 64-bit integer Q16.16 fixed-point fractal value noise (returns [0..65536]).
        /// Eliminates cross-CPU float/FMA rounding drift from Mathf.PerlinNoise.
        /// </summary>
        private static int GetDeterministicOctaveNoiseFp(int x, int y, int offsetX, int offsetY, float scale, int octaves, float pers, float lacun)
        {
            const long ONE = 65536L; // Q16.16 fixed-point 1.0
            long scaleFp = System.Math.Max(655L, (long)Mathf.RoundToInt(scale * 65536f));
            long persFp = (long)Mathf.Clamp(Mathf.RoundToInt(pers * 65536f), 0, 65536);
            long lacunFp = System.Math.Max(ONE, (long)Mathf.RoundToInt(lacun * 65536f));

            long amplitude = ONE;
            long frequency = ONE;
            long total = 0;
            long maxTotal = 0;

            long baseX = ((long)(x + offsetX) << 16);
            long baseY = ((long)(y + offsetY) << 16);

            for (int i = 0; i < octaves; i++)
            {
                long sx = (baseX * frequency) / scaleFp;
                long sy = (baseY * frequency) / scaleFp;

                long sample = DeterministicValueNoise2DFp(sx, sy, (uint)(i * 7919 + 17));
                total += (sample * amplitude) >> 16;
                maxTotal += amplitude;

                amplitude = (amplitude * persFp) >> 16;
                frequency = (frequency * lacunFp) >> 16;
            }

            if (maxTotal <= 0) return 32768;
            long normalized = (total << 16) / maxTotal;
            if (normalized < 0) normalized = 0;
            if (normalized > ONE) normalized = ONE;
            return (int)normalized;
        }

        private static long DeterministicValueNoise2DFp(long xFp, long yFp, uint octaveSalt)
        {
            const long ONE = 65536L;
            int ix = (int)(xFp >> 16);
            int iy = (int)(yFp >> 16);
            long fx = xFp & 0xFFFFL;
            long fy = yFp & 0xFFFFL;

            // Fixed-point cubic smoothstep: f*f*(3 - 2*f) in Q16.16
            long fx2 = (fx * fx) >> 16;
            long ux = (fx2 * ((3L * ONE) - (2L * fx))) >> 16;
            long fy2 = (fy * fy) >> 16;
            long uy = (fy2 * ((3L * ONE) - (2L * fy))) >> 16;

            long n00 = Hash32(octaveSalt, (uint)ix, (uint)iy) & 0xFFFFu;
            long n10 = Hash32(octaveSalt, (uint)(ix + 1), (uint)iy) & 0xFFFFu;
            long n01 = Hash32(octaveSalt, (uint)ix, (uint)(iy + 1)) & 0xFFFFu;
            long n11 = Hash32(octaveSalt, (uint)(ix + 1), (uint)(iy + 1)) & 0xFFFFu;

            long nx0 = n00 + (((n10 - n00) * ux) >> 16);
            long nx1 = n01 + (((n11 - n01) * ux) >> 16);
            return nx0 + (((nx1 - nx0) * uy) >> 16);
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
