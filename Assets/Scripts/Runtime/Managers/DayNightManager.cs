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
        [Tooltip("ความยาว 1 วันเต็มในเกม (วินาทีจริง) = 360 วินาที (6 นาที)")]
        [SerializeField] private float fullDayDuration = 360f;
        [Tooltip("ช่วงกลางวัน 06:00 - 18:00 (12 ชม. ในเกม) = 180 วินาที")]
        [SerializeField] private float dayDuration = 180f;
        [Tooltip("ช่วงพลบค่ำ 18:00 - 21:00 (3 ชม. ในเกม) = 45 วินาที")]
        [SerializeField] private float duskDuration = 45f;
        [Tooltip("ช่วงกลางคืน 21:00 - 06:00 (9 ชม. ในเกม) = 135 วินาที")]
        [SerializeField] private float nightDuration = 135f;
        [Tooltip("Smooth fade duration into darkness/dusk (seconds)")]
        [SerializeField] private float fadeSpeed = 1.2f;

        [Header("Speed Multiplier (Debug / Fast-Forward)")]
        [SerializeField] private float cycleSpeedMultiplier = 1.0f;

        [Header("Dawn (Sunrise) Atmospheric Lighting (Golden-Yellow Glow)")]
        [Tooltip("Golden-yellow sunrise morning sunlight color")]
        [SerializeField] private Color dawnSunColor = new Color(1.0f, 0.92f, 0.55f, 1f);
        [SerializeField] private float dawnSunIntensity = 0.85f;
        [Tooltip("Warm golden morning ambient fill")]
        [SerializeField] private Color dawnAmbientColor = new Color(0.56f, 0.52f, 0.38f, 1f);
        [Tooltip("Soft morning sky background tint")]
        [SerializeField] private Color dawnCameraColor = new Color(0.18f, 0.22f, 0.30f, 1f);
        [Tooltip("Morning ground ambient tint (golden-yellow haze)")]
        [SerializeField] private Color dawnGroundTint = new Color(0.98f, 0.94f, 0.68f, 1f);
        [Tooltip("Sunrise sun angle as it breaks over the eastern horizon")]
        [SerializeField] private Vector3 dawnSunriseSunAngle = new Vector3(18f, -90f, 0f);

        [Header("Scene Sun / Directional Light Dimming")]
        [SerializeField] private Light sceneDirectionalLight;
        [SerializeField] private float daySunIntensity = 1.38f;
        [SerializeField] private float duskSunIntensity = 1.15f;
        [SerializeField] private float nightSunIntensity = 0.20f;
        [SerializeField] private Color daySunColor = new Color(1.0f, 0.95f, 0.86f);
        [SerializeField] private Color duskSunColor = new Color(1.0f, 0.58f, 0.20f);
        [SerializeField] private Color nightSunColor = new Color(0.28f, 0.42f, 0.72f);

        [Header("Ambient & Shadow Fill (Warmth & Vibrancy)")]
        [SerializeField] private Color dayAmbientColor = new Color(0.55f, 0.52f, 0.48f);
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
        public float DayDuration => dayDuration;
        public float DuskDuration => duskDuration;
        public float NightDuration => nightDuration;
        public TextMeshProUGUI TimeText => timeText;

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
                return;
            }

            // บังคับใช้รอบเวลา 360 วินาที (ความเร็วคงที่สม่ำเสมอ 15 วินาทีต่อ 1 ชม. ในเกม)
            if (Mathf.Abs((dayDuration + duskDuration + nightDuration) - 360f) > 0.5f)
            {
                ApplyUniformCycle(360f);
            }
        }

        private void OnValidate()
        {
            if (fullDayDuration > 0f)
            {
                float secPerHour = fullDayDuration / 24f;
                dayDuration = secPerHour * 12f;
                duskDuration = secPerHour * 3f;
                nightDuration = secPerHour * 9f;
            }
        }

        private void OnEnable()
        {
            disableLegacyLighting = false;
            if (nightGroundTint == default || nightGroundTint.a < 0.01f) nightGroundTint = new Color(0.12f, 0.16f, 0.28f, 1f);
            if (duskGroundTint == default || duskGroundTint.a < 0.01f) duskGroundTint = new Color(0.96f, 0.68f, 0.40f, 1f);
            if (dawnGroundTint == default || dawnGroundTint.a < 0.01f) dawnGroundTint = new Color(0.98f, 0.94f, 0.68f, 1f);
            if (dawnSunColor == default || dawnSunColor.a < 0.01f) dawnSunColor = new Color(1.0f, 0.92f, 0.55f, 1f);
            if (dawnAmbientColor == default || dawnAmbientColor.a < 0.01f) dawnAmbientColor = new Color(0.56f, 0.52f, 0.38f, 1f);
            if (dawnCameraColor == default || dawnCameraColor.a < 0.01f) dawnCameraColor = new Color(0.18f, 0.22f, 0.30f, 1f);
            if (dawnSunriseSunAngle == default) dawnSunriseSunAngle = new Vector3(18f, -90f, 0f);
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
            EnsureTimeHUDController();
        }

        public void EnsureTimeHUDController()
        {
            if (TimeHUDController.Instance == null)
            {
                GameObject hudObj = GameObject.Find("Time_HUD");
                if (hudObj != null)
                {
                    TimeHUDController ctrl = hudObj.GetComponent<TimeHUDController>();
                    if (ctrl == null) ctrl = hudObj.AddComponent<TimeHUDController>();
                    ctrl.InitializeHUD();
                }
            }
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

        public void GetCurrentLightingTargets(out Color targetAmbient, out float targetIntensity, out Color targetColor, out Color targetTint)
        {
            float t = PhaseProgress;
            switch (_currentPhase)
            {
                case TimePhase.Dusk:
                    targetAmbient = duskAmbientColor;
                    targetIntensity = duskSunIntensity;
                    targetColor = duskSunColor;
                    targetTint = duskGroundTint;
                    break;
                case TimePhase.Night:
                    targetAmbient = nightAmbientColor;
                    targetIntensity = nightSunIntensity;
                    targetColor = nightSunColor;
                    targetTint = nightGroundTint;
                    break;
                case TimePhase.Day:
                default:
                    // First half (0.0 to 0.5) is Dawn: Warm golden-yellow morning sunrise glow, similar to Dusk but yellowish
                    if (t < 0.5f)
                    {
                        float dawnT = t * 2.0f; // 0.0 at sunrise -> 1.0 at noon
                        targetAmbient = Color.Lerp(dawnAmbientColor, dayAmbientColor, dawnT);
                        targetIntensity = Mathf.Lerp(dawnSunIntensity, daySunIntensity, dawnT);
                        targetColor = Color.Lerp(dawnSunColor, daySunColor, dawnT);
                        targetTint = Color.Lerp(dawnGroundTint, dayGroundTint, dawnT);
                    }
                    else
                    {
                        // Second half (0.5 to 1.0) is Noon -> gentle afternoon warming toward Dusk
                        float noonT = (t - 0.5f) * 2.0f; // 0.0 at noon -> 1.0 at dusk
                        targetAmbient = Color.Lerp(dayAmbientColor, duskAmbientColor, noonT * 0.35f);
                        targetIntensity = Mathf.Lerp(daySunIntensity, Mathf.Lerp(daySunIntensity, duskSunIntensity, 0.5f), noonT * 0.5f);
                        targetColor = Color.Lerp(daySunColor, duskSunColor, noonT * 0.35f);
                        targetTint = Color.Lerp(dayGroundTint, duskGroundTint, noonT * 0.35f);
                    }
                    break;
            }
        }

        private void UpdateFades()
        {
            float targetDarkness = (_currentPhase == TimePhase.Night) ? 1.0f : 0.0f;
            GetCurrentLightingTargets(out Color targetAmbient, out float targetIntensity, out Color targetColor, out Color targetTint);

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

            GetCurrentLightingTargets(out Color targetAmbient, out float targetIntensity, out Color targetColor, out Color targetTint);

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
                    // Dawn (Sunrise low in eastern horizon) -> Noon (peak height) -> Afternoon
                    if (t < 0.5f)
                    {
                        float dawnT = t * 2.0f;
                        float pitch = Mathf.Lerp(dawnSunriseSunAngle.x, dayNoonSunAngle.x, dawnT);
                        float yaw = Mathf.Lerp(dawnSunriseSunAngle.y, dayNoonSunAngle.y, dawnT);
                        return Quaternion.Euler(pitch, yaw, 0f);
                    }
                    else
                    {
                        float noonT = (t - 0.5f) * 2.0f;
                        float pitch = Mathf.Lerp(dayNoonSunAngle.x, dayAfternoonSunAngle.x, noonT);
                        float yaw = Mathf.Lerp(dayNoonSunAngle.y, dayAfternoonSunAngle.y, noonT);
                        return Quaternion.Euler(pitch, yaw, 0f);
                    }
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
                default:
                    if (PhaseProgress < 0.5f)
                    {
                        float dawnT = PhaseProgress * 2.0f;
                        return Color.Lerp(dawnCameraColor, dayCameraColor, dawnT);
                    }
                    return dayCameraColor;
            }
        }

        /// <summary>
        /// Converts the current cycle progress to a 24-hour in-game clock time string (e.g. "06:00" to "23:59").
        /// Dawn: 06:00 - 12:00, Noon: 12:00 - 18:00, Dusk: 18:00 - 21:00, Night: 21:00 - 06:00.
        /// </summary>
        public string GetClockTimeString(bool includeSeconds = false)
        {
            float totalHours = 6.0f;

            switch (_currentPhase)
            {
                case TimePhase.Day:
                    float dayProgress = PhaseProgress;
                    if (dayProgress < 0.5f)
                    {
                        // 06:00 -> 12:00 (Dawn)
                        totalHours = 6.0f + (dayProgress * 2.0f) * 6.0f;
                    }
                    else
                    {
                        // 12:00 -> 18:00 (Noon / Afternoon)
                        totalHours = 12.0f + ((dayProgress - 0.5f) * 2.0f) * 6.0f;
                    }
                    break;

                case TimePhase.Dusk:
                    // 18:00 -> 21:00 (Dusk)
                    totalHours = 18.0f + PhaseProgress * 3.0f;
                    break;

                case TimePhase.Night:
                    // 21:00 -> 06:00 next day (Night, spans 9 hours, wraps midnight 00:00)
                    totalHours = (21.0f + PhaseProgress * 9.0f) % 24.0f;
                    break;
            }

            int hours = Mathf.FloorToInt(totalHours) % 24;
            float remainderMinutes = (totalHours - Mathf.Floor(totalHours)) * 60f;
            int minutes = Mathf.FloorToInt(remainderMinutes) % 60;

            if (includeSeconds)
            {
                int seconds = Mathf.FloorToInt((remainderMinutes - minutes) * 60f) % 60;
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }

            return $"{hours:D2}:{minutes:D2}";
        }

        private void UpdateUIText(float currentDuration)
        {
            if (timeText == null) return;

            string clock = GetClockTimeString();
            Color labelColor;

            switch (_currentPhase)
            {
                case TimePhase.Dusk:
                    labelColor = new Color(1f, 0.55f, 0.20f); // Sunset orange
                    break;
                case TimePhase.Night:
                    labelColor = new Color(0.50f, 0.80f, 1f); // Moonlit blue
                    break;
                case TimePhase.Day:
                default:
                    if (PhaseProgress < 0.5f)
                    {
                        labelColor = new Color(1f, 0.88f, 0.42f); // Warm Dawn golden-yellow
                    }
                    else
                    {
                        labelColor = new Color(1f, 0.98f, 0.55f); // Bright Noon yellow
                    }
                    break;
            }

            timeText.text = clock;
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

                if (TimeHUDController.Instance != null)
                {
                    TimeHUDController.Instance.SnapToCurrentPhase();
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


        /// <summary>
        /// ตั้งค่ารอบเวลา 24 ชั่วโมงในเกมให้เดินด้วยความเร็วคงที่สม่ำเสมอตลอดทั้งวัน
        /// เช่น 360 วินาที = 15 วินาทีต่อ 1 ชั่วโมงในเกม
        /// กลางวัน (06:00 - 18:00 = 12 ชม.) = 180 วินาที
        /// พลบค่ำ (18:00 - 21:00 = 3 ชม.) = 45 วินาที
        /// กลางคืน (21:00 - 06:00 = 9 ชม.) = 135 วินาที
        /// </summary>
        public void ApplyUniformCycle(float totalSeconds = 360f)
        {
            fullDayDuration = totalSeconds;
            float secPerHour = totalSeconds / 24f; // 15s / in-game hour
            dayDuration = secPerHour * 12f;       // 180s (12 hours)
            duskDuration = secPerHour * 3f;       // 45s (3 hours)
            nightDuration = secPerHour * 9f;      // 135s (9 hours)
            Debug.Log($"[DayNightManager] Uniform 24h cycle applied: Total={totalSeconds}s (Day={dayDuration}s, Dusk={duskDuration}s, Night={nightDuration}s, {secPerHour:F1}s/hour)");
        }

        public void SetCycleDurations(float day, float dusk, float night)
        {
            dayDuration = Mathf.Max(1f, day);
            duskDuration = Mathf.Max(1f, dusk);
            nightDuration = Mathf.Max(1f, night);
            fullDayDuration = dayDuration + duskDuration + nightDuration;
            Debug.Log($"[DayNightManager] Cycle updated: Day={dayDuration}s, Dusk={duskDuration}s, Night={nightDuration}s (Total={fullDayDuration}s)");
        }
        public void ToggleTimePhase()
        {
            AdvancePhase();
        }
    }
}
