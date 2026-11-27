# Artifact Rework & Custom Skill Notes (บันทึกแผนปรับแต่ง Artifact)

ไฟล์นี้ใช้จดสเปกการปรับแต่ง Artifact, แอนิเมชัน, สกิลคลิกขวา (RMB) และ Synergy ระหว่าง Artifact ตามที่ผู้พัฒนาออกแบบไว้ ก่อนลงมือทำจริง

---

## 1. Pistol

### `ART011_Pistol_11_AkimboPhantom` — Akimbo Phantom (Legendary)
- **สถานะ**: `Planned / Noted`
- **โมเดล & แอนิเมชัน (Visual / Anim)**:
  - เปลี่ยนท่าถือ/วิ่ง/ยิงเป็น **ถือปืนพก 2 กระบอก (Dual Pistols / Akimbo)** ทั้งมือซ้ายและมือขวา
- **ค่าสถานะติดตัว (Passive)**:
  - **Max Magazine ×2** (ความจุแม็กกาซีนคูณ 2 เพราะถือปืน 2 มือ)
- **สกิลคลิกขวา (Right-Click Active Skill — 360° Spin Barrage)**:
  - เมื่อกด **คลิกขวา (RMB)** ตัวละครจะเล่น **แอนิเมชันหมุนตัว (Spin)** แล้วยิงกระสุนกระจายออก **รอบตัว 360°** ทุกทิศทาง (ตามภาพร่าง: กระสุนพุ่งออกจากตัว Player รอบวง 1 รอบ)
- **Synergy กับ `ART005_Pistol_05_TwinChamber` (Twin Chamber)**:
  - ถ้าไม่ได้ใส่ `ART005`: คลิกขวาจะยิงรอบตัว **1 รอบ**
  - ถ้าใส่คู่กับ **`ART005_Pistol_05_TwinChamber`**: คลิกขวาจะยิงรอบตัว **ติดต่อกัน 2 รอบ (2 Volleys)**

---

## 4. Shotgun

### `ART047_Shotgun_11_DoubleBarrelSalvo` — Double-Barrel Salvo (Legendary)
- **สถานะ**: `Planned / Noted`
- **สกิลคลิกขวา (Right-Click Active Skill — Knockback Blast / Pushback)**:
  - เปลี่ยนเป็นเมื่อกด **คลิกขวา (RMB)** จะยิง/กระแทกคลื่นผลัก **Enemy ถอยหลังออกไป (Knockback Pushback)**
