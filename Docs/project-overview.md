# เอกสารสรุปโครงสร้างโปรเจกต์ (Project Overview & Systems Guide)

เอกสารฉบับนี้จัดทำขึ้นเพื่อสรุปภาพรวมระบบการทำงานทั้งหมดของโปรเจกต์ **"The Last Empire"** เพื่อให้ผู้พัฒนาใหม่ ทีมงาน หรือผู้ที่เข้ามาอ่านสามารถทำความเข้าใจโครงสร้าง สถาปัตยกรรม วิธีการทำงานของโค้ดแต่ละส่วน และแนวทางการพัฒนาต่อยอดได้อย่างสะดวกรวดเร็ว

---

## 📂 1. ภาพรวมของเกม (Core Gameplay Loop)
**The Last Empire** เป็นเกมแนว Action Survival มุมมองแบบ Top-Down 3D 
*   **การสำรวจ (Exploration):** ผู้เล่นจะเดินทางสลับห้องผ่านประตูพอร์ทัล 4 ทิศทาง (เหนือ, ใต้, ออก, ตก) บนแผนที่กริดที่เกิดจากการสุ่มสร้างแบบไดนามิก
*   **การเอาชีวิตรอด (Survival):** ตัวละครผู้เล่นมีความหิว (Hunger) ที่ลดลงเรื่อยๆ ตามเวลา (การวิ่งเร็วจะลดหิวคูณสอง) ต้องกินขนมปังเพื่อประทังชีวิต และมีค่าพลังชีวิต (Health) ที่ต้องฟื้นฟูด้วยน้ำยาโพชั่น
*   **การต่อสู้ (Combat):** ต่อสู้กับฝูงซอมบี้ด้วยอาวุธปืน (ยิงกระสุนจำกัดตามแมกกาซีน ต้องคอยรีโหลดกระสุนจากเป้) หรือใช้อาวุธระยะประชิดฟันสกัดผลักศัตรูให้กระเด็นชะงัก
*   **เศรษฐกิจ (Economy):** กำจัดซอมบี้เพื่อเก็บเงิน ฟาร์มไอเทม หรือเอาของเหลือใช้ไปขายให้ NPC พ่อค้าที่ราคากลาง 30% แล้วนำเงินไปซื้อปืน, กระสุน หรือโพชั่นเพื่อเตรียมพร้อมลุยห้องต่อไป

---

## 🛠️ 2. ระบบหลักในปัจจุบันและการทำงาน (Core Systems & Mechanics)

```mermaid
graph TD
    Player[Player/Controller] --> |Consumes| Inventory[Player Inventory]
    Player --> |Attacks| Combat[Melee / Ranged Combat]
    Player --> |Triggers Warp| Portal[Transition Portal]
    Portal --> |Regenerates Room| MapManager[World Map Manager]
    MapManager --> |Spawns| Env[Environment Manager]
    Combat --> |Kills Enemies| Loot[Loot Dropper]
    Loot --> |Adds Loot| Inventory
    NPC[NPC Merchant] <--> |Buy / Sell Items| Inventory
```

### **2.1 ระบบควบคุมตัวละครและการเอาชีวิตรอด (Player Controller & Survival)**
*   **ไฟล์หลัก:** [PlayerController.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerController.cs), [PlayerInventory.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Player/PlayerInventory.cs), [Health.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/Health.cs)
*   **การเคลื่อนไหว:** รองรับทิศทาง WASD / จอยคอนโทรลเลอร์
*   **การวิ่งเร็ว (Sprinting):** กด `Shift` ค้าง เพิ่มความเร็ว 1.5 เท่า แต่แลกมาด้วยอัตราความหิวลดฮวบคูณ 2
*   **การแดชหลบหลีก (Dashing):** กด `Spacebar` พุ่งตัวหลบหลีก พร้อมสถานะอมตะชั่วขณะ (I-Frames)
*   **ความหิวและพลังชีวิต (Hunger & Health):** พลังชีวิตห้ามเหลือ 0 ความหิวที่ลดลงจนสุดจะค่อยๆ ลดค่าพลังชีวิตลงแทน ฟื้นฟูด้วยไอเทมจำพวกโพชั่นและขนมปัง

