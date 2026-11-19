using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TheLastEmpire
{
    public class AddressableManager : MonoBehaviour
    {
        public static AddressableManager Instance { get; private set; }

        public static bool IsPreloadComplete { get; private set; } = false;
        public static float PreloadProgress { get; private set; } = 0f;
        public static event Action OnPreloadCompleted;

        // Cache loaded handles so we don't load the same asset multiple times
        private readonly Dictionary<string, AsyncOperationHandle> _loadedAssetHandles = new Dictionary<string, AsyncOperationHandle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AsyncOperationHandle> _loadedLabelHandles = new Dictionary<string, AsyncOperationHandle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GameObject, AsyncOperationHandle> _instantiatedInstances = new Dictionary<GameObject, AsyncOperationHandle>();

        // Preloaded RAM caches (Critical for WebGL where WaitForCompletion() cannot block HTTP bundles!)
        private readonly Dictionary<string, List<UnityEngine.Object>> _preloadedByKey = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<UnityEngine.Object>> _preloadedByFilename = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<UnityEngine.Object>> _preloadedByLabel = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite[]> _preloadedSpritesByKey = new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AsyncOperationHandle> _preloadHandles = new List<AsyncOperationHandle>();
        private bool _preloadStarted = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[AddressableManager]");
                Instance = go.AddComponent<AddressableManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

#if UNITY_EDITOR
            IsPreloadComplete = true;
            PreloadProgress = 1f;
#endif
        }

        private void Start()
        {
#if !UNITY_EDITOR
            EnsurePreloadStarted();
#endif
        }

        public static void EnsurePreloadStarted()
        {
#if UNITY_EDITOR
            IsPreloadComplete = true;
            PreloadProgress = 1f;
            return;
#else
            if (Instance == null)
            {
                AutoInitialize();
            }
            if (Instance != null && !Instance._preloadStarted && !IsPreloadComplete)
            {
                Instance._preloadStarted = true;
                Instance.StartCoroutine(Instance.PreloadAllCoroutine());
            }
#endif
        }

        private IEnumerator PreloadAllCoroutine()
        {
            PreloadProgress = 0.02f;

            // 1. Initialize Addressables catalog asynchronously (required on WebGL!)
            AsyncOperationHandle<IResourceLocator> initHandle = Addressables.InitializeAsync();
            yield return initHandle;
            PreloadProgress = 0.08f;

            // 2. Gather every unique resource location & map keys/labels to locations
            Dictionary<string, IResourceLocation> uniqueLocations = new Dictionary<string, IResourceLocation>(StringComparer.Ordinal);
            Dictionary<string, List<string>> keyToLocationIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (Addressables.ResourceLocators != null)
            {
                foreach (IResourceLocator locator in Addressables.ResourceLocators)
                {
                    if (locator == null || locator.Keys == null) continue;
                    foreach (object rawKey in locator.Keys)
                    {
                        if (rawKey is not string keyStr || string.IsNullOrEmpty(keyStr)) continue;
                        // Skip raw 32-char GUID keys to keep dictionary lean
                        if (keyStr.Length == 32 && IsHexGuid(keyStr)) continue;

                        if (!locator.Locate(rawKey, typeof(UnityEngine.Object), out IList<IResourceLocation> locs) || locs == null || locs.Count == 0)
                            continue;

                        if (!keyToLocationIds.TryGetValue(keyStr, out List<string> locIdList))
                        {
                            locIdList = new List<string>(locs.Count);
                            keyToLocationIds[keyStr] = locIdList;
                        }

                        for (int i = 0; i < locs.Count; i++)
                        {
                            IResourceLocation loc = locs[i];
                            if (loc == null || string.IsNullOrEmpty(loc.InternalId)) continue;
                            // Skip scene files — never preload scenes as Object assets
                            if (loc.InternalId.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                            if (loc.ResourceType != null && loc.ResourceType.Name.Contains("Scene")) continue;

                            string uid = $"{loc.InternalId}|{loc.ResourceType?.FullName ?? "Obj"}";
                            if (!uniqueLocations.ContainsKey(uid))
                            {
                                uniqueLocations[uid] = loc;
                            }
                            if (!locIdList.Contains(uid))
                            {
                                locIdList.Add(uid);
                            }
                        }
                    }
                }
            }

            // 3. Load all unique locations asynchronously in parallel batches (super fast on WebGL!)
            List<KeyValuePair<string, IResourceLocation>> allLocs = new List<KeyValuePair<string, IResourceLocation>>(uniqueLocations);
            Dictionary<string, UnityEngine.Object> loadedByUid = new Dictionary<string, UnityEngine.Object>(allLocs.Count);
            Dictionary<string, Sprite[]> loadedSpritesByUid = new Dictionary<string, Sprite[]>();

            const int batchSize = 24;
            int total = Mathf.Max(1, allLocs.Count);
            int completed = 0;

            for (int start = 0; start < allLocs.Count; start += batchSize)
            {
                int end = Mathf.Min(start + batchSize, allLocs.Count);
                List<(string uid, IResourceLocation loc, AsyncOperationHandle handle, bool isSpriteSheet)> activeBatch =
                    new List<(string, IResourceLocation, AsyncOperationHandle, bool)>(end - start);

                for (int i = start; i < end; i++)
                {
                    string uid = allLocs[i].Key;
                    IResourceLocation loc = allLocs[i].Value;

                    // If location is a Sprite array/sheet or Texture/Sprite, also handle multi-sprite sheets cleanly
                    bool isSpriteType = loc.ResourceType == typeof(Sprite) || loc.ResourceType == typeof(IList<Sprite>);
                    if (isSpriteType)
                    {
                        AsyncOperationHandle<IList<Sprite>> sh = Addressables.LoadAssetAsync<IList<Sprite>>(loc);
                        activeBatch.Add((uid, loc, sh, true));
                    }
                    else
                    {
                        AsyncOperationHandle<UnityEngine.Object> oh = Addressables.LoadAssetAsync<UnityEngine.Object>(loc);
                        activeBatch.Add((uid, loc, oh, false));
                    }
                }

                // Wait for batch to finish asynchronously
                for (int i = 0; i < activeBatch.Count; i++)
                {
                    var item = activeBatch[i];
                    if (!item.handle.IsDone)
                    {
                        yield return item.handle;
                    }

                    if (item.handle.Status == AsyncOperationStatus.Succeeded && item.handle.Result != null)
                    {
                        _preloadHandles.Add(item.handle);
                        if (item.isSpriteSheet && item.handle.Result is IList<Sprite> spriteList && spriteList.Count > 0)
                        {
                            Sprite[] arr = new Sprite[spriteList.Count];
                            for (int s = 0; s < spriteList.Count; s++) arr[s] = spriteList[s];
                            loadedSpritesByUid[item.uid] = arr;
                            loadedByUid[item.uid] = arr[0];
                        }
                        else if (item.handle.Result is UnityEngine.Object uObj)
                        {
                            loadedByUid[item.uid] = uObj;
                            if (uObj is Sprite singleSp)
                            {
                                loadedSpritesByUid[item.uid] = new Sprite[] { singleSp };
                            }
                        }
                    }
                    else if (item.handle.IsValid())
                    {
                        // Retry single sprite fallback if IList<Sprite> failed on single-mode sprite
                        if (item.isSpriteSheet)
                        {
                            Addressables.Release(item.handle);
                            AsyncOperationHandle<Sprite> singleHandle = Addressables.LoadAssetAsync<Sprite>(item.loc);
                            yield return singleHandle;
                            if (singleHandle.Status == AsyncOperationStatus.Succeeded && singleHandle.Result != null)
                            {
                                _preloadHandles.Add(singleHandle);
                                loadedByUid[item.uid] = singleHandle.Result;
                                loadedSpritesByUid[item.uid] = new Sprite[] { singleHandle.Result };
                            }
                            else if (singleHandle.IsValid())
                            {
                                Addressables.Release(singleHandle);
                            }
                        }
                        else
                        {
                            Addressables.Release(item.handle);
                        }
                    }

                    completed++;
                }

                PreloadProgress = 0.08f + 0.90f * ((float)completed / total);
                yield return null;
            }

            // 4. Index every key, normalized key, filename, and label into instant O(1) lookup dictionaries
            foreach (var kvp in keyToLocationIds)
            {
                string rawKey = kvp.Key;
                string normKey = NormalizeKey(rawKey);
                string noExtKey = StripExtension(normKey);
                string fileOnly = System.IO.Path.GetFileNameWithoutExtension(normKey);
                List<string> uids = kvp.Value;

                bool isLabel = uids.Count > 1 || !rawKey.Contains("/");

                foreach (string uid in uids)
                {
                    if (loadedByUid.TryGetValue(uid, out UnityEngine.Object obj) && obj != null)
                    {
                        AddToCache(_preloadedByKey, rawKey, obj);
                        if (!string.IsNullOrEmpty(normKey)) AddToCache(_preloadedByKey, normKey, obj);
                        if (!string.IsNullOrEmpty(noExtKey)) AddToCache(_preloadedByKey, noExtKey, obj);
                        if (!string.IsNullOrEmpty(fileOnly)) AddToCache(_preloadedByFilename, fileOnly, obj);
                        if (isLabel)
                        {
                            AddToCache(_preloadedByLabel, rawKey, obj);
                        }
                    }

                    if (loadedSpritesByUid.TryGetValue(uid, out Sprite[] sprites) && sprites != null && sprites.Length > 0)
                    {
                        _preloadedSpritesByKey[rawKey] = sprites;
                        if (!string.IsNullOrEmpty(normKey)) _preloadedSpritesByKey[normKey] = sprites;
                        if (!string.IsNullOrEmpty(noExtKey)) _preloadedSpritesByKey[noExtKey] = sprites;
                        if (!string.IsNullOrEmpty(fileOnly) && !_preloadedSpritesByKey.ContainsKey(fileOnly))
                        {
                            _preloadedSpritesByKey[fileOnly] = sprites;
                        }
                    }
                }
            }

            PreloadProgress = 1f;
            IsPreloadComplete = true;
            Debug.Log($"[AddressableManager] Preload complete! Cached {loadedByUid.Count} assets across {keyToLocationIds.Count} keys/labels.");
            OnPreloadCompleted?.Invoke();
        }

        private static bool IsHexGuid(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private static string StripExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            if (dot > slash && dot > 0)
            {
                return path.Substring(0, dot);
            }
            return path;
        }

        private static void AddToCache(Dictionary<string, List<UnityEngine.Object>> dict, string key, UnityEngine.Object obj)
        {
            if (string.IsNullOrEmpty(key) || obj == null) return;
            if (!dict.TryGetValue(key, out List<UnityEngine.Object> list))
            {
                list = new List<UnityEngine.Object>(2);
                dict[key] = list;
            }
            if (!list.Contains(obj))
            {
                list.Add(obj);
            }
        }

        private static T FindInPreloadCache<T>(string normalizedKey) where T : UnityEngine.Object
        {
            if (Instance == null || string.IsNullOrEmpty(normalizedKey)) return null;

            string noExt = StripExtension(normalizedKey);
            string filenameOnly = System.IO.Path.GetFileNameWithoutExtension(normalizedKey);

            T found = TryMatchTyped<T>(Instance._preloadedByKey, normalizedKey);
            if (found != null) return found;

            if (!string.Equals(noExt, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                found = TryMatchTyped<T>(Instance._preloadedByKey, noExt);
                if (found != null) return found;
            }

            if (!string.IsNullOrEmpty(filenameOnly))
            {
                found = TryMatchTyped<T>(Instance._preloadedByKey, filenameOnly);
                if (found != null) return found;

                found = TryMatchTyped<T>(Instance._preloadedByFilename, filenameOnly);
                if (found != null) return found;
            }

            return null;
        }

        private static T TryMatchTyped<T>(Dictionary<string, List<UnityEngine.Object>> dict, string key) where T : UnityEngine.Object
        {
            if (dict == null || !dict.TryGetValue(key, out List<UnityEngine.Object> list) || list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T typed) return typed;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ClearAll();
            }
        }

        // ==========================================
        // 1. Load Single Asset Synchronously
        // ==========================================
        public static T LoadAsset<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            string normalizedKey = NormalizeKey(key);

            // 0. Instant check in preloaded WebGL/Runtime RAM cache!
            T preloaded = FindInPreloadCache<T>(normalizedKey);
            if (preloaded != null) return preloaded;

            // 1. Check in-memory handle cache
            if (Instance != null && Instance._loadedAssetHandles.TryGetValue(normalizedKey, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as T;
                }
            }

#if UNITY_EDITOR
            // 2. In Unity Editor, load directly from AssetDatabase to avoid Addressable missing-key exceptions
            T editorAsset = LoadAssetInEditor<T>(normalizedKey);
            if (editorAsset != null)
            {
                return editorAsset;
            }
#endif

            string filenameOnly = System.IO.Path.GetFileNameWithoutExtension(normalizedKey);

#if !UNITY_WEBGL
            // 3. Try Addressables WaitForCompletion only on platforms that support synchronous I/O (not WebGL!)
            if (HasAddressableKey(normalizedKey))
            {
                try
                {
                    AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(normalizedKey);
                    T result = handle.WaitForCompletion();

                    if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
                    {
                        if (Instance != null)
                        {
                            Instance._loadedAssetHandles[normalizedKey] = handle;
                        }
                        return result;
                    }
                    else if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                catch { }
            }

            // 4. Fallback: filename only in Addressables
            if (!string.Equals(filenameOnly, normalizedKey, StringComparison.OrdinalIgnoreCase) && HasAddressableKey(filenameOnly))
            {
                try
                {
                    AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(filenameOnly);
                    T result = handle.WaitForCompletion();
                    if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
                    {
                        if (Instance != null)
                        {
                            Instance._loadedAssetHandles[normalizedKey] = handle;
                            Instance._loadedAssetHandles[filenameOnly] = handle;
                        }
                        return result;
                    }
                    else if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                catch { }
            }
#endif

            // 5. Fallback: Resources.Load
            T resFallback = Resources.Load<T>(normalizedKey);
            if (resFallback != null) return resFallback;

            if (!string.Equals(filenameOnly, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                resFallback = Resources.Load<T>(filenameOnly);
                if (resFallback != null) return resFallback;
            }

            return null;
        }

        // ==========================================
        // 2. Load Single Asset Async
        // ==========================================
        public static async Task<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            string normalizedKey = NormalizeKey(key);

            T preloaded = FindInPreloadCache<T>(normalizedKey);
            if (preloaded != null) return preloaded;

            if (Instance != null && Instance._loadedAssetHandles.TryGetValue(normalizedKey, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as T;
                }
            }

#if UNITY_EDITOR
            T editorAsset = LoadAssetInEditor<T>(normalizedKey);
            if (editorAsset != null)
            {
                return editorAsset;
            }
#endif

            if (HasAddressableKey(normalizedKey))
            {
                try
                {
                    AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(normalizedKey);
                    T result = await handle.Task;

                    if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
                    {
                        if (Instance != null)
                        {
                            Instance._loadedAssetHandles[normalizedKey] = handle;
                        }
                        return result;
                    }
                    else
                    {
                        if (handle.IsValid()) Addressables.Release(handle);
                    }
                }
                catch { }
            }

            return LoadAsset<T>(key);
        }

        // ==========================================
        // 3. Load Multiple Assets by Label Synchronously
        // ==========================================
        public static IList<T> LoadAssetsByLabel<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label)) return new List<T>();

            // 0. Check preloaded RAM label cache first!
            if (Instance != null && Instance._preloadedByLabel.TryGetValue(label, out List<UnityEngine.Object> preList) && preList != null)
            {
                List<T> matched = new List<T>(preList.Count);
                for (int i = 0; i < preList.Count; i++)
                {
                    if (preList[i] is T typed && !matched.Contains(typed))
                    {
                        matched.Add(typed);
                    }
                }
                if (matched.Count > 0) return matched;
            }

            // Secondary preloaded scan by key prefix (e.g. "Stage" -> "Stages/...", "ItemData" -> "Items/...")
            if (Instance != null && Instance._preloadedByKey.Count > 0)
            {
                List<T> matched = new List<T>();
                foreach (var kvp in Instance._preloadedByKey)
                {
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        if (kvp.Value[i] is T typed && !matched.Contains(typed))
                        {
                            // Match by folder prefix when label matches asset type/folder
                            if (kvp.Key.StartsWith(label, StringComparison.OrdinalIgnoreCase) ||
                                typeof(T) == typeof(ItemData) || typeof(T) == typeof(BuildingData) ||
                                (typeof(T) == typeof(TextAsset) && kvp.Key.IndexOf("Stage", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                matched.Add(typed);
                            }
                        }
                    }
                }
                if (matched.Count > 0) return matched;
            }

            if (Instance != null && Instance._loadedLabelHandles.TryGetValue(label, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as IList<T>;
                }
            }

#if UNITY_EDITOR
            List<T> editorList = LoadAssetsByLabelInEditor<T>(label);
            if (editorList != null && editorList.Count > 0)
            {
                return editorList;
            }
#endif

#if !UNITY_WEBGL
            if (HasAddressableKey(label))
            {
                try
                {
                    AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, null);
                    IList<T> results = handle.WaitForCompletion();

                    if (handle.Status == AsyncOperationStatus.Succeeded && results != null)
                    {
                        if (Instance != null)
                        {
                            Instance._loadedLabelHandles[label] = handle;
                        }
                        return results;
                    }
                    else if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                catch { }
            }
#endif

            // Fallback: Resources.LoadAll
            T[] resAll = Resources.LoadAll<T>(label);
            if (resAll != null && resAll.Length > 0) return resAll;

            resAll = Resources.LoadAll<T>($"{label}s");
            if (resAll != null && resAll.Length > 0) return resAll;

            return new List<T>();
        }

        // ==========================================
        // 4. Load Multiple Assets by Label Async
        // ==========================================
        public static async Task<IList<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label)) return new List<T>();

            // Check preloaded label cache first
            if (Instance != null && Instance._preloadedByLabel.TryGetValue(label, out List<UnityEngine.Object> preList) && preList != null)
            {
                List<T> matched = new List<T>(preList.Count);
                for (int i = 0; i < preList.Count; i++)
                {
                    if (preList[i] is T typed && !matched.Contains(typed))
                    {
                        matched.Add(typed);
                    }
                }
                if (matched.Count > 0) return matched;
            }

            if (Instance != null && Instance._loadedLabelHandles.TryGetValue(label, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as IList<T>;
                }
            }

