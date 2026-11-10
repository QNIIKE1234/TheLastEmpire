using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public enum ShiftAbilityType
    {
        AdrenalineRush,    // Vanguard: Boosts speed, attack speed, damage resistance
        UnstoppableCharge, // Juggernaut: Charges forward, deflects bullets, stuns enemies
        ShadowStep,        // Phantom: Phasing through enemies, stealth, guaranteed crit
        EagleEye,          // Marksman: Wide tactical zoom, doubled range, piercing bullets
        GrapplingHook,     // Scavenger: Fires grappling wire forward, pulling player to buildings/structures
        CycloneSwing,      // Rebel: Rapid 360 whirlwind bat spin, deflects bullets, AOE knockback and damage reduction
        SatchelVault       // Demolitionist: Plants C4 satchel mine and vaults backwards with I-Frames
    }

    [Serializable]
    public struct StartingAmmoConfig
    {
        [Tooltip("Exact item name of ammo in inventory database (e.g. Medium Ammo, Light Ammo, Shotgun Ammo, Bolts).")]
        public string ammoType;
        [Tooltip("Number of rounds or ammo items granted on game start.")]
        public int count;

        public StartingAmmoConfig(string ammoType, int count)
        {
            this.ammoType = ammoType;
            this.count = count;
        }
    }

    [Serializable]
    public struct StartingItemConfig
    {
        [Tooltip("Item name from item database (e.g. Bandage, Medkit).")]
        public string itemName;
        [Tooltip("Quantity of items granted.")]
        public int count;

        public StartingItemConfig(string itemName, int count)
        {
            this.itemName = itemName;
            this.count = count;
        }
    }

    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "The Last Empire/Character Data", order = 1)]
    public class CharacterData : ScriptableObject
    {
        [Header("=== IDENTITY & LORE ===")]
        [Tooltip("Unique uppercase identifier for system lookups (e.g. VANGUARD, JUGGERNAUT).")]
        public string characterId = "VANGUARD";

        [Tooltip("Display name shown in UI and Dossier header.")]
        public string characterName = "The Vanguard";

        [Tooltip("Military or Wasteland title (e.g. Imperial Vanguard).")]
        public string characterTitle = "Imperial Vanguard";

        [Tooltip("Class category / tactical role.")]
        public string roleCategory = "All-Rounder / Frontline";

        [Tooltip("Full-body illustration or portrait displayed in operative selection screen.")]
        public Sprite characterPortrait;

        [Tooltip("Optional 3D character model prefab. If assigned, replaces the default player mesh with this model.")]
        public GameObject characterModelPrefab;

        [TextArea(3, 5)]
        [Tooltip("Background biography and operative dossier text.")]
        public string loreSynopsis = "A battle-tested soldier of the imperial guard. Awakens in the garrison ruins with his unit's oath echoing in his mind.";

        [TextArea(2, 4)]
        [Tooltip("First awakening thought / amnesia monologue.")]
        public string amnesiaWakingClue = "\"My head hurts... Where is the squad? My rifle's still primed. I have to reach the inner citadel before everything collapses.\"";

        [Tooltip("Color accent used for UI themes, borders, and badges.")]
        public Color themeColor = new Color(0.95f, 0.55f, 0.15f, 1f);

        [Header("=== BASE ATTRIBUTES & COMBAT ===")]
        [Range(10f, 500f)]
        public float maxHealth = 100f;

        [Range(10f, 300f)]
        public float maxHunger = 100f;

        [Range(10f, 300f)]
        public float maxWater = 100f;

        [Range(10f, 300f)]
        public float maxSanity = 100f;

        [Range(0.1f, 3.0f)]
        [Tooltip("ตัวคูณอัตราการลดของค่าสติโดยรวม (1.0 = ปกติ, 0.7 = จิตใจแข็งแกร่งสติลดช้าลง, 1.3 = หวาดระแวงง่ายสติลดเร็ว)")]
        public float sanityDepletionMultiplier = 1.0f;

        [Range(0.01f, 0.50f)]
        [Tooltip("เปอร์เซ็นต์ของความเสียหายที่ได้รับที่จะถูกหักออกจากค่าสติ (0.10 = 10% ของดาเมจที่ได้รับ)")]
        public float sanityDamageTakenPercent = 0.10f;

        [Range(0.05f, 2.0f)]
        [Tooltip("อัตราการลดของค่าสติต่อวินาทีในตอนกลางคืน")]
        public float nightSanityDepletionRate = 0.5f;

        [Range(0.05f, 2.0f)]
        [Tooltip("อัตราการฟื้นฟูค่าสติต่อวินาทีเมื่ออยู่อย่างปลอดภัยในตอนกลางวัน")]
        public float daySanityRecoveryRate = 0.2f;

        [Tooltip("Base damage added to melee attacks.")]
        public int baseMeleeDamage = 10;

        [Tooltip("Base damage added to ranged attacks.")]
        public int baseRangedDamage = 10;

        [Range(0f, 1f)]
        [Tooltip("Passive critical strike chance bonus (e.g. 0.25 = +25% crit chance).")]
        public float critChanceBonus = 0f;

        [Range(0f, 3f)]
        [Tooltip("Passive critical damage multiplier bonus (added to base crit multiplier).")]
        public float critMultiplierBonus = 0f;

        [Range(0f, 0.9f)]
        [Tooltip("Permanent passive damage reduction (0.15 = 15% armor reduction, e.g. Juggernaut).")]
        public float damageReductionPercent = 0f;

        [Header("=== MOVEMENT & MOBILITY ===")]
        [Range(1f, 20f)]
        [Tooltip("Base walking speed in m/s.")]
        public float moveSpeed = 5.0f;

        [Range(1f, 3f)]
        [Tooltip("Sprint speed multiplier applied when holding Shift while moving.")]
        public float sprintSpeedMultiplier = 1.5f;

        [Range(0f, 3f)]
        [Tooltip("Duration in seconds that sprinting is locked after firing a gun (forces walk speed).")]
        public float shootSprintLockDuration = 1.0f;

        [Range(5f, 40f)]
        [Tooltip("Dash velocity in m/s (Spacebar).")]
        public float dashSpeed = 15f;

        [Range(0.05f, 1f)]
        [Tooltip("Dash duration in seconds.")]
        public float dashDuration = 0.25f;

        [Range(0.1f, 5f)]
        [Tooltip("Dash cooldown time in seconds.")]
        public float dashCooldown = 0.8f;

        [Header("=== HUNGER & SURVIVAL ===")]
        [Range(0.01f, 5f)]
        [Tooltip("Base hunger depletion rate per second while walking or idle.")]
        public float baseHungerDepletionRate = 0.5f;

        [Range(1f, 10f)]
        [Tooltip("Hunger consumption multiplier when sprinting (e.g. 4.0 = 4x base rate).")]
        public float sprintHungerMultiplier = 4.0f;

        [Range(0.1f, 5f)]
        [Tooltip("Global hunger consumption multiplier for this character (1.0 = normal, 0.85 = slower, 1.2 = faster).")]
        public float hungerConsumptionMultiplier = 1.0f;

        [Range(0.1f, 5f)]
        [Tooltip("Global water consumption multiplier for this character (1.0 = normal, 0.85 = slower, 1.2 = faster).")]
        public float waterConsumptionMultiplier = 1.0f;

        [Range(1f, 50f)]
        [Tooltip("Damage dealt per starvation tick when hunger reaches 0.")]
        public float starvationDamage = 5.0f;

        [Range(0.5f, 10f)]
        [Tooltip("Interval in seconds between starvation damage ticks.")]
        public float starvationInterval = 2.0f;

        [Range(0f, 50f)]
        [Tooltip("Bonus HP healed whenever consuming food/bread (Scavenger perk: +10 HP).")]
        public float foodHealBonus = 0f;

        [Header("=== INVENTORY & STORAGE ===")]
        [Range(18, 30)]
        [Tooltip("ความจุช่องเก็บของในกระเป๋าสำหรับตัวละครนี้ (ขั้นต่ำ 18 ช่อง, มากสุด 30 ช่อง)")]
        public int maxInventorySlots = 20;

        [Header("=== STARTING ARSENAL & LOADOUT ===")]
        [Tooltip("Starting credits / gold in player pouch.")]
        public int startingMoney = 100;

        [Tooltip("Starting ration bread count given upon spawn.")]
        public int startingRations = 2;

        [Tooltip("List of weapon names added to inventory & arsenal on start (e.g. Rifle, Knife).")]
        public List<string> startingWeapons = new List<string> { "Rifle", "Knife" };

        [Tooltip("Starting ammo stacks granted upon game start.")]
        public List<StartingAmmoConfig> startingAmmo = new List<StartingAmmoConfig>();

        [Tooltip("Additional items to grant in starting inventory.")]
        public List<StartingItemConfig> startingExtraItems = new List<StartingItemConfig>();

        [Tooltip("Legacy pistol ammo count - used as fallback if startingAmmo is empty.")]
        public int startingPistolAmmo = 60;

        [Range(0f, 2f)]
        [Tooltip("Bonus loot drop rate (e.g. 0.25 = +25% drop rate for Scavenger).")]
        public float dropRateBonus = 0f;

        [Range(0.5f, 5f)]
        [Tooltip("Money pickup multiplier (e.g. 1.30 = +30% Gold for Scavenger).")]
        public float moneyMultiplier = 1.0f;

        [Header("=== SIGNATURE SHIFT ACTIVE ABILITY ===")]
        public ShiftAbilityType shiftAbilityType = ShiftAbilityType.AdrenalineRush;
        public string shiftAbilityName = "Adrenaline Rush";
        [TextArea(2, 4)]
        public string shiftAbilityDescription = "Hold [Shift] to surge adrenaline: +75% move speed, +30% attack speed, 20% damage reduction. (Consumes hunger faster)";
        [Tooltip("Cooldown in seconds for active burst skills (0 for toggle/hold channeling).")]
        public float shiftCooldown = 0f;

        [Header("--- Ability Tuning: Adrenaline Rush (Vanguard) ---")]
        [Range(1f, 3f)] public float adrenalineSpeedMultiplier = 1.75f;
        [Range(0f, 1f)] public float adrenalineAttackSpeedBonus = 0.30f;
        [Range(0f, 0.9f)] public float adrenalineDamageReduction = 0.20f;
        [Range(0.1f, 10f)] public float adrenalineHungerDrainRate = 1.5f;

        [Header("--- Ability Tuning: Unstoppable Charge (Juggernaut) ---")]
        [Range(5f, 35f)] public float chargeSpeed = 18f;
        [Range(0.1f, 2f)] public float chargeDuration = 0.35f;
        [Range(5f, 100f)] public float chargeDamage = 25f;
        [Range(0.1f, 5f)] public float chargeStunDuration = 1.0f;
        [Range(0f, 50f)] public float chargeHungerCost = 5.0f;

        [Header("--- Ability Tuning: Shadow Step (Phantom) ---")]
        [Range(1f, 3f)] public float shadowSpeedMultiplier = 1.40f;
        [Range(0.1f, 10f)] public float shadowHungerDrainRate = 2.5f;
        [Range(1f, 5f)] public float shadowExitCritMultiplier = 2.0f;

        [Header("--- Ability Tuning: Eagle Eye (Marksman) ---")]
        [Range(1.1f, 2.5f)] public float eagleEyeZoomMultiplier = 1.30f;
        [Range(1f, 4f)] public float eagleEyeRangeMultiplier = 2.0f;
        [Range(0.1f, 2f)] public float eagleEyeMoveSpeedMultiplier = 0.35f;
        [Range(0.1f, 5f)] public float eagleEyeHungerDrainRate = 0.8f;

        [Header("--- Ability Tuning: Grappling Hook (Scavenger) ---")]
        [Range(5f, 40f)] public float grappleRange = 18f;
        [Range(10f, 40f)] public float grapplePullSpeed = 22f;
        [Range(0f, 20f)] public float grappleHungerCost = 3.5f;

        [Header("--- Ability Tuning: Cyclone Swing (Rebel) ---")]
        [Range(0.5f, 3.0f)] public float cycloneDuration = 1.2f;
        [Range(5f, 80f)] public float cycloneDamagePerHit = 22f;
        [Range(0.1f, 0.5f)] public float cycloneHitInterval = 0.25f;
        [Range(1f, 5f)] public float cycloneRadius = 2.4f;
        [Range(5f, 30f)] public float cycloneKnockback = 12f;
        [Range(0f, 0.9f)] public float cycloneDamageReduction = 0.40f;
        [Range(1f, 2.5f)] public float cycloneMoveSpeedMultiplier = 1.30f;
        [Range(0f, 20f)] public float cycloneHungerCost = 4.0f;

        [Header("--- Passive Tuning: Adrenaline Mania (Rebel) ---")]
        [Range(0.1f, 0.9f)] public float rebelFrenzyHpThreshold = 0.50f;
        [Range(0f, 1f)] public float rebelFrenzyAttackSpeedBonus = 0.35f;
        [Range(0f, 1f)] public float rebelFrenzyCritBonus = 0.25f;
        [Range(0f, 20f)] public float rebelMeleeKillHungerRestore = 3.0f;

        [Header("--- Ability Tuning: Satchel Vault (Demolitionist) ---")]
        [Range(10f, 150f)] public float satchelDamage = 65f;
        [Range(1f, 10f)] public float satchelBlastRadius = 4.5f;
        [Range(0.2f, 3.0f)] public float satchelStunDuration = 1.2f;
        [Range(0.1f, 2.0f)] public float satchelArmDelay = 0.3f;
        [Range(1.0f, 6.0f)] public float satchelAutoDetonateTime = 2.5f;
        [Range(1f, 10f)] public float vaultDistance = 4.5f;
        [Range(5f, 30f)] public float vaultSpeed = 16f;
        [Range(0f, 20f)] public float satchelHungerCost = 5.0f;

        [Header("--- Passive Tuning: Blast Shield and Ordnance (Demolitionist) ---")]
        [Range(0f, 1f)] public float explosiveDamageBonus = 0.20f;
        [Range(0f, 1f)] public float explosiveRadiusBonus = 0.30f;
        [Range(0f, 0.9f)] public float blastDamageReduction = 0.60f;

        [HideInInspector] public float toxicSlowPercent = 0.50f;
        [HideInInspector] public float toxicPoisonDps = 8f;
        [HideInInspector] public float toxicCloudDuration = 3.5f;
        [HideInInspector] public float toxicCloudRadius = 1.6f;
        [HideInInspector] public float toxicHungerDrainRate = 1.8f;

        /// <summary>
        /// Generates a clean, formatted loadout string for UI presentation.
        /// </summary>
        public string GetFormattedLoadoutString()
        {
            string wpns = (startingWeapons != null && startingWeapons.Count > 0) ? string.Join(", ", startingWeapons) : "Knife";
            string ammoDesc = "";
            if (startingAmmo != null && startingAmmo.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (var a in startingAmmo)
                {
                    if (a.count > 0 && !string.IsNullOrEmpty(a.ammoType))
                    {
                        parts.Add($"{a.ammoType}: {a.count}");
                    }
                }
                ammoDesc = string.Join(" | ", parts);
            }

            if (string.IsNullOrEmpty(ammoDesc))
            {
                ammoDesc = startingPistolAmmo > 0 ? $"{startingPistolAmmo} rounds" : "None";
            }

            string rationStr = startingRations > 0 ? $"{startingRations}x Bread" : "None";
            return $"- Primary Weapon: <b>{wpns}</b>\n- Starting Ammo: <b>{ammoDesc}</b> | Ration: <b>{rationStr}</b> | Credits: <b>{startingMoney} Gold</b>";
        }

        /// <summary>
        /// Generates a clean, formatted attributes summary string for UI presentation.
        /// </summary>
        public string GetFormattedAttributesString()
        {
            return $"- Max HP: <b>{maxHealth}</b> | Hunger: <b>{maxHunger}</b> | Water: <b>{maxWater}</b> | Sanity: <b>{maxSanity}</b>\n- Backpack: <b>{maxInventorySlots} Slots</b> | Move Speed: <b>{moveSpeed:F1} m/s</b> (Sprint: <b>{sprintSpeedMultiplier:F1}x</b>)\n- Dash Speed: <b>{dashSpeed:F0} m/s</b> ({dashCooldown:F1}s CD)";
        }
    }
}
