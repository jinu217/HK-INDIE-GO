using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI.CharacterScene
{
    public class PlayerCharacterPanelView : MonoBehaviour
    {
        [Tooltip("플레이어 번호 텍스트")]
        [SerializeField] private TMP_Text playerText;
        [Tooltip("선택 상태 텍스트")]
        [SerializeField] private TMP_Text stateText;
        [Tooltip("캐릭터 아이콘")]
        [SerializeField] private Image characterIconImage;
        [Tooltip("캐릭터 이름 텍스트")]
        [SerializeField] private TMP_Text characterNameText;

        public void Refresh(int playerIndex, CharacterData data, bool isSelected)
        {
            SetText(playerText, $"{playerIndex + 1}P");
            SetText(stateText, isSelected ? "선택 완료" : "선택 중");
            SetText(characterNameText, data != null ? data.char_Name : string.Empty);

            if (characterIconImage != null)
            {
                characterIconImage.sprite = data != null ? data.char_Icon : null;
                characterIconImage.enabled = characterIconImage.sprite != null;
            }

        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
