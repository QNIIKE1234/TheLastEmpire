using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [Serializable]
    public class StagePlacedObject
    {
        public string prefabName;       // e.g. "Apoc_Prop_Concrete_Barricade"
        public string resourcePath;     // e.g. "Prefabs/Environment/Apoc_Prop_Concrete_Barricade"
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
    }

    [Serializable]
    public class StageEnemySpawnPoint
    {
        public string enemyPrefabName;  // e.g. "ZombieNormal", "ZombieRunner", "ZombieBoomer", "ZombieDog", "ZombieLeader"
        public Vector3 position;
        public Vector3 rotation;
    }

    [Serializable]
    public class StageLootSpawnPoint
    {
        public string itemName;         // e.g. "Bread", "Potion", "Money"
        public int quantity = 1;
        public bool isMoney = false;
        public int moneyAmount = 50;
        public Vector3 position;
    }

    [Serializable]
    public class StageNPCSpawnPoint
    {
        public NPCType npcType = NPCType.Shop;
        public Vector3 position;
        public Vector3 rotation;
    }

    [Serializable]
    public class StageTemplateData
    {
        public string templateId = "New_Stage_Template";
        public string templateName = "New Stage Template";
        public BiomeType biome = BiomeType.UrbanRuins;
        public float selectionWeight = 1.0f; // Weight for random selection pool
        public Vector2 roomSize = new Vector2(32f, 18f);

        public List<StagePlacedObject> obstacles = new List<StagePlacedObject>();
        public List<StagePlacedObject> decorations = new List<StagePlacedObject>();
        public List<StageEnemySpawnPoint> enemySpawns = new List<StageEnemySpawnPoint>();
        public List<StageLootSpawnPoint> lootSpawns = new List<StageLootSpawnPoint>();
        public List<StageNPCSpawnPoint> npcSpawns = new List<StageNPCSpawnPoint>();

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static StageTemplateData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<StageTemplateData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageTemplateData] Failed to parse template JSON: {ex.Message}");
                return null;
            }
        }
    }
}
