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

---

## 7. คืนความเข้ม สดใส และความอุ่นของสีสันฉาก (Color Vibrancy, Saturation & Warm Ambient Recalibration)

### ปัญหาที่พบ:
* หลังจากถอด Overlay สีส้มเดิมออก ทำให้ภาพขาดความอิ่มตัวของสี (Saturation ต่ำเกินไปที่ระดับ 8) และ Ambient Fill เดิมถูกดึงเป็นสีเทาเย็น ส่งผลให้ภาพดูซีด ทึม และรู้สึกเหมือน **"มีแต่เงา สีไม่เข้มเหมือนเดิม"**

### การแก้ไขและปรับจูนความเข้มสดของสี:
1. **เร่งความอิ่มตัวและความเปล่งปลั่งของสี (Color Saturation & Post-Exposure)**:
   * ใน [DefaultVolumeProfile.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Settings/DefaultVolumeProfile.asset):
     * เพิ่ม **Saturation = 22** (จากเดิม 8) ทำให้สีเสื้อผ้าตัวละคร, สีต้นไม้, ถังน้ำมัน, เลือด และพื้นกลับมาสด ฉ่ำ สวยงามคมเข้มทันที
     * ปรับ **Post-Exposure = +0.25** และ **Contrast = 18** ให้ภาพสว่างสดใส มีชีวิตชีวา ไม่อึมครึม
     * ปรับ Bloom Tint ให้อบอุ่นเรืองรองทองคำ (`{r: 1, g: 0.95, b: 0.88, a: 1}`)
2. **เปลี่ยนแสงเงา Ambient Shadow Fill ให้อบอุ่น ไม่เป็นสีเทาซีด**:
   * ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs):
     * เปลี่ยน Day Ambient จากสีเทาครามซีด `(0.40, 0.44, 0.50)` เป็นแสงสะท้อนอุ่นอมทองธรรมชาติ `(0.58, 0.54, 0.50)`
     * เปลี่ยน Dusk Ambient เป็น `(0.50, 0.34, 0.24)` และ Sun Intensity เป็น `1.15f` พร้อมสีทองส้มจัดจ้าน `(1.0, 0.58, 0.20)`
3. **คืนสีส้มอิฐทองอันอบอุ่นให้พื้น Texture (Terracotta Golden Ground Tint)**:
   * ปรับ Ground Ambient Tint ในช่วง Dusk คืนสู่โทนส้มทองเข้มข้น `(0.96, 0.68, 0.40)` เพื่อไม่ให้พื้นดินกลายเป็นสีขาวอมเทาจืดชืด
4. **ใส่ Radial Sunset Vignette โอบล้อมขอบจอในยามพลบค่ำ**:
   * ใน [NightDarknessOverlay.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/NightDarknessOverlay.shader): ให้ช่วง Dusk มีไออุ่นสีส้มอำพันนวลๆ โอบรอบขอบจออย่างมีมิติ โดยที่พื้นที่กลางจอตรงตัวละครยังสว่างใส คมชัด ไม่ถูกบดบัง

---

## 8. ระบบแสงแดดส่องผ่านยอดไม้ ฝุ่นละอองในอากาศ และลำแสง (God Rays, Atmospheric Dust Motes & Woodland Lighting)

### ตามภาพอ้างอิงของผู้ใช้ (`media_1793459221819.png`):
* แสงแดดยามกลางวันส่องเฉียงลงมาผ่านยอดไม้ (Pitch 28° - 38°, Yaw -105° ถึง -155°) ทอดเงายาวไปตามพื้นและวัตถุ
* ลำแสง Volumetric Sun Shafts (God Rays) ส่องลงมาจากฟากฟ้าตัดผ่านยอดไม้
* มีละอองฝุ่นทองคำ (Atmospheric Dust Motes) ลอยฟุ้งเป็นประกายระยิบระยับในอากาศเมื่อต้องแสงแดด ("เหมือนมีฝุ่นๆ")
* หมอกบรรยากาศสีทองอ่อนแบบ Linear Fog (ระยะ 18m - 75m) ที่ไม่บดบังระยะต่อสู้ใกล้ตัวผู้เล่น

