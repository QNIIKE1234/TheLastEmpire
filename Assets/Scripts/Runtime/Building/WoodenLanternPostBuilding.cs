using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WoodenLanternPostBuilding — เสาโคมไฟไม้โบราณส่องสว่าง (BUILD0023_WoodenLanternPost)
    /// คุณสมบัติ:
    /// - เสาไม้ธรรมชาติที่มีคานไม้ยื่นและโคมไฟห้อยลงมา
    /// - ส่องแสงสว่างสีส้มทองอบอุ่น (Warm Lantern Glow) กะพริบเป็นประกายเปลวไฟอย่างเป็นธรรมชาติ
    /// - ขับไล่ความมืดมิดในยามค่ำคืนในรัศมี 12 เมตร
    /// - มอบออร่าฟื้นฟูสติเล็กน้อย (+1.0 Sanity/วินาที) เมื่อยืนอยู่ใต้แสงไฟตอนกลางคืน
    /// - คราฟต์ง่าย ใช้วัตถุดิบพื้นฐานจากธรรมชาติ (ไม้, หิน, เชือก/เส้นใย, ถ่าน)
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WoodenLanternPostBuilding : MonoBehaviour
    {
        public static readonly List<WoodenLanternPostBuilding> ActiveLanterns = new List<WoodenLanternPostBuilding>();

        [Header("Lantern Illumination Settings")]
        [Tooltip("รัศมีของแสงไฟส่องสว่าง (เมตร)")]
        [SerializeField] private float lightRadius = 12.0f;

        [Tooltip("อัตราการฟื้นฟูสติของผู้เล่นเมื่ออยู่ใต้แสงไฟ (Sanity/วินาที)")]
        [SerializeField] private float sanityRegenRate = 1.0f;

        [Header("Light Component & Flicker")]
        [SerializeField] private Light lanternLight;
        [SerializeField] private float minIntensity = 2.4f;
        [SerializeField] private float maxIntensity = 3.2f;
        [SerializeField] private float flickerSpeed = 5.0f;
        [SerializeField] private Color lightColor = new Color(1.0f, 0.72f, 0.35f);

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private PlayerController _playerController;
        private bool _isPlayerInsideAura = false;
        private float _flickerNoiseSeed;

        public float LightRadius => lightRadius;
        public Light LanternLight => lanternLight;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (lanternLight == null)
            {
                lanternLight = GetComponentInChildren<Light>();
            }

            if (lanternLight != null)
            {
                lanternLight.color = lightColor;
                lanternLight.range = lightRadius;
            }

            _flickerNoiseSeed = Random.Range(0f, 1000f);
        }

        private void OnEnable()
        {
            if (!ActiveLanterns.Contains(this))
            {
                ActiveLanterns.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveLanterns.Remove(this);
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
            if (_buildingObject != null && _buildingObject.IsDead)
            {
                if (lanternLight != null && lanternLight.enabled)
                {
                    lanternLight.enabled = false;
                }
                RemovePlayerAura();
                return;
            }

            // แสงไฟกะพริบเป็นประกายเปลวไฟอย่างเป็นธรรมชาติผ่าน Perlin Noise
            if (lanternLight != null && lanternLight.enabled)
            {
                float noise = Mathf.PerlinNoise(_flickerNoiseSeed, Time.time * flickerSpeed);
                lanternLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
            }

            // ตรวจสอบระยะผู้เล่นเพื่อมอบความอุ่นใจ/สติเมื่ออยู่ใต้แสงไฟยามค่ำคืน
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool inside = dist <= lightRadius;

            if (inside && !_isPlayerInsideAura)
            {
                _isPlayerInsideAura = true;
                if (_playerController != null && sanityRegenRate > 0f)
                {
                    _playerController.AddSanityAura(sanityRegenRate);
                }
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
                if (_playerController != null && sanityRegenRate > 0f)
                {
                    _playerController.RemoveSanityAura(sanityRegenRate);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1.0f, 0.75f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, lightRadius);
        }
    }
}
