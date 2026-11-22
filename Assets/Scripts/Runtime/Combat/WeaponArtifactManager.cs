using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// ค่าสถานะรวมจาก Artifact ทั้งหมดที่ติดตั้งอยู่บนอาวุธ 1 ชิ้น (สูงสุด 4 ช่อง)
    /// </summary>
    public struct WeaponArtifactModifiers
    {
        public float bonusDamage;
        public float damageMultiplier;
        public int bonusPellets;
        public float extraSpreadAngle;
        public float fireRateMultiplier;
        public int bonusMagazineSize;
        public float reloadSpeedMultiplier;
        public float bonusRange;
        public bool grantPierce;
        public bool grantAutomatic;
        public int equippedCount;

        public bool HasAnyEffect =>
            equippedCount > 0 ||
            Mathf.Abs(bonusDamage) > 0.001f ||
            Mathf.Abs(damageMultiplier) > 0.001f ||
            bonusPellets != 0 ||
            Mathf.Abs(fireRateMultiplier) > 0.001f ||
            bonusMagazineSize != 0 ||
            Mathf.Abs(reloadSpeedMultiplier) > 0.001f ||
            Mathf.Abs(bonusRange) > 0.001f ||
            grantPierce ||
            grantAutomatic;
    }

    /// <summary>
    /// WeaponArtifactManager — จัดการการติดตั้ง/ถอด Artifact บนอาวุธ (1 อาวุธใส่ได้สูงสุด 4 ชิ้น)
    /// และคำนวณค่าโบนัสเพื่อส่งต่อไปยัง PlayerController (ตอนยิง/ฟัน) และ WeaponUpgradeUI
    /// </summary>
    public static class WeaponArtifactManager
    {
        public const int MaxArtifactSlots = 4;

        // key: normalized weaponName (lowercase trimmed) -> string[4] (ชื่อไอเทม Artifact ในแต่ละช่อง)
        private static readonly Dictionary<string, string[]> _equippedByWeapon = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        public static event Action<string> OnWeaponArtifactsChanged;

        private static string NormalizeWeaponKey(string weaponName)
        {
            return string.IsNullOrWhiteSpace(weaponName) ? string.Empty : weaponName.Trim().ToLowerInvariant();
        }

        public static void ClearAll()
        {
            _equippedByWeapon.Clear();
            OnWeaponArtifactsChanged?.Invoke(string.Empty);
        }

        /// <summary>
        /// ดึงอาร์เรย์ 4 ช่องของ Artifact ที่ติดอยู่บนอาวุธชิ้นนี้ (ช่องว่างจะเป็น null/empty)
        /// </summary>
        public static string[] GetEquippedSlots(string weaponName)
        {
            string key = NormalizeWeaponKey(weaponName);
            if (string.IsNullOrEmpty(key)) return new string[MaxArtifactSlots];

            if (!_equippedByWeapon.TryGetValue(key, out string[] slots) || slots == null || slots.Length != MaxArtifactSlots)
            {
                slots = new string[MaxArtifactSlots];
                _equippedByWeapon[key] = slots;
            }
            return slots;
        }

        public static int GetFirstEmptySlotIndex(string weaponName)
        {
            string[] slots = GetEquippedSlots(weaponName);
            for (int i = 0; i < MaxArtifactSlots; i++)
            {
                if (string.IsNullOrEmpty(slots[i])) return i;
            }
            return -1;
        }

        /// <summary>
        /// ตรวจสอบว่าไอเทมนี้เป็น Artifact หรือไม่
        /// </summary>
        public static bool IsArtifactItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            if (ItemDatabase.Instance != null)
            {
                ItemData data = ItemDatabase.Instance.GetItemByName(itemName);
                if (data != null) return data.type == ItemType.Artifact;
            }
            return false;
        }

        /// <summary>
        /// ตรวจสอบว่าไอเทมนี้เป็นอาวุธ (Ranged / Melee / Throwing) หรือไม่
        /// </summary>
        public static bool IsWeaponItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            if (ItemDatabase.Instance != null)
            {
                ItemData data = ItemDatabase.Instance.GetItemByName(itemName);
                if (data != null)
                {
                    return data.type == ItemType.RangedWeapon ||
                           data.type == ItemType.MeleeWeapon ||
                           data.type == ItemType.ThrowingWeapon;
                }
            }
            return false;
        }

        /// <summary>
        /// ตรวจสอบว่า Artifact ชิ้นนี้สามารถติดตั้งกับอาวุธปลายทางได้หรือไม่
        /// (ถ้า compatibleWeaponNames ว่าง = ใส่ได้กับทุกอาวุธ)
        /// </summary>
        public static bool IsArtifactCompatibleWithWeapon(string artifactItemName, string weaponName)
        {
            if (string.IsNullOrWhiteSpace(artifactItemName) || string.IsNullOrWhiteSpace(weaponName)) return false;
            ItemData artData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(artifactItemName) : null;
            if (artData == null || artData.type != ItemType.Artifact) return false;

            ArtifactModConfig cfg = artData.artifactConfig;
            if (cfg == null || cfg.compatibleWeaponNames == null || cfg.compatibleWeaponNames.Count == 0)
            {
                return true;
            }

            string cleanWeapon = weaponName.Trim().ToLowerInvariant();
            for (int i = 0; i < cfg.compatibleWeaponNames.Count; i++)
            {
                string allowed = cfg.compatibleWeaponNames[i];
                if (string.IsNullOrWhiteSpace(allowed)) continue;
                string cleanAllowed = allowed.Trim().ToLowerInvariant();
                if (cleanWeapon == cleanAllowed || cleanWeapon.Contains(cleanAllowed) || cleanAllowed.Contains(cleanWeapon))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// ติดตั้ง Artifact จากกระเป๋าผู้เล่นลงช่องที่กำหนด (หรือช่องว่างแรกถ้า slotIndex &lt; 0)
        /// หากช่องนั้นมี Artifact เดิมอยู่จะถอดคืนกระเป๋าก่อนอัตโนมัติ
        /// </summary>
        public static bool EquipArtifact(string weaponName, string artifactItemName, PlayerInventory inventory, int slotIndex, out string failReason)
        {
            failReason = string.Empty;
            if (string.IsNullOrWhiteSpace(weaponName) || string.IsNullOrWhiteSpace(artifactItemName) || inventory == null)
            {
                failReason = "INVALID TARGET";
                return false;
            }

            if (!IsArtifactCompatibleWithWeapon(artifactItemName, weaponName))
            {
                failReason = "INCOMPATIBLE WEAPON";
                return false;
            }

            if (inventory.GetItemCount(artifactItemName) <= 0)
            {
                failReason = "ARTIFACT NOT IN BAG";
                return false;
            }

            string[] slots = GetEquippedSlots(weaponName);
            int targetSlot = slotIndex;
            if (targetSlot < 0 || targetSlot >= MaxArtifactSlots)
            {
                targetSlot = GetFirstEmptySlotIndex(weaponName);
                if (targetSlot < 0)
                {
                    failReason = "ALL 4 SLOTS FULL";
                    return false;
                }
            }

            string existing = slots[targetSlot];

            // หัก Artifact ใหม่ออกจากกระเป๋าก่อน (เผื่อการหักจะเปิดช่องว่างสำหรับรับอันเก่ากลับเข้ากระเป๋า)
            if (!inventory.RemoveItem(artifactItemName, 1))
            {
                failReason = "ARTIFACT NOT IN BAG";
                return false;
            }

            if (!string.IsNullOrEmpty(existing))
            {
                if (!inventory.CanAddItem(existing, 1))
                {
                    // คืนอันใหม่กลับกระเป๋าหากไม่สามารถคืนอันเก่าได้เพราะกระเป๋าเต็ม
                    bool prevSup = inventory.SuppressNotificationEvents;
                    inventory.SuppressNotificationEvents = true;
                    inventory.AddItem(artifactItemName, 1);
                    inventory.SuppressNotificationEvents = prevSup;
                    failReason = "INVENTORY FULL!";
                    return false;
                }

                bool prevSup2 = inventory.SuppressNotificationEvents;
                inventory.SuppressNotificationEvents = true;
                inventory.AddItem(existing, 1);
                inventory.SuppressNotificationEvents = prevSup2;
            }

            slots[targetSlot] = artifactItemName.Trim();
            OnWeaponArtifactsChanged?.Invoke(weaponName);
            Debug.Log($"[WeaponArtifactManager] Equipped '{artifactItemName}' into '{weaponName}' slot A{targetSlot + 1}.");
            return true;
        }

        /// <summary>
        /// ถอด Artifact ออกจากช่องกลับเข้ากระเป๋าผู้เล่น (ตรวจกระเป๋าเต็มก่อนถอด!)
        /// </summary>
        public static bool UnequipArtifact(string weaponName, int slotIndex, PlayerInventory inventory, out string failReason)
        {
            failReason = string.Empty;
            if (string.IsNullOrWhiteSpace(weaponName) || inventory == null || slotIndex < 0 || slotIndex >= MaxArtifactSlots)
            {
                failReason = "INVALID SLOT";
                return false;
            }

            string[] slots = GetEquippedSlots(weaponName);
            string existing = slots[slotIndex];
            if (string.IsNullOrEmpty(existing))
            {
                return false;
            }

            if (!inventory.CanAddItem(existing, 1))
            {
                failReason = "INVENTORY FULL!";
                return false;
            }

            bool prevSup = inventory.SuppressNotificationEvents;
            inventory.SuppressNotificationEvents = true;
            bool added = inventory.AddItem(existing, 1);
            inventory.SuppressNotificationEvents = prevSup;

            if (!added)
            {
                failReason = "INVENTORY FULL!";
                return false;
            }

            slots[slotIndex] = null;
            OnWeaponArtifactsChanged?.Invoke(weaponName);
            Debug.Log($"[WeaponArtifactManager] Unequipped '{existing}' from '{weaponName}' slot A{slotIndex + 1}.");
            return true;
        }

        /// <summary>
        /// รวมค่าโบนัสจาก Artifact ทั้งหมดที่ติดอยู่บนอาวุธนี้
        /// </summary>
        public static WeaponArtifactModifiers GetAggregatedModifiers(string weaponName)
        {
            WeaponArtifactModifiers mods = default;
            string key = NormalizeWeaponKey(weaponName);
            if (string.IsNullOrEmpty(key)) return mods;

            if (!_equippedByWeapon.TryGetValue(key, out string[] slots) || slots == null)
            {
                return mods;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                string artName = slots[i];
                if (string.IsNullOrEmpty(artName)) continue;

                mods.equippedCount++;
                ItemData artData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(artName) : null;
                if (artData == null || artData.artifactConfig == null) continue;

                ArtifactModConfig cfg = artData.artifactConfig;
                mods.bonusDamage += cfg.bonusDamage;
                mods.damageMultiplier += cfg.damageMultiplier;
                mods.bonusPellets += cfg.bonusPelletsPerShot;
                mods.extraSpreadAngle += cfg.extraSpreadAngle;
                mods.fireRateMultiplier += cfg.fireRateMultiplier;
                mods.bonusMagazineSize += cfg.bonusMagazineSize;
                mods.reloadSpeedMultiplier += cfg.reloadSpeedMultiplier;
                mods.bonusRange += cfg.bonusRange;
                if (cfg.grantPierce) mods.grantPierce = true;
                if (cfg.grantAutomatic) mods.grantAutomatic = true;
            }

            return mods;
        }

        /// <summary>
        /// สร้างข้อความสรุปสเตตัสและผลของ Artifact บนอาวุธสำหรับแสดงในช่อง Descriiption ของ WeaponUpgrade_HUD
        /// </summary>
        public static string BuildWeaponSummaryText(string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName)) return "No weapon in inventory.";

            ItemData weaponData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(weaponName) : null;
            WeaponArtifactModifiers mods = GetAggregatedModifiers(weaponName);

            StringBuilder sb = new StringBuilder();
            if (weaponData != null)
            {
                float baseDmg = weaponData.damage;
                float finalDmg = (baseDmg + mods.bonusDamage) * (1f + mods.damageMultiplier);
                sb.Append($"Attack {finalDmg:F0}");
                if (mods.bonusDamage > 0f || mods.damageMultiplier > 0f)
                {
                    sb.Append($" (+{finalDmg - baseDmg:F0})");
                }

                if (weaponData.type == ItemType.RangedWeapon || weaponData.type == ItemType.ThrowingWeapon)
                {
                    WeaponConfig wc = weaponData.weaponConfig;
                    int pellets = (wc != null && wc.pelletsPerShot > 0 ? wc.pelletsPerShot : 1) + mods.bonusPellets;
                    bool isAuto = (wc != null && wc.isAutomatic) || mods.grantAutomatic;
                    bool canPierce = (wc != null && wc.canPierce) || mods.grantPierce;
                    int mag = (wc != null ? wc.magazineSize : 12) + mods.bonusMagazineSize;

                    sb.Append(isAuto ? " | Full Auto" : " | Semi Auto");
                    if (pellets > 1) sb.Append($" | Shots x{pellets}");
                    if (mag > 0) sb.Append($" | Mag {mag}");
                    if (canPierce) sb.Append(" | Pierce");
                }
                else if (weaponData.type == ItemType.MeleeWeapon)
                {
                    float range = weaponData.attackRadius + mods.bonusRange;
                    sb.Append($" | Range {range:F1}m");
                }

                if (Mathf.Abs(mods.fireRateMultiplier) > 0.001f)
                {
                    sb.Append($" | Fire Rate +{(mods.fireRateMultiplier * 100f):F0}%");
                }
            }

            // รายชื่อ Artifact ที่ใส่อยู่
            string[] slots = GetEquippedSlots(weaponName);
            List<string> activeEffects = new List<string>();
            for (int i = 0; i < MaxArtifactSlots; i++)
            {
                if (string.IsNullOrEmpty(slots[i])) continue;
                ItemData art = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(slots[i]) : null;
                string summary = art != null ? BuildSingleArtifactSummary(art) : slots[i];
                activeEffects.Add($"[A{i + 1}] {slots[i]}: {summary}");
            }

            if (activeEffects.Count > 0)
            {
                sb.Append("\n");
                sb.Append(string.Join("\n", activeEffects));
            }
            else if (weaponData != null && !string.IsNullOrWhiteSpace(weaponData.description))
            {
                sb.Append("\n");
                sb.Append(weaponData.description);
            }

            return sb.ToString();
        }

        public static string BuildSingleArtifactSummary(ItemData artData)
        {
            if (artData == null) return string.Empty;
            ArtifactModConfig cfg = artData.artifactConfig;
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.shortEffectSummary))
            {
                return cfg.shortEffectSummary.Trim();
            }

            var parts = new List<string>();
            if (cfg != null)
            {
                if (cfg.bonusDamage != 0f) parts.Add($"ATK {(cfg.bonusDamage > 0 ? "+" : "")}{cfg.bonusDamage:F0}");
                if (cfg.damageMultiplier != 0f) parts.Add($"DMG {(cfg.damageMultiplier > 0 ? "+" : "")}{(cfg.damageMultiplier * 100f):F0}%");
                if (cfg.bonusPelletsPerShot != 0) parts.Add($"Shot +{cfg.bonusPelletsPerShot}");
                if (cfg.fireRateMultiplier != 0f) parts.Add($"SPD +{(cfg.fireRateMultiplier * 100f):F0}%");
                if (cfg.bonusMagazineSize != 0) parts.Add($"MAG +{cfg.bonusMagazineSize}");
                if (cfg.reloadSpeedMultiplier != 0f) parts.Add($"RLD +{(cfg.reloadSpeedMultiplier * 100f):F0}%");
                if (cfg.bonusRange != 0f) parts.Add($"RNG +{cfg.bonusRange:F1}");
                if (cfg.grantPierce) parts.Add("Pierce");
                if (cfg.grantAutomatic) parts.Add("Auto");
            }

            if (parts.Count > 0) return string.Join(", ", parts);
            return !string.IsNullOrWhiteSpace(artData.description) ? artData.description : "Artifact";
        }
    }
}
