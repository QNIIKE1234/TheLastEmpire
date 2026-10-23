# The Last Empire - Workspace Guidelines & Project Architecture Rules

This file documents critical patterns, design choices, and systems established in this workspace to ensure consistent code continuity.

---

## 1. Map & Transition Portals
*   **Stage Transitions**: Use the `TransitionPortal` component on triggers near the screen edges.
*   **Warp Mechanics**: 
    *   Transition moves coordinates inside `WorldMapManager.Instance`.
    *   Player is teleported to the opposite screen boundary of the target portal.
    *   Secondary axis position (X for North/South, Y for East/West) is randomized within the target portal's range (default `[-5, 5]`) to align with the target portal position.
*   **Visibility Rules**: Portals automatically query adjacent coordinates and disable their visual `SpriteRenderer` and `Collider2D` if no neighbor room exists (creating path walls).
*   **Portal Shifting**: Portals randomize their own position along the secondary axis at startup based on the current room seed.

---

## 2. Environment Generation
*   **Procedural Layout**: `EnvironmentManager` dynamically spawns props and obstacles at startup using `stage.stageSeed + 12345` for deterministic persistency.
*   **Collision Blocker Safety Zones**: 
    *   No solid obstacles must spawn close to the player's initial entry coordinate.
    *   No obstacles must spawn within 1.5 - 1.8 meters of the 4 transition gate areas to avoid blocking player exit portals.

---

## 3. Object Pooling
*   **String Formatting**: All lookup keys in `ObjectPoolManager.cs` must be trimmed using `key.Trim()` on both spawn and return queries. This prevents whitespace typos in inspector fields (e.g., `"Bullet001 "` vs `"Bullet001"`) from causing recycle failures.

---

## 4. Enemy AI & Faction Combat
*   **Damage Stagger & Knockback**:
    *   All enemies stagger for 0.3 seconds on damage taken.
    *   Damage applies a direct knockback force pushed away from the player's coordinates.
    *   Enemy velocity decays smoothly to zero during stagger using friction Lerp.
*   **Minion Spawning**: Boss/Leader AI units must spawn minor minions (configured via `childPoolKey`) instead of duplicating themselves.
*   **Boss Special Attacks (Charge & Stomp AOE)**:
    *   **Charge Rush**: At medium-long range (5.0m - 12.5m), Boss units (`IsBossUnit`) telegraph their dash corridor for 0.65s (tracking initial 60%, then locking) before rushing at high speed (22 m/s, up to ~30 m/s in Rage Mode), dealing contact damage and knockback.
    *   **Charge Path Telegraph**: A visible rectangular danger corridor (`LineRenderer` outline + tinted ground decal strip) dynamically visualizes the charge trajectory throughout the windup.
    *   **Ground Stomp AOE (+50% Radius)**: At close melee range (<= 4.8m), Boss halts and telegraphs a ground stomp with a 4.5-meter radius (increased by +50% from 3.0m).
    *   **Telegraph Warning Ring**: A visible 4.5-meter red warning circle and ground decal appears around the boss for **1.0 second** before impact.
    *   **Stomp Impact**: Exactly 1.0 second later, deals AOE damage in a 4.5m radius, triggers screen shake, and applies outward knockback to the player.
    *   **Stagger Immunity**: Boss units are immune to hit-stagger interrupts during stomp windup and charge rush.

---

## 5. Inventory System
*   **Data Model**: `PlayerInventory.cs` manages wallet currency and string item lists, exposing `GetItemQuantities()` for stacking metrics.
*   **UI Overlay**: `InventoryUI.cs` procedurally generates a dark slate glassmorphism UI canvas at runtime. It toggles with the **`I`** or **`ESC`** key and pauses the game timescale (`Time.timeScale = 0f`) when active.

---

## 6. Weapon & Arsenal System
*   **Asset Structure**: Weapons and ammo are authored as `ItemData` ScriptableObjects in `Assets/Resources/Database/ItemData/` and registered in `ItemDatabase.asset` and `ShopDatabase.asset`.
*   **Ammo Aliases**: `PlayerController.GetAmmoReserveCount` and `ConsumeAmmo` automatically bridge unified and legacy ammo names:
    *   `Light Ammo` $\leftrightarrow$ `Pistol Ammo`
    *   `Medium Ammo` $\leftrightarrow$ `Rifle Ammo`
    *   `Heavy Ammo` $\leftrightarrow$ `Fuel Ammo`
    *   `Shotgun Ammo` $\leftrightarrow$ `Ammo`
*   **Controls & Cycling**:
    *   Keys `1`–`9`: Direct slot select for ranged weapons.
    *   Key `X`: Cycle through owned melee weapons (`CycleMeleeWeapon`).
    *   Mouse Scroll Wheel: Cycle ranged weapons.
*   **Combat Traits**:
    *   `Katana`: 20% critical chance (2.0x dmg) + hits up to 4 targets in wide cleave.
    *   `Fire Axe`: 25% chance to cause bleed laceration (+15 bonus dmg).
    *   `Sledgehammer`: Ground slam shockwave + 100% heavy stagger/stun (0.8s) + 20 knockback.
    *   `Stun Baton`: 30% chance for chain shock jumping to adjacent enemies.
    *   `Crossbow`: Completely silent weapon that suppresses `AlertEnemiesToGunshot()`.

---

