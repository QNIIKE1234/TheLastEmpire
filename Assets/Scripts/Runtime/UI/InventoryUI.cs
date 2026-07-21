using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [Header("UI Customization")]
        [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.94f); // Sleek slate dark mode
        [SerializeField] private Color accentColor = new Color(0.95f, 0.75f, 0.2f, 1f);   // Bright yellow accent

        [Header("UI References (Optional)")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private TMPro.TMP_Text moneyTextHUD;
        [SerializeField] private TMPro.TMP_Text healthTextHUD;
        [SerializeField] private GameObject itemSlotsContainer;

        private GameObject _canvasObject;
        private GameObject _panelObject;
        private TMP_Text _moneyText;
        private TMP_Text _healthText;
        private GameObject _itemContainer;
        private PlayerInventory _playerInventory;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (inventoryPanel != null)
                {
                    _canvasObject = inventoryPanel;
                    _moneyText = moneyTextHUD;
                    _healthText = healthTextHUD;
                    _itemContainer = itemSlotsContainer;

                    // Ensure the custom panel is hidden initially
                    _canvasObject.SetActive(false);
                }
                else
                {
                    CreateProceduralUI();
                }
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
                    if (_playerInventory != null)
                    {
                        _playerInventory.OnInventoryChanged += RefreshUI;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerInventory != null)
            {
                _playerInventory.OnInventoryChanged -= RefreshUI;
            }
        }

        /// <summary>
        /// Toggles the inventory visibility state, updating timescale and refreshing content.
        /// </summary>
        public void ToggleInventory()
        {
            if (_playerInventory == null)
            {
                FindInventory();
            }

            _isOpen = !_isOpen;

            if (_canvasObject != null)
            {
                _canvasObject.SetActive(_isOpen);
            }

            // Pause game when inventory is open, resume when closed
            Time.timeScale = _isOpen ? 0f : 1f;

            if (_isOpen)
            {
                RefreshUI();
            }
        }

        /// <summary>
        /// Rebuilds the item lists and currency fields.
        /// </summary>
        public void RefreshUI()
        {
            if (_playerInventory == null) return;

            // 1. Update currency and health status
            if (_moneyText != null)
            {
                _moneyText.text = $"Wallet: <color=yellow>${_playerInventory.Money}</color>";
            }
            if (_healthText != null)
            {
                Health health = _playerInventory.GetComponent<Health>();
                if (health != null)
                {
                    _healthText.text = $"HP: <color=#1bff33>{Mathf.RoundToInt(health.CurrentHealth)}</color> / {Mathf.RoundToInt(health.MaxHealth)}";
                }
            }

            // 2. Clear old item slots
            if (_itemContainer != null)
            {
                foreach (Transform child in _itemContainer.transform)
                {
                    Destroy(child.gameObject);
                }

                // 3. Render grouped items with quantities
                Dictionary<string, int> quantities = _playerInventory.GetItemQuantities();
                if (quantities.Count == 0)
                {
                    CreateItemRow("", "<i>- Inventory Empty -</i>", Color.gray, false);
                }
                else
                {
                    PlayerController player = _playerInventory.GetComponent<PlayerController>();
                    string equippedWeaponName = player != null ? player.CurrentWeaponName : "";

                    foreach (var pair in quantities)
                    {
                        string key = pair.Key;
                        string cleanKey = (key ?? "").ToLower().Trim();
                        bool isWeapon = cleanKey.Contains("rifl") || cleanKey.Contains("shot") || cleanKey.Contains("pist");
                        bool isUsable = (key == "Potion" || key == "Bread" || isWeapon);
                        string displayName = key;

                        if (key == "Potion")
                        {
                            displayName = "<color=#1bff33>[USABLE]</color> Potion <color=#111111>(Click to Use)</color>";
                        }
                        else if (key == "Bread")
                        {
                            displayName = "<color=#ffeb3b>[USABLE]</color> Bread <color=#111111>(Click to Eat)</color>";
                        }
                        else if (isWeapon)
                        {
                            string cleanEquipped = (equippedWeaponName ?? "").ToLower().Trim();
                            bool isEquipped = cleanKey.Contains(cleanEquipped) || cleanEquipped.Contains(cleanKey) || (cleanKey.Contains("pist") && cleanEquipped.Contains("pist"));
                            if (isEquipped)
                            {
                                displayName = $"<color=#00e5ff>[EQUIPPED]</color> {key} <color=#111111>(Active)</color>";
                            }
                            else
                            {
                                displayName = $"<color=#111111>[WEAPON]</color> {key} <color=#111111>(Click to Equip)</color>";
                            }
                        }
                        else if (key == "Ammo")
                        {
                            displayName = "<color=#00e5ff>[AMMO]</color> Ammo";
                        }
                        else if (key == "ETC")
                        {
                            displayName = "<color=#ff9a00>[ETC]</color> ETC";
                        }
                        else
                        {
                            displayName = $"<color=#ff9a00>[ETC]</color> {key}";
                        }
                        CreateItemRow(key, $"{displayName} <color=#90A4AE>x{pair.Value}</color>", Color.white, isUsable);
                    }
                }
            }
        }

        private void CreateItemRow(string itemName, string textContent, Color textColor, bool isUsable)
        {
            GameObject row = new GameObject("ItemRow");
            row.transform.SetParent(_itemContainer.transform, false);

            // Add panel background for each item row
            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(1f, 1f, 1f, 0.04f); // Subtle background highlights

            RectTransform rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500f, 50f);

            if (isUsable)
            {
                Button btn = row.AddComponent<Button>();
                
                // Color configuration for hover/click transitions
                ColorBlock colors = btn.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0.04f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.2f);
                colors.selectedColor = new Color(1f, 1f, 1f, 0.04f);
                btn.colors = colors;

                btn.onClick.AddListener(() =>
                {
                    if (_playerInventory != null)
                    {
                        if (_playerInventory.UseItem(itemName))
                        {
                            RefreshUI();
                        }
                    }
                });
            }

            // Draw icon if available in Database
            bool hasIcon = false;
            ItemData data = (ItemDatabase.Instance != null) ? ItemDatabase.Instance.GetItemByName(itemName) : null;
            if (data != null && data.icon != null)
            {
                GameObject iconObj = new GameObject("ItemIcon");
                iconObj.transform.SetParent(row.transform, false);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = data.icon;
                iconImg.preserveAspect = true;

                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(12f, 0f);
                iconRect.sizeDelta = new Vector2(32f, 32f);

                hasIcon = true;
            }

            // Add row text
            GameObject textObj = new GameObject("ItemText");
            textObj.transform.SetParent(row.transform, false);

            TMP_Text rowText = textObj.AddComponent<TextMeshProUGUI>();
            rowText.text = textContent;
            rowText.fontSize = 24;
            rowText.color = textColor;
            rowText.alignment = TextAlignmentOptions.Left;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(hasIcon ? 56f : 15f, 5f);
            textRect.offsetMax = new Vector2(-15f, -5f);
        }

        /// <summary>
        /// Builds the Canvas and the main inventory panel procedurally at runtime.
        /// </summary>
        private void CreateProceduralUI()
        {
            // 1. Canvas Setup
            _canvasObject = new GameObject("InventoryCanvas");
            DontDestroyOnLoad(_canvasObject);
            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99; // Top visual sorting index

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvasObject.AddComponent<GraphicRaycaster>();

            // 2. Central Panel Setup
            _panelObject = new GameObject("InventoryPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            Image panelImage = _panelObject.AddComponent<Image>();
            panelImage.color = panelColor;

            RectTransform panelRect = _panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 680f);

            // Add a thin border line to make it look premium
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(_panelObject.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.3f);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            // Put border behind panel contents
            borderObj.transform.SetAsFirstSibling();

            // 3. Title Text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_panelObject.transform, false);

            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "SURVIVAL GEAR / INVENTORY";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = accentColor;
            titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector3(0f, -40f, 0f);
            titleRect.sizeDelta = new Vector2(500f, 50f);

            // 4. Money/Wallet Text & Health Text (Side-by-Side)
            GameObject moneyObj = new GameObject("MoneyText");
            moneyObj.transform.SetParent(_panelObject.transform, false);

            _moneyText = moneyObj.AddComponent<TextMeshProUGUI>();
            _moneyText.text = "Wallet: <color=yellow>$0</color>";
            _moneyText.fontSize = 24;
            _moneyText.alignment = TextAlignmentOptions.Left;

            RectTransform moneyRect = moneyObj.GetComponent<RectTransform>();
            moneyRect.anchorMin = new Vector2(0f, 1f);
            moneyRect.anchorMax = new Vector2(0.5f, 1f);
            moneyRect.pivot = new Vector2(0f, 1f);
            moneyRect.anchoredPosition = new Vector3(30f, -95f, 0f);
            moneyRect.sizeDelta = new Vector2(250f, 40f);

            GameObject healthObj = new GameObject("HealthText");
            healthObj.transform.SetParent(_panelObject.transform, false);

            _healthText = healthObj.AddComponent<TextMeshProUGUI>();
            _healthText.text = "HP: <color=red>100/100</color>";
            _healthText.fontSize = 24;
            _healthText.alignment = TextAlignmentOptions.Right;

            RectTransform healthRect = healthObj.GetComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0.5f, 1f);
            healthRect.anchorMax = new Vector2(1f, 1f);
            healthRect.pivot = new Vector2(1f, 1f);
            healthRect.anchoredPosition = new Vector3(-30f, -95f, 0f);
            healthRect.sizeDelta = new Vector2(250f, 40f);

            // 5. Scroll Rect / List Container
            GameObject scrollObj = new GameObject("ItemScrollView");
            scrollObj.transform.SetParent(_panelObject.transform, false);
            
            RectTransform scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            scrollRectTrans.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRectTrans.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTrans.anchoredPosition = new Vector3(0f, -60f, 0f);
            scrollRectTrans.sizeDelta = new Vector2(500f, 440f);

            // Item slot list parent (with Vertical Layout Group)
            _itemContainer = new GameObject("ItemListContent");
            _itemContainer.transform.SetParent(scrollObj.transform, false);

            VerticalLayoutGroup layout = _itemContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            RectTransform containerRect = _itemContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // 6. Close Button hint text at the bottom
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(_panelObject.transform, false);

            TMP_Text hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "Press [ I ] or [ ESC ] to Close";
            hintText.fontSize = 20;
            hintText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            hintText.alignment = TextAlignmentOptions.Center;

            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector3(0f, 25f, 0f);
            hintRect.sizeDelta = new Vector2(500f, 30f);

            // Default state: Hidden
            _canvasObject.SetActive(false);
        }
    }
}
