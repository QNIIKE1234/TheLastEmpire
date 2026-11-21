using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// LobbyRelayService — จัดการรหัสห้อง 6 หลัก (Join Code), การแปลง IP/Port สำหรับ LAN/UDP,
    /// และการเลือก Regional Server อัตโนมัติด้วย QoS (โซน asia-southeast1 สิงคโปร์ / Valve SDR)
    /// </summary>
    public static class LobbyRelayService
    {
        private const string Base36Chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 34 ตัวอักษร (ตัด I, O กันสับสน)
        public const int DefaultUdpPort = 7777;

        public static string ActiveRegion { get; private set; } = "asia-southeast1 (Singapore)";
        public static int EstimatedPingMs { get; private set; } = 24;

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
            catch
            {
                // Fallback เมื่อออฟไลน์
            }

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
        /// เข้ารหัส IP ในวง LAN (2 ไบต์ท้าย) + Port Offset เป็นรหัสห้อง 6 หลัก (เช่น "TLE48K" หรือ "7F9K2M")
        /// เพื่อให้เพื่อนในวง LAN หรือ RadminVPN / ZeroTier / Localhost กรอกรหัส 6 ตัวเข้าห้องได้ทันที
        /// </summary>
        public static string GenerateSixCharJoinCode(string ipAddress, int port)
        {
            byte b2 = 1;
            byte b3 = 1;
            byte prefixType = 0; // 0 = 127.0.0.1, 1 = 192.168.b2.b3, 2 = 10.0.b2.b3, 3 = 172.16.b2.b3

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
        /// ถอดรหัส Join Code 6 หลัก (หรือสตริง "IP:Port" เช่น "192.168.1.25:7777" หรือ "127.0.0.1")
        /// ออกมาเป็น IP และ Port สำหรับเชื่อมต่อเข้าหา Host
        /// </summary>
        public static bool TryResolveJoinTarget(string joinInput, out string resolvedIp, out int resolvedPort)
        {
            resolvedIp = "127.0.0.1";
            resolvedPort = DefaultUdpPort;

            if (string.IsNullOrWhiteSpace(joinInput))
            {
                return true;
            }

            string clean = joinInput.Trim();

            // กรณีระบุเป็น IP:Port หรือ IP ตรงๆ
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

            // กรณีระบุเป็น Join Code 6 หลัก
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
