using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// SettingManager — ควบคุมระบบการตั้งค่าทั้งหมดของเกม (Audio Volume, Display Mode, Tabs)
    /// ทำหน้าที่เป็น Single Source of Truth สำหรับ Settings:
    /// - จัดการ UI Sliders, Percentage Text, Checkbox/Toggle, Buttons (Default, Apply, Exit)
    /// - สลับ Category Tabs (General, Control, Sound, Graphic, Accessory)
    /// - จัดเก็บและโหลดค่าผ่าน PlayerPrefs พร้อมส่ง Event แจ้งเตือนระบบเสียง/หน้าจอ
    /// </summary>
    public class SettingManager : MonoBehaviour
    {
        public static SettingManager Instance { get; private set; }

        public const string PrefMasterVolume = "Setting_MasterVolume";
        public const string PrefBgmVolume = "Setting_BGMVolume";
        public const string PrefSfxVolume = "Setting_SFXVolume";
        public const string PrefFullscreen = "Setting_Fullscreen";

        // Legacy key fallbacks
        private const string LegacyMasterVolume = "MasterVolume";
        private const string LegacyBgmVolume = "BGMVolume";
        private const string LegacySfxVolume = "SFXVolume";
        private const string LegacyFullscreen = "Fullscreen";

        // Default constant values
        public const float DefaultMasterVolume = 1.0f;
        public const float DefaultBgmVolume = 0.85f;
        public const float DefaultSfxVolume = 1.0f;
        public const bool DefaultFullscreen = true;

        [Header("Modal Root Window")]
        [Tooltip("The root GameObject of the Settings Modal (e.g. SettingPanel). Toggled on Open/Close.")]
        [SerializeField] private GameObject modalRoot;

        [Header("Category Tabs (Sidebar)")]
        [SerializeField] private Button generalTabBtn;
        [SerializeField] private Button controlTabBtn;
        [SerializeField] private Button soundTabBtn;
        [SerializeField] private Button graphicTabBtn;
        [SerializeField] private Button accessoryTabBtn;

        [Header("Category Panels (Optional - Switched when tabs clicked)")]
        [SerializeField] private GameObject generalPanel;
        [SerializeField] private GameObject controlPanel;
        [SerializeField] private GameObject soundPanel;
        [SerializeField] private GameObject graphicPanel;
        [SerializeField] private GameObject accessoryPanel;

        [Header("Audio Sliders & Percentage Texts")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeValueText;

        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private TMP_Text bgmVolumeValueText;

        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text sfxVolumeValueText;

        [Header("Display Settings")]
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Action Buttons")]
        [SerializeField] private Button defaultButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button saveAndBackButton;

        public enum SettingTab { General, Control, Sound, Graphic, Accessory }
        private SettingTab _currentTab = SettingTab.General;

        // Current runtime values
        private float _masterVolume = DefaultMasterVolume;
        private float _bgmVolume = DefaultBgmVolume;
        private float _sfxVolume = DefaultSfxVolume;
        private bool _isFullscreen = DefaultFullscreen;

        // Public Accessors
        public float MasterVolume => _masterVolume;
        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;
        public bool IsFullscreen => _isFullscreen;
        public SettingTab CurrentTab => _currentTab;

        // Global Event Callbacks for audio/display subsystems
        public static event Action<float> OnMasterVolumeChanged;
        public static event Action<float> OnBgmVolumeChanged;
        public static event Action<float> OnSfxVolumeChanged;
        public static event Action<bool> OnFullscreenChanged;

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
            AutoWireUIElements();
            BindUIEvents();
            ApplySettings();
        }

        private void OnEnable()
        {
            RefreshUI();
        }

        private void ResolveModalRoot()
        {
            if (modalRoot == null || modalRoot == gameObject || modalRoot.name == "Setting")
            {
                // ตรวจสอบว่ามี Parent หรือ Child ชื่อ SettingPanel หรือไม่ เพื่อให้เปิด/ปิดได้ทั้งกรอบ
                Transform panel = transform.Find("SettingPanel");
                if (panel == null && transform.parent != null)
                {
                    if (transform.parent.name.Contains("Setting"))
                    {
                        panel = transform.parent;
                    }
                    else
                    {
                        panel = transform.parent.Find("SettingPanel");
                    }
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
        /// Loads settings from PlayerPrefs with fallback to legacy keys and system defaults.
        /// </summary>
        public void LoadSettings()
        {
            // 1. Master Volume
            if (PlayerPrefs.HasKey(PrefMasterVolume))
                _masterVolume = PlayerPrefs.GetFloat(PrefMasterVolume);
            else if (PlayerPrefs.HasKey(LegacyMasterVolume))
                _masterVolume = PlayerPrefs.GetFloat(LegacyMasterVolume);
            else
                _masterVolume = DefaultMasterVolume;

            // 2. BGM Volume
            if (PlayerPrefs.HasKey(PrefBgmVolume))
                _bgmVolume = PlayerPrefs.GetFloat(PrefBgmVolume);
            else if (PlayerPrefs.HasKey(LegacyBgmVolume))
                _bgmVolume = PlayerPrefs.GetFloat(LegacyBgmVolume);
            else
                _bgmVolume = DefaultBgmVolume;

            // 3. SFX Volume
            if (PlayerPrefs.HasKey(PrefSfxVolume))
                _sfxVolume = PlayerPrefs.GetFloat(PrefSfxVolume);
            else if (PlayerPrefs.HasKey(LegacySfxVolume))
                _sfxVolume = PlayerPrefs.GetFloat(LegacySfxVolume);
            else
                _sfxVolume = DefaultSfxVolume;

            // 4. Fullscreen
            if (PlayerPrefs.HasKey(PrefFullscreen))
                _isFullscreen = PlayerPrefs.GetInt(PrefFullscreen) == 1;
            else if (PlayerPrefs.HasKey(LegacyFullscreen))
                _isFullscreen = PlayerPrefs.GetInt(LegacyFullscreen) == 1;
            else
                _isFullscreen = Screen.fullScreen;

            // Clamp values
            _masterVolume = Mathf.Clamp01(_masterVolume);
            _bgmVolume = Mathf.Clamp01(_bgmVolume);
            _sfxVolume = Mathf.Clamp01(_sfxVolume);
        }

        /// <summary>
        /// Saves current settings to PlayerPrefs and flushes to disk.
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(PrefMasterVolume, _masterVolume);
            PlayerPrefs.SetFloat(PrefBgmVolume, _bgmVolume);
            PlayerPrefs.SetFloat(PrefSfxVolume, _sfxVolume);
            PlayerPrefs.SetInt(PrefFullscreen, _isFullscreen ? 1 : 0);

            // Also sync legacy keys for backwards-compatibility
            PlayerPrefs.SetFloat(LegacyMasterVolume, _masterVolume);
            PlayerPrefs.SetFloat(LegacyBgmVolume, _bgmVolume);
            PlayerPrefs.SetFloat(LegacySfxVolume, _sfxVolume);
            PlayerPrefs.SetInt(LegacyFullscreen, _isFullscreen ? 1 : 0);

            PlayerPrefs.Save();
            Debug.Log($"[SettingManager] Settings saved: Master={_masterVolume * 100:F0}%, BGM={_bgmVolume * 100:F0}%, SFX={_sfxVolume * 100:F0}%, Fullscreen={_isFullscreen}");
        }

        /// <summary>
        /// Applies current volume and display settings to the Unity engine.
        /// </summary>
        public void ApplySettings()
        {
            AudioListener.volume = _masterVolume;

            if (Screen.fullScreen != _isFullscreen)
            {
                Screen.fullScreen = _isFullscreen;
            }

            OnMasterVolumeChanged?.Invoke(_masterVolume);
            OnBgmVolumeChanged?.Invoke(_bgmVolume);
            OnSfxVolumeChanged?.Invoke(_sfxVolume);
            OnFullscreenChanged?.Invoke(_isFullscreen);
        }

        /// <summary>
        /// Automatically wires up UI sliders, percentage text labels, and buttons if not assigned via Inspector.
        /// </summary>
        private void AutoWireUIElements()
        {
            Transform searchRoot = modalRoot != null ? modalRoot.transform : transform;

            // 1. Sliders & Percentage Texts
            Slider[] sliders = searchRoot.GetComponentsInChildren<Slider>(true);
            foreach (Slider s in sliders)
            {
                string sName = s.gameObject.name.ToLowerInvariant();
                Transform parent = s.transform.parent;
                string pName = parent != null ? parent.name.ToLowerInvariant() : "";

                if (masterVolumeSlider == null && (sName.Contains("master") || pName.Contains("master")))
                {
                    masterVolumeSlider = s;
                    masterVolumeValueText = FindValueTextInRow(s.transform);
                }
                else if (bgmVolumeSlider == null && (sName.Contains("bgm") || sName.Contains("music") || pName.Contains("bgm") || pName.Contains("music")))
                {
                    bgmVolumeSlider = s;
                    bgmVolumeValueText = FindValueTextInRow(s.transform);
                }
                else if (sfxVolumeSlider == null && (sName.Contains("sfx") || sName.Contains("sound") || pName.Contains("sfx") || pName.Contains("sound")))
                {
                    sfxVolumeSlider = s;
                    sfxVolumeValueText = FindValueTextInRow(s.transform);
                }
            }

            if (sliders.Length >= 3)
            {
                if (masterVolumeSlider == null) { masterVolumeSlider = sliders[0]; masterVolumeValueText = FindValueTextInRow(sliders[0].transform); }
                if (bgmVolumeSlider == null) { bgmVolumeSlider = sliders[1]; bgmVolumeValueText = FindValueTextInRow(sliders[1].transform); }
                if (sfxVolumeSlider == null) { sfxVolumeSlider = sliders[2]; sfxVolumeValueText = FindValueTextInRow(sliders[2].transform); }
            }

            // 2. Fullscreen Toggle
            if (fullscreenToggle == null)
            {
                fullscreenToggle = searchRoot.GetComponentInChildren<Toggle>(true);
            }

            // 3. Buttons (Default, Apply, Exit, Save & Back, Category Tabs)
            Button[] buttons = searchRoot.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string bName = btn.gameObject.name.ToLowerInvariant();
                TMP_Text bText = btn.GetComponentInChildren<TMP_Text>(true);
                string textContent = bText != null ? bText.text.ToLowerInvariant() : "";

                // Action Buttons
                if (defaultButton == null && (bName.Contains("default") || textContent.Contains("default")))
                {
                    defaultButton = btn;
                }
                else if (applyButton == null && (bName.Contains("apply") || textContent.Contains("apply")))
                {
                    applyButton = btn;
                }
                else if (exitButton == null && (bName.Contains("exit") || textContent.Contains("exit")))
                {
                    exitButton = btn;
                }
                else if (saveAndBackButton == null && (bName.Contains("save") || bName.Contains("back") || bName.Contains("close") || textContent.Contains("save") || textContent.Contains("back") || textContent.Contains("close")))
                {
                    saveAndBackButton = btn;
                }

                // Category Tab Buttons
                if (generalTabBtn == null && (bName == "general" || textContent.Contains("general")))
                    generalTabBtn = btn;
                else if (controlTabBtn == null && (bName == "control" || textContent.Contains("control")))
                    controlTabBtn = btn;
                else if (soundTabBtn == null && (bName == "sound" || textContent.Contains("sound")))
                    soundTabBtn = btn;
                else if (graphicTabBtn == null && (bName == "graphic" || textContent.Contains("graphic")))
                    graphicTabBtn = btn;
                else if (accessoryTabBtn == null && (bName.Contains("acces") || textContent.Contains("acces")))
                    accessoryTabBtn = btn;
            }
        }

        private TMP_Text FindValueTextInRow(Transform sliderTransform)
        {
            if (sliderTransform == null || sliderTransform.parent == null) return null;
            TMP_Text[] texts = sliderTransform.parent.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text t in texts)
            {
                if (t.gameObject.name.ToLowerInvariant().Contains("value") || t.text.Contains("%"))
                {
                    return t;
                }
            }
            return null;
        }

        private void BindUIEvents()
        {
            // Master Slider
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
                masterVolumeSlider.onValueChanged.AddListener(OnMasterSliderChanged);
            }

            // BGM Slider
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
                bgmVolumeSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }

            // SFX Slider
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }

            // Fullscreen Toggle
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenToggleChanged);
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
            }

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
                exitButton.onClick.RemoveListener(CloseSettingsModal);
                exitButton.onClick.AddListener(CloseSettingsModal);
            }

            if (saveAndBackButton != null)
            {
                saveAndBackButton.onClick.RemoveListener(OnSaveAndBack);
                saveAndBackButton.onClick.AddListener(OnSaveAndBack);
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

            masterVolumeSlider = other.masterVolumeSlider;
            masterVolumeValueText = other.masterVolumeValueText;
            bgmVolumeSlider = other.bgmVolumeSlider;
            bgmVolumeValueText = other.bgmVolumeValueText;
            sfxVolumeSlider = other.sfxVolumeSlider;
            sfxVolumeValueText = other.sfxVolumeValueText;
            fullscreenToggle = other.fullscreenToggle;
            defaultButton = other.defaultButton;
            applyButton = other.applyButton;
            exitButton = other.exitButton;
            saveAndBackButton = other.saveAndBackButton;

            ResolveModalRoot();
            AutoWireUIElements();
            BindUIEvents();
            RefreshUI();
        }

        /// <summary>
        /// Updates all UI sliders, value labels, and toggles with current settings.
        /// </summary>
        public void RefreshUI()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(_masterVolume);
            }
            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.SetValueWithoutNotify(_bgmVolume);
            }
            if (bgmVolumeValueText != null)
            {
                bgmVolumeValueText.text = $"{Mathf.RoundToInt(_bgmVolume * 100)}%";
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(_sfxVolume);
            }
            if (sfxVolumeValueText != null)
            {
                sfxVolumeValueText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}%";
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(_isFullscreen);
            }
        }

        // =========================================================================
        //  Category Tabs
        // =========================================================================

        public void SwitchCategoryTab(SettingTab tab)
        {
            _currentTab = tab;

            if (generalPanel != null) generalPanel.SetActive(tab == SettingTab.General);
            if (controlPanel != null) controlPanel.SetActive(tab == SettingTab.Control);
            if (soundPanel != null) soundPanel.SetActive(tab == SettingTab.Sound);
            if (graphicPanel != null) graphicPanel.SetActive(tab == SettingTab.Graphic);
            if (accessoryPanel != null) accessoryPanel.SetActive(tab == SettingTab.Accessory);

            Debug.Log($"[SettingManager] Switched to tab: {tab}");
        }

        // =========================================================================
        //  UI Event Handlers
        // =========================================================================

        public void OnMasterSliderChanged(float val)
        {
            _masterVolume = Mathf.Clamp01(val);
            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
            }
            AudioListener.volume = _masterVolume;
            OnMasterVolumeChanged?.Invoke(_masterVolume);
        }

        public void OnBgmSliderChanged(float val)
        {
            _bgmVolume = Mathf.Clamp01(val);
            if (bgmVolumeValueText != null)
            {
                bgmVolumeValueText.text = $"{Mathf.RoundToInt(_bgmVolume * 100)}%";
            }
            OnBgmVolumeChanged?.Invoke(_bgmVolume);
        }

        public void OnSfxSliderChanged(float val)
        {
            _sfxVolume = Mathf.Clamp01(val);
            if (sfxVolumeValueText != null)
            {
                sfxVolumeValueText.text = $"{Mathf.RoundToInt(_sfxVolume * 100)}%";
            }
            OnSfxVolumeChanged?.Invoke(_sfxVolume);
        }

        public void OnFullscreenToggleChanged(bool isFs)
        {
            _isFullscreen = isFs;
            Screen.fullScreen = _isFullscreen;
            OnFullscreenChanged?.Invoke(_isFullscreen);
        }

        /// <summary>
        /// Resets all settings to their default values (Master: 100%, BGM: 85%, SFX: 100%, Fullscreen: On).
        /// </summary>
        public void OnResetDefault()
        {
            _masterVolume = DefaultMasterVolume;
            _bgmVolume = DefaultBgmVolume;
            _sfxVolume = DefaultSfxVolume;
            _isFullscreen = DefaultFullscreen;

            RefreshUI();
            ApplySettings();
            Debug.Log("[SettingManager] Settings reset to default values.");
        }

        /// <summary>
        /// Saves current settings to disk and applies them to engine.
        /// </summary>
        public void OnApplySettings()
        {
            SaveSettings();
            ApplySettings();
            Debug.Log("[SettingManager] Settings applied successfully.");
        }

        /// <summary>
        /// Saves current settings to disk and closes the modal.
        /// </summary>
        public void OnSaveAndBack()
        {
            SaveSettings();
            ApplySettings();
            CloseSettingsModal();
        }

        /// <summary>
        /// Opens the Settings Modal and refreshes all UI elements with current saved values.
        /// </summary>
        public void OpenSettingsModal()
        {
            ResolveModalRoot();
            LoadSettings();
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
            RefreshUI();
        }

        /// <summary>
        /// Closes the Settings Modal.
        /// </summary>
        public void CloseSettingsModal()
        {
            ResolveModalRoot();
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Alias class supporting plural naming convention (SettingsManager).
    /// </summary>
    public class SettingsManager : SettingManager
    {
    }
}
