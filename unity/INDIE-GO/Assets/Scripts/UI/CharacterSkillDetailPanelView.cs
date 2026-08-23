using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI
{
    /// <summary>
    /// 캐릭터의 패시브와 액티브 스킬 상세 정보를 한 패널에 함께 표시합니다.
    /// </summary>
    public sealed class CharacterSkillDetailPanelView : MonoBehaviour
    {
        [Header("Passive Skill")]
        [SerializeField] private Image passiveSkillImage;
        [SerializeField] private TMP_Text passiveSkillNameText;
        [SerializeField] private TMP_Text passiveSkillDescriptionText;

        [Header("Active Skill")]
        [SerializeField] private Image activeSkillImage;
        [SerializeField] private TMP_Text activeSkillNameText;
        [SerializeField] private TMP_Text activeSkillDescriptionText;

        public void Show(CharacterData character)
        {
            if (character == null)
            {
                Hide();
                return;
            }

            SetImage(passiveSkillImage, character.passive_Icon);
            SetText(passiveSkillNameText, character.passive_Name);
            SetText(passiveSkillDescriptionText, character.passive_Desc);

            SetImage(activeSkillImage, character.active_Icon);
            SetText(activeSkillNameText, character.active_Name);
            SetText(activeSkillDescriptionText, character.active_Desc);

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null) return;

            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }
    }
}
