using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    /// <summary>
    /// Xe Đẩy Ẩm Thực Đường Phố (Town Street Snack Cart):
    /// - Quầy thức ăn vặt thơm lừng đặt ngay trục phố đi bộ quảng trường trung tâm.
    /// - Phục vụ các món ăn đường phố Việt Nam thơm ngon nóng hổi:
    ///   1. Bánh Mì Thịt Nướng Giòn Rụm (+65 No, +25 Thể lực)
    ///   2. Trà Đào Cam Sả Mát Lạnh (+60 Nước, +15 Thể lực, Giải nhiệt)
    ///   3. Kem Dừa Non Bến Tre (+30 Thể lực, +25 Nước, Giảm stress)
    /// </summary>
    public class TownStreetFoodCartInteractable : WorldInteractableBase
    {
        public static bool IsAnyModalOpen { get; private set; }

        private bool isModalOpen = false;
        private ProfessionAndInventory activePlayer;
        private CharacterStats activeStats;

        private void Awake()
        {
            stationType = StationType.None;
            interactTitle = "Xe Đẩy Ẩm Thực Đường Phố";
        }

        private void Update()
        {
            if (isModalOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    isModalOpen = false;
                    IsAnyModalOpen = false;
                    CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                }
            }
        }

        public override string GetWorldOverheadLabel()
        {
            return "🥖 Quầy Ẩm Thực Phố [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "[E] Ghé mua đồ ăn vặt đường phố (Bánh mì thịt nướng, Trà đào cam sả, Kem dừa)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            activePlayer = player;
            activeStats = stats;
            isModalOpen = true;
            IsAnyModalOpen = true;
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
        }

        private void OnGUI()
        {
            if (!isModalOpen) return;

            GUI.depth = -100;

            float w = 480f;
            float h = 330f;
            Rect winRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(0.12f, 0.10f, 0.08f, 0.98f);
            GUI.DrawTexture(winRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Border
            DrawBorder(winRect, new Color(0.95f, 0.65f, 0.25f), 2f);

            GUILayout.BeginArea(new Rect(winRect.x + 16, winRect.y + 14, w - 32, h - 28));

            GUILayout.BeginHorizontal();
            GUILayout.Label("🥖 XE ĐẨY ẨM THỰC ĐƯỜNG PHỐ — CÔ BA", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✖ Đóng", GUILayout.Width(70), GUILayout.Height(24)))
            {
                isModalOpen = false;
                IsAnyModalOpen = false;
                CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"💰 Tiền trong túi: {activePlayer?.Coins ?? 0} Xu  •  Hương vị quê nhà thơm nức mũi~", subStyle);
            GUILayout.Space(8);

            // Item 1: Bánh Mì Thịt Nướng
            DrawFoodItemRow("🥖 Bánh Mì Thịt Nướng Bơ Giòn", 18, "+65 No  •  +25 Thể lực", () =>
            {
                BuyAndEat(18, "Bánh Mì Thịt Nướng", 65f, 0f, 25f);
            });

            // Item 2: Trà Đào Cam Sả
            DrawFoodItemRow("🍹 Trà Đào Cam Sả Đá Tuyết", 12, "+60 Nước  •  +15 Thể lực (Giải nhiệt)", () =>
            {
                BuyAndEat(12, "Trà Đào Cam Sả", 0f, 60f, 15f);
            });

            // Item 3: Kem Dừa Non
            DrawFoodItemRow("🥥 Kem Dừa Non Bến Tre Thơm Béo", 15, "+30 Thể lực  •  +25 Nước (Ngọt mát)", () =>
            {
                BuyAndEat(15, "Kem Dừa Non", 15f, 25f, 30f);
            });

            GUILayout.Space(10);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                isModalOpen = false;
            }

            GUILayout.EndArea();
        }

        private void DrawFoodItemRow(string name, int price, string effect, System.Action onBuy)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.BeginVertical();
            GUILayout.Label($"<b>{name}</b>  <color=#FFD700>({price} Xu)</color>", itemTitleStyle);
            GUILayout.Label($"Hiệu quả: {effect}", subStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            bool canAfford = activePlayer != null && activePlayer.Coins >= price;
            if (GUILayout.Button(canAfford ? "Thưởng thức 😋" : "Thiếu Xu", GUILayout.Width(110), GUILayout.Height(34)))
            {
                if (canAfford)
                {
                    onBuy?.Invoke();
                }
                else
                {
                    CozyLifeHUD.PushToast("⚠️ Bạn không đủ Xu để mua món này!");
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void BuyAndEat(int price, string foodName, float hunger, float thirst, float stamina)
        {
            if (activePlayer == null || !activePlayer.SpendCoins(price))
            {
                CozyLifeHUD.PushToast("⚠️ Không đủ tiền!");
                return;
            }

            activeStats?.ModifyVitals(0f, hunger, thirst, stamina);
            CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
            CozyAudioManager.PlaySfx(CozySfxType.CashRegister);
            CozySaveManager.SaveGame(false);
            CozyLifeHUD.PushToast($"😋 Bạn vừa thưởng thức {foodName} nóng hổi ngon tuyệt (-{price} Xu)!");
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
            normal = { textColor = new Color(0.85f, 0.85f, 0.90f) }
        };
    }
}
