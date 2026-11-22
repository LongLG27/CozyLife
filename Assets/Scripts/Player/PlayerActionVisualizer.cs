using System;
using System.Collections.Generic;
using UnityEngine;
using CozyLife.Core;
using CozyLife.World;

namespace CozyLife.Player
{
    public enum ActionVisualType
    {
        None = 0,
        Sleeping,        // Nằm ngủ trên giường + ngáy Zzz... (hiển thị rõ trên nệm gối)
        EatingBread,     // Gặm bánh mì giòn cầm tay + vụn rơi + *nhai nhai*
        DrinkingWater,   // Uống ly nước mát có ống hút / trà + *ực ực*
        EatingSoup,      // Bát súp kem nóng bốc khói + *xì xụp*
        Cooking,         // Xốc chảo nấu ăn + khói bốc + *xèo xèo!*
        CarpentrySaw,    // Kéo cưa gỗ thợ mộc + mạt cưa vàng + *kèn kẹt!*
        Cashier,         // Thu ngân tính tiền + xu vàng nảy + *kaching!*
        WateringPlants,  // Nông dân tưới nước luống rau (bình tưới) + *rào rào...*
        DoctorHealing,   // Khám bệnh & sơ cứu y tế (hộp cứu thương) + *chăm sóc...*
        ReadingBook      // Đọc sách thư giãn (sách mở bìa da) + *sột soạt...*
    }

    /// <summary>
    /// Quản lý toàn bộ hoạt ảnh, đạo cụ cầm tay (handheld pixel art props),
    /// hiệu ứng hạt chữ (floating bezier vfx) và âm thanh đa dạng cho các hành động trong game.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerActionVisualizer : MonoBehaviour
    {
        private PlayerController playerController;
        private SpriteRenderer playerSr;
        private IsometricSorting isoSorting;
        private Camera mainCam;

        // Child GameObject chứa sprite đạo cụ cầm trên tay
        private GameObject propHolderObj;
        private SpriteRenderer propSr;

        // Trạng thái hành động hiện tại
        private ActionVisualType currentVisual = ActionVisualType.None;
        private Vector3 savedStandPosition;
        private Vector3? activeBedPos = null;
        private int forcedBedSortingOrder = 0;
        private float actionTimer = 0f;
        private float sfxRepeatTimer = 0f;

        // Hạt bay hiệu ứng (Zzz, khói, mạt cưa, đồng xu, vụn bánh, giọt nước, tia sáng)
        private struct FloatingVfx
        {
            public Vector3 worldOrigin;
            public float elapsed;
            public float maxLife;
            public string text;
            public Color color;
            public float fontSize;
            public float driftAngle;
            public float speed;
        }

        private readonly List<FloatingVfx> activeVfx = new List<FloatingVfx>();
        private float vfxSpawnTimer = 0f;

        // GUI Styles
        private GUIStyle vfxTextStyle;
        private bool stylesReady = false;

        public ActionVisualType CurrentVisual => currentVisual;
        public bool IsSleeping => currentVisual == ActionVisualType.Sleeping;

        public static ActionVisualType DetectVisualType(string actionTitle)
        {
            if (string.IsNullOrEmpty(actionTitle))
                return ActionVisualType.None;

            string lower = actionTitle.ToLowerInvariant();
            if (lower.Contains("ngủ") || lower.Contains("nghỉ ngơi") || lower.Contains("giường"))
                return ActionVisualType.Sleeping;
            if (lower.Contains("súp") || lower.Contains("canh"))
                return ActionVisualType.EatingSoup;
            if (lower.Contains("uống") || lower.Contains("nước") || lower.Contains("trà"))
                return ActionVisualType.DrinkingWater;
            if (lower.Contains("bánh mì") || lower.Contains("ăn bánh") || lower.Contains("gặm") || lower.Contains("thưởng thức") || lower.Contains("món ăn") || lower.Contains("dùng bữa") || lower.Contains("combo") || lower.Contains("đại tiệc"))
                return ActionVisualType.EatingBread;
            if (lower.Contains("bếp") || lower.Contains("nấu") || lower.Contains("nướng") || lower.Contains("chiên") || lower.Contains("xào") || lower.Contains("phụ bếp"))
                return ActionVisualType.Cooking;
            if (lower.Contains("xẻ") || lower.Contains("cưa") || lower.Contains("gỗ") || lower.Contains("ghế") || lower.Contains("đóng") || lower.Contains("bàn cưa"))
                return ActionVisualType.CarpentrySaw;
            if (lower.Contains("bán") || lower.Contains("mua") || lower.Contains("chợ") || lower.Contains("thu ngân") || lower.Contains("tiền"))
                return ActionVisualType.Cashier;
            if (lower.Contains("tưới") || lower.Contains("gieo hạt") || lower.Contains("chăm bón") || lower.Contains("thu hoạch") || lower.Contains("trồng") || lower.Contains("ruộng"))
                return ActionVisualType.WateringPlants;
            if (lower.Contains("sơ cứu") || lower.Contains("khám") || lower.Contains("chữa bệnh") || lower.Contains("băng bó") || lower.Contains("thuốc") || lower.Contains("y tế") || lower.Contains("medkit") || lower.Contains("điều trị"))
                return ActionVisualType.DoctorHealing;
            if (lower.Contains("đọc") || lower.Contains("sách") || lower.Contains("kệ sách") || lower.Contains("nghiên cứu"))
                return ActionVisualType.ReadingBook;

            return ActionVisualType.None;
        }

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerSr = GetComponent<SpriteRenderer>();
            isoSorting = GetComponent<IsometricSorting>();

            EnsurePropRenderer();
        }

