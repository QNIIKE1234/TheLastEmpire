using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using TheLastEmpire.Story;
using UnityEngine.InputSystem;

namespace TheLastEmpire.UI
{
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject dialogueContainer;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text dialogueBodyText;
        [SerializeField] private Image portraitImage; // Optional, can be used later

        [Header("Settings")]
        [SerializeField] private float typingSpeed = 0.03f;

        private DialogueTreeSO _currentDialogueTree;
        private int _currentLineIndex = 0;
        private bool _isTyping = false;
        private string _currentFullText = "";
        private Coroutine _typewriterCoroutine;

        public bool IsDialogueActive => dialogueContainer != null && dialogueContainer.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);
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

        public void StartDialogue(DialogueTreeSO dialogueTree)
        {
            if (dialogueTree == null || dialogueTree.dialogueLines.Count == 0) return;

            _currentDialogueTree = dialogueTree;
            _currentLineIndex = 0;
            
            if (dialogueContainer != null)
                dialogueContainer.SetActive(true);

            // Pause Game
            Time.timeScale = 0f;

            PlayNextLine();
        }

        private void PlayNextLine()
        {
            if (_currentLineIndex < _currentDialogueTree.dialogueLines.Count)
            {
                DialogueNode node = _currentDialogueTree.dialogueLines[_currentLineIndex];
                speakerNameText.text = node.speakerName;
                _currentFullText = node.dialogueText;

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
                dialogueBodyText.text = _currentFullText;
                _isTyping = false;
            }
            else
            {
                // Trigger events from the finished line
                DialogueNode currentNode = _currentDialogueTree.dialogueLines[_currentLineIndex];
                currentNode.onDialogueEnd?.Invoke();

                // Go to next line
                _currentLineIndex++;
                PlayNextLine();
            }
        }

        private void EndDialogue()
        {
            if (dialogueContainer != null)
                dialogueContainer.SetActive(false);
            
            _currentDialogueTree = null;
            _currentLineIndex = 0;

            // Resume Game
            Time.timeScale = 1f;
        }

        private IEnumerator TypewriterEffect(string text)
        {
            _isTyping = true;
            dialogueBodyText.text = "";
            foreach (char c in text.ToCharArray())
            {
                dialogueBodyText.text += c;
                // Use unscaledDeltaTime since timeScale is 0
                yield return new WaitForSecondsRealtime(typingSpeed);
            }
            _isTyping = false;
        }
    }
}
