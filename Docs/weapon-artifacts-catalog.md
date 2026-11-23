# รายการ Artifact แต่งอาวุธทั้งหมด (Weapon Artifacts Master List)

**อัปเดตล่าสุด**: 24 พฤศจิกายน 2026  
**สถานะ**: สร้าง ItemData Asset จริง + ลงทะเบียนใน ItemDatabase.asset ครบทั้งหมด **234 Artifacts** แล้ว (19 อาวุธ × 12 ชิ้นเฉพาะตัว = 232 + 2 ชิ้น Universal ปืนทุกกระบอก, ไม่รวมระบบธาตุ พิษ/ไฟ/น้ำแข็ง/ช็อต)

## กติกาการติดตั้ง Artifact ต่ออาวุธ 1 ชิ้น (สูงสุด 4 ช่อง A1–A4)
- **Common**: ใส่ได้ **0–4 ใบ**
- **Rare**: ใส่ได้ **สูงสุด 2 ใบ (1–2 ใบ)**
- **Legendary**: ใส่ได้ **1 ใบเท่านั้น**

## สรุปจำนวน Artifact ของแต่ละอาวุธ (Tracker)

| # | อาวุธ (Weapon) | หมวดหมู่ | จำนวนที่มีแล้ว | สัดส่วน (C / R / L) | โฟลเดอร์ Asset |
|---|---|---|---|---|---|
| 1 | **Pistol** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/01_Pistol` |
| 2 | **Revolver** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/02_Revolver` |
| 3 | **SMG** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/03_SMG` |
| 4 | **Shotgun** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/04_Shotgun` |
| 5 | **Rifle** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/05_Rifle` |
| 6 | **Sniper Rifle** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/06_SniperRifle` |
| 7 | **Crossbow** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/07_Crossbow` |
| 8 | **Flamethrower** | Ranged | **13** (+2 Universal) | 4 Common / 6 Rare / 3 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/08_Flamethrower` |
| 9 | **Rocket Launcher** | Ranged | **12** (+2 Universal) | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/09_RocketLauncher` |
| 10 | **Knife** | Melee | **13** | 4 Common / 6 Rare / 3 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/10_Knife` |
| 11 | **Baseball Bat** | Melee | **13** | 4 Common / 6 Rare / 3 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/11_BaseballBat` |
| 12 | **Machete** | Melee | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/12_Machete` |
| 13 | **Fire Axe** | Melee | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/13_FireAxe` |
| 14 | **Katana** | Melee | **13** | 4 Common / 6 Rare / 3 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/14_Katana` |
| 15 | **Sledgehammer** | Melee | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/15_Sledgehammer` |
| 16 | **Survival Spear** | Melee | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/16_SurvivalSpear` |
| 17 | **Stun Baton** | Melee | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/17_StunBaton` |
| 18 | **Chainsaw** | Melee | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/18_Chainsaw` |
| 19 | **Grenade** | Throwing | **12** | 4 Common / 6 Rare / 2 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/19_Grenade` |
| + | **Universal Ranged (ปืนทุกชนิด)** | Ranged (All 9) | **2** | 0 Common / 2 Rare / 0 Legendary | `Assets/Resources_Addressable/Database/ItemData/Artifacts/00_Universal` |
| | **รวมทั้งหมด** | | **234 Artifacts** | **76 Common / 116 Rare / 42 Legendary** | |

---

### 1. Pistol (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART001_Pistol_01_HeavyHollowPoint` | Heavy Hollow Point | Common | เพิ่มพลังทำลายกระสุนปืนพก +12 (`ATK +12`) |
| 2 | `ART002_Pistol_02_HairTrigger` | Hair Trigger | Common | ลดแรงเหนี่ยวไก ยิงปืนพกไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART003_Pistol_03_ExtendedPistolMag` | Extended Pistol Mag | Common | ขยายแม็กกาซีนปืนพกเพิ่ม +8 นัด (`MAG +8`) |
| 4 | `ART004_Pistol_04_QuickReleaseLoader` | Quick-Release Loader | Common | ระบบปลดแม็กไว รีโหลดเร็วขึ้น +35% (`RLD +35%`) |
| 5 | `ART005_Pistol_05_TwinChamber` | Twin Chamber | Rare | ยิงกระสุนปืนพกออกทีละ 2 นัดพร้อมกัน (`Shot +1 (x2)`) |
| 6 | `ART006_Pistol_06_AutoSearMod` | Auto Sear Mod | Rare | ดัดแปลงปืนพกเป็นยิงรัวอัตโนมัติ (Full-Auto) (`Full-Auto, SPD +15%`) |
| 7 | `ART007_Pistol_07_TungstenTip` | Tungsten Tip | Rare | หัวกระสุนทังสเตนเจาะทะลุศัตรู (Pierce) (`Pierce, ATK +8`) |
| 8 | `ART008_Pistol_08_LaserSightCore` | Laser Sight Core | Rare | เลเซอร์เล็งจุดตาย เพิ่มระยะและคริติคอล +20% (`CRIT +20%, RNG +1.0`) |
| 9 | `ART009_Pistol_09_HeavyMagnumSlide` | Heavy Magnum Slide | Rare | เสริมสไลด์หนัก เพิ่มดาเมจ +45% และแรงกระแทก (`DMG +45%, KB +1.0`) |
| 10 | `ART010_Pistol_10_TacticalRecycler` | Tactical Recycler | Rare | โอกาส 30% ไม่เสียกระสุนเมื่อยิง + รีโหลดไว +25% (`Save 30%, RLD +25%`) |
| 11 | `ART011_Pistol_11_AkimboPhantom` | Akimbo Phantom | Legendary | ยิงออกทีละ 2 นัด + ยิงไว +30% + แม็ก +12 (`Shot +1, SPD +30%, MAG +12`) |
| 12 | `ART012_Pistol_12_DeadshotAce` | Deadshot Ace | Legendary | คริติคอล +35% แรงคริ +80% และเจาะทะลุศัตรู (`CRIT +35%, CDMG +80%, Pierce`) |

### 2. Revolver (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART013_Revolver_01_MagnumPowder` | Magnum Powder | Common | ดินปืนแรงสูงสำหรับลูกโม่ ดาเมจ +25 (`ATK +25`) |
| 2 | `ART014_Revolver_02_FanTheHammer` | Fan-The-Hammer | Common | ง้างนกสับไว ยิงลูกโม่เร็วขึ้น +30% (`SPD +30%`) |
| 3 | `ART015_Revolver_03_SpeedloaderDrum` | Speedloader Drum | Common | สปีดโหลดเดอร์ บรรจุโม่ไวขึ้น +45% (`RLD +45%`) |
| 4 | `ART016_Revolver_04_EightShotCylinder` | Eight-Shot Cylinder | Common | โม่รุ่นพิเศษเพิ่มความจุ +2 นัด (`MAG +2`) |
| 5 | `ART017_Revolver_05_ArmorPiercingSlug` | Armor-Piercing Slug | Rare | กระสุนเจาะเกราะทะลุศัตรู (Pierce) + ดาเมจ +20% (`Pierce, DMG +20%`) |
| 6 | `ART018_Revolver_06_TwinBarrelSplit` | Twin Barrel Split | Rare | ยิงลูกโม่ออกทีละ 2 นัดพร้อมกัน (`Shot +1 (x2)`) |
| 7 | `ART019_Revolver_07_HeavyImpactRound` | Heavy Impact Round | Rare | หัวกระสุนหนักเพิ่มแรงกระแทกและชะงัก (`ATK +18, KB +2, STAG +0.4`) |
| 8 | `ART020_Revolver_08_DeadeyeScope` | Deadeye Scope | Rare | กล้องเล็งระยะไกล คริ +25% และแรงคริ +50% (`CRIT +25%, CDMG +50%`) |
| 9 | `ART021_Revolver_09_CowboyRapidFan` | Cowboy Rapid Fan | Rare | เปลี่ยนลูกโม่เป็น Full-Auto + ยิงไว +45% (`Full-Auto, SPD +45%`) |
| 10 | `ART022_Revolver_10_LuckyChamber` | Lucky Chamber | Rare | โอกาส 35% ไม่เสียกระสุนในโม่ + ดาเมจ +25% (`Save 35%, DMG +25%`) |
| 11 | `ART023_Revolver_11_SixShooterOverkill` | Six-Shooter Overkill | Legendary | ดาเมจ +75% + แรงคริ +70% + เจาะทะลุ (`DMG +75%, CDMG +70%, Pierce`) |
| 12 | `ART024_Revolver_12_GunslingerLegend` | Gunslinger Legend | Legendary | ยิงออกทีละ 2 นัด + Full-Auto + รีโหลดไว +50% (`Shot +1, Auto, RLD +50%`) |

