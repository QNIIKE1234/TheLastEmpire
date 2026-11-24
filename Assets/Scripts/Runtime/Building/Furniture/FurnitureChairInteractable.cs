using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Interactive Chair / Armchair / Sofa furniture.
    /// Pressing [E] seats the player cleanly on the cushion and gently restores Sanity while seated.
    /// </summary>
    public class FurnitureChairInteractable : MonoBehaviour, IBuildingInteractable
    {
        [SerializeField] private string chairDisplayName = "Chair";
        [SerializeField] private float interactionDistance = 2.3f;
        [SerializeField] private float sanityRegenPerSecond = 2.5f;
        [SerializeField] private Vector3 seatLocalOffset = new Vector3(0f, 0.35f, 0.15f);

        private BuildingObject _buildingObject;
        private PlayerController _seatedPlayer;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.45f;
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
            _seatedPlayer = null;
            BuildingInteractionRegistry.Unregister(this);
        }

        private void Update()
        {
            if (_seatedPlayer == null) return;
            if (!_seatedPlayer.IsSitting || Vector3.Distance(_seatedPlayer.transform.position, transform.position) > 2.0f)
            {
                _seatedPlayer = null;
                return;
            }

            if (sanityRegenPerSecond > 0f)
            {
                _seatedPlayer.RestoreSanity(sanityRegenPerSecond * Time.deltaTime);
            }
        }

        public string GetInteractionPrompt()
        {
            return _seatedPlayer != null && _seatedPlayer.IsSitting
                ? $"[E] Stand Up"
                : $"[E] Sit on {chairDisplayName}";
        }

        public void OnInteract()
        {
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc == null) return;

            if (_seatedPlayer == pc && pc.IsSitting)
            {
                pc.SetSitting(false);
                _seatedPlayer = null;
                return;
            }

            Vector3 seatPos = transform.TransformPoint(seatLocalOffset);
            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            pc.transform.position = new Vector3(seatPos.x, 0.5f, seatPos.z);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f) pc.transform.forward = fwd.normalized;
            pc.SetSitting(true);
            _seatedPlayer = pc;
        }

        public void OnSecondaryInteract() { }
    }
}
