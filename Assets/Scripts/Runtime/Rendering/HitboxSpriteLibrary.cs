using System;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Centralized runtime loader & helper for the stylized hitbox/telegraph sprite sheet
    /// (Assets/Resources_Addressable/Image/UI/UI_Hitbox_Sprites.png):
    ///   [0] UI_Hitbox_Sprites_0 — Hazard / Radiation Circle (Boss Stomp, Boss Leap, Boomer/Kamikaze blast)
    ///   [1] UI_Hitbox_Sprites_1 — Solid Bullseye Target (Grenade AOE, C4 Satchel blast)
    ///   [2] UI_Hitbox_Sprites_2 — Dashed Crosshair Reticle (Sniper Aim Red Reticle, Throw Range ring)
    ///   [3] UI_Hitbox_Sprites_3 — Radial Spiked Burst (Cyclone Swing 360, Toxic Trail hazard)
    ///   [4] UI_Hitbox_Sprites_4 — Solid Corner-Bracket Square Frame (Area zone)
    ///   [5] UI_Hitbox_Sprites_5 — Hollow Corner Lock Brackets + Dot (Grappling Hook Yellow Target Lock)
    ///   [6] UI_Hitbox_Sprites_6 — 4-Quadrant Precision Target Grid
    ///   [7] UI_Hitbox_Sprites_7 — Directional Chevron Arrows >>>> (Boss Charge/Dash, Grapple pull direction)
    /// </summary>
    public static class HitboxSpriteLibrary
    {
        public const string SheetAddress = "Image/UI/UI_Hitbox_Sprites";
        private const string EditorAssetPath = "Assets/Resources_Addressable/Image/UI/UI_Hitbox_Sprites.png";

        // Shared palette tuned for UI_Hitbox_Sprites (base art is Yellow #FFD000 + Black outline)
        public const float DefaultHitboxAlpha = 0.25f;
        public static readonly Color DefaultOriginalColor = new Color(1f, 1f, 1f, DefaultHitboxAlpha);
        public static readonly Color SniperRed = new Color(1.0f, 0.10f, 0.08f, DefaultHitboxAlpha);
        public static readonly Color DangerRed = new Color(1.0f, 0.14f, 0.08f, DefaultHitboxAlpha);
        public static readonly Color GrappleYellow = new Color(1.0f, 1.0f, 1.0f, DefaultHitboxAlpha);
        public static readonly Color RangeSoftTint = new Color(1.0f, 1.0f, 1.0f, DefaultHitboxAlpha);
        public static readonly Color ToxicGreen = new Color(1.0f, 1.0f, 1.0f, DefaultHitboxAlpha);

        private static Sprite[] _cachedSprites;
        private static bool _attemptedLoad;

        public static Sprite HazardCircle => GetSprite(0);
        public static Sprite TargetBullseye => GetSprite(1);
        public static Sprite DashedCrosshair => GetSprite(2);
        public static Sprite RadialBurst => GetSprite(3);
        public static Sprite SolidFrame => GetSprite(4);
        public static Sprite CornerLockBrackets => GetSprite(5);
        public static Sprite QuadrantGrid => GetSprite(6);
        public static Sprite DirectionalChevrons => GetSprite(7);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            _cachedSprites = null;
            _attemptedLoad = false;
        }

        public static Sprite GetSprite(int index)
        {
            if (index < 0 || index > 7) return null;
            EnsureLoaded();
            if (_cachedSprites != null && index < _cachedSprites.Length && _cachedSprites[index] != null)
            {
                return _cachedSprites[index];
            }
            return null;
        }

        private static void EnsureLoaded()
        {
            if (_cachedSprites != null && _cachedSprites.Length >= 8 && _cachedSprites[0] != null)
                return;

            if (_attemptedLoad && _cachedSprites != null)
                return;

            _attemptedLoad = true;
            Sprite[] raw = null;

            // 1. Primary: Addressables (works in Standalone, WebGL, and Editor)
            try
            {
                raw = AddressableManager.LoadAllSprites(SheetAddress);
            }
            catch
            {
                raw = null;
            }

            // 2. Fallback: Editor AssetDatabase (works immediately in Editor even before Addressables rebuild)
#if UNITY_EDITOR
            if (raw == null || raw.Length == 0)
            {
                UnityEngine.Object[] objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
                if (objs != null && objs.Length > 0)
                {
                    var list = new System.Collections.Generic.List<Sprite>(8);
                    for (int i = 0; i < objs.Length; i++)
                    {
                        if (objs[i] is Sprite s) list.Add(s);
                    }
                    raw = list.ToArray();
                }
            }
#endif

            if (raw == null || raw.Length == 0)
                return;

            _cachedSprites = new Sprite[8];
            for (int i = 0; i < raw.Length; i++)
            {
                Sprite s = raw[i];
                if (s == null) continue;

                // Match exact trailing index: UI_Hitbox_Sprites_0 .. UI_Hitbox_Sprites_7
                int idx = -1;
                int underscore = s.name.LastIndexOf('_');
                if (underscore >= 0 && underscore + 1 < s.name.Length)
                {
                    int.TryParse(s.name.Substring(underscore + 1), out idx);
                }

                if (idx >= 0 && idx < 8)
                {
                    _cachedSprites[idx] = s;
                }
                else if (i < 8 && _cachedSprites[i] == null)
                {
                    _cachedSprites[i] = s;
                }
            }
        }

        /// <summary>
        /// Creates a flat ground-projected SpriteRenderer facing straight up toward the top-down camera.
        /// </summary>
        public static SpriteRenderer CreateGroundSpriteIndicator(string name, Transform parent, Sprite sprite, Color color, int sortingOrder = 5)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        /// <summary>
        /// Scales a ground-projected circular/square sprite cleanly to match a world-space diameter (radius * 2).
        /// </summary>
        public static void SetWorldDiameter(SpriteRenderer sr, float worldDiameter)
        {
            if (sr == null || sr.sprite == null) return;
            float baseW = sr.sprite.bounds.size.x;
            float baseH = sr.sprite.bounds.size.y;
            if (baseW <= 0.0001f || baseH <= 0.0001f) return;

            sr.transform.localScale = new Vector3(worldDiameter / baseW, worldDiameter / baseH, 1f);
        }

        /// <summary>
        /// Computes top-down ground rotation so UI_Hitbox_Sprites_7 (chevrons pointing +X in sprite space)
        /// points cleanly along horizontal world direction 'forwardDir' while facing upward (+Y).
        /// </summary>
        public static Quaternion GetGroundChevronRotation(Vector3 forwardDir)
        {
            forwardDir.y = 0f;
            if (forwardDir.sqrMagnitude < 0.0001f) forwardDir = Vector3.forward;
            forwardDir.Normalize();
            float yaw = Mathf.Atan2(-forwardDir.z, forwardDir.x) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
