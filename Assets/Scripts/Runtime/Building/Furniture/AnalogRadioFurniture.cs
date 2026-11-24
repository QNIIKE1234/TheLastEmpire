using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Retro Analog Radio furniture:
    /// - Press [E] to toggle Power ON / OFF
    /// - Press [Z] to tune between 3 procedural warm analog music stations
    /// - Emits a gentle Sanity-calming aura while playing music nearby!
    /// </summary>
    public class AnalogRadioFurniture : MonoBehaviour, IBuildingInteractable
    {
        [SerializeField] private float interactionDistance = 2.4f;
        [SerializeField] private float listeningRadius = 9.0f;
        [SerializeField] private float sanityRegenPerSecond = 2.0f;
        [SerializeField] private bool startTurnedOn = false;

        private static AudioClip[] _cachedStationClips;
        private static readonly string[] StationNames =
        {
            "FM 88.5 - Midnight Lo-Fi",
            "FM 94.2 - Campfire Acoustic",
            "FM 102.7 - Old World Jazz"
        };

        private BuildingObject _buildingObject;
        private AudioSource _audioSource;
        private Light _dialLight;
        private bool _isPlaying = false;
        private int _stationIndex = 0;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.65f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => isActiveAndEnabled && gameObject.activeInHierarchy && (_buildingObject == null || !_buildingObject.IsDead);

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            EnsureAudioSource();
            EnsureDialLight();
        }

        private void Start()
        {
            if (startTurnedOn)
            {
                SetPower(true);
            }
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private void EnsureAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.75f;
            _audioSource.minDistance = 2.5f;
            _audioSource.maxDistance = 14.0f;
            _audioSource.volume = 0.28f;
        }

        private void EnsureDialLight()
        {
            Transform existing = transform.Find("RadioDialLight");
            if (existing != null)
            {
                _dialLight = existing.GetComponent<Light>();
                return;
            }
            GameObject go = new GameObject("RadioDialLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.16f, 1.0f, 0.28f);
            _dialLight = go.AddComponent<Light>();
            _dialLight.type = LightType.Point;
            _dialLight.color = new Color(0.32f, 1.0f, 0.56f);
            _dialLight.range = 2.8f;
            _dialLight.intensity = 1.2f;
            _dialLight.shadows = LightShadows.None;
            _dialLight.enabled = false;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc != null && sanityRegenPerSecond > 0f)
            {
                if (Vector3.Distance(transform.position, pc.transform.position) <= listeningRadius)
                {
                    pc.RestoreSanity(sanityRegenPerSecond * Time.deltaTime);
                }
            }
        }

        public string GetInteractionPrompt()
        {
            return _isPlaying
                ? "[E] Radio OFF | [Z] Next Station"
                : "[E] Radio ON";
        }

        public void OnInteract()
        {
            SetPower(!_isPlaying);
        }

        public void OnSecondaryInteract()
        {
            _stationIndex = (_stationIndex + 1) % StationNames.Length;
            SetPower(true);
        }

        private void SetPower(bool on)
        {
            _isPlaying = on;
            if (_dialLight != null) _dialLight.enabled = _isPlaying;

            if (_isPlaying)
            {
                EnsureStationClips();
                if (_audioSource != null && _cachedStationClips != null && _cachedStationClips.Length > 0)
                {
                    _audioSource.clip = _cachedStationClips[_stationIndex % _cachedStationClips.Length];
                    _audioSource.Play();
                }
                ShowFeedback($"RADIO: {StationNames[_stationIndex]}");
            }
            else
            {
                if (_audioSource != null) _audioSource.Stop();
                ShowFeedback("RADIO OFF");
            }
        }

        private void ShowFeedback(string text)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(transform.position + Vector3.up * 1.4f, 0f, false, text);
            }
        }

        /// <summary>
        /// Synthesizes 3 warm retro lo-fi analog chord loops procedurally so the radio plays real music
        /// immediately with zero missing audio file dependencies.
        /// </summary>
        private static void EnsureStationClips()
        {
            if (_cachedStationClips != null) return;
            _cachedStationClips = new AudioClip[3];
            _cachedStationClips[0] = CreateLoFiLoopClip("Station_LoFi", new[] { 220f, 261.63f, 329.63f, 293.66f }, 0.015f);
            _cachedStationClips[1] = CreateLoFiLoopClip("Station_Acoustic", new[] { 196f, 246.94f, 293.66f, 329.63f }, 0.010f);
            _cachedStationClips[2] = CreateLoFiLoopClip("Station_Jazz", new[] { 174.61f, 220f, 261.63f, 246.94f }, 0.020f);
        }

        private static AudioClip CreateLoFiLoopClip(string clipName, float[] roots, float vinylNoise)
        {
            const int sampleRate = 22050;
            const float chordSeconds = 1.6f;
            int totalSamples = Mathf.RoundToInt(sampleRate * chordSeconds * roots.Length);
            float[] samples = new float[totalSamples];
            System.Random rng = new System.Random(clipName.GetHashCode());

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                int chordIdx = Mathf.Min(roots.Length - 1, Mathf.FloorToInt(t / chordSeconds));
                float localT = t - chordIdx * chordSeconds;
                float env = Mathf.Sin(Mathf.Clamp01(localT / chordSeconds) * Mathf.PI);

                float f1 = roots[chordIdx];
                float f2 = f1 * 1.25f; // major/warm third
                float f3 = f1 * 1.50f; // fifth

                float wave =
                    Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.42f +
                    Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.30f +
                    Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.20f;

                float hiss = (float)(rng.NextDouble() * 2.0 - 1.0) * vinylNoise;
                samples[i] = Mathf.Clamp(wave * env * 0.28f + hiss, -0.8f, 0.8f);
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
