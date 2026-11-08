#if UNITY_EDITOR
using System;
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
        [InitializeOnLoadMethod]
        private static void AutoSyncAddressables()
        {
            EditorApplication.delayCall += () =>
            {
                SyncAllAddressables(false);
            };
        }

        [MenuItem("The Last Empire/Migrate Resources to Addressables")]
        public static void MigrateAll()
        {
            SyncAllAddressables(true);
        }

        public static void SyncAllAddressables(bool verbose = false)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (verbose)
                {
                    Debug.LogError("[AddressableMigrationTool] AddressableAssetSettings not found! Please create Addressables settings first.");
                }
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

            // 2. Register all assets in Assets/Resources_Addressable to Addressables
            int registered = RegisterAddressablesInFolder(targetFolder, settings);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            if (verbose || registered > 0)
            {
                Debug.Log($"[AddressableMigrationTool] Addressables sync completed. Total synchronized assets: {registered}");
            }
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

        public static int RegisterAddressablesInFolder(string rootFolder, AddressableAssetSettings settings)
        {
            if (!Directory.Exists(rootFolder) || settings == null) return 0;

            string[] allAssetPaths = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);
            int count = 0;
            bool modified = false;

            foreach (string filePath in allAssetPaths)
            {
                string unityPath = filePath.Replace('\\', '/');
                if (unityPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

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

                AddressableAssetEntry entry = settings.FindAssetEntry(guid);
                if (entry == null || entry.parentGroup != group || entry.address != addressKey)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    if (entry != null)
                    {
                        entry.address = addressKey;
                        modified = true;
                    }
                }

                if (entry != null)
                {
                    // Add labels
                    List<string> labels = DetermineLabels(relativePath);
                    foreach (var lbl in labels)
                    {
                        settings.AddLabel(lbl);
                        if (!entry.labels.Contains(lbl))
                        {
                            entry.SetLabel(lbl, true, false);
                            modified = true;
                        }
                    }

                    count++;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(settings);
            }

            return count;
        }

        private static string DetermineGroupName(string relativePath)
        {
            if (relativePath.StartsWith("Prefabs/Environment", StringComparison.OrdinalIgnoreCase)) return "Environment_Group";
            if (relativePath.StartsWith("Prefabs/Character", StringComparison.OrdinalIgnoreCase)) return "Characters_Group";
            if (relativePath.StartsWith("Prefabs/Item", StringComparison.OrdinalIgnoreCase)) return "Items_Group";
            if (relativePath.StartsWith("Prefabs/Weapon", StringComparison.OrdinalIgnoreCase)) return "Weapons_Group";
            if (relativePath.StartsWith("Prefabs/UI", StringComparison.OrdinalIgnoreCase)) return "UI_Group";
            if (relativePath.StartsWith("Prefabs", StringComparison.OrdinalIgnoreCase)) return "Prefabs_Group";
            if (relativePath.StartsWith("Image", StringComparison.OrdinalIgnoreCase)) return "Images_Group";
            if (relativePath.StartsWith("Stages", StringComparison.OrdinalIgnoreCase)) return "Stages_Group";
            if (relativePath.StartsWith("Database", StringComparison.OrdinalIgnoreCase)) return "Database_Group";
            if (relativePath.StartsWith("Models", StringComparison.OrdinalIgnoreCase)) return "Environment_Group";

            return "Default Local Group";
        }

        private static List<string> DetermineLabels(string relativePath)
        {
            var labels = new List<string>();

            if (relativePath.StartsWith("Prefabs/Environment", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Environment");
                if (relativePath.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    relativePath.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    labels.Add("Building");
                }
                if (relativePath.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    labels.Add("Craft");
                }
                if (relativePath.Contains("Urban_Obstacles")) labels.Add("Urban_Obstacles");
                if (relativePath.Contains("Urban_Decorations")) labels.Add("Urban_Decorations");
            }
            else if (relativePath.StartsWith("Prefabs/Character", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Character");
            }
            else if (relativePath.StartsWith("Prefabs/Item", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Item");
            }
            else if (relativePath.StartsWith("Prefabs/Weapon", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Weapon");
            }
            else if (relativePath.StartsWith("Prefabs/UI", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("UI");
            }
            else if (relativePath.StartsWith("Image", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Image");
                if (relativePath.Contains("UI")) labels.Add("UI");
            }
            else if (relativePath.StartsWith("Stages", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Stage");
            }
            else if (relativePath.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Database");
                if (relativePath.Contains("ItemData")) labels.Add("ItemData");
                if (relativePath.Contains("Characters")) labels.Add("Characters");
                if (relativePath.Contains("Buildings"))
                {
                    labels.Add("BuildingData");
                    labels.Add("Building");
                }
            }
            else if (relativePath.StartsWith("Models", StringComparison.OrdinalIgnoreCase))
            {
                labels.Add("Environment");
                labels.Add("Model");
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
#endif
