using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Controls the sliding Time HUD (Dawn -> Noon -> Dusk -> Night),
    /// smoothly scrolling Content to the left as the day/night cycle progresses
    /// and seamlessly looping forever.
    /// </summary>
    public class TimeHUDController : MonoBehaviour
    {
        public static TimeHUDController Instance { get; private set; }

        [Header("Scroll References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform contentRect;

        [Header("Time Icons (Dawn, Noon, Dusk, Night)")]
        [SerializeField] private RectTransform dawnIcon;
        [SerializeField] private RectTransform noonIcon;
        [SerializeField] private RectTransform duskIcon;
        [SerializeField] private RectTransform nightIcon;

        [Header("Optional Text Label")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private bool showDayPrefix = true;

        [Header("Scroll Tuning")]
        [Tooltip("Smooth transition speed when interpolating position (higher = snappier)")]
        [SerializeField] private float smoothSpeed = 6f;

        private float _targetDawnX;
        private float _targetNoonX;
        private float _targetDuskX;
        private float _targetNightX;
        private float _targetLoopDawnX;
        private float _cycleWidth = 492f;
        private bool _isInitialized = false;
        private DayNightManager.TimePhase _lastPhase = DayNightManager.TimePhase.Day;
        private float _currentSmoothX;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            InitializeHUD();
        }

        private void Start()
        {
            InitializeHUD();
        }

        public void InitializeHUD()
        {
            if (_isInitialized) return;

            // 1. Auto-discover references if not explicitly assigned in inspector
            if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null)
            {
                // Disable user dragging so mouse input in gameplay cannot disrupt time HUD alignment
                scrollRect.horizontal = false;
                scrollRect.vertical = false;
                scrollRect.inertia = false;
                scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

                if (viewportRect == null) viewportRect = scrollRect.viewport;
                if (contentRect == null) contentRect = scrollRect.content;
            }

            if (contentRect != null)
            {
                if (dawnIcon == null) dawnIcon = contentRect.Find("Dawn") as RectTransform;
                if (noonIcon == null) noonIcon = contentRect.Find("Noon") as RectTransform;
                if (duskIcon == null) duskIcon = contentRect.Find("Dusk") as RectTransform;
                if (nightIcon == null) nightIcon = contentRect.Find("Night") as RectTransform;
            }

            if (timeText == null)
            {
                // Look for sibling "Time_text" under parent canvas
                if (transform.parent != null)
                {
                    Transform tTextTr = transform.parent.Find("Time_text");
                    if (tTextTr != null) timeText = tTextTr.GetComponent<TextMeshProUGUI>();
                }
                if (timeText == null && DayNightManager.Instance != null)
                {
                    timeText = DayNightManager.Instance.TimeText;
                }
            }

            if (dawnIcon == null || noonIcon == null || duskIcon == null || nightIcon == null || contentRect == null)
            {
                return;
            }

            // 2. Setup seamless infinite loop clones (Dawn_Loop and Noon_Loop)
            SetupLoopClones();

            // 3. Compute exact target anchor X coordinates relative to Viewport center
            ComputeTargetPositions();

            // Start at the current DayNightManager progress
            if (DayNightManager.Instance != null)
            {
                _currentSmoothX = GetTargetContentX(DayNightManager.Instance.CurrentPhase, DayNightManager.Instance.PhaseProgress, DayNightManager.Instance);
                contentRect.anchoredPosition = new Vector2(_currentSmoothX, contentRect.anchoredPosition.y);
            }
            else
            {
                _currentSmoothX = _targetDawnX;
                contentRect.anchoredPosition = new Vector2(_currentSmoothX, contentRect.anchoredPosition.y);
            }

            _isInitialized = true;
        }

        private void SetupLoopClones()
        {
            float spacing = Mathf.Abs(noonIcon.anchoredPosition.x - dawnIcon.anchoredPosition.x);
            if (spacing < 10f) spacing = 123f;
            _cycleWidth = spacing * 4f; // 492f

            // Clone Dawn as 5th icon (Dawn_Loop)
            Transform existingLoop = contentRect.Find("Dawn_Loop");
            RectTransform dawnLoop;
            if (existingLoop == null)
            {
                GameObject loopObj = Instantiate(dawnIcon.gameObject, contentRect);
                loopObj.name = "Dawn_Loop";
                dawnLoop = loopObj.GetComponent<RectTransform>();
                float loopX = nightIcon.anchoredPosition.x + spacing;
                dawnLoop.anchoredPosition = new Vector2(loopX, dawnIcon.anchoredPosition.y);
                dawnLoop.sizeDelta = dawnIcon.sizeDelta;
                dawnLoop.localScale = dawnIcon.localScale;
            }
            else
            {
                dawnLoop = existingLoop as RectTransform;
            }

            // Also clone Noon as 6th icon (Noon_Loop) so smooth scroll never runs out of content buffer
            Transform existingNoonLoop = contentRect.Find("Noon_Loop");
            if (existingNoonLoop == null && dawnLoop != null)
            {
                GameObject noonLoopObj = Instantiate(noonIcon.gameObject, contentRect);
                noonLoopObj.name = "Noon_Loop";
                RectTransform noonLoop = noonLoopObj.GetComponent<RectTransform>();
                float noonLoopX = dawnLoop.anchoredPosition.x + spacing;
                noonLoop.anchoredPosition = new Vector2(noonLoopX, noonIcon.anchoredPosition.y);
                noonLoop.sizeDelta = noonIcon.sizeDelta;
                noonLoop.localScale = noonIcon.localScale;
            }

            // Expand content sizeDelta so Unity layout doesn't restrict it
            float totalWidth = nightIcon.anchoredPosition.x + (spacing * 3f);
            contentRect.sizeDelta = new Vector2(totalWidth, contentRect.sizeDelta.y);
        }

        private void ComputeTargetPositions()
        {
            float vpWidth = (viewportRect != null) ? viewportRect.rect.width : 128.9945f;
            if (vpWidth <= 1f) vpWidth = 128.9945f;
            float vpCenter = vpWidth * 0.5f;

            _targetDawnX = vpCenter - dawnIcon.anchoredPosition.x;
            _targetNoonX = vpCenter - noonIcon.anchoredPosition.x;
            _targetDuskX = vpCenter - duskIcon.anchoredPosition.x;
            _targetNightX = vpCenter - nightIcon.anchoredPosition.x;

            RectTransform dawnLoop = contentRect.Find("Dawn_Loop") as RectTransform;
            if (dawnLoop != null)
            {
                _targetLoopDawnX = vpCenter - dawnLoop.anchoredPosition.x;
            }
            else
            {
                _targetLoopDawnX = _targetDawnX - _cycleWidth;
            }
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                InitializeHUD();
                if (!_isInitialized) return;
            }

            DayNightManager dnm = DayNightManager.Instance;
            if (dnm == null) return;

            DayNightManager.TimePhase currentPhase = dnm.CurrentPhase;
            float progress = dnm.PhaseProgress;

            // Handle seamless wrap when transitioning from Night to Day (new morning)
            if (_lastPhase == DayNightManager.TimePhase.Night && currentPhase == DayNightManager.TimePhase.Day)
            {
                // Wrap content position smoothly from Loop_Dawn back to Dawn
                float wrapOffset = _cycleWidth; // 492
                _currentSmoothX += wrapOffset;
                contentRect.anchoredPosition = new Vector2(_currentSmoothX, contentRect.anchoredPosition.y);
            }
            _lastPhase = currentPhase;

            float targetX = GetTargetContentX(currentPhase, progress, dnm);

            // Smooth interpolation to target position (moving steadily to the left)
            _currentSmoothX = Mathf.Lerp(_currentSmoothX, targetX, Time.deltaTime * smoothSpeed);
            contentRect.anchoredPosition = new Vector2(_currentSmoothX, contentRect.anchoredPosition.y);

            // Update Time_text label with current period and countdown
            UpdateTimeText(dnm);
        }

        private float GetTargetContentX(DayNightManager.TimePhase phase, float progress, DayNightManager dnm)
        {
            switch (phase)
            {
                case DayNightManager.TimePhase.Day:
                    // Day 0.0 -> 0.5 is Dawn -> Noon
                    // Day 0.5 -> 1.0 is Noon -> Dusk
                    if (progress < 0.5f)
                    {
                        float t = progress * 2f;
                        return Mathf.Lerp(_targetDawnX, _targetNoonX, t);
                    }
                    else
                    {
                        float t = (progress - 0.5f) * 2f;
                        return Mathf.Lerp(_targetNoonX, _targetDuskX, t);
                    }

                case DayNightManager.TimePhase.Dusk:
                    // Dusk 0.0 -> 1.0 is Dusk -> Night
                    return Mathf.Lerp(_targetDuskX, _targetNightX, progress);

                case DayNightManager.TimePhase.Night:
                    // Night 0.0 -> 1.0 is Night -> Dawn_Loop
                    return Mathf.Lerp(_targetNightX, _targetLoopDawnX, progress);

                default:
                    return _targetDawnX;
            }
        }

        private void UpdateTimeText(DayNightManager dnm)
        {
            if (timeText == null) return;

            string periodName;
            Color periodColor;
            float remainingSeconds;

            float phaseTimer = dnm.PhaseTimer;
            float dayDuration = dnm.DayDuration;
            float duskDuration = dnm.DuskDuration;
            float nightDuration = dnm.NightDuration;

            switch (dnm.CurrentPhase)
            {
                case DayNightManager.TimePhase.Day:
                    float halfDay = dayDuration * 0.5f;
                    if (phaseTimer < halfDay)
                    {
                        periodName = "DAWN";
                        periodColor = new Color(1f, 0.82f, 0.40f); // Warm morning sunrise gold
                        remainingSeconds = Mathf.Max(0f, halfDay - phaseTimer);
                    }
                    else
                    {
                        periodName = "NOON";
                        periodColor = new Color(1f, 0.95f, 0.50f); // Bright midday sun yellow
                        remainingSeconds = Mathf.Max(0f, dayDuration - phaseTimer);
                    }
                    break;

                case DayNightManager.TimePhase.Dusk:
                    periodName = "DUSK";
                    periodColor = new Color(1f, 0.55f, 0.20f); // Sunset amber orange
                    remainingSeconds = Mathf.Max(0f, duskDuration - phaseTimer);
                    break;

                case DayNightManager.TimePhase.Night:
                default:
                    periodName = "NIGHT";
                    periodColor = new Color(0.50f, 0.80f, 1f); // Moonlit ice blue
                    remainingSeconds = Mathf.Max(0f, nightDuration - phaseTimer);
                    break;
            }

            int sec = Mathf.CeilToInt(remainingSeconds);
            if (showDayPrefix)
            {
                timeText.text = $"DAY {dnm.DayCount} - {periodName} ({sec}s)";
            }
            else
            {
                timeText.text = $"{periodName} ({sec}s)";
            }
            timeText.color = periodColor;
        }

        /// <summary>
        /// Instantly snaps the HUD to the current time period without smooth interpolation.
        /// </summary>
        public void SnapToCurrentPhase()
        {
            if (!_isInitialized) InitializeHUD();
            if (DayNightManager.Instance != null)
            {
                _currentSmoothX = GetTargetContentX(DayNightManager.Instance.CurrentPhase, DayNightManager.Instance.PhaseProgress, DayNightManager.Instance);
                if (contentRect != null)
                {
                    contentRect.anchoredPosition = new Vector2(_currentSmoothX, contentRect.anchoredPosition.y);
                }
                UpdateTimeText(DayNightManager.Instance);
            }
        }
    }
}
