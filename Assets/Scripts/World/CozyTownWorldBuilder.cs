using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CozyLife.Core;
using CozyLife.UI;

namespace CozyLife.World
{
    /// <summary>
    /// Quản lý bản đồ Thế Giới Mở (Ngoài trời) và các Không Gian Bên Trong Tòa Nhà (Interior Scenes).
    /// Mỗi khu vực chỉ giữ đúng chức năng chuyên biệt của nghề đó:
    /// - Ngoài Trời (Nông Trại & Quảng Trường): Trồng trọt, thu hoạch, tách hạt giống và lấy nước tại Giếng Nước.
    /// - Nhà Hàng (Đầu Bếp): Nấu ăn tại Bếp Lò, làm Phụ Bếp 1 tiếng, ngồi Bàn Ăn gọi món.
    /// - Phòng Khám (Bác Sĩ): Sơ cứu, pha chế thuốc tại Tủ Thuốc, nằm Giường Bệnh điều trị.
    /// - Siêu Thị: Mua hạt giống, vật liệu và bán nông sản tại Quầy Hàng.
    /// - Xưởng Thợ Mộc (Thợ Mộc): Xẻ Gỗ Thông và đóng nội thất tại Bàn Cưa.
    /// - Nhà Riêng Của Bạn: Ngủ nghỉ ngơi trên Giường Ngủ và đặt trang trí nội thất.
    /// Tích hợp chu kỳ Ngày / Đêm trực quan (Bình minh -> Ban ngày -> Hoàng hôn -> Ban đêm + Đèn đường tự bật sáng).
    /// </summary>
    public class CozyTownWorldBuilder : MonoBehaviour
    {
        public static CozyTownWorldBuilder Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance()
        {
            Instance = null;
        }

        [Header("Day / Night Cycle (1 phút thực = 1 tiếng game)")]
        [SerializeField] private int currentDay = 1;
        [SerializeField] private float currentHour = 8.0f;
        [SerializeField] private float hoursPerRealSecond = 1f / 60f; // 60 giây thực tế = 1 tiếng trong game

        [Header("Weather System (Kéo dài 12 tiếng game)")]
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private float weatherHoursRemaining = 0f;
        [SerializeField] private float nextWeatherEventHours = 28f; // Sau hơn 1 ngày đầu, sau đó cách 1.5 - 2.5 ngày
        private int weatherEventCounter = 0;

        private Transform worldRoot;
        private Camera mainCam;
        private Light2D globalLight2D;
        private readonly List<SpriteRenderer> streetLightGlows = new List<SpriteRenderer>();
        private bool wasNightTime = false;

        private BuildingType currentBuilding = BuildingType.None;
        private Vector3 lastOutdoorDoorPosition = Vector3.zero;
        private readonly Dictionary<BuildingType, Vector3> interiorEntrancePositions = new Dictionary<BuildingType, Vector3>();
        private readonly Dictionary<BuildingType, Vector3> outdoorDoorPositions = new Dictionary<BuildingType, Vector3>();

        public int CurrentDay => currentDay;
        public float CurrentHour => currentHour;
        public BuildingType CurrentBuilding => currentBuilding;
        public bool IsNightTime => currentHour >= 18.5f || currentHour < 5.5f;

        public WeatherType CurrentWeather => currentWeather;
        public float WeatherHoursRemaining => weatherHoursRemaining;
        public float NextWeatherEventHours => nextWeatherEventHours;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CozyLifeDatabase.EnsureInitialized();
            CozyAudioManager.EnsureInstance();
            CozySaveManager.EnsureInstance();

            mainCam = Camera.main;
            GameObject lightObj = GameObject.Find("Global Light 2D");
            if (lightObj != null)
            {
                globalLight2D = lightObj.GetComponent<Light2D>();
            }

            BuildIsometricTownAndInteriors();
            UpdateSkyAndLighting();

            if (CozySaveManager.HasSaveData())
            {
                CozySaveManager.LoadGame(false);
            }
        }

        public void RestoreWorldStateFromSave(int day, float hour, WeatherType weather, float weatherRem, float nextWeather, BuildingType bld)
        {
            currentDay = Mathf.Max(1, day);
            currentHour = Mathf.Clamp(hour, 0f, 23.99f);
            currentWeather = weather;
            weatherHoursRemaining = Mathf.Max(0f, weatherRem);
            nextWeatherEventHours = Mathf.Max(2f, nextWeather);
            SetCurrentBuildingState(bld);
            UpdateSkyAndLighting();
        }

        private void Update()
        {
            currentHour += hoursPerRealSecond * Time.deltaTime;
            if (currentHour >= 24f)
            {
                currentHour -= 24f;
                currentDay++;
                CozyLifeHUD.PushToast($"🌅 Bình minh Ngày {currentDay}!");
            }

            bool nowNight = IsNightTime;
            if (nowNight && !wasNightTime)
            {
                CozyLifeHUD.PushToast("🌙 Trời đã tối, đèn đường thị trấn đã bật sáng!");
            }
            wasNightTime = nowNight;

            UpdateWeatherCycle();
            UpdateSkyAndLighting();
        }

        private void UpdateWeatherCycle()
        {
            float dtHours = hoursPerRealSecond * Time.deltaTime;

            if (currentWeather != WeatherType.Clear)
            {
                weatherHoursRemaining -= dtHours;
                if (weatherHoursRemaining <= 0f)
                {
                    weatherHoursRemaining = 0f;
                    currentWeather = WeatherType.Clear;
                    nextWeatherEventHours = UnityEngine.Random.Range(36f, 60f); // 1.5 - 2.5 ngày tiếp theo
                    CozyLifeHUD.PushToast("🌤️ Thời tiết đã tạnh ráo, trời trở lại nắng ấm bình thường!");
                }
            }
            else
            {
                nextWeatherEventHours -= dtHours;
                if (nextWeatherEventHours <= 0f)
                {
                    WeatherType[] badWeathers = { WeatherType.Rain, WeatherType.Heatwave, WeatherType.Snow };
                    currentWeather = badWeathers[weatherEventCounter % badWeathers.Length];
                    weatherEventCounter++;
                    weatherHoursRemaining = 12.0f; // Kéo dài đúng 12 tiếng trong game!

                    string weatherMsg = currentWeather switch
                    {
                        WeatherType.Rain => "🌧️ Bắt đầu đổ MƯA RÀO LỚN (12 tiếng)! Đừng đi mưa lâu kẻo bị sốt!",
                        WeatherType.Heatwave => "☀️🔥 Thời tiết chuyển sang NẮNG GẮT thiêu đốt (12 tiếng)! Coi chừng sốc nhiệt!",
                        WeatherType.Snow => "❄️ BÃO TUYẾT buốt giá bắt đầu đổ xuống (12 tiếng)! Coi chừng cảm lạnh!",
                        _ => ""
                    };
                    CozyLifeHUD.PushToast(weatherMsg);
                }
            }
        }

        public void CycleWeatherForTesting()
        {
            currentWeather = currentWeather switch
            {
                WeatherType.Clear => WeatherType.Rain,
                WeatherType.Rain => WeatherType.Heatwave,
                WeatherType.Heatwave => WeatherType.Snow,
                WeatherType.Snow => WeatherType.Clear,
                _ => WeatherType.Clear
            };

            if (currentWeather != WeatherType.Clear)
            {
                weatherHoursRemaining = 12.0f;
                string msg = currentWeather switch
                {
                    WeatherType.Rain => "🌧️ Đã chuyển: MƯA RÀO LỚN (12 tiếng)",
                    WeatherType.Heatwave => "☀️🔥 Đã chuyển: NẮNG GẮT (12 tiếng)",
                    WeatherType.Snow => "❄️ Đã chuyển: BÃO TUYẾT (12 tiếng)",
                    _ => ""
                };
                CozyLifeHUD.PushToast(msg);
            }
            else
            {
                weatherHoursRemaining = 0f;
                nextWeatherEventHours = UnityEngine.Random.Range(36f, 60f);
                CozyLifeHUD.PushToast("🌤️ Đã chuyển: TRỜI QUANG NẮNG RÁO");
            }
        }

        public string GetWeatherDisplayTitle()
        {
            return currentWeather switch
            {
                WeatherType.Rain => $"🌧️ Mưa ({weatherHoursRemaining:0.0}h)",
                WeatherType.Heatwave => $"☀️🔥 Nắng Gắt ({weatherHoursRemaining:0.0}h)",
                WeatherType.Snow => $"❄️ Tuyết ({weatherHoursRemaining:0.0}h)",
                _ => "🌤️ Nắng Ráo"
            };
        }

        public string GetDayPhaseLabel()
        {
            if (currentHour >= 5f && currentHour < 7.5f)
                return "🌅 Bình Minh";
            if (currentHour >= 7.5f && currentHour < 16.5f)
                return "☀️ Ban Ngày";
            if (currentHour >= 16.5f && currentHour < 19f)
                return "🌇 Hoàng Hôn";
            return "🌙 Ban Đêm";
        }

        public string GetFormattedTime()
        {
            int hh = Mathf.FloorToInt(currentHour);
            int mm = Mathf.FloorToInt((currentHour - hh) * 60f);
            return $"{GetDayPhaseLabel()} | Ngày {currentDay} - {hh:00}:{mm:00}";
        }

