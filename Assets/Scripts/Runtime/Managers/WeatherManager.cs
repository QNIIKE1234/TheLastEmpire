using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Dynamic Normal Rain Weather System (Co-op Multiplayer Synchronized).
    /// - Rolls randomly every 60 seconds (10% chance) on Host / Offline during gameplay.
    /// - When triggered, normal rain falls for 30 seconds with smooth fade-in/out, ground splashes,
    ///   wet ground shader tint (_GlobalWetness), and soft procedural rain audio.
    /// - Automatically pauses particles & softens audio when inside a building interior.
    /// - Host-authoritative sync via NetPacketType.WeatherSync (including Late-Join & [F1] Debug Triggers).
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Random Rain Schedule")]
        [Tooltip("Interval between random rain rolls in seconds (default: 60s)")]
        [SerializeField] private float rollIntervalSeconds = 60f;
        [Tooltip("Chance to trigger rain on each roll (0.10 = 10%)")]
        [Range(0f, 1f)]
        [SerializeField] private float rainChance = 0.10f;
        [Tooltip("Duration of each rain shower in seconds (default: 30s)")]
        [SerializeField] private float rainDurationSeconds = 30f;

        [Header("Visual & Audio Tuning")]
        [SerializeField] private float fadeInSpeed = 0.45f;
        [SerializeField] private float fadeOutSpeed = 0.35f;
        [SerializeField] private float outdoorRainVolume = 0.16f;
        [SerializeField] private float indoorRainVolume = 0f;

        [Header("Rain Gameplay Effects (Outdoors Only)")]
        [Tooltip("Multiplier applied to Sanity, Hunger, and Thirst depletion while exposed to rain outdoors (2.0 = 2x faster drain)")]
        [SerializeField] private float rainSurvivalDrainMultiplier = 2.0f;

        public bool IsRaining { get; private set; } = false;
        public bool IsPlayerSheltered => BuildingInteriorManager.IsInsideInterior || BuildingInteriorManager.IsTransitioning;
        public bool IsPlayerExposedToRain => IsRaining && !IsPlayerSheltered && IsGameplaySceneActive();
        public float SurvivalDrainMultiplier => IsPlayerExposedToRain ? rainSurvivalDrainMultiplier : 1.0f;
        public float RainRemainingSeconds => IsRaining ? Mathf.Max(0f, _rainRemainingTimer) : 0f;
        public float NextRollRemainingSeconds => IsRaining ? 0f : Mathf.Max(0f, _rollTimer);
        public float RainIntensity { get; private set; } = 0f;
        public float GroundWetness { get; private set; } = 0f;
        public float RollIntervalSeconds => rollIntervalSeconds;
        public float RainChance => rainChance;
        public float RainDurationSeconds => rainDurationSeconds;

        private static readonly int PropGlobalWetness = Shader.PropertyToID("_GlobalWetness");

        private float _rollTimer = 60f;
        private float _rainRemainingTimer = 0f;

        // Procedural Rain & Splash VFX
        private GameObject _rainRigRoot;
        private ParticleSystem _rainDropPS;
        private ParticleSystem _rainSplashPS;
        private Texture2D _rainDropTex;
        private Texture2D _rainSplashTex;
        private Material _rainDropMat;
        private Material _rainSplashMat;

        // Procedural Soft Rain Audio
        private AudioSource _rainAudioSource;
        private AudioClip _rainLoopClip;

        // Rain HUD Badge (shown ONLY when outdoors in rain; hidden inside buildings!)
        private Texture2D _hudTex;
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
            Shader.SetGlobalFloat(PropGlobalWetness, 0f);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Shader.SetGlobalFloat(PropGlobalWetness, 0f);
            CleanupResources();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsGameplaySceneActive())
            {
                SetRainVisualsActive(false);
                Shader.SetGlobalFloat(PropGlobalWetness, 0f);
                if (_rainAudioSource != null) _rainAudioSource.volume = 0f;
            }
        }

        private static bool IsGameplaySceneActive()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.IndexOf("Gameplay", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // =========================================================================
        //  PUBLIC CONTROLS (F1 Debug Menu & Gameplay API — Co-op Synced!)
        // =========================================================================

        public void TriggerRain(float durationSeconds = -1f)
        {
            float dur = durationSeconds > 0.1f ? durationSeconds : rainDurationSeconds;

            // If triggered by a Co-op Client (e.g. from [F1] debug menu), ask Host to apply & broadcast to everyone!
            if (EmpireNetworkManager.IsOnlineClient && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
                {
                    w.Write(true);
                    w.Write(dur);
                    w.Write(false);
                });
                return;
            }

            ApplyRainStateLocal(isRaining: true, remainingSeconds: dur, snapInstant: false);
            BroadcastWeatherFromHost(snapInstant: false);
        }

        public void StopRain()
        {
            if (EmpireNetworkManager.IsOnlineClient && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
                {
                    w.Write(false);
                    w.Write(0f);
                    w.Write(false);
                });
                return;
            }

            ApplyRainStateLocal(isRaining: false, remainingSeconds: 0f, snapInstant: false);
            BroadcastWeatherFromHost(snapInstant: false);
        }

        public void ForceRollNow()
        {
            if (EmpireNetworkManager.IsOnlineClient)
            {
                // On Client, let Host run a manual rain trigger if requested via [F1]
                bool hit = Random.value < rainChance;
                if (hit) TriggerRain(rainDurationSeconds);
                else StopRain();
                return;
            }

            _rollTimer = rollIntervalSeconds;
            if (Random.value < rainChance)
            {
                ApplyRainStateLocal(true, rainDurationSeconds, snapInstant: false);
            }
            else
            {
                ApplyRainStateLocal(false, 0f, snapInstant: false);
            }
            BroadcastWeatherFromHost(snapInstant: false);
        }

        // =========================================================================
        //  MULTIPLAYER CO-OP SYNC (Host Authoritative + Late-Join)
        // =========================================================================

        public void SendWeatherStateToClient(ulong targetClientId)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;
            BroadcastWeatherFromHost(snapInstant: true, targetClientId: targetClientId);
        }

        private void BroadcastWeatherFromHost(bool snapInstant, ulong targetClientId = ulong.MaxValue)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            bool raining = IsRaining;
            float remain = RainRemainingSeconds;
            EmpireNetworkManager.Instance.SendPacket(NetPacketType.WeatherSync, w =>
            {
                w.Write(raining);
                w.Write(remain);
                w.Write(snapInstant);
            }, targetClientId);
        }

        public void HandleRemoteWeatherSync(bool isRaining, float remainingSeconds, bool snapInstant, bool receivedOnHost)
        {
            if (receivedOnHost)
            {
                // A Co-op Client requested a weather change (e.g. via [F1] menu) — Host accepts & broadcasts to all!
                ApplyRainStateLocal(isRaining, isRaining ? Mathf.Max(1f, remainingSeconds) : 0f, snapInstant: false);
                BroadcastWeatherFromHost(snapInstant: false);
                return;
            }

            ApplyRainStateLocal(isRaining, isRaining ? Mathf.Max(0f, remainingSeconds) : 0f, snapInstant);
        }

        private void ApplyRainStateLocal(bool isRaining, float remainingSeconds, bool snapInstant)
        {
            IsRaining = isRaining;
            _rainRemainingTimer = isRaining ? remainingSeconds : 0f;
            if (!isRaining)
            {
                _rollTimer = rollIntervalSeconds;
            }

            if (snapInstant)
            {
                RainIntensity = isRaining ? 1f : 0f;
                GroundWetness = isRaining ? 1f : 0f;
            }
        }

        // =========================================================================
        //  UPDATE LOOP: 60s RANDOM ROLL, 30s RAIN TIMER, PARTICLES, WETNESS & AUDIO
        // =========================================================================

        private void Update()
        {
            if (!IsGameplaySceneActive())
            {
                Shader.SetGlobalFloat(PropGlobalWetness, 0f);
                return;
            }

            float dt = Time.deltaTime;

            // 1. Authoritative Rain Timer & 60s Random Roll (Offline or Host)
            if (!EmpireNetworkManager.IsOnlineClient)
            {
                if (IsRaining)
                {
                    _rainRemainingTimer -= dt;
                    if (_rainRemainingTimer <= 0f)
                    {
                        IsRaining = false;
                        _rainRemainingTimer = 0f;
                        _rollTimer = rollIntervalSeconds;
                        BroadcastWeatherFromHost(snapInstant: false);
                    }
                }
                else
                {
                    _rollTimer -= dt;
                    if (_rollTimer <= 0f)
                    {
                        _rollTimer = rollIntervalSeconds;
                        if (Random.value < rainChance)
                        {
                            IsRaining = true;
                            _rainRemainingTimer = rainDurationSeconds;
                            BroadcastWeatherFromHost(snapInstant: false);
                        }
                    }
                }
            }
            else if (IsRaining && _rainRemainingTimer > 0f)
            {
                // Smooth local countdown on Client while waiting for Host stop packet
                _rainRemainingTimer = Mathf.Max(0f, _rainRemainingTimer - dt);
            }

            // 2. Smooth Rain Intensity & Ground Wetness Curves
            float targetIntensity = IsRaining ? 1f : 0f;
            float speed = IsRaining ? fadeInSpeed : fadeOutSpeed;
            RainIntensity = Mathf.MoveTowards(RainIntensity, targetIntensity, dt * speed);

            float wetSpeed = IsRaining ? 0.18f : 0.12f;
            GroundWetness = Mathf.MoveTowards(GroundWetness, targetIntensity, dt * wetSpeed);

            bool insideInterior = IsPlayerSheltered;

            // 3. Apply Wet Ground Shader Global (0 indoors so interior floors stay dry!)
            Shader.SetGlobalFloat(PropGlobalWetness, insideInterior ? 0f : GroundWetness);

            // 4. Update Rain Particle Rig (Follows Local Player / Camera outdoors, completely hidden indoors)
            bool showParticles = RainIntensity > 0.01f && !insideInterior;
            if (showParticles)
            {
                EnsureRainRigCreated();
                UpdateRainRigPositionAndEmission();
            }
            else if (_rainRigRoot != null)
            {
                SetRainVisualsActive(false);
            }

            // 5. Update Procedural Soft Rain Audio (completely off inside buildings)
            UpdateRainAudio(insideInterior);
        }

        private void EnsureRainRigCreated()
        {
            if (_rainRigRoot != null) return;

            _rainRigRoot = new GameObject("Weather_NormalRainRig");
            DontDestroyOnLoad(_rainRigRoot);

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            // --- 1. Falling Rain Streaks ---
            _rainDropTex = CreateRainStreakTexture();
            _rainDropMat = CreateAlphaBlendParticleMaterial(particleShader, _rainDropTex, new Color(0.76f, 0.86f, 0.98f, 0.52f));

            GameObject dropObj = new GameObject("RainDrops");
            dropObj.transform.SetParent(_rainRigRoot.transform, false);
            dropObj.transform.localPosition = new Vector3(0f, 15f, 0f);
            _rainDropPS = dropObj.AddComponent<ParticleSystem>();

            var dMain = _rainDropPS.main;
            dMain.loop = true;
            dMain.playOnAwake = true;
            dMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.62f, 0.82f);
            dMain.startSpeed = 0f;
            dMain.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.085f);
            dMain.startColor = new Color(0.78f, 0.88f, 1f, 0.55f);
            dMain.maxParticles = 260;

            var dEmis = _rainDropPS.emission;
            dEmis.enabled = true;
            dEmis.rateOverTime = 0f;

            var dShape = _rainDropPS.shape;
            dShape.enabled = true;
            dShape.shapeType = ParticleSystemShapeType.Box;
            dShape.scale = new Vector3(30f, 1.5f, 22f);

            var dVel = _rainDropPS.velocityOverLifetime;
            dVel.enabled = true;
            dVel.space = ParticleSystemSimulationSpace.World;
            dVel.x = -2.2f;
            dVel.y = -24.0f;
            dVel.z = -1.2f;

            var dRen = _rainDropPS.GetComponent<ParticleSystemRenderer>();
            dRen.renderMode = ParticleSystemRenderMode.Stretch;
            dRen.lengthScale = 0.0f;
            dRen.velocityScale = 0.045f;
            dRen.sharedMaterial = _rainDropMat;
            dRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dRen.receiveShadows = false;

            // --- 2. Ground Rain Splash Rings ---
            _rainSplashTex = CreateRainSplashTexture();
            _rainSplashMat = CreateAlphaBlendParticleMaterial(particleShader, _rainSplashTex, new Color(0.80f, 0.90f, 1f, 0.42f));

            GameObject splashObj = new GameObject("RainSplashes");
            splashObj.transform.SetParent(_rainRigRoot.transform, false);
            splashObj.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            _rainSplashPS = splashObj.AddComponent<ParticleSystem>();

            var sMain = _rainSplashPS.main;
            sMain.loop = true;
            sMain.playOnAwake = true;
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
            sMain.startSpeed = 0f;
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
            sMain.startColor = new Color(0.82f, 0.92f, 1f, 0.42f);
            sMain.maxParticles = 110;

            var sEmis = _rainSplashPS.emission;
            sEmis.enabled = true;
            sEmis.rateOverTime = 0f;

            var sShape = _rainSplashPS.shape;
            sShape.enabled = true;
            sShape.shapeType = ParticleSystemShapeType.Box;
            sShape.scale = new Vector3(26f, 0.05f, 18f);

            var sSize = _rainSplashPS.sizeOverLifetime;
            sSize.enabled = true;
            AnimationCurve growCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1.0f));
            sSize.size = new ParticleSystem.MinMaxCurve(1f, growCurve);

            var sCol = _rainSplashPS.colorOverLifetime;
            sCol.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            sCol.color = fadeGrad;

            var sRen = _rainSplashPS.GetComponent<ParticleSystemRenderer>();
            sRen.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            sRen.sharedMaterial = _rainSplashMat;
            sRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sRen.receiveShadows = false;
        }

        private void UpdateRainRigPositionAndEmission()
        {
            if (_rainRigRoot == null) return;
            if (!_rainRigRoot.activeSelf) _rainRigRoot.SetActive(true);

            Vector3 anchor = Vector3.zero;
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc != null)
            {
                anchor = pc.transform.position;
            }
            else if (Camera.main != null)
            {
                anchor = Camera.main.transform.position;
            }
            _rainRigRoot.transform.position = new Vector3(anchor.x, 0f, anchor.z);

            if (_rainDropPS != null)
            {
                var em = _rainDropPS.emission;
                em.rateOverTime = 240f * RainIntensity;
                if (!_rainDropPS.isPlaying) _rainDropPS.Play();
            }

            if (_rainSplashPS != null)
            {
                var em = _rainSplashPS.emission;
                em.rateOverTime = 95f * RainIntensity;
                if (!_rainSplashPS.isPlaying) _rainSplashPS.Play();
            }
        }

        private void SetRainVisualsActive(bool active)
        {
            if (_rainDropPS != null)
            {
                var em = _rainDropPS.emission;
                em.rateOverTime = 0f;
                if (!active)
                {
                    _rainDropPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            if (_rainSplashPS != null)
            {
                var em = _rainSplashPS.emission;
                em.rateOverTime = 0f;
                if (!active)
                {
                    _rainSplashPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            if (_rainRigRoot != null && _rainRigRoot.activeSelf != active)
            {
                _rainRigRoot.SetActive(active);
            }
        }

        private void UpdateRainAudio(bool insideInterior)
        {
            float targetVol = RainIntensity * (insideInterior ? indoorRainVolume : outdoorRainVolume);
            if (targetVol <= 0.002f)
            {
                if (_rainAudioSource != null && _rainAudioSource.isPlaying)
                {
                    _rainAudioSource.volume = 0f;
                    _rainAudioSource.Stop();
                }
                return;
            }

            if (_rainAudioSource == null)
            {
                _rainAudioSource = gameObject.AddComponent<AudioSource>();
                _rainAudioSource.loop = true;
                _rainAudioSource.playOnAwake = false;
                _rainAudioSource.spatialBlend = 0f;
                _rainLoopClip = CreateProceduralRainClip();
                _rainAudioSource.clip = _rainLoopClip;
            }

            _rainAudioSource.volume = targetVol;
            _rainAudioSource.pitch = insideInterior ? 0.78f : 1.0f;
            if (!_rainAudioSource.isPlaying)
            {
                _rainAudioSource.Play();
            }
        }

        // =========================================================================
        //  PROCEDURAL TEXTURES, MATERIALS & SOFT RAIN AUDIO CLIP
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
                // Soft vertical fade: brightest near bottom tip, tapering gently toward top tail
                float vFade = Mathf.Sin(v * Mathf.PI);
                for (int x = 0; x < w; x++)
                {
                    float u = ((float)x / (w - 1)) * 2f - 1f;
                    float hFade = Mathf.Clamp01(1f - Mathf.Abs(u));
                    hFade *= hFade;
                    float a = vFade * hFade;
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
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
                    // Crisp hollow ripple ring at r ~ 0.72
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) / 0.20f);
                    float a = ring * ring * (r < 1.0f ? 1f : 0f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static AudioClip CreateProceduralRainClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2; // 2-second seamless loop
            float[] data = new float[sampleCount];

            System.Random rng = new System.Random(20261127);
            float lp1 = 0f;
            float lp2 = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Gentle two-stage low-pass filter for natural soft rain hush (no harsh hiss)
                lp1 = lp1 + 0.14f * (white - lp1);
                lp2 = lp2 + 0.08f * (lp1 - lp2);
                float sample = (lp1 * 0.45f + lp2 * 0.55f) * 0.55f;

                // Subtle occasional raindrop patter impulse
                if (rng.NextDouble() < 0.0012)
                {
                    sample += (float)(rng.NextDouble() * 0.35 - 0.175);
                }
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            // Crossfade loop seam (last 2048 samples into first 2048 samples) for click-free looping
            const int seam = 2048;
            for (int i = 0; i < seam; i++)
            {
                float t = (float)i / seam;
                data[i] = Mathf.Lerp(data[sampleCount - seam + i], data[i], t);
            }

            AudioClip clip = AudioClip.Create("ProceduralSoftRain", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnGUI()
        {
            // Never show weather status HUD while inside a building interior or outside gameplay
            if (!IsPlayerExposedToRain || RainIntensity < 0.05f) return;

            if (_hudTex == null)
            {
                _hudTex = new Texture2D(1, 1);
                _hudTex.SetPixel(0, 0, Color.white);
                _hudTex.Apply();
            }

            if (_hudLabelStyle == null)
            {
                _hudLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _hudLabelStyle.normal.textColor = new Color(0.84f, 0.94f, 1f, 1f);
            }

            float alpha = Mathf.Clamp01(RainIntensity);
            float w = 360f;
            float h = 26f;
            float x = (Screen.width - w) * 0.5f;
            float y = 78f;

            Color prev = GUI.color;
            GUI.color = new Color(0.06f, 0.14f, 0.24f, 0.80f * alpha);
            GUI.DrawTexture(new Rect(x, y, w, h), _hudTex);
            GUI.color = new Color(0.35f, 0.72f, 1f, 0.85f * alpha);
            GUI.DrawTexture(new Rect(x, y, w, 2f), _hudTex);
            GUI.DrawTexture(new Rect(x, y + h - 2f, w, 2f), _hudTex);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x, y, w, h), $"RAINING ({RainRemainingSeconds:F0}s)  |  SANITY / HUNGER / THIRST x{rainSurvivalDrainMultiplier:F0}", _hudLabelStyle);
            GUI.color = prev;
        }

        private void CleanupResources()
        {
            if (_rainRigRoot != null) Destroy(_rainRigRoot);
            if (_rainDropMat != null) Destroy(_rainDropMat);
            if (_rainSplashMat != null) Destroy(_rainSplashMat);
            if (_rainDropTex != null) Destroy(_rainDropTex);
            if (_rainSplashTex != null) Destroy(_rainSplashTex);
            if (_rainLoopClip != null) Destroy(_rainLoopClip);
            if (_hudTex != null) Destroy(_hudTex);
        }
    }
}
