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
            EditorApplication.delayCall += EnsureNPCDatabaseExists;
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
            zack.customModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedModels/NPCs/NPC_Zack_Merchant_Rigged.fbx");
            PopulateZackInventory(zack);

            // 2. Create Dr. Elena (Field Medic)
            NPCDataSO elena = EnsureNPCData($"{npcDataFolder}/NPC_Doctor_Elena.asset", "doctor_elena", "Dr. Elena", "Combat Medic", NPCType.Shop, "Merchant_Intro");
            elena.customModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedModels/NPCs/NPC_Elena_Medic_Rigged.fbx");
            elena.tintColor = Color.white;
            PopulateMedicInventory(elena);

            // 3. Create Otto (Weaponsmith)
            NPCDataSO otto = EnsureNPCData($"{npcDataFolder}/NPC_Mechanic_Otto.asset", "mechanic_otto", "Otto", "Imperial Gunsmith", NPCType.Shop, "Merchant_Intro");
            otto.customModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedModels/NPCs/NPC_Otto_Gunsmith_Rigged.fbx");
            otto.tintColor = Color.white;
            PopulateGunsmithInventory(otto);

            // 4. Create Maya (Scout / Story NPC)
            NPCDataSO maya = EnsureNPCData($"{npcDataFolder}/NPC_Watcher_Maya.asset", "watcher_maya", "Maya", "Imperial Informant", NPCType.StoryNPC, "Merchant_Rumor");
            maya.customModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedModels/NPCs/NPC_Maya_Scout_Rigged.fbx");
            maya.tintColor = Color.white;

            // 5. Create Roy (Old Scavenger Veteran)
            NPCDataSO roy = EnsureNPCData($"{npcDataFolder}/NPC_Survivor_Roy.asset", "survivor_roy", "Old Roy", "Grizzled Wasteland Scavenger", NPCType.Shop, "Merchant_Rumor");
            roy.customModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedModels/NPCs/NPC_Roy_Veteran_Rigged.fbx");
            roy.tintColor = Color.white;
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
            AssetDatabase.Refresh();

            Debug.Log($"<color=#50FA7B>[NPCDatabaseInitializer]</color> NPC Database & Data-Driven Presets initialized at {dbPath} (Total: {db.npcs.Count} NPCs)");
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
            else
            {
                data.npcId = id;
                data.npcName = name;
                data.npcTitle = title;
                data.npcType = type;
                data.dialogueId = dialogueId;
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        private static void PopulateZackInventory(NPCDataSO npc)
        {
            if (ItemDatabase.Instance == null) return;
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

