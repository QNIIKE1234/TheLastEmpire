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
