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
        ToxicTrail         // Scavenger: Spawns toxic fog trails, slows, DoT, food heals extra
    }

    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "The Last Empire/Character Data", order = 1)]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity & Lore")]
        public string characterId = "VANGUARD";
        public string characterName = "The Vanguard";
        public string characterTitle = "Imperial Vanguard";
        public string roleCategory = "All-Rounder / Frontline";
        [Tooltip("Full-body illustration or portrait displayed in operative selection screen.")]
        public Sprite characterPortrait;
        [TextArea(3, 6)]
        public string loreSynopsis = "A battle-tested soldier of the imperial guard. Awakens in the military garrison ruins with his oath etched into his memory.";
        [TextArea(2, 4)]
        public string amnesiaWakingClue = "\"My head hurts... Where is the squad? My rifle's still loaded. I need to reach the central citadel before it's too late.\"";
        public Color themeColor = new Color(0.95f, 0.55f, 0.15f, 1f); // Accent color for UI

        [Header("Base Attributes")]
        public float maxHealth = 100f;
        public float maxHunger = 100f;
        public float moveSpeed = 5.0f;
        public int baseMeleeDamage = 10;
        public int baseRangedDamage = 10;
        public float hungerConsumptionMultiplier = 1.0f;

        [Header("Mobility & Defense")]
        public float dashSpeed = 15f;
        public float dashDuration = 0.25f;
        public float dashCooldown = 0.8f;
        public float damageReductionPercent = 0f;

        [Header("Starting Economy & Loadout")]
        public int startingMoney = 0;
        public int startingPistolAmmo = 60;
        public List<string> startingWeapons = new List<string> { "Pistol", "Knife" };
        public float dropRateBonus = 0f;
        public float moneyMultiplier = 1.0f;

        [Header("Signature Shift Active Ability")]
        public ShiftAbilityType shiftAbilityType = ShiftAbilityType.AdrenalineRush;
        public string shiftAbilityName = "Adrenaline Rush";
        [TextArea(2, 4)]
        public string shiftAbilityDescription = "Hold Shift to surge with combat adrenaline: +75% move speed, +30% fire/attack rate, and 20% damage resistance. Consumes hunger faster.";
        public float shiftCooldown = 0f; // 0 for toggle/channeling, >0 for active burst cooldown
    }
}
