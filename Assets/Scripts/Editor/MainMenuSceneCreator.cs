using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class MainMenuSceneCreator
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/Gameplay_3D.unity";
        private const string ScenicBgPath = "Assets/Textures/MainMenu_Scenic_BG.jpg";

        // Layout Theme: Arcane / Post-Apocalyptic Vibrant Palette matching reference
        private static readonly Color PrimaryAccent = new Color(0.66f, 0.33f, 0.97f, 1f);    // Vibrant Purple (#A855F7)
        private static readonly Color HighlightAccent = new Color(0.75f, 0.52f, 0.99f, 1f);  // Lilac (#C084FC)
        private static readonly Color SidebarBgColor = new Color(0.08f, 0.09f, 0.11f, 0.96f); // Dark Obsidian Sidebar
        private static readonly Color SecondaryBtnColor = new Color(0.14f, 0.16f, 0.19f, 0.95f); // Dark Button Slate
        private static readonly Color BorderColor = new Color(0.24f, 0.28f, 0.34f, 0.60f);

        static MainMenuSceneCreator()
        {
            EditorApplication.delayCall += AutoBakeHierarchyIfNeeded;
        }

        private static void AutoBakeHierarchyIfNeeded()
        {
            EnsureMainMenuRegisteredInBuildSettings();

            if (File.Exists(MainMenuScenePath))
            {
                string sceneText = File.ReadAllText(MainMenuScenePath);
                // If it doesn't have the LeftSidebarPanel yet, bake the new layout!
                if (!sceneText.Contains("LeftSidebarPanel"))
                {
                    Scene activeScene = EditorSceneManager.GetActiveScene();
                    bool isOpen = activeScene.path.Equals(MainMenuScenePath, System.StringComparison.OrdinalIgnoreCase);

                    Scene targetScene;
                    if (isOpen)
                    {
                        targetScene = activeScene;
                    }
                    else
                    {
                        targetScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
                    }

                    BakeCompleteHierarchyInActiveScene(targetScene);
                }
            }
        }

        [MenuItem("The Last Empire/Bake Main Menu Hierarchy in Scene", priority = 1)]
        public static void BakeMainMenuHierarchy()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool isOpen = activeScene.path.Equals(MainMenuScenePath, System.StringComparison.OrdinalIgnoreCase);

            Scene sceneToBake;
            if (isOpen)
            {
                sceneToBake = activeScene;
            }
            else
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    sceneToBake = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
                }
                else
                {
                    return;
                }
            }

            BakeCompleteHierarchyInActiveScene(sceneToBake);
        }

        public static void BakeCompleteHierarchyInActiveScene(Scene scene)
        {
            // 1. Clear existing objects to guarantee clean rebuild
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Object.DestroyImmediate(root);
            }

            // 2. Camera Setup
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.transform.position = new Vector3(0f, 0f, -10f);

            // 3. Directional Light
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.88f, 0.75f, 1f);
            dirLight.intensity = 0.8f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 4. Background Atmospheric Embers Particle System (Zero VelocityModule errors)
            GameObject emberObj = new GameObject("BackgroundEmbersVFX");
            emberObj.transform.position = new Vector3(2f, -5f, 5f);
            emberObj.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = emberObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.55f, 0.15f, 0.75f),
                new Color(0.9f, 0.45f, 1.0f, 0.85f)
            );
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 10f, 1f);
            shape.randomDirectionAmount = 0.2f;

            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = false; // Kept safely disabled

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.7f, 0.3f), 0f), new GradientColorKey(new Color(0.7f, 0.3f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            ParticleSystemRenderer psRenderer = emberObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"));

            // 5. EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            System.Type inputSystemModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                eventSystemObj.AddComponent(inputSystemModuleType);
            }
            else
            {
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 6. Locate Teko Font if present
            TMP_FontAsset tekoFont = null;
            TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in allFonts)
            {
                if (f != null && f.name.Contains("Teko"))
                {
                    tekoFont = f;
                    break;
                }
            }

            // 7. MainMenuCanvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvasObj.layer = 5; // UI
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();

            // -------------------------------------------------------------
            // Fullscreen Scenic Background (Right & Center World Artwork)
            // -------------------------------------------------------------
            GameObject bgObj = CreateUIObject("ScenicBackground", canvasObj.transform);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.white;
            Sprite scenicSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ScenicBgPath);
            if (scenicSprite != null)
            {
                bgImg.sprite = scenicSprite;
                bgImg.preserveAspect = false;
            }
            else
            {
                bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            }
            SetFullStretch(bgObj.GetComponent<RectTransform>());

            // Right-Side Vignette Shadow (for depth)
            GameObject vignetteObj = CreateUIObject("VignetteOverlay", bgObj.transform);
            Image vignetteImg = vignetteObj.AddComponent<Image>();
            vignetteImg.color = new Color(0f, 0f, 0f, 0.20f);
            SetFullStretch(vignetteObj.GetComponent<RectTransform>());

            // -------------------------------------------------------------
            // Left Sidebar Panel (Matches Reference Layout!)
            // -------------------------------------------------------------
            GameObject sidebarObj = CreateUIObject("LeftSidebarPanel", canvasObj.transform);
            Image sidebarImg = sidebarObj.AddComponent<Image>();
            sidebarImg.color = SidebarBgColor;

            RectTransform sidebarRect = sidebarObj.GetComponent<RectTransform>();
            sidebarRect.anchorMin = new Vector2(0f, 0f);
            sidebarRect.anchorMax = new Vector2(0f, 1f);
            sidebarRect.pivot = new Vector2(0f, 0.5f);
            sidebarRect.anchoredPosition = Vector2.zero;
            sidebarRect.sizeDelta = new Vector2(470f, 0f);

            // Right Edge Divider on Sidebar
            GameObject dividerObj = CreateUIObject("SidebarRightBorder", sidebarObj.transform);
            Image divImg = dividerObj.AddComponent<Image>();
            divImg.color = new Color(0.18f, 0.20f, 0.25f, 0.85f);
            RectTransform divRect = dividerObj.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(1f, 0f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(1f, 0.5f);
            divRect.anchoredPosition = Vector2.zero;
            divRect.sizeDelta = new Vector2(3f, 0f);

            // -------------------------------------------------------------
            // Logo / Header (Top of Sidebar)
            // -------------------------------------------------------------
            GameObject logoContainer = CreateUIObject("LogoContainer", sidebarObj.transform);
            RectTransform logoRect = logoContainer.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 1f);
            logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 1f);
            logoRect.anchoredPosition = new Vector2(0f, -60f);
            logoRect.sizeDelta = new Vector2(410f, 190f);

            // Top Flourish
            GameObject topFlourishObj = CreateUIObject("TopFlourishText", logoContainer.transform);
            TextMeshProUGUI topFlourish = topFlourishObj.AddComponent<TextMeshProUGUI>();
            topFlourish.text = "THE LAST";
            topFlourish.fontSize = 20;
            topFlourish.fontStyle = FontStyles.Bold;
            topFlourish.color = HighlightAccent;
            topFlourish.characterSpacing = 8;
            topFlourish.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) topFlourish.font = tekoFont;
            RectTransform tfRect = topFlourishObj.GetComponent<RectTransform>();
            tfRect.anchorMin = new Vector2(0f, 1f);
            tfRect.anchorMax = new Vector2(1f, 1f);
            tfRect.anchoredPosition = new Vector2(0f, -10f);
            tfRect.sizeDelta = new Vector2(410f, 32f);

            // Main Title "EMPIRE"
            GameObject mainTitleObj = CreateUIObject("MainTitleText", logoContainer.transform);
            TextMeshProUGUI titleText = mainTitleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "EMPIRE";
            titleText.fontSize = 78;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.characterSpacing = 4;
            titleText.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) titleText.font = tekoFont;
            RectTransform mtRect = mainTitleObj.GetComponent<RectTransform>();
            mtRect.anchorMin = new Vector2(0f, 1f);
            mtRect.anchorMax = new Vector2(1f, 1f);
            mtRect.anchoredPosition = new Vector2(0f, -50f);
            mtRect.sizeDelta = new Vector2(410f, 85f);

            // Crest Rune Flourish
            GameObject crestObj = CreateUIObject("CrestFlourish", logoContainer.transform);
            TextMeshProUGUI crestText = crestObj.AddComponent<TextMeshProUGUI>();
            crestText.text = "";
            crestText.fontSize = 16;
            crestText.color = new Color(PrimaryAccent.r, PrimaryAccent.g, PrimaryAccent.b, 0.85f);
            crestText.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) crestText.font = tekoFont;
            RectTransform cfRect = crestObj.GetComponent<RectTransform>();
            cfRect.anchorMin = new Vector2(0f, 1f);
            cfRect.anchorMax = new Vector2(1f, 1f);
            cfRect.anchoredPosition = new Vector2(0f, -135f);
            cfRect.sizeDelta = new Vector2(410f, 25f);

            // Subtitle
            GameObject subTitleObj = CreateUIObject("SubtitleText", logoContainer.transform);
            TextMeshProUGUI subText = subTitleObj.AddComponent<TextMeshProUGUI>();
            subText.text = "SURVIVAL ROGUELIKE // PROTOCOL ACTIVE";
            subText.fontSize = 12;
            subText.color = new Color(0.6f, 0.65f, 0.72f, 0.85f);
            subText.characterSpacing = 4;
            subText.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) subText.font = tekoFont;
            RectTransform stRect = subTitleObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 1f);
            stRect.anchorMax = new Vector2(1f, 1f);
            stRect.anchoredPosition = new Vector2(0f, -162f);
            stRect.sizeDelta = new Vector2(410f, 22f);

            // -------------------------------------------------------------
            // Menu Buttons Container (Middle of Sidebar)
            // -------------------------------------------------------------
            GameObject buttonsContainer = CreateUIObject("MenuButtonsList", sidebarObj.transform);
            RectTransform btnListRect = buttonsContainer.GetComponent<RectTransform>();
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

            // Highlighted Primary Button: NEW GAME
            Button newGameBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_NEW GAME", "NEW GAME", PrimaryAccent, true, tekoFont);
            Button continueBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_CONTINUE", "CONTINUE", SecondaryBtnColor, false, tekoFont);
            Button controlsBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_HOW TO PLAY", "HOW TO PLAY", SecondaryBtnColor, false, tekoFont);
            Button settingsBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_SETTINGS", "OPTIONS", SecondaryBtnColor, false, tekoFont);
            Button quitBtn = CreateSidebarButton(buttonsContainer.transform, "Btn_QUIT GAME", "EXIT", SecondaryBtnColor, false, tekoFont);

            // -------------------------------------------------------------
            // Bottom Social & Community Bar (Bottom of Sidebar)
            // -------------------------------------------------------------
            GameObject socialBar = CreateUIObject("BottomSocialBar", sidebarObj.transform);
            RectTransform sBarRect = socialBar.GetComponent<RectTransform>();
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

            // Discord Button
            CreateSocialButton(socialBar.transform, "Btn_Discord", "DISCORD", 70f, tekoFont);

            // Wishlist Button (Pill shape)
            CreateSocialButton(socialBar.transform, "Btn_Wishlist", "WISHLIST ON STEAM", 200f, tekoFont);

            // Twitter / Community Button
            CreateSocialButton(socialBar.transform, "Btn_Community", "COMMUNITY", 80f, tekoFont);

            // Version text beneath social bar
            GameObject verObj = CreateUIObject("VersionText", sidebarObj.transform);
            TextMeshProUGUI vText = verObj.AddComponent<TextMeshProUGUI>();
            vText.text = "THE LAST EMPIRE // v1.0.4 PROTOCOL";
            vText.fontSize = 11;
            vText.color = new Color(0.45f, 0.48f, 0.55f, 0.75f);
            vText.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) vText.font = tekoFont;
            RectTransform vRect = verObj.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.5f, 0f);
            vRect.anchorMax = new Vector2(0.5f, 0f);
            vRect.pivot = new Vector2(0.5f, 0f);
            vRect.anchoredPosition = new Vector2(0f, 10f);
            vRect.sizeDelta = new Vector2(370f, 16f);

            // -------------------------------------------------------------
            // Modals (Controls & Settings)
            // -------------------------------------------------------------
            Button closeControlsBtn;
            GameObject controlsModalObj = CreateBakeControlsModal(canvasObj.transform, tekoFont, out closeControlsBtn);
            controlsModalObj.SetActive(false);

            Button closeSettingsBtn;
            Slider masterSlider, bgmSlider, sfxSlider;
            Toggle fsToggle;
            GameObject settingsModalObj = CreateBakeSettingsModal(canvasObj.transform, tekoFont, out closeSettingsBtn, out masterSlider, out bgmSlider, out sfxSlider, out fsToggle);
            settingsModalObj.SetActive(false);

            // Fullscreen Scene Fade Curtain
            GameObject fadeCurtainObj = CreateUIObject("SceneFadeCurtain", canvasObj.transform);
            Image fadeImg = fadeCurtainObj.AddComponent<Image>();
            fadeImg.color = Color.black;
            CanvasGroup fadeGroup = fadeCurtainObj.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            SetFullStretch(fadeCurtainObj.GetComponent<RectTransform>());
            fadeCurtainObj.transform.SetAsLastSibling();

            // -------------------------------------------------------------
            // MainMenuManager Component Linking
            // -------------------------------------------------------------
            GameObject managerObj = new GameObject("MainMenuManager");
            MainMenuManager menuManager = managerObj.AddComponent<MainMenuManager>();
            menuManager.SetHierarchyReferences(
                canvas, canvasGroup,
                newGameBtn, continueBtn, controlsBtn, settingsBtn, quitBtn,
                controlsModalObj, settingsModalObj,
                titleText, closeControlsBtn, closeSettingsBtn,
                masterSlider, bgmSlider, sfxSlider, fsToggle,
                fadeCurtainObj, fadeGroup
            );

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            if (saved)
            {
                Debug.Log($"<color=#50FA7B>[MainMenuSceneCreator]</color> Successfully baked Left Sidebar Main Menu layout into: <b>{MainMenuScenePath}</b>");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.layer = 5; // UI
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button CreateSidebarButton(Transform parent, string goName, string label, Color bgColor, bool isPrimary, TMP_FontAsset font)
        {
            GameObject btnObj = CreateUIObject(goName, parent);

            LayoutElement layout = btnObj.AddComponent<LayoutElement>();
            float btnHeight = isPrimary ? 56f : 48f;
            layout.preferredHeight = btnHeight;
            layout.minHeight = btnHeight;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = bgColor;

            // Border
            GameObject borderObj = CreateUIObject("Border", btnObj.transform);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = isPrimary ? new Color(1f, 1f, 1f, 0.45f) : BorderColor;
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-1.5f, -1.5f);
            borderRect.offsetMax = new Vector2(1.5f, 1.5f);
            borderObj.transform.SetAsFirstSibling();

            // Label
            GameObject textObj = CreateUIObject("Label", btnObj.transform);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = isPrimary ? 21 : 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.characterSpacing = 2;
            tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            Button btn = btnObj.AddComponent<Button>();
            btnObj.AddComponent<MenuButtonHoverEffect>();

            return btn;
        }

        private static void CreateSocialButton(Transform parent, string name, string label, float width, TMP_FontAsset font)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 40f);

            Image bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.17f, 0.90f);

            GameObject borderObj = CreateUIObject("Border", btnObj.transform);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.35f, 0.42f, 0.50f);
            RectTransform bRect = borderObj.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(-1f, -1f);
            bRect.offsetMax = new Vector2(1f, 1f);
            borderObj.transform.SetAsFirstSibling();

            GameObject textObj = CreateUIObject("Label", btnObj.transform);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.85f, 0.88f, 0.95f, 0.95f);
            tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;
            SetFullStretch(textObj.GetComponent<RectTransform>());

            btnObj.AddComponent<Button>();
            btnObj.AddComponent<MenuButtonHoverEffect>();
        }

        private static GameObject CreateBakeControlsModal(Transform canvasTransform, TMP_FontAsset font, out Button closeBtn)
        {
            GameObject modalRoot = CreateUIObject("ControlsModal", canvasTransform);
            SetFullStretch(modalRoot.GetComponent<RectTransform>());

            Image backdrop = modalRoot.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.85f);

            GameObject panel = CreateUIObject("ModalPanel", modalRoot.transform);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = SidebarBgColor;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(760f, 620f);

            GameObject border = CreateUIObject("PanelBorder", panel.transform);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(PrimaryAccent.r, PrimaryAccent.g, PrimaryAccent.b, 0.45f);
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            border.transform.SetAsFirstSibling();

            GameObject headerObj = CreateUIObject("ModalTitle", panel.transform);
            TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "SURVIVAL MANUAL // CONTROLS & PROTOCOLS";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = HighlightAccent;
            headerText.alignment = TextAlignmentOptions.Center;
            if (font != null) headerText.font = font;
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -24f);
            hRect.sizeDelta = new Vector2(700f, 40f);

            GameObject contentObj = CreateUIObject("ControlsContent", panel.transform);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(40f, 90f);
            contentRect.offsetMax = new Vector2(-40f, -75f);

            VerticalLayoutGroup vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 10f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;

            AddBakeControlRow(contentObj.transform, "W, A, S, D", "Movement // บังคับทิศทางเดิน", font);
            AddBakeControlRow(contentObj.transform, "Mouse Aim + Left Click", "Aim & Shoot / Attack // เล็งและยิง/โจมตี", font);
            AddBakeControlRow(contentObj.transform, "1 - 9 / Mouse Scroll", "Quick Select Ranged Weapons // สลับปืน", font);
            AddBakeControlRow(contentObj.transform, "X Key", "Cycle Melee Weapons // สลับอาวุธประชิด (Katana, Axe, Hammer)", font);
            AddBakeControlRow(contentObj.transform, "R Key", "Reload Weapon Magazine // บรรจุกระสุน", font);
            AddBakeControlRow(contentObj.transform, "I / ESC Key", "Inventory & Pause // เปิดกระเป๋าเป้ / หยุดเกม", font);
            AddBakeControlRow(contentObj.transform, "Screen Edge Portals", "Warp to Adjacent Biomes // เดินเข้าประตูเพื่อข้ามโซน", font);
            AddBakeControlRow(contentObj.transform, "Day / Night Cycle", "Day = 30s | Night = High Alien & Zombie aggro!", font);

            GameObject closeBtnObj = CreateUIObject("CloseButton", panel.transform);
            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 22f);
            closeRect.sizeDelta = new Vector2(220f, 48f);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.color = SecondaryBtnColor;

            GameObject closeTextObj = CreateUIObject("Label", closeBtnObj.transform);
            TextMeshProUGUI closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "CLOSE // ปิด";
            closeText.fontSize = 20;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;
            if (font != null) closeText.font = font;
            SetFullStretch(closeTextObj.GetComponent<RectTransform>());

            closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtnObj.AddComponent<MenuButtonHoverEffect>();

            return modalRoot;
        }

        private static void AddBakeControlRow(Transform parent, string keyBadge, string description, TMP_FontAsset font)
        {
            GameObject rowObj = CreateUIObject("ControlRow", parent);

            LayoutElement layout = rowObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;

            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.04f);

            GameObject badgeObj = CreateUIObject("KeyBadge", rowObj.transform);
            Image badgeBg = badgeObj.AddComponent<Image>();
            badgeBg.color = new Color(PrimaryAccent.r, PrimaryAccent.g, PrimaryAccent.b, 0.22f);
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(10f, 0f);
            badgeRect.sizeDelta = new Vector2(240f, 32f);

            GameObject badgeTextObj = CreateUIObject("KeyText", badgeObj.transform);
            TextMeshProUGUI bText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            bText.text = keyBadge;
            bText.fontSize = 16;
            bText.fontStyle = FontStyles.Bold;
            bText.color = HighlightAccent;
            bText.alignment = TextAlignmentOptions.Center;
            if (font != null) bText.font = font;
            SetFullStretch(badgeTextObj.GetComponent<RectTransform>());

            GameObject descObj = CreateUIObject("DescText", rowObj.transform);
            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = description;
            descText.fontSize = 16;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
            descText.alignment = TextAlignmentOptions.Left;
            if (font != null) descText.font = font;
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.offsetMin = new Vector2(265f, 0f);
            descRect.offsetMax = new Vector2(-10f, 0f);
        }

        private static GameObject CreateBakeSettingsModal(Transform canvasTransform, TMP_FontAsset font,
            out Button closeBtn, out Slider masterSlider, out Slider bgmSlider, out Slider sfxSlider, out Toggle fsToggle)
        {
            GameObject modalRoot = CreateUIObject("SettingsModal", canvasTransform);
            SetFullStretch(modalRoot.GetComponent<RectTransform>());

            Image backdrop = modalRoot.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.85f);

            GameObject panel = CreateUIObject("ModalPanel", modalRoot.transform);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = SidebarBgColor;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(680f, 520f);

            GameObject border = CreateUIObject("PanelBorder", panel.transform);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(PrimaryAccent.r, PrimaryAccent.g, PrimaryAccent.b, 0.45f);
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            border.transform.SetAsFirstSibling();

            GameObject headerObj = CreateUIObject("ModalTitle", panel.transform);
            TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "SYSTEM CONFIGURATION // SETTINGS";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = HighlightAccent;
            headerText.alignment = TextAlignmentOptions.Center;
            if (font != null) headerText.font = font;
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -24f);
            hRect.sizeDelta = new Vector2(600f, 40f);

            GameObject contentObj = CreateUIObject("SettingsContent", panel.transform);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(40f, 90f);
            contentRect.offsetMax = new Vector2(-40f, -75f);

            VerticalLayoutGroup vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 18f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;

            masterSlider = CreateBakeSliderRow(contentObj.transform, "Master Audio Volume // ระดับเสียงหลัก", 1.0f, font);
            bgmSlider = CreateBakeSliderRow(contentObj.transform, "BGM Music Volume // เสียงดนตรีประกอบ", 0.85f, font);
            sfxSlider = CreateBakeSliderRow(contentObj.transform, "SFX Sound Volume // เสียงเอฟเฟกต์อาวุธ", 1.0f, font);
            fsToggle = CreateBakeToggleRow(contentObj.transform, "Fullscreen Mode // โหมดเต็มจอ", true, font);

            GameObject closeBtnObj = CreateUIObject("SaveAndBackBtn", panel.transform);
            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 22f);
            closeRect.sizeDelta = new Vector2(240f, 48f);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.color = SecondaryBtnColor;

            GameObject closeTextObj = CreateUIObject("Label", closeBtnObj.transform);
            TextMeshProUGUI closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "SAVE & BACK // บันทึกและย้อนกลับ";
            closeText.fontSize = 18;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;
            if (font != null) closeText.font = font;
            SetFullStretch(closeTextObj.GetComponent<RectTransform>());

            closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtnObj.AddComponent<MenuButtonHoverEffect>();

            return modalRoot;
        }

        private static Slider CreateBakeSliderRow(Transform parent, string title, float initialValue, TMP_FontAsset font)
        {
            GameObject row = CreateUIObject("SliderRow", parent);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 56f;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.03f);

            GameObject labelObj = CreateUIObject("Label", row.transform);
            TextMeshProUGUI lbl = labelObj.AddComponent<TextMeshProUGUI>();
            lbl.text = title;
            lbl.fontSize = 16;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Left;
            if (font != null) lbl.font = font;
            RectTransform lRect = labelObj.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 0.5f);
            lRect.anchorMax = new Vector2(0f, 0.5f);
            lRect.pivot = new Vector2(0f, 0.5f);
            lRect.anchoredPosition = new Vector2(16f, 0f);
            lRect.sizeDelta = new Vector2(300f, 30f);

            GameObject valTextObj = CreateUIObject("ValueText", row.transform);
            TextMeshProUGUI valText = valTextObj.AddComponent<TextMeshProUGUI>();
            valText.text = $"{Mathf.RoundToInt(initialValue * 100)}%";
            valText.fontSize = 16;
            valText.color = HighlightAccent;
            valText.alignment = TextAlignmentOptions.Right;
            if (font != null) valText.font = font;
            RectTransform vRect = valTextObj.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(1f, 0.5f);
            vRect.anchorMax = new Vector2(1f, 0.5f);
            vRect.pivot = new Vector2(1f, 0.5f);
            vRect.anchoredPosition = new Vector2(-16f, 0f);
            vRect.sizeDelta = new Vector2(60f, 30f);

            GameObject sliderObj = CreateUIObject("Slider", row.transform);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(1f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(1f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(-85f, 0f);
            sliderRect.sizeDelta = new Vector2(170f, 20f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;

            GameObject sBg = CreateUIObject("Background", sliderObj.transform);
            Image sBgImg = sBg.AddComponent<Image>();
            sBgImg.color = new Color(0.18f, 0.2f, 0.26f, 1f);
            RectTransform sBgRect = sBg.GetComponent<RectTransform>();
            sBgRect.anchorMin = new Vector2(0f, 0.25f);
            sBgRect.anchorMax = new Vector2(1f, 0.75f);
            sBgRect.offsetMin = Vector2.zero;
            sBgRect.offsetMax = Vector2.zero;

            GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            RectTransform fAreaRect = fillArea.GetComponent<RectTransform>();
            fAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fAreaRect.offsetMin = Vector2.zero;
            fAreaRect.offsetMax = Vector2.zero;

            GameObject fill = CreateUIObject("Fill", fillArea.transform);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = PrimaryAccent;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;

            GameObject handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform);
            RectTransform hAreaRect = handleArea.GetComponent<RectTransform>();
            SetFullStretch(hAreaRect);

            GameObject handle = CreateUIObject("Handle", handleArea.transform);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18f, 24f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private static Toggle CreateBakeToggleRow(Transform parent, string title, bool initialValue, TMP_FontAsset font)
        {
            GameObject row = CreateUIObject("ToggleRow", parent);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 50f;

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.03f);

            GameObject labelObj = CreateUIObject("Label", row.transform);
            TextMeshProUGUI lbl = labelObj.AddComponent<TextMeshProUGUI>();
            lbl.text = title;
            lbl.fontSize = 16;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Left;
            if (font != null) lbl.font = font;
            RectTransform lRect = labelObj.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 0.5f);
            lRect.anchorMax = new Vector2(0f, 0.5f);
            lRect.pivot = new Vector2(0f, 0.5f);
            lRect.anchoredPosition = new Vector2(16f, 0f);
            lRect.sizeDelta = new Vector2(300f, 30f);

            GameObject toggleObj = CreateUIObject("Toggle", row.transform);
            RectTransform tRect = toggleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(1f, 0.5f);
            tRect.anchorMax = new Vector2(1f, 0.5f);
            tRect.pivot = new Vector2(1f, 0.5f);
            tRect.anchoredPosition = new Vector2(-20f, 0f);
            tRect.sizeDelta = new Vector2(32f, 32f);

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = initialValue;

            Image tBg = toggleObj.AddComponent<Image>();
            tBg.color = new Color(0.18f, 0.2f, 0.26f, 1f);

            GameObject checkmarkObj = CreateUIObject("Checkmark", toggleObj.transform);
            Image checkmarkImg = checkmarkObj.AddComponent<Image>();
            checkmarkImg.color = PrimaryAccent;
            RectTransform cRect = checkmarkObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.2f, 0.2f);
            cRect.anchorMax = new Vector2(0.8f, 0.8f);
            cRect.offsetMin = Vector2.zero;
            cRect.offsetMax = Vector2.zero;

            toggle.graphic = checkmarkImg;
            return toggle;
        }

        [MenuItem("The Last Empire/Ensure Main Menu in Build Settings", priority = 11)]
        public static void EnsureMainMenuRegisteredInBuildSettings()
        {
            if (!File.Exists(MainMenuScenePath)) return;

            var currentScenes = EditorBuildSettings.scenes;
            bool mainMenuFound = false;
            int mainMenuIndex = -1;

            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path.Equals(MainMenuScenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    mainMenuFound = true;
                    mainMenuIndex = i;
                    break;
                }
            }

            if (mainMenuFound && mainMenuIndex == 0 && currentScenes[0].enabled)
            {
                return;
            }

            var newSceneList = new List<EditorBuildSettingsScene>();
            newSceneList.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));

            foreach (var scene in currentScenes)
            {
                if (scene.path.Equals(MainMenuScenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (scene.path.Equals(GameplayScenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    newSceneList.Add(new EditorBuildSettingsScene(scene.path, true));
                }
                else
                {
                    newSceneList.Add(new EditorBuildSettingsScene(scene.path, scene.enabled));
                }
            }

            EditorBuildSettings.scenes = newSceneList.ToArray();
            Debug.Log($"<color=#66D9EF>[MainMenuSceneCreator]</color> Build Settings updated! MainMenu is set to Index 0.");
        }
    }
}