### สิ่งที่พัฒนาและติดตั้ง:
1. **สร้าง Shader ละอองฝุ่นในอากาศ [AtmosphericDustMote.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/AtmosphericDustMote.shader)**:
   * คำนวณ Point Sprite แบบ Soft Radial Additive เรืองแสงสีทองโดยไม่ต้องใช้ Texture ภายนอก
2. **ระบบอนุภาคฝุ่นละอองลอยในอากาศ (Atmospheric Dust Motes)** ใน [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs):
   * ปล่อยละอองฝุ่น 180 อนุภาค ลอยเบาๆ ในขอบเขต 28m x 10m x 28m รอบตัวละครและกล้อง
   * เคลื่อนไหวแบบ Brownian Motion + Turbulence Noise ลอยขึ้นและพลิ้วไหวอย่างเป็นธรรมชาติ
   * ปรับสีตามเวลา: กลางวันสีทองนวล (`1.0, 0.92, 0.75`), พลบค่ำสีส้มอำพัน (`1.0, 0.68, 0.35`)
3. **ยกเครื่องการกำเนิดลำแสง God Rays จากฟากฟ้า**:
   * ย้ายจุดกำเนิด Quad ขึ้นไปบนท้องฟ้าตามทิศทาง `-sunForward * beamLength` ส่องลงมาสู่พื้นดินรอบตัวผู้เล่น
   * ปรับความกว้าง 14m และความยาว 55m พร้อมหมุนตามองศาพระอาทิตย์แบบ Real-time
4. **ปรับมุมพระอาทิตย์กลางวันแบบ Cinematic Diagonal** ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs):
   * ล็อคมุม Pitch ให้อยู่ระหว่าง 28° - 38° ตลอดทั้งวัน เพื่อให้เกิดเงาและลำแสงส่องเฉียงที่งดงามเหมือนในวิดีโออ้างอิง ไม่ตั้งชันเป็น 60°
5. **เพิ่มปุ่มรีเซ็ตแสงและฝุ่นในเมนูโกง F1** ใน [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs) และอัปเดตหน้าต่างปรับแต่ง [AtmosphericLightingEditorWindow.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/AtmosphericLightingEditorWindow.cs)

---

## 9. ปิดการทำงานของระบบแสงและแผ่นฟิล์ม Overlay แบบเดิม (Disabling Legacy 2D Lighting System)

### วัตถุประสงค์:
* ปิดระบบการคูณสีย้อมพื้นแบบ 2D (`_GlobalAmbientTint`) และแผ่น Canvas Mask ความมืดแบบเดิม เพื่อให้เกมทำงานด้วยระบบแสงและเงา 3D ของ URP (Directional Sun, Volumetric God Rays, Linear Fog, Dust Motes, และ Post-Processing Volume) 100% เต็มรูปแบบโดยไม่มี Overlay แบนๆ มารบกวน

### สิ่งที่ดำเนินการ:
1. **ปิด Canvas ความมืด 2D (`NightDarknessOverlay`)**:
   * ใน [NightDarknessController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/NightDarknessController.cs) ตั้งค่า `disableDarknessOverlay = true` เป็นค่าเริ่มต้น และสั่งปิด GameObject ทันทีตอนเริ่มเกม
2. **ปิดการย้อมสีพื้น 2D (`_GlobalAmbientTint`)**:
   * ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs) เพิ่มตัวเลือก `disableLegacyLighting = true` ล็อคค่า `_GlobalAmbientTint = Color.white`, `_GlobalDarknessAlpha = 0`, `_GlobalDuskAlpha = 0` เพื่อให้พื้นดินแสดงสี Texture ตามจริง และรับแสงสะท้อนจาก Directional Light 3D และ Post-Processing เต็ม 100%
