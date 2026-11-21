using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace TheLastEmpire.Network
{
    [Serializable]
    public class DiscoveredLanRoom
    {
        public string roomName;
        public string joinCode;
        public string hostIp;
        public int hostPort;
        public int currentPlayers;
        public int maxPlayers;
        public string sceneName;
        public float lastSeenRealtime;
    }

    /// <summary>
    /// LobbyRelayService — จัดการระบบค้นหาห้องในวง LAN อัตโนมัติ (UDP Broadcast Auto-Discovery),
    /// รหัสห้อง 6 หลัก (Join Code), และการแปลง IP:Port สำหรับ LAN / Localhost / VPN (Radmin, ZeroTier)
    /// </summary>
    public static class LobbyRelayService
    {
        private const string Base36Chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const int DefaultUdpPort = 7777;
        public const int LanDiscoveryPort = 47777;
        private const string BeaconPrefix = "TLE_LAN_V1|";

        public static string ActiveRegion { get; private set; } = "LAN / Local Network (UDP Direct)";
        public static int EstimatedPingMs { get; private set; } = 1;

        private static readonly Dictionary<string, DiscoveredLanRoom> _discoveredRooms = new Dictionary<string, DiscoveredLanRoom>();
        public static IReadOnlyDictionary<string, DiscoveredLanRoom> DiscoveredRooms => _discoveredRooms;

        public static void RegisterDiscoveredRoom(string joinCode, string hostIp, int hostPort, int currentPlayers, int maxPlayers, string sceneName, string roomName = "")
        {
            if (string.IsNullOrEmpty(hostIp) || hostPort <= 0) return;
            string key = $"{hostIp}:{hostPort}";
            if (!_discoveredRooms.TryGetValue(key, out DiscoveredLanRoom room))
            {
                room = new DiscoveredLanRoom();
                _discoveredRooms[key] = room;
            }
            room.roomName = string.IsNullOrWhiteSpace(roomName) ? $"ROOM {joinCode}" : roomName.Trim();
            room.joinCode = joinCode ?? "";
            room.hostIp = hostIp;
            room.hostPort = hostPort;
            room.currentPlayers = currentPlayers;
            room.maxPlayers = maxPlayers;
            room.sceneName = sceneName ?? "Lobby_NetCode";
            room.lastSeenRealtime = Time.realtimeSinceStartup;
        }

        public static List<DiscoveredLanRoom> GetActiveLanRooms()
        {
            float now = Time.realtimeSinceStartup;
            List<string> expiredKeys = null;
            List<DiscoveredLanRoom> active = new List<DiscoveredLanRoom>();

            foreach (var kvp in _discoveredRooms)
            {
                if (now - kvp.Value.lastSeenRealtime > 4.5f)
                {
                    if (expiredKeys == null) expiredKeys = new List<string>();
                    expiredKeys.Add(kvp.Key);
                }
                else
                {
                    active.Add(kvp.Value);
                }
            }

            if (expiredKeys != null)
            {
                for (int i = 0; i < expiredKeys.Count; i++)
                {
                    _discoveredRooms.Remove(expiredKeys[i]);
                }
            }

            return active;
        }

        public static byte[] BuildLanBeaconPacket(string joinCode, string hostIp, int hostPort, int currentPlayers, int maxPlayers, string sceneName, string roomName = "")
        {
            string safeRoomName = string.IsNullOrWhiteSpace(roomName) ? $"EMPIRE ROOM [{joinCode}]" : roomName.Replace("|", " ").Trim();
            string msg = $"{BeaconPrefix}{joinCode}|{hostIp}|{hostPort}|{currentPlayers}|{maxPlayers}|{sceneName}|{safeRoomName}";
            return Encoding.UTF8.GetBytes(msg);
        }

        public static bool TryParseLanBeaconPacket(byte[] data, IPEndPoint senderEp, out DiscoveredLanRoom room)
        {
            room = null;
            if (data == null || data.Length < BeaconPrefix.Length) return false;

            try
            {
                string text = Encoding.UTF8.GetString(data);
                if (!text.StartsWith(BeaconPrefix, StringComparison.Ordinal)) return false;

                string[] parts = text.Substring(BeaconPrefix.Length).Split('|');
                if (parts.Length < 6) return false;

                string code = parts[0];
                string ip = senderEp != null && !IPAddress.IsLoopback(senderEp.Address)
                    ? senderEp.Address.ToString()
                    : parts[1];
                if (!int.TryParse(parts[2], out int port)) port = DefaultUdpPort;
                if (!int.TryParse(parts[3], out int curP)) curP = 1;
                if (!int.TryParse(parts[4], out int maxP)) maxP = 6;
                string scene = parts[5];
                string rName = parts.Length >= 7 ? parts[6] : $"EMPIRE ROOM [{code}]";

                RegisterDiscoveredRoom(code, ip, port, curP, maxP, scene, rName);
                _discoveredRooms.TryGetValue($"{ip}:{port}", out room);
                return room != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ดึง IPv4 ในวง LAN ของเครื่องปัจจุบัน (เช่น 192.168.1.45)
        /// </summary>
        public static string GetLocalIPv4()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint endPoint)
                    {
                        return endPoint.Address.ToString();
                    }
                }
            }
            catch { }

            try
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        /// <summary>
        /// เข้ารหัส IP ในวง LAN + Port Offset เป็นรหัสห้อง 6 หลัก (เช่น "0CK4J1")
        /// </summary>
        public static string GenerateSixCharJoinCode(string ipAddress, int port)
        {
            byte b2 = 1;
            byte b3 = 1;
            byte prefixType = 0; // 0 = 127.0.0.1, 1 = 192.168.b2.b3, 2 = 10.0.b2.b3, 3 = Other LAN

            if (!string.IsNullOrEmpty(ipAddress) && IPAddress.TryParse(ipAddress, out IPAddress parsed))
            {
                byte[] bytes = parsed.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    b2 = bytes[2];
                    b3 = bytes[3];
                    if (bytes[0] == 127) prefixType = 0;
                    else if (bytes[0] == 192 && bytes[1] == 168) prefixType = 1;
                    else if (bytes[0] == 10) prefixType = 2;
                    else prefixType = 3;
                }
            }

            uint portOffset = (uint)Mathf.Clamp(port - 7700, 0, 255);
            ulong packed = ((ulong)prefixType << 24) | ((ulong)b2 << 16) | ((ulong)b3 << 8) | (ulong)portOffset;

            char[] code = new char[6];
            ulong temp = packed;
            uint baseLen = (uint)Base36Chars.Length;
            for (int i = 5; i >= 0; i--)
            {
                code[i] = Base36Chars[(int)(temp % baseLen)];
                temp /= baseLen;
            }
            return new string(code);
        }

        /// <summary>
        /// ถอดรหัส Join Code 6 หลัก (หรือค้นหาจากห้อง LAN ที่ตรวจพบอัตโนมัติ หรือสตริง "IP:Port")
        /// </summary>
        public static bool TryResolveJoinTarget(string joinInput, out string resolvedIp, out int resolvedPort)
        {
            resolvedIp = "127.0.0.1";
            resolvedPort = DefaultUdpPort;

            if (string.IsNullOrWhiteSpace(joinInput))
            {
                // หากไม่ได้กรอกอะไรแต่มีห้องในวง LAN ที่ตรวจพบ ให้จอยห้องแรกทันที
                var rooms = GetActiveLanRooms();
                if (rooms.Count > 0)
                {
                    resolvedIp = rooms[0].hostIp;
                    resolvedPort = rooms[0].hostPort;
                }
                return true;
            }

            string clean = joinInput.Trim();

            // 1. ตรวจสอบจากรายการห้องในวง LAN ที่ค้นพบอัตโนมัติก่อน (รองรับทุกวง IP 100%)
            foreach (var kvp in _discoveredRooms)
            {
                if (string.Equals(kvp.Value.joinCode, clean, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedIp = kvp.Value.hostIp;
                    resolvedPort = kvp.Value.hostPort;
                    return true;
                }
            }

            // 2. กรณีระบุเป็น IP:Port หรือ IP ตรงๆ
            if (clean.Contains(".") || clean.Contains(":"))
            {
                string[] parts = clean.Split(':');
                resolvedIp = string.IsNullOrWhiteSpace(parts[0]) ? "127.0.0.1" : parts[0].Trim();
                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int parsedPort) && parsedPort > 0 && parsedPort < 65535)
                {
                    resolvedPort = parsedPort;
                }
                return true;
            }

            // 3. กรณีระบุเป็น Join Code 6 หลัก
            clean = clean.ToUpperInvariant();
            if (clean.Length == 6)
            {
                ulong packed = 0;
                uint baseLen = (uint)Base36Chars.Length;
                for (int i = 0; i < 6; i++)
                {
                    int idx = Base36Chars.IndexOf(clean[i]);
                    if (idx < 0) return false;
                    packed = packed * baseLen + (ulong)idx;
                }

                byte portOffset = (byte)(packed & 0xFF);
                byte b3 = (byte)((packed >> 8) & 0xFF);
                byte b2 = (byte)((packed >> 16) & 0xFF);
                byte prefixType = (byte)((packed >> 24) & 0xFF);

                resolvedPort = 7700 + portOffset;
                switch (prefixType)
                {
                    case 0:
                        resolvedIp = "127.0.0.1";
                        break;
                    case 1:
                        resolvedIp = $"192.168.{b2}.{b3}";
                        break;
                    case 2:
                        resolvedIp = $"10.0.{b2}.{b3}";
                        break;
                    default:
                        resolvedIp = "127.0.0.1";
                        break;
                }
                return true;
            }

            return false;
        }
    }
}
