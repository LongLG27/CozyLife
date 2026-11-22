using System;
using UnityEngine;
using CozyLife.Core;

namespace CozyLife.Player
{
    public enum GenderType
    {
        Male = 0,
        Female = 1
    }

    /// <summary>
    /// Dữ liệu tùy biến ngoại trang nhân vật (Tên, Giới tính, Da, Tóc, Biểu cảm, Áo, Quần, Phụ kiện, Giày).
    /// </summary>
    [Serializable]
    public class CharacterAppearanceData
    {
        public string characterName = "Bình An";
        public GenderType gender = GenderType.Male;
        public int skinToneIndex = 1;      // 0: Trắng hồng, 1: Tự nhiên, 2: Rám nắng, 3: Ngăm
        public int hairStyleIndex = 0;     // 0..5 kiểu tóc
        public int hairColorIndex = 0;     // 0..5 màu tóc
        public int expressionIndex = 0;    // 0..4 biểu cảm
        public int shirtIndex = 0;         // 0..5 mẫu áo
        public int shirtColorIndex = 0;    // 0..5 màu áo
        public int pantsIndex = 0;         // 0..5 mẫu quần
        public int pantsColorIndex = 0;    // 0..5 màu quần
        public int accessoryIndex = 0;     // 0..7 phụ kiện
        public int shoesIndex = 0;         // 0..4 mẫu giày
        public ProfessionType starterProfession = ProfessionType.Farmer;

        public static readonly string[] MaleHairStyles = new string[]
        {
            "Tóc Ngắn Nam Tính",
            "Tóc Lãng Tử Rẽ Ngôi",
            "Tóc Vuốt Dựng Cá Tính",
            "Tóc Xù Dễ Thương",
            "Tóc Rẽ Ngôi 7/3 Hàn Quốc",
            "Tóc Dài Lãng Tử Cột Đuôi"
        };

        public static readonly string[] FemaleHairStyles = new string[]
        {
            "Tóc Dài Xoăn Gợn Sóng",
            "Tóc Bob Ngắn Thanh Lịch",
            "Tóc Đuôi Ngựa Năng Động",
            "Tóc Hai Chùm Đáng Yêu",
            "Tóc Búi Củ Tỏi Vintage",
            "Tóc Tém Sành Điệu"
        };

        public static readonly string[] HairColorNames = new string[]
        {
            "Đen Nhánh",
            "Nâu Hạt Dẻ",
            "Vàng Kim Rơm",
            "Bạch Kim Khói",
            "Nâu Đỏ Đồng",
            "Xanh Rêu Pastel"
        };

        public static readonly Color32[] HairColors = new Color32[]
        {
            new Color32(36, 32, 38, 255),
            new Color32(94, 58, 38, 255),
            new Color32(218, 172, 88, 255),
            new Color32(208, 212, 222, 255),
            new Color32(148, 62, 42, 255),
            new Color32(82, 126, 110, 255)
        };

        public static readonly string[] Expressions = new string[]
        {
            "😊 Tươi Cười Rạng Rỡ",
            "😎 Điềm Tĩnh Tự Tin",
            "😉 Dễ Thương Nháy Mắt",
            "🙂 Trầm Ấm Dịu Dàng",
            "🤩 Mắt Sao Long Lanh"
        };

        public static readonly string[] SkinToneNames = new string[]
        {
            "Trắng Hồng",
            "Tự Nhiên Ấm",
            "Rám Nắng Hè",
            "Ngăm Khỏe Khoắn"
        };

        public static readonly Color32[] SkinColors = new Color32[]
        {
            new Color32(255, 224, 206, 255),
            new Color32(245, 204, 176, 255),
            new Color32(226, 174, 140, 255),
            new Color32(188, 134, 102, 255)
        };

        public static readonly string[] ShirtStyles = new string[]
        {
            "Áo Thun Cổ Tròn Năng Động",
            "Áo Sơ Mi Thanh Lịch",
            "Áo Len Cổ Lọ Ấm Áp",
            "Áo Hoodie Thời Trang",
            "Áo Yếm Nông Trại",
            "Áo Khoác Jacket Denim"
        };

        public static readonly string[] ColorPaletteNames = new string[]
        {
            "Trắng Tinh Khôi",
            "Xanh Denim Biển",
            "Đỏ Ruby Ấm",
            "Xanh Lá Olive",
            "Vàng Mù Tạt",
            "Đen Than Sang Trọng"
        };

        public static readonly Color32[] OutfitColors = new Color32[]
        {
            new Color32(240, 242, 245, 255),
            new Color32(56, 112, 168, 255),
            new Color32(186, 52, 64, 255),
            new Color32(76, 138, 86, 255),
            new Color32(224, 168, 48, 255),
            new Color32(42, 45, 52, 255)
        };

