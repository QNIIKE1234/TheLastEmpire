using UnityEngine;
using System.Collections.Generic;

namespace TheLastEmpire.Story
{
    [CreateAssetMenu(fileName = "NewDialogueTree", menuName = "TheLastEmpire/Story/Dialogue Tree")]
    public class DialogueTreeSO : ScriptableObject
    {
        [Tooltip("List of dialogue nodes to play in sequence")]
        public List<DialogueNode> dialogueLines = new List<DialogueNode>();
    }
}
