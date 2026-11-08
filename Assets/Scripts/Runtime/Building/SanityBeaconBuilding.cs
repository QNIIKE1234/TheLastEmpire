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
    public class SanityBeaconBuilding : MonoBehaviour
    {
        public static readonly List<SanityBeaconBuilding> ActiveBeacons = new List<SanityBeaconBuilding>();

        [Header("Sanity Beacon Settings")]
        [Tooltip("อัตราการฟื้นฟูสติของผู้เล่นต่อวินาที")]
        [SerializeField] private float sanityRegenRate = 2.5f;

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

        public float BeaconRadius => beaconRadius;

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
        }

        private void OnDisable()
        {
            ActiveBeacons.Remove(this);
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
            // แสงไฟกะพริบเป็นจังหวะหายใจอย่างสงบ
            if (beaconLight != null)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                beaconLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool inside = dist <= beaconRadius;

            if (inside && !_isPlayerInsideAura)
            {
                _isPlayerInsideAura = true;
                if (_playerController != null)
                {
                    _playerController.AddSanityAura(sanityRegenRate);
                }
                Debug.Log("[SanityBeacon] Player entered Sanity Beacon aura: +2.5 Sanity/s");
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
                if (_playerController != null)
                {
                    _playerController.RemoveSanityAura(sanityRegenRate);
                }
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

