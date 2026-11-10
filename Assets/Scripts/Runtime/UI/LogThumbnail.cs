using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Component representing a single notification row inside Log_HUD (LogThumbnail).
    /// Displays an item/building Icon and status Description text, then fades out after ~2 seconds.
    /// </summary>
    public class LogThumbnail : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private CanvasGroup canvasGroup;

        private RectTransform _rectTransform;
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }
                return _rectTransform;
            }
        }

        public Image IconImage => iconImage;
        public TMP_Text DescriptionText => descriptionText;

        public string ItemKey { get; private set; }
        public string Prefix { get; private set; }
        public string DisplayName { get; private set; }
        public int Quantity { get; private set; }
        public float ElapsedTime { get; set; }
        public float CreatedAtUnscaledTime { get; private set; }

        private void Awake()
        {
            AutoBindIfNeeded();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoBindIfNeeded();
        }
#endif

        [ContextMenu("Auto Bind UI Elements")]
        public void AutoBindIfNeeded()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            if (iconImage == null)
            {
                Transform iconTrans = transform.Find("Icon");
                if (iconTrans != null)
                {
                    iconImage = iconTrans.GetComponent<Image>();
                }

                if (iconImage == null)
                {
                    Image[] images = GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < images.Length; i++)
                    {
                        if (images[i].gameObject != gameObject)
                        {
                            iconImage = images[i];
                            break;
                        }
                    }
                }
            }

            if (descriptionText == null)
            {
                Transform desTrans = transform.Find("Des");
                if (desTrans != null)
                {
                    descriptionText = desTrans.GetComponent<TMP_Text>();
                }

                if (descriptionText == null)
                {
                    descriptionText = GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        public void Setup(string itemKey, string prefix, string displayName, int quantity, Sprite icon, Sprite fallbackIcon)
        {
            AutoBindIfNeeded();

            ItemKey = itemKey;
            Prefix = prefix;
            DisplayName = displayName;
            Quantity = Mathf.Max(1, quantity);
            ElapsedTime = 0f;
            CreatedAtUnscaledTime = Time.unscaledTime;

            if (iconImage != null)
            {
                Sprite finalIcon = icon != null ? icon : fallbackIcon;
                if (finalIcon != null)
                {
                    iconImage.sprite = finalIcon;
                    iconImage.enabled = true;
                    iconImage.color = Color.white;
                    iconImage.preserveAspect = true;
                }
            }

            RefreshText();
            SetAlpha(1f);
        }

        public void AddQuantity(int extraQuantity)
        {
            Quantity += Mathf.Max(1, extraQuantity);
            ElapsedTime = 0f;
            RefreshText();
            SetAlpha(1f);
        }

        private void RefreshText()
        {
            if (descriptionText == null) return;

            if (string.Equals(ItemKey, "__MONEY__", StringComparison.Ordinal))
            {
                descriptionText.text = $"{Prefix} ${Quantity}";
            }
            else
            {
                descriptionText.text = $"{Prefix} {DisplayName} x{Quantity}";
            }
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}

