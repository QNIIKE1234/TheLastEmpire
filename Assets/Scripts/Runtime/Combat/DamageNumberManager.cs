using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class DamageNumberManager : MonoBehaviour
    {
        private static DamageNumberManager _instance;

        public static DamageNumberManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DamageNumberManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[DamageNumberManager]");
                        _instance = go.AddComponent<DamageNumberManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private int prewarmCount = 25;

        private Queue<FloatingDamageNumber> _pool = new Queue<FloatingDamageNumber>();
        private Transform _container;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePool();
        }

        private void InitializePool()
        {
            if (_container == null)
            {
                GameObject containerObj = new GameObject("DamageNumberPool");
                containerObj.transform.SetParent(transform);
                _container = containerObj.transform;
            }

            for (int i = 0; i < prewarmCount; i++)
            {
                FloatingDamageNumber item = CreateNewDamageNumber();
                item.gameObject.SetActive(false);
                _pool.Enqueue(item);
            }
        }

        private FloatingDamageNumber CreateNewDamageNumber()
        {
            GameObject go = new GameObject("DamageNumber");
            go.transform.SetParent(_container != null ? _container : transform);
            FloatingDamageNumber f = go.AddComponent<FloatingDamageNumber>();
            return f;
        }

        /// <summary>
        /// Spawns a floating combat text at worldPos.
        /// </summary>
        public void SpawnDamageNumber(Vector3 worldPos, float amount, bool isCrit = false, string overrideText = null)
        {
            // Do not spawn 0 damage numbers unless it's a special status like "BLOCKED"
            if (amount <= 0f && string.IsNullOrEmpty(overrideText)) return;

            FloatingDamageNumber number;
            if (_pool.Count > 0)
            {
                number = _pool.Dequeue();
            }
            else
            {
                number = CreateNewDamageNumber();
            }

            number.Initialize(worldPos, amount, isCrit, overrideText, ReturnToPool);
        }

        private void ReturnToPool(FloatingDamageNumber item)
        {
            if (item != null)
            {
                item.transform.SetParent(_container != null ? _container : transform);
                _pool.Enqueue(item);
            }
        }
    }
}
