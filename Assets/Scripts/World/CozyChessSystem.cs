using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    public enum ChessPieceType
    {
        None = 0,
        Pawn = 1,
        Knight = 2,
        Bishop = 3,
        Rook = 4,
        Queen = 5,
        King = 6
    }

    public enum ChessColor
    {
        White = 0,
        Black = 1
    }

    [Serializable]
    public struct ChessPiece
    {
        public ChessPieceType type;
        public ChessColor color;
        public bool hasMoved;

        public bool IsEmpty => type == ChessPieceType.None;

        public int MaterialValue => type switch
        {
            ChessPieceType.Pawn => 1,
            ChessPieceType.Knight => 3,
            ChessPieceType.Bishop => 3,
            ChessPieceType.Rook => 5,
            ChessPieceType.Queen => 9,
            _ => 0
        };
    }

    public struct ChessMove
    {
        public int fromR, fromC;
        public int toR, toC;
        public ChessPiece movedPiece;
        public ChessPiece capturedPiece;
        public bool isEnPassant;
        public bool isCastlingKingside;
        public bool isCastlingQueenside;
        public bool isPromotion;
        public ChessPieceType promotedTo;
        public string algebraicNotation;
    }

    public struct ChessBoardSnapshot
    {
        public ChessPiece[,] board;
        public ChessColor turn;
        public Vector2Int enPassantTarget;
        public int halfMoveClock;
        public int fullMoveNumber;
        public List<ChessPiece> capturedByWhite;
        public List<ChessPiece> capturedByBlack;
        public List<string> moveNotations;
        public ChessMove lastMove;
        public bool hasLastMove;
    }

    /// <summary>
    /// Bàn Cờ Vua Công Viên gồm 1 bàn cờ và 2 chiếc ghế.
    /// Khi người chơi ngồi vào ghế, kỳ thủ thứ 2 sẽ cùng ngồi vào ghế đối diện;
    /// Khi cả 2 người đều ngồi vào bàn thì đếm ngược 3 giây rồi chuyển sang Scene Đánh Cờ Vua FIDE.
    /// </summary>
    public class ParkChessTableInteractable : WorldInteractableBase
    {
        public static ParkChessTableInteractable ActiveSeatedTable { get; private set; }

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
        public CharacterAppearanceData PartnerAppearance => partnerAppearance;

        public void ConfigureParkChessTable(Transform parentGroup)
        {
            interactTitle = "Bàn Cờ Vua Công Viên";
            partnerStandPos = transform.position + new Vector3(0.72f, 0.36f, 0f);

            // Tạo nhân vật người chơi thứ 2 / Kỳ thủ Công Viên đứng cạnh bàn cờ sẵn sàng đối kháng
            partnerNpcObj = new GameObject("ParkChess_SecondPlayer");
            if (parentGroup != null)
                partnerNpcObj.transform.SetParent(parentGroup);
            partnerNpcObj.transform.position = partnerStandPos;

            partnerSr = partnerNpcObj.AddComponent<SpriteRenderer>();
            partnerAppearance = new CharacterAppearanceData
            {
                characterName = "Kỳ Thủ Hoàng",
                gender = GenderType.Male,
                skinToneIndex = 0,
                hairStyleIndex = 4, // Tóc rẽ ngôi Hàn Quốc
                hairColorIndex = 0,
                expressionIndex = 2,
                shirtIndex = 1,     // Áo sơ mi lịch lãm
                shirtColorIndex = 3,
                pantsIndex = 5,
                pantsColorIndex = 0,
                accessoryIndex = 1, // Kính trí thức
                shoesIndex = 1
            };
            partnerSr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(partnerAppearance, 0);
            partnerSr.flipX = true;
            partnerNpcObj.AddComponent<IsometricSorting>().Configure(true, 2);
        }

        private void Update()
        {
            if (!isPlayer1Seated)
            {
                // Đảm bảo người thứ 2 đứng ở vị trí chờ khi bàn trống
                if (partnerNpcObj != null && !CozyChessManager.IsChessSceneActive)
                {
                    partnerNpcObj.transform.position = Vector3.MoveTowards(
                        partnerNpcObj.transform.position, partnerStandPos, 1.8f * Time.deltaTime);
                    if (partnerSr != null) partnerSr.flipX = true;
                }
                return;
            }

            if (CozyChessManager.IsChessSceneActive)
                return;

            // Giữ vị trí người chơi 1 ngồi trên ghế trái
            if (seatedPlayerController != null)
            {
                seatedPlayerController.transform.position = Seat1WorldPos;
                var rb = seatedPlayerController.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = Seat1WorldPos;
                var sr = seatedPlayerController.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = false; // Nhìn sang phải về phía bàn cờ
            }

            // Người chơi có thể bấm phím di chuyển hoặc ESC để đứng dậy trước khi vào trận
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame ||
                kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
            {
                StandUpFromChessTable();
                CozyLifeHUD.PushToast("🚶 Đã đứng dậy khỏi bàn cờ vua.");
                return;
            }

            // Người thứ 2 bước vào ghế phải (Seat 2)
            if (!isPlayer2Seated)
            {
                seat2WalkProgress += Time.deltaTime * 2.2f;
                if (partnerNpcObj != null)
                {
                    partnerNpcObj.transform.position = Vector3.Lerp(partnerStandPos, Seat2WorldPos, Mathf.Clamp01(seat2WalkProgress));
                    if (partnerSr != null) partnerSr.flipX = true; // Nhìn sang trái về phía bàn cờ
                }

                if (seat2WalkProgress >= 1.0f)
                {
                    isPlayer2Seated = true;
                    countdownTimer = 3.0f;
                    lastCountdownBeepSec = 4;
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                    CozyLifeHUD.PushToast("♟️ Cả 2 người đã ngồi vào bàn! Bắt đầu ván cờ sau 3 giây...");
                }
            }
            else if (!matchTriggered)
            {
                // Khi cả 2 người đều đã ngồi vào bàn -> Đếm ngược đúng 3 giây rồi chuyển sang Scene Đánh Cờ
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
                        CozyLifeHUD.Instance.PlaySceneTransition("♟️ BÀN CỜ VUA CÔNG VIÊN — CHUẨN FIDE", () =>
                        {
                            CozyChessManager.StartChessMatch(this);
                        });
                    }
                    else
                    {
                        CozyChessManager.StartChessMatch(this);
                    }
                }
            }
        }

        public override string GetWorldOverheadLabel()
        {
            if (isPlayer1Seated && isPlayer2Seated && !matchTriggered)
            {
                int sec = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 3);
                return $"♟️ Vào Bàn Cờ sau {sec}s...";
            }
            if (isPlayer1Seated && !isPlayer2Seated)
            {
                return "🪑 Đang chờ người thứ 2 ngồi...";
            }
            return "♟️ Bàn Cờ Vua [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            if (isPlayer1Seated)
            {
                int sec = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 3);
                return isPlayer2Seated
                    ? $"♟️ Đủ 2 người ngồi! Chuyển sang đánh cờ sau {sec}s... ([WASD/ESC]: Đứng dậy)"
                    : "🪑 Đang chờ người chơi thứ 2 ngồi vào ghế đối diện...";
            }
            return "[E] Ngồi vào Bàn Cờ Vua Công Viên (2 Ghế)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (player == null) return;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            if (isPlayer1Seated)
            {
                StandUpFromChessTable();
                return;
            }

            // Ngồi vào Ghế 1 (bên trái bàn cờ)
            seatedPlayerController = pc;
            ActiveSeatedTable = this;
            isPlayer1Seated = true;
            isPlayer2Seated = false;
            seat2WalkProgress = 0f;
            countdownTimer = 3.0f;
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
            CozyLifeHUD.PushToast("🪑 Bạn đã ngồi vào ghế Trắng. Kỳ thủ đối diện đang ngồi vào ghế Đen...");
        }

        public void StandUpFromChessTable()
        {
            isPlayer1Seated = false;
            isPlayer2Seated = false;
            matchTriggered = false;
            if (ActiveSeatedTable == this)
                ActiveSeatedTable = null;

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
            }
        }
    }

    // =========================================================================
    // TRÌNH QUẢN LÝ & SCENE ĐÁNH CỜ VUA CHUẨN LUẬT QUỐC TẾ FIDE
    // =========================================================================
    public class CozyChessManager : MonoBehaviour
    {
        public static CozyChessManager Instance { get; private set; }
        public static bool IsChessSceneActive => Instance != null && Instance.isMatchActive;

        private bool isMatchActive = false;
        private ParkChessTableInteractable activeTable;

        // Trạng thái Bàn cờ 8x8: board[row, col]
        // row = 0..7 (tương ứng hạng 1..8, White xuất phát ở row 0,1; Black ở row 6,7)
        // col = 0..7 (tương ứng cột a..h)
        private ChessPiece[,] board = new ChessPiece[8, 8];
        private ChessColor currentTurn = ChessColor.White;
        private Vector2Int enPassantTarget = new Vector2Int(-1, -1); // Ô đích bắt tốt qua đường
        private int halfMoveClock = 0;
        private int fullMoveNumber = 1;

        // Lịch sử ăn quân & biên bản
        private readonly List<ChessPiece> capturedByWhite = new List<ChessPiece>(); // Các quân Đen bị Trắng ăn
        private readonly List<ChessPiece> capturedByBlack = new List<ChessPiece>(); // Các quân Trắng bị Đen ăn
        private readonly List<string> moveNotations = new List<string>();
        private readonly Stack<ChessBoardSnapshot> undoStack = new Stack<ChessBoardSnapshot>();

        // Tương tác chọn ô & nước đi hợp lệ
        private Vector2Int selectedSquare = new Vector2Int(-1, -1);
        private readonly List<ChessMove> currentSquareLegalMoves = new List<ChessMove>();
        private ChessMove lastExecutedMove;
        private bool hasLastMove = false;

        // Hộp thoại Phong cấp Tốt (Pawn Promotion)
        private bool awaitingPromotion = false;
        private ChessMove pendingPromotionBaseMove;

        // Kết quả ván cờ
        private bool isGameOver = false;
        private bool isCheckmate = false;
        private bool isStalemateOrDraw = false;
        private string gameOverHeadline = "";
        private string gameOverSubtext = "";
        private bool isKingCurrentlyInCheck = false;

        // Chế độ chơi: Mặc định 2 Người Cùng Đánh (Pass & Play) theo đúng yêu cầu "để cả 2 cùng nhau đánh cờ"
        // Có nút bật/tắt AI hỗ trợ bất kỳ lúc nào
        private bool playAgainstAI = false;
        private float aiThinkTimer = 0f;
        private bool flipBoardPerspective = false;
        private bool useCustomPieceSprites = true; // Có thể chuyển đổi giữa Sprite và Ký hiệu

        // Thời gian suy nghĩ mỗi bên (15 phút chuẩn Rapid FIDE)
        private float whiteTimeRemaining = 900f;
        private float blackTimeRemaining = 900f;

        // Cache Texture & Sprites
        private Texture2D whiteTex;
        private readonly Dictionary<string, Texture2D> pieceTextures = new Dictionary<string, Texture2D>();
        private Texture2D player1PortraitTex;
        private Texture2D player2PortraitTex;
        private Vector2 moveScrollPos;

        public static void StartChessMatch(ParkChessTableInteractable table)
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("CozyChessManager");
                Instance = go.AddComponent<CozyChessManager>();
                DontDestroyOnLoad(go);
            }
            Instance.BeginMatch(table);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            whiteTex = new Texture2D(2, 2);
            whiteTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            whiteTex.Apply();
            LoadPieceSprites();
        }

        private void LoadPieceSprites()
        {
            string[] names = { "king", "queen", "rook", "bishop", "knight", "pawn" };
            foreach (var n in names)
            {
                Texture2D tw = Resources.Load<Texture2D>($"CozyLifeArt/Chess/piece_w_{n}");
                if (tw != null) pieceTextures[$"w_{n}"] = tw;

                Texture2D tb = Resources.Load<Texture2D>($"CozyLifeArt/Chess/piece_b_{n}");
                if (tb != null) pieceTextures[$"b_{n}"] = tb;
            }
        }

        private Texture2D GetPieceTexture(ChessPiece p)
        {
            if (p.IsEmpty) return null;
            string prefix = (p.color == ChessColor.White) ? "w_" : "b_";
            string name = p.type switch
            {
                ChessPieceType.King => "king",
                ChessPieceType.Queen => "queen",
                ChessPieceType.Rook => "rook",
                ChessPieceType.Bishop => "bishop",
                ChessPieceType.Knight => "knight",
                ChessPieceType.Pawn => "pawn",
                _ => "pawn"
            };
            pieceTextures.TryGetValue(prefix + name, out Texture2D tex);
            return tex;
        }

        public void BeginMatch(ParkChessTableInteractable table)
        {
            activeTable = table;
            isMatchActive = true;
            playAgainstAI = false; // Mặc định 2 người cùng nhau đánh cờ

            // Chuẩn bị ảnh đại diện 2 kỳ thủ
            var profInv = FindAnyObjectByType<ProfessionAndInventory>();
            if (profInv != null && profInv.Appearance != null)
            {
                Sprite s1 = CharacterAppearanceCompositor.GetOrCreateCustomSprite(profInv.Appearance, 0);
                if (s1 != null) player1PortraitTex = s1.texture;
            }
            if (table != null && table.PartnerAppearance != null)
            {
                Sprite s2 = CharacterAppearanceCompositor.GetOrCreateCustomSprite(table.PartnerAppearance, 0);
                if (s2 != null) player2PortraitTex = s2.texture;
            }

            ResetBoardToStandardFIDE();
        }

        public void ExitChessScene()
        {
            isMatchActive = false;
            awaitingPromotion = false;
            if (activeTable != null)
            {
                activeTable.StandUpFromChessTable();
                activeTable = null;
            }
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
        }

        // =====================================================================
        // KHỞI TẠO BÀN CỜ VUA TIÊU CHUẨN FIDE
        // =====================================================================
        public void ResetBoardToStandardFIDE()
        {
            board = new ChessPiece[8, 8];
            currentTurn = ChessColor.White;
            enPassantTarget = new Vector2Int(-1, -1);
            halfMoveClock = 0;
            fullMoveNumber = 1;
            capturedByWhite.Clear();
            capturedByBlack.Clear();
            moveNotations.Clear();
            undoStack.Clear();
            selectedSquare = new Vector2Int(-1, -1);
            currentSquareLegalMoves.Clear();
            hasLastMove = false;
            awaitingPromotion = false;
            isGameOver = false;
            isCheckmate = false;
            isStalemateOrDraw = false;
            gameOverHeadline = "";
            gameOverSubtext = "";
            isKingCurrentlyInCheck = false;
            whiteTimeRemaining = 900f;
            blackTimeRemaining = 900f;

            ChessPieceType[] backRank = {
                ChessPieceType.Rook, ChessPieceType.Knight, ChessPieceType.Bishop, ChessPieceType.Queen,
                ChessPieceType.King, ChessPieceType.Bishop, ChessPieceType.Knight, ChessPieceType.Rook
            };

            for (int c = 0; c < 8; c++)
            {
                board[0, c] = new ChessPiece { type = backRank[c], color = ChessColor.White, hasMoved = false };
                board[1, c] = new ChessPiece { type = ChessPieceType.Pawn, color = ChessColor.White, hasMoved = false };

                board[6, c] = new ChessPiece { type = ChessPieceType.Pawn, color = ChessColor.Black, hasMoved = false };
                board[7, c] = new ChessPiece { type = backRank[c], color = ChessColor.Black, hasMoved = false };
            }
        }

        private void Update()
        {
            if (!isMatchActive) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (awaitingPromotion)
                {
                    awaitingPromotion = false;
                }
                else
                {
                    ExitChessScene();
                }
                return;
            }

            if (!isGameOver && !awaitingPromotion)
            {
                if (currentTurn == ChessColor.White)
                {
                    whiteTimeRemaining = Mathf.Max(0f, whiteTimeRemaining - Time.deltaTime);
                    if (whiteTimeRemaining <= 0f)
                    {
                        isGameOver = true;
                        gameOverHeadline = "⏰ HẾT GIỜ! QUÂN ĐEN THẮNG!";
                        gameOverSubtext = "Quân Trắng đã hết thời gian suy nghĩ.";
                    }
                }
                else
                {
                    blackTimeRemaining = Mathf.Max(0f, blackTimeRemaining - Time.deltaTime);
                    if (blackTimeRemaining <= 0f)
                    {
                        isGameOver = true;
                        gameOverHeadline = "⏰ HẾT GIỜ! QUÂN TRẮNG THẮNG!";
                        gameOverSubtext = "Quân Đen đã hết thời gian suy nghĩ.";
                    }
                }

                // Nếu người chơi bật chế độ Đấu với Máy (AI cầm quân Đen)
                if (playAgainstAI && currentTurn == ChessColor.Black && !isGameOver)
                {
                    aiThinkTimer += Time.deltaTime;
                    if (aiThinkTimer >= 0.55f)
                    {
                        aiThinkTimer = 0f;
                        ExecuteSmartAIMove();
                    }
                }
            }
        }

        // =====================================================================
        // BỘ MÁY LUẬT CỜ VUA QUỐC TẾ FIDE (FIDE RULES ENGINE)
        // =====================================================================

        private static bool InBounds(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;
        private static ChessColor Opponent(ChessColor col) => col == ChessColor.White ? ChessColor.Black : ChessColor.White;

        private static ChessPiece[,] CloneBoard(ChessPiece[,] src)
        {
            ChessPiece[,] copy = new ChessPiece[8, 8];
            Array.Copy(src, copy, src.Length);
            return copy;
        }

        private static Vector2Int FindKing(ChessPiece[,] b, ChessColor color)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (b[r, c].type == ChessPieceType.King && b[r, c].color == color)
                        return new Vector2Int(r, c);
                }
            }
            return new Vector2Int(-1, -1);
        }

        /// <summary>
        /// Kiểm tra ô (targetR, targetC) trên bàn cờ b có đang bị bất kỳ quân nào của byColor tấn công hay không.
        /// </summary>
        public static bool IsSquareAttacked(ChessPiece[,] b, int targetR, int targetC, ChessColor byColor)
        {
            // 1. Tấn công bởi Tốt (Pawn)
            int pawnSourceRow = (byColor == ChessColor.White) ? targetR - 1 : targetR + 1;
            if (InBounds(pawnSourceRow, targetC - 1))
            {
                var p = b[pawnSourceRow, targetC - 1];
                if (p.type == ChessPieceType.Pawn && p.color == byColor) return true;
            }
            if (InBounds(pawnSourceRow, targetC + 1))
            {
                var p = b[pawnSourceRow, targetC + 1];
                if (p.type == ChessPieceType.Pawn && p.color == byColor) return true;
            }

            // 2. Tấn công bởi Mã (Knight)
            int[] kr = { -2, -2, -1, -1, 1, 1, 2, 2 };
            int[] kc = { -1, 1, -2, 2, -2, 2, -1, 1 };
            for (int i = 0; i < 8; i++)
            {
                int nr = targetR + kr[i];
                int nc = targetC + kc[i];
                if (InBounds(nr, nc))
                {
                    var p = b[nr, nc];
                    if (p.type == ChessPieceType.Knight && p.color == byColor) return true;
                }
            }

            // 3. Tấn công bởi Vua đối phương (King - 8 ô xung quanh)
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = targetR + dr;
                    int nc = targetC + dc;
                    if (InBounds(nr, nc))
                    {
                        var p = b[nr, nc];
                        if (p.type == ChessPieceType.King && p.color == byColor) return true;
                    }
                }
            }

            // 4. Tấn công theo phương ngang/dọc bởi Xe (Rook) hoặc Hậu (Queen)
            int[] orthoR = { -1, 1, 0, 0 };
            int[] orthoC = { 0, 0, -1, 1 };
            for (int d = 0; d < 4; d++)
            {
                int nr = targetR + orthoR[d];
                int nc = targetC + orthoC[d];
                while (InBounds(nr, nc))
                {
                    var p = b[nr, nc];
                    if (!p.IsEmpty)
                    {
                        if (p.color == byColor && (p.type == ChessPieceType.Rook || p.type == ChessPieceType.Queen))
                            return true;
                        break;
                    }
                    nr += orthoR[d];
                    nc += orthoC[d];
                }
            }

            // 5. Tấn công theo đường chéo bởi Tượng (Bishop) hoặc Hậu (Queen)
            int[] diagR = { -1, -1, 1, 1 };
            int[] diagC = { -1, 1, -1, 1 };
            for (int d = 0; d < 4; d++)
            {
                int nr = targetR + diagR[d];
                int nc = targetC + diagC[d];
                while (InBounds(nr, nc))
                {
                    var p = b[nr, nc];
                    if (!p.IsEmpty)
                    {
                        if (p.color == byColor && (p.type == ChessPieceType.Bishop || p.type == ChessPieceType.Queen))
                            return true;
                        break;
                    }
                    nr += diagR[d];
                    nc += diagC[d];
                }
            }

            return false;
        }

        public static bool IsKingInCheck(ChessPiece[,] b, ChessColor kingColor)
        {
            Vector2Int kPos = FindKing(b, kingColor);
            if (kPos.x < 0) return false;
            return IsSquareAttacked(b, kPos.x, kPos.y, Opponent(kingColor));
        }

        /// <summary>
        /// Thực thi nước đi trên bản sao bàn cờ để kiểm tra tính hợp lệ (không để Vua bị chiếu).
        /// </summary>
        private static void ApplyMoveOnBoard(ChessPiece[,] b, ChessMove m)
        {
            ChessPiece moving = b[m.fromR, m.fromC];
            moving.hasMoved = true;

            if (m.isPromotion && m.promotedTo != ChessPieceType.None)
            {
                moving.type = m.promotedTo;
            }

            // Xử lý Bắt Tốt Qua Đường (En Passant)
            if (m.isEnPassant)
            {
                b[m.fromR, m.toC] = default; // Xóa quân tốt đối phương ở ngang hàng xuất phát
            }

            // Xử lý Nhập Thành Cánh Vua (O-O)
            if (m.isCastlingKingside)
            {
                ChessPiece rook = b[m.fromR, 7];
                rook.hasMoved = true;
                b[m.fromR, 5] = rook;
                b[m.fromR, 7] = default;
            }
            // Xử lý Nhập Thành Cánh Hậu (O-O-O)
            else if (m.isCastlingQueenside)
            {
                ChessPiece rook = b[m.fromR, 0];
                rook.hasMoved = true;
                b[m.fromR, 3] = rook;
                b[m.fromR, 0] = default;
            }

            b[m.toR, m.toC] = moving;
            b[m.fromR, m.fromC] = default;
        }

        /// <summary>
        /// Sinh toàn bộ các nước đi hợp lệ chuẩn FIDE cho ô (r, c).
        /// </summary>
        public List<ChessMove> GenerateLegalMovesForSquare(ChessPiece[,] b, int r, int c, Vector2Int epTarget)
        {
            List<ChessMove> pseudoMoves = new List<ChessMove>();
            ChessPiece p = b[r, c];
            if (p.IsEmpty) return pseudoMoves;

            ChessColor myColor = p.color;
            ChessColor enemyColor = Opponent(myColor);

            switch (p.type)
            {
                case ChessPieceType.Pawn:
                {
                    int dir = (myColor == ChessColor.White) ? 1 : -1;
                    int startRow = (myColor == ChessColor.White) ? 1 : 6;
                    int promoRow = (myColor == ChessColor.White) ? 7 : 0;

                    // 1. Tiến thẳng 1 ô
                    int nextR = r + dir;
                    if (InBounds(nextR, c) && b[nextR, c].IsEmpty)
                    {
                        bool isPromo = (nextR == promoRow);
                        pseudoMoves.Add(new ChessMove
                        {
                            fromR = r, fromC = c, toR = nextR, toC = c,
                            movedPiece = p,
                            isPromotion = isPromo,
                            promotedTo = isPromo ? ChessPieceType.Queen : ChessPieceType.None
                        });

                        // 2. Tiến thẳng 2 ô từ hàng xuất phát
                        int doubleR = r + dir * 2;
                        if (r == startRow && InBounds(doubleR, c) && b[doubleR, c].IsEmpty)
                        {
                            pseudoMoves.Add(new ChessMove
                            {
                                fromR = r, fromC = c, toR = doubleR, toC = c,
                                movedPiece = p
                            });
                        }
                    }

                    // 3. Ăn chéo 2 bên & Bắt Tốt Qua Đường (En Passant)
                    for (int dc = -1; dc <= 1; dc += 2)
                    {
                        int capC = c + dc;
                        if (!InBounds(nextR, capC)) continue;

                        ChessPiece target = b[nextR, capC];
                        bool isPromo = (nextR == promoRow);

                        if (!target.IsEmpty && target.color == enemyColor)
                        {
                            pseudoMoves.Add(new ChessMove
                            {
                                fromR = r, fromC = c, toR = nextR, toC = capC,
                                movedPiece = p,
                                capturedPiece = target,
                                isPromotion = isPromo,
                                promotedTo = isPromo ? ChessPieceType.Queen : ChessPieceType.None
                            });
                        }
                        else if (epTarget.x == nextR && epTarget.y == capC)
                        {
                            ChessPiece epVictim = b[r, capC];
                            if (epVictim.type == ChessPieceType.Pawn && epVictim.color == enemyColor)
                            {
                                pseudoMoves.Add(new ChessMove
                                {
                                    fromR = r, fromC = c, toR = nextR, toC = capC,
                                    movedPiece = p,
                                    capturedPiece = epVictim,
                                    isEnPassant = true
                                });
                            }
                        }
                    }
                    break;
                }

                case ChessPieceType.Knight:
                {
                    int[] kr = { -2, -2, -1, -1, 1, 1, 2, 2 };
                    int[] kc = { -1, 1, -2, 2, -2, 2, -1, 1 };
                    for (int i = 0; i < 8; i++)
                    {
                        int nr = r + kr[i];
                        int nc = c + kc[i];
                        if (!InBounds(nr, nc)) continue;
                        ChessPiece target = b[nr, nc];
                        if (target.IsEmpty || target.color == enemyColor)
                        {
                            pseudoMoves.Add(new ChessMove
                            {
                                fromR = r, fromC = c, toR = nr, toC = nc,
                                movedPiece = p, capturedPiece = target
                            });
                        }
                    }
                    break;
                }

                case ChessPieceType.Bishop:
                case ChessPieceType.Rook:
                case ChessPieceType.Queen:
                {
                    List<Vector2Int> dirs = new List<Vector2Int>();
                    if (p.type == ChessPieceType.Rook || p.type == ChessPieceType.Queen)
                    {
                        dirs.Add(new Vector2Int(-1, 0));
                        dirs.Add(new Vector2Int(1, 0));
                        dirs.Add(new Vector2Int(0, -1));
                        dirs.Add(new Vector2Int(0, 1));
                    }
                    if (p.type == ChessPieceType.Bishop || p.type == ChessPieceType.Queen)
                    {
                        dirs.Add(new Vector2Int(-1, -1));
                        dirs.Add(new Vector2Int(-1, 1));
                        dirs.Add(new Vector2Int(1, -1));
                        dirs.Add(new Vector2Int(1, 1));
                    }

                    foreach (var d in dirs)
                    {
                        int nr = r + d.x;
                        int nc = c + d.y;
                        while (InBounds(nr, nc))
                        {
                            ChessPiece target = b[nr, nc];
                            if (target.IsEmpty)
                            {
                                pseudoMoves.Add(new ChessMove
                                {
                                    fromR = r, fromC = c, toR = nr, toC = nc,
                                    movedPiece = p
                                });
                            }
                            else
                            {
                                if (target.color == enemyColor)
                                {
                                    pseudoMoves.Add(new ChessMove
                                    {
                                        fromR = r, fromC = c, toR = nr, toC = nc,
                                        movedPiece = p, capturedPiece = target
                                    });
                                }
                                break;
                            }
                            nr += d.x;
                            nc += d.y;
                        }
                    }
                    break;
                }

                case ChessPieceType.King:
                {
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;
                            int nr = r + dr;
                            int nc = c + dc;
                            if (!InBounds(nr, nc)) continue;
                            ChessPiece target = b[nr, nc];
                            if (target.IsEmpty || target.color == enemyColor)
                            {
                                pseudoMoves.Add(new ChessMove
                                {
                                    fromR = r, fromC = c, toR = nr, toC = nc,
                                    movedPiece = p, capturedPiece = target
                                });
                            }
                        }
                    }

                    // Nhập thành chuẩn FIDE (Castling O-O và O-O-O)
                    int homeRow = (myColor == ChessColor.White) ? 0 : 7;
                    if (!p.hasMoved && r == homeRow && c == 4 && !IsKingInCheck(b, myColor))
                    {
                        // 1. Nhập thành cánh Vua (O-O: e1->g1 / e8->g8)
                        ChessPiece rookH = b[homeRow, 7];
                        if (rookH.type == ChessPieceType.Rook && rookH.color == myColor && !rookH.hasMoved)
                        {
                            if (b[homeRow, 5].IsEmpty && b[homeRow, 6].IsEmpty)
                            {
                                if (!IsSquareAttacked(b, homeRow, 5, enemyColor) &&
                                    !IsSquareAttacked(b, homeRow, 6, enemyColor))
                                {
                                    pseudoMoves.Add(new ChessMove
                                    {
                                        fromR = r, fromC = c, toR = homeRow, toC = 6,
                                        movedPiece = p, isCastlingKingside = true
                                    });
                                }
                            }
                        }

                        // 2. Nhập thành cánh Hậu (O-O-O: e1->c1 / e8->c8)
                        ChessPiece rookA = b[homeRow, 0];
                        if (rookA.type == ChessPieceType.Rook && rookA.color == myColor && !rookA.hasMoved)
                        {
                            if (b[homeRow, 1].IsEmpty && b[homeRow, 2].IsEmpty && b[homeRow, 3].IsEmpty)
                            {
                                if (!IsSquareAttacked(b, homeRow, 3, enemyColor) &&
                                    !IsSquareAttacked(b, homeRow, 2, enemyColor))
                                {
                                    pseudoMoves.Add(new ChessMove
                                    {
                                        fromR = r, fromC = c, toR = homeRow, toC = 2,
                                        movedPiece = p, isCastlingQueenside = true
                                    });
                                }
                            }
                        }
                    }
                    break;
                }
            }

            // Lọc chỉ giữ lại các nước đi không làm Vua đội nhà bị chiếu
            List<ChessMove> legalMoves = new List<ChessMove>();
            foreach (var mv in pseudoMoves)
            {
                ChessPiece[,] sim = CloneBoard(b);
                ApplyMoveOnBoard(sim, mv);
                if (!IsKingInCheck(sim, myColor))
                {
                    legalMoves.Add(mv);
                }
            }

            return legalMoves;
        }

        public List<ChessMove> GenerateAllLegalMoves(ChessPiece[,] b, ChessColor side, Vector2Int epTarget)
        {
            List<ChessMove> all = new List<ChessMove>();
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (!b[r, c].IsEmpty && b[r, c].color == side)
                    {
                        all.AddRange(GenerateLegalMovesForSquare(b, r, c, epTarget));
                    }
                }
            }
            return all;
        }

        private bool IsInsufficientMaterial(ChessPiece[,] b)
        {
            List<ChessPiece> minorOrMajor = new List<ChessPiece>();
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = b[r, c];
                    if (p.IsEmpty || p.type == ChessPieceType.King) continue;
                    if (p.type == ChessPieceType.Pawn || p.type == ChessPieceType.Rook || p.type == ChessPieceType.Queen)
                        return false;
                    minorOrMajor.Add(p);
                }
            }
            // K vs K, K+B vs K, K+N vs K
            return minorOrMajor.Count <= 1;
        }

        // =====================================================================
        // THỰC THI NƯỚC ĐI & KIỂM TRA CHIẾU HẾT / HÒA CỜ
        // =====================================================================

        private void SaveSnapshotForUndo()
        {
            undoStack.Push(new ChessBoardSnapshot
            {
                board = CloneBoard(board),
                turn = currentTurn,
                enPassantTarget = enPassantTarget,
                halfMoveClock = halfMoveClock,
                fullMoveNumber = fullMoveNumber,
                capturedByWhite = new List<ChessPiece>(capturedByWhite),
                capturedByBlack = new List<ChessPiece>(capturedByBlack),
                moveNotations = new List<string>(moveNotations),
                lastMove = lastExecutedMove,
                hasLastMove = hasLastMove
            });
        }

        public void UndoLastMove()
        {
            if (undoStack.Count == 0 || awaitingPromotion) return;

            var snap = undoStack.Pop();
            board = snap.board;
            currentTurn = snap.turn;
            enPassantTarget = snap.enPassantTarget;
            halfMoveClock = snap.halfMoveClock;
            fullMoveNumber = snap.fullMoveNumber;
            capturedByWhite.Clear();
            capturedByWhite.AddRange(snap.capturedByWhite);
            capturedByBlack.Clear();
            capturedByBlack.AddRange(snap.capturedByBlack);
            moveNotations.Clear();
            moveNotations.AddRange(snap.moveNotations);
            lastExecutedMove = snap.lastMove;
            hasLastMove = snap.hasLastMove;

            selectedSquare = new Vector2Int(-1, -1);
            currentSquareLegalMoves.Clear();
            isGameOver = false;
            isCheckmate = false;
            isStalemateOrDraw = false;
            isKingCurrentlyInCheck = IsKingInCheck(board, currentTurn);
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
        }

        private void ExecuteMove(ChessMove move)
        {
            SaveSnapshotForUndo();

            // Ghi nhận quân bị ăn
            if (!move.capturedPiece.IsEmpty)
            {
                if (currentTurn == ChessColor.White)
                    capturedByWhite.Add(move.capturedPiece);
                else
                    capturedByBlack.Add(move.capturedPiece);
            }

            // Cập nhật điểm bắt tốt qua đường cho lượt kế tiếp
            if (move.movedPiece.type == ChessPieceType.Pawn && Mathf.Abs(move.toR - move.fromR) == 2)
            {
                enPassantTarget = new Vector2Int((move.fromR + move.toR) / 2, move.fromC);
            }
            else
            {
                enPassantTarget = new Vector2Int(-1, -1);
            }

            // Đồng hồ 50 nước
            if (move.movedPiece.type == ChessPieceType.Pawn || !move.capturedPiece.IsEmpty)
                halfMoveClock = 0;
            else
                halfMoveClock++;

            ApplyMoveOnBoard(board, move);

            ChessColor nextSide = Opponent(currentTurn);
            bool givesCheck = IsKingInCheck(board, nextSide);
            List<ChessMove> nextLegalMoves = GenerateAllLegalMoves(board, nextSide, enPassantTarget);
            bool givesCheckmate = givesCheck && nextLegalMoves.Count == 0;

            // Ký hiệu đại số chuẩn FIDE
            string notation = FormatAlgebraicNotation(move, givesCheck, givesCheckmate);
            if (currentTurn == ChessColor.White)
            {
                moveNotations.Add($"{fullMoveNumber}. {notation}");
            }
            else
            {
                if (moveNotations.Count > 0)
                    moveNotations[moveNotations.Count - 1] += $"   {notation}";
                else
                    moveNotations.Add($"{fullMoveNumber}... {notation}");
                fullMoveNumber++;
            }

            moveScrollPos.y = 99999f;
            lastExecutedMove = move;
            hasLastMove = true;
            currentTurn = nextSide;
            selectedSquare = new Vector2Int(-1, -1);
            currentSquareLegalMoves.Clear();
            isKingCurrentlyInCheck = givesCheck;

            // Âm thanh đặt quân / ăn quân / chiếu
            if (givesCheckmate)
            {
                CozyAudioManager.PlaySfx(CozySfxType.Cheer, 0.95f);
            }
            else if (givesCheck)
            {
                CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert, 0.85f);
            }
            else if (!move.capturedPiece.IsEmpty)
            {
                CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture, 0.95f);
            }
            else
            {
                CozyAudioManager.PlaySfx(CozySfxType.UiClick, 0.85f);
            }

            // Kiểm tra kết thúc ván cờ theo luật FIDE
            if (nextLegalMoves.Count == 0)
            {
                isGameOver = true;
                if (givesCheck)
                {
                    isCheckmate = true;
                    string winner = ( nextSide == ChessColor.Black ) ? "QUÂN TRẮNG (WHITE)" : "QUÂN ĐEN (BLACK)";
                    gameOverHeadline = $"🏆 CHIẾU HẾT! (CHECKMATE) — {winner} CHIẾN THẮNG!";
                    gameOverSubtext = $"Vua {(nextSide == ChessColor.White ? "Trắng" : "Đen")} bị chiếu và không còn nước đi hợp lệ nào.";

                    // Thưởng Xu & Thể lực kỷ niệm ván cờ hay
                    var profInv = FindAnyObjectByType<ProfessionAndInventory>();
                    if (profInv != null) profInv.AddCoins(25);
                    CozyLife.Core.CozyDiarySystem.RecordGameWon();
                }
                else
                {
                    isStalemateOrDraw = true;
                    gameOverHeadline = "🤝 HÒA CỜ! (STALEMATE — HẾT NƯỚC ĐI HỢP LỆ)";
                    gameOverSubtext = "Bên tới lượt không bị chiếu nhưng không còn nước đi hợp lệ nào theo luật FIDE.";
                }
            }
            else if (IsInsufficientMaterial(board))
            {
                isGameOver = true;
                isStalemateOrDraw = true;
                gameOverHeadline = "🤝 HÒA CỜ! (KHÔNG ĐỦ LỰC LƯỢNG CHIẾU HẾT)";
                gameOverSubtext = "Hai bên không còn đủ quân để có thể chiếu hết đối phương theo chuẩn FIDE.";
            }
            else if (halfMoveClock >= 100)
            {
                isGameOver = true;
                isStalemateOrDraw = true;
                gameOverHeadline = "🤝 HÒA CỜ! (LUẬT 50 NƯỚC CỦA FIDE)";
                gameOverSubtext = "Đã qua 50 nước liên tiếp không có quân nào bị ăn và không có Tốt di chuyển.";
            }
        }

        private static string SquareName(int r, int c)
        {
            char file = (char)('a' + c);
            char rank = (char)('1' + r);
            return $"{file}{rank}";
        }

        private static string FormatAlgebraicNotation(ChessMove m, bool check, bool checkmate)
        {
            string suffix = checkmate ? "#" : (check ? "+" : "");
            if (m.isCastlingKingside) return "O-O" + suffix;
            if (m.isCastlingQueenside) return "O-O-O" + suffix;

            string pChar = m.movedPiece.type switch
            {
                ChessPieceType.King => "K",
                ChessPieceType.Queen => "Q",
                ChessPieceType.Rook => "R",
                ChessPieceType.Bishop => "B",
                ChessPieceType.Knight => "N",
                _ => ""
            };

            bool isCapture = !m.capturedPiece.IsEmpty || m.isEnPassant;
            string capStr = isCapture ? "x" : "";
            if (m.movedPiece.type == ChessPieceType.Pawn && isCapture)
            {
                pChar = ((char)('a' + m.fromC)).ToString();
            }

            string promoStr = "";
            if (m.isPromotion && m.promotedTo != ChessPieceType.None)
            {
                string pr = m.promotedTo switch
                {
                    ChessPieceType.Queen => "Q",
                    ChessPieceType.Rook => "R",
                    ChessPieceType.Bishop => "B",
                    ChessPieceType.Knight => "N",
                    _ => "Q"
                };
                promoStr = "=" + pr;
            }

            return $"{pChar}{capStr}{SquareName(m.toR, m.toC)}{promoStr}{suffix}";
        }

        // =====================================================================
        // AI ĐỐI THỦ TÙY CHỌN (KHI BẬT CHẾ ĐỘ ĐẤU VỚI MÁY)
        // =====================================================================
        private void ExecuteSmartAIMove()
        {
            List<ChessMove> allMoves = GenerateAllLegalMoves(board, ChessColor.Black, enPassantTarget);
            if (allMoves.Count == 0) return;

            ChessMove bestMove = allMoves[0];
            int bestScore = int.MinValue;

            foreach (var mv in allMoves)
            {
                int score = UnityEngine.Random.Range(0, 15);
                if (!mv.capturedPiece.IsEmpty)
                {
                    score += mv.capturedPiece.MaterialValue * 100 - mv.movedPiece.MaterialValue * 8;
                }
                if (mv.isPromotion) score += 800;
                if (mv.isCastlingKingside || mv.isCastlingQueenside) score += 60;

                // Ưu tiên kiểm soát trung tâm (d4, d5, e4, e5)
                if (mv.toR >= 2 && mv.toR <= 5 && mv.toC >= 2 && mv.toC <= 5)
                    score += 20;

                ChessPiece[,] sim = CloneBoard(board);
                ApplyMoveOnBoard(sim, mv);
                if (IsKingInCheck(sim, ChessColor.White))
                    score += 45;
                if (IsSquareAttacked(sim, mv.toR, mv.toC, ChessColor.White))
                    score -= mv.movedPiece.MaterialValue * 55;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = mv;
                }
            }

            ExecuteMove(bestMove);
        }

        // =====================================================================
        // XỬ LÝ CLICK TRÊN BÀN CỜ
        // =====================================================================
        private void OnSquareClicked(int r, int c)
        {
            if (isGameOver || awaitingPromotion) return;
            if (playAgainstAI && currentTurn == ChessColor.Black) return;

            // Kiểm tra xem ô vừa click có nằm trong danh sách nước đi hợp lệ của quân đang chọn không
            for (int i = 0; i < currentSquareLegalMoves.Count; i++)
            {
                var mv = currentSquareLegalMoves[i];
                if (mv.toR == r && mv.toC == c)
                {
                    // Nếu là nước Phong cấp Tốt -> Mở hộp thoại chọn quân phong cấp (Hậu, Xe, Tượng, Mã)
                    if (mv.isPromotion)
                    {
                        pendingPromotionBaseMove = mv;
                        awaitingPromotion = true;
                        CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                        return;
                    }

                    ExecuteMove(mv);
                    return;
                }
            }

            // Nếu click vào quân cờ thuộc phe đang tới lượt -> Chọn quân đó và hiển thị nước đi hợp lệ
            ChessPiece clickedPiece = board[r, c];
            if (!clickedPiece.IsEmpty && clickedPiece.color == currentTurn)
            {
                selectedSquare = new Vector2Int(r, c);
                currentSquareLegalMoves.Clear();
                currentSquareLegalMoves.AddRange(GenerateLegalMovesForSquare(board, r, c, enPassantTarget));
                CozyAudioManager.PlaySfx(CozySfxType.UiClick, 0.7f);
            }
            else
            {
                selectedSquare = new Vector2Int(-1, -1);
                currentSquareLegalMoves.Clear();
            }
        }

        // =====================================================================
        // GIAO DIỆN SCENE ĐÁNH CỜ VUA (FULL-SCREEN INTERACTIVE CHESS SCENE)
        // =====================================================================
        private void OnGUI()
        {
            if (!isMatchActive) return;

            GUI.depth = -25;

            // 1. Phông nền không gian Công Viên & Bàn Cờ Gỗ Sang Trọng
            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.11f, 0.15f, 0.98f));

            // Viền trang trí
            Rect outerFrame = new Rect(14f, 12f, Screen.width - 28f, Screen.height - 24f);
            DrawBorderRect(outerFrame, new Color(0.85f, 0.68f, 0.32f, 0.65f), 2f);

            // 2. Thanh Tiêu Đề Trên Cùng (Top Header Bar)
            DrawTopHeaderBar();

            // Tính toán kích thước Bàn cờ 8x8 ở chính giữa màn hình
            float availH = Screen.height - 118f;
            float boardSize = Mathf.Clamp(availH - 24f, 360f, 560f);
            float sqSize = Mathf.Floor(boardSize / 8f);
            boardSize = sqSize * 8f;

            float boardX = (Screen.width - boardSize) * 0.5f;
            float boardY = 74f + (availH - boardSize) * 0.5f;

            // 3. Bảng Thông Tin Kỳ Thủ 1 (Quân Trắng - Bên Trái)
            float sideW = Mathf.Clamp(boardX - 48f, 210f, 300f);
            Rect leftPanel = new Rect(26f, boardY - 12f, sideW, boardSize + 24f);
            DrawPlayerSidePanel(leftPanel, ChessColor.White);

            // 4. Bảng Thông Tin Kỳ Thủ 2 & Biên Bản FIDE (Quân Đen - Bên Phải)
            Rect rightPanel = new Rect(Screen.width - sideW - 26f, boardY - 12f, sideW, boardSize + 24f);
            DrawRightPanelAndNotation(rightPanel);

            // 5. Bàn Cờ Vua 8x8 Chuẩn FIDE ở Trung Tâm
            DrawChessBoard8x8(boardX, boardY, sqSize, boardSize);

            // 6. Hộp thoại Phong Cấp Tốt (Pawn Promotion Modal)
            if (awaitingPromotion)
            {
                DrawPromotionModal(boardX + boardSize * 0.5f, boardY + boardSize * 0.5f);
            }

            // 7. Bảng Thông Báo Kết Quả Ván Đấu (Checkmate / Stalemate)
            if (isGameOver)
            {
                DrawGameOverBanner(boardX + boardSize * 0.5f, boardY + boardSize * 0.5f);
            }
        }

        private void DrawTopHeaderBar()
        {
            Rect topBar = new Rect(26f, 20f, Screen.width - 52f, 44f);
            DrawSolidRect(topBar, new Color(0.13f, 0.16f, 0.22f, 0.96f));
            DrawBorderRect(topBar, new Color(0.90f, 0.72f, 0.34f), 1.5f);

            GUIStyle titleSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.90f, 0.48f) }
            };
            GUI.Label(new Rect(topBar.x + 14f, topBar.y + 8f, 420f, 28f), "♟️ BÀN CỜ VUA CÔNG VIÊN — LUẬT QUỐC TẾ FIDE", titleSt);

            // Hiển thị trạng thái lượt đi hoặc cảnh báo CHIẾU TƯỚNG
            string turnStatus;
            Color statusCol;
            if (isGameOver)
            {
                turnStatus = gameOverHeadline;
                statusCol = new Color(1f, 0.88f, 0.32f);
            }
            else if (isKingCurrentlyInCheck)
            {
                string sideInCheck = (currentTurn == ChessColor.White) ? "TRẮNG" : "ĐEN";
                turnStatus = $"⚡ CHIẾU TƯỚNG! (CHECK!) — Lượt Quân {sideInCheck} phải giải chiếu!";
                statusCol = new Color(1f, 0.35f, 0.35f);
            }
            else
            {
                turnStatus = (currentTurn == ChessColor.White)
                    ? "⚪ LƯỢT ĐI: QUÂN TRẮNG (WHITE)"
                    : "⚫ LƯỢT ĐI: QUÂN ĐEN (BLACK)";
                statusCol = (currentTurn == ChessColor.White)
                    ? new Color(0.98f, 0.96f, 0.88f)
                    : new Color(0.65f, 0.88f, 1.0f);
            }

            GUIStyle turnSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = statusCol }
            };
            GUI.Label(new Rect((Screen.width - 420f) * 0.5f, topBar.y + 8f, 420f, 28f), turnStatus, turnSt);

            // Các nút công cụ bên phải thanh Header
            GUIStyle btnSt = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold };

            string modeLabel = playAgainstAI ? "🤖 Chế độ: Đấu Máy" : "👥 Chế độ: 2 Người Đánh";
            if (GUI.Button(new Rect(topBar.xMax - 430f, topBar.y + 8f, 146f, 28f), modeLabel, btnSt))
            {
                playAgainstAI = !playAgainstAI;
                aiThinkTimer = 0f;
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            }

            string artLabel = useCustomPieceSprites ? "🖼️ Quân: Hình Ảnh" : "♔ Quân: Ký Hiệu";
            if (GUI.Button(new Rect(topBar.xMax - 278f, topBar.y + 8f, 128f, 28f), artLabel, btnSt))
            {
                useCustomPieceSprites = !useCustomPieceSprites;
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            }

            if (GUI.Button(new Rect(topBar.xMax - 144f, topBar.y + 8f, 134f, 28f), "🚪 Đứng Dậy [ESC]", btnSt))
            {
                ExitChessScene();
            }
        }

        private void DrawPlayerSidePanel(Rect rect, ChessColor side)
        {
            bool isActiveTurn = (!isGameOver && currentTurn == side);
            DrawSolidRect(rect, new Color(0.12f, 0.15f, 0.20f, 0.96f));
            DrawBorderRect(rect, isActiveTurn ? new Color(0.35f, 0.95f, 0.55f) : new Color(0.35f, 0.40f, 0.50f), isActiveTurn ? 2.5f : 1.5f);

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f));

            GUIStyle headerSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.90f, 0.48f) }
            };
            GUIStyle subSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.90f, 0.95f) }
            };

            GUILayout.Label("⚪ KỲ THỦ 1 — QUÂN TRẮNG", headerSt);
            GUILayout.Space(4f);

            // Khung Avatar & Tên người chơi
            Rect avBox = GUILayoutUtility.GetRect(rect.width - 24f, 64f);
            DrawSolidRect(avBox, new Color(0.18f, 0.22f, 0.28f, 0.95f));
            DrawBorderRect(avBox, new Color(0.75f, 0.65f, 0.40f), 1f);
            if (player1PortraitTex != null)
            {
                GUI.DrawTexture(new Rect(avBox.x + 6f, avBox.y + 4f, 52f, 56f), player1PortraitTex, ScaleMode.ScaleToFit);
            }
            var profInv = FindAnyObjectByType<ProfessionAndInventory>();
            string p1Name = (profInv != null && profInv.Appearance != null) ? profInv.Appearance.characterName : "Bạn (Người Chơi 1)";
            GUI.Label(new Rect(avBox.x + 64f, avBox.y + 8f, avBox.width - 70f, 22f), p1Name, headerSt);
            GUI.Label(new Rect(avBox.x + 64f, avBox.y + 30f, avBox.width - 70f, 20f), isActiveTurn ? "🟢 Đang tới lượt đi..." : "⏳ Đang chờ đối thủ...", subSt);

            GUILayout.Space(10f);

            // Đồng hồ thi đấu FIDE
            int mins = Mathf.FloorToInt(whiteTimeRemaining / 60f);
            int secs = Mathf.FloorToInt(whiteTimeRemaining % 60f);
            Rect clockRect = GUILayoutUtility.GetRect(rect.width - 24f, 38f);
            DrawSolidRect(clockRect, isActiveTurn ? new Color(0.16f, 0.32f, 0.22f) : new Color(0.14f, 0.16f, 0.20f));
            DrawBorderRect(clockRect, isActiveTurn ? new Color(0.45f, 1f, 0.60f) : new Color(0.4f, 0.4f, 0.5f), 1.5f);
            GUIStyle clockSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(clockRect, $"⏱️ {mins:00}:{secs:00}", clockSt);

            GUILayout.Space(12f);

            // Danh sách quân Đen mà Trắng đã ăn được
            int adv = CalculateMaterialAdvantage(ChessColor.White);
            string advStr = adv > 0 ? $" (+{adv})" : "";
            GUILayout.Label($"⚔️ Quân đã ăn được ({capturedByWhite.Count}){advStr}:", headerSt);
            Rect capBox = GUILayoutUtility.GetRect(rect.width - 24f, 84f);
            DrawSolidRect(capBox, new Color(0.18f, 0.20f, 0.25f, 0.90f));
            DrawBorderRect(capBox, new Color(0.35f, 0.40f, 0.48f), 1f);
            DrawCapturedPiecesGrid(capBox, capturedByWhite);

            GUILayout.Space(12f);

            // Hướng dẫn luật FIDE tích hợp
            GUILayout.Label("📘 Chuẩn Luật Quốc Tế FIDE:", headerSt);
            GUIStyle ruleSt = new GUIStyle(subSt) { fontSize = 10, wordWrap = true };
            GUILayout.Label("• Đầy đủ Nhập thành cánh Vua (O-O) & cánh Hậu (O-O-O).\n• Tự động nhận diện Bắt Tốt Qua Đường (En Passant).\n• Phong cấp Tốt khi tiến tới hàng 8 (Hậu, Xe, Tượng, Mã).\n• Kiểm tra Chiếu Tướng, Ghim quân (Pin), Chiếu Hết & Hòa Cờ.", ruleSt);

            GUILayout.FlexibleSpace();

            // Các nút điều khiển bàn cờ
            GUIStyle btnSt = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold };
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↩ Đi Lại (Undo)", btnSt, GUILayout.Height(28f)))
            {
                UndoLastMove();
            }
            if (GUILayout.Button("🔄 Xoay Bàn", btnSt, GUILayout.Height(28f)))
            {
                flipBoardPerspective = !flipBoardPerspective;
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (GUILayout.Button("🔄 Bắt Đầu Ván Cờ Mới", btnSt, GUILayout.Height(30f)))
            {
                ResetBoardToStandardFIDE();
                CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.7f);
            }

            GUILayout.EndArea();
        }

        private void DrawRightPanelAndNotation(Rect rect)
        {
            bool isActiveTurn = (!isGameOver && currentTurn == ChessColor.Black);
            DrawSolidRect(rect, new Color(0.12f, 0.15f, 0.20f, 0.96f));
            DrawBorderRect(rect, isActiveTurn ? new Color(0.35f, 0.95f, 0.55f) : new Color(0.35f, 0.40f, 0.50f), isActiveTurn ? 2.5f : 1.5f);

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f));

            GUIStyle headerSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.65f, 0.88f, 1f) }
            };
            GUIStyle subSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.90f, 0.95f) }
            };

            GUILayout.Label("⚫ KỲ THỦ 2 — QUÂN ĐEN", headerSt);
            GUILayout.Space(4f);

            Rect avBox = GUILayoutUtility.GetRect(rect.width - 24f, 64f);
            DrawSolidRect(avBox, new Color(0.18f, 0.22f, 0.28f, 0.95f));
            DrawBorderRect(avBox, new Color(0.45f, 0.65f, 0.85f), 1f);
            if (player2PortraitTex != null)
            {
                GUI.DrawTexture(new Rect(avBox.x + 6f, avBox.y + 4f, 52f, 56f), player2PortraitTex, ScaleMode.ScaleToFit);
            }
            string p2Name = playAgainstAI ? "Kỳ Thủ Hoàng (Máy AI)" : "Kỳ Thủ Hoàng (Người Chơi 2)";
            GUI.Label(new Rect(avBox.x + 64f, avBox.y + 8f, avBox.width - 70f, 22f), p2Name, headerSt);
            GUI.Label(new Rect(avBox.x + 64f, avBox.y + 30f, avBox.width - 70f, 20f), isActiveTurn ? "🟢 Đang tới lượt đi..." : "⏳ Đang chờ đối thủ...", subSt);

            GUILayout.Space(10f);

            int mins = Mathf.FloorToInt(blackTimeRemaining / 60f);
            int secs = Mathf.FloorToInt(blackTimeRemaining % 60f);
            Rect clockRect = GUILayoutUtility.GetRect(rect.width - 24f, 38f);
            DrawSolidRect(clockRect, isActiveTurn ? new Color(0.16f, 0.32f, 0.22f) : new Color(0.14f, 0.16f, 0.20f));
            DrawBorderRect(clockRect, isActiveTurn ? new Color(0.45f, 1f, 0.60f) : new Color(0.4f, 0.4f, 0.5f), 1.5f);
            GUIStyle clockSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(clockRect, $"⏱️ {mins:00}:{secs:00}", clockSt);

            GUILayout.Space(10f);

            int adv = CalculateMaterialAdvantage(ChessColor.Black);
            string advStr = adv > 0 ? $" (+{adv})" : "";
            GUILayout.Label($"⚔️ Quân đã ăn được ({capturedByBlack.Count}){advStr}:", headerSt);
            Rect capBox = GUILayoutUtility.GetRect(rect.width - 24f, 70f);
            DrawSolidRect(capBox, new Color(0.18f, 0.20f, 0.25f, 0.90f));
            DrawBorderRect(capBox, new Color(0.35f, 0.40f, 0.48f), 1f);
            DrawCapturedPiecesGrid(capBox, capturedByBlack);

            GUILayout.Space(10f);

            // Biên bản nước đi chuẩn FIDE (Move History)
            GUILayout.Label("📜 Biên Bản Nước Đi (FIDE Notation):", headerSt);
            moveScrollPos = GUILayout.BeginScrollView(moveScrollPos, GUILayout.ExpandHeight(true));
            if (moveNotations.Count == 0)
            {
                GUILayout.Label("Chưa có nước đi nào. Quân Trắng đi trước!", subSt);
            }
            else
            {
                GUIStyle moveLineSt = new GUIStyle(subSt) { fontSize = 12, fontStyle = FontStyle.Bold };
                for (int i = 0; i < moveNotations.Count; i++)
                {
                    GUILayout.Label(moveNotations[i], moveLineSt);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawCapturedPiecesGrid(Rect box, List<ChessPiece> list)
        {
            float size = 26f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((box.width - 8f) / (size + 2f)));
            for (int i = 0; i < list.Count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                Rect itemRect = new Rect(box.x + 4f + c * (size + 2f), box.y + 4f + r * (size + 2f), size, size);
                DrawSingleChessPiece(itemRect, list[i]);
            }
        }

        private int CalculateMaterialAdvantage(ChessColor side)
        {
            int whiteScore = 0;
            int blackScore = 0;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = board[r, c];
                    if (p.IsEmpty) continue;
                    if (p.color == ChessColor.White) whiteScore += p.MaterialValue;
                    else blackScore += p.MaterialValue;
                }
            }
            return (side == ChessColor.White) ? (whiteScore - blackScore) : (blackScore - whiteScore);
        }

        // =====================================================================
        // VẼ BÀN CỜ VUA 8x8 TRUNG TÂM & QUÂN CỜ
        // =====================================================================
        private void DrawChessBoard8x8(float boardX, float boardY, float sqSize, float boardSize)
        {
            // Khung gỗ bàn cờ
            Rect woodFrame = new Rect(boardX - 24f, boardY - 24f, boardSize + 48f, boardSize + 48f);
            DrawSolidRect(woodFrame, new Color(0.26f, 0.16f, 0.09f, 1f));
            DrawBorderRect(woodFrame, new Color(0.88f, 0.70f, 0.34f), 3f);

            Color lightSqColor = new Color(0.93f, 0.85f, 0.72f); // Kem gỗ sáng chuẩn FIDE
            Color darkSqColor = new Color(0.64f, 0.45f, 0.29f);  // Nâu gỗ óc chó chuẩn FIDE

            GUIStyle coordSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.85f, 0.62f) }
            };

            // Vẽ 64 ô cờ
            for (int screenR = 0; screenR < 8; screenR++)
            {
                for (int screenC = 0; screenC < 8; screenC++)
                {
                    int boardR = flipBoardPerspective ? screenR : (7 - screenR);
                    int boardC = flipBoardPerspective ? (7 - screenC) : screenC;

                    Rect sqRect = new Rect(boardX + screenC * sqSize, boardY + screenR * sqSize, sqSize, sqSize);
                    bool isLightSquare = ((boardR + boardC) % 2 != 0);
                    DrawSolidRect(sqRect, isLightSquare ? lightSqColor : darkSqColor);

                    // Tô sáng nước đi vừa thực hiện
                    if (hasLastMove && (( lastExecutedMove.fromR == boardR && lastExecutedMove.fromC == boardC ) ||
                                        ( lastExecutedMove.toR == boardR && lastExecutedMove.toC == boardC )))
                    {
                        DrawSolidRect(sqRect, new Color(0.98f, 0.90f, 0.25f, 0.42f));
                    }

                    // Tô sáng ô đang được chọn
                    if (selectedSquare.x == boardR && selectedSquare.y == boardC)
                    {
                        DrawSolidRect(sqRect, new Color(0.25f, 0.88f, 0.45f, 0.55f));
                        DrawBorderRect(sqRect, new Color(0.15f, 1f, 0.35f), 2.5f);
                    }

                    // Tô đỏ rực ô Vua nếu đang bị CHIẾU TƯỚNG (Check)
                    ChessPiece piece = board[boardR, boardC];
                    if (isKingCurrentlyInCheck && piece.type == ChessPieceType.King && piece.color == currentTurn)
                    {
                        DrawSolidRect(sqRect, new Color(0.95f, 0.18f, 0.18f, 0.68f));
                        DrawBorderRect(sqRect, new Color(1f, 0.85f, 0.25f), 2.5f);
                    }

                    // Vẽ Quân Cờ trên ô
                    if (!piece.IsEmpty)
                    {
                        Rect pieceRect = new Rect(sqRect.x + 4f, sqRect.y + 4f, sqSize - 8f, sqSize - 8f);
                        DrawSingleChessPiece(pieceRect, piece);
                    }

                    // Vẽ chấm gợi ý các nước đi hợp lệ chuẩn FIDE
                    for (int m = 0; m < currentSquareLegalMoves.Count; m++)
                    {
                        var mv = currentSquareLegalMoves[m];
                        if (mv.toR == boardR && mv.toC == boardC)
                        {
                            bool isCaptureMove = !mv.capturedPiece.IsEmpty || mv.isEnPassant;
                            if (isCaptureMove)
                            {
                                // Viền đỏ cam báo hiệu nước ăn quân hợp lệ
                                DrawSolidRect(sqRect, new Color(0.95f, 0.28f, 0.22f, 0.35f));
                                DrawBorderRect(sqRect, new Color(1f, 0.25f, 0.20f), 3f);
                            }
                            else
                            {
                                // Chấm tròn xanh ở tâm ô trống hợp lệ
                                float dotSz = sqSize * 0.28f;
                                Rect dotRect = new Rect(
                                    sqRect.x + (sqSize - dotSz) * 0.5f,
                                    sqRect.y + (sqSize - dotSz) * 0.5f,
                                    dotSz, dotSz);
                                DrawSolidRect(dotRect, new Color(0.15f, 0.65f, 0.30f, 0.72f));
                            }
                            break;
                        }
                    }

                    // Click tương tác vào ô cờ
                    if (GUI.Button(sqRect, GUIContent.none, GUIStyle.none))
                    {
                        OnSquareClicked(boardR, boardC);
                    }
                }
            }

            // Vẽ nhãn cột (a..h) và hàng (1..8) trên viền bàn cờ
            for (int i = 0; i < 8; i++)
            {
                int boardC = flipBoardPerspective ? (7 - i) : i;
                int boardR = flipBoardPerspective ? i : (7 - i);

                string fileStr = ((char)('a' + boardC)).ToString();
                string rankStr = ((char)('1' + boardR)).ToString();

                GUI.Label(new Rect(boardX + i * sqSize, boardY + boardSize + 2f, sqSize, 20f), fileStr, coordSt);
                GUI.Label(new Rect(boardX + i * sqSize, boardY - 22f, sqSize, 20f), fileStr, coordSt);
                GUI.Label(new Rect(boardX - 22f, boardY + i * sqSize, 20f, sqSize), rankStr, coordSt);
                GUI.Label(new Rect(boardX + boardSize + 2f, boardY + i * sqSize, 20f, sqSize), rankStr, coordSt);
            }
        }

        private void DrawSingleChessPiece(Rect rect, ChessPiece p)
        {
            if (p.IsEmpty) return;

            if (useCustomPieceSprites)
            {
                Texture2D tex = GetPieceTexture(p);
                if (tex != null)
                {
                    GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                    return;
                }
            }

            // Chế độ hiển thị Ký hiệu Cờ Vua Cổ Điển
            string symbol = p.type switch
            {
                ChessPieceType.King => "♚",
                ChessPieceType.Queen => "♛",
                ChessPieceType.Rook => "♜",
                ChessPieceType.Bishop => "♝",
                ChessPieceType.Knight => "♞",
                ChessPieceType.Pawn => "♟",
                _ => ""
            };

            GUIStyle symSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(rect.height * 0.78f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = (p.color == ChessColor.White) ? new Color(0.99f, 0.97f, 0.90f) : new Color(0.14f, 0.16f, 0.22f) }
            };

            GUIStyle outlineSt = new GUIStyle(symSt)
            {
                normal = { textColor = (p.color == ChessColor.White) ? new Color(0.20f, 0.15f, 0.10f) : new Color(0.85f, 0.88f, 0.95f) }
            };
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), symbol, outlineSt);
            GUI.Label(rect, symbol, symSt);
        }

        // =====================================================================
        // HỘP THOẠI PHONG CẤP TỐT (PAWN PROMOTION) & KẾT THÚC VÁN ĐẤU
        // =====================================================================
        private void DrawPromotionModal(float centerX, float centerY)
        {
            float w = 360f;
            float h = 135f;
            Rect box = new Rect(centerX - w * 0.5f, centerY - h * 0.5f, w, h);
            DrawSolidRect(box, new Color(0.10f, 0.13f, 0.18f, 0.98f));
            DrawBorderRect(box, new Color(1f, 0.85f, 0.35f), 2.5f);

            GUIStyle titleSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.45f) }
            };
            GUI.Label(new Rect(box.x, box.y + 10f, box.width, 24f), "👑 PHONG CẤP TỐT (CHỌN QUÂN PHONG CẤP)", titleSt);

            ChessPieceType[] promoTypes = {
                ChessPieceType.Queen, ChessPieceType.Rook, ChessPieceType.Bishop, ChessPieceType.Knight
            };
            string[] promoNames = { "Hậu (Q)", "Xe (R)", "Tượng (B)", "Mã (N)" };

            for (int i = 0; i < 4; i++)
            {
                Rect card = new Rect(box.x + 16f + i * 84f, box.y + 42f, 76f, 80f);
                DrawSolidRect(card, new Color(0.20f, 0.25f, 0.34f, 0.96f));
                DrawBorderRect(card, new Color(0.75f, 0.65f, 0.35f), 1.5f);

                ChessPiece previewP = new ChessPiece { type = promoTypes[i], color = currentTurn };
                DrawSingleChessPiece(new Rect(card.x + 12f, card.y + 4f, 52f, 52f), previewP);

                GUIStyle lblSt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(card.x, card.y + 56f, card.width, 20f), promoNames[i], lblSt);

                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    ChessMove finalMove = pendingPromotionBaseMove;
                    finalMove.isPromotion = true;
                    finalMove.promotedTo = promoTypes[i];
                    awaitingPromotion = false;
                    ExecuteMove(finalMove);
                }
            }
        }

        private void DrawGameOverBanner(float centerX, float centerY)
        {
            float w = 480f;
            float h = 156f;
            Rect box = new Rect(centerX - w * 0.5f, centerY - h * 0.5f, w, h);
            DrawSolidRect(box, new Color(0.08f, 0.11f, 0.16f, 0.97f));
            Color borderColor = isCheckmate
                ? new Color(1f, 0.84f, 0.32f)
                : (isStalemateOrDraw ? new Color(0.58f, 0.78f, 0.96f) : new Color(1f, 0.84f, 0.32f));
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
                ResetBoardToStandardFIDE();
                CozyAudioManager.PlaySfx(CozySfxType.Whistle, 0.7f);
            }
            if (GUI.Button(new Rect(box.x + 255f, box.y + 108f, 175f, 34f), "🚪 Đứng Dậy Rời Bàn", btnSt))
            {
                ExitChessScene();
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
