using System.IO;
using UnityEngine;

namespace TheLastEmpire
{
    public static class SaveSystem
    {
        private static readonly string SaveFileName = "savegame.json";

        public static string SaveFilePath
        {
            get
            {
#if UNITY_EDITOR
                // Save in the project root folder during editor testing for easy access and reading
                return Path.Combine(Application.dataPath, "../savegame.json");
#else
                // Save in standard persistent path on runtime devices
                return Path.Combine(Application.persistentDataPath, SaveFileName);
#endif
            }
        }

        public static void Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"Game saved successfully to: {SaveFilePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save game: {ex.Message}");
            }
        }

        public static SaveData Load()
        {
            if (!HasSaveFile())
            {
                Debug.LogWarning("No save file found.");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                Debug.Log($"Game loaded successfully from: {SaveFilePath}");
                return data;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load game: {ex.Message}");
                return null;
            }
        }

        public static bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }

        public static void DeleteSave()
        {
            if (HasSaveFile())
            {
                File.Delete(SaveFilePath);
                Debug.Log("Save file deleted.");
            }
        }
    }
}
