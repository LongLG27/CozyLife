using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    /// <summary>
    /// Hệ thống Câu Cá Hồ Sinh Thái Công Viên (Eco-Pond Interactive Fishing System).
    /// Người chơi cầm Cần Câu, thả phao bập bùng trên mặt hồ, chờ cá cắn câu và giật cần thành công!
    /// Có thể câu được: Cá Chép Vàng, Cá Hồi Suối, và Cá Nguyệt Quang Thần Thoại (ban đêm).
    /// </summary>
    public class FishingPierInteractable : WorldInteractableBase
    {
        public static FishingPierInteractable Instance { get; private set; }

        private enum FishingState
        {
            Idle,
            Casting,
            WaitingForBite,
            FishBiting,
            ReelingSuccess,
            ReelingMissed
        }

        private FishingState state = FishingState.Idle;
        private float stateTimer = 0f;
        private float biteWindowTimer = 0f;
        private const float BiteWindowDuration = 1.75f;

        private GameObject bobberObj;
        private SpriteRenderer bobberSr;
        private Vector3 waterTargetPos;
        private PlayerController activePlayer;
        private ProfessionAndInventory activePlayerInv;
        private CharacterStats activePlayerStats;

        private float bobberBobTimer = 0f;
        private bool isBitingAlertActive = false;

        private void Awake()
        {
            Instance = this;
            interactTitle = "Cầu Câu Cá Công Viên";
            stationType = StationType.None;
        }

        public void Configure(Vector3 pierPos, Vector3 waterPos)
        {
            transform.position = pierPos;
            waterTargetPos = waterPos;
            EnsureBobberObject();
        }

        private void EnsureBobberObject()
        {
            if (bobberObj == null)
            {
                bobberObj = new GameObject("FishingBobber");
                bobberObj.transform.SetParent(transform);
                bobberSr = bobberObj.AddComponent<SpriteRenderer>();
                // Red & White round fishing float
                bobberSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/prop_basketball", new Vector2(0.5f, 0.5f), 120f);
                bobberSr.color = new Color(1f, 0.35f, 0.35f, 1f);
                bobberObj.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                bobberSr.sortingOrder = 50;
                bobberObj.SetActive(false);
            }
        }

        public override string GetWorldOverheadLabel()
        {
            if (state == FishingState.FishBiting)
                return "❗ CÁ CẮN CÂU! [E / SPACE]!";
            if (state == FishingState.WaitingForBite || state == FishingState.Casting)
                return "🎣 Đang câu cá...";

            return "🎣 Cầu Câu Cá [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            if (state != FishingState.Idle)
                return (state == FishingState.FishBiting) ? "[E / Space] GIẬT CẦN NGAY!" : "🎣 Đang chờ cá cắn câu...";

            bool hasRod = player.GetItemCount(ItemType.FishingRod) > 0;
            return hasRod
                ? "[E] Thả cần câu cá tại hồ (Mất 15p)"
                : "[E] Cần có [Cần Câu Cá] để câu (Đóng tại Bàn Cưa hoặc mua ở Siêu Thị)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (state != FishingState.Idle)
            {
                if (state == FishingState.FishBiting)
                {
                    PerformCatchFish();
                }
                return;
            }

            if (player.GetItemCount(ItemType.FishingRod) <= 0)
            {
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                CozyLifeHUD.PushToast("⚠️ Bạn chưa có Cần Câu Cá! Hãy ghé Bàn Cưa xưởng mộc (2 Gỗ Thông) hoặc Siêu Thị để trang bị.");
                return;
            }

            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            activePlayer = pc;
            activePlayerInv = player;
            activePlayerStats = stats;

            StartFishingSequence();
        }

        private void StartFishingSequence()
        {
            state = FishingState.Casting;
            stateTimer = 1.0f;

            CozyAudioManager.PlaySfx(CozySfxType.NetSwish);
            CozyLifeHUD.PushToast("🎣 Đã vung cần câu ra mặt hồ sinh thái... Chờ cá đớp mồi!");

            EnsureBobberObject();
            if (bobberObj != null)
            {
                bobberObj.transform.position = waterTargetPos;
                bobberObj.SetActive(true);
            }
        }

        private void Update()
        {
            if (state == FishingState.Idle) return;

            float dt = Time.deltaTime;

            if (bobberObj != null && bobberObj.activeSelf)
            {
                bobberBobTimer += dt * 3.5f;
                float bobY = Mathf.Sin(bobberBobTimer) * 0.035f;
                bobberObj.transform.position = waterTargetPos + new Vector3(0f, bobY, 0f);
            }

            // Xử lý phím giật cần nhanh (E, Space hoặc Chuột trái)
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool actionPressed = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                                 (mouse != null && mouse.leftButton.wasPressedThisFrame);

            switch (state)
            {
                case FishingState.Casting:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        state = FishingState.WaitingForBite;
                        stateTimer = UnityEngine.Random.Range(2.8f, 5.5f);
                        CozyAudioManager.PlaySfx(CozySfxType.Splash);
                    }
                    break;

                case FishingState.WaitingForBite:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        // Cá bắt đầu cắn câu!
                        state = FishingState.FishBiting;
                        biteWindowTimer = BiteWindowDuration;
                        isBitingAlertActive = true;
                        CozyAudioManager.PlaySfx(CozySfxType.ReelIn);
                        CozyLifeHUD.PushToast("❗ CÁ CẮN CÂU RỒI! BẤM [E] HOẶC [SPACE] NGAY!");

                        if (bobberSr != null)
                        {
                            bobberSr.color = Color.yellow;
                            bobberObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                        }
                    }
                    break;

                case FishingState.FishBiting:
                    biteWindowTimer -= dt;
                    if (actionPressed)
                    {
                        PerformCatchFish();
                    }
                    else if (biteWindowTimer <= 0f)
                    {
                        // Quá thời gian, cá chạy mất
                        state = FishingState.ReelingMissed;
                        stateTimer = 1.2f;
                        isBitingAlertActive = false;
                        CozyAudioManager.PlaySfx(CozySfxType.Splash);
                        CozyLifeHUD.PushToast("💦 Tiếc quá! Cá đã quẫy đuôi bơi mất rồi. Hãy kiên nhẫn thử lại nhé!");
                        if (bobberSr != null)
                        {
                            bobberSr.color = new Color(1f, 0.35f, 0.35f, 1f);
                            bobberObj.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                        }
                    }
                    break;

                case FishingState.ReelingSuccess:
                case FishingState.ReelingMissed:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        ResetFishing();
                    }
                    break;
            }
        }

        private void PerformCatchFish()
        {
            state = FishingState.ReelingSuccess;
            stateTimer = 1.4f;
            isBitingAlertActive = false;

            CozyAudioManager.PlaySfx(CozySfxType.Cheer);
            CozyAudioManager.PlaySfx(CozySfxType.Coin);

            bool isNight = CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.IsNightTime;

            ItemType caughtItem = ItemType.FishGoldenCarp;
            string fishName = "Cá Chép Vàng";
            float weight = UnityEngine.Random.Range(1.8f, 3.8f);

            float roll = UnityEngine.Random.value;
            if (isNight && roll < 0.35f)
            {
                caughtItem = ItemType.FishMoonlight;
                fishName = "🌟 CÁ NGUYỆT QUANG THẦN THOẠI";
                weight = UnityEngine.Random.Range(3.5f, 6.2f);
            }
            else if (roll < 0.50f)
            {
                caughtItem = ItemType.FishSalmon;
                fishName = "Cá Hồi Suối";
                weight = UnityEngine.Random.Range(2.2f, 4.5f);
            }
            else
            {
                caughtItem = ItemType.FishGoldenCarp;
                fishName = "Cá Chép Vàng";
                weight = UnityEngine.Random.Range(1.5f, 3.4f);
            }

            if (activePlayerInv != null)
            {
                activePlayerInv.AddItem(caughtItem, 1);
            }

            // Ghi nhận thành tựu vào Sổ Tay Thị Trấn
            CozyDiarySystem.RecordFishCaught(caughtItem);

            if (activePlayerStats != null)
            {
                activePlayerStats.TryConsumeStamina(8f);
            }

            CozyLifeHUD.PushToast($"🎉 TUYỆT VỜI! Bạn đã giật được [{fishName}] nặng {weight:0.0}kg!");

            if (bobberSr != null)
            {
                bobberSr.color = Color.white;
            }
        }

        private void ResetFishing()
        {
            state = FishingState.Idle;
            isBitingAlertActive = false;
            if (bobberObj != null)
                bobberObj.SetActive(false);
        }

        private void OnGUI()
        {
            if (state == FishingState.FishBiting && isBitingAlertActive)
            {
                float w = 320f;
                float h = 60f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height * 0.35f;

                Rect r = new Rect(x, y, w, h);
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);

                GUI.color = Color.yellow;
                GUI.Box(r, "");

                float pulse = 0.85f + Mathf.PingPong(Time.time * 6f, 0.3f);
                GUI.color = new Color(1f, pulse, 0.2f, 1f);

                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(r, "⚡ CÁ CẮN CÂU! [E / SPACE]!", style);
                GUI.color = Color.white;
            }
        }
    }
}
