using UnityEngine;

namespace TheLastEmpire
{
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledParticle : MonoBehaviour
    {
        [Tooltip("Key ที่ตรงกับใน ObjectPoolManager เพื่อให้ดึงกลับ Pool ได้ถูกต้อง")]
        [SerializeField] private string poolKey;

        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            // สั่งให้ Particle เริ่มเล่นทุกครั้งที่โดน Spawn ออกมาจาก Pool
            if (_particleSystem != null)
            {
                _particleSystem.Play(true);
            }
        }

        private void Update()
        {
            // เช็คว่า Particle เล่นจบหรือยัง
            if (_particleSystem != null && !_particleSystem.IsAlive(true))
            {
                // ถ้าเล่นจบแล้ว ให้ส่งตัวเองกลับเข้า Pool
                if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolKey))
                {
                    ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
                }
                else
                {
                    // Fallback กรณีไม่ได้ใส่ Key หรือไม่มี Pool Manager
                    gameObject.SetActive(false);
                }
            }
        }
        
        public void SetPoolKey(string key)
        {
            poolKey = key;
        }
    }
}