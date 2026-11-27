using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages all 5 Active Night Combat Items (usable via Inventory [Use] or Quick-Slot hotbar):
    /// 1. Tactical Flashlight  (75s): Directional aim-following light cone cutting through darkness, dazzles enemies (-20% speed), reveals AlienInvisible, -50% night Sanity drain.
    /// 2. Night Vision Goggles (60s): Full-screen phosphor-green NVG clearing night darkness, 0 night Sanity drain, +15% Crit Chance.
    /// 3. Signal Flare         (30s): Fires a glowing rocket flare UP into the sky, deploys an overhead red parachute flare bathing the whole stage in crimson-red light (+15% dmg to enemies, blocks AlienInvisible cloaking, Co-op synced!).
    /// 4. Thermal Bio-Visor    (45s): Highlights all enemies through darkness & obstacles with glowing thermal trackers, reveals AlienInvisible 100%, +20% bonus damage vs ALL Aliens (including Alien Overlord!).
    /// 5. UV Flashbang         (Instant): Blinding UV flash across the screen (2.2s), stuns enemies within 8.5m for 3.5s, reveals AlienInvisible for 8s, deals 180 UV dmg (380 vs Aliens!).
    /// </summary>
    public class NightCombatItemManager : MonoBehaviour
    {
        public static NightCombatItemManager Instance { get; private set; }

        public const string ItemTacticalFlashlight = "Tactical Flashlight";
        public const string ItemNightVisionGoggles = "Night Vision Goggles";
        public const string ItemSignalFlare = "Signal Flare";
        public const string ItemThermalBioVisor = "Thermal Bio-Visor";
        public const string ItemUVFlashbang = "UV Flashbang";

        public const float FlashlightDuration = 75f;
        public const float NvgDuration = 60f;
        public const float SignalFlareDuration = 30f;
        public const float ThermalVisorDuration = 45f;
        public const float UvFlashbangStunDuration = 3.5f;
        public const float UvFlashbangRadius = 8.5f;

        private static readonly int PropFlashlightDir = Shader.PropertyToID("_GlobalFlashlightDir");
        private static readonly int PropNvgStrength = Shader.PropertyToID("_GlobalNvgStrength");
        private static readonly int PropRedFlareStrength = Shader.PropertyToID("_GlobalRedFlareStrength");
        private static readonly int PropFlashbangStrength = Shader.PropertyToID("_GlobalFlashbangStrength");

        // Active effect timers
        public float FlashlightTimer { get; private set; }
        public float NvgTimer { get; private set; }
        public float RedFlareTimer { get; private set; }
        public float ThermalVisorTimer { get; private set; }
        public float FlashbangTimer { get; private set; }

        public bool IsFlashlightActive => FlashlightTimer > 0f;
        public bool IsNvgActive => NvgTimer > 0f;
        public bool IsRedFlareActive => RedFlareTimer > 0f;
        public bool IsThermalVisorActive => ThermalVisorTimer > 0f;
        public bool HasScreenOverlayVisual => IsNvgActive || IsRedFlareActive || FlashbangTimer > 0f;

        // 3D Lights & Sky Flare VFX
        private Light _flashlightSpot;
        private GameObject _flashlightObj;
        private Light _redSkyFlareLight;
        private GameObject _redSkyFlareRoot;
        private float _dazzleTickTimer;

        // Smooth shader ramps
        private float _flashlightAlpha;
        private float _nvgAlpha;
        private float _redFlareAlpha;
        private float _flashbangAlpha;

        // Active stage flare cache on Host for Late-Join & stage-entry sync
        private struct ActiveStageFlareEntry
        {
            public Vector3 Position;
            public float ExpireUnscaledTime;
        }
        private readonly Dictionary<(int, int, int), ActiveStageFlareEntry> _hostActiveStageFlares = new Dictionary<(int, int, int), ActiveStageFlareEntry>();
        private int _lastLocalStageX = int.MinValue;
        private int _lastLocalStageY = int.MinValue;
        private int _lastLocalInteriorId = int.MinValue;

        // Status toast & cached GUI
        private string _toastText = "";
        private Color _toastColor = Color.white;
        private float _toastTimer;
        private Texture2D _whiteTex;
        private GUIStyle _pillStyle;
        private GUIStyle _thermalLabelStyle;
        private GUIStyle _toastStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[NightCombatItemManager]");
                DontDestroyOnLoad(go);
                go.AddComponent<NightCombatItemManager>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResetShaderGlobals();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ResetShaderGlobals();
            Cleanup3DLights();
            if (_whiteTex != null)
            {
                Destroy(_whiteTex);
                _whiteTex = null;
            }
        }

        private static bool IsGameplaySceneActive()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.IndexOf("Gameplay", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void ResetAllNightCombatEffects()
        {
            FlashlightTimer = 0f;
            NvgTimer = 0f;
            RedFlareTimer = 0f;
            ThermalVisorTimer = 0f;
            FlashbangTimer = 0f;
            _toastTimer = 0f;
            _toastText = "";

            _flashlightAlpha = 0f;
            _nvgAlpha = 0f;
            _redFlareAlpha = 0f;
            _flashbangAlpha = 0f;

            _hostActiveStageFlares.Clear();
            _lastLocalStageX = int.MinValue;
            _lastLocalStageY = int.MinValue;
            _lastLocalInteriorId = 0;

            CleanupSkyFlare();
            CleanupFlashlight();
            ResetShaderGlobals();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsGameplaySceneActive())
            {
                // Returning to MainMenu or any non-gameplay scene: wipe NVG green overlay, Thermal, Flashlight, Flare & Flashbang!
                ResetAllNightCombatEffects();
                return;
            }

            // Clear stage-bound sky flare & flashbang on gameplay scene reload
            RedFlareTimer = 0f;
            FlashbangTimer = 0f;
            _redFlareAlpha = 0f;
            _flashbangAlpha = 0f;
            CleanupSkyFlare();
            CleanupFlashlight();
            ResetShaderGlobals();
        }

        private static void ResetShaderGlobals()
        {
            Shader.SetGlobalVector(PropFlashlightDir, Vector4.zero);
            Shader.SetGlobalFloat(PropNvgStrength, 0f);
            Shader.SetGlobalFloat(PropRedFlareStrength, 0f);
            Shader.SetGlobalFloat(PropFlashbangStrength, 0f);
        }

        // =========================================================================
        //  ITEM IDENTIFICATION & ACTIVE USE ENTRY POINT
        // =========================================================================

        public static bool IsNightCombatItem(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return false;
            string n = itemName.Trim();
            return string.Equals(n, ItemTacticalFlashlight, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(n, ItemNightVisionGoggles, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(n, ItemSignalFlare, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(n, ItemThermalBioVisor, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(n, ItemUVFlashbang, StringComparison.OrdinalIgnoreCase);
        }

        public bool TryActivateItem(string itemName, PlayerController user)
        {
            if (string.IsNullOrWhiteSpace(itemName) || user == null) return false;
            string n = itemName.Trim();

            if (string.Equals(n, ItemTacticalFlashlight, StringComparison.OrdinalIgnoreCase))
            {
                FlashlightTimer = FlashlightDuration;
                ShowToast($"TACTICAL FLASHLIGHT ON ({FlashlightDuration:F0}s)", new Color(1f, 0.94f, 0.65f));
                return true;
            }

            if (string.Equals(n, ItemNightVisionGoggles, StringComparison.OrdinalIgnoreCase))
            {
                NvgTimer = NvgDuration;
                ShowToast($"NIGHT VISION GOGGLES ACTIVE ({NvgDuration:F0}s | +15% CRIT)", new Color(0.32f, 1f, 0.45f));
                return true;
            }

            if (string.Equals(n, ItemSignalFlare, StringComparison.OrdinalIgnoreCase))
            {
                Vector3 launchPos = user.transform.position + Vector3.up * 1.1f;
                ActivateRedSkyFlareLocal(launchPos, SignalFlareDuration);
                int sx = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
                int sy = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
                int interiorId = BuildingInteriorManager.CurrentInteriorId;
                if (EmpireNetworkManager.IsOnlineHost)
                {
                    RecordHostActiveStageFlare(sx, sy, interiorId, launchPos, SignalFlareDuration);
                }
                BroadcastNightCombatEffectToCoop(effectType: 1, launchPos, SignalFlareDuration);
                ShowToast($"RED SIGNAL FLARE LAUNCHED! ({SignalFlareDuration:F0}s | +15% DMG)", new Color(1f, 0.30f, 0.28f));
                return true;
            }

            if (string.Equals(n, ItemThermalBioVisor, StringComparison.OrdinalIgnoreCase))
            {
                ThermalVisorTimer = ThermalVisorDuration;
                ShowToast($"THERMAL BIO-VISOR ONLINE ({ThermalVisorDuration:F0}s | +20% VS ALIENS)", new Color(1f, 0.56f, 0.18f));
                return true;
            }

            if (string.Equals(n, ItemUVFlashbang, StringComparison.OrdinalIgnoreCase))
            {
                Vector3 aimDir = GetPlayerAimFlatDir(user);
                Vector3 blastPos = user.transform.position + aimDir * 2.4f;
                blastPos.y = 0.5f;
                // On Co-op Clients: play 0ms visual feedback locally, and let Host authoritatively apply stun/reveal/damage ONCE (prevents double damage!)
                bool applyCombatLocally = !EmpireNetworkManager.IsOnlineClient;
                TriggerUvFlashbangLocal(blastPos, applyDamageAndStun: applyCombatLocally);
                BroadcastNightCombatEffectToCoop(effectType: 2, blastPos, 0f);
                ShowToast("UV SOLAR FLASHBANG!  (STUN 3.5s + ANTI-ALIEN BURN)", new Color(0.85f, 0.55f, 1f));
                return true;
            }

            return false;
        }

        // =========================================================================
        //  COMBAT & SURVIVAL MODIFIER QUERIES
        // =========================================================================

        public float GetNightSanityDrainMultiplier()
        {
            if (IsNvgActive || IsRedFlareActive) return 0f; // Full dark-sanity immunity!
            if (IsFlashlightActive) return 0.5f;            // 50% reduced night sanity loss
            return 1f;
        }

        public float GetBonusCritChance()
        {
            return IsNvgActive ? 0.15f : 0f;
        }

        public float GetEnemyDamageTakenMultiplier(GameObject enemyObj)
        {
            if (enemyObj == null) return 1f;
            float mult = 1f;
            if (IsRedFlareActive)
            {
                mult *= 1.15f; // +15% damage while whole stage is bathed in Red Sky Flare light!
            }
            if (IsThermalVisorActive)
            {
                bool isAlien = enemyObj.GetComponent<AlienAI>() != null ||
                               enemyObj.name.IndexOf("Alien", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isAlien)
                {
                    mult *= 1.20f; // +20% bonus xenomorph weakpoint damage (works on Alien Overlord too!)
                }
            }
            return mult;
        }

        public bool IsPointIlluminatedByFlashlight(Vector3 worldPos)
        {
            if (IsFlashlightActive)
            {
                PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
                if (IsPointInPlayerFlashlightCone(pc, GetPlayerAimFlatDir(pc), worldPos))
                {
                    return true;
                }
            }

            // Also check Co-op teammates in the same stage who have their Tactical Flashlight turned on!
            var allPlayers = PlayerRegistry.AllPlayers;
            if (allPlayers != null)
            {
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    PlayerController p = allPlayers[i];
                    if (p == null || !p.IsRemotePlayer || !p.IsRemoteFlashlightOn || !p.IsInSameStageAsLocalPlayer) continue;
                    Vector3 aim = p.AimDirection.sqrMagnitude > 0.01f ? p.AimDirection : p.transform.forward;
                    aim.y = 0f;
                    if (aim.sqrMagnitude > 0.01f && IsPointInPlayerFlashlightCone(p, aim.normalized, worldPos))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsPointInPlayerFlashlightCone(PlayerController pc, Vector3 aimDir, Vector3 worldPos)
        {
            if (pc == null) return false;
            Vector3 toTarget = worldPos - pc.transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist > 14f) return false;
            if (dist < 2.2f) return true;
            float dot = Vector3.Dot(toTarget.normalized, aimDir);
            return dot >= 0.85f; // ~31 degree half-cone
        }

        public bool HasAnyActiveFlashlightInStage()
        {
            if (IsFlashlightActive) return true;
            var allPlayers = PlayerRegistry.AllPlayers;
            if (allPlayers != null)
            {
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    PlayerController p = allPlayers[i];
                    if (p != null && p.IsRemotePlayer && p.IsRemoteFlashlightOn && p.IsInSameStageAsLocalPlayer)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool ShouldForceRevealInvisibleAlien(Vector3 alienPos)
        {
            if (IsRedFlareActive || IsThermalVisorActive) return true;
            if (IsPointIlluminatedByFlashlight(alienPos)) return true;
            return false;
        }

        // =========================================================================
        //  CO-OP MULTIPLAYER SYNC (Signal Flare & UV Flashbang + Late-Join Cache)
        // =========================================================================

        private void RecordHostActiveStageFlare(int stageX, int stageY, int interiorId, Vector3 pos, float duration)
        {
            if (duration <= 0.1f) return;
            _hostActiveStageFlares[(stageX, stageY, interiorId)] = new ActiveStageFlareEntry
            {
                Position = pos,
                ExpireUnscaledTime = Time.unscaledTime + duration
            };
        }

        public void SendActiveStageFlaresToClient(ulong targetClientId, int filterStageX = int.MinValue, int filterStageY = int.MinValue, int filterInteriorId = int.MinValue)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;
            if (_hostActiveStageFlares.Count == 0) return;

            float now = Time.unscaledTime;
            List<(int, int, int)> expired = null;

            foreach (var kvp in _hostActiveStageFlares)
            {
                float remain = kvp.Value.ExpireUnscaledTime - now;
                if (remain <= 0.5f)
                {
                    if (expired == null) expired = new List<(int, int, int)>();
                    expired.Add(kvp.Key);
                    continue;
                }

                var key = kvp.Key;
                if (filterStageX != int.MinValue && (key.Item1 != filterStageX || key.Item2 != filterStageY || key.Item3 != filterInteriorId))
                {
                    continue;
                }

                Vector3 pos = kvp.Value.Position;
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.NightCombatEffect, w =>
                {
                    w.Write(ulong.MaxValue); // Server-sync sentinel so target client always accepts
                    w.Write(key.Item1);
                    w.Write(key.Item2);
                    w.Write(key.Item3);
                    w.Write((byte)1);
                    w.Write(pos.x);
                    w.Write(pos.y);
                    w.Write(pos.z);
                    w.Write(remain);
                }, targetClientId);
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                {
                    _hostActiveStageFlares.Remove(expired[i]);
                }
            }
        }

        private void BroadcastNightCombatEffectToCoop(byte effectType, Vector3 pos, float duration)
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;
            int sx = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int sy = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int interiorId = BuildingInteriorManager.CurrentInteriorId;
            ulong myClientId = EmpireNetworkManager.Instance.LocalClientId;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.NightCombatEffect, w =>
            {
                w.Write(myClientId);
                w.Write(sx);
                w.Write(sy);
                w.Write(interiorId);
                w.Write(effectType);
                w.Write(pos.x);
                w.Write(pos.y);
                w.Write(pos.z);
                w.Write(duration);
            });
        }

        public void HandleRemoteNightCombatEffect(ulong senderClientId, int stageX, int stageY, int interiorId, byte effectType, Vector3 pos, float duration, bool relayIfHost)
        {
            float effectiveDuration = duration > 0.1f ? duration : SignalFlareDuration;

            if (relayIfHost && EmpireNetworkManager.Instance != null)
            {
                if (effectType == 1)
                {
                    RecordHostActiveStageFlare(stageX, stageY, interiorId, pos, effectiveDuration);
                }

                // Relay preserving original senderClientId so originating client ignores the echo!
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.NightCombatEffect, w =>
                {
                    w.Write(senderClientId);
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(interiorId);
                    w.Write(effectType);
                    w.Write(pos.x);
                    w.Write(pos.y);
                    w.Write(pos.z);
                    w.Write(effectiveDuration);
                });
            }

            // Skip echo back to the player who triggered this item (prevents double VFX & toast!)
            if (EmpireNetworkManager.Instance != null && senderClientId == EmpireNetworkManager.Instance.LocalClientId)
            {
                return;
            }

            int myX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int myInt = BuildingInteriorManager.CurrentInteriorId;
            if (myX != stageX || myY != stageY || myInt != interiorId) return;

            if (effectType == 1)
            {
                bool alreadyActive = RedFlareTimer > 0.5f;
                ActivateRedSkyFlareLocal(pos, effectiveDuration);
                if (!alreadyActive && senderClientId != ulong.MaxValue)
                {
                    ShowToast("TEAMMATE LAUNCHED RED SIGNAL FLARE!", new Color(1f, 0.35f, 0.30f));
                }
            }
            else if (effectType == 2)
            {
                // Host applies authoritative stun/reveal/damage ONCE for client-thrown flashbang; other clients play VFX only
                bool applyCombat = EmpireNetworkManager.IsOnlineHost;
                TriggerUvFlashbangLocal(pos, applyDamageAndStun: applyCombat);
            }
        }

        // =========================================================================
        //  SIGNAL FLARE (SHOOT INTO SKY -> OVERHEAD RED PARACHUTE FLARE!)
        // =========================================================================

        private void ActivateRedSkyFlareLocal(Vector3 launchPos, float duration)
        {
            RedFlareTimer = Mathf.Max(RedFlareTimer, duration);
            StartCoroutine(LaunchRedSkyFlareRoutine(launchPos, duration));
        }

        private IEnumerator LaunchRedSkyFlareRoutine(Vector3 launchPos, float duration)
        {
            CleanupSkyFlare();

            _redSkyFlareRoot = new GameObject("RedSkySignalFlare");
            _redSkyFlareRoot.transform.position = launchPos;

            // Glowing red flare core sphere
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "FlareCore";
            Collider c = core.GetComponent<Collider>();
            if (c != null) Destroy(c);
            core.transform.SetParent(_redSkyFlareRoot.transform, false);
            core.transform.localScale = Vector3.one * 0.42f;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material coreMat = unlit != null ? new Material(unlit) : null;
            if (coreMat != null)
            {
                Color brightRed = new Color(1f, 0.20f, 0.14f, 1f);
                coreMat.color = brightRed;
                if (coreMat.HasProperty("_BaseColor")) coreMat.SetColor("_BaseColor", brightRed);
                Renderer r = core.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = coreMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // Overhead crimson-red PointLight illuminating the entire ground stage!
            GameObject lightObj = new GameObject("RedSkyFlareLight");
            lightObj.transform.SetParent(_redSkyFlareRoot.transform, false);
            _redSkyFlareLight = lightObj.AddComponent<Light>();
            _redSkyFlareLight.type = LightType.Point;
            _redSkyFlareLight.color = new Color(1f, 0.18f, 0.14f);
            _redSkyFlareLight.range = 42f;
            _redSkyFlareLight.intensity = 7.5f;
            _redSkyFlareLight.shadows = LightShadows.None;

            // Phase 1: Shoot straight UP into the night sky (0.65s) with bright red spark trail!
            float launchDuration = 0.65f;
            float elapsed = 0f;
            Vector3 apexPos = new Vector3(launchPos.x, 12.0f, launchPos.z);
            float sparkTimer = 0f;

            while (elapsed < launchDuration && _redSkyFlareRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / launchDuration);
                float easeOut = 1f - Mathf.Pow(1f - t, 2.5f);
                _redSkyFlareRoot.transform.position = Vector3.Lerp(launchPos, apexPos, easeOut);
                _redSkyFlareLight.intensity = Mathf.Lerp(4.0f, 10.5f, easeOut);

                sparkTimer -= Time.deltaTime;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = 0.04f;
                    SpawnFlareSpark(_redSkyFlareRoot.transform.position, new Color(1f, 0.25f, 0.15f, 0.9f), 0.22f, 0.45f);
                }
                yield return null;
            }

            // Phase 2: Burst flare canopy & slowly drift downward while bathing the stage in crimson-red light!
            if (_redSkyFlareRoot != null)
            {
                for (int b = 0; b < 8; b++)
                {
                    SpawnFlareSpark(apexPos + UnityEngine.Random.insideUnitSphere * 0.6f, new Color(1f, 0.35f, 0.18f, 0.95f), 0.34f, 0.75f);
                }

                float burnElapsed = 0f;
                float burnDuration = Mathf.Max(1f, duration - launchDuration);
                Vector3 endPos = new Vector3(launchPos.x, 6.2f, launchPos.z);

                while (burnElapsed < burnDuration && _redSkyFlareRoot != null && RedFlareTimer > 0f)
                {
                    burnElapsed += Time.deltaTime;
                    float bt = Mathf.Clamp01(burnElapsed / burnDuration);
                    Vector3 curP = Vector3.Lerp(apexPos, endPos, bt);
                    curP.x += Mathf.Sin(burnElapsed * 1.3f) * 0.45f;
                    _redSkyFlareRoot.transform.position = curP;

                    float flicker = 8.5f + Mathf.Sin(burnElapsed * 14f) * 1.4f + Mathf.Cos(burnElapsed * 23f) * 0.8f;
                    float fadeOut = bt > 0.88f ? (1f - (bt - 0.88f) / 0.12f) : 1f;
                    _redSkyFlareLight.intensity = flicker * fadeOut;

                    sparkTimer -= Time.deltaTime;
                    if (sparkTimer <= 0f)
                    {
                        sparkTimer = 0.16f;
                        SpawnFlareSpark(curP, new Color(1f, 0.22f, 0.15f, 0.85f), 0.18f, 0.75f);
                    }
                    yield return null;
                }
            }

            if (coreMat != null) Destroy(coreMat);
            CleanupSkyFlare();
        }

        private void SpawnFlareSpark(Vector3 pos, Color color, float size, float lifetime)
        {
            StartCoroutine(FlareSparkRoutine(pos, color, size, lifetime));
        }

        private static IEnumerator FlareSparkRoutine(Vector3 pos, Color color, float size, float lifetime)
        {
            GameObject sp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider c = sp.GetComponent<Collider>();
            if (c != null) Destroy(c);
            sp.transform.position = pos;
            sp.transform.localScale = Vector3.one * size;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material m = unlit != null ? new Material(unlit) : null;
            if (m != null)
            {
                m.color = color;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                Renderer r = sp.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = m;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            Vector3 vel = new Vector3(UnityEngine.Random.Range(-0.9f, 0.9f), UnityEngine.Random.Range(-2.6f, -0.8f), UnityEngine.Random.Range(-0.9f, 0.9f));
            float t = 0f;
            while (t < lifetime)
            {
                t += Time.deltaTime;
                sp.transform.position += vel * Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / lifetime);
                sp.transform.localScale = Vector3.one * (size * k);
                yield return null;
            }

            if (m != null) Destroy(m);
            Destroy(sp);
        }

        // =========================================================================
        //  UV SOLAR FLASHBANG (BLINDING FLASH + 3.5s STUN + ANTI-ALIEN BURN!)
        // =========================================================================

        private void TriggerUvFlashbangLocal(Vector3 blastPos, bool applyDamageAndStun)
        {
            FlashbangTimer = 2.2f;
            _flashbangAlpha = 1.0f;
            StartCoroutine(UvFlashbangWaveVfx(blastPos));

            if (!applyDamageAndStun) return;

            if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                var enemies = new List<GameObject>(GameManager.Instance.ActiveEnemies);
                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject e = enemies[i];
                    if (e == null || !e.activeInHierarchy) continue;

                    float dist = Vector3.Distance(
                        new Vector3(e.transform.position.x, 0f, e.transform.position.z),
                        new Vector3(blastPos.x, 0f, blastPos.z));
                    if (dist > UvFlashbangRadius) continue;

                    BaseEnemyAI ai = e.GetComponent<BaseEnemyAI>();
                    AlienAI alien = e.GetComponent<AlienAI>();
                    bool isAlien = alien != null || e.name.IndexOf("Alien", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (ai != null)
                    {
                        // Stun enemy for 3.5 seconds (knocks them into Stagger state cleanly!)
                        Vector3 pushDir = (e.transform.position - blastPos);
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude < 0.01f) pushDir = Vector3.forward;
                        ai.ApplyMeleeKnockback(pushDir.normalized, 2.5f, UvFlashbangStunDuration);
                    }

                    if (alien != null)
                    {
                        alien.ForceRevealFromStealth(8.0f);
                    }

                    Health h = e.GetComponent<Health>();
                    if (h != null && !h.IsDead)
                    {
                        float uvDamage = isAlien ? 380f : 180f;
                        h.TakeDamage(uvDamage, e.transform.position + Vector3.up * 1.2f, isCrit: isAlien);
                    }
                }
            }
        }

        private static IEnumerator UvFlashbangWaveVfx(Vector3 center)
        {
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "UVFlashbangDome";
            Collider c = dome.GetComponent<Collider>();
            if (c != null) Destroy(c);
            dome.transform.position = center;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = unlit != null ? new Material(unlit) : null;
            Color uvCol = new Color(0.84f, 0.45f, 1f, 0.72f);
            if (mat != null)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.color = uvCol;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", uvCol);
                Renderer r = dome.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            GameObject lightObj = new GameObject("UVFlashLight");
            lightObj.transform.position = center + Vector3.up * 2.5f;
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.85f, 0.52f, 1f);
            l.range = 28f;
            l.intensity = 16f;
            l.shadows = LightShadows.None;

            float elapsed = 0f;
            const float dur = 0.65f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(0.8f, UvFlashbangRadius * 2f, ease);
                dome.transform.localScale = Vector3.one * scale;
                l.intensity = 16f * (1f - t);

                if (mat != null)
                {
                    Color cc = uvCol;
                    cc.a = uvCol.a * (1f - t);
                    mat.color = cc;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cc);
                }
                yield return null;
            }

            if (mat != null) Destroy(mat);
            Destroy(dome);
            Destroy(lightObj);
        }

        // =========================================================================
        //  UPDATE LOOP: TIMERS, SHADER UNIFORMS, 3D SPOTLIGHT & FLASHLIGHT DAZZLE
        // =========================================================================

        private void Update()
        {
            if (!IsGameplaySceneActive())
            {
                if (IsFlashlightActive || IsNvgActive || IsRedFlareActive || IsThermalVisorActive || FlashbangTimer > 0f || _nvgAlpha > 0f || _redFlareAlpha > 0f || _flashbangAlpha > 0f || _flashlightAlpha > 0f)
                {
                    ResetAllNightCombatEffects();
                }
                else
                {
                    ResetShaderGlobals();
                    if (_flashlightObj != null && _flashlightObj.activeSelf) _flashlightObj.SetActive(false);
                }
                return;
            }

            float dt = Time.deltaTime;

            if (FlashlightTimer > 0f) FlashlightTimer = Mathf.Max(0f, FlashlightTimer - dt);
            if (NvgTimer > 0f) NvgTimer = Mathf.Max(0f, NvgTimer - dt);
            if (RedFlareTimer > 0f) RedFlareTimer = Mathf.Max(0f, RedFlareTimer - dt);
            if (ThermalVisorTimer > 0f) ThermalVisorTimer = Mathf.Max(0f, ThermalVisorTimer - dt);
            if (FlashbangTimer > 0f) FlashbangTimer = Mathf.Max(0f, FlashbangTimer - dt);
            if (_toastTimer > 0f) _toastTimer = Mathf.Max(0f, _toastTimer - Time.unscaledDeltaTime);

            // Smooth fade curves
            _flashlightAlpha = Mathf.MoveTowards(_flashlightAlpha, IsFlashlightActive ? 1f : 0f, dt * 4.5f);
            _nvgAlpha = Mathf.MoveTowards(_nvgAlpha, IsNvgActive ? 1f : 0f, dt * 3.5f);
            _redFlareAlpha = Mathf.MoveTowards(_redFlareAlpha, IsRedFlareActive ? 1f : 0f, dt * 2.2f);
            _flashbangAlpha = FlashbangTimer > 0f ? Mathf.Pow(Mathf.Clamp01(FlashbangTimer / 2.2f), 1.6f) : 0f;

            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            UpdateFlashlightBeamAndSpot(pc);

            Shader.SetGlobalFloat(PropNvgStrength, _nvgAlpha);
            Shader.SetGlobalFloat(PropRedFlareStrength, _redFlareAlpha);
            Shader.SetGlobalFloat(PropFlashbangStrength, _flashbangAlpha);

            // Clear stage-bound Red Sky Flare when local player leaves the flared stage or enters/exits an interior
            int curStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int curInteriorId = BuildingInteriorManager.CurrentInteriorId;
            if (_lastLocalStageX != int.MinValue &&
                (_lastLocalStageX != curStageX || _lastLocalStageY != curStageY || _lastLocalInteriorId != curInteriorId))
            {
                RedFlareTimer = 0f;
                CleanupSkyFlare();

                // If Host re-entered a stage that still has an active flare in cache, restore it locally
                if (EmpireNetworkManager.IsOnlineHost &&
                    _hostActiveStageFlares.TryGetValue((curStageX, curStageY, curInteriorId), out ActiveStageFlareEntry cachedFlare))
                {
                    float rem = cachedFlare.ExpireUnscaledTime - Time.unscaledTime;
                    if (rem > 0.5f)
                    {
                        ActivateRedSkyFlareLocal(cachedFlare.Position, rem);
                    }
                }
            }
            _lastLocalStageX = curStageX;
            _lastLocalStageY = curStageY;
            _lastLocalInteriorId = curInteriorId;

            // Apply Tactical Flashlight dazzle slow & AlienInvisible reveal every 0.25s
            // (Authoritative on Host/Offline for both local & remote teammates' flashlights; Client runs stealth reveal locally)
            if (HasAnyActiveFlashlightInStage())
            {
                _dazzleTickTimer -= dt;
                if (_dazzleTickTimer <= 0f)
                {
                    _dazzleTickTimer = 0.25f;
                    ApplyFlashlightConeEffects();
                }
            }
        }

        private void UpdateFlashlightBeamAndSpot(PlayerController pc)
        {
            if (_flashlightAlpha <= 0.01f || pc == null)
            {
                Shader.SetGlobalVector(PropFlashlightDir, Vector4.zero);
                if (_flashlightObj != null && _flashlightObj.activeSelf) _flashlightObj.SetActive(false);
                return;
            }

            Vector3 aimDir = GetPlayerAimFlatDir(pc);
            Camera cam = Camera.main;
            Vector2 screenDir = new Vector2(0f, 1f);
            if (cam != null)
            {
                Vector3 p0 = cam.WorldToViewportPoint(pc.transform.position);
                Vector3 p1 = cam.WorldToViewportPoint(pc.transform.position + aimDir * 4f);
                Vector2 delta = new Vector2((p1.x - p0.x) * ((float)Screen.width / Mathf.Max(1, Screen.height)), p1.y - p0.y);
                if (delta.sqrMagnitude > 0.0001f) screenDir = delta.normalized;
            }

            Shader.SetGlobalVector(PropFlashlightDir, new Vector4(screenDir.x, screenDir.y, 0.78f, _flashlightAlpha));

            // 3D Tactical SpotLight following aim direction
            if (_flashlightObj == null)
            {
                _flashlightObj = new GameObject("TacticalFlashlightSpot");
                DontDestroyOnLoad(_flashlightObj);
                _flashlightSpot = _flashlightObj.AddComponent<Light>();
                _flashlightSpot.type = LightType.Spot;
                _flashlightSpot.color = new Color(1f, 0.96f, 0.85f);
                _flashlightSpot.spotAngle = 56f;
                _flashlightSpot.range = 18f;
                _flashlightSpot.shadows = LightShadows.None;
            }

            if (!_flashlightObj.activeSelf) _flashlightObj.SetActive(true);
            _flashlightObj.transform.position = pc.transform.position + Vector3.up * 1.25f + aimDir * 0.35f;
            _flashlightObj.transform.rotation = Quaternion.LookRotation((aimDir + Vector3.down * 0.14f).normalized);
            _flashlightSpot.intensity = 6.5f * _flashlightAlpha;
        }

        private void ApplyFlashlightConeEffects()
        {
            if (GameManager.Instance == null || GameManager.Instance.ActiveEnemies == null) return;
            var enemies = GameManager.Instance.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                GameObject e = enemies[i];
                if (e == null || !e.activeInHierarchy) continue;
                if (!IsPointIlluminatedByFlashlight(e.transform.position)) continue;

                AlienAI alien = e.GetComponent<AlienAI>();
                if (alien != null)
                {
                    alien.ForceRevealFromStealth(0.65f);
                }

                // Gently dazzle/slow enemies caught directly inside the bright beam
                Rigidbody rb = e.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 v = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(v.x * 0.80f, v.y, v.z * 0.80f);
                }
            }
        }

        private static Vector3 GetPlayerAimFlatDir(PlayerController pc)
        {
            if (pc == null) return Vector3.forward;
            Camera cam = Camera.main;
            if (cam != null && UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                Ray ray = cam.ScreenPointToRay(mousePos);
                Plane ground = new Plane(Vector3.up, new Vector3(0f, pc.transform.position.y, 0f));
                if (ground.Raycast(ray, out float enter))
                {
                    Vector3 dir = ray.GetPoint(enter) - pc.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) return dir.normalized;
                }
            }
            Vector3 fwd = pc.transform.forward;
            fwd.y = 0f;
            return fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        }

        private void CleanupFlashlight()
        {
            if (_flashlightObj != null)
            {
                Destroy(_flashlightObj);
                _flashlightObj = null;
                _flashlightSpot = null;
            }
        }

        private void CleanupSkyFlare()
        {
            if (_redSkyFlareRoot != null)
            {
                Destroy(_redSkyFlareRoot);
                _redSkyFlareRoot = null;
                _redSkyFlareLight = null;
            }
        }

        private void Cleanup3DLights()
        {
            CleanupFlashlight();
            CleanupSkyFlare();
        }

        private void ShowToast(string msg, Color col)
        {
            _toastText = msg;
            _toastColor = col;
            _toastTimer = 3.2f;
        }

        // =========================================================================
        //  HUD: ACTIVE NIGHT BUFF PILLS + THERMAL BIO-VISOR ENEMY TRACKERS
        // =========================================================================

        private void EnsureGuiStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            if (_pillStyle == null)
            {
                _pillStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_thermalLabelStyle == null)
            {
                _thermalLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _thermalLabelStyle.normal.textColor = new Color(1f, 0.72f, 0.22f, 1f);
            }
            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private void OnGUI()
        {
            if (!IsGameplaySceneActive()) return;

            bool anyBuff = IsFlashlightActive || IsNvgActive || IsRedFlareActive || IsThermalVisorActive;
            if (!anyBuff && _toastTimer <= 0f) return;

            EnsureGuiStyles();
            Color prev = GUI.color;

            // 1. Thermal Bio-Visor: Draw glowing orange-red heat tracker boxes OVER night darkness for EVERY enemy!
            if (IsThermalVisorActive && GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null && Camera.main != null)
            {
                Camera cam = Camera.main;
                var enemies = GameManager.Instance.ActiveEnemies;
                float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 8f);

                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject e = enemies[i];
                    if (e == null || !e.activeInHierarchy) continue;
                    Health h = e.GetComponent<Health>();
                    if (h != null && h.IsDead) continue;

                    Vector3 sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 0.9f);
                    if (sp.z <= 0f) continue;

                    float gx = sp.x;
                    float gy = Screen.height - sp.y;
                    bool isAlien = e.GetComponent<AlienAI>() != null || e.name.IndexOf("Alien", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isBoss = h != null && h.MaxHealth >= 10000f;
                    float boxSize = isBoss ? 68f : 38f;
                    Rect box = new Rect(gx - boxSize * 0.5f, gy - boxSize * 0.5f, boxSize, boxSize);

                    Color heatColor = isAlien
                        ? new Color(1f, 0.22f, 0.14f, 0.92f * pulse)  // Hot crimson-orange for Aliens (+20% DMG!)
                        : new Color(1f, 0.62f, 0.12f, 0.82f * pulse); // Amber-orange for Zombies/Robots

                    // Subtle glowing core fill + crisp 4-edge thermal bracket
                    GUI.color = new Color(heatColor.r, heatColor.g, heatColor.b, 0.16f * pulse);
                    GUI.DrawTexture(box, _whiteTex);
                    GUI.color = heatColor;
                    float t = isBoss ? 3f : 2f;
                    GUI.DrawTexture(new Rect(box.x, box.y, box.width, t), _whiteTex);
                    GUI.DrawTexture(new Rect(box.x, box.yMax - t, box.width, t), _whiteTex);
                    GUI.DrawTexture(new Rect(box.x, box.y, t, box.height), _whiteTex);
                    GUI.DrawTexture(new Rect(box.xMax - t, box.y, t, box.height), _whiteTex);

                    if (isAlien)
                    {
                        GUI.color = Color.white;
                        GUI.Label(new Rect(box.x - 24f, box.y - 16f, box.width + 48f, 16f), isBoss ? "XENO BOSS +20%" : "ALIEN +20%", _thermalLabelStyle);
                    }
                }
            }

            // 2. Active Night Combat Buff Timer Pills (Top-Right below clock)
            if (anyBuff)
            {
                float px = Screen.width - 236f;
                float py = 112f;

                if (IsFlashlightActive)
                {
                    DrawBuffPill(new Rect(px, py, 220f, 24f), $"FLASHLIGHT  {FlashlightTimer:F0}s", new Color(1f, 0.90f, 0.45f));
                    py += 28f;
                }
                if (IsNvgActive)
                {
                    DrawBuffPill(new Rect(px, py, 220f, 24f), $"NVG (+15% CRIT)  {NvgTimer:F0}s", new Color(0.30f, 1f, 0.45f));
                    py += 28f;
                }
                if (IsRedFlareActive)
                {
                    DrawBuffPill(new Rect(px, py, 220f, 24f), $"RED FLARE (+15% DMG)  {RedFlareTimer:F0}s", new Color(1f, 0.30f, 0.25f));
                    py += 28f;
                }
                if (IsThermalVisorActive)
                {
                    DrawBuffPill(new Rect(px, py, 220f, 24f), $"THERMAL (+20% ALIEN)  {ThermalVisorTimer:F0}s", new Color(1f, 0.56f, 0.16f));
                }
            }

            // 3. Activation Toast Banner
            if (_toastTimer > 0f && !string.IsNullOrEmpty(_toastText))
            {
                float alpha = Mathf.Clamp01(_toastTimer / 0.5f);
                float w = Mathf.Min(500f, Screen.width * 0.52f);
                Rect tr = new Rect((Screen.width - w) * 0.5f, Screen.height - 135f, w, 30f);
                GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.80f * alpha);
                GUI.DrawTexture(tr, _whiteTex);
                _toastStyle.normal.textColor = new Color(_toastColor.r, _toastColor.g, _toastColor.b, alpha);
                GUI.color = Color.white;
                GUI.Label(tr, _toastText, _toastStyle);
            }

            GUI.color = prev;
        }

        private void DrawBuffPill(Rect rect, string text, Color accent)
        {
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.82f);
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), _whiteTex);
            _pillStyle.normal.textColor = accent;
            GUI.color = Color.white;
            GUI.Label(rect, text, _pillStyle);
        }
    }
}