3. **เพิ่มปุ่มสลับระบบแสงเก่าในเมนูโกง F1**:
   * ใน [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs) เพิ่มปุ่ม `ระบบแสงเก่า (Legacy 2D Overlay): ปิดอยู่ [OFF - ใช้ URP 100%] / เปิดอยู่ [ON]` ให้ผู้ใช้สามารถกดเปิด/ปิดเพื่อเทียบความแตกต่างได้ทุกเมื่อ

---

## 10. กำจัดแผ่นโพลีกอนลำแสงเหลี่ยมที่วิ่งตามตัวละคร (Removing Giant Billboard Light Quads)

### ปัญหาที่พบจากภาพหน้าจอของผู้ใช้:
* เกิดแถบแสงสีน้ำตาลทองขนาดใหญ่เป็นเส้นทแยงมุมเหลี่ยมผ่ากลางจอ และแถบแสงนี้เคลื่อนที่วิ่งตามตัวละครตลอดเวลา ("ทำไมแสง... มันตามตัวกุ")
* เกิดจากระบบจำลอง God Rays แบบ Mesh Quad (55 เมตร) เดิมที่เขียนให้จุดศูนย์กลางวิ่งตามตำแหน่งของผู้เล่น ทำให้กลายเป็นแผ่นโพลีกอนยักษ์บังฉากและตัดผ่านพื้นดิน

### การแก้ไข:
1. **ถอดระบบ Mesh Quad ออกอย่างถาวร**:
   * ลบตัวสร้าง `VolumetricGodRaysContainer` และ Mesh Quad ออกทั้งหมดจาก [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs)
   * เพิ่มฟังก์ชัน `DestroyLegacyGodRayContainers()` คอยค้นหาและทำลาย Container เก่าทันทีตอนเปิดเกม
2. **คงไว้เฉพาะระบบฝุ่นละอองลอยอิสระ (Atmospheric Dust Motes)**:
   * ละอองฝุ่นทองคำ 180 จุดยังคงลอยอย่างนุ่มนวลรอบๆ ตัวละครในพิกัด World Space (ผู้เล่นเดินผ่านฝุ่นได้ ตัวฝุ่นไม่ติดตัวละคร)
3. **คงระบบแสง URP 3D บริสุทธิ์**:
---

## 11. คืนระบบความมืดและไฟฉายยามค่ำคืน (Restoring Survival Horror Night Darkness & Torch Light Perimeter)

### ความต้องการของผู้ใช้:
* ผู้ใช้ชื่นชอบแสงกลางวันชุดใหม่ที่มีฝุ่นทองลอยและเงาเฉียง 3D สดใส ("อันนี้ดี") แต่ต้องการให้ **ตอนกลางคืน (Night)** มืดทึบและมีไฟฉายเหมือนก่อนปิดระบบแสงเดิม ("แต่ตอนมืดอะอยากให้เป็นเหมือนก่อนปิดแสงเก่า")

### การปรับปรุงและสถาปัตยกรรมที่ลงตัว:
1. **กลางวัน (Day) สดใส คมชัด บริสุทธิ์ 100%**:
   * ใน [NightDarknessController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/NightDarknessController.cs): เมื่อเป็นช่วงกลางวัน Canvas Overlay จะถูก Deactivate (`_overlayCanvasObj.SetActive(false)`) อัตโนมัติ จึงไม่มีการวาด 2D ทับหน้าจอแม้แต่น้อย (Zero Canvas Overdraw) ปล่อยให้แสงอาทิตย์สีทอง, เงา 3D เฉียง, และละอองฝุ่นทองคำทำงานเต็มประสิทธิภาพ
