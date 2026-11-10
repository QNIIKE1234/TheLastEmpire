using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "NPCDatabase", menuName = "The Last Empire/NPC Database")]
    public class NPCDatabase : ScriptableObject
    {
        private static NPCDatabase _instance;
        public static NPCDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AddressableManager.LoadAsset<NPCDatabase>("Database/NPCDatabase");
                    if (_instance == null)
                    {
                        _instance = AddressableManager.LoadAsset<NPCDatabase>("NPCDatabase");
                    }
                    if (_instance == null)
                    {
                        NPCDatabase[] databases = Resources.FindObjectsOfTypeAll<NPCDatabase>();
                        if (databases != null && databases.Length > 0)
                        {
                            _instance = databases[0];
                        }
                    }
                    if (_instance == null)
                    {
                        var all = Resources.LoadAll<NPCDatabase>("");
                        if (all != null && all.Length > 0)
                        {
                            _instance = all[0];
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("Registered NPCs")]
        public List<NPCDataSO> npcs = new List<NPCDataSO>();

        public NPCDataSO GetNPCById(string id)
        {
            if (string.IsNullOrEmpty(id) || npcs == null) return null;
            return npcs.Find(n => n != null && string.Equals(n.npcId, id, StringComparison.OrdinalIgnoreCase));
        }

        public NPCDataSO GetDefaultMerchant()
        {
            NPCDataSO merchant = GetNPCById("merchant_zack");
            if (merchant != null) return merchant;
            if (npcs != null)
            {
                return npcs.Find(n => n != null && n.npcType == NPCType.Shop);
            }
            return null;
        }

        public List<NPCDataSO> GetNPCsByType(NPCType type)
        {
            List<NPCDataSO> list = new List<NPCDataSO>();
            if (npcs == null) return list;
            foreach (var n in npcs)
            {
                if (n != null && n.npcType == type)
                {
                    list.Add(n);
                }
            }
            return list;
        }
    }
}
