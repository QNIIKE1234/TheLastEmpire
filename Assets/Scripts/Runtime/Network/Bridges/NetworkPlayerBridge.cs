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
            _player.NetworkStageX = curStageX;
            _player.NetworkStageY = curStageY;

            PlayerStateNetPayload payload = new PlayerStateNetPayload
            {
                clientId = EmpireNetworkManager.Instance.LocalClientId,
                slotIndex = _player.PartySlotIndex,
                playerName = _player.NetworkPlayerName ?? "SURVIVOR",
                characterId = _player.ActiveCharacterData != null ? _player.ActiveCharacterData.characterId : "VANGUARD",
                stageX = curStageX,
                stageY = curStageY,
                position = _player.transform.position,
                velocity = _rb != null ? _rb.linearVelocity : new Vector3(_player.MoveInput.x, 0f, _player.MoveInput.y),
                aimDirection = _player.AimDirection,
                currentHealth = h != null ? h.CurrentHealth : 100f,
                maxHealth = h != null ? h.MaxHealth : 100f,
                heldItemType = (byte)_player.CurrentHeldItemType,
                activeWeaponName = activeWep ?? "",
                isSprinting = _player.IsSprinting,
                isSitting = _player.IsSitting,
                isDashing = false,
                isShiftActive = _player.ActiveShiftAbility != null && _player.ActiveShiftAbility.IsActive,
                shiftType = (byte)(_player.ActiveCharacterData != null ? (int)_player.ActiveCharacterData.shiftAbilityType : 0),
                isDowned = _player.IsDowned,
                downedRemainingSeconds = _player.DownedBleedoutTimer,
                reviveProgress01 = _player.ReviveProgress01
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

        public void NotifyLocalWeaponFired(Vector3 spawnPos, Vector3 aimDir, Vector3 targetPoint, string weaponName, float damage, float range, bool isRocket)
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;

            int curStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;

            ProjectileFireNetPayload payload = new ProjectileFireNetPayload
            {
                shooterClientId = EmpireNetworkManager.Instance.LocalClientId,
                stageX = curStageX,
                stageY = curStageY,
                spawnPosition = spawnPos,
                aimDirection = aimDir,
                aimTargetPoint = targetPoint,
                weaponName = weaponName ?? "Pistol",
                pelletSeed = UnityEngine.Random.Range(1, 999999),
                damage = damage,
                weaponRange = range,
                isRocketLauncher = isRocket
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

            // หากคนยิงอยู่คนละช่องพิกัด (Stage X, Y) กับเรา ไม่ต้องเสกกระสุนของเขาในช่องของเรา
            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            if (payload.stageX != myStageX || payload.stageY != myStageY)
            {
                return;
            }

            PlayerController shooter = PlayerRegistry.GetPlayerByClientId(payload.shooterClientId);
            GameObject bulletObj = null;
            if (ObjectPoolManager.Instance != null)
            {
                bulletObj = ObjectPoolManager.Instance.SpawnFromPool("Bullet", payload.spawnPosition, Quaternion.LookRotation(payload.aimDirection.sqrMagnitude > 0.01f ? payload.aimDirection : Vector3.forward));
            }
            if (bulletObj == null)
            {
                GameObject prefab = AddressableManager.LoadAsset<GameObject>("Prefabs/Item/BulletPrefabs");
                if (prefab != null)
                {
                    bulletObj = Instantiate(prefab, payload.spawnPosition, Quaternion.LookRotation(payload.aimDirection.sqrMagnitude > 0.01f ? payload.aimDirection : Vector3.forward));
                }
            }

            if (bulletObj != null)
            {
                Projectile proj = bulletObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    float authoritativeDmg = isHost ? payload.damage : 0f; // ดาเมจจริงตัดสินที่ Host
                    proj.SetStats(authoritativeDmg, payload.weaponRange, false, payload.weaponName, DamageCalculator.DEFAULT_MAX_FALLOFF_RANGE, "Bullet");
                    if (payload.isRocketLauncher)
                    {
                        proj.ConfigureExplosiveOverride(13f, 2.5f, "Explosion");
                    }
                    proj.Setup(payload.aimDirection, payload.aimTargetPoint, shooter != null ? shooter.gameObject : null, "Bullet");
                }
            }
        }

        public void NotifyLocalMeleeSwing(Vector3 attackOrigin, Vector3 aimDir, string weaponName, int comboStep, float damage, float radius)
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;

            int curStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;

            MeleeAttackNetPayload payload = new MeleeAttackNetPayload
            {
                attackerClientId = EmpireNetworkManager.Instance.LocalClientId,
                stageX = curStageX,
                stageY = curStageY,
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
            bool isSameStage = payload.stageX == myStageX && payload.stageY == myStageY;

            if (isHost && payload.attackerClientId != EmpireNetworkManager.Instance.LocalClientId)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.MeleeAttack, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    payload.NetworkSerialize(serializer);
                });

                // Host ประมวลผลดาเมจระยะประชิดให้ศัตรูในรัศมี เฉพาะเมื่ออยู่ในช่อง (Stage X, Y) เดียวกัน
                if (isSameStage)
                {
                    Collider[] hits = Physics.OverlapSphere(payload.attackOrigin, Mathf.Max(0.8f, payload.radius));
                    for (int i = 0; i < hits.Length; i++)
                    {
                        BaseEnemyAI enemy = hits[i].GetComponent<BaseEnemyAI>();
                        if (enemy != null)
                        {
                            IDamageable dmg = enemy.GetComponent<IDamageable>();
                            if (dmg != null && !dmg.IsDead)
                            {
                                dmg.TakeDamage(payload.damage, enemy.transform.position, false);
                            }
                        }
                    }
                }
            }

            if (payload.attackerClientId == EmpireNetworkManager.Instance.LocalClientId || !isSameStage) return;

            PlayerController attacker = PlayerRegistry.GetPlayerByClientId(payload.attackerClientId);
            if (attacker != null)
            {
                Animator anim = attacker.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.Play("Slash1", 0, 0f);
                }
            }
        }
    }
}
