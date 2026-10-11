using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using TheLastEmpire.Story;
using UnityEngine.InputSystem;

namespace TheLastEmpire.UI
{
    public class DialogueUI : MonoBehaviour, IPopUp
    {
        public static DialogueUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<DialogueUI>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("DialogueUIManager");
                        _instance = go.AddComponent<DialogueUI>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        private static DialogueUI _instance;

        [Header("UI Customization")]
        [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        [SerializeField] private Color accentColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        [Header("UI References (Optional Override)")]
        [SerializeField] private GameObject dialogueContainer;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text dialogueBodyText;
        [SerializeField] private GameObject portraitBox;
        [SerializeField] private Image portraitImage;

        [Header("Settings")]
        [SerializeField] private float typingSpeed = 0.03f;

        private DialogueTreeSO _currentDialogueTree;
        private Sprite _fallbackPortrait;
        private int _currentLineIndex = 0;
        private bool _isTyping = false;
        private string _currentFullText = "";
        private Coroutine _typewriterCoroutine;

        private RectTransform _bodyTextRect;
        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLockState;

        public bool IsDialogueActive => dialogueContainer != null && dialogueContainer.activeSelf;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);

                if (dialogueContainer == null)
                {
                    CreateProceduralUI();
                }
                else
                {
                    dialogueContainer.SetActive(false);
                    if (dialogueBodyText != null)
                    {
                        _bodyTextRect = dialogueBodyText.GetComponent<RectTransform>();
                    }
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!IsDialogueActive) return;

