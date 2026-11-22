# บันทึกความคืบหน้าการพัฒนาโครงการ (The Last Empire)

**วันที่บันทึกล่าสุด**: 22 พฤศจิกายน 2026  
**สถานะ Git**: `multi-player-netcode` (Synced `main` + `feat/project-mayhem` + ล่าสุดรีโมเดล Alien/Zombie Boss & ปรับสมดุล Enemy)  

---

## 26. รีโมเดล Alien 5 สายพันธุ์ + บอส Zombie (`Ancient Homunkulus`) + สลับพฤติกรรม/โมเดล `ZombieTank` + ปรับสมดุล HP & กระสุนศัตรู
- **สคริปต์ & แอสเซ็ตที่เกี่ยวข้อง**:
  - Blender Scripts:
    - [`Tools/Create_Aliens_Smart_And_Hook.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Aliens_Smart_And_Hook.py)
    - [`Tools/Create_Alien_Normal_TekeTeke.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Alien_Normal_TekeTeke.py)
    - [`Tools/Create_Alien_Invisible_Enderman.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Alien_Invisible_Enderman.py)
    - [`Tools/Create_Alien_Crawler.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Alien_Crawler.py)
    - [`Tools/Create_Zombie_Boss_Homunkulus.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Zombie_Boss_Homunkulus.py)
    - [`Tools/Create_Zombie_Spitter_And_Tank.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Zombie_Spitter_And_Tank.py)
  - Runtime & Editor Scripts:
    - [`Assets/Scripts/Editor/AlienModelBuilder.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/AlienModelBuilder.cs) & [`Assets/Scripts/Editor/ChibiZombieModelBuilder.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/ChibiZombieModelBuilder.cs)
    - [`Assets/Scripts/Runtime/Combat/ZombieAI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/ZombieAI.cs), [`RobotAI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/RobotAI.cs), [`AlienAI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/AlienAI.cs), [`Projectile.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/Projectile.cs)