        /// <summary>
        /// Màu phủ bầu khí quyển ngoài trời để làm rõ hiệu ứng Bình Minh / Hoàng Hôn / Ban Đêm.
        /// </summary>
        public Color GetOutdoorAtmosphericTint()
        {
            if (currentBuilding != BuildingType.None)
                return Color.clear;

            Color baseTint;
            if (currentHour >= 5f && currentHour < 7.5f)
            {
                float t = (currentHour - 5f) / 2.5f;
                baseTint = Color.Lerp(new Color(0.08f, 0.12f, 0.28f, 0.42f), new Color(1.0f, 0.65f, 0.40f, 0.12f), t);
            }
            else if (currentHour >= 7.5f && currentHour < 16.5f)
            {
                baseTint = Color.clear;
            }
            else if (currentHour >= 16.5f && currentHour < 19f)
            {
                float t = (currentHour - 16.5f) / 2.5f;
                if (t < 0.5f)
                    baseTint = Color.Lerp(Color.clear, new Color(0.95f, 0.48f, 0.18f, 0.22f), t * 2f);
                else
                    baseTint = Color.Lerp(new Color(0.95f, 0.48f, 0.18f, 0.22f), new Color(0.06f, 0.10f, 0.25f, 0.46f), (t - 0.5f) * 2f);
            }
            else
            {
                baseTint = new Color(0.05f, 0.09f, 0.22f, 0.46f);
            }

            Color weatherTint = currentWeather switch
            {
                WeatherType.Rain => new Color(0.10f, 0.15f, 0.26f, 0.40f),
                WeatherType.Heatwave => new Color(0.98f, 0.56f, 0.12f, 0.25f),
                WeatherType.Snow => new Color(0.68f, 0.86f, 0.98f, 0.32f),
                _ => Color.clear
            };

            if (weatherTint.a > 0.01f)
            {
                return Color.Lerp(baseTint, weatherTint, 0.70f);
            }
            return baseTint;
        }

        private void UpdateSkyAndLighting()
        {
            if (mainCam == null)
                mainCam = Camera.main;

            // Bật đèn đường ngoài trời từ 17:15 chiều tới 06:15 sáng
            bool lampsOn = (currentHour >= 17.25f || currentHour < 6.25f);
            float pulseAlpha = 0.78f + Mathf.Sin(Time.time * 2.6f) * 0.10f;
            for (int i = 0; i < streetLightGlows.Count; i++)
            {
                var glowSr = streetLightGlows[i];
                if (glowSr == null) continue;
                if (glowSr.gameObject.activeSelf != lampsOn)
                    glowSr.gameObject.SetActive(lampsOn);

                if (lampsOn)
                {
                    glowSr.color = new Color(1f, 0.88f, 0.55f, pulseAlpha);
                }
            }

            // Nếu đang ở bên trong tòa nhà -> Giữ ánh sáng ấm áp trong phòng
            if (currentBuilding != BuildingType.None)
            {
                if (mainCam != null)
                    mainCam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);

                if (globalLight2D != null)
                {
                    globalLight2D.color = new Color(1f, 0.96f, 0.90f);
                    globalLight2D.intensity = 0.98f;
                }
                return;
            }

            Color dawnSky = new Color(0.32f, 0.28f, 0.34f);
            Color daySky = new Color(0.24f, 0.38f, 0.30f);
            Color sunsetSky = new Color(0.36f, 0.24f, 0.26f);
            Color nightSky = new Color(0.07f, 0.10f, 0.18f);

            Color dawnLight = new Color(1.0f, 0.84f, 0.72f);
            Color dayLight = Color.white;
            Color sunsetLight = new Color(1.0f, 0.72f, 0.50f);
            Color nightLight = new Color(0.42f, 0.52f, 0.78f);

            Color targetBg;
            Color targetLightColor;
            float targetIntensity;

            if (currentHour >= 5f && currentHour < 7.5f)
            {
                float t = (currentHour - 5f) / 2.5f;
                targetBg = Color.Lerp(nightSky, dawnSky, t);
                targetLightColor = Color.Lerp(nightLight, dawnLight, t);
                targetIntensity = Mathf.Lerp(0.55f, 0.95f, t);
            }
            else if (currentHour >= 7.5f && currentHour < 16.5f)
            {
                targetBg = daySky;
                targetLightColor = dayLight;
                targetIntensity = 1.0f;
            }
            else if (currentHour >= 16.5f && currentHour < 19f)
            {
                float t = (currentHour - 16.5f) / 2.5f;
                targetBg = Color.Lerp(daySky, sunsetSky, t);
                targetLightColor = Color.Lerp(dayLight, sunsetLight, t);
                targetIntensity = Mathf.Lerp(1.0f, 0.68f, t);
            }
            else
            {
                targetBg = nightSky;
                targetLightColor = nightLight;
                targetIntensity = 0.55f;
            }

            if (mainCam != null)
                mainCam.backgroundColor = targetBg;

                        if (currentWeather == WeatherType.Rain)
            {
                targetLightColor = Color.Lerp(targetLightColor, new Color(0.68f, 0.76f, 0.90f), 0.55f);
                targetIntensity *= 0.76f;
            }
            else if (currentWeather == WeatherType.Heatwave)
            {
                targetLightColor = Color.Lerp(targetLightColor, new Color(1.0f, 0.90f, 0.68f), 0.50f);
                targetIntensity *= 1.25f;
            }
            else if (currentWeather == WeatherType.Snow)
            {
                targetLightColor = Color.Lerp(targetLightColor, new Color(0.85f, 0.94f, 1.0f), 0.55f);
                targetIntensity *= 1.10f;
            }

