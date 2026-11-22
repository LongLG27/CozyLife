using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyLife.Player
{
    /// <summary>
    /// Bộ tổng hợp ngoại trang nhân vật Isometric pixel-art 64x84 hỗ trợ đa khung hình chuyển động (Walk Cycle).
    /// Tự động kết hợp lớp thân, biểu cảm, tóc, áo, quần, giày và phụ kiện cùng chuyển động vung tay & bước chân.
    /// </summary>
    public static class CharacterAppearanceCompositor
    {
        private const int W = 64;
        private const int H = 84;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        private struct WalkPoseOffsets
        {
            public int bodyBobY;
            public int hairBounceY;
            public int leftArmDX;
            public int leftArmDY;
            public int rightArmDX;
            public int rightArmDY;
            public int leftLegDX;
            public int leftLegDY;
            public int rightLegDX;
            public int rightLegDY;
            public int skirtSwayX;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            spriteCache.Clear();
            texCache.Clear();
        }

        public static Sprite GetOrCreateCustomSprite(CharacterAppearanceData data, int walkFrame = 0)
        {
            if (data == null) data = new CharacterAppearanceData();
            int normalizedFrame = ((walkFrame % 4) + 4) % 4;
            string hash = $"{data.GetAppearanceHash()}_F{normalizedFrame}";

            if (spriteCache.TryGetValue(hash, out Sprite cached) && cached != null)
                return cached;

            Texture2D tex = GetOrCreateCustomTexture(data, normalizedFrame);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.15f), 85f);
            sp.name = $"CharSprite_{data.characterName}_{hash}";
            spriteCache[hash] = sp;
            return sp;
        }

        public static Texture2D GetOrCreateCustomTexture(CharacterAppearanceData data, int walkFrame = 0)
        {
            if (data == null) data = new CharacterAppearanceData();
            int normalizedFrame = ((walkFrame % 4) + 4) % 4;
            string hash = $"{data.GetAppearanceHash()}_F{normalizedFrame}";

            if (texCache.TryGetValue(hash, out Texture2D cached) && cached != null)
                return cached;

            Color32[] pixels = new Color32[W * H];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            WalkPoseOffsets pose = GetWalkPoseOffsets(normalizedFrame);
            DrawCharacter(pixels, data, pose);

            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(pixels);
            tex.Apply();

            texCache[hash] = tex;
            return tex;
        }

        private static WalkPoseOffsets GetWalkPoseOffsets(int frame)
        {
            return frame switch
            {
                // Frame 1: Chân trái bước cao về trước, tay phải đánh mạnh về trước, tay trái vung ra sau
                1 => new WalkPoseOffsets
                {
                    bodyBobY = 2,
                    hairBounceY = 3,
                    leftArmDX = -2,
                    leftArmDY = 4,
                    rightArmDX = -1,
                    rightArmDY = -3,
                    leftLegDX = -2,
                    leftLegDY = 4,
                    rightLegDX = 1,
                    rightLegDY = -1,
                    skirtSwayX = -1
                },
                // Frame 2: Nhịp chuyển giao giữa hai bước chân (nhún nhẹ)
                2 => new WalkPoseOffsets
                {
                    bodyBobY = 1,
                    hairBounceY = 1,
                    leftArmDX = -1,
                    leftArmDY = 1,
                    rightArmDX = 1,
                    rightArmDY = 1,
                    leftLegDX = -1,
                    leftLegDY = 1,
                    rightLegDX = 1,
                    rightLegDY = 1,
                    skirtSwayX = 0
                },
                // Frame 3: Chân phải bước cao về trước, tay trái đánh mạnh về trước, tay phải vung ra sau
                3 => new WalkPoseOffsets
                {
                    bodyBobY = 2,
                    hairBounceY = 3,
                    leftArmDX = 1,
                    leftArmDY = -3,
                    rightArmDX = 2,
                    rightArmDY = 4,
                    leftLegDX = -1,
                    leftLegDY = -1,
                    rightLegDX = 2,
                    rightLegDY = 4,
                    skirtSwayX = 1
                },
                // Frame 0: Đứng yên (Idle)
                _ => new WalkPoseOffsets()
            };
        }

        private static void DrawCharacter(Color32[] pixels, CharacterAppearanceData d, WalkPoseOffsets pose)
        {
            Color32 skin = CharacterAppearanceData.SkinColors[Mathf.Clamp(d.skinToneIndex, 0, CharacterAppearanceData.SkinColors.Length - 1)];
            Color32 skinShade = Darken(skin, 0.82f);
            Color32 hairCol = CharacterAppearanceData.HairColors[Mathf.Clamp(d.hairColorIndex, 0, CharacterAppearanceData.HairColors.Length - 1)];
            Color32 shirtCol = CharacterAppearanceData.OutfitColors[Mathf.Clamp(d.shirtColorIndex, 0, CharacterAppearanceData.OutfitColors.Length - 1)];
            Color32 pantsCol = CharacterAppearanceData.PantsColors[Mathf.Clamp(d.pantsColorIndex, 0, CharacterAppearanceData.PantsColors.Length - 1)];

            // 1. Bóng đổ dưới chân (Soft Shadow co giãn nhẹ theo nhịp bước)
            int shadowRx = 13 + (pose.bodyBobY > 1 ? 1 : 0);
            DrawEllipse(pixels, 32, 4, shadowRx, 3, new Color32(15, 20, 28, 85));

            // 2. Tóc sau lưng (nếu tóc dài, nhún theo nhịp bước)
            DrawBackHair(pixels, d.hairStyleIndex, d.gender, hairCol, pose.hairBounceY);

            // 3. Chân / Giày (bước chân trái/phải)
            DrawShoesAndLegs(pixels, d, skin, skinShade, pantsCol, pose);

            // 4. Quần / Váy (co duỗi theo bước chân)
            DrawPantsOrSkirt(pixels, d, pantsCol, pose);

            // 5. Thân & Tay (Áo + Vung hai cánh tay)
            DrawShirtAndArms(pixels, d, shirtCol, skin, skinShade, pose);

            // 6. Đầu & Khuôn mặt
            DrawHeadBase(pixels, skin, skinShade, pose.bodyBobY);

            // 7. Biểu cảm (Mắt, Miệng, Má hồng)
            DrawExpression(pixels, d.expressionIndex, pose.bodyBobY);

            // 8. Tóc trước & Mái
            DrawFrontHair(pixels, d.hairStyleIndex, d.gender, hairCol, pose.bodyBobY);

            // 9. Phụ kiện (Kính, Mũ, Nơ, Khăn quàng)
            DrawAccessory(pixels, d.accessoryIndex, hairCol, shirtCol, pose.bodyBobY, pose.hairBounceY);
        }

        private static void DrawHeadBase(Color32[] px, Color32 skin, Color32 skinShade, int bobY)
        {
            // Cổ
            DrawFilledRect(px, 29, 43 + bobY, 6, 5, skinShade);

            // Khuôn mặt trái xoan chibi
            DrawEllipse(px, 32, 56 + bobY, 11, 9, skin);
            DrawEllipseOutline(px, 32, 56 + bobY, 11, 9, skinShade);

            // Tai
            DrawFilledRect(px, 19, 54 + bobY, 2, 4, skin);
            DrawFilledRect(px, 43, 54 + bobY, 2, 4, skin);
            DrawPixel(px, 20, 55 + bobY, skinShade);
            DrawPixel(px, 43, 55 + bobY, skinShade);
        }

        private static void DrawExpression(Color32[] px, int exprIdx, int bobY)
        {
            Color32 eyeDark = new Color32(28, 30, 42, 255);
            Color32 eyeWhite = new Color32(250, 250, 252, 255);
            Color32 blush = new Color32(245, 128, 142, 175);
            Color32 mouthCol = new Color32(178, 72, 85, 255);

            // Má hồng dễ thương hai bên
            DrawFilledRect(px, 22, 52 + bobY, 3, 2, blush);
            DrawFilledRect(px, 39, 52 + bobY, 3, 2, blush);

            // Lông mày
            DrawFilledRect(px, 25, 60 + bobY, 4, 1, eyeDark);
            DrawFilledRect(px, 35, 60 + bobY, 4, 1, eyeDark);

            switch (exprIdx)
            {
                case 1: // 😎 Điềm tĩnh tự tin
                    DrawFilledRect(px, 24, 55 + bobY, 4, 2, eyeWhite);
                    DrawFilledRect(px, 36, 55 + bobY, 4, 2, eyeWhite);
                    DrawFilledRect(px, 26, 55 + bobY, 2, 2, eyeDark);
                    DrawFilledRect(px, 36, 55 + bobY, 2, 2, eyeDark);
                    DrawPixel(px, 32, 49 + bobY, mouthCol);
                    DrawPixel(px, 33, 49 + bobY, mouthCol);
                    break;

                case 2: // 😉 Nháy mắt tinh nghịch
                    DrawFilledRect(px, 24, 54 + bobY, 4, 4, eyeWhite);
                    DrawFilledRect(px, 25, 54 + bobY, 2, 3, eyeDark);
                    DrawPixel(px, 25, 56 + bobY, eyeWhite);
                    DrawPixel(px, 36, 55 + bobY, eyeDark);
                    DrawPixel(px, 37, 56 + bobY, eyeDark);
                    DrawPixel(px, 38, 56 + bobY, eyeDark);
                    DrawPixel(px, 39, 55 + bobY, eyeDark);
                    DrawFilledRect(px, 31, 48 + bobY, 3, 2, mouthCol);
                    break;

                case 3: // 🙂 Trầm ấm dịu dàng
                    DrawFilledRect(px, 25, 55 + bobY, 3, 2, eyeDark);
                    DrawFilledRect(px, 36, 55 + bobY, 3, 2, eyeDark);
                    DrawPixel(px, 25, 56 + bobY, eyeWhite);
                    DrawPixel(px, 36, 56 + bobY, eyeWhite);
                    DrawFilledRect(px, 31, 49 + bobY, 3, 1, mouthCol);
                    break;

                case 4: // 🤩 Mắt sao long lanh
                    Color32 starGold = new Color32(255, 215, 60, 255);
                    DrawFilledRect(px, 24, 54 + bobY, 4, 4, eyeWhite);
                    DrawFilledRect(px, 36, 54 + bobY, 4, 4, eyeWhite);
                    DrawFilledRect(px, 25, 54 + bobY, 2, 3, starGold);
                    DrawFilledRect(px, 37, 54 + bobY, 2, 3, starGold);
                    DrawPixel(px, 26, 56 + bobY, eyeWhite);
                    DrawPixel(px, 38, 56 + bobY, eyeWhite);
                    DrawFilledRect(px, 30, 48 + bobY, 4, 2, mouthCol);
                    break;

                default: // 😊 Tươi cười rạng rỡ (Mặc định)
                    DrawFilledRect(px, 24, 54 + bobY, 4, 4, eyeWhite);
                    DrawFilledRect(px, 36, 54 + bobY, 4, 4, eyeWhite);
                    DrawFilledRect(px, 25, 54 + bobY, 2, 3, eyeDark);
                    DrawFilledRect(px, 37, 54 + bobY, 2, 3, eyeDark);
                    DrawPixel(px, 25, 56 + bobY, eyeWhite);
                    DrawPixel(px, 37, 56 + bobY, eyeWhite);
                    DrawFilledRect(px, 31, 48 + bobY, 3, 2, mouthCol);
                    DrawPixel(px, 32, 48 + bobY, eyeWhite);
                    break;
            }
        }

        private static void DrawBackHair(Color32[] px, int styleIdx, GenderType gender, Color32 hairCol, int bounceY)
        {
            Color32 darkHair = Darken(hairCol, 0.75f);

            if (gender == GenderType.Female)
            {
                if (styleIdx == 0) // Tóc dài xoăn sóng
                {
                    DrawFilledRect(px, 18, 40 + bounceY, 6, 20, darkHair);
                    DrawFilledRect(px, 40, 40 + bounceY, 6, 20, darkHair);
                    DrawFilledRect(px, 19, 34 + bounceY, 5, 8, darkHair);
                    DrawFilledRect(px, 40, 34 + bounceY, 5, 8, darkHair);
                }
                else if (styleIdx == 2) // Đuôi ngựa
                {
                    DrawFilledRect(px, 38, 48 + bounceY, 6, 16, darkHair);
                    DrawFilledRect(px, 42, 42 + bounceY, 4, 10, darkHair);
                }
                else if (styleIdx == 3) // Hai chùm
                {
                    DrawFilledRect(px, 15, 42 + bounceY, 6, 16, darkHair);
                    DrawFilledRect(px, 43, 42 + bounceY, 6, 16, darkHair);
                }
            }
            else
            {
                if (styleIdx == 5) // Tóc dài lãng tử cột đuôi
                {
                    DrawFilledRect(px, 31, 46 + bounceY, 3, 10, darkHair);
                }
            }
        }

        private static void DrawFrontHair(Color32[] px, int styleIdx, GenderType gender, Color32 hairCol, int bobY)
        {
            Color32 lightHair = Lighten(hairCol, 1.25f);
            Color32 darkHair = Darken(hairCol, 0.78f);

            // Mái tóc chung đỉnh đầu
            DrawEllipse(px, 32, 64 + bobY, 12, 6, hairCol);
            DrawFilledRect(px, 22, 61 + bobY, 20, 4, hairCol);
            DrawFilledRect(px, 26, 65 + bobY, 12, 1, lightHair);

            if (gender == GenderType.Male)
            {
                switch (styleIdx)
                {
                    case 1: // Rẽ ngôi lãng tử
                        DrawFilledRect(px, 21, 58 + bobY, 4, 5, hairCol);
                        DrawFilledRect(px, 39, 58 + bobY, 4, 5, hairCol);
                        DrawFilledRect(px, 28, 60 + bobY, 4, 2, darkHair);
                        break;
                    case 2: // Vuốt dựng spiky
                        DrawFilledRect(px, 27, 68 + bobY, 10, 3, hairCol);
                        DrawPixel(px, 30, 71 + bobY, hairCol);
                        DrawPixel(px, 34, 71 + bobY, hairCol);
                        break;
                    case 3: // Tóc xù xoăn
                        DrawFilledRect(px, 20, 60 + bobY, 5, 6, hairCol);
                        DrawFilledRect(px, 39, 60 + bobY, 5, 6, hairCol);
                        DrawFilledRect(px, 23, 67 + bobY, 18, 2, hairCol);
                        break;
                    case 4: // Rẽ ngôi 7/3 Hàn Quốc
                        DrawFilledRect(px, 22, 59 + bobY, 11, 3, hairCol);
                        DrawFilledRect(px, 35, 59 + bobY, 7, 2, hairCol);
                        break;
                    case 5: // Cột đuôi
                        DrawFilledRect(px, 21, 57 + bobY, 4, 6, hairCol);
                        DrawFilledRect(px, 39, 57 + bobY, 4, 6, hairCol);
                        break;
                    default: // Ngắn vuốt thể thao
                        DrawFilledRect(px, 21, 59 + bobY, 22, 3, hairCol);
                        break;
                }
            }
            else // Nữ
            {
                switch (styleIdx)
                {
                    case 1: // Bob ngắn
                        DrawFilledRect(px, 19, 52 + bobY, 4, 12, hairCol);
                        DrawFilledRect(px, 41, 52 + bobY, 4, 12, hairCol);
                        DrawFilledRect(px, 23, 59 + bobY, 18, 3, hairCol);
                        break;
                    case 2: // Đuôi ngựa
                        DrawFilledRect(px, 21, 59 + bobY, 22, 3, hairCol);
                        DrawFilledRect(px, 38, 65 + bobY, 4, 4, darkHair);
                        break;
                    case 3: // Hai chùm
                        DrawFilledRect(px, 19, 58 + bobY, 4, 6, hairCol);
                        DrawFilledRect(px, 41, 58 + bobY, 4, 6, hairCol);
                        DrawFilledRect(px, 16, 62 + bobY, 4, 4, hairCol);
                        DrawFilledRect(px, 44, 62 + bobY, 4, 4, hairCol);
                        break;
                    case 4: // Búi củ tỏi
                        DrawEllipse(px, 32, 72 + bobY, 5, 4, hairCol);
                        DrawPixel(px, 32, 73 + bobY, lightHair);
                        break;
                    case 5: // Tóc tém sành điệu
                        DrawFilledRect(px, 21, 58 + bobY, 22, 4, hairCol);
                        DrawPixel(px, 20, 56 + bobY, hairCol);
                        DrawPixel(px, 43, 56 + bobY, hairCol);
                        break;
                    default: // Dài xoăn sóng
                        DrawFilledRect(px, 19, 48 + bobY, 4, 14, hairCol);
                        DrawFilledRect(px, 41, 48 + bobY, 4, 14, hairCol);
                        DrawFilledRect(px, 22, 59 + bobY, 20, 3, hairCol);
                        break;
                }
            }
        }

        private static void DrawShirtAndArms(Color32[] px, CharacterAppearanceData d, Color32 shirtCol, Color32 skin, Color32 skinShade, WalkPoseOffsets pose)
        {
            int bobY = pose.bodyBobY;
            Color32 shirtShade = Darken(shirtCol, 0.80f);
            Color32 shirtHighlight = Lighten(shirtCol, 1.20f);

            int lArmX = 18 + pose.leftArmDX;
            int lArmY = 26 + bobY + pose.leftArmDY;
            int rArmX = 40 + pose.rightArmDX;
            int rArmY = 26 + bobY + pose.rightArmDY;

            // Cầu vai nối thân áo với 2 cánh tay để khi tay vung mạnh vẫn liền lạc
            DrawFilledRect(px, 19, 39 + bobY, 5, 5, shirtCol);
            DrawFilledRect(px, 40, 39 + bobY, 5, 5, shirtCol);

            // Cánh tay Trái & Phải (vung lên/xuống và tách ngang khi bước đi)
            if (d.shirtIndex == 0) // Áo thun cộc tay: lộ cánh tay da
            {
                // Ống tay ngắn
                DrawFilledRect(px, lArmX, Mathf.Max(34 + bobY, lArmY + 10), 6, 6, shirtCol);
                DrawFilledRect(px, rArmX, Mathf.Max(34 + bobY, rArmY + 10), 6, 6, shirtCol);
                // Cẳng tay da
                DrawFilledRect(px, lArmX + 1, lArmY + 4, 4, 8, skin);
                DrawFilledRect(px, rArmX + 1, rArmY + 4, 4, 8, skin);
                // Bàn tay nắm rõ ràng
                DrawFilledRect(px, lArmX + 1, lArmY, 4, 4, skinShade);
                DrawFilledRect(px, rArmX + 1, rArmY, 4, 4, skinShade);
            }
            else // Áo dài tay (Sơ mi, Hoodie, Len, Jacket)
            {
                int lSleeveTop = 43 + bobY;
                int lSleeveBottom = lArmY + 3;
                int lSleeveH = Mathf.Max(6, lSleeveTop - lSleeveBottom);
                DrawFilledRect(px, lArmX, lSleeveBottom, 6, lSleeveH, shirtCol);
                DrawFilledRect(px, lArmX, lSleeveBottom, 6, 2, shirtShade);
                DrawFilledRect(px, lArmX + 1, lArmY, 4, 4, skin);
                DrawFilledRect(px, lArmX + 1, lArmY, 4, 1, skinShade);

                int rSleeveTop = 43 + bobY;
                int rSleeveBottom = rArmY + 3;
                int rSleeveH = Mathf.Max(6, rSleeveTop - rSleeveBottom);
                DrawFilledRect(px, rArmX, rSleeveBottom, 6, rSleeveH, shirtCol);
                DrawFilledRect(px, rArmX, rSleeveBottom, 6, 2, shirtShade);
                DrawFilledRect(px, rArmX + 1, rArmY, 4, 4, skin);
                DrawFilledRect(px, rArmX + 1, rArmY, 4, 1, skinShade);
            }

            // Thân áo chính (vẽ đè nhẹ phần ngực để tạo chiều sâu tay vung)
            DrawFilledRect(px, 24, 28 + bobY, 16, 16, shirtCol);
            DrawFilledRect(px, 24, 28 + bobY, 16, 1, shirtShade);

            // Cổ áo
            DrawFilledRect(px, 30, 42 + bobY, 4, 2, shirtHighlight);

            // Chi tiết đặc thù cho từng loại áo
            if (d.shirtIndex == 1) // Sơ mi: hàng cúc áo
            {
                DrawFilledRect(px, 31, 29 + bobY, 2, 14, shirtHighlight);
                for (int y = 30 + bobY; y <= 40 + bobY; y += 3)
                    DrawPixel(px, 32, y, new Color32(40, 40, 40, 255));
            }
            else if (d.shirtIndex == 3) // Hoodie: túi trước bụng
            {
                DrawFilledRect(px, 27, 29 + bobY, 10, 6, shirtShade);
            }
            else if (d.shirtIndex == 4) // Áo yếm: dây đai yếm
            {
                Color32 denim = new Color32(42, 70, 115, 255);
                DrawFilledRect(px, 26, 30 + bobY, 3, 14, denim);
                DrawFilledRect(px, 35, 30 + bobY, 3, 14, denim);
                DrawFilledRect(px, 26, 28 + bobY, 12, 6, denim);
            }
            else if (d.shirtIndex == 5) // Jacket denim: áo khoác mở
            {
                Color32 innerShirt = new Color32(235, 235, 240, 255);
                DrawFilledRect(px, 29, 28 + bobY, 6, 15, innerShirt);
            }

            // Nếu tay nào đang đánh về phía trước (DY < 0), vẽ bàn tay nổi lên trước vạt áo để thấy rõ động tác đánh tay!
            if (pose.leftArmDY < 0)
            {
                DrawFilledRect(px, lArmX + 1, lArmY, 5, 4, skin);
                DrawFilledRect(px, lArmX + 1, lArmY, 5, 1, skinShade);
            }
            if (pose.rightArmDY < 0)
            {
                DrawFilledRect(px, rArmX, rArmY, 5, 4, skin);
                DrawFilledRect(px, rArmX, rArmY, 5, 1, skinShade);
            }
        }

        private static void DrawPantsOrSkirt(Color32[] px, CharacterAppearanceData d, Color32 pantsCol, WalkPoseOffsets pose)
        {
            int bobY = pose.bodyBobY;
            Color32 pantsShade = Darken(pantsCol, 0.78f);
            Color32 pantsHighlight = Lighten(pantsCol, 1.12f);

            if (d.gender == GenderType.Female && d.pantsIndex == 2) // Váy xòe vintage (đung đưa theo bước chân)
            {
                for (int y = 0; y < 13; y++)
                {
                    int span = 7 + (13 - y);
                    int sway = (y < 6) ? pose.skirtSwayX : 0;
                    DrawFilledRect(px, 32 - span + sway, 16 + bobY + y, span * 2, 1, pantsCol);
                }
                DrawPixel(px, 26 + pose.skirtSwayX, 17 + bobY, pantsShade);
                DrawPixel(px, 32 + pose.skirtSwayX, 17 + bobY, pantsShade);
                DrawPixel(px, 38 + pose.skirtSwayX, 17 + bobY, pantsShade);
            }
            else if (d.pantsIndex == 3) // Quần short ngắn
            {
                DrawFilledRect(px, 25, 22 + bobY, 14, 7, pantsCol);
                int lShortY = 19 + bobY + Mathf.Max(0, pose.leftLegDY / 2);
                int rShortY = 19 + bobY + Mathf.Max(0, pose.rightLegDY / 2);
                DrawFilledRect(px, 24 + pose.leftLegDX / 2, lShortY, 6, 5, pantsCol);
                DrawFilledRect(px, 34 + pose.rightLegDX / 2, rShortY, 6, 5, pantsCol);
            }
            else // Quần dài (Jean, Kaki, Cargo, Yếm, Slacks)
            {
                // Phần hông quần
                DrawFilledRect(px, 24, 24 + bobY, 16, 5, pantsCol);

                // Ống quần trái (co lên/bước sang ngang theo chân trái)
                int lLegX = 24 + pose.leftLegDX;
                int lLegBottom = 11 + pose.leftLegDY;
                int lLegTop = 25 + bobY;
                int lLegH = Mathf.Max(6, lLegTop - lLegBottom);
                DrawFilledRect(px, lLegX, lLegBottom, 6, lLegH, pantsCol);

                // Tạo nếp gấp đầu gối sáng/tối khi chân trái nhấc cao
                if (pose.leftLegDY >= 2)
                {
                    DrawFilledRect(px, lLegX, lLegBottom + 4, 6, 2, pantsHighlight);
                    DrawFilledRect(px, lLegX, lLegBottom, 6, 1, pantsShade);
                }
                else if (pose.leftLegDY < 0)
                {
                    DrawFilledRect(px, lLegX, lLegBottom, 6, lLegH, pantsShade);
                }

                // Ống quần phải (co lên/bước sang ngang theo chân phải)
                int rLegX = 34 + pose.rightLegDX;
                int rLegBottom = 11 + pose.rightLegDY;
                int rLegTop = 25 + bobY;
                int rLegH = Mathf.Max(6, rLegTop - rLegBottom);
                DrawFilledRect(px, rLegX, rLegBottom, 6, rLegH, pantsCol);

                if (pose.rightLegDY >= 2)
                {
                    DrawFilledRect(px, rLegX, rLegBottom + 4, 6, 2, pantsHighlight);
                    DrawFilledRect(px, rLegX, rLegBottom, 6, 1, pantsShade);
                }
                else if (pose.rightLegDY < 0)
                {
                    DrawFilledRect(px, rLegX, rLegBottom, 6, rLegH, pantsShade);
                }

                // Đũng quần
                DrawFilledRect(px, 30, 22 + bobY, 4, 3, pantsShade);

                if (d.pantsIndex == 4) // Cargo: túi hộp hai bên hông
                {
                    DrawFilledRect(px, lLegX - 1, lLegBottom + 5, 2, 4, pantsShade);
                    DrawFilledRect(px, rLegX + 5, rLegBottom + 5, 2, 4, pantsShade);
                }
            }
        }

        private static void DrawShoesAndLegs(Color32[] px, CharacterAppearanceData d, Color32 skin, Color32 skinShade, Color32 pantsCol, WalkPoseOffsets pose)
        {
            int lShoeX = 23 + pose.leftLegDX;
            int lShoeY = 5 + pose.leftLegDY;
            int rShoeX = 34 + pose.rightLegDX;
            int rShoeY = 5 + pose.rightLegDY;

            // Nếu mặc quần short hoặc váy -> Vẽ cẳng chân lộ da co duỗi theo bước chân
            if (d.pantsIndex == 3 || (d.gender == GenderType.Female && d.pantsIndex == 2))
            {
                int lTop = 21 + pose.bodyBobY;
                int lBottom = lShoeY + 5;
                DrawFilledRect(px, 25 + pose.leftLegDX, lBottom, 5, Mathf.Max(4, lTop - lBottom), pose.leftLegDY < 0 ? skinShade : skin);

                int rTop = 21 + pose.bodyBobY;
                int rBottom = rShoeY + 5;
                DrawFilledRect(px, 34 + pose.rightLegDX, rBottom, 5, Mathf.Max(4, rTop - rBottom), pose.rightLegDY < 0 ? skinShade : skin);
            }

            // Mẫu giày
            Color32 shoeCol = d.shoesIndex switch
            {
                1 => new Color32(32, 30, 35, 255),    // Giày da đen
                2 => new Color32(110, 62, 38, 255),   // Bốt du hành nâu
                3 => new Color32(195, 75, 95, 255),   // Giày búp bê đỏ
                4 => new Color32(185, 150, 110, 255), // Sandal da
                _ => new Color32(245, 245, 248, 255)  // Sneaker trắng (Mặc định)
            };
            Color32 soleCol = new Color32(40, 42, 48, 255);

            // Giày trái
            DrawFilledRect(px, lShoeX, lShoeY + 1, 7, 5, shoeCol);
            int lSoleThickness = pose.leftLegDY >= 2 ? 2 : 1; // Khi nhấc chân cao sẽ lộ rõ đế giày hơn
            DrawFilledRect(px, lShoeX, lShoeY, 7, lSoleThickness, soleCol);

            // Giày phải
            DrawFilledRect(px, rShoeX, rShoeY + 1, 7, 5, shoeCol);
            int rSoleThickness = pose.rightLegDY >= 2 ? 2 : 1;
            DrawFilledRect(px, rShoeX, rShoeY, 7, rSoleThickness, soleCol);

            if (d.shoesIndex == 0) // Sneaker: dây giày trắng
            {
                DrawFilledRect(px, lShoeX + 2, lShoeY + 3, 3, 1, new Color32(200, 205, 215, 255));
                DrawFilledRect(px, rShoeX + 2, rShoeY + 3, 3, 1, new Color32(200, 205, 215, 255));
            }
            else if (d.shoesIndex == 2) // Bốt cổ cao
            {
                DrawFilledRect(px, lShoeX, lShoeY + 6, 7, 3, shoeCol);
                DrawFilledRect(px, rShoeX, rShoeY + 6, 7, 3, shoeCol);
            }
        }

        private static void DrawAccessory(Color32[] px, int accIdx, Color32 hairCol, Color32 shirtCol, int bobY, int bounceY)
        {
            switch (accIdx)
            {
                case 1: // 👓 Kính cận tri thức gọng tròn
                    Color32 frame = new Color32(35, 35, 40, 255);
                    Color32 glass = new Color32(210, 235, 255, 160);
                    DrawFilledRect(px, 23, 54 + bobY, 5, 4, glass);
                    DrawFilledRect(px, 36, 54 + bobY, 5, 4, glass);
                    DrawHollowRect(px, 23, 54 + bobY, 5, 4, frame);
                    DrawHollowRect(px, 36, 54 + bobY, 5, 4, frame);
                    DrawFilledRect(px, 28, 56 + bobY, 8, 1, frame);
                    break;

                case 2: // 🕶️ Kính râm đen sành điệu
                    Color32 sunFrame = new Color32(20, 20, 24, 255);
                    Color32 sunLens = new Color32(40, 42, 50, 245);
                    DrawFilledRect(px, 23, 53 + bobY, 6, 5, sunLens);
                    DrawFilledRect(px, 35, 53 + bobY, 6, 5, sunLens);
                    DrawFilledRect(px, 22, 57 + bobY, 20, 1, sunFrame);
                    break;

                case 3: // 👒 Mũ rơm dạo phố hè
                    Color32 straw = new Color32(236, 202, 128, 255);
                    Color32 strawDark = new Color32(196, 160, 94, 255);
                    Color32 ribbon = new Color32(195, 48, 58, 255);
                    DrawFilledRect(px, 15, 63 + bobY, 34, 3, straw);
                    DrawFilledRect(px, 14, 63 + bobY, 36, 1, strawDark);
                    DrawFilledRect(px, 23, 66 + bobY, 18, 7, straw);
                    DrawFilledRect(px, 23, 66 + bobY, 18, 2, ribbon);
                    break;

                case 4: // 🧶 Mũ len Beanie ấm áp
                    Color32 beanieCol = new Color32(195, 68, 55, 255);
                    Color32 beanieShade = new Color32(155, 48, 38, 255);
                    DrawEllipse(px, 32, 69 + bobY, 12, 7, beanieCol);
                    DrawFilledRect(px, 20, 61 + bobY, 24, 3, beanieShade);
                    DrawEllipse(px, 32, 76 + bounceY, 3, 3, new Color32(245, 245, 245, 255));
                    break;

                case 5: // 🧢 Mũ lưỡi trai thể thao
                    Color32 capCol = new Color32(45, 82, 145, 255);
                    Color32 capBrim = new Color32(35, 65, 115, 255);
                    DrawEllipse(px, 32, 67 + bobY, 11, 6, capCol);
                    DrawFilledRect(px, 25, 62 + bobY, 18, 3, capBrim);
                    break;

                case 6: // 🎀 Băng đô nơ cài tóc
                    Color32 bowCol = new Color32(235, 95, 128, 255);
                    DrawFilledRect(px, 21, 64 + bobY, 22, 2, bowCol);
                    DrawFilledRect(px, 40, 65 + bounceY, 5, 4, bowCol);
                    DrawPixel(px, 42, 67 + bounceY, new Color32(255, 240, 245, 255));
                    break;

                case 7: // 🧣 Khăn quàng cổ len ấm (đuôi khăn tung bay nhẹ theo nhịp bước)
                    Color32 scarfCol = new Color32(188, 62, 52, 255);
                    Color32 scarfFringe = new Color32(230, 180, 80, 255);
                    DrawFilledRect(px, 26, 41 + bobY, 12, 4, scarfCol);
                    DrawFilledRect(px, 34, 31 + bounceY, 4, 11, scarfCol);
                    DrawFilledRect(px, 34, 30 + bounceY, 4, 1, scarfFringe);
                    break;
            }
        }

        // =====================================================================
        // PRIMITIVE PIXEL DRAWING HELPERS
        // =====================================================================

        private static void DrawPixel(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H || c.a == 0) return;
            int idx = y * W + x;
            if (c.a == 255)
            {
                px[idx] = c;
            }
            else
            {
                Color32 bg = px[idx];
                float a = c.a / 255f;
                byte r = (byte)(c.r * a + bg.r * (1f - a));
                byte g = (byte)(c.g * a + bg.g * (1f - a));
                byte b = (byte)(c.b * a + bg.b * (1f - a));
                byte fa = (byte)Mathf.Min(255, bg.a + c.a);
                px[idx] = new Color32(r, g, b, fa);
            }
        }

        private static void DrawFilledRect(Color32[] px, int x, int y, int w, int h, Color32 c)
        {
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    DrawPixel(px, x + dx, y + dy, c);
                }
            }
        }

        private static void DrawHollowRect(Color32[] px, int x, int y, int w, int h, Color32 c)
        {
            for (int dx = 0; dx < w; dx++)
            {
                DrawPixel(px, x + dx, y, c);
                DrawPixel(px, x + dx, y + h - 1, c);
            }
            for (int dy = 0; dy < h; dy++)
            {
                DrawPixel(px, x, y + dy, c);
                DrawPixel(px, x + w - 1, y + dy, c);
            }
        }

        private static void DrawEllipse(Color32[] px, int cx, int cy, int rx, int ry, Color32 c)
        {
            for (int dy = -ry; dy <= ry; dy++)
            {
                for (int dx = -rx; dx <= rx; dx++)
                {
                    float nx = dx / (float)rx;
                    float ny = dy / (float)ry;
                    if (nx * nx + ny * ny <= 1.0f)
                    {
                        DrawPixel(px, cx + dx, cy + dy, c);
                    }
                }
            }
        }

        private static void DrawEllipseOutline(Color32[] px, int cx, int cy, int rx, int ry, Color32 c)
        {
            for (int dy = -ry; dy <= ry; dy++)
            {
                for (int dx = -rx; dx <= rx; dx++)
                {
                    float dist = (dx * dx) / (float)(rx * rx) + (dy * dy) / (float)(ry * ry);
                    if (dist <= 1.0f && dist >= 0.72f)
                    {
                        DrawPixel(px, cx + dx, cy + dy, c);
                    }
                }
            }
        }

        private static Color32 Darken(Color32 c, float factor)
        {
            return new Color32((byte)(c.r * factor), (byte)(c.g * factor), (byte)(c.b * factor), c.a);
        }

        private static Color32 Lighten(Color32 c, float factor)
        {
            byte r = (byte)Mathf.Min(255, c.r * factor);
            byte g = (byte)Mathf.Min(255, c.g * factor);
            byte b = (byte)Mathf.Min(255, c.b * factor);
            return new Color32(r, g, b, c.a);
        }
    }
}
