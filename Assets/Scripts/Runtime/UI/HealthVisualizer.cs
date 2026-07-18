using UnityEngine;
using TMPro;

namespace TheLastEmpire
{
    public class HealthVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health health;
        [SerializeField] private TMP_Text hpText;

        [Header("Settings")]
        [SerializeField] private bool showMaxHealth = true;

        private void Start()
        {
            // Auto-detect Health component in parents if not assigned
            if (health == null)
            {
                health = GetComponentInParent<Health>();
            }

            // Auto-detect TextMeshPro component on the same GameObject if not assigned
            if (hpText == null)
            {
                hpText = GetComponent<TMP_Text>();
            }

            if (health != null)
            {
                // Listen to health changes dynamically
                health.onHealthChanged.AddListener(UpdateHPText);
                
                // Initialize display value
                UpdateHPText(health.CurrentHealth);
            }
            else
            {
                Debug.LogWarning($"[HealthVisualizer] Health component not found on {gameObject.name} or its parent hierarchy.");
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.onHealthChanged.RemoveListener(UpdateHPText);
            }
        }

        private void UpdateHPText(float currentHealth)
        {
            if (hpText == null || health == null) return;

            if (showMaxHealth)
            {
                hpText.text = $"{currentHealth}/{health.MaxHealth}";
            }
            else
            {
                hpText.text = currentHealth.ToString();
            }
        }
    }
}
