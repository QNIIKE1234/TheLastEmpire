using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// Attach this component to any trigger collider (Campfire, Safe Zone, Lantern, Shelter).
    /// When the player stands inside this zone, their Sanity is continuously restored.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SanityAuraZone : MonoBehaviour
    {
        [Header("Sanity Aura Settings")]
        [Tooltip("อัตราการฟื้นฟูค่าสติต่อวินาทีเมื่อผู้เล่นอยู่ในรัศมี (เช่น 4.0 = +4 สติต่อวินาที)")]
        [SerializeField] private float sanityRegenPerSecond = 4.0f;

        [Tooltip("ชื่อแสดงผลของโซนนี้ เช่น Campfire, Safe Shelter")]
        [SerializeField] private string zoneName = "Safe Zone / Campfire";

        [Tooltip("จำเป็นต้องมีกองไฟลุกอยู่หรือไม่ (สามารถปิดการทำงานถ้าไฟดับ)")]
        [SerializeField] private bool isActive = true;

        public bool IsActive
        {
            get => isActive;
            set => isActive = value;
        }

        public float SanityRegenPerSecond
        {
            get => sanityRegenPerSecond;
            set => sanityRegenPerSecond = value;
        }

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;
            PlayerController pc = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.AddSanityAura(sanityRegenPerSecond);
                Debug.Log($"[SanityAuraZone] Player entered {zoneName}! Regenerating +{sanityRegenPerSecond} Sanity/sec.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController pc = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.RemoveSanityAura(sanityRegenPerSecond);
                Debug.Log($"[SanityAuraZone] Player exited {zoneName}.");
            }
        }
    }
}

