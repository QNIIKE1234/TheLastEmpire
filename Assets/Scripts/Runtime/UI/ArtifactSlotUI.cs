using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// ArtifactSlotUI — สคริปต์เฉพาะสำหรับช่องติดตั้ง Artifact (A1, A2, A3, A4) ในหน้าต่าง WeaponUpgrade_HUD
    /// รองรับครบ:
    /// - แสดงไอคอนของ Artifact ที่ติดตั้งอยู่ในช่อง
    /// - ลาก Artifact จากรายการฝั่งซ้ายมาปล่อยลงช่องนี้เพื่อติดตั้ง (IDropHandler)
    /// - ลาก Artifact ออกจากช่องนี้ไปปล่อยฝั่งซ้ายหรือนอกกรอบเพื่อถอดคืนกระเป๋า (IBeginDrag / IDrag / IEndDrag)
    /// - ลากสลับตำแหน่งระหว่างช่อง A1 - A4 ได้โดยตรง
    /// - คลิกที่ช่องที่มี Artifact อยู่เพื่อถอดคืนกระเป๋าได้ทันที (IPointerClickHandler)
    /// </summary>
    public class ArtifactSlotUI : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        public static ArtifactSlotUI DraggedSlot { get; private set; }
        public static bool ConsumedByDropTarget { get; set; } = false;

        private static GameObject _dragGhostObj;
        private static Image _dragGhostImage;
        private static TMP_Text _dragGhostBadge;

        [Header("UI References")]
        [Tooltip("Image ลูกสำหรับแสดงไอคอน Artifact (เว้นว่างไว้จะหา child ชื่อ icon / Icon อัตโนมัติ)")]
        [SerializeField] private Image iconImage;

        [Tooltip("Object ไฮไลต์ตอนเอาเมาส์ชี้หรือลากมาจ่อ (Optional)")]
        [SerializeField] private GameObject highlightObject;

        [Tooltip("ข้อความป้ายช่อง เช่น A1 (Optional)")]
        [SerializeField] private TMP_Text slotLabelText;

        private WeaponUpgradeUI _ownerUI;
        private int _slotIndex = -1;
        private string _equippedArtifactName;
        private bool _wasDragged;

        public int SlotIndex => _slotIndex;
        public string EquippedArtifactName => _equippedArtifactName;
        public bool IsOccupied => !string.IsNullOrEmpty(_equippedArtifactName);

        private void Awake()
        {
            // ให้แน่ใจว่ามี Graphic รับ Raycast เพื่อให้คลิกและลากวางได้ทั้งช่อง
            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = Color.clear;
                bg.raycastTarget = true;
            }

            AutoBindReferences();
            if (highlightObject != null) highlightObject.SetActive(false);
        }

        public void AutoBindReferences()
        {
            if (iconImage == null)
            {
                Transform iconT = transform.Find("icon") ?? transform.Find("Icon");
                if (iconT != null)
                {
                    iconImage = iconT.GetComponent<Image>();
                }
            }
        }

        public void Initialize(WeaponUpgradeUI ownerUI, int slotIndex)
        {
            _ownerUI = ownerUI;
            _slotIndex = slotIndex;
            AutoBindReferences();

            if (slotLabelText != null && string.IsNullOrEmpty(slotLabelText.text))
            {
                slotLabelText.text = $"A{slotIndex + 1}";
            }
        }

        public void SetArtifact(string artifactName, Sprite icon)
        {
            AutoBindReferences();
            _equippedArtifactName = artifactName;

            bool hasItem = !string.IsNullOrEmpty(artifactName);
            if (iconImage != null)
            {
                iconImage.sprite = hasItem ? icon : null;
                iconImage.enabled = hasItem && icon != null;
                iconImage.color = hasItem ? Color.white : new Color(1f, 1f, 1f, 0f);
                iconImage.raycastTarget = false;
            }
        }

        public void Clear()
        {
            SetArtifact(null, null);
            SetHighlight(false);
        }

        public void SetHighlight(bool active)
        {
            if (highlightObject != null)
            {
                highlightObject.SetActive(active);
            }
        }

        // =====================================================
        //  Pointer Hover & Click (คลิกเพื่อถอด)
        // =====================================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsOccupied || !string.IsNullOrEmpty(InventoryItemSlot.DraggedItemName) || DraggedSlot != null)
            {
                SetHighlight(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_wasDragged)
            {
                _wasDragged = false;
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!IsOccupied || _ownerUI == null) return;

            _ownerUI.UnequipArtifactAtSlot(_slotIndex);
        }

        // =====================================================
        //  Drag OUT (ลากออกจากช่องเพื่อถอด หรือลากสลับช่อง)
        // =====================================================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsOccupied || _ownerUI == null) return;

            _wasDragged = true;
            DraggedSlot = this;
            ConsumedByDropTarget = false;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                EnsureDragGhost(rootCanvas);

                if (iconImage != null && iconImage.sprite != null)
                {
                    _dragGhostImage.sprite = iconImage.sprite;
                    _dragGhostImage.color = Color.white;
                }
                else
                {
                    _dragGhostImage.sprite = null;
                    _dragGhostImage.color = new Color(1f, 1f, 1f, 0.4f);
                }

                if (_dragGhostBadge != null)
                {
                    _dragGhostBadge.text = "";
                    _dragGhostBadge.gameObject.SetActive(false);
                }

                UpdateGhostPosition(rootCanvas, eventData.position);
                _dragGhostObj.SetActive(true);
                _dragGhostObj.transform.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (DraggedSlot != this || _dragGhostObj == null) return;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                UpdateGhostPosition(rootCanvas, eventData.position);
            }

            if (_dragGhostBadge != null && _ownerUI != null)
            {
                if (_ownerUI.TryGetAttachSlotDragPreview(this, eventData.position, eventData.pressEventCamera, out string badgeText, out Color badgeColor))
                {
                    _dragGhostBadge.gameObject.SetActive(true);
                    _dragGhostBadge.text = badgeText;
                    _dragGhostBadge.color = badgeColor;
                }
                else
                {
                    _dragGhostBadge.gameObject.SetActive(false);
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (DraggedSlot == this)
            {
                DraggedSlot = null;
            }

            if (_dragGhostObj != null) _dragGhostObj.SetActive(false);
            if (_dragGhostBadge != null) _dragGhostBadge.gameObject.SetActive(false);

            if (!ConsumedByDropTarget && _ownerUI != null && IsOccupied)
            {
                _ownerUI.HandleAttachSlotDragEnd(this, eventData.position, eventData.pressEventCamera);
            }

            ConsumedByDropTarget = false;
            _wasDragged = false;
        }

        // =====================================================
        //  Drop ONTO this slot (ลากเข้าเพื่อติดตั้ง หรือสลับช่อง)
        // =====================================================

        public void OnDrop(PointerEventData eventData)
        {
            SetHighlight(false);
            if (_ownerUI == null) return;

            // 1. ลากมาจากช่อง ArtifactSlotUI อีกช่อง -> สลับช่อง
            if (DraggedSlot != null && DraggedSlot != this)
            {
                if (_ownerUI.SwapArtifactSlots(DraggedSlot.SlotIndex, _slotIndex))
                {
                    ConsumedByDropTarget = true;
                }
                return;
            }

            // 2. ลากมาจากรายการ Artifact ฝั่งซ้าย (InventoryItemSlot) -> ติดตั้งลงช่องนี้โดยตรง
            if (!string.IsNullOrEmpty(InventoryItemSlot.DraggedItemName))
            {
                InventoryItemSlot sourceSlot = eventData.pointerDrag != null
                    ? eventData.pointerDrag.GetComponent<InventoryItemSlot>()
                    : null;

                if (sourceSlot != null && _ownerUI.IsArtifactListSlot(sourceSlot))
                {
                    if (_ownerUI.EquipArtifactFromListIntoSlot(InventoryItemSlot.DraggedItemName, _slotIndex))
                    {
                        InventoryItemSlot.ConsumedByDropTarget = true;
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (DraggedSlot == this)
            {
                DraggedSlot = null;
                if (_dragGhostObj != null) _dragGhostObj.SetActive(false);
                if (_dragGhostBadge != null) _dragGhostBadge.gameObject.SetActive(false);
            }
            SetHighlight(false);
        }

        private static void EnsureDragGhost(Canvas rootCanvas)
        {
            if (_dragGhostObj == null)
            {
                _dragGhostObj = new GameObject("ArtifactSlot_DragGhost");
                _dragGhostObj.transform.SetParent(rootCanvas.transform, false);

                _dragGhostImage = _dragGhostObj.AddComponent<Image>();
                _dragGhostImage.raycastTarget = false;

                CanvasGroup cg = _dragGhostObj.AddComponent<CanvasGroup>();
                cg.alpha = 0.88f;
                cg.blocksRaycasts = false;

                RectTransform rt = _dragGhostObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(76f, 76f);

                GameObject badgeObj = new GameObject("ArtifactDragBadge");
                badgeObj.transform.SetParent(_dragGhostObj.transform, false);
                _dragGhostBadge = badgeObj.AddComponent<TextMeshProUGUI>();
                _dragGhostBadge.fontSize = 18f;
                _dragGhostBadge.fontStyle = FontStyles.Bold;
                _dragGhostBadge.alignment = TextAlignmentOptions.Center;
                _dragGhostBadge.raycastTarget = false;

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
            }
        }

        private static void UpdateGhostPosition(Canvas rootCanvas, Vector2 screenPos)
        {
            if (_dragGhostObj == null || rootCanvas == null) return;

            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _dragGhostObj.transform.position = screenPos;
            }
            else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                rootCanvas.transform as RectTransform, screenPos, rootCanvas.worldCamera, out Vector3 worldPos))
            {
                _dragGhostObj.transform.position = worldPos;
            }
            else
            {
                _dragGhostObj.transform.position = screenPos;
            }
        }
    }
}
