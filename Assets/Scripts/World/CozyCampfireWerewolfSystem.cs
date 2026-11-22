using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;
using CozyLife.Network;

namespace CozyLife.World
{
    #region Enums & Data Structures
    public enum WerewolfRole
    {
        Villager = 0,    // Dân Làng
        Werewolf = 1,    // Ma Sói
        Seer = 2,        // Tiên Tri
        Guard = 3,       // Bảo Vệ
        Witch = 4,       // Phù Thủy
        Hunter = 5       // Thợ Săn
    }

    public enum WerewolfTeam
    {
        Village,
        Werewolf
    }

    public enum WerewolfPhase
    {
        Lobby = 0,
        RoleReveal = 1,
        NightFall = 2,
        NightGuard = 3,
        NightWerewolf = 4,
        NightWitch = 5,
        NightSeer = 6,
        DayDawn = 7,
        HunterDawnShot = 8,
        DayDiscussion = 9,
        DayVoting = 10,
        DayExecution = 11,
        HunterExecutionShot = 12,
        GameOver = 13
    }

    public static class WerewolfRoleInfo
    {
        public static string GetNameVN(WerewolfRole role) => role switch
        {
            WerewolfRole.Werewolf => "Ma Sói",
            WerewolfRole.Seer => "Tiên Tri",
            WerewolfRole.Guard => "Bảo Vệ",
            WerewolfRole.Witch => "Phù Thủy",
            WerewolfRole.Hunter => "Thợ Săn",
            _ => "Dân Làng"
        };

        public static string GetIcon(WerewolfRole role) => role switch
        {
            WerewolfRole.Werewolf => "🐺",
            WerewolfRole.Seer => "🔮",
            WerewolfRole.Guard => "🛡️",
            WerewolfRole.Witch => "🧙",
            WerewolfRole.Hunter => "🏹",
            _ => "👨‍🌾"
        };

        public static WerewolfTeam GetTeam(WerewolfRole role) =>
            role == WerewolfRole.Werewolf ? WerewolfTeam.Werewolf : WerewolfTeam.Village;

        public static string GetTeamNameVN(WerewolfRole role) =>
            GetTeam(role) == WerewolfTeam.Werewolf ? "Phe Ma Sói 🐺" : "Phe Dân Làng 🕊️";

        public static Color GetTeamColor(WerewolfRole role) =>
            GetTeam(role) == WerewolfTeam.Werewolf ? new Color(0.95f, 0.28f, 0.28f) : new Color(0.35f, 0.85f, 0.45f);

        public static string GetCardSpritePath(WerewolfRole role) => role switch
        {
            WerewolfRole.Werewolf => "CozyLifeArt/Werewolf/card_werewolf",
            WerewolfRole.Seer => "CozyLifeArt/Werewolf/card_seer",
            WerewolfRole.Guard => "CozyLifeArt/Werewolf/card_guard",
            WerewolfRole.Witch => "CozyLifeArt/Werewolf/card_witch",
            WerewolfRole.Hunter => "CozyLifeArt/Werewolf/card_hunter",
            _ => "CozyLifeArt/Werewolf/card_villager"
        };

        public static string GetDescriptionVN(WerewolfRole role) => role switch
        {
            WerewolfRole.Werewolf => "Ác quỷ đội lốt người. Ban đêm thức dậy chọn 1 người để cắn xé. Ban ngày giả vờ là dân làng hiền lành để lừa gạt mọi người.",
            WerewolfRole.Seer => "Nhà thông thái có con mắt thứ ba thấu thị. Mỗi đêm được chỉ định soi danh tính bí mật của 1 người xem người đó là Sói hay Thiện Lành.",
            WerewolfRole.Guard => "Dũng sĩ mang khiên hộ tống dân làng. Mỗi đêm chọn bảo vệ 1 người (người đó miễn nhiễm bị cắn). Không được chọn 1 người 2 đêm liên tiếp.",
            WerewolfRole.Witch => "Phù thủy bí ẩn mang 2 bình độc dược dùng 1 lần trong cả ván: 1 Bình Cứu (cứu nạn nhân bị Sói cắn) và 1 Bình Độc (đầu độc 1 người bất kỳ).",
            WerewolfRole.Hunter => "Xạ thủ thiện xạ của rừng già. Khi bị hạ gục (dù do Sói cắn hay bị dân làng treo cổ), được giương súng bắn 1 phát kéo theo 1 người bất kỳ cùng chết!",
            _ => "Người dân thị trấn chăm chỉ, yêu hòa bình. Ban đêm ngủ say, ban ngày dùng trí tuệ, khả năng quan sát và tài hùng biện để tìm ra Sói và treo cổ."
        };

        public static string GetNightActionPrompt(WerewolfRole role) => role switch
        {
            WerewolfRole.Werewolf => "🐺 Hãy chọn 1 người để đàn Sói xé xác đêm nay:",
            WerewolfRole.Guard => "🛡️ Hãy chọn 1 người để trao khiên bảo vệ đêm nay:",
            WerewolfRole.Seer => "🔮 Hãy chọn 1 người để soi danh tính bí mật:",
            WerewolfRole.Witch => "🧙 Phù thủy hãy quyết định dùng độc dược đêm nay:",
            _ => "Đang ngủ..."
        };
    }

    [Serializable]
    public class WerewolfPlayerSlot
    {
        public int seatIndex;
        public string playerName;
        public bool isLocal;
        public bool isBot;
        public int networkClientId;
        public CharacterAppearanceData botAppearance;

        public WerewolfRole role = WerewolfRole.Villager;
        public bool isAlive = true;
        public int deathDay = 0;
        public string deathReason = "";

        public int votedTargetSeat = -1; // -1: chưa vote, -2: bỏ phiếu trắng
        public bool hasUsedHealPotion = false;
        public bool hasUsedPoisonPotion = false;
        public int lastProtectedSeat = -1;

        // Trí tuệ Bot
        public int botSuspectTarget = -1;
        public bool botKnowsWolf = false;
    }

    [Serializable]
    public class WerewolfNetworkPacket
    {
        public string action;
        public int senderSeat;
        public int targetSeat;
        public int phase;
        public int extraInt;
        public string extraString;
    }
    #endregion

    #region Campfire & Seat World Interactables
    /// <summary>
    /// Bàn Lửa Trại Trung Tâm Thị Trấn (Campfire Werewolf)
    /// Đặt tại bãi cỏ gx: -4, gy: 4 trước Nhà Hàng.
    /// Khúc gỗ ấm cúng quanh đống lửa cho 4-8 người chơi Ma Sói.
    /// </summary>
    public class CampfireWerewolfInteractable : WorldInteractableBase
    {
        private CampfireSeatInteractable[] seats = new CampfireSeatInteractable[8];
        private SpriteRenderer campfireSr;
        private SpriteRenderer glowSr;

        public CampfireSeatInteractable[] Seats => seats;

        private void Awake()
        {
            interactTitle = "🔥 Lửa Trại Ma Sói";
            campfireSr = GetComponent<SpriteRenderer>();
            CreateFireGlowAura();
        }

        private void CreateFireGlowAura()
        {
            GameObject glowObj = new GameObject("FireGlowAura");
            glowObj.transform.SetParent(transform);
            glowObj.transform.localPosition = new Vector3(0f, -0.02f, 0f);

            glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = CreateSoftRadialGlowSprite();
            glowSr.sortingOrder = -1999;
            glowObj.transform.localScale = new Vector3(1.8f, 1.1f, 1f);
        }

        private static Sprite CreateSoftRadialGlowSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 0.62f, 0.18f, alpha * 0.42f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private void Update()
        {
            // Hiệu ứng ánh sáng than lửa ấm áp bập bùng nhẹ (không làm rung lắc các vật thể)
            if (glowSr != null)
            {
                float auraAlpha = 0.28f + 0.08f * Mathf.Sin(Time.time * 5.5f);
                glowSr.color = new Color(1f, 0.55f, 0.18f, auraAlpha);
            }
        }

        public void RegisterSeat(int index, CampfireSeatInteractable seat)
        {
            if (index >= 0 && index < 8)
                seats[index] = seat;
        }

        public override string GetWorldOverheadLabel()
        {
            return "🔥 Lửa Trại Ma Sói [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "[E] Mở Bàn Trò Chơi Ma Sói Thị Trấn (4 - 8 Người)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            CozyWerewolfManager.Instance?.OpenWerewolfUI();
        }
    }

    /// <summary>
    /// Chỗ Ngồi Trên Khúc Gỗ Lửa Trại (Log Bench Seat)
    /// </summary>
    public class CampfireSeatInteractable : WorldInteractableBase
    {
        private int seatIndex = 0;
        private CampfireWerewolfInteractable campfireParent;
        private SpriteRenderer seatSr;
        private GameObject seatedAvatarObj;
        private SpriteRenderer seatedAvatarSr;

        public int SeatIndex => seatIndex;
        public CampfireWerewolfInteractable CampfireParent => campfireParent;
        public Vector3 SeatWorldPosition => transform.position + new Vector3(0f, 0.12f, 0f);
        public bool FacesLeft => campfireParent != null && transform.position.x > campfireParent.transform.position.x + 0.1f;

        public void ConfigureSeat(int index, CampfireWerewolfInteractable parent)
        {
            seatIndex = index;
            campfireParent = parent;
            interactTitle = $"Chỗ Ngồi #{index + 1}";
            parent.RegisterSeat(index, this);
        }

        private void Awake()
        {
            seatSr = GetComponent<SpriteRenderer>();
        }

