using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// WindowWallBuilding — กำแพงไม้พร้อมช่องหน้าต่างยิง (BUILD0003_WoodenWindowWall)
    /// คุณสมบัติ:
    /// - ป้องกันการบุกทะลวงของศัตรูประชิด (NavMesh Carving)
    /// - มีช่องหน้าต่างสำหรับเล็งและยิงปืนออกไปภายนอก
    /// - มอบบัฟ "Window Cover": ลดความเสียหายจากการถูกยิงระยะไกลลง 50% เมื่อผู้เล่นยืนหลบหลังกำแพงหน้าต่าง
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WindowWallBuilding : MonoBehaviour
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

            // ซ่อมแซมด้วยปุ่ม E เมื่อกำแพงหน้าต่างได้รับความเสียหาย
            if (_isPlayerTakingCover && _buildingObject != null && _buildingObject.CurrentHealth < _buildingObject.MaxHealth)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    RepairWindow();
                }
            }
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

        private void OnGUI()
        {
            if (_isPlayerTakingCover)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
                if (screenPos.z > 0)
                {
                    float width = 230f;
                    float height = 36f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    GUI.color = new Color(0.4f, 0.85f, 1.0f, 0.95f);
                    GUI.Box(rect, $"[Window Cover: -{(rangedDamageReduction * 100f):F0}% Ranged Dmg]");
                }
            }
        }
    }
}
