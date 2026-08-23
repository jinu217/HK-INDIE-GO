using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YutArena.UI
{
    /// <summary>
    /// 포인터가 1초 동안 머물면 연결된 스킬 상세 패널을 표시합니다.
    /// </summary>
    public sealed class SkillTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SkillDetailPanelView detailPanel;
        [Min(0f)]
        [Tooltip("마우스를 올린 뒤 스킬 상세 패널이 표시될 때까지의 시간(초)")]
        [SerializeField] private float hoverDelaySeconds = 1f;

        private Coroutine hoverCoroutine;
        private Sprite skillIcon;
        private string skillName;
        private string skillDescription;
        private bool hasSkillData;

        public void Configure(Sprite icon, string name, string description)
        {
            CancelHover();
            if (detailPanel != null) detailPanel.Hide();

            skillIcon = icon;
            skillName = name;
            skillDescription = description;
            hasSkillData = icon != null ||
                           !string.IsNullOrWhiteSpace(name) ||
                           !string.IsNullOrWhiteSpace(description);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hasSkillData || detailPanel == null) return;

            CancelHover();
            hoverCoroutine = StartCoroutine(ShowAfterDelay());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHover();
            if (detailPanel != null) detailPanel.Hide();
        }

        private void OnDisable()
        {
            CancelHover();
            if (detailPanel != null) detailPanel.Hide();
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(hoverDelaySeconds);
            hoverCoroutine = null;

            if (detailPanel != null)
                detailPanel.Show(skillIcon, skillName, skillDescription);
        }

        private void CancelHover()
        {
            if (hoverCoroutine == null) return;

            StopCoroutine(hoverCoroutine);
            hoverCoroutine = null;
        }
    }
}
