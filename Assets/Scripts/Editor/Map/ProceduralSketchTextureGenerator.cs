using UnityEditor;
using UnityEngine;
using System.IO;

namespace TheLastEmpire
{
    public static class ProceduralSketchTextureGenerator
    {
        [MenuItem("The Last Empire/Generate Procedural Don't Starve Textures")]
        public static void GenerateTextures()
        {
            string destDir = "Assets/Textures/Biomes";
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            int size = 512; // 512x512 offers high crispness for board-game tile details

            // Generate Zombicide board-game tile textures for each biome
            CreateBiomeTexture($"{destDir}/UrbanRuins.png", size, BiomeType.UrbanRuins);
            CreateBiomeTexture($"{destDir}/Highways.png", size, BiomeType.Highways);
            CreateBiomeTexture($"{destDir}/OvergrownForests.png", size, BiomeType.OvergrownForests);
            CreateBiomeTexture($"{destDir}/Highlands.png", size, BiomeType.Highlands);
            CreateBiomeTexture($"{destDir}/Waterways.png", size, BiomeType.Waterways);
            CreateBiomeTexture($"{destDir}/SuburbanVillages.png", size, BiomeType.SuburbanVillages);
            CreateBiomeTexture($"{destDir}/SpecialEvent.png", size, BiomeType.SpecialEvent);
            CreateBiomeTexture($"{destDir}/Default.png", size, BiomeType.SpecialEvent); // Use SpecialEvent style as default

            AssetDatabase.Refresh();
            Debug.Log("ProceduralSketchTextureGenerator: Successfully generated and imported all Zombicide Board Game style textures!");
        }

        private static void CreateBiomeTexture(string path, int size, BiomeType biome)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color sketchColor = new Color(0.12f, 0.12f, 0.15f, 0.12f); // Sketch pencil shadow lines
            Color outlineColor = new Color(0.08f, 0.08f, 0.1f, 0.45f);  // Sharp board-game pen borders
            
            // 1. Initial base texture generation with printed cardboard paper texture
            Random.State oldState = Random.state;
            Random.InitState(54321 + (int)biome); // Deterministic seed per biome

            Color baseColor = GetBaseColor(biome);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Cardboard grime and noise
                    float noise1 = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.08f;
                    float noise2 = Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 0.04f;
                    float noise = noise1 + noise2 - 0.06f;

