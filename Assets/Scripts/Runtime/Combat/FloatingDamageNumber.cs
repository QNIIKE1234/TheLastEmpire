using TMPro;
using UnityEngine;

namespace TheLastEmpire
{
    public class FloatingDamageNumber : MonoBehaviour
    {
        private TextMeshPro _mainText;
        private TextMeshPro _shadowText;

        private float _lifetime = 0.85f;
        private float _age = 0f;
        private Vector3 _startPosition;
        private Vector3 _velocity;
        private float _initialScaleMultiplier = 1f;
        public static float GlobalScaleMultiplier = 1.5f; // ขยายขนาดตัวเลขดาเมจเพิ่ม 50% (1.5x)
        private Color _baseColor;
        private Color _shadowColor = new Color(0f, 0f, 0f, 0.85f);
        private System.Action<FloatingDamageNumber> _onCompleteCallback;

        private static TMP_FontAsset _cachedFontAsset;

        private void Awake()
        {
            SetupComponents();
        }

        private static TMP_FontAsset ResolveDamageFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }
            if (_cachedFontAsset != null) return _cachedFontAsset;

            TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null && loaded[i].name.IndexOf("Bungee", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _cachedFontAsset = loaded[i];
                    return _cachedFontAsset;
                }
            }
            if (loaded.Length > 0)
            {
                _cachedFontAsset = loaded[0];
            }
            return _cachedFontAsset;
        }

        private void SetupComponents()
        {
            if (_mainText != null) return;

            TMP_FontAsset fontAsset = ResolveDamageFont();

            // Drop shadow TMP object (rendered directly behind main text for crisp readability)
            GameObject shadowObj = new GameObject("ShadowText");
            shadowObj.transform.SetParent(transform, false);
            shadowObj.transform.localPosition = new Vector3(0.032f, -0.032f, 0.02f);

            _shadowText = shadowObj.AddComponent<TextMeshPro>();
            if (fontAsset != null) _shadowText.font = fontAsset;
            _shadowText.alignment = TextAlignmentOptions.Center;
            _shadowText.textWrappingMode = TextWrappingModes.NoWrap;
            _shadowText.extraPadding = true;
            _shadowText.sortingOrder = 550;
            _shadowText.rectTransform.sizeDelta = new Vector2(8f, 2f);

            // Main 3D TMP text component (matches the project's default Bungee-Regular SDF font!)
            _mainText = gameObject.GetComponent<TextMeshPro>();
            if (_mainText == null)
            {
                _mainText = gameObject.AddComponent<TextMeshPro>();
            }
            if (fontAsset != null) _mainText.font = fontAsset;
            _mainText.alignment = TextAlignmentOptions.Center;
            _mainText.textWrappingMode = TextWrappingModes.NoWrap;
            _mainText.extraPadding = true;
            _mainText.sortingOrder = 551;
            _mainText.rectTransform.sizeDelta = new Vector2(8f, 2f);
            _mainText.outlineWidth = 0.22f;
            _mainText.outlineColor = new Color32(10, 10, 14, 240);
        }

        public void Initialize(
            Vector3 worldPos,
            float damageAmount,
            bool isCrit,
            string overrideText,
            System.Action<FloatingDamageNumber> onComplete)
        {
            SetupComponents();
            _onCompleteCallback = onComplete;
            _age = 0f;

            // Keep font synced with current TMP default font asset (e.g. if switched via Global Font Switcher)
            TMP_FontAsset activeFont = ResolveDamageFont();
            if (activeFont != null)
            {
                if (_mainText.font != activeFont) _mainText.font = activeFont;
                if (_shadowText != null && _shadowText.font != activeFont) _shadowText.font = activeFont;
            }

            // Add subtle random jitter to spawn position so consecutive hits don't overlap directly
            Vector3 jitter = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.1f, 0.35f), Random.Range(-0.2f, 0.2f));
            transform.position = worldPos + jitter;
            _startPosition = transform.position;

            // Initial upward drift velocity
            float upwardSpeed = isCrit ? 2.2f : 1.7f;
            float horizontalDrift = Random.Range(-0.45f, 0.45f);
            _velocity = new Vector3(horizontalDrift, upwardSpeed, 0f);

            string displayText;
            if (!string.IsNullOrEmpty(overrideText))
            {
                displayText = overrideText;
                _baseColor = new Color(0.65f, 0.85f, 1.0f); // Cool silver-cyan for Blocked
                _mainText.fontSize = 1.8f;
                _initialScaleMultiplier = 1.0f * GlobalScaleMultiplier;
                _lifetime = 0.75f;
            }
            else if (isCrit)
            {
                displayText = $"HEADSHOT! {Mathf.RoundToInt(damageAmount)}";
                _baseColor = new Color(1.0f, 0.28f, 0.10f); // Fiery blazing orange-red
                _mainText.fontSize = 1.8f;
                _initialScaleMultiplier = 1.0f * GlobalScaleMultiplier;
                _lifetime = 1.0f;
            }
            else
            {
                displayText = Mathf.RoundToInt(damageAmount).ToString();
                _baseColor = Color.white; // Crisp pure white for normal damage
                _mainText.fontSize = 1.8f;
                _initialScaleMultiplier = 1.0f * GlobalScaleMultiplier;
                _lifetime = 0.85f;
            }

            _mainText.text = displayText;
            _mainText.fontStyle = FontStyles.Bold;
            _mainText.color = _baseColor;

            if (_shadowText != null)
            {
                _shadowText.text = displayText;
                _shadowText.fontSize = _mainText.fontSize;
                _shadowText.fontStyle = _mainText.fontStyle;
                _shadowText.color = _shadowColor;
            }

            // Billboard immediately
            UpdateBillboard();
            transform.localScale = Vector3.one * (0.6f * _initialScaleMultiplier);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float progress = Mathf.Clamp01(_age / _lifetime);

            // Pop scale animation: punch up then settle
            float scale;
            if (progress < 0.18f)
            {
                float t = progress / 0.18f;
                scale = Mathf.Lerp(0.6f, 1.25f, t) * _initialScaleMultiplier;
            }
            else if (progress < 0.40f)
            {
                float t = (progress - 0.18f) / 0.22f;
                scale = Mathf.Lerp(1.25f, 1.0f, t) * _initialScaleMultiplier;
            }
            else
            {
                scale = 1.0f * _initialScaleMultiplier;
            }
            transform.localScale = Vector3.one * scale;

            // Upward drift with slight deceleration
            _velocity.y = Mathf.Max(0.4f, _velocity.y - Time.deltaTime * 1.5f);
            transform.position += _velocity * Time.deltaTime;

            // Fade out in the last 45% of lifetime
            float alpha = 1f;
            if (progress > 0.55f)
            {
                alpha = Mathf.Lerp(1f, 0f, (progress - 0.55f) / 0.45f);
            }

            _mainText.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
            if (_shadowText != null)
            {
                _shadowText.color = new Color(0f, 0f, 0f, alpha * 0.85f);
            }

            if (_age >= _lifetime)
            {
                gameObject.SetActive(false);
                _onCompleteCallback?.Invoke(this);
            }
        }

        private void LateUpdate()
        {
            UpdateBillboard();
        }

        private void UpdateBillboard()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Align rotation to match camera facing plane
                transform.rotation = cam.transform.rotation;
            }
        }

        private static Font _cachedLegacyFont;

        public static Font GetBuiltinFont()
        {
            if (_cachedLegacyFont != null) return _cachedLegacyFont;
            try { _cachedLegacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (_cachedLegacyFont == null)
            {
                try { _cachedLegacyFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return _cachedLegacyFont;
        }
    }
}
