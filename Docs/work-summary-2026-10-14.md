# สรุปบันทึกการพัฒนาเกม (Work Summary) - 14 ตุลาคม 2026

บันทึกชุดนี้สรุปการพัฒนาและนำระบบคลังอาวุธทั้งหมดเข้าสู่เกม **The Last Empire** ตามเอกสาร Game Design Document ([weapon-design.md](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/GDD/weapon-design.md)) ครอบคลุมทั้งระบบ ItemData, ฐานข้อมูล ItemDatabase, ราคากลางใน ShopDatabase, ระบบต่อสู้พิเศษของอาวุธประชิด, ระบบกระสุนสากล, และการควบคุมการสลับอาวุธ

---

## 1. การสร้างและนำเข้า Asset คลังอาวุธใหม่ 14 รายการ

สร้าง `ItemData` ScriptableObject ใหม่ 14 ไฟล์ ภายใต้ `Assets/Resources/Database/ItemData/` และลงทะเบียนเข้าสู่ [ItemDatabase.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemDatabase.asset):

### 🗡️ อาวุธระยะประชิดใหม่ 6 ชนิด (Melee Weapons):
1. **Fire Axe (ขวานดับเพลิง):** [ITEM0016_FireAxe.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0016_FireAxe.asset)
   * Damage: 65 | Attack Rate: 0.70s | Radius: 1.35m | Knockback: 12 | Stagger: 0.4s
   * คุณสมบัติพิเศษ: โอกาส 25% สร้างสถานะ **Bleed** (+15 ดาเมจแผลลึก)
2. **Katana (ดาบซามูไร):** [ITEM0017_Katana.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0017_Katana.asset)
   * Damage: 50 | Attack Rate: 0.40s | Radius: 1.60m | Knockback: 8 | Stagger: 0.3s
   * คุณสมบัติพิเศษ: **Critical Chance +20% (สร้างดาเมจ 2.0 เท่า)** และกวาดฟันศัตรูพร้อมกันได้สูงสุด 4 ตัว
3. **Sledgehammer (ค้อนปอนด์ยักษ์):** [ITEM0018_Sledgehammer.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0018_Sledgehammer.asset)
   * Damage: 90 | Attack Rate: 0.95s | Radius: 1.40m | Knockback: 20 | Stagger: 0.8s
   * คุณสมบัติพิเศษ: **Ground Slam Shockwave** ทุบพื้นสร้างแรงสะเทือน สตั๊นศัตรูชะงัก 100% นาน 0.8 วินาที
4. **Survival Spear (หอกดัดแปลง):** [ITEM0019_SurvivalSpear.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0019_SurvivalSpear.asset)
   * Damage: 45 | Attack Rate: 0.45s | Radius: 2.20m | Knockback: 10 | Stagger: 0.3s
   * คุณสมบัติพิเศษ: ระยะแทงไกลที่สุดในเกม (2.2 เมตร) รักษาระยะห่างปลอดภัยจากฝูงซอมบี้
5. **Stun Baton (กระบองช็อตไฟฟ้า):** [ITEM0020_StunBaton.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0020_StunBaton.asset)
   * Damage: 35 | Attack Rate: 0.45s | Radius: 1.20m | Knockback: 6 | Stagger: 0.6s
   * คุณสมบัติพิเศษ: โอกาส 30% ปล่อย **Chain Shock** ช็อตส่งต่อประกายไฟชะงักศัตรูตัวข้างเคียง
6. **Chainsaw (เลื่อยไฟฟ้า):** [ITEM0021_Chainsaw.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0021_Chainsaw.asset)
   * Damage: 20 | Attack Rate: 0.10s | Radius: 1.20m | Knockback: 3 | Stagger: 0.1s
   * คุณสมบัติพิเศษ: ฟันรัวถี่ระดับสูง (0.1 วิ/ครั้ง = 200 DPS) เหมาะสำหรับบดขยี้เป้าหมายเดี่ยว

### 🔫 อาวุธระยะไกลใหม่ 6 ชนิด (Ranged Weapons):
7. **SMG (ปืนกลมือ):** [ITEM0022_SMG.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0022_SMG.asset)
   * Damage: 16 | Fire Rate: 0.08s | Mag: 35 | Reload: 1.2s | Range: 2.0m | Ammo: Light Ammo (Auto)
8. **Sniper Rifle (สไนเปอร์ไรเฟิล):** [ITEM0023_SniperRifle.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0023_SniperRifle.asset)
   * Damage: 110 | Fire Rate: 1.20s | Mag: 5 | Reload: 2.2s | Range: 6.0m | Ammo: Medium Ammo (Piercing)
9. **Revolver (ปืนลูกโม่แม็กนั่ม):** [ITEM0024_Revolver.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0024_Revolver.asset)
   * Damage: 55 | Fire Rate: 0.65s | Mag: 6 | Reload: 1.8s | Range: 2.5m | Ammo: Light Ammo (High Stopping Power)
