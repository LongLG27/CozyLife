using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using CozyLife.Core;

namespace CozyLife.Network
{
    public struct NetworkIncomingMessage
    {
        public int clientId;
        public PacketType type;
        public string payload;
    }

    /// <summary>
    /// Máy chủ phòng chơi TCP đa luồng & phát tín hiệu UDP Beacon tìm phòng nội bộ.
    /// </summary>
    public class CozyNetworkServer
    {
        public class ConnectedClient
        {
            public int id;
            public TcpClient client;
            public NetworkStream stream;
            public byte[] readBuffer = new byte[4096];
            public MemoryStream receiveStream = new MemoryStream();
            public PlayerDataSync data;
            public bool isReady = false;
            public float lastActivityTime;
        }

        public string RoomId { get; private set; }
        public string RoomName { get; private set; }
        public int Port { get; private set; }
        public int MaxPlayers { get; private set; }
        public bool IsRunning { get; private set; }

        private TcpListener tcpListener;
        private int nextClientId = 1;
        private readonly Dictionary<int, ConnectedClient> clients = new Dictionary<int, ConnectedClient>();
        private readonly object clientLock = new object();

        private readonly ConcurrentQueue<NetworkIncomingMessage> incomingQueue = new ConcurrentQueue<NetworkIncomingMessage>();
        private readonly ConcurrentQueue<int> disconnectedQueue = new ConcurrentQueue<int>();

        private UdpClient beaconClient;
        private Thread beaconThread;
        private bool isBeaconRunning = false;

        public ConcurrentQueue<NetworkIncomingMessage> IncomingQueue => incomingQueue;
        public ConcurrentQueue<int> DisconnectedQueue => disconnectedQueue;

        public int ConnectedPlayerCount
        {
            get
            {
                lock (clientLock)
                {
                    return clients.Count + 1; // +1 cho Host
                }
            }
        }

        public bool StartServer(string roomId, string roomName, int port = CozyNetworkCodec.DefaultPort, int maxPlayers = 8)
        {
            if (IsRunning)
                StopServer();

            RoomId = string.IsNullOrEmpty(roomId) ? CozyNetworkCodec.GenerateRoomId() : roomId;
            RoomName = string.IsNullOrEmpty(roomName) ? "Thế Giới Cozy" : roomName;
            Port = port;
            MaxPlayers = Mathf.Clamp(maxPlayers, 2, 16);

            try
            {
                tcpListener = new TcpListener(IPAddress.Any, Port);
                tcpListener.Start();
                IsRunning = true;

                tcpListener.BeginAcceptTcpClient(OnClientConnected, null);
                StartBeaconBroadcasting();

                Debug.Log($"[CozyServer] Máy chủ đã mở! Phòng: {RoomId} | Cổng: {Port} | Tối đa: {MaxPlayers} người.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CozyServer] Lỗi khởi động máy chủ tại cổng {Port}: {ex.Message}");
                StopServer();
                return false;
            }
        }

        private void OnClientConnected(IAsyncResult ar)
        {
            if (!IsRunning || tcpListener == null)
                return;

            TcpClient tcpClient = null;
            try
            {
                tcpClient = tcpListener.EndAcceptTcpClient(ar);
            }
            catch (Exception)
            {
                // Listener có thể đã dừng
                return;
            }

            // Tiếp tục lắng nghe kết nối mới
            try
            {
                tcpListener.BeginAcceptTcpClient(OnClientConnected, null);
            }
            catch (Exception) { }

            if (tcpClient == null) return;

            lock (clientLock)
            {
                if (clients.Count + 1 >= MaxPlayers)
                {
                    // Phòng đầy, từ chối kết nối
                    try
                    {
                        var rej = new JoinRoomResponsePacket { success = false, message = "Phòng đã đủ số lượng người chơi tối đa!" };
                        byte[] rejBytes = CozyNetworkCodec.EncodePacket(PacketType.JoinRoomResponse, JsonUtility.ToJson(rej));
                        tcpClient.GetStream().Write(rejBytes, 0, rejBytes.Length);
                        tcpClient.Close();
                    }
                    catch { }
                    return;
                }

                int clientId = ++nextClientId;
                ConnectedClient c = new ConnectedClient
                {
                    id = clientId,
                    client = tcpClient,
                    stream = tcpClient.GetStream(),
                    lastActivityTime = Time.realtimeSinceStartup
                };

                clients[clientId] = c;
                BeginReadFromClient(c);
            }
        }

        private void BeginReadFromClient(ConnectedClient client)
        {
            try
            {
                client.stream.BeginRead(client.readBuffer, 0, client.readBuffer.Length, OnDataReceived, client);
            }
            catch (Exception)
            {
                DisconnectClient(client.id, "Mất kết nối luồng đọc");
            }
        }

