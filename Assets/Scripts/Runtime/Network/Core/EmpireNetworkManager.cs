using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastEmpire.Network
{
    public enum NetPacketType : byte
    {
        HandshakeRequest = 1,
        HandshakeAccept = 2,
        DisconnectNotice = 3,
        PlayerStateSync = 10,
        ProjectileFire = 11,
        MeleeAttack = 12,
        DashAction = 13,
        ReviveAction = 14,
        StageTransitionRequest = 20,
        StageTransitionBegin = 21,
        StageTransitionReady = 22,
        StageGameplayStart = 23,
        DayNightTimeSync = 24,
        EnemyBatchSync = 30,
        EnemyChaserPhasing = 31,
        DamageNumberBroadcast = 32,
        LootSpawnBroadcast = 40,
        LootClaimRequest = 41,
        LootClaimResult = 42,
        BuildingPlaceRequest = 50,
        BuildingPlaceBroadcast = 51,
        BuildingDestroyBroadcast = 52,
        CrateTransactionRequest = 53,
        CrateTransactionBroadcast = 54
    }

    /// <summary>
    /// EmpireNetworkManager — ผู้จัดการระบบ Co-op Multiplayer 1–6 คน (Zero Scene Diff Bootstrap)
    /// - เสกตัวเองและระบบ Network Bridges ทั้งหมดอัตโนมัติผ่าน [RuntimeInitializeOnLoadMethod]
    /// - รองรับทั้งโหมด Host (Listen Server), Client (Join ผ่านรหัส 6 หลัก หรือ IP:Port),
    ///   และโหมดเสกเพื่อนร่วมทีมจำลอง (Co-op Bot / Simulated Teammates 1–5 คน) สำหรับทดสอบในเครื่องเดียวทันที
    /// - รักษาโหมด Singleplayer Offline ให้ทำงานเหมือนเดิม 100% เมื่อไม่ได้เปิดห้อง
    /// </summary>
    public class EmpireNetworkManager : MonoBehaviour
    {
        public const int MaxPlayers = 6;

        public static EmpireNetworkManager Instance { get; private set; }

        public static bool IsOnline => Instance != null && Instance._isSessionActive;
        public static bool IsMultiplayer => IsOnline || PlayerRegistry.GetTotalPlayerCount() > 1;
        public static bool IsOnlineHost => IsOnline && Instance._isHost;
        public static bool IsOnlineClient => IsOnline && !Instance._isHost;
        public static bool IsHostOrOffline => !IsOnline || Instance._isHost;

        public bool IsSessionActive => _isSessionActive;
        public bool IsHostMode => _isHost;
        public ulong LocalClientId => _localClientId;
        public string ActiveJoinCode => _activeJoinCode;
        public string ActiveRoomName => _activeRoomName;
        public string ActiveHostIp => _activeHostIp;
        public int ActivePort => _activePort;

        private bool _isSessionActive = false;
        private bool _isHost = true;
        private bool _isClientHandshakeAccepted = false;
        private float _handshakeRetryTimer = 0f;
        private ulong _localClientId = 0;
        private string _activeJoinCode = "";
        private string _activeRoomName = "THE LAST EMPIRE";
        private string _activeHostIp = "127.0.0.1";
        private int _activePort = LobbyRelayService.DefaultUdpPort;

        private UdpClient _udpSocket;
        private UdpClient _lanDiscoverySocket;
        private float _lanBeaconTimer = 0f;
        private IPEndPoint _serverEndPoint;
        private readonly Dictionary<ulong, IPEndPoint> _clientEndpoints = new Dictionary<ulong, IPEndPoint>();
        private readonly Dictionary<string, ulong> _endpointToClientId = new Dictionary<string, ulong>();
        private ulong _nextAssignedClientId = 1;

        public int ConnectedPeerCount => _isSessionActive ? (_isHost ? 1 + _clientEndpoints.Count + _simulatedTeammates.Count : Mathf.Max(2, PlayerRegistry.GetTotalPlayerCount())) : 1;

        private readonly List<PlayerController> _simulatedTeammates = new List<PlayerController>();
        public IReadOnlyList<PlayerController> SimulatedTeammates => _simulatedTeammates;

        public event Action OnSessionStarted;
        public event Action OnSessionEnded;
        public event Action<ulong> OnPeerConnected;
        public event Action<ulong> OnPeerDisconnected;

        private static readonly string[] CoOpOperativeOrder = new[]
        {
            "VANGUARD",
            "JUGGERNAUT",
            "PHANTOM",
            "MARKSMAN",
            "SCAVENGER",
            "REBEL",
            "DEMOLITIONIST"
        };

        private static readonly string[] CoOpCallsigns = new[]
        {
            "HOST-01",
            "STRIKER-02",
            "GHOST-03",
            "HAWKEYE-04",
            "NOMAD-05",
            "BLAZE-06"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;

            GameObject root = new GameObject("[EmpireNetworkBootstrap]");
            DontDestroyOnLoad(root);

            if (root.GetComponent<NetworkManager>() == null)
            {
                root.AddComponent<NetworkManager>();
            }

            Instance = root.AddComponent<EmpireNetworkManager>();
            root.AddComponent<NetworkStageSyncManager>();
            root.AddComponent<NetworkEnemyBridge>();
            root.AddComponent<NetworkLootManager>();
            root.AddComponent<NetworkBuildingSyncManager>();
            root.AddComponent<MultiplayerLobbyUI>();
            root.AddComponent<PartyStatusHUD>();
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                // สำคัญมากสำหรับการเปิด 2 หน้าต่าง (Host + Client) บนคอมเครื่องเดียวกัน:
                // ป้องกันไม่ให้หน้าต่างที่ไม่ได้คลิกโฟกัสหยุดรันเกมหรือหยุดส่งแพ็กเก็ต UDP
                Application.runInBackground = true;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            InitLanDiscoverySocket();
            CheckCommandLineAutoConnect();
        }

        private void InitLanDiscoverySocket()
        {
            if (_lanDiscoverySocket != null) return;
            try
            {
                _lanDiscoverySocket = new UdpClient();
                _lanDiscoverySocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _lanDiscoverySocket.ExclusiveAddressUse = false;
                _lanDiscoverySocket.EnableBroadcast = true;
                _lanDiscoverySocket.Client.Bind(new IPEndPoint(IPAddress.Any, LobbyRelayService.LanDiscoveryPort));
                _lanDiscoverySocket.Client.Blocking = false;
            }
            catch
            {
                // Fallback if port 47777 is restricted
                try
                {
                    _lanDiscoverySocket = new UdpClient(0);
                    _lanDiscoverySocket.EnableBroadcast = true;
                    _lanDiscoverySocket.Client.Blocking = false;
                }
                catch { }
            }
        }

        private void UpdateLanDiscovery()
        {
            if (_lanDiscoverySocket == null) InitLanDiscoverySocket();
            if (_lanDiscoverySocket == null) return;

            // 1. หากเป็น Host ให้กระจายสัญญาณ LAN Beacon ทุก 0.8 วินาที เพื่อให้เครื่องอื่นในวง LAN เห็นห้องอัตโนมัติ
            if (_isSessionActive && _isHost)
            {
                _lanBeaconTimer -= Time.unscaledDeltaTime;
                if (_lanBeaconTimer <= 0f)
                {
                    _lanBeaconTimer = 0.8f;
                    byte[] beacon = LobbyRelayService.BuildLanBeaconPacket(
                        _activeJoinCode,
                        _activeHostIp,
                        _activePort,
                        ConnectedPeerCount,
                        MaxPlayers,
                        SceneManager.GetActiveScene().name,
                        _activeRoomName);

                    try
                    {
                        _lanDiscoverySocket.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Broadcast, LobbyRelayService.LanDiscoveryPort));
                        _lanDiscoverySocket.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Loopback, LobbyRelayService.LanDiscoveryPort));
                    }
                    catch { }
                }
            }

            // 2. รับสัญญาณ LAN Beacon จากเครื่อง Host ในวง LAN
            int budget = 16;
            while (budget-- > 0 && _lanDiscoverySocket != null && _lanDiscoverySocket.Available > 0)
            {
                try
                {
                    IPEndPoint senderEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _lanDiscoverySocket.Receive(ref senderEp);
                    LobbyRelayService.TryParseLanBeaconPacket(data, senderEp, out _);
                }
                catch
                {
                    break;
                }
            }
        }

        private void CheckCommandLineAutoConnect()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "-coopLobby", StringComparison.OrdinalIgnoreCase))
                    {
                        if (SceneManager.GetActiveScene().name != "Lobby_NetCode")
                        {
                            SceneManager.LoadScene("Lobby_NetCode");
                        }
                        break;
                    }
                    else if (string.Equals(args[i], "-coopClient", StringComparison.OrdinalIgnoreCase))
                    {
                        string target = (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            ? args[i + 1]
                            : "127.0.0.1:7777";
                        StartClientSession(target);
                        break;
                    }
                    else if (string.Equals(args[i], "-coopHost", StringComparison.OrdinalIgnoreCase))
                    {
                        StartHostSession();
                        break;
                    }
                }
            }
            catch { }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            CloseUdpSocket();
            if (_lanDiscoverySocket != null)
            {
                try { _lanDiscoverySocket.Close(); } catch { }
                _lanDiscoverySocket = null;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureLocalPlayerBridgeAttached();
            if (_isSessionActive && _isHost && NetworkStageSyncManager.Instance != null)
            {
                NetworkStageSyncManager.Instance.CaptureHostStageState();
            }
            if (_isSessionActive && !_isHost && NetworkStageSyncManager.Instance != null)
            {
                NetworkStageSyncManager.Instance.TryApplyPendingHostWorldOnSceneLoad();
            }
        }

        private void Update()
        {
            EnsureLocalPlayerBridgeAttached();
            UpdateLanDiscovery();
            PollUdpPackets();

            // หากเป็นเครื่อง Client ที่กำลังรอ HandshakeAccept จาก Host ให้ส่ง HandshakeRequest ซ้ำทุก 1 วินาที
            // เพื่อให้เปิด 2 หน้าต่างในลำดับไหนก็ได้ (จะกด Join ก่อนหรือกด Host ก่อนก็เชื่อมติดทันที)
            if (_isSessionActive && !_isHost && !_isClientHandshakeAccepted)
            {
                _handshakeRetryTimer -= Time.unscaledDeltaTime;
                if (_handshakeRetryTimer <= 0f)
                {
                    _handshakeRetryTimer = 1.0f;
                    SendHandshakeRequestToHost();
                }
            }
        }

        private void EnsureLocalPlayerBridgeAttached()
        {
            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local != null && local.GetComponent<NetworkPlayerBridge>() == null)
            {
                local.gameObject.AddComponent<NetworkPlayerBridge>();
            }
        }

        // =====================================================
        //  Host / Client / Disconnect Lifecycle
        // =====================================================

        public bool StartHostSession(int port = LobbyRelayService.DefaultUdpPort, string customRoomName = "")
        {
            DisconnectSession();

            // หากพอร์ตหลักถูกใช้งานอยู่ (เช่น เปิด 2 จอบนเครื่องเดียวกัน) ให้ขยับพอร์ตอัตโนมัติ
            int boundPort = port;
            bool bound = false;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    _udpSocket = new UdpClient(boundPort);
                    _udpSocket.Client.Blocking = false;
                    bound = true;
                    break;
                }
                catch
                {
                    boundPort++;
                }
            }

            if (!bound)
            {
                Debug.LogWarning("[EmpireNetworkManager] Could not bind UDP port; starting Host in local memory mode.");
            }

            _isSessionActive = true;
            _isHost = true;
            _localClientId = 0;
            _activePort = boundPort;
            _activeHostIp = LobbyRelayService.GetLocalIPv4();
            _activeJoinCode = LobbyRelayService.GenerateSixCharJoinCode(_activeHostIp, _activePort);
            _activeRoomName = string.IsNullOrWhiteSpace(customRoomName)
                ? $"EMPIRE SQUAD [{_activeJoinCode}]"
                : customRoomName.Trim();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartHost();
            }

            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local != null)
            {
                local.NetworkClientId = 0;
                local.PartySlotIndex = 0;
                local.NetworkPlayerName = "HOST (YOU)";
            }

            if (NetworkStageSyncManager.Instance != null)
            {
                NetworkStageSyncManager.Instance.CaptureHostStageState();
            }

            Debug.Log($"<color=#38BDF8>[EmpireNetworkManager]</color> Hosted 1–6P Co-op Room! Join Code: <b>{_activeJoinCode}</b> ({_activeHostIp}:{_activePort})");
            OnSessionStarted?.Invoke();
            return true;
        }

        public bool StartClientSession(string joinCodeOrAddress)
        {
            DisconnectSession();

            if (!LobbyRelayService.TryResolveJoinTarget(joinCodeOrAddress, out string targetIp, out int targetPort))
            {
                Debug.LogWarning($"[EmpireNetworkManager] Invalid Join Code or IP: '{joinCodeOrAddress}'");
                return false;
            }

            try
            {
                _udpSocket = new UdpClient(0);
                _udpSocket.Client.Blocking = false;
                _serverEndPoint = new IPEndPoint(IPAddress.Parse(targetIp), targetPort);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmpireNetworkManager] Failed to open Client UDP socket to {targetIp}:{targetPort} — {ex.Message}");
                return false;
            }

            Application.runInBackground = true;
            _isSessionActive = true;
            _isHost = false;
            _isClientHandshakeAccepted = false;
            _handshakeRetryTimer = 1.0f;
            _localClientId = (ulong)UnityEngine.Random.Range(100, 999);
            _activeHostIp = targetIp;
            _activePort = targetPort;
            _activeJoinCode = joinCodeOrAddress.Trim().ToUpperInvariant();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
            }

            SendHandshakeRequestToHost();

            Debug.Log($"<color=#38BDF8>[EmpireNetworkManager]</color> Connecting to Host at {targetIp}:{targetPort} (Code: {_activeJoinCode})...");
            OnSessionStarted?.Invoke();
            return true;
        }

        public void DisconnectSession()
        {
            ClearSimulatedTeammates();
            RemoveAllRemotePlayers();
            CloseUdpSocket();

            _clientEndpoints.Clear();
            _endpointToClientId.Clear();
            _nextAssignedClientId = 1;

            bool wasActive = _isSessionActive;
            _isSessionActive = false;
            _isHost = true;
            _localClientId = 0;
            _activeJoinCode = "";

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            if (wasActive)
            {
                OnPeerDisconnected?.Invoke(_localClientId);
                Debug.Log("<color=#F59E0B>[EmpireNetworkManager]</color> Disconnected Co-op session. Returned to Singleplayer Offline mode.");
                OnSessionEnded?.Invoke();
            }
        }

        private void CloseUdpSocket()
        {
            if (_udpSocket != null)
            {
                try { _udpSocket.Close(); } catch { }
                _udpSocket = null;
            }
        }

        /// <summary>
        /// ส่งข้อมูล WorldSeed และพิกัดด่านปัจจุบันของ Host ไปยังลูกทีมทุกคนที่เชื่อมต่ออยู่
        /// (ใช้เมื่อ Host กด NEW GAME เข้าด่าน Gameplay_3D เพื่อดึงลูกทีมที่รออยู่หน้า MainMenu ตามเข้าด่านมาพร้อมกันทันที)
        /// </summary>
        public void BroadcastHostWorldStateToAllClients()
        {
            if (!_isSessionActive || !_isHost) return;

            int worldSeed = (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
                ? WorldMapManager.Instance.MapGenerator.seed
                : 12345;
            int curX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int curY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            string activeSceneName = SceneManager.GetActiveScene().name;

            int slotCounter = 1;
            foreach (var kvp in _clientEndpoints)
            {
                ulong clientId = kvp.Key;
                int slotIdx = Mathf.Clamp(slotCounter++, 1, MaxPlayers - 1);
                SendPacket(NetPacketType.HandshakeAccept, w =>
                {
                    w.Write(clientId);
                    w.Write(slotIdx);
                    w.Write(worldSeed);
                    w.Write(curX);
                    w.Write(curY);
                    w.Write(activeSceneName ?? "Gameplay_3D");
                }, clientId);
            }
        }

        // =====================================================
        //  Remote Player & Simulated Co-op Teammate Spawning (1..6 Players)
        // =====================================================

        /// <summary>
        /// เสกตัวละครเพื่อนร่วมทีม Co-op (รองรับสูงสุดรวมเราเป็น 6 คน)
        /// เพื่อให้ผู้พัฒนาทดสอบระบบปาร์ตี้ 1–6 คน (Multi-Target Aggro, Party HUD, Portal Formation, Downed & Revive) ได้ทันทีในหน้าจอเดียว
        /// </summary>
        public PlayerController SpawnSimulatedCoopTeammate()
        {
            PlayerRegistry.CleanupNullEntries();
            if (PlayerRegistry.GetTotalPlayerCount() >= MaxPlayers)
            {
                Debug.LogWarning($"[EmpireNetworkManager] Party is already full ({MaxPlayers}/{MaxPlayers} players)!");
                return null;
            }

            if (!_isSessionActive)
            {
                StartHostSession();
            }

            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local == null)
            {
                Debug.LogWarning("[EmpireNetworkManager] Cannot spawn teammate: LocalPlayer is not in a gameplay scene yet.");
                return null;
            }

            int slotIndex = PlayerRegistry.GetTotalPlayerCount();
            ulong botClientId = 1000UL + (ulong)slotIndex;
            string charId = CoOpOperativeOrder[slotIndex % CoOpOperativeOrder.Length];
            string callsign = CoOpCallsigns[slotIndex % CoOpCallsigns.Length];

            PlayerController teammate = CreateRemoteOrBotPlayerAvatar(botClientId, slotIndex, callsign, charId, isSimulatedBot: true);
            if (teammate != null)
            {
                _simulatedTeammates.Add(teammate);
                OnPeerConnected?.Invoke(botClientId);
            }
            return teammate;
        }

        public void ClearSimulatedTeammates()
        {
            for (int i = _simulatedTeammates.Count - 1; i >= 0; i--)
            {
                if (_simulatedTeammates[i] != null)
                {
                    PlayerRegistry.Unregister(_simulatedTeammates[i]);
                    Destroy(_simulatedTeammates[i].gameObject);
                }
            }
            _simulatedTeammates.Clear();
        }

        public void RemoveAllRemotePlayers()
        {
            var all = new List<PlayerController>(PlayerRegistry.AllPlayers);
            for (int i = 0; i < all.Count; i++)
            {
                PlayerController p = all[i];
                if (p != null && p.IsRemotePlayer)
                {
                    PlayerRegistry.Unregister(p);
                    Destroy(p.gameObject);
                }
            }
        }

        public PlayerController GetOrCreateRemotePlayerAvatar(ulong clientId, int slotIndex, string playerName, string characterId)
        {
            PlayerController existing = PlayerRegistry.GetPlayerByClientId(clientId);
            if (existing != null)
            {
                return existing;
            }

            return CreateRemoteOrBotPlayerAvatar(clientId, slotIndex, playerName, characterId, isSimulatedBot: false);
        }

        private PlayerController CreateRemoteOrBotPlayerAvatar(ulong clientId, int slotIndex, string playerName, string characterId, bool isSimulatedBot)
        {
            PlayerController template = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (template == null) return null;

            float angle = slotIndex * 60f * Mathf.Deg2Rad;
            Vector3 spawnOffset = new Vector3(Mathf.Cos(angle) * 1.4f, 0f, Mathf.Sin(angle) * 1.4f);
            Vector3 spawnPos = template.transform.position + spawnOffset;
            spawnPos.y = Mathf.Max(template.transform.position.y, 0.5f);

            // ตั้งแฟล็กชั่วคราวก่อน Instantiate เพื่อให้ Awake() ของตัวโคลนรู้ว่าเป็น Remote Player ทันที (ไม่แย่ง PlayerController.Instance)
            PlayerController.NextInstantiatedIsRemote = true;
            GameObject cloneObj = Instantiate(template.gameObject, spawnPos, template.transform.rotation);
            PlayerController.NextInstantiatedIsRemote = false;

            cloneObj.name = $"CoopPlayer_Slot{slotIndex + 1}_{playerName}";
            PlayerController remotePc = cloneObj.GetComponent<PlayerController>();
            if (remotePc == null)
            {
                Destroy(cloneObj);
                return null;
            }

            remotePc.ConfigureAsRemotePlayer(clientId, slotIndex, playerName, characterId, isSimulatedBot);
            if (cloneObj.GetComponent<NetworkPlayerBridge>() == null)
            {
                cloneObj.AddComponent<NetworkPlayerBridge>();
            }

            return remotePc;
        }

        // =====================================================
        //  Low-Latency UDP Packet Transport
        // =====================================================

        public void SendPacket(NetPacketType type, Action<BinaryWriter> writeAction, ulong targetClientId = ulong.MaxValue)
        {
            if (!_isSessionActive || _udpSocket == null) return;

            using (MemoryStream ms = new MemoryStream(256))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)0x54); // 'T'
                writer.Write((byte)0x4C); // 'L'
                writer.Write((byte)type);
                writer.Write(_localClientId);
                writeAction?.Invoke(writer);

                byte[] bytes = ms.ToArray();
                try
                {
                    if (_isHost)
                    {
                        if (targetClientId != ulong.MaxValue && _clientEndpoints.TryGetValue(targetClientId, out IPEndPoint singleEp))
                        {
                            _udpSocket.Send(bytes, bytes.Length, singleEp);
                        }
                        else
                        {
                            foreach (var kvp in _clientEndpoints)
                            {
                                _udpSocket.Send(bytes, bytes.Length, kvp.Value);
                            }
                        }
                    }
                    else if (_serverEndPoint != null)
                    {
                        _udpSocket.Send(bytes, bytes.Length, _serverEndPoint);
                    }
                }
                catch { }
            }
        }

        private void SendHandshakeRequestToHost()
        {
            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            string charId = local != null && local.ActiveCharacterData != null ? local.ActiveCharacterData.characterId : "VANGUARD";

            SendPacket(NetPacketType.HandshakeRequest, w =>
            {
                w.Write($"SURVIVOR-{UnityEngine.Random.Range(10, 99)}");
                w.Write(charId ?? "VANGUARD");
            });
        }

        private void PollUdpPackets()
        {
            if (!_isSessionActive || _udpSocket == null) return;

            int safetyBudget = 64;
            while (safetyBudget-- > 0 && _udpSocket != null && _udpSocket.Available > 0)
            {
                try
                {
                    IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpSocket.Receive(ref remoteEp);
                    if (data == null || data.Length < 11) continue;
                    if (data[0] != 0x54 || data[1] != 0x4C) continue;

                    using (MemoryStream ms = new MemoryStream(data))
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        reader.ReadByte();
                        reader.ReadByte();
                        NetPacketType packetType = (NetPacketType)reader.ReadByte();
                        ulong senderId = reader.ReadUInt64();
                        HandleIncomingPacket(packetType, senderId, remoteEp, reader);
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        private void HandleIncomingPacket(NetPacketType type, ulong senderId, IPEndPoint remoteEp, BinaryReader reader)
        {
            if (_isHost && remoteEp != null)
            {
                string epKey = remoteEp.ToString();
                if (_endpointToClientId.TryGetValue(epKey, out ulong registeredId))
                {
                    senderId = registeredId;
                }
                else if (type == NetPacketType.HandshakeRequest)
                {
                    if (_clientEndpoints.Count + 1 >= MaxPlayers) return; // ห้องเต็ม 6 คนแล้ว
                    ulong assignedId = _nextAssignedClientId++;
                    _endpointToClientId[epKey] = assignedId;
                    _clientEndpoints[assignedId] = remoteEp;
                    senderId = assignedId;
                }
            }

            var brw = BinaryBufferReaderWriter.CreateReader(reader);
            var serializer = new BufferSerializer<BinaryBufferReaderWriter>(brw);

            switch (type)
            {
                case NetPacketType.HandshakeRequest:
                    if (_isHost)
                    {
                        string clientName = reader.ReadString();
                        string clientCharId = reader.ReadString();
                        int slotIdx = Mathf.Clamp(_clientEndpoints.Count, 1, MaxPlayers - 1);

                        GetOrCreateRemotePlayerAvatar(senderId, slotIdx, clientName, clientCharId);

                        int worldSeed = (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
                            ? WorldMapManager.Instance.MapGenerator.seed
                            : 12345;
                        int curX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
                        int curY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
                        string activeSceneName = SceneManager.GetActiveScene().name;

                        SendPacket(NetPacketType.HandshakeAccept, w =>
                        {
                            w.Write(senderId);
                            w.Write(slotIdx);
                            w.Write(worldSeed);
                            w.Write(curX);
                            w.Write(curY);
                            w.Write(activeSceneName ?? "Gameplay_3D");
                        }, senderId);

                        NetworkBuildingSyncManager.Instance?.SendAllPlacedBuildingsToClient(senderId);
                        OnPeerConnected?.Invoke(senderId);
                    }
                    break;

                case NetPacketType.HandshakeAccept:
                    if (!_isHost)
                    {
                        _isClientHandshakeAccepted = true;
                        ulong assignedClientId = reader.ReadUInt64();
                        int assignedSlot = reader.ReadInt32();
                        int hostSeed = reader.ReadInt32();
                        int hostStageX = reader.ReadInt32();
                        int hostStageY = reader.ReadInt32();
                        string hostSceneName = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadString() : "Gameplay_3D";

                        _localClientId = assignedClientId;
                        PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
                        if (local != null)
                        {
                            local.NetworkClientId = assignedClientId;
                            local.PartySlotIndex = assignedSlot;
                            local.NetworkPlayerName = $"PLAYER-{assignedSlot + 1} (YOU)";
                        }

                        NetworkStageSyncManager.Instance?.ApplyHostWorldAndStageOnClient(hostSeed, hostStageX, hostStageY, hostSceneName);
                        OnPeerConnected?.Invoke(assignedClientId);
                    }
                    break;

                case NetPacketType.PlayerStateSync:
                    {
                        PlayerStateNetPayload payload = default;
                        payload.NetworkSerialize(serializer);
                        if (_isHost)
                        {
                            payload.clientId = senderId;
                            // กระจายต่อให้ลูกทีมคนอื่นด้วย
                            SendPacket(NetPacketType.PlayerStateSync, w =>
                            {
                                var wSerializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                                payload.NetworkSerialize(wSerializer);
                            });
                        }
                        if (payload.clientId != _localClientId)
                        {
                            PlayerController remote = GetOrCreateRemotePlayerAvatar(payload.clientId, payload.slotIndex, payload.playerName, payload.characterId);
                            if (remote != null)
                            {
                                remote.ApplyNetworkState(payload);
                            }
                        }
                    }
                    break;

                case NetPacketType.ProjectileFire:
                    {
                        ProjectileFireNetPayload firePayload = default;
                        firePayload.NetworkSerialize(serializer);
                        NetworkPlayerBridge.HandleIncomingProjectileFire(firePayload, _isHost);
                    }
                    break;

                case NetPacketType.MeleeAttack:
                    {
                        MeleeAttackNetPayload meleePayload = default;
                        meleePayload.NetworkSerialize(serializer);
                        NetworkPlayerBridge.HandleIncomingMeleeAttack(meleePayload, _isHost);
                    }
                    break;

                case NetPacketType.StageTransitionRequest:
                    if (_isHost)
                    {
                        byte dirByte = reader.ReadByte();
                        NetworkStageSyncManager.Instance?.HandleClientPortalRequest(senderId, (TransitionPortal.TransitionDirection)dirByte);
                    }
                    break;

                case NetPacketType.StageTransitionBegin:
                    if (!_isHost)
                    {
                        StageTransitionNetPayload stPayload = default;
                        stPayload.NetworkSerialize(serializer);
                        NetworkStageSyncManager.Instance?.BeginStageTransitionClientRpc(stPayload);
                    }
                    break;

                case NetPacketType.StageTransitionReady:
                    if (_isHost)
                    {
                        NetworkStageSyncManager.Instance?.NotifyStageReadyServerRpc(senderId);
                    }
                    break;

                case NetPacketType.StageGameplayStart:
                    if (!_isHost)
                    {
                        StageTransitionNetPayload stPayload = default;
                        stPayload.NetworkSerialize(serializer);
                        NetworkStageSyncManager.Instance?.StartStageGameplayClientRpc(stPayload);
                    }
                    break;

                case NetPacketType.DayNightTimeSync:
                    if (!_isHost)
                    {
                        byte phase = reader.ReadByte();
                        float timer = reader.ReadSingle();
                        int dayCount = reader.ReadInt32();
                        NetworkStageSyncManager.Instance?.ApplyHostTimeSync(phase, timer, dayCount);
                    }
                    break;

                case NetPacketType.EnemyBatchSync:
                    if (!_isHost)
                    {
                        NetworkEnemyBridge.Instance?.ReceiveEnemyBatchFromHost(reader);
                    }
                    break;

                case NetPacketType.DamageNumberBroadcast:
                    if (!_isHost)
                    {
                        Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        float dmg = reader.ReadSingle();
                        bool isCrit = reader.ReadBoolean();
                        DamageNumberManager.Instance?.SpawnDamageNumber(pos, dmg, isCrit);
                    }
                    break;

                case NetPacketType.LootClaimRequest:
                    if (_isHost)
                    {
                        int dropId = reader.ReadInt32();
                        NetworkLootManager.Instance?.HandleClaimLootRequestOnServer(dropId, senderId);
                    }
                    break;

                case NetPacketType.LootClaimResult:
                    if (!_isHost)
                    {
                        int dropId = reader.ReadInt32();
                        ulong winnerClientId = reader.ReadUInt64();
                        string itemName = reader.ReadString();
                        int qty = reader.ReadInt32();
                        bool isMoney = reader.ReadBoolean();
                        int moneyAmt = reader.ReadInt32();
                        NetworkLootManager.Instance?.HandleLootClaimResultOnClient(dropId, winnerClientId, itemName, qty, isMoney, moneyAmt);
                    }
                    break;

                case NetPacketType.BuildingPlaceRequest:
                    if (_isHost)
                    {
                        PlacedBuildingNetData bData = default;
                        bData.NetworkSerialize(serializer);
                        NetworkBuildingSyncManager.Instance?.HandleClientPlaceBuildingRequest(bData);
                    }
                    break;

                case NetPacketType.BuildingPlaceBroadcast:
                    if (!_isHost)
                    {
                        PlacedBuildingNetData bData = default;
                        bData.NetworkSerialize(serializer);
                        NetworkBuildingSyncManager.Instance?.ApplyRemotePlacedBuilding(bData);
                    }
                    break;

                case NetPacketType.CrateTransactionRequest:
                    if (_isHost)
                    {
                        CrateTransactionNetPayload tx = default;
                        tx.NetworkSerialize(serializer);
                        NetworkBuildingSyncManager.Instance?.HandleCrateTransactionOnServer(tx);
                    }
                    break;

                case NetPacketType.CrateTransactionBroadcast:
                    if (!_isHost)
                    {
                        CrateTransactionNetPayload tx = default;
                        tx.NetworkSerialize(serializer);
                        NetworkBuildingSyncManager.Instance?.ApplyCrateTransactionOnClient(tx);
                    }
                    break;
            }
        }
    }
}
