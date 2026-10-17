using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace TheLastEmpire
{
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance { get; private set; }

        /// <summary>
        /// Flag checked by WorldMapManager upon loading the gameplay scene to decide whether to restore saved progression.
        /// </summary>
        public static bool ShouldLoadSave { get; set; } = false;

        [Header("Scene Transition Settings")]
        [SerializeField] private string gameplaySceneName = "Gameplay_3D";
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Theme Color Palette (Arcane / Post-Apocalyptic Vibrant)")]
        [SerializeField] private Color primaryAccent = new Color(0.66f, 0.33f, 0.97f, 1f);    // Vibrant Purple (#A855F7)
        [SerializeField] private Color secondaryAccent = new Color(0.75f, 0.52f, 0.99f, 1f);  // Lilac Flourish (#C084FC)
        [SerializeField] private Color panelBgColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);  // Dark Obsidian Sidebar (#15171C)
        [SerializeField] private Color buttonBgColor = new Color(0.14f, 0.16f, 0.19f, 0.95f); // Dark Slate Button (#242930)

        [Header("Serialized References (Optional - Built procedurally if null)")]
        [SerializeField] private Canvas menuCanvas;
        [SerializeField] private CanvasGroup menuCanvasGroup;
        [SerializeField] private Button newGameBtn;
        [SerializeField] private Button continueBtn;
        [SerializeField] private Button controlsBtn;
        [SerializeField] private Button settingsBtn;
        [SerializeField] private Button quitBtn;
        [SerializeField] private GameObject controlsModalObj;
        [SerializeField] private GameObject settingsModalObj;
        [SerializeField] private GameObject characterSelectModalObj;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button closeControlsBtn;
        [SerializeField] private Button closeSettingsBtn;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private GameObject fadeOverlayObj;
        [SerializeField] private CanvasGroup fadeOverlayGroup;

        private GameObject _fadeOverlayObj;
        private CanvasGroup _fadeOverlayGroup;
        private bool _isTransitioning = false;
        private TMP_FontAsset _customFont;

        public void SetHierarchyReferences(
            Canvas canvas, CanvasGroup canvasGroup,
            Button newGame, Button cont, Button controls, Button settings, Button quit,
            GameObject controlsModal, GameObject settingsModal,
            TMP_Text title, Button closeControls, Button closeSettings,
            Slider masterSlider, Slider bgmSlider, Slider sfxSlider, Toggle fsToggle,
            GameObject fadeObj, CanvasGroup fadeGrp,
            GameObject charModal = null)
        {
            menuCanvas = canvas;
            menuCanvasGroup = canvasGroup;
            newGameBtn = newGame;
            continueBtn = cont;
            controlsBtn = controls;
            settingsBtn = settings;
            quitBtn = quit;
            controlsModalObj = controlsModal;
            settingsModalObj = settingsModal;
            characterSelectModalObj = charModal;
            titleLabel = title;
            closeControlsBtn = closeControls;
            closeSettingsBtn = closeSettings;
            masterVolumeSlider = masterSlider;
            bgmVolumeSlider = bgmSlider;
            sfxVolumeSlider = sfxSlider;
            fullscreenToggle = fsToggle;
            fadeOverlayObj = fadeObj;
            fadeOverlayGroup = fadeGrp;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Clean up any lingering singletons from previous gameplay sessions to prevent stale state
            PurgeLingeringGameSingletons();

            // Locate Teko font if available in project assets
            LocateCustomFont();

            // Build complete UI hierarchy if serialized references aren't assigned in inspector
            if (menuCanvas == null)
            {
                CreateProceduralUI();
            }
            else
            {
                WireExistingReferences();
            }

            // Ensure background ember particles exist for atmosphere
            EnsureAtmosphericBackground();
        }

        private void Start()
        {
            Time.timeScale = 1f;

            // Check save file availability to enable or disable Continue button
            UpdateContinueButtonState();

            // Fade in smoothly from black
            StartCoroutine(FadeInRoutine());
        }

        private void Update()
        {
            // Subtle breathing pulse animation on game title
            if (titleLabel != null)
            {
                float scale = 1.0f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.025f;
                titleLabel.transform.localScale = new Vector3(scale, scale, 1.0f);
            }
        }

        // =========================================================================
        //  Cleanup Lingering Singletons
        // =========================================================================
        private void PurgeLingeringGameSingletons()
        {
            if (GameManager.Instance != null)
            {
                Destroy(GameManager.Instance.gameObject);
            }
            if (WorldMapManager.Instance != null)
            {
                Destroy(WorldMapManager.Instance.gameObject);
            }
            if (DayNightManager.Instance != null)
            {
                Destroy(DayNightManager.Instance.gameObject);
            }
            if (InventoryUI.Instance != null)
            {
                Destroy(InventoryUI.Instance.gameObject);
            }
        }

        private void LocateCustomFont()
        {
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts)
            {
                if (f != null && f.name.Contains("Teko"))
                {
                    _customFont = f;
                    break;
                }
            }
        }

        private void ApplyCustomFont(TMP_Text text)
        {
            if (text != null && _customFont != null)
            {
                text.font = _customFont;
            }
        }

        // =========================================================================
        //  UI Actions
        // =========================================================================
        public void OnClickNewGame()
        {
            if (_isTransitioning) return;
            if (characterSelectModalObj != null)
            {
                characterSelectModalObj.SetActive(true);
            }
            else
            {
                ShouldLoadSave = false;
                StartCoroutine(TransitionRoutine(gameplaySceneName));
            }
        }

        public void OnCloseCharacterSelect()
        {
            if (characterSelectModalObj != null)
            {
                characterSelectModalObj.SetActive(false);
            }
        }

        public void StartGameWithCharacter(string characterId)
        {
            if (_isTransitioning) return;
            CharacterManager.SelectedCharacterId = characterId;
            ShouldLoadSave = false;
            StartCoroutine(TransitionRoutine(gameplaySceneName));
        }

        public void OnClickContinue()
        {
            if (_isTransitioning) return;
            if (!SaveSystem.HasSaveFile()) return;

            ShouldLoadSave = true;
            StartCoroutine(TransitionRoutine(gameplaySceneName));
        }

        public void OnClickControls()
        {
            if (controlsModalObj != null)
            {
                controlsModalObj.SetActive(true);
            }
        }

        public void OnCloseControls()
        {
            if (controlsModalObj != null)
            {
                controlsModalObj.SetActive(false);
            }
        }

        public void OnClickSettings()
        {
            if (settingsModalObj != null)
            {
                settingsModalObj.SetActive(true);
            }
        }

        public void OnCloseSettings()
        {
            if (settingsModalObj != null)
            {
                settingsModalObj.SetActive(false);
            }
        }

        public void OnClickQuit()
        {
            if (_isTransitioning) return;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =========================================================================
        //  Save File & Continue State Check
        // =========================================================================
        private void UpdateContinueButtonState()
        {
            bool hasSave = SaveSystem.HasSaveFile();

            if (continueBtn != null)
            {
                continueBtn.interactable = hasSave;

                CanvasGroup cg = continueBtn.GetComponent<CanvasGroup>();
                if (cg == null) cg = continueBtn.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = hasSave ? 1.0f : 0.45f;

                TMP_Text btnText = continueBtn.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = hasSave ? "᚛  CONTINUE  ᚜" : "᚛  CONTINUE (NO SAVE)  ᚜";
                    btnText.color = hasSave ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);
                }
            }
        }

        // =========================================================================
        //  Scene Transitions (Fade In & Fade Out)
        // =========================================================================
        private IEnumerator FadeInRoutine()
        {
            if (_fadeOverlayGroup != null)
            {
                _fadeOverlayGroup.alpha = 1.0f;
                _fadeOverlayGroup.blocksRaycasts = false;

                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _fadeOverlayGroup.alpha = Mathf.Lerp(1.0f, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                _fadeOverlayGroup.alpha = 0f;
                _fadeOverlayObj.SetActive(false);
            }
        }

        private IEnumerator TransitionRoutine(string sceneName)
        {
            _isTransitioning = true;

            if (_fadeOverlayObj != null && _fadeOverlayGroup != null)
            {
                _fadeOverlayObj.SetActive(true);
                _fadeOverlayGroup.blocksRaycasts = true;

                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _fadeOverlayGroup.alpha = Mathf.Lerp(0f, 1.0f, elapsed / fadeDuration);
                    yield return null;
                }
                _fadeOverlayGroup.alpha = 1.0f;
            }

            SceneManager.LoadScene(sceneName);
        }

        // =========================================================================
        //  Wire Serialized Inspector References
        // =========================================================================
        private void WireExistingReferences()
        {
            if (newGameBtn != null)
            {
                newGameBtn.onClick.RemoveAllListeners();
                newGameBtn.onClick.AddListener(OnClickNewGame);
            }
            if (continueBtn != null)
            {
                continueBtn.onClick.RemoveAllListeners();
                continueBtn.onClick.AddListener(OnClickContinue);
            }
            if (controlsBtn != null)
            {
                controlsBtn.onClick.RemoveAllListeners();
                controlsBtn.onClick.AddListener(OnClickControls);
            }
            if (settingsBtn != null)
            {
                settingsBtn.onClick.RemoveAllListeners();
                settingsBtn.onClick.AddListener(OnClickSettings);
            }
            if (quitBtn != null)
            {
                quitBtn.onClick.RemoveAllListeners();
                quitBtn.onClick.AddListener(OnClickQuit);
            }

            if (closeControlsBtn != null)
            {
                closeControlsBtn.onClick.RemoveAllListeners();
                closeControlsBtn.onClick.AddListener(OnCloseControls);
            }
            if (closeSettingsBtn != null)
            {
                closeSettingsBtn.onClick.RemoveAllListeners();
                closeSettingsBtn.onClick.AddListener(OnCloseSettings);
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
                masterVolumeSlider.onValueChanged.RemoveAllListeners();
                masterVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    AudioListener.volume = val;
                    PlayerPrefs.SetFloat("MasterVolume", val);
                });
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.85f);
                bgmVolumeSlider.onValueChanged.RemoveAllListeners();
                bgmVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    PlayerPrefs.SetFloat("BGMVolume", val);
                });
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                sfxVolumeSlider.onValueChanged.AddListener((val) =>
                {
                    PlayerPrefs.SetFloat("SFXVolume", val);
                });
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.onValueChanged.AddListener((isFs) =>
                {
                    Screen.fullScreen = isFs;
                    PlayerPrefs.SetInt("Fullscreen", isFs ? 1 : 0);
                });
            }

            if (fadeOverlayObj != null)
            {
                _fadeOverlayObj = fadeOverlayObj;
                _fadeOverlayGroup = fadeOverlayGroup ?? fadeOverlayObj.GetComponent<CanvasGroup>();
            }

            if (characterSelectModalObj == null && menuCanvas != null)
            {
                characterSelectModalObj = CreateCharacterSelectModal(menuCanvas.transform);
                characterSelectModalObj.SetActive(false);
            }

            AttachHoverEffect(newGameBtn);
            AttachHoverEffect(continueBtn);
            AttachHoverEffect(controlsBtn);
            AttachHoverEffect(settingsBtn);
            AttachHoverEffect(quitBtn);
            AttachHoverEffect(closeControlsBtn);
            AttachHoverEffect(closeSettingsBtn);
        }

        // =========================================================================
        //  Atmospheric Background Visuals (Particle System Embers)
        // =========================================================================
        private void EnsureAtmosphericBackground()
        {
            // If already present in scene, ensure velocityOverLifetime is safely disabled to prevent any mode mismatch errors
            ParticleSystem existingPs = FindFirstObjectByType<ParticleSystem>();
            if (existingPs != null)
            {
                var v = existingPs.velocityOverLifetime;
                v.enabled = false;
                return;
            }

            GameObject emberObj = new GameObject("BackgroundEmbersVFX");
            emberObj.transform.position = new Vector3(0f, -5f, 5f);
            emberObj.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = emberObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.45f, 0.05f, 0.8f),
                new Color(1f, 0.85f, 0.2f, 0.9f)
            );
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 12f, 1f);
            shape.randomDirectionAmount = 0.2f;

            // Explicitly disable velocityOverLifetime to prevent any mode mismatch issues across Unity versions
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = false;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            ParticleSystemRenderer psRenderer = emberObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"));
        }

        // =========================================================================
        //  Procedural UI Generation (Left Sidebar & Scenic Background - Arcane Layout)
        // =========================================================================
        private void CreateProceduralUI()
        {
            // 1. Root Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.layer = 5; // UI
            menuCanvas = canvasObj.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menuCanvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            menuCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // 2. Fullscreen Scenic Background (Right & Center World Artwork)
            GameObject bgObj = new GameObject("ScenicBackground");
            bgObj.layer = 5;
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.white;

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Load scenic artwork from Assets/Textures/MainMenu_Scenic_BG.jpg if available
            string bgPath = Path.Combine(Application.dataPath, "Textures/MainMenu_Scenic_BG.jpg");
            if (File.Exists(bgPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(bgPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(data))
                    {
                        bgImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
                catch
                {
                    bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
                }
            }
            else
            {
                bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            }

            // Right-Side Vignette Shadow for cinematic depth
            GameObject vignetteObj = new GameObject("VignetteOverlay");
            vignetteObj.layer = 5;
            vignetteObj.transform.SetParent(bgObj.transform, false);
            Image vignetteImg = vignetteObj.AddComponent<Image>();
            vignetteImg.color = new Color(0f, 0f, 0f, 0.20f);
            RectTransform vRect = vignetteObj.GetComponent<RectTransform>();
            vRect.anchorMin = Vector2.zero;
            vRect.anchorMax = Vector2.one;
            vRect.offsetMin = Vector2.zero;
            vRect.offsetMax = Vector2.zero;

            // 3. Left Sidebar Panel (Matches Reference Layout!)
            GameObject sidebarObj = new GameObject("LeftSidebarPanel");
            sidebarObj.layer = 5;
            sidebarObj.transform.SetParent(canvasObj.transform, false);
            Image sidebarImg = sidebarObj.AddComponent<Image>();
            sidebarImg.color = panelBgColor;

            RectTransform sidebarRect = sidebarObj.GetComponent<RectTransform>();
            sidebarRect.anchorMin = new Vector2(0f, 0f);
            sidebarRect.anchorMax = new Vector2(0f, 1f);
            sidebarRect.pivot = new Vector2(0f, 0.5f);
            sidebarRect.anchoredPosition = Vector2.zero;
            sidebarRect.sizeDelta = new Vector2(470f, 0f);

            // Right Edge Divider on Sidebar
            GameObject dividerObj = new GameObject("SidebarRightBorder");
            dividerObj.layer = 5;
            dividerObj.transform.SetParent(sidebarObj.transform, false);
            Image divImg = dividerObj.AddComponent<Image>();
            divImg.color = new Color(0.18f, 0.20f, 0.25f, 0.85f);
            RectTransform divRect = dividerObj.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(1f, 0f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(1f, 0.5f);
            divRect.anchoredPosition = Vector2.zero;
            divRect.sizeDelta = new Vector2(3f, 0f);

            // 4. Logo / Header Container (Top of Sidebar)
            GameObject logoContainer = new GameObject("LogoContainer");
            logoContainer.layer = 5;
            logoContainer.transform.SetParent(sidebarObj.transform, false);
            RectTransform logoRect = logoContainer.AddComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 1f);
            logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 1f);
            logoRect.anchoredPosition = new Vector2(0f, -60f);
            logoRect.sizeDelta = new Vector2(410f, 190f);

            // Top Flourish
            GameObject topFlourishObj = new GameObject("TopFlourishText");
            topFlourishObj.layer = 5;
            topFlourishObj.transform.SetParent(logoContainer.transform, false);
            TMP_Text topFlourish = topFlourishObj.AddComponent<TextMeshProUGUI>();
            topFlourish.text = "—   T H E   L A S T   —";
            topFlourish.fontSize = 20;
            topFlourish.fontStyle = FontStyles.Bold;
            topFlourish.color = secondaryAccent;
            topFlourish.characterSpacing = 8;
            topFlourish.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(topFlourish);
            RectTransform tfRect = topFlourishObj.GetComponent<RectTransform>();
            tfRect.anchorMin = new Vector2(0f, 1f);
            tfRect.anchorMax = new Vector2(1f, 1f);
            tfRect.anchoredPosition = new Vector2(0f, -10f);
            tfRect.sizeDelta = new Vector2(410f, 32f);

            // Main Title "EMPIRE"
            GameObject mainTitleObj = new GameObject("MainTitleText");
            mainTitleObj.layer = 5;
            mainTitleObj.transform.SetParent(logoContainer.transform, false);
            titleLabel = mainTitleObj.AddComponent<TextMeshProUGUI>();
            titleLabel.text = "EMPIRE";
            titleLabel.fontSize = 78;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.color = Color.white;
            titleLabel.characterSpacing = 4;
            titleLabel.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(titleLabel);
            RectTransform mtRect = mainTitleObj.GetComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(0f, 1f);
            mtRect.anchorMax = new Vector2(1f, 1f);
            mtRect.anchoredPosition = new Vector2(0f, -50f);
            mtRect.sizeDelta = new Vector2(410f, 85f);

            // Crest Rune Flourish
            GameObject crestObj = new GameObject("CrestFlourish");
            crestObj.layer = 5;
            crestObj.transform.SetParent(logoContainer.transform, false);
            TMP_Text crestText = crestObj.AddComponent<TextMeshProUGUI>();
            crestText.text = "◈ ─────── ❖ ─────── ◈";
            crestText.fontSize = 16;
            crestText.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.85f);
            crestText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(crestText);
            RectTransform cfRect = crestObj.GetComponent<RectTransform>();
            cfRect.anchorMin = new Vector2(0f, 1f);
            cfRect.anchorMax = new Vector2(1f, 1f);
            cfRect.anchoredPosition = new Vector2(0f, -135f);
            cfRect.sizeDelta = new Vector2(410f, 25f);

            // Subtitle
            GameObject subTitleObj = new GameObject("SubtitleText");
            subTitleObj.layer = 5;
            subTitleObj.transform.SetParent(logoContainer.transform, false);
            TMP_Text subText = subTitleObj.AddComponent<TextMeshProUGUI>();
            subText.text = "SURVIVAL ROGUELIKE // PROTOCOL ACTIVE";
            subText.fontSize = 12;
            subText.color = new Color(0.6f, 0.65f, 0.72f, 0.85f);
            subText.characterSpacing = 4;
            subText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(subText);
            RectTransform stRect = subTitleObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 1f);
            stRect.anchorMax = new Vector2(1f, 1f);
            stRect.anchoredPosition = new Vector2(0f, -162f);
            stRect.sizeDelta = new Vector2(410f, 22f);

            // 5. Menu Buttons Container (Middle of Sidebar)
            GameObject buttonsContainer = new GameObject("MenuButtonsList");
            buttonsContainer.layer = 5;
            buttonsContainer.transform.SetParent(sidebarObj.transform, false);
            RectTransform btnListRect = buttonsContainer.AddComponent<RectTransform>();
            btnListRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnListRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnListRect.pivot = new Vector2(0.5f, 0.5f);
            btnListRect.anchoredPosition = new Vector2(0f, -30f);
            btnListRect.sizeDelta = new Vector2(370f, 340f);

            VerticalLayoutGroup vLayout = buttonsContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 14f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Highlighted Primary Button: NEW GAME (Vibrant purple matching reference!)
            newGameBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_NEW GAME", "⊶  NEW GAME  ⊷", primaryAccent, true, OnClickNewGame);
            continueBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_CONTINUE", "᚛  CONTINUE  ᚜", buttonBgColor, false, OnClickContinue);
            controlsBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_HOW TO PLAY", "❖  HOW TO PLAY  ❖", buttonBgColor, false, OnClickControls);
            settingsBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_SETTINGS", "⟐  OPTIONS  ⟐", buttonBgColor, false, OnClickSettings);
            quitBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_QUIT GAME", "⊶  EXIT  ⊷", buttonBgColor, false, OnClickQuit);

            // 6. Bottom Social & Community Bar (Bottom of Sidebar)
            GameObject socialBar = new GameObject("BottomSocialBar");
            socialBar.layer = 5;
            socialBar.transform.SetParent(sidebarObj.transform, false);
            RectTransform sBarRect = socialBar.AddComponent<RectTransform>();
            sBarRect.anchorMin = new Vector2(0.5f, 0f);
            sBarRect.anchorMax = new Vector2(0.5f, 0f);
            sBarRect.pivot = new Vector2(0.5f, 0f);
            sBarRect.anchoredPosition = new Vector2(0f, 30f);
            sBarRect.sizeDelta = new Vector2(370f, 44f);

            HorizontalLayoutGroup hLayout = socialBar.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;

            CreateSocialButton(socialBar.transform, "Btn_Discord", "💬", 46f);
            CreateSocialButton(socialBar.transform, "Btn_Wishlist", "↗ WISHLIST ON STEAM", 230f);
            CreateSocialButton(socialBar.transform, "Btn_Community", "𝕏", 46f);

            // Version text beneath social bar
            GameObject verObj = new GameObject("VersionText");
            verObj.layer = 5;
            verObj.transform.SetParent(sidebarObj.transform, false);
            TMP_Text vText = verObj.AddComponent<TextMeshProUGUI>();
            vText.text = "THE LAST EMPIRE // v1.0.4 PROTOCOL";
            vText.fontSize = 11;
            vText.color = new Color(0.45f, 0.48f, 0.55f, 0.75f);
            vText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(vText);
            RectTransform verRect = verObj.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.5f, 0f);
            verRect.anchorMax = new Vector2(0.5f, 0f);
            verRect.pivot = new Vector2(0.5f, 0f);
            verRect.anchoredPosition = new Vector2(0f, 10f);
            verRect.sizeDelta = new Vector2(370f, 16f);

            // 7. Modals
            controlsModalObj = CreateControlsModal(canvasObj.transform);
            controlsModalObj.SetActive(false);

            settingsModalObj = CreateSettingsModal(canvasObj.transform);
            settingsModalObj.SetActive(false);

            characterSelectModalObj = CreateCharacterSelectModal(canvasObj.transform);
            characterSelectModalObj.SetActive(false);

            // 8. Fullscreen Fade Curtain
            _fadeOverlayObj = new GameObject("SceneFadeCurtain");
            _fadeOverlayObj.layer = 5;
            _fadeOverlayObj.transform.SetParent(canvasObj.transform, false);
            Image fadeImg = _fadeOverlayObj.AddComponent<Image>();
            fadeImg.color = Color.black;
            _fadeOverlayGroup = _fadeOverlayObj.AddComponent<CanvasGroup>();
            _fadeOverlayGroup.alpha = 0f;
            _fadeOverlayGroup.blocksRaycasts = false;
            RectTransform fadeRect = _fadeOverlayObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            _fadeOverlayObj.transform.SetAsLastSibling();
        }

        private Button CreateSidebarButton(Transform parent, string goName, string label, Color bgColor, bool isPrimary, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject(goName);
            btnObj.layer = 5;
            btnObj.transform.SetParent(parent, false);

            LayoutElement layout = btnObj.AddComponent<LayoutElement>();
            float btnHeight = isPrimary ? 56f : 48f;
            layout.preferredHeight = btnHeight;
            layout.minHeight = btnHeight;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = bgColor;

            // Border Line
            GameObject borderObj = new GameObject("Border");
            borderObj.layer = 5;
            borderObj.transform.SetParent(btnObj.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = isPrimary ? new Color(1f, 1f, 1f, 0.45f) : new Color(0.24f, 0.28f, 0.34f, 0.60f);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-1.5f, -1.5f);
            borderRect.offsetMax = new Vector2(1.5f, 1.5f);
            borderObj.transform.SetAsFirstSibling();

            // Text Label
            GameObject textObj = new GameObject("Label");
            textObj.layer = 5;
            textObj.transform.SetParent(btnObj.transform, false);
            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = isPrimary ? 21 : 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.characterSpacing = 2;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(tmp);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            Button btn = btnObj.AddComponent<Button>();
            if (action != null)
            {
                btn.onClick.AddListener(action);
            }

            AttachHoverEffect(btn);
            return btn;
        }

        private void CreateSocialButton(Transform parent, string name, string label, float width)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.layer = 5;
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 40f);

            Image bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.17f, 0.90f);

            GameObject borderObj = new GameObject("Border");
            borderObj.layer = 5;
            borderObj.transform.SetParent(btnObj.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.35f, 0.42f, 0.50f);
            RectTransform bRect = borderObj.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(-1f, -1f);
            bRect.offsetMax = new Vector2(1f, 1f);
            borderObj.transform.SetAsFirstSibling();

            GameObject textObj = new GameObject("Label");
            textObj.layer = 5;
            textObj.transform.SetParent(btnObj.transform, false);
            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.85f, 0.88f, 0.95f, 0.95f);
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(tmp);
            RectTransform tRect = textObj.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            Button btn = btnObj.AddComponent<Button>();
            AttachHoverEffect(btn);
        }

        private void AttachHoverEffect(Button btn)
        {
            if (btn == null) return;
            MenuButtonHoverEffect hover = btn.gameObject.GetComponent<MenuButtonHoverEffect>();
            if (hover == null)
            {
                hover = btn.gameObject.AddComponent<MenuButtonHoverEffect>();
            }
        }

        // =========================================================================
        //  Controls Modal Dialog
        // =========================================================================
        private GameObject CreateControlsModal(Transform canvasTransform)
        {
            GameObject modalRoot = new GameObject("ControlsModal");
            modalRoot.transform.SetParent(canvasTransform, false);

            RectTransform modalRect = modalRoot.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            // Semi-transparent blocker backdrop
            Image backdrop = modalRoot.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.85f);

            // Center Panel
            GameObject panel = new GameObject("ModalPanel");
            panel.transform.SetParent(modalRoot.transform, false);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = panelBgColor;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 620f);

            // Panel Border
            GameObject border = new GameObject("PanelBorder");
            border.transform.SetParent(panel.transform, false);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(secondaryAccent.r, secondaryAccent.g, secondaryAccent.b, 0.45f);
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            border.transform.SetAsFirstSibling();

            // Modal Header Title
            GameObject headerObj = new GameObject("ModalTitle");
            headerObj.transform.SetParent(panel.transform, false);
            TMP_Text headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "SURVIVAL MANUAL // CONTROLS & PROTOCOLS";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = secondaryAccent;
            headerText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(headerText);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -24f);
            hRect.sizeDelta = new Vector2(700f, 40f);

            // Controls Content Box
            GameObject contentObj = new GameObject("ControlsContent");
            contentObj.transform.SetParent(panel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(40f, 90f);
            contentRect.offsetMax = new Vector2(-40f, -75f);

            VerticalLayoutGroup vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 10f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;

            // Add instruction rows
            AddControlRow(contentObj.transform, "W, A, S, D", "Movement // บังคับทิศทางเดิน");
            AddControlRow(contentObj.transform, "Mouse Aim + Left Click", "Aim & Shoot / Attack // เล็งและยิง/โจมตี");
            AddControlRow(contentObj.transform, "1 - 9 / Mouse Scroll", "Quick Select Ranged Weapons // สลับปืน");
            AddControlRow(contentObj.transform, "X Key", "Cycle Melee Weapons // สลับอาวุธประชิด (Katana, Axe, Hammer)");
            AddControlRow(contentObj.transform, "R Key", "Reload Weapon Magazine // บรรจุกระสุน");
            AddControlRow(contentObj.transform, "I / ESC Key", "Inventory & Pause // เปิดกระเป๋าเป้ / หยุดเกม");
            AddControlRow(contentObj.transform, "Screen Edge Portals", "Warp to Adjacent Biomes // เดินเข้าประตูเพื่อข้ามโซน");
            AddControlRow(contentObj.transform, "Day / Night Cycle", "Day = 30s | Night = High Alien & Zombie aggro!");

            // Close Button
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(panel.transform, false);
            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 22f);
            closeRect.sizeDelta = new Vector2(220f, 48f);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.color = buttonBgColor;

            GameObject closeTextObj = new GameObject("Label");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            TMP_Text closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "CLOSE // ปิด";
            closeText.fontSize = 20;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(closeText);
            RectTransform cTextRect = closeTextObj.GetComponent<RectTransform>();
            cTextRect.anchorMin = Vector2.zero;
            cTextRect.anchorMax = Vector2.one;

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(OnCloseControls);
            AttachHoverEffect(closeBtn);

            return modalRoot;
        }

        private void AddControlRow(Transform parent, string keyBadge, string description)
        {
            GameObject rowObj = new GameObject("ControlRow");
            rowObj.transform.SetParent(parent, false);

            LayoutElement layout = rowObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;

            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.04f);

            // Key Badge Container
            GameObject badgeObj = new GameObject("KeyBadge");
            badgeObj.transform.SetParent(rowObj.transform, false);
            Image badgeBg = badgeObj.AddComponent<Image>();
            badgeBg.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.20f);
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(10f, 0f);
            badgeRect.sizeDelta = new Vector2(240f, 32f);

            GameObject badgeTextObj = new GameObject("KeyText");
            badgeTextObj.transform.SetParent(badgeObj.transform, false);
            TMP_Text bText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            bText.text = keyBadge;
            bText.fontSize = 16;
            bText.fontStyle = FontStyles.Bold;
            bText.color = primaryAccent;
            bText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(bText);
            RectTransform bTextRect = badgeTextObj.GetComponent<RectTransform>();
            bTextRect.anchorMin = Vector2.zero;
            bTextRect.anchorMax = Vector2.one;

            // Description text
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(rowObj.transform, false);
            TMP_Text descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = description;
            descText.fontSize = 16;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
            descText.alignment = TextAlignmentOptions.Left;
            ApplyCustomFont(descText);
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.offsetMin = new Vector2(265f, 0f);
            descRect.offsetMax = new Vector2(-10f, 0f);
        }

        // =========================================================================
        //  Settings Modal Dialog
        // =========================================================================
        private GameObject CreateSettingsModal(Transform canvasTransform)
        {
            GameObject modalRoot = new GameObject("SettingsModal");
            modalRoot.transform.SetParent(canvasTransform, false);

            RectTransform modalRect = modalRoot.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            Image backdrop = modalRoot.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.85f);

            GameObject panel = new GameObject("ModalPanel");
            panel.transform.SetParent(modalRoot.transform, false);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = panelBgColor;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680f, 520f);

            GameObject border = new GameObject("PanelBorder");
            border.transform.SetParent(panel.transform, false);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.45f);
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            border.transform.SetAsFirstSibling();

            // Title
            GameObject headerObj = new GameObject("ModalTitle");
            headerObj.transform.SetParent(panel.transform, false);
            TMP_Text headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "SYSTEM CONFIGURATION // SETTINGS";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = primaryAccent;
            headerText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(headerText);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -24f);
            hRect.sizeDelta = new Vector2(600f, 40f);

            // Container for Settings Rows
            GameObject contentObj = new GameObject("SettingsContent");
            contentObj.transform.SetParent(panel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(40f, 90f);
            contentRect.offsetMax = new Vector2(-40f, -75f);

            VerticalLayoutGroup vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 18f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;

            // Volume Slider Rows
            float savedMasterVol = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            CreateVolumeSliderRow(contentObj.transform, "Master Audio Volume // ระดับเสียงหลัก", savedMasterVol, (val) =>
            {
                AudioListener.volume = val;
                PlayerPrefs.SetFloat("MasterVolume", val);
            });

            float savedBgmVol = PlayerPrefs.GetFloat("BGMVolume", 0.85f);
            CreateVolumeSliderRow(contentObj.transform, "BGM Music Volume // เสียงดนตรีประกอบ", savedBgmVol, (val) =>
            {
                PlayerPrefs.SetFloat("BGMVolume", val);
            });

            float savedSfxVol = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            CreateVolumeSliderRow(contentObj.transform, "SFX Sound Volume // เสียงเอฟเฟกต์อาวุธ", savedSfxVol, (val) =>
            {
                PlayerPrefs.SetFloat("SFXVolume", val);
            });

            // Fullscreen Toggle Row
            CreateToggleRow(contentObj.transform, "Fullscreen Mode // โหมดเต็มจอ", Screen.fullScreen, (isFs) =>
            {
                Screen.fullScreen = isFs;
                PlayerPrefs.SetInt("Fullscreen", isFs ? 1 : 0);
            });

            // Close / Save Button
            GameObject closeBtnObj = new GameObject("SaveAndBackBtn");
            closeBtnObj.transform.SetParent(panel.transform, false);
            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 22f);
            closeRect.sizeDelta = new Vector2(240f, 48f);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.color = buttonBgColor;

            GameObject closeTextObj = new GameObject("Label");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            TMP_Text closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "SAVE & BACK // บันทึกและย้อนกลับ";
            closeText.fontSize = 18;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(closeText);
            RectTransform cTextRect = closeTextObj.GetComponent<RectTransform>();
            cTextRect.anchorMin = Vector2.zero;
            cTextRect.anchorMax = Vector2.one;

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(OnCloseSettings);
            AttachHoverEffect(closeBtn);

            return modalRoot;
        }

        private void CreateVolumeSliderRow(Transform parent, string title, float initialValue, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            GameObject row = new GameObject("SliderRow");
            row.transform.SetParent(parent, false);

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 56f;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.03f);

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            TMP_Text lbl = labelObj.AddComponent<TextMeshProUGUI>();
            lbl.text = title;
            lbl.fontSize = 16;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Left;
            ApplyCustomFont(lbl);
            RectTransform lRect = labelObj.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 0.5f);
            lRect.anchorMax = new Vector2(0f, 0.5f);
            lRect.pivot = new Vector2(0f, 0.5f);
            lRect.anchoredPosition = new Vector2(16f, 0f);
            lRect.sizeDelta = new Vector2(300f, 30f);

            // Value Display Text
            GameObject valTextObj = new GameObject("ValueText");
            valTextObj.transform.SetParent(row.transform, false);
            TMP_Text valText = valTextObj.AddComponent<TextMeshProUGUI>();
            valText.text = $"{Mathf.RoundToInt(initialValue * 100)}%";
            valText.fontSize = 16;
            valText.color = primaryAccent;
            valText.alignment = TextAlignmentOptions.Right;
            ApplyCustomFont(valText);
            RectTransform vRect = valTextObj.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(1f, 0.5f);
            vRect.anchorMax = new Vector2(1f, 0.5f);
            vRect.pivot = new Vector2(1f, 0.5f);
            vRect.anchoredPosition = new Vector2(-16f, 0f);
            vRect.sizeDelta = new Vector2(60f, 30f);

            // Slider Object
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(row.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(1f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(1f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(-85f, 0f);
            sliderRect.sizeDelta = new Vector2(170f, 20f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;

            // Slider Background
            GameObject sBg = new GameObject("Background");
            sBg.transform.SetParent(sliderObj.transform, false);
            Image sBgImg = sBg.AddComponent<Image>();
            sBgImg.color = new Color(0.18f, 0.2f, 0.26f, 1f);
            RectTransform sBgRect = sBg.GetComponent<RectTransform>();
            sBgRect.anchorMin = new Vector2(0f, 0.25f);
            sBgRect.anchorMax = new Vector2(1f, 0.75f);
            sBgRect.offsetMin = Vector2.zero;
            sBgRect.offsetMax = Vector2.zero;

            // Slider Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fAreaRect = fillArea.AddComponent<RectTransform>();
            fAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fAreaRect.offsetMin = Vector2.zero;
            fAreaRect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = primaryAccent;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;

            // Slider Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform hAreaRect = handleArea.AddComponent<RectTransform>();
            hAreaRect.anchorMin = Vector2.zero;
            hAreaRect.anchorMax = Vector2.one;
            hAreaRect.offsetMin = Vector2.zero;
            hAreaRect.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18f, 24f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            slider.onValueChanged.AddListener((v) =>
            {
                valText.text = $"{Mathf.RoundToInt(v * 100)}%";
                onValueChanged?.Invoke(v);
            });
        }

        private void CreateToggleRow(Transform parent, string title, bool initialValue, UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            GameObject row = new GameObject("ToggleRow");
            row.transform.SetParent(parent, false);

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 50f;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.03f);

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            TMP_Text lbl = labelObj.AddComponent<TextMeshProUGUI>();
            lbl.text = title;
            lbl.fontSize = 16;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Left;
            ApplyCustomFont(lbl);
            RectTransform lRect = labelObj.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 0.5f);
            lRect.anchorMax = new Vector2(0f, 0.5f);
            lRect.pivot = new Vector2(0f, 0.5f);
            lRect.anchoredPosition = new Vector2(16f, 0f);
            lRect.sizeDelta = new Vector2(300f, 30f);

            // Toggle Container
            GameObject toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(row.transform, false);
            RectTransform tRect = toggleObj.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(1f, 0.5f);
            tRect.anchorMax = new Vector2(1f, 0.5f);
            tRect.pivot = new Vector2(1f, 0.5f);
            tRect.anchoredPosition = new Vector2(-20f, 0f);
            tRect.sizeDelta = new Vector2(32f, 32f);

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = initialValue;

            Image tBg = toggleObj.AddComponent<Image>();
            tBg.color = new Color(0.18f, 0.2f, 0.26f, 1f);

            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleObj.transform, false);
            Image checkmarkImg = checkmarkObj.AddComponent<Image>();
            checkmarkImg.color = primaryAccent;
            RectTransform cRect = checkmarkObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.2f, 0.2f);
            cRect.anchorMax = new Vector2(0.8f, 0.8f);
            cRect.offsetMin = Vector2.zero;
            cRect.offsetMax = Vector2.zero;

            toggle.graphic = checkmarkImg;
            toggle.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));
        }

        // =========================================================================
        //  Character Selection Modal Dialog
        // =========================================================================
        private GameObject CreateCharacterSelectModal(Transform canvasTransform)
        {
            GameObject modalRoot = new GameObject("CharacterSelectModal");
            modalRoot.transform.SetParent(canvasTransform, false);

            RectTransform modalRect = modalRoot.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            // Semi-transparent blocker backdrop
            Image backdrop = modalRoot.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

            // Center Panel (1180 x 700)
            GameObject panel = new GameObject("ModalPanel");
            panel.transform.SetParent(modalRoot.transform, false);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = panelBgColor;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1180f, 700f);

            // Panel Border (Arcane Purple Accent)
            GameObject border = new GameObject("PanelBorder");
            border.transform.SetParent(panel.transform, false);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.60f);
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            border.transform.SetAsFirstSibling();

            // Header Title
            GameObject headerObj = new GameObject("ModalTitle");
            headerObj.transform.SetParent(panel.transform, false);
            TMP_Text headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "OPERATIVE SELECTION // เลือกตัวละครเข้าสู่โลกาวินาศ";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = secondaryAccent;
            headerText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(headerText);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -18f);
            hRect.sizeDelta = new Vector2(1100f, 32f);

            // Header Subtitle
            GameObject subHeaderObj = new GameObject("ModalSubtitle");
            subHeaderObj.transform.SetParent(panel.transform, false);
            TMP_Text subHeaderText = subHeaderObj.AddComponent<TextMeshProUGUI>();
            subHeaderText.text = "5 OPERATIVES • 5 AMNESIA PERSPECTIVES OF THE FALLEN EMPIRE • SIGNATURE SHIFT SKILLS";
            subHeaderText.fontSize = 13;
            subHeaderText.color = new Color(0.7f, 0.75f, 0.85f, 0.70f);
            subHeaderText.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(subHeaderText);
            RectTransform shRect = subHeaderObj.GetComponent<RectTransform>();
            shRect.anchorMin = new Vector2(0f, 1f);
            shRect.anchorMax = new Vector2(1f, 1f);
            shRect.pivot = new Vector2(0.5f, 1f);
            shRect.anchoredPosition = new Vector2(0f, -50f);
            shRect.sizeDelta = new Vector2(1100f, 20f);

            // Content Area Container (Split into Left Roster and Right Dossier)
            GameObject contentObj = new GameObject("ContentSplitArea");
            contentObj.transform.SetParent(panel.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(30f, 80f);
            contentRect.offsetMax = new Vector2(-30f, -80f);

            // Left Roster Column (320px)
            GameObject rosterCol = new GameObject("RosterColumn");
            rosterCol.transform.SetParent(contentObj.transform, false);
            RectTransform rColRect = rosterCol.AddComponent<RectTransform>();
            rColRect.anchorMin = new Vector2(0f, 0f);
            rColRect.anchorMax = new Vector2(0f, 1f);
            rColRect.pivot = new Vector2(0f, 0.5f);
            rColRect.anchoredPosition = Vector2.zero;
            rColRect.sizeDelta = new Vector2(320f, 0f);

            VerticalLayoutGroup rLayout = rosterCol.AddComponent<VerticalLayoutGroup>();
            rLayout.spacing = 10f;
            rLayout.childControlWidth = true;
            rLayout.childControlHeight = true;
            rLayout.childForceExpandWidth = true;
            rLayout.childForceExpandHeight = false;

            // Right Dossier Column (Remaining space: ~770px)
            GameObject dossierCol = new GameObject("DossierColumn");
            dossierCol.transform.SetParent(contentObj.transform, false);
            RectTransform dColRect = dossierCol.AddComponent<RectTransform>();
            dColRect.anchorMin = new Vector2(0f, 0f);
            dColRect.anchorMax = new Vector2(1f, 1f);
            dColRect.pivot = new Vector2(1f, 0.5f);
            dColRect.offsetMin = new Vector2(340f, 0f);
            dColRect.offsetMax = Vector2.zero;

            Image dossierBg = dossierCol.AddComponent<Image>();
            dossierBg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);

            GameObject dossierBorder = new GameObject("DossierBorder");
            dossierBorder.transform.SetParent(dossierCol.transform, false);
            Image dBorderImg = dossierBorder.AddComponent<Image>();
            dBorderImg.color = new Color(0.2f, 0.25f, 0.35f, 0.45f);
            RectTransform dbRect = dossierBorder.GetComponent<RectTransform>();
            dbRect.anchorMin = Vector2.zero;
            dbRect.anchorMax = Vector2.one;
            dbRect.offsetMin = new Vector2(-1f, -1f);
            dbRect.offsetMax = new Vector2(1f, 1f);
            dossierBorder.transform.SetAsFirstSibling();

            // Right Dossier Sub-elements:
            // 1. Header (Name + Title + Role Category)
            GameObject dHeader = new GameObject("DossierHeader");
            dHeader.transform.SetParent(dossierCol.transform, false);
            TMP_Text dNameText = dHeader.AddComponent<TextMeshProUGUI>();
            dNameText.fontSize = 28;
            dNameText.fontStyle = FontStyles.Bold;
            dNameText.color = Color.white;
            ApplyCustomFont(dNameText);
            RectTransform dhRect = dHeader.GetComponent<RectTransform>();
            dhRect.anchorMin = new Vector2(0f, 1f);
            dhRect.anchorMax = new Vector2(1f, 1f);
            dhRect.pivot = new Vector2(0f, 1f);
            dhRect.anchoredPosition = new Vector2(25f, -18f);
            dhRect.sizeDelta = new Vector2(720f, 36f);

            GameObject dRoleObj = new GameObject("DossierRole");
            dRoleObj.transform.SetParent(dossierCol.transform, false);
            TMP_Text dRoleText = dRoleObj.AddComponent<TextMeshProUGUI>();
            dRoleText.fontSize = 15;
            dRoleText.fontStyle = FontStyles.Bold;
            dRoleText.color = secondaryAccent;
            ApplyCustomFont(dRoleText);
            RectTransform drRect = dRoleObj.GetComponent<RectTransform>();
            drRect.anchorMin = new Vector2(0f, 1f);
            drRect.anchorMax = new Vector2(1f, 1f);
            drRect.pivot = new Vector2(0f, 1f);
            drRect.anchoredPosition = new Vector2(25f, -54f);
            drRect.sizeDelta = new Vector2(720f, 24f);

            // 2. Amnesia Waking Clue Box
            GameObject clueBox = new GameObject("AmnesiaClueBox");
            clueBox.transform.SetParent(dossierCol.transform, false);
            Image clueBoxBg = clueBox.AddComponent<Image>();
            clueBoxBg.color = new Color(0.18f, 0.12f, 0.05f, 0.55f);
            RectTransform cbRect = clueBox.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0f, 1f);
            cbRect.anchorMax = new Vector2(1f, 1f);
            cbRect.pivot = new Vector2(0.5f, 1f);
            cbRect.anchoredPosition = new Vector2(0f, -86f);
            cbRect.sizeDelta = new Vector2(730f, 85f);

            GameObject clueBorder = new GameObject("ClueBorder");
            clueBorder.transform.SetParent(clueBox.transform, false);
            Image cbBorderImg = clueBorder.AddComponent<Image>();
            cbBorderImg.color = new Color(0.95f, 0.65f, 0.2f, 0.5f);
            RectTransform cbbRect = clueBorder.GetComponent<RectTransform>();
            cbbRect.anchorMin = Vector2.zero;
            cbbRect.anchorMax = Vector2.one;
            cbbRect.offsetMin = new Vector2(-1f, -1f);
            cbbRect.offsetMax = new Vector2(1f, 1f);
            clueBorder.transform.SetAsFirstSibling();

            GameObject clueTitleObj = new GameObject("ClueTitle");
            clueTitleObj.transform.SetParent(clueBox.transform, false);
            TMP_Text clueTitleText = clueTitleObj.AddComponent<TextMeshProUGUI>();
            clueTitleText.text = "❖ ความทรงจำสุดท้ายก่อนตื่น (LAST MEMORY BEFORE AWAKENING) ❖";
            clueTitleText.fontSize = 12;
            clueTitleText.fontStyle = FontStyles.Bold;
            clueTitleText.color = new Color(1f, 0.75f, 0.3f, 0.95f);
            ApplyCustomFont(clueTitleText);
            RectTransform ctRect = clueTitleObj.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(0f, 1f);
            ctRect.anchorMax = new Vector2(1f, 1f);
            ctRect.anchoredPosition = new Vector2(16f, -10f);
            ctRect.sizeDelta = new Vector2(700f, 18f);

            GameObject clueBodyObj = new GameObject("ClueBody");
            clueBodyObj.transform.SetParent(clueBox.transform, false);
            TMP_Text clueBodyText = clueBodyObj.AddComponent<TextMeshProUGUI>();
            clueBodyText.fontSize = 13;
            clueBodyText.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);
            ApplyCustomFont(clueBodyText);
            RectTransform cBodyRect = clueBodyObj.GetComponent<RectTransform>();
            cBodyRect.anchorMin = new Vector2(0f, 0f);
            cBodyRect.anchorMax = new Vector2(1f, 1f);
            cBodyRect.offsetMin = new Vector2(16f, 8f);
            cBodyRect.offsetMax = new Vector2(-16f, -28f);

            // 3. Lore Synopsis
            GameObject loreBox = new GameObject("LoreBox");
            loreBox.transform.SetParent(dossierCol.transform, false);
            RectTransform lbRect = loreBox.AddComponent<RectTransform>();
            lbRect.anchorMin = new Vector2(0f, 1f);
            lbRect.anchorMax = new Vector2(1f, 1f);
            lbRect.pivot = new Vector2(0.5f, 1f);
            lbRect.anchoredPosition = new Vector2(0f, -178f);
            lbRect.sizeDelta = new Vector2(730f, 75f);

            GameObject loreTitleObj = new GameObject("LoreTitle");
            loreTitleObj.transform.SetParent(loreBox.transform, false);
            TMP_Text loreTitleText = loreTitleObj.AddComponent<TextMeshProUGUI>();
            loreTitleText.text = "❖ ปูมหลังและมุมมอง (BACKGROUND & PERSPECTIVE) ❖";
            loreTitleText.fontSize = 12;
            loreTitleText.fontStyle = FontStyles.Bold;
            loreTitleText.color = secondaryAccent;
            ApplyCustomFont(loreTitleText);
            RectTransform ltRect = loreTitleObj.GetComponent<RectTransform>();
            ltRect.anchorMin = new Vector2(0f, 1f);
            ltRect.anchorMax = new Vector2(1f, 1f);
            ltRect.anchoredPosition = new Vector2(0f, 0f);
            ltRect.sizeDelta = new Vector2(730f, 18f);

            GameObject loreBodyObj = new GameObject("LoreBody");
            loreBodyObj.transform.SetParent(loreBox.transform, false);
            TMP_Text loreBodyText = loreBodyObj.AddComponent<TextMeshProUGUI>();
            loreBodyText.fontSize = 13;
            loreBodyText.color = new Color(0.85f, 0.88f, 0.94f, 0.95f);
            ApplyCustomFont(loreBodyText);
            RectTransform lBodyRect = loreBodyObj.GetComponent<RectTransform>();
            lBodyRect.anchorMin = new Vector2(0f, 0f);
            lBodyRect.anchorMax = new Vector2(1f, 1f);
            lBodyRect.offsetMin = new Vector2(0f, 0f);
            lBodyRect.offsetMax = new Vector2(0f, -22f);

            // 4. Attributes Block (Left side) + Shift Skill Block (Right side)
            GameObject statsRow = new GameObject("StatsAndSkillRow");
            statsRow.transform.SetParent(dossierCol.transform, false);
            RectTransform srRect = statsRow.AddComponent<RectTransform>();
            srRect.anchorMin = new Vector2(0f, 1f);
            srRect.anchorMax = new Vector2(1f, 1f);
            srRect.pivot = new Vector2(0.5f, 1f);
            srRect.anchoredPosition = new Vector2(0f, -258f);
            srRect.sizeDelta = new Vector2(730f, 160f);

            // Attributes Sub-box
            GameObject attrBox = new GameObject("AttributesBox");
            attrBox.transform.SetParent(statsRow.transform, false);
            Image attrBg = attrBox.AddComponent<Image>();
            attrBg.color = new Color(1f, 1f, 1f, 0.04f);
            RectTransform attrRect = attrBox.GetComponent<RectTransform>();
            attrRect.anchorMin = new Vector2(0f, 0f);
            attrRect.anchorMax = new Vector2(0.42f, 1f);
            attrRect.offsetMin = Vector2.zero;
            attrRect.offsetMax = Vector2.zero;

            GameObject attrTitleObj = new GameObject("AttrTitle");
            attrTitleObj.transform.SetParent(attrBox.transform, false);
            TMP_Text attrTitleText = attrTitleObj.AddComponent<TextMeshProUGUI>();
            attrTitleText.text = "⚡ OPERATIVE ATTRIBUTES";
            attrTitleText.fontSize = 12;
            attrTitleText.fontStyle = FontStyles.Bold;
            attrTitleText.color = new Color(0.3f, 0.85f, 1f, 0.95f);
            ApplyCustomFont(attrTitleText);
            RectTransform atRect = attrTitleObj.GetComponent<RectTransform>();
            atRect.anchorMin = new Vector2(0f, 1f);
            atRect.anchorMax = new Vector2(1f, 1f);
            atRect.anchoredPosition = new Vector2(12f, -10f);
            atRect.sizeDelta = new Vector2(280f, 18f);

            GameObject attrBodyObj = new GameObject("AttrBody");
            attrBodyObj.transform.SetParent(attrBox.transform, false);
            TMP_Text attrBodyText = attrBodyObj.AddComponent<TextMeshProUGUI>();
            attrBodyText.fontSize = 13;
            attrBodyText.color = Color.white;
            ApplyCustomFont(attrBodyText);
            RectTransform aBodyRect = attrBodyObj.GetComponent<RectTransform>();
            aBodyRect.anchorMin = new Vector2(0f, 0f);
            aBodyRect.anchorMax = new Vector2(1f, 1f);
            aBodyRect.offsetMin = new Vector2(12f, 8f);
            aBodyRect.offsetMax = new Vector2(-12f, -30f);

            // Shift Ability Sub-box
            GameObject skillBox = new GameObject("ShiftSkillBox");
            skillBox.transform.SetParent(statsRow.transform, false);
            Image skillBg = skillBox.AddComponent<Image>();
            skillBg.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.12f);
            RectTransform skillRect = skillBox.GetComponent<RectTransform>();
            skillRect.anchorMin = new Vector2(0.44f, 0f);
            skillRect.anchorMax = new Vector2(1f, 1f);
            skillRect.offsetMin = Vector2.zero;
            skillRect.offsetMax = Vector2.zero;

            GameObject skillBorder = new GameObject("SkillBorder");
            skillBorder.transform.SetParent(skillBox.transform, false);
            Image sBorderImg = skillBorder.AddComponent<Image>();
            sBorderImg.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.45f);
            RectTransform sbRect = skillBorder.GetComponent<RectTransform>();
            sbRect.anchorMin = Vector2.zero;
            sbRect.anchorMax = Vector2.one;
            sbRect.offsetMin = new Vector2(-1f, -1f);
            sbRect.offsetMax = new Vector2(1f, 1f);
            skillBorder.transform.SetAsFirstSibling();

            GameObject skillTitleObj = new GameObject("SkillTitle");
            skillTitleObj.transform.SetParent(skillBox.transform, false);
            TMP_Text skillTitleText = skillTitleObj.AddComponent<TextMeshProUGUI>();
            skillTitleText.fontSize = 14;
            skillTitleText.fontStyle = FontStyles.Bold;
            skillTitleText.color = secondaryAccent;
            ApplyCustomFont(skillTitleText);
            RectTransform stRect = skillTitleObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 1f);
            stRect.anchorMax = new Vector2(1f, 1f);
            stRect.anchoredPosition = new Vector2(12f, -10f);
            stRect.sizeDelta = new Vector2(380f, 20f);

            GameObject skillBodyObj = new GameObject("SkillBody");
            skillBodyObj.transform.SetParent(skillBox.transform, false);
            TMP_Text skillBodyText = skillBodyObj.AddComponent<TextMeshProUGUI>();
            skillBodyText.fontSize = 12;
            skillBodyText.color = new Color(0.92f, 0.92f, 0.95f, 0.95f);
            ApplyCustomFont(skillBodyText);
            RectTransform sBodyRect = skillBodyObj.GetComponent<RectTransform>();
            sBodyRect.anchorMin = new Vector2(0f, 0f);
            sBodyRect.anchorMax = new Vector2(1f, 1f);
            sBodyRect.offsetMin = new Vector2(12f, 8f);
            sBodyRect.offsetMax = new Vector2(-12f, -34f);

            // 5. Starting Loadout Box
            GameObject loadoutBox = new GameObject("LoadoutBox");
            loadoutBox.transform.SetParent(dossierCol.transform, false);
            RectTransform loutRect = loadoutBox.AddComponent<RectTransform>();
            loutRect.anchorMin = new Vector2(0f, 0f);
            loutRect.anchorMax = new Vector2(1f, 0f);
            loutRect.pivot = new Vector2(0.5f, 0f);
            loutRect.anchoredPosition = new Vector2(0f, 15f);
            loutRect.sizeDelta = new Vector2(730f, 85f);

            Image loutBg = loadoutBox.AddComponent<Image>();
            loutBg.color = new Color(1f, 1f, 1f, 0.04f);

            GameObject loutTitleObj = new GameObject("LoadoutTitle");
            loutTitleObj.transform.SetParent(loadoutBox.transform, false);
            TMP_Text loutTitleText = loutTitleObj.AddComponent<TextMeshProUGUI>();
            loutTitleText.text = "🎒 STARTING ARSENAL & SURVIVAL LOADOUT";
            loutTitleText.fontSize = 12;
            loutTitleText.fontStyle = FontStyles.Bold;
            loutTitleText.color = new Color(0.4f, 0.95f, 0.6f, 0.95f);
            ApplyCustomFont(loutTitleText);
            RectTransform lttRect = loutTitleObj.GetComponent<RectTransform>();
            lttRect.anchorMin = new Vector2(0f, 1f);
            lttRect.anchorMax = new Vector2(1f, 1f);
            lttRect.anchoredPosition = new Vector2(12f, -10f);
            lttRect.sizeDelta = new Vector2(700f, 18f);

            GameObject loutBodyObj = new GameObject("LoadoutBody");
            loutBodyObj.transform.SetParent(loadoutBox.transform, false);
            TMP_Text loutBodyText = loutBodyObj.AddComponent<TextMeshProUGUI>();
            loutBodyText.fontSize = 13;
            loutBodyText.color = Color.white;
            ApplyCustomFont(loutBodyText);
            RectTransform ltBodyRect = loutBodyObj.GetComponent<RectTransform>();
            ltBodyRect.anchorMin = new Vector2(0f, 0f);
            ltBodyRect.anchorMax = new Vector2(1f, 1f);
            ltBodyRect.offsetMin = new Vector2(12f, 8f);
            ltBodyRect.offsetMax = new Vector2(-12f, -30f);

            // Populate Roster Column with 5 Operative Tabs
            List<CharacterData> roster = (CharacterDatabase.Instance != null && CharacterDatabase.Instance.Characters.Count > 0)
                ? CharacterDatabase.Instance.Characters
                : CharacterDatabase.CreateDefaultRoster();

            string currentSelectedId = CharacterManager.SelectedCharacterId;
            List<Button> tabButtons = new List<Button>();
            List<Image> tabBorders = new List<Image>();

            System.Action<CharacterData> displayCharacter = (ch) =>
            {
                currentSelectedId = ch.characterId;
                dNameText.text = ch.characterName.ToUpper();
                dRoleText.text = $"{ch.characterTitle}  •  <color=#A855F7>{ch.roleCategory}</color>";
                clueBodyText.text = ch.amnesiaWakingClue;
                loreBodyText.text = ch.loreSynopsis;
                attrBodyText.text = $"• Max HP: <b>{ch.maxHealth}</b>\n• Max Hunger: <b>{ch.maxHunger}</b>\n• Move Speed: <b>{ch.moveSpeed:F1} m/s</b>\n• Dash Speed: <b>{ch.dashSpeed:F0} m/s</b> ({ch.dashCooldown:F1}s CD)";
                skillTitleText.text = $"✦ [SHIFT] {ch.shiftAbilityName.ToUpper()}";
                skillBodyText.text = ch.shiftAbilityDescription;
                string wpns = (ch.startingWeapons != null && ch.startingWeapons.Count > 0) ? string.Join(", ", ch.startingWeapons) : "Knife";
                loutBodyText.text = $"• Primary Weapon: <b>{wpns}</b>\n• Starting Ammo: <b>{ch.startingPistolAmmo} rounds</b> | Ration: <b>2x Bread</b> | Credits: <b>{ch.startingMoney} Gold</b>";

                for (int i = 0; i < tabBorders.Count; i++)
                {
                    bool isThis = roster[i].characterId.Equals(ch.characterId, System.StringComparison.OrdinalIgnoreCase);
                    tabBorders[i].color = isThis ? ch.themeColor : new Color(0.24f, 0.28f, 0.35f, 0.40f);
                }
            };

            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                GameObject tabObj = new GameObject($"Tab_{ch.characterId}");
                tabObj.transform.SetParent(rosterCol.transform, false);

                LayoutElement le = tabObj.AddComponent<LayoutElement>();
                le.preferredHeight = 90f;
                le.minHeight = 90f;

                Image tabBg = tabObj.AddComponent<Image>();
                tabBg.color = buttonBgColor;

                GameObject tBorder = new GameObject("Border");
                tBorder.transform.SetParent(tabObj.transform, false);
                Image tbImg = tBorder.AddComponent<Image>();
                tbImg.color = new Color(0.24f, 0.28f, 0.35f, 0.40f);
                RectTransform tbRect = tBorder.GetComponent<RectTransform>();
                tbRect.anchorMin = Vector2.zero;
                tbRect.anchorMax = Vector2.one;
                tbRect.offsetMin = new Vector2(-1.5f, -1.5f);
                tbRect.offsetMax = new Vector2(1.5f, 1.5f);
                tBorder.transform.SetAsFirstSibling();
                tabBorders.Add(tbImg);

                // Tab Text Container
                GameObject tLabelObj = new GameObject("Label");
                tLabelObj.transform.SetParent(tabObj.transform, false);
                TMP_Text tLabel = tLabelObj.AddComponent<TextMeshProUGUI>();
                tLabel.text = $"<b>{ch.characterName.ToUpper()}</b>\n<size=11><color=#A855F7>{ch.roleCategory}</color></size>";
                tLabel.fontSize = 15;
                tLabel.color = Color.white;
                ApplyCustomFont(tLabel);
                RectTransform tlRect = tLabelObj.GetComponent<RectTransform>();
                tlRect.anchorMin = Vector2.zero;
                tlRect.anchorMax = Vector2.one;
                tlRect.offsetMin = new Vector2(16f, 0f);
                tlRect.offsetMax = new Vector2(-16f, 0f);

                Button tBtn = tabObj.AddComponent<Button>();
                tBtn.onClick.AddListener(() => displayCharacter(ch));
                AttachHoverEffect(tBtn);
                tabButtons.Add(tBtn);
            }

            // Find default selection and display it
            CharacterData defaultChar = roster.Find(c => c.characterId.Equals(currentSelectedId, System.StringComparison.OrdinalIgnoreCase)) ?? roster[0];
            displayCharacter(defaultChar);

            // Bottom Action Bar: CANCEL (Left) and EMBARK (Right)
            GameObject cancelBtnObj = new GameObject("CancelButton");
            cancelBtnObj.transform.SetParent(panel.transform, false);
            RectTransform cancelRect = cancelBtnObj.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0f, 0f);
            cancelRect.anchorMax = new Vector2(0f, 0f);
            cancelRect.pivot = new Vector2(0f, 0f);
            cancelRect.anchoredPosition = new Vector2(30f, 18f);
            cancelRect.sizeDelta = new Vector2(200f, 48f);

            Image cancelBg = cancelBtnObj.AddComponent<Image>();
            cancelBg.color = buttonBgColor;

            GameObject cancelLabelObj = new GameObject("Label");
            cancelLabelObj.transform.SetParent(cancelBtnObj.transform, false);
            TMP_Text cancelLabel = cancelLabelObj.AddComponent<TextMeshProUGUI>();
            cancelLabel.text = "CANCEL // ย้อนกลับ";
            cancelLabel.fontSize = 18;
            cancelLabel.fontStyle = FontStyles.Bold;
            cancelLabel.color = Color.white;
            cancelLabel.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(cancelLabel);
            RectTransform cLabelRect = cancelLabelObj.GetComponent<RectTransform>();
            cLabelRect.anchorMin = Vector2.zero;
            cLabelRect.anchorMax = Vector2.one;

            Button cancelBtn = cancelBtnObj.AddComponent<Button>();
            cancelBtn.onClick.AddListener(OnCloseCharacterSelect);
            AttachHoverEffect(cancelBtn);

            // EMBARK Button (Right side, glowing vibrant purple)
            GameObject embarkBtnObj = new GameObject("EmbarkButton");
            embarkBtnObj.transform.SetParent(panel.transform, false);
            RectTransform embarkRect = embarkBtnObj.AddComponent<RectTransform>();
            embarkRect.anchorMin = new Vector2(1f, 0f);
            embarkRect.anchorMax = new Vector2(1f, 0f);
            embarkRect.pivot = new Vector2(1f, 0f);
            embarkRect.anchoredPosition = new Vector2(-30f, 18f);
            embarkRect.sizeDelta = new Vector2(320f, 48f);

            Image embarkBg = embarkBtnObj.AddComponent<Image>();
            embarkBg.color = primaryAccent;

            GameObject embarkLabelObj = new GameObject("Label");
            embarkLabelObj.transform.SetParent(embarkBtnObj.transform, false);
            TMP_Text embarkLabel = embarkLabelObj.AddComponent<TextMeshProUGUI>();
            embarkLabel.text = "EMBARK // เข้าสู่โลกาวินาศ ➔";
            embarkLabel.fontSize = 20;
            embarkLabel.fontStyle = FontStyles.Bold;
            embarkLabel.color = Color.white;
            embarkLabel.alignment = TextAlignmentOptions.Center;
            ApplyCustomFont(embarkLabel);
            RectTransform eLabelRect = embarkLabelObj.GetComponent<RectTransform>();
            eLabelRect.anchorMin = Vector2.zero;
            eLabelRect.anchorMax = Vector2.one;

            Button embarkBtn = embarkBtnObj.AddComponent<Button>();
            embarkBtn.onClick.AddListener(() => StartGameWithCharacter(currentSelectedId));
            AttachHoverEffect(embarkBtn);

            return modalRoot;
        }

        // =========================================================================
        //  Footer UI (Atmospheric Lore Quote & Version Tag)
        // =========================================================================
        private void CreateFooterUI(Transform canvasTransform)
        {
            // Bottom-Left Atmospheric Quote
            GameObject quoteObj = new GameObject("LoreQuoteText");
            quoteObj.transform.SetParent(canvasTransform, false);
            TMP_Text quoteText = quoteObj.AddComponent<TextMeshProUGUI>();
            quoteText.text = "<i>\"The empire collapsed under ash and rupture. Survive the night, scavenge the ruins.\"</i>";
            quoteText.fontSize = 14;
            quoteText.color = new Color(0.6f, 0.65f, 0.72f, 0.70f);
            quoteText.alignment = TextAlignmentOptions.Left;
            ApplyCustomFont(quoteText);
            RectTransform qRect = quoteObj.GetComponent<RectTransform>();
            qRect.anchorMin = new Vector2(0f, 0f);
            qRect.anchorMax = new Vector2(0f, 0f);
            qRect.pivot = new Vector2(0f, 0f);
            qRect.anchoredPosition = new Vector2(32f, 24f);
            qRect.sizeDelta = new Vector2(700f, 30f);

            // Bottom-Right Version & Build Status
            GameObject verObj = new GameObject("VersionTagText");
            verObj.transform.SetParent(canvasTransform, false);
            TMP_Text verText = verObj.AddComponent<TextMeshProUGUI>();
            verText.text = "<b>THE LAST EMPIRE</b> // v1.0 - BUILD PROTOCOL ACTIVE";
            verText.fontSize = 14;
            verText.color = new Color(secondaryAccent.r, secondaryAccent.g, secondaryAccent.b, 0.65f);
            verText.alignment = TextAlignmentOptions.Right;
            ApplyCustomFont(verText);
            RectTransform vRect = verObj.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(1f, 0f);
            vRect.anchorMax = new Vector2(1f, 0f);
            vRect.pivot = new Vector2(1f, 0f);
            vRect.anchoredPosition = new Vector2(-32f, 24f);
            vRect.sizeDelta = new Vector2(500f, 30f);
        }
    }

    // =========================================================================
    //  Interactive Hover Effect Component for Menu Buttons
    // =========================================================================
    public class MenuButtonHoverEffect : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler
    {
        private Vector3 _originalScale = Vector3.one;
        private Image _bgImage;
        private Color _originalBgColor;
        private Image _borderImage;
        private Color _originalBorderColor;

        private void Awake()
        {
            _originalScale = transform.localScale;
            _bgImage = GetComponent<Image>();
            if (_bgImage != null) _originalBgColor = _bgImage.color;

            Transform borderTransform = transform.Find("Border");
            if (borderTransform != null)
            {
                _borderImage = borderTransform.GetComponent<Image>();
                if (_borderImage != null) _originalBorderColor = _borderImage.color;
            }
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Button btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            transform.localScale = _originalScale * 1.035f;

            if (_bgImage != null)
            {
                _bgImage.color = new Color(_originalBgColor.r + 0.08f, _originalBgColor.g + 0.08f, _originalBgColor.b + 0.12f, 1f);
            }
            if (_borderImage != null)
            {
                _borderImage.color = new Color(_originalBorderColor.r, _originalBorderColor.g, _originalBorderColor.b, 0.90f);
            }
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            transform.localScale = _originalScale;
            if (_bgImage != null) _bgImage.color = _originalBgColor;
            if (_borderImage != null) _borderImage.color = _originalBorderColor;
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Button btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            transform.localScale = _originalScale * 0.97f;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Button btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            transform.localScale = _originalScale * 1.035f;
        }
    }
}
