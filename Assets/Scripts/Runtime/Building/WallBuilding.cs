using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// WallBuilding — กำแพงป้องกันฐานทัพ
    /// รองรับ:
    /// - BUILD0002_WoodenWallPanel (กำแพงไม้)
    /// - BUILD0005_StoneAndBrickWall (กำแพงหินและอิฐ)
    /// - BUILD0006_ScrapMetalWall (กำแพงเศษเหล็ก)
    /// - BUILD0007_ArmoredTitaniumWall (กำแพงไททาเนียมหุ้มเกราะ)
    /// 
    /// คุณสมบัติ:
    /// - บล็อกการเดินของศัตรูอย่างสมบูรณ์ (NavMeshObstacle Carving)
    /// - รองรับการกด [E] ซ่อมแซมกำแพงเมื่อได้รับความเสียหาย
    /// - แสดงสถานะความทนทานและความต้านทานดาเมจ
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WallBuilding : MonoBehaviour
    {
        public enum WallMaterialType
        {
            Wood,
            Stone,
            ScrapMetal,
            ArmoredTitanium
        }

        [Header("Wall Configuration")]
        [SerializeField] private WallMaterialType wallType = WallMaterialType.Wood;
        [SerializeField] private float repairAmountPerInteract = 50f;
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private bool _isPlayerNearby = false;

        public WallMaterialType WallType => wallType;
        public BuildingObject Building => _buildingObject;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
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
                _playerTransform = pc.transform;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            // เมื่อกำแพงเสียหาย และผู้เล่นกด E เพื่อซ่อมแซม
            if (_isPlayerNearby && _buildingObject != null && _buildingObject.CurrentHealth < _buildingObject.MaxHealth)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    RepairWall();
                }
            }
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

        private void OnGUI()
        {
            // แสดง Interaction Prompt เมื่อผู้เล่นอยู่ใกล้และกำแพงเสียหาย
            if (_isPlayerNearby && _buildingObject != null && _buildingObject.CurrentHealth < _buildingObject.MaxHealth)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.6f);
                if (screenPos.z > 0)
                {
                    float width = 220f;
                    float height = 40f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    GUI.color = new Color(1f, 0.9f, 0.4f, 0.95f);
                    GUI.Box(rect, $"[E] ซ่อมแซม ({_buildingObject.CurrentHealth:F0}/{_buildingObject.MaxHealth:F0})");
                }
            }
        }
    }
}

