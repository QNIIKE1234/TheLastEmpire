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
                float zoomMult = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.eagleEyeZoomMultiplier : 1.30f;
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
                float zoomMult = (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.eagleEyeZoomMultiplier : 1.30f;
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
    
    // =========================================================================
    //  5. Scavenger: Grappling Hook (Spider-Man Cable / Wire Launcher)
    // =========================================================================
    public class GrapplingHookAbility : IShiftAbility
    {
        public string AbilityName => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityName : "Grappling Hook";
        public string Description => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftAbilityDescription : "Fires a survival grappling wire forward: latches onto buildings/obstacles and zips you directly to the point.";
        public bool IsActive => _isPulling || _isExtendingMiss;
        public bool IsPulling => _isPulling;
        public float CooldownRemaining => _cooldownTimer;
        public float CooldownDuration => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.shiftCooldown : 3.0f;

        private PlayerController _player;
        private bool _isPulling = false;
        private bool _isExtendingMiss = false;
        private float _cooldownTimer = 0f;
        private float _pullTimer = 0f;
        private const float MaxPullDuration = 1.2f;

        private Vector3 _hookTargetPoint;
        private LineRenderer _cableLine;
        private GameObject _hookAnchorVisual;
        private float _lastDistanceToTarget = 999f;
        private float _stuckTimer = 0f;

        private float GetGrappleRange() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.grappleRange : 18f;
        private float GetGrapplePullSpeed() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.grapplePullSpeed : 22f;
        private float GetGrappleHungerCost() => (_player != null && _player.ActiveCharacterData != null) ? _player.ActiveCharacterData.grappleHungerCost : 3.5f;

        public void Initialize(PlayerController player)
        {
            _player = player;
            CreateCableRenderer();
        }

        public void OnShiftPressed()
        {
            if (_player == null || _cooldownTimer > 0f || _isPulling || _player.CurrentHunger <= 0f) return;

            Vector3 origin = _player.transform.position + Vector3.up * 0.7f;
            Vector3 aimDir = _player.AimDirection;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = _player.transform.forward;
            aimDir.Normalize();

            float maxRange = GetGrappleRange();

            // Raycast forward to detect buildings, walls, obstacles, structures
            RaycastHit[] hits = Physics.RaycastAll(origin, aimDir, maxRange, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit? validHit = null;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.root == _player.transform.root) continue;
                if (h.collider.CompareTag("Player")) continue;
                if (h.collider.isTrigger) continue;
                if (h.collider.GetComponent<CollectibleItem>() != null) continue;

                // Ignore flat ground plane (normal pointing mostly upward)
                if (h.normal.y > 0.65f) continue;

                // Ignore enemies so hook passes through them to latch onto buildings behind them
                if (h.collider.CompareTag("Enemy") || h.collider.GetComponent<BaseEnemyAI>() != null) continue;

                validHit = h;
                break;
            }

            if (validHit.HasValue)
            {
                RaycastHit hit = validHit.Value;
                Vector3 latchPt = hit.point;

                // Position target slightly away from wall along normal so player doesn't clip
                Vector3 standPos = latchPt + hit.normal * 0.8f;
                standPos.y = _player.transform.position.y; // Keep player level on ground

                _hookTargetPoint = standPos;
                _lastDistanceToTarget = Vector3.Distance(_player.transform.position, _hookTargetPoint);
                _stuckTimer = 0f;
                _isPulling = true;
                _pullTimer = MaxPullDuration;
                _cooldownTimer = CooldownDuration;

                _player.DrainHunger(GetGrappleHungerCost());

                ShowCable(origin, latchPt);
                CreateAnchorVisual(latchPt, hit.normal);
            }
            else
            {
                // Missed: shoot line forward briefly with short cooldown
                Vector3 missPt = origin + aimDir * (maxRange * 0.75f);
                _isExtendingMiss = true;
                _pullTimer = 0.22f;
                _cooldownTimer = 0.8f;
                ShowCable(origin, missPt);
            }
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

            if (_isExtendingMiss)
            {
                _pullTimer -= Time.deltaTime;
                if (_cableLine != null && _player != null)
                {
                    _cableLine.SetPosition(0, _player.transform.position + Vector3.up * 0.7f);
                }
                if (_pullTimer <= 0f)
                {
                    _isExtendingMiss = false;
                    HideCable();
                }
                return;
            }

            if (_isPulling && _player != null)
            {
                _pullTimer -= Time.deltaTime;

                Vector3 currentPos = _player.transform.position;
                Vector3 toTarget = _hookTargetPoint - currentPos;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                // Update cable origin to player's current moving position
                if (_cableLine != null)
                {
                    _cableLine.SetPosition(0, currentPos + Vector3.up * 0.7f);
                }

                // Grant brief invulnerability (I-Frames) during high-speed pull
                if (_player.PlayerHealth != null)
                {
                    _player.PlayerHealth.TriggerInvulnerability(0.1f);
                }

                // Detect if player is stuck against an obstacle
                if (Mathf.Abs(dist - _lastDistanceToTarget) < 0.05f)
                {
                    _stuckTimer += Time.deltaTime;
                }
                else
                {
                    _stuckTimer = 0f;
                }
                _lastDistanceToTarget = dist;

                // Check arrival, timeout, or stuck
                if (dist < 0.9f || _pullTimer <= 0f || _stuckTimer > 0.25f)
                {
                    EndGrapplePull();
                    return;
                }

                // Rapid pull towards target
                float speed = GetGrapplePullSpeed();
                Vector3 pullVelocity = toTarget.normalized * speed;

                Rigidbody rb = _player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(pullVelocity.x, rb.linearVelocity.y, pullVelocity.z);
                }

                // Make player face pulling direction
                if (toTarget.sqrMagnitude > 0.05f)
                {
                    _player.transform.forward = toTarget.normalized;
                }
            }
        }

        private void EndGrapplePull()
        {
            _isPulling = false;
            HideCable();
            if (_hookAnchorVisual != null)
            {
                GameObject.Destroy(_hookAnchorVisual);
                _hookAnchorVisual = null;
            }
            if (_player != null)
            {
                Rigidbody rb = _player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }
        }

        private void CreateCableRenderer()
        {
            if (_cableLine != null || _player == null) return;

            GameObject cableObj = new GameObject("GrappleCableRenderer");
            cableObj.transform.SetParent(_player.transform);
            cableObj.transform.localPosition = Vector3.zero;

            _cableLine = cableObj.AddComponent<LineRenderer>();
            _cableLine.startWidth = 0.06f;
            _cableLine.endWidth = 0.04f;
            _cableLine.useWorldSpace = true;
            _cableLine.loop = false;
            _cableLine.positionCount = 2;

            Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") 
                ?? Shader.Find("Unlit/Color") 
                ?? Shader.Find("Sprites/Default"));

            Color wireColor = new Color(0.95f, 0.85f, 0.25f, 0.95f); // High-contrast survival steel/amber wire
            lineMat.color = wireColor;
            _cableLine.material = lineMat;
            _cableLine.startColor = wireColor;
            _cableLine.endColor = new Color(0.85f, 0.95f, 0.35f, 0.90f);
            _cableLine.enabled = false;
        }

        private void ShowCable(Vector3 start, Vector3 end)
        {
            if (_cableLine == null) CreateCableRenderer();
            if (_cableLine != null)
            {
                _cableLine.positionCount = 2;
                _cableLine.SetPosition(0, start);
                _cableLine.SetPosition(1, end);
                _cableLine.enabled = true;
            }
        }

        private void HideCable()
        {
            if (_cableLine != null) _cableLine.enabled = false;
        }

        private void CreateAnchorVisual(Vector3 pos, Vector3 normal)
        {
            if (_hookAnchorVisual != null) GameObject.Destroy(_hookAnchorVisual);

            _hookAnchorVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _hookAnchorVisual.name = "GrappleHookAnchor";
            _hookAnchorVisual.transform.position = pos;
            _hookAnchorVisual.transform.localScale = Vector3.one * 0.28f;
            GameObject.Destroy(_hookAnchorVisual.GetComponent<Collider>());

            MeshRenderer mr = _hookAnchorVisual.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") 
                    ?? Shader.Find("Unlit/Color") 
                    ?? Shader.Find("Sprites/Default"));
                mr.material.color = new Color(1f, 0.85f, 0.2f, 1f);
            }
        }

        public void OnDestroy()
        {
            EndGrapplePull();
            if (_cableLine != null && _cableLine.gameObject != null)
            {
                GameObject.Destroy(_cableLine.gameObject);
                _cableLine = null;
            }
        }
    }

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
