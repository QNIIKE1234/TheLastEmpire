using UnityEngine;

namespace TheLastEmpire
{
    public struct DamageResult
    {
        public float RawDamage;
        public float DistanceFalloffMultiplier;
        public float Variance;
        public bool IsCritical;
        public float CritMultiplier;
        public float FinalDamage;
    }

    /// <summary>
    /// Centralized combat damage calculator supporting:
    /// 1. Distance-based damage falloff (smooth reduction down to 40% minimum floor)
    /// 2. Random damage variance (+-10)
    /// 3. Universal critical strikes (20% base chance, 1.3x base multiplier) with class/weapon overrides:
    ///    - Katana: 2.0x multiplier
    ///    - Phantom (Shadow Step exit): 100% guaranteed crit, 2.0x multiplier
    ///    - Marksman: 35% crit chance, 1.5x multiplier (2.0x in EagleEye)
    /// </summary>
    public static class DamageCalculator
    {
        public const float MIN_FALLOFF_FLOOR = 0.40f;         // Minimum damage floor is 40%
        public const float POINT_BLANK_RANGE = 2.0f;          // 100% damage within this distance
        public const float DEFAULT_MAX_FALLOFF_RANGE = 14.0f; // Drops to 40% at/beyond this distance
        public const float BASE_CRIT_CHANCE = 0.20f;          // 20% crit chance across all entities
        public const float BASE_CRIT_MULTIPLIER = 1.30f;      // 1.3x damage on critical strike

