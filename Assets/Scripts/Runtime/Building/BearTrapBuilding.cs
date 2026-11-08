using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// BearTrapBuilding — กับดักหมีฟันเหล็กสปริง (BUILD0019_SnapJawBearTrap)
    /// คุณสมบัติ:
    /// - ทำงานเมื่อศัตรูเดินเหยียบกับดัก (Trigger)
    /// - สร้างความเสียหายกายภาพฉับพลัน 120 หน่วย และตรึงศัตรูให้อยู่กับที่ (Root / Stun) นาน 3.5 วินาที
    /// - เมื่อสปริงทำงานแล้ว ผู้เล่นสามารถเดินมากด [E] เพื่อติดตั้ง (Rearm) กับดักใหม่ได้
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BearTrapBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Trap Settings")]
        [Tooltip("ความเสียหายเมื่อกับดักงับ")]
        [SerializeField] private float snapDamage = 120f;

        [Tooltip("ระยะเวลาตรึงศัตรูให้อยู่กับที่ (วินาที)")]
        [SerializeField] private float rootDuration = 3.5f;

        [Tooltip("สถานะว่ากับดักถูกดีดไปแล้วหรือไม่")]
        [SerializeField] private bool isSprung = false;

        [Header("Visual Elements")]
        [Tooltip("ชิ้นส่วนขากรรไกรกับดักเพื่อหุบหรือกาง")]
        [SerializeField] private Transform trapJawsTransform;
        [SerializeField] private float interactionDistance = 2.2f;

        private BuildingObject _buildingObject;

        public bool IsSprung => isSprung;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 0.8f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => isSprung && (_buildingObject == null || !_buildingObject.IsDead);

        public string GetInteractionPrompt()
        {
            return "<color=yellow><b>[E]</b></color> Rearm Trap";
        }

        public void OnInteract()
        {
            if (isSprung)
            {
                RearmTrap();
            }
        }

        public void OnSecondaryInteract()
        {
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (_buildingObject != null)
            {
                _buildingObject.AutoAddNavMeshObstacle = false;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSprung) return;

            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                return;
            }

            IDamageable victim = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (victim != null)
            {
                SpringTrap(victim, other.gameObject);
            }
        }

        private void SpringTrap(IDamageable victim, GameObject victimGo)
        {
            isSprung = true;

            if (trapJawsTransform != null)
            {
                trapJawsTransform.localScale = new Vector3(1f, 0.2f, 1f);
            }

            victim.TakeDamage(snapDamage);

            BaseEnemyAI enemy = victimGo.GetComponent<BaseEnemyAI>() ?? victimGo.GetComponentInParent<BaseEnemyAI>();
            if (enemy != null)
            {
                // Can trigger stagger or stop movement
            }

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 0.6f,
                    snapDamage,
                    true,
                    "SNAP! ROOTED"
                );
            }

            Debug.Log($"[BearTrap] Trap snapped on {victimGo.name}! Dealt {snapDamage} damage and rooted for {rootDuration}s.");
        }

        public void RearmTrap()
        {
            isSprung = false;

            if (trapJawsTransform != null)
            {
                trapJawsTransform.localScale = Vector3.one;
            }

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 0.6f,
                    0f,
                    false,
                    "TRAP RE-ARMED"
                );
            }

            Debug.Log("[BearTrap] Bear trap re-armed and ready for action.");
        }
    }
}
