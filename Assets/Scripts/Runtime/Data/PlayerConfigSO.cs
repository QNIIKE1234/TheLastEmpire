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
        public float dashSpeed = 15f;
        public float dashDuration = 0.25f;
        public float dashCooldown = 0.8f;

        [Header("Survival")]
        public float maxHunger = 100f;

        [Header("Starting Loadout")]
        [Tooltip("จำนวนเงินเริ่มต้น")]
        public int startingMoney = 0;
        
        [Tooltip("รายการไอเทมที่จะได้รับตอนเริ่มเกม (ชื่อไอเทม)")]
        public List<string> startingItems = new List<string> { "Pistol", "Knife" };
        
        [Tooltip("กระสุนปืนพกเริ่มต้น")]
        public int startingPistolAmmo = 60;
    }
}
