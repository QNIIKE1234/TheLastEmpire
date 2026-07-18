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

        private void InitializePools()
        {
            _poolDictionary = new Dictionary<string, Queue<GameObject>>();
            _prefabDictionary = new Dictionary<string, GameObject>();

            foreach (Pool pool in pools)
            {
                if (pool.prefab == null || string.IsNullOrEmpty(pool.key)) continue;

                if (!_prefabDictionary.ContainsKey(pool.key))
                {
                    _prefabDictionary.Add(pool.key, pool.prefab);
                }

                Queue<GameObject> objectPool = new Queue<GameObject>();

                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = Instantiate(pool.prefab);
                    obj.SetActive(false);
                    obj.transform.SetParent(transform);
                    objectPool.Enqueue(obj);
                }

                _poolDictionary.Add(pool.key, objectPool);
            }
        }

        public GameObject SpawnFromPool(string key, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(key) || _poolDictionary == null || !_poolDictionary.ContainsKey(key))
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool with key '{key}' does not exist or has not been initialized.");
                return null;
            }

            Queue<GameObject> poolQueue = _poolDictionary[key];
            GameObject objectToSpawn;

            // Dynamically grow the pool if it runs out of items
            if (poolQueue.Count == 0)
            {
                if (_prefabDictionary.TryGetValue(key, out GameObject prefab))
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
            if (string.IsNullOrEmpty(key) || _poolDictionary == null || !_poolDictionary.ContainsKey(key))
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool with key '{key}' does not exist. Destroying object instead.");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform);
            _poolDictionary[key].Enqueue(obj);
        }
    }
}
