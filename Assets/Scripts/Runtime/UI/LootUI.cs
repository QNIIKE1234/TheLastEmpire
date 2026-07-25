using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    public class LootUI : MonoBehaviour, IPopUp
    {
        public static LootUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("LootUIManager");
                    _instance = go.AddComponent<LootUI>();
                }
                return _instance;
            }
        }
        private static LootUI _instance;

        [Header("UI Customization")]
        [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.94f); // Sleek slate dark mode
        [SerializeField] private Color accentColor = new Color(0.95f, 0.75f, 0.2f, 1f);   // Bright yellow accent

        [Header("UI References (Optional)")]
        [SerializeField] private GameObject lootPanel;
        [SerializeField] private TMP_Text titleTextHUD;
        [Tooltip("Content GameObject — วาง InventoryItemSlot ลูกไว้ข้างในได้เลย")]
        [SerializeField] private GameObject itemSlotsContainer;
        [SerializeField] private Button lootAllButton;
        [SerializeField] private Button closeButton;

        [Header("Interaction Prompt")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TMP_Text promptText;

        // ===== Internal Pool (built-in) =====
        private readonly List<InventoryItemSlot> _slotPool = new List<InventoryItemSlot>();
        private int _activeSlotCount = 0;

        private GameObject _canvasObject;
        private GameObject _panelObject;
        private TMP_Text _titleText;
        private GameObject _itemContainer;

        private Transform _promptTarget;
        private Vector2 _originalPromptAnchoredPos;
        private bool _hasOriginalPromptPos = false;

        private LootContainer _currentContainer;
        private PlayerInventory _playerInventory;
        private bool _isOpen = false;
        private int _justOpenedFrame = -1;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);

                if (lootPanel != null)
                {
                    _canvasObject = lootPanel; // Using the provided prefab's root canvas/panel
                    _panelObject = lootPanel;
                    _titleText = titleTextHUD;
                    _itemContainer = itemSlotsContainer;

                    if (_itemContainer != null)
                    {
                        ScanExistingSlots();
                    }

                    if (lootAllButton != null) lootAllButton.onClick.AddListener(LootAll);
                    if (closeButton != null) closeButton.onClick.AddListener(Close);

                    _canvasObject.SetActive(false);
                    if (promptPanel != null) 
                    {
                        promptPanel.SetActive(false);
                        RectTransform r = promptPanel.GetComponent<RectTransform>();
                        if (r != null)
                        {
                            _originalPromptAnchoredPos = r.anchoredPosition;
                            _hasOriginalPromptPos = true;
                        }
                    }
                }
                else
                {
                    CreateProceduralUI();
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (promptPanel != null && promptPanel.activeSelf && _promptTarget != null)
            {
                if (Camera.main != null)
                {
                    // Project the target's 3D position to the screen (+2 meters offset upwards)
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(_promptTarget.position + Vector3.up * 2.0f);
                    if (screenPos.z > 0)
                    {
                        promptPanel.transform.position = screenPos;
                    }
                }
            }
        }

        private void ScanExistingSlots()
        {
            _slotPool.Clear();
            foreach (Transform child in _itemContainer.transform)
            {
                InventoryItemSlot slot = child.GetComponent<InventoryItemSlot>();
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    _slotPool.Add(slot);
                }
            }
        }

        private InventoryItemSlot GetSlot()
        {
            if (_activeSlotCount >= _slotPool.Count)
            {
                if (_slotPool.Count == 0)
                {
                    Debug.LogWarning("[LootUI] No InventoryItemSlot found in container. Place at least one in the Editor.");
                    return null;
                }
                InventoryItemSlot clone = Instantiate(_slotPool[0], _itemContainer.transform);
                clone.name = $"LootSlot_{_slotPool.Count:00}";
                _slotPool.Add(clone);
            }

            InventoryItemSlot s = _slotPool[_activeSlotCount];
            s.gameObject.SetActive(true);
            _activeSlotCount++;
            return s;
        }

        private void PoolBeginFrame() => _activeSlotCount = 0;

        private void PoolEndFrame()
        {
            for (int i = _activeSlotCount; i < _slotPool.Count; i++)
            {
                if (_slotPool[i].gameObject.activeSelf)
                {
                    _slotPool[i].Clear();
                    _slotPool[i].gameObject.SetActive(false);
                }
            }
        }

        public void ShowPrompt(string actionText, Transform targetTransform = null)
        {
            if (promptPanel == null || promptText == null) return;

            if (_isOpen)
            {
                HidePrompt();
                return;
            }

            _promptTarget = targetTransform;

            promptText.text = $"Press <color=yellow><b>[E]</b></color> to {actionText}";
            promptPanel.SetActive(true);
        }

        public void HidePrompt()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
                if (_hasOriginalPromptPos)
                {
                    RectTransform r = promptPanel.GetComponent<RectTransform>();
                    if (r != null) r.anchoredPosition = _originalPromptAnchoredPos;
                }
            }
            _promptTarget = null;
        }

        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLockState;

        public void Open(LootContainer container, PlayerInventory inventory)
        {
            if (container == null || inventory == null) return;

            _currentContainer = container;
            _playerInventory = inventory;
            _isOpen = true;
            _justOpenedFrame = Time.frameCount;

            HidePrompt();

            if (_panelObject != null)
            {
                _panelObject.SetActive(true);
            }

            if (PopUpManager.Instance != null) PopUpManager.Instance.Push(this);

            _savedCursorVisible = Cursor.visible;
            _savedCursorLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            RefreshUI();
            Debug.Log($"[LootUI] Opened container: {container.containerName}");
        }

        public void Close()
        {
            _isOpen = false;

            if (_panelObject != null)
            {
                _panelObject.SetActive(false);
            }

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLockState;

            _currentContainer = null;
            _playerInventory = null;
            Debug.Log("[LootUI] Loot menu closed.");

            if (PopUpManager.Instance != null) PopUpManager.Instance.Remove(this);
        }

        public void ClosePopUp()
        {
            if (_isOpen)
            {
                Close();
            }
        }

        public void RefreshUI()
        {
            if (_currentContainer == null || _playerInventory == null) return;

            if (_titleText != null)
            {
                _titleText.text = $"SEARCHING: {_currentContainer.containerName.ToUpper()}";
            }

            if (_itemContainer == null) return;

            bool hasLoot = false;

            // Use Pool System
            if (_slotPool.Count > 0)
            {
                PoolBeginFrame();

                // 1. Money Slot
                if (_currentContainer.moneyAmount > 0)
                {
                    hasLoot = true;
                    InventoryItemSlot moneySlot = GetSlot();
                    if (moneySlot != null)
                    {
                        ItemData cashData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName("Cash") : null;
                        Sprite dynamicMoneyIcon = cashData != null ? cashData.icon : null;

                        moneySlot.SetData(
                            itemName: "Wallet Cash", 
                            quantity: _currentContainer.moneyAmount, 
                            icon: dynamicMoneyIcon, 
                            isEquipped: false,
                            onClickUse: (name) => {
                                if (_playerInventory != null && _currentContainer != null)
                                {
                                    _playerInventory.AddMoney(_currentContainer.moneyAmount);
                                    _currentContainer.moneyAmount = 0;
                                    RefreshUI();
                                }
                            },
                            customButtonText: "TAKE",
                            customQuantityText: $"<color=yellow>${_currentContainer.moneyAmount}</color>"
                        );
                    }
                }

                // 2. Item Slots
                for (int i = 0; i < _currentContainer.itemsInside.Count; i++)
                {
                    hasLoot = true;
                    int index = i;
                    LootSlot slotData = _currentContainer.itemsInside[index];

                    ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(slotData.itemName) : null;
                    Sprite icon = itemData != null ? itemData.icon : null;

                    InventoryItemSlot uiSlot = GetSlot();
                    if (uiSlot != null)
                    {
                        uiSlot.SetData(
                            itemName: slotData.itemName,
                            quantity: slotData.quantity,
                            icon: icon, 
                            isEquipped: false,
                            onClickUse: (name) => {
                                if (_playerInventory != null && _currentContainer != null && index < _currentContainer.itemsInside.Count)
                                {
                                    LootSlot innerSlot = _currentContainer.itemsInside[index];
                                    _playerInventory.AddItem(innerSlot.itemName, innerSlot.quantity);
                                    _currentContainer.itemsInside.RemoveAt(index);
                                    RefreshUI();
                                }
                            },
                            customButtonText: "TAKE"
                        );
                    }
                }

                // 3. Empty Fallback
                if (!hasLoot)
                {
                    _currentContainer.isSearched = true;
                    InventoryItemSlot emptySlot = GetSlot();
                    if (emptySlot != null)
                    {
                        emptySlot.SetData("- Container Empty -", 0, null, false, null, "---");
                    }
                }

                PoolEndFrame();
            }
            else
            {
                // Legacy / Procedural Render Path (Clear & Create)
                foreach (Transform child in _itemContainer.transform)
                {
                    Destroy(child.gameObject);
                }

                if (_currentContainer.moneyAmount > 0)
                {
                    hasLoot = true;
                    CreateLootRow($"Wallet Cash (${_currentContainer.moneyAmount})", () => {
                        if (_playerInventory != null && _currentContainer != null)
                        {
                            _playerInventory.AddMoney(_currentContainer.moneyAmount);
                            _currentContainer.moneyAmount = 0;
                            RefreshUI();
                        }
                    });
                }

                for (int i = 0; i < _currentContainer.itemsInside.Count; i++)
                {
                    hasLoot = true;
                    int index = i;
                    LootSlot slotData = _currentContainer.itemsInside[index];
                    string dispText = $"{slotData.itemName} x{slotData.quantity}";

                    CreateLootRow(dispText, () => {
                        if (_playerInventory != null && _currentContainer != null && index < _currentContainer.itemsInside.Count)
                        {
                            LootSlot innerSlot = _currentContainer.itemsInside[index];
                            _playerInventory.AddItem(innerSlot.itemName, innerSlot.quantity);
                            _currentContainer.itemsInside.RemoveAt(index);
                            RefreshUI();
                        }
                    });
                }

                if (!hasLoot)
                {
                    _currentContainer.isSearched = true;
                    CreateEmptyPlaceholder();
                }
            }
        }

        private void LootAll()
        {
            if (_currentContainer == null || _playerInventory == null) return;

            if (_currentContainer.moneyAmount > 0)
            {
                _playerInventory.AddMoney(_currentContainer.moneyAmount);
                _currentContainer.moneyAmount = 0;
            }

            foreach (LootSlot slot in _currentContainer.itemsInside)
            {
                _playerInventory.AddItem(slot.itemName, slot.quantity);
            }
            _currentContainer.itemsInside.Clear();
            _currentContainer.isSearched = true;

            Close();
        }

        // =====================================================
        //  Procedural / Legacy Methods
        // =====================================================

        private void CreateLootRow(string textContent, UnityEngine.Events.UnityAction onLootClick)
        {
            GameObject row = new GameObject("LootRow");
            row.transform.SetParent(_itemContainer.transform, false);

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.05f); 

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(500f, 60f);

            GameObject textObj = new GameObject("ItemText");
            textObj.transform.SetParent(row.transform, false);

            TMP_Text rowText = textObj.AddComponent<TextMeshProUGUI>();
            rowText.text = textContent;
            rowText.fontSize = 24;
            rowText.color = Color.white;
            rowText.alignment = TextAlignmentOptions.Left;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(0.7f, 1f);
            textRect.offsetMin = new Vector2(15f, 5f);
            textRect.offsetMax = new Vector2(-5f, -5f);

            GameObject btnObj = new GameObject("LootButton");
            btnObj.transform.SetParent(row.transform, false);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.2f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg; 
            btn.onClick.AddListener(onLootClick);

            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-15f, 0f);
            btnRect.sizeDelta = new Vector2(120f, 40f);

            GameObject btnTextObj = new GameObject("BtnText");
            btnTextObj.transform.SetParent(btnObj.transform, false);

            TMP_Text btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "TAKE";
            btnText.fontSize = 18;
            btnText.fontStyle = FontStyles.Bold;
            btnText.color = accentColor;
            btnText.alignment = TextAlignmentOptions.Center;

            RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
        }

        private void CreateEmptyPlaceholder()
        {
            GameObject row = new GameObject("EmptyRow");
            row.transform.SetParent(_itemContainer.transform, false);

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(500f, 60f);

            GameObject textObj = new GameObject("EmptyText");
            textObj.transform.SetParent(row.transform, false);

            TMP_Text rowText = textObj.AddComponent<TextMeshProUGUI>();
            rowText.text = "<i>- Container Empty -</i>";
            rowText.fontSize = 22;
            rowText.color = Color.gray;
            rowText.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void CreateProceduralUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            _canvasObject = new GameObject("LootCanvas");
            DontDestroyOnLoad(_canvasObject);
            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasObject.AddComponent<GraphicRaycaster>();

            _panelObject = new GameObject("LootPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);
            Image panelImage = _panelObject.AddComponent<Image>();
            panelImage.color = panelColor;

            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 680f);

            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(_panelObject.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.3f);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            borderObj.transform.SetAsFirstSibling();

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_panelObject.transform, false);
            _titleText = titleObj.AddComponent<TextMeshProUGUI>();
            _titleText.text = "SEARCHING CONTAINER";
            _titleText.fontSize = 28;
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.color = accentColor;
            _titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector3(0f, -40f, 0f);
            titleRect.sizeDelta = new Vector2(500f, 50f);

            GameObject scrollObj = new GameObject("LootScrollView");
            scrollObj.transform.SetParent(_panelObject.transform, false);
            RectTransform scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            scrollRectTrans.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRectTrans.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchoredPosition = new Vector3(0f, -10f, 0f);
            scrollRectTrans.sizeDelta = new Vector2(500f, 440f);

            _itemContainer = new GameObject("LootListContent");
            _itemContainer.transform.SetParent(scrollObj.transform, false);

            VerticalLayoutGroup layout = _itemContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            RectTransform contentRect = _itemContainer.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            ContentSizeFitter fitter = _itemContainer.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = scrollRectTrans;

            GameObject bottomPanel = new GameObject("BottomButtons");
            bottomPanel.transform.SetParent(_panelObject.transform, false);
            RectTransform bottomRect = bottomPanel.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.5f, 0f);
            bottomRect.anchorMax = new Vector2(0.5f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = new Vector3(0f, 30f, 0f);
            bottomRect.sizeDelta = new Vector2(500f, 60f);

            GameObject lootAllObj = new GameObject("LootAllButton");
            lootAllObj.transform.SetParent(bottomPanel.transform, false);
            Image lootAllImg = lootAllObj.AddComponent<Image>();
            lootAllImg.color = accentColor;
            Button lootAllBtn = lootAllObj.AddComponent<Button>();
            lootAllBtn.targetGraphic = lootAllImg;
            lootAllBtn.onClick.AddListener(LootAll);

            RectTransform lootAllRect = lootAllObj.GetComponent<RectTransform>();
            lootAllRect.anchorMin = new Vector2(0f, 0.5f);
            lootAllRect.anchorMax = new Vector2(0.48f, 0.5f);
            lootAllRect.pivot = new Vector2(0f, 0.5f);
            lootAllRect.sizeDelta = new Vector2(0f, 50f);

            GameObject lootAllTextObj = new GameObject("LootAllText");
            lootAllTextObj.transform.SetParent(lootAllObj.transform, false);
            TMP_Text lootAllText = lootAllTextObj.AddComponent<TextMeshProUGUI>();
            lootAllText.text = "LOOT ALL";
            lootAllText.fontSize = 20;
            lootAllText.fontStyle = FontStyles.Bold;
            lootAllText.color = Color.black;
            lootAllText.alignment = TextAlignmentOptions.Center;
            RectTransform latRect = lootAllTextObj.GetComponent<RectTransform>();
            latRect.anchorMin = Vector2.zero;
            latRect.anchorMax = Vector2.one;
            latRect.offsetMin = Vector2.zero;
            latRect.offsetMax = Vector2.zero;

            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(bottomPanel.transform, false);
            Image closeImg = closeObj.AddComponent<Image>();
            closeImg.color = new Color(0.2f, 0.2f, 0.22f, 1f);
            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);

            RectTransform closeRect = closeObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.52f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(0f, 0.5f);
            closeRect.sizeDelta = new Vector2(0f, 50f);

            GameObject closeTextObj = new GameObject("CloseText");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            TMP_Text closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "CLOSE";
            closeText.fontSize = 20;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;
            RectTransform ctRect = closeTextObj.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero;
            ctRect.offsetMax = Vector2.zero;

            _panelObject.SetActive(false);

            promptPanel = new GameObject("InteractionPrompt");
            promptPanel.transform.SetParent(_canvasObject.transform, false);
            Image promptBg = promptPanel.AddComponent<Image>();
            promptBg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);

            RectTransform promptRect = promptPanel.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector3(0f, 120f, 0f);
            promptRect.sizeDelta = new Vector2(400f, 60f);

            GameObject pBorder = new GameObject("PromptBorder");
            pBorder.transform.SetParent(promptPanel.transform, false);
            Image pbImg = pBorder.AddComponent<Image>();
            pbImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
            RectTransform pbRect = pBorder.GetComponent<RectTransform>();
            pbRect.anchorMin = Vector2.zero;
            pbRect.anchorMax = Vector2.one;
            pbRect.offsetMin = new Vector2(-2f, -2f);
            pbRect.offsetMax = new Vector2(2f, 2f);
            pBorder.transform.SetAsFirstSibling();

            GameObject pTextObj = new GameObject("PromptText");
            pTextObj.transform.SetParent(promptPanel.transform, false);
            promptText = pTextObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "Press [E] to Interact";
            promptText.fontSize = 20;
            promptText.fontStyle = FontStyles.Bold;
            promptText.color = Color.white;
            promptText.alignment = TextAlignmentOptions.Center;

            RectTransform ptRect = pTextObj.GetComponent<RectTransform>();
            ptRect.anchorMin = Vector2.zero;
            ptRect.anchorMax = Vector2.one;
            ptRect.offsetMin = Vector2.zero;
            ptRect.offsetMax = Vector2.zero;

            _originalPromptAnchoredPos = promptRect.anchoredPosition;
            _hasOriginalPromptPos = true;

            promptPanel.SetActive(false);
        }
    }
}