2. **กลางคืน (Night) ดึงความมืดสไตล์ Survival Horror กลับมาสมบูรณ์แบบ**:
   * ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs): ตั้งค่า `disableLegacyLighting = false` คืนการคำนวณ `_currentDarknessAlpha` ให้ค่อยๆ เฟดเข้าสู่ 1.0 และปรับโทนสีพื้น `_currentAmbientTint` เป็นสีกรมท่าครามใต้แสงจันทร์ `(0.25, 0.30, 0.45)`
   * แสง Directional Light ปรับลด Intensity สู่ 0.20 พร้อมสีครามเงิน
   * ใน [NightDarknessController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/NightDarknessController.cs): ตั้งค่า `disableDarknessOverlay = false` Canvas Overlay จะเปิดใช้งานอัตโนมัติเมื่อเริ่มเข้าสู่ความมืด
   * ใน [NightDarknessOverlay.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/NightDarknessOverlay.shader): แสดงวงกลมไฟฉายสว่างรอบตัวผู้เล่น พร้อมแสงวูบวาบหายใจธรรมชาติ (`_FlickerAmount = 0.008`) และรอบนอกตัดเข้าสู่ความมืด 90% ตามที่ผู้ใช้เคยตั้งค่าไว้
3. **การควบคุมไฟฉาย (Flashlight Toggle)**:
   * รองรับทั้งการกดปุ่ม **`L`** และ **`F`** เพื่อเปิด/ปิดไฟฉายรอบตัวละคร
   * หากดับไฟฉายในเวลากลางคืน จอจะมืดสนิทถึง 96%
   * มีระบบเปิดไฟฉายอัตโนมัติเมื่อก้าวเข้าสู่เวลากลางคืน (`autoLightAtNight = true`)
---

## 12. แก้ไขแผ่นพื้น (Plane) ไม่มืด และคืนระบบความมืดเต็มรูปแบบ (Ground Plane Night Darkening & Legacy Darkness Restoration)

### ปัญหาที่พบจากภาพหน้าจอของผู้ใช้:
* ผู้ใช้ส่งภาพหน้าจอช่วงเวลา Night พบว่า:
  1. ก้อนวัตถุและสิ่งกีดขวาง (Obstacle Cubes) มืดลงเป็นสีน้ำเงินเข้มตามแสง URP แต่ **"แผ่นพื้น (Ground Plane)"** กลับสว่างจ้าเป็นสีส้ม/น้ำตาลของเวลากลางวัน ไม่ยอมมืดตามฉาก
  2. ในหน้าต่าง F1 Cheat Menu ปุ่ม `Darkness Mask` แสดงสถานะสีแดง `[Darkness Mask: ซ่อน]` ทำให้ไม่มีแผ่นฟิล์มความมืดรอบตัวละครและไม่มีวงไฟฉาย

### สาเหตุเชิงเทคนิค:
1. แผ่นพื้นใช้ Shader น้ำหนักเบา [WorldSpaceGroundShader.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/WorldSpaceGroundShader.shader) ซึ่งทำงานแบบ Unlit (ไม่มีการคำนวณแสงแบบ Lit เพื่อรักษา 120+ FPS) และอาศัยตัวแปร `_GlobalAmbientTint` ในการหรี่แสง
2. เมื่อผู้ใช้สั่งให้ "ลองปิดระบบแสงเก่าไปด้วย" ตัวแปร `disableLegacyLighting` ได้สั่งล็อค `_GlobalAmbientTint = Color.white` เสมอ ทำให้แผ่นพื้นสะท้อนแสงเต็ม 100% ตลอดเวลาแม้จะเป็นเวลากลางคืน
3. ตัวแปร `disableDarknessOverlay` ถูกจำค่าสถานะเดิมที่ปิดไว้ ทำให้ Canvas ความมืดไม่ถูกวาดบนหน้าจอ

### การแก้ไขและคืนระบบแสงเก่าสมบูรณ์แบบ:
1. **ปลดล็อคการหรี่แสงแผ่นพื้น (Ground Plane Darkening)**:
   * ใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs): ถอดเงื่อนไขบายพาส `disableLegacyLighting` ออกทั้งหมด
   * เพิ่มค่าปรับตั้ง `nightGroundTint = new Color(0.12f, 0.16f, 0.28f, 1f)` สีกรมท่าครามใต้แสงจันทร์ เพื่อให้คูณกับ Texture แผ่นพื้นแล้วมืดลงกลมกลืนกับก้อน Obstacle Cubes ทันที
