# แผนพัฒนาระบบฤดูกาล & สภาพอากาศ (Dynamic Season & Weather System)

**วันที่บันทึก**: 27 พฤศจิกายน 2026  
**กำหนดทำ**: พรุ่งนี้ (28 พฤศจิกายน 2026)  
**สถานะ**: รอเริ่มพัฒนา (Planned)

---

## 1. ระบบฤดูกาล (`Season System`) — เปลี่ยนทุก 5 วัน
- **ระยะเวลาอ้างอิงจาก `DayNightManager` ปัจจุบัน (`Gameplay_3D.unity`)**:
  - **1 วันเต็ม** = `360 วินาที` (**6 นาที** — กลางวัน 3 นาที + พลบค่ำ 45 วิ + กลางคืน 2 นาที 15 วิ)
  - **5 วัน (1 ฤดู)** = **`30 นาที` (ครึ่งชั่วโมงพอดี)** (หรือประมาณ **18.75 นาที** หากผู้เล่นนอนเตียง `[Z]` ข้ามคืนทุกคืน) — เล่นรอบหนึ่ง ~2 ชั่วโมงจะได้สัมผัสครบทั้ง 4 ฤดู
  - ใส่ตัวแปร `daysPerSeason = 5` ใน Inspector เพื่อให้ปรับสั้น/ยาวได้ตลอด
- **รอบ 4 ฤดูกาล** (โชว์บน HUD คู่กับวัน เช่น `DAY 1–5 • AUTUMN` / `DAY 6–10 • RAINY` / `DAY 11–15 • WINTER` / `DAY 16–20 • SUMMER`, บันทึกลง `SaveData`, ซิงค์ Co-op):
  1. **Autumn / Mild (ใบไม้ร่วง / เริ่มต้น)** — อากาศแจ่มใสเป็นหลัก สลับฝนปรอยบางวัน เหมาะฟาร์มของเตรียมเสบียง
  2. **Monsoon / Rainy (ฤดูฝน)** — มีโอกาสเกิด **ฝนตกธรรมดา (`Rain`)** และ **ฝนตกฟ้าผ่า (`Thunderstorm`)** สูง, พืชในกระถางโตไวขึ้น
  3. **Winter (ฤดูหนาว)** — มีโอกาสเกิด **หิมะตก (`Snow`)** และ **พายุหิมะ (`Blizzard`)** สูง, หิมะเกาะพื้นและหลังคาสิ่งปลูกสร้างตลอดฤดู ต้องพึ่งกองไฟ/เตาผิง/หลบในบ้าน
  4. **Summer (ฤดูร้อน)** — แดดแรงโทนอบอุ่น, หิวน้ำไวขึ้นเล็กน้อย, มีพายุฝนฟ้าคะนองสลับบางวัน

---

## 2. สภาพอากาศ 4 รูปแบบ (`WeatherType`)

### 2.1 ฝนตกธรรมดา (`Rain`) — ✅ Implemented (`WeatherManager.cs`)
- **ตารางสุ่ม (Host Authoritative + Co-op Synced)**: สุ่มทุก **60 วินาที** โอกาสเกิด **10%** ตกนาน **30 วินาที** (`NetPacketType.WeatherSync`, รองรับ Late-Join + ปุ่มเสกในเมนู `[F1]` แท็บ `Weather / Events`)
- **Visual / Particle**: เม็ดฝนตกโปรยปรายรอบกล้องผู้เล่น + วงน้ำกระเซ็นบนพื้น (`RainSplashes`), ซ่อนอัตโนมัติเวลาเข้าในบ้าน (`BuildingInteriorManager`)
- **พื้นเปียก (`WorldSpaceGroundShader.shader`)**: ใช้ `_GlobalWetness` ทำให้พื้นนอกบ้านสีเข้มชื้นเนียนๆ และแห้งกลับเมื่อฝนหยุด (พื้นในบ้านเป็น `0` เสมอ)
- **เข้าบ้าน (`BuildingInteriorManager`)**: ปิดภาพฝน, น้ำกระเซ็น, แถบสถานะฝน, เสียงฝน, พื้นเปียก และยกเลิกผลลดเร็ว x2 ทันทีเมื่ออยู่ในอาคาร
- **Gameplay Effect (ตากฝนกลางแจ้ง)**: ค่า **สติ (Sanity), ความหิว (Hunger), และความกระหาย (Thirst) ลดเร็วขึ้น `2x`** (หลบเข้าบ้านเพื่อกันผลนี้)

