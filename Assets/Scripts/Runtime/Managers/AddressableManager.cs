using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TheLastEmpire
{
    public class AddressableManager : MonoBehaviour
    {
        public static AddressableManager Instance { get; private set; }

        // Cache loaded handles so we don't load the same asset multiple times
        private readonly Dictionary<string, AsyncOperationHandle> _loadedAssetHandles = new Dictionary<string, AsyncOperationHandle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AsyncOperationHandle> _loadedLabelHandles = new Dictionary<string, AsyncOperationHandle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GameObject, AsyncOperationHandle> _instantiatedInstances = new Dictionary<GameObject, AsyncOperationHandle>();

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
            }
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

            // 1. Check in-memory handle cache
            if (Instance != null && Instance._loadedAssetHandles.TryGetValue(normalizedKey, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as T;
                }
            }

            // 2. Try Addressables Synchronous Load (WaitForCompletion)
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
                else
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
            }
            catch
            {
                // Key not found in Addressables catalog; proceed to fallbacks
            }

            // 3. Fallback: If normalizedKey has folder, try filename only in Addressables
            string filenameOnly = System.IO.Path.GetFileNameWithoutExtension(normalizedKey);
            if (!string.Equals(filenameOnly, normalizedKey, StringComparison.OrdinalIgnoreCase))
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

#if UNITY_EDITOR
            // 4. Editor Fallback: Direct search in AssetDatabase
            T editorAsset = LoadAssetInEditor<T>(normalizedKey);
            if (editorAsset != null)
            {
                return editorAsset;
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

            if (Instance != null && Instance._loadedAssetHandles.TryGetValue(normalizedKey, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as T;
                }
            }

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
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressableManager] Addressables async failed for '{key}', checking fallbacks: {ex.Message}");
            }

            // Fallback to synchronous loader (Editor / Resources)
            return LoadAsset<T>(key);
        }

        // ==========================================
        // 3. Load Multiple Assets by Label Synchronously
        // ==========================================
        public static IList<T> LoadAssetsByLabel<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label)) return new List<T>();

            if (Instance != null && Instance._loadedLabelHandles.TryGetValue(label, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as IList<T>;
                }
            }

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
                else
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                    return new List<T>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressableManager] Exception loading label '{label}': {ex.Message}");
                return new List<T>();
            }
        }

        // ==========================================
        // 4. Load Multiple Assets by Label Async
        // ==========================================
        public static async Task<IList<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label)) return new List<T>();

            if (Instance != null && Instance._loadedLabelHandles.TryGetValue(label, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone && cachedHandle.Result != null)
                {
                    return cachedHandle.Result as IList<T>;
                }
            }

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
                else
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                    return new List<T>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressableManager] Exception loading label '{label}': {ex.Message}");
                return new List<T>();
            }
        }

        // ==========================================
        // 5. Load All Sprites from Sprite Sheet
        // ==========================================
        public static Sprite[] LoadAllSprites(string key)
        {
            if (string.IsNullOrEmpty(key)) return new Sprite[0];

            string normalized = NormalizeKey(key);

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

            // Fallback
            return Instantiate(key, position, rotation, parent);
        }

        // ==========================================
        // 6. Release Memory / Unload
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
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string trimmed = key.Trim().Replace('\\', '/');
            // Remove leading "Assets/Resources/" or "Assets/Resources_Addressable/" or "Resources/" if present
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
            // Try direct path in Assets/Resources_Addressable
            string p1 = $"Assets/Resources_Addressable/{key}";
            T obj = AssetDatabase.LoadAssetAtPath<T>(p1);
            if (obj != null) return obj;

            // Try with extensions
            string[] extensions = { ".prefab", ".asset", ".png", ".jpg", ".json", ".txt" };
            foreach (var ext in extensions)
            {
                obj = AssetDatabase.LoadAssetAtPath<T>($"{p1}{ext}");
                if (obj != null) return obj;
            }

            // Search by filename in Resources_Addressable
            string fname = System.IO.Path.GetFileNameWithoutExtension(key);
            string[] guids = AssetDatabase.FindAssets($"{fname} t:{typeof(T).Name}", new[] { "Assets/Resources_Addressable" });
            if (guids != null && guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                obj = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (obj != null) return obj;
            }

            return null;
        }
#endif
    }
}
