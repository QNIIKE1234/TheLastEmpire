using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TheLastEmpire;

namespace TheLastEmpire.Editor
{
    [InitializeOnLoad]
    public static class NPCDatabaseInitializer
    {
        static NPCDatabaseInitializer()
        {
            // ตรวจสอบเฉพาะตอนที่ยังไม่มีไฟล์ Database เท่านั้น จะไม่รันเขียนทับการตั้งค่า Inspector ของผู้พัฒนาทุกครั้งที่ Compile
            if (!System.IO.File.Exists("Assets/Resources_Addressable/Database/NPCDatabase.asset"))
            {
                EditorApplication.delayCall += EnsureNPCDatabaseExists;
            }
        }

        [MenuItem("Tools/The Last Empire/Initialize NPC Database & Presets")]
        public static void EnsureNPCDatabaseExists()
        {
            string dbFolder = "Assets/Resources_Addressable/Database";
            string npcDataFolder = $"{dbFolder}/NPCData";

            if (!AssetDatabase.IsValidFolder("Assets/Resources_Addressable"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources_Addressable");
            }
            if (!AssetDatabase.IsValidFolder(dbFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources_Addressable", "Database");
            }
            if (!AssetDatabase.IsValidFolder(npcDataFolder))
            {
                AssetDatabase.CreateFolder(dbFolder, "NPCData");
            }

            // 1. Create Zack (Default Merchant)
            NPCDataSO zack = EnsureNPCData($"{npcDataFolder}/NPC_Merchant_Zack.asset", "merchant_zack", "Zack", "Outpost Merchant", NPCType.Shop, "Merchant_Intro");
            if (zack.customModelPrefab == null)
            {
                zack.customModelPrefab = LoadDefaultModel("NPC_Zack_Merchant_Rigged", "NPC_Zack_Merchant_Rigged");
                EditorUtility.SetDirty(zack);
            }
            PopulateZackInventory(zack);

            // 2. Create Dr. Elena (Field Medic)
            NPCDataSO elena = EnsureNPCData($"{npcDataFolder}/NPC_Doctor_Elena.asset", "doctor_elena", "Dr. Elena", "Combat Medic", NPCType.Shop, "Merchant_Intro");
            if (elena.customModelPrefab == null)
            {
                elena.customModelPrefab = LoadDefaultModel("NPC_Elena_Medic_Rigged", "NPC_Elena_Medic_Rigged");
                EditorUtility.SetDirty(elena);
            }
            PopulateMedicInventory(elena);

            // 3. Create Otto (Weaponsmith)
            NPCDataSO otto = EnsureNPCData($"{npcDataFolder}/NPC_Mechanic_Otto.asset", "mechanic_otto", "Otto", "Imperial Gunsmith", NPCType.Shop, "Merchant_Intro");
            if (otto.customModelPrefab == null)
            {
                otto.customModelPrefab = LoadDefaultModel("NPC_Otto_Gunsmith_Rigged", "NPC_Otto_Gunsmith_Rigged");
                EditorUtility.SetDirty(otto);
            }
            PopulateGunsmithInventory(otto);

            // 4. Create Maya (Scout / Story NPC)
            NPCDataSO maya = EnsureNPCData($"{npcDataFolder}/NPC_Watcher_Maya.asset", "watcher_maya", "Maya", "Imperial Informant", NPCType.StoryNPC, "Merchant_Rumor");
            if (maya.customModelPrefab == null)
            {
                maya.customModelPrefab = LoadDefaultModel("NPC_Maya_Scout_Rigged", "NPC_Maya_Scout_Rigged");
                EditorUtility.SetDirty(maya);
            }

            // 5. Create Roy (Old Scavenger Veteran)
            NPCDataSO roy = EnsureNPCData($"{npcDataFolder}/NPC_Survivor_Roy.asset", "survivor_roy", "Old Roy", "Grizzled Wasteland Scavenger", NPCType.Shop, "Merchant_Rumor");
            if (roy.customModelPrefab == null)
            {
                roy.customModelPrefab = LoadDefaultModel("NPC_Roy_Veteran_Rigged", "NPC_Roy_Veteran_Rigged");
                EditorUtility.SetDirty(roy);
            }
            PopulateRoyInventory(roy);

            // 6. Ensure NPCDatabase asset exists and links all NPCs
            string dbPath = $"{dbFolder}/NPCDatabase.asset";
            NPCDatabase db = AssetDatabase.LoadAssetAtPath<NPCDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<NPCDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            db.npcs = new List<NPCDataSO> { zack, elena, otto, maya, roy };
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=#50FA7B>[NPCDatabaseInitializer]</color> NPC Database & Data-Driven Presets initialized at {dbPath} (Total: {db.npcs.Count} NPCs)");
        }

        private static GameObject LoadDefaultModel(string prefabName, string fbxName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources_Addressable/Prefabs/Character/{prefabName}.prefab");
            if (prefab != null) return prefab;
            return AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GeneratedModels/NPCs/{fbxName}.fbx");
        }

        private static NPCDataSO EnsureNPCData(string path, string id, string name, string title, NPCType type, string dialogueId)
        {
            NPCDataSO data = AssetDatabase.LoadAssetAtPath<NPCDataSO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<NPCDataSO>();
                data.npcId = id;
                data.npcName = name;
                data.npcTitle = title;
                data.npcType = type;
                data.dialogueId = dialogueId;
                AssetDatabase.CreateAsset(data, path);
            }
            return data;
        }

        private static void PopulateZackInventory(NPCDataSO npc)
        {
            if (ItemDatabase.Instance == null) return;
            if (npc.shopItems != null && npc.shopItems.Count > 0) return;
            npc.shopItems.Clear();

            string[] defaultItems = new string[]
            {
                "Pistol", "Revolver", "SMG", "Shotgun", "Rifle", "Crossbow",
                "Knife", "Fire Axe", "Katana", "Sledgehammer",
                "Bandage", "First Aid Kit", "Potion", "Adrenaline Shot",
                "Bread", "Energy Bar", "Canned Food",
                "Pistol Ammo", "Shotgun Ammo", "Rifle Ammo"
            };

            foreach (var itemName in defaultItems)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(itemName);
                if (item != null)
                {
                    int price = ShopDatabase.Instance != null ? ShopDatabase.Instance.GetDefaultPrice(itemName) : 100;
                    npc.shopItems.Add(new ShopItemConfig { item = item, price = price });
                }
            }
            EditorUtility.SetDirty(npc);
        }

