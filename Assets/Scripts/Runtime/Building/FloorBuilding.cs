using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// FloorBuilding — ฐานรากพื้นไม้ (BUILD0001_WoodenFloorFoundation)
    /// คุณสมบัติ:
    /// - เป็นพื้นฐานรากสำหรับสร้างฐานที่มั่น
    /// - ไม่ขวางทางเดิน NavMesh (NavMeshObstacle ปิดหรือ Carving พื้นผิว)
    /// - มอบบัฟ "Survivor Foundation Stability": ผู้เล่นเดินเร็วขึ้น +10% เมื่ออยู่บนพื้นฐานทัพ
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class FloorBuilding : MonoBehaviour
    {
        [Header("Foundation Buff Settings")]
        [Tooltip("โบนัสความเร็วเคลื่อนที่เมื่อผู้เล่นเดินบนพื้นฐานทัพ (+10% = 0.10f)")]
        [SerializeField] private float speedBuffMultiplier = 0.10f;

        private BuildingObject _buildingObject;
        private bool _isPlayerOnFloor = false;
        private PlayerController _playerOnFloor;

        public float SpeedBuffMultiplier => speedBuffMultiplier;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (_buildingObject != null)
            {
                // พื้นไม่ควรเป็น NavMeshObstacle ที่บล็อกการเดิน
                _buildingObject.AutoAddNavMeshObstacle = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerController player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    _playerOnFloor = player;
                    _isPlayerOnFloor = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (_playerOnFloor != null && other.gameObject == _playerOnFloor.gameObject)
                {
                    _isPlayerOnFloor = false;
                    _playerOnFloor = null;
                }
            }
        }

        private void OnDestroy()
        {
            _isPlayerOnFloor = false;
            _playerOnFloor = null;
        }
    }
}

