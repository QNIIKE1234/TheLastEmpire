using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Component managing the Operative Selection Modal in the Main Menu scene.
    /// Allows designers to customize layout, visual elements, and decorations directly in the Unity Scene/Inspector.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("Action Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button embarkButton;

        [Header("Dossier Header")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text roleLabel;

        [Header("Dossier Content")]
        [SerializeField] private TMP_Text amnesiaClueLabel;
        [SerializeField] private TMP_Text loreSynopsisLabel;
        [SerializeField] private TMP_Text attributesLabel;
        [SerializeField] private TMP_Text shiftAbilityTitleLabel;
        [SerializeField] private TMP_Text shiftAbilityDescLabel;
        [SerializeField] private TMP_Text loadoutLabel;

        [Header("Operative Portrait Artwork")]
        [Tooltip("The Image component on ModalCharacter where character artwork is displayed.")]
        [SerializeField] private Image characterPortraitImage;

        [Header("Operative Portraits (Scene References)")]
        [Tooltip("Artwork for The Vanguard")]
        [SerializeField] private Sprite vanguardPortrait;
        [Tooltip("Artwork for The Juggernaut")]
        [SerializeField] private Sprite juggernautPortrait;
        [Tooltip("Artwork for The Phantom")]
        [SerializeField] private Sprite phantomPortrait;
        [Tooltip("Artwork for The Marksman")]
        [SerializeField] private Sprite marksmanPortrait;
        [Tooltip("Artwork for The Scavenger")]
        [SerializeField] private Sprite scavengerPortrait;

        [System.Serializable]
        public struct CustomPortraitEntry
        {
            public string characterId;
            public Sprite portrait;
        }

        [Tooltip("Additional or custom character portraits.")]
        [SerializeField] private List<CustomPortraitEntry> customPortraits = new List<CustomPortraitEntry>();

        [Header("Roster Tabs")]
        [SerializeField] private Transform rosterColumn;
        [SerializeField] private List<Button> tabButtons = new List<Button>();
        [SerializeField] private List<Image> tabBackgrounds = new List<Image>();
        [SerializeField] private List<Image> tabActiveIndicators = new List<Image>();
        [SerializeField] private List<TMP_Text> tabLabels = new List<TMP_Text>();

        private string _selectedCharacterId = "VANGUARD";
        private List<CharacterData> _roster;

        public void BindReferences(
            Button cancel, Button embark,
            TMP_Text nameTxt, TMP_Text roleTxt,
            TMP_Text clueTxt, TMP_Text loreTxt,
            TMP_Text attrTxt, TMP_Text shiftTitleTxt, TMP_Text shiftDescTxt,
            TMP_Text loadoutTxt, Transform rCol,
            List<Button> buttons, List<Image> bgs, List<Image> indicators, List<TMP_Text> labels,
            Image portraitImg = null)
        {
            cancelButton = cancel;
            embarkButton = embark;
            nameLabel = nameTxt;
            roleLabel = roleTxt;
            amnesiaClueLabel = clueTxt;
            loreSynopsisLabel = loreTxt;
            attributesLabel = attrTxt;
            shiftAbilityTitleLabel = shiftTitleTxt;
            shiftAbilityDescLabel = shiftDescTxt;
            loadoutLabel = loadoutTxt;
            rosterColumn = rCol;
            tabButtons = buttons;
            tabBackgrounds = bgs;
            tabActiveIndicators = indicators;
            tabLabels = labels;
            if (portraitImg != null)
            {
                characterPortraitImage = portraitImg;
            }
        }

        private void Awake()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (embarkButton != null)
            {
                embarkButton.onClick.RemoveAllListeners();
                embarkButton.onClick.AddListener(OnEmbarkClicked);
            }

            EnsurePortraitReference();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsurePortraitReference();
        }