                    Color c = baseColor;
                    c.r = Mathf.Clamp01(c.r + noise);
                    c.g = Mathf.Clamp01(c.g + noise);
                    c.b = Mathf.Clamp01(c.b + noise);
                    tex.SetPixel(x, y, c);
                }
            }

            // 2. Draw cardboard grid borders (Zombicide board tiles have distinct edges)
            Color gridBorderColor = new Color(0.05f, 0.05f, 0.05f, 0.5f);
            DrawLine(tex, 0, 0, size, 0, gridBorderColor);
            DrawLine(tex, 0, 0, 0, size, gridBorderColor);
            DrawLine(tex, size - 1, 0, size - 1, size, gridBorderColor);
            DrawLine(tex, 0, size - 1, size, size - 1, gridBorderColor);

            // 3. Draw Zombicide Board Game tile structures depending on biome
            switch (biome)
            {
                case BiomeType.UrbanRuins:
                    // VERTICAL STREET TILE: Left/Right sidewalks, center asphalt road
                    Color sidewalkColor = new Color(0.35f, 0.35f, 0.38f);
                    Color roadLineColor = new Color(0.85f, 0.85f, 0.85f, 0.65f); // White road lines
                    
                    // Draw sidewalks
                    int sidewalkWidth = 96;
                    FillRect(tex, 0, 0, sidewalkWidth, size, sidewalkColor);
                    FillRect(tex, size - sidewalkWidth, 0, sidewalkWidth, size, sidewalkColor);

                    // Draw sidewalk borders (curbs)
                    DrawLine(tex, sidewalkWidth, 0, sidewalkWidth, size, outlineColor);
                    DrawLine(tex, size - sidewalkWidth, 0, size - sidewalkWidth, size, outlineColor);

                    // Draw sidewalk concrete panel dividers
                    for (int y = 0; y < size; y += 64)
                    {
                        DrawLine(tex, 0, y, sidewalkWidth, y, outlineColor);
                        DrawLine(tex, size - sidewalkWidth, y, size, y, outlineColor);
                    }

                    // Draw a manhole cover in the center of the asphalt street
                    int mx = size / 2;
                    int my = size / 2;
                    DrawCircle(tex, mx, my, 24, outlineColor);
                    DrawCircle(tex, mx, my, 21, outlineColor);
                    for (int o = -18; o <= 18; o += 6)
                    {
                        DrawLine(tex, mx + o, my - 12, mx + o, my + 12, outlineColor);
                        DrawLine(tex, mx - 12, my + o, mx + 12, my + o, outlineColor);
                    }

                    // Draw a broken white zebra crosswalk
                    for (int y = 64; y < size - 64; y += 48)
                    {
                        if (Random.Range(0, 10) > 2)
                        {
                            FillRect(tex, sidewalkWidth + 20, y, 40, 24, roadLineColor);
                            FillRect(tex, size - sidewalkWidth - 60, y, 40, 24, roadLineColor);
                        }
                    }

                    // Draw random asphalt cracks and grit
                    for (int c = 0; c < 5; c++)
                    {
                        int cx = Random.Range(sidewalkWidth + 10, size - sidewalkWidth - 10);
                        int cy = Random.Range(20, size - 20);
                        DrawCrack(tex, cx, cy, Random.Range(25, 45), outlineColor);
                    }
                    break;

                case BiomeType.Highways:
                    // HORIZONTAL HIGHWAY TILE: Top/Bottom sidewalks, center highway
                    Color highwaySidewalkColor = new Color(0.32f, 0.32f, 0.35f);
                    Color yellowDividerColor = new Color(0.8f, 0.65f, 0.15f, 0.6f); // Yellow lane dashes
                    
                    int hSidewalkHeight = 80;
                    FillRect(tex, 0, 0, size, hSidewalkHeight, highwaySidewalkColor);
                    FillRect(tex, 0, size - hSidewalkHeight, size, hSidewalkHeight, highwaySidewalkColor);

                    // Draw sidewalk borders
                    DrawLine(tex, 0, hSidewalkHeight, size, hSidewalkHeight, outlineColor);
                    DrawLine(tex, 0, size - hSidewalkHeight, size, size - hSidewalkHeight, outlineColor);

                    // Draw horizontal concrete dividers
                    for (int x = 0; x < size; x += 64)
                    {
                        DrawLine(tex, x, 0, x, hSidewalkHeight, outlineColor);
                        DrawLine(tex, x, size - hSidewalkHeight, x, size, outlineColor);
                    }

                    // Yellow dashed center line
                    for (int x = 0; x < size; x += 64)
                    {
                        FillRect(tex, x + 8, size / 2 - 3, 32, 6, yellowDividerColor);
                    }

                    // Draw highway tire skid marks (skidding tire tracks!)
                    Color skidColor = new Color(0.05f, 0.05f, 0.08f, 0.45f);
                    int skidY1 = size / 2 - 40;
                    int skidY2 = size / 2 + 20;
                    for (int x = 32; x < size - 32; x++)
                    {
                        float wave1 = Mathf.Sin(x * 0.02f) * 8f;
                        float wave2 = Mathf.Cos(x * 0.015f) * 6f;
                        tex.SetPixel(x, skidY1 + (int)wave1, Color.Lerp(tex.GetPixel(x, skidY1 + (int)wave1), skidColor, skidColor.a));
                        tex.SetPixel(x, skidY2 + (int)wave2, Color.Lerp(tex.GetPixel(x, skidY2 + (int)wave2), skidColor, skidColor.a));
                    }
                    break;

                case BiomeType.OvergrownForests:
                    // FOREST PATH TILE: Green grass with a diagonal dirt path cut through
                    Color pathColor = new Color(0.28f, 0.22f, 0.16f); // Dark dirt path
                    Color forestOutlineColor = new Color(0.06f, 0.12f, 0.05f, 0.35f);

                    // Carve a curvy dirt path from Left-Center to Right-Center
                    for (int x = 0; x < size; x++)
                    {
                        float centerY = size / 2f + Mathf.Sin(x * 0.025f) * 64f;
                        int pathHalfWidth = 64 + (int)(Mathf.Cos(x * 0.01f) * 16f);
                        for (int y = (int)centerY - pathHalfWidth; y < (int)centerY + pathHalfWidth; y++)
                        {
                            if (y >= 0 && y < size)
                            {
                                // Blended dirt path texture
                                float blend = Mathf.Clamp01(1f - Mathf.Abs(y - centerY) / pathHalfWidth);
                                // Add Perlin noise to make dirt edge organic
                                float edgeNoise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.3f;
                                if (blend + edgeNoise > 0.4f)
                                {
                                    tex.SetPixel(x, y, Color.Lerp(tex.GetPixel(x, y), pathColor, 0.85f));
                                }
                            }
                        }
                    }

                    // Draw grass tuft board details along the path boundaries
                    for (int i = 0; i < 35; i++)
                    {
                        int gx = Random.Range(10, size - 10);
                        int gy = Random.Range(10, size - 10);
                        // Make sure we only draw grass on grass areas (not center path)
                        float centerY = size / 2f + Mathf.Sin(gx * 0.025f) * 64f;
                        if (Mathf.Abs(gy - centerY) > 80f)
                        {
                            DrawGrassTuft(tex, gx, gy, forestOutlineColor);
                            if (i % 5 == 0)
                            {
                                DrawCuteFlower(tex, gx + 6, gy + 4, new Color(0.55f, 0.5f, 0.4f, 0.45f));
                            }
                        }
                    }
                    break;

                case BiomeType.Highlands:
                    // RUGGED ROCKY TILE: Dusty sand with stone paving slabs at boundaries
                    Color stoneSlabColor = new Color(0.32f, 0.3f, 0.28f);
                    
                    // Draw outer border stone slabs
                    int borderMargin = 40;
                    // Top
                    FillRect(tex, 0, size - borderMargin, size, borderMargin, stoneSlabColor);
                    // Bottom
                    FillRect(tex, 0, 0, size, borderMargin, stoneSlabColor);
                    // Left
                    FillRect(tex, 0, 0, borderMargin, size, stoneSlabColor);
                    // Right
                    FillRect(tex, size - borderMargin, 0, borderMargin, size, stoneSlabColor);

                    // Draw borders & lines separating stone slabs
                    DrawRectOutline(tex, borderMargin, borderMargin, size - borderMargin * 2, size - borderMargin * 2, outlineColor);
                    for (int i = 0; i < size; i += 80)
                    {
                        DrawLine(tex, i, 0, i, borderMargin, outlineColor);
                        DrawLine(tex, i, size - borderMargin, i, size, outlineColor);
                        DrawLine(tex, 0, i, borderMargin, i, outlineColor);
                        DrawLine(tex, size - borderMargin, i, size, i, outlineColor);
                    }

                    // Draw stone cracks
                    for (int c = 0; c < 8; c++)
                    {
                        int side = Random.Range(0, 4);
                        int cx = Random.Range(10, size - 10);
                        int cy = Random.Range(10, size - 10);
                        if (side == 0) cy = Random.Range(0, borderMargin);
                        else if (side == 1) cy = Random.Range(size - borderMargin, size);
                        else if (side == 2) cx = Random.Range(0, borderMargin);
                        else cx = Random.Range(size - borderMargin, size);
                        DrawCrack(tex, cx, cy, Random.Range(15, 30), outlineColor);
                    }
                    break;

                case BiomeType.Waterways:
                    // DOCK CANAL TILE: Water center with concrete canal borders
                    Color concreteDockColor = new Color(0.3f, 0.3f, 0.33f);
                    Color waterBlue = new Color(0.12f, 0.16f, 0.22f); // Deep dark water
                    Color waterRippleColor = new Color(0.25f, 0.35f, 0.45f, 0.35f);

                    int dockWidth = 64;
                    // Left Dock
                    FillRect(tex, 0, 0, dockWidth, size, concreteDockColor);
                    DrawLine(tex, dockWidth, 0, dockWidth, size, outlineColor);
                    // Right Dock
                    FillRect(tex, size - dockWidth, 0, dockWidth, size, concreteDockColor);
                    DrawLine(tex, size - dockWidth, 0, size - dockWidth, size, outlineColor);

                    // Water in the middle
                    FillRect(tex, dockWidth, 0, size - dockWidth * 2, size, waterBlue);

                    // Draw docks vertical concrete board lines
                    for (int y = 0; y < size; y += 48)
                    {
                        DrawLine(tex, 0, y, dockWidth, y, outlineColor);
                        DrawLine(tex, size - dockWidth, y, size, y, outlineColor);
                    }

                    // Draw sketchy water ripples inside the canal
                    for (int w = 0; w < 16; w++)
                    {
                        int startY = w * (size / 16) + Random.Range(-12, 12);
                        // Draw ripples confined to the water canal
                        DrawWavyLineBounded(tex, startY, 40f, 8f, waterRippleColor, dockWidth + 10, size - dockWidth - 10);
                    }
                    break;

                case BiomeType.SuburbanVillages:
                    // COBBLESTONE BOARD TILE: Beautiful clean repeating brick paving
                    for (int y = 0; y < size + 32; y += 32)
                    {
                        for (int x = 0; x < size + 32; x += 48)
                        {
                            int shift = (y / 32) % 2 * 24;
                            DrawStoneArc(tex, x + shift, y, 20, outlineColor);
                        }
                    }
                    break;

                case BiomeType.SpecialEvent:
                    // BIOHAZARD HAZARD ZONE: Black and yellow biohazard warning borders!
                    Color hazardYellow = new Color(0.65f, 0.55f, 0.1f);
                    Color hazardBlack = new Color(0.08f, 0.08f, 0.1f);
                    Color toxicSludge = new Color(0.2f, 0.5f, 0.15f, 0.8f); // Eerie radioactive waste

                    // Draw a thick outer border
                    int hBorder = 48;
                    DrawRectOutline(tex, hBorder, hBorder, size - hBorder * 2, size - hBorder * 2, outlineColor);

                    // Draw diagonal yellow and black stripes along the borders
                    for (int i = -size; i < size * 2; i += 24)
                    {
                        // Draw diagonal stripe bands
                        // Keep them outside the center area
                        DrawDiagonalStripeBorder(tex, i, 16, hBorder, hazardYellow, hazardBlack);
                    }

                    // Draw a toxic waste puddle in the center
                    int poolCx = size / 2 + Random.Range(-20, 20);
                    int poolCy = size / 2 + Random.Range(-20, 20);
                    DrawBlob(tex, poolCx, poolCy, 48, toxicSludge);
                    
                    // Draw warning details/circles
                    DrawCircle(tex, poolCx, poolCy, 56, outlineColor);
                    DrawCircle(tex, poolCx, poolCy, 52, outlineColor);
                    break;
            }

            // 4. Final soft sketchy cross-hatching to blend into board-game texture look
            for (int i = -size; i < size * 2; i += 24)
            {
                DrawLine(tex, i, 0, i + size, size, sketchColor);
                if (i % 48 == 0)
                {
                    DrawLine(tex, i, size, i + size, 0, new Color(0.1f, 0.1f, 0.12f, 0.06f));
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);

            Random.state = oldState;
        }

        private static Color GetBaseColor(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.UrbanRuins:
                    return new Color(0.18f, 0.18f, 0.2f);   // Asphalt black center
                case BiomeType.Highways:
                    return new Color(0.16f, 0.16f, 0.18f);   // Dark asphalt
                case BiomeType.OvergrownForests:
                    return new Color(0.18f, 0.24f, 0.16f);   // Desaturated grass green
                case BiomeType.Highlands:
                    return new Color(0.24f, 0.2f, 0.16f);    // Clay brown dirt
                case BiomeType.Waterways:
                    return new Color(0.15f, 0.15f, 0.18f);   // Dock concrete
                case BiomeType.SuburbanVillages:
                    return new Color(0.25f, 0.22f, 0.2f);    // Warm brick grey-brown
                case BiomeType.SpecialEvent:
                    return new Color(0.18f, 0.18f, 0.2f);   // Hazard zone concrete
                default:
                    return new Color(0.2f, 0.2f, 0.2f);
            }
        }

        private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color)
        {
            int w = tex.width;
            int h = tex.height;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                int px = (x0 % w + w) % w;
                int py = (y0 % h + h) % h;

                Color current = tex.GetPixel(px, py);
                tex.SetPixel(px, py, Color.Lerp(current, color, color.a));

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void FillRect(Texture2D tex, int x, int y, int width, int height, Color color)
        {
            int w = tex.width;
            int h = tex.height;
            for (int cy = y; cy < y + height; cy++)
            {
                int py = (cy % h + h) % h;
                for (int cx = x; cx < x + width; cx++)
                {
                    int px = (cx % w + w) % w;
                    tex.SetPixel(px, py, color);
                }
            }
        }

        private static void DrawRectOutline(Texture2D tex, int x, int y, int width, int height, Color color)
        {
            DrawLine(tex, x, y, x + width, y, color);
            DrawLine(tex, x, y, x, y + height, color);
            DrawLine(tex, x + width, y, x + width, y + height, color);
            DrawLine(tex, x, y + height, x + width, y + height, color);
        }

        private static void DrawCircle(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int w = tex.width;
            int h = tex.height;
            int steps = 36;
            int px = cx + radius;
            int py = cy;

            for (int i = 1; i <= steps; i++)
            {
                float angle = (i / (float)steps) * Mathf.PI * 2f;
                int nx = cx + (int)(Mathf.Cos(angle) * radius);
                int ny = cy + (int)(Mathf.Sin(angle) * radius);
                DrawLine(tex, px, py, nx, ny, color);
                px = nx;
                py = ny;
            }
        }

        private static void DrawCrack(Texture2D tex, int x, int y, int length, Color color)
        {
            int segments = length / 6;
            int cx = x;
            int cy = y;

            for (int i = 0; i < segments; i++)
            {
                int nx = cx + Random.Range(-10, 11);
                int ny = cy + Random.Range(-10, 11);
                DrawLine(tex, cx, cy, nx, ny, color);
                cx = nx;
                cy = ny;
            }
        }

        private static void DrawWavyLineBounded(Texture2D tex, int startY, float wavelength, float amplitude, Color color, int minX, int maxX)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float offset = Mathf.Sin(x / wavelength * Mathf.PI * 2f) * amplitude;
                int y = startY + (int)offset;
                int py = (y % tex.height + tex.height) % tex.height;
                Color current = tex.GetPixel(x, py);
                tex.SetPixel(x, py, Color.Lerp(current, color, color.a));
            }
        }

        private static void DrawGrassTuft(Texture2D tex, int x, int y, Color color)
        {
            DrawLine(tex, x, y, x - 6, y + 14, color);
            DrawLine(tex, x, y, x, y + 18, color);
            DrawLine(tex, x, y, x + 6, y + 14, color);
        }

        private static void DrawCuteFlower(Texture2D tex, int cx, int cy, Color color)
        {
            DrawLine(tex, cx - 5, cy, cx + 5, cy, color);
            DrawLine(tex, cx, cy - 5, cx, cy + 5, color);
            int w = tex.width;
            int h = tex.height;
            tex.SetPixel((cx % w + w) % w, (cy % h + h) % h, new Color(color.r * 1.3f, color.g * 1.3f, color.b * 0.8f, color.a * 1.5f));
        }

        private static void DrawStoneArc(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int steps = 12;
            int px = cx - radius;
            int py = cy;

            for (int i = 1; i <= steps; i++)
            {
                float angle = (i / (float)steps) * Mathf.PI;
                int nx = cx - (int)(Mathf.Cos(angle) * radius);
                int ny = cy + (int)(Mathf.Sin(angle) * radius);
                DrawLine(tex, px, py, nx, ny, color);
                px = nx;
                py = ny;
            }
        }

        private static void DrawBlob(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int w = tex.width;
            int h = tex.height;
            for (int y = cy - radius; y < cy + radius; y++)
            {
                int py = (y % h + h) % h;
                for (int x = cx - radius; x < cx + radius; x++)
                {
                    int px = (x % w + w) % w;
                    // Distance with Perlin noise to make a blobby organic puddle
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 16f - 8f;
                    if (dist + noise < radius)
                    {
                        Color current = tex.GetPixel(px, py);
                        tex.SetPixel(px, py, Color.Lerp(current, color, color.a));
                    }
                }
            }
        }

        private static void DrawDiagonalStripeBorder(Texture2D tex, int startVal, int stripeWidth, int borderSize, Color yellowColor, Color blackColor)
        {
            int w = tex.width;
            int h = tex.height;

            for (int y = 0; y < h; y++)
            {
                // Only draw inside the border margin
                bool isBorder = (y < borderSize) || (y >= h - borderSize);
                
                for (int x = 0; x < w; x++)
                {
                    bool isLeftRightBorder = (x < borderSize) || (x >= w - borderSize);
                    
                    if (isBorder || isLeftRightBorder)
                    {
                        // Calculate diagonal band index
                        int val = x + y;
                        bool isYellowStripe = ((val - startVal) % (stripeWidth * 2)) < stripeWidth;
                        Color targetColor = isYellowStripe ? yellowColor : blackColor;
                        
                        // Soft overlay
                        Color current = tex.GetPixel(x, y);
                        tex.SetPixel(x, y, Color.Lerp(current, targetColor, 0.7f));
                    }
                }
            }
        }
    }
}
