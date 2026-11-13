# บันทึกความคืบหน้าการพัฒนาโครงการ (The Last Empire)

**วันที่บันทึกล่าสุด**: 14 พฤศจิกายน 2026  
**สถานะ Git**: `main` (Up to date with `origin/main`)  

---

## 10. ระบบเหตุการณ์ไดนามิก: Maya (Scout) วิ่งหนีตายข้ามฉากพร้อมฝูงมอนสเตอร์กวดตามหลัง (`FleeingSurvivor`)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`StageEventType.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageEventType.cs)
  - [`RandomStageEventManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs)
  - [`FleeingSurvivorRunner.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/FleeingSurvivorRunner.cs)
  - [`BossSpawnerRuntime.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs)
  - [`TransitionPortal.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/TransitionPortal.cs)
- **รายละเอียดการทำงานและฟังก์ชันหลัก**:
  1. **รูปแบบเหตุการณ์ (`FleeingSurvivor`)**:
     - เมื่อผู้เล่นก้าวเข้าสู่ห้อง Maya (Scout) จะปรากฏตัวขึ้นที่ประตูฝั่งตรงข้ามของห้อง และออกตัววิ่งหนีตายตัดผ่านห้องตรงดิ่งเข้าหาตัวผู้เล่น ผ่านหน้าผู้เล่น และวิ่งทะลุออกทางประตูที่ผู้เล่นเพิ่งก้าวเข้ามา
     - ลากฝูงศัตรูสายวิ่งกวด (ZombieRunner, AlienCrawler, ZombieDog, RobotKamikaze, AlienSpitter, ZombieNormal) จำนวน 4-5 ตัว ไล่ตามหลังเป็นแนวเส้นตรงบนถนนเส้นหลักเข้ามาหาผู้เล่น
  2. **สคริปต์ควบคุมการวิ่ง [`FleeingSurvivorRunner.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/FleeingSurvivorRunner.cs)**:
     - ดึงข้อมูลอัตลักษณ์ของ Maya (Scout) จาก `NPCDatabase` รหัส `watcher_maya` สวมทับบน Universal NPC Prefab ทันที
     - ความเร็วในการวิ่งปรับเป็น **1.5 m/s** (ฝูงมอนสเตอร์กวดตามที่ 1.35 m/s วิ่งตามหลังไม่แซงหน้า)
     - **ระบบวิ่งตรงเข้าหาตัว Player โดยตรง (Player Tracking)**: Maya ติดตามตำแหน่งของ `PlayerController` และวิ่งตรงเข้าหาตัวผู้เล่น เมื่อวิ่งเข้าใกล้ระยะประชิด 1.2 เมตรหรือผ่านตัวผู้เล่นไปแล้ว จึงมุ่งหน้าตรงออกสู่ประตูทางออกหลังผู้เล่นและหายตัวไป
     - มี Floating Speech Text แสดงเสียงตะโกนขอความช่วยเหลือแบบสุ่ม เช่น *"RUN FOR YOUR LIFE!!"*, *"THEY'RE RIGHT BEHIND ME!!"*, *"HELP! GET OUT OF THE WAY!!"*
     - **ป้องกันการติดตึก/สิ่งกีดขวาง 100%**: ปรับ `rb.isKinematic = true`, `rb.detectCollisions = false` และสั่งให้ Collider ทุกชิ้นในตัวและลูกๆ เป็น `isTrigger = true` รวมถึงล็อคระดับความสูงแกน Y บนพื้นห้อง ป้องกันปัญหาจมดินหรือสะดุดตึก
  3. **การคำนวณเวกเตอร์ตำแหน่งตรงกันข้ามกับตัวผู้เล่น (Opposite Spawn Point Calculation)**:
     - คำนวณจากตำแหน่งแกนหลักที่ผู้เล่นยืนอยู่จริงในห้อง:
       - **ผู้เล่นอยู่ซีกใต้ (South: `z < 0`):** Maya สปอว์นที่ฝั่งตรงข้ามด้านเหนือ (`z = +9.5f`) วิ่งลงใต้หาผู้เล่น และทะลุออกประตูด้านใต้ (`z = -18.0f`)
       - **ผู้เล่นอยู่ซีกเหนือ (North: `z >= 0`):** Maya สปอว์นที่ฝั่งตรงข้ามด้านใต้ (`z = -9.5f`) วิ่งขึ้นเหนือหาผู้เล่น และทะลุออกประตูด้านเหนือ (`z = +18.0f`)
       - **ผู้เล่นอยู่ซีกตะวันตก (West: `x < 0`):** Maya สปอว์นที่ฝั่งตรงข้ามด้านตะวันออก (`x = +15.5f`) วิ่งไปหาผู้เล่น และทะลุออกประตูตะวันตก (`x = -25.0f`)
       - **ผู้เล่นอยู่ซีกตะวันออก (East: `x >= 0`):** Maya สปอว์นที่ฝั่งตรงข้ามด้านตะวันตก (`x = -15.5f`) วิ่งไปหาผู้เล่น และทะลุออกประตูตะวันออก (`x = +25.0f`)
  4. **การแก้ไขข้อผิดพลาดเมื่อย้ายแมพไป-กลับ (Multi-Stage Persistence & Cleanup Fixes)**:
     - **เคลียร์สถานะข้ามห้อง (`CleanupActiveEvent`)**: เมื่อผู้เล่นวาร์ปเปลี่ยนห้อง ระบบจะหยุด Coroutine สปอว์นมอนสเตอร์ทันที และทำลาย NPC Maya ที่กำลังวิ่งอยู่ ไม่ให้หลุดไปค้างหรือสปอว์นมอนสเตอร์รั่วข้ามไปยังห้องถัดไป
     - **แก้ปัญหา Portal Phantom Trigger**: ตรวจสอบระยะห่างจริงของผู้เล่นก่อนเปิดใช้งาน Portal (`<= 3.5m`) และรีเซ็ต `_playerInRange = false` ทุกครั้งที่มีการวาร์ป ป้องกันประตูค้าง
     - **รอ 1 เฟรมเพื่อซิงค์ตำแหน่งสมบูรณ์**: หน่วงการประเมินอีเวนต์ในห้องใหม่ 1 เฟรม เพื่อให้ `TransitionPortal` วาร์ปตัวผู้เล่นไปอยู่ที่ประตูใหม่และอัปเดตฟิสิกส์ให้เสร็จเรียบร้อย 100% ก่อนคำนวณทิศทางการวิ่ง
     - **บันทึกศัตรูที่เหลือและเคลียร์ห้องอย่างถูกต้อง**: ฝูงศัตรูที่วิ่งตามหลัง Maya ถูกบันทึกลงใน `stage.remainingEnemyPrefabNames` หากผู้เล่นเดินกลับมาห้องเดิม มอนสเตอร์จะถูกฟื้นฟูกลับมาอย่างถูกต้อง และหากศัตรูหมดห้อง จะถูกบันทึกเป็น `isCleared = true` ทันที ไม่ติดบัคห้องค้างอีกต่อไป
  5. **การเชื่อมต่อ UI แจ้งเตือนกับพาเนลเกม (`Notification_HUD`)**:
     - ยกเลิกกล่อง Procedural ชั่วคราว และผสานเข้ากับพาเนล `Notification_HUD` ในฉากโดยตรง (`NamePanel/Name_text` และ `NotificationPanel/Notification_text`) พร้อมระบบ CanvasGroup Fade In/Out อย่างนุ่มนวล
  6. **เครื่องมือทดสอบและคีย์ลัด (Hotkeys & Cheat Menu)**:
     - เพิ่มแท็บ **"Events [F9]"** ใน `[+] Cheat Menu [F1]` บนหน้าจอ
     - **`Shift + F9`**: เสก Maya วิ่งหนีตายตัดผ่านห้องเข้าหาผู้เล่นทันทีในห้องปัจจุบัน
     - **`F9`**: จองคิวด่านถัดไป (Queue Event) เมื่อเดินข้ามประตูวาร์ป อีเวนต์จะทำงานแน่นอน 100%

---

## 11. การปรับสมดุลการเกิดของเอเลี่ยนช่วงกลางวันและกลางคืน (Day/Night Alien Spawn Rebalancing)
- **สคริปต์ที่เกี่ยวข้อง**: [`GameManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/GameManager.cs)
- **รายละเอียดการปรับสมดุล**:
  1. แยกหมวดหมู่ศัตรูใน `configsToSpawn` ออกจากซอมบี้มาตรฐานอย่างชัดเจน โดยตรวจสอบ `AlienAI` หรือคำว่า `"Alien"` ในชื่อ Prefab
  2. **กลางคืน (Nighttime):** เอเลี่ยนยังคงเป็นภัยคุกคามหลัก โอกาสเกิด 80% ต่อชนิด สุ่มเกิด 0-3 ตัว
  3. **กลางวัน (Daytime):** ปรับลดโอกาสการเกิดเอเลี่ยนลงเหลือเพียง **3% (`0.03f`)** ทั่วทั้งห้อง และหากสุ่มเกิดสำเร็จ จะจำกัดจำนวนไว้ที่ **1 - 2 ตัวเสมอ** ป้องกันไม่ให้เอเลี่ยน (เช่น Shadow Lurker หรือ Skittering Crawler) เกิดเกลื่อนในเวลากลางวัน