#if UNITY_EDITOR
            List<T> editorList = LoadAssetsByLabelInEditor<T>(label);
            if (editorList != null && editorList.Count > 0)
            {
                return editorList;
            }
#endif

            if (HasAddressableKey(label))
            {
                try
                {
                    AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, null);
                    IList<T> results = await handle.Task;

                    if (handle.Status == AsyncOperationStatus.Succeeded && results != null)
                    {
                        if (Instance != null)
                        {
                            Instance._loadedLabelHandles[label] = handle;
                        }
                        return results;
                    }
                    else if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                catch { }
            }

            // Fallback: Resources.LoadAll
            T[] resAll = Resources.LoadAll<T>(label);
            if (resAll != null && resAll.Length > 0) return resAll;

            resAll = Resources.LoadAll<T>($"{label}s");
            if (resAll != null && resAll.Length > 0) return resAll;

            return new List<T>();
        }

        // ==========================================
        // 5. Load All Sprites from Sprite Sheet
        // ==========================================
        public static Sprite[] LoadAllSprites(string key)
        {
            if (string.IsNullOrEmpty(key)) return new Sprite[0];

            string normalized = NormalizeKey(key);
            string noExt = StripExtension(normalized);
            string fileOnly = System.IO.Path.GetFileNameWithoutExtension(normalized);

            // 0. Check preloaded WebGL/Runtime sprite sheet cache first!
            if (Instance != null)
            {
                if (Instance._preloadedSpritesByKey.TryGetValue(normalized, out Sprite[] s1) && s1 != null && s1.Length > 0) return s1;
                if (Instance._preloadedSpritesByKey.TryGetValue(noExt, out Sprite[] s2) && s2 != null && s2.Length > 0) return s2;
                if (Instance._preloadedSpritesByKey.TryGetValue(fileOnly, out Sprite[] s3) && s3 != null && s3.Length > 0) return s3;
            }

#if UNITY_EDITOR
            string p1 = $"Assets/Resources_Addressable/{normalized}";
            string[] extensions = { ".png", ".jpg", ".asset" };
            foreach (var ext in extensions)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath($"{p1}{ext}");
                if (assets != null && assets.Length > 0)
                {
                    List<Sprite> sprites = new List<Sprite>();
                    foreach (var a in assets)
                    {
                        if (a is Sprite s) sprites.Add(s);
                    }
                    if (sprites.Count > 0) return sprites.ToArray();
                }
            }
