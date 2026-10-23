using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class CharacterSelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string PrefabDir = "Assets/Resources/Prefabs/UI";
        private const string PrefabPath = "Assets/Resources/Prefabs/UI/CharacterSelectModal.prefab";

        static CharacterSelectSceneBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNeeded;
        }

        private static void AutoBuildIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath) return;

            MainMenuManager menuManager = Object.FindAnyObjectByType<MainMenuManager>();
            if (menuManager == null) return;

            SerializedObject menuSo = new SerializedObject(menuManager);
            SerializedProperty charModalProp = menuSo.FindProperty("characterSelectModalObj");
            if (charModalProp != null && charModalProp.objectReferenceValue != null) return;

            // Automatically instantiate modal into scene if not already bound
            BuildInScene();
        }

        [MenuItem("Tools/The Last Empire/Instantiate Character Select in Scene", false, 10)]
        public static void BuildInScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                activeScene = EditorSceneManager.OpenScene(ScenePath);
            }

            MainMenuManager menuManager = Object.FindAnyObjectByType<MainMenuManager>();
            if (menuManager == null)
            {
                Debug.LogError("[CharacterSelectSceneBuilder] Could not find MainMenuManager in MainMenu.unity!");
                return;
            }

            SerializedObject menuSo = new SerializedObject(menuManager);
            SerializedProperty canvasProp = menuSo.FindProperty("menuCanvas");
            Canvas canvas = canvasProp != null ? canvasProp.objectReferenceValue as Canvas : null;
            if (canvas == null)
            {
                canvas = Object.FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogError("[CharacterSelectSceneBuilder] No Canvas found in MainMenu.unity!");
                return;
            }

            // Remove existing modal in scene if any, while preserving user portrait artwork
            Transform existing = canvas.transform.Find("CharacterSelectModal");
            Sprite existingPortraitSprite = null;
            if (existing != null)
            {
                Transform existingMc = existing.Find("ModalPanel/ModalCharacter") ?? existing.Find("ModalCharacter");
                if (existingMc != null)
                {
                    Image mcImg = existingMc.GetComponent<Image>();
                    if (mcImg != null && mcImg.sprite != null)
                    {
                        existingPortraitSprite = mcImg.sprite;
                    }
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // Load Teko Font if present
            TMP_FontAsset tekoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Teko-VariableFont_wght SDF.asset");

            Color primaryAccent = new Color(0.66f, 0.33f, 0.97f, 1f);   // Arcane Purple
            Color secondaryAccent = new Color(0.75f, 0.52f, 0.99f, 1f); // Lilac
            Color panelBgColor = new Color(0.06f, 0.07f, 0.10f, 0.98f);  // Dark Obsidian
            Color buttonBgColor = new Color(0.12f, 0.14f, 0.18f, 0.85f); // Slate Button

            // 1. Root Modal Object
            GameObject modalRoot = new GameObject("CharacterSelectModal", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(modalRoot, "Create CharacterSelectModal");
            modalRoot.transform.SetParent(canvas.transform, false);

            RectTransform modalRect = modalRoot.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            Image backdrop = modalRoot.GetComponent<Image>();
            backdrop.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

            // 2. Outer Border Frame (1184 x 704)
            GameObject borderFrame = new GameObject("ModalPanelBorder", typeof(RectTransform), typeof(Image));
            borderFrame.transform.SetParent(modalRoot.transform, false);
            Image borderImg = borderFrame.GetComponent<Image>();
            borderImg.color = new Color(primaryAccent.r, primaryAccent.g, primaryAccent.b, 0.85f);
            RectTransform borderRect = borderFrame.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(1184f, 704f);

            // 3. Center Panel (1180 x 700)
            GameObject panel = new GameObject("ModalPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(modalRoot.transform, false);
            Image panelImg = panel.GetComponent<Image>();
            panelImg.color = panelBgColor;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1180f, 700f);

            // Header Title
            GameObject headerObj = new GameObject("ModalTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(panel.transform, false);
            TMP_Text headerText = headerObj.GetComponent<TextMeshProUGUI>();
            headerText.text = "OPERATIVE SELECTION // SURVIVOR DOSSIER";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = secondaryAccent;
            headerText.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) headerText.font = tekoFont;
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.5f, 1f);
            hRect.anchorMax = new Vector2(0.5f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -16f);
            hRect.sizeDelta = new Vector2(1100f, 32f);

            // Header Subtitle
            GameObject subHeaderObj = new GameObject("ModalSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subHeaderObj.transform.SetParent(panel.transform, false);
            TMP_Text subHeaderText = subHeaderObj.GetComponent<TextMeshProUGUI>();
            subHeaderText.text = "5 OPERATIVES - 5 AMNESIA PERSPECTIVES OF THE FALLEN EMPIRE - SIGNATURE SHIFT SKILLS";
            subHeaderText.fontSize = 12;
            subHeaderText.color = new Color(0.7f, 0.75f, 0.85f, 0.75f);
            subHeaderText.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) subHeaderText.font = tekoFont;
            RectTransform shRect = subHeaderObj.GetComponent<RectTransform>();
            shRect.anchorMin = new Vector2(0.5f, 1f);
            shRect.anchorMax = new Vector2(0.5f, 1f);
            shRect.pivot = new Vector2(0.5f, 1f);
            shRect.anchoredPosition = new Vector2(0f, -48f);
            shRect.sizeDelta = new Vector2(1100f, 20f);

            // Operative Character Illustration (ModalCharacter)
            GameObject charObj = new GameObject("ModalCharacter", typeof(RectTransform), typeof(Image));
            charObj.transform.SetParent(panel.transform, false);
            RectTransform mcRect = charObj.GetComponent<RectTransform>();
            mcRect.anchorMin = new Vector2(0.48f, 0f);
            mcRect.anchorMax = new Vector2(1f, 1f);
            mcRect.pivot = new Vector2(1f, 0.5f);
            mcRect.anchoredPosition = new Vector2(-15f, 0f);
            mcRect.sizeDelta = new Vector2(0f, -40f);

            Image charImg = charObj.GetComponent<Image>();
            charImg.preserveAspect = true;
            Sprite vanguardDefaultSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Image/Picture/ChatGPT Image Oct 23, 2026, 10_07_54 PM.png");
            if (existingPortraitSprite != null)
            {
                charImg.sprite = existingPortraitSprite;
            }
            else if (vanguardDefaultSpr != null)
            {
                charImg.sprite = vanguardDefaultSpr;
            }

            // Content Area Container
            GameObject contentObj = new GameObject("ContentSplitArea", typeof(RectTransform));
            contentObj.transform.SetParent(panel.transform, false);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(25f, 75f);
            contentRect.offsetMax = new Vector2(-25f, -75f);

            // Left Roster Column (320px)
            GameObject rosterCol = new GameObject("RosterColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rosterCol.transform.SetParent(contentObj.transform, false);
            RectTransform rColRect = rosterCol.GetComponent<RectTransform>();
            rColRect.anchorMin = new Vector2(0f, 0f);
            rColRect.anchorMax = new Vector2(0f, 1f);
            rColRect.pivot = new Vector2(0f, 0.5f);
            rColRect.anchoredPosition = Vector2.zero;
            rColRect.sizeDelta = new Vector2(320f, 0f);

            VerticalLayoutGroup rLayout = rosterCol.GetComponent<VerticalLayoutGroup>();
            rLayout.spacing = 10f;
            rLayout.childControlWidth = true;
            rLayout.childControlHeight = true;
            rLayout.childForceExpandWidth = true;
            rLayout.childForceExpandHeight = false;

            // Right Dossier Column (Remaining space: ~785px)
            GameObject dossierCol = new GameObject("DossierColumn", typeof(RectTransform), typeof(Image));
            dossierCol.transform.SetParent(contentObj.transform, false);
            RectTransform dColRect = dossierCol.GetComponent<RectTransform>();
            dColRect.anchorMin = new Vector2(0f, 0f);
            dColRect.anchorMax = new Vector2(1f, 1f);
            dColRect.pivot = new Vector2(0.5f, 0.5f);
            dColRect.offsetMin = new Vector2(340f, 0f);
            dColRect.offsetMax = Vector2.zero;

            Image dossierBg = dossierCol.GetComponent<Image>();
            dossierBg.color = new Color(0.04f, 0.05f, 0.08f, 0.90f);

            // Dossier Header (Name & Role)
            GameObject dHeader = new GameObject("DossierHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            dHeader.transform.SetParent(dossierCol.transform, false);
            TMP_Text dNameText = dHeader.GetComponent<TextMeshProUGUI>();
            dNameText.fontSize = 26;
            dNameText.fontStyle = FontStyles.Bold;
            dNameText.color = Color.white;
            if (tekoFont != null) dNameText.font = tekoFont;
            RectTransform dhRect = dHeader.GetComponent<RectTransform>();
            dhRect.anchorMin = new Vector2(0f, 1f);
            dhRect.anchorMax = new Vector2(1f, 1f);
            dhRect.pivot = new Vector2(0f, 1f);
            dhRect.anchoredPosition = new Vector2(20f, -14f);
            dhRect.sizeDelta = new Vector2(-40f, 32f);

            GameObject dRoleObj = new GameObject("DossierRole", typeof(RectTransform), typeof(TextMeshProUGUI));
            dRoleObj.transform.SetParent(dossierCol.transform, false);
            TMP_Text dRoleText = dRoleObj.GetComponent<TextMeshProUGUI>();
            dRoleText.fontSize = 14;
            dRoleText.fontStyle = FontStyles.Bold;
            dRoleText.color = secondaryAccent;
            if (tekoFont != null) dRoleText.font = tekoFont;
            RectTransform drRect = dRoleObj.GetComponent<RectTransform>();
            drRect.anchorMin = new Vector2(0f, 1f);
            drRect.anchorMax = new Vector2(1f, 1f);
            drRect.pivot = new Vector2(0f, 1f);
            drRect.anchoredPosition = new Vector2(20f, -44f);
            drRect.sizeDelta = new Vector2(-40f, 22f);

            // Amnesia Clue Box
            GameObject clueBox = new GameObject("AmnesiaClueBox", typeof(RectTransform), typeof(Image));
            clueBox.transform.SetParent(dossierCol.transform, false);
            Image clueBoxBg = clueBox.GetComponent<Image>();
            clueBoxBg.color = new Color(0.14f, 0.10f, 0.05f, 0.65f);
            RectTransform cbRect = clueBox.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0f, 1f);
            cbRect.anchorMax = new Vector2(1f, 1f);
            cbRect.pivot = new Vector2(0f, 1f);
            cbRect.anchoredPosition = new Vector2(20f, -74f);
            cbRect.sizeDelta = new Vector2(-40f, 76f);

            GameObject clueAccent = new GameObject("ClueAccentStripe", typeof(RectTransform), typeof(Image));
            clueAccent.transform.SetParent(clueBox.transform, false);
            Image caImg = clueAccent.GetComponent<Image>();
            caImg.color = new Color(0.95f, 0.65f, 0.2f, 0.90f);
            RectTransform caRect = clueAccent.GetComponent<RectTransform>();
            caRect.anchorMin = new Vector2(0f, 0f);
            caRect.anchorMax = new Vector2(0f, 1f);
            caRect.pivot = new Vector2(0f, 0.5f);
            caRect.anchoredPosition = Vector2.zero;
            caRect.sizeDelta = new Vector2(4f, 0f);

            GameObject clueTitleObj = new GameObject("ClueTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            clueTitleObj.transform.SetParent(clueBox.transform, false);
            TMP_Text clueTitleText = clueTitleObj.GetComponent<TextMeshProUGUI>();
            clueTitleText.text = "LAST MEMORY BEFORE AWAKENING";
            clueTitleText.fontSize = 11;
            clueTitleText.fontStyle = FontStyles.Bold;
            clueTitleText.color = new Color(1f, 0.75f, 0.3f, 0.95f);
            if (tekoFont != null) clueTitleText.font = tekoFont;
            RectTransform ctRect = clueTitleObj.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(0f, 1f);
            ctRect.anchorMax = new Vector2(1f, 1f);
            ctRect.pivot = new Vector2(0f, 1f);
            ctRect.anchoredPosition = new Vector2(16f, -8f);
            ctRect.sizeDelta = new Vector2(-32f, 18f);

            GameObject clueBodyObj = new GameObject("ClueBody", typeof(RectTransform), typeof(TextMeshProUGUI));
            clueBodyObj.transform.SetParent(clueBox.transform, false);
            TMP_Text clueBodyText = clueBodyObj.GetComponent<TextMeshProUGUI>();
            clueBodyText.fontSize = 13;
            clueBodyText.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);
            if (tekoFont != null) clueBodyText.font = tekoFont;
            RectTransform cBodyRect = clueBodyObj.GetComponent<RectTransform>();
            cBodyRect.anchorMin = Vector2.zero;
            cBodyRect.anchorMax = Vector2.one;
            cBodyRect.offsetMin = new Vector2(16f, 6f);
            cBodyRect.offsetMax = new Vector2(-16f, -26f);

            // Lore Synopsis Box
            GameObject loreBox = new GameObject("LoreBox", typeof(RectTransform), typeof(Image));
            loreBox.transform.SetParent(dossierCol.transform, false);
            Image loreBoxBg = loreBox.GetComponent<Image>();
            loreBoxBg.color = new Color(0.08f, 0.10f, 0.15f, 0.65f);
            RectTransform lbRect = loreBox.GetComponent<RectTransform>();
            lbRect.anchorMin = new Vector2(0f, 1f);
            lbRect.anchorMax = new Vector2(1f, 1f);
            lbRect.pivot = new Vector2(0f, 1f);
            lbRect.anchoredPosition = new Vector2(20f, -156f);
            lbRect.sizeDelta = new Vector2(-40f, 76f);

            GameObject loreAccent = new GameObject("LoreAccentStripe", typeof(RectTransform), typeof(Image));
            loreAccent.transform.SetParent(loreBox.transform, false);
            Image laImg = loreAccent.GetComponent<Image>();
            laImg.color = new Color(0.4f, 0.65f, 0.85f, 0.90f);
            RectTransform laRect = loreAccent.GetComponent<RectTransform>();
            laRect.anchorMin = new Vector2(0f, 0f);
            laRect.anchorMax = new Vector2(0f, 1f);
            laRect.pivot = new Vector2(0f, 0.5f);
            laRect.anchoredPosition = Vector2.zero;
            laRect.sizeDelta = new Vector2(4f, 0f);

            GameObject loreTitleObj = new GameObject("LoreTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            loreTitleObj.transform.SetParent(loreBox.transform, false);
            TMP_Text loreTitleText = loreTitleObj.GetComponent<TextMeshProUGUI>();
            loreTitleText.text = "BACKGROUND & OPERATIVE DOSSIER";
            loreTitleText.fontSize = 11;
            loreTitleText.fontStyle = FontStyles.Bold;
            loreTitleText.color = secondaryAccent;
            if (tekoFont != null) loreTitleText.font = tekoFont;
            RectTransform ltRect = loreTitleObj.GetComponent<RectTransform>();
            ltRect.anchorMin = new Vector2(0f, 1f);
            ltRect.anchorMax = new Vector2(1f, 1f);
            ltRect.pivot = new Vector2(0f, 1f);
            ltRect.anchoredPosition = new Vector2(16f, -8f);
            ltRect.sizeDelta = new Vector2(-32f, 18f);

            GameObject loreBodyObj = new GameObject("LoreBody", typeof(RectTransform), typeof(TextMeshProUGUI));
            loreBodyObj.transform.SetParent(loreBox.transform, false);
            TMP_Text loreBodyText = loreBodyObj.GetComponent<TextMeshProUGUI>();
            loreBodyText.fontSize = 13;
            loreBodyText.color = new Color(0.85f, 0.88f, 0.94f, 0.95f);
            if (tekoFont != null) loreBodyText.font = tekoFont;
            RectTransform lBodyRect = loreBodyObj.GetComponent<RectTransform>();
            lBodyRect.anchorMin = Vector2.zero;
            lBodyRect.anchorMax = Vector2.one;
            lBodyRect.offsetMin = new Vector2(16f, 6f);
            lBodyRect.offsetMax = new Vector2(-16f, -26f);

            // Stats and Skill Row
            GameObject statsRow = new GameObject("StatsAndSkillRow", typeof(RectTransform));
            statsRow.transform.SetParent(dossierCol.transform, false);
            RectTransform srRect = statsRow.GetComponent<RectTransform>();
            srRect.anchorMin = new Vector2(0f, 1f);
            srRect.anchorMax = new Vector2(1f, 1f);
            srRect.pivot = new Vector2(0f, 1f);
            srRect.anchoredPosition = new Vector2(20f, -238f);
            srRect.sizeDelta = new Vector2(-40f, 155f);

            // Attributes Sub-box
            GameObject attrBox = new GameObject("AttributesBox", typeof(RectTransform), typeof(Image));
            attrBox.transform.SetParent(statsRow.transform, false);
            Image attrBg = attrBox.GetComponent<Image>();
            attrBg.color = new Color(0.06f, 0.09f, 0.13f, 0.75f);
            RectTransform attrRect = attrBox.GetComponent<RectTransform>();
            attrRect.anchorMin = new Vector2(0f, 0f);
            attrRect.anchorMax = new Vector2(0.42f, 1f);
            attrRect.offsetMin = Vector2.zero;
            attrRect.offsetMax = new Vector2(-6f, 0f);

            GameObject attrAccent = new GameObject("AttrAccentStripe", typeof(RectTransform), typeof(Image));
            attrAccent.transform.SetParent(attrBox.transform, false);
            Image aaImg = attrAccent.GetComponent<Image>();
            aaImg.color = new Color(0.2f, 0.75f, 0.95f, 0.90f);
            RectTransform aaRect = attrAccent.GetComponent<RectTransform>();
            aaRect.anchorMin = new Vector2(0f, 0f);
            aaRect.anchorMax = new Vector2(0f, 1f);
            aaRect.pivot = new Vector2(0f, 0.5f);
            aaRect.anchoredPosition = Vector2.zero;
            aaRect.sizeDelta = new Vector2(4f, 0f);

            GameObject attrTitleObj = new GameObject("AttrTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            attrTitleObj.transform.SetParent(attrBox.transform, false);
            TMP_Text attrTitleText = attrTitleObj.GetComponent<TextMeshProUGUI>();
            attrTitleText.text = "OPERATIVE ATTRIBUTES";
            attrTitleText.fontSize = 11;
            attrTitleText.fontStyle = FontStyles.Bold;
            attrTitleText.color = new Color(0.3f, 0.85f, 1f, 0.95f);
            if (tekoFont != null) attrTitleText.font = tekoFont;
            RectTransform atRect = attrTitleObj.GetComponent<RectTransform>();
            atRect.anchorMin = new Vector2(0f, 1f);
            atRect.anchorMax = new Vector2(1f, 1f);
            atRect.pivot = new Vector2(0f, 1f);
            atRect.anchoredPosition = new Vector2(14f, -8f);
            atRect.sizeDelta = new Vector2(-20f, 18f);

            GameObject attrBodyObj = new GameObject("AttrBody", typeof(RectTransform), typeof(TextMeshProUGUI));
            attrBodyObj.transform.SetParent(attrBox.transform, false);
            TMP_Text attrBodyText = attrBodyObj.GetComponent<TextMeshProUGUI>();
            attrBodyText.fontSize = 13;
            attrBodyText.color = Color.white;
            if (tekoFont != null) attrBodyText.font = tekoFont;
            RectTransform aBodyRect = attrBodyObj.GetComponent<RectTransform>();
            aBodyRect.anchorMin = Vector2.zero;
            aBodyRect.anchorMax = Vector2.one;
            aBodyRect.offsetMin = new Vector2(14f, 8f);
            aBodyRect.offsetMax = new Vector2(-10f, -28f);

            // Shift Skill Sub-box
            GameObject skillBox = new GameObject("ShiftSkillBox", typeof(RectTransform), typeof(Image));
            skillBox.transform.SetParent(statsRow.transform, false);
            Image skillBg = skillBox.GetComponent<Image>();
            skillBg.color = new Color(0.12f, 0.08f, 0.18f, 0.75f);
            RectTransform skillRect = skillBox.GetComponent<RectTransform>();
            skillRect.anchorMin = new Vector2(0.42f, 0f);
            skillRect.anchorMax = new Vector2(1f, 1f);
            skillRect.offsetMin = new Vector2(6f, 0f);
            skillRect.offsetMax = Vector2.zero;

            GameObject skillAccent = new GameObject("SkillAccentStripe", typeof(RectTransform), typeof(Image));
            skillAccent.transform.SetParent(skillBox.transform, false);
            Image saImg = skillAccent.GetComponent<Image>();
            saImg.color = primaryAccent;
            RectTransform saRect = skillAccent.GetComponent<RectTransform>();
            saRect.anchorMin = new Vector2(0f, 0f);
            saRect.anchorMax = new Vector2(0f, 1f);
            saRect.pivot = new Vector2(0f, 0.5f);
            saRect.anchoredPosition = Vector2.zero;
            saRect.sizeDelta = new Vector2(4f, 0f);

            GameObject skillTitleObj = new GameObject("SkillTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            skillTitleObj.transform.SetParent(skillBox.transform, false);
            TMP_Text skillTitleText = skillTitleObj.GetComponent<TextMeshProUGUI>();
            skillTitleText.fontSize = 13;
            skillTitleText.fontStyle = FontStyles.Bold;
            skillTitleText.color = secondaryAccent;
            if (tekoFont != null) skillTitleText.font = tekoFont;
            RectTransform stRect = skillTitleObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 1f);
            stRect.anchorMax = new Vector2(1f, 1f);
            stRect.pivot = new Vector2(0f, 1f);
            stRect.anchoredPosition = new Vector2(14f, -8f);
            stRect.sizeDelta = new Vector2(-20f, 18f);

            GameObject skillBodyObj = new GameObject("SkillBody", typeof(RectTransform), typeof(TextMeshProUGUI));
            skillBodyObj.transform.SetParent(skillBox.transform, false);
            TMP_Text skillBodyText = skillBodyObj.GetComponent<TextMeshProUGUI>();
            skillBodyText.fontSize = 12;
            skillBodyText.color = new Color(0.92f, 0.92f, 0.95f, 0.95f);
            if (tekoFont != null) skillBodyText.font = tekoFont;
            RectTransform sBodyRect = skillBodyObj.GetComponent<RectTransform>();
            sBodyRect.anchorMin = Vector2.zero;
            sBodyRect.anchorMax = Vector2.one;
            sBodyRect.offsetMin = new Vector2(14f, 8f);
            sBodyRect.offsetMax = new Vector2(-10f, -28f);

            // Starting Loadout Box
            GameObject loadoutBox = new GameObject("LoadoutBox", typeof(RectTransform), typeof(Image));
            loadoutBox.transform.SetParent(dossierCol.transform, false);
            RectTransform loutRect = loadoutBox.GetComponent<RectTransform>();
            loutRect.anchorMin = new Vector2(0f, 1f);
            loutRect.anchorMax = new Vector2(1f, 1f);
            loutRect.pivot = new Vector2(0f, 1f);
            loutRect.anchoredPosition = new Vector2(20f, -399f);
            loutRect.sizeDelta = new Vector2(-40f, 75f);

            Image loutBg = loadoutBox.GetComponent<Image>();
            loutBg.color = new Color(0.06f, 0.12f, 0.08f, 0.75f);

            GameObject loutAccent = new GameObject("LoadoutAccentStripe", typeof(RectTransform), typeof(Image));
            loutAccent.transform.SetParent(loadoutBox.transform, false);
            Image laaImg = loutAccent.GetComponent<Image>();
            laaImg.color = new Color(0.35f, 0.90f, 0.55f, 0.90f);
            RectTransform laaRect = loutAccent.GetComponent<RectTransform>();
            laaRect.anchorMin = new Vector2(0f, 0f);
            laaRect.anchorMax = new Vector2(0f, 1f);
            laaRect.pivot = new Vector2(0f, 0.5f);
            laaRect.anchoredPosition = Vector2.zero;
            laaRect.sizeDelta = new Vector2(4f, 0f);

            GameObject loutTitleObj = new GameObject("LoadoutTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            loutTitleObj.transform.SetParent(loadoutBox.transform, false);
            TMP_Text loutTitleText = loutTitleObj.GetComponent<TextMeshProUGUI>();
            loutTitleText.text = "STARTING ARSENAL & SURVIVAL LOADOUT";
            loutTitleText.fontSize = 11;
            loutTitleText.fontStyle = FontStyles.Bold;
            loutTitleText.color = new Color(0.4f, 0.95f, 0.6f, 0.95f);
            if (tekoFont != null) loutTitleText.font = tekoFont;
            RectTransform lttRect = loutTitleObj.GetComponent<RectTransform>();
            lttRect.anchorMin = new Vector2(0f, 1f);
            lttRect.anchorMax = new Vector2(1f, 1f);
            lttRect.pivot = new Vector2(0f, 1f);
            lttRect.anchoredPosition = new Vector2(14f, -8f);
            lttRect.sizeDelta = new Vector2(-20f, 18f);

            GameObject loutBodyObj = new GameObject("LoadoutBody", typeof(RectTransform), typeof(TextMeshProUGUI));
            loutBodyObj.transform.SetParent(loadoutBox.transform, false);
            TMP_Text loutBodyText = loutBodyObj.GetComponent<TextMeshProUGUI>();
            loutBodyText.fontSize = 13;
            loutBodyText.color = Color.white;
            if (tekoFont != null) loutBodyText.font = tekoFont;
            RectTransform ltBodyRect = loutBodyObj.GetComponent<RectTransform>();
            ltBodyRect.anchorMin = Vector2.zero;
            ltBodyRect.anchorMax = Vector2.one;
            ltBodyRect.offsetMin = new Vector2(14f, 6f);
            ltBodyRect.offsetMax = new Vector2(-10f, -26f);

            // Populate Roster Column Tabs in the Scene!
            List<CharacterData> roster = (CharacterDatabase.Instance != null && CharacterDatabase.Instance.Characters.Count > 0)
                ? CharacterDatabase.Instance.Characters
                : CharacterDatabase.CreateDefaultRoster();

            List<Button> tabButtons = new List<Button>();
            List<Image> tabBgs = new List<Image>();
            List<Image> tabIndicators = new List<Image>();
            List<TMP_Text> tabLabels = new List<TMP_Text>();

            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                GameObject tabObj = new GameObject($"Tab_{ch.characterId}", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
                tabObj.transform.SetParent(rosterCol.transform, false);

                LayoutElement le = tabObj.GetComponent<LayoutElement>();
                le.preferredHeight = 92f;
                le.minHeight = 92f;

                Image tabBg = tabObj.GetComponent<Image>();
                tabBg.color = new Color(0.09f, 0.10f, 0.14f, 0.75f);
                tabBgs.Add(tabBg);

                Button tBtn = tabObj.GetComponent<Button>();
                tabButtons.Add(tBtn);

                // Left active indicator strip
                GameObject activeBar = new GameObject("ActiveIndicator", typeof(RectTransform), typeof(Image));
                activeBar.transform.SetParent(tabObj.transform, false);
                Image barImg = activeBar.GetComponent<Image>();
                barImg.color = ch.themeColor;
                RectTransform barRect = activeBar.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 0.5f);
                barRect.anchoredPosition = Vector2.zero;
                barRect.sizeDelta = new Vector2(4f, 0f);
                activeBar.SetActive(i == 0);
                tabIndicators.Add(barImg);

                // Tab Text Container
                GameObject tLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                tLabelObj.transform.SetParent(tabObj.transform, false);
                TMP_Text tLabel = tLabelObj.GetComponent<TextMeshProUGUI>();
                tLabel.text = $"<b>{ch.characterName.ToUpper()}</b>\n<size=12><color=#A855F7>{ch.roleCategory}</color></size>";
                tLabel.fontSize = 15;
                tLabel.color = (i == 0) ? Color.white : new Color(0.70f, 0.75f, 0.85f, 0.85f);
                if (tekoFont != null) tLabel.font = tekoFont;
                RectTransform tlRect = tLabelObj.GetComponent<RectTransform>();
                tlRect.anchorMin = Vector2.zero;
                tlRect.anchorMax = Vector2.one;
                tlRect.offsetMin = new Vector2(18f, 0f);
                tlRect.offsetMax = new Vector2(-12f, 0f);
                tabLabels.Add(tLabel);
            }

            // Bottom Action Bar: CANCEL (Left) and EMBARK (Right)
            GameObject cancelBtnObj = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelBtnObj.transform.SetParent(panel.transform, false);
            RectTransform cancelRect = cancelBtnObj.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0f, 0f);
            cancelRect.anchorMax = new Vector2(0f, 0f);
            cancelRect.pivot = new Vector2(0f, 0f);
            cancelRect.anchoredPosition = new Vector2(30f, 18f);
            cancelRect.sizeDelta = new Vector2(200f, 44f);

            Image cancelBg = cancelBtnObj.GetComponent<Image>();
            cancelBg.color = buttonBgColor;
            Button cancelBtn = cancelBtnObj.GetComponent<Button>();

            GameObject cancelLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            cancelLabelObj.transform.SetParent(cancelBtnObj.transform, false);
            TMP_Text cancelLabel = cancelLabelObj.GetComponent<TextMeshProUGUI>();
            cancelLabel.text = "CANCEL // BACK";
            cancelLabel.fontSize = 17;
            cancelLabel.fontStyle = FontStyles.Bold;
            cancelLabel.color = Color.white;
            cancelLabel.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) cancelLabel.font = tekoFont;
            RectTransform cLabelRect = cancelLabelObj.GetComponent<RectTransform>();
            cLabelRect.anchorMin = Vector2.zero;
            cLabelRect.anchorMax = Vector2.one;

            // EMBARK Button (Right side, glowing vibrant purple)
            GameObject embarkBtnObj = new GameObject("EmbarkButton", typeof(RectTransform), typeof(Image), typeof(Button));
            embarkBtnObj.transform.SetParent(panel.transform, false);
            RectTransform embarkRect = embarkBtnObj.GetComponent<RectTransform>();
            embarkRect.anchorMin = new Vector2(1f, 0f);
            embarkRect.anchorMax = new Vector2(1f, 0f);
            embarkRect.pivot = new Vector2(1f, 0f);
            embarkRect.anchoredPosition = new Vector2(-30f, 18f);
            embarkRect.sizeDelta = new Vector2(320f, 44f);

            Image embarkBg = embarkBtnObj.GetComponent<Image>();
            embarkBg.color = primaryAccent;
            Button embarkBtn = embarkBtnObj.GetComponent<Button>();

            GameObject embarkLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            embarkLabelObj.transform.SetParent(embarkBtnObj.transform, false);
            TMP_Text embarkLabel = embarkLabelObj.GetComponent<TextMeshProUGUI>();
            embarkLabel.text = "EMBARK / ENTER WASTELAND";
            embarkLabel.fontSize = 18;
            embarkLabel.fontStyle = FontStyles.Bold;
            embarkLabel.color = Color.white;
            embarkLabel.alignment = TextAlignmentOptions.Center;
            if (tekoFont != null) embarkLabel.font = tekoFont;
            RectTransform eLabelRect = embarkLabelObj.GetComponent<RectTransform>();
            eLabelRect.anchorMin = Vector2.zero;
            eLabelRect.anchorMax = Vector2.one;

            // Attach CharacterSelectUI Component to the root modal in scene
            CharacterSelectUI uiComp = modalRoot.AddComponent<CharacterSelectUI>();
            uiComp.BindReferences(
                cancelBtn, embarkBtn,
                dNameText, dRoleText,
                clueBodyText, loreBodyText,
                attrBodyText, skillTitleText, skillBodyText,
                loutBodyText, rosterCol.transform,
                tabButtons, tabBgs, tabIndicators, tabLabels,
                charImg
            );

            // Assign to MainMenuManager serialized field in scene
            SerializedProperty charModalProp = menuSo.FindProperty("characterSelectModalObj");
            if (charModalProp != null)
            {
                charModalProp.objectReferenceValue = modalRoot;
                menuSo.ApplyModifiedProperties();
            }

            // Save Prefab asset in project as well
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(modalRoot, PrefabPath, InteractionMode.AutomatedAction);

            // Keep modal active by default in Editor so designer can see and edit it right away!
            // When play mode starts, MainMenuManager will wire it up.
            modalRoot.SetActive(false);

            EditorUtility.SetDirty(menuManager);
            EditorUtility.SetDirty(modalRoot);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"<color=#50FA7B>[CharacterSelectSceneBuilder]</color> Successfully instantiated CharacterSelectModal in {ScenePath} and saved Prefab to {PrefabPath}!");
        }
    }
}
