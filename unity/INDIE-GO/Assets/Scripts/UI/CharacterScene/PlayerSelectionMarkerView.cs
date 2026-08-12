using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI.CharacterScene
{
    public class PlayerSelectionMarkerView : MonoBehaviour
    {
        [Tooltip("플레이어 테두리")]
        [SerializeField] private Image frameImage;
        [Tooltip("플레이어 색상")]
        [SerializeField] private Color playerColor = Color.white;
        [Tooltip("플레이어 표시 오브젝트")]
        [SerializeField] private GameObject playerObject;

        private RectTransform rectTransform;
        private RectTransform playerObjectRectTransform;
        private Vector2 playerObjectOrigin;

        private void Awake()
        {
            rectTransform = transform as RectTransform;

            if (playerObject != null)
            {
                playerObjectRectTransform = playerObject.transform as RectTransform;

                if (playerObjectRectTransform != null)
                {
                    playerObjectOrigin = playerObjectRectTransform.anchoredPosition;
                }
            }
        }

        public void Initialize(int playerIndex)
        {
            if (frameImage != null)
            {
                frameImage.color = playerColor;
            }
        }

        public void MoveTo(
            RectTransform target,
            bool showFrame,
            int sameCardOrder,
            float playerObjectSpacing)
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

            gameObject.SetActive(true);
            rectTransform.SetParent(target, false);
            rectTransform.SetAsLastSibling();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            if (frameImage != null)
            {
                frameImage.enabled = showFrame;
                frameImage.color = playerColor;
            }

            if (playerObject != null)
            {
                playerObject.SetActive(true);
            }

            if (playerObjectRectTransform != null)
            {
                playerObjectRectTransform.anchoredPosition =
                    playerObjectOrigin + Vector2.right * (sameCardOrder * playerObjectSpacing);
            }
        }

    }
}
