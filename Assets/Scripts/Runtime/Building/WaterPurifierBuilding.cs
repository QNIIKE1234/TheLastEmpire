using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// WaterPurifierBuilding — เครื่องกลั่นและกรองน้ำดื่มสะอาด (BUILD0012_WaterPurifierStation)
    /// คุณสมบัติ:
    /// - ผลิตน้ำดื่มสะอาด (Clean Water / Clean Glass Bottle) อัตโนมัติตามเวลา
    /// - ผลิต 1 ขวดทุกๆ 90 วินาที (กักเก็บสูงสุด 5 ขวด)
    /// - ผู้เล่นสามารถกด [E] เพื่อเก็บน้ำสะอาดเข้ากระเป๋า
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WaterPurifierBuilding : MonoBehaviour
    {
        [Header("Purifier Production Settings")]
        [Tooltip("ระยะเวลาการกลั่นน้ำสะอาด 1 ขวด (วินาที)")]
        [SerializeField] private float purifyCycleDuration = 90.0f;

        [Tooltip("จำนวนน้ำดื่มสะอาดสูงสุดที่สถานีนี้กักเก็บได้")]
        [SerializeField] private int maxStoredWater = 5;

        [Tooltip("จำนวนน้ำดื่มสะอาดที่พร้อมเก็บในปัจจุบัน")]
        [SerializeField] private int currentStoredWater = 1;

        [Tooltip("ชื่อไอเทมน้ำสะอาดที่จะมอบให้ผู้เล่น")]
        [SerializeField] private string cleanWaterItemName = "ITEM0075_CleanGlassBottle";

        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private PlayerInventory _playerInventory;
        private bool _isPlayerNearby = false;
        private float _cycleTimer = 0f;

        public int CurrentStoredWater => currentStoredWater;
        public int MaxStoredWater => maxStoredWater;
        public float ProductionProgress => Mathf.Clamp01(_cycleTimer / purifyCycleDuration);

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
                _playerInventory = pc.GetComponent<PlayerInventory>();
            }
        }

        private void Update()
        {
            // ทำการผลิตน้ำสะอาดเมื่อคลังเก็บยังไม่เต็ม
            if (currentStoredWater < maxStoredWater)
            {
                _cycleTimer += Time.deltaTime;
                if (_cycleTimer >= purifyCycleDuration)
                {
                    _cycleTimer = 0f;
                    currentStoredWater++;
                    Debug.Log($"[WaterPurifier] Pure water ready: {currentStoredWater}/{maxStoredWater}");
                }
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            if (_isPlayerNearby && currentStoredWater > 0)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    CollectWater();
                }
            }
        }

        public void CollectWater()
        {
            if (currentStoredWater <= 0) return;

            if (_playerInventory == null)
            {
                FindPlayer();
                if (_playerInventory == null) return;
            }

            int collected = currentStoredWater;
            bool added = _playerInventory.AddItem(cleanWaterItemName, collected);

            // หากชื่อรหัสไอเทมไม่เข้า ให้ลองใช้ Clean Glass Bottle
            if (!added)
            {
                added = _playerInventory.AddItem("Clean Glass Bottle", collected);
            }

            if (added)
            {
                currentStoredWater = 0;
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        $"+น้ำสะอาด x{collected}"
                    );
                }
                Debug.Log($"[WaterPurifier] Player collected {collected} bottles of clean water.");
            }
            else
            {
                Debug.LogWarning("[WaterPurifier] Inventory full! Cannot collect water.");
            }
        }

        private void OnGUI()
        {
            if (_isPlayerNearby)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.6f);
                if (screenPos.z > 0)
                {
                    float width = 250f;
                    float height = 40f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    if (currentStoredWater > 0)
                    {
                        GUI.color = new Color(0.3f, 0.9f, 1f, 0.95f);
                        GUI.Box(rect, $"[E] เก็บน้ำดื่มสะอาด ({currentStoredWater}/{maxStoredWater} ขวด)");
                    }
                    else
                    {
                        GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                        GUI.Box(rect, $"กำลังกลั่นน้ำ... ({(ProductionProgress * 100f):F0}%)");
                    }
                }
            }
        }
    }
}
