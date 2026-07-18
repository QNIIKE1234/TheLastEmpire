using UnityEngine;

namespace TheLastEmpire
{
    public class DayNightManager : MonoBehaviour
    {
        public static DayNightManager Instance { get; private set; }

        [Header("Timing Settings")]
        [SerializeField] private float dayDuration = 30f;
        [SerializeField] private float nightDuration = 30f;

        [Header("Visual Feedback (Camera Tint)")]
        [SerializeField] private Color dayCameraColor = new Color(0.15f, 0.3f, 0.45f, 1f);
        [SerializeField] private Color nightCameraColor = new Color(0.02f, 0.05f, 0.12f, 1f);

        private float _phaseTimer = 0f;
        private bool _isNight = false;

        public bool IsNight => _isNight;
        
        public float PhaseProgress => Mathf.Clamp01(_phaseTimer / (_isNight ? nightDuration : dayDuration));

        public event System.Action<bool> OnTimePhaseChanged;

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
            _phaseTimer = 0f;
            _isNight = false;
        }

        private void Update()
        {
            _phaseTimer += Time.deltaTime;
            float currentDuration = _isNight ? nightDuration : dayDuration;

            // Smoothly shift camera background to visually reflect day/night
            if (Camera.main != null)
            {
                Color targetColor = _isNight ? nightCameraColor : dayCameraColor;
                Camera.main.backgroundColor = Color.Lerp(Camera.main.backgroundColor, targetColor, Time.deltaTime * 2f);
            }

            if (_phaseTimer >= currentDuration)
            {
                _phaseTimer = 0f;
                _isNight = !_isNight;
                OnTimePhaseChanged?.Invoke(_isNight);
                Debug.Log($"[DayNightManager] Time Phase Changed! Is Night: {_isNight}");
            }
        }

        public void ToggleTimePhase()
        {
            _phaseTimer = 0f;
            _isNight = !_isNight;
            OnTimePhaseChanged?.Invoke(_isNight);
            Debug.Log($"[DayNightManager] Time Phase Toggled! Is Night: {_isNight}");
        }
    }
}
