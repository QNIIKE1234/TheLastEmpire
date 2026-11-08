using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// SentryTurretBuilding — ป้อมปืนกลอัตโนมัติ (BUILD0021_AutomatedSentryTurret)
    /// คุณสมบัติ:
    /// - หมุนตรวจจับศัตรู 360 องศาในระยะ 14 เมตร
    /// - ล็อกเป้าและยิงกระสุนต่อเนื่อง 5 นัด/วินาที (ดาเมจนัดละ 15 หน่วย)
    /// - หากมีเครื่องปั่นไฟ (Fusion Generator) อยู่ใกล้เคียง จะได้รับโบนัสความเร็วการยิง +25%
    /// - มีระบบกระสุนภายใน 200 นัด (ผู้เล่นสามารถกด [E] เพื่อเติมกระสุนได้)
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class SentryTurretBuilding : MonoBehaviour
    {
        [Header("Targeting & Range")]
        [Tooltip("ระยะการตรวจจับและยิงของป้อมปืน (เมตร)")]
        [SerializeField] private float targetingRange = 14.0f;

        [Tooltip("ความเร็วในการหมุนกระบอกปืนหันหาเป้าหมาย (องศา/วินาที)")]
        [SerializeField] private float rotationSpeed = 180.0f;

        [Header("Combat & Fire Rate")]
        [Tooltip("อัตราการยิงพื้นฐาน (นัดต่อวินาที)")]
        [SerializeField] private float fireRate = 5.0f;

        [Tooltip("ความเสียหายต่อหนึ่งนัด")]
        [SerializeField] private float damagePerShot = 15.0f;

        [Tooltip("กระสุนไม่จำกัดหรือไม่")]
        [SerializeField] private bool infiniteAmmo = true;

        [Tooltip("จำนวนกระสุนสูงสุดที่บรรจุได้")]
        [SerializeField] private int maxAmmo = 200;

        [Tooltip("กระสุนคงเหลือปัจจุบัน")]
        [SerializeField] private int currentAmmo = 200;

        [Header("Transforms & VFX")]
        [Tooltip("ชิ้นส่วนหัวป้อมปืนที่จะหมุนเล็ง (หากไม่กำหนดจะค้นหาอัตโนมัติ)")]
        [SerializeField] private Transform turretHead;

        [Tooltip("ตำแหน่งปากกระบอกปืนสำหรับเอฟเฟกต์ไฟแลบ")]
        [SerializeField] private Transform muzzlePoint;

        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _currentTarget;
        private float _fireTimer = 0f;
        private float _scanTimer = 0f;
        private Transform _playerTransform;
        private bool _isPlayerNearby = false;

        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => maxAmmo;
        public bool HasTarget => _currentTarget != null;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();

            if (turretHead == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("gun", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("barrel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        turretHead = child;
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }

        private void Update()
        {
            if (_buildingObject != null && _buildingObject.IsDead) return;

            // ตรวจสอบพลังงานจาก Fusion Generator ใกล้เคียง
            bool isPowered = GeneratorBuilding.IsPositionPowered(transform.position);
            float effectiveFireRate = isPowered ? fireRate * 1.25f : fireRate;

            // ค้นหาเป้าหมายทุกๆ 0.2 วินาที
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= 0.2f)
            {
                _scanTimer = 0f;
                ScanForTarget();
            }

            // หมุนป้อมปืนและยิง
            if (_currentTarget != null)
            {
                AimAtTarget();

                _fireTimer += Time.deltaTime;
                float interval = 1.0f / Mathf.Max(0.1f, effectiveFireRate);
                if (_fireTimer >= interval)
                {
                    _fireTimer = 0f;
                    FireAtTarget();
                }
            }

            // ตรวจสอบผู้เล่นสำหรับเติมกระสุน
            CheckPlayerInteraction();
        }

        private void ScanForTarget()
        {
            // ตรวจสอบว่าเป้าหมายปัจจุบันยังใช้ได้อยู่หรือไม่
            if (_currentTarget != null)
            {
                if (!_currentTarget.gameObject.activeInHierarchy || Vector3.Distance(transform.position, _currentTarget.position) > targetingRange)
                {
                    _currentTarget = null;
                }
                else
                {
                    IDamageable dmg = _currentTarget.GetComponent<IDamageable>() ?? _currentTarget.GetComponentInParent<IDamageable>();
                    if (dmg != null && dmg.IsDead)
                    {
                        _currentTarget = null;
                    }
                }
            }

            if (_currentTarget != null) return;

            // สแกนหาศัตรูที่ใกล้ที่สุด
            Collider[] hits = Physics.OverlapSphere(transform.position, targetingRange);
            float closestDist = float.MaxValue;
            Transform bestTarget = null;

            foreach (Collider hit in hits)
            {
                if (hit == null || hit.gameObject == gameObject) continue;

                if (hit.CompareTag("Enemy") || hit.name.Contains("Alien") || hit.name.Contains("Zombie") || hit.name.Contains("Robot"))
                {
                    IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        float dist = Vector3.Distance(transform.position, hit.transform.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            bestTarget = hit.transform;
                        }
                    }
                }
            }

            _currentTarget = bestTarget;
        }

        private void AimAtTarget()
        {
            if (_currentTarget == null) return;

            Vector3 targetDir = (_currentTarget.position - transform.position);
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                if (turretHead != null)
                {
                    turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
                else
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
        }

        private void FireAtTarget()
        {
            if (_currentTarget == null) return;

            if (!infiniteAmmo)
            {
                if (currentAmmo <= 0) return;
                currentAmmo--;
            }

            IDamageable damageable = _currentTarget.GetComponent<IDamageable>() ?? _currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                Vector3 hitPoint = _currentTarget.position + Vector3.up * 0.8f;
                damageable.TakeDamage(damagePerShot, hitPoint, false);
            }
        }

        private void CheckPlayerInteraction()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            if (_isPlayerNearby && !infiniteAmmo && currentAmmo < maxAmmo)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    ReloadAmmo();
                }
            }
        }

        public void ReloadAmmo()
        {
            currentAmmo = maxAmmo;
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 1.8f,
                    0f,
                    false,
                    "RELOADED (200 RDS)"
                );
            }
            Debug.Log("[SentryTurret] Turret ammo replenished to maximum.");
        }

        private void OnGUI()
        {
            if (_isPlayerNearby)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
                if (screenPos.z > 0)
                {
                    float width = 230f;
                    float height = 38f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    bool isPowered = GeneratorBuilding.IsPositionPowered(transform.position);
                    GUI.color = isPowered ? new Color(0.4f, 1f, 0.4f, 0.95f) : new Color(1f, 0.85f, 0.3f, 0.95f);

                    string powerTag = isPowered ? "[⚡POWERED +25%]" : "[STANDALONE]";
                    string text = infiniteAmmo ? $"Sentry Turret {powerTag}" : $"[E] บรรจุกระสุน ({currentAmmo}/{maxAmmo}) {powerTag}";
                    GUI.Box(rect, text);
                }
            }
        }
    }
}

