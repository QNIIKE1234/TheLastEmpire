param(
    [string]$ProjectRoot = "d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire"
)

$ErrorActionPreference = "Stop"

# Deterministic Unity 32-hex GUID generator
function Get-DeterministicGuid([string]$seed) {
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes("TheLastEmpire::WeaponArtifact::$seed")
    $hash = $md5.ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

function Write-FolderMeta([string]$folderPath, [string]$seed) {
    $metaPath = "$folderPath.meta"
    if (Test-Path $metaPath) { return }
    $guid = Get-DeterministicGuid "folder::$seed"
    $meta = @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    [System.IO.File]::WriteAllText($metaPath, $meta, (New-Object System.Text.UTF8Encoding($false)))
}

function Write-AssetMeta([string]$assetPath, [string]$guid) {
    $metaPath = "$assetPath.meta"
    $meta = @"
fileFormatVersion: 2
guid: $guid
NativeFormatImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    [System.IO.File]::WriteAllText($metaPath, $meta, (New-Object System.Text.UTF8Encoding($false)))
}

# Icon pool from existing sprite sheets in the project (all verified valid fileID + guid pairs)
$Icons = @{
    bolt     = "{fileID: 21300048, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Hydraulic Piston Bolt
    chip     = "{fileID: 21300049, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Basic Logic Chip
    oil      = "{fileID: 21300050, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Synthetic Machine Oil
    cap      = "{fileID: 21300051, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # High-Capacity Capacitor
    titanium = "{fileID: 21300052, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Reinforced Titanium Plate
    neural   = "{fileID: 21300053, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Advanced Neural Processor
    fusion   = "{fileID: 21300054, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Miniature Fusion Reactor
    servo    = "{fileID: 21300055, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Military Overdrive Servo
    chitin   = "{fileID: 21300056, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Chitin Carapace Shard
    stinger  = "{fileID: 21300060, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Prismatic Stinger
    crystal  = "{fileID: 21300064, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Overlord Xenocrystal Core
    grav     = "{fileID: 21300065, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Gravitational Bio-Organ
    nails    = "{fileID: 21300070, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Box of Nails
    tape     = "{fileID: 21300071, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Duct Tape
    pipe     = "{fileID: 21300072, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Scrap Steel Pipe
    powder   = "{fileID: 21300074, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Gunpowder Mix
    spring   = "{fileID: 21300087, guid: f2056d7bac6c5924185ef5ba29212f07, type: 3}" # Spring Steel Coil
    heart    = "{fileID: 21300044, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Pulsing Heart (Lifesteal)
    skull    = "{fileID: 21300045, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Titan Skull Crest
    fang     = "{fileID: 21300039, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Fang
    claw     = "{fileID: 21300038, guid: 87705a9622bd7a74390009e00f6674f6, type: 3}" # Claw
}

# Rarity colors (Common=0, Rare=1, Epic=2, Legendary=3)
$RarityColors = @{
    0 = @{ r = 0.85; g = 0.88; b = 0.92 } # Common (Silver)
    1 = @{ r = 0.28; g = 0.78; b = 1.00 } # Rare (Sky Blue)
    2 = @{ r = 0.78; g = 0.38; b = 1.00 } # Epic (Purple)
    3 = @{ r = 1.00; g = 0.78; b = 0.18 } # Legendary (Gold)
}
$RarityNames = @{ 0 = "Common"; 1 = "Rare"; 2 = "Epic"; 3 = "Legendary" }

# Helper to define an artifact entry cleanly (NO elemental status effects!)
function Art(
    [string]$name, [int]$rarity, [string]$iconKey, [string]$desc, [string]$short,
    [double]$dmg = 0, [double]$dmgPct = 0, [int]$pellets = 0, [double]$spread = 0,
    [double]$rate = 0, [int]$mag = 0, [double]$reload = 0, [double]$range = 0,
    [int]$pierce = 0, [int]$auto = 0, [double]$crit = 0, [double]$critDmg = 0,
    [double]$kb = 0, [double]$stag = 0, [double]$leech = 0, [double]$save = 0,
    [int]$burst = 0, [double]$scale = 0
) {
    return [ordered]@{
        name = $name; rarity = $rarity; icon = $Icons[$iconKey]; desc = $desc; short = $short
        dmg = $dmg; dmgPct = $dmgPct; pellets = $pellets; spread = $spread
        rate = $rate; mag = $mag; reload = $reload; range = $range
        pierce = $pierce; auto = $auto; crit = $crit; critDmg = $critDmg
        kb = $kb; stag = $stag; leech = $leech; save = $save; burst = $burst; scale = $scale
    }
}

# ALL 19 WEAPONS x 12 ARTIFACTS = 228 ARTIFACTS (Zero elemental effects — 100% live combat mechanics!)
$Weapons = @(
    [ordered]@{ weapon = "Pistol"; folder = "01_Pistol"; category = "Ranged"; list = @(
        (Art "Heavy Hollow Point"    0 "powder"   "เพิ่มพลังทำลายกระสุนปืนพก +12"                         "ATK +12"                      -dmg 12)
        (Art "Hair Trigger"          0 "spring"   "ลดแรงเหนี่ยวไก ยิงปืนพกไวขึ้น +25%"                     "SPD +25%"                     -rate 0.25)
        (Art "Extended Pistol Mag"   0 "tape"     "ขยายแม็กกาซีนปืนพกเพิ่ม +8 นัด"                        "MAG +8"                       -mag 8)
        (Art "Quick-Release Loader"  0 "oil"      "ระบบปลดแม็กไว รีโหลดเร็วขึ้น +35%"                     "RLD +35%"                     -reload 0.35)
        (Art "Twin Chamber"          1 "pipe"     "ยิงกระสุนปืนพกออกทีละ 2 นัดพร้อมกัน"                   "Shot +1 (x2)"                 -pellets 1 -spread 4)
        (Art "Auto Sear Mod"         1 "chip"     "ดัดแปลงปืนพกเป็นยิงรัวอัตโนมัติ (Full-Auto)"           "Full-Auto, SPD +15%"          -auto 1 -rate 0.15)
        (Art "Tungsten Tip"          1 "titanium" "หัวกระสุนทังสเตนเจาะทะลุศัตรู (Pierce)"                "Pierce, ATK +8"               -pierce 1 -dmg 8)
        (Art "Laser Sight Core"      1 "neural"   "เลเซอร์เล็งจุดตาย เพิ่มระยะและคริติคอล +20%"            "CRIT +20%, RNG +1.0"          -crit 0.20 -range 1.0)
        (Art "Heavy Magnum Slide"    2 "bolt"     "เสริมสไลด์หนัก เพิ่มดาเมจ +45% และแรงกระแทก"            "DMG +45%, KB +1.0"            -dmgPct 0.45 -kb 1.0)
        (Art "Tactical Recycler"     2 "cap"      "โอกาส 30% ไม่เสียกระสุนเมื่อยิง + รีโหลดไว +25%"       "Save 30%, RLD +25%"           -save 0.30 -reload 0.25)
        (Art "Akimbo Phantom"        3 "servo"    "ยิงออกทีละ 2 นัด + ยิงไว +30% + แม็ก +12"              "Shot +1, SPD +30%, MAG +12"   -pellets 1 -spread 4.5 -rate 0.30 -mag 12)
        (Art "Deadshot Ace"          3 "crystal"  "คริติคอล +35% แรงคริ +80% และเจาะทะลุศัตรู"            "CRIT +35%, CDMG +80%, Pierce" -crit 0.35 -critDmg 0.80 -pierce 1)
    )},
    [ordered]@{ weapon = "Revolver"; folder = "02_Revolver"; category = "Ranged"; list = @(
        (Art "Magnum Powder"         0 "powder"   "ดินปืนแรงสูงสำหรับลูกโม่ ดาเมจ +25"                    "ATK +25"                      -dmg 25)
        (Art "Fan-The-Hammer"        0 "spring"   "ง้างนกสับไว ยิงลูกโม่เร็วขึ้น +30%"                    "SPD +30%"                     -rate 0.30)
        (Art "Speedloader Drum"      0 "oil"      "สปีดโหลดเดอร์ บรรจุโม่ไวขึ้น +45%"                     "RLD +45%"                     -reload 0.45)
        (Art "Eight-Shot Cylinder"   0 "bolt"     "โม่รุ่นพิเศษเพิ่มความจุ +2 นัด"                         "MAG +2"                       -mag 2)
        (Art "Armor-Piercing Slug"   1 "titanium" "กระสุนเจาะเกราะทะลุศัตรู (Pierce) + ดาเมจ +20%"         "Pierce, DMG +20%"             -pierce 1 -dmgPct 0.20)
        (Art "Twin Barrel Split"     1 "pipe"     "ยิงลูกโม่ออกทีละ 2 นัดพร้อมกัน"                        "Shot +1 (x2)"                 -pellets 1 -spread 3.5)
        (Art "Heavy Impact Round"    1 "nails"    "หัวกระสุนหนักเพิ่มแรงกระแทกและชะงัก"                    "ATK +18, KB +2, STAG +0.4"    -dmg 18 -kb 2.0 -stag 0.4)
        (Art "Deadeye Scope"         1 "neural"   "กล้องเล็งระยะไกล คริ +25% และแรงคริ +50%"               "CRIT +25%, CDMG +50%"         -crit 0.25 -critDmg 0.50 -range 1.5)
        (Art "Cowboy Rapid Fan"      2 "servo"    "เปลี่ยนลูกโม่เป็น Full-Auto + ยิงไว +45%"              "Full-Auto, SPD +45%"          -auto 1 -rate 0.45)
        (Art "Lucky Chamber"         2 "cap"      "โอกาส 35% ไม่เสียกระสุนในโม่ + ดาเมจ +25%"              "Save 35%, DMG +25%"           -save 0.35 -dmgPct 0.25)
        (Art "Six-Shooter Overkill"  3 "skull"    "ดาเมจ +75% + แรงคริ +70% + เจาะทะลุ"                   "DMG +75%, CDMG +70%, Pierce"  -dmgPct 0.75 -critDmg 0.70 -pierce 1)
        (Art "Gunslinger Legend"     3 "crystal"  "ยิงออกทีละ 2 นัด + Full-Auto + รีโหลดไว +50%"          "Shot +1, Auto, RLD +50%"      -pellets 1 -spread 3.0 -auto 1 -reload 0.50 -mag 4)
    )},
    [ordered]@{ weapon = "SMG"; folder = "03_SMG"; category = "Ranged"; list = @(
        (Art "High-Velocity Rounds"  0 "powder"   "กระสุนความเร็วสูงสำหรับ SMG ดาเมจ +8"                  "ATK +8"                       -dmg 8)
        (Art "Overclocked Bolt"      0 "spring"   "ลูกเลื่อนเบา ยิง SMG รัวขึ้น +30%"                     "SPD +30%"                     -rate 0.30)
        (Art "Drum Magazine"         0 "tape"     "แม็กกลมขนาดใหญ่ +25 นัด"                                "MAG +25"                      -mag 25)
        (Art "Tactical Foregrip"     0 "oil"      "กริปยุทธวิธี รีโหลดไวขึ้น +35%"                        "RLD +35%"                     -reload 0.35)
        (Art "Twin Nozzle SMG"       1 "pipe"     "ปากกระบอกคู่ ยิงออกทีละ 2 นัดพร้อมกัน"                 "Shot +1 (x2)"                 -pellets 1 -spread 6.0)
        (Art "FMJ Penetrator"        1 "titanium" "กระสุนเคลือบทองแดง เจาะทะลุศัตรู (Pierce)"             "Pierce, ATK +5"               -pierce 1 -dmg 5)
        (Art "Extended Carbine Kit"  1 "bolt"     "ชุดแต่งลำกล้องยาว เพิ่มระยะยิงและดาเมจ +20%"            "DMG +20%, RNG +1.5"           -dmgPct 0.20 -range 1.5)
        (Art "Reflex Crit Optic"     1 "neural"   "กล้องจุดแดง โอกาสคริติคอล +25%"                        "CRIT +25%"                    -crit 0.25)
        (Art "Bullet Hose Core"      2 "servo"    "ยิงไว +50% + แม็ก +40 นัด ราดกระสุนไม่ยั้ง"            "SPD +50%, MAG +40"            -rate 0.50 -mag 40)
        (Art "Hollow Shredder"       2 "claw"     "ดาเมจ +35% + แรงคริติคอล +45%"                         "DMG +35%, CDMG +45%"          -dmgPct 0.35 -critDmg 0.45)
        (Art "Endless Spray"         3 "fusion"   "โอกาส 40% ไม่เสียกระสุน + แม็ก +30 + ยิงไว +25%"       "Save 40%, MAG +30, SPD +25%"  -save 0.40 -mag 30 -rate 0.25)
        (Art "Storm Sweeper"         3 "crystal"  "ยิงออกทีละ 2 นัด + เจาะทะลุ + ดาเมจ +30%"              "Shot +1, Pierce, DMG +30%"    -pellets 1 -spread 5.5 -pierce 1 -dmgPct 0.30)
    )},
    [ordered]@{ weapon = "Shotgun"; folder = "04_Shotgun"; category = "Ranged"; list = @(
        (Art "Heavy Buckshot"        0 "powder"   "เม็ดลูกปรายใหญ่ขึ้น ดาเมจต่อเม็ด +6"                   "ATK +6/pellet"                -dmg 6)
        (Art "Pump Assist"           0 "spring"   "ปั๊มลูกไวขึ้น ยิงลูกซองเร็วขึ้น +25%"                 "SPD +25%"                     -rate 0.25)
        (Art "Tube Extension"        0 "pipe"     "ต่อหลอดกระสุนลูกซองเพิ่ม +4 นัด"                       "MAG +4"                       -mag 4)
        (Art "Shell Carrier"         0 "tape"     "แผงกระสุนข้างปืน รีโหลดไวขึ้น +40%"                    "RLD +40%"                     -reload 0.40)
        (Art "Extra Pellet Pack"     1 "nails"    "เพิ่มจำนวนลูกปรายต่อนัด +3 เม็ด"                       "Pellets +3"                   -pellets 3 -spread 4.0)
        (Art "Long-Range Choke"      1 "titanium" "ปลอกบีบกลุ่มกระสุน เพิ่มระยะยิง +1.2 และดาเมจ +20%"    "DMG +20%, RNG +1.2"           -dmgPct 0.20 -range 1.2)
        (Art "Solid Slug Mod"        1 "bolt"     "กระสุนลูกโดดเจาะทะลุศัตรู (Pierce) + ดาเมจ +45%"       "Pierce, DMG +45%"             -pierce 1 -dmgPct 0.45)
        (Art "Auto-Shotgun Receiver" 1 "chip"     "เปลี่ยนลูกซองเป็น Full-Auto + ยิงไว +20%"              "Full-Auto, SPD +20%"          -auto 1 -rate 0.20)
        (Art "Riot Breacher"         2 "skull"    "แรงกระแทกและชะงักสูง + ดาเมจ +35%"                     "DMG +35%, KB +2.5, STAG +0.5" -dmgPct 0.35 -kb 2.5 -stag 0.5)
        (Art "Shell Recycler"        2 "cap"      "โอกาส 35% ไม่เสียลูกซองเมื่อยิง + แม็ก +3"            "Save 35%, MAG +3"             -save 0.35 -mag 3)
        (Art "Double-Barrel Salvo"   3 "fusion"   "ม่านกระสุนยักษ์เพิ่มลูกปราย +5 เม็ด + ดาเมจ +25%"      "Pellets +5, DMG +25%"         -pellets 5 -spread 6.0 -dmgPct 0.25)
        (Art "Doomsweeper Core"      3 "crystal"  "Full-Auto + Pierce + ลูกปราย +3 + ยิงไว +35%"          "Auto, Pierce, +3P, SPD +35%"  -auto 1 -pierce 1 -pellets 3 -rate 0.35)
    )},
    [ordered]@{ weapon = "Rifle"; folder = "05_Rifle"; category = "Ranged"; list = @(
        (Art "Match-Grade Barrel"    0 "pipe"     "ลำกล้องมาตรฐานแข่ง ดาเมจไรเฟิล +15"                    "ATK +15"                      -dmg 15)
        (Art "Gas-Block Tuner"       0 "spring"   "ปรับแก๊สยิงไวขึ้น +22%"                                "SPD +22%"                     -rate 0.22)
        (Art "Dual-Clamped Mag"      0 "tape"     "แม็กคู่ติดเทป ความจุ +15 นัด"                          "MAG +15"                      -mag 15)
        (Art "Sleight-of-Hand Mag"   0 "oil"      "เปลี่ยนซองไว รีโหลดเร็วขึ้น +35%"                      "RLD +35%"                     -reload 0.35)
        (Art "Double-Tap Chamber"    1 "bolt"     "ยิงไรเฟิลออกทีละ 2 นัดพร้อมกัน"                        "Shot +1 (x2)"                 -pellets 1 -spread 2.5)
        (Art "AP Steel Core"         1 "titanium" "กระสุนแกนเหล็กเจาะทะลุศัตรู (Pierce)"                  "Pierce, ATK +10"              -pierce 1 -dmg 10)
        (Art "Marksman Optic"        1 "neural"   "กล้องเล็งระยะกลาง คริ +20% ระยะ +2.0"                  "CRIT +20%, RNG +2.0"          -crit 0.20 -range 2.0)
        (Art "Heavy Recoil Buffer"   1 "servo"    "ดาเมจ +25% + ยิงไวขึ้น +15%"                           "DMG +25%, SPD +15%"           -dmgPct 0.25 -rate 0.15)
        (Art "Ramp-Up Barrel"        2 "cap"      "เร่งรอบลำกล้อง ยิงไว +45% + แม็ก +20"                  "SPD +45%, MAG +20"            -rate 0.45 -mag 20)
        (Art "Ammo Conserver"        2 "chip"     "โอกาส 35% ไม่เสียกระสุน + รีโหลดไว +30%"               "Save 35%, RLD +30%"           -save 0.35 -reload 0.30)
        (Art "Tri-Burst Rail"        3 "fusion"   "ยิงออกทีละ 3 นัดทรงพัดแคบ + เจาะทะลุ (Pierce)"         "Shot +2 (x3), Pierce"         -pellets 2 -spread 4.0 -pierce 1)
        (Art "Tactical Overdrive"    3 "crystal"  "ดาเมจ +55% + คริ +25% + แรงคริ +50% + ยิงไว +25%"     "DMG +55%, CRIT +25%, SPD +25%" -dmgPct 0.55 -crit 0.25 -critDmg 0.50 -rate 0.25)
    )},
    [ordered]@{ weapon = "Sniper Rifle"; folder = "06_SniperRifle"; category = "Ranged"; list = @(
        (Art "High-Caliber Bolt"     0 "powder"   "กระสุนลำกล้องใหญ่ ดาเมจสไนเปอร์ +45"                  "ATK +45"                      -dmg 45)
        (Art "Straight-Pull Bolt"    0 "spring"   "ลูกเลื่อนดึงตรง ยิงสไนเปอร์ไวขึ้น +25%"                "SPD +25%"                     -rate 0.25)
        (Art "Extended Sniper Mag"   0 "tape"     "แม็กสไนเปอร์ยาวขึ้น +4 นัด"                            "MAG +4"                       -mag 4)
        (Art "Precision Loader"      0 "oil"      "ระบบบรรจุไว รีโหลดเร็วขึ้น +35%"                       "RLD +35%"                     -reload 0.35)
        (Art "Twin-Shot Chamber"     1 "pipe"     "ยิงสไนเปอร์ออกทีละ 2 ลูกพร้อมกัน!"                     "Shot +1 (x2)"                 -pellets 1 -spread 2.5)
        (Art "Railgun Piercer"       1 "titanium" "เจาะทะลุศัตรูทุกตัวเป็นเส้นตรง + ระยะ +3.0"            "Pierce, RNG +3.0, DMG +20%"   -pierce 1 -range 3.0 -dmgPct 0.20)
        (Art "Auto-Sniper Receiver"  1 "chip"     "เปลี่ยนสไนเปอร์เป็น Full-Auto + ยิงไว +35%"            "Full-Auto, SPD +35%"          -auto 1 -rate 0.35)
        (Art "Headshot Amplifier"    1 "neural"   "เสริมคริติคอล +25% และตัวคูณคริ +70%"                  "CRIT +25%, CDMG +70%"         -crit 0.25 -critDmg 0.70)
        (Art "Anti-Materiel Round"   2 "skull"    "กระสุนเจาะรถถัง ดาเมจ +60% + เจาะทะลุ"                 "DMG +60%, Pierce"             -dmgPct 0.60 -pierce 1)
        (Art "Ghost Magazine"        2 "cap"      "โอกาส 40% ไม่เสียกระสุน + แม็ก +5"                     "Save 40%, MAG +5"             -save 0.40 -mag 5)
        (Art "Triple-Line Rail"      3 "fusion"   "ยิงออกทีละ 3 ลูก + เจาะทะลุ (Pierce) + ดาเมจ +30%"     "Shot +2 (x3), Pierce, +30%"   -pellets 2 -spread 4.0 -pierce 1 -dmgPct 0.30)
        (Art "One-Shot Sovereign"    3 "crystal"  "ดาเมจ +100% + แรงคริ +100% + เจาะทะลุ"                 "DMG +100%, CDMG +100%, Pierce" -dmgPct 1.00 -critDmg 1.00 -pierce 1)
    )},
    [ordered]@{ weapon = "Crossbow"; folder = "07_Crossbow"; category = "Ranged"; list = @(
        (Art "Reinforced Limb"       0 "titanium" "คันหน้าไม้เสริมแรง ดาเมจ +25"                          "ATK +25"                      -dmg 25)
        (Art "Quick-Draw Pulley"     0 "spring"   "รอกดึงสายไว ยิงเร็วขึ้น +25%"                          "SPD +25%"                     -rate 0.25)
        (Art "Repeating Quiver"      0 "tape"     "ซองบรรจุลูกธนูอัตโนมัติ +5 ดอก"                        "MAG +5"                       -mag 5)
        (Art "Auto-Winch Crank"      0 "oil"      "รอกหมุนขึ้นสายไว รีโหลดเร็วขึ้น +40%"                  "RLD +40%"                     -reload 0.40)
        (Art "Split-Bolt Fork"       1 "pipe"     "รางคู่ ยิงลูกธนูออกทีละ 2 ดอกพร้อมกัน"                 "Shot +1 (x2)"                 -pellets 1 -spread 5.0)
        (Art "Skewer Bolt"           1 "stinger"  "หัวธนูเหล็กแหลม เจาะทะลุศัตรู (Pierce)"                "Pierce, DMG +20%"             -pierce 1 -dmgPct 0.20)
        (Art "Repeater Trigger"      1 "chip"     "เปลี่ยนหน้าไม้เป็น Full-Auto ยิงรัวต่อเนื่อง"           "Full-Auto, SPD +30%"          -auto 1 -rate 0.30)
        (Art "Hunter Scope"          1 "neural"   "กล้องพรานป่า คริ +25% แรงคริ +60%"                     "CRIT +25%, CDMG +60%"         -crit 0.25 -critDmg 0.60 -range 1.5)
        (Art "Tri-Spread Bow"        2 "servo"    "ยิงลูกธนูออกทีละ 3 ดอกเป็นพัด (มุม 10°)"               "Shot +2 (x3)"                 -pellets 2 -spread 10.0)
        (Art "Bolt Recycler"         2 "cap"      "โอกาส 50% ยิงแล้วไม่เสียลูกธนู (Bolts)"                "Save 50%, RLD +25%"           -save 0.50 -reload 0.25)
        (Art "Siege Ballista Core"   3 "skull"    "ดาเมจ +85% + เจาะทะลุ + แรงคริ +60%"                   "DMG +85%, Pierce, CDMG +60%"  -dmgPct 0.85 -pierce 1 -critDmg 0.60)
        (Art "Hunter's Volley"       3 "crystal"  "ยิงออกทีละ 5 ดอกพัดกว้าง + เจาะทะลุทุกดอก!"            "Shot +4 (x5), Pierce"         -pellets 4 -spread 16.0 -pierce 1)
    )},
    [ordered]@{ weapon = "Flamethrower"; folder = "08_Flamethrower"; category = "Ranged"; list = @(
        (Art "High-Density Fuel"     0 "powder"   "เชื้อเพลิงอัดแน่น ดาเมจ +12"                           "ATK +12"                      -dmg 12)
        (Art "High-Pressure Valve"   0 "spring"   "วาล์วแรงดันสูง พ่นไวขึ้น +25%"                         "SPD +25%"                     -rate 0.25)
        (Art "Pressurized Tank"      0 "pipe"     "ถังอัดแรงดันใหญ่ ความจุ +35 หน่วย"                     "MAG +35"                      -mag 35)
        (Art "Quick-Swap Canister"   0 "oil"      "ถังเปลี่ยนเร็ว รีโหลดไวขึ้น +40%"                      "RLD +40%"                     -reload 0.40)
        (Art "Wide-Cone Nozzle"      1 "bolt"     "หัวฉีดกว้าง พ่นออกทีละ +2 สาย"                         "Streams +2"                   -pellets 2 -spread 14.0)
        (Art "Long-Jet Nozzle"       1 "titanium" "หัวฉีดแรงดันไกล ระยะพ่นไกลขึ้น +2.0"                  "RNG +2.0, DMG +15%"           -range 2.0 -dmgPct 0.15)
        (Art "Piercing Jet Core"     1 "stinger"  "ลำพ่นทะลุผ่านฝูงศัตรูทุกตัว (Pierce)"                  "Pierce, ATK +8"               -pierce 1 -dmg 8)
        (Art "Fuel Saver Regulator"  1 "chip"     "ลดการสิ้นเปลือง โอกาส 40% ไม่เสียเชื้อเพลิง"            "Save 40%"                     -save 0.40)
        (Art "Turbo Injector"        2 "servo"    "ดาเมจ +45% + พ่นไวขึ้น +30%"                           "DMG +45%, SPD +30%"           -dmgPct 0.45 -rate 0.30)
        (Art "Heavy Compression Tank" 2 "cap"     "ความจุ +60 หน่วย + รีโหลดไว +35%"                      "MAG +60, RLD +35%"            -mag 60 -reload 0.35)
        (Art "Overpressure Surge"    3 "fusion"   "ดาเมจ +70% + ทะลุศัตรู + ระยะ +2.5"                    "DMG +70%, Pierce, RNG +2.5"   -dmgPct 0.70 -pierce 1 -range 2.5)
        (Art "Hellstorm Nozzle"      3 "crystal"  "พ่นออก +3 สาย + ทะลุศัตรู + ดาเมจ +35% + ประหยัด 30%"  "Streams +3, Pierce, +35%"     -pellets 3 -spread 18.0 -pierce 1 -dmgPct 0.35 -save 0.30)
    )},
    [ordered]@{ weapon = "Rocket Launcher"; folder = "09_RocketLauncher"; category = "Ranged"; list = @(
        (Art "High-Explosive Head"   0 "powder"   "หัวรบแรงสูง ดาเมจจรวด +60"                             "ATK +60"                      -dmg 60)
        (Art "Fast-Cycle Tube"       0 "spring"   "ท่อยิงรอบไว ยิงจรวดเร็วขึ้น +25%"                      "SPD +25%"                     -rate 0.25)
        (Art "Quad-Pod Magazine"     0 "pipe"     "กระบอกบรรจุหลายลูก แม็ก +3 ลูก"                        "MAG +3"                       -mag 3)
        (Art "Auto-Loader Rack"      0 "oil"      "ระบบโหลดอัตโนมัติ รีโหลดไวขึ้น +40%"                   "RLD +40%"                     -reload 0.40)
        (Art "Twin Salvo Pod"        1 "bolt"     "ยิงจรวดออกทีละ 2 ลูกพร้อมกัน!"                         "Shot +1 (x2)"                 -pellets 1 -spread 7.0)
        (Art "Bunker Buster"         1 "titanium" "หัวรบเจาะบังเกอร์ ดาเมจ +45%"                          "DMG +45%"                     -dmgPct 0.45)
        (Art "Booster Thruster"      1 "servo"    "เพิ่มระยะพุ่ง +3.0 และยิงไวขึ้น +20%"                  "RNG +3.0, SPD +20%"           -range 3.0 -rate 0.20)
        (Art "Warhead Optimizer"     1 "neural"   "โอกาสคริติคอล +30% และแรงคริ +50%"                     "CRIT +30%, CDMG +50%"         -crit 0.30 -critDmg 0.50)
        (Art "Munitions Recycler"    2 "cap"      "โอกาส 35% ยิงจรวดไม่เสียกระสุน + แม็ก +2"              "Save 35%, MAG +2"             -save 0.35 -mag 2)
        (Art "Triple Salvo Rack"     2 "skull"    "ยิงจรวดออกทีละ 3 ลูกพร้อมกัน (มุม 12°)"                "Shot +2 (x3)"                 -pellets 2 -spread 12.0)
        (Art "Barrage Launcher"      3 "fusion"   "Full-Auto + แม็ก +4 + ยิงไว +40% + รีโหลด +35%"        "Auto, MAG +4, SPD +40%"       -auto 1 -mag 4 -rate 0.40 -reload 0.35)
        (Art "Tactical Nuke Core"    3 "crystal"  "ดาเมจ +120% + ยิงจรวดออกทีละ 2 ลูก!"                   "DMG +120%, Shot +1 (x2)"      -dmgPct 1.20 -pellets 1 -spread 6.0)
    )},
    [ordered]@{ weapon = "Knife"; folder = "10_Knife"; category = "Melee"; list = @(
        (Art "Razor Honed Edge"      0 "titanium" "ลับคมมีดพิเศษ ดาเมจ +15"                               "ATK +15"                      -dmg 15)
        (Art "Featherweight Grip"    0 "tape"     "ด้ามเบาพิเศษ ฟันมีดไวขึ้น +25%"                        "SPD +25%"                     -rate 0.25)
        (Art "Extended Tang"         0 "pipe"     "ต่อใบมีดยาวขึ้น ระยะฟัน +0.6m"                         "RNG +0.6m"                    -range 0.6)
        (Art "Serrated Teeth"        0 "nails"    "ฟันเลื่อยเพิ่มแรงชะงักและแรงผลัก"                       "ATK +10, KB +2, STAG +0.3"    -dmg 10 -kb 2.0 -stag 0.3)
        (Art "Assassin's Edge"       1 "stinger"  "โอกาสคริติคอล +30% + แรงคริ +40%"                      "CRIT +30%, CDMG +40%"         -crit 0.30 -critDmg 0.40)
        (Art "Combat Tanto Mod"      1 "bolt"     "ดาเมจ +30% + ระยะฟัน +0.5m"                            "DMG +30%, RNG +0.5m"          -dmgPct 0.30 -range 0.5)
        (Art "Twin-Slash Balance"    1 "spring"   "ฟันไวขึ้น +40%"                                        "SPD +40%"                     -rate 0.40)
        (Art "Vampiric Fang"         1 "heart"    "ฟันโดนศัตรูดูดเลือดคืน +4 HP ต่อครั้ง"                 "Leech +4 HP"                  -leech 4)
        (Art "Shadow Blade Core"     2 "claw"     "ดาเมจ +45% + ฟันไวขึ้น +30%"                           "DMG +45%, SPD +30%"           -dmgPct 0.45 -rate 0.30)
        (Art "Bloodthirst Hilt"      2 "fang"     "ดูดเลือด +7 HP ต่อครั้ง + คริ +20%"                    "Leech +7 HP, CRIT +20%"       -leech 7 -crit 0.20)
        (Art "Phantom Flurry"        3 "servo"    "ฟันไว +65% + ระยะ +0.9m + ดาเมจ +30%"                  "SPD +65%, RNG +0.9m, +30%"    -rate 0.65 -range 0.9 -dmgPct 0.30)
        (Art "Coup De Grace"         3 "crystal"  "ดาเมจ +80% + คริ +45% + แรงคริ +90%"                   "DMG +80%, CRIT +45%, +90%"    -dmgPct 0.80 -crit 0.45 -critDmg 0.90)
    )},
    [ordered]@{ weapon = "Baseball Bat"; folder = "11_BaseballBat"; category = "Melee"; list = @(
        (Art "Barbed Wire Wrap"      0 "nails"    "พันลวดหนามรอบไม้เบสบอล ดาเมจ +18"                      "ATK +18"                      -dmg 18)
        (Art "Corked Core"           0 "tape"     "ไม้เบาพิเศษ หวดไวขึ้น +25%"                            "SPD +25%"                     -rate 0.25)
        (Art "Long-Slugger Shaft"    0 "pipe"     "ด้ามยาวพิเศษ ระยะหวด +0.7m"                            "RNG +0.7m"                    -range 0.7)
        (Art "Weighted Tip"          0 "bolt"     "ถ่วงหัวไม้ แรงกระแทก +5 และชะงัก +0.4 วิ"               "KB +5, STAG +0.4s"            -kb 5.0 -stag 0.4)
        (Art "Steel Studs"           1 "titanium" "ตอกหมุดเหล็ก ดาเมจ +30% + คริ +15%"                    "DMG +30%, CRIT +15%"          -dmgPct 0.30 -crit 0.15)
        (Art "Concussion Swing"      1 "skull"    "หวดมึนงง สตั๊น +0.8 วิ + แรงกระแทก +6"                 "STAG +0.8s, KB +6"            -stag 0.8 -kb 6.0)
        (Art "Wide Cleave Arc"       1 "spring"   "ระยะหวด +1.1m ฟาดโดนหลายตัวพร้อมกัน"                   "RNG +1.1m"                    -range 1.1)
        (Art "Sawblade Studs"        1 "claw"     "ติดใบเลื่อยข้างไม้ ดาเมจ +30 + แรงคริ +40%"             "ATK +30, CDMG +40%"           -dmg 30 -critDmg 0.40)
        (Art "Home-Run Impact"       2 "servo"    "ดาเมจ +45% + แรงกระแทก +10 ปลิวทั้งแถว"                "DMG +45%, KB +10"             -dmgPct 0.45 -kb 10.0)
        (Art "Brawler's Grit"        2 "heart"    "หวดโดนศัตรูฟื้นเลือด +5 HP + หวดไว +25%"               "Leech +5 HP, SPD +25%"        -leech 5 -rate 0.25)
        (Art "Grand Slam Core"       3 "fusion"   "ดาเมจ +75% + คริ +30% + สตั๊น +1.0 วิ"                 "DMG +75%, CRIT +30%, STAG +1s" -dmgPct 0.75 -crit 0.30 -stag 1.0)
        (Art "Batter's Fury"         3 "crystal"  "ดาเมจ +55% + ระยะ +1.3m + หวดไว +35%"                  "DMG +55%, RNG +1.3m, SPD +35%" -dmgPct 0.55 -range 1.3 -rate 0.35)
    )},
    [ordered]@{ weapon = "Machete"; folder = "12_Machete"; category = "Melee"; list = @(
        (Art "Carbon Steel Blade"    0 "titanium" "ใบมีดเหล็กคาร์บอน ดาเมจ +22"                           "ATK +22"                      -dmg 22)
        (Art "Balanced Hilt"         0 "tape"     "ถ่วงด้ามสมดุล ฟันไวขึ้น +24%"                          "SPD +24%"                     -rate 0.24)
        (Art "Jungle Reach"          0 "pipe"     "ใบมีดพร้ายาวขึ้น ระยะฟัน +0.8m"                        "RNG +0.8m"                    -range 0.8)
        (Art "Heavy Cleaver Spine"   0 "bolt"     "สันมีดหนา เพิ่มดาเมจ +22% และชะงัก +0.3 วิ"            "DMG +22%, STAG +0.3s"         -dmgPct 0.22 -stag 0.3)
        (Art "Butcher's Rhythm"      1 "spring"   "จังหวะสับต่อเนื่อง ฟันไวขึ้น +38%"                     "SPD +38%"                     -rate 0.38)
        (Art "Gore Cleave"           1 "claw"     "ระยะฟัน +1.2m กวาดฝูงกว้างขึ้น"                        "RNG +1.2m, ATK +15"           -range 1.2 -dmg 15)
        (Art "Keen Razor Edge"       1 "stinger"  "โอกาสคริติคอล +25% + แรงคริ +45%"                      "CRIT +25%, CDMG +45%"         -crit 0.25 -critDmg 0.45)
        (Art "Heavy Chop Weight"     1 "nails"    "ดาเมจ +35% + แรงกระแทก +4"                             "DMG +35%, KB +4"              -dmgPct 0.35 -kb 4.0)
        (Art "Blood Frenzy"          2 "heart"    "ฟันโดนศัตรูดูดเลือดคืน +6 HP + ฟันไว +20%"             "Leech +6 HP, SPD +20%"        -leech 6 -rate 0.20)
        (Art "Tempered Saw-Back"     2 "servo"    "ดาเมจ +45% + สตั๊น +0.5 วิ"                            "DMG +45%, STAG +0.5s"         -dmgPct 0.45 -stag 0.5)
        (Art "Executioner's Arc"     3 "skull"    "ดาเมจ +70% + ระยะ +1.3m + แรงคริ +60%"                 "DMG +70%, RNG +1.3m, +60%"    -dmgPct 0.70 -range 1.3 -critDmg 0.60)
        (Art "Endless Carnage"       3 "crystal"  "ฟันไว +55% + ดูดเลือด +6 HP + ดาเมจ +35%"              "SPD +55%, Leech +6, +35%"     -rate 0.55 -leech 6 -dmgPct 0.35)
    )},
    [ordered]@{ weapon = "Fire Axe"; folder = "13_FireAxe"; category = "Melee"; list = @(
        (Art "Forged Axe Head"       0 "titanium" "หัวขวานตีเหล็กกล้า ดาเมจ +30"                          "ATK +30"                      -dmg 30)
        (Art "Fiberglass Handle"     0 "tape"     "ด้ามไฟเบอร์น้ำหนักเบา เหวี่ยงไวขึ้น +25%"               "SPD +25%"                     -rate 0.25)
        (Art "Long-Haft Reach"       0 "pipe"     "ด้ามขวานยาวพิเศษ ระยะฟัน +0.9m"                        "RNG +0.9m"                    -range 0.9)
        (Art "Armor Splitter"        0 "bolt"     "ผ่าเกราะหนัก ดาเมจ +28%"                               "DMG +28%"                     -dmgPct 0.28)
        (Art "Skull Cracker"         1 "skull"    "ดาเมจ +35% + สตั๊น +0.7 วิ"                            "DMG +35%, STAG +0.7s"         -dmgPct 0.35 -stag 0.7)
        (Art "Cleaving Sweep"        1 "claw"     "ระยะฟัน +1.4m กวาดรอบตัวกว้างขึ้น"                     "RNG +1.4m"                    -range 1.4)
        (Art "Breacher's Momentum"   1 "spring"   "เหวี่ยงขวานไวขึ้น +38% + แรงผลัก +4"                   "SPD +38%, KB +4"              -rate 0.38 -kb 4.0)
        (Art "Splitting Crit Edge"   1 "stinger"  "โอกาสคริติคอล +25% + แรงคริ +55%"                      "CRIT +25%, CDMG +55%"         -crit 0.25 -critDmg 0.55)
        (Art "Timber Shockwave"      2 "servo"    "ระยะ +1.6m + แรงกระแทก +8 + สตั๊น +0.8 วิ"             "RNG +1.6m, KB +8, STAG +0.8s" -range 1.6 -kb 8.0 -stag 0.8)
        (Art "Berserker's Heart"     2 "heart"    "จามโดนศัตรูดูดเลือด +8 HP + ดาเมจ +30%"                "Leech +8 HP, DMG +30%"        -leech 8 -dmgPct 0.30)
        (Art "Guillotine Strike"     3 "fusion"   "ดาเมจ +90% + แรงคริ +70%"                              "DMG +90%, CDMG +70%"          -dmgPct 0.90 -critDmg 0.70)
        (Art "Titan Chopper"         3 "crystal"  "ดาเมจ +55% + ระยะ +1.6m + เหวี่ยงไว +35%"              "DMG +55%, RNG +1.6m, SPD +35%" -dmgPct 0.55 -range 1.6 -rate 0.35)
    )},
    [ordered]@{ weapon = "Katana"; folder = "14_Katana"; category = "Melee"; list = @(
        (Art "Tamahagane Fold"       0 "titanium" "ตีเหล็กพับชั้น ดาเมจคาตานะ +28"                        "ATK +28"                      -dmg 28)
        (Art "Quick-Draw Saya"       0 "oil"      "ฝักดาบลื่น ฟันไวขึ้น +25%"                             "SPD +25%"                     -rate 0.25)
        (Art "Odachi Lengthening"    0 "pipe"     "ยืดใบดาบโอตาจิ ระยะฟัน +1.0m"                          "RNG +1.0m"                    -range 1.0)
        (Art "Keen Edge Polish"      0 "stinger"  "ลับคมระดับกระจก โอกาสคริติคอล +22%"                    "CRIT +22%"                    -crit 0.22)
        (Art "Wind-Cutter Blade"     1 "claw"     "คลื่นดาบลมไกลขึ้น ระยะฟัน +1.6m"                       "RNG +1.6m, ATK +18"           -range 1.6 -dmg 18)
        (Art "Lightning Draw"        1 "spring"   "ชักดาบสายฟ้า ฟันไวขึ้น +42%"                           "SPD +42%"                     -rate 0.42)
        (Art "Iaijutsu Focus"        1 "neural"   "คริติคอล +25% + แรงคริติคอล +65%"                      "CRIT +25%, CDMG +65%"         -crit 0.25 -critDmg 0.65)
        (Art "Ronin Steel Spine"     1 "bolt"     "ดาเมจ +35% + ชะงัก +0.4 วิ"                            "DMG +35%, STAG +0.4s"         -dmgPct 0.35 -stag 0.4)
        (Art "Moonlit Iaido"         2 "servo"    "ดาเมจ +55% + ฟันไว +30%"                               "DMG +55%, SPD +30%"           -dmgPct 0.55 -rate 0.30)
        (Art "Soul Siphon"           2 "heart"    "ฟันโดนศัตรูดูดเลือด +7 HP + ระยะ +0.8m"                "Leech +7 HP, RNG +0.8m"       -leech 7 -range 0.8)
        (Art "Thousand Cuts"         3 "fusion"   "ฟันไว +60% + ระยะ +1.5m + ดาเมจ +35%"                  "SPD +60%, RNG +1.5m, +35%"    -rate 0.60 -range 1.5 -dmgPct 0.35)
        (Art "Muramasa Curse"        3 "crystal"  "ดาเมจ +100% + คริ +40% + แรงคริ +80%"                  "DMG +100%, CRIT +40%, +80%"   -dmgPct 1.00 -crit 0.40 -critDmg 0.80)
    )},
    [ordered]@{ weapon = "Sledgehammer"; folder = "15_Sledgehammer"; category = "Melee"; list = @(
        (Art "Lead-Filled Head"      0 "bolt"     "หัวค้อนถ่วงตะกั่ว ดาเมจ +40"                           "ATK +40"                      -dmg 40)
        (Art "Alloy Shaft"           0 "pipe"     "ด้ามอัลลอยด์เบา เหวี่ยงค้อนไวขึ้น +30%"                "SPD +30%"                     -rate 0.30)
        (Art "Giant Reach"           0 "tape"     "ด้ามยักษ์ ระยะทุบ +1.0m"                               "RNG +1.0m"                    -range 1.0)
        (Art "Seismic Impact"        0 "skull"    "แรงกระแทกแผ่นดินไหว สตั๊น +1.0 วิ + ผลัก +6"           "STAG +1.0s, KB +6"            -stag 1.0 -kb 6.0)
        (Art "Armor Crusher"         1 "titanium" "บดขยี้เกราะ ดาเมจ +45%"                                "DMG +45%"                     -dmgPct 0.45)
        (Art "Tremor Wave"           1 "grav"     "คลื่นแผ่นดินไหว ระยะทุบ +1.7m"                         "RNG +1.7m"                    -range 1.7)
        (Art "Piston Rocket Head"    1 "servo"    "ติดลูกสูบดันหัวค้อน เหวี่ยงไว +42% + แรงผลัก +8"       "SPD +42%, KB +8"              -rate 0.42 -kb 8.0)
        (Art "Skullbreaker Crit"     1 "nails"    "โอกาสคริ +25% + แรงคริ +60%"                           "CRIT +25%, CDMG +60%"         -crit 0.25 -critDmg 0.60)
        (Art "Meteor Slam"           2 "fusion"   "ดาเมจ +65% + แรงกระแทก +10 + สตั๊น +0.8 วิ"            "DMG +65%, KB +10, STAG +0.8s" -dmgPct 0.65 -kb 10.0 -stag 0.8)
        (Art "Juggernaut Vigor"      2 "heart"    "ทุบโดนศัตรูดูดเลือด +10 HP + ดาเมจ +25%"               "Leech +10 HP, DMG +25%"       -leech 10 -dmgPct 0.25)
        (Art "Earthquake Core"       3 "chitin"   "ระยะทุบ +2.2m + ดาเมจ +50% + สตั๊น +1.4 วิ"            "RNG +2.2m, DMG +50%, +1.4s"   -range 2.2 -dmgPct 0.50 -stag 1.4)
        (Art "Titan Overdrive"       3 "crystal"  "ดาเมจ +95% + เหวี่ยงไว +38% + แรงผลัก +12"             "DMG +95%, SPD +38%, KB +12"   -dmgPct 0.95 -rate 0.38 -kb 12.0)
    )},
    [ordered]@{ weapon = "Survival Spear"; folder = "16_SurvivalSpear"; category = "Melee"; list = @(
        (Art "Forged Spearhead"      0 "titanium" "หัวหอกเหล็กกล้า ดาเมจ +22"                             "ATK +22"                      -dmg 22)
        (Art "Rapid Thrust Grip"     0 "tape"     "ด้ามจับกันลื่น แทงไวขึ้น +28%"                         "SPD +28%"                     -rate 0.28)
        (Art "Pike Extension"        0 "pipe"     "ต่อด้ามหอกยาว ระยะแทง +1.2m"                           "RNG +1.2m"                    -range 1.2)
        (Art "Barbed Hook Tip"       0 "nails"    "เงี่ยงหอกเกี่ยวศัตรู ชะงัก +0.5 วิ + แรงผลัก +4"        "STAG +0.5s, KB +4"            -stag 0.5 -kb 4.0)
        (Art "Skewer Thrust"         1 "stinger"  "ระยะแทง +1.6m + ดาเมจ +25%"                            "RNG +1.6m, DMG +25%"          -range 1.6 -dmgPct 0.25)
        (Art "Halberd Side-Blade"    1 "claw"     "เสริมใบขวานข้างหัวหอก ดาเมจ +35% + คริ +15%"           "DMG +35%, CRIT +15%"          -dmgPct 0.35 -crit 0.15)
        (Art "Phalanx Brace"         1 "bolt"     "ผลักศัตรูถอยห่าง แรงกระแทก +8 + ระยะ +0.8m"            "KB +8, RNG +0.8m"             -kb 8.0 -range 0.8)
        (Art "Whirlwind Thrust"      1 "spring"   "แทงรัวไวขึ้น +42%"                                     "SPD +42%"                     -rate 0.42)
        (Art "Dragon-Slayer Tip"     2 "skull"    "หัวหอกล่าบอส ดาเมจ +60% + แรงคริ +50%"                 "DMG +60%, CDMG +50%"          -dmgPct 0.60 -critDmg 0.50)
        (Art "Valkyrie Siphon"       2 "heart"    "แทงโดนศัตรูดูดเลือด +5 HP + แทงไว +25%"                "Leech +5 HP, SPD +25%"        -leech 5 -rate 0.25)
        (Art "Longinus Reach"        3 "fusion"   "ระยะแทงไกลสุดขีด +2.4m + ดาเมจ +55%"                  "RNG +2.4m, DMG +55%"          -range 2.4 -dmgPct 0.55)
        (Art "Spartan Barrage"       3 "crystal"  "แทงไว +60% + ดูดเลือด +5 HP + คริ +30%"                "SPD +60%, Leech +5, CRIT +30%" -rate 0.60 -leech 5 -crit 0.30)
    )},
    [ordered]@{ weapon = "Stun Baton"; folder = "17_StunBaton"; category = "Melee"; list = @(
        (Art "Heavy Core Rod"        0 "bolt"     "แกนเหล็กถ่วงน้ำหนัก ดาเมจกระบอง +18"                   "ATK +18"                      -dmg 18)
        (Art "Rapid Baton Drill"     0 "spring"   "ฝึกจังหวะฟาด ฟาดไวขึ้น +25%"                           "SPD +25%"                     -rate 0.25)
        (Art "Telescopic Rod"        0 "pipe"     "กระบองยืดหด ระยะฟาด +0.8m"                             "RNG +0.8m"                    -range 0.8)
        (Art "Impact Capacitor"      0 "cap"      "แรงกระแทกมึนงง สตั๊นศัตรูเพิ่มขึ้น +0.9 วิ"            "STAG +0.9s"                   -stag 0.9)
        (Art "Riot Sweeper"          1 "titanium" "ระยะฟาด +1.3m + แรงผลัก +6"                            "RNG +1.3m, KB +6"             -range 1.3 -kb 6.0)
        (Art "Kinetic Surge"         1 "servo"    "ดาเมจ +35% + ฟาดไวขึ้น +22%"                           "DMG +35%, SPD +22%"           -dmgPct 0.35 -rate 0.22)
        (Art "Precision Strike Tip"  1 "neural"   "ตีจุดอ่อน คริติคอล +28% + แรงคริ +50%"                 "CRIT +28%, CDMG +50%"         -crit 0.28 -critDmg 0.50)
        (Art "Lockdown Baton"        1 "chip"     "สตั๊น +1.2 วิ + ดาเมจ +20%"                            "STAG +1.2s, DMG +20%"         -stag 1.2 -dmgPct 0.20)
        (Art "Enforcer Combo"        2 "nails"    "ฟาดไว +45% + ดาเมจ +30%"                               "SPD +45%, DMG +30%"           -rate 0.45 -dmgPct 0.30)
        (Art "Tactical Recovery"     2 "heart"    "ฟาดโดนศัตรูดูดเลือด +6 HP + สตั๊น +0.6 วิ"             "Leech +6 HP, STAG +0.6s"      -leech 6 -stag 0.6)
        (Art "Riot Master Core"      3 "fusion"   "ดาเมจ +75% + สตั๊น +1.5 วิ + แรงกระแทก +9"             "DMG +75%, STAG +1.5s, KB +9"  -dmgPct 0.75 -stag 1.5 -kb 9.0)
        (Art "Hyper-Pulse Overdrive" 3 "crystal"  "ฟาดไว +55% + ระยะ +1.5m + คริ +35%"                    "SPD +55%, RNG +1.5m, +35%"    -rate 0.55 -range 1.5 -crit 0.35)
    )},
    [ordered]@{ weapon = "Chainsaw"; folder = "18_Chainsaw"; category = "Melee"; list = @(
        (Art "Diamond-Tip Chain"     0 "titanium" "โซ่ฟันเพชร ดาเมจเลื่อย +14 ต่อติ๊ก"                    "ATK +14"                      -dmg 14)
        (Art "High-RPM Carburetor"   0 "oil"      "คาร์บูเรเตอร์แต่ง รอบตัดไวขึ้น +25%"                   "SPD +25%"                     -rate 0.25)
        (Art "Long Guide Bar"        0 "pipe"     "บาร์เลื่อยยาวพิเศษ ระยะตัด +0.9m"                      "RNG +0.9m"                    -range 0.9)
        (Art "Heavy Flywheel"        0 "bolt"     "ฟลายวีลหนัก เพิ่มแรงชะงักและแรงกระแทก"                 "STAG +0.4s, KB +3"            -stag 0.4 -kb 3.0)
        (Art "Rip-Saw Teeth"         1 "claw"     "ฟันเลื่อยฉีกเนื้อ ดาเมจ +35%"                           "DMG +35%"                     -dmgPct 0.35)
        (Art "Turbo Exhaust"         1 "servo"    "ท่อเทอร์โบ ความเร็วรอบตัด +42%"                        "SPD +42%"                     -rate 0.42)
        (Art "Shredder Reach"        1 "nails"    "บาร์กว้างพิเศษ ระยะตัด +1.4m กวาดฝูงด้านหน้า"          "RNG +1.4m"                    -range 1.4)
        (Art "Precision Teeth"       1 "stinger"  "โอกาสคริติคอล +25% + แรงคริ +45%"                      "CRIT +25%, CDMG +45%"         -crit 0.25 -critDmg 0.45)
        (Art "Blood-Fueled Engine"   2 "heart"    "เลื่อยโดนศัตรูดูดเลือดคืน +3 HP ต่อติ๊ก"                "Leech +3 HP/hit"              -leech 3)
        (Art "Armor Grinder"         2 "skull"    "ฟันเจาะเกราะ ดาเมจ +55% + ชะงัก +0.5 วิ"               "DMG +55%, STAG +0.5s"         -dmgPct 0.55 -stag 0.5)
        (Art "Doom Ripper"           3 "fusion"   "ดาเมจ +70% + รอบตัด +38% + ระยะ +1.3m"                 "DMG +70%, SPD +38%, RNG +1.3" -dmgPct 0.70 -rate 0.38 -range 1.3)
        (Art "Unstoppable Rampage"   3 "crystal"  "ดูดเลือด +5 HP + รอบตัด +55% + ดาเมจ +45%"             "Leech +5, SPD +55%, +45%"     -leech 5 -rate 0.55 -dmgPct 0.45)
    )},
    [ordered]@{ weapon = "Grenade"; folder = "19_Grenade"; category = "Throwing"; list = @(
        (Art "C4 Compound Fill"      0 "powder"   "อัดดินระเบิด C4 ดาเมจระเบิด +45"                       "ATK +45"                      -dmg 45)
        (Art "Quick-Pull Pin"        0 "spring"   "สลักดึงไว ขว้างระเบิดเร็วขึ้น +30%"                    "SPD +30%"                     -rate 0.30)
        (Art "Aero-Shell Casing"     0 "pipe"     "ปลอกอากาศพลศาสตร์ ขว้างไกลขึ้น +2.5"                   "RNG +2.5"                     -range 2.5)
        (Art "Extra Shrapnel"        0 "nails"    "อัดสะเก็ดระเบิดเพิ่ม ดาเมจ +28% + สตั๊น"               "DMG +28%, STAG +0.6s"         -dmgPct 0.28 -stag 0.6)
        (Art "Twin Toss Rig"         1 "tape"     "ขว้างระเบิดออกทีละ 2 ลูกพร้อมกัน!"                     "Throw +1 (x2)"                -pellets 1 -spread 10.0)
        (Art "Cluster Bomblets"      1 "bolt"     "ขว้างระเบิดออกทีละ 3 ลูกเป็นพัด (15°)"                 "Throw +2 (x3)"                -pellets 2 -spread 15.0)
        (Art "Concussion Charge"     1 "skull"    "แรงอัดกระแทกหนัก สตั๊น +1.5 วิ + แรงผลัก +8"           "STAG +1.5s, KB +8"            -stag 1.5 -kb 8.0)
        (Art "Precision Fuze"        1 "neural"   "ชนวนแม่นยำ โอกาสคริติคอล +30% + แรงคริ +50%"           "CRIT +30%, CDMG +50%"         -crit 0.30 -critDmg 0.50)
        (Art "Rapid Grenadier"       2 "servo"    "เปลี่ยนเป็น Full-Auto ขว้างรัวต่อเนื่องไว +45%"        "Full-Auto, SPD +45%"          -auto 1 -rate 0.45)
        (Art "Heavy Frag Core"       2 "titanium" "สะเก็ดระเบิดหนัก ดาเมจ +60% + แม็ก +4"                 "DMG +60%, MAG +4"             -dmgPct 0.60 -mag 4)
        (Art "Tactical Recycler"     3 "cap"      "โอกาส 45% ขว้างแล้วไม่เสียระเบิด + ขว้างไว +25%"       "Save 45%, SPD +25%"           -save 0.45 -rate 0.25)
        (Art "Carpet Bombing"        3 "crystal"  "ขว้างออกทีละ 4 ลูกพร้อมกัน + ดาเมจ +50%!"              "Throw +3 (x4), DMG +50%"      -pellets 3 -spread 20.0 -dmgPct 0.50)
    )}
)

$UniversalArtifacts = @(
    [ordered]@{
        folder = "00_Universal"
        scope = "ปืนทุกชนิด (All 9 Ranged Weapons)"
        weapons = @("Pistol", "Revolver", "SMG", "Shotgun", "Rifle", "Sniper Rifle", "Crossbow", "Flamethrower", "Rocket Launcher")
        artifact = (Art "Double-Tap Trigger" 1 "servo" "กดยิง 1 ที ยิงซ้ำออก 2 ทีติดกัน! (ใส่ได้กับปืนทุกชนิด)" "Burst +1 (1 Click = 2 Shots)" -burst 1)
    }
    [ordered]@{
        folder = "00_Universal"
        scope = "ปืนทุกชนิด (All 9 Ranged Weapons)"
        weapons = @("Pistol", "Revolver", "SMG", "Shotgun", "Rifle", "Sniper Rifle", "Crossbow", "Flamethrower", "Rocket Launcher")
        artifact = (Art "Heavy Caliber Shell" 1 "grav" "ขยายขนาดกระสุนและ Hitbox ใหญ่ขึ้นเป็น 2.5 เท่า ยิงศัตรูตัวเล็กโดนง่ายมาก! (ใส่ได้กับปืนทุกชนิด)" "Size x2.5 (Huge Hitbox)" -scale 1.5)
    }
)

$artifactsRoot = Join-Path $ProjectRoot "Assets\Resources_Addressable\Database\ItemData\Artifacts"
if (-not (Test-Path $artifactsRoot)) { New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null }
Write-FolderMeta $artifactsRoot "ArtifactsRoot"

$itemDataScriptGuid = "f66e274d5d4a559448705b384dc143eb"
$allGeneratedGuids = New-Object System.Collections.Generic.List[string]
$catalogMd = New-Object System.Text.StringBuilder

# Normalize all legacy rarity=2 (Epic) entries -> rarity=1 (Rare) so every weapon matches the 3-tier system:
# Common (0-4 slots) / Rare (max 2 slots) / Legendary (max 1 slot)
foreach ($w in $Weapons) {
    foreach ($a in $w.list) {
        if ($a.rarity -eq 2) { $a.rarity = 1 }
    }
}

$totalSpecific = 0
foreach ($w in $Weapons) { $totalSpecific += $w.list.Count }
$grandTotal = $totalSpecific + $UniversalArtifacts.Count

[void]$catalogMd.AppendLine("# รายการ Artifact แต่งอาวุธทั้งหมด (Weapon Artifacts Master List)")
[void]$catalogMd.AppendLine("")
[void]$catalogMd.AppendLine("**อัปเดตล่าสุด**: 24 พฤศจิกายน 2026  ")
[void]$catalogMd.AppendLine("**สถานะ**: สร้าง `ItemData` Asset จริง + ลงทะเบียนใน `ItemDatabase.asset` ครบทั้งหมด **$grandTotal Artifacts** แล้ว (19 อาวุธ × 12 ชิ้นเฉพาะตัว = $totalSpecific + $($UniversalArtifacts.Count) ชิ้น Universal ปืนทุกกระบอก, ไม่รวมระบบธาตุ พิษ/ไฟ/น้ำแข็ง/ช็อต)")
[void]$catalogMd.AppendLine("")
[void]$catalogMd.AppendLine("## กติกาการติดตั้ง Artifact ต่ออาวุธ 1 ชิ้น (สูงสุด 4 ช่อง A1–A4)")
[void]$catalogMd.AppendLine("- **Common**: ใส่ได้ **0–4 ใบ**")
[void]$catalogMd.AppendLine("- **Rare**: ใส่ได้ **สูงสุด 2 ใบ (1–2 ใบ)**")
[void]$catalogMd.AppendLine("- **Legendary**: ใส่ได้ **1 ใบเท่านั้น**")
[void]$catalogMd.AppendLine("")
[void]$catalogMd.AppendLine("## สรุปจำนวน Artifact ของแต่ละอาวุธ (Tracker)")
[void]$catalogMd.AppendLine("")
[void]$catalogMd.AppendLine("| # | อาวุธ (Weapon) | หมวดหมู่ | จำนวนที่มีแล้ว | สัดส่วน (C / R / L) | โฟลเดอร์ Asset |")
[void]$catalogMd.AppendLine("|---|---|---|---|---|---|")

$wIdx = 0
$totalC = 0; $totalR = 0; $totalL = 0
foreach ($w in $Weapons) {
    $wIdx++
    $cnt = $w.list.Count
    $cCnt = ($w.list | Where-Object { $_.rarity -eq 0 }).Count
    $rCnt = ($w.list | Where-Object { $_.rarity -eq 1 }).Count
    $lCnt = ($w.list | Where-Object { $_.rarity -eq 3 }).Count
    $totalC += $cCnt; $totalR += $rCnt; $totalL += $lCnt
    $extraNote = if ($w.category -eq "Ranged") { " (+$($UniversalArtifacts.Count) Universal)" } else { "" }
    [void]$catalogMd.AppendLine("| $wIdx | **$($w.weapon)** | $($w.category) | **$cnt / 12**$extraNote | $cCnt Common / $rCnt Rare / $lCnt Legendary | ``Assets/Resources_Addressable/Database/ItemData/Artifacts/$($w.folder)`` |")
}
$uR = @($UniversalArtifacts | Where-Object { $_.artifact.rarity -eq 1 }).Count
$totalR += $uR
[void]$catalogMd.AppendLine("| + | **Universal Ranged (ปืนทุกชนิด)** | Ranged (All 9) | **$($UniversalArtifacts.Count)** | 0 Common / $uR Rare / 0 Legendary | ``Assets/Resources_Addressable/Database/ItemData/Artifacts/00_Universal`` |")
[void]$catalogMd.AppendLine("| | **รวมทั้งหมด** | | **$grandTotal Artifacts** | **$totalC Common / $totalR Rare / $totalL Legendary** | |")
[void]$catalogMd.AppendLine("")
[void]$catalogMd.AppendLine("---")
[void]$catalogMd.AppendLine("")

$globalIndex = 0

# Helper to emit a single ItemData YAML + meta
function Write-ArtifactAsset([string]$dir, [string]$assetName, [object]$a, [string[]]$compatWeapons) {
    $assetPath = Join-Path $dir "$assetName.asset"
    $guid = Get-DeterministicGuid $assetName
    $allGeneratedGuids.Add($guid)

    $rc = $RarityColors[$a.rarity]
    $safeDesc = ($a.desc -replace '"', '\"')
    $safeShort = ($a.short -replace '"', '\"')
    $safeName = ($a.name -replace '"', '\"')

    $compatLines = ($compatWeapons | ForEach-Object { "    - `"$_`"" }) -join "`r`n"

    $yaml = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $itemDataScriptGuid, type: 3}
  m_Name: $assetName
  m_EditorClassIdentifier: TheLastEmpire.Runtime::TheLastEmpire.ItemData
  itemName: "$safeName"
  description: "$safeDesc"
  icon: $($a.icon)
  type: 10
  restorationValue: 0
  sanityRestorationValue: 0
  waterRestorationValue: 0
  maxStackSize: 99
  defaultQuantity: 1
  themeColor: {r: $($rc.r), g: $($rc.g), b: $($rc.b), a: 1}
  dropMaterial: {fileID: 0}
  damage: 0
  attackRadius: 0
  attackRate: 0
  knockbackForce: 0
  staggerDuration: 0
  weaponConfig:
    projectilePrefab: {fileID: 0}
    fireRate: 0.2
    magazineSize: 12
    reloadDuration: 1
    range: 3
    canPierce: 0
    spreadAngle: 0
    pelletsPerShot: 1
    isAutomatic: 0
    ammoType: 
    vfxPoolKey: 
    projectileVisualId: 
  artifactConfig:
    rarity: $($a.rarity)
    compatibleWeaponNames:
$compatLines
    bonusDamage: $($a.dmg)
    damageMultiplier: $($a.dmgPct)
    bonusPelletsPerShot: $($a.pellets)
    extraSpreadAngle: $($a.spread)
    fireRateMultiplier: $($a.rate)
    bonusMagazineSize: $($a.mag)
    reloadSpeedMultiplier: $($a.reload)
    bonusRange: $($a.range)
    grantPierce: $($a.pierce)
    grantAutomatic: $($a.auto)
    bonusCritChance: $($a.crit)
    bonusCritMultiplier: $($a.critDmg)
    bonusKnockback: $($a.kb)
    bonusStagger: $($a.stag)
    lifestealOnHit: $($a.leech)
    ammoSaveChance: $($a.save)
    bonusBurstShots: $($a.burst)
    projectileScaleBonus: $($a.scale)
    shortEffectSummary: "$safeShort"
"@
    [System.IO.File]::WriteAllText($assetPath, $yaml, (New-Object System.Text.UTF8Encoding($false)))
    Write-AssetMeta $assetPath $guid
}

$wNum = 0
foreach ($w in $Weapons) {
    $wNum++
    $weaponName = $w.weapon
    $folderName = $w.folder
    $weaponDir = Join-Path $artifactsRoot $folderName
    if (-not (Test-Path $weaponDir)) { New-Item -ItemType Directory -Path $weaponDir -Force | Out-Null }
    Write-FolderMeta $weaponDir $folderName

    [void]$catalogMd.AppendLine("### $wNum. $weaponName ($($w.category)) — $($w.list.Count) Artifacts")
    [void]$catalogMd.AppendLine("| # | Asset ID | ชื่อ Artifact | ระดับ | ความสามารถจริงในเกม |")
    [void]$catalogMd.AppendLine("|---|---|---|---|---|")

    $localIdx = 0
    foreach ($a in $w.list) {
        $globalIndex++
        $localIdx++
        $slug = ($a.name -replace '[^A-Za-z0-9]', '')
        $weaponSlug = ($weaponName -replace '[^A-Za-z0-9]', '')
        $assetName = ("ART{0:D3}_{1}_{2:D2}_{3}" -f $globalIndex, $weaponSlug, $localIdx, $slug)
        Write-ArtifactAsset $weaponDir $assetName $a @($weaponName)

        $rName = $RarityNames[$a.rarity]
        [void]$catalogMd.AppendLine("| $localIdx | ``$assetName`` | $($a.name) | $rName | $($a.desc) (``$($a.short)``) |")
    }
    [void]$catalogMd.AppendLine("")
}

# Generate Universal Ranged Artifacts
[void]$catalogMd.AppendLine("### 20. Universal Ranged Artifacts (ใส่ได้กับปืนทุกชนิดทั้ง 9 กระบอก)")
[void]$catalogMd.AppendLine("| # | Asset ID | ชื่อ Artifact | ระดับ | อาวุธที่ใส่ได้ | ความสามารถจริงในเกม |")
[void]$catalogMd.AppendLine("|---|---|---|---|---|---|")
$uIdx = 0
foreach ($u in $UniversalArtifacts) {
    $globalIndex++
    $uIdx++
    $uDir = Join-Path $artifactsRoot $u.folder
    if (-not (Test-Path $uDir)) { New-Item -ItemType Directory -Path $uDir -Force | Out-Null }
    Write-FolderMeta $uDir $u.folder

    $a = $u.artifact
    $slug = ($a.name -replace '[^A-Za-z0-9]', '')
    $assetName = ("ART{0:D3}_Universal_{1:D2}_{2}" -f $globalIndex, $uIdx, $slug)
    Write-ArtifactAsset $uDir $assetName $a $u.weapons

    $rName = $RarityNames[$a.rarity]
    [void]$catalogMd.AppendLine("| $uIdx | ``$assetName`` | $($a.name) | $rName | $($u.scope) | $($a.desc) (``$($a.short)``) |")
}
[void]$catalogMd.AppendLine("")

# Register all 228 Artifact GUIDs into ItemDatabase.asset (avoiding duplicates)
$dbPath = Join-Path $ProjectRoot "Assets\Resources_Addressable\Database\ItemDatabase.asset"
$dbText = [System.IO.File]::ReadAllText($dbPath, (New-Object System.Text.UTF8Encoding($false)))
$addedToDb = 0
$appendLines = New-Object System.Text.StringBuilder
foreach ($g in $allGeneratedGuids) {
    if ($dbText -notmatch $g) {
        [void]$appendLines.AppendLine("  - {fileID: 11400000, guid: $g, type: 2}")
        $addedToDb++
    }
}
if ($addedToDb -gt 0) {
    if (-not $dbText.EndsWith("`n")) { $dbText += "`n" }
    $dbText += $appendLines.ToString()
    [System.IO.File]::WriteAllText($dbPath, $dbText, (New-Object System.Text.UTF8Encoding($false)))
}

# Write updated catalog & count tracker
$catalogPath = Join-Path $ProjectRoot "Docs\weapon-artifacts-catalog.md"
[System.IO.File]::WriteAllText($catalogPath, $catalogMd.ToString(), (New-Object System.Text.UTF8Encoding($false)))

Write-Output "GENERATED_ARTIFACTS:$globalIndex"
Write-Output "ADDED_TO_ITEMDATABASE:$addedToDb"
Write-Output "WEAPONS_COUNT:$($Weapons.Count)"