### 3. SMG (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART025_SMG_01_HighVelocityRounds` | High-Velocity Rounds | Common | กระสุนความเร็วสูงสำหรับ SMG ดาเมจ +8 (`ATK +8`) |
| 2 | `ART026_SMG_02_OverclockedBolt` | Overclocked Bolt | Common | ลูกเลื่อนเบา ยิง SMG รัวขึ้น +30% (`SPD +30%`) |
| 3 | `ART027_SMG_03_DrumMagazine` | Drum Magazine | Common | แม็กกลมขนาดใหญ่ +25 นัด (`MAG +25`) |
| 4 | `ART028_SMG_04_TacticalForegrip` | Tactical Foregrip | Common | กริปยุทธวิธี รีโหลดไวขึ้น +35% (`RLD +35%`) |
| 5 | `ART029_SMG_05_TwinNozzleSMG` | Twin Nozzle SMG | Rare | ปากกระบอกคู่ ยิงออกทีละ 2 นัดพร้อมกัน (`Shot +1 (x2)`) |
| 6 | `ART030_SMG_06_FMJPenetrator` | FMJ Penetrator | Rare | กระสุนเคลือบทองแดง เจาะทะลุศัตรู (Pierce) (`Pierce, ATK +5`) |
| 7 | `ART031_SMG_07_ExtendedCarbineKit` | Extended Carbine Kit | Rare | ชุดแต่งลำกล้องยาว เพิ่มระยะยิงและดาเมจ +20% (`DMG +20%, RNG +1.5`) |
| 8 | `ART032_SMG_08_ReflexCritOptic` | Reflex Crit Optic | Rare | กล้องจุดแดง โอกาสคริติคอล +25% (`CRIT +25%`) |
| 9 | `ART033_SMG_09_BulletHoseCore` | Bullet Hose Core | Rare | ยิงไว +50% + แม็ก +40 นัด ราดกระสุนไม่ยั้ง (`SPD +50%, MAG +40`) |
| 10 | `ART034_SMG_10_HollowShredder` | Hollow Shredder | Rare | ดาเมจ +35% + แรงคริติคอล +45% (`DMG +35%, CDMG +45%`) |
| 11 | `ART035_SMG_11_EndlessSpray` | Endless Spray | Legendary | โอกาส 40% ไม่เสียกระสุน + แม็ก +30 + ยิงไว +25% (`Save 40%, MAG +30, SPD +25%`) |
| 12 | `ART036_SMG_12_StormSweeper` | Storm Sweeper | Legendary | ยิงออกทีละ 2 นัด + เจาะทะลุ + ดาเมจ +30% (`Shot +1, Pierce, DMG +30%`) |

### 4. Shotgun (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART037_Shotgun_01_HeavyBuckshot` | Heavy Buckshot | Common | เม็ดลูกปรายใหญ่ขึ้น ดาเมจต่อเม็ด +6 (`ATK +6/pellet`) |
| 2 | `ART038_Shotgun_02_PumpAssist` | Pump Assist | Common | ปั๊มลูกไวขึ้น ยิงลูกซองเร็วขึ้น +25% (`SPD +25%`) |
| 3 | `ART039_Shotgun_03_TubeExtension` | Tube Extension | Common | ต่อหลอดกระสุนลูกซองเพิ่ม +4 นัด (`MAG +4`) |
| 4 | `ART040_Shotgun_04_ShellCarrier` | Shell Carrier | Common | แผงกระสุนข้างปืน รีโหลดไวขึ้น +40% (`RLD +40%`) |
| 5 | `ART041_Shotgun_05_ExtraPelletPack` | Extra Pellet Pack | Rare | เพิ่มจำนวนลูกปรายต่อนัด +3 เม็ด (`Pellets +3`) |
| 6 | `ART042_Shotgun_06_LongRangeChoke` | Long-Range Choke | Rare | ปลอกบีบกลุ่มกระสุน เพิ่มระยะยิง +1.2 และดาเมจ +20% (`DMG +20%, RNG +1.2`) |
| 7 | `ART043_Shotgun_07_SolidSlugMod` | Solid Slug Mod | Rare | กระสุนลูกโดดเจาะทะลุศัตรู (Pierce) + ดาเมจ +45% (`Pierce, DMG +45%`) |
| 8 | `ART044_Shotgun_08_AutoShotgunReceiver` | Auto-Shotgun Receiver | Rare | เปลี่ยนลูกซองเป็น Full-Auto + ยิงไว +20% (`Full-Auto, SPD +20%`) |
| 9 | `ART045_Shotgun_09_RiotBreacher` | Riot Breacher | Rare | แรงกระแทกและชะงักสูง + ดาเมจ +35% (`DMG +35%, KB +2.5, STAG +0.5`) |
| 10 | `ART046_Shotgun_10_ShellRecycler` | Shell Recycler | Rare | โอกาส 35% ไม่เสียลูกซองเมื่อยิง + แม็ก +3 (`Save 35%, MAG +3`) |
| 11 | `ART047_Shotgun_11_DoubleBarrelSalvo` | Double-Barrel Salvo | Legendary | ม่านกระสุนยักษ์เพิ่มลูกปราย +5 เม็ด + ดาเมจ +25% (`Pellets +5, DMG +25%`) |
| 12 | `ART048_Shotgun_12_DoomsweeperCore` | Doomsweeper Core | Legendary | Full-Auto + Pierce + ลูกปราย +3 + ยิงไว +35% (`Auto, Pierce, +3P, SPD +35%`) |

