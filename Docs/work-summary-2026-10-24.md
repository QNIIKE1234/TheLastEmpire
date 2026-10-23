# สรุปบันทึกการพัฒนาเกม (Work Summary) - 24 ตุลาคม 2026

เอกสารฉบับนี้จัดทำขึ้นเพื่อรวบรวมฟีเจอร์, ระบบเกม, โครงสร้างโค้ด, และการเปลี่ยนแปลงทั้งหมดที่ถูกเพิ่มเข้าสู่โปรเจกต์ **The Last Empire** ในช่วงวันที่ 14 - 24 ตุลาคม 2026 เพื่อให้ Agent และทีมงานสามารถอ่านทำความเข้าใจและนำไปต่อยอดได้ทันทีโดยไม่ต้องค้น Git log ย้อนหลัง

---

## 📑 สารบัญระบบที่พัฒนาและอัปเดตล่าสุด
1. [ระบบหน้าเมนูหลักและคัดเลือกตัวละคร (Main Menu & Character Select Modal)](#1-ระบบหน้าเมนูหลักและคัดเลือกตัวละคร-main-menu--character-select-modal)
2. [ระบบ 5 Operatives & กลไกเนื้อเรื่องฟื้นความทรงจำ (Amnesia System)](#2-ระบบ-5-operatives--กลไกเนื้อเรื่องฟื้นความทรงจำ-amnesia-system)
3. [ระบบการคำนวณความเสียหายและตัวเลขดาเมจลอย (Combat & Damage System)](#3-ระบบการคำนวณความเสียหายและตัวเลขดาเมจลอย-combat--damage-system)
4. [ระบบอาวุธ กระสุน และเอฟเฟกต์การยิง (Arsenal, Bullet Tracer & Recoil)](#4-ระบบอาวุธ-กระสุน-และเอฟเฟกต์การยิง-arsenal-bullet-tracer--recoil)
5. [ระบบไอเทม อาหาร ยารักษา และเศรษฐกิจร้านค้า (Items, Food, Meds & Shop)](#5-ระบบไอเทม-อาหาร-ยารักษา-และเศรษฐกิจร้านค้า-items-food-meds--shop)
6. [ระบบพฤติกรรมศัตรูและบอส (Enemy AI, Boomer & Boss Attacks)](#6-ระบบพฤติกรรมศัตรูและบอส-enemy-ai-boomer--boss-attacks)
7. [การแก้ปัญหาฟอนต์ บั๊กกล่องสี่เหลี่ยม และไอคอนคลาส (Font Cleanup & Class Icons)](#7-การแก้ปัญหาฟอนต์-บั๊กกล่องสี่เหลี่ยม-และไอคอนคลาส-font-cleanup--class-icons)

---

## 1. ระบบหน้าเมนูหลักและคัดเลือกตัวละคร (Main Menu & Character Select Modal)

### ไฟล์หลัก:
* [MainMenu.unity](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scenes/MainMenu.unity) — ซีนเมนูหลักพร้อมบรรยากาศซากปรักหักพัง
* [MainMenuManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/MainMenuManager.cs) — ควบคุมโฟลว์การกดเริ่มเกม, สลับหน้าต่าง, และโหลดเข้าฉาก Gameplay
* [CharacterSelectUI.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/CharacterSelectUI.cs) — ตัวจัดการ Dossier ข้อมูลตัวละคร, ภาพ Portrait, และแถบปุ่มเลือกตัวละคร
* [CharacterSelectModal.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/UI/CharacterSelectModal.prefab) — หน้าต่าง Modal สไตล์ Dark Glassmorphism กึ่งโปร่งใส

### รายละเอียดการทำงาน:
* **เลย์เอาต์ 2 ฝั่ง (Split Column Layout)**:
  * **ฝั่งซ้าย (Roster Column)**: แสดงแถบปุ่ม 5 ตัวละครพร้อมแถบ Active Indicator แสดงสีประจำตัวละคร (Theme Color) เมื่อถูกเลือก
  * **ฝั่งขวา (Dossier Panel)**: แสดงข้อมูลประจำตัว ได้แก่
    * Header: ชื่อตัวละคร, ฉายา, หมวดหมู่คลาส (Role Category)
    * Background & Operative History: เรื่องย่อปูมหลังก่อนโลกล่มสลาย
    * Awakening Clue: เบาะแสฟื้นความจำเมื่อตื่นขึ้นมากลางซากเมือง
    * Operative Attributes: ค่าพลังชีวิตสูงสุด, ความหิวสูงสุด, ความเร็วเดิน, ความเร็วแดช, คูลดาวน์แดช
    * Shift Ability Box: ชื่อสกิลกดปุ่ม Shift และคำอธิบายความสามารถ
    * Starting Arsenal: อาวุธเริ่มต้น, กระสุนติดตัว, อาหาร และเงินทุนตั้งต้น
* **Class Icon System**: เพิ่ม Game Object `ClassIcon` ที่เชื่อมต่อกับ Sprite Sheet สำหรับแสดงไอคอนสัญลักษณ์ประจำคลาสโดยเฉพาะ

---

## 2. ระบบ 5 Operatives & กลไกเนื้อเรื่องฟื้นความทรงจำ (Amnesia System)

### คลาสทั้ง 5 ในระบบ ([CharacterDatabase.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/CharacterDatabase.asset)):
1. **Vanguard (แนวหน้า / สมดุลรอบด้าน)**:
   * **Active Shift**: `Adrenaline Rush` — เร่งความเร็วเดิน +75%, โจมตีเร็วขึ้น +30%, ลดความเสียหายที่ได้รับ 20% (แลกด้วยความหิวลดไวขึ้น)
   * **Loadout เริ่มต้น**: ปืน Rifle, มีด Combat Knife, กระสุน 60 นัด
2. **Juggernaut (สายชนปะทะ / รถถังมนุษย์)**:
   * **Active Shift**: `Unstoppable Charge` — วิ่งพุ่งชนเปิดฉาก สะท้อนกระสุนปืน สตั๊นศัตรู 1.0 วินาที + 25 ดาเมจกระแทก (CD: 5s)
   * **Passive**: ลดความเสียหายจากการโจมตีทุกชนิด 15% ติดตัวตลอดเวลา
   * **Loadout เริ่มต้น**: ค้อนปอนด์ Sledgehammer, ปืนลูกซอง Shotgun, กระสุน 20 นัด
3. **Phantom (มือสังหารเงา / ลอบเร้น)**:
   * **Active Shift**: `Shadow Step` — ดำดิ่งสู่เงามืด ล่องหนทะลุผ่านศัตรูได้ (แลกด้วยความหิวลดเร็วขึ้น) เมื่อก้าวออกจากสถานะเงา การโจมตีครั้งถัดไปจะติด **Critical Hit 100% แน่นอน (ตัวคูณดาเมจ 2.0x)**
   * **Loadout เริ่มต้น**: ดาบซามูไร Katana, ปืนพก Pistol, กระสุน 40 นัด
4. **Marksman (พลแม่นปืนสายส่องไกล)**:
   * **Active Shift**: `Eagle Eye` — ตั้งท่ายืนเล็ง Tactical Zoom มุมกล้องซูมออก 1.35x, ระยะยิงเพิ่มขึ้น 2 เท่า, กระสุนยิงเจาะทะลุศัตรู (Piercing) แต่ความเร็วเคลื่อนที่ลดลงขณะเล็ง
   * **Passive**: อัตราติดคริติคอลพื้นฐาน 35% (แรง 1.5x, และเพิ่มเป็น 2.0x ขณะอยู่ในท่า Eagle Eye)
   * **Loadout เริ่มต้น**: ปืน Sniper Rifle, ปืนลูกโม่ Revolver, กระสุน 30 นัด
5. **Scavenger (คนจรจัดแดนรกร้าง / กับดักพิษ)**:
   * **Active Shift**: `Toxic Trail` — ปล่อยควันพิษเขียวตามรอยเท้า ศัตรูที่เหยียบโดนจะเดินช้าลง 50% และติดพิษดาเมจ 8 DoT
   * **Passive**: การกินอาหาร Bread จะฟื้นฟูพลังชีวิตพิเศษ +10 HP นอกเหนือจากฟื้นความหิว
   * **Loadout เริ่มต้น**: หน้าไม้ Crossbow, หอก Survival Spear, เงินสดติดตัว 300 Gold

### กลไกการเชื่อมต่อข้ามฉากและ HUD:
* ตัวแปร `CharacterManager.SelectedCharacterId` จัดเก็บตัวละครที่เลือกจากหน้า Main Menu และส่งต่อไปยังฉาก Gameplay อย่างราบรื่น
* **Awakening Memory Banner**: เมื่อเข้าสู่ฉากเกม [PlayerHUD.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerHUD.cs) จะแสดงแถบแบนเนอร์ความทรงจำลอยขึ้นมาเป็นเวลา 6 วินาที พร้อมเสียงพึมพำของตัวละครนั้นๆ
* **Shift Ability Indicator**: แถบ HUD แสดงไอคอนสกิลกด Shift สถานะพร้อมใช้งาน และแถบ Cooldown แบบ Real-time

---

## 3. ระบบการคำนวณความเสียหายและตัวเลขดาเมจลอย (Combat & Damage System)

### ไฟล์หลัก:
* [DamageNumberManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/DamageNumberManager.cs) — ตัวจัดการสร้างและพูลตัวเลขดาเมจ
* [FloatingDamageNumber.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/FloatingDamageNumber.cs) — พฤติกรรมแอนิเมชันป๊อปอัป ขยายขนาด ลอยขึ้นชะลอความเร็ว และจางหาย

### สูตรการคำนวณดาเมจใน [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs):
1. **ระยะยิงตกหล่น (Distance Falloff)**:
   * ระยะเผาขน ($\le 2.0\text{ m}$): ดาเมจเต็ม 100%
   * ลดหลั่นตามระยะทางตั้งแต่ $2.0\text{ m}$ ถึง $14.0\text{ m}$ (หรือ $22.0\text{ m}$ สำหรับ Sniper Rifle / Marksman)
   * **Damage Floor**: มีเพดานดาเมจขั้นต่ำถูกล็อกไว้ที่ **40% (0.40x)** เสมอ ไม่ว่ากระสุนจะยิงไกลแค่ไหน
2. **ความแปรปรวนสุ่ม (Random Variance)**:
   * ทุกการโจมตีจะสุ่มดาเมจบวก/ลบ $Random.Range(-10\text{f}, +10\text{f})$ โดยการันตีด่าเมจขั้นต่ำไม่ต่ำกว่า 1
3. **ระบบคริติคอลและการแสดงผล (Floating Text Palette)**:
   * **ดาเมจปกติ (Normal Hit)**: ตัวหนังสือสีเหลืองทองประกาย (`#FFF073`) ขนาดมาตรฐาน
   * **คริติคอล / Headshot**: ตัวหนังสือสีส้มแดงเพลิงขนาดใหญ่ 35pt (`#FF471A`) ขึ้นนำหน้าด้วยคำว่า `"HEADSHOT! "`
   * **การบล็อก / กระสุนสะท้อน (Deflection)**: ตัวหนังสือสีฟ้าเงิน (`#A6D8FF`) ขึ้นคำว่า `"BLOCKED"` เมื่อยิงโดนโล่เกราะด้านหน้าของศัตรูประเภท Tanker

---

## 4. ระบบอาวุธ กระสุน และเอฟเฟกต์การยิง (Arsenal, Bullet Tracer & Recoil)

* **Bullet Tracer Trail**: กระสุนปืนทุกนัดติดตั้งคอมโพเนนต์ `TrailRenderer` สร้างเส้นลำแสงเลเซอร์พุ่งตามวิถีกระสุน ช่วยให้อ่านทิศทางการยิงในความมืดได้ชัดเจน
* **Screen Shake Recoil**: เมื่อลั่นไกปืน กล้องหลักจะเกิดแรงสั่นสะบัด (Impulse Shake) ตามขนาดลำกล้องของปืนนั้นๆ (ปืนลูกซอง, สไนเปอร์, ปืนจรวดจะมีแรงถีบสูงกว่าปืนพก)
* **Silent Stealth Crossbow**: 
  * แยกพูลกระสุนหน้าไม้ `CrossbowBoltPool` โดยเฉพาะใน [ObjectPoolManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/System/ObjectPoolManager.cs)
  * หน้าไม้จะไม่ส่งสัญญาณเสียง `AlertEnemiesToGunshot()` ทำให้ยิงสังหารศัตรูได้โดยไม่ทำให้ฝูงซอมบี้ในห้องตื่นตัว
* **Ammo Aliases (ระบบชื่อกระสุนสากล)**:
  * `Light Ammo` $\leftrightarrow$ `Pistol Ammo`
  * `Medium Ammo` $\leftrightarrow$ `Rifle Ammo`
  * `Heavy Ammo` $\leftrightarrow$ `Fuel Ammo`
  * `Shotgun Ammo` $\leftrightarrow$ `Ammo`

---

## 5. ระบบไอเทม อาหาร ยารักษา และเศรษฐกิจร้านค้า (Items, Food, Meds & Shop)

### ไอเทมใหม่ที่ลงทะเบียนสมบูรณ์:
1. **หมวดอาหาร (Food Items)**:
   * `Bread`: ฟื้นฟูความหิว 30
   * `Canned Meat`: ฟื้นฟูความหิว 50, ฟื้น HP 10
   * `Energy Drink`: ฟื้นฟูความหิว 20, บูสต์ความเร็วชั่วขณะ
   * `Apple`: ฟื้นฟูความหิว 15, ฟื้น HP 5
2. **หมวดยาและการรักษา (Medical Items)**:
   * `Small Potion`: ฟื้น HP 30
   * `Large Potion`: ฟื้น HP 70
   * `Bandage`: ฟื้น HP 20, หยุดอาการเลือดไหล (Stop Bleed)
   * `Adrenaline Shot`: ฟื้น HP 40, เพิ่มความเร็วเคลื่อนที่ฉุกเฉิน
3. **ระบบร้านค้า NPC**:
   * ราคากลางสินค้าทั้งหมดถูกผูกเข้าสู่ [ShopDatabase.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ShopDatabase.asset)
   * NPC Merchant ในฉากดึงไอเทมอาวุธ กระสุน อาหาร และยาชุดใหม่ไปวางจำหน่ายครบถ้วน
   * มีเอกสาร Master Catalog: [item-list.md](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Docs/item-list.md) สำหรับอ้างอิง ID, หมวดหมู่, สเตตัส และราคา

---

## 6. ระบบพฤติกรรมศัตรูและบอส (Enemy AI, Boomer & Boss Attacks)

* **Zombie Boomer 1-Second Fuse**:
  * เมื่อเข้าใกล้ผู้เล่นต่ำกว่า 2 เมตร หรือเมื่อพลังชีวิตหมดลง จะหยุดเดินและเข้าสู่ช่วงหน่วงเวลา 1.0 วินาที
  * แสดงวงแหวนเตือนอันตรายสีแดงบนพื้น (Danger Radius Circle Telegraph) ก่อนระเบิดสร้างดาเมจ AOE วงกว้าง
* **Boss AI (Charge Rush & Ground Stomp)**:
  * **Charge Rush (พุ่งชนกวาดทาง)**: ที่ระยะ 5.0m - 12.5m แสดงทางวิ่งสีแดงล่วงหน้า 0.65 วินาที ก่อนพุ่งด้วยความเร็ว 22 - 30 m/s
  * **Ground Stomp AOE (กระทืบพื้นสะเทือน)**: รัศมีวงกว้าง 4.5 เมตร พร้อมวงแหวนเตือนสีแดงล่วงหน้า 1.0 วินาที สร้างดาเมจแรงกระแทก ผลักกระเด็นรอบทิศทาง
  * **Stagger Immunity**: บอสมีสถานะป้องกันการชะงักระหว่างออกท่าไม้ตายทั้งสองท่า

---

## 7. การแก้ปัญหาฟอนต์ บั๊กกล่องสี่เหลี่ยม และไอคอนคลาส (Font Cleanup & Class Icons)

* **การถอด Unicode พิเศษและ Emoji**:
  * TextMeshPro ไม่รองรับอักขระ Emoji หรือ Glyph ประดับ เช่น `◈`, `❖`, `✦`, `➔`, `💬`, `🔥` ส่งผลให้ตัวอักษรกลายเป็นกล่องสี่เหลี่ยมบั๊ก `□`
  * ได้ทำการกวาดล้างและแทนที่ด้วยข้อความ ASCII มาตรฐานทั่วทั้งโปรเจกต์ ทั้งใน Prefabs, Scenes ([MainMenu.unity](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scenes/MainMenu.unity)), และสคริปต์ C# ทั้งหมด
* **Class Icon Integration**:
  * นำเข้าและตัดแบ่ง Sprite Sheet `ChatGPT Image Oct 24, 2026, 12_56_17 AM.png` ใน `Assets/Resources/Image/UI/`
  * ติดตั้ง Game Object `ClassIcon` ลงใน [CharacterSelectModal.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/UI/CharacterSelectModal.prefab) และเชื่อมต่อ Reference แสดงสัญลักษณ์ของแต่ละคลาสได้อย่างถูกต้อง

---

## 8. ระบบสร้างด่านด้วย JSON และเครื่องมือออกแบบในฉาก (Stage Template Designer & Hybrid Generation)

### ไฟล์หลัก:
* [StageTemplateData.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageTemplateData.cs) — โครงสร้างข้อมูล Serializable สำหรับจัดเก็บสิ่งกีดขวาง (Obstacles), พร็อพตกแต่ง (Decorations), จุดสปอว์นมอนสเตอร์ (Enemy Spawns), กล่องไอเทม (Loot Spawns), และจุดตั้ง NPC
* [StageTemplateManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageTemplateManager.cs) — ตัวโหลดไฟล์ JSON จากโฟลเดอร์ `Resources/Stages/` แยกตาม BiomeType และคำนวณการสุ่มผสม (Hybrid Chance)
* [StageObjectMarker.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageObjectMarker.cs) — คอมโพเนนต์ช่วยจำค่า Prefab ดั้งเดิมและสเปกไอเทมใน Editor
* [StageTemplateDesignerWindow.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/StageTemplateDesignerWindow.cs) — เครื่องมือ Editor Window (`Tools -> The Last Empire -> Stage Template Designer`) สำหรับจัดวางวัตถุใน Scene และ Export ออกเป็น JSON
* [EnvironmentManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/EnvironmentManager.cs) & [GameManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/GameManager.cs) — รองรับการอ่านข้อมูลจากแม่แบบ JSON เพื่อสร้างสิ่งกีดขวาง มอนสเตอร์ และไอเทมดรอปลงในห้อง

### รายละเอียดการทำงาน:
* **Workflow การออกแบบ**:
  1. เปิดหน้าต่าง `Tools -> The Last Empire -> Stage Template Designer`
  2. กดปุ่ม `[ Spawn Design Workspace in Scene ]` จะสร้างกรอบห้อง $32 \times 18$ และมาร์กเกอร์ประตูทางออก 4 ทิศทาง (เหนือ, ใต้, ออก, ตก) พร้อมวงกลมเตือน Safety Zone $1.8\text{ m}$
  3. คลิกปุ่มจาก Palette เพื่อวางสิ่งกีดขวาง (เช่น Concrete Barricade, Sandbags, รถพัง), มอนสเตอร์ (Zombie Runner, Boomer ฯลฯ), หรือกล่องเงิน/อาหาร
  4. ใช้เครื่องมือ Move/Rotate ใน Scene View จัดแต่งตามใจชอบ
  5. กดปุ่ม `[ Save & Export Template to JSON ]` ระบบจะแปลงข้อมูลเป็น JSON จัดเก็บที่ `Assets/Resources/Stages/{Biome}/{TemplateId}.json` ทันที
  6. สามารถกดโหลดแม่แบบเก่า (`[ Load Template into Scene ]`) เพื่อเปิดขึ้นมาแก้ไขตำแหน่งและเซฟทับได้ตลอดเวลา
* **กลไก Hybrid Generation ในเกม**:
  * เมื่อผู้เล่นเดินเข้าสู่ด่านใหม่ [StageTemplateManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/StageTemplateManager.cs) จะตรวจสอบความน่าจะเป็น `templateSelectionChance` (ค่าเริ่มต้น 50% หรือปรับแต่งได้):
    * **ถ้าถูกสุ่มให้ใช้ Template:** จะดึงแม่แบบ JSON ที่ตรงกับ Biome ของห้องนั้นมาสร้างสิ่งกีดขวางและสปอว์นศัตรู/ของตามไฟล์ 100%
    * **ถ้าถูกสุ่มเป็น Procedural หรือไม่มี Template ของ Biome นั้น:** จะตกกลับไปสร้างแบบ Procedural Noise ดั้งเดิม ทำให้เกมยังคงมีความสดใหม่ ไม่ตายตัว

---

## 9. ระบบจัดการการตั้งค่าเกม (SettingManager & Settings Modal)

### ไฟล์หลัก:
* [SettingManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/SettingManager.cs) — ตัวจัดการค่า Volume (Master, BGM, SFX) และ Display (Fullscreen), บันทึกและโหลดผ่าน `PlayerPrefs`, รองรับ Auto-Wiring หา UI Component ในฉากอัตโนมัติ

### รายละเอียดการทำงาน:
* **Audio Sliders & Percentage Texts**:
  * Master Audio Volume (0-100%, ค่าเริ่มต้น 100%)
  * BGM Music Volume (0-100%, ค่าเริ่มต้น 85%)
  * SFX Sound Volume (0-100%, ค่าเริ่มต้น 100%)
  * เมื่อเลื่อน Slider จะปรับลดเสียงแบบ Real-time และอัปเดตตัวเลขเปอร์เซ็นต์ (`ValueText`) ทันที
* **Fullscreen Mode Toggle**:
  * สลับโหมดเต็มจอ / หน้าต่าง ผ่าน `Screen.fullScreen` และบันทึกลง PlayerPrefs
* **Action Buttons**:
  * **DEFAULT Button**: รีเซ็ตค่าการตั้งค่าทั้งหมดกลับสู่ค่าเริ่มต้นมาตรฐาน (Master 100%, BGM 85%, SFX 100%, Fullscreen เปิด) พร้อมอัปเดตหน้า UI ทันที
  * **SAVE & BACK Button**: สั่งบันทึกลงดิสก์ผ่าน `PlayerPrefs.Save()` และปิดหน้าต่าง Modal
* **Auto-Wiring & Singleton**:
  * หากนำสคริปต์ไปแปะไว้ที่ GameObject `SettingsModal` ใน Scene ระบบจะสแกนหา Slider, Text, และปุ่มต่างๆ ให้เองอัตโนมัติ ไม่จำเป็นต้องลากใส่ Inspector ทีละชิ้น
  * มี Event Actions: `OnMasterVolumeChanged`, `OnBgmVolumeChanged`, `OnSfxVolumeChanged`, `OnFullscreenChanged` ให้ระบบเสียงในอนาคต subscribe เพื่อปรับระดับเสียงตามหมวดหมู่ได้ทันที

---

## 10. ระบบภาพควันและหมอกบรรยากาศในหน้าเมนู (Smoke VFX & Fog Drifter)

### ไฟล์และ Asset หลัก:
* [Smoke_Puff_VFX.png](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Image/UI/Smoke_Puff_VFX.png) — สไปรต์กลุ่มควันทรงกลมแบบ Volumetric โทนม่วงเทา พร้อมตัดขอบโปร่งใสด้วยการแปลง Grayscale to Alpha
* [Smoke_Horizontal_Fog.png](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Image/UI/Smoke_Horizontal_Fog.png) — สไปรต์แนวนอนพาโนรามาสำหรับชั้นหมอกควันเรี่ยพื้นซากเมือง
* [UIFogDrifter.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/UIFogDrifter.cs) — สคริปต์ควบคุมการพริ้วไหว ลอยส่ายไปมาอย่างช้าๆ (Drift & Sway) และการเต้นกระพริบความโปร่งแสงแบบจางๆ (Alpha Breathing Pulse) เหมาะสำหรับ GameObject `SmokeVFX` ใน MainMenuCanvas


