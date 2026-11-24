using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Toggleable illumination furniture (Desk Lamp, Standing Floor Lamp, Wood Stove, Retro CRT TV).
    /// Press [E] to switch ON / OFF.
    /// </summary>
    public class ToggleableLampFurniture : MonoBehaviour, IBuildingInteractable
    {
        [SerializeField] private string lampDisplayName = "Lamp";
        [SerializeField] private float interactionDistance = 2.4f;
        [SerializeField] private bool startTurnedOn = true;
        [SerializeField] private float lightRange = 8.5f;
        [SerializeField] private float lightIntensity = 2.6f;
        [SerializeField] private Color lightColor = new Color(1.0f, 0.82f, 0.48f);
        [SerializeField] private Vector3 lightLocalOffset = new Vector3(0f, 1.35f, 0f);
        [SerializeField] private bool gentleFlicker = false;

        private BuildingObject _buildingObject;
        private Light _pointLight;
        private bool _isOn = true;
        private float _flickerSeed;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.65f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => isActiveAndEnabled && gameObject.activeInHierarchy && (_buildingObject == null || !_buildingObject.IsDead);

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            _flickerSeed = Random.Range(0f, 100f);
            EnsureLight();
            ApplyLightState(startTurnedOn);
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void EnsureLight()
        {
            _pointLight = GetComponentInChildren<Light>();
            if (_pointLight == null)
            {
                GameObject lObj = new GameObject("FurnitureLampLight");
                lObj.transform.SetParent(transform, false);
                lObj.transform.localPosition = lightLocalOffset;
                _pointLight = lObj.AddComponent<Light>();
                _pointLight.type = LightType.Point;
                _pointLight.shadows = LightShadows.None;
            }
            _pointLight.color = lightColor;
            _pointLight.range = lightRange;
            _pointLight.intensity = lightIntensity;
        }

        private void ApplyLightState(bool on)
        {
            _isOn = on;
            if (_pointLight != null)
            {
                _pointLight.enabled = _isOn;
            }
        }

        private void Update()
        {
            if (!_isOn || !gentleFlicker || _pointLight == null) return;
            float n = Mathf.PerlinNoise(_flickerSeed, Time.time * 4.5f);
            _pointLight.intensity = lightIntensity * (0.85f + 0.30f * n);
        }

        public string GetInteractionPrompt()
        {
            return _isOn ? $"[E] Turn Off {lampDisplayName}" : $"[E] Turn On {lampDisplayName}";
        }

        public void OnInteract()
        {
            ApplyLightState(!_isOn);
        }

        public void OnSecondaryInteract() { }
    }
}
