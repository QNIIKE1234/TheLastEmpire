import os
import uuid

ITEM_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\Resources_Addressable\Database\ItemData"
DATABASE_ASSET = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\Resources_Addressable\Database\ItemDatabase.asset"

crafting_items = [
    {
        "id": "ITEM0081",
        "asset_name": "ITEM0081_ScrapLeather",
        "item_name": "Scrap Leather",
        "desc": "เศษหนังสัตว์เหนียวผ่านการฟอก ใช้ตัดเย็บซองกระสุน ด้ามจับมีด หรือสายรัดเกราะ",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.58, 0.35, 0.18),
        "sprite_id": 1332722573
    },
    {
        "id": "ITEM0082",
        "asset_name": "ITEM0082_CopperWireSpool",
        "item_name": "Copper Wire Spool",
        "desc": "ลวดทองแดงนำไฟฟ้าความบริสุทธิ์สูง ใช้เดินสายกับดักไฟฟ้า อุปกรณ์สื่อสาร และชิ้นส่วนสวิตช์ชนวน",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.85, 0.53, 0.30),
        "sprite_id": 1179007688
    },
    {
        "id": "ITEM0083",
        "asset_name": "ITEM0083_IndustrialGlue",
        "item_name": "Industrial Glue",
        "desc": "กาวอีพ็อกซีสองส่วนผสม ยึดเกาะแน่นหนาทนความร้อน ใช้ประสานเนื้อไม้ โลหะ และพลาสติกแข็ง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.90, 0.75, 0.25),
        "sprite_id": -1296086851
    },
    {
        "id": "ITEM0084",
        "asset_name": "ITEM0084_RubberStrips",
        "item_name": "Rubber Strips",
        "desc": "ยางตัดจากยางในรถยนต์ มีความยืดหยุ่นและกันน้ำ ใช้ทำสายหนังสติ๊ก ซีลกันรั่ว หรือรองข้อต่อกันกระแทก",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.28, 0.28, 0.30),
        "sprite_id": 2060257239
    },
    {
        "id": "ITEM0085",
        "asset_name": "ITEM0085_SulfurPowder",
        "item_name": "Sulfur Powder",
        "desc": "ผงแร่กำมะถันจากเหมืองหิน ส่วนผสมตั้งต้นในการปรุงดินปืน น้ำกรด และสารเคมีไล่แมลง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.92, 0.88, 0.20),
        "sprite_id": 1781142183
    },
    {
        "id": "ITEM0086",
        "asset_name": "ITEM0086_RefinedFuelCan",
        "item_name": "Refined Fuel Can",
        "desc": "น้ำมันเชื้อเพลิงผ่านการกลั่น เหมาะสำหรับเติมเครื่องปั่นไฟ คราฟต์ระเบิดขวดโมโลตอฟ หรือใช้เป็นเชื้อเพลิงคบเพลิง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.88, 0.45, 0.12),
        "sprite_id": 742553694
    },
    {
        "id": "ITEM0087",
        "asset_name": "ITEM0087_SpringSteelCoil",
        "item_name": "Spring Steel Coil",
        "desc": "สปริงขดลวดเหล็กกล้าแรงดีดสูง ใช้ในระบบลั่นไกปืน กับดักหมี และกลไกสลักระเบิด",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.50, 0.60, 0.65),
        "sprite_id": -1843549058
    },
    {
        "id": "ITEM0088",
        "asset_name": "ITEM0088_HardenedResin",
        "item_name": "Hardened Resin",
        "desc": "ยางไม้สนสกัดแห้ง นำไปหลอมเป็นสารกันซึม น้ำยาประสานลูกธนู หรือใช้ช่วยเร่งการติดไฟของคบเพลิง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.85, 0.60, 0.15),
        "sprite_id": 1747125092
    },
    {
        "id": "ITEM0089",
        "asset_name": "ITEM0089_CeramicShard",
        "item_name": "Ceramic Shard",
        "desc": "ชิ้นส่วนแผ่นเซรามิกหนาแน่นสูงที่ทนความร้อนและแรงกระแทกได้ดี ใช้เสริมเกราะกันกระสุนและเครื่องบด",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.82, 0.82, 0.85),
        "sprite_id": -1430548115
    },
    {
        "id": "ITEM0090",
        "asset_name": "ITEM0090_CleanWaterFilter",
        "item_name": "Clean Water Filter",
        "desc": "ชุดไส้กรองถ่านกัมมันต์และทรายละเอียด ใช้คราฟต์ถังกรองน้ำสะอาดประจำฐานที่พักเพื่อสุขอนามัย",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.40, 0.75, 0.85),
        "sprite_id": 83372190
    }
]

created_guids = []

for item in crafting_items:
    asset_file = os.path.join(ITEM_DIR, f"{item['asset_name']}.asset")
    meta_file = f"{asset_file}.meta"
    
    item_guid = uuid.uuid5(uuid.NAMESPACE_DNS, item["asset_name"]).hex
    created_guids.append(item_guid)
    
    r, g, b = item["color"]
    
    asset_content = f"""%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: f66e274d5d4a559448705b384dc143eb, type: 3}}
  m_Name: {item['asset_name']}
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.ItemData
  itemName: {item['item_name']}
  description: "{item['desc']}"
  icon: {{fileID: {item['sprite_id']}, guid: bddb817137cb8eb42958dae007d7f9a0, type: 3}}
  type: {item['type']}
  restorationValue: {item['restore']}
  sanityRestorationValue: {item['sanity']}
  defaultQuantity: 1
  themeColor: {{r: {r:.2f}, g: {g:.2f}, b: {b:.2f}, a: 1}}
  dropMaterial: {{fileID: 2100000, guid: 20ba20516012b33707aa691d0ac26f98, type: 2}}
  damage: 0
  attackRadius: 0
  attackRate: 0
  knockbackForce: 0
  staggerDuration: 0
  weaponConfig:
    projectilePrefab: {{fileID: 0}}
    fireRate: 0.2
    magazineSize: 12
    reloadDuration: 1
    range: 3
    canPierce: 0
    spreadAngle: 0
    pelletsPerShot: 1
    isAutomatic: 0
    ammoType: 
    vfxPoolKey: 
"""

    meta_content = f"""fileFormatVersion: 2
guid: {item_guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

    with open(asset_file, "w", encoding="utf-8") as f:
        f.write(asset_content)
    with open(meta_file, "w", encoding="utf-8") as f:
        f.write(meta_content)
    
    print(f"Created {item['asset_name']}.asset (GUID: {item_guid})")

# Append new items to ItemDatabase.asset
if os.path.exists(DATABASE_ASSET):
    with open(DATABASE_ASSET, "r", encoding="utf-8") as f:
        db_text = f.read()

    new_entries = []
    for g in created_guids:
        entry = f"  - {{fileID: 11400000, guid: {g}, type: 2}}"
        if entry not in db_text:
            new_entries.append(entry)

    if new_entries:
        db_text = db_text.rstrip() + "\n" + "\n".join(new_entries) + "\n"
        with open(DATABASE_ASSET, "w", encoding="utf-8") as f:
            f.write(db_text)
        print(f"Appended {len(new_entries)} items to ItemDatabase.asset!")
    else:
        print("All items were already in ItemDatabase.asset.")