### 5. Rifle (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART049_Rifle_01_MatchGradeBarrel` | Match-Grade Barrel | Common | ลำกล้องมาตรฐานแข่ง ดาเมจไรเฟิล +15 (`ATK +15`) |
| 2 | `ART050_Rifle_02_GasBlockTuner` | Gas-Block Tuner | Common | ปรับแก๊สยิงไวขึ้น +22% (`SPD +22%`) |
| 3 | `ART051_Rifle_03_DualClampedMag` | Dual-Clamped Mag | Common | แม็กคู่ติดเทป ความจุ +15 นัด (`MAG +15`) |
| 4 | `ART052_Rifle_04_SleightofHandMag` | Sleight-of-Hand Mag | Common | เปลี่ยนซองไว รีโหลดเร็วขึ้น +35% (`RLD +35%`) |
| 5 | `ART053_Rifle_05_DoubleTapChamber` | Double-Tap Chamber | Rare | ยิงไรเฟิลออกทีละ 2 นัดพร้อมกัน (`Shot +1 (x2)`) |
| 6 | `ART054_Rifle_06_APSteelCore` | AP Steel Core | Rare | กระสุนแกนเหล็กเจาะทะลุศัตรู (Pierce) (`Pierce, ATK +10`) |
| 7 | `ART055_Rifle_07_MarksmanOptic` | Marksman Optic | Rare | กล้องเล็งระยะกลาง คริ +20% ระยะ +2.0 (`CRIT +20%, RNG +2.0`) |
| 8 | `ART056_Rifle_08_HeavyRecoilBuffer` | Heavy Recoil Buffer | Rare | ดาเมจ +25% + ยิงไวขึ้น +15% (`DMG +25%, SPD +15%`) |
| 9 | `ART057_Rifle_09_RampUpBarrel` | Ramp-Up Barrel | Rare | เร่งรอบลำกล้อง ยิงไว +45% + แม็ก +20 (`SPD +45%, MAG +20`) |
| 10 | `ART058_Rifle_10_AmmoConserver` | Ammo Conserver | Rare | โอกาส 35% ไม่เสียกระสุน + รีโหลดไว +30% (`Save 35%, RLD +30%`) |
| 11 | `ART059_Rifle_11_TriBurstRail` | Tri-Burst Rail | Legendary | ยิงออกทีละ 3 นัดทรงพัดแคบ + เจาะทะลุ (Pierce) (`Shot +2 (x3), Pierce`) |
| 12 | `ART060_Rifle_12_TacticalOverdrive` | Tactical Overdrive | Legendary | ดาเมจ +55% + คริ +25% + แรงคริ +50% + ยิงไว +25% (`DMG +55%, CRIT +25%, SPD +25%`) |

### 6. Sniper Rifle (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART061_SniperRifle_01_HighCaliberBolt` | High-Caliber Bolt | Common | กระสุนลำกล้องใหญ่ ดาเมจสไนเปอร์ +45 (`ATK +45`) |
| 2 | `ART062_SniperRifle_02_StraightPullBolt` | Straight-Pull Bolt | Common | ลูกเลื่อนดึงตรง ยิงสไนเปอร์ไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART063_SniperRifle_03_ExtendedSniperMag` | Extended Sniper Mag | Common | แม็กสไนเปอร์ยาวขึ้น +4 นัด (`MAG +4`) |
| 4 | `ART064_SniperRifle_04_PrecisionLoader` | Precision Loader | Common | ระบบบรรจุไว รีโหลดเร็วขึ้น +35% (`RLD +35%`) |
| 5 | `ART065_SniperRifle_05_TwinShotChamber` | Twin-Shot Chamber | Rare | ยิงสไนเปอร์ออกทีละ 2 ลูกพร้อมกัน! (`Shot +1 (x2)`) |
| 6 | `ART066_SniperRifle_06_RailgunPiercer` | Railgun Piercer | Rare | เจาะทะลุศัตรูทุกตัวเป็นเส้นตรง + ระยะ +3.0 (`Pierce, RNG +3.0, DMG +20%`) |
| 7 | `ART067_SniperRifle_07_AutoSniperReceiver` | Auto-Sniper Receiver | Rare | เปลี่ยนสไนเปอร์เป็น Full-Auto + ยิงไว +35% (`Full-Auto, SPD +35%`) |
| 8 | `ART068_SniperRifle_08_HeadshotAmplifier` | Headshot Amplifier | Rare | เสริมคริติคอล +25% และตัวคูณคริ +70% (`CRIT +25%, CDMG +70%`) |
| 9 | `ART069_SniperRifle_09_AntiMaterielRound` | Anti-Materiel Round | Rare | กระสุนเจาะรถถัง ดาเมจ +60% + เจาะทะลุ (`DMG +60%, Pierce`) |
| 10 | `ART070_SniperRifle_10_GhostMagazine` | Ghost Magazine | Rare | โอกาส 40% ไม่เสียกระสุน + แม็ก +5 (`Save 40%, MAG +5`) |
| 11 | `ART071_SniperRifle_11_TripleLineRail` | Triple-Line Rail | Legendary | ยิงออกทีละ 3 ลูก + เจาะทะลุ (Pierce) + ดาเมจ +30% (`Shot +2 (x3), Pierce, +30%`) |
| 12 | `ART072_SniperRifle_12_OneShotSovereign` | One-Shot Sovereign | Legendary | ดาเมจ +100% + แรงคริ +100% + เจาะทะลุ (`DMG +100%, CDMG +100%, Pierce`) |

### 7. Crossbow (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART073_Crossbow_01_ReinforcedLimb` | Reinforced Limb | Common | คันหน้าไม้เสริมแรง ดาเมจ +25 (`ATK +25`) |
| 2 | `ART074_Crossbow_02_QuickDrawPulley` | Quick-Draw Pulley | Common | รอกดึงสายไว ยิงเร็วขึ้น +25% (`SPD +25%`) |
| 3 | `ART075_Crossbow_03_RepeatingQuiver` | Repeating Quiver | Common | ซองบรรจุลูกธนูอัตโนมัติ +5 ดอก (`MAG +5`) |
| 4 | `ART076_Crossbow_04_AutoWinchCrank` | Auto-Winch Crank | Common | รอกหมุนขึ้นสายไว รีโหลดเร็วขึ้น +40% (`RLD +40%`) |
| 5 | `ART077_Crossbow_05_SplitBoltFork` | Split-Bolt Fork | Rare | รางคู่ ยิงลูกธนูออกทีละ 2 ดอกพร้อมกัน (`Shot +1 (x2)`) |
| 6 | `ART078_Crossbow_06_SkewerBolt` | Skewer Bolt | Rare | หัวธนูเหล็กแหลม เจาะทะลุศัตรู (Pierce) (`Pierce, DMG +20%`) |
| 7 | `ART079_Crossbow_07_RepeaterTrigger` | Repeater Trigger | Rare | เปลี่ยนหน้าไม้เป็น Full-Auto ยิงรัวต่อเนื่อง (`Full-Auto, SPD +30%`) |
| 8 | `ART080_Crossbow_08_HunterScope` | Hunter Scope | Rare | กล้องพรานป่า คริ +25% แรงคริ +60% (`CRIT +25%, CDMG +60%`) |
| 9 | `ART081_Crossbow_09_TriSpreadBow` | Tri-Spread Bow | Rare | ยิงลูกธนูออกทีละ 3 ดอกเป็นพัด (มุม 10°) (`Shot +2 (x3)`) |
| 10 | `ART082_Crossbow_10_BoltRecycler` | Bolt Recycler | Rare | โอกาส 50% ยิงแล้วไม่เสียลูกธนู (Bolts) (`Save 50%, RLD +25%`) |
| 11 | `ART083_Crossbow_11_SiegeBallistaCore` | Siege Ballista Core | Legendary | ดาเมจ +85% + เจาะทะลุ + แรงคริ +60% (`DMG +85%, Pierce, CDMG +60%`) |
| 12 | `ART084_Crossbow_12_HuntersVolley` | Hunter's Volley | Legendary | ยิงออกทีละ 5 ดอกพัดกว้าง + เจาะทะลุทุกดอก! (`Shot +4 (x5), Pierce`) |

