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

        [Header("Atmospheric Morning Mist & Dust Haze (หมอกและฝุ่นละอองยามเช้า)")]
        [SerializeField] private bool enableDustMotes = true;
        [Range(20, 150)]
        [SerializeField] private int dustMoteCount = 65;
        [Tooltip("Size of soft billowing mist/dust clouds (meters)")]
        [Range(0.8f, 5.0f)]
        [SerializeField] private float dustMoteSize = 2.4f;
        [Tooltip("Soft morning mist & dust color (translucent pale morning haze)")]
        [SerializeField] private Color dayDustColor = new Color(0.92f, 0.90f, 0.86f, 0.16f);
        [Tooltip("Warm evening sunset mist & dust haze")]
        [SerializeField] private Color duskDustColor = new Color(0.88f, 0.74f, 0.64f, 0.14f);
        [Tooltip("Subtle cool midnight fog mist")]
        [SerializeField] private Color nightDustColor = new Color(0.60f, 0.68f, 0.80f, 0.08f);
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
                // Smoothly position mist particle emitter low near ground level around the camera view
                if (_dustParticleSystem != null)
                {
                    Vector3 dustPos = followTarget.position;
                    dustPos.y = 0.8f; // Low ground-level morning mist
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustMoteSize * 0.7f, dustMoteSize * 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startColor = dayDustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = ps.emission;
            emission.rateOverTime = dustMoteCount / 8.0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 2.5f, 36f);
            shape.position = new Vector3(0f, 0.5f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f);

            var rotLife = ps.rotationOverLifetime;
            rotLife.enabled = true;
            rotLife.separateAxes = false;
            rotLife.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.8f);
            sizeCurve.AddKey(1f, 1.25f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = 0.22f;
            noise.frequency = 0.25f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
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
