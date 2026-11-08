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
        private static readonly int PropCampfireScreenPos = Shader.PropertyToID("_GlobalCampfireScreenPos");

        [Header("Light / Flashlight Settings")]
        [SerializeField] private bool hasTorchLight = true;
        [SerializeField] private bool autoLightAtNight = true;

        [Header("Gradual Distance Falloff & Environment Visibility")]
        [Tooltip("Center bright radius around player")]
        [Range(0.05f, 0.4f)] public float innerRadius = 0.10f;
        [Tooltip("Outer fade radius where darkness reaches max level")]
        [Range(0.15f, 1.2f)] public float outerRadius = 0.45f;
        [Tooltip("Max darkness opacity outside light (0.90 = 90% dark)")]
        [Range(0.3f, 0.98f)] public float maxDarkness = 0.90f;

        private PlayerController _cachedPlayer;
        private float _playerSearchTimer = 0f;
        private GameObject _overlayCanvasObj;
        private Material _darknessMaterial;
        private float _currentVisionMultiplier = 1.0f;

        public float VisionBonusMultiplier { get; set; } = 1.0f;
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
            disableDarknessOverlay = false;
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

        private void OnEnable()
        {
            disableDarknessOverlay = false;
        }

        private void Start()
        {
            disableDarknessOverlay = false;
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

            // Keep Daytime 100% clean of 2D canvas overdraw; activate canvas dynamically when night is present
            if (_overlayCanvasObj != null)
            {
                if (!disableDarknessOverlay)
                {
                    bool shouldBeActive = (DayNightManager.Instance != null)
                        ? (DayNightManager.Instance.IsNight || DayNightManager.Instance.CurrentDarknessAlpha > 0.005f)
                        : true;
                    if (_overlayCanvasObj.activeSelf != shouldBeActive)
                    {
                        _overlayCanvasObj.SetActive(shouldBeActive);
                    }
                }
                else
                {
                    if (_overlayCanvasObj.activeSelf) _overlayCanvasObj.SetActive(false);
                }
            }

            // ตรวจสอบว่าผู้เล่นอยู่ใกล้กองไฟ (Campfire) หรือไม่ หากอยู่ใกล้จะได้รับโบนัสขยายระยะการมองเห็น +70%
            float targetVisionBonus = 1.0f;
            CampfireBuilding closestCampfire = null;
            float closestDist = float.MaxValue;

            if (_cachedPlayer != null)
            {
                Vector3 pPos = _cachedPlayer.transform.position;
                for (int i = 0; i < CampfireBuilding.ActiveCampfires.Count; i++)
                {
                    CampfireBuilding cb = CampfireBuilding.ActiveCampfires[i];
                    if (cb == null) continue;

                    float d = Vector3.Distance(pPos, cb.transform.position);
                    if (d <= cb.VisionAuraRadius)
                    {
                        targetVisionBonus = Mathf.Max(targetVisionBonus, 1.0f + cb.NightVisionBonus);
                    }

                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestCampfire = cb;
                    }
                }
            }

            VisionBonusMultiplier = targetVisionBonus;
            _currentVisionMultiplier = Mathf.MoveTowards(_currentVisionMultiplier, VisionBonusMultiplier, Time.deltaTime * 1.5f);

            float currentInner = innerRadius * _currentVisionMultiplier;
            float currentOuter = outerRadius * _currentVisionMultiplier;

            // เมื่ออยู่ใกล้กองไฟ (Campfire) ขยายระยะการมองเห็น +70% และลดความมืดลงเหลือเพียง 30% (เปิดการมองเห็นสว่างชัดเจน 70%)
            float campfireInfluence = Mathf.Clamp01((_currentVisionMultiplier - 1.0f) / 0.70f);
            float currentMaxDarkness = Mathf.Lerp(maxDarkness, 0.30f, campfireInfluence);

            // ส่งพิกัดของกองไฟที่ใกล้ที่สุดเข้า Shader เพื่อเจาะรูสว่างบนหน้าจอมืด
            Camera mainCam = Camera.main;
            if (closestCampfire != null && mainCam != null && closestDist < 35f)
            {
                Vector3 vp = mainCam.WorldToViewportPoint(closestCampfire.transform.position + Vector3.up * 0.4f);
                if (vp.z > 0 && vp.x >= -0.2f && vp.x <= 1.2f && vp.y >= -0.2f && vp.y <= 1.2f)
                {
                    Shader.SetGlobalVector(PropCampfireScreenPos, new Vector4(vp.x, vp.y, closestCampfire.LightHoleRadius, 1f));
                }
                else
                {
                    Shader.SetGlobalVector(PropCampfireScreenPos, Vector4.zero);
                }
            }
            else
            {
                Shader.SetGlobalVector(PropCampfireScreenPos, Vector4.zero);
            }

            if (_darknessMaterial != null)
            {
                _darknessMaterial.SetFloat("_InnerRadius", currentInner);
                _darknessMaterial.SetFloat("_OuterRadius", currentOuter);
                _darknessMaterial.SetFloat("_MaxDarkness", currentMaxDarkness);
            }
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            // L key (or F key) toggles torch / flashlight
            if (Keyboard.current.lKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)
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
