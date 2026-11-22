using System.Collections.Generic;
using UnityEngine;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.Core
{
    /// <summary>
    /// Sổ Tay & Thành Tựu Cuộc Sống (Cozy Life Diary & Milestones):
    /// - Mở / Đóng nhanh bằng phím tắt [J] (Journal) hoặc nút trên giao diện.
    /// - Theo dõi 10 dấu mốc cuộc sống thị trấn yên bình:
    ///   1. Ngư Phủ Dạo Hồ
    ///   2. Bí Ẩn Ánh Trăng
    ///   3. Tổ Ấm Trong Mơ
    ///   4. Bạn Tri Kỷ Bé Vàng
    ///   5. Vòng Quay Thị Trấn (Đi xe đạp)
    ///   6. Mùa Vàng Bội Thu
    ///   7. Thực Khách Tinh Tế (Cafe Nhật)
    ///   8. Thư Giãn Suối Khoáng (Onsen)
    ///   9. Kỳ Thủ Công Viên (Cờ Vua / Cờ Tướng)
    ///   10. Cư Dân Gương Mẫu (Bảng tin thị trấn)
    /// - Nhận thưởng Xu và âm thanh chúc mừng hân hoan khi hoàn thành!
    /// </summary>
    public class CozyDiarySystem : MonoBehaviour
    {
        public static CozyDiarySystem Instance { get; private set; }

        public class Milestone
        {
            public string id;
            public string title;
            public string description;
            public int rewardCoins;
            public bool isUnlocked;
            public bool isClaimed;

            public Milestone(string id, string title, string description, int rewardCoins)
            {
                this.id = id;
                this.title = title;
                this.description = description;
                this.rewardCoins = rewardCoins;
                this.isUnlocked = PlayerPrefs.GetInt($"CozyDiary_U_{id}", 0) == 1;
                this.isClaimed = PlayerPrefs.GetInt($"CozyDiary_C_{id}", 0) == 1;
            }

            public void Unlock()
            {
                if (!isUnlocked)
                {
                    isUnlocked = true;
                    PlayerPrefs.SetInt($"CozyDiary_U_{id}", 1);
                    PlayerPrefs.Save();
                }
            }

            public void Claim()
            {
                isClaimed = true;
                PlayerPrefs.SetInt($"CozyDiary_C_{id}", 1);
                PlayerPrefs.Save();
            }
        }

        private readonly List<Milestone> milestones = new List<Milestone>();
        private bool isDiaryOpen = false;
        private Vector2 scrollPos = Vector2.zero;

        public bool IsDiaryOpen => isDiaryOpen;
        public static bool IsAnyModalOpen => Instance != null && Instance.isDiaryOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("CozyDiarySystem");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CozyDiarySystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitMilestones();
        }

        private void InitMilestones()
        {
            milestones.Clear();
            milestones.Add(new Milestone("fish_first", "🎣 Ngư Phủ Dạo Hồ", "Câu thành công con cá đầu tiên dưới làn nước hồ thị trấn.", 50));
            milestones.Add(new Milestone("fish_moonlight", "🌙 Bí Ẩn Ánh Trăng", "Bắt được loài Cá Ánh Trăng phát sáng huyền ảo trong đêm muộn.", 150));
            milestones.Add(new Milestone("home_cozy", "🛋️ Tổ Ấm Trong Mơ", "Trang trí nhà riêng đạt điểm Ấm Cúng từ 60 điểm trở lên.", 100));
            milestones.Add(new Milestone("pet_friend", "🐶 Bạn Tri Kỷ Bé Vàng", "Đạt Mức Thân Thiết Cấp 2 với cún cưng Bé Vàng.", 80));
            milestones.Add(new Milestone("bicycle_ride", "🚴 Vòng Quay Thị Trấn", "Sở hữu và đạp chiếc xe đạp dạo quanh các con phố thị trấn.", 50));
            milestones.Add(new Milestone("crop_harvest", "🌾 Mùa Vàng Bội Thu", "Chăm bón và thu hoạch thành công một vụ mùa nông sản tươi tốt.", 40));
            milestones.Add(new Milestone("cafe_visit", "🍵 Thực Khách Tinh Tế", "Ghé thăm và thưởng thức ẩm thực tại Quán Cafe & Trà Đạo Nhật Bản.", 60));
            milestones.Add(new Milestone("onsen_bath", "♨️ Thư Giãn Suối Khoáng", "Ngâm mình thư thái 30 phút trong làn nước Suối Nước Nóng Onsen.", 50));
            milestones.Add(new Milestone("board_game", "♟️ Kỳ Thủ Công Viên", "Tham gia so tài một ván Cờ Vua hoặc Cờ Tướng tại kỳ đài công viên.", 60));
            milestones.Add(new Milestone("town_board", "📋 Cư Dân Gương Mẫu", "Hoàn thành một yêu cầu của cư dân thị trấn trên Bảng Tin.", 80));
        }

        private void Update()
        {
            // Bấm [J] để mở / đóng Sổ Tay
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame)
                {
                    ToggleDiary();
                }
                else if (isDiaryOpen && kb.escapeKey.wasPressedThisFrame)
                {
                    isDiaryOpen = false;
                    CozyAudioManager.PlaySfx(CozySfxType.PageTurn);
                }
            }
        }

        public void ToggleDiary()
        {
            isDiaryOpen = !isDiaryOpen;
            CozyAudioManager.PlaySfx(CozySfxType.PageTurn);
        }

        public static void RecordFishCaught(ItemType fish)
        {
            EnsureInstance();
            Instance?.TriggerUnlock("fish_first");
            if (fish == ItemType.FishMoonlight)
            {
                Instance?.TriggerUnlock("fish_moonlight");
            }
        }

        public static void RecordFurnitureScore(int score)
        {
            if (score >= 60)
            {
                EnsureInstance();
                Instance?.TriggerUnlock("home_cozy");
            }
        }

        public static void RecordPetFriendship(int level)
        {
            if (level >= 2)
            {
                EnsureInstance();
                Instance?.TriggerUnlock("pet_friend");
            }
        }

        public static void RecordBicycleRide()
        {
            EnsureInstance();
            Instance?.TriggerUnlock("bicycle_ride");
        }

        public static void RecordCropHarvested()
        {
            EnsureInstance();
            Instance?.TriggerUnlock("crop_harvest");
        }

        public static void RecordCafeVisit()
        {
            EnsureInstance();
            Instance?.TriggerUnlock("cafe_visit");
        }

        public static void RecordOnsenBath()
        {
            EnsureInstance();
            Instance?.TriggerUnlock("onsen_bath");
        }

        public static void RecordGameWon()
        {
            EnsureInstance();
            Instance?.TriggerUnlock("board_game");
        }

        public static void RecordBoardQuestCompleted()
        {
            EnsureInstance();
            Instance?.TriggerUnlock("town_board");
        }

        private void TriggerUnlock(string id)
        {
            var m = milestones.Find(x => x.id == id);
            if (m != null && !m.isUnlocked)
            {
                m.Unlock();
                CozyAudioManager.PlaySfx(CozySfxType.MysticChime);
                CozyLifeHUD.PushToast($"🏆 MỞ KHÓA THÀNH TỰU: [{m.title}]! Bấm [J] để nhận thưởng!");
            }
        }

        private void OnGUI()
        {
            if (!isDiaryOpen) return;

            GUI.depth = -100;

            float w = 580f;
            float h = 480f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            // Semi-transparent parchment leather background
            GUI.color = new Color(0.12f, 0.11f, 0.14f, 0.98f);
            GUI.DrawTexture(winRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawBorder(winRect, new Color(0.92f, 0.72f, 0.35f), 2.5f);

            GUILayout.BeginArea(new Rect(winRect.x + 16, winRect.y + 14, w - 32, h - 28));

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("📖 SỔ TAY THÀNH TỰU THỊ TRẤN — COZY DIARY", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✖ Đóng [J/Esc]", GUILayout.Width(100), GUILayout.Height(24)))
            {
                isDiaryOpen = false;
                CozyAudioManager.PlaySfx(CozySfxType.PageTurn);
            }
            GUILayout.EndHorizontal();

            int completedCount = milestones.FindAll(x => x.isUnlocked).Count;
            int total = milestones.Count;
            int pct = Mathf.RoundToInt((float)completedCount / total * 100f);

            GUILayout.Label($"✨ Tiến độ cuộc sống: {completedCount}/{total} Thành tựu ({pct}%)  •  Lưu giữ những khoảnh khắc đáng nhớ", subStyle);
            GUILayout.Space(8);

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(w - 32), GUILayout.Height(h - 110));

            var profInv = FindAnyObjectByType<ProfessionAndInventory>();

            for (int i = 0; i < milestones.Count; i++)
            {
                var m = milestones[i];

                GUILayout.BeginHorizontal(GUI.skin.box);

                GUILayout.BeginVertical();
                string statusColor = m.isUnlocked ? "#76FF03" : "#888888";
                string statusPrefix = m.isUnlocked ? "✓ ĐÃ HOÀN THÀNH" : "🔒 CHƯA ĐẠT";
                GUILayout.Label($"<b>{m.title}</b>  <color={statusColor}>[{statusPrefix}]</color>", itemTitleStyle);
                GUILayout.Label(m.description, subStyle);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                if (m.isClaimed)
                {
                    GUI.enabled = false;
                    GUILayout.Button("✓ Đã nhận", GUILayout.Width(100), GUILayout.Height(36));
                    GUI.enabled = true;
                }
                else if (m.isUnlocked)
                {
                    GUI.backgroundColor = new Color(0.35f, 0.85f, 0.40f);
                    if (GUILayout.Button($"Nhận +{m.rewardCoins} Xu", GUILayout.Width(100), GUILayout.Height(36)))
                    {
                        m.Claim();
                        profInv?.AddCoins(m.rewardCoins);
                        CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
                        CozyLifeHUD.PushToast($"💰 Nhận thưởng +{m.rewardCoins} Xu từ thành tựu [{m.title}]!");
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button($"+{m.rewardCoins} Xu", GUILayout.Width(100), GUILayout.Height(36));
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("💡 Phím tắt: Bấm [J] mọi lúc để mở hoặc đóng Sổ Tay Thành Tựu.", subStyle);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                isDiaryOpen = false;
            }

            GUILayout.EndArea();
        }

        private void DrawBorder(Rect r, Color c, float thickness)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static GUIStyle titleStyle => new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.88f, 0.45f) }
        };

        private static GUIStyle itemTitleStyle => new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = true,
            normal = { textColor = Color.white }
        };

        private static GUIStyle subStyle => new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
        };
    }
}
