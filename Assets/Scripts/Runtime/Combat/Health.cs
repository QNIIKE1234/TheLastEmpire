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