2. **การันตีเปิดใช้งาน Canvas ความมืดและไฟฉายตอนกลางคืน**:
   * ใน [NightDarknessController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/NightDarknessController.cs): บังคับให้ `disableDarknessOverlay = false` ทุกครั้งที่สคริปต์เริ่มทำงาน (`Awake`, `OnEnable`, `Start`)
   * ปรับปุ่มใน [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs) เมื่อกดปุ่ม `NIGHT (กลางคืน)` จะสั่งรีเซ็ตเปิด `DisableDarknessOverlay = false` ทันที พร้อมแสดงปุ่มสถานะสีเขียว `[Darkness Mask: เปิดใช้งาน [ON]]`
---

## 13. ตัดช่วงเวลา Dusk (พลบค่ำ) ออกทั้งหมด – หมุนเวียนเฉพาะ Day และ Night (Eliminating Dusk Phase - Direct Day <-> Night Cycle)

### ความต้องการของผู้ใช้:
* ผู้ใช้สั่ง: "dusk ไม่เอาได้ปะ" และ "เอาเฉพาะ night"
* ตัดช่วงเวลาพลบค่ำ (Dusk) ที่ย้อมแสงสีส้มออกทั้งหมด ให้เกมสลับระหว่าง **กลางวัน (Day)** และ **กลางคืน (Night)** โดยตรง

### สิ่งที่ดำเนินการ:
1. **ปรับวงรอบวัฏจักรเวลาใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs)**:
   * ปรับฟังก์ชัน `AdvancePhase()`: จากเดิม `Day -> Dusk -> Night -> Day` ให้เปลี่ยนเป็น `Day -> Night -> Day (DayCount++) -> Night` สลับกันโดยตรง 100%
   * ปรับ `GetCurrentPhaseDuration()`: เหลือเฉพาะ `dayDuration` และ `nightDuration`
   * ปรับ `UpdateFades()`: ลบการคำนวณ `targetDusk` และล็อค `_currentDuskAlpha = 0f` เสมอ
   * ปรับ `UpdateUIText()`: แสดงเฉพาะ `DAY` (สีเหลืองทอง) และ `NIGHT` (สีฟ้าคราม)
   * ปรับ `GetTargetSunRotation()`: พระอาทิตย์ส่องเฉียงยามกลางวัน สลับกับพระจันทร์โค้งยามค่ำคืนทันที ไร้ช่วงทแยงต่ำของ Dusk
2. **ทำความสะอาด Shader [NightDarknessOverlay.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/NightDarknessOverlay.shader)**:
   * ลบ Pass และการคำนวณ Radial Dusk Vignette สีส้มออกทั้งหมด
   * เหลือเฉพาะเงื่อนไข:
     * หากเป็นกลางวัน (`darkAlpha <= 0.005`) คืนค่าโปร่งใส `half4(0, 0, 0, 0)` ทันที (0 Overhead)
     * หากเป็นกลางคืน คืนค่าความมืด 90% และวงกลมไฟฉายรอบตัวผู้เล่น
3. **ปรับแต่งเมนูดีบั๊ก F1 ใน [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs)**:
   * ลบปุ่ม `[DUSK (เย็น)]` ออกจากแถบเปลี่ยนช่วงเวลาทันที เหลือเพียง 2 ปุ่มใหญ่:
     * `[ DAY (กลางวัน) ]`
     * `[ NIGHT (กลางคืน) ]`
   * ปุ่มกด `N` (Next Phase) จะสลับกระโดดระหว่าง Day และ Night ทันที
4. **ปรับ [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs) และ [DayNightLightingEditorWindow.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/DayNightLightingEditorWindow.cs)**:
   * หมอกและละอองฝุ่นทองคำสลับสีระหว่าง กลางวัน (Golden) และ กลางคืน (Midnight Cool)
   * ถอดปุ่ม Dusk ออกจากหน้าต่าง Editor Window
---

## 14. คืนช่วงเวลา Dusk (พลบค่ำ) โดยใช้แสงธรรมชาติ URP ใหม่ และใช้ระบบแสงเก่าเฉพาะเวลากลางคืนเท่านั้น (Dusk Restored with Modern URP Lighting & Legacy Darkness Kept Strictly for Night)

