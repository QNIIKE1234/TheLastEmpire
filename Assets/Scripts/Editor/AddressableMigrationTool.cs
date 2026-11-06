using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace TheLastEmpire.Editor
{
    public static class AddressableMigrationTool
    {
        [MenuItem("The Last Empire/Migrate Resources to Addressables")]
        public static void MigrateAll()
        {
            Debug.Log("[AddressableMigrationTool] Starting Addressables migration...");

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressableMigrationTool] AddressableAssetSettings not found! Please create Addressables settings first.");
                return;
            }

            string targetFolder = "Assets/Resources_Addressable";
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            // 1. Move folders from Resources to Resources_Addressable if they still exist in Resources
            MoveFolderIfPresent("Assets/Resources/Prefabs", "Assets/Resources_Addressable/Prefabs");
            MoveFolderIfPresent("Assets/Resources/Image", "Assets/Resources_Addressable/Image");
            MoveFolderIfPresent("Assets/Resources/Stages", "Assets/Resources_Addressable/Stages");
            MoveFolderIfPresent("Assets/Resources/Database", "Assets/Resources_Addressable/Database");

            AssetDatabase.Refresh();

            // 2. Register all assets in Assets/Resources_Addressable to Addressables
            RegisterAddressablesInFolder(targetFolder, settings);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AddressableMigrationTool] Migration completed successfully!");
        }

        private static void MoveFolderIfPresent(string source, string destination)
        {
            if (Directory.Exists(source))
            {
                string result = AssetDatabase.MoveAsset(source, destination);
                if (string.IsNullOrEmpty(result))
                {
                    Debug.Log($"[AddressableMigrationTool] Moved '{source}' -> '{destination}'");
                }
                else
                {
                    Debug.LogWarning($"[AddressableMigrationTool] MoveAsset failed or partially moved '{source}': {result}");
                }
            }
        }

        public static void RegisterAddressablesInFolder(string rootFolder, AddressableAssetSettings settings)
        {
            if (!Directory.Exists(rootFolder)) return;

            string[] allAssetPaths = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);
            int count = 0;

            foreach (string filePath in allAssetPaths)
            {
                string unityPath = filePath.Replace('\\', '/');
                if (unityPath.EndsWith(".meta")) continue;

                // Relative path under rootFolder (e.g. "Prefabs/Item/BoltPrefabs.prefab")
                string relativePath = unityPath.Substring(rootFolder.Length).TrimStart('/');
                string addressKey = relativePath;
                // Strip extension for address key
                int dotIndex = addressKey.LastIndexOf('.');
                if (dotIndex > 0)
                {
                    addressKey = addressKey.Substring(0, dotIndex);
                }

                // Determine target group
                string groupName = DetermineGroupName(relativePath);
                AddressableAssetGroup group = GetOrCreateGroup(settings, groupName);

                string guid = AssetDatabase.AssetPathToGUID(unityPath);
                if (string.IsNullOrEmpty(guid)) continue;

                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry != null)
                {
                    entry.address = addressKey;

                    // Add labels
                    List<string> labels = DetermineLabels(relativePath);
                    foreach (var lbl in labels)
                    {
                        settings.AddLabel(lbl);
                        entry.SetLabel(lbl, true, false);
                    }

                    count++;
                }
            }

            Debug.Log($"[AddressableMigrationTool] Registered {count} assets in Addressables.");
        }

        private static string DetermineGroupName(string relativePath)
        {
            if (relativePath.StartsWith("Prefabs/Environment", System.StringComparison.OrdinalIgnoreCase)) return "Environment_Group";
            if (relativePath.StartsWith("Prefabs/Character", System.StringComparison.OrdinalIgnoreCase)) return "Characters_Group";
            if (relativePath.StartsWith("Prefabs/Item", System.StringComparison.OrdinalIgnoreCase)) return "Items_Group";
            if (relativePath.StartsWith("Prefabs/Weapon", System.StringComparison.OrdinalIgnoreCase)) return "Weapons_Group";
            if (relativePath.StartsWith("Prefabs/UI", System.StringComparison.OrdinalIgnoreCase)) return "UI_Group";
            if (relativePath.StartsWith("Prefabs", System.StringComparison.OrdinalIgnoreCase)) return "Prefabs_Group";
            if (relativePath.StartsWith("Image", System.StringComparison.OrdinalIgnoreCase)) return "Images_Group";
            if (relativePath.StartsWith("Stages", System.StringComparison.OrdinalIgnoreCase)) return "Stages_Group";
            if (relativePath.StartsWith("Database", System.StringComparison.OrdinalIgnoreCase)) return "Database_Group";

            return "Default Local Group";
        }

        private static List<string> DetermineLabels(string relativePath)
        {
            var labels = new List<string>();

            if (relativePath.StartsWith("Prefabs/Environment", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Environment");
                if (relativePath.Contains("Building")) labels.Add("Building");
                if (relativePath.Contains("Urban_Obstacles")) labels.Add("Urban_Obstacles");
                if (relativePath.Contains("Urban_Decorations")) labels.Add("Urban_Decorations");
            }
            else if (relativePath.StartsWith("Prefabs/Character", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Character");
            }
            else if (relativePath.StartsWith("Prefabs/Item", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Item");
            }
            else if (relativePath.StartsWith("Prefabs/Weapon", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Weapon");
            }
            else if (relativePath.StartsWith("Prefabs/UI", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("UI");
            }
            else if (relativePath.StartsWith("Image", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Image");
                if (relativePath.Contains("UI")) labels.Add("UI");
            }
            else if (relativePath.StartsWith("Stages", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Stage");
            }
            else if (relativePath.StartsWith("Database", System.StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Database");
                if (relativePath.Contains("ItemData")) labels.Add("ItemData");
                if (relativePath.Contains("Characters")) labels.Add("Characters");
            }

            return labels;
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, false, settings.DefaultGroup.Schemas);
            }
            return group;
        }
    }
}

