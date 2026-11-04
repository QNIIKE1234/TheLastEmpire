using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "The Last Empire/Character Database", order = 2)]
    public class CharacterDatabase : ScriptableObject
    {
        [Header("Registered Playable Characters")]
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

        private static CharacterDatabase _cachedInstance;

        public List<CharacterData> Characters => characters;

        public static CharacterDatabase Instance
        {
            get
            {
                if (_cachedInstance == null)
                {
                    _cachedInstance = Resources.Load<CharacterDatabase>("Database/CharacterDatabase");
                    if (_cachedInstance == null)
                    {
                        var all = Resources.LoadAll<CharacterDatabase>("");
                        if (all != null && all.Length > 0)
                        {
                            _cachedInstance = all[0];
                        }
                    }
                }

                if (_cachedInstance == null || _cachedInstance.characters == null || _cachedInstance.characters.Count == 0)
                {
                    _cachedInstance = ScriptableObject.CreateInstance<CharacterDatabase>();
                    _cachedInstance.characters = CreateDefaultRoster();
                }

                return _cachedInstance;
            }
        }

        public CharacterData GetCharacter(string id)
        {
            if (string.IsNullOrEmpty(id)) return characters.Count > 0 ? characters[0] : null;

            foreach (var ch in characters)
            {
                if (ch != null && ch.characterId.Equals(id, System.StringComparison.OrdinalIgnoreCase))
                {
                    return ch;
                }
            }

            return characters.Count > 0 ? characters[0] : null;
        }

        public static List<CharacterData> CreateDefaultRoster()
        {
            List<CharacterData> roster = new List<CharacterData>();

            // 1. Vanguard
            var vanguard = ScriptableObject.CreateInstance<CharacterData>();
            vanguard.characterId = "VANGUARD";
            vanguard.characterName = "The Vanguard";
            vanguard.characterTitle = "Imperial Vanguard";
            vanguard.roleCategory = "All-Rounder / Frontline";
            vanguard.loreSynopsis = "Battle-hardened frontline veteran. Balanced in all combat ranges. Awakens in the garrison ruins with his unit's oath echoing in his mind.";
            vanguard.amnesiaWakingClue = "\"My head is pounding... Where is the squad? My rifle's still primed. I have to reach the inner citadel before everything collapses.\"";
            vanguard.themeColor = new Color(0.95f, 0.55f, 0.15f, 1f);
            vanguard.maxHealth = 100f;
            vanguard.maxHunger = 100f;
            vanguard.moveSpeed = 5.0f;
            vanguard.baseMeleeDamage = 10;
            vanguard.baseRangedDamage = 10;
            vanguard.hungerConsumptionMultiplier = 1.0f;
            vanguard.dashSpeed = 15f;
            vanguard.dashDuration = 0.25f;
            vanguard.dashCooldown = 0.8f;
            vanguard.damageReductionPercent = 0f;
            vanguard.startingMoney = 100;
            vanguard.startingRations = 2;
            vanguard.startingPistolAmmo = 60;
            vanguard.startingWeapons = new List<string> { "Rifle", "Knife" };
            vanguard.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Medium Ammo", 60) };
            vanguard.sprintSpeedMultiplier = 1.5f;
            vanguard.shootSprintLockDuration = 1.0f;
            vanguard.shiftAbilityType = ShiftAbilityType.AdrenalineRush;
            vanguard.shiftAbilityName = "Adrenaline Rush";
            vanguard.shiftAbilityDescription = "Hold [Shift] to surge adrenaline: +75% move speed, +30% attack speed, 20% damage reduction. (Consumes hunger faster)";
            vanguard.shiftCooldown = 0f;
            roster.Add(vanguard);

            // 2. Juggernaut
            var juggernaut = ScriptableObject.CreateInstance<CharacterData>();
            juggernaut.characterId = "JUGGERNAUT";
            juggernaut.characterName = "The Juggernaut";
            juggernaut.characterTitle = "Ironbound Behemoth";
            juggernaut.roleCategory = "Melee Brawler / Tank";
            juggernaut.loreSynopsis = "Genetically modified steel juggernaut. Awakens in the ruins of an underground foundry, fueled by unyielding vengeance against the Empire.";
            juggernaut.amnesiaWakingClue = "\"The chains are shattered... What did they do to my flesh? I feel no pain anymore. I will smash through anything in my path.\"";
            juggernaut.themeColor = new Color(0.92f, 0.22f, 0.22f, 1f);
            juggernaut.maxHealth = 150f;
            juggernaut.maxHunger = 120f;
            juggernaut.moveSpeed = 4.3f;
            juggernaut.baseMeleeDamage = 15;
            juggernaut.baseRangedDamage = 8;
            juggernaut.hungerConsumptionMultiplier = 1.2f;
            juggernaut.dashSpeed = 12f;
            juggernaut.dashDuration = 0.25f;
            juggernaut.dashCooldown = 1.0f;
            juggernaut.damageReductionPercent = 0.15f;
            juggernaut.startingMoney = 50;
            juggernaut.startingRations = 2;
            juggernaut.startingPistolAmmo = 20;
            juggernaut.startingWeapons = new List<string> { "Sledgehammer", "Shotgun" };
            juggernaut.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Shotgun Ammo", 20) };
            juggernaut.sprintSpeedMultiplier = 1.4f;
            juggernaut.shootSprintLockDuration = 1.0f;
            juggernaut.shiftAbilityType = ShiftAbilityType.UnstoppableCharge;
            juggernaut.shiftAbilityName = "Unstoppable Charge";
            juggernaut.shiftAbilityDescription = "Press [Shift] to charge forward: deals 25 impact damage + 1s stun, deflects all incoming bullets. 5s CD. (Passive: 15% damage reduction at all times)";
            juggernaut.shiftCooldown = 5.0f;
            roster.Add(juggernaut);

            // 3. Phantom
            var phantom = ScriptableObject.CreateInstance<CharacterData>();
            phantom.characterId = "PHANTOM";
            phantom.characterName = "The Phantom";
            phantom.characterTitle = "Shadow Assassin";
            phantom.roleCategory = "Hit-and-Run / Assassin";
            phantom.loreSynopsis = "Stealth dimensional assassin. Awakens in the shadows of a ruined alleyway with an encrypted mission to eliminate high-value targets.";
            phantom.amnesiaWakingClue = "\"My target... Where is the doctor? Why did the Empire want him silenced? There are dark secrets buried beneath this ruins.\"";
            phantom.themeColor = new Color(0.66f, 0.33f, 0.97f, 1f);
            phantom.maxHealth = 75f;
            phantom.maxHunger = 90f;
            phantom.moveSpeed = 6.0f;
            phantom.baseMeleeDamage = 12;
            phantom.baseRangedDamage = 10;
            phantom.hungerConsumptionMultiplier = 1.0f;
            phantom.dashSpeed = 18f;
            phantom.dashDuration = 0.30f;
            phantom.dashCooldown = 0.55f;
            phantom.damageReductionPercent = 0f;
            phantom.startingMoney = 150;
            phantom.startingRations = 2;
            phantom.startingPistolAmmo = 40;
            phantom.startingWeapons = new List<string> { "Katana", "Pistol" };
            phantom.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Light Ammo", 40) };
            phantom.sprintSpeedMultiplier = 1.6f;
            phantom.shootSprintLockDuration = 1.0f;
            phantom.shiftAbilityType = ShiftAbilityType.ShadowStep;
            phantom.shiftAbilityName = "Shadow Step";
            phantom.shiftAbilityDescription = "Hold [Shift] to enter shadow phase: +40% speed, pass through enemies freely. Exiting shadow guarantees 2.0x Critical Strike on your next attack.";
            phantom.shiftCooldown = 0f;
            roster.Add(phantom);

            // 4. Marksman
            var marksman = ScriptableObject.CreateInstance<CharacterData>();
            marksman.characterId = "MARKSMAN";
            marksman.characterName = "The Marksman";
            marksman.characterTitle = "Apex Sharpshooter";
            marksman.roleCategory = "Long-Range / Tactical";
            marksman.loreSynopsis = "Eagle-eyed sharpshooter. Awakens atop an abandoned observation tower, vividly recalling the moment the reactor detonated through his scope.";
            marksman.amnesiaWakingClue = "\"Through my scope that day... The one who flipped the switch wasn't an invader. It was an Imperial officer! I will uncover the betrayal.\"";
            marksman.themeColor = new Color(0.0f, 0.75f, 0.95f, 1f);
            marksman.maxHealth = 85f;
            marksman.maxHunger = 100f;
            marksman.moveSpeed = 4.8f;
            marksman.baseMeleeDamage = 7;
            marksman.baseRangedDamage = 14;
            marksman.hungerConsumptionMultiplier = 1.0f;
            marksman.dashSpeed = 14f;
            marksman.dashDuration = 0.25f;
            marksman.dashCooldown = 0.8f;
            marksman.damageReductionPercent = 0f;
            marksman.startingMoney = 80;
            marksman.startingRations = 2;
            marksman.startingPistolAmmo = 30;
            marksman.startingWeapons = new List<string> { "Sniper Rifle", "Revolver" };
            marksman.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Medium Ammo", 20), new StartingAmmoConfig("Light Ammo", 20) };
            marksman.critChanceBonus = 0.25f;
            marksman.critMultiplierBonus = 0.5f;
            marksman.sprintSpeedMultiplier = 1.5f;
            marksman.shootSprintLockDuration = 1.0f;
            marksman.shiftAbilityType = ShiftAbilityType.EagleEye;
            marksman.shiftAbilityName = "Eagle Eye";
            marksman.shiftAbilityDescription = "Hold [Shift] to enter tactical focus: 1.35x sniper zoom, 2x attack range, bullets pierce through all enemies. (Move slower while aiming)";
            marksman.shiftCooldown = 0f;
            roster.Add(marksman);

            // 5. Scavenger
            var scavenger = ScriptableObject.CreateInstance<CharacterData>();
            scavenger.characterId = "SCAVENGER";
            scavenger.characterName = "The Scavenger";
            scavenger.characterTitle = "Wasteland Nomad";
            scavenger.roleCategory = "Survivalist / Trapper";
            scavenger.loreSynopsis = "Hooded wasteland nomad who slipped past city walls scavenging for survival rations, now trapped amidst the fallen ruins.";
            scavenger.amnesiaWakingClue = "\"My stomach is burning with hunger... Need to find bread or scraps first. Forget the Empire, I just need to blast the city gates open and survive.\"";
            scavenger.themeColor = new Color(0.25f, 0.85f, 0.45f, 1f);
            scavenger.maxHealth = 90f;
            scavenger.maxHunger = 110f;
            scavenger.moveSpeed = 5.2f;
            scavenger.baseMeleeDamage = 10;
            scavenger.baseRangedDamage = 10;
            scavenger.hungerConsumptionMultiplier = 0.85f;
            scavenger.dashSpeed = 16f;
            scavenger.dashDuration = 0.25f;
            scavenger.dashCooldown = 0.75f;
            scavenger.damageReductionPercent = 0f;
            scavenger.startingMoney = 300;
            scavenger.startingRations = 2;
            scavenger.startingPistolAmmo = 0;
            scavenger.startingWeapons = new List<string> { "Crossbow", "Survival Spear" };
            scavenger.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Bolts", 25) };
            scavenger.dropRateBonus = 0.25f;
            scavenger.moneyMultiplier = 1.30f;
            scavenger.foodHealBonus = 10f;
            scavenger.sprintSpeedMultiplier = 1.5f;
            scavenger.shootSprintLockDuration = 1.0f;
            scavenger.shiftAbilityType = ShiftAbilityType.GrapplingHook;
            scavenger.shiftAbilityName = "Grappling Hook";
            scavenger.shiftAbilityDescription = "Press [Shift] to fire a survival grappling wire: latches onto buildings/obstacles and zips you directly to the point. (Passive: eating food grants +10 bonus HP)";
            scavenger.shiftCooldown = 3.0f;
            scavenger.grappleRange = 18f;
            scavenger.grapplePullSpeed = 22f;
            scavenger.grappleHungerCost = 3.5f;
            roster.Add(scavenger);

            // 6. Rebel
            var rebel = ScriptableObject.CreateInstance<CharacterData>();
            rebel.characterId = "REBEL";
            rebel.characterName = "The Rebel";
            rebel.characterTitle = "Anarchist Brawler";
            rebel.roleCategory = "Brawler / Close Quarters";
            rebel.loreSynopsis = "Former wasteland riot punk armed with custom spiked weaponry. Awakens amidst smoldering bar barricades, defying both imperial enforcers and necrotic swarms.";
            rebel.amnesiaWakingClue = "\"Ugh, that blast gave me a ringing headache... Where's my bat? If the Empire thinks they can chain me down, they're in for a bloody surprise!\"";
            rebel.themeColor = new Color(0.98f, 0.20f, 0.40f, 1f);
            rebel.maxHealth = 110f;
            rebel.maxHunger = 100f;
            rebel.moveSpeed = 5.2f;
            rebel.baseMeleeDamage = 16;
            rebel.baseRangedDamage = 8;
            rebel.critChanceBonus = 0.15f;
            rebel.critMultiplierBonus = 0.4f;
            rebel.damageReductionPercent = 0.05f;
            rebel.hungerConsumptionMultiplier = 1.05f;
            rebel.dashSpeed = 16f;
            rebel.dashDuration = 0.25f;
            rebel.dashCooldown = 0.70f;
            rebel.startingMoney = 120;
            rebel.startingRations = 2;
            rebel.startingPistolAmmo = 24;
            rebel.startingWeapons = new List<string> { "Baseball Bat", "Shotgun" };
            rebel.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Shotgun Ammo", 24) };
            rebel.sprintSpeedMultiplier = 1.5f;
            rebel.shootSprintLockDuration = 1.0f;
            rebel.shiftAbilityType = ShiftAbilityType.CycloneSwing;
            rebel.shiftAbilityName = "Cyclone Swing";
            rebel.shiftAbilityDescription = "Press [Shift] to spin violently in a 360 whirlwind for 1.2s: damages & knocks back all nearby enemies, deflects incoming bullets, and grants 40% damage reduction. (Passive: Frenzy at <50% HP grants +35% attack speed & +25% crit, melee kills restore hunger)";
            rebel.shiftCooldown = 4.5f;
            rebel.cycloneDuration = 1.2f;
            rebel.cycloneDamagePerHit = 22f;
            rebel.cycloneHitInterval = 0.25f;
            rebel.cycloneRadius = 2.4f;
            rebel.cycloneKnockback = 12f;
            rebel.cycloneDamageReduction = 0.40f;
            rebel.cycloneMoveSpeedMultiplier = 1.30f;
            rebel.cycloneHungerCost = 4.0f;
            rebel.rebelFrenzyHpThreshold = 0.50f;
            rebel.rebelFrenzyAttackSpeedBonus = 0.35f;
            rebel.rebelFrenzyCritBonus = 0.25f;
            rebel.rebelMeleeKillHungerRestore = 3.0f;
            roster.Add(rebel);

            // 7. Demolitionist
            var demolitionist = ScriptableObject.CreateInstance<CharacterData>();
            demolitionist.characterId = "DEMOLITIONIST";
            demolitionist.characterName = "The Demolitionist";
            demolitionist.characterTitle = "Pyrotechnic Saboteur";
            demolitionist.roleCategory = "Trapper / Area Denial";
            demolitionist.loreSynopsis = "Former imperial ordnance specialist turned freelance saboteur. Awakens in the crater of a collapsed munitions depot, surrounded by unexploded ordinance.";
            demolitionist.amnesiaWakingClue = "\"Careful where you step... that timer's still ticking. The depot was sabotaged from the inside. Whoever blew the blast doors won't escape my payload.\"";
            demolitionist.themeColor = new Color(0.96f, 0.45f, 0.05f, 1f);
            demolitionist.maxHealth = 105f;
            demolitionist.maxHunger = 105f;
            demolitionist.moveSpeed = 4.9f;
            demolitionist.baseMeleeDamage = 11;
            demolitionist.baseRangedDamage = 12;
            demolitionist.critChanceBonus = 0.05f;
            demolitionist.critMultiplierBonus = 0.2f;
            demolitionist.damageReductionPercent = 0f;
            demolitionist.hungerConsumptionMultiplier = 1.0f;
            demolitionist.dashSpeed = 14f;
            demolitionist.dashDuration = 0.25f;
            demolitionist.dashCooldown = 0.80f;
            demolitionist.startingMoney = 90;
            demolitionist.startingRations = 2;
            demolitionist.startingPistolAmmo = 36;
            demolitionist.startingWeapons = new List<string> { "Fire Axe", "Revolver" };
            demolitionist.startingAmmo = new List<StartingAmmoConfig> { new StartingAmmoConfig("Light Ammo", 36) };
            demolitionist.sprintSpeedMultiplier = 1.5f;
            demolitionist.shootSprintLockDuration = 1.0f;
            demolitionist.shiftAbilityType = ShiftAbilityType.SatchelVault;
            demolitionist.shiftAbilityName = "Satchel Vault";
            demolitionist.shiftAbilityDescription = "Press [Shift] to plant a C4 satchel charge and launch yourself backward 4.5m with evasion I-frames. The mine arms in 0.3s and detonates on enemy proximity or 2.5s timer dealing 65 AOE damage and 1.2s stun. (Passive: Blast Shield reduces explosive damage taken by 60%, +30% blast radius)";
            demolitionist.shiftCooldown = 5.0f;
            demolitionist.satchelDamage = 65f;
            demolitionist.satchelBlastRadius = 4.5f;
            demolitionist.satchelStunDuration = 1.2f;
            demolitionist.satchelArmDelay = 0.3f;
            demolitionist.satchelAutoDetonateTime = 2.5f;
            demolitionist.vaultDistance = 4.5f;
            demolitionist.vaultSpeed = 16f;
            demolitionist.satchelHungerCost = 5.0f;
            demolitionist.explosiveDamageBonus = 0.20f;
            demolitionist.explosiveRadiusBonus = 0.30f;
            demolitionist.blastDamageReduction = 0.60f;
            roster.Add(demolitionist);

            return roster;
        }
    }

    /// <summary>
    /// Global session manager holding the currently selected character ID across scenes.
    /// </summary>
    public static class CharacterManager
    {
        private const string PrefsKey = "TheLastEmpire_SelectedCharacterId";
        public static string SelectedCharacterId
        {
            get => PlayerPrefs.GetString(PrefsKey, "VANGUARD");
            set
            {
                PlayerPrefs.SetString(PrefsKey, value);
                PlayerPrefs.Save();
            }
        }

        public static CharacterData GetSelectedCharacter()
        {
            if (CharacterDatabase.Instance != null)
            {
                return CharacterDatabase.Instance.GetCharacter(SelectedCharacterId);
            }
            return null;
        }
    }
}
