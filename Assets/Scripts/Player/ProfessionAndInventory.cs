using System;
using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.World;

namespace CozyLife.Player
{
    /// <summary>
    /// Quản lý Nghề nghiệp, Túi đồ 8 ô, Chế tạo/Nấu ăn và Đặt nội thất (có thời gian thực hiện thực tế).
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    public class ProfessionAndInventory : MonoBehaviour
    {
        [Header("Player Identity")]
        [SerializeField] private string playerName = "Bạn";
        [SerializeField] private ProfessionType currentProfession = ProfessionType.Farmer;
        [SerializeField] private int coins = 120;

        private readonly List<InventorySlot> inventory = new List<InventorySlot>();
        private int selectedSlotIndex = 0;
        private CharacterStats stats;
        private PlayerController playerController;
        private SpriteRenderer spriteRenderer;

        public CharacterAppearanceData Appearance { get; private set; }

        public string PlayerName
        {
            get => playerName;
            set => playerName = value;
        }

        public ProfessionType CurrentProfession => currentProfession;
        public ProfessionDefinition CurrentProfessionDef => CozyLifeDatabase.GetProfession(currentProfession);
        public int Coins => coins;
        public int SelectedSlotIndex
        {
            get => selectedSlotIndex;
            set => selectedSlotIndex = Mathf.Clamp(value, 0, 7);
        }

        public IReadOnlyList<InventorySlot> Inventory => inventory;

        public event Action<string> OnActionFeedback;
        public event Action OnInventoryOrProfessionChanged;

        private void Awake()
        {
            CozyLifeDatabase.EnsureInitialized();
            stats = GetComponent<CharacterStats>();
            playerController = GetComponent<PlayerController>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (Appearance == null)
            {
                Appearance = CharacterAppearanceData.LoadFromPrefs();
            }
        }

        private void Start()
        {
            if (inventory.Count == 0)
            {
                AddItem(ItemType.CropSeed, 5);
                AddItem(ItemType.WaterBottle, 2);
                AddItem(ItemType.BakedBread, 2);
                AddItem(ItemType.WoodPlank, 2);
            }

            ApplyProfessionVisuals();
        }

        public void RestoreStateFromSave(string savedName, ProfessionType savedProf, int savedCoins, List<InventorySlot> savedInv)
        {
            if (!string.IsNullOrWhiteSpace(savedName))
                playerName = savedName;

            currentProfession = savedProf;
            coins = Mathf.Max(0, savedCoins);

            if (savedInv != null)
            {
                inventory.Clear();
                foreach (var s in savedInv)
                {
                    if (s != null && s.itemType != ItemType.None && s.count > 0)
                    {
                        inventory.Add(new InventorySlot(s.itemType, s.count));
                    }
                }
            }

            ApplyProfessionVisuals();
            OnInventoryOrProfessionChanged?.Invoke();
        }

        public void ChangeProfession(ProfessionType newProfession, bool grantStarterBonus = true)
        {
            currentProfession = newProfession;
            ApplyProfessionVisuals();

            if (grantStarterBonus)
            {
                switch (newProfession)
                {
                    case ProfessionType.Farmer:
                        AddItem(ItemType.CropSeed, 4);
                        break;
                    case ProfessionType.Chef:
                        AddItem(ItemType.Wheat, 2);
                        AddItem(ItemType.Tomato, 2);
                        break;
                    case ProfessionType.Doctor:
                        AddItem(ItemType.HerbalTea, 1);
                        AddItem(ItemType.Medkit, 1);
                        break;
                    case ProfessionType.Carpenter:
                        AddItem(ItemType.WoodPlank, 4);
                        break;
                }
            }

            var def = CurrentProfessionDef;
            OnActionFeedback?.Invoke($"✨ Đã đổi sang nghề {def.titleBadge}!");
            OnInventoryOrProfessionChanged?.Invoke();
        }

        public void SetAppearance(CharacterAppearanceData newAppearance)
        {
            Appearance = newAppearance;
            if (newAppearance != null)
            {
                playerName = newAppearance.characterName;
                currentProfession = newAppearance.starterProfession;
                newAppearance.SaveToPrefs();
            }
            ApplyProfessionVisuals();
            OnInventoryOrProfessionChanged?.Invoke();
        }

        private void ApplyProfessionVisuals()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            Sprite charSprite = null;
            if (Appearance != null)
            {
                charSprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(Appearance);
            }

            if (charSprite == null)
            {
                var def = CurrentProfessionDef;
                charSprite = CozyLifeDatabase.LoadSprite(def.spriteResourcePath, new Vector2(0.5f, 0.15f), 85f);
            }

            if (spriteRenderer != null && charSprite != null)
            {
                spriteRenderer.sprite = charSprite;
                spriteRenderer.color = Color.white;
            }
        }

