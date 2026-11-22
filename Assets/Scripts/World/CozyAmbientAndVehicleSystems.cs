using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    /// <summary>
    /// Hệ thống Xe Đạp Dạo Phố Thị Trấn (Cozy Bicycle Travelling System).
    /// Người chơi có thể leo lên xe đạp dạo quanh thị trấn với tốc độ cao (+80%),
    /// nhấn [Space] bấm chuông xe kính coong và tận hưởng làn gió mát!
    /// </summary>
    public class CozyBicycleController : MonoBehaviour
    {
        public static CozyBicycleController Instance { get; private set; }

        private PlayerController player;
        private GameObject bikeVisualObj;
        private SpriteRenderer bikeSr;
        private bool isRiding = false;
        private float originalSpeed = 3.8f;
        private float bellCooldown = 0f;

        public bool IsRiding => isRiding;

        private void Awake()
        {
            Instance = this;
            player = GetComponent<PlayerController>();
        }

        public void ToggleBicycle()
        {
            if (isRiding)
            {
                DismountBicycle();
            }
            else
            {
                MountBicycle();
            }
        }

        public void MountBicycle()
        {
            if (isRiding) return;

            // Chỉ cho phép đạp xe ngoài trời
            if (CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.CurrentBuilding != BuildingType.None)
            {
                CozyLifeHUD.PushToast("⚠️ Không thể đạp xe trong nhà! Hãy ra ngoài thị trấn nhé.");
                return;
            }

            isRiding = true;
            EnsureBikeVisual();
            if (bikeVisualObj != null)
                bikeVisualObj.SetActive(true);

            CozyAudioManager.PlaySfx(CozySfxType.BicycleBell);
            CozyLifeHUD.PushToast("🚲 Đã lên Xe Đạp Dạo Phố! (+80% Tốc độ • Bấm [Space] bấm chuông • Bấm [B] để xuống xe)");

            // Ghi nhận thành tựu vào Sổ Tay Thị Trấn
            CozyDiarySystem.RecordBicycleRide();
        }

        public void DismountBicycle()
        {
            if (!isRiding) return;

            isRiding = false;
            if (bikeVisualObj != null)
                bikeVisualObj.SetActive(false);

            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("🚶 Đã xuống xe đạp đi bộ.");
        }

        private void EnsureBikeVisual()
        {
            if (bikeVisualObj == null)
            {
                bikeVisualObj = new GameObject("PlayerBicycleAttachment");
                bikeVisualObj.transform.SetParent(transform);
                bikeVisualObj.transform.localPosition = new Vector3(0f, -0.15f, 0f);

                bikeSr = bikeVisualObj.AddComponent<SpriteRenderer>();
                bikeSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_bicycle", new Vector2(0.5f, 0.45f), 115f);
                bikeSr.sortingOrder = 5;
            }
        }

        private void Update()
        {
            if (bellCooldown > 0f)
                bellCooldown -= Time.deltaTime;

            if (isRiding)
            {
                // Tự động xuống xe nếu người chơi bước vào trong nhà
                if (CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.CurrentBuilding != BuildingType.None)
                {
                    DismountBicycle();
                    return;
                }

                // Cập nhật hướng xoay của xe đạp theo nhân vật
                var playerSr = GetComponent<SpriteRenderer>();
                if (playerSr != null && bikeSr != null)
                {
                    bikeSr.flipX = playerSr.flipX;
                }

                // Nhấn phím Space để bấm chuông xe đạp
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame && bellCooldown <= 0f)
                {
                    bellCooldown = 0.5f;
                    CozyAudioManager.PlaySfx(CozySfxType.BicycleBell);
                    CozyLifeHUD.PushToast("🔔 Kính coong! Kính coong~");
                }
            }
        }
    }

    /// <summary>
    /// Tính năng tương tác cho các món nội thất cao cấp: Lò Sưởi, Đàn Piano, Máy Hát Đĩa Than, Bể Cá, Sofa.
    /// </summary>
    public class CozyLuxuryFurnitureInteractive : WorldInteractableBase
    {
        private ItemType furnitureType;
        private bool isGramophonePlaying = false;

        public void ConfigureLuxuryFurniture(ItemType type)
        {
            furnitureType = type;
            interactTitle = CozyLifeDatabase.GetItem(type)?.displayName ?? "Nội Thất Cao Cấp";

            if (furnitureType == ItemType.StoneFireplace)
            {
                // Tạo ánh lửa bập bùng ấm áp
                var glowObj = new GameObject("FireplaceWarmLight");
                glowObj.transform.SetParent(transform);
                glowObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                var glowSr = glowObj.AddComponent<SpriteRenderer>();
                glowSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_light_glow", new Vector2(0.5f, 0.5f), 80f);
                glowSr.color = new Color(1f, 0.55f, 0.20f, 0.65f);
                glowSr.sortingOrder = 1450;
            }
            else if (furnitureType == ItemType.FishAquarium)
            {
                // Ánh sáng xanh biển nhẹ nhàng
                var glowObj = new GameObject("AquariumCoolLight");
                glowObj.transform.SetParent(transform);
                glowObj.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                var glowSr = glowObj.AddComponent<SpriteRenderer>();
                glowSr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_light_glow", new Vector2(0.5f, 0.5f), 90f);
                glowSr.color = new Color(0.25f, 0.75f, 1f, 0.45f);
                glowSr.sortingOrder = 1450;
            }
        }

        public override string GetWorldOverheadLabel()
        {
            return furnitureType switch
            {
                ItemType.StoneFireplace => "🔥 Lò Sưởi [E]",
                ItemType.ClassicPiano => "🎹 Đàn Piano [E]",
                ItemType.VintageGramophone => isGramophonePlaying ? "📻 Máy Đĩa Than (Đang phát) [E]" : "📻 Máy Đĩa Than [E]",
                ItemType.FishAquarium => "🐠 Bể Cá Cảnh [E]",
                ItemType.LuxurySofa => "🛋️ Sofa Nhung [E]",
                _ => $"✨ {interactTitle} [E]"
            };
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return furnitureType switch
            {
                ItemType.StoneFireplace => "[E] Sưởi ấm bên ngọn lửa (+25 Thể lực, Giảm lạnh)",
                ItemType.ClassicPiano => "[E] Ngồi dạo một khúc nhạc Piano du dương",
                ItemType.VintageGramophone => isGramophonePlaying ? "[E] Tắt máy đĩa than" : "[E] Bật nhạc Lo-Fi đĩa than",
                ItemType.FishAquarium => "[E] Ngắm & Thả thức ăn cho cá vàng bơi lội",
                ItemType.LuxurySofa => "[E] Ngả lưng thư giãn trên Sofa (+20 Thể lực)",
                _ => $"[E] Tương tác với {interactTitle}"
            };
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            switch (furnitureType)
            {
                case ItemType.StoneFireplace:
                    stats.ModifyVitals(10f, 0f, 0f, 25f);
                    if (stats.IsSick)
                    {
                        stats.CureSickness("🔥 Hơi ấm lò sưởi đã xua tan cơn cảm lạnh!");
                    }
                    CozyAudioManager.PlaySfx(CozySfxType.Cure);
                    CozyLifeHUD.PushToast("🔥 Ngọn lửa sưởi ấm tâm hồn bạn (+25 Thể lực, Ấm áp dễ chịu)!");
                    break;

                case ItemType.ClassicPiano:
                    stats.ModifyVitals(0f, 0f, 0f, 20f);
                    CozyAudioManager.PlaySfx(CozySfxType.PianoTune);
                    CozyLifeHUD.PushToast("🎹 Những nốt nhạc du dương vang lên ngọt ngào khắp căn phòng~ 🎵");
                    break;

                case ItemType.VintageGramophone:
                    isGramophonePlaying = !isGramophonePlaying;
                    if (isGramophonePlaying)
                    {
                        CozyAudioManager.PlaySfx(CozySfxType.GramophoneMusic);
                        CozyLifeHUD.PushToast("📻 Máy đĩa than bắt đầu phát giai điệu Lo-Fi thị trấn dịu êm... ✨");
                    }
                    else
                    {
                        CozyAudioManager.PlaySfx(CozySfxType.UiClick);
                        CozyLifeHUD.PushToast("📻 Đã tắt máy đĩa than.");
                    }
                    break;

                case ItemType.FishAquarium:
                    CozyAudioManager.PlaySfx(CozySfxType.Splash);
                    CozyLifeHUD.PushToast("🐠 Bạn thả chút mồi cho đàn cá vàng tung tăng bơi lội dưới làn nước trong xanh!");
                    break;

                case ItemType.LuxurySofa:
                    stats.ModifyVitals(5f, 0f, 0f, 20f);
                    CozyAudioManager.PlaySfx(CozySfxType.EatDrink);
                    CozyLifeHUD.PushToast("🛋️ Ngả lưng trên sofa nệm nhung mềm mại, cảm giác thật thảnh thơi (+20 Thể lực)!");
                    break;
            }
        }
    }

    /// <summary>
    /// Hiệu ứng Môi Trường Tự Nhiên Sinh Động: Cánh hoa rơi ở công viên, Đom đóm đêm bay lượn quanh cây.
    /// </summary>
    public class CozyTownAmbientParticles : MonoBehaviour
    {
        private readonly List<Transform> fireflyNodes = new List<Transform>();
        private float animTimer = 0f;

        private void Start()
        {
            // Tạo 8 đốm đom đóm đêm quanh công viên và thị trấn
            for (int i = 0; i < 8; i++)
            {
                GameObject ff = new GameObject($"Firefly_{i + 1}");
                ff.transform.SetParent(transform);
                Vector3 origin = new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-2f, 4f), 0f);
                ff.transform.position = origin;

                var sr = ff.AddComponent<SpriteRenderer>();
                sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_light_glow", new Vector2(0.5f, 0.5f), 150f);
                sr.color = new Color(0.85f, 1f, 0.35f, 0.70f);
                sr.sortingOrder = 2500;
                ff.transform.localScale = new Vector3(0.20f, 0.20f, 1f);

                fireflyNodes.Add(ff.transform);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            animTimer += dt;

            bool isNight = CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.IsNightTime;

            for (int i = 0; i < fireflyNodes.Count; i++)
            {
                var f = fireflyNodes[i];
                if (f == null) continue;

                if (f.gameObject.activeSelf != isNight)
                    f.gameObject.SetActive(isNight);

                if (isNight)
                {
                    float angle = animTimer * 1.2f + i * 1.5f;
                    float ox = Mathf.Cos(angle) * 0.45f;
                    float oy = Mathf.Sin(angle * 1.4f) * 0.30f;
                    f.localPosition = new Vector3(ox + (i % 4 - 1.5f) * 2f, oy + (i / 4 - 0.5f) * 2f, 0f);

                    var sr = f.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        float alpha = 0.45f + Mathf.Sin(animTimer * 4f + i) * 0.35f;
                        sr.color = new Color(0.85f, 1f, 0.35f, alpha);
                    }
                }
            }
        }
    }
}
