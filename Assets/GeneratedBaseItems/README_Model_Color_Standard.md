# มาตรฐานการใช้สีและ Material สำหรับสร้างโมเดล 3D (Chibi Tactical Standard) & บันทึกความคืบหน้า

เอกสารนี้รวบรวมมาตรฐานสี พารามิเตอร์ Material ใน Blender และ Unity URP (อ้างอิงจากโมเดลตัวละครหลัก `Chibi_TacticalGirl_Source.blend` / `Chibi_TacticalGirl_Rigged.prefab`) รวมถึงขั้นตอนการ Export FBX เพื่อไม่ให้โมเดลดูมันเงาเหมือนดินน้ำมัน และไม่ให้ลำดับ SubMesh / Material หลุดเป็นสีชมพู

---

## 1. กฎเหล็กของการตั้งค่า Material (ไม่ให้เงาเหมือนดินน้ำมัน)

เมื่อนำเข้าไฟล์ `.fbx` เข้าสู่ Unity (โดยตั้งค่า `materialLocation: 1` หรือ Embedded Materials):
1. **ห้ามแปลงค่าสี Linear -> sRGB ซ้ำตอนเขียนลง FBX**: ให้ใช้ค่าสี Linear RGB ใน Blender `Principled BSDF -> Base Color` ตามตารางด้านล่างโดยตรงเหมือนกับไฟล์ `Chibi_TacticalGirl_Source.blend`
2. **ห้ามปล่อยค่า Default `_Smoothness = 0.5` ของ Unity**: ค่า Default ของ FBX Importer จะทำให้พื้นผิวมีไฮไลท์ขาวมันวาวเหมือนดินน้ำมัน ต้องใช้ `AssetPostprocessor.OnPostprocessMaterial` (ใน `Assets/Scripts/Editor/ChibiArmPoseFixer.cs`) และ Runtime Fallback (ใน `BuildingObject.ApplyMatteMaterialFinish()`) บังคับค่าดังนี้:
   - **พื้นผิวด้านทั่วไป (ไม้, หิน, อิฐ, ปูน, สนิม, ผ้า, เชือก, กระดูก, ไคติน, สีเหลืองเตือนภัย, สีน้ำเงินทาเหล็ก)**:
     - `_Smoothness` / `_Glossiness` = **`0.08f`**
     - `_Metallic` = **`0.0f`**
     - `_SpecColor` = **`new Color(0.05f, 0.05f, 0.05f, 1f)`**
   - **เกราะเหล็กสีเทาเข้ม (`Titanium | graphite armor`, `Iron | dark forged steel`)**:
     - `_Smoothness` / `_Glossiness` = **`0.18f`**
     - `_Metallic` = **`0.08f`**
     - `_SpecColor` = **`new Color(0.05f, 0.05f, 0.05f, 1f)`**
   - **โลหะสีเทาเงิน (`Titanium | bright bevel`, `Iron | scraped edges`)**:
     - `_Smoothness` / `_Glossiness` = **`0.48f`**
     - `_Metallic` = **`0.70f`**
   - **กระจก / น้ำ / คริสตัลเรืองแสง (`Glass`, `Water`, `Plasma`, `Ember`)**:
     - `_Smoothness` = **`0.45f`**, `_Metallic` = **`0.0f`**
3. **ขอบ Bevel ของโมเดล**:
   - ใช้ `Bevel Modifier` แบบ **`segments = 1`** (`width = 0.005..0.008`, `angle_limit = 34°`) และ `mesh_smooth_type = "FACE"` เพื่อให้สันขอบคมชัดสไตล์ Chibi Arena ไม่มนย้วยเหมือนดินน้ำมัน

---

## 2. ตารางค่าสีมาตรฐาน (Blender Linear RGB & Unity FBX Material `fileID`)

ค่าสีหลักดึงตรงจากพาเลตของ `Chibi_TacticalGirl_Source.blend` (`Mat_Jacket_Gold`, `Mat_Armor_Grey`, `Mat_Cap_Black`, `Mat_Metal_Silver`, `Mat_Glove_Blue`, `Mat_Hair_Purple`):

