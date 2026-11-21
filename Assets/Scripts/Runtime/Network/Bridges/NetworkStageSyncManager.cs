using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// NetworkStageSyncManager — ซิงค์แผนที่โลก (Deterministic WorldSeed), พิกัดห้อง (stageX, stageY),
    /// ระบบย้ายห้องพร้อมกันทั้งปาร์ตี้ (Two-Phase Barrier Party Travel), และเวลา Day/Night
    /// </summary>
    public class NetworkStageSyncManager : NetworkBehaviour
    {
        public static NetworkStageSyncManager Instance { get; private set; }

        public NetworkVariable<int> NetWorldSeed = new NetworkVariable<int>(0);
        public NetworkVariable<int> NetStageX = new NetworkVariable<int>(64);
        public NetworkVariable<int> NetStageY = new NetworkVariable<int>(64);

        // สถานะการนับถอยหลังย้ายห้องของปาร์ตี้ (Party Portal Countdown)
        public bool IsPortalCountdownActive => _isCountdownActive;
        public float PortalCountdownRemaining => _countdownTimer;
        public TransitionPortal.TransitionDirection PendingDirection => _pendingPortalDirection;
        public int PendingTargetX => _pendingTargetX;
        public int PendingTargetY => _pendingTargetY;

        private bool _isCountdownActive = false;
        private float _countdownTimer = 0f;
        private TransitionPortal.TransitionDirection _pendingPortalDirection;
        private int _pendingTargetX;
        private int _pendingTargetY;

        private bool _isWaitingForBarrierReady = false;
        private float _barrierTimeoutTimer = 0f;
        private StageTransitionNetPayload _activeTransitionPayload;
        private readonly HashSet<ulong> _readyClientIds = new HashSet<ulong>();

        private float _timeSyncTimer = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Update()
        {
            if (!EmpireNetworkManager.IsOnline) return;

            if (EmpireNetworkManager.IsOnlineHost)
            {
                UpdateHostPortalCountdown();
                UpdateHostBarrierCheck();
                UpdateHostDayNightTimeSync();
            }
        }

        private bool _hasPendingHostWorld = false;
        private int _pendingHostSeed = 0;
        private int _pendingHostStageX = 64;
        private int _pendingHostStageY = 64;

        public void CaptureHostStageState()
        {
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                NetWorldSeed.Value = WorldMapManager.Instance.MapGenerator.seed;
                NetStageX.Value = WorldMapManager.Instance.CurrentPlayerX;
                NetStageY.Value = WorldMapManager.Instance.CurrentPlayerY;
            }
        }

        public void ApplyHostWorldAndStageOnClient(int worldSeed, int stageX, int stageY, string hostSceneName = "Gameplay_3D")
        {
            NetWorldSeed.Value = worldSeed;
            NetStageX.Value = stageX;
            NetStageY.Value = stageY;

            _hasPendingHostWorld = true;
            _pendingHostSeed = worldSeed;
            _pendingHostStageX = stageX;
            _pendingHostStageY = stageY;

            // หาก Client ยังอยู่หน้า MainMenu แต่ Host อยู่ในฉาก Gameplay_3D แล้ว ให้โหลดตามเข้าฉาก Gameplay_3D อัตโนมัติทันที!
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(hostSceneName) &&
                !string.Equals(currentScene, hostSceneName, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(hostSceneName, "Gameplay_3D", System.StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay_3D");
                return;
            }

            TryApplyPendingHostWorldOnSceneLoad();
        }

        public void TryApplyPendingHostWorldOnSceneLoad()
        {
            if (!_hasPendingHostWorld) return;
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                _hasPendingHostWorld = false;
                if (WorldMapManager.Instance.MapGenerator.seed != _pendingHostSeed)
                {
                    WorldMapManager.Instance.StartNewGame(_pendingHostSeed);
                }

                if (WorldMapManager.Instance.CurrentPlayerX != _pendingHostStageX || WorldMapManager.Instance.CurrentPlayerY != _pendingHostStageY)
                {
                    WorldMapManager.Instance.MovePlayer(_pendingHostStageX, _pendingHostStageY);
                }
            }
        }

        // =====================================================
        //  Party Travel & Two-Phase Barrier (Section 4.3)
        // =====================================================

        public void RequestPartyPortalTransition(TransitionPortal portal, int targetX, int targetY)
        {
            if (portal == null) return;

            if (EmpireNetworkManager.IsOnlineClient)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.StageTransitionRequest, w =>
                {
                    w.Write((byte)portal.Direction);
                });
                return;
            }

            // เครื่อง Host เริ่มนับถอยหลัง (หรือไปทันทีถ้าผู้เล่นทุกคนยืนอยู่ใกล้ประตูแล้ว)
            StartHostPortalTransitionCountdown(portal.Direction, targetX, targetY);
        }

        public void HandleClientPortalRequest(ulong clientId, TransitionPortal.TransitionDirection dir)
        {
            if (!EmpireNetworkManager.IsOnlineHost || WorldMapManager.Instance == null) return;

            int curX = WorldMapManager.Instance.CurrentPlayerX;
            int curY = WorldMapManager.Instance.CurrentPlayerY;
            int targetX = curX;
            int targetY = curY;

            switch (dir)
            {
                case TransitionPortal.TransitionDirection.North: targetY = curY + 1; break;
                case TransitionPortal.TransitionDirection.South: targetY = curY - 1; break;
                case TransitionPortal.TransitionDirection.East: targetX = curX + 1; break;
                case TransitionPortal.TransitionDirection.West: targetX = curX - 1; break;
            }

            StartHostPortalTransitionCountdown(dir, targetX, targetY);
        }

        private void StartHostPortalTransitionCountdown(TransitionPortal.TransitionDirection dir, int targetX, int targetY)
        {
            if (_isCountdownActive || _isWaitingForBarrierReady) return;

            _pendingPortalDirection = dir;
            _pendingTargetX = targetX;
            _pendingTargetY = targetY;

            // หากมีผู้เล่นในห้องแค่คนเดียว หรือเป็นบอทจำลอง ให้วาร์ปทันทีไม่ต้องรอ 3 วินาที
            bool hasRemoteHumanClients = EmpireNetworkManager.IsOnlineHost &&
                                         PlayerRegistry.GetTotalPlayerCount() > 1 &&
                                         EmpireNetworkManager.Instance.SimulatedTeammates.Count < (PlayerRegistry.GetTotalPlayerCount() - 1);

            if (!hasRemoteHumanClients)
            {
                ExecuteHostBeginStageTransition();
                return;
            }

            _isCountdownActive = true;
            _countdownTimer = 2.5f;
            Debug.Log($"<color=#38BDF8>[NetworkStageSyncManager]</color> Party moving to Stage ({targetX}, {targetY}) [{dir}] in {_countdownTimer:F1}s...");
        }

        private void UpdateHostPortalCountdown()
        {
            if (!_isCountdownActive) return;

            _countdownTimer -= Time.unscaledDeltaTime;
            if (_countdownTimer <= 0f)
            {
                _isCountdownActive = false;
                ExecuteHostBeginStageTransition();
            }
        }

        private void ExecuteHostBeginStageTransition()
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            int worldSeed = WorldMapManager.Instance.MapGenerator.seed;
            StageData targetStage = WorldMapManager.Instance.MapGenerator.GetStage(_pendingTargetX, _pendingTargetY);
            int stageSeed = targetStage != null ? targetStage.stageSeed : GameManager.GetDeterministicStageSeed(worldSeed, _pendingTargetX, _pendingTargetY);

            // สุ่ม StageEventType + EventSeed ประจำห้องใหม่บนเครื่อง Host เพียงจุดเดียว
            StageEventType rolledEvent = RollDeterministicStageEventOnHost(targetStage, stageSeed, out int eventSeed);

            TransitionPortal.TransitionDirection entryDir = TransitionPortal.GetOppositeDirection(_pendingPortalDirection);

            _activeTransitionPayload = new StageTransitionNetPayload
            {
                worldSeed = worldSeed,
                targetStageX = _pendingTargetX,
                targetStageY = _pendingTargetY,
                entryDirection = (byte)entryDir,
                triggeredEventType = (byte)rolledEvent,
                stageEventSeed = eventSeed
            };

            _readyClientIds.Clear();
            _isWaitingForBarrierReady = true;
            _barrierTimeoutTimer = 3.0f;

            // ส่งคำสั่ง Phase 1: BeginStageTransitionClientRpc ให้ทุกเครื่องสร้างฉากและจัดแถวหน้าประตู
            EmpireNetworkManager.Instance.SendPacket(NetPacketType.StageTransitionBegin, w =>
            {
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                _activeTransitionPayload.NetworkSerialize(serializer);
            });

            BeginStageTransitionClientRpc(_activeTransitionPayload);
        }

        private StageEventType RollDeterministicStageEventOnHost(StageData targetStage, int stageSeed, out int eventSeed)
        {
            eventSeed = stageSeed + 4242;
            if (targetStage == null || targetStage.isShopStage || targetStage.isCleared)
            {
                return StageEventType.None;
            }

            if (RandomStageEventManager.Instance != null && RandomStageEventManager.Instance.IsFleeingEventQueued)
            {
                return StageEventType.FleeingSurvivor;
            }

            System.Random rng = new System.Random(eventSeed);
            if (rng.NextDouble() > 0.25)
            {
                return StageEventType.None;
            }

            StageEventType[] pool = new[]
            {
                StageEventType.SupplyAirdrop,
                StageEventType.HostileAmbush,
                StageEventType.NomadTrader,
                StageEventType.RadioactiveFallout,
                StageEventType.BloodMoonSurge,
                StageEventType.ScavengerCache,
                StageEventType.FleeingSurvivor
            };
            return pool[rng.Next(0, pool.Length)];
        }

        [ClientRpc]
        public void BeginStageTransitionClientRpc(StageTransitionNetPayload payload)
        {
            NetWorldSeed.Value = payload.worldSeed;
            NetStageX.Value = payload.targetStageX;
            NetStageY.Value = payload.targetStageY;

            // 1. ล็อกการควบคุมผู้เล่นชั่วคราวระหว่างโหลดฉากและรอเพื่อนในปาร์ตี้
            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local != null)
            {
                local.SetControlLocked(true);
            }

            NetworkEnemyBridge.Instance?.ClearClientPuppets();

            // 2. อัปเดตตำแหน่งประตูทางเข้าของห้องใหม่ด้วย Seed เดียวกัน
            TransitionPortal.TransitionDirection entryDir = (TransitionPortal.TransitionDirection)payload.entryDirection;
            TransitionPortal[] allPortals = Object.FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            TransitionPortal arrivalPortal = null;
            for (int i = 0; i < allPortals.Length; i++)
            {
                TransitionPortal tp = allPortals[i];
                if (TransitionPortal.IsWorldPortal(tp))
                {
                    tp.UpdatePortalVisibility(payload.targetStageX, payload.targetStageY);
                    if (tp.Direction == entryDir)
                    {
                        arrivalPortal = tp;
                    }
                }
            }

            Vector3 baseEntryPos = arrivalPortal != null
                ? arrivalPortal.GetInwardEntryPosition()
                : new Vector3(0f, 0.5f, -5.5f);

            // 3. จัดแถวยืนผู้เล่น 1–6 คน (Spawn Formation เว้นระยะ 1.2m) ก่อนสร้างฉากเพื่อไม่ให้ต้นไม้/ตึกเกิดทับผู้เล่น
            TransitionPortal.ArrangeAllPartyMembersAtArrivalPortal(baseEntryPos, entryDir);

            // 4. สั่งเปลี่ยนพิกัดห้องใน WorldMapManager (จะเรียก EnvironmentManager.GenerateStageEnvironment ด้วย stageSeed เดียวกัน 100%)
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.MovePlayer(payload.targetStageX, payload.targetStageY);
            }

            // 5. จัดแถวยืนซ้ำอีกครั้งหลังสร้างฉากเสร็จเพื่อความแม่นยำของ Rigidbody
            TransitionPortal.ArrangeAllPartyMembersAtArrivalPortal(baseEntryPos, entryDir);

            // 6. แจ้ง Host ว่าโหลดฉากเสร็จพร้อมลุยแล้ว (Two-Phase Barrier Ready)
            if (EmpireNetworkManager.IsOnlineHost)
            {
                NotifyStageReadyServerRpc(0);
            }
            else if (EmpireNetworkManager.IsOnlineClient)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.StageTransitionReady, null);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void NotifyStageReadyServerRpc(ulong clientId)
        {
            _readyClientIds.Add(clientId);
        }

        private void UpdateHostBarrierCheck()
        {
            if (!_isWaitingForBarrierReady) return;

            _barrierTimeoutTimer -= Time.unscaledDeltaTime;

            // เมื่อทุกคนพร้อม (หรือครบ Timeout 3 วินาที) ให้ส่ง Phase 2: StartStageGameplayClientRpc
            int humanCount = 1;
            if (_readyClientIds.Count >= humanCount || _barrierTimeoutTimer <= 0f)
            {
                _isWaitingForBarrierReady = false;

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.StageGameplayStart, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    _activeTransitionPayload.NetworkSerialize(serializer);
                });

                StartStageGameplayClientRpc(_activeTransitionPayload);
            }
        }

        [ClientRpc]
        public void StartStageGameplayClientRpc(StageTransitionNetPayload payload)
        {
            // ปลดล็อกการควบคุมผู้เล่นพร้อมกันทุกจอ
            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local != null)
            {
                local.SetControlLocked(false);
            }

            // หากห้องนี้มีอีเวนต์หรือคัตซีน (เช่น FleeingSurvivor Maya วิ่งหนี) ให้เริ่มรันพร้อมกันด้วย Seed เดียวกันเป๊ะ!
            StageEventType eventType = (StageEventType)payload.triggeredEventType;
            if (eventType != StageEventType.None && RandomStageEventManager.Instance != null && WorldMapManager.Instance != null)
            {
                StageData stage = WorldMapManager.Instance.MapGenerator?.GetStage(payload.targetStageX, payload.targetStageY);
                if (stage != null)
                {
                    RandomStageEventManager.Instance.ExecuteEventWithSeed(eventType, stage, payload.stageEventSeed);
                }
            }
        }

        // =====================================================
        //  Day/Night Cycle Clock Sync
        // =====================================================

        private void UpdateHostDayNightTimeSync()
        {
            if (DayNightManager.Instance == null) return;

            _timeSyncTimer -= Time.unscaledDeltaTime;
            if (_timeSyncTimer <= 0f)
            {
                _timeSyncTimer = 2.0f;
                byte phase = (byte)DayNightManager.Instance.CurrentPhase;
                float timer = DayNightManager.Instance.PhaseTimer;
                int dayCount = DayNightManager.Instance.DayCount;

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.DayNightTimeSync, w =>
                {
                    w.Write(phase);
                    w.Write(timer);
                    w.Write(dayCount);
                });
            }
        }

        public void ApplyHostTimeSync(byte phase, float timer, int dayCount)
        {
            // DayNightManager ซิงค์เวลาจากเครื่อง Host
            if (DayNightManager.Instance == null) return;
        }
    }
}