        private void Start()
        {
            mainCam = Camera.main;
        }

        private void EnsurePropRenderer()
        {
            if (propHolderObj == null)
            {
                propHolderObj = new GameObject("PlayerActionPropHolder");
                propHolderObj.transform.SetParent(transform);
                propHolderObj.transform.localPosition = new Vector3(0.18f, 0.28f, 0f);
                propHolderObj.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

                propSr = propHolderObj.AddComponent<SpriteRenderer>();
                propSr.sortingOrder = 20;
            }
            propHolderObj.SetActive(false);
        }

        public void BeginAction(string actionTitle, ActionVisualType forcedType = ActionVisualType.None, Vector3? targetBedPos = null)
        {
            StopCurrentActionVisuals(false);

            currentVisual = (forcedType != ActionVisualType.None)
                ? forcedType
                : DetectVisualType(actionTitle);

            actionTimer = 0f;
            sfxRepeatTimer = 0.12f;
            vfxSpawnTimer = 0f;
            activeVfx.Clear();
            activeBedPos = targetBedPos;

            EnsurePropRenderer();

            if (currentVisual == ActionVisualType.Sleeping)
            {
                savedStandPosition = transform.position;

                // Tạm thời vô hiệu hóa IsometricSorting để người không bị tụt xuống dưới chiếc giường
                if (isoSorting != null)
                {
                    isoSorting.enabled = false;
                }

                if (targetBedPos.HasValue)
                {
                    // Chiếc giường có pivot ở chân giường (0.5, 0.22).
                    // Khi nhân vật xoay 80 độ quanh gốc chân, đầu sẽ nằm gọn gàng trên 2 chiếc gối trắng!
                    transform.position = targetBedPos.Value + new Vector3(0.18f, 0.28f, 0f);
                    forcedBedSortingOrder = Mathf.RoundToInt(-targetBedPos.Value.y * 100f) + 12;
                }
                else
                {
                    forcedBedSortingOrder = Mathf.RoundToInt(-transform.position.y * 100f) + 12;
                }

                if (playerSr != null)
                {
                    playerSr.sortingOrder = forcedBedSortingOrder;
                }

                transform.localRotation = Quaternion.Euler(0f, 0f, 80f);
                propHolderObj.SetActive(false);
                CozyAudioManager.PlaySfx(CozySfxType.Snore, 0.70f);
            }
            else
            {
                SetupPropVisuals();
            }
        }