### 8. Flamethrower (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART085_Flamethrower_01_HighDensityFuel` | High-Density Fuel | Common | เชื้อเพลิงอัดแน่น ดาเมจ +12 (`ATK +12`) |
| 2 | `ART086_Flamethrower_02_HighPressureValve` | High-Pressure Valve | Common | วาล์วแรงดันสูง พ่นไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART087_Flamethrower_03_PressurizedTank` | Pressurized Tank | Common | ถังอัดแรงดันใหญ่ ความจุ +35 หน่วย (`MAG +35`) |
| 4 | `ART088_Flamethrower_04_QuickSwapCanister` | Quick-Swap Canister | Common | ถังเปลี่ยนเร็ว รีโหลดไวขึ้น +40% (`RLD +40%`) |
| 5 | `ART089_Flamethrower_05_WideConeNozzle` | Wide-Cone Nozzle | Rare | หัวฉีดกว้าง พ่นออกทีละ +2 สาย (`Streams +2`) |
| 6 | `ART090_Flamethrower_06_LongJetNozzle` | Long-Jet Nozzle | Rare | หัวฉีดแรงดันไกล ระยะพ่นไกลขึ้น +2.0 (`RNG +2.0, DMG +15%`) |
| 7 | `ART091_Flamethrower_07_PiercingJetCore` | Piercing Jet Core | Rare | ลำพ่นทะลุผ่านฝูงศัตรูทุกตัว (Pierce) (`Pierce, ATK +8`) |
| 8 | `ART092_Flamethrower_08_FuelSaverRegulator` | Fuel Saver Regulator | Rare | ลดการสิ้นเปลือง โอกาส 40% ไม่เสียเชื้อเพลิง (`Save 40%`) |
| 9 | `ART093_Flamethrower_09_TurboInjector` | Turbo Injector | Rare | ดาเมจ +45% + พ่นไวขึ้น +30% (`DMG +45%, SPD +30%`) |
| 10 | `ART094_Flamethrower_10_HeavyCompressionTank` | Heavy Compression Tank | Rare | ความจุ +60 หน่วย + รีโหลดไว +35% (`MAG +60, RLD +35%`) |
| 11 | `ART095_Flamethrower_11_OverpressureSurge` | Overpressure Surge | Legendary | ดาเมจ +70% + ทะลุศัตรู + ระยะ +2.5 (`DMG +70%, Pierce, RNG +2.5`) |
| 12 | `ART096_Flamethrower_12_HellstormNozzle` | Hellstorm Nozzle | Legendary | พ่นออก +3 สาย + ทะลุศัตรู + ดาเมจ +35% + ประหยัด 30% (`Streams +3, Pierce, +35%`) |

### 9. Rocket Launcher (Ranged) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART097_RocketLauncher_01_HighExplosiveHead` | High-Explosive Head | Common | หัวรบแรงสูง ดาเมจจรวด +60 (`ATK +60`) |
| 2 | `ART098_RocketLauncher_02_FastCycleTube` | Fast-Cycle Tube | Common | ท่อยิงรอบไว ยิงจรวดเร็วขึ้น +25% (`SPD +25%`) |
| 3 | `ART099_RocketLauncher_03_QuadPodMagazine` | Quad-Pod Magazine | Common | กระบอกบรรจุหลายลูก แม็ก +3 ลูก (`MAG +3`) |
| 4 | `ART100_RocketLauncher_04_AutoLoaderRack` | Auto-Loader Rack | Common | ระบบโหลดอัตโนมัติ รีโหลดไวขึ้น +40% (`RLD +40%`) |
| 5 | `ART101_RocketLauncher_05_TwinSalvoPod` | Twin Salvo Pod | Rare | ยิงจรวดออกทีละ 2 ลูกพร้อมกัน! (`Shot +1 (x2)`) |
| 6 | `ART102_RocketLauncher_06_BunkerBuster` | Bunker Buster | Rare | หัวรบเจาะบังเกอร์ ดาเมจ +45% (`DMG +45%`) |
| 7 | `ART103_RocketLauncher_07_BoosterThruster` | Booster Thruster | Rare | เพิ่มระยะพุ่ง +3.0 และยิงไวขึ้น +20% (`RNG +3.0, SPD +20%`) |
| 8 | `ART104_RocketLauncher_08_WarheadOptimizer` | Warhead Optimizer | Rare | โอกาสคริติคอล +30% และแรงคริ +50% (`CRIT +30%, CDMG +50%`) |
| 9 | `ART105_RocketLauncher_09_MunitionsRecycler` | Munitions Recycler | Rare | โอกาส 35% ยิงจรวดไม่เสียกระสุน + แม็ก +2 (`Save 35%, MAG +2`) |
| 10 | `ART106_RocketLauncher_10_TripleSalvoRack` | Triple Salvo Rack | Rare | ยิงจรวดออกทีละ 3 ลูกพร้อมกัน (มุม 12°) (`Shot +2 (x3)`) |
| 11 | `ART107_RocketLauncher_11_BarrageLauncher` | Barrage Launcher | Legendary | Full-Auto + แม็ก +4 + ยิงไว +40% + รีโหลด +35% (`Auto, MAG +4, SPD +40%`) |
| 12 | `ART108_RocketLauncher_12_TacticalNukeCore` | Tactical Nuke Core | Legendary | ดาเมจ +120% + ยิงจรวดออกทีละ 2 ลูก! (`DMG +120%, Shot +1 (x2)`) |

