using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// SettingsUIBuilder — Editor tool for baking and configuring all 5 Settings Panels
    /// directly in the Unity Scene hierarchy for SettingManager.
    /// หมวดหมู่ทั้ง 5: General, Control, Sound, Graphic, Accessory
    /// </summary>
    [CustomEditor(typeof(SettingManager))]
    public class SettingsUIBuilder : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("🛠️ Bake / Rebuild All 5 Settings Panels In Scene", GUILayout.Height(36)))
            {
                SettingManager sm = (SettingManager)target;
                BakePanelsForManager(sm);
            }
            GUI.backgroundColor = Color.white;
        }

        [MenuItem("Tools/The Last Empire/Bake All Setting Panels", false, 100)]
        public static void BakeAllSettingPanelsMenu()
        {
            SettingManager sm = UnityEngine.Object.FindAnyObjectByType<SettingManager>();
            if (sm == null)
            {
                EditorUtility.DisplayDialog("Bake Setting Panels", "Could not find SettingManager in the active scene!", "OK");
                return;
            }

            BakePanelsForManager(sm);
            EditorUtility.DisplayDialog("Bake Setting Panels", "Successfully baked and wired all 5 Settings Panels into the scene!", "OK");
        }

        public static void BakePanelsForManager(SettingManager sm)
        {
            if (sm == null) return;
            Undo.RegisterFullObjectHierarchyUndo(sm.gameObject, "Bake All Settings Panels");

            SerializedObject so = new SerializedObject(sm);

            // 1. Locate modal root
            Transform modalRootT = sm.transform.Find("SettingPanel");
            if (modalRootT == null)
            {
                modalRootT = sm.transform;
            }
            so.FindProperty("modalRoot").objectReferenceValue = modalRootT.gameObject;

            // 2. Wire Tab Buttons & Action Buttons
            Transform catagoryT = modalRootT.Find("Catagory");
            if (catagoryT != null)
            {
                SetPropertyIfNull(so, "generalTabBtn", FindButton(catagoryT, "General"));
                SetPropertyIfNull(so, "controlTabBtn", FindButton(catagoryT, "Control"));
                SetPropertyIfNull(so, "soundTabBtn", FindButton(catagoryT, "Sound"));
                SetPropertyIfNull(so, "graphicTabBtn", FindButton(catagoryT, "Graphic"));
                SetPropertyIfNull(so, "accessoryTabBtn", FindButton(catagoryT, "Accesory", "Accessory"));
                SetPropertyIfNull(so, "exitButton", FindButton(catagoryT, "Exit"));
            }

            Transform settingT = modalRootT.Find("Setting");
            if (settingT != null)
            {
                SetPropertyIfNull(so, "defaultButton", FindButton(settingT, "DefaultBtn", "Default"));
                SetPropertyIfNull(so, "applyButton", FindButton(settingT, "SaveAndBackBtn", "Apply", "Save"));
            }

            // 3. Locate Content Container & Templates
            Transform contentContainer = modalRootT.Find("Setting/SettingsContent");
            if (contentContainer == null)
            {
                contentContainer = modalRootT.Find("Setting");
            }
            if (contentContainer == null)
            {
                Debug.LogError("[SettingsUIBuilder] Could not find SettingsContent container!");
                so.ApplyModifiedProperties();
                return;
            }

            // Find Slider & Toggle Row Templates from loose children
            Transform sliderTemplate = null;
            Transform toggleTemplate = null;

            foreach (Transform child in contentContainer)
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

            // 4. Create Subpanels
            GameObject panelGeneral   = GetOrCreateSubPanel(contentContainer, "Panel_General");
            GameObject panelControl   = GetOrCreateSubPanel(contentContainer, "Panel_Control");
            GameObject panelSound     = GetOrCreateSubPanel(contentContainer, "Panel_Sound");
            GameObject panelGraphic   = GetOrCreateSubPanel(contentContainer, "Panel_Graphic");
            GameObject panelAccessory = GetOrCreateSubPanel(contentContainer, "Panel_Accessory");

            so.FindProperty("generalPanel").objectReferenceValue = panelGeneral;
            so.FindProperty("controlPanel").objectReferenceValue = panelControl;
            so.FindProperty("soundPanel").objectReferenceValue = panelSound;
            so.FindProperty("graphicPanel").objectReferenceValue = panelGraphic;
            so.FindProperty("accessoryPanel").objectReferenceValue = panelAccessory;

            // 5. Populate Each Panel
            // A. General Panel: Fullscreen, Screen Shake, Damage Numbers, Auto Reload
            Toggle fsToggle = GetOrCreateToggleRow(panelGeneral.transform, toggleTemplate, "Row_FullscreenMode", "Fullscreen Mode", sm.IsFullscreen);
            Toggle shakeToggle = GetOrCreateToggleRow(panelGeneral.transform, toggleTemplate, "Row_ScreenShake", "Screen Shake Effect", sm.ScreenShake);
            Toggle dmgToggle = GetOrCreateToggleRow(panelGeneral.transform, toggleTemplate, "Row_DamageNumbers", "Floating Damage Numbers", sm.DamageNumbers);
            Toggle reloadToggle = GetOrCreateToggleRow(panelGeneral.transform, toggleTemplate, "Row_AutoReload", "Auto Reload Empty Weapon", sm.AutoReload);

            so.FindProperty("fullscreenToggle").objectReferenceValue = fsToggle;
            so.FindProperty("screenShakeToggle").objectReferenceValue = shakeToggle;
            so.FindProperty("damageNumbersToggle").objectReferenceValue = dmgToggle;
            so.FindProperty("autoReloadToggle").objectReferenceValue = reloadToggle;

            // B. Control Panel: Mouse Sensitivity, Invert Y, Keybind Card
            var (sensSlider, sensText) = GetOrCreateSliderRow(panelControl.transform, sliderTemplate, "Row_MouseSensitivity", "Mouse Sensitivity", sm.MouseSensitivity / 3.0f, $"{sm.MouseSensitivity:F1}x");
            Toggle invertYToggle = GetOrCreateToggleRow(panelControl.transform, toggleTemplate, "Row_InvertMouseY", "Invert Mouse Y-Axis", sm.InvertMouseY);
            GetOrCreateKeybindCard(panelControl.transform);

            so.FindProperty("mouseSensitivitySlider").objectReferenceValue = sensSlider;
            so.FindProperty("mouseSensitivityValueText").objectReferenceValue = sensText;
            so.FindProperty("invertMouseYToggle").objectReferenceValue = invertYToggle;

            // C. Sound Panel: Master, BGM, SFX, UI Sliders, Mute on focus lost
            var (masterSlider, masterText) = GetOrCreateSliderRow(panelSound.transform, sliderTemplate, "Row_MasterVolume", "Master Audio Volume", sm.MasterVolume, $"{Mathf.RoundToInt(sm.MasterVolume * 100)}%");
            var (bgmSlider, bgmText) = GetOrCreateSliderRow(panelSound.transform, sliderTemplate, "Row_BGMVolume", "BGM Music Volume", sm.BgmVolume, $"{Mathf.RoundToInt(sm.BgmVolume * 100)}%");
            var (sfxSlider, sfxText) = GetOrCreateSliderRow(panelSound.transform, sliderTemplate, "Row_SFXVolume", "SFX Sound Volume", sm.SfxVolume, $"{Mathf.RoundToInt(sm.SfxVolume * 100)}%");
            var (uiSlider, uiText) = GetOrCreateSliderRow(panelSound.transform, sliderTemplate, "Row_UIVolume", "UI Sound Volume", sm.UiVolume, $"{Mathf.RoundToInt(sm.UiVolume * 100)}%");
            Toggle muteToggle = GetOrCreateToggleRow(panelSound.transform, toggleTemplate, "Row_MuteWhenUnfocused", "Mute When Unfocused", sm.MuteOnFocusLost);

            so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
            so.FindProperty("masterVolumeValueText").objectReferenceValue = masterText;
            so.FindProperty("bgmVolumeSlider").objectReferenceValue = bgmSlider;
            so.FindProperty("bgmVolumeValueText").objectReferenceValue = bgmText;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("sfxVolumeValueText").objectReferenceValue = sfxText;
            so.FindProperty("uiVolumeSlider").objectReferenceValue = uiSlider;
            so.FindProperty("uiVolumeValueText").objectReferenceValue = uiText;
            so.FindProperty("muteOnFocusLostToggle").objectReferenceValue = muteToggle;

            // D. Graphic Panel: V-Sync, High Dynamic Shadows, Anti-Aliasing
            Toggle vSyncToggle = GetOrCreateToggleRow(panelGraphic.transform, toggleTemplate, "Row_VSync", "V-Sync (Vertical Sync)", sm.VSync);
            GetOrCreateToggleRow(panelGraphic.transform, toggleTemplate, "Row_Shadows", "High Dynamic Shadows", true);
            GetOrCreateToggleRow(panelGraphic.transform, toggleTemplate, "Row_AntiAliasing", "Anti-Aliasing Smoothing", true);

            so.FindProperty("vSyncToggle").objectReferenceValue = vSyncToggle;

            // E. Accessory Panel: Minimap, Health Bars, Blood & Gore, Vignette
            Toggle minimapToggle = GetOrCreateToggleRow(panelAccessory.transform, toggleTemplate, "Row_Minimap", "Show Minimap Radar", sm.ShowMinimap);
            Toggle hpBarsToggle = GetOrCreateToggleRow(panelAccessory.transform, toggleTemplate, "Row_EnemyHealthBars", "Show Enemy Health Bars", sm.ShowEnemyHealthBars);
            Toggle bloodToggle = GetOrCreateToggleRow(panelAccessory.transform, toggleTemplate, "Row_BloodGore", "Blood & Gore VFX", sm.BloodGore);
            Toggle vigToggle = GetOrCreateToggleRow(panelAccessory.transform, toggleTemplate, "Row_LowHealthVignette", "Low Health Red Vignette", sm.LowHealthVignette);

            so.FindProperty("showMinimapToggle").objectReferenceValue = minimapToggle;
            so.FindProperty("showEnemyHealthBarsToggle").objectReferenceValue = hpBarsToggle;
            so.FindProperty("bloodGoreToggle").objectReferenceValue = bloodToggle;
            so.FindProperty("lowHealthVignetteToggle").objectReferenceValue = vigToggle;

            // 6. Hide template / loose items
            foreach (Transform child in contentContainer)
            {
                if (!child.name.StartsWith("Panel_") && child.name != "BG" && child.name != "PanelBorder" && child.name != "DefaultBtn" && child.name != "SaveAndBackBtn")
                {
                    child.gameObject.SetActive(false);
                }
            }

            // Set active panel: General active, others inactive
            panelGeneral.SetActive(true);
            panelControl.SetActive(false);
            panelSound.SetActive(false);
            panelGraphic.SetActive(false);
            panelAccessory.SetActive(false);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sm);
            EditorSceneManager.MarkSceneDirty(sm.gameObject.scene);

            Debug.Log($"[SettingsUIBuilder] Successfully baked 5 Settings Panels for '{sm.gameObject.name}' in scene '{sm.gameObject.scene.name}'.");
        }

        private static GameObject GetOrCreateSubPanel(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject panel = new GameObject(name);
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

            return panel;
        }

        private static (Slider slider, TMP_Text text) GetOrCreateSliderRow(Transform parent, Transform template, string rowName, string title, float initialVal, string displayText)
        {
            Transform existing = parent.Find(rowName);
            GameObject row = existing != null ? existing.gameObject : (template != null ? Object.Instantiate(template.gameObject, parent) : null);
            if (row == null) return (null, null);

            row.name = rowName;
            row.SetActive(true);

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0) texts[0].text = title;
            TMP_Text outText = null;
            if (texts.Length > 1)
            {
                outText = texts[1];
                outText.text = displayText;
            }

            Slider outSlider = row.GetComponentInChildren<Slider>(true);
            if (outSlider != null)
            {
                outSlider.SetValueWithoutNotify(initialVal);
            }

            return (outSlider, outText);
        }

        private static Toggle GetOrCreateToggleRow(Transform parent, Transform template, string rowName, string title, bool initialVal)
        {
            Transform existing = parent.Find(rowName);
            GameObject row = existing != null ? existing.gameObject : (template != null ? Object.Instantiate(template.gameObject, parent) : null);
            if (row == null) return null;

            row.name = rowName;
            row.SetActive(true);

            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = title;

            Toggle outToggle = row.GetComponentInChildren<Toggle>(true);
            if (outToggle != null)
            {
                outToggle.SetIsOnWithoutNotify(initialVal);
            }

            return outToggle;
        }

        private static void GetOrCreateKeybindCard(Transform parent)
        {
            Transform existing = parent.Find("KeybindReferenceCard");
            if (existing != null) return;

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

        private static Button FindButton(Transform root, params string[] searchNames)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string bName = btn.gameObject.name.ToLowerInvariant();
                TMP_Text txt = btn.GetComponentInChildren<TMP_Text>(true);
                string tName = txt != null ? txt.text.ToLowerInvariant() : "";

                foreach (string sn in searchNames)
                {
                    string target = sn.ToLowerInvariant();
                    if (bName.Contains(target) || tName.Contains(target))
                    {
                        return btn;
                    }
                }
            }
            return null;
        }

        private static void SetPropertyIfNull(SerializedObject so, string propertyName, Object val)
        {
            if (val == null) return;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = val;
            }
        }
    }
}
