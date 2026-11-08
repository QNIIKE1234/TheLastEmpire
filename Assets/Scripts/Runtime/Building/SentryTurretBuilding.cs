using System;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// SentryTurretBuilding — ป้อมปืนกลอัตโนมัติ (BUILD0021_AutomatedSentryTurret)
    /// คุณสมบัติ:
    /// - หมุนตรวจจับศัตรู 360 องศาในระยะ 14 เมตร
    /// - ล็อกเป้าและยิงกระสุนต่อเนื่อง 5 นัด/วินาที (ดาเมจนัดละ 15 หน่วย)
    /// - หากมีเครื่องปั่นไฟ (Fusion Generator) อยู่ใกล้เคียง จะได้รับโบนัสความเร็วการยิง +25%
    /// - มีระบบกระสุนภายใน 200 นัด (ผู้เล่นสามารถกด [E] เพื่อเติมกระสุนหรือเปิด Turret_HUD)
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class SentryTurretBuilding : MonoBehaviour, IBuildingInteractable
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

        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => maxAmmo;
        public bool HasTarget => _currentTarget != null;

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.8f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            bool isPowered = GeneratorBuilding.IsPositionPowered(transform.position);
            string powerTag = isPowered ? " [⚡POWERED]" : "";

            if (!infiniteAmmo && currentAmmo < maxAmmo)
            {
                return $"<color=yellow><b>[E]</b></color> Reload Ammo ({currentAmmo}/{maxAmmo}){powerTag}";
            }
            return $"Sentry Turret{powerTag}";
        }

        public void OnInteract()
        {
            // ตรวจสอบว่ามี Turret_HUD ให้เปิดหรือไม่
            GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
            if (canvasGo != null)
            {
                Transform hud = canvasGo.transform.Find("Turret_HUD");
                if (hud != null && hud.childCount > 0)
                {
                    hud.gameObject.SetActive(true);
                    Debug.Log("[SentryTurret] Opened Turret_HUD.");
                    return;
                }
            }

            if (!infiniteAmmo && currentAmmo < maxAmmo)
            {
                ReloadAmmo();
            }
        }

        public void OnSecondaryInteract()
        {
        }

        private void OnEnable()
        {
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            BuildingInteractionRegistry.Unregister(this);
        }

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

        private void Update()
        {
            if (_buildingObject != null && _buildingObject.IsDead) return;

            bool isPowered = GeneratorBuilding.IsPositionPowered(transform.position);
            float effectiveFireRate = isPowered ? fireRate * 1.25f : fireRate;

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= 0.2f)
            {
                _scanTimer = 0f;
                ScanForTarget();
            }

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
        }

        private void ScanForTarget()
        {
            if (_currentTarget != null)
            {
                IDamageable currentDam = _currentTarget.GetComponent<IDamageable>() ?? _currentTarget.GetComponentInParent<IDamageable>();
                if (currentDam == null || currentDam.IsDead || _currentTarget.IsChildOf(transform) || Vector3.Distance(transform.position, _currentTarget.position) > targetingRange)
                {
                    _currentTarget = null;
                }
            }

            if (_currentTarget != null) return;

            Collider[] hits = Physics.OverlapSphere(transform.position, targetingRange);
            float closestDist = float.MaxValue;
            Transform bestTarget = null;

            foreach (var col in hits)
            {
                if (col == null) continue;

                // 1. ห้ามเล็งตัวเองและชิ้นส่วนของตัวเองเด็ดขาด
                if (col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                // 2. ห้ามเล็งผู้เล่น
                if (col.CompareTag("Player") || col.GetComponentInParent<PlayerController>() != null)
                {
                    continue;
                }

                // 3. ห้ามเล็งสิ่งปลูกสร้างพวกเดียวกัน (เช่น กำแพง, แคมป์ไฟ, ป้อมอื่น)
                if (col.GetComponentInParent<BuildingObject>() != null)
                {
                    continue;
                }

                // 4. ต้องเป็นศัตรูเท่านั้น (Alien, Zombie, Robot, Tag: Enemy, หรือมี BaseEnemyAI)
                bool isEnemy = col.CompareTag("Enemy") ||
                               col.GetComponentInParent<BaseEnemyAI>() != null ||
                               col.gameObject.name.IndexOf("Alien", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               col.gameObject.name.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               col.gameObject.name.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               col.gameObject.name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isEnemy) continue;

                IDamageable dam = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (dam != null && !dam.IsDead)
                {
                    float d = Vector3.Distance(transform.position, col.transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        bestTarget = col.transform;
                    }
                }
            }

            _currentTarget = bestTarget;
        }

        private void AimAtTarget()
        {
            if (turretHead == null || _currentTarget == null) return;

            Vector3 dir = (_currentTarget.position - turretHead.position);
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private void FireAtTarget()
        {
            if (_currentTarget == null || _currentTarget == transform || _currentTarget.IsChildOf(transform))
            {
                _currentTarget = null;
                return;
            }

            if (!infiniteAmmo)
            {
                if (currentAmmo <= 0)
                {
                    return;
                }
                currentAmmo--;
            }

            IDamageable dam = _currentTarget.GetComponent<IDamageable>() ?? _currentTarget.GetComponentInParent<IDamageable>();
            if (dam != null && !dam.IsDead)
            {
                dam.TakeDamage(damagePerShot);

                if (DamageNumberManager.Instance != null)
                {
                    Vector3 hitPos = _currentTarget.position + Vector3.up * 1.0f;
                    DamageNumberManager.Instance.SpawnDamageNumber(hitPos, damagePerShot, false);
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
                    $"RELOADED ({maxAmmo} RDS)"
                );
            }
            Debug.Log("[SentryTurret] Turret ammo replenished to maximum.");
        }
    }
}
