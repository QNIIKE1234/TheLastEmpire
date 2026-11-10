using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace TheLastEmpire
{
    /// <summary>
    /// Controller for Log_HUD that displays item pickup and crafting notifications (LogThumbnail).
    /// Each notification stays for ~2 seconds then fades out, and multiple notifications stack vertically.
    /// </summary>
    public class ItemLogHUD : MonoBehaviour
    {
        public static ItemLogHUD Instance { get; private set; }

        [Header("Template Reference")]
        [Tooltip("Template LogThumbnail inside Log_HUD")]
        [SerializeField] private GameObject logThumbnailTemplate;

        [Header("Display & Layout Settings")]
        [Tooltip("Duration (in seconds) each log entry stays on screen before disappearing")]
        [SerializeField] private float displayDuration = 2.0f;

        [Tooltip("Fade-out duration at the end of displayDuration")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Tooltip("Vertical spacing in pixels between stacked log entries")]
        [SerializeField] private float verticalSpacing = 58f;

        [Tooltip("Maximum number of simultaneous log entries visible on screen")]
        [SerializeField] private int maxVisibleEntries = 8;

        [Tooltip("Speed at which stacked entries slide up when an entry above disappears")]
        [SerializeField] private float slideSpeed = 14f;

        private readonly List<LogThumbnail> _activeEntries = new List<LogThumbnail>();
        private Vector2 _baseAnchoredPos = new Vector2(-160f, -60f);
        private Sprite _fallbackIcon;
        private bool _templateInitialized = false;

        private PlayerInventory _subscribedInventory;
        private CraftController _subscribedCraftController;
        private BuildingPlacer _subscribedBuildingPlacer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeOnLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInstanceInScene();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorAutoAttachToScene()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                GameObject logHudObj = GameObject.Find("Log_HUD");
                if (logHudObj == null) return;

                ItemLogHUD hud = logHudObj.GetComponent<ItemLogHUD>();
                if (hud == null)
                {
                    hud = logHudObj.AddComponent<ItemLogHUD>();
                    UnityEditor.EditorUtility.SetDirty(logHudObj);
                }

                Transform thumbTrans = logHudObj.transform.Find("LogThumbnail");
                if (thumbTrans != null)
                {
                    if (hud.logThumbnailTemplate != thumbTrans.gameObject)
                    {
                        hud.logThumbnailTemplate = thumbTrans.gameObject;
                        UnityEditor.EditorUtility.SetDirty(hud);
                    }

                    LogThumbnail lt = thumbTrans.GetComponent<LogThumbnail>();
                    if (lt == null)
                    {
                        lt = thumbTrans.gameObject.AddComponent<LogThumbnail>();
                    }
                    lt.AutoBindIfNeeded();
                    UnityEditor.EditorUtility.SetDirty(lt);
                }
            };
        }