#endif

            // Fallback: Resources.LoadAll
            Sprite[] resSprites = Resources.LoadAll<Sprite>(normalized);
            if (resSprites != null && resSprites.Length > 0) return resSprites;

            return new Sprite[0];
        }

        // ==========================================
        // 6. Instantiate Prefabs
        // ==========================================
        public static GameObject Instantiate(string key, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            GameObject prefab = LoadAsset<GameObject>(key);
            if (prefab == null)
            {
                Debug.LogWarning($"[AddressableManager] Could not find prefab to instantiate with key: '{key}'");
                return null;
            }

            return UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
        }

        public static async Task<GameObject> InstantiateAsync(string key, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            if (string.IsNullOrEmpty(key)) return null;

            GameObject preloaded = LoadAsset<GameObject>(key);
            if (preloaded != null)
            {
                return UnityEngine.Object.Instantiate(preloaded, position, rotation, parent);
            }

            string normalizedKey = NormalizeKey(key);

            try
            {
                AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(normalizedKey, position, rotation, parent);
                GameObject instance = await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded && instance != null)
                {
                    if (Instance != null)
                    {
                        Instance._instantiatedInstances[instance] = handle;
                    }
                    return instance;
                }
                else
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressableManager] InstantiateAsync Addressables failed for '{key}', fallback to Instantiate: {ex.Message}");
            }

            return Instantiate(key, position, rotation, parent);
        }

        // ==========================================
        // 7. Release Memory / Unload
        // ==========================================
        public static void ReleaseAsset(string key)
        {
            if (Instance == null || string.IsNullOrEmpty(key)) return;
            string normalizedKey = NormalizeKey(key);

            if (Instance._loadedAssetHandles.TryGetValue(normalizedKey, out AsyncOperationHandle handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                Instance._loadedAssetHandles.Remove(normalizedKey);
            }
        }

        public static void ReleaseLabel(string label)
        {
            if (Instance == null || string.IsNullOrEmpty(label)) return;

            if (Instance._loadedLabelHandles.TryGetValue(label, out AsyncOperationHandle handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                Instance._loadedLabelHandles.Remove(label);
            }
        }

        public static void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;

            if (Instance != null && Instance._instantiatedInstances.TryGetValue(instance, out AsyncOperationHandle handle))
            {
                Instance._instantiatedInstances.Remove(instance);
                if (handle.IsValid())
                {
                    Addressables.ReleaseInstance(instance);
                    return;
                }
            }

            UnityEngine.Object.Destroy(instance);
        }

        public void ClearAll()
        {
            foreach (var kvp in _instantiatedInstances)
            {
                if (kvp.Key != null && kvp.Value.IsValid())
                {
                    Addressables.ReleaseInstance(kvp.Key);
                }
            }
            _instantiatedInstances.Clear();

            foreach (var handle in _loadedAssetHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _loadedAssetHandles.Clear();

            foreach (var handle in _loadedLabelHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _loadedLabelHandles.Clear();

            for (int i = 0; i < _preloadHandles.Count; i++)
            {
                if (_preloadHandles[i].IsValid()) Addressables.Release(_preloadHandles[i]);
            }
            _preloadHandles.Clear();
            _preloadedByKey.Clear();
            _preloadedByFilename.Clear();
            _preloadedByLabel.Clear();
            _preloadedSpritesByKey.Clear();
            IsPreloadComplete = false;
            PreloadProgress = 0f;
            _preloadStarted = false;
        }

        public static bool HasAddressableKey(object key)
        {
            if (key == null) return false;
            try
            {
                if (Addressables.ResourceLocators != null)
                {
                    foreach (var locator in Addressables.ResourceLocators)
                    {
                        if (locator != null && locator.Locate(key, typeof(UnityEngine.Object), out var locations))
                        {
                            if (locations != null && locations.Count > 0) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string trimmed = key.Trim().Replace('\\', '/');
            if (trimmed.StartsWith("Assets/Resources_Addressable/", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring("Assets/Resources_Addressable/".Length);
            else if (trimmed.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring("Assets/Resources/".Length);
            else if (trimmed.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring("Resources/".Length);

            return trimmed;
        }

#if UNITY_EDITOR
        private static T LoadAssetInEditor<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (key.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                T direct = AssetDatabase.LoadAssetAtPath<T>(key);
                if (direct != null) return direct;
            }

            string p1 = $"Assets/Resources_Addressable/{key}";
            T obj = AssetDatabase.LoadAssetAtPath<T>(p1);
            if (obj != null) return obj;

            string[] extensions = { ".prefab", ".asset", ".png", ".jpg", ".json", ".txt" };
            foreach (var ext in extensions)
            {
                obj = AssetDatabase.LoadAssetAtPath<T>($"{p1}{ext}");
                if (obj != null) return obj;
            }

            string pAssets = $"Assets/{key}";
            obj = AssetDatabase.LoadAssetAtPath<T>(pAssets);
            if (obj != null) return obj;
            foreach (var ext in extensions)
            {
                obj = AssetDatabase.LoadAssetAtPath<T>($"{pAssets}{ext}");
                if (obj != null) return obj;
            }

            string fname = System.IO.Path.GetFileNameWithoutExtension(key);
            if (!string.IsNullOrEmpty(fname))
            {
                string[] guids = AssetDatabase.FindAssets($"{fname} t:{typeof(T).Name}");
                if (guids != null && guids.Length > 0)
                {
                    foreach (var g in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(g);
                        obj = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                        if (obj != null) return obj;
                    }
                }
            }

            return null;
        }

        private static List<T> LoadAssetsByLabelInEditor<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label)) return null;

            List<T> list = new List<T>();
            string[] searchFolders = new[] {
                $"Assets/Resources_Addressable/{label}",
                $"Assets/Resources_Addressable/{label}s",
                "Assets/Resources_Addressable"
            };

            List<string> validFolders = new List<string>();
            foreach (var f in searchFolders)
            {
                if (AssetDatabase.IsValidFolder(f)) validFolders.Add(f);
            }

            if (validFolders.Count > 0)
            {
                string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", validFolders.ToArray());
                if (guids != null && guids.Length > 0)
                {
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        T item = AssetDatabase.LoadAssetAtPath<T>(path);
                        if (item != null && !list.Contains(item))
                        {
                            list.Add(item);
                        }
                    }
                }
            }

            return list;
        }
#endif
    }
}
