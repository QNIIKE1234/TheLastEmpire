using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire.Story
{
    [System.Serializable]
    public class DialogueEntry
    {
        [Tooltip("Unique ID for this dialogue (e.g. 'Merchant_Intro', 'Quest_Start', 'Alien_Warning')")]
        public string dialogueId;

        [Tooltip("Category or Tag for grouping (e.g. 'Shop', 'Story', 'Rumor', 'Event')")]
        public string category = "General";

        [Tooltip("Description or developer notes")]
        public string description;

        [Tooltip("Direct reference to an existing DialogueTreeSO asset (optional override)")]
        public DialogueTreeSO dialogueTree;

        [Tooltip("Inline dialogue lines configured directly inside this database entry")]
        public List<DialogueNode> inlineLines = new List<DialogueNode>();

        private DialogueTreeSO _generatedTree;

        /// <summary>
        /// Returns the associated DialogueTreeSO, or generates a runtime tree from inline lines.
        /// </summary>
        public DialogueTreeSO GetEffectiveTree()
        {
            if (dialogueTree != null) return dialogueTree;

            if (inlineLines != null && inlineLines.Count > 0)
            {
                if (_generatedTree == null)
                {
                    _generatedTree = ScriptableObject.CreateInstance<DialogueTreeSO>();
                    _generatedTree.name = $"RuntimeTree_{dialogueId}";
                    _generatedTree.dialogueLines = inlineLines;
                }
                return _generatedTree;
            }

            return null;
        }
    }

    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "TheLastEmpire/Dialogue Database")]
    public class DialogueDatabase : ScriptableObject
    {
        private static DialogueDatabase _instance;
        public static DialogueDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<DialogueDatabase>("Database/DialogueDatabase");
                    if (_instance == null)
                    {
                        _instance = Resources.Load<DialogueDatabase>("DialogueDatabase");
                    }
                    if (_instance == null)
                    {
                        DialogueDatabase[] databases = Resources.FindObjectsOfTypeAll<DialogueDatabase>();
                        if (databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("Global Dialogue Registry")]
        [Tooltip("List of all configured dialogue entries in the game")]
        public List<DialogueEntry> dialogues = new List<DialogueEntry>();

        /// <summary>
        /// Finds a dialogue entry by its unique ID (case-insensitive and trimmed).
        /// </summary>
        public DialogueEntry GetEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string clean = id.Trim().ToLowerInvariant();

            foreach (var entry in dialogues)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.dialogueId) && entry.dialogueId.Trim().ToLowerInvariant() == clean)
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves the playable DialogueTreeSO for the given dialogue ID.
        /// </summary>
        public DialogueTreeSO GetDialogueTree(string id)
        {
            DialogueEntry entry = GetEntry(id);
            return entry?.GetEffectiveTree();
        }

        /// <summary>
        /// Checks if a dialogue ID exists in the database.
        /// </summary>
        public bool HasDialogue(string id)
        {
            return GetEntry(id) != null;
        }

        /// <summary>
        /// Gets all dialogues matching a specific category (e.g. 'Rumor', 'Shop').
        /// </summary>
        public List<DialogueEntry> GetDialoguesByCategory(string category)
        {
            List<DialogueEntry> result = new List<DialogueEntry>();
            if (string.IsNullOrEmpty(category)) return result;

            string clean = category.Trim().ToLowerInvariant();
            foreach (var entry in dialogues)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.category) && entry.category.Trim().ToLowerInvariant() == clean)
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a random dialogue tree from a given category (great for merchant rumors/banter).
        /// </summary>
        public DialogueTreeSO GetRandomDialogueByCategory(string category)
        {
            var list = GetDialoguesByCategory(category);
            if (list.Count == 0) return null;

            int randomIndex = Random.Range(0, list.Count);
            return list[randomIndex].GetEffectiveTree();
        }
    }
}