#endif

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstanceInScene();
        }

        public static void EnsureInstanceInScene()
        {
            if (Instance != null) return;

            ItemLogHUD existing = FindFirstObjectByType<ItemLogHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                existing.InitializeTemplate();
                return;
            }

            // Find Log_HUD in scene
            GameObject logHudObj = GameObject.Find("Log_HUD");
            if (logHudObj == null)
            {
                RectTransform[] allRects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < allRects.Length; i++)
                {
                    if (allRects[i] != null && string.Equals(allRects[i].name, "Log_HUD", StringComparison.OrdinalIgnoreCase))
                    {
                        logHudObj = allRects[i].gameObject;
                        break;
                    }
                }
            }

            if (logHudObj != null)
            {
                if (!logHudObj.activeSelf)
                {
                    logHudObj.SetActive(true);
                }
                ItemLogHUD hud = logHudObj.GetComponent<ItemLogHUD>();
                if (hud == null)
                {
                    hud = logHudObj.AddComponent<ItemLogHUD>();
                }
                Instance = hud;
                hud.InitializeTemplate();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            InitializeTemplate();
        }

        private void OnEnable()
        {
            InitializeTemplate();
            EnsureSubscriptions();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        [ContextMenu("Auto Bind Template")]
        public void InitializeTemplate()
        {
            if (_templateInitialized && logThumbnailTemplate != null) return;

            if (logThumbnailTemplate == null)
            {
                Transform child = transform.Find("LogThumbnail");
                if (child == null)
                {
                    child = transform.Find("Log");
                }
                if (child == null && transform.childCount > 0)
                {
                    child = transform.GetChild(0);
                }
                if (child != null)
                {
                    logThumbnailTemplate = child.gameObject;
                }
            }

            if (logThumbnailTemplate != null)
            {
                RectTransform rt = logThumbnailTemplate.GetComponent<RectTransform>();
                if (rt != null)
                {
                    _baseAnchoredPos = rt.anchoredPosition;
                }

                LogThumbnail thumbComp = logThumbnailTemplate.GetComponent<LogThumbnail>();
                if (thumbComp == null)
                {
                    thumbComp = logThumbnailTemplate.AddComponent<LogThumbnail>();
                }
                thumbComp.AutoBindIfNeeded();

                if (thumbComp.IconImage != null && thumbComp.IconImage.sprite != null)
                {
                    _fallbackIcon = thumbComp.IconImage.sprite;
                }

                // Hide the scene template so placeholder text ("Got apple ane pine 1 each") is not shown at start
                logThumbnailTemplate.SetActive(false);
                _templateInitialized = true;
            }
        }

        private void Update()
        {
            EnsureSubscriptions();

            if (_activeEntries.Count == 0) return;

            float dt = Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.5f, displayDuration);
            float fadeStart = Mathf.Max(0f, duration - Mathf.Clamp(fadeOutDuration, 0.05f, duration));

            for (int i = _activeEntries.Count - 1; i >= 0; i--)
            {
                LogThumbnail entry = _activeEntries[i];
                if (entry == null)
                {
                    _activeEntries.RemoveAt(i);
                    continue;
                }

                entry.ElapsedTime += dt;

                if (entry.ElapsedTime >= duration)
                {
                    Destroy(entry.gameObject);
                    _activeEntries.RemoveAt(i);
                    continue;
                }

                if (entry.ElapsedTime >= fadeStart)
                {
                    float t = (entry.ElapsedTime - fadeStart) / Mathf.Max(0.01f, duration - fadeStart);
                    entry.SetAlpha(1f - t);
                }
                else
                {
                    entry.SetAlpha(1f);
                }
            }

            // Update vertical stacked positions smoothly
            for (int i = 0; i < _activeEntries.Count; i++)
            {
                LogThumbnail entry = _activeEntries[i];
                if (entry == null || entry.RectTransform == null) continue;

                Vector2 targetPos = GetSlotAnchoredPosition(i);
                entry.RectTransform.anchoredPosition = Vector2.Lerp(
                    entry.RectTransform.anchoredPosition,
                    targetPos,
                    Mathf.Clamp01(dt * slideSpeed)
                );
            }
        }

        private Vector2 GetSlotAnchoredPosition(int index)
        {
            return _baseAnchoredPos + new Vector2(0f, -index * verticalSpacing);
        }

        private void EnsureSubscriptions()
        {
            if (PlayerInventory.Instance != _subscribedInventory)
            {
                if (_subscribedInventory != null)
                {
                    _subscribedInventory.OnItemAdded -= HandleItemAdded;
                    _subscribedInventory.OnMoneyAdded -= HandleMoneyAdded;
                }
                _subscribedInventory = PlayerInventory.Instance;
                if (_subscribedInventory != null)
                {
                    _subscribedInventory.OnItemAdded += HandleItemAdded;
                    _subscribedInventory.OnMoneyAdded += HandleMoneyAdded;
                }
            }

            if (CraftController.Instance != _subscribedCraftController)
            {
                if (_subscribedCraftController != null)
                {
                    _subscribedCraftController.OnRecipeCrafted -= HandleRecipeCrafted;
                }
                _subscribedCraftController = CraftController.Instance;
                if (_subscribedCraftController != null)
                {
                    _subscribedCraftController.OnRecipeCrafted += HandleRecipeCrafted;
                }
            }

            if (BuildingPlacer.Instance != _subscribedBuildingPlacer)
            {
                if (_subscribedBuildingPlacer != null)
                {
                    _subscribedBuildingPlacer.OnBuildingPlaced -= HandleBuildingPlaced;
                }
                _subscribedBuildingPlacer = BuildingPlacer.Instance;
                if (_subscribedBuildingPlacer != null)
                {
                    _subscribedBuildingPlacer.OnBuildingPlaced += HandleBuildingPlaced;
                }
            }
        }

        private void UnsubscribeAll()
        {
            if (_subscribedInventory != null)
            {
                _subscribedInventory.OnItemAdded -= HandleItemAdded;
                _subscribedInventory.OnMoneyAdded -= HandleMoneyAdded;
                _subscribedInventory = null;
            }

            if (_subscribedCraftController != null)
            {
                _subscribedCraftController.OnRecipeCrafted -= HandleRecipeCrafted;
                _subscribedCraftController = null;
            }

            if (_subscribedBuildingPlacer != null)
            {
                _subscribedBuildingPlacer.OnBuildingPlaced -= HandleBuildingPlaced;
                _subscribedBuildingPlacer = null;
            }
        }

        private void HandleItemAdded(string itemName, int quantity)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0) return;

            string displayName = itemName;
            Sprite icon = null;

            if (ItemDatabase.Instance != null)
            {
                ItemData data = ItemDatabase.Instance.GetItemByName(itemName);
                if (data != null)
                {
                    if (!string.IsNullOrEmpty(data.itemName))
                    {
                        displayName = data.itemName;
                    }
                    icon = data.icon;
                }
            }

            ShowNotification(
                itemKey: $"GOT:{displayName.ToLowerInvariant()}",
                prefix: "Got",
                displayName: displayName,
                quantity: quantity,
                icon: icon
            );
        }

        private void HandleMoneyAdded(int amount)
        {
            if (amount <= 0) return;

            Sprite moneyIcon = null;
            if (ItemDatabase.Instance != null)
            {
                ItemData moneyData = ItemDatabase.Instance.GetItemByName("Money");
                if (moneyData != null)
                {
                    moneyIcon = moneyData.icon;
                }
            }

            ShowNotification(
                itemKey: "__MONEY__",
                prefix: "Got Money",
                displayName: "Money",
                quantity: amount,
                icon: moneyIcon
            );
        }

        private void HandleRecipeCrafted(CraftingRecipe recipe)
        {
            if (recipe == null) return;

            string displayName = !string.IsNullOrEmpty(recipe.recipeName)
                ? recipe.recipeName
                : recipe.ResolvedResultName;
            int amount = Mathf.Max(1, recipe.resultAmount);
            Sprite icon = recipe.ResolvedResultIcon;

            if (icon == null && BuildingManager.Instance != null)
            {
                BuildingData bData = BuildingManager.Instance.GetBuildingDataForRecipe(recipe);
                if (bData != null && bData.icon != null)
                {
                    icon = bData.icon;
                }
            }

            if (icon == null && ItemDatabase.Instance != null)
            {
                ItemData data = ItemDatabase.Instance.GetItemByName(recipe.ResolvedResultName);
                if (data != null && data.icon != null)
                {
                    icon = data.icon;
                }
            }

            ShowNotification(
                itemKey: $"CRAFT:{displayName.ToLowerInvariant()}",
                prefix: "Crafted",
                displayName: displayName,
                quantity: amount,
                icon: icon
            );
        }

        private void HandleBuildingPlaced(BuildingObject building)
        {
            if (building == null || building.Data == null) return;

            string displayName = !string.IsNullOrEmpty(building.Data.buildingName)
                ? building.Data.buildingName
                : building.name;
            Sprite icon = building.Data.icon;

            ShowNotification(
                itemKey: $"BUILD:{displayName.ToLowerInvariant()}",
                prefix: "Crafted",
                displayName: displayName,
                quantity: 1,
                icon: icon
            );
        }

        /// <summary>
        /// Spawns a new LogThumbnail notification (or merges if the exact same item was added in the same frame).
        /// </summary>
        public void ShowNotification(string itemKey, string prefix, string displayName, int quantity, Sprite icon)
        {
            InitializeTemplate();
            if (logThumbnailTemplate == null) return;

            // If the exact same itemKey was added in the same frame (< 0.08s ago), merge quantity into that row
            for (int i = _activeEntries.Count - 1; i >= 0; i--)
            {
                LogThumbnail existing = _activeEntries[i];
                if (existing != null &&
                    string.Equals(existing.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase) &&
                    (Time.unscaledTime - existing.CreatedAtUnscaledTime) <= 0.08f)
                {
                    existing.AddQuantity(quantity);
                    return;
                }
            }

            // Enforce max visible entries by removing the oldest one if needed
            while (_activeEntries.Count >= Mathf.Max(1, maxVisibleEntries))
            {
                LogThumbnail oldest = _activeEntries[0];
                _activeEntries.RemoveAt(0);
                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }

            GameObject cloneObj = Instantiate(logThumbnailTemplate, transform);
            cloneObj.name = $"LogThumbnail_{displayName}_{_activeEntries.Count + 1}";
            cloneObj.SetActive(true);

            LogThumbnail entry = cloneObj.GetComponent<LogThumbnail>();
            if (entry == null)
            {
                entry = cloneObj.AddComponent<LogThumbnail>();
            }

            int slotIndex = _activeEntries.Count;
            if (entry.RectTransform != null)
            {
                entry.RectTransform.anchoredPosition = GetSlotAnchoredPosition(slotIndex);
                entry.RectTransform.localScale = Vector3.one;
            }

            entry.Setup(itemKey, prefix, displayName, quantity, icon, _fallbackIcon);
            _activeEntries.Add(entry);
        }
    }
}
