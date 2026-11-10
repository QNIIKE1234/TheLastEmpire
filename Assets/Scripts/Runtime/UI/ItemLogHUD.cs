using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastEmpire
{
    /// <summary>
    /// Controller for Log_HUD that displays item pickup and crafting notifications (LogThumbnail).
    /// Uses a pre-allocated Object Pool of 10 LogThumbnail items instantiated from Prefabs/UI/LogThumbnail.prefab.
    /// Works seamlessly with Log_HUD's GridLayoutGroup: active items are ordered top-to-bottom and deactivated back to pool after 2s.
    /// </summary>
    public class ItemLogHUD : MonoBehaviour
    {
        public static ItemLogHUD Instance { get; private set; }

        [Header("Template & Pool Settings")]
        [Tooltip("Prefab LogThumbnail (Assets/Resources_Addressable/Prefabs/UI/LogThumbnail.prefab)")]
        [SerializeField] private GameObject logThumbnailTemplate;

        [Tooltip("Number of pre-allocated LogThumbnail objects in the pool")]
        [SerializeField] private int poolSize = 10;

        [Tooltip("Pre-allocated pool items under Log_HUD")]
        [SerializeField] private List<LogThumbnail> pooledThumbnails = new List<LogThumbnail>(10);

        [Header("Display Settings")]
        [Tooltip("Duration (in seconds) each log entry stays on screen before disappearing")]
        [SerializeField] private float displayDuration = 2.0f;

        [Tooltip("Fade-out duration at the end of displayDuration")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Header("Fallback Manual Layout (Used only if no LayoutGroup on Log_HUD)")]
        [SerializeField] private Vector2 baseAnchoredPosition = new Vector2(-160f, -60f);
        [SerializeField] private Vector2 thumbnailSize = new Vector2(250f, 50f);
        [SerializeField] private float verticalSpacing = 58f;
        [SerializeField] private float slideSpeed = 14f;

        private readonly Queue<LogThumbnail> _inactivePool = new Queue<LogThumbnail>(10);
        private readonly List<LogThumbnail> _activeEntries = new List<LogThumbnail>(10);

        private LayoutGroup _layoutGroup;
        private Sprite _fallbackIcon;
        private bool _poolInitialized = false;

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
        private const string PrefabAssetPath = "Assets/Resources_Addressable/Prefabs/UI/LogThumbnail.prefab";

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

                if (hud.logThumbnailTemplate == null)
                {
                    GameObject prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
                    if (prefabAsset != null)
                    {
                        hud.logThumbnailTemplate = prefabAsset;
                        UnityEditor.EditorUtility.SetDirty(hud);
                    }
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
                existing.InitializePool();
                return;
            }

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
                hud.InitializePool();
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
            _layoutGroup = GetComponent<LayoutGroup>();
            InitializePool();
        }

        private void OnEnable()
        {
            if (_layoutGroup == null)
            {
                _layoutGroup = GetComponent<LayoutGroup>();
            }
            InitializePool();
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

        private GameObject ResolveSpawnSource()
        {
            if (logThumbnailTemplate == null)
            {
                logThumbnailTemplate = AddressableManager.LoadAsset<GameObject>("Prefabs/UI/LogThumbnail");
            }
#if UNITY_EDITOR
            if (logThumbnailTemplate == null)
            {
                logThumbnailTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            }
#endif
            if (logThumbnailTemplate == null && transform.childCount > 0)
            {
                logThumbnailTemplate = transform.GetChild(0).gameObject;
            }

            return logThumbnailTemplate;
        }

        [ContextMenu("Initialize Pool (10 Items)")]
        public void InitializePool()
        {
            int targetPoolSize = Mathf.Max(1, poolSize);
            if (_poolInitialized && pooledThumbnails != null && pooledThumbnails.Count >= targetPoolSize)
            {
                return;
            }

            if (_layoutGroup == null)
            {
                _layoutGroup = GetComponent<LayoutGroup>();
            }

            GameObject spawnSource = ResolveSpawnSource();
            if (spawnSource == null) return;

            LogThumbnail sourceThumbComp = spawnSource.GetComponent<LogThumbnail>();
            if (sourceThumbComp != null)
            {
                sourceThumbComp.AutoBindIfNeeded();
                if (sourceThumbComp.IconImage != null && sourceThumbComp.IconImage.sprite != null)
                {
                    _fallbackIcon = sourceThumbComp.IconImage.sprite;
                }
            }

            if (pooledThumbnails == null)
            {
                pooledThumbnails = new List<LogThumbnail>(targetPoolSize);
            }
            pooledThumbnails.RemoveAll(x => x == null);
            _inactivePool.Clear();
            _activeEntries.Clear();

            // 1. Collect any existing children under Log_HUD into the pool first
            for (int i = 0; i < transform.childCount && pooledThumbnails.Count < targetPoolSize; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;

                LogThumbnail item = child.GetComponent<LogThumbnail>();
                if (item == null)
                {
                    item = child.gameObject.AddComponent<LogThumbnail>();
                }
                item.AutoBindIfNeeded();

                if (_fallbackIcon == null && item.IconImage != null && item.IconImage.sprite != null)
                {
                    _fallbackIcon = item.IconImage.sprite;
                }

                child.localScale = Vector3.one;
                child.localRotation = Quaternion.identity;
                child.gameObject.name = $"LogThumbnail_Pool_{pooledThumbnails.Count + 1:00}";
                child.gameObject.SetActive(false);

                if (!pooledThumbnails.Contains(item))
                {
                    pooledThumbnails.Add(item);
                }
            }

            // 2. Pre-instantiate remaining items from logThumbnailTemplate prefab up to poolSize (10)
            while (pooledThumbnails.Count < targetPoolSize)
            {
                int index = pooledThumbnails.Count;
                GameObject cloneObj = Instantiate(spawnSource, transform, false);
                cloneObj.name = $"LogThumbnail_Pool_{index + 1:00}";
                cloneObj.transform.localScale = Vector3.one;
                cloneObj.transform.localRotation = Quaternion.identity;
                cloneObj.SetActive(false);

                LogThumbnail item = cloneObj.GetComponent<LogThumbnail>();
                if (item == null)
                {
                    item = cloneObj.AddComponent<LogThumbnail>();
                }
                item.AutoBindIfNeeded();

                if (_layoutGroup == null && item.RectTransform != null)
                {
                    item.RectTransform.anchorMin = new Vector2(1f, 1f);
                    item.RectTransform.anchorMax = new Vector2(1f, 1f);
                    item.RectTransform.pivot = new Vector2(0.5f, 0.5f);
                    item.RectTransform.sizeDelta = thumbnailSize;
                    item.RectTransform.anchoredPosition = GetSlotAnchoredPosition(index);
                }

                pooledThumbnails.Add(item);
            }

            // 3. Enqueue all 10 pooled items into the inactive queue
            for (int i = 0; i < pooledThumbnails.Count; i++)
            {
                LogThumbnail item = pooledThumbnails[i];
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                    _inactivePool.Enqueue(item);
                }
            }

            _poolInitialized = true;
        }

        private LogThumbnail AcquireFromPool()
        {
            InitializePool();

            while (_inactivePool.Count > 0)
            {
                LogThumbnail item = _inactivePool.Dequeue();
                if (item != null)
                {
                    return item;
                }
            }

            // If all 10 pooled items are currently active on screen, recycle the oldest active entry
            if (_activeEntries.Count > 0)
            {
                LogThumbnail oldest = _activeEntries[0];
                _activeEntries.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.gameObject.SetActive(false);
                    return oldest;
                }
            }

            return null;
        }

        private void ReleaseToPool(LogThumbnail entry)
        {
            if (entry == null) return;
            entry.SetAlpha(0f);
            entry.gameObject.SetActive(false);
            _inactivePool.Enqueue(entry);
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
                    _activeEntries.RemoveAt(i);
                    ReleaseToPool(entry);
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

            // Only manually animate anchoredPosition if Log_HUD does not use a LayoutGroup (e.g. GridLayoutGroup)
            if (_layoutGroup == null || !_layoutGroup.enabled)
            {
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
        }

        private Vector2 GetSlotAnchoredPosition(int index)
        {
            return baseAnchoredPosition + new Vector2(0f, -index * verticalSpacing);
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
        /// Activates a pooled LogThumbnail notification (or merges if the exact same item was added in the same frame).
        /// </summary>
        public void ShowNotification(string itemKey, string prefix, string displayName, int quantity, Sprite icon)
        {
            InitializePool();

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

            LogThumbnail entry = AcquireFromPool();
            if (entry == null) return;

            int slotIndex = _activeEntries.Count;
            if (_layoutGroup == null || !_layoutGroup.enabled)
            {
                if (entry.RectTransform != null)
                {
                    entry.RectTransform.anchoredPosition = GetSlotAnchoredPosition(slotIndex);
                    entry.RectTransform.localScale = Vector3.one;
                }
            }
            else
            {
                entry.transform.localScale = Vector3.one;
                entry.transform.SetAsLastSibling();
            }

            entry.gameObject.SetActive(true);
            entry.Setup(itemKey, prefix, displayName, quantity, icon, _fallbackIcon);
            _activeEntries.Add(entry);
        }
    }
}
