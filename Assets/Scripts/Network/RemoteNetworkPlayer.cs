using System;
using UnityEngine;
using CozyLife.Core;
using CozyLife.Player;
using CozyLife.World;

namespace CozyLife.Network
{
    /// <summary>
    /// Đại diện nhân vật của người chơi khác kết nối vào cùng thế giới qua mạng.
    /// Tự động đồng bộ vị trí, ngoại trang tùy biến, biểu cảm, cử động bước đi và bong bóng chat trên đầu.
    /// </summary>
    public class RemoteNetworkPlayer : MonoBehaviour
    {
        public int ClientId { get; private set; }
        public string PlayerName { get; private set; } = "Bạn Bè";
        public ProfessionType Profession { get; private set; } = ProfessionType.Farmer;
        public CharacterAppearanceData Appearance { get; private set; }
        public BuildingType CurrentBuilding { get; private set; } = BuildingType.None;
        public string CurrentActionTitle { get; private set; } = "";
        public bool IsSeated { get; private set; }
        public bool IsSleeping { get; private set; }

        private SpriteRenderer spriteRenderer;
        private IsometricSorting isoSorting;
        private Vector3 targetPosition;
        private bool isMoving = false;
        private int currentWalkFrame = 0;
        private float lastUpdateTime;

        // Bong bóng trò chuyện trên đầu (Speech Bubble)
        private string chatBubbleText = "";
        private float chatBubbleTimer = 0f;
        private const float ChatBubbleDuration = 5.0f;

        // IMGUI Style cho Nameplate & Bong bóng Chat
        private static GUIStyle nameTagStyle;
        private static GUIStyle chatBubbleStyle;
        private static Texture2D whiteBoxTex;
        private static bool stylesReady = false;

        public void Initialize(PlayerDataSync data)
        {
            ClientId = data.clientId;
            PlayerName = string.IsNullOrEmpty(data.playerName) ? $"Người Chơi #{ClientId}" : data.playerName;
            Profession = (ProfessionType)data.profession;
            CurrentBuilding = (BuildingType)data.currentBuilding;
            CurrentActionTitle = data.actionTitle ?? "";
            IsSeated = data.isSeated;
            IsSleeping = data.isSleeping;

            try
            {
                if (!string.IsNullOrEmpty(data.appearanceJson))
                {
                    Appearance = JsonUtility.FromJson<CharacterAppearanceData>(data.appearanceJson);
                }
            }
            catch (Exception)
            {
                Appearance = new CharacterAppearanceData { characterName = PlayerName };
            }

            if (Appearance == null)
            {
                Appearance = new CharacterAppearanceData { characterName = PlayerName };
            }

            transform.position = new Vector3(data.posX, data.posY, data.posZ);
            targetPosition = transform.position;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            isoSorting = GetComponent<IsometricSorting>();
            if (isoSorting == null)
                isoSorting = gameObject.AddComponent<IsometricSorting>();
            isoSorting.Configure(false, 5);

            UpdateAppearanceSprite(0);
            lastUpdateTime = Time.realtimeSinceStartup;
        }

        public void ApplyNetworkUpdate(PlayerDataSync data)
        {
            targetPosition = new Vector3(data.posX, data.posY, data.posZ);
            CurrentBuilding = (BuildingType)data.currentBuilding;
            CurrentActionTitle = data.actionTitle ?? "";
            IsSeated = data.isSeated;
            IsSleeping = data.isSleeping;
            isMoving = data.isMoving;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = data.flipX;
            }

            if (data.walkFrame != currentWalkFrame)
            {
                currentWalkFrame = data.walkFrame;
                UpdateAppearanceSprite(currentWalkFrame);
            }

            lastUpdateTime = Time.realtimeSinceStartup;
        }

        public void ShowChatBubble(string message)
        {
            chatBubbleText = message;
            chatBubbleTimer = ChatBubbleDuration;
            CozyAudioManager.PlaySfx(CozySfxType.UiClick, 0.75f);
        }

        private void UpdateAppearanceSprite(int frame)
        {
            if (spriteRenderer == null || Appearance == null)
                return;

            Sprite s = CharacterAppearanceCompositor.GetOrCreateCustomSprite(Appearance, frame);
            if (s != null)
            {
                spriteRenderer.sprite = s;
            }
        }

