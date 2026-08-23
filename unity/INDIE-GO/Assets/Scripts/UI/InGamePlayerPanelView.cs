using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YutArena.UI
{
    /// <summary>
    /// 플레이어 한 명의 인게임 HUD 정보를 표시합니다.
    /// </summary>
    public sealed class InGamePlayerPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string EscapePrefix = "ESCAPED ";

        [Tooltip("선택한 캐릭터 이미지")]
        [SerializeField] private Image characterImage;
        [Tooltip("선택한 캐릭터 이름")]
        [SerializeField] private TMP_Text characterNameText;
        [Tooltip("탈출한 말 수")]
        [SerializeField] private TMP_Text escapeCountText;
        [Tooltip("현재 차례일 때만 활성화할 테두리 또는 표식")]
        [SerializeField] private GameObject currentTurnHighlight;
        [Tooltip("다음 차례일 때만 활성화할 테두리 또는 표식")]
        [SerializeField] private GameObject nextTurnHighlight;
        [Tooltip("이 플레이어의 패시브/액티브 상세 정보를 함께 표시할 공용 패널")]
        [SerializeField] private CharacterSkillDetailPanelView skillDetailPanel;
        [Min(0f)]
        [Tooltip("마우스를 올린 뒤 스킬 상세 패널이 표시될 때까지의 시간(초)")]
        [SerializeField] private float hoverDelaySeconds = 1f;

        private CharacterData currentCharacter;
        private Coroutine hoverCoroutine;

        public void Refresh(PlayerController player, int targetEscapeCount)
        {
            if (player == null || player.RuntimeData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            CharacterData character = player.SelectedCharacter;
            currentCharacter = character;
            SetText(
                characterNameText,
                character != null && !string.IsNullOrWhiteSpace(character.char_Name)
                    ? character.char_Name
                    : player.RuntimeData.PlayerName);

            if (characterImage != null)
            {
                characterImage.sprite = character != null ? character.char_Icon : null;
                characterImage.enabled = characterImage.sprite != null;
            }

            RefreshEscapeCount(player, targetEscapeCount);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentCharacter == null || skillDetailPanel == null) return;

            CancelHover();
            hoverCoroutine = StartCoroutine(ShowSkillDetailAfterDelay());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHover();
            if (skillDetailPanel != null) skillDetailPanel.Hide();
        }

        private void OnDisable()
        {
            CancelHover();
            if (skillDetailPanel != null) skillDetailPanel.Hide();
        }

        public void RefreshEscapeCount(PlayerController player, int targetEscapeCount)
        {
            if (player == null || player.RuntimeData == null) return;

            int escapedCount = 0;
            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            {
                if (piece.IsFinished) escapedCount++;
            }

            SetText(escapeCountText, EscapePrefix + escapedCount + "/" + targetEscapeCount);
        }

        public void SetTurnState(bool isCurrentTurn, bool isNextTurn)
        {
            // 현재 차례가 최우선입니다. 현재/다음 차례가 아니면 둘 다 꺼집니다.
            bool showCurrentHighlight = isCurrentTurn;
            bool showNextHighlight = !isCurrentTurn && isNextTurn;

            if (currentTurnHighlight != null)
                currentTurnHighlight.SetActive(showCurrentHighlight);
            if (nextTurnHighlight != null)
                nextTurnHighlight.SetActive(showNextHighlight);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }

        private IEnumerator ShowSkillDetailAfterDelay()
        {
            yield return new WaitForSecondsRealtime(hoverDelaySeconds);
            hoverCoroutine = null;

            if (skillDetailPanel != null && currentCharacter != null)
                skillDetailPanel.Show(currentCharacter);
        }

        private void CancelHover()
        {
            if (hoverCoroutine == null) return;

            StopCoroutine(hoverCoroutine);
            hoverCoroutine = null;
        }

    }
}
