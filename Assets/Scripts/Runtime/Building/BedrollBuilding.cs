using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// BedrollBuilding — ที่นอนถุงนอนผู้รอดชีวิต (BUILD0013_SurvivorSleepingBedroll)
    /// คุณสมบัติ:
    /// - กด [E] เพื่อบันทึกจุดเกิดใหม่ (Set Respawn Point)
    /// - กด [Z] หรือนอนหลับตอนกลางคืน: ข้ามคืนไปยังรุ่งเช้า (Day), ฟื้นฟู +50 HP และ +50 Sanity
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BedrollBuilding : MonoBehaviour
    {
        [Header("Bedroll Settings")]
        [SerializeField] private float hpRestoredOnSleep = 50f;
        [SerializeField] private float sanityRestoredOnSleep = 50f;
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private PlayerController _playerController;
        private bool _isPlayerNearby = false;

        public static Vector3? CurrentRespawnPoint { get; private set; }
        public static BedrollBuilding ActiveBedroll { get; private set; }

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
                _playerTransform = pc.transform;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            if (_isPlayerNearby && Keyboard.current != null)
            {
                // กด E: ตั้งจุดเกิดใหม่
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    SetRespawnPoint();
                }

                // กด Z: นอนหลับพักผ่อน (เฉพาะตอนกลางคืน)
                if (Keyboard.current.zKey.wasPressedThisFrame)
                {
                    TrySleep();
                }
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

            // ข้ามเวลาไปยังเช้าวันถัดไป
            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.AddDays(1);
                DayNightManager.Instance.SetPhase(DayNightManager.TimePhase.Day, true);
            }

            // ฟื้นฟูเลือดและสติ
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

        private void OnDestroy()
        {
            if (ActiveBedroll == this)
            {
                ActiveBedroll = null;
            }
        }

        private void OnGUI()
        {
            if (_isPlayerNearby)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
                if (screenPos.z > 0)
                {
                    float width = 270f;
                    float height = 50f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
                    GUI.color = new Color(0.95f, 0.85f, 0.5f, 0.95f);

                    string prompt = isNight 
                        ? "[E] จุดเกิดใหม่  |  [Z] เข้านอนข้ามคืน"
                        : "[E] บันทึกจุดเกิดใหม่ (Spawn Point)";
                    GUI.Box(rect, prompt);
                }
            }
        }
    }
}

