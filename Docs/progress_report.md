# บันทึกความคืบหน้าการพัฒนาโครงการ (The Last Empire)

**วันที่บันทึกล่าสุด**: 14 พฤศจิกายน 2026  
**สถานะ Git**: `main` (Up to date with `origin/main`)  

---

## 12. แก้ไขบัคห้องว่างเปล่าจากการเปลี่ยนด่าน (Stage Transition Empty Void Bug Fix)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`TransitionPortal.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/TransitionPortal.cs)
  - [`EnvironmentManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/EnvironmentManager.cs)
  - [`PlayerController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs)
- **สาเหตุที่แท้จริง**:
  - ในซีน `Gameplay_3D.unity` มี Canvas UI เก่าชื่อ `CanvasColider` (จากเวอร์ชัน 2D เดิม) ที่มีคอมโพเนนต์ `TransitionPortal` ติดอยู่บน RectTransform (Layer 5: UI)
  - เมื่อคำสั่ง `FindObjectsByType<TransitionPortal>` ทำงานเพื่อค้นหาประตูทางเข้าของห้องใหม่ มันหยิบตัว Canvas UI Portal มาแทนที่จะเป็น 3D Portal จริงในฉาก ทำให้พิกัด Position ของตัวละครถูกวาร์ปไปที่ `(0.12, 0.50, 35.18)` ซึ่งอยู่ **นอกขอบเขตพื้นที่เล่นของห้อง (20 เมตรนอกแมพ)** ทำให้เห็นเป็นห้องว่างเปล่า ไม่มีสิ่งปลูกสร้างหรือศัตรู
- **การแก้ไข**:
  - เพิ่มฟังก์ชัน `IsWorldPortal(TransitionPortal portal)` ตรวจสอบและคัดกรอง RectTransform และ Layer 5 (UI) ออกทั้งหมด
  - กรองให้เลือกเฉพาะ 3D World Portal แท้ในฉาก ทำให้ผู้เล่นสปอว์นที่ประตูห้องใหม่อย่างถูกต้อง 100% เสมอ

---

## 13. ปรับปรุงระบบอีเวนต์หนีตาย Maya (Fleeing Survivor) & Animation วิ่ง
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`FleeingSurvivorRunner.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/FleeingSurvivorRunner.cs)
  - [`RandomStageEventManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs)
- **รายละเอียดการปรับปรุง**:
  - **Animation วิ่ง (Run Animation)**: แก้ปัญหาตัวละครค้างท่า Idle โดยใช้ `CrossFadeInFixedTime("Run", 0.05f)` ข้ามเงื่อนไข `HasExitTime` ใน Animator Controller พร้อมระบบดึง Animator Controller สำรองจาก `PlayerController.Instance` โดยตรง
  - **ปรับความเร็ว NPC**: ปรับลดความเร็วการวิ่งของ Maya ลง 1 m/s (จาก 5.0 m/s เหลือ **4.0 m/s**) ตามที่กำหนด
  - **ปิดการ Interact 100%**: ปิด `CanInteract = false` และสั่งทำลาย `NPCController` ทิ้งเมื่อเริ่มวิ่งหนีตาย เพื่อไม่ให้ขึ้นปุ่มกดคุยขวางหน้าจอ

---

## 14. ปิดการเกิดของเอเลี่ยนในเวลากลางวัน 100% (Strict Nighttime Alien Spawning)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`GameManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/GameManager.cs)
  - [`RandomStageEventManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs)
- **รายละเอียด**:
  - ยกเลิกโอกาสการสุ่มเกิด 3% ของเอเลี่ยนในเวลากลางวันออกทั้งหมดใน `GameManager.cs`
  - กรอง `AlienCrawler` และ `AlienSpitter` ออกจากอีเวนต์สุ่มศัตรูช่วงกลางวัน (HostileAmbush และ FleeingSurvivor Chaser Pack) ทำให้เอเลี่ยนเกิดได้เฉพาะเวลากลางคืน (`DayNightManager.Instance.IsNight`) เท่านั้น

---

## 15. ป้องกัน Inspector คืนค่า Prefab เดิม (NPCDatabaseInitializer Overwrite Bug)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`NPCDatabaseInitializer.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/NPCDatabaseInitializer.cs)
- **สาเหตุ & การแก้ไข**:
  - เดิมมี `[InitializeOnLoad]` ที่รันทุกครั้งหลัง compile แล้วเขียนทับ `customModelPrefab` ใน `NPCDatabase` เป็น `.fbx` อัตโนมัติ
  - แก้ไขให้ตรวจสอบหาก ScriptableObject มีข้อมูลอยู่แล้วจะไม่เขียนทับ และให้ความสำคัญกับ `.prefab` ก่อนไฟล์ `.fbx` เสมอ ทำให้ค่าที่ตั้งไว้ใน Inspector ไม่ถูกรีเซ็ตกลับ

---

## 16. เพิ่มช่อง Reference บทพูดลอย & ไอคอนตัวละคร (Floating Dialog & NPCDataSO)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`NPCDataSO.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Data/NPCDataSO.cs)
  - [`NPCController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/NPCController.cs)
  - [`FleeingSurvivorRunner.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/FleeingSurvivorRunner.cs)
- **รายละเอียด**:
  - เปิดช่อง Serialized Reference สำหรับ `dialogPanel`, `dialogText`, และ `dialogIcon` ใน Inspector
  - เพิ่มฟิลด์ `characterImage`, `npcIcon`, `npcPortrait` ใต้หมวดหมู่ `[Header("Portraits & Icons")]` ใน `NPCDataSO`
  - นำ `npcIcon` ไปแสดงผลแทนรูปกระเป๋าในกล่องข้อความลอยเหนือหัว (`DIalogPanel`) ทันทีเมื่อเกิด

---

## 17. ระบบ Obstacle Phasing สำหรับศัตรูที่วิ่งไล่กวด (Chaser Enemies Obstacle Phasing)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`BaseEnemyAI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BaseEnemyAI.cs)
  - [`RandomStageEventManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs)
- **รายละเอียด**:
  - เพิ่มระบบ `EnableChaserPhasing(Transform targetPlayer, float restoreDistance = 4.5f)` และ `DisableChaserPhasing()`
  - ขณะเริ่มวิ่งไล่กวดมาจากฝั่งตรงข้าม Collider ทุกชิ้นของมอนสเตอร์จะถูกปรับเป็น `Trigger` ชั่วคราว และปิดแรงโน้มถ่วง พร้อมล็อคระดับความสูงแกน Y บนพื้น ทำให้วิ่งทะลุสิ่งกีดขวาง/ตึก/กำแพง/กล่อง และไม่ติดกันเอง
  - เมื่อเข้าใกล้ตัวผู้เล่นในระยะ `<= 4.5m` หรือเมื่อโดนโจมตี/ดาเมจ ระบบจะคืนค่า Collider กลับเป็น Solid Physical Collision และเปิด Gravity ปกติเพื่อเริ่มการต่อสู้ทันที
  - ปรับความเร็วไล่กวดของฝูงมอนสเตอร์เป็น **3.5 m/s** (ช้ากว่า Maya เล็กน้อย เพื่อให้วิ่งไล่กวดตามหลังอย่างเป็นธรรมชาติ)

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