- **รายละเอียดการพัฒนา**:
  1. **รีโมเดล Alien ครบ 5 สายพันธุ์พร้อม Rig & Animations**:
     - **`AlienSmart`**: โดมหลอดไฟแก้วใส URP Transparent มองเห็นสมองเอเลี่ยนเต้นตุบๆ + เส้นประสาทเรืองแสง, ตาเดียวสีเหลือง, 4 ขาแมงมุม + 2 มือกรงเล็บ โทนม่วง-น้ำเงิน-เขียวมะนาว
     - **`AlienHook`**: กะโหลกเอเลี่ยนยาว, ฐานหนวด 8 เส้น, ตาเดียว, **มือซ้ายก้ามยักษ์ยืดพุ่งคว้าได้ 2.75x** (ผูกท่า `Hook` กับ `AlienAI.ExecuteHookPull()`) และปรับโทนสีเป็นสไตล์ **`Panoptes`** (เนื้อชมพูอมส้ม-ดำเงา-ชมพูเลือด-ตาขาว)
     - **`AlienNormal`**: ทรง `Sightless Worm` หัวโตไร้ตา ปากเขี้ยวใหญ่มีน้ำลายยืด ไม่มีขา แต่ยืดลำตัวไปข้างหลังยาวเหมือน **จระเข้ไร้ขา** + ท่าเดิน/วิ่งคลานลากตัวด้วย 2 มือแบบ **ผีเทเกะเทเกะ (`Teke Teke`)**
     - **`AlienInvisible`**: ทรงตัวสูงผอมยาวสีดำสนิทสไตล์ **Enderman** (~2.92 ม.) ตาเดียวเรืองแสงสีม่วง + หนวดหลังหัวแบบ **จอมมารบูยาวถึงระดับก้น** พร้อมอัปเกรดระบบล่องหน 3D ใน `AlienAI.HandleInvisibilityFade()` ให้ตัวจางพรางตัวแต่ลูกตาเรืองแสงวาวในความมืด
     - **`AlienCrawler`**: เอเลี่ยนคลาน 4 ขาขนสีน้ำตาล ตาเดียวโปนใหญ่ หางม้วน พร้อม **กรงเล็บยาวมากทั้ง 4 เท้า** และ **ลิ้นสีแดงยาวพิเศษ** ริก 3 ข้อสะบัดตามการวิ่ง/โจมตี
  2. **รีโมเดลบอสซอมบี้ (Slash Top-Hat Brute + ปรสิตหนวดปลาหมึกสีชมพูบนหลัง) & สลับพฤติกรรม/โมเดลกับ `ZombieTank`**:
     - **บอส `ZombieLeader`**: รีโมเดลใหม่เป็น **ร็อคสตาร์ซอมบี้หมวกทรงสูง (Slash Top-Hat + ผมหยิกดำฟู + แว่นกันแดด + แขนกอลิล่าใหญ่ 2 ข้าง ไม่มีกีตาร์ + ท่าเดินหลังค่อม + ปรสิตหนวดปลาหมึก 8 เส้นสีชมพูเนื้อ/เลือดหมูบนหลัง)** พร้อมชุดพฤติกรรมใหม่: **เสกลูกน้อง + กระโดดวิถีโค้งทับ (`Leap` ไม่ใช่พุ่ง!) + พ่นกรด (`Spit` 3 ทิศตอน Rage) + Ground Stomp**
     - **`ZombieTank`**: ย้ายพฤติกรรมบอสซอมบี้เดิมทั้งหมด (พุ่ง Charge + Ground Stomp AOE + Rage Mode) **ยกเว้นเสกลูกน้อง** มาใส่ `ZombieTank` พร้อมนำโมเดลบอสเดิม (`Chibi_ZombieBoss_Legacy_Rigged.fbx`) มาใช้กับ `ZombieTank.prefab` และเก็บรักษาโมเดล Tank กอลิล่า (`Chibi_ZombieTank_Rigged.fbx`) ไว้ในโปรเจกต์เผื่อใช้งานต่อ
     - เพิ่ม `ZombieSpitter` และ `ZombieTank` เข้า Object Pool + Spawn Configs ของ `Gameplay_3D.unity` และ Ambush Event ใน `RandomStageEventManager.cs`
  3. **แก้ Texture ผิวดินน้ำมันศัตรูทุกตัว (Triplanar PBR Shader + 6 ชุด Texture/Normal Map) & เร่ง Animation ศัตรูสายไว +30%**:
     - สร้าง Shader [`TheLastEmpire_EnemyDetailLit.shader`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/TheLastEmpire_EnemyDetailLit.shader) (URP Triplanar Object-Space Albedo Detail + Normal Perturbation + Micro-Cavity) และสคริปต์ [`EnemyPbrMaterialUpgrader.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/EnemyPbrMaterialUpgrader.cs) สร้าง Texture 512×512 ไร้รอยต่อ 6 ชุด (`OrganicFlesh`, `ChitinBio`, `FurHair`, `WornCloth`, `BrushedMetal`, `BoneClaw`) อัปเกรด Material ศัตรูทุกตัว (Zombies, Aliens, Robots) แก้ปัญหาผิวดินน้ำมัน/พลาสติกเรียบ
     - อัปเดต [`BaseEnemyAI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BaseEnemyAI.cs) เร่งความเร็วท่าเดิน/วิ่ง (`Walk`/`Run`) ของศัตรูที่เคลื่อนที่ไว (`moveSpeed >= 3.5f`) ขึ้นอีก **+30%** (`1.30x`) อัตโนมัติ
  4. **ปรับสมดุล Combat (HP, Projectile Speed, Robot Gunner 5-Round Burst) & มาตรฐาน UI ชื่อ/เลือด**:
     - **ความเร็วกระสุนศัตรู (`Projectile.cs`, `RobotAI.cs`, `ZombieAI.cs`, `AlienAI.cs`)**: ปรับความเร็วกระสุนของศัตรูทุกตัวเป็น **`50f`** เท่ากระสุนผู้เล่น (`BulletPrefabs.prefab`)
     - **`RobotGunner`**: ปรับการยิงเป็นชุด **Burst ทีละ 5 นัด** (ห่างกัน 0.12 วิ) แล้วสุ่มเว้นช่วงพัก **`1 - 3 วินาที`** ก่อนยิงชุดใหม่
     - **ปรับสมดุลเลือด (HP) ศัตรูทุก Prefab**: บอสทุกตัว (**×2**: `ZombieLeader` 3000, `AlienLeader` 2800, `RobotLeader` 3200), ศัตรูทั่วไป (**+30%** ครบทุกตัว), **ยกเว้น `ZombieBoomer`** (คงเดิม 500)
     - **มาตรฐาน `Name_text` & `Health_text` ครบทั้ง 24 Prefab**: ตั้งค่า RectTransform (`Width 300, Height 50`) และ TextMeshPro (`Teko SDF`, `Bold`, `Auto Size 18–36`, สีขาว, ล้างข้อความค้าง) ตรงกันทุก Enemy, Player, และ NPC