        public static readonly string[] PantsStyles = new string[]
        {
            "Quần Jean Cổ Điển",
            "Quần Kaki Be Thanh Lịch",
            "Váy Dài Vintage / Quần Âu",
            "Quần Short Mùa Hè",
            "Quần Túi Hộp Bụi Bặm",
            "Quần Yếm Denim Liền"
        };

        public static readonly string[] PantsColorNames = new string[]
        {
            "Xanh Jean Cổ Điển",
            "Nâu Be Kaki",
            "Đen Than",
            "Trắng Kem",
            "Xanh Rêu Sẫm",
            "Xanh Navy"
        };

        public static readonly Color32[] PantsColors = new Color32[]
        {
            new Color32(48, 82, 136, 255),
            new Color32(188, 162, 132, 255),
            new Color32(38, 40, 48, 255),
            new Color32(232, 230, 222, 255),
            new Color32(66, 88, 68, 255),
            new Color32(32, 48, 88, 255)
        };

        public static readonly string[] Accessories = new string[]
        {
            "Không Phụ Kiện",
            "Kính Cận Tri Thức 👓",
            "Kính Râm Sành Điệu 🕶️",
            "Mũ Rơm Dạo Hè 👒",
            "Mũ Len Ấm Áp 🧶",
            "Mũ Lưỡi Trai Thể Thao 🧢",
            "Băng Đô Nơ Dễ Thương 🎀",
            "Khăn Quàng Len Ấm 🧣"
        };

        public static readonly string[] Shoes = new string[]
        {
            "Sneaker Trắng Năng Động 👟",
            "Giày Da Oxford Đen 👞",
            "Bốt Du Hành Cao Cổ 🥾",
            "Giày Búp Bê Dễ Thương 👡",
            "Sandal Mùa Hè 🩴"
        };

        private static readonly string[] RandomMaleNames = new string[]
        {
            "Bình An", "Minh Khang", "Gia Huy", "Tuấn Kiệt", "Quang Hải", "Hùng Dũng",
            "Thành Nam", "Bảo Long", "Việt Anh", "Đức Minh", "Hoàng Quân", "Khánh Duy"
        };

        private static readonly string[] RandomFemaleNames = new string[]
        {
            "Mai Chi", "Thùy Linh", "Lan Hương", "Khánh Vy", "Phương Thảo", "Bảo Ngọc",
            "Hà My", "Ánh Tuyết", "Ngọc Trâm", "Quỳnh Anh", "Diệu Linh", "Yến Nhi"
        };

        public string GetHairStyleName()
        {
            var arr = (gender == GenderType.Male) ? MaleHairStyles : FemaleHairStyles;
            int idx = Mathf.Clamp(hairStyleIndex, 0, arr.Length - 1);
            return arr[idx];
        }

        public void NextHairStyle(int delta = 1)
        {
            var arr = (gender == GenderType.Male) ? MaleHairStyles : FemaleHairStyles;
            hairStyleIndex = (hairStyleIndex + delta + arr.Length) % arr.Length;
        }

        public void NextHairColor(int delta = 1)
        {
            hairColorIndex = (hairColorIndex + delta + HairColorNames.Length) % HairColorNames.Length;
        }

        public void NextExpression(int delta = 1)
        {
            expressionIndex = (expressionIndex + delta + Expressions.Length) % Expressions.Length;
        }

        public void NextSkinTone(int delta = 1)
        {
            skinToneIndex = (skinToneIndex + delta + SkinToneNames.Length) % SkinToneNames.Length;
        }

        public void NextShirt(int delta = 1)
        {
            shirtIndex = (shirtIndex + delta + ShirtStyles.Length) % ShirtStyles.Length;
        }

        public void NextShirtColor(int delta = 1)
        {
            shirtColorIndex = (shirtColorIndex + delta + ColorPaletteNames.Length) % ColorPaletteNames.Length;
        }

        public void NextPants(int delta = 1)
        {
            pantsIndex = (pantsIndex + delta + PantsStyles.Length) % PantsStyles.Length;
        }

        public void NextPantsColor(int delta = 1)
        {
            pantsColorIndex = (pantsColorIndex + delta + PantsColorNames.Length) % PantsColorNames.Length;
        }

        public void NextAccessory(int delta = 1)
        {
            accessoryIndex = (accessoryIndex + delta + Accessories.Length) % Accessories.Length;
        }

        public void NextShoes(int delta = 1)
        {
            shoesIndex = (shoesIndex + delta + Shoes.Length) % Shoes.Length;
        }

