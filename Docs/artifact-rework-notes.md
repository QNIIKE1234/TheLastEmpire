# Artifact Rework & Custom Skill Notes (บันทึกแผนปรับแต่ง Artifact)

ไฟล์นี้ใช้จดและติดตามสถานะการปรับแต่ง Artifact, แอนิเมชัน, สกิลคลิกขวา (RMB) และ Synergy ระหว่าง Artifact

---

## 1. Pistol

### `ART011_Pistol_11_AkimboPhantom` — Akimbo Phantom (Legendary)
- **สถานะ**: `Implemented`
- **โมเดล & แอนิเมชัน (Visual / Anim)**:
  - เปิดโหมด **ถือปืนพก 2 กระบอก (Dual Pistols / Akimbo)** — สร้างปืนพกมือซ้าย (`_offhandPistolObj`) บนกระดูก `LeftHand` พร้อมยกแขนซ้ายเล็งคู่กับมือขวาใน `LateUpdate()`
- **ค่าสถานะติดตัว (Passive)**:
  - **Max Magazine ×2** (`magazineMultiplier = 2.0`, เช่น 12 → 24 นัด หรือรวมโบนัสแม็กแล้วคูณ 2) + `Shot +1, SPD +30%`
- **สกิลคลิกขวา (Right-Click Active Skill — `Pistol_AkimboSpin`)**:
  - กด **คลิกขวา (RMB)** เล่นท่าหมุนตัว **360°** กางแขนถือปืน 2 มือยิงกระสุนกระจายออก **รอบตัวทุกทิศทาง (12 นัด/รอบ)**
  - **Synergy กับ `ART005_Pistol_05_TwinChamber`**: ถ้าใส่คู่กันจะหมุนตัวยิงรอบตัว **ติดต่อกัน 2 รอบ (24 นัด, 720°)** + ซิงก์ Co-op

---

## 4. Shotgun

### `ART047_Shotgun_11_DoubleBarrelSalvo` — Double-Barrel Salvo (Legendary)
- **สถานะ**: `Implemented`
- **ค่าสถานะติดตัว (Passive — คงของเดิมครบ)**:
  - เพิ่มลูกปราย **+5 เม็ด** + ดาเมจ **+25%** (`Pellets +5, DMG +25%`)
- **สกิลคลิกขวา (Right-Click Active Skill — `Shotgun_KnockbackBlast`)**:
  - กด **คลิกขวา (RMB)** ยิงคลื่นกระแทกเป็นกรวยกว้างด้านหน้า (8m) ทำดาเมจและ **ผลักศัตรูทุกตัวถอยหลังออกไปอย่างแรง** + ซิงก์แรงผลักแบบ Host-authoritative ใน Co-op

---

## 9. Rocket Launcher

### `ART107_RocketLauncher_11_BarrageLauncher` — Barrage Launcher (Legendary)
- **สถานะ**: `Implemented`
- **ค่าสถานะติดตัว (Passive — คงของเดิมครบ)**:
  - `Full-Auto, MAG +4, SPD +40%, RLD +35%`
- **สกิลคลิกขวา (Right-Click Active Skill — `Rocket_JavelinHoming`)**:
  - กด **คลิกขวา (RMB)** ล็อกเป้าศัตรูอัตโนมัติ (ขึ้นเป้าล็อกสีแดงบนพื้นใต้ศัตรู) แล้วปล่อย **ขีปนาวุธพุ่งขึ้นฟ้า** โค้งกลางอากาศแล้ว **ดิ่งพุ่งติดตามเข้าใส่เป้าหมาย** ระเบิด AOE รุนแรง + ซิงก์ Co-op

---

## Melee Range Artifacts (Global Balance)

- **สถานะ**: `Implemented` (ปรับลงไฟล์ `.asset` ครบทั้ง **30 ชิ้น** ในหมวดอาวุธตีใกล้ `10_Knife` – `18_Chainsaw`)
- **กติกา**: Artifact อาวุธตีใกล้ทุกชิ้นที่เพิ่มระยะโจมตี (`bonusRange > 0`) ถูกปรับลด **`damageMultiplier` ลง -10% (`-0.10`)** พร้อมอัปเดต `shortEffectSummary` และ UI แสดงค่าดาเมจที่ลดลงเรียบร้อย

---

## 10. Knife

### `ART120_Knife_12_CoupDeGrace` — Coup De Grace (Legendary)
- **สถานะ**: `Implemented`
- **ค่าสถานะติดตัว (Passive — คงของเดิมครบ)**:
  - `DMG +80%, CRIT +45%, CDMG +90%`
- **สกิลคลิกขวา (Right-Click Active Skill — `Knife_ThrowConsume`)**:
  - กด **คลิกขวา (RMB)** ขว้างมีดหมุนพุ่งไปข้างหน้าใส่ศัตรูทำดาเมจ **×3 เท่า** แต่ **มีดจะหายไปจากกระเป๋าและมืออย่างถาวร** + ซิงก์ภาพมีดขว้างใน Co-op
