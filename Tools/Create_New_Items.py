import os
import uuid

ITEM_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\Resources_Addressable\Database\ItemData"
DATABASE_ASSET = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\Resources_Addressable\Database\ItemDatabase.asset"

items_to_create = [
    # --- 10 GENERAL CRAFTING MATERIALS (Type 8: Material) ---
    {
        "id": "ITEM0066",
        "asset_name": "ITEM0066_WoodLog",
        "item_name": "Wood Log",
        "desc": "ท่อนไม้เนื้อแข็งที่ตัดทอนจากต้นไม้แห้ง ใช้ก่อกองไฟ สร้างสิ่งปลูกสร้าง หรือคราฟต์ด้ามจับอาวุธ",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.65, 0.42, 0.22),
        "sprite_id": 1622922038
    },
    {
        "id": "ITEM0067",
        "asset_name": "ITEM0067_RoughStone",
        "item_name": "Rough Stone",
        "desc": "หินภูเขาเนื้อแกร่ง มีขอบคมพอสำหรับใช้ขูดสกัดหรือขึ้นรูปเป็นหัวอาวุธและเตาหิน",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.55, 0.58, 0.62),
        "sprite_id": -973460069
    },
    {
        "id": "ITEM0068",
        "asset_name": "ITEM0068_PlantFiber",
        "item_name": "Plant Fiber",
        "desc": "เส้นใยจากเถาวัลย์และเปลือกไม้แห้ง นำไปถักทอเป็นเชือก สายธนู หรือผ้าดิบ",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.52, 0.68, 0.35),
        "sprite_id": -1845263680
    },
    {
        "id": "ITEM0069",
        "asset_name": "ITEM0069_SturdyRope",
        "item_name": "Sturdy Rope",
        "desc": "เชือกถักเกลียวหนา ทนแรงดึงได้สูงมาก เหมาะสำหรับผูกมัดโครงสร้างและคราฟต์กับดัก",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.78, 0.68, 0.50),
        "sprite_id": -400497548
    },
    {
        "id": "ITEM0070",
        "asset_name": "ITEM0070_BoxOfNails",
        "item_name": "Box of Nails",
        "desc": "กล่องบรรจุตะปูเหล็กกล้าสำหรับงานช่าง จำเป็นอย่างยิ่งในการตอกยึดแผ่นไม้และสิ่งกีดขวาง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.75, 0.78, 0.82),
        "sprite_id": -1055355373
    },
    {
        "id": "ITEM0071",
        "asset_name": "ITEM0071_DuctTape",
        "item_name": "Duct Tape",
        "desc": "เทปกาวเหนียวระดับอุตสาหกรรม ซ่อมแซมได้แทบทุกอย่าง ตั้งแต่ด้ามปืนจนถึงชุดเกราะ",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.45, 0.48, 0.52),
        "sprite_id": -732024087
    },
    {
        "id": "ITEM0072",
        "asset_name": "ITEM0072_ScrapSteelPipe",
        "item_name": "Scrap Steel Pipe",
        "desc": "ท่อน้ำเหล็กเก่าที่ตัดออกมา แข็งแรงพอจะใช้เป็นลำกล้องปืนประดิษฐ์หรือด้ามกระบอง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.60, 0.62, 0.65),
        "sprite_id": 217607080
    },
    {
        "id": "ITEM0073",
        "asset_name": "ITEM0073_CharcoalLump",
        "item_name": "Charcoal Lump",
        "desc": "ถ่านไม้คุณภาพดี ให้ความร้อนสม่ำเสมอ เป็นเชื้อเพลิงเตาหลอมและส่วนผสมทำดินปืน",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.22, 0.22, 0.24),
        "sprite_id": 37568664
    },
    {
        "id": "ITEM0074",
        "asset_name": "ITEM0074_GunpowderMix",
        "item_name": "Gunpowder Mix",
        "desc": "ผงดินปืนที่บดผสมสัดส่วนอย่างแม่นยำ วัตถุดิบสำคัญในการบรรจุกระสุนปืนและระเบิดมือ",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.35, 0.35, 0.38),
        "sprite_id": 1929067104
    },
    {
        "id": "ITEM0075",
        "asset_name": "ITEM0075_CleanGlassBottle",
        "item_name": "Clean Glass Bottle",
        "desc": "ขวดแก้วหนาที่ผ่านการล้างทำความสะอาด ใช้กักเก็บน้ำสะอาด ยาปฏิชีวนะ หรือทำระเบิดเพลิง",
        "type": 8,
        "restore": 0,
        "sanity": 0,
        "color": (0.65, 0.85, 0.85),
        "sprite_id": -218275088
    },

    # --- 5 DEDICATED SANITY ITEMS ---
    {
        "id": "ITEM0076",
        "asset_name": "ITEM0076_HotBlackCoffee",
        "item_name": "Hot Black Coffee",
        "desc": "กาแฟดำคั่วเข้มร้อนๆ กลิ่นหอมกรุ่นช่วยปลุกประสาทให้ตื่นตัว ฟื้นฟูสติ +35 ทันที",
        "type": 0, # Potion / Drink
        "restore": 0,
        "sanity": 35,
        "color": (0.42, 0.25, 0.12),
        "sprite_id": -299771230
    },
    {
        "id": "ITEM0077",
        "asset_name": "ITEM0077_PackOfCigarettes",
        "item_name": "Pack of Cigarettes",
        "desc": "ซองบุหรี่หายากในยุคสิ้นโลก ควันอุ่นๆ ช่วยคลายความเครียดสะสมได้ดีเยี่ยม ฟื้นฟูสติ +45",
        "type": 3, # ETC / Consumable
        "restore": 0,
        "sanity": 45,
        "color": (0.85, 0.30, 0.30),
        "sprite_id": 1146315964
    },
    {
        "id": "ITEM0078",
        "asset_name": "ITEM0078_AntiPanicSedatives",
        "item_name": "Anti-Panic Sedatives",
        "desc": "ยาเม็ดระงับประสาทเกรดการแพทย์ ยับยั้งอาการตื่นตระหนกและประสาทหลอน ฟื้นฟูสติ +55",
        "type": 0, # Potion / Medical
        "restore": 0,
        "sanity": 55,
        "color": (0.20, 0.75, 0.95),
        "sprite_id": -1710416233
    },
    {
        "id": "ITEM0079",
        "asset_name": "ITEM0079_HerbalCalmingTea",
        "item_name": "Herbal Calming Tea",
        "desc": "ชาต้มจากสมุนไพรป่า ช่วยให้หัวใจเต้นสงบลง ฟื้นฟูสติ +25 และลดความหิว +15",
        "type": 2, # Food / Bread
        "restore": 15,
        "sanity": 25,
        "color": (0.45, 0.85, 0.55),
        "sprite_id": -12206817
    },
    {
        "id": "ITEM0080",
        "asset_name": "ITEM0080_NostalgicCassetteTape",
        "item_name": "Nostalgic Cassette Tape",
        "desc": "ตลับเทปเพลงยุคเก่าก่อนวันโลกแตก เสียงดนตรีที่คุ้นเคยช่วยดึงจิตใจกลับมาสู่ความหวัง ฟื้นฟูสติ +60",
        "type": 3, # ETC / Music
        "restore": 0,
        "sanity": 60,
        "color": (0.95, 0.65, 0.15),
        "sprite_id": 1755154677
    }
]

created_guids = []

for item in items_to_create:
    asset_file = os.path.join(ITEM_DIR, f"{item['asset_name']}.asset")
    meta_file = f"{asset_file}.meta"
    
    # Generate deterministic or unique 32-hex guid
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

