using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages URP cinematic atmospheric lighting, including Golden Hour sun angles,
    /// soft linear distance fog, Volumetric God Ray Beams (Sun Shafts), and Atmospheric Dust Motes.
    /// Recreates the rich atmospheric woodland lighting from the reference image.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphericLightingController : MonoBehaviour
    {
        public static AtmosphericLightingController Instance { get; private set; }

        [Header("Sun & Golden Hour Directional Light")]
        [SerializeField] private Light sunLight;
        [Tooltip("Low pitch angle (26-38 deg) to cast dramatic long diagonal shadows and sunbeams")]
        [Range(10f, 60f)]
        [SerializeField] private float sunPitchAngle = 32f;
        [Range(-180f, 180f)]
        [SerializeField] private float sunYawAngle = -135f;
        [SerializeField] private Color goldenSunColor = new Color(1.0f, 0.90f, 0.74f, 1.0f);
        [SerializeField] private float sunIntensity = 1.45f;

        [Header("Atmospheric Mist & Linear Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private Color goldenFogColor = new Color(0.92f, 0.84f, 0.70f, 1.0f);
        [SerializeField] private Color duskFogColor = new Color(0.96f, 0.62f, 0.35f, 1.0f);
        [SerializeField] private Color nightFogColor = new Color(0.08f, 0.12f, 0.22f, 1.0f);
        [SerializeField] private float fogStartDistance = 18f;
        [SerializeField] private float fogEndDistance = 75f;

        [Header("Volumetric God Ray Beams (Sun Shafts)")]
        [SerializeField] private bool enableGodRays = true;
        [SerializeField] private Material godRayMaterial;
        [Range(1, 12)]
        [SerializeField] private int beamCount = 8;
        [Range(20f, 80f)]
        [SerializeField] private float beamLength = 55f;
        [Range(6f, 30f)]
        [SerializeField] private float beamWidth = 14f;
        [Range(0.2f, 3.5f)]
        [SerializeField] private float godRayIntensity = 1.75f;

        [Header("Atmospheric Floating Dust Particles (Dust Motes - ฝุ่นละอองในอากาศ)")]
        [SerializeField] private bool enableDustMotes = true;
        [Range(50, 400)]
        [SerializeField] private int dustMoteCount = 180;
        [SerializeField] private float dustMoteSize = 0.14f;
        [SerializeField] private Color dayDustColor = new Color(1.0f, 0.92f, 0.75f, 0.70f);
        [SerializeField] private Color duskDustColor = new Color(1.0f, 0.68f, 0.35f, 0.85f);
        [SerializeField] private Color nightDustColor = new Color(0.35f, 0.55f, 0.85f, 0.30f);
        [SerializeField] private Material dustMoteMaterial;

        [Header("Post-Processing & Volume")]
        [SerializeField] private VolumeProfile targetVolumeProfile;

        private GameObject _godRayContainer;
        private readonly List<Transform> _beamTransforms = new List<Transform>();
        private readonly List<Material> _beamMaterialInstances = new List<Material>();
        private readonly List<Vector2> _beamOffsets = new List<Vector2>();
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

            FindOrCreateReferences();
        }

        private void Start()
        {
            ApplyAtmosphere();
            SetupCameraAndVolume();
            SetupGodRayBeams();
            SetupDustMotes();
        }

        private void OnEnable()
        {
            ApplyAtmosphere();
            SetupGodRayBeams();
            SetupDustMotes();
        }

        private void Update()
        {
            Transform followTarget = Camera.main != null ? Camera.main.transform : transform;
            if (followTarget != null)
            {
                Vector3 targetPos = followTarget.position;
                targetPos.y = 0f;

                // Center god ray cluster smoothly
                if (_godRayContainer != null)
                {
                    _godRayContainer.transform.position = Vector3.Lerp(_godRayContainer.transform.position, targetPos, Time.deltaTime * 5f);
                }

                // Center dust particle system
                if (_dustParticleSystem != null)
                {
                    Vector3 dustPos = followTarget.position;
                    dustPos.y = Mathf.Max(2f, dustPos.y * 0.5f);
                    _dustParticleSystem.transform.position = dustPos;
                }
            }

            // Sync with DayNightManager
            if (Application.isPlaying && DayNightManager.Instance != null)
            {
                SyncWithDayNightCycle();
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

            if (godRayMaterial == null)
            {
                godRayMaterial = Resources.Load<Material>("Materials/VolumetricGodRayBeam");
                if (godRayMaterial == null)
                {
                    Shader s = Shader.Find("Custom/VolumetricGodRayBeam");
                    if (s != null) godRayMaterial = new Material(s);
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

        public void SetupGodRayBeams()
        {
            if (!enableGodRays || godRayMaterial == null)
            {
                if (_godRayContainer != null)
                {
                    if (Application.isPlaying) Destroy(_godRayContainer);
                    else DestroyImmediate(_godRayContainer);
                    _beamTransforms.Clear();
                    _beamMaterialInstances.Clear();
                    _beamOffsets.Clear();
                }
                return;
            }

            if (_godRayContainer != null)
            {
                if (Application.isPlaying) Destroy(_godRayContainer);
                else DestroyImmediate(_godRayContainer);
                _beamTransforms.Clear();
                _beamMaterialInstances.Clear();
                _beamOffsets.Clear();
            }

            _godRayContainer = new GameObject("VolumetricGodRaysContainer");
            _godRayContainer.transform.SetParent(transform);

            Vector3 sunForward = sunLight != null ? sunLight.transform.forward : Quaternion.Euler(sunPitchAngle, sunYawAngle, 0f) * Vector3.forward;
            Quaternion rayRotation = Quaternion.LookRotation(sunForward, Vector3.up);

            float spread = 18f;
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(-spread * 0.65f, -spread * 0.35f),
                new Vector2( spread * 0.45f, -spread * 0.55f),
                new Vector2(-spread * 0.20f,  spread * 0.45f),
                new Vector2( spread * 0.75f,  spread * 0.25f),
                new Vector2(-spread * 0.75f,  spread * 0.20f),
                new Vector2( 0f,            -spread * 0.10f),
                new Vector2( spread * 0.30f,  spread * 0.65f),
                new Vector2(-spread * 0.35f, -spread * 0.75f),
            };

            int count = Mathf.Min(beamCount, offsets.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject beamObj = new GameObject($"GodRayBeam_{i + 1:00}");
                beamObj.transform.SetParent(_godRayContainer.transform, false);

                // Sky anchor: origin up in the sky streaming down toward local ground offset
                Vector3 groundOffset = new Vector3(offsets[i].x, 0f, offsets[i].y);
                Vector3 skyOrigin = groundOffset - sunForward * (beamLength * 0.90f);
                beamObj.transform.localPosition = skyOrigin;
                beamObj.transform.rotation = rayRotation;

                MeshFilter mf = beamObj.AddComponent<MeshFilter>();
                MeshRenderer mr = beamObj.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;

                Material instMat = new Material(godRayMaterial);
                instMat.SetFloat("_Intensity", godRayIntensity * Random.Range(0.85f, 1.25f));
                mr.sharedMaterial = instMat;
                _beamMaterialInstances.Add(instMat);

                float w = beamWidth * Random.Range(0.85f, 1.2f);
                float len = beamLength * Random.Range(0.9f, 1.15f);
                mf.sharedMesh = GenerateBeamMesh(w, len);

                _beamTransforms.Add(beamObj.transform);
                _beamOffsets.Add(offsets[i]);
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
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.40f);
            main.startSize = new ParticleSystem.MinMaxCurve(dustMoteSize * 0.6f, dustMoteSize * 1.6f);
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
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.06f, 0.20f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

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

        private Mesh GenerateBeamMesh(float width, float length)
        {
            Mesh mesh = new Mesh();
            mesh.name = "GodRayBeamQuad";

            float hw = width * 0.5f;

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-hw, 0f, 0f),
                new Vector3( hw, 0f, 0f),
                new Vector3(-hw * 1.4f, 0f, length),
                new Vector3( hw * 1.4f, 0f, length)
            };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            };

            Color[] colors = new Color[4]
            {
                Color.white,
                Color.white,
                new Color(1f, 1f, 1f, 0.15f),
                new Color(1f, 1f, 1f, 0.15f)
            };

            int[] triangles = new int[6]
            {
                0, 1, 2,
                1, 3, 2
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void SyncWithDayNightCycle()
        {
            var dn = DayNightManager.Instance;
            if (dn == null) return;

            Color targetFog = dn.CurrentPhase switch
            {
                DayNightManager.TimePhase.Day => goldenFogColor,
                DayNightManager.TimePhase.Dusk => duskFogColor,
                DayNightManager.TimePhase.Night => nightFogColor,
                _ => goldenFogColor
            };

            float targetRayMul = dn.CurrentPhase switch
            {
                DayNightManager.TimePhase.Day => 1.0f,
                DayNightManager.TimePhase.Dusk => 0.85f,
                DayNightManager.TimePhase.Night => 0.15f,
                _ => 1.0f
            };

            Color targetDust = dn.CurrentPhase switch
            {
                DayNightManager.TimePhase.Day => dayDustColor,
                DayNightManager.TimePhase.Dusk => duskDustColor,
                DayNightManager.TimePhase.Night => nightDustColor,
                _ => dayDustColor
            };

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 1.5f);

            foreach (var mat in _beamMaterialInstances)
            {
                if (mat != null)
                {
                    mat.SetFloat("_Intensity", godRayIntensity * targetRayMul);
                }
            }

            if (sunLight != null && _beamTransforms.Count > 0)
            {
                Vector3 sunForward = sunLight.transform.forward;
                Quaternion rayRotation = Quaternion.LookRotation(sunForward, Vector3.up);

                for (int i = 0; i < _beamTransforms.Count; i++)
                {
                    if (_beamTransforms[i] != null && i < _beamOffsets.Count)
                    {
                        Vector3 groundOffset = new Vector3(_beamOffsets[i].x, 0f, _beamOffsets[i].y);
                        _beamTransforms[i].localPosition = groundOffset - sunForward * (beamLength * 0.90f);
                        _beamTransforms[i].rotation = rayRotation;
                    }
                }
            }

            if (_dustParticleSystem != null)
            {
                var main = _dustParticleSystem.main;
                main.startColor = Color.Lerp(main.startColor.color, targetDust, Time.deltaTime * 2f);
            }
        }

        private void OnDisable()
        {
            if (_godRayContainer != null)
            {
                if (Application.isPlaying) Destroy(_godRayContainer);
                else DestroyImmediate(_godRayContainer);
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