| หมวดหมู่ | ชื่อ Material ใน Blender / FBX | Blender Base Color `(R, G, B)` | Roughness | Metallic | Unity Embedded `fileID` |
| :--- | :--- | :--- | :---: | :---: | :---: |
| **สีเหลืองตัวละคร** | `Hazard \| faded amber` | `(0.7484, 0.3931, 0.0272)` *(Mat_Jacket_Gold)* | `0.90` | `0.00` | `-482887956499609092` |
| **สีเทาเกราะเข้ม** | `Titanium \| graphite armor` | `(0.0550, 0.0637, 0.0890)` *(Mat_Armor_Grey)* | `0.78` | `0.08` | `7507402042184923473` |
| **สีเทาเหล็กหล่อเข้ม** | `Iron \| dark forged steel` | `(0.0380, 0.0440, 0.0620)` | `0.78` | `0.08` | `405695604403225985` |
| **สีดำถ่าน / หมวก** | `Charcoal \| blackened` | `(0.0220, 0.0245, 0.0331)` *(Mat_Cap_Black)* | `0.92` | `0.00` | `-7181685566173657166` |
| **สีเทาเงินสว่าง** | `Titanium \| bright bevel` | `(0.4770, 0.5382, 0.6383)` *(Mat_Metal_Silver)* | `0.45` | `0.55` | `-8589822895404907163` |
| **สีเทาเงินเหล็กขูด** | `Iron \| scraped edges` | `(0.3800, 0.4300, 0.5200)` | `0.45` | `0.55` | `1957219476845421901` |
| **สีน้ำเงินตัวละคร** | `Paint \| faded survival blue` | `(0.0331, 0.0946, 0.3000)` *(Mat_Glove_Blue)* | `0.88` | `0.00` | `6623978844445042594` |
| **สีสนิมส้ม** | `Rust \| orange oxide` | `(0.3200, 0.1200, 0.0400)` | `0.88` | `0.00` | `3205885576641329788` |
| **สีสนิมน้ำตาลเข้ม** | `Rust \| deep brown` | `(0.1600, 0.0600, 0.0250)` | `0.90` | `0.00` | `-1839816330221225562` |
| **ไม้ซีดาร์เก่า (สว่าง)** | `Wood \| weathered cedar` | `(0.3600, 0.2300, 0.1200)` | `0.88` | `0.00` | `8310209739754355863` |
| **ไม้เนื้อกลาง** | `Wood \| exposed grain` | `(0.2600, 0.1600, 0.0800)` | `0.88` | `0.00` | `3326320646306402449` |
| **ไม้เข้ม / ร่องเสี้ยนไม้** | `Wood \| charred and stained` | `(0.1200, 0.0700, 0.0350)` | `0.90` | `0.00` | `8431337906315347019` |
| **หน้าตัดหัวไม้** | `Wood \| cut end` | `(0.4500, 0.3000, 0.1600)` | `0.88` | `0.00` | `-7736619948960863613` |
| **หินเทาเข้ม** | `Stone \| charcoal gray` | `(0.0900, 0.1000, 0.1300)` | `0.88` | `0.00` | `7018494452378973129` |
| **หินเทากลาง** | `Stone \| worn facets` | `(0.2400, 0.2500, 0.2800)` | `0.86` | `0.00` | `-6874098081551650745` |
| **คอนกรีตสว่าง** | `Concrete \| broken aggregate` | `(0.3400, 0.3500, 0.3800)` | `0.86` | `0.00` | `-5430719123078995249` |
| **ปูนก่ออิฐ** | `Mortar \| pale sand` | `(0.1600, 0.1600, 0.1800)` | `0.90` | `0.00` | `7611822336396784311` |
| **อิฐดินเผา** | `Brick \| fired clay` | `(0.3800, 0.1400, 0.0700)` | `0.86` | `0.00` | `6938061359785430796` |
| **ผ้าใบเขียวทหาร** | `Cloth \| field canvas` | `(0.1000, 0.1600, 0.0700)` | `0.90` | `0.00` | `7954999086020926922` |
| **ผ้าใบเขียวเข้ม** | `Cloth \| patched canvas` | `(0.0600, 0.1000, 0.0400)` | `0.90` | `0.00` | `-2738131743881995424` |
| **หนังสีน้ำตาลเข้ม** | `Leather \| dark hide` | `(0.1500, 0.0700, 0.0300)` | `0.88` | `0.00` | `-2989397751443692843` |
| **เชือกป่าน** | `Rope \| plant fiber` | `(0.3200, 0.2200, 0.1000)` | `0.90` | `0.00` | `-2469832957612292858` |
| **กระดูก / เขี้ยว** | `Bone \| infected ivory` | `(0.5800, 0.5000, 0.3600)` | `0.88` | `0.00` | `-2938380724185884601` |
| **เกราะไคตินม่วงเข้ม** | `Chitin \| dark carapace` | `(0.1626, 0.0400, 0.2800)` *(Mat_Hair_Purple)* | `0.88` | `0.00` | `-1837847668371168799` |
| **เกราะไคตินม่วงสว่าง** | `Chitin \| olive ridges` | `(0.2800, 0.1000, 0.4200)` | `0.88` | `0.00` | `3077501789586785686` |
| **กระจกรมควัน** | `Glass \| dusty teal` | `(0.1200, 0.2800, 0.3400)` *(Alpha 0.55)* | `0.22` | `0.10` | `7487158002706050869` |
| **กระจกใส** | `Glass \| pale reflection` | `(0.3800, 0.5800, 0.6800)` *(Alpha 0.65)* | `0.18` | `0.10` | `4735867354007733578` |
| **น้ำสะอาดเรืองแสง** | `Water \| purified cyan` | `(0.1000, 0.4400, 0.6800)` *(Emission 0.45)* | `0.15` | `0.00` | `3691393181202530451` |
| **ไฟเรืองแสงสีส้ม** | `Ember \| orange glow` | `(0.8900, 0.2800, 0.0200)` *(Emission 2.4)* | `0.35` | `0.00` | `-2186898949452057549` |
| **พลาสมาคริสตัลฟ้า** | `Plasma \| xenocrystal cyan` | `(0.1400, 0.6400, 0.8800)` *(Emission 2.4)* | `0.20` | `0.00` | `2063695588891646619` |
| **น้ำดีพิษสีเขียว** | `Bile \| toxic phosphor` | `(0.2800, 0.5600, 0.0600)` *(Emission 1.2)* | `0.45` | `0.00` | `-5260649039624814096` |

