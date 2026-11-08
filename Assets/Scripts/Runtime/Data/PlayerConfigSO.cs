using UnityEngine;
using System.Collections.Generic;

namespace TheLastEmpire
{
    [CreateAssetMenu(fileName = "NewPlayerConfig", menuName = "TheLastEmpire/Player Config")]
    public class PlayerConfigSO : ScriptableObject
    {
        [Header("Status Base")]
        public float maxHealth = 100f;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float dashSpeed = 10f;
        public float dashDuration = 0.25f;
        public float dashCooldown = 0.8f;

        [Header("Survival")]
        public float maxHunger = 100f;
        public float maxSanity = 100f;
        [Tooltip("ตัวคูณอัตราการลดของค่าสติโดยรวม (1.0 = ปกติ, <1.0 = ลดช้า, >1.0 = ลดเร็ว)")]
        public float sanityDepletionMultiplier = 1.0f;
        [Tooltip("เปอร์เซ็นต์ดาเมจที่ได้รับที่จะถูกหักออกจากค่าสติ (0.10 = 10%)")]
        public float sanityDamageTakenPercent = 0.10f;
        [Tooltip("อัตราการลดของค่าสติต่อวินาทีในตอนกลางคืน")]
        public float nightSanityDepletionRate = 0.5f;
        [Tooltip("อัตราการฟื้นฟูค่าสติต่อวินาทีเมื่ออยู่อย่างปลอดภัยในตอนกลางวัน")]
        public float daySanityRecoveryRate = 0.2f;

        [Header("Starting Loadout")]
        [Tooltip("ความจุช่องเก็บของในกระเป๋าเริ่มต้น (ขั้นต่ำ 18, สูงสุด 30)")]
        [Range(18, 30)]
        public int maxInventorySlots = 20;

        [Tooltip("จำนวนเงินเริ่มต้น")]
        public int startingMoney = 0;
        
        [Tooltip("รายการไอเทมที่จะได้รับตอนเริ่มเกม (ชื่อไอเทม)")]
        public List<string> startingItems = new List<string> { "Pistol", "Knife" };
        
        [Tooltip("กระสุนปืนพกเริ่มต้น")]
        public int startingPistolAmmo = 60;
    }
}
