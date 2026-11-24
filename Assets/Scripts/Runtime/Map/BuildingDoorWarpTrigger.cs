using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Attach this script to a doorway BoxCollider (isTrigger = true, recommended depth Z = 1.5 units)
    /// on any building or house prefab.
    /// When the local Player enters the trigger collider and presses [E], they warp into the building's
    /// multi-floor JSON interior.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuildingDoorWarpTrigger : MonoBehaviour, IBuildingInteractable
    {
        [Header("Interior Settings (Optional Overrides)")]
        [Tooltip("Leave empty to auto-select a matching JSON template from Resources/Interiors by prefab name & seed.")]
        [SerializeField] private string overrideTemplateId = "";

        [Tooltip("Optional custom display name shown in UI banner (leave empty to use JSON displayName).")]
        [SerializeField] private string overrideDisplayName = "";

        [Tooltip("If false (default), EnvironmentManager selects 1-2 houses per stage to be enterable. If true, this door is always enterable.")]
        [SerializeField] private bool alwaysEnterable = false;

        [Header("Exit Return & Visuals")]
        [Tooltip("Distance along this trigger's forward (+Z) where the player spawns when exiting the building.")]
        [SerializeField] private float returnOffsetDistance = 1.35f;

        [Tooltip("Shows a subtle glowing threshold beacon when this building is unlocked/enterable in the current stage.")]
        [SerializeField] private bool showBeaconWhenEnterable = true;

        private Collider _triggerCollider;
        private bool _localPlayerInTrigger = false;
        private bool _isUnlockedInStage = false;
        private bool _wasConfiguredByManager = false;

        public int BuildingId { get; private set; } = 1;
        public int StageX { get; private set; } = 64;
        public int StageY { get; private set; } = 64;
        public int InteriorSeed { get; private set; } = 1337;
        public InteriorTheme Theme { get; private set; } = InteriorTheme.House;
        public string BuildingDisplayName { get; private set; } = "Building";
        public string SourcePrefabName { get; private set; } = "";
        public float BuildingWorldHeight { get; private set; } = 5f;
        public int FloorCount { get; private set; } = 2;
        public string OverrideTemplateId => overrideTemplateId;
        public bool AlwaysEnterable => alwaysEnterable;

        public Vector3 ExteriorReturnForward
        {
            get
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            }
        }

        public Vector3 ExteriorReturnPosition
        {
            get
            {
                Vector3 fwd = ExteriorReturnForward;
                Vector3 basePos = transform.position;
                return new Vector3(
                    basePos.x + fwd.x * returnOffsetDistance,
                    0.5f,
                    basePos.z + fwd.z * returnOffsetDistance);
            }
        }

        private GameObject _beaconRoot;
        private Light _beaconLight;
        private Color _beaconColor = new Color(1f, 0.80f, 0.36f, 1f);

        // IBuildingInteractable implementation:
        // PlayerController checks InteractionDistance, so we report a generous radius while gating strictly on _localPlayerInTrigger!
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => new Vector3(0f, 1.85f, 0f);
        public float InteractionDistance => 6.0f;

        public bool CanInteract =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            (_isUnlockedInStage || alwaysEnterable) &&
            _localPlayerInTrigger &&
            !BuildingInteriorManager.IsInsideInterior &&
            !BuildingInteriorManager.IsTransitioning;

        private void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.2f, 2.4f, 1.5f);
            box.center = Vector3.zero;
        }

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
            // If placed manually in a scene with alwaysEnterable=true and never configured by EnvironmentManager
            if (alwaysEnterable && !_wasConfiguredByManager)
            {
                AutoConfigureStandalone();
            }
        }

        private void Start()
        {
            // Fallback for buildings placed manually in a scene where EnvironmentManager did not configure them
            if (!_wasConfiguredByManager)
            {
                AutoConfigureStandalone();
            }
        }

        private void OnDisable()
        {
            _localPlayerInTrigger = false;
            BuildingInteractionRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void AutoConfigureStandalone()
        {
            int sx = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int sy = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            Transform root = transform.parent != null ? transform.parent : transform;
            string pName = root.name.Replace("(Clone)", "").Trim();
            int seed = BuildingInteriorManager.ComputeInteriorSeed(sx * 1000 + sy, sx, sy, 1, pName);
            UnlockForStage(1, sx, sy, seed, pName, 6f);
        }

        /// <summary>
        /// Called by EnvironmentManager when this building is selected as enterable in the current stage.
        /// </summary>
        public void UnlockForStage(
            int buildingId,
            int stageX,
            int stageY,
            int interiorSeed,
            string sourcePrefabName,
            float buildingWorldHeight)
        {
            _wasConfiguredByManager = true;
            _isUnlockedInStage = true;
            BuildingId = buildingId;
            StageX = stageX;
            StageY = stageY;
            InteriorSeed = interiorSeed;
            SourcePrefabName = sourcePrefabName ?? "";
            BuildingWorldHeight = Mathf.Max(2f, buildingWorldHeight);

            InteriorTemplateData tpl = ResolveTemplate();
            Theme = tpl != null
                ? tpl.GetThemeEnum()
                : BuildingEntrance.InferThemeFromPrefabName(SourcePrefabName, interiorSeed);
            FloorCount = tpl != null ? tpl.floorCount : 2;
            BuildingDisplayName = !string.IsNullOrWhiteSpace(overrideDisplayName)
                ? overrideDisplayName
                : (tpl != null && !string.IsNullOrWhiteSpace(tpl.displayName) ? tpl.displayName : Theme.ToString());

            _beaconColor = Theme switch
            {
                InteriorTheme.Clinic => new Color(0.35f, 0.92f, 0.85f, 1f),
                InteriorTheme.CornerShop => new Color(0.45f, 0.88f, 1f, 1f),
                InteriorTheme.Warehouse => new Color(1f, 0.62f, 0.25f, 1f),
                _ => new Color(1f, 0.80f, 0.36f, 1f)
            };

            if (showBeaconWhenEnterable)
            {
                EnsureBeaconVisual(true);
            }
        }

        /// <summary>
        /// Called by EnvironmentManager for non-selected buildings in this stage (unless alwaysEnterable is checked).
        /// </summary>
        public void SetLockedInStage()
        {
            _wasConfiguredByManager = true;
            if (alwaysEnterable) return;

            _isUnlockedInStage = false;
            _localPlayerInTrigger = false;
            EnsureBeaconVisual(false);
        }

        public InteriorTemplateData ResolveTemplate()
        {
            if (!string.IsNullOrWhiteSpace(overrideTemplateId))
            {
                var all = InteriorTemplateLibrary.GetAllTemplates();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (all[i] != null && string.Equals(all[i].templateId, overrideTemplateId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            return all[i];
                        }
                    }
                }
            }
            return InteriorTemplateLibrary.SelectTemplateForBuilding(SourcePrefabName, BuildingWorldHeight, InteriorSeed);
        }

        private void EnsureBeaconVisual(bool visible)
        {
            if (!visible)
            {
                if (_beaconRoot != null) _beaconRoot.SetActive(false);
                return;
            }

            if (_beaconRoot == null)
            {
                _beaconRoot = new GameObject("DoorWarpBeacon");
                _beaconRoot.transform.SetParent(transform, false);
                _beaconRoot.transform.localPosition = Vector3.zero;
                _beaconRoot.transform.localRotation = Quaternion.identity;

                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                // Ground glow mat right inside the 1.5u doorway trigger so players immediately recognize enterable doors
                float yToGround = -transform.localPosition.y + 0.04f;
                GameObject matObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                matObj.name = "DoorThresholdMat";
                matObj.transform.SetParent(_beaconRoot.transform, false);
                matObj.transform.localPosition = new Vector3(0f, yToGround, 0f);
                matObj.transform.localScale = new Vector3(1.55f, 0.05f, 1.15f);
                Collider mc = matObj.GetComponent<Collider>();
                if (mc != null) Destroy(mc);

                Renderer rend = matObj.GetComponent<Renderer>();
                if (rend != null && shader != null)
                {
                    Material m = new Material(shader);
                    m.color = _beaconColor * 0.85f;
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", _beaconColor * 1.15f);
                    }
                    rend.material = m;
                }

                GameObject lightObj = new GameObject("DoorGlowLight");
                lightObj.transform.SetParent(_beaconRoot.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, yToGround + 1.65f, 0.25f);
                _beaconLight = lightObj.AddComponent<Light>();
                _beaconLight.type = LightType.Point;
                _beaconLight.color = _beaconColor;
                _beaconLight.range = 4.0f;
                _beaconLight.intensity = 1.75f;
                _beaconLight.shadows = LightShadows.None;
            }

            _beaconRoot.SetActive(true);
        }

        private static bool IsLocalPlayerCollider(Collider other)
        {
            if (other == null) return false;
            PlayerController pc = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            return pc != null && !pc.IsRemotePlayer;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsLocalPlayerCollider(other))
            {
                _localPlayerInTrigger = true;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsLocalPlayerCollider(other))
            {
                _localPlayerInTrigger = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayerCollider(other))
            {
                _localPlayerInTrigger = false;
            }
        }

        private void Update()
        {
            if (_beaconLight != null && _beaconRoot != null && _beaconRoot.activeSelf)
            {
                _beaconLight.intensity = 1.75f * (0.82f + 0.18f * Mathf.Sin(Time.time * 2.6f));
            }

            // Safety sync against missed exit / spawn-inside-trigger edge cases
            if (!_isUnlockedInStage && !alwaysEnterable)
            {
                _localPlayerInTrigger = false;
                return;
            }

            if (BuildingInteriorManager.IsInsideInterior)
            {
                _localPlayerInTrigger = false;
                return;
            }

            PlayerController localPlayer = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPlayer != null && _triggerCollider != null && _triggerCollider.enabled)
            {
                Vector3 pPos = localPlayer.transform.position;
                Vector3 sample = new Vector3(pPos.x, _triggerCollider.bounds.center.y, pPos.z);
                Vector3 closest = _triggerCollider.ClosestPoint(sample);
                float sqrDist = (sample - closest).sqrMagnitude;
                _localPlayerInTrigger = sqrDist <= 0.25f; // Player is touching / inside the 1.5u trigger collider
            }
        }

        public string GetInteractionPrompt()
        {
            return $"[E] Enter {BuildingDisplayName}";
        }

        public void OnInteract()
        {
            if (!CanInteract) return;
            BuildingInteriorManager.EnsureExists().EnterBuildingFromDoorTrigger(this);
        }

        public void OnSecondaryInteract()
        {
            // No secondary action
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            bool isLockedAtRuntime = Application.isPlaying && !_isUnlockedInStage && !alwaysEnterable;
            if (isLockedAtRuntime)
            {
                // Dim red wireframe only when locked/blocked at runtime so Scene View clearly distinguishes open vs locked doors
                Gizmos.color = new Color(0.95f, 0.25f, 0.25f, 0.45f);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = prevMatrix;
                return;
            }

            Gizmos.color = new Color(0.20f, 0.95f, 0.65f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.20f, 1.0f, 0.65f, 0.95f);
            Gizmos.DrawWireCube(box.center, box.size);

            // Outward exit direction arrow (+Z)
            Gizmos.color = Color.yellow;
            Vector3 start = box.center;
            Vector3 end = start + Vector3.forward * returnOffsetDistance;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.12f);

            Gizmos.matrix = prevMatrix;
        }
    }
}
