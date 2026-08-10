using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI.CharacterScene
{
    public class CharacterCardView : MonoBehaviour
    {
        [Tooltip("캐릭터 초상화")]
        [SerializeField] private Image portraitImage;
        [Tooltip("마커 기준 위치")]
        [SerializeField] private RectTransform markerTarget;

        public RectTransform MarkerTarget => markerTarget != null
            ? markerTarget
            : transform as RectTransform;

        public void SetCharacter(CharacterData characterData)
        {
            if (portraitImage == null)
            {
                return;
            }

            portraitImage.sprite = characterData != null ? characterData.char_Icon : null;
            portraitImage.enabled = portraitImage.sprite != null;
        }

    }
}
