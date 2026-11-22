using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Component สำหรับ Item Slot 1 ช่องใน Inventory UI.
    /// รองรับทั้ง Hover + กดเลข 1-5 และ Drag & Drop ไปยัง Quick Slot
    /// </summary>
    public class InventoryItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public static string DraggedItemName { get; private set; }
        public static InventoryItemSlot HoveredSlot { get; private set; }
        public static bool ConsumedByDropTarget { get; set; } = false;

        private static GameObject _dragGhostObj;
        private static Image _dragGhostImage;
        private static TMP_Text _dragGhostDropBadge;

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

        public string CurrentItemName => _currentItemName;

        private void Awake()
        {
            // ตรวจสอบให้แน่ใจว่ามี Graphic รับ Raycast เพื่อให้รับ Hover และ Drag ได้ทั่วทั้งช่อง
            Image img = GetComponent<Image>();
            if (img == null)
            {
                img = gameObject.AddComponent<Image>();
                img.color = Color.clear;
                img.raycastTarget = true;
            }
        }

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
                        label = "MEDICINE";
                        color = new Color(0.368f, 1f, 0.584f, 1f);
                        break;
                    case ItemType.Bread:
                        label = "FOOD";
                        color = new Color(1f, 0.647f, 0.235f, 1f);
                        break;
                    case ItemType.Water:
                        label = "DRINK";
                        color = new Color(0.25f, 0.77f, 1f, 1f);
                        break;
                    case ItemType.Material:
                        label = "MATERIAL";
                        color = new Color(0.35f, 0.85f, 1f, 1f);
                        break;
                    case ItemType.Artifact:
                        label = "ARTIFACT";
                        color = new Color(0.82f, 0.45f, 1f, 1f);
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
                ItemType.Water   => "Drink",
                _ => "Select"
            };
        }

        // =====================================================
        //  Hover & Drag & Drop Handlers
        // =====================================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(_currentItemName) && _currentItemName != "- Empty -")
            {
                HoveredSlot = this;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (HoveredSlot == this)
            {
                HoveredSlot = null;
            }
        }

        private void OnDisable()
        {
            if (HoveredSlot == this)
            {
                HoveredSlot = null;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_currentItemName) || _currentItemName == "- Empty -")
                return;

            DraggedItemName = _currentItemName;
            ConsumedByDropTarget = false;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                if (_dragGhostObj == null)
                {
                    _dragGhostObj = new GameObject("Inventory_DragGhost");
                    _dragGhostObj.transform.SetParent(rootCanvas.transform, false);

                    _dragGhostImage = _dragGhostObj.AddComponent<Image>();
                    _dragGhostImage.raycastTarget = false; // สำคัญมาก ห้ามบล็อก OnDrop

                    CanvasGroup cg = _dragGhostObj.AddComponent<CanvasGroup>();
                    cg.alpha = 0.85f;
                    cg.blocksRaycasts = false;

                    RectTransform rt = _dragGhostObj.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(80f, 80f);

                    GameObject badgeObj = new GameObject("DropHintBadge");
                    badgeObj.transform.SetParent(_dragGhostObj.transform, false);
                    _dragGhostDropBadge = badgeObj.AddComponent<TextMeshProUGUI>();
                    _dragGhostDropBadge.fontSize = 18f;
                    _dragGhostDropBadge.fontStyle = FontStyles.Bold;
                    _dragGhostDropBadge.alignment = TextAlignmentOptions.Center;
                    _dragGhostDropBadge.raycastTarget = false;
                    RectTransform brt = badgeObj.GetComponent<RectTransform>();
                    brt.anchorMin = new Vector2(0.5f, 0f);
                    brt.anchorMax = new Vector2(0.5f, 0f);
                    brt.pivot = new Vector2(0.5f, 1f);
                    brt.anchoredPosition = new Vector2(0f, -4f);
                    brt.sizeDelta = new Vector2(180f, 28f);
                }
                else
                {
                    _dragGhostObj.transform.SetParent(rootCanvas.transform, false);
                    _dragGhostObj.SetActive(true);
                }

                if (_dragGhostDropBadge != null)
                {
                    _dragGhostDropBadge.text = "";
                    _dragGhostDropBadge.gameObject.SetActive(false);
                }

                if (itemIcon != null && itemIcon.sprite != null)
                {
                    _dragGhostImage.sprite = itemIcon.sprite;
                    _dragGhostImage.color = Color.white;
                }
                else
                {
                    _dragGhostImage.sprite = null;
                    _dragGhostImage.color = new Color(1f, 1f, 1f, 0.4f);
                }

                UpdateGhostPosition(rootCanvas, eventData.position);
                _dragGhostObj.transform.SetAsLastSibling();
            }
        }

        private static bool IsAltHeld()
        {
            if (Keyboard.current != null &&
                (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed))
            {
                return true;
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                return true;
            }
#endif
            return false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(DraggedItemName) || _dragGhostObj == null) return;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                UpdateGhostPosition(rootCanvas, eventData.position);
            }

            // แสดงข้อความบอกสถานะการติดตั้ง Artifact เมื่อลากไปฝั่งอาวุธใน WeaponUpgradeUI
            if (_dragGhostDropBadge != null && WeaponUpgradeUI.HasInstance && WeaponUpgradeUI.Instance.IsOpen && WeaponUpgradeUI.Instance.IsSlotOwnedByWeaponUpgradeUI(this))
            {
                if (WeaponUpgradeUI.Instance.TryGetDragPreview(this, eventData.position, eventData.pressEventCamera, out string badgeText, out Color badgeColor))
                {
                    _dragGhostDropBadge.gameObject.SetActive(true);
                    _dragGhostDropBadge.text = badgeText;
                    _dragGhostDropBadge.color = badgeColor;
                }
                else
                {
                    _dragGhostDropBadge.gameObject.SetActive(false);
                }
                return;
            }

            // แสดงข้อความบอกสถานะการฝาก/ถอนเมื่อลากข้ามฝั่งใน StorageUI
            if (_dragGhostDropBadge != null && StorageUI.HasInstance && StorageUI.Instance.IsOpen && StorageUI.Instance.IsSlotOwnedByStorageUI(this))
            {
                bool isAltHeld = IsAltHeld();
                if (StorageUI.Instance.TryGetDragPreview(this, eventData.position, eventData.pressEventCamera, isAltHeld, out string badgeText, out Color badgeColor))
                {
                    _dragGhostDropBadge.gameObject.SetActive(true);
                    _dragGhostDropBadge.text = badgeText;
                    _dragGhostDropBadge.color = badgeColor;
                }
                else
                {
                    _dragGhostDropBadge.gameObject.SetActive(false);
                }
                return;
            }

            // แสดงข้อความบอกสถานะการโยนเมื่อลากไอเทมออกนอกกรอบกระเป๋า
            if (_dragGhostDropBadge != null && InventoryUI.Instance != null && InventoryUI.Instance.IsOpen && InventoryUI.Instance.IsSlotOwnedByInventory(this))
            {
                bool outsideBag = InventoryUI.Instance.IsPointerOutsideInventoryPanel(eventData.position, eventData.pressEventCamera);
                if (outsideBag)
                {
                    bool isAltHeld = IsAltHeld();
                    int totalCount = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(DraggedItemName) : 1;
                    _dragGhostDropBadge.gameObject.SetActive(true);
                    if (isAltHeld && totalCount > 1)
                    {
                        _dragGhostDropBadge.text = $"DROP ALL (x{totalCount})";
                        _dragGhostDropBadge.color = new Color(1f, 0.42f, 0.22f, 1f);
                    }
                    else
                    {
                        _dragGhostDropBadge.text = "DROP x1";
                        _dragGhostDropBadge.color = new Color(1f, 0.88f, 0.25f, 1f);
                    }
                }
                else
                {
                    _dragGhostDropBadge.gameObject.SetActive(false);
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            string draggedItem = DraggedItemName;
            DraggedItemName = null;

            if (_dragGhostObj != null)
            {
                _dragGhostObj.SetActive(false);
            }
            if (_dragGhostDropBadge != null)
            {
                _dragGhostDropBadge.gameObject.SetActive(false);
            }

            // ถ้าเปิด WeaponUpgradeUI อยู่และลาก Artifact ไปปล่อยบนแผงอาวุธ -> ติดตั้งทันที!
            if (!ConsumedByDropTarget &&
                !string.IsNullOrEmpty(draggedItem) &&
                WeaponUpgradeUI.HasInstance &&
                WeaponUpgradeUI.Instance.IsOpen &&
                WeaponUpgradeUI.Instance.IsSlotOwnedByWeaponUpgradeUI(this))
            {
                if (WeaponUpgradeUI.Instance.TryHandleArtifactDrop(this, eventData.position, eventData.pressEventCamera))
                {
                    ConsumedByDropTarget = true;
                }
            }

            // ถ้าเปิด StorageUI อยู่และลากปล่อยข้ามฝั่ง (Inventory <-> Storage) -> ฝาก/ถอนทันที!
            if (!ConsumedByDropTarget &&
                !string.IsNullOrEmpty(draggedItem) &&
                StorageUI.HasInstance &&
                StorageUI.Instance.IsOpen &&
                StorageUI.Instance.IsSlotOwnedByStorageUI(this))
            {
                if (StorageUI.Instance.TryHandleSlotDrop(this, eventData.position, eventData.pressEventCamera, IsAltHeld()))
                {
                    ConsumedByDropTarget = true;
                }
            }

            // ถ้าไม่ได้ปล่อยลง QuickSlot และลากปล่อยออกนอกกรอบ Panel กระเป๋า -> โยนไอเทมลงพื้น!
            if (!ConsumedByDropTarget &&
                !string.IsNullOrEmpty(draggedItem) &&
                InventoryUI.Instance != null &&
                InventoryUI.Instance.IsOpen &&
                InventoryUI.Instance.IsSlotOwnedByInventory(this))
            {
                if (InventoryUI.Instance.IsPointerOutsideInventoryPanel(eventData.position, eventData.pressEventCamera))
                {
                    InventoryUI.Instance.DropItemFromInventory(draggedItem, dropAll: IsAltHeld());
                }
            }

            ConsumedByDropTarget = false;
        }

        private void UpdateGhostPosition(Canvas rootCanvas, Vector2 screenPos)
        {
            if (_dragGhostObj == null || rootCanvas == null) return;

            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _dragGhostObj.transform.position = screenPos;
            }
            else
            {
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rootCanvas.transform as RectTransform, screenPos, rootCanvas.worldCamera, out Vector3 worldPoint))
                {
                    _dragGhostObj.transform.position = worldPoint;
                }
                else
                {
                    _dragGhostObj.transform.position = screenPos;
                }
            }
        }
    }
}