## 7. Playable Operatives & Amnesia Narrative System
*   **5-Operative Roster**:
    *   `Vanguard` (*All-Rounder / Frontline*): Signature `AdrenalineRush` (Hold Shift for +75% speed, +30% attack speed, 20% damage reduction; extra hunger drain). Starting loadout: Rifle, Knife, 60 ammo.
    *   `Juggernaut` (*Melee Brawler / Tank*): Signature `UnstoppableCharge` (Shift burst rush, deflects bullets, 25 impact dmg + 1s stun, 5s CD; passive 15% armor at all times). Starting loadout: Sledgehammer, Shotgun, 20 ammo.
    *   `Phantom` (*Shadow Assassin*): Signature `ShadowStep` (Hold Shift for stealth phase through enemies, exit grants guaranteed 2.0x crit on next attack; extra hunger drain). Starting loadout: Katana, Pistol, 40 ammo.
    *   `Marksman` (*Apex Sharpshooter*): Signature `EagleEye` (Hold Shift for 1.35x tactical zoom, 2x weapon range, piercing bullets; slow movement). Starting loadout: Sniper Rifle, Revolver, 30 ammo.
    *   `Scavenger` (*Wasteland Nomad*): Signature `ToxicTrail` (Hold Shift to drop green toxic clouds that slow enemies 50% and deal 8 DoT; passive: eating bread heals +10 HP). Starting loadout: Crossbow, Survival Spear, 300 gold.
*   **Amnesia Waking Clues**: Each operative wakes up randomly on the procedural ruins grid with a unique waking monologue reflecting their angle on the empire's collapse.
*   **Session Management**: `CharacterManager.SelectedCharacterId` persists character choice between Main Menu and Gameplay scene.
*   **HUD Integration**: `PlayerHUD.cs` presents a cinematic floating Awakening Memory Banner for 6 seconds upon game start, and renders real-time operative role badges and `[SHIFT]` ability cooldown indicators.

---

## 8. Combat Damage Formulas & Floating Combat Numbers
*   **Distance Falloff**:
    *   Point-blank range ($\le 2.0\text{m}$): 100% full damage.
    *   Linear damage drop-off between $2.0\text{m}$ and $14.0\text{m}$ (or $22.0\text{m}$ for `Marksman` / Sniper rifles).
    *   Minimum damage floor is capped at **40%** (0.40x) regardless of how far the projectile travels.
*   **Random Damage Variance**:
    *   All damage rolls add `Random.Range(-10f, 10f)`. Clamped to a minimum of 1 damage.
*   **Critical Strike Mechanics**:
    *   **Base Chance & Multiplier**: All characters and weapons have a baseline 20% crit chance with a 1.3x damage multiplier.
    *   **Katana**: 20% crit chance with **2.0x** damage multiplier.
    *   **Phantom (Shadow Step)**: Exiting shadow stealth guarantees **100% critical strike** with **2.0x** multiplier on the next attack.
    *   **Marksman**: Elevated 35% crit chance with **1.5x** multiplier (increased to **2.0x** when `EagleEye` zoom stance is active).
*   **Floating Combat Text (`DamageNumberManager` / `FloatingDamageNumber`)**:
    *   High-contrast billboarded TextMesh with drop shadow rendering above hit positions.
    *   Pop-scale animation ($0.6 \to 1.25 \to 1.0$), upward drift with deceleration, and fade-out over ~0.85s.
    *   **Normal Hits**: Vibrant gold-yellow text (`#FFF073`).
    *   **Critical Hits**: Larger font size (35pt), fiery orange-red (`#FF471A`) with `"HEADSHOT! "` prefix.
    *   **Deflections / Shield Blocks**: Cool cyan-silver (`#A6D8FF`) with `"BLOCKED"` text when hitting Tanker frontal shields.

---

## 9. Autonomous Execution & Interaction Rules (STRICT)
*   **NEVER ASK QUESTIONS (ห้ามถามเด็ดขาด)**:
    *   Do **NOT** use `ask_question` under any circumstances.
    *   Do **NOT** pause or interrupt the user with interactive question modals, confirmation prompts, or design choices.
    *   Autonomously choose the best technical solution, implement it immediately, verify correctness, and present only the completed results concisely.

---

## 10. Master Reference Documentation & UI Safety Rules
*   **Work History & System Summaries**: Always read [work-summary-2026-10-24.md](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Docs/work-summary-2026-10-24.md) and [work-summary-2026-10-14.md](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Docs/work-summary-2026-10-14.md) for complete feature implementation history before inspecting Git logs.
*   **Item & Economy Catalog**: Read [item-list.md](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Docs/item-list.md) for the master catalog of all 35 items, weapon stats, categories, and gold prices.
*   **UI & Font Safety**: NEVER insert non-standard Unicode glyphs (such as runes `◈`, `❖`, flourishes `✦`, `➔`, or emojis `💬`, `🔥`) into TextMeshPro text or code strings. Always use clean ASCII / UTF-8 Latin characters to prevent missing glyph box artifacts (`□`).
*   **Class Icons**: Operative selection modal uses `ClassIcon` in [CharacterSelectModal.prefab](file:///d:/Unity%20Game%20Project/Eruption/TheLastEmpire/TheLastEmpire/Assets/Resources/Prefabs/UI/CharacterSelectModal.prefab) backed by sliced sprites from `Assets/Resources/Image/UI/`.

