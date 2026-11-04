# สรุปบันทึกการพัฒนาเกม (Work Summary) - 4 พฤศจิกายน 2026

เอกสารฉบับนี้จัดทำขึ้นเพื่อรวบรวมฟีเจอร์, โมเดล 3D ตัวละครหลัก, ระบบแอนิเมชัน 10 ท่า, การควบคุม Layer Mask และการปรับปรุงระบบการต่อสู้/การเคลื่อนที่ทั้งหมดที่พัฒนาในโปรเจกต์ **The Last Empire** ในวันที่ 4 พฤศจิกายน 2026 เพื่อให้ทีมงานและ AI สามารถตรวจสอบและนำไปต่อยอดได้ทันที

---

## 📑 สารบัญระบบที่พัฒนาและอัปเดตล่าสุด
1. [โมเดล 3D ตัวละครหลัก Chibi Tactical Girl สไตล์ Toy-Chibi / Nendoroid พร้อม Rig 20 กระดูก](#1-โมเดล-3d-ตัวละครหลัก-chibi-tactical-girl-สไตล์-toy-chibi--nendoroid-พร้อม-rig-20-กระดูก)
2. [การปรับจูน Material เนื้อด้าน (Matte Finish) และระบบ Postprocessor อัตโนมัติ](#2-การปรับจูน-material-เนื้อด้าน-matte-finish-และระบบ-postprocessor-อัตโนมัติ)
3. [ชุดแอนิเมชัน 10 ท่าครบวงจร และท่าตายสุดเท่ 1.5 วินาที (10-Clip Animation Suite & Dramatic Death)](#3-ชุดแอนิเมชัน-10-ท่าครบวงจร-และท่าตายสุดเท่-15-วินาที-10-clip-animation-suite--dramatic-death)
4. [ระบบ UpperBody AvatarMask สำหรับเดินยิง, แสงไฟปากกระบอกปืน และการแก้บั๊กฟันประชิดหลังถือปืน](#4-ระบบ-upperbody-avatarmask-สำหรับเดินยิง-แสงไฟปากกระบอกปืน-และการแก้บั๊กฟันประชิดหลังถือปืน)
5. [ระบบความเร็ววิ่งเร็ว และลดความเร็วเหลือ 0.75x เดินปกติ 1 วินาทีเมื่อกดยิงปืน (Sprint Speed & 0.75x Shoot Penalty)](#5-ระบบความเร็ววิ่งเร็ว-และลดความเร็วเหลือ-075x-เดินปกติ-1-วินาทีเมื่อกดยิงปืน-sprint-speed--075x-shoot-penalty)
6. [ระบบ ScriptableObject Status & Config สำหรับปรับแต่งตัวละครทั้งหมด (Customizable CharacterData ScriptableObjects & Custom Inspector)](#6-ระบบ-scriptableobject-status--config-สำหรับปรับแต่งตัวละครทั้งหมด-customizable-characterdata-scriptableobjects--custom-inspector)
7. [สกิลใหม่ The Scavenger: Grappling Hook สลิงดึงตัวเข้าหาสิ่งปลูกสร้างสไตล์ Spider-Man](#7-สกิลใหม่-the-scavenger-grappling-hook-สลิงดึงตัวเข้าหาสิ่งปลูกสร้างสไตล์-spider-man)

---

## 1. โมเดล 3D ตัวละครหลัก Chibi Tactical Girl สไตล์ Toy-Chibi / Nendoroid พร้อม Rig 20 กระดูก

### ไฟล์หลัก:
* [Chibi_TacticalGirl_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_TacticalGirl_Rigged.fbx) — โมเดล 3D ตัวละครผู้เล่นหลักพร้อมโครงกระดูก `Chibi_Base_Rig` และ AnimationClips ทั้ง 10 ท่าฝังในตัว
* [Chibi_TacticalGirl_Source.blend](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_TacticalGirl_Source.blend) — ไฟล์ต้นฉบับ Blender 5.2 LTS
* [Chibi_TacticalGirl_Preview.png](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_TacticalGirl_Preview.png) — ภาพเรนเดอร์พรีวิวตัวละคร
* [PlayerPrefabs.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/Player/PlayerPrefabs.prefab) — พรีแฟบผู้เล่นหลักที่ผูกโมเดลและ Animator เรียบร้อย

### รายละเอียดโครงสร้างและสัดส่วน (Toy-Chibi Proportions):
* **สัดส่วนมาตรฐานเดียวกับ `ChibiGhostOperator` (`~2.0m`)**:
  * ปรับขนาดศีรษะลดลง 25% จากโมเดลฐานเดิม ให้สมดุลกับลำตัวและหมวกแก๊ปยุทธวิธีสีดำ (`Mat_Cap_Black`)
  * ขากางเกงทรงกระบอกสั้นตัน (`z = 0.20..0.68`) พร้อมสายรัดต้นขาสีเหลืองทอง (`ThighBand`), สนับเข่า 2 ชั้น (`KneePadOuter` / `KneePadInner`), และรองเท้าบูทยุทธวิธีหัวมนพื้นหนา (`BootUpper` / `BootSole`)
  * แจ็คเก็ตยุทธวิธีสีเหลืองทองตัวสั้น (`Mat_Jacket_Gold`) คอปกตั้ง (`Jacket_Collar`) กระเป๋าหน้าอกซ้าย-ขวา สายรัดอกสีดำพร้อมหัวเข็มขัดโลหะ เกราะไหล่สีเทาปืน (`ShoulderPad`) และถุงมือยุทธวิธีเปิดนิ้วสีน้ำเงิน (`Mat_Glove_Blue`)
  * ความยาวแขนกะทัดรัด (`|x| = 0.18..0.90` ที่ `z = 1.23`) ทำให้ในท่ายืน `Idle` แขนทั้งสองข้างทิ้งตัวลงข้างลำตัวตรงเป็นธรรมชาติ ไม่โก่งเป็นวงเล็บ `(   )`
* **ใบหน้าและดวงตาสไตล์ Nendoroid แนบสนิทผิวหน้า (`head_surface_point`)**:
  * คำนวณพิกัดและเวกเตอร์ตั้งฉากของพื้นผิวใบหน้าเพื่อวางกรอบตา ตาขาว ม่านตาสีส้มอำพัน (`Mat_Eye_Amber`) ไฮไลท์สีทองด้านล่าง รูม่านตาดำเงา (`Mat_Glossy_Black`) จุดสะท้อนแสงสีขาวคู่ ขอบตาดำแบบเฉี่ยว และคิ้วสีม่วงเข้ม ให้แนบสนิทไปกับความโค้งของใบหน้าในระยะ `0.6–3.1mm` ไร้เงาตกกระทบเป็นก้อน
* **ทรงผมอนิเมะสีม่วงแบบ Flat-Ribbon & หางม้าชี้ฟู**:
  * สร้างปอยผมหน้าม้า ปอยผมข้างแก้ม และหางม้าด้านบนด้วยเส้นโค้ง Catmull-Rom หน้าตัดแบนโค้ง (`create_ribbon_hair_strand`) พร้อมก้อนผมปิดขมับ (`Hair_Temple`) ไร้ช่องว่างด้านข้าง

---

## 2. การปรับจูน Material เนื้อด้าน (Matte Finish) และระบบ Postprocessor อัตโนมัติ

### ไฟล์หลัก:
* [ChibiArmPoseFixer.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/ChibiArmPoseFixer.cs) (`ChibiTacticalGirlMaterialPostprocessor`)

### รายละเอียดการแก้ไข:
* **แก้ปัญหาโมเดลเงาเหมือนพลาสติกเคลือบ**:
  * แปลงค่าสี Material จาก sRGB เป็น Scene-Linear (`srgb_to_lin`) ที่ต้นทางใน Blender พร้อมตั้งค่า `Roughness = 0.88 – 0.92` และ `Specular = 0.03 – 0.05` สำหรับผิวหนัง เสื้อผ้า หมวก ผม และรองเท้าบูททั้งหมด
  * คงความเงาวาว (`Roughness = 0.06 – 0.12`, `_Smoothness = 0.90`) ไว้เฉพาะที่ดวงตา (`Mat_Glossy_Black`, `Mat_Eye_Amber`, `Mat_Eye_Gold_Hi`, `Mat_Eye_White`)
  * เพิ่ม `ChibiTacticalGirlMaterialPostprocessor` ใน Unity Editor เพื่อคุมค่า `_Smoothness = 0.08` บนวัสดุเนื้อด้านทุกครั้งที่ Import ไฟล์ FBX โดยอัตโนมัติ

---

## 3. ชุดแอนิเมชัน 10 ท่าครบวงจร และท่าตายสุดเท่ 1.5 วินาที (10-Clip Animation Suite & Dramatic Death)

### ไฟล์หลัก:
* [Chibi_Player_Controller.controller](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_Player_Controller.controller)
* [Chibi_TacticalGirl_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_TacticalGirl_Rigged.fbx)
* [Chibi_Base_Rigged.fbx](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_Base_Rigged.fbx)

### รายการแอนิเมชันทั้ง 10 ท่า (24 FPS):
1. **`Idle` (49 เฟรม, Loop)**: ยืนหายใจขยับอกและไหล่เบาๆ แขนทิ้งตัวข้างลำตัวอย่างเป็นธรรมชาติ
2. **`Walk` (25 เฟรม, Loop)**: เดินก้าวเท้าสลับแกว่งแขนสมดุล
3. **`Run` (17 เฟรม, Loop)**: วิ่งโน้มตัวไปข้างหน้า ก้าวขายาวและแกว่งแขนกระฉับกระเฉง
4. **`GunHold` (25 เฟรม, Loop)**: ท่ายืนประทับปืนสองมือระดับอก
5. **`Shoot` (15 เฟรม)**: ท่ายิงปืนสองมือพร้อมแรงถีบ Recoil กระตุกไหล่และลำตัว
6. **`Slash1` (19 เฟรม)**: คอมโบฟันระยะประชิดขั้นที่ 1 (ฟันกวาดขวาไปซ้าย)
7. **`Slash2` (19 เฟรม)**: คอมโบฟันระยะประชิดขั้นที่ 2 (ฟันตวัดย้อนซ้ายไปขวา)
8. **`Slash3` (23 เฟรม)**: คอมโบฟันระยะประชิดขั้นที่ 3 (ก้าวบิดตัวฟันผ่าลงเต็มแรง 360 องศา)
9. **`DashRoll` (21 เฟรม)**: พุ่งตัวก้มต่ำหลบหลีก (Tactical Roll / Evasive Dash)
10. **`Death` (37 เฟรม = 1.5 วินาที, Clamp Forever)**:
    * **เฟรม 0–7 (0.0s – 0.3s)**: โดนดาเมจกระแทกตัวแอ่นสะบัดไปด้านหลัง แขนกางออก ศีรษะเงย
    * **เฟรม 7–19 (0.3s – 0.8s)**: ทรุดเข่ากระแทกพื้น มือกุมหน้าอก ก้มหน้าหอบ
    * **เฟรม 19–36 (0.8s – 1.5s)**: ล้มฟุบตะแคงลงกับพื้นพร้อมแรงสะท้อนเบาๆ แล้วแน่นิ่งจนจบเกม (`HandlePlayerDeath()` ใน [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs) สั่ง Trigger `Die` และ延时แสดงหน้าจอ Game Over หลังเล่นท่าตายครบ 1.5 วินาที)

---

## 4. ระบบ UpperBody AvatarMask สำหรับเดินยิง, แสงไฟปากกระบอกปืน และการแก้บั๊กฟันประชิดหลังถือปืน

### ไฟล์หลัก:
* [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs)
* [Chibi_Player_Controller.controller](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_Player_Controller.controller)
* [Chibi_UpperBody_Mask.mask](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Chibi_UpperBody_Mask.mask)

### รายละเอียดการทำงาน:
* **เดินยิงปืนแยกช่วงบน-ช่วงล่าง (`UpperBody` Layer 1)**:
  * ใช้ `Chibi_UpperBody_Mask.mask` เปิดใช้งานเฉพาะกระดูก `Spine`, `Chest`, `Neck`, `Head`, และแขนซ้าย-ขวา ส่วน `Hips` และขาซ้าย-ขวาปล่อยให้ `Base Layer` (Layer 0) เล่นท่า `Walk` หรือ `Idle` ตามปกติ
* **การแก้ไขปัญหาไม่เล่นท่าฟัน (`Slash1/2/3`) หลังจากถือหรือยิงปืน**:
  * **สาเหตุ**: เดิม Layer 1 (`UpperBody`) มีค่า `Weight = 1.0` ค้างไว้ตลอดเวลา และสถานะ `UpperBody_Empty` มี `Motion: None` ทำให้เมื่อยิงปืน (`Shoot`) ไปแล้ว 1 ครั้ง Mecanim จะค้างเฟรมสุดท้ายของท่ายิงปืนทับลำตัวช่วงบนตลอดเวลา จนบดบังท่าฟัน `Slash1/2/3` บน Layer 0
  * **วิธีแก้ไข**:
    1. ตั้งค่าเริ่มต้น `m_DefaultWeight: 0` บน Layer `UpperBody` และเปิด `m_WriteDefaultValues: 1` บน `UpperBody_Empty`
    2. เพิ่มตัวนับเวลา `_shootAnimLayerTimer` และ `_meleeAnimTimer` ใน [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs) เพื่อเปิด `SetLayerWeight(1, 1f)` เฉพาะขณะยิงปืน (`0.42s`) และสั่งตัด `SetLayerWeight(1, 0f)` ทันทีที่กดฟันประชิด (`MeleeAttack()`) หรือพุ่งหลบ (`StartDash()`)
* **แสงไฟปากกระบอกปืนไดนามิก (`MuzzleFlashLight`)**:
  * สร้าง Point Light สีส้มทอง (`Intensity = 10`, `Range = 6m`) สว่างวาบที่ตำแหน่งปากกระบอกปืนเป็นเวลา `0.08s` ทุกครั้งที่ยิง แล้วค่อยๆ เฟดดับลงอย่างนุ่มนวล

---

## 5. ระบบความเร็ววิ่งเร็ว และลดความเร็วเหลือ 0.75x เดินปกติ 1 วินาทีเมื่อกดยิงปืน (Sprint Speed & 0.75x Shoot Penalty)

### ไฟล์หลัก:
* [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs)
* [CharacterData.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Data/CharacterData.cs)
* [CHAR_04_Marksman.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/Characters/CHAR_04_Marksman.asset)

### รายละเอียดการทำงาน:
* **ความเร็วและสกิล Eagle Eye ของสไนเปอร์ (Marksman)**:
  * สกิล `Eagle Eye` ของ Marksman ได้รับการปรับแต่งตามดีไซน์สไนเปอร์: เมื่อกด Shift จะเข้าสู่สถานะเล็งยิงทางยุทธวิธี
  * **ซูมกล้องออก 30% (1.30x)**: ปรับขนาดมุมมองกล้องให้กว้างขึ้น 30% อย่างนุ่มนวล เพื่อเห็นพื้นที่รอบข้างและศัตรูในระยะไกลชัดเจนยิ่งขึ้น
  * **เส้นเล็งเลเซอร์สีแดง (Red Laser Sight & Target Dot)**: วาดเส้นเล็งสีแดงสด (`LineRenderer`) พุ่งตรงออกจากตัวละครไปยังทิศทางที่เล็งตามระยะยิงของอาวุธจริง (ตรวจจับสิ่งกีดขวาง/กำแพงด้วย Raycast) พร้อมวงกลมเป้าเล็งสีแดงที่ปลายระยะยิง
  * **การเคลื่อนที่ขณะเล็ง**: เดินย่องเล็งอย่างประณีตที่ `0.35x` (ปรับจูนผ่าน SO `eagleEyeMoveSpeedMultiplier` ได้อย่างอิสระ) พร้อมสลับแอนิเมชันเดินเล็งอย่างสมจริง
* **เงื่อนไขระบบวิ่งและยิงปืน (สำหรับทุกตัวละคร)**:
  1. **กด Shift วิ่งโดยไม่ได้ยิงปืน**: สำหรับตัวละครทั่วไป (Vanguard, Juggernaut, Phantom, Scavenger) ความเร็วจะกลายเป็น **วิ่งเร็ว** เสมอ (`currentSpeed = moveSpeed * sprintSpeedMultiplier` เช่น 1.5x - 1.75x) พร้อมเล่นแอนิเมชันวิ่ง `IsRunning = true`
  2. **วิ่งอยู่แล้วกดยิงปืน**: ทันทีที่กดยิง ตัวนับเวลา `_shootSprintLockTimer = 1.0f` จะทำงาน และความเร็วจะถูกปรับเป็น **เท่าเดิน * 0.75** ทันที (`currentSpeed = moveSpeed * 0.75f`) เป็นเวลา 1.0 วินาที
  3. **หยุดยิงครบ 1.0 วินาที**: หากผู้เล่นยังคงกดปุ่ม Shift ค้างอยู่ ความเร็วจะสปริงกลับไปเป็น **วิ่งเร็ว** ทันทีโดยอัตโนมัติ และสลับกลับไปเล่นท่าวิ่ง `IsRunning = true`

---

## 6. ระบบ ScriptableObject Status & Config สำหรับปรับแต่งตัวละครทั้งหมด (Customizable CharacterData ScriptableObjects & Custom Inspector)

### ไฟล์หลัก:
* [CharacterData.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Data/CharacterData.cs) — สคริปต์ ScriptableObject กำหนด Data Schema สำหรับ Stat, Loadout, ความหิว, อัตราวิ่ง และ Skill ตัวละครทุกตัว
* [CharacterDataEditor.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/CharacterDataEditor.cs) — Custom Inspector สไตล์ Survivor Dossier จัดหมวดหมู่อย่างสวยงาม พร้อมแถบสี, พรีวิวภาพตัวละคร, กล่องพรีวิว UI สด และซ่อน/แสดงช่องจูนตามสกิลที่เลือก
* [CharacterAssetCreator.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/CharacterAssetCreator.cs) — ปรับปรุงไม่ให้เขียนทับค่าที่ผู้พัฒนาปรับแต่งไว้ใน Inspector ทุกครั้งที่ Reload Domain พร้อมเมนูสร้าง/รีเซ็ตอย่างปลอดภัย
* [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs) — เชื่อมต่อการคำนวณความเร็ววิ่ง, ล็อกความเร็วหลังยิง, การแจกกระสุนตามลิสต์, อาหารเริ่มต้น, และอัตราลดความหิวจาก SO โดยตรง
* [ShiftAbilities.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Character/ShiftAbilities.cs) — สกิลประจำตัวทั้ง 5 ตัวละครดึงค่า Tuning Parameters (ความเร็วพุ่ง, สตั้น, อัตราดูดเลือด/สโลว์, อัตรากินความหิว) จาก SO แบบเรียลไทม์
* [DamageCalculator.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/DamageCalculator.cs) & [Health.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/Health.cs) — คำนวณ Critical Chance, Critical Multiplier และ Armor Damage Reduction จาก CharacterData
* [CharacterSelectUI.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CharacterSelectUI.cs) & [MainMenuManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/MainMenuManager.cs) — แสดงข้อมูล Stat, ข้อมูลกระสุน และเสบียงบนหน้าต่างเลือกตัวละครอย่างตรงไปตรงมาตามค่าที่ปรับใน SO

### ไฟล์ Asset ตัวละครที่ปรับแต่งได้ทันทีใน Inspector (`Assets/Resources/Database/Characters/`):
* `CHAR_01_Vanguard.asset` (The Vanguard: สมดุล, วิ่งเร็ว 1.5x, สกิล Adrenaline Rush ปรับจูนได้)
* `CHAR_02_Juggernaut.asset` (The Juggernaut: เลือด 150, เกราะติดตัว 15%, สกิลพุ่งชน Unstoppable Charge ปรับจูนได้)
* `CHAR_03_Phantom.asset` (The Phantom: วิ่งไว 6.0 m/s, สกิล Shadow Step วิ่งทะลุศัตรูพร้อมการันตีคริติคอลหลังออกจากเงามืด)
* `CHAR_04_Marksman.asset` (The Marksman: อัตราคริติคอลพื้นฐาน 35%, กระสุน Medium 20 + Light 20, สกิล Eagle Eye ซูม 1.35x ยิงทะลุ)
* `CHAR_05_Scavenger.asset` (The Scavenger: โบนัสตกของ +25%, โบนัสทอง +30%, สกิล Grappling Hook ยิงสลิงดึงตัวเข้าหาสิ่งปลูกสร้างสไตล์ Spider-Man, Passive กินอาหารฮีลเลือด +10 HP)

### จุดสำคัญที่ได้รับการแก้ไข (Root-Cause Fix):
1. **แก้ปัญหา Inspector ถูก Reset คืนค่าเริ่มต้นทุกครั้งที่ Recompile**:
   * เดิม `CharacterAssetCreator.cs` มีแอตทริบิวต์ `[InitializeOnLoad]` และเขียนทับตัวแปรทุกตัวบนไฟล์ `.asset` ด้วยฮาร์ดโค้ดเดิมทุกครั้งที่ Unity คอมไพล์ใหม่
   * ได้ทำการลบ `[InitializeOnLoad]` ออก พร้อมเปลี่ยนระบบเป็น **Safe Load** (หากไฟล์ `.asset` มีอยู่แล้ว จะไม่แตะต้องค่าที่ผู้ใช้อิดิทไว้เด็ดขาด) และเพิ่มเมนูรีเซ็ตสำหรับ Factory Reset แยกชัดเจน
2. **ปลดล็อกการตั้งค่ากระสุนเริ่มต้นและเสบียง (Decoupled Loadouts)**:
   * เพิ่ม `StartingAmmoConfig` แบบ List ทำให้กำหนดประเภทกระสุนกี่ชนิดและจำนวนเท่าใดก็ได้ใน Inspector ของแต่ละตัวละคร
   * เพิ่ม `startingRations` สำหรับปรับจำนวนขนมปังเริ่มต้น
   * เชื่อมต่อ `shootSprintLockDuration` เข้ากับ SO เพื่อให้ผู้พัฒนาสามารถปรับจูนระยะเวลาห้ามวิ่งหลังยิงของแต่ละตัวละครได้อย่างอิสระ

---

## 7. สกิลใหม่ The Scavenger: Grappling Hook สลิงดึงตัวเข้าหาสิ่งปลูกสร้างสไตล์ Spider-Man

### ไฟล์หลัก:
* [ShiftAbilities.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Character/ShiftAbilities.cs) (`GrapplingHookAbility`)
* [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs)
* [CharacterData.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Data/CharacterData.cs)
* [CharacterDataEditor.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/CharacterDataEditor.cs)
* [CharacterDatabase.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Data/CharacterDatabase.cs)
* [CharacterAssetCreator.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/CharacterAssetCreator.cs)
* [CHAR_05_Scavenger.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/Characters/CHAR_05_Scavenger.asset)

### กลไกการทำงาน (Grappling Hook Mechanics):
1. **การกดใช้งานและการเล็งทิศทาง (Targeting & Firing)**:
   * ผู้เล่น The Scavenger กดปุ่ม **[Shift]** จะเป็นการยิงสายเคเบิลสลิงเอาชีวิตรอดพุ่งตรงไปยังทิศทางที่เล็งเมาส์ (`_aimDirection`) ในระยะไกลสูงสุด `18 เมตร` (ปรับจูนได้)
   * ระบบใช้ `Physics.RaycastAll` ตรวจจับสิ่งปลูกสร้าง, กำแพง, บาริเคด, และสิ่งกีดขวางแนวตั้ง (`normal.y <= 0.65f`) โดยไม่ถูกพื้นเรียบขวาง
   * หากมีศัตรูยืนขวางอยู่ สายสลิงจะพุ่งทะลุกลุ่มศัตรูไปเกาะติดกับสิ่งปลูกสร้างด้านหลัง เพื่อให้ผู้เล่นโหน/ดึงตัวแหวกวงล้อมหนีได้สไตล์ Spider-Man
2. **การยึดเกาะและการลากตัวด้วยความเร็วสูง (Zip & Pull Phase)**:
   * เมื่อสลิงกระทบสิ่งปลูกสร้าง จะสร้างจุดยึดสมอเรืองแสงสีทอง (`GrappleHookAnchor`) บนผนัง
   * วาดเส้นลวดสลิงสีเหลืองอำพันสะท้อนแสง (`LineRenderer`) เชื่อมจากหน้าอกผู้เล่นไปยังจุดที่สลิงเกาะอย่างชัดเจน
   * ลากตัวผู้เล่นพุ่งเข้าหาจุดยึดด้วยความเร็วสูง **22 m/s** (`grapplePullSpeed`)
   * ให้สถานะอมตะชั่วคราว (I-Frames) ขณะพุ่งเพื่อหลบหลีกการโจมตีอันตราย
   * คำนวณจุดหยุดยืนให้เว้นระยะห่างจากผนัง `0.8 เมตร` ตามแนว Normal เพื่อไม่ให้ตัวละครติดหรือคลิปจมเข้าไปในสิ่งปลูกสร้าง
   * มีระบบตรวจจับการชนหรือติดขัด (`_stuckTimer`) และจำกัดเวลาพุ่งสูงสุด (`MaxPullDuration = 1.2s`) เพื่อปลดสลิงทันที ไม่ทำให้ตัวละครติดบั๊กค้าง
3. **กรณีที่ยิงสลิงไม่โดนสิ่งปลูกสร้าง (Miss / Whiff)**:
   * หากยิงไปในที่โล่งและไม่โดนสิ่งปลูกสร้างใดๆ เส้นสลิงจะพุ่งออกไปสุดระยะแล้วหดกลับมาอย่างรวดเร็ว (0.22s) โดยไม่ลากตัวผู้เล่น และลดคูลดาวน์ลงเหลือเพียง `0.8s`
4. **พารามิเตอร์การปรับจูนใน ScriptableObject (`CharacterData`)**:
   * `grappleRange`: ระยะยิงสลิงไกลสุด (ค่าเริ่มต้น: 18m)
   * `grapplePullSpeed`: ความเร็วในการลากดึงตัวผู้เล่น (ค่าเริ่มต้น: 22 m/s)
   * `grappleHungerCost`: ปริมาณความหิวที่ใช้ต่อการยิง 1 ครั้ง (ค่าเริ่มต้น: 3.5 หน่วย)
   * `shiftCooldown`: คูลดาวน์ของสกิล (ค่าเริ่มต้น: 3.0s)
   * รองรับการปรับแต่งผ่าน Survivor Dossier Inspector ใน Unity Editor ได้อย่างอิสระ
