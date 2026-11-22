using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyLife.Core
{
    /// <summary>
    /// Các nghề nghiệp chính trong thế giới mở CozyLife.
    /// Mỗi nghề có kỹ năng đặc thù và tạo thành vòng lặp tương hỗ giữa những người chơi.
    /// </summary>
    public enum ProfessionType
    {
        Farmer = 0,     // Nông dân: Trồng trọt nhanh gấp đôi, thu hoạch nhiều nông sản
        Chef = 1,       // Đầu bếp: Nấu món ăn hồi nhiều Đói/Khát và tạo hiệu ứng No Bụng (Well-Fed)
        Doctor = 2,     // Bác sĩ: Pha trà thảo mộc, chế túi sơ cứu và băng bó hồi Máu cho bạn bè
        Carpenter = 3   // Thợ mộc: Khai thác gỗ và đóng nội thất trang trí thế giới mở
    }

    /// <summary>
    /// Các khu vực / Ngôi nhà có thể bước vào bên trong (Interior Scenes / Rooms).
    /// </summary>
    public enum BuildingType
    {
        None = 0,               // Ngoài trời (Thị trấn mở)
        Restaurant = 1,         // Nhà Hàng & Tiệm Bánh Ấm Cúng (Ngồi bàn & Order món ăn)
        Market = 2,             // Siêu Thị & Chợ Nông Sản
        Clinic = 3,             // Phòng Khám Y Tế Thị Trấn
        Workshop = 4,           // Xưởng Thợ Mộc & Nội Thất
        PlayerHome = 5,         // Ngôi Nhà Khởi Đầu Của Bạn
        BuyableCottage = 6,     // Nhà Gỗ Ven Công Viên (Mua: 150 Xu)
        BuyableTownhouse = 7,   // Nhà Phố Khu Đông (Mua: 250 Xu)
        BuyableVilla = 8,       // Biệt Thự Đồi Thông (Mua: 400 Xu)
        Cafe = 9                // Quán Cafe & Trà Đạo Nhật Bản (Mẫu 3)
    }

    /// <summary>
    /// Danh sách vật phẩm trong game.
    /// </summary>
    public enum ItemType
    {
        None = 0,
        CropSeed = 1,       // Hạt giống nông sản
        Wheat = 2,          // Lúa mì tươi
        Tomato = 3,         // Cà chua chín
        WaterBottle = 4,    // Chai nước sạch
        BakedBread = 5,     // Bánh mì nướng thơm
        TomatoSoup = 6,     // Súp cà chua kem ấm
        HerbalTea = 7,      // Trà thảo mộc thanh nhiệt
        Medkit = 8,         // Túi sơ cứu y tế
        WoodPlank = 9,      // Gỗ ván thông
        CozyChair = 10,     // Ghế gỗ ấm cúng (có thể đặt trang trí)
        TeaTable = 11,      // Bàn trà gỗ thông (có ấm chén trà)
        PersianRug = 12,    // Thảm dệt hoa văn trải sàn
        PottedPlant = 13,   // Chậu cây cảnh xanh mát
        FloorLamp = 14,     // Đèn ngủ ánh vàng ấm áp
        Bookshelf = 15,     // Tủ sách gỗ thông nhiều ngăn
        RawMeat = 16,       // Thịt tươi tươi ngon mua ở cửa hàng
        RawFish = 17,       // Cá tươi biển sâu mua ở cửa hàng
        GrilledSteak = 18,  // Bò bít tết áp chảo bơ tỏi thơm lừng
        GrilledFish = 19,   // Cá nướng giòn thơm sốt cà chua
        HeartyBurger = 20,  // Burger bò phô mai xà lách đại tiệc
        KitchenStove = 21,  // Khu bếp nấu ăn gia đình bằng gỗ & đá đặt trong nhà
        LuxurySofa = 22,    // Ghế Sofa Bắc Âu nệm nhung ấm êm (+35 Điểm Ấm Cúng)
        StoneFireplace = 23,// Lò sưởi gạch đá củi ấm (+45 Điểm Ấm Cúng)
        FishAquarium = 24,  // Bể cá cảnh sinh động (+40 Điểm Ấm Cúng)
        ClassicPiano = 25,  // Đàn Piano gỗ cổ điển (+50 Điểm Ấm Cúng)
        VintageGramophone = 26, // Máy hát đĩa than Lo-Fi (+35 Điểm Ấm Cúng)
        FishingRod = 27,    // Cần câu cá thủ công
        FishGoldenCarp = 28,// Cá chép vàng hồ sinh thái
        FishSalmon = 29,    // Cá hồi suối
        FishMoonlight = 30, // Cá Nguyệt Quang Thần Thoại
        PetBed = 31,        // Nệm thú cưng ấm áp
        Bicycle = 32        // Xe đạp dạo phố thị trấn
    }

    public enum ItemCategory
    {
        Seed,
        RawIngredient,
        FoodAndDrink,
        Medical,
        Material,
        PlaceableFurniture
    }

    /// <summary>
    /// Các trạm tương tác nghề nghiệp trong thế giới.
    /// </summary>
    public enum StationType
    {
        None = 0,
        Farmland = 1,       // Ô đất ruộng
        CookingStove = 2,   // Bếp lò tiệm bánh / nhà hàng
        WaterWell = 3,      // Giếng nước thị trấn
        MedicalStation = 4, // Bàn y tế phòng khám
        Workbench = 5,      // Bàn cưa xưởng mộc
        MarketStall = 6     // Quầy tạp hóa / chợ thị trấn
    }

    /// <summary>
    /// Hệ thống "Moodles" lấy cảm hứng từ Project Zomboid:
    /// Biểu thị trạng thái sinh tồn và cảm xúc của nhân vật ở góc phải màn hình.
    /// </summary>
    public enum MoodleType
    {
        WellFed,        // No bụng: Hồi máu nhẹ theo thời gian, tăng tốc độ làm việc
        Hungry,         // Đói bụng: Cần tìm đồ ăn sớm
        Starving,       // Đói lả: Mất máu từ từ, giảm thể lực
        Hydrated,       // Đủ nước: Thể lực hồi phục nhanh
        Thirsty,        // Khát nước: Tốc độ hồi thể lực giảm
        Dehydrated,     // Kiệt nước: Mất máu và đi chậm lại
        Injured,        // Bị thương: Máu thấp, cần Bác sĩ hoặc Túi sơ cứu
        Tired,          // Mệt mỏi: Thể lực thấp
        CozyRested,     // Thư giãn: Đang ở gần nội thất / đang ngồi bàn nhà hàng
        SickFever,      // Sốt: Đi chậm -30%, tốn thể lực x1.6, hồi sức -55%
        SickHeatstroke, // Sốc nhiệt: Đi chậm -35%, tốn thể lực x1.8, hồi sức -60%
        SickCold        // Cảm lạnh: Đi chậm -25%, tốn thể lực x1.5, hồi sức -50%
    }

    /// <summary>
    /// Các kiểu thời tiết trong thế giới mở CozyLife (kéo dài 12 tiếng trong game).
    /// </summary>
    public enum WeatherType
    {
        Clear = 0,      // Nắng ráo / Quang đãng
        Rain = 1,       // Mưa rào
        Heatwave = 2,   // Nắng gắt thiêu đốt
        Snow = 3        // Bão tuyết buốt giá
    }

    /// <summary>
    /// Các trạng thái bệnh tật khi phơi mình dưới thời tiết xấu quá lâu.
    /// </summary>
    public enum SicknessType
    {
        None = 0,
        Fever = 1,      // Sốt (do dầm mưa)
        Heatstroke = 2, // Sốc nhiệt (do dầm nắng gắt)
        Cold = 3        // Cảm lạnh (do dầm tuyết)
    }

    [Serializable]
    public class ProfessionDefinition
    {
        public ProfessionType type;
        public string displayName;
        public string titleBadge;
        public string description;
        public string passiveBonusText;
        public Color themeColor;
        public string spriteResourcePath;
        public float moveSpeedMultiplier = 1f;
        public float workSpeedMultiplier = 1f;
    }

    [Serializable]
    public class ItemDefinition
    {
        public ItemType type;
        public string displayName;
        public string description;
        public ItemCategory category;
        public string iconResourcePath;
        public float restoreHealth;
        public float restoreHunger;
        public float restoreThirst;
        public float restoreStamina;
        public int basePrice;
        public string placeableObjectSpritePath;
        public int cozyScoreBonus;
    }

    [Serializable]
    public struct RecipeIngredient
    {
        public ItemType item;
        public int count;

        public RecipeIngredient(ItemType item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }

    [Serializable]
    public class RecipeDefinition
    {
        public string recipeId;
        public string displayName;
        public ItemType resultItem;
        public int resultCount = 1;
        public ProfessionType recommendedProfession;
        public bool requireExactProfession;
        public StationType requiredStation;
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();
        public string description;
    }

    /// <summary>
    /// Món ăn trong Thực Đơn Gọi Món (Order Menu) khi ngồi bàn trong Nhà Hàng.
    /// </summary>
    [Serializable]
    public class RestaurantDishDefinition
    {
        public string dishId;
        public string displayName;
        public string description;
        public string iconResourcePath;
        public int priceCoins;
        public float restoreHealth;
        public float restoreHunger;
        public float restoreThirst;
        public float restoreStamina;
        public ItemType takeawayBonusItem;
    }

    [Serializable]
    public class InventorySlot
    {
        public ItemType itemType;
        public int count;

        public InventorySlot(ItemType type, int count)
        {
            this.itemType = type;
            this.count = count;
        }
    }

    [Serializable]
    public struct MoodleActiveInfo
    {
        public MoodleType type;
        public string title;
        public string subtitle;
        public Color badgeColor;
        public bool isPositive;
    }

    /// <summary>
    /// Cấu trúc dữ liệu đồng bộ hóa (Network-Ready Snapshot).
    /// </summary>
    [Serializable]
    public struct CharacterVitalsSnapshot
    {
        public float health;
        public float maxHealth;
        public float hunger;
        public float maxHunger;
        public float thirst;
        public float maxThirst;
        public float stamina;
        public float maxStamina;
        public ProfessionType profession;
        public BuildingType currentBuilding;
    }

    // =====================================================================
    // DỮ LIỆU LƯU GAME BỀN VỮNG (PERSISTENT SAVE SYSTEM DATA)
    // =====================================================================

    [Serializable]
    public class SavedCropPlotData
    {
        public int plotIndex;
        public int cropStage;
        public float growthGameMinutes;
        public float requiredGameMinutes;
    }

    [Serializable]
    public class SavedPlacedFurnitureData
    {
        public ItemType itemType;
        public BuildingType building;
        public float posX;
        public float posY;
        public bool isFlipped;
    }

    [Serializable]
    public class SavedFriendData
    {
        public string friendName;
        public int friendshipPoints;
        public float friendHealth;
        public float friendHunger;
    }

    [Serializable]
    public class CozyLifeSaveData
    {
        public int saveVersion = 1;
        public string savedAtTimestamp = "";

        // 1. Nhân vật & Chỉ số sinh tồn
        public string playerName = "Bạn";
        public ProfessionType currentProfession = ProfessionType.Farmer;
        public int coins = 120;
        public float health = 100f;
        public float hunger = 85f;
        public float thirst = 85f;
        public float stamina = 100f;
        public SicknessType currentSickness = SicknessType.None;
        public float sicknessDurationMinutes = 0f;
        public float outdoorWeatherExposureMinutes = 0f;

        // 2. Thời gian Thế Giới, Thời tiết & Vị trí
        public int currentDay = 1;
        public float currentHour = 8.0f;
        public WeatherType currentWeather = WeatherType.Clear;
        public float weatherHoursRemaining = 0f;
        public float nextWeatherEventHours = 28f;
        public BuildingType currentBuilding = BuildingType.None;
        public float playerPosX = 0f;
        public float playerPosY = 0f;

        // 3. Túi đồ, Bất động sản đã mua, Nông trại, Nội thất trang trí & Bạn bè
        public List<InventorySlot> inventory = new List<InventorySlot>();
        public List<BuildingType> unlockedBuildings = new List<BuildingType>();
        public List<SavedCropPlotData> cropPlots = new List<SavedCropPlotData>();
        public List<SavedPlacedFurnitureData> placedFurniture = new List<SavedPlacedFurnitureData>();
        public List<SavedFriendData> friends = new List<SavedFriendData>();
    }
}
