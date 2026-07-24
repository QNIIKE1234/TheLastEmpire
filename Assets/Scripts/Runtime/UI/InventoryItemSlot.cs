using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Component สำหรับ Item Slot 1 ช่องใน Inventory UI.
    /// ลาก Script นี้ใส่บน Prefab ของ Item Slot แล้ว Assign ช่องใน Inspector
    /// จากนั้นเรียก SetData() เพื่อเซทข้อมูลไอเทมได้เลย
    /// </summary>
    public class InventoryItemSlot : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Image สำหรับแสดง Icon ของไอเทม")]
        [SerializeField] private Image itemIcon;

        [Tooltip("TMP_Text สำหรับแสดงชื่อไอเทม")]
        [SerializeField] private TMP_Text itemNameText;

        [Tooltip("TMP_Text สำหรับแสดงจำนวน เช่น x60")]
        [SerializeField] private TMP_Text itemQuantityText;

        [Tooltip("Badge / Label แสดงประเภทไอเทม เช่น WEAPON, AMMO, USABLE")]
        [SerializeField] private TMP_Text itemTypeBadgeText;

        [Tooltip("Image พื้นหลัง Badge (optional ใช้เปลี่ยนสีตามประเภท)")]
        [SerializeField] private Image badgeBackground;

        [Tooltip("Border หรือ Highlight เมื่อไอเทมถูก Equip อยู่")]
        [SerializeField] private GameObject equippedHighlight;

        [Tooltip("Button สำหรับกดใช้ไอเทม (ถ้าไม่ใช้ได้ก็จะ Disable อัตโนมัติ)")]
        [SerializeField] private Button useButton;

        [Tooltip("Text บนปุ่ม เช่น 'Use', 'Equip', 'Eat'")]
        [SerializeField] private TMP_Text useButtonLabel;

        // ข้อมูล Item ปัจจุบันที่ Slot นี้แสดง
        private string _currentItemName;
        private Action<string> _onClickCallback;

        // =====================================================
        // Palette สีตามประเภทไอเทม
        // =====================================================
        private static readonly Color ColorWeapon  = new Color(0f,    0.898f, 1f,    1f); // Cyan
        private static readonly Color ColorAmmo    = new Color(0.557f, 0.557f, 0.557f, 1f); // Gray
        private static readonly Color ColorUsable  = new Color(0.106f, 1f,    0.2f,  1f); // Green
        private static readonly Color ColorFood    = new Color(1f,    0.922f, 0.231f, 1f); // Yellow
        private static readonly Color ColorEtc     = new Color(1f,    0.604f, 0f,    1f); // Orange
        private static readonly Color ColorEquipped= new Color(0f,    0.898f, 1f,    1f); // Same as weapon equipped

        // =====================================================
        //  Public API
        // =====================================================

        /// <summary>
        /// เซทข้อมูลไอเทมให้ Slot นี้แสดงผล
        /// </summary>
        /// <param name="itemName">ชื่อไอเทม เช่น "Pistol", "Pistol Ammo", "Potion"</param>
        /// <param name="quantity">จำนวน</param>
        /// <param name="icon">Sprite icon (ใส่ null ถ้าไม่มี)</param>
        /// <param name="isEquipped">true = ไอเทมนี้กำลัง Equip อยู่</param>
        /// <param name="onClickUse">Callback เมื่อกดปุ่ม Use (ใส่ null = ปุ่มจะถูก Disable)</param>
        public void SetData(string itemName, int quantity, Sprite icon = null,
                            bool isEquipped = false, Action<string> onClickUse = null,
                            string customButtonText = null, string customQuantityText = null)
        {
            _currentItemName = itemName;
            _onClickCallback = onClickUse;

            string cleanName = (itemName ?? "").ToLower().Trim();
            ItemType type = ResolveItemType(cleanName);

            // --- Icon ---
            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.gameObject.SetActive(icon != null);
            }

            // --- ชื่อไอเทม ---
            if (itemNameText != null)
            {
                itemNameText.text = itemName;
                itemNameText.color = isEquipped ? ColorEquipped : Color.white;
            }

            // --- จำนวน ---
            if (itemQuantityText != null)
            {
                if (!string.IsNullOrEmpty(customQuantityText))
                    itemQuantityText.text = customQuantityText;
                else if (isEquipped && type == ItemType.Weapon)
                    itemQuantityText.text = "[ON]"; // Weapon แสดงเป็น "Equipped" แทน xN ถ้ากำลัง Equip
                else
                    itemQuantityText.text = $"x{quantity}";

                itemQuantityText.color = new Color(0.565f, 0.643f, 0.682f, 1f);
            }

            // --- Badge Label & สี ---
            ApplyBadge(type, isEquipped);

            // --- Equipped Highlight ---
            if (equippedHighlight != null)
                equippedHighlight.SetActive(isEquipped);

            // --- Use Button ---
            bool canUse = onClickUse != null;
            if (useButton != null)
            {
                useButton.gameObject.SetActive(canUse);
                useButton.onClick.RemoveAllListeners();
                if (canUse)
                {
                    useButton.onClick.AddListener(OnUseClicked);
                    if (useButtonLabel != null)
                        useButtonLabel.text = !string.IsNullOrEmpty(customButtonText) ? customButtonText : ResolveButtonLabel(type, isEquipped);
                }
            }
        }

        /// <summary>
        /// ล้าง Slot ให้ว่างเปล่า (ใช้ตอน Reset pool หรือ clear UI)
        /// </summary>
        public void Clear()
        {
            _currentItemName = null;
            _onClickCallback = null;

            if (itemIcon != null)        { itemIcon.sprite = null; itemIcon.gameObject.SetActive(false); }
            if (itemNameText != null)    itemNameText.text = "";
            if (itemQuantityText != null) itemQuantityText.text = "";
            if (itemTypeBadgeText != null) itemTypeBadgeText.text = "";
            if (equippedHighlight != null) equippedHighlight.SetActive(false);
            if (useButton != null)       useButton.gameObject.SetActive(false);
        }

        // =====================================================
        //  Private Helpers
        // =====================================================

        private void OnUseClicked()
        {
            if (!string.IsNullOrEmpty(_currentItemName))
                _onClickCallback?.Invoke(_currentItemName);
        }

        private void ApplyBadge(ItemType type, bool isEquipped)
        {
            if (itemTypeBadgeText == null) return;

            string label;
            Color color;

            if (isEquipped && type == ItemType.Weapon)
            {
                label = "EQUIPPED";
                color = ColorEquipped;
            }
            else
            {
                switch (type)
                {
                    case ItemType.Weapon:
                        label = "WEAPON";  color = ColorWeapon;  break;
                    case ItemType.Ammo:
                        label = "AMMO";    color = ColorAmmo;    break;
                    case ItemType.Potion:
                        label = "USABLE";  color = ColorUsable;  break;
                    case ItemType.Food:
                        label = "FOOD";    color = ColorFood;    break;
                    default:
                        label = "ITEM";    color = ColorEtc;     break;
                }
            }

            itemTypeBadgeText.text = label;
            itemTypeBadgeText.color = color;

            if (badgeBackground != null)
                badgeBackground.color = new Color(color.r, color.g, color.b, 0.15f);
        }

        private static string ResolveButtonLabel(ItemType type, bool isEquipped)
        {
            return type switch
            {
                ItemType.Weapon => isEquipped ? "UnEquip" : "Equip",
                ItemType.Potion => "Use",
                ItemType.Food   => "Eat",
                _               => "Use",
            };
        }

        private static ItemType ResolveItemType(string cleanName)
        {
            if (cleanName.Contains("ammo"))                                              return ItemType.Ammo;
            if (cleanName.Contains("potion"))                                            return ItemType.Potion;
            if (cleanName.Contains("bread") || cleanName.Contains("food"))              return ItemType.Food;
            if (cleanName.Contains("pist") || cleanName.Contains("rifl") ||
                cleanName.Contains("shot") || cleanName.Contains("knife") ||
                cleanName.Contains("bat")  || cleanName.Contains("machete"))             return ItemType.Weapon;
            return ItemType.Etc;
        }

        // =====================================================
        //  Inner Enum
        // =====================================================
        private enum ItemType { Weapon, Ammo, Potion, Food, Etc }
    }
}
