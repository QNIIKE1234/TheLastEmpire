using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// CraftController — ควบคุมหน้าต่างระบบคราฟต์ (Crafting UI) ทั้งหมด
    /// รองรับ:
    /// - 7 หมวดหมู่ (Building, Utility, Defense, Weapons, Consumables, Tools, Special)
    /// - ช่องค้นหาชื่อไอเทม (Search InputField)
    /// - ดึง CraftThumbnail ลูกใน Container ด้วย GetComponent แล้วเซตข้อมูลผ่าน SetData()
    /// - ระบบแบ่งหน้า (Pagination < 1 > หน้าละ 6 แถวตามรูป UI)
    /// - หักวัตถุดิบสูงสุด 5 ชนิดจาก PlayerInventory และเพิ่มไอเทมที่คราฟต์เข้ากระเป๋าทันที
    /// </summary>
    public class CraftController : MonoBehaviour, IPopUp
    {
        public static CraftController Instance { get; private set; }

        [Header("Database Reference")]
        [Tooltip("อ้างอิง CraftingDatabase (หากเว้นว่างจะดึงจาก CraftingDatabase.Instance อัตโนมัติ)")]
        [SerializeField] private CraftingDatabase craftingDatabase;

        [Header("Main UI References")]
        [Tooltip("GameObject หน้าต่างหลักของระบบคราฟต์ (ใช้เปิด/ปิดหน้าต่าง)")]
        [SerializeField] private GameObject craftPanel;

        [Tooltip("ช่องค้นหาชื่อไอเทมมุมซ้ายบน (Search)")]
        [SerializeField] private TMP_InputField searchInputField;

        [Tooltip("ปุ่มกากบาทหรือปุ่มปิดหน้าต่างคราฟต์ (Optional)")]
        [SerializeField] private Button closeButton;

        [Header("Category Buttons (แถบหมวดหมู่ฝั่งซ้าย 7 หมวด)")]
        [SerializeField] private Button buildingTabButton;
        [SerializeField] private Button utilityTabButton;
        [SerializeField] private Button defenseTabButton;
        [SerializeField] private Button weaponsTabButton;
        [SerializeField] private Button consumablesTabButton;
        [SerializeField] private Button toolsTabButton;
        [SerializeField] private Button specialTabButton;

        [Header("Category Tab Sprites & Colors (สลับรูปและสีข้อความตอนเลือกหมวดหมู่)")]
        [Tooltip("Sprite ตอนที่ปุ่มหมวดหมู่ถูกเลือก (เช่น กรอบสีเหลืองตามรูป)")]
        [SerializeField] private Sprite selectedTabSprite;

        [Tooltip("Sprite ตอนที่ปุ่มหมวดหมู่ไม่ได้เลือก (เช่น กรอบสีดำตามรูป)")]
        [SerializeField] private Sprite unselectedTabSprite;

        [Tooltip("สีข้อความ TextMeshPro ตอนถูกเลือก (ค่าเริ่มต้น: สีดำ)")]
        [SerializeField] private Color selectedTabTextColor = Color.black;

        [Tooltip("สีข้อความ TextMeshPro ตอนไม่ได้เลือก (ค่าเริ่มต้น: สีขาว)")]
        [SerializeField] private Color unselectedTabTextColor = Color.white;

        [Header("Thumbnail Slots (รายการแถวคราฟต์ฝั่งขวา)")]
        [Tooltip("ลากแถว CraftThumbnail ทั้งหมดที่วางอยู่ในหน้าต่าง UI ใส่ที่นี่ (เช่น 6 แถว) เพื่อวนเซตข้อมูลซ้ำ ไม่สร้างใหม่ ไม่เปลือง Memory")]
        [SerializeField] private List<CraftThumbnail> thumbnailSlots = new List<CraftThumbnail>();

        [Tooltip("Transform พ่อที่มีแถว CraftThumbnail เป็นลูก (Optional: หากไม่ลาก thumbnailSlots ระบบจะดึงลูกจาก Container นี้ให้อัตโนมัติ)")]
        [SerializeField] private Transform thumbnailContainer;

        [Header("Pagination (ระบบเปลี่ยนหน้าด้านล่าง < 99 >)")]
        [Tooltip("จำนวนแถวที่แสดงต่อ 1 หน้า (ถ้าปล่อยเป็น 0 หรือไม่ตรง จะอิงตามจำนวน thumbnailSlots อัตโนมัติ)")]
        [SerializeField] private int itemsPerPage = 6;

        [Tooltip("ปุ่มลูกศรย้อนกลับหน้าก่อนหน้า (<)")]
        [SerializeField] private Button prevPageButton;

        [Tooltip("ปุ่มลูกศรไปหน้าถัดไป (>)")]
        [SerializeField] private Button nextPageButton;

        [Tooltip("TMP_Text แสดงเลขหน้าปัจจุบันตรงกลางปุ่มลูกศร")]
        [SerializeField] private TMP_Text pageNumberText;

        [Header("Input Settings")]
        [Tooltip("เปิดให้กดปุ่มลัดคีย์บอร์ดเพื่อเปิด/ปิดหน้าต่างคราฟต์")]
        [SerializeField] private bool enableShortcutKey = true;

        [Tooltip("ปุ่มลัดสำหรับเปิด/ปิดหน้าต่างคราฟต์ (ค่าเริ่มต้น: ปุ่ม C)")]
        [SerializeField] private Key toggleCraftKey = Key.C;

        [Header("Cheat / Debug Settings")]
        [Tooltip("เปิดโหมด Free Craft: คราฟต์ได้ทุกสูตรโดยไม่ต้องมีวัตถุดิบ และไม่หักของออกจากกระเป๋า")]
        [SerializeField] private bool freeCraftMode = false;

        [Tooltip("ปุ่มลัดสำหรับสลับโหมด Free Craft ทันทีในเกม (ค่าเริ่มต้น: ปุ่ม F8)")]
        [SerializeField] private Key toggleFreeCraftKey = Key.F8;

        private readonly List<CraftingRecipe> _filteredRecipes = new List<CraftingRecipe>();

        private CraftingCategory _currentCategory = CraftingCategory.Building;
        private string _currentSearchQuery = string.Empty;
        private int _currentPage = 0;
        private int _totalPages = 1;
        private bool _isOpen = false;

        private PlayerInventory _playerInventory;

        public bool IsOpen => _isOpen;
        public CraftingCategory CurrentCategory => _currentCategory;

        private Button _freeCraftButton;
        private TMP_Text _freeCraftButtonText;

        public bool FreeCraftMode
        {
            get => freeCraftMode || CraftingDatabase.IsFreeCraft;
            set
            {
                freeCraftMode = value;
                CraftingDatabase.IsFreeCraft = value;
                UpdateFreeCraftButtonVisuals();
                RefreshActiveThumbnailsState();
            }
        }

        public event Action<CraftingRecipe> OnRecipeCrafted;

        // =====================================================
        //  Unity Lifecycle
        // =====================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

#if UNITY_EDITOR
            // ในโหมด Unity Editor ให้เปิด Free Craft เป็นค่าเริ่มต้นเสมอ เพื่อให้ทดสอบได้ทันที
            freeCraftMode = true;
            CraftingDatabase.IsFreeCraft = true;
#endif

            if (freeCraftMode)
            {
                CraftingDatabase.IsFreeCraft = true;
            }

            if (craftingDatabase == null)
            {
                craftingDatabase = CraftingDatabase.Instance;
            }

            ScanExistingThumbnails();
            BindUIEvents();

            if (craftPanel != null)
            {
                craftPanel.SetActive(false);
            }
        }

        private void Start()
        {
            FindPlayerInventory();
            if (craftPanel != null && craftPanel.activeSelf)
            {
                EnsureFreeCraftButtonCreated();
                UpdateFreeCraftButtonVisuals();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // สลับโหมด Free Craft ด้วย F8, F1, หรือปุ่ม ~ (Backquote)
            bool toggleFreePressed = false;
            if (Keyboard.current.f8Key != null && Keyboard.current.f8Key.wasPressedThisFrame) toggleFreePressed = true;
            if (Keyboard.current.backquoteKey != null && Keyboard.current.backquoteKey.wasPressedThisFrame) toggleFreePressed = true;
            if (Keyboard.current.f1Key != null && Keyboard.current.f1Key.wasPressedThisFrame) toggleFreePressed = true;
            if (toggleFreeCraftKey != Key.None && Keyboard.current[toggleFreeCraftKey] != null && Keyboard.current[toggleFreeCraftKey].wasPressedThisFrame) toggleFreePressed = true;

            if (toggleFreePressed)
            {
                ToggleFreeCraft();
            }

            if (!enableShortcutKey) return;

            // ไม่กดเปิดทับถ้าผู้เล่นกำลังพิมพ์ในช่อง Search อยู่
            if (searchInputField != null && searchInputField.isFocused) return;

            if (Keyboard.current[toggleCraftKey].wasPressedThisFrame)
            {
                ToggleCraftMenu();
            }
        }

        [ContextMenu("Toggle Free Craft")]
        public void ToggleFreeCraft()
        {
            FreeCraftMode = !FreeCraftMode;
            Debug.Log($"[CraftController] Free Craft Mode: {(FreeCraftMode ? "ENABLED (เปิดโหมดคราฟต์ฟรี ไม่ต้องมีวัตถุดิบ)" : "DISABLED (ปิดโหมดคราฟต์ฟรี)")}");
            UpdateFreeCraftButtonVisuals();
            RefreshActiveThumbnailsState();
        }

        private void EnsureFreeCraftButtonCreated()
        {
            if (_freeCraftButton != null || craftPanel == null) return;

            GameObject btnObj = new GameObject("FreeCraftToggleBtn");
            btnObj.transform.SetParent(craftPanel.transform, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(300f, 420f);
            rt.sizeDelta = new Vector2(210f, 40f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

            _freeCraftButton = btnObj.AddComponent<Button>();
            _freeCraftButton.onClick.AddListener(ToggleFreeCraft);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _freeCraftButtonText = textObj.AddComponent<TextMeshProUGUI>();
            if (pageNumberText != null && pageNumberText.font != null)
            {
                _freeCraftButtonText.font = pageNumberText.font;
            }
            _freeCraftButtonText.alignment = TextAlignmentOptions.Center;
            _freeCraftButtonText.fontSize = 20f;
            _freeCraftButtonText.fontStyle = FontStyles.Bold;

            UpdateFreeCraftButtonVisuals();
        }

        private void UpdateFreeCraftButtonVisuals()
        {
            if (_freeCraftButtonText != null)
            {
                bool isFree = FreeCraftMode;
                _freeCraftButtonText.text = isFree ? "⚡ FREE CRAFT: ON" : "FREE CRAFT: OFF";
                _freeCraftButtonText.color = isFree ? new Color(1f, 0.82f, 0.15f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
                if (_freeCraftButton != null && _freeCraftButton.GetComponent<Image>() != null)
                {
                    _freeCraftButton.GetComponent<Image>().color = isFree ? new Color(0.28f, 0.22f, 0.05f, 0.95f) : new Color(0.15f, 0.15f, 0.18f, 0.95f);
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= HandleInventoryChanged;
            }
        }

        // =====================================================
        //  Setup & Binding
        // =====================================================

        private void BindUIEvents()
        {
            if (buildingTabButton != null)
                buildingTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Building));
            if (utilityTabButton != null)
                utilityTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Utility));
            if (defenseTabButton != null)
                defenseTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Defense));
            if (weaponsTabButton != null)
                weaponsTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Weapons));
            if (consumablesTabButton != null)
                consumablesTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Consumables));
            if (toolsTabButton != null)
                toolsTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Tools));
            if (specialTabButton != null)
                specialTabButton.onClick.AddListener(() => SelectCategory(CraftingCategory.Special));

            if (prevPageButton != null)
                prevPageButton.onClick.AddListener(PrevPage);
            if (nextPageButton != null)
                nextPageButton.onClick.AddListener(NextPage);
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseCraftMenu);

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.AddListener(OnSearchValueChanged);
            }
        }

        /// <summary>
        /// สแกนหา CraftThumbnail จาก thumbnailSlots หรือจากลูกใน thumbnailContainer / Hierarchy
        /// </summary>
        [ContextMenu("Scan Existing Thumbnails")]
        public void ScanExistingThumbnails()
        {
            if (thumbnailSlots == null)
            {
                thumbnailSlots = new List<CraftThumbnail>();
            }

            // ถ้าใน Inspector ยังไม่ได้ลากใส่ ให้ดึงจากลูกของ thumbnailContainer หรือ GetComponentsInChildren อัตโนมัติ
            if (thumbnailSlots.Count == 0)
            {
                if (thumbnailContainer != null)
                {
                    foreach (Transform child in thumbnailContainer)
                    {
                        CraftThumbnail thumb = child.GetComponent<CraftThumbnail>();
                        if (thumb == null)
                        {
                            thumb = child.GetComponentInChildren<CraftThumbnail>(true);
                        }

                        if (thumb != null && !thumbnailSlots.Contains(thumb))
                        {
                            thumbnailSlots.Add(thumb);
                        }
                    }
                }
                else
                {
                    CraftThumbnail[] found = GetComponentsInChildren<CraftThumbnail>(true);
                    foreach (CraftThumbnail thumb in found)
                    {
                        if (thumb != null && !thumbnailSlots.Contains(thumb))
                        {
                            thumbnailSlots.Add(thumb);
                        }
                    }
                }
            }

            for (int i = 0; i < thumbnailSlots.Count; i++)
            {
                CraftThumbnail thumb = thumbnailSlots[i];
                if (thumb != null)
                {
                    thumb.AutoBindIfNeeded();
                    thumb.gameObject.SetActive(false);
                }
            }
        }

        private void FindPlayerInventory()
        {
            if (_playerInventory != null) return;

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerInventory = playerObj.GetComponent<PlayerInventory>();
            }

            if (_playerInventory == null)
            {
                _playerInventory = FindAnyObjectByType<PlayerInventory>();
            }

            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= HandleInventoryChanged;
                _playerInventory.OnInventoryChanged += HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            if (_isOpen)
            {
                RefreshActiveThumbnailsState();
            }
        }

        // =====================================================
        //  Public API (เปิด / ปิด / เปลี่ยนหมวดหมู่ / ค้นหา)
        // =====================================================

        public void ToggleCraftMenu()
        {
            if (_isOpen)
                CloseCraftMenu();
            else
                OpenCraftMenu();
        }

        public void OpenCraftMenu()
        {
            FindPlayerInventory();
            if (craftingDatabase == null)
            {
                craftingDatabase = CraftingDatabase.Instance;
            }

            if (thumbnailSlots == null || thumbnailSlots.Count == 0)
            {
                ScanExistingThumbnails();
            }

            _isOpen = true;
            if (craftPanel != null)
            {
                craftPanel.SetActive(true);
            }

            EnsureFreeCraftButtonCreated();
            UpdateFreeCraftButtonVisuals();

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Push(this);
            }

            SelectCategory(_currentCategory);
        }

        public void CloseCraftMenu()
        {
            _isOpen = false;
            if (craftPanel != null)
            {
                craftPanel.SetActive(false);
            }

            if (PopUpManager.Instance != null)
            {
                PopUpManager.Instance.Remove(this);
            }
        }

        public void ClosePopUp()
        {
            if (_isOpen)
            {
                CloseCraftMenu();
            }
        }

        public void SelectCategory(CraftingCategory category)
        {
            if (_isOpen && _currentCategory == category && string.IsNullOrEmpty(_currentSearchQuery) && _filteredRecipes.Count > 0)
            {
                return;
            }

            _currentCategory = category;
            _currentPage = 0;
            UpdateCategoryTabButtons();
            RebuildFilteredList();
            RefreshUI();
        }

        // เมธอดเสริมเผื่อลากใส่ OnClick() ของปุ่มใน Inspector โดยตรง
        public void SelectCategoryByIndex(int categoryIndex)
        {
            if (Enum.IsDefined(typeof(CraftingCategory), categoryIndex))
            {
                SelectCategory((CraftingCategory)categoryIndex);
            }
        }

        public void SelectBuildingTab() => SelectCategory(CraftingCategory.Building);
        public void SelectUtilityTab() => SelectCategory(CraftingCategory.Utility);
        public void SelectDefenseTab() => SelectCategory(CraftingCategory.Defense);
        public void SelectWeaponsTab() => SelectCategory(CraftingCategory.Weapons);
        public void SelectConsumablesTab() => SelectCategory(CraftingCategory.Consumables);
        public void SelectToolsTab() => SelectCategory(CraftingCategory.Tools);
        public void SelectSpecialTab() => SelectCategory(CraftingCategory.Special);

        public void OnSearchValueChanged(string searchText)
        {
            _currentSearchQuery = searchText ?? string.Empty;
            _currentPage = 0;
            RebuildFilteredList();
            RefreshUI();
        }

        public void NextPage()
        {
            if (_currentPage < _totalPages - 1)
            {
                _currentPage++;
                RefreshUI();
            }
        }

        public void PrevPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshUI();
            }
        }

        // =====================================================
        //  UI Refresh & Pagination
        // =====================================================

        private void UpdateCategoryTabButtons()
        {
            UpdateTabVisual(buildingTabButton, _currentCategory == CraftingCategory.Building);
            UpdateTabVisual(utilityTabButton, _currentCategory == CraftingCategory.Utility);
            UpdateTabVisual(defenseTabButton, _currentCategory == CraftingCategory.Defense);
            UpdateTabVisual(weaponsTabButton, _currentCategory == CraftingCategory.Weapons);
            UpdateTabVisual(consumablesTabButton, _currentCategory == CraftingCategory.Consumables);
            UpdateTabVisual(toolsTabButton, _currentCategory == CraftingCategory.Tools);
            UpdateTabVisual(specialTabButton, _currentCategory == CraftingCategory.Special);
        }

        private void UpdateTabVisual(Button tabButton, bool isSelected)
        {
            if (tabButton == null) return;

            // คง interactable ไว้เสมอเพื่อไม่ให้โดน disabled gray-out ทับสี
            tabButton.interactable = true;

            // 1. ดึง Image จากตัว Button (GetComponent หรือ button.image) เพื่อสลับ Sprite
            Image targetImg = tabButton.GetComponent<Image>();
            if (targetImg == null)
            {
                targetImg = tabButton.image;
            }
            if (targetImg == null)
            {
                targetImg = tabButton.GetComponentInChildren<Image>();
            }

            if (targetImg != null)
            {
                if (isSelected && selectedTabSprite != null)
                {
                    targetImg.sprite = selectedTabSprite;
                }
                else if (!isSelected && unselectedTabSprite != null)
                {
                    targetImg.sprite = unselectedTabSprite;
                }
            }

            // 2. ดึง TextMeshPro จากลูก (GetComponentInChildren) เพื่อเปลี่ยนสีตัวหนังสือ (เลือก = สีดำ, ไม่ได้เลือก = สีขาว)
            TMP_Text label = tabButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = isSelected ? selectedTabTextColor : unselectedTabTextColor;
            }
        }

        private void RebuildFilteredList()
        {
            _filteredRecipes.Clear();
            if (craftingDatabase == null)
            {
                craftingDatabase = CraftingDatabase.Instance;
            }
            if (craftingDatabase == null) return;

            List<CraftingRecipe> matched = craftingDatabase.SearchRecipes(_currentSearchQuery, _currentCategory);
            _filteredRecipes.AddRange(matched);
        }

        public void RefreshUI()
        {
            FindPlayerInventory();

            if (thumbnailSlots == null || thumbnailSlots.Count == 0)
            {
                ScanExistingThumbnails();
            }

            // ถ้ามี slot อยู่ใน List จะใช้จำนวน slot นั้นเป็นขนาดต่อหน้าทันที (เช่น 6 ช่องตาม UI)
            int pageSize = thumbnailSlots.Count > 0 ? thumbnailSlots.Count : Mathf.Max(1, itemsPerPage);
            int totalRecipes = _filteredRecipes.Count;
            _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalRecipes / pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, _totalPages - 1);

            // อัปเดตตัวเลขหน้าและปุ่ม < >
            if (pageNumberText != null)
            {
                pageNumberText.text = (_currentPage + 1).ToString();
            }
            if (prevPageButton != null)
            {
                prevPageButton.interactable = _currentPage > 0;
            }
            if (nextPageButton != null)
            {
                nextPageButton.interactable = _currentPage < _totalPages - 1;
            }

            int startIndex = _currentPage * pageSize;

            // วนเซตข้อมูลลงในช่องที่มีอยู่เดิม ไม่สร้างใหม่ ไม่เปลือง Memory
            for (int i = 0; i < thumbnailSlots.Count; i++)
            {
                CraftThumbnail thumb = thumbnailSlots[i];
                if (thumb == null) continue;

                int recipeIndex = startIndex + i;
                if (recipeIndex < totalRecipes)
                {
                    CraftingRecipe recipe = _filteredRecipes[recipeIndex];
                    thumb.gameObject.SetActive(true);
                    thumb.SetData(recipe, _playerInventory, HandleThumbnailCraftClicked);
                }
                else
                {
                    thumb.Clear();
                    thumb.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshActiveThumbnailsState()
        {
            for (int i = 0; i < thumbnailSlots.Count; i++)
            {
                CraftThumbnail thumb = thumbnailSlots[i];
                if (thumb != null && thumb.gameObject.activeSelf && thumb.CurrentRecipe != null)
                {
                    thumb.RefreshState(_playerInventory);
                }
            }
        }

        // =====================================================
        //  Crafting Execution
        // =====================================================

        private void HandleThumbnailCraftClicked(CraftingRecipe recipe)
        {
            if (recipe == null) return;

            // ตรวจสอบว่าเป็นสิ่งปลูกสร้างหรือไม่ ถ้าใช่ ให้ปิดหน้าต่างคราฟต์แล้วเข้าสู่โหมดเล็งวาง (Build Mode) ทันที
            if (BuildingManager.Instance != null && BuildingPlacer.Instance != null)
            {
                BuildingData bData = BuildingManager.Instance.GetBuildingDataForRecipe(recipe);
                if (bData != null)
                {
                    FindPlayerInventory();
                    bool canBuild = FreeCraftMode || (craftingDatabase != null ? craftingDatabase.CanCraft(recipe, _playerInventory) : true);
                    if (!canBuild)
                    {
                        Debug.Log($"[CraftController] Not enough materials to build '{bData.buildingName}'!");
                        return;
                    }

                    CloseCraftMenu();
                    BuildingPlacer.Instance.StartPlacement(bData, recipe);
                    return;
                }
            }

            TryCraftRecipe(recipe);
        }

        /// <summary>
        /// ทำการคราฟต์ไอเทมตามสูตร: ตรวจสอบวัตถุดิบ -> หักวัตถุดิบออกจากกระเป๋า -> เพิ่มไอเทมผลลัพธ์
        /// </summary>
        public bool TryCraftRecipe(CraftingRecipe recipe)
        {
            if (recipe == null) return false;
            FindPlayerInventory();

            if (_playerInventory == null)
            {
                Debug.LogWarning("[CraftController] Cannot craft: PlayerInventory not found!");
                return false;
            }

            if (craftingDatabase == null)
            {
                craftingDatabase = CraftingDatabase.Instance;
            }

            bool isFree = FreeCraftMode;

            // 1. ตรวจสอบว่ามีวัตถุดิบครบทุกชิ้นหรือไม่ (ถ้าไม่ใช่โหมด Free Craft)
            if (!isFree)
            {
                if (recipe.ingredients == null || recipe.ingredients.Count == 0) return false;
                for (int i = 0; i < recipe.ingredients.Count; i++)
                {
                    CraftingIngredient ing = recipe.ingredients[i];
                    string reqName = ing.ResolvedItemName;
                    if (string.IsNullOrEmpty(reqName) || _playerInventory.GetItemCount(reqName) < ing.amount)
                    {
                        Debug.Log($"[CraftController] Not enough '{reqName}' to craft '{recipe.recipeName}'.");
                        return false;
                    }
                }
            }

            // 2. หักวัตถุดิบทั้งหมดออกจาก PlayerInventory (ถ้าไม่ใช่โหมด Free Craft)
            if (!isFree && recipe.ingredients != null)
            {
                for (int i = 0; i < recipe.ingredients.Count; i++)
                {
                    CraftingIngredient ing = recipe.ingredients[i];
                    _playerInventory.RemoveItem(ing.ResolvedItemName, ing.amount);
                }
            }

            // 3. มอบไอเทมผลลัพธ์เข้า PlayerInventory
            string resultName = recipe.ResolvedResultName;
            int resultAmount = Mathf.Max(1, recipe.resultAmount);

            if (recipe.resultItem != null && recipe.resultItem.type == ItemType.Money)
            {
                _playerInventory.AddMoney(resultAmount);
            }
            else
            {
                _playerInventory.AddItem(resultName, resultAmount);
            }

            if (isFree)
            {
                Debug.Log($"[CraftController] [FreeCraft] Crafted '{resultName}' x{resultAmount} (ฟรี ไม่เสียวัตถุดิบ)!");
            }
            else
            {
                Debug.Log($"[CraftController] Crafted '{resultName}' x{resultAmount}!");
            }
            OnRecipeCrafted?.Invoke(recipe);

            // 4. รีเฟรชสถานะปุ่มและจำนวนของทันที
            RefreshActiveThumbnailsState();
            return true;
        }
    }
}

