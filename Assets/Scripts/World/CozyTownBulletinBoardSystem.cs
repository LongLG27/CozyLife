using System;
using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    [Serializable]
    public class TownDailyQuest
    {
        public string questId;
        public string requesterName;
        public string requesterRole;
        public string questDescription;
        public ItemType requiredItem;
        public int requiredCount;
        public int rewardCoins;
        public int rewardReputation;
        public bool isCompleted;
    }

    /// <summary>
    /// Bảng Thông Báo & Nhiệm Vụ Cư Dân Thị Trấn (Town Daily Bulletin Board System).
    /// Đặt tại Quảng Trường Trung Tâm, mỗi ngày mới cư dân (An, Minh, Lan, Hùng, Yuki)
    /// sẽ ghim các nhiệm vụ nhờ vả để người chơi thực hiện nhận Xu & Điểm Thân Thiết!
    /// </summary>
    public class TownBulletinBoardInteractable : WorldInteractableBase
    {
        public static TownBulletinBoardInteractable Instance { get; private set; }
        public static bool IsAnyModalOpen => Instance != null && Instance.isModalOpen;

        private readonly List<TownDailyQuest> dailyQuests = new List<TownDailyQuest>();
        private int lastGeneratedDay = -1;
        private bool isModalOpen = false;

        private void Awake()
        {
            Instance = this;
            interactTitle = "Bảng Thông Báo Cư Dân Thị Trấn";
            stationType = StationType.None;
        }

        private void Start()
        {
            EnsureDailyQuestsForDay();
        }

        private void Update()
        {
            if (CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.CurrentDay != lastGeneratedDay)
            {
                EnsureDailyQuestsForDay();
            }

            if (isModalOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    CloseModal();
                }
            }
        }

        public void EnsureDailyQuestsForDay()
        {
            int day = (CozyTownWorldBuilder.Instance != null) ? CozyTownWorldBuilder.Instance.CurrentDay : 1;
            if (lastGeneratedDay == day && dailyQuests.Count > 0) return;

            lastGeneratedDay = day;
            dailyQuests.Clear();

            // 1. Nông Dân An
            dailyQuests.Add(new TownDailyQuest
            {
                questId = $"day_{day}_an",
                requesterName = "Nông Dân An",
                requesterRole = "Khu Nông Trại",
                questDescription = "Cần 2 Cà Chua tươi mọng để ép nước uống giải nhiệt cho bà con nông dân.",
                requiredItem = ItemType.Tomato,
                requiredCount = 2,
                rewardCoins = 35,
                rewardReputation = 15,
                isCompleted = false
            });

            // 2. Đầu Bếp Minh
            dailyQuests.Add(new TownDailyQuest
            {
                questId = $"day_{day}_minh",
                requesterName = "Đầu Bếp Minh",
                requesterRole = "Nhà Hàng",
                questDescription = "Cần 1 Cá Chép Vàng hoặc Cá Tươi để chuẩn bị món đặc sản tối nay.",
                requiredItem = ItemType.FishGoldenCarp,
                requiredCount = 1,
                rewardCoins = 50,
                rewardReputation = 20,
                isCompleted = false
            });

            // 3. Bác Sĩ Lan
            dailyQuests.Add(new TownDailyQuest
            {
                questId = $"day_{day}_lan",
                requesterName = "Bác Sĩ Lan",
                requesterRole = "Phòng Khám",
                questDescription = "Cần 1 Trà Thảo Mộc thơm dịu để điều trị cho bệnh nhân bị sốt nhẹ.",
                requiredItem = ItemType.HerbalTea,
                requiredCount = 1,
                rewardCoins = 45,
                rewardReputation = 20,
                isCompleted = false
            });

            // 4. Thợ Mộc Hùng
            dailyQuests.Add(new TownDailyQuest
            {
                questId = $"day_{day}_hung",
                requesterName = "Thợ Mộc Hùng",
                requesterRole = "Xưởng Mộc",
                questDescription = "Cần 3 Gỗ Thông để đóng nôi em bé và gia cố lan can công viên.",
                requiredItem = ItemType.WoodPlank,
                requiredCount = 3,
                rewardCoins = 45,
                rewardReputation = 20,
                isCompleted = false
            });
        }

        public override string GetWorldOverheadLabel()
        {
            int remaining = 0;
            foreach (var q in dailyQuests)
            {
                if (!q.isCompleted) remaining++;
            }
            return remaining > 0 ? $"📋 Bảng Nhiệm Vụ ({remaining}) [E]" : "📋 Bảng Nhiệm Vụ [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "[E] Mở Bảng Thông Báo Cư Dân Thị Trấn";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            CozyAudioManager.PlaySfx(CozySfxType.PageTurn);
            isModalOpen = !isModalOpen;
        }

        public void CloseModal()
        {
            isModalOpen = false;
        }

        private void OnGUI()
        {
            if (!isModalOpen) return;

            GUI.depth = -100;

            float modalW = 560f;
            float modalH = 460f;
            float mx = (Screen.width - modalW) * 0.5f;
            float my = (Screen.height - modalH) * 0.5f;

            Rect modalRect = new Rect(mx, my, modalW, modalH);

            // Nền bảng thông báo giả gỗ bọc nỉ ấm cúng
            GUI.color = new Color(0.12f, 0.10f, 0.08f, 0.94f);
            GUI.DrawTexture(modalRect, Texture2D.whiteTexture);

            GUI.color = new Color(0.85f, 0.65f, 0.40f, 1f);
            GUI.Box(modalRect, "");

            // Tiêu đề
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.90f, 0.65f);
            GUI.Label(new Rect(mx, my + 12, modalW, 28), "📋 BẢNG NHIỆM VỤ CƯ DÂN HÀNG NGÀY", titleStyle);

            int curDay = (CozyTownWorldBuilder.Instance != null) ? CozyTownWorldBuilder.Instance.CurrentDay : 1;
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            GUI.Label(new Rect(mx, my + 38, modalW, 20), $"Bản tin Thị Trấn - Ngày {curDay} • Hoàn thành để nhận Xu & Điểm Thân Thiết!", subStyle);

            // Nút đóng [X]
            if (GUI.Button(new Rect(mx + modalW - 36, my + 10, 26, 26), "✕"))
            {
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                CloseModal();
                return;
            }

            var player = FindAnyObjectByType<ProfessionAndInventory>();

            // Danh sách 4 nhiệm vụ
            float rowY = my + 66;
            float rowH = 82;
            for (int i = 0; i < dailyQuests.Count; i++)
            {
                var q = dailyQuests[i];
                Rect rowRect = new Rect(mx + 16, rowY + i * (rowH + 6), modalW - 32, rowH);

                Color rowBg = q.isCompleted
                    ? new Color(0.18f, 0.32f, 0.22f, 0.85f)
                    : new Color(0.20f, 0.18f, 0.16f, 0.85f);
                GUI.color = rowBg;
                GUI.DrawTexture(rowRect, Texture2D.whiteTexture);

                GUI.color = q.isCompleted ? Color.green : new Color(0.7f, 0.6f, 0.5f);
                GUI.Box(rowRect, "");
                GUI.color = Color.white;

                // Tên người nhờ & Vị trí
                GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
                nameStyle.normal.textColor = q.isCompleted ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.85f, 0.5f);
                GUI.Label(new Rect(rowRect.x + 12, rowRect.y + 8, 220, 22), $"{q.requesterName} ({q.requesterRole})", nameStyle);

                // Mô tả
                GUIStyle descStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
                descStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(rowRect.x + 12, rowRect.y + 30, rowRect.width - 150, 44), q.questDescription, descStyle);

                // Kiểm tra số lượng đồ trong túi
                int playerHas = (player != null) ? player.GetItemCount(q.requiredItem) : 0;
                string itemDisplay = CozyLifeDatabase.GetItem(q.requiredItem)?.displayName ?? q.requiredItem.ToString();

                Rect btnRect = new Rect(rowRect.x + rowRect.width - 132, rowRect.y + 16, 120, 48);

                if (q.isCompleted)
                {
                    GUI.color = new Color(0.4f, 1f, 0.5f);
                    GUI.Box(btnRect, "✅ ĐÃ XONG\n+Thân Thiết", GUI.skin.button);
                    GUI.color = Color.white;
                }
                else
                {
                    bool canDeliver = playerHas >= q.requiredCount;
                    GUI.color = canDeliver ? new Color(0.35f, 0.95f, 0.45f) : new Color(0.65f, 0.65f, 0.65f);

                    string btnText = canDeliver
                        ? $"GIAO ĐỒ [E]\n({playerHas}/{q.requiredCount}) • +{q.rewardCoins} Xu"
                        : $"CẦN: {q.requiredCount} {itemDisplay}\n(Có: {playerHas}/{q.requiredCount})";

                    if (GUI.Button(btnRect, btnText))
                    {
                        if (canDeliver && player != null)
                        {
                            player.RemoveItem(q.requiredItem, q.requiredCount);
                            player.AddCoins(q.rewardCoins);
                            q.isCompleted = true;

                            CozyAudioManager.PlaySfx(CozySfxType.Coin);
                            CozyAudioManager.PlaySfx(CozySfxType.Cheer);
                            CozyLifeHUD.PushToast($"🎉 Hoàn thành nhiệm vụ của {q.requesterName}! +{q.rewardCoins} Xu & Tăng Uy Tín!");
                            CozyDiarySystem.RecordBoardQuestCompleted();
                        }
                        else
                        {
                            CozyLifeHUD.PushToast($"⚠️ Bạn chưa gom đủ {q.requiredCount} {itemDisplay} trong túi!");
                        }
                    }
                    GUI.color = Color.white;
                }
            }
        }
    }
}
