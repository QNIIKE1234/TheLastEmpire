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
    public class WaterPurifierBuilding : MonoBehaviour, IBuildingInteractable, IPowerConsumer
    {
        [Header("Purifier Production Settings")]
        [Tooltip("ระยะเวลาการกลั่นน้ำสะอาด 1 ขวด (วินาที) — 1 วันในเกม (360s) ได้ 2 ขวด = 180s ต่อขวด")]
        [SerializeField] private float purifyCycleDuration = 180.0f;

        [Tooltip("จำนวนน้ำดื่มสะอาดสูงสุดที่สถานีนี้กักเก็บสำรองได้ (สูงสุด 3 ขวด)")]
        [SerializeField] private int maxStoredWater = 3;

        [Tooltip("จำนวนน้ำดื่มสะอาดที่สำรองพร้อมเก็บในเครื่องปัจจุบัน")]
        [SerializeField] private int currentStoredWater = 0;

        [Tooltip("ชื่อไอเทมน้ำสะอาดที่จะมอบให้ผู้เล่น")]
        [SerializeField] private string cleanWaterItemName = "Water Bottle";

        [Tooltip("ส่งน้ำเข้ากระเป๋าผู้เล่นอัตโนมัติทันทีที่ผลิตเสร็จ (false = กักเก็บในเครื่องให้เปิด HUD เก็บ)")]
        [SerializeField] private bool autoDeliverToPlayer = false;

        [Header("Power Settings")]
        [Tooltip("จำเป็นต้องมีไฟฟ้าจากเครื่องปั่นไฟ (Fusion Generator) ในการทำงานหรือไม่")]
        [SerializeField] private bool requiresPower = true;

        [Tooltip("ปริมาณไฟฟ้าที่ต้องการใช้ในการทำงาน (วัตต์/หน่วย)")]
        [SerializeField] private float powerDemand = 40.0f;

        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private PlayerInventory _playerInventory;
        private float _cycleTimer = 0f;

        public int CurrentStoredWater => currentStoredWater;
        public int MaxStoredWater => maxStoredWater;
        public float CycleDuration
        {
            get
            {
                if (DayNightManager.Instance != null && DayNightManager.Instance.FullDayDuration > 0f)
                {
                    return DayNightManager.Instance.FullDayDuration / 2.0f; // 1 วันในเกม กรองได้ 2 ขวด
                }
                return purifyCycleDuration;
            }
        }
        public float ProductionProgress => Mathf.Clamp01(_cycleTimer / CycleDuration);

        // IPowerConsumer implementation
        public Vector3 Position => transform.position;
        public float PowerDemand => requiresPower ? powerDemand : 0f;
        public bool IsConsumerActive => CanInteract && (currentStoredWater < maxStoredWater);

        public float PowerEfficiency
        {
            get
            {
                if (!requiresPower) return 1.0f;
                return PowerGridManager.GetPowerEfficiency(transform.position, powerDemand);
            }
        }

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.6f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            float efficiency = PowerEfficiency;
            if (efficiency <= 0f)
            {
                return $"<color=yellow><b>[E]</b></color> Water Purifier <color=#ff3333>(OFFLINE - NO POWER ⚡)</color>";
            }

            if (currentStoredWater >= maxStoredWater)
            {
                return $"<color=yellow><b>[E]</b></color> Water Purifier <color=#00ff88>(FULL {currentStoredWater}/{maxStoredWater})</color>";
            }

            if (efficiency < 1.0f)
            {
                return $"<color=yellow><b>[E]</b></color> Water Purifier <color=#ffb700>(LOW POWER {(efficiency * 100f):F0}%)</color> [{(ProductionProgress * 100f):F0}%]";
            }

            if (currentStoredWater > 0)
            {
                return $"<color=yellow><b>[E]</b></color> Water Purifier ({currentStoredWater}/{maxStoredWater}) [{(ProductionProgress * 100f):F0}%]";
            }

            return $"<color=yellow><b>[E]</b></color> Water Purifier <color=#00e5ff>({(ProductionProgress * 100f):F0}%)</color>";
        }

        public void OnInteract()
        {
            // 1. เปิดผ่าน WaterPurifierHUD ทันที
            if (WaterPurifierHUD.Instance != null)
            {
                WaterPurifierHUD.Instance.OpenHUD(this);
                return;
            }

            // 2. กรณีไม่มี Singleton ให้ลองค้นหา GameObject WaterPurifier_HUD ในฉาก
            GameObject hudGo = GameObject.Find("WaterPurifier_HUD");
            if (hudGo != null)
            {
                WaterPurifierHUD hud = hudGo.GetComponent<WaterPurifierHUD>() ?? hudGo.AddComponent<WaterPurifierHUD>();
                hud.OpenHUD(this);
                return;
            }

            // 3. Fallback: หากหา HUD ไม่เจอ ให้เก็บน้ำโดยตรง
            if (currentStoredWater > 0)
            {
                CollectWater();
            }
            else
            {
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        $"Purifying water... {(ProductionProgress * 100f):F0}%"
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
            PowerGridManager.RegisterConsumer(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
            PowerGridManager.UnregisterConsumer(this);
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
                float efficiency = PowerEfficiency;
                if (efficiency <= 0f)
                {
                    // ไม่มีไฟฟ้าเลย! เครื่องหยุดนิ่งสนิท 0% ไม่มีการเดินรอบผลิต!
                    return;
                }

                // เดินรอบผลิตตามอัตราส่วนไฟฟ้าจริง (ไฟน้อยเดินช้า ไฟเต็มเดินความเร็วปกติ)
                _cycleTimer += Time.deltaTime * efficiency;
                if (_cycleTimer >= CycleDuration)
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
                            "+Water Bottle x1"
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
                            $"Clean Water Ready ({currentStoredWater}/{maxStoredWater})"
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

        public void CollectAllWater()
        {
            CollectWater();
        }

        public void CollectSingleWater()
        {
            if (currentStoredWater <= 0) return;

            if (_playerInventory == null)
            {
                FindPlayer();
                if (_playerInventory == null) return;
            }

            bool added = TryAddWaterToInventory(_playerInventory, 1);
            if (added)
            {
                currentStoredWater--;
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        "+Water Bottle x1"
                    );
                }
                Debug.Log($"[WaterPurifier] Collected 1 bottle. Remaining: {currentStoredWater}/{maxStoredWater}");
            }
            else
            {
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(
                        transform.position + Vector3.up * 1.8f,
                        0f,
                        false,
                        "Inventory Full! Clear space first"
                    );
                }
                Debug.LogWarning("[WaterPurifier] Inventory full! Cannot collect water.");
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
                        $"+Water Bottle x{toCollect}"
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
                        "Inventory Full! Clear space first"
                    );
                }
                Debug.LogWarning("[WaterPurifier] Inventory full! Cannot collect water.");
            }
        }
    }
}