---

## 5. ระบบสุ่มเหตุการณ์ฉุกเฉินประจำด่าน (Dynamic Random Stage Encounter System)
- **สคริปต์ [`StageEventType.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageEventType.cs) และ [`RandomStageEventManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs)**:
  - รองรับรูปแบบเหตุการณ์ฉุกเฉินที่สุ่มเจอได้ในห้องธรรมดาทั่วไป:
    1. **`SupplyAirdrop`**: แอร์ดรอปเสบียงทหารทิ้งร่มลงมากลางห้อง ตกอาวุธระดับสูง (Sniper, Katana, RPG, Shotgun), กระสุน Rifle Ammo (30-60 นัด), เงินสด (250-600 Gold), และชุดปฐมพยาบาล
    2. **`HostileAmbush`**: ฝูงศัตรูสายวิ่งเร็ว (ZombieRunner, RobotKamikaze, AlienCrawler, ZombieDog) 3-5 ตัว โผล่ล้อมกรอบผู้เล่นรอบทิศทางในระยะประชิดทันทีที่ก้าวผ่านประตู
    3. **`NomadTrader`**: พ่อค้าเร่พเนจร (Elena, Otto, Maya หรือ Roy) มาตั้งแคมป์ชั่วคราวในห้องธรรมดา ให้ซื้อขายไอเทมฉุกเฉิน
    4. **`RadioactiveFallout`**: หมอกกัมมันตรังสีสีเขียวปกคลุมห้อง ลดทัศนวิสัย และลด Sanity ของผู้เล่นทันที 10 หน่วย
    5. **`BloodMoonSurge`**: แสงห้องเปลี่ยนเป็นสีแดงสด ศัตรูในห้องได้รับบัฟเคลื่อนที่ไวขึ้น +30% (`ai.moveSpeed *= 1.30f`)
    6. **`ScavengerCache`**: เศษซากเสบียงลับ 4 จุดถูกซ่อนกระจายอยู่ตามมุมห้องให้เก็บฟรี
    7. **`FleeingSurvivor`**: ผู้รอดชีวิต (Maya) วิ่งหนีฝูงมอนสเตอร์ตัดผ่านห้องมาหาผู้เล่น
  - **Deterministic Persistence**: สุ่มจาก `stage.stageSeed + 4242` ทำให้ผลลัพธ์ของห้องเดิมไม่เปลี่ยนเมื่อเดินย้อนกลับมา
  - **การผสานระบบ**: เชื่อมเข้ากับ [`StageData.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageData.cs) และ [`GameManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/GameManager.cs) ใน `SpawnLocalStageContent()` ครอบคลุมทั้งห้องใหม่, ห้องเคลียร์แล้ว, และห้องที่มี Template

