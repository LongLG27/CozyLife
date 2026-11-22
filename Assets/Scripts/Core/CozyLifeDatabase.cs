using System.Collections.Generic;
using UnityEngine;

namespace CozyLife.Core
{
    /// <summary>
    /// Cơ sở dữ liệu trung tâm với mô tả ngắn gọn, súc tích.
    /// Quy ước thời gian: 1 phút thực tế (60s) = 1 tiếng trong game (60 phút game).
    /// </summary>
    public static class CozyLifeDatabase
    {
        private static Dictionary<ProfessionType, ProfessionDefinition> professions;
        private static Dictionary<ItemType, ItemDefinition> items;
        private static List<RecipeDefinition> recipes;
        private static List<RestaurantDishDefinition> restaurantMenu;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            spriteCache.Clear();
            texCache.Clear();
        }

        public static void EnsureInitialized()
        {
            if (professions != null && items != null && recipes != null && restaurantMenu != null)
                return;

            InitProfessions();
            InitItems();
            InitRecipes();
            InitRestaurantMenu();
        }

        public static string GetBuildingVietnameseName(BuildingType building)
        {
            return building switch
            {
                BuildingType.Restaurant => "🍽️ Nhà Hàng",
                BuildingType.Market => "🏪 Siêu Thị",
                BuildingType.Clinic => "⚕️ Phòng Khám",
                BuildingType.Workshop => "🔨 Xưởng Mộc",
                BuildingType.PlayerHome => "🏠 Nhà Khởi Đầu",
                BuildingType.BuyableCottage => "🏡 Nhà Gỗ Ven Công Viên",
                BuildingType.BuyableTownhouse => "🏘️ Nhà Phố Khu Đông",
                BuildingType.BuyableVilla => "🏰 Biệt Thự Đồi Thông",
                BuildingType.Cafe => "☕ Quán Cafe Nhật Bản",
                _ => "🌳 Thị Trấn"
            };
        }

        public static bool IsResidentialHome(BuildingType building)
        {
            return building == BuildingType.PlayerHome ||
                   building == BuildingType.BuyableCottage ||
                   building == BuildingType.BuyableTownhouse ||
                   building == BuildingType.BuyableVilla;
        }

        private static void InitProfessions()
        {
            professions = new Dictionary<ProfessionType, ProfessionDefinition>
            {
                [ProfessionType.Farmer] = new ProfessionDefinition
                {
                    type = ProfessionType.Farmer,
                    displayName = "Nông Dân",
                    titleBadge = "🌱 Nông Dân",
                    description = "Trồng trọt và cung cấp nông sản.",
                    passiveBonusText = "• Trồng & thu hoạch nhanh x2 (10 phút)\n• Sản lượng thu hoạch x2",
                    themeColor = new Color(0.38f, 0.72f, 0.32f),
                    spriteResourcePath = "CozyLifeArt/Characters/char_farmer",
                    moveSpeedMultiplier = 1.05f,
                    workSpeedMultiplier = 2.0f
                },
                [ProfessionType.Chef] = new ProfessionDefinition
                {
                    type = ProfessionType.Chef,
                    displayName = "Đầu Bếp",
                    titleBadge = "🍳 Đầu Bếp",
                    description = "Nấu món ngon và điều hành Nhà Hàng.",
                    passiveBonusText = "• Nấu Súp Cà Chua & nhận x2 khẩu phần\n• Giảm 50% giá ăn Nhà Hàng, Phụ bếp +35 Xu",
                    themeColor = new Color(0.92f, 0.46f, 0.26f),
                    spriteResourcePath = "CozyLifeArt/Characters/char_chef",
                    moveSpeedMultiplier = 1.0f,
                    workSpeedMultiplier = 1.5f
                },
                [ProfessionType.Doctor] = new ProfessionDefinition
                {
                    type = ProfessionType.Doctor,
                    displayName = "Bác Sĩ",
                    titleBadge = "⚕️ Bác Sĩ",
                    description = "Chữa trị và pha chế thuốc tại Phòng Khám.",
                    passiveBonusText = "• Chế Túi Sơ Cứu & x2 Trà Thảo Mộc\n• Tăng +50% hiệu quả hồi Máu",
                    themeColor = new Color(0.28f, 0.65f, 0.92f),
                    spriteResourcePath = "CozyLifeArt/Characters/char_doctor",
                    moveSpeedMultiplier = 1.05f,
                    workSpeedMultiplier = 1.5f
                },
                [ProfessionType.Carpenter] = new ProfessionDefinition
                {
                    type = ProfessionType.Carpenter,
                    displayName = "Thợ Mộc",
                    titleBadge = "🔨 Thợ Mộc",
                    description = "Xẻ gỗ và đóng nội thất trang trí.",
                    passiveBonusText = "• Xẻ gỗ nhận x2 Gỗ Ván Thông\n• Đóng Ghế Gỗ nhanh (30p) & nhận +1 ghế phụ",
                    themeColor = new Color(0.82f, 0.56f, 0.25f),
                    spriteResourcePath = "CozyLifeArt/Characters/char_carpenter",
                    moveSpeedMultiplier = 1.0f,
                    workSpeedMultiplier = 1.6f
                }
            };
        }

