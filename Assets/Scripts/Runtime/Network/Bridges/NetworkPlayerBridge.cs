using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// NetworkPlayerBridge — ซิงค์ข้อมูลตัวละครผู้เล่น 1–6 คน (Owner-Authoritative 0ms Input Lag + Client Prediction)
    /// - ส่งสถานะการเดิน การหันเมาส์ เลือด อาวุธที่ถือ และสถานะล้มรอชุบ (Downed & Revive)
    /// - ยิงกระสุนด้วย Client-Side Visual Prediction (0ms บนเครื่องคนยิง) พร้อมกระจาย RPC ให้เพื่อนเห็นกระสุนตรงกัน
    /// - วาดป้ายชื่อและหลอดเลือดเหนือหัวเพื่อนร่วมทีมในฉาก 3D
    /// </summary>
    public class NetworkPlayerBridge : NetworkBehaviour
    {
        public const float FriendlyFireDamageMultiplier = 0.20f; // 20% friendly fire damage in Multiplayer
        public static bool FriendlyFireEnabled = true;

        private PlayerController _player;
        private Rigidbody _rb;
        private float _syncSendTimer = 0f;
        private const float SyncIntervalSeconds = 0.045f; // ~22 Hz state broadcast

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_player == null) _player = GetComponent<PlayerController>();
            if (_player == null || _player.IsRemotePlayer) return;
            if (!EmpireNetworkManager.IsOnline) return;

            _syncSendTimer -= Time.unscaledDeltaTime;
            if (_syncSendTimer <= 0f)
            {
                _syncSendTimer = SyncIntervalSeconds;
                BroadcastLocalPlayerState();
            }
        }

        private void BroadcastLocalPlayerState()
        {
            if (EmpireNetworkManager.Instance == null || _player == null) return;

            Health h = _player.PlayerHealth ?? _player.GetComponent<Health>();
            string activeWep = _player.CurrentHeldItemType == PlayerController.HeldItemType.Melee
                ? _player.CurrentMeleeWeaponName
                : _player.CurrentWeaponName;

            int curStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int curInteriorId = BuildingInteriorManager.CurrentInteriorId;
            _player.NetworkStageX = curStageX;
            _player.NetworkStageY = curStageY;
            _player.NetworkInteriorId = curInteriorId;

            PlayerStateNetPayload payload = new PlayerStateNetPayload
            {
                clientId = EmpireNetworkManager.Instance.LocalClientId,
                slotIndex = _player.PartySlotIndex,
                playerName = _player.NetworkPlayerName ?? "SURVIVOR",
                characterId = _player.ActiveCharacterData != null ? _player.ActiveCharacterData.characterId : "VANGUARD",
                stageX = curStageX,
                stageY = curStageY,
                interiorId = curInteriorId,
                position = _player.transform.position,
                velocity = _rb != null ? _rb.linearVelocity : new Vector3(_player.MoveInput.x, 0f, _player.MoveInput.y),
                aimDirection = (_player.IsSitting || _player.IsSleeping) ? _player.transform.forward : _player.AimDirection,
                currentHealth = h != null ? h.CurrentHealth : 100f,
                maxHealth = h != null ? h.MaxHealth : 100f,
                heldItemType = (byte)_player.CurrentHeldItemType,
                activeWeaponName = activeWep ?? "",
                isSprinting = _player.IsSprinting,
                isSitting = _player.IsSitting,
                isSleeping = _player.IsSleeping,
                isDashing = false,
                isShiftActive = _player.ActiveShiftAbility != null && _player.ActiveShiftAbility.IsActive,
                shiftType = (byte)(_player.ActiveCharacterData != null ? (int)_player.ActiveCharacterData.shiftAbilityType : 0),
                isDowned = _player.IsDowned,
                downedRemainingSeconds = _player.DownedBleedoutTimer,
                reviveProgress01 = _player.ReviveProgress01,
                isFlashlightOn = NightCombatItemManager.Instance != null && NightCombatItemManager.Instance.IsFlashlightActive
            };

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.PlayerStateSync, w =>
            {
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                payload.NetworkSerialize(serializer);
            });
        }

        // =====================================================
        //  Ranged & Melee Combat RPC Sync (0ms Client Prediction)
        // =====================================================

        public void NotifyLocalWeaponFired(
            Vector3 spawnPos,
            Vector3 aimDir,
            Vector3 targetPoint,
            string weaponName,
            float damage,
            float range,
            bool isRocket,
            int pelletCount = 1,
            float spreadAngle = 0f,
            bool canPierce = false,
            string visualId = "Bullet")
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;

            int curStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int curInteriorId = BuildingInteriorManager.CurrentInteriorId;

            ProjectileFireNetPayload payload = new ProjectileFireNetPayload
            {
                shooterClientId = EmpireNetworkManager.Instance.LocalClientId,
                stageX = curStageX,
                stageY = curStageY,
                interiorId = curInteriorId,
                spawnPosition = spawnPos,
                aimDirection = aimDir,
                aimTargetPoint = targetPoint,
                weaponName = weaponName ?? "Pistol",
                pelletSeed = UnityEngine.Random.Range(1, 999999),
                damage = damage,
                weaponRange = range,
                isRocketLauncher = isRocket,
                pelletCount = Mathf.Max(1, pelletCount),
                spreadAngle = spreadAngle,
                canPierce = canPierce,
                visualId = string.IsNullOrEmpty(visualId) ? "Bullet" : visualId
            };

            FireProjectileServerRpc(payload);
        }

        [ServerRpc(RequireOwnership = false)]
        public void FireProjectileServerRpc(ProjectileFireNetPayload payload)
        {
            if (EmpireNetworkManager.Instance == null) return;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.ProjectileFire, w =>
            {
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                payload.NetworkSerialize(serializer);
            });
        }

        public static void HandleIncomingProjectileFire(ProjectileFireNetPayload payload, bool isHost)
        {
            if (EmpireNetworkManager.Instance == null) return;

            // หากเราเป็น Host และได้รับคำขอยิงจาก Client ให้กระจายต่อให้ผู้เล่นทุกคนเห็นกระสุนด้วย
            if (isHost && payload.shooterClientId != EmpireNetworkManager.Instance.LocalClientId)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.ProjectileFire, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    payload.NetworkSerialize(serializer);
                });
            }

            // ข้ามเครื่องคนยิงเอง (เพราะคนยิงแสดง Visual Prediction 0ms ไปแล้วตอนคลิกเมาส์)
            if (payload.shooterClientId == EmpireNetworkManager.Instance.LocalClientId)
            {
                return;
            }

            // หากคนยิงอยู่คนละช่องพิกัด (Stage X, Y) หรือคนละบ้าน (InteriorId) กับเรา ไม่ต้องเสกกระสุนของเขาในช่องของเรา
            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int myInteriorId = BuildingInteriorManager.CurrentInteriorId;
            if (payload.stageX != myStageX || payload.stageY != myStageY || payload.interiorId != myInteriorId)
            {
                return;
            }

            PlayerController shooter = PlayerRegistry.GetPlayerByClientId(payload.shooterClientId);
            int pellets = Mathf.Max(1, payload.pelletCount);
            float spread = Mathf.Max(0f, payload.spreadAngle);
            string visualId = string.IsNullOrEmpty(payload.visualId) ? "Bullet" : payload.visualId;
            bool isFireballSkill = visualId.Equals("FlamethrowerFireball", System.StringComparison.OrdinalIgnoreCase);
            Vector3 baseDir = payload.aimDirection.sqrMagnitude > 0.01f ? payload.aimDirection.normalized : Vector3.forward;

            // หากเพื่อนกำลังพ่นไฟ Flamethrower ให้เปิด Cone Fire VFX บนตัวเพื่อนด้วย และไม่ต้องเสกกระสุนโลหะซ้อน
            bool isFlameConeShot = visualId.Equals("FlameCone", System.StringComparison.OrdinalIgnoreCase);
            if (!isFireballSkill && (isFlameConeShot || (!string.IsNullOrEmpty(payload.weaponName) &&
                payload.weaponName.IndexOf("Flamethrower", System.StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                if (shooter != null)
                {
                    shooter.TriggerRemoteFlamethrowerConeVfx();
                }
                return;
            }

            for (int i = 0; i < pellets; i++)
            {
                Vector3 bulletDir = baseDir;
                if (spread > 0f && pellets > 1)
                {
                    float angleOffset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, (float)i / (pellets - 1));
                    bulletDir = Quaternion.Euler(0f, angleOffset, 0f) * baseDir;
                }

                GameObject bulletObj = null;
                if (isFireballSkill)
                {
                    if (ObjectPoolManager.Instance != null)
                    {
                        bulletObj = ObjectPoolManager.Instance.SpawnFromPool("FlamethrowerFireball", payload.spawnPosition, Quaternion.LookRotation(bulletDir));
                    }
                    if (bulletObj == null)
                    {
                        GameObject fbPrefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/FlamethrowerFireballPrefabs");
                        if (fbPrefab != null) bulletObj = Instantiate(fbPrefab, payload.spawnPosition, Quaternion.LookRotation(bulletDir));
                    }
                }
                else
                {
                    if (ObjectPoolManager.Instance != null)
                    {
                        bulletObj = ObjectPoolManager.Instance.SpawnFromPool("Bullet", payload.spawnPosition, Quaternion.LookRotation(bulletDir));
                    }
                    if (bulletObj == null)
                    {
                        GameObject prefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/BulletPrefabs");
                        if (prefab != null)
                        {
                            bulletObj = Instantiate(prefab, payload.spawnPosition, Quaternion.LookRotation(bulletDir));
                        }
                    }
                }

                if (bulletObj != null)
                {
                    Projectile proj = bulletObj.GetComponent<Projectile>();
                    if (proj != null)
                    {
                        // กระสุนที่รีเพลย์จากเพื่อนเป็น Visual (ดาเมจที่ Client ยิงโดนศัตรู Puppet ส่งตรงผ่าน EnemyDamageRequest เพื่อไม่ให้คิดดาเมจเบิ้ล)
                        proj.SetStats(0f, payload.weaponRange, payload.canPierce, payload.weaponName, DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE, isFireballSkill ? "" : visualId);
                        if (isFireballSkill)
                        {
                            proj.PoolKey = "FlamethrowerFireball";
                            proj.SetSpeed(25f);
                        }
                        else if (payload.isRocketLauncher)
                        {
                            proj.ConfigureExplosiveOverride(13f, 2.5f, "Explosion");
                        }
                        proj.Setup(bulletDir, payload.aimTargetPoint, shooter != null ? shooter.gameObject : null, isFireballSkill ? "" : visualId);
                    }
                }
            }
        }

        public void NotifyLocalMeleeSwing(Vector3 attackOrigin, Vector3 aimDir, string weaponName, int comboStep, float damage, float radius)
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;

            int curStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int curInteriorId = BuildingInteriorManager.CurrentInteriorId;

            MeleeAttackNetPayload payload = new MeleeAttackNetPayload
            {
                attackerClientId = EmpireNetworkManager.Instance.LocalClientId,
                stageX = curStageX,
                stageY = curStageY,
                interiorId = curInteriorId,
                attackOrigin = attackOrigin,
                aimDirection = aimDir,
                weaponName = weaponName ?? "Knife",
                comboStep = comboStep,
                damage = damage,
                radius = radius
            };

            PerformMeleeServerRpc(payload);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PerformMeleeServerRpc(MeleeAttackNetPayload payload)
        {
            if (EmpireNetworkManager.Instance == null) return;
            EmpireNetworkManager.Instance.SendPacket(NetPacketType.MeleeAttack, w =>
            {
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                payload.NetworkSerialize(serializer);
            });
        }

        public static void HandleIncomingMeleeAttack(MeleeAttackNetPayload payload, bool isHost)
        {
            if (EmpireNetworkManager.Instance == null) return;

            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int myInteriorId = BuildingInteriorManager.CurrentInteriorId;
            bool isSameStage = payload.stageX == myStageX && payload.stageY == myStageY && payload.interiorId == myInteriorId;

            if (isHost && payload.attackerClientId != EmpireNetworkManager.Instance.LocalClientId)
            {
                // หาก Client ใช้สกิล Knife Hook (comboStep == 101) ให้ Host ดึงมอนตัวจริงให้ตรงกันทุกเครื่อง (ยกเว้นบอส)
                if (payload.comboStep == 101 && isSameStage)
                {
                    PlayerController.ExecuteHostAuthoritativeKnifeHookPull(payload.attackOrigin, payload.aimDirection, payload.radius);
                }

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.MeleeAttack, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    payload.NetworkSerialize(serializer);
                });
            }

            if (payload.attackerClientId == EmpireNetworkManager.Instance.LocalClientId || !isSameStage) return;

            PlayerController attacker = PlayerRegistry.GetPlayerByClientId(payload.attackerClientId);
            if (attacker != null)
            {
                Animator anim = attacker.GetComponentInChildren<Animator>();
                if (payload.comboStep == 101)
                {
                    if (anim != null) anim.Play("Slash3", 0, 0f);
                    attacker.SpawnRemoteVisualKnifeHook(payload.attackOrigin, payload.aimDirection, payload.radius);
                }
                else if (payload.comboStep == 102)
                {
                    if (anim != null) anim.Play("Slash2", 0, 0f);
                    attacker.SpawnRemoteVisualBatBoomerang(payload.attackOrigin, payload.aimDirection, payload.radius);
                }
                else
                {
                    int step = Mathf.Clamp(payload.comboStep, 1, 3);
                    if (anim != null) anim.Play("Slash" + step, 0, 0f);
                    if (ObjectPoolManager.Instance != null)
                    {
                        Vector3 dir = payload.aimDirection.sqrMagnitude > 0.001f ? payload.aimDirection.normalized : attacker.transform.forward;
                        GameObject vfx = ObjectPoolManager.Instance.SpawnFromPool("SwordSlash", payload.attackOrigin, Quaternion.LookRotation(dir));
                        if (vfx != null)
                        {
                            PooledParticle pp = vfx.GetComponent<PooledParticle>();
                            float sc = Mathf.Max(0.8f, payload.radius);
                            if (pp != null) { pp.SetPoolKey("SwordSlash"); pp.ApplyScaleMultiplier(sc); }
                            else vfx.transform.localScale = Vector3.one * sc;
                        }
                    }
                }
            }
        }

        // =====================================================
        //  Multiplayer Friendly Fire (20% Damage Sync)
        // =====================================================

        /// <summary>
        /// Checks if `targetObj` is a teammate PlayerController (distinct from `attackerObj`).
        /// Returns true if `targetObj` IS a player (handled here — caller should NOT also apply 100% TakeDamage).
        /// Applies 20% damage synced to the teammate's owner machine (or directly to Simulated Co-op Bots).
        /// </summary>
        public static bool TryApplyFriendlyFireDamage(
            GameObject attackerObj,
            GameObject targetObj,
            float fullDamage,
            Vector3 hitPoint,
            bool isCritical)
        {
            if (targetObj == null) return false;

            PlayerController targetPlayer = targetObj.GetComponent<PlayerController>() ?? targetObj.GetComponentInParent<PlayerController>();
            if (targetPlayer == null) return false;

            PlayerController attackerPlayer = attackerObj != null
                ? (attackerObj.GetComponent<PlayerController>() ?? attackerObj.GetComponentInParent<PlayerController>())
                : null;
            if (attackerPlayer == null) return false; // Enemy hitting player -> let normal 100% damage run

            // Never self-damage
            if (attackerPlayer == targetPlayer) return true;

            // Only apply in Multiplayer when Friendly Fire is enabled & attacker is our local player (not remote visual replay!)
            if (!FriendlyFireEnabled || !EmpireNetworkManager.IsMultiplayer) return true;
            if (attackerPlayer.IsRemotePlayer || fullDamage <= 0.001f) return true;

            // Do not damage already downed or dead teammates
            if (targetPlayer.IsDowned || (targetPlayer.PlayerHealth != null && targetPlayer.PlayerHealth.IsDead)) return true;

            float ffDamage = Mathf.Max(1f, fullDamage * FriendlyFireDamageMultiplier);

            if (targetPlayer.IsSimulatedCoopBot)
            {
                if (targetPlayer.PlayerHealth != null)
                {
                    targetPlayer.PlayerHealth.TakeDamage(ffDamage, hitPoint, isCritical);
                }
                return true;
            }

            if (targetPlayer.IsRemotePlayer && EmpireNetworkManager.IsOnline && EmpireNetworkManager.Instance != null)
            {
                // Immediate visual feedback on shooter's screen
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.SpawnDamageNumber(hitPoint + Vector3.up * 0.7f, ffDamage, isCritical);
                }

                int stageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
                int stageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
                int interiorId = BuildingInteriorManager.CurrentInteriorId;
                ulong targetClientId = targetPlayer.NetworkClientId;
                ulong attackerClientId = EmpireNetworkManager.Instance.LocalClientId;

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.FriendlyFireDamage, w =>
                {
                    w.Write(targetClientId);
                    w.Write(attackerClientId);
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(interiorId);
                    w.Write(ffDamage);
                    w.Write(hitPoint.x);
                    w.Write(hitPoint.y);
                    w.Write(hitPoint.z);
                    w.Write(isCritical);
                });
            }

            return true;
        }

        public static void HandleIncomingFriendlyFireDamage(
            ulong targetClientId,
            ulong attackerClientId,
            int stageX,
            int stageY,
            int interiorId,
            float damage,
            Vector3 hitPoint,
            bool isCritical,
            bool relayIfHost)
        {
            if (EmpireNetworkManager.Instance == null || damage <= 0.001f) return;

            // If Host receives a Client-to-Client friendly fire hit, relay it directly to the target client
            if (relayIfHost && EmpireNetworkManager.IsOnlineHost && targetClientId != EmpireNetworkManager.Instance.LocalClientId)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.FriendlyFireDamage, w =>
                {
                    w.Write(targetClientId);
                    w.Write(attackerClientId);
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(interiorId);
                    w.Write(damage);
                    w.Write(hitPoint.x);
                    w.Write(hitPoint.y);
                    w.Write(hitPoint.z);
                    w.Write(isCritical);
                }, targetClientId);
                return;
            }

            if (targetClientId != EmpireNetworkManager.Instance.LocalClientId) return;

            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int myInteriorId = BuildingInteriorManager.CurrentInteriorId;
            if (stageX != myStageX || stageY != myStageY || interiorId != myInteriorId) return;

            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local == null || local.IsRemotePlayer || local.IsDowned) return;
            if (local.PlayerHealth == null || local.PlayerHealth.IsDead) return;

            local.PlayerHealth.TakeDamage(damage, hitPoint, isCritical);
        }
    }
}
