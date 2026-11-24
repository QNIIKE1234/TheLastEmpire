using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Exit doorway trigger placed at the front (-Z) threshold of an active building interior.
    /// Pressing [E] returns the player outside to the building's front entrance.
    /// </summary>
    public class BuildingExitPortal : MonoBehaviour, IBuildingInteractable
    {
        private Light _exitLight;
        private Material _matMaterial;
        private readonly Color _exitGlow = new Color(0.35f, 1f, 0.58f, 1f);

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => new Vector3(0f, 1.35f, 0f);
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
            if (_matMaterial != null)
            {
                Destroy(_matMaterial);
                _matMaterial = null;
            }
        }

        public void BuildExitVisual(float doorWidth = 2.0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject matObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            matObj.name = "ExitDoormat";
            matObj.transform.SetParent(transform, false);
            matObj.transform.localPosition = new Vector3(0f, 0.03f, 0.15f);
            matObj.transform.localScale = new Vector3(doorWidth * 0.85f, 0.05f, 0.85f);
            Collider col = matObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = matObj.GetComponent<Renderer>();
            if (rend != null && shader != null)
            {
                _matMaterial = new Material(shader);
                _matMaterial.color = new Color(0.24f, 0.65f, 0.42f, 1f);
                if (_matMaterial.HasProperty("_EmissionColor"))
                {
                    _matMaterial.EnableKeyword("_EMISSION");
                    _matMaterial.SetColor("_EmissionColor", _exitGlow * 0.9f);
                }
                rend.material = _matMaterial;
            }

            // Threshold bar
            GameObject barObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barObj.name = "ExitThresholdStrip";
            barObj.transform.SetParent(transform, false);
            barObj.transform.localPosition = new Vector3(0f, 0.045f, -0.32f);
            barObj.transform.localScale = new Vector3(doorWidth, 0.07f, 0.18f);
            Collider barCol = barObj.GetComponent<Collider>();
            if (barCol != null) Destroy(barCol);

            Renderer barRend = barObj.GetComponent<Renderer>();
            if (barRend != null && shader != null)
            {
                Material barMat = new Material(shader);
                barMat.color = new Color(0.18f, 0.14f, 0.12f, 1f);
                barRend.material = barMat;
            }

            GameObject lightObj = new GameObject("ExitGlowLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.0f, 0.1f);
            _exitLight = lightObj.AddComponent<Light>();
            _exitLight.type = LightType.Point;
            _exitLight.color = _exitGlow;
            _exitLight.range = 3.5f;
            _exitLight.intensity = 1.4f;
            _exitLight.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (_exitLight == null) return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            _exitLight.intensity = 1.35f * pulse;
        }

        public string GetInteractionPrompt()
        {
            return "[E] Exit Building";
        }

        public void OnInteract()
        {
            if (!CanInteract) return;
            BuildingInteriorManager.Instance?.ExitCurrentBuilding();
        }

        public void OnSecondaryInteract()
        {
            // No secondary action
        }
    }
}