        private static void InitItems()
        {
            items = new Dictionary<ItemType, ItemDefinition>
            {
                [ItemType.CropSeed] = new ItemDefinition
                {
                    type = ItemType.CropSeed,
                    displayName = "Hạt Giống",
                    description = "Gieo tại Ô Đất [E] (Mất 15p).",
                    category = ItemCategory.Seed,
                    iconResourcePath = "CozyLifeArt/Items/item_seed",
                    basePrice = 5
                },
                [ItemType.Wheat] = new ItemDefinition
                {
                    type = ItemType.Wheat,
                    displayName = "Lúa Mì",
                    description = "Nguyên liệu nướng Bánh Mì.",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_wheat",
                    restoreHunger = 10f,
                    basePrice = 10
                },
                [ItemType.Tomato] = new ItemDefinition
                {
                    type = ItemType.Tomato,
                    displayName = "Cà Chua",
                    description = "Ăn nhẹ (+12 No, +10 Nước) hoặc nấu Súp.",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_tomato",
                    restoreHunger = 12f,
                    restoreThirst = 10f,
                    basePrice = 12
                },
                [ItemType.WaterBottle] = new ItemDefinition
                {
                    type = ItemType.WaterBottle,
                    displayName = "Chai Nước",
                    description = "Uống mất 15p (+45 Nước, +15 Sức).",
                    category = ItemCategory.FoodAndDrink,
                    iconResourcePath = "CozyLifeArt/Items/item_water",
                    restoreThirst = 45f,
                    restoreStamina = 15f,
                    basePrice = 8
                },
                [ItemType.BakedBread] = new ItemDefinition
                {
                    type = ItemType.BakedBread,
                    displayName = "Bánh Mì",
                    description = "Ăn mất 30p (+45 No, +10 Máu).",
                    category = ItemCategory.FoodAndDrink,
                    iconResourcePath = "CozyLifeArt/Items/item_bread",
                    restoreHealth = 10f,
                    restoreHunger = 45f,
                    restoreStamina = 20f,
                    basePrice = 25
                },
                [ItemType.TomatoSoup] = new ItemDefinition
                {
                    type = ItemType.TomatoSoup,
                    displayName = "Súp Cà Chua",
                    description = "Ăn mất 30p (+55 No, +35 Nước, +15 Máu).",
                    category = ItemCategory.FoodAndDrink,
                    iconResourcePath = "CozyLifeArt/Items/item_soup",
                    restoreHealth = 15f,
                    restoreHunger = 55f,
                    restoreThirst = 35f,
                    restoreStamina = 30f,
                    basePrice = 40
                },
                [ItemType.HerbalTea] = new ItemDefinition
                {
                    type = ItemType.HerbalTea,
                    displayName = "Trà Thảo Mộc",
                    description = "Uống mất 20p (+25 Máu, +50 Nước, +40 Sức).",
                    category = ItemCategory.Medical,
                    iconResourcePath = "CozyLifeArt/Items/item_tea",
                    restoreHealth = 25f,
                    restoreThirst = 50f,
                    restoreStamina = 40f,
                    basePrice = 35
                },
                [ItemType.Medkit] = new ItemDefinition
                {
                    type = ItemType.Medkit,
                    displayName = "Túi Sơ Cứu",
                    description = "Băng bó mất 30p (+60 Máu).",
                    category = ItemCategory.Medical,
                    iconResourcePath = "CozyLifeArt/Items/item_medkit",
                    restoreHealth = 60f,
                    restoreStamina = 20f,
                    basePrice = 50
                },
                [ItemType.WoodPlank] = new ItemDefinition
                {
                    type = ItemType.WoodPlank,
                    displayName = "Gỗ Thông",
                    description = "Nguyên liệu đóng bàn ghế.",
                    category = ItemCategory.Material,
                    iconResourcePath = "CozyLifeArt/Items/item_wood",
                    basePrice = 10
                },
                [ItemType.CozyChair] = new ItemDefinition
                {
                    type = ItemType.CozyChair,
                    displayName = "Ghế Gỗ",
                    description = "Ghế gỗ thông ấm áp (+15 Điểm Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_chair",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_chair",
                    basePrice = 35,
                    cozyScoreBonus = 15
                },
                [ItemType.TeaTable] = new ItemDefinition
                {
                    type = ItemType.TeaTable,
                    displayName = "Bàn Trà Gỗ",
                    description = "Bàn trà kèm ấm chén sứ (+25 Điểm Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_tea_table",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_tea_table",
                    basePrice = 55,
                    cozyScoreBonus = 25
                },
                [ItemType.PersianRug] = new ItemDefinition
                {
                    type = ItemType.PersianRug,
                    displayName = "Thảm Hoa Văn",
                    description = "Thảm dệt thổ cẩm trải sàn (+30 Điểm Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_rug",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_rug",
                    basePrice = 65,
                    cozyScoreBonus = 30
                },
                [ItemType.PottedPlant] = new ItemDefinition
                {
                    type = ItemType.PottedPlant,
                    displayName = "Chậu Cây Cảnh",
                    description = "Chậu cây xanh lọc không khí (+20 Điểm Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_potted_plant",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_potted_plant",
                    basePrice = 40,
                    cozyScoreBonus = 20
                },
                [ItemType.FloorLamp] = new ItemDefinition
                {
                    type = ItemType.FloorLamp,
                    displayName = "Đèn Ngủ Vàng",
                    description = "Đèn cây tỏa ánh sáng ấm áp (+25 Điểm Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_floor_lamp",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_floor_lamp",
                    basePrice = 60,
                    cozyScoreBonus = 25
                },
                [ItemType.Bookshelf] = new ItemDefinition
                {
                    type = ItemType.Bookshelf,
                    displayName = "Tủ Sách Gỗ",
                    description = "Tủ sách gỗ thông 3 tầng (+35 Điểm Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_bookshelf",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_bookshelf",
                    basePrice = 80,
                    cozyScoreBonus = 35
                },
                [ItemType.RawMeat] = new ItemDefinition
                {
                    type = ItemType.RawMeat,
                    displayName = "Thịt Tươi",
                    description = "Thịt bò tươi ngon mua ở Siêu Thị. Dùng nấu Bò Bít Tết & Burger.",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_meat",
                    restoreHunger = 15f,
                    basePrice = 15
                },
                [ItemType.RawFish] = new ItemDefinition
                {
                    type = ItemType.RawFish,
                    displayName = "Cá Tươi",
                    description = "Cá tươi ngon mua ở Siêu Thị. Dùng nướng giòn hoặc nấu canh.",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_fish",
                    restoreHunger = 15f,
                    restoreThirst = 8f,
                    basePrice = 12
                },
                [ItemType.GrilledSteak] = new ItemDefinition
                {
                    type = ItemType.GrilledSteak,
                    displayName = "Bò Bít Tết Áp Chảo",
                    description = "Bít tết áp chảo bơ tỏi thơm lừng (+65 No, +35 Sức, +20 Máu).",
                    category = ItemCategory.FoodAndDrink,
                    iconResourcePath = "CozyLifeArt/Items/item_steak",
                    restoreHealth = 20f,
                    restoreHunger = 65f,
                    restoreStamina = 35f,
                    basePrice = 45
                },
                [ItemType.GrilledFish] = new ItemDefinition
                {
                    type = ItemType.GrilledFish,
                    displayName = "Cá Nướng Giòn Thơm",
                    description = "Cá nướng sốt cà chua và chanh tươi (+55 No, +25 Nước, +25 Sức).",
                    category = ItemCategory.FoodAndDrink,
                    iconResourcePath = "CozyLifeArt/Items/item_grilled_fish",
                    restoreHealth = 15f,
                    restoreHunger = 55f,
                    restoreThirst = 25f,
                    restoreStamina = 25f,
                    basePrice = 40
                },
                [ItemType.HeartyBurger] = new ItemDefinition
                {
                    type = ItemType.HeartyBurger,
                    displayName = "Burger Bò Phô Mai",
                    description = "Burger bò phô mai xà lách đại tiệc (+80 No, +40 Sức, +30 Máu).",
                    category = ItemCategory.FoodAndDrink,
                    iconResourcePath = "CozyLifeArt/Items/item_burger",
                    restoreHealth = 30f,
                    restoreHunger = 80f,
                    restoreStamina = 40f,
                    basePrice = 65
                },
                [ItemType.KitchenStove] = new ItemDefinition
                {
                    type = ItemType.KitchenStove,
                    displayName = "Khu Bếp Nấu Ăn Gia Đình",
                    description = "Bếp gỗ & mặt đá granit đặt trong nhà để tự nấu mọi món ăn (+40 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_kitchen_stove",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_kitchen_stove",
                    basePrice = 120,
                    cozyScoreBonus = 40
                },
                [ItemType.LuxurySofa] = new ItemDefinition
                {
                    type = ItemType.LuxurySofa,
                    displayName = "Sofa Bắc Âu Cao Cấp",
                    description = "Sofa nệm nhung màu xanh ngọc êm ái kèm gối tựa và chăn len (+35 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_sofa",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_sofa",
                    basePrice = 95,
                    cozyScoreBonus = 35
                },
                [ItemType.StoneFireplace] = new ItemDefinition
                {
                    type = ItemType.StoneFireplace,
                    displayName = "Lò Sưởi Đá Tự Nhiên",
                    description = "Lò sưởi gạch đá với ngọn lửa reo bập bùng và hơi ấm lan tỏa (+45 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_fireplace",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_fireplace",
                    basePrice = 150,
                    cozyScoreBonus = 45
                },
                [ItemType.FishAquarium] = new ItemDefinition
                {
                    type = ItemType.FishAquarium,
                    displayName = "Bể Cá Cảnh Thủy Sinh",
                    description = "Bể kính nước trong veo với cá vàng bơi lội và bọt khí mát lành (+40 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_aquarium",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_aquarium",
                    basePrice = 130,
                    cozyScoreBonus = 40
                },
                [ItemType.ClassicPiano] = new ItemDefinition
                {
                    type = ItemType.ClassicPiano,
                    displayName = "Đàn Piano Gỗ Cổ Điển",
                    description = "Đàn piano phím ngà bóng loáng. Bấm [E] để ngồi chơi nhạc du dương (+50 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_piano",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_piano",
                    basePrice = 220,
                    cozyScoreBonus = 50
                },
                [ItemType.VintageGramophone] = new ItemDefinition
                {
                    type = ItemType.VintageGramophone,
                    displayName = "Máy Hát Đĩa Than Lo-Fi",
                    description = "Loa kèn đồng cổ điển phát các bản nhạc Lo-Fi thư thái trong nhà (+35 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_gramophone",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_gramophone",
                    basePrice = 110,
                    cozyScoreBonus = 35
                },
                [ItemType.PetBed] = new ItemDefinition
                {
                    type = ItemType.PetBed,
                    displayName = "Nệm Thú Cưng Ấm Áp",
                    description = "Nệm nỉ êm ái cho thú cưng ngủ ngon giấc bên cạnh bạn (+25 Ấm Cúng).",
                    category = ItemCategory.PlaceableFurniture,
                    iconResourcePath = "CozyLifeArt/Items/item_pet_bed",
                    placeableObjectSpritePath = "CozyLifeArt/Objects/obj_pet_bed",
                    basePrice = 60,
                    cozyScoreBonus = 25
                },
                [ItemType.FishingRod] = new ItemDefinition
                {
                    type = ItemType.FishingRod,
                    displayName = "Cần Câu Cá Thủ Công",
                    description = "Dùng tại Cầu Câu Cá Công Viên [E] để câu các loài cá tươi ngon.",
                    category = ItemCategory.Material,
                    iconResourcePath = "CozyLifeArt/Items/item_fishing_rod",
                    basePrice = 35
                },
                [ItemType.FishGoldenCarp] = new ItemDefinition
                {
                    type = ItemType.FishGoldenCarp,
                    displayName = "Cá Chép Vàng",
                    description = "Cá chép vàng vảy lấp lánh câu từ hồ sinh thái (+25 No, +15 Máu).",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_fish_golden_carp",
                    restoreHunger = 25f,
                    restoreHealth = 15f,
                    basePrice = 40
                },
                [ItemType.FishSalmon] = new ItemDefinition
                {
                    type = ItemType.FishSalmon,
                    displayName = "Cá Hồi Suối",
                    description = "Cá hồi tươi ngon thịt đỏ cam giàu đạm (+35 No, +20 Máu, +15 Sức).",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_fish_salmon",
                    restoreHunger = 35f,
                    restoreHealth = 20f,
                    restoreStamina = 15f,
                    basePrice = 55
                },
                [ItemType.FishMoonlight] = new ItemDefinition
                {
                    type = ItemType.FishMoonlight,
                    displayName = "Cá Nguyệt Quang Thần Thoại",
                    description = "Loài cá quý hiếm chỉ cắn câu vào đêm trăng sáng (+50 No, +40 Máu, +50 Sức).",
                    category = ItemCategory.RawIngredient,
                    iconResourcePath = "CozyLifeArt/Items/item_fish_moonlight",
                    restoreHunger = 50f,
                    restoreHealth = 40f,
                    restoreStamina = 50f,
                    basePrice = 120
                },
                [ItemType.Bicycle] = new ItemDefinition
                {
                    type = ItemType.Bicycle,
                    displayName = "Xe Đạp Dạo Phố",
                    description = "Bấm [B] khi ở ngoài trời để đạp xe (+80% tốc độ di chuyển, nhấn [Space] bấm chuông).",
                    category = ItemCategory.Material,
                    iconResourcePath = "CozyLifeArt/Items/item_bicycle",
                    basePrice = 85
                }
            };
        }