---

## 25. โมเดล 3D Chibi Anime NPC ใหม่ 3 ตัว & ระบบสร้างโมเดลผู้เล่นจาก Character SO โดยตรง
- **สคริปต์ & แอสเซ็ตที่เกี่ยวข้อง**:
  - [`Tools/Create_Custom_3_NPCs.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Custom_3_NPCs.py)
  - [`Assets/Scripts/Editor/NpcCustomModelsAutoSync.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/NpcCustomModelsAutoSync.cs)
  - [`Assets/Scripts/Runtime/Player/PlayerController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs)
  - [`Assets/Scripts/Editor/PlayerCharacterModelListSetup.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/PlayerCharacterModelListSetup.cs)
  - Prefabs: `NPC_Elena_Medic_Rigged.prefab`, `NPC_Maya_Scout_Rigged.prefab`, `NPC_Roy_Veteran_Rigged.prefab` (ใน `Assets/Resources_Addressable/Prefabs/Character/`)
- **รายละเอียดการพัฒนา**:
  1. **ปั้นและแทนที่โมเดล 3D Chibi Anime NPC 3 ตัวใหม่** (Rig 18 กระดูก `Chibi_Base_Rig` + 11 แอนิเมชัน + Texture ใบหน้า 1024×1024 และผม 512×512 + URP Lit Materials Remap อัตโนมัติ พร้อมรักษา Prefab GUID เดิม):
     - **`NPC_Elena_Medic_Rigged`**: สาวสตรีทเทคผมบ๊อบดำ หูฟังเหลือง ตาสีม่วงลาย X-Sparkle หัวใจแดงใต้ตา แจ็คเก็ตเหลืองปกขนฟูขาว อาร์มหมี เข็มขัดแดงพร้อมขวดน้ำยา 3 สี (ฟ้า/เขียว/ชมพู) และรองเท้าผ้าใบขาว
     - **`NPC_Maya_Scout_Rigged`**: สาวโกธิคสไตล์ `P4NDORA` ผมบ๊อบดำเรียบหน้าม้าตรง ปากแดงเข้ม โช้คเกอร์ดำห่วงเงิน เดรสสั้นสายเดี่ยวสีดำขอบระบาย สร้อยข้อมือจี้แดง และบูทดำหัวเข็มขัดเงิน
     - **`NPC_Roy_Veteran_Rigged`**: สาวไซเบอร์เทคหางม้าสีฟ้า ตาสีม่วง ต่างหูห่วงทอง ปลอกคอไซเบอร์ดำแถบเหลือง เสื้อครอปขาว ปลอกแขนดำ-เหลือง กางเกงขาสั้นดำแถบข้างเหลือง และบูทสูงขอบเหลือง
  2. **ระบบเสกโมเดลผู้เล่นจาก `CharacterData` SO เข้า `PlayerPrefabs/Model` อัตโนมัติ**:
     - ปรับโครงสร้าง `PlayerPrefabs` ให้ไม่ต้องเก็บโมเดลตัวละครทั้ง 7 ตัวซ้ำไว้ใต้ `Model` อีกต่อไป โดยให้ `PlayerController.ApplySelectedCharacterModel()` ดึง `CharacterData.characterModelPrefab` จากไฟล์ SO (`CHAR_01`–`CHAR_07`) มา `Instantiate` ไว้ใต้ `PlayerPrefabs/Model` พร้อมตั้งค่ามาตรฐาน `Position = (0, -0.6, 0)`, `Rotation = (0, 0, 0)`, `Scale = (0.7, 0.7, 0.7)`
     - ปิดตาโมเดลต้นแบบ (`Chibi_Base_Rigged`) อัตโนมัติ (พร้อมดึง `AnimatorController` สำรองมาใช้หากโมเดลปลายทางไม่ได้ผูกไว้)
     - ย้ายชุดอาวุธหลัก `Model/Malee` และ `Model/Range` (ครบ 18 ชิ้น) เข้าไปในกระดูก `RightHand` ของโมเดลที่เพิ่งสร้างพร้อมรีเซ็ต Local Transform (`pos=0, rot=0, scale=1`) ป้องกันอาการอาวุธขยายสเกล 70 เท่า

---

