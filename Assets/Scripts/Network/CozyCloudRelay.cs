using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CozyLife.Network
{
    public struct CloudPacketMessage
    {
        public string topic;
        public PacketType type;
        public string payload;
    }

    /// <summary>
    /// Bộ chuyển tiếp đám mây toàn cầu (Global Cloud Relay qua MQTT):
    /// - Cho phép người chơi ở KHÁC MẠNG (4G, Wi-Fi khác nhau, khác nhà mạng/tỉnh thành)
    ///   kết nối với nhau 100% thoải mái CHỈ CẦN MÃ ID PHÒNG.
    /// - KHÔNG cần mở cổng Router (No Port Forwarding), KHÔNG cần cài VPN (No Hamachi/Radmin).
    /// - Tự động phát hiện phòng online trên toàn cầu (Global Lobby Discovery).
    /// </summary>
    public class CozyCloudRelay
    {
        private static readonly string[] PublicBrokers = new string[]
        {
            "broker.hivemq.com",
            "broker.emqx.io"
        };
        private const int BrokerPort = 1883;

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private Thread receiveThread;
        private Thread pingThread;
        private bool isRunning = false;

        private readonly ConcurrentQueue<CloudPacketMessage> incomingPackets = new ConcurrentQueue<CloudPacketMessage>();
        private readonly ConcurrentQueue<DiscoveredRoom> discoveredOnlineRooms = new ConcurrentQueue<DiscoveredRoom>();
        private readonly Dictionary<string, DiscoveredRoom> activeRoomsCache = new Dictionary<string, DiscoveredRoom>();
        private readonly object cacheLock = new object();

        private string currentRoomId = "";
        private bool isHost = false;
        private string clientId = "";
        private ushort packetIdCounter = 1;

        public bool IsConnected => isRunning && tcpClient != null && tcpClient.Connected;
        public ConcurrentQueue<CloudPacketMessage> IncomingPackets => incomingPackets;
        public ConcurrentQueue<DiscoveredRoom> DiscoveredOnlineRooms => discoveredOnlineRooms;

        public List<DiscoveredRoom> GetActiveOnlineRooms()
        {
            lock (cacheLock)
            {
                // Xử lý các phòng mới nhận được từ queue
                while (discoveredOnlineRooms.TryDequeue(out var room))
                {
                    activeRoomsCache[room.roomId] = room;
                }

                List<DiscoveredRoom> list = new List<DiscoveredRoom>();
                List<string> expired = new List<string>();

                foreach (var kvp in activeRoomsCache)
                {
                    if (kvp.Value.IsExpired(7.0f))
                    {
                        expired.Add(kvp.Key);
                    }
                    else
                    {
                        list.Add(kvp.Value);
                    }
                }

                foreach (var key in expired)
                {
                    activeRoomsCache.Remove(key);
                }

                return list;
            }
        }

        public void ConnectToCloud(Action<bool, string> onComplete)
        {
            if (IsConnected)
            {
                onComplete?.Invoke(true, "Đã kết nối Cloud Relay.");
                return;
            }

            Disconnect();

            new Thread(() =>
            {
                bool success = false;
                string lastError = "";

                foreach (var broker in PublicBrokers)
                {
                    try
                    {
                        tcpClient = new TcpClient();
                        var ar = tcpClient.BeginConnect(broker, BrokerPort, null, null);
                        bool ok = ar.AsyncWaitHandle.WaitOne(4000, true);

                        if (!ok || !tcpClient.Connected)
                        {
                            try { tcpClient.Close(); } catch { }
                            lastError = $"Không thể kết nối đến {broker}";
                            continue;
                        }

                        tcpClient.EndConnect(ar);
                        networkStream = tcpClient.GetStream();

                        // 1. Gửi gói tin CONNECT chuẩn MQTT
                        clientId = $"CozyLife_{Guid.NewGuid():N}".Substring(0, 20);
                        byte[] connectPacket = BuildConnectPacket(clientId);
                        networkStream.Write(connectPacket, 0, connectPacket.Length);

                        // 2. Chờ nhận CONNACK (4 byte)
                        byte[] connack = new byte[4];
                        int r = networkStream.Read(connack, 0, 4);
                        if (r >= 4 && connack[0] == 0x20 && connack[3] == 0x00)
                        {
                            isRunning = true;
                            success = true;
                            Debug.Log($"[CozyCloud] Đã kết nối thành công tới Đám Mây Toàn Cầu ({broker}:1883)!");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }
                }

                if (!success)
                {
                    Disconnect();
                    onComplete?.Invoke(false, $"Không thể kết nối tới máy chủ Đám Mây: {lastError}");
                    return;
                }

                // 3. Khởi chạy luồng nhận dữ liệu & luồng giữ kết nối (Keep-Alive Ping)
                StartReceiveLoop();
                StartPingLoop();

                // 4. Luôn lắng nghe kênh sảnh thế giới để phát hiện các phòng online đang mở
                SubscribeTopic("cozylife/lobby/announce");

                onComplete?.Invoke(true, "Kết nối Cloud Relay thành công!");
            })
            {
                IsBackground = true
            }.Start();
        }

        public void JoinRoomChannel(string roomId, bool asHost)
        {
            currentRoomId = roomId;
            isHost = asHost;

            if (!IsConnected) return;

            // Nếu là Host: Lắng nghe kênh C2S (Client gửi tới Server)
            // Nếu là Client: Lắng nghe kênh S2C (Server gửi tới Client)
            string subTopic = asHost ? $"cozylife/rooms/{roomId}/c2s" : $"cozylife/rooms/{roomId}/s2c";
            SubscribeTopic(subTopic);
            Debug.Log($"[CozyCloud] Đã gia nhập kênh phòng: {subTopic}");
        }

        public void SendPacketToRoom(PacketType type, string jsonPayload)
        {
            if (!IsConnected || string.IsNullOrEmpty(currentRoomId)) return;

            // Host gửi lên kênh s2c, Client gửi lên kênh c2s
            string targetTopic = isHost ? $"cozylife/rooms/{currentRoomId}/s2c" : $"cozylife/rooms/{currentRoomId}/c2s";

            // Định dạng payload: [1 byte PacketType] + [UTF-8 json]
            byte[] jsonBytes = string.IsNullOrEmpty(jsonPayload) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(jsonPayload);
            byte[] fullPayload = new byte[1 + jsonBytes.Length];
            fullPayload[0] = (byte)type;
            if (jsonBytes.Length > 0)
            {
                Array.Copy(jsonBytes, 0, fullPayload, 1, jsonBytes.Length);
            }

            PublishTopic(targetTopic, fullPayload);
        }

        public void PublishLobbyAnnounce(string roomId, string roomName, int curPlayers, int maxPlayers)
        {
            if (!IsConnected) return;

            string msg = $"{CozyNetworkCodec.BeaconHeader}|{roomId}|{roomName}|{curPlayers}|{maxPlayers}|{CozyNetworkCodec.DefaultPort}";
            byte[] payload = Encoding.UTF8.GetBytes(msg);
            PublishTopic("cozylife/lobby/announce", payload);
        }

        private void SubscribeTopic(string topic)
        {
            if (!IsConnected || networkStream == null) return;

            try
            {
                byte[] subPacket = BuildSubscribePacket(++packetIdCounter, topic);
                lock (networkStream)
                {
                    networkStream.Write(subPacket, 0, subPacket.Length);
                }
            }
            catch (Exception) { }
        }

        private void PublishTopic(string topic, byte[] payload)
        {
            if (!IsConnected || networkStream == null) return;

            try
            {
                byte[] pubPacket = BuildPublishPacket(topic, payload);
                lock (networkStream)
                {
                    networkStream.Write(pubPacket, 0, pubPacket.Length);
                }
            }
            catch (Exception) { }
        }

        private void StartReceiveLoop()
        {
            receiveThread = new Thread(() =>
            {
                byte[] readBuf = new byte[8192];
                while (isRunning && networkStream != null)
                {
                    try
                    {
                        int packetHeader = networkStream.ReadByte();
                        if (packetHeader < 0) break; // Stream closed

                        byte headerByte = (byte)packetHeader;
                        byte packetType = (byte)(headerByte & 0xF0);

                        int remainingLen = ReadRemainingLength(networkStream);

                        if (packetType == 0x30) // PUBLISH
                        {
                            // Đọc độ dài Topic (2 byte big-endian)
                            int tLenHigh = networkStream.ReadByte();
                            int tLenLow = networkStream.ReadByte();
                            int topicLen = (tLenHigh << 8) | tLenLow;

                            byte[] topicBytes = ReadExactBytes(networkStream, topicLen);
                            string topic = Encoding.UTF8.GetString(topicBytes);

                            int payloadLen = remainingLen - 2 - topicLen;
                            byte[] payloadBytes = ReadExactBytes(networkStream, payloadLen);

                            HandleReceivedMqttMessage(topic, payloadBytes);
                        }
                        else
                        {
                            // Bỏ qua payload của các gói tin khác (SUBACK, PINGRESP, etc.)
                            if (remainingLen > 0)
                            {
                                ReadExactBytes(networkStream, remainingLen);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }

                Disconnect();
            })
            {
                IsBackground = true
            };
            receiveThread.Start();
        }

        private void HandleReceivedMqttMessage(string topic, byte[] data)
        {
            if (data == null || data.Length == 0) return;

            // 1. Nếu là thông báo sảnh toàn cầu
            if (topic.Equals("cozylife/lobby/announce", StringComparison.OrdinalIgnoreCase))
            {
                string text = Encoding.UTF8.GetString(data);
                if (CozyNetworkCodec.TryParseBeacon(text, "Cloud", out DiscoveredRoom room))
                {
                    discoveredOnlineRooms.Enqueue(room);
                }
                return;
            }

            // 2. Nếu là gói tin trong phòng
            PacketType pType = (PacketType)data[0];
            string json = "";
            if (data.Length > 1)
            {
                json = Encoding.UTF8.GetString(data, 1, data.Length - 1);
            }

            incomingPackets.Enqueue(new CloudPacketMessage
            {
                topic = topic,
                type = pType,
                payload = json
            });
        }

        private void StartPingLoop()
        {
            pingThread = new Thread(() =>
            {
                byte[] pingReq = new byte[] { 0xC0, 0x00 };
                while (isRunning && networkStream != null)
                {
                    try
                    {
                        Thread.Sleep(20000); // Gửi Ping mỗi 20 giây
                        if (isRunning && networkStream != null && networkStream.CanWrite)
                        {
                            lock (networkStream)
                            {
                                networkStream.Write(pingReq, 0, 2);
                            }
                        }
                    }
                    catch (Exception) { break; }
                }
            })
            {
                IsBackground = true
            };
            pingThread.Start();
        }

        public void Disconnect()
        {
            isRunning = false;
            try { networkStream?.Close(); } catch { }
            networkStream = null;

            try { tcpClient?.Close(); } catch { }
            tcpClient = null;

            currentRoomId = "";
        }

        #region MQTT Protocol Helpers
        private static byte[] BuildConnectPacket(string cId)
        {
            var ms = new MemoryStream();
            ms.Write(new byte[] { 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C }, 0, 10);

            byte[] cidBytes = Encoding.UTF8.GetBytes(cId);
            ms.WriteByte((byte)(cidBytes.Length >> 8));
            ms.WriteByte((byte)(cidBytes.Length & 0xFF));
            ms.Write(cidBytes, 0, cidBytes.Length);

            byte[] body = ms.ToArray();
            var packet = new MemoryStream();
            packet.WriteByte(0x10); // CONNECT
            WriteRemainingLength(packet, body.Length);
            packet.Write(body, 0, body.Length);
            return packet.ToArray();
        }

        private static byte[] BuildSubscribePacket(ushort packetId, string topic)
        {
            var ms = new MemoryStream();
            ms.WriteByte((byte)(packetId >> 8));
            ms.WriteByte((byte)(packetId & 0xFF));

            byte[] tBytes = Encoding.UTF8.GetBytes(topic);
            ms.WriteByte((byte)(tBytes.Length >> 8));
            ms.WriteByte((byte)(tBytes.Length & 0xFF));
            ms.Write(tBytes, 0, tBytes.Length);
            ms.WriteByte(0x00); // QoS 0

            byte[] body = ms.ToArray();
            var packet = new MemoryStream();
            packet.WriteByte(0x82); // SUBSCRIBE
            WriteRemainingLength(packet, body.Length);
            packet.Write(body, 0, body.Length);
            return packet.ToArray();
        }

        private static byte[] BuildPublishPacket(string topic, byte[] payload)
        {
            byte[] tBytes = Encoding.UTF8.GetBytes(topic);
            int bodyLen = 2 + tBytes.Length + (payload != null ? payload.Length : 0);

            var packet = new MemoryStream();
            packet.WriteByte(0x30); // PUBLISH (QoS 0)
            WriteRemainingLength(packet, bodyLen);
            packet.WriteByte((byte)(tBytes.Length >> 8));
            packet.WriteByte((byte)(tBytes.Length & 0xFF));
            packet.Write(tBytes, 0, tBytes.Length);
            if (payload != null && payload.Length > 0)
            {
                packet.Write(payload, 0, payload.Length);
            }
            return packet.ToArray();
        }

        private static void WriteRemainingLength(Stream s, int length)
        {
            do
            {
                byte digit = (byte)(length % 128);
                length /= 128;
                if (length > 0)
                    digit |= 0x80;
                s.WriteByte(digit);
            } while (length > 0);
        }

        private static int ReadRemainingLength(Stream s)
        {
            int multiplier = 1;
            int value = 0;
            byte digit;
            do
            {
                int b = s.ReadByte();
                if (b < 0) throw new IOException("Unexpected end of stream");
                digit = (byte)b;
                value += (digit & 0x7F) * multiplier;
                multiplier *= 128;
            } while ((digit & 0x80) != 0);
            return value;
        }

        private static byte[] ReadExactBytes(Stream s, int length)
        {
            byte[] buf = new byte[length];
            int total = 0;
            while (total < length)
            {
                int r = s.Read(buf, total, length - total);
                if (r <= 0) throw new IOException("Unexpected end of stream");
                total += r;
            }
            return buf;
        }
        #endregion
    }
}
