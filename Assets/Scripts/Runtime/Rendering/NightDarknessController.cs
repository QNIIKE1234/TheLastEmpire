using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// Controls the dynamic night darkness fade and player personal light perimeter.
    /// Fades smoothly from a bright illuminated center to a moody dark environment over distance.
    /// Manages player screen coordinates and aspect ratio with zero GPU overhead.
    /// </summary>
    public class NightDarknessController : MonoBehaviour
    {
        public static NightDarknessController Instance { get; private set; }

        private static readonly int PropPlayerScreenPos = Shader.PropertyToID("_GlobalPlayerScreenPos");
        private static readonly int PropAspect = Shader.PropertyToID("_GlobalAspect");
        private static readonly int PropHasTorchLight = Shader.PropertyToID("_GlobalHasTorchLight");

        [Header("Light / Flashlight Settings")]
        [SerializeField] private bool hasTorchLight = true;
        [SerializeField] private bool autoLightAtNight = true;

        [Header("Gradual Distance Falloff & Environment Visibility")]
        [Tooltip("Center bright radius around player")]
        [Range(0.05f, 0.4f)] public float innerRadius = 0.12f;
        [Tooltip("Outer fade radius where darkness reaches max level")]
        [Range(0.2f, 1.5f)] public float outerRadius = 0.70f;
        [Tooltip("Max darkness opacity outside light (0.74 keeps the environment clearly visible as dark night!)")]
        [Range(0.3f, 0.95f)] public float maxDarkness = 0.74f;

        private PlayerController _cachedPlayer;
        private float _playerSearchTimer = 0f;
        private GameObject _overlayCanvasObj;
        private Material _darknessMaterial;

        public bool HasTorchLight => hasTorchLight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[NightDarknessController]");
                DontDestroyOnLoad(go);
                go.AddComponent<NightDarknessController>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeDarknessCanvas();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeDarknessCanvas()
        {
            Shader darknessShader = Shader.Find("Custom/NightDarknessOverlay");
            if (darknessShader == null)
            {
                Debug.LogWarning("[NightDarknessController] Shader 'Custom/NightDarknessOverlay' not found!");
                return;
            }

            _overlayCanvasObj = new GameObject("[NightDarknessOverlay]");
            DontDestroyOnLoad(_overlayCanvasObj);

            Canvas canvas = _overlayCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95; // In front of gameplay, behind pause menus

            UnityEngine.UI.CanvasScaler scaler = _overlayCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Create Fullscreen Image with the darkness shader
            GameObject imgObj = new GameObject("DarknessMask");
            imgObj.transform.SetParent(_overlayCanvasObj.transform, false);

            UnityEngine.UI.Image img = imgObj.AddComponent<UnityEngine.UI.Image>();
            _darknessMaterial = new Material(darknessShader);
            img.material = _darknessMaterial;
            img.color = Color.white;
            img.raycastTarget = false;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        [Header("Debug Controls")]
        [SerializeField] private bool disableDarknessOverlay = false;

        public bool DisableDarknessOverlay
        {
            get => disableDarknessOverlay;
            set
            {
                disableDarknessOverlay = value;
                if (_overlayCanvasObj != null)
                {
                    _overlayCanvasObj.SetActive(!disableDarknessOverlay);
                }
            }
        }

        private void Update()
        {
            HandleInput();
            UpdatePlayerScreenPosition();

            if (_darknessMaterial != null)
            {
                _darknessMaterial.SetFloat("_InnerRadius", innerRadius);
                _darknessMaterial.SetFloat("_OuterRadius", outerRadius);
                _darknessMaterial.SetFloat("_MaxDarkness", maxDarkness);
            }
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            // L key (or F) toggles torch / flashlight
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                ToggleTorchLight();
            }
        }

        public void ToggleTorchLight()
        {
            hasTorchLight = !hasTorchLight;
            Debug.Log($"[NightDarknessController] Light: {(hasTorchLight ? "ON" : "OFF")}");
        }

        public void SetTorchLight(bool state)
        {
            hasTorchLight = state;
        }

        private void UpdatePlayerScreenPosition()
        {
            // Cache player safely with 1-second throttle
            if (_cachedPlayer == null)
            {
                _playerSearchTimer -= Time.deltaTime;
                if (_playerSearchTimer <= 0f)
                {
                    _playerSearchTimer = 1.0f;
                    _cachedPlayer = Object.FindFirstObjectByType<PlayerController>();
                }
            }

            Vector2 screenPos = new Vector2(0.5f, 0.5f); // Center fallback
            Camera mainCam = Camera.main;

            if (_cachedPlayer != null && mainCam != null)
            {
                Vector3 vp = mainCam.WorldToViewportPoint(_cachedPlayer.transform.position);
                screenPos = new Vector2(vp.x, vp.y);
            }

            // Push Viewport Position of player & Screen Aspect
            Shader.SetGlobalVector(PropPlayerScreenPos, new Vector4(screenPos.x, screenPos.y, 0f, 0f));

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            Shader.SetGlobalFloat(PropAspect, aspect);

            // Automatically turn on torch when entering night
            if (autoLightAtNight && DayNightManager.Instance != null && DayNightManager.Instance.IsNight && !hasTorchLight)
            {
                hasTorchLight = true;
            }

            Shader.SetGlobalFloat(PropHasTorchLight, hasTorchLight ? 1.0f : 0.0f);
        }
    }
}
