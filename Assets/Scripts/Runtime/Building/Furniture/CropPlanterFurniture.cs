using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Square Planter Box that grows edible crops (Fresh Lime, Potato, Ripe Tomato, Sweet Carrot, Strawberry).
    /// Visually grows from Seedling -> Growing -> Ripe Fruit, and pressing [E] harvests 1-3 crops into PlayerInventory.
    /// </summary>
    public class CropPlanterFurniture : MonoBehaviour, IBuildingInteractable
    {
        [SerializeField] private string cropItemName = "Fresh Lime";
        [SerializeField] private string planterDisplayName = "Lime Planter";
        [SerializeField] private int minYield = 1;
        [SerializeField] private int maxYield = 3;
        [SerializeField] private float growthDurationSeconds = 55f;
        [SerializeField] private bool startReadyToHarvest = true;
        [SerializeField] private float interactionDistance = 2.4f;

        private BuildingObject _buildingObject;
        private Transform _foliageRoot;
        private Transform _fruitRoot;
        private float _growthProgress = 1f; // 0.0 -> 1.0 (ready)

        public bool IsReadyToHarvest => _growthProgress >= 0.999f;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.45f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => isActiveAndEnabled && gameObject.activeInHierarchy && (_buildingObject == null || !_buildingObject.IsDead);

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            _growthProgress = startReadyToHarvest ? 1f : 0.15f;
        }

        private void Start()
        {
            ResolveVisualParts();
            UpdatePlantVisuals();
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void ResolveVisualParts()
        {
            FurnitureVisualBuilder builder = GetComponent<FurnitureVisualBuilder>();
            if (builder != null) builder.BuildIfNeeded();

            Transform vis = transform.Find("VisualRoot");
            if (vis != null)
            {
                _foliageRoot = FindChildRecursive(vis, "CropFoliageRoot");
                _fruitRoot = FindChildRecursive(vis, "CropFruitRoot");
            }
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            Transform direct = root.Find(targetName);
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void Update()
        {
            if (_growthProgress < 1f)
            {
                float rate = 1f / Mathf.Max(5f, growthDurationSeconds);
                _growthProgress = Mathf.Min(1f, _growthProgress + Time.deltaTime * rate);
                UpdatePlantVisuals();
            }
        }

        private void UpdatePlantVisuals()
        {
            if (_foliageRoot == null) ResolveVisualParts();

            if (_foliageRoot != null)
            {
                float s = Mathf.Lerp(0.28f, 1.0f, _growthProgress);
                _foliageRoot.localScale = new Vector3(s, s, s);
            }

            if (_fruitRoot != null)
            {
                bool showFruit = _growthProgress >= 0.95f;
                if (_fruitRoot.gameObject.activeSelf != showFruit)
                {
                    _fruitRoot.gameObject.SetActive(showFruit);
                }
            }
        }

        public string GetInteractionPrompt()
        {
            if (IsReadyToHarvest)
            {
                return $"[E] Harvest {cropItemName}";
            }
            int pct = Mathf.RoundToInt(_growthProgress * 100f);
            return $"[E] {planterDisplayName} ({pct}%)";
        }

        public void OnInteract()
        {
            if (!IsReadyToHarvest)
            {
                int pct = Mathf.RoundToInt(_growthProgress * 100f);
                SpawnToast($"GROWING... {pct}%");
                return;
            }

            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            PlayerInventory inv = pc != null ? pc.GetComponent<PlayerInventory>() : PlayerInventory.Instance;
            if (inv == null) return;

            int qty = Random.Range(Mathf.Max(1, minYield), Mathf.Max(minYield, maxYield) + 1);
            inv.AddItem(cropItemName, qty);

            _growthProgress = 0.05f;
            UpdatePlantVisuals();
            NetworkBuildingSyncManager.NotifyLocalFurnitureStateChanged(
                _buildingObject,
                FurnitureStateSyncType.PlanterHarvest,
                0,
                _growthProgress);
            SpawnToast($"+{qty} {cropItemName.ToUpperInvariant()}");
        }

        public void ApplyNetworkGrowthProgress(float progress)
        {
            _growthProgress = Mathf.Clamp01(progress);
            UpdatePlantVisuals();
        }

        public void OnSecondaryInteract() { }

        private void SpawnToast(string msg)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(transform.position + Vector3.up * 1.35f, 0f, false, msg);
            }
        }
    }
}
