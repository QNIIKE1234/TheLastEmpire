using UnityEngine;
using UnityEngine.Events;

namespace TheLastEmpire
{
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Settings")]
        [SerializeField] private float maxHealth = 100;
        [SerializeField] private float currentHealth = 100;
        [SerializeField] private float defaultInvulnerabilityDuration = 0.25f;

        [Header("Events")]
        public UnityEvent<float> onHealthChanged;
        public UnityEvent<float> onDamageTaken;
        public UnityEvent onDeath;

        private float _invulnerabilityTimer = 0f;

        public float MaxHealth => maxHealth;
        
        public float CurrentHealth
        {
            get => currentHealth;
            private set
            {
                currentHealth = Mathf.Clamp(value, 0, maxHealth);
                onHealthChanged?.Invoke(currentHealth);
            }
        }

        public bool IsDead => CurrentHealth <= 0;

        public void SetMaxHealth(float newMax, bool healToFull = true)
        {
            maxHealth = newMax;
            if (healToFull)
            {
                CurrentHealth = maxHealth;
            }
        }

        private void Start()
        {
            CurrentHealth = maxHealth;
        }

        private void Update()
        {
            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer -= Time.deltaTime;
            }
        }
        [Header("Effects")]
        [Tooltip("ชื่อ Pool Key สำหรับเล่นเอฟเฟกต์ตอนโดนโจมตี (เช่น GreenBlood)")]
        [SerializeField] private string hitEffectPoolKey;

        public void TriggerInvulnerability(float duration)
        {
            _invulnerabilityTimer = Mathf.Max(_invulnerabilityTimer, duration);
        }

        public void TakeDamage(float damageAmount)
        {
            if (IsDead) return;
            if (_invulnerabilityTimer > 0f) return; // Immune to damage during I-frames!

            CurrentHealth -= damageAmount;
            onDamageTaken?.Invoke(damageAmount);

            // 🟢 เล่นเอฟเฟกต์เลือด/โดนตี ถ้ามีการตั้งค่า Pool Key ไว้
            if (!string.IsNullOrEmpty(hitEffectPoolKey) && ObjectPoolManager.Instance != null)
            {
                // เลื่อนจุดเกิดขึ้นมาเหนือพื้นดินหน่อย (เช่นสูง 1.5 เมตร)
                Vector3 spawnPos = transform.position + (Vector3.up * 1.5f);
                ObjectPoolManager.Instance.SpawnFromPool(hitEffectPoolKey, spawnPos, Quaternion.identity);
            }

            // Grant brief temporary immunity to prevent rapid multi-hits
            TriggerInvulnerability(defaultInvulnerabilityDuration);

            if (IsDead)
            {
                onDeath?.Invoke();
            }
        }

        public void Heal(float healAmount)
        {
            if (IsDead) return;
            CurrentHealth += healAmount;
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            _invulnerabilityTimer = 0f; // Clear any active I-frames on reset
        }
    }
}