### 10. Knife (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART109_Knife_01_RazorHonedEdge` | Razor Honed Edge | Common | ลับคมมีดพิเศษ ดาเมจ +15 (`ATK +15`) |
| 2 | `ART110_Knife_02_FeatherweightGrip` | Featherweight Grip | Common | ด้ามเบาพิเศษ ฟันมีดไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART111_Knife_03_ExtendedTang` | Extended Tang | Common | ต่อใบมีดยาวขึ้น ระยะฟัน +0.6m (`RNG +0.6m`) |
| 4 | `ART112_Knife_04_SerratedTeeth` | Serrated Teeth | Common | ฟันเลื่อยเพิ่มแรงชะงักและแรงผลัก (`ATK +10, KB +2, STAG +0.3`) |
| 5 | `ART113_Knife_05_AssassinsEdge` | Assassin's Edge | Rare | โอกาสคริติคอล +30% + แรงคริ +40% (`CRIT +30%, CDMG +40%`) |
| 6 | `ART114_Knife_06_CombatTantoMod` | Combat Tanto Mod | Rare | ดาเมจ +30% + ระยะฟัน +0.5m (`DMG +30%, RNG +0.5m`) |
| 7 | `ART115_Knife_07_TwinSlashBalance` | Twin-Slash Balance | Rare | ฟันไวขึ้น +40% (`SPD +40%`) |
| 8 | `ART116_Knife_08_VampiricFang` | Vampiric Fang | Rare | ฟันโดนศัตรูดูดเลือดคืน +4 HP ต่อครั้ง (`Leech +4 HP`) |
| 9 | `ART117_Knife_09_ShadowBladeCore` | Shadow Blade Core | Rare | ดาเมจ +45% + ฟันไวขึ้น +30% (`DMG +45%, SPD +30%`) |
| 10 | `ART118_Knife_10_BloodthirstHilt` | Bloodthirst Hilt | Rare | ดูดเลือด +7 HP ต่อครั้ง + คริ +20% (`Leech +7 HP, CRIT +20%`) |
| 11 | `ART119_Knife_11_PhantomFlurry` | Phantom Flurry | Legendary | ฟันไว +65% + ระยะ +0.9m + ดาเมจ +30% (`SPD +65%, RNG +0.9m, +30%`) |
| 12 | `ART120_Knife_12_CoupDeGrace` | Coup De Grace | Legendary | ดาเมจ +80% + คริ +45% + แรงคริ +90% (`DMG +80%, CRIT +45%, +90%`) |

### 11. Baseball Bat (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART121_BaseballBat_01_BarbedWireWrap` | Barbed Wire Wrap | Common | พันลวดหนามรอบไม้เบสบอล ดาเมจ +18 (`ATK +18`) |
| 2 | `ART122_BaseballBat_02_CorkedCore` | Corked Core | Common | ไม้เบาพิเศษ หวดไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART123_BaseballBat_03_LongSluggerShaft` | Long-Slugger Shaft | Common | ด้ามยาวพิเศษ ระยะหวด +0.7m (`RNG +0.7m`) |
| 4 | `ART124_BaseballBat_04_WeightedTip` | Weighted Tip | Common | ถ่วงหัวไม้ แรงกระแทก +5 และชะงัก +0.4 วิ (`KB +5, STAG +0.4s`) |
| 5 | `ART125_BaseballBat_05_SteelStuds` | Steel Studs | Rare | ตอกหมุดเหล็ก ดาเมจ +30% + คริ +15% (`DMG +30%, CRIT +15%`) |
| 6 | `ART126_BaseballBat_06_ConcussionSwing` | Concussion Swing | Rare | หวดมึนงง สตั๊น +0.8 วิ + แรงกระแทก +6 (`STAG +0.8s, KB +6`) |
| 7 | `ART127_BaseballBat_07_WideCleaveArc` | Wide Cleave Arc | Rare | ระยะหวด +1.1m ฟาดโดนหลายตัวพร้อมกัน (`RNG +1.1m`) |
| 8 | `ART128_BaseballBat_08_SawbladeStuds` | Sawblade Studs | Rare | ติดใบเลื่อยข้างไม้ ดาเมจ +30 + แรงคริ +40% (`ATK +30, CDMG +40%`) |
| 9 | `ART129_BaseballBat_09_HomeRunImpact` | Home-Run Impact | Rare | ดาเมจ +45% + แรงกระแทก +10 ปลิวทั้งแถว (`DMG +45%, KB +10`) |
| 10 | `ART130_BaseballBat_10_BrawlersGrit` | Brawler's Grit | Rare | หวดโดนศัตรูฟื้นเลือด +5 HP + หวดไว +25% (`Leech +5 HP, SPD +25%`) |
| 11 | `ART131_BaseballBat_11_GrandSlamCore` | Grand Slam Core | Legendary | ดาเมจ +75% + คริ +30% + สตั๊น +1.0 วิ (`DMG +75%, CRIT +30%, STAG +1s`) |
| 12 | `ART132_BaseballBat_12_BattersFury` | Batter's Fury | Legendary | ดาเมจ +55% + ระยะ +1.3m + หวดไว +35% (`DMG +55%, RNG +1.3m, SPD +35%`) |

### 12. Machete (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART133_Machete_01_CarbonSteelBlade` | Carbon Steel Blade | Common | ใบมีดเหล็กคาร์บอน ดาเมจ +22 (`ATK +22`) |
| 2 | `ART134_Machete_02_BalancedHilt` | Balanced Hilt | Common | ถ่วงด้ามสมดุล ฟันไวขึ้น +24% (`SPD +24%`) |
| 3 | `ART135_Machete_03_JungleReach` | Jungle Reach | Common | ใบมีดพร้ายาวขึ้น ระยะฟัน +0.8m (`RNG +0.8m`) |
| 4 | `ART136_Machete_04_HeavyCleaverSpine` | Heavy Cleaver Spine | Common | สันมีดหนา เพิ่มดาเมจ +22% และชะงัก +0.3 วิ (`DMG +22%, STAG +0.3s`) |
| 5 | `ART137_Machete_05_ButchersRhythm` | Butcher's Rhythm | Rare | จังหวะสับต่อเนื่อง ฟันไวขึ้น +38% (`SPD +38%`) |
| 6 | `ART138_Machete_06_GoreCleave` | Gore Cleave | Rare | ระยะฟัน +1.2m กวาดฝูงกว้างขึ้น (`RNG +1.2m, ATK +15`) |
| 7 | `ART139_Machete_07_KeenRazorEdge` | Keen Razor Edge | Rare | โอกาสคริติคอล +25% + แรงคริ +45% (`CRIT +25%, CDMG +45%`) |
| 8 | `ART140_Machete_08_HeavyChopWeight` | Heavy Chop Weight | Rare | ดาเมจ +35% + แรงกระแทก +4 (`DMG +35%, KB +4`) |
| 9 | `ART141_Machete_09_BloodFrenzy` | Blood Frenzy | Rare | ฟันโดนศัตรูดูดเลือดคืน +6 HP + ฟันไว +20% (`Leech +6 HP, SPD +20%`) |
| 10 | `ART142_Machete_10_TemperedSawBack` | Tempered Saw-Back | Rare | ดาเมจ +45% + สตั๊น +0.5 วิ (`DMG +45%, STAG +0.5s`) |
| 11 | `ART143_Machete_11_ExecutionersArc` | Executioner's Arc | Legendary | ดาเมจ +70% + ระยะ +1.3m + แรงคริ +60% (`DMG +70%, RNG +1.3m, +60%`) |
| 12 | `ART144_Machete_12_EndlessCarnage` | Endless Carnage | Legendary | ฟันไว +55% + ดูดเลือด +6 HP + ดาเมจ +35% (`SPD +55%, Leech +6, +35%`) |

