using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Component สำหรับ Quick Slot 1 ช่องใน QuickSlot_HUD (ช่อง 1 - 5)
    /// รองรับทั้งการคลิกใช้/เคลียร์ของ และรับ Drag & Drop จาก Inventory
    /// </summary>
    public class QuickSlotUIItem : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private Image frameImage;

        public int SlotIndex => slotIndex;

        public void Setup(int index)
        {
            slotIndex = index;
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            if (iconImage == null)
            {
                Transform iconT = transform.Find("Icon");
                if (iconT != null) iconImage = iconT.GetComponent<Image>();
            }

            if (keyText == null)
            {
                Transform textT = transform.Find("Text");
                if (textT != null) keyText = textT.GetComponent<TMP_Text>();
            }

            if (frameImage == null)
            {
                frameImage = GetComponent<Image>();
            }

            if (quantityText == null)
            {
                Transform countT = transform.Find("CountText");
                if (countT != null)
                {
                    quantityText = countT.GetComponent<TMP_Text>();
                }
                else
                {
                    // สร้าง CountText dynamic สำหรับแสดงจำนวน x2, x3
                    GameObject countObj = new GameObject("CountText");
                    countObj.transform.SetParent(transform, false);
                    RectTransform rt = countObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0.4f);
                    rt.offsetMin = new Vector2(6f, 4f);
                    rt.offsetMax = new Vector2(-6f, 0f);

                    quantityText = countObj.AddComponent<TextMeshProUGUI>();
                    quantityText.fontSize = 20f;
                    quantityText.fontStyle = FontStyles.Bold;
                    quantityText.color = new Color(1f, 0.9f, 0.2f, 1f); // Yellow
                    quantityText.alignment = TextAlignmentOptions.BottomLeft;
                }
            }

            // ป้องกันไม่ให้ Child components บล็อกการรับ Drop และ Click event บน Frame
            if (iconImage != null) iconImage.raycastTarget = false;
            if (keyText != null) keyText.raycastTarget = false;
            if (quantityText != null) quantityText.raycastTarget = false;
            if (frameImage != null) frameImage.raycastTarget = true;
        }

        public void SetData(string itemName, Sprite icon, int count, bool isConsumable, bool isEquipped)
        {
            ResolveReferences();

            bool hasItem = !string.IsNullOrEmpty(itemName);

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasItem && icon != null);
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    if (isConsumable && count <= 0)
                        iconImage.color = new Color(1f, 1f, 1f, 0.4f); // Dim out if empty count
                    else
                        iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                }
            }

            if (keyText != null)
            {
                keyText.text = $"{slotIndex + 1}";
            }

            if (quantityText != null)
            {
                if (hasItem && isConsumable)
                {
                    quantityText.gameObject.SetActive(true);
                    quantityText.text = $"x{count}";
                    quantityText.color = count > 0 ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(0.8f, 0.3f, 0.3f, 1f);
                }
                else
                {
                    quantityText.gameObject.SetActive(false);
                }
            }

            if (frameImage != null)
            {
                // ไฮไลต์ถ้ากำลังถืออาวุธในช่องนี้อยู่
                if (isEquipped)
                {
                    frameImage.color = new Color(0f, 0.898f, 1f, 1f); // Cyan
                }
                else
                {
                    frameImage.color = hasItem ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.6f);
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(InventoryItemSlot.DraggedItemName))
            {
                string droppedItem = InventoryItemSlot.DraggedItemName;
                if (QuickSlotController.Instance != null)
                {
                    QuickSlotController.Instance.AssignSlot(slotIndex, droppedItem);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (QuickSlotController.Instance == null) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                QuickSlotController.Instance.UseSlot(slotIndex);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                QuickSlotController.Instance.ClearSlot(slotIndex);
            }
        }
    }
}
