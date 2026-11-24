using System.Collections.Generic;
using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// BedrollBuilding — เตียงนอน / ที่นอนผู้รอดชีวิต (BUILD0013_SurvivorSleepingBedroll & BUILD0030_WoodenSingleBed)
    /// คุณสมบัติ:
    /// - กด [E] เพื่อขึ้นไปนอนบนเตียง (ท่า Sleep) + ตั้งจุดเกิดใหม่ + ฟื้นฟูเลือด +1 HP ต่อวินาที (กด WASD เพื่อลุก)
    /// - กด [Z] เพื่อนอนข้ามคืนตอนกลางคืน (ฟื้นฟู HP + Sanity และซิงค์เวลาเช้าใน Co-op)
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BedrollBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Bedroll Settings")]
        [SerializeField] private float hpRestoredOnSleep = 50f;
        [SerializeField] private float sanityRestoredOnSleep = 50f;
        [SerializeField] private float interactionDistance = 2.5f;

        [Header("Lie-Down Rest Regeneration")]
        [SerializeField] private float hpRegenPerSecond = 1.0f;
        [SerializeField] private float sanityRegenPerSecond = 1.5f;

        private BuildingObject _buildingObject;
        private PlayerController _playerController;
        private PlayerController _sleepingPlayer;
        private readonly List<(Collider playerCol, Collider bedCol)> _ignoredCollisionPairs = new List<(Collider, Collider)>();
        private bool _savedUseGravity = true;
        private float _savedStandingY = 0.7f;
        private float _restoreCollisionTimer = 0f;
        private float _hpRegenAccum = 0f;

        public static Vector3? CurrentRespawnPoint { get; private set; }
        public static BedrollBuilding ActiveBedroll { get; private set; }

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.15f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            (_buildingObject == null || !_buildingObject.IsDead) &&
            !(_sleepingPlayer != null && _sleepingPlayer.IsSleeping);

        public string GetInteractionPrompt()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            return isNight
                ? "<color=yellow><b>[E]</b></color> Lie Down (+1 HP/s)  |  <color=#00e5ff><b>[Z]</b></color> Sleep Till Dawn"
                : "<color=yellow><b>[E]</b></color> Lie Down (+1 HP/s)";
        }

        public void OnInteract()
        {
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc == null) return;

            if (_sleepingPlayer == pc && pc.IsSleeping)
            {
                WakeUpCleanly(pc);
                return;
            }

            SetRespawnPoint(silent: true);
            LieDownOnBed(pc);
        }

        public void OnSecondaryInteract()
        {
            TrySleep();
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (_sleepingPlayer != null)
            {
                RestoreGravityForPlayer(_sleepingPlayer);
                if (_sleepingPlayer.IsSleeping) _sleepingPlayer.SetSleeping(false);
                _sleepingPlayer = null;
            }
            RestoreIgnoredCollisions();
            BuildingInteractionRegistry.Unregister(this);
            if (ActiveBedroll == this)
            {
                ActiveBedroll = null;
            }
        }

        private void OnDestroy()
        {
            if (_sleepingPlayer != null)
            {
                RestoreGravityForPlayer(_sleepingPlayer);
                _sleepingPlayer = null;
            }
            RestoreIgnoredCollisions();
        }

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
        }

        private void Start()
        {
            FindPlayer();
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

            if (_sleepingPlayer == null) return;

            // If player woke up via movement/attack/dash or moved away, step cleanly off the bed onto the floor
            if (!_sleepingPlayer.IsSleeping || Vector3.Distance(_sleepingPlayer.transform.position, transform.position) > 2.6f)
            {
                PlayerController woken = _sleepingPlayer;
                _sleepingPlayer = null;
                _hpRegenAccum = 0f;
                StepPlayerOffBed(woken);
                return;
            }

            // Keep player peacefully planted on the mattress with zero physics jitter
            Rigidbody rb = _sleepingPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Regenerate +1 HP per second while resting on the bed
            if (hpRegenPerSecond > 0f && _sleepingPlayer.PlayerHealth != null && !_sleepingPlayer.PlayerHealth.IsDead)
            {
                if (_sleepingPlayer.PlayerHealth.CurrentHealth < _sleepingPlayer.PlayerHealth.MaxHealth)
                {
                    _hpRegenAccum += hpRegenPerSecond * Time.deltaTime;
                    if (_hpRegenAccum >= 1f)
                    {
                        int wholeHp = Mathf.FloorToInt(_hpRegenAccum);
                        _hpRegenAccum -= wholeHp;
                        _sleepingPlayer.PlayerHealth.Heal(wholeHp);
                    }
                }
                else
                {
                    _hpRegenAccum = 0f;
                }
            }

            if (sanityRegenPerSecond > 0f)
            {
                _sleepingPlayer.RestoreSanity(sanityRegenPerSecond * Time.deltaTime);
            }
        }

        private void FindPlayer()
        {
            PlayerController pc = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (pc == null)
            {
                pc = Object.FindFirstObjectByType<PlayerController>();
            }
            if (pc != null && !pc.IsRemotePlayer)
            {
                _playerController = pc;
            }
        }

        private bool IsRaisedWoodenBed()
        {
            string n = gameObject.name;
            return n.IndexOf("SingleBed", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("BUILD0030", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetComponent<FurnitureVisualBuilder>() != null;
        }

        private void LieDownOnBed(PlayerController pc)
        {
            // 1. Ignore collision with bed colliders FIRST so PhysX never launches player upward
            IgnoreCollisionsWithPlayer(pc);

            // 2. Capture player's real standing root Y (PlayerPrefabs root sits ~0.70m above floor because CapsuleCollider bottom is -0.70m!)
            _savedStandingY = pc.transform.position.y;

            // 3. Place player cleanly ON TOP of the mattress/duvet with head resting on the pillow (+Z of bed), feet toward footboard (-Z)
            bool raisedBed = IsRaisedWoodenBed();
            float liftAboveFloor = raisedBed ? 0.64f : 0.08f;
            float zOffset = raisedBed ? 0.14f : 0.0f;
            Vector3 bedXZ = transform.TransformPoint(new Vector3(0f, 0f, zOffset));
            Vector3 liePos = new Vector3(bedXZ.x, _savedStandingY + liftAboveFloor, bedXZ.z);

            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _savedUseGravity = rb.useGravity;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = liePos;
            }
            pc.transform.position = liePos;

            // Sleep animation lays character back with HEAD toward -player.forward and FEET toward +player.forward.
            // Baked bed FBX has pillow at +bed.forward and footboard at -bed.forward -> face player toward -bed.forward!
            Vector3 feetDir = -transform.forward;
            feetDir.y = 0f;
            if (feetDir.sqrMagnitude > 0.01f) pc.transform.forward = feetDir.normalized;

            Physics.SyncTransforms();
            pc.SetSleeping(true);
            _sleepingPlayer = pc;
            _hpRegenAccum = 0f;
            _restoreCollisionTimer = 0f;

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 1.45f,
                    0f,
                    false,
                    "RESTING (+1 HP/s)"
                );
            }
        }

        private void WakeUpCleanly(PlayerController pc)
        {
            _sleepingPlayer = null;
            _hpRegenAccum = 0f;
            pc.SetSleeping(false);
            StepPlayerOffBed(pc);
        }

        private void StepPlayerOffBed(PlayerController pc)
        {
            if (pc == null) return;
            RestoreGravityForPlayer(pc);

            // Step smoothly beside the bed at the player's original floor standing height
            Vector3 sideOffset = IsRaisedWoodenBed() ? new Vector3(-1.15f, 0f, 0f) : new Vector3(0f, 0f, -0.85f);
            Vector3 standPos = transform.TransformPoint(sideOffset);
            standPos.y = _savedStandingY;

            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = standPos;
            }
            pc.transform.position = standPos;
            Physics.SyncTransforms();

            _restoreCollisionTimer = 0.35f;
        }

        private void RestoreGravityForPlayer(PlayerController pc)
        {
            if (pc == null) return;
            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = _savedUseGravity;
            }
        }

        private void IgnoreCollisionsWithPlayer(PlayerController pc)
        {
            RestoreIgnoredCollisions();
            Collider[] pCols = pc.GetComponentsInChildren<Collider>(true);
            Collider[] bCols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < pCols.Length; i++)
            {
                if (pCols[i] == null || pCols[i].isTrigger) continue;
                for (int j = 0; j < bCols.Length; j++)
                {
                    if (bCols[j] == null || bCols[j].isTrigger) continue;
                    Physics.IgnoreCollision(pCols[i], bCols[j], true);
                    _ignoredCollisionPairs.Add((pCols[i], bCols[j]));
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            for (int i = 0; i < _ignoredCollisionPairs.Count; i++)
            {
                var pair = _ignoredCollisionPairs[i];
                if (pair.playerCol != null && pair.bedCol != null)
                {
                    Physics.IgnoreCollision(pair.playerCol, pair.bedCol, false);
                }
            }
            _ignoredCollisionPairs.Clear();
        }

        public void SetRespawnPoint(bool silent = false)
        {
            CurrentRespawnPoint = transform.position + Vector3.up * 0.5f;
            ActiveBedroll = this;

            if (!silent && DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 1.2f,
                    0f,
                    false,
                    "RESPAWN POINT SET"
                );
            }

            Debug.Log($"[BedrollBuilding] Respawn point updated to: {CurrentRespawnPoint.Value}");
        }

        public void TrySleep()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;

            if (!isNight)
            {
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.2f,
                        0f,
                        false,
                        "Can only skip night at night"
                    );
                }
                return;
            }

            if (DayNightManager.Instance != null)
            {
                if (EmpireNetworkManager.IsOnlineClient)
                {
                    NetworkBuildingSyncManager.NotifyLocalFurnitureStateChanged(
                        _buildingObject,
                        FurnitureStateSyncType.BedSleepToDawn);
                }
                else
                {
                    DayNightManager.Instance.AddDays(1);
                    DayNightManager.Instance.SetPhase(DayNightManager.TimePhase.Day, true);
                }
            }

            if (_playerController == null)
            {
                FindPlayer();
            }

            if (_playerController != null)
            {
                if (_playerController.PlayerHealth != null)
                {
                    _playerController.PlayerHealth.Heal(hpRestoredOnSleep);
                }
                _playerController.RestoreSanity(sanityRestoredOnSleep);
            }

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 1.5f,
                    0f,
                    false,
                    $"+{hpRestoredOnSleep} HP & +{sanityRestoredOnSleep} Sanity"
                );
            }

            Debug.Log("[BedrollBuilding] Player slept through the night and woke up refreshed at dawn.");
        }
    }
}