        public override string GetWorldOverheadLabel()
        {
            var mgr = CozyWerewolfManager.Instance;
            string occupantName = "";
            bool isOccupied = mgr != null && mgr.IsSeatOccupied(seatIndex, out occupantName);

            if (isOccupied)
            {
                return $"🪑 Chỗ #{seatIndex + 1} ({occupantName})";
            }

            // Chỉ hiện label khi người chơi lại gần (< 1.25m) để tránh làm rối màn hình
            if (PlayerController.LocalPlayer != null)
            {
                float dist = Vector2.Distance(PlayerController.LocalPlayer.transform.position, transform.position);
                if (dist > 1.25f) return "";
            }

            return $"🪑 Chỗ #{seatIndex + 1} [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            var mgr = CozyWerewolfManager.Instance;
            if (mgr != null && mgr.IsSeatOccupied(seatIndex, out string occupantName))
            {
                return $"[E] Chỗ #{seatIndex + 1} đang có {occupantName} ngồi";
            }
            return $"[E] Ngồi Vào Khúc Gỗ (Chỗ #{seatIndex + 1})";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            var mgr = CozyWerewolfManager.Instance;
            if (mgr == null) return;

            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null) return;

            if (mgr.IsSeatOccupied(seatIndex, out string occupantName))
            {
                CozyLifeHUD.PushToast($"🪑 Chỗ #{seatIndex + 1} đã có {occupantName} ngồi rồi!");
                mgr.OpenWerewolfUI();
                return;
            }

