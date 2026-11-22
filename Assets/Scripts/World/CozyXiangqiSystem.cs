using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    public enum XiangqiPieceType
    {
        None = 0,
        General = 1,  // Tướng / Soái
        Advisor = 2,  // Sĩ
        Elephant = 3, // Tượng
        Horse = 4,    // Mã
        Chariot = 5,  // Xe
        Cannon = 6,   // Pháo
        Soldier = 7   // Binh / Tốt
    }

    public enum XiangqiColor
    {
        Red = 0,   // Đi trước
        Black = 1  // Đi sau
    }

    [Serializable]
    public struct XiangqiPiece
    {
        public XiangqiPieceType type;
        public XiangqiColor color;

        public bool IsEmpty => type == XiangqiPieceType.None;

        public int MaterialValue => type switch
        {
            XiangqiPieceType.Soldier => 1,
            XiangqiPieceType.Advisor => 2,
            XiangqiPieceType.Elephant => 2,
            XiangqiPieceType.Horse => 4,
            XiangqiPieceType.Cannon => 4,
            XiangqiPieceType.Chariot => 9,
            _ => 0
        };

        public string HanziName => (color == XiangqiColor.Red) ? type switch
        {
            XiangqiPieceType.General => "帥",
            XiangqiPieceType.Advisor => "仕",
            XiangqiPieceType.Elephant => "相",
            XiangqiPieceType.Horse => "傌",
            XiangqiPieceType.Chariot => "俥",
            XiangqiPieceType.Cannon => "炮",
            XiangqiPieceType.Soldier => "兵",
            _ => ""
        } : type switch
        {
            XiangqiPieceType.General => "將",
            XiangqiPieceType.Advisor => "士",
            XiangqiPieceType.Elephant => "象",
            XiangqiPieceType.Horse => "馬",
            XiangqiPieceType.Chariot => "車",
            XiangqiPieceType.Cannon => "砲",
            XiangqiPieceType.Soldier => "卒",
            _ => ""
        };

        public string VietnameseName => (color == XiangqiColor.Red) ? type switch
        {
            XiangqiPieceType.General => "Tướng",
            XiangqiPieceType.Advisor => "Sĩ",
            XiangqiPieceType.Elephant => "Tượng",
            XiangqiPieceType.Horse => "Mã",
            XiangqiPieceType.Chariot => "Xe",
            XiangqiPieceType.Cannon => "Pháo",
            XiangqiPieceType.Soldier => "Binh",
            _ => ""
        } : type switch
        {
            XiangqiPieceType.General => "Tướng",
            XiangqiPieceType.Advisor => "Sĩ",
            XiangqiPieceType.Elephant => "Tượng",
            XiangqiPieceType.Horse => "Mã",
            XiangqiPieceType.Chariot => "Xe",
            XiangqiPieceType.Cannon => "Pháo",
            XiangqiPieceType.Soldier => "Tốt",
            _ => ""
        };
    }

    public struct XiangqiMove
    {
        public int fromR, fromC;
        public int toR, toC;
        public XiangqiPiece movedPiece;
        public XiangqiPiece capturedPiece;
        public string notation;
    }

    /// <summary>
    /// Bàn Cờ Tướng Công Viên gồm 1 bàn cờ đá/gỗ và 2 chiếc ghế ngồi 2 bên đặt gần Bàn Cờ Vua.
    /// Khi người chơi lại gần bấm [E] sẽ ngồi vào ghế 1 (phe Đỏ);
    /// Kỳ thủ thứ 2 sẽ bước tới ngồi vào ghế 2 (phe Đen);
    /// Khi cả 2 người đều đã ngồi vào bàn thì 3 giây sau sẽ chuyển sang Scene Đánh Cờ Tướng.
    /// </summary>
    public class ParkXiangqiTableInteractable : WorldInteractableBase
    {
        public static ParkXiangqiTableInteractable ActiveSeatedTable { get; private set; }

        private GameObject partnerNpcObj;
        private SpriteRenderer partnerSr;
        private CharacterAppearanceData partnerAppearance;
        private Vector3 partnerStandPos;

        private bool isPlayer1Seated = false;
        private bool isPlayer2Seated = false;
        private float seat2WalkProgress = 0f;
        private float countdownTimer = 3.0f;
        private int lastCountdownBeepSec = 4;
        private bool matchTriggered = false;

        private PlayerController seatedPlayerController;

        public Vector3 Seat1WorldPos => transform.position + new Vector3(-0.42f, -0.04f, 0f);
        public Vector3 Seat2WorldPos => transform.position + new Vector3(0.42f, -0.04f, 0f);
        public bool IsPlayerSeatedHere => isPlayer1Seated;
        public bool AreBothSeated => isPlayer1Seated && isPlayer2Seated;
        public float CountdownRemaining => countdownTimer;

        public void ConfigureParkXiangqiTable(Transform parent)
        {
            interactTitle = "Bàn Cờ Tướng Công Viên";
            partnerStandPos = transform.position + new Vector3(0.72f, 0.36f, 0f);

            partnerAppearance = new CharacterAppearanceData
            {
                characterName = "Kỳ Thủ Lão Thành",
                gender = GenderType.Male,
                skinToneIndex = 1,
                hairStyleIndex = 1,
                hairColorIndex = 3, // Tóc muối tiêu
                expressionIndex = 1,
                shirtIndex = 2,
                shirtColorIndex = 2,
                pantsIndex = 1,
                pantsColorIndex = 1,
                accessoryIndex = 0,
                shoesIndex = 1
            };

            partnerNpcObj = new GameObject("ParkXiangqi_MasterPlayer2");
            if (parent != null)
                partnerNpcObj.transform.SetParent(parent);
            partnerNpcObj.transform.position = partnerStandPos;

            partnerSr = partnerNpcObj.AddComponent<SpriteRenderer>();
            partnerSr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(partnerAppearance, 0);
            partnerSr.flipX = true;

            IsometricSorting sorting = partnerNpcObj.AddComponent<IsometricSorting>();
            sorting.Configure(true, 2);
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            if (isPlayer1Seated)
            {
                int sec = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 3);
                return isPlayer2Seated
                    ? $"🀄 Đủ 2 người ngồi! Chuyển sang cờ tướng sau {sec}s... ([WASD/ESC]: Đứng dậy)"
                    : "🪑 Đang chờ kỳ thủ thứ 2 ngồi vào ghế đối diện...";
            }
            return "[E] Ngồi vào Bàn Cờ Tướng Công Viên (2 Ghế)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (player == null) return;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            if (isPlayer1Seated)
            {
                StandUp();
                return;
            }

            SitDown(pc);
        }

        private void SitDown(PlayerController pc)
        {
            ActiveSeatedTable = this;
            seatedPlayerController = pc;
            isPlayer1Seated = true;
            isPlayer2Seated = false;
            seat2WalkProgress = 0f;
            countdownTimer = 3.0f;
            lastCountdownBeepSec = 4;
            matchTriggered = false;

            pc.transform.position = Seat1WorldPos;
            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = Seat1WorldPos;
            }
            var sr = pc.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = false;

            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("🀄 Bạn đã ngồi vào bàn Cờ Tướng (Phe Đỏ)! Chờ kỳ thủ thứ 2 ngồi vào...");
        }

        public void StandUp()
        {
            isPlayer1Seated = false;
            isPlayer2Seated = false;
            seat2WalkProgress = 0f;
            countdownTimer = 3.0f;
            matchTriggered = false;

            if (seatedPlayerController != null)
            {
                Vector3 exitPos = transform.position + new Vector3(0f, -0.45f, 0f);
                seatedPlayerController.transform.position = exitPos;
                var rb = seatedPlayerController.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = exitPos;
                seatedPlayerController = null;
            }

            if (partnerNpcObj != null)
            {
                partnerNpcObj.transform.position = partnerStandPos;
                if (partnerSr != null) partnerSr.flipX = true;
            }

            if (ActiveSeatedTable == this)
                ActiveSeatedTable = null;

            CozyLifeHUD.PushToast("Đã đứng dậy rời bàn Cờ Tướng.");
        }

        private void Update()
        {
            if (!isPlayer1Seated)
            {
                if (partnerNpcObj != null && !CozyXiangqiManager.IsXiangqiSceneActive)
                {
                    partnerNpcObj.transform.position = Vector3.MoveTowards(
                        partnerNpcObj.transform.position, partnerStandPos, 1.8f * Time.deltaTime);
                    if (partnerSr != null) partnerSr.flipX = true;
                }
                return;
            }

            if (CozyXiangqiManager.IsXiangqiSceneActive)
                return;

            // Giữ vị trí người chơi 1 ngồi trên ghế trái
            if (seatedPlayerController != null)
            {
                seatedPlayerController.transform.position = Seat1WorldPos;
                var rb = seatedPlayerController.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = Seat1WorldPos;
                var sr = seatedPlayerController.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = false;
            }

            // Người chơi có thể bấm phím di chuyển hoặc ESC để đứng dậy trước khi vào trận
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame ||
                kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
            {
                StandUp();
                return;
            }

            if (!isPlayer2Seated)
            {
                seat2WalkProgress += Time.deltaTime * 2.2f;
                if (partnerNpcObj != null)
                {
                    partnerNpcObj.transform.position = Vector3.Lerp(partnerStandPos, Seat2WorldPos, Mathf.Clamp01(seat2WalkProgress));
                    if (partnerSr != null) partnerSr.flipX = true;
                }

                if (seat2WalkProgress >= 1f)
                {
                    isPlayer2Seated = true;
                    countdownTimer = 3.0f;
                    lastCountdownBeepSec = 4;
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                    CozyLifeHUD.PushToast("🀄 Cả 2 người đã ngồi vào bàn! Bắt đầu ván Cờ Tướng sau 3 giây...");
                }
            }
            else if (!matchTriggered)
            {
                // Khi cả 2 người đều đã ngồi vào bàn -> Đếm ngược 3 giây
                countdownTimer -= Time.deltaTime;
                int ceilSec = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 3);
                if (ceilSec < lastCountdownBeepSec && countdownTimer > 0f)
                {
                    lastCountdownBeepSec = ceilSec;
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick, 0.75f);
                }

                if (countdownTimer <= 0f)
                {
                    matchTriggered = true;
                    CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.9f);
                    if (CozyLifeHUD.Instance != null)
                    {
                        CozyLifeHUD.Instance.PlaySceneTransition("🀄 BÀN CỜ TƯỚNG CÔNG VIÊN — CHUẨN QUỐC TẾ", () =>
                        {
                            CozyXiangqiManager.StartXiangqiMatch(this);
                        });
                    }
                    else
                    {
                        CozyXiangqiManager.StartXiangqiMatch(this);
                    }
                }
            }
        }

        public override string GetWorldOverheadLabel()
        {
            if (isPlayer1Seated && isPlayer2Seated && !matchTriggered)
            {
                int sec = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 3);
                return $"🀄 Vào Bàn Cờ sau {sec}s...";
            }
            if (isPlayer1Seated)
            {
                return "🀄 Đang chờ Kỳ thủ thứ 2...";
            }
            return "[E] Ngồi Chơi Cờ Tướng";
        }
    }

    /// <summary>
    /// Bộ máy Cờ Tướng Quốc Tế Chuẩn (Xiangqi Engine & Match Manager):
    /// - Bàn cờ 9 cột x 10 hàng (90 điểm giao cắt), ranh giới Sông (Sở Hà - Hán Giới) và 2 Cung Cấm (Cung Tướng).
    /// - Đầy đủ 100% luật Cờ Tướng:
    ///   + Tướng/Soái: Di chuyển 1 ô ngang/dọc trong Cung; LUẬT LỘ MẶT TƯỚNG (2 Tướng không được nhìn thấy nhau trên cùng cột).
    ///   + Sĩ: Đi chéo 1 ô trong Cung (5 vị trí cố định).
    ///   + Tượng: Đi chéo 2 ô (hình chữ Điền), KHÔNG ĐƯỢC QUA SÔNG, LUẬT CẢN TƯỢNG (mắt tượng bị chặn).
    ///   + Mã: Đi hình chữ Nhật (chữ L), LUẬT CẢN MÃ (vướng chân Mã).
    ///   + Xe: Đi ngang/dọc không giới hạn ô khi không có quân cản.
    ///   + Pháo: Đi như Xe khi không ăn; Khi ăn quân PHẢI CÓ ĐÚNG 1 QUÂN LÀM NGÒI.
    ///   + Binh/Tốt: Trước khi qua sông chỉ tiến 1 ô; Sau khi qua sông có thể tiến 1 ô hoặc đi ngang 1 ô; Không bao giờ lùi.
    /// - Kiểm tra Chiếu Tướng, Chiếu Bí, Hết Nước Đi (Bí nước thì bị xử thua chuẩn Cờ Tướng).
    /// - Hỗ trợ chế độ 2 Người Cùng Đánh luân phiên Đỏ & Đen, tùy chọn bật AI, ghi biên bản nước đi tiếng Việt, đồng hồ thi đấu.
    /// </summary>
    public class CozyXiangqiManager : MonoBehaviour
    {
        public static CozyXiangqiManager Instance { get; private set; }
        public static bool IsMatchActive => Instance != null && Instance.isSceneOpen;
        public static bool IsXiangqiSceneActive => IsMatchActive;

        private bool isSceneOpen = false;
        private ParkXiangqiTableInteractable boundTable;

        // Bàn cờ 10 hàng (0..9) x 9 cột (0..8)
        // Hàng 0 là đáy phe Đỏ, hàng 9 là đáy phe Đen
        private readonly XiangqiPiece[,] board = new XiangqiPiece[10, 9];
        private XiangqiColor currentTurn = XiangqiColor.Red;
        private int turnNumber = 1;

        // Quân cờ đã ăn
        private readonly List<XiangqiPiece> capturedByRed = new List<XiangqiPiece>();
        private readonly List<XiangqiPiece> capturedByBlack = new List<XiangqiPiece>();
        private readonly List<string> moveNotations = new List<string>();

        // Tương tác chọn điểm & nước đi hợp lệ
        private Vector2Int selectedPoint = new Vector2Int(-1, -1);
        private readonly List<XiangqiMove> currentLegalMoves = new List<XiangqiMove>();
        private XiangqiMove lastExecutedMove;
        private bool hasLastMove = false;

        // Kết quả ván cờ
        private bool isGameOver = false;
        private bool isCheckmate = false;
        private bool isStalemate = false;
        private string gameOverHeadline = "";
        private string gameOverSubtext = "";
        private bool isGeneralInCheck = false;

        // Chế độ chơi: 2 người cùng đánh hoặc đấu máy
        private bool playAgainstAI = false;
        private float aiThinkTimer = 0f;
        private bool flipBoardPerspective = false;
        private bool useCustomPieceSprites = true;

        // Đồng hồ thi đấu (15 phút mỗi bên)
        private float redTimeRemaining = 900f;
        private float blackTimeRemaining = 900f;

        // Cache Textures
        private Texture2D whiteTex;
        private readonly Dictionary<string, Texture2D> pieceTextures = new Dictionary<string, Texture2D>();
        private Vector2 moveScrollPos;

        public static void StartXiangqiMatch(ParkXiangqiTableInteractable table)
        {
            if (Instance == null)
            {
                GameObject host = new GameObject("CozyXiangqi_Host");
                Instance = host.AddComponent<CozyXiangqiManager>();
                DontDestroyOnLoad(host);
            }
            Instance.OpenMatch(table);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadPieceTextures();
        }

        private void LoadPieceTextures()
        {
            string[] types = { "general", "advisor", "elephant", "horse", "chariot", "cannon", "soldier" };
            foreach (var t in types)
            {
                Texture2D rTex = Resources.Load<Texture2D>($"CozyLifeArt/Xiangqi/piece_r_{t}");
                if (rTex != null) pieceTextures[$"r_{t}"] = rTex;

                Texture2D bTex = Resources.Load<Texture2D>($"CozyLifeArt/Xiangqi/piece_b_{t}");
                if (bTex != null) pieceTextures[$"b_{t}"] = bTex;
            }
        }

        public void OpenMatch(ParkXiangqiTableInteractable table)
        {
            boundTable = table;
            isSceneOpen = true;
            playAgainstAI = false;
            flipBoardPerspective = false;
            useCustomPieceSprites = true;

            ResetBoardToStandardXiangqi();
            CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.8f);
            CozyLifeHUD.PushToast("🀄 Ván Cờ Tướng bắt đầu! Phe Đỏ đi trước.");
        }

        public void ExitXiangqiScene()
        {
            isSceneOpen = false;
            if (boundTable != null)
            {
                boundTable.StandUp();
            }
            CozyAudioManager.PlaySfx(CozySfxType.Door);
        }

        public void ResetBoardToStandardXiangqi()
        {
            for (int r = 0; r < 10; r++)
                for (int c = 0; c < 9; c++)
                    board[r, c] = default;

            capturedByRed.Clear();
            capturedByBlack.Clear();
            moveNotations.Clear();
            selectedPoint = new Vector2Int(-1, -1);
            currentLegalMoves.Clear();
            hasLastMove = false;

            isGameOver = false;
            isCheckmate = false;
            isStalemate = false;
            gameOverHeadline = "";
            gameOverSubtext = "";
            isGeneralInCheck = false;

            currentTurn = XiangqiColor.Red;
            turnNumber = 1;
            redTimeRemaining = 900f;
            blackTimeRemaining = 900f;

            // Phe Đỏ (Hàng 0, 2, 3)
            board[0, 0] = new XiangqiPiece { type = XiangqiPieceType.Chariot, color = XiangqiColor.Red };
            board[0, 1] = new XiangqiPiece { type = XiangqiPieceType.Horse, color = XiangqiColor.Red };
            board[0, 2] = new XiangqiPiece { type = XiangqiPieceType.Elephant, color = XiangqiColor.Red };
            board[0, 3] = new XiangqiPiece { type = XiangqiPieceType.Advisor, color = XiangqiColor.Red };
            board[0, 4] = new XiangqiPiece { type = XiangqiPieceType.General, color = XiangqiColor.Red };
            board[0, 5] = new XiangqiPiece { type = XiangqiPieceType.Advisor, color = XiangqiColor.Red };
            board[0, 6] = new XiangqiPiece { type = XiangqiPieceType.Elephant, color = XiangqiColor.Red };
            board[0, 7] = new XiangqiPiece { type = XiangqiPieceType.Horse, color = XiangqiColor.Red };
            board[0, 8] = new XiangqiPiece { type = XiangqiPieceType.Chariot, color = XiangqiColor.Red };

            board[2, 1] = new XiangqiPiece { type = XiangqiPieceType.Cannon, color = XiangqiColor.Red };
            board[2, 7] = new XiangqiPiece { type = XiangqiPieceType.Cannon, color = XiangqiColor.Red };

            for (int c = 0; c < 9; c += 2)
                board[3, c] = new XiangqiPiece { type = XiangqiPieceType.Soldier, color = XiangqiColor.Red };

            // Phe Đen (Hàng 9, 7, 6)
            board[9, 0] = new XiangqiPiece { type = XiangqiPieceType.Chariot, color = XiangqiColor.Black };
            board[9, 1] = new XiangqiPiece { type = XiangqiPieceType.Horse, color = XiangqiColor.Black };
            board[9, 2] = new XiangqiPiece { type = XiangqiPieceType.Elephant, color = XiangqiColor.Black };
            board[9, 3] = new XiangqiPiece { type = XiangqiPieceType.Advisor, color = XiangqiColor.Black };
            board[9, 4] = new XiangqiPiece { type = XiangqiPieceType.General, color = XiangqiColor.Black };
            board[9, 5] = new XiangqiPiece { type = XiangqiPieceType.Advisor, color = XiangqiColor.Black };
            board[9, 6] = new XiangqiPiece { type = XiangqiPieceType.Elephant, color = XiangqiColor.Black };
            board[9, 7] = new XiangqiPiece { type = XiangqiPieceType.Horse, color = XiangqiColor.Black };
            board[9, 8] = new XiangqiPiece { type = XiangqiPieceType.Chariot, color = XiangqiColor.Black };

            board[7, 1] = new XiangqiPiece { type = XiangqiPieceType.Cannon, color = XiangqiColor.Black };
            board[7, 7] = new XiangqiPiece { type = XiangqiPieceType.Cannon, color = XiangqiColor.Black };

            for (int c = 0; c < 9; c += 2)
                board[6, c] = new XiangqiPiece { type = XiangqiPieceType.Soldier, color = XiangqiColor.Black };
        }

        private void Update()
        {
            if (!isSceneOpen)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitXiangqiScene();
                return;
            }

            if (isGameOver)
                return;

            // Đếm ngược đồng hồ thi đấu
            if (currentTurn == XiangqiColor.Red)
            {
                redTimeRemaining -= Time.deltaTime;
                if (redTimeRemaining <= 0f)
                {
                    redTimeRemaining = 0f;
                    TriggerTimeOut(XiangqiColor.Red);
                }
            }
            else
            {
                blackTimeRemaining -= Time.deltaTime;
                if (blackTimeRemaining <= 0f)
                {
                    blackTimeRemaining = 0f;
                    TriggerTimeOut(XiangqiColor.Black);
                }
            }

            // Xử lý AI nếu đang chơi với máy
            if (playAgainstAI && currentTurn == XiangqiColor.Black && !isGameOver)
            {
                aiThinkTimer += Time.deltaTime;
                if (aiThinkTimer >= 0.75f)
                {
                    aiThinkTimer = 0f;
                    PerformAIMove();
                }
            }
        }

        private void TriggerTimeOut(XiangqiColor timedOutColor)
        {
            isGameOver = true;
            string winner = (timedOutColor == XiangqiColor.Red) ? "Phe Đen (Black)" : "Phe Đỏ (Red)";
            string loser = (timedOutColor == XiangqiColor.Red) ? "Phe Đỏ" : "Phe Đen";
            gameOverHeadline = $"⌛ HẾT GIỜ THI ĐẤU! {winner.ToUpper()} THẮNG!";
            gameOverSubtext = $"{loser} đã cạn thời gian suy nghĩ 15 phút.";
            CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.9f);
        }

        // =====================================================================
        // QUY TẮC NƯỚC ĐI CỜ TƯỚNG CHUẨN QUỐC TẾ
        // =====================================================================
        public static bool IsInside(int r, int c) => r >= 0 && r < 10 && c >= 0 && c < 9;

        public static bool IsInPalace(XiangqiColor color, int r, int c)
        {
            if (c < 3 || c > 5) return false;
            return (color == XiangqiColor.Red) ? (r >= 0 && r <= 2) : (r >= 7 && r <= 9);
        }

        private void GeneratePseudoLegalMoves(XiangqiPiece[,] b, int fromR, int fromC, List<XiangqiMove> moves)
        {
            XiangqiPiece p = b[fromR, fromC];
            if (p.IsEmpty) return;

            switch (p.type)
            {
                case XiangqiPieceType.General:
                    GenerateGeneralMoves(b, fromR, fromC, p.color, moves);
                    break;
                case XiangqiPieceType.Advisor:
                    GenerateAdvisorMoves(b, fromR, fromC, p.color, moves);
                    break;
                case XiangqiPieceType.Elephant:
                    GenerateElephantMoves(b, fromR, fromC, p.color, moves);
                    break;
                case XiangqiPieceType.Horse:
                    GenerateHorseMoves(b, fromR, fromC, p.color, moves);
                    break;
                case XiangqiPieceType.Chariot:
                    GenerateChariotMoves(b, fromR, fromC, p.color, moves);
                    break;
                case XiangqiPieceType.Cannon:
                    GenerateCannonMoves(b, fromR, fromC, p.color, moves);
                    break;
                case XiangqiPieceType.Soldier:
                    GenerateSoldierMoves(b, fromR, fromC, p.color, moves);
                    break;
            }
        }

        private void GenerateGeneralMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            int[] dr = { 1, -1, 0, 0 };
            int[] dc = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nr = r + dr[i];
                int nc = c + dc[i];
                if (IsInPalace(color, nr, nc))
                {
                    XiangqiPiece dest = b[nr, nc];
                    if (dest.IsEmpty || dest.color != color)
                    {
                        moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                    }
                }
            }
        }

        private void GenerateAdvisorMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            int[] dr = { 1, 1, -1, -1 };
            int[] dc = { 1, -1, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nr = r + dr[i];
                int nc = c + dc[i];
                if (IsInPalace(color, nr, nc))
                {
                    XiangqiPiece dest = b[nr, nc];
                    if (dest.IsEmpty || dest.color != color)
                    {
                        moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                    }
                }
            }
        }

        private void GenerateElephantMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            int[] dr = { 2, 2, -2, -2 };
            int[] dc = { 2, -2, 2, -2 };
            for (int i = 0; i < 4; i++)
            {
                int nr = r + dr[i];
                int nc = c + dc[i];
                if (!IsInside(nr, nc)) continue;

                // Tượng không được qua sông
                if (color == XiangqiColor.Red && nr > 4) continue;
                if (color == XiangqiColor.Black && nr < 5) continue;

                // Kiểm tra Mắt Tượng (Cản Tượng)
                int eyeR = r + dr[i] / 2;
                int eyeC = c + dc[i] / 2;
                if (!b[eyeR, eyeC].IsEmpty) continue;

                XiangqiPiece dest = b[nr, nc];
                if (dest.IsEmpty || dest.color != color)
                {
                    moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                }
            }
        }

        private void GenerateHorseMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            // 8 hướng nhảy của Mã
            int[] dr = { 2, 2, -2, -2, 1, 1, -1, -1 };
            int[] dc = { 1, -1, 1, -1, 2, -2, 2, -2 };
            // Điểm cản tương ứng của chân Mã
            int[] legR = { 1, 1, -1, -1, 0, 0, 0, 0 };
            int[] legC = { 0, 0, 0, 0, 1, -1, 1, -1 };

            for (int i = 0; i < 8; i++)
            {
                int nr = r + dr[i];
                int nc = c + dc[i];
                if (!IsInside(nr, nc)) continue;

                // Kiểm tra Vướng Chân Mã (Cản Mã)
                int blockR = r + legR[i];
                int blockC = c + legC[i];
                if (!b[blockR, blockC].IsEmpty) continue;

                XiangqiPiece dest = b[nr, nc];
                if (dest.IsEmpty || dest.color != color)
                {
                    moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                }
            }
        }

        private void GenerateChariotMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            int[] dr = { 1, -1, 0, 0 };
            int[] dc = { 0, 0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int step = 1;
                while (true)
                {
                    int nr = r + dr[i] * step;
                    int nc = c + dc[i] * step;
                    if (!IsInside(nr, nc)) break;

                    XiangqiPiece dest = b[nr, nc];
                    if (dest.IsEmpty)
                    {
                        moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                    }
                    else
                    {
                        if (dest.color != color)
                        {
                            moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                        }
                        break; // Bị quân cản
                    }
                    step++;
                }
            }
        }

        private void GenerateCannonMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            int[] dr = { 1, -1, 0, 0 };
            int[] dc = { 0, 0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int step = 1;
                bool foundScreen = false; // Đã tìm thấy quân làm Ngòi chưa

                while (true)
                {
                    int nr = r + dr[i] * step;
                    int nc = c + dc[i] * step;
                    if (!IsInside(nr, nc)) break;

                    XiangqiPiece dest = b[nr, nc];
                    if (!foundScreen)
                    {
                        if (dest.IsEmpty)
                        {
                            // Đi không ăn quân (chưa qua ngòi)
                            moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                        }
                        else
                        {
                            // Gặp quân đầu tiên -> Trở thành Ngòi Pháo
                            foundScreen = true;
                        }
                    }
                    else
                    {
                        // Đã có ngòi -> Tìm quân đối phương để ăn
                        if (!dest.IsEmpty)
                        {
                            if (dest.color != color)
                            {
                                moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                            }
                            break; // Sau khi gặp quân thứ 2 thì dừng
                        }
                    }
                    step++;
                }
            }
        }

        private void GenerateSoldierMoves(XiangqiPiece[,] b, int r, int c, XiangqiColor color, List<XiangqiMove> moves)
        {
            int fwd = (color == XiangqiColor.Red) ? 1 : -1;
            bool crossedRiver = (color == XiangqiColor.Red) ? (r >= 5) : (r <= 4);

            // Nước tiến về phía trước
            int nr = r + fwd;
            if (IsInside(nr, c))
            {
                XiangqiPiece dest = b[nr, c];
                if (dest.IsEmpty || dest.color != color)
                {
                    moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = nr, toC = c, movedPiece = b[r, c], capturedPiece = dest });
                }
            }

            // Sau khi qua sông được đi ngang 1 ô (trái/phải)
            if (crossedRiver)
            {
                int[] dc = { 1, -1 };
                for (int i = 0; i < 2; i++)
                {
                    int nc = c + dc[i];
                    if (IsInside(r, nc))
                    {
                        XiangqiPiece dest = b[r, nc];
                        if (dest.IsEmpty || dest.color != color)
                        {
                            moves.Add(new XiangqiMove { fromR = r, fromC = c, toR = r, toC = nc, movedPiece = b[r, c], capturedPiece = dest });
                        }
                    }
                }
            }
        }

        // =====================================================================
        // KIỂM TRA LUẬT LỘ MẶT TƯỚNG & CHIẾU TƯỚNG
        // =====================================================================
        public static bool IsFlyingGeneral(XiangqiPiece[,] b)
        {
            // Tìm vị trí 2 Tướng
            int rRed = -1, cRed = -1;
            int rBlack = -1, cBlack = -1;

            for (int r = 0; r < 10; r++)
            {
                for (int c = 3; c <= 5; c++)
                {
                    var p = b[r, c];
                    if (p.type == XiangqiPieceType.General)
                    {
                        if (p.color == XiangqiColor.Red) { rRed = r; cRed = c; }
                        else { rBlack = r; cBlack = c; }
                    }
                }
            }

            if (rRed == -1 || rBlack == -1) return false;

            // Nếu 2 Tướng cùng cột
            if (cRed == cBlack)
            {
                int minR = Mathf.Min(rRed, rBlack);
                int maxR = Mathf.Max(rRed, rBlack);
                int blockers = 0;
                for (int r = minR + 1; r < maxR; r++)
                {
                    if (!b[r, cRed].IsEmpty) blockers++;
                }
                if (blockers == 0) return true; // Lộ mặt tướng!
            }
            return false;
        }

        public static bool IsGeneralInCheck(XiangqiPiece[,] b, XiangqiColor kingColor)
        {
            // Tìm vị trí Tướng phe mình
            int kr = -1, kc = -1;
            for (int r = 0; r < 10; r++)
            {
                for (int c = 3; c <= 5; c++)
                {
                    var p = b[r, c];
                    if (p.type == XiangqiPieceType.General && p.color == kingColor)
                    {
                        kr = r; kc = c;
                        break;
                    }
                }
                if (kr != -1) break;
            }

            if (kr == -1) return true;

            XiangqiColor enemyColor = (kingColor == XiangqiColor.Red) ? XiangqiColor.Black : XiangqiColor.Red;

            // Kiểm tra xem có quân địch nào có thể ăn Tướng tại (kr, kc) không
            for (int r = 0; r < 10; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var p = b[r, c];
                    if (p.IsEmpty || p.color != enemyColor) continue;

                    switch (p.type)
                    {
                        case XiangqiPieceType.Chariot:
                            if (CanChariotAttack(b, r, c, kr, kc)) return true;
                            break;
                        case XiangqiPieceType.Cannon:
                            if (CanCannonAttack(b, r, c, kr, kc)) return true;
                            break;
                        case XiangqiPieceType.Horse:
                            if (CanHorseAttack(b, r, c, kr, kc)) return true;
                            break;
                        case XiangqiPieceType.Soldier:
                            if (CanSoldierAttack(r, c, enemyColor, kr, kc)) return true;
                            break;
                    }
                }
            }
            return false;
        }

        private static bool CanChariotAttack(XiangqiPiece[,] b, int fr, int fc, int tr, int tc)
        {
            if (fr != tr && fc != tc) return false;
            int dr = Math.Sign(tr - fr);
            int dc = Math.Sign(tc - fc);
            int curR = fr + dr;
            int curC = fc + dc;
            while (curR != tr || curC != tc)
            {
                if (!b[curR, curC].IsEmpty) return false;
                curR += dr;
                curC += dc;
            }
            return true;
        }

        private static bool CanCannonAttack(XiangqiPiece[,] b, int fr, int fc, int tr, int tc)
        {
            if (fr != tr && fc != tc) return false;
            int dr = Math.Sign(tr - fr);
            int dc = Math.Sign(tc - fc);
            int curR = fr + dr;
            int curC = fc + dc;
            int blockers = 0;
            while (curR != tr || curC != tc)
            {
                if (!b[curR, curC].IsEmpty) blockers++;
                curR += dr;
                curC += dc;
            }
            return blockers == 1; // Đúng 1 quân làm ngòi
        }

        private static bool CanHorseAttack(XiangqiPiece[,] b, int fr, int fc, int tr, int tc)
        {
            int dr = tr - fr;
            int dc = tc - fc;
            if (Math.Abs(dr) == 2 && Math.Abs(dc) == 1)
            {
                return b[fr + Math.Sign(dr), fc].IsEmpty;
            }
            if (Math.Abs(dr) == 1 && Math.Abs(dc) == 2)
            {
                return b[fr, fc + Math.Sign(dc)].IsEmpty;
            }
            return false;
        }

        private static bool CanSoldierAttack(int fr, int fc, XiangqiColor soldierColor, int tr, int tc)
        {
            int fwd = (soldierColor == XiangqiColor.Red) ? 1 : -1;
            bool crossedRiver = (soldierColor == XiangqiColor.Red) ? (fr >= 5) : (fr <= 4);

            if (tr == fr + fwd && tc == fc) return true;
            if (crossedRiver && tr == fr && Math.Abs(tc - fc) == 1) return true;
            return false;
        }

        // Lọc tất cả các nước đi hợp lệ tuyệt đối (Strictly Legal Moves)
        public List<XiangqiMove> GetStrictlyLegalMovesForPiece(int r, int c)
        {
            List<XiangqiMove> result = new List<XiangqiMove>();
            XiangqiPiece p = board[r, c];
            if (p.IsEmpty || p.color != currentTurn) return result;

            List<XiangqiMove> pseudo = new List<XiangqiMove>();
            GeneratePseudoLegalMoves(board, r, c, pseudo);

            XiangqiPiece[,] temp = new XiangqiPiece[10, 9];
            for (int i = 0; i < pseudo.Count; i++)
            {
                XiangqiMove m = pseudo[i];
                Array.Copy(board, temp, board.Length);

                // Thử đi
                temp[m.toR, m.toC] = temp[m.fromR, m.fromC];
                temp[m.fromR, m.fromC] = default;

                // Kiểm tra 2 điều kiện bất khả xâm phạm:
                // 1. Không phạm luật Lộ Mặt Tướng
                if (IsFlyingGeneral(temp)) continue;

                // 2. Không để Tướng phe mình bị Chiếu
                if (IsGeneralInCheck(temp, currentTurn)) continue;

                m.notation = GenerateVietnameseNotation(m);
                result.Add(m);
            }
            return result;
        }

        public List<XiangqiMove> GetAllStrictLegalMoves(XiangqiColor side)
        {
            List<XiangqiMove> all = new List<XiangqiMove>();
            for (int r = 0; r < 10; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    if (!board[r, c].IsEmpty && board[r, c].color == side)
                    {
                        all.AddRange(GetStrictlyLegalMovesForPiece(r, c));
                    }
                }
            }
            return all;
        }

        // Tạo ký hiệu đại số tiếng Việt chuẩn Cờ Tướng:
        // Ví dụ: "Pháo 2 bình 5", "Mã 8 tiến 7", "Xe 9 tiến 1", "Tốt 5 tiến 1"
        private string GenerateVietnameseNotation(XiangqiMove m)
        {
            string piece = m.movedPiece.VietnameseName;
            // Cột theo góc nhìn của người chơi (Đỏ: cột 8 là 1, cột 0 là 9; hoặc quy ước chuẩn 1..9 từ phải qua trái)
            int fromColDisplay = (m.movedPiece.color == XiangqiColor.Red) ? (9 - m.fromC) : (m.fromC + 1);
            int toColDisplay = (m.movedPiece.color == XiangqiColor.Red) ? (9 - m.toC) : (m.toC + 1);

            int dr = m.toR - m.fromR;
            bool isRed = (m.movedPiece.color == XiangqiColor.Red);
            bool movingForward = isRed ? (dr > 0) : (dr < 0);
            bool movingBackward = isRed ? (dr < 0) : (dr > 0);

            if (m.fromC == m.toC)
            {
                int steps = Math.Abs(dr);
                string action = movingForward ? "tiến" : (movingBackward ? "thoái" : "bình");
                return $"{piece} {fromColDisplay} {action} {steps}";
            }
            else
            {
                // Đi chéo hoặc đi ngang
                if (m.movedPiece.type == XiangqiPieceType.Horse || m.movedPiece.type == XiangqiPieceType.Elephant || m.movedPiece.type == XiangqiPieceType.Advisor)
                {
                    string action = movingForward ? "tiến" : "thoái";
                    return $"{piece} {fromColDisplay} {action} {toColDisplay}";
                }
                else
                {
                    return $"{piece} {fromColDisplay} bình {toColDisplay}";
                }
            }
        }

        // =====================================================================
        // THỰC HIỆN NƯỚC ĐI & CHUYỂN LƯỢT
        // =====================================================================
        public void ExecuteMove(XiangqiMove m)
        {
            if (m.capturedPiece.type != XiangqiPieceType.None)
            {
                if (currentTurn == XiangqiColor.Red)
                    capturedByRed.Add(m.capturedPiece);
                else
                    capturedByBlack.Add(m.capturedPiece);

                CozyAudioManager.PlaySfx(CozySfxType.Harvest, 0.9f);
            }
            else
            {
                CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture, 0.85f);
            }

            board[m.toR, m.toC] = m.movedPiece;
            board[m.fromR, m.fromC] = default;

            lastExecutedMove = m;
            hasLastMove = true;

            // Ghi vào biên bản
            if (currentTurn == XiangqiColor.Red)
            {
                moveNotations.Add($"{turnNumber}. Đỏ: {m.notation}");
            }
            else
            {
                int lastIdx = moveNotations.Count - 1;
                if (lastIdx >= 0)
                    moveNotations[lastIdx] += $"  |  Đen: {m.notation}";
                else
                    moveNotations.Add($"{turnNumber}. Đen: {m.notation}");
                turnNumber++;
            }

            // Đổi lượt
            currentTurn = (currentTurn == XiangqiColor.Red) ? XiangqiColor.Black : XiangqiColor.Red;
            selectedPoint = new Vector2Int(-1, -1);
            currentLegalMoves.Clear();

            // Kiểm tra Chiếu Tướng và Kết quả
            isGeneralInCheck = IsGeneralInCheck(board, currentTurn);
            var nextTurnMoves = GetAllStrictLegalMoves(currentTurn);

            if (isGeneralInCheck)
            {
                CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert, 0.95f);
                CozyLifeHUD.PushToast($"⚔️ CHIẾU TƯỚNG! Phe {((currentTurn == XiangqiColor.Red) ? "Đỏ" : "Đen")} đang bị chiếu!");
            }

            if (nextTurnMoves.Count == 0)
            {
                isGameOver = true;
                string winner = (currentTurn == XiangqiColor.Red) ? "Phe Đen (Black)" : "Phe Đỏ (Red)";
                if (isGeneralInCheck)
                {
                    isCheckmate = true;
                    gameOverHeadline = $"👑 CHIẾU BÍ! {winner.ToUpper()} TOÀN THẮNG!";
                    gameOverSubtext = $"Tướng đối phương bị chiếu bí, không còn đường thoát!";
                }
                else
                {
                    // Trong Cờ Tướng, bên hết nước đi (bị bí nước) thì THUA
                    isStalemate = true;
                    gameOverHeadline = $"🔒 HẾT NƯỚC ĐI (BÍ NƯỚC)! {winner.ToUpper()} THẮNG!";
                    gameOverSubtext = $"Bên bị bí nước không có nước đi hợp lệ nào, xử thua theo luật Cờ Tướng!";
                }
                CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.9f);
            }
        }

        private void PerformAIMove()
        {
            var moves = GetAllStrictLegalMoves(XiangqiColor.Black);
            if (moves.Count == 0) return;

            // Tìm nước đi tốt nhất: ưu tiên ăn quân có giá trị lớn hoặc chiếu tướng
            XiangqiMove bestMove = moves[0];
            int bestScore = -999;

            foreach (var m in moves)
            {
                int score = 0;
                if (!m.capturedPiece.IsEmpty)
                {
                    score += m.capturedPiece.MaterialValue * 10;
                }

                // Điểm kiểm soát trung tâm
                if (m.toC >= 3 && m.toC <= 5) score += 2;
                if (m.toR <= 4) score += 1; // Tiến sâu vào sân đối phương

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = m;
                }
            }

            ExecuteMove(bestMove);
        }

        // =====================================================================
        // GIAO DIỆN SCENE ĐÁNH CỜ TƯỚNG (FULL ONGUI)
        // =====================================================================
        private void EnsureWhiteTex()
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }
        }

        private void OnGUI()
        {
            if (!isSceneOpen)
                return;

            EnsureWhiteTex();

            float sw = Screen.width;
            float sh = Screen.height;

            // Nền tối mờ mờ toàn màn hình
            DrawSolidRect(new Rect(0, 0, sw, sh), new Color(0.06f, 0.08f, 0.12f, 0.97f));

            // Kích thước Bàn Cờ Tướng 9 cột x 10 hàng
            float boardAreaH = sh - 110f;
            float ptSpacingY = boardAreaH / 10.2f;
            float ptSpacingX = ptSpacingY * 1.02f;
            float boardW = ptSpacingX * 8f;
            float boardH = ptSpacingY * 9f;

            float boardCenterX = sw * 0.5f;
            float boardCenterY = sh * 0.5f + 16f;
            float boardLeft = boardCenterX - boardW * 0.5f;
            float boardTop = boardCenterY - boardH * 0.5f;

            // Thanh Tiêu Đề Top Bar
            DrawTopBar(sw);

            // Bảng điều khiển Trái: Thông tin Phe Đỏ (Người chơi)
            Rect leftPanelRect = new Rect(24f, 72f, Mathf.Min(300f, boardLeft - 36f), sh - 96f);
            DrawPlayerPanel(leftPanelRect, XiangqiColor.Red, "Kỳ Thủ 1 (Bạn)", redTimeRemaining, capturedByRed);

            // Bảng điều khiển Phải: Thông tin Phe Đen + Biên Bản Ký Hiệu FIDE
            Rect rightPanelRect = new Rect(boardLeft + boardW + 24f, 72f, Mathf.Min(320f, sw - (boardLeft + boardW + 48f)), sh - 96f);
            DrawOpponentAndHistoryPanel(rightPanelRect, XiangqiColor.Black, playAgainstAI ? "Kỳ Thủ AI Công Viên" : "Kỳ Thủ 2 (Công Viên)", blackTimeRemaining, capturedByBlack);

            // Vẽ Bàn Cờ Tướng & Quân Cờ ở giữa
            DrawXiangqiBoard(boardLeft, boardTop, ptSpacingX, ptSpacingY, boardW, boardH);

            // Xử lý Click trên bàn cờ
            HandleBoardClick(boardLeft, boardTop, ptSpacingX, ptSpacingY);

            // Banner Kết thúc ván cờ
            if (isGameOver)
            {
                DrawGameOverBanner(boardCenterX, boardCenterY);
            }
        }

        private void DrawTopBar(float sw)
        {
            Rect barRect = new Rect(0, 0, sw, 56f);
            DrawSolidRect(barRect, new Color(0.12f, 0.15f, 0.22f, 0.98f));
            DrawBorderRect(barRect, new Color(0.85f, 0.70f, 0.35f), 1.5f);

            GUIStyle titleSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.88f, 0.42f) }
            };
            GUI.Label(new Rect(28f, 10f, 420f, 36f), "🀄 BÀN CỜ TƯỚNG CÔNG VIÊN — CHUẨN QUỐC TẾ", titleSt);

            // Nút Bật/Tắt AI
            GUIStyle btnSt = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold };
            string aiText = playAgainstAI ? "🤖 Đấu Với Bạn Bè" : "🤖 Đấu Với AI";
            if (GUI.Button(new Rect(sw - 470f, 12f, 140f, 32f), aiText, btnSt))
            {
                playAgainstAI = !playAgainstAI;
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            }

            // Nút Xoay Bàn Cờ
            if (GUI.Button(new Rect(sw - 320f, 12f, 140f, 32f), "🔄 Xoay Bàn Cờ", btnSt))
            {
                flipBoardPerspective = !flipBoardPerspective;
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            }

            // Nút Rời Bàn
            if (GUI.Button(new Rect(sw - 170f, 12f, 140f, 32f), "🚪 Đứng Dậy Rời Bàn", btnSt))
            {
                ExitXiangqiScene();
            }
        }

        private void DrawPlayerPanel(Rect rect, XiangqiColor side, string title, float timeRem, List<XiangqiPiece> captured)
        {
            DrawSolidRect(rect, new Color(0.10f, 0.13f, 0.18f, 0.92f));
            DrawBorderRect(rect, (currentTurn == side) ? new Color(1f, 0.40f, 0.30f) : new Color(0.30f, 0.35f, 0.45f), (currentTurn == side) ? 2.5f : 1f);

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f));

            GUIStyle nameSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = (side == XiangqiColor.Red) ? new Color(1f, 0.35f, 0.30f) : new Color(0.55f, 0.80f, 1f) }
            };
            GUILayout.Label($"🔴 {title} (Phe Đỏ)", nameSt);

            // Đồng hồ thời gian
            int mins = Mathf.FloorToInt(timeRem / 60f);
            int secs = Mathf.FloorToInt(timeRem % 60f);
            string timeStr = $"{mins:00}:{secs:00}";
            GUIStyle clockSt = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = (timeRem < 60f) ? Color.red : Color.white }
            };
            GUILayout.Box($"⏱️ {timeStr}", clockSt, GUILayout.Height(36f));

            if (currentTurn == side)
            {
                GUIStyle turnSt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.85f, 0.30f) }
                };
                GUILayout.Label("👉 ĐANG ĐẾN LƯỢT ĐI...", turnSt);
            }

            GUILayout.Space(12f);

            // Danh sách quân cờ đã ăn được
            GUIStyle headerSt = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            int adv = CalculateMaterialAdvantage(side);
            string advStr = adv > 0 ? $" (+{adv})" : "";
            GUILayout.Label($"⚔️ Quân đối phương đã ăn ({captured.Count}){advStr}:", headerSt);

            Rect capBox = GUILayoutUtility.GetRect(rect.width - 24f, 100f);
            DrawSolidRect(capBox, new Color(0.16f, 0.18f, 0.24f, 0.90f));
            DrawBorderRect(capBox, new Color(0.35f, 0.40f, 0.48f), 1f);
            DrawCapturedGrid(capBox, captured);

            GUILayout.Space(12f);
            GUILayout.Label("💡 Hướng dẫn chơi:", headerSt);
            GUIStyle tipSt = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.82f, 0.85f, 0.90f) } };
            GUILayout.Label("• Click quân cờ để xem các nước đi hợp lệ.\n• Chấm xanh lá: Điểm có thể đi.\n• Vòng tròn đỏ: Điểm có thể ăn quân đối phương.\n• Nhớ quy tắc Cản Mã, Cản Tượng và Lộ Mặt Tướng!", tipSt);

            GUILayout.EndArea();
        }

        private void DrawOpponentAndHistoryPanel(Rect rect, XiangqiColor side, string title, float timeRem, List<XiangqiPiece> captured)
        {
            DrawSolidRect(rect, new Color(0.10f, 0.13f, 0.18f, 0.92f));
            DrawBorderRect(rect, (currentTurn == side) ? new Color(0.40f, 0.75f, 1f) : new Color(0.30f, 0.35f, 0.45f), (currentTurn == side) ? 2.5f : 1f);

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f));

            GUIStyle nameSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.60f, 0.85f, 1f) }
            };
            GUILayout.Label($"⚫ {title} (Phe Đen)", nameSt);

            int mins = Mathf.FloorToInt(timeRem / 60f);
            int secs = Mathf.FloorToInt(timeRem % 60f);
            string timeStr = $"{mins:00}:{secs:00}";
            GUIStyle clockSt = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = (timeRem < 60f) ? Color.red : Color.white }
            };
            GUILayout.Box($"⏱️ {timeStr}", clockSt, GUILayout.Height(36f));

            if (currentTurn == side)
            {
                GUIStyle turnSt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.50f, 0.85f, 1f) }
                };
                GUILayout.Label("👉 ĐANG SUY NGHĨ...", turnSt);
            }

            GUILayout.Space(8f);

            GUIStyle headerSt = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            int adv = CalculateMaterialAdvantage(side);
            string advStr = adv > 0 ? $" (+{adv})" : "";
            GUILayout.Label($"⚔️ Quân đối phương đã ăn ({captured.Count}){advStr}:", headerSt);
            Rect capBox = GUILayoutUtility.GetRect(rect.width - 24f, 70f);
            DrawSolidRect(capBox, new Color(0.16f, 0.18f, 0.24f, 0.90f));
            DrawBorderRect(capBox, new Color(0.35f, 0.40f, 0.48f), 1f);
            DrawCapturedGrid(capBox, captured);

            GUILayout.Space(8f);

            // Biên bản nước đi chuẩn Cờ Tướng tiếng Việt
            GUILayout.Label("📜 Biên Bản Nước Đi:", headerSt);
            moveScrollPos = GUILayout.BeginScrollView(moveScrollPos, GUILayout.ExpandHeight(true));
            if (moveNotations.Count == 0)
            {
                GUILayout.Label("Chưa có nước đi nào. Phe Đỏ đi trước!", new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.gray } });
            }
            else
            {
                GUIStyle moveLineSt = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.92f, 0.92f, 0.95f) } };
                for (int i = 0; i < moveNotations.Count; i++)
                {
                    GUILayout.Label(moveNotations[i], moveLineSt);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawCapturedGrid(Rect box, List<XiangqiPiece> list)
        {
            float size = 26f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((box.width - 8f) / (size + 2f)));
            for (int i = 0; i < list.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                Rect itemRect = new Rect(box.x + 4f + c * (size + 2f), box.y + 4f + r * (size + 2f), size, size);
                DrawSingleXiangqiPiece(itemRect, list[i]);
            }
        }

        private int CalculateMaterialAdvantage(XiangqiColor side)
        {
            int redScore = 0;
            int blackScore = 0;
            for (int r = 0; r < 10; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var p = board[r, c];
                    if (p.IsEmpty) continue;
                    if (p.color == XiangqiColor.Red) redScore += p.MaterialValue;
                    else blackScore += p.MaterialValue;
                }
            }
            return (side == XiangqiColor.Red) ? (redScore - blackScore) : (blackScore - redScore);
        }

        // =====================================================================
        // VẼ BÀN CỜ TƯỚNG TRUNG TÂM (LƯỚI 9x10, SÔNG & CUNG CẤM)
        // =====================================================================
        private void DrawXiangqiBoard(float boardLeft, float boardTop, float spX, float spY, float boardW, float boardH)
        {
            float pad = 24f;
            Rect woodTableRect = new Rect(boardLeft - pad, boardTop - pad, boardW + pad * 2f, boardH + pad * 2f);

            // Nền gỗ bàn cờ truyền thống
            DrawSolidRect(woodTableRect, new Color(0.92f, 0.80f, 0.62f));
            DrawBorderRect(woodTableRect, new Color(0.40f, 0.22f, 0.10f), 4f);

            Color gridLineColor = new Color(0.38f, 0.22f, 0.12f, 0.95f);

            // 1. Vẽ 10 đường ngang
            for (int r = 0; r < 10; r++)
            {
                float y = boardTop + r * spY;
                DrawSolidRect(new Rect(boardLeft, y - 0.75f, boardW, 1.5f), gridLineColor);
            }

            // 2. Vẽ 9 đường dọc (các cột trong có khoảng trống Sông giữa hàng 4 và 5)
            for (int c = 0; c < 9; c++)
            {
                float x = boardLeft + c * spX;
                if (c == 0 || c == 8)
                {
                    // Cột biên chạy thẳng từ hàng 0 tới hàng 9
                    DrawSolidRect(new Rect(x - 0.75f, boardTop, 1.5f, boardH), gridLineColor);
                }
                else
                {
                    // Cột giữa bị đứt đoạn tại Sông
                    float riverTopY = boardTop + 4 * spY;
                    float riverBotY = boardTop + 5 * spY;
                    DrawSolidRect(new Rect(x - 0.75f, boardTop, 1.5f, 4 * spY), gridLineColor);
                    DrawSolidRect(new Rect(x - 0.75f, riverBotY, 1.5f, 4 * spY), gridLineColor);
                }
            }

            // 3. Ranh giới Sông (SỞ HÀ — HÁN GIỚI)
            float riverY = boardTop + 4 * spY;
            Rect riverRect = new Rect(boardLeft + 1.5f, riverY + 1.5f, boardW - 3f, spY - 3f);
            DrawSolidRect(riverRect, new Color(0.96f, 0.86f, 0.68f, 0.75f));

            GUIStyle riverSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.45f, 0.25f, 0.15f) }
            };
            GUI.Label(new Rect(boardLeft + 20f, riverY, boardW * 0.4f, spY), "楚 河 (SỞ HÀ)", riverSt);
            GUI.Label(new Rect(boardLeft + boardW * 0.6f - 20f, riverY, boardW * 0.4f, spY), "漢 界 (HÁN GIỚI)", riverSt);

            // 4. Vẽ đường chéo Cung Cấm (Palace)
            DrawPalaceDiagonals(boardLeft, boardTop, spX, spY);

            // 5. Highlight nước đi vừa thực hiện (Last Move)
            if (hasLastMove)
            {
                DrawPointHighlight(boardLeft, boardTop, spX, spY, lastExecutedMove.fromR, lastExecutedMove.fromC, new Color(1f, 0.85f, 0.35f, 0.40f));
                DrawPointHighlight(boardLeft, boardTop, spX, spY, lastExecutedMove.toR, lastExecutedMove.toC, new Color(1f, 0.85f, 0.35f, 0.60f));
            }

            // 6. Highlight điểm đang chọn
            if (selectedPoint.x != -1)
            {
                DrawPointHighlight(boardLeft, boardTop, spX, spY, selectedPoint.x, selectedPoint.y, new Color(0.25f, 0.85f, 0.45f, 0.65f));

                // Vẽ các gợi ý nước đi hợp lệ
                for (int i = 0; i < currentLegalMoves.Count; i++)
                {
                    var m = currentLegalMoves[i];
                    Vector2 screenPos = PointToScreenPos(boardLeft, boardTop, spX, spY, m.toR, m.toC);
                    if (m.capturedPiece.IsEmpty)
                    {
                        // Chấm xanh lá cho điểm trống
                        DrawCircleSolid(screenPos, 7f, new Color(0.18f, 0.80f, 0.35f, 0.85f));
                    }
                    else
                    {
                        // Vòng tròn đỏ cho điểm có quân đối phương (ăn quân)
                        DrawCircleRing(screenPos, 19f, new Color(0.95f, 0.20f, 0.20f, 0.90f), 2.5f);
                    }
                }
            }

            // 7. Báo đỏ nếu Tướng đang bị chiếu
            if (isGeneralInCheck)
            {
                for (int r = 0; r < 10; r++)
                {
                    for (int c = 3; c <= 5; c++)
                    {
                        var p = board[r, c];
                        if (p.type == XiangqiPieceType.General && p.color == currentTurn)
                        {
                            DrawPointHighlight(boardLeft, boardTop, spX, spY, r, c, new Color(1f, 0.15f, 0.15f, 0.60f));
                        }
                    }
                }
            }

            // 8. Vẽ 32 quân cờ trên bàn
            float pieceRadius = Mathf.Min(spX, spY) * 0.46f;
            for (int r = 0; r < 10; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    var p = board[r, c];
                    if (p.IsEmpty) continue;

                    Vector2 pt = PointToScreenPos(boardLeft, boardTop, spX, spY, r, c);
                    Rect pRect = new Rect(pt.x - pieceRadius, pt.y - pieceRadius, pieceRadius * 2f, pieceRadius * 2f);
                    DrawSingleXiangqiPiece(pRect, p);
                }
            }
        }

        private void DrawPalaceDiagonals(float bLeft, float bTop, float spX, float spY)
        {
            // Vẽ 2 đường chéo Cung đáy (hàng 0..2)
            Vector2 p00 = PointToScreenPos(bLeft, bTop, spX, spY, 0, 3);
            Vector2 p02 = PointToScreenPos(bLeft, bTop, spX, spY, 2, 5);
            Vector2 p03 = PointToScreenPos(bLeft, bTop, spX, spY, 0, 5);
            Vector2 p04 = PointToScreenPos(bLeft, bTop, spX, spY, 2, 3);
            DrawAngledLine(p00, p02, new Color(0.38f, 0.22f, 0.12f, 0.80f), 1.5f);
            DrawAngledLine(p03, p04, new Color(0.38f, 0.22f, 0.12f, 0.80f), 1.5f);

            // Vẽ 2 đường chéo Cung trên (hàng 7..9)
            Vector2 p10 = PointToScreenPos(bLeft, bTop, spX, spY, 7, 3);
            Vector2 p12 = PointToScreenPos(bLeft, bTop, spX, spY, 9, 5);
            Vector2 p13 = PointToScreenPos(bLeft, bTop, spX, spY, 7, 5);
            Vector2 p14 = PointToScreenPos(bLeft, bTop, spX, spY, 9, 3);
            DrawAngledLine(p10, p12, new Color(0.38f, 0.22f, 0.12f, 0.80f), 1.5f);
            DrawAngledLine(p13, p14, new Color(0.38f, 0.22f, 0.12f, 0.80f), 1.5f);
        }

        private void DrawAngledLine(Vector2 pA, Vector2 pB, Color col, float thick)
        {
            Vector2 d = pB - pA;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float length = d.magnitude;

            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pA);
            DrawSolidRect(new Rect(pA.x, pA.y - thick * 0.5f, length, thick), col);
            GUI.matrix = prev;
        }

        private Vector2 PointToScreenPos(float bLeft, float bTop, float spX, float spY, int r, int c)
        {
            int visualR = flipBoardPerspective ? r : (9 - r);
            int visualC = flipBoardPerspective ? (8 - c) : c;
            return new Vector2(bLeft + visualC * spX, bTop + visualR * spY);
        }

        private void ScreenPosToBoardPoint(float bLeft, float bTop, float spX, float spY, Vector2 mouse, out int r, out int c)
        {
            float relX = (mouse.x - bLeft) / spX;
            float relY = (mouse.y - bTop) / spY;

            int vc = Mathf.RoundToInt(relX);
            int vr = Mathf.RoundToInt(relY);

            if (vc >= 0 && vc < 9 && vr >= 0 && vr < 10)
            {
                c = flipBoardPerspective ? (8 - vc) : vc;
                r = flipBoardPerspective ? vr : (9 - vr);
            }
            else
            {
                r = -1; c = -1;
            }
        }

        private void DrawPointHighlight(float bLeft, float bTop, float spX, float spY, int r, int c, Color col)
        {
            Vector2 pt = PointToScreenPos(bLeft, bTop, spX, spY, r, c);
            float sz = Mathf.Min(spX, spY) * 0.85f;
            DrawSolidRect(new Rect(pt.x - sz * 0.5f, pt.y - sz * 0.5f, sz, sz), col);
        }

        private void DrawCircleSolid(Vector2 center, float radius, Color col)
        {
            DrawSolidRect(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), col);
        }

        private void DrawCircleRing(Vector2 center, float radius, Color col, float thickness)
        {
            Rect r = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            DrawBorderRect(r, col, thickness);
        }

        private void DrawSingleXiangqiPiece(Rect rect, XiangqiPiece piece)
        {
            if (piece.IsEmpty) return;

            string colorKey = (piece.color == XiangqiColor.Red) ? "r" : "b";
            string typeKey = piece.type.ToString().ToLower();
            string fullKey = $"{colorKey}_{typeKey}";

            if (useCustomPieceSprites && pieceTextures.TryGetValue(fullKey, out Texture2D tex) && tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback nếu không có sprite: Vẽ thẻ cờ gỗ với chữ Hán & phụ đề
                DrawProceduralXiangqiToken(rect, piece);
            }
        }

        private void DrawProceduralXiangqiToken(Rect rect, XiangqiPiece piece)
        {
            bool isRed = (piece.color == XiangqiColor.Red);
            Color woodBg = isRed ? new Color(0.98f, 0.94f, 0.86f) : new Color(0.91f, 0.88f, 0.82f);
            Color rimCol = isRed ? new Color(0.80f, 0.20f, 0.15f) : new Color(0.25f, 0.25f, 0.28f);

            DrawSolidRect(rect, woodBg);
            DrawBorderRect(rect, rimCol, 2f);

            GUIStyle hzSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(rect.height * 0.48f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = rimCol }
            };
            GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, rect.height * 0.65f), piece.HanziName, hzSt);

            GUIStyle subSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(8, Mathf.RoundToInt(rect.height * 0.20f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = rimCol }
            };
            GUI.Label(new Rect(rect.x, rect.yMax - rect.height * 0.35f, rect.width, rect.height * 0.32f), piece.VietnameseName, subSt);
        }

        // =====================================================================
        // XỬ LÝ CLICK TƯƠNG TÁC
        // =====================================================================
        private void HandleBoardClick(float bLeft, float bTop, float spX, float spY)
        {
            if (isGameOver)
                return;

            if (playAgainstAI && currentTurn == XiangqiColor.Black)
                return;

            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0)
                return;

            ScreenPosToBoardPoint(bLeft, bTop, spX, spY, e.mousePosition, out int clickR, out int clickC);
            if (clickR == -1 || clickC == -1)
                return;

            // Nếu đang chọn 1 quân cờ và click vào 1 trong các ô nước đi hợp lệ
            if (selectedPoint.x != -1)
            {
                for (int i = 0; i < currentLegalMoves.Count; i++)
                {
                    var m = currentLegalMoves[i];
                    if (m.toR == clickR && m.toC == clickC)
                    {
                        ExecuteMove(m);
                        e.Use();
                        return;
                    }
                }
            }

            // Chọn quân cờ thuộc phe đang đến lượt
            XiangqiPiece clickedPiece = board[clickR, clickC];
            if (!clickedPiece.IsEmpty && clickedPiece.color == currentTurn)
            {
                selectedPoint = new Vector2Int(clickR, clickC);
                currentLegalMoves.Clear();
                currentLegalMoves.AddRange(GetStrictlyLegalMovesForPiece(clickR, clickC));
                CozyAudioManager.PlaySfx(CozySfxType.UiClick, 0.7f);
                e.Use();
            }
            else
            {
                selectedPoint = new Vector2Int(-1, -1);
                currentLegalMoves.Clear();
            }
        }

        private void DrawGameOverBanner(float centerX, float centerY)
        {
            float w = 480f;
            float h = 156f;
            Rect box = new Rect(centerX - w * 0.5f, centerY - h * 0.5f, w, h);
            DrawSolidRect(box, new Color(0.08f, 0.11f, 0.16f, 0.98f));
            Color borderColor = isCheckmate ? new Color(1f, 0.84f, 0.32f) : (isStalemate ? new Color(0.58f, 0.78f, 0.96f) : new Color(1f, 0.84f, 0.32f));
            DrawBorderRect(box, borderColor, 3f);

            GUIStyle headSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.90f, 0.40f) }
            };
            GUIStyle subSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(box.x + 16f, box.y + 16f, box.width - 32f, 44f), gameOverHeadline, headSt);
            GUI.Label(new Rect(box.x + 16f, box.y + 62f, box.width - 32f, 36f), gameOverSubtext, subSt);

            GUIStyle btnSt = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(box.x + 50f, box.y + 108f, 175f, 34f), "🔄 Đánh Ván Mới", btnSt))
            {
                ResetBoardToStandardXiangqi();
                CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.7f);
            }
            if (GUI.Button(new Rect(box.x + 255f, box.y + 108f, 175f, 34f), "🚪 Đứng Dậy Rời Bàn", btnSt))
            {
                ExitXiangqiScene();
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