---

## 6. สถาปัตยกรรม Single-Prefab สำหรับ NPC ด้วย Procedural SetData
- ยกเลิกการ Duplicate Prefab แยกรายตัวละคร ให้เหลือเพียง Universal Prefab ชิ้นเดียว
- ระบบกำหนดข้อมูล (SetData) จะส่งข้อมูลจาก ScriptableObject เพื่อโหลดโมเดล 3D Mesh, ข้อมูลร้านค้า, และบทสนทนาประจำตัวละครนั้นๆ ในจังหวะสปอว์น ช่วยลดการบำรุงรักษา Prefab ลงอย่างมหาศาล

---

## 7. โมเดลตัวละคร 3D Chibi NPC ชุดใหม่ 5 ตัวละคร (ใช้ Armature และ Rig เดียวกับตัวหลัก)
- สร้างโมเดล 3D Chibi NPC 5 ตัวละครด้วยสคริปต์ [Create_NPC_Characters.py](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_NPC_Characters.py) และ [Generate_NPC_Metas.py](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Generate_NPC_Metas.py) พร้อม Rigging โครงกระดูก 18 ชิ้น (`Chibi_Base_Rig`) ที่แชร์แอนิเมชันร่วมกับตัวละครหลักได้ทันที:
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
- จัดทำเอกสารข้อกำหนดเชิงเทคนิค [Docs/save-system-plan.md](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Docs/save-system-plan.md)
- ออกแบบโครงสร้างรหัสห้อง 8 หลัก (8-Digit RoomCode) เพื่ออำนวยความสะดวกในการแชร์และโหลดพิกัดห้องพร้อมสถานะของโลก

