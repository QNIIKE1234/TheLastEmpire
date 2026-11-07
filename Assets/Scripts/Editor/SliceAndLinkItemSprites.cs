using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace TheLastEmpire
{
    public class SliceAndLinkItemSprites : EditorWindow
    {
        private struct SheetDef
        {
            public string sheetName;
            public string relativePath;
            public int startItem;
            public int endItem;

            public SheetDef(string name, string path, int start, int end)
            {
                sheetName = name;
                relativePath = path;
                startItem = start;
                endItem = end;
            }
        }

        [MenuItem("Tools/Slice and Link All Item Sprites")]
        public static void SliceAndLinkAll()
        {
            SheetDef[] sheets = new SheetDef[]
            {
                new SheetDef("ITEM1TO30", "Assets/Resources_Addressable/Image/UI/ITEM1TO30.png", 1, 30),
                new SheetDef("ITEM31TO60", "Assets/Resources_Addressable/Image/UI/ITEM31TO60.png", 31, 60),
                new SheetDef("ITEM61TO90", "Assets/Resources_Addressable/Image/UI/ITEM61TO90.png", 61, 90),
            };

            // Force re-import textures to update sprite sub-assets
            foreach (var sheet in sheets)
            {
                AssetDatabase.ImportAsset(sheet.relativePath, ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.Refresh();

            // Link all sprites to ItemData
            int linkedCount = 0;
            string itemDataDir = "Assets/Resources_Addressable/Database/ItemData";

            foreach (var sheet in sheets)
            {
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(sheet.relativePath);
                Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>();

                foreach (Object obj in subAssets)
                {
                    if (obj is Sprite sprite)
                    {
                        spriteMap[sprite.name] = sprite;
                    }
                }

                for (int num = sheet.startItem; num <= sheet.endItem; num++)
                {
                    string[] assetGuids = AssetDatabase.FindAssets($"ITEM{num:D4}*", new string[] { itemDataDir });
                    if (assetGuids.Length > 0)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[0]);
                        ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                        if (itemData != null)
                        {
                            string expectedSpriteName = Path.GetFileNameWithoutExtension(assetPath);
                            if (spriteMap.TryGetValue(expectedSpriteName, out Sprite matchedSprite))
                            {
                                itemData.icon = matchedSprite;
                                EditorUtility.SetDirty(itemData);
                                linkedCount++;
                            }
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SliceAndLinkItemSprites] Successfully verified and linked {linkedCount} ItemData icons!");
            EditorUtility.DisplayDialog("Item Sprites", $"Successfully sliced and linked {linkedCount}/90 ItemData icons!", "OK");
        }
    }
}

