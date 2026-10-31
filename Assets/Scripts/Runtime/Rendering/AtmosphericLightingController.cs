using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages URP cinematic atmospheric lighting, including Golden Hour sun angles,
    /// soft linear distance fog, Atmospheric Dust Motes, and Post-Processing.
    /// Eliminates bulky mesh billboard quads that slice across the screen.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphericLightingController : MonoBehaviour
    {
        public static AtmosphericLightingController Instance { get; private set; }

        [Header("Sun & Golden Hour Directional Light")]
        [SerializeField] private Light sunLight;
        [Tooltip("Low pitch angle (26-38 deg) to cast dramatic long diagonal shadows")]
        [Range(10f, 60f)]
        [SerializeField] private float sunPitchAngle = 32f;
        [Range(-180f, 180f)]
        [SerializeField] private float sunYawAngle = -135f;
        [SerializeField] private Color goldenSunColor = new Color(1.0f, 0.90f, 0.68f, 1.0f);
        [SerializeField] private float sunIntensity = 1.35f;

        [Header("Bangkok PM 2.5 Golden Smog Haze (ม่านหมอกมลพิษสีเหลืองทองแบบกรุงเทพฯ)")]
        [SerializeField] private bool enableFog = true;
        [Tooltip("Bangkok PM 2.5 dense smog haze color (dusty mustard-sepia)")]
        [SerializeField] private Color goldenFogColor = new Color(0.85f, 0.76f, 0.50f, 1.0f);
        [Tooltip("Dusk post-fire smoldering smoke sunset")]
        [SerializeField] private Color duskFogColor = new Color(0.82f, 0.55f, 0.35f, 1.0f);
        [Tooltip("Night ash smog & darkness")]
        [SerializeField] private Color nightFogColor = new Color(0.12f, 0.16f, 0.24f, 1.0f);
        [Tooltip("Distance where smog begins to obscure the scene (close to camera)")]
        [Range(1f, 15f)]
        [SerializeField] private float fogStartDistance = 2.0f;
        [Tooltip("Distance where smog reaches full opacity (screen edges)")]
        [Range(10f, 35f)]
        [SerializeField] private float fogEndDistance = 20.0f;

        [Header("Airborne Micro-Dust Flecks (Faint Ambient Shimmer)")]
        [Tooltip("Disabled by default for clean pure Bangkok PM 2.5 smog haze (zero distracting dots)")]
        [SerializeField] private bool enableDustMotes = false;
        [Range(20, 150)]
        [SerializeField] private int dustMoteCount = 60;
        [Tooltip("Micro-fine dust fleck size (meters)")]
        [Range(0.04f, 0.5f)]
        [SerializeField] private float dustMoteSize = 0.08f;
        [Tooltip("Daytime PM 2.5 micro-dust color")]
        [SerializeField] private Color dayDustColor = new Color(0.85f, 0.78f, 0.55f, 0.18f);
        [Tooltip("Dusk post-fire ash color")]
        [SerializeField] private Color duskDustColor = new Color(0.80f, 0.55f, 0.35f, 0.20f);
        [Tooltip("Night dark soot color")]
        [SerializeField] private Color nightDustColor = new Color(0.30f, 0.35f, 0.45f, 0.15f);
        [SerializeField] private Material dustMoteMaterial;

        [Header("Post-Processing & Volume")]
        [SerializeField] private VolumeProfile targetVolumeProfile;

        private Volume _sceneGlobalVolume;
        private ParticleSystem _dustParticleSystem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (Instance == null && Application.isPlaying)
            {
                AtmosphericLightingController existing = Object.FindFirstObjectByType<AtmosphericLightingController>();
                if (existing == null)
                {
                    GameObject go = new GameObject("AtmosphericLightingController");
                    go.AddComponent<AtmosphericLightingController>();
                    DontDestroyOnLoad(go);
                }
            }
        }

        private void Awake()
        {
            if (Application.isPlaying)
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
            }

            // Immediately destroy any legacy god ray containers that followed the player
            DestroyLegacyGodRayContainers();
            FindOrCreateReferences();
        }

        private void Start()
        {
            DestroyLegacyGodRayContainers();
            ApplyAtmosphere();
            SetupCameraAndVolume();
            SetupDustMotes();
        }

        private void OnEnable()
        {
            DestroyLegacyGodRayContainers();
            ApplyAtmosphere();
            SetupDustMotes();
        }

        private void Update()
        {
            Transform followTarget = Camera.main != null ? Camera.main.transform : transform;
            if (followTarget != null)
            {
                // Smoothly position PM 2.5 ash particulate emitter at eye level around camera view
                if (_dustParticleSystem != null)
                {
                    Vector3 dustPos = followTarget.position;
                    dustPos.y = 2.0f; // Airborne PM 2.5 dust & soot particulates
                    _dustParticleSystem.transform.position = dustPos;
                }
            }

            // Sync with DayNightManager
            if (Application.isPlaying && DayNightManager.Instance != null)
            {
                SyncWithDayNightCycle();
            }
        }

        public void DestroyLegacyGodRayContainers()
        {
            var old1 = GameObject.Find("VolumetricGodRaysContainer");
            if (old1 != null)
            {
                if (Application.isPlaying) Destroy(old1);
                else DestroyImmediate(old1);
            }

            Transform child = transform.Find("VolumetricGodRaysContainer");
            if (child != null)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private void FindOrCreateReferences()
        {
            if (sunLight == null)
            {
                Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in allLights)
                {
                    if (l != null && l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            if (dustMoteMaterial == null)
            {
                Shader s = Shader.Find("Custom/AtmosphericDustMote");
                if (s != null) dustMoteMaterial = new Material(s);
            }

            if (targetVolumeProfile == null)
            {
                targetVolumeProfile = Resources.Load<VolumeProfile>("Settings/DefaultVolumeProfile");
            }
        }

        public void ApplyAtmosphere()
        {
            if (sunLight != null)
            {
                if (DayNightManager.Instance == null || !Application.isPlaying)
                {
                    sunLight.transform.rotation = Quaternion.Euler(sunPitchAngle, sunYawAngle, 0f);
                    sunLight.color = goldenSunColor;
                    sunLight.intensity = sunIntensity;
                }

                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowResolution = LightShadowResolution.VeryHigh;
                sunLight.shadowNormalBias = 0.4f;
                sunLight.shadowBias = 0.05f;
            }

            if (enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;
                RenderSettings.fogColor = goldenFogColor;
            }
            else
            {
                RenderSettings.fog = false;
            }

            if (Camera.main != null)
            {
                Camera.main.backgroundColor = goldenFogColor;
            }
        }

        private void SetupCameraAndVolume()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = goldenFogColor;
                UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                    camData.requiresDepthTexture = true;
                    camData.requiresColorTexture = true;
                }
            }

            if (_sceneGlobalVolume == null)
            {
                Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                foreach (var v in volumes)
                {
                    if (v.isGlobal)
                    {
                        _sceneGlobalVolume = v;
                        break;
                    }
                }

                if (_sceneGlobalVolume == null)
                {
                    GameObject volObj = new GameObject("AtmosphericGlobalVolume");
                    volObj.transform.SetParent(transform);
                    _sceneGlobalVolume = volObj.AddComponent<Volume>();
                    _sceneGlobalVolume.isGlobal = true;
                    _sceneGlobalVolume.priority = 1f;
                    _sceneGlobalVolume.weight = 1f;
                }
            }

            if (_sceneGlobalVolume != null)
            {
                if (_sceneGlobalVolume.profile == null)
                {
                    _sceneGlobalVolume.profile = targetVolumeProfile != null ? targetVolumeProfile : ScriptableObject.CreateInstance<VolumeProfile>();
                }
                var profile = _sceneGlobalVolume.profile;
                if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
                {
                    colorAdj = profile.Add<ColorAdjustments>(true);
                }
                colorAdj.active = true;
                colorAdj.contrast.Override(-14f); // Low contrast for authentic thick Bangkok PM 2.5 smog
                colorAdj.colorFilter.Override(new Color(1.0f, 0.95f, 0.82f, 1f)); // Warm golden-mustard atmospheric wash
                colorAdj.postExposure.Override(0.22f);

                if (!profile.TryGet<Bloom>(out var bloom))
                {
                    bloom = profile.Add<Bloom>(true);
                }
                bloom.active = true;
                bloom.threshold.Override(0.85f);
                bloom.intensity.Override(0.50f);
                bloom.scatter.Override(0.70f);
            }
        }

        public void SetupDustMotes()
        {
            if (!enableDustMotes)
            {
                if (_dustParticleSystem != null)
                {
                    if (Application.isPlaying) Destroy(_dustParticleSystem.gameObject);
                    else DestroyImmediate(_dustParticleSystem.gameObject);
                    _dustParticleSystem = null;
                }
                return;
            }

            if (_dustParticleSystem == null)
            {
                GameObject dustObj = new GameObject("AtmosphericDustMotes");
                dustObj.transform.SetParent(transform);
                _dustParticleSystem = dustObj.AddComponent<ParticleSystem>();

                var renderer = dustObj.GetComponent<ParticleSystemRenderer>();
                if (dustMoteMaterial == null)
                {
                    Shader s = Shader.Find("Custom/AtmosphericDustMote");
                    if (s != null) dustMoteMaterial = new Material(s);
                }
                renderer.sharedMaterial = dustMoteMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var ps = _dustParticleSystem;
            var main = ps.main;
            main.loop = true;
            main.maxParticles = dustMoteCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustMoteSize * 0.4f, dustMoteSize * 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startColor = dayDustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = ps.emission;
            emission.rateOverTime = dustMoteCount / 7.5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 7f, 32f);
            shape.position = new Vector3(0f, 1.5f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.06f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f);

            var rotLife = ps.rotationOverLifetime;
            rotLife.enabled = true;
            rotLife.separateAxes = false;
            rotLife.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = 0.18f;
            noise.frequency = 0.35f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.20f), new GradientAlphaKey(1f, 0.80f), new GradientAlphaKey(0f, 1f) }
            );
            colorLife.color = grad;

            if (!ps.isPlaying) ps.Play();
        }

        private void SyncWithDayNightCycle()
        {
            var dn = DayNightManager.Instance;
            if (dn == null) return;

            Color targetFog = (dn.CurrentPhase == DayNightManager.TimePhase.Night) ? nightFogColor :
                              (dn.CurrentPhase == DayNightManager.TimePhase.Dusk) ? duskFogColor : goldenFogColor;
            Color targetDust = (dn.CurrentPhase == DayNightManager.TimePhase.Night) ? nightDustColor :
                               (dn.CurrentPhase == DayNightManager.TimePhase.Dusk) ? duskDustColor : dayDustColor;

            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 2f);
            RenderSettings.fogStartDistance = Mathf.MoveTowards(RenderSettings.fogStartDistance, fogStartDistance, Time.deltaTime * 6f);
            RenderSettings.fogEndDistance = Mathf.MoveTowards(RenderSettings.fogEndDistance, fogEndDistance, Time.deltaTime * 12f);

            if (_dustParticleSystem != null)
            {
                var main = _dustParticleSystem.main;
                main.startColor = Color.Lerp(main.startColor.color, targetDust, Time.deltaTime * 2f);
            }
        }

        private void OnDisable()
        {
            DestroyLegacyGodRayContainers();

            if (_dustParticleSystem != null)
            {
                if (Application.isPlaying) Destroy(_dustParticleSystem.gameObject);
                else DestroyImmediate(_dustParticleSystem.gameObject);
                _dustParticleSystem = null;
            }
        }
    }
}
