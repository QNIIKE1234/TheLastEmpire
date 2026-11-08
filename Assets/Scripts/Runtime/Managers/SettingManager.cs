using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// SettingManager — จัดการระบบการตั้งค่าทั้งหมดของเกมทั้ง 5 หมวดหมู่:
    /// 1. General (โหมดหน้าจอ, ความละเอียด, สั่นจอ, ตัวเลขดาเมจ, รีโหลดอัตโนมัติ)
    /// 2. Control (ความไวเมาส์, กลับทิศ Y, ตารางปุ่มควบคุมเกมทั้งหมด)
    /// 3. Sound (Master, BGM, SFX, UI Volume, ตัดเสียงเมื่อสลับจอ)
    /// 4. Graphic (ระดับกราฟิก, V-Sync, ลิมิตเฟรมเรต, เงา, Anti-Aliasing)
    /// 5. Accessory (เรดาร์แผนที่, หลอดเลือดศัตรู, เอฟเฟกต์เลือด, ขอบจอแดง)
    /// </summary>
    public class SettingManager : MonoBehaviour, IPopUp
    {
        public static SettingManager Instance { get; private set; }

        // =========================================================================
        //  PlayerPrefs Keys
        // =========================================================================
        public const string PrefMasterVolume = "Setting_MasterVolume";
        public const string PrefBgmVolume = "Setting_BGMVolume";
        public const string PrefSfxVolume = "Setting_SFXVolume";
        public const string PrefUiVolume = "Setting_UIVolume";
        public const string PrefMuteOnFocusLost = "Setting_MuteOnFocusLost";

        public const string PrefFullscreen = "Setting_Fullscreen";
        public const string PrefResolutionIndex = "Setting_ResolutionIndex";
        public const string PrefQualityLevel = "Setting_QualityLevel";
        public const string PrefVSync = "Setting_VSync";
        public const string PrefTargetFps = "Setting_TargetFps";
        public const string PrefShadowQuality = "Setting_ShadowQuality";
        public const string PrefAntiAliasing = "Setting_AntiAliasing";

        public const string PrefScreenShake = "Setting_ScreenShake";
        public const string PrefDamageNumbers = "Setting_DamageNumbers";
        public const string PrefAutoReload = "Setting_AutoReload";

        public const string PrefMouseSensitivity = "Setting_MouseSensitivity";
        public const string PrefInvertMouseY = "Setting_InvertMouseY";

        public const string PrefShowMinimap = "Setting_ShowMinimap";
        public const string PrefShowEnemyHealthBars = "Setting_ShowEnemyHealthBars";
        public const string PrefBloodGore = "Setting_BloodGore";
        public const string PrefLowHealthVignette = "Setting_LowHealthVignette";

        // Legacy key fallbacks
        public const string LegacyMasterVolume = "MasterVolume";
        public const string LegacyBgmVolume = "BGMVolume";
        public const string LegacySfxVolume = "SFXVolume";
        public const string LegacyFullscreen = "Fullscreen";

        // =========================================================================
        //  Default Values
        // =========================================================================
        public const float DefaultMasterVolume = 1.0f;
        public const float DefaultBgmVolume = 0.85f;
        public const float DefaultSfxVolume = 1.0f;
        public const float DefaultUiVolume = 1.0f;
        public const bool DefaultMuteOnFocusLost = false;

        public const bool DefaultFullscreen = true;
        public const int DefaultResolutionIndex = 0; // 1920x1080
        public const int DefaultQualityLevel = 2; // High/Medium
        public const bool DefaultVSync = false;
        public const int DefaultTargetFps = 1; // 60 FPS
        public const int DefaultShadowQuality = 2;
        public const int DefaultAntiAliasing = 1; // 2x

        public const bool DefaultScreenShake = true;
        public const bool DefaultDamageNumbers = true;
        public const bool DefaultAutoReload = true;

        public const float DefaultMouseSensitivity = 1.0f;
        public const bool DefaultInvertMouseY = false;

        public const bool DefaultShowMinimap = true;
        public const bool DefaultShowEnemyHealthBars = true;
        public const bool DefaultBloodGore = true;
        public const bool DefaultLowHealthVignette = true;

        // Supported standard screen resolutions
        public static readonly (int width, int height)[] Resolutions = new (int, int)[]
        {
            (1920, 1080),
            (2560, 1440),
            (1600, 900),
            (1366, 768),
            (1280, 720)
        };

        // =========================================================================
        //  Inspector References
        // =========================================================================
        [Header("Modal Root Window")]
        [Tooltip("กรอบหลักของหน้า Setting (เช่น SettingPanel)")]
        [SerializeField] private GameObject modalRoot;

        [Header("Category Tabs (Sidebar ฝั่งซ้าย)")]
        [SerializeField] private Button generalTabBtn;
        [SerializeField] private Button controlTabBtn;
        [SerializeField] private Button soundTabBtn;
        [SerializeField] private Button graphicTabBtn;
        [SerializeField] private Button accessoryTabBtn;

        [Header("Category Panels (แผงตั้งค่าฝั่งขวา)")]
        [SerializeField] private GameObject generalPanel;
        [SerializeField] private GameObject controlPanel;
        [SerializeField] private GameObject soundPanel;
        [SerializeField] private GameObject graphicPanel;
        [SerializeField] private GameObject accessoryPanel;

        [Header("Action Buttons (ล่างสุด)")]
        [SerializeField] private Button defaultButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button closeButton;

        // 1. General Controls
        [Header("1. General Controls")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle damageNumbersToggle;
        [SerializeField] private Toggle autoReloadToggle;

        // 2. Control Controls
        [Header("2. Control Controls")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private TMP_Text mouseSensitivityValueText;
        [SerializeField] private Toggle invertMouseYToggle;
        [SerializeField] private GameObject keybindListContainer;

        // 3. Sound Controls
        [Header("3. Sound Controls")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeValueText;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private TMP_Text bgmVolumeValueText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text sfxVolumeValueText;
        [SerializeField] private Slider uiVolumeSlider;
        [SerializeField] private TMP_Text uiVolumeValueText;
        [SerializeField] private Toggle muteOnFocusLostToggle;

        // 4. Graphic Controls
        [Header("4. Graphic Controls")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Dropdown targetFpsDropdown;
        [SerializeField] private TMP_Dropdown shadowQualityDropdown;
        [SerializeField] private TMP_Dropdown antiAliasingDropdown;

        // 5. Accessory Controls
        [Header("5. Accessory Controls")]
        [SerializeField] private Toggle showMinimapToggle;
        [SerializeField] private Toggle showEnemyHealthBarsToggle;
        [SerializeField] private Toggle bloodGoreToggle;
        [SerializeField] private Toggle lowHealthVignetteToggle;

        // =========================================================================
        //  Runtime Values
        // =========================================================================
        public enum SettingTab { General, Control, Sound, Graphic, Accessory }
        private SettingTab _currentTab = SettingTab.General;

        private float _masterVolume = DefaultMasterVolume;
        private float _bgmVolume = DefaultBgmVolume;
        private float _sfxVolume = DefaultSfxVolume;
        private float _uiVolume = DefaultUiVolume;
        private bool _muteOnFocusLost = DefaultMuteOnFocusLost;

        private bool _isFullscreen = DefaultFullscreen;
        private int _resolutionIndex = DefaultResolutionIndex;
        private int _qualityLevel = DefaultQualityLevel;
        private bool _vSync = DefaultVSync;
        private int _targetFpsIndex = DefaultTargetFps;
        private int _shadowQuality = DefaultShadowQuality;
        private int _antiAliasing = DefaultAntiAliasing;

        private bool _screenShake = DefaultScreenShake;
        private bool _damageNumbers = DefaultDamageNumbers;
        private bool _autoReload = DefaultAutoReload;

        private float _mouseSensitivity = DefaultMouseSensitivity;
        private bool _invertMouseY = DefaultInvertMouseY;

        private bool _showMinimap = DefaultShowMinimap;
        private bool _showEnemyHealthBars = DefaultShowEnemyHealthBars;
        private bool _bloodGore = DefaultBloodGore;
        private bool _lowHealthVignette = DefaultLowHealthVignette;

        // =========================================================================
        //  Public Accessors
        // =========================================================================
        public float MasterVolume => _masterVolume;
        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;
        public float UiVolume => _uiVolume;
        public bool MuteOnFocusLost => _muteOnFocusLost;

        public bool IsFullscreen => _isFullscreen;
        public int ResolutionIndex => _resolutionIndex;
        public int QualityLevel => _qualityLevel;
        public bool VSync => _vSync;
        public int TargetFpsIndex => _targetFpsIndex;

        public bool ScreenShake => _screenShake;
        public bool DamageNumbers => _damageNumbers;
        public bool AutoReload => _autoReload;

        public float MouseSensitivity => _mouseSensitivity;
        public bool InvertMouseY => _invertMouseY;

        public bool ShowMinimap => _showMinimap;
        public bool ShowEnemyHealthBars => _showEnemyHealthBars;
        public bool BloodGore => _bloodGore;
        public bool LowHealthVignette => _lowHealthVignette;

        public static bool IsDamageNumbersEnabled => Instance != null ? Instance._damageNumbers : true;
        public static bool IsCameraShakeEnabled => Instance != null ? Instance._screenShake : true;
        public static float CurrentMouseSensitivity => Instance != null ? Instance._mouseSensitivity : 1.0f;

        // Global Event Callbacks
        public static event Action<float> OnMasterVolumeChanged;
        public static event Action<float> OnBgmVolumeChanged;
        public static event Action<float> OnSfxVolumeChanged;
        public static event Action<float> OnUiVolumeChanged;
        public static event Action<bool> OnFullscreenChanged;
        public static event Action<float> OnMouseSensitivityChanged;

        // =========================================================================
        //  Unity Lifecycle
        // =========================================================================
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (Instance != this)
            {
                Instance.SyncUIReferencesFrom(this);
                Destroy(gameObject);
                return;
            }

            ResolveModalRoot();
            LoadSettings();
            EnsurePanelsBuilt();
            AutoWireUIElements();
            BindUIEvents();
            ApplySettings();
        }

        private void OnEnable()
        {
            RefreshUI();
            SwitchCategoryTab(_currentTab);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_muteOnFocusLost)
            {
                AudioListener.pause = !hasFocus;
            }
        }

        // =========================================================================
        //  Resolve Root & Panels Auto-Construction
        // =========================================================================
        private void ResolveModalRoot()
        {
            if (modalRoot == null || modalRoot == gameObject || modalRoot.name == "Setting")
            {
                Transform panel = transform.Find("SettingPanel");
                if (panel == null && transform.parent != null)
                {
                    panel = transform.parent.name.Contains("Setting") ? transform.parent : transform.parent.Find("SettingPanel");
                }

                if (panel != null)
                {
                    modalRoot = panel.gameObject;
                }
                else if (modalRoot == null)
                {
                    modalRoot = gameObject;
                }
            }
        }

        /// <summary>
        /// ตรวจสอบและสร้าง Panel ทั้ง 5 หมวดหมู่ให้อัตโนมัติ หากในฉากยังไม่ได้สร้างเตรียมไว้
        /// </summary>
        public void EnsurePanelsBuilt()
        {
            Transform contentContainer = null;
            if (modalRoot != null)
            {
                contentContainer = modalRoot.transform.Find("Setting/SettingsContent");
                if (contentContainer == null)
                {
                    contentContainer = modalRoot.transform.Find("Setting");
                }
            }

            if (contentContainer == null) return;

            // ตรวจสอบ Panel เดิมหรือสร้างใหม่
            generalPanel   = FindOrCreateSubPanel(contentContainer, "Panel_General", ref generalPanel);
            controlPanel   = FindOrCreateSubPanel(contentContainer, "Panel_Control", ref controlPanel);
            soundPanel     = FindOrCreateSubPanel(contentContainer, "Panel_Sound", ref soundPanel);
            graphicPanel   = FindOrCreateSubPanel(contentContainer, "Panel_Graphic", ref graphicPanel);
            accessoryPanel = FindOrCreateSubPanel(contentContainer, "Panel_Accessory", ref accessoryPanel);

            // ย้ายสิ่งที่มีอยู่เดิมเข้าหมวดหมู่ให้ถูกต้อง
            PopulatePanelsIfEmpty(contentContainer);
        }

        private GameObject FindOrCreateSubPanel(Transform parent, string panelName, ref GameObject targetRef)
        {
            Transform existing = parent.Find(panelName);
            if (existing != null)
            {
                targetRef = existing.gameObject;
                return existing.gameObject;
            }

            GameObject panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            targetRef = panel;
            return panel;
        }

        private void PopulatePanelsIfEmpty(Transform container)
        {
            // ดึง Template จาก SliderRow และ ToggleRow ที่มีอยู่เดิมใน SettingsContent
            Transform sliderTemplate = null;
            Transform toggleTemplate = null;

            foreach (Transform child in container)
            {
                if (child.name.StartsWith("Panel_")) continue;
                if (child.GetComponentInChildren<Slider>(true) != null && sliderTemplate == null)
                {
                    sliderTemplate = child;
                }
                if (child.GetComponentInChildren<Toggle>(true) != null && toggleTemplate == null)
                {
                    toggleTemplate = child;
                }
            }

            // ถ้ามี Template ให้นำมาจัดสรรลงแต่ละหมวดหมู่
            if (sliderTemplate != null || toggleTemplate != null)
            {
                // 1. หมวด SOUND: ย้ายหรือสร้าง Slider คุมเสียง Master, BGM, SFX, UI
                if (soundPanel != null && soundPanel.transform.childCount == 0)
                {
                    BuildSliderRow(soundPanel.transform, sliderTemplate, "Master Audio Volume", _masterVolume, out masterVolumeSlider, out masterVolumeValueText);
                    BuildSliderRow(soundPanel.transform, sliderTemplate, "BGM Music Volume", _bgmVolume, out bgmVolumeSlider, out bgmVolumeValueText);
                    BuildSliderRow(soundPanel.transform, sliderTemplate, "SFX Sound Volume", _sfxVolume, out sfxVolumeSlider, out sfxVolumeValueText);
                    BuildSliderRow(soundPanel.transform, sliderTemplate, "UI Sound Volume", _uiVolume, out uiVolumeSlider, out uiVolumeValueText);
                    BuildToggleRow(soundPanel.transform, toggleTemplate, "Mute When Unfocused", _muteOnFocusLost, out muteOnFocusLostToggle);
                }

                // 2. หมวด GENERAL: Fullscreen, Screen Shake, Damage Numbers, Auto Reload
                if (generalPanel != null && generalPanel.transform.childCount == 0)
                {
                    BuildToggleRow(generalPanel.transform, toggleTemplate, "Fullscreen Mode", _isFullscreen, out fullscreenToggle);
                    BuildToggleRow(generalPanel.transform, toggleTemplate, "Screen Shake Effect", _screenShake, out screenShakeToggle);
                    BuildToggleRow(generalPanel.transform, toggleTemplate, "Floating Damage Numbers", _damageNumbers, out damageNumbersToggle);
                    BuildToggleRow(generalPanel.transform, toggleTemplate, "Auto Reload Empty Weapon", _autoReload, out autoReloadToggle);
                }

                // 3. หมวด CONTROL: Mouse Sensitivity, Invert Y, Keybinds
                if (controlPanel != null && controlPanel.transform.childCount == 0)
                {
                    BuildSliderRow(controlPanel.transform, sliderTemplate, "Mouse Sensitivity", _mouseSensitivity / 3.0f, out mouseSensitivitySlider, out mouseSensitivityValueText);
                    BuildToggleRow(controlPanel.transform, toggleTemplate, "Invert Mouse Y-Axis", _invertMouseY, out invertMouseYToggle);
                    BuildKeybindCard(controlPanel.transform);
                }

                // 4. หมวด GRAPHIC: V-Sync, Anti-Aliasing, Shadows
                if (graphicPanel != null && graphicPanel.transform.childCount == 0)
                {
                    BuildToggleRow(graphicPanel.transform, toggleTemplate, "V-Sync (Vertical Sync)", _vSync, out vSyncToggle);
                    BuildToggleRow(graphicPanel.transform, toggleTemplate, "High Dynamic Shadows", _shadowQuality > 0, out _);
                    BuildToggleRow(graphicPanel.transform, toggleTemplate, "Anti-Aliasing Smoothing", _antiAliasing > 0, out _);
                }

                // 5. หมวด ACCESSORY: Minimap, Health Bars, Blood & Gore, Vignette
                if (accessoryPanel != null && accessoryPanel.transform.childCount == 0)
                {
                    BuildToggleRow(accessoryPanel.transform, toggleTemplate, "Show Minimap Radar", _showMinimap, out showMinimapToggle);
                    BuildToggleRow(accessoryPanel.transform, toggleTemplate, "Show Enemy Health Bars", _showEnemyHealthBars, out showEnemyHealthBarsToggle);
                    BuildToggleRow(accessoryPanel.transform, toggleTemplate, "Blood & Gore VFX", _bloodGore, out bloodGoreToggle);
                    BuildToggleRow(accessoryPanel.transform, toggleTemplate, "Low Health Red Vignette", _lowHealthVignette, out lowHealthVignetteToggle);
                }

                // ซ่อน Template ดั้งเดิมที่อยู่นอก Panel
                foreach (Transform child in container)
                {
                    if (!child.name.StartsWith("Panel_") && child.name != "BG" && child.name != "PanelBorder" && child.name != "DefaultBtn" && child.name != "SaveAndBackBtn")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void BuildSliderRow(Transform parent, Transform template, string title, float initialVal, out Slider outSlider, out TMP_Text outText)
        {
            outSlider = null;
            outText = null;
            if (template == null) return;

            GameObject row = Instantiate(template.gameObject, parent);
            row.name = $"Row_{title.Replace(" ", "")}";
            row.SetActive(true);

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0) texts[0].text = title;
            if (texts.Length > 1)
            {
                outText = texts[1];
                outText.text = $"{Mathf.RoundToInt(initialVal * 100)}%";
            }

            outSlider = row.GetComponentInChildren<Slider>(true);
            if (outSlider != null)
            {
                outSlider.SetValueWithoutNotify(initialVal);
            }
        }

        private void BuildToggleRow(Transform parent, Transform template, string title, bool initialVal, out Toggle outToggle)
        {
            outToggle = null;
            if (template == null) return;

            GameObject row = Instantiate(template.gameObject, parent);
            row.name = $"Row_{title.Replace(" ", "")}";
            row.SetActive(true);

            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = title;

            outToggle = row.GetComponentInChildren<Toggle>(true);
            if (outToggle != null)
            {
                outToggle.SetIsOnWithoutNotify(initialVal);
            }
        }

        private void BuildKeybindCard(Transform parent)
        {
            GameObject card = new GameObject("KeybindReferenceCard");
            card.transform.SetParent(parent, false);

            RectTransform rt = card.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 220f);

            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);

            GameObject textObj = new GameObject("KeybindText");
            textObj.transform.SetParent(card.transform, false);

            RectTransform trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20f, 10f);
            trt.offsetMax = new Vector2(-20f, -10f);

            TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 15f;
            text.lineSpacing = 6f;
            text.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            text.richText = true;
            text.text =
                "<b><color=#F59E0B>SURVIVAL CONTROLS REFERENCE // ปุ่มควบคุม</color></b>\n" +
                "<color=#60A5FA>Move:</color> [W, A, S, D]        <color=#60A5FA>Aim / Shoot:</color> [Mouse Aim / Left Click]\n" +
                "<color=#60A5FA>Dash / Dodge:</color> [Spacebar]   <color=#60A5FA>Signature Skill:</color> [Left Shift]\n" +
                "<color=#60A5FA>Interact / Talk:</color> [E]       <color=#60A5FA>Reload Weapon:</color> [R]\n" +
                "<color=#60A5FA>Inventory Bag:</color> [Tab / I]   <color=#60A5FA>Crafting Menu:</color> [C]\n" +
                "<color=#60A5FA>Quick Item Slot:</color> [1, 2, 3, 4]  <color=#F59E0B>Freecraft Mode:</color> [F8]  |  <color=#F59E0B>Cheat:</color> [F1]";
        }

        // =========================================================================
        //  Load / Save / Apply Settings
        // =========================================================================
        public void LoadSettings()
        {
            // 1. Audio
            _masterVolume = PlayerPrefs.GetFloat(PrefMasterVolume, PlayerPrefs.GetFloat(LegacyMasterVolume, DefaultMasterVolume));
            _bgmVolume    = PlayerPrefs.GetFloat(PrefBgmVolume, PlayerPrefs.GetFloat(LegacyBgmVolume, DefaultBgmVolume));
            _sfxVolume    = PlayerPrefs.GetFloat(PrefSfxVolume, PlayerPrefs.GetFloat(LegacySfxVolume, DefaultSfxVolume));
            _uiVolume     = PlayerPrefs.GetFloat(PrefUiVolume, DefaultUiVolume);
            _muteOnFocusLost = PlayerPrefs.GetInt(PrefMuteOnFocusLost, DefaultMuteOnFocusLost ? 1 : 0) == 1;

            // 2. Display & Graphic
            _isFullscreen = PlayerPrefs.GetInt(PrefFullscreen, PlayerPrefs.GetInt(LegacyFullscreen, Screen.fullScreen ? 1 : 0)) == 1;
            _resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefResolutionIndex, DefaultResolutionIndex), 0, Resolutions.Length - 1);
            _qualityLevel = PlayerPrefs.GetInt(PrefQualityLevel, QualitySettings.GetQualityLevel());
            _vSync = PlayerPrefs.GetInt(PrefVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            _targetFpsIndex = PlayerPrefs.GetInt(PrefTargetFps, DefaultTargetFps);
            _shadowQuality = PlayerPrefs.GetInt(PrefShadowQuality, DefaultShadowQuality);
            _antiAliasing = PlayerPrefs.GetInt(PrefAntiAliasing, DefaultAntiAliasing);

            // 3. General
            _screenShake = PlayerPrefs.GetInt(PrefScreenShake, DefaultScreenShake ? 1 : 0) == 1;
            _damageNumbers = PlayerPrefs.GetInt(PrefDamageNumbers, DefaultDamageNumbers ? 1 : 0) == 1;
            _autoReload = PlayerPrefs.GetInt(PrefAutoReload, DefaultAutoReload ? 1 : 0) == 1;

            // 4. Control
            _mouseSensitivity = PlayerPrefs.GetFloat(PrefMouseSensitivity, DefaultMouseSensitivity);
            _invertMouseY = PlayerPrefs.GetInt(PrefInvertMouseY, DefaultInvertMouseY ? 1 : 0) == 1;

            // 5. Accessory
            _showMinimap = PlayerPrefs.GetInt(PrefShowMinimap, DefaultShowMinimap ? 1 : 0) == 1;
            _showEnemyHealthBars = PlayerPrefs.GetInt(PrefShowEnemyHealthBars, DefaultShowEnemyHealthBars ? 1 : 0) == 1;
            _bloodGore = PlayerPrefs.GetInt(PrefBloodGore, DefaultBloodGore ? 1 : 0) == 1;
            _lowHealthVignette = PlayerPrefs.GetInt(PrefLowHealthVignette, DefaultLowHealthVignette ? 1 : 0) == 1;
        }

        public void SaveSettings()
        {
            // Audio
            PlayerPrefs.SetFloat(PrefMasterVolume, _masterVolume);
            PlayerPrefs.SetFloat(PrefBgmVolume, _bgmVolume);
            PlayerPrefs.SetFloat(PrefSfxVolume, _sfxVolume);
            PlayerPrefs.SetFloat(PrefUiVolume, _uiVolume);
            PlayerPrefs.SetInt(PrefMuteOnFocusLost, _muteOnFocusLost ? 1 : 0);

            PlayerPrefs.SetFloat(LegacyMasterVolume, _masterVolume);
            PlayerPrefs.SetFloat(LegacyBgmVolume, _bgmVolume);
            PlayerPrefs.SetFloat(LegacySfxVolume, _sfxVolume);

            // Display & Graphics
            PlayerPrefs.SetInt(PrefFullscreen, _isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt(LegacyFullscreen, _isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PrefResolutionIndex, _resolutionIndex);
            PlayerPrefs.SetInt(PrefQualityLevel, _qualityLevel);
            PlayerPrefs.SetInt(PrefVSync, _vSync ? 1 : 0);
            PlayerPrefs.SetInt(PrefTargetFps, _targetFpsIndex);
            PlayerPrefs.SetInt(PrefShadowQuality, _shadowQuality);
            PlayerPrefs.SetInt(PrefAntiAliasing, _antiAliasing);

            // General
            PlayerPrefs.SetInt(PrefScreenShake, _screenShake ? 1 : 0);
            PlayerPrefs.SetInt(PrefDamageNumbers, _damageNumbers ? 1 : 0);
            PlayerPrefs.SetInt(PrefAutoReload, _autoReload ? 1 : 0);

            // Control
            PlayerPrefs.SetFloat(PrefMouseSensitivity, _mouseSensitivity);
            PlayerPrefs.SetInt(PrefInvertMouseY, _invertMouseY ? 1 : 0);

            // Accessory
            PlayerPrefs.SetInt(PrefShowMinimap, _showMinimap ? 1 : 0);
            PlayerPrefs.SetInt(PrefShowEnemyHealthBars, _showEnemyHealthBars ? 1 : 0);
            PlayerPrefs.SetInt(PrefBloodGore, _bloodGore ? 1 : 0);
            PlayerPrefs.SetInt(PrefLowHealthVignette, _lowHealthVignette ? 1 : 0);

            PlayerPrefs.Save();
            Debug.Log("[SettingManager] All settings saved successfully.");
        }

        public void ApplySettings()
        {
            // Audio
            AudioListener.volume = _masterVolume;

            // Display Mode & Resolution
            if (_resolutionIndex >= 0 && _resolutionIndex < Resolutions.Length)
            {
                var (w, h) = Resolutions[_resolutionIndex];
                Screen.SetResolution(w, h, _isFullscreen);
            }
            else
            {
                Screen.fullScreen = _isFullscreen;
            }

            // Quality & Performance
            if (QualitySettings.GetQualityLevel() != _qualityLevel && _qualityLevel >= 0 && _qualityLevel < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(_qualityLevel, true);
            }

            QualitySettings.vSyncCount = _vSync ? 1 : 0;

            int targetFps = _targetFpsIndex switch
            {
                0 => 30,
                1 => 60,
                2 => 120,
                3 => 144,
                _ => -1
            };
            Application.targetFrameRate = targetFps;

            // Shadows & Anti-Aliasing
            QualitySettings.shadows = _shadowQuality == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.antiAliasing = _antiAliasing switch
            {
                1 => 2,
                2 => 4,
                3 => 8,
                _ => 0
            };

            // Call Global Event Listeners
            OnMasterVolumeChanged?.Invoke(_masterVolume);
            OnBgmVolumeChanged?.Invoke(_bgmVolume);
            OnSfxVolumeChanged?.Invoke(_sfxVolume);
            OnUiVolumeChanged?.Invoke(_uiVolume);
            OnFullscreenChanged?.Invoke(_isFullscreen);
            OnMouseSensitivityChanged?.Invoke(_mouseSensitivity);
        }

        // =========================================================================
        //  Category Tabs Switching
        // =========================================================================
        public void SwitchCategoryTab(SettingTab tab)
        {
            _currentTab = tab;

            if (generalPanel != null) generalPanel.SetActive(tab == SettingTab.General);
            if (controlPanel != null) controlPanel.SetActive(tab == SettingTab.Control);
            if (soundPanel != null) soundPanel.SetActive(tab == SettingTab.Sound);
            if (graphicPanel != null) graphicPanel.SetActive(tab == SettingTab.Graphic);
            if (accessoryPanel != null) accessoryPanel.SetActive(tab == SettingTab.Accessory);

            UpdateTabButtonVisuals(generalTabBtn, tab == SettingTab.General);
            UpdateTabButtonVisuals(controlTabBtn, tab == SettingTab.Control);
            UpdateTabButtonVisuals(soundTabBtn, tab == SettingTab.Sound);
            UpdateTabButtonVisuals(graphicTabBtn, tab == SettingTab.Graphic);
            UpdateTabButtonVisuals(accessoryTabBtn, tab == SettingTab.Accessory);

            Debug.Log($"[SettingManager] Active tab: {tab}");
        }

        private void UpdateTabButtonVisuals(Button btn, bool isActive)
        {
            if (btn == null) return;
            btn.interactable = !isActive;

            ColorBlock cb = btn.colors;
            cb.disabledColor = new Color(1f, 1f, 1f, 1f); // แท็บที่เลือก = ขาวเต็ม 100% สว่างคม
            cb.normalColor = new Color(0.6f, 0.6f, 0.6f, 0.65f); // แท็บที่ไม่ได้เลือก = มืดลง
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            btn.colors = cb;

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = isActive ? Color.white : new Color(0.7f, 0.7f, 0.75f, 0.8f);
            }
        }

        // =========================================================================
        //  Auto-Wire & Events
        // =========================================================================
        private void AutoWireUIElements()
        {
            Transform searchRoot = modalRoot != null ? modalRoot.transform : transform;

            // 1. Action Buttons
            Button[] buttons = searchRoot.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string bName = btn.gameObject.name.ToLowerInvariant();
                TMP_Text bText = btn.GetComponentInChildren<TMP_Text>(true);
                string textContent = bText != null ? bText.text.ToLowerInvariant() : "";

                if (defaultButton == null && (bName.Contains("default") || textContent.Contains("default"))) defaultButton = btn;
                else if (applyButton == null && (bName.Contains("apply") || bName.Contains("save") || textContent.Contains("apply") || textContent.Contains("save"))) applyButton = btn;
                else if (exitButton == null && (bName.Contains("exit") || textContent.Contains("exit"))) exitButton = btn;
                else if (closeButton == null && (bName.Contains("close") || bName == "cancel" || bName == "back" || textContent.Contains("close") || textContent.Contains("cancel"))) closeButton = btn;

                // Category Tabs
                if (generalTabBtn == null && (bName == "general" || textContent.Contains("general"))) generalTabBtn = btn;
                else if (controlTabBtn == null && (bName == "control" || textContent.Contains("control"))) controlTabBtn = btn;
                else if (soundTabBtn == null && (bName == "sound" || textContent.Contains("sound"))) soundTabBtn = btn;
                else if (graphicTabBtn == null && (bName == "graphic" || textContent.Contains("graphic"))) graphicTabBtn = btn;
                else if (accessoryTabBtn == null && (bName.Contains("acces") || textContent.Contains("acces"))) accessoryTabBtn = btn;
            }
        }

        private void BindUIEvents()
        {
            // Action Buttons
            if (defaultButton != null)
            {
                defaultButton.onClick.RemoveListener(OnResetDefault);
                defaultButton.onClick.AddListener(OnResetDefault);
            }
            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(OnApplySettings);
                applyButton.onClick.AddListener(OnApplySettings);
            }
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitClicked);
                exitButton.onClick.AddListener(OnExitClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseSettingsModal);
                closeButton.onClick.AddListener(CloseSettingsModal);
            }

            // Category Tab Buttons
            if (generalTabBtn != null)
            {
                generalTabBtn.onClick.RemoveAllListeners();
                generalTabBtn.onClick.AddListener(() => SwitchCategoryTab(SettingTab.General));
            }
            if (controlTabBtn != null)
            {
                controlTabBtn.onClick.RemoveAllListeners();
                controlTabBtn.onClick.AddListener(() => SwitchCategoryTab(SettingTab.Control));
            }
            if (soundTabBtn != null)
            {
                soundTabBtn.onClick.RemoveAllListeners();
                soundTabBtn.onClick.AddListener(() => SwitchCategoryTab(SettingTab.Sound));
            }
            if (graphicTabBtn != null)
            {
                graphicTabBtn.onClick.RemoveAllListeners();
                graphicTabBtn.onClick.AddListener(() => SwitchCategoryTab(SettingTab.Graphic));
            }
            if (accessoryTabBtn != null)
            {
                accessoryTabBtn.onClick.RemoveAllListeners();
                accessoryTabBtn.onClick.AddListener(() => SwitchCategoryTab(SettingTab.Accessory));
            }

            // 1. General Controls
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.onValueChanged.AddListener((val) => { _isFullscreen = val; Screen.fullScreen = val; });
            }
            if (screenShakeToggle != null)
            {
                screenShakeToggle.onValueChanged.RemoveAllListeners();
                screenShakeToggle.onValueChanged.AddListener((val) => _screenShake = val);
            }
            if (damageNumbersToggle != null)
            {
                damageNumbersToggle.onValueChanged.RemoveAllListeners();
                damageNumbersToggle.onValueChanged.AddListener((val) => _damageNumbers = val);
            }
            if (autoReloadToggle != null)
            {
                autoReloadToggle.onValueChanged.RemoveAllListeners();
                autoReloadToggle.onValueChanged.AddListener((val) => _autoReload = val);
            }

            // 2. Control Controls
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
                mouseSensitivitySlider.onValueChanged.AddListener((val) =>
                {
                    _mouseSensitivity = Mathf.Clamp(val * 3.0f, 0.2f, 3.0f);
                    if (mouseSensitivityValueText != null) mouseSensitivityValueText.text = $"{_mouseSensitivity:F1}x";
                    OnMouseSensitivityChanged?.Invoke(_mouseSensitivity);
                });
            }
            if (invertMouseYToggle != null)
            {
                invertMouseYToggle.onValueChanged.RemoveAllListeners();
                invertMouseYToggle.onValueChanged.AddListener((val) => _invertMouseY = val);
            }

            // 3. Sound Controls
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveAllListeners();
                masterVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    _masterVolume = Mathf.Clamp01(val);
                    if (masterVolumeValueText != null) masterVolumeValueText.text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
                    AudioListener.volume = _masterVolume;
                    OnMasterVolumeChanged?.Invoke(_masterVolume);
                });
            }
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.RemoveAllListeners();
                bgmVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    _bgmVolume = Mathf.Clamp01(val);
                    if (bgmVolumeValueText != null) bgmVolumeValueText.text = $"{Mathf.RoundToInt(_bgmVolume * 100)}%";
                    OnBgmVolumeChanged?.Invoke(_bgmVolume);
                });
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                sfxVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    _sfxVolume = Mathf.Clamp01(val);
                    if (sfxVolumeValueText != null) sfxVolumeValueText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}%";
                    OnSfxVolumeChanged?.Invoke(_sfxVolume);
                });
            }
            if (uiVolumeSlider != null)
            {
                uiVolumeSlider.onValueChanged.RemoveAllListeners();
                uiVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    _uiVolume = Mathf.Clamp01(val);
                    if (uiVolumeValueText != null) uiVolumeValueText.text = $"{Mathf.RoundToInt(_uiVolume * 100)}%";
                    OnUiVolumeChanged?.Invoke(_uiVolume);
                });
            }
            if (muteOnFocusLostToggle != null)
            {
                muteOnFocusLostToggle.onValueChanged.RemoveAllListeners();
                muteOnFocusLostToggle.onValueChanged.AddListener((val) => _muteOnFocusLost = val);
            }

            // 4. Graphic Controls
            if (vSyncToggle != null)
            {
                vSyncToggle.onValueChanged.RemoveAllListeners();
                vSyncToggle.onValueChanged.AddListener((val) => { _vSync = val; QualitySettings.vSyncCount = val ? 1 : 0; });
            }

            // 5. Accessory Controls
            if (showMinimapToggle != null)
            {
                showMinimapToggle.onValueChanged.RemoveAllListeners();
                showMinimapToggle.onValueChanged.AddListener((val) => _showMinimap = val);
            }
            if (showEnemyHealthBarsToggle != null)
            {
                showEnemyHealthBarsToggle.onValueChanged.RemoveAllListeners();
                showEnemyHealthBarsToggle.onValueChanged.AddListener((val) => _showEnemyHealthBars = val);
            }
            if (bloodGoreToggle != null)
            {
                bloodGoreToggle.onValueChanged.RemoveAllListeners();
                bloodGoreToggle.onValueChanged.AddListener((val) => _bloodGore = val);
            }
            if (lowHealthVignetteToggle != null)
            {
                lowHealthVignetteToggle.onValueChanged.RemoveAllListeners();
                lowHealthVignetteToggle.onValueChanged.AddListener((val) => _lowHealthVignette = val);
            }
        }

        public void RefreshUI()
        {
            // 1. General
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(_isFullscreen);
            if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(_screenShake);
            if (damageNumbersToggle != null) damageNumbersToggle.SetIsOnWithoutNotify(_damageNumbers);
            if (autoReloadToggle != null) autoReloadToggle.SetIsOnWithoutNotify(_autoReload);

            // 2. Control
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(_mouseSensitivity / 3.0f);
            if (mouseSensitivityValueText != null) mouseSensitivityValueText.text = $"{_mouseSensitivity:F1}x";
            if (invertMouseYToggle != null) invertMouseYToggle.SetIsOnWithoutNotify(_invertMouseY);

            // 3. Sound
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(_masterVolume);
            if (masterVolumeValueText != null) masterVolumeValueText.text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
            if (bgmVolumeSlider != null) bgmVolumeSlider.SetValueWithoutNotify(_bgmVolume);
            if (bgmVolumeValueText != null) bgmVolumeValueText.text = $"{Mathf.RoundToInt(_bgmVolume * 100)}%";
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(_sfxVolume);
            if (sfxVolumeValueText != null) sfxVolumeValueText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}%";
            if (uiVolumeSlider != null) uiVolumeSlider.SetValueWithoutNotify(_uiVolume);
            if (uiVolumeValueText != null) uiVolumeValueText.text = $"{Mathf.RoundToInt(_uiVolume * 100)}%";
            if (muteOnFocusLostToggle != null) muteOnFocusLostToggle.SetIsOnWithoutNotify(_muteOnFocusLost);

            // 4. Graphic
            if (vSyncToggle != null) vSyncToggle.SetIsOnWithoutNotify(_vSync);

            // 5. Accessory
            if (showMinimapToggle != null) showMinimapToggle.SetIsOnWithoutNotify(_showMinimap);
            if (showEnemyHealthBarsToggle != null) showEnemyHealthBarsToggle.SetIsOnWithoutNotify(_showEnemyHealthBars);
            if (bloodGoreToggle != null) bloodGoreToggle.SetIsOnWithoutNotify(_bloodGore);
            if (lowHealthVignetteToggle != null) lowHealthVignetteToggle.SetIsOnWithoutNotify(_lowHealthVignette);
        }

        private void SyncUIReferencesFrom(SettingManager other)
        {
            if (other == null) return;
            modalRoot = other.modalRoot;

            generalTabBtn = other.generalTabBtn;
            controlTabBtn = other.controlTabBtn;
            soundTabBtn = other.soundTabBtn;
            graphicTabBtn = other.graphicTabBtn;
            accessoryTabBtn = other.accessoryTabBtn;

            generalPanel = other.generalPanel;
            controlPanel = other.controlPanel;
            soundPanel = other.soundPanel;
            graphicPanel = other.graphicPanel;
            accessoryPanel = other.accessoryPanel;

            defaultButton = other.defaultButton;
            applyButton = other.applyButton;
            exitButton = other.exitButton;

            ResolveModalRoot();
            AutoWireUIElements();
            BindUIEvents();
            RefreshUI();
        }

        // =========================================================================
        //  Action Buttons Handlers
        // =========================================================================
        public void OnResetDefault()
        {
            _masterVolume = DefaultMasterVolume;
            _bgmVolume = DefaultBgmVolume;
            _sfxVolume = DefaultSfxVolume;
            _uiVolume = DefaultUiVolume;
            _muteOnFocusLost = DefaultMuteOnFocusLost;

            _isFullscreen = DefaultFullscreen;
            _resolutionIndex = DefaultResolutionIndex;
            _qualityLevel = DefaultQualityLevel;
            _vSync = DefaultVSync;
            _targetFpsIndex = DefaultTargetFps;

            _screenShake = DefaultScreenShake;
            _damageNumbers = DefaultDamageNumbers;
            _autoReload = DefaultAutoReload;

            _mouseSensitivity = DefaultMouseSensitivity;
            _invertMouseY = DefaultInvertMouseY;

            _showMinimap = DefaultShowMinimap;
            _showEnemyHealthBars = DefaultShowEnemyHealthBars;
            _bloodGore = DefaultBloodGore;
            _lowHealthVignette = DefaultLowHealthVignette;

            RefreshUI();
            ApplySettings();
            Debug.Log("[SettingManager] Reset all settings to default values.");
        }

        public void OnApplySettings()
        {
            SaveSettings();
            ApplySettings();
            CloseSettingsModal();
            Debug.Log("[SettingManager] Applied all settings and closed modal.");
        }

        public void OnExitClicked()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Equals("MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[SettingManager] Exit clicked in MainMenu -> Quitting game.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else
            {
                Debug.Log("[SettingManager] Exit clicked in Gameplay -> Returning to MainMenu.");
                Time.timeScale = 1f;
                CloseSettingsModal();
                SceneManager.LoadScene("MainMenu");
            }
        }

        public bool IsOpen
        {
            get
            {
                if (modalRoot == null) ResolveModalRoot();
                return modalRoot != null && modalRoot.activeSelf;
            }
        }

        public void OpenSettingsModal()
        {
            ResolveModalRoot();
            LoadSettings();
            EnsurePanelsBuilt();
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }
            RefreshUI();
            SwitchCategoryTab(_currentTab);
        }

        public void CloseSettingsModal()
        {
            ResolveModalRoot();
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }
        }

        public void ToggleSettingsModal()
        {
            if (IsOpen)
            {
                CloseSettingsModal();
            }
            else
            {
                OpenSettingsModal();
            }
        }

        public void ClosePopUp()
        {
            CloseSettingsModal();
        }
    }

    /// <summary>
    /// Alias class supporting plural naming convention (SettingsManager).
    /// </summary>
    public class SettingsManager : SettingManager
    {
    }
}