        public void StopCurrentActionVisuals(bool resetPositionIfSleeping = true)
        {
            if (currentVisual == ActionVisualType.Sleeping)
            {
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;

                if (resetPositionIfSleeping)
                {
                    Vector3 exitPos = activeBedPos.HasValue
                        ? activeBedPos.Value + new Vector3(0f, -0.42f, 0f)
                        : (savedStandPosition != Vector3.zero ? savedStandPosition : transform.position);
                    transform.position = exitPos;
                }

                // Kích hoạt lại IsometricSorting khi thức dậy
                if (isoSorting != null)
                {
                    isoSorting.enabled = true;
                    isoSorting.UpdateSorting();
                }
            }

            currentVisual = ActionVisualType.None;
            activeBedPos = null;

            if (propHolderObj != null)
                propHolderObj.SetActive(false);

            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void SetupPropVisuals()
        {
            if (propHolderObj == null || propSr == null)
                EnsurePropRenderer();

            string propSpritePath = "";
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float ppu = 100f;
            Vector3 propScale = new Vector3(0.75f, 0.75f, 1f);

            switch (currentVisual)
            {
                case ActionVisualType.EatingBread:
                    propSpritePath = "CozyLifeArt/Objects/prop_hand_bread";
                    ppu = 100f;
                    propScale = new Vector3(0.80f, 0.80f, 1f);
                    break;

                case ActionVisualType.DrinkingWater:
                    propSpritePath = "CozyLifeArt/Objects/prop_hand_cup";
                    ppu = 100f;
                    propScale = new Vector3(0.80f, 0.80f, 1f);
                    break;

                case ActionVisualType.EatingSoup:
                    propSpritePath = "CozyLifeArt/Objects/prop_hand_soup";
                    ppu = 100f;
                    propScale = new Vector3(0.78f, 0.78f, 1f);
                    break;

                case ActionVisualType.Cooking:
                    propSpritePath = "CozyLifeArt/Objects/prop_frying_pan";
                    ppu = 88f;
                    propScale = new Vector3(0.85f, 0.85f, 1f);
                    break;

                case ActionVisualType.CarpentrySaw:
                    propSpritePath = "CozyLifeArt/Objects/prop_hand_saw";
                    ppu = 88f;
                    propScale = new Vector3(0.85f, 0.85f, 1f);
                    break;

                case ActionVisualType.Cashier:
                    propSpritePath = "CozyLifeArt/Objects/prop_cash_register";
                    ppu = 88f;
                    propScale = new Vector3(0.82f, 0.82f, 1f);
                    break;

                case ActionVisualType.WateringPlants:
                    propSpritePath = "CozyLifeArt/Objects/prop_watering_can";
                    ppu = 95f;
                    propScale = new Vector3(0.80f, 0.80f, 1f);
                    break;

                case ActionVisualType.DoctorHealing:
                    propSpritePath = "CozyLifeArt/Objects/prop_medkit_held";
                    ppu = 95f;
                    propScale = new Vector3(0.80f, 0.80f, 1f);
                    break;

                case ActionVisualType.ReadingBook:
                    propSpritePath = "CozyLifeArt/Objects/prop_reading_book";
                    ppu = 95f;
                    propScale = new Vector3(0.82f, 0.82f, 1f);
                    break;
            }

            if (!string.IsNullOrEmpty(propSpritePath))
            {
                Sprite sp = CozyLifeDatabase.LoadSprite(propSpritePath, pivot, ppu);
                propSr.sprite = sp;
                propSr.transform.localScale = propScale;
                propHolderObj.SetActive(true);
            }
            else
            {
                propHolderObj.SetActive(false);
            }
        }

        private void Update()
        {
            if (playerController == null || !playerController.IsPerformingAction)
            {
                if (currentVisual != ActionVisualType.None)
                {
                    StopCurrentActionVisuals(true);
                }
                UpdateFloatingVfx(Time.deltaTime);
                return;
            }

            float dt = Time.deltaTime;
            actionTimer += dt;
            sfxRepeatTimer -= dt;
            vfxSpawnTimer -= dt;

            // Đảm bảo sortingOrder của nhân vật khi ngủ luôn ở trên giường
            if (currentVisual == ActionVisualType.Sleeping && playerSr != null)
            {
                playerSr.sortingOrder = forcedBedSortingOrder;
            }

            // Đảm bảo đạo cụ luôn hiển thị phía trước thân áo nhân vật
            if (propSr != null && playerSr != null && currentVisual != ActionVisualType.Sleeping)
            {
                propSr.sortingOrder = playerSr.sortingOrder + 2;
            }

            // Đồng bộ hướng lật mặt của đạo cụ theo sprite nhân vật
            bool isFlipped = (playerSr != null && playerSr.flipX);
            float sideSign = isFlipped ? -1f : 1f;

            UpdateVisualMotion(dt, isFlipped, sideSign);
            UpdateAudioTriggers();
            UpdateVfxSpawns(sideSign);
            UpdateFloatingVfx(dt);
        }

        private void UpdateVisualMotion(float dt, bool isFlipped, float sideSign)
        {
            switch (currentVisual)
            {
                case ActionVisualType.Sleeping:
                    // Nhịp thở êm ái khi ngủ
                    float breath = Mathf.Sin(actionTimer * 2.8f) * 0.045f;
                    transform.localScale = new Vector3(1f, 1f + breath, 1f);
                    break;

                case ActionVisualType.EatingBread:
                    // Đưa bánh mì lên miệng gặm rồi hạ xuống nhai
                    float eatCycle = Mathf.PingPong(actionTimer * 3.5f, 1f);
                    float eatLiftY = Mathf.Lerp(0.26f, 0.38f, eatCycle);
                    float eatRot = Mathf.Lerp(isFlipped ? 12f : -12f, isFlipped ? -20f : 20f, eatCycle);

                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.16f, eatLiftY, 0f);
                    propHolderObj.transform.localRotation = Quaternion.Euler(0f, 0f, eatRot);
                    propSr.flipX = isFlipped;

                    // Đầu gật nhẹ theo nhịp nhai giòn rụm
                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(actionTimer * 7.0f) * 2.2f);
                    break;

                case ActionVisualType.DrinkingWater:
                    // Cầm ly nước đưa lên miệng uống nghiêng góc 25 độ
                    float drinkCycle = Mathf.PingPong(actionTimer * 3.0f, 1f);
                    float drinkY = Mathf.Lerp(0.26f, 0.39f, drinkCycle);
                    float drinkTilt = Mathf.Lerp(0f, isFlipped ? -28f : 28f, drinkCycle);

                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.15f, drinkY, 0f);
                    propHolderObj.transform.localRotation = Quaternion.Euler(0f, 0f, drinkTilt);
                    propSr.flipX = isFlipped;

