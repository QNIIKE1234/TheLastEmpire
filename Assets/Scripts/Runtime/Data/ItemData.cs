using UnityEngine;

namespace TheLastEmpire
{
    public enum ItemType
    {
        Potion,
        Ammo,
        Bread,
        ETC,
        Money,
        Weapon
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
        public Material dropMaterial;
        public int damage; // Additional weapon damage
        public float attackRadius; // Melee range/radius
        public float attackRate; // Attack rate/cooldown
        public float knockbackForce; // Knockback force
        public float staggerDuration; // Stagger duration on hits
    }
}

#if UNITY_EDITOR
namespace TheLastEmpire
{
    using UnityEditor;

    [CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty itemName = serializedObject.FindProperty("itemName");
            SerializedProperty description = serializedObject.FindProperty("description");
            SerializedProperty icon = serializedObject.FindProperty("icon");
            SerializedProperty type = serializedObject.FindProperty("type");
            SerializedProperty restorationValue = serializedObject.FindProperty("restorationValue");
            SerializedProperty defaultQuantity = serializedObject.FindProperty("defaultQuantity");
            SerializedProperty themeColor = serializedObject.FindProperty("themeColor");
            SerializedProperty dropMaterial = serializedObject.FindProperty("dropMaterial");
            SerializedProperty damage = serializedObject.FindProperty("damage");
            SerializedProperty attackRadius = serializedObject.FindProperty("attackRadius");
            SerializedProperty attackRate = serializedObject.FindProperty("attackRate");
            SerializedProperty knockbackForce = serializedObject.FindProperty("knockbackForce");
            SerializedProperty staggerDuration = serializedObject.FindProperty("staggerDuration");

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(itemName);
            EditorGUILayout.PropertyField(description);
            EditorGUILayout.PropertyField(icon);
            EditorGUILayout.PropertyField(type);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stats & Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(restorationValue);
            EditorGUILayout.PropertyField(defaultQuantity);
            EditorGUILayout.PropertyField(themeColor);
            EditorGUILayout.PropertyField(dropMaterial);

            // Conditionally show weapon fields only if item type is Weapon (ItemType index 5)
            if (type.enumValueIndex == (int)ItemType.Weapon)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Weapon Specific Stats", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(damage);
                EditorGUILayout.PropertyField(attackRadius);
                EditorGUILayout.PropertyField(attackRate);
                EditorGUILayout.PropertyField(knockbackForce);
                EditorGUILayout.PropertyField(staggerDuration);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

