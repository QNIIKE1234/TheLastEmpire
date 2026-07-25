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
        RangedWeapon,
        MeleeWeapon,
        ThrowingWeapon
    }

    [System.Serializable]
    public class WeaponConfig
    {
        [Tooltip("The bullet or grenade prefab to spawn")]
        public Projectile projectilePrefab;
        public float fireRate = 0.2f;
        public int magazineSize = 12;
        public float reloadDuration = 1.0f;
        [Tooltip("Projectile lifetime or range")]
        public float range = 3f;
        public bool canPierce = false;
        
        [Header("Spread & Pellets (Shotgun)")]
        public float spreadAngle = 0f;
        public int pelletsPerShot = 1;
        public bool isAutomatic = false;
        
        [Tooltip("Type of ammo this weapon consumes (e.g. Pistol Ammo)")]
        public string ammoType;

        [Header("Visuals")]
        [Tooltip("Muzzle flash VFX pool key")]
        public string vfxPoolKey;
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

        [Header("Ranged/Throwing Weapon Config")]
        public WeaponConfig weaponConfig;
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
            SerializedProperty weaponConfig = serializedObject.FindProperty("weaponConfig");

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

            // Conditionally show weapon fields if item type is a weapon
            bool isWeapon = type.enumValueIndex == (int)ItemType.RangedWeapon || 
                            type.enumValueIndex == (int)ItemType.MeleeWeapon || 
                            type.enumValueIndex == (int)ItemType.ThrowingWeapon;

            if (isWeapon)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Base Weapon Stats", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(damage);
                EditorGUILayout.PropertyField(attackRadius);
                EditorGUILayout.PropertyField(attackRate);
                EditorGUILayout.PropertyField(knockbackForce);
                EditorGUILayout.PropertyField(staggerDuration);
            }

            bool isRangedOrThrowing = type.enumValueIndex == (int)ItemType.RangedWeapon || 
                                      type.enumValueIndex == (int)ItemType.ThrowingWeapon;
            
            if (isRangedOrThrowing)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(weaponConfig);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