            // Handle Input to advance or skip dialogue (Space or Left Click)
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame ||
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                AdvanceDialogue();
            }
        }

        public void StartDialogue(string dialogueId, Sprite fallbackPortrait = null)
        {
            if (DialogueDatabase.Instance != null)
            {
                DialogueTreeSO tree = DialogueDatabase.Instance.GetDialogueTree(dialogueId);
                if (tree != null)
                {
                    StartDialogue(tree, fallbackPortrait);
                    return;
                }
            }
            Debug.LogWarning($"[DialogueUI] Cannot find dialogue with ID '{dialogueId}' in DialogueDatabase.");
        }

        public void StartDialogue(DialogueTreeSO dialogueTree, Sprite fallbackPortrait = null)
        {
            if (dialogueTree == null || dialogueTree.dialogueLines.Count == 0) return;

            if (dialogueContainer == null)
            {
                CreateProceduralUI();
            }

            _currentDialogueTree = dialogueTree;
            _fallbackPortrait = fallbackPortrait;
            _currentLineIndex = 0;
            
            if (dialogueContainer != null)
                dialogueContainer.SetActive(true);

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }

            _savedCursorVisible = Cursor.visible;
            _savedCursorLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Pause Game
            Time.timeScale = 0f;

            PlayNextLine();
        }

        private void PlayNextLine()
        {
            if (_currentDialogueTree == null || dialogueBodyText == null) return;

            if (_currentLineIndex < _currentDialogueTree.dialogueLines.Count)
            {
                DialogueNode node = _currentDialogueTree.dialogueLines[_currentLineIndex];
                if (speakerNameText != null)
                {
                    speakerNameText.text = string.IsNullOrEmpty(node.speakerName) ? "NPC" : node.speakerName;
                }
                _currentFullText = node.dialogueText;

                // Handle Speaker Portrait
                Sprite portraitToUse = node.speakerPortrait != null ? node.speakerPortrait : _fallbackPortrait;
                GameObject boxToToggle = portraitBox != null ? portraitBox : (portraitImage != null ? portraitImage.gameObject : null);

                if (portraitImage != null && portraitToUse != null)
                {
                    portraitImage.sprite = portraitToUse;
                    if (boxToToggle != null) boxToToggle.SetActive(true);

                    if (_bodyTextRect != null)
                    {
                        _bodyTextRect.offsetMin = new Vector2(195f, 30f);
                    }
                }
                else
                {
                    if (boxToToggle != null) boxToToggle.SetActive(false);

                    if (_bodyTextRect != null)
                    {
                        _bodyTextRect.offsetMin = new Vector2(35f, 30f);
                    }
                }

                if (_typewriterCoroutine != null)
                {
                    StopCoroutine(_typewriterCoroutine);
                }
                _typewriterCoroutine = StartCoroutine(TypewriterEffect(_currentFullText));
            }
            else
            {
                EndDialogue();
            }
        }

        private void AdvanceDialogue()
        {
            if (_isTyping)
            {
                // Skip typing, show full text immediately
                if (_typewriterCoroutine != null)
                {
                    StopCoroutine(_typewriterCoroutine);
                }
                if (dialogueBodyText != null)
                {
                    dialogueBodyText.text = _currentFullText;
                }
                _isTyping = false;
            }
            else
            {
                // Trigger events from the finished line
                if (_currentDialogueTree != null && _currentLineIndex < _currentDialogueTree.dialogueLines.Count)
                {
                    DialogueNode currentNode = _currentDialogueTree.dialogueLines[_currentLineIndex];
                    currentNode.onDialogueEnd?.Invoke();
                }

                // Go to next line
                _currentLineIndex++;
                PlayNextLine();
            }
        }

        public void EndDialogue()
        {
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);
            
            _currentDialogueTree = null;
            _fallbackPortrait = null;
            _currentLineIndex = 0;

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLockState;

            // Resume Game
            Time.timeScale = 1f;
        }

        public void ClosePopUp()
        {
            if (IsDialogueActive)
            {
                EndDialogue();
            }
        }

        private IEnumerator TypewriterEffect(string text)
        {
            _isTyping = true;
            if (dialogueBodyText != null)
            {
                dialogueBodyText.text = "";
                foreach (char c in text.ToCharArray())
                {
                    dialogueBodyText.text += c;
                    // Use unscaledDeltaTime since timeScale is 0
                    yield return new WaitForSecondsRealtime(typingSpeed);
                }
            }
            _isTyping = false;
        }

        // =====================================================
        //  Procedural UI Fallback
        // =====================================================

        private void CreateProceduralUI()
        {
            if (dialogueContainer != null) return;

            GameObject canvasObj = new GameObject("DialogueCanvas");
            DontDestroyOnLoad(canvasObj);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Dialogue Box Container
            dialogueContainer = new GameObject("DialoguePanel");
            dialogueContainer.transform.SetParent(canvasObj.transform, false);

            Image panelImage = dialogueContainer.AddComponent<Image>();
            panelImage.color = panelColor;

            RectTransform containerRect = dialogueContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.2f, 0.05f);
            containerRect.anchorMax = new Vector2(0.8f, 0.28f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // Border
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(dialogueContainer.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            borderObj.transform.SetAsFirstSibling();

            // Speaker Name Badge
            GameObject nameBadgeObj = new GameObject("NameBadge");
            nameBadgeObj.transform.SetParent(dialogueContainer.transform, false);
            Image nameBadgeBg = nameBadgeObj.AddComponent<Image>();
            nameBadgeBg.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

            RectTransform badgeRect = nameBadgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(25f, 0f);
            badgeRect.sizeDelta = new Vector2(200f, 40f);

            // Badge Border
            GameObject badgeBorder = new GameObject("BadgeBorder");
            badgeBorder.transform.SetParent(nameBadgeObj.transform, false);
            Image bbImg = badgeBorder.AddComponent<Image>();
            bbImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f);
            RectTransform bbRect = badgeBorder.GetComponent<RectTransform>();
            bbRect.anchorMin = Vector2.zero;
            bbRect.anchorMax = Vector2.one;
            bbRect.offsetMin = new Vector2(-2f, -2f);
            bbRect.offsetMax = new Vector2(2f, 2f);
            badgeBorder.transform.SetAsFirstSibling();

            // Speaker Name Text
            GameObject nameTextObj = new GameObject("SpeakerNameText");
            nameTextObj.transform.SetParent(nameBadgeObj.transform, false);
            speakerNameText = nameTextObj.AddComponent<TextMeshProUGUI>();
            speakerNameText.text = "NPC";
            speakerNameText.fontSize = 24;
            speakerNameText.fontStyle = FontStyles.Bold;
            speakerNameText.color = accentColor;
            speakerNameText.alignment = TextAlignmentOptions.Center;
            RectTransform ntRect = nameTextObj.GetComponent<RectTransform>();
            ntRect.anchorMin = Vector2.zero;
            ntRect.anchorMax = Vector2.one;
            ntRect.offsetMin = Vector2.zero;
            ntRect.offsetMax = Vector2.zero;

            // Portrait Box Container
            portraitBox = new GameObject("PortraitBox");
            portraitBox.transform.SetParent(dialogueContainer.transform, false);
            Image pBoxBg = portraitBox.AddComponent<Image>();
            pBoxBg.color = new Color(0.12f, 0.12f, 0.16f, 0.98f);

            RectTransform pBoxRect = portraitBox.GetComponent<RectTransform>();
            pBoxRect.anchorMin = new Vector2(0f, 0.5f);
            pBoxRect.anchorMax = new Vector2(0f, 0.5f);
            pBoxRect.pivot = new Vector2(0f, 0.5f);
            pBoxRect.anchoredPosition = new Vector2(25f, -8f);
            pBoxRect.sizeDelta = new Vector2(150f, 150f);

            // Portrait Border
            GameObject pBorder = new GameObject("PortraitBorder");
            pBorder.transform.SetParent(portraitBox.transform, false);
            Image pbImg = pBorder.AddComponent<Image>();
            pbImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f);
            RectTransform pbRect = pBorder.GetComponent<RectTransform>();
            pbRect.anchorMin = Vector2.zero;
            pbRect.anchorMax = Vector2.one;
            pbRect.offsetMin = new Vector2(-2f, -2f);
            pbRect.offsetMax = new Vector2(2f, 2f);
            pBorder.transform.SetAsFirstSibling();

            // Portrait Image
            GameObject pImgObj = new GameObject("PortraitImage");
            pImgObj.transform.SetParent(portraitBox.transform, false);
            portraitImage = pImgObj.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            RectTransform piRect = pImgObj.GetComponent<RectTransform>();
            piRect.anchorMin = Vector2.zero;
            piRect.anchorMax = Vector2.one;
            piRect.offsetMin = new Vector2(4f, 4f);
            piRect.offsetMax = new Vector2(-4f, -4f);

            portraitBox.SetActive(false);

            // Dialogue Body Text
            GameObject bodyTextObj = new GameObject("DialogueBodyText");
            bodyTextObj.transform.SetParent(dialogueContainer.transform, false);
            dialogueBodyText = bodyTextObj.AddComponent<TextMeshProUGUI>();
            dialogueBodyText.text = "...";
            dialogueBodyText.fontSize = 26;
            dialogueBodyText.color = Color.white;
            dialogueBodyText.alignment = TextAlignmentOptions.TopLeft;
            dialogueBodyText.enableWordWrapping = true;
            dialogueBodyText.lineSpacing = 8f;

            _bodyTextRect = bodyTextObj.GetComponent<RectTransform>();
            _bodyTextRect.anchorMin = Vector2.zero;
            _bodyTextRect.anchorMax = Vector2.one;
            _bodyTextRect.offsetMin = new Vector2(35f, 30f);
            _bodyTextRect.offsetMax = new Vector2(-35f, -32f);

            // Continue Hint
            GameObject hintObj = new GameObject("ContinueHint");
            hintObj.transform.SetParent(dialogueContainer.transform, false);
            TMP_Text hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "[ Space / Left Click ]  ▶";
            hintText.fontSize = 18;
            hintText.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
            hintText.alignment = TextAlignmentOptions.Right;

            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-25f, 12f);
            hintRect.sizeDelta = new Vector2(300f, 25f);

            dialogueContainer.SetActive(false);
        }
    }
}
