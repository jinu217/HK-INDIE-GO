using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI.CharacterScene
{
    public class PlayerSelectionMarkerView : MonoBehaviour
    {
        [Tooltip("플레이어 테두리")]
        [SerializeField] private Image frameImage;
        [Tooltip("플레이어 번호 이미지 (1PImage, 2PImage 등)")]
        [SerializeField] private Image playerImage;
        private RectTransform rectTransform;
        private Vector2 markerSize;
        private Sprite originalPlayerSprite;
        private bool originalPlayerSpriteCaptured;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            if (rectTransform != null && markerSize == Vector2.zero)
                markerSize = rectTransform.sizeDelta;

        }

        public void SetTeamImage(bool isTeamMode, int teamIndex, Sprite redTeamSprite, Sprite blueTeamSprite)
        {
            if (playerImage == null) return;

            if (!originalPlayerSpriteCaptured)
            {
                originalPlayerSprite = playerImage.sprite;
                originalPlayerSpriteCaptured = true;
            }

            Sprite teamSprite = teamIndex == 1 ? redTeamSprite : teamIndex == 2 ? blueTeamSprite : null;
            playerImage.sprite = isTeamMode && teamSprite != null ? teamSprite : originalPlayerSprite;
        }

        public void MoveTo(
            RectTransform target,
            bool showFrame)
        {
            if (target == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }
            if (markerSize == Vector2.zero)
                markerSize = rectTransform.sizeDelta;

            gameObject.SetActive(true);
            rectTransform.SetParent(target, false);
            rectTransform.SetAsLastSibling();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = markerSize;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            if (frameImage != null)
            {
                frameImage.enabled = showFrame;
            }

        }

    }
}