---

## 3. ขั้นตอนการ Export FBX ให้ลำดับ Material และแกนตั้งตรงกับ Unity 100%

1. **Bake Modifier ก่อนจัดลำดับ Material**:
   - เมื่อใช้ `BEVEL` Modifier ใน Blender ลำดับ Polygon หลัง Evaluate จะเปลี่ยนไปจากตอนสร้าง BMesh
   - ต้อง Bake Evaluated Mesh ลงใน `obj.data` และลบ Modifier ออกก่อนเสมอ
2. **จัดเรียง `mesh.materials` ให้ตรงกับลำดับการปรากฏของ Polygon (`SLOT_ORDER == POLY_FIRST_APPEARANCE_ORDER`)**:
   - สแกน `mesh.polygons` ตามลำดับ `0..N-1` เพื่อเก็บลิสต์ Material ตามลำดับที่เจอครั้งแรก (`ordered_mats`)
   - **ข้อควรระวังใน Blender**: การเรียก `mesh.materials.clear()` จะรีเซ็ต `p.material_index` ของทุก Polygon เป็น `0` ทันที! ดังนั้นต้องเก็บ `new_poly_mat_indices` ไว้ในลิสต์ก่อน แล้วค่อยเขียน `p.material_index` คืนหลังจาก `mesh.materials.append(mat)` ครบแล้ว
3. **หมุนแกนพิกัด Vertex ให้ตั้งตรงใน Unity (`m_LocalRotation = Quaternion.identity`)**:
   - แปลงพิกัด Vertex จาก Blender Z-up เป็น `v.co = Vector((-co.x, co.z, co.y))` แล้ว Export ด้วย `axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False`

---

## 4. สรุปความคืบหน้าที่ดำเนินการเสร็จสิ้นแล้ว

