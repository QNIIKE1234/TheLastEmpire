using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// PowerGridHUD — ควบคุมหน้าต่างจัดการโครงข่ายพลังงานไฟฟ้าประจำฐาน (Base Power Grid System)
    /// คุณสมบัติ:
    /// - แสดงสถานะกำลังผลิตไฟฟ้ารวม (Total Supply) และความต้องการไฟฟ้ารวม (Total Demand)
    /// - หลอดเกจพลังงาน Slider ปรับค่าและสีตามสัดส่วนการใช้พลังงาน (ปกติ: สีทอง/ส้ม, ขาดแคลน: สีแดง)
    /// - แสดงสัญลักษณ์และประสิทธิภาพ (%) ของสิ่งปลูกสร้างแต่ละชนิดในฐาน (ป้อมปืน, หอสายฟ้า, เครื่องผลิตน้ำ ฯลฯ)
    /// - กล่อง Description ด้านล่างสรุปข้อมูลโครงข่าย และแสดงรายละเอียดเมื่อคลิกเลือกแต่ละอุปกรณ์
    /// - ปุ่ม [BACK] / [ESC] ปิดหน้าต่างได้อย่างลื่นไหล รองรับ IPopUp
    /// </summary>
    public class PowerGridHUD : MonoBehaviour, IPopUp
    {
        public static PowerGridHUD Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<PowerGridHUD>();
                    if (_instance == null)
                    {
                        GameObject hudGo = GameObject.Find("PowerGrid_HUD");
                        if (hudGo != null)
                        {
                            _instance = hudGo.AddComponent<PowerGridHUD>();
                        }
                    }
                }
                return _instance;
            }
        }
        private static PowerGridHUD _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureHUD()
        {
            if (_instance == null)
            {
                _instance = UnityEngine.Object.FindFirstObjectByType<PowerGridHUD>();
                if (_instance == null)
                {
                    GameObject hudGo = GameObject.Find("PowerGrid_HUD");
                    if (hudGo != null)
                    {
                        _instance = hudGo.AddComponent<PowerGridHUD>();
                        Debug.Log("[PowerGridHUD] Auto-attached to PowerGrid_HUD on scene load.");
                    }
                }
            }
        }

        [Header("UI Panels & Root")]
        [Tooltip("Root GameObject ของ PowerGrid HUD")]
        [SerializeField] private GameObject hudRoot;

        [Tooltip("Panel หลัก (PowerGrid__Panel)")]
        [SerializeField] private GameObject panelObject;

        [Tooltip("ปุ่มกากบาท/ย้อนกลับสีแดง (Close)")]
        [SerializeField] private Button closeButton;

        [Header("Power Gauge Bar")]
        [Tooltip("Slider หลอดวัดปริมาณการใช้ไฟฟ้า")]
        [SerializeField] private Slider powerSlider;

        [Tooltip("Text แสดงตัวเลข Supply / Demand บน Slider เช่น 500 / 500")]
        [SerializeField] private TMP_Text amountText;

        [Tooltip("Image หลอด Fill สำหรับเปลี่ยนสีตามสถานะ")]
        [SerializeField] private Image sliderFillImage;

        [Header("Descriptions & Titles")]
        [Tooltip("Text แสดงรายละเอียดที่ด้านล่าง (Des)")]
        [SerializeField] private TMP_Text descriptionText;

        [Tooltip("Text หัวข้อ (Title_Text)")]
        [SerializeField] private TMP_Text titleText;

        [Header("Grid Items Container")]
        [Tooltip("Content ใน Scroll View ที่บรรจุ GridItemTtumbnai")]
        [SerializeField] private Transform contentContainer;

        [System.Serializable]
        public class GridItemEntry
        {
            [Tooltip("ลาก Prefab ของสิ่งปลูกสร้างชิ้นนี้มาใส่ (ระบบจะจับคู่เช็คในฉากและอัปเดตไอคอนให้อัตโนมัติ)")]
            public GameObject buildingPrefab;

            [Tooltip("หรือลาก BuildingData ScriptableObject มาใส่ (ระบบจะดึงชื่อ ไอคอน Prefab และคำอธิบายให้อัตโนมัติ)")]
            public BuildingData buildingData;

            [Header("Overrides & Custom Settings (เว้นว่างได้ ระบบจะดึงจาก Prefab / Data ให้)")]
            public string buildingId;
            public string buildingName;
            public Sprite customIcon;
            public float powerWatt;
            public bool isProducer;
            [TextArea(2, 3)]
            public string description;

            [HideInInspector] public GameObject rootObject;
            [HideInInspector] public Image iconImage;
            [HideInInspector] public TMP_Text percentText;
            [HideInInspector] public Button button;

            public string DisplayName
            {
                get
                {
                    if (!string.IsNullOrEmpty(buildingName)) return buildingName;
                    if (buildingData != null && !string.IsNullOrEmpty(buildingData.buildingName)) return buildingData.buildingName;
                    if (buildingPrefab != null) return CleanName(buildingPrefab.name);
                    return "Building";
                }
            }

            public Sprite DisplayIcon
            {
                get
                {
                    if (customIcon != null) return customIcon;
                    if (buildingData != null && buildingData.icon != null) return buildingData.icon;
                    return null;
                }
            }

            public string DisplayDescription
            {
                get
                {
                    if (!string.IsNullOrEmpty(description)) return description;
                    if (buildingData != null && !string.IsNullOrEmpty(buildingData.description)) return buildingData.description;
                    return "Base infrastructure equipment.";
                }
            }
        }

        [Header("Building Grid Configurations")]
        [SerializeField] private List<GridItemEntry> gridEntries = new List<GridItemEntry>();

        private bool _isOpen = false;
        private int _selectedItemIndex = -1;
        private float _refreshTimer = 0f;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            SetupDefaultGridEntries();
            AutoBindReferences();
        }

        private void Reset()
        {
            gridEntries = null;
            SetupDefaultGridEntries();
            AutoBindReferences();
        }

        [ContextMenu("Populate Default Grid Entries")]
        public void PopulateDefaults()
        {
            gridEntries = null;
            SetupDefaultGridEntries();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (gridEntries == null || gridEntries.Count == 0)
            {
                SetupDefaultGridEntries();
            }

            // ซิงค์รูปไอคอนและชื่อทันทีเมื่อผู้ใช้ลาก Prefab หรือ BuildingData มาใส่ใน Inspector
            AutoBindReferences();
        }
