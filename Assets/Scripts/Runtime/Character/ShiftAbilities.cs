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
        public string AbilityName => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityName : "Adrenaline Rush";
        public string Description => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityDescription : "+75% Speed, +30% Attack Rate, 20% Dmg Resistance, Rapid Hunger Drain";
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
                // Extra hunger drain applied during adrenaline state (configured in CharacterData)
                float drainRate = (_player.ActiveCharacterData != null) ? _player.ActiveCharacterData.adrenalineHungerDrainRate : 1.5f;
                _player.DrainHunger(Time.deltaTime * drainRate);
            }
        }

        public void OnDestroy() { _isActive = false; }
    }

    // =========================================================================
    //  2. Juggernaut: Unstoppable Charge
    // =========================================================================
    public class UnstoppableChargeAbility : IShiftAbility
    {
        public string AbilityName => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityName : "Unstoppable Charge";
        public string Description => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityDescription : "Burst dash that deflects bullets, deals impact damage, and stuns enemies";
        public bool IsActive => _isCharging;
        public float CooldownRemaining => _cooldownTimer;
        public float CooldownDuration => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftCooldown : 5.0f;

        private PlayerController _player;
        private bool _isCharging = false;
        private float _chargeTimer = 0f;
        private float _cooldownTimer = 0f;
        private Vector3 _chargeDir;

        private float GetChargeSpeed() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.chargeSpeed : 18f;
        private float GetChargeDuration() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.chargeDuration : 0.35f;
        private float GetChargeDamage() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.chargeDamage : 25f;
        private float GetChargeStunDuration() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.chargeStunDuration : 1.0f;
        private float GetChargeHungerCost() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.chargeHungerCost : 5.0f;

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
            _chargeTimer = GetChargeDuration();
            _cooldownTimer = CooldownDuration;
            _player.DrainHunger(GetChargeHungerCost()); // Configured hunger cost for burst
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

                float speed = GetChargeSpeed();
                float damage = GetChargeDamage();
                float stun = GetChargeStunDuration();

                // Move with high speed
                Rigidbody rb = _player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = _chargeDir * speed;
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
                            enemyHealth.TakeDamage(damage);
                        }

                        BaseEnemyAI ai = h.GetComponent<BaseEnemyAI>();
                        if (ai != null)
                        {
                            Vector3 pushDir = (h.transform.position - _player.transform.position).normalized;
                            ai.ApplyMeleeKnockback(pushDir, speed, stun); // Impact knockback + configured stun
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
        public string AbilityName => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityName : "Shadow Step";
        public string Description => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityDescription : "Phase into shadow, walk through enemies, guaranteed critical strike on exit";
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
                float drainRate = (_player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shadowHungerDrainRate : 2.5f;
                _player.DrainHunger(Time.deltaTime * drainRate);
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
        public string AbilityName => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityName : "Eagle Eye";
        public string Description => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityDescription : "Tactical stance: camera zooms out, expanded weapon range, piercing bullets";
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
                _defaultCamSize = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;
                float zoomMult = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.eagleEyeZoomMultiplier : 1.20f;
                _targetCamSize = _defaultCamSize * zoomMult;
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
                float zoomMult = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.eagleEyeZoomMultiplier : 1.20f;
                _targetCamSize = _defaultCamSize * zoomMult;

                float target = _isAiming ? _targetCamSize : _defaultCamSize;
                if (_cam.orthographic)
                {
                    _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, target, Time.deltaTime * 5f);
                }
                else
                {
                    _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, target, Time.deltaTime * 5f);
                }
            }

            if (_isAiming && _player != null)
            {
                float drainRate = (_player.ActiveCharacterData != null) ? _player.ActiveCharacterData.eagleEyeHungerDrainRate : 0.8f;
                _player.DrainHunger(Time.deltaTime * drainRate);
            }
        }

        public void OnDestroy()
        {
            _isAiming = false;
            if (_cam != null)
            {
                if (_cam.orthographic) _cam.orthographicSize = _defaultCamSize;
                else _cam.fieldOfView = _defaultCamSize;
            }
        }
    }

    // =========================================================================
    //  5. Scavenger: Toxic Trail
    // =========================================================================
    public class ToxicTrailAbility : IShiftAbility
    {
        public string AbilityName => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityName : "Toxic Trail";
        public string Description => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityDescription : "Leaves toxic cloud trail that slows enemies with poison DoT";
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
                float drainRate = (_player.ActiveCharacterData != null) ? _player.ActiveCharacterData.toxicHungerDrainRate : 1.8f;
                _player.DrainHunger(Time.deltaTime * drainRate);

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
            float duration = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.toxicCloudDuration : 3.5f;
            float radius = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.toxicCloudRadius : 1.6f;
            float dps = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.toxicPoisonDps : 8f;
            float slow = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.toxicSlowPercent : 0.5f;

            GameObject cloudObj = new GameObject("ToxicCloudDecal");
            cloudObj.transform.position = pos + Vector3.up * 0.1f;
            ToxicCloudHazard hazard = cloudObj.AddComponent<ToxicCloudHazard>();
            hazard.Initialize(pos, duration, radius, dps, slow);
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
        private float _slowPercent;
        private float _timer = 0f;

        public void Initialize(Vector3 pos, float duration, float radius, float dotDamage, float slowPercent = 0.5f)
        {
            _lifetime = duration;
            _radius = radius;
            _dotDamage = dotDamage;
            _slowPercent = slowPercent;

            // Visual ring/decal
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
