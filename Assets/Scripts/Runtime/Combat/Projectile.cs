using UnityEngine;

namespace TheLastEmpire
{
    public enum TrajectoryMode
    {
        Straight,
        Arced
    }

    [System.Serializable]
    public class ProjectileVisualEntry
    {
        [Tooltip("ID หรือชื่อของกระสุน/ระเบิด เช่น ITEM0008_Grenade, ITEM0009_Bullet, ITEM0010_Arrow หรือ Grenade, Rifle, Crossbow")]
        public string id;

        [Tooltip("GameObject โมเดลที่จะเปิดใช้งาน (เปิดตา) เมื่อไอดีตรงกัน")]
        public GameObject visualObject;

        public ProjectileVisualEntry() {}

        public ProjectileVisualEntry(string id, GameObject visualObject)
        {
            this.id = id;
            this.visualObject = visualObject;
        }
    }

    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [Header("Visual Models Mapping")]
        [Tooltip("รายการจับคู่ ID กับ GameObject โมเดล เพื่อเปิดตา (SetActive) เฉพาะโมเดลที่ตรงกับกระสุนหรือระเบิดที่ยิง")]
        [SerializeField] private System.Collections.Generic.List<ProjectileVisualEntry> visualEntries = new System.Collections.Generic.List<ProjectileVisualEntry>();

        public System.Collections.Generic.List<ProjectileVisualEntry> VisualEntries => visualEntries;

        [Header("Settings")]
        [SerializeField] private float speed = 50f;
        [SerializeField] private float damage = 20;
        [SerializeField] private float decreseMulti = 0.1f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private string poolKey = "";

        [Header("Explosive Settings")]
        [SerializeField] private bool isExplosive = false;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private string explosionVfxPoolKey = "EnemyExplosion";
        [SerializeField] private float explosionVfxScaleMultiplier = 1.0f;

        [Header("Trajectory Settings")]
        [SerializeField] private TrajectoryMode trajectoryMode = TrajectoryMode.Straight;
        [SerializeField] private float arcHeight = 2.5f;

