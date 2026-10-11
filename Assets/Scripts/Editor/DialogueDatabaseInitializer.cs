using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TheLastEmpire.Story;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class DialogueDatabaseInitializer
    {
        static DialogueDatabaseInitializer()
        {
            EditorApplication.delayCall += EnsureDialogueDatabaseExists;
        }

        [MenuItem("Tools/Ensure Dialogue Database Exists")]
        public static void EnsureDialogueDatabaseExists()
        {
            string folderPath = "Assets/Resources/Database";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Database");
            }

            string assetPath = $"{folderPath}/DialogueDatabase.asset";
            DialogueDatabase db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(assetPath);

            if (db == null)
            {
                db = ScriptableObject.CreateInstance<DialogueDatabase>();
                PopulateDefaultDialogues(db);
                AssetDatabase.CreateAsset(db, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"<color=#50FA7B>[DialogueDatabaseInitializer]</color> Created DialogueDatabase at {assetPath}");
            }
        }

        private static void PopulateDefaultDialogues(DialogueDatabase db)
        {
            // 1. Merchant Welcome & Tutorial Intro
            DialogueEntry merchantIntro = new DialogueEntry
            {
                dialogueId = "Merchant_Intro",
                category = "Shop",
                description = "Default welcome greeting from the Outpost Merchant",
                inlineLines = new List<DialogueNode>
                {
                    new DialogueNode
                    {
                        speakerName = "Merchant",
                        dialogueText = "Welcome to the outpost, survivor! The ruins are crawling with danger, but I've got weapons and supplies if you have the cash."
                    },
                    new DialogueNode
                    {
                        speakerName = "Merchant",
                        dialogueText = "Press [E] to trade with me anytime. Or press [Q] if you want to chat or hear rumors about the wasteland!"
                    }
                }
            };

            // 2. Merchant Rumors & Survival Tips
            DialogueEntry merchantRumors = new DialogueEntry
            {
                dialogueId = "Merchant_Rumor",
                category = "Rumor",
                description = "Wasteland survival tips from the Merchant",
                inlineLines = new List<DialogueNode>
                {
                    new DialogueNode
                    {
                        speakerName = "Merchant",
                        dialogueText = "Word of advice: Watch the sky! When night falls, Aliens become much more aggressive."
                    },
                    new DialogueNode
                    {
                        speakerName = "Merchant",
                        dialogueText = "Always keep a stash of ammunition and food in your backpack before stepping through unexplored portals."
                    }
                }
            };

            // 3. Survivor / Story Sample
            DialogueEntry storyGreeting = new DialogueEntry
            {
                dialogueId = "Wanderer_Greeting",
                category = "Story",
                description = "General survivor wasteland greeting",
                inlineLines = new List<DialogueNode>
                {
                    new DialogueNode
                    {
                        speakerName = "Survivor",
                        dialogueText = "Still alive, huh? In this wasteland, that's already a victory."
                    }
                }
            };

            db.dialogues.Add(merchantIntro);
            db.dialogues.Add(merchantRumors);
            db.dialogues.Add(storyGreeting);
        }
    }
}