### 13. Fire Axe (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART145_FireAxe_01_ForgedAxeHead` | Forged Axe Head | Common | หัวขวานตีเหล็กกล้า ดาเมจ +30 (`ATK +30`) |
| 2 | `ART146_FireAxe_02_FiberglassHandle` | Fiberglass Handle | Common | ด้ามไฟเบอร์น้ำหนักเบา เหวี่ยงไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART147_FireAxe_03_LongHaftReach` | Long-Haft Reach | Common | ด้ามขวานยาวพิเศษ ระยะฟัน +0.9m (`RNG +0.9m`) |
| 4 | `ART148_FireAxe_04_ArmorSplitter` | Armor Splitter | Common | ผ่าเกราะหนัก ดาเมจ +28% (`DMG +28%`) |
| 5 | `ART149_FireAxe_05_SkullCracker` | Skull Cracker | Rare | ดาเมจ +35% + สตั๊น +0.7 วิ (`DMG +35%, STAG +0.7s`) |
| 6 | `ART150_FireAxe_06_CleavingSweep` | Cleaving Sweep | Rare | ระยะฟัน +1.4m กวาดรอบตัวกว้างขึ้น (`RNG +1.4m`) |
| 7 | `ART151_FireAxe_07_BreachersMomentum` | Breacher's Momentum | Rare | เหวี่ยงขวานไวขึ้น +38% + แรงผลัก +4 (`SPD +38%, KB +4`) |
| 8 | `ART152_FireAxe_08_SplittingCritEdge` | Splitting Crit Edge | Rare | โอกาสคริติคอล +25% + แรงคริ +55% (`CRIT +25%, CDMG +55%`) |
| 9 | `ART153_FireAxe_09_TimberShockwave` | Timber Shockwave | Rare | ระยะ +1.6m + แรงกระแทก +8 + สตั๊น +0.8 วิ (`RNG +1.6m, KB +8, STAG +0.8s`) |
| 10 | `ART154_FireAxe_10_BerserkersHeart` | Berserker's Heart | Rare | จามโดนศัตรูดูดเลือด +8 HP + ดาเมจ +30% (`Leech +8 HP, DMG +30%`) |
| 11 | `ART155_FireAxe_11_GuillotineStrike` | Guillotine Strike | Legendary | ดาเมจ +90% + แรงคริ +70% (`DMG +90%, CDMG +70%`) |
| 12 | `ART156_FireAxe_12_TitanChopper` | Titan Chopper | Legendary | ดาเมจ +55% + ระยะ +1.6m + เหวี่ยงไว +35% (`DMG +55%, RNG +1.6m, SPD +35%`) |

### 14. Katana (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART157_Katana_01_TamahaganeFold` | Tamahagane Fold | Common | ตีเหล็กพับชั้น ดาเมจคาตานะ +28 (`ATK +28`) |
| 2 | `ART158_Katana_02_QuickDrawSaya` | Quick-Draw Saya | Common | ฝักดาบลื่น ฟันไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART159_Katana_03_OdachiLengthening` | Odachi Lengthening | Common | ยืดใบดาบโอตาจิ ระยะฟัน +1.0m (`RNG +1.0m`) |
| 4 | `ART160_Katana_04_KeenEdgePolish` | Keen Edge Polish | Common | ลับคมระดับกระจก โอกาสคริติคอล +22% (`CRIT +22%`) |
| 5 | `ART161_Katana_05_WindCutterBlade` | Wind-Cutter Blade | Rare | คลื่นดาบลมไกลขึ้น ระยะฟัน +1.6m (`RNG +1.6m, ATK +18`) |
| 6 | `ART162_Katana_06_LightningDraw` | Lightning Draw | Rare | ชักดาบสายฟ้า ฟันไวขึ้น +42% (`SPD +42%`) |
| 7 | `ART163_Katana_07_IaijutsuFocus` | Iaijutsu Focus | Rare | คริติคอล +25% + แรงคริติคอล +65% (`CRIT +25%, CDMG +65%`) |
| 8 | `ART164_Katana_08_RoninSteelSpine` | Ronin Steel Spine | Rare | ดาเมจ +35% + ชะงัก +0.4 วิ (`DMG +35%, STAG +0.4s`) |
| 9 | `ART165_Katana_09_MoonlitIaido` | Moonlit Iaido | Rare | ดาเมจ +55% + ฟันไว +30% (`DMG +55%, SPD +30%`) |
| 10 | `ART166_Katana_10_SoulSiphon` | Soul Siphon | Rare | ฟันโดนศัตรูดูดเลือด +7 HP + ระยะ +0.8m (`Leech +7 HP, RNG +0.8m`) |
| 11 | `ART167_Katana_11_ThousandCuts` | Thousand Cuts | Legendary | ฟันไว +60% + ระยะ +1.5m + ดาเมจ +35% (`SPD +60%, RNG +1.5m, +35%`) |
| 12 | `ART168_Katana_12_MuramasaCurse` | Muramasa Curse | Legendary | ดาเมจ +100% + คริ +40% + แรงคริ +80% (`DMG +100%, CRIT +40%, +80%`) |

### 15. Sledgehammer (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART169_Sledgehammer_01_LeadFilledHead` | Lead-Filled Head | Common | หัวค้อนถ่วงตะกั่ว ดาเมจ +40 (`ATK +40`) |
| 2 | `ART170_Sledgehammer_02_AlloyShaft` | Alloy Shaft | Common | ด้ามอัลลอยด์เบา เหวี่ยงค้อนไวขึ้น +30% (`SPD +30%`) |
| 3 | `ART171_Sledgehammer_03_GiantReach` | Giant Reach | Common | ด้ามยักษ์ ระยะทุบ +1.0m (`RNG +1.0m`) |
| 4 | `ART172_Sledgehammer_04_SeismicImpact` | Seismic Impact | Common | แรงกระแทกแผ่นดินไหว สตั๊น +1.0 วิ + ผลัก +6 (`STAG +1.0s, KB +6`) |
| 5 | `ART173_Sledgehammer_05_ArmorCrusher` | Armor Crusher | Rare | บดขยี้เกราะ ดาเมจ +45% (`DMG +45%`) |
| 6 | `ART174_Sledgehammer_06_TremorWave` | Tremor Wave | Rare | คลื่นแผ่นดินไหว ระยะทุบ +1.7m (`RNG +1.7m`) |
| 7 | `ART175_Sledgehammer_07_PistonRocketHead` | Piston Rocket Head | Rare | ติดลูกสูบดันหัวค้อน เหวี่ยงไว +42% + แรงผลัก +8 (`SPD +42%, KB +8`) |
| 8 | `ART176_Sledgehammer_08_SkullbreakerCrit` | Skullbreaker Crit | Rare | โอกาสคริ +25% + แรงคริ +60% (`CRIT +25%, CDMG +60%`) |
| 9 | `ART177_Sledgehammer_09_MeteorSlam` | Meteor Slam | Rare | ดาเมจ +65% + แรงกระแทก +10 + สตั๊น +0.8 วิ (`DMG +65%, KB +10, STAG +0.8s`) |
| 10 | `ART178_Sledgehammer_10_JuggernautVigor` | Juggernaut Vigor | Rare | ทุบโดนศัตรูดูดเลือด +10 HP + ดาเมจ +25% (`Leech +10 HP, DMG +25%`) |
| 11 | `ART179_Sledgehammer_11_EarthquakeCore` | Earthquake Core | Legendary | ระยะทุบ +2.2m + ดาเมจ +50% + สตั๊น +1.4 วิ (`RNG +2.2m, DMG +50%, +1.4s`) |
| 12 | `ART180_Sledgehammer_12_TitanOverdrive` | Titan Overdrive | Legendary | ดาเมจ +95% + เหวี่ยงไว +38% + แรงผลัก +12 (`DMG +95%, SPD +38%, KB +12`) |