1. **โมเดลสิ่งปลูกสร้าง 22 ชิ้น (`Assets/GeneratedBaseItems/` & `Assets/Resources_Addressable/Prefabs/Environment/Craft/`)**:
   - รีโมเดล 19 ชิ้น (`01–08`, `10–12`, `14–16`, `18–22`) ให้ตรงกับไอคอน 2D ใน `BUILDING1TO22.png` พร้อมลงดีเทลงานไม้เก่า ร่องเสี้ยนไม้ คราบสนิม และชิ้นส่วนกลไก 3D
   - คงโมเดลเดิมของ `09_Survivor_Campfire`, `13_Survivor_Sleeping_Bedroll`, และ `17_Razor_Wire_Barricade` ไว้ตามที่กำหนด
   - แก้ปัญหา `BUILD0002_WoodenSpikeBarricade.asset` ชื่อซ้ำกับ `Razor Wire Barricade` ทำให้สร้างลวดหนามได้โมเดลลวดหนาม (`17`) ถูกต้อง
   - รักษาค่า `m_LocalScale` ของผู้ใช้ครบทุกชิ้น (`0.7`, `1.4/0.7/0.7`, `0.5`, `1.0`)
2. **ระบบโต๊ะคราฟต์ (`Assets/Scripts/Runtime/Building/WorkbenchBuilding.cs`)**:
   - เปลี่ยนจากการวัดระยะห่างเป็นเมตร เป็นการเช็กว่ามีโต๊ะคราฟต์ที่ยังไม่พังอยู่ในแมพ (Stage `StageX, StageY`) เดียวกับผู้เล่นหรือไม่ (`IsInCurrentMap()`) หากอยู่แมพเดียวกันจะคราฟต์หมวด `Building` และ `Defense` ได้จากทุกจุดในแมพทันที
3. **โมเดลและอนิเมชันซอมบี้ (`Assets/GeneratedModels/Zombies/`)**:
   - ปรับท่าวิ่ง `Run` ของ `ZombieRunner` ให้มีความบิดเบี้ยวผิดรูป
   - สร้างโมเดล `Chibi_ZombieBoss_Rigged.fbx` แนวแฟรงเกนสไตน์ (แขนซ้ายใหญ่ผิดปกติ + แท่งเหล็ก 3 แท่งปักไหล่ซ้าย) พร้อมอนิเมชันครบ 10 ท่า (`Idle`, `Walk`, `Run`, `Slash1`, `Slash2`, `GroundSlam`, `Charge`, `Throw`, `RageRoar`, `Death`) และผูกเข้ากับ `ZombieLeader.prefab` และ `ZombieAI.cs`
   - สร้าง `Chibi_ZombieBoomer_Rigged.fbx` พร้อมกระดูก `Belly` (19 กระดูก) และท่า `Explode` พองท้องระเบิด
4. **โมเดลหุ่นยนต์ศัตรูครบทั้ง 8 ตัวแบบ Full-Robot พร้อม Rig + Animation (`Assets/GeneratedModels/Robots/Rigged/`)**:
   - ปรับดีไซน์ใหม่ทั้งหมดเป็นหุ่นยนต์เต็มตัว (มีทั้งแบบ Tripod, Quadruped Walker, Spider-Mine, Hover Eye-Drone และ Heavy Mech) ภายใต้โทนสี **Crimson Strike (แดงเข้ม-เทากราไฟต์-ดำถ่าน + ไฟแดง)** และ **Hazard Siege (เหลืองอุตสาหกรรม-เทากราไฟต์-ดำถ่าน + ไฟส้มแดง)** ตามมาตรฐานพื้นผิวด้าน
   - สร้างผ่าน `Tools/Create_Stylized_Robots_Rigged.py` ครบทั้ง 8 ตัว (`Robot_Normal`, `Robot_Blade`, `Robot_Drone`, `Robot_Gunner`, `Robot_Kamikaze`, `Robot_Laser`, `Robot_Tanker`, `Robot_Leader`) พร้อมสคริปต์ `Assets/Scripts/Editor/StylizedRobotModelBuilder.cs` สำหรับสร้าง AnimatorController และแทนที่โมเดลเดิมใน `Robot*.prefab` อัตโนมัติ และเชื่อม `PlayActionAnimation` ใน `RobotAI.cs`
