using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI.CharacterScene
{
    public class LoadingPlayerEntryView : MonoBehaviour
    {
        [SerializeField] private Image playerMarkerImage;
        [SerializeField] private TMP_Text playerMarkerText;
        [SerializeField] private Image characterIllustrationImage;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private Image activeSkillImage;
        [SerializeField] private Image passiveSkillImage;

        public void Refresh(CharacterData character, PlayerSelectionMarkerView marker)
        {
            if (characterNameText != null)
                characterNameText.text = character != null ? character.char_Name : string.Empty;
            SetImage(characterIllustrationImage, character != null ? character.illustrationImage : null);
            SetImage(activeSkillImage, character != null ? character.active_Icon : null);
            SetImage(passiveSkillImage, character != null ? character.passive_Icon : null);

            if (marker == null) return;
            if (playerMarkerImage != null && marker.PlayerImage != null)
            {
                playerMarkerImage.sprite = marker.PlayerImage.sprite;
                playerMarkerImage.color = marker.PlayerImage.color;
            }
            if (playerMarkerText != null && marker.PlayerText != null)
            {
                playerMarkerText.text = marker.PlayerText.text;
                playerMarkerText.color = marker.PlayerText.color;
            }
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }
}
