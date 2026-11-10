# สรุปบันทึกการพัฒนาเกม (Work Summary) - 11 พฤศจิกายน 2026

เอกสารฉบับนี้จัดทำขึ้นเพื่อรวบรวมความคืบหน้าการพัฒนาโปรเจกต์ **The Last Empire** ล่าสุด ครอบคลุมระบบ Log แจ้งเตือนไอเทมแบบ Object Pool 10 ชิ้น, ระบบโต๊ะคราฟต์และการซ่อนแท็บหมวดหมู่, การสร้างโมเดล 3D ตัวละคร Rigged ใหม่ 3 ตัว (รองรับ `Chibi_Player_Controller`), ระบบเสาไฟ `Wooden Lantern Post`, และการแยกสิ่งปลูกสร้างตามพิกัดแมพ `(StageX, StageY)`

---

## 📑 สารบัญระบบที่พัฒนาและอัปเดตล่าสุด
1. [ระบบ Log แจ้งเตือนไอเทมที่ได้รับและคราฟต์ (Log_HUD & 10-Item Object Pool)](#1-ระบบ-log-แจ้งเตือนไอเทมที่ได้รับและคราฟต์-log_hud--10-item-object-pool)
2. [ระบบคราฟต์ไอเทม โต๊ะคราฟต์ และการซ่อนแท็บหมวดหมู่ (Crafting & Workbench)](#2-ระบบคราฟต์ไอเทม-โต๊ะคราฟต์-และการซ่อนแท็บหมวดหมู่-crafting--workbench)
3. [การสร้างโมเดลตัวละคร 3D Rigged ใหม่ 3 ตัว (รองรับ Chibi_Player_Controller)](#3-การสร้างโมเดลตัวละคร-3d-rigged-ใหม่-3-ตัว-รองรับ-chibi_player_controller)
4. [ระบบเสาไฟไม้ Wooden Lantern Post (BUILD0023) และระบบแยกสิ่งปลูกสร้างตามแมพ](#4-ระบบเสาไฟไม้-wooden-lantern-post-build0023-และระบบแยกสิ่งปลูกสร้างตามแมพ)

---

## 1. ระบบ Log แจ้งเตือนไอเทมที่ได้รับและคราฟต์ (`Log_HUD` & 10-Item Object Pool)

พัฒนาสคริปต์ [`ItemLogHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/ItemLogHUD.cs) และ [`LogThumbnail.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/LogThumbnail.cs) เชื่อมต่อกับ [LogThumbnail.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/UI/LogThumbnail.prefab) และ `Log_HUD` (`GridLayoutGroup`) ในฉาก [Gameplay_3D.unity](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scenes/Gameplay_3D.unity):

* **Pre-allocated Object Pool 10 ชิ้น (`poolSize = 10`):**
  * เตรียม `LogThumbnail_Pool_01` ถึง `LogThumbnail_Pool_10` ไว้ภายใต้ `Log_HUD` ตั้งแต่เริ่มเกม (ซ่อนด้วย `SetActive(false)`)
  * ทำงานแบบ Zero Runtime GC ไม่มีการ `Instantiate` หรือ `Destroy` ระหว่างเล่น
* **การแสดงผลและอายุของแจ้งเตือน:**
  * เมื่อเก็บไอเทม (`PlayerInventory.OnItemAdded` / `OnMoneyAdded`): แสดงไอคอนพร้อมข้อความ `Got <ItemName> x<Amount>` (หรือ `Got Money $<Amount>`)
  * เมื่อคราฟต์ไอเทมหรือวางสิ่งปลูกสร้าง (`CraftController.OnRecipeCrafted` / `BuildingPlacer.OnBuildingPlaced`): แสดงไอคอนพร้อมข้อความ `Crafted <Name> x<Amount>`
  * แสดงผลนาน **2 วินาที** (`displayDuration = 2.0f`) พร้อมเอฟเฟกต์ค่อยๆ จางหาย (`CanvasGroup`) ในช่วง `0.35` วินาทีสุดท้าย แล้วส่งกลับเข้า Pool ทันที
  * กรณีได้รับไอเทมหลายชิ้นต่อเนื่อง จะใช้ `SetAsLastSibling()` ร่วมกับ `GridLayoutGroup` เพื่อเรียงต่อกันลงมาอัตโนมัติ

---

## 2. ระบบคราฟต์ไอเทม โต๊ะคราฟต์ และการซ่อนแท็บหมวดหมู่ (`Crafting & Workbench`)

ปรับปรุง [`CraftController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CraftController.cs), [`BuildingManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/BuildingManager.cs), [`WorkbenchBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/WorkbenchBuilding.cs), และ [`CraftDescriptionPanel.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CraftDescriptionPanel.cs):

* **เงื่อนไขการใช้โต๊ะคราฟต์ (`Survivor Workbench`):**
  * หมวด **`Utility`**, **`Weapons`**, **`Consumables`**, **`Tools`**, และ **`Special`** สามารถกดคราฟต์ได้ทุกที่โดยไม่ต้องอยู่ใกล้โต๊ะคราฟต์
  * เฉพาะหมวด **`Building`** และ **`Defense`** เท่านั้นที่ต้องเดินเข้าใกล้โต๊ะคราฟต์แล้วกดใช้งาน
  * ซ่อนปุ่มแท็บหมวดหมู่ซ้ายมือ (`buildingTabButton` และ `defenseTabButton`) ด้วย `SetActive(false)` เมื่อไม่ได้อยู่ใกล้โต๊ะคราฟต์ และเปิด `SetActive(true)` เฉพาะเมื่ออยู่ใกล้โต๊ะคราฟต์
* **การโหลดข้อมูลสิ่งปลูกสร้างครบ 23 ชนิด:**
  * ปรับปรุง `BuildingManager.LoadDefaultBuildingDataIfEmpty()` ให้รวม `BuildingData` ทั้ง 23 ชิ้นเข้า `allBuildings` เสมอ เพื่อให้กดปุ่มคราฟต์สิ่งปลูกสร้างแล้วเข้าสู่โหมดเล็งวาง (`BuildingPlacer`) ได้ครบทุกชิ้น
* **รองรับฟอนต์ภาษาอังกฤษ (`Teko-VariableFont_wght SDF`):**
  * เปลี่ยนข้อความ Interaction Prompt ของโต๊ะคราฟต์เป็น `[E] Crafting Table` และข้อความแจ้งเตือนใน `CraftDescriptionPanel` เป็นภาษาอังกฤษทั้งหมด

---

## 3. การสร้างโมเดลตัวละคร 3D Rigged ใหม่ 3 ตัว (รองรับ `Chibi_Player_Controller`)

สร้างโมเดลตัวละคร 3D Chibi ด้วย Blender Python Pipeline พร้อมโครงกระดูกมาตรฐาน 18 ชิ้น (`Chibi_Base_Rig`) ที่ใช้ร่วมกับ `Chibi_Player_Controller.controller` และแอนิเมชันทั้ง 11 ท่าของ Player ได้ทันที:

1. **`Chibi_CurlyGirl_Rigged`** ([Chibi_CurlyGirl_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_CurlyGirl_Rigged.fbx) | [Create_Curly_Glasses_Girl.py](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Curly_Glasses_Girl.py))
   * สาวแว่นผมหยิกสีชมพูมัดหางม้า เสื้อแจ็คเก็ตสีฟ้าอมเขียว กางเกงคาร์โก้สีเหลือง
2. **`Chibi_BlueOperative_Rigged`** ([Chibi_BlueOperative_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_BlueOperative_Rigged.fbx) | [Chibi_BlueOperative_Rigged.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Character/Model/Chibi_BlueOperative_Rigged.prefab))
   * หนุ่มทรงผม Undercut สีน้ำเงินเข้ม แว่นกันแดดทรงกลมเลนส์ฟ้า หูฟังสื่อสาร เสื้อแจ็คเก็ตยุทธวิธีสีเขียวเข้มพร้อมวิทยุสื่อสาร
3. **`Chibi_HeavyStriker_Rigged`** ([Chibi_HeavyStriker_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_HeavyStriker_Rigged.fbx) | [Chibi_HeavyStriker_Rigged.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Character/Model/Chibi_HeavyStriker_Rigged.prefab))
   * นักรบเกราะหนักทรงผมตั้งสีคราม แผลเป็นคาดตาซ้าย ผ้าพันคอ เกราะยุทธวิธีพร้อมกระเป๋าแม็กกาซีนและเกราะไหล่

---

## 4. ระบบเสาไฟไม้ `Wooden Lantern Post` (`BUILD0023`) และระบบแยกสิ่งปลูกสร้างตามแมพ

* **เสาไฟไม้ (`BUILD0023_WoodenLanternPost`):**
  * เพิ่มข้อมูล [BUILD0023_WoodenLanternPost.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Database/Buildings/BUILD0023_WoodenLanternPost.asset) และ Prefab [23_Wooden_Lantern_Post.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Environment/Craft/23_Wooden_Lantern_Post.prefab) พร้อมไฟ Point Light สีส้มอบอุ่นที่เปิดอัตโนมัติในเวลากลางคืน
* **การแยกสิ่งปลูกสร้างตามพิกัดห้อง `(StageX, StageY)`:**
  * สิ่งปลูกสร้างทุกชิ้นผูกกับพิกัดห้อง `(StageX, StageY)` ใน [`BuildingManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/BuildingManager.cs) และ [`BuildingObject.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/BuildingObject.cs) ไม่ตามผู้เล่นไปแมพอื่น และแสดงผลกลับมาครบถ้วนเมื่อเดินกลับห้องเดิม
