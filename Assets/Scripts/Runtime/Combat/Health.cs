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

        public void TakeDamage(float damageAmount, UnityEngine.Vector3? hitPoint = null, bool isCrit = false)
        {
            TakeDamage(damageAmount, hitPoint, isCrit, false);
        }

        public void TakeExplosiveDamage(float damageAmount, UnityEngine.Vector3? hitPoint = null, bool isCrit = false)
        {
            TakeDamage(damageAmount, hitPoint, isCrit, true);
        }

        public void TakeDamage(float damageAmount, UnityEngine.Vector3? hitPoint, bool isCrit, bool isExplosion)
        {
            if (IsDead) return;
            if (_invulnerabilityTimer > 0f) return; // Immune to damage during I-frames!

            // หากเครื่อง Client ยิงหรือฟันใส่ศัตรู Puppet ที่ซิงค์มาจาก Host ให้ส่งคำขอดาเมจไปให้ Host คำนวณและซิงค์เลือด/การตายกลับมา
            if (TheLastEmpire.Network.EmpireNetworkManager.IsOnlineClient)
            {
                BaseEnemyAI enemyAi = GetComponent<BaseEnemyAI>();
                if (enemyAi != null && enemyAi.IsNetworkPuppet && enemyAi.NetworkEnemyId > 0)
                {
                    TheLastEmpire.Network.NetworkEnemyBridge.Instance?.SendClientDamageToHost(enemyAi.NetworkEnemyId, damageAmount, isCrit);

                    // แสดงตัวเลขดาเมจและเอฟเฟกต์เลือดทันทีบนเครื่อง Client (0ms Visual Feedback)
                    Vector3 clientDmgPos = hitPoint ?? (transform.position + Vector3.up * 1.3f);
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.SpawnDamageNumber(clientDmgPos, damageAmount, isCrit);
                    }
                    if (!string.IsNullOrEmpty(hitEffectPoolKey) && ObjectPoolManager.Instance != null)
                    {
                        Vector3 spawnPos = hitPoint ?? (transform.position + (Vector3.up * 1.5f));
                        GameObject hitEffect = ObjectPoolManager.Instance.SpawnFromPool(hitEffectPoolKey, spawnPos, Quaternion.identity);
                        if (hitEffect != null)
                        {
                            PooledParticle pooledParticle = hitEffect.GetComponent<PooledParticle>();
                            if (pooledParticle != null) pooledParticle.SetPoolKey(hitEffectPoolKey);
                        }
                    }
                    return;
                }
            }

            // Player character defense traits & abilities
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null)
            {
                if (pc.ActiveShiftAbility is AdrenalineRushAbility && pc.ActiveShiftAbility.IsActive)
                {
                    float reduction = pc.ActiveCharacterData != null ? pc.ActiveCharacterData.adrenalineDamageReduction : 0.20f;
                    damageAmount *= (1f - reduction); // Configured damage reduction during Adrenaline Rush
                }
                if (pc.ActiveShiftAbility is CycloneSwingAbility && pc.ActiveShiftAbility.IsActive)
                {
                    float reduction = pc.ActiveCharacterData != null ? pc.ActiveCharacterData.cycloneDamageReduction : 0.40f;
                    damageAmount *= (1f - reduction);
                }
                if (pc.ActiveShiftAbility is ShadowStepAbility && pc.ActiveShiftAbility.IsActive)
                {
                    float reduction = (pc.ActiveCharacterData != null && pc.ActiveCharacterData.shadowDamageReduction > 0f)
                        ? pc.ActiveCharacterData.shadowDamageReduction
                        : 0.50f;
                    damageAmount *= (1f - reduction); // 50% damage reduction during Shadow Step
                }
                if (isExplosion && pc.ActiveCharacterData != null && pc.ActiveCharacterData.blastDamageReduction > 0f)
                {
                    damageAmount *= (1f - pc.ActiveCharacterData.blastDamageReduction);
                }
                if (pc.ActiveCharacterData != null && pc.ActiveCharacterData.damageReductionPercent > 0f)
                {
                    damageAmount *= (1f - pc.ActiveCharacterData.damageReductionPercent); // Passive armor (e.g. Juggernaut 15%)
                }
            }

            CurrentHealth -= damageAmount;
            onDamageTaken?.Invoke(damageAmount);

            // 💥 แสดงตัวเลขดาเมจลอยขึ้นเหนือเป้าหมาย (Floating Damage Text)
            Vector3 dmgNumberPos = hitPoint ?? (transform.position + Vector3.up * 1.3f);
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(dmgNumberPos, damageAmount, isCrit);
            }
            TheLastEmpire.Network.NetworkLootManager.BroadcastFloatingDamageNumber(dmgNumberPos, damageAmount, isCrit);

            // 🟢 เล่นเอฟเฟกต์เลือด/โดนตี ถ้ามีการตั้งค่า Pool Key ไว้
            if (!string.IsNullOrEmpty(hitEffectPoolKey) && ObjectPoolManager.Instance != null)
            {
                // ถ้ามีตำแหน่งที่ชนส่งมา ให้ใช้ตำแหน่งนั้น ถ้าไม่มีให้ดึงตำแหน่งตัวละคร + ความสูง 1.5 เมตร
                Vector3 spawnPos = hitPoint ?? (transform.position + (Vector3.up * 1.5f));
                GameObject hitEffect = ObjectPoolManager.Instance.SpawnFromPool(hitEffectPoolKey, spawnPos, Quaternion.identity);
                
                if (hitEffect != null)
                {
                    PooledParticle pooledParticle = hitEffect.GetComponent<PooledParticle>();
                    if (pooledParticle != null)
                    {
                        pooledParticle.SetPoolKey(hitEffectPoolKey);
                    }
                }
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

        /// <summary>
        /// อัปเดตค่าเลือดจากแพ็กเก็ตเครือข่ายโดยตรง (ไม่วนลูปส่งดาเมจซ้ำหรือเสก VFX ซ้ำ)
        /// </summary>
        public void SetHealthFromNetwork(float newCurrentHealth, float newMaxHealth)
        {
            if (newMaxHealth > 0f)
            {
                maxHealth = newMaxHealth;
            }
            CurrentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);
        }
    }
}