        /// <summary>
        /// Calculates ranged projectile damage based on distance traveled, random variance (+-10), and critical strike.
        /// </summary>
        public static DamageResult CalculateRangedDamage(
            float baseDamage,
            float distanceTraveled,
            GameObject attacker = null,
            string weaponName = "",
            float maxFalloffRange = DEFAULT_MAX_FALLOFF_RANGE)
        {
            DamageResult result = new DamageResult();
            result.RawDamage = baseDamage;

            // 1. Distance Falloff (Point-blank 100% -> Max Range 40% floor)
            float t = Mathf.Clamp01((distanceTraveled - POINT_BLANK_RANGE) / Mathf.Max(0.1f, maxFalloffRange - POINT_BLANK_RANGE));
            float falloff = Mathf.Lerp(1.0f, MIN_FALLOFF_FLOOR, t);
            // Ensure it NEVER drops below 40%
            falloff = Mathf.Max(MIN_FALLOFF_FLOOR, falloff);
            result.DistanceFalloffMultiplier = falloff;

            float damageAfterFalloff = baseDamage * falloff;

            // 2. Random Variance (+-10 damage)
            float variance = Random.Range(-10f, 10f);
            result.Variance = variance;
            float damageAfterVariance = Mathf.Max(1f, damageAfterFalloff + variance);

            // 3. Critical Strike Evaluation
            float critChance = BASE_CRIT_CHANCE;
            float critMultiplier = BASE_CRIT_MULTIPLIER;
            bool guaranteedCrit = false;

            if (attacker != null)
            {
                PlayerController pc = attacker.GetComponent<PlayerController>();
                if (pc != null)
                {
                    // Check Phantom Shadow Step guaranteed crit bonus
                    if (pc.HasGuaranteedCrit)
                    {
                        guaranteedCrit = true;
                        critMultiplier = 2.0f;
                        pc.HasGuaranteedCrit = false;
                    }
                    else if (pc.ActiveShiftAbility is ShadowStepAbility shadow && shadow.HasGuaranteedCrit)
                    {
                        guaranteedCrit = true;
                        critMultiplier = 2.0f;
                        shadow.ConsumeCritBonus();
                    }

                    // Marksman class specialization
                    if (pc.ActiveCharacterData != null && pc.ActiveCharacterData.characterId == "MARKSMAN")
                    {
                        critChance = 0.35f; // 35% crit chance for Marksman
                        critMultiplier = (pc.ActiveShiftAbility is EagleEyeAbility eagle && eagle.IsActive) ? 2.0f : 1.50f;
                    }
                }
            }

            // Weapon specific crit modifiers
            if (!string.IsNullOrEmpty(weaponName))
            {
                if (weaponName.IndexOf("Sniper", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    critChance += 0.15f;
                    critMultiplier = Mathf.Max(critMultiplier, 1.50f);
                }
            }

            bool isCrit = guaranteedCrit || (Random.value < critChance);
            result.IsCritical = isCrit;
            result.CritMultiplier = isCrit ? critMultiplier : 1.0f;

            float finalDamage = isCrit ? (damageAfterVariance * critMultiplier) : damageAfterVariance;
            result.FinalDamage = Mathf.Max(1f, Mathf.Round(finalDamage));

            return result;
        }

        /// <summary>
        /// Calculates melee damage with random variance (+-10) and critical strike evaluation (no distance falloff for melee).
        /// </summary>
        public static DamageResult CalculateMeleeDamage(
            float baseDamage,
            GameObject attacker = null,
            string weaponName = "")
        {
            DamageResult result = new DamageResult();
            result.RawDamage = baseDamage;
            result.DistanceFalloffMultiplier = 1.0f; // Melee is point-blank

            // 1. Random Variance (+-10 damage)
            float variance = Random.Range(-10f, 10f);
            result.Variance = variance;
            float damageAfterVariance = Mathf.Max(1f, baseDamage + variance);

            // 2. Critical Strike Evaluation
            float critChance = BASE_CRIT_CHANCE;
            float critMultiplier = BASE_CRIT_MULTIPLIER;
            bool guaranteedCrit = false;

            bool isKatana = !string.IsNullOrEmpty(weaponName) && weaponName.IndexOf("Katana", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isKatana)
            {
                critMultiplier = 2.0f; // Katana deals 2.0x on crit
            }

            if (attacker != null)
            {
                PlayerController pc = attacker.GetComponent<PlayerController>();
                if (pc != null)
                {
                    if (pc.HasGuaranteedCrit)
                    {
                        guaranteedCrit = true;
                        critMultiplier = 2.0f;
                        pc.HasGuaranteedCrit = false;
                    }
                    else if (pc.ActiveShiftAbility is ShadowStepAbility shadow && shadow.HasGuaranteedCrit)
                    {
                        guaranteedCrit = true;
                        critMultiplier = 2.0f;
                        shadow.ConsumeCritBonus();
                    }
                }
            }

            bool isCrit = guaranteedCrit || (Random.value < critChance);
            result.IsCritical = isCrit;
            result.CritMultiplier = isCrit ? critMultiplier : 1.0f;

            float finalDamage = isCrit ? (damageAfterVariance * critMultiplier) : damageAfterVariance;
            result.FinalDamage = Mathf.Max(1f, Mathf.Round(finalDamage));

            return result;
        }

        /// <summary>
        /// Calculates explosion AOE damage with distance falloff from explosion epicenter and variance.
        /// </summary>
        public static DamageResult CalculateExplosionDamage(
            float baseDamage,
            float distanceFromCenter,
            float explosionRadius,
            GameObject attacker = null)
        {
            DamageResult result = new DamageResult();
            result.RawDamage = baseDamage;

            float t = Mathf.Clamp01(distanceFromCenter / Mathf.Max(0.5f, explosionRadius));
            float falloff = Mathf.Lerp(1.0f, MIN_FALLOFF_FLOOR, t);
            result.DistanceFalloffMultiplier = falloff;

            float damageAfterFalloff = baseDamage * falloff;
            float variance = Random.Range(-10f, 10f);
            result.Variance = variance;
            float damageAfterVariance = Mathf.Max(1f, damageAfterFalloff + variance);

            bool isCrit = Random.value < BASE_CRIT_CHANCE;
            result.IsCritical = isCrit;
            result.CritMultiplier = isCrit ? BASE_CRIT_MULTIPLIER : 1.0f;

            float finalDamage = isCrit ? (damageAfterVariance * BASE_CRIT_MULTIPLIER) : damageAfterVariance;
            result.FinalDamage = Mathf.Max(1f, Mathf.Round(finalDamage));

            return result;
        }
    }
}
