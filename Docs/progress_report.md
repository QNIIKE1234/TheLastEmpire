# บันทึกความคืบหน้าการพัฒนาโครงการ (The Last Empire)

**วันที่บันทึกล่าสุด**: 12 พฤศจิกายน 2026  
**สถานะ Git**: `main` (Up to date with `origin/main`)  

---

## 5. ระบบสุ่มเหตุการณ์ฉุกเฉินประจำด่าน (Dynamic Random Stage Encounter System)
- **สคริปต์ [`StageEventType.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Map/StageEventType.cs) และ [`RandomStageEventManager.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs)**:
  - รองรับ 6 รูปแบบเหตุการณ์ฉุกเฉินที่สุ่มเจอได้ในห้องธรรมดาทั่วไป (โอกาสตั้งต้น 25% ปรับได้ใน Inspector):
    1. **`SupplyAirdrop`**: แอร์ดรอปเสบียงทหารทิ้งร่มลงมากลางห้อง ตกอาวุธระดับสูง (Sniper, Katana, RPG, Shotgun), กระสุน Rifle Ammo (30-60 นัด), เงินสด (250-600 Gold), และชุดปฐมพยาบาล
    2. **`HostileAmbush`**: ฝูงศัตรูสายวิ่งเร็ว (ZombieRunner, RobotKamikaze, AlienCrawler, ZombieDog) 3-5 ตัว โผล่ล้อมกรอบผู้เล่นรอบทิศทางในระยะประชิดทันทีที่ก้าวผ่านประตู
    3. **`NomadTrader`**: พ่อค้าเร่พเนจร (Elena, Otto, Maya หรือ Roy) มาตั้งแคมป์ชั่วคราวในห้องธรรมดา ให้ซื้อขายไอเทมฉุกเฉิน
    4. **`RadioactiveFallout`**: หมอกกัมมันตรังสีสีเขียวปกคลุมห้อง ลดทัศนวิสัย และลด Sanity ของผู้เล่นทันที 10 หน่วย
    5. **`BloodMoonSurge`**: แสงห้องเปลี่ยนเป็นสีแดงสด ศัตรูในห้องได้รับบัฟเคลื่อนที่ไวขึ้น +30% (`ai.moveSpeed *= 1.30f`)
    6. **`ScavengerCache`**: เศษซากเสบียงลับ 4 จุดถูกซ่อนกระจายอยู่ตามมุมห้องให้เก็บฟรี
  - **Deterministic Persistence**: สุ่มจาก `stage.stageSeed + 4242` ทำให้ผลลัพธ์ของห้องเดิมไม่เปลี่ยนเมื่อเดินย้อนกลับมา
  - **Glassmorphism HUD Banner**: แสดงแบนเนอร์แจ้งเตือนสถานการณ์ด้านบนหน้าจอ สวยงาม ชัดเจน และไม่บล็อกการคลิกยิงของผู้เล่น (`blocksRaycasts = false`)
  - **การผสานระบบ**: เชื่อมเข้ากับ [`StageData.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Map/StageData.cs) และ [`GameManager.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Managers/GameManager.cs) ใน `SpawnLocalStageContent()` ครอบคลุมทั้งห้องใหม่, ห้องเคลียร์แล้ว, และห้องที่มี Template

---

## 6. สถาปัตยกรรม Single-Prefab สำหรับ NPC ด้วย Procedural SetData
- ยกเลิกการ Duplicate Prefab แยกรายตัวละคร ให้เหลือเพียง Universal Prefab ชิ้นเดียว
- ระบบกำหนดข้อมูล (SetData) จะส่งข้อมูลจาก ScriptableObject เพื่อโหลดโมเดล 3D Mesh, ข้อมูลร้านค้า, และบทสนทนาประจำตัวละครนั้นๆ ในจังหวะสปอว์น ช่วยลดการบำรุงรักษา Prefab ลงอย่างมหาศาล

---

