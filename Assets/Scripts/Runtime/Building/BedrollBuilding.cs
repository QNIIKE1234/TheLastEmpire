using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// BedrollBuilding — ที่นอนถุงนอนผู้รอดชีวิต (BUILD0013_SurvivorSleepingBedroll)
    /// คุณสมบัติ:
    /// - กด [E] เพื่อบันทึกจุดเกิดใหม่ (Set Respawn Point)
    /// - กด [Z] เพื่อเข้านอนหลับพักผ่อนตอนกลางคืน: ข้ามคืนไปยังรุ่งเช้า (Day), ฟื้นฟู +50 HP และ +50 Sanity
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BedrollBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Bedroll Settings")]
        [SerializeField] private float hpRestoredOnSleep = 50f;
        [SerializeField] private float sanityRestoredOnSleep = 50f;
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private PlayerController _playerController;

        public static Vector3? CurrentRespawnPoint { get; private set; }
        public static BedrollBuilding ActiveBedroll { get; private set; }

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.0f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
            return isNight
                ? "<color=yellow><b>[E]</b></color> Set Spawn  |  <color=#00e5ff><b>[Z]</b></color> Sleep"
                : "<color=yellow><b>[E]</b></color> Set Spawn Point";
        }

        public void OnInteract()
        {
            SetRespawnPoint();
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
            BuildingInteractionRegistry.Unregister(this);
            if (ActiveBedroll == this)
            {
                ActiveBedroll = null;
            }
        }

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerController = pc;
            }
        }

        public void SetRespawnPoint()
        {
            CurrentRespawnPoint = transform.position + Vector3.up * 0.5f;
            ActiveBedroll = this;

            if (DamageNumberManager.Instance != null)
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
                        "นอนได้เฉพาะตอนกลางคืน"
                    );
                }
                return;
            }

            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.AddDays(1);
                DayNightManager.Instance.SetPhase(DayNightManager.TimePhase.Day, true);
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
