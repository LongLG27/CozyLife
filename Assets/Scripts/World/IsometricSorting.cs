using UnityEngine;

namespace CozyLife.World
{
    /// <summary>
    /// Tự động sắp xếp thứ tự hiển thị (Sorting Order) theo trục Y trong góc nhìn Isometric (như Project Zomboid).
    /// Nhân vật đi phía sau tòa nhà sẽ bị che khuất, đi phía trước sẽ nổi lên trên.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsometricSorting : MonoBehaviour
    {
        [SerializeField] private bool isStaticObject = false;
        [SerializeField] private int sortingOffset = 0;

        private SpriteRenderer spriteRenderer;

        public void Configure(bool staticObj, int offset = 0)
        {
            isStaticObject = staticObj;
            sortingOffset = offset;
            UpdateSorting();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            UpdateSorting();
        }

        private void LateUpdate()
        {
            if (!isStaticObject)
            {
                UpdateSorting();
            }
        }

        public void UpdateSorting()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                // Giá trị Y càng thấp (càng gần phía dưới màn hình) thì sortingOrder càng cao
                spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f) + sortingOffset;
            }
        }
    }
}
