using System;
using UnityEngine;
using UnityEngine.InputSystem;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.World;
using CozyLife.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3.8f;
    [SerializeField] private float sprintMultiplier = 1.45f;
    [SerializeField] private float interactRadius = 1.35f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;

    private CharacterStats stats;
    private ProfessionAndInventory profInv;
    private IsometricSorting isoSorting;
    private Camera mainCam;
    private PlayerActionVisualizer actionVisualizer;

    private RestaurantTableInteractable seatedTable;
    private JapaneseFloorTableInteractable seatedFloorTable;
    private CozyLife.World.CampfireSeatInteractable seatedCampfireSeat;
    private Vector3 seatedFloorCushionPos;
    private float seatedTimer = 0f;
    private float customSpeedBuffMultiplier = 1f;
    private float customSpeedBuffTimer = 0f;

    // Chu kỳ chuyển động tay chân khi di chuyển (Walk Cycle Animation)
    private static readonly int[] WalkSequence = { 1, 2, 3, 0 };
    private float walkCycleTimer = 0f;
    private int walkStepIndex = 0;
    private int currentDisplayedWalkFrame = 0;

    // Hệ thống Thời Gian Thực Hiện Hành Động (1 giây thực = 1 phút trong game)
    private bool isPerformingAction = false;
    private string currentActionTitle = "";
    private float actionElapsedMinutes = 0f;
    private float actionTotalMinutes = 30f;
    private Action onActionCompleteCallback;

    // Chế độ Trang Trí Nhà (Home Decorating Sandbox)
    private bool isDecoratingMode = false;
    private bool isRepositionMode = false; // Phân tách rõ ràng: Đặt Đồ Mới vs Dời Đồ Cũ
    private ItemType selectedDecorFurnitureType = ItemType.CozyChair;
    private bool isDecorGhostFlipped = false;
    private GameObject decorGhostObj;
    private SpriteRenderer decorGhostSr;
    private CozyFurnitureAura movingFurnitureAura = null;
    private Vector3 movingOriginalPosition;
    private bool movingOriginalFlipped;
    private CozyFurnitureAura hoveredFurniture = null;

    public WorldInteractableBase NearestInteractable { get; private set; }
    public StationType CurrentNearbyStation { get; private set; } = StationType.None;
    public bool IsSeatedAtTable => seatedTable != null;
    public bool IsSeatedAtFloorTable => seatedFloorTable != null;
    public bool IsSeatedAtCampfire => seatedCampfireSeat != null;
    public bool IsDecoratingMode => isDecoratingMode;
    public bool IsRepositionMode => isRepositionMode;
    public ItemType SelectedDecorFurnitureType => selectedDecorFurnitureType;
    public bool IsDecorGhostFlipped => isDecorGhostFlipped;
    public bool IsMovingExistingFurniture => movingFurnitureAura != null;
    public string MovingFurnitureTitle => movingFurnitureAura != null ? movingFurnitureAura.FurnitureTitle : "";
    public CozyFurnitureAura HoveredFurniture => hoveredFurniture;

    public bool IsPerformingAction => isPerformingAction;
    public string CurrentActionTitle => currentActionTitle;
    public float ActionElapsedMinutes => actionElapsedMinutes;
    public float ActionTotalMinutes => actionTotalMinutes;
    public float ActionProgress01 => actionTotalMinutes > 0.01f ? Mathf.Clamp01(actionElapsedMinutes / actionTotalMinutes) : 1f;
    public int CurrentDisplayedWalkFrame => currentDisplayedWalkFrame;
    public static PlayerController LocalPlayer { get; private set; }

    private void Awake()
    {
        LocalPlayer = this;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        spriteRenderer = GetComponent<SpriteRenderer>();

        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            boxCol.size = new Vector2(0.36f, 0.22f);
            boxCol.offset = new Vector2(0f, -0.12f);
        }

        stats = GetComponent<CharacterStats>();
        if (stats == null) stats = gameObject.AddComponent<CharacterStats>();

        profInv = GetComponent<ProfessionAndInventory>();
        if (profInv == null) profInv = gameObject.AddComponent<ProfessionAndInventory>();

        isoSorting = GetComponent<IsometricSorting>();
        if (isoSorting == null) isoSorting = gameObject.AddComponent<IsometricSorting>();
        isoSorting.Configure(false, 5);

        actionVisualizer = GetComponent<PlayerActionVisualizer>();
        if (actionVisualizer == null) actionVisualizer = gameObject.AddComponent<PlayerActionVisualizer>();
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.orthographicSize = 3.6f;
        }

        if (CozyTownWorldBuilder.Instance == null)
        {
            GameObject worldMgr = new GameObject("CozyTownWorldManager");
            worldMgr.AddComponent<CozyTownWorldBuilder>();
        }

        if (CozyLifeHUD.Instance == null)
        {
            GameObject hudObj = new GameObject("CozyLifeHUD");
            CozyLifeHUD hud = hudObj.AddComponent<CozyLifeHUD>();
            hud.BindPlayer(this, stats, profInv);
        }
        else
        {
            CozyLifeHUD.Instance.BindPlayer(this, stats, profInv);
        }

        if (CozyLifeMainMenu.Instance == null)
        {
            GameObject menuObj = new GameObject("CozyLifeMainMenu");
            CozyLifeMainMenu menu = menuObj.AddComponent<CozyLifeMainMenu>();
            menu.BindPlayer(this, profInv);
        }
        else
        {
            CozyLifeMainMenu.Instance.BindPlayer(this, profInv);
        }

        if (CozyLife.Network.CozyNetworkManager.Instance == null)
        {
            GameObject netObj = new GameObject("CozyNetworkManager");
            netObj.AddComponent<CozyLife.Network.CozyNetworkManager>();
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    /// <summary>
    /// Bắt đầu một hành động cần thời gian thực tế trong game (VD: Ăn uống 30p, Phụ bếp 60p, Đóng gỗ 45p, Ngủ 120p).
    /// Với quy chuẩn 1 phút thực = 1 tiếng game, thì 1 phút trong game = 1 giây thực tế.
    /// Kèm theo hoạt ảnh trực quan và âm thanh sống động (ngủ ngáy Zzz, xốc chảo, cưa gỗ, gặm bánh, uống nước, tính tiền).
    /// </summary>
    public void StartTimedAction(string title, float gameMinutesDuration, Action onComplete, ActionVisualType forcedVisual = ActionVisualType.None, Vector3? targetPos = null)
    {
        isPerformingAction = true;
        currentActionTitle = title;
        actionElapsedMinutes = 0f;
        actionTotalMinutes = Mathf.Max(1f, gameMinutesDuration);
        onActionCompleteCallback = onComplete;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;

        actionVisualizer?.BeginAction(title, forcedVisual, targetPos);
    }

    public void StartSleepingOnBed(BedInteractable bed, bool isClinic, float gameMinutesDuration, Action onComplete)
    {
        string label = isClinic ? "🛏️ Đang nằm điều trị" : "😴 Đang ngủ nghỉ ngơi";
        Vector3 bedPos = bed.transform.position;
        StartTimedAction(label, gameMinutesDuration, onComplete, ActionVisualType.Sleeping, bedPos);
    }

    public void CancelTimedAction(string reason = "⚠️ Đã hủy hành động.")
    {
        if (!isPerformingAction) return;
        isPerformingAction = false;
        onActionCompleteCallback = null;

        actionVisualizer?.StopCurrentActionVisuals(true);

        if (!string.IsNullOrEmpty(reason))
            CozyLifeHUD.PushToast(reason);
    }

    public void SitAtRestaurantTable(RestaurantTableInteractable table)
    {
        if (table == null) return;
        seatedTable = table;
        seatedTimer = 0.35f;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        transform.position = table.SeatWorldPosition;
        rb.position = table.SeatWorldPosition;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = false;
        }
    }

    public void StandUpFromTable()
    {
        if (seatedTable == null) return;

        if (isPerformingAction)
            CancelTimedAction("⚠️ Đã đứng dậy khỏi bàn khi chưa xong.");

        Vector3 standPos = seatedTable.transform.position + new Vector3(0f, -0.38f, 0f);
        seatedTable = null;
        transform.position = standPos;
        rb.position = standPos;
        CozyLifeHUD.Instance?.CloseRestaurantOrderModal();
    }

    public void SitAtFloorTable(JapaneseFloorTableInteractable table, Vector3 seatPos)
    {
        if (table == null) return;
        seatedFloorTable = table;
        seatedFloorCushionPos = seatPos;
        seatedTimer = 0.35f;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        transform.position = seatPos;
        rb.position = seatPos;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = seatPos.x < table.transform.position.x ? false : true;
        }
    }

    public void StandUpFromFloorTable()
    {
        if (seatedFloorTable == null) return;

        if (isPerformingAction)
            CancelTimedAction("⚠️ Đã đứng dậy khỏi bàn bệt khi chưa xong.");

        Vector3 standPos = seatedFloorTable.transform.position + new Vector3(0f, -0.32f, 0f);
        seatedFloorTable = null;
        transform.position = standPos;
        rb.position = standPos;
        CozyJapaneseCafeManager.Instance?.CloseMenuModal();
    }

    public void SitAtCampfireSeat(CozyLife.World.CampfireSeatInteractable seat)
    {
        if (seat == null) return;
        seatedCampfireSeat = seat;
        seatedTimer = 0.35f;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        transform.position = seat.SeatWorldPosition;
        rb.position = seat.SeatWorldPosition;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = seat.FacesLeft;
        }
    }

    public void StandUpFromCampfireSeat()
    {
        if (seatedCampfireSeat == null) return;
        var seat = seatedCampfireSeat;
        seatedCampfireSeat = null;
        seat.OnPlayerStoodUp();
    }

    public void ApplySpeedBuff(float multiplier, float durationSeconds)
    {
        customSpeedBuffMultiplier = multiplier;
        customSpeedBuffTimer = durationSeconds;
    }

    public void TeleportToPosition(Vector3 targetPos)
    {
        CancelTimedAction("");
        ExitDecoratingMode();
        rb.linearVelocity = Vector2.zero;
        transform.position = targetPos;
        rb.position = targetPos;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(targetPos.x, targetPos.y, -10f);
        }
    }

    private void Update()
    {
        if (CozyLifeMainMenu.IsActive || CozyChessManager.IsChessSceneActive || CozyXiangqiManager.IsXiangqiSceneActive || CozyLife.Network.CozyNetworkManager.IsChatFocused)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (seatedTimer > 0f)
            seatedTimer -= Time.deltaTime;

        if (customSpeedBuffTimer > 0f)
        {
            customSpeedBuffTimer -= Time.deltaTime;
            if (customSpeedBuffTimer <= 0f)
            {
                customSpeedBuffMultiplier = 1f;
                CozyLifeHUD.PushToast("Hết hiệu ứng tăng tốc độ cà phê.");
            }
        }

        if (seatedFloorTable != null && seatedTimer <= 0f)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame ||
                kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
            {
                if (!CozyJapaneseCafeManager.IsMenuOpen)
                {
                    StandUpFromFloorTable();
                    CozyLifeHUD.PushToast("🚶 Đã đứng dậy khỏi bàn bệt.");
                }
            }
        }

        UpdateTimedActionProgress();
        HandleKeyboardShortcuts();
        UpdateWalkAnimation();
        UpdateHomeDecoratingSandbox();
        ScanNearbyEnvironment();
    }

    public void ToggleDecoratingMode()
    {
        BuildingType curBld = CozyTownWorldBuilder.Instance != null
            ? CozyTownWorldBuilder.Instance.CurrentBuilding
            : BuildingType.None;

        if (!isDecoratingMode)
        {
            if (!CozyLifeDatabase.IsResidentialHome(curBld) || !BuildingDoorInteractable.IsBuildingOwned(curBld))
            {
                CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert);
                CozyLifeHUD.PushToast("🏠 Hãy bước vào bên trong ngôi nhà thuộc sở hữu của bạn rồi nhấn [B] để Trang Trí!");
                return;
            }

            isDecoratingMode = true;
            isRepositionMode = false;
            movingFurnitureAura = null;
            hoveredFurniture = null;
            CozyLifeHUD.Instance?.CloseAllModals();
            EnsureDecorGhostObject();
            RefreshDecorGhostSprite();
            if (decorGhostObj != null)
                decorGhostObj.SetActive(true);
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("🛋️ Đã mở Chế Độ Trang Trí! [Chuột Trái]: Đặt • [Chuột Phải / R]: Xoay • [M]: Chuyển sang Dời Đồ");
        }
        else
        {
            ExitDecoratingMode();
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("🛋️ Đã thoát Chế Độ Trang Trí Nhà.");
        }
    }

    public void ExitDecoratingMode()
    {
        if (movingFurnitureAura != null)
        {
            CancelMovingFurniture();
        }
        isDecoratingMode = false;
        isRepositionMode = false;
        movingFurnitureAura = null;
        hoveredFurniture = null;
        if (decorGhostObj != null)
            decorGhostObj.SetActive(false);
    }

    public void SetDecorSubMode(bool moveMode)
    {
        if (isRepositionMode == moveMode) return;

        if (isRepositionMode && movingFurnitureAura != null)
        {
            CancelMovingFurniture();
        }

        isRepositionMode = moveMode;
        if (isRepositionMode)
        {
            // Trong Chế độ Dời Đồ: Ẩn ghost preview của đồ mới nếu chưa nhấc đồ
            if (decorGhostObj != null && movingFurnitureAura == null)
                decorGhostObj.SetActive(false);
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            CozyLifeHUD.PushToast("✋ Đã bật Chế Độ Dời Đồ! Di chuột & Click Chuột Trái vào món đồ muốn dời.");
        }
        else
        {
            // Quay lại Chế độ Đặt Đồ: Hiển thị lại ghost preview của đồ đang chọn
            EnsureDecorGhostObject();
            RefreshDecorGhostSprite();
            if (decorGhostObj != null)
                decorGhostObj.SetActive(true);
            CozyAudioManager.PlaySfx(CozySfxType.UiClick);
            ItemDefinition def = CozyLifeDatabase.GetItem(selectedDecorFurnitureType);
            string name = def != null ? def.displayName : "Nội Thất";
            CozyLifeHUD.PushToast($"🔨 Chế Độ Đặt Đồ: [{name}]. [Click Trái]: Đặt • [Chuột Phải / R]: Xoay");
        }
    }

    public void ToggleMoveMode()
    {
        SetDecorSubMode(!isRepositionMode);
    }

    public void SelectDecorFurniture(ItemType type)
    {
        if (isRepositionMode)
        {
            if (movingFurnitureAura != null)
                CancelMovingFurniture();
            isRepositionMode = false;
        }

        selectedDecorFurnitureType = type;
        EnsureDecorGhostObject();
        RefreshDecorGhostSprite();
        if (decorGhostObj != null)
            decorGhostObj.SetActive(true);
        CozyAudioManager.PlaySfx(CozySfxType.UiClick);
    }

    public void FlipDecorGhost()
    {
        isDecorGhostFlipped = !isDecorGhostFlipped;
        if (decorGhostSr != null)
            decorGhostSr.flipX = isDecorGhostFlipped;
        CozyAudioManager.PlaySfx(CozySfxType.UiClick);
    }

    private void EnsureDecorGhostObject()
    {
        if (decorGhostObj == null)
        {
            decorGhostObj = new GameObject("DecorGhostPreview");
            decorGhostSr = decorGhostObj.AddComponent<SpriteRenderer>();
            decorGhostSr.sortingOrder = 1400;
        }
        decorGhostObj.SetActive(isDecoratingMode);
    }

    private void RefreshDecorGhostSprite()
    {
        EnsureDecorGhostObject();
        ItemDefinition def = CozyLifeDatabase.GetItem(selectedDecorFurnitureType);
        if (def != null && decorGhostSr != null)
        {
            Vector2 pivot = (selectedDecorFurnitureType == ItemType.PersianRug) ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.18f);
            float ppu = (selectedDecorFurnitureType == ItemType.PersianRug) ? 85f : 88f;
            decorGhostSr.sprite = CozyLifeDatabase.LoadSprite(def.placeableObjectSpritePath, pivot, ppu);
            decorGhostSr.flipX = isDecorGhostFlipped;
            decorGhostSr.color = new Color(0.55f, 1.0f, 0.68f, 0.65f);
        }
    }

    private Vector3 GetClampedDecorWorldPosition(BuildingType curBld)
    {
        if (mainCam == null) mainCam = Camera.main;
        Vector3 rawWorld = transform.position + new Vector3(0.45f, -0.20f, 0f);
        if (mainCam != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 mWorld = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));
            mWorld.z = 0f;
            rawWorld = mWorld;
        }

        Vector3 origin = CozyTownWorldBuilder.GetInteriorRoomOrigin(curBld);
        Vector3 localOffset = rawWorld - origin;
        Vector2Int isoGrid = CozyTownWorldBuilder.WorldToIsoGrid(localOffset);
        int maxHalf = (curBld == BuildingType.BuyableTownhouse || curBld == BuildingType.BuyableVilla) ? 4 : 3;
        int clampedGx = Mathf.Clamp(isoGrid.x, -maxHalf, maxHalf);
        int clampedGy = Mathf.Clamp(isoGrid.y, -maxHalf, maxHalf);

        // Hút nhẹ về lưới nửa ô để đặt đồ mượt mà, thẳng hàng đẹp mắt
        Vector3 snapped = origin + CozyTownWorldBuilder.IsoToWorld(clampedGx, clampedGy);
        return Vector3.Lerp(rawWorld, snapped, 0.35f);
    }

    private void UpdateHomeDecoratingSandbox()
    {
        if (!isDecoratingMode)
            return;

        BuildingType curBld = CozyTownWorldBuilder.Instance != null
            ? CozyTownWorldBuilder.Instance.CurrentBuilding
            : BuildingType.None;

        if (!CozyLifeDatabase.IsResidentialHome(curBld))
        {
            ExitDecoratingMode();
            return;
        }

        Vector3 targetPos = GetClampedDecorWorldPosition(curBld);

        if (Mouse.current == null)
            return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        float guiY = Screen.height - mouseScreen.y;
        // Không đặt nhầm khi click vào thanh công cụ dưới cùng hoặc góc đồng hồ
        bool overHudBar = (guiY >= Screen.height - 180f) || (mouseScreen.x >= Screen.width - 255f && guiY <= 140f);

        // ==========================================
        // 1. CHẾ ĐỘ DỜI ĐỒ (REPOSITION / MOVE MODE)
        // ==========================================
        if (isRepositionMode)
        {
            if (IsMovingExistingFurniture)
            {
                // Đang cầm một món đồ đi dời vị trí
                EnsureDecorGhostObject();
                decorGhostObj.transform.position = targetPos;
                decorGhostObj.SetActive(true);

                if (!overHudBar)
                {
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        FinishMovingFurniture(curBld, targetPos);
                    }
                    else if (Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        // Chuột phải xoay hướng món đồ đang dời
                        FlipDecorGhost();
                    }
                }
            }
            else
            {
                // Chưa cầm món nào: tìm món đồ gần chuột để highlight
                if (decorGhostObj != null && decorGhostObj.activeSelf)
                    decorGhostObj.SetActive(false);

                hoveredFurniture = FindFurnitureNearPosition(curBld, targetPos, 0.85f);

                if (!overHudBar)
                {
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (hoveredFurniture != null)
                        {
                            StartMovingFurniture(hoveredFurniture);
                        }
                        else
                        {
                            CozyLifeHUD.PushToast("ℹ️ Hãy click chuột trái vào món đồ nội thất bạn muốn dời vị trí.");
                        }
                    }
                    else if (Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        if (hoveredFurniture != null)
                        {
                            TryPickupFurnitureNearPosition(curBld, targetPos);
                        }
                    }
                }
            }
        }
        // ==========================================
        // 2. CHẾ ĐỘ ĐẶT MỚI (PLACE NEW MODE)
        // ==========================================
        else
        {
            hoveredFurniture = null;
            EnsureDecorGhostObject();
            decorGhostObj.transform.position = targetPos;
            decorGhostObj.SetActive(true);

            if (!overHudBar)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    // LUÔN LUÔN ĐẶT MÓN ĐỒ ĐANG CHỌN - TUYỆT ĐỐI KHÔNG TỰ Ý NHẶT ĐỒ XUNG QUANH
                    PlaceSelectedFurnitureAt(curBld, targetPos);
                }
                else if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    // Chuột phải xoay hướng cực kỳ thuận tiện cho người chơi
                    FlipDecorGhost();
                }
            }
        }
    }

    public CozyFurnitureAura FindFurnitureNearPosition(BuildingType curBld, Vector3 refPos, float maxDist = 0.75f)
    {
        CozyFurnitureAura closest = null;
        float bestDist = maxDist;

        for (int i = 0; i < CozyFurnitureAura.AllFurniture.Count; i++)
        {
            var f = CozyFurnitureAura.AllFurniture[i];
            if (f == null || !f.IsPlayerPlaced || f.RoomBuilding != curBld)
                continue;

            float d = Vector2.Distance(refPos, f.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                closest = f;
            }
        }
        return closest;
    }

    public void StartMovingFurniture(CozyFurnitureAura furniture)
    {
        if (furniture == null) return;

        movingFurnitureAura = furniture;
        movingOriginalPosition = furniture.transform.position;
        movingOriginalFlipped = furniture.IsFlipped;

        // Cập nhật ghost preview khớp với món đồ đang dời
        selectedDecorFurnitureType = furniture.FurnitureItemType;
        isDecorGhostFlipped = furniture.IsFlipped;
        RefreshDecorGhostSprite();

        // Ẩn tạm vật thể gốc trong lúc đang dời vị trí
        furniture.gameObject.SetActive(false);

        if (decorGhostObj != null)
            decorGhostObj.SetActive(true);

        CozyAudioManager.PlaySfx(CozySfxType.PickupFurniture);
        CozyLifeHUD.PushToast($"✋ Đang dời [{furniture.FurnitureTitle}]! Click Trái: Đặt vị trí mới • [R / Chuột Phải]: Xoay • [X]: Cất • [ESC]: Hủy");
    }

    public void FinishMovingFurniture(BuildingType curBld, Vector3 targetPos)
    {
        if (movingFurnitureAura == null) return;

        movingFurnitureAura.transform.position = targetPos;
        movingFurnitureAura.IsFlipped = isDecorGhostFlipped;
        var sr = movingFurnitureAura.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = isDecorGhostFlipped;

        movingFurnitureAura.gameObject.SetActive(true);
        string title = movingFurnitureAura.FurnitureTitle;
        movingFurnitureAura = null;

        if (decorGhostObj != null)
            decorGhostObj.SetActive(false);

        CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture);
        CozySaveManager.SaveGame(false);
        int totalScore = CozyFurnitureAura.GetRoomCozyScore(curBld);
        CozyLifeHUD.PushToast($"🛋️ Đã dời [{title}] đến vị trí mới! (✨ Điểm Ấm Cúng: {totalScore}đ)");
    }

    public void CancelMovingFurniture()
    {
        if (movingFurnitureAura == null) return;

        movingFurnitureAura.transform.position = movingOriginalPosition;
        movingFurnitureAura.IsFlipped = movingOriginalFlipped;
        var sr = movingFurnitureAura.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = movingOriginalFlipped;

        movingFurnitureAura.gameObject.SetActive(true);
        string title = movingFurnitureAura.FurnitureTitle;
        movingFurnitureAura = null;

        if (decorGhostObj != null)
            decorGhostObj.SetActive(false);

        CozyAudioManager.PlaySfx(CozySfxType.UiClick);
        CozyLifeHUD.PushToast($"↩️ Đã hủy dời vị trí [{title}].");
    }

    public void StoreMovingFurnitureToInventory()
    {
        if (movingFurnitureAura == null) return;

        ItemType itemType = movingFurnitureAura.FurnitureItemType;
        string title = movingFurnitureAura.FurnitureTitle;
        Destroy(movingFurnitureAura.gameObject);
        movingFurnitureAura = null;

        if (decorGhostObj != null)
            decorGhostObj.SetActive(false);

        profInv.AddItem(itemType, 1);
        CozyAudioManager.PlaySfx(CozySfxType.PickupFurniture);
        CozySaveManager.SaveGame(false);
        CozyLifeHUD.PushToast($"📦 Đã cất [{title}] vào túi đồ!");
    }

    public void StartMovingFurnitureUnderCursorOrNearest()
    {
        BuildingType curBld = CozyTownWorldBuilder.Instance != null
            ? CozyTownWorldBuilder.Instance.CurrentBuilding
            : BuildingType.None;
        Vector3 pos = isDecoratingMode ? GetClampedDecorWorldPosition(curBld) : transform.position;
        CozyFurnitureAura candidate = FindFurnitureNearPosition(curBld, pos, 1.25f);
        if (candidate != null)
        {
            if (!isRepositionMode)
                isRepositionMode = true;
            StartMovingFurniture(candidate);
        }
        else
        {
            SetDecorSubMode(true);
            CozyLifeHUD.PushToast("✋ Đã bật Chế Độ Dời Đồ! Hãy click chuột trái vào món đồ muốn dời.");
        }
    }

    public void PlaceSelectedFurnitureAt(BuildingType curBld, Vector3 worldPos)
    {
        ItemDefinition itemDef = CozyLifeDatabase.GetItem(selectedDecorFurnitureType);
        if (itemDef == null)
            return;

        bool usedFromInventory = false;
        if (profInv.GetItemCount(selectedDecorFurnitureType) > 0)
        {
            usedFromInventory = profInv.RemoveItem(selectedDecorFurnitureType, 1);
        }
        else if (profInv.SpendCoins(itemDef.basePrice))
        {
            usedFromInventory = true;
        }
        else
        {
            CozyAudioManager.PlaySfx(CozySfxType.SicknessOrAlert);
            CozyLifeHUD.PushToast($"⚠️ Cần 1 {itemDef.displayName} trong túi hoặc {itemDef.basePrice} Xu để đặt!");
            return;
        }

        if (usedFromInventory)
        {
            CozyFurnitureAura.SpawnPlacedFurniture(selectedDecorFurnitureType, curBld, worldPos, isDecorGhostFlipped, true);
            CozyAudioManager.PlaySfx(CozySfxType.PlaceFurniture);
            CozySaveManager.SaveGame(false);
            int totalScore = CozyFurnitureAura.GetRoomCozyScore(curBld);
            CozyLifeHUD.PushToast($"🛋️ Đã đặt {itemDef.displayName}! (✨ Điểm Ấm Cúng Phòng: {totalScore}đ)");
        }
    }

    public void TryPickupFurnitureNearCursorOrPlayer()
    {
        BuildingType curBld = CozyTownWorldBuilder.Instance != null
            ? CozyTownWorldBuilder.Instance.CurrentBuilding
            : BuildingType.None;
        Vector3 pos = isDecoratingMode ? GetClampedDecorWorldPosition(curBld) : transform.position;
        TryPickupFurnitureNearPosition(curBld, pos);
    }

    private void TryPickupFurnitureNearPosition(BuildingType curBld, Vector3 refPos)
    {
        CozyFurnitureAura closest = FindFurnitureNearPosition(curBld, refPos, 1.25f);

        if (closest != null)
        {
            ItemType returnedType = closest.FurnitureItemType;
            string title = closest.FurnitureTitle;
            Destroy(closest.gameObject);
            profInv.AddItem(returnedType, 1);
            CozyAudioManager.PlaySfx(CozySfxType.PickupFurniture);
            CozySaveManager.SaveGame(false);
            CozyLifeHUD.PushToast($"📦 Đã cất {title} vào lại túi đồ!");
        }
        else
        {
            CozyLifeHUD.PushToast("ℹ️ Hãy đưa chuột lại gần món nội thất bạn đã đặt rồi nhấn Chuột Phải hoặc [X] để cất.");
        }
    }

    private void UpdateWalkAnimation()
    {
        if (spriteRenderer == null || profInv == null)
            return;

        bool isMoving = !IsSeatedAtTable && !IsSeatedAtFloorTable && !isPerformingAction && moveInput.sqrMagnitude > 0.01f;
        if (!isMoving)
        {
            walkCycleTimer = 0f;
            walkStepIndex = 0;
            if (actionVisualizer == null || !actionVisualizer.IsSleeping)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.identity, 14f * Time.deltaTime);
            }

            if (currentDisplayedWalkFrame != 0 && profInv.Appearance != null)
            {
                currentDisplayedWalkFrame = 0;
                spriteRenderer.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(profInv.Appearance, 0);
            }
            return;
        }

        var kb = Keyboard.current;
        bool isSprinting = kb != null && kb.leftShiftKey.isPressed && stats != null && stats.Stamina > 2f;
        float framesPerSecond = isSprinting ? 13.5f : 9.2f;

        walkCycleTimer += Time.deltaTime * framesPerSecond;
        if (walkCycleTimer >= 1f)
        {
            walkCycleTimer -= 1f;
            walkStepIndex = (walkStepIndex + 1) % WalkSequence.Length;
        }

        int targetFrame = WalkSequence[walkStepIndex];
        if (targetFrame != currentDisplayedWalkFrame && profInv.Appearance != null)
        {
            currentDisplayedWalkFrame = targetFrame;
            spriteRenderer.sprite = CharacterAppearanceCompositor.GetOrCreateCustomSprite(profInv.Appearance, targetFrame);
            if (targetFrame == 1 || targetFrame == 3)
            {
                CozyAudioManager.PlaySfx(CozySfxType.Footstep, 0.55f);
            }
        }

        // Nhịp nghiêng vai nhẹ theo bước chân trái / phải giúp dáng đi sinh động hơn
        float tiltAngle = (targetFrame == 1) ? 3.2f : (targetFrame == 3 ? -3.2f : 0f);
        Quaternion targetRot = Quaternion.Euler(0f, 0f, tiltAngle);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, 16f * Time.deltaTime);
    }

    private void UpdateTimedActionProgress()
    {
        if (!isPerformingAction)
            return;

        // Thế giới chung (Multiplayer-Ready): Thời gian chạy tự nhiên, 1 giây thực = 1 phút trong game
        actionElapsedMinutes += Time.deltaTime;
        if (actionElapsedMinutes >= actionTotalMinutes)
        {
            isPerformingAction = false;
            actionVisualizer?.StopCurrentActionVisuals(true);
            var cb = onActionCompleteCallback;
            onActionCompleteCallback = null;
            cb?.Invoke();
        }
    }

    private void HandleKeyboardShortcuts()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 kbMove = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1f;

        if (CozySportsMinigameManager.IsGameActive)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (isPerformingAction)
        {
            moveInput = Vector2.zero;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                CancelTimedAction("⚠️ Đã hủy hành động.");
            }
            return;
        }

        if (IsSeatedAtTable)
        {
            if ((seatedTimer <= 0f && kbMove.sqrMagnitude > 0.1f) || kb.escapeKey.wasPressedThisFrame)
            {
                StandUpFromTable();
            }
            moveInput = Vector2.zero;
        }
        else if (IsSeatedAtFloorTable)
        {
            if ((seatedTimer <= 0f && kbMove.sqrMagnitude > 0.1f) || kb.escapeKey.wasPressedThisFrame)
            {
                StandUpFromFloorTable();
            }
            moveInput = Vector2.zero;
        }
        else if (IsSeatedAtCampfire)
        {
            if ((seatedTimer <= 0f && kbMove.sqrMagnitude > 0.1f) || kb.escapeKey.wasPressedThisFrame)
            {
                StandUpFromCampfireSeat();
            }
            moveInput = Vector2.zero;
        }
        else
        {
            if (kbMove.sqrMagnitude > 0.01f)
                moveInput = kbMove.normalized;
            else if (!kb.anyKey.isPressed)
                moveInput = Vector2.zero;

            if (spriteRenderer != null && Mathf.Abs(moveInput.x) > 0.05f)
            {
                spriteRenderer.flipX = moveInput.x < 0f;
            }
        }

        if (kb.eKey.wasPressedThisFrame && NearestInteractable != null)
        {
            NearestInteractable.Interact(profInv, stats);
        }

        if (kb.fKey.wasPressedThisFrame)
        {
            if (NearestInteractable is TownFriendAgent friend)
                friend.ReceiveGiftFromPlayer(profInv);
            else if (NearestInteractable is PetInteractableAgent petAgent)
                petAgent.Feed(profInv);
        }

        if (kb.pKey.wasPressedThisFrame)
        {
            CozyLifeHUD.Instance?.ToggleProfessionPanel();
        }

        if (kb.cKey.wasPressedThisFrame)
        {
            CozyLifeHUD.Instance?.OpenCraftingPanel();
        }

        if (kb.oKey.wasPressedThisFrame)
        {
            CozyLifeMainMenu.Instance?.OpenMenuInGame();
        }

        if (kb.tabKey.wasPressedThisFrame)
        {
            CozyLifeHUD.Instance?.ToggleFriendsPanel();
        }

        if (kb.kKey.wasPressedThisFrame)
        {
            CozyTownWorldBuilder.Instance?.CycleWeatherForTesting();
        }

        if (kb.mKey.wasPressedThisFrame)
        {
            CozyAudioManager.ToggleMute();
        }

        if (kb.f5Key.wasPressedThisFrame)
        {
            CozySaveManager.SaveGame(true);
        }

        if (kb.bKey.wasPressedThisFrame)
        {
            if (CozyTownWorldBuilder.Instance != null && CozyTownWorldBuilder.Instance.CurrentBuilding == BuildingType.None)
            {
                var bike = GetComponent<CozyBicycleController>();
                if (bike == null) bike = gameObject.AddComponent<CozyBicycleController>();
                bike.ToggleBicycle();
            }
            else
            {
                ToggleDecoratingMode();
            }
        }

        if (kb.vKey.wasPressedThisFrame)
        {
            var bike = GetComponent<CozyBicycleController>();
            if (bike == null) bike = gameObject.AddComponent<CozyBicycleController>();
            bike.ToggleBicycle();
        }

        if (isDecoratingMode)
        {
            if (kb.mKey.wasPressedThisFrame)
            {
                if (isRepositionMode && IsMovingExistingFurniture)
                    CancelMovingFurniture();
                else
                    ToggleMoveMode();
            }
            if (kb.rKey.wasPressedThisFrame)
                FlipDecorGhost();
            if (kb.xKey.wasPressedThisFrame)
            {
                if (isRepositionMode && IsMovingExistingFurniture)
                    StoreMovingFurnitureToInventory();
                else
                    TryPickupFurnitureNearCursorOrPlayer();
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (isRepositionMode)
                {
                    if (IsMovingExistingFurniture)
                        CancelMovingFurniture();
                    else
                        SetDecorSubMode(false); // Trở về Chế độ Đặt Đồ
                }
                else
                {
                    ExitDecoratingMode();
                }
            }
        }

        if (kb.digit1Key.wasPressedThisFrame) profInv.UseItemAtSlot(0);
        if (kb.digit2Key.wasPressedThisFrame) profInv.UseItemAtSlot(1);
        if (kb.digit3Key.wasPressedThisFrame) profInv.UseItemAtSlot(2);
        if (kb.digit4Key.wasPressedThisFrame) profInv.UseItemAtSlot(3);
        if (kb.digit5Key.wasPressedThisFrame) profInv.UseItemAtSlot(4);
        if (kb.digit6Key.wasPressedThisFrame) profInv.UseItemAtSlot(5);
        if (kb.digit7Key.wasPressedThisFrame) profInv.UseItemAtSlot(6);
        if (kb.digit8Key.wasPressedThisFrame) profInv.UseItemAtSlot(7);
    }

    private void ScanNearbyEnvironment()
    {
        float bestDist = interactRadius;
        WorldInteractableBase closest = null;
        StationType detectedStation = StationType.None;

        foreach (var inter in WorldInteractableBase.AllInteractables)
        {
            if (inter == null) continue;
            float d = Vector2.Distance(transform.position, inter.transform.position);
            if (d <= interactRadius)
            {
                if (inter.Station != StationType.None)
                    detectedStation = inter.Station;

                if (d < bestDist)
                {
                    bestDist = d;
                    closest = inter;
                }
            }
        }

        NearestInteractable = closest;
        CurrentNearbyStation = detectedStation;

        bool nearChair = IsSeatedAtTable;
        if (!nearChair)
        {
            var chairs = CozyFurnitureAura.AllFurniture;
            for (int i = 0; i < chairs.Count; i++)
            {
                if (chairs[i] != null && Vector2.Distance(transform.position, chairs[i].transform.position) <= 1.5f)
                {
                    nearChair = true;
                    break;
                }
            }
        }
        stats.SetNearCozyFurniture(nearChair);
    }

    private void FixedUpdate()
    {
        if (CozyLifeMainMenu.IsActive || CozyChessManager.IsChessSceneActive || CozyXiangqiManager.IsXiangqiSceneActive || ParkChessTableInteractable.ActiveSeatedTable != null || ParkXiangqiTableInteractable.ActiveSeatedTable != null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (IsSeatedAtTable || IsSeatedAtFloorTable || IsSeatedAtCampfire || isPerformingAction)
        {
            if (IsSeatedAtTable)
                rb.position = seatedTable.SeatWorldPosition;
            else if (IsSeatedAtFloorTable)
                rb.position = seatedFloorCushionPos;
            else if (IsSeatedAtCampfire)
                rb.position = seatedCampfireSeat.SeatWorldPosition;
            return;
        }

        float speedMult = stats != null ? stats.GetMovementSpeedMultiplier() : 1f;
        if (profInv != null)
            speedMult *= profInv.CurrentProfessionDef.moveSpeedMultiplier;
        if (customSpeedBuffTimer > 0f)
            speedMult *= customSpeedBuffMultiplier;

        var bike = GetComponent<CozyBicycleController>();
        if (bike != null && bike.IsRiding)
        {
            speedMult *= 1.85f;
        }

        var kb = Keyboard.current;
        bool isSprinting = kb != null && kb.leftShiftKey.isPressed && moveInput.sqrMagnitude > 0.01f;
        if (isSprinting && stats != null && stats.TryConsumeStamina(14f * Time.fixedDeltaTime))
        {
            speedMult *= sprintMultiplier;
        }

        rb.MovePosition(rb.position + moveInput * (moveSpeed * speedMult * Time.fixedDeltaTime));
    }

    private void LateUpdate()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 targetCamPos = new Vector3(transform.position.x, transform.position.y, -10f);
            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetCamPos, 7f * Time.deltaTime);
        }
    }
}