        private void Update()
        {
            // 1. Kiểm tra sự phù hợp phân khu tòa nhà: Chỉ hiển thị khi cùng ở Ngoài trời hoặc cùng trong cùng 1 tòa nhà
            BuildingType localBld = CozyTownWorldBuilder.Instance != null
                ? CozyTownWorldBuilder.Instance.CurrentBuilding
                : BuildingType.None;

            bool isSameArea = (localBld == CurrentBuilding);
            if (spriteRenderer != null && spriteRenderer.enabled != isSameArea)
            {
                spriteRenderer.enabled = isSameArea;
            }

            if (!isSameArea)
                return;

            // 2. Nội suy mượt mà vị trí (Smooth Lerp)
            float lerpSpeed = 14f;
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);

            // 3. Hiệu ứng nghiêng nhẹ vai khi bước đi
            if (isMoving && !IsSleeping && !IsSeated)
            {
                float tilt = (currentWalkFrame == 1) ? 3.0f : (currentWalkFrame == 3 ? -3.0f : 0f);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(0, 0, tilt), 16f * Time.deltaTime);
            }
            else
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.identity, 16f * Time.deltaTime);
            }

            // 4. Giảm thời gian hiển thị bong bóng chat
            if (chatBubbleTimer > 0f)
            {
                chatBubbleTimer -= Time.deltaTime;
                if (chatBubbleTimer <= 0f)
                {
                    chatBubbleText = "";
                }
            }
        }

        private static void EnsureStyles()
        {
            if (stylesReady && whiteBoxTex != null) return;

            whiteBoxTex = new Texture2D(1, 1);
            whiteBoxTex.SetPixel(0, 0, Color.white);
            whiteBoxTex.Apply();

            nameTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            chatBubbleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.12f, 0.12f, 0.16f) }
            };

            stylesReady = true;
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null || spriteRenderer == null || !spriteRenderer.enabled)
                return;

            // Kiểm tra nếu local player đang ở màn hình menu thì không vẽ tag
            if (CozyLife.UI.CozyLifeMainMenu.IsActive)
                return;

            EnsureStyles();

            Vector3 headWorldPos = transform.position + new Vector3(0, 0.72f, 0);
            Vector3 screenPos = cam.WorldToScreenPoint(headWorldPos);

            // Nếu nằm ngoài màn hình camera thì bỏ qua
            if (screenPos.z < 0 || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height)
                return;

            float guiY = Screen.height - screenPos.y;
            float guiX = screenPos.x;

            var profDef = CozyLifeDatabase.GetProfession(Profession);
            Color tagTheme = profDef.themeColor;

            // 1. Thẻ Tên Nhân Vật & Nghề Nghiệp
            string displayName = $"{PlayerName} • {profDef.titleBadge}";
            if (!string.IsNullOrEmpty(CurrentActionTitle))
            {
                displayName = $"{PlayerName} ({CurrentActionTitle})";
            }

            Vector2 tagSize = nameTagStyle.CalcSize(new GUIContent(displayName));
            float tagW = Mathf.Max(90f, tagSize.x + 14f);
            float tagH = 20f;
            Rect tagRect = new Rect(guiX - tagW * 0.5f, guiY - tagH, tagW, tagH);

            DrawSolid(tagRect, new Color(0.10f, 0.12f, 0.16f, 0.88f));
            DrawBorder(tagRect, tagTheme, 1.2f);
            GUI.Label(tagRect, displayName, nameTagStyle);

            // 2. Bong bóng Trò Chuyện (Speech Bubble) trên đầu
            if (chatBubbleTimer > 0f && !string.IsNullOrEmpty(chatBubbleText))
            {
                float alpha = Mathf.Clamp01(chatBubbleTimer / 0.5f);
                string bubbleMsg = $"💬 {chatBubbleText}";
                Vector2 bSize = chatBubbleStyle.CalcSize(new GUIContent(bubbleMsg));
                float bW = Mathf.Clamp(bSize.x + 16f, 80f, 220f);
                float bH = Mathf.Max(26f, bSize.y + 8f);
                Rect bRect = new Rect(guiX - bW * 0.5f, tagRect.y - bH - 6f, bW, bH);

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                DrawSolid(bRect, new Color(0.98f, 0.98f, 0.96f, 0.96f * alpha));
                DrawBorder(bRect, new Color(0.35f, 0.55f, 0.85f, alpha), 1.5f);
                GUI.Label(bRect, bubbleMsg, chatBubbleStyle);

                GUI.color = prevColor;
            }
        }

        private static void DrawSolid(Rect rect, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(rect, whiteBoxTex);
            GUI.color = prev;
        }

        private static void DrawBorder(Rect rect, Color c, float width = 1f)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, width), c);
            DrawSolid(new Rect(rect.x, rect.yMax - width, rect.width, width), c);
            DrawSolid(new Rect(rect.x, rect.y, width, rect.height), c);
            DrawSolid(new Rect(rect.xMax - width, rect.y, width, rect.height), c);
        }
    }
}