## 24. ระบบซิงค์ Multiplayer เชิงลึก (Loot/Container, Building/Event, Drop ของจากกระเป๋า & แก้บั๊ก Trigger/Skill)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`Assets/Scripts/Runtime/Network/Bridges/NetworkLootManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Bridges/NetworkLootManager.cs)
  - [`Assets/Scripts/Runtime/Network/Hooks/RandomStageEventManager.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/RandomStageEventManager.NetHook.cs)
  - [`Assets/Scripts/Runtime/Network/Hooks/PlayerController.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/PlayerController.NetHook.cs)
  - [`Assets/Scripts/Runtime/UI/InventoryUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/InventoryUI.cs) & [`Assets/Scripts/Runtime/UI/LootUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/LootUI.cs)
  - [`Assets/Scripts/Runtime/Combat/CollectibleItem.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/CollectibleItem.cs) & [`Assets/Scripts/Runtime/Combat/ShiftAbilities.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/ShiftAbilities.cs)
  - [`Assets/Scripts/Runtime/Map/TransitionPortal.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/TransitionPortal.cs), [`CampfireBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/CampfireBuilding.cs), [`FloorBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/FloorBuilding.cs), [`SanityAuraZone.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Environment/SanityAuraZone.cs)
- **รายละเอียดการพัฒนา**:
  1. **ระบบโยนของออกจากกระเป๋า (Drag-to-Drop + Stack Drop + Manual `[E]` Pickup)**:
     - ลากไอเทมจากช่องกระเป๋าโยนออกนอกกรอบ `InventoryPanel` เพื่อโยนลงพื้นทีละ 1 ชิ้น หรือกด **`Alt + ลาก`** เพื่อโยนทั้งกองเป็นไอเทมบนพื้นเพียง 1 ชิ้นที่เก็บจำนวนเต็ม (เช่น ยา x100)
     - ของที่ผู้เล่นโยนทิ้งจะไม่ถูกดูดเข้าตัวอัตโนมัติ (`RequireManualPickup = true`) ต้องเดินเข้าไปกด **`[E]`** เพื่อเก็บเท่านั้น และซิงค์ให้ผู้เล่นคนอื่นในด่านเดียวกันเห็น/เก็บได้ผ่าน `ClientDropItemRequest`
  2. **ซิงค์การเก็บของจากรถ/กล่อง, ของดรอปจากศัตรู, Building & Stage Events ครบทุกกรณี**:
     - แก้ไข `NetworkDropId` จากตำแหน่งเริ่มต้น (`InitialSpawnPosition`) ไม่ให้เพี้ยนตอนไอเทมโดนดูด, เพิ่ม `LootSpawnBroadcast` ให้ Client เห็นของดรอปจากศัตรูตรงกัน, และให้ Client เก็บของในด่านเดี่ยวได้
     - ซิงค์สถานะของในรถ/กล่อง (`ContainerLootSync`) แบบเรียลไทม์: หาก A เปิดหน้าต่าง Loot อยู่แล้ว B หยิบของไป ช่องไอเทมฝั่ง A จะหายทันที และเก็บแคช `_syncedContainerStates` ข้ามด่านให้เพื่อนที่เดินตามเข้าด่านทีหลังเห็นรถที่ถูกรื้อไปแล้วตรงกัน
     - ซิงค์การรื้อ/ทำลายสิ่งปลูกสร้าง (`BuildingDestroyBroadcast`), ซิงค์ Event สุ่มในด่าน (`StageEventBroadcast`) พร้อมกู้คืนบรรยากาศ Event, `NomadTrader`, และของดรอปบนพื้นสำหรับเพื่อนที่ย้ายแมพเข้ามาทีหลัง (`BroadcastAllActiveStageDropsToClient`)
  3. **แก้บั๊ก Dual-Client, Overhead Name, Demolitionist Skill & Portal Trigger**:
     - แยก `_sessionSelectedCharacterId` ต่อหน้าต่าง, ซิงค์ `WorldSeed`, ปรับ `EnemyBatchSync` + `EnemyDamageRequest`, และเพิ่มป้ายชื่อผู้เล่นเหนือหัวบน `PlayerPrefabs/Canvas/Text (TMP)`
     - แก้สกิล `SatchelVaultAbility` ของ Demolitionist ให้กลิ้งไปข้างหน้า 4.5m พร้อมวาง C4 และไม่ทำดาเมจโดนตัวเอง/เพื่อนร่วมทีม
     - แก้บั๊ก **A ยืนตรงจุดวาร์ปแล้ว B กด `E` เก็บของไกลๆ แต่ B โดนวาร์ปข้ามแมพ**: ปรับ `TransitionPortal` ให้ตรวจเฉพาะ Local Player (`!IsRemotePlayer`) + เช็คระยะห่างจาก Collider จริงซ้ำ 2 ชั้น รวมถึงแก้ Trigger ของ `CampfireBuilding`, `FloorBuilding`, และ `SanityAuraZone` ให้รับเฉพาะ Local Player เช่นเดียวกัน

