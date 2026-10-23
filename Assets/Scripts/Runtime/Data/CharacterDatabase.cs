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
            vanguard.startingPistolAmmo = 60;
            vanguard.startingWeapons = new List<string> { "Rifle", "Knife" };
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
            juggernaut.startingPistolAmmo = 20;
            juggernaut.startingWeapons = new List<string> { "Sledgehammer", "Shotgun" };
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
            phantom.startingPistolAmmo = 40;
            phantom.startingWeapons = new List<string> { "Katana", "Pistol" };
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
            marksman.startingPistolAmmo = 30;
            marksman.startingWeapons = new List<string> { "Sniper Rifle", "Revolver" };
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
            scavenger.startingPistolAmmo = 0;
            scavenger.startingWeapons = new List<string> { "Crossbow", "Survival Spear" };
            scavenger.shiftAbilityType = ShiftAbilityType.ToxicTrail;
            scavenger.shiftAbilityName = "Toxic Trail";
            scavenger.shiftAbilityDescription = "Hold [Shift] to sprint and release toxic smoke clouds: enemies are slowed by 50% and suffer 8 poison DPS. (Passive: eating food grants +10 bonus HP)";
            scavenger.shiftCooldown = 0f;
            roster.Add(scavenger);

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
