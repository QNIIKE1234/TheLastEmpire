using UnityEngine;
using UnityEngine.Events;

namespace TheLastEmpire.Story
{
    [System.Serializable]
    public class DialogueNode
    {
        [Tooltip("The name of the character speaking")]
        public string speakerName;

        [Tooltip("Portrait image for this speaker line (optional)")]
        public Sprite speakerPortrait;

        [Tooltip("The dialogue text to display")]
        [TextArea(3, 10)]
        public string dialogueText;

        [Tooltip("Events to trigger when this dialogue line finishes (e.g. give item, start quest)")]
        public UnityEvent onDialogueEnd;
    }
}