---

## 23. ระบบ Co-op Multiplayer 1–6 คน (LAN Netcode, `Lobby_NetCode` UI, Account Name, & Independent Grid Stage Encounter)
- **สคริปต์ & ซีนที่เกี่ยวข้อง**:
  - **Core & Transport**:
    - [`Assets/Scripts/Runtime/Network/Core/NetcodeCompatLayer.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Core/NetcodeCompatLayer.cs)
    - [`Assets/Scripts/Runtime/Network/Core/EmpireNetworkManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Core/EmpireNetworkManager.cs)
    - [`Assets/Scripts/Runtime/Network/Core/LobbyRelayService.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Core/LobbyRelayService.cs)
    - [`Assets/Scripts/Runtime/Network/Core/PlayerRegistry.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Core/PlayerRegistry.cs)
    - [`Assets/Scripts/Runtime/Network/Data/NetDataModels.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Data/NetDataModels.cs)
  - **Partial Class Hooks**:
    - [`Assets/Scripts/Runtime/Network/Hooks/PlayerController.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/PlayerController.NetHook.cs)
    - [`Assets/Scripts/Runtime/Network/Hooks/BaseEnemyAI.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/BaseEnemyAI.NetHook.cs)
    - [`Assets/Scripts/Runtime/Network/Hooks/TransitionPortal.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/TransitionPortal.NetHook.cs)
    - [`Assets/Scripts/Runtime/Network/Hooks/GameManager.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/GameManager.NetHook.cs)
    - [`Assets/Scripts/Runtime/Network/Hooks/RandomStageEventManager.NetHook.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Hooks/RandomStageEventManager.NetHook.cs)
  - **Network Bridges**:
    - [`Assets/Scripts/Runtime/Network/Bridges/NetworkPlayerBridge.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Bridges/NetworkPlayerBridge.cs)
    - [`Assets/Scripts/Runtime/Network/Bridges/NetworkEnemyBridge.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Bridges/NetworkEnemyBridge.cs)
    - [`Assets/Scripts/Runtime/Network/Bridges/NetworkStageSyncManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Bridges/NetworkStageSyncManager.cs)
    - [`Assets/Scripts/Runtime/Network/Bridges/NetworkLootManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Bridges/NetworkLootManager.cs)
    - [`Assets/Scripts/Runtime/Network/Bridges/NetworkBuildingSyncManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/Bridges/NetworkBuildingSyncManager.cs)
  - **Lobby & Menu UI (100% Pure `[SerializeField]` Inspector References)**:
    - [`Assets/Scripts/Runtime/Network/UI/MultiplayerLobbyUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/UI/MultiplayerLobbyUI.cs)
    - [`Assets/Scripts/Runtime/Network/UI/RoomThumbnailUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/UI/RoomThumbnailUI.cs)
    - [`Assets/Scripts/Runtime/Network/UI/RoomMemberSlotUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/UI/RoomMemberSlotUI.cs)
    - [`Assets/Scripts/Runtime/Network/UI/PartyStatusHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Network/UI/PartyStatusHUD.cs)
    - [`Assets/Scripts/Runtime/UI/MainMenuManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/MainMenuManager.cs)
    - [`Assets/Scripts/Editor/MultiplayerDualClientLauncher.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/MultiplayerDualClientLauncher.cs)
- **รายละเอียดการพัฒนา**:
  1. **ระบบเมนูหลัก & ตั้งชื่อผู้เล่น (`MainMenu.unity` & `MainMenuManager.cs`)**:
     - แยกปุ่ม **`PLAY OFFLINE`** (`newGameBtn` — เลือกตัวละครเข้าเล่นคนเดียว) และปุ่ม **`ONLINE`** (`onlineBtn` — ย้ายไปซีน `Lobby_NetCode`)
     - ระบบตั้งชื่อผู้เล่นมุมขวาบน (`AccountName`, `ChangeNamePanel`, `ChangeNameButton`): เริ่มต้นสุ่มรหัส 7 ตัวอักษร (เช่น `9CS51XE`) หรือโหลดจาก `PlayerPrefs` (`TLE_PlayerAccountName`) เมื่อกดปุ่มเหลืองจะซ่อน `AccountName` แล้วเปิดตา `ChangeNamePanel` โดยใช้ชื่อปัจจุบันแสดงใน `Placeholder` และเมื่อกดยืนยันจะปิดตา `ChangeNamePanel` แล้วอัปเดตชื่อใหม่ไปใช้ทั้งใน Lobby และ Gameplay
  2. **ระบบค้นหาห้องและห้องเตรียมตัว 6 คน (`Lobby_NetCode.unity`)**:
     - ออกแบบสคริปต์ `MultiplayerLobbyUI`, `RoomThumbnailUI`, และ `RoomMemberSlotUI` ให้ผูกผ่านช่อง `[SerializeField]` ใน Inspector 100% (เปลี่ยนชื่อ GameObject ได้อิสระ)
     - **`SelectRoomPanel`**: ค้นหาห้องในวง LAN อัตโนมัติด้วย UDP Broadcast Beacon (Port `47777`) โดยใช้ชื่อหัวห้องเป็นชื่อห้อง หากยังไม่มีห้องใน LAN จะซ่อนแถวห้องทั้งหมด
     - **`RoomInfoPanel`**: รองรับผู้เล่น 1–6 คน เชื่อมกับ `CharacterDatabase.asset` (`ScriptableObject` ตัวเดียวครบทั้ง 7 อาชีพ) หากผู้เล่นเพิ่งเข้าห้องและยังไม่ได้เลือกตัวละครจะแสดงรูป Default (เข็มทิศ) ปุ่มสีเหลืองรูปประแจ (`selectCharacterButton`) แสดงเฉพาะช่องของตัวเราเองเพื่อเปิดหน้าเลือกตัวละคร ปุ่มสีแดง `[X]` (`kickButton`) แสดงเฉพาะหัวห้องเพื่อเตะลูกทีม และปุ่ม `ENTER WASTELAND` จำกัดให้หัวห้องกดเริ่มเกมได้คนเดียว
  3. **ระบบแยกเดินข้ามช่องพิกัด `(Stage X, Y)` แบบอิสระ & เจอกันเมื่ออยู่ช่องเดียวกัน**:
     - ผู้เล่นแต่ละคนเดินเข้าประตู `TransitionPortal` ข้ามช่องพิกัด `(X, Y)` บนแผนที่โลก 128x128 ได้อย่างอิสระโดยไม่ดึงเพื่อนทั้งตี้ตามไป
     - ซิงค์พิกัด `(stageX, stageY)` ของผู้เล่นแต่ละคนตลอดเวลา: หากผู้เล่นอยู่ในช่อง `(X, Y)` **เดียวกัน** จะเปิดแสดงตัวละคร เห็นกระสุน/การฟัน และสู้ซอมบี้ร่วมกันทันที แต่หากอยู่ **คนละช่อง** จะซ่อนตัวละครและกระสุนของอีกฝ่ายไว้จนกว่าจะเดินมาเจอกันในช่องเดียวกัน
     - แถบสถานะปาร์ตี้ `PartyStatusHUD` ด้านขวาของหน้าจอแสดงชื่อ อาชีพ หลอดเลือด สถานะล้มรอชุบ (Downed & Revive 30s) และพิกัดช่องปัจจุบัน `(X,Y)` ของสมาชิกแต่ละคนในทีม

---

## 22. ระบบ Cinematic Cutscene สำหรับอีเวนต์ `FleeingSurvivor` & Camera Whip-Pan
- **สคริปต์ & เอกสารที่เกี่ยวข้อง**:
  - [`RandomStageEventManager.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Managers/RandomStageEventManager.cs) (`FleeingSurvivorCutsceneRoutine`)
  - [`FleeingSurvivorRunner.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Combat/FleeingSurvivorRunner.cs)
  - [`CameraFollow.cs`](file:///d:/TLEGIT/TheLastEmpire/Assets/Scripts/Runtime/Player/CameraFollow.cs) (`SetTemporaryTarget`, `ResetTarget`)
  - [`GDD/event-cutscene-design.md`](file:///d:/TLEGIT/TheLastEmpire/GDD/event-cutscene-design.md)
- **รายละเอียด**:
  - **ลำดับ Cutscene เปิดตัวเหตุการณ์:** ล็อกอินพุตผู้เล่นชั่วคราว โฟกัสกล้อง 1.0 วินาที -> ทำ **Camera Whip-Pan** (`smoothTime: 0.12f`) ข้ามห้องไปจับภาพ Maya ที่ฝั่งตรงข้าม -> แสดงกล่องบทพูดตะโกนเตือน (`"Help me!! They're right behind me!!"`) -> Maya เริ่มวิ่งหนีเข้าหาผู้เล่นและฝูงศัตรู 4–5 ตัวสปอว์นไล่หลังด้วยสปีดคัตซีน `1.0 m/s` -> ครบ 2.0 วินาทีเร่งสปีดศัตรูเป็น `3.5 m/s`, คืนกล้องกลับหาผู้เล่น, และปลดล็อกให้ต่อสู้ทันที
  - **แก้ไขระดับพื้นดิน (NPC & Enemy Grounding):** ปรับ `floorY` และ `naturalGroundY` จาก `CapsuleCollider` ให้เท้าของ NPC และศัตรูที่ไล่กวดสัมผัสพื้น `Y = 0` พอดี ไม่จมหรือลอย

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

## 18. สร้างโมเดล 3D ตัวละครใหม่ `Chibi_BlondeBrawler_Rigged` (รองรับ `Chibi_Player_Controller`)
- **ไฟล์ที่เกี่ยวข้อง**:
  - [`Tools/Create_Blonde_Brawler.py`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Tools/Create_Blonde_Brawler.py)
  - [`Assets/GeneratedModels/Chibi_BlondeBrawler_Rigged.fbx`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_BlondeBrawler_Rigged.fbx)
  - [`Assets/GeneratedModels/Chibi_BlondeBrawler_Source.blend`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_BlondeBrawler_Source.blend)
  - [`Assets/GeneratedModels/Chibi_BlondeBrawler_Preview.png`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_BlondeBrawler_Preview.png)
- **รายละเอียด**:
  - สร้างโมเดล 3D Toy-Chibi ชายผมทองทรงตั้งแหลม คาดผ้าคาดหัวสีกรมท่า สวมแว่นกันแดดทรงเหลี่ยมสีดำพร้อมแถบแสงสะท้อน แปะพลาสเตอร์ยาที่แก้มขวา ยิ้มกว้างเห็นฟันขาว สวมเสื้อแจ็คเก็ตสีส้มคอตั้งขอบขาวทับเสื้อคอเต่าสีกรมท่า พร้อมสายรัดอกยุทธวิธี หัวเข็มขัดเงิน กระเป๋าอกคู่ ถุงมือชกเปิดนิ้วเสริมเกราะสนับมือสีส้ม กางเกงคาร์โก้สีกรมท่าสนับเข่า และรองเท้าบูท
  - ใช้โครงกระดูก `Chibi_Base_Rig` (18 Bones) และชุด Animation ครบทั้ง 11 ท่า (`Idle`, `Walk`, `Run`, `DashRoll`, `Slash1`, `Slash2`, `Slash3`, `GunHold`, `Shoot`, `Death`, `Sit`) ที่เข้ากันได้ 100% กับ `Chibi_Player_Controller` เหมือนโมเดล Chibi ชุดก่อนหน้า

---

## 19. ระบบ HUD เครื่องผลิตน้ำสะอาด (Water Purifier HUD & Capacity 3 Bottles)
- **สคริปต์ & พรีแฟบที่เกี่ยวข้อง**:
  - [`Assets/Scripts/Runtime/UI/WaterPurifierHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/WaterPurifierHUD.cs)
  - [`Assets/Scripts/Runtime/Building/WaterPurifierBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/WaterPurifierBuilding.cs)
  - [`Assets/Resources_Addressable/Prefabs/Environment/Craft/12_Water_Purifier_Station.prefab`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Prefabs/Environment/Craft/12_Water_Purifier_Station.prefab)
  - [`Assets/Scenes/Gameplay_3D.unity`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scenes/Gameplay_3D.unity)
- **รายละเอียด**:
  - กำหนดความจุสูงสุดของเครื่องกลั่นน้ำเป็น **3 ขวดต่อเครื่อง** (`maxStoredWater = 3`) และเก็บสำรองในเครื่องเพื่อรอผู้เล่นมากดเก็บ
  - สร้างระบบหน้าต่าง `WaterPurifierHUD` ที่จะเปิดขึ้นมาเฉพาะเมื่อผู้เล่นเดินเข้าไปกด [E] Interact กับเครื่องผลิตน้ำ
  - ภายในหน้าต่างแบ่งแถวแสดงผล 3 ช่องตามสถานะจริง:
    - **เสร็จแล้ว (Produced)**: แสดงไอคอนขวดน้ำ, ชื่อ "Water Bottle" x1, และปุ่ม `[COLLECT]` ให้กดเก็บทีละขวดได้
    - **กำลังกลั่น (In Production)**: แสดงเปอร์เซ็นต์ % ความคืบหน้า และเวลานับถอยหลัง (เช่น `Purifying... (45%)` `33s`) พร้อมปุ่มสถานะ `[PURIFYING]` (ไม่สามารถกดได้)
    - **รอคิว (Waiting Queue)**: แสดงสถานะรอคิวผลิตขวดถัดไป
  - **ปุ่ม [Take all]**: รวบรวมน้ำสะอาดทั้งหมดที่ผลิตเสร็จแล้วในเครื่องเข้ากระเป๋าผู้เล่นทันทีในคลิกเดียว
  - **กล่อง Description Text**: แสดงรายละเอียดเครื่องจักร, ความจุถัง, อัตราการกลั่น, และสถานะพลังงานไฟฟ้า (40 ⚡)
  - รองรับการปิดหน้าต่างด้วยปุ่มสีแดง, กด [ESC], หรือเดินห่างออกจากตัวเครื่องเกินระยะตรวจจับ

---

## 20. แก้ไขระบบคราฟต์ไอเทม: บังคับกดค้างเท่านั้น (Strict Hold to Craft Fix)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`Assets/Scripts/Runtime/UI/CraftThumbnail.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CraftThumbnail.cs)
- **สาเหตุ & การแก้ไข**:
  - เดิมใน `HandleCraftButtonClick()` มีการเรียก `ExecuteCraft()` โดยตรงเมื่อ Event `Button.onClick` ทำงาน ส่งผลให้การคลิกเพียงครั้งเดียว (Single Click) ลัดขั้นตอนและสั่งคราฟต์ไอเทมทันทีโดยไม่ต้องกดค้าง
  - แก้ไขให้การคลิกธรรมดาทำหน้าที่เพียงเลือกดูรายละเอียดแถวนั้น (`HandleRowSelectedClicked()`)
  - บังคับให้การคราฟต์จะเกิดขึ้นก็ต่อเมื่อผู้เล่น **กดคลิกค้าง (Hold)** บนปุ่มประแจจนหลอดกลม Radial Fill หมุนเต็ม 100% (ครบระยะเวลา `holdDuration = 1.0s`) เท่านั้น หากปล่อยมือก่อนหลอดจะค่อยๆ ลดกลับเป็น 0 และไม่เกิดการคราฟต์

