using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// WaterPurifierHUD — ควบคุมหน้าต่าง UI ของเครื่องกลั่นน้ำสะอาด (BUILD0012_WaterPurifierStation)
    /// คุณสมบัติ:
    /// - แสดงผลช่องน้ำ 3 ช่องตามความจุสูงสุด (Max 3 ขวดต่อเครื่อง)
    /// - แต่ละช่องจะแสดงสถานะ:
    ///   1. เสร็จแล้ว (Produced) -> แสดงไอคอน น้ำสะอาด x1 พร้อมปุ่ม [COLLECT] กดเก็บทีละขวดได้
    ///   2. กำลังกลั่น (In Production) -> แสดงเปอร์เซ็นต์ % ความคืบหน้า และเวลานับถอยหลัง
    ///   3. รอคิว (Waiting / Queued) -> แสดงสถานะรอคิวถัดไป
    /// - กล่อง Description Text แสดงข้อมูลสถิติของเครื่อง อัตราการผลิต และพลังงานไฟฟ้า (40 ⚡)
    /// - ปุ่ม [Take all] ด้านล่าง สำหรับเก็บน้ำที่ผลิตเสร็จทั้งหมดเข้ากระเป๋าในคลิกเดียว
    /// - ปุ่ม [Close] สีแดง หรือกด [ESC] หรือเดินห่างออกจากเครื่อง จะปิดหน้าต่างอัตโนมัติ
    /// </summary>
    public class WaterPurifierHUD : MonoBehaviour, IPopUp
    {
        public static WaterPurifierHUD Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<WaterPurifierHUD>();
                    if (_instance == null)
                    {
                        GameObject hudGo = GameObject.Find("WaterPurifier_HUD");
                        if (hudGo != null)
                        {
                            _instance = hudGo.AddComponent<WaterPurifierHUD>();
                        }
                    }
                }
                return _instance;
            }
        }
        private static WaterPurifierHUD _instance;

        [Header("UI Panels & Root")]
        [Tooltip("Root GameObject ของ WaterPurifier HUD")]
        [SerializeField] private GameObject hudRoot;

        [Tooltip("Panel หลัก (WaterPurifier_Panel)")]
        [SerializeField] private GameObject panelObject;

        [Tooltip("ปุ่มกากบาท/ย้อนกลับสีแดงด้านซ้ายบน")]
        [SerializeField] private Button closeButton;

        [Header("Slots Configuration")]
        [Tooltip("Container ที่บรรจุ InventoryItemSlot แถวทั้ง 3 ช่อง")]
        [SerializeField] private Transform slotsContainer;

        [Tooltip("รายการ Slot แถวทั้ง 3 ช่อง")]
        [SerializeField] private List<InventoryItemSlot> slotRows = new List<InventoryItemSlot>();

        [Header("Descriptions & Buttons")]
        [Tooltip("Text แสดงรายละเอียดเครื่องจักรใน DesPanel")]
        [SerializeField] private TMP_Text descriptionText;

        [Tooltip("ปุ่ม Take all ด้านล่าง")]
        [SerializeField] private Button takeAllButton;

        [Tooltip("Text บนปุ่ม Take all")]
        [SerializeField] private TMP_Text takeAllButtonLabel;

        [Tooltip("Text หัวข้อ (WaterPurifier)")]
        [SerializeField] private TMP_Text titleText;

        [Header("Item Data")]
        [SerializeField] private string cleanWaterItemName = "Water Bottle";

        private WaterPurifierBuilding _currentBuilding;
        private Sprite _waterIcon;
        private bool _isOpen = false;
        private float _refreshTimer = 0f;

        public bool IsOpen => _isOpen;
        public WaterPurifierBuilding CurrentBuilding => _currentBuilding;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            AutoBindReferences();

            // เริ่มต้นปิดซ่อนหน้าต่างไว้ก่อน
            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
            else if (panelObject != null)
            {
                panelObject.SetActive(false);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureHUDInScene()
        {
            GameObject hudGo = GameObject.Find("WaterPurifier_HUD");
            if (hudGo != null)
            {
                WaterPurifierHUD hud = hudGo.GetComponent<WaterPurifierHUD>();
                if (hud == null)
                {
                    hud = hudGo.AddComponent<WaterPurifierHUD>();
                }
                _instance = hud;
                hud.AutoBindReferences();
                hudGo.SetActive(false);
                Debug.Log("[WaterPurifierHUD] Auto-initialized and bound to WaterPurifier_HUD in scene.");
            }
        }

        private void Start()
        {
            AutoBindReferences();
            CacheWaterIcon();
        }

        /// <summary>
        /// ค้นหาและเชื่อมต่อ UI References อัตโนมัติในกรณีที่ไม่ได้ลากใน Inspector
        /// </summary>
        public void AutoBindReferences()
        {
            if (hudRoot == null)
            {
                hudRoot = gameObject;
            }

            if (panelObject == null)
            {
                Transform panelTrans = transform.Find("WaterPurifier_Panel");
                if (panelTrans != null) panelObject = panelTrans.gameObject;
                else panelObject = gameObject;
            }

            Transform searchRoot = panelObject != null ? panelObject.transform : transform;

            // 1. ปุ่ม Close
            if (closeButton == null)
            {
                Transform closeT = searchRoot.Find("Close");
                if (closeT != null)
                {
                    closeButton = closeT.GetComponent<Button>();
                }
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseHUD);
                closeButton.onClick.AddListener(CloseHUD);
            }

            // 2. Title Text
            if (titleText == null)
            {
                Transform resPanel = searchRoot.Find("ResultPanel");
                if (resPanel != null)
                {
                    Transform titleT = resPanel.Find("Title") ?? resPanel.Find("Text") ?? resPanel.Find("TitleText");
                    if (titleT != null) titleText = titleT.GetComponent<TMP_Text>();
                }
            }

            // 3. Slot Rows ใน ResultPanel -> Scroll View -> Viewport -> Content
            if (slotRows == null || slotRows.Count == 0)
            {
                slotRows = new List<InventoryItemSlot>();
                Transform resPanel = searchRoot.Find("ResultPanel");
                if (resPanel != null)
                {
                    Transform content = resPanel.Find("Scroll View/Viewport/Content");
                    if (content != null)
                    {
                        slotsContainer = content;
                        slotRows.AddRange(content.GetComponentsInChildren<InventoryItemSlot>(true));
                    }
                }
            }

            // 4. Description Text ใน DesPanel
            if (descriptionText == null)
            {
                Transform desPanel = searchRoot.Find("DesPanel");
                if (desPanel != null)
                {
                    descriptionText = desPanel.GetComponentInChildren<TMP_Text>(true);
                }
            }

            // 5. ปุ่ม Take all ใน LootBot
            if (takeAllButton == null)
            {
                Transform lootBot = searchRoot.Find("LootBot");
                if (lootBot != null)
                {
                    takeAllButton = lootBot.GetComponentInChildren<Button>(true);
                    if (takeAllButton != null)
                    {
                        takeAllButtonLabel = takeAllButton.GetComponentInChildren<TMP_Text>(true);
                    }
                }
            }
            if (takeAllButton != null)
            {
                takeAllButton.onClick.RemoveListener(OnTakeAllClicked);
                takeAllButton.onClick.AddListener(OnTakeAllClicked);
            }
        }

        private void CacheWaterIcon()
        {
            if (_waterIcon != null) return;
            if (ItemDatabase.Instance != null)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(cleanWaterItemName);
                if (item != null && item.icon != null)
                {
                    _waterIcon = item.icon;
                    return;
                }
                item = ItemDatabase.Instance.GetItemByName("ITEM0091_WaterBottle");
                if (item != null && item.icon != null)
                {
                    _waterIcon = item.icon;
                }
            }
        }

        /// <summary>
        /// เปิดหน้าต่าง WaterPurifier HUD สำหรับเครื่องที่กำลัง Interact
        /// </summary>
        public void OpenHUD(WaterPurifierBuilding building)
        {
            if (building == null) return;

            _currentBuilding = building;
            _isOpen = true;

            AutoBindReferences();
            CacheWaterIcon();

            if (hudRoot != null) hudRoot.SetActive(true);
            if (panelObject != null) panelObject.SetActive(true);

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }

            RefreshUI(true);
            Debug.Log($"[WaterPurifierHUD] Opened for {building.name}. Stored: {building.CurrentStoredWater}/{building.MaxStoredWater}");
        }

        /// <summary>
        /// ปิดหน้าต่าง WaterPurifier HUD
        /// </summary>
        public void CloseHUD()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _currentBuilding = null;

            if (hudRoot != null) hudRoot.SetActive(false);
            if (panelObject != null) panelObject.SetActive(false);

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }

            Debug.Log("[WaterPurifierHUD] Closed.");
        }

        // IPopUp implementation
        public void ClosePopUp()
        {
            CloseHUD();
        }

        private void Update()
        {
            if (!_isOpen) return;

            // 1. ตรวจสอบว่าสิ่งปลูกสร้างยังอยู่และไม่พัง
            if (_currentBuilding == null || !_currentBuilding.CanInteract)
            {
                CloseHUD();
                return;
            }

            // 2. ตรวจสอบระยะห่างผู้เล่น ถ้าเดินห่างเกินระยะ ให้ปิด HUD
            PlayerController pc = PlayerController.Instance != null ? PlayerController.Instance : UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                float dist = Vector3.Distance(pc.transform.position, _currentBuilding.transform.position);
                if (dist > _currentBuilding.InteractionDistance + 1.2f)
                {
                    CloseHUD();
                    return;
                }
            }

            // 3. อัปเดต Progress แบบ Realtime
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.1f)
            {
                _refreshTimer = 0f;
                RefreshUI(false);
            }
        }

        /// <summary>
        /// อัปเดตและแสดงผลข้อมูล Slot ทั้ง 3 ช่องและกล่องข้อความ
        /// </summary>
        public void RefreshUI(bool fullRefresh = false)
        {
            if (_currentBuilding == null) return;

            int storedCount = Mathf.Clamp(_currentBuilding.CurrentStoredWater, 0, _currentBuilding.MaxStoredWater);
            int maxSlots = Mathf.Min(3, _currentBuilding.MaxStoredWater);
            float progress = _currentBuilding.ProductionProgress;
            float remainingSeconds = Mathf.Max(0f, _currentBuilding.CycleDuration * (1f - progress));

            // ตรวจสอบพลังงานไฟฟ้าในพื้นที่
            bool isPowered = GeneratorBuilding.IsPositionPowered(_currentBuilding.transform.position);
            string powerStatus = isPowered 
                ? "<color=#00e5ff><b>⚡ GRID POWERED (40W) — 100% Speed</b></color>"
                : "<color=#ffb700><b>⚠️ BACKUP BATTERY — Working Normal</b></color>";

            // 1. อัปเดตกล่องข้อความ Description
            if (descriptionText != null)
            {
                string statusLine;
                if (storedCount >= maxSlots)
                {
                    statusLine = "<color=#00ff88><b>STORAGE FULL (3/3) — Collect water to resume</b></color>";
                }
                else
                {
                    statusLine = $"Purifying Bottle #{storedCount + 1}... <color=#00e5ff><b>{(progress * 100f):F0}%</b></color> ({remainingSeconds:F0}s left)";
                }

                descriptionText.text = 
                    $"<b>WATER PURIFIER STATION</b>\n" +
                    $"Status: {statusLine}\n" +
                    $"Storage Capacity: <b>{storedCount} / {maxSlots} Bottles</b>  |  Cycle: 1 Bottle / {_currentBuilding.CycleDuration:F0}s\n" +
                    $"Power Grid: {powerStatus}\n" +
                    $"<i>Click [COLLECT] on each slot or press [Take all] below to collect clean water into inventory.</i>";
            }

            // 2. อัปเดต Slot ทั้ง 3 ช่อง
            for (int i = 0; i < slotRows.Count && i < maxSlots; i++)
            {
                InventoryItemSlot slot = slotRows[i];
                if (slot == null) continue;

                int slotIndex = i;

                if (slotIndex < storedCount)
                {
                    // === ช่องที่ผลิตเสร็จแล้ว (READY TO COLLECT) ===
                    slot.gameObject.SetActive(true);
                    slot.SetData(
                        itemName: "Water Bottle",
                        quantity: 1,
                        icon: _waterIcon,
                        isEquipped: false,
                        onClickUse: (name) => OnCollectSingleSlot(slotIndex),
                        customButtonText: "COLLECT",
                        customQuantityText: "x1"
                    );
                }
                else if (slotIndex == storedCount && storedCount < maxSlots)
                {
                    // === ช่องที่กำลังกลั่นอยู่ (IN PRODUCTION) ===
                    slot.gameObject.SetActive(true);
                    string progText = $"{(progress * 100f):F0}%";
                    string timeText = $"{remainingSeconds:F0}s";

                    slot.SetData(
                        itemName: $"Purifying... ({progText})",
                        quantity: 1,
                        icon: _waterIcon,
                        isEquipped: false,
                        onClickUse: null, // ปิดปุ่มกด
                        customButtonText: "PURIFYING",
                        customQuantityText: timeText
                    );
                }
                else
                {
                    // === ช่องที่ว่าง / รอคิว (QUEUED / EMPTY) ===
                    slot.gameObject.SetActive(true);
                    slot.SetData(
                        itemName: $"Water Slot #{slotIndex + 1} (Waiting)",
                        quantity: 0,
                        icon: _waterIcon,
                        isEquipped: false,
                        onClickUse: null,
                        customButtonText: "WAITING",
                        customQuantityText: "-"
                    );
                }
            }

            // 3. ปรับสถานะปุ่ม Take all
            if (takeAllButton != null)
            {
                bool canTake = storedCount > 0;
                takeAllButton.interactable = canTake;
                if (takeAllButtonLabel != null)
                {
                    takeAllButtonLabel.text = canTake ? $"Take all ({storedCount})" : "Take all (0)";
                }
            }
        }

        /// <summary>
        /// เมื่อผู้เล่นกดปุ่ม COLLECT ที่ช่องใดช่องหนึ่ง
        /// </summary>
        private void OnCollectSingleSlot(int slotIndex)
        {
            if (_currentBuilding == null) return;
            _currentBuilding.CollectSingleWater();
            RefreshUI(true);
        }

        /// <summary>
        /// เมื่อผู้เล่นกดปุ่ม Take all
        /// </summary>
        private void OnTakeAllClicked()
        {
            if (_currentBuilding == null) return;
            _currentBuilding.CollectAllWater();
            RefreshUI(true);
        }
    }
}
