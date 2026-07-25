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

            ItemType type = ItemType.ETC;
            if (ItemDatabase.Instance != null)
            {
                ItemData itemData = ItemDatabase.Instance.GetItemByName(itemName);
                if (itemData != null) type = itemData.type;
            }

            bool isWeapon = type == ItemType.RangedWeapon || type == ItemType.MeleeWeapon || type == ItemType.ThrowingWeapon;

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
                else if (isEquipped && isWeapon)
                    itemQuantityText.text = "[ON]"; // Weapon แสดงเป็น "Equipped" แทน xN ถ้ากำลัง Equip
                else
                    itemQuantityText.text = $"x{quantity}";

                itemQuantityText.color = new Color(0.565f, 0.643f, 0.682f, 1f);
            }

            ApplyBadge(type, isEquipped, isWeapon);

            // --- Equipped Highlight ---
            if (equippedHighlight != null)
                equippedHighlight.SetActive(isEquipped);

            // --- Use Button ---
            if (useButton != null)
            {
                if (onClickUse != null)
                {
                    useButton.gameObject.SetActive(true);
                    useButton.onClick.RemoveAllListeners();
                    useButton.onClick.AddListener(OnButtonClicked);
                    if (useButtonLabel != null)
                        useButtonLabel.text = !string.IsNullOrEmpty(customButtonText) ? customButtonText : ResolveButtonLabel(type, isEquipped, isWeapon);
                }
                else
                {
                    useButton.gameObject.SetActive(false);
                }
            }

        }

        public void Deselect()
        {
            if (equippedHighlight != null) equippedHighlight.SetActive(false);
        }

        /// <summary>
        /// ล้าง Slot ให้ว่างเปล่า (ใช้ตอน Reset pool หรือ clear UI)
        /// </summary>
        public void Clear()
        {
            _currentItemName = null;
            _onClickCallback = null;

            if (itemIcon != null)        itemIcon.gameObject.SetActive(false);
            if (itemNameText != null)    itemNameText.text = "";
            if (itemQuantityText != null) itemQuantityText.text = "";
            if (itemTypeBadgeText != null) itemTypeBadgeText.text = "";
            if (equippedHighlight != null) equippedHighlight.SetActive(false);
            if (useButton != null)       useButton.gameObject.SetActive(false);
        }

        // =====================================================
        //  Private Helpers
        // =====================================================

        private void OnButtonClicked()
        {
            if (_onClickCallback != null)
            {
                _onClickCallback.Invoke(_currentItemName);
            }
        }

        private void ApplyBadge(ItemType type, bool isEquipped, bool isWeapon)
        {
            if (itemTypeBadgeText == null) return;

            string label = "";
            Color color = Color.white;

            if (isEquipped && isWeapon)
            {
                label = "EQUIPPED";
                color = ColorEquipped;
            }
            else
            {
                switch (type)
                {
                    case ItemType.RangedWeapon:
                    case ItemType.ThrowingWeapon:
                    case ItemType.MeleeWeapon:
                        label = "WEAPON";
                        color = new Color(1f, 0.443f, 0.368f, 1f);
                        break;
                    case ItemType.Ammo:
                        label = "AMMO";
                        color = new Color(1f, 0.843f, 0f, 1f);
                        break;
                    case ItemType.Potion:
                        label = "POTION";
                        color = new Color(0.368f, 1f, 0.584f, 1f);
                        break;
                    case ItemType.Bread:
                        label = "FOOD";
                        color = new Color(1f, 0.647f, 0.235f, 1f);
                        break;
                    default:
                        label = "ETC";
                        color = new Color(0.7f, 0.7f, 0.7f, 1f);
                        break;
                }
            }

            itemTypeBadgeText.text = label;
            itemTypeBadgeText.color = color;
            if (badgeBackground != null)
            {
                badgeBackground.color = new Color(color.r, color.g, color.b, 0.2f);
            }
        }

        private static string ResolveButtonLabel(ItemType type, bool isEquipped, bool isWeapon)
        {
            if (isWeapon) return isEquipped ? "Unequip" : "Equip";
            
            return type switch
            {
                ItemType.Potion => "Use",
                ItemType.Bread   => "Eat",
                _ => "Select"
            };
        }
        // Removed inner enum and resolve methods
    }
}
