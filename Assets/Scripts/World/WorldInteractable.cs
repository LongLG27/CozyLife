using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;

namespace CozyLife.World
{
    /// <summary>
    /// Lớp cơ sở cho mọi vật thể tương tác, với nhãn hiển thị ngắn gọn và thời gian thực hiện thực tế.
    /// </summary>
    public abstract class WorldInteractableBase : MonoBehaviour
    {
        public static readonly List<WorldInteractableBase> AllInteractables = new List<WorldInteractableBase>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticLists()
        {
            AllInteractables.Clear();
            CozyFurnitureAura.AllFurniture.Clear();
            BuildingDoorInteractable.AllDoors.Clear();
            CropPlotInteractable.AllCropPlots.Clear();
            TownFriendAgent.AllFriends.Clear();
        }

        [SerializeField] protected string interactTitle = "Vật thể";
        [SerializeField] protected StationType stationType = StationType.None;

        public string InteractTitle => interactTitle;
        public StationType Station => stationType;

        protected virtual void OnEnable()
        {
            if (!AllInteractables.Contains(this))
                AllInteractables.Add(this);
        }

        protected virtual void OnDisable()
        {
            AllInteractables.Remove(this);
        }

        public abstract string GetPromptText(ProfessionAndInventory player);
        public abstract string GetWorldOverheadLabel();
        public abstract void Interact(ProfessionAndInventory player, CharacterStats stats);
    }

    public class CozyFurnitureAura : MonoBehaviour
    {
        public static readonly List<CozyFurnitureAura> AllFurniture = new List<CozyFurnitureAura>();
        public string FurnitureTitle = "Ghế Gỗ";
        public ItemType FurnitureItemType = ItemType.CozyChair;
        public BuildingType RoomBuilding = BuildingType.None;
        public bool IsPlayerPlaced = false;
        public bool IsFlipped = false;
        public int CozyScoreBonus = 15;

        private void OnEnable()
        {
            if (!AllFurniture.Contains(this))
                AllFurniture.Add(this);
        }

        private void OnDisable()
        {
            AllFurniture.Remove(this);
        }

        public static int GetRoomCozyScore(BuildingType building)
        {
            if (building == BuildingType.None)
                return 0;

            int score = 0;
            for (int i = 0; i < AllFurniture.Count; i++)
            {
                var f = AllFurniture[i];
                if (f != null && f.RoomBuilding == building)
                {
                    score += Mathf.Max(10, f.CozyScoreBonus);
                }
            }
            return score;
        }

        public static CozyFurnitureAura SpawnPlacedFurniture(ItemType itemType, BuildingType building, Vector3 worldPos, bool isFlipped, bool isPlayerPlaced = true, Transform customParent = null)
        {
            ItemDefinition itemDef = CozyLifeDatabase.GetItem(itemType);
            if (itemDef == null)
                return null;

            GameObject fObj = new GameObject($"Placed_{itemDef.displayName}");
            if (customParent != null)
                fObj.transform.SetParent(customParent);
            fObj.transform.position = worldPos;

            SpriteRenderer sr = fObj.AddComponent<SpriteRenderer>();
            Vector2 pivot = (itemType == ItemType.PersianRug) ? new Vector2(0.5f, 0.5f) :
                            (itemType == ItemType.KitchenStove || itemType == ItemType.ClassicPiano || itemType == ItemType.StoneFireplace) ? new Vector2(0.5f, 0.20f) :
                            new Vector2(0.5f, 0.18f);
            float ppu = (itemType == ItemType.PersianRug) ? 85f :
                        (itemType == ItemType.KitchenStove || itemType == ItemType.ClassicPiano) ? 80f : 88f;
            sr.sprite = CozyLifeDatabase.LoadSprite(itemDef.placeableObjectSpritePath, pivot, ppu);
            sr.flipX = isFlipped;

            if (itemType == ItemType.PersianRug)
            {
                // Thảm trải sàn luôn nằm sát mặt sàn bên dưới nhân vật và bàn ghế
                sr.sortingOrder = -1450 + Mathf.RoundToInt(-worldPos.y * 5f);
            }
            else
            {
                IsometricSorting sorting = fObj.AddComponent<IsometricSorting>();
                sorting.Configure(true, 0);
            }

            if (itemType == ItemType.FloorLamp)
            {
                // Đèn ngủ tỏa ánh sáng vàng ấm áp trong phòng
                GameObject glowObj = new GameObject("LampWarmGlow");
                glowObj.transform.SetParent(fObj.transform);
                glowObj.transform.localPosition = new Vector3(0f, 0.36f, 0f);
                glowObj.transform.localScale = new Vector3(1.45f, 1.20f, 1f);
                SpriteRenderer glowSr = glowObj.AddComponent<SpriteRenderer>();
                glowSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_light_glow", new Vector2(0.5f, 0.5f), 75f);
                glowSr.sortingOrder = 1480;
                glowSr.color = new Color(1f, 0.88f, 0.52f, 0.72f);
            }

            if (itemType == ItemType.KitchenStove)
            {
                // Khu bếp nấu ăn đặt trong nhà có đầy đủ chức năng trạm nấu nướng CookingStove
                WorkstationInteractable ws = fObj.AddComponent<WorkstationInteractable>();
                ws.ConfigureStation(StationType.CookingStove, "Bếp Nấu Ăn Gia Đình");
            }

            if (itemType == ItemType.StoneFireplace || itemType == ItemType.ClassicPiano ||
                itemType == ItemType.VintageGramophone || itemType == ItemType.FishAquarium ||
                itemType == ItemType.LuxurySofa)
            {
                var lux = fObj.AddComponent<CozyLuxuryFurnitureInteractive>();
                lux.ConfigureLuxuryFurniture(itemType);
            }

            CircleCollider2D col = fObj.AddComponent<CircleCollider2D>();
            col.radius = (itemType == ItemType.KitchenStove) ? 0.75f : 0.65f;
            col.isTrigger = true;

            CozyFurnitureAura aura = fObj.AddComponent<CozyFurnitureAura>();
            aura.FurnitureTitle = itemDef.displayName;
            aura.FurnitureItemType = itemType;
            aura.RoomBuilding = building;
            aura.IsPlayerPlaced = isPlayerPlaced;
            aura.IsFlipped = isFlipped;
            aura.CozyScoreBonus = Mathf.Max(15, itemDef.cozyScoreBonus);
            return aura;
        }
    }

