using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    public class DayNightManager : MonoBehaviour
    {
        public static DayNightManager Instance { get; private set; }

        public enum TimePhase
        {
            Day,
            Dusk,
            Night
        }

        [Header("Timing Settings (Day-Night Cycle)")]
        [SerializeField] private float dayDuration = 35f;
        [SerializeField] private float duskDuration = 12f;
        [SerializeField] private float nightDuration = 25f;
        [Tooltip("Smooth fade duration into darkness/dusk (seconds)")]
        [SerializeField] private float fadeSpeed = 1.2f;

        [Header("Speed Multiplier (Debug / Fast-Forward)")]
        [SerializeField] private float cycleSpeedMultiplier = 1.0f;

        [Header("Scene Sun / Directional Light Dimming")]
        [SerializeField] private Light sceneDirectionalLight;
        [SerializeField] private float daySunIntensity = 1.38f;
        [SerializeField] private float duskSunIntensity = 1.15f;
        [SerializeField] private float nightSunIntensity = 0.20f;
        [SerializeField] private Color daySunColor = new Color(1.0f, 0.95f, 0.86f);
        [SerializeField] private Color duskSunColor = new Color(1.0f, 0.58f, 0.20f);
        [SerializeField] private Color nightSunColor = new Color(0.28f, 0.42f, 0.72f);

        [Header("Ambient & Shadow Fill (Warmth & Vibrancy)")]
        [SerializeField] private Color dayAmbientColor = new Color(0.58f, 0.54f, 0.50f);
        [SerializeField] private Color duskAmbientColor = new Color(0.50f, 0.34f, 0.24f);
        [SerializeField] private Color nightAmbientColor = new Color(0.12f, 0.15f, 0.24f);

        [Header("Dynamic Celestial Sun & Moon Path")]
        [Tooltip("When enabled, the directional light smoothly sweeps across the sky matching time of day")]
        [SerializeField] private bool dynamicLightAngle = true;
        [SerializeField] private Vector3 dayMorningSunAngle = new Vector3(28f, -105f, 0f);
        [SerializeField] private Vector3 dayNoonSunAngle = new Vector3(38f, -135f, 0f);
        [SerializeField] private Vector3 dayAfternoonSunAngle = new Vector3(30f, -155f, 0f);
        [SerializeField] private Vector3 duskStartSunAngle = new Vector3(30f, -155f, 0f);
        [SerializeField] private Vector3 duskSunsetSunAngle = new Vector3(16f, -175f, 0f);
        [SerializeField] private Vector3 nightMoonriseAngle = new Vector3(25f, 35f, 0f);
        [SerializeField] private Vector3 nightMidnightAngle = new Vector3(55f, 60f, 0f);
        [SerializeField] private Vector3 nightDawnpinAngle = new Vector3(30f, 80f, 0f);

        [Header("Visual Feedback (Camera Background Tint)")]
        [SerializeField] private Color dayCameraColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        [SerializeField] private Color duskCameraColor = new Color(0.14f, 0.11f, 0.13f, 1f);
        [SerializeField] private Color nightCameraColor = new Color(0.02f, 0.03f, 0.05f, 1f);

        [Header("Ground Plane Ambient Tint (WorldSpaceGroundShader)")]
        [SerializeField] private Color dayGroundTint = Color.white;
        [SerializeField] private Color duskGroundTint = new Color(0.96f, 0.68f, 0.40f, 1f);
        [SerializeField] private Color nightGroundTint = new Color(0.12f, 0.16f, 0.28f, 1f);

        [Header("Legacy Lighting Compatibility")]
        [SerializeField] private bool disableLegacyLighting = false;

        [Header("UI Reference")]
        [SerializeField] private TextMeshProUGUI timeText;

        private float _phaseTimer = 0f;
        private TimePhase _currentPhase = TimePhase.Day;
        private bool _isNight = false;

        private float _currentDarknessAlpha = 0f;
        private float _currentDuskAlpha = 0f;
        private Color _currentAmbientTint = Color.white;

        public TimePhase CurrentPhase => _currentPhase;
        public bool IsNight => _isNight;
        public bool IsDusk => _currentPhase == TimePhase.Dusk;
        public bool IsDay => _currentPhase == TimePhase.Day;
        public int DayCount { get; private set; } = 1;

        public float CurrentDarknessAlpha => _currentDarknessAlpha;
        public float CurrentDuskAlpha => 0f;
        public Color CurrentAmbientTint => _currentAmbientTint;

        public bool DisableLegacyLighting
        {
            get => false;
            set
            {
                disableLegacyLighting = false;
                if (NightDarknessController.Instance != null)
                {
                    NightDarknessController.Instance.DisableDarknessOverlay = false;
                }
            }
        }

        public float CycleSpeedMultiplier
        {
            get => cycleSpeedMultiplier;
            set => cycleSpeedMultiplier = Mathf.Max(0.1f, value);
        }

        public float PhaseTimer => _phaseTimer;
        public float CurrentPhaseDuration => GetCurrentPhaseDuration();
        public float RemainingPhaseTime => Mathf.Max(0f, GetCurrentPhaseDuration() - _phaseTimer);

        public float PhaseProgress
        {
            get
            {
                float duration = GetCurrentPhaseDuration();
                return Mathf.Clamp01(_phaseTimer / Mathf.Max(0.01f, duration));
            }
        }

        public event System.Action<bool> OnTimePhaseChanged;
        public event System.Action<TimePhase> OnPhaseChanged;

        private void Awake()
        {
            disableLegacyLighting = false;
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            disableLegacyLighting = false;
            if (nightGroundTint == default || nightGroundTint.a < 0.01f) nightGroundTint = new Color(0.12f, 0.16f, 0.28f, 1f);
            if (duskGroundTint == default || duskGroundTint.a < 0.01f) duskGroundTint = new Color(0.96f, 0.68f, 0.40f, 1f);
            if (dayGroundTint == default || dayGroundTint.a < 0.01f) dayGroundTint = Color.white;

            PushShaderValues();
        }

        private void Start()
        {
            disableLegacyLighting = false;
            if (NightDarknessController.Instance != null)
            {
                NightDarknessController.Instance.DisableDarknessOverlay = false;
            }
            ResetDayCount();
        }

        public void ResetDayCount()
        {
            DayCount = 1;
            _phaseTimer = 0f;
            _currentPhase = TimePhase.Day;
            _isNight = false;
            _currentDarknessAlpha = 0f;
            _currentDuskAlpha = 0f;
            _currentAmbientTint = Color.white;

            PushShaderValues();

            if (timeText != null)
            {
                timeText.text = $"DAY ({Mathf.CeilToInt(dayDuration)}s)";
                timeText.color = new Color(1f, 0.85f, 0.2f);
            }
        }

        private float GetCurrentPhaseDuration()
        {
            switch (_currentPhase)
            {
                case TimePhase.Dusk: return duskDuration;
                case TimePhase.Night: return nightDuration;
                case TimePhase.Day:
                default: return dayDuration;
            }
        }

        private void Update()
        {
            _phaseTimer += Time.deltaTime * cycleSpeedMultiplier;
            float currentDuration = GetCurrentPhaseDuration();

            // Smooth transition for screen darkness, sun dimming, and ambient tint
            UpdateFades();

            // Smoothly shift camera background
            if (Camera.main != null)
            {
                Color targetCameraColor = GetTargetCameraColor();
                Camera.main.backgroundColor = Color.Lerp(Camera.main.backgroundColor, targetCameraColor, Time.deltaTime * 2f);
            }

            // Update UI
            UpdateUIText(currentDuration);

            // Phase transition check
            if (_phaseTimer >= currentDuration)
            {
                AdvancePhase();
            }
        }

        private void UpdateFades()
        {
            float targetDarkness = (_currentPhase == TimePhase.Night) ? 1.0f : 0.0f;
            Color targetTint = (_currentPhase == TimePhase.Night) ? nightGroundTint : (_currentPhase == TimePhase.Dusk ? duskGroundTint : dayGroundTint);

            _currentDarknessAlpha = Mathf.MoveTowards(_currentDarknessAlpha, targetDarkness, Time.deltaTime * fadeSpeed);
            _currentDuskAlpha = 0f; // No legacy 2D dusk overlay; Dusk uses modern URP atmospheric sunlight & fog
            _currentAmbientTint = Color.Lerp(_currentAmbientTint, targetTint, Time.deltaTime * fadeSpeed * 1.5f);

            // Smoothly dim Directional Light (Sun) & Ambient Fill
            UpdateSunLight();

            PushShaderValues();
        }

        private void UpdateSunLight()
        {
            if (sceneDirectionalLight == null)
            {
                if (RenderSettings.sun != null)
                {
                    sceneDirectionalLight = RenderSettings.sun;
                }
                else
                {
                    Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                    foreach (var l in lights)
                    {
                        if (l != null && l.type == LightType.Directional)
                        {
                            sceneDirectionalLight = l;
                            break;
                        }
                    }
                }
            }

            // Update Ambient Light fill for rich shadow depth
            Color targetAmbient;
            float targetIntensity;
            Color targetColor;

            switch (_currentPhase)
            {
                case TimePhase.Dusk:
                    targetAmbient = duskAmbientColor;
                    targetIntensity = duskSunIntensity;
                    targetColor = duskSunColor;
                    break;
                case TimePhase.Night:
                    targetAmbient = nightAmbientColor;
                    targetIntensity = nightSunIntensity;
                    targetColor = nightSunColor;
                    break;
                case TimePhase.Day:
                default:
                    targetAmbient = dayAmbientColor;
                    targetIntensity = daySunIntensity;
                    targetColor = daySunColor;
                    break;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, Time.deltaTime * fadeSpeed);

            if (sceneDirectionalLight != null)
            {
                // Dynamic celestial path (smooth sun/moon rotation over the time of day)
                Quaternion targetRot = GetTargetSunRotation();
                sceneDirectionalLight.transform.rotation = Quaternion.Slerp(
                    sceneDirectionalLight.transform.rotation,
                    targetRot,
                    Time.deltaTime * Mathf.Max(2f, fadeSpeed)
                );

                sceneDirectionalLight.intensity = Mathf.MoveTowards(sceneDirectionalLight.intensity, targetIntensity, Time.deltaTime * fadeSpeed);
                sceneDirectionalLight.color = Color.Lerp(sceneDirectionalLight.color, targetColor, Time.deltaTime * fadeSpeed);
            }
        }

        public Quaternion GetTargetSunRotation()
        {
            if (!dynamicLightAngle)
            {
                return Quaternion.Euler(48f, -130f, 0f);
            }

            float t = PhaseProgress; // 0.0 to 1.0 within current phase
            switch (_currentPhase)
            {
                case TimePhase.Dusk:
                {
                    // Sunset descent: Long dramatic diagonal shadows
                    float pitch = Mathf.Lerp(duskStartSunAngle.x, duskSunsetSunAngle.x, t);
                    float yaw = Mathf.Lerp(duskStartSunAngle.y, duskSunsetSunAngle.y, t);
                    return Quaternion.Euler(pitch, yaw, 0f);
                }
                case TimePhase.Night:
                {
                    // Moonlight arc across the opposite side of the sky
                    float pitch = (t < 0.5f)
                        ? Mathf.Lerp(nightMoonriseAngle.x, nightMidnightAngle.x, t * 2f)
                        : Mathf.Lerp(nightMidnightAngle.x, nightDawnpinAngle.x, (t - 0.5f) * 2f);
                    float yaw = Mathf.Lerp(nightMoonriseAngle.y, nightDawnpinAngle.y, t);
                    return Quaternion.Euler(pitch, yaw, 0f);
                }
                case TimePhase.Day:
                default:
                {
                    // Morning -> Noon (peak height) -> Afternoon
                    float pitch = (t < 0.5f)
                        ? Mathf.Lerp(dayMorningSunAngle.x, dayNoonSunAngle.x, t * 2f)
                        : Mathf.Lerp(dayNoonSunAngle.x, dayAfternoonSunAngle.x, (t - 0.5f) * 2f);
                    float yaw = Mathf.Lerp(dayMorningSunAngle.y, dayAfternoonSunAngle.y, t);
                    return Quaternion.Euler(pitch, yaw, 0f);
                }
            }
        }

        private void PushShaderValues()
        {
            Shader.SetGlobalFloat("_GlobalDarknessAlpha", _currentDarknessAlpha);
            Shader.SetGlobalFloat("_GlobalDuskAlpha", 0f);
            Shader.SetGlobalColor("_GlobalAmbientTint", _currentAmbientTint);
        }

        private Color GetTargetCameraColor()
        {
            switch (_currentPhase)
            {
                case TimePhase.Dusk: return duskCameraColor;
                case TimePhase.Night: return nightCameraColor;
                case TimePhase.Day:
                default: return dayCameraColor;
            }
        }

        private void UpdateUIText(float currentDuration)
        {
            if (timeText == null) return;

            float remainingTime = Mathf.Max(0f, currentDuration - _phaseTimer);
            string label;
            Color labelColor;

            switch (_currentPhase)
            {
                case TimePhase.Dusk:
                    label = "DUSK";
                    labelColor = new Color(1f, 0.6f, 0.2f);
                    break;
                case TimePhase.Night:
                    label = "NIGHT";
                    labelColor = new Color(0.4f, 0.8f, 1f);
                    break;
                case TimePhase.Day:
                default:
                    label = "DAY";
                    labelColor = new Color(1f, 0.85f, 0.2f);
                    break;
            }

            timeText.text = $"{label} ({Mathf.CeilToInt(remainingTime)}s)";
            timeText.color = labelColor;
        }

        public void AdvancePhase(bool snapVisuals = false)
        {
            _phaseTimer = 0f;

            switch (_currentPhase)
            {
                case TimePhase.Day:
                    SetPhase(TimePhase.Dusk, snapVisuals);
                    break;
                case TimePhase.Dusk:
                    SetPhase(TimePhase.Night, snapVisuals);
                    break;
                case TimePhase.Night:
                    DayCount++;
                    SetPhase(TimePhase.Day, snapVisuals);
                    break;
                default:
                    SetPhase(TimePhase.Day, snapVisuals);
                    break;
            }
        }

        public void FastForward(float seconds)
        {
            _phaseTimer += seconds;
            float currentDuration = GetCurrentPhaseDuration();
            if (_phaseTimer >= currentDuration)
            {
                AdvancePhase();
            }
        }

        public void SetPhase(TimePhase newPhase, bool snapVisuals = false)
        {
            _currentPhase = newPhase;
            _phaseTimer = 0f;

            bool wasNight = _isNight;
            _isNight = (_currentPhase == TimePhase.Night);

            if (snapVisuals)
            {
                _currentDarknessAlpha = (_currentPhase == TimePhase.Night) ? 1.0f : 0.0f;
                _currentDuskAlpha = 0f;
                _currentAmbientTint = (_currentPhase == TimePhase.Night) ? nightGroundTint : (_currentPhase == TimePhase.Dusk ? duskGroundTint : dayGroundTint);

                Color targetAmbient;
                float targetIntensity;
                Color targetColor;

                switch (_currentPhase)
                {
                    case TimePhase.Dusk:
                        targetAmbient = duskAmbientColor;
                        targetIntensity = duskSunIntensity;
                        targetColor = duskSunColor;
                        break;
                    case TimePhase.Night:
                        targetAmbient = nightAmbientColor;
                        targetIntensity = nightSunIntensity;
                        targetColor = nightSunColor;
                        break;
                    case TimePhase.Day:
                    default:
                        targetAmbient = dayAmbientColor;
                        targetIntensity = daySunIntensity;
                        targetColor = daySunColor;
                        break;
                }

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = targetAmbient;

                if (sceneDirectionalLight != null)
                {
                    sceneDirectionalLight.transform.rotation = GetTargetSunRotation();
                    sceneDirectionalLight.intensity = targetIntensity;
                    sceneDirectionalLight.color = targetColor;
                }

                PushShaderValues();

                if (Camera.main != null)
                {
                    Camera.main.backgroundColor = GetTargetCameraColor();
                }
            }

            if (wasNight != _isNight)
            {
                OnTimePhaseChanged?.Invoke(_isNight);
            }
            OnPhaseChanged?.Invoke(_currentPhase);

            Debug.Log($"[DayNightManager] Phase changed to {_currentPhase}. Day: {DayCount}");
        }

        public void SetDayCount(int day)
        {
            DayCount = Mathf.Max(1, day);
        }

        public void AddDays(int delta)
        {
            DayCount = Mathf.Max(1, DayCount + delta);
        }

        public void ToggleTimePhase()
        {
            AdvancePhase();
        }
    }
}
