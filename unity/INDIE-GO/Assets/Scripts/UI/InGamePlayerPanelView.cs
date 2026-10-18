using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YutArena.Common;

namespace YutArena.UI
{
    /// <summary>
    /// 플레이어 한 명의 인게임 HUD 정보를 표시합니다.
    /// </summary>
    public sealed class InGamePlayerPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("선택한 캐릭터 이미지")]
        [SerializeField] private Image characterImage;
        [Tooltip("선택한 캐릭터 이름")]
        [SerializeField] private TMP_Text characterNameText;
        [Tooltip("패시브 스킬 이미지")]
        [SerializeField] private Image passiveSkillImage;
        [Tooltip("패시브 스킬 이름")]
        [SerializeField] private TMP_Text passiveSkillNameText;
        [Tooltip("액티브 스킬 이미지")]
        [SerializeField] private Image activeSkillImage;
        [Tooltip("액티브 스킬 이름")]
        [SerializeField] private TMP_Text activeSkillNameText;
        [Header("Victory Conditions")]
        [SerializeField] private GameObject escapeVictoryPanel;
        [SerializeField] private TMP_Text currentEscapeCountText;
        [SerializeField] private TMP_Text targetEscapeCountText;
        [SerializeField] private GameObject classicVictoryPanel;
        [SerializeField] private Image[] classicPieceImages;
        [Tooltip("클래식에서 아직 탈출하지 않은 말의 이미지 (1~4번)")]
        [SerializeField] private Sprite[] classicPendingSprites;
        [Tooltip("클래식에서 탈출한 말의 이미지 (1~4번)")]
        [SerializeField] private Sprite[] classicFinishedSprites;
        [Header("Available Pieces")]
        [Tooltip("게임 시작 시 플레이어의 말 개수만큼 복제할 버튼. 씬에서는 비활성화해 둡니다.")]
        [SerializeField] private Button availablePieceButtonTemplate;
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
        private PlayerController availablePiecePlayer;
        private readonly List<Button> availablePieceButtons = new List<Button>();
        private int[] pieceIdByButton;
        private bool[] hiddenPieceIds;

        public void Refresh(PlayerController player, GameStartSettings settings, int escapeScore)
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

            SetImage(passiveSkillImage, character != null ? character.passive_Icon : null);
            SetImage(activeSkillImage, character != null ? character.active_Icon : null);
            SetText(passiveSkillNameText, character != null ? character.passive_Name : string.Empty);
            SetText(activeSkillNameText, character != null ? character.active_Name : string.Empty);
            RefreshVictoryCondition(player, settings, escapeScore);
        }

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.enabled = sprite != null;
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

        public void RefreshVictoryCondition(PlayerController player, GameStartSettings settings, int escapeScore)
        {
            if (player == null || player.RuntimeData == null) return;

            RefreshAvailablePieces(player);

            bool isEscape = settings != null && settings.gameMode == GameMode.Escape;
            bool isClassic = settings != null && settings.gameMode == GameMode.Classic;
            if (escapeVictoryPanel != null) escapeVictoryPanel.SetActive(isEscape);
            if (classicVictoryPanel != null) classicVictoryPanel.SetActive(isClassic);

            if (isEscape)
            {
                SetText(currentEscapeCountText, Mathf.Max(0, escapeScore).ToString());
                SetText(targetEscapeCountText, settings.targetEscapeCount.ToString());
            }

            if (!isClassic || classicPieceImages == null) return;

            int finishedCount = 0;
            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
                if (piece.IsFinished) finishedCount++;

            for (int i = 0; i < classicPieceImages.Length; i++)
            {
                Image image = classicPieceImages[i];
                if (image == null) continue;
                Sprite[] sprites = i < finishedCount ? classicFinishedSprites : classicPendingSprites;
                Sprite sprite = sprites != null && i < sprites.Length ? sprites[i] : null;
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
        }

        private void RefreshAvailablePieces(PlayerController player)
        {
            if (availablePieceButtonTemplate == null) return;

            int pieceCount = player.RuntimeData.Pieces.Count;
            EnsurePieceButtonCount(pieceCount);

            if (availablePiecePlayer != player || hiddenPieceIds == null ||
                hiddenPieceIds.Length != pieceCount)
            {
                availablePiecePlayer = player;
                hiddenPieceIds = new bool[pieceCount];
            }
            if (pieceIdByButton == null || pieceIdByButton.Length != pieceCount)
                pieceIdByButton = new int[pieceCount];

            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
                if (piece.State != PieceState.Waiting)
                    hiddenPieceIds[piece.PieceId] = false;

            int index = 0;
            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            {
                if (piece.State != PieceState.Waiting || hiddenPieceIds[piece.PieceId]) continue;
                if (index >= availablePieceButtons.Count) break;

                int slot = index++;
                Button button = availablePieceButtons[slot];
                if (button == null) continue;
                pieceIdByButton[slot] = piece.PieceId;
                button.gameObject.SetActive(true);
                button.interactable = true;
                Image buttonImage = button.targetGraphic as Image;
                if (buttonImage != null)
                {
                    buttonImage.sprite = player.SelectedCharacter != null
                        ? player.SelectedCharacter.pieceImage : null;
                    buttonImage.preserveAspect = true;
                    buttonImage.color = Color.white;
                }
            }

            for (; index < availablePieceButtons.Count; index++)
                if (availablePieceButtons[index] != null)
                    availablePieceButtons[index].gameObject.SetActive(false);
        }

        private void EnsurePieceButtonCount(int pieceCount)
        {
            availablePieceButtonTemplate.gameObject.SetActive(false);
            while (availablePieceButtons.Count < pieceCount)
            {
                int slot = availablePieceButtons.Count;
                Button button = Instantiate(availablePieceButtonTemplate,
                    availablePieceButtonTemplate.transform.parent, false);
                button.name = $"Piece{slot + 1}Button";
                button.onClick.AddListener(() => HidePieceButton(slot));
                availablePieceButtons.Add(button);
            }

            while (availablePieceButtons.Count > pieceCount)
            {
                int last = availablePieceButtons.Count - 1;
                Button button = availablePieceButtons[last];
                availablePieceButtons.RemoveAt(last);
                if (button != null) Destroy(button.gameObject);
            }
        }

        private void HidePieceButton(int slot)
        {
            if (availablePiecePlayer == null || slot < 0 || slot >= pieceIdByButton.Length) return;
            int pieceId = pieceIdByButton[slot];
            if (pieceId < 0 || pieceId >= hiddenPieceIds.Length) return;
            hiddenPieceIds[pieceId] = true;
            RefreshAvailablePieces(availablePiecePlayer);
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
