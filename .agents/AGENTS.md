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

---

## 5. Inventory System
*   **Data Model**: `PlayerInventory.cs` manages wallet currency and string item lists, exposing `GetItemQuantities()` for stacking metrics.
*   **UI Overlay**: `InventoryUI.cs` procedurally generates a dark slate glassmorphism UI canvas at runtime. It toggles with the **`I`** or **`ESC`** key and pauses the game timescale (`Time.timeScale = 0f`) when active.
