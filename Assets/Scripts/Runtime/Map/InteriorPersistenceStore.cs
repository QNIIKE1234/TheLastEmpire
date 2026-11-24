using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheLastEmpire
{
    [Serializable]
    public class InteriorSavedLootSlot
    {
        public string itemName;
        public int quantity;
    }

    [Serializable]
    public class InteriorSavedContainerRecord
    {
        public string containerKey;
        public int remainingMoney;
        public bool isSearched;
        public List<InteriorSavedLootSlot> remainingItems = new List<InteriorSavedLootSlot>();
    }

    [Serializable]
    public class InteriorPersistenceSaveData
    {
        public int worldSeed = 0;
        public List<string> visitedFloorKeys = new List<string>();
        public List<string> clearedFloorKeys = new List<string>();
        public List<InteriorSavedContainerRecord> containers = new List<InteriorSavedContainerRecord>();
    }

    /// <summary>
    /// Persists building interior progress to JSON on disk (Application.persistentDataPath/InteriorProgressSave.json):
    /// - Which buildings/floors have been entered (visited)
    /// - Which buildings/floors have had their zombies cleared (never respawn!)
    /// - Exact remaining loot & cash in every interior container (looted containers never refill!)
    /// </summary>
    public static class InteriorPersistenceStore
    {
        private const string SaveFileName = "InteriorProgressSave.json";

        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static void LoadInto(
            int activeWorldSeed,
            HashSet<string> visitedFloors,
            HashSet<string> clearedFloors,
            Dictionary<string, InteriorSavedContainerRecord> containerMap)
        {
            try
            {
                string path = SaveFilePath;
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                InteriorPersistenceSaveData data = JsonUtility.FromJson<InteriorPersistenceSaveData>(json);
                if (data == null) return;

                // If a different world seed is running, keep memory clean for the new world
                if (activeWorldSeed != 0 && data.worldSeed != 0 && data.worldSeed != activeWorldSeed)
                {
                    return;
                }

                if (data.visitedFloorKeys != null)
                {
                    for (int i = 0; i < data.visitedFloorKeys.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(data.visitedFloorKeys[i]))
                            visitedFloors.Add(data.visitedFloorKeys[i]);
                    }
                }

                if (data.clearedFloorKeys != null)
                {
                    for (int i = 0; i < data.clearedFloorKeys.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(data.clearedFloorKeys[i]))
                            clearedFloors.Add(data.clearedFloorKeys[i]);
                    }
                }

                if (data.containers != null)
                {
                    for (int i = 0; i < data.containers.Count; i++)
                    {
                        InteriorSavedContainerRecord rec = data.containers[i];
                        if (rec != null && !string.IsNullOrEmpty(rec.containerKey))
                        {
                            containerMap[rec.containerKey] = rec;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InteriorPersistenceStore] Failed to load JSON save: {ex.Message}");
            }
        }

        public static void SaveToDisk(
            int activeWorldSeed,
            HashSet<string> visitedFloors,
            HashSet<string> clearedFloors,
            Dictionary<string, InteriorSavedContainerRecord> containerMap)
        {
            try
            {
                InteriorPersistenceSaveData data = new InteriorPersistenceSaveData
                {
                    worldSeed = activeWorldSeed
                };

                foreach (string k in visitedFloors) data.visitedFloorKeys.Add(k);
                foreach (string k in clearedFloors) data.clearedFloorKeys.Add(k);
                foreach (var kvp in containerMap)
                {
                    if (kvp.Value != null) data.containers.Add(kvp.Value);
                }

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string dir = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InteriorPersistenceStore] Failed to save JSON: {ex.Message}");
            }
        }

        public static void DeleteSaveFile()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InteriorPersistenceStore] Failed to delete JSON save: {ex.Message}");
            }
        }
    }
}