### ความต้องการของผู้ใช้:
* ผู้ใช้ชี้แจง: *"กูหมายถึงเอา Dusk แต่แสงตอน Dusk ไม่เอาแสงเก่า แสงเก่าเอาใช้แค่ night"*
* ต้องการให้มีช่วงเวลา **Dusk (พลบค่ำ)** อยู่ในวัฏจักรเวลา แต่:
  1. **ช่วงเวลา Dusk**: ห้ามใช้ระบบแสงเก่า (ไม่ใช้ 2D Darkness Mask Overlay, ไม่มีย้อมมืด, ไม่มี 2D Vignette บังจอ) ให้ใช้ระบบแสงธรรมชาติ URP ใหม่ทั้งหมด: แสงอาทิตย์อัสดงสีส้มทองมุมต่ำ (16°–30°) ทอดยาว, หมอกสีส้มอบอุ่น, และละอองฝุ่นทองคำเรืองแสง
  2. **ช่วงเวลากลางคืน (Night)**: ใช้ระบบแสงเก่าแบบจัดเต็ม: แผ่น Canvas ความมืด 90% รอบตัวละคร, วงไฟฉายส่องสว่าง (เปิด/ปิดด้วย L หรือ F), และแผ่นพื้นหรี่มืด 88% (`nightGroundTint`)

### สิ่งที่ดำเนินการ:
1. **คืนช่วงเวลา Dusk สู่ระบบใน [DayNightManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs)**:
   * วงรอบวัฏจักรกลับมาหมุนเวียน 3 ช่วงสมบูรณ์: `Day -> Dusk -> Night -> Day (DayCount++) -> Dusk -> Night`
   * `GetCurrentPhaseDuration()`: คืนค่า `duskDuration` (12 วินาที)
   * `IsDusk` คืนค่าตาม `_currentPhase == TimePhase.Dusk`
   * `UpdateUIText()`: แสดงแถบชื่อ `DUSK` สีส้มอัสดง (`#FF9933`)
2. **แยกพฤติกรรมแสงอย่างชัดเจน (Old Light ONLY for Night)**:
   * **ช่วง Dusk**:
     * `targetDarkness = 0.0f` (ไม่มีฟิล์มความมืด)
     * `_currentDuskAlpha = 0.0f` (ไม่ส่ง 2D Vignette แสงเก่าลง GPU)
     * แผ่น Canvas ความมืด [NightDarknessController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/NightDarknessController.cs) ปิดการทำงานอัตโนมัติ (`SetActive(false)`) เพราะทำงานเฉพาะเมื่อ `IsNight` เท่านั้น
     * แผ่นพื้นใช้ `duskGroundTint` (สีส้มทองอบอุ่นสว่าง ไม่มืด)
     * แสง Directional Light ปรับมุมทอดยาวต่ำสู่ขอบฟ้า (Pitch 30° -> 16°) สีส้มทอง Sunset (`duskSunColor`)
   * **ช่วง Night**:
     * `targetDarkness = 1.0f` (ฟิล์มความมืด 90% รอบตัวละครทำงาน)
     * แผ่น Canvas ความมืดเปิดใช้งานอัตโนมัติ (`SetActive(true)`)
     * แผ่นพื้นหรี่มืดลงสู่สีกรมท่าลึก 88% (`nightGroundTint`)
     * วงไฟฉายรอบตัวผู้เล่นเปิดใช้งาน (กด L หรือ F เพื่อเปิด/ปิด)
3. **ผสานหมอกและละอองฝุ่นช่วง Dusk ใน [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs)**:
   * เมื่อเข้าสู่ช่วง Dusk:
     * หมอกระยะไกล (Linear Fog) ปรับสีเป็นสีส้มอบอุ่นยามอาทิตย์อัสดง (`duskFogColor`)
     * ละอองฝุ่นลอยในอากาศ (Dust Motes) สะท้อนแสงเป็นสีทองประกายส้ม (`duskDustColor`)
