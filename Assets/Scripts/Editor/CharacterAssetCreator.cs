using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class CharacterAssetCreator
    {
        private const string CharDir = "Assets/Resources/Database/Characters";
        private const string DbDir = "Assets/Resources/Database";
        private const string DatabaseAssetPath = "Assets/Resources/Database/CharacterDatabase.asset";

        static CharacterAssetCreator()
        {
            EditorApplication.delayCall += EnsureAllCharacterAssetsCreated;
        }

        [MenuItem("The Last Empire/Generate 5 Playable Character Assets", priority = 2)]
        public static void EnsureAllCharacterAssetsCreated()
        {
            if (!Directory.Exists(CharDir))
            {
                Directory.CreateDirectory(CharDir);
                AssetDatabase.Refresh();
            }

            List<CharacterData> createdList = new List<CharacterData>();

            // 1. Vanguard
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_01_Vanguard", "VANGUARD", "The Vanguard", "Imperial Vanguard",
                "All-Rounder / Frontline",
                "Battle-hardened frontline veteran. Balanced in all combat ranges. Awakens in the garrison ruins with his unit's oath echoing in his mind.",
                "\"My head is pounding... Where is the squad? My rifle's still primed. I have to reach the inner citadel before everything collapses.\"",
                new Color(0.95f, 0.55f, 0.15f, 1f),
                100f, 100f, 5.0f, 10, 10, 1.0f,
                15f, 0.25f, 0.8f, 0f, 100, 60,
                new List<string> { "Rifle", "Knife" }, 0f, 1.0f,
                ShiftAbilityType.AdrenalineRush, "Adrenaline Rush",
                "Hold [Shift] to surge adrenaline: +75% move speed, +30% attack speed, 20% damage reduction. (Consumes hunger faster)",
                0f
            ));

            // 2. Juggernaut
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_02_Juggernaut", "JUGGERNAUT", "The Juggernaut", "Ironbound Behemoth",
                "Melee Brawler / Tank",
                "Genetically modified steel juggernaut. Awakens in the ruins of an underground foundry, fueled by unyielding vengeance against the Empire.",
                "\"The chains are shattered... What did they do to my flesh? I feel no pain anymore. I will smash through anything in my path.\"",
                new Color(0.92f, 0.22f, 0.22f, 1f),
                150f, 120f, 4.3f, 15, 8, 1.2f,
                12f, 0.25f, 1.0f, 0.15f, 50, 20,
                new List<string> { "Sledgehammer", "Shotgun" }, 0f, 1.0f,
                ShiftAbilityType.UnstoppableCharge, "Unstoppable Charge",
                "Press [Shift] to charge forward: deals 25 impact damage + 1s stun, deflects all incoming bullets. 5s CD. (Passive: 15% damage reduction at all times)",
                5.0f
            ));

            // 3. Phantom
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_03_Phantom", "PHANTOM", "The Phantom", "Shadow Assassin",
                "Hit-and-Run / Assassin",
                "Stealth dimensional assassin. Awakens in the shadows of a ruined alleyway with an encrypted mission to eliminate high-value targets.",
                "\"My target... Where is the doctor? Why did the Empire want him silenced? There are dark secrets buried beneath this ruins.\"",
                new Color(0.66f, 0.33f, 0.97f, 1f),
                75f, 90f, 6.0f, 12, 10, 1.0f,
                18f, 0.30f, 0.55f, 0f, 150, 40,
                new List<string> { "Katana", "Pistol" }, 0f, 1.0f,
                ShiftAbilityType.ShadowStep, "Shadow Step",
                "Hold [Shift] to enter shadow phase: +40% speed, pass through enemies freely. Exiting shadow guarantees 2.0x Critical Strike on your next attack.",
                0f
            ));

            // 4. Marksman
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_04_Marksman", "MARKSMAN", "The Marksman", "Apex Sharpshooter",
                "Long-Range / Tactical",
                "Eagle-eyed sharpshooter. Awakens atop an abandoned observation tower, vividly recalling the moment the reactor detonated through his scope.",
                "\"Through my scope that day... The one who flipped the switch wasn't an invader. It was an Imperial officer! I will uncover the betrayal.\"",
                new Color(0.0f, 0.75f, 0.95f, 1f),
                85f, 100f, 4.8f, 7, 14, 1.0f,
                14f, 0.25f, 0.8f, 0f, 80, 30,
                new List<string> { "Sniper Rifle", "Revolver" }, 0f, 1.0f,
                ShiftAbilityType.EagleEye, "Eagle Eye",
                "Hold [Shift] to enter tactical focus: 1.35x sniper zoom, 2x attack range, bullets pierce through all enemies. (Move slower while aiming)",
                0f
            ));

            // 5. Scavenger
            createdList.Add(CreateOrUpdateCharacter(
                "CHAR_05_Scavenger", "SCAVENGER", "The Scavenger", "Wasteland Nomad",
                "Survivalist / Trapper",
                "Hooded wasteland nomad who slipped past city walls scavenging for survival rations, now trapped amidst the fallen ruins.",
                "\"My stomach is burning with hunger... Need to find bread or scraps first. Forget the Empire, I just need to blast the city gates open and survive.\"",
                new Color(0.25f, 0.85f, 0.45f, 1f),
                90f, 110f, 5.2f, 10, 10, 0.85f,
                16f, 0.25f, 0.75f, 0f, 300, 0,
                new List<string> { "Crossbow", "Survival Spear" }, 0.25f, 1.30f,
                ShiftAbilityType.ToxicTrail, "Toxic Trail",
                "Hold [Shift] to sprint and release toxic smoke clouds: enemies are slowed by 50% and suffer 8 poison DPS. (Passive: eating food grants +10 bonus HP)",
                0f
            ));

            // Create or update CharacterDatabase.asset
            CharacterDatabase db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabaseAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            }

            db.Characters.Clear();
            foreach (var ch in createdList)
            {
                if (ch != null && !db.Characters.Contains(ch))
                {
                    db.Characters.Add(ch);
                }
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#50FA7B>[CharacterAssetCreator]</color> Successfully initialized 5 playable characters in: {DatabaseAssetPath}");
        }

        private static CharacterData CreateOrUpdateCharacter(
            string assetName, string id, string name, string title, string role,
            string lore, string clue, Color theme,
            float hp, float hunger, float speed, int meleeDmg, int rangedDmg, float hungerMultiplier,
            float dashSpeed, float dashDuration, float dashCooldown, float dmgReduction, int money, int pistolAmmo,
            List<string> weapons, float dropBonus, float moneyMult,
            ShiftAbilityType shiftType, string shiftName, string shiftDesc, float shiftCd)
        {
            string path = $"{CharDir}/{assetName}.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.characterId = id;
            data.characterName = name;
            data.characterTitle = title;
            data.roleCategory = role;
            data.loreSynopsis = lore;
            data.amnesiaWakingClue = clue;
            data.themeColor = theme;

            data.maxHealth = hp;
            data.maxHunger = hunger;
            data.moveSpeed = speed;
            data.baseMeleeDamage = meleeDmg;
            data.baseRangedDamage = rangedDmg;
            data.hungerConsumptionMultiplier = hungerMultiplier;

            data.dashSpeed = dashSpeed;
            data.dashDuration = dashDuration;
            data.dashCooldown = dashCooldown;
            data.damageReductionPercent = dmgReduction;

            data.startingMoney = money;
            data.startingPistolAmmo = pistolAmmo;
            data.startingWeapons = weapons;
            data.dropRateBonus = dropBonus;
            data.moneyMultiplier = moneyMult;

            data.shiftAbilityType = shiftType;
            data.shiftAbilityName = shiftName;
            data.shiftAbilityDescription = shiftDesc;
            data.shiftCooldown = shiftCd;

            // Preserve existing portrait or assign Vanguard default artwork if unassigned
            if (data.characterPortrait == null && id.Equals("VANGUARD", System.StringComparison.OrdinalIgnoreCase))
            {
                Sprite vanguardSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Image/Picture/ChatGPT Image Oct 23, 2026, 10_07_54 PM.png");
                if (vanguardSpr != null)
                {
                    data.characterPortrait = vanguardSpr;
                }
            }

            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
