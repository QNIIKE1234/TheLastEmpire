import os
import uuid

BASE_DIR = r"d:\TLEGIT\TheLastEmpire"
DB_DIR = os.path.join(BASE_DIR, "Assets", "Resources_Addressable", "Database")
NPC_DATA_DIR = os.path.join(DB_DIR, "NPCData")
os.makedirs(NPC_DATA_DIR, exist_ok=True)

NPC_DATA_SO_GUID = "5d6f4e3c2b1a09874e3c2b1a09874e3c"
NPC_DATABASE_GUID = "6e7f5a4b3c2d1e0f6e7f5a4b3c2d1e0f"
PORTRAIT_PEPPER_GUID = "1a6b6eaa23bf9454f9e69c5d023efd0a"

def make_meta(asset_guid):
    return f"""fileFormatVersion: 2
guid: {asset_guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

# Extract Zack's shop items from NPCPrefabs.prefab
prefab_path = os.path.join(BASE_DIR, "Assets", "Resources_Addressable", "Prefabs", "Character", "NPCPrefabs.prefab")
with open(prefab_path, "r", encoding="utf-8") as f:
    prefab_text = f.read()

import re
shop_items_match = re.search(r"  shopItems:\n(    - item:.*\n(?:      .*\n)*?|  - item:[\s\S]*?)(?=  isFree:)", prefab_text)
zack_shop_items = shop_items_match.group(1) if shop_items_match else ""

# Zack GUID
zack_guid = "9a1b2c3d4e5f60718293a4b5c6d7e8f0"
elena_guid = "9a1b2c3d4e5f60718293a4b5c6d7e8f1"
otto_guid = "9a1b2c3d4e5f60718293a4b5c6d7e8f2"
maya_guid = "9a1b2c3d4e5f60718293a4b5c6d7e8f3"
npc_db_guid = "9a1b2c3d4e5f60718293a4b5c6d7e8f4"

# 1. NPC_Merchant_Zack.asset
zack_asset = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {NPC_DATA_SO_GUID}, type: 3}}
  m_Name: NPC_Merchant_Zack
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.NPCDataSO
  npcId: merchant_zack
  npcName: Zack
  npcTitle: Outpost Merchant
  npcType: 0
  interactRange: 2
  customModelPrefab: {{fileID: 0}}
  tintColor: {{r: 1, g: 1, b: 1, a: 1}}
  dialogueId: Merchant_Intro
  dialogueTree: {{fileID: 0}}
  npcPortrait: {{fileID: 21300000, guid: {PORTRAIT_PEPPER_GUID}, type: 3}}
  shopItems:
{zack_shop_items.rstrip()}
  isFree: 0
"""
zack_path = os.path.join(NPC_DATA_DIR, "NPC_Merchant_Zack.asset")
with open(zack_path, "w", encoding="utf-8") as f:
    f.write(zack_asset)
with open(zack_path + ".meta", "w", encoding="utf-8") as f:
    f.write(make_meta(zack_guid))

# 2. Doctor Elena
elena_items = """  - item: {fileID: 11400000, guid: 0474284a0f7c41759bf6f9bbfffcd5e2, type: 2}
    price: 45
  - item: {fileID: 11400000, guid: 517bae9a5ddb4522ad3256572a318f6a, type: 2}
    price: 110
  - item: {fileID: 11400000, guid: 99259f2e874f60343bc28d427eb691cb, type: 2}
    price: 100
  - item: {fileID: 11400000, guid: c33302859d1f46628b64be3284327d23, type: 2}
    price: 260
  - item: {fileID: 11400000, guid: d0bcb8dc8d676ba428263cdcb07a2324, type: 2}
    price: 50
  - item: {fileID: 11400000, guid: cc190728f5674b67a51afeb3a29c53f7, type: 2}
    price: 65"""

elena_asset = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {NPC_DATA_SO_GUID}, type: 3}}
  m_Name: NPC_Doctor_Elena
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.NPCDataSO
  npcId: doctor_elena
  npcName: Dr. Elena
  npcTitle: Field Combat Medic
  npcType: 0
  interactRange: 2.2
  customModelPrefab: {{fileID: 0}}
  tintColor: {{r: 0.3, g: 0.85, b: 0.5, a: 1}}
  dialogueId: Merchant_Intro
  dialogueTree: {{fileID: 0}}
  npcPortrait: {{fileID: 21300000, guid: {PORTRAIT_PEPPER_GUID}, type: 3}}
  shopItems:
{elena_items}
  isFree: 0
