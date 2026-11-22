using System;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.World;

namespace CozyLife.UI
{
    /// <summary>
    /// Giao diện HUD tinh gọn, hiển thị Thanh Tiến Trình Hành Động (Timed Action Bar) kiểu Project Zomboid.
    /// Quy chuẩn thời gian: 1 phút thực tế = 1 tiếng trong game (1 giây = 1 phút game).
    /// </summary>
    public class CozyLifeHUD : MonoBehaviour
    {
        public static CozyLifeHUD Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance()
        {
            Instance = null;
            currentToastMessage = "WASD: Di chuyển | [E]: Tương tác / Vào nhà | [P]: Đổi nghề | [C]: Chế tạo";
            toastTimer = 8f;
        }

        private CharacterStats playerStats;
        private ProfessionAndInventory playerProfInv;
        private PlayerController playerController;

        private bool showProfessionModal = false;
        private bool showCraftingModal = false;
        private bool showFriendsModal = false;
        private bool showRestaurantOrderModal = false;
        private RestaurantTableInteractable activeDiningTable;
        private bool showMarketModal = false;
        private int marketActiveTab = 0;
        private Vector2 marketScrollPos = Vector2.zero;
        private Vector2 decorScrollPos = Vector2.zero;

        private bool isTransitioning = false;
        private float transitionTimer = 0f;
        private float transitionDuration = 0.55f;
        private bool transitionCallbackFired = false;
        private string transitionBannerText = "";
        private Action onMidTransitionCallback;

        private static string currentToastMessage = "WASD: Di chuyển | [E]: Tương tác / Vào nhà | [P]: Đổi nghề | [C]: Chế tạo";
        private static float toastTimer = 8f;

        private struct RainParticle
        {
            public float x, y, speed, length, alpha;
        }

        private struct SnowParticle
        {
            public float x, y, speed, size, seed, alpha;
        }

        private readonly RainParticle[] rainDrops = new RainParticle[85];
        private readonly SnowParticle[] snowFlakes = new SnowParticle[75];
        private bool weatherParticlesInitialized = false;
        private Vector2 craftingScrollPos = Vector2.zero;

        private Texture2D whiteTex;
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle worldTagStyle;
        private GUIStyle chatBubbleStyle;
        private GUIStyle bannerStyle;
        private bool stylesInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void BindPlayer(PlayerController controller, CharacterStats stats, ProfessionAndInventory profInv)
        {
            playerController = controller;
            playerStats = stats;
            playerProfInv = profInv;

            stats.OnStatusNotification += PushToast;
            profInv.OnActionFeedback += PushToast;
        }

        public static void PushToast(string message)
        {
            currentToastMessage = message;
            toastTimer = 5.0f;
        }

        public void PlaySceneTransition(string bannerText, Action onMidpoint)
        {
            transitionBannerText = bannerText;
            onMidTransitionCallback = onMidpoint;
            transitionTimer = 0f;
            transitionCallbackFired = false;
            isTransitioning = true;
            CloseAllModals();
        }

        public void CloseAllModals()
        {
            showProfessionModal = false;
            showCraftingModal = false;
            showFriendsModal = false;
            showRestaurantOrderModal = false;
            showMarketModal = false;
        }

        public void OpenMarketModal()
        {
            CloseAllModals();
            marketActiveTab = 0;
            marketScrollPos = Vector2.zero;
            showMarketModal = true;
        }

        public void CloseMarketModal()
        {
            showMarketModal = false;
        }

        public void OpenRestaurantOrderModal(RestaurantTableInteractable table)
        {
            CloseAllModals();
            activeDiningTable = table;
            showRestaurantOrderModal = true;
        }

        public void CloseRestaurantOrderModal()
        {
            showRestaurantOrderModal = false;
            activeDiningTable = null;
        }

        public void ToggleProfessionPanel()
        {
            bool next = !showProfessionModal;
            CloseAllModals();
            showProfessionModal = next;
        }

        public void OpenCraftingPanel(bool forceOpen = false)
        {
            bool next = forceOpen ? true : !showCraftingModal;
            CloseAllModals();
            showCraftingModal = next;
        }

        public void ToggleFriendsPanel()
        {
            bool next = !showFriendsModal;
            CloseAllModals();
            showFriendsModal = next;
        }

        private void Update()
        {
            if (toastTimer > 0f)
                toastTimer -= Time.deltaTime;

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (showMarketModal || showRestaurantOrderModal || showProfessionModal || showCraftingModal || showFriendsModal)
                {
                    CloseAllModals();
                }
            }

            if (isTransitioning)
            {
                transitionTimer += Time.deltaTime;
                if (!transitionCallbackFired && transitionTimer >= transitionDuration * 0.45f)
                {
                    transitionCallbackFired = true;
                    onMidTransitionCallback?.Invoke();
                }

                if (transitionTimer >= transitionDuration)
                {
                    isTransitioning = false;
                }
            }
        }