        public int GetItemCount(ItemType type)
        {
            int total = 0;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].itemType == type)
                    total += inventory[i].count;
            }
            return total;
        }

        public void AddCoins(int amount)
        {
            coins = Mathf.Max(0, coins + amount);
            OnInventoryOrProfessionChanged?.Invoke();
        }

        public bool SpendCoins(int amount)
        {
            if (coins < amount)
                return false;
            coins -= amount;
            OnInventoryOrProfessionChanged?.Invoke();
            return true;
        }

        public void AddItem(ItemType type, int amount = 1)
        {
            if (type == ItemType.None || amount <= 0)
                return;

            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i].itemType == type)
                {
                    inventory[i].count += amount;
                    OnInventoryOrProfessionChanged?.Invoke();
                    return;
                }
            }

            inventory.Add(new InventorySlot(type, amount));
            OnInventoryOrProfessionChanged?.Invoke();
        }

        public bool RemoveItem(ItemType type, int amount = 1)
        {
            if (GetItemCount(type) < amount)
                return false;

            int remaining = amount;
            for (int i = inventory.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (inventory[i].itemType == type)
                {
                    int take = Mathf.Min(inventory[i].count, remaining);
                    inventory[i].count -= take;
                    remaining -= take;
                    if (inventory[i].count <= 0)
                        inventory.RemoveAt(i);
                }
            }

            if (selectedSlotIndex >= inventory.Count && inventory.Count > 0)
                selectedSlotIndex = inventory.Count - 1;

            OnInventoryOrProfessionChanged?.Invoke();
            return true;
        }

        public InventorySlot GetSelectedSlot()
        {
            if (selectedSlotIndex >= 0 && selectedSlotIndex < inventory.Count)
                return inventory[selectedSlotIndex];
            return null;
        }

        /// <summary>
        /// Sử dụng vật phẩm trên thanh Hotbar (cần thời gian thực tế: Ăn uống 20–30 phút game, Đặt ghế 15 phút game).
        /// </summary>
        public void UseItemAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= inventory.Count)
                return;

            if (playerController != null && playerController.IsPerformingAction)
            {
                OnActionFeedback?.Invoke("⏳ Đang bận thực hiện hành động!");
                return;
            }

            selectedSlotIndex = slotIndex;
            InventorySlot slot = inventory[slotIndex];
            ItemDefinition itemDef = CozyLifeDatabase.GetItem(slot.itemType);
            if (itemDef == null)
                return;

            if (itemDef.category == ItemCategory.Seed)
            {
                OnActionFeedback?.Invoke("🌱 Đứng gần Ô Đất và nhấn [E] để gieo hạt!");
                return;
            }
            if (itemDef.category == ItemCategory.Material)
            {
                OnActionFeedback?.Invoke("🪵 Dùng Gỗ Thông trong bảng Chế Tạo [C] tại Xưởng Mộc!");
                return;
            }

            // 1. Nếu là Nội thất: Chỉ đặt trang trí bên trong các ngôi nhà thuộc sở hữu của bạn (mất 15 phút trong game)
            if (itemDef.category == ItemCategory.PlaceableFurniture)
            {
                if (CozyTownWorldBuilder.Instance != null && !CozyLifeDatabase.IsResidentialHome(CozyTownWorldBuilder.Instance.CurrentBuilding))
                {
                    OnActionFeedback?.Invoke("🏠 Hãy vào bên trong ngôi nhà của bạn để đặt nội thất trang trí!");
                    return;
                }

                if (playerController != null)
                {
                    playerController.StartTimedAction($"🪑 Đang đặt {itemDef.displayName}", 15f, () =>
                    {
                        if (RemoveItem(itemDef.type, 1))
                        {
                            PlaceFurnitureInWorld(itemDef);
                        }
                    });
                }
                return;
            }

            // 2. Ăn uống / Sơ cứu: Mất 20–30 phút trong game
            float durationMinutes = (itemDef.type == ItemType.WaterBottle || itemDef.type == ItemType.HerbalTea) ? 20f : 30f;
            string verb = (itemDef.restoreThirst > itemDef.restoreHunger) ? "💧 Đang uống" :
                          (itemDef.category == ItemCategory.Medical) ? "⚕️ Đang dùng" : "🍽️ Đang ăn";

            ActionVisualType visual = itemDef.type switch
            {
                ItemType.BakedBread or ItemType.GrilledSteak or ItemType.GrilledFish or ItemType.HeartyBurger or ItemType.RawMeat or ItemType.RawFish => ActionVisualType.EatingBread,
                ItemType.TomatoSoup => ActionVisualType.EatingSoup,
                ItemType.WaterBottle or ItemType.HerbalTea => ActionVisualType.DrinkingWater,
                ItemType.Medkit => ActionVisualType.DoctorHealing,
                _ => ActionVisualType.None
            };

            if (playerController != null)
            {
                playerController.StartTimedAction($"{verb} {itemDef.displayName}", durationMinutes, () =>
                {
                    if (!RemoveItem(itemDef.type, 1))
                        return;

                    float hpGain = itemDef.restoreHealth;
                    float hungerGain = itemDef.restoreHunger;
                    float thirstGain = itemDef.restoreThirst;
                    float staminaGain = itemDef.restoreStamina;

                    if (currentProfession == ProfessionType.Doctor && itemDef.category == ItemCategory.Medical)
                    {
                        hpGain *= 1.5f;
                        staminaGain *= 1.3f;
                    }

                    stats.ModifyVitals(hpGain, hungerGain, thirstGain, staminaGain);

                    if (stats.IsSick && (itemDef.type == ItemType.HerbalTea || itemDef.type == ItemType.Medkit))
                    {
                        stats.CureSickness($"✨ {itemDef.displayName} đã giúp bạn chữa khỏi bệnh hoàn toàn!");
                    }
                    else
                    {
                        CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
                    }

                    OnActionFeedback?.Invoke($"✅ Đã dùng {itemDef.displayName}!");
                }, visual);
            }
        }

        private void PlaceFurnitureInWorld(ItemDefinition itemDef)
        {
            Vector3 spawnPos = transform.position + new Vector3(0.55f, -0.25f, 0f);
            BuildingType curBuilding = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentBuilding
                : BuildingType.PlayerHome;

            CozyFurnitureAura.SpawnPlacedFurniture(itemDef.type, curBuilding, spawnPos, false, true);
            CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture);
            CozySaveManager.SaveGame(false);

            int roomScore = CozyFurnitureAura.GetRoomCozyScore(curBuilding);
            OnActionFeedback?.Invoke($"🛋️ Đã đặt {itemDef.displayName} (+{itemDef.cozyScoreBonus} Ấm Cúng — Tổng phòng: {roomScore}đ)!");
        }

        /// <summary>
        /// Chế tạo hoặc Nấu ăn cần thời gian thực hiện (30 - 45 phút trong game).
        /// </summary>
        public bool TryCraftRecipe(RecipeDefinition recipe, StationType nearbyStation, out string message)
        {
            if (recipe == null)
            {
                message = "Công thức lỗi.";
                return false;
            }

            if (playerController != null && playerController.IsPerformingAction)
            {
                message = "⏳ Đang bận thực hiện hành động!";
                OnActionFeedback?.Invoke(message);
                return false;
            }

            if (recipe.requireExactProfession && currentProfession != recipe.recommendedProfession)
            {
                var reqProf = CozyLifeDatabase.GetProfession(recipe.recommendedProfession);
                message = $"⚠️ Cần nghề {reqProf.titleBadge}!";
                OnActionFeedback?.Invoke(message);
                return false;
            }

            if (recipe.requiredStation == StationType.None || nearbyStation != recipe.requiredStation)
            {
                message = $"⚠️ Phải tới {GetStationVietnameseName(recipe.requiredStation)} để làm!";
                OnActionFeedback?.Invoke(message);
                return false;
            }

            foreach (var ing in recipe.ingredients)
            {
                int have = GetItemCount(ing.item);
                if (have < ing.count)
                {
                    var ingDef = CozyLifeDatabase.GetItem(ing.item);
                    message = $"⚠️ Thiếu {ing.count - have} {ingDef.displayName}!";
                    OnActionFeedback?.Invoke(message);
                    return false;
                }
            }

            // Trừ nguyên liệu trước khi bắt đầu làm
            foreach (var ing in recipe.ingredients)
            {
                RemoveItem(ing.item, ing.count);
            }

            bool isSpecialist = (currentProfession == recipe.recommendedProfession);
            float craftMinutes = recipe.resultItem == ItemType.CropSeed ? 15f : (isSpecialist ? 30f : 45f);
            var outDef = CozyLifeDatabase.GetItem(recipe.resultItem);

            CozyLife.UI.CozyLifeHUD.Instance?.CloseAllModals();

            if (playerController != null)
            {
                string actionVerb = recipe.requiredStation switch
                {
                    StationType.CookingStove => "🍳 Đang nấu",
                    StationType.Workbench => "🔨 Đang cưa & đóng",
                    StationType.MedicalStation => "⚕️ Đang pha chế",
                    _ => "🔨 Đang làm"
                };

                ActionVisualType visual = recipe.requiredStation switch
                {
                    StationType.CookingStove => ActionVisualType.Cooking,
                    StationType.Workbench => ActionVisualType.CarpentrySaw,
                    _ => ActionVisualType.None
                };

                playerController.StartTimedAction($"{actionVerb} {outDef.displayName}", craftMinutes, () =>
                {
                    int finalOutput = recipe.resultCount + (isSpecialist ? 1 : 0);
                    AddItem(recipe.resultItem, finalOutput);
                    CozyAudioManager.PlaySfx(CozySfxType.Craft);
                    CozySaveManager.SaveGame(false);
                    OnActionFeedback?.Invoke($"✨ Hoàn thành +{finalOutput} {outDef.displayName}!");
                }, visual);
            }

            message = $"⏳ Bắt đầu làm {outDef.displayName} ({Mathf.RoundToInt(craftMinutes)}p)...";
            return true;
        }

        public static string GetStationVietnameseName(StationType station)
        {
            return station switch
            {
                StationType.Farmland => "Ô Đất (Nông Trại)",
                StationType.CookingStove => "Bếp Lò (Nhà Hàng)",
                StationType.WaterWell => "Giếng Nước (Quảng Trường)",
                StationType.MedicalStation => "Tủ Thuốc (Phòng Khám)",
                StationType.Workbench => "Bàn Cưa (Xưởng Mộc)",
                StationType.MarketStall => "Quầy Hàng (Siêu Thị)",
                _ => "Chưa đứng gần trạm nghề"
            };
        }
    }
}
