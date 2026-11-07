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
        [SerializeField] private float speed = 12f;
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

        // Dynamic overrides set by Weapon stats
        private float _activeDamage;
        private float _activeLifetime;
        private bool _canPierce = false;
        private string _weaponName = "";
        private float _maxFalloffDistance = DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE;
        private System.Collections.Generic.List<IDamageable> _hitTargets = new System.Collections.Generic.List<IDamageable>();

        public string PoolKey
        {
            get => poolKey;
            set => poolKey = value;
        }

        public float Speed => speed;
        public bool IsExplosive => isExplosive;
        public float ExplosionRadius => explosionRadius;
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
            _activeDamage = damage;
            _activeLifetime = lifetime;
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
        /// สลับเปิด/ปิด GameObject โมเดลตามไอดี (เช่น ITEM0008_Grenade, ITEM0009_Bullet, ITEM0010_Arrow)
        /// เปิดตา GO ที่ตรงกับไอดี และปิดตา GO ตัวอื่นๆ ทั้งหมด
        /// </summary>
        public void ApplyVisual(string visualId)
        {
            if (string.IsNullOrEmpty(visualId)) return;
            _activeVisualId = visualId;

            string cleanTarget = visualId.Trim().ToLower();
            bool matchedAny = false;

            // 1. ตรวจสอบจาก visualEntries ใน Inspector ก่อน
            if (visualEntries != null && visualEntries.Count > 0)
            {
                int bestMatchIdx = -1;

                // Match pass 1: Exact match
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

                // Match pass 2: Substring / Contains match
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
                    if (cName == cleanTarget || cName.Contains(cleanTarget) || cleanTarget.Contains(cName))
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

            // 3. ปิดตา Sphere Placeholder อัตโนมัติเมื่อโมเดล 3D ถูกเปิดใช้งาน
            if (matchedAny)
            {
                Transform sphere = transform.Find("Sphere");
                if (sphere != null)
                {
                    sphere.gameObject.SetActive(false);
                }
            }
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
                    if (isExplosive) Explode();
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
                    if (isExplosive) Explode();
                    DeactivateProjectile();
                }
            }
        }

        public void Setup(Vector3 direction, Vector3 targetPoint, GameObject owner, string visualId = "")
        {
            _owner = owner;
            _targetPoint = targetPoint;
            _startPoint = transform.position;

            if (!string.IsNullOrEmpty(visualId))
            {
                ApplyVisual(visualId);
            }

            if (trajectoryMode == TrajectoryMode.Arced)
            {
                _rb.linearVelocity = Vector3.zero; // Manual movement via Update
                float distance = Vector3.Distance(new Vector3(_startPoint.x, 0, _startPoint.z), new Vector3(_targetPoint.x, 0, _targetPoint.z));
                _flightDuration = distance / speed;
                
                // Ensure it doesn't instantly snap if distance is extremely close
                if (_flightDuration < 0.1f) _flightDuration = 0.1f;
            }
            else
            {
                _rb.linearVelocity = direction.normalized * speed;
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.forward = direction.normalized;
                }
            }
        }

        public void SetStats(float damageVal, float lifetimeVal, bool pierceVal, string weaponName = "", float maxFalloffRange = 0f, string visualId = "")
        {
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

            // Ignore owner, any child transforms of the owner, or same root hierarchy
            if (_owner != null)
            {
                if (collision.gameObject == _owner || collision.transform.IsChildOf(_owner.transform) || collision.transform.root == _owner.transform.root) return;
                if (_owner.CompareTag("Player") && collision.CompareTag("Player")) return;
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
                if (isExplosive)
                {
                    Explode();
                    DeactivateProjectile();
                    return;
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

                    damageable.TakeDamage(dmgResult.FinalDamage, exactHitPoint, dmgResult.IsCritical);
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
                if (isExplosive)
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
            Debug.Log($"[Projectile] Grenade exploded at {transform.position}!");

            // Play Explosion VFX
            if (!string.IsNullOrEmpty(explosionVfxPoolKey) && ObjectPoolManager.Instance != null)
            {
                GameObject explosionVFX = ObjectPoolManager.Instance.SpawnFromPool(explosionVfxPoolKey, transform.position, Quaternion.identity);
                if (explosionVFX != null)
                {
                    PooledParticle pooledParticle = explosionVFX.GetComponent<PooledParticle>();
                    float multiplier = explosionVfxScaleMultiplier > 0 ? explosionVfxScaleMultiplier : 1.0f;

                    if (pooledParticle != null)
                    {
                        pooledParticle.SetPoolKey(explosionVfxPoolKey);
                        pooledParticle.ApplyScaleMultiplier(explosionRadius * multiplier);
                    }
                    else
                    {
                        explosionVFX.transform.localScale = Vector3.one * (explosionRadius * multiplier);
                    }
                }
            }

            // AOE Damage
            Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var target in targets)
            {
                if (_owner != null && target.gameObject == _owner) continue; // No friendly fire to owner
                
                // Deal damage to enemies and obstacles
                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float distFromCenter = Vector3.Distance(transform.position, target.transform.position);
                    DamageResult dmgResult = DamageCalculator.CalculateExplosionDamage(_activeDamage, distFromCenter, explosionRadius, _owner);
                    damageable.TakeDamage(dmgResult.FinalDamage, target.transform.position, dmgResult.IsCritical);
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
