# แผนฟีเจอร์ที่ต้องพัฒนาต่อ (Stash Storage & Weapon Modification / Artifact System)

**วันที่บันทึก**: 22 พฤศจิกายน 2026 (อัปเดตล่าสุด: 24 พฤศจิกายน 2026)  
**สถานะ**: โครงสร้างหลัก, UI, Online Sync, กติกาจำกัดระดับ (`Common 0–4 | Rare 1–2 | Legendary 1`) และ **Weapon Artifacts ครบทั้ง 229 ชิ้น (19 อาวุธ × 12 ชิ้น + 1 Universal Ranged `Double-Tap Trigger`, ไม่มีสถานะธาตุ)** เสร็จสมบูรณ์และลงทะเบียนใน `ItemDatabase.asset` แล้ว (คงเหลือผูก SaveSystem และระบบธาตุในรอบถัดไป)

---

## 1. ระบบคลังเก็บของ (Storage / Stash System) — ✅ Implemented
- สร้าง [`StorageUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/StorageUI.cs) เชื่อมกับ `Storage_HUD` และ `StorageCrateBuilding` (รองรับคลิกฝาก/ถอนทีละ 1, `Alt + คลิก/ลาก` ฝาก/ถอนทั้งหมด, Drag & Drop ข้ามฝั่ง, แบ่งหน้า 2 ฝั่ง และซิงค์ Co-op Online + Late-Join + ข้ามห้องเรียบร้อย)
- **คงเหลือ**: บันทึกข้อมูลคลังเก็บของลง `SaveSystem` (`SaveData`)

---

## 2. ระบบปรับแต่งอาวุธ & ใส่ Artifact (`Modify Weapon` System) — ✅ Implemented (229/229 Artifacts Ready)
- สร้าง [`WeaponArtifactManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/WeaponArtifactManager.cs), [`WeaponUpgradeUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/WeaponUpgradeUI.cs), [`ArtifactSlotUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/ArtifactSlotUI.cs) และ `ItemType.Artifact` + `ArtifactModConfig` (รองรับ Damage, Damage%, Multi-Shot/Throw, Echo Burst 1-click=2-shots, Spread, Fire Rate/Attack Speed, Mag Size, Reload Speed, Range, Pierce, Full-Auto, Crit Chance, Crit Multiplier, Knockback, Stagger, Lifesteal, Ammo Save) พร้อมระบบจำกัดจำนวนใส่ต่ออาวุธ (`Common 0–4 ใบ`, `Rare สูงสุด 2 ใบ`, `Legendary 1 ใบเท่านั้น`) เชื่อมกับ `WeaponUpgrade_HUD` + การยิง/ฟัน/ระเบิดจริง + ซิงค์ Online Co-op เรียบร้อยแล้ว
- สร้าง `ItemData` Asset จริงครบ **229 ชิ้น** (19 อาวุธ × 12 ชิ้นเฉพาะตัว + 1 Universal Ranged `Double-Tap Trigger` ใส่ได้กับปืนทุกชนิด) ภายใต้ `Assets/Resources_Addressable/Database/ItemData/Artifacts/` พร้อมลงทะเบียนใน [`ItemDatabase.asset`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources_Addressable/Database/ItemDatabase.asset) และจดตารางนับจำนวนครบทุกอาวุธใน [`weapon-artifacts-catalog.md`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Docs/weapon-artifacts-catalog.md)
- **คงเหลือ**: เชื่อมบันทึก Artifact ที่ติดตั้งลง `SaveData` และระบบสถานะธาตุ (พิษ/ไฟ/น้ำแข็ง/ช็อต — พักไว้ก่อนตามคำสั่ง)
