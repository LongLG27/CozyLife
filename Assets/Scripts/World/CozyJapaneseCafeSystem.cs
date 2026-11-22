using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    [Serializable]
    public class CafeMenuItem
    {
        public string id;
        public string name;
        public string description;
        public int price;
        public float hunger;
        public float thirst;
        public float health;
        public float stamina;
        public float speedBuffSeconds;
        public ActionVisualType visualType;

        public CafeMenuItem(string id, string name, string description, int price, float hunger, float thirst, float health, float stamina, float speedBuff, ActionVisualType visual)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.price = price;
            this.hunger = hunger;
            this.thirst = thirst;
            this.health = health;
            this.stamina = stamina;
            this.speedBuffSeconds = speedBuff;
            this.visualType = visual;
        }
    }

    /// <summary>
    /// Bàn Trà Ngồi Bệt Kiểu Nhật (Chabudai) đặt trên sàn chiếu Tatami:
    /// Có nệm ngồi bệt Zabuton ở 2 bên trái & phải.
    /// Khi người chơi lại gần bấm [E], nhân vật sẽ ngồi bệt xuống nệm và mở thực đơn gọi món hoặc thưởng thức đồ uống.
    /// </summary>
    public class JapaneseFloorTableInteractable : WorldInteractableBase
    {
        private int tableNumber = 1;
        private bool hasDrinkServed = false;
        private string currentServedDrink = "";
        private SpriteRenderer sr;

        public int TableNumber => tableNumber;

        // Vị trí 2 nệm ngồi bệt ở 2 bên bàn
        public Vector3 SeatLeftPosition => transform.position + new Vector3(-0.30f, 0.02f, 0f);
        public Vector3 SeatRightPosition => transform.position + new Vector3(0.30f, 0.02f, 0f);

        public void ConfigureTable(int number)
        {
            tableNumber = number;
            interactTitle = $"Bàn Bệt #{number}";
            sr = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public override string GetWorldOverheadLabel()
        {
            if (hasDrinkServed)
                return $"🍵 Bàn #{tableNumber} ({currentServedDrink})";

            return $"🍵 Bàn Bệt #{tableNumber} [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            if (hasDrinkServed)
                return $"[E] Ngồi bệt & thưởng thức {currentServedDrink}";

            return $"[E] Ngồi bệt Bàn #{tableNumber} & Gọi Cà Phê / Trà Nhật";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            if (player == null) return;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null) return;

            // Xác định nệm gần nhất để ngồi
            Vector3 targetSeat = (Vector3.Distance(pc.transform.position, SeatLeftPosition) < Vector3.Distance(pc.transform.position, SeatRightPosition))
                ? SeatLeftPosition : SeatRightPosition;

            pc.SitAtFloorTable(this, targetSeat);

            // Mở thực đơn Quán Cafe
            CozyJapaneseCafeManager.EnsureInstance().OpenMenuModal(this);
        }

        public void ServeDrinkOnTable(string drinkName)
        {
            hasDrinkServed = true;
            currentServedDrink = drinkName;
        }

        public void ClearDrink()
        {
            hasDrinkServed = false;
            currentServedDrink = "";
        }
    }

    /// <summary>
    /// Quầy Pha Chế Barista trong Quán Cafe:
    /// Có máy Espresso retro, cối xay hạt và tủ bánh Mochi.
    /// Lại gần bấm [E] để gọi món trực tiếp từ Barista Yuki.
    /// </summary>
    public class CafeBaristaCounterInteractable : WorldInteractableBase
    {
        private void Awake()
        {
            interactTitle = "Quầy Pha Chế Barista";
        }

        public override string GetWorldOverheadLabel()
        {
            return "☕ Quầy Barista [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "[E] Gặp Barista Yuki & Gọi Cà Phê / Bánh Mochi";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            CozyJapaneseCafeManager.EnsureInstance().OpenMenuModal(null);
        }
    }

    /// <summary>
    /// Quản lý Hệ Thống Quán Cafe & Trà Đạo Nhật Bản (Cozy Japanese Cafe System):
    /// - Thực đơn 4 món đặc trưng Nhật Bản (Matcha Latte, Cà Phê Phin, Trà Đào Cam Sả, Bánh Mochi).
    /// - Giao diện OnGUI sang trọng, ấm cúng phong cách Nhật Bản.
    /// - Phục vụ món tại bàn bệt hoặc mang về.
    /// </summary>
    public class CozyJapaneseCafeManager : MonoBehaviour
    {
        private static CozyJapaneseCafeManager instance;
        public static CozyJapaneseCafeManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<CozyJapaneseCafeManager>();
                    if (instance == null)
                    {
                        GameObject host = new GameObject("CozyJapaneseCafe_Host");
                        instance = host.AddComponent<CozyJapaneseCafeManager>();
                        DontDestroyOnLoad(host);
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        public static CozyJapaneseCafeManager EnsureInstance() => Instance;

        public static bool IsMenuOpen => instance != null && instance.isModalOpen;

        private bool isModalOpen = false;
        private JapaneseFloorTableInteractable currentTable;
        private readonly List<CafeMenuItem> menuList = new List<CafeMenuItem>();

        private Texture2D whiteTex;
        private Vector2 scrollPos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitMenu();
        }

        private void InitMenu()
        {
            menuList.Clear();
            menuList.Add(new CafeMenuItem(
                "cafe_matcha_latte",
                "🍵 Matcha Latte Uji (Nóng / Đá)",
                "Trà xanh Uji thượng hạng hòa quyện sữa tươi béo ngậy. Hồi phục 40 Khát, 20 Đói và giảm mệt mỏi.",
                20, 20f, 40f, 10f, 30f, 0f,
                ActionVisualType.DrinkingWater
            ));

            menuList.Add(new CafeMenuItem(
                "cafe_iced_coffee",
                "☕ Cà Phê Phin Sữa Đá Đậm Đà",
                "Hạt Robusta rang mộc pha phin đậm vị truyền thống. Hồi phục 35 Khát, 15 Đói và TĂNG TỐC ĐỘ CHẠY (+30% trong 60s)!",
                18, 15f, 35f, 0f, 45f, 60f,
                ActionVisualType.DrinkingWater
            ));

            menuList.Add(new CafeMenuItem(
                "cafe_peach_tea",
                "🍑 Trà Đào Cam Sả Thơm Mát",
                "Trà đen hảo hạng với đào giòn, cam tươi và sả thanh mát. Hồi phục 50 Khát, giải nhiệt tức thì!",
                18, 10f, 50f, 5f, 25f, 0f,
                ActionVisualType.DrinkingWater
            ));

            menuList.Add(new CafeMenuItem(
                "cafe_mochi_cake",
                "🍡 Bánh Mochi Đậu Đỏ Nhật Bản",
                "Bánh gạo nếp dẻo thơm nhân đậu đỏ Azuki ngọt bùi. Hồi phục 45 Đói, 20 Máu và nhận hiệu ứng No Bụng (Well-Fed)!",
                22, 45f, 10f, 20f, 20f, 0f,
                ActionVisualType.EatingBread
            ));
        }

        public void OpenMenuModal(JapaneseFloorTableInteractable table)
        {
            if (menuList == null || menuList.Count == 0)
            {
                InitMenu();
            }
            currentTable = table;
            isModalOpen = true;
            scrollPos = Vector2.zero;
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
        }

        public void CloseMenuModal()
        {
            isModalOpen = false;
            currentTable = null;
        }

        private void Update()
        {
            if (!isModalOpen) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMenuModal();
            }
        }

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
            if (!isModalOpen) return;

            GUI.depth = -100;

            EnsureWhiteTex();

            float sw = Screen.width;
            float sh = Screen.height;

            // Nền tối mờ toàn màn hình
            DrawSolidRect(new Rect(0, 0, sw, sh), new Color(0.04f, 0.06f, 0.08f, 0.75f));

            float w = 620f;
            float h = 480f;
            Rect modalRect = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);

            // Khung cửa sổ gỗ phong cách Nhật
            DrawSolidRect(modalRect, new Color(0.12f, 0.15f, 0.20f, 0.98f));
            DrawBorderRect(modalRect, new Color(0.85f, 0.68f, 0.38f), 2.5f);

            // Tiêu đề
            Rect titleBar = new Rect(modalRect.x, modalRect.y, modalRect.width, 50f);
            DrawSolidRect(titleBar, new Color(0.18f, 0.22f, 0.28f, 0.95f));
            DrawBorderRect(titleBar, new Color(0.70f, 0.55f, 0.30f), 1f);

            GUIStyle titleSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.90f, 0.45f) }
            };
            string header = (currentTable != null)
                ? $"☕ THỰC ĐƠN QUÁN CAFE NHẬT BẢN — BÀN BỆT #{currentTable.TableNumber}"
                : "☕ THỰC ĐƠN QUÁN CAFE NHẬT BẢN — QUẦY BARISTA YUKI";
            GUI.Label(new Rect(modalRect.x + 18f, modalRect.y + 12f, modalRect.width - 60f, 28f), header, titleSt);

            // Nút đóng [X]
            GUIStyle closeBtnSt = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(modalRect.xMax - 40f, modalRect.y + 10f, 30f, 30f), "✕", closeBtnSt))
            {
                CloseMenuModal();
                return;
            }

            // Danh sách món ăn đồ uống
            GUILayout.BeginArea(new Rect(modalRect.x + 18f, modalRect.y + 60f, modalRect.width - 36f, modalRect.height - 120f));
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

            GUIStyle itemBoxSt = new GUIStyle(GUI.skin.box);
            GUIStyle itemNameSt = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.88f, 0.45f) } };
            GUIStyle itemDescSt = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.85f, 0.88f, 0.92f) } };
            GUIStyle priceSt = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 0.85f, 0.25f) } };
            GUIStyle orderBtnSt = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold };

            var player = FindAnyObjectByType<PlayerController>();
            var profInv = player != null ? player.GetComponent<ProfessionAndInventory>() : null;
            int playerCoins = profInv != null ? profInv.Coins : 0;

            for (int i = 0; i < menuList.Count; i++)
            {
                var item = menuList[i];
                GUILayout.BeginVertical(itemBoxSt);

                GUILayout.BeginHorizontal();
                GUILayout.Label(item.name, itemNameSt);
                GUILayout.Label($"💰 {item.price} Xu", priceSt, GUILayout.Width(100f));
                GUILayout.EndHorizontal();

                GUILayout.Label(item.description, itemDescSt);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                bool canAfford = playerCoins >= item.price;
                GUI.enabled = canAfford;

                string orderLabel = (currentTable != null) ? "🍵 Dùng Tại Bàn" : "☕ Thưởng Thức Ngay";
                if (GUILayout.Button(orderLabel, orderBtnSt, GUILayout.Width(130f), GUILayout.Height(28f)))
                {
                    OrderDrink(item, false);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Thanh chân trang (Số xu hiện có & Nút đóng)
            Rect footerRect = new Rect(modalRect.x + 18f, modalRect.yMax - 50f, modalRect.width - 36f, 40f);
            GUIStyle coinSt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.88f, 0.35f) }
            };
            GUI.Label(new Rect(footerRect.x, footerRect.y + 6f, 250f, 28f), $"💰 Số Dư Của Bạn: {playerCoins} Xu", coinSt);

            if (GUI.Button(new Rect(footerRect.xMax - 140f, footerRect.y + 4f, 140f, 32f), "Đóng Menu", closeBtnSt))
            {
                CloseMenuModal();
            }
        }

        private void OrderDrink(CafeMenuItem item, bool isTakeaway)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            var profInv = player.GetComponent<ProfessionAndInventory>();
            var stats = player.GetComponent<CharacterStats>();

            if (profInv == null || stats == null) return;

            if (!profInv.SpendCoins(item.price))
            {
                CozyLifeHUD.PushToast("⚠️ Bạn không đủ Xu để gọi món này!");
                CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert);
                return;
            }

            CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
            CloseMenuModal();

            // Nếu đang ngồi tại bàn bệt -> Phục vụ ra bàn
            if (currentTable != null)
            {
                currentTable.ServeDrinkOnTable(item.name);
            }

            // NPC Barista Yuki gửi lời chào
            var friends = FindObjectsByType<TownFriendAgent>();
            foreach (var f in friends)
            {
                if (f != null && f.AssignedBuilding == BuildingType.Cafe)
                {
                    f.Say($"☕ Yuki: Arigato! Mời bạn dùng {item.name}! 🌸", 4.5f);
                    break;
                }
            }

            // Bắt đầu hành động thưởng thức
            player.StartTimedAction($"🍵 Đang thưởng thức {item.name}", 20f, () =>
            {
                stats.ModifyVitals(item.health, item.hunger, item.thirst, item.stamina);

                if (item.speedBuffSeconds > 0f)
                {
                    // Tăng tốc độ chạy tạm thời
                    player.ApplySpeedBuff(1.30f, item.speedBuffSeconds);
                    CozyLifeHUD.PushToast($"⚡ Hiệu ứng Cà Phê: Tăng +30% tốc độ di chuyển trong {Mathf.RoundToInt(item.speedBuffSeconds)}s!");
                }

                CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
                CozyLifeHUD.PushToast($"✅ Đã dùng xong {item.name}! (+{Mathf.RoundToInt(item.hunger)} No, +{Mathf.RoundToInt(item.thirst)} Nước)");
                CozyDiarySystem.RecordCafeVisit();
            }, item.visualType);
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