### 16. Survival Spear (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART181_SurvivalSpear_01_ForgedSpearhead` | Forged Spearhead | Common | หัวหอกเหล็กกล้า ดาเมจ +22 (`ATK +22`) |
| 2 | `ART182_SurvivalSpear_02_RapidThrustGrip` | Rapid Thrust Grip | Common | ด้ามจับกันลื่น แทงไวขึ้น +28% (`SPD +28%`) |
| 3 | `ART183_SurvivalSpear_03_PikeExtension` | Pike Extension | Common | ต่อด้ามหอกยาว ระยะแทง +1.2m (`RNG +1.2m`) |
| 4 | `ART184_SurvivalSpear_04_BarbedHookTip` | Barbed Hook Tip | Common | เงี่ยงหอกเกี่ยวศัตรู ชะงัก +0.5 วิ + แรงผลัก +4 (`STAG +0.5s, KB +4`) |
| 5 | `ART185_SurvivalSpear_05_SkewerThrust` | Skewer Thrust | Rare | ระยะแทง +1.6m + ดาเมจ +25% (`RNG +1.6m, DMG +25%`) |
| 6 | `ART186_SurvivalSpear_06_HalberdSideBlade` | Halberd Side-Blade | Rare | เสริมใบขวานข้างหัวหอก ดาเมจ +35% + คริ +15% (`DMG +35%, CRIT +15%`) |
| 7 | `ART187_SurvivalSpear_07_PhalanxBrace` | Phalanx Brace | Rare | ผลักศัตรูถอยห่าง แรงกระแทก +8 + ระยะ +0.8m (`KB +8, RNG +0.8m`) |
| 8 | `ART188_SurvivalSpear_08_WhirlwindThrust` | Whirlwind Thrust | Rare | แทงรัวไวขึ้น +42% (`SPD +42%`) |
| 9 | `ART189_SurvivalSpear_09_DragonSlayerTip` | Dragon-Slayer Tip | Rare | หัวหอกล่าบอส ดาเมจ +60% + แรงคริ +50% (`DMG +60%, CDMG +50%`) |
| 10 | `ART190_SurvivalSpear_10_ValkyrieSiphon` | Valkyrie Siphon | Rare | แทงโดนศัตรูดูดเลือด +5 HP + แทงไว +25% (`Leech +5 HP, SPD +25%`) |
| 11 | `ART191_SurvivalSpear_11_LonginusReach` | Longinus Reach | Legendary | ระยะแทงไกลสุดขีด +2.4m + ดาเมจ +55% (`RNG +2.4m, DMG +55%`) |
| 12 | `ART192_SurvivalSpear_12_SpartanBarrage` | Spartan Barrage | Legendary | แทงไว +60% + ดูดเลือด +5 HP + คริ +30% (`SPD +60%, Leech +5, CRIT +30%`) |

### 17. Stun Baton (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART193_StunBaton_01_HeavyCoreRod` | Heavy Core Rod | Common | แกนเหล็กถ่วงน้ำหนัก ดาเมจกระบอง +18 (`ATK +18`) |
| 2 | `ART194_StunBaton_02_RapidBatonDrill` | Rapid Baton Drill | Common | ฝึกจังหวะฟาด ฟาดไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART195_StunBaton_03_TelescopicRod` | Telescopic Rod | Common | กระบองยืดหด ระยะฟาด +0.8m (`RNG +0.8m`) |
| 4 | `ART196_StunBaton_04_ImpactCapacitor` | Impact Capacitor | Common | แรงกระแทกมึนงง สตั๊นศัตรูเพิ่มขึ้น +0.9 วิ (`STAG +0.9s`) |
| 5 | `ART197_StunBaton_05_RiotSweeper` | Riot Sweeper | Rare | ระยะฟาด +1.3m + แรงผลัก +6 (`RNG +1.3m, KB +6`) |
| 6 | `ART198_StunBaton_06_KineticSurge` | Kinetic Surge | Rare | ดาเมจ +35% + ฟาดไวขึ้น +22% (`DMG +35%, SPD +22%`) |
| 7 | `ART199_StunBaton_07_PrecisionStrikeTip` | Precision Strike Tip | Rare | ตีจุดอ่อน คริติคอล +28% + แรงคริ +50% (`CRIT +28%, CDMG +50%`) |
| 8 | `ART200_StunBaton_08_LockdownBaton` | Lockdown Baton | Rare | สตั๊น +1.2 วิ + ดาเมจ +20% (`STAG +1.2s, DMG +20%`) |
| 9 | `ART201_StunBaton_09_EnforcerCombo` | Enforcer Combo | Rare | ฟาดไว +45% + ดาเมจ +30% (`SPD +45%, DMG +30%`) |
| 10 | `ART202_StunBaton_10_TacticalRecovery` | Tactical Recovery | Rare | ฟาดโดนศัตรูดูดเลือด +6 HP + สตั๊น +0.6 วิ (`Leech +6 HP, STAG +0.6s`) |
| 11 | `ART203_StunBaton_11_RiotMasterCore` | Riot Master Core | Legendary | ดาเมจ +75% + สตั๊น +1.5 วิ + แรงกระแทก +9 (`DMG +75%, STAG +1.5s, KB +9`) |
| 12 | `ART204_StunBaton_12_HyperPulseOverdrive` | Hyper-Pulse Overdrive | Legendary | ฟาดไว +55% + ระยะ +1.5m + คริ +35% (`SPD +55%, RNG +1.5m, +35%`) |

### 18. Chainsaw (Melee) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART205_Chainsaw_01_DiamondTipChain` | Diamond-Tip Chain | Common | โซ่ฟันเพชร ดาเมจเลื่อย +14 ต่อติ๊ก (`ATK +14`) |
| 2 | `ART206_Chainsaw_02_HighRPMCarburetor` | High-RPM Carburetor | Common | คาร์บูเรเตอร์แต่ง รอบตัดไวขึ้น +25% (`SPD +25%`) |
| 3 | `ART207_Chainsaw_03_LongGuideBar` | Long Guide Bar | Common | บาร์เลื่อยยาวพิเศษ ระยะตัด +0.9m (`RNG +0.9m`) |
| 4 | `ART208_Chainsaw_04_HeavyFlywheel` | Heavy Flywheel | Common | ฟลายวีลหนัก เพิ่มแรงชะงักและแรงกระแทก (`STAG +0.4s, KB +3`) |
| 5 | `ART209_Chainsaw_05_RipSawTeeth` | Rip-Saw Teeth | Rare | ฟันเลื่อยฉีกเนื้อ ดาเมจ +35% (`DMG +35%`) |
| 6 | `ART210_Chainsaw_06_TurboExhaust` | Turbo Exhaust | Rare | ท่อเทอร์โบ ความเร็วรอบตัด +42% (`SPD +42%`) |
| 7 | `ART211_Chainsaw_07_ShredderReach` | Shredder Reach | Rare | บาร์กว้างพิเศษ ระยะตัด +1.4m กวาดฝูงด้านหน้า (`RNG +1.4m`) |
| 8 | `ART212_Chainsaw_08_PrecisionTeeth` | Precision Teeth | Rare | โอกาสคริติคอล +25% + แรงคริ +45% (`CRIT +25%, CDMG +45%`) |
| 9 | `ART213_Chainsaw_09_BloodFueledEngine` | Blood-Fueled Engine | Rare | เลื่อยโดนศัตรูดูดเลือดคืน +3 HP ต่อติ๊ก (`Leech +3 HP/hit`) |
| 10 | `ART214_Chainsaw_10_ArmorGrinder` | Armor Grinder | Rare | ฟันเจาะเกราะ ดาเมจ +55% + ชะงัก +0.5 วิ (`DMG +55%, STAG +0.5s`) |
| 11 | `ART215_Chainsaw_11_DoomRipper` | Doom Ripper | Legendary | ดาเมจ +70% + รอบตัด +38% + ระยะ +1.3m (`DMG +70%, SPD +38%, RNG +1.3`) |
| 12 | `ART216_Chainsaw_12_UnstoppableRampage` | Unstoppable Rampage | Legendary | ดูดเลือด +5 HP + รอบตัด +55% + ดาเมจ +45% (`Leech +5, SPD +55%, +45%`) |

