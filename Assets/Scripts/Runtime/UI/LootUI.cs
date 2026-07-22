using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    public class LootUI : MonoBehaviour
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

        private GameObject _canvasObject;
        private GameObject _panelObject;
        private TMP_Text _titleText;
        private GameObject _itemContainer;

        private GameObject _promptPanel;
        private TMP_Text _promptText;

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
                CreateProceduralUI();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_isOpen && Keyboard.current != null)
            {
                // Toggle loot UI closed if player presses ESC or E again (prevent closing on the same frame it was opened)
                if (Keyboard.current.escapeKey.wasPressedThisFrame || 
                    (Keyboard.current.eKey.wasPressedThisFrame && Time.frameCount > _justOpenedFrame))
                {
                    Close();
                }
            }
        }

        public void ShowPrompt(string actionText)
        {
            if (_promptPanel == null || _promptText == null) return;

            // Don't show prompt if UI is open or canvas is disabled
            if (_isOpen)
            {
                HidePrompt();
                return;
            }

            _promptText.text = $"Press <color=yellow><b>[E]</b></color> to {actionText}";
            
            // Ensure canvas is active to render prompt even when looting UI itself is closed
            if (_canvasObject != null && !_canvasObject.activeSelf)
            {
                _canvasObject.SetActive(true);
            }

            _promptPanel.SetActive(true);
        }

        public void HidePrompt()
        {
            if (_promptPanel != null)
            {
                _promptPanel.SetActive(false);
            }

            // Hide the canvas entirely if the main loot menu is also closed
            if (!_isOpen && _canvasObject != null && _canvasObject.activeSelf)
            {
                _canvasObject.SetActive(false);
            }
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

            // Pause game timescale during looting interaction
            Time.timeScale = 0f;

            // Hide the interaction prompt when menu opens
            HidePrompt();

            if (_panelObject != null)
            {
                _panelObject.SetActive(true);
            }

            // Save and unlock mouse cursor for UI clicks
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
            Time.timeScale = 1f; // Resume normal timescale

            if (_panelObject != null)
            {
                _panelObject.SetActive(false);
            }

            // Restore mouse cursor state
            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLockState;

            _currentContainer = null;
            _playerInventory = null;
            Debug.Log("[LootUI] Loot menu closed.");
        }

        public void RefreshUI()
        {
            if (_currentContainer == null || _playerInventory == null) return;

            // 1. Update Title Name
            if (_titleText != null)
            {
                _titleText.text = $"SEARCHING: {_currentContainer.containerName.ToUpper()}";
            }

            // 2. Clear old list slots
            if (_itemContainer != null)
            {
                foreach (Transform child in _itemContainer.transform)
                {
                    Destroy(child.gameObject);
                }

                bool hasLoot = false;

                // 3. Render Money Slot (if any)
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

                // 4. Render Item Slots
                for (int i = 0; i < _currentContainer.itemsInside.Count; i++)
                {
                    hasLoot = true;
                    int index = i;
                    LootSlot slot = _currentContainer.itemsInside[index];
                    string dispText = $"{slot.itemName} x{slot.quantity}";

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

                // 5. Empty Status Fallback
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

            // 1. Collect Cash
            if (_currentContainer.moneyAmount > 0)
            {
                _playerInventory.AddMoney(_currentContainer.moneyAmount);
                _currentContainer.moneyAmount = 0;
            }

            // 2. Collect Items
            foreach (LootSlot slot in _currentContainer.itemsInside)
            {
                _playerInventory.AddItem(slot.itemName, slot.quantity);
            }
            _currentContainer.itemsInside.Clear();
            _currentContainer.isSearched = true;

            Close();
        }

        private void CreateLootRow(string textContent, UnityEngine.Events.UnityAction onLootClick)
        {
            // Row parent
            GameObject row = new GameObject("LootRow");
            row.transform.SetParent(_itemContainer.transform, false);

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.05f); // Subtle slot background highlights

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(500f, 60f);

            // Row Text
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

            // Loot Button
            GameObject btnObj = new GameObject("LootButton");
            btnObj.transform.SetParent(row.transform, false);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.2f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg; // Explicitly link graphic for raycast detection
            btn.onClick.AddListener(onLootClick);

            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-15f, 0f);
            btnRect.sizeDelta = new Vector2(120f, 40f);

            // Button Text
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
            // Ensure EventSystem exists in the scene so UI click events can be processed
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 1. Canvas Setup
            _canvasObject = new GameObject("LootCanvas");
            DontDestroyOnLoad(_canvasObject);
            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Render above standard Inventory UI

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasObject.AddComponent<GraphicRaycaster>();

            // 2. Central Panel Setup
            _panelObject = new GameObject("LootPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            Image panelImage = _panelObject.AddComponent<Image>();
            panelImage.color = panelColor;

            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 680f);

            // Border Line
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

            // 3. Title Text
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

            // 4. Scroll List Container
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

            // 5. Bottom Buttons (Loot All & Close)
            GameObject bottomPanel = new GameObject("BottomButtons");
            bottomPanel.transform.SetParent(_panelObject.transform, false);

            RectTransform bottomRect = bottomPanel.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.5f, 0f);
            bottomRect.anchorMax = new Vector2(0.5f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = new Vector3(0f, 30f, 0f);
            bottomRect.sizeDelta = new Vector2(500f, 60f);

            // Loot All Button
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

            // Close Button
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

            // Disable Loot panel by default, prompt will control visibility separately
            _panelObject.SetActive(false);

            // 6. Interaction Prompt Panel Setup (Centered bottom of screen)
            _promptPanel = new GameObject("InteractionPrompt");
            _promptPanel.transform.SetParent(_canvasObject.transform, false);

            Image promptBg = _promptPanel.AddComponent<Image>();
            promptBg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);

            RectTransform promptRect = _promptPanel.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector3(0f, 120f, 0f);
            promptRect.sizeDelta = new Vector2(400f, 60f);

            // Gold border overlay
            GameObject pBorder = new GameObject("PromptBorder");
            pBorder.transform.SetParent(_promptPanel.transform, false);
            Image pbImg = pBorder.AddComponent<Image>();
            pbImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
            RectTransform pbRect = pBorder.GetComponent<RectTransform>();
            pbRect.anchorMin = Vector2.zero;
            pbRect.anchorMax = Vector2.one;
            pbRect.offsetMin = new Vector2(-2f, -2f);
            pbRect.offsetMax = new Vector2(2f, 2f);
            pBorder.transform.SetAsFirstSibling();

            // Prompt text
            GameObject pTextObj = new GameObject("PromptText");
            pTextObj.transform.SetParent(_promptPanel.transform, false);
            _promptText = pTextObj.AddComponent<TextMeshProUGUI>();
            _promptText.text = "Press [E] to Interact";
            _promptText.fontSize = 20;
            _promptText.fontStyle = FontStyles.Bold;
            _promptText.color = Color.white;
            _promptText.alignment = TextAlignmentOptions.Center;

            RectTransform ptRect = pTextObj.GetComponent<RectTransform>();
            ptRect.anchorMin = Vector2.zero;
            ptRect.anchorMax = Vector2.one;
            ptRect.offsetMin = Vector2.zero;
            ptRect.offsetMax = Vector2.zero;

            _promptPanel.SetActive(false); // Hidden by default
        }
    }
}
