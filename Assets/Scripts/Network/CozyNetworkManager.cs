using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.World;
using CozyLife.UI;

namespace CozyLife.Network
{
    public struct ChatLogEntry
    {
        public string senderName;
        public string message;
        public string timeStr;
        public Color nameColor;
    }

    /// <summary>
    /// Bộ điều phối Mạng Online trung tâm (Network Manager):
    /// - Hỗ trợ cả ĐÁM MÂY TOÀN CẦU (Cloud Relay - Khác mạng, 4G, xa xôi đều vào được)
    ///   lẫn MẠNG NỘI BỘ (Local LAN TCP).
    /// - Quản lý Trạng thái Tạo Phòng (Host) / Vào Phòng (Client) CHỈ VỚI MÃ ID.
    /// - Đồng bộ vị trí, ngoại trang, biểu cảm, cử chỉ, phân khu thế giới thời gian thực.
    /// - Tích hợp Hệ thống Trò chuyện (Chat Bar & Floating Speech Bubble).
    /// </summary>
    public class CozyNetworkManager : MonoBehaviour
    {
        public static CozyNetworkManager Instance { get; private set; }

        public static bool IsOnline => Instance != null && Instance.isOnlineActive;
        public static bool IsHost => Instance != null && Instance.isHostActive;
        public static string CurrentRoomId => Instance != null ? Instance.currentRoomId : "";
        public static bool IsChatFocused => Instance != null && Instance.isChatInputActive;

        [Header("Network State")]
        [SerializeField] private bool isOnlineActive = false;
        [SerializeField] private bool isHostActive = false;
        [SerializeField] private string currentRoomId = "";
        [SerializeField] private bool isUsingCloudRelay = false;

        private CozyNetworkServer server;
        private CozyNetworkClient client;
        private CozyCloudRelay cloudRelay;

        private readonly Dictionary<int, RemoteNetworkPlayer> remotePlayers = new Dictionary<int, RemoteNetworkPlayer>();
        private GameObject remotePlayerContainer;

        private float syncTimer = 0f;
        private const float SyncInterval = 0.05f; // 20 lần / giây (20 Hz)
        private float worldSyncTimer = 0f;
        private const float WorldSyncInterval = 3.0f; // Đồng bộ giờ & thời tiết mỗi 3 giây
        private float cloudAnnounceTimer = 0f;
        private int nextCloudClientId = 2;
        private string currentRoomName = "Thế Giới Cozy";
        private int currentMaxPlayers = 8;

        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> mainThreadActions = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        public void EnqueueMainThread(Action action)
        {
            if (action != null)
                mainThreadActions.Enqueue(action);
        }

        public static event Action<string> OnWerewolfPacketReceived;

        public void BroadcastWerewolfEvent(string jsonPayload)
        {
            if (isHostActive)
            {
                server?.Broadcast(PacketType.WerewolfEvent, jsonPayload);
                if (isUsingCloudRelay && cloudRelay != null && cloudRelay.IsConnected)
                {
                    cloudRelay.SendPacketToRoom(PacketType.WerewolfEvent, jsonPayload);
                }
                OnWerewolfPacketReceived?.Invoke(jsonPayload);
            }
            else if (isOnlineActive)
            {
                if (isUsingCloudRelay && cloudRelay != null && cloudRelay.IsConnected)
                {
                    cloudRelay.SendPacketToRoom(PacketType.WerewolfEvent, jsonPayload);
                }
                else if (client != null && client.IsConnected)
                {
                    client.SendPacket(PacketType.WerewolfEvent, jsonPayload);
                }
            }
        }

        // Hệ thống Chat trong game
        private bool isChatInputActive = false;
        private string chatInputText = "";
        private readonly List<ChatLogEntry> chatHistory = new List<ChatLogEntry>();
        private const int MaxChatHistory = 15;
        private Vector2 chatScrollPos = Vector2.zero;
        private bool showRoomDetailsModal = false;

        // IMGUI Styles
        private Texture2D whiteTex;
        private GUIStyle roomBadgeStyle;
        private GUIStyle chatBoxStyle;
        private GUIStyle chatInputStyle;
        private GUIStyle chatItemStyle;
        private GUIStyle buttonStyle;
        private GUIStyle modalTitleStyle;
        private bool stylesInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            server = new CozyNetworkServer();
            client = new CozyNetworkClient();
            cloudRelay = new CozyCloudRelay();
        }

        private void Start()
        {
            // Bắt đầu lắng nghe phòng trong mạng LAN
            client.StartBeaconListening();

            // Kết nối sẵn tới Đám Mây Toàn Cầu để sẵn sàng tìm phòng và nhận phòng online
            cloudRelay.ConnectToCloud((ok, msg) =>
            {
                if (ok)
                    Debug.Log("[CozyNetwork] Đám Mây Toàn Cầu sẵn sàng hoạt động!");
            });
        }

        private void OnDestroy()
        {
            LeaveRoom();
            client.StopBeaconListening();
            cloudRelay.Disconnect();
        }