### 19. Grenade (Throwing) — 12 Artifacts
| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|
| 1 | `ART217_Grenade_01_C4CompoundFill` | C4 Compound Fill | Common | อัดดินระเบิด C4 ดาเมจระเบิด +45 (`ATK +45`) |
| 2 | `ART218_Grenade_02_QuickPullPin` | Quick-Pull Pin | Common | สลักดึงไว ขว้างระเบิดเร็วขึ้น +30% (`SPD +30%`) |
| 3 | `ART219_Grenade_03_AeroShellCasing` | Aero-Shell Casing | Common | ปลอกอากาศพลศาสตร์ ขว้างไกลขึ้น +2.5 (`RNG +2.5`) |
| 4 | `ART220_Grenade_04_ExtraShrapnel` | Extra Shrapnel | Common | อัดสะเก็ดระเบิดเพิ่ม ดาเมจ +28% + สตั๊น (`DMG +28%, STAG +0.6s`) |
| 5 | `ART221_Grenade_05_TwinTossRig` | Twin Toss Rig | Rare | ขว้างระเบิดออกทีละ 2 ลูกพร้อมกัน! (`Throw +1 (x2)`) |
| 6 | `ART222_Grenade_06_ClusterBomblets` | Cluster Bomblets | Rare | ขว้างระเบิดออกทีละ 3 ลูกเป็นพัด (15°) (`Throw +2 (x3)`) |
| 7 | `ART223_Grenade_07_ConcussionCharge` | Concussion Charge | Rare | แรงอัดกระแทกหนัก สตั๊น +1.5 วิ + แรงผลัก +8 (`STAG +1.5s, KB +8`) |
| 8 | `ART224_Grenade_08_PrecisionFuze` | Precision Fuze | Rare | ชนวนแม่นยำ โอกาสคริติคอล +30% + แรงคริ +50% (`CRIT +30%, CDMG +50%`) |
| 9 | `ART225_Grenade_09_RapidGrenadier` | Rapid Grenadier | Rare | เปลี่ยนเป็น Full-Auto ขว้างรัวต่อเนื่องไว +45% (`Full-Auto, SPD +45%`) |
| 10 | `ART226_Grenade_10_HeavyFragCore` | Heavy Frag Core | Rare | สะเก็ดระเบิดหนัก ดาเมจ +60% + แม็ก +4 (`DMG +60%, MAG +4`) |
| 11 | `ART227_Grenade_11_TacticalRecycler` | Tactical Recycler | Legendary | โอกาส 45% ขว้างแล้วไม่เสียระเบิด + ขว้างไว +25% (`Save 45%, SPD +25%`) |
| 12 | `ART228_Grenade_12_CarpetBombing` | Carpet Bombing | Legendary | ขว้างออกทีละ 4 ลูกพร้อมกัน + ดาเมจ +50%! (`Throw +3 (x4), DMG +50%`) |

### 20. Universal Ranged Artifacts (ใส่ได้กับปืนทุกชนิดทั้ง 9 กระบอก)
| # | Asset ID | ชื่อ Artifact | ระดับ | อาวุธที่ใส่ได้ | ความสามารถจริงในเกม |
|---|---|---|---|---|---|
| 1 | `ART229_Universal_01_DoubleTapTrigger` | Double-Tap Trigger | Rare | ปืนทุกชนิด (All 9 Ranged Weapons) | กดยิง 1 ที ยิงซ้ำออก 2 ทีติดกัน! (ใส่ได้กับปืนทุกชนิด) (`Burst +1 (1 Click = 2 Shots)`) |
| 2 | `ART230_Universal_02_HeavyCaliberShell` | Heavy Caliber Shell | Rare | ปืนทุกชนิด (All 9 Ranged Weapons) | ขยายขนาดกระสุนและ Hitbox ใหญ่ขึ้นเป็น 2.5 เท่า ยิงศัตรูตัวเล็กโดนง่ายมาก! (ใส่ได้กับปืนทุกชนิด) (`Size x2.5 (Huge Hitbox)`) |

### 21. Extra Weapon-Specific Legendary Active Skill Artifacts
| # | Asset ID | อาวุธ | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |
|---|---|---|---|---|---|
| 1 | `ART231_Flamethrower_13_InfernoFireballCore` | Flamethrower | Inferno Fireball Core | Legendary | สกิลคลิกขวา (CD 5 วิ): ชาร์จ 2 วิ ใช้แก๊ส 10 หน่วย ยิงบอลเพลิงพุ่งตรง 20m ดาเมจ 8.5x ทะลุทุกตัว! (`RMB (5s CD): 2s Charge Fireball (10 Gas, 8.5x, 20m)`) |
| 2 | `ART232_Knife_13_KnifeHook` | Knife | Knife Hook | Legendary | สกิลคลิกขวา (CD 5 วิ): ขว้างมีดติดเชือกใส่ศัตรูข้างหน้าแล้วดึงพุ่งกลับมาหาเรา! (ยกเว้นบอส) (`RMB (5s CD): Knife Hook & Pull (No Boss)`) |
| 3 | `ART233_BaseballBat_13_BatBoomerang` | Baseball Bat | Bat Boomerang | Legendary | สกิลคลิกขวา (CD 5 วิ): ขว้างไม้เบสบอลหมุนวนเป็นวงกลมข้างหน้า (ระยะตี x2.5) ฟาดกระแทกศัตรูทุกตัว! (`RMB (5s CD): Circular Bat Boomerang (Range x2.5)`) |
| 4 | `ART234_Katana_13_RipperCombo` | Katana | RipperCombo | Legendary | สกิลคลิกขวา (CD 5 วิ): ฟันคาตานะต่อเนื่องเป็นชุด 3 Hit สุดรุนแรง! (`RMB (5s CD): 3-Hit RipperCombo`) |

