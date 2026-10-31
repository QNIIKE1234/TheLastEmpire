using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages atmospheric lighting, golden sunlight angles, volumetric procedural ground mist with stationary world-space sun shafts,
    /// airborne micro-dust motes, and post-processing without blinding the top-down camera view or causing polygon clipping.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphericLightingController : MonoBehaviour
    {
        public static AtmosphericLightingController Instance { get; private set; }

        [Header("Sun & Directional Light")]
        [SerializeField] private Light sunLight;
        [Tooltip("Sun pitch angle (deg) for diagonal shadows")]
        [Range(10f, 60f)]
        [SerializeField] private float sunPitchAngle = 32f;
        [Range(-180f, 180f)]
        [SerializeField] private float sunYawAngle = -135f;
        [SerializeField] private Color goldenSunColor = new Color(1.0f, 0.90f, 0.68f, 1.0f);
        [SerializeField] private float sunIntensity = 1.35f;

        [Header("Procedural Volumetric Ground Mist (หมอกเรี่ยพื้นสีเหลืองทอง พร้อมริ้วลำแสงแดด World-Space)")]
        [Tooltip("Spawns a swirling, translucent procedural mist plane that hugs the ground")]
        [SerializeField] private bool enableGroundMist = true;
        [Tooltip("Height above ground where mist plane hovers (meters)")]
        [Range(0.05f, 0.8f)]
        [SerializeField] private float groundMistHeight = 0.20f;
        [Tooltip("Ground mist plane size in meters (large enough to seamlessly cover camera view)")]
        [Range(40f, 120f)]
        [SerializeField] private float groundMistSize = 75f;
        [Tooltip("Warm golden morning / PM 2.5 champagne mist color")]
        [SerializeField] private Color dayGroundMistColor = new Color(0.98f, 0.92f, 0.74f, 0.22f);
        [Tooltip("Sunset warm amber mist color")]
        [SerializeField] private Color duskGroundMistColor = new Color(0.96f, 0.60f, 0.25f, 0.25f);
        [Tooltip("Night moonlit mist color")]
        [SerializeField] private Color nightGroundMistColor = new Color(0.35f, 0.48f, 0.70f, 0.16f);
        [SerializeField] private Material groundMistMaterial;

        [Header("Distance Fog (Disabled by default to prevent top-down viewport blinding)")]
        [SerializeField] private bool enableFog = false;
        [SerializeField] private Color goldenFogColor = new Color(0.92f, 0.80f, 0.48f, 1.0f);
        [SerializeField] private Color duskFogColor = new Color(0.85f, 0.55f, 0.30f, 1.0f);
        [SerializeField] private Color nightFogColor = new Color(0.12f, 0.16f, 0.24f, 1.0f);
        [Range(15f, 60f)]
        [SerializeField] private float fogStartDistance = 40.0f;
        [Range(50f, 150f)]
        [SerializeField] private float fogEndDistance = 90.0f;

        [Header("Airborne Micro-Dust Particles (ฝุ่นละอองทองบางๆ ลอยในอากาศ)")]
        [SerializeField] private bool enableDustMotes = true;
        [Range(10, 100)]
        [SerializeField] private int dustMoteCount = 45;
        [Range(0.04f, 0.3f)]
        [SerializeField] private float dustMoteSize = 0.08f;
        [SerializeField] private Color dayDustColor = new Color(0.96f, 0.88f, 0.60f, 0.20f);
        [SerializeField] private Color duskDustColor = new Color(0.88f, 0.58f, 0.32f, 0.20f);
        [SerializeField] private Color nightDustColor = new Color(0.35f, 0.42f, 0.55f, 0.12f);
        [SerializeField] private Material dustMoteMaterial;

        [Header("Post-Processing & Volume")]
        [SerializeField] private VolumeProfile targetVolumeProfile;

        private Volume _sceneGlobalVolume;
        private ParticleSystem _dustParticleSystem;
        private GameObject _groundMistObject;
        private Material _instancedGroundMistMaterial;

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

            DestroyLegacyGodRayContainers();
            FindOrCreateReferences();
        }

        private void Start()
        {
            DestroyLegacyGodRayContainers();
            ApplyAtmosphere();
            SetupCameraAndVolume();
            SetupGroundMist();
            SetupDustMotes();
        }

        private void OnEnable()
        {
            DestroyLegacyGodRayContainers();
            ApplyAtmosphere();
            SetupGroundMist();
            SetupDustMotes();
        }

        private void Update()
        {
            Transform followTarget = Camera.main != null ? Camera.main.transform : transform;
            if (followTarget != null)
            {
                Vector3 camPos = followTarget.position;

                // Keep ground mist plane hovering right above ground beneath camera
                if (_groundMistObject != null)
                {
                    _groundMistObject.transform.position = new Vector3(camPos.x, groundMistHeight, camPos.z);
                    _groundMistObject.transform.localScale = new Vector3(groundMistSize, groundMistSize, 1f);
                }

                // Keep micro-dust particulate volume centered around camera view
                if (_dustParticleSystem != null)
                {
                    Vector3 dustPos = camPos;
                    dustPos.y = 2.0f;
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
            if (!gameObject.scene.isLoaded) return;
            try
            {
                var old1 = GameObject.Find("VolumetricGodRaysContainer");
                if (old1 != null)
                {
                    if (Application.isPlaying) Destroy(old1);
                    else DestroyImmediate(old1);
                }

                var oldShafts = GameObject.Find("VolumetricSunShafts");
                if (oldShafts != null)
                {
                    if (Application.isPlaying) Destroy(oldShafts);
                    else DestroyImmediate(oldShafts);
                }

                Transform child1 = transform.Find("VolumetricGodRaysContainer");
                if (child1 != null)
                {
                    if (Application.isPlaying) Destroy(child1.gameObject);
                    else DestroyImmediate(child1.gameObject);
                }

                Transform childShafts = transform.Find("VolumetricSunShafts");
                if (childShafts != null)
                {
                    if (Application.isPlaying) Destroy(childShafts.gameObject);
                    else DestroyImmediate(childShafts.gameObject);
                }
            }
            catch {}
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

            if (groundMistMaterial == null)
            {
                Shader s = Shader.Find("Custom/ProceduralGroundMist");
                if (s != null) groundMistMaterial = new Material(s);
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
                colorAdj.contrast.Override(2f); // Crisp contrast so characters and terrain are sharp
                colorAdj.colorFilter.Override(Color.white); // Clean neutral color filter
                colorAdj.postExposure.Override(0.08f);

                if (!profile.TryGet<Bloom>(out var bloom))
                {
                    bloom = profile.Add<Bloom>(true);
                }
                bloom.active = true;
                bloom.threshold.Override(0.90f);
                bloom.intensity.Override(0.40f);
                bloom.scatter.Override(0.70f);
            }
        }

        public void SetupGroundMist()
        {
            if (!enableGroundMist)
            {
                if (_groundMistObject != null)
                {
                    if (Application.isPlaying) Destroy(_groundMistObject);
                    else DestroyImmediate(_groundMistObject);
                    _groundMistObject = null;
                }
                return;
            }

            if (_groundMistObject == null)
            {
                _groundMistObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _groundMistObject.name = "ProceduralGroundMistPlane";
                _groundMistObject.transform.SetParent(transform);

                // Remove collider so it never interferes with player movement or raycasts
                Collider col = _groundMistObject.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }

                // Rotate 90 deg around X to lay flat horizontally on ground, facing up towards top-down camera
                _groundMistObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _groundMistObject.transform.localScale = new Vector3(groundMistSize, groundMistSize, 1f);

                if (groundMistMaterial == null)
                {
                    Shader s = Shader.Find("Custom/ProceduralGroundMist");
                    if (s != null) groundMistMaterial = new Material(s);
                }

                if (groundMistMaterial != null)
                {
                    _instancedGroundMistMaterial = new Material(groundMistMaterial);
                    _instancedGroundMistMaterial.SetColor("_MistColor", dayGroundMistColor);
                    var rend = _groundMistObject.GetComponent<MeshRenderer>();
                    rend.sharedMaterial = _instancedGroundMistMaterial;
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }
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
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustMoteSize * 0.4f, dustMoteSize * 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startColor = dayDustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = ps.emission;
            emission.rateOverTime = dustMoteCount / 7.5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 6f, 32f);
            shape.position = new Vector3(0f, 1.5f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.06f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.20f, 0.25f);

            var rotLife = ps.rotationOverLifetime;
            rotLife.enabled = true;
            rotLife.separateAxes = false;
            rotLife.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = 0.15f;
            noise.frequency = 0.30f;
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
            Color targetMist = (dn.CurrentPhase == DayNightManager.TimePhase.Night) ? nightGroundMistColor :
                               (dn.CurrentPhase == DayNightManager.TimePhase.Dusk) ? duskGroundMistColor : dayGroundMistColor;

            if (enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 2f);
                RenderSettings.fogStartDistance = Mathf.MoveTowards(RenderSettings.fogStartDistance, fogStartDistance, Time.deltaTime * 6f);
                RenderSettings.fogEndDistance = Mathf.MoveTowards(RenderSettings.fogEndDistance, fogEndDistance, Time.deltaTime * 12f);
            }
            else
            {
                RenderSettings.fog = false;
            }

            // Sync dynamic ground mist color
            if (_instancedGroundMistMaterial != null)
            {
                Color cur = _instancedGroundMistMaterial.GetColor("_MistColor");
                _instancedGroundMistMaterial.SetColor("_MistColor", Color.Lerp(cur, targetMist, Time.deltaTime * 2f));
            }

            // Sync dust motes color
            if (_dustParticleSystem != null)
            {
                var main = _dustParticleSystem.main;
                main.startColor = Color.Lerp(main.startColor.color, targetDust, Time.deltaTime * 2f);
            }
        }

        private void OnDisable()
        {
            DestroyLegacyGodRayContainers();

            if (_groundMistObject != null)
            {
                if (Application.isPlaying) Destroy(_groundMistObject);
                else DestroyImmediate(_groundMistObject);
                _groundMistObject = null;
            }

            if (_dustParticleSystem != null)
            {
                if (Application.isPlaying) Destroy(_dustParticleSystem.gameObject);
                else DestroyImmediate(_dustParticleSystem.gameObject);
                _dustParticleSystem = null;
            }
        }
    }
}
