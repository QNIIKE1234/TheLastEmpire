using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages URP cinematic atmospheric lighting, including Golden Hour sun angles,
    /// soft ExponentialSquared fog, SSAO depth, and Volumetric God Ray Beams (Sun Shafts).
    /// </summary>
    [ExecuteAlways]
    public class AtmosphericLightingController : MonoBehaviour
    {
        public static AtmosphericLightingController Instance { get; private set; }

        [Header("Sun & Golden Hour Directional Light")]
        [SerializeField] private Light sunLight;
        [Tooltip("Low pitch angle (18-32 deg) to cast dramatic long diagonal shadows and sunbeams")]
        [Range(10f, 60f)]
        [SerializeField] private float sunPitchAngle = 26f;
        [Range(-180f, 180f)]
        [SerializeField] private float sunYawAngle = -135f;
        [SerializeField] private Color goldenSunColor = new Color(1.0f, 0.88f, 0.72f, 1.0f);
        [SerializeField] private float sunIntensity = 1.45f;

        [Header("Atmospheric Mist & Height Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private Color goldenFogColor = new Color(0.92f, 0.82f, 0.65f, 1.0f);
        [SerializeField] private Color duskFogColor = new Color(0.95f, 0.55f, 0.35f, 1.0f);
        [SerializeField] private Color nightFogColor = new Color(0.08f, 0.10f, 0.16f, 1.0f);
        [Range(0.001f, 0.05f)]
        [SerializeField] private float fogDensity = 0.006f;

        [Header("Volumetric God Ray Beams (Sun Shafts)")]
        [SerializeField] private bool enableGodRays = true;
        [SerializeField] private Material godRayMaterial;
        [Range(1, 10)]
        [SerializeField] private int beamCount = 6;
        [Range(15f, 70f)]
        [SerializeField] private float beamLength = 45f;
        [Range(4f, 25f)]
        [SerializeField] private float beamWidth = 12f;
        [Range(0.2f, 3.5f)]
        [SerializeField] private float godRayIntensity = 1.8f;

        [Header("Post-Processing & Volume")]
        [SerializeField] private VolumeProfile targetVolumeProfile;

        private GameObject _godRayContainer;
        private readonly List<Transform> _beamTransforms = new List<Transform>();
        private readonly List<Material> _beamMaterialInstances = new List<Material>();
        private Volume _sceneGlobalVolume;

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
        }

        private void OnEnable()
        {
            ApplyAtmosphere();
            SetupGodRayBeams();
        }

        private void Update()
        {
            // Keep the god ray cluster centered smoothly near camera/player
            Transform followTarget = Camera.main != null ? Camera.main.transform : transform;
            if (_godRayContainer != null && followTarget != null)
            {
                Vector3 targetPos = followTarget.position;
                targetPos.y = 0f; // ground reference
                _godRayContainer.transform.position = Vector3.Lerp(_godRayContainer.transform.position, targetPos, Time.deltaTime * 5f);
            }

            // Sync with DayNightManager if present
            if (Application.isPlaying && DayNightManager.Instance != null)
            {
                SyncWithDayNightCycle();
            }
        }

        private void FindOrCreateReferences()
        {
            // Find sun
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

            // Load default god ray material if not assigned
            if (godRayMaterial == null)
            {
                godRayMaterial = Resources.Load<Material>("Materials/VolumetricGodRayBeam");
                if (godRayMaterial == null)
                {
                    // Fallback to loading via asset database in editor, or shader lookup
                    Shader s = Shader.Find("Custom/VolumetricGodRayBeam");
                    if (s != null) godRayMaterial = new Material(s);
                }
            }

            // Load DefaultVolumeProfile if not assigned
            if (targetVolumeProfile == null)
            {
                targetVolumeProfile = Resources.Load<VolumeProfile>("Settings/DefaultVolumeProfile");
            }
        }

        public void ApplyAtmosphere()
        {
            // 1. Configure Directional Light (Sun)
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(sunPitchAngle, sunYawAngle, 0f);
                sunLight.color = goldenSunColor;
                sunLight.intensity = sunIntensity;
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowResolution = LightShadowResolution.VeryHigh;
                sunLight.shadowNormalBias = 0.4f;
                sunLight.shadowBias = 0.05f;
            }

            // 2. Configure RenderSettings Fog
            if (enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = goldenFogColor;
                RenderSettings.fogDensity = fogDensity;
            }
            else
            {
                RenderSettings.fog = false;
            }
        }

        private void SetupCameraAndVolume()
        {
            // Ensure Camera renders post-processing & generates depth texture
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

            // Ensure a Global Volume exists in scene
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

        private void SetupGodRayBeams()
        {
            if (!enableGodRays || godRayMaterial == null) return;

            if (_godRayContainer != null)
            {
                if (Application.isPlaying) Destroy(_godRayContainer);
                else DestroyImmediate(_godRayContainer);
                _beamTransforms.Clear();
                _beamMaterialInstances.Clear();
            }

            _godRayContainer = new GameObject("VolumetricGodRaysContainer");
            _godRayContainer.transform.SetParent(transform);

            // Compute sunlight direction
            Vector3 sunForward = sunLight != null ? sunLight.transform.forward : Quaternion.Euler(sunPitchAngle, sunYawAngle, 0f) * Vector3.forward;
            Quaternion rayRotation = Quaternion.LookRotation(sunForward, Vector3.up);

            // Procedural grid distribution around the scene area
            float spread = 18f;
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(-spread * 0.7f, -spread * 0.4f),
                new Vector2( spread * 0.5f, -spread * 0.6f),
                new Vector2(-spread * 0.2f,  spread * 0.5f),
                new Vector2( spread * 0.8f,  spread * 0.3f),
                new Vector2(-spread * 0.8f,  spread * 0.2f),
                new Vector2( 0f,            -spread * 0.1f),
                new Vector2( spread * 0.3f,  spread * 0.7f),
                new Vector2(-spread * 0.4f, -spread * 0.8f),
            };

            int count = Mathf.Min(beamCount, offsets.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject beamObj = new GameObject($"GodRayBeam_{i + 1:00}");
                beamObj.transform.SetParent(_godRayContainer.transform, false);

                // Offset beam along horizontal ground
                Vector3 localPos = new Vector3(offsets[i].x, 0f, offsets[i].y);
                beamObj.transform.localPosition = localPos;
                beamObj.transform.rotation = rayRotation;

                // Mesh Quad for the beam
                MeshFilter mf = beamObj.AddComponent<MeshFilter>();
                MeshRenderer mr = beamObj.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;

                // Create individual material instance for slight variation
                Material instMat = new Material(godRayMaterial);
                instMat.SetFloat("_Intensity", godRayIntensity * Random.Range(0.85f, 1.25f));
                mr.sharedMaterial = instMat;
                _beamMaterialInstances.Add(instMat);

                // Build Quad mesh oriented from origin down the beam vector
                float w = beamWidth * Random.Range(0.85f, 1.2f);
                float len = beamLength * Random.Range(0.9f, 1.15f);
                mf.sharedMesh = GenerateBeamMesh(w, len);

                _beamTransforms.Add(beamObj.transform);
            }
        }

        private Mesh GenerateBeamMesh(float width, float length)
        {
            Mesh mesh = new Mesh();
            mesh.name = "GodRayBeamQuad";

            float hw = width * 0.5f;

            // Vertices: top source (y=1 in uv) to bottom ground (y=0 in uv)
            // Stretched along local Z axis into the sunlight direction
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-hw, 0f, 0f),      // Top Left
                new Vector3( hw, 0f, 0f),      // Top Right
                new Vector3(-hw * 1.35f, 0f, length), // Bottom Left (flaring wider)
                new Vector3( hw * 1.35f, 0f, length)  // Bottom Right (flaring wider)
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
                new Color(1f, 1f, 1f, 0.2f),
                new Color(1f, 1f, 1f, 0.2f)
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
                DayNightManager.TimePhase.Dusk => 0.75f,
                DayNightManager.TimePhase.Night => 0.12f, // Faint moonbeam
                _ => 1.0f
            };

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 1.5f);

            foreach (var mat in _beamMaterialInstances)
            {
                if (mat != null)
                {
                    mat.SetFloat("_Intensity", godRayIntensity * targetRayMul);
                }
            }
        }

        private void OnDisable()
        {
            if (_godRayContainer != null)
            {
                if (Application.isPlaying) Destroy(_godRayContainer);
                else DestroyImmediate(_godRayContainer);
            }
        }
    }
}
