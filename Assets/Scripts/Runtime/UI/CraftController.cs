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

        private readonly List<CraftingRecipe> _filteredRecipes = new List<CraftingRecipe>();

        private CraftingCategory _currentCategory = CraftingCategory.Building;
        private string _currentSearchQuery = string.Empty;
        private int _currentPage = 0;
        private int _totalPages = 1;
        private bool _isOpen = false;

        private PlayerInventory _playerInventory;

        public bool IsOpen => _isOpen;
        public CraftingCategory CurrentCategory => _currentCategory;

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
        }

        private void Update()
        {
            if (!enableShortcutKey || Keyboard.current == null) return;

            // ไม่กดเปิดทับถ้าผู้เล่นกำลังพิมพ์ในช่อง Search อยู่
            if (searchInputField != null && searchInputField.isFocused) return;

            if (Keyboard.current[toggleCraftKey].wasPressedThisFrame)
            {
                ToggleCraftMenu();
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
            SetTabInteractable(buildingTabButton, _currentCategory != CraftingCategory.Building);
            SetTabInteractable(utilityTabButton, _currentCategory != CraftingCategory.Utility);
            SetTabInteractable(defenseTabButton, _currentCategory != CraftingCategory.Defense);
            SetTabInteractable(weaponsTabButton, _currentCategory != CraftingCategory.Weapons);
            SetTabInteractable(consumablesTabButton, _currentCategory != CraftingCategory.Consumables);
            SetTabInteractable(toolsTabButton, _currentCategory != CraftingCategory.Tools);
            SetTabInteractable(specialTabButton, _currentCategory != CraftingCategory.Special);
        }

        private static void SetTabInteractable(Button tabButton, bool interactable)
        {
            if (tabButton != null)
            {
                tabButton.interactable = interactable;
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

            // 1. ตรวจสอบว่ามีวัตถุดิบครบทุกชิ้นหรือไม่
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

            // 2. หักวัตถุดิบทั้งหมดออกจาก PlayerInventory
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                CraftingIngredient ing = recipe.ingredients[i];
                _playerInventory.RemoveItem(ing.ResolvedItemName, ing.amount);
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

            Debug.Log($"[CraftController] Crafted '{resultName}' x{resultAmount}!");
            OnRecipeCrafted?.Invoke(recipe);

            // 4. รีเฟรชสถานะปุ่มและจำนวนของทันที
            RefreshActiveThumbnailsState();
            return true;
        }
    }
}

