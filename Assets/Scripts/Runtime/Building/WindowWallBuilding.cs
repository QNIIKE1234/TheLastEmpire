using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WindowWallBuilding — กำแพงไม้พร้อมช่องหน้าต่างยิง (BUILD0003_WoodenWindowWall)
    /// คุณสมบัติ:
    /// - ป้องกันการบุกทะลวงของศัตรูประชิด (NavMesh Carving)
    /// - มีช่องหน้าต่างสำหรับเล็งและยิงปืนออกไปภายนอก
    /// - มอบบัฟ "Window Cover": ลดความเสียหายจากการถูกยิงระยะไกลลง 50% เมื่อผู้เล่นยืนหลบหลังกำแพงหน้าต่าง
    /// - กด [E] เพื่อซ่อมแซมหน้าต่างเมื่อได้รับความเสียหาย ผ่านระบบ Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WindowWallBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Window Cover Settings")]
        [Tooltip("ระยะที่ผู้เล่นต้องอยู่ใกล้หน้าต่างเพื่อรับโบนัสกำบัง (เมตร)")]
        [SerializeField] private float coverDistance = 2.0f;

        [Tooltip("เปอร์เซ็นต์ลดความเสียหายระยะไกลเมื่ออยู่ในแนวกำบัง (0.50 = ลด 50%)")]
        [Range(0f, 0.9f)]
        [SerializeField] private float rangedDamageReduction = 0.50f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private bool _isPlayerTakingCover = false;

        public bool IsPlayerTakingCover => _isPlayerTakingCover;
        public float RangedDamageReduction => rangedDamageReduction;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.6f;
        public float InteractionDistance => coverDistance;
        public bool CanInteract => _buildingObject != null && !_buildingObject.IsDead && _buildingObject.CurrentHealth < _buildingObject.MaxHealth;

        public string GetInteractionPrompt()
        {
            if (_buildingObject != null)
            {
                return $"<color=yellow><b>[E]</b></color> Repair Window ({_buildingObject.CurrentHealth:F0}/{_buildingObject.MaxHealth:F0})";
            }
            return "<color=yellow><b>[E]</b></color> Repair Window";
        }

        public void OnInteract()
        {
            RepairWindow();
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
            _isPlayerTakingCover = dist <= coverDistance;
        }

        public void RepairWindow()
        {
            if (_buildingObject == null || _buildingObject.IsDead) return;

            float missingHp = _buildingObject.MaxHealth - _buildingObject.CurrentHealth;
            if (missingHp <= 0f) return;

            float healAmt = Mathf.Min(missingHp, 40f);
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
        }
    }
}
