using UnityEngine;

namespace YutArena.InGame
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class DebugPieceView : MonoBehaviour
    {
        public int PlayerId { get; private set; }
        public int PieceId { get; private set; }
        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private Vector3 initialScale;
        private bool usesFallbackVisual;

        private void Awake()
        {
            initialScale = transform.localScale;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            usesFallbackVisual = spriteRenderer == null;
            if (usesFallbackVisual)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateCircleSprite();
            }
            spriteRenderer.sortingOrder = 10;
        }

        public void Configure(int playerId, int pieceId, Color color)
        {
            PlayerId = playerId;
            PieceId = pieceId;
            // Preserve a job prefab's authored appearance; only the primitive fallback is player-tinted.
            baseColor = usesFallbackVisual ? color : Color.white;
            spriteRenderer.color = color;
            gameObject.name = $"Piece_P{playerId}_{pieceId + 1}";
        }

        /// <summary>
        /// InGamePieceDebugController의 Inspector 배율을 말 프리팹에 적용합니다.
        /// 이후 선택 강조가 발생해도 이 크기를 기준으로 유지됩니다.
        /// </summary>
        public void SetBaseScaleMultiplier(float multiplier)
        {
            initialScale *= Mathf.Max(0.01f, multiplier);
            transform.localScale = initialScale;
        }

        public void SetSelected(bool selected)
        {
            transform.localScale = initialScale * (selected ? 1.1f : 1f);
            spriteRenderer.color = selected ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
        }

        public void RefreshColor(bool selected)
        {
            if (spriteRenderer == null) return;
            spriteRenderer.color = selected ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float alpha = Mathf.Clamp01(size * 0.46f - Vector2.Distance(new Vector2(x, y), center) + 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
