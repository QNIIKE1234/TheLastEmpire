using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    public enum SeasonType : byte
    {
        Autumn = 0, // Days 1-5: Mild + occasional Rain
        Rainy  = 1, // Days 6-10: Rain & Thunderstorms
        Winter = 2, // Days 11-15: Snow & Blizzards
        Summer = 3  // Days 16-20: Warm + occasional Thunderstorms
    }

    public enum WeatherType : byte
    {
        Clear        = 0,
        Rain         = 1,
        Thunderstorm = 2,
        Snow         = 3,
        Blizzard     = 4
    }

    /// <summary>
    /// Full Dynamic 5-Day Season & 4-Weather System (Co-op Multiplayer Synchronized).
    /// - Seasons rotate every 5 days (~30 real minutes): Autumn -> Rainy -> Winter -> Summer.
    /// - Weather rolls randomly every 60 seconds (10% chance, lasts 30s) choosing season-matched weather:
    ///     * Rain         : Overcast gloom + +25% denser rain + splashes + wet ground + 2x survival drain outdoors
    ///     * Thunderstorm : Heavy dark gloom + heavy windy rain + real Co-op synced lightning strikes (telegraph -> bolt -> AOE shock) + 2x drain
    ///     * Snow         : Crisp cool winter tint + soft snowflakes + world-space snow on ground & building roofs (melts around campfires)
    ///     * Blizzard     : Harsh frosty gloom + fierce howling snow + frost screen vignette + 2x drain + -18% chill slow (prevented indoors/near campfire)
    /// - 100% hidden & sheltered inside building interiors (BuildingInteriorManager.IsInsideInterior).
    /// - Host-authoritative Co-op sync via NetPacketType.WeatherSync & NetPacketType.LightningStrikeSync (including [F1] debug triggers & Late-Join).
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Season Settings")]
        [Tooltip("Number of in-game days per season (5 days = 30 real minutes at 6 min/day)")]
        [SerializeField] private int daysPerSeason = 5;

        [Header("Random Weather Schedule")]
        [Tooltip("Interval between random weather rolls in seconds (default: 60s)")]
        [SerializeField] private float rollIntervalSeconds = 60f;
        [Tooltip("Chance to trigger weather on each roll (0.10 = 10%)")]
        [Range(0f, 1f)]
        [SerializeField] private float weatherChance = 0.10f;
        [Tooltip("Minimum duration of each weather event in seconds (60s = 1 minute)")]
        [SerializeField] private float minWeatherDurationSeconds = 60f;
        [Tooltip("Maximum duration of each weather event in seconds (360s = 6 minutes)")]
        [SerializeField] private float maxWeatherDurationSeconds = 360f;

        [Header("Visual & Audio Tuning")]
        [SerializeField] private float fadeInSpeed = 0.45f;
        [SerializeField] private float fadeOutSpeed = 0.35f;
        [SerializeField] private float outdoorRainVolume = 0.18f;
        [SerializeField] private float outdoorStormVolume = 0.25f;
        [SerializeField] private float outdoorWindVolume = 0.18f;
        [SerializeField] private float indoorAudioVolume = 0f;

        [Header("Gameplay Effects (Outdoors Only)")]
        [Tooltip("Sanity/Hunger/Thirst drain multiplier while exposed to Rain, Thunderstorm, or Blizzard outdoors")]
        [SerializeField] private float severeSurvivalDrainMultiplier = 2.0f;
        [Tooltip("Sanity/Hunger/Thirst drain multiplier while exposed to gentle Snow outdoors (when away from campfire)")]
        [SerializeField] private float snowSurvivalDrainMultiplier = 1.4f;
        [Tooltip("Movement speed multiplier during Blizzard outdoors when away from a campfire (0.82 = -18% slow)")]
        [SerializeField] private float blizzardMoveSpeedMultiplier = 0.82f;
        [Tooltip("HP damage per second while exposed to Blizzard outdoors away from a campfire (default: 3 HP/s)")]
        [SerializeField] private float blizzardDamagePerSecond = 3.0f;

        [Header("Thunderstorm Lightning Strikes")]
        [SerializeField] private float minLightningInterval = 4.8f;
        [SerializeField] private float maxLightningInterval = 8.0f;
        [SerializeField] private float lightningTelegraphDuration = 1.35f;
        [SerializeField] private float lightningStrikeRadius = 2.3f;
        [SerializeField] private float lightningEnemyDamage = 160f;
        [SerializeField] private float lightningPlayerDamage = 28f;

        // Public State Queries
        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
        public bool IsRaining => CurrentWeather == WeatherType.Rain || CurrentWeather == WeatherType.Thunderstorm;
        public bool IsSnowing => CurrentWeather == WeatherType.Snow || CurrentWeather == WeatherType.Blizzard;
        public bool IsPlayerSheltered => BuildingInteriorManager.IsInsideInterior || BuildingInteriorManager.IsTransitioning;
        public bool IsPlayerExposedToWeather => CurrentWeather != WeatherType.Clear && !IsPlayerSheltered && IsGameplaySceneActive();
        public bool IsPlayerExposedToRain => IsRaining && !IsPlayerSheltered && IsGameplaySceneActive();
        public bool IsPlayerExposedToBlizzard => CurrentWeather == WeatherType.Blizzard && !IsPlayerSheltered && IsGameplaySceneActive();
        public bool IsShiftBlockedByWeather => IsPlayerExposedToWeather &&
            (CurrentWeather == WeatherType.Thunderstorm || CurrentWeather == WeatherType.Blizzard);

        public int DaysPerSeason => Mathf.Max(1, daysPerSeason);
        public float RollIntervalSeconds => rollIntervalSeconds;
        public float RainChance => weatherChance;
        public float MinWeatherDurationSeconds => Mathf.Max(5f, minWeatherDurationSeconds);
        public float MaxWeatherDurationSeconds => Mathf.Max(MinWeatherDurationSeconds, maxWeatherDurationSeconds);
        public float RainDurationSeconds => MaxWeatherDurationSeconds;
        public float WeatherRemainingSeconds => CurrentWeather != WeatherType.Clear ? Mathf.Max(0f, _weatherRemainingTimer) : 0f;
        public float RainRemainingSeconds => WeatherRemainingSeconds;
        public float NextRollRemainingSeconds => CurrentWeather != WeatherType.Clear ? 0f : Mathf.Max(0f, _rollTimer);

        public static string FormatWeatherDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            if (total >= 60)
            {
                return $"{total / 60}:{total % 60:D2}";
            }
            return $"{total}s";
        }

        private float RollRandomWeatherDuration()
        {
            return Random.Range(MinWeatherDurationSeconds, MaxWeatherDurationSeconds);
        }

        public SeasonType CurrentSeason
        {
            get
            {
                if (_hasSeasonOverride) return _seasonOverride;
                int day = DayNightManager.Instance != null ? Mathf.Max(1, DayNightManager.Instance.DayCount) : 1;
                int idx = ((day - 1) / DaysPerSeason) % 4;
                return (SeasonType)idx;
            }
        }

        public int DayInCurrentSeason
        {
            get
            {
                int day = DayNightManager.Instance != null ? Mathf.Max(1, DayNightManager.Instance.DayCount) : 1;
                return ((day - 1) % DaysPerSeason) + 1;
            }
        }

        public bool IsNearCampfireWarmth
        {
            get
            {
                if (CampfireBuilding.ActiveCampfires == null || CampfireBuilding.ActiveCampfires.Count == 0) return false;
                PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
                if (pc == null) return false;
                Vector3 p = pc.transform.position;
                for (int i = 0; i < CampfireBuilding.ActiveCampfires.Count; i++)
                {
                    CampfireBuilding cf = CampfireBuilding.ActiveCampfires[i];
                    if (cf == null || !cf.gameObject.activeInHierarchy) continue;
                    float rad = Mathf.Max(5.5f, cf.VisionAuraRadius);
                    Vector3 d = cf.transform.position - p;
                    d.y = 0f;
                    if (d.sqrMagnitude <= rad * rad) return true;
                }
                return false;
            }
        }

        public float SurvivalDrainMultiplier
        {
            get
            {
                if (!IsPlayerExposedToWeather) return 1.0f;
                switch (CurrentWeather)
                {
                    case WeatherType.Rain:
                    case WeatherType.Thunderstorm:
                        return severeSurvivalDrainMultiplier;
                    case WeatherType.Blizzard:
                        return IsNearCampfireWarmth ? 1.0f : severeSurvivalDrainMultiplier;
                    case WeatherType.Snow:
                        return IsNearCampfireWarmth ? 1.0f : snowSurvivalDrainMultiplier;
                    default:
                        return 1.0f;
                }
            }
        }

        public float MoveSpeedMultiplier
        {
            get
            {
                if (IsPlayerExposedToBlizzard && !IsNearCampfireWarmth)
                {
                    return blizzardMoveSpeedMultiplier;
                }
                return 1.0f;
            }
        }

        public float CropGrowthRateMultiplier => CurrentSeason == SeasonType.Rainy || IsRaining ? 1.35f : 1.0f;

        // Smooth visual blend values (0..1)
        public float RainIntensity { get; private set; } = 0f;
        public float StormIntensity { get; private set; } = 0f;
        public float SnowParticleIntensity { get; private set; } = 0f;
        public float BlizzardIntensity { get; private set; } = 0f;
        public float GroundWetness { get; private set; } = 0f;
        public float GroundSnowAmount { get; private set; } = 0f;
        public float LightningFlashAlpha => _lightningFlashAlpha;

        // Shader Globals
        private static readonly int PropGlobalWetness = Shader.PropertyToID("_GlobalWetness");
        private static readonly int PropGlobalSnowAmount = Shader.PropertyToID("_GlobalSnowAmount");
        private static readonly int PropGlobalCampfireCount = Shader.PropertyToID("_GlobalCampfireCount");
        private static readonly int PropGlobalCampfireData = Shader.PropertyToID("_GlobalCampfireData");

        private readonly Vector4[] _campfireShaderArray = new Vector4[4];

        // Internal schedule & season override state
        private bool _hasSeasonOverride = false;
        private SeasonType _seasonOverride = SeasonType.Autumn;
        private float _rollTimer = 60f;
        private float _weatherRemainingTimer = 0f;
        private float _lightningStrikeTimer = 5f;
        private float _ambientFlashTimer = 7f;
        private float _lightningFlashAlpha = 0f;
        private float _blizzardDamageTimer = 0f;

        // Particle Rigs
        private GameObject _weatherRigRoot;
        private ParticleSystem _rainDropPS;
        private ParticleSystem _rainSplashPS;
        private ParticleSystem _snowFlakePS;
        private Texture2D _rainDropTex;
        private Texture2D _rainSplashTex;
        private Texture2D _snowFlakeTex;
        private Material _rainDropMat;
        private Material _rainSplashMat;
        private Material _snowFlakeMat;

        // Building / Prop Roof Snow Cap Overlay (Graphics.DrawMesh, zero prefab edits!)
        private Material _snowCapMaterial;
        private struct SnowMeshTarget
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }
        private readonly List<SnowMeshTarget> _snowTargets = new List<SnowMeshTarget>(128);
        private float _snowTargetRefreshTimer = 0f;

        // Procedural Audio
        private AudioSource _rainAudioSource;
        private AudioSource _windAudioSource;
        private AudioSource _thunderAudioSource;
        private AudioClip _rainLoopClip;
        private AudioClip _windLoopClip;
        private AudioClip _thunderClip;

        // Overlays & HUD (Overcast gloom veil, Blizzard frost vignette, Compact weather pill)
        private Texture2D _whiteTex;
        private Texture2D _frostVignetteTex;
        private GUIStyle _hudLabelStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[WeatherManager]");
                DontDestroyOnLoad(go);
                go.AddComponent<WeatherManager>();
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

            _rollTimer = rollIntervalSeconds;
            ResetGlobalWeatherShaders();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ResetGlobalWeatherShaders();
            CleanupResources();
        }

        private static void ResetGlobalWeatherShaders()
        {
            Shader.SetGlobalFloat(PropGlobalWetness, 0f);
            Shader.SetGlobalFloat(PropGlobalSnowAmount, 0f);
            Shader.SetGlobalInt(PropGlobalCampfireCount, 0);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _snowTargets.Clear();
            _snowTargetRefreshTimer = 0f;
            if (!IsGameplaySceneActive())
            {
                // Returning to MainMenu or any non-gameplay scene: wipe all active weather, flashes, particles, audio & shaders!
                StopAllCoroutines();
                CurrentWeather = WeatherType.Clear;
                _weatherRemainingTimer = 0f;
                _rollTimer = rollIntervalSeconds;
                _hasSeasonOverride = false;
                RainIntensity = 0f;
                StormIntensity = 0f;
                SnowParticleIntensity = 0f;
                BlizzardIntensity = 0f;
                GroundWetness = 0f;
                GroundSnowAmount = 0f;
                _lightningFlashAlpha = 0f;
                _blizzardDamageTimer = 0f;

                SetAllWeatherParticlesActive(false);
                if (_weatherRigRoot != null) _weatherRigRoot.SetActive(false);
                ResetGlobalWeatherShaders();
                MuteAllAudio();
            }
            else if (_weatherRigRoot != null && !_weatherRigRoot.activeSelf)
            {
                _weatherRigRoot.SetActive(true);
            }
        }

        private static bool IsGameplaySceneActive()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.IndexOf("Gameplay", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // =========================================================================
        //  PUBLIC CONTROLS ([F1] Debug Menu & Gameplay API — Co-op Synced!)
        // =========================================================================

        public void ResetWeatherForNewRun()
        {
            StopAllCoroutines();
            CurrentWeather = WeatherType.Clear;
            _weatherRemainingTimer = 0f;
            _rollTimer = rollIntervalSeconds;
            _hasSeasonOverride = false;
            RainIntensity = 0f;
            StormIntensity = 0f;
            SnowParticleIntensity = 0f;
            BlizzardIntensity = 0f;
            GroundWetness = 0f;
            GroundSnowAmount = 0f;
            _lightningFlashAlpha = 0f;
            _blizzardDamageTimer = 0f;
            _snowTargets.Clear();
            _snowTargetRefreshTimer = 0f;

            SetAllWeatherParticlesActive(false);
            ResetGlobalWeatherShaders();
            MuteAllAudio();
        }

        public void SetWeather(WeatherType weather, float durationSeconds = -1f)
        {
            float dur = weather == WeatherType.Clear
                ? 0f
                : (durationSeconds > 0.1f ? durationSeconds : RollRandomWeatherDuration());

            if (EmpireNetworkManager.IsOnlineClient && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
                {
                    w.Write((byte)weather);
                    w.Write(dur);
                    w.Write((byte)_seasonOverride);
                    w.Write(_hasSeasonOverride);
                    w.Write(false);
                });
                return;
            }

            ApplyWeatherStateLocal(weather, dur, _seasonOverride, _hasSeasonOverride, snapInstant: false);
            BroadcastWeatherFromHost(snapInstant: false);
        }

        public void SetSeasonOverride(SeasonType season, bool autoMatchSnow = true)
        {
            if (EmpireNetworkManager.IsOnlineClient && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
                {
                    w.Write((byte)CurrentWeather);
                    w.Write(WeatherRemainingSeconds);
                    w.Write((byte)season);
                    w.Write(true);
                    w.Write(false);
                });
                return;
            }

            _hasSeasonOverride = true;
            _seasonOverride = season;
            if (autoMatchSnow && season == SeasonType.Winter && GroundSnowAmount < 0.35f)
            {
                GroundSnowAmount = 0.55f;
            }
            BroadcastWeatherFromHost(snapInstant: false);
        }

        public void ClearSeasonOverride()
        {
            if (EmpireNetworkManager.IsOnlineClient && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
                {
                    w.Write((byte)CurrentWeather);
                    w.Write(WeatherRemainingSeconds);
                    w.Write((byte)SeasonType.Autumn);
                    w.Write(false);
                    w.Write(false);
                });
                return;
            }

            _hasSeasonOverride = false;
            BroadcastWeatherFromHost(snapInstant: false);
        }

        public void TriggerRain(float durationSeconds = -1f) => SetWeather(WeatherType.Rain, durationSeconds);
        public void StopRain() => SetWeather(WeatherType.Clear, 0f);

        public void ForceRollNow()
        {
            WeatherType rolled = RollSeasonWeather();
            if (Random.value < weatherChance)
            {
                SetWeather(rolled, RollRandomWeatherDuration());
            }
            else
            {
                SetWeather(WeatherType.Clear, 0f);
            }
        }

        public void TriggerDebugLightningStrikeNow()
        {
            if (!IsGameplaySceneActive() || IsPlayerSheltered) return;
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc == null) return;

            Vector3 fwd = pc.AimDirection.sqrMagnitude > 0.01f ? pc.AimDirection.normalized : pc.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 targetPos = pc.transform.position + fwd.normalized * 5.2f;
            targetPos.y = 0f;

            int sx = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int sy = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;

            if (EmpireNetworkManager.IsOnlineClient && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.LightningStrikeSync, w =>
                {
                    w.Write(sx);
                    w.Write(sy);
                    w.Write(targetPos.x);
                    w.Write(targetPos.y);
                    w.Write(targetPos.z);
                    w.Write(lightningTelegraphDuration);
                });
                return;
            }

            ExecuteLightningStrikeAndBroadcast(sx, sy, targetPos, lightningTelegraphDuration);
        }

        private WeatherType RollSeasonWeather()
        {
            float r = Random.value;
            switch (CurrentSeason)
            {
                case SeasonType.Rainy:
                    return r < 0.55f ? WeatherType.Rain : WeatherType.Thunderstorm;
                case SeasonType.Winter:
                    return r < 0.60f ? WeatherType.Snow : WeatherType.Blizzard;
                case SeasonType.Summer:
                    return r < 0.40f ? WeatherType.Rain : WeatherType.Thunderstorm;
                case SeasonType.Autumn:
                default:
                    return r < 0.85f ? WeatherType.Rain : WeatherType.Thunderstorm;
            }
        }

        // =========================================================================
        //  MULTIPLAYER CO-OP SYNC (WeatherSync & LightningStrikeSync)
        // =========================================================================

        public void SendWeatherStateToClient(ulong targetClientId)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;
            BroadcastWeatherFromHost(snapInstant: true, targetClientId: targetClientId);
        }

        private void BroadcastWeatherFromHost(bool snapInstant, ulong targetClientId = ulong.MaxValue)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            byte wType = (byte)CurrentWeather;
            float remain = WeatherRemainingSeconds;
            byte seasonOv = (byte)_seasonOverride;
            bool hasOv = _hasSeasonOverride;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
            {
                w.Write(wType);
                w.Write(remain);
                w.Write(seasonOv);
                w.Write(hasOv);
                w.Write(snapInstant);
            }, targetClientId);
        }

        public void HandleRemoteWeatherSync(byte weatherByte, float remainingSeconds, byte seasonOverrideByte, bool hasSeasonOverride, bool snapInstant, bool receivedOnHost)
        {
            WeatherType wType = (WeatherType)Mathf.Clamp(weatherByte, 0, 4);
            SeasonType sType = (SeasonType)Mathf.Clamp(seasonOverrideByte, 0, 3);

            if (receivedOnHost)
            {
                float safeDur = wType == WeatherType.Clear ? 0f : Mathf.Max(1f, remainingSeconds);
                ApplyWeatherStateLocal(wType, safeDur, sType, hasSeasonOverride, snapInstant: false);
                BroadcastWeatherFromHost(snapInstant: false);
                return;
            }

            ApplyWeatherStateLocal(wType, wType == WeatherType.Clear ? 0f : Mathf.Max(0f, remainingSeconds), sType, hasSeasonOverride, snapInstant);
        }

        private void ApplyWeatherStateLocal(WeatherType weather, float remainingSeconds, SeasonType seasonOverride, bool hasSeasonOverride, bool snapInstant)
        {
            CurrentWeather = weather;
            _weatherRemainingTimer = weather != WeatherType.Clear ? remainingSeconds : 0f;
            _hasSeasonOverride = hasSeasonOverride;
            _seasonOverride = seasonOverride;

            if (weather == WeatherType.Clear)
            {
                _rollTimer = rollIntervalSeconds;
            }
            else if (weather == WeatherType.Thunderstorm)
            {
                _lightningStrikeTimer = Mathf.Min(_lightningStrikeTimer, 2.5f);
            }

            if (snapInstant)
            {
                RainIntensity = weather == WeatherType.Rain ? 1f : 0f;
                StormIntensity = weather == WeatherType.Thunderstorm ? 1f : 0f;
                SnowParticleIntensity = weather == WeatherType.Snow ? 1f : 0f;
                BlizzardIntensity = weather == WeatherType.Blizzard ? 1f : 0f;
                GroundWetness = IsRaining ? 1f : 0f;
                GroundSnowAmount = GetTargetSnowAmount();
            }
        }

        public void HandleRemoteLightningStrike(int stageX, int stageY, Vector3 strikePos, float telegraphDuration, bool receivedOnHost)
        {
            if (receivedOnHost)
            {
                ExecuteLightningStrikeAndBroadcast(stageX, stageY, strikePos, telegraphDuration);
                return;
            }

            if (IsPlayerSheltered || !IsGameplaySceneActive()) return;
            int myX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            if (myX != stageX || myY != stageY) return;

            StartCoroutine(LightningStrikeSequenceRoutine(strikePos, Mathf.Clamp(telegraphDuration, 0.5f, 3.0f), applyEnemyDamage: false));
        }

        private void ExecuteLightningStrikeAndBroadcast(int stageX, int stageY, Vector3 strikePos, float telegraphDuration)
        {
            if (EmpireNetworkManager.IsOnlineHost && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.LightningStrikeSync, w =>
                {
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(strikePos.x);
                    w.Write(strikePos.y);
                    w.Write(strikePos.z);
                    w.Write(telegraphDuration);
                });
            }

            int myX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            bool hostOnSameStage = !IsPlayerSheltered && myX == stageX && myY == stageY;
            if (hostOnSameStage || !EmpireNetworkManager.IsOnline)
            {
                StartCoroutine(LightningStrikeSequenceRoutine(strikePos, telegraphDuration, applyEnemyDamage: true));
            }
        }

        // =========================================================================
        //  ATMOSPHERIC LIGHTING MODIFIERS (Called by DayNightManager every frame)
        // =========================================================================

        public void ApplyWeatherLightingModifiers(ref Color targetAmbient, ref float targetIntensity, ref Color targetColor, ref Color targetTint)
        {
            if (!IsGameplaySceneActive() || IsPlayerSheltered) return;

            // 1. Subtle Season atmosphere baseline
            switch (CurrentSeason)
            {
                case SeasonType.Winter:
                    targetColor = Color.Lerp(targetColor, new Color(0.82f, 0.90f, 1.0f, 1f), 0.22f);
                    targetAmbient = Color.Lerp(targetAmbient, new Color(0.45f, 0.53f, 0.65f, 1f), 0.16f);
                    break;
                case SeasonType.Summer:
                    targetIntensity *= 1.06f;
                    targetColor = Color.Lerp(targetColor, new Color(1.0f, 0.94f, 0.78f, 1f), 0.14f);
                    break;
                case SeasonType.Rainy:
                    targetIntensity *= 0.94f;
                    targetColor = Color.Lerp(targetColor, new Color(0.84f, 0.89f, 0.95f, 1f), 0.12f);
                    break;
            }

            // 2. Normal Rain — Overcast gloomy mood (บรรยากาศครึ้ม!)
            if (RainIntensity > 0.005f)
            {
                float k = RainIntensity;
                targetIntensity *= Mathf.Lerp(1f, 0.58f, k);
                targetColor = Color.Lerp(targetColor, new Color(0.60f, 0.67f, 0.78f, 1f), 0.54f * k);
                targetAmbient = Color.Lerp(targetAmbient, new Color(0.28f, 0.34f, 0.44f, 1f), 0.46f * k);
                targetTint = Color.Lerp(targetTint, new Color(0.72f, 0.78f, 0.87f, 1f), 0.36f * k);
            }

            // 3. Thunderstorm — Deep dark storm gloom!
            if (StormIntensity > 0.005f)
            {
                float k = StormIntensity;
                targetIntensity *= Mathf.Lerp(1f, 0.38f, k);
                targetColor = Color.Lerp(targetColor, new Color(0.42f, 0.49f, 0.64f, 1f), 0.70f * k);
                targetAmbient = Color.Lerp(targetAmbient, new Color(0.18f, 0.22f, 0.32f, 1f), 0.58f * k);
                targetTint = Color.Lerp(targetTint, new Color(0.56f, 0.62f, 0.74f, 1f), 0.48f * k);
            }

            // 4. Snow — Crisp cool winter clouds
            if (SnowParticleIntensity > 0.005f)
            {
                float k = SnowParticleIntensity;
                targetIntensity *= Mathf.Lerp(1f, 0.80f, k);
                targetColor = Color.Lerp(targetColor, new Color(0.84f, 0.92f, 1.0f, 1f), 0.45f * k);
                targetAmbient = Color.Lerp(targetAmbient, new Color(0.48f, 0.56f, 0.68f, 1f), 0.36f * k);
            }

            // 5. Blizzard — Harsh icy whiteout gloom
            if (BlizzardIntensity > 0.005f)
            {
                float k = BlizzardIntensity;
                targetIntensity *= Mathf.Lerp(1f, 0.50f, k);
                targetColor = Color.Lerp(targetColor, new Color(0.72f, 0.82f, 0.94f, 1f), 0.68f * k);
                targetAmbient = Color.Lerp(targetAmbient, new Color(0.40f, 0.49f, 0.62f, 1f), 0.56f * k);
                targetTint = Color.Lerp(targetTint, new Color(0.80f, 0.87f, 0.96f, 1f), 0.42f * k);
            }

            // 6. Lightning sky flash momentary illumination spike
            if (_lightningFlashAlpha > 0.01f)
            {
                float f = _lightningFlashAlpha;
                targetIntensity += 1.65f * f;
                targetColor = Color.Lerp(targetColor, new Color(0.90f, 0.96f, 1.0f, 1f), 0.85f * f);
                targetAmbient = Color.Lerp(targetAmbient, new Color(0.76f, 0.86f, 1.0f, 1f), 0.70f * f);
                targetTint = Color.Lerp(targetTint, new Color(0.96f, 0.98f, 1.0f, 1f), 0.65f * f);
            }
        }

        // =========================================================================
        //  UPDATE & LATEUPDATE: SCHEDULE, PARTICLES, SNOW CAPS, LIGHTNING & AUDIO
        // =========================================================================

        private float GetTargetSnowAmount()
        {
            if (CurrentWeather == WeatherType.Blizzard) return 1.0f;
            if (CurrentWeather == WeatherType.Snow) return 0.90f;
            if (IsRaining) return 0f;
            if (CurrentSeason == SeasonType.Winter) return 0.55f;
            return 0f;
        }

        private void Update()
        {
            if (!IsGameplaySceneActive())
            {
                ResetGlobalWeatherShaders();
                return;
            }

            float dt = Time.deltaTime;

            // Decay lightning flash rapidly
            if (_lightningFlashAlpha > 0f)
            {
                _lightningFlashAlpha = Mathf.MoveTowards(_lightningFlashAlpha, 0f, dt * 4.5f);
            }

            // 1. Authoritative Schedule & Thunderstorm Lightning Strikes (Host or Offline)
            if (!EmpireNetworkManager.IsOnlineClient)
            {
                if (CurrentWeather != WeatherType.Clear)
                {
                    _weatherRemainingTimer -= dt;
                    if (_weatherRemainingTimer <= 0f)
                    {
                        CurrentWeather = WeatherType.Clear;
                        _weatherRemainingTimer = 0f;
                        _rollTimer = rollIntervalSeconds;
                        BroadcastWeatherFromHost(snapInstant: false);
                    }
                    else if (CurrentWeather == WeatherType.Thunderstorm)
                    {
                        UpdateHostThunderstormStrikes(dt);
                    }
                }
                else
                {
                    _rollTimer -= dt;
                    if (_rollTimer <= 0f)
                    {
                        _rollTimer = rollIntervalSeconds;
                        if (Random.value < weatherChance)
                        {
                            CurrentWeather = RollSeasonWeather();
                            _weatherRemainingTimer = RollRandomWeatherDuration();
                            _lightningStrikeTimer = Random.Range(2.2f, 4.2f);
                            BroadcastWeatherFromHost(snapInstant: false);
                        }
                    }
                }
            }
            else if (CurrentWeather != WeatherType.Clear && _weatherRemainingTimer > 0f)
            {
                _weatherRemainingTimer = Mathf.Max(0f, _weatherRemainingTimer - dt);
            }

            // Distant ambient sheet-lightning flash during Thunderstorm (outdoors only)
            bool sheltered = IsPlayerSheltered;
            if (CurrentWeather == WeatherType.Thunderstorm && !sheltered)
            {
                _ambientFlashTimer -= dt;
                if (_ambientFlashTimer <= 0f)
                {
                    _ambientFlashTimer = Random.Range(5.5f, 10.5f);
                    _lightningFlashAlpha = Mathf.Max(_lightningFlashAlpha, 0.55f);
                    PlayThunderSound(0.28f, 0.85f);
                }
            }

            // 2. Smooth Weather Intensity Curves
            RainIntensity = Mathf.MoveTowards(RainIntensity, CurrentWeather == WeatherType.Rain ? 1f : 0f, dt * (CurrentWeather == WeatherType.Rain ? fadeInSpeed : fadeOutSpeed));
            StormIntensity = Mathf.MoveTowards(StormIntensity, CurrentWeather == WeatherType.Thunderstorm ? 1f : 0f, dt * (CurrentWeather == WeatherType.Thunderstorm ? fadeInSpeed : fadeOutSpeed));
            SnowParticleIntensity = Mathf.MoveTowards(SnowParticleIntensity, CurrentWeather == WeatherType.Snow ? 1f : 0f, dt * (CurrentWeather == WeatherType.Snow ? fadeInSpeed : fadeOutSpeed));
            BlizzardIntensity = Mathf.MoveTowards(BlizzardIntensity, CurrentWeather == WeatherType.Blizzard ? 1f : 0f, dt * (CurrentWeather == WeatherType.Blizzard ? fadeInSpeed : fadeOutSpeed));

            float targetWet = IsRaining ? 1f : 0f;
            GroundWetness = Mathf.MoveTowards(GroundWetness, targetWet, dt * (IsRaining ? 0.20f : 0.12f));

            float targetSnow = GetTargetSnowAmount();
            float snowRate = targetSnow > GroundSnowAmount
                ? (CurrentWeather == WeatherType.Blizzard ? 0.16f : 0.085f)
                : (IsRaining ? 0.22f : 0.06f);
            GroundSnowAmount = Mathf.MoveTowards(GroundSnowAmount, targetSnow, dt * snowRate);

            // 3. Push Global Shader Values (0 indoors so building floors stay completely dry & snow-free!)
            Shader.SetGlobalFloat(PropGlobalWetness, sheltered ? 0f : GroundWetness);
            Shader.SetGlobalFloat(PropGlobalSnowAmount, sheltered ? 0f : GroundSnowAmount);
            PushCampfireMeltGlobals(sheltered);

            // 4. Update Particles (completely hidden & cleared when inside a building interior!)
            float combinedRain = Mathf.Clamp01(RainIntensity + StormIntensity);
            float combinedSnow = Mathf.Clamp01(SnowParticleIntensity + BlizzardIntensity);
            bool anyParticles = !sheltered && (combinedRain > 0.01f || combinedSnow > 0.01f);

            if (anyParticles)
            {
                EnsureWeatherRigCreated();
                UpdateWeatherParticles(combinedRain, combinedSnow);
            }
            else if (_weatherRigRoot != null)
            {
                SetAllWeatherParticlesActive(false);
            }

            // 5. Update Procedural Weather Audio (completely silent indoors!)
            UpdateWeatherAudio(sheltered);

            // 6. Blizzard Freezing HP Drain (-3 HP/s outdoors on Local Player unless sheltered indoors or warmed by Campfire!)
            UpdateLocalBlizzardFreezeDamage(dt, sheltered);

            // 7. Refresh cached outdoor building/prop meshes for Roof Snow Cap overlay when snow is present
            if (!sheltered && GroundSnowAmount > 0.01f)
            {
                _snowTargetRefreshTimer -= dt;
                if (_snowTargetRefreshTimer <= 0f)
                {
                    _snowTargetRefreshTimer = 0.85f;
                    RefreshOutdoorSnowMeshTargets();
                }
            }
        }

        private void UpdateLocalBlizzardFreezeDamage(float dt, bool sheltered)
        {
            if (CurrentWeather != WeatherType.Blizzard || sheltered || blizzardDamagePerSecond <= 0f || IsNearCampfireWarmth)
            {
                _blizzardDamageTimer = 0f;
                return;
            }

            PlayerController localPc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPc == null || localPc.IsRemotePlayer || localPc.IsDowned)
            {
                _blizzardDamageTimer = 0f;
                return;
            }

            Health ph = localPc.PlayerHealth ?? localPc.GetComponent<Health>();
            if (ph == null || ph.IsDead)
            {
                _blizzardDamageTimer = 0f;
                return;
            }

            _blizzardDamageTimer += dt;
            if (_blizzardDamageTimer >= 1.0f)
            {
                _blizzardDamageTimer -= 1.0f;
                // Owner-authoritative HP deduction automatically syncs to all Co-op teammates via NetworkPlayerBridge
                ph.TakeDamage(blizzardDamagePerSecond, localPc.transform.position + Vector3.up * 1.25f);
            }
        }

        private void LateUpdate()
        {
            // Render Top-Facing Snow Caps on outdoor buildings, roofs, cars, trees, props & crafted structures
            if (!IsGameplaySceneActive() || IsPlayerSheltered || GroundSnowAmount <= 0.01f) return;
            DrawOutdoorRoofSnowCaps();
        }

        private void PushCampfireMeltGlobals(bool sheltered)
        {
            if (sheltered || GroundSnowAmount <= 0.005f || CampfireBuilding.ActiveCampfires == null)
            {
                Shader.SetGlobalInt(PropGlobalCampfireCount, 0);
                return;
            }

            int count = 0;
            var list = CampfireBuilding.ActiveCampfires;
            for (int i = 0; i < list.Count && count < 4; i++)
            {
                CampfireBuilding cf = list[i];
                if (cf == null || !cf.gameObject.activeInHierarchy) continue;
                Vector3 p = cf.transform.position;
                _campfireShaderArray[count++] = new Vector4(p.x, p.y, p.z, 5.5f);
            }

            Shader.SetGlobalInt(PropGlobalCampfireCount, count);
            if (count > 0)
            {
                Shader.SetGlobalVectorArray(PropGlobalCampfireData, _campfireShaderArray);
            }
        }

        // =========================================================================
        //  THUNDERSTORM REAL GROUND LIGHTNING STRIKES (TELEGRAPH -> 3D BOLT -> SHOCK)
        // =========================================================================

        private void UpdateHostThunderstormStrikes(float dt)
        {
            _lightningStrikeTimer -= dt;
            if (_lightningStrikeTimer > 0f) return;
            _lightningStrikeTimer = Random.Range(minLightningInterval, maxLightningInterval);

            // Pick a random alive outdoor player on Host's stage (never strike inside buildings!)
            int hostStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int hostStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;

            List<PlayerController> outdoorCandidates = new List<PlayerController>();
            var all = PlayerRegistry.AllPlayers;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerController p = all[i];
                    if (p == null || !p.gameObject.activeInHierarchy || p.IsDowned) continue;
                    if (!p.IsRemotePlayer)
                    {
                        if (!BuildingInteriorManager.IsInsideInterior) outdoorCandidates.Add(p);
                    }
                    else if (p.NetworkInteriorId == 0 && p.NetworkStageX == hostStageX && p.NetworkStageY == hostStageY)
                    {
                        outdoorCandidates.Add(p);
                    }
                }
            }

            if (outdoorCandidates.Count == 0) return;

            PlayerController target = outdoorCandidates[Random.Range(0, outdoorCandidates.Count)];
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(2.8f, 9.5f);
            Vector3 strikePos = target.transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            strikePos.x = Mathf.Clamp(strikePos.x, -12.5f, 12.5f);
            strikePos.y = 0f;
            strikePos.z = Mathf.Clamp(strikePos.z, -6.5f, 6.5f);

            ExecuteLightningStrikeAndBroadcast(hostStageX, hostStageY, strikePos, lightningTelegraphDuration);
        }

        private IEnumerator LightningStrikeSequenceRoutine(Vector3 strikePos, float telegraphDuration, bool applyEnemyDamage)
        {
            // 1. Ground Warning Telegraph Ring (HitboxSpriteLibrary.HazardCircle)
            Sprite hazardSp = HitboxSpriteLibrary.HazardCircle;
            SpriteRenderer warnRing = null;
            GameObject warnLightObj = new GameObject("LightningTelegraphLight");
            warnLightObj.transform.position = new Vector3(strikePos.x, 1.4f, strikePos.z);
            Light warnLight = warnLightObj.AddComponent<Light>();
            warnLight.type = LightType.Point;
            warnLight.color = new Color(0.55f, 0.85f, 1f);
            warnLight.range = 6.5f;
            warnLight.intensity = 2.0f;
            warnLight.shadows = LightShadows.None;

            if (hazardSp != null)
            {
                warnRing = HitboxSpriteLibrary.CreateGroundSpriteIndicator(
                    "LightningStrikeWarning",
                    null,
                    hazardSp,
                    new Color(0.55f, 0.88f, 1f, 0.30f),
                    8);
                if (warnRing != null)
                {
                    warnRing.transform.position = new Vector3(strikePos.x, 0.07f, strikePos.z);
                    warnRing.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    HitboxSpriteLibrary.SetWorldDiameter(warnRing, lightningStrikeRadius * 2f);
                }
            }

            float elapsed = 0f;
            while (elapsed < telegraphDuration)
            {
                if (IsPlayerSheltered || !IsGameplaySceneActive())
                {
                    if (warnRing != null) Destroy(warnRing.gameObject);
                    Destroy(warnLightObj);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / telegraphDuration);
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * (10f + t * 22f));
                warnLight.intensity = Mathf.Lerp(1.5f, 7.5f, t) * (0.65f + 0.35f * pulse);
                if (warnRing != null)
                {
                    warnRing.color = new Color(
                        Mathf.Lerp(0.45f, 0.95f, t),
                        Mathf.Lerp(0.82f, 0.98f, t),
                        1f,
                        Mathf.Lerp(0.22f, 0.45f, t) * (0.7f + 0.3f * pulse));
                }
                yield return null;
            }

            if (warnRing != null) Destroy(warnRing.gameObject);
            Destroy(warnLightObj);

            if (IsPlayerSheltered || !IsGameplaySceneActive()) yield break;

            // 2. LIGHTNING IMPACT: 3D Jagged Bolt + Sky Flash + Screen Shake + Thunder Crack!
            _lightningFlashAlpha = 1.0f;
            CameraFollow.TriggerShake(0.32f, 0.36f);
            PlayThunderSound(0.65f, Random.Range(0.92f, 1.12f));

            StartCoroutine(LightningBoltVfxRoutine(strikePos));

            // 3. Apply Shock Damage & Stagger to Enemies (Host/Offline authoritative)
            if (applyEnemyDamage && GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                var enemies = new List<GameObject>(GameManager.Instance.ActiveEnemies);
                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject e = enemies[i];
                    if (e == null || !e.activeInHierarchy) continue;
                    Vector3 diff = e.transform.position - strikePos;
                    diff.y = 0f;
                    if (diff.magnitude > lightningStrikeRadius + 0.4f) continue;

                    BaseEnemyAI ai = e.GetComponent<BaseEnemyAI>();
                    if (ai != null)
                    {
                        Vector3 push = diff.sqrMagnitude > 0.01f ? diff.normalized : Vector3.forward;
                        ai.ApplyMeleeKnockback(push, 5.5f, 1.25f);
                    }

                    Health eh = e.GetComponent<Health>();
                    if (eh != null && !eh.IsDead)
                    {
                        eh.TakeDamage(lightningEnemyDamage, e.transform.position + Vector3.up * 1.2f, isCrit: true);
                    }
                }
            }

            // 4. Apply Shock Damage to Local Player if standing inside the strike circle outdoors
            PlayerController localPc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPc != null && !localPc.IsDowned && !IsPlayerSheltered)
            {
                Vector3 pDiff = localPc.transform.position - strikePos;
                pDiff.y = 0f;
                if (pDiff.magnitude <= lightningStrikeRadius)
                {
                    Health ph = localPc.PlayerHealth ?? localPc.GetComponent<Health>();
                    if (ph != null && !ph.IsDead)
                    {
                        ph.TakeDamage(lightningPlayerDamage, localPc.transform.position + Vector3.up * 1.1f);
                    }
                    Vector3 push = pDiff.sqrMagnitude > 0.01f ? pDiff.normalized : Vector3.forward;
                    localPc.ApplyKnockback(push * 7.5f, 0.25f);
                }
            }
        }

        private static IEnumerator LightningBoltVfxRoutine(Vector3 impactPos)
        {
            GameObject boltObj = new GameObject("LightningBolt3D");
            LineRenderer lr = boltObj.AddComponent<LineRenderer>();
            const int segs = 12;
            lr.positionCount = segs;
            lr.startWidth = 0.24f;
            lr.endWidth = 0.09f;
            lr.useWorldSpace = true;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = sh != null ? new Material(sh) : null;
            if (mat != null)
            {
                mat.color = new Color(0.86f, 0.96f, 1f, 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.86f, 0.96f, 1f, 1f));
                lr.material = mat;
            }
            lr.startColor = Color.white;
            lr.endColor = new Color(0.55f, 0.85f, 1f, 1f);

            GameObject flashObj = new GameObject("LightningImpactLight");
            flashObj.transform.position = new Vector3(impactPos.x, 3.2f, impactPos.z);
            Light impactLight = flashObj.AddComponent<Light>();
            impactLight.type = LightType.Point;
            impactLight.color = new Color(0.75f, 0.92f, 1f);
            impactLight.range = 24f;
            impactLight.intensity = 18f;
            impactLight.shadows = LightShadows.None;

            Vector3 topPos = new Vector3(impactPos.x + Random.Range(-1.8f, 1.8f), 26f, impactPos.z + Random.Range(-1.8f, 1.8f));
            for (int i = 0; i < segs; i++)
            {
                float t = (float)i / (segs - 1);
                Vector3 p = Vector3.Lerp(topPos, impactPos + Vector3.up * 0.1f, t);
                if (i > 0 && i < segs - 1)
                {
                    p.x += Random.Range(-0.85f, 0.85f);
                    p.z += Random.Range(-0.85f, 0.85f);
                }
                lr.SetPosition(i, p);
            }

            float elapsed = 0f;
            const float dur = 0.24f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(elapsed / dur);
                lr.startWidth = 0.26f * k;
                lr.endWidth = 0.10f * k;
                impactLight.intensity = 18f * k * k;
                yield return null;
            }

            if (mat != null) Destroy(mat);
            Destroy(boltObj);
            Destroy(flashObj);
        }

        // =========================================================================
        //  OUTDOOR BUILDING / ROOF / PROP TOP-FACING SNOW CAP OVERLAY
        // =========================================================================

        private void RefreshOutdoorSnowMeshTargets()
        {
            _snowTargets.Clear();
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;

                // Exclude characters, enemies, NPCs, ground planes, particles, UI, and projectiles
                Transform root = mr.transform;
                if (root.GetComponentInParent<PlayerController>() != null) continue;
                if (root.GetComponentInParent<BaseEnemyAI>() != null) continue;
                if (root.GetComponentInParent<NPCController>() != null) continue;
                if (root.GetComponentInParent<Projectile>() != null) continue;
                if (root.GetComponentInParent<Canvas>() != null) continue;

                string n = mr.gameObject.name;
                if (n.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Mist", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Hitbox", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Portal", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Trigger", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Outline", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // Skip flat road/decal sheets hugging y ~ 0
                if (mr.bounds.size.y < 0.08f && mr.bounds.center.y < 0.15f) continue;

                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                _snowTargets.Add(new SnowMeshTarget { Filter = mf, Renderer = mr });
            }
        }

        private void DrawOutdoorRoofSnowCaps()
        {
            if (_snowCapMaterial == null)
            {
                Shader sh = Shader.Find("Custom/WeatherSnowCapOverlay");
                if (sh == null) return;
                _snowCapMaterial = new Material(sh);
            }

            Camera cam = Camera.main;
            Vector3 camAnchor = Vector3.zero;
            if (cam != null) camAnchor = cam.transform.position;
            else
            {
                PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
                if (pc != null) camAnchor = pc.transform.position;
            }

            for (int i = 0; i < _snowTargets.Count; i++)
            {
                SnowMeshTarget t = _snowTargets[i];
                if (t.Filter == null || t.Renderer == null || !t.Renderer.enabled || !t.Renderer.gameObject.activeInHierarchy) continue;

                Mesh mesh = t.Filter.sharedMesh;
                if (mesh == null) continue;

                // Distance cull far offscreen meshes (fast XZ check)
                Vector3 d = t.Renderer.bounds.center - camAnchor;
                if (d.x * d.x + d.z * d.z > 2400f) continue;

                Matrix4x4 mtx = t.Renderer.localToWorldMatrix;
                int subCount = mesh.subMeshCount;
                for (int s = 0; s < subCount; s++)
                {
                    Graphics.DrawMesh(mesh, mtx, _snowCapMaterial, 0, null, s, null, false, false, false);
                }
            }
        }

        // =========================================================================
        //  WEATHER PARTICLE RIGS (RAIN +25%, THUNDERSTORM, SNOW, BLIZZARD)
        // =========================================================================

        private void EnsureWeatherRigCreated()
        {
            if (_weatherRigRoot != null) return;

            _weatherRigRoot = new GameObject("Weather_ParticleRig");
            DontDestroyOnLoad(_weatherRigRoot);

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            // --- 1. Falling Rain Streaks (+25% heavier for normal rain, dense for storm!) ---
            _rainDropTex = CreateRainStreakTexture();
            _rainDropMat = CreateAlphaBlendParticleMaterial(particleShader, _rainDropTex, new Color(0.76f, 0.86f, 0.98f, 0.56f));

            GameObject dropObj = new GameObject("RainDrops");
            dropObj.transform.SetParent(_weatherRigRoot.transform, false);
            dropObj.transform.localPosition = new Vector3(0f, 15f, 0f);
            _rainDropPS = dropObj.AddComponent<ParticleSystem>();

            var dMain = _rainDropPS.main;
            dMain.loop = true;
            dMain.playOnAwake = true;
            dMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.58f, 0.78f);
            dMain.startSpeed = 0f;
            dMain.startSize = new ParticleSystem.MinMaxCurve(0.058f, 0.092f);
            dMain.startColor = new Color(0.78f, 0.88f, 1f, 0.58f);
            dMain.maxParticles = 560;

            var dEmis = _rainDropPS.emission;
            dEmis.enabled = true;
            dEmis.rateOverTime = 0f;

            var dShape = _rainDropPS.shape;
            dShape.enabled = true;
            dShape.shapeType = ParticleSystemShapeType.Box;
            dShape.scale = new Vector3(32f, 1.5f, 24f);

            var dVel = _rainDropPS.velocityOverLifetime;
            dVel.enabled = true;
            dVel.space = ParticleSystemSimulationSpace.World;
            dVel.x = -2.5f;
            dVel.y = -24.5f;
            dVel.z = -1.3f;

            var dRen = _rainDropPS.GetComponent<ParticleSystemRenderer>();
            dRen.renderMode = ParticleSystemRenderMode.Stretch;
            dRen.lengthScale = 0.0f;
            dRen.velocityScale = 0.048f;
            dRen.sharedMaterial = _rainDropMat;
            dRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dRen.receiveShadows = false;

            // --- 2. Ground Rain Splash Rings (+25% heavier!) ---
            _rainSplashTex = CreateRainSplashTexture();
            _rainSplashMat = CreateAlphaBlendParticleMaterial(particleShader, _rainSplashTex, new Color(0.80f, 0.90f, 1f, 0.45f));

            GameObject splashObj = new GameObject("RainSplashes");
            splashObj.transform.SetParent(_weatherRigRoot.transform, false);
            splashObj.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            _rainSplashPS = splashObj.AddComponent<ParticleSystem>();

            var sMain = _rainSplashPS.main;
            sMain.loop = true;
            sMain.playOnAwake = true;
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
            sMain.startSpeed = 0f;
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.50f);
            sMain.startColor = new Color(0.82f, 0.92f, 1f, 0.44f);
            sMain.maxParticles = 240;

            var sEmis = _rainSplashPS.emission;
            sEmis.enabled = true;
            sEmis.rateOverTime = 0f;

            var sShape = _rainSplashPS.shape;
            sShape.enabled = true;
            sShape.shapeType = ParticleSystemShapeType.Box;
            sShape.scale = new Vector3(28f, 0.05f, 20f);

            var sSize = _rainSplashPS.sizeOverLifetime;
            sSize.enabled = true;
            sSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1.0f)));

            var sCol = _rainSplashPS.colorOverLifetime;
            sCol.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.82f, 0f), new GradientAlphaKey(0f, 1f) });
            sCol.color = fadeGrad;

            var sRen = _rainSplashPS.GetComponent<ParticleSystemRenderer>();
            sRen.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            sRen.sharedMaterial = _rainSplashMat;
            sRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sRen.receiveShadows = false;

            // --- 3. Snowflakes & Blizzard Wind Gusts ---
            _snowFlakeTex = CreateSnowflakeTexture();
            _snowFlakeMat = CreateAlphaBlendParticleMaterial(particleShader, _snowFlakeTex, new Color(0.95f, 0.98f, 1f, 0.85f));

            GameObject snowObj = new GameObject("SnowFlakes");
            snowObj.transform.SetParent(_weatherRigRoot.transform, false);
            snowObj.transform.localPosition = new Vector3(0f, 14f, 0f);
            _snowFlakePS = snowObj.AddComponent<ParticleSystem>();

            var fMain = _snowFlakePS.main;
            fMain.loop = true;
            fMain.playOnAwake = true;
            fMain.simulationSpace = ParticleSystemSimulationSpace.World;
            fMain.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
            fMain.startSpeed = 0f;
            fMain.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.24f);
            fMain.startColor = new Color(0.95f, 0.98f, 1f, 0.85f);
            fMain.maxParticles = 540;

            var fEmis = _snowFlakePS.emission;
            fEmis.enabled = true;
            fEmis.rateOverTime = 0f;

            var fShape = _snowFlakePS.shape;
            fShape.enabled = true;
            fShape.shapeType = ParticleSystemShapeType.Box;
            fShape.scale = new Vector3(36f, 2.0f, 26f);

            var fVel = _snowFlakePS.velocityOverLifetime;
            fVel.enabled = true;
            fVel.space = ParticleSystemSimulationSpace.World;
            fVel.x = -1.6f;
            fVel.y = -3.4f;
            fVel.z = -0.8f;

            var fNoise = _snowFlakePS.noise;
            fNoise.enabled = true;
            fNoise.strength = 0.85f;
            fNoise.frequency = 0.45f;

            var fRen = _snowFlakePS.GetComponent<ParticleSystemRenderer>();
            fRen.renderMode = ParticleSystemRenderMode.Billboard;
            fRen.sharedMaterial = _snowFlakeMat;
            fRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fRen.receiveShadows = false;
        }

        private void UpdateWeatherParticles(float combinedRain, float combinedSnow)
        {
            if (_weatherRigRoot == null) return;
            if (!_weatherRigRoot.activeSelf) _weatherRigRoot.SetActive(true);

            Vector3 anchor = Vector3.zero;
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc != null) anchor = pc.transform.position;
            else if (Camera.main != null) anchor = Camera.main.transform.position;

            // Offset blizzard spawn slightly upwind so fierce slanted snow covers full viewport
            float windOffsetX = BlizzardIntensity * 8.0f + StormIntensity * 2.5f;
            _weatherRigRoot.transform.position = new Vector3(anchor.x + windOffsetX, 0f, anchor.z);

            // Rain / Thunderstorm
            if (_rainDropPS != null)
            {
                var em = _rainDropPS.emission;
                // Normal Rain = 300/s (+25% heavier than 240/s!), Thunderstorm = 480/s
                float rainRate = RainIntensity * 300f + StormIntensity * 480f;
                em.rateOverTime = rainRate;

                var vel = _rainDropPS.velocityOverLifetime;
                vel.x = Mathf.Lerp(-2.5f, -6.8f, StormIntensity);
                vel.y = Mathf.Lerp(-24.5f, -28.5f, StormIntensity);
                vel.z = Mathf.Lerp(-1.3f, -2.6f, StormIntensity);

                if (rainRate > 0.5f && !_rainDropPS.isPlaying) _rainDropPS.Play();
            }

            if (_rainSplashPS != null)
            {
                var em = _rainSplashPS.emission;
                // Normal Rain = 125/s (+25% heavier!), Thunderstorm = 200/s
                float splashRate = RainIntensity * 125f + StormIntensity * 200f;
                em.rateOverTime = splashRate;
                if (splashRate > 0.5f && !_rainSplashPS.isPlaying) _rainSplashPS.Play();
            }

            // Snow / Blizzard
            if (_snowFlakePS != null)
            {
                var em = _snowFlakePS.emission;
                float snowRate = SnowParticleIntensity * 190f + BlizzardIntensity * 420f;
                em.rateOverTime = snowRate;

                var vel = _snowFlakePS.velocityOverLifetime;
                vel.x = Mathf.Lerp(-1.6f, -12.5f, BlizzardIntensity);
                vel.y = Mathf.Lerp(-3.4f, -6.5f, BlizzardIntensity);
                vel.z = Mathf.Lerp(-0.8f, -4.2f, BlizzardIntensity);

                var main = _snowFlakePS.main;
                main.startLifetime = BlizzardIntensity > 0.5f
                    ? new ParticleSystem.MinMaxCurve(1.4f, 2.2f)
                    : new ParticleSystem.MinMaxCurve(2.4f, 4.0f);

                if (snowRate > 0.5f && !_snowFlakePS.isPlaying) _snowFlakePS.Play();
            }
        }

        private void SetAllWeatherParticlesActive(bool active)
        {
            StopParticleSystem(_rainDropPS, active);
            StopParticleSystem(_rainSplashPS, active);
            StopParticleSystem(_snowFlakePS, active);
            if (_weatherRigRoot != null && _weatherRigRoot.activeSelf != active)
            {
                _weatherRigRoot.SetActive(active);
            }
        }

        private static void StopParticleSystem(ParticleSystem ps, bool active)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = 0f;
            if (!active)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // =========================================================================
        //  PROCEDURAL AUDIO (RAIN, STORM, THUNDER CRACK, WINTER/BLIZZARD WIND)
        // =========================================================================

        private void UpdateWeatherAudio(bool sheltered)
        {
            float rainVol = sheltered
                ? indoorAudioVolume
                : (RainIntensity * outdoorRainVolume + StormIntensity * outdoorStormVolume);

            if (rainVol > 0.002f)
            {
                if (_rainAudioSource == null)
                {
                    _rainAudioSource = gameObject.AddComponent<AudioSource>();
                    _rainAudioSource.loop = true;
                    _rainAudioSource.playOnAwake = false;
                    _rainAudioSource.spatialBlend = 0f;
                    _rainLoopClip = CreateProceduralRainClip();
                    _rainAudioSource.clip = _rainLoopClip;
                }
                _rainAudioSource.volume = rainVol;
                _rainAudioSource.pitch = Mathf.Lerp(1.0f, 1.12f, StormIntensity);
                if (!_rainAudioSource.isPlaying) _rainAudioSource.Play();
            }
            else if (_rainAudioSource != null && _rainAudioSource.isPlaying)
            {
                _rainAudioSource.volume = 0f;
                _rainAudioSource.Stop();
            }

            float windVol = sheltered
                ? indoorAudioVolume
                : (SnowParticleIntensity * (outdoorWindVolume * 0.40f) + BlizzardIntensity * outdoorWindVolume + StormIntensity * (outdoorWindVolume * 0.45f));

            if (windVol > 0.002f)
            {
                if (_windAudioSource == null)
                {
                    _windAudioSource = gameObject.AddComponent<AudioSource>();
                    _windAudioSource.loop = true;
                    _windAudioSource.playOnAwake = false;
                    _windAudioSource.spatialBlend = 0f;
                    _windLoopClip = CreateProceduralWindClip();
                    _windAudioSource.clip = _windLoopClip;
                }
                _windAudioSource.volume = windVol;
                _windAudioSource.pitch = Mathf.Lerp(0.82f, 1.25f, BlizzardIntensity);
                if (!_windAudioSource.isPlaying) _windAudioSource.Play();
            }
            else if (_windAudioSource != null && _windAudioSource.isPlaying)
            {
                _windAudioSource.volume = 0f;
                _windAudioSource.Stop();
            }
        }

        private void PlayThunderSound(float volume, float pitch)
        {
            if (IsPlayerSheltered || !IsGameplaySceneActive()) return;
            if (_thunderAudioSource == null)
            {
                _thunderAudioSource = gameObject.AddComponent<AudioSource>();
                _thunderAudioSource.loop = false;
                _thunderAudioSource.playOnAwake = false;
                _thunderAudioSource.spatialBlend = 0f;
                _thunderClip = CreateProceduralThunderClip();
            }
            _thunderAudioSource.pitch = pitch;
            _thunderAudioSource.PlayOneShot(_thunderClip, Mathf.Clamp01(volume));
        }

        private void MuteAllAudio()
        {
            if (_rainAudioSource != null) { _rainAudioSource.volume = 0f; _rainAudioSource.Stop(); }
            if (_windAudioSource != null) { _windAudioSource.volume = 0f; _windAudioSource.Stop(); }
        }

        // =========================================================================
        //  SCREEN OVERCAST VEIL, BLIZZARD FROST VIGNETTE & COMPACT HUD PILL
        // =========================================================================

        private void EnsureGuiTextures()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            if (_frostVignetteTex == null)
            {
                _frostVignetteTex = CreateFrostVignetteTexture();
            }
            if (_hudLabelStyle == null)
            {
                _hudLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _hudLabelStyle.normal.textColor = new Color(0.90f, 0.96f, 1f, 1f);
            }
        }

        private void OnGUI()
        {
            // Never display ANY weather overlay or weather pill inside buildings or non-gameplay scenes!
            if (!IsGameplaySceneActive() || IsPlayerSheltered) return;

            float overcastAlpha = RainIntensity * 0.14f + StormIntensity * 0.24f + BlizzardIntensity * 0.16f;
            bool showFrost = BlizzardIntensity > 0.02f;
            bool showFlash = _lightningFlashAlpha > 0.02f;
            bool showPill = IsPlayerExposedToWeather &&
                (RainIntensity > 0.05f || StormIntensity > 0.05f || SnowParticleIntensity > 0.05f || BlizzardIntensity > 0.05f);

            if (overcastAlpha <= 0.005f && !showFrost && !showFlash && !showPill) return;

            EnsureGuiTextures();
            Color prev = GUI.color;

            // 1. Subtle Overcast Gloomy Veil (Rain / Thunderstorm / Blizzard)
            if (overcastAlpha > 0.005f)
            {
                Color veilCol = BlizzardIntensity > 0.3f
                    ? new Color(0.76f, 0.86f, 0.96f, overcastAlpha)
                    : new Color(0.08f, 0.12f, 0.20f, overcastAlpha);
                GUI.color = veilCol;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _whiteTex);
            }

            // 2. Blizzard Frost Screen-Edge Vignette
            if (showFrost && _frostVignetteTex != null)
            {
                float frostAlpha = BlizzardIntensity * (IsNearCampfireWarmth ? 0.22f : 0.78f);
                GUI.color = new Color(0.84f, 0.94f, 1f, frostAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _frostVignetteTex);
            }

            // 3. Lightning Sky Flash
            if (showFlash)
            {
                GUI.color = new Color(0.88f, 0.95f, 1f, _lightningFlashAlpha * 0.42f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _whiteTex);
            }

            // 4. Compact Weather & Season Debuff Pill (top-center below boss bar)
            if (showPill)
            {
                float pillAlpha = Mathf.Clamp01(RainIntensity + StormIntensity + SnowParticleIntensity + BlizzardIntensity);
                string labelText;
                Color borderCol;
                Color bgCol;

                string remainStr = FormatWeatherDuration(WeatherRemainingSeconds);
                switch (CurrentWeather)
                {
                    case WeatherType.Thunderstorm:
                        labelText = $"THUNDERSTORM ({remainStr})  |  DRAIN x2  |  SHIFT LOCKED";
                        borderCol = new Color(0.55f, 0.78f, 1f, 0.90f * pillAlpha);
                        bgCol = new Color(0.06f, 0.10f, 0.22f, 0.84f * pillAlpha);
                        break;
                    case WeatherType.Blizzard:
                        labelText = IsNearCampfireWarmth
                            ? $"BLIZZARD ({remainStr})  |  WARMED BY CAMPFIRE  |  SHIFT LOCKED"
                            : $"BLIZZARD ({remainStr})  |  -{blizzardDamagePerSecond:F0} HP/s  |  DRAIN x2  |  SHIFT LOCKED";
                        borderCol = new Color(0.72f, 0.92f, 1f, 0.92f * pillAlpha);
                        bgCol = new Color(0.08f, 0.18f, 0.28f, 0.84f * pillAlpha);
                        break;
                    case WeatherType.Snow:
                        labelText = IsNearCampfireWarmth
                            ? $"SNOWING ({remainStr})  |  WARMED BY CAMPFIRE"
                            : $"SNOWING ({remainStr})  |  COLD DRAIN x1.4";
                        borderCol = new Color(0.78f, 0.92f, 1f, 0.85f * pillAlpha);
                        bgCol = new Color(0.08f, 0.16f, 0.26f, 0.78f * pillAlpha);
                        break;
                    case WeatherType.Rain:
                    default:
                        labelText = $"RAINING ({remainStr})  |  SANITY / HUNGER / THIRST x2";
                        borderCol = new Color(0.38f, 0.74f, 1f, 0.88f * pillAlpha);
                        bgCol = new Color(0.06f, 0.14f, 0.24f, 0.82f * pillAlpha);
                        break;
                }

                float w = 470f;
                float h = 26f;
                float x = (Screen.width - w) * 0.5f;
                float y = 78f;

                GUI.color = bgCol;
                GUI.DrawTexture(new Rect(x, y, w, h), _whiteTex);
                GUI.color = borderCol;
                GUI.DrawTexture(new Rect(x, y, w, 2f), _whiteTex);
                GUI.DrawTexture(new Rect(x, y + h - 2f, w, 2f), _whiteTex);

                GUI.color = new Color(1f, 1f, 1f, pillAlpha);
                GUI.Label(new Rect(x, y, w, h), labelText, _hudLabelStyle);
            }

            GUI.color = prev;
        }

        // =========================================================================
        //  PROCEDURAL TEXTURES, MATERIALS & AUDIO SYNTHESIS (100% ASSET-FREE)
        // =========================================================================

        private static Material CreateAlphaBlendParticleMaterial(Shader shader, Texture2D tex, Color color)
        {
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static Texture2D CreateRainStreakTexture()
        {
            const int w = 16;
            const int h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                float vFade = Mathf.Sin(v * Mathf.PI);
                for (int x = 0; x < w; x++)
                {
                    float u = ((float)x / (w - 1)) * 2f - 1f;
                    float hFade = Mathf.Clamp01(1f - Mathf.Abs(u));
                    hFade *= hFade;
                    pixels[y * w + x] = new Color(1f, 1f, 1f, vFade * hFade);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateRainSplashTexture()
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) / 0.20f);
                    float a = ring * ring * (r < 1.0f ? 1f : 0f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSnowflakeTexture()
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateFrostVignetteTexture()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = ((float)y / (size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((float)x / (size - 1)) * 2f - 1f;
                    float edge = Mathf.Sqrt(nx * nx + ny * ny);
                    float border = Mathf.Clamp01((edge - 0.62f) / 0.72f);
                    float crystalNoise = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.14f, y * 0.14f);
                    float a = border * border * crystalNoise;
                    pixels[y * size + x] = new Color(0.88f, 0.96f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static AudioClip CreateProceduralRainClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] data = new float[sampleCount];

            System.Random rng = new System.Random(20261127);
            float lp1 = 0f;
            float lp2 = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 = lp1 + 0.14f * (white - lp1);
                lp2 = lp2 + 0.08f * (lp1 - lp2);
                float sample = (lp1 * 0.45f + lp2 * 0.55f) * 0.55f;
                if (rng.NextDouble() < 0.0014)
                {
                    sample += (float)(rng.NextDouble() * 0.35 - 0.175);
                }
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            CrossfadeLoopSeam(data, 2048);
            AudioClip clip = AudioClip.Create("ProceduralSoftRain", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralWindClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 3;
            float[] data = new float[sampleCount];

            System.Random rng = new System.Random(20261128);
            float lp1 = 0f;
            float lp2 = 0f;
            float lp3 = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float gust = 0.65f + 0.35f * Mathf.Sin(t * Mathf.PI * 2f / 3f);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 = lp1 + 0.045f * (white - lp1);
                lp2 = lp2 + 0.032f * (lp1 - lp2);
                lp3 = lp3 + 0.025f * (lp2 - lp3);
                float howl = Mathf.Sin(t * 240f + Mathf.Sin(t * 2.1f) * 18f) * 0.045f * gust;
                data[i] = Mathf.Clamp((lp3 * 1.6f + howl) * gust, -1f, 1f);
            }

            CrossfadeLoopSeam(data, 3072);
            AudioClip clip = AudioClip.Create("ProceduralBlizzardWind", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralThunderClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = (int)(sampleRate * 2.6f);
            float[] data = new float[sampleCount];

            System.Random rng = new System.Random(20261129);
            float lp1 = 0f;
            float lp2 = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Sharp initial lightning crack (0 - 0.14s) + deep rolling rumble tail (0.1s - 2.6s)
                float crackEnv = Mathf.Exp(-t * 16f);
                float rumbleEnv = Mathf.Exp(-t * 1.35f) * (0.72f + 0.28f * Mathf.Sin(t * 11f));

                lp1 = lp1 + 0.055f * (white - lp1);
                lp2 = lp2 + 0.028f * (lp1 - lp2);
                float subBoom = Mathf.Sin(t * 195f * Mathf.Exp(-t * 1.2f)) * 0.45f * Mathf.Exp(-t * 3.5f);

                float sample = (white * 0.55f * crackEnv) + (lp2 * 2.4f * rumbleEnv) + subBoom;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ProceduralThunder", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void CrossfadeLoopSeam(float[] data, int seam)
        {
            int n = data.Length;
            for (int i = 0; i < seam && i < n; i++)
            {
                float t = (float)i / seam;
                data[i] = Mathf.Lerp(data[n - seam + i], data[i], t);
            }
        }

        private void CleanupResources()
        {
            if (_weatherRigRoot != null) Destroy(_weatherRigRoot);
            if (_rainDropMat != null) Destroy(_rainDropMat);
            if (_rainSplashMat != null) Destroy(_rainSplashMat);
            if (_snowFlakeMat != null) Destroy(_snowFlakeMat);
            if (_snowCapMaterial != null) Destroy(_snowCapMaterial);
            if (_rainDropTex != null) Destroy(_rainDropTex);
            if (_rainSplashTex != null) Destroy(_rainSplashTex);
            if (_snowFlakeTex != null) Destroy(_snowFlakeTex);
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_frostVignetteTex != null) Destroy(_frostVignetteTex);
            if (_rainLoopClip != null) Destroy(_rainLoopClip);
            if (_windLoopClip != null) Destroy(_windLoopClip);
            if (_thunderClip != null) Destroy(_thunderClip);
        }
    }
}
