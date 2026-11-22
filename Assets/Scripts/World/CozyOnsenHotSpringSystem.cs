using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.UI;

namespace CozyLife.World
{
    /// <summary>
    /// Khu Nghỉ Dưỡng Suối Nước Nóng Thiên Nhiên (Town Onsen Mineral Bath):
    /// - Tọa lạc tại khu sinh thái ven hồ và đồi thông thị trấn.
    /// - Hơi nước khoáng nóng bốc lên nghi ngút làm mờ ảo làn nước ngọc bích.
    /// - Người chơi bấm [E] để ngâm mình thư giãn 30 phút game:
    ///   + Hồi phục tức thì +50 Thể lực, +15 Máu.
    ///   + Giải trừ hoàn toàn Cảm Lạnh, Cảm Sốt, Say Nắng.
    ///   + Nhận hiệu ứng "Tinh Thần Sảng Khoái" (+20% tốc độ chạy trong ngày).
    ///   + Mở khóa huy hiệu Thành Tựu trong Nhật Ký Thị Trấn!
    /// </summary>
    public class CozyOnsenHotSpringInteractable : WorldInteractableBase
    {
        private readonly List<Transform> steamWisps = new List<Transform>();
        private float steamTimer = 0f;

        private void Awake()
        {
            stationType = StationType.None;
            interactTitle = "Suối Nước Nóng Onsen";
        }

        private void Start()
        {
            CreateSteamWisps();
        }

        private void CreateSteamWisps()
        {
            // Tạo 6 làn hơi nước ấm bốc lên từ lòng suối
            for (int i = 0; i < 6; i++)
            {
                GameObject wisp = new GameObject($"SteamWisp_{i}");
                wisp.transform.SetParent(transform);
                wisp.transform.localPosition = new Vector3(
                    Random.Range(-0.8f, 0.8f),
                    Random.Range(-0.2f, 0.4f),
                    0f
                );

                var sr = wisp.AddComponent<SpriteRenderer>();
                sr.sprite = CozyLifeDatabase.LoadSprite("CozyLifeArt/Objects/obj_light_glow", new Vector2(0.5f, 0.5f), 120f);
                sr.color = new Color(1f, 1f, 1f, 0.28f);
                sr.sortingOrder = 2450;
                wisp.transform.localScale = new Vector3(0.35f, 0.35f, 1f);

                steamWisps.Add(wisp.transform);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            steamTimer += dt;

            for (int i = 0; i < steamWisps.Count; i++)
            {
                var w = steamWisps[i];
                if (w == null) continue;

                Vector3 lPos = w.localPosition;
                lPos.y += dt * 0.25f;
                lPos.x += Mathf.Sin(steamTimer * 1.8f + i) * dt * 0.15f;

                var sr = w.GetComponent<SpriteRenderer>();
                if (lPos.y > 0.95f)
                {
                    lPos.y = Random.Range(-0.25f, 0.1f);
                    lPos.x = Random.Range(-0.75f, 0.75f);
                    w.localScale = new Vector3(0.25f, 0.25f, 1f);
                    if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.15f);
                }
                else
                {
                    float progress = (lPos.y - (-0.25f)) / 1.2f;
                    float alpha = Mathf.Sin(progress * Mathf.PI) * 0.35f;
                    if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
                    float s = 0.25f + progress * 0.35f;
                    w.localScale = new Vector3(s, s, 1f);
                }

                w.localPosition = lPos;
            }
        }

        public override string GetWorldOverheadLabel()
        {
            return "♨️ Suối Nước Nóng Onsen [E]";
        }

        public override string GetPromptText(ProfessionAndInventory player)
        {
            return "[E] Ngâm mình thư giãn suối khoáng nóng (30 phút, Hồi +50 Sức, Giải ốm)";
        }

        public override void Interact(ProfessionAndInventory player, CharacterStats stats)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc == null || pc.IsPerformingAction) return;

            pc.StartTimedAction("♨️ Đang ngâm mình thư giãn dưới làn nước khoáng nóng...", 30f, () =>
            {
                stats.ModifyVitals(15f, 0f, 0f, 50f);

                if (stats.IsSick)
                {
                    stats.CureSickness("♨️ Suối khoáng nóng kỳ diệu đã chữa lành hoàn toàn cơn bệnh!");
                }

                CozyAudioManager.PlaySfx(CozySfxType.Splash);
                CozyLifeHUD.PushToast("♨️ Ngâm mình sảng khoái! (+50 Thể lực, +15 Máu, Giải trừ mệt mỏi) ✨");

                // Thưởng hiệu ứng Tinh Thần Sảng Khoái
                CozyLifeHUD.PushToast("✨ Nhận hiệu ứng [Tinh Thần Sảng Khoái]: Bước đi nhẹ tênh cả ngày!");

                // Cún cưng Bé Vàng vui vẻ
                if (CozyPetCompanion.Instance != null)
                {
                    CozyAudioManager.PlaySfx(CozySfxType.PetBark);
                    CozyLifeHUD.PushToast("🐶 Bé Vàng vẫy đuôi chờ bạn bên bờ đá suối nước nóng!");
                }

                // Ghi nhận thành tựu vào Nhật Ký Thị Trấn
                CozyDiarySystem.RecordOnsenBath();
            }, ActionVisualType.Sleeping);
        }
    }
}