        private void EnsureStyles()
        {
            if (stylesInitialized && whiteTex != null)
                return;

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.95f, 0.88f) }
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.87f, 0.85f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            worldTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            chatBubbleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.97f, 0.85f) }
            };

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.96f, 0.84f) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (playerStats == null || playerProfInv == null)
                return;

            EnsureStyles();

            if (CozyLifeMainMenu.IsActive || CozyChessManager.IsChessSceneActive || CozyXiangqiManager.IsXiangqiSceneActive)
            {
                if (CozyLifeMainMenu.IsActive)
                    DrawOutdoorDayNightAtmosphere();
                if (isTransitioning)
                    DrawSceneTransitionOverlay();
                return;
            }

            DrawOutdoorDayNightAtmosphere();
            DrawOutdoorWeatherParticles();

            // Chỉ vẽ nhãn nổi tương tác trên đầu vật thể và nhân vật khi KHÔNG có bảng thông báo/nhiệm vụ nào đang mở
            if (!IsAnyModalOrGameWindowOpen())
            {
                DrawWorldLabelsAndBubbles();
            }

            // Hiển thị đếm ngược 3 giây khi cả 2 người đã ngồi vào Bàn Cờ Vua Công Viên
            if (ParkChessTableInteractable.ActiveSeatedTable != null && ParkChessTableInteractable.ActiveSeatedTable.IsPlayerSeatedHere)
            {
                var chessTbl = ParkChessTableInteractable.ActiveSeatedTable;
                float bw = 440f;
                float bh = 46f;
                Rect cdBox = new Rect((Screen.width - bw) * 0.5f, 22f, bw, bh);
                DrawSolidRect(cdBox, new Color(0.10f, 0.13f, 0.19f, 0.95f));
                DrawBorderRect(cdBox, new Color(1f, 0.84f, 0.32f), 2f);
                GUIStyle cdSt = new GUIStyle(titleStyle)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.92f, 0.45f) }
                };
                string msg = chessTbl.AreBothSeated
                    ? $"♟️ Cả 2 người đã ngồi vào bàn! Vào ván cờ sau {Mathf.Clamp(Mathf.CeilToInt(chessTbl.CountdownRemaining), 1, 3)} giây..."
                    : "🪑 Đang chờ người chơi thứ 2 ngồi vào ghế đối diện...";
                GUI.Label(cdBox, msg, cdSt);
            }

            // Hiển thị đếm ngược 3 giây khi cả 2 người đã ngồi vào Bàn Cờ Tướng Công Viên
            if (ParkXiangqiTableInteractable.ActiveSeatedTable != null && ParkXiangqiTableInteractable.ActiveSeatedTable.IsPlayerSeatedHere)
            {
                var xqTbl = ParkXiangqiTableInteractable.ActiveSeatedTable;
                float bw = 460f;
                float bh = 46f;
                Rect cdBox = new Rect((Screen.width - bw) * 0.5f, 22f, bw, bh);
                DrawSolidRect(cdBox, new Color(0.10f, 0.13f, 0.19f, 0.95f));
                DrawBorderRect(cdBox, new Color(1f, 0.45f, 0.35f), 2f);
                GUIStyle cdSt = new GUIStyle(titleStyle)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.92f, 0.45f) }
                };
                string msg = xqTbl.AreBothSeated
                    ? $"🀄 Cả 2 người đã ngồi vào bàn! Vào ván Cờ Tướng sau {Mathf.Clamp(Mathf.CeilToInt(xqTbl.CountdownRemaining), 1, 3)} giây..."
                    : "🪑 Đang chờ kỳ thủ thứ 2 ngồi vào ghế đối diện...";
                GUI.Label(cdBox, msg, cdSt);
            }

            // Khi đang trong Minigame Thể Thao (Bóng Rổ / Bóng Đá): Ẩn thanh Hotbar & các thông báo dưới đáy để nhường chỗ hoàn toàn cho thanh căn lực & nút ném bóng
            if (!CozySportsMinigameManager.IsGameActive)
            {
                DrawBottomLeftCompactVitalsPanel();
                DrawBottomHotbarAndPrompt();
            }

            DrawTopRightGlobalClockAndMoodles();

            if (showMarketModal)
                DrawMarketModal();
            else if (showRestaurantOrderModal)
                DrawRestaurantOrderWindow();
            else if (showProfessionModal)
                DrawProfessionSelectorWindow();
            else if (showCraftingModal)
                DrawCraftingWindow();
            else if (showFriendsModal)
                DrawFriendsWindow();

            if (isTransitioning)
                DrawSceneTransitionOverlay();
        }

        /// <summary>
        /// Lớp phủ ánh sáng bầu khí quyển Ngày / Hoàng Hôn / Đêm ngoài trời.
        /// </summary>
        private void DrawOutdoorDayNightAtmosphere()
        {
            if (CozyTownWorldBuilder.Instance == null)
                return;

            Color tint = CozyTownWorldBuilder.Instance.GetOutdoorAtmosphericTint();
            if (tint.a > 0.005f)
            {
                DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), tint);
            }
        }

        private void InitWeatherParticles()
        {
            if (weatherParticlesInitialized) return;
            for (int i = 0; i < rainDrops.Length; i++)
            {
                rainDrops[i] = new RainParticle
                {
                    x = UnityEngine.Random.Range(0, Screen.width + 200),
                    y = UnityEngine.Random.Range(0, Screen.height),
                    speed = UnityEngine.Random.Range(650f, 950f),
                    length = UnityEngine.Random.Range(14f, 24f),
                    alpha = UnityEngine.Random.Range(0.42f, 0.78f)
                };
            }
            for (int i = 0; i < snowFlakes.Length; i++)
            {
                snowFlakes[i] = new SnowParticle
                {
                    x = UnityEngine.Random.Range(0, Screen.width),
                    y = UnityEngine.Random.Range(0, Screen.height),
                    speed = UnityEngine.Random.Range(65f, 130f),
                    size = UnityEngine.Random.Range(2.5f, 5.0f),
                    seed = UnityEngine.Random.Range(0f, 100f),
                    alpha = UnityEngine.Random.Range(0.60f, 0.95f)
                };
            }
            weatherParticlesInitialized = true;
        }

        /// <summary>
        /// Hiệu ứng hạt mưa rơi / tuyết rơi / nắng gắt chỉ xuất hiện khi ở ngoài trời.
        /// </summary>
        private void DrawOutdoorWeatherParticles()
        {
            if (CozyTownWorldBuilder.Instance == null)
                return;

            // Khi ở bên trong các tòa nhà -> Không bị mưa / tuyết / nắng gắt tác động!
            if (CozyTownWorldBuilder.Instance.CurrentBuilding != BuildingType.None)
                return;

            WeatherType weather = CozyTownWorldBuilder.Instance.CurrentWeather;
            if (weather == WeatherType.Clear)
                return;

            InitWeatherParticles();
            float dt = Time.deltaTime;

            if (weather == WeatherType.Rain)
            {
                Color rainCol = new Color(0.72f, 0.84f, 1.0f);
                for (int i = 0; i < rainDrops.Length; i++)
                {
                    rainDrops[i].y += rainDrops[i].speed * dt;
                    rainDrops[i].x -= rainDrops[i].speed * 0.28f * dt;

                    if (rainDrops[i].y > Screen.height)
                    {
                        rainDrops[i].y = -rainDrops[i].length;
                        rainDrops[i].x = UnityEngine.Random.Range(0, Screen.width + 250);
                    }
                    if (rainDrops[i].x < -50)
                    {
                        rainDrops[i].x = Screen.width + 50;
                    }

                    Color c = new Color(rainCol.r, rainCol.g, rainCol.b, rainDrops[i].alpha);
                    DrawSolidRect(new Rect(rainDrops[i].x, rainDrops[i].y, 2f, rainDrops[i].length), c);
                }
            }
            else if (weather == WeatherType.Snow)
            {
                Color snowCol = new Color(0.96f, 0.98f, 1.0f);
                for (int i = 0; i < snowFlakes.Length; i++)
                {
                    snowFlakes[i].y += snowFlakes[i].speed * dt;
                    float sway = Mathf.Sin(Time.time * 2.2f + snowFlakes[i].seed) * 16f;
                    float drawX = snowFlakes[i].x + sway;

                    if (snowFlakes[i].y > Screen.height)
                    {
                        snowFlakes[i].y = -10f;
                        snowFlakes[i].x = UnityEngine.Random.Range(0, Screen.width);
                    }

                    Color c = new Color(snowCol.r, snowCol.g, snowCol.b, snowFlakes[i].alpha);
                    DrawSolidRect(new Rect(drawX, snowFlakes[i].y, snowFlakes[i].size, snowFlakes[i].size), c);
                }
            }
            else if (weather == WeatherType.Heatwave)
            {
                float pulse = 0.12f + Mathf.Sin(Time.time * 3f) * 0.04f;
                DrawSolidRect(new Rect(0, 0, Screen.width, 38f), new Color(1f, 0.85f, 0.35f, pulse));
            }
        }

        /// <summary>
        /// Bảng chỉ số Sinh Tồn (Máu, Đói, Khát, Thể Lực) nhỏ gọn ở góc trái dưới màn hình.
        /// </summary>
        private void DrawBottomLeftCompactVitalsPanel()
        {
            BuildingType currentBld = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentBuilding
                : BuildingType.None;

            float panelW = 208f;
            float panelH = 138f;
            Rect panelRect = new Rect(12f, Screen.height - panelH - 12f, panelW, panelH);

            DrawSolidRect(panelRect, new Color(0.09f, 0.11f, 0.14f, 0.92f));
            DrawBorderRect(panelRect, playerProfInv.CurrentProfessionDef.themeColor, 1.5f);

            GUIStyle miniHeaderStyle = new GUIStyle(titleStyle) { fontSize = 11 };
            GUIStyle miniBtnStyle = new GUIStyle(buttonStyle) { fontSize = 10 };

            // Dòng 1: Tên nhân vật + Badge Bệnh nếu đang mắc bệnh
            string displayName = string.IsNullOrEmpty(playerProfInv.PlayerName) ? "Bạn" : playerProfInv.PlayerName;
            string sickBadge = playerStats.CurrentSickness switch
            {
                SicknessType.Fever => " [🤒 SỐT]",
                SicknessType.Heatstroke => " [🥵 SỐC NHIỆT]",
                SicknessType.Cold => " [🥶 CẢM LẠNH]",
                _ => ""
            };
            Color nameCol = playerStats.IsSick ? new Color(1f, 0.40f, 0.40f) : new Color(0.98f, 0.95f, 0.88f);
            GUIStyle customNameStyle = new GUIStyle(miniHeaderStyle) { normal = { textColor = nameCol } };
            GUI.Label(new Rect(panelRect.x + 7, panelRect.y + 4, 130, 16), $"👤 {displayName}{sickBadge}", customNameStyle);

            if (currentBld != BuildingType.None)
            {
                if (GUI.Button(new Rect(panelRect.xMax - 68, panelRect.y + 4, 62, 16), "🚪 Ra", miniBtnStyle))
                {
                    CozyTownWorldBuilder.Instance?.ExitCurrentBuildingToTown();
                }
            }
            else
            {
                GUIStyle coinStyle = new GUIStyle(miniHeaderStyle) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(panelRect.x + 100, panelRect.y + 4, panelW - 108, 16), $"💰 {playerProfInv.Coins} Xu", coinStyle);
            }

            // 4 Thanh chỉ số sinh tồn siêu gọn (Máu, Đói, Khát, Thể Lực)
            float barX = panelRect.x + 7f;
            float barW = panelW - 14f;
            float barY = panelRect.y + 24f;
            float barH = 14f;
            float barStep = 17f;

            DrawCompactVitalBar(new Rect(barX, barY + barStep * 0, barW, barH), "❤️ Máu", playerStats.Health, playerStats.MaxHealth, new Color(0.88f, 0.28f, 0.32f));
            DrawCompactVitalBar(new Rect(barX, barY + barStep * 1, barW, barH), "🍖 Đói", playerStats.Hunger, playerStats.MaxHunger, new Color(0.92f, 0.58f, 0.22f));
            DrawCompactVitalBar(new Rect(barX, barY + barStep * 2, barW, barH), "💧 Khát", playerStats.Thirst, playerStats.MaxThirst, new Color(0.24f, 0.68f, 0.95f));
            DrawCompactVitalBar(new Rect(barX, barY + barStep * 3, barW, barH), "⚡ Sức", playerStats.Stamina, playerStats.MaxStamina, new Color(0.45f, 0.82f, 0.36f));

            // Dòng nút phím tắt nhỏ gọn dưới cùng (4 nút)
            float btnY = panelRect.yMax - 24f;
            float btnW = (barW - 9f) / 4f;
            if (GUI.Button(new Rect(barX, btnY, btnW, 19f), "Nghề [P]", miniBtnStyle))
                ToggleProfessionPanel();
            if (GUI.Button(new Rect(barX + (btnW + 3f), btnY, btnW, 19f), "Làm [C]", miniBtnStyle))
                OpenCraftingPanel();
            if (GUI.Button(new Rect(barX + (btnW + 3f) * 2f, btnY, btnW, 19f), "Bạn [Tab]", miniBtnStyle))
                ToggleFriendsPanel();
            if (GUI.Button(new Rect(barX + (btnW + 3f) * 3f, btnY, btnW, 19f), "Đồ [O]", miniBtnStyle))
                CozyLifeMainMenu.Instance?.OpenMenuInGame();
        }

        private void DrawCompactVitalBar(Rect rowRect, string label, float current, float max, Color barColor)
        {
            DrawSolidRect(rowRect, new Color(0.04f, 0.05f, 0.07f, 0.88f));

            float ratio = Mathf.Clamp01(current / Mathf.Max(1f, max));
            Color fillCol = (ratio < 0.25f) ? Color.Lerp(barColor, Color.red, Mathf.PingPong(Time.time * 3f, 1f)) : barColor;
            DrawSolidRect(new Rect(rowRect.x + 1, rowRect.y + 1, (rowRect.width - 2) * ratio, rowRect.height - 2), fillCol);

            GUIStyle leftStyle = new GUIStyle(smallStyle) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            GUIStyle rightStyle = new GUIStyle(leftStyle) { alignment = TextAnchor.MiddleRight };

            GUI.Label(new Rect(rowRect.x + 5, rowRect.y - 1, 90, rowRect.height + 2), label, leftStyle);
            GUI.Label(new Rect(rowRect.x, rowRect.y - 1, rowRect.width - 5, rowRect.height + 2), $"{Mathf.CeilToInt(current)}%", rightStyle);
        }

        /// <summary>
        /// Dòng thời gian chung thế giới (Global Game Clock) và trạng thái cơ thể (Moodles) ở góc trên bên phải.
        /// </summary>
        private void DrawTopRightGlobalClockAndMoodles()
        {
            float width = 232f;
            float x = Screen.width - width - 14f;
            float y = 14f;

            // 1. Widget Dòng Thời Gian Chung (Global Clock) + Vị trí + Thời Tiết + Âm Thanh & Lưu Game
            float clockH = 92f;
            Rect clockRect = new Rect(x, y, width, clockH);
            DrawSolidRect(clockRect, new Color(0.10f, 0.12f, 0.16f, 0.95f));
            DrawBorderRect(clockRect, new Color(0.95f, 0.78f, 0.35f, 0.9f), 1.5f);

            string timeText = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.GetFormattedTime()
                : "☀️ Ban Ngày | Ngày 1 - 08:00";

            string locationText = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.GetCurrentLocationDisplayName(playerStats.transform.position)
                : "🌳 Thị Trấn";

            string weatherTitle = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.GetWeatherDisplayTitle()
                : "🌤️ Nắng Ráo";

            GUIStyle clockTitle = new GUIStyle(titleStyle) { fontSize = 11 };
            GUI.Label(new Rect(x + 8, y + 3, width - 16, 16), timeText, clockTitle);
            GUI.Label(new Rect(x + 8, y + 20, width - 16, 16), $"📍 {locationText}  •  💰 {playerProfInv.Coins} Xu", smallStyle);

            GUIStyle weatherBtnStyle = new GUIStyle(buttonStyle) { fontSize = 10 };
            Rect weatherBtnRect = new Rect(x + 8, y + 40, width - 16, 21);
            if (GUI.Button(weatherBtnRect, $"{weatherTitle}  [Đổi K]", weatherBtnStyle))
            {
                CozyTownWorldBuilder.Instance?.CycleWeatherForTesting();
            }

            float thirdBtnW = (width - 24f) / 3f;
            string muteLabel = CozyAudioManager.IsMuted ? "🔇 Tắt" : "🔊 Nhạc";
            if (GUI.Button(new Rect(x + 8, y + 65, thirdBtnW, 21), muteLabel, weatherBtnStyle))
            {
                CozyAudioManager.ToggleMute();
            }
            if (GUI.Button(new Rect(x + 12 + thirdBtnW, y + 65, thirdBtnW, 21), "💾 Lưu [F5]", weatherBtnStyle))
            {
                CozySaveManager.SaveGame(true);
            }
            if (GUI.Button(new Rect(x + 16 + thirdBtnW * 2, y + 65, thirdBtnW, 21), "📖 Sổ Tay [J]", weatherBtnStyle))
            {
                CozyDiarySystem.EnsureInstance();
                CozyDiarySystem.Instance?.ToggleDiary();
            }

            y += clockH + 6f;

            // Cảnh báo phơi nhiễm thời tiết xấu ngoài trời (nếu đi quá 15 phút game mà chưa bị bệnh)
            bool isOutdoors = CozyTownWorldBuilder.Instance == null || CozyTownWorldBuilder.Instance.CurrentBuilding == BuildingType.None;
            if (isOutdoors && playerStats.OutdoorExposureMinutes >= 15f && !playerStats.IsSick)
            {
                Rect warnRect = new Rect(x, y, width, 24f);
                DrawSolidRect(warnRect, new Color(0.85f, 0.32f, 0.15f, 0.92f));
                DrawBorderRect(warnRect, Color.yellow, 1f);
                GUI.Label(warnRect, $"⚠️ Dầm thời tiết {Mathf.RoundToInt(playerStats.OutdoorExposureMinutes)}p (10% nhiễm bệnh)", worldTagStyle);
                y += 28f;
            }

            // 2. Danh sách Moodles (hiển thị ngay bên dưới đồng hồ thời gian)
            var moodles = playerStats.GetActiveMoodles();
            for (int i = 0; i < moodles.Count; i++)
            {
                var m = moodles[i];
                Rect mRect = new Rect(x, y, width, 38f);
                DrawSolidRect(mRect, new Color(0.12f, 0.14f, 0.18f, 0.90f));
                DrawSolidRect(new Rect(x, y, 5f, 38f), m.badgeColor);
                DrawBorderRect(mRect, new Color(m.badgeColor.r, m.badgeColor.g, m.badgeColor.b, 0.5f), 1f);

                GUI.Label(new Rect(x + 12, y + 2, width - 16, 18), m.title, titleStyle);
                GUI.Label(new Rect(x + 12, y + 19, width - 16, 16), m.subtitle, smallStyle);

                y += 42f;
            }
        }

        /// <summary>
        /// Thực Đơn Nhà Hàng ngắn gọn, có tính thời gian ăn (30p) và Phụ Bếp (1 tiếng = 60p).
        /// </summary>
        private void DrawRestaurantOrderWindow()
        {
            GUI.depth = -100;
            float w = 520f;
            float h = 395f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f - 10f, w, h);
            DrawSolidRect(winRect, new Color(0.12f, 0.10f, 0.09f, 0.97f));
            DrawBorderRect(winRect, new Color(0.95f, 0.68f, 0.28f), 2f);

            int tableNum = activeDiningTable != null ? activeDiningTable.TableNumber : 1;
            bool isChef = playerProfInv.CurrentProfession == ProfessionType.Chef;

            GUILayout.BeginArea(new Rect(winRect.x + 14, winRect.y + 12, w - 28, h - 24));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"🍽️ THỰC ĐƠN NHÀ HÀNG — Bàn #{tableNum}", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🚶 Đứng Dậy", buttonStyle, GUILayout.Width(95), GUILayout.Height(24)))
            {
                playerController?.StandUpFromTable();
                CloseRestaurantOrderModal();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(isChef ? "✨ Đầu Bếp được giảm 50% giá món ăn!" : $"💰 Số dư: {playerProfInv.Coins} Xu", smallStyle);
            GUILayout.Space(6);

            var menu = CozyLifeDatabase.GetRestaurantMenu();
            for (int i = 0; i < menu.Count; i++)
            {
                var dish = menu[i];
                int finalPrice = isChef ? Mathf.Max(1, dish.priceCoins / 2) : dish.priceCoins;
                bool canAfford = playerProfInv.Coins >= finalPrice;

                Rect cardRect = GUILayoutUtility.GetRect(w - 32, 56);
                DrawSolidRect(cardRect, new Color(0.18f, 0.15f, 0.14f, 0.96f));
                DrawBorderRect(cardRect, canAfford ? new Color(0.85f, 0.55f, 0.25f) : new Color(0.45f, 0.30f, 0.30f), 1f);

                Texture2D dishIcon = CozyLifeDatabase.LoadIconTexture(dish.iconResourcePath);
                if (dishIcon != null)
                {
                    GUI.DrawTexture(new Rect(cardRect.x + 8, cardRect.y + 8, 40, 40), dishIcon, ScaleMode.ScaleToFit);
                }

                GUI.Label(new Rect(cardRect.x + 56, cardRect.y + 5, 320, 20), $"{dish.displayName}  ({finalPrice} Xu)", titleStyle);
                GUI.Label(new Rect(cardRect.x + 56, cardRect.y + 27, 330, 22), dish.description, smallStyle);

                Rect orderBtnRect = new Rect(cardRect.x + cardRect.width - 108, cardRect.y + 12, 98, 32);
                if (GUI.Button(orderBtnRect, canAfford ? $"Gọi ({finalPrice} Xu)" : "Thiếu Xu", buttonStyle))
                {
                    OrderDishAtTable(dish, finalPrice);
                }

                GUILayout.Space(5);
            }

            GUILayout.Space(4);

            // Làm Phụ Bếp mất đúng 1 tiếng trong game (60 phút game = 1 phút thực tế)
            int wage = isChef ? 35 : 25;
            if (GUILayout.Button($"🍳 Làm Phụ Bếp 1 Tiếng (60p) — Nhận +{wage} Xu", buttonStyle, GUILayout.Height(34)))
            {
                StartKitchenWorkShift(wage);
            }

            GUILayout.EndArea();
        }

        private void OrderDishAtTable(RestaurantDishDefinition dish, int price)
        {
            if (playerController == null || playerController.IsPerformingAction)
                return;

            if (!playerProfInv.SpendCoins(price))
            {
                PushToast("⚠️ Thiếu Xu! Hãy bấm 'Làm Phụ Bếp 1 Tiếng' bên dưới để nhận Xu.");
                return;
            }

            var table = activeDiningTable;
            float eatMinutes = dish.dishId == "dish_peach_tea" ? 20f : (dish.dishId == "dish_cozy_feast" ? 40f : 30f);

            if (table != null)
            {
                table.ServeDishOnTable(dish, eatMinutes + 15f);
            }

            foreach (var f in TownFriendAgent.AllFriends)
            {
                if (f != null && f.AssignedBuilding == BuildingType.Restaurant)
                {
                    f.Say($"🍽️ Mời Bàn #{(table != null ? table.TableNumber : 1)} dùng {dish.displayName}!", 4.5f);
                }
            }

            showRestaurantOrderModal = false;

            ActionVisualType dishVisual = ActionVisualType.EatingBread;
            if (dish.dishId == "dish_tomato_soup" || (dish.displayName != null && (dish.displayName.Contains("Súp") || dish.displayName.Contains("Canh"))))
            {
                dishVisual = ActionVisualType.EatingSoup;
            }
            else if (dish.dishId == "dish_peach_tea" || (dish.displayName != null && (dish.displayName.Contains("Trà") || dish.displayName.Contains("Nước"))))
            {
                dishVisual = ActionVisualType.DrinkingWater;
            }

            playerController.StartTimedAction($"🍽️ Đang dùng {dish.displayName}", eatMinutes, () =>
            {
                playerStats.ModifyVitals(dish.restoreHealth, dish.restoreHunger, dish.restoreThirst, dish.restoreStamina);
                if (dish.takeawayBonusItem != ItemType.None)
                {
                    playerProfInv.AddItem(dish.takeawayBonusItem, 1);
                }
                CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
                PushToast($"✅ Đã ăn xong {dish.displayName}! (+{Mathf.RoundToInt(dish.restoreHunger)} No, +{Mathf.RoundToInt(dish.restoreThirst)} Nước)");
            }, dishVisual);
        }

        private void StartKitchenWorkShift(int wage)
        {
            if (playerController == null || playerController.IsPerformingAction)
                return;

            if (!playerStats.TryConsumeStamina(12f))
            {
                PushToast("⚠️ Bạn đang mệt, hãy nghỉ ngơi hoặc ăn uống trước!");
                return;
            }

            playerController.StandUpFromTable();
            CloseRestaurantOrderModal();

            // Làm phụ bếp đúng 1 tiếng trong game (60 phút game = 60 giây thực tế)
            playerController.StartTimedAction("🍳 Đang làm Phụ Bếp (1 tiếng)", 60f, () =>
            {
                playerProfInv.AddCoins(wage);
                CozyAudioManager.PlaySfx(CozySfxType.Coin);
                PushToast($"💰 Hoàn thành 1 tiếng Phụ Bếp! Nhận +{wage} Xu.");
            }, ActionVisualType.Cooking);
        }

        /// <summary>
        /// Giao diện Siêu Thị Cozy Mart: 2 Tab Mua Nguyên Liệu (Hạt giống, Thịt, Cá, Gỗ, Bánh mì, Nước) và Bán Nông Sản / Món Ăn.
        /// </summary>
        private void DrawMarketModal()
        {
            GUI.depth = -100;
            float w = 560f;
            float h = 420f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f - 10f, w, h);
            DrawSolidRect(winRect, new Color(0.10f, 0.12f, 0.14f, 0.98f));
            DrawBorderRect(winRect, new Color(0.96f, 0.76f, 0.28f), 2f);

            GUILayout.BeginArea(new Rect(winRect.x + 14, winRect.y + 12, w - 28, h - 24));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🏪 SIÊU THỊ THỊ TRẤN — COZY MART", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✖ Đóng [Esc]", buttonStyle, GUILayout.Width(95), GUILayout.Height(24)))
            {
                CloseMarketModal();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"💰 Số dư của bạn: {playerProfInv.Coins} Xu  •  🛒 Quầy tạp hóa & thực phẩm tươi sống", smallStyle);
            GUILayout.Space(6);

            // Tab bar
            GUILayout.BeginHorizontal();
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = (marketActiveTab == 0) ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.22f, 0.24f, 0.28f);
            if (GUILayout.Button("🛒 MUA NGUYÊN LIỆU & THỰC PHẨM", buttonStyle, GUILayout.Height(28)))
            {
                marketActiveTab = 0;
                marketScrollPos = Vector2.zero;
            }
            GUI.backgroundColor = (marketActiveTab == 1) ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.22f, 0.24f, 0.28f);
            if (GUILayout.Button("💰 BÁN NÔNG SẢN & MÓN ĂN", buttonStyle, GUILayout.Height(28)))
            {
                marketActiveTab = 1;
                marketScrollPos = Vector2.zero;
            }
            GUI.backgroundColor = oldBg;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            float contentW = w - 28;
            float contentH = h - 146;

            if (marketActiveTab == 0)
            {
                DrawMarketBuyTab(contentW, contentH);
            }
            else
            {
                DrawMarketSellTab(contentW, contentH);
            }

            GUILayout.Space(6);

            // Ca làm việc Thu Ngân 1 tiếng
            if (GUILayout.Button("💼 Làm Ca Thu Ngân 1 Tiếng (60p) — Nhận +30 Xu", buttonStyle, GUILayout.Height(30)))
            {
                StartMarketShift();
            }

            GUILayout.EndArea();
        }

        private struct MarketGoodItem
        {
            public ItemType item;
            public int price;
            public string note;
            public MarketGoodItem(ItemType i, int p, string n) { item = i; price = p; note = n; }
        }

        private static readonly MarketGoodItem[] marketGoods = new MarketGoodItem[]
        {
            new MarketGoodItem(ItemType.CropSeed, 5, "Hạt giống cây trồng: gieo tại luống ruộng ngoài trời."),
            new MarketGoodItem(ItemType.RawMeat, 15, "Thịt tươi sống: dùng nấu Bò Bít Tết & Burger bò phô mai."),
            new MarketGoodItem(ItemType.RawFish, 12, "Cá tươi sống: dùng nướng giòn thơm bơ sốt cà chua."),
            new MarketGoodItem(ItemType.WoodPlank, 10, "Gỗ thông: nguyên liệu đóng bàn ghế và Bếp Gia Đình."),
            new MarketGoodItem(ItemType.WaterBottle, 8, "Nước tinh khiết (+45 Nước, +15 Sức) hoặc chế biến."),
            new MarketGoodItem(ItemType.BakedBread, 20, "Bánh mì nướng thơm giòn (+45 No) hoặc kẹp Burger."),
            new MarketGoodItem(ItemType.FishingRod, 45, "Cần câu tre bọc thép: Dùng để câu cá bên bờ hồ thị trấn."),
            new MarketGoodItem(ItemType.PetBed, 50, "Đệm êm thú cưng: Dành cho cún cưng Bé Vàng nằm nghỉ ngơi."),
            new MarketGoodItem(ItemType.Bicycle, 150, "Xe đạp dạo phố: Tăng +85% tốc độ di chuyển ngoài trời, bấm [V] để chuông reng!"),
            new MarketGoodItem(ItemType.FloorLamp, 30, "Đèn đứng retro ánh vàng ấm cúng (+12 Điểm Ấm Cúng)."),
            new MarketGoodItem(ItemType.PottedPlant, 25, "Chậu monstera xanh mát trang trí phòng khách (+10 Điểm Ấm Cúng)."),
            new MarketGoodItem(ItemType.VintageGramophone, 180, "Máy hát đĩa than cổ điển: Bấm [E] để phát nhạc Lo-Fi du dương.")
        };

        private void DrawMarketBuyTab(float tabW, float tabH)
        {
            marketScrollPos = GUILayout.BeginScrollView(marketScrollPos, GUILayout.Width(tabW), GUILayout.Height(tabH));

            for (int i = 0; i < marketGoods.Length; i++)
            {
                var g = marketGoods[i];
                var def = CozyLifeDatabase.GetItem(g.item);
                if (def == null) continue;

                Rect rowRect = GUILayoutUtility.GetRect(tabW - 20, 52);
                DrawSolidRect(rowRect, new Color(0.14f, 0.16f, 0.18f, 0.95f));
                DrawBorderRect(rowRect, new Color(0.85f, 0.65f, 0.28f), 1f);

                Texture2D icon = CozyLifeDatabase.LoadIconTexture(def.iconResourcePath);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(rowRect.x + 6, rowRect.y + 6, 40, 40), icon, ScaleMode.ScaleToFit);
                }

                GUI.Label(new Rect(rowRect.x + 52, rowRect.y + 4, 300, 20), $"{def.displayName}  ({g.price} Xu)", titleStyle);
                GUI.Label(new Rect(rowRect.x + 52, rowRect.y + 24, 300, 24), g.note, smallStyle);

                bool canAfford1 = playerProfInv.Coins >= g.price;
                bool canAfford5 = playerProfInv.Coins >= g.price * 5;

                Rect btn1 = new Rect(rowRect.x + rowRect.width - 162, rowRect.y + 11, 74, 30);
                if (GUI.Button(btn1, canAfford1 ? $"Mua x1" : "Thiếu Xu", buttonStyle))
                {
                    if (canAfford1 && playerProfInv.SpendCoins(g.price))
                    {
                        playerProfInv.AddItem(g.item, 1);
                        CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
                        CozySaveManager.SaveGame(false);
                        PushToast($"🛒 Đã mua +1 {def.displayName} (-{g.price} Xu)!");
                    }
                    else
                    {
                        PushToast("⚠️ Không đủ Xu để mua!");
                    }
                }

                Rect btn5 = new Rect(rowRect.x + rowRect.width - 82, rowRect.y + 11, 76, 30);
                if (GUI.Button(btn5, canAfford5 ? $"Mua x5" : $"{g.price * 5} Xu", buttonStyle))
                {
                    if (canAfford5 && playerProfInv.SpendCoins(g.price * 5))
                    {
                        playerProfInv.AddItem(g.item, 5);
                        CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
                        CozySaveManager.SaveGame(false);
                        PushToast($"🛒 Đã mua +5 {def.displayName} (-{g.price * 5} Xu)!");
                    }
                    else
                    {
                        PushToast("⚠️ Không đủ Xu để mua 5 món!");
                    }
                }

                GUILayout.Space(5);
            }

            GUILayout.EndScrollView();
        }

        private void DrawMarketSellTab(float tabW, float tabH)
        {
            marketScrollPos = GUILayout.BeginScrollView(marketScrollPos, GUILayout.Width(tabW), GUILayout.Height(tabH));

            var inv = playerProfInv.Inventory;
            int sellableCount = 0;

            for (int i = 0; i < inv.Count; i++)
            {
                var slot = inv[i];
                if (slot.count <= 0) continue;
                var def = CozyLifeDatabase.GetItem(slot.itemType);
                if (def == null) continue;

                int unitSellPrice = Mathf.Max(4, Mathf.RoundToInt(def.basePrice * 0.85f));
                sellableCount++;

                Rect rowRect = GUILayoutUtility.GetRect(tabW - 20, 52);
                DrawSolidRect(rowRect, new Color(0.14f, 0.17f, 0.16f, 0.95f));
                DrawBorderRect(rowRect, new Color(0.35f, 0.75f, 0.50f), 1f);

                Texture2D icon = CozyLifeDatabase.LoadIconTexture(def.iconResourcePath);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(rowRect.x + 6, rowRect.y + 6, 40, 40), icon, ScaleMode.ScaleToFit);
                }

                GUI.Label(new Rect(rowRect.x + 52, rowRect.y + 4, 280, 20), $"{def.displayName}  (Có: x{slot.count})", titleStyle);
                GUI.Label(new Rect(rowRect.x + 52, rowRect.y + 24, 280, 24), $"Giá thu mua: {unitSellPrice} Xu/cái", smallStyle);

                Rect btn1 = new Rect(rowRect.x + rowRect.width - 180, rowRect.y + 11, 84, 30);
                if (GUI.Button(btn1, $"Bán 1 (+{unitSellPrice})", buttonStyle))
                {
                    ItemType t = slot.itemType;
                    playerProfInv.RemoveItem(t, 1);
                    playerProfInv.AddCoins(unitSellPrice);
                    CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
                    CozySaveManager.SaveGame(false);
                    PushToast($"💰 Đã bán 1 {def.displayName} (+{unitSellPrice} Xu)!");
                    break;
                }

                int totalGain = unitSellPrice * slot.count;
                Rect btnAll = new Rect(rowRect.x + rowRect.width - 90, rowRect.y + 11, 84, 30);
                if (GUI.Button(btnAll, $"Bán Hết (+{totalGain})", buttonStyle))
                {
                    ItemType t = slot.itemType;
                    int count = slot.count;
                    playerProfInv.RemoveItem(t, count);
                    playerProfInv.AddCoins(totalGain);
                    CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
                    CozySaveManager.SaveGame(false);
                    PushToast($"💰 Đã bán {count} {def.displayName} (+{totalGain} Xu)!");
                    break;
                }

                GUILayout.Space(5);
            }

            if (sellableCount == 0)
            {
                GUILayout.Space(25);
                GUILayout.Label("🌾 Túi đồ của bạn hiện chưa có món nào để bán.\n\n• Hãy gieo hạt Lúa Mì & Cà Chua ngoài ruộng để thu hoạch.\n• Hoặc vào bếp nấu Bò Bít Tết, Cá Nướng, Bánh Mì rồi đem bán lấy thật nhiều Xu nhé!", smallStyle);
            }

            GUILayout.EndScrollView();
        }

        private void StartMarketShift()
        {
            if (playerController == null || playerController.IsPerformingAction)
                return;

            if (!playerStats.TryConsumeStamina(12f))
            {
                PushToast("⚠️ Bạn đang mệt, hãy nghỉ ngơi hoặc ăn uống trước!");
                return;
            }

            CloseMarketModal();
            playerController.StartTimedAction("💰 Đang làm ca thu ngân tính tiền tại Siêu Thị", 60f, () =>
            {
                playerProfInv.AddCoins(30);
                CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
                CozySaveManager.SaveGame(false);
                PushToast("🎉 Hoàn thành 1 tiếng ca thu ngân! Nhận lương +30 Xu.");
            }, ActionVisualType.Cashier);
        }

        private void DrawBottomHotbarAndPrompt()
        {
            float barWidth = 468f;
            float barHeight = 62f;
            float startX = Mathf.Max(228f, (Screen.width - barWidth) * 0.5f);
            float startY = Screen.height - barHeight - 12f;

            BuildingType curBld = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentBuilding
                : BuildingType.None;
            bool inOwnedHome = CozyLifeDatabase.IsResidentialHome(curBld) && BuildingDoorInteractable.IsBuildingOwned(curBld);

            // 0. Nếu đang ở trong Nhà Riêng -> Hiển thị Thanh Sandbox Trang Trí Nhà hoặc Nút Mở Trang Trí [B]
            if (inOwnedHome && playerController != null)
            {
                if (playerController.IsDecoratingMode)
                {
                    DrawHomeDecoratingSandboxBar(curBld, startX, startY);
                }
                else
                {
                    int roomScore = CozyFurnitureAura.GetRoomCozyScore(curBld);
                    CozyDiarySystem.RecordFurnitureScore(roomScore);
                    Rect decorBtn = new Rect(startX + barWidth + 8f, startY + 12f, 148f, 38f);
                    DrawSolidRect(decorBtn, new Color(0.22f, 0.16f, 0.32f, 0.95f));
                    DrawBorderRect(decorBtn, new Color(0.88f, 0.65f, 0.98f), 1.5f);
                    if (GUI.Button(decorBtn, $"🛋️ Trang Trí [B]\n✨ Ấm Cúng: {roomScore}đ", buttonStyle))
                    {
                        playerController.ToggleDecoratingMode();
                    }
                }
            }

            // 1. Nếu đang thực hiện hành động tốn thời gian -> Vẽ Thanh Tiến Trình Hành Động (Timed Action Bar)
            if (playerController != null && playerController.IsPerformingAction)
            {
                Rect actionRect = new Rect(startX, startY - 68f, barWidth, 34f);
                DrawSolidRect(actionRect, new Color(0.10f, 0.14f, 0.18f, 0.96f));

                float prog = playerController.ActionProgress01;
                DrawSolidRect(new Rect(actionRect.x + 2, actionRect.y + 2, (actionRect.width - 76f) * prog, actionRect.height - 4),
                    new Color(0.28f, 0.72f, 0.42f, 0.90f));
                DrawBorderRect(actionRect, new Color(0.45f, 0.88f, 0.55f), 1.5f);

                int curMin = Mathf.FloorToInt(playerController.ActionElapsedMinutes);
                int totMin = Mathf.RoundToInt(playerController.ActionTotalMinutes);
                GUI.Label(new Rect(actionRect.x + 10, actionRect.y + 7, actionRect.width - 88f, 20),
                    $"⏳ {playerController.CurrentActionTitle} ({curMin}p / {totMin}p)", titleStyle);

                Rect cancelBtn = new Rect(actionRect.xMax - 68f, actionRect.y + 4f, 62f, 25f);
                if (GUI.Button(cancelBtn, "✖ Hủy", buttonStyle))
                {
                    playerController.CancelTimedAction("⚠️ Đã hủy hành động.");
                }
            }
            else if (playerController != null && playerController.IsSeatedAtTable)
            {
                Rect seatRect = new Rect(startX, startY - 60f, barWidth, 26f);
                DrawSolidRect(seatRect, new Color(0.26f, 0.18f, 0.12f, 0.94f));
                DrawBorderRect(seatRect, new Color(0.95f, 0.72f, 0.28f), 1f);
                GUI.Label(seatRect, "🪑 Đang ngồi bàn — [E]: Mở Thực Đơn  |  [WASD]: Đứng dậy", worldTagStyle);
            }
            else if (!IsAnyModalOrGameWindowOpen() && playerController != null && playerController.NearestInteractable != null)
            {
                string prompt = playerController.NearestInteractable.GetPromptText(playerProfInv);
                Rect promptRect = new Rect(startX, startY - 60f, barWidth, 26f);
                DrawSolidRect(promptRect, new Color(0.14f, 0.22f, 0.17f, 0.94f));
                DrawBorderRect(promptRect, new Color(0.45f, 0.88f, 0.52f), 1f);
                GUI.Label(promptRect, prompt, worldTagStyle);
            }

            // 2. Thông báo ngắn (Toast)
            if (toastTimer > 0f && !string.IsNullOrEmpty(currentToastMessage))
            {
                Rect toastRect = new Rect(startX - 10f, startY - 28f, barWidth + 20f, 22f);
                DrawSolidRect(toastRect, new Color(0.08f, 0.10f, 0.14f, 0.88f));
                GUI.Label(toastRect, currentToastMessage, worldTagStyle);
            }

            // 3. Thanh Hotbar 8 ô
            Rect hotbarRect = new Rect(startX, startY, barWidth, barHeight);
            DrawSolidRect(hotbarRect, new Color(0.11f, 0.13f, 0.16f, 0.92f));
            DrawBorderRect(hotbarRect, new Color(0.35f, 0.40f, 0.48f), 1.5f);

            var inv = playerProfInv.Inventory;
            float slotSize = 50f;
            float gap = 6f;
            float slotStartX = startX + 13f;

            for (int i = 0; i < 8; i++)
            {
                Rect sRect = new Rect(slotStartX + i * (slotSize + gap), startY + 6f, slotSize, slotSize);
                bool isSelected = (playerProfInv.SelectedSlotIndex == i);

                DrawSolidRect(sRect, isSelected ? new Color(0.26f, 0.36f, 0.28f, 0.95f) : new Color(0.17f, 0.19f, 0.23f, 0.95f));
                DrawBorderRect(sRect, isSelected ? new Color(0.95f, 0.78f, 0.30f) : new Color(0.30f, 0.33f, 0.38f), isSelected ? 2f : 1f);

                GUI.Label(new Rect(sRect.x + 3, sRect.y + 1, 16, 14), $"{i + 1}", smallStyle);

                if (i < inv.Count)
                {
                    var slot = inv[i];
                    var itemDef = CozyLifeDatabase.GetItem(slot.itemType);
                    if (itemDef != null)
                    {
                        Texture2D iconTex = CozyLifeDatabase.LoadIconTexture(itemDef.iconResourcePath);
                        if (iconTex != null)
                        {
                            GUI.DrawTexture(new Rect(sRect.x + 11, sRect.y + 6, 28, 28), iconTex, ScaleMode.ScaleToFit);
                        }

                        GUIStyle countStyle = new GUIStyle(smallStyle)
                        {
                            fontSize = 10,
                            alignment = TextAnchor.LowerRight,
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = new Color(1f, 0.92f, 0.55f) }
                        };
                        GUI.Label(new Rect(sRect.x, sRect.y, sRect.width - 3, sRect.height - 13), $"x{slot.count}", countStyle);

                        GUIStyle nameStyle = new GUIStyle(smallStyle) { fontSize = 8, alignment = TextAnchor.LowerCenter };
                        GUI.Label(new Rect(sRect.x, sRect.y, sRect.width, sRect.height - 1), itemDef.displayName, nameStyle);

                        if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                        {
                            playerProfInv.UseItemAtSlot(i);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Bảng điều khiển Home Decorating Sandbox khi người chơi bấm [B] bên trong ngôi nhà thuộc sở hữu.
        /// Tách biệt rõ ràng 2 Chế Độ: [🔨 Đặt Mới] (chuột trái luôn đặt đồ) và [✋ Dời Đồ] (chuột trái nhấc đồ đã đặt để dời vị trí).
        /// </summary>
        private void DrawHomeDecoratingSandboxBar(BuildingType curBld, float hotbarX, float hotbarY)
        {
            float w = Mathf.Min(Screen.width - 40f, 760f);
            float h = 96f;
            float x = (Screen.width - w) * 0.5f;
            float y = hotbarY - h - 30f;

            Rect box = new Rect(x, y, w, h);
            DrawSolidRect(box, new Color(0.10f, 0.11f, 0.16f, 0.96f));
            DrawBorderRect(box, playerController.IsRepositionMode ? new Color(1f, 0.78f, 0.32f) : new Color(0.85f, 0.62f, 0.96f), 2f);

            int roomScore = CozyFurnitureAura.GetRoomCozyScore(curBld);
            int bonusPct = 60 + Mathf.Clamp(Mathf.RoundToInt(roomScore * 0.5f), 0, 90);

            // ==========================================
            // HÀNG 1: 2 TAB CHẾ ĐỘ + ĐIỂM ẤM CÚNG + CÁC NÚT THAO TÁC
            // ==========================================
            bool isMoveMode = playerController.IsRepositionMode;
            bool isHolding = playerController.IsMovingExistingFurniture;

            // Tab 1: [🔨 Đặt Mới]
            Rect tabPlaceRect = new Rect(x + 8f, y + 5f, 96f, 22f);
            Color tabPlaceBg = !isMoveMode ? new Color(0.24f, 0.55f, 0.34f, 0.96f) : new Color(0.16f, 0.17f, 0.24f, 0.90f);
            Color tabPlaceBorder = !isMoveMode ? new Color(0.55f, 1f, 0.65f) : new Color(0.38f, 0.38f, 0.50f);
            DrawSolidRect(tabPlaceRect, tabPlaceBg);
            DrawBorderRect(tabPlaceRect, tabPlaceBorder, !isMoveMode ? 2f : 1f);
            GUIStyle tabSt = new GUIStyle(buttonStyle) { fontSize = 11, fontStyle = FontStyle.Bold };
            if (GUI.Button(tabPlaceRect, "🔨 Đặt Mới", tabSt))
            {
                playerController.SetDecorSubMode(false);
            }

            // Tab 2: [✋ Dời Đồ [M]]
            Rect tabMoveRect = new Rect(x + 108f, y + 5f, 106f, 22f);
            Color tabMoveBg = isMoveMode ? new Color(0.68f, 0.44f, 0.12f, 0.96f) : new Color(0.16f, 0.17f, 0.24f, 0.90f);
            Color tabMoveBorder = isMoveMode ? new Color(1f, 0.88f, 0.35f) : new Color(0.38f, 0.38f, 0.50f);
            DrawSolidRect(tabMoveRect, tabMoveBg);
            DrawBorderRect(tabMoveRect, tabMoveBorder, isMoveMode ? 2f : 1f);
            if (GUI.Button(tabMoveRect, "✋ Dời Đồ [M]", tabSt))
            {
                playerController.SetDecorSubMode(true);
            }

            // Điểm Ấm Cúng Phòng ở giữa
            GUIStyle scoreSt = new GUIStyle(smallStyle) { fontSize = 9, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.95f, 0.88f, 1f) } };
            GUI.Label(new Rect(x + 220f, y + 7f, 150f, 18f), $"✨ {roomScore}đ (+{bonusPct}% Hồi)", scoreSt);

            // Các nút thao tác góc phải
            GUIStyle miniBtn = new GUIStyle(buttonStyle) { fontSize = 10 };
            if (isHolding)
            {
                if (GUI.Button(new Rect(x + w - 248f, y + 5f, 74f, 22f), "↩ Hủy [ESC]", miniBtn))
                    playerController.CancelMovingFurniture();
                if (GUI.Button(new Rect(x + w - 170f, y + 5f, 60f, 22f), "🔄 Xoay [R]", miniBtn))
                    playerController.FlipDecorGhost();
                if (GUI.Button(new Rect(x + w - 106f, y + 5f, 52f, 22f), "📦 Cất", miniBtn))
                    playerController.StoreMovingFurnitureToInventory();
                if (GUI.Button(new Rect(x + w - 50f, y + 5f, 44f, 22f), "✖ Đóng", miniBtn))
                    playerController.ToggleDecoratingMode();
            }
            else if (isMoveMode)
            {
                if (GUI.Button(new Rect(x + w - 156f, y + 5f, 54f, 22f), "🔄 Xoay", miniBtn))
                    playerController.FlipDecorGhost();
                if (GUI.Button(new Rect(x + w - 98f, y + 5f, 52f, 22f), "📦 Cất [X]", miniBtn))
                    playerController.TryPickupFurnitureNearCursorOrPlayer();
                if (GUI.Button(new Rect(x + w - 44f, y + 5f, 38f, 22f), "✖", miniBtn))
                    playerController.ToggleDecoratingMode();
            }
            else
            {
                if (GUI.Button(new Rect(x + w - 162f, y + 5f, 64f, 22f), "🔄 Xoay [R]", miniBtn))
                    playerController.FlipDecorGhost();
                if (GUI.Button(new Rect(x + w - 94f, y + 5f, 52f, 22f), "📦 Cất [X]", miniBtn))
                    playerController.TryPickupFurnitureNearCursorOrPlayer();
                if (GUI.Button(new Rect(x + w - 40f, y + 5f, 34f, 22f), "✖", miniBtn))
                    playerController.ToggleDecoratingMode();
            }

            // ==========================================
            // HÀNG 2: NỘI DUNG THEO CHẾ ĐỘ
            // ==========================================
            if (isMoveMode)
            {
                // BANNER CHẾ ĐỘ DỜI ĐỒ
                Rect bannerRect = new Rect(x + 8f, y + 32f, w - 16f, 58f);
                if (isHolding)
                {
                    DrawSolidRect(bannerRect, new Color(0.24f, 0.16f, 0.08f, 0.95f));
                    DrawBorderRect(bannerRect, new Color(1f, 0.82f, 0.35f), 1.5f);
                    GUIStyle holdTitleSt = new GUIStyle(titleStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.92f, 0.45f) } };
                    GUI.Label(new Rect(bannerRect.x, bannerRect.y + 7f, bannerRect.width, 18f), $"✋ ĐANG CẦM: {playerController.MovingFurnitureTitle.ToUpper()}", holdTitleSt);
                    GUIStyle holdSubSt = new GUIStyle(smallStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                    GUI.Label(new Rect(bannerRect.x, bannerRect.y + 29f, bannerRect.width, 18f), "[Click Chuột Trái]: Đặt vị trí mới  •  [R / Chuột Phải]: Xoay  •  [ESC]: Hủy  •  [X]: Cất", holdSubSt);
                }
                else
                {
                    DrawSolidRect(bannerRect, new Color(0.12f, 0.14f, 0.20f, 0.95f));
                    DrawBorderRect(bannerRect, new Color(0.40f, 0.70f, 0.95f), 1.5f);
                    GUIStyle moveTitleSt = new GUIStyle(titleStyle) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.45f, 0.88f, 1f) } };
                    GUI.Label(new Rect(bannerRect.x, bannerRect.y + 7f, bannerRect.width, 18f), "✋ CHẾ ĐỘ DỜI VỊ TRÍ NỘI THẤT", moveTitleSt);
                    GUIStyle moveSubSt = new GUIStyle(smallStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.88f, 0.92f, 0.98f) } };
                    GUI.Label(new Rect(bannerRect.x, bannerRect.y + 29f, bannerRect.width, 18f), "👉 Di chuột vào món đồ muốn dời và [Click Chuột Trái] để nhấc lên  •  Bấm [🔨 Đặt Mới] hoặc [M] để quay lại", moveSubSt);
                }
            }
            else
            {
                // DANH SÁCH TOÀN BỘ NỘI THẤT TRONG CHẾ ĐỘ ĐẶT MỚI
                var furnList = CozyLifeDatabase.GetAllFurnitureItems();
                float cardW = 90f;
                float cardH = 54f;
                float totalW = furnList.Count * (cardW + 6f);
                Rect viewRect = new Rect(x + 10f, y + 32f, w - 20f, 60f);
                decorScrollPos = GUI.BeginScrollView(viewRect, decorScrollPos, new Rect(0, 0, totalW, cardH), false, false);

                for (int i = 0; i < furnList.Count; i++)
                {
                    var item = furnList[i];
                    float cx = i * (cardW + 6f);
                    Rect cRect = new Rect(cx, 0, cardW, cardH);

                    bool isSelected = (playerController.SelectedDecorFurnitureType == item.type);
                    int invCount = playerProfInv.GetItemCount(item.type);

                    DrawSolidRect(cRect, isSelected ? new Color(0.28f, 0.22f, 0.42f, 0.98f) : new Color(0.16f, 0.15f, 0.22f, 0.95f));
                    DrawBorderRect(cRect, isSelected ? new Color(1f, 0.85f, 0.35f) : new Color(0.42f, 0.38f, 0.55f), isSelected ? 2f : 1f);

                    Texture2D icon = CozyLifeDatabase.LoadIconTexture(item.iconResourcePath);
                    if (icon != null)
                    {
                        GUI.DrawTexture(new Rect(cRect.x + 4, cRect.y + 4, 26, 26), icon, ScaleMode.ScaleToFit);
                    }

                    GUIStyle fNameSt = new GUIStyle(smallStyle) { fontSize = 9, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                    GUI.Label(new Rect(cRect.x + 32, cRect.y + 2, cRect.width - 34, 16), item.displayName, fNameSt);

                    GUIStyle cozySt = new GUIStyle(smallStyle) { fontSize = 8, normal = { textColor = new Color(0.75f, 0.95f, 0.65f) } };
                    GUI.Label(new Rect(cRect.x + 32, cRect.y + 17, cRect.width - 34, 14), $"+{item.cozyScoreBonus}đ", cozySt);

                    string costTag = invCount > 0 ? $"Túi: x{invCount}" : $"{item.basePrice} Xu";
                    Color tagCol = invCount > 0 ? new Color(0.45f, 0.95f, 0.60f) : new Color(1f, 0.86f, 0.45f);
                    GUIStyle costSt = new GUIStyle(smallStyle) { fontSize = 9, alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold, normal = { textColor = tagCol } };
                    GUI.Label(new Rect(cRect.x + 2, cRect.y + 34, cRect.width - 4, 18), costTag, costSt);

                    if (GUI.Button(cRect, GUIContent.none, GUIStyle.none))
                    {
                        playerController.SelectDecorFurniture(item.type);
                    }
                }
                GUI.EndScrollView();
            }
        }

        private void DrawProfessionSelectorWindow()
        {
            GUI.depth = -100;
            float w = 480f;
            float h = 330f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            DrawSolidRect(winRect, new Color(0.10f, 0.12f, 0.16f, 0.97f));
            DrawBorderRect(winRect, new Color(0.90f, 0.72f, 0.32f), 2f);

            GUILayout.BeginArea(new Rect(winRect.x + 14, winRect.y + 12, w - 28, h - 24));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🎭 CHỌN NGHỀ NGHIỆP", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Đóng [P]", buttonStyle, GUILayout.Width(76)))
                showProfessionModal = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            foreach (var prof in CozyLifeDatabase.GetAllProfessions())
            {
                bool isCurrent = (playerProfInv.CurrentProfession == prof.type);
                Rect cardRect = GUILayoutUtility.GetRect(w - 32, 58);
                DrawSolidRect(cardRect, isCurrent ? new Color(0.20f, 0.30f, 0.22f, 0.95f) : new Color(0.15f, 0.18f, 0.22f, 0.95f));
                DrawBorderRect(cardRect, prof.themeColor, isCurrent ? 2f : 1f);

                GUI.Label(new Rect(cardRect.x + 10, cardRect.y + 5, 200, 20), prof.titleBadge, titleStyle);
                GUI.Label(new Rect(cardRect.x + 10, cardRect.y + 24, cardRect.width - 125, 32), prof.passiveBonusText.Replace("\n", " | "), smallStyle);

                Rect btnRect = new Rect(cardRect.x + cardRect.width - 110, cardRect.y + 13, 100, 30);
                if (GUI.Button(btnRect, isCurrent ? "✓ Đang Chọn" : "Chọn Nghề", buttonStyle))
                {
                    playerProfInv.ChangeProfession(prof.type, true);
                    showProfessionModal = false;
                }
                GUILayout.Space(4);
            }

            GUILayout.EndArea();
        }

        private void DrawCraftingWindow()
        {
            GUI.depth = -100;
            float w = 520f;
            float h = 360f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            DrawSolidRect(winRect, new Color(0.10f, 0.12f, 0.16f, 0.97f));
            DrawBorderRect(winRect, new Color(0.42f, 0.78f, 0.92f), 2f);

            StationType nearbyStation = playerController != null ? playerController.CurrentNearbyStation : StationType.None;

            GUILayout.BeginArea(new Rect(winRect.x + 14, winRect.y + 12, w - 28, h - 24));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"🔨 CHẾ TẠO TẠI TRẠM: {ProfessionAndInventory.GetStationVietnameseName(nearbyStation)}", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Đóng [C]", buttonStyle, GUILayout.Width(76)))
                showCraftingModal = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            var recipes = CozyLifeDatabase.GetAllRecipes();
            int shownCount = 0;

            craftingScrollPos = GUILayout.BeginScrollView(craftingScrollPos, GUILayout.Width(w - 28), GUILayout.Height(h - 68));

            for (int i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                // Mỗi khu vực chỉ hiển thị công việc thuộc đúng trạm nghề tại nơi đó
                if (r.requiredStation != nearbyStation)
                    continue;

                shownCount++;
                Rect rowRect = GUILayoutUtility.GetRect(w - 52, 54);
                bool profOk = (!r.requireExactProfession || playerProfInv.CurrentProfession == r.recommendedProfession);

                DrawSolidRect(rowRect, profOk ? new Color(0.16f, 0.24f, 0.20f, 0.96f) : new Color(0.16f, 0.15f, 0.18f, 0.92f));
                DrawBorderRect(rowRect, profOk ? new Color(0.38f, 0.78f, 0.48f) : new Color(0.65f, 0.40f, 0.35f), 1.5f);

                string ingText = "";
                foreach (var ing in r.ingredients)
                {
                    var ingDef = CozyLifeDatabase.GetItem(ing.item);
                    int have = playerProfInv.GetItemCount(ing.item);
                    ingText += $"{ingDef.displayName} {have}/{ing.count}  ";
                }

                GUI.Label(new Rect(rowRect.x + 10, rowRect.y + 5, 340, 20), r.displayName, titleStyle);
                GUI.Label(new Rect(rowRect.x + 10, rowRect.y + 26, 350, 20), $"Cần: {ingText} ({r.description})", smallStyle);

                Rect btnRect = new Rect(rowRect.x + rowRect.width - 102, rowRect.y + 11, 92, 32);
                if (GUI.Button(btnRect, "Làm Ngay", buttonStyle))
                {
                    playerProfInv.TryCraftRecipe(r, nearbyStation, out _);
                }

                GUILayout.Space(6);
            }

            if (shownCount == 0)
            {
                Rect guideRect = GUILayoutUtility.GetRect(w - 48, 205);
                DrawSolidRect(guideRect, new Color(0.14f, 0.16f, 0.20f, 0.95f));
                DrawBorderRect(guideRect, new Color(0.85f, 0.68f, 0.30f, 0.8f), 1f);

                string guideText =
                    "📍 MỖI KHU VỰC CÓ CHỨC NĂNG RIÊNG ĐỘC ĐÁO:\n" +
                    "Hãy di chuyển tới đúng nơi làm việc và đứng gần trạm nghề để thao tác:\n\n" +
                    "• 🌱 Nông Trại: Gieo hạt, Thu hoạch Lúa Mì & Cà Chua [E], Tách hạt giống [C]\n" +
                    "• 🍽️ Nhà Hàng: Nấu Bò Bít Tết, Cá Nướng, Burger, Bánh Mì, Súp [E/C], Gọi món & Phụ Bếp\n" +
                    "• ⚕️ Phòng Khám: Sơ cứu [E], Pha Trà Thảo Mộc & Chế Túi Sơ Cứu tại Tủ Thuốc [C]\n" +
                    "• 🔨 Xưởng Mộc: Xẻ Gỗ Thông [E] & Đóng Bếp Nấu Ăn Gia Đình, 6 mẫu nội thất [C]\n" +
                    "• 🏪 Siêu Thị: Mua Hạt Giống, Thịt, Cá, Gỗ & Bán Nông Sản tại Quầy Hàng [E]\n" +
                    "• 🏠 Nhà Riêng: Tự nấu ăn tại Bếp Gia Đình [E], Ngủ nghỉ ngơi & Trang Trí Nhà [B]";

                GUI.Label(new Rect(guideRect.x + 12, guideRect.y + 10, guideRect.width - 24, guideRect.height - 20), guideText, smallStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawFriendsWindow()
        {
            GUI.depth = -100;
            float w = 490f;
            float h = 355f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            DrawSolidRect(winRect, new Color(0.10f, 0.12f, 0.16f, 0.97f));
            DrawBorderRect(winRect, new Color(0.55f, 0.85f, 0.48f), 2f);

            GUILayout.BeginArea(new Rect(winRect.x + 14, winRect.y + 12, w - 28, h - 24));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🤝 DANH SÁCH BẠN BÈ", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Đóng [Tab]", buttonStyle, GUILayout.Width(84)))
                showFriendsModal = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            foreach (var f in TownFriendAgent.AllFriends)
            {
                if (f == null) continue;
                var prof = CozyLifeDatabase.GetProfession(f.FriendProfession);
                Rect cardRect = GUILayoutUtility.GetRect(w - 32, 60);
                DrawSolidRect(cardRect, new Color(0.15f, 0.18f, 0.22f, 0.95f));
                DrawBorderRect(cardRect, prof.themeColor, 1.5f);

                string locName = CozyLifeDatabase.GetBuildingVietnameseName(f.AssignedBuilding);
                GUI.Label(new Rect(cardRect.x + 10, cardRect.y + 5, 320, 20), $"{prof.titleBadge} - {f.FriendName} (❤️ {f.FriendshipPoints})", titleStyle);
                GUI.Label(new Rect(cardRect.x + 10, cardRect.y + 25, 330, 18), $"📍 {locName}  |  Máu: {Mathf.RoundToInt(f.FriendHealth)}%  |  No: {Mathf.RoundToInt(f.FriendHunger)}%", smallStyle);

                Rect gotoBtn = new Rect(cardRect.x + cardRect.width - 112, cardRect.y + 14, 102, 30);
                if (GUI.Button(gotoBtn, "Đến Chỗ Bạn", buttonStyle))
                {
                    var capturedFriend = f;
                    PlaySceneTransition($"🚪 Đến chỗ {capturedFriend.FriendName}...", () =>
                    {
                        CozyTownWorldBuilder.Instance?.SetCurrentBuildingState(capturedFriend.AssignedBuilding);
                        playerController.StandUpFromTable();
                        playerController.TeleportToPosition(capturedFriend.transform.position + new Vector3(0f, -0.45f, 0f));
                        capturedFriend.Say("Chào cậu ghé chơi nhé! 👋", 4f);
                    });
                }

                GUILayout.Space(5);
            }

            GUILayout.EndArea();
        }

        private void DrawSceneTransitionOverlay()
        {
            float normalized = Mathf.Clamp01(transitionTimer / transitionDuration);
            float alpha = Mathf.Clamp01((1f - Mathf.Abs(normalized * 2f - 1f)) * 1.35f);

            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.05f, 0.07f, alpha));

            if (alpha > 0.35f && !string.IsNullOrEmpty(transitionBannerText))
            {
                Rect bannerRect = new Rect(0, Screen.height * 0.45f, Screen.width, 45f);
                GUI.Label(bannerRect, transitionBannerText, bannerStyle);
            }
        }

        public static bool IsAnyModalOrGameWindowOpen()
        {
            if (Instance != null)
            {
                if (Instance.showMarketModal || Instance.showRestaurantOrderModal ||
                    Instance.showProfessionModal || Instance.showCraftingModal ||
                    Instance.showFriendsModal)
                    return true;
            }
            if (TownBulletinBoardInteractable.IsAnyModalOpen) return true;
            if (TownStreetFoodCartInteractable.IsAnyModalOpen) return true;
            if (CozyDiarySystem.IsAnyModalOpen) return true;
            if (CozyJapaneseCafeManager.IsMenuOpen) return true;
            if (CozyWerewolfManager.IsGameActive) return true;
            if (CozyChessManager.IsChessSceneActive) return true;
            if (CozyXiangqiManager.IsXiangqiSceneActive) return true;
            if (CozySportsMinigameManager.IsGameActive) return true;
            return false;
        }

        private void DrawWorldLabelsAndBubbles()
        {
            if (IsAnyModalOrGameWindowOpen()) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // 1. Vẽ Thanh Tiến Trình Hành Động ngay trên đầu nhân vật (giống Project Zomboid)
            if (playerController != null && playerController.IsPerformingAction)
            {
                Vector3 headWorld = playerController.transform.position + new Vector3(0f, 0.82f, 0f);
                Vector3 headScreen = cam.WorldToScreenPoint(headWorld);
                if (headScreen.z > 0)
                {
                    float hx = headScreen.x;
                    float hy = Screen.height - headScreen.y;
                    Rect barBg = new Rect(hx - 65f, hy - 14f, 130f, 16f);
                    DrawSolidRect(barBg, new Color(0.06f, 0.08f, 0.10f, 0.90f));
                    DrawSolidRect(new Rect(barBg.x + 2, barBg.y + 2, (barBg.width - 4) * playerController.ActionProgress01, barBg.height - 4),
                        new Color(0.32f, 0.85f, 0.45f));
                    DrawBorderRect(barBg, Color.white, 1f);
                }
            }

            // 2. Chỉ hiện nhãn trên đầu vật thể khi ở gần (<= 3.8m) để màn hình thoáng đãng, không bị rối
            foreach (var inter in WorldInteractableBase.AllInteractables)
            {
                if (inter == null) continue;
                float dist = Vector2.Distance(playerStats.transform.position, inter.transform.position);
                if (dist > 3.8f) continue;

                Vector3 worldPos = inter.transform.position + new Vector3(0f, 0.58f, 0f);
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0) continue;

                float guiX = screenPos.x;
                float guiY = Screen.height - screenPos.y;

                string label = inter.GetWorldOverheadLabel();
                if (!string.IsNullOrEmpty(label))
                {
                    float tagW = Mathf.Max(96f, label.Length * 6.8f);
                    Rect tagRect = new Rect(guiX - tagW * 0.5f, guiY - 10f, tagW, 20f);
                    DrawSolidRect(tagRect, new Color(0.08f, 0.10f, 0.14f, 0.78f));
                    GUI.Label(tagRect, label, worldTagStyle);
                }

                if (inter is TownFriendAgent friend && !string.IsNullOrEmpty(friend.CurrentChatBubble))
                {
                    Rect bubbleRect = new Rect(guiX - 105f, guiY - 46f, 210f, 32f);
                    DrawSolidRect(bubbleRect, new Color(0.16f, 0.22f, 0.28f, 0.92f));
                    DrawBorderRect(bubbleRect, CozyLifeDatabase.GetProfession(friend.FriendProfession).themeColor, 1f);
                    GUI.Label(bubbleRect, friend.CurrentChatBubble, chatBubbleStyle);
                }
            }

            // 3. Highlight món đồ nội thất đang hover trong Chế Độ Dời Đồ (Move Mode)
            if (playerController != null && playerController.IsDecoratingMode && playerController.IsRepositionMode && !playerController.IsMovingExistingFurniture)
            {
                var hf = playerController.HoveredFurniture;
                if (hf != null)
                {
                    Vector3 fScreen = cam.WorldToScreenPoint(hf.transform.position + new Vector3(0f, 0.42f, 0f));
                    if (fScreen.z > 0)
                    {
                        float fx = fScreen.x;
                        float fy = Screen.height - fScreen.y;
                        string tag = $"✋ [Click Trái]: Nhấc {hf.FurnitureTitle}";
                        float tagW = Mathf.Max(120f, tag.Length * 7.5f);
                        Rect tagRect = new Rect(fx - tagW * 0.5f, fy - 12f, tagW, 24f);
                        DrawSolidRect(tagRect, new Color(0.12f, 0.09f, 0.05f, 0.94f));
                        DrawBorderRect(tagRect, new Color(1f, 0.82f, 0.32f), 1.8f);
                        GUIStyle hoverSt = new GUIStyle(worldTagStyle)
                        {
                            fontSize = 11,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = new Color(1f, 0.92f, 0.45f) }
                        };
                        GUI.Label(tagRect, tag, hoverSt);
                    }
                }
            }
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex);
            GUI.color = prev;
        }

        private void DrawBorderRect(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
