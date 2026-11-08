using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// CampfireBuilding — กองไฟเพื่อการเอาชีวิตรอด
    /// คุณสมบัติ:
    /// - ปล่อยออร่าฟื้นฟูค่าสติ (Sanity Aura) ให้กับผู้เล่นที่อยู่ใกล้กองไฟ
    /// - มีแสงสว่างนำทางในความมืด
    /// - ทำอาหาร: เมื่อกด E ใกล้กองไฟ สามารถย่างเนื้อเป็น Cooked Meat ได้
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class CampfireBuilding : MonoBehaviour
    {
        [Header("Sanity Aura")]
        [Tooltip("รัศมีของออร่ากองไฟ (เมตร)")]
        [SerializeField] private float auraRadius = 5.0f;

        [Tooltip("อัตราการฟื้นฟูค่าสติ (Sanity/วินาที)")]
        [SerializeField] private float sanityRegenRate = 3.0f;

        [Header("Light Flickering")]
        [SerializeField] private Light fireLight;
        [SerializeField] private float minIntensity = 2.0f;
        [SerializeField] private float maxIntensity = 3.5f;

        private PlayerController _activePlayer;
        private bool _isPlayerInsideAura = false;

        private void Start()
        {
            if (fireLight == null)
            {
                fireLight = GetComponentInChildren<Light>();
            }

            // สร้าง Trigger สำหรับเช็คระยะออร่ากองไฟ (Aura Trigger) แยกต่างหาก ไม่ทับ Physical Collider
            SphereCollider auraTrigger = gameObject.AddComponent<SphereCollider>();
            auraTrigger.isTrigger = true;
            auraTrigger.radius = auraRadius;
            auraTrigger.center = Vector3.zero;
        }

        private void Update()
        {
            if (fireLight != null)
            {
                fireLight.intensity = Mathf.Lerp(fireLight.intensity, Random.Range(minIntensity, maxIntensity), Time.deltaTime * 8f);
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
            if (_activePlayer != null && _isPlayerInsideAura)
            {
                _activePlayer.RemoveSanityAura(sanityRegenRate);
                _isPlayerInsideAura = false;
                _activePlayer = null;
            }
        }
    }
}

