using System.Collections.Generic;
using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Interactive Chair / Armchair / Sofa furniture.
    /// Pressing [E] seats the player cleanly on the cushion (ignoring chair collider overlap so PhysX
    /// never launches the player upward!) and gently restores Sanity while seated.
    /// </summary>
    public class FurnitureChairInteractable : MonoBehaviour, IBuildingInteractable
    {
        [SerializeField] private string chairDisplayName = "Chair";
        [SerializeField] private float interactionDistance = 2.3f;
        [SerializeField] private float sanityRegenPerSecond = 2.5f;
        [SerializeField] private Vector3 seatLocalOffset = new Vector3(0f, 0.05f, 0.14f);

        private BuildingObject _buildingObject;
        private PlayerController _seatedPlayer;
        private readonly List<(Collider playerCol, Collider chairCol)> _ignoredCollisionPairs = new List<(Collider, Collider)>();
        private float _restoreCollisionTimer = 0f;

        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.45f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            (_buildingObject == null || !_buildingObject.IsDead) &&
            !(_seatedPlayer != null && _seatedPlayer.IsSitting);

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
            RestoreIgnoredCollisions();
            _seatedPlayer = null;
            BuildingInteractionRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            RestoreIgnoredCollisions();
        }

        private void Update()
        {
            if (_restoreCollisionTimer > 0f)
            {
                _restoreCollisionTimer -= Time.deltaTime;
                if (_restoreCollisionTimer <= 0f)
                {
                    RestoreIgnoredCollisions();
                }
            }

            if (_seatedPlayer == null) return;

            if (!_seatedPlayer.IsSitting || Vector3.Distance(_seatedPlayer.transform.position, transform.position) > 2.2f)
            {
                // Player stood up via movement input -> nudge slightly clear of chair & re-enable collision shortly
                _seatedPlayer = null;
                _restoreCollisionTimer = 0.35f;
                return;
            }

            // Keep seated player firmly planted with zero vertical bounce
            Rigidbody rb = _seatedPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (sanityRegenPerSecond > 0f)
            {
                _seatedPlayer.RestoreSanity(sanityRegenPerSecond * Time.deltaTime);
            }
        }

        public string GetInteractionPrompt()
        {
            return _seatedPlayer != null && _seatedPlayer.IsSitting
                ? "[E] Stand Up"
                : $"[E] Sit on {chairDisplayName}";
        }

        public void OnInteract()
        {
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc == null) return;

            if (_seatedPlayer == pc && pc.IsSitting)
            {
                StandUpCleanly(pc);
                return;
            }

            // 1. Ignore collision between player and this chair FIRST so PhysX depenetration NEVER launches player into the sky!
            IgnoreCollisionsWithPlayer(pc);

            // 2. Position player cleanly on the seat at ground level facing outward (-transform.forward = front of FBX chair/sofa!)
            Vector3 seatPos = transform.TransformPoint(new Vector3(seatLocalOffset.x, 0f, -Mathf.Abs(seatLocalOffset.z)));
            float groundY = Mathf.Clamp(pc.transform.position.y, 0.02f, 0.12f);
            Vector3 targetPos = new Vector3(seatPos.x, groundY, seatPos.z);

            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = targetPos;
            }
            pc.transform.position = targetPos;

            Vector3 fwd = -transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f) pc.transform.forward = fwd.normalized;

            Physics.SyncTransforms();
            pc.SetSitting(true);
            _seatedPlayer = pc;
            _restoreCollisionTimer = 0f;
        }

        private void StandUpCleanly(PlayerController pc)
        {
            pc.SetSitting(false);
            _seatedPlayer = null;

            // Step smoothly onto the front edge (-Z) of the chair before restoring collider contact
            Vector3 standPos = transform.TransformPoint(new Vector3(0f, 0f, -0.95f));
            float groundY = Mathf.Clamp(pc.transform.position.y, 0.02f, 0.12f);
            Vector3 targetPos = new Vector3(standPos.x, groundY, standPos.z);

            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = targetPos;
            }
            pc.transform.position = targetPos;
            Physics.SyncTransforms();

            _restoreCollisionTimer = 0.25f;
        }

        private void IgnoreCollisionsWithPlayer(PlayerController pc)
        {
            RestoreIgnoredCollisions();
            Collider[] pCols = pc.GetComponentsInChildren<Collider>(true);
            Collider[] cCols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < pCols.Length; i++)
            {
                if (pCols[i] == null || pCols[i].isTrigger) continue;
                for (int j = 0; j < cCols.Length; j++)
                {
                    if (cCols[j] == null || cCols[j].isTrigger) continue;
                    Physics.IgnoreCollision(pCols[i], cCols[j], true);
                    _ignoredCollisionPairs.Add((pCols[i], cCols[j]));
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            for (int i = 0; i < _ignoredCollisionPairs.Count; i++)
            {
                var pair = _ignoredCollisionPairs[i];
                if (pair.playerCol != null && pair.chairCol != null)
                {
                    Physics.IgnoreCollision(pair.playerCol, pair.chairCol, false);
                }
            }
            _ignoredCollisionPairs.Clear();
        }

        public void OnSecondaryInteract() { }
    }
}
