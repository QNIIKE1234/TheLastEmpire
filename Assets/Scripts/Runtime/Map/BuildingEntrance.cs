using UnityEngine;

namespace TheLastEmpire
{
    public enum InteriorTheme
    {
        House = 0,
        CornerShop = 1,
        Clinic = 2,
        Warehouse = 3
    }

    /// <summary>
    /// Interactable doorway placed on selected enterable buildings in a stage.
    /// Integrates seamlessly with PlayerController's IBuildingInteractable / [E] prompt system.
    /// </summary>
    public class BuildingEntrance : MonoBehaviour, IBuildingInteractable
    {
        public int BuildingId { get; private set; }
        public int StageX { get; private set; }
        public int StageY { get; private set; }
        public int InteriorSeed { get; private set; }
        public InteriorTheme Theme { get; private set; }
        public string BuildingDisplayName { get; private set; } = "House";
        public string SourcePrefabName { get; private set; } = "";
        public float BuildingWorldHeight { get; private set; } = 5f;
        public int FloorCount { get; private set; } = 2;
        public Vector3 ExteriorReturnPosition { get; private set; }
        public Vector3 ExteriorReturnForward { get; private set; } = Vector3.back;

        private Light _doorLight;
        private Renderer _matRenderer;
        private Material _matMaterial;
        private Color _baseGlowColor = new Color(1f, 0.78f, 0.35f, 1f);
        private float _pulsePhase;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => new Vector3(0f, 1.85f, 0f);
        public float InteractionDistance => 2.5f;

        public bool CanInteract =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            !BuildingInteriorManager.IsInsideInterior &&
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

        public void Configure(
            int buildingId,
            int stageX,
            int stageY,
            int interiorSeed,
            InteriorTheme theme,
            string displayName,
            Vector3 outwardNormal,
            string sourcePrefabName = "",
            float buildingWorldHeight = 5f,
            int floorCount = 2)
        {
            BuildingId = buildingId;
            StageX = stageX;
            StageY = stageY;
            InteriorSeed = interiorSeed;
            Theme = theme;
            BuildingDisplayName = string.IsNullOrWhiteSpace(displayName) ? theme.ToString() : displayName;
            SourcePrefabName = sourcePrefabName ?? "";
            BuildingWorldHeight = Mathf.Max(2f, buildingWorldHeight);
            FloorCount = Mathf.Clamp(floorCount, 1, 4);

            Vector3 flatNormal = new Vector3(outwardNormal.x, 0f, outwardNormal.z);
            if (flatNormal.sqrMagnitude < 0.001f) flatNormal = Vector3.back;
            flatNormal.Normalize();

            ExteriorReturnForward = flatNormal;
            ExteriorReturnPosition = new Vector3(
                transform.position.x + flatNormal.x * 0.65f,
                0.5f,
                transform.position.z + flatNormal.z * 0.65f);

            transform.rotation = Quaternion.LookRotation(flatNormal, Vector3.up);
            _pulsePhase = (interiorSeed & 0xFF) * 0.05f;

            _baseGlowColor = theme switch
            {
                InteriorTheme.Clinic => new Color(0.35f, 0.92f, 0.85f, 1f),
                InteriorTheme.CornerShop => new Color(0.45f, 0.88f, 1f, 1f),
                InteriorTheme.Warehouse => new Color(1f, 0.62f, 0.25f, 1f),
                _ => new Color(1f, 0.80f, 0.36f, 1f)
            };

            BuildDoorwayMarkerVisual();
        }

