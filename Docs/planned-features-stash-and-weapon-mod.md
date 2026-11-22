# แผนฟีเจอร์ที่ต้องพัฒนาต่อ (Stash Storage & Weapon Modification / Artifact System)

**วันที่บันทึก**: 22 พฤศจิกายน 2026 (อัปเดตล่าสุด: 23 พฤศจิกายน 2026)  
**สถานะ**: โครงสร้างหลักและ UI + Online Sync เสร็จแล้ว (พร้อมเพิ่มข้อมูล Asset Artifact 4–10 ชิ้นต่ออาวุธ + ผูก SaveSystem ในรอบถัดไป)

---

## 1. ระบบคลังเก็บของ (Storage / Stash System) — ✅ Implemented
- สร้าง [`StorageUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/StorageUI.cs) เชื่อมกับ `Storage_HUD` และ `StorageCrateBuilding` (รองรับคลิกฝาก/ถอนทีละ 1, `Alt + คลิก/ลาก` ฝาก/ถอนทั้งหมด, Drag & Drop ข้ามฝั่ง, แบ่งหน้า 2 ฝั่ง และซิงค์ Co-op Online + Late-Join + ข้ามห้องเรียบร้อย)
- **คงเหลือ**: บันทึกข้อมูลคลังเก็บของลง `SaveSystem` (`SaveData`)

---

## 2. ระบบปรับแต่งอาวุธ & ใส่ Artifact (`Modify Weapon` System) — ✅ Core & UI Implemented
- สร้าง [`WeaponArtifactManager.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/WeaponArtifactManager.cs), [`WeaponUpgradeUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/WeaponUpgradeUI.cs), [`ArtifactSlotUI.cs`](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/ArtifactSlotUI.cs) และ `ItemType.Artifact` + `ArtifactModConfig` ใน `ItemData.cs` เชื่อมกับ `WeaponUpgrade_HUD` + การยิง/ฟันจริง + ซิงค์ Online Co-op เรียบร้อยแล้ว
- **คงเหลือ**: สร้าง ItemData Asset ของ Artifact เฉพาะอาวุธแต่ละชนิด (4–10 อย่างต่ออาวุธ) และเชื่อมบันทึก Artifact ที่ติดตั้งลง `SaveData`
