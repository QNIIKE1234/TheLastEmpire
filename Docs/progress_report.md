# บันทึกความคืบหน้าการพัฒนาโครงการ (The Last Empire)

**วันที่บันทึก**: 11 พฤศจิกายน 2026  
**สถานะ Git**: `main` (Up to date with `origin/main`)  

---

## 1. ระบบ Log แจ้งเตือนไอเทมที่ได้รับและคราฟต์ (`Log_HUD` & Object Pool 10 ชิ้น)
- **สคริปต์ [`ItemLogHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/ItemLogHUD.cs) และ [`LogThumbnail.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/LogThumbnail.cs)**:
  - เชื่อมกับ Prefab [LogThumbnail.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/UI/LogThumbnail.prefab) และทำงานร่วมกับ `GridLayoutGroup` บน `Log_HUD` ในฉาก [Gameplay_3D.unity](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scenes/Gameplay_3D.unity)
  - **Pre-allocated Object Pool 10 ชิ้น (`poolSize = 10`)**: สร้าง `LogThumbnail_Pool_01` ถึง `LogThumbnail_Pool_10` เตรียมไว้ล่วงหน้าตั้งแต่เริ่มเกม ไม่มีการ `Instantiate` หรือ `Destroy` ระหว่างเล่นเพื่อลดขยะในหน่วยความจำ (Zero Runtime GC)
  - **แจ้งเตือนเมื่อเก็บของ / ได้รับไอเทม (`PlayerInventory.OnItemAdded` / `OnMoneyAdded`)**: แสดงไอคอนและข้อความ `Got <ItemName> x<Amount>` (หรือ `Got Money $<Amount>`)
  - **แจ้งเตือนเมื่อคราฟต์ไอเทมหรือวางสิ่งปลูกสร้าง (`CraftController.OnRecipeCrafted` / `BuildingPlacer.OnBuildingPlaced`)**: แสดงไอคอนและข้อความ `Crafted <Name> x<Amount>`
  - **เวลาแสดงผลและการเรียงลำดับ**: แสดงผลนาน **2 วินาที** (`displayDuration = 2.0f`) พร้อมค่อยๆ จางหาย (`CanvasGroup`) แล้วส่งกลับเข้า Pool (`SetActive(false)`) หากได้รับหลายชิ้นจะเรียงต่อกันลงมาอัตโนมัติ

---

## 2. ระบบคราฟต์ไอเทม โต๊ะคราฟต์ และการซ่อนหมวดหมู่ (`Crafting & Workbench`)
- **แยกเงื่อนไขการใช้โต๊ะคราฟต์ ([`CraftController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CraftController.cs))**:
  - หมวด **`Utility`**, **`Weapons`**, **`Consumables`**, **`Tools`**, **`Special`** สามารถคราฟต์ได้ทุกที่โดยไม่ต้องใช้โต๊ะคราฟต์
  - หมวด **`Building`** และ **`Defense`** ต้องเข้าใกล้โต๊ะคราฟต์ (`Survivor Workbench`) และกดเปิดคราฟต์เท่านั้น
  - ซ่อนปุ่มหมวดหมู่ซ้ายมือ (`buildingTabButton` และ `defenseTabButton`) ด้วย `SetActive(false)` เมื่อไม่ได้อยู่ใกล้โต๊ะคราฟต์ และเปิด `SetActive(true)` เฉพาะเมื่ออยู่ใกล้โต๊ะคราฟต์
- **แก้ไขการโหลดฐานข้อมูลสิ่งปลูกสร้าง ([`BuildingManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/BuildingManager.cs))**:
  - ปรับปรุง `LoadDefaultBuildingDataIfEmpty()` ให้โหลดและรวม `BuildingData` ทั้ง 23 ชิ้นเข้า `allBuildings` เสมอ ทำให้กดปุ่มคราฟต์สิ่งปลูกสร้างแล้วเข้าสู่โหมดเล็งวาง (`BuildingPlacer`) ได้ครบทุกชิ้น
- **ปรับข้อความ UI ในเกมเป็นภาษาอังกฤษทั้งหมด ([`WorkbenchBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/WorkbenchBuilding.cs), [`CraftDescriptionPanel.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CraftDescriptionPanel.cs))**:
  - เปลี่ยน Interaction Prompt ของโต๊ะคราฟต์เป็น `[E] Crafting Table` และคำอธิบายแจ้งเตือนเป็นภาษาอังกฤษทั้งหมด เพื่อให้รองรับกับฟอนต์ `Teko-VariableFont_wght SDF`

---

## 3. โมเดลตัวละคร 3D Rigged ใหม่ 3 ตัว (รองรับ `Chibi_Player_Controller`)
สร้างโมเดล 3D พร้อมโครงกระดูก 18 ชิ้น (`Chibi_Base_Rig`) และ Weight Paint ที่ใช้ร่วมกับแอนิเมชันเดิมของ Player (`Chibi_Player_Controller.controller`) ได้ทันที:
1. **`Chibi_CurlyGirl_Rigged`** ([Chibi_CurlyGirl_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_CurlyGirl_Rigged.fbx) | สคริปต์ [Create_Curly_Glasses_Girl.py](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Curly_Glasses_Girl.py))
   - สาวแว่นผมหยิกสีชมพูมัดหางม้า เสื้อแจ็คเก็ตสีฟ้าอมเขียว กางเกงคาร์โก้สีเหลือง
2. **`Chibi_BlueOperative_Rigged`** ([Chibi_BlueOperative_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_BlueOperative_Rigged.fbx) | Prefab [Chibi_BlueOperative_Rigged.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Character/Model/Chibi_BlueOperative_Rigged.prefab))
   - หนุ่มทรงผม Undercut สีน้ำเงินเข้ม แว่นกันแดดทรงกลมเลนส์ฟ้า หูฟังสื่อสาร เสื้อแจ็คเก็ตยุทธวิธีสีเขียวเข้มพร้อมวิทยุสื่อสาร
3. **`Chibi_HeavyStriker_Rigged`** ([Chibi_HeavyStriker_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_HeavyStriker_Rigged.fbx) | Prefab [Chibi_HeavyStriker_Rigged.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Character/Model/Chibi_HeavyStriker_Rigged.prefab))
   - นักรบเกราะหนักทรงผมตั้งสีคราม แผลเป็นคาดตาซ้าย ผ้าพันคอ เกราะยุทธวิธีพร้อมกระเป๋าแม็กกาซีนและเกราะไหล่

---

## 4. ระบบเสาไฟ `Wooden Lantern Post` (`BUILD0023`) และระบบแยกสิ่งปลูกสร้างตามแมพ
- **เสาไฟไม้ (`BUILD0023_WoodenLanternPost`)**:
  - เพิ่มข้อมูลสิ่งปลูกสร้าง [BUILD0023_WoodenLanternPost.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Database/Buildings/BUILD0023_WoodenLanternPost.asset) และ Prefab [23_Wooden_Lantern_Post.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Environment/Craft/23_Wooden_Lantern_Post.prefab) พร้อมไฟ Point Light สีส้มอบอุ่นที่เปิดอัตโนมัติในเวลากลางคืน
- **ระบบแยกสิ่งปลูกสร้างตามพิกัดแมพ `(StageX, StageY)`**:
  - สิ่งปลูกสร้างทุกชิ้นผูกกับพิกัดห้อง `(StageX, StageY)` ไม่ตามผู้เล่นไปแมพอื่น และแสดงผลกลับมาครบถ้วนเมื่อเดินกลับห้องเดิม