        /// <summary>
        /// Lấy toàn bộ danh sách phòng khả dụng (Cả phòng Online Toàn Cầu lẫn phòng trong mạng LAN)
        /// </summary>
        public List<DiscoveredRoom> GetAllAvailableRooms()
        {
            var list = new List<DiscoveredRoom>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (cloudRelay != null)
            {
                foreach (var r in cloudRelay.GetActiveOnlineRooms())
                {
                    if (seen.Add(r.roomId))
                        list.Add(r);
                }
            }

            if (client != null)
            {
                foreach (var r in client.GetDiscoveredRooms())
                {
                    if (seen.Add(r.roomId))
                        list.Add(r);
                }
            }

            return list;
        }

        public List<DiscoveredRoom> GetDiscoveredRooms()
        {
            return GetAllAvailableRooms();
        }

        #region Host Room
        public bool StartHostRoom(string preferredRoomId, string roomName, int port = CozyNetworkCodec.DefaultPort, int maxPlayers = 8)
        {
            LeaveRoom();

            string roomId = string.IsNullOrEmpty(preferredRoomId) ? CozyNetworkCodec.GenerateRoomId() : preferredRoomId;
            currentRoomName = string.IsNullOrEmpty(roomName) ? "Thế Giới Cozy" : roomName;
            currentMaxPlayers = maxPlayers;

            // Khởi động server máy chủ nội bộ
            bool ok = server.StartServer(roomId, currentRoomName, port, maxPlayers);
            if (!ok)
            {
                CozyLifeHUD.PushToast($"❌ Không thể mở máy chủ tại cổng {port}!");
                return false;
            }

            isHostActive = true;
            currentRoomId = server.RoomId;
            nextCloudClientId = 2;
            isUsingCloudRelay = true;

            // Mở kênh trên Đám Mây Toàn Cầu để người chơi KHÁC MẠNG có thể vào tự do
            cloudRelay.ConnectToCloud((success, msg) =>
            {
                EnqueueMainThread(() =>
                {
                    if (success)
                    {
                        cloudRelay.JoinRoomChannel(currentRoomId, asHost: true);
                        cloudRelay.PublishLobbyAnnounce(currentRoomId, currentRoomName, 1, currentMaxPlayers);
                        Debug.Log($"[CozyNetwork] Phòng [{currentRoomId}] đã phát sóng Online Toàn Cầu!");
                    }
                });
            });

            // Kết nối client loopback cho chính Host
            client.TargetRoomId = currentRoomId;
            client.AssignedClientId = 1;
            client.Connect("127.0.0.1", port, (success, msg) =>
            {
                EnqueueMainThread(() =>
                {
                    if (success)
                    {
                        var appearance = CharacterAppearanceData.LoadFromPrefs();
                        var req = new JoinRoomRequestPacket
                        {
                            roomId = currentRoomId,
                            playerName = appearance.characterName,
                            appearanceJson = JsonUtility.ToJson(appearance),
                            profession = (int)appearance.starterProfession
                        };

                        client.SendPacket(PacketType.JoinRoomRequest, JsonUtility.ToJson(req));
                    }
                });
            });

            isOnlineActive = true;
            CozyLifeHUD.PushToast($"🌐 Đã tạo phòng Online! Mã ID: {currentRoomId}");
            AddSystemChatMessage($"🌐 Phòng [{currentRoomId}] đã mở online toàn cầu! Bạn bè ở bất kỳ đâu chỉ cần nhập mã ID là vào được.");
            return true;
        }
        #endregion

        #region Join Room
        public void JoinRoomByTarget(string targetInput, Action<bool, string> onComplete)
        {
            LeaveRoom();

            if (!CozyNetworkCodec.ParseConnectionTarget(targetInput, out string parsedRoomId, out string targetIp, out int targetPort))
            {
                onComplete?.Invoke(false, "Mã phòng hoặc địa chỉ IP không hợp lệ!");
                return;
            }

            bool isPureRoomCode = !string.IsNullOrEmpty(parsedRoomId) && (string.IsNullOrEmpty(targetIp) || targetIp == "127.0.0.1");

            // Kiểm tra xem phòng có trên LAN nội bộ không
            DiscoveredRoom lanMatch = null;
            if (client != null && !string.IsNullOrEmpty(parsedRoomId))
            {
                foreach (var r in client.GetDiscoveredRooms())
                {
                    if (r.roomId.Equals(parsedRoomId, StringComparison.OrdinalIgnoreCase) && r.hostIp != "Cloud")
                    {
                        lanMatch = r;
                        break;
                    }
                }
            }

            if (lanMatch != null)
            {
                targetIp = lanMatch.hostIp;
                targetPort = lanMatch.port;
                isPureRoomCode = false;
            }

            if (isPureRoomCode)
            {
                // KẾT NỐI QUA ĐÁM MÂY TOÀN CẦU (Khác mạng, 4G, không cần VPN hay Port Forward)
                currentRoomId = parsedRoomId;
                isUsingCloudRelay = true;

                cloudRelay.ConnectToCloud((success, msg) =>
                {
                    EnqueueMainThread(() =>
                    {
                        if (!success)
                        {
                            onComplete?.Invoke(false, $"Lỗi kết nối Đám Mây: {msg}");
                            return;
                        }

                        cloudRelay.JoinRoomChannel(parsedRoomId, asHost: false);

                        var appearance = CharacterAppearanceData.LoadFromPrefs();
                        var req = new JoinRoomRequestPacket
                        {
                            roomId = parsedRoomId,
                            playerName = appearance.characterName,
                            appearanceJson = JsonUtility.ToJson(appearance),
                            profession = (int)appearance.starterProfession
                        };

                        cloudRelay.SendPacketToRoom(PacketType.JoinRoomRequest, JsonUtility.ToJson(req));
                        onComplete?.Invoke(true, "Đang vào phòng qua Đám Mây Toàn Cầu...");
                    });
                });
            }
            else
            {
                // KẾT NỐI TRỰC TIẾP QUA TCP (Mạng nội bộ LAN hoặc IP máy chủ chỉ định)
                isUsingCloudRelay = false;
                client.TargetRoomId = parsedRoomId;
                client.Connect(targetIp, targetPort, (success, msg) =>
                {
                    EnqueueMainThread(() =>
                    {
                        if (!success)
                        {
                            onComplete?.Invoke(false, msg);
                            return;
                        }

                        var appearance = CharacterAppearanceData.LoadFromPrefs();
                        var req = new JoinRoomRequestPacket
                        {
                            roomId = parsedRoomId,
                            playerName = appearance.characterName,
                            appearanceJson = JsonUtility.ToJson(appearance),
                            profession = (int)appearance.starterProfession
                        };

                        client.SendPacket(PacketType.JoinRoomRequest, JsonUtility.ToJson(req));
                        onComplete?.Invoke(true, "Đang đồng bộ thế giới...");
                    });
                });
            }
        }
        #endregion

