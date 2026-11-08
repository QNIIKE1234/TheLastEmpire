using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WaterPurifierBuilding — เครื่องกลั่นและกรองน้ำดื่มสะอาด (BUILD0012_WaterPurifierStation)
    /// คุณสมบัติ:
    /// - ผลิตน้ำดื่มสะอาด (Clean Water / Clean Glass Bottle) อัตโนมัติตามเวลา
    /// - ผลิต 1 ขวดทุกๆ 90 วินาที (กักเก็บสูงสุด 5 ขวด)
    /// - ผู้เล่นสามารถกด [E] เพื่อเก็บน้ำสะอาดเข้ากระเป๋า หรือเปิด WaterPurifier_HUD
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WaterPurifierBuilding : MonoBehaviour, IBuildingInteractable
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
        private PlayerInventory _playerInventory;
        private float _cycleTimer = 0f;

        public int CurrentStoredWater => currentStoredWater;
        public int MaxStoredWater => maxStoredWater;
        public float ProductionProgress => Mathf.Clamp01(_cycleTimer / purifyCycleDuration);

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.6f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            if (currentStoredWater > 0)
            {
                return $"<color=yellow><b>[E]</b></color> เก็บน้ำสะอาด ({currentStoredWater}/{maxStoredWater} ขวด)";
            }
            return $"กำลังกลั่นน้ำ... <color=#00e5ff>{(ProductionProgress * 100f):F0}%</color>";
        }

        public void OnInteract()
        {
            // ตรวจสอบว่ามี WaterPurifier_HUD ให้เปิดหรือไม่
            GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
            if (canvasGo != null)
            {
                Transform hud = canvasGo.transform.Find("WaterPurifier_HUD");
                if (hud != null && hud.childCount > 0)
                {
                    hud.gameObject.SetActive(true);
                    Debug.Log("[WaterPurifier] Opened WaterPurifier_HUD.");
                    return;
                }
            }

            if (currentStoredWater > 0)
            {
                CollectWater();
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
    }
}