                    // Đầu hơi ngửa ra sau khi uống
                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, isFlipped ? 4f : -4f, drinkCycle));
                    break;

                case ActionVisualType.EatingSoup:
                    // Bát súp bốc khói cầm bằng hai tay trước ngực
                    float soupBob = Mathf.Sin(actionTimer * 4.0f) * 0.015f;
                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.14f, 0.27f + soupBob, 0f);
                    propHolderObj.transform.localRotation = Quaternion.identity;
                    propSr.flipX = isFlipped;

                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(actionTimer * 5.0f) * 1.8f);
                    break;

                case ActionVisualType.Cooking:
                    // Động tác xốc chảo nấu nướng: Chảo hất bổng thức ăn lên rồi đỡ lại
                    float panCycle = (actionTimer * 2.2f) % 1.0f;
                    float panY = (panCycle < 0.35f)
                        ? Mathf.Sin(panCycle / 0.35f * Mathf.PI) * 0.12f
                        : 0f;
                    float panTilt = (panCycle < 0.35f) ? (isFlipped ? -20f : 20f) : 0f;

                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.22f, 0.22f + panY, 0f);
                    propHolderObj.transform.localRotation = Quaternion.Euler(0f, 0f, panTilt);
                    propSr.flipX = isFlipped;

                    // Nhún người nhiệt tình khi nấu ăn
                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(actionTimer * 4.5f) * 2.6f);
                    break;

                case ActionVisualType.CarpentrySaw:
                    // Động tác kéo cưa qua lại nhịp nhàng (kéo cưa lừa xẻ)
                    float sawStroke = Mathf.Sin(actionTimer * 9.0f);
                    float sawX = sideSign * (0.20f + sawStroke * 0.10f);
                    float sawY = 0.20f - sawStroke * 0.03f;

                    propHolderObj.transform.localPosition = new Vector3(sawX, sawY, 0f);
                    propHolderObj.transform.localRotation = Quaternion.Euler(0f, 0f, isFlipped ? -35f : 35f);
                    propSr.flipX = isFlipped;

                    // Nghiêng người theo nhịp cưa
                    transform.localRotation = Quaternion.Euler(0f, 0f, sawStroke * 3.5f);
                    break;

                case ActionVisualType.Cashier:
                    // Máy tính tiền trước ngực, tay gõ cạch cạch
                    float tap = Mathf.PingPong(actionTimer * 7f, 1f) * 0.02f;
                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.18f, 0.22f + tap, 0f);
                    propHolderObj.transform.localRotation = Quaternion.identity;
                    propSr.flipX = isFlipped;
                    break;

                case ActionVisualType.WateringPlants:
                    // Nghiêng bình tưới 32 độ tưới nước xuống đất
                    float waterSway = Mathf.Sin(actionTimer * 3.5f) * 0.02f;
                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.18f, 0.22f + waterSway, 0f);
                    propHolderObj.transform.localRotation = Quaternion.Euler(0f, 0f, isFlipped ? -32f : 32f);
                    propSr.flipX = isFlipped;

                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(actionTimer * 3.5f) * 2.0f);
                    break;

                case ActionVisualType.DoctorHealing:
                    // Hộp cứu thương nâng trước ngực, nhịp hồi phục dịu dàng
                    float medPulse = Mathf.Sin(actionTimer * 3.0f) * 0.02f;
                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.14f, 0.25f + medPulse, 0f);
                    propHolderObj.transform.localRotation = Quaternion.identity;
                    propSr.flipX = isFlipped;
                    break;

                case ActionVisualType.ReadingBook:
                    // Cuốn sách mở cầm đọc chăm chú
                    float bookBob = Mathf.Sin(actionTimer * 2.5f) * 0.015f;
                    propHolderObj.transform.localPosition = new Vector3(sideSign * 0.14f, 0.26f + bookBob, 0f);
                    propHolderObj.transform.localRotation = Quaternion.Euler(0f, 0f, isFlipped ? 8f : -8f);
                    propSr.flipX = isFlipped;
                    break;
            }
        }

        private void UpdateAudioTriggers()
        {
            if (sfxRepeatTimer > 0f) return;

            switch (currentVisual)
            {
                case ActionVisualType.Sleeping:
                    CozyAudioManager.PlaySfx(CozySfxType.Snore, 0.65f);
                    sfxRepeatTimer = 2.6f;
                    break;

                case ActionVisualType.EatingBread:
                case ActionVisualType.EatingSoup:
                    CozyAudioManager.PlaySfx(CozySfxType.EatDrink, 0.75f);
                    sfxRepeatTimer = 0.95f;
                    break;

                case ActionVisualType.DrinkingWater:
                    CozyAudioManager.PlaySfx(CozySfxType.EatDrink, 0.70f);
                    sfxRepeatTimer = 0.85f;
                    break;

                case ActionVisualType.Cooking:
                    CozyAudioManager.PlaySfx(CozySfxType.Sizzle, 0.75f);
                    sfxRepeatTimer = 0.75f;
                    break;

                case ActionVisualType.CarpentrySaw:
                    CozyAudioManager.PlaySfx(CozySfxType.SawWood, 0.70f);
                    sfxRepeatTimer = 0.60f;
                    break;

                case ActionVisualType.Cashier:
                    CozyAudioManager.PlaySfx(CozySfxType.CashRegister, 0.75f);
                    sfxRepeatTimer = 0.85f;
                    break;

                case ActionVisualType.WateringPlants:
                    CozyAudioManager.PlaySfx(CozySfxType.Watering, 0.70f);
                    sfxRepeatTimer = 0.55f;
                    break;

                case ActionVisualType.DoctorHealing:
                    CozyAudioManager.PlaySfx(CozySfxType.DoctorHeal, 0.75f);
                    sfxRepeatTimer = 0.90f;
                    break;

                case ActionVisualType.ReadingBook:
                    CozyAudioManager.PlaySfx(CozySfxType.PageTurn, 0.65f);
                    sfxRepeatTimer = 1.8f;
                    break;
            }
        }

        private void UpdateVfxSpawns(float sideSign)
        {
            if (vfxSpawnTimer > 0f) return;

            Vector3 headPos = transform.position + new Vector3(0f, 0.55f, 0f);
            Vector3 handPos = transform.position + new Vector3(sideSign * 0.20f, 0.28f, 0f);

            switch (currentVisual)
            {
                case ActionVisualType.Sleeping:
                    // Khi nằm ngủ, đầu tựa lên gối bên trái chiếc giường
                    Vector3 sleepHeadPos = transform.position + new Vector3(-0.45f, 0.10f, 0f);
                    string[] zTexts = { "Z", "z", "Zz", "Zzz..." };
                    string zChoice = zTexts[UnityEngine.Random.Range(0, zTexts.Length)];
                    SpawnFloatingVfx(sleepHeadPos + new Vector3(UnityEngine.Random.Range(-0.06f, 0.06f), 0.08f, 0f),
                        zChoice, new Color(0.70f, 0.88f, 1.0f, 0.95f), UnityEngine.Random.Range(14f, 22f), 1.6f, 0.35f);
                    vfxSpawnTimer = 0.65f;
                    break;

                case ActionVisualType.EatingBread:
                    // Vụn bánh mì rơi lả tả & chữ nhai nhai
                    string[] breadVfx = { "🍞", "•", "*nhai nhai*", "•" };
                    string bChoice = breadVfx[UnityEngine.Random.Range(0, breadVfx.Length)];
                    Color bCol = (bChoice == "*nhai nhai*") ? new Color(1f, 0.90f, 0.55f) : new Color(0.92f, 0.65f, 0.35f);
                    SpawnFloatingVfx(handPos, bChoice, bCol, 12f, 0.9f, 0.28f);
                    vfxSpawnTimer = 0.45f;
                    break;

                case ActionVisualType.DrinkingWater:
                    // Bong bóng nước / giọt nước & chữ ực ực
                    string[] waterVfx = { "💧", "🫧", "*ực ực*", "🫧" };
                    string wChoice = waterVfx[UnityEngine.Random.Range(0, waterVfx.Length)];
                    Color wCol = new Color(0.45f, 0.85f, 1.0f);
                    SpawnFloatingVfx(handPos, wChoice, wCol, 12f, 0.85f, 0.30f);
                    vfxSpawnTimer = 0.45f;
                    break;

                case ActionVisualType.EatingSoup:
                    // Làn khói nghi ngút bốc lên từ bát súp
                    string[] soupVfx = { "♨️", "~", "*xì xụp*", "~" };
                    string sChoice = soupVfx[UnityEngine.Random.Range(0, soupVfx.Length)];
                    Color sCol = new Color(1f, 0.82f, 0.65f);
                    SpawnFloatingVfx(handPos, sChoice, sCol, 13f, 1.1f, 0.32f);
                    vfxSpawnTimer = 0.40f;
                    break;

                case ActionVisualType.Cooking:
                    // Khói chảo cuộn tròn + xèo xèo
                    string[] cookVfx = { "♨️", "🍳", "*xèo xèo!*", "✨" };
                    string cChoice = cookVfx[UnityEngine.Random.Range(0, cookVfx.Length)];
                    Color cCol = (cChoice == "*xèo xèo!*") ? new Color(1f, 0.65f, 0.25f) : new Color(1f, 0.92f, 0.70f);
                    SpawnFloatingVfx(handPos + new Vector3(0f, 0.12f, 0f), cChoice, cCol, 13f, 1.0f, 0.42f);
                    vfxSpawnTimer = 0.35f;
                    break;

                case ActionVisualType.CarpentrySaw:
                    // Mạt cưa vàng óng văng ra
                    string[] sawVfx = { "🪵", "✦", "*kèn kẹt!*", "•", "✦" };
                    string swChoice = sawVfx[UnityEngine.Random.Range(0, sawVfx.Length)];
                    Color swCol = (swChoice == "*kèn kẹt!*") ? new Color(1f, 0.85f, 0.45f) : new Color(0.95f, 0.75f, 0.35f);
                    SpawnFloatingVfx(handPos + new Vector3(sideSign * 0.1f, -0.05f, 0f), swChoice, swCol, 12f, 0.85f, 0.38f);
                    vfxSpawnTimer = 0.32f;
                    break;

                case ActionVisualType.Cashier:
                    // Đồng xu vàng nảy lên leng keng
                    string[] cashVfx = { "💰", "🪙", "*kaching!*", "✨" };
                    string csChoice = cashVfx[UnityEngine.Random.Range(0, cashVfx.Length)];
                    Color csCol = new Color(1f, 0.90f, 0.30f);
                    SpawnFloatingVfx(handPos + new Vector3(0f, 0.15f, 0f), csChoice, csCol, 14f, 1.1f, 0.45f);
                    vfxSpawnTimer = 0.38f;
                    break;

                case ActionVisualType.WateringPlants:
                    // Giọt nước tưới tuôn rơi rào rào
                    string[] waterCanVfx = { "💧", "💦", "*rào rào...*", "🌱", "💧" };
                    string wcChoice = waterCanVfx[UnityEngine.Random.Range(0, waterCanVfx.Length)];
                    Color wcCol = new Color(0.40f, 0.85f, 1.0f);
                    SpawnFloatingVfx(handPos + new Vector3(sideSign * 0.15f, -0.05f, 0f), wcChoice, wcCol, 13f, 0.9f, 0.32f);
                    vfxSpawnTimer = 0.35f;
                    break;

                case ActionVisualType.DoctorHealing:
                    // Trái tim & chữ thập đỏ bay lên hồi phục
                    string[] docVfx = { "✚", "💖", "*chăm sóc...*", "✨", "✚" };
                    string dChoice = docVfx[UnityEngine.Random.Range(0, docVfx.Length)];
                    Color dCol = (dChoice == "💖") ? new Color(1f, 0.45f, 0.65f) : ((dChoice == "✚") ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.90f, 0.50f));
                    SpawnFloatingVfx(handPos, dChoice, dCol, 13f, 1.2f, 0.38f);
                    vfxSpawnTimer = 0.40f;
                    break;

                case ActionVisualType.ReadingBook:
                    // Tia sáng tri thức & lật trang sách
                    string[] bookVfx = { "📖", "✨", "*sột soạt...*", "💡", "✨" };
                    string bkChoice = bookVfx[UnityEngine.Random.Range(0, bookVfx.Length)];
                    Color bkCol = new Color(1f, 0.95f, 0.60f);
                    SpawnFloatingVfx(handPos + new Vector3(0f, 0.10f, 0f), bkChoice, bkCol, 13f, 1.1f, 0.30f);
                    vfxSpawnTimer = 0.55f;
                    break;
            }
        }

        private void SpawnFloatingVfx(Vector3 origin, string text, Color color, float fontSize, float maxLife, float speed)
        {
            activeVfx.Add(new FloatingVfx
            {
                worldOrigin = origin,
                elapsed = 0f,
                maxLife = maxLife,
                text = text,
                color = color,
                fontSize = fontSize,
                driftAngle = UnityEngine.Random.Range(-0.35f, 0.35f),
                speed = speed
            });
        }

        private void UpdateFloatingVfx(float dt)
        {
            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                var v = activeVfx[i];
                v.elapsed += dt;
                if (v.elapsed >= v.maxLife)
                {
                    activeVfx.RemoveAt(i);
                }
                else
                {
                    activeVfx[i] = v;
                }
            }
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;

            vfxTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            stylesReady = true;
        }

        private void OnGUI()
        {
            if (activeVfx.Count == 0 && currentVisual == ActionVisualType.None)
                return;

            if (mainCam == null)
                mainCam = Camera.main;
            if (mainCam == null)
                return;

            EnsureStyles();

            // Vẽ các hạt hiệu ứng bay bổng sinh động trên màn hình
            for (int i = 0; i < activeVfx.Count; i++)
            {
                var v = activeVfx[i];
                float t = Mathf.Clamp01(v.elapsed / v.maxLife);
                float curY = v.worldOrigin.y + v.speed * v.elapsed;
                float curX = v.worldOrigin.x + Mathf.Sin(v.elapsed * 4.5f + v.driftAngle) * 0.12f;

                Vector3 screenPoint = mainCam.WorldToScreenPoint(new Vector3(curX, curY, v.worldOrigin.z));
                if (screenPoint.z <= 0f) continue;

                float guiX = screenPoint.x;
                float guiY = Screen.height - screenPoint.y;

                float alpha = Mathf.Sin(t * Mathf.PI);
                Color drawCol = new Color(v.color.r, v.color.g, v.color.b, v.color.a * alpha);

                int size = Mathf.RoundToInt(v.fontSize * (1f + t * 0.35f));
                vfxTextStyle.fontSize = size;
                vfxTextStyle.normal.textColor = drawCol;

                float labelW = 120f;
                float labelH = 28f;
                Rect rect = new Rect(guiX - labelW * 0.5f, guiY - labelH * 0.5f, labelW, labelH);
                GUI.Label(rect, v.text, vfxTextStyle);
            }

            // Nhãn biểu cảm trạng thái sinh động ngay phía trên nhân vật
            if (playerController != null && playerController.IsPerformingAction)
            {
                string tag = currentVisual switch
                {
                    ActionVisualType.Sleeping => "😴 Đang ngủ say... Zzz",
                    ActionVisualType.EatingBread => "🍞 Đang gặm bánh mì thơm ngon",
                    ActionVisualType.DrinkingWater => "💧 Đang uống nước giải khát",
                    ActionVisualType.EatingSoup => "🍲 Đang dùng súp ấm nóng",
                    ActionVisualType.Cooking => "🍳 Đang xốc chảo nấu ăn",
                    ActionVisualType.CarpentrySaw => "🪚 Đang cưa gỗ thợ mộc",
                    ActionVisualType.Cashier => "💰 Đang tính tiền thanh toán",
                    ActionVisualType.WateringPlants => "🌱 Đang tưới nước & chăm sóc ruộng",
                    ActionVisualType.DoctorHealing => "⚕️ Đang sơ cứu & điều trị vết thương",
                    ActionVisualType.ReadingBook => "📖 Đang đọc sách thư giãn",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(tag))
                {
                    Vector3 tagWorld = transform.position + new Vector3(0f, (currentVisual == ActionVisualType.Sleeping ? 0.38f : 0.85f), 0f);
                    Vector3 tagScreen = mainCam.WorldToScreenPoint(tagWorld);
                    if (tagScreen.z > 0f)
                    {
                        float gx = tagScreen.x;
                        float gy = Screen.height - tagScreen.y;
                        float tw = Mathf.Max(140f, tag.Length * 7.5f);
                        Rect tRect = new Rect(gx - tw * 0.5f, gy - 20f, tw, 22f);

                        Color bgCol = (currentVisual == ActionVisualType.Sleeping)
                            ? new Color(0.12f, 0.16f, 0.28f, 0.88f)
                            : new Color(0.12f, 0.15f, 0.18f, 0.88f);
                        DrawSolidRect(tRect, bgCol);

                        GUIStyle labelSt = new GUIStyle(vfxTextStyle)
                        {
                            fontSize = 10,
                            normal = { textColor = new Color(1f, 0.95f, 0.75f) }
                        };
                        GUI.Label(tRect, tag, labelSt);
                    }
                }
            }
        }

        private static Texture2D solidTexture;
        private static void DrawSolidRect(Rect rect, Color color)
        {
            if (solidTexture == null)
            {
                solidTexture = new Texture2D(1, 1);
                solidTexture.SetPixel(0, 0, Color.white);
                solidTexture.Apply();
            }
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, solidTexture);
            GUI.color = old;
        }
    }
}
