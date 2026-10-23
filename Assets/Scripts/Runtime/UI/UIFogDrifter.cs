using UnityEngine;
using UnityEngine.UI;

namespace TheLastEmpire
{
    /// <summary>
    /// Component for animating atmospheric smoke, fog, and mist layers on the UI Canvas.
    /// Provides gentle drifting, swaying, and alpha breathing pulses to bring scenes alive.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFogDrifter : MonoBehaviour
    {
        [Header("Movement & Drifting")]
        [Tooltip("Horizontal drift speed in units per second.")]
        [SerializeField] private float driftSpeedX = 15f;

        [Tooltip("Vertical floating speed in units per second.")]
        [SerializeField] private float driftSpeedY = 4f;

        [Tooltip("If true, the smoke sways back and forth smoothly within maxSwayRange.")]
        [SerializeField] private bool useSway = true;

        [SerializeField] private float maxSwayRangeX = 60f;
        [SerializeField] private float maxSwayRangeY = 20f;

        [Header("Alpha Breathing Pulse")]
        [SerializeField] private bool enableAlphaPulse = true;
        [Range(0.05f, 1f)] [SerializeField] private float minAlpha = 0.30f;
        [Range(0.05f, 1f)] [SerializeField] private float maxAlpha = 0.65f;
        [SerializeField] private float pulseFrequency = 0.4f;

        private RectTransform _rectTransform;
        private Image _image;
        private Vector2 _initialAnchoredPos;
        private float _timeOffset;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _initialAnchoredPos = _rectTransform.anchoredPosition;
            _timeOffset = Random.Range(0f, 100f);

            // Auto-assign smoke sprite if empty
            if (_image != null && _image.sprite == null)
            {
                Sprite smokeSprite = Resources.Load<Sprite>("Image/UI/Smoke_Puff_VFX") 
                                  ?? Resources.Load<Sprite>("Image/UI/Smoke_Horizontal_Fog");
                if (smokeSprite != null)
                {
                    _image.sprite = smokeSprite;
                    _image.color = new Color(0.85f, 0.80f, 0.95f, 0.50f); // Moody atmospheric purple-white tint
                }
            }
        }

        private void Update()
        {
            float t = Time.time + _timeOffset;

            // 1. Drifting / Swaying Position
            if (useSway)
            {
                float offsetX = Mathf.Sin(t * (driftSpeedX * 0.05f)) * maxSwayRangeX;
                float offsetY = Mathf.Cos(t * (driftSpeedY * 0.05f)) * maxSwayRangeY;
                _rectTransform.anchoredPosition = _initialAnchoredPos + new Vector2(offsetX, offsetY);
            }
            else
            {
                _rectTransform.anchoredPosition += new Vector2(driftSpeedX, driftSpeedY) * Time.deltaTime;
            }

            // 2. Alpha Breathing
            if (enableAlphaPulse && _image != null)
            {
                float alphaNormalized = (Mathf.Sin(t * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
                float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, alphaNormalized);
                Color c = _image.color;
                c.a = currentAlpha;
                _image.color = c;
            }
        }
    }
}
