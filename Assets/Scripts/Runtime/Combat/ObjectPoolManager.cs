using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class ObjectPoolManager : MonoBehaviour
    {
        public static ObjectPoolManager Instance { get; private set; }

        [System.Serializable]
        public struct Pool
        {
            public string key;
            public GameObject prefab;
            public int size;
        }

        [Header("Pool Configurations")]
        [SerializeField] private List<Pool> pools;

        private Dictionary<string, Queue<GameObject>> _poolDictionary;
        private Dictionary<string, GameObject> _prefabDictionary;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePools();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            CleanAndRebuildPools();
        }

        private void CleanAndRebuildPools()
        {
            // Destroy all currently pooled child objects
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            if (_poolDictionary != null) _poolDictionary.Clear();
            if (_prefabDictionary != null) _prefabDictionary.Clear();

            InitializePools();
            Debug.Log("[ObjectPoolManager] Pools successfully cleaned and rebuilt for the new scene.");
        }

        private void InitializePools()
        {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();
            _prefabDictionary = new Dictionary<string, GameObject>();

            foreach (Pool pool in pools)
            {
                if (pool.prefab == null || string.IsNullOrEmpty(pool.key)) continue;

                string trimmedKey = pool.key.Trim();

                if (!_prefabDictionary.ContainsKey(trimmedKey))
                {
                    _prefabDictionary.Add(trimmedKey, pool.prefab);
                }

                Queue<GameObject> objectPool = new Queue<GameObject>();

                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = Instantiate(pool.prefab);
                    obj.SetActive(false);
                    obj.transform.SetParent(transform);
                    objectPool.Enqueue(obj);
                }

                if (!_poolDictionary.ContainsKey(trimmedKey))
                {
                    _poolDictionary.Add(trimmedKey, objectPool);
                }
            }
        }

        public GameObject SpawnFromPool(string key, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(key)) return null;
            
            string trimmedKey = key.Trim();

            if (_poolDictionary == null || !_poolDictionary.ContainsKey(trimmedKey))
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool with key '{trimmedKey}' does not exist or has not been initialized.");
                return null;
            }

            Queue<GameObject> poolQueue = _poolDictionary[trimmedKey];
            GameObject objectToSpawn;

            // Dynamically grow the pool if it runs out of items
            if (poolQueue.Count == 0)
            {
                if (_prefabDictionary.TryGetValue(trimmedKey, out GameObject prefab))
                {
                    objectToSpawn = Instantiate(prefab);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                objectToSpawn = poolQueue.Dequeue();
            }

            objectToSpawn.SetActive(true);
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;

            return objectToSpawn;
        }

        public void ReturnToPool(string key, GameObject obj)
        {
            if (string.IsNullOrEmpty(key))
            {
                Destroy(obj);
                return;
            }

            string trimmedKey = key.Trim();

            if (_poolDictionary == null || !_poolDictionary.ContainsKey(trimmedKey))
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool with key '{trimmedKey}' does not exist. Destroying object instead.");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform);
            _poolDictionary[trimmedKey].Enqueue(obj);
        }
    }
}