        [Header("Bullet Trail VFX")]
        [SerializeField] private bool useTrailRenderer = true;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.35f, 0.9f);
        [SerializeField] private float trailTime = 0.08f;
        [SerializeField] private float trailStartWidth = 0.15f;

        [Header("Bullet Front Light")]
        [SerializeField] private bool useBulletLight = true;
        [SerializeField] private Light bulletLight;
        [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private float lightRange = 6f;
        [SerializeField] private float lightIntensity = 2.5f;
        [SerializeField] private Vector3 lightOffset = new Vector3(0f, 0f, 0.6f);

        [Header("Impact Spark VFX")]
        [SerializeField] private string obstacleHitVfxPoolKey = "PistolFlash";

        private Rigidbody _rb;
        private GameObject _owner;
        private float _lifeTimer;
        private TrailRenderer _trail;

        // Arced trajectory variables
        private Vector3 _startPoint;
        private Vector3 _targetPoint;
        private float _flightDuration;
        private float _currentFlightTime;

        // Dynamic overrides set by Weapon stats (reset per-shot so pooled bullets stay clean)
        private float _activeDamage;
        private float _activeLifetime;
        private float _activeSpeed;
        private bool _activeIsExplosive;
        private float _activeExplosionRadius;
        private string _activeExplosionVfxPoolKey;
        private bool _canPierce = false;
        private bool _isVisualOnly = false;
        private bool _isEnemyProjectile = false;
        private string _weaponName = "";
        private float _maxFalloffDistance = DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;
        private float _bonusKnockback = 0f;
        private float _bonusStagger = 0f;
        private float _lifestealOnHit = 0f;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _hasBaseLocalScale = false;
        private System.Collections.Generic.List<IDamageable> _hitTargets = new System.Collections.Generic.List<IDamageable>();

        public string PoolKey
        {
            get => poolKey;
            set => poolKey = value;
        }

        public float Speed => _activeSpeed > 0f ? _activeSpeed : speed;

        /// <summary>
        /// ดึงความเร็วกระสุนจริงของปืนพก (Pistol -> BulletPrefabs) เพื่อให้ Spitter ใช้ความเร็วเท่าปืนพกเสมอ
        /// </summary>
        public static float ResolvePistolProjectileSpeed(float fallbackSpeed = 50f)
        {
            if (ItemDatabase.Instance != null)
            {
                ItemData pistol = ItemDatabase.Instance.GetItemByName("Pistol");
                if (pistol != null && pistol.weaponConfig != null && pistol.weaponConfig.projectilePrefab != null)
                {
                    float s = pistol.weaponConfig.projectilePrefab.Speed;
                    if (s > 0.1f) return s;
                }
            }
            return fallbackSpeed;
        }

        public void SetSpeed(float newSpeed)
        {
            if (newSpeed > 0.1f)
            {
                speed = newSpeed;
                _activeSpeed = newSpeed;
                if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.001f)
                {
                    _rb.linearVelocity = _rb.linearVelocity.normalized * newSpeed;
                }
            }
        }
        public bool IsExplosive => _activeIsExplosive;
        public float ExplosionRadius => _activeExplosionRadius > 0f ? _activeExplosionRadius : explosionRadius;
        public string ExplosionVfxPoolKey => !string.IsNullOrEmpty(_activeExplosionVfxPoolKey) ? _activeExplosionVfxPoolKey : explosionVfxPoolKey;
        public TrajectoryMode Trajectory => trajectoryMode;
        public GameObject Owner => _owner;
        public Light BulletLight => bulletLight;

        public void SetBulletLight(Color color, float range, float intensity)
        {
            lightColor = color;
            lightRange = range;
            lightIntensity = intensity;
            if (bulletLight != null)
            {
                bulletLight.color = color;
                bulletLight.range = range;
                bulletLight.intensity = intensity;
            }
        }

        private void SetupBulletLight()
        {
            if (!useBulletLight)
            {
                if (bulletLight != null) bulletLight.enabled = false;
                return;
            }

            if (bulletLight == null)
            {
                bulletLight = GetComponentInChildren<Light>();
            }

            if (bulletLight == null)
            {
                GameObject lightObj = new GameObject("FrontLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = lightOffset;
                bulletLight = lightObj.AddComponent<Light>();
                bulletLight.type = LightType.Point;
                bulletLight.shadows = LightShadows.None;
            }

            bulletLight.color = lightColor;
            bulletLight.range = lightRange;
            bulletLight.intensity = lightIntensity;
            bulletLight.shadows = LightShadows.None;
            bulletLight.transform.localPosition = lightOffset;
            bulletLight.enabled = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (bulletLight == null)
            {
                bulletLight = GetComponentInChildren<Light>();
            }
            if (bulletLight != null)
            {
                bulletLight.color = lightColor;
                bulletLight.range = lightRange;
                bulletLight.intensity = lightIntensity;
                bulletLight.shadows = LightShadows.None;
                bulletLight.transform.localPosition = lightOffset;
            }

            // ถ้า visualEntries ยังว่างอยู่ ให้พยายาม Auto-discover จาก Child Models
            if (visualEntries == null || visualEntries.Count == 0)
            {
                AutoPopulateVisualEntriesFromChildren();
            }
        }

        public void AutoPopulateVisualEntriesFromChildren()
        {
            if (visualEntries == null) visualEntries = new System.Collections.Generic.List<ProjectileVisualEntry>();
            if (visualEntries.Count > 0) return;

            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == transform) continue;
                if (child.name.Equals("Sphere", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (child.name.Equals("FrontLight", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (child.name.Equals("default", System.StringComparison.OrdinalIgnoreCase)) continue;

                // ตรวจสอบว่า child ตัวนี้หรือลูกของมันมี Renderer
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    bool alreadyHasParent = false;
                    foreach (var entry in visualEntries)
                    {
                        if (entry != null && entry.visualObject != null && child.IsChildOf(entry.visualObject.transform))
                        {
                            alreadyHasParent = true;
                            break;
                        }
                    }

                    if (!alreadyHasParent)
                    {
                        visualEntries.Add(new ProjectileVisualEntry(child.name, child.gameObject));
                    }
                }
            }
        }

        [ContextMenu("Auto Populate Visual Entries from Children")]
        private void ContextMenuAutoPopulate()
        {
            if (visualEntries == null) visualEntries = new System.Collections.Generic.List<ProjectileVisualEntry>();
            visualEntries.Clear();
            AutoPopulateVisualEntriesFromChildren();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public void Deflect(Vector3 newDirection, GameObject newOwner)
        {
            _owner = newOwner;
            if (_hitTargets == null) _hitTargets = new System.Collections.Generic.List<IDamageable>();
            _hitTargets.Clear();
            if (_rb != null)
            {
                _rb.linearVelocity = newDirection.normalized * speed;
            }
            if (newDirection.sqrMagnitude > 0.01f)
            {
                transform.forward = newDirection.normalized;
            }
        }

        private void Awake()
        {
            _baseLocalScale = transform.localScale;
            _hasBaseLocalScale = true;
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            SetupTrailRenderer();
            SetupBulletLight();

            // Set initial defaults
            ResetDynamicOverrides();
        }

        private void ResetDynamicOverrides()
        {
            if (!_hasBaseLocalScale)
            {
                _baseLocalScale = transform.localScale;
                _hasBaseLocalScale = true;
            }
            transform.localScale = _baseLocalScale;
            _activeDamage = damage;
            _activeLifetime = lifetime;
            _activeSpeed = speed;
            _activeIsExplosive = isExplosive;
            _activeExplosionRadius = explosionRadius;
            _activeExplosionVfxPoolKey = explosionVfxPoolKey;
            _bonusKnockback = 0f;
            _bonusStagger = 0f;
            _lifestealOnHit = 0f;
            _isVisualOnly = false;
            _isEnemyProjectile = false;
            if (_trail != null)
            {
                _trail.enabled = useTrailRenderer;
                _trail.startWidth = trailStartWidth;
                _trail.time = trailTime;
            }
            if (bulletLight != null && useBulletLight)
            {
                bulletLight.enabled = true;
                bulletLight.color = lightColor;
                bulletLight.range = lightRange;
                bulletLight.intensity = lightIntensity;
            }
        }

        public void SetArtifactImpactModifiers(float bonusKnockback, float bonusStagger, float lifestealOnHit, float projectileScaleBonus = 0f)
        {
            _bonusKnockback = Mathf.Max(0f, bonusKnockback);
            _bonusStagger = Mathf.Max(0f, bonusStagger);
            _lifestealOnHit = Mathf.Max(0f, lifestealOnHit);

            if (!_hasBaseLocalScale)
            {
                _baseLocalScale = transform.localScale;
                _hasBaseLocalScale = true;
            }

            float scaleMult = 1f + Mathf.Max(0f, projectileScaleBonus);
            transform.localScale = _baseLocalScale * scaleMult;
            if (_trail != null && scaleMult > 1.01f)
            {
                _trail.startWidth = trailStartWidth * (1f + (scaleMult - 1f) * 0.6f);
            }
        }

        /// <summary>
        /// Dynamically configures this pooled projectile as a slow, long-range explosive rocket
        /// (resets cleanly back to prefab defaults on next shot / pool reuse).
        /// </summary>
        public void ConfigureExplosiveOverride(float overrideSpeed, float overrideExplosionRadius, string overrideExplosionVfxKey = "Explosion")
        {
            if (overrideSpeed > 0.1f) _activeSpeed = overrideSpeed;
            _activeIsExplosive = true;
            if (overrideExplosionRadius > 0.1f) _activeExplosionRadius = overrideExplosionRadius;
            if (!string.IsNullOrEmpty(overrideExplosionVfxKey)) _activeExplosionVfxPoolKey = overrideExplosionVfxKey;

            if (_trail != null)
            {
                _trail.startWidth = Mathf.Max(trailStartWidth, 0.28f);
                _trail.time = Mathf.Max(trailTime, 0.22f);
            }
            if (bulletLight != null)
            {
                bulletLight.color = new Color(1f, 0.42f, 0.1f, 1f);
                bulletLight.range = Mathf.Max(lightRange, 8f);
                bulletLight.intensity = Mathf.Max(lightIntensity, 4f);
                bulletLight.enabled = true;
            }
        }

        private void SetupTrailRenderer()
        {
            if (!useTrailRenderer) return;

            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();
                _trail.time = trailTime;
                _trail.startWidth = trailStartWidth;
                _trail.endWidth = 0.0f;
                _trail.autodestruct = false;

                if (trailMaterial != null)
                {
                    _trail.material = trailMaterial;
                }
                else
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
                    if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                    if (shader != null)
                    {
                        _trail.material = new Material(shader);
                    }
                }

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(Color.white, 0.0f),
                        new GradientColorKey(trailColor, 0.35f),
                        new GradientColorKey(new Color(1f, 0.45f, 0.05f), 1.0f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.9f, 0.0f),
                        new GradientAlphaKey(0.7f, 0.5f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                _trail.colorGradient = gradient;
            }
        }

        private string _activeVisualId = "";

        private void OnEnable()
        {
            if (_trail != null)
            {
                _trail.Clear();
            }
            if (bulletLight != null && useBulletLight)
            {
                bulletLight.enabled = true;
            }
            _lifeTimer = _activeLifetime;
            _currentFlightTime = 0f;
            _owner = null;
            _isVisualOnly = false;
            _isEnemyProjectile = false;
            if (_hitTargets == null)
            {
                _hitTargets = new System.Collections.Generic.List<IDamageable>();
            }
            _hitTargets.Clear();

            // นำโมเดล Visual กลับมาแสดงผลตามที่ตั้งค่าไว้
            if (!string.IsNullOrEmpty(_activeVisualId))
            {
                ApplyVisual(_activeVisualId);
            }
            else if (visualEntries != null && visualEntries.Count > 0 && visualEntries[0] != null && visualEntries[0].visualObject != null)
            {
                ApplyVisual(visualEntries[0].id);
            }
        }

        /// <summary>
        /// สลับเปิด/ปิด GameObject โมเดลตามไอดี (เช่น Bullet, Arrow, Grenade หรือ ITEM0009_Bullet, ITEM0010_Arrow)
        /// เปิดตา GO ที่ตรงกับไอดี และปิดตา GO ตัวอื่นๆ ทั้งหมด
        /// </summary>
        public void ApplyVisual(string visualId)
        {
            if (string.IsNullOrEmpty(visualId)) return;
            _activeVisualId = visualId;

            // โหมดซ่อนโมเดลกระสุน (เช่น Flamethrower ที่ใช้ Particle Effect เปลวไฟทรงกรวยแทนลูกกระสุน)
            if (visualId.Equals("FlameCone", System.StringComparison.OrdinalIgnoreCase) ||
                visualId.Equals("Hidden", System.StringComparison.OrdinalIgnoreCase))
            {
                HideAllVisualsForInvisibleHitbox();
                return;
            }

            string cleanTarget = visualId.Trim().ToLower();
            string normTarget = NormalizeVisualKey(cleanTarget);
            bool matchedAny = false;

            // 1. ตรวจสอบจาก visualEntries ใน Inspector ก่อน
            if (visualEntries != null && visualEntries.Count > 0)
            {
                int bestMatchIdx = -1;

                // Match pass 1: Exact match on id
                for (int i = 0; i < visualEntries.Count; i++)
                {
                    var entry = visualEntries[i];
                    if (entry == null || entry.visualObject == null || string.IsNullOrEmpty(entry.id)) continue;

                    string entryIdClean = entry.id.Trim().ToLower();
                    if (entryIdClean == cleanTarget)
                    {
                        bestMatchIdx = i;
                        break;
                    }
                }

                // Match pass 2: Normalized alias match (Bullet vs Arrow vs Grenade)
                if (bestMatchIdx < 0 && !string.IsNullOrEmpty(normTarget))
                {
                    for (int i = 0; i < visualEntries.Count; i++)
                    {
                        var entry = visualEntries[i];
                        if (entry == null || entry.visualObject == null) continue;

                        string entryNorm = NormalizeVisualKey(entry.id);
                        if (!string.IsNullOrEmpty(entryNorm) && entryNorm == normTarget)
                        {
                            bestMatchIdx = i;
                            break;
                        }
                    }
                }

                // Match pass 3: Substring / Contains match on entry.id
                if (bestMatchIdx < 0)
                {
                    for (int i = 0; i < visualEntries.Count; i++)
                    {
                        var entry = visualEntries[i];
                        if (entry == null || entry.visualObject == null || string.IsNullOrEmpty(entry.id)) continue;

                        string entryIdClean = entry.id.Trim().ToLower();
                        if (entryIdClean.Contains(cleanTarget) || cleanTarget.Contains(entryIdClean))
                        {
                            bestMatchIdx = i;
                            break;
                        }
                    }
                }

                // Match pass 4: Check entry.visualObject GameObject name (e.g. ITEM0009_Bullet, ITEM0010_Arrow)
                if (bestMatchIdx < 0)
                {
                    for (int i = 0; i < visualEntries.Count; i++)
                    {
                        var entry = visualEntries[i];
                        if (entry == null || entry.visualObject == null) continue;

                        string voNameClean = entry.visualObject.name.Trim().ToLower();
                        string voNorm = NormalizeVisualKey(voNameClean);
                        if (voNameClean == cleanTarget || (!string.IsNullOrEmpty(normTarget) && voNorm == normTarget) ||
                            voNameClean.Contains(cleanTarget) || cleanTarget.Contains(voNameClean))
                        {
                            bestMatchIdx = i;
                            break;
                        }
                    }
                }

                if (bestMatchIdx >= 0)
                {
                    matchedAny = true;
                    for (int i = 0; i < visualEntries.Count; i++)
                    {
                        var entry = visualEntries[i];
                        if (entry != null && entry.visualObject != null)
                        {
                            bool shouldBeActive = (i == bestMatchIdx);
                            entry.visualObject.SetActive(shouldBeActive);

                            // ถ้า Parent ของโมเดลถูกปิดอยู่ (เช่น Parent 'Bullet') ให้เปิดตา Parent ด้วย
                            if (shouldBeActive && entry.visualObject.transform.parent != null && entry.visualObject.transform.parent != transform)
                            {
                                entry.visualObject.transform.parent.gameObject.SetActive(true);
                            }
                        }
                    }
                }
            }

            // 2. Fallback: ถ้าใน visualEntries ยังไม่ได้ตั้งค่า ให้ค้นหาจากชื่อ Child GameObjects โดยตรง
            if (!matchedAny)
            {
                Transform[] allChildren = GetComponentsInChildren<Transform>(true);
                Transform targetChild = null;

                foreach (var child in allChildren)
                {
                    if (child == transform) continue;
                    if (child.name.Equals("Sphere", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (child.name.Equals("FrontLight", System.StringComparison.OrdinalIgnoreCase)) continue;

                    string cName = child.name.Trim().ToLower();
                    string cNorm = NormalizeVisualKey(cName);
                    if (cName == cleanTarget || (!string.IsNullOrEmpty(normTarget) && cNorm == normTarget) ||
                        cName.Contains(cleanTarget) || cleanTarget.Contains(cName))
                    {
                        targetChild = child;
                        break;
                    }
                }

                if (targetChild != null)
                {
                    matchedAny = true;
                    if (visualEntries != null)
                    {
                        foreach (var entry in visualEntries)
                        {
                            if (entry != null && entry.visualObject != null)
                            {
                                entry.visualObject.SetActive(entry.visualObject == targetChild.gameObject);
                            }
                        }
                    }

                    targetChild.gameObject.SetActive(true);
                    if (targetChild.parent != null && targetChild.parent != transform)
                    {
                        targetChild.parent.gameObject.SetActive(true);
                    }
                }
            }

            // 3. ปิดตา Sphere Placeholder และ Root MeshRenderer อัตโนมัติเมื่อโมเดล 3D ถูกเปิดใช้งาน
            if (matchedAny)
            {
                var rootMr = GetComponent<MeshRenderer>();
                if (rootMr != null) rootMr.enabled = false;

                Transform sphere = transform.Find("Sphere");
                if (sphere != null)
                {
                    sphere.gameObject.SetActive(false);
                }
            }
        }

        private void HideAllVisualsForInvisibleHitbox()
        {
            if (visualEntries != null)
            {
                for (int i = 0; i < visualEntries.Count; i++)
                {
                    if (visualEntries[i] != null && visualEntries[i].visualObject != null)
                    {
                        visualEntries[i].visualObject.SetActive(false);
                    }
                }
            }

            MeshRenderer rootMr = GetComponent<MeshRenderer>();
            if (rootMr != null) rootMr.enabled = false;
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null) rootSr.enabled = false;

            Transform sphere = transform.Find("Sphere");
            if (sphere != null) sphere.gameObject.SetActive(false);

            if (_trail != null)
            {
                _trail.Clear();
                _trail.enabled = false;
            }

            if (bulletLight != null)
            {
                bulletLight.enabled = false;
            }
        }

        private static string NormalizeVisualKey(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return "";
            string lower = rawId.Trim().ToLower();
            if (lower.Contains("arrow") || lower.Contains("bolt") || lower.Contains("bow") || lower.Contains("crossbow"))
                return "arrow";
            if (lower.Contains("bullet") || lower.Contains("ammo") || lower.Contains("pistol") || lower.Contains("rifle") || 
                lower.Contains("shotgun") || lower.Contains("smg") || lower.Contains("sniper") || lower.Contains("revolver"))
                return "bullet";
            if (lower.Contains("grenade") || lower.Contains("bomb"))
                return "grenade";
            return lower;
        }

        private void Update()
        {
            if (trajectoryMode == TrajectoryMode.Arced)
            {
                _currentFlightTime += Time.deltaTime;
                float t = _currentFlightTime / _flightDuration;

                if (t >= 1f)
                {
                    // Reached the destination
                    transform.position = _targetPoint;
                    if (_activeIsExplosive) Explode();
                    DeactivateProjectile();
                    return;
                }

                // Calculate parabolic arc manually
                Vector3 currentPos = Vector3.Lerp(_startPoint, _targetPoint, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

                // Face the direction of movement
                Vector3 moveDir = (currentPos - transform.position);
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    transform.forward = moveDir.normalized;
                }

                transform.position = currentPos;
            }
            if (trajectoryMode != TrajectoryMode.Arced)
            {
                _lifeTimer -= Time.deltaTime;
                if (_lifeTimer <= 0f)
                {
                    if (_activeIsExplosive) Explode();
                    DeactivateProjectile();
                }
            }
        }

        public void Setup(Vector3 direction, Vector3 targetPoint, GameObject owner, string visualId = "")
        {
            _owner = owner;
            _targetPoint = targetPoint;
            _startPoint = transform.position;
            float moveSpeed = _activeSpeed > 0.01f ? _activeSpeed : speed;

            if (!string.IsNullOrEmpty(visualId))
            {
                ApplyVisual(visualId);
            }

            if (trajectoryMode == TrajectoryMode.Arced)
            {
                _rb.linearVelocity = Vector3.zero; // Manual movement via Update
                float distance = Vector3.Distance(new Vector3(_startPoint.x, 0, _startPoint.z), new Vector3(_targetPoint.x, 0, _targetPoint.z));
                _flightDuration = distance / moveSpeed;
                
                // Ensure it doesn't instantly snap if distance is extremely close
                if (_flightDuration < 0.1f) _flightDuration = 0.1f;
            }
            else
            {
                _rb.linearVelocity = direction.normalized * moveSpeed;
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.forward = direction.normalized;
                }
            }
        }

        public void SetVisualOnly(bool visualOnly)
        {
            _isVisualOnly = visualOnly;
        }

        public void SetEnemyProjectile(bool isEnemy)
        {
            _isEnemyProjectile = isEnemy;
        }

        public void SetStats(float damageVal, float lifetimeVal, bool pierceVal, string weaponName = "", float maxFalloffRange = 0f, string visualId = "")
        {
            ResetDynamicOverrides();
            _activeDamage = damageVal;
            _activeLifetime = lifetimeVal;
            _canPierce = pierceVal;
            _lifeTimer = lifetimeVal; // Update active timer
            _weaponName = weaponName;
            _maxFalloffDistance = (maxFalloffRange > 0f) ? maxFalloffRange : DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;

            string targetVisual = !string.IsNullOrEmpty(visualId) ? visualId : weaponName;
            if (!string.IsNullOrEmpty(targetVisual))
            {
                ApplyVisual(targetVisual);
            }
        }

        private void OnTriggerEnter(Collider collision)
        {
            if (collision == null) return;

            bool isEnemyShot = _isEnemyProjectile || (_owner != null && _owner.CompareTag("Enemy"));

            // Ignore owner, any child transforms of the owner, or same root hierarchy
            if (_owner != null)
            {
                if (collision.gameObject == _owner || collision.transform.IsChildOf(_owner.transform) || collision.transform.root == _owner.transform.root) return;
                if (isEnemyShot && collision.CompareTag("Enemy")) return;
                if (_owner.CompareTag("Player") && collision.CompareTag("Player"))
                {
                    // Allow bullets to hit co-op teammates for 20% friendly fire damage (skip if solo/disabled/downed)
                    PlayerController hitAlly = collision.GetComponent<PlayerController>() ?? collision.GetComponentInParent<PlayerController>();
                    if (hitAlly == null ||
                        !TheLastEmpire.Network.EmpireNetworkManager.IsMultiplayer ||
                        !TheLastEmpire.Network.NetworkPlayerBridge.FriendlyFireEnabled ||
                        hitAlly.IsDowned ||
                        (hitAlly.PlayerHealth != null && hitAlly.PlayerHealth.IsDead))
                    {
                        return;
                    }
                }
            }
            else if (isEnemyShot)
            {
                if (collision.CompareTag("Enemy")) return;
            }
            else if (collision.CompareTag("Player"))
            {
                return; // Guard against frame-0 collisions before Setup assigns owner
            }

            // Ignore Item Drops, Ground, Floor, and Walkable Terrain
            if (collision.GetComponent<CollectibleItem>() != null) return;
            if (collision.CompareTag("Ground") || collision.gameObject.name.ToLower().Contains("ground") || collision.gameObject.name.ToLower().Contains("floor")) return;

            // If it's an arced projectile, it ignores walls mid-air and only explodes on landing (in Update)
            // However, if we want it to hit enemies mid-air or explode on impact:
            if (trajectoryMode == TrajectoryMode.Arced && collision.isTrigger) return;
            if (trajectoryMode == TrajectoryMode.Arced)
            {
                // Optionally allow exploding mid-air if it hits an enemy, but usually grenades fly over until landing
                // If you want it to bounce or pass through, we can just return here.
                // Let's make it explode early if it hits an enemy or wall directly.
            }

            IDamageable damageable = collision.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // ไม่ให้กระสุนของผู้เล่นทำลายสิ่งปลูกสร้างของฝ่ายผู้เล่นเอง
                BuildingObject building = collision.GetComponentInParent<BuildingObject>();
                if (building != null && !building.CanBeDamagedByPlayer && _owner != null && _owner.CompareTag("Player"))
                {
                    DeactivateProjectile();
                    return;
                }

                if (_activeIsExplosive)
                {
                    Explode();
                    DeactivateProjectile();
                    return;
                }

                // Remote Co-op visual replay projectile: never deal fake damage or trigger enemy I-frames!
                if (_isVisualOnly)
                {
                    if (!_canPierce)
                    {
                        DeactivateProjectile();
                    }
                    return;
                }

                // Co-op authority: Enemy projectiles only apply HP damage on the player who owns that avatar.
                // Hitting a remote teammate plays the splash/impact visually without double-damaging them locally.
                if (isEnemyShot)
                {
                    PlayerController hitPlayer = collision.GetComponent<PlayerController>() ?? collision.GetComponentInParent<PlayerController>();
                    if (hitPlayer != null && (hitPlayer.IsRemotePlayer || hitPlayer.IsDowned))
                    {
                        DeactivateProjectile();
                        return;
                    }
                }

                if (!_hitTargets.Contains(damageable))
                {
                    _hitTargets.Add(damageable);
                    Vector3 exactHitPoint = collision.ClosestPoint(transform.position);

                    // 🎯 โมดิฟายดาเมจ: ยิงไกลยิ่งเบา (ต่ำสุด 40%) + สุ่ม +-10 + Critical (20% -> 1.3x หรือตามคลาส)
                    float distanceTraveled = Vector3.Distance(_startPoint, exactHitPoint);
                    DamageResult dmgResult = DamageCalculator.CalculateRangedDamage(
                        _activeDamage,
                        distanceTraveled,
                        _owner,
                        _weaponName,
                        _maxFalloffDistance
                    );

                    // Multiplayer Friendly Fire: hitting a teammate deals 20% damage synced to their client (no lifesteal)
                    if (TheLastEmpire.Network.NetworkPlayerBridge.TryApplyFriendlyFireDamage(
                            _owner, collision.gameObject, dmgResult.FinalDamage, exactHitPoint, dmgResult.IsCritical))
                    {
                        if (!_canPierce)
                        {
                            DeactivateProjectile();
                        }
                        return;
                    }

                    damageable.TakeDamage(dmgResult.FinalDamage, exactHitPoint, dmgResult.IsCritical);

                    if (_bonusKnockback > 0.01f || _bonusStagger > 0.01f)
                    {
                        BaseEnemyAI hitEnemy = collision.GetComponent<BaseEnemyAI>();
                        if (hitEnemy != null)
                        {
                            Vector3 pushDir = transform.forward;
                            pushDir.y = 0f;
                            if (pushDir.sqrMagnitude < 0.01f) pushDir = (hitEnemy.transform.position - transform.position);
                            pushDir.y = 0f;
                            hitEnemy.ApplyMeleeKnockback(pushDir.normalized, _bonusKnockback, _bonusStagger);
                        }
                    }

                    if (_lifestealOnHit > 0.01f && _owner != null)
                    {
                        Health ownerHealth = _owner.GetComponent<Health>();
                        if (ownerHealth != null && !ownerHealth.IsDead)
                        {
                            ownerHealth.Heal(_lifestealOnHit);
                        }
                    }
                }

                if (!_canPierce)
                {
                    DeactivateProjectile();
                }
                return;
            }

            // Hit solid wall or obstacle
            if (!collision.isTrigger)
            {
                if (_activeIsExplosive)
                {
                    Explode();
                }
                else if (!string.IsNullOrEmpty(obstacleHitVfxPoolKey) && ObjectPoolManager.Instance != null)
                {
                    Vector3 hitPt = collision.ClosestPoint(transform.position);
                    Vector3 normal = (transform.position - hitPt).normalized;
                    if (normal.sqrMagnitude < 0.01f) normal = -transform.forward;
                    ObjectPoolManager.Instance.SpawnFromPool(obstacleHitVfxPoolKey, hitPt, Quaternion.LookRotation(normal));
                }
                DeactivateProjectile();
            }
        }

        private void Explode()
        {
            float activeRadius = _activeExplosionRadius > 0.01f ? _activeExplosionRadius : explosionRadius;
            string activeVfxKey = !string.IsNullOrEmpty(_activeExplosionVfxPoolKey) ? _activeExplosionVfxPoolKey : explosionVfxPoolKey;
            Debug.Log($"[Projectile] Explosive ({_weaponName}) detonated at {transform.position} (radius={activeRadius:F1}m)!");

            // Play Explosion VFX
            if (!string.IsNullOrEmpty(activeVfxKey) && ObjectPoolManager.Instance != null)
            {
                GameObject explosionVFX = ObjectPoolManager.Instance.SpawnFromPool(activeVfxKey, transform.position, Quaternion.identity);
                if (explosionVFX != null)
                {
                    PooledParticle pooledParticle = explosionVFX.GetComponent<PooledParticle>();
                    float multiplier = explosionVfxScaleMultiplier > 0 ? explosionVfxScaleMultiplier : 1.0f;

                    if (pooledParticle != null)
                    {
                        pooledParticle.SetPoolKey(activeVfxKey);
                        pooledParticle.ApplyScaleMultiplier(activeRadius * multiplier);
                    }
                    else
                    {
                        explosionVFX.transform.localScale = Vector3.one * (activeRadius * multiplier);
                    }
                }
            }

            // Remote Co-op visual replay explosion: VFX only, skip AOE damage (damage is sent authoritatively via EnemyDamageRequest)
            if (_isVisualOnly) return;

            // AOE Damage
            Collider[] targets = Physics.OverlapSphere(transform.position, activeRadius);
            var hitPlayers = new System.Collections.Generic.HashSet<PlayerController>();
            foreach (var target in targets)
            {
                if (_owner != null && (target.gameObject == _owner || target.transform.root == _owner.transform.root)) continue; // No friendly fire to owner
                
                // Deal damage to enemies, teammates (20% friendly fire), and obstacles
                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    BuildingObject building = target.GetComponentInParent<BuildingObject>();
                    if (building != null && !building.CanBeDamagedByPlayer && _owner != null && _owner.CompareTag("Player"))
                    {
                        continue;
                    }

                    float distFromCenter = Vector3.Distance(transform.position, target.transform.position);
                    DamageResult dmgResult = DamageCalculator.CalculateExplosionDamage(_activeDamage, distFromCenter, activeRadius, _owner, _weaponName);

                    PlayerController hitAlly = target.GetComponent<PlayerController>() ?? target.GetComponentInParent<PlayerController>();
                    if (hitAlly != null && _owner != null && _owner.CompareTag("Player"))
                    {
                        if (hitPlayers.Add(hitAlly))
                        {
                            TheLastEmpire.Network.NetworkPlayerBridge.TryApplyFriendlyFireDamage(
                                _owner, target.gameObject, dmgResult.FinalDamage, target.transform.position, dmgResult.IsCritical);
                        }
                        continue;
                    }

                    damageable.TakeDamage(dmgResult.FinalDamage, target.transform.position, dmgResult.IsCritical);

                    if (_bonusKnockback > 0.01f || _bonusStagger > 0.01f)
                    {
                        BaseEnemyAI hitEnemy = target.GetComponent<BaseEnemyAI>();
                        if (hitEnemy != null)
                        {
                            Vector3 pushDir = (hitEnemy.transform.position - transform.position);
                            pushDir.y = 0f;
                            if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward;
                            pushDir.y = 0f;
                            hitEnemy.ApplyMeleeKnockback(pushDir.normalized, _bonusKnockback, _bonusStagger);
                        }
                    }

                    if (_lifestealOnHit > 0.01f && _owner != null)
                    {
                        Health ownerHealth = _owner.GetComponent<Health>();
                        if (ownerHealth != null && !ownerHealth.IsDead)
                        {
                            ownerHealth.Heal(_lifestealOnHit);
                        }
                    }
                }
            }
        }

        private void DeactivateProjectile()
        {
            if (_trail != null)
            {
                _trail.Clear();
            }
            if (bulletLight != null)
            {
                bulletLight.enabled = false;
            }

            if (!string.IsNullOrEmpty(poolKey) && ObjectPoolManager.Instance != null)
            {
                _rb.linearVelocity = Vector3.zero;
                ObjectPoolManager.Instance.ReturnToPool(poolKey, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
