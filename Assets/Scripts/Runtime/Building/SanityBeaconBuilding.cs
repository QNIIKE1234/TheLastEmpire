using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// SanityBeaconBuilding — ตะเกียงประภาคารฟื้นฟูสติ (BUILD0014_SanityBeaconLantern)
    /// คุณสมบัติ:
    /// - ปล่อยแสงสีฟ้าอบอุ่นขับไล่ความมืดมิดในยามค่ำคืน
    /// - ฟื้นฟูสติ (Sanity) +2.5 หน่วย/วินาที แก่ผู้เล่นที่อยู่ในรัศมี 10 เมตร
    /// - ช่วยเปิดการมองเห็นตอนกลางคืนในรัศมีกว้างขึ้น
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class SanityBeaconBuilding : MonoBehaviour, IPowerConsumer
    {
        public static readonly List<SanityBeaconBuilding> ActiveBeacons = new List<SanityBeaconBuilding>();

        [Header("Sanity Beacon Settings")]
        [Tooltip("อัตราการฟื้นฟูสติของผู้เล่นต่อวินาที")]
        [SerializeField] private float sanityRegenRate = 2.5f;

        [Tooltip("ปริมาณการใช้พลังงานไฟฟ้า (วัตต์)")]
        [SerializeField] private float powerDemand = 15.0f;

        [Tooltip("รัศมีของแสงและออร่าฟื้นฟูสติ (เมตร)")]
        [SerializeField] private float beaconRadius = 10.0f;

        [Header("Light Pulse Settings")]
        [SerializeField] private Light beaconLight;
        [SerializeField] private float minIntensity = 2.0f;
        [SerializeField] private float maxIntensity = 3.2f;
        [SerializeField] private float pulseSpeed = 1.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private PlayerController _playerController;
        private bool _isPlayerInsideAura = false;
        private float _appliedRegen = 0f;

        public float BeaconRadius => beaconRadius;

        // IPowerConsumer implementation
        public Vector3 Position => transform.position;
        public float PowerDemand => powerDemand;
        public bool IsConsumerActive => _buildingObject == null || !_buildingObject.IsDead;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (beaconLight == null)
            {
                beaconLight = GetComponentInChildren<Light>();
            }
        }

        private void OnEnable()
        {
            if (!ActiveBeacons.Contains(this))
            {
                ActiveBeacons.Add(this);
            }
            PowerGridManager.RegisterConsumer(this);
        }

        private void OnDisable()
        {
            ActiveBeacons.Remove(this);
            PowerGridManager.UnregisterConsumer(this);
            RemovePlayerAura();
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
            float powerEff = PowerGridManager.GetPowerEfficiency(transform.position, powerDemand);

            // แสงไฟกะพริบตามอัตราพลังงาน
            if (beaconLight != null)
            {
                if (powerEff <= 0f)
                {
                    beaconLight.intensity = 0f;
                }
                else
                {
                    float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                    beaconLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t) * powerEff;
                }
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool inside = dist <= beaconRadius && powerEff > 0f;

            if (inside && !_isPlayerInsideAura)
            {
                _isPlayerInsideAura = true;
                _appliedRegen = sanityRegenRate * powerEff;
                if (_playerController != null)
                {
                    _playerController.AddSanityAura(_appliedRegen);
                }
                Debug.Log($"[SanityBeacon] Player entered Sanity Beacon aura: +{_appliedRegen:F1} Sanity/s");
            }
            else if (!inside && _isPlayerInsideAura)
            {
                RemovePlayerAura();
            }
        }

        private void RemovePlayerAura()
        {
            if (_isPlayerInsideAura)
            {
                _isPlayerInsideAura = false;
                if (_playerController != null && _appliedRegen > 0f)
                {
                    _playerController.RemoveSanityAura(_appliedRegen);
                }
                _appliedRegen = 0f;
                Debug.Log("[SanityBeacon] Player left Sanity Beacon aura.");
            }
        }

        private void OnDestroy()
        {
            ActiveBeacons.Remove(this);
            RemovePlayerAura();
        }
    }
}

