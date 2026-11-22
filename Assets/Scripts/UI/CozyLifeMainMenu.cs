using System;
using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.Network;

namespace CozyLife.UI
{
    /// <summary>
    /// Menu Chính & Bảng Tùy Chỉnh Ngoại Trang Nhân Vật trước khi vào game:
    /// - Đặt tên nhân vật (có nút xúc xắc ngẫu nhiên tên hay)
    /// - Chọn Giới tính Nam / Nữ
    /// - Tùy chỉnh Kiểu tóc & Màu tóc (có ô màu trực quan)
    /// - Tùy chỉnh Biểu cảm khuôn mặt
    /// - Tùy chỉnh Áo & Màu áo
    /// - Tùy chỉnh Quần / Váy & Màu quần
    /// - Tùy chỉnh Phụ kiện (Kính, Mũ, Nơ, Khăn quàng)
    /// - Tùy chỉnh Mẫu Giày & Màu Da
    /// - Khung Xem Trước (Live Preview) thời gian thực không bị che khuất
    /// - Chế độ Chơi Đơn (Offline) & Tạo Phòng / Vào Phòng Online theo Mã ID
    /// </summary>
    public class CozyLifeMainMenu : MonoBehaviour
    {
        public static CozyLifeMainMenu Instance { get; private set; }
        public static bool IsActive { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            IsActive = true;
        }

        private CharacterAppearanceData currentData;
        private PlayerController boundPlayer;
        private ProfessionAndInventory boundProfInv;

        private int activeTab = 0; // 0: Thông tin, 1: Tóc & Mặt, 2: Áo & Quần, 3: Phụ kiện & Giày
        private Texture2D whiteTex;
        private GUIStyle titleHeaderStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle sectionHeaderStyle;
        private GUIStyle labelStyle;
        private GUIStyle inputStyle;
        private GUIStyle buttonNormalStyle;
        private GUIStyle buttonPrimaryStyle;
        private GUIStyle buttonOnlineStyle;
        private GUIStyle buttonJoinStyle;
        private GUIStyle tabActiveStyle;
        private GUIStyle tabInactiveStyle;
        private GUIStyle centerBoxStyle;
        private bool stylesInitialized = false;

        private bool isReCustomizingInGame = false;
        private bool previewWalkAnimation = true;
        private static readonly int[] PreviewWalkFrames = { 1, 2, 3, 0 };

        // Cửa sổ Tạo Phòng & Vào Phòng Online
        private bool showCreateRoomModal = false;
        private bool showJoinRoomModal = false;
        private string inputCreateRoomId = "";
        private string inputCreateRoomName = "";
        private int inputCreateMaxPlayers = 8;
        private string inputJoinRoomTarget = "";
        private string joinStatusMessage = "";
        private bool isConnectingOnline = false;
        private Vector2 lanRoomsScrollPos = Vector2.zero;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            IsActive = true;
            currentData = CharacterAppearanceData.LoadFromPrefs();
        }

        public void BindPlayer(PlayerController player, ProfessionAndInventory profInv)
        {
            boundPlayer = player;
            boundProfInv = profInv;

            if (boundProfInv != null)
            {
                boundProfInv.SetAppearance(currentData);
            }
        }

        public void OpenMenuInGame()
        {
            isReCustomizingInGame = true;
            IsActive = true;
            if (boundProfInv != null && boundProfInv.Appearance != null)
            {
                currentData = boundProfInv.Appearance;
            }
        }

        public void CloseMenu()
        {
            IsActive = false;
            isReCustomizingInGame = false;
        }

