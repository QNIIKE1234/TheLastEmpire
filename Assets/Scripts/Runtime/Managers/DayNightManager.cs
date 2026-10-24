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
        [SerializeField] private float daySunIntensity = 1.0f;
        [SerializeField] private float duskSunIntensity = 0.45f;
        [SerializeField] private float nightSunIntensity = 0.08f;
        [SerializeField] private Color daySunColor = new Color(1f, 0.98f, 0.92f);
        [SerializeField] private Color duskSunColor = new Color(0.95f, 0.60f, 0.35f);
        [SerializeField] private Color nightSunColor = new Color(0.12f, 0.16f, 0.28f);

        [Header("Visual Feedback (Camera Background Tint)")]
        [SerializeField] private Color dayCameraColor = new Color(0.15f, 0.3f, 0.45f, 1f);
        [SerializeField] private Color duskCameraColor = new Color(0.25f, 0.15f, 0.12f, 1f);
        [SerializeField] private Color nightCameraColor = new Color(0.02f, 0.02f, 0.04f, 1f);

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

        private void Start()
        {
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
                case TimePhase.Day: return dayDuration;
                case TimePhase.Dusk: return duskDuration;
                case TimePhase.Night: return nightDuration;
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
            float targetDusk = (_currentPhase == TimePhase.Dusk) ? 1.0f : 0.0f;

            Color targetTint = Color.white;
            if (_currentPhase == TimePhase.Dusk)
            {
                targetTint = new Color(0.95f, 0.70f, 0.45f, 1f);
            }
            else if (_currentPhase == TimePhase.Night)
            {
                targetTint = new Color(0.10f, 0.12f, 0.18f, 1f);
            }

            _currentDarknessAlpha = Mathf.MoveTowards(_currentDarknessAlpha, targetDarkness, Time.deltaTime * fadeSpeed);
            _currentDuskAlpha = Mathf.MoveTowards(_currentDuskAlpha, targetDusk, Time.deltaTime * fadeSpeed);
            _currentAmbientTint = Color.Lerp(_currentAmbientTint, targetTint, Time.deltaTime * fadeSpeed * 1.5f);

            // Smoothly dim Directional Light (Sun)
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

            if (sceneDirectionalLight != null)
            {
                float targetIntensity = _currentPhase switch
                {
                    TimePhase.Day => daySunIntensity,
                    TimePhase.Dusk => duskSunIntensity,
                    TimePhase.Night => nightSunIntensity,
                    _ => daySunIntensity
                };

                Color targetColor = _currentPhase switch
                {
                    TimePhase.Day => daySunColor,
                    TimePhase.Dusk => duskSunColor,
                    TimePhase.Night => nightSunColor,
                    _ => daySunColor
                };

                sceneDirectionalLight.intensity = Mathf.MoveTowards(sceneDirectionalLight.intensity, targetIntensity, Time.deltaTime * fadeSpeed);
                sceneDirectionalLight.color = Color.Lerp(sceneDirectionalLight.color, targetColor, Time.deltaTime * fadeSpeed);
            }
        }

        private void PushShaderValues()
        {
            Shader.SetGlobalFloat("_GlobalDarknessAlpha", _currentDarknessAlpha);
            Shader.SetGlobalFloat("_GlobalDuskAlpha", _currentDuskAlpha);
            Shader.SetGlobalColor("_GlobalAmbientTint", _currentAmbientTint);
        }

        private Color GetTargetCameraColor()
        {
            switch (_currentPhase)
            {
                case TimePhase.Day: return dayCameraColor;
                case TimePhase.Dusk: return duskCameraColor;
                case TimePhase.Night: return nightCameraColor;
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
                case TimePhase.Day:
                    label = "DAY";
                    labelColor = new Color(1f, 0.85f, 0.2f);
                    break;
                case TimePhase.Dusk:
                    label = "DUSK";
                    labelColor = new Color(1f, 0.55f, 0.2f);
                    break;
                case TimePhase.Night:
                    label = "NIGHT";
                    labelColor = new Color(0.4f, 0.8f, 1f);
                    break;
                default:
                    label = "DAY";
                    labelColor = Color.white;
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
                _currentDuskAlpha = (_currentPhase == TimePhase.Dusk) ? 1.0f : 0.0f;
                if (_currentPhase == TimePhase.Day) _currentAmbientTint = Color.white;
                else if (_currentPhase == TimePhase.Dusk) _currentAmbientTint = new Color(0.95f, 0.70f, 0.45f, 1f);
                else _currentAmbientTint = new Color(0.10f, 0.12f, 0.18f, 1f);

                if (sceneDirectionalLight != null)
                {
                    sceneDirectionalLight.intensity = (_currentPhase == TimePhase.Night) ? nightSunIntensity :
                                                     (_currentPhase == TimePhase.Dusk) ? duskSunIntensity : daySunIntensity;
                    sceneDirectionalLight.color = (_currentPhase == TimePhase.Night) ? nightSunColor :
                                                  (_currentPhase == TimePhase.Dusk) ? duskSunColor : daySunColor;
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
