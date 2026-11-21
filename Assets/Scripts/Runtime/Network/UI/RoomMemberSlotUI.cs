using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// RoomMemberSlotUI — แปะไว้ที่การ์ดสมาชิกแต่ละช่องใน `RoomInfoPanel` (เช่น `MemberPrefabs` ทั้ง 6 ช่อง)
    /// ลาก `Profile_Image/Image`, `NameText`, และปุ่มแดง `Button` (Kick) มาใส่ใน Inspector ได้เลย
    /// </summary>
    public class RoomMemberSlotUI : MonoBehaviour
    {
        [Header("Member Card UI References (Drag & Drop in Inspector)")]
        [Tooltip("รูปโปรไฟล์ตัวละครของสมาชิกช่องนี้ (เช่น Profile_Image -> Image)")]
        [SerializeField] private Image profileImage;

        [Tooltip("ข้อความชื่อผู้เล่นใต้กรอบรูป (เช่น NameText)")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("ปุ่มกากบาทสีแดงมุมขวาบนสำหรับเตะผู้เล่นออกจากห้อง (เช่น Button)")]
        [SerializeField] private Button kickButton;

        [Header("Empty Slot Behavior")]
        [Tooltip("ถ้าติ๊กถูก ช่องที่ยังไม่มีคนเข้าจะถูกซ่อนทั้งกรอบ (SetActive(false)) / ถ้าไม่ติ๊ก จะแสดงกรอบว่างเขียนว่า EMPTY")]
        [SerializeField] private bool hideCardWhenEmpty = false;

        public Image ProfileImage => profileImage;
        public TMP_Text NameText => nameText;
        public Button KickButton => kickButton;

        /// <summary>
        /// ตั้งค่าข้อมูลผู้เล่นในช่องนี้เมื่อมีคนอยู่ในสล็อต
        /// </summary>
        public void SetOccupied(string memberName, Sprite portraitSprite, bool canKick, Action onKickClicked)
        {
            gameObject.SetActive(true);

            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(memberName) ? "SURVIVOR" : memberName;
                nameText.color = Color.white;
            }

            if (profileImage != null)
            {
                profileImage.enabled = true;
                if (portraitSprite != null)
                {
                    profileImage.sprite = portraitSprite;
                }
                profileImage.color = Color.white;
            }

            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(canKick);
                kickButton.onClick.RemoveAllListeners();
                if (canKick && onKickClicked != null)
                {
                    kickButton.onClick.AddListener(() => onKickClicked.Invoke());
                }
            }
        }

        /// <summary>
        /// ตั้งค่าช่องนี้เมื่อยังไม่มีผู้เล่นเข้ามาในสล็อต (ว่าง)
        /// </summary>
        public void SetEmpty(int slotNumber)
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
                profileImage.color = new Color(0.20f, 0.22f, 0.26f, 0.35f);
            }

            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(false);
                kickButton.onClick.RemoveAllListeners();
            }
        }
    }
}