        private void EnsureStyles()
        {
            if (stylesInitialized && whiteTex != null)
                return;

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();

            titleHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.55f) }
            };

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
            };

            sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.80f, 0.40f) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.90f, 0.92f, 0.95f) }
            };

            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            buttonNormalStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            buttonPrimaryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.95f, 0.55f) }
            };

            buttonOnlineStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.40f, 0.95f, 1f) }
            };

            buttonJoinStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.90f, 0.45f) }
            };

            tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.95f, 0.55f) }
            };

            tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.75f, 0.78f, 0.82f) }
            };

            centerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.95f, 0.88f) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!IsActive)
                return;

            EnsureStyles();

            // Lớp phủ nền mờ phong cảnh thị trấn
            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.06f, 0.08f, 0.88f));

            float totalW = 820f;
            float totalH = 525f;
            Rect mainWin = new Rect((Screen.width - totalW) * 0.5f, (Screen.height - totalH) * 0.5f, totalW, totalH);

            DrawSolidRect(mainWin, new Color(0.10f, 0.12f, 0.16f, 0.98f));
            DrawBorderRect(mainWin, new Color(0.92f, 0.75f, 0.35f), 2f);

            // 1. Tiêu đề chính
            GUI.Label(new Rect(mainWin.x, mainWin.y + 10, totalW, 22), "🌿 COZY LIFE — THIẾT KẾ NHÂN VẬT & THẾ GIỚI", titleHeaderStyle);
            GUI.Label(new Rect(mainWin.x, mainWin.y + 32, totalW, 16), isReCustomizingInGame
                ? "Thay đổi trang phục & diện mạo của bạn bất cứ lúc nào!"
                : "Tạo nhân vật của riêng bạn, mở phòng hoặc nhập mã ID để bước vào thế giới cùng bạn bè", subHeaderStyle);

            // 2. Cột Trái: Khung Xem Trước (Preview Card)
            float leftX = mainWin.x + 16f;
            float leftY = mainWin.y + 56f;
            float leftW = 230f;
            float leftH = 405f;
            DrawLeftPreviewCard(leftX, leftY, leftW, leftH);

            // 3. Cột Phải: Bảng Tùy Chỉnh (Customization Panel)
            float rightX = mainWin.x + 258f;
            float rightY = mainWin.y + 56f;
            float rightW = totalW - 274f;
            float rightH = 405f;
            DrawRightCustomizationPanel(rightX, rightY, rightW, rightH);

            // 4. Thanh nút dưới cùng (Bottom Buttons)
            float bottomY = mainWin.y + totalH - 46f;
            DrawBottomButtons(mainWin.x + 16f, bottomY, totalW - 32f);

            // 5. Cửa sổ Tạo Phòng & Vào Phòng Online
            if (showCreateRoomModal)
            {
                DrawCreateRoomModal();
            }
            else if (showJoinRoomModal)
            {
                DrawJoinRoomModal();
            }
        }

        private void DrawLeftPreviewCard(float x, float y, float w, float h)
        {
            Rect cardRect = new Rect(x, y, w, h);
            DrawSolidRect(cardRect, new Color(0.14f, 0.16f, 0.22f, 0.96f));
            DrawBorderRect(cardRect, new Color(0.35f, 0.45f, 0.60f), 1f);

            // Tiêu đề khung preview
            GUI.Label(new Rect(x, y + 8, w, 16), "XEM TRƯỚC DIỆN MẠO", subHeaderStyle);

            // Khung chứa Sprite nhân vật phóng to rõ nét (144x189 px)
            float pW = 144f;
            float pH = 189f;
            float px = x + (w - pW) * 0.5f;
            float py = y + 28f;
            Rect previewBox = new Rect(px, py, pW, pH);

            DrawSolidRect(previewBox, new Color(0.08f, 0.09f, 0.12f, 0.98f));
            DrawBorderRect(previewBox, new Color(0.85f, 0.70f, 0.35f, 0.7f), 1.5f);

            int previewFrame = 0;
            if (previewWalkAnimation)
            {
                int stepIdx = Mathf.FloorToInt(Time.realtimeSinceStartup * 6.5f) % PreviewWalkFrames.Length;
                previewFrame = PreviewWalkFrames[stepIdx];
            }

            Texture2D charTex = CharacterAppearanceCompositor.GetOrCreateCustomTexture(currentData, previewFrame);
            if (charTex != null)
            {
                GUI.DrawTexture(new Rect(previewBox.x + 8, previewBox.y + 6, previewBox.width - 16, previewBox.height - 12), charTex, ScaleMode.ScaleToFit);
            }

            // Tên nhân vật (không bị che khuất)
            float nameY = py + pH + 10f;
            GUIStyle nameTagStyle = new GUIStyle(titleHeaderStyle) { fontSize = 14 };
            GUI.Label(new Rect(x + 6, nameY, w - 12, 20), currentData.characterName, nameTagStyle);

            // Giới tính & Nghề nghiệp (nằm riêng biệt bên dưới tên)
            float badgeY = nameY + 22f;
            string genderBadge = (currentData.gender == GenderType.Male) ? "👦 Nam" : "👧 Nữ";
            string profBadge = CozyLifeDatabase.GetProfession(currentData.starterProfession).titleBadge;
            GUI.Label(new Rect(x + 6, badgeY, w - 12, 18), $"{genderBadge}  •  {profBadge}", subHeaderStyle);

            // Nút Random ngẫu nhiên ngoại trang
            float randBtnY = badgeY + 24f;
            Rect randBtnRect = new Rect(x + 14, randBtnY, w - 28, 26);
            if (GUI.Button(randBtnRect, "🎲 Ngẫu Nhiên Ngoại Trang", buttonNormalStyle))
            {
                currentData.RandomizeAll();
            }

            // Nút bật/tắt xem thử chuyển động bước đi (vung tay & bước chân)
            float walkBtnY = randBtnY + 32f;
            Rect walkBtnRect = new Rect(x + 14, walkBtnY, w - 28, 26);
            string walkLabel = previewWalkAnimation ? "🏃 Dáng Đi: ĐANG CHẠY" : "🧍 Dáng Đi: ĐỨNG YÊN";
            if (GUI.Button(walkBtnRect, walkLabel, buttonNormalStyle))
            {
                previewWalkAnimation = !previewWalkAnimation;
            }
        }

        private void DrawRightCustomizationPanel(float x, float y, float w, float h)
        {
            // 1. Thanh 4 Tab chuyển đổi
            string[] tabs = new string[] { "👤 Thông Tin", "💇 Tóc & Mặt", "👕 Trang Phục", "👓 Phụ Kiện" };
            float tabGap = 6f;
            float tabW = (w - tabGap * 3f) / 4f;
            float tabH = 30f;

            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tabRect = new Rect(x + i * (tabW + tabGap), y, tabW, tabH);
                bool isActive = (activeTab == i);
                GUIStyle st = isActive ? tabActiveStyle : tabInactiveStyle;

                if (isActive)
                {
                    DrawSolidRect(tabRect, new Color(0.24f, 0.32f, 0.44f, 0.95f));
                    DrawBorderRect(tabRect, new Color(0.95f, 0.82f, 0.40f), 1.5f);
                }

                if (GUI.Button(tabRect, tabs[i], st))
                {
                    activeTab = i;
                }
            }

            // 2. Khung nội dung Tab
            float boxY = y + tabH + 6f;
            float boxH = h - tabH - 6f;
            Rect contentBox = new Rect(x, boxY, w, boxH);

            DrawSolidRect(contentBox, new Color(0.12f, 0.14f, 0.19f, 0.96f));
            DrawBorderRect(contentBox, new Color(0.35f, 0.42f, 0.55f), 1.5f);

            // Tọa độ nội dung bên trong khung
            float padX = x + 16f;
            float innerW = w - 32f;
            float curY = boxY + 12f;

            switch (activeTab)
            {
                case 0:
                    DrawTabGeneralInfo(padX, innerW, ref curY);
                    break;
                case 1:
                    DrawTabHairAndFace(padX, innerW, ref curY);
                    break;
                case 2:
                    DrawTabOutfits(padX, innerW, ref curY);
                    break;
                case 3:
                    DrawTabAccessoriesAndShoes(padX, innerW, ref curY);
                    break;
            }
        }

        private void DrawTabGeneralInfo(float x, float w, ref float curY)
        {
            GUI.Label(new Rect(x, curY, w, 18), "THÔNG TIN CƠ BẢN", sectionHeaderStyle);
            curY += 24f;

            // 1. Tên nhân vật
            GUI.Label(new Rect(x, curY, w, 16), "Họ và Tên Nhân Vật:", labelStyle);
            curY += 18f;

            float tfW = w - 100f;
            currentData.characterName = GUI.TextField(new Rect(x, curY, tfW, 26), currentData.characterName, 18, inputStyle);
            if (GUI.Button(new Rect(x + tfW + 8f, curY, 92f, 26), "🎲 Tên Hay", buttonNormalStyle))
            {
                currentData.GenerateRandomName();
            }
            curY += 34f;

            // 2. Giới tính Nam / Nữ
            GUI.Label(new Rect(x, curY, w, 16), "Giới Tính:", labelStyle);
            curY += 18f;

            float halfW = (w - 8f) * 0.5f;
            bool isMale = currentData.gender == GenderType.Male;

            Rect maleBtn = new Rect(x, curY, halfW, 28);
            if (isMale)
            {
                DrawSolidRect(maleBtn, new Color(0.20f, 0.35f, 0.55f, 0.95f));
                DrawBorderRect(maleBtn, new Color(0.55f, 0.85f, 1f), 1.5f);
            }
            if (GUI.Button(maleBtn, "👦 Nam", isMale ? tabActiveStyle : buttonNormalStyle))
            {
                currentData.gender = GenderType.Male;
            }

            Rect femaleBtn = new Rect(x + halfW + 8f, curY, halfW, 28);
            if (!isMale)
            {
                DrawSolidRect(femaleBtn, new Color(0.55f, 0.25f, 0.40f, 0.95f));
                DrawBorderRect(femaleBtn, new Color(1f, 0.65f, 0.85f), 1.5f);
            }
            if (GUI.Button(femaleBtn, "👧 Nữ", !isMale ? tabActiveStyle : buttonNormalStyle))
            {
                currentData.gender = GenderType.Female;
            }
            curY += 36f;

            // 3. Màu Da
            DrawRowSelector(x, w, ref curY, "Màu Da:", CharacterAppearanceData.SkinToneNames[currentData.skinToneIndex],
                () => currentData.NextSkinTone(-1),
                () => currentData.NextSkinTone(1));

            // 4. Nghề Nghiệp Khởi Đầu
            string profName = CozyLifeDatabase.GetProfession(currentData.starterProfession).titleBadge;
            DrawRowSelector(x, w, ref curY, "Nghề Nghiệp Khởi Đầu:", profName,
                () => currentData.NextProfession(-1),
                () => currentData.NextProfession(1));

            string profDesc = CozyLifeDatabase.GetProfession(currentData.starterProfession).description;
            GUI.Label(new Rect(x, curY, w, 18), $"ℹ️ {profDesc}", subHeaderStyle);
        }

        private void DrawTabHairAndFace(float x, float w, ref float curY)
        {
            GUI.Label(new Rect(x, curY, w, 18), "KIỂU TÓC & BIỂU CẢM", sectionHeaderStyle);
            curY += 24f;

            // 1. Kiểu tóc
            DrawRowSelector(x, w, ref curY, "Kiểu Tóc:", currentData.GetHairStyleName(),
                () => currentData.NextHairStyle(-1),
                () => currentData.NextHairStyle(1));

            // 2. Màu tóc
            DrawRowSelector(x, w, ref curY, "Màu Tóc:", CharacterAppearanceData.HairColorNames[currentData.hairColorIndex],
                () => currentData.NextHairColor(-1),
                () => currentData.NextHairColor(1));

            // Ô chọn màu tóc nhanh (6 màu)
            float swatchW = (w - 5f * 6f) / 6f;
            for (int i = 0; i < CharacterAppearanceData.HairColors.Length; i++)
            {
                Rect swRect = new Rect(x + i * (swatchW + 6f), curY, swatchW, 18f);
                DrawSolidRect(swRect, CharacterAppearanceData.HairColors[i]);
                if (currentData.hairColorIndex == i)
                {
                    DrawBorderRect(swRect, Color.white, 2f);
                }
                else
                {
                    DrawBorderRect(swRect, new Color(0.2f, 0.2f, 0.2f, 0.8f), 1f);
                }
                if (GUI.Button(swRect, GUIContent.none, GUIStyle.none))
                {
                    currentData.hairColorIndex = i;
                }
            }
            curY += 26f;

            // 3. Biểu cảm
            DrawRowSelector(x, w, ref curY, "Biểu Cảm Khuôn Mặt:", CharacterAppearanceData.Expressions[currentData.expressionIndex],
                () => currentData.NextExpression(-1),
                () => currentData.NextExpression(1));
        }

        private void DrawTabOutfits(float x, float w, ref float curY)
        {
            GUI.Label(new Rect(x, curY, w, 18), "TRANG PHỤC HÀNG NGÀY", sectionHeaderStyle);
            curY += 24f;

            // 1. Mẫu Áo
            DrawRowSelector(x, w, ref curY, "Mẫu Áo (Thân Trên):", CharacterAppearanceData.ShirtStyles[currentData.shirtIndex],
                () => currentData.NextShirt(-1),
                () => currentData.NextShirt(1));

            // 2. Màu Áo
            DrawRowSelector(x, w, ref curY, "Màu Áo:", CharacterAppearanceData.ColorPaletteNames[currentData.shirtColorIndex],
                () => currentData.NextShirtColor(-1),
                () => currentData.NextShirtColor(1));

            // Ô chọn màu áo nhanh (6 màu)
            float swatchW = (w - 5f * 6f) / 6f;
            for (int i = 0; i < CharacterAppearanceData.OutfitColors.Length; i++)
            {
                Rect swRect = new Rect(x + i * (swatchW + 6f), curY, swatchW, 18f);
                DrawSolidRect(swRect, CharacterAppearanceData.OutfitColors[i]);
                if (currentData.shirtColorIndex == i)
                {
                    DrawBorderRect(swRect, Color.white, 2f);
                }
                else
                {
                    DrawBorderRect(swRect, new Color(0.2f, 0.2f, 0.2f, 0.8f), 1f);
                }
                if (GUI.Button(swRect, GUIContent.none, GUIStyle.none))
                {
                    currentData.shirtColorIndex = i;
                }
            }
            curY += 26f;

            // 3. Mẫu Quần / Váy
            DrawRowSelector(x, w, ref curY, "Mẫu Quần / Váy (Thân Dưới):", CharacterAppearanceData.PantsStyles[currentData.pantsIndex],
                () => currentData.NextPants(-1),
                () => currentData.NextPants(1));

            // 4. Màu Quần
            DrawRowSelector(x, w, ref curY, "Màu Quần / Váy:", CharacterAppearanceData.PantsColorNames[currentData.pantsColorIndex],
                () => currentData.NextPantsColor(-1),
                () => currentData.NextPantsColor(1));
        }

        private void DrawTabAccessoriesAndShoes(float x, float w, ref float curY)
        {
            GUI.Label(new Rect(x, curY, w, 18), "PHỤ KIỆN & GIÀY", sectionHeaderStyle);
            curY += 24f;

            // 1. Phụ kiện
            DrawRowSelector(x, w, ref curY, "Phụ Kiện (Mũ / Kính / Khăn / Nơ):", CharacterAppearanceData.Accessories[currentData.accessoryIndex],
                () => currentData.NextAccessory(-1),
                () => currentData.NextAccessory(1));

            curY += 8f;

            // 2. Mẫu Giày
            DrawRowSelector(x, w, ref curY, "Mẫu Giày Đi Lại:", CharacterAppearanceData.Shoes[currentData.shoesIndex],
                () => currentData.NextShoes(-1),
                () => currentData.NextShoes(1));
        }

        private void DrawRowSelector(float x, float w, ref float curY, string label, string currentValue, Action onPrev, Action onNext)
        {
            GUI.Label(new Rect(x, curY, w, 16), label, labelStyle);
            curY += 18f;

            Rect prevBtn = new Rect(x, curY, 34, 26);
            if (GUI.Button(prevBtn, "◀", buttonNormalStyle))
            {
                onPrev?.Invoke();
            }

            Rect valBox = new Rect(x + 38, curY, w - 76, 26);
            GUI.Box(valBox, currentValue, centerBoxStyle);

            Rect nextBtn = new Rect(x + w - 34, curY, 34, 26);
            if (GUI.Button(nextBtn, "▶", buttonNormalStyle))
            {
                onNext?.Invoke();
            }

            curY += 34f;
        }

        private void DrawBottomButtons(float x, float y, float w)
        {
            float leftOffset = x;
            string audioLabel = CozyAudioManager.IsMuted ? "🔇 Bật Nhạc [M]" : "🔊 Tắt Nhạc [M]";
            if (GUI.Button(new Rect(leftOffset, y, 120, 36), audioLabel, buttonNormalStyle))
            {
                CozyAudioManager.ToggleMute();
            }
            leftOffset += 126f;

            if (isReCustomizingInGame)
            {
                if (GUI.Button(new Rect(leftOffset, y, 140, 36), "✕ Đóng Không Lưu", buttonNormalStyle))
                {
                    CloseMenu();
                }

                float saveBtnW = 280f;
                float saveBtnX = x + w - saveBtnW;
                Rect saveBtnRect = new Rect(saveBtnX, y, saveBtnW, 36);
                DrawSolidRect(saveBtnRect, new Color(0.20f, 0.45f, 0.28f, 0.95f));
                DrawBorderRect(saveBtnRect, new Color(0.60f, 0.95f, 0.65f), 1.5f);
                if (GUI.Button(saveBtnRect, "✓ LƯU NGOẠI TRANG & TIẾP TỤC CHƠI", buttonPrimaryStyle))
                {
                    StartGameWithCurrentCharacter();
                }
                return;
            }

            if (CozySaveManager.HasSaveData())
            {
                if (GUI.Button(new Rect(leftOffset, y, 100, 36), "🗑️ Xóa Lưu", buttonNormalStyle))
                {
                    CozySaveManager.DeleteSaveData();
                }
                leftOffset += 106f;
            }

            // Nút Tạo Phòng Online
            Rect createRoomBtnRect = new Rect(leftOffset, y, 150, 36);
            DrawSolidRect(createRoomBtnRect, new Color(0.12f, 0.26f, 0.38f, 0.95f));
            DrawBorderRect(createRoomBtnRect, new Color(0.40f, 0.85f, 1f), 1.5f);
            if (GUI.Button(createRoomBtnRect, "🌐 Tạo Phòng Online", buttonOnlineStyle))
            {
                OpenCreateRoomModal();
            }
            leftOffset += 156f;

            // Nút Vào Phòng Theo Mã ID
            Rect joinRoomBtnRect = new Rect(leftOffset, y, 160, 36);
            DrawSolidRect(joinRoomBtnRect, new Color(0.36f, 0.24f, 0.10f, 0.95f));
            DrawBorderRect(joinRoomBtnRect, new Color(1f, 0.82f, 0.35f), 1.5f);
            if (GUI.Button(joinRoomBtnRect, "🔑 Vào Phòng Theo ID", buttonJoinStyle))
            {
                OpenJoinRoomModal();
            }

            // Nút Chơi Đơn (Offline)
            bool hasSave = CozySaveManager.HasSaveData();
            string soloLabel = hasSave ? "🎮 TIẾP TỤC (CHƠI ĐƠN)" : "🎮 CHƠI ĐƠN (OFFLINE)";
            float soloBtnW = 185f;
            float soloBtnX = x + w - soloBtnW;
            Rect soloBtnRect = new Rect(soloBtnX, y, soloBtnW, 36);

            DrawSolidRect(soloBtnRect, new Color(0.20f, 0.45f, 0.28f, 0.95f));
            DrawBorderRect(soloBtnRect, new Color(0.60f, 0.95f, 0.65f), 1.5f);

            if (GUI.Button(soloBtnRect, soloLabel, buttonPrimaryStyle))
            {
                StartGameWithCurrentCharacter();
            }
        }

        #region Create Room Modal
        private void OpenCreateRoomModal()
        {
            showJoinRoomModal = false;
            showCreateRoomModal = true;
            inputCreateRoomId = CozyNetworkCodec.GenerateRoomId();
            string pName = string.IsNullOrWhiteSpace(currentData.characterName) ? "Bạn" : currentData.characterName;
            inputCreateRoomName = $"Thế Giới Của {pName}";
            inputCreateMaxPlayers = 8;
        }

        private void DrawCreateRoomModal()
        {
            // Dark overlay
            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.78f));

            float mw = 490f;
            float mh = 335f;
            Rect mRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

            DrawSolidRect(mRect, new Color(0.10f, 0.13f, 0.18f, 0.98f));
            DrawBorderRect(mRect, new Color(0.40f, 0.85f, 1f), 2f);

            // Title
            GUI.Label(new Rect(mRect.x, mRect.y + 12, mw, 22), "🌐 TẠO PHÒNG CHƠI ONLINE (HOST)", titleHeaderStyle);
            GUI.Label(new Rect(mRect.x, mRect.y + 34, mw, 16), "Bạn bè ở bất kỳ mạng nào (Wi-Fi khác, 4G, mọi nơi) chỉ cần nhập mã ID là vào chung thế giới!", subHeaderStyle);

            float cx = mRect.x + 20f;
            float cw = mw - 40f;
            float cy = mRect.y + 60f;

            // Mã Phòng ID
            GUI.Label(new Rect(cx, cy, cw, 16), "Mã Phòng ID (Gửi mã này cho bạn bè):", labelStyle);
            cy += 18f;

            Rect idBox = new Rect(cx, cy, cw - 110f, 28);
            GUI.Box(idBox, inputCreateRoomId, centerBoxStyle);
            if (GUI.Button(new Rect(cx + cw - 100f, cy, 100f, 28), "🎲 Đổi Mã", buttonNormalStyle))
            {
                inputCreateRoomId = CozyNetworkCodec.GenerateRoomId();
            }
            cy += 36f;

            // Tên Phòng
            GUI.Label(new Rect(cx, cy, cw, 16), "Tên Thế Giới:", labelStyle);
            cy += 18f;
            inputCreateRoomName = GUI.TextField(new Rect(cx, cy, cw, 26), inputCreateRoomName, 30, inputStyle);
            cy += 34f;

            // Chế độ mạng
            GUI.Label(new Rect(cx, cy, cw, 16), "🌐 Kết nối: Đám Mây Toàn Cầu (Khác mạng vào tự do, không cần VPN)", subHeaderStyle);
            cy += 20f;

            // Số người tối đa
            GUI.Label(new Rect(cx, cy, 140, 24), $"Số người tối đa: {inputCreateMaxPlayers} người", labelStyle);
            if (GUI.Button(new Rect(cx + 150, cy, 32, 22), "2", (inputCreateMaxPlayers == 2) ? tabActiveStyle : buttonNormalStyle)) inputCreateMaxPlayers = 2;
            if (GUI.Button(new Rect(cx + 186, cy, 32, 22), "4", (inputCreateMaxPlayers == 4) ? tabActiveStyle : buttonNormalStyle)) inputCreateMaxPlayers = 4;
            if (GUI.Button(new Rect(cx + 222, cy, 32, 22), "8", (inputCreateMaxPlayers == 8) ? tabActiveStyle : buttonNormalStyle)) inputCreateMaxPlayers = 8;
            if (GUI.Button(new Rect(cx + 258, cy, 36, 22), "16", (inputCreateMaxPlayers == 16) ? tabActiveStyle : buttonNormalStyle)) inputCreateMaxPlayers = 16;

            if (GUI.Button(new Rect(cx + cw - 130, cy, 130, 24), "📋 Sao Chép Mã", buttonNormalStyle))
            {
                GUIUtility.systemCopyBuffer = inputCreateRoomId;
                CozyLifeHUD.PushToast($"📋 Đã sao chép mã phòng [{inputCreateRoomId}]!");
            }
            cy += 38f;

            // Nút Thao tác dưới đáy
            float btnY = mRect.y + mh - 46f;
            if (GUI.Button(new Rect(cx, btnY, 110, 32), "✕ Đóng", buttonNormalStyle))
            {
                showCreateRoomModal = false;
            }

            Rect hostBtnRect = new Rect(cx + cw - 280, btnY, 280, 32);
            DrawSolidRect(hostBtnRect, new Color(0.18f, 0.42f, 0.28f, 0.95f));
            DrawBorderRect(hostBtnRect, new Color(0.60f, 0.95f, 0.65f), 1.5f);
            if (GUI.Button(hostBtnRect, "🚀 KHỞI TẠO PHÒNG & VÀO GAME", buttonPrimaryStyle))
            {
                StartHostGame();
            }
        }

        private void StartHostGame()
        {
            if (string.IsNullOrWhiteSpace(currentData.characterName))
            {
                currentData.characterName = (currentData.gender == GenderType.Male) ? "Bình An" : "Mai Chi";
            }

            currentData.SaveToPrefs();
            CozyAudioManager.PlaySfx(CozySfxType.Coin);

            if (boundProfInv != null)
            {
                boundProfInv.PlayerName = currentData.characterName;
                boundProfInv.SetAppearance(currentData);
                boundProfInv.ChangeProfession(currentData.starterProfession, false);
            }

            if (CozyNetworkManager.Instance == null)
            {
                GameObject netObj = new GameObject("CozyNetworkManager");
                netObj.AddComponent<CozyNetworkManager>();
            }

            bool ok = CozyNetworkManager.Instance.StartHostRoom(inputCreateRoomId, inputCreateRoomName, CozyNetworkCodec.DefaultPort, inputCreateMaxPlayers);
            if (!ok)
            {
                return;
            }

            showCreateRoomModal = false;
            IsActive = false;
            isReCustomizingInGame = false;
            CozySaveManager.SaveGame(false);

            if (CozyLifeHUD.Instance != null)
            {
                CozyLifeHUD.Instance.PlaySceneTransition($"🌐 Phòng [{inputCreateRoomId}] đã sẵn sàng! Chào mừng {currentData.characterName}!", () =>
                {
                    CozyLifeHUD.PushToast($"👑 Bạn là Chủ Phòng [{inputCreateRoomId}]. Đang sẵn sàng đón bạn bè cùng vào!");
                });
            }
        }
        #endregion

        #region Join Room Modal
        private void OpenJoinRoomModal()
        {
            showCreateRoomModal = false;
            showJoinRoomModal = true;
            joinStatusMessage = "";
            isConnectingOnline = false;
        }

        private void DrawJoinRoomModal()
        {
            // Dark overlay
            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.78f));

            float mw = 530f;
            float mh = 385f;
            Rect mRect = new Rect((Screen.width - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

            DrawSolidRect(mRect, new Color(0.10f, 0.13f, 0.18f, 0.98f));
            DrawBorderRect(mRect, new Color(1f, 0.84f, 0.35f), 2f);

            // Title
            GUI.Label(new Rect(mRect.x, mRect.y + 12, mw, 22), "🔑 VÀO PHÒNG THEO MÃ ID", titleHeaderStyle);
            GUI.Label(new Rect(mRect.x, mRect.y + 34, mw, 16), "Nhập mã ID phòng (Khác mạng hay cùng mạng đều vào tự do không cần VPN)", subHeaderStyle);

            float cx = mRect.x + 20f;
            float cw = mw - 40f;
            float cy = mRect.y + 58f;

            // Nhập mã phòng ID hoặc IP
            GUI.Label(new Rect(cx, cy, cw, 16), "Mã Phòng ID (VD: CZ-4821) hoặc IP:", labelStyle);
            cy += 18f;

            float pasteBtnW = 125f;
            inputJoinRoomTarget = GUI.TextField(new Rect(cx, cy, cw - pasteBtnW - 8f, 28), inputJoinRoomTarget, 50, inputStyle);
            if (GUI.Button(new Rect(cx + cw - pasteBtnW, cy, pasteBtnW, 28), "📋 Dán từ Bộ Nhớ", buttonNormalStyle))
            {
                if (!string.IsNullOrEmpty(GUIUtility.systemCopyBuffer))
                {
                    inputJoinRoomTarget = GUIUtility.systemCopyBuffer.Trim();
                }
            }
            cy += 36f;

            // Khu vực phòng Online toàn cầu & LAN tự động phát hiện
            GUI.Label(new Rect(cx, cy, cw, 16), "📡 CÁC PHÒNG ĐANG MỞ ONLINE TOÀN CẦU & LAN:", labelStyle);
            cy += 20f;

            Rect lanBox = new Rect(cx, cy, cw, 120f);
            DrawSolidRect(lanBox, new Color(0.06f, 0.08f, 0.12f, 0.95f));
            DrawBorderRect(lanBox, new Color(0.28f, 0.36f, 0.48f), 1f);

            var discovered = CozyNetworkManager.Instance != null ? CozyNetworkManager.Instance.GetAllAvailableRooms() : new List<DiscoveredRoom>();
            if (discovered != null && discovered.Count > 0)
            {
                float lanContentH = Mathf.Max(116f, discovered.Count * 32f);
                lanRoomsScrollPos = GUI.BeginScrollView(new Rect(cx + 4, cy + 4, cw - 8, 112f), lanRoomsScrollPos, new Rect(0, 0, cw - 26, lanContentH));
                float rowY = 2f;
                foreach (var room in discovered)
                {
                    Rect rowRect = new Rect(2, rowY, cw - 30, 28);
                    DrawSolidRect(rowRect, new Color(0.14f, 0.18f, 0.24f, 0.9f));
                    DrawBorderRect(rowRect, new Color(0.4f, 0.7f, 1f, 0.6f), 1f);

                    string rTag = (room.hostIp == "Cloud") ? "<color=#66ddff>[🌐 Toàn Cầu]</color>" : "<color=#88ff88>[📡 LAN]</color>";
                    string rLabel = $"{rTag} <b>[{room.roomId}]</b> {room.roomName} • {room.currentPlayers}/{room.maxPlayers} người";
                    GUIStyle rSt = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11, normal = { textColor = Color.white } };
                    GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 4, rowRect.width - 90, 20), rLabel, rSt);

                    if (GUI.Button(new Rect(rowRect.x + rowRect.width - 80, rowRect.y + 2, 76, 24), "Vào Ngay", buttonOnlineStyle))
                    {
                        inputJoinRoomTarget = room.roomId;
                        StartJoinGame();
                    }
                    rowY += 32f;
                }
                GUI.EndScrollView();
            }
            else
            {
                GUIStyle emptySt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.6f, 0.65f, 0.72f) }
                };
                GUI.Label(lanBox, "Đang quét tìm phòng online toàn cầu...\n(Bạn bè ở bất cứ đâu khác mạng chỉ cần nhập mã ID phòng là vào được ngay!)", emptySt);
            }
            cy += 128f;

            // Thông báo trạng thái kết nối
            if (!string.IsNullOrEmpty(joinStatusMessage))
            {
                GUIStyle statSt = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = isConnectingOnline ? Color.yellow : new Color(1f, 0.45f, 0.45f) }
                };
                GUI.Label(new Rect(cx, cy, cw, 18), joinStatusMessage, statSt);
            }
            cy += 20f;

            // Nút Thao tác dưới đáy
            float btnY = mRect.y + mh - 46f;
            if (GUI.Button(new Rect(cx, btnY, 110, 32), "✕ Đóng", buttonNormalStyle))
            {
                showJoinRoomModal = false;
                joinStatusMessage = "";
                isConnectingOnline = false;
            }

            Rect joinBtnRect = new Rect(cx + cw - 260, btnY, 260, 32);
            DrawSolidRect(joinBtnRect, new Color(0.38f, 0.28f, 0.12f, 0.95f));
            DrawBorderRect(joinBtnRect, new Color(1f, 0.85f, 0.4f), 1.5f);
            string joinBtnText = isConnectingOnline ? "⏳ Đang Kết Nối..." : "🚀 KẾT NỐI & VÀO PHÒNG";
            if (GUI.Button(joinBtnRect, joinBtnText, buttonJoinStyle) && !isConnectingOnline)
            {
                StartJoinGame();
            }
        }

        private void StartJoinGame()
        {
            if (string.IsNullOrWhiteSpace(inputJoinRoomTarget))
            {
                joinStatusMessage = "⚠️ Vui lòng nhập mã phòng ID (VD: CZ-4821) hoặc địa chỉ IP!";
                return;
            }

            if (string.IsNullOrWhiteSpace(currentData.characterName))
            {
                currentData.characterName = (currentData.gender == GenderType.Male) ? "Bình An" : "Mai Chi";
            }

            currentData.SaveToPrefs();
            isConnectingOnline = true;
            joinStatusMessage = "⏳ Đang kết nối đến phòng...";

            if (CozyNetworkManager.Instance == null)
            {
                GameObject netObj = new GameObject("CozyNetworkManager");
                netObj.AddComponent<CozyNetworkManager>();
            }

            CozyNetworkManager.Instance.JoinRoomByTarget(inputJoinRoomTarget, (success, msg) =>
            {
                isConnectingOnline = false;
                if (!success)
                {
                    joinStatusMessage = $"❌ {msg}";
                }
                else
                {
                    if (boundProfInv != null)
                    {
                        boundProfInv.PlayerName = currentData.characterName;
                        boundProfInv.SetAppearance(currentData);
                        boundProfInv.ChangeProfession(currentData.starterProfession, false);
                    }

                    showJoinRoomModal = false;
                    IsActive = false;
                    isReCustomizingInGame = false;

                    if (CozyLifeHUD.Instance != null)
                    {
                        CozyLifeHUD.Instance.PlaySceneTransition($"🌿 Đã gia nhập phòng thành công!", () =>
                        {
                            CozyLifeHUD.PushToast($"🤝 Đã vào thế giới chung cùng bạn bè!");
                        });
                    }
                }
            });
        }
        #endregion

        private void StartGameWithCurrentCharacter()
        {
            if (string.IsNullOrWhiteSpace(currentData.characterName))
            {
                currentData.characterName = (currentData.gender == GenderType.Male) ? "Bình An" : "Mai Chi";
            }

            currentData.SaveToPrefs();
            CozyAudioManager.PlaySfx(CozySfxType.Coin);

            bool hasSave = CozySaveManager.HasSaveData();
            if (boundProfInv != null)
            {
                if (!isReCustomizingInGame && hasSave)
                {
                    CozySaveManager.LoadGame(false);
                    boundProfInv.PlayerName = currentData.characterName;
                    boundProfInv.SetAppearance(currentData);
                    boundProfInv.ChangeProfession(currentData.starterProfession, false);
                }
                else
                {
                    boundProfInv.PlayerName = currentData.characterName;
                    boundProfInv.SetAppearance(currentData);
                    boundProfInv.ChangeProfession(currentData.starterProfession, !isReCustomizingInGame);
                }
            }

            IsActive = false;
            isReCustomizingInGame = false;
            CozySaveManager.SaveGame(false);

            if (CozyLifeHUD.Instance != null)
            {
                CozyLifeHUD.Instance.PlaySceneTransition($"🌳 Chào mừng {currentData.characterName} đến với Cozy Life!", () =>
                {
                    CozyLifeHUD.PushToast(hasSave
                        ? $"📂 Đã khôi phục tiến trình lưu của {currentData.characterName}!"
                        : $"🎉 Bắt đầu cuộc sống mới với nghề {CozyLifeDatabase.GetProfession(currentData.starterProfession).titleBadge}!");
                });
            }
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex);
            GUI.color = prev;
        }

        private void DrawBorderRect(Rect rect, Color color, float width = 1f)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, width), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            DrawSolidRect(new Rect(rect.x, rect.y, width, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }
}
