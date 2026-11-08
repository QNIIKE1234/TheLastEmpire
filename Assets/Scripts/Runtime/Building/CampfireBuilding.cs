using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// CampfireBuilding — กองไฟเพื่อการเอาชีวิตรอด
    /// คุณสมบัติ:
    /// - ปล่อยออร่าฟื้นฟูค่าสติ (Sanity Aura +3/วินาที)
    /// - แสงสว่างนำทางในความมืด (Point Light พร้อมเปลวไฟกะพริบสมจริง)
    /// - เปิดระยะการมองเห็นตอนกลางคืนเพิ่มอีก 40% เมื่ออยู่ใกล้กองไฟ
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class CampfireBuilding : MonoBehaviour
    {
        public static readonly List<CampfireBuilding> ActiveCampfires = new List<CampfireBuilding>();

        [Header("Sanity Aura")]
        [Tooltip("รัศมีของออร่ากองไฟ (เมตร)")]
        [SerializeField] private float auraRadius = 5.0f;

        [Tooltip("อัตราการฟื้นฟูค่าสติ (Sanity/วินาที)")]
        [SerializeField] private float sanityRegenRate = 3.0f;

        [Header("Light Flickering")]
        [SerializeField] private Light fireLight;
        [SerializeField] private float minIntensity = 2.2f;
        [SerializeField] private float maxIntensity = 3.8f;
        [SerializeField] private float lightRange = 10.0f;
        [SerializeField] private Color lightColor = new Color(1.0f, 0.62f, 0.22f);

        [Header("Night Vision Expansion")]
        [Tooltip("เปอร์เซ็นต์ขยายระยะการมองเห็นตอนกลางคืนเมื่ออยู่ใกล้กองไฟ (0.40 = +40%)")]
        [SerializeField] private float nightVisionBonus = 0.40f;

        [Tooltip("รัศมีตรวจจับว่าผู้เล่นอยู่ใกล้กองไฟเพื่อให้โบนัสการมองเห็น 40% (เมตร)")]
        [SerializeField] private float visionAuraRadius = 6.5f;

        [Tooltip("รัศมีวงสว่างของกองไฟบนหน้าจอ Shader (Viewport Radius)")]
        [SerializeField] private float lightHoleRadius = 0.26f;

        public float VisionAuraRadius => visionAuraRadius;
        public float NightVisionBonus => nightVisionBonus;
        public float LightHoleRadius => lightHoleRadius;

        private PlayerController _activePlayer;
        private bool _isPlayerInsideAura = false;
        private float _flickerSeed;

        private void OnEnable()
        {
            if (!ActiveCampfires.Contains(this))
            {
                ActiveCampfires.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveCampfires.Remove(this);
        }

        private void Awake()
        {
            _flickerSeed = Random.Range(0f, 1000f);
            EnsureFireLightCreated();
        }

        private void Start()
        {
            EnsureFireLightCreated();

            // สร้าง Trigger สำหรับเช็คระยะออร่ากองไฟ (Aura Trigger) แยกต่างหาก ไม่ทับ Physical Collider
            SphereCollider auraTrigger = gameObject.AddComponent<SphereCollider>();
            auraTrigger.isTrigger = true;
            auraTrigger.radius = auraRadius;
            auraTrigger.center = Vector3.zero;
        }

        private void EnsureFireLightCreated()
        {
            if (fireLight == null)
            {
                fireLight = GetComponentInChildren<Light>();
            }

            if (fireLight == null)
            {
                GameObject lightObj = new GameObject("Campfire_PointLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);

                fireLight = lightObj.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = lightColor;
                fireLight.range = lightRange;
                fireLight.intensity = (minIntensity + maxIntensity) * 0.5f;
                fireLight.shadows = LightShadows.Soft;
            }
        }

        private void Update()
        {
            if (fireLight != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * 6.5f, _flickerSeed);
                fireLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerController player = other.GetComponent<PlayerController>();
                if (player != null && !_isPlayerInsideAura)
                {
                    _activePlayer = player;
                    _isPlayerInsideAura = true;
                    _activePlayer.AddSanityAura(sanityRegenRate);
                    Debug.Log("[Campfire] Player entered campfire warmth: Gained Sanity recovery aura.");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (_activePlayer != null && _isPlayerInsideAura)
                {
                    _activePlayer.RemoveSanityAura(sanityRegenRate);
                    _isPlayerInsideAura = false;
                    _activePlayer = null;
                    Debug.Log("[Campfire] Player left campfire warmth: Removed Sanity aura.");
                }
            }
        }

        private void OnDestroy()
        {
            ActiveCampfires.Remove(this);
            if (_activePlayer != null && _isPlayerInsideAura)
            {
                _activePlayer.RemoveSanityAura(sanityRegenRate);
                _isPlayerInsideAura = false;
                _activePlayer = null;
            }
        }
    }
}

