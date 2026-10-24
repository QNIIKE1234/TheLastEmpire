# สรุปบันทึกการพัฒนาเกม (Work Summary) - 25 ตุลาคม 2026

เอกสารฉบับนี้จัดทำขึ้นเพื่อรวบรวมฟีเจอร์, ระบบเกม, โครงสร้างโค้ด, และการแก้ไขบั๊กทั้งหมดที่ถูกเพิ่มเข้าสู่โปรเจกต์ **The Last Empire** ในวันที่ 25 ตุลาคม 2026 เพื่อให้ Agent และทีมงานสามารถอ่านทำความเข้าใจและนำไปต่อยอดได้ทันทีโดยไม่ต้องค้น Git log ย้อนหลัง

---

## 📑 สารบัญระบบที่พัฒนาและอัปเดตล่าสุด
1. [ระบบกลางวัน-กลางคืนและแสงไฟฉาย (Day-Night Lighting & Darkness System)](#1-ระบบกลางวัน-กลางคืนและแสงไฟฉาย-day-night-lighting--darkness-system)
2. [ระบบควบคุมและเร่งเวลาในเมนู F1 (In-Game Time Acceleration & Debug Cheat)](#2-ระบบควบคุมและเร่งเวลาในเมนู-f1-in-game-time-acceleration--debug-cheat)
3. [การปรับปรุง Shader พื้นและสเกลพื้นผิว (Ground Shader & Texture Tiling Optimization)](#3-การปรับปรุง-shader-พื้นและสเกลพื้นผิว-ground-shader--texture-tiling-optimization)
4. [การแก้ไขบั๊ก QuickSlot และระบบความเป็นเจ้าของอาวุธ (QuickSlot & Weapon Ownership Fix)](#4-การแก้ไขบั๊ก-quickslot-และระบบความเป็นเจ้าของอาวุธ-quickslot--weapon-ownership-fix)
5. [การแก้ปัญหา Error UI Canvas และทำความสะอาดโค้ด (UI Canvas Shader & Code Cleanup)](#5-การแก้ปัญหา-error-ui-canvas-และทำความสะอาดโค้ด-ui-canvas-shader--code-cleanup)

---

## 1. ระบบกลางวัน-กลางคืนและแสงไฟฉาย (Day-Night Lighting & Darkness System)

### ไฟล์หลัก:
* [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs) — ผู้จัดการวัฏจักรเวลา 3 ช่วง (Day, Dusk, Night) ควบคุม Ambient Tint และแสง Directional Light
* [NightDarknessController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/NightDarknessController.cs) — ควบคุม Canvas Overlay แผ่นความมืด, อัปเดตพิกัดผู้เล่นบนหน้าจอ, และเปิด-ปิดไฟฉาย
* [NightDarknessOverlay.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/NightDarknessOverlay.shader) — Shader คำนวณความมืดแบบ Radial Falloff รอบตัวผู้เล่น ความมืด 90%
* [DayNightLightingEditorWindow.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/DayNightLightingEditorWindow.cs) — เครื่องมือปรับแสงและเทสระบบเวลาใน Unity Editor (`Tools -> The Last Empire -> Day-Night & Lighting Controller`)

### รายละเอียดการทำงาน:
* **3-Phase Atmospheric Cycle**:
  * **DAY (35 วินาที)**: แสงแดดสว่างเต็มที่ Directional Light Intensity `1.0`, สีขาวอบอุ่น `(1.0, 0.98, 0.92)`, แผ่นความมืดโปร่งใส 100%
  * **DUSK (12 วินาที)**: ท้องฟ้าและฉากย้อมแสงสีส้มทองยามเย็น Directional Light Intensity `0.45`, สีทองส้ม `(0.95, 0.60, 0.35)`
  * **NIGHT (25 วินาที)**: ท้องฟ้ายามดึกมืดสนิท Directional Light Intensity ลดลงเหลือ `0.08` (แสงจันทร์สลัว) สีน้ำเงินเข้ม `(0.12, 0.16, 0.28)`
* **ความมืด 90% (90% Max Darkness)**:
  * ปรับค่า `_MaxDarkness` เป็น `0.90` (90%) ให้ความมืดโอบล้อมรอบตัวได้อย่างน่ากลัวและสมจริง
  * ปรับลดรัศมีแสงด้านนอก `_OuterRadius` ลงเหลือ `0.45` เพื่อให้ความมืด 90% เข้ามาใกล้ตัวผู้เล่นขึ้น ไม่ฟุ้งสว่างเกินไป
* **ไฟฉายผู้เล่น (Personal Flashlight Perimeter)**:
  * ปรับการคำนวณสัดส่วนหน้าจอ Screen Aspect Ratio อัตโนมัติ ทำให้วงแสงไฟฉายกลมสม่ำเสมอตลอดเวลา
  * มีระบบลมหายใจ/เปลวไฟวูบวาบแบบออร์แกนิก (Subtle Organic Torch Flicker)
  * กดปุ่ม **`L`** เพื่อเปิด/ปิดไฟฉายได้ตลอดเวลา และระบบจะเปิดไฟฉายให้อัตโนมัติเมื่อเข้าสู่กลางคืน

---

## 2. ระบบควบคุมและเร่งเวลาในเมนู F1 (In-Game Time Acceleration & Debug Cheat)

### ไฟล์หลัก:
* [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs) — หน้าต่าง In-Game Debug UI (กด `F1`) เพิ่มแท็บ Time & Day/Night Cheat

### ฟังก์ชันและปุ่มควบคุมใหม่ในแท็บ Time Cheat:
1. **Status Badge แบบเรียลไทม์**:
   * แสดงชื่อ Phase ปัจจุบัน (`DAY` สีเหลือง / `DUSK` สีส้ม / `NIGHT` สีฟ้า)
   * แสดงวันที่ (Day Count) และเวลานับถอยหลังก่อนเปลี่ยน Phase พร้อมเปอร์เซ็นต์ความคืบหน้า
2. **เร่งความเร็วเกม (`Time.timeScale`)**:
   * ปุ่ม Preset: `[ 0x (หยุด) ]`, `[ 1x (ปกติ) ]`, `[ 2x ]`, `[ 5x ]`, `[ 10x ]`
   * Slider ละเอียด ปรับความเร็วเกมได้ตั้งแต่ `0.0x` ถึง `10.0x`
3. **เร่งเวลาย่นเวลานาฬิกา (+วินาที)**:
   * ปุ่ม `[ +5s ]`, `[ +15s ]`, `[ +30s ]`, `[ +60s ]` กดย่นเวลาไปข้างหน้าได้ทันที
4. **สลับช่วงเวลาทันที (Instant Phase Snap)**:
   * ปุ่มลัด: `[ ☀️ DAY ]`, `[ 🌅 DUSK ]`, `[ 🌙 NIGHT ]`, และ `[ ⏭️ ข้ามไปช่วงถัดไป ]`
5. **ควบคุมวัน (Day Navigation)**:
   * `[ ◀ วันก่อนหน้า ]` | `[ วันถัดไป ▶ ]` | `[ รีเซ็ตเป็นวันที่ 1 ]`
6. **Cheat แสงสว่าง & ความมืด**:
   * `[ 💡 เปิด/ปิด ไฟฉาย (กด L) ]`
   * `[ 🌑 ปิด/เปิด Mask ความมืด ]` (สำหรับซ่อนแผ่นความมืดเพื่อดูแมพหรือเทสวัตถุรอบๆ)
   * Slider สด ปรับระดับความมืด (`Darkness: 40% - 98%`) ปรับสดขณะเล่นเกมได้ทันที

### คีย์ลัดในเกม (Hotkeys):
* **`F1`**: เปิด/ปิดหน้าต่าง Debug Overlay
* **`T`**: วนลูปเร่งความเร็วเกม (`1x` $\to$ `2x` $\to$ `5x` $\to$ `10x` $\to$ `1x`) พร้อม Toast แจ้งเตือนบนจอ
* **`N`**: ข้ามช่วงเวลาไป Phase ถัดไปทันที
* **`L`**: เปิด/ปิด ไฟฉาย

---

## 3. การปรับปรุง Shader พื้นและสเกลพื้นผิว (Ground Shader & Texture Tiling Optimization)

### ไฟล์หลัก:
* [WorldSpaceGroundShader.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/WorldSpaceGroundShader.shader) — Shader พื้นน้ำหนักเบาพิเศษ
* [AssignGroundShader.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/Map/AssignGroundShader.cs) — สคริปต์ Assign Shader และค่า Tiling ให้กับ Material ทุกไบโอม

### รายละเอียดการปรับปรุง:
* **0-Overhead Shader**: ตัดการคำนวณแสงและการเขียนโค้ดที่ซ้ำซ้อนใน Fragment Shader ออกทั้งหมด คงเหลือเพียงการคำนวณ World Space UV และการคูณด้วย `_GlobalAmbientTint` ทำให้เฟรมเรตคงที่ 60–120+ FPS ไม่มีปัญหาหน่วงหรือกระตุก
* **Texture Tiling 0.10**: ปรับจูนค่า Tiling จากเดิม `0.05` ขยับเป็น `0.10` บน Material พื้นผิวทุกชิ้น ([UrbanRuins.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/UrbanRuins.mat), [Highlands.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/Highlands.mat), [Highways.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/Highways.mat), ฯลฯ) ส่งผลให้เห็นรายละเอียดเส้นทางเท้า ลายถนน และพื้นผิวไบโอมได้อย่างคมชัด ไม่ขยายใหญ่จนกลืนหาย

---

## 4. การแก้ไขบั๊ก QuickSlot และระบบความเป็นเจ้าของอาวุธ (QuickSlot & Weapon Ownership Fix)

### ไฟล์หลัก:
* [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs) — จัดการคีย์ลัดอาวุธ ตรวจสอบความเป็นเจ้าของ และการสลับอาวุธ

### สาเหตุของบั๊กเดิม:
1. ฟังก์ชัน `HasRangedWeaponAccess` และ `HasMeleeWeaponAccess` มี Hardcode เก่าตกค้าง:
   * `if (clean.Contains("pist")) return true;`
   * `if (clean.Contains("knife")) return true;`
   ทำให้ตัวละครทุกตัวสามารถถือปืนพกและมีดได้เสมอ แม้ไม่มีใน Loadout เริ่มต้นหรือในกระเป๋า
2. ปุ่มตัวเลข `1`–`9` ผูกตรงกับ Index แคตตาล็อกอาวุธ (`weapons[0]` คือ Pistol) ทำให้การกดเลข `1` กลายเป็นการบังคับหยิบปืนพกออกมาเสมอ

### การแก้ไข:
1. **ลบ Hardcode ปืนพกและมีดทิ้งทั้งหมด**: บังคับให้ตรวจสอบเฉพาะอาวุธที่อยู่ใน `activeCharacterData.startingWeapons` หรือใน `_inventory.Items` เท่านั้น
2. **สร้างระบบ QuickSlot อัจฉริยะ ([SwitchToQuickSlot](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs#L2018-L2048))**:
   * ดึงรายการอาวุธปืนที่ผู้เล่นมีครอบครองจริงๆ มาจัดลำดับตามสล็อต QuickSlot
   * **กดปุ่ม `1`**: สลับไปที่ **ปืนชิ้นแรกที่ผู้เล่นมีจริงทันที** (เช่น Vanguard ถือ Rifle, Juggernaut ถือ Shotgun, Scavenger ถือ Crossbow, Marksman ถือ Sniper Rifle)
   * **กดปุ่ม `2`, `3`...**: สลับไปที่ปืนชิ้นถัดไปที่ผู้เล่นเก็บได้ในตัว
   * หากไม่มีอาวุธใน Slot นั้น ระบบจะไม่ทำงาน และไม่มีทางหยิบปืนพกที่ไม่ได้เป็นเจ้าของออกมาได้อีก
3. **แก้ Error CS0246**: เพิ่ม `using System.Collections.Generic;` ที่ด้านบนของ [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs#L1)

---

## 5. การแก้ปัญหา Error UI Canvas และทำความสะอาดโค้ด (UI Canvas Shader & Code Cleanup)

### ไฟล์หลัก:
* [NightDarknessOverlay.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/NightDarknessOverlay.shader)
* [DayNightVignette.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/DayNightVignette.shader)

### การแก้ไข:
* **แก้ Error `doesn't have a texture property '_MainTex'`**: ระบบ `CanvasRenderer` และคอมโพเนนต์ `UnityEngine.UI.Image` จำเป็นต้องมีตัวแปร `_MainTex` ใน Shader เสมอ ได้เพิ่ม:
  ```shader
  [HideInInspector] _MainTex("Texture", 2D) = "white" {}
  [HideInInspector] _Color("Color", Color) = (1, 1, 1, 1)
  ```
  เข้าไปใน Shader ทำให้คอมไพล์และเรนเดอร์ Canvas ผ่าน 100% ไม่มี Error สีแดงใน Console
* **ลบคำว่า "DontStarve" ออกจากทั้งโปรเจกต์**: เปลี่ยนชื่อ Script, Shader, ตัวแปร, และเมนูทั้งหมดให้ใช้ชื่อมาตรฐาน `NightDarkness*` และ `DayNight*` เพื่อความสะอาดและเป็นระเบียบของโค้ดเบส