#endif

        private void OnEnable()
        {
            EnsurePortraitReference();
            InitializeRoster();
        }

        /// <summary>
        /// Automatically discovers ModalCharacter image and binds default artwork if unassigned.
        /// </summary>
        public void EnsurePortraitReference()
        {
            if (characterPortraitImage == null)
            {
                // Search under ModalPanel first (standard hierarchy)
                Transform panel = transform.Find("ModalPanel");
                if (panel != null)
                {
                    Transform mc = panel.Find("ModalCharacter");
                    if (mc != null) characterPortraitImage = mc.GetComponent<Image>();
                }

                // Search direct child
                if (characterPortraitImage == null)
                {
                    Transform mc = transform.Find("ModalCharacter");
                    if (mc != null) characterPortraitImage = mc.GetComponent<Image>();
                }

                // Deep search in all descendants
                if (characterPortraitImage == null)
                {
                    var images = GetComponentsInChildren<Image>(true);
                    foreach (var img in images)
                    {
                        if (img.gameObject.name.Equals("ModalCharacter", StringComparison.OrdinalIgnoreCase))
                        {
                            characterPortraitImage = img;
                            break;
                        }
                    }
                }
            }

            // If ModalCharacter already has a sprite placed in the scene by designer, capture it as Vanguard default
            if (characterPortraitImage != null && characterPortraitImage.sprite != null && vanguardPortrait == null)
            {
                vanguardPortrait = characterPortraitImage.sprite;
            }
        }

        /// <summary>
        /// Resolves the operative portrait from either inspector slot references or CharacterData ScriptableObject.
        /// </summary>
        public Sprite GetPortraitForCharacter(CharacterData ch)
        {
            if (ch == null) return null;

            string id = !string.IsNullOrEmpty(ch.characterId) ? ch.characterId.ToUpperInvariant() : "";
            Sprite found = null;

            switch (id)
            {
                case "VANGUARD":
                    found = vanguardPortrait;
                    break;
                case "JUGGERNAUT":
                    found = juggernautPortrait;
                    break;
                case "PHANTOM":
                    found = phantomPortrait;
                    break;
                case "MARKSMAN":
                    found = marksmanPortrait;
                    break;
                case "SCAVENGER":
                    found = scavengerPortrait;
                    break;
            }

            if (found != null) return found;

            // Check custom inspector list
            if (customPortraits != null && customPortraits.Count > 0)
            {
                var entry = customPortraits.Find(e => !string.IsNullOrEmpty(e.characterId) && e.characterId.Equals(ch.characterId, StringComparison.OrdinalIgnoreCase));
                if (entry.portrait != null) return entry.portrait;
            }

            // Fallback to CharacterData ScriptableObject field
            if (ch.characterPortrait != null)
            {
                return ch.characterPortrait;
            }

            return null;
        }

        public void InitializeRoster()
        {
            _roster = (CharacterDatabase.Instance != null && CharacterDatabase.Instance.Characters.Count > 0)
                ? CharacterDatabase.Instance.Characters
                : CharacterDatabase.CreateDefaultRoster();

            _selectedCharacterId = CharacterManager.SelectedCharacterId;

            // Auto-wire tab buttons if list unassigned
            if ((tabButtons == null || tabButtons.Count == 0) && rosterColumn != null)
            {
                var btns = rosterColumn.GetComponentsInChildren<Button>(true);
                tabButtons = new List<Button>(btns);
                tabBackgrounds = new List<Image>();
                tabActiveIndicators = new List<Image>();
                tabLabels = new List<TMP_Text>();
                foreach (var b in btns)
                {
                    tabBackgrounds.Add(b.GetComponent<Image>());
                    Transform ind = b.transform.Find("ActiveIndicator");
                    tabActiveIndicators.Add(ind != null ? ind.GetComponent<Image>() : null);
                    tabLabels.Add(b.GetComponentInChildren<TMP_Text>(true));
                }
            }

            // Wire existing tab buttons in scene if present
            for (int i = 0; i < tabButtons.Count && i < _roster.Count; i++)
            {
                var ch = _roster[i];
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.RemoveAllListeners();
                    tabButtons[i].onClick.AddListener(() => SelectCharacter(ch));
                }
            }

            // Find current selected character or default to first
            CharacterData selected = _roster.Find(c => c.characterId.Equals(_selectedCharacterId, StringComparison.OrdinalIgnoreCase)) ?? _roster[0];
            SelectCharacter(selected);
        }

        public void SelectCharacter(CharacterData ch)
        {
            if (ch == null) return;
            _selectedCharacterId = ch.characterId;

            // 1. Update Character Portrait Artwork (ModalCharacter)
            EnsurePortraitReference();
            if (characterPortraitImage != null)
            {
                Sprite portrait = GetPortraitForCharacter(ch);
                if (portrait != null)
                {
                    characterPortraitImage.sprite = portrait;
                    characterPortraitImage.gameObject.SetActive(true);
                    characterPortraitImage.enabled = true;
                    characterPortraitImage.color = Color.white;
                }
                else
                {
                    // If no artwork is configured for this operative, hide to avoid displaying mismatched character
                    characterPortraitImage.sprite = null;
                    characterPortraitImage.color = new Color(1f, 1f, 1f, 0f);
                }
            }

            // 2. Update Dossier Texts
            if (nameLabel != null) nameLabel.text = ch.characterName.ToUpper();
            if (roleLabel != null) roleLabel.text = $"{ch.characterTitle}  •  <color=#A855F7>{ch.roleCategory}</color>";
            if (amnesiaClueLabel != null) amnesiaClueLabel.text = ch.amnesiaWakingClue;
            if (loreSynopsisLabel != null) loreSynopsisLabel.text = ch.loreSynopsis;
            if (attributesLabel != null)
            {
                attributesLabel.text = $"• Max HP: <b>{ch.maxHealth}</b>\n• Max Hunger: <b>{ch.maxHunger}</b>\n• Move Speed: <b>{ch.moveSpeed:F1} m/s</b>\n• Dash Speed: <b>{ch.dashSpeed:F0} m/s</b> ({ch.dashCooldown:F1}s CD)";
            }
            if (shiftAbilityTitleLabel != null) shiftAbilityTitleLabel.text = $"✦ [SHIFT] {ch.shiftAbilityName.ToUpper()}";
            if (shiftAbilityDescLabel != null) shiftAbilityDescLabel.text = ch.shiftAbilityDescription;

            if (loadoutLabel != null)
            {
                string wpns = (ch.startingWeapons != null && ch.startingWeapons.Count > 0) ? string.Join(", ", ch.startingWeapons) : "Knife";
                loadoutLabel.text = $"• Primary Weapon: <b>{wpns}</b>\n• Starting Ammo: <b>{ch.startingPistolAmmo} rounds</b> | Ration: <b>2x Bread</b> | Credits: <b>{ch.startingMoney} Gold</b>";
            }

            // 3. Update tab highlights
            for (int i = 0; i < _roster.Count; i++)
            {
                bool isThis = _roster[i].characterId.Equals(ch.characterId, StringComparison.OrdinalIgnoreCase);

                if (i < tabBackgrounds.Count && tabBackgrounds[i] != null)
                {
                    tabBackgrounds[i].color = isThis ? new Color(0.18f, 0.20f, 0.28f, 0.95f) : new Color(0.09f, 0.10f, 0.14f, 0.75f);
                }

                if (i < tabActiveIndicators.Count && tabActiveIndicators[i] != null)
                {
                    tabActiveIndicators[i].gameObject.SetActive(isThis);
                    tabActiveIndicators[i].color = _roster[i].themeColor;
                }

                if (i < tabLabels.Count && tabLabels[i] != null)
                {
                    tabLabels[i].color = isThis ? Color.white : new Color(0.70f, 0.75f, 0.85f, 0.85f);
                }
            }
        }

        public void OnCancelClicked()
        {
            if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.OnCloseCharacterSelect();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void OnEmbarkClicked()
        {
            if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.StartGameWithCharacter(_selectedCharacterId);
            }
        }
    }
}
