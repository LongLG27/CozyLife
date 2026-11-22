using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    public enum SportsMinigameType
    {
        None = 0,
        Basketball,
        SoccerPenalty
    }

    /// <summary>
    /// Hệ thống Quản Lý & Điều Khiển 2 Mini Game Thể Thao:
    /// 1. Vua Ném Rổ (Basketball 3-Point Shootout)
    /// 2. Vua Sút Phạt Đền (Soccer Penalty Shootout)
    /// Tích hợp đầy đủ quỹ đạo bóng Parabol vật lý, thanh canh lực / góc sút, âm thanh còi, lưới, reo hò,
    /// điểm số, bảng tổng kết và phần thưởng Xu + Thể lực.
    /// </summary>
    public class CozySportsMinigameManager : MonoBehaviour
    {
        public static CozySportsMinigameManager Instance { get; private set; }

        private SportsMinigameType activeGame = SportsMinigameType.None;
        public static bool IsGameActive => Instance != null && Instance.activeGame != SportsMinigameType.None;
        public static SportsMinigameType ActiveGameType => Instance != null ? Instance.activeGame : SportsMinigameType.None;

        // References
        private Camera mainCam;
        private PlayerController playerController;
        private ProfessionAndInventory playerProfInv;
        private CharacterStats playerStats;

        // State Machine
        private enum MinigameState
        {
            Ready = 0,
            Aiming,           // Canh góc sút (Soccer)
            PowerMeter,       // Canh lực ném / sút
            BallFlying,       // Bóng đang bay theo quỹ đạo parabol
            RoundCelebration, // Ăn mừng vòng đấu / Hiện chữ kết quả
            GameOverModal     // Bảng tổng kết trao giải
        }

        private MinigameState state = MinigameState.Ready;

        // Vòng đấu & Điểm số
        private int currentRound = 1;
        private const int TotalRounds = 5;
        private int totalScore = 0;              // Dành cho bóng rổ (tối đa 15 điểm)
        private int goalsScored = 0;             // Dành cho bóng đá (tối đa 5 bàn)
        private readonly bool[] roundResults = new bool[TotalRounds]; // true: thành công, false: trượt

        // Timers & Gauges
        private float gaugeTimer = 0f;
        private float lockedAim = 0f;            // -1.0 .. +1.0 (trái sang phải)
        private float lockedPower = 0.5f;        // 0.0 .. 1.0 (lực ném/sút)
        private float celebrationTimer = 0f;

        // Vật lý bóng bay (Parabolic Arc)
        private GameObject ballObj;
        private SpriteRenderer ballSr;
        private Vector3 ballStartPos;
        private Vector3 ballTargetPos;
        private float ballFlyTimer = 0f;
        private float ballFlyDuration = 0.75f;
        private float ballArcHeight = 0.85f;
        private bool isCurrentShotGoal = false;
        private int currentShotPoints = 0;
        private string outcomeText = "";
        private Color outcomeColor = Color.white;

        // Thủ Môn Bóng Đá (Chibi Goalkeeper on pitch)
        private GameObject goalkeeperObj;
        private SpriteRenderer goalkeeperSr;
        private Vector3 gkDefaultPos;
        private Vector3 gkDiveTarget;
        private float gkDiveTimer = 0f;
        private float gkDiveTiltAngle = 0f;
        private CharacterAppearanceData gkAppearance;

        // Tọa độ sân đấu
        private Vector3 courtCenter;
        private Vector3 targetHoopPos;
        private Vector3 targetGoalPos;
        private Vector3 soccerGoalCenterOnGround;
        private Vector3 shootingSpotPos;

        // GUI Styles
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle btnStyle;
        private GUIStyle popupStyle;
        private bool stylesReady = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            mainCam = Camera.main;
            EnsureBallObject();
        }

        private void EnsureBallObject()
        {
            if (ballObj == null)
            {
                ballObj = new GameObject("SportsMinigame_Ball");
                ballObj.transform.SetParent(transform);
                ballSr = ballObj.AddComponent<SpriteRenderer>();
                ballSr.sortingOrder = 1600;
            }
            ballObj.SetActive(false);

            if (goalkeeperObj == null)
            {
                goalkeeperObj = new GameObject("SportsMinigame_Goalkeeper");
                goalkeeperObj.transform.SetParent(transform);
                goalkeeperSr = goalkeeperObj.AddComponent<SpriteRenderer>();
                goalkeeperSr.sortingOrder = 1500;
            }
            if (gkAppearance == null)
            {
                gkAppearance = new CharacterAppearanceData
                {
                    characterName = "Thủ Môn Lâm Tây",
                    gender = GenderType.Male,
                    skinToneIndex = 1,
                    hairStyleIndex = 2, // Tóc Vuốt Dựng Cá Tính
                    hairColorIndex = 0, // Đen
                    expressionIndex = 1, // Tập trung
                    shirtIndex = 2, // Áo thể thao
                    shirtColorIndex = 2, // Áo vàng nổi bật
                    pantsIndex = 1, // Quần thể thao đen
                    pantsColorIndex = 0,
                    shoesIndex = 1 // Giày thể thao
                };
            }
            goalkeeperObj.SetActive(false);
        }

        private void FindPlayerComponents()
        {
            if (playerController == null)
                playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerProfInv = playerController.GetComponent<ProfessionAndInventory>();
                playerStats = playerController.GetComponent<CharacterStats>();
            }
        }

        // =====================================================================
        // PUBLIC API KHỞI ĐỘNG MINI GAME
        // =====================================================================

        public static void StartBasketballGame(Vector3 hoopPos, Vector3 courtCenterPos)
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("CozySportsMinigameManager");
                go.AddComponent<CozySportsMinigameManager>();
            }
            Instance.LaunchBasketballGame(hoopPos, courtCenterPos);
        }

        public static void StartSoccerPenaltyGame(Vector3 goalPos, Vector3 penaltySpot)
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("CozySportsMinigameManager");
                go.AddComponent<CozySportsMinigameManager>();
            }
            Instance.LaunchSoccerPenaltyGame(goalPos, penaltySpot);
        }

        private void LaunchBasketballGame(Vector3 hoopPos, Vector3 centerPos)
        {
            FindPlayerComponents();
            if (playerController != null && playerController.IsPerformingAction)
                return;

            activeGame = SportsMinigameType.Basketball;
            targetHoopPos = hoopPos + new Vector3(0f, 0.40f, 0f);
            courtCenter = centerPos;
            // Vị trí đứng ném 3 điểm / ném phạt
            shootingSpotPos = targetHoopPos + new Vector3(-1.85f, -0.45f, 0f);

            if (playerController != null)
            {
                playerController.transform.position = shootingSpotPos;
                var sr = playerController.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = false;
            }

            currentRound = 1;
            totalScore = 0;
            Array.Clear(roundResults, 0, roundResults.Length);

            ballSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/prop_basketball", new Vector2(0.5f, 0.5f), 100f);
            ballSr.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            goalkeeperObj.SetActive(false);
            CozyAudioManager.PlaySfx(CozySfxType.Whistle);
            CozyLifeHUD.PushToast("🏀 Bắt đầu Vua Ném Rổ! Canh vạch xanh để ném chuẩn xác.");

            PrepareRound();
        }

        private void LaunchSoccerPenaltyGame(Vector3 goalPos, Vector3 penaltySpot)
        {
            FindPlayerComponents();
            if (playerController != null && playerController.IsPerformingAction)
                return;

            activeGame = SportsMinigameType.SoccerPenalty;
            soccerGoalCenterOnGround = goalPos;
            targetGoalPos = goalPos;
            shootingSpotPos = penaltySpot;
            courtCenter = penaltySpot;

            if (playerController != null)
            {
                playerController.transform.position = shootingSpotPos + new Vector3(-0.45f, -0.15f, 0f);
                var sr = playerController.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = false;
            }

            currentRound = 1;
            goalsScored = 0;
            Array.Clear(roundResults, 0, roundResults.Length);

            ballSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/prop_soccer_ball", new Vector2(0.5f, 0.5f), 100f);
            ballSr.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            // Vector vào sâu trong lưới từ vạch cầu môn
            Vector3 intoNetDir = new Vector3(0.24f, 0.12f, 0f).normalized;
            // Thủ môn đứng ngay trước vạch vôi cầu môn
            gkDefaultPos = soccerGoalCenterOnGround - intoNetDir * 0.05f;
            goalkeeperObj.transform.position = gkDefaultPos;
            goalkeeperObj.transform.rotation = Quaternion.identity;

            if (gkAppearance == null)
            {
                gkAppearance = new CharacterAppearanceData
                {
                    characterName = "Thủ Môn Lâm Tây",
                    gender = GenderType.Male,
                    skinToneIndex = 1,
                    hairStyleIndex = 2,
                    hairColorIndex = 0,
                    expressionIndex = 1,
                    shirtIndex = 2,
                    shirtColorIndex = 2,
                    pantsIndex = 1,
                    pantsColorIndex = 0,
                    shoesIndex = 1
                };
            }

            Sprite gkSprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(gkAppearance, 0);
            goalkeeperSr.sprite = gkSprite;
            goalkeeperSr.color = Color.white;
            goalkeeperSr.flipX = false;
            goalkeeperSr.sortingOrder = 1500;
            goalkeeperObj.SetActive(true);

            CozyAudioManager.PlaySfx(CozySfxType.Whistle);
            CozyLifeHUD.PushToast("⚽ Bắt đầu Sút Penalty! Bấm [Space] hoặc Click chọn góc và lực sút.");

            PrepareRound();
        }

        private void PrepareRound()
        {
            gaugeTimer = 0f;
            lockedAim = 0f;
            lockedPower = 0.5f;
            outcomeText = "";

            ballObj.SetActive(true);
            if (activeGame == SportsMinigameType.Basketball)
            {
                ballObj.transform.position = shootingSpotPos + new Vector3(0.18f, 0.35f, 0f);
                state = MinigameState.PowerMeter;
            }
            else // Soccer
            {
                ballObj.transform.position = shootingSpotPos;
                goalkeeperObj.transform.position = gkDefaultPos;
                goalkeeperObj.transform.rotation = Quaternion.identity;
                if (gkAppearance != null)
                {
                    goalkeeperSr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(gkAppearance, 0);
                }
                goalkeeperObj.SetActive(true);
                state = MinigameState.Aiming;
            }
        }

        // =====================================================================
        // VÒNG LẶP CẬP NHẬT HOẠT ẢNH & VẬT LÝ BÓNG
        // =====================================================================

        private void Update()
        {
            if (activeGame == SportsMinigameType.None)
                return;

            float dt = Time.deltaTime;
            gaugeTimer += dt;

            // Xử lý Input bằng phím Space hoặc Chuột trái
            bool spacePressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
            bool escPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

            if (escPressed && state != MinigameState.GameOverModal)
            {
                QuitGame();
                return;
            }

            switch (state)
            {
                case MinigameState.Aiming:
                    // Mũi tên góc sút dao động từ trái qua phải (-1 .. +1)
                    lockedAim = Mathf.Sin(gaugeTimer * 3.2f);
                    if (spacePressed)
                    {
                        CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                        gaugeTimer = 0f;
                        state = MinigameState.PowerMeter;
                    }
                    break;

                case MinigameState.PowerMeter:
                    if (activeGame == SportsMinigameType.Basketball)
                    {
                        // Thanh lực bóng rổ chạy qua lại 0..1 (vùng ngọt ở giữa 0.45..0.55)
                        lockedPower = Mathf.PingPong(gaugeTimer * 1.8f, 1f);
                    }
                    else
                    {
                        // Thanh lực bóng đá dâng lên hạ xuống 0..1
                        lockedPower = Mathf.PingPong(gaugeTimer * 2.4f, 1f);
                    }

                    if (spacePressed)
                    {
                        TriggerShot();
                    }
                    break;

                case MinigameState.BallFlying:
                    ballFlyTimer += dt;
                    float tau = Mathf.Clamp01(ballFlyTimer / ballFlyDuration);

                    // Quỹ đạo Parabol 3D mềm mại
                    Vector3 linearPos = Vector3.Lerp(ballStartPos, ballTargetPos, tau);
                    float arcY = 4f * ballArcHeight * tau * (1f - tau);
                    ballObj.transform.position = new Vector3(linearPos.x, linearPos.y + arcY, linearPos.z);

                    // Xoay quả bóng trong không trung
                    ballObj.transform.Rotate(0f, 0f, -dt * 720f);

                    // Thủ môn bay người cản phá (Soccer)
                    if (activeGame == SportsMinigameType.SoccerPenalty && goalkeeperObj != null && goalkeeperObj.activeSelf)
                    {
                        gkDiveTimer += dt;
                        float gkT = Mathf.Clamp01(gkDiveTimer / (ballFlyDuration * 0.85f));
                        goalkeeperObj.transform.position = Vector3.Lerp(gkDefaultPos, gkDiveTarget, gkT);
                        goalkeeperObj.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, gkDiveTiltAngle, gkT));
                    }

                    if (tau >= 1.0f)
                    {
                        OnBallReachedTarget();
                    }
                    break;

                case MinigameState.RoundCelebration:
                    celebrationTimer -= dt;
                    if (celebrationTimer <= 0f)
                    {
                        if (currentRound < TotalRounds)
                        {
                            currentRound++;
                            PrepareRound();
                        }
                        else
                        {
                            ShowGameOverModal();
                        }
                    }
                    break;
            }
        }

        private void TriggerShot()
        {
            state = MinigameState.BallFlying;
            ballFlyTimer = 0f;
            ballStartPos = ballObj.transform.position;

            if (activeGame == SportsMinigameType.Basketball)
            {
                ballFlyDuration = 0.85f;
                ballArcHeight = 1.10f;

                // Độ chính xác dựa vào lockedPower (vùng chuẩn xác 0.46 .. 0.54)
                float dev = Mathf.Abs(lockedPower - 0.50f);
                if (dev <= 0.05f)
                {
                    // Perfect Swish
                    isCurrentShotGoal = true;
                    currentShotPoints = 3;
                    ballTargetPos = targetHoopPos;
                    outcomeText = "🔥 HOÀN HẢO! SWISH! (+3 ĐIỂM)";
                    outcomeColor = new Color(0.25f, 1f, 0.45f);
                }
                else if (dev <= 0.14f)
                {
                    // Good Shot
                    isCurrentShotGoal = true;
                    currentShotPoints = 2;
                    ballTargetPos = targetHoopPos + new Vector3(0.04f, 0.02f, 0f);
                    outcomeText = "✨ VÀO RỔ! (+2 ĐIỂM)";
                    outcomeColor = new Color(1f, 0.88f, 0.25f);
                }
                else
                {
                    // Miss
                    isCurrentShotGoal = false;
                    currentShotPoints = 0;
                    ballTargetPos = targetHoopPos + new Vector3(dev > 0 ? 0.32f : -0.32f, -0.15f, 0f);
                    outcomeText = "❌ BẬT VÀNH! Trượt rồi!";
                    outcomeColor = new Color(1f, 0.35f, 0.35f);
                }

                CozyAudioManager.PlaySfx(CozySfxType.BallBounce, 0.85f);
            }
            else // Soccer Penalty
            {
                ballFlyDuration = 0.65f;
                gkDiveTimer = 0f;

                // Trục vạch cầu môn (chạy từ Tây Bắc sang Đông Nam)
                Vector3 goalLineAxis = new Vector3(0.524f, -0.262f, 0f).normalized;
                // Vector vào sâu trong lưới (slope +0.50)
                Vector3 intoNetDir = new Vector3(0.24f, 0.12f, 0f).normalized;

                // lockedAim: -1 (góc trái), 0 (giữa), +1 (góc phải)
                Vector3 aimOffset = goalLineAxis * (lockedAim * 0.23f);

                // Thủ môn AI phán đoán góc bay (-0.85 .. +0.85)
                float gkChosenAim = UnityEngine.Random.Range(-0.85f, 0.85f);
                gkDiveTarget = gkDefaultPos + goalLineAxis * (gkChosenAim * 0.24f) + new Vector3(0f, 0.08f, 0f);
                gkDiveTiltAngle = (gkChosenAim > 0.05f) ? -28f : (gkChosenAim < -0.05f ? 28f : 0f);

                if (gkAppearance != null && goalkeeperSr != null)
                {
                    goalkeeperSr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(gkAppearance, 2);
                }

                if (lockedPower > 0.85f)
                {
                    // Sút quá mạnh vọt xà ngang (xà ngang cao 0.38f, bóng đạt 0.52f .. 0.68f)
                    isCurrentShotGoal = false;
                    currentShotPoints = 0;
                    ballArcHeight = 0.35f;
                    float overHeight = Mathf.Lerp(0.52f, 0.68f, (lockedPower - 0.85f) / 0.15f);
                    ballTargetPos = soccerGoalCenterOnGround + aimOffset + intoNetDir * 0.42f + new Vector3(0f, overHeight, 0f);
                    outcomeText = "⚠️ VỌT XÀ NGANG! Sút quá bổng bay ra ngoài!";
                    outcomeColor = new Color(1f, 0.55f, 0.25f);
                }
                else if (lockedPower < 0.28f)
                {
                    // Sút quá nhẹ lăn sệt mặt đất, thủ môn dễ dàng ôm gọn
                    isCurrentShotGoal = false;
                    currentShotPoints = 0;
                    ballArcHeight = 0.08f;
                    ballTargetPos = gkDefaultPos + aimOffset * 0.35f;
                    outcomeText = "🧤 SÚT QUÁ NHẸ! Thủ môn bắt gọn bóng.";
                    outcomeColor = new Color(1f, 0.45f, 0.45f);
                }
                else if (Mathf.Abs(lockedAim - gkChosenAim) < 0.30f)
                {
                    // Thủ môn phán đoán đúng hướng và bay người cản phá!
                    isCurrentShotGoal = false;
                    currentShotPoints = 0;
                    ballArcHeight = 0.22f;
                    ballTargetPos = gkDiveTarget + new Vector3(-0.16f, -0.06f, 0f);
                    outcomeText = "🧤 CẢN PHÁ XUẤT THẦN! Thủ môn bay người đẩy bóng ra!";
                    outcomeColor = new Color(1f, 0.45f, 0.45f);
                }
                else
                {
                    // VÀOOOOOO! Bóng bay chuẩn xác găm vào lưới (dưới xà ngang 0.38f)
                    isCurrentShotGoal = true;
                    currentShotPoints = 1;
                    ballArcHeight = 0.22f;
                    float shotHeight = Mathf.Lerp(0.06f, 0.28f, (lockedPower - 0.28f) / 0.57f);
                    ballTargetPos = soccerGoalCenterOnGround + aimOffset + intoNetDir * 0.16f + new Vector3(0f, shotHeight, 0f);
                    outcomeText = "⚽ VÀOOOOOO! CÚ SÚT HIỂM HÓC GĂM VÀO LƯỚI!";
                    outcomeColor = new Color(0.25f, 1f, 0.45f);
                }

                CozyAudioManager.PlaySfx(CozySfxType.BallBounce, 0.95f);
            }
        }

        private void OnBallReachedTarget()
        {
            state = MinigameState.RoundCelebration;
            celebrationTimer = 1.4f;

            roundResults[currentRound - 1] = isCurrentShotGoal;

            if (isCurrentShotGoal)
            {
                if (activeGame == SportsMinigameType.Basketball)
                {
                    totalScore += currentShotPoints;
                    CozyAudioManager.PlaySfx(CozySfxType.NetSwish, 0.85f);
                    CozyAudioManager.PlaySfx(CozySfxType.Cheer, 0.65f);
                }
                else
                {
                    goalsScored += 1;
                    CozyAudioManager.PlaySfx(CozySfxType.NetSwish, 0.95f);
                    CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.75f);
                    CozyAudioManager.PlaySfx(CozySfxType.Cheer, 0.85f);
                }
            }
            else
            {
                CozyAudioManager.PlaySfx(CozySfxType.BallBounce, 0.70f);
            }
        }

        private void ShowGameOverModal()
        {
            state = MinigameState.GameOverModal;
            if (ballObj != null) ballObj.SetActive(false);
            if (goalkeeperObj != null)
            {
                goalkeeperObj.transform.rotation = Quaternion.identity;
                goalkeeperObj.SetActive(false);
            }

            int rewardCoins = 0;
            float restoreStamina = 0f;

            if (activeGame == SportsMinigameType.Basketball)
            {
                if (totalScore >= 12) { rewardCoins = 50; restoreStamina = 25f; }
                else if (totalScore >= 8) { rewardCoins = 30; restoreStamina = 18f; }
                else if (totalScore >= 4) { rewardCoins = 15; restoreStamina = 10f; }
                else { rewardCoins = 5; restoreStamina = 5f; }
            }
            else // Soccer
            {
                if (goalsScored >= 5) { rewardCoins = 60; restoreStamina = 30f; }
                else if (goalsScored >= 3) { rewardCoins = 35; restoreStamina = 20f; }
                else if (goalsScored >= 1) { rewardCoins = 15; restoreStamina = 10f; }
                else { rewardCoins = 5; restoreStamina = 5f; }
            }

            if (playerProfInv != null)
                playerProfInv.AddCoins(rewardCoins);
            if (playerStats != null)
                playerStats.ModifyVitals(0f, 0f, 0f, restoreStamina);

            CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.85f);
            CozyAudioManager.PlaySfx(CozySfxType.Cheer, 0.80f);
            CozySaveManager.SaveGame(false);
        }

        public void QuitGame()
        {
            activeGame = SportsMinigameType.None;
            state = MinigameState.Ready;
            if (ballObj != null) ballObj.SetActive(false);
            if (goalkeeperObj != null)
            {
                goalkeeperObj.transform.rotation = Quaternion.identity;
                goalkeeperObj.SetActive(false);
            }
        }

        // =====================================================================
        // ONGUI: GIAO DIỆN MINI GAME BÓNG RỔ & BÓNG ĐÁ
        // =====================================================================

        private void EnsureStyles()
        {
            if (stylesReady) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.50f) }
            };

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.90f, 0.95f) }
            };

            btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            popupStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            stylesReady = true;
        }

        private void OnGUI()
        {
            if (activeGame == SportsMinigameType.None)
                return;

            EnsureStyles();
            GUI.depth = -10;

            // 1. BẢNG ĐIỂM THỂ THAO TRÊN CÙNG MÀN HÌNH
            DrawTopScoreboard();

            // 2. THANH CANH GÓC SÚT & THANH ĐO LỰC Ở DƯỚI
            if (state == MinigameState.Aiming)
            {
                DrawAimingGauge();
            }
            else if (state == MinigameState.PowerMeter)
            {
                DrawPowerGauge();
            }
            else if (state == MinigameState.RoundCelebration)
            {
                DrawOutcomeBanner();
            }
            else if (state == MinigameState.GameOverModal)
            {
                DrawGameOverModal();
            }
        }

        private void DrawTopScoreboard()
        {
            float w = 460f;
            float h = 86f;
            Rect rect = new Rect((Screen.width - w) * 0.5f, 12f, w, h);

            DrawSolidRect(rect, new Color(0.10f, 0.14f, 0.22f, 0.94f));
            DrawOutline(rect, new Color(1f, 0.85f, 0.35f, 0.85f), 2);

            string title = (activeGame == SportsMinigameType.Basketball)
                ? "🏀 VUA NÉM RỔ — COZY HOOPS"
                : "⚽ VUA SÚT PHẠT ĐỀN — PENALTY SHOOTOUT";
            GUI.Label(new Rect(rect.x, rect.y + 4, w, 22), title, headerStyle);

            string scoreStr = (activeGame == SportsMinigameType.Basketball)
                ? $"Quả: {currentRound}/{TotalRounds}  |  Tổng Điểm: {totalScore} Điểm"
                : $"Lượt sút: {currentRound}/{TotalRounds}  |  Bàn thắng: {goalsScored}/{currentRound - (state == MinigameState.RoundCelebration ? 0 : 1)}";
            GUI.Label(new Rect(rect.x, rect.y + 25, w, 18), scoreStr, subHeaderStyle);

            GUIStyle hintStyle = new GUIStyle(subHeaderStyle) { fontSize = 11, normal = { textColor = new Color(0.95f, 0.82f, 0.40f) } };
            GUI.Label(new Rect(rect.x, rect.y + 43, w, 18), "🎮 Bấm [SPACE] hoặc Click Chuột vào Nút để Chốt & Ném / Sút  •  [ESC]: Thoát", hintStyle);

            // Các chấm tròn biểu thị kết quả từng vòng (● / ◯)
            float circleStartX = rect.x + (w - (TotalRounds * 24f)) * 0.5f;
            for (int r = 0; r < TotalRounds; r++)
            {
                Rect dotRect = new Rect(circleStartX + r * 24f, rect.y + 63, 16, 16);
                Color dotCol = (r < currentRound - 1 || (r == currentRound - 1 && state == MinigameState.RoundCelebration))
                    ? (roundResults[r] ? new Color(0.25f, 1f, 0.40f) : new Color(1f, 0.30f, 0.30f))
                    : (r == currentRound - 1 ? new Color(1f, 0.90f, 0.20f) : new Color(0.40f, 0.45f, 0.55f));
                DrawSolidCircle(dotRect, dotCol);
            }
        }

        private void DrawAimingGauge()
        {
            // Canh góc sút bóng đá (Mũi tên chỉ hướng khung thành)
            float w = 460f;
            float h = 106f;
            Rect rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 45f, w, h);

            DrawSolidRect(rect, new Color(0.08f, 0.12f, 0.18f, 0.95f));
            DrawOutline(rect, new Color(0.40f, 0.85f, 1.0f, 0.90f), 2);

            GUI.Label(new Rect(rect.x, rect.y + 6, w, 22), "🎯 BƯỚC 1: CANH HƯỚNG SÚT BÓNG", headerStyle);
            GUI.Label(new Rect(rect.x, rect.y + 26, w, 16), "⬅️ Góc Trái  |  🎯 Chính Giữa  |  ➡️ Góc Phải", subHeaderStyle);

            // Khung thành thu nhỏ để canh góc
            Rect goalBar = new Rect(rect.x + 36, rect.y + 44, w - 72, 20);
            DrawSolidRect(goalBar, new Color(0.18f, 0.26f, 0.36f));

            // Vạch chia 3 khu vực: Góc Trái | Chính Giữa | Góc Phải
            float gw = goalBar.width;
            DrawSolidRect(new Rect(goalBar.x + gw * 0.33f, goalBar.y, 2, goalBar.height), Color.white);
            DrawSolidRect(new Rect(goalBar.x + gw * 0.66f, goalBar.y, 2, goalBar.height), Color.white);

            // Con trỏ mục tiêu di chuyển
            float pointerX = goalBar.x + (lockedAim + 1f) * 0.5f * (gw - 14);
            Rect pointerRect = new Rect(pointerX, goalBar.y - 4, 14, 28);
            DrawSolidRect(pointerRect, new Color(1f, 0.25f, 0.35f));

            GUIStyle bigBtnStyle = new GUIStyle(btnStyle) { fontSize = 13, normal = { textColor = new Color(1f, 0.92f, 0.40f) } };
            if (GUI.Button(new Rect(rect.x + 30, rect.y + 68, w - 60, 30), "⚡ BẤM [SPACE] HOẶC VÀO ĐÂY ĐỂ CHỐT HƯỚNG", bigBtnStyle))
            {
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                gaugeTimer = 0f;
                state = MinigameState.PowerMeter;
            }
        }

        private void DrawPowerGauge()
        {
            float w = 460f;
            float h = 108f;
            Rect rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 45f, w, h);

            DrawSolidRect(rect, new Color(0.08f, 0.12f, 0.18f, 0.95f));
            DrawOutline(rect, new Color(1f, 0.80f, 0.25f, 0.95f), 2);

            string stepLabel = (activeGame == SportsMinigameType.Basketball)
                ? "🏀 BƯỚC 1: CANH VẠCH XANH Ở GIỮA ĐỂ NÉM SWISH!"
                : "⚡ BƯỚC 2: CANH LỰC SÚT CĂNG VÀO LƯỚI!";
            GUI.Label(new Rect(rect.x, rect.y + 6, w, 22), stepLabel, headerStyle);

            string hintLabel = (activeGame == SportsMinigameType.Basketball)
                ? "Canh vạch trắng trúng vùng XANH LÁ ở chính giữa để ghi 3 điểm!"
                : "Vùng Xanh Lá = Cú sút căng hiểm hóc hạ gục thủ môn!";
            GUI.Label(new Rect(rect.x, rect.y + 26, w, 16), hintLabel, subHeaderStyle);

            // Thanh lực ngang
            Rect barBg = new Rect(rect.x + 36, rect.y + 44, w - 72, 22);
            DrawSolidRect(barBg, new Color(0.18f, 0.20f, 0.25f));

            if (activeGame == SportsMinigameType.Basketball)
            {
                // Vùng Vàng (Good) và Xanh Lá (Perfect Swish) ở giữa
                float bw = barBg.width;
                Rect yellowZone = new Rect(barBg.x + bw * 0.35f, barBg.y, bw * 0.30f, barBg.height);
                Rect greenZone = new Rect(barBg.x + bw * 0.45f, barBg.y, bw * 0.10f, barBg.height);
                DrawSolidRect(yellowZone, new Color(0.95f, 0.75f, 0.20f, 0.85f));
                DrawSolidRect(greenZone, new Color(0.20f, 0.95f, 0.35f, 0.95f));
            }
            else
            {
                // Bóng đá: Vùng xanh lá ở mức 60%..86%
                float bw = barBg.width;
                Rect greenZone = new Rect(barBg.x + bw * 0.60f, barBg.y, bw * 0.26f, barBg.height);
                Rect redOverZone = new Rect(barBg.x + bw * 0.88f, barBg.y, bw * 0.12f, barBg.height);
                DrawSolidRect(greenZone, new Color(0.20f, 0.95f, 0.35f, 0.85f));
                DrawSolidRect(redOverZone, new Color(0.95f, 0.25f, 0.25f, 0.85f));
            }

            // Con trỏ lực
            float curX = barBg.x + lockedPower * (barBg.width - 12);
            Rect needleRect = new Rect(curX, barBg.y - 3, 12, barBg.height + 6);
            DrawSolidRect(needleRect, Color.white);

            string btnPrompt = (activeGame == SportsMinigameType.Basketball)
                ? "🔥 BẤM [SPACE] HOẶC VÀO ĐÂY ĐỂ NÉM BÓNG!"
                : "🚀 BẤM [SPACE] HOẶC VÀO ĐÂY ĐỂ SÚT CĂNG!";

            GUIStyle bigBtnStyle = new GUIStyle(btnStyle) { fontSize = 13, normal = { textColor = new Color(0.35f, 1f, 0.45f) } };
            if (GUI.Button(new Rect(rect.x + 30, rect.y + 70, w - 60, 30), btnPrompt, bigBtnStyle))
            {
                TriggerShot();
            }
        }

        private void DrawOutcomeBanner()
        {
            float w = 480f;
            float h = 48f;
            Rect rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.38f, w, h);

            DrawSolidRect(rect, new Color(0.08f, 0.10f, 0.16f, 0.92f));
            DrawOutline(rect, outcomeColor, 2);

            GUIStyle outcomeStyle = new GUIStyle(popupStyle)
            {
                fontSize = 17,
                normal = { textColor = outcomeColor }
            };
            GUI.Label(rect, outcomeText, outcomeStyle);
        }

        private void DrawGameOverModal()
        {
            float w = 440f;
            float h = 260f;
            Rect rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawSolidRect(rect, new Color(0.12f, 0.15f, 0.22f, 0.96f));
            DrawOutline(rect, new Color(1f, 0.85f, 0.30f), 3);

            string gameTitle = (activeGame == SportsMinigameType.Basketball)
                ? "🏆 TỔNG KẾT VUA NÉM RỔ"
                : "🏆 TỔNG KẾT VUA PHẠT ĐỀN";
            GUI.Label(new Rect(rect.x, rect.y + 14, w, 26), gameTitle, headerStyle);

            string starRating = "";
            string scoreDetail = "";
            string rewardText = "";

            if (activeGame == SportsMinigameType.Basketball)
            {
                scoreDetail = $"Bạn ghi được: {totalScore} / 15 Điểm ({CountGoals()} quả vào rổ)";
                if (totalScore >= 12) { starRating = "⭐⭐⭐ HẠNG S: SIÊU SAO BÓNG RỔ!"; rewardText = "Thưởng: +50 Xu, +25 Thể Lực!"; }
                else if (totalScore >= 8) { starRating = "⭐⭐ HẠNG A: XẠ THỦ ĐIÊU LUYỆN!"; rewardText = "Thưởng: +30 Xu, +18 Thể Lực!"; }
                else if (totalScore >= 4) { starRating = "⭐ HẠNG B: NÉM RẤT KHÁ!"; rewardText = "Thưởng: +15 Xu, +10 Thể Lực!"; }
                else { starRating = "HẠNG C: CỐ GẮNG RÈN LUYỆN THÊM!"; rewardText = "Thưởng: +5 Xu, +5 Thể Lực!"; }
            }
            else // Soccer
            {
                scoreDetail = $"Bạn ghi được: {goalsScored} / 5 Bàn Thắng";
                if (goalsScored >= 5) { starRating = "⭐⭐⭐ HẠNG S: VUA PHÁ LƯỚI THỊ TRẤN!"; rewardText = "Thưởng: +60 Xu, +30 Thể Lực!"; }
                else if (goalsScored >= 3) { starRating = "⭐⭐ HẠNG A: TIỀN ĐẠO XUẤT SẮC!"; rewardText = "Thưởng: +35 Xu, +20 Thể Lực!"; }
                else if (goalsScored >= 1) { starRating = "⭐ HẠNG B: SÚT BÓNG TỐT!"; rewardText = "Thưởng: +15 Xu, +10 Thể Lực!"; }
                else { starRating = "HẠNG C: THỦ MÔN BẮT QUÁ XUẤT THẦN!"; rewardText = "Thưởng: +5 Xu, +5 Thể Lực!"; }
            }

            GUI.Label(new Rect(rect.x, rect.y + 48, w, 22), scoreDetail, subHeaderStyle);
            GUIStyle starStyle = new GUIStyle(headerStyle)
            {
                fontSize = 15,
                normal = { textColor = new Color(1f, 0.90f, 0.25f) }
            };
            GUI.Label(new Rect(rect.x, rect.y + 76, w, 24), starRating, starStyle);

            GUIStyle rewardStyle = new GUIStyle(subHeaderStyle)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.40f, 1f, 0.50f) }
            };
            GUI.Label(new Rect(rect.x, rect.y + 110, w, 22), rewardText, rewardStyle);

            // Nút Chơi Lại hoặc Rời Sân
            float btnW = 160f;
            float btnH = 36f;
            float btnY = rect.y + h - 55f;

            if (GUI.Button(new Rect(rect.x + 35, btnY, btnW, btnH), "🔄 Chơi Lại", btnStyle))
            {
                if (activeGame == SportsMinigameType.Basketball)
                    LaunchBasketballGame(targetHoopPos, courtCenter);
                else
                    LaunchSoccerPenaltyGame(targetGoalPos, shootingSpotPos);
            }

            if (GUI.Button(new Rect(rect.x + rect.width - btnW - 35, btnY, btnW, btnH), "✅ Rời Sân [ESC]", btnStyle))
            {
                QuitGame();
            }
        }

        private int CountGoals()
        {
            int c = 0;
            for (int i = 0; i < roundResults.Length; i++)
                if (roundResults[i]) c++;
            return c;
        }

        // =====================================================================
        // HELPER DRAWING PRIMITIVES
        // =====================================================================

        private static Texture2D solidTex;
        private static void DrawSolidRect(Rect r, Color c)
        {
            if (solidTex == null)
            {
                solidTex = new Texture2D(1, 1);
                solidTex.SetPixel(0, 0, Color.white);
                solidTex.Apply();
            }
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, solidTex);
            GUI.color = old;
        }

        private static void DrawOutline(Rect r, Color c, int thickness = 2)
        {
            DrawSolidRect(new Rect(r.x, r.y, r.width, thickness), c);
            DrawSolidRect(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), c);
            DrawSolidRect(new Rect(r.x, r.y, thickness, r.height), c);
            DrawSolidRect(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), c);
        }

        private static void DrawSolidCircle(Rect r, Color c)
        {
            DrawSolidRect(r, c);
        }
    }

    /// <summary>
    /// Đối tượng Tương tác kích hoạt Minigame trên Sân Bóng Rổ.
    /// </summary>
    public class BasketballCourtInteractable : WorldInteractableBase
    {
        private Vector3 hoopPos;

        public void Configure(Vector3 hoopPosition)
        {
            hoopPos = hoopPosition;
            interactTitle = "Sân Bóng Rổ";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "🏀 Chơi Vua Ném Rổ (3-Point Shootout)";
        }

        public override string GetWorldOverheadLabel()
        {
            return "🏀 Sân Bóng Rổ";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (player == null) return;
            CozySportsMinigameManager.StartBasketballGame(hoopPos, transform.position);
        }
    }

    /// <summary>
    /// Đối tượng Tương tác kích hoạt Minigame trên Sân Bóng Đá.
    /// </summary>
    public class SoccerPitchInteractable : WorldInteractableBase
    {
        private Vector3 goalPos;

        public void Configure(Vector3 goalPosition)
        {
            goalPos = goalPosition;
            interactTitle = "Sân Bóng Đá Mini";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "⚽ Thử Thách Sút Penalty";
        }

        public override string GetWorldOverheadLabel()
        {
            return "⚽ Sân Bóng Đá Mini";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (player == null) return;
            CozySportsMinigameManager.StartSoccerPenaltyGame(goalPos, transform.position);
        }
    }
}
