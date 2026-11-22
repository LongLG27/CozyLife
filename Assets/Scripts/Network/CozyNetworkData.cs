using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;

namespace CozyLife.Network
{
    public enum PacketType : byte
    {
        None = 0,
        JoinRoomRequest = 1,
        JoinRoomResponse = 2,
        PlayerJoined = 3,
        PlayerLeft = 4,
        PlayerStateSync = 5,
        WorldStateSync = 6,
        ChatMessage = 7,
        Ping = 8,
        Pong = 9,
        LeaveRoom = 10,
        WerewolfEvent = 20
    }

    [Serializable]
    public class PlayerDataSync
    {
        public int clientId;
        public string playerName;
        public string appearanceJson;
        public int profession;
        public float posX;
        public float posY;
        public float posZ;
        public bool flipX;
        public int walkFrame;
        public bool isMoving;
        public int currentBuilding;
        public string actionTitle;
        public bool isSeated;
        public bool isSleeping;
    }

    [Serializable]
    public class WorldStateSync
    {
        public int day;
        public float hour;
        public int weather;
        public float weatherRemaining;
    }

    [Serializable]
    public class ChatMessagePacket
    {
        public int senderId;
        public string senderName;
        public string message;
        public string timestamp;
    }

    [Serializable]
    public class JoinRoomRequestPacket
    {
        public string roomId;
        public string playerName;
        public string appearanceJson;
        public int profession;
    }

    [Serializable]
    public class JoinRoomResponsePacket
    {
        public bool success;
        public string message;
        public string roomId;
        public int assignedClientId;
        public int worldDay;
        public float worldHour;
        public int currentWeather;
        public List<PlayerDataSync> currentPlayers = new List<PlayerDataSync>();
    }

    [Serializable]
    public class SimpleNotificationPacket
    {
        public int clientId;
        public string reason;
    }

    public class DiscoveredRoom
    {
        public string roomId;
        public string roomName;
        public string hostIp;
        public int port;
        public int currentPlayers;
        public int maxPlayers;
        public float lastSeenTime;

        public bool IsExpired(float timeout = 4.0f)
        {
            return (Time.realtimeSinceStartup - lastSeenTime) > timeout;
        }
    }

    /// <summary>
    /// Các tiện ích mã hóa, giải mã gói tin mạng và phân tích mã phòng / IP.
    /// </summary>
    public static class CozyNetworkCodec
    {
        public const int DefaultPort = 7777;
        public const int BeaconPort = 7778;
        public const string BeaconHeader = "COZY_ROOM_BEACON";

        /// <summary>
        /// Tạo khung gói tin (Frame): [4 byte: Độ dài] + [1 byte: PacketType] + [N byte: UTF-8 Payload]
        /// </summary>
        public static byte[] EncodePacket(PacketType type, string jsonPayload)
        {
            byte[] payloadBytes = string.IsNullOrEmpty(jsonPayload) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(jsonPayload);
            int totalLength = 1 + payloadBytes.Length; // 1 byte type + payload

            byte[] buffer = new byte[4 + totalLength];
            byte[] lenBytes = BitConverter.GetBytes(totalLength);
            Array.Copy(lenBytes, 0, buffer, 0, 4);
            buffer[4] = (byte)type;

            if (payloadBytes.Length > 0)
            {
                Array.Copy(payloadBytes, 0, buffer, 5, payloadBytes.Length);
            }

            return buffer;
        }

        /// <summary>
        /// Đọc một gói tin hoàn chỉnh từ MemoryStream đệm.
        /// Trả về true nếu đọc thành công 1 gói và trích xuất type + jsonPayload.
        /// </summary>
        public static bool TryReadPacket(MemoryStream stream, out PacketType type, out string jsonPayload)
        {
            type = PacketType.None;
            jsonPayload = null;

            if (stream == null || stream.Length < 4)
                return false;

            byte[] buffer;
            int offset = 0;
            if (stream.TryGetBuffer(out ArraySegment<byte> seg))
            {
                buffer = seg.Array;
                offset = seg.Offset;
            }
            else
            {
                buffer = stream.ToArray();
                offset = 0;
            }

            int totalLength = BitConverter.ToInt32(buffer, offset);

            if (totalLength <= 0 || totalLength > 1024 * 1024) // Giới hạn an toàn 1MB
            {
                stream.SetLength(0);
                stream.Position = 0;
                return false;
            }

            if (stream.Length < 4 + totalLength)
                return false; // Chưa đủ dữ liệu gói tin, chờ byte tiếp theo

            type = (PacketType)buffer[offset + 4];
            int payloadLen = totalLength - 1;

            if (payloadLen > 0)
            {
                jsonPayload = Encoding.UTF8.GetString(buffer, offset + 5, payloadLen);
            }
            else
            {
                jsonPayload = "";
            }

            // Dịch chuyển phần còn lại của stream về đầu
            int remaining = (int)stream.Length - (4 + totalLength);
            if (remaining > 0)
            {
                Buffer.BlockCopy(buffer, offset + 4 + totalLength, buffer, offset, remaining);
                stream.SetLength(remaining);
                stream.Position = remaining;
            }
            else
            {
                stream.SetLength(0);
                stream.Position = 0;
            }

            return true;
        }