"""
elena_path = os.path.join(NPC_DATA_DIR, "NPC_Doctor_Elena.asset")
with open(elena_path, "w", encoding="utf-8") as f:
    f.write(elena_asset)
with open(elena_path + ".meta", "w", encoding="utf-8") as f:
    f.write(make_meta(elena_guid))

# 3. Otto (Gunsmith)
otto_items = """  - item: {fileID: 11400000, guid: 994b8a468fb719744bf6cb1e0d620037, type: 2}
    price: 2400
  - item: {fileID: 11400000, guid: 29748bd8c38e2734e9a328da32e89353, type: 2}
    price: 6700
  - item: {fileID: 11400000, guid: 27aeb4710cfe4359ad2edcaeeaf74dab, type: 2}
    price: 3200
  - item: {fileID: 11400000, guid: 56b668d5a62c4cdf91e1f82e00e206f6, type: 2}
    price: 5500
  - item: {fileID: 11400000, guid: e448b9ce40024846b2fce004f8d6ab08, type: 2}
    price: 4800
  - item: {fileID: 11400000, guid: 932483d85c764320b9bded3bae727763, type: 2}
    price: 7500
  - item: {fileID: 11400000, guid: 2f5aa5e0ab314f89be7b2d4a7a672a82, type: 2}
    price: 140
  - item: {fileID: 11400000, guid: 6e078f72ffab7514bb76c21f650d3e3b, type: 2}
    price: 100"""

otto_asset = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {NPC_DATA_SO_GUID}, type: 3}}
  m_Name: NPC_Mechanic_Otto
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.NPCDataSO
  npcId: mechanic_otto
  npcName: Otto
  npcTitle: Imperial Gunsmith
  npcType: 0
  interactRange: 2.0
  customModelPrefab: {{fileID: 0}}
  tintColor: {{r: 1.0, g: 0.65, b: 0.2, a: 1}}
  dialogueId: Merchant_Intro
  dialogueTree: {{fileID: 0}}
  npcPortrait: {{fileID: 0}}
  shopItems:
{otto_items}
  isFree: 0
"""
otto_path = os.path.join(NPC_DATA_DIR, "NPC_Mechanic_Otto.asset")
with open(otto_path, "w", encoding="utf-8") as f:
    f.write(otto_asset)
with open(otto_path + ".meta", "w", encoding="utf-8") as f:
    f.write(make_meta(otto_guid))

# 4. Maya (Story NPC)
maya_asset = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {NPC_DATA_SO_GUID}, type: 3}}
  m_Name: NPC_Watcher_Maya
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.NPCDataSO
  npcId: watcher_maya
  npcName: Maya
  npcTitle: Imperial Scout & Informant
  npcType: 2
  interactRange: 2.5
  customModelPrefab: {{fileID: 0}}
  tintColor: {{r: 0.7, g: 0.45, b: 1.0, a: 1}}
  dialogueId: Merchant_Rumor
  dialogueTree: {{fileID: 0}}
  npcPortrait: {{fileID: 0}}
  shopItems: []
  isFree: 0
"""
maya_path = os.path.join(NPC_DATA_DIR, "NPC_Watcher_Maya.asset")
with open(maya_path, "w", encoding="utf-8") as f:
    f.write(maya_asset)
with open(maya_path + ".meta", "w", encoding="utf-8") as f:
    f.write(make_meta(maya_guid))

# 5. NPCDatabase.asset
db_asset = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {NPC_DATABASE_GUID}, type: 3}}
  m_Name: NPCDatabase
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.NPCDatabase
  npcs:
  - {{fileID: 11400000, guid: {zack_guid}, type: 2}}
  - {{fileID: 11400000, guid: {elena_guid}, type: 2}}
  - {{fileID: 11400000, guid: {otto_guid}, type: 2}}
  - {{fileID: 11400000, guid: {maya_guid}, type: 2}}
"""
db_path = os.path.join(DB_DIR, "NPCDatabase.asset")
with open(db_path, "w", encoding="utf-8") as f:
    f.write(db_asset)
with open(db_path + ".meta", "w", encoding="utf-8") as f:
    f.write(make_meta(npc_db_guid))

# 6. NPCData folder meta
npc_data_meta = """fileFormatVersion: 2
guid: 8b2c3d4e5f60718293a4b5c6d7e8f001
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
with open(os.path.join(DB_DIR, "NPCData.meta"), "w", encoding="utf-8") as f:
    f.write(npc_data_meta)

print("Successfully generated all NPC assets, ScriptableObjects, and NPCDatabase!")

