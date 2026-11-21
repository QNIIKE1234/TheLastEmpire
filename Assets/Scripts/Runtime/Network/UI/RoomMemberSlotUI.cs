using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// RoomMemberSlotUI — แปะไว้ที่การ์ดผู้เล่นแต่ละช่องใน `RoomInfoPanel` (`MemberPrefabs` ทั้ง 6 ช่อง)
    /// - หากผู้เล่นเพิ่งเข้าห้องและยังไม่ได้เลือกตัวละคร จะแสดงรูป Default (เช่น รูปเข็มทิศ)
    /// - ปุ่มเหลืองรูปประแจ (`selectCharacterButton`): แสดงและกดได้เฉพาะช่องของตัวเราเองเท่านั้น เพื่อเปิดหน้าเลือกตัวละคร
    /// - ปุ่มแดงกากบาท (`kickButton`): แสดงและกดได้เฉพาะหัวห้อง (Host) บนช่องของลูกทีมคนอื่น เพื่อเตะออกจากห้อง
    /// </summary>
    public class RoomMemberSlotUI : MonoBehaviour
    {
        [Header("Member Card UI References (Drag & Drop in Inspector)")]
        [Tooltip("รูปโปรไฟล์ตัวละครของสมาชิกช่องนี้ (เช่น Profile_Image -> Image)")]
        [SerializeField] private Image profileImage;

        [Tooltip("รูป Default กรณีที่ผู้เล่นยังไม่ได้เลือกตัวละคร (เช่น รูปเข็มทิศ — ถ้าไม่ลากใส่ จะใช้รูปที่ตั้งไว้บน Profile Image ในซีนเป็นค่าเริ่มต้น)")]
        [SerializeField] private Sprite defaultUnselectedSprite;

        [Tooltip("ข้อความชื่อผู้เล่นใต้กรอบรูป (เช่น NameText)")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("ปุ่มสีเหลืองรูปประแจ (ใช้ได้เฉพาะช่องของตัวเราเอง เพื่อกดเปิดหน้าเลือกตัวละคร)")]
        [SerializeField] private Button selectCharacterButton;

        [Tooltip("ปุ่มกากบาทสีแดง [X] (แสดงเฉพาะหัวห้องบนช่องของคนอื่น เพื่อเตะออกจากห้อง)")]
        [SerializeField] private Button kickButton;

        [Header("Empty Slot Behavior")]
        [Tooltip("ถ้าติ๊กถูก ช่องที่ยังไม่มีคนเข้าจะถูกซ่อนทั้งกรอบ (SetActive(false)) / ถ้าไม่ติ๊ก จะแสดงกรอบว่างเขียนว่า EMPTY")]
        [SerializeField] private bool hideCardWhenEmpty = false;

        private Sprite _cachedSceneDefaultSprite;

        public Image ProfileImage => profileImage;
        public Sprite DefaultUnselectedSprite => defaultUnselectedSprite != null ? defaultUnselectedSprite : _cachedSceneDefaultSprite;
        public TMP_Text NameText => nameText;
        public Button SelectCharacterButton => selectCharacterButton;
        public Button KickButton => kickButton;

        private void Awake()
        {
            if (profileImage != null && profileImage.sprite != null)
            {
                _cachedSceneDefaultSprite = profileImage.sprite;
            }
        }

        /// <summary>
        /// ตั้งค่าข้อมูลผู้เล่นในช่องนี้เมื่อมีผู้เล่นอยู่ในสล็อต
        /// (หาก portraitSprite เป็น null = ยังไม่ได้เลือกตัวละคร -> จะแสดงรูป Default เข็มทิศ)
        /// </summary>
        public void SetOccupied(
            string memberName,
            Sprite portraitSprite,
            Sprite fallbackDefaultSprite,
            bool isLocalPlayerSlot,
            Action onSelectCharacterClicked,
            bool canHostKickThisSlot,
            Action onKickClicked)
        {
            gameObject.SetActive(true);

            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(memberName) ? "SURVIVOR" : memberName;
                nameText.color = isLocalPlayerSlot ? new Color(1f, 0.92f, 0.25f, 1f) : Color.white;
            }

            if (profileImage != null)
            {
                profileImage.enabled = true;
                Sprite spriteToUse = portraitSprite != null
                    ? portraitSprite
                    : (defaultUnselectedSprite != null
                        ? defaultUnselectedSprite
                        : (fallbackDefaultSprite != null ? fallbackDefaultSprite : _cachedSceneDefaultSprite));

                if (spriteToUse != null)
                {
                    profileImage.sprite = spriteToUse;
                }
                profileImage.color = Color.white;
            }

            // 1. ปุ่มสีเหลืองรูปประแจ: แสดงและกดได้เฉพาะช่องของตัวเราเองเท่านั้น!
            if (selectCharacterButton != null)
            {
                selectCharacterButton.gameObject.SetActive(isLocalPlayerSlot);
                selectCharacterButton.interactable = isLocalPlayerSlot;
                selectCharacterButton.onClick.RemoveAllListeners();
                if (isLocalPlayerSlot && onSelectCharacterClicked != null)
                {
                    selectCharacterButton.onClick.AddListener(() => onSelectCharacterClicked.Invoke());
                }
            }

            // 2. ปุ่มกากบาทสีแดง [X]: แสดงเฉพาะเมื่อเราเป็นหัวห้อง (Host) และไม่ใช่ช่องของตัวเอง
            if (kickButton != null && kickButton != selectCharacterButton)
            {
                kickButton.gameObject.SetActive(canHostKickThisSlot);
                kickButton.interactable = canHostKickThisSlot;
                kickButton.onClick.RemoveAllListeners();
                if (canHostKickThisSlot && onKickClicked != null)
                {
                    kickButton.onClick.AddListener(() => onKickClicked.Invoke());
                }
            }
        }

        /// <summary>
        /// ตั้งค่าช่องนี้เมื่อยังไม่มีผู้เล่นเข้ามาในสล็อต (ว่าง)
        /// </summary>
        public void SetEmpty(int slotNumber, Sprite fallbackDefaultSprite = null)
        {
            if (hideCardWhenEmpty)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (nameText != null)
            {
                nameText.text = $"EMPTY ({slotNumber}/6)";
                nameText.color = new Color(0.55f, 0.60f, 0.68f, 0.65f);
            }

            if (profileImage != null)
            {
                Sprite defaultSp = defaultUnselectedSprite != null
                    ? defaultUnselectedSprite
                    : (fallbackDefaultSprite != null ? fallbackDefaultSprite : _cachedSceneDefaultSprite);
                if (defaultSp != null)
                {
                    profileImage.sprite = defaultSp;
                }
                profileImage.color = new Color(0.25f, 0.28f, 0.32f, 0.35f);
            }

            if (selectCharacterButton != null)
            {
                selectCharacterButton.gameObject.SetActive(false);
                selectCharacterButton.onClick.RemoveAllListeners();
            }

            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(false);
                kickButton.onClick.RemoveAllListeners();
            }
        }
    }
}