        /// <summary>
        /// Tạo mã phòng ID ngẫu nhiên dễ nhớ (VD: CZ-4821)
        /// </summary>
        public static string GenerateRoomId()
        {
            int code = UnityEngine.Random.Range(1000, 9999);
            return $"CZ-{code}";
        }

        /// <summary>
        /// Lấy địa chỉ IP nội bộ (LAN IPv4) của máy chủ hiện tại
        /// </summary>
        public static string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception)
            {
                // Fallback nếu không truy xuất được host entry
            }
            return "127.0.0.1";
        }

        /// <summary>
        /// Phân tích thông minh chuỗi người dùng nhập (hỗ trợ mã phòng CZ-xxxx, IP:Port, IP trần, hoặc chuỗi sao chép dài)
        /// </summary>
        public static bool ParseConnectionTarget(string input, out string roomId, out string targetIp, out int targetPort)
        {
            roomId = "";
            targetIp = "127.0.0.1";
            targetPort = DefaultPort;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string clean = input.Trim();

            // 1. Kiểm tra nếu có mã phòng định dạng CZ-xxxx
            var matchRoom = Regex.Match(clean, @"\b(CZ-\d{4,6})\b", RegexOptions.IgnoreCase);
            if (matchRoom.Success)
            {
                roomId = matchRoom.Value.ToUpper();
            }

            // 2. Kiểm tra nếu có chứa IP:Port (VD: 192.168.1.15:7777 hoặc 127.0.0.1:7777)
            var matchIpPort = Regex.Match(clean, @"\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{2,5})\b");
            if (matchIpPort.Success)
            {
                targetIp = matchIpPort.Groups[1].Value;
                if (int.TryParse(matchIpPort.Groups[2].Value, out int p))
                    targetPort = p;
                return true;
            }

            // 3. Kiểm tra IP trần (VD: 192.168.1.15 hoặc 10.0.0.2)
            var matchIp = Regex.Match(clean, @"\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b");
            if (matchIp.Success)
            {
                targetIp = matchIp.Value;
                return true;
            }

            // 4. Nếu người dùng nhập "localhost"
            if (clean.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetIp = "127.0.0.1";
                return true;
            }

            // 5. Nếu chỉ nhập mã phòng ID (VD: CZ-4821)
            if (!string.IsNullOrEmpty(roomId))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tạo nội dung gói tin UDP Beacon phát hiện phòng trong mạng LAN
        /// </summary>
        public static string BuildBeaconMessage(string roomId, string roomName, int curPlayers, int maxPlayers, int port)
        {
            return $"{BeaconHeader}|{roomId}|{roomName}|{curPlayers}|{maxPlayers}|{port}";
        }

        /// <summary>
        /// Giải mã gói tin UDP Beacon phát hiện phòng
        /// </summary>
        public static bool TryParseBeacon(string raw, string senderIp, out DiscoveredRoom room)
        {
            room = null;
            if (string.IsNullOrEmpty(raw) || !raw.StartsWith(BeaconHeader))
                return false;

            string[] parts = raw.Split('|');
            if (parts.Length < 6)
                return false;

            string roomId = parts[1];
            string roomName = parts[2];
            int.TryParse(parts[3], out int cur);
            int.TryParse(parts[4], out int max);
            int.TryParse(parts[5], out int port);
            if (port <= 0) port = DefaultPort;

            room = new DiscoveredRoom
            {
                roomId = roomId,
                roomName = roomName,
                hostIp = senderIp,
                port = port,
                currentPlayers = cur,
                maxPlayers = max,
                lastSeenTime = Time.realtimeSinceStartup
            };

            return true;
        }
    }
}
