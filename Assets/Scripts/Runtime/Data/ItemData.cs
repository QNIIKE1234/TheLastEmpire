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
        ThrowingWeapon,
        Material,
        Water,
        Artifact
    }

    public enum ArtifactRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [System.Serializable]
    public class ArtifactModConfig
    {
        [Header("Classification")]
        public ArtifactRarity rarity = ArtifactRarity.Common;
        [Tooltip("รายชื่ออาวุธที่ใส่ Artifact ชิ้นนี้ได้ (เว้นว่าง = ใส่ได้กับทุกอาวุธ) เช่น Sniper Rifle, Shotgun")]
        public System.Collections.Generic.List<string> compatibleWeaponNames = new System.Collections.Generic.List<string>();

        [Header("Stat Modifiers")]
        public float bonusDamage = 0f;
        [Tooltip("ตัวคูณดาเมจเพิ่ม เช่น 0.25 = +25%")]
        public float damageMultiplier = 0f;
        [Tooltip("จำนวนกระสุนที่ยิงออกเพิ่มต่อนัด เช่น +1 (สไนเปอร์ยิงออกทีละ 2 ลูก)")]
        public int bonusPelletsPerShot = 0;
        [Tooltip("มุมกระจายเพิ่มเมื่อยิงหลายลูกพร้อมกัน (องศา)")]
        public float extraSpreadAngle = 0f;
        [Tooltip("ความเร็วการยิง/ฟันเพิ่ม เช่น 0.20 = ไวขึ้น 20%")]
        public float fireRateMultiplier = 0f;
        public int bonusMagazineSize = 0;
        [Tooltip("ตัวคูณความจุแม็กกาซีน เช่น 2.0 = แม็ก x2 (เช่น ถือปืนพก 2 กระบอก Akimbo)")]
        public float magazineMultiplier = 1f;
        [Tooltip("เปิดโหมดถือปืนพก 2 กระบอก (Dual Pistols / Akimbo Animation & Left-Hand Pistol)")]
        public bool dualWieldPistol = false;
        [Tooltip("ความเร็วรีโหลดเพิ่ม เช่น 0.25 = รีโหลดไวขึ้น 25%")]
        public float reloadSpeedMultiplier = 0f;
        public float bonusRange = 0f;
        public bool grantPierce = false;
        public bool grantAutomatic = false;

        [Header("Advanced Combat Modifiers")]
        [Tooltip("เพิ่มโอกาสคริติคอล เช่น 0.20 = +20%")]
        public float bonusCritChance = 0f;
        [Tooltip("เพิ่มตัวคูณคริติคอล เช่น 0.50 = +0.5x")]
        public float bonusCritMultiplier = 0f;
        public float bonusKnockback = 0f;
        public float bonusStagger = 0f;
        [Tooltip("ดูดเลือดคืนเมื่อโจมตีโดน (HP ต่อครั้ง)")]
        public float lifestealOnHit = 0f;
        [Tooltip("โอกาสไม่เสียกระสุนเมื่อยิง (0.0 - 0.85)")]
        public float ammoSaveChance = 0f;
        [Tooltip("ยิงซ้ำอัตโนมัติหลังกดยิง 1 ครั้ง เช่น 1 = กดยิง 1 ทีออก 2 ที (Echo Burst)")]
        public int bonusBurstShots = 0;
        [Tooltip("เพิ่มขนาดกระสุนและ Hitbox เช่น 1.5 = ขนาดใหญ่ขึ้นเป็น 2.5 เท่า (1 + 1.5)")]
        public float projectileScaleBonus = 0f;

        [Header("Legendary Right-Click Active Skill")]
        [Tooltip("รหัสสกิลคลิกขวาของ Artifact ระดับ Legendary เช่น Flamethrower_Fireball, Knife_Hook, Bat_Boomerang, Katana_RipperCombo")]
        public string rightClickSkillId = "";
        [Tooltip("คูลดาวน์สกิลคลิกขวา (วินาที) ค่าเริ่มต้น 5 วินาที")]
        public float rightClickCooldown = 5.0f;
        [Tooltip("เวลาชาร์จคลิกขวา (วินาที) เช่น 2.0 สำหรับปืนไฟ, 0 สำหรับกดออกทันที")]
        public float rightClickChargeDuration = 0f;
        [Tooltip("จำนวนกระสุนที่ใช้ต่อการยิงสกิลคลิกขวา 1 ครั้ง เช่น 10")]
        public int rightClickAmmoCost = 0;
        [Tooltip("ดาเมจสกิลคลิกขวา (ค่าเริ่มต้นเป็นตัวคูณดาเมจปืน เช่น 8.5 = 8.5x หรือเปิด rightClickUseFlatDamage เพื่อใช้เป็นค่าตรง)")]
        public float rightClickDamage = 0f;
        [Tooltip("ระยะยิงของสกิลคลิกขวา (เมตร) เช่น 20")]
        public float rightClickRange = 0f;
        [Tooltip("ถ้าติ๊กถูกจะใช้ rightClickDamage เป็นดาเมจคงที่โดยตรง (ไม่คูณกับดาเมจปืน)")]
        public bool rightClickUseFlatDamage = false;

        [Header("Display")]
        [Tooltip("ข้อความสรุปสั้นๆ (เว้นว่างให้สร้างอัตโนมัติจากค่าสถานะด้านบน)")]
        public string shortEffectSummary;
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

        [Tooltip("ID ของโมเดลกระสุน/ระเบิดสำหรับเปิดตาใน Projectile เช่น ITEM0008_Grenade, ITEM0009_Bullet, ITEM0010_Arrow")]
        public string projectileVisualId;
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
        [Tooltip("Sanity restored when consumed (e.g. Coffee, Pills, Energy Bar)")]
        public float sanityRestorationValue = 0f; // Sanity restored
        [Tooltip("Water / Hydration restored when consumed")]
        public float waterRestorationValue = 0f; // Water restored
        [Tooltip("Max stack size (0 = auto-determined: Ammo=9999, Weapons=1, Others=99)")]
        public int maxStackSize = 0;

        public int MaxStackSize
        {
            get
            {
                if (maxStackSize > 0) return maxStackSize;
                switch (type)
                {
                    case ItemType.Ammo:
                        return 9999;
                    case ItemType.RangedWeapon:
                    case ItemType.MeleeWeapon:
                    case ItemType.ThrowingWeapon:
                        return 1;
                    default:
                        return 99;
                }
            }
        }

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

        [Header("Weapon Artifact Config")]
        public ArtifactModConfig artifactConfig;
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
            SerializedProperty sanityRestorationValue = serializedObject.FindProperty("sanityRestorationValue");
            SerializedProperty waterRestorationValue = serializedObject.FindProperty("waterRestorationValue");
            SerializedProperty maxStackSize = serializedObject.FindProperty("maxStackSize");
            SerializedProperty defaultQuantity = serializedObject.FindProperty("defaultQuantity");
            SerializedProperty themeColor = serializedObject.FindProperty("themeColor");
            SerializedProperty dropMaterial = serializedObject.FindProperty("dropMaterial");
            SerializedProperty damage = serializedObject.FindProperty("damage");
            SerializedProperty attackRadius = serializedObject.FindProperty("attackRadius");
            SerializedProperty attackRate = serializedObject.FindProperty("attackRate");
            SerializedProperty knockbackForce = serializedObject.FindProperty("knockbackForce");
            SerializedProperty staggerDuration = serializedObject.FindProperty("staggerDuration");
            SerializedProperty weaponConfig = serializedObject.FindProperty("weaponConfig");
            SerializedProperty artifactConfig = serializedObject.FindProperty("artifactConfig");

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(itemName);
            EditorGUILayout.PropertyField(description);
            EditorGUILayout.PropertyField(icon);
            EditorGUILayout.PropertyField(type);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stats & Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(restorationValue);
            EditorGUILayout.PropertyField(sanityRestorationValue);
            EditorGUILayout.PropertyField(waterRestorationValue);
            EditorGUILayout.PropertyField(maxStackSize);
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

            if (type.enumValueIndex == (int)ItemType.Artifact)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(artifactConfig, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

