using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI
{
    /// <summary>
    /// 패시브 또는 액티브 스킬 하나의 상세 정보를 표시하는 패널입니다.
    /// </summary>
    public sealed class SkillDetailPanelView : MonoBehaviour
    {
        [SerializeField] private Image skillImage;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private TMP_Text skillDescriptionText;

        public void Show(Sprite icon, string skillName, string description)
        {
            if (skillImage != null)
            {
                skillImage.sprite = icon;
                skillImage.enabled = icon != null;
            }

            SetText(skillNameText, skillName);
            SetText(skillDescriptionText, description);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }
    }
}
