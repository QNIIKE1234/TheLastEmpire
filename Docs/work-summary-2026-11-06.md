# สรุปบันทึกการพัฒนาเกม (Work Summary) - 6 พฤศจิกายน 2026

เอกสารฉบับนี้จัดทำขึ้นเพื่อรวบรวมฟีเจอร์, โมเดล 3D อาวุธครบเซ็ต, การจัดระเบียบโครงสร้างแอนิเมชัน, ระบบการแสดงผลอาวุธในมือผู้เล่น, การปรับขนาดตัวเลขดาเมจ, ระบบการปรับอัตราการลดของพลังงาน, และการปรับปรุงระบบ HUD/การเก็บไอเทมทั้งหมดที่พัฒนาในโปรเจกต์ **The Last Empire** ในวันที่ 6 พฤศจิกายน 2026

---

## 📑 สารบัญระบบที่พัฒนาและอัปเดตล่าสุด
1. [การสร้างโมเดล 3D อาวุธครบเซ็ต 19 ชิ้น พร้อม Material Library](#1-การสร้างโมเดล-3d-อาวุธครบเซ็ต-19-ชิ้น-พร้อม-material-library)
2. [การย้ายและจัดโครงสร้างโฟลเดอร์ Animation (Zero GUID Loss)](#2-การย้ายและจัดโครงสร้างโฟลเดอร์-animation-zero-guid-loss)
3. [ระบบแสดงผลอาวุธในมือผู้เล่น (Weapon Visual Sockets & Auto-Toggle System)](#3-ระบบแสดงผลอาวุธในมือผู้เล่น-weapon-visual-sockets--auto-toggle-system)
4. [การขยายขนาดตัวเลขดาเมจบนหน้าจอเพิ่ม 50% (Floating Damage Numbers 1.5x Scale)](#4-การขยายขนาดตัวเลขดาเมจบนหน้าจอเพิ่ม-50-floating-damage-numbers-15x-scale)
5. [การปรับจูนอัตราการลดของพลังงานและความหิวให้ช้าลงอีกเท่าตัว (Energy Depletion Rate Halved)](#5-การปรับจูนอัตราการลดของพลังงานและความหิวให้ช้าลงอีกเท่าตัว-energy-depletion-rate-halved)
6. [ระบบดูดไอเทมเข้าตัวผู้เล่นอัตโนมัติพร้อม Effect ย่อขนาด (Magnet Item Pickup & Shrink Visual)](#6-ระบบดูดไอเทมเข้าตัวผู้เล่นอัตโนมัติพร้อม-effect-ย่อขนาด-magnet-item-pickup--shrink-visual)
7. [การปรับปรุงป้ายแจ้งเตือนความทรงจำ Amnesia Awakening Banner](#7-การปรับปรุงป้ายแจ้งเตือนความทรงจำ-amnesia-awakening-banner)

---

## 1. การสร้างโมเดล 3D อาวุธครบเซ็ต 19 ชิ้น พร้อม Material Library

สร้างโมเดล 3D ความละเอียดสูงในโฟลเดอร์ [Assets/GeneratedModels/Weapons](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Weapons) ครบทุกชนิดตามรายการใน `ItemDatabase` พร้อม Material Definition [Weapons.mtl](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/GeneratedModels/Weapons/Weapons.mtl) และ `.meta` ครบถ้วน:

### รายการโมเดลอาวุธ 19 ชนิด:
* **อาวุธปืนและระยะไกล (Ranged Weapons):**
  * `Weapon_Pistol.obj` — ปืนพกยุทธวิธีมาตรฐาน 9mm
  * `Weapon_Shotgun.obj` — ปืนลูกซองปั๊มแอ็กชันพร้อมลำกล้องคู่และพานท้าย
  * `Weapon_Rifle.obj` — ปืนไรเฟิลจู่โจมพร้อมแม็กกาซีนโค้งและศูนย์เล็ง
  * `Weapon_SMG.obj` — ปืนกลมือขนาดกะทัดรัดพร้อมกริปจับด้านหน้า
  * `Weapon_SniperRifle.obj` — ปืนสไนเปอร์ไรเฟิลระยะไกลพร้อมกล้องเล็ง Scope และขาทราย Bipod
  * `Weapon_Revolver.obj` — ปืนลูกโม่แม็กนั่มเฮฟวี่ 6 นัด
  * `Weapon_Crossbow.obj` — หน้าไม้ล่าสัตว์พร้อมร่องยิงลูกดอก Bolt
  * `Weapon_RocketLauncher.obj` — ปืนยิงจรวด RPG ทรงกระบอกพร้อมด้ามจับคู่
  * `Weapon_Flamethrower.obj` — ปืนพ่นไฟพร้อมถังเชื้อเพลิงและหัวฉีดคู่
* **อาวุธระยะประชิดและอาวุธพิเศษ (Melee & Special Weapons):**
  * `Weapon_Knife.obj` — มีดยุทธวิธี Combat Knife
  * `Weapon_BaseballBat.obj` — ไม้เบสบอลเสริมแถบเหล็กรัด
  * `Weapon_Machete.obj` — มีดมาเชเต้ฟันทางใบมีดยาว
  * `Weapon_Katana.obj` — ดาบซามูไรคาตานะพร้อมด้ามถักและแผ่นกั้น Tsuba
  * `Weapon_FireAxe.obj` — ขวานดับเพลิงหัวสองด้านสีแดง
  * `Weapon_Sledgehammer.obj` — ค้อนปอนด์ยักษ์สำหรับทุบพื้น Shockwave
  * `Weapon_SurvivalSpear.obj` — หอกเอาชีวิตรอดปลายฉมวกเหล็ก
  * `Weapon_StunBaton.obj` — กระบองไฟฟ้าพร้อมหัวปล่อยกระแสช็อต
  * `Weapon_Chainsaw.obj` — เลื่อยยนต์ตัดไม้พร้อมฟันเลื่อย
  * `Weapon_Grenade.obj` — ลูกระเบิดมือแบบขว้างทรงสับปะรด

---

## 2. การย้ายและจัดโครงสร้างโฟลเดอร์ Animation (Zero GUID Loss)

ทำการย้ายไฟล์ Animation Clips และ Controller ทั้งหมดจากโฟลเดอร์เดิม `Assets/GeneratedModels/` ไปรวมศูนย์ไว้ที่ [Assets/Resources/Image/Animation](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Image/Animation) โดยใช้คำสั่งย้ายแบบรักษารหัส GUID เดิม 100%:

* [Chibi_Player_Controller.controller](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Image/Animation/Chibi_Player_Controller.controller) (GUID: `2e7b3de4c1c44d742b1795abbffe89b4`)
* [Chibi_UpperBody_Mask.mask](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Image/Animation/Chibi_UpperBody_Mask.mask) (GUID: `e5a8b942d71449209bc3a0f12e876543`)
* **9 Animation Clips:**
  * `Chibi_Base_Rig_Idle.anim`
  * `Chibi_Base_Rig_Walk.anim`
  * `Chibi_Base_Rig_Run.anim`
  * `Chibi_Base_Rig_Shoot.anim`
  * `Chibi_Base_Rig_GunHold.anim`
  * `Chibi_Base_Rig_DashRoll.anim`
  * `Chibi_Base_Rig_Slash1.anim`, `Slash2.anim`, `Slash3.anim`
* **ผลการตรวจสอบ:** ตรวจสอบความถูกต้องกับพรีแฟบ [PlayerPrefabs.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/Character/PlayerPrefabs.prefab), [NPCPrefabs.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/Character/NPCPrefabs.prefab), และ [Chibi_Base_Rigged.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/Character/Chibi_Base_Rigged.prefab) พบว่า Animator Controller ยังคงเชื่อมโยงสมบูรณ์ ไม่มีการหลุด (No missing references)

---

## 3. ระบบแสดงผลอาวุธในมือผู้เล่น (Weapon Visual Sockets & Auto-Toggle System)

พัฒนาต่อยอดใน [`PlayerController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs):
* **ช่อง Reference สำหรับ Inspector:**
  * `meleeWeapon`: GameObject เก็บอาวุธประชิด (เชื่อมกับ `Malee` socket บนมือขวาตัวละคร)
  * `rangeWeapon`: GameObject เก็บอาวุธระยะไกล (เชื่อมกับ `Range` socket บนมือขวาตัวละคร)
* **การสลับอาวุธแบบเรียลไทม์:**
  * เมื่อคลิกขวาหรือกด `F` เพื่อฟันประชิด (`MeleeAttack`): ระบบจะเปิดใช้งาน `meleeWeapon` และปิด `rangeWeapon` โดยตรวจจับชื่ออาวุธประชิดที่ถืออยู่ (`CurrentMeleeWeaponName`) เพื่อเปิดเฉพาะโมเดลลูกที่ตรงกัน (เช่น Knife, Katana, Bat, Machete, Axe)
  * เมื่อคลิกซ้ายเพื่อยิงปืน (`ShootWeapon`): ระบบจะเปิดใช้งาน `rangeWeapon` และปิด `meleeWeapon` โดยตรวจจับชื่อปืนที่ถืออยู่ (`CurrentWeaponName`) เพื่อเปิดเฉพาะโมเดลปืนที่ตรงกัน (เช่น Pistol)
  * มีระบบ **Auto-Fallback** ใน `Awake` หากไม่ได้ลากลง Inspector ระบบจะค้นหา Child Socket ให้อัตโนมัติ

---

## 4. การขยายขนาดตัวเลขดาเมจบนหน้าจอเพิ่ม 50% (Floating Damage Numbers 1.5x Scale)

ปรับปรุงระบบแสดงผลตัวเลขดาเมจที่เด้งขึ้นตอนโจมตีใน [`FloatingDamageNumber.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/FloatingDamageNumber.cs) และ [`DamageNumberManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/DamageNumberManager.cs):
* เพิ่ม `GlobalScaleMultiplier = 1.5f` ขยายขนาดตัวเลขดาเมจทุกประเภทขึ้นอีก **50% (1.5 เท่า)**:
  * ดาเมจปกติ (สีเหลืองทอง): ขยายสเกลชัดเจนขึ้น
  * คริติคอล HEADSHOT! (สีส้มเพลิง): ขยายใหญ่พิเศษมองเห็นได้เด่นชัด
  * ตัวเลขบล็อก BLOCKED (สีฟ้าเงิน): ขยายขนาดตามสัดส่วน
* เพิ่มตัวเลือก `damageNumberScale = 1.5f` ใน Inspector ของ `DamageNumberManager` ให้ทีมงานสามารถปรับขยายหรือลดขนาดได้อิสระ

---

## 5. การปรับจูนอัตราการลดของพลังงานและความหิวให้ช้าลงอีกเท่าตัว (Energy Depletion Rate Halved)

แก้ไขปัญหาหลอดพลังงานสีเหลือง (สัญลักษณ์สายฟ้า / ค่าความหิว) ลดเร็วเกินไปใน [`PlayerController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs):
* เพิ่มตัวแปร `hungerDepletionMultiplier = 0.5f` ใน Inspector และผูกลง [`PlayerPrefabs.prefab`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/Character/PlayerPrefabs.prefab)
* **การลดลงปกติ (Idle / Walk):** ลดช้าลงครึ่งหนึ่งจากเดิม `0.5/วินาที` $\rightarrow$ เหลือ `0.25/วินาที` (ทำให้อยู่ได้นานเกือบ 7 นาทีต่อ 100 หน่วย)
* **การวิ่งเร็ว (Sprint):** ลดช้าลงเท่าตัวจากเดิม `2.0/วินาที` $\rightarrow$ เหลือ `1.0/วินาที` (วิ่งต่อเนื่องได้นานถึง 100 วินาที)
* **การใช้สกิลพิเศษ (Shift Abilities):** อัตราการดูดพลังงานจากสกิลต่างๆ (เช่น Adrenaline, Toxic Cloak) ถูกคูณลดลงเหลือ 50%

---

## 6. ระบบดูดไอเทมเข้าตัวผู้เล่นอัตโนมัติพร้อม Effect ย่อขนาด (Magnet Item Pickup & Shrink Visual)

ปรับปรุงใน [`CollectibleItem.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/CollectibleItem.cs):
* ผู้เล่นเพียงเดินเข้าใกล้ไอเทมดรอป ไอเทมจะถูกดูด (Magnetized) พุ่งเข้าหาตัวผู้เล่นทันทีโดยไม่ต้องกดปุ่ม `E`
* เพิ่มลูกเล่นแอนิเมชันตอนดูด: ไอเทมจะค่อยๆ หดเล็กลงตามระยะทางที่เข้าใกล้ตัวผู้เล่น จนกระทั่งดูดเข้าตัวสำเร็จ ให้ฟีลลิ่งการเก็บของที่นุ่มนวลและมี Impact

---

## 7. การปรับปรุงป้ายแจ้งเตือนความทรงจำ Amnesia Awakening Banner

ปรับปรุงระบบใน [`PlayerHUD.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerHUD.cs):
* รองรับการเชื่อมต่อแผง UI `Notification_HUD` แบบปรับแต่งเอง
* เพิ่มระบบ Fallback อัตโนมัติ: ในกรณีที่ UI มีกล่องข้อความเพียง 2 กล่อง (`Name_text` และ `Notification_text`) และไม่ได้ตั้งค่า `Awakening Quote Text` ระบบจะรวมฉายาและบทสนทนาเนื้อเรื่อง (`amnesiaWakingClue`) เข้าไปแสดงในกล่อง `Notification_text` ร่วมกันโดยอัตโนมัติ ไม่ตกหล่น

---

### ประวัติ Git Commits ล่าสุด:
* `17b5b9e` — `feat(player): reduce energy/hunger depletion rate by 50% (twice as slow)`
* `4ec8b2f` — `fix`
* `3ef98d3` — `feat(hud): support 2-line awakening banner fallback combining subtitle and monologue clue`
* `65f12cd` — `feat(combat): scale up floating damage numbers by 50%`
* `3228333` — `feat(player): add melee and ranged weapon visual reference slots and auto-toggle on attack/shoot`
* `9116d3f` — `feat(models): generate 3D weapon models in GeneratedModels/Weapons and relocate animations to Resources/Image/Animation`