        private static void InitRecipes()
        {
            recipes = new List<RecipeDefinition>
            {
                new RecipeDefinition
                {
                    recipeId = "cook_bread",
                    displayName = "Nướng Bánh Mì (30p)",
                    resultItem = ItemType.BakedBread,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Chef,
                    requireExactProfession = false,
                    requiredStation = StationType.CookingStove,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.Wheat, 2) },
                    description = "Đầu Bếp nhận +1 bánh phụ"
                },
                new RecipeDefinition
                {
                    recipeId = "cook_soup",
                    displayName = "Nấu Súp Cà Chua (40p)",
                    resultItem = ItemType.TomatoSoup,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Chef,
                    requireExactProfession = true,
                    requiredStation = StationType.CookingStove,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.Tomato, 2),
                        new RecipeIngredient(ItemType.WaterBottle, 1)
                    },
                    description = "Riêng Đầu Bếp (+55 No, +35 Nước)"
                },
                new RecipeDefinition
                {
                    recipeId = "cook_steak",
                    displayName = "Bò Bít Tết Áp Chảo (35p)",
                    resultItem = ItemType.GrilledSteak,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Chef,
                    requireExactProfession = false,
                    requiredStation = StationType.CookingStove,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.RawMeat, 1),
                        new RecipeIngredient(ItemType.Tomato, 1)
                    },
                    description = "Đậm đà bơ tỏi (+65 No, +35 Sức, +20 Máu)"
                },
                new RecipeDefinition
                {
                    recipeId = "cook_grilled_fish",
                    displayName = "Cá Nướng Giòn Thơm (30p)",
                    resultItem = ItemType.GrilledFish,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Chef,
                    requireExactProfession = false,
                    requiredStation = StationType.CookingStove,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.RawFish, 1),
                        new RecipeIngredient(ItemType.Tomato, 1)
                    },
                    description = "Cá nướng sốt cà chua (+55 No, +25 Nước, +25 Sức)"
                },
                new RecipeDefinition
                {
                    recipeId = "cook_burger",
                    displayName = "Burger Bò Phô Mai (45p)",
                    resultItem = ItemType.HeartyBurger,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Chef,
                    requireExactProfession = false,
                    requiredStation = StationType.CookingStove,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.BakedBread, 1),
                        new RecipeIngredient(ItemType.RawMeat, 1),
                        new RecipeIngredient(ItemType.Tomato, 1)
                    },
                    description = "Đại tiệc dinh dưỡng (+80 No, +40 Sức, +30 Máu)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_tea",
                    displayName = "Pha Trà Thảo Mộc (30p)",
                    resultItem = ItemType.HerbalTea,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Doctor,
                    requireExactProfession = false,
                    requiredStation = StationType.MedicalStation,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.Tomato, 1),
                        new RecipeIngredient(ItemType.WaterBottle, 1)
                    },
                    description = "Bác Sĩ nhận x2 bình"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_medkit",
                    displayName = "Chế Túi Sơ Cứu (40p)",
                    resultItem = ItemType.Medkit,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Doctor,
                    requireExactProfession = true,
                    requiredStation = StationType.MedicalStation,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.Wheat, 1),
                        new RecipeIngredient(ItemType.WaterBottle, 1)
                    },
                    description = "Riêng Bác Sĩ"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_chair",
                    displayName = "Đóng Ghế Gỗ (45p)",
                    resultItem = ItemType.CozyChair,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 2) },
                    description = "Thợ Mộc đóng 30p & +1 ghế"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_tea_table",
                    displayName = "Đóng Bàn Trà Gỗ (45p)",
                    resultItem = ItemType.TeaTable,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 3) },
                    description = "+25 Điểm Ấm Cúng"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_potted_plant",
                    displayName = "Ghép Chậu Cây Cảnh (30p)",
                    resultItem = ItemType.PottedPlant,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.WoodPlank, 1),
                        new RecipeIngredient(ItemType.CropSeed, 2)
                    },
                    description = "+20 Điểm Ấm Cúng"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_floor_lamp",
                    displayName = "Chế Đèn Ngủ Ánh Vàng (45p)",
                    resultItem = ItemType.FloorLamp,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 3) },
                    description = "Tỏa sáng ấm áp (+25 Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_rug",
                    displayName = "Dệt Thảm Hoa Văn (45p)",
                    resultItem = ItemType.PersianRug,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.Wheat, 2),
                        new RecipeIngredient(ItemType.WoodPlank, 1)
                    },
                    description = "Trải sàn nhà (+30 Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_bookshelf",
                    displayName = "Đóng Tủ Sách Gỗ Thông (45p)",
                    resultItem = ItemType.Bookshelf,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = true,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 4) },
                    description = "Riêng Thợ Mộc (+35 Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_kitchen_stove",
                    displayName = "Đóng Khu Bếp Nấu Ăn (45p)",
                    resultItem = ItemType.KitchenStove,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 4) },
                    description = "Đặt trong nhà để tự nấu ăn (+40 Điểm Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "extract_seeds",
                    displayName = "Tách Hạt Giống x3 (15p)",
                    resultItem = ItemType.CropSeed,
                    resultCount = 3,
                    recommendedProfession = ProfessionType.Farmer,
                    requireExactProfession = false,
                    requiredStation = StationType.Farmland,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.Tomato, 1) },
                    description = "Làm tại Ô Đất Nông Trại"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_fishing_rod",
                    displayName = "Chế Tạo Cần Câu Cá (25p)",
                    resultItem = ItemType.FishingRod,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 2) },
                    description = "Dùng tại Cầu Câu Cá Công Viên"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_sofa",
                    displayName = "Đóng Sofa Bắc Âu Nệm Nhung (40p)",
                    resultItem = ItemType.LuxurySofa,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.WoodPlank, 4),
                        new RecipeIngredient(ItemType.Wheat, 2)
                    },
                    description = "Nội thất cao cấp (+35 Điểm Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_fireplace",
                    displayName = "Xây Lò Sưởi Đá Tự Nhiên (50p)",
                    resultItem = ItemType.StoneFireplace,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 5) },
                    description = "Tỏa hơi ấm và ánh lửa (+45 Điểm Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_aquarium",
                    displayName = "Lắp Ráp Bể Cá Thủy Sinh (35p)",
                    resultItem = ItemType.FishAquarium,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.WoodPlank, 3),
                        new RecipeIngredient(ItemType.WaterBottle, 1)
                    },
                    description = "Cá bơi sinh động (+40 Điểm Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_piano",
                    displayName = "Chế Tác Đàn Piano Cổ Điển (60p)",
                    resultItem = ItemType.ClassicPiano,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = true,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 6) },
                    description = "Đỉnh cao âm nhạc (+50 Điểm Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_gramophone",
                    displayName = "Chế Tác Máy Hát Đĩa Than Lo-Fi (30p)",
                    resultItem = ItemType.VintageGramophone,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 3) },
                    description = "Phát nhạc Lo-Fi (+35 Điểm Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_pet_bed",
                    displayName = "May Nệm Thú Cưng Ấm Áp (25p)",
                    resultItem = ItemType.PetBed,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient>
                    {
                        new RecipeIngredient(ItemType.Wheat, 2),
                        new RecipeIngredient(ItemType.WoodPlank, 1)
                    },
                    description = "Chỗ ngủ cho Bé Shiba / Bé Miu (+25 Ấm Cúng)"
                },
                new RecipeDefinition
                {
                    recipeId = "craft_bicycle",
                    displayName = "Lắp Ráp Xe Đạp Dạo Phố (40p)",
                    resultItem = ItemType.Bicycle,
                    resultCount = 1,
                    recommendedProfession = ProfessionType.Carpenter,
                    requireExactProfession = false,
                    requiredStation = StationType.Workbench,
                    ingredients = new List<RecipeIngredient> { new RecipeIngredient(ItemType.WoodPlank, 3) },
                    description = "Bấm [B] ngoài trời để đạp xe dạo phố"
                }
            };
        }

        private static void InitRestaurantMenu()
        {
            restaurantMenu = new List<RestaurantDishDefinition>
            {
                new RestaurantDishDefinition
                {
                    dishId = "dish_garlic_bread",
                    displayName = "🥐 Bánh Mì Bơ Tỏi",
                    description = "+50 No, +10 Máu, +25 Sức  |  ⏱️ Ăn: 30 phút",
                    iconResourcePath = "CozyLifeArt/Items/item_bread",
                    priceCoins = 10,
                    restoreHealth = 10f,
                    restoreHunger = 50f,
                    restoreThirst = 0f,
                    restoreStamina = 25f,
                    takeawayBonusItem = ItemType.BakedBread
                },
                new RestaurantDishDefinition
                {
                    dishId = "dish_tomato_soup",
                    displayName = "🍲 Súp Cà Chua Kem Nấm",
                    description = "+65 No, +45 Nước, +20 Máu  |  ⏱️ Ăn: 30 phút",
                    iconResourcePath = "CozyLifeArt/Items/item_soup",
                    priceCoins = 16,
                    restoreHealth = 20f,
                    restoreHunger = 65f,
                    restoreThirst = 45f,
                    restoreStamina = 35f,
                    takeawayBonusItem = ItemType.None
                },
                new RestaurantDishDefinition
                {
                    dishId = "dish_peach_tea",
                    displayName = "🍹 Trà Đào Cam Sả",
                    description = "+75 Nước, +15 Máu, +40 Sức  |  ⏱️ Uống: 20 phút",
                    iconResourcePath = "CozyLifeArt/Items/item_tea",
                    priceCoins = 8,
                    restoreHealth = 15f,
                    restoreHunger = 10f,
                    restoreThirst = 75f,
                    restoreStamina = 40f,
                    takeawayBonusItem = ItemType.None
                },
                new RestaurantDishDefinition
                {
                    dishId = "dish_cozy_feast",
                    displayName = "🥩 Combo Đại Tiệc",
                    description = "Đầy 100% No & Nước, +45 Máu  |  ⏱️ Ăn: 40 phút",
                    iconResourcePath = "CozyLifeArt/Items/item_soup",
                    priceCoins = 28,
                    restoreHealth = 45f,
                    restoreHunger = 100f,
                    restoreThirst = 100f,
                    restoreStamina = 60f,
                    takeawayBonusItem = ItemType.TomatoSoup
                }
            };
        }

        public static ProfessionDefinition GetProfession(ProfessionType type)
        {
            EnsureInitialized();
            return professions[type];
        }

        public static IEnumerable<ProfessionDefinition> GetAllProfessions()
        {
            EnsureInitialized();
            return professions.Values;
        }

        public static ItemDefinition GetItem(ItemType type)
        {
            EnsureInitialized();
            return items.TryGetValue(type, out var def) ? def : null;
        }

        public static List<ItemDefinition> GetAllFurnitureItems()
        {
            EnsureInitialized();
            var list = new List<ItemDefinition>();
            foreach (var kv in items)
            {
                if (kv.Value.category == ItemCategory.PlaceableFurniture)
                    list.Add(kv.Value);
            }
            return list;
        }

        public static List<RecipeDefinition> GetAllRecipes()
        {
            EnsureInitialized();
            return recipes;
        }

        public static List<RestaurantDishDefinition> GetRestaurantMenu()
        {
            EnsureInitialized();
            return restaurantMenu;
        }

        public static Sprite LoadSprite(string resourcePath, Vector2? customPivot = null, float ppu = 100f)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return null;

            string cacheKey = $"{resourcePath}_{ppu}";
            if (spriteCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            Vector2 pivot = customPivot ?? new Vector2(0.5f, 0.5f);

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                texCache[resourcePath] = tex;
                float effectivePpu = ppu;
                if (resourcePath.Contains("CozyLifeArt/Objects/") && tex.width >= 120 && !resourcePath.Contains("court") && !resourcePath.Contains("pitch"))
                {
                    effectivePpu = ppu * 2f;
                }

                Sprite created = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, effectivePpu);
                created.name = resourcePath;
                spriteCache[cacheKey] = created;
                return created;
            }

            Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
            if (loadedSprite != null)
            {
                if (loadedSprite.texture != null && resourcePath.Contains("CozyLifeArt/Objects/") && loadedSprite.texture.width >= 120 && !resourcePath.Contains("court") && !resourcePath.Contains("pitch"))
                {
                    float effectivePpu = ppu * 2f;
                    Sprite created = Sprite.Create(loadedSprite.texture, new Rect(0, 0, loadedSprite.texture.width, loadedSprite.texture.height), pivot, effectivePpu);
                    created.name = resourcePath;
                    spriteCache[cacheKey] = created;
                    return created;
                }
                spriteCache[cacheKey] = loadedSprite;
                return loadedSprite;
            }

            Texture2D fallbackTex = CreateProceduralIsoDiamond(64, 32, new Color(0.45f, 0.72f, 0.38f));
            Sprite fallback = Sprite.Create(fallbackTex, new Rect(0, 0, fallbackTex.width, fallbackTex.height), pivot, ppu);
            spriteCache[cacheKey] = fallback;
            return fallback;
        }

        public static Texture2D LoadIconTexture(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return null;

            if (texCache.TryGetValue(resourcePath, out var cached) && cached != null)
                return cached;

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                texCache[resourcePath] = tex;
                return tex;
            }

            Sprite sp = Resources.Load<Sprite>(resourcePath);
            if (sp != null && sp.texture != null)
            {
                texCache[resourcePath] = sp.texture;
                return sp.texture;
            }

            return null;
        }

        private static Texture2D CreateProceduralIsoDiamond(int w, int h, Color color)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx) / cx;
                    float dy = Mathf.Abs(y - cy) / cy;
                    tex.SetPixel(x, y, (dx + dy <= 0.95f) ? color : clear);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
