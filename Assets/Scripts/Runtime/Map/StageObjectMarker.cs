using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Helper component attached to objects created in the Stage Template Designer.
    /// Preserves original prefab identity, resource paths, and custom metadata for clean JSON serialization.
    /// </summary>
    [ExecuteInEditMode]
    public class StageObjectMarker : MonoBehaviour
    {
        public enum MarkerCategory
        {
            Obstacle,
            Decoration,
            EnemySpawn,
            LootSpawn,
            NPCSpawn
        }

        [Header("Classification")]
        public MarkerCategory category = MarkerCategory.Obstacle;
        public string originalPrefabName = "";
        public string resourcePath = "";

        [Header("Loot Details (if category == LootSpawn)")]
        public string itemName = "Bread";
        public int quantity = 1;
        public bool isMoney = false;
        public int moneyAmount = 50;

        [Header("NPC Details (if category == NPCSpawn)")]
        public NPCType npcType = NPCType.Shop;

        private void Reset()
        {
            if (string.IsNullOrEmpty(originalPrefabName))
            {
                originalPrefabName = gameObject.name.Replace("(Clone)", "").Trim();
            }
        }
    }
}
