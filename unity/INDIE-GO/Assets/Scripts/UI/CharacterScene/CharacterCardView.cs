using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YutArena.UI.CharacterScene
{
    public class CharacterCardView : MonoBehaviour
    {
        [Tooltip("캐릭터 초상화")]
        [SerializeField] private Image portraitImage;
        [Tooltip("마커 기준 위치")]
        [SerializeField] private RectTransform markerTarget;
        [Tooltip("Character name shown when no portrait asset is available.")]
        [SerializeField] private TMP_Text characterNameText;

        public RectTransform MarkerTarget => markerTarget != null
            ? markerTarget
            : transform as RectTransform;

        public void SetCharacter(CharacterData characterData)
        {
            if (characterNameText == null)
                characterNameText = GetComponentInChildren<TMP_Text>(true);

            if (characterNameText != null)
                characterNameText.text = characterData != null
                    ? characterData.char_Name
                    : string.Empty;

            if (portraitImage == null)
            {
                return;
            }

            portraitImage.sprite = characterData != null ? characterData.char_Icon : null;
            // Keep the card background visible even when portrait production
            // is incomplete; the character name remains selectable.
            portraitImage.enabled = true;
            if (portraitImage.sprite == null)
                portraitImage.color = new Color(.18f, .22f, .3f, 1f);
            else
                portraitImage.color = Color.white;
        }

    }
}
