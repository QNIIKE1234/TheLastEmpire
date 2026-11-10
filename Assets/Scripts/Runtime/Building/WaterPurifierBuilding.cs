using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// WaterPurifierBuilding — เครื่องกลั่นและกรองน้ำดื่มสะอาด (BUILD0012_WaterPurifierStation)
    /// คุณสมบัติ:
    /// - ผลิตน้ำดื่มสะอาด (Water Bottle) อัตโนมัติตามเวลา
    /// - ผลิต 1 ขวดทุกๆ รอบการทำงาน (ค่าเริ่มต้น 60 วินาที)
    /// - เมื่อผลิตเสร็จ ส่งน้ำดื่ม 1 ขวดเข้าตัว/กระเป๋าผู้เล่นโดยตรงทันที (+น้ำดื่ม x1)
    /// - หากช่องกระเป๋าผู้เล่นเต็ม จะกักเก็บสำรองไว้ในเครื่อง (สูงสุด 5 ขวด) ผู้เล่นสามารถกด [E] เพื่อเก็บเข้าตัวได้เมื่อช่องว่าง
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class WaterPurifierBuilding : MonoBehaviour, IBuildingInteractable
    {
        [Header("Purifier Production Settings")]
        [Tooltip("ระยะเวลาการกลั่นน้ำสะอาด 1 ขวด (วินาที)")]
        [SerializeField] private float purifyCycleDuration = 60.0f;

        [Tooltip("จำนวนน้ำดื่มสะอาดสูงสุดที่สถานีนี้กักเก็บสำรองได้ (กรณีช่องกระเป๋าผู้เล่นเต็ม)")]
        [SerializeField] private int maxStoredWater = 5;

        [Tooltip("จำนวนน้ำดื่มสะอาดที่สำรองพร้อมเก็บในเครื่องปัจจุบัน")]
        [SerializeField] private int currentStoredWater = 0;

        [Tooltip("ชื่อไอเทมน้ำสะอาดที่จะมอบให้ผู้เล่น")]
        [SerializeField] private string cleanWaterItemName = "Water Bottle";

        [Tooltip("ส่งน้ำเข้ากระเป๋าผู้เล่นอัตโนมัติทันทีที่ผลิตเสร็จ")]
        [SerializeField] private bool autoDeliverToPlayer = true;

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
                return $"<color=yellow><b>[E]</b></color> Collect Water ({currentStoredWater}/{maxStoredWater})";
            }
            return $"Purifying Water... <color=#00e5ff>{(ProductionProgress * 100f):F0}%</color>";
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
            else
            {
                // หากไม่มีน้ำสำรองในเครื่อง ให้แสดงสถานะการผลิต
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        $"กำลังกลั่นน้ำ... {(ProductionProgress * 100f):F0}%"
                    );
                }
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
            if (_buildingObject != null && _buildingObject.IsDead) return;

            // หากคลังสำรองยังไม่เต็ม สามารถเดินรอบผลิตได้ต่อเนื่อง
            if (currentStoredWater < maxStoredWater)
            {
                _cycleTimer += Time.deltaTime;
                if (_cycleTimer >= purifyCycleDuration)
                {
                    _cycleTimer = 0f;
                    OnPurifyCycleComplete();
                }
            }
        }

        private void OnPurifyCycleComplete()
        {
            if (_playerInventory == null)
            {
                FindPlayer();
            }

            bool delivered = false;

            // 1. ถ้าตั้ง autoDeliverToPlayer ให้ลองส่งเข้าตัวผู้เล่นทันที
            if (autoDeliverToPlayer && _playerInventory != null)
            {
                delivered = TryAddWaterToInventory(_playerInventory, 1);
                if (delivered)
                {
                    Debug.Log("[WaterPurifier] Pure water ready: Delivered 1 bottle directly to player inventory.");
                    if (DamageNumberManager.Instance != null)
                    {
                        Vector3 noticePos = _playerInventory.transform.position + Vector3.up * 1.8f;
                        DamageNumberManager.Instance.SpawnDamageNumber(
                            noticePos,
                            0f,
                            false,
                            "+น้ำสะอาด (Water Bottle) x1"
                        );
                    }
                }
            }

            // 2. หากส่งเข้าตัวไม่ได้ (เช่น กระเป๋าเต็ม หรือไม่มีผู้เล่น) ให้กักเก็บสำรองไว้ในเครื่อง
            if (!delivered)
            {
                if (currentStoredWater < maxStoredWater)
                {
                    currentStoredWater++;
                    Debug.Log($"[WaterPurifier] Pure water buffered in machine: {currentStoredWater}/{maxStoredWater}");

                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.SpawnDamageNumber(
                            transform.position + Vector3.up * 1.8f,
                            0f,
                            false,
                            $"น้ำสะอาดพร้อมเก็บ ({currentStoredWater}/{maxStoredWater})"
                        );
                    }
                }
            }
        }

        private bool TryAddWaterToInventory(PlayerInventory inventory, int count)
        {
            if (inventory == null || count <= 0) return false;

            // ตรวจสอบชื่อไอเทมน้ำสะอาด ป้องกันกรณีใช้ ID เก่า (ITEM0075_CleanGlassBottle)
            string targetItem = cleanWaterItemName;
            if (string.IsNullOrEmpty(targetItem) || targetItem == "ITEM0075_CleanGlassBottle" || targetItem == "Clean Glass Bottle")
            {
                targetItem = "Water Bottle";
            }

            bool added = inventory.AddItem(targetItem, count);
            if (!added && targetItem != "Water Bottle")
            {
                added = inventory.AddItem("Water Bottle", count);
            }
            if (!added && targetItem != "ITEM0091_WaterBottle")
            {
                added = inventory.AddItem("ITEM0091_WaterBottle", count);
            }

            return added;
        }

        public void CollectWater()
        {
            if (currentStoredWater <= 0) return;

            if (_playerInventory == null)
            {
                FindPlayer();
                if (_playerInventory == null) return;
            }

            int toCollect = currentStoredWater;
            bool added = TryAddWaterToInventory(_playerInventory, toCollect);

            if (added)
            {
                currentStoredWater = 0;
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        $"+น้ำสะอาด (Water Bottle) x{toCollect}"
                    );
                }
                Debug.Log($"[WaterPurifier] Player collected {toCollect} bottles of clean water.");
            }
            else
            {
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        "กระเป๋าเต็ม! เคลียร์ช่องก่อนเก็บน้ำ"
                    );
                }
                Debug.LogWarning("[WaterPurifier] Inventory full! Cannot collect water.");
            }
        }
    }
}