### **2.2 ระบบแผนที่และการสลับฉาก (Procedural Map & Portal Transition)**
*   **ไฟล์หลัก:** [WorldMapManager.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/WorldMapManager.cs), [TransitionPortal.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Map/TransitionPortal.cs)
*   **ตารางห้องแผนที่ (Grid Map):** โลกถูกแบ่งเป็นตารางขนาด $n \times n$ ทุกครั้งที่วาร์ปเข้าด่านใหม่ ข้อมูลห้องและสิ่งกีดขวางจะถูกสร้างขึ้นด้วย Stage Seed สำหรับด่านนั้นๆ เสมอ
*   **สลักประตูทางเชื่อม (Portal Shifting):** ประตูพอร์ทัลจะสลับตำแหน่งสุ่มแกนรองตาม Seed เสมอ เพื่อลดความจำเจในการเดิน
*   **ความปลอดภัยในการเปิดเส้นทาง (Portal Visibility):** ประตูจะปิดและซ่อนตัวเองลงอัตโนมัติ (ปิด MeshRenderer/Collider) หากด้านข้างไม่มีห้องข้างเคียงอยู่จริง ป้องกันตัวละครเดินตกฉาก
*   **แอนิเมชั่นเปลี่ยนผ่าน (DOTween Transition):** ตอนผู้เล่นแตะพอร์ทัล ฉากจะถูกบดบังด้วยภาพสีดำ (Fade In) -> สลับฉากเทเลพอร์ตผู้เล่น -> ค้างหน้าจอดำไว้ 0.5 วินาที -> ภาพค่อยๆ จางออก (Fade Out) และสั่งปิดการทำงานตัววาร์ป เพื่อความนุ่มนวลของสายตา

### **2.3 ระบบการต่อสู้ (Combat System)**
*   **อาวุธปืน (Ranged Combat):** 
    *   ต้องมีกระสุนปืนตรงรุ่นอยู่ในกระเป๋าเป้ เมื่อกดรีโหลด (`R`) ระบบจะหักกระสุนจากเป้มาใส่ในแมกกาซีน
    *   กระสุนถูกยิงออกจากกระป๋องเก็บพูลวัตถุ (Object Pool) เพื่อประหยัดทรัพยากร
    *   ความยาวกระสุนคำนวณสัมพันธ์กับ `Attack Radius` (ระยะเมตรของปืน) หารด้วยความเร็วกระสุนจริง
*   **อาวุธระยะประชิด (Melee Combat):**
    *   ตัวละครมีมีดติดตัวตอนเริ่ม สามารถเก็บอาวุธอื่น เช่น ไม้เบสบอล หรือมาเชเต้มาสวมใส่ทดแทนได้
    *   การฟันจะสร้างขอบเขตกลม (OverlapSphere) ด้านหน้า ตรวจจับศัตรูทั้งหมดที่อยู่ในรัศมี ทำการสร้างดาเมจ ผลักกระเด็น (Knockback) และชะงัก (Stagger) ตามสถิติอาวุธ
*   **สูตรดาเมจโจมตี:**
    $$\text{ดาเมจสุทธิ} = \text{ดาเมจพื้นฐานของตัวละคร (10)} + \text{โบนัสดาเมจของอาวุธนั้น ๆ}$$

### **2.4 ระบบคลังและร้านค้าซื้อ-ขาย (Inventory & Shop UI)**
*   **ไฟล์หลัก:** [ShopUI.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/ShopUI.cs), [InventoryUI.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/UI/InventoryUI.cs)
*   **สไตล์หน้าจอ:** ใช้ Slick Dark Mode กึ่งกระจกเบลอ (Glassmorphism) สวยงามสะดุดตา
*   **คลังกระเป๋าเป้ (Inventory):** กดเปิดด้วยปุ่ม `I` หรือ `ESC` ตัวเกมจะหยุดเวลาทันที (`Time.timeScale = 0`) แสดงไอเทมที่ซ้อนทับกัน, ดาเมจอาวุธ, และปุ่มกดกิน/ใช้งานได้ทันที
*   **แท็บซื้อและขาย (Buy & Sell Tabs):** 
    *   **BUY:** แสดงไอเทมที่พ่อค้ามีขายโดยใช้ราคาเริ่มต้นจากไฟล์กลาง `ShopDatabase`
    *   **SELL:** ดึงไอเทมในกระเป๋าเป้ผู้เล่นมาแสดงผลเพื่อแลกเปลี่ยนเป็นเงินสดในอัตราราคากลาง **30%** (ปุ่มขายของติดตัวที่สวมใส่อยู่จะขึ้นสีล็อกเตือน ห้ามกดขาย)
    *   **โหมดสว่างตรวจสอบฟรี (Is Free):** ตั้งค่าที่ NPC ตัวนั้นๆ ราคาสินค้าจะเป็น `$0` ทันทีเพื่อความสะดวกรวดเร็วในการเทสเกม