---

## 21. ระบบโครงข่ายไฟฟ้าฐานและเชื่อมต่อเครื่องกรองน้ำ (Base Power Grid System & Water Purifier)
- **สคริปต์ที่เกี่ยวข้อง**:
  - [`Assets/Scripts/Runtime/Building/PowerGridManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/PowerGridManager.cs)
  - [`Assets/Scripts/Runtime/Building/GeneratorBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/GeneratorBuilding.cs)
  - [`Assets/Scripts/Runtime/Building/WaterPurifierBuilding.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Building/WaterPurifierBuilding.cs)
  - [`Assets/Scripts/Runtime/UI/WaterPurifierHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/WaterPurifierHUD.cs)
- **รายละเอียด**:
  - สร้าง `PowerGridManager` จัดการระบบไฟฟ้าในฐาน คำนวณความต้องการไฟ (Demand) เทียบกับกำลังการผลิต (Supply)
  - เครื่องปั่นไฟ `GeneratorBuilding` จ่ายไฟ 60W ต่อเครื่อง ในรัศมี 20 เมตร
  - เครื่องกลั่นน้ำ `WaterPurifierBuilding` ใช้ไฟ 40W (`powerDemand = 40.0f`):
    - **กรณีไม่มีเครื่องปั่นไฟในรัศมี**: ประสิทธิภาพพลังงาน = 0.0 (`PowerEfficiency = 0`) เครื่องจะหยุดนิ่งสนิท 0% ไม่เดินรอบการกลั่น และขึ้นสถานะ `[OFFLINE - NO POWER ⚡]`
    - **กรณีพลังงานไม่พอ (Brownout)**: ความเร็วในการกลั่นน้ำจะช้าลงตามอัตราส่วนไฟฟ้าจริง (Supply / Demand)
    - **กรณีพลังงานเพียงพอ**: เครื่องทำงานเต็มประสิทธิภาพ 100% ตามความเร็วรอบปกติ

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
