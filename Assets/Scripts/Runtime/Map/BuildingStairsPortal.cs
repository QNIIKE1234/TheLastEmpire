using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Interactive staircase inside multi-floor building interiors (Floors 1..4).
    /// Pressing [E] smoothly transitions the player to the floor above or below.
    /// </summary>
    public class BuildingStairsPortal : MonoBehaviour, IBuildingInteractable
    {
        public bool IsGoingUp { get; private set; } = true;
        public int TargetFloorIndex { get; private set; } = 2;

        private Light _stairLight;
        private Material _stepAccentMat;
        private Color _glowColor = new Color(0.35f, 0.78f, 1f, 1f);

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => new Vector3(0f, 1.75f, 0f);
        public float InteractionDistance => 2.5f;

        public bool CanInteract =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            BuildingInteriorManager.IsInsideInterior &&
            !BuildingInteriorManager.IsTransitioning;

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            BuildingInteractionRegistry.Unregister(this);
            if (_stepAccentMat != null)
            {
                Destroy(_stepAccentMat);
                _stepAccentMat = null;
            }
        }

        public void Configure(bool goingUp, int targetFloorIndex)
        {
            IsGoingUp = goingUp;
            TargetFloorIndex = targetFloorIndex;
            _glowColor = goingUp
                ? new Color(0.35f, 0.82f, 1.0f, 1f)   // Cool cyan-blue for Stairs Up
                : new Color(1.0f, 0.74f, 0.30f, 1f);  // Warm amber for Stairs Down

            BuildStaircaseVisual();
        }

        private void BuildStaircaseVisual()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material baseMat = shader != null ? new Material(shader) : null;
            if (baseMat != null)
            {
                baseMat.color = new Color(0.28f, 0.24f, 0.20f, 1f);
            }

            _stepAccentMat = shader != null ? new Material(shader) : null;
            if (_stepAccentMat != null)
            {
                _stepAccentMat.color = _glowColor * 0.75f;
                if (_stepAccentMat.HasProperty("_EmissionColor"))
                {
                    _stepAccentMat.EnableKeyword("_EMISSION");
                    _stepAccentMat.SetColor("_EmissionColor", _glowColor * 0.9f);
                }
            }

            // 5 stepped treads ascending (going up) or descending landing platform (going down)
            int stepCount = 5;
            float stairWidth = 1.65f;
            float stepDepth = 0.38f;
            for (int i = 0; i < stepCount; i++)
            {
                float h = IsGoingUp ? (0.18f + i * 0.22f) : (0.16f + (stepCount - 1 - i) * 0.14f);
                float z = -0.75f + i * stepDepth;

                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{i}";
                step.transform.SetParent(transform, false);
                step.transform.localPosition = new Vector3(0f, h * 0.5f, z);
                step.transform.localScale = new Vector3(stairWidth, h, stepDepth * 0.96f);
                Collider c = step.GetComponent<Collider>();
                if (c != null) Destroy(c);
                Renderer r = step.GetComponent<Renderer>();
                if (r != null && baseMat != null) r.sharedMaterial = baseMat;

                // Glowing nose strip on every step edge so stairs are unmistakable in top-down view
                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"StepGlow_{i}";
                strip.transform.SetParent(transform, false);
                strip.transform.localPosition = new Vector3(0f, h + 0.015f, z - stepDepth * 0.36f);
                strip.transform.localScale = new Vector3(stairWidth * 0.92f, 0.03f, 0.08f);
                Collider sc = strip.GetComponent<Collider>();
                if (sc != null) Destroy(sc);
                Renderer sr = strip.GetComponent<Renderer>();
                if (sr != null && _stepAccentMat != null) sr.sharedMaterial = _stepAccentMat;
            }

            // Side rails
            CreateRail("RailLeft", new Vector3(-stairWidth * 0.52f, 0.72f, 0f), new Vector3(0.10f, 1.35f, stepCount * stepDepth), baseMat);
            CreateRail("RailRight", new Vector3(stairWidth * 0.52f, 0.72f, 0f), new Vector3(0.10f, 1.35f, stepCount * stepDepth), baseMat);

            // Soft indicator point light
            GameObject lightObj = new GameObject("StairsBeaconLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            _stairLight = lightObj.AddComponent<Light>();
            _stairLight.type = LightType.Point;
            _stairLight.color = _glowColor;
            _stairLight.range = 4.0f;
            _stairLight.intensity = 1.65f;
            _stairLight.shadows = LightShadows.None;
        }

        private void CreateRail(string partName, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private void Update()
        {
            if (_stairLight == null) return;
            float pulse = 0.84f + 0.16f * Mathf.Sin(Time.time * 2.8f);
            _stairLight.intensity = 1.65f * pulse;
        }

        public string GetInteractionPrompt()
        {
            return IsGoingUp ? $"[E] Go Up to Floor {TargetFloorIndex}" : $"[E] Go Down to Floor {TargetFloorIndex}";
        }

        public void OnInteract()
        {
            if (!CanInteract) return;
            BuildingInteriorManager.Instance?.TransitionToFloor(TargetFloorIndex, arrivedFromBelow: IsGoingUp);
        }

        public void OnSecondaryInteract()
        {
            // No secondary action
        }
    }
}