### 2.2 ฝนตกฟ้าผ่า (`Thunderstorm`)
- **Visual / Particle**: ฝนตกหนักเอียงตามแรงลม + เม็ดน้ำกระเซ็นหนาแน่น + หมอกมืดครึ้ม
- **ฟ้าแลบ & ฟ้าร้อง**: แสงแฟลชฟ้าแลบวาบทั่วฉากเป็นระยะ ตามด้วยเสียงฟ้าร้องทุ้มๆ
- **ระบบฟ้าผ่าลงพื้นจริง (Lightning Strike)**:
  - สุ่มจุดฟ้าผ่ารอบพื้นที่ผู้เล่น (ไม่ผ่าในบ้าน/Interior)
  - แสดงวงเตือนบนพื้นล่วงหน้า ~1.2–1.5 วินาที (ใช้ Sprite จาก `HitboxSpriteLibrary` โปร่งแสง) + ประกายไฟฟ้าเตือน
  - ผ่าลงมาเป็นเส้นสายฟ้า 3D สว่างจ้า + กล้องสั่น (`CameraShake`) + ดาเมจ/สตั๊นใส่ทั้งผู้เล่นและศัตรูที่ยืนอยู่ในวง

### 2.3 หิมะตก (`Snow`)
- **Visual / Particle**: เกล็ดหิมะสีขาวโปรยช้าๆ ลอยพลิ้วตามลมเบาๆ รอบพื้นที่ผู้เล่น
- **บรรยากาศ & แสง**: โทนแสงฟ้าเย็น (Cool Winter Tint) + หมอกขาวจางๆ ไม่บดบังสายตา
- **เสียง (Audio)**: เสียงลมหนาวเบาๆ

### 2.4 พายุหิมะ (`Blizzard`)
- **Visual / Particle**: เกล็ดหิมะและลมหิมะพัดเฉียงแนวนอนรุนแรง + หมอกหิมะหนาลดระยะมองเห็น + ไอเย็นขอบจอ (Frost Vignette)
- **เสียง (Audio)**: เสียงพายุลมหิมะหวีดหวิวแรง
- **Gameplay**: ลมหนาว/ทัศนวิสัยจำกัด — การหลบเข้าบ้าน (`BuildingInteriorManager`) หรือยืนใกล้กองไฟ (`CampfireBuilding` / เตาผิง) ช่วยกันพายุและลดผลกระทบ

---

## 3. หิมะเกาะพื้น & หลังคาสิ่งปลูกสร้าง (ไม่ต้องปั้นโมเดลใหม่)
- **พื้น (`WorldSpaceGroundShader.shader`)**:
  - เพิ่ม Global Shader Property `_GlobalSnowAmount` (`0` → `1`) และ `_GlobalWetness` (`0` → `1`)
  - ใช้ World-Space Noise ให้หิมะค่อยๆ จับเป็นหย่อมๆ แล้วคลุมเนียนทั้ง 9 แผ่นแมพ (ค่อยๆ ละลายเมื่อพ้นฤดูหนาว) และสั่งเป็น `0` อัตโนมัติตอนอยู่ข้างในบ้าน (`Interior`)
- **สิ่งปลูกสร้าง ตึก รถ ต้นไม้ & ของคราฟต์ (`Top-Facing Snow`)**:
  - ใช้เช็ก Normal ด้านบน `dot(normalWS, float3(0, 1, 0))` เพื่อพอกหิมะขาวเฉพาะผิวที่หันขึ้นฟ้า (หลังคา, ฝากระโปรงรถ, ยอดไม้, ขอบกำแพง, ป้อมปืน, หีบ) โดยผนังด้านข้างยังเห็นสีและลายเดิมครบ 100%
  - **วงละลายรอบกองไฟ**: หิมะรอบ `CampfireBuilding` ละลายเห็นพื้นดินจริง

---

## 4. จุดเชื่อมต่อระบบเดิม & เครื่องมือทดสอบ
- **ศูนย์กลางจัดการ**: สร้าง `SeasonWeatherManager.cs` (Singleton + Smooth Transition + `daysPerSeason = 5`)
- **แสงและเวลา**: เชื่อม [`DayNightManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Managers/DayNightManager.cs) และ [`AtmosphericLightingController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Rendering/AtmosphericLightingController.cs) (คงขอบโลกสีดำ `#000000`)
- **เข้า-ออกบ้าน (Interior)**: เชื่อม [`BuildingInteriorManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/BuildingInteriorManager.cs) และ [`InteriorWindowDayNightController.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/InteriorWindowDayNightController.cs)
- **Save & Co-op**: บันทึกฤดู/สภาพอากาศลง `SaveData` และซิงค์จาก Host → Client ผ่าน `EmpireNetworkManager` (รองรับ Late-Join + ซิงค์จุดฟ้าผ่า)
- **Debug / Test Controls**: ปุ่ม/คำสั่งสลับฤดูและอากาศ (`Clear`, `Rain`, `Thunderstorm`, `Snow`, `Blizzard`) และปรับระดับหิมะเกาะพื้นได้ทันทีตอนเทส