        private void BuildDoorwayMarkerVisual()
        {
            // Clean any previous visual children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 1. Glowing threshold doormat on ground
            GameObject matObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            matObj.name = "DoorThresholdMat";
            matObj.transform.SetParent(transform, false);
            matObj.transform.localPosition = new Vector3(0f, 0.04f, 0.15f);
            matObj.transform.localScale = new Vector3(1.45f, 0.06f, 0.95f);
            Collider matCol = matObj.GetComponent<Collider>();
            if (matCol != null) Destroy(matCol);

            _matRenderer = matObj.GetComponent<Renderer>();
            if (_matRenderer != null && shader != null)
            {
                _matMaterial = new Material(shader);
                _matMaterial.color = _baseGlowColor * 0.85f;
                if (_matMaterial.HasProperty("_EmissionColor"))
                {
                    _matMaterial.EnableKeyword("_EMISSION");
                    _matMaterial.SetColor("_EmissionColor", _baseGlowColor * 1.15f);
                }
                _matRenderer.material = _matMaterial;
            }

            // 2. Slim doorway arch posts + lintel framing the enterable door
            Material frameMat = shader != null ? new Material(shader) : null;
            if (frameMat != null)
            {
                frameMat.color = new Color(0.22f, 0.17f, 0.14f, 1f);
                if (frameMat.HasProperty("_EmissionColor"))
                {
                    frameMat.EnableKeyword("_EMISSION");
                    frameMat.SetColor("_EmissionColor", _baseGlowColor * 0.28f);
                }
            }

            CreateVisualPart("LeftPost", new Vector3(-0.74f, 1.05f, -0.25f), new Vector3(0.14f, 2.1f, 0.16f), frameMat);
            CreateVisualPart("RightPost", new Vector3(0.74f, 1.05f, -0.25f), new Vector3(0.14f, 2.1f, 0.16f), frameMat);
            CreateVisualPart("TopLintel", new Vector3(0f, 2.12f, -0.25f), new Vector3(1.66f, 0.18f, 0.22f), frameMat);

            // 3. Dark inner doorway recess backing so it reads clearly as a real door opening
            Material recessMat = shader != null ? new Material(shader) : null;
            if (recessMat != null)
            {
                recessMat.color = new Color(0.05f, 0.04f, 0.04f, 1f);
            }
            CreateVisualPart("DoorRecess", new Vector3(0f, 1.02f, -0.32f), new Vector3(1.32f, 2.02f, 0.08f), recessMat);

            // 4. Warm inviting beacon light above the entrance
            GameObject lightObj = new GameObject("DoorBeaconLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.9f, 0.35f);
            _doorLight = lightObj.AddComponent<Light>();
            _doorLight.type = LightType.Point;
            _doorLight.color = _baseGlowColor;
            _doorLight.range = 4.2f;
            _doorLight.intensity = 1.9f;
            _doorLight.shadows = LightShadows.None;
        }

        private void CreateVisualPart(string partName, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private void Update()
        {
            if (_doorLight == null && _matMaterial == null) return;
            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 2.4f + _pulsePhase);
            if (_doorLight != null)
            {
                _doorLight.intensity = 1.75f * pulse;
            }
            if (_matMaterial != null && _matMaterial.HasProperty("_EmissionColor"))
            {
                _matMaterial.SetColor("_EmissionColor", _baseGlowColor * (0.9f * pulse));
            }
        }

        public string GetInteractionPrompt()
        {
            return $"[E] Enter {BuildingDisplayName}";
        }

        public void OnInteract()
        {
            if (!CanInteract) return;
            BuildingInteriorManager.EnsureExists().EnterBuilding(this);
        }

        public void OnSecondaryInteract()
        {
            // No secondary action
        }

        public static InteriorTheme InferThemeFromPrefabName(string prefabName, int seed)
        {
            if (!string.IsNullOrEmpty(prefabName))
            {
                string lower = prefabName.ToLowerInvariant();
                if (lower.Contains("shop") || lower.Contains("store") || lower.Contains("market") || lower.Contains("cafe"))
                    return InteriorTheme.CornerShop;
                if (lower.Contains("clinic") || lower.Contains("hospital") || lower.Contains("med") || lower.Contains("pharm"))
                    return InteriorTheme.Clinic;
                if (lower.Contains("warehouse") || lower.Contains("hangar") || lower.Contains("factory") || lower.Contains("garage") || lower.Contains("industrial"))
                    return InteriorTheme.Warehouse;
                if (lower.Contains("house") || lower.Contains("cottage") || lower.Contains("chalet") || lower.Contains("townhouse") || lower.Contains("home"))
                    return InteriorTheme.House;
            }

            // General urban buildings ("Apoc_Building_01..07", etc.) deterministically vary across all 4 themes
            int pick = Mathf.Abs(seed) % 10;
            if (pick < 4) return InteriorTheme.House;
            if (pick < 7) return InteriorTheme.CornerShop;
            if (pick < 9) return InteriorTheme.Clinic;
            return InteriorTheme.Warehouse;
        }
    }
}