        public void NextProfession(int delta = 1)
        {
            int cur = (int)starterProfession;
            cur = (cur + delta + 4) % 4;
            starterProfession = (ProfessionType)cur;
        }

        public void GenerateRandomName()
        {
            var names = (gender == GenderType.Male) ? RandomMaleNames : RandomFemaleNames;
            characterName = names[UnityEngine.Random.Range(0, names.Length)];
        }

        public void RandomizeAll()
        {
            gender = (UnityEngine.Random.value > 0.5f) ? GenderType.Male : GenderType.Female;
            GenerateRandomName();
            skinToneIndex = UnityEngine.Random.Range(0, SkinToneNames.Length);
            hairStyleIndex = UnityEngine.Random.Range(0, (gender == GenderType.Male ? MaleHairStyles : FemaleHairStyles).Length);
            hairColorIndex = UnityEngine.Random.Range(0, HairColorNames.Length);
            expressionIndex = UnityEngine.Random.Range(0, Expressions.Length);
            shirtIndex = UnityEngine.Random.Range(0, ShirtStyles.Length);
            shirtColorIndex = UnityEngine.Random.Range(0, ColorPaletteNames.Length);
            pantsIndex = UnityEngine.Random.Range(0, PantsStyles.Length);
            pantsColorIndex = UnityEngine.Random.Range(0, PantsColorNames.Length);
            accessoryIndex = UnityEngine.Random.Range(0, Accessories.Length);
            shoesIndex = UnityEngine.Random.Range(0, Shoes.Length);
            starterProfession = (ProfessionType)UnityEngine.Random.Range(0, 4);
        }

        public string GetAppearanceHash()
        {
            return $"{gender}_{skinToneIndex}_{hairStyleIndex}_{hairColorIndex}_{expressionIndex}_{shirtIndex}_{shirtColorIndex}_{pantsIndex}_{pantsColorIndex}_{accessoryIndex}_{shoesIndex}";
        }

        public void SaveToPrefs()
        {
            PlayerPrefs.SetString("CozyLife_CharName", characterName);
            PlayerPrefs.SetInt("CozyLife_Gender", (int)gender);
            PlayerPrefs.SetInt("CozyLife_SkinTone", skinToneIndex);
            PlayerPrefs.SetInt("CozyLife_HairStyle", hairStyleIndex);
            PlayerPrefs.SetInt("CozyLife_HairColor", hairColorIndex);
            PlayerPrefs.SetInt("CozyLife_Expression", expressionIndex);
            PlayerPrefs.SetInt("CozyLife_Shirt", shirtIndex);
            PlayerPrefs.SetInt("CozyLife_ShirtColor", shirtColorIndex);
            PlayerPrefs.SetInt("CozyLife_Pants", pantsIndex);
            PlayerPrefs.SetInt("CozyLife_PantsColor", pantsColorIndex);
            PlayerPrefs.SetInt("CozyLife_Accessory", accessoryIndex);
            PlayerPrefs.SetInt("CozyLife_Shoes", shoesIndex);
            PlayerPrefs.SetInt("CozyLife_Profession", (int)starterProfession);
            PlayerPrefs.Save();
        }

        public static CharacterAppearanceData LoadFromPrefs()
        {
            var data = new CharacterAppearanceData();
            if (PlayerPrefs.HasKey("CozyLife_CharName"))
            {
                data.characterName = PlayerPrefs.GetString("CozyLife_CharName", "Bình An");
                data.gender = (GenderType)PlayerPrefs.GetInt("CozyLife_Gender", 0);
                data.skinToneIndex = PlayerPrefs.GetInt("CozyLife_SkinTone", 1);
                data.hairStyleIndex = PlayerPrefs.GetInt("CozyLife_HairStyle", 0);
                data.hairColorIndex = PlayerPrefs.GetInt("CozyLife_HairColor", 0);
                data.expressionIndex = PlayerPrefs.GetInt("CozyLife_Expression", 0);
                data.shirtIndex = PlayerPrefs.GetInt("CozyLife_Shirt", 0);
                data.shirtColorIndex = PlayerPrefs.GetInt("CozyLife_ShirtColor", 0);
                data.pantsIndex = PlayerPrefs.GetInt("CozyLife_Pants", 0);
                data.pantsColorIndex = PlayerPrefs.GetInt("CozyLife_PantsColor", 0);
                data.accessoryIndex = PlayerPrefs.GetInt("CozyLife_Accessory", 0);
                data.shoesIndex = PlayerPrefs.GetInt("CozyLife_Shoes", 0);
                data.starterProfession = (ProfessionType)PlayerPrefs.GetInt("CozyLife_Profession", 0);
            }
            return data;
        }
    }
}
