using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Component ควบคุมการแสดงผล Cooldown ของปุ่ม Shift (Shiftskill บน HUD)
    /// รองรับทั้ง Radial Fill 360 และตัวเลขนับถอยหลัง (Cooldown Text)
    /// </summary>
    public class ShiftSkillHUD : MonoBehaviour
    {
        public static ShiftSkillHUD Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("Image ซ้อนทับแสดงสถานะคูลดาวน์ (Filled - Radial 360)")]
        [SerializeField] private Image cooldownImage;

        [Tooltip("Image ไอคอนพื้นหลัง")]
        [SerializeField] private Image iconImage;

        [Tooltip("Text แสดงเวลานับถอยหลัง เช่น 3.5s")]
        [SerializeField] private TMP_Text cooldownText;

        [Header("Tuning")]
        [Tooltip("true = ค่อยๆ เติมภาพสว่างขึ้นจาก 0 ถึง 1 เมื่อคูลดาวน์ลดลง (เหมาะกับกรณี Cooldown เป็นรูปสว่างวางทับเงาดำ)\nfalse = ภาพเต็ม 1 แล้วลดลงเหลือ 0 (กรณี Cooldown เป็น Mask สีดำ)")]
        [SerializeField] private bool fillRecovers = true;

        private PlayerController _player;
        private bool _wasOnCooldown = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoHookScene()
        {
            if (Instance == null)
            {
                ShiftSkillHUD existing = UnityEngine.Object.FindFirstObjectByType<ShiftSkillHUD>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject skillObj = GameObject.Find("Shiftskill");
                    if (skillObj != null)
                    {
                        Instance = skillObj.AddComponent<ShiftSkillHUD>();
                    }
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            FindPlayer();
        }

        public void ResolveReferences()
        {
            if (cooldownImage == null)
            {
                Transform cdT = transform.Find("Cooldown");
                if (cdT != null) cooldownImage = cdT.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                Transform iconT = transform.Find("Icon");
                if (iconT != null) iconImage = iconT.GetComponent<Image>();
            }

            if (cooldownText == null)
            {
                Transform textT = transform.Find("CooldownText");
                if (textT != null)
                {
                    cooldownText = textT.GetComponent<TMP_Text>();
                }
                else
                {
                    // สร้าง CooldownText dynamic ตรงกลาง icon สไตล์เกม Action
                    GameObject textObj = new GameObject("CooldownText");
                    textObj.transform.SetParent(transform, false);
                    RectTransform rt = textObj.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
                    tmp.fontSize = 24f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = Color.white;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableWordWrapping = false;
                    tmp.raycastTarget = false;

                    // Black Outline เพื่อให้อ่านง่ายบนทุกภาพพื้นหลัง
                    tmp.outlineWidth = 0.25f;
                    tmp.outlineColor = new Color32(0, 0, 0, 255);

                    cooldownText = tmp;
                    textObj.SetActive(false);
                }
            }

            // ถ้า cooldownImage เป็น Filled Radial 360 ให้แน่ใจว่า raycastTarget ไม่ขวาง
            if (cooldownImage != null)
            {
                cooldownImage.raycastTarget = false;
            }
            if (iconImage != null)
            {
                iconImage.raycastTarget = false;
            }
        }

        private void FindPlayer()
        {
            if (_player == null)
            {
                _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            }
        }

        private void Update()
        {
            if (_player == null)
            {
                FindPlayer();
                if (_player == null) return;
            }

            ResolveReferences();

            IShiftAbility ability = _player.ActiveShiftAbility;
            if (ability == null)
            {
                if (cooldownImage != null) cooldownImage.fillAmount = fillRecovers ? 1f : 0f;
                if (cooldownText != null) cooldownText.gameObject.SetActive(false);
                return;
            }

            float cdRemaining = ability.CooldownRemaining;
            float cdDuration = ability.CooldownDuration;

            bool onCooldown = cdRemaining > 0.05f && cdDuration > 0.01f;

            if (onCooldown)
            {
                _wasOnCooldown = true;
                float ratio = Mathf.Clamp01(cdRemaining / cdDuration);

                if (cooldownImage != null)
                {
                    cooldownImage.gameObject.SetActive(true);
                    cooldownImage.fillAmount = fillRecovers ? (1f - ratio) : ratio;
                }

                if (cooldownText != null)
                {
                    cooldownText.gameObject.SetActive(true);
                    cooldownText.text = $"{cdRemaining:F1}";
                }
            }
            else
            {
                // เมื่อเพิ่งหายจาก Cooldown เล่น Pop Effect
                if (_wasOnCooldown)
                {
                    _wasOnCooldown = false;
                    TriggerReadyPop();
                }

                if (cooldownImage != null)
                {
                    cooldownImage.fillAmount = fillRecovers ? 1f : 0f;
                }

                if (cooldownText != null)
                {
                    cooldownText.gameObject.SetActive(false);
                }
            }
        }

        private void TriggerReadyPop()
        {
            // ทำ Scale animation สั้นๆ ให้รู้สึกว่าสกิลพร้อมแล้ว
            transform.localScale = Vector3.one * 1.15f;
            LeanTweenOrSimpleReturn();
        }

        private void LeanTweenOrSimpleReturn()
        {
            transform.localScale = Vector3.one;
        }
    }
}
