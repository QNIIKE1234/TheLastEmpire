using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// SteamP2PTransport — Real-time online Co-op transport powered by Facepunch.Steamworks
    /// Steam Datagram Relay (SteamNetworkingSockets) + SteamMatchmaking Lobbies.
    ///
    /// - Zero dedicated server or port forwarding required (traverses NAT/CGNAT via Valve SDR).
    /// - Uses AppID 480 (Spacewar) for free online testing before publishing on Steam.
    /// - Separates Unreliable (PlayerStateSync / EnemyBatchSync) and Reliable (Loot / Combat / Lobby) packets.
    /// - Plugs directly into EmpireNetworkManager's existing NetPacketType protocol with zero gameplay rewrites.
    /// </summary>
    public class SteamP2PTransport : IConnectionManager, ISocketManager
    {
        public const uint DefaultTestAppId = 480;
        public const string LobbyGameKey = "tle_game";
        public const string LobbyGameValue = "JunkCycloneCrisis_Coop_v1";
        public const string LobbyRoomNameKey = "tle_room";
        public const string LobbyCodeKey = "tle_code";
        public const string LobbyHostSteamIdKey = "tle_host_steamid";
        public const string LobbySceneKey = "tle_scene";

        private class SteamPeerConnection
        {
            public uint ConnectionId;
            public SteamId SteamId;
            public Connection Connection;
        }

        private readonly uint _appId;
        private bool _steamInitialized;
        private bool _relayInitialized;
        private bool _isHostSocketActive;
        private bool _isClientConnected;
        private bool _isLobbyQueryRunning;

        private SocketManager _socketManager;
        private ConnectionManager _connectionManager;
        private Lobby? _activeLobby;

        private readonly Dictionary<uint, SteamPeerConnection> _peers = new Dictionary<uint, SteamPeerConnection>();
        private byte[] _receiveBuffer = new byte[8192];

        public bool IsSteamAvailable => _steamInitialized && SteamClient.IsValid;
        public bool IsHostRelayActive => _isHostSocketActive && _socketManager != null;
        public bool IsClientRelayActive => _connectionManager != null;
        public bool IsClientRelayConnected => _isClientConnected && _connectionManager != null;
        public bool HasActiveLobby => _activeLobby.HasValue && _activeLobby.Value.Id.IsValid;
        public Lobby? ActiveLobby => _activeLobby;
        public ulong LocalSteamId => IsSteamAvailable ? SteamClient.SteamId.Value : 0;
        public string LocalSteamName => IsSteamAvailable ? SteamClient.Name : "";
        public int ConnectedSteamPeerCount => _peers.Count;

        public event Action<uint, SteamId> OnSteamPeerConnected;
        public event Action<uint, SteamId> OnSteamPeerDisconnected;
        public event Action OnClientConnectedToHost;
        public event Action OnClientDisconnectedFromHost;
        public event Action<uint, byte[]> OnPacketReceived;
        public event Action<Lobby, SteamId> OnInviteJoinRequested;

        public SteamP2PTransport(uint appId = DefaultTestAppId)
        {
            _appId = appId;
        }

        public bool TryInitializeSteam()
        {
            if (_steamInitialized && SteamClient.IsValid) return true;

            try
            {
                if (!SteamClient.IsValid)
                {
                    SteamClient.Init(_appId, true);
                }

                if (SteamClient.IsValid)
                {
                    _steamInitialized = true;
                    EnsureRelayAccess();

                    SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
                    SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;

                    Debug.Log($"<color=#38BDF8>[SteamP2PTransport]</color> Steam initialized: <b>{SteamClient.Name}</b> (SteamId={SteamClient.SteamId}, AppID={_appId})");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamP2PTransport] Steam is not running or unavailable ({ex.Message}). LAN/Local mode remains active.");
            }

            _steamInitialized = false;
            return false;
        }

        private void EnsureRelayAccess()
        {
            if (!_relayInitialized && SteamClient.IsValid)
            {
                SteamNetworkingUtils.InitRelayNetworkAccess();
                _relayInitialized = true;
            }
        }

        public void Tick()
        {
            if (!_steamInitialized || !SteamClient.IsValid) return;

            try
            {
                SteamClient.RunCallbacks();
                EnsureRelayAccess();
                _socketManager?.Receive();
                _connectionManager?.Receive();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamP2PTransport] Tick error: {ex.Message}");
            }
        }

        // =========================================================================
        //  HOST: CREATE STEAM RELAY SOCKET + STEAM LOBBY
        // =========================================================================
        public async Task<bool> StartHostRelayAsync(string roomName, string joinCode, int maxPlayers, string sceneName)
        {
            if (!TryInitializeSteam()) return false;

            CloseSocketsAndLobby();
            EnsureRelayAccess();

            try
            {
                _socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(0);
                _socketManager.Interface = this;
                _isHostSocketActive = true;

                Lobby? created = await SteamMatchmaking.CreateLobbyAsync(Mathf.Clamp(maxPlayers, 2, 8));
                if (created.HasValue)
                {
                    Lobby lobby = created.Value;
                    lobby.SetPublic();
                    lobby.SetJoinable(true);
                    lobby.SetData(LobbyGameKey, LobbyGameValue);
                    lobby.SetData(LobbyRoomNameKey, string.IsNullOrWhiteSpace(roomName) ? SteamClient.Name : roomName.Trim());
                    lobby.SetData(LobbyCodeKey, (joinCode ?? "").Trim().ToUpperInvariant());
                    lobby.SetData(LobbyHostSteamIdKey, SteamClient.SteamId.Value.ToString());
                    lobby.SetData(LobbySceneKey, string.IsNullOrEmpty(sceneName) ? "Lobby_NetCode" : sceneName);
                    _activeLobby = lobby;

                    Debug.Log($"<color=#38BDF8>[SteamP2PTransport]</color> Steam Relay Host LIVE! LobbyId={lobby.Id}, Code=<b>{joinCode}</b>, HostSteamId={SteamClient.SteamId}");
                }
                else
                {
                    Debug.LogWarning("[SteamP2PTransport] Steam Relay socket opened, but Steam Lobby creation returned null.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTransport] StartHostRelayAsync failed: {ex}");
                CloseSocketsAndLobby();
                return false;
            }
        }

        public void UpdateHostLobbyMetadata(string roomName, string joinCode, string sceneName)
        {
            if (!HasActiveLobby || !_isHostSocketActive) return;

            try
            {
                Lobby lobby = _activeLobby.Value;
                if (!string.IsNullOrWhiteSpace(roomName)) lobby.SetData(LobbyRoomNameKey, roomName.Trim());
                if (!string.IsNullOrWhiteSpace(joinCode)) lobby.SetData(LobbyCodeKey, joinCode.Trim().ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(sceneName)) lobby.SetData(LobbySceneKey, sceneName);
            }
            catch
            {
                // Ignore transient Steam metadata errors
            }
        }

        // =========================================================================
        //  CLIENT: CONNECT OVER STEAM RELAY (BY HOST STEAM ID / LOBBY ID / CODE)
        // =========================================================================
        public bool ConnectToSteamHost(ulong hostSteamId)
        {
            if (hostSteamId == 0) return false;
            if (!TryInitializeSteam()) return false;

            CloseSocketsOnly();
            EnsureRelayAccess();

            try
            {
                _isClientConnected = false;
                _connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(hostSteamId, 0);
                _connectionManager.Interface = this;
                Debug.Log($"<color=#38BDF8>[SteamP2PTransport]</color> Connecting via Steam Relay to Host SteamId={hostSteamId}...");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTransport] ConnectToSteamHost({hostSteamId}) failed: {ex}");
                CloseSocketsAndLobby();
                return false;
            }
        }

        public async Task<bool> JoinSteamLobbyByIdAsync(ulong lobbyId)
        {
            if (lobbyId == 0 || !TryInitializeSteam()) return false;

            try
            {
                Lobby? joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
                if (!joined.HasValue)
                {
                    Debug.LogWarning($"[SteamP2PTransport] Could not join Steam Lobby {lobbyId}.");
                    return false;
                }

                return ConnectToJoinedLobby(joined.Value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTransport] JoinSteamLobbyByIdAsync failed: {ex}");
                return false;
            }
        }

        public async Task<bool> JoinSteamLobbyAsync(Lobby lobby)
        {
            if (!TryInitializeSteam()) return false;

            try
            {
                RoomEnter res = await lobby.Join();
                if (res != RoomEnter.Success)
                {
                    Debug.LogWarning($"[SteamP2PTransport] Failed to enter Steam Lobby {lobby.Id}: {res}");
                    return false;
                }

                return ConnectToJoinedLobby(lobby);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTransport] JoinSteamLobbyAsync failed: {ex}");
                return false;
            }
        }

        public async Task<bool> FindAndJoinSteamRoomByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode) || !TryInitializeSteam()) return false;
            string cleanCode = joinCode.Trim().ToUpperInvariant();

            try
            {
                Lobby[] lobbies = await SteamMatchmaking.LobbyList
                    .FilterDistanceWorldwide()
                    .WithKeyValue(LobbyGameKey, LobbyGameValue)
                    .WithKeyValue(LobbyCodeKey, cleanCode)
                    .WithMaxResults(10)
                    .RequestAsync();

                if (lobbies == null || lobbies.Length == 0)
                {
                    Debug.LogWarning($"[SteamP2PTransport] No Steam Lobby found for code '{cleanCode}'.");
                    return false;
                }

                return await JoinSteamLobbyAsync(lobbies[0]);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamP2PTransport] FindAndJoinSteamRoomByCodeAsync failed: {ex}");
                return false;
            }
        }

        private bool ConnectToJoinedLobby(Lobby lobby)
        {
            _activeLobby = lobby;
            ulong hostSteamId = 0;
            ulong.TryParse(lobby.GetData(LobbyHostSteamIdKey), out hostSteamId);
            if (hostSteamId == 0) hostSteamId = lobby.Owner.Id.Value;

            if (hostSteamId == 0)
            {
                Debug.LogWarning("[SteamP2PTransport] Steam Lobby has no valid HostSteamId.");
                return false;
            }

            return ConnectToSteamHost(hostSteamId);
        }

        // =========================================================================
        //  STEAM LOBBY DISCOVERY (FEEDS DIRECTLY INTO LobbyRelayService)
        // =========================================================================
        public async Task RefreshSteamLobbiesIntoDiscoveryAsync()
        {
            if (_isLobbyQueryRunning || !IsSteamAvailable) return;
            _isLobbyQueryRunning = true;

            try
            {
                Lobby[] lobbies = await SteamMatchmaking.LobbyList
                    .FilterDistanceWorldwide()
                    .WithKeyValue(LobbyGameKey, LobbyGameValue)
                    .WithMaxResults(25)
                    .RequestAsync();

                if (lobbies == null) return;

                foreach (Lobby lobby in lobbies)
                {
                    ulong hostSteamId = 0;
                    ulong.TryParse(lobby.GetData(LobbyHostSteamIdKey), out hostSteamId);
                    if (hostSteamId == 0) hostSteamId = lobby.Owner.Id.Value;
                    if (hostSteamId == 0) continue;

                    string roomName = lobby.GetData(LobbyRoomNameKey);
                    if (string.IsNullOrWhiteSpace(roomName)) roomName = lobby.Owner.Name;
                    if (string.IsNullOrWhiteSpace(roomName)) roomName = $"STEAM ROOM {lobby.Id}";

                    string code = lobby.GetData(LobbyCodeKey);
                    if (string.IsNullOrWhiteSpace(code)) code = lobby.Id.Value.ToString();

                    string scene = lobby.GetData(LobbySceneKey);
                    if (string.IsNullOrWhiteSpace(scene)) scene = "Lobby_NetCode";

                    LobbyRelayService.RegisterDiscoveredSteamRoom(
                        lobby.Id.Value,
                        hostSteamId,
                        code,
                        Mathf.Max(1, lobby.MemberCount),
                        Mathf.Max(2, lobby.MaxMembers),
                        scene,
                        roomName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamP2PTransport] Lobby discovery query failed: {ex.Message}");
            }
            finally
            {
                _isLobbyQueryRunning = false;
            }
        }

        public bool OpenSteamInviteOverlay()
        {
            if (!IsSteamAvailable || !HasActiveLobby) return false;
            SteamFriends.OpenGameInviteOverlay(_activeLobby.Value.Id);
            return true;
        }

        // =========================================================================
        //  SEND / DISCONNECT / CLEANUP
        // =========================================================================
        public static bool IsReliablePacket(NetPacketType type)
        {
            return type != NetPacketType.PlayerStateSync && type != NetPacketType.EnemyBatchSync;
        }

        public void SendToServer(byte[] bytes, bool reliable)
        {
            if (_connectionManager == null || bytes == null || bytes.Length == 0) return;
            try
            {
                SendType sendType = reliable ? SendType.Reliable : SendType.Unreliable;
                _connectionManager.Connection.SendMessage(bytes, 0, bytes.Length, sendType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamP2PTransport] SendToServer failed: {ex.Message}");
            }
        }

        public void SendToPeer(uint connectionId, byte[] bytes, bool reliable)
        {
            if (bytes == null || bytes.Length == 0) return;
            if (_peers.TryGetValue(connectionId, out SteamPeerConnection peer))
            {
                try
                {
                    SendType sendType = reliable ? SendType.Reliable : SendType.Unreliable;
                    peer.Connection.SendMessage(bytes, 0, bytes.Length, sendType);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SteamP2PTransport] SendToPeer({connectionId}) failed: {ex.Message}");
                }
            }
        }

        public void BroadcastToPeers(byte[] bytes, bool reliable, uint excludeConnectionId = 0)
        {
            if (bytes == null || bytes.Length == 0 || _peers.Count == 0) return;
            SendType sendType = reliable ? SendType.Reliable : SendType.Unreliable;

            foreach (var kvp in _peers)
            {
                if (excludeConnectionId != 0 && kvp.Key == excludeConnectionId) continue;
                try
                {
                    kvp.Value.Connection.SendMessage(bytes, 0, bytes.Length, sendType);
                }
                catch
                {
                    // Ignore individual peer send errors; OnDisconnected cleans up dead peers
                }
            }
        }

        public void DisconnectPeer(uint connectionId)
        {
            if (_peers.TryGetValue(connectionId, out SteamPeerConnection peer))
            {
                try
                {
                    peer.Connection.Flush();
                    peer.Connection.Close();
                }
                catch { }
                _peers.Remove(connectionId);
            }
        }

        public int GetEstimatedPingMs()
        {
            try
            {
                if (_connectionManager != null)
                {
                    return Mathf.Max(0, _connectionManager.Connection.QuickStatus().Ping);
                }
            }
            catch { }
            return 0;
        }

        public void CloseSocketsAndLobby()
        {
            CloseSocketsOnly();

            if (_activeLobby.HasValue)
            {
                try { _activeLobby.Value.Leave(); } catch { }
                _activeLobby = null;
            }
        }

        private void CloseSocketsOnly()
        {
            foreach (var kvp in _peers)
            {
                try
                {
                    kvp.Value.Connection.Flush();
                    kvp.Value.Connection.Close();
                }
                catch { }
            }
            _peers.Clear();

            if (_connectionManager != null)
            {
                try { _connectionManager.Close(); } catch { }
                _connectionManager = null;
            }

            if (_socketManager != null)
            {
                try { _socketManager.Close(); } catch { }
                _socketManager = null;
            }

            _isHostSocketActive = false;
            _isClientConnected = false;
        }

        public void ShutdownSteamOnQuit()
        {
            CloseSocketsAndLobby();

            if (_steamInitialized)
            {
                SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
                if (SteamClient.IsValid)
                {
                    try { SteamClient.Shutdown(); } catch { }
                }
                _steamInitialized = false;
            }
        }

        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            Debug.Log($"<color=#38BDF8>[SteamP2PTransport]</color> Steam Friend Invite accepted for Lobby {lobby.Id} from {friendId}.");
            OnInviteJoinRequested?.Invoke(lobby, friendId);
        }

        private byte[] CopyIncomingMessage(IntPtr data, int size)
        {
            if (_receiveBuffer.Length < size)
            {
                _receiveBuffer = new byte[Math.Max(_receiveBuffer.Length * 2, size)];
            }

            Marshal.Copy(data, _receiveBuffer, 0, size);
            byte[] packet = new byte[size];
            Buffer.BlockCopy(_receiveBuffer, 0, packet, 0, size);
            return packet;
        }

        // =========================================================================
        //  IConnectionManager (Client -> Steam Relay Host)
        // =========================================================================
        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {
            Debug.Log($"[SteamP2PTransport] Handshaking with Steam Relay Host {info.Identity.SteamId}...");
        }

        void IConnectionManager.OnConnected(ConnectionInfo info)
        {
            _isClientConnected = true;
            Debug.Log($"<color=#4ADE80>[SteamP2PTransport]</color> Connected to Steam Relay Host {info.Identity.SteamId}!");
            OnClientConnectedToHost?.Invoke();
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            _isClientConnected = false;
            Debug.Log($"<color=#F59E0B>[SteamP2PTransport]</color> Disconnected from Steam Relay Host {info.Identity.SteamId} ({info.EndReason}).");
            OnClientDisconnectedFromHost?.Invoke();
        }

        void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            if (size <= 0 || data == IntPtr.Zero) return;
            byte[] packet = CopyIncomingMessage(data, size);
            OnPacketReceived?.Invoke(0, packet);
        }

        // =========================================================================
        //  ISocketManager (Steam Relay Host <- Clients)
        // =========================================================================
        void ISocketManager.OnConnecting(Connection connection, ConnectionInfo info)
        {
            Debug.Log($"[SteamP2PTransport] Accepting Steam Relay client {info.Identity.SteamId} (connId={connection.Id}).");
            connection.Accept();
        }

        void ISocketManager.OnConnected(Connection connection, ConnectionInfo info)
        {
            uint connId = connection.Id;
            _peers[connId] = new SteamPeerConnection
            {
                ConnectionId = connId,
                SteamId = info.Identity.SteamId,
                Connection = connection
            };

            Debug.Log($"<color=#4ADE80>[SteamP2PTransport]</color> Steam Relay client connected: {info.Identity.SteamId} (connId={connId}).");
            OnSteamPeerConnected?.Invoke(connId, info.Identity.SteamId);
        }

        void ISocketManager.OnDisconnected(Connection connection, ConnectionInfo info)
        {
            uint connId = connection.Id;
            if (_peers.Remove(connId))
            {
                Debug.Log($"<color=#F59E0B>[SteamP2PTransport]</color> Steam Relay client disconnected: {info.Identity.SteamId} (connId={connId}).");
                OnSteamPeerDisconnected?.Invoke(connId, info.Identity.SteamId);
            }
        }

        void ISocketManager.OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            if (size <= 0 || data == IntPtr.Zero) return;
            byte[] packet = CopyIncomingMessage(data, size);
            OnPacketReceived?.Invoke(connection.Id, packet);
        }
    }
}