### **2.5 ระบบควบคุมไอเทมดรอป (Loot Table System)**
*   **ไฟล์หลัก:** [LootDropper.cs](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Scripts/Runtime/Combat/LootDropper.cs)
*   **การตั้งค่าอิสระ (Custom Loot Table):** สามารถกดแอดรายการดรอปใน Inspector ของซอมบี้แต่ละพรีแฟบได้อย่างอิสระ:
    *   ระบุชื่อไอเทมดรอป (เช่น Potion, Bread, Pistol Ammo, หรือคีย์เวิร์ดพิเศษ `"Money"` สำหรับสุ่มเงินสด)
    *   ระบุจำนวนต่ำสุด-สูงสุด
    *   ระบุเปอร์เซ็นต์โอกาสดรอป (`0.0` ถึง `1.0` หรือ 0%-100%)
*   **กลไกการสุ่ม:** ใช้หลักการหมุนค่าความน่าจะเป็นอิสระทุกชิ้น ทำให้การตาย 1 ครั้งของซอมบี้สามารถมีไอเทมดรอปตกลงมามากกว่า 1 ชิ้นพร้อมกันได้

---

## 🧠 3. สถาปัตยกรรมทางโค้ดที่สำคัญ (Technical Highlights)
1.  **การล้างหน่วยความจำเมื่อ Retry (Clean Scene Reloading):**
    *   เมื่อโหลดด่านใหม่ผ่านการ Retry `ObjectPoolManager` จะดักจับอีเวนต์ `sceneLoaded` ทำการกวาดทำลาย (Destroy) วัตถุศัตรู/กระสุนเดิมที่ยังค้างอยู่ในฉากจากรอบการเล่นที่แล้วทั้งหมด และสร้างพูลออฟเจกต์เริ่มต้นใหม่ทันที เพื่อเลี่ยงบั๊ก AI เป้าหมายเสีย
2.  **การดึงข้อมูลฐานข้อมูลแบบยืดหยุ่น (Recursive Resources Load):**
    *   ใช้เมธอด `Resources.LoadAll<T>("")` เพื่อทำดัชนีและดักจับไอเทมทั้งหมดที่อยู่นอกลิสต์ดาต้าเบส หรืออยู่ในโฟลเดอร์ลึกๆ อัตโนมัติ ทำให้ผู้พัฒนาปรับเปลี่ยนโครงสร้างโฟลเดอร์ได้โดยไม่เกิดปัญหา Code Break

---

## 📝 4. แผนงานพัฒนาต่อยอดในอนาคต (Planned Features Roadmap)
*   **ระบบค่าสถานะตัวละคร (Player Stats System):** เพิ่มค่าประสบการณ์ เลเวลอัพตัวละคร และนำแต้มไปอัพเกรดสเตตัสหลัก (เช่น ความอึด, ดาเมจพื้นฐาน, ความเร็วในการหลบหลีก)
*   **ระบบปรับปรุงคุณภาพอาวุธ (Weapon Upgrades):** ตู้คอนเทนเนอร์อัพเกรดปืนเพื่อเพิ่มพลังดาเมจและลดระยะเวลารีโหลดกระสุน
*   **เพิ่มประเภทสิ่งกีดขวางและกับดัก (Hazards & Trap Barriers):** บล็อกหนาม, ท่อนซุงแกว่ง หรือหลุมพิษที่สร้างดาเมจแบบ Real-time ให้ผู้เล่นในด่าน
*   **เพิ่มประเภทของปืนใหม่ (New Weapon Classes):** อาวุธไฟพ่น (Flame Thrower) หรืออาวุธระเบิดวงกว้าง (Rocket Launcher)
*   **บอสอารีน่า (Boss Arenas):** ปิดทางออกของพอร์ทัลบังคับเผชิญหน้ากับซอมบี้ระดับบอสยักษ์ก่อนเคลียร์ Biome เพื่อสลับไปสู่ Biome ระดับความยากถัดไป
