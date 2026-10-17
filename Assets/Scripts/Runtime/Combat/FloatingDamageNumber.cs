using UnityEngine;

namespace TheLastEmpire
{
    public class FloatingDamageNumber : MonoBehaviour
    {
        private TextMesh _mainText;
        private TextMesh _shadowText;
        private MeshRenderer _mainRenderer;
        private MeshRenderer _shadowRenderer;

        private float _lifetime = 0.85f;
        private float _age = 0f;
        private Vector3 _startPosition;
        private Vector3 _velocity;
        private float _initialScaleMultiplier = 1f;
        private Color _baseColor;
        private Color _shadowColor = new Color(0f, 0f, 0f, 0.85f);
        private System.Action<FloatingDamageNumber> _onCompleteCallback;

        private void Awake()
        {
            SetupComponents();
        }

        private void SetupComponents()
        {
            if (_mainText != null) return;

            Font font = GetBuiltinFont();

            // Shadow text object (rendered behind main text)
            GameObject shadowObj = new GameObject("ShadowText");
            shadowObj.transform.SetParent(transform, false);
            shadowObj.transform.localPosition = new Vector3(0.035f, -0.035f, 0.02f);

            _shadowText = shadowObj.AddComponent<TextMesh>();
            _shadowText.font = font;
            _shadowText.alignment = TextAlignment.Center;
            _shadowText.anchor = TextAnchor.MiddleCenter;
            _shadowRenderer = shadowObj.GetComponent<MeshRenderer>();
            if (_shadowRenderer != null && font != null)
            {
                _shadowRenderer.material = font.material;
                _shadowRenderer.sortingOrder = 550;
            }

            // Main text component
            _mainText = gameObject.GetComponent<TextMesh>();
            if (_mainText == null)
            {
                _mainText = gameObject.AddComponent<TextMesh>();
            }
            _mainText.font = font;
            _mainText.alignment = TextAlignment.Center;
            _mainText.anchor = TextAnchor.MiddleCenter;

            _mainRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_mainRenderer != null && font != null)
            {
                _mainRenderer.material = font.material;
                _mainRenderer.sortingOrder = 551;
            }
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
                _mainText.fontSize = 28;
                _mainText.characterSize = 0.050f;
                _initialScaleMultiplier = 1.0f;
                _lifetime = 0.75f;
            }
            else if (isCrit)
            {
                displayText = $"HEADSHOT! {Mathf.RoundToInt(damageAmount)}";
                _baseColor = new Color(1.0f, 0.28f, 0.10f); // Fiery blazing orange-red
                _mainText.fontSize = 35;
                _mainText.characterSize = 0.060f;
                _initialScaleMultiplier = 1.4f;
                _lifetime = 1.0f;
            }
            else
            {
                displayText = Mathf.RoundToInt(damageAmount).ToString();
                _baseColor = new Color(1.0f, 0.94f, 0.45f); // Vibrant gold-yellow
                _mainText.fontSize = 30;
                _mainText.characterSize = 0.055f;
                _initialScaleMultiplier = 1.0f;
                _lifetime = 0.85f;
            }

            _mainText.text = displayText;
            _mainText.fontStyle = isCrit ? FontStyle.Bold : FontStyle.Bold;
            _mainText.color = _baseColor;

            if (_shadowText != null)
            {
                _shadowText.text = displayText;
                _shadowText.fontSize = _mainText.fontSize;
                _shadowText.characterSize = _mainText.characterSize;
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

        private static Font _cachedFont;

        public static Font GetBuiltinFont()
        {
            if (_cachedFont != null) return _cachedFont;

            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch { }

            if (_cachedFont == null)
            {
                try
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch { }
            }

            return _cachedFont;
        }
    }
}