---

*(ประวัติการพัฒนาก่อนหน้า: 11 พฤศจิกายน 2026)*

## 1. ระบบ Log แจ้งเตือนไอเทมที่ได้รับและคราฟต์ (`Log_HUD` & Object Pool 10 ชิ้น)
- **สคริปต์ [`ItemLogHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/ItemLogHUD.cs) และ [`LogThumbnail.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/LogThumbnail.cs)**:
  - Pre-allocated Object Pool 10 ชิ้น แสดงผล 2 วินาที แจ้งเตือนทั้งการเก็บไอเทมและการคราฟต์/สร้าง

## 2. ระบบคราฟต์ไอเทม โต๊ะคราฟต์ และการซ่อนหมวดหมู่ (`Crafting & Workbench`)
- แยกเงื่อนไขโต๊ะคราฟต์ (`Survivor Workbench`) สำหรับหมวด `Building` และ `Defense` พร้อมซ่อนปุ่มหมวดหมู่เมื่อไม่ได้อยู่ใกล้โต๊ะคราฟต์

## 3. โมเดลตัวละคร 3D Rigged ใหม่ 3 ตัว (รองรับ `Chibi_Player_Controller`)
- สร้างโมเดล `Chibi_CurlyGirl_Rigged`, `Chibi_BlueOperative_Rigged`, และ `Chibi_HeavyStriker_Rigged`

## 4. ระบบเสาไฟ `Wooden Lantern Post` (`BUILD0023`) และระบบแยกสิ่งปลูกสร้างตามแมพ
- เสาไฟไม้เปิดไฟอัตโนมัติเวลากลางคืน และแยกระบบสิ่งปลูกสร้างตามพิกัดห้อง `(StageX, StageY)`