10. **Flamethrower (ปืนพ่นไฟ):** [ITEM0025_Flamethrower.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0025_Flamethrower.asset)
    * Damage: 10 | Fire Rate: 0.10s | Mag: 50 | Reload: 2.5s | Range: 1.8m cone | Ammo: Heavy Ammo (Auto)
11. **Rocket Launcher (ปืนยิงจรวด):** [ITEM0026_RocketLauncher.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0026_RocketLauncher.asset)
    * Damage: 150 | Fire Rate: 1.50s | Mag: 1 | Reload: 2.5s | Range: 4.0m AOE 3.5m | Ammo: Heavy Ammo
12. **Crossbow (หน้าไม้ล่าสัตว์):** [ITEM0027_Crossbow.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0027_Crossbow.asset)
    * Damage: 70 | Fire Rate: 1.00s | Mag: 1 | Reload: 1.2s | Range: 3.5m | Ammo: Bolts (Silent Stealth)

### 📦 กระสุนใหม่ 2 ชนิด (Ammo Items):
13. **Heavy Ammo (ถังเชื้อเพลิง/จรวด):** [ITEM0028_HeavyAmmo.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0028_HeavyAmmo.asset)
14. **Bolts (ลูกศรหน้าไม้):** [ITEM0029_Bolts.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ItemData/ITEM0029_Bolts.asset)

---

## 2. การอัปเดตระบบเศรษฐกิจร้านค้า (Shop Integration)
อัปเดต [ShopDatabase.asset](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Database/ShopDatabase.asset) เพิ่มราคากลางสำหรับซื้อ-ขายในร้านค้า NPC:
* Fire Axe: 1,500
* Katana: 2,200
* Sledgehammer: 2,800
* Survival Spear: 1,200
* Stun Baton: 1,400
* Chainsaw: 5,000
* SMG: 3,200
* Sniper Rifle: 5,500
* Revolver: 2,200
* Flamethrower: 4,800
* Rocket Launcher: 7,500
* Crossbow: 1,800
* Heavy Ammo: 80
* Bolts: 25

---

## 3. การอัปเกรดระบบ Gameplay ใน PlayerController.cs
* **Ammo Alias System:** ปืนที่ใช้กระสุนกลุ่มใหม่สามารถดึงกระสุนชื่อเดิมมาใช้งานร่วมกันได้แบบ 100% Backwards-compatible:
  * `"Light Ammo"` $\leftrightarrow$ `"Pistol Ammo"`
  * `"Medium Ammo"` $\leftrightarrow$ `"Rifle Ammo"`
  * `"Heavy Ammo"` $\leftrightarrow$ `"Fuel Ammo"`
  * `"Shotgun Ammo"` $\leftrightarrow$ `"Ammo"`
* **Melee Special Mechanics:** ติดตั้งกลไกการคำนวณดาเมจและเอฟเฟกต์เฉพาะตัวของอาวุธประชิดในเมธอด `MeleeAttack()`:
  * Katana: คำนวณคริติคอล 2.0 เท่า (โอกาส 20%) และกวาดโดนได้ถึง 4 ตัว
  * Fire Axe: คำนวณดาเมจ Bleed เสริม 15 ดาเมจ (โอกาส 25%)
  * Sledgehammer: ปล่อยแรงกระแทกคลื่นระเบิดรอบทิศ + สตั๊น 0.8 วินาที
  * Stun Baton: ปล่อยคลื่นไฟฟ้าลูกโซ่ช็อตศัตรูตัวรอบข้าง (โอกาส 30%)
* **Stealth Firing (Crossbow):** ในเมธอด `AlertEnemiesToGunshot()` มีการดักไม่ส่งสัญญาณเสียงแจ้งเตือนมอนสเตอร์หากยิงด้วย Crossbow ทำให้ลอบยิงได้เงียบเชียบ
* **Expanded Controls:**
  * กดปุ่มตัวเลข **`1` ถึง `9`** เพื่อสลับปืนในมือได้โดยตรง
  * เพิ่มปุ่ม **`X`** สำหรับสลับอาวุธระยะประชิด (Cycle Melee Weapon) ที่มีอยู่ในกระเป๋า
  * ยังคงใช้ Mouse Scroll Wheel สำหรับหมุนสลับปืนตามเดิม

---

## 4. การปรับปรุงระบบกระเป๋าเป้ (PlayerInventory.cs)
* ปรับปรุงฟังก์ชัน `UseItem` ของอาวุธระยะไกลและขว้าง ให้สแกนหา Slot ของอาวุธนั้น ๆ ใน `WeaponsList` หรือเพิ่มเข้าไปในลิสต์โดยตรง แทนการเขียนทับ Slot 0 เดิม
* ส่งผลให้ผู้เล่นสามารถเก็บปืนหลายกระบอกไว้ในกระเป๋า และแต่ละกระบอกรักษาจำนวนกระสุนในแม็กกาซีน (`currentMagazine`) ของตัวเองไว้อย่างถูกต้อง
