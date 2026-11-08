using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// ShockTowerBuilding — หอคอยสายฟ้าผลึกเอเลี่ยน (BUILD0022_XenocrystalShockTower)
    /// คุณสมบัติ:
    /// - ปล่อยกระแสไฟฟ้าแรงสูงแบบลูกโซ่ (Chain Lightning) ใส่ศัตรูสูงสุด 4 เป้าหมายพร้อมกันในระยะ 11 เมตร
    /// - ปล่อยสายฟ้าทุกๆ 2.5 วินาที สร้างความเสียหายไฟฟ้า 80 หน่วยต่อนัด
    /// - ทำให้ศัตรูชีวภาพเป็นอัมพาต (EM Stun) 1.5 วินาที และทำให้ศัตรูหุ่นยนต์รวน
    /// - วาดเส้นสายฟ้าชีวภาพ (LineRenderer) พุ่งจากยอดผลึกสู่เป้าหมาย
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class ShockTowerBuilding : MonoBehaviour
    {
        [Header("Shock Combat Settings")]
        [Tooltip("ความเสียหายไฟฟ้าต่อเป้าหมาย")]
        [SerializeField] private float shockDamage = 80.0f;

        [Tooltip("ระยะการปล่อยสายฟ้า (เมตร)")]
        [SerializeField] private float dischargeRange = 11.0f;

        [Tooltip("รอบเวลาการปล่อยประจุไฟฟ้า (วินาที)")]
        [SerializeField] private float dischargeInterval = 2.5f;

        [Tooltip("จำนวนเป้าหมายสูงสุดที่สายฟ้าช็อตพร้อมกัน")]
        [SerializeField] private int maxChainTargets = 4;

        [Tooltip("ระยะเวลาที่ศัตรูติดสถานะมึนงง/ช็อต (วินาที)")]
        [SerializeField] private float stunDuration = 1.5f;

        [Header("Visual & Beam")]
        [Tooltip("จุดยอดผลึกสำหรับปล่อยสายฟ้า")]
        [SerializeField] private Transform crystalTip;

        [SerializeField] private Color lightningColor = new Color(0.3f, 0.9f, 1.0f, 1.0f);

        private BuildingObject _buildingObject;
        private float _dischargeTimer = 0f;
        private List<LineRenderer> _beamRenderers = new List<LineRenderer>();

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();

            if (crystalTip == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.IndexOf("crystal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        child.name.IndexOf("tip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        crystalTip = child;
                        break;
                    }
                }
            }

            if (crystalTip == null)
            {
                crystalTip = transform;
            }

            SetupBeams();
        }

        private void SetupBeams()
        {
            for (int i = 0; i < maxChainTargets; i++)
            {
                GameObject beamObj = new GameObject($"ShockBeam_{i}");
                beamObj.transform.SetParent(transform, false);

                LineRenderer lr = beamObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.15f;
                lr.endWidth = 0.08f;
                lr.useWorldSpace = true;
                lr.enabled = false;

                Material beamMat = new Material(Shader.Find("Sprites/Default"));
                beamMat.color = lightningColor;
                lr.material = beamMat;

                _beamRenderers.Add(lr);
            }
        }

        private void Update()
        {
            if (_buildingObject != null && _buildingObject.IsDead) return;

            _dischargeTimer += Time.deltaTime;
            if (_dischargeTimer >= dischargeInterval)
            {
                _dischargeTimer = 0f;
                DischargeChainLightning();
            }
        }

        private void DischargeChainLightning()
        {
            Vector3 origin = crystalTip != null ? crystalTip.position : transform.position + Vector3.up * 2.5f;

            Collider[] hits = Physics.OverlapSphere(origin, dischargeRange);
            List<Transform> validTargets = new List<Transform>();

            foreach (Collider hit in hits)
            {
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.CompareTag("Player") || hit.GetComponentInParent<PlayerController>() != null) continue;
                if (hit.GetComponentInParent<BuildingObject>() != null) continue;

                bool isEnemy = hit.CompareTag("Enemy") ||
                               hit.GetComponentInParent<BaseEnemyAI>() != null ||
                               hit.gameObject.name.IndexOf("Alien", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               hit.gameObject.name.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               hit.gameObject.name.IndexOf("Robot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               hit.gameObject.name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isEnemy)
                {
                    IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        validTargets.Add(hit.transform);
                        if (validTargets.Count >= maxChainTargets) break;
                    }
                }
            }

            if (validTargets.Count == 0) return;

            for (int i = 0; i < validTargets.Count; i++)
            {
                Transform target = validTargets[i];
                if (target == null) continue;

                IDamageable damageable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    Vector3 targetPos = target.position + Vector3.up * 0.8f;
                    damageable.TakeDamage(shockDamage, targetPos, true);

                    // สตันและผลักศัตรู
                    BaseEnemyAI ai = target.GetComponent<BaseEnemyAI>() ?? target.GetComponentInParent<BaseEnemyAI>();
                    if (ai != null)
                    {
                        ai.ApplyMeleeKnockback(Vector3.zero, 0f, stunDuration);
                    }

                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.SpawnDamageNumber(
                            targetPos + Vector3.up * 0.5f,
                            shockDamage,
                            true,
                            "⚡SHOCK"
                        );
                    }

                    // แสดงลำแสงสายฟ้า
                    if (i < _beamRenderers.Count)
                    {
                        StartCoroutine(AnimateLightningBeam(_beamRenderers[i], origin, targetPos));
                    }
                }
            }

            Debug.Log($"[ShockTower] Discharged Chain Lightning to {validTargets.Count} targets for {shockDamage} dmg each!");
        }

        private IEnumerator AnimateLightningBeam(LineRenderer lr, Vector3 start, Vector3 end)
        {
            if (lr == null) yield break;

            lr.enabled = true;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            yield return new WaitForSeconds(0.2f);

            if (lr != null)
            {
                lr.enabled = false;
            }
        }
    }
}

