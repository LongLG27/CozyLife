using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    /// <summary>
    /// Hệ thống Thú Cưng Đồng Hành Dễ Thương (Shiba Inu Bé Vàng & Mèo Tam Thể Bé Miu).
    /// Chạy theo người chơi trung thành, vẫy đuôi, ngồi chờ khi chủ dừng lại, ngủ dưới chân giường,
    /// cho vuốt ve hồi thể lực [E] và ăn cá/thịt để đào báu vật tặng người chơi!
    /// </summary>
    public class CozyPetCompanion : MonoBehaviour
    {
        public static CozyPetCompanion Instance { get; private set; }

        public enum PetType
        {
            ShibaInu,
            CalicoCat
        }

        [SerializeField] private PetType petKind = PetType.ShibaInu;
        [SerializeField] private string petName = "Bé Vàng";
        [SerializeField] private float followSpeed = 4.2f;
        [SerializeField] private float stopDistance = 0.85f;

        private Transform playerTarget;
        private SpriteRenderer sr;
        private int friendshipLevel = 1;
        private int feedingsCount = 0;
        private float barkCooldown = 0f;
        private float heartTimer = 0f;
        private string speechBubble = "";
        private float speechTimer = 0f;

        private Vector3 prevPosition;
        private float bobTimer = 0f;

        public string PetName => petName;
        public int FriendshipLevel => friendshipLevel;
        public bool IsAdopted => true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void InitializePet(PetType type, string name, Vector3 spawnPos)
        {
            petKind = type;
            petName = name;
            transform.position = spawnPos;
            prevPosition = spawnPos;

            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

            sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_pet_bed", new Vector2(0.5f, 0.35f), 110f);
            sr.sortingOrder = 5;

            var sort = gameObject.AddComponent<IsometricSorting>();
            sort.Configure(true, 0);

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;
            col.isTrigger = true;

            gameObject.AddComponent<PetInteractableAgent>().Configure(this);
        }

        private void Update()
        {
            if (playerTarget == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTarget = pc.transform;
            }

            float dt = Time.deltaTime;
            if (barkCooldown > 0f) barkCooldown -= dt;
            if (heartTimer > 0f) heartTimer -= dt;
            if (speechTimer > 0f)
            {
                speechTimer -= dt;
                if (speechTimer <= 0f) speechBubble = "";
            }

            if (playerTarget == null) return;

            // Kiểm tra trạng thái người chơi đang ngủ trên giường
            var pcController = playerTarget.GetComponent<PlayerController>();
            bool isPlayerSleeping = pcController != null && pcController.IsPerformingAction;

            Vector3 targetPos = isPlayerSleeping
                ? playerTarget.position + new Vector3(0.35f, -0.30f, 0f) // Nằm dưới chân giường
                : playerTarget.position;

            float dist = Vector2.Distance(transform.position, targetPos);
            bool isMoving = dist > stopDistance;

            if (isMoving)
            {
                Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, followSpeed * dt);
                Vector3 delta = nextPos - transform.position;

                if (Mathf.Abs(delta.x) > 0.01f && sr != null)
                {
                    sr.flipX = delta.x < 0f;
                }

                transform.position = nextPos;

                // Hiệu ứng nhún nhảy khi chạy lon ton
                bobTimer += dt * 10f;
                float bobY = Mathf.Sin(bobTimer) * 0.035f;
                if (sr != null)
                {
                    sr.transform.localPosition = new Vector3(0f, bobY, 0f);
                }
            }
            else
            {
                bobTimer = 0f;
                if (sr != null)
                {
                    sr.transform.localPosition = Vector3.zero;
                }
            }
        }

        public void PetInteract(ProfessionAndInventory player, CharacterStats stats)
        {
            // Vuốt ve thú cưng
            heartTimer = 2.5f;
            speechBubble = (petKind == PetType.ShibaInu) ? "Gâu gâu! ❤️ (Vui sướng vẫy đuôi)" : "Meo meo~ ❤️ (Dụi đầu vào tay bạn)";
            speechTimer = 3.5f;

            stats.ModifyVitals(0f, 0f, 0f, 15f);
            CozyAudioManager.PlaySfx(CozySfxType.PetBark);
            CozyLifeHUD.PushToast($"🐾 Bạn đã âu yếm {petName}! (+15 Thể lực, Tinh thần thư thái)");
        }

        public void FeedPet(ItemType foodType, ProfessionAndInventory player)
        {
            player.RemoveItem(foodType, 1);
            feedingsCount++;

            CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
            CozyAudioManager.PlaySfx(CozySfxType.Cheer);

            speechBubble = "Ngoàm ngoàm! Ngon quá đi! ✨";
            speechTimer = 3.5f;
            heartTimer = 3.0f;

            if (feedingsCount % 3 == 0)
            {
                friendshipLevel++;
                int rewardCoins = UnityEngine.Random.Range(20, 45);
                player.AddCoins(rewardCoins);
                CozyAudioManager.PlaySfx(CozySfxType.Coin);
                CozyLifeHUD.PushToast($"🐶 {petName} đã đào được kho báu tặng bạn: +{rewardCoins} Xu & Tăng Cấp Thân Thiết ({friendshipLevel})! 🎉");
                CozyDiarySystem.RecordPetFriendship(friendshipLevel);
            }
            else
            {
                CozyLifeHUD.PushToast($"🍖 {petName} đã ăn rất ngon lành! (Độ thân thiết: {feedingsCount}/3)");
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(speechBubble) || Camera.main == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(0f, 0.45f, 0f));
            if (screenPos.z < 0) return;

            float y = Screen.height - screenPos.y;
            float x = screenPos.x;

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;

            GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
            GUI.Box(new Rect(x - 90, y - 24, 180, 24), speechBubble, style);
            GUI.color = Color.white;
        }
    }

    public class PetInteractableAgent : WorldInteractableBase
    {
        private CozyPetCompanion pet;

        public void Configure(CozyPetCompanion companion)
        {
            pet = companion;
            interactTitle = companion.PetName;
        }

        public override string GetWorldOverheadLabel()
        {
            return $"🐾 {pet.PetName} [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            bool hasFood = player.GetItemCount(ItemType.RawMeat) > 0 ||
                           player.GetItemCount(ItemType.RawFish) > 0 ||
                           player.GetItemCount(ItemType.FishGoldenCarp) > 0 ||
                           player.GetItemCount(ItemType.FishSalmon) > 0;

            return hasFood
                ? $"[E] Vuốt ve • [F] Cho {pet.PetName} ăn cá/thịt"
                : $"[E] Vuốt ve âu yếm {pet.PetName} (+15 Thể lực)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            pet.PetInteract(player, stats);
        }

        public void Feed(ProfessionAndInventory player)
        {
            ItemType[] foods = { ItemType.FishGoldenCarp, ItemType.FishSalmon, ItemType.RawFish, ItemType.RawMeat };
            foreach (var f in foods)
            {
                if (player.GetItemCount(f) > 0)
                {
                    pet.FeedPet(f, player);
                    return;
                }
            }
            CozyLifeHUD.PushToast($"⚠️ Không có thịt hoặc cá trong túi để cho {pet.PetName} ăn!");
        }
    }

    /// <summary>
    /// Điểm Nhận Nuôi Thú Cưng tại Quảng Trường Công Viên.
    /// </summary>
    public class PetAdoptionSpotInteractable : WorldInteractableBase
    {
        private bool hasAdopted = false;

        private void Awake()
        {
            interactTitle = "Góc Nhận Nuôi Thú Cưng";
            stationType = StationType.None;
        }

        public override string GetWorldOverheadLabel()
        {
            return hasAdopted ? "🏡 Mái Ấm Thú Cưng" : "🐶 Nhận Nuôi Thú Cưng [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return hasAdopted
                ? "🏡 Bạn đã nhận nuôi Bé Vàng rồi!"
                : "[E] Nhận nuôi chú chó Shiba Inu 'Bé Vàng' làm bạn đồng hành (Miễn phí)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (hasAdopted)
            {
                CozyLifeHUD.PushToast("🐶 Bé Vàng đang đi theo bạn rất vui vẻ!");
                return;
            }

            hasAdopted = true;
            GameObject petObj = new GameObject("Pet_ShibaInu");
            var companion = petObj.AddComponent<CozyPetCompanion>();
            companion.InitializePet(CozyPetCompanion.PetType.ShibaInu, "Bé Vàng", transform.position + new Vector3(0.4f, -0.2f, 0f));

            CozyAudioManager.PlaySfx(CozySfxType.Cheer);
            CozyAudioManager.PlaySfx(CozySfxType.PetBark);
            CozyLifeHUD.PushToast("🎉 CHÚC MỪNG! Bạn đã nhận nuôi chú chó Shiba Inu 'Bé Vàng'! Bé sẽ luôn bên cạnh bạn.");
        }
    }
}