## 7. โมเดลตัวละคร 3D Chibi NPC ชุดใหม่ 5 ตัวละคร (ใช้ Armature และ Rig เดียวกับตัวหลัก)
- สร้างโมเดล 3D Chibi NPC 5 ตัวละครด้วยสคริปต์ [Create_NPC_Characters.py](file:///d:/TLEGIT/TheLastEmpire/Tools/Create_NPC_Characters.py) และ [Generate_NPC_Metas.py](file:///d:/TLEGIT/TheLastEmpire/Tools/Generate_NPC_Metas.py) พร้อม Rigging โครงกระดูก 18 ชิ้น (`Chibi_Base_Rig`) ที่แชร์แอนิเมชันร่วมกับตัวละครหลักได้ทันที:
  1. **`Chibi_DoctorElena`**: แพทย์สนามหญิง เสื้อกาวน์สีขาว แว่นตากลม
  2. **`Chibi_MechanicOtto`**: ช่างซ่อมบำรุงชุดเอี๊ยมสีส้ม แว่นเชื่อมเหล็ก
  3. **`Chibi_WatcherMaya`**: พลสอดแนมหญิง ผ้าคลุมกันฝุ่นสีเขียว หมวกปีกกว้าง
  4. **`Chibi_SurvivorRoy`**: ผู้รอดชีวิตพเนจร เสื้อแจ็คเก็ตหนังและผ้าพันคอ
  5. **`Chibi_TraderViktor`**: พ่อค้าตลาดมืด หมวกเบเร่ต์สีดำ แว่นกันแดด และเสื้อโค้ตยาว

---

## 8. การปรับปรุงระบบเครื่องดื่มและการฟื้นฟูสถิติหลายค่า (Coffee & Tea Usability)
- เพิ่มและแก้ไขฟังก์ชันการดื่มกาแฟ (Coffee) และชา (Tea) ให้สามารถใช้งานได้โดยตรงจากช่องเก็บของ
- รองรับการฟื้นฟูสถิติพร้อมกัน 3 ค่า: ค่าน้ำ (Water), ความหิว (Hunger), และสติสัมปชัญญะ (Sanity)

---

## 9. การวางแผนสถาปัตยกรรมระบบเซฟ (8-Digit RoomCode & Persistence Architecture)
- จัดทำเอกสารข้อกำหนดเชิงเทคนิค [Docs/save-system-plan.md](file:///d:/TLEGIT/TheLastEmpire/Docs/save-system-plan.md)
- ออกแบบโครงสร้างรหัสห้อง 8 หลัก (8-Digit RoomCode) เพื่ออำนวยความสะดวกในการแชร์และโหลดพิกัดห้องพร้อมสถานะของโลก

---

*(ประวัติการพัฒนาก่อนหน้า: 11 พฤศจิกายน 2026)*

## 1. ระบบ Log แจ้งเตือนไอเทมที่ได้รับและคราฟต์ (`Log_HUD` & Object Pool 10 ชิ้น)
- **สคริปต์ [`ItemLogHUD.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/UI/ItemLogHUD.cs) และ [`LogThumbnail.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/UI/LogThumbnail.cs)**:
  - Pre-allocated Object Pool 10 ชิ้น แสดงผล 2 วินาที แจ้งเตือนทั้งการเก็บไอเทมและการคราฟต์/สร้าง

## 2. ระบบคราฟต์ไอเทม โต๊ะคราฟต์ และการซ่อนหมวดหมู่ (`Crafting & Workbench`)
- แยกเงื่อนไขโต๊ะคราฟต์ (`Survivor Workbench`) สำหรับหมวด `Building` และ `Defense` พร้อมซ่อนปุ่มหมวดหมู่เมื่อไม่ได้อยู่ใกล้โต๊ะคราฟต์

## 3. โมเดลตัวละคร 3D Rigged ใหม่ 3 ตัว (รองรับ `Chibi_Player_Controller`)
- สร้างโมเดล `Chibi_CurlyGirl_Rigged`, `Chibi_BlueOperative_Rigged`, และ `Chibi_HeavyStriker_Rigged`

## 4. ระบบเสาไฟ `Wooden Lantern Post` (`BUILD0023`) และระบบแยกสิ่งปลูกสร้างตามแมพ
- เสาไฟไม้เปิดไฟอัตโนมัติเวลากลางคืน และแยกระบบสิ่งปลูกสร้างตามพิกัดห้อง `(StageX, StageY)`
