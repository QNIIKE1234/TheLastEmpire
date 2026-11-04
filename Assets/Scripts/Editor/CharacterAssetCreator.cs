using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Utility for creating and maintaining the 5 playable character ScriptableObject assets.
    /// NOTE: Does NOT auto-overwrite user edits on domain reload!
    /// </summary>
    public static class CharacterAssetCreator
    {
        private const string CharDir = "Assets/Resources/Database/Characters";
        private const string DbDir = "Assets/Resources/Database";
        private const string DatabaseAssetPath = "Assets/Resources/Database/CharacterDatabase.asset";

        [MenuItem("The Last Empire/Characters/Ensure Missing Character Assets Exist (Safe)", priority = 1)]
        public static void EnsureMissingCharacterAssetsExist()
        {
            CreateOrUpdateRoster(false);
        }

        [MenuItem("The Last Empire/Characters/Reset All 5 Characters to Default Config (Warning: Overwrites SO)", priority = 2)]
        public static void ForceResetAllCharactersToDefault()
        {
            if (EditorUtility.DisplayDialog(
                "Reset Character ScriptableObjects",
                "Are you sure you want to reset all 5 playable character configs to default? Any custom Inspector edits on these 5 CharacterData assets will be overwritten.",
                "Yes, Reset All", "Cancel"))
            {
                CreateOrUpdateRoster(true);
            }
        }

        public static void CreateOrUpdateRoster(bool forceOverwrite)
        {
            if (!Directory.Exists(CharDir))
            {
                Directory.CreateDirectory(CharDir);
                AssetDatabase.Refresh();
            }

            List<CharacterData> roster = new List<CharacterData>();

            // 1. Vanguard
            roster.Add(GetOrCreateVanguard(forceOverwrite));

            // 2. Juggernaut
            roster.Add(GetOrCreateJuggernaut(forceOverwrite));

            // 3. Phantom
            roster.Add(GetOrCreatePhantom(forceOverwrite));

            // 4. Marksman
            roster.Add(GetOrCreateMarksman(forceOverwrite));

            // 5. Scavenger
            roster.Add(GetOrCreateScavenger(forceOverwrite));

            // Update CharacterDatabase.asset references
            CharacterDatabase db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabaseAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            }

            db.Characters.Clear();
            foreach (var ch in roster)
            {
                if (ch != null && !db.Characters.Contains(ch))
                {
                    db.Characters.Add(ch);
                }
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string mode = forceOverwrite ? "Reset to defaults" : "Preserved existing assets & verified references";
            Debug.Log($"<color=#50FA7B>[CharacterAssetCreator]</color> Successfully processed 5 playable characters ({mode}) in: {DatabaseAssetPath}");
        }

        private static CharacterData GetOrCreateVanguard(bool forceOverwrite)
        {
            string path = $"{CharDir}/CHAR_01_Vanguard.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            bool isNew = (data == null);

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            else if (!forceOverwrite)
            {
                // Ensure startingAmmo and new fields have sensible defaults if missing
                if (data.startingAmmo == null || data.startingAmmo.Count == 0)
                {
                    data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Medium Ammo", 60) };
                    if (data.startingRations == 0) data.startingRations = 2;
                    EditorUtility.SetDirty(data);
                }
                return data;
            }

            data.characterId = "VANGUARD";
            data.characterName = "The Vanguard";
            data.characterTitle = "Imperial Vanguard";
            data.roleCategory = "All-Rounder / Frontline";
            data.loreSynopsis = "Battle-hardened frontline veteran. Balanced in all combat ranges. Awakens in the garrison ruins with his unit's oath echoing in his mind.";
            data.amnesiaWakingClue = "\"My head is pounding... Where is the squad? My rifle's still primed. I have to reach the inner citadel before everything collapses.\"";
            data.themeColor = new Color(0.95f, 0.55f, 0.15f, 1f);

            data.maxHealth = 100f;
            data.maxHunger = 100f;
            data.baseMeleeDamage = 10;
            data.baseRangedDamage = 10;
            data.critChanceBonus = 0f;
            data.critMultiplierBonus = 0f;
            data.damageReductionPercent = 0f;

            data.moveSpeed = 5.0f;
            data.sprintSpeedMultiplier = 1.5f;
            data.shootSprintLockDuration = 1.0f;
            data.dashSpeed = 15f;
            data.dashDuration = 0.25f;
            data.dashCooldown = 0.8f;

            data.baseHungerDepletionRate = 0.5f;
            data.sprintHungerMultiplier = 4.0f;
            data.hungerConsumptionMultiplier = 1.0f;
            data.starvationDamage = 5.0f;
            data.starvationInterval = 2.0f;
            data.foodHealBonus = 0f;

            data.startingMoney = 100;
            data.startingRations = 2;
            data.startingPistolAmmo = 60;
            data.startingWeapons = new List<string> { "Rifle", "Knife" };
            data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Medium Ammo", 60) };
            data.dropRateBonus = 0f;
            data.moneyMultiplier = 1.0f;

            data.shiftAbilityType = ShiftAbilityType.AdrenalineRush;
            data.shiftAbilityName = "Adrenaline Rush";
            data.shiftAbilityDescription = "Hold [Shift] to surge adrenaline: +75% move speed, +30% attack speed, 20% damage reduction. (Consumes hunger faster)";
            data.shiftCooldown = 0f;

            data.adrenalineSpeedMultiplier = 1.75f;
            data.adrenalineAttackSpeedBonus = 0.30f;
            data.adrenalineDamageReduction = 0.20f;
            data.adrenalineHungerDrainRate = 1.5f;

            if (data.characterPortrait == null)
            {
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Image/Picture/ChatGPT Image Oct 23, 2026, 10_07_54 PM.png");
                if (sp != null) data.characterPortrait = sp;
            }

            EditorUtility.SetDirty(data);
            return data;
        }

        private static CharacterData GetOrCreateJuggernaut(bool forceOverwrite)
        {
            string path = $"{CharDir}/CHAR_02_Juggernaut.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            bool isNew = (data == null);

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            else if (!forceOverwrite)
            {
                if (data.startingAmmo == null || data.startingAmmo.Count == 0)
                {
                    data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Shotgun Ammo", 20) };
                    if (data.startingRations == 0) data.startingRations = 2;
                    EditorUtility.SetDirty(data);
                }
                return data;
            }

            data.characterId = "JUGGERNAUT";
            data.characterName = "The Juggernaut";
            data.characterTitle = "Ironbound Behemoth";
            data.roleCategory = "Melee Brawler / Tank";
            data.loreSynopsis = "Genetically modified steel juggernaut. Awakens in the ruins of an underground foundry, fueled by unyielding vengeance against the Empire.";
            data.amnesiaWakingClue = "\"The chains are shattered... What did they do to my flesh? I feel no pain anymore. I will smash through anything in my path.\"";
            data.themeColor = new Color(0.92f, 0.22f, 0.22f, 1f);

            data.maxHealth = 150f;
            data.maxHunger = 120f;
            data.baseMeleeDamage = 15;
            data.baseRangedDamage = 8;
            data.critChanceBonus = 0f;
            data.critMultiplierBonus = 0f;
            data.damageReductionPercent = 0.15f; // Passive 15% armor

            data.moveSpeed = 4.3f;
            data.sprintSpeedMultiplier = 1.4f;
            data.shootSprintLockDuration = 1.0f;
            data.dashSpeed = 12f;
            data.dashDuration = 0.25f;
            data.dashCooldown = 1.0f;

            data.baseHungerDepletionRate = 0.5f;
            data.sprintHungerMultiplier = 4.0f;
            data.hungerConsumptionMultiplier = 1.2f;
            data.starvationDamage = 5.0f;
            data.starvationInterval = 2.0f;
            data.foodHealBonus = 0f;

            data.startingMoney = 50;
            data.startingRations = 2;
            data.startingPistolAmmo = 20;
            data.startingWeapons = new List<string> { "Sledgehammer", "Shotgun" };
            data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Shotgun Ammo", 20) };
            data.dropRateBonus = 0f;
            data.moneyMultiplier = 1.0f;

            data.shiftAbilityType = ShiftAbilityType.UnstoppableCharge;
            data.shiftAbilityName = "Unstoppable Charge";
            data.shiftAbilityDescription = "Press [Shift] to charge forward: deals 25 impact damage + 1s stun, deflects all incoming bullets. 5s CD. (Passive: 15% damage reduction at all times)";
            data.shiftCooldown = 5.0f;

            data.chargeSpeed = 18f;
            data.chargeDuration = 0.35f;
            data.chargeDamage = 25f;
            data.chargeStunDuration = 1.0f;
            data.chargeHungerCost = 5.0f;

            EditorUtility.SetDirty(data);
            return data;
        }

        private static CharacterData GetOrCreatePhantom(bool forceOverwrite)
        {
            string path = $"{CharDir}/CHAR_03_Phantom.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            bool isNew = (data == null);

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            else if (!forceOverwrite)
            {
                if (data.startingAmmo == null || data.startingAmmo.Count == 0)
                {
                    data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Light Ammo", 40) };
                    if (data.startingRations == 0) data.startingRations = 2;
                    EditorUtility.SetDirty(data);
                }
                return data;
            }

            data.characterId = "PHANTOM";
            data.characterName = "The Phantom";
            data.characterTitle = "Shadow Assassin";
            data.roleCategory = "Hit-and-Run / Assassin";
            data.loreSynopsis = "Stealth dimensional assassin. Awakens in the shadows of a ruined alleyway with an encrypted mission to eliminate high-value targets.";
            data.amnesiaWakingClue = "\"My target... Where is the doctor? Why did the Empire want him silenced? There are dark secrets buried beneath this ruins.\"";
            data.themeColor = new Color(0.66f, 0.33f, 0.97f, 1f);

            data.maxHealth = 75f;
            data.maxHunger = 90f;
            data.baseMeleeDamage = 12;
            data.baseRangedDamage = 10;
            data.critChanceBonus = 0f;
            data.critMultiplierBonus = 0.5f;
            data.damageReductionPercent = 0f;

            data.moveSpeed = 6.0f;
            data.sprintSpeedMultiplier = 1.6f;
            data.shootSprintLockDuration = 1.0f;
            data.dashSpeed = 18f;
            data.dashDuration = 0.30f;
            data.dashCooldown = 0.55f;

            data.baseHungerDepletionRate = 0.5f;
            data.sprintHungerMultiplier = 4.0f;
            data.hungerConsumptionMultiplier = 1.0f;
            data.starvationDamage = 5.0f;
            data.starvationInterval = 2.0f;
            data.foodHealBonus = 0f;

            data.startingMoney = 150;
            data.startingRations = 2;
            data.startingPistolAmmo = 40;
            data.startingWeapons = new List<string> { "Katana", "Pistol" };
            data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Light Ammo", 40) };
            data.dropRateBonus = 0f;
            data.moneyMultiplier = 1.0f;

            data.shiftAbilityType = ShiftAbilityType.ShadowStep;
            data.shiftAbilityName = "Shadow Step";
            data.shiftAbilityDescription = "Hold [Shift] to enter shadow phase: +40% speed, pass through enemies freely. Exiting shadow guarantees 2.0x Critical Strike on your next attack.";
            data.shiftCooldown = 0f;

            data.shadowSpeedMultiplier = 1.40f;
            data.shadowHungerDrainRate = 2.5f;
            data.shadowExitCritMultiplier = 2.0f;

            EditorUtility.SetDirty(data);
            return data;
        }

        private static CharacterData GetOrCreateMarksman(bool forceOverwrite)
        {
            string path = $"{CharDir}/CHAR_04_Marksman.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            bool isNew = (data == null);

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            else if (!forceOverwrite)
            {
                if (data.startingAmmo == null || data.startingAmmo.Count == 0)
                {
                    data.startingAmmo = new List<StartingAmmoConfig>
                    {
                        new StartingAmmoConfig("Medium Ammo", 20),
                        new StartingAmmoConfig("Light Ammo", 20)
                    };
                    if (data.startingRations == 0) data.startingRations = 2;
                    EditorUtility.SetDirty(data);
                }
                return data;
            }

            data.characterId = "MARKSMAN";
            data.characterName = "The Marksman";
            data.characterTitle = "Apex Sharpshooter";
            data.roleCategory = "Long-Range / Tactical";
            data.loreSynopsis = "Eagle-eyed sharpshooter. Awakens atop an abandoned observation tower, vividly recalling the moment the reactor detonated through his scope.";
            data.amnesiaWakingClue = "\"Through my scope that day... The one who flipped the switch wasn't an invader. It was an Imperial officer! I will uncover the betrayal.\"";
            data.themeColor = new Color(0.0f, 0.75f, 0.95f, 1f);

            data.maxHealth = 85f;
            data.maxHunger = 100f;
            data.baseMeleeDamage = 7;
            data.baseRangedDamage = 14;
            data.critChanceBonus = 0.25f; // Marksman 35% base crit (10% + 25%)
            data.critMultiplierBonus = 0.5f; // +0.5x crit multiplier
            data.damageReductionPercent = 0f;

            data.moveSpeed = 4.8f;
            data.sprintSpeedMultiplier = 1.5f;
            data.shootSprintLockDuration = 1.0f;
            data.dashSpeed = 14f;
            data.dashDuration = 0.25f;
            data.dashCooldown = 0.8f;

            data.baseHungerDepletionRate = 0.5f;
            data.sprintHungerMultiplier = 4.0f;
            data.hungerConsumptionMultiplier = 1.0f;
            data.starvationDamage = 5.0f;
            data.starvationInterval = 2.0f;
            data.foodHealBonus = 0f;

            data.startingMoney = 80;
            data.startingRations = 2;
            data.startingPistolAmmo = 30;
            data.startingWeapons = new List<string> { "Sniper Rifle", "Revolver" };
            data.startingAmmo = new List<StartingAmmoConfig>
            {
                new StartingAmmoConfig("Medium Ammo", 20),
                new StartingAmmoConfig("Light Ammo", 20)
            };
            data.dropRateBonus = 0f;
            data.moneyMultiplier = 1.0f;

            data.shiftAbilityType = ShiftAbilityType.EagleEye;
            data.shiftAbilityName = "Eagle Eye";
            data.shiftAbilityDescription = "Hold [Shift] to enter tactical focus: 1.35x sniper zoom, 2x attack range, bullets pierce through all enemies. (Move slower while aiming)";
            data.shiftCooldown = 0f;

            data.eagleEyeZoomMultiplier = 1.30f;
            data.eagleEyeRangeMultiplier = 2.0f;
            data.eagleEyeMoveSpeedMultiplier = 0.35f;
            data.eagleEyeHungerDrainRate = 0.8f;

            EditorUtility.SetDirty(data);
            return data;
        }

        private static CharacterData GetOrCreateScavenger(bool forceOverwrite)
        {
            string path = $"{CharDir}/CHAR_05_Scavenger.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            bool isNew = (data == null);

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            else if (!forceOverwrite)
            {
                if (data.startingAmmo == null || data.startingAmmo.Count == 0)
                {
                    data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Bolts", 25) };
                    if (data.startingRations == 0) data.startingRations = 2;
                    EditorUtility.SetDirty(data);
                }
                return data;
            }

            data.characterId = "SCAVENGER";
            data.characterName = "The Scavenger";
            data.characterTitle = "Wasteland Nomad";
            data.roleCategory = "Survivalist / Trapper";
            data.loreSynopsis = "Hooded wasteland nomad who slipped past city walls scavenging for survival rations, now trapped amidst the fallen ruins.";
            data.amnesiaWakingClue = "\"My stomach is burning with hunger... Need to find bread or scraps first. Forget the Empire, I just need to blast the city gates open and survive.\"";
            data.themeColor = new Color(0.25f, 0.85f, 0.45f, 1f);

            data.maxHealth = 90f;
            data.maxHunger = 110f;
            data.baseMeleeDamage = 10;
            data.baseRangedDamage = 10;
            data.critChanceBonus = 0f;
            data.critMultiplierBonus = 0f;
            data.damageReductionPercent = 0f;

            data.moveSpeed = 5.2f;
            data.sprintSpeedMultiplier = 1.5f;
            data.shootSprintLockDuration = 1.0f;
            data.dashSpeed = 16f;
            data.dashDuration = 0.25f;
            data.dashCooldown = 0.75f;

            data.baseHungerDepletionRate = 0.5f;
            data.sprintHungerMultiplier = 4.0f;
            data.hungerConsumptionMultiplier = 0.85f; // Slower hunger consumption
            data.starvationDamage = 5.0f;
            data.starvationInterval = 2.0f;
            data.foodHealBonus = 10f; // Passive: Eating food gives +10 HP

            data.startingMoney = 300;
            data.startingRations = 2;
            data.startingPistolAmmo = 0;
            data.startingWeapons = new List<string> { "Crossbow", "Survival Spear" };
            data.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Bolts", 25) };
            data.dropRateBonus = 0.25f;
            data.moneyMultiplier = 1.30f;

            data.shiftAbilityType = ShiftAbilityType.GrapplingHook;
            data.shiftAbilityName = "Grappling Hook";
            data.shiftAbilityDescription = "Press [Shift] to fire a survival grappling wire: latches onto buildings/obstacles and zips you directly to the point. (Passive: eating food grants +10 bonus HP)";
            data.shiftCooldown = 3.0f;
            data.grappleRange = 18f;
            data.grapplePullSpeed = 22f;
            data.grappleHungerCost = 3.5f;

            data.toxicSlowPercent = 0.50f;
            data.toxicPoisonDps = 8f;
            data.toxicCloudDuration = 3.5f;
            data.toxicCloudRadius = 1.6f;
            data.toxicHungerDrainRate = 1.8f;

            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
