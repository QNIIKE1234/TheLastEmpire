using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    public enum FurnitureUtilityMode
    {
        CookingPotPlaceholder = 0,
        FirstAidHealStation = 1
    }

    /// <summary>
    /// Utility furniture interactions:
    /// - CookingPotPlaceholder: shows "[E] Cooking Pot" and informs player cooking is coming soon.
    /// - FirstAidHealStation: restores +25 HP on a 45s cooldown.
    /// </summary>
    public class FurnitureUtilityInteractable : MonoBehaviour, IBuildingInteractable
    {
        [SerializeField] private FurnitureUtilityMode utilityMode = FurnitureUtilityMode.CookingPotPlaceholder;
        [SerializeField] private string displayName = "Cooking Pot";
        [SerializeField] private float healAmount = 25f;
        [SerializeField] private float cooldownSeconds = 45f;
        [SerializeField] private float interactionDistance = 2.4f;

        private BuildingObject _buildingObject;
        private float _cooldownTimer = 0f;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.55f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => isActiveAndEnabled && gameObject.activeInHierarchy && (_buildingObject == null || !_buildingObject.IsDead);

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        public string GetInteractionPrompt()
        {
            if (utilityMode == FurnitureUtilityMode.CookingPotPlaceholder)
            {
                return $"[E] {displayName}";
            }

            if (_cooldownTimer > 0.5f)
            {
                return $"[E] {displayName} ({Mathf.CeilToInt(_cooldownTimer)}s)";
            }
            return $"[E] Use {displayName} (+{Mathf.RoundToInt(healAmount)} HP)";
        }

        public void OnInteract()
        {
            if (utilityMode == FurnitureUtilityMode.CookingPotPlaceholder)
            {
                SpawnToast("COOKING SYSTEM COMING SOON");
                return;
            }

            if (_cooldownTimer > 0f)
            {
                SpawnToast($"RESTOCKING ({Mathf.CeilToInt(_cooldownTimer)}s)");
                return;
            }

            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc != null && pc.PlayerHealth != null && !pc.PlayerHealth.IsDead)
            {
                pc.PlayerHealth.Heal(healAmount);
                _cooldownTimer = cooldownSeconds;
                SpawnToast($"+{Mathf.RoundToInt(healAmount)} HP");
            }
        }

        public void OnSecondaryInteract() { }

        private void SpawnToast(string msg)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(transform.position + Vector3.up * 1.4f, 0f, false, msg);
            }
        }
    }
}
