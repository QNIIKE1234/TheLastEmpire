#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// SliceAndLinkBuildingSprites — ตัดรูป BUILDING1TO22.png (6 คอลัมน์ x 4 แถว)
    /// และทำการ Reference ไอคอนเข้ากับ BuildingData ทั้งหมด 22 ชนิด + CraftingDatabase อัตโนมัติ
    /// </summary>
    public static class SliceAndLinkBuildingSprites
    {
        private const string TexturePath = "Assets/Resources_Addressable/Image/UI/BUILDING1TO22.png";
        private const string BuildingDataDir = "Assets/Resources_Addressable/Database/Buildings";
        private const string CraftingDbPath = "Assets/Resources_Addressable/Database/CraftingDatabase.asset";

        private static readonly string[] BuildingIds = new string[]
        {
            // Row 0 (แถวบนสุด: Index 1 - 6)
            "BUILD0001_WoodenFloorFoundation",
            "BUILD0002_WoodenWallPanel",
            "BUILD0003_WoodenWindowWall",
            "BUILD0004_ReinforcedWoodenDoor",
            "BUILD0005_StoneAndBrickWall",
            "BUILD0006_ScrapMetalWall",

            // Row 1 (แถวที่สอง: Index 7 - 12)
            "BUILD0007_ArmoredTitaniumWall",
            "BUILD0008_MotorizedBlastDoor",
            "BUILD0009_SurvivorCampfire",
            "BUILD0010_SurvivorWorkbench",
            "BUILD0011_WoodenStorageCrate",
            "BUILD0012_WaterPurifierStation",

            // Row 2 (แถวที่สาม: Index 13 - 18)
            "BUILD0013_SurvivorSleepingBedroll",
            "BUILD0014_SanityBeaconLantern",
            "BUILD0015_CompactFusionGenerator",
            "BUILD0016_WoodenSpikeBarricade",
            "BUILD0017_RazorWireBarricade",
            "BUILD0018_ChitinCarapaceWall",

            // Row 3 (แถวล่างสุด: Index 19 - 22)
            "BUILD0019_SnapJawBearTrap",
            "BUILD0020_CorrosiveBileLandmine",
            "BUILD0021_AutomatedSentryTurret",
            "BUILD0022_XenocrystalShockTower"
        };

        [InitializeOnLoadMethod]
        public static void InitializeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                ExecuteSliceAndLink(false);
            };
        }

        [MenuItem("Tools/The Last Empire/Slice and Link Building Sprites")]
        public static void SliceAndLinkAll()
        {
            ExecuteSliceAndLink(true);
        }

        public static void ExecuteSliceAndLink(bool showDialog)
        {
            if (!File.Exists(TexturePath))
            {
                Debug.LogError($"[SliceAndLinkBuildingSprites] ไม่พบไฟล์ที่ {TexturePath}");
                return;
            }

            // 1. ตรวจสอบและทำให้พื้นหลังโปร่งใส (Flood Fill จากขอบ ป้องกันไม่ให้สีขาวในตัวไอคอนโดนตัด)
            ProcessBackgroundTransparency();

            // 2. ตั้งค่า TextureImporter และตัดแบ่ง Sprite Rects 22 รูป
            SliceSpriteSheet();

            // 3. ผูก Reference เข้ากับ BuildingData และ CraftingDatabase
            int linkedCount = LinkSpritesToBuildingsAndRecipes();

            AssetDatabase.SaveAssets();
            Debug.Log($"[SliceAndLinkBuildingSprites] ตัดรูปและผูก Reference ให้ BuildingData และ CraftingDatabase สำเร็จ {linkedCount} รายการ!");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Building Sprites", $"ตัดรูป BUILDING1TO22 และผูก Reference สำเร็จ {linkedCount} รายการเรียบร้อย!", "ตกลง");
            }
        }

        public static void ProcessBackgroundTransparency()
        {
            byte[] fileData = File.ReadAllBytes(TexturePath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(fileData)) return;

            int width = tex.width;
            int height = tex.height;
            Color[] pixels = tex.GetPixels();

            Color corner = pixels[0];
            // ถ้ามุมซ้ายล่างมี alpha = 0 อยู่แล้ว แสดงว่าโปร่งใสอยู่แล้ว ไม่ต้องทำซ้ำ
            if (corner.a < 0.1f)
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return;
            }

            // ถ้าพื้นหลังเป็นสีขาวเกือบสนิท (R > 0.85, G > 0.85, B > 0.85)
            if (corner.r > 0.85f && corner.g > 0.85f && corner.b > 0.85f)
            {
                bool[] visited = new bool[pixels.Length];
                Queue<int> queue = new Queue<int>();

                // เพิ่มพิกเซลขอบภาพทั้งหมดเป็นจุดเริ่มต้นของ Flood Fill
                for (int x = 0; x < width; x++)
                {
                    EnqueueIfBg(x, 0, width, height, pixels, corner, visited, queue);
                    EnqueueIfBg(x, height - 1, width, height, pixels, corner, visited, queue);
                }
                for (int y = 0; y < height; y++)
                {
                    EnqueueIfBg(0, y, width, height, pixels, corner, visited, queue);
                    EnqueueIfBg(width - 1, y, width, height, pixels, corner, visited, queue);
                }

                // ดำเนินการ Flood Fill เฉพาะพิกเซลที่ต่อเชื่อมกับขอบภายนอกเท่านั้น (พิกเซลสีขาวในตัวสติกเกอร์จะไม่โดนแตะ)
                while (queue.Count > 0)
                {
                    int idx = queue.Dequeue();
                    pixels[idx] = Color.clear;

                    int px = idx % width;
                    int py = idx / width;

                    if (px > 0) EnqueueIfBg(px - 1, py, width, height, pixels, corner, visited, queue);
                    if (px < width - 1) EnqueueIfBg(px + 1, py, width, height, pixels, corner, visited, queue);
                    if (py > 0) EnqueueIfBg(px, py - 1, width, height, pixels, corner, visited, queue);
                    if (py < height - 1) EnqueueIfBg(px, py + 1, width, height, pixels, corner, visited, queue);
                }

                tex.SetPixels(pixels);
                tex.Apply();

                byte[] pngBytes = tex.EncodeToPNG();
                File.WriteAllBytes(TexturePath, pngBytes);
                Debug.Log("[SliceAndLinkBuildingSprites] ประมวลผลพื้นหลังภายนอกให้โปร่งใสเรียบร้อย");
            }

            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void EnqueueIfBg(int x, int y, int width, int height, Color[] pixels, Color targetBg, bool[] visited, Queue<int> queue)
        {
            int idx = y * width + x;
            if (visited[idx]) return;

            Color c = pixels[idx];
            float diff = Mathf.Abs(c.r - targetBg.r) + Mathf.Abs(c.g - targetBg.g) + Mathf.Abs(c.b - targetBg.b);
            if (diff < 0.2f)
            {
                visited[idx] = true;
                queue.Enqueue(idx);
            }
        }

        public static void SliceSpriteSheet()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) return;

            importer.isReadable = true;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            int cols = 6;
            int cellSize = 256;
            int totalHeight = 1024;

            SpriteMetaData[] metaArray = new SpriteMetaData[BuildingIds.Length];

            for (int i = 0; i < BuildingIds.Length; i++)
            {
                int col = i % cols;
                int rowFromTop = i / cols;
                // ใน Unity พิกัด Y=0 คือด้านล่างสุด ดังนั้นแถวบนสุดคือ Y = totalHeight - (rowFromTop + 1) * cellSize
                int y = totalHeight - (rowFromTop + 1) * cellSize;
                int x = col * cellSize;

                metaArray[i] = new SpriteMetaData
                {
                    name = BuildingIds[i],
                    rect = new Rect(x, y, cellSize, cellSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }

#pragma warning disable CS0618
            importer.spritesheet = metaArray;
#pragma warning restore CS0618

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        private static int LinkSpritesToBuildingsAndRecipes()
        {
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(TexturePath);
            Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

            foreach (UnityEngine.Object obj in subAssets)
            {
                if (obj is Sprite sprite)
                {
                    spriteMap[sprite.name] = sprite;
                }
            }

            int linkedCount = 0;

            // 1. ผูกกับ BuildingData assets ใน Assets/Resources_Addressable/Database/Buildings
            for (int i = 0; i < BuildingIds.Length; i++)
            {
                string bId = BuildingIds[i];
                if (!spriteMap.TryGetValue(bId, out Sprite sprite))
                {
                    Debug.LogWarning($"[SliceAndLinkBuildingSprites] ไม่พบ Sprite: {bId}");
                    continue;
                }

                string assetPath = $"{BuildingDataDir}/{bId}.asset";
                BuildingData bData = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
                if (bData != null)
                {
                    bData.icon = sprite;
                    EditorUtility.SetDirty(bData);
                    linkedCount++;
                }
            }

            // Sync Legacy BuildingData assets
            SyncLegacyBuildingIcon("BUILD0001_Campfire", "BUILD0009_SurvivorCampfire", spriteMap);
            SyncLegacyBuildingIcon("BUILD0002_WoodenSpikeBarricade", "BUILD0016_WoodenSpikeBarricade", spriteMap);
            SyncLegacyBuildingIcon("BUILD0003_WoodenStorageCrate", "BUILD0011_WoodenStorageCrate", spriteMap);

            // 2. ผูกกับ CraftingDatabase.asset
            CraftingDatabase craftDb = AssetDatabase.LoadAssetAtPath<CraftingDatabase>(CraftingDbPath);
            if (craftDb != null && craftDb.recipes != null)
            {
                for (int i = 0; i < BuildingIds.Length; i++)
                {
                    string bId = BuildingIds[i];
                    string recipeId = $"CRAFT{i + 1:D4}";

                    if (!spriteMap.TryGetValue(bId, out Sprite sprite)) continue;

                    CraftingRecipe recipe = craftDb.recipes.Find(r => r != null && 
                        (string.Equals(r.recipeId, recipeId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(r.recipeName, GetFriendlyBuildingName(bId), StringComparison.OrdinalIgnoreCase)));

                    if (recipe != null)
                    {
                        recipe.icon = sprite;
                        // เคลียร์ resultItem ออกเพื่อไม่ให้ชี้ไปที่ Wood Log โดยไม่ได้ตั้งใจ
                        recipe.resultItem = null;
                        EditorUtility.SetDirty(craftDb);
                    }
                }
            }

            return linkedCount;
        }

        private static void SyncLegacyBuildingIcon(string legacyName, string sourceSpriteName, Dictionary<string, Sprite> spriteMap)
        {
            if (spriteMap.TryGetValue(sourceSpriteName, out Sprite sprite))
            {
                string path = $"{BuildingDataDir}/{legacyName}.asset";
                BuildingData legacyData = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (legacyData != null)
                {
                    legacyData.icon = sprite;
                    EditorUtility.SetDirty(legacyData);
                }
            }
        }

        private static string GetFriendlyBuildingName(string buildingId)
        {
            // แปลง BUILD0001_WoodenFloorFoundation -> Wooden Floor Foundation
            int under = buildingId.IndexOf('_');
            if (under >= 0 && under < buildingId.Length - 1)
            {
                string namePart = buildingId.Substring(under + 1);
                // Insert space before capital letters
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < namePart.Length; i++)
                {
                    if (i > 0 && char.IsUpper(namePart[i]))
                    {
                        sb.Append(' ');
                    }
                    sb.Append(namePart[i]);
                }
                return sb.ToString();
            }
            return buildingId;
        }
    }
}
#endif
