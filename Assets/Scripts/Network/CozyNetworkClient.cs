using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CozyLife.Network
{
    /// <summary>
    /// Máy khách TCP xử lý gửi/nhận dữ liệu với máy chủ phòng và lắng nghe phát hiện phòng LAN.
    /// </summary>
    public class CozyNetworkClient
    {
        public bool IsConnected => tcpClient != null && tcpClient.Connected && isRunning;
        public int AssignedClientId { get; set; } = -1;
        public string TargetRoomId { get; set; } = "";

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private byte[] readBuffer = new byte[4096];
        private MemoryStream receiveStream = new MemoryStream();
        private bool isRunning = false;

        private readonly ConcurrentQueue<NetworkIncomingMessage> incomingQueue = new ConcurrentQueue<NetworkIncomingMessage>();
        public ConcurrentQueue<NetworkIncomingMessage> IncomingQueue => incomingQueue;

        // UDP Beacon Listener phát hiện phòng trong mạng nội bộ
        private UdpClient beaconListener;
        private Thread beaconListenerThread;
        private bool isBeaconListening = false;
        private readonly Dictionary<string, DiscoveredRoom> discoveredRooms = new Dictionary<string, DiscoveredRoom>();
        private readonly object roomLock = new object();

        public List<DiscoveredRoom> GetDiscoveredRooms()
        {
            lock (roomLock)
            {
                List<DiscoveredRoom> list = new List<DiscoveredRoom>();
                List<string> expiredKeys = new List<string>();

                foreach (var kvp in discoveredRooms)
                {
                    if (kvp.Value.IsExpired(4.5f))
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                    else
                    {
                        list.Add(kvp.Value);
                    }
                }

                foreach (var exp in expiredKeys)
                {
                    discoveredRooms.Remove(exp);
                }

                return list;
            }
        }

        public void StartBeaconListening()
        {
            if (isBeaconListening) return;

            isBeaconListening = true;
            beaconListenerThread = new Thread(() =>
            {
                try
                {
                    beaconListener = new UdpClient();
                    beaconListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    beaconListener.Client.Bind(new IPEndPoint(IPAddress.Any, CozyNetworkCodec.BeaconPort));

                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                    while (isBeaconListening)
                    {
                        try
                        {
                            byte[] data = beaconListener.Receive(ref remoteEP);
                            string text = Encoding.UTF8.GetString(data);

                            if (CozyNetworkCodec.TryParseBeacon(text, remoteEP.Address.ToString(), out DiscoveredRoom room))
                            {
                                lock (roomLock)
                                {
                                    discoveredRooms[room.roomId] = room;
                                }
                            }
                        }
                        catch (SocketException) { break; }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
                finally
                {
                    try { beaconListener?.Close(); } catch { }
                    beaconListener = null;
                }
            })
            {
                IsBackground = true
            };
            beaconListenerThread.Start();
        }

        public void StopBeaconListening()
        {
            isBeaconListening = false;
            try { beaconListener?.Close(); } catch { }
            beaconListener = null;
        }

        public void Connect(string hostIp, int port, Action<bool, string> onComplete)
        {
            Disconnect();

            new Thread(() =>
            {
                try
                {
                    tcpClient = new TcpClient();
                    var result = tcpClient.BeginConnect(hostIp, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(4000, true); // Chờ tối đa 4 giây

                    if (!success || !tcpClient.Connected)
                    {
                        try { tcpClient.Close(); } catch { }
                        tcpClient = null;
                        onComplete?.Invoke(false, $"Không thể kết nối đến máy chủ {hostIp}:{port} (Hết thời gian chờ).");
                        return;
                    }

                    tcpClient.EndConnect(result);
                    networkStream = tcpClient.GetStream();
                    isRunning = true;

                    BeginRead();
                    onComplete?.Invoke(true, "Kết nối TCP thành công!");
                }
                catch (Exception ex)
                {
                    Disconnect();
                    onComplete?.Invoke(false, $"Lỗi kết nối: {ex.Message}");
                }
            })
            {
                IsBackground = true
            }.Start();
        }

        private void BeginRead()
        {
            if (!isRunning || networkStream == null || !networkStream.CanRead)
                return;

            try
            {
                networkStream.BeginRead(readBuffer, 0, readBuffer.Length, OnDataReceived, null);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private void OnDataReceived(IAsyncResult ar)
        {
            if (!isRunning || networkStream == null)
                return;

            int bytesRead = 0;
            try
            {
                bytesRead = networkStream.EndRead(ar);
            }
            catch (Exception)
            {
                bytesRead = 0;
            }

            if (bytesRead <= 0)
            {
                incomingQueue.Enqueue(new NetworkIncomingMessage
                {
                    clientId = AssignedClientId,
                    type = PacketType.LeaveRoom,
                    payload = "Mất kết nối với máy chủ phòng."
                });
                Disconnect();
                return;
            }

            lock (receiveStream)
            {
                receiveStream.Write(readBuffer, 0, bytesRead);
                receiveStream.Position = 0;

                while (CozyNetworkCodec.TryReadPacket(receiveStream, out PacketType type, out string payload))
                {
                    incomingQueue.Enqueue(new NetworkIncomingMessage
                    {
                        clientId = AssignedClientId,
                        type = type,
                        payload = payload
                    });
                }
            }

            BeginRead();
        }

        public void SendPacket(PacketType type, string jsonPayload)
        {
            if (!IsConnected || networkStream == null || !networkStream.CanWrite)
                return;

            try
            {
                byte[] packet = CozyNetworkCodec.EncodePacket(type, jsonPayload);
                networkStream.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CozyClient] Lỗi gửi gói {type}: {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            isRunning = false;
            try { networkStream?.Close(); } catch { }
            networkStream = null;

            try { tcpClient?.Close(); } catch { }
            tcpClient = null;

            AssignedClientId = -1;
        }
    }
}