        #region Disconnect & Clean up
        public void LeaveRoom()
        {
            if (isUsingCloudRelay && cloudRelay != null && cloudRelay.IsConnected)
            {
                try
                {
                    int myId = client != null ? client.AssignedClientId : 0;
                    var notify = new SimpleNotificationPacket { clientId = myId, reason = "Rời phòng tự nguyện" };
                    cloudRelay.SendPacketToRoom(PacketType.LeaveRoom, JsonUtility.ToJson(notify));
                }
                catch { }
            }

            if (client != null && client.IsConnected)
            {
                try
                {
                    var notify = new SimpleNotificationPacket { clientId = client.AssignedClientId, reason = "Rời phòng tự nguyện" };
                    client.SendPacket(PacketType.LeaveRoom, JsonUtility.ToJson(notify));
                }
                catch { }
                client.Disconnect();
            }

            if (server != null && server.IsRunning)
            {
                server.StopServer();
            }

            isOnlineActive = false;
            isHostActive = false;
            isUsingCloudRelay = false;
            currentRoomId = "";

            ClearAllRemotePlayers();
        }

        private void ClearAllRemotePlayers()
        {
            foreach (var p in remotePlayers.Values)
            {
                if (p != null)
                {
                    Destroy(p.gameObject);
                }
            }
            remotePlayers.Clear();
        }
        #endregion

        #region Main Thread Update Loop
        private void Update()
        {
            while (mainThreadActions.TryDequeue(out var act))
            {
                try { act?.Invoke(); } catch (Exception ex) { Debug.LogError($"[CozyNetwork] Lỗi action: {ex}"); }
            }

            ProcessServerIncomingMessages();
            ProcessClientIncomingMessages();
            ProcessCloudIncomingMessages();

            if (!isOnlineActive)
                return;

            HandleKeyboardChatShortcuts();

            // 1. Chu kỳ gửi dữ liệu người chơi nội bộ lên máy chủ (20 Hz)
            syncTimer += Time.deltaTime;
            if (syncTimer >= SyncInterval)
            {
                syncTimer = 0f;
                BroadcastLocalPlayerState();
            }

            // 2. Nếu là Host: Định kỳ gửi đồng bộ Thế giới & phát sóng sảnh Online
            if (isHostActive)
            {
                worldSyncTimer += Time.deltaTime;
                if (worldSyncTimer >= WorldSyncInterval)
                {
                    worldSyncTimer = 0f;
                    BroadcastWorldStateFromHost();
                }

                if (cloudRelay != null && cloudRelay.IsConnected)
                {
                    cloudAnnounceTimer += Time.deltaTime;
                    if (cloudAnnounceTimer >= 2.5f)
                    {
                        cloudAnnounceTimer = 0f;
                        cloudRelay.PublishLobbyAnnounce(currentRoomId, currentRoomName, remotePlayers.Count + 1, currentMaxPlayers);
                    }
                }
            }
        }