    /// <summary>
    /// Cửa ra vào Tòa nhà (Hỗ trợ cả Tòa nhà chức năng nghề nghiệp và Bất động sản Mua Bán bằng Xu).
    /// </summary>
    public class BuildingDoorInteractable : WorldInteractableBase
    {
        public static readonly List<BuildingDoorInteractable> AllDoors = new List<BuildingDoorInteractable>();

        private BuildingType targetBuilding = BuildingType.None;
        private bool isExitDoor = false;
        private Vector3 customReturnPosition;
        private int purchasePriceCoins = 0;
        private bool isUnlocked = true;
        private GameObject forSaleSignObject;

        public BuildingType TargetBuilding => targetBuilding;
        public bool IsExitDoor => isExitDoor;
        public bool IsUnlocked => isUnlocked;
        public int PurchasePriceCoins => purchasePriceCoins;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!AllDoors.Contains(this))
                AllDoors.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllDoors.Remove(this);
        }

        public void SetUnlockedFromSave(bool unlocked)
        {
            isUnlocked = unlocked;
            if (forSaleSignObject != null)
                forSaleSignObject.SetActive(!isUnlocked);
        }

        public static bool IsBuildingOwned(BuildingType building)
        {
            if (building == BuildingType.PlayerHome)
                return true;

            for (int i = 0; i < AllDoors.Count; i++)
            {
                var d = AllDoors[i];
                if (d != null && !d.isExitDoor && d.targetBuilding == building)
                    return d.isUnlocked;
            }
            return false;
        }

        public void ConfigureDoor(BuildingType building, bool isExit, Vector3 outdoorReturnPos, int priceCoins = 0, GameObject signObj = null)
        {
            targetBuilding = building;
            isExitDoor = isExit;
            customReturnPosition = outdoorReturnPos;
            purchasePriceCoins = priceCoins;
            forSaleSignObject = signObj;
            isUnlocked = (priceCoins <= 0) || isExit;
            interactTitle = CozyLifeDatabase.GetBuildingVietnameseName(building);
        }

        public override string GetWorldOverheadLabel()
        {
            if (isExitDoor)
                return "🚪 Ra Ngoài [E]";

            if (!isUnlocked && purchasePriceCoins > 0)
                return $"🏷️ {CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding)} ({purchasePriceCoins} Xu) [E]";

            if (purchasePriceCoins > 0 && isUnlocked)
                return $"🏠 {CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding)} (Của Bạn) [E]";

            return $"🚪 {CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding)} [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            if (isExitDoor)
                return "[E] Ra ngoài Thị Trấn";

            if (!isUnlocked && purchasePriceCoins > 0)
                return $"[E] Mua sở hữu {CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding)} — Giá: {purchasePriceCoins} Xu";

            return $"[E] Vào {CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding)}";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (CozyTownWorldBuilder.Instance == null)
                return;

            if (isExitDoor)
            {
                CozyAudioManager.PlaySfx(CozySfxType.Door);
                CozyTownWorldBuilder.Instance.ExitCurrentBuildingToTown();
                return;
            }

            if (!isUnlocked && purchasePriceCoins > 0)
            {
                if (player.SpendCoins(purchasePriceCoins))
                {
                    isUnlocked = true;
                    if (forSaleSignObject != null)
                        forSaleSignObject.SetActive(false);

                    CozyAudioManager.PlaySfx(CozySfxType.Coin);
                    string bName = CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding);
                    CozyLife.UI.CozyLifeHUD.PushToast($"🎉 Chúc mừng! Bạn đã mua sở hữu {bName} (-{purchasePriceCoins} Xu)!");
                    CozyTownWorldBuilder.Instance.EnterBuildingInterior(targetBuilding, customReturnPosition);
                    CozySaveManager.SaveGame(false);
                }
                else
                {
                    CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert);
                    string bName = CozyLifeDatabase.GetBuildingVietnameseName(targetBuilding);
                    CozyLife.UI.CozyLifeHUD.PushToast($"⚠️ Cần {purchasePriceCoins} Xu để mua {bName}! (Bạn đang có {player.Coins} Xu)");
                }
                return;
            }

            CozyAudioManager.PlaySfx(CozySfxType.Door);
            CozyTownWorldBuilder.Instance.EnterBuildingInterior(targetBuilding, customReturnPosition);
        }
    }

    /// <summary>
    /// Bàn ăn trong Nhà Hàng: Ngồi vào bàn và mở Thực Đơn Gọi Món.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class RestaurantTableInteractable : WorldInteractableBase
    {
        private int tableNumber = 1;
        private bool hasFoodOnTable = false;
        private string servedDishName = "";
        private float dishRemainMinutes = 0f;
        private SpriteRenderer sr;

        public int TableNumber => tableNumber;
        public Vector3 SeatWorldPosition => transform.position + new Vector3(-0.26f, -0.05f, 0f);

        public void ConfigureTable(int number)
        {
            tableNumber = number;
            interactTitle = $"Bàn #{number}";
            sr = GetComponent<SpriteRenderer>();
            UpdateTableVisuals();
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (hasFoodOnTable && dishRemainMinutes > 0f)
            {
                dishRemainMinutes -= Time.deltaTime;
                if (dishRemainMinutes <= 0f)
                {
                    hasFoodOnTable = false;
                    servedDishName = "";
                    UpdateTableVisuals();
                }
            }
        }

        public void ServeDishOnTable(RestaurantDishDefinition dish, float durationMinutes = 45f)
        {
            hasFoodOnTable = true;
            servedDishName = dish.displayName;
            dishRemainMinutes = durationMinutes;
            UpdateTableVisuals();
        }

        private void UpdateTableVisuals()
        {
            if (sr == null)
                sr = GetComponent<SpriteRenderer>();

            string spritePath = hasFoodOnTable
                ? "CozyLifeArt/Objects/obj_dining_table_food"
                : "CozyLifeArt/Objects/obj_dining_table";

            sr.sprite = CozyLifeDatabase.LoadSprite(spritePath, new Vector2(0.5f, 0.25f), 88f);
        }

        public override string GetWorldOverheadLabel()
        {
            if (hasFoodOnTable)
                return $"🍽️ Bàn #{tableNumber}";

            return $"🪑 Bàn #{tableNumber} [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return $"[E] Ngồi Bàn #{tableNumber} & Gọi Món";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SitAtRestaurantTable(this);
            }

            CozyLife.UI.CozyLifeHUD.Instance?.OpenRestaurantOrderModal(this);
        }
    }

    /// <summary>
    /// Giường ngủ (Ngủ nghỉ mất 60 phút trong game = 1 tiếng game).
    /// </summary>
    public class BedInteractable : WorldInteractableBase
    {
        private bool isClinicBed;

        public void ConfigureBed(string title, bool clinicBed)
        {
            interactTitle = title;
            isClinicBed = clinicBed;
        }

        public override string GetWorldOverheadLabel()
        {
            return isClinicBed ? "🛏️ Giường Y Tế [E]" : "🛏️ Giường Ngủ [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return isClinicBed
                ? "[E] Nằm điều trị (1 tiếng — +60 Máu, Đầy Sức)"
                : "[E] Ngủ nghỉ ngơi (1 tiếng — +40 Máu, Đầy Sức)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null) return;

            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            pc.StartSleepingOnBed(this, isClinicBed, 60f, () =>
            {
                float heal = isClinicBed ? 60f : 40f;
                stats.ModifyVitals(heal, 0f, 0f, 100f);
                if (stats.IsSick)
                {
                    stats.CureSickness("✨ Sau giấc ngủ ấm áp, bạn đã khỏi bệnh hoàn toàn!");
                }
                CozyAudioManager.PlaySfx(CozySfxType.Cure);
                CozySaveManager.SaveGame(false);
                CozyLife.UI.CozyLifeHUD.PushToast($"✅ Đã nghỉ ngơi 1 tiếng & Lưu game! (+{Mathf.RoundToInt(heal)} Máu, Đầy Sức)");
            });
        }
    }

    /// <summary>
    /// Ô đất trồng trọt (Gieo & Thu hoạch mất 10-20 phút game; cây chín sau 30-60 phút game).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CropPlotInteractable : WorldInteractableBase
    {
        public static readonly List<CropPlotInteractable> AllCropPlots = new List<CropPlotInteractable>();

        public enum CropStage
        {
            Empty = 0,
            Seeded = 1,
            Growing = 2,
            Ready = 3
        }

        private CropStage stage = CropStage.Empty;
        private float growthGameMinutes = 0f;
        private float requiredGameMinutes = 45f;
        private SpriteRenderer sr;

        public int PlotIndex { get; set; }
        public CropStage CurrentStage => stage;
        public float GrowthMinutes => growthGameMinutes;
        public float RequiredMinutes => requiredGameMinutes;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!AllCropPlots.Contains(this))
                AllCropPlots.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllCropPlots.Remove(this);
        }

        public void RestoreFromSave(CropStage savedStage, float savedGrowth, float savedReq)
        {
            stage = savedStage;
            growthGameMinutes = savedGrowth;
            requiredGameMinutes = Mathf.Max(10f, savedReq);
            UpdateVisuals();
        }

        private void Awake()
        {
            stationType = StationType.Farmland;
            interactTitle = "Ô Đất";
            sr = GetComponent<SpriteRenderer>();
            UpdateVisuals();
        }

        public void SetInitialStage(CropStage initialStage)
        {
            stage = initialStage;
            growthGameMinutes = (initialStage == CropStage.Growing) ? requiredGameMinutes * 0.5f : 0f;
            UpdateVisuals();
        }

        private void Update()
        {
            if (stage == CropStage.Seeded || stage == CropStage.Growing)
            {
                growthGameMinutes += Time.deltaTime;

                if (growthGameMinutes >= requiredGameMinutes)
                {
                    stage = CropStage.Ready;
                    UpdateVisuals();
                }
                else if (growthGameMinutes >= requiredGameMinutes * 0.45f && stage == CropStage.Seeded)
                {
                    stage = CropStage.Growing;
                    UpdateVisuals();
                }
            }
        }

        private void UpdateVisuals()
        {
            if (sr == null)
                sr = GetComponent<SpriteRenderer>();

            string path = stage switch
            {
                CropStage.Empty => "CozyLifeArt/Objects/obj_crop_empty",
                CropStage.Seeded => "CozyLifeArt/Objects/obj_crop_seed",
                CropStage.Growing => "CozyLifeArt/Objects/obj_crop_growing",
                CropStage.Ready => "CozyLifeArt/Objects/obj_crop_ready",
                _ => "CozyLifeArt/Objects/obj_crop_empty"
            };

            sr.sprite = CozyLifeDatabase.LoadSprite(path, new Vector2(0.5f, 0.35f), 90f);
        }

        public override string GetWorldOverheadLabel()
        {
            if (stage == CropStage.Empty)
                return "🌱 Đất Trống [E]";
            if (stage == CropStage.Ready)
                return "🌾 Chín Rồi [E]";

            int pct = Mathf.Clamp(Mathf.RoundToInt((growthGameMinutes / requiredGameMinutes) * 100f), 0, 99);
            return $"🌿 {pct}%";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            bool isFarmer = player.CurrentProfession == ProfessionType.Farmer;
            int actionMins = isFarmer ? 10 : 20;

            return stage switch
            {
                CropStage.Empty => $"[E] Gieo hạt ({actionMins}p)",
                CropStage.Seeded or CropStage.Growing => isFarmer
                    ? "[E] Bón phân thúc chín (15p)"
                    : "🌿 Cây đang lớn...",
                CropStage.Ready => $"[E] Thu hoạch ({actionMins}p)",
                _ => "[E] Tương tác"
            };
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            bool isFarmer = player.CurrentProfession == ProfessionType.Farmer;
            float workMinutes = isFarmer ? 10f : 20f;

            if (stage == CropStage.Empty)
            {
                if (player.GetItemCount(ItemType.CropSeed) <= 0)
                {
                    CozyLife.UI.CozyLifeHUD.PushToast("⚠️ Hết Hạt Giống! Hãy vào Siêu Thị mua thêm.");
                    return;
                }

                pc.StartTimedAction("🌱 Đang gieo hạt", workMinutes, () =>
                {
                    if (!player.RemoveItem(ItemType.CropSeed, 1))
                        return;

                    stage = CropStage.Seeded;
                    growthGameMinutes = 0f;
                    requiredGameMinutes = isFarmer ? 30f : 60f;
                    stats.TryConsumeStamina(6f);
                    UpdateVisuals();
                    CozyAudioManager.PlaySfx(CozySfxType.Harvest);
                    CozyLife.UI.CozyLifeHUD.PushToast($"🌱 Đã gieo hạt! (Chín sau {Mathf.RoundToInt(requiredGameMinutes)}p)");
                }, ActionVisualType.WateringPlants);
            }
            else if (stage == CropStage.Seeded || stage == CropStage.Growing)
            {
                if (isFarmer)
                {
                    pc.StartTimedAction("💧 Đang tưới nước & chăm bón", 15f, () =>
                    {
                        stage = CropStage.Ready;
                        stats.TryConsumeStamina(5f);
                        UpdateVisuals();
                        CozyAudioManager.PlaySfx(CozySfxType.Harvest);
                        CozyLife.UI.CozyLifeHUD.PushToast("✨ Cây đã chín nhờ tay nghề Nông Dân!");
                    }, ActionVisualType.WateringPlants);
                }
                else
                {
                    CozyLife.UI.CozyLifeHUD.PushToast("🌿 Chỉ Nông Dân mới thúc chín nhanh được.");
                }
            }
            else if (stage == CropStage.Ready)
            {
                pc.StartTimedAction("🌾 Đang thu hoạch", workMinutes, () =>
                {
                    int yieldCount = isFarmer ? 2 : 1;
                    player.AddItem(ItemType.Wheat, yieldCount);
                    player.AddItem(ItemType.Tomato, yieldCount);
                    player.AddItem(ItemType.CropSeed, yieldCount);

                    stage = CropStage.Empty;
                    growthGameMinutes = 0f;
                    stats.TryConsumeStamina(6f);
                    UpdateVisuals();
                    CozyAudioManager.PlaySfx(CozySfxType.Harvest);
                    CozyLife.UI.CozyLifeHUD.PushToast($"🌾 Thu hoạch +{yieldCount} Lúa Mì, +{yieldCount} Cà Chua, +{yieldCount} Hạt Giống!");
                    CozyDiarySystem.RecordCropHarvested();
                }, ActionVisualType.WateringPlants);
            }
        }
    }

    /// <summary>
    /// Các trạm làm việc chức năng: Giếng Nước, Bếp Lò, Tủ Thuốc, Bàn Cưa, Quầy Chợ.
    /// </summary>
    public class WorkstationInteractable : WorldInteractableBase
    {
        public void ConfigureStation(StationType type, string title)
        {
            stationType = type;
            interactTitle = title;
        }

        public override string GetWorldOverheadLabel()
        {
            return stationType switch
            {
                StationType.WaterWell => "💧 Giếng Nước [E]",
                StationType.CookingStove => "🍳 Bếp Lò [E]",
                StationType.MedicalStation => "⚕️ Tủ Thuốc [E]",
                StationType.Workbench => "🔨 Bàn Cưa [E]",
                StationType.MarketStall => "🏪 Siêu Thị Cozy Mart [E]",
                _ => interactTitle
            };
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return stationType switch
            {
                StationType.WaterWell => "[E] Uống & Lấy nước (15p)",
                StationType.CookingStove => "[E] Mở Bếp Nấu Ăn",
                StationType.MedicalStation => "[E] Sơ cứu (30p) & Pha thuốc",
                StationType.Workbench => "[E] Xẻ Gỗ Thông (30p) / Đóng đồ [C]",
                StationType.MarketStall => "[E] Mở Cửa Hàng Siêu Thị Cozy Mart",
                _ => $"[E] {interactTitle}"
            };
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            switch (stationType)
            {
                case StationType.WaterWell:
                    pc.StartTimedAction("💧 Đang múc nước & uống", 15f, () =>
                    {
                        stats.ModifyVitals(0f, 0f, 40f, 15f);
                        if (player.GetItemCount(ItemType.WaterBottle) < 5)
                            player.AddItem(ItemType.WaterBottle, 1);

                        CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
                        CozyLife.UI.CozyLifeHUD.PushToast("💧 Đã uống nước (+40 Nước) & lấy +1 Chai Nước!");
                    }, ActionVisualType.DrinkingWater);
                    break;

                case StationType.CookingStove:
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                    CozyLife.UI.CozyLifeHUD.Instance?.OpenCraftingPanel(true);
                    break;

                case StationType.MedicalStation:
                    pc.StartTimedAction("⚕️ Đang sơ cứu & điều trị", 30f, () =>
                    {
                        float healAmount = (player.CurrentProfession == ProfessionType.Doctor) ? 45f : 25f;
                        stats.ModifyVitals(healAmount, 0f, 0f, 20f);
                        if (stats.IsSick)
                        {
                            stats.CureSickness("⚕️ Tủ thuốc y tế đã giúp bạn chữa dứt điểm bệnh!");
                        }
                        CozyAudioManager.PlaySfx(CozySfxType.Cure);
                        CozyLife.UI.CozyLifeHUD.PushToast($"⚕️ Đã sơ cứu (+{Mathf.RoundToInt(healAmount)} Máu)! Bấm [C] nếu muốn pha thuốc.");
                    }, ActionVisualType.DoctorHealing);
                    break;

                case StationType.Workbench:
                    pc.StartTimedAction("🪚 Đang xẻ Gỗ Thông", 30f, () =>
                    {
                        int woodGain = (player.CurrentProfession == ProfessionType.Carpenter) ? 2 : 1;
                        player.AddItem(ItemType.WoodPlank, woodGain);
                        stats.TryConsumeStamina(8f);
                        CozyAudioManager.PlaySfx(CozySfxType.Craft);
                        CozyLife.UI.CozyLifeHUD.PushToast($"🪵 Đã xẻ +{woodGain} Gỗ Thông! Bấm [C] để đóng nội thất.");
                    }, ActionVisualType.CarpentrySaw);
                    break;

                case StationType.MarketStall:
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                    CozyLife.UI.CozyLifeHUD.Instance?.OpenMarketModal();
                    break;
            }
        }
    }

    /// <summary>
    /// Bạn bè Cư Dân trong thị trấn và trong các tòa nhà.
    /// </summary>
    public class TownFriendAgent : WorldInteractableBase
    {
        public static readonly List<TownFriendAgent> AllFriends = new List<TownFriendAgent>();

        public string FriendName { get; private set; }
        public ProfessionType FriendProfession { get; private set; }
        public BuildingType AssignedBuilding { get; private set; } = BuildingType.None;
        public float FriendHealth { get; private set; } = 92f;
        public float FriendHunger { get; private set; } = 74f;
        public int FriendshipPoints { get; private set; } = 20;
        public string CurrentChatBubble { get; private set; } = "";

        private Vector3 homePosition;
        private Vector3 targetPosition;
        private float roamRadius = 1.0f;
        private float moveTimer;
        private float chatTimer;
        private float helpCooldown;

        private SpriteRenderer sr;
        private CharacterAppearanceData npcAppearance;
        private static readonly int[] WalkSequence = { 1, 2, 3, 0 };
        private float walkCycleTimer = 0f;
        private int walkStepIndex = 0;
        private int currentWalkFrame = 0;

        public void InitializeFriend(string friendName, ProfessionType profession, Vector3 homePos, string greeting, BuildingType building = BuildingType.None, float maxRoam = 1.0f)
        {
            FriendName = friendName;
            FriendProfession = profession;
            AssignedBuilding = building;
            homePosition = homePos;
            targetPosition = homePos;
            roamRadius = maxRoam;
            interactTitle = friendName;
            CurrentChatBubble = greeting;
            chatTimer = 7f;
            moveTimer = UnityEngine.Random.Range(1.2f, 2.8f);

            npcAppearance = CreatePresetAppearanceForFriend(friendName, profession);
            sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(npcAppearance, 0);
            }
        }

        private static CharacterAppearanceData CreatePresetAppearanceForFriend(string name, ProfessionType profession)
        {
            var d = new CharacterAppearanceData
            {
                characterName = name,
                starterProfession = profession
            };

            switch (profession)
            {
                case ProfessionType.Farmer: // Nông Dân An: Mũ rơm, áo xanh lá, quần yếm nâu
                    d.gender = GenderType.Male;
                    d.skinToneIndex = 1;
                    d.hairStyleIndex = 0;
                    d.hairColorIndex = 1;
                    d.expressionIndex = 0;
                    d.shirtIndex = 4;
                    d.shirtColorIndex = 0;
                    d.pantsIndex = 0;
                    d.pantsColorIndex = 1;
                    d.shoesIndex = 2;
                    d.accessoryIndex = 3;
                    break;

                case ProfessionType.Chef: // Đầu Bếp Minh: Áo sơ mi trắng bếp trưởng, quần âu đen
                    d.gender = GenderType.Male;
                    d.skinToneIndex = 0;
                    d.hairStyleIndex = 4;
                    d.hairColorIndex = 0;
                    d.expressionIndex = 2;
                    d.shirtIndex = 1;
                    d.shirtColorIndex = 5;
                    d.pantsIndex = 5;
                    d.pantsColorIndex = 3;
                    d.shoesIndex = 1;
                    d.accessoryIndex = 0;
                    break;

                case ProfessionType.Doctor: // Bác Sĩ Lan: Nữ, kính cận tròn, áo blouse trắng, tóc đuôi ngựa
                    d.gender = GenderType.Female;
                    d.skinToneIndex = 0;
                    d.hairStyleIndex = 2;
                    d.hairColorIndex = 1;
                    d.expressionIndex = 3;
                    d.shirtIndex = 1;
                    d.shirtColorIndex = 5;
                    d.pantsIndex = 1;
                    d.pantsColorIndex = 0;
                    d.shoesIndex = 3;
                    d.accessoryIndex = 1;
                    break;

                case ProfessionType.Carpenter: // Thợ Mộc Hùng: Mũ len Beanie, áo khoác nâu cam, quần Cargo
                    d.gender = GenderType.Male;
                    d.skinToneIndex = 2;
                    d.hairStyleIndex = 2;
                    d.hairColorIndex = 0;
                    d.expressionIndex = 1;
                    d.shirtIndex = 5;
                    d.shirtColorIndex = 2;
                    d.pantsIndex = 4;
                    d.pantsColorIndex = 1;
                    d.shoesIndex = 2;
                    d.accessoryIndex = 4;
                    break;
            }
            return d;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!AllFriends.Contains(this))
                AllFriends.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllFriends.Remove(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (helpCooldown > 0f)
                helpCooldown -= dt;

            FriendHunger = Mathf.Clamp(FriendHunger - 0.10f * dt, 25f, 100f);

            moveTimer -= dt;
            if (moveTimer <= 0f)
            {
                moveTimer = UnityEngine.Random.Range(3.2f, 5.8f);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * roamRadius;
                targetPosition = homePosition + new Vector3(offset.x, offset.y * 0.55f, 0f);
            }

            Vector3 prevPos = transform.position;
            transform.position = Vector3.MoveTowards(prevPos, targetPosition, 1.10f * dt);
            Vector3 delta = transform.position - prevPos;
            bool isWalking = delta.sqrMagnitude > 0.00001f;

            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null && npcAppearance != null)
            {
                if (isWalking)
                {
                    if (Mathf.Abs(delta.x) > 0.0002f)
                        sr.flipX = delta.x < 0f;

                    walkCycleTimer += dt * 8.2f;
                    if (walkCycleTimer >= 1f)
                    {
                        walkCycleTimer -= 1f;
                        walkStepIndex = (walkStepIndex + 1) % WalkSequence.Length;
                    }

                    int targetFrame = WalkSequence[walkStepIndex];
                    if (targetFrame != currentWalkFrame)
                    {
                        currentWalkFrame = targetFrame;
                        sr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(npcAppearance, targetFrame);
                    }

                    float tilt = (targetFrame == 1) ? 3.0f : (targetFrame == 3 ? -3.0f : 0f);
                    transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(0f, 0f, tilt), 14f * dt);
                }
                else
                {
                    walkCycleTimer = 0f;
                    walkStepIndex = 0;
                    transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.identity, 12f * dt);
                    if (currentWalkFrame != 0)
                    {
                        currentWalkFrame = 0;
                        sr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(npcAppearance, 0);
                    }
                }
            }

            if (chatTimer > 0f)
            {
                chatTimer -= dt;
                if (chatTimer <= 0f)
                    CurrentChatBubble = "";
            }
        }

        public void Say(string text, float duration = 4.5f)
        {
            CurrentChatBubble = text;
            chatTimer = duration;
        }

        public void RestoreFromSave(int friendship, float hp, float hunger)
        {
            FriendshipPoints = Mathf.Max(0, friendship);
            FriendHealth = Mathf.Clamp(hp, 20f, 100f);
            FriendHunger = Mathf.Clamp(hunger, 20f, 100f);
        }

        public override string GetWorldOverheadLabel()
        {
            var prof = CozyLifeDatabase.GetProfession(FriendProfession);
            return $"{prof.titleBadge.Split(' ')[0]} {FriendName}";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return $"[E] Nhờ {FriendName} giúp (15p)  |  [F] Tặng đồ";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            if (helpCooldown > 0.1f)
            {
                Say("Đợi mình chút nhé!", 3f);
                return;
            }

            pc.StartTimedAction($"🤝 Đang trò chuyện với {FriendName}", 15f, () =>
            {
                helpCooldown = 10f;
                FriendshipPoints += 5;

                switch (FriendProfession)
                {
                    case ProfessionType.Chef:
                        player.AddItem(ItemType.TomatoSoup, 1);
                        Say("Tặng cậu 1 tô Súp Cà Chua nè! 🍲", 5f);
                        CozyLife.UI.CozyLifeHUD.PushToast($"🍳 {FriendName} tặng +1 Súp Cà Chua!");
                        break;

                    case ProfessionType.Doctor:
                        stats.ModifyVitals(45f, 0f, 20f, 30f);
                        if (stats.IsSick)
                        {
                            stats.CureSickness("⚕️ Bác Sĩ Lan đã tiêm thuốc & chữa khỏi bệnh cho bạn!");
                        }
                        player.AddItem(ItemType.HerbalTea, 1);
                        Say("Đã sơ cứu và tặng cậu 1 Trà Thảo Mộc! ⚕️", 5f);
                        CozyLife.UI.CozyLifeHUD.PushToast($"⚕️ {FriendName} hồi +45 Máu & tặng +1 Trà!");
                        break;

                    case ProfessionType.Carpenter:
                        player.AddItem(ItemType.CozyChair, 1);
                        Say("Tặng cậu chiếc Ghế Gỗ mới đóng nè! 🪑", 5f);
                        CozyLife.UI.CozyLifeHUD.PushToast($"🔨 {FriendName} tặng +1 Ghế Gỗ!");
                        break;

                    case ProfessionType.Farmer:
                        player.AddItem(ItemType.CropSeed, 3);
                        player.AddItem(ItemType.Tomato, 2);
                        Say("Cầm lấy Hạt Giống và Cà Chua tươi nhé! 🌱", 5f);
                        CozyLife.UI.CozyLifeHUD.PushToast($"🌱 {FriendName} tặng +3 Hạt Giống, +2 Cà Chua!");
                        break;
                }
            });
        }

        public void ReceiveGiftFromPlayer(ProfessionAndInventory player)
        {
            var slot = player.GetSelectedSlot();
            if (slot == null || slot.count <= 0)
            {
                CozyLife.UI.CozyLifeHUD.PushToast("⚠️ Chưa có đồ trên tay để tặng!");
                return;
            }

            var itemDef = CozyLifeDatabase.GetItem(slot.itemType);
            player.RemoveItem(slot.itemType, 1);

            FriendHunger = Mathf.Clamp(FriendHunger + Mathf.Max(20f, itemDef.restoreHunger), 0f, 100f);
            FriendHealth = Mathf.Clamp(FriendHealth + Mathf.Max(10f, itemDef.restoreHealth), 0f, 100f);
            FriendshipPoints += 10;

            int rewardCoins = Mathf.Max(12, itemDef.basePrice);
            player.AddCoins(rewardCoins);
            CozyAudioManager.PlaySfx(CozySfxType.Coin);

            Say($"Cảm ơn nhé! Gửi cậu {rewardCoins} Xu! ❤️", 4.5f);
            CozyLife.UI.CozyLifeHUD.PushToast($"🎁 Đã tặng {itemDef.displayName} (+{rewardCoins} Xu)!");
        }
    }
}