        private void OnDataReceived(IAsyncResult ar)
        {
            ConnectedClient client = (ConnectedClient)ar.AsyncState;
            if (!IsRunning || client == null || client.stream == null)
                return;

            int bytesRead = 0;
            try
            {
                bytesRead = client.stream.EndRead(ar);
            }
            catch (Exception)
            {
                bytesRead = 0;
            }

            if (bytesRead <= 0)
            {
                DisconnectClient(client.id, "Khách đã ngắt kết nối");
                return;
            }

            client.lastActivityTime = Time.realtimeSinceStartup;

            lock (client.receiveStream)
            {
                client.receiveStream.Write(client.readBuffer, 0, bytesRead);
                client.receiveStream.Position = 0;

                while (CozyNetworkCodec.TryReadPacket(client.receiveStream, out PacketType type, out string payload))
                {
                    incomingQueue.Enqueue(new NetworkIncomingMessage
                    {
                        clientId = client.id,
                        type = type,
                        payload = payload
                    });
                }
            }

            // Tiếp tục nhận dữ liệu
            BeginReadFromClient(client);
        }

        public void DisconnectClient(int clientId, string reason)
        {
            ConnectedClient client = null;
            lock (clientLock)
            {
                if (clients.TryGetValue(clientId, out client))
                {
                    clients.Remove(clientId);
                }
            }

            if (client != null)
            {
                try { client.stream?.Close(); } catch { }
                try { client.client?.Close(); } catch { }
                disconnectedQueue.Enqueue(clientId);
                Debug.Log($"[CozyServer] Người chơi #{clientId} đã rời: {reason}");
            }
        }

        public void SendTo(int clientId, PacketType type, string json)
        {
            ConnectedClient c = null;
            lock (clientLock)
            {
                clients.TryGetValue(clientId, out c);
            }

            if (c != null && c.stream != null && c.stream.CanWrite)
            {
                try
                {
                    byte[] packet = CozyNetworkCodec.EncodePacket(type, json);
                    c.stream.Write(packet, 0, packet.Length);
                }
                catch (Exception)
                {
                    DisconnectClient(clientId, "Lỗi ghi gói tin");
                }
            }
        }

        public void Broadcast(PacketType type, string json, int excludeClientId = -1)
        {
            byte[] packet = CozyNetworkCodec.EncodePacket(type, json);
            List<ConnectedClient> clientList;
            lock (clientLock)
            {
                clientList = new List<ConnectedClient>(clients.Values);
            }

            foreach (var c in clientList)
            {
                if (c.id == excludeClientId)
                    continue;

                try
                {
                    if (c.stream != null && c.stream.CanWrite)
                    {
                        c.stream.Write(packet, 0, packet.Length);
                    }
                }
                catch (Exception)
                {
                    DisconnectClient(c.id, "Lỗi gửi dữ liệu broadcast");
                }
            }
        }

        public void SetClientData(int clientId, PlayerDataSync data)
        {
            lock (clientLock)
            {
                if (clients.TryGetValue(clientId, out var c))
                {
                    c.data = data;
                    c.isReady = true;
                }
            }
        }

        public List<PlayerDataSync> GetAllConnectedPlayerData()
        {
            List<PlayerDataSync> list = new List<PlayerDataSync>();
            lock (clientLock)
            {
                foreach (var c in clients.Values)
                {
                    if (c.isReady && c.data != null)
                    {
                        list.Add(c.data);
                    }
                }
            }
            return list;
        }

        private void StartBeaconBroadcasting()
        {
            isBeaconRunning = true;
            beaconThread = new Thread(() =>
            {
                try
                {
                    beaconClient = new UdpClient();
                    beaconClient.EnableBroadcast = true;
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, CozyNetworkCodec.BeaconPort);

                    while (isBeaconRunning && IsRunning)
                    {
                        int cur = ConnectedPlayerCount;
                        string msg = CozyNetworkCodec.BuildBeaconMessage(RoomId, RoomName, cur, MaxPlayers, Port);
                        byte[] bytes = Encoding.UTF8.GetBytes(msg);
                        beaconClient.Send(bytes, bytes.Length, endPoint);

                        Thread.Sleep(1500); // Phát mỗi 1.5 giây
                    }
                }
                catch (Exception) { }
                finally
                {
                    try { beaconClient?.Close(); } catch { }
                    beaconClient = null;
                }
            })
            {
                IsBackground = true
            };
            beaconThread.Start();
        }

        public void StopServer()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            isBeaconRunning = false;

            // Thông báo máy chủ đóng
            try
            {
                var notify = new SimpleNotificationPacket { clientId = 0, reason = "Chủ phòng đã đóng phòng chơi." };
                Broadcast(PacketType.LeaveRoom, JsonUtility.ToJson(notify));
            }
            catch { }

            lock (clientLock)
            {
                foreach (var c in clients.Values)
                {
                    try { c.stream?.Close(); } catch { }
                    try { c.client?.Close(); } catch { }
                }
                clients.Clear();
            }

            try { tcpListener?.Stop(); } catch { }
            tcpListener = null;

            try { beaconClient?.Close(); } catch { }
            beaconClient = null;

            Debug.Log("[CozyServer] Máy chủ phòng đã đóng.");
        }
    }
}
