using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "New_NPCData", menuName = "The Last Empire/NPC Data")]
    public class NPCDataSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for database lookups (e.g. 'merchant_zack', 'dr_pepper')")]
        public string npcId = "merchant_zack";
        public string npcName = "Merchant";
        public string npcTitle = "Wasteland Trader";
        public NPCType npcType = NPCType.Shop;
        public float interactRange = 2.0f;

        [Header("Visual & Appearance")]
        [Tooltip("Optional 3D Model Prefab to replace the default body mesh")]
        public GameObject customModelPrefab;
        [Tooltip("Color tint for visuals or fallback capsule")]
        public Color tintColor = Color.white;

        [Header("Dialogue & Story")]
        [Tooltip("Dialogue ID to fetch from DialogueDatabase (e.g. 'Merchant_Intro')")]
        public string dialogueId = "Merchant_Intro";
        [Tooltip("Direct reference to DialogueTreeSO (optional, overrides dialogueId)")]
        public TheLastEmpire.Story.DialogueTreeSO dialogueTree;
        [Tooltip("Portrait sprite for Dialogue UI")]
        public Sprite npcPortrait;

        [Header("Merchant / Shop Settings")]
        public List<ShopItemConfig> shopItems = new List<ShopItemConfig>();
        public bool isFree = false;
    }
}
