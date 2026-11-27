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
        LobbyRosterSync = 4,
        HostStartGameplay = 5,
        LobbyCharacterChange = 6,
        PartyWipeBroadcast = 7,
        HostRetryMatch = 8,
        ClientHeartbeat = 9,
        PlayerStateSync = 10,
        ProjectileFire = 11,
        MeleeAttack = 12,
        DashAction = 13,
        ReviveAction = 14,
        FriendlyFireDamage = 15,
        StageTransitionRequest = 20,
        StageTransitionBegin = 21,
        StageTransitionReady = 22,
        StageGameplayStart = 23,
        DayNightTimeSync = 24,
        StageEventBroadcast = 25,
        EnemyBatchSync = 30,
        EnemyChaserPhasing = 31,
        DamageNumberBroadcast = 32,
        EnemyDamageRequest = 33,
        StageEnemyProgressSync = 34,
        LootSpawnBroadcast = 40,
        LootClaimRequest = 41,
        LootClaimResult = 42,
        ContainerLootSync = 43,
        ClientDropItemRequest = 44,
        BuildingPlaceRequest = 50,
        BuildingPlaceBroadcast = 51,
        BuildingDestroyBroadcast = 52,
        CrateTransactionRequest = 53,
        CrateTransactionBroadcast = 54,
        FurnitureStateSync = 55
    }

    [Serializable]
    public struct LobbyMemberNetInfo
    {
        public ulong clientId;
        public int slotIndex;
        public string playerName;
        public string characterId;
        public bool isHost;
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
        public const string PlayerNamePrefKey = "TLE_PlayerAccountName";

        public static EmpireNetworkManager Instance { get; private set; }
        public static int PendingMatchWorldSeed { get; set; } = 0;

        private static string _cachedLocalPlayerName;

        /// <summary>
        /// ชื่อผู้เล่นของเครื่องเรา (ตั้งค่าได้จากหน้า MainMenu มุมขวาบน และเซฟลง PlayerPrefs)
        /// หากยังไม่เคยตั้งชื่อ จะสุ่มรหัส 7 ตัวอักษร (เช่น 9CS51XE) ให้เป็นค่าเริ่มต้น
        /// </summary>
        public static string LocalPlayerName => GetOrInitLocalPlayerName();

        public static string GetOrInitLocalPlayerName(string fallbackFromScene = "")
        {
            if (!string.IsNullOrWhiteSpace(_cachedLocalPlayerName))
            {
                return _cachedLocalPlayerName;
            }

            string saved = PlayerPrefs.GetString(PlayerNamePrefKey, "").Trim();
            if (!string.IsNullOrWhiteSpace(saved))
            {
                _cachedLocalPlayerName = saved;
                return _cachedLocalPlayerName;
            }

            string cleanFallback = string.IsNullOrWhiteSpace(fallbackFromScene)
                ? ""
                : fallbackFromScene.Replace("\u200B", "").Trim();

            if (!string.IsNullOrWhiteSpace(cleanFallback))
            {
                _cachedLocalPlayerName = cleanFallback;
            }
            else
            {
                const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                char[] buf = new char[7];
                for (int i = 0; i < buf.Length; i++)
                {
                    buf[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
                }
                _cachedLocalPlayerName = new string(buf);
            }

            PlayerPrefs.SetString(PlayerNamePrefKey, _cachedLocalPlayerName);
            PlayerPrefs.Save();
            return _cachedLocalPlayerName;
        }

        public static void SetLocalPlayerName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            string cleaned = newName.Replace("\u200B", "").Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) return;

            _cachedLocalPlayerName = cleaned;
            PlayerPrefs.SetString(PlayerNamePrefKey, _cachedLocalPlayerName);
            PlayerPrefs.Save();

            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local != null && !local.IsRemotePlayer)
            {
                local.NetworkPlayerName = _cachedLocalPlayerName;
                local.RefreshOverheadNameLabel();
            }

            if (Instance != null && Instance._lobbyMembers.Count > 0)
            {
                for (int i = 0; i < Instance._lobbyMembers.Count; i++)
                {
                    if (Instance._lobbyMembers[i].clientId == Instance._localClientId)
                    {
                        var m = Instance._lobbyMembers[i];
                        m.playerName = _cachedLocalPlayerName;
                        Instance._lobbyMembers[i] = m;
                        break;
                    }
                }
                if (Instance._isSessionActive && Instance._isHost)
                {
                    Instance._activeRoomName = _cachedLocalPlayerName;
                    Instance.BroadcastLobbyRosterFromHost();
                }
                Instance.OnLobbyRosterChanged?.Invoke();
            }
        }

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
        private string _activeRoomName = "JUNK CYCLONE CRISIS";
        private string _activeHostIp = "127.0.0.1";
        private int _activePort = LobbyRelayService.DefaultUdpPort;
        private string _localLobbyCharacterId = ""; // ว่าง = ยังไม่ได้กดปุ่มประแจเลือกตัวละคร (แสดงรูป Default เข็มทิศ)

        private UdpClient _udpSocket;
        private UdpClient _lanDiscoverySocket;
        private float _lanBeaconTimer = 0f;
        private IPEndPoint _serverEndPoint;
        private readonly Dictionary<ulong, IPEndPoint> _clientEndpoints = new Dictionary<ulong, IPEndPoint>();
        private readonly Dictionary<string, ulong> _endpointToClientId = new Dictionary<string, ulong>();
        private ulong _nextAssignedClientId = 1;

        // Steam Datagram Relay (SDR) transport & peer maps (runs alongside LAN UDP seamlessly)
        private SteamP2PTransport _steamTransport;
        private readonly Dictionary<uint, ulong> _steamConnToClientId = new Dictionary<uint, ulong>();
        private readonly Dictionary<ulong, uint> _clientIdToSteamConn = new Dictionary<ulong, uint>();
        private readonly Dictionary<ulong, ulong> _clientIdToSteamId = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, float> _clientLastHeardRealtime = new Dictionary<ulong, float>();
        private float _clientHeartbeatTimer = 0f;
        private float _hostStalePeerCheckTimer = 0f;
        private const float ClientHeartbeatIntervalSeconds = 1.5f;
        private const float HostPeerTimeoutSeconds = 7.0f;
        private bool _isUsingSteamRelayClient = false;
        private float _steamDiscoveryTimer = 0f;
        private bool _hasTriggeredPartyWipe = false;
        private float _partyWipeMonitorDelay = 0f;
        private float _partyWipeCheckTimer = 0f;

        public bool HasTriggeredPartyWipe => _hasTriggeredPartyWipe;

        public bool IsSteamAvailable => _steamTransport != null && _steamTransport.IsSteamAvailable;
        public bool IsSteamRelayActive => _steamTransport != null && (_isHost ? _steamTransport.IsHostRelayActive : _isUsingSteamRelayClient);
        public bool HasActiveSteamLobby => _steamTransport != null && _steamTransport.HasActiveLobby;
        public string SteamPlayerName => _steamTransport != null ? _steamTransport.LocalSteamName : "";
        public int SteamEstimatedPingMs => _steamTransport != null ? _steamTransport.GetEstimatedPingMs() : 0;

        public int ConnectedPeerCount => _isSessionActive
            ? (_isHost ? 1 + _clientEndpoints.Count + _clientIdToSteamConn.Count + _simulatedTeammates.Count : Mathf.Max(2, PlayerRegistry.GetTotalPlayerCount()))
            : 1;

        private readonly List<PlayerController> _simulatedTeammates = new List<PlayerController>();
        public IReadOnlyList<PlayerController> SimulatedTeammates => _simulatedTeammates;

        private readonly List<LobbyMemberNetInfo> _lobbyMembers = new List<LobbyMemberNetInfo>();
        public IReadOnlyList<LobbyMemberNetInfo> LobbyMembers => _lobbyMembers;

        public event Action OnSessionStarted;
        public event Action OnSessionEnded;
        public event Action OnLobbyRosterChanged;
        public event Action OnKickedFromRoom;
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
            if (FindFirstObjectByType<MultiplayerLobbyUI>() == null)
            {
                root.AddComponent<MultiplayerLobbyUI>();
            }
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
            InitSteamRelayTransport();
            CheckCommandLineAutoConnect();
        }

        private void InitSteamRelayTransport()
        {
            if (_steamTransport != null) return;
            _steamTransport = new SteamP2PTransport(SteamP2PTransport.DefaultTestAppId);
            _steamTransport.OnPacketReceived += HandleIncomingSteamPacket;
            _steamTransport.OnClientConnectedToHost += HandleSteamClientConnectedToHost;
            _steamTransport.OnClientDisconnectedFromHost += HandleSteamClientDisconnectedFromHost;
            _steamTransport.OnSteamPeerDisconnected += HandleSteamPeerDisconnectedOnHost;
            _steamTransport.OnInviteJoinRequested += HandleSteamInviteJoinRequested;

            if (_steamTransport.TryInitializeSteam())
            {
                // Auto-use Steam display name if player has never set a custom name yet
                if (string.IsNullOrWhiteSpace(PlayerPrefs.GetString(PlayerNamePrefKey, "")) &&
                    !string.IsNullOrWhiteSpace(_steamTransport.LocalSteamName))
                {
                    SetLocalPlayerName(_steamTransport.LocalSteamName);
                }
            }
        }

        public void RequestSteamLobbyRefresh(bool forceImmediate = false)
        {
            if (!IsSteamAvailable) return;
            if (!forceImmediate && _steamDiscoveryTimer > 0f) return;
            _steamDiscoveryTimer = 3.5f;
            _ = _steamTransport.RefreshSteamLobbiesIntoDiscoveryAsync();
        }

        public bool OpenSteamInviteOverlay()
        {
            if (_steamTransport == null) return false;
            return _steamTransport.OpenSteamInviteOverlay();
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
            if (_steamTransport != null)
            {
                _steamTransport.OnPacketReceived -= HandleIncomingSteamPacket;
                _steamTransport.OnClientConnectedToHost -= HandleSteamClientConnectedToHost;
                _steamTransport.OnClientDisconnectedFromHost -= HandleSteamClientDisconnectedFromHost;
                _steamTransport.OnSteamPeerDisconnected -= HandleSteamPeerDisconnectedOnHost;
                _steamTransport.OnInviteJoinRequested -= HandleSteamInviteJoinRequested;
                _steamTransport.CloseSocketsAndLobby();
            }
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

        private void OnApplicationQuit()
        {
            if (_isSessionActive && !_isHost)
            {
                SendPacket(NetPacketType.DisconnectNotice, w => { w.Write("Client Quit"); });
            }
            _steamTransport?.ShutdownSteamOnQuit();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _hasTriggeredPartyWipe = false;
            _partyWipeMonitorDelay = 3.0f;
            EnsureLocalPlayerBridgeAttached();
            if (_isSessionActive && _isHost)
            {
                _steamTransport?.UpdateHostLobbyMetadata(_activeRoomName, _activeJoinCode, scene.name);
                if (NetworkStageSyncManager.Instance != null)
                {
                    NetworkStageSyncManager.Instance.CaptureHostStageState();
                }
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

            if (_steamTransport != null)
            {
                _steamTransport.Tick();
                if (_steamDiscoveryTimer > 0f)
                {
                    _steamDiscoveryTimer -= Time.unscaledDeltaTime;
                }
            }

            // หากเป็นเครื่อง Client ที่กำลังรอ HandshakeAccept จาก Host ให้ส่ง HandshakeRequest ซ้ำทุก 1 วินาที
            // เพื่อให้เปิด 2 หน้าต่างในลำดับไหนก็ได้ (จะกด Join ก่อนหรือกด Host ก่อนก็เชื่อมติดทันที)
            if (_isSessionActive && !_isHost && !_isClientHandshakeAccepted)
            {
                _handshakeRetryTimer -= Time.unscaledDeltaTime;
                if (_handshakeRetryTimer <= 0f)
                {
                    _handshakeRetryTimer = 1.0f;
                    if (!_isUsingSteamRelayClient || (_steamTransport != null && _steamTransport.IsClientRelayConnected))
                    {
                        SendHandshakeRequestToHost();
                    }
                }
            }

            // หากเป็นเครื่อง Client ที่เชื่อมติดแล้ว ให้ส่ง Heartbeat เบาๆ ทุก 1.5 วิ (กันหลุดค้างในห้อง Lobby ถ้าเน็ตตัด/Alt+F4)
            if (_isSessionActive && !_isHost && _isClientHandshakeAccepted)
            {
                _clientHeartbeatTimer -= Time.unscaledDeltaTime;
                if (_clientHeartbeatTimer <= 0f)
                {
                    _clientHeartbeatTimer = ClientHeartbeatIntervalSeconds;
                    SendPacket(NetPacketType.ClientHeartbeat, w => { w.Write((byte)1); });
                }
            }

            // Host คอยเคลียร์ลูกห้องที่เงียบหายเกิน 7 วินาที (เช่น เกมค้าง / ปิดโปรแกรม / เน็ตหลุด) ออกจาก Lobby และแมปอัตโนมัติ
            if (_isSessionActive && _isHost && _clientLastHeardRealtime.Count > 0)
            {
                _hostStalePeerCheckTimer -= Time.unscaledDeltaTime;
                if (_hostStalePeerCheckTimer <= 0f)
                {
                    _hostStalePeerCheckTimer = 1.5f;
                    float now = Time.realtimeSinceStartup;
                    List<ulong> staleIds = null;
                    foreach (var kvp in _clientLastHeardRealtime)
                    {
                        if (now - kvp.Value > HostPeerTimeoutSeconds)
                        {
                            if (staleIds == null) staleIds = new List<ulong>();
                            staleIds.Add(kvp.Key);
                        }
                    }
                    if (staleIds != null)
                    {
                        for (int i = 0; i < staleIds.Count; i++)
                        {
                            Debug.Log($"<color=#F59E0B>[EmpireNetworkManager]</color> Pruning timed-out peer clientId={staleIds[i]} from room.");
                            RemovePeerFromHost(staleIds[i], sendKickNotice: false);
                        }
                    }
                }
            }

            // Host คอยตรวจสอบ Party Wipe (ตาย/ล้มครบทั้งห้อง) ในฉาก Gameplay_3D เพื่อขึ้น Game Over พร้อมกันทุกเครื่อง
            if (_isSessionActive && _isHost && !_hasTriggeredPartyWipe && SceneManager.GetActiveScene().name == "Gameplay_3D")
            {
                if (_partyWipeMonitorDelay > 0f)
                {
                    _partyWipeMonitorDelay -= Time.unscaledDeltaTime;
                }
                else
                {
                    _partyWipeCheckTimer -= Time.unscaledDeltaTime;
                    if (_partyWipeCheckTimer <= 0f)
                    {
                        _partyWipeCheckTimer = 0.25f;
                        if (PlayerRegistry.GetTotalPlayerCount() > 0 && PlayerRegistry.AreAllPlayersDeadOrDowned())
                        {
                            NotifyPartyWipe();
                        }
                    }
                }
            }
        }

        private void EnsureLocalPlayerBridgeAttached()
        {
            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (local != null)
            {
                if (!local.IsRemotePlayer)
                {
                    local.NetworkPlayerName = GetOrInitLocalPlayerName();
                    local.RefreshOverheadNameLabel();
                }
                if (local.GetComponent<NetworkPlayerBridge>() == null)
                {
                    local.gameObject.AddComponent<NetworkPlayerBridge>();
                }
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
            string hostPlayerName = GetOrInitLocalPlayerName();
            _activeRoomName = string.IsNullOrWhiteSpace(customRoomName)
                ? hostPlayerName
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
                local.NetworkPlayerName = hostPlayerName;
            }

            if (NetworkStageSyncManager.Instance != null)
            {
                NetworkStageSyncManager.Instance.CaptureHostStageState();
            }

            _lobbyMembers.Clear();
            _localLobbyCharacterId = ""; // เริ่มต้นเข้าห้องยังไม่ได้เลือกตัวละคร ให้แสดงรูป Default ก่อน
            _lobbyMembers.Add(new LobbyMemberNetInfo
            {
                clientId = 0,
                slotIndex = 0,
                playerName = hostPlayerName,
                characterId = _localLobbyCharacterId,
                isHost = true
            });
            OnLobbyRosterChanged?.Invoke();

            // เปิด Steam Datagram Relay + Steam Lobby ควบคู่กับ LAN UDP อัตโนมัติ (ถ้าเครื่องเปิด Steam อยู่)
            // ทำให้เพื่อนข้ามอินเทอร์เน็ต Join ผ่าน Steam Relay ได้ทันทีโดยไม่ต้อง Forward Port
            if (_steamTransport == null) InitSteamRelayTransport();
            if (IsSteamAvailable)
            {
                _ = _steamTransport.StartHostRelayAsync(
                    _activeRoomName,
                    _activeJoinCode,
                    MaxPlayers,
                    SceneManager.GetActiveScene().name);
            }

            Debug.Log($"<color=#38BDF8>[EmpireNetworkManager]</color> Hosted 1–6P Co-op Room! Join Code: <b>{_activeJoinCode}</b> ({_activeHostIp}:{_activePort}) | SteamRelay={(IsSteamAvailable ? "ENABLED" : "OFFLINE")}");
            OnSessionStarted?.Invoke();
            return true;
        }

        public bool StartClientSession(string joinCodeOrAddress)
        {
            DisconnectSession();

            string trimmedInput = (joinCodeOrAddress ?? "").Trim();

            // 1. Explicit Steam Relay target ("steamlobby:<id>" or "steam:<hostSteamId>")
            if (trimmedInput.StartsWith("steamlobby:", StringComparison.OrdinalIgnoreCase) &&
                ulong.TryParse(trimmedInput.Substring("steamlobby:".Length).Trim(), out ulong explicitLobbyId))
            {
                return BeginSteamClientJoin(explicitLobbyId, 0, trimmedInput);
            }

            if (trimmedInput.StartsWith("steam:", StringComparison.OrdinalIgnoreCase) &&
                ulong.TryParse(trimmedInput.Substring("steam:".Length).Trim(), out ulong explicitHostSteamId))
            {
                return BeginSteamClientJoin(0, explicitHostSteamId, trimmedInput);
            }

            // 2. Match against discovered Steam Relay rooms by 6-char Join Code / Lobby ID
            if (LobbyRelayService.TryFindDiscoveredSteamRoom(trimmedInput, out DiscoveredLanRoom steamRoom))
            {
                return BeginSteamClientJoin(steamRoom.steamLobbyId, steamRoom.hostSteamId, steamRoom.joinCode);
            }

            // 3. Pure 6-char Join Code (not an IP:Port and not found on LAN) -> search Steam Relay worldwide!
            bool looksLikeIp = trimmedInput.Contains(".") || trimmedInput.Contains(":");
            bool foundOnLan = false;
            if (!looksLikeIp && !string.IsNullOrEmpty(trimmedInput))
            {
                var activeRooms = LobbyRelayService.GetActiveLanRooms();
                for (int i = 0; i < activeRooms.Count; i++)
                {
                    if (!activeRooms[i].isSteamRelay &&
                        string.Equals(activeRooms[i].joinCode, trimmedInput, StringComparison.OrdinalIgnoreCase))
                    {
                        foundOnLan = true;
                        break;
                    }
                }
            }

            if (!looksLikeIp && !foundOnLan && trimmedInput.Length == 6 && IsSteamAvailable)
            {
                return BeginSteamClientJoinByCode(trimmedInput.ToUpperInvariant());
            }

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
            _isUsingSteamRelayClient = false;
            _isClientHandshakeAccepted = false;
            _handshakeRetryTimer = 1.0f;
            _localLobbyCharacterId = ""; // เริ่มต้นเข้าห้องยังไม่ได้เลือกตัวละคร ให้แสดงรูป Default ก่อน
            _localClientId = (ulong)UnityEngine.Random.Range(100, 999);
            _activeHostIp = targetIp;
            _activePort = targetPort;
            _activeJoinCode = trimmedInput.ToUpperInvariant();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
            }

            SendHandshakeRequestToHost();

            Debug.Log($"<color=#38BDF8>[EmpireNetworkManager]</color> Connecting to Host at {targetIp}:{targetPort} (Code: {_activeJoinCode})...");
            OnSessionStarted?.Invoke();
            return true;
        }

        private bool BeginSteamClientJoin(ulong steamLobbyId, ulong hostSteamId, string displayCode)
        {
            if (_steamTransport == null) InitSteamRelayTransport();
            if (!IsSteamAvailable)
            {
                Debug.LogWarning("[EmpireNetworkManager] Cannot join Steam Relay room: Steam is not running.");
                return false;
            }

            PrepareClientSessionStateForSteam(displayCode);
            _ = ConnectSteamClientAsync(steamLobbyId, hostSteamId);
            return true;
        }

        private bool BeginSteamClientJoinByCode(string sixCharCode)
        {
            if (_steamTransport == null) InitSteamRelayTransport();
            if (!IsSteamAvailable) return false;

            PrepareClientSessionStateForSteam(sixCharCode);
            _ = ConnectSteamClientByCodeAsync(sixCharCode);
            return true;
        }

        private void PrepareClientSessionStateForSteam(string displayCode)
        {
            Application.runInBackground = true;
            _isSessionActive = true;
            _isHost = false;
            _isUsingSteamRelayClient = true;
            _isClientHandshakeAccepted = false;
            _handshakeRetryTimer = 0.6f;
            _localLobbyCharacterId = "";
            _localClientId = (ulong)UnityEngine.Random.Range(100, 999);
            _activeHostIp = "STEAM_RELAY";
            _activePort = 0;
            _activeJoinCode = string.IsNullOrWhiteSpace(displayCode) ? "STEAM" : displayCode.Trim().ToUpperInvariant();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
            }

            OnSessionStarted?.Invoke();
        }

        private async System.Threading.Tasks.Task ConnectSteamClientAsync(ulong steamLobbyId, ulong hostSteamId)
        {
            bool ok = false;
            if (steamLobbyId != 0)
            {
                ok = await _steamTransport.JoinSteamLobbyByIdAsync(steamLobbyId);
            }
            if (!ok && hostSteamId != 0)
            {
                ok = _steamTransport.ConnectToSteamHost(hostSteamId);
            }
            if (!ok)
            {
                Debug.LogWarning("[EmpireNetworkManager] Failed to connect via Steam Relay.");
                DisconnectSession();
            }
        }

        private async System.Threading.Tasks.Task ConnectSteamClientByCodeAsync(string sixCharCode)
        {
            bool ok = await _steamTransport.FindAndJoinSteamRoomByCodeAsync(sixCharCode);
            if (!ok)
            {
                Debug.LogWarning($"[EmpireNetworkManager] Steam room code '{sixCharCode}' not found online. Trying LAN fallback...");
                _isUsingSteamRelayClient = false;
                if (LobbyRelayService.TryResolveJoinTarget(sixCharCode, out string lanIp, out int lanPort))
                {
                    StartClientSession($"{lanIp}:{lanPort}");
                }
                else
                {
                    DisconnectSession();
                }
            }
        }

        private async void HandleSteamInviteJoinRequested(Steamworks.Data.Lobby lobby, Steamworks.SteamId friendId)
        {
            DisconnectSession();
            string code = lobby.GetData(SteamP2PTransport.LobbyCodeKey);
            PrepareClientSessionStateForSteam(string.IsNullOrEmpty(code) ? lobby.Id.Value.ToString() : code);

            bool ok = await _steamTransport.JoinSteamLobbyAsync(lobby);
            if (!ok)
            {
                DisconnectSession();
                return;
            }

            if (SceneManager.GetActiveScene().name != "Lobby_NetCode" && SceneManager.GetActiveScene().name != "Gameplay_3D")
            {
                SceneManager.LoadScene("Lobby_NetCode");
            }
            else if (MultiplayerLobbyUI.Instance != null)
            {
                MultiplayerLobbyUI.Instance.ShowRoomInfoPanelAfterExternalJoin();
            }
        }

        private void HandleSteamClientConnectedToHost()
        {
            if (_isSessionActive && !_isHost && _isUsingSteamRelayClient)
            {
                SendHandshakeRequestToHost();
            }
        }

        private void HandleSteamClientDisconnectedFromHost()
        {
            if (_isSessionActive && !_isHost && _isUsingSteamRelayClient)
            {
                DisconnectSession();
                OnKickedFromRoom?.Invoke();
            }
        }

        private void HandleSteamPeerDisconnectedOnHost(uint connId, Steamworks.SteamId steamId)
        {
            if (!_isSessionActive || !_isHost) return;
            if (!_steamConnToClientId.TryGetValue(connId, out ulong clientId)) return;

            RemovePeerFromHost(clientId, sendKickNotice: false);
        }

        /// <summary>
        /// ลบลูกห้อง (clientId) ออกจากห้อง Lobby และในฉากเกมอย่างสมบูรณ์ทั้งฝั่ง LAN UDP และ Steam Relay
        /// พร้อมจัดเรียง slotIndex ใหม่ (0..N-1) และกระจาย Roster ล่าสุดให้ทุกคนในห้องทันที
        /// </summary>
        private void RemovePeerFromHost(ulong clientId, bool sendKickNotice, string kickReason = "Kicked by Host")
        {
            if (!_isSessionActive || !_isHost || clientId == 0) return;

            if (sendKickNotice)
            {
                SendPacket(NetPacketType.DisconnectNotice, w =>
                {
                    w.Write(kickReason ?? "Disconnected");
                }, clientId);
            }

            if (_clientEndpoints.TryGetValue(clientId, out IPEndPoint ep) && ep != null)
            {
                _endpointToClientId.Remove(ep.ToString());
                _clientEndpoints.Remove(clientId);
            }

            if (_clientIdToSteamConn.TryGetValue(clientId, out uint steamConnId))
            {
                _steamConnToClientId.Remove(steamConnId);
                _clientIdToSteamConn.Remove(clientId);
                _steamTransport?.DisconnectPeer(steamConnId);
            }

            _clientIdToSteamId.Remove(clientId);
            _clientLastHeardRealtime.Remove(clientId);

            int idx = _lobbyMembers.FindIndex(m => m.clientId == clientId);
            if (idx >= 0)
            {
                _lobbyMembers.RemoveAt(idx);
                for (int i = 0; i < _lobbyMembers.Count; i++)
                {
                    var m = _lobbyMembers[i];
                    m.slotIndex = i;
                    _lobbyMembers[i] = m;
                }
            }

            PlayerController avatar = PlayerRegistry.GetPlayerByClientId(clientId);
            if (avatar != null && avatar.IsRemotePlayer)
            {
                PlayerRegistry.Unregister(avatar);
                Destroy(avatar.gameObject);
            }

            BroadcastLobbyRosterFromHost();
            OnLobbyRosterChanged?.Invoke();
            OnPeerDisconnected?.Invoke(clientId);
        }

        public void DisconnectSession()
        {
            // แจ้ง Host ทันทีก่อนปิด Socket เพื่อให้ Host เคลียร์สล็อตของเราออกจากห้อง Lobby ทันทีไม่ค้างซ้ำ
            if (_isSessionActive && !_isHost)
            {
                SendPacket(NetPacketType.DisconnectNotice, w => { w.Write("Client Left"); });
            }

            ClearSimulatedTeammates();
            RemoveAllRemotePlayers();
            NetworkBuildingSyncManager.Instance?.ClearRegistry();
            NetworkLootManager.Instance?.ClearMatchLootCache();
            DayNightManager.ClearPendingNetworkTimeSync();
            NetworkEnemyBridge.Instance?.ClearStageProgressCache();
            CloseUdpSocket();
            _steamTransport?.CloseSocketsAndLobby();

            _clientEndpoints.Clear();
            _endpointToClientId.Clear();
            _steamConnToClientId.Clear();
            _clientIdToSteamConn.Clear();
            _clientIdToSteamId.Clear();
            _clientLastHeardRealtime.Clear();
            _lobbyMembers.Clear();
            _nextAssignedClientId = 1;

            bool wasActive = _isSessionActive;
            _isSessionActive = false;
            _isHost = true;
            _isUsingSteamRelayClient = false;
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
        /// อัปเดตตัวละครที่ผู้เล่นเครื่องนี้เลือกจากปุ่มประแจสีเหลืองใน RoomInfoPanel
        /// แล้วซิงค์ให้ทุกคนในห้องเห็นรูปโปรไฟล์ตัวละครใหม่ทันที
        /// </summary>
        public void UpdateLocalLobbyCharacter(string newCharacterId)
        {
            if (string.IsNullOrEmpty(newCharacterId)) return;
            _localLobbyCharacterId = newCharacterId;
            CharacterManager.SelectedCharacterId = newCharacterId;

            if (!_isSessionActive) return;

            if (_isHost)
            {
                for (int i = 0; i < _lobbyMembers.Count; i++)
                {
                    if (_lobbyMembers[i].clientId == _localClientId || _lobbyMembers[i].isHost)
                    {
                        var m = _lobbyMembers[i];
                        m.characterId = newCharacterId;
                        _lobbyMembers[i] = m;
                        break;
                    }
                }
                BroadcastLobbyRosterFromHost();
                OnLobbyRosterChanged?.Invoke();
            }
            else
            {
                SendPacket(NetPacketType.LobbyCharacterChange, w =>
                {
                    w.Write(newCharacterId);
                });
            }
        }

        /// <summary>
        /// เตะผู้เล่นในช่อง slotIndex (1..5) ออกจากห้อง (เรียกได้เฉพาะ Host เมื่อกดปุ่มแดง [X] บนการ์ดสมาชิกใน RoomInfoPanel)
        /// </summary>
        public void KickLobbyMemberBySlot(int slotIndex)
        {
            if (!_isSessionActive || !_isHost || slotIndex <= 0) return;

            int memberIdx = -1;
            for (int i = 0; i < _lobbyMembers.Count; i++)
            {
                if (_lobbyMembers[i].slotIndex == slotIndex)
                {
                    memberIdx = i;
                    break;
                }
            }

            if (memberIdx < 0) return;

            ulong targetClientId = _lobbyMembers[memberIdx].clientId;
            RemovePeerFromHost(targetClientId, sendKickNotice: true, "Kicked by Host");
        }

        public void BroadcastLobbyRosterFromHost()
        {
            if (!_isSessionActive || !_isHost) return;

            SendPacket(NetPacketType.LobbyRosterSync, w =>
            {
                w.Write(_activeRoomName ?? "JUNK CYCLONE CRISIS");
                w.Write(_activeJoinCode ?? "");
                w.Write(_lobbyMembers.Count);
                for (int i = 0; i < _lobbyMembers.Count; i++)
                {
                    var m = _lobbyMembers[i];
                    w.Write(m.clientId);
                    w.Write(m.slotIndex);
                    w.Write(m.playerName ?? $"PLAYER-{i + 1}");
                    w.Write(m.characterId ?? "");
                    w.Write(m.isHost);
                }
            });
        }

        /// <summary>
        /// เรียกเมื่อ Host กดปุ่ม `ENTER WASTELAND` ใน `RoomInfoPanel` เพื่อพาทุกคนในห้องโหลดเข้าฉาก `Gameplay_3D` พร้อมกัน
        /// </summary>
        public void HostStartGameplayFromLobby(string gameplaySceneName = "Gameplay_3D")
        {
            if (!_isSessionActive)
            {
                StartHostSession();
            }

            // บังคับใช้ตัวละครที่เครื่อง Host เลือกไว้ในห้อง Lobby (กัน PlayerPrefs โดนหน้าต่าง Client บนเครื่องเดียวกันเขียนทับ)
            if (!string.IsNullOrEmpty(_localLobbyCharacterId))
            {
                CharacterManager.SelectedCharacterId = _localLobbyCharacterId;
            }

            // สุ่ม WorldSeed กลาง 1 ค่าแล้วส่งให้ทุกเครื่องก่อนโหลดเข้าด่าน เพื่อให้แมพ 128x128 และห้องศัตรูตรงกัน 100%
            NetworkBuildingSyncManager.Instance?.ClearRegistry();
            NetworkLootManager.Instance?.ClearMatchLootCache();
            DayNightManager.ClearPendingNetworkTimeSync();
            int matchWorldSeed = UnityEngine.Random.Range(1000, 99999);
            PendingMatchWorldSeed = matchWorldSeed;

            if (_isHost)
            {
                SendPacket(NetPacketType.HostStartGameplay, w =>
                {
                    w.Write(gameplaySceneName ?? "Gameplay_3D");
                    w.Write(matchWorldSeed);
                });
            }

            MainMenuManager.ShouldLoadSave = false;
            _hasTriggeredPartyWipe = false;
            SceneManager.LoadScene(string.IsNullOrEmpty(gameplaySceneName) ? "Gameplay_3D" : gameplaySceneName);
        }

        /// <summary>
        /// แจ้งเหตุการณ์ Party Wipe (ผู้เล่นทุกคนในห้องตาย/ล้มหมดแล้ว) -> Host กระจายให้ทุกเครื่องแสดงหน้า Game Over พร้อมกัน
        /// </summary>
        public void NotifyPartyWipe()
        {
            if (!_isSessionActive)
            {
                ApplyPartyWipeLocally();
                return;
            }

            if (_isHost)
            {
                if (_hasTriggeredPartyWipe) return;
                _hasTriggeredPartyWipe = true;
                SendPacket(NetPacketType.PartyWipeBroadcast, w => { w.Write((byte)1); });
                ApplyPartyWipeLocally();
            }
            else
            {
                // Client แจ้ง Host ว่าตัวเองเป็นคนสุดท้ายที่ตาย เพื่อให้ Host ยืนยันและกระจาย Party Wipe ทั้งห้อง
                SendPacket(NetPacketType.PartyWipeBroadcast, w => { w.Write((byte)1); });
                if (!_hasTriggeredPartyWipe)
                {
                    ApplyPartyWipeLocally();
                }
            }
        }

        private void ApplyPartyWipeLocally()
        {
            _hasTriggeredPartyWipe = true;

            var all = PlayerRegistry.AllPlayers;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerController p = all[i];
                if (p != null)
                {
                    p.ForcePartyWipeDeathFromNetwork();
                }
            }

            PlayerHUD.Instance?.ShowCoopPartyWipeGameOver();
        }

        /// <summary>
        /// เรียกเมื่อหัวห้อง (Host) กดปุ่ม Retry บนหน้า Game Over เพื่อเริ่มเกมใหม่พร้อมกันทั้งห้อง
        /// </summary>
        public void HostRetryMatch(bool randomizeSeed)
        {
            if (_isSessionActive && !_isHost) return;

            Time.timeScale = 1f;
            _hasTriggeredPartyWipe = false;

            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.ResetDayCount();
            }

            NetworkBuildingSyncManager.Instance?.ClearRegistry();
            NetworkLootManager.Instance?.ClearMatchLootCache();
            NetworkEnemyBridge.Instance?.ClearClientPuppets();
            NetworkEnemyBridge.Instance?.ClearStageProgressCache();
            RemoveAllRemotePlayers();
            ClearSimulatedTeammates();

            int seedToUse = UnityEngine.Random.Range(1000, 99999);
            if (!randomizeSeed && WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null && WorldMapManager.Instance.MapGenerator.seed > 0)
            {
                seedToUse = WorldMapManager.Instance.MapGenerator.seed;
            }

            PendingMatchWorldSeed = seedToUse;

            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                WorldMapManager.Instance.ResetMapProgression();
                WorldMapManager.Instance.StartNewGame(seedToUse);
            }

            string targetScene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(targetScene)) targetScene = "Gameplay_3D";

            if (_isSessionActive && _isHost)
            {
                SendPacket(NetPacketType.HostRetryMatch, w =>
                {
                    w.Write(targetScene);
                    w.Write(seedToUse);
                });
            }

            MainMenuManager.ShouldLoadSave = false;
            SceneManager.LoadScene(targetScene);
        }

        private void HandleHostRetryMatchOnClient(string targetScene, int retrySeed)
        {
            Time.timeScale = 1f;
            _hasTriggeredPartyWipe = false;

            if (DayNightManager.Instance != null)
            {
                DayNightManager.Instance.ResetDayCount();
            }

            NetworkBuildingSyncManager.Instance?.ClearRegistry();
            NetworkLootManager.Instance?.ClearMatchLootCache();
            NetworkEnemyBridge.Instance?.ClearClientPuppets();
            NetworkEnemyBridge.Instance?.ClearStageProgressCache();
            RemoveAllRemotePlayers();

            if (retrySeed > 0)
            {
                PendingMatchWorldSeed = retrySeed;
            }

            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                WorldMapManager.Instance.ResetMapProgression();
                if (retrySeed > 0)
                {
                    WorldMapManager.Instance.StartNewGame(retrySeed);
                }
            }

            if (string.IsNullOrEmpty(targetScene)) targetScene = "Gameplay_3D";
            SceneManager.LoadScene(targetScene);
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
            var allClientIds = new List<ulong>();
            foreach (var kvp in _clientEndpoints) allClientIds.Add(kvp.Key);
            foreach (var kvp in _clientIdToSteamConn) allClientIds.Add(kvp.Key);

            for (int i = 0; i < allClientIds.Count; i++)
            {
                ulong clientId = allClientIds[i];
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
            if (!_isSessionActive) return;
            bool hasUdp = _udpSocket != null;
            bool hasSteam = _steamTransport != null && (_isHost ? _steamTransport.IsHostRelayActive : _isUsingSteamRelayClient);
            if (!hasUdp && !hasSteam) return;

            using (MemoryStream ms = new MemoryStream(256))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)0x54); // 'T'
                writer.Write((byte)0x4C); // 'L'
                writer.Write((byte)type);
                writer.Write(_localClientId);
                writeAction?.Invoke(writer);

                byte[] bytes = ms.ToArray();
                bool reliable = SteamP2PTransport.IsReliablePacket(type);

                try
                {
                    if (_isHost)
                    {
                        if (targetClientId != ulong.MaxValue)
                        {
                            if (hasUdp && _clientEndpoints.TryGetValue(targetClientId, out IPEndPoint singleEp))
                            {
                                _udpSocket.Send(bytes, bytes.Length, singleEp);
                            }
                            else if (hasSteam && _clientIdToSteamConn.TryGetValue(targetClientId, out uint singleSteamConn))
                            {
                                _steamTransport.SendToPeer(singleSteamConn, bytes, reliable);
                            }
                        }
                        else
                        {
                            if (hasUdp)
                            {
                                foreach (var kvp in _clientEndpoints)
                                {
                                    _udpSocket.Send(bytes, bytes.Length, kvp.Value);
                                }
                            }
                            if (hasSteam)
                            {
                                _steamTransport.BroadcastToPeers(bytes, reliable);
                            }
                        }
                    }
                    else if (_isUsingSteamRelayClient && hasSteam)
                    {
                        _steamTransport.SendToServer(bytes, reliable);
                    }
                    else if (hasUdp && _serverEndPoint != null)
                    {
                        _udpSocket.Send(bytes, bytes.Length, _serverEndPoint);
                    }
                }
                catch { }
            }
        }

        private void HandleIncomingSteamPacket(uint steamConnId, byte[] data)
        {
            if (!_isSessionActive || data == null || data.Length < 11) return;
            if (data[0] != 0x54 || data[1] != 0x4C) return;

            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    reader.ReadByte();
                    reader.ReadByte();
                    NetPacketType packetType = (NetPacketType)reader.ReadByte();
                    ulong senderId = reader.ReadUInt64();
                    HandleIncomingPacket(packetType, senderId, null, reader, steamConnId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EmpireNetworkManager] Steam packet parse error: {ex.Message}");
            }
        }

        private void SendHandshakeRequestToHost()
        {
            PlayerController local = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            string charId = local != null && local.ActiveCharacterData != null
                ? local.ActiveCharacterData.characterId
                : (_localLobbyCharacterId ?? "");

            SendPacket(NetPacketType.HandshakeRequest, w =>
            {
                w.Write(GetOrInitLocalPlayerName());
                w.Write(charId);
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

        private void HandleIncomingPacket(NetPacketType type, ulong senderId, IPEndPoint remoteEp, BinaryReader reader, uint steamConnId = 0)
        {
            if (_isHost)
            {
                if (remoteEp != null)
                {
                    string epKey = remoteEp.ToString();
                    if (_endpointToClientId.TryGetValue(epKey, out ulong registeredId))
                    {
                        senderId = registeredId;
                        _clientLastHeardRealtime[senderId] = Time.realtimeSinceStartup;
                    }
                    else if (type == NetPacketType.HandshakeRequest)
                    {
                        if (_clientEndpoints.Count + _clientIdToSteamConn.Count + 1 >= MaxPlayers) return; // ห้องเต็ม 6 คนแล้ว
                        ulong assignedId = _nextAssignedClientId++;
                        _endpointToClientId[epKey] = assignedId;
                        _clientEndpoints[assignedId] = remoteEp;
                        _clientLastHeardRealtime[assignedId] = Time.realtimeSinceStartup;
                        senderId = assignedId;
                    }
                }
                else if (steamConnId != 0)
                {
                    if (_steamConnToClientId.TryGetValue(steamConnId, out ulong registeredSteamId))
                    {
                        senderId = registeredSteamId;
                        _clientLastHeardRealtime[senderId] = Time.realtimeSinceStartup;
                    }
                    else if (type == NetPacketType.HandshakeRequest)
                    {
                        // กันไอดี Steam เดิมเข้าออกซ้ำจนเต็มห้อง: ถ้า SteamId นี้เคยค้างอยู่ในห้อง ให้เคลียร์สล็อตเก่าก่อนเสมอ!
                        ulong incomingSteamId = 0;
                        if (_steamTransport != null && _steamTransport.TryGetPeerSteamId(steamConnId, out Steamworks.SteamId sId))
                        {
                            incomingSteamId = sId.Value;
                            if (incomingSteamId != 0)
                            {
                                ulong duplicateClientId = 0;
                                foreach (var kvp in _clientIdToSteamId)
                                {
                                    if (kvp.Value == incomingSteamId)
                                    {
                                        duplicateClientId = kvp.Key;
                                        break;
                                    }
                                }
                                if (duplicateClientId != 0)
                                {
                                    RemovePeerFromHost(duplicateClientId, sendKickNotice: false);
                                }
                            }
                        }

                        if (_clientEndpoints.Count + _clientIdToSteamConn.Count + 1 >= MaxPlayers) return; // ห้องเต็ม 6 คนแล้ว
                        ulong assignedId = _nextAssignedClientId++;
                        _steamConnToClientId[steamConnId] = assignedId;
                        _clientIdToSteamConn[assignedId] = steamConnId;
                        if (incomingSteamId != 0) _clientIdToSteamId[assignedId] = incomingSteamId;
                        _clientLastHeardRealtime[assignedId] = Time.realtimeSinceStartup;
                        senderId = assignedId;
                    }
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
                        int slotIdx = Mathf.Clamp(_clientEndpoints.Count + _clientIdToSteamConn.Count, 1, MaxPlayers - 1);

                        // บันทึกสมาชิกเข้า Lobby Roster (ป้องกันซ้ำหากส่ง Handshake ซ้ำ; ถ้ายังไม่เลือกตัวละครให้เก็บ "" เพื่อโชว์รูป Default)
                        int existingIdx = _lobbyMembers.FindIndex(m => m.clientId == senderId);
                        if (existingIdx >= 0)
                        {
                            slotIdx = _lobbyMembers[existingIdx].slotIndex;
                            string keepChar = !string.IsNullOrEmpty(clientCharId) ? clientCharId : _lobbyMembers[existingIdx].characterId;
                            _lobbyMembers[existingIdx] = new LobbyMemberNetInfo
                            {
                                clientId = senderId,
                                slotIndex = slotIdx,
                                playerName = string.IsNullOrEmpty(clientName) ? $"PLAYER-{slotIdx + 1}" : clientName,
                                characterId = keepChar ?? "",
                                isHost = false
                            };
                        }
                        else if (_lobbyMembers.Count < MaxPlayers)
                        {
                            slotIdx = _lobbyMembers.Count;
                            _lobbyMembers.Add(new LobbyMemberNetInfo
                            {
                                clientId = senderId,
                                slotIndex = slotIdx,
                                playerName = string.IsNullOrEmpty(clientName) ? $"PLAYER-{slotIdx + 1}" : clientName,
                                characterId = clientCharId ?? "",
                                isHost = false
                            });
                        }

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

                        BroadcastLobbyRosterFromHost();
                        OnLobbyRosterChanged?.Invoke();
                        NetworkBuildingSyncManager.Instance?.SendAllPlacedBuildingsToClient(senderId);
                        DayNightManager.Instance?.BroadcastTimeSyncFromHost(senderId, snapVisuals: true);
                        OnPeerConnected?.Invoke(senderId);
                    }
                    break;

                case NetPacketType.DisconnectNotice:
                    if (_isHost)
                    {
                        // ลูกห้องกด LEAVE หรือปิดเกม -> เคลียร์สล็อตออกจากห้องและกระจาย Roster ล่าสุดทันที!
                        RemovePeerFromHost(senderId, sendKickNotice: false);
                    }
                    else
                    {
                        DisconnectSession();
                        OnKickedFromRoom?.Invoke();
                    }
                    break;

                case NetPacketType.ClientHeartbeat:
                    // _clientLastHeardRealtime ถูกอัปเดตด้านบนแล้ว
                    break;

                case NetPacketType.LobbyRosterSync:
                    if (!_isHost)
                    {
                        _activeRoomName = reader.ReadString();
                        string hostJoinCode = reader.ReadString();
                        if (!string.IsNullOrEmpty(hostJoinCode)) _activeJoinCode = hostJoinCode;
                        int memberCount = reader.ReadInt32();
                        _lobbyMembers.Clear();
                        for (int i = 0; i < memberCount; i++)
                        {
                            _lobbyMembers.Add(new LobbyMemberNetInfo
                            {
                                clientId = reader.ReadUInt64(),
                                slotIndex = reader.ReadInt32(),
                                playerName = reader.ReadString(),
                                characterId = reader.ReadString(),
                                isHost = reader.ReadBoolean()
                            });
                        }
                        OnLobbyRosterChanged?.Invoke();
                    }
                    break;

                case NetPacketType.HostStartGameplay:
                    if (!_isHost)
                    {
                        string targetScene = reader.ReadString();
                        int matchWorldSeed = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : 0;
                        if (matchWorldSeed != 0)
                        {
                            PendingMatchWorldSeed = matchWorldSeed;
                        }
                        if (!string.IsNullOrEmpty(_localLobbyCharacterId))
                        {
                            CharacterManager.SelectedCharacterId = _localLobbyCharacterId;
                        }
                        if (string.IsNullOrEmpty(targetScene)) targetScene = "Gameplay_3D";
                        if (SceneManager.GetActiveScene().name != targetScene)
                        {
                            SceneManager.LoadScene(targetScene);
                        }
                    }
                    break;

                case NetPacketType.PartyWipeBroadcast:
                    if (_isHost)
                    {
                        if (!_hasTriggeredPartyWipe)
                        {
                            _hasTriggeredPartyWipe = true;
                            SendPacket(NetPacketType.PartyWipeBroadcast, w => { w.Write((byte)1); });
                            ApplyPartyWipeLocally();
                        }
                    }
                    else
                    {
                        ApplyPartyWipeLocally();
                    }
                    break;

                case NetPacketType.HostRetryMatch:
                    if (!_isHost)
                    {
                        string retryScene = reader.ReadString();
                        int retrySeed = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : 0;
                        HandleHostRetryMatchOnClient(retryScene, retrySeed);
                    }
                    break;

                case NetPacketType.LobbyCharacterChange:
                    if (_isHost)
                    {
                        string newCharId = reader.ReadString();
                        for (int i = 0; i < _lobbyMembers.Count; i++)
                        {
                            if (_lobbyMembers[i].clientId == senderId)
                            {
                                var m = _lobbyMembers[i];
                                m.characterId = newCharId;
                                _lobbyMembers[i] = m;
                                break;
                            }
                        }
                        PlayerController remoteAvatar = PlayerRegistry.GetPlayerByClientId(senderId);
                        if (remoteAvatar != null)
                        {
                            remoteAvatar.ApplyRemoteCharacterById(newCharId);
                        }
                        BroadcastLobbyRosterFromHost();
                        OnLobbyRosterChanged?.Invoke();
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
                            local.NetworkPlayerName = GetOrInitLocalPlayerName();
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
                                bool wasSameStage = remote.IsInSameStageAsLocalPlayer;
                                remote.ApplyNetworkState(payload);
                                bool isNowSameStage = remote.IsInSameStageAsLocalPlayer;

                                // หากเพื่อน (Client) เพิ่งเดินตามเข้ามาในช่องพิกัดเดียวกับ Host ให้ส่งของดรอปบนพื้นที่ยังเหลืออยู่ไปให้เห็นครบทันที!
                                if (_isHost && !wasSameStage && isNowSameStage)
                                {
                                    NetworkLootManager.Instance?.BroadcastAllActiveStageDropsToClient(payload.clientId);
                                }
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

                case NetPacketType.ReviveAction:
                    {
                        ulong targetClientId = reader.ReadUInt64();
                        float progress01 = reader.ReadSingle();
                        bool isCompleted = reader.ReadBoolean();

                        if (_isHost)
                        {
                            SendPacket(NetPacketType.ReviveAction, w =>
                            {
                                w.Write(targetClientId);
                                w.Write(progress01);
                                w.Write(isCompleted);
                            });
                        }

                        PlayerController.HandleIncomingReviveAction(targetClientId, progress01, isCompleted);
                    }
                    break;

                case NetPacketType.FriendlyFireDamage:
                    {
                        ulong targetClientId = reader.ReadUInt64();
                        ulong attackerClientId = reader.ReadUInt64();
                        int ffStageX = reader.ReadInt32();
                        int ffStageY = reader.ReadInt32();
                        int ffInteriorId = reader.ReadInt32();
                        float ffDamage = reader.ReadSingle();
                        Vector3 ffHitPoint = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        bool ffIsCrit = reader.ReadBoolean();
                        NetworkPlayerBridge.HandleIncomingFriendlyFireDamage(
                            targetClientId, attackerClientId,
                            ffStageX, ffStageY, ffInteriorId,
                            ffDamage, ffHitPoint, ffIsCrit,
                            relayIfHost: _isHost);
                    }
                    break;

                case NetPacketType.DayNightTimeSync:
                    {
                        int syncedDay = reader.ReadInt32();
                        byte syncedPhase = reader.ReadByte();
                        float syncedPhaseTimer = reader.ReadSingle();
                        float syncedSpeed = reader.ReadSingle();
                        bool snapVisuals = reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadBoolean();

                        if (!_isHost)
                        {
                            DayNightManager.HandleIncomingTimeSync(syncedDay, syncedPhase, syncedPhaseTimer, syncedSpeed, snapVisuals);
                        }
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

                case NetPacketType.StageEventBroadcast:
                    if (!_isHost)
                    {
                        int evStageX = reader.ReadInt32();
                        int evStageY = reader.ReadInt32();
                        StageEventType evType = (StageEventType)reader.ReadByte();
                        int evSeed = reader.ReadInt32();
                        RandomStageEventManager.Instance?.HandleSyncedStageEventFromHost(evStageX, evStageY, evType, evSeed);
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
                        ulong sourceClientId = reader.BaseStream.Position + 8 <= reader.BaseStream.Length ? reader.ReadUInt64() : ulong.MaxValue;
                        // ไม่ต้องโชว์ตัวเลขดาเมจซ้ำบนเครื่อง Client ที่เป็นคนยิงเอง (เพราะเครื่องคนยิงแสดงไปตอน 0ms แล้ว)
                        if (sourceClientId != _localClientId)
                        {
                            DamageNumberManager.Instance?.SpawnDamageNumber(pos, dmg, isCrit);
                        }
                    }
                    break;

                case NetPacketType.EnemyDamageRequest:
                    if (_isHost)
                    {
                        int enemyNetId = reader.ReadInt32();
                        float dmg = reader.ReadSingle();
                        bool isCrit = reader.ReadBoolean();
                        bool isExplosion = reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadBoolean();
                        NetworkEnemyBridge.Instance?.HandleClientEnemyDamageOnHost(enemyNetId, dmg, isCrit, isExplosion, senderId);
                    }
                    break;

                case NetPacketType.StageEnemyProgressSync:
                    NetworkEnemyBridge.Instance?.ReceiveStageEnemyProgress(reader, isHost: _isHost, senderClientId: senderId);
                    break;

                case NetPacketType.LootSpawnBroadcast:
                    if (!_isHost)
                    {
                        int dropId = reader.ReadInt32();
                        int dropStageX = reader.ReadInt32();
                        int dropStageY = reader.ReadInt32();
                        int dropInteriorId = reader.ReadInt32();
                        string itemName = reader.ReadString();
                        int qty = reader.ReadInt32();
                        bool isMoney = reader.ReadBoolean();
                        int moneyAmt = reader.ReadInt32();
                        Vector3 dropPos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        bool requireManualPickup = reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadBoolean();
                        NetworkLootManager.Instance?.HandleHostLootSpawnOnClient(dropId, dropStageX, dropStageY, dropInteriorId, itemName, qty, isMoney, moneyAmt, dropPos, requireManualPickup);
                    }
                    break;

                case NetPacketType.ClientDropItemRequest:
                    if (_isHost)
                    {
                        int dropStageX = reader.ReadInt32();
                        int dropStageY = reader.ReadInt32();
                        int dropInteriorId = reader.ReadInt32();
                        string itemName = reader.ReadString();
                        int qty = reader.ReadInt32();
                        Vector3 dropPos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        NetworkLootManager.Instance?.HandleClientDropItemRequestOnHost(dropStageX, dropStageY, dropInteriorId, itemName, qty, dropPos);
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

                case NetPacketType.ContainerLootSync:
                    {
                        NetworkLootManager.Instance?.HandleContainerLootSyncPacket(reader, relayIfHost: _isHost);
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

                case NetPacketType.BuildingDestroyBroadcast:
                    {
                        int bStageX = reader.ReadInt32();
                        int bStageY = reader.ReadInt32();
                        Vector3 bPos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        int bInteriorId = reader.BaseStream.Position < reader.BaseStream.Length ? reader.ReadInt32() : 0;
                        NetworkBuildingSyncManager.Instance?.HandleRemoteBuildingDestroyed(bStageX, bStageY, bInteriorId, bPos, relayIfHost: _isHost);
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

                case NetPacketType.FurnitureStateSync:
                    {
                        int fStageX = reader.ReadInt32();
                        int fStageY = reader.ReadInt32();
                        int fInteriorId = reader.ReadInt32();
                        Vector3 fPos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        byte stateType = reader.ReadByte();
                        int intVal = reader.ReadInt32();
                        float floatVal = reader.ReadSingle();
                        NetworkBuildingSyncManager.Instance?.HandleRemoteFurnitureState(
                            fStageX, fStageY, fInteriorId, fPos,
                            (FurnitureStateSyncType)stateType, intVal, floatVal,
                            relayIfHost: _isHost);
                    }
                    break;
            }
        }
    }
}
