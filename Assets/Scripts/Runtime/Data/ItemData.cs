using UnityEngine;

namespace TheLastEmpire
{
    public enum ItemType
    {
        Potion,
        Ammo,
        Bread,
        ETC,
        Money
    }

    [CreateAssetMenu(fileName = "NewItemData", menuName = "TheLastEmpire/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string itemName;
        [TextArea(2, 5)]
        public string description;
        public Sprite icon;
        public ItemType type;

        [Header("Stats & Settings")]
        public float restorationValue; // HP healed or Hunger restored
        public int defaultQuantity = 1;
        public Color themeColor = Color.white; // Theme color for drops/prompts
    }
}
