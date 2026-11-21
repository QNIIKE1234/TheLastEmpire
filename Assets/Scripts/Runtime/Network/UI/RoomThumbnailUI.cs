using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// RoomThumbnailUI — แปะไว้ที่ GameObject แถวห้องแต่ละแถว (เช่น RoomThumbnail 1..5)
    /// ลาก UI ลูกมาใส่ช่องใน Inspector ได้เลย เปลี่ยนชื่อ GameObject ได้อิสระ 100%
    /// </summary>
    public class RoomThumbnailUI : MonoBehaviour
    {
        [Header("Room Row UI References (Drag & Drop in Inspector)")]
        [Tooltip("ข้อความแสดงชื่อห้อง / รหัสห้อง (เช่น RoomNameText)")]
        [SerializeField] private TMP_Text roomNameText;

        [Tooltip("ข้อความแสดงจำนวนผู้เล่นในห้อง เช่น 1/6 (เช่น ConutText)")]
        [SerializeField] private TMP_Text playerCountText;

        [Tooltip("ไอคอนแม่กุญแจ (เช่น LockImage)")]
        [SerializeField] private Image lockImage;

        [Tooltip("ปุ่มกดเข้าร่วมห้อง (เช่น JoinButton)")]
        [SerializeField] private Button joinButton;

        [Tooltip("ข้อความบนปุ่ม Join (ถ้าไม่ลาก ระบบจะดึง TMP_Text ลูกของปุ่ม Join ให้)")]
        [SerializeField] private TMP_Text joinButtonLabel;

        public TMP_Text RoomNameText => roomNameText;
        public TMP_Text PlayerCountText => playerCountText;
        public Image LockImage => lockImage;
        public Button JoinButton => joinButton;
        public TMP_Text JoinButtonLabel => joinButtonLabel;

        private void Awake()
        {
            EnsureValidTextReferences();
        }

        private void EnsureValidTextReferences()
        {
            if (playerCountText == null || playerCountText == roomNameText)
            {
                TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text t = texts[i];
                    if (t == null || t == roomNameText || t == joinButtonLabel) continue;
                    if (joinButton != null && t.transform.IsChildOf(joinButton.transform)) continue;

                    playerCountText = t;
                    break;
                }
            }
        }

        public void SetRoomData(string displayName, string countDisplay, bool isJoinable, string buttonText, Action onJoinClicked, bool isLocked = false)
        {
            EnsureValidTextReferences();
            gameObject.SetActive(true);

            if (roomNameText != null)
            {
                roomNameText.text = displayName;
            }

            if (playerCountText != null)
            {
                playerCountText.text = countDisplay;
            }

            if (lockImage != null)
            {
                lockImage.color = isLocked ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }

            if (joinButton != null)
            {
                joinButton.gameObject.SetActive(true);
                joinButton.interactable = isJoinable;
                joinButton.onClick.RemoveAllListeners();
                if (isJoinable && onJoinClicked != null)
                {
                    joinButton.onClick.AddListener(() => onJoinClicked.Invoke());
                }
            }

            TMP_Text lbl = joinButtonLabel != null ? joinButtonLabel : (joinButton != null ? joinButton.GetComponentInChildren<TMP_Text>(true) : null);
            if (lbl != null)
            {
                lbl.text = buttonText;
            }
        }

        public void HideRow()
        {
            gameObject.SetActive(false);
        }
    }
}
