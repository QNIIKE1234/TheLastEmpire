# สรุปบันทึกการพัฒนาเกม (Work Summary) - 31 ตุลาคม 2026

เอกสารฉบับนี้จัดทำขึ้นเพื่อรวบรวมฟีเจอร์, ระบบเกม, โครงสร้างโค้ด, และการแก้ไขปัญหาภาพและแสงทั้งหมดที่ถูกพัฒนาในโปรเจกต์ **The Last Empire** ในวันที่ 31 ตุลาคม 2026 เพื่อให้ทีมงานและ AI สามารถตรวจสอบและนำไปต่อยอดได้ทันที

---

## 📑 สารบัญระบบที่พัฒนาและอัปเดตล่าสุด
1. [ระบบแสงภาพยนตร์ Golden Hour & ลำแสงทะลุหมอก (Volumetric God Rays / Sun Shafts)](#1-ระบบแสงภาพยนตร์-golden-hour--ลำแสงทะลุหมอก-volumetric-god-rays--sun-shafts)
2. [การแก้ไขปัญหาจอฟ้าและปรับแต่งเฉดสีท้องฟ้า (Sky Cyan Washout Fix & Color Calibration)](#2-การแก้ไขปัญหาจอฟ้าและปรับแต่งเฉดสีท้องฟ้า-sky-cyan-washout-fix--color-calibration)
3. [การเพิ่ม Depth Pass ให้ Shader พื้นและผูก Material แต่ละ Biome (Ground Depth & Biome Materials)](#3-การเพิ่ม-depth-pass-ให้-shader-พื้นและผูก-material-แต่ละ-biome-ground-depth--biome-materials)
4. [หน้าต่างเครื่องมือควบคุมบรรยากาศและปุ่มโกงเวลา F1 (Atmospheric Editor Window & In-Game Cheats)](#4-หน้าต่างเครื่องมือควบคุมบรรยากาศและปุ่มโกงเวลา-f1-atmospheric-editor-window--in-game-cheats)
5. [แผนพัฒนาระบบ QuickSlot Drag & Drop (QuickSlot Drag & Drop Architecture Plan)](#5-แผนพัฒนาระบบ-quickslot-drag--drop-quickslot-drag--drop-architecture-plan)

---

## 1. ระบบแสงภาพยนตร์ Golden Hour & ลำแสงทะลุหมอก (Volumetric God Rays / Sun Shafts)

### ไฟล์หลัก:
* [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs) — ผู้จัดการแสงบรรยากาศ URP ควบคุมองศาแสงอาทิตย์ (Golden Hour), ระบบหมอก (ExponentialSquared Fog), และการกระจายตัวของ God Ray Quads
* [VolumetricGodRayBeam.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/VolumetricGodRayBeam.shader) — Shader คำนวณลำแสงทะลุหมอก (Additive Blending, Lateral Soft Edge, Lengthwise Falloff, Procedural Dust Noise, และ Soft Depth Fade)
* [VolumetricGodRayBeam.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/VolumetricGodRayBeam.mat) — Material สำหรับลำแสง
* [DefaultVolumeProfile.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Settings/DefaultVolumeProfile.asset) — ปรับแต่ง ACES Tonemapping, Bloom Threshold 0.95, Color Adjustments
* [PC_Renderer.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Settings/PC_Renderer.asset) — SSAO Intensity 0.65, Radius 0.4

### รายละเอียดการทำงาน:
* **Golden Hour Sunlight**:
  * มุมแสง Pitch 26 องศา, Yaw -135 องศา ส่องเฉียงยาวทอดเงาพาดผ่านแนวต้นไม้และพื้นฉาก
  * แสง Directional Light โทนสีทองอบอุ่น `RGB(1.0, 0.88, 0.72)` Intensity `1.45` พร้อมเงา Soft Shadows คุณภาพสูง
* **Volumetric Sunbeams**:
  * สร้าง Mesh Quad จำนวน 6 ลำ กว้าง 12 เมตร ยาว 45 เมตร กระจายตัวรอบกล้องตามทิศทางแสงแดด
  * มีระบบเคลื่อนที่ติดตามกล้อง/ผู้เล่นอย่างนุ่มนวลแบบ Zero Allocation
  * คำนวณ Soft Depth Fade ไม่ให้เกิดรอยตัดขอบแข็งเมื่อลำแสงกระทบกับพื้นผิวหรือสิ่งกีดขวาง

---

## 2. การแก้ไขปัญหาจอฟ้าและปรับแต่งเฉดสีท้องฟ้า (Sky Cyan Washout Fix & Color Calibration)

### สาเหตุของปัญหา (Root Cause):
* เดิม [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs) กำหนดค่า `dayCameraColor = new Color(0.15f, 0.3f, 0.45f, 1f)` ซึ่งเป็นสีฟ้าอมเขียว (Teal/Cyan)
* เมื่อเปิดใช้งาน URP Post-Processing พร้อม ACES Tonemapping และ Bloom สีเคลียร์ของกล้องจึงถูกดันแสงจนสว่างกลายเป็นสีฟ้าสดใสจนกลบฉาก ("ฟ้าเชียว")

### แนวทางแก้ไข:
* ปรับเปลี่ยน `dayCameraColor` ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs) ให้เป็นโทนสีกลางนวลสบายตา `new Color(0.18f, 0.18f, 0.20f, 1f)`
* ปรับจูน `fogDensity` เริ่มต้นใน [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs) ลงจาก `0.016f` เหลือ `0.006f` เพื่อให้ได้ไอหมอกบางเบาตามธรรมชาติ ไม่หนาทึบจนบดบังทัศนียภาพ
* ปรับ Bloom Threshold ใน [DefaultVolumeProfile.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Settings/DefaultVolumeProfile.asset) จาก `0.82` ขึ้นเป็น `0.95` และปรับ Intensity ลงจาก `1.6` เป็น `1.1` เพื่อป้องกันแสงฟุ้งเกินขอบเขต

---

## 3. การเพิ่ม Depth Pass ให้ Shader พื้นและผูก Material แต่ละ Biome (Ground Depth & Biome Materials)

### การปรับปรุง:
* **DepthOnly Pass ใน [WorldSpaceGroundShader.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/WorldSpaceGroundShader.shader)**:
  * เพิ่ม Pass `DepthOnly` (ZWrite On, ColorMask R) เพื่อให้พื้นดินบันทึกค่าความลึกลงใน `_CameraDepthTexture` ของ URP
  * ทำให้ระบบ God Rays Soft Depth Intersection และ Screen Space Ambient Occlusion (SSAO) ตรวจจับพื้นได้อย่างแม่นยำ
* **ผูก Material ตาม Biome ใน [Gameplay_3D.unity](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scenes/Gameplay_3D.unity)**:
  * แก้ไขการอ้างอิง Material ในคอมโพเนนต์ `GroundBiomeMaterialController` ซึ่งเดิมทุกช่องชี้ไปยัง `DarkGray.mat` ทั้งหมด
  * เชื่อมโยง GUID ของ Material แต่ละ Biome อย่างถูกต้อง:
    * UrbanRuins -> [UrbanRuins.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/UrbanRuins.mat)
    * Highways -> [Highways.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/Highways.mat)
    * OvergrownForests -> [OvergrownForests.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/OvergrownForests.mat)
    * Highlands -> [Highlands.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/Highlands.mat)
    * Waterways -> [Waterways.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/Waterways.mat)
    * SuburbanVillages -> [SuburbanVillages.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/SuburbanVillages.mat)
    * SpecialEvent -> [SpecialEvent.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/SpecialEvent.mat)
    * Default -> [Default.mat](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Materials/Default.mat)

---

## 4. หน้าต่างเครื่องมือควบคุมบรรยากาศและปุ่มโกงเวลา F1 (Atmospheric Editor Window & In-Game Cheats)

### เครื่องมือ:
* **Unity Editor Window**:
  * [AtmosphericLightingEditorWindow.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/AtmosphericLightingEditorWindow.cs) เปิดผ่านเมนู `Tools -> The Last Empire -> Atmospheric Lighting & God Rays` เพื่อพรีวิวและปรับจูนแสง/หมอก/ลำแสงแบบ Real-time ใน Scene View
* **In-Game Debug Menu (F1 Cheat Window)**:
  * เพิ่มปุ่มควบคุม Fog เปิด/ปิด (`หมอกบรรยากาศ (Fog): เปิด [ON] / ปิด [OFF]`)
  * เพิ่มปุ่ม `รีเซ็ตแสง Golden Hour` ใน [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs) ภายใต้แท็บ `TIME & DAY/NIGHT CHEAT`

---

## 5. แผนพัฒนาระบบ QuickSlot Drag & Drop (QuickSlot Drag & Drop Architecture Plan)

* ออกแบบโครงสร้างรองรับการลากไอเทมจาก Inventory ฝั่งซ้ายมาวางใน QuickSlot 1 - 5 ฝั่งขวา บันทึกไว้ใน [quickslot_drag_drop_plan.md](file:///C:/Users/Acer/.gemini/antigravity-ide/brain/ffdcd308-350d-4497-99fb-4a8fea84f0d7/quickslot_drag_drop_plan.md)
* โครงสร้างเตรียมพร้อมรองรับทั้งอาวุธ (ติดตั้งทันทีเมื่อกด 1-5) และไอเทมบริโภค (กดใช้ตามคีย์ลัด)

---

## 6. การยกเครื่องระบบแสง URP และการหมุนองศาแสงตามช่วงเวลา (URP Lighting Overhaul & Dynamic Celestial Path)

### ปัญหาเดิมที่พบจากภาพหน้าจอของผู้ใช้:
* ในช่วง Dusk (พลบค่ำ) มี Shader Quad แบบ 2D คลุมสีส้มทั่วจอ (`NightDarknessOverlay.shader`) ซ้อนทับกับ Ground Tint สีส้มจัด และหมอกสีส้ม
* ทำให้เกิดอาการ **"ส้มทั้งจอแบนๆ"** บดบังลายตารางพื้น Texture และทำให้โมเดลตัวละคร/ฉากกลืนกัน ขาดคอนทราสต์และแสงเงาแบบ 3 มิติ

### การแก้ไขและปรับปรุง:
1. **ลบ Screen-space 2D Dusk Overlay**:
   * แก้ไข [NightDarknessOverlay.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/NightDarknessOverlay.shader) ให้ไม่วาดสีส้มแบนทับจอในเวลาพลบค่ำ เพื่อคืนรายละเอียด 100% ให้แก่ Texture พื้นและแสงเงา 3D
2. **ระบบหมุนองศาแสงและเงาตามเวลาจริง (Dynamic Celestial Sun/Moon Path)** ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs):
   * คำนวณองศา Directional Light (Sun/Moon) แบบ Real-time ตามความคืบหน้าของเวลา (`PhaseProgress` 0.0 -> 1.0):
     * **Day (เช้า -> บ่าย)**: พระอาทิตย์ขึ้นมุมเฉียง (Pitch 35°, Yaw -70°) เงาทอดยาว -> เที่ยงวันขึ้นมุมสูง (Pitch 60°, Yaw -115°) ให้เงากระชับใต้เท้าและพื้นอ่านง่าย -> บ่ายคล้อยลงสู่ Pitch 38°
     * **Dusk (พลบค่ำ Golden Hour)**: พระอาทิตย์คล้อยต่ำสู่ขอบฟ้า (Pitch 38° -> 18°, Yaw -150° -> -175°) ทำให้เงา 3D ทอดยาวขนานพื้นอย่างสวยงามสไตล์ Cinematic
     * **Night (ราตรี)**: แสงจันทร์ขึ้นจากฟากตรงข้าม (Pitch 25° -> 55°, Yaw 35° -> 80°) ส่องประกายสีเงินคราม ให้เงาคมชัดในทิศตรงข้ามกับกลางวัน
3. **การสร้าง Contrast อุณหภูมิสี (Warm Sunlight vs Cool Ambient Fill)**:
   * แยกแสงอาทิตย์ (Directional Light) ให้เป็นโทนอุ่น และแสงเงาในบรรยากาศ (Ambient Light) ให้เป็นโทนเย็นอมฟ้า/ม่วง
   * ป้องกันไม่ให้เงามืดสนิทหรือกลายเป็นสีส้มทึบ ทำให้เห็นรายละเอียดทั้งในที่สว่างและในร่มเงา
4. **ปรับจูน Post-Processing ใน [DefaultVolumeProfile.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Settings/DefaultVolumeProfile.asset)**:
   * ปรับ Color Filter เป็นสีขาวกลาง (Neutral), ปรับ Exposure = 0.0, เพิ่ม Contrast = 15
   * ปรับ Bloom Threshold เป็น 1.05 เพื่อเรืองแสงเฉพาะไฟ ตะเกียง กระสุน ไม่ให้แสงขาวฟุ้งบวม
   * เปิด Vignette (Intensity 0.25, Smoothness 0.45) ให้ขอบจอมีความนุ่มลึกตีกรอบมุมมอง Isometric ได้อย่างลงตัว

