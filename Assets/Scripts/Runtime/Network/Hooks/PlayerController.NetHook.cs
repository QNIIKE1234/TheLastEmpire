using UnityEngine;
using UnityEngine.InputSystem;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// PlayerController.NetHook — ส่วนขยาย partial class สำหรับระบบ Co-op Multiplayer 1–6 คน
    /// - แยกควบคุม LocalPlayer (0ms Input Lag) และ RemotePlayer / Simulated Co-op Teammate
    /// - ระบบล้มคลานรอเพื่อนชุบชีวิต (Co-op Downed & Revive: 30 วินาที / กด E ค้าง 3 วินาที ชุบเลือด 30%)
    /// - แสดงป้ายชื่อ + หลอดเลือดเหนือหัวเพื่อนร่วมทีม
    /// </summary>
    public partial class PlayerController
    {
        public static bool NextInstantiatedIsRemote = false;

        public bool IsRemotePlayer { get; set; } = false;
        public bool IsSimulatedCoopBot { get; set; } = false;
        public ulong NetworkClientId { get; set; } = 0;
        public int PartySlotIndex { get; set; } = 0;
        public string NetworkPlayerName { get; set; } = "PLAYER-1";
        public int NetworkStageX { get; set; } = 64;
        public int NetworkStageY { get; set; } = 64;

        /// <summary>
        /// ตรวจสอบว่าผู้เล่นคนนี้อยู่ในช่องพิกัดด่าน (Stage X, Y) เดียวกับผู้เล่นเครื่องเราหรือไม่
        /// - ถ้าอยู่ช่องเดียวกัน -> มองเห็นกันและร่วมสู้ด้วยกัน
        /// - ถ้าอยู่คนละช่อง -> ซ่อนตัวละครของอีกฝ่ายไว้จนกว่าจะเดินมาเจอกันในช่องเดียวกัน
        /// </summary>
        public bool IsInSameStageAsLocalPlayer
        {
            get
            {
                if (!IsRemotePlayer || IsSimulatedCoopBot) return true;
                int localX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
                int localY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
                return NetworkStageX == localX && NetworkStageY == localY;
            }
        }

        public void RefreshStageVisibility()
        {
            if (!IsRemotePlayer || IsSimulatedCoopBot) return;
            bool shouldBeVisible = IsInSameStageAsLocalPlayer;
            if (gameObject.activeSelf != shouldBeVisible)
            {
                gameObject.SetActive(shouldBeVisible);
            }
        }

        // Co-op Downed & Revive State (Section 4.7)
        public const float MaxDownedBleedoutSeconds = 30f;
        public const float ReviveHoldDurationSeconds = 3.0f;
        public const float ReviveInteractRadius = 2.4f;

        public bool IsDowned { get; private set; } = false;
        public float DownedBleedoutTimer { get; private set; } = 0f;
        public float ReviveProgress01 { get; private set; } = 0f;

        private Vector3 _networkTargetPosition;
        private Vector3 _networkTargetAimDir = Vector3.forward;
        private Vector3 _networkTargetVelocity;
        private bool _hasNetworkPose = false;

        // AI timers for Simulated Co-op Teammate Bot (+ Manual P2 Arrow-Key Override)
        private float _botDecisionTimer = 0f;
        private float _botFireTimer = 0f;
        private float _manualP2OverrideTimer = 0f;
        private Vector3 _botMoveTargetOffset = new Vector3(1.8f, 0f, -1.5f);

        public HeldItemType CurrentHeldItemType => _heldItemType;

        public void ConfigureAsRemotePlayer(ulong clientId, int slotIndex, string playerName, string characterId, bool isSimulatedBot)
        {
            IsRemotePlayer = true;
            IsSimulatedCoopBot = isSimulatedBot;
            NetworkClientId = clientId;
            PartySlotIndex = slotIndex;
            NetworkPlayerName = string.IsNullOrEmpty(playerName) ? $"ALLY-{slotIndex + 1}" : playerName;

            _networkTargetPosition = transform.position;
            _networkTargetAimDir = transform.forward;
            _hasNetworkPose = true;

            float angle = (slotIndex * 65f) * Mathf.Deg2Rad;
            _botMoveTargetOffset = new Vector3(Mathf.Cos(angle) * 2.2f, 0f, Mathf.Sin(angle) * 2.2f);

            // ปิด PlayerInput บนตัวละคร Remote เพื่อไม่ให้คีย์บอร์ด/เมาส์ของเราไปบังคับตัวเพื่อน
            PlayerInput pi = GetComponent<PlayerInput>();
            if (pi != null)
            {
                pi.enabled = false;
            }

            // ซ่อนวงเล็งระเบิด/เลเซอร์ของตัวเพื่อนบนจอเรา
            if (_throwRangeIndicator != null) _throwRangeIndicator.gameObject.SetActive(false);
            if (_aoeIndicator != null) _aoeIndicator.gameObject.SetActive(false);
            if (_throwRangeSpriteRenderer != null) _throwRangeSpriteRenderer.gameObject.SetActive(false);
            if (_aoeSpriteRenderer != null) _aoeSpriteRenderer.gameObject.SetActive(false);
            if (_laserSightIndicator != null) _laserSightIndicator.gameObject.SetActive(false);
            if (_laserTargetDot != null) _laserTargetDot.gameObject.SetActive(false);

            // สลับโมเดลตัวละครตาม Character ID ของเพื่อน
            if (!string.IsNullOrEmpty(characterId))
            {
                ApplyRemoteCharacterById(characterId);
            }

            PlayerRegistry.Register(this);
        }

        public void ApplyRemoteCharacterById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;
            if (activeCharacterData != null && string.Equals(activeCharacterData.characterId, characterId, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (characterModels != null)
            {
                for (int i = 0; i < characterModels.Count; i++)
                {
                    var entry = characterModels[i];
                    if (entry == null) continue;
                    string id = entry.characterData != null ? entry.characterData.characterId : entry.characterId;
                    if (string.Equals(id, characterId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (entry.characterData != null)
                        {
                            SwitchCharacterModel(entry.characterData);
                        }
                        else if (entry.modelObject != null)
                        {
                            ApplySelectedCharacterModel(activeCharacterData);
                        }
                        return;
                    }
                }
            }
        }

        public void ApplyNetworkState(PlayerStateNetPayload state)
        {
            if (!IsRemotePlayer) return;

            NetworkPlayerName = string.IsNullOrEmpty(state.playerName) ? NetworkPlayerName : state.playerName;
            PartySlotIndex = state.slotIndex;
            NetworkStageX = state.stageX;
            NetworkStageY = state.stageY;
            RefreshStageVisibility();

            _networkTargetPosition = state.position;
            _networkTargetVelocity = state.velocity;
            if (state.aimDirection.sqrMagnitude > 0.001f)
            {
                _networkTargetAimDir = state.aimDirection.normalized;
            }
            _hasNetworkPose = true;

            _moveInput = new Vector2(state.velocity.x, state.velocity.z);
            if (_moveInput.sqrMagnitude > 1f) _moveInput.Normalize();

            _isSprinting = state.isSprinting;
            _isDashing = state.isDashing;
            IsDowned = state.isDowned;
            DownedBleedoutTimer = state.downedRemainingSeconds;
            ReviveProgress01 = state.reviveProgress01;

            if (_isSitting != state.isSitting)
            {
                SetSitting(state.isSitting);
            }

            if (!string.IsNullOrEmpty(state.characterId))
            {
                ApplyRemoteCharacterById(state.characterId);
            }

            if (_health != null && state.maxHealth > 0f)
            {
                if (Mathf.Abs(_health.MaxHealth - state.maxHealth) > 0.5f)
                {
                    _health.SetMaxHealth(state.maxHealth, false);
                }
                float diff = state.currentHealth - _health.CurrentHealth;
                if (Mathf.Abs(diff) > 0.5f)
                {
                    if (diff > 0f) _health.Heal(diff);
                    else _health.TakeDamage(-diff, transform.position, false);
                }
            }

            if (!string.IsNullOrEmpty(state.activeWeaponName))
            {
                HeldItemType remoteType = (HeldItemType)state.heldItemType;
                if (remoteType == HeldItemType.Ranged && CurrentWeaponName != state.activeWeaponName)
                {
                    SetHeldRangedWeapon(state.activeWeaponName);
                }
                else if (remoteType == HeldItemType.Melee && CurrentMeleeWeaponName != state.activeWeaponName)
                {
                    SetHeldMeleeWeapon(state.activeWeaponName);
                }
            }
        }

        private void UpdateRemotePlayerVisuals()
        {
            UpdateMuzzleFlashLight();

            if (IsSimulatedCoopBot)
            {
                UpdateSimulatedCoopBotAI();
                UpdateCoopDownedAndRevive();
                UpdateAnimation();
                return;
            }

            if (_hasNetworkPose)
            {
                transform.position = Vector3.Lerp(transform.position, _networkTargetPosition, Time.deltaTime * 16f);
                if (!_isSitting && _networkTargetAimDir.sqrMagnitude > 0.01f)
                {
                    transform.forward = Vector3.Slerp(transform.forward, _networkTargetAimDir, Time.deltaTime * 18f);
                    _aimDirection = transform.forward;
                }
            }

            UpdateAnimation();
        }

        private void FixedUpdateRemotePlayer()
        {
            if (IsSimulatedCoopBot)
            {
                if (IsDowned || (_health != null && _health.IsDead))
                {
                    if (_rb != null) _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                    return;
                }
                if (_rb != null)
                {
                    float spd = _isSprinting ? moveSpeed * 1.35f : moveSpeed;
                    _rb.linearVelocity = new Vector3(_moveInput.x * spd, _rb.linearVelocity.y, _moveInput.y * spd);
                }
                return;
            }

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// ลอจิก AI สำหรับเพื่อนร่วมทีมจำลอง (Co-op Bot Teammate)
        /// - เดินตามคุ้มกันหัวหน้าทีม, หันยิงซอมบี้ที่อยู่ในระยะอัตโนมัติ, และวิ่งเข้ามาชุบชีวิตเมื่อผู้เล่นล้ม (Downed)
        /// </summary>
        private void UpdateSimulatedCoopBotAI()
        {
            if (_health != null && _health.IsDead) return;
            if (IsDowned)
            {
                _moveInput = Vector2.zero;
                return;
            }

            if (_fireCooldownTimer > 0f) _fireCooldownTimer -= Time.deltaTime;
            _botDecisionTimer -= Time.deltaTime;
            _botFireTimer -= Time.deltaTime;

            // รองรับการบังคับผู้เล่นคนที่ 2 (P2) ด้วยปุ่มลูกศร (Arrow Keys) + Right Ctrl / Enter เพื่อยิงปืน
            // สำหรับกรณีต้องการเทสต์บังคับ 2 ตัวละครพร้อมกันบนหน้าจอเดียว!
            if (PartySlotIndex == 1 && Keyboard.current != null)
            {
                Vector2 p2ArrowInput = Vector2.zero;
                if (Keyboard.current.upArrowKey.isPressed) p2ArrowInput.y += 1f;
                if (Keyboard.current.downArrowKey.isPressed) p2ArrowInput.y -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed) p2ArrowInput.x += 1f;
                if (Keyboard.current.leftArrowKey.isPressed) p2ArrowInput.x -= 1f;
                bool p2Shoot = Keyboard.current.rightCtrlKey.isPressed || Keyboard.current.enterKey.isPressed || Keyboard.current.numpad0Key.isPressed;

                if (p2ArrowInput.sqrMagnitude > 0.01f || p2Shoot)
                {
                    _manualP2OverrideTimer = 3.5f;
                }

                if (_manualP2OverrideTimer > 0f)
                {
                    _manualP2OverrideTimer -= Time.deltaTime;
                    if (p2ArrowInput.sqrMagnitude > 1f) p2ArrowInput.Normalize();
                    _moveInput = p2ArrowInput;
                    _isSprinting = Keyboard.current.rightShiftKey.isPressed && _moveInput.sqrMagnitude > 0.01f;

                    if (_moveInput.sqrMagnitude > 0.01f)
                    {
                        _aimDirection = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
                        transform.forward = _aimDirection;
                    }

                    if (p2Shoot && _fireCooldownTimer <= 0f)
                    {
                        if (CurrentWeapon != null && CurrentWeapon.currentMagazine <= 0)
                        {
                            CurrentWeapon.currentMagazine = CurrentWeapon.magazineSize > 0 ? CurrentWeapon.magazineSize : 12;
                        }
                        ShootWeapon();
                    }
                    return;
                }
            }

            PlayerController leader = PlayerRegistry.LocalPlayer;
            if (leader == null) return;

            // 1. หากหัวหน้าทีมหรือเพื่อนล้ม (IsDowned) ให้บอทวิ่งเข้าไปชุบชีวิตทันที!
            PlayerController downedAlly = FindNearestDownedTeammate(18f);
            if (downedAlly != null)
            {
                Vector3 toDowned = downedAlly.transform.position - transform.position;
                toDowned.y = 0f;
                float distToDowned = toDowned.magnitude;
                if (distToDowned > ReviveInteractRadius * 0.75f)
                {
                    Vector3 dir = toDowned.normalized;
                    _moveInput = new Vector2(dir.x, dir.z);
                    _isSprinting = true;
                    _aimDirection = dir;
                    transform.forward = dir;
                }
                else
                {
                    _moveInput = Vector2.zero;
                    _isSprinting = false;
                    downedAlly.AddReviveProgress(Time.deltaTime / ReviveHoldDurationSeconds);
                }
                return;
            }

            // 2. เดินเกาะกลุ่มกับหัวหน้าทีม
            Vector3 desiredFollowPos = leader.transform.position + _botMoveTargetOffset;
            Vector3 toFollow = desiredFollowPos - transform.position;
            toFollow.y = 0f;
            float followDist = toFollow.magnitude;

            if (followDist > 14f)
            {
                // วาร์ปตามถ้าหลุดขอบไกลเกินไป
                transform.position = desiredFollowPos + Vector3.up * 0.2f;
                _moveInput = Vector2.zero;
                _isSprinting = false;
            }
            else if (followDist > 1.35f)
            {
                Vector3 dir = toFollow.normalized;
                _moveInput = new Vector2(dir.x, dir.z);
                _isSprinting = followDist > 3.8f;
            }
            else
            {
                _moveInput = Vector2.zero;
                _isSprinting = false;
            }

            // 3. ค้นหาศัตรูที่ใกล้ที่สุดในฉากแล้วยิงช่วยสู้
            BaseEnemyAI nearestEnemy = FindNearestAliveEnemy(13.5f);
            if (nearestEnemy != null)
            {
                Vector3 toEnemy = nearestEnemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.01f)
                {
                    _aimDirection = toEnemy.normalized;
                    _aimTargetPoint = nearestEnemy.transform.position;
                    transform.forward = _aimDirection;
                }

                if (_botFireTimer <= 0f && _fireCooldownTimer <= 0f)
                {
                    if (CurrentWeapon != null && CurrentWeapon.currentMagazine <= 0)
                    {
                        CurrentWeapon.currentMagazine = CurrentWeapon.magazineSize > 0 ? CurrentWeapon.magazineSize : 12;
                    }
                    ShootWeapon();
                    _botFireTimer = Mathf.Max(0.28f, CurrentWeapon != null ? CurrentWeapon.fireRate * 1.15f : 0.35f);
                }
            }
            else if (_moveInput.sqrMagnitude > 0.05f)
            {
                _aimDirection = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
                transform.forward = _aimDirection;
            }
        }

        private BaseEnemyAI FindNearestAliveEnemy(float maxRadius)
        {
            if (GameManager.Instance == null || GameManager.Instance.ActiveEnemies == null) return null;
            BaseEnemyAI best = null;
            float bestDistSq = maxRadius * maxRadius;
            var list = GameManager.Instance.ActiveEnemies;
            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = list[i];
                if (go == null || !go.activeInHierarchy) continue;
                BaseEnemyAI ai = go.GetComponent<BaseEnemyAI>();
                if (ai == null) continue;
                Health h = go.GetComponent<Health>();
                if (h != null && h.IsDead) continue;

                float dSq = (go.transform.position - transform.position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = ai;
                }
            }
            return best;
        }

        // =====================================================
        //  Co-op Downed & Revive System (Section 4.7)
        // =====================================================

        private bool TryEnterCoopDownedState()
        {
            // หากเล่นคนเดียว (ไม่มีเพื่อนในปาร์ตี้) ให้ตายตามปกติ
            if (PlayerRegistry.GetTotalPlayerCount() <= 1)
            {
                IsDowned = false;
                return false;
            }

            // หากยังไม่ได้อยู่ในสถานะ Downed และยังมีเพื่อนคนอื่นยืนสู้ได้ ให้เข้าสู่สถานะล้มรอชุบ 30 วินาที
            if (!IsDowned)
            {
                IsDowned = true;
                DownedBleedoutTimer = MaxDownedBleedoutSeconds;
                ReviveProgress01 = 0f;

                // คงเลือดไว้ที่ 1 HP ชั่วคราวระหว่างล้มคลาน เพื่อไม่ให้ Health.IsDead ล็อกทุกอย่างถาวรจนชุบไม่ได้
                if (_health != null)
                {
                    _health.ResetHealth();
                    float leaveOneHp = Mathf.Max(0f, _health.MaxHealth - 1f);
                    if (leaveOneHp > 0f)
                    {
                        _health.TakeDamage(leaveOneHp, transform.position, false);
                    }
                    _health.TriggerInvulnerability(2.5f);
                }

                SetSitting(true);
                Debug.Log($"<color=#F59E0B>[Co-op Revive]</color> {NetworkPlayerName} is DOWNED! 30s to revive (Hold [E] near teammate).");

                // ตรวจสอบว่าถ้าทุกคนในปาร์ตี้ล้มพร้อมกันหมดแล้ว ให้สั่ง Party Wipe ทันที
                if (PlayerRegistry.AreAllPlayersDeadOrDowned())
                {
                    TriggerPartyWipeDeath();
                }
                return true;
            }

            return false;
        }

        private void TriggerPartyWipeDeath()
        {
            var all = PlayerRegistry.AllPlayers;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerController p = all[i];
                if (p != null)
                {
                    p.IsDowned = false;
                }
            }
            Debug.Log("<color=#EF4444>[Co-op Revive]</color> PARTY WIPE! All operatives are downed.");
        }

        private void UpdateCoopDownedAndRevive()
        {
            if (IsDowned)
            {
                DownedBleedoutTimer -= Time.deltaTime;
                if (_health != null)
                {
                    // ป้องกันซอมบี้ตีซ้ำตอนล้มรอชุบ
                    _health.TriggerInvulnerability(0.5f);
                }

                if (DownedBleedoutTimer <= 0f)
                {
                    IsDowned = false;
                    SetSitting(false);
                    if (_health != null && !_health.IsDead)
                    {
                        _health.TakeDamage(_health.CurrentHealth + 999f, transform.position, false);
                    }
                }
                return;
            }

            // หากเรายืนอยู่และกดปุ่ม [E] ค้างใกล้เพื่อนที่ล้มอยู่ (ระยะ 2.4m) ให้ชุบชีวิตเพื่อน
            if (!IsRemotePlayer && Keyboard.current != null && Keyboard.current.eKey.isPressed)
            {
                PlayerController downedAlly = FindNearestDownedTeammate(ReviveInteractRadius);
                if (downedAlly != null)
                {
                    downedAlly.AddReviveProgress(Time.deltaTime / ReviveHoldDurationSeconds);
                }
            }
        }

        private PlayerController FindNearestDownedTeammate(float radius)
        {
            PlayerController best = null;
            float bestDistSq = radius * radius;
            var all = PlayerRegistry.AllPlayers;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerController candidate = all[i];
                if (candidate == null || candidate == this || !candidate.IsDowned) continue;
                float dSq = (candidate.transform.position - transform.position).sqrMagnitude;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    best = candidate;
                }
            }
            return best;
        }

        public void AddReviveProgress(float delta01)
        {
            if (!IsDowned) return;
            ReviveProgress01 = Mathf.Clamp01(ReviveProgress01 + delta01);
            if (ReviveProgress01 >= 1f)
            {
                CompleteRevive();
            }
        }

        public void CompleteRevive()
        {
            if (!IsDowned) return;
            IsDowned = false;
            DownedBleedoutTimer = 0f;
            ReviveProgress01 = 0f;
            SetSitting(false);

            if (_health != null)
            {
                _health.ResetHealth();
                // ฟื้นขึ้นมาด้วยเลือด 30% ตาม GDD Section 4.7
                float targetHp = Mathf.Max(15f, _health.MaxHealth * 0.30f);
                float dmgToTrim = Mathf.Max(0f, _health.MaxHealth - targetHp);
                if (dmgToTrim > 0f)
                {
                    _health.TakeDamage(dmgToTrim, transform.position, false);
                }
                _health.TriggerInvulnerability(2.0f);
            }

            Debug.Log($"<color=#22C55E>[Co-op Revive]</color> {NetworkPlayerName} was REVIVED at 30% HP!");
        }

        // =====================================================
        //  Combat & Loot Network Notification Hooks
        // =====================================================

        private void NotifyNetworkShoot(Vector3 spawnPos, Vector3 aimDir, Vector3 targetPoint, string weaponName, float finalDamage, float weaponRange, bool isRocket)
        {
            if (IsRemotePlayer || !EmpireNetworkManager.IsOnline) return;
            NetworkPlayerBridge bridge = GetComponent<NetworkPlayerBridge>();
            if (bridge != null)
            {
                bridge.NotifyLocalWeaponFired(spawnPos, aimDir, targetPoint, weaponName, finalDamage, weaponRange, isRocket);
            }
        }

        private void NotifyNetworkMelee(Vector3 attackPoint, Vector3 aimDir, string weaponName, int comboStep, float damage, float radius)
        {
            if (IsRemotePlayer || !EmpireNetworkManager.IsOnline) return;
            NetworkPlayerBridge bridge = GetComponent<NetworkPlayerBridge>();
            if (bridge != null)
            {
                bridge.NotifyLocalMeleeSwing(attackPoint, aimDir, weaponName, comboStep, damage, radius);
            }
        }

        private bool TryNetworkClaimCollectible(CollectibleItem item)
        {
            if (item == null || item.IsCollected) return true;
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.IsOnlineHost)
            {
                // เครื่อง Host ตรวจสอบผ่าน NetworkLootManager และอนุญาตทันที
                if (EmpireNetworkManager.IsOnlineHost && NetworkLootManager.Instance != null)
                {
                    return !NetworkLootManager.Instance.TryClaimLootOnHostImmediate(item, this);
                }
                return false;
            }

            // เครื่อง Client ส่ง Atomic Claim ไปหา Host เพื่อป้องกัน Loot Dupe 100%
            if (NetworkLootManager.Instance != null)
            {
                NetworkLootManager.Instance.RequestClaimLootFromClient(item);
                return true;
            }
            return false;
        }
    }
}
