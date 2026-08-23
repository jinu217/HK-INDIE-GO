using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI
{
    /// <summary>
    /// 플레이어 한 명의 인게임 HUD 정보를 표시합니다.
    /// </summary>
    public sealed class InGamePlayerPanelView : MonoBehaviour
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

        public void Refresh(PlayerController player, int targetEscapeCount)
        {
            if (player == null || player.RuntimeData == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            CharacterData character = player.SelectedCharacter;
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
    }
}
