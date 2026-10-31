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
        [SerializeField] private Color goldenSunColor = new Color(1.0f, 0.92f, 0.78f, 1.0f);
        [SerializeField] private float sunIntensity = 1.45f;

        [Header("Atmospheric Mist & Linear Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private Color goldenFogColor = new Color(0.92f, 0.84f, 0.70f, 1.0f);
        [SerializeField] private Color duskFogColor = new Color(0.96f, 0.62f, 0.35f, 1.0f);
        [SerializeField] private Color nightFogColor = new Color(0.08f, 0.12f, 0.22f, 1.0f);
        [SerializeField] private float fogStartDistance = 18f;
        [SerializeField] private float fogEndDistance = 75f;

        [Header("Atmospheric Floating Dust Particles (Dust Motes - ฝุ่นละอองในอากาศ)")]
        [SerializeField] private bool enableDustMotes = true;
        [Range(50, 400)]
        [SerializeField] private int dustMoteCount = 180;
        [SerializeField] private float dustMoteSize = 0.12f;
        [SerializeField] private Color dayDustColor = new Color(1.0f, 0.92f, 0.75f, 0.70f);
        [SerializeField] private Color duskDustColor = new Color(1.0f, 0.68f, 0.35f, 0.85f);
        [SerializeField] private Color nightDustColor = new Color(0.35f, 0.55f, 0.85f, 0.30f);
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
                // Smoothly position dust particle emitter around the camera view
                if (_dustParticleSystem != null)
                {
                    Vector3 dustPos = followTarget.position;
                    dustPos.y = 3f;
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
        }

        private void SetupCameraAndVolume()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
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

            if (_sceneGlobalVolume != null && targetVolumeProfile != null)
            {
                _sceneGlobalVolume.profile = targetVolumeProfile;
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustMoteSize * 0.6f, dustMoteSize * 1.5f);
            main.startColor = dayDustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = ps.emission;
            emission.rateOverTime = dustMoteCount / 7.0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(28f, 10f, 28f);
            shape.position = new Vector3(0f, 3.5f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.20f, 0.20f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.06f, 0.20f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.20f, 0.20f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = 0.35f;
            noise.frequency = 0.40f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
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

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 1.5f);

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