        private static void PopulateMedicInventory(NPCDataSO npc)
        {
            if (ItemDatabase.Instance == null) return;
            if (npc.shopItems != null && npc.shopItems.Count > 0) return;
            npc.shopItems.Clear();

            string[] medicItems = new string[]
            {
                "Bandage", "First Aid Kit", "Potion", "Adrenaline Shot",
                "Bread", "Energy Bar", "Canned Food", "Cooked Meat"
            };

            foreach (var itemName in medicItems)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(itemName);
                if (item != null)
                {
                    int price = ShopDatabase.Instance != null ? ShopDatabase.Instance.GetDefaultPrice(itemName) : 50;
                    npc.shopItems.Add(new ShopItemConfig { item = item, price = price });
                }
            }
            EditorUtility.SetDirty(npc);
        }

        private static void PopulateGunsmithInventory(NPCDataSO npc)
        {
            if (ItemDatabase.Instance == null) return;
            if (npc.shopItems != null && npc.shopItems.Count > 0) return;
            npc.shopItems.Clear();

            string[] gunsmithItems = new string[]
            {
                "Sniper Rifle", "Rocket Launcher", "Flamethrower", "Rifle", "Shotgun",
                "Sledgehammer", "Survival Spear", "Stun Baton",
                "Pistol Ammo", "Shotgun Ammo", "Rifle Ammo", "Heavy Ammo", "Bolts"
            };

            foreach (var itemName in gunsmithItems)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(itemName);
                if (item != null)
                {
                    int price = ShopDatabase.Instance != null ? ShopDatabase.Instance.GetDefaultPrice(itemName) : 200;
                    npc.shopItems.Add(new ShopItemConfig { item = item, price = price });
                }
            }
            EditorUtility.SetDirty(npc);
        }

        private static void PopulateRoyInventory(NPCDataSO npc)
        {
            if (ItemDatabase.Instance == null) return;
            if (npc.shopItems != null && npc.shopItems.Count > 0) return;
            npc.shopItems.Clear();

            string[] scavItems = new string[]
            {
                "Crossbow", "Survival Spear", "Knife", "Bandage", "Bread", "Bolts"
            };

            foreach (var itemName in scavItems)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(itemName);
                if (item != null)
                {
                    int price = ShopDatabase.Instance != null ? ShopDatabase.Instance.GetDefaultPrice(itemName) : 80;
                    npc.shopItems.Add(new ShopItemConfig { item = item, price = price });
                }
            }
            EditorUtility.SetDirty(npc);
        }
    }
}

