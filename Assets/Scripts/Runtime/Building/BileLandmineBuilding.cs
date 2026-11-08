using System.Collections;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// BileLandmineBuilding — กับระเบิดกรดและสารคัดหลั่งเอเลี่ยน (BUILD0020_CorrosiveBileLandmine)
    /// คุณสมบัติ:
    /// - ตรวจจับศัตรูในระยะประชิด (Proximity Trigger)
    /// - หน่วงเวลาชนวน 0.4 วินาทีแล้วระเบิดรุนแรง สร้างความเสียหายกรด 160 หน่วยในรัศมี 4.5 เมตร
    /// - ทิ้งแอ่งกรดกัดกร่อน (Caustic Puddle) ไว้ 5 วินาที สร้างความเสียหายต่อเนื่อง
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class BileLandmineBuilding : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [Tooltip("ความเสียหายระเบิดกรด")]
        [SerializeField] private float explosionDamage = 160f;

        [Tooltip("รัศมีความเสียหายของระเบิด (เมตร)")]
        [SerializeField] private float explosionRadius = 4.5f;

        [Tooltip("เวลาหน่วงชนวนระเบิด (วินาที)")]
        [SerializeField] private float fuseTime = 0.4f;

        [Header("Lingering Acid Puddle")]
        [SerializeField] private float puddleDuration = 5.0f;
        [SerializeField] private float puddleDamagePerSec = 15f;

        private BuildingObject _buildingObject;
        private bool _isTriggered = false;

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (_buildingObject != null)
            {
                _buildingObject.AutoAddNavMeshObstacle = false;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered) return;

            if (other.CompareTag("Enemy") || other.name.Contains("Alien") || other.name.Contains("Zombie") || other.name.Contains("Robot"))
            {
                StartCoroutine(DetonateSequence());
            }
        }

        private IEnumerator DetonateSequence()
        {
            _isTriggered = true;

            // หน่วงเวลาชนวนระเบิด
            float elapsed = 0f;
            Light warnLight = GetComponentInChildren<Light>();
            if (warnLight == null)
            {
                GameObject lObj = new GameObject("WarnLight");
                lObj.transform.SetParent(transform, false);
                lObj.transform.localPosition = Vector3.up * 0.3f;
                warnLight = lObj.AddComponent<Light>();
                warnLight.type = LightType.Point;
                warnLight.color = Color.red;
                warnLight.range = 3f;
                warnLight.intensity = 3f;
            }

            while (elapsed < fuseTime)
            {
                elapsed += Time.deltaTime;
                if (warnLight != null)
                {
                    warnLight.enabled = !warnLight.enabled;
                }
                yield return new WaitForSeconds(0.08f);
                elapsed += 0.08f;
            }

            Explode();
        }

        private void Explode()
        {
            Vector3 center = transform.position + Vector3.up * 0.3f;

            // ค้นหาเป้าหมายทั้งหมดในรัศมีระเบิด
            Collider[] hitColliders = Physics.OverlapSphere(center, explosionRadius);
            foreach (Collider hit in hitColliders)
            {
                if (hit == null || hit.gameObject == gameObject) continue;

                if (hit.CompareTag("Enemy") || hit.name.Contains("Alien") || hit.name.Contains("Zombie") || hit.name.Contains("Robot"))
                {
                    IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        float dist = Vector3.Distance(center, hit.transform.position);
                        float falloff = Mathf.Clamp01(1f - (dist / explosionRadius) * 0.5f);
                        float dmg = explosionDamage * falloff;

                        damageable.TakeDamage(dmg, hit.bounds.center, true);

                        // ผลักศัตรูกระเด็นออกจากศูนย์กลางระเบิด
                        BaseEnemyAI ai = hit.GetComponent<BaseEnemyAI>() ?? hit.GetComponentInParent<BaseEnemyAI>();
                        if (ai != null)
                        {
                            Vector3 dir = (hit.transform.position - center).normalized;
                            dir.y = 0.2f;
                            ai.ApplyDamageKnockback(dir * 8f);
                        }
                    }
                }
            }

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    center + Vector3.up * 1.0f,
                    explosionDamage,
                    true,
                    "BILE DETONATION!"
                );
            }

            // สร้างแอ่งกรดตกค้าง
            SpawnAcidPuddle(center);

            Debug.Log($"[BileLandmine] Detonated at {center}! Dealt {explosionDamage} damage in {explosionRadius}m radius.");
            Destroy(gameObject);
        }

        private void SpawnAcidPuddle(Vector3 position)
        {
            GameObject puddleObj = new GameObject("AcidPuddle_Hazard");
            puddleObj.transform.position = position;

            SphereCollider sc = puddleObj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = explosionRadius * 0.8f;

            AcidPuddleHazard hazard = puddleObj.AddComponent<AcidPuddleHazard>();
            hazard.Initialize(puddleDamagePerSec, puddleDuration);
        }
    }

    /// <summary>
    /// AcidPuddleHazard — แอ่งกรดที่คงอยู่สร้างความเสียหายต่อเนื่อง
    /// </summary>
    public class AcidPuddleHazard : MonoBehaviour
    {
        private float _dps = 15f;
        private float _duration = 5f;
        private float _tickTimer = 0f;

        public void Initialize(float dps, float duration)
        {
            _dps = dps;
            _duration = duration;
            Destroy(gameObject, duration);
        }

        private void OnTriggerStay(Collider other)
        {
            if (Time.time - _tickTimer < 0.5f) return;

            if (other.CompareTag("Enemy") || other.name.Contains("Alien") || other.name.Contains("Zombie") || other.name.Contains("Robot"))
            {
                _tickTimer = Time.time;
                IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(_dps * 0.5f, other.bounds.center, false);
                }
            }
        }
    }
}