            if (globalLight2D != null)
            {
                globalLight2D.color = targetLightColor;
                globalLight2D.intensity = targetIntensity;
            }
        }

        public static Vector3 IsoToWorld(int gx, int gy)
        {
            float wx = (gx - gy) * 0.48f;
            float wy = (gx + gy) * 0.24f;
            return new Vector3(wx, wy, 0f);
        }

        public static Vector2Int WorldToIsoGrid(Vector3 worldPos)
        {
            float a = worldPos.x / 0.48f;
            float b = worldPos.y / 0.24f;
            int gx = Mathf.RoundToInt(0.5f * (a + b));
            int gy = Mathf.RoundToInt(0.5f * (b - a));
            return new Vector2Int(gx, gy);
        }

        public string GetCurrentLocationDisplayName(Vector3 playerWorldPos)
        {
            if (currentBuilding != BuildingType.None)
                return CozyLifeDatabase.GetBuildingVietnameseName(currentBuilding);

            Vector2Int g = WorldToIsoGrid(playerWorldPos);
            if (Mathf.Abs(g.x) <= 3 && Mathf.Abs(g.y) <= 3)
                return "⛲ Quảng Trường";
            if (Mathf.Abs(g.x) <= 2 && g.y >= -7 && g.y <= -4)
                return "🐺 Lửa Trại Ma Sói";
            if (g.x >= 10 && g.y >= 4 && g.y <= 7)
                return "⚕️ Khu Y Tế";
            if (g.x >= 10 && g.y >= -4 && g.y <= 3)
                return "🔨 Khu Xưởng Mộc";
            if (g.x >= 4 && g.x <= 9 && g.y >= 4 && g.y <= 8)
                return "🌳 Công Viên Hồ Nước";
            if (g.x >= 3 && g.x <= 11 && g.y >= -19 && g.y <= -6)
                return "🏀 Sân Thể Thao";
            if (g.x >= -13 && g.x <= -4 && g.y >= -13 && g.y <= -5)
                return "🌾 Khu Nông Trại";
            if (g.x <= -4 && g.y >= 0 && g.y <= 7)
                return "🛍️ Phố Thương Mại";
            if (g.y >= 8)
                return "🏡 Khu Dân Cư";

            return "🌳 Thị Trấn";
        }

        public static Vector3 GetInteriorRoomOrigin(BuildingType type)
        {
            return new Vector3((int)type * 40f, 0f, 0f);
        }

        /// <summary>
        /// Chuyển cảnh vào bên trong một ngôi nhà với hiệu ứng màn hình chuyển cảnh mượt mà.
        /// </summary>
        public void EnterBuildingInterior(BuildingType targetBuilding, Vector3 outdoorReturnPos)
        {
            if (targetBuilding == BuildingType.None)
                return;

            lastOutdoorDoorPosition = outdoorReturnPos;
            string buildingTitle = CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding);

            if (CozyLifeHUD.Instance != null)
            {
                CozyLifeHUD.Instance.PlaySceneTransition($"🚪 Vào {buildingTitle}", () =>
                {
                    PerformTeleportToBuilding(targetBuilding);
                });
            }
            else
            {
                PerformTeleportToBuilding(targetBuilding);
            }
        }

        private void PerformTeleportToBuilding(BuildingType targetBuilding)
        {
            currentBuilding = targetBuilding;
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null && interiorEntrancePositions.TryGetValue(targetBuilding, out Vector3 spawnPos))
            {
                player.StandUpFromTable();
                player.TeleportToPosition(spawnPos);
            }

            if (targetBuilding == BuildingType.Restaurant)
            {
                CozyLifeHUD.PushToast("🍽️ Lại gần Bàn Ăn bấm [E] để Ngồi & Gọi Món!");
            }
            else
            {
                CozyLifeHUD.PushToast($"📍 Đã vào {CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding)}");
            }
        }

        /// <summary>
        /// Bước từ bên trong ngôi nhà ra lại đúng vị trí cửa trước ở Quảng Trường Thị Trấn.
        /// </summary>
        public void ExitCurrentBuildingToTown()
        {
            BuildingType leaving = currentBuilding;
            Vector3 returnPos = lastOutdoorDoorPosition;
            if (returnPos == Vector3.zero && outdoorDoorPositions.TryGetValue(leaving, out Vector3 savedDoor))
            {
                returnPos = savedDoor;
            }

            if (CozyLifeHUD.Instance != null)
            {
                CozyLifeHUD.Instance.PlaySceneTransition("🌳 Ra ngoài Thị Trấn...", () =>
                {
                    PerformTeleportToTown(returnPos);
                });
            }
            else
            {
                PerformTeleportToTown(returnPos);
            }
        }

        public void SetCurrentBuildingState(BuildingType building)
        {
            currentBuilding = building;
            if (building != BuildingType.None && outdoorDoorPositions.TryGetValue(building, out Vector3 doorPos))
            {
                lastOutdoorDoorPosition = doorPos;
            }
        }

        private void PerformTeleportToTown(Vector3 returnPos)
        {
            currentBuilding = BuildingType.None;
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.StandUpFromTable();
                player.TeleportToPosition(returnPos);
            }
            CozyLifeHUD.PushToast("🌳 Đã ra ngoài Thị Trấn");
        }

        private void BuildIsometricTownAndInteriors()
        {
            if (worldRoot != null)
                return;

            worldRoot = new GameObject("CozyTown_GeneratedWorld").transform;

            BuildOutdoorTown();

            // Dựng Không Gian Nội Thất (Interior Scenes) riêng biệt cho từng tòa nhà chức năng & các căn nhà mua bán
            BuildRestaurantInterior();
            BuildMarketInterior();
            BuildClinicInterior();
            BuildWorkshopInterior();
            BuildPlayerHomeInterior();
            BuildBuyableHouseInteriors();
            BuildCafeInterior();
        }

        private void BuildOutdoorTown()
        {
            Transform townRoot = new GameObject("00_TownExterior").transform;
            townRoot.SetParent(worldRoot);

            // 1. Mặt đất Isometric mở rộng (-16..16 = 33x33 ô, quy hoạch thành 6 phân khu rõ rệt)
            Transform groundGroup = new GameObject("01_GroundTiles").transform;
            groundGroup.SetParent(townRoot);

            Sprite grassSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_grass", new Vector2(0.5f, 0.5f), 100f);
            Sprite roadSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_road", new Vector2(0.5f, 0.5f), 100f);
            Sprite plazaSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_plaza", new Vector2(0.5f, 0.5f), 100f);
            Sprite farmTileSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_farmland", new Vector2(0.5f, 0.5f), 100f);
            Sprite waterSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_water", new Vector2(0.5f, 0.5f), 100f);
            Sprite parkPathSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_park_path", new Vector2(0.5f, 0.5f), 100f);
            Sprite courtFloorSprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_court_floor", new Vector2(0.5f, 0.5f), 100f);

            int minGx = -20;
            int maxGx = 20;
            int minGy = -23;
            int maxGy = 19;
            for (int gx = minGx; gx <= maxGx; gx++)
            {
                for (int gy = minGy; gy <= maxGy; gy++)
                {
                    Vector3 pos = IsoToWorld(gx, gy);
                    GameObject tileObj = new GameObject($"Tile_{gx}_{gy}");
                    tileObj.transform.SetParent(groundGroup);
                    tileObj.transform.position = pos;

                    SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();

                    bool isCentralPlaza = Mathf.Abs(gx) <= 3 && Mathf.Abs(gy) <= 3;
                    bool isNorthAvenue = Mathf.Abs(gx) <= 1 && gy >= 4 && gy <= 7;
                    bool isNorthResidentialStreet = gy == 7 && gx >= -6 && gx <= 8;
                    bool isResidentialApproach = gy == 8 && (gx == -5 || gx == -1 || gx == 3 || gx == 7);
                    bool isSouthAvenue = Mathf.Abs(gx) <= 1 && gy <= -4 && gy >= -6;
                    bool isFarmRoad = gy == -6 && gx >= -11 && gx <= -2;

                    // Tuyến phố thương mại Tây & các đường nhánh dẫn thẳng vào cửa từng nhà hàng / quán
                    bool isCommercialStreet = gy == 2 && gx >= -11 && gx <= -3;
                    bool isRestaurantApproach = gx == -8 && gy >= 3 && gy <= 5;
                    bool isMarketApproach = gx == -11 && (gy == 3 || gy == 4);
                    bool isCafeApproach = gx == -11 && gy == 1;

                    // Trục đường Đông dẫn đến Xưởng Mộc & Phòng Khám ven hồ
                    bool isEastAvenue = gy == 0 && gx >= 4 && gx <= 11;
                    bool isClinicApproach = gx == 10 && gy >= 1 && gy <= 4;

                    // Khu Công Viên Hồ Nước (Đông - Bắc: gx 6..8, gy 5..7 là mặt nước; gx 4..9, gy 4..8 là đường dạo ven hồ)
                    bool isParkPond = gx >= 6 && gx <= 8 && gy >= 5 && gy <= 7;
                    bool isParkWalkway = !isParkPond && !isCentralPlaza && gx >= 4 && gx <= 9 && gy >= 4 && gy <= 8;

                    // Khu Nông Trại mở rộng (Tây - Nam: gx -13..-6, gy -13..-6)
                    bool isFarmZone = gx >= -13 && gx <= -6 && gy >= -13 && gy <= -6;

                    if (isParkPond)
                    {
                        sr.sprite = waterSprite;
                        BoxCollider2D waterCol = tileObj.AddComponent<BoxCollider2D>();
                        waterCol.size = new Vector2(0.52f, 0.28f);
                    }
                    else if (isParkWalkway)
                    {
                        sr.sprite = parkPathSprite;
                    }
                    else if (isCentralPlaza)
                    {
                        sr.sprite = plazaSprite;
                    }
                    else if (isNorthAvenue || isNorthResidentialStreet || isResidentialApproach ||
                             isSouthAvenue || isFarmRoad ||
                             isCommercialStreet || isRestaurantApproach || isMarketApproach || isCafeApproach ||
                             isEastAvenue || isClinicApproach)
                    {
                        sr.sprite = roadSprite;
                    }
                    else if (isFarmZone)
                    {
                        sr.sprite = farmTileSprite;
                    }
                    else
                    {
                        sr.sprite = grassSprite;
                    }

                    sr.sortingOrder = -2000 + Mathf.RoundToInt(-pos.y * 10f);
                }
            }

            // 2. Đặt các Tòa Nhà Nghề Nghiệp phân bố theo từng Phân Khu & 3 Bất Động Sản Mua Bán
            Transform buildingGroup = new GameObject("02_TownBuildings").transform;
            buildingGroup.SetParent(townRoot);

            // --- PHÂN KHU THƯƠNG MẠI & ẨM THỰC (Tây - Bắc) ---
            // Nhà Hàng nằm cạnh bãi cỏ Lửa Trại Ma Sói, có đường lát đá nối thẳng vào cửa
            SpawnEnterableBuilding(buildingGroup, BuildingType.Restaurant, "CozyLifeArt/Buildings/building_bakery", IsoToWorld(-8, 6), IsoToWorld(-8, 5), 0);
            SpawnEnterableBuilding(buildingGroup, BuildingType.Market, "CozyLifeArt/Buildings/building_market", IsoToWorld(-11, 5), IsoToWorld(-11, 4), 0);
            SpawnEnterableBuilding(buildingGroup, BuildingType.Cafe, "CozyLifeArt/Buildings/building_cafe", IsoToWorld(-11, 1), IsoToWorld(-11, 1), 0);

            // --- PHÂN KHU Y TẾ (Đông - Bắc, bên hồ sinh thái) ---
            SpawnEnterableBuilding(buildingGroup, BuildingType.Clinic, "CozyLifeArt/Buildings/building_clinic", IsoToWorld(11, 5), IsoToWorld(10, 4), 0);

            // --- PHÂN KHU XƯỞNG MỘC (Đông - Nam, rìa rừng thông) ---
            SpawnEnterableBuilding(buildingGroup, BuildingType.Workshop, "CozyLifeArt/Buildings/building_workshop", IsoToWorld(11, -1), IsoToWorld(11, 0), 0);

            // --- PHÂN KHU DÂN CƯ & CÁC NGÔI NHÀ CÓ THỂ MUA (Nằm ngay cạnh vỉa hè phố Bắc, có đường nhánh nối thẳng vào cửa) ---
            // Nhà khởi đầu miễn phí của người chơi
            SpawnEnterableBuilding(buildingGroup, BuildingType.PlayerHome, "CozyLifeArt/Buildings/building_home", IsoToWorld(-1, 10), IsoToWorld(-1, 9), 0);

            // 3 Ngôi nhà có thể mua khi tích lũy đủ Xu:
            SpawnEnterableBuilding(buildingGroup, BuildingType.BuyableTownhouse, "CozyLifeArt/Buildings/building_modern_house", IsoToWorld(-5, 10), IsoToWorld(-5, 9), 250);
            SpawnEnterableBuilding(buildingGroup, BuildingType.BuyableCottage, "CozyLifeArt/Buildings/building_cottage_yellow", IsoToWorld(3, 10), IsoToWorld(3, 9), 150);
            SpawnEnterableBuilding(buildingGroup, BuildingType.BuyableVilla, "CozyLifeArt/Buildings/building_villa_blue", IsoToWorld(7, 10), IsoToWorld(7, 9), 400);

            // 3. Giếng Nước thị trấn gần Nông Trại
            Transform stationGroup = new GameObject("03_OutdoorStations").transform;
            stationGroup.SetParent(townRoot);
            SpawnWorkstation(stationGroup, StationType.WaterWell, "Giếng Nước Thị Trấn", "CozyLifeArt/Objects/obj_well", IsoToWorld(-4, -6));

            // 4. Khu Nông Trại Trù Phú phía Tây - Nam (9 Ô Đất Trồng xếp luống 3x3)
            Transform farmGroup = new GameObject("04_FarmPlots").transform;
            farmGroup.SetParent(townRoot);

            Vector2Int[] plotCoords = new Vector2Int[]
            {
                new Vector2Int(-7, -8), new Vector2Int(-9, -8), new Vector2Int(-11, -8),
                new Vector2Int(-7, -10), new Vector2Int(-9, -10), new Vector2Int(-11, -10),
                new Vector2Int(-7, -12), new Vector2Int(-9, -12), new Vector2Int(-11, -12)
            };

            for (int i = 0; i < plotCoords.Length; i++)
            {
                Vector3 plotPos = IsoToWorld(plotCoords[i].x, plotCoords[i].y);
                GameObject plotObj = new GameObject($"CropPlot_{i + 1}");
                plotObj.transform.SetParent(farmGroup);
                plotObj.transform.position = plotPos;

                plotObj.AddComponent<SpriteRenderer>();
                IsometricSorting sorting = plotObj.AddComponent<IsometricSorting>();
                sorting.Configure(true, -5);

                CropPlotInteractable crop = plotObj.AddComponent<CropPlotInteractable>();
                crop.PlotIndex = i;
                if (i < 3)
                    crop.SetInitialStage(CropPlotInteractable.CropStage.Ready);
                else if (i < 6)
                    crop.SetInitialStage(CropPlotInteractable.CropStage.Growing);
                else
                    crop.SetInitialStage(CropPlotInteractable.CropStage.Empty);
            }

            // 5. Công Viên Hồ Nước, Sân Thể Thao, Cây Xanh & Đèn Đường
            Transform decorGroup = new GameObject("05_ParkSportsAndDecor").transform;
            decorGroup.SetParent(townRoot);

            // --- ĐẠI QUẢNG TRƯỜNG TRUNG TÂM: ĐÀI PHUN NƯỚC NẰM CHÍNH GIỮA TRUNG TÂM, BỒN HOA & KỲ ĐÀI CỜ ---
            SpawnDecorObject(decorGroup, "ParkFountain", "CozyLifeArt/Objects/obj_fountain", IsoToWorld(0, 0), 80f, true);
            SpawnDecorObject(decorGroup, "PlazaFlower1", "CozyLifeArt/Objects/obj_flower_bed", IsoToWorld(-2, -2), 92f, false);
            SpawnDecorObject(decorGroup, "PlazaFlower2", "CozyLifeArt/Objects/obj_flower_bed", IsoToWorld(2, 2), 92f, false);

            // Bàn Cờ Vua Công Viên tại cánh Tây Quảng Trường (-2, 0) kèm 2 chiếc ghế 2 bên
            Vector3 chessTablePos = IsoToWorld(-2, 0);
            GameObject chessTableObj = new GameObject("Park_ChessTable");
            chessTableObj.transform.SetParent(decorGroup);
            chessTableObj.transform.position = chessTablePos;
            SpriteRenderer chessTableSr = chessTableObj.AddComponent<SpriteRenderer>();
            chessTableSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_chess_table", new Vector2(0.5f, 0.25f), 86f);
            chessTableObj.AddComponent<IsometricSorting>().Configure(true, 4);
            CircleCollider2D chessTriggerCol = chessTableObj.AddComponent<CircleCollider2D>();
            chessTriggerCol.radius = 0.88f;
            chessTriggerCol.isTrigger = true;
            ParkChessTableInteractable chessInteract = chessTableObj.AddComponent<ParkChessTableInteractable>();
            chessInteract.ConfigureParkChessTable(decorGroup);

            // 2 Chiếc ghế đối diện nhau ở hai bên bàn cờ vua
            SpawnDecorObject(decorGroup, "ParkChess_ChairLeft", "CozyLifeArt/Objects/obj_chair", chessTablePos + new Vector3(-0.42f, -0.04f, 0f), 90f, false);
            GameObject chairRight = new GameObject("ParkChess_ChairRight");
            chairRight.transform.SetParent(decorGroup);
            chairRight.transform.position = chessTablePos + new Vector3(0.42f, -0.04f, 0f);
            SpriteRenderer srChairR = chairRight.AddComponent<SpriteRenderer>();
            srChairR.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_chair", new Vector2(0.5f, 0.25f), 90f);
            srChairR.flipX = true;
            chairRight.AddComponent<IsometricSorting>().Configure(true, 0);

            // Bàn Cờ Tướng Công Viên tại cánh Đông Quảng Trường (2, 0) kèm 2 chiếc ghế 2 bên
            Vector3 xiangqiTablePos = IsoToWorld(2, 0);
            GameObject xiangqiTableObj = new GameObject("Park_XiangqiTable");
            xiangqiTableObj.transform.SetParent(decorGroup);
            xiangqiTableObj.transform.position = xiangqiTablePos;
            SpriteRenderer xiangqiTableSr = xiangqiTableObj.AddComponent<SpriteRenderer>();
            xiangqiTableSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_xiangqi_table", new Vector2(0.5f, 0.25f), 86f);
            xiangqiTableObj.AddComponent<IsometricSorting>().Configure(true, 4);
            CircleCollider2D xiangqiTriggerCol = xiangqiTableObj.AddComponent<CircleCollider2D>();
            xiangqiTriggerCol.radius = 0.88f;
            xiangqiTriggerCol.isTrigger = true;
            ParkXiangqiTableInteractable xiangqiInteract = xiangqiTableObj.AddComponent<ParkXiangqiTableInteractable>();
            xiangqiInteract.ConfigureParkXiangqiTable(decorGroup);

            // 2 Chiếc ghế đối diện nhau ở hai bên bàn cờ tướng
            SpawnDecorObject(decorGroup, "ParkXiangqi_ChairLeft", "CozyLifeArt/Objects/obj_chair", xiangqiTablePos + new Vector3(-0.42f, -0.04f, 0f), 90f, false);
            GameObject chairXqRight = new GameObject("ParkXiangqi_ChairRight");
            chairXqRight.transform.SetParent(decorGroup);
            chairXqRight.transform.position = xiangqiTablePos + new Vector3(0.42f, -0.04f, 0f);
            SpriteRenderer srChairXqR = chairXqRight.AddComponent<SpriteRenderer>();
            srChairXqR.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_chair", new Vector2(0.5f, 0.25f), 90f);
            srChairXqR.flipX = true;
            chairXqRight.AddComponent<IsometricSorting>().Configure(true, 0);

            // --- CÔNG VIÊN HỒ NƯỚC SINH THÁI (Đông - Bắc) ---
            SpawnDecorObject(decorGroup, "ParkFlower1", "CozyLifeArt/Objects/obj_flower_bed", IsoToWorld(5, 4), 92f, false);
            SpawnDecorObject(decorGroup, "ParkFlower2", "CozyLifeArt/Objects/obj_flower_bed", IsoToWorld(5, 7), 92f, false);
            SpawnDecorObject(decorGroup, "ParkFlower3", "CozyLifeArt/Objects/obj_flower_bed", IsoToWorld(9, 7), 92f, false);
            SpawnDecorObject(decorGroup, "ParkFlower4", "CozyLifeArt/Objects/obj_flower_bed", IsoToWorld(9, 3), 92f, false);

            // --- SÂN THỂ THAO BÓNG RỔ (Đông - Nam: gx 4..9, gy -11..-7) ---
            Vector3 courtCenter = 0.5f * (IsoToWorld(4, -11) + IsoToWorld(9, -7)) + new Vector3(0f, 0.06f, 0f);
            GameObject courtSurfaceObj = new GameObject("BasketballCourt_UnifiedSurface");
            courtSurfaceObj.transform.SetParent(decorGroup);
            courtSurfaceObj.transform.position = courtCenter;
            SpriteRenderer courtSr = courtSurfaceObj.AddComponent<SpriteRenderer>();
            courtSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_court_markings", new Vector2(0.5f, 0.5f), 100f);
            courtSr.sortingOrder = -1400;

            Vector3 bbHoopWest = IsoToWorld(4, -9) + new Vector3(-0.10f, -0.05f, 0f);
            Vector3 bbHoopEast = IsoToWorld(9, -9) + new Vector3(0.10f, 0.05f, 0f);
            SpawnDecorObject(decorGroup, "BasketballHoop_West", "CozyLifeArt/Objects/obj_basketball_hoop", bbHoopWest, 84f, false);
            SpawnDecorObject(decorGroup, "BasketballHoop_East", "CozyLifeArt/Objects/obj_basketball_hoop", bbHoopEast, 84f, false);

            // Quả bóng rổ kích hoạt Minigame "Vua Ném Rổ"
            GameObject bbTrigger = new GameObject("Basketball_MinigameTrigger");
            bbTrigger.transform.SetParent(decorGroup);
            bbTrigger.transform.position = courtCenter + new Vector3(0f, 0.08f, 0f);
            SpriteRenderer bbSr = bbTrigger.AddComponent<SpriteRenderer>();
            bbSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/prop_basketball", new Vector2(0.5f, 0.5f), 100f);
            bbSr.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            bbTrigger.AddComponent<IsometricSorting>().Configure(true, 5);
            CircleCollider2D bbCol = bbTrigger.AddComponent<CircleCollider2D>();
            bbCol.radius = 0.70f;
            bbCol.isTrigger = true;
            BasketballCourtInteractable bbInteract = bbTrigger.AddComponent<BasketballCourtInteractable>();
            bbInteract.Configure(bbHoopEast);

            // --- SÂN BÓNG ĐÁ MINI CẠNH SÂN BÓNG RỔ (Đông - Nam: gx 4..9, gy -17..-13) ---
            Vector3 soccerCenter = 0.5f * (IsoToWorld(4, -17) + IsoToWorld(9, -13)) + new Vector3(0f, 0.06f, 0f);
            GameObject soccerSurfaceObj = new GameObject("SoccerPitch_UnifiedSurface");
            soccerSurfaceObj.transform.SetParent(decorGroup);
            soccerSurfaceObj.transform.position = soccerCenter;
            SpriteRenderer soccerSr = soccerSurfaceObj.AddComponent<SpriteRenderer>();
            soccerSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_soccer_pitch", new Vector2(0.5f, 0.5f), 100f);
            soccerSr.sortingOrder = -1400;

            // 2 Khung thành bóng đá phía Tây & phía Đông (căn chuẩn khớp vạch cầu môn)
            Vector3 goalWestPos = IsoToWorld(4, -15);
            Vector3 goalEastPos = IsoToWorld(9, -15);
            SpawnDecorObject(decorGroup, "SoccerGoal_West", "CozyLifeArt/Objects/obj_soccer_goal_west", goalWestPos, 84f, false, new Vector2(0.54f, 0.455f));
            SpawnDecorObject(decorGroup, "SoccerGoal_East", "CozyLifeArt/Objects/obj_soccer_goal_east", goalEastPos, 84f, false, new Vector2(0.46f, 0.318f));

            // Quả bóng đá ở chấm phạt đền kích hoạt Minigame "Vua Sút Phạt Đền"
            Vector3 penaltySpot = IsoToWorld(7, -15);
            GameObject fbTrigger = new GameObject("Soccer_MinigameTrigger");
            fbTrigger.transform.SetParent(decorGroup);
            fbTrigger.transform.position = penaltySpot;
            SpriteRenderer fbSr = fbTrigger.AddComponent<SpriteRenderer>();
            fbSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/prop_soccer_ball", new Vector2(0.5f, 0.5f), 100f);
            fbSr.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            fbTrigger.AddComponent<IsometricSorting>().Configure(true, 5);
            CircleCollider2D fbCol = fbTrigger.AddComponent<CircleCollider2D>();
            fbCol.radius = 0.70f;
            fbCol.isTrigger = true;
            SoccerPitchInteractable fbInteract = fbTrigger.AddComponent<SoccerPitchInteractable>();
            fbInteract.Configure(goalEastPos);

            // --- RỪNG CÂY BAO QUANH THỊ TRẤN & SÂN THỂ THAO ---
            Vector2Int[] treeCoords = new Vector2Int[]
            {
                new Vector2Int(-15, 5), new Vector2Int(-15, 9), new Vector2Int(-14, 15),
                new Vector2Int(-5, 16), new Vector2Int(1, 16), new Vector2Int(7, 16),
                new Vector2Int(16, 12), new Vector2Int(16, 6), new Vector2Int(16, -2),
                new Vector2Int(14, -8), new Vector2Int(13, -13), new Vector2Int(11, -17),
                new Vector2Int(2, -13), new Vector2Int(-4, -14), new Vector2Int(-13, -14),
                new Vector2Int(-15, -5), new Vector2Int(5, 9),
                new Vector2Int(12, -15), new Vector2Int(13, -19), new Vector2Int(7, -21),
                new Vector2Int(2, -20), new Vector2Int(-3, -19), new Vector2Int(16, -10),
                new Vector2Int(16, -16), new Vector2Int(-16, -10)
            };

            foreach (var tc in treeCoords)
            {
                SpawnDecorObject(decorGroup, "Tree", "CozyLifeArt/Objects/obj_tree", IsoToWorld(tc.x, tc.y), 85f, false);
            }

            // --- HỆ THỐNG ĐÈN ĐƯỜNG THỊ TRẤN (Tự bật sáng vào Hoàng Hôn & Ban Đêm: 17:15 - 06:15) ---
            Vector2Int[] lampCoords = new Vector2Int[]
            {
                // 4 Đèn bao quanh 4 góc Đại Quảng Trường Trung Tâm
                new Vector2Int(-3, 3), new Vector2Int(3, 3),
                new Vector2Int(-3, -3), new Vector2Int(3, -3),

                // 2 Đèn dọc Phố Thương Mại (gy = 2)
                new Vector2Int(-6, 2), new Vector2Int(-10, 2),

                // 3 Đèn dọc Phố Dân Cư Bắc (gy = 7)
                new Vector2Int(-4, 7), new Vector2Int(0, 7), new Vector2Int(5, 7),

                // 2 Đèn Phân Khu Y Tế & Xưởng Mộc
                new Vector2Int(6, 0), new Vector2Int(9, 0),

                // 2 Đèn Tuyến Đường Nông Trại (gy = -6)
                new Vector2Int(-5, -6), new Vector2Int(-9, -6),

                // 2 Đèn Khu Phức Hợp Thể Thao
                new Vector2Int(3, -9), new Vector2Int(10, -9)
            };
            for (int i = 0; i < lampCoords.Length; i++)
            {
                SpawnStreetLight(decorGroup, $"StreetLight_{i + 1}", IsoToWorld(lampCoords[i].x, lampCoords[i].y));
            }

            // 6. Bạn bè Nông Dân An đứng chăm sóc Khu Nông Trại phía Tây - Nam
            SpawnFriend(townRoot, "An", ProfessionType.Farmer, IsoToWorld(-5, -8),
                "Nông trại mới rộng rãi quá! Cùng trồng cây nhé! 🌱", BuildingType.None, 1.1f);

            // 7. Khu Vực Lửa Trại Ma Sói Trên Thảm Cỏ Xanh Phía Trước Nhà Hàng (4 - 8 Người Chơi)
            // Đặt trên thảm cỏ xanh mướt gx: -4, gy: 5, hoàn toàn bao bọc bởi 4 khúc gỗ tự nhiên
            SpawnCampfireWerewolfArea(decorGroup, IsoToWorld(-4, 5));

            // 8. Cầu Câu Cá Công Viên Ven Hồ Sinh Thái (gx: 7, gy: 4)
            SpawnFishingPierArea(decorGroup, IsoToWorld(7, 4));

            // 9. Bảng Nhiệm Vụ & Thành Tựu Cư Dân Thị Trấn tại Đại Quảng Trường Trung Tâm (gx: 1, gy: -2)
            SpawnTownBulletinBoard(decorGroup, IsoToWorld(1, -2));

            // 10. Mái Ấm Nhận Nuôi Thú Cưng tại Góc Đường Công Viên (gx: 4, gy: 5)
            SpawnPetAdoptionArea(decorGroup, IsoToWorld(4, 5));

            // 11. Hệ thống Hạt Môi Trường Tự Nhiên (Đom đóm đêm, ánh sáng lập lòe)
            decorGroup.gameObject.AddComponent<CozyTownAmbientParticles>();

            // 12. Suối Nước Nóng Thiên Nhiên Onsen ven hồ sinh thái (gx: 10, gy: 5)
            SpawnHotSpringOnsenArea(decorGroup, IsoToWorld(10, 5));

            // 13. Xe Đẩy Ẩm Thực Đường Phố tại Trục Phố Đi Bộ (gx: 2, gy: -1)
            SpawnStreetFoodCart(decorGroup, IsoToWorld(2, -1));

            // 14. Hệ thống Khí Hậu & Bầu Không Khí Môi Trường (Mưa rào, Tuyết rơi, Cánh hoa Sakura)
            townRoot.gameObject.AddComponent<CozyWeatherAmbianceSystem>();

            // 15. Kích hoạt Sổ Tay Thành Tựu Thị Trấn CozyDiary
            CozyDiarySystem.EnsureInstance();
        }

        // =====================================================================
        // XÂY DỰNG NỘI THẤT CÁC TÒA NHÀ (INTERIOR SCENES)
        // =====================================================================

        private Transform CreateBaseInteriorRoom(BuildingType type, string floorSpritePath, int halfSize = 4)
        {
            Vector3 origin = GetInteriorRoomOrigin(type);
            Transform roomRoot = new GameObject($"Interior_{type}").transform;
            roomRoot.SetParent(worldRoot);
            roomRoot.position = origin;

            Sprite floorSp = CozyLifeDatabase.LoadSprite(floorSpritePath, new Vector2(0.5f, 0.5f), 100f);
            Sprite wallSp = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_wall_brick", new Vector2(0.5f, 0.25f), 100f);
            Sprite matSp = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_door_mat", new Vector2(0.5f, 0.5f), 100f);

            for (int gx = -halfSize; gx <= halfSize; gx++)
            {
                for (int gy = -halfSize; gy <= halfSize; gy++)
                {
                    Vector3 worldPos = origin + IsoToWorld(gx, gy);
                    GameObject fObj = new GameObject($"Floor_{gx}_{gy}");
                    fObj.transform.SetParent(roomRoot);
                    fObj.transform.position = worldPos;

                    SpriteRenderer sr = fObj.AddComponent<SpriteRenderer>();
                    sr.sprite = floorSp;
                    sr.sortingOrder = -2000 + Mathf.RoundToInt(-worldPos.y * 10f);

                    // Dựng tường bao phía Bắc và Tây-Bắc (giống góc cắt Isometric của Project Zomboid)
                    if (gx == -halfSize || gy == halfSize)
                    {
                        GameObject wObj = new GameObject($"Wall_{gx}_{gy}");
                        wObj.transform.SetParent(roomRoot);
                        wObj.transform.position = worldPos;

                        SpriteRenderer wSr = wObj.AddComponent<SpriteRenderer>();
                        wSr.sprite = wallSp;
                        IsometricSorting iso = wObj.AddComponent<IsometricSorting>();
                        iso.Configure(true, 12);

                        BoxCollider2D wallCol = wObj.AddComponent<BoxCollider2D>();
                        wallCol.size = new Vector2(0.55f, 0.32f);
                    }
                    // Tường chắn vô hình ở 2 cạnh dưới để nhân vật không đi rơi khỏi phòng
                    else if (gx == halfSize || gy == -halfSize)
                    {
                        if (!(gx == 0 && gy == -halfSize))
                        {
                            GameObject borderObj = new GameObject($"Border_{gx}_{gy}");
                            borderObj.transform.SetParent(roomRoot);
                            borderObj.transform.position = worldPos + new Vector3(0f, -0.2f, 0f);
                            BoxCollider2D bCol = borderObj.AddComponent<BoxCollider2D>();
                            bCol.size = new Vector2(0.45f, 0.25f);
                        }
                    }
                }
            }

            // Đặt Thảm Lối Ra (Exit Door Mat) ở phía Nam căn phòng (0, -halfSize + 1)
            Vector3 exitPos = origin + IsoToWorld(0, -halfSize + 1);
            interiorEntrancePositions[type] = origin + IsoToWorld(0, -halfSize + 2);

            GameObject exitMat = new GameObject("ExitDoorMat");
            exitMat.transform.SetParent(roomRoot);
            exitMat.transform.position = exitPos;

            SpriteRenderer matSr = exitMat.AddComponent<SpriteRenderer>();
            matSr.sprite = matSp;
            matSr.sortingOrder = -1500;

            BuildingDoorInteractable exitDoor = exitMat.AddComponent<BuildingDoorInteractable>();
            Vector3 returnOutdoor = outdoorDoorPositions.TryGetValue(type, out Vector3 od) ? od : Vector3.zero;
            exitDoor.ConfigureDoor(type, true, returnOutdoor);

            return roomRoot;
        }

        private void BuildRestaurantInterior()
        {
            Transform room = CreateBaseInteriorRoom(BuildingType.Restaurant, "CozyLifeArt/Tiles/iso_wood_floor", 4);
            Vector3 origin = GetInteriorRoomOrigin(BuildingType.Restaurant);

            // 4 Bàn Ăn Nhà Hàng để người chơi và bạn bè ngồi vào & Order món ăn!
            SpawnRestaurantTable(room, 1, origin + IsoToWorld(-2, 0));
            SpawnRestaurantTable(room, 2, origin + IsoToWorld(1, 0));
            SpawnRestaurantTable(room, 3, origin + IsoToWorld(-2, -2));
            SpawnRestaurantTable(room, 4, origin + IsoToWorld(1, -2));

            // Bếp Lò Nhà Hàng ở góc trên cho nghề Đầu Bếp
            SpawnWorkstation(room, StationType.CookingStove, "Bếp Lò Chính Nhà Hàng", "CozyLifeArt/Objects/obj_stove", origin + IsoToWorld(-2, 3));
            SpawnWorkstation(room, StationType.CookingStove, "Quầy Bếp Phụ", "CozyLifeArt/Objects/obj_counter", origin + IsoToWorld(0, 3));

            // Đầu Bếp Minh đứng phục vụ bên trong Nhà Hàng
            SpawnFriend(room, "Minh", ProfessionType.Chef, origin + IsoToWorld(-1, 2),
                "Mời ngồi Bàn [E] để gọi món nhé! 🍽️",
                BuildingType.Restaurant, 0.7f);
        }

        private void BuildMarketInterior()
        {
            Transform room = CreateBaseInteriorRoom(BuildingType.Market, "CozyLifeArt/Tiles/iso_wood_floor", 4);
            Vector3 origin = GetInteriorRoomOrigin(BuildingType.Market);

            // Siêu Thị chỉ giữ chức năng Mua Hạt Giống, Vật Liệu & Bán Nông Sản
            SpawnWorkstation(room, StationType.MarketStall, "Quầy Thu Ngân Siêu Thị", "CozyLifeArt/Objects/obj_counter", origin + IsoToWorld(0, 2));
            SpawnWorkstation(room, StationType.MarketStall, "Kệ Nông Sản & Hạt Giống", "CozyLifeArt/Objects/obj_counter", origin + IsoToWorld(-2, 1));
            SpawnWorkstation(room, StationType.MarketStall, "Kệ Vật Liệu & Nước Uống", "CozyLifeArt/Objects/obj_counter", origin + IsoToWorld(2, 1));
        }

        private void BuildClinicInterior()
        {
            Transform room = CreateBaseInteriorRoom(BuildingType.Clinic, "CozyLifeArt/Tiles/iso_clinic_floor", 4);
            Vector3 origin = GetInteriorRoomOrigin(BuildingType.Clinic);

            // Phòng Khám chuyên biệt cho nghề Bác Sĩ: Pha chế thuốc, Sơ cứu và Giường Bệnh Y Tế
            SpawnWorkstation(room, StationType.MedicalStation, "Tủ Thuốc & Pha Chế Y Tế", "CozyLifeArt/Objects/obj_med_station", origin + IsoToWorld(0, 3));
            SpawnBed(room, "Giường Bệnh Sơ Cứu #1", true, origin + IsoToWorld(-2, 1));
            SpawnBed(room, "Giường Bệnh Sơ Cứu #2", true, origin + IsoToWorld(2, 1));

            // Bác Sĩ Lan trực bên trong Phòng Khám
            SpawnFriend(room, "Lan", ProfessionType.Doctor, origin + IsoToWorld(0, 1),
                "Cần sơ cứu hay pha thuốc cứ lại Tủ Thuốc nhé! ⚕️",
                BuildingType.Clinic, 0.7f);
        }

        private void BuildWorkshopInterior()
        {
            Transform room = CreateBaseInteriorRoom(BuildingType.Workshop, "CozyLifeArt/Tiles/iso_wood_floor", 4);
            Vector3 origin = GetInteriorRoomOrigin(BuildingType.Workshop);

            // Xưởng Mộc chuyên biệt cho nghề Thợ Mộc: Xẻ Gỗ Thông & Đóng Ghế Gỗ
            SpawnWorkstation(room, StationType.Workbench, "Bàn Cưa Chính Xưởng Mộc", "CozyLifeArt/Objects/obj_workbench", origin + IsoToWorld(-1, 2));
            SpawnWorkstation(room, StationType.Workbench, "Kho Gỗ Thông", "CozyLifeArt/Objects/obj_counter", origin + IsoToWorld(2, 2));
            SpawnDecorObject(room, "SampleChair1", "CozyLifeArt/Objects/obj_chair", origin + IsoToWorld(-2, 0), 90f, true);
            SpawnDecorObject(room, "SampleChair2", "CozyLifeArt/Objects/obj_chair", origin + IsoToWorld(2, 0), 90f, true);

            // Thợ Mộc Hùng làm việc trong Xưởng Mộc
            SpawnFriend(room, "Hùng", ProfessionType.Carpenter, origin + IsoToWorld(0, 1),
                "Bấm [E] vào Bàn Cưa để xẻ gỗ, bấm [C] đóng ghế nhé! 🔨",
                BuildingType.Workshop, 0.7f);
        }

        private void BuildPlayerHomeInterior()
        {
            Transform room = CreateBaseInteriorRoom(BuildingType.PlayerHome, "CozyLifeArt/Tiles/iso_wood_floor", 4);
            Vector3 origin = GetInteriorRoomOrigin(BuildingType.PlayerHome);

            // Giường Ngủ Ấm Cúng Của Bạn ở góc tây bắc
            SpawnBed(room, "Giường Ngủ Ấm Cúng Của Bạn", false, origin + IsoToWorld(-2, 2));

            // Thảm Thổ Cẩm trải sàn phòng khách
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PersianRug, BuildingType.PlayerHome, origin + IsoToWorld(0, 0), false, false, room);

            // Bàn Trà Gỗ Kèm Ấm Trà Sứ, Sofa Nhung & Ghế Gỗ
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.TeaTable, BuildingType.PlayerHome, origin + IsoToWorld(0, 0), false, false, room);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.LuxurySofa, BuildingType.PlayerHome, origin + IsoToWorld(1, -1), false, false, room);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.CozyChair, BuildingType.PlayerHome, origin + IsoToWorld(-1, -1), false, false, room);

            // Tủ Sách Gỗ Thông, Đèn Ngủ & Cây Cảnh Lọc Không Khí
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.Bookshelf, BuildingType.PlayerHome, origin + IsoToWorld(2, 3), false, false, room);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.FloorLamp, BuildingType.PlayerHome, origin + IsoToWorld(-3, 1), false, false, room);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PottedPlant, BuildingType.PlayerHome, origin + IsoToWorld(3, 1), false, false, room);

            // Bếp Nấu Ăn Gia Đình & Nệm Thú Cưng Ấm Áp
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.KitchenStove, BuildingType.PlayerHome, origin + IsoToWorld(0, 3), false, false, room);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PetBed, BuildingType.PlayerHome, origin + IsoToWorld(-2, 0), false, false, room);
        }

        private void BuildBuyableHouseInteriors()
        {
            // 1. Nhà Gỗ Ven Công Viên (150 Xu) - Lò Sưởi Đá & Máy Đĩa Than Ấm Áp
            Transform cottageRoom = CreateBaseInteriorRoom(BuildingType.BuyableCottage, "CozyLifeArt/Tiles/iso_wood_floor", 4);
            Vector3 cOrigin = GetInteriorRoomOrigin(BuildingType.BuyableCottage);
            SpawnBed(cottageRoom, "Giường Gỗ Thông Ven Công Viên", false, cOrigin + IsoToWorld(-2, 2));

            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PersianRug, BuildingType.BuyableCottage, cOrigin + IsoToWorld(0, 0), false, false, cottageRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.StoneFireplace, BuildingType.BuyableCottage, cOrigin + IsoToWorld(-1, 3), false, false, cottageRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.LuxurySofa, BuildingType.BuyableCottage, cOrigin + IsoToWorld(1, 0), false, false, cottageRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.TeaTable, BuildingType.BuyableCottage, cOrigin + IsoToWorld(0, 0), false, false, cottageRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.VintageGramophone, BuildingType.BuyableCottage, cOrigin + IsoToWorld(2, 2), false, false, cottageRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PottedPlant, BuildingType.BuyableCottage, cOrigin + IsoToWorld(-3, 1), false, false, cottageRoom);

            // 2. Nhà Phố Khu Đông (250 Xu) - Bể Cá Thủy Sinh & Khu Bếp Hiện Đại
            Transform townRoom = CreateBaseInteriorRoom(BuildingType.BuyableTownhouse, "CozyLifeArt/Tiles/iso_wood_floor", 5);
            Vector3 tOrigin = GetInteriorRoomOrigin(BuildingType.BuyableTownhouse);
            SpawnBed(townRoom, "Giường Đôi Hiện Đại Nhà Phố", false, tOrigin + IsoToWorld(-3, 3));

            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PersianRug, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(0, 0), false, false, townRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.FishAquarium, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(1, 3), false, false, townRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.LuxurySofa, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(-1, 1), false, false, townRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.TeaTable, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(-1, 0), false, false, townRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.FloorLamp, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(-3, 0), false, false, townRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.Bookshelf, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(3, 2), false, false, townRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.KitchenStove, BuildingType.BuyableTownhouse, tOrigin + IsoToWorld(-1, 3), false, false, townRoom);

            // 3. Biệt Thự Đồi Thông (400 Xu) - Đỉnh Cao Sang Trọng: Đàn Piano, Lò Sưởi, Bể Cá, Đĩa Than & 2 Giường Hoàng Gia
            Transform villaRoom = CreateBaseInteriorRoom(BuildingType.BuyableVilla, "CozyLifeArt/Tiles/iso_wood_floor", 5);
            Vector3 vOrigin = GetInteriorRoomOrigin(BuildingType.BuyableVilla);
            SpawnBed(villaRoom, "Giường Hoàng Gia Biệt Thự #1", false, vOrigin + IsoToWorld(-3, 3));
            SpawnBed(villaRoom, "Giường Khách Biệt Thự #2", false, vOrigin + IsoToWorld(2, 3));

            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PersianRug, BuildingType.BuyableVilla, vOrigin + IsoToWorld(0, 0), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.ClassicPiano, BuildingType.BuyableVilla, vOrigin + IsoToWorld(-1, 3), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.StoneFireplace, BuildingType.BuyableVilla, vOrigin + IsoToWorld(1, 2), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.FishAquarium, BuildingType.BuyableVilla, vOrigin + IsoToWorld(-3, 0), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.VintageGramophone, BuildingType.BuyableVilla, vOrigin + IsoToWorld(3, 1), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.LuxurySofa, BuildingType.BuyableVilla, vOrigin + IsoToWorld(0, -1), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.TeaTable, BuildingType.BuyableVilla, vOrigin + IsoToWorld(0, 0), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.FloorLamp, BuildingType.BuyableVilla, vOrigin + IsoToWorld(3, 3), false, false, villaRoom);
            CozyFurnitureAura.SpawnPlacedFurniture(ItemType.PottedPlant, BuildingType.BuyableVilla, vOrigin + IsoToWorld(-3, 2), false, false, villaRoom);
        }

        private void BuildCafeInterior()
        {
            // Sàn chiếu Tatami 9x9 (halfSize = 4)
            Transform room = CreateBaseInteriorRoom(BuildingType.Cafe, "CozyLifeArt/Tiles/iso_tatami_floor", 4);
            Vector3 origin = GetInteriorRoomOrigin(BuildingType.Cafe);
            room.gameObject.AddComponent<CozyJapaneseCafeManager>();

            // 1. Quầy Pha Chế Barista ở phía Bắc
            Vector3 counterPos = origin + IsoToWorld(-1, 2);
            GameObject counterObj = new GameObject("Cafe_BaristaCounter");
            counterObj.transform.SetParent(room);
            counterObj.transform.position = counterPos;
            SpriteRenderer counterSr = counterObj.AddComponent<SpriteRenderer>();
            counterSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_cafe_barista_counter", new Vector2(0.5f, 0.25f), 90f);
            counterObj.AddComponent<IsometricSorting>().Configure(true, 8);
            BoxCollider2D counterCol = counterObj.AddComponent<BoxCollider2D>();
            counterCol.size = new Vector2(0.9f, 0.45f);
            counterCol.isTrigger = true;
            counterObj.AddComponent<CafeBaristaCounterInteractable>();

            // 2. Barista NPC Yuki đứng quầy
            Vector3 baristaPos = counterPos + new Vector3(0f, 0.28f, 0f);
            SpawnFriend(room, "Yuki", ProfessionType.Chef, baristaPos,
                "Konnichiwa! Chào mừng bạn đến với Quán Cafe Nhật Bản! ☕🌸", BuildingType.Cafe, 0.35f);

            // 3. Ba Bàn Trà Ngồi Bệt Kiểu Nhật (Chabudai kèm nệm Zabuton)
            SpawnJapaneseFloorTable(room, 1, origin + IsoToWorld(1, 1));
            SpawnJapaneseFloorTable(room, 2, origin + IsoToWorld(-2, -1));
            SpawnJapaneseFloorTable(room, 3, origin + IsoToWorld(2, -1));

            // 4. Cây Cảnh & Đèn Ngủ phong cách Nhật
            SpawnDecorObject(room, "CafeBonsai1", "CozyLifeArt/Objects/obj_potted_plant", origin + IsoToWorld(-3, 3), 90f, false);
            SpawnDecorObject(room, "CafeBonsai2", "CozyLifeArt/Objects/obj_potted_plant", origin + IsoToWorld(3, 3), 90f, false);
            SpawnDecorObject(room, "CafeLantern", "CozyLifeArt/Objects/obj_floor_lamp", origin + IsoToWorld(3, 1), 90f, false);
        }

        private void SpawnJapaneseFloorTable(Transform parent, int tableNum, Vector3 pos)
        {
            GameObject tObj = new GameObject($"JapaneseFloorTable_{tableNum}");
            tObj.transform.SetParent(parent);
            tObj.transform.position = pos;

            SpriteRenderer sr = tObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_japanese_floor_table", new Vector2(0.5f, 0.25f), 88f);

            IsometricSorting sorting = tObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 4);

            CircleCollider2D col = tObj.AddComponent<CircleCollider2D>();
            col.radius = 0.65f;
            col.isTrigger = true;

            JapaneseFloorTableInteractable table = tObj.AddComponent<JapaneseFloorTableInteractable>();
            table.ConfigureTable(tableNum);
        }

        // =====================================================================
        // HELPER SPAWNERS
        // =====================================================================

        private void SpawnEnterableBuilding(Transform parent, BuildingType type, string spritePath, Vector3 buildingPos, Vector3 doorPos, int priceCoins = 0)
        {
            GameObject bObj = new GameObject($"Building_{type}");
            bObj.transform.SetParent(parent);
            bObj.transform.position = buildingPos;

            SpriteRenderer sr = bObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite(spritePath, new Vector2(0.5f, 0.22f), 78f);

            IsometricSorting sorting = bObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 10);

            BoxCollider2D col = bObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.85f, 0.45f);
            col.offset = new Vector2(0f, 0.1f);

            GameObject signObj = null;
            if (priceCoins > 0)
            {
                signObj = new GameObject($"ForSaleSign_{type}");
                signObj.transform.SetParent(parent);
                signObj.transform.position = doorPos + new Vector3(0.42f, 0.08f, 0f);
                SpriteRenderer signSr = signObj.AddComponent<SpriteRenderer>();
                signSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_forsale_sign", new Vector2(0.5f, 0.18f), 92f);
                IsometricSorting signSort = signObj.AddComponent<IsometricSorting>();
                signSort.Configure(true, 4);
            }

            // Tạo điểm Cửa Vào Nhà ngay trước mặt tiền tòa nhà
            outdoorDoorPositions[type] = doorPos + new Vector3(0f, -0.15f, 0f);
            GameObject doorObj = new GameObject($"Door_{type}");
            doorObj.transform.SetParent(parent);
            doorObj.transform.position = doorPos;

            SpriteRenderer matSr = doorObj.AddComponent<SpriteRenderer>();
            matSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Tiles/iso_door_mat", new Vector2(0.5f, 0.5f), 115f);
            matSr.sortingOrder = -1500;

            BuildingDoorInteractable door = doorObj.AddComponent<BuildingDoorInteractable>();
            door.ConfigureDoor(type, false, outdoorDoorPositions[type], priceCoins, signObj);
        }

        private void SpawnRestaurantTable(Transform parent, int tableNum, Vector3 pos)
        {
            GameObject tObj = new GameObject($"DiningTable_{tableNum}");
            tObj.transform.SetParent(parent);
            tObj.transform.position = pos;

            tObj.AddComponent<SpriteRenderer>();
            IsometricSorting sorting = tObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 0);

            CircleCollider2D col = tObj.AddComponent<CircleCollider2D>();
            col.radius = 0.28f;

            tObj.AddComponent<CozyFurnitureAura>();

            RestaurantTableInteractable table = tObj.AddComponent<RestaurantTableInteractable>();
            table.ConfigureTable(tableNum);
        }

        private void SpawnBed(Transform parent, string title, bool isClinic, Vector3 pos)
        {
            GameObject bObj = new GameObject(title);
            bObj.transform.SetParent(parent);
            bObj.transform.position = pos;

            SpriteRenderer sr = bObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_bed", new Vector2(0.5f, 0.22f), 78f);

            IsometricSorting sorting = bObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 0);

            CircleCollider2D col = bObj.AddComponent<CircleCollider2D>();
            col.radius = 0.42f;

            var bedAura = bObj.AddComponent<CozyFurnitureAura>();
            int bldIndex = Mathf.RoundToInt(pos.x / 40f);
            if (bldIndex >= 1 && bldIndex <= 8)
                bedAura.RoomBuilding = (BuildingType)bldIndex;
            bedAura.CozyScoreBonus = 25;

            BedInteractable bed = bObj.AddComponent<BedInteractable>();
            bed.ConfigureBed(title, isClinic);
        }

        private void SpawnWorkstation(Transform parent, StationType type, string title, string spritePath, Vector3 position)
        {
            GameObject stObj = new GameObject($"Station_{title}");
            stObj.transform.SetParent(parent);
            stObj.transform.position = position;

            SpriteRenderer sr = stObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite(spritePath, new Vector2(0.5f, 0.25f), 88f);

            IsometricSorting sorting = stObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 0);

            CircleCollider2D col = stObj.AddComponent<CircleCollider2D>();
            col.radius = 0.30f;

            WorkstationInteractable ws = stObj.AddComponent<WorkstationInteractable>();
            ws.ConfigureStation(type, title);
        }

        private void SpawnDecorObject(Transform parent, string objName, string spritePath, Vector3 pos, float ppu, bool isCozyFurniture, Vector2? customPivot = null)
        {
            GameObject obj = new GameObject(objName);
            obj.transform.SetParent(parent);
            obj.transform.position = pos;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            Vector2 pivot = customPivot ?? new Vector2(0.5f, 0.18f);
            sr.sprite = CozyLifeDatabase.LoadSprite(spritePath, pivot, ppu);

            IsometricSorting sorting = obj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 0);

            if (isCozyFurniture)
            {
                var aura = obj.AddComponent<CozyFurnitureAura>();
                int bldIndex = Mathf.RoundToInt(pos.x / 40f);
                if (bldIndex >= 1 && bldIndex <= 8)
                    aura.RoomBuilding = (BuildingType)bldIndex;
                aura.CozyScoreBonus = 15;
            }
        }

        private void SpawnStreetLight(Transform parent, string name, Vector3 pos)
        {
            GameObject lampObj = new GameObject(name);
            lampObj.transform.SetParent(parent);
            lampObj.transform.position = pos;

            SpriteRenderer sr = lampObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_streetlight", new Vector2(0.5f, 0.12f), 85f);

            IsometricSorting sorting = lampObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 6);

            // Tạo hào quang ánh đèn vàng ấm tự bật vào buổi tối
            GameObject glowObj = new GameObject("NightGlow");
            glowObj.transform.SetParent(lampObj.transform);
            glowObj.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            glowObj.transform.localScale = new Vector3(1.65f, 1.35f, 1f);

            SpriteRenderer glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_light_glow", new Vector2(0.5f, 0.5f), 70f);
            glowSr.sortingOrder = 1500;
            glowSr.color = new Color(1f, 0.88f, 0.55f, 0.85f);

            streetLightGlows.Add(glowSr);
        }

        private void SpawnFriend(Transform parent, string friendName, ProfessionType prof, Vector3 pos, string greeting, BuildingType building, float roamRadius)
        {
            GameObject fObj = new GameObject($"Friend_{friendName}");
            fObj.transform.SetParent(parent);
            fObj.transform.position = pos;

            fObj.AddComponent<SpriteRenderer>();
            IsometricSorting sorting = fObj.AddComponent<IsometricSorting>();
            sorting.Configure(false, 5);

            TownFriendAgent agent = fObj.AddComponent<TownFriendAgent>();
            agent.InitializeFriend(friendName, prof, pos, greeting, building, roamRadius);
        }

        private void SpawnCampfireWerewolfArea(Transform parent, Vector3 centerPos)
        {
            // Đảm bảo có CozyWerewolfManager trong Scene
            if (FindAnyObjectByType<CozyWerewolfManager>() == null)
            {
                GameObject mgrObj = new GameObject("CozyWerewolfManager");
                mgrObj.transform.SetParent(worldRoot);
                mgrObj.AddComponent<CozyWerewolfManager>();
            }

            GameObject campfireArea = new GameObject("Campfire_Werewolf_Area");
            campfireArea.transform.SetParent(parent);
            campfireArea.transform.position = centerPos;

            // 1. ĐỐNG LỬA TRẠI TRUNG TÂM (Củi xếp nón, than hồng và ngọn lửa ấm áp bốc lên trên)
            GameObject fireObj = new GameObject("Campfire");
            fireObj.transform.SetParent(campfireArea.transform);
            fireObj.transform.position = centerPos;

            SpriteRenderer fireSr = fireObj.AddComponent<SpriteRenderer>();
            fireSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_campfire", new Vector2(0.5f, 0.28f), 85f);
            fireObj.AddComponent<IsometricSorting>().Configure(true, 5);

            CircleCollider2D fireCol = fireObj.AddComponent<CircleCollider2D>();
            fireCol.radius = 0.52f;
            fireCol.isTrigger = true;

            CampfireWerewolfInteractable campfireInteract = fireObj.AddComponent<CampfireWerewolfInteractable>();
            CozyWerewolfManager.Instance?.RegisterCampfire(campfireInteract);

            // 2. KHÚC GỖ PHÍA BẮC (Sau Lửa Trại — 2 Chỗ Ngồi: Ghế #1, #2)
            Vector3 northLogPos = centerPos + new Vector3(0f, 0.58f, 0f);
            GameObject northLogObj = new GameObject("LogBench_North");
            northLogObj.transform.SetParent(campfireArea.transform);
            northLogObj.transform.position = northLogPos;

            SpriteRenderer northSr = northLogObj.AddComponent<SpriteRenderer>();
            northSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_log_bench_horizontal", new Vector2(0.5f, 0.35f), 90f);
            northLogObj.AddComponent<IsometricSorting>().Configure(true, -2);

            SpawnSeatSpot(northLogObj.transform, 0, northLogPos + new Vector3(-0.22f, 0.02f, 0f), campfireInteract);
            SpawnSeatSpot(northLogObj.transform, 1, northLogPos + new Vector3(0.22f, 0.02f, 0f), campfireInteract);

            // 3. KHÚC GỖ PHÍA NAM (Trước Lửa Trại — 2 Chỗ Ngồi: Ghế #3, #4)
            Vector3 southLogPos = centerPos + new Vector3(0f, -0.52f, 0f);
            GameObject southLogObj = new GameObject("LogBench_South");
            southLogObj.transform.SetParent(campfireArea.transform);
            southLogObj.transform.position = southLogPos;

            SpriteRenderer southSr = southLogObj.AddComponent<SpriteRenderer>();
            southSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_log_bench_horizontal", new Vector2(0.5f, 0.35f), 90f);
            southLogObj.AddComponent<IsometricSorting>().Configure(true, 6);

            SpawnSeatSpot(southLogObj.transform, 2, southLogPos + new Vector3(-0.22f, 0.02f, 0f), campfireInteract);
            SpawnSeatSpot(southLogObj.transform, 3, southLogPos + new Vector3(0.22f, 0.02f, 0f), campfireInteract);

            // 4. KHÚC GỖ PHÍA TÂY (Bên Trái Lửa Trại — 2 Chỗ Ngồi: Ghế #5, #6)
            Vector3 westLogPos = centerPos + new Vector3(-0.76f, 0.03f, 0f);
            GameObject westLogObj = new GameObject("LogBench_West");
            westLogObj.transform.SetParent(campfireArea.transform);
            westLogObj.transform.position = westLogPos;

            SpriteRenderer westSr = westLogObj.AddComponent<SpriteRenderer>();
            westSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_log_bench_vertical", new Vector2(0.5f, 0.50f), 90f);
            westLogObj.AddComponent<IsometricSorting>().Configure(true, 1);

            SpawnSeatSpot(westLogObj.transform, 4, westLogPos + new Vector3(0f, 0.20f, 0f), campfireInteract);
            SpawnSeatSpot(westLogObj.transform, 5, westLogPos + new Vector3(0f, -0.16f, 0f), campfireInteract);

            // 5. KHÚC GỖ PHÍA ĐÔNG (Bên Phải Lửa Trại — 2 Chỗ Ngồi: Ghế #7, #8)
            Vector3 eastLogPos = centerPos + new Vector3(0.76f, 0.03f, 0f);
            GameObject eastLogObj = new GameObject("LogBench_East");
            eastLogObj.transform.SetParent(campfireArea.transform);
            eastLogObj.transform.position = eastLogPos;

            SpriteRenderer eastSr = eastLogObj.AddComponent<SpriteRenderer>();
            eastSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_log_bench_vertical", new Vector2(0.5f, 0.50f), 90f);
            eastLogObj.AddComponent<IsometricSorting>().Configure(true, 1);

            SpawnSeatSpot(eastLogObj.transform, 6, eastLogPos + new Vector3(0f, 0.20f, 0f), campfireInteract);
            SpawnSeatSpot(eastLogObj.transform, 7, eastLogPos + new Vector3(0f, -0.16f, 0f), campfireInteract);
        }

        private void SpawnSeatSpot(Transform parent, int seatIndex, Vector3 pos, CampfireWerewolfInteractable campfire)
        {
            GameObject seatObj = new GameObject($"CampfireSeat_{seatIndex + 1}");
            seatObj.transform.SetParent(parent);
            seatObj.transform.position = pos;

            CircleCollider2D col = seatObj.AddComponent<CircleCollider2D>();
            col.radius = 0.38f;
            col.isTrigger = true;

            CampfireSeatInteractable seatInteract = seatObj.AddComponent<CampfireSeatInteractable>();
            seatInteract.ConfigureSeat(seatIndex, campfire);
        }

        private void SpawnFishingPierArea(Transform parent, Vector3 pierPos)
        {
            GameObject pierObj = new GameObject("Park_FishingPier");
            pierObj.transform.SetParent(parent);
            pierObj.transform.position = pierPos;

            SpriteRenderer sr = pierObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_fishing_pier", new Vector2(0.5f, 0.40f), 100f);
            pierObj.AddComponent<IsometricSorting>().Configure(true, 2);

            CircleCollider2D col = pierObj.AddComponent<CircleCollider2D>();
            col.radius = 0.85f;
            col.isTrigger = true;

            var fishing = pierObj.AddComponent<FishingPierInteractable>();
            Vector3 waterSpot = pierPos + new Vector3(-0.35f, 0.22f, 0f);
            fishing.Configure(pierPos, waterSpot);
        }

        private void SpawnTownBulletinBoard(Transform parent, Vector3 boardPos)
        {
            GameObject boardObj = new GameObject("TownSquare_BulletinBoard");
            boardObj.transform.SetParent(parent);
            boardObj.transform.position = boardPos;

            SpriteRenderer sr = boardObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_town_board", new Vector2(0.5f, 0.22f), 90f);
            boardObj.AddComponent<IsometricSorting>().Configure(true, 4);

            CircleCollider2D col = boardObj.AddComponent<CircleCollider2D>();
            col.radius = 0.85f;
            col.isTrigger = true;

            boardObj.AddComponent<TownBulletinBoardInteractable>();
        }

        private void SpawnPetAdoptionArea(Transform parent, Vector3 spotPos)
        {
            GameObject spotObj = new GameObject("Park_PetAdoptionSpot");
            spotObj.transform.SetParent(parent);
            spotObj.transform.position = spotPos;

            SpriteRenderer sr = spotObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_pet_bed", new Vector2(0.5f, 0.35f), 100f);
            spotObj.AddComponent<IsometricSorting>().Configure(true, 2);

            CircleCollider2D col = spotObj.AddComponent<CircleCollider2D>();
            col.radius = 0.80f;
            col.isTrigger = true;

            spotObj.AddComponent<PetAdoptionSpotInteractable>();
        }

        private void SpawnHotSpringOnsenArea(Transform parent, Vector3 onsenPos)
        {
            GameObject onsenObj = new GameObject("Park_HotSpringOnsen");
            onsenObj.transform.SetParent(parent);
            onsenObj.transform.position = onsenPos;

            SpriteRenderer sr = onsenObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_hot_spring", new Vector2(0.5f, 0.35f), 90f);
            onsenObj.AddComponent<IsometricSorting>().Configure(true, 1);

            CircleCollider2D col = onsenObj.AddComponent<CircleCollider2D>();
            col.radius = 1.15f;
            col.isTrigger = true;

            onsenObj.AddComponent<CozyOnsenHotSpringInteractable>();
        }

        private void SpawnStreetFoodCart(Transform parent, Vector3 cartPos)
        {
            GameObject cartObj = new GameObject("TownSquare_StreetFoodCart");
            cartObj.transform.SetParent(parent);
            cartObj.transform.position = cartPos;

            SpriteRenderer sr = cartObj.AddComponent<SpriteRenderer>();
            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_food_cart", new Vector2(0.5f, 0.20f), 90f);
            cartObj.AddComponent<IsometricSorting>().Configure(true, 4);

            CircleCollider2D col = cartObj.AddComponent<CircleCollider2D>();
            col.radius = 0.90f;
            col.isTrigger = true;

            cartObj.AddComponent<TownStreetFoodCartInteractable>();
        }
    }
}