#endif

        private void Start()
        {
            AutoBindReferences();
            if (panelObject != null && !_isOpen)
            {
                panelObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            PowerGridManager.OnGridChanged += HandleGridChanged;
        }

        private void OnDisable()
        {
            PowerGridManager.OnGridChanged -= HandleGridChanged;
        }

        private void HandleGridChanged()
        {
            if (_isOpen)
            {
                RefreshUI();
            }
        }

        private void SetupDefaultGridEntries()
        {
            if (gridEntries != null && gridEntries.Count > 0) return;

            gridEntries = new List<GridItemEntry>
            {
                new GridItemEntry
                {
                    buildingId = "BUILD0014",
                    buildingName = "Sanity Beacon",
                    powerWatt = 15f,
                    isProducer = false,
                    description = "Lantern beacon restoring survivor Sanity (+2.5/s). Requires 15W continuous power."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0021",
                    buildingName = "Sentry Turret",
                    powerWatt = 30f,
                    isProducer = false,
                    description = "Automated 360-degree defense cannon. Consumes 30W. Receives +25% fire rate when fully powered."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0022",
                    buildingName = "Shock Tower",
                    powerWatt = 45f,
                    isProducer = false,
                    description = "High-voltage Xenocrystal chain lightning tower hitting up to 4 targets. Requires 45W."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0015",
                    buildingName = "Fusion Generator",
                    powerWatt = 60f,
                    isProducer = true,
                    description = "Compact fusion reactor supplying +60W clean electrical power across a 20-meter radius."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0008",
                    buildingName = "Motorized Blast Door",
                    powerWatt = 10f,
                    isProducer = false,
                    description = "Heavy reinforced motorized security door. Powered locking and hydraulic servos consume 10W."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0007",
                    buildingName = "Armored Wall Panel",
                    powerWatt = 0f,
                    isProducer = false,
                    description = "Heavy titanium-reinforced wall barricade protecting base perimeter. Passive armor structure (0W)."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0010",
                    buildingName = "Survivor Workbench",
                    powerWatt = 0f,
                    isProducer = false,
                    description = "Survivor crafting workbench for tools and survival equipment. Operates manually without electricity (0W)."
                },
                new GridItemEntry
                {
                    buildingId = "BUILD0017",
                    buildingName = "Razor Wire Barricade",
                    powerWatt = 0f,
                    isProducer = false,
                    description = "Barricade with circular serrated blades and barbed wire shredding advancing enemies. Physical trap (0W)."
                }
            };
        }

        /// <summary>
        /// ค้นหาและเชื่อมโยง UI References อัตโนมัติในฉาก
        /// </summary>
        public void AutoBindReferences()
        {
            if (hudRoot == null)
            {
                hudRoot = gameObject;
            }

            if (panelObject == null)
            {
                Transform panelT = transform.Find("PowerGrid__Panel") ?? transform.Find("PowerGrid_Panel");
                if (panelT != null) panelObject = panelT.gameObject;
                else panelObject = gameObject;
            }

            Transform searchRoot = panelObject != null ? panelObject.transform : transform;

            // 1. ปุ่ม Close
            if (closeButton == null)
            {
                Transform closeT = searchRoot.Find("Close");
                if (closeT != null) closeButton = closeT.GetComponent<Button>();
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseHUD);
                closeButton.onClick.AddListener(CloseHUD);
            }

            // 2. GridPanel
            Transform gridPanel = searchRoot.Find("GridPanel") ?? searchRoot;

            // Title
            if (titleText == null)
            {
                Transform titleT = gridPanel.Find("Title/Title_Text") ?? gridPanel.Find("Title");
                if (titleT != null) titleText = titleT.GetComponent<TMP_Text>();
            }

            // Slider & Amount Text
            if (powerSlider == null)
            {
                Transform sliderT = gridPanel.Find("Slider");
                if (sliderT != null)
                {
                    powerSlider = sliderT.GetComponent<Slider>();
                    Transform fillT = sliderT.Find("Fill Area/Fill");
                    if (fillT != null) sliderFillImage = fillT.GetComponent<Image>();
                    Transform amountT = sliderT.Find("Amount");
                    if (amountT != null) amountText = amountT.GetComponent<TMP_Text>();
                }
            }

            // Description Text
            if (descriptionText == null)
            {
                Transform desT = gridPanel.Find("Des");
                if (desT != null) descriptionText = desT.GetComponent<TMP_Text>();
            }

            // Content Container
            if (contentContainer == null)
            {
                Transform contentT = gridPanel.Find("Scroll View/Viewport/Content");
                if (contentT != null) contentContainer = contentT;
            }

            // เชื่อมโยงแต่ละ GridItemTtumbnai
            if (contentContainer != null)
            {
                int childCount = contentContainer.childCount;
                for (int i = 0; i < gridEntries.Count; i++)
                {
                    if (i < childCount)
                    {
                        Transform itemT = contentContainer.GetChild(i);
                        GridItemEntry entry = gridEntries[i];
                        entry.rootObject = itemT.gameObject;

                        Transform iconT = itemT.Find("Icon");
                        if (iconT != null)
                        {
                            entry.iconImage = iconT.GetComponent<Image>();
                            // อัปเดตรูปไอคอนให้ตรงกับ Prefab หรือ BuildingData โดยอัตโนมัติ
                            Sprite spr = entry.DisplayIcon;
                            if (spr != null && entry.iconImage != null)
                            {
                                entry.iconImage.sprite = spr;
                            }
                        }

                        // ถ้าระบุ buildingPrefab ไว้ และยังไม่ได้กำหนดค่าวัตต์/ประเภทผลิตไฟ ให้ดึงจาก Component อัตโนมัติ
                        if (entry.buildingPrefab != null)
                        {
                            if (entry.buildingData == null)
                            {
                                BuildingObject bo = entry.buildingPrefab.GetComponent<BuildingObject>();
                                if (bo != null && bo.Data != null) entry.buildingData = bo.Data;
                            }

                            if (entry.powerWatt == 0f && !entry.isProducer)
                            {
                                GeneratorBuilding gen = entry.buildingPrefab.GetComponent<GeneratorBuilding>();
                                if (gen != null)
                                {
                                    entry.isProducer = true;
                                    entry.powerWatt = gen.PowerOutput > 0 ? gen.PowerOutput : 60f;
                                }
                                else
                                {
                                    IPowerConsumer consumer = entry.buildingPrefab.GetComponent<IPowerConsumer>();
                                    if (consumer != null)
                                    {
                                        entry.powerWatt = consumer.PowerDemand;
                                    }
                                }
                            }
                        }

                        Transform txtT = itemT.Find("TextPercant");
                        if (txtT != null) entry.percentText = txtT.GetComponent<TMP_Text>();

                        entry.button = itemT.GetComponent<Button>();
                        if (entry.button == null)
                        {
                            entry.button = itemT.gameObject.AddComponent<Button>();
                        }

                        int capturedIndex = i;
                        entry.button.onClick.RemoveAllListeners();
                        entry.button.onClick.AddListener(() => OnSelectItem(capturedIndex));
                    }
                }
            }
        }

        /// <summary>
        /// เปิดหน้าต่าง Power Grid HUD
        /// </summary>
        public void OpenHUD()
        {
            _isOpen = true;
            _selectedItemIndex = -1;

            AutoBindReferences();

            if (hudRoot != null) hudRoot.SetActive(true);
            if (panelObject != null) panelObject.SetActive(true);

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            RefreshUI();
            Debug.Log("[PowerGridHUD] Opened Power Grid UI.");
        }

        /// <summary>
        /// ปิดหน้าต่าง Power Grid HUD
        /// </summary>
        public void CloseHUD()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _selectedItemIndex = -1;

            if (panelObject != null) panelObject.SetActive(false);

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }

            // ในเกมมุมมอง Top-down เมาส์ต้องเปิดแสดงและไม่ล็อกเสมอ
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Debug.Log("[PowerGridHUD] Closed Power Grid UI.");
        }

        // IPopUp implementation
        public void ClosePopUp()
        {
            CloseHUD();
        }

        private void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame && _isOpen)
                {
                    CloseHUD();
                    return;
                }

                // ปุ่มลัด [P] (Power) สำหรับเปิด/ปิดหน้าต่างโครงข่ายไฟฟ้าได้สะดวก
                if (Keyboard.current.pKey.wasPressedThisFrame)
                {
                    if (_isOpen)
                    {
                        CloseHUD();
                        return;
                    }
                    else
                    {
                        OpenHUD();
                        return;
                    }
                }
            }

            if (!_isOpen) return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.25f)
            {
                _refreshTimer = 0f;
                RefreshUI();
            }
        }

        private void OnSelectItem(int index)
        {
            if (_selectedItemIndex == index)
            {
                _selectedItemIndex = -1; // toggle off selection
            }
            else
            {
                _selectedItemIndex = index;
            }
            RefreshUI();
        }

        /// <summary>
        /// รีเฟรชข้อมูลเกจและสถานะสิ่งปลูกสร้างทั้งหมด
        /// </summary>
        public void RefreshUI()
        {
            float supply = PowerGridManager.TotalSupply;
            float demand = PowerGridManager.TotalDemand;
            float eff = PowerGridManager.GlobalEfficiency;
            int activeGens = PowerGridManager.ActiveGeneratorsCount;
            int activeConsumers = PowerGridManager.ActiveConsumersCount;

            // 1. อัปเดต Slider และ Amount Text
            if (powerSlider != null)
            {
                powerSlider.minValue = 0f;
                if (supply > 0f)
                {
                    powerSlider.maxValue = Mathf.Max(supply, demand);
                    powerSlider.value = demand;
                }
                else
                {
                    powerSlider.maxValue = 100f;
                    powerSlider.value = 0f;
                }
            }

            if (amountText != null)
            {
                if (supply <= 0f && demand <= 0f)
                {
                    amountText.text = "0 / 0 W  <color=#FF5555>[NO GENERATOR]</color>";
                }
                else if (supply <= 0f)
                {
                    amountText.text = $"0 / {demand:F0} W  <color=#FF3333>[BLACKOUT - 0%]</color>";
                }
                else
                {
                    float loadPercent = (demand / supply) * 100f;
                    if (demand <= supply)
                    {
                        amountText.text = $"{demand:F0} / {supply:F0} W  ({loadPercent:F0}% LOAD)";
                    }
                    else
                    {
                        amountText.text = $"{demand:F0} / {supply:F0} W  <color=#FF5555>({loadPercent:F0}% OVERLOAD)</color>";
                    }
                }
            }

            // ปรับสีหลอด Slider
            if (sliderFillImage != null)
            {
                if (supply <= 0f)
                {
                    sliderFillImage.color = new Color(0.4f, 0.4f, 0.4f, 1f); // Dark Gray
                }
                else if (demand > supply)
                {
                    sliderFillImage.color = new Color(0.95f, 0.25f, 0.2f, 1f); // Red Overload
                }
                else if (demand / supply > 0.85f)
                {
                    sliderFillImage.color = new Color(1.0f, 0.65f, 0.1f, 1f); // Orange Near Full
                }
                else
                {
                    sliderFillImage.color = new Color(1.0f, 0.72f, 0.15f, 1f); // Golden Yellow Default
                }
            }

            // 2. อัปเดตรายการสิ่งปลูกสร้างแต่ละช่อง (Grid Items)
            for (int i = 0; i < gridEntries.Count; i++)
            {
                GridItemEntry entry = gridEntries[i];
                if (entry.percentText == null) continue;

                int placedCount = CountPlacedBuildings(entry);

                if (placedCount <= 0)
                {
                    entry.percentText.text = "<color=#777777>0 BUILT</color>";
                }
                else if (entry.isProducer)
                {
                    entry.percentText.text = activeGens > 0 
                        ? $"<color=#00FF66>100%</color>" 
                        : $"<color=#FF4444>OFF</color>";
                }
                else if (entry.powerWatt <= 0f)
                {
                    // สิ่งปลูกสร้างที่ไม่ต้องใช้ไฟฟ้า (เช่น โต๊ะคราฟต์) จะทำงาน 100% เสมอเมื่อสร้างแล้ว
                    entry.percentText.text = "<color=#00FF66>100%</color>";
                }
                else
                {
                    if (eff <= 0f)
                    {
                        entry.percentText.text = "<color=#FF3333>OFFLINE</color>";
                    }
                    else if (eff < 1f)
                    {
                        entry.percentText.text = $"<color=#FFB800>{eff * 100f:F0}%</color>";
                    }
                    else
                    {
                        entry.percentText.text = "<color=#00FF66>100%</color>";
                    }
                }
            }

            // 3. อัปเดตข้อความ Description Text ด้านล่าง
            if (descriptionText != null)
            {
                if (_selectedItemIndex >= 0 && _selectedItemIndex < gridEntries.Count)
                {
                    GridItemEntry sel = gridEntries[_selectedItemIndex];
                    int count = CountPlacedBuildings(sel);
                    string statusStr;

                    if (count <= 0)
                    {
                        statusStr = "<color=#999999>Not Constructed</color>";
                    }
                    else if (sel.isProducer)
                    {
                        statusStr = activeGens > 0 ? "<color=#00FF66>OPERATIONAL</color>" : "<color=#FF4444>OFFLINE</color>";
                    }
                    else if (sel.powerWatt <= 0f)
                    {
                        statusStr = "<color=#00FF66>OPERATIONAL (No Power Required)</color>";
                    }
                    else
                    {
                        statusStr = eff <= 0f ? "<color=#FF3333>OFFLINE (NO POWER)</color>" 
                                  : eff < 1f ? $"<color=#FFB800>BROWNOUT ({eff * 100f:F0}%)</color>" 
                                  : "<color=#00FF66>NORMAL (100%)</color>";
                    }

                    string spec;
                    if (sel.isProducer)
                    {
                        spec = $"Output: <color=#00FF66>+{sel.powerWatt:F0}W</color> per unit (Total: +{count * sel.powerWatt:F0}W)";
                    }
                    else if (sel.powerWatt <= 0f)
                    {
                        spec = "Power Demand: <color=#00FF66>None (0W - Manual Operation)</color>";
                    }
                    else
                    {
                        spec = $"Demand: <color=#FFB800>-{sel.powerWatt:F0}W</color> per unit (Total: -{count * sel.powerWatt:F0}W)";
                    }

                    descriptionText.text = $"<b>[{sel.DisplayName}]</b>  —  Active: {count} unit(s)  |  Status: {statusStr}\n{spec}\n{sel.DisplayDescription}";
                }
                else
                {
                    // โหมดภาพรวมโครงข่าย (Default Grid Overview)
                    string gridStatus;
                    if (supply <= 0f && demand <= 0f)
                    {
                        gridStatus = "<color=#AAAAAA>IDLE (NO GENERATOR PLACED)</color>";
                    }
                    else if (supply <= 0f)
                    {
                        gridStatus = "<color=#FF3333>BLACKOUT — NO POWER SUPPLY ⚡</color>";
                    }
                    else if (demand > supply)
                    {
                        gridStatus = $"<color=#FF5555>OVERLOADED — INSUFFICIENT POWER ({eff * 100f:F0}% SPEED) ⚡</color>";
                    }
                    else
                    {
                        gridStatus = "<color=#00FF66>GRID STABLE — ALL SYSTEMS NOMINAL ⚡</color>";
                    }

                    descriptionText.text = 
                        $"<b>[BASE POWER GRID OVERVIEW]</b>  Status: {gridStatus}\n" +
                        $"Active Generators: <b>{activeGens}</b> (Supply: <color=#00FF66>+{supply:F0} W</color>)   |   " +
                        $"Power Consumers: <b>{activeConsumers}</b> (Demand: <color=#FFB800>-{demand:F0} W</color>)\n" +
                        $"Overall Efficiency: <b>{eff * 100f:F0}%</b>   |   Click any item above for detailed specifications.";
                }
            }
        }

        private int CountPlacedBuildings(GridItemEntry entry)
        {
            if (entry == null) return 0;
            int count = 0;
            BuildingObject[] all = UnityEngine.Object.FindObjectsByType<BuildingObject>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                BuildingObject b = all[i];
                if (b == null || b.IsDead) continue;

                // 1. เทียบตรงกับ BuildingData (แม่นยำที่สุด)
                if (entry.buildingData != null && b.Data == entry.buildingData)
                {
                    count++;
                    continue;
                }

                // 2. เทียบตรงกับ buildingPrefab (ถ้าผู้ใช้ลาก Prefab มาใส่)
                if (entry.buildingPrefab != null)
                {
                    if (b.Data != null && b.Data.prefab == entry.buildingPrefab)
                    {
                        count++;
                        continue;
                    }

                    string targetPName = CleanName(entry.buildingPrefab.name);
                    if (b.gameObject.name.IndexOf(targetPName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                        continue;
                    }
                }

                // 3. เทียบจาก ID
                if (!string.IsNullOrEmpty(entry.buildingId))
                {
                    if (b.Data != null && !string.IsNullOrEmpty(b.Data.buildingId) &&
                        b.Data.buildingId.IndexOf(entry.buildingId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                        continue;
                    }

                    if (b.gameObject.name.IndexOf(entry.buildingId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                        continue;
                    }
                }

                // 4. เทียบจากชื่อ DisplayName
                string dName = entry.DisplayName;
                if (!string.IsNullOrEmpty(dName))
                {
                    if (b.Data != null && !string.IsNullOrEmpty(b.Data.buildingName) &&
                        b.Data.buildingName.IndexOf(dName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                        continue;
                    }

                    string cleanDName = CleanName(dName).Replace(" ", "");
                    if (b.gameObject.name.IndexOf(cleanDName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw.Replace("(Clone)", "").Trim();
            return s;
        }
    }
}
