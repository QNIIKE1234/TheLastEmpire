using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WallBuilding — กำแพงป้องกันฐานทัพ
    /// รองรับ:
    /// - BUILD0002_WoodenWallPanel (กำแพงไม้)
    /// - BUILD0005_StoneAndBrickWall (กำแพงหินและอิฐ)
    /// - BUILD0006_ScrapMetalWall (กำแพงเศษเหล็ก)
    /// - BUILD0007_ArmoredTitaniumWall (กำแพงไททาเนียมหุ้มเกราะ)
    /// - BUILD0018_ChitinCarapaceWall (กำแพงเกราะเปลือกไคติน)
    /// 
    /// คุณสมบัติ:
    /// - บล็อกการเดินของศัตรูอย่างสมบูรณ์ (NavMeshObstacle Carving)
    /// - รองรับการกด [E] ซ่อมแซมกำแพงเมื่อได้รับความเสียหาย
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WallBuilding : MonoBehaviour, IBuildingInteractable
    {
        public enum WallMaterialType
        {
            Wood,
            Stone,
            ScrapMetal,
            ArmoredTitanium,
            Chitin
        }

        [Header("Wall Configuration")]
        [SerializeField] private WallMaterialType wallType = WallMaterialType.Wood;
        [SerializeField] private float repairAmountPerInteract = 50f;
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;

        public WallMaterialType WallType => wallType;
        public BuildingObject Building => _buildingObject;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.6f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject != null && !_buildingObject.IsDead && _buildingObject.CurrentHealth < _buildingObject.MaxHealth;

        public string GetInteractionPrompt()
        {
            if (_buildingObject != null)
            {
                return $"<color=yellow><b>[E]</b></color> ซ่อมแซม ({_buildingObject.CurrentHealth:F0}/{_buildingObject.MaxHealth:F0})";
            }
            return "<color=yellow><b>[E]</b></color> ซ่อมแซม";
        }

        public void OnInteract()
        {
            RepairWall();
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
        }

        public void RepairWall()
        {
            if (_buildingObject == null || _buildingObject.IsDead) return;

            float missingHp = _buildingObject.MaxHealth - _buildingObject.CurrentHealth;
            if (missingHp <= 0f) return;

            float healAmt = Mathf.Min(missingHp, repairAmountPerInteract);
            _buildingObject.Repair(healAmt);

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 1.5f,
                    healAmt,
                    false,
                    $"+{healAmt:F0} HP"
                );
            }

            Debug.Log($"[WallBuilding] Repaired {_buildingObject.Data?.buildingName ?? gameObject.name}: +{healAmt} HP ({_buildingObject.CurrentHealth}/{_buildingObject.MaxHealth})");
        }
    }
}