4. **อัปเดตเมนู F1 ใน [BossSpawnerRuntime.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/BossSpawnerRuntime.cs) และ [DayNightLightingEditorWindow.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Editor/DayNightLightingEditorWindow.cs)**:
   * เพิ่มปุ่ม `[ DUSK (เย็น) ]` สีส้มกลับมาในเมนูทั้งสอง สามารถกดคลิกสลับช่วงเวลาได้ทันที 1 คลิก
---

## 15. ปรับปรุงละอองฝุ่นเป็นหมอกและฝุ่นละมุนยามเช้า (Morning Mist & Atmospheric Dust Haze Overhaul)

### ความต้องการของผู้ใช้:
* ผู้ใช้สั่ง: *"ฝุ่นอะอยากได้ฝุ่นๆแบบฝุ่นๆเลยเหมือนหมอกตกตอนเช้าอะ"*
* เปลี่ยนจากอนุภาคจุดประกายแสงขนาดเล็ก (ที่เดิมดูเหมือนสะเก็ดลูกไฟ/เถ้าถ่านเรืองแสง) ให้กลายเป็น **"มวลหมอกและละอองฝุ่นยามเช้า"** ที่ลอยเรี่ยพื้นละมุนตา เป็นธรรมชาติ ไร้แสงไฟเรืองแสบตา

### สิ่งที่ดำเนินการ:
1. **ยกเครื่อง Shader [AtmosphericDustMote.shader](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Shaders/AtmosphericDustMote.shader)**:
   * **เปลี่ยนโหมด Blending**: จาก Additive (`Blend One One` ที่สว่างจ้าเหมือนหลอดไฟ) เป็น Alpha Blending ธรรมชาติ (`Blend SrcAlpha OneMinusSrcAlpha`)
   * **โครงสร้างกลุ่มเมฆหมอกนุ่มนวล (Soft Cloud Falloff)**: ใช้อัลกอริทึม Organic Cloud Noise ผสานกับ Smoothstep Falloff ทำให้ขอบอนุภาคนวลตา ไร้เหลี่ยม และไม่เป็นวงกลมแข็งกระด้าง
2. **ปรับแต่งพฤติกรรม Particle System ใน [AtmosphericLightingController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs)**:
   * **ขนาดอนุภาค (Puff Size)**: ขยายจากจุดเล็ก 0.12m เป็นกลุ่มก้อนหมอกขนาดกว้าง 1.6m – 3.2m (`dustMoteSize = 2.4f`)
   * **ตำแหน่งเรี่ยพื้น (Ground-Level Fog)**: ปรับตำแหน่ง Emitter ต่ำลงมาอยู่ที่ความสูง 0.8m เหนือพื้น ให้ลอยเรี่ยระดับเอวและพื้นดินเหมือนหมอกยามเช้าตก
   * **การเคลื่อนที่และการหมุนอย่างละมุน**:
     * เพิ่มการหมุนอย่างช้าๆ (`rotationOverLifetime`) ให้กลุ่มหมอกคลี่ตัวหมุนวนอย่างเป็นธรรมชาติ
     * ค่อยๆ ขยายขนาดขึ้นตามเวลา (`sizeOverLifetime: 0.8x -> 1.25x`)
     * ความเร็วลมพัดแผ่วเบาในแนวระนาบ X/Z
   * **สีและความโปร่งแสง (Translucent Mist)**:
     * **Day (หมอกยามเช้า)**: สีขาวนวลจางๆ โปร่งแสง 16% (`rgba(0.92, 0.90, 0.86, 0.16)`)
     * **Dusk (ไอหมอกยามเย็น)**: สีพีชอบอุ่นละมุน ไม่ส้มจัด ไม่เป็นลูกไฟ (`rgba(0.88, 0.74, 0.64, 0.14)`)
     * **Night (ไอหมอกใต้แสงจันทร์)**: สีครามจางๆ 8% (`rgba(0.60, 0.68, 0.80, 0.08)`)









