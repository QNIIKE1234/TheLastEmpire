using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TheLastEmpire
{
    public class AddressableManager : MonoBehaviour
    {
        public static AddressableManager Instance { get; private set; }

        // Cache loaded handles so we don't load the same asset multiple times
        private readonly Dictionary<string, AsyncOperationHandle> _loadedAssetHandles = new Dictionary<string, AsyncOperationHandle>();
        private readonly Dictionary<string, AsyncOperationHandle> _loadedLabelHandles = new Dictionary<string, AsyncOperationHandle>();
        private readonly Dictionary<GameObject, AsyncOperationHandle> _instantiatedInstances = new Dictionary<GameObject, AsyncOperationHandle>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
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
        // 1. Load Single Asset Async
        // ==========================================
        public static async Task<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (Instance != null && Instance._loadedAssetHandles.TryGetValue(key, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone)
                {
                    return cachedHandle.Result as T;
                }
            }

            try
            {
                AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
                T result = await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (Instance != null)
                    {
                        Instance._loadedAssetHandles[key] = handle;
                    }
                    return result;
                }
                else
                {
                    Debug.LogWarning($"[AddressableManager] Failed to load asset with key: '{key}'");
                    Addressables.Release(handle);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressableManager] Exception loading '{key}': {ex.Message}");
                return null;
            }
        }

        // ==========================================
        // 2. Load Multiple Assets by Label Async
        // ==========================================
        public static async Task<IList<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(label)) return new List<T>();

            if (Instance != null && Instance._loadedLabelHandles.TryGetValue(label, out AsyncOperationHandle cachedHandle))
            {
                if (cachedHandle.IsValid() && cachedHandle.IsDone)
                {
                    return cachedHandle.Result as IList<T>;
                }
            }

            try
            {
                AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, null);
                IList<T> results = await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (Instance != null)
                    {
                        Instance._loadedLabelHandles[label] = handle;
                    }
                    return results;
                }
                else
                {
                    Debug.LogWarning($"[AddressableManager] Failed to load assets with label: '{label}'");
                    Addressables.Release(handle);
                    return new List<T>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressableManager] Exception loading label '{label}': {ex.Message}");
                return new List<T>();
            }
        }

        // ==========================================
        // 3. Instantiate Addressable Prefab
        // ==========================================
        public static async Task<GameObject> InstantiateAsync(string key, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, position, rotation, parent);
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
                    Debug.LogWarning($"[AddressableManager] Failed to instantiate prefab with key: '{key}'");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddressableManager] Exception instantiating '{key}': {ex.Message}");
                return null;
            }
        }

        // ==========================================
        // 4. Release Memory / Unload
        // ==========================================
        public static void ReleaseAsset(string key)
        {
            if (Instance == null || string.IsNullOrEmpty(key)) return;

            if (Instance._loadedAssetHandles.TryGetValue(key, out AsyncOperationHandle handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                Instance._loadedAssetHandles.Remove(key);
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
            }

            Addressables.ReleaseInstance(instance);
        }

        public void ClearAll()
        {
            foreach (var kvp in _instantiatedInstances)
            {
                if (kvp.Key != null)
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
    }
}