            // Người chơi địa phương ngồi vào ghế
            mgr.TakeSeat(seatIndex, isLocal: true, isBot: false, playerName: player.PlayerName);
            pc.SitAtCampfireSeat(this);
            CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture, 0.9f);
            CozyLifeHUD.PushToast($"🪑 Đã ngồi vào Khúc Gỗ (Chỗ #{seatIndex + 1})!");
            mgr.OpenWerewolfUI();
        }

        public void OnPlayerStoodUp()
        {
            var mgr = CozyWerewolfManager.Instance;
            if (mgr != null && mgr.CurrentPhase == WerewolfPhase.Lobby)
            {
                mgr.VacateSeat(seatIndex);
            }
            ClearSeatedAvatar();
        }

        public void SetOccupantVisual(CharacterAppearanceData appearance, bool flip)
        {
            if (seatedAvatarObj == null)
            {
                seatedAvatarObj = new GameObject($"SeatedAvatar_Seat{seatIndex + 1}");
                seatedAvatarObj.transform.SetParent(transform);
                seatedAvatarObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                seatedAvatarSr = seatedAvatarObj.AddComponent<SpriteRenderer>();
                var sorting = seatedAvatarObj.AddComponent<IsometricSorting>();
                sorting.Configure(true, 5);
            }

            if (appearance != null && seatedAvatarSr != null)
            {
                seatedAvatarSr.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(appearance, 0);
                seatedAvatarSr.flipX = flip;
                seatedAvatarObj.SetActive(true);
            }
        }

        public void ClearSeatedAvatar()
        {
            if (seatedAvatarObj != null)
            {
                seatedAvatarObj.SetActive(false);
            }
        }
    }
    #endregion

    #region Werewolf Match Manager
    /// <summary>
    /// Bộ Điều Phối & Giao Diện Trò Chơi Ma Sói Lửa Trại (4 - 8 Người)
    /// </summary>
    public class CozyWerewolfManager : MonoBehaviour
    {
        public static CozyWerewolfManager Instance { get; private set; }
        public static bool IsGameActive => Instance != null && Instance.isUIOpen;

        private const int MaxSeats = 8;
        private readonly WerewolfPlayerSlot[] playerSlots = new WerewolfPlayerSlot[MaxSeats];
        private CampfireWerewolfInteractable campfireInstance;

        [Header("Match State")]
        private WerewolfPhase currentPhase = WerewolfPhase.Lobby;
        private int currentDayNumber = 1;
        private float phaseTimer = 0f;
        private float phaseDuration = 0f;
        private bool isUIOpen = false;

        // Lưu vết đêm
        private int guardProtectedSeat = -1;
        private int wolfVotedTargetSeat = -1;
        private int witchPoisonTargetSeat = -1;
        private bool witchSavedVictim = false;
        private int seerInspectedSeat = -1;
        private string seerInspectResult = "";

        // Nạn nhân đêm & hành quyết ngày
        private List<int> dawnCasualties = new List<int>();
        private int hunterRevengePlayerSeat = -1;
        private int lynchedSeat = -1;
        private WerewolfTeam winningTeam = WerewolfTeam.Village;

        // Chat & nhật ký biến cố trong trận Ma Sói
        private readonly List<string> matchLog = new List<string>();
        private Vector2 logScrollPos = Vector2.zero;

        // UI Textures & Caches
        private readonly Dictionary<WerewolfRole, Texture2D> roleCardTextures = new Dictionary<WerewolfRole, Texture2D>();
        private Texture2D campfireTexture;
        private Texture2D stumpTexture;
        private Texture2D blankWhiteTex;

        // Danh sách bạn bè thị trấn (AI Bots)
        private static readonly string[] BotNames = new string[]
        {
            "An (Nông Dân)", "Yuki (Đầu Bếp)", "Minh (Bác Sĩ)",
            "Linh (Thợ Mộc)", "Ken (Ngư Phủ)", "Mai (Thảo Dược)", "Dan (Thợ Rèn)"
        };

        public WerewolfPhase CurrentPhase => currentPhase;
        public bool IsUIOpen => isUIOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = 0; i < MaxSeats; i++)
            {
                playerSlots[i] = null;
            }

            CreateBlankWhiteTex();
            PreloadCardTextures();
        }

        private void OnEnable()
        {
            CozyNetworkManager.OnWerewolfPacketReceived += HandleIncomingNetworkPacket;
        }

        private void OnDisable()
        {
            CozyNetworkManager.OnWerewolfPacketReceived -= HandleIncomingNetworkPacket;
        }

        public void RegisterCampfire(CampfireWerewolfInteractable campfire)
        {
            campfireInstance = campfire;
        }

        private void CreateBlankWhiteTex()
        {
            blankWhiteTex = new Texture2D(2, 2);
            blankWhiteTex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            blankWhiteTex.Apply();
        }

        private void PreloadCardTextures()
        {
            foreach (WerewolfRole r in Enum.GetValues(typeof(WerewolfRole)))
            {
                string path = WerewolfRoleInfo.GetCardSpritePath(r);
                Texture2D tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    roleCardTextures[r] = tex;
                }
            }
            campfireTexture = Resources.Load<Texture2D>("CozyLifeArt/Objects/obj_campfire");
            stumpTexture = Resources.Load<Texture2D>("CozyLifeArt/Objects/obj_stump_seat");
        }

        public void OpenWerewolfUI()
        {
            isUIOpen = true;
        }

        public void CloseWerewolfUI()
        {
            isUIOpen = false;
        }

        public bool IsSeatOccupied(int seatIndex, out string occupantName)
        {
            occupantName = "";
            if (seatIndex < 0 || seatIndex >= MaxSeats) return false;
            var slot = playerSlots[seatIndex];
            if (slot != null && !string.IsNullOrEmpty(slot.playerName))
            {
                occupantName = slot.playerName;
                return true;
            }
            return false;
        }

        public int GetOccupiedSeatCount()
        {
            int count = 0;
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null) count++;
            }
            return count;
        }

        public int GetAlivePlayerCount()
        {
            int count = 0;
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null && playerSlots[i].isAlive) count++;
            }
            return count;
        }

        public WerewolfPlayerSlot GetLocalPlayerSlot()
        {
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null && playerSlots[i].isLocal) return playerSlots[i];
            }
            return null;
        }

        #region Seating & Lobby Management
        public void TakeSeat(int seatIndex, bool isLocal, bool isBot = false, string playerName = "Người Chơi", int clientId = 1, CharacterAppearanceData appearance = null)
        {
            if (seatIndex < 0 || seatIndex >= MaxSeats) return;

            var slot = new WerewolfPlayerSlot
            {
                seatIndex = seatIndex,
                playerName = playerName,
                isLocal = isLocal,
                isBot = isBot,
                networkClientId = clientId,
                isAlive = true,
                botAppearance = appearance ?? CreateBotAppearance(seatIndex, playerName)
            };

            playerSlots[seatIndex] = slot;

            // Cập nhật ngoại trang ghế ngồi nếu là Bot
            if (isBot && campfireInstance != null && campfireInstance.Seats[seatIndex] != null)
            {
                campfireInstance.Seats[seatIndex].SetOccupantVisual(slot.botAppearance, campfireInstance.Seats[seatIndex].FacesLeft);
            }

            AddLog($"🪑 [{slot.playerName}] đã ngồi vào Ghế #{seatIndex + 1}.");

            // Đồng bộ mạng nếu có
            if (isLocal)
            {
                SendNetworkEvent("TakeSeat", seatIndex, extraStr: playerName);
            }
        }

        public void VacateSeat(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= MaxSeats) return;
            if (playerSlots[seatIndex] != null)
            {
                AddLog($"🚪 [{playerSlots[seatIndex].playerName}] đã rời Ghế #{seatIndex + 1}.");
                playerSlots[seatIndex] = null;
            }

            if (campfireInstance != null && campfireInstance.Seats[seatIndex] != null)
            {
                campfireInstance.Seats[seatIndex].ClearSeatedAvatar();
            }

            SendNetworkEvent("LeaveSeat", seatIndex);
        }

        public void FillRemainingSeatsWithBots()
        {
            if (currentPhase != WerewolfPhase.Lobby) return;

            int botIdx = 0;
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] == null)
                {
                    string bName = botIdx < BotNames.Length ? BotNames[botIdx] : $"Bạn Thị Trấn #{i + 1}";
                    botIdx++;
                    TakeSeat(i, isLocal: false, isBot: true, playerName: bName);
                }
            }
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("🤖 Đã mời các bạn thị trấn (An, Yuki, Minh, Linh...) vào đầy đủ 8 ghế!");
        }

        public void ClearAllBots()
        {
            if (currentPhase != WerewolfPhase.Lobby) return;

            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null && playerSlots[i].isBot)
                {
                    VacateSeat(i);
                }
            }
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("🧹 Đã dọn sạch các ghế Bot!");
        }

        private CharacterAppearanceData CreateBotAppearance(int index, string name)
        {
            var data = new CharacterAppearanceData();
            data.characterName = name;
            data.skinToneIndex = index % 4;
            data.hairStyleIndex = index % 6;
            data.hairColorIndex = (index * 2) % 6;
            data.shirtIndex = (index + 1) % 6;
            data.pantsIndex = (index + 2) % 6;
            return data;
        }
        #endregion

        #region Match Loop & State Machine
        public void StartMatch()
        {
            int total = GetOccupiedSeatCount();
            if (total < 4)
            {
                CozyLifeHUD.PushToast("⚠️ Cần ít nhất 4 người chơi để bắt đầu Ma Sói! Hãy nhấn 'Mời Bot' để điền đầy chỗ.");
                return;
            }

            currentDayNumber = 1;
            matchLog.Clear();
            AddLog("🔥 Trò chơi Ma Sói Thị Trấn chính thức BẮT ĐẦU!");

            DistributeRoles(total);
            SetPhase(WerewolfPhase.RoleReveal, 6.0f);
            CozyAudioManager.PlaySfx(CozySfxType.MysticChime, 1f);

            SendNetworkEvent("StartGame", 0);
        }

        private void DistributeRoles(int totalPlayers)
        {
            List<WerewolfRole> deck = new List<WerewolfRole>();

            // Phân phối vai trò chuẩn 4 - 8 người
            switch (totalPlayers)
            {
                case 4:
                    deck.AddRange(new WerewolfRole[] { WerewolfRole.Werewolf, WerewolfRole.Seer, WerewolfRole.Guard, WerewolfRole.Villager });
                    break;
                case 5:
                    deck.AddRange(new WerewolfRole[] { WerewolfRole.Werewolf, WerewolfRole.Seer, WerewolfRole.Guard, WerewolfRole.Witch, WerewolfRole.Villager });
                    break;
                case 6:
                    deck.AddRange(new WerewolfRole[] { WerewolfRole.Werewolf, WerewolfRole.Werewolf, WerewolfRole.Seer, WerewolfRole.Guard, WerewolfRole.Witch, WerewolfRole.Villager });
                    break;
                case 7:
                    deck.AddRange(new WerewolfRole[] { WerewolfRole.Werewolf, WerewolfRole.Werewolf, WerewolfRole.Seer, WerewolfRole.Guard, WerewolfRole.Witch, WerewolfRole.Hunter, WerewolfRole.Villager });
                    break;
                default: // 8 người
                    deck.AddRange(new WerewolfRole[] { WerewolfRole.Werewolf, WerewolfRole.Werewolf, WerewolfRole.Seer, WerewolfRole.Guard, WerewolfRole.Witch, WerewolfRole.Hunter, WerewolfRole.Villager, WerewolfRole.Villager });
                    break;
            }

            // Trộn bài ngẫu nhiên (Fisher-Yates)
            System.Random rng = new System.Random();
            int n = deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var val = deck[k];
                deck[k] = deck[n];
                deck[n] = val;
            }

            // Chia bài cho từng người đang ngồi ghế
            int cardIdx = 0;
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null && cardIdx < deck.Count)
                {
                    playerSlots[i].role = deck[cardIdx++];
                    playerSlots[i].isAlive = true;
                    playerSlots[i].deathDay = 0;
                    playerSlots[i].deathReason = "";
                    playerSlots[i].votedTargetSeat = -1;
                    playerSlots[i].hasUsedHealPotion = false;
                    playerSlots[i].hasUsedPoisonPotion = false;
                    playerSlots[i].lastProtectedSeat = -1;
                }
            }

            var local = GetLocalPlayerSlot();
            if (local != null)
            {
                AddLog($"📜 Lá bài bí mật của bạn là: [{WerewolfRoleInfo.GetNameVN(local.role)}] {WerewolfRoleInfo.GetIcon(local.role)}");
            }
        }

        private void SetPhase(WerewolfPhase newPhase, float duration)
        {
            currentPhase = newPhase;
            phaseDuration = duration;
            phaseTimer = duration;

            OnPhaseEnter(newPhase);
        }

        private void OnPhaseEnter(WerewolfPhase phase)
        {
            switch (phase)
            {
                case WerewolfPhase.NightFall:
                    AddLog($"🌙 Đêm Thứ #{currentDayNumber} buông xuống... Cả làng chìm vào giấc ngủ.");
                    CozyAudioManager.PlaySfx(CozySfxType.WolfHowl, 0.9f);
                    guardProtectedSeat = -1;
                    wolfVotedTargetSeat = -1;
                    witchPoisonTargetSeat = -1;
                    witchSavedVictim = false;
                    seerInspectedSeat = -1;
                    seerInspectResult = "";
                    break;

                case WerewolfPhase.NightGuard:
                    AddLog("🛡️ Bảo Vệ thức giấc hộ tống một người dân...");
                    HandleBotGuardAction();
                    break;

                case WerewolfPhase.NightWerewolf:
                    AddLog("🐺 Đàn Sói mở mắt săn lùng con mồi...");
                    HandleBotWerewolfAction();
                    break;

                case WerewolfPhase.NightWitch:
                    AddLog("🧙 Phù Thủy pha chế độc dược...");
                    HandleBotWitchAction();
                    break;

                case WerewolfPhase.NightSeer:
                    AddLog("🔮 Tiên Tri hé mở thiên nhãn soi rọi danh tính...");
                    HandleBotSeerAction();
                    break;

                case WerewolfPhase.DayDawn:
                    AddLog($"☀️ Bình minh Ngày Thứ #{currentDayNumber} ló rạng!");
                    CozyAudioManager.PlaySfx(CozySfxType.BellGong, 1f);
                    ResolveNightCasualties();
                    break;

                case WerewolfPhase.DayDiscussion:
                    AddLog("💬 Dân làng bắt đầu thảo luận tìm ra kẻ đáng nghi!");
                    for (int i = 0; i < MaxSeats; i++)
                    {
                        if (playerSlots[i] != null) playerSlots[i].votedTargetSeat = -1;
                    }
                    TriggerBotDiscussionDialogue();
                    break;

                case WerewolfPhase.DayVoting:
                    AddLog("⚖️ Bắt đầu biểu quyết treo cổ kẻ tình nghi!");
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                    HandleBotDayVoting();
                    break;

                case WerewolfPhase.DayExecution:
                    ResolveDayExecution();
                    break;

                case WerewolfPhase.GameOver:
                    ResolveGameOver();
                    break;
            }
        }

        private void Update()
        {
            if (currentPhase == WerewolfPhase.Lobby) return;

            if (phaseTimer > 0f)
            {
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                {
                    AdvanceToNextPhase();
                }
            }
        }

        public void AdvanceToNextPhase()
        {
            switch (currentPhase)
            {
                case WerewolfPhase.RoleReveal:
                    SetPhase(WerewolfPhase.NightFall, 4.0f);
                    break;

                case WerewolfPhase.NightFall:
                    // Kiểm tra xem trong trận có Bảo Vệ còn sống không
                    if (HasAliveRole(WerewolfRole.Guard))
                        SetPhase(WerewolfPhase.NightGuard, 6.5f);
                    else
                        SetPhase(WerewolfPhase.NightWerewolf, 7.5f);
                    break;

                case WerewolfPhase.NightGuard:
                    SetPhase(WerewolfPhase.NightWerewolf, 7.5f);
                    break;

                case WerewolfPhase.NightWerewolf:
                    if (HasAliveRole(WerewolfRole.Witch))
                        SetPhase(WerewolfPhase.NightWitch, 7.0f);
                    else if (HasAliveRole(WerewolfRole.Seer))
                        SetPhase(WerewolfPhase.NightSeer, 7.0f);
                    else
                        SetPhase(WerewolfPhase.DayDawn, 6.0f);
                    break;

                case WerewolfPhase.NightWitch:
                    if (HasAliveRole(WerewolfRole.Seer))
                        SetPhase(WerewolfPhase.NightSeer, 7.0f);
                    else
                        SetPhase(WerewolfPhase.DayDawn, 6.0f);
                    break;

                case WerewolfPhase.NightSeer:
                    SetPhase(WerewolfPhase.DayDawn, 6.0f);
                    break;

                case WerewolfPhase.DayDawn:
                    if (hunterRevengePlayerSeat >= 0)
                    {
                        SetPhase(WerewolfPhase.HunterDawnShot, 7.0f);
                    }
                    else if (CheckWinCondition())
                    {
                        SetPhase(WerewolfPhase.GameOver, 0f);
                    }
                    else
                    {
                        SetPhase(WerewolfPhase.DayDiscussion, 25.0f);
                    }
                    break;

                case WerewolfPhase.HunterDawnShot:
                    hunterRevengePlayerSeat = -1;
                    if (CheckWinCondition())
                    {
                        SetPhase(WerewolfPhase.GameOver, 0f);
                    }
                    else
                    {
                        SetPhase(WerewolfPhase.DayDiscussion, 25.0f);
                    }
                    break;

                case WerewolfPhase.DayDiscussion:
                    SetPhase(WerewolfPhase.DayVoting, 16.0f);
                    break;

                case WerewolfPhase.DayVoting:
                    SetPhase(WerewolfPhase.DayExecution, 6.5f);
                    break;

                case WerewolfPhase.DayExecution:
                    if (hunterRevengePlayerSeat >= 0)
                    {
                        SetPhase(WerewolfPhase.HunterExecutionShot, 7.0f);
                    }
                    else if (CheckWinCondition())
                    {
                        SetPhase(WerewolfPhase.GameOver, 0f);
                    }
                    else
                    {
                        currentDayNumber++;
                        SetPhase(WerewolfPhase.NightFall, 4.0f);
                    }
                    break;

                case WerewolfPhase.HunterExecutionShot:
                    hunterRevengePlayerSeat = -1;
                    if (CheckWinCondition())
                    {
                        SetPhase(WerewolfPhase.GameOver, 0f);
                    }
                    else
                    {
                        currentDayNumber++;
                        SetPhase(WerewolfPhase.NightFall, 4.0f);
                    }
                    break;
            }
        }

        private bool HasAliveRole(WerewolfRole role)
        {
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null && playerSlots[i].isAlive && playerSlots[i].role == role)
                    return true;
            }
            return false;
        }

        #region Night Resolves & Bot Actions
        private void HandleBotGuardAction()
        {
            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive && slot.isBot && slot.role == WerewolfRole.Guard)
                {
                    List<int> validTargets = new List<int>();
                    for (int t = 0; t < MaxSeats; t++)
                    {
                        if (playerSlots[t] != null && playerSlots[t].isAlive && t != slot.lastProtectedSeat)
                        {
                            validTargets.Add(t);
                        }
                    }
                    if (validTargets.Count > 0)
                    {
                        int choice = validTargets[UnityEngine.Random.Range(0, validTargets.Count)];
                        guardProtectedSeat = choice;
                        slot.lastProtectedSeat = choice;
                    }
                    break;
                }
            }
        }

        private void HandleBotWerewolfAction()
        {
            // Nếu người chơi địa phương là Sói thì đợi người chơi chọn
            var local = GetLocalPlayerSlot();
            if (local != null && local.isAlive && local.role == WerewolfRole.Werewolf)
                return;

            // Bot Sói chọn nạn nhân là dân lành còn sống
            List<int> targets = new List<int>();
            for (int i = 0; i < MaxSeats; i++)
            {
                if (playerSlots[i] != null && playerSlots[i].isAlive && playerSlots[i].role != WerewolfRole.Werewolf)
                {
                    targets.Add(i);
                }
            }

            if (targets.Count > 0)
            {
                wolfVotedTargetSeat = targets[UnityEngine.Random.Range(0, targets.Count)];
            }
        }

        private void HandleBotWitchAction()
        {
            var local = GetLocalPlayerSlot();
            if (local != null && local.isAlive && local.role == WerewolfRole.Witch)
                return;

            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive && slot.isBot && slot.role == WerewolfRole.Witch)
                {
                    // Cứu nếu có người bị cắn và chưa dùng bình cứu
                    if (wolfVotedTargetSeat >= 0 && !slot.hasUsedHealPotion)
                    {
                        witchSavedVictim = true;
                        slot.hasUsedHealPotion = true;
                    }
                    // Đầu độc ngẫu nhiên ở những đêm sau nếu nghi ngờ
                    else if (currentDayNumber >= 2 && !slot.hasUsedPoisonPotion && UnityEngine.Random.value < 0.35f)
                    {
                        List<int> targets = new List<int>();
                        for (int t = 0; t < MaxSeats; t++)
                        {
                            if (playerSlots[t] != null && playerSlots[t].isAlive && t != i)
                                targets.Add(t);
                        }
                        if (targets.Count > 0)
                        {
                            witchPoisonTargetSeat = targets[UnityEngine.Random.Range(0, targets.Count)];
                            slot.hasUsedPoisonPotion = true;
                        }
                    }
                    break;
                }
            }
        }

        private void HandleBotSeerAction()
        {
            var local = GetLocalPlayerSlot();
            if (local != null && local.isAlive && local.role == WerewolfRole.Seer)
                return;

            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive && slot.isBot && slot.role == WerewolfRole.Seer)
                {
                    List<int> targets = new List<int>();
                    for (int t = 0; t < MaxSeats; t++)
                    {
                        if (playerSlots[t] != null && playerSlots[t].isAlive && t != i)
                            targets.Add(t);
                    }
                    if (targets.Count > 0)
                    {
                        int inspect = targets[UnityEngine.Random.Range(0, targets.Count)];
                        slot.botSuspectTarget = inspect;
                        slot.botKnowsWolf = playerSlots[inspect].role == WerewolfRole.Werewolf;
                    }
                    break;
                }
            }
        }

        public void SubmitLocalGuardAction(int targetSeat)
        {
            guardProtectedSeat = targetSeat;
            var local = GetLocalPlayerSlot();
            if (local != null) local.lastProtectedSeat = targetSeat;

            CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture, 1f);
            CozyLifeHUD.PushToast($"🛡️ Bạn đã trao khiên bảo vệ cho [{playerSlots[targetSeat].playerName}]!");
        }

        public void SubmitLocalWerewolfAction(int targetSeat)
        {
            wolfVotedTargetSeat = targetSeat;
            CozyAudioManager.PlaySfx(CozySfxType.WolfHowl, 0.7f);
            CozyLifeHUD.PushToast($"🐺 Đàn Sói đã chọn mục tiêu cắn: [{playerSlots[targetSeat].playerName}]!");
        }

        public void SubmitLocalWitchAction(bool useHeal, int poisonSeat = -1)
        {
            var local = GetLocalPlayerSlot();
            if (local == null) return;

            if (useHeal && !local.hasUsedHealPotion)
            {
                witchSavedVictim = true;
                local.hasUsedHealPotion = true;
                CozyLifeHUD.PushToast("🧪 Bạn đã dùng Bình Cứu hồi sinh nạn nhân!");
            }
            if (poisonSeat >= 0 && !local.hasUsedPoisonPotion)
            {
                witchPoisonTargetSeat = poisonSeat;
                local.hasUsedPoisonPotion = true;
                CozyLifeHUD.PushToast($"☠️ Bạn đã dùng Bình Độc lên [{playerSlots[poisonSeat].playerName}]!");
            }
            CozyAudioManager.PlaySfx(CozySfxType.Sizzle, 1f);
        }

        public void SubmitLocalSeerAction(int targetSeat)
        {
            seerInspectedSeat = targetSeat;
            var target = playerSlots[targetSeat];
            bool isWolf = target.role == WerewolfRole.Werewolf;
            seerInspectResult = isWolf
                ? $"⚠️ [{target.playerName}] chính là MA SÓI 🐺!"
                : $"✨ [{target.playerName}] là DÂN LÀNG / THIỆN LÀNH 🕊️";

            CozyAudioManager.PlaySfx(CozySfxType.MysticChime, 1f);
            CozyLifeHUD.PushToast(seerInspectResult);
        }

        private void ResolveNightCasualties()
        {
            dawnCasualties.Clear();

            // 1. Sói cắn
            if (wolfVotedTargetSeat >= 0)
            {
                if (wolfVotedTargetSeat == guardProtectedSeat)
                {
                    AddLog($"🛡️ Kỳ diệu thay! Bảo Vệ đã kịp che chở người bị Sói tấn công đêm qua!");
                }
                else if (witchSavedVictim)
                {
                    AddLog($"🧪 Phù Thủy đã kịp thời rót thuốc giải độc cứu sống nạn nhân!");
                }
                else
                {
                    dawnCasualties.Add(wolfVotedTargetSeat);
                    KillPlayer(wolfVotedTargetSeat, "bị Ma Sói cắn xé dã man 🐺");
                }
            }
            else
            {
                AddLog("🕊️ Một đêm bình yên kỳ lạ trôi qua, không có ai bị Sói cắn.");
            }

            // 2. Phù Thủy đầu độc
            if (witchPoisonTargetSeat >= 0 && !dawnCasualties.Contains(witchPoisonTargetSeat))
            {
                dawnCasualties.Add(witchPoisonTargetSeat);
                KillPlayer(witchPoisonTargetSeat, "bị trúng Độc Dược bí ẩn của Phù Thủy ☠️");
            }

            if (dawnCasualties.Count == 0)
            {
                AddLog("🎉 Bình minh rạng rỡ! Tất cả mọi người trong thị trấn đều an toàn!");
            }
            else
            {
                foreach (int seat in dawnCasualties)
                {
                    var slot = playerSlots[seat];
                    AddLog($"🩸 Nạn nhân đêm qua: [{slot.playerName}] {slot.deathReason}!");

                    // Nếu là Thợ Săn chết -> kích hoạt phát súng trả đòn
                    if (slot.role == WerewolfRole.Hunter)
                    {
                        hunterRevengePlayerSeat = seat;
                        AddLog($"🏹 Thợ Săn [{slot.playerName}] trước khi trút hơi thở cuối cùng giương nỏ bắn trả!");
                    }
                }
            }
        }
        #endregion

        #region Day Discussion & Voting
        private void TriggerBotDiscussionDialogue()
        {
            StartCoroutine(RoutineBotDialogue());
        }

        private IEnumerator RoutineBotDialogue()
        {
            yield return new WaitForSeconds(1.2f);

            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive && slot.isBot)
                {
                    yield return new WaitForSeconds(UnityEngine.Random.Range(2.0f, 4.0f));
                    if (currentPhase != WerewolfPhase.DayDiscussion) yield break;

                    string speech = GetBotSpeech(slot);
                    if (!string.IsNullOrEmpty(speech))
                    {
                        AddLog($"💬 [{slot.playerName}]: \"{speech}\"");
                    }
                }
            }
        }

        private string GetBotSpeech(WerewolfPlayerSlot bot)
        {
            if (bot.role == WerewolfRole.Seer && bot.botKnowsWolf && bot.botSuspectTarget >= 0)
            {
                string suspectName = playerSlots[bot.botSuspectTarget].playerName;
                return $"Tôi là Tiên Tri đây! Đêm qua tôi vừa soi ra [{suspectName}] chính là MA SÓI 🐺! Mọi người mau vote hắn!";
            }

            if (bot.role == WerewolfRole.Werewolf)
            {
                string[] wolfLies = new string[]
                {
                    "Tôi đêm qua nghe tiếng bước chân rón rén ở phía đối diện, chắc chắn không phải tôi!",
                    "Mọi người đừng vội tin ai, kẻ nói to nhất thường mới là Sói đóng giả đó!",
                    "Tôi là Dân Làng chân chính 100%, ai vu oan tôi là mắc mưu của Sói rồi!"
                };
                return wolfLies[UnityEngine.Random.Range(0, wolfLies.Length)];
            }

            string[] generalThoughts = new string[]
            {
                "Đêm qua căng thẳng thật đấy, chúng ta phải quan sát kỹ từng cử chỉ!",
                "Tôi thấy những ai im lặng nãy giờ rất đáng ngờ nha...",
                "Hãy biểu quyết cẩn trọng, làng ta không thể để mất thêm người vô tội nữa!",
                "Tôi tin tưởng trực giác của mình, lát nữa tôi sẽ vote cho kẻ tình nghi nhất!"
            };
            return generalThoughts[UnityEngine.Random.Range(0, generalThoughts.Length)];
        }

        private void HandleBotDayVoting()
        {
            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive && slot.isBot)
                {
                    List<int> candidates = new List<int>();
                    for (int c = 0; c < MaxSeats; c++)
                    {
                        if (playerSlots[c] != null && playerSlots[c].isAlive && c != i)
                        {
                            candidates.Add(c);
                        }
                    }

                    if (slot.role == WerewolfRole.Seer && botHasWolfCandidate(slot, out int targetWolf))
                    {
                        slot.votedTargetSeat = targetWolf;
                    }
                    else if (candidates.Count > 0)
                    {
                        // 85% cơ hội vote, 15% bỏ phiếu trắng
                        slot.votedTargetSeat = UnityEngine.Random.value < 0.85f
                            ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
                            : -2;
                    }
                }
            }
        }

        private bool botHasWolfCandidate(WerewolfPlayerSlot bot, out int wolfSeat)
        {
            wolfSeat = -1;
            if (bot.botKnowsWolf && bot.botSuspectTarget >= 0 && playerSlots[bot.botSuspectTarget].isAlive)
            {
                wolfSeat = bot.botSuspectTarget;
                return true;
            }
            return false;
        }

        public void SubmitLocalVote(int targetSeat)
        {
            var local = GetLocalPlayerSlot();
            if (local == null || !local.isAlive) return;

            local.votedTargetSeat = targetSeat;
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            if (targetSeat == -2)
            {
                CozyLifeHUD.PushToast("⚪ Bạn đã chọn Bỏ Phiếu Trắng!");
            }
            else
            {
                CozyLifeHUD.PushToast($"🗳️ Bạn đã bỏ phiếu treo cổ [{playerSlots[targetSeat].playerName}]!");
            }
        }

        private void ResolveDayExecution()
        {
            Dictionary<int, int> voteTally = new Dictionary<int, int>();
            int blankVotes = 0;

            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive)
                {
                    int v = slot.votedTargetSeat;
                    if (v == -2)
                    {
                        blankVotes++;
                    }
                    else if (v >= 0 && playerSlots[v] != null && playerSlots[v].isAlive)
                    {
                        if (!voteTally.ContainsKey(v)) voteTally[v] = 0;
                        voteTally[v]++;
                    }
                }
            }

            int topSeat = -1;
            int maxVotes = 0;
            bool isTie = false;

            foreach (var kvp in voteTally)
            {
                if (kvp.Value > maxVotes)
                {
                    maxVotes = kvp.Value;
                    topSeat = kvp.Key;
                    isTie = false;
                }
                else if (kvp.Value == maxVotes)
                {
                    isTie = true;
                }
            }

            if (topSeat >= 0 && !isTie && maxVotes > blankVotes)
            {
                lynchedSeat = topSeat;
                var victim = playerSlots[topSeat];
                AddLog($"⚖️ Dân làng đã đồng thuận đưa [{victim.playerName}] lên giàn treo cổ ({maxVotes} phiếu)!");
                CozyAudioManager.PlaySfx(CozySfxType.Door, 1f);

                KillPlayer(topSeat, "bị dân làng biểu quyết treo cổ ⚖️");
                AddLog($"📜 Danh tính thật của [{victim.playerName}] hé lộ: [{WerewolfRoleInfo.GetNameVN(victim.role)}] {WerewolfRoleInfo.GetIcon(victim.role)}!");

                if (victim.role == WerewolfRole.Hunter)
                {
                    hunterRevengePlayerSeat = topSeat;
                    AddLog($"🏹 Thợ Săn [{victim.playerName}] giương súng bắn phát đạn báo thù cuối cùng!");
                }
            }
            else
            {
                lynchedSeat = -1;
                AddLog("🕊️ Số phiếu biểu quyết hòa hoặc phiếu trắng chiếm đa số! Hôm nay không có ai bị treo cổ.");
            }
        }

        public void SubmitHunterShot(int targetSeat)
        {
            if (targetSeat < 0 || targetSeat >= MaxSeats) return;
            var target = playerSlots[targetSeat];
            if (target == null || !target.isAlive) return;

            CozyAudioManager.PlaySfx(CozySfxType.Gunshot, 1f);
            AddLog($"💥 ĐOÀNG! Phát súng của Thợ Săn đã bắn gục [{target.playerName}]!");
            KillPlayer(targetSeat, "bị Thợ Săn bắn gục trước lúc lâm chung 🏹");
            AddLog($"📜 Danh tính của [{target.playerName}]: [{WerewolfRoleInfo.GetNameVN(target.role)}] {WerewolfRoleInfo.GetIcon(target.role)}!");

            hunterRevengePlayerSeat = -1;
            AdvanceToNextPhase();
        }

        private void KillPlayer(int seatIndex, string reason)
        {
            if (seatIndex < 0 || seatIndex >= MaxSeats) return;
            var slot = playerSlots[seatIndex];
            if (slot == null) return;

            slot.isAlive = false;
            slot.deathDay = currentDayNumber;
            slot.deathReason = reason;

            // Xóa avatar ngồi nếu có
            if (campfireInstance != null && campfireInstance.Seats[seatIndex] != null)
            {
                campfireInstance.Seats[seatIndex].ClearSeatedAvatar();
            }
        }

        private bool CheckWinCondition()
        {
            int wolvesAlive = 0;
            int humansAlive = 0;

            for (int i = 0; i < MaxSeats; i++)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.isAlive)
                {
                    if (slot.role == WerewolfRole.Werewolf)
                        wolvesAlive++;
                    else
                        humansAlive++;
                }
            }

            if (wolvesAlive == 0)
            {
                winningTeam = WerewolfTeam.Village;
                return true;
            }
            if (wolvesAlive >= humansAlive)
            {
                winningTeam = WerewolfTeam.Werewolf;
                return true;
            }
            return false;
        }

        private void ResolveGameOver()
        {
            CozyAudioManager.PlaySfx(CozySfxType.Cheer, 1f);
            if (winningTeam == WerewolfTeam.Village)
            {
                AddLog("🎉🎉 PHE DÂN LÀNG CHIẾN THẮNG! Toàn bộ Ma Sói đã bị thanh trừng!");
            }
            else
            {
                AddLog("🐺🐺 PHE MA SÓI CHIẾN THẮNG! Bóng đêm và móng vuốt đã nuốt chửng thị trấn!");
            }

            // Trao thưởng Xu Cozy
            var local = GetLocalPlayerSlot();
            if (local != null)
            {
                var prof = FindAnyObjectByType<ProfessionAndInventory>();
                if (prof != null)
                {
                    bool isWinner = WerewolfRoleInfo.GetTeam(local.role) == winningTeam;
                    int reward = isWinner ? 50 : 20;
                    prof.AddCoins(reward);
                    CozyLifeHUD.PushToast($"💰 Nhận thưởng Ma Sói: +{reward} Xu Cozy!");
                }
            }
        }
        #endregion

        #region Multiplayer Sync & Networking
        private void SendNetworkEvent(string action, int targetSeat = -1, int phaseVal = -1, int extraVal = 0, string extraStr = "")
        {
            var net = CozyNetworkManager.Instance;
            if (net == null || !CozyNetworkManager.IsOnline) return;

            var local = GetLocalPlayerSlot();
            int mySeat = local != null ? local.seatIndex : 0;

            var packet = new WerewolfNetworkPacket
            {
                action = action,
                senderSeat = mySeat,
                targetSeat = targetSeat,
                phase = phaseVal,
                extraInt = extraVal,
                extraString = extraStr
            };

            string json = JsonUtility.ToJson(packet);
            net.BroadcastWerewolfEvent(json);
        }

        private void HandleIncomingNetworkPacket(string payloadJson)
        {
            try
            {
                var packet = JsonUtility.FromJson<WerewolfNetworkPacket>(payloadJson);
                if (packet == null) return;

                switch (packet.action)
                {
                    case "TakeSeat":
                        TakeSeat(packet.senderSeat, isLocal: false, isBot: false, playerName: packet.extraString);
                        break;
                    case "LeaveSeat":
                        VacateSeat(packet.senderSeat);
                        break;
                    case "StartGame":
                        if (currentPhase == WerewolfPhase.Lobby)
                        {
                            StartMatch();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WerewolfNet] Lỗi giải mã gói tin: {ex.Message}");
            }
        }
        #endregion

        #region Logging Helper
        private void AddLog(string msg)
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            matchLog.Add($"[{timeStr}] {msg}");
            logScrollPos.y = float.MaxValue;
        }
        #endregion

        #endregion

        #region OnGUI Interactive Werewolf Interface
        private void OnGUI()
        {
            if (!isUIOpen) return;

            GUI.depth = -30;
            DrawAtmosphericBackground();

            switch (currentPhase)
            {
                case WerewolfPhase.Lobby:
                    DrawLobbyUI();
                    break;
                case WerewolfPhase.RoleReveal:
                    DrawRoleRevealUI();
                    break;
                case WerewolfPhase.NightFall:
                case WerewolfPhase.NightGuard:
                case WerewolfPhase.NightWerewolf:
                case WerewolfPhase.NightWitch:
                case WerewolfPhase.NightSeer:
                    DrawNightPhaseUI();
                    break;
                case WerewolfPhase.DayDawn:
                case WerewolfPhase.HunterDawnShot:
                case WerewolfPhase.DayDiscussion:
                case WerewolfPhase.DayVoting:
                case WerewolfPhase.DayExecution:
                case WerewolfPhase.HunterExecutionShot:
                    DrawDayPhaseUI();
                    break;
                case WerewolfPhase.GameOver:
                    DrawGameOverUI();
                    break;
            }
        }

        private void DrawAtmosphericBackground()
        {
            Color bgColor = (currentPhase >= WerewolfPhase.NightFall && currentPhase <= WerewolfPhase.NightSeer)
                ? new Color(0.04f, 0.06f, 0.12f, 0.95f) // Đêm trăng huyền bí
                : new Color(0.08f, 0.11f, 0.15f, 0.94f); // Ban ngày / Lobby ấm cúng

            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), bgColor);

            // Viền vàng kim tinh tế
            Rect frame = new Rect(16f, 16f, Screen.width - 32f, Screen.height - 32f);
            DrawBorderRect(frame, new Color(0.85f, 0.65f, 0.28f, 0.65f), 2f);
        }

        #region 1. Lobby Window
        private void DrawLobbyUI()
        {
            float w = Mathf.Min(780f, Screen.width - 40f);
            float h = Mathf.Min(560f, Screen.height - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Tiêu đề
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.45f) }
            };
            GUI.Label(new Rect(x, y + 16f, w, 32f), "🔥 BÀN LỬA TRẠI MA SÓI THỊ TRẤN (4 - 8 NGƯỜI)", titleStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            int count = GetOccupiedSeatCount();
            GUI.Label(new Rect(x, y + 48f, w, 22f), $"Số người đã ngồi vào ghế: {count}/8 • Cần tối thiểu 4 người để khai cuộc", subStyle);

            // 8 Ghế hiển thị theo 2 hàng x 4 cột
            float gridX = x + 30f;
            float gridY = y + 80f;
            float slotW = (w - 60f - 30f) / 4f;
            float slotH = 92f;

            for (int i = 0; i < MaxSeats; i++)
            {
                int row = i / 4;
                int col = i % 4;
                Rect slotRect = new Rect(gridX + col * (slotW + 10f), gridY + row * (slotH + 12f), slotW, slotH);

                var slot = playerSlots[i];
                bool isOccupied = slot != null;

                Color cardBg = isOccupied
                    ? (slot.isLocal ? new Color(0.20f, 0.40f, 0.25f, 0.85f) : new Color(0.18f, 0.24f, 0.35f, 0.85f))
                    : new Color(0.12f, 0.15f, 0.20f, 0.70f);

                DrawSolidRect(slotRect, cardBg);
                DrawBorderRect(slotRect, isOccupied ? new Color(0.80f, 0.70f, 0.35f) : new Color(0.40f, 0.45f, 0.50f), 1.5f);

                GUIStyle sTitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = isOccupied ? Color.white : new Color(0.6f, 0.6f, 0.6f) }
                };
                GUI.Label(new Rect(slotRect.x + 8f, slotRect.y + 6f, slotRect.width - 16f, 18f), $"🪑 Ghế #{i + 1}", sTitle);

                if (isOccupied)
                {
                    string tag = slot.isLocal ? " [BẠN]" : (slot.isBot ? " [AI]" : " [ONLINE]");
                    GUI.Label(new Rect(slotRect.x + 8f, slotRect.y + 26f, slotRect.width - 16f, 36f), $"{slot.playerName}\n{tag}");

                    if (slot.isLocal || slot.isBot)
                    {
                        if (GUI.Button(new Rect(slotRect.x + 8f, slotRect.y + slotRect.height - 24f, slotRect.width - 16f, 18f), "Rời ghế"))
                        {
                            VacateSeat(i);
                        }
                    }
                }
                else
                {
                    GUI.Label(new Rect(slotRect.x + 8f, slotRect.y + 30f, slotRect.width - 16f, 20f), "Trống");
                    if (GUI.Button(new Rect(slotRect.x + 8f, slotRect.y + slotRect.height - 24f, slotRect.width - 16f, 18f), "Ngồi vào"))
                    {
                        var pc = PlayerController.LocalPlayer;
                        string pName = pc != null ? pc.GetComponent<ProfessionAndInventory>()?.PlayerName ?? "Bạn" : "Bạn";
                        TakeSeat(i, isLocal: true, isBot: false, playerName: pName);
                        if (pc != null && campfireInstance != null && campfireInstance.Seats[i] != null)
                        {
                            pc.SitAtCampfireSeat(campfireInstance.Seats[i]);
                        }
                    }
                }
            }

            // Thanh Nhật Ký & Hướng Dẫn
            float logY = gridY + 2 * slotH + 28f;
            float logH = 110f;
            Rect logRect = new Rect(gridX, logY, w - 60f, logH);
            DrawSolidRect(logRect, new Color(0.05f, 0.08f, 0.12f, 0.85f));
            DrawBorderRect(logRect, new Color(0.35f, 0.40f, 0.45f), 1f);

            GUILayout.BeginArea(new Rect(logRect.x + 6f, logRect.y + 6f, logRect.width - 12f, logRect.height - 12f));
            logScrollPos = GUILayout.BeginScrollView(logScrollPos);
            foreach (var log in matchLog)
            {
                GUILayout.Label(log);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Nút bấm hành động dưới cùng
            float btnY = logY + logH + 14f;
            float btnW = 160f;

            if (GUI.Button(new Rect(gridX, btnY, btnW, 36f), "🤖 Mời Bot Điền Chỗ"))
            {
                FillRemainingSeatsWithBots();
            }

            if (GUI.Button(new Rect(gridX + btnW + 12f, btnY, 120f, 36f), "🧹 Xóa Bot"))
            {
                ClearAllBots();
            }

            GUI.enabled = count >= 4;
            GUI.color = count >= 4 ? new Color(0.40f, 1f, 0.40f) : Color.gray;
            if (GUI.Button(new Rect(gridX + (w - 60f) - 190f, btnY, 190f, 36f), $"🚀 BẮT ĐẦU ({count}/8)"))
            {
                StartMatch();
            }
            GUI.color = Color.white;
            GUI.enabled = true;

            // Nút đóng góc trên
            if (GUI.Button(new Rect(x + w - 38f, y + 12f, 26f, 26f), "✕"))
            {
                CloseWerewolfUI();
            }
        }
        #endregion

        #region 2. Role Reveal Window
        private void DrawRoleRevealUI()
        {
            var local = GetLocalPlayerSlot();
            if (local == null)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f - 20f, 300f, 40f), "Đang phân phát lá bài bí mật...");
                return;
            }

            float cardW = 280f;
            float cardH = 390f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f - 10f;

            // Khung thẻ bài bí mật
            DrawSolidRect(new Rect(cardX, cardY, cardW, cardH), new Color(0.12f, 0.16f, 0.22f, 0.98f));
            DrawBorderRect(new Rect(cardX, cardY, cardW, cardH), WerewolfRoleInfo.GetTeamColor(local.role), 3f);

            // Ảnh Lá Bài Pixel Art
            if (roleCardTextures.TryGetValue(local.role, out Texture2D cardTex) && cardTex != null)
            {
                GUI.DrawTexture(new Rect(cardX + 20f, cardY + 20f, cardW - 40f, 140f), cardTex, ScaleMode.ScaleToFit);
            }

            // Tên vai trò
            GUIStyle roleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = WerewolfRoleInfo.GetTeamColor(local.role) }
            };
            GUI.Label(new Rect(cardX + 10f, cardY + 170f, cardW - 20f, 28f), $"{WerewolfRoleInfo.GetIcon(local.role)} {WerewolfRoleInfo.GetNameVN(local.role)}", roleStyle);

            GUIStyle teamStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(cardX + 10f, cardY + 200f, cardW - 20f, 20f), WerewolfRoleInfo.GetTeamNameVN(local.role), teamStyle);

            // Mô tả khả năng
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUI.Label(new Rect(cardX + 18f, cardY + 230f, cardW - 36f, 110f), WerewolfRoleInfo.GetDescriptionVN(local.role), descStyle);

            // Countdown đếm ngược
            GUIStyle timeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(cardX, cardY + cardH - 32f, cardW, 24f), $"Màn đêm buông xuống sau: {Mathf.CeilToInt(phaseTimer)}s", timeStyle);
        }
        #endregion

        #region 3. Night Phase Window
        private void DrawNightPhaseUI()
        {
            float w = Mathf.Min(820f, Screen.width - 40f);
            float h = Mathf.Min(580f, Screen.height - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Banner Đêm
            GUIStyle nightTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.70f, 0.85f, 1.0f) }
            };
            GUI.Label(new Rect(x, y + 16f, w, 32f), $"🌙 ĐÊM THỨ #{currentDayNumber} — LỬA TRẠI THỊ TRẤN", nightTitle);

            var local = GetLocalPlayerSlot();
            bool isDead = local != null && !local.isAlive;

            // Xác định xem đêm này có phải lượt của Local Player hay không
            bool isMyTurn = false;
            string promptText = "Trời tối rồi, dân làng hãy đi ngủ...";

            if (!isDead && local != null)
            {
                if (currentPhase == WerewolfPhase.NightGuard && local.role == WerewolfRole.Guard)
                {
                    isMyTurn = true;
                    promptText = "🛡️ BẢO VỆ: Hãy chọn 1 người để bảo vệ đêm nay!";
                }
                else if (currentPhase == WerewolfPhase.NightWerewolf && local.role == WerewolfRole.Werewolf)
                {
                    isMyTurn = true;
                    promptText = "🐺 MA SÓI: Hãy chọn 1 nạn nhân để cắn đêm nay!";
                }
                else if (currentPhase == WerewolfPhase.NightWitch && local.role == WerewolfRole.Witch)
                {
                    isMyTurn = true;
                    promptText = "🧙 PHÙ THỦY: Hãy quyết định cứu hoặc đầu độc!";
                }
                else if (currentPhase == WerewolfPhase.NightSeer && local.role == WerewolfRole.Seer)
                {
                    isMyTurn = true;
                    promptText = "🔮 TIÊN TRI: Hãy chọn 1 người để soi danh tính bí mật!";
                }
            }

            GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isMyTurn ? Color.yellow : new Color(0.75f, 0.75f, 0.75f) }
            };
            GUI.Label(new Rect(x, y + 52f, w, 24f), promptText, promptStyle);

            // Bàn tròn lửa trại thể hiện 8 người chơi
            DrawSeatingCircle(x + w * 0.5f, y + 230f, 170f, isNight: true, canSelect: isMyTurn);

            // Hành động riêng biệt nếu là lượt của Local Player
            if (isMyTurn && local != null)
            {
                DrawNightActionButtons(x, y + 420f, w, local);
            }

            // Thanh tiến trình đêm
            GUIStyle timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            GUI.Label(new Rect(x, y + h - 30f, w, 20f), $"Thời gian còn lại: {Mathf.CeilToInt(phaseTimer)}s", timerStyle);
        }

        private void DrawNightActionButtons(float x, float y, float w, WerewolfPlayerSlot local)
        {
            if (local.role == WerewolfRole.Witch)
            {
                float btnW = 190f;
                float startX = x + (w - (btnW * 3 + 24f)) * 0.5f;

                // Nút Cứu
                GUI.enabled = !local.hasUsedHealPotion && wolfVotedTargetSeat >= 0;
                string healTitle = local.hasUsedHealPotion ? "Đã dùng Bình Cứu" : (wolfVotedTargetSeat >= 0 ? $"Cứu [{playerSlots[wolfVotedTargetSeat].playerName}]" : "Không ai bị cắn");
                if (GUI.Button(new Rect(startX, y, btnW, 36f), $"🧪 {healTitle}"))
                {
                    SubmitLocalWitchAction(useHeal: true);
                }

                // Nút Độc
                GUI.enabled = !local.hasUsedPoisonPotion;
                string poisonTitle = local.hasUsedPoisonPotion ? "Đã dùng Bình Độc" : "Dùng Bình Độc (Chọn người)";
                if (GUI.Button(new Rect(startX + btnW + 12f, y, btnW, 36f), $"☠️ {poisonTitle}"))
                {
                    // Chọn người đầu độc
                    for (int i = 0; i < MaxSeats; i++)
                    {
                        if (playerSlots[i] != null && playerSlots[i].isAlive && i != local.seatIndex)
                        {
                            SubmitLocalWitchAction(useHeal: false, poisonSeat: i);
                            break;
                        }
                    }
                }

                // Nút Bỏ qua
                GUI.enabled = true;
                if (GUI.Button(new Rect(startX + (btnW + 12f) * 2, y, btnW, 36f), "Bỏ qua đêm nay"))
                {
                    CozyLifeHUD.PushToast("🧙 Phù Thủy đã chọn không dùng phép đêm nay.");
                }
                GUI.enabled = true;
            }
            else if (local.role == WerewolfRole.Seer && !string.IsNullOrEmpty(seerInspectResult))
            {
                GUIStyle resStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };
                GUI.Box(new Rect(x + 50f, y, w - 100f, 40f), seerInspectResult, resStyle);
            }
        }
        #endregion

        #region 4. Day Phase Window (Dawn, Discussion, Voting, Execution)
        private void DrawDayPhaseUI()
        {
            float w = Mathf.Min(840f, Screen.width - 40f);
            float h = Mathf.Min(600f, Screen.height - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            string phaseTitle = currentPhase switch
            {
                WerewolfPhase.DayDawn => "☀️ BÌNH MINH LÓ RẠNG — CÔNG BỐ KẾT QUẢ ĐÊM",
                WerewolfPhase.HunterDawnShot => "🏹 PHÁT SÚNG BÁO THÙ CỦA THỢ SĂN!",
                WerewolfPhase.DayDiscussion => "💬 THẢO LUẬN BAN NGÀY — AI LÀ MA SÓI?",
                WerewolfPhase.DayVoting => "⚖️ BIỂU QUYẾT TREO CỔ KẺ TÌNH NGHI",
                WerewolfPhase.DayExecution => "⚖️ KẾT QUẢ HÀNH QUYẾT & DANH TÍNH",
                WerewolfPhase.HunterExecutionShot => "🏹 PHÁT SÚNG TRẢ THÙ CỦA THỢ SĂN!",
                _ => "BAN NGÀY"
            };

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.90f, 0.40f) }
            };
            GUI.Label(new Rect(x, y + 14f, w, 28f), $"{phaseTitle} (Ngày #{currentDayNumber})", titleStyle);

            // Bàn tròn ghế ngồi 8 người chơi
            bool canVote = (currentPhase == WerewolfPhase.DayVoting);
            DrawSeatingCircle(x + 220f, y + 210f, 155f, isNight: false, canSelect: canVote);

            // Bảng Nhật Ký & Đoạn Chat bên phải
            float logW = w - 460f;
            float logH = 340f;
            Rect logBox = new Rect(x + w - logW - 20f, y + 60f, logW, logH);
            DrawSolidRect(logBox, new Color(0.06f, 0.09f, 0.13f, 0.90f));
            DrawBorderRect(logBox, new Color(0.40f, 0.45f, 0.50f), 1.5f);

            GUILayout.BeginArea(new Rect(logBox.x + 8f, logBox.y + 8f, logBox.width - 16f, logBox.height - 16f));
            logScrollPos = GUILayout.BeginScrollView(logScrollPos);
            foreach (var log in matchLog)
            {
                GUILayout.Label(log);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Nếu đang trong lượt Thợ Săn nổ súng
            var local = GetLocalPlayerSlot();
            if ((currentPhase == WerewolfPhase.HunterDawnShot || currentPhase == WerewolfPhase.HunterExecutionShot))
            {
                if (local != null && local.seatIndex == hunterRevengePlayerSeat)
                {
                    GUIStyle hunterStyle = new GUIStyle(GUI.skin.box)
                    {
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.yellow }
                    };
                    GUI.Box(new Rect(x + 30f, y + 420f, w - 60f, 40f), "🏹 Bạn là Thợ Săn! Hãy nhấp chọn 1 người trên bàn tròn để nổ phát súng kéo theo cùng chết!", hunterStyle);
                }
            }

            // Nút Bỏ Phiếu Trắng trong pha Voting
            if (currentPhase == WerewolfPhase.DayVoting && local != null && local.isAlive)
            {
                float btnX = x + 30f;
                float btnY = y + 430f;
                if (GUI.Button(new Rect(btnX, btnY, 200f, 36f), "⚪ Bỏ Phiếu Trắng (Không Treo)"))
                {
                    SubmitLocalVote(-2);
                }
            }

            // Nút Bỏ Qua Thảo Luận nhanh
            if (currentPhase == WerewolfPhase.DayDiscussion)
            {
                if (GUI.Button(new Rect(x + 30f, y + 430f, 220f, 36f), "⚡ Bỏ Qua -> Tiến Tới Bỏ Phiếu"))
                {
                    AdvanceToNextPhase();
                }
            }

            // Đồng hồ đếm ngược
            GUIStyle timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            GUI.Label(new Rect(x, y + h - 28f, w, 20f), $"Giai đoạn kết thúc sau: {Mathf.CeilToInt(phaseTimer)}s", timerStyle);
        }
        #endregion

        #region 5. Game Over Window
        private void DrawGameOverUI()
        {
            float w = Mathf.Min(780f, Screen.width - 40f);
            float h = Mathf.Min(560f, Screen.height - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            Color winColor = winningTeam == WerewolfTeam.Village ? new Color(0.35f, 0.90f, 0.45f) : new Color(0.95f, 0.30f, 0.30f);
            string winTitle = winningTeam == WerewolfTeam.Village
                ? "🎉 PHE DÂN LÀNG CHIẾN THẮNG!"
                : "🐺 PHE MA SÓI CHIẾN THẮNG!";

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = winColor }
            };
            GUI.Label(new Rect(x, y + 20f, w, 36f), winTitle, titleStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(x, y + 58f, w, 22f), "BẢNG CÔNG BỐ DANH TÍNH BÍ MẬT CỦA TOÀN BỘ NGƯỜI CHƠI", subStyle);

            // Bảng danh sách 8 người chơi và vai trò
            float gridX = x + 30f;
            float gridY = y + 90f;
            float slotW = (w - 60f - 30f) / 4f;
            float slotH = 130f;

            for (int i = 0; i < MaxSeats; i++)
            {
                int row = i / 4;
                int col = i % 4;
                Rect slotRect = new Rect(gridX + col * (slotW + 10f), gridY + row * (slotH + 12f), slotW, slotH);

                var slot = playerSlots[i];
                if (slot == null)
                {
                    DrawSolidRect(slotRect, new Color(0.1f, 0.12f, 0.16f, 0.5f));
                    DrawBorderRect(slotRect, Color.gray, 1f);
                    GUI.Label(slotRect, $"Ghế #{i + 1}\nTrống", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                    continue;
                }

                Color c = WerewolfRoleInfo.GetTeamColor(slot.role);
                DrawSolidRect(slotRect, new Color(0.12f, 0.16f, 0.22f, 0.95f));
                DrawBorderRect(slotRect, c, 2f);

                // Role Card Icon
                if (roleCardTextures.TryGetValue(slot.role, out Texture2D cTex) && cTex != null)
                {
                    GUI.DrawTexture(new Rect(slotRect.x + slotRect.width * 0.5f - 24f, slotRect.y + 6f, 48f, 48f), cTex, ScaleMode.ScaleToFit);
                }

                GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = slot.isAlive ? Color.white : new Color(0.6f, 0.6f, 0.6f) }
                };
                string lifeIcon = slot.isAlive ? "💚 Còn Sống" : "💀 Hy Sinh";
                GUI.Label(new Rect(slotRect.x + 4f, slotRect.y + 56f, slotRect.width - 8f, 18f), slot.playerName, nameStyle);

                GUIStyle rStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = c }
                };
                GUI.Label(new Rect(slotRect.x + 4f, slotRect.y + 74f, slotRect.width - 8f, 18f), $"{WerewolfRoleInfo.GetIcon(slot.role)} {WerewolfRoleInfo.GetNameVN(slot.role)}", rStyle);

                GUI.Label(new Rect(slotRect.x + 4f, slotRect.y + 94f, slotRect.width - 8f, 28f), $"{lifeIcon}\n{(slot.isAlive ? "" : slot.deathReason)}", new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
            }

            // Nút chơi lại & Thoát
            float btnY = y + h - 55f;
            if (GUI.Button(new Rect(x + w * 0.5f - 170f, btnY, 160f, 38f), "🔄 Chơi Ván Mới"))
            {
                currentPhase = WerewolfPhase.Lobby;
                for (int i = 0; i < MaxSeats; i++)
                {
                    if (playerSlots[i] != null)
                    {
                        playerSlots[i].isAlive = true;
                        playerSlots[i].role = WerewolfRole.Villager;
                    }
                }
            }

            if (GUI.Button(new Rect(x + w * 0.5f + 10f, btnY, 160f, 38f), "🚪 Rời Lửa Trại"))
            {
                CloseWerewolfUI();
                PlayerController.LocalPlayer?.StandUpFromCampfireSeat();
            }
        }
        #endregion

        #region Seating Circle Drawing Helper
        private void DrawSeatingCircle(float centerX, float centerY, float radius, bool isNight, bool canSelect)
        {
            // Vẽ biểu tượng lửa trại ở giữa bàn tròn
            if (campfireTexture != null)
            {
                float fireW = 64f;
                float fireH = 64f;
                GUI.DrawTexture(new Rect(centerX - fireW * 0.5f, centerY - fireH * 0.5f, fireW, fireH), campfireTexture, ScaleMode.ScaleToFit);
            }

            var local = GetLocalPlayerSlot();

            for (int i = 0; i < MaxSeats; i++)
            {
                float angleDeg = i * (360f / MaxSeats) - 90f; // Bắt đầu từ đỉnh 12h
                float rad = angleDeg * Mathf.Deg2Rad;
                float px = centerX + Mathf.Cos(rad) * radius;
                float py = centerY + Mathf.Sin(rad) * (radius * 0.72f); // Isometric ellipse

                var slot = playerSlots[i];
                bool isOccupied = slot != null;

                float nodeW = 76f;
                float nodeH = 54f;
                Rect nodeRect = new Rect(px - nodeW * 0.5f, py - nodeH * 0.5f, nodeW, nodeH);

                Color nodeBg = isOccupied
                    ? (slot.isAlive ? new Color(0.16f, 0.22f, 0.32f, 0.90f) : new Color(0.25f, 0.12f, 0.12f, 0.85f))
                    : new Color(0.10f, 0.12f, 0.16f, 0.60f);

                DrawSolidRect(nodeRect, nodeBg);

                Color borderCol = isOccupied
                    ? (slot.isLocal ? Color.green : (slot.isAlive ? new Color(0.85f, 0.75f, 0.35f) : Color.red))
                    : Color.gray;

                // Nếu người chơi này được bầu chọn / bảo vệ / cắn
                if (canSelect && isOccupied && slot.isAlive)
                {
                    if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition))
                    {
                        OnCircleNodeClicked(i);
                        Event.current.Use();
                    }
                }

                DrawBorderRect(nodeRect, borderCol, 1.5f);

                if (isOccupied)
                {
                    string statusIcon = slot.isAlive ? (slot.isLocal ? "⭐" : "👤") : "💀";
                    string nameText = slot.playerName;
                    if (nameText.Length > 9) nameText = nameText.Substring(0, 8) + "..";

                    GUIStyle nStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = slot.isAlive ? Color.white : new Color(0.7f, 0.7f, 0.7f) }
                    };
                    GUI.Label(new Rect(nodeRect.x, nodeRect.y + 4f, nodeRect.width, 16f), $"{statusIcon} #{i + 1}", nStyle);
                    GUI.Label(new Rect(nodeRect.x, nodeRect.y + 18f, nodeRect.width, 18f), nameText, nStyle);

                    // Thể hiện vai trò nếu là bản thân hoặc cùng phe Sói ban đêm
                    if (local != null && (local.seatIndex == i || (local.role == WerewolfRole.Werewolf && slot.role == WerewolfRole.Werewolf)))
                    {
                        GUI.Label(new Rect(nodeRect.x, nodeRect.y + 34f, nodeRect.width, 16f), $"{WerewolfRoleInfo.GetIcon(slot.role)}", new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter });
                    }
                    else if (!slot.isAlive && currentPhase >= WerewolfPhase.DayDawn)
                    {
                        GUI.Label(new Rect(nodeRect.x, nodeRect.y + 34f, nodeRect.width, 16f), "Đã chết", new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.red } });
                    }
                }
                else
                {
                    GUI.Label(nodeRect, $"#{i + 1}\nTrống", new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter });
                }
            }
        }

        private void OnCircleNodeClicked(int seatIndex)
        {
            var local = GetLocalPlayerSlot();
            if (local == null || !local.isAlive) return;

            var target = playerSlots[seatIndex];
            if (target == null || !target.isAlive) return;

            if (currentPhase == WerewolfPhase.NightGuard && local.role == WerewolfRole.Guard)
            {
                if (seatIndex != local.lastProtectedSeat)
                {
                    SubmitLocalGuardAction(seatIndex);
                }
                else
                {
                    CozyLifeHUD.PushToast("⚠️ Không thể bảo vệ cùng 1 người 2 đêm liên tiếp!");
                }
            }
            else if (currentPhase == WerewolfPhase.NightWerewolf && local.role == WerewolfRole.Werewolf)
            {
                SubmitLocalWerewolfAction(seatIndex);
            }
            else if (currentPhase == WerewolfPhase.NightSeer && local.role == WerewolfRole.Seer)
            {
                SubmitLocalSeerAction(seatIndex);
            }
            else if (currentPhase == WerewolfPhase.DayVoting)
            {
                SubmitLocalVote(seatIndex);
            }
            else if ((currentPhase == WerewolfPhase.HunterDawnShot || currentPhase == WerewolfPhase.HunterExecutionShot) && local.seatIndex == hunterRevengePlayerSeat)
            {
                SubmitHunterShot(seatIndex);
            }
        }
        #endregion

        #region GUI Drawing Primitives
        private void DrawSolidRect(Rect rect, Color color)
        {
            if (blankWhiteTex == null) CreateBlankWhiteTex();
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, blankWhiteTex);
            GUI.color = old;
        }

        private void DrawBorderRect(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
        #endregion // GUI Drawing Primitives

        #endregion // OnGUI Interactive Werewolf Interface

        #endregion // Werewolf Match Manager
    }
}