        private void HandleKeyboardChatShortcuts()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tKey.wasPressedThisFrame && !isChatInputActive && !CozyLifeMainMenu.IsActive)
            {
                isChatInputActive = true;
                chatInputText = "";
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (isChatInputActive)
                {
                    SubmitChatMessage();
                }
                else if (!CozyLifeMainMenu.IsActive)
                {
                    isChatInputActive = true;
                    chatInputText = "";
                }
            }
            else if (kb.escapeKey.wasPressedThisFrame && isChatInputActive)
            {
                isChatInputActive = false;
            }
        }

        public void SubmitChatMessage()
        {
            if (string.IsNullOrWhiteSpace(chatInputText))
            {
                isChatInputActive = false;
                return;
            }

            string clean = chatInputText.Trim();
            chatInputText = "";
            isChatInputActive = false;

            var localPlayer = FindAnyObjectByType<PlayerController>();
            string myName = (localPlayer != null && localPlayer.GetComponent<ProfessionAndInventory>() != null)
                ? localPlayer.GetComponent<ProfessionAndInventory>().PlayerName
                : "Bạn";

            int myId = client != null ? client.AssignedClientId : 1;
            var msg = new ChatMessagePacket
            {
                senderId = myId,
                senderName = myName,
                message = clean,
                timestamp = DateTime.Now.ToString("HH:mm")
            };

            string json = JsonUtility.ToJson(msg);

            if (isHostActive)
            {
                server.Broadcast(PacketType.ChatMessage, json);
                if (cloudRelay != null && cloudRelay.IsConnected)
                    cloudRelay.SendPacketToRoom(PacketType.ChatMessage, json);
                HandleClientChatMessage(json);
            }
            else
            {
                if (isUsingCloudRelay && cloudRelay != null && cloudRelay.IsConnected)
                {
                    cloudRelay.SendPacketToRoom(PacketType.ChatMessage, json);
                }
                else if (client != null && client.IsConnected)
                {
                    client.SendPacket(PacketType.ChatMessage, json);
                }
                ShowLocalPlayerChatBubble(clean);
            }
        }

        private void ShowLocalPlayerChatBubble(string text)
        {
            var localPlayer = FindAnyObjectByType<PlayerController>();
            if (localPlayer != null)
            {
                var bubble = localPlayer.GetComponent<RemoteNetworkPlayer>();
                if (bubble == null)
                {
                    bubble = localPlayer.gameObject.AddComponent<RemoteNetworkPlayer>();
                    bubble.Initialize(new PlayerDataSync
                    {
                        clientId = client != null ? client.AssignedClientId : 1,
                        playerName = localPlayer.GetComponent<ProfessionAndInventory>()?.PlayerName ?? "Bạn"
                    });
                }
                bubble.ShowChatBubble(text);
            }
        }
        #endregion

        #region Cloud Packet Processing
        private void ProcessCloudIncomingMessages()
        {
            if (cloudRelay == null) return;

            while (cloudRelay.IncomingPackets.TryDequeue(out CloudPacketMessage msg))
            {
                if (isHostActive)
                {
                    // Host nhận từ các client khác mạng gửi lên qua kênh c2s
                    switch (msg.type)
                    {
                        case PacketType.JoinRoomRequest:
                            int assignedId = nextCloudClientId++;
                            HandleServerJoinRequest(assignedId, msg.payload);
                            break;

                        case PacketType.PlayerStateSync:
                            var pData = JsonUtility.FromJson<PlayerDataSync>(msg.payload);
                            if (pData != null)
                            {
                                server.SetClientData(pData.clientId, pData);
                                server.Broadcast(PacketType.PlayerStateSync, msg.payload, pData.clientId);
                                cloudRelay.SendPacketToRoom(PacketType.PlayerStateSync, msg.payload);
                                SpawnOrUpdateRemotePlayer(pData);
                            }
                            break;

                        case PacketType.ChatMessage:
                            server.Broadcast(PacketType.ChatMessage, msg.payload);
                            cloudRelay.SendPacketToRoom(PacketType.ChatMessage, msg.payload);
                            HandleClientChatMessage(msg.payload);
                            break;

                        case PacketType.WerewolfEvent:
                            server.Broadcast(PacketType.WerewolfEvent, msg.payload);
                            cloudRelay.SendPacketToRoom(PacketType.WerewolfEvent, msg.payload);
                            OnWerewolfPacketReceived?.Invoke(msg.payload);
                            break;

                        case PacketType.LeaveRoom:
                            var notify = JsonUtility.FromJson<SimpleNotificationPacket>(msg.payload);
                            if (notify != null)
                            {
                                server.Broadcast(PacketType.PlayerLeft, msg.payload);
                                cloudRelay.SendPacketToRoom(PacketType.PlayerLeft, msg.payload);
                                HandleClientPlayerLeft(msg.payload);
                            }
                            break;
                    }
                }
                else
                {
                    // Client nhận từ Host gửi xuống qua kênh s2c
                    switch (msg.type)
                    {
                        case PacketType.JoinRoomResponse:
                            HandleClientJoinResponse(msg.payload);
                            break;

                        case PacketType.PlayerJoined:
                            HandleClientPlayerJoined(msg.payload);
                            break;

                        case PacketType.PlayerLeft:
                            HandleClientPlayerLeft(msg.payload);
                            break;

                        case PacketType.PlayerStateSync:
                            HandleClientPlayerStateSync(msg.payload);
                            break;

                        case PacketType.WorldStateSync:
                            HandleClientWorldStateSync(msg.payload);
                            break;

                        case PacketType.ChatMessage:
                            HandleClientChatMessage(msg.payload);
                            break;

                        case PacketType.WerewolfEvent:
                            OnWerewolfPacketReceived?.Invoke(msg.payload);
                            break;

                        case PacketType.LeaveRoom:
                            CozyLifeHUD.PushToast("⚠️ Chủ phòng đã đóng phòng chơi.");
                            LeaveRoom();
                            break;
                    }
                }
            }
        }
        #endregion

        #region Server Packet Processing (Chạy trên Host)
        private void ProcessServerIncomingMessages()
        {
            if (!isHostActive || server == null)
                return;

            while (server.DisconnectedQueue.TryDequeue(out int dcClientId))
            {
                var notify = new SimpleNotificationPacket { clientId = dcClientId, reason = "Đã ngắt kết nối" };
                string json = JsonUtility.ToJson(notify);
                server.Broadcast(PacketType.PlayerLeft, json);
                if (cloudRelay != null && cloudRelay.IsConnected)
                    cloudRelay.SendPacketToRoom(PacketType.PlayerLeft, json);
            }

            while (server.IncomingQueue.TryDequeue(out NetworkIncomingMessage msg))
            {
                switch (msg.type)
                {
                    case PacketType.JoinRoomRequest:
                        HandleServerJoinRequest(msg.clientId, msg.payload);
                        break;

                    case PacketType.PlayerStateSync:
                        HandleServerPlayerStateSync(msg.clientId, msg.payload);
                        break;

                    case PacketType.ChatMessage:
                        server.Broadcast(PacketType.ChatMessage, msg.payload);
                        if (cloudRelay != null && cloudRelay.IsConnected)
                            cloudRelay.SendPacketToRoom(PacketType.ChatMessage, msg.payload);
                        HandleClientChatMessage(msg.payload);
                        break;

                    case PacketType.WerewolfEvent:
                        server.Broadcast(PacketType.WerewolfEvent, msg.payload, msg.clientId);
                        if (cloudRelay != null && cloudRelay.IsConnected)
                            cloudRelay.SendPacketToRoom(PacketType.WerewolfEvent, msg.payload);
                        OnWerewolfPacketReceived?.Invoke(msg.payload);
                        break;

                    case PacketType.LeaveRoom:
                        server.DisconnectClient(msg.clientId, "Client chủ động rời");
                        break;
                }
            }
        }

        private void HandleServerJoinRequest(int clientId, string payload)
        {
            var req = JsonUtility.FromJson<JoinRoomRequestPacket>(payload);
            if (req == null) return;

            var pData = new PlayerDataSync
            {
                clientId = clientId,
                playerName = req.playerName,
                appearanceJson = req.appearanceJson,
                profession = req.profession,
                posX = 0,
                posY = 0,
                posZ = 0
            };
            server.SetClientData(clientId, pData);

            List<PlayerDataSync> allPlayers = server.GetAllConnectedPlayerData();

            int curDay = 1;
            float curHour = 8f;
            int curWeather = 0;
            if (CozyTownWorldBuilder.Instance != null)
            {
                curDay = CozyTownWorldBuilder.Instance.CurrentDay;
                curHour = CozyTownWorldBuilder.Instance.CurrentHour;
                curWeather = (int)CozyTownWorldBuilder.Instance.CurrentWeather;
            }

            var resp = new JoinRoomResponsePacket
            {
                success = true,
                message = "Gia nhập phòng thành công!",
                roomId = currentRoomId,
                assignedClientId = clientId,
                worldDay = curDay,
                worldHour = curHour,
                currentWeather = curWeather,
                currentPlayers = allPlayers
            };

            string respJson = JsonUtility.ToJson(resp);
            string joinedJson = JsonUtility.ToJson(pData);

            server.SendTo(clientId, PacketType.JoinRoomResponse, respJson);
            server.Broadcast(PacketType.PlayerJoined, joinedJson, clientId);

            if (cloudRelay != null && cloudRelay.IsConnected)
            {
                cloudRelay.SendPacketToRoom(PacketType.JoinRoomResponse, respJson);
                cloudRelay.SendPacketToRoom(PacketType.PlayerJoined, joinedJson);
            }

            SpawnOrUpdateRemotePlayer(pData);
        }

        private void HandleServerPlayerStateSync(int clientId, string payload)
        {
            var pData = JsonUtility.FromJson<PlayerDataSync>(payload);
            if (pData == null) return;

            pData.clientId = clientId;
            server.SetClientData(clientId, pData);

            server.Broadcast(PacketType.PlayerStateSync, payload, clientId);
            if (cloudRelay != null && cloudRelay.IsConnected)
                cloudRelay.SendPacketToRoom(PacketType.PlayerStateSync, payload);
        }

        private void BroadcastWorldStateFromHost()
        {
            if (CozyTownWorldBuilder.Instance == null) return;

            var sync = new WorldStateSync
            {
                day = CozyTownWorldBuilder.Instance.CurrentDay,
                hour = CozyTownWorldBuilder.Instance.CurrentHour,
                weather = (int)CozyTownWorldBuilder.Instance.CurrentWeather,
                weatherRemaining = CozyTownWorldBuilder.Instance.WeatherHoursRemaining
            };

            string json = JsonUtility.ToJson(sync);
            server.Broadcast(PacketType.WorldStateSync, json);
            if (cloudRelay != null && cloudRelay.IsConnected)
                cloudRelay.SendPacketToRoom(PacketType.WorldStateSync, json);
        }
        #endregion

        #region Client Packet Processing
        private void ProcessClientIncomingMessages()
        {
            if (client == null) return;

            while (client.IncomingQueue.TryDequeue(out NetworkIncomingMessage msg))
            {
                switch (msg.type)
                {
                    case PacketType.JoinRoomResponse:
                        HandleClientJoinResponse(msg.payload);
                        break;

                    case PacketType.PlayerJoined:
                        HandleClientPlayerJoined(msg.payload);
                        break;

                    case PacketType.PlayerLeft:
                        HandleClientPlayerLeft(msg.payload);
                        break;

                    case PacketType.PlayerStateSync:
                        HandleClientPlayerStateSync(msg.payload);
                        break;

                    case PacketType.WorldStateSync:
                        HandleClientWorldStateSync(msg.payload);
                        break;

                    case PacketType.ChatMessage:
                        HandleClientChatMessage(msg.payload);
                        break;

                    case PacketType.WerewolfEvent:
                        OnWerewolfPacketReceived?.Invoke(msg.payload);
                        break;

                    case PacketType.LeaveRoom:
                        CozyLifeHUD.PushToast("⚠️ Phòng đã bị đóng hoặc mất kết nối.");
                        LeaveRoom();
                        break;
                }
            }
        }

        private void HandleClientJoinResponse(string payload)
        {
            var resp = JsonUtility.FromJson<JoinRoomResponsePacket>(payload);
            if (resp == null) return;

            if (!resp.success)
            {
                CozyLifeHUD.PushToast($"❌ Gia nhập thất bại: {resp.message}");
                LeaveRoom();
                return;
            }

            if (client != null && !isHostActive)
                client.AssignedClientId = resp.assignedClientId;

            currentRoomId = resp.roomId;
            isOnlineActive = true;

            if (CozyTownWorldBuilder.Instance != null && !isHostActive)
            {
                CozyTownWorldBuilder.Instance.RestoreWorldStateFromSave(
                    resp.worldDay,
                    resp.worldHour,
                    (WeatherType)resp.currentWeather,
                    8f,
                    24f,
                    BuildingType.None
                );
            }

            int myId = client != null ? client.AssignedClientId : 0;
            if (resp.currentPlayers != null)
            {
                foreach (var p in resp.currentPlayers)
                {
                    if (p.clientId != myId)
                    {
                        SpawnOrUpdateRemotePlayer(p);
                    }
                }
            }

            CozyLifeHUD.PushToast($"🎉 Đã kết nối vào phòng [{currentRoomId}] thành công!");
            AddSystemChatMessage($"🌿 Bạn đã tham gia phòng [{currentRoomId}]. Nhấn [T] hoặc [Enter] để trò chuyện!");
        }

        private void HandleClientPlayerJoined(string payload)
        {
            var pData = JsonUtility.FromJson<PlayerDataSync>(payload);
            int myId = client != null ? client.AssignedClientId : 0;
            if (pData == null || pData.clientId == myId) return;

            SpawnOrUpdateRemotePlayer(pData);
            CozyAudioManager.PlaySfx(CozySfxType.Coin);
            CozyLifeHUD.PushToast($"👋 {pData.playerName} đã bước vào thế giới!");
            AddSystemChatMessage($"👋 {pData.playerName} đã tham gia thế giới.");
        }

        private void HandleClientPlayerLeft(string payload)
        {
            var notify = JsonUtility.FromJson<SimpleNotificationPacket>(payload);
            if (notify == null) return;

            if (remotePlayers.TryGetValue(notify.clientId, out var rp))
            {
                string pName = rp.PlayerName;
                Destroy(rp.gameObject);
                remotePlayers.Remove(notify.clientId);
                CozyLifeHUD.PushToast($"🚶 {pName} đã rời phòng.");
                AddSystemChatMessage($"🚶 {pName} đã rời phòng.");
            }
        }

        private void HandleClientPlayerStateSync(string payload)
        {
            var pData = JsonUtility.FromJson<PlayerDataSync>(payload);
            int myId = client != null ? client.AssignedClientId : 0;
            if (pData == null || pData.clientId == myId) return;

            SpawnOrUpdateRemotePlayer(pData);
        }

        private void HandleClientWorldStateSync(string payload)
        {
            if (isHostActive) return;

            var wData = JsonUtility.FromJson<WorldStateSync>(payload);
            if (wData == null || CozyTownWorldBuilder.Instance == null) return;

            CozyTownWorldBuilder.Instance.RestoreWorldStateFromSave(
                wData.day,
                wData.hour,
                (WeatherType)wData.weather,
                wData.weatherRemaining,
                24f,
                CozyTownWorldBuilder.Instance.CurrentBuilding
            );
        }

        private void HandleClientChatMessage(string payload)
        {
            var chat = JsonUtility.FromJson<ChatMessagePacket>(payload);
            if (chat == null) return;

            Color nameCol = Color.yellow;
            if (remotePlayers.TryGetValue(chat.senderId, out var rp))
            {
                nameCol = CozyLifeDatabase.GetProfession(rp.Profession).themeColor;
                rp.ShowChatBubble(chat.message);
            }
            else if (client != null && chat.senderId == client.AssignedClientId)
            {
                ShowLocalPlayerChatBubble(chat.message);
            }

            chatHistory.Add(new ChatLogEntry
            {
                senderName = chat.senderName,
                message = chat.message,
                timeStr = string.IsNullOrEmpty(chat.timestamp) ? DateTime.Now.ToString("HH:mm") : chat.timestamp,
                nameColor = nameCol
            });

            if (chatHistory.Count > MaxChatHistory)
            {
                chatHistory.RemoveAt(0);
            }

            chatScrollPos.y = float.MaxValue;
        }

        private void AddSystemChatMessage(string text)
        {
            chatHistory.Add(new ChatLogEntry
            {
                senderName = "HỆ THỐNG",
                message = text,
                timeStr = DateTime.Now.ToString("HH:mm"),
                nameColor = new Color(0.4f, 0.85f, 1f)
            });
            if (chatHistory.Count > MaxChatHistory)
            {
                chatHistory.RemoveAt(0);
            }
        }
        #endregion

        #region Remote Player Management
        private void SpawnOrUpdateRemotePlayer(PlayerDataSync data)
        {
            if (remotePlayers.TryGetValue(data.clientId, out var existing))
            {
                existing.ApplyNetworkUpdate(data);
                return;
            }

            if (remotePlayerContainer == null)
            {
                remotePlayerContainer = new GameObject("RemotePlayers_Container");
            }

            GameObject obj = new GameObject($"RemotePlayer_{data.clientId}_{data.playerName}");
            obj.transform.SetParent(remotePlayerContainer.transform);

            var rp = obj.AddComponent<RemoteNetworkPlayer>();
            rp.Initialize(data);
            remotePlayers[data.clientId] = rp;
        }

        private void BroadcastLocalPlayerState()
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            var profInv = player.GetComponent<ProfessionAndInventory>();
            var sr = player.GetComponent<SpriteRenderer>();

            BuildingType curBld = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentBuilding
                : BuildingType.None;

            int myId = client != null ? client.AssignedClientId : 1;
            var data = new PlayerDataSync
            {
                clientId = myId,
                playerName = profInv != null ? profInv.PlayerName : "Bạn",
                profession = (int)(profInv != null ? profInv.CurrentProfession : ProfessionType.Farmer),
                appearanceJson = (profInv != null && profInv.Appearance != null) ? JsonUtility.ToJson(profInv.Appearance) : "",
                posX = player.transform.position.x,
                posY = player.transform.position.y,
                posZ = player.transform.position.z,
                flipX = sr != null && sr.flipX,
                walkFrame = player.CurrentDisplayedWalkFrame,
                isMoving = player.GetComponent<Rigidbody2D>() != null && player.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude > 0.01f,
                currentBuilding = (int)curBld,
                actionTitle = player.IsPerformingAction ? player.CurrentActionTitle : "",
                isSeated = player.IsSeatedAtTable || player.IsSeatedAtFloorTable,
                isSleeping = player.GetComponent<PlayerActionVisualizer>() != null && player.GetComponent<PlayerActionVisualizer>().IsSleeping
            };

            string json = JsonUtility.ToJson(data);

            if (isHostActive)
            {
                server.Broadcast(PacketType.PlayerStateSync, json);
                if (cloudRelay != null && cloudRelay.IsConnected)
                    cloudRelay.SendPacketToRoom(PacketType.PlayerStateSync, json);
            }
            else
            {
                if (isUsingCloudRelay && cloudRelay != null && cloudRelay.IsConnected)
                {
                    cloudRelay.SendPacketToRoom(PacketType.PlayerStateSync, json);
                }
                else if (client != null && client.IsConnected)
                {
                    client.SendPacket(PacketType.PlayerStateSync, json);
                }
            }
        }
        #endregion

        #region In-Game GUI (Room Widget & Chat Bar)
        private void EnsureStyles()
        {
            if (stylesInitialized && whiteTex != null) return;

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();

            roomBadgeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.92f, 0.5f) }
            };

            chatBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperLeft
            };

            chatInputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            chatItemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            modalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.90f, 0.45f) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!isOnlineActive || CozyLifeMainMenu.IsActive)
                return;

            EnsureStyles();

            DrawRoomOnlineBadge();
            DrawInGameChatSystem();

            if (showRoomDetailsModal)
            {
                DrawRoomDetailsModal();
            }
        }

        private void DrawRoomOnlineBadge()
        {
            int totalPlayers = remotePlayers.Count + 1;
            string roleLabel = isHostActive ? "👑 Chủ Phòng" : "🤝 Khách";
            string netType = isUsingCloudRelay ? "🌐 Đám Mây" : "📡 LAN";
            string badgeText = $"{netType}: {currentRoomId} | {roleLabel} | 👥 {totalPlayers} người";

            float bw = 250f;
            float bh = 26f;
            float bx = Screen.width - bw - 14f;
            float by = 60f;
            Rect badgeRect = new Rect(bx, by, bw, bh);

            DrawSolidRect(badgeRect, new Color(0.10f, 0.14f, 0.20f, 0.92f));
            DrawBorderRect(badgeRect, new Color(0.40f, 0.70f, 0.95f), 1.2f);

            if (GUI.Button(badgeRect, badgeText, roomBadgeStyle))
            {
                showRoomDetailsModal = !showRoomDetailsModal;
            }
        }

        private void DrawInGameChatSystem()
        {
            float chatW = 320f;
            float chatH = 140f;
            float chatX = 14f;
            float chatY = Screen.height - 180f - chatH;

            Rect chatRect = new Rect(chatX, chatY, chatW, chatH);
            DrawSolidRect(chatRect, new Color(0.08f, 0.10f, 0.14f, isChatInputActive ? 0.92f : 0.65f));
            DrawBorderRect(chatRect, isChatInputActive ? new Color(0.45f, 0.75f, 1f) : new Color(0.25f, 0.32f, 0.42f, 0.5f), 1f);

            float viewH = Mathf.Max(chatH - 34f, chatHistory.Count * 22f);
            Rect viewRect = new Rect(0, 0, chatW - 20f, viewH);
            chatScrollPos = GUI.BeginScrollView(new Rect(chatX + 6, chatY + 6, chatW - 12, chatH - 36), chatScrollPos, viewRect);

            float curY = 0f;
            foreach (var item in chatHistory)
            {
                Rect lineRect = new Rect(2, curY, viewRect.width, 20);
                string text = $"[{item.timeStr}] <b>{item.senderName}</b>: {item.message}";
                GUIStyle st = new GUIStyle(chatItemStyle)
                {
                    richText = true,
                    normal = { textColor = item.nameColor }
                };
                GUI.Label(lineRect, text, st);
                curY += 20f;
            }

            GUI.EndScrollView();

            float barY = chatY + chatH - 28f;
            if (isChatInputActive)
            {
                GUI.SetNextControlName("ChatInputField");
                chatInputText = GUI.TextField(new Rect(chatX + 6, barY, chatW - 68, 22), chatInputText, 100, chatInputStyle);
                GUI.FocusControl("ChatInputField");

                if (GUI.Button(new Rect(chatX + chatW - 58, barY, 52, 22), "Gửi", buttonStyle))
                {
                    SubmitChatMessage();
                }
            }
            else
            {
                string hint = "Nhấn [T] hoặc [Enter] để trò chuyện cùng bạn bè...";
                GUIStyle hintSt = new GUIStyle(chatItemStyle) { normal = { textColor = new Color(0.65f, 0.72f, 0.82f, 0.8f) } };
                if (GUI.Button(new Rect(chatX + 6, barY, chatW - 12, 22), hint, hintSt))
                {
                    isChatInputActive = true;
                    chatInputText = "";
                }
            }
        }

        private void DrawRoomDetailsModal()
        {
            float mw = 360f;
            float mh = 260f;
            Rect modalRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

            DrawSolidRect(modalRect, new Color(0.11f, 0.14f, 0.19f, 0.98f));
            DrawBorderRect(modalRect, new Color(0.92f, 0.75f, 0.35f), 1.8f);

            GUI.Label(new Rect(modalRect.x, modalRect.y + 10, mw, 20), $"🌐 THÔNG TIN PHÒNG CHƠI [{currentRoomId}]", modalTitleStyle);

            float curY = modalRect.y + 36f;
            GUI.Label(new Rect(modalRect.x + 14, curY, mw - 28, 18), $"Mã Phòng ID: <b>{currentRoomId}</b>", chatItemStyle);
            curY += 22f;

            string connType = isUsingCloudRelay ? "Đám Mây Toàn Cầu (Khác mạng chơi tự do)" : "Mạng Nội Bộ LAN";
            GUI.Label(new Rect(modalRect.x + 14, curY, mw - 28, 18), $"Kết nối: {connType}", chatItemStyle);
            curY += 24f;

            if (GUI.Button(new Rect(modalRect.x + 14, curY, 160, 26), "📋 Sao chép mã phòng", buttonStyle))
            {
                GUIUtility.systemCopyBuffer = currentRoomId;
                CozyLifeHUD.PushToast($"📋 Đã sao chép mã phòng [{currentRoomId}] vào bộ nhớ tạm!");
            }
            curY += 34f;

            GUI.Label(new Rect(modalRect.x + 14, curY, mw - 28, 18), "👥 Danh sách bạn bè trong phòng:", chatItemStyle);
            curY += 20f;

            var localPlayer = FindAnyObjectByType<PlayerController>();
            string myName = localPlayer?.GetComponent<ProfessionAndInventory>()?.PlayerName ?? "Bạn";
            var myProf = localPlayer?.GetComponent<ProfessionAndInventory>()?.CurrentProfessionDef;
            string myProfTitle = myProf != null ? myProf.titleBadge : "Nông Dân";

            GUI.Label(new Rect(modalRect.x + 22, curY, mw - 40, 18), $"• {myName} (Bạn) - {myProfTitle} {(isHostActive ? "[👑 Chủ]" : "")}", chatItemStyle);
            curY += 18f;

            foreach (var rp in remotePlayers.Values)
            {
                if (rp == null) continue;
                string rpProf = CozyLifeDatabase.GetProfession(rp.Profession).titleBadge;
                GUI.Label(new Rect(modalRect.x + 22, curY, mw - 40, 18), $"• {rp.PlayerName} - {rpProf}", chatItemStyle);
                curY += 18f;
            }

            float btnY = modalRect.y + mh - 36f;
            if (GUI.Button(new Rect(modalRect.x + 14, btnY, 110, 26), "Đóng [Esc]", buttonStyle))
            {
                showRoomDetailsModal = false;
            }

            if (GUI.Button(new Rect(modalRect.x + mw - 144, btnY, 130, 26), "🚪 Rời Phòng Online", buttonStyle))
            {
                showRoomDetailsModal = false;
                LeaveRoom();
                CozyLifeHUD.PushToast("Đã rời phòng online.");
            }
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex);
            GUI.color = prev;
        }

        private void DrawBorderRect(Rect rect, Color color, float width = 1f)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, width), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            DrawSolidRect(new Rect(rect.x, rect.y, width, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
        #endregion
    }
}
