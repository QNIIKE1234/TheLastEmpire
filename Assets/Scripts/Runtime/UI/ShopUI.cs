using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        [Header("UI Customization")]
        [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.96f); // Slick dark mode
        [SerializeField] private Color accentColor = new Color(0f, 0.9f, 1f, 1f);       // Vibrant cyan accent

        private GameObject _canvasObject;
        private GameObject _panelObject;
        private TMP_Text _moneyText;
        private GameObject _itemContainer;
        private PlayerInventory _playerInventory;
        private System.Collections.Generic.List<ShopItemConfig> _activeShopItems = new System.Collections.Generic.List<ShopItemConfig>();
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                CreateProceduralUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindInventory();
        }

        private void FindInventory()
        {
            if (_playerInventory == null)
            {
                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    _playerInventory = player.GetComponent<PlayerInventory>();
                }
            }
        }

        public void OpenShopMenu(System.Collections.Generic.List<ShopItemConfig> itemsForSale)
        {
            if (_playerInventory == null)
            {
                FindInventory();
            }

            _activeShopItems = itemsForSale ?? new System.Collections.Generic.List<ShopItemConfig>();

            _isOpen = true;
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(true);
            }

            // Pause the game
            Time.timeScale = 0f;

            RefreshUI();
        }

        public void CloseShopMenu()
        {
            _isOpen = false;
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(false);
            }

            // Resume game
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_isOpen && Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseShopMenu();
                }
            }
        }

        public void RefreshUI()
        {
            if (_playerInventory == null) return;

            // Update cash display
            if (_moneyText != null)
            {
                _moneyText.text = $"Your Wallet: <color=yellow>${_playerInventory.Money}</color>";
            }

            // Clear old items
            if (_itemContainer != null)
            {
                foreach (Transform child in _itemContainer.transform)
                {
                    Destroy(child.gameObject);
                }

                // Render shop items dynamically
                if (_activeShopItems != null)
                {
                    foreach (ShopItemConfig config in _activeShopItems)
                    {
                        if (config.item != null)
                        {
                            RenderShopItem(config.item, config.price);
                        }
                    }
                }
            }
        }

        private void RenderShopItem(ItemData item, int price)
        {
            string itemName = item.itemName;
            string description = item.description;

            GameObject row = new GameObject("ShopItemRow");
            row.transform.SetParent(_itemContainer.transform, false);

            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.04f);

            RectTransform rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(650f, 60f);

            // Icon support
            bool hasIcon = false;
            if (item.icon != null)
            {
                GameObject iconObj = new GameObject("ItemIcon");
                iconObj.transform.SetParent(row.transform, false);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = item.icon;
                iconImg.preserveAspect = true;

                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(15f, 0f);
                iconRect.sizeDelta = new Vector2(38f, 38f);
                hasIcon = true;
            }

            // Text info (Name & description)
            GameObject textObj = new GameObject("ItemTextInfo");
            textObj.transform.SetParent(row.transform, false);
            TMP_Text rowText = textObj.AddComponent<TextMeshProUGUI>();
            rowText.text = $"<b>{itemName}</b>\n<size=20><color=grey>{description}</color></size>";
            rowText.fontSize = 24;
            rowText.color = Color.white;
            rowText.alignment = TextAlignmentOptions.Left;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(hasIcon ? 65f : 20f, 5f);
            textRect.offsetMax = new Vector2(-200f, -5f);

            // Buy Button
            GameObject btnObj = new GameObject("BuyButton");
            btnObj.transform.SetParent(row.transform, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-15f, 0f);
            btnRect.sizeDelta = new Vector2(150f, 40f);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            // Button highlights
            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            colors.highlightedColor = new Color(0f, 0.8f, 1f, 0.25f);
            colors.pressedColor = new Color(0f, 0.8f, 1f, 0.5f);
            btn.colors = colors;

            GameObject btnTextObj = new GameObject("BuyText");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            TMP_Text btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = $"Buy <color=yellow>${price}</color>";
            btnText.fontSize = 20;
            btnText.fontStyle = FontStyles.Bold;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;

            RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            // Buy Click Action
            btn.onClick.AddListener(() =>
            {
                if (_playerInventory != null)
                {
                    // Check if already owns weapon
                    if ((itemName == "Rifle" || itemName == "Shotgun" || itemName == "Pistol") && _playerInventory.Items.Contains(itemName))
                    {
                        Debug.LogWarning($"[ShopUI] You already own the {itemName}!");
                        return;
                    }

                    if (_playerInventory.Money >= price)
                    {
                        if (itemName == "Ammo")
                        {
                            _playerInventory.AddMoney(-price);
                            PlayerController player = _playerInventory.GetComponent<PlayerController>();
                            if (player != null)
                            {
                                player.AddReserveAmmo(30); // Add 30 reserve bullets directly
                            }
                            Debug.Log("[ShopUI] Purchased Ammo! Added 30 bullets to reserves.");
                        }
                        else
                        {
                            _playerInventory.AddMoney(-price);
                            _playerInventory.AddItem(itemName, 1);
                            Debug.Log($"[ShopUI] Purchased {itemName}!");
                        }
                        
                        // Refresh both Shop and Inventory UI
                        RefreshUI();
                        if (InventoryUI.Instance != null)
                        {
                            InventoryUI.Instance.RefreshUI();
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[ShopUI] Not enough money!");
                    }
                }
            });

            // Disable button if gun is already owned
            if ((itemName == "Rifle" || itemName == "Shotgun" || itemName == "Pistol") && _playerInventory != null && _playerInventory.Items.Contains(itemName))
            {
                btnText.text = "<color=grey>Owned</color>";
                btn.interactable = false;
            }
        }

        private void CreateProceduralUI()
        {
            // 1. Canvas Setup
            _canvasObject = new GameObject("ShopCanvas");
            DontDestroyOnLoad(_canvasObject);
            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Render on top of HUD and Inventory

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasObject.AddComponent<GraphicRaycaster>();

            // 2. Main Panel Background Setup
            _panelObject = new GameObject("ShopPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            Image panelImage = _panelObject.AddComponent<Image>();
            panelImage.color = panelColor;

            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(800f, 650f);

            // Add panel border highlight
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(_panelObject.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0f, 0.9f, 1f, 0.3f);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            borderObj.transform.SetAsFirstSibling();

            // 3. Title Text Setup
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_panelObject.transform, false);

            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "SPECIAL MERCHANT";
            titleText.fontSize = 38;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = accentColor;
            titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -25f);
            titleRect.sizeDelta = new Vector2(600f, 50f);

            // 4. Money/Cash Text Setup
            GameObject moneyObj = new GameObject("MoneyText");
            moneyObj.transform.SetParent(_panelObject.transform, false);

            _moneyText = moneyObj.AddComponent<TextMeshProUGUI>();
            _moneyText.text = "Your Wallet: $0";
            _moneyText.fontSize = 24;
            _moneyText.alignment = TextAlignmentOptions.Center;

            RectTransform moneyRect = moneyObj.GetComponent<RectTransform>();
            moneyRect.anchorMin = new Vector2(0.5f, 1f);
            moneyRect.anchorMax = new Vector2(0.5f, 1f);
            moneyRect.pivot = new Vector2(0.5f, 1f);
            moneyRect.anchoredPosition = new Vector2(0f, -75f);
            moneyRect.sizeDelta = new Vector2(600f, 40f);

            // 5. Scroll Rect container for shop items
            GameObject scrollObj = new GameObject("ItemsScrollArea");
            scrollObj.transform.SetParent(_panelObject.transform, false);

            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = new Vector2(0f, -10f);
            scrollRect.sizeDelta = new Vector2(720f, 420f);

            ScrollRect sRect = scrollObj.AddComponent<ScrollRect>();
            sRect.horizontal = false;
            sRect.vertical = true;

            // Mask viewable viewport area
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            viewportObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;

            sRect.viewport = viewportRect;

            // Content container inside viewport
            _itemContainer = new GameObject("Content");
            _itemContainer.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = _itemContainer.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 400f);

            // Vertical list layout and size fitter
            VerticalLayoutGroup vLayout = _itemContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(10, 10, 10, 10);
            vLayout.spacing = 10;
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.childControlHeight = false;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childForceExpandWidth = true;

            ContentSizeFitter fitter = _itemContainer.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sRect.content = contentRect;

            // 6. Close Button Setup
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(_panelObject.transform, false);

            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 25f);
            closeRect.sizeDelta = new Vector2(250f, 50f);

            Image closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            ColorBlock cColors = closeBtn.colors;
            cColors.normalColor = new Color(0.2f, 0.2f, 0.25f, 1f);
            cColors.highlightedColor = new Color(0.85f, 0.24f, 0.24f, 0.4f); // Red tint highlight
            cColors.pressedColor = new Color(0.85f, 0.24f, 0.24f, 0.7f);
            closeBtn.colors = cColors;
            closeBtn.onClick.AddListener(CloseShopMenu);

            GameObject closeTextObj = new GameObject("CloseText");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            TMP_Text closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "CLOSE";
            closeText.fontSize = 22;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;

            RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            // Hide UI initially
            _canvasObject.SetActive(false);
        }
    }
}
