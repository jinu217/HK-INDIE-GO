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
        [Tooltip("액티브 스킬 이미지")]
        [SerializeField] private Image activeSkillImage;
        [Tooltip("액티브 스킬 이름")]
        [SerializeField] private TMP_Text activeSkillNameText;
        [Tooltip("패시브 스킬 이미지")]
        [SerializeField] private Image passiveSkillImage;
        [Tooltip("패시브 스킬 이름")]
        [SerializeField] private TMP_Text passiveSkillNameText;

        public void Refresh(int playerIndex, CharacterData data, bool isSelected)
        {
            SetText(playerText, $"{playerIndex + 1}P");
            SetText(stateText, isSelected ? "선택 완료" : "선택 중");
            SetText(characterNameText, data != null ? data.char_Name : string.Empty);
            SetText(activeSkillNameText, data != null ? data.active_Name : string.Empty);
            SetText(passiveSkillNameText, data != null ? data.passive_Name : string.Empty);

            SetImage(characterIconImage, data != null ? data.char_Icon : null);
            SetImage(activeSkillImage, data != null ? data.active_Icon : null);
            SetImage(passiveSkillImage, data != null ? data.passive_Icon : null);

        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        public void RefreshRandom(int playerIndex, bool isSelected, Sprite questionSprite)
        {
            SetText(playerText, $"{playerIndex + 1}P");
            SetText(stateText, isSelected ? "선택 완료" : "선택 중");
            SetText(characterNameText, "?");
            SetText(activeSkillNameText, "?");
            SetText(passiveSkillNameText, "?");
            SetImage(characterIconImage, questionSprite);
            SetImage(activeSkillImage, questionSprite);
            SetImage(passiveSkillImage, questionSprite);
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }
}
