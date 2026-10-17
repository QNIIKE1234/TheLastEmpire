using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    // =========================================================================
    //  1. Vanguard: Adrenaline Rush
    // =========================================================================
    public class AdrenalineRushAbility : IShiftAbility
    {
        public string AbilityName => "Adrenaline Rush";
        public string Description => "+75% Speed, +30% Attack Rate, 20% Dmg Resistance, Rapid Hunger Drain";
        public bool IsActive => _isActive;
        public float CooldownRemaining => 0f;
        public float CooldownDuration => 0f;

        private PlayerController _player;
        private bool _isActive = false;

        public void Initialize(PlayerController player)
        {
            _player = player;
        }

        public void OnShiftPressed() { }

        public void OnShiftHeld()
        {
            if (_player == null || _player.CurrentHunger <= 0f)
            {
                if (_isActive) OnShiftReleased();
                return;
            }

            _isActive = true;
        }

        public void OnShiftReleased()
        {
            _isActive = false;
        }

        public void UpdateAbility()
        {
            if (_isActive && _player != null)
            {
                // Extra hunger drain applied during adrenaline state
                _player.DrainHunger(Time.deltaTime * 1.5f);
            }
        }

        public void OnDestroy() { _isActive = false; }
    }

    // =========================================================================
    //  2. Juggernaut: Unstoppable Charge
    // =========================================================================
    public class UnstoppableChargeAbility : IShiftAbility
    {
        public string AbilityName => "Unstoppable Charge";
        public string Description => "Burst dash that deflects bullets, deals 25 damage, and stuns enemies";
        public bool IsActive => _isCharging;
        public float CooldownRemaining => _cooldownTimer;
        public float CooldownDuration => 5.0f;

        private PlayerController _player;
        private bool _isCharging = false;
        private float _chargeTimer = 0f;
        private float _cooldownTimer = 0f;
        private Vector3 _chargeDir;
        private const float ChargeSpeed = 18f;
        private const float ChargeDuration = 0.35f;

        public void Initialize(PlayerController player)
        {
            _player = player;
        }

        public void OnShiftPressed()
        {
            if (_player == null || _cooldownTimer > 0f || _isCharging) return;

            Vector2 moveInput = _player.MoveInput;
            _chargeDir = moveInput.sqrMagnitude > 0.05f 
                ? new Vector3(moveInput.x, 0f, moveInput.y).normalized 
                : _player.AimDirection.normalized;

            if (_chargeDir.sqrMagnitude < 0.01f) _chargeDir = Vector3.right;

            _isCharging = true;
            _chargeTimer = ChargeDuration;
            _cooldownTimer = CooldownDuration;
            _player.DrainHunger(5f); // Instant hunger cost for burst
        }

        public void OnShiftHeld() { }
        public void OnShiftReleased() { }

        public void UpdateAbility()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer < 0f) _cooldownTimer = 0f;
            }

            if (_isCharging && _player != null)
            {
                _chargeTimer -= Time.deltaTime;

                // Move with high speed
                Rigidbody rb = _player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = _chargeDir * ChargeSpeed;
                }

                // Check collisions with enemies in frontal cone
                Collider[] hits = Physics.OverlapSphere(_player.transform.position, 1.8f);
                foreach (var h in hits)
                {
                    if (h != null && (h.CompareTag("Enemy") || h.GetComponent<BaseEnemyAI>() != null))
                    {
                        Health enemyHealth = h.GetComponent<Health>();
                        if (enemyHealth != null && !enemyHealth.IsDead)
                        {
                            enemyHealth.TakeDamage(25);
                        }

                        BaseEnemyAI ai = h.GetComponent<BaseEnemyAI>();
                        if (ai != null)
                        {
                            Vector3 pushDir = (h.transform.position - _player.transform.position).normalized;
                            ai.ApplyMeleeKnockback(pushDir, 18f, 1.0f); // Impact knockback + 1s stun
                        }
                    }
                }

                if (_chargeTimer <= 0f)
                {
                    _isCharging = false;
                }
            }
        }

        public void OnDestroy() { _isCharging = false; }
    }

    // =========================================================================
    //  3. Phantom: Shadow Step
    // =========================================================================
    public class ShadowStepAbility : IShiftAbility
    {
        public string AbilityName => "Shadow Step";
        public string Description => "Phase into shadow, walk through enemies, guaranteed 2.0x crit on exit";
        public bool IsActive => _isInShadow;
        public float CooldownRemaining => 0f;
        public float CooldownDuration => 0f;

        private PlayerController _player;
        private bool _isInShadow = false;
        private bool _hasGuaranteedCrit = false;
        private SpriteRenderer _sr;
        private Color _originalColor = Color.white;

        public bool HasGuaranteedCrit => _hasGuaranteedCrit;

        public void Initialize(PlayerController player)
        {
            _player = player;
            if (_player != null)
            {
                _sr = _player.GetComponent<SpriteRenderer>();
                if (_sr != null) _originalColor = _sr.color;
            }
        }

        public void OnShiftPressed() { }

        public void OnShiftHeld()
        {
            if (_player == null || _player.CurrentHunger <= 0f)
            {
                if (_isInShadow) OnShiftReleased();
                return;
            }

            if (!_isInShadow)
            {
                _isInShadow = true;
                if (_sr != null)
                {
                    _sr.color = new Color(0.4f, 0.5f, 1f, 0.45f); // Ghost blue tint
                }
            }
        }

        public void OnShiftReleased()
        {
            if (_isInShadow)
            {
                _isInShadow = false;
                _hasGuaranteedCrit = true; // Next attack lands critical damage!
                if (_sr != null)
                {
                    _sr.color = _originalColor;
                }
            }
        }

        public void ConsumeCritBonus()
        {
            _hasGuaranteedCrit = false;
        }

        public void UpdateAbility()
        {
            if (_isInShadow && _player != null)
            {
                // Extra hunger drain applied in shadow form
                _player.DrainHunger(Time.deltaTime * 2.5f);
            }
        }

        public void OnDestroy()
        {
            _isInShadow = false;
            if (_sr != null) _sr.color = _originalColor;
        }
    }

    // =========================================================================
    //  4. Marksman: Eagle Eye
    // =========================================================================
    public class EagleEyeAbility : IShiftAbility
    {
        public string AbilityName => "Eagle Eye";
        public string Description => "Tactical stance: camera zooms out 1.35x, 2x weapon range, piercing bullets";
        public bool IsActive => _isAiming;
        public float CooldownRemaining => 0f;
        public float CooldownDuration => 0f;

        private PlayerController _player;
        private bool _isAiming = false;
        private Camera _cam;
        private float _defaultCamSize = 5.0f;
        private float _targetCamSize = 7.0f;

        public void Initialize(PlayerController player)
        {
            _player = player;
            _cam = Camera.main;
            if (_cam != null)
            {
                _defaultCamSize = _cam.orthographicSize;
                _targetCamSize = _defaultCamSize * 1.35f;
            }
        }

        public void OnShiftPressed() { }

        public void OnShiftHeld()
        {
            if (_player == null || _player.CurrentHunger <= 0f)
            {
                if (_isAiming) OnShiftReleased();
                return;
            }

            _isAiming = true;
        }

        public void OnShiftReleased()
        {
            _isAiming = false;
        }

        public void UpdateAbility()
        {
            if (_cam != null)
            {
                float target = _isAiming ? _targetCamSize : _defaultCamSize;
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, target, Time.deltaTime * 4.5f);
            }

            if (_isAiming && _player != null)
            {
                _player.DrainHunger(Time.deltaTime * 0.8f);
            }
        }

        public void OnDestroy()
        {
            _isAiming = false;
            if (_cam != null) _cam.orthographicSize = _defaultCamSize;
        }
    }

    // =========================================================================
    //  5. Scavenger: Toxic Trail
    // =========================================================================
    public class ToxicTrailAbility : IShiftAbility
    {
        public string AbilityName => "Toxic Trail";
        public string Description => "Leaves toxic cloud trail that slows enemies by 50% with poison DoT";
        public bool IsActive => _isActive;
        public float CooldownRemaining => 0f;
        public float CooldownDuration => 0f;

        private PlayerController _player;
        private bool _isActive = false;
        private float _cloudSpawnTimer = 0f;
        private const float CloudInterval = 0.22f;

        public void Initialize(PlayerController player)
        {
            _player = player;
        }

        public void OnShiftPressed() { }

        public void OnShiftHeld()
        {
            if (_player == null || _player.CurrentHunger <= 0f)
            {
                if (_isActive) OnShiftReleased();
                return;
            }

            _isActive = true;
        }

        public void OnShiftReleased()
        {
            _isActive = false;
        }

        public void UpdateAbility()
        {
            if (_isActive && _player != null)
            {
                _player.DrainHunger(Time.deltaTime * 1.8f);

                _cloudSpawnTimer -= Time.deltaTime;
                if (_cloudSpawnTimer <= 0f)
                {
                    _cloudSpawnTimer = CloudInterval;
                    SpawnToxicCloudDecal(_player.transform.position);
                }
            }
        }

        private void SpawnToxicCloudDecal(Vector3 pos)
        {
            GameObject cloudObj = new GameObject("ToxicCloudDecal");
            cloudObj.transform.position = pos + Vector3.up * 0.1f;
            ToxicCloudHazard hazard = cloudObj.AddComponent<ToxicCloudHazard>();
            hazard.Initialize(pos, 3.5f, 1.6f, 8f);
        }

        public void OnDestroy() { _isActive = false; }
    }

    // =========================================================================
    //  Hazard Trigger Component for Toxic Trail
    // =========================================================================
    public class ToxicCloudHazard : MonoBehaviour
    {
        private float _lifetime;
        private float _radius;
        private float _dotDamage;
        private float _timer = 0f;
        private SpriteRenderer _sr;

        public void Initialize(Vector3 pos, float duration, float radius, float dotDamage)
        {
            _lifetime = duration;
            _radius = radius;
            _dotDamage = dotDamage;

            // Simple visual ring/decal
            GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vis.transform.SetParent(transform, false);
            vis.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vis.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            Destroy(vis.GetComponent<Collider>());

            MeshRenderer mr = vis.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
                mr.material.color = new Color(0.2f, 0.9f, 0.3f, 0.28f); // Toxic green
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Damage and slow nearby enemies
            Collider[] hits = Physics.OverlapSphere(transform.position, _radius);
            foreach (var h in hits)
            {
                if (h != null && (h.CompareTag("Enemy") || h.GetComponent<BaseEnemyAI>() != null))
                {
                    Health health = h.GetComponent<Health>();
                    if (health != null && !health.IsDead)
                    {
                        health.TakeDamage(_dotDamage * Time.deltaTime);
                    }
                }
            }
        }
    }
}
