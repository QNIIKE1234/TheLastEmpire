using System;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastEmpire
{
    /// <summary>
    /// BuildingObject — Component หลักที่ติดอยู่กับสิ่งปลูกสร้างจริงทุกชิ้นในฉาก
    /// ควบคุม:
    /// - เลือด HP และการรับความเสียหาย (IDamageable)
    /// - NavMeshObstacle อัตโนมัติ เพื่อให้ศัตรูไม่เดินทะลุ
    /// - การซ่อมแซม (Repair) และการรื้อถอน (Dismantle) คืนวัตถุดิบ 50%
    /// </summary>
    [SelectionBase]
    public class BuildingObject : MonoBehaviour, IDamageable
    {
        [Header("Building Data")]
        [SerializeField] private BuildingData data;
        [SerializeField] private float currentHealth = 250f;

        [Header("Stage Coordinates")]
        [Tooltip("พิกัด Stage X ที่สิ่งปลูกสร้างนี้ถูกสร้างขึ้น")]
        [SerializeField] private int stageX;
        [Tooltip("พิกัด Stage Y ที่สิ่งปลูกสร้างนี้ถูกสร้างขึ้น")]
        [SerializeField] private int stageY;
        [Tooltip("รหัสห้องภายในบ้าน (0 = ด้านนอก, >0 = ภายในตึก/ชั้น) สำหรับแยกการแสดงผลและซิงค์ Co-op")]
        [SerializeField] private int interiorId;

        public int StageX => stageX;
        public int StageY => stageY;
        public int InteriorId => interiorId;

        public void SetStageCoordinates(int x, int y, int intId = 0)
        {
            stageX = x;
            stageY = y;
            interiorId = intId;
        }

        [Header("Health Bar Settings")]
        [Tooltip("แสดงหลอดเลือดลอยเหนือสิ่งปลูกสร้างหรือไม่ (ค่าเริ่มต้น: false เพื่อคงรูปแบบเดิม)")]
        [SerializeField] private bool showHealthBar = false;

        public bool ShowHealthBar
        {
            get => showHealthBar;
            set => showHealthBar = value;
        }

        [Header("NavMesh Settings")]
        [Tooltip("เพิ่ม NavMeshObstacle อัตโนมัติหรือไม่ เพื่อให้มอนสเตอร์เดินอ้อมสิ่งปลูกสร้างนี้")]
        [SerializeField] private bool autoAddNavMeshObstacle = true;

        public bool AutoAddNavMeshObstacle
        {
            get => autoAddNavMeshObstacle;
            set => autoAddNavMeshObstacle = value;
        }

        [Header("Dismantle Settings")]
        [Tooltip("เปอร์เซ็นต์วัตถุดิบที่จะได้คืนเมื่อผู้เล่นรื้อถอน (ค่าเริ่มต้น 50%)")]
        [Range(0f, 1f)]
        [SerializeField] private float dismantleRefundRatio = 0.5f;

        [Header("Damage Settings")]
        [Tooltip("อนุญาตให้ผู้เล่นโจมตีทำลายสิ่งปลูกสร้างชิ้นนี้ได้ด้วยอาวุธทั่วไปหรือไม่ (ค่าเริ่มต้น: false เพื่อป้องกันการเผลอฟันพัง)")]
        [SerializeField] private bool canBeDamagedByPlayer = false;

        public bool CanBeDamagedByPlayer
        {
            get => canBeDamagedByPlayer;
            set => canBeDamagedByPlayer = value;
        }

        [Header("Audio & VFX")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private GameObject destroyVfxPrefab;

        private NavMeshObstacle _navObstacle;
        private bool _isDestroyed = false;

        public BuildingData Data => data;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => data != null ? data.maxHealth : 250f;
        public bool IsDead => _isDestroyed || currentHealth <= 0f;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action OnDestroyed;

        public void Initialize(BuildingData buildingData)
        {
            data = buildingData;
            if (data != null)
            {
                currentHealth = data.maxHealth;
            }
        }

        private void Awake()
        {
            if (data != null && currentHealth <= 0f)
            {
                currentHealth = data.maxHealth;
            }

            if (stageX == 0 && stageY == 0 && WorldMapManager.Instance != null)
            {
                stageX = WorldMapManager.Instance.CurrentPlayerX;
                stageY = WorldMapManager.Instance.CurrentPlayerY;
            }

            SetupNavMeshObstacle();
            EnsureHealthDisplay();
            ApplyMatteMaterialFinish();
        }

        private static Texture2D _runtimeWeatheredDetailTex;

        private static Texture2D GetRuntimeWeatheredDetailTex()
        {
            if (_runtimeWeatheredDetailTex != null) return _runtimeWeatheredDetailTex;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "Craft_Runtime_Weathered_Detail";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n1 = Mathf.PerlinNoise(u * 6f, v * 6f);
                    float n2 = Mathf.PerlinNoise(u * 14f + 3.7f, v * 14f + 6.1f);
                    float n3 = Mathf.PerlinNoise(u * 28f + 7.3f, v * 28f + 1.9f);
                    float n = n1 * 0.50f + n2 * 0.32f + n3 * 0.18f;
                    float t = Mathf.Clamp01((n - 0.25f) / 0.53f);
                    float shade = Mathf.Lerp(0.80f, 1.0f, t);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(shade * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            _runtimeWeatheredDetailTex = tex;
            return _runtimeWeatheredDetailTex;
        }

        /// <summary>
        /// ปรับค่า Smoothness, Metallic และ Specular ของวัสดุสิ่งปลูกสร้างให้ตรงกับโมเดลตัวละคร Chibi_TacticalGirl_Rigged 100%
        /// </summary>
        private void ApplyMatteMaterialFinish()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null) return;

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null || rend is LineRenderer || rend is ParticleSystemRenderer) continue;

                Material[] mats = rend.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;

                    string matName = mat.name;
                    bool isGlassOrWater = matName.Contains("Glass") || matName.Contains("Water") || matName.Contains("Plasma") || matName.Contains("Ember");
                    bool isSilver = matName.Contains("bright bevel") || matName.Contains("scraped edges");
                    bool isArmor = matName.Contains("graphite armor") || matName.Contains("dark forged steel");

                    float smoothness = isGlassOrWater ? 0.45f : (isSilver ? 0.48f : (isArmor ? 0.18f : 0.08f));
                    float metallic = isSilver ? 0.70f : (isArmor ? 0.08f : 0.0f);

                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                    if (mat.HasProperty("_SpecColor") && !isGlassOrWater && !isSilver)
                        mat.SetColor("_SpecColor", new Color(0.05f, 0.05f, 0.05f, 1f));
                }
            }
        }

        private void EnsureHealthDisplay()
        {
            if (showHealthBar && GetComponent<BuildingHealthDisplay>() == null)
            {
                gameObject.AddComponent<BuildingHealthDisplay>();
            }
        }

        private void Start()
        {
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.TrackBuilding(this);
            }
        }

        private void OnDestroy()
        {
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.UntrackBuilding(this);
            }
        }

        private void SetupNavMeshObstacle()
        {
            if (!autoAddNavMeshObstacle) return;

            _navObstacle = GetComponent<NavMeshObstacle>();
            if (_navObstacle == null)
            {
                _navObstacle = gameObject.AddComponent<NavMeshObstacle>();
            }

            _navObstacle.carving = true;
            _navObstacle.carveOnlyStationary = false;

            if (data != null)
            {
                Vector3 worldCenter = Vector3.up * (data.footprintSize.y * 0.5f);
                _navObstacle.center = transform.InverseTransformDirection(worldCenter);

                Vector3 worldSize = data.footprintSize;
                Vector3 localX = transform.InverseTransformDirection(Vector3.right);
                Vector3 localY = transform.InverseTransformDirection(Vector3.up);
                Vector3 localZ = transform.InverseTransformDirection(Vector3.forward);

                _navObstacle.size = new Vector3(
                    Mathf.Abs(localX.x) * worldSize.x + Mathf.Abs(localY.x) * worldSize.y + Mathf.Abs(localZ.x) * worldSize.z,
                    Mathf.Abs(localX.y) * worldSize.x + Mathf.Abs(localY.y) * worldSize.y + Mathf.Abs(localZ.y) * worldSize.z,
                    Mathf.Abs(localX.z) * worldSize.x + Mathf.Abs(localY.z) * worldSize.y + Mathf.Abs(localZ.z) * worldSize.z
                );
            }
            else
            {
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    _navObstacle.size = col.bounds.size;
                    _navObstacle.center = col.bounds.center - transform.position;
                }
            }
        }

        // =========================================================================
        //  IDamageable Implementation
        // =========================================================================
        public void TakeDamage(float damageAmount, Vector3? hitPoint = null, bool isCrit = false)
        {
            if (IsDead) return;

            float resistance = data != null ? data.damageResistance : 0f;
            float actualDamage = Mathf.Max(1f, damageAmount * (1f - resistance));

            currentHealth = Mathf.Max(0f, currentHealth - actualDamage);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            Vector3 spawnPos = hitPoint ?? (transform.position + Vector3.up * 1.0f);

            // แสดงตัวเลขดาเมจบนจอ
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(spawnPos, actualDamage, isCrit);
            }

            if (hitVfxPrefab != null)
            {
                Instantiate(hitVfxPrefab, spawnPos, Quaternion.identity);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Repair(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private void Die()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            if (destroyVfxPrefab != null)
            {
                Instantiate(destroyVfxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            OnDestroyed?.Invoke();
            TheLastEmpire.Network.NetworkBuildingSyncManager.NotifyLocalBuildingDestroyed(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// รื้อถอนสิ่งปลูกสร้าง และคืนวัตถุดิบ 50% เข้ากระเป๋าผู้เล่น
        /// </summary>
        public void Dismantle(PlayerInventory playerInventory)
        {
            if (IsDead) return;

            if (playerInventory != null && data != null && data.ingredients != null)
            {
                foreach (CraftingIngredient ing in data.ingredients)
                {
                    int refundAmount = Mathf.Max(1, Mathf.FloorToInt(ing.amount * dismantleRefundRatio));
                    playerInventory.AddItem(ing.ResolvedItemName, refundAmount);
                    Debug.Log($"[BuildingObject] Dismantled '{data.buildingName}': Refunded '{ing.ResolvedItemName}' x{refundAmount}");
                }
            }

            Die();
        }
    }
}

