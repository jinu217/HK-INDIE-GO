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
        [Tooltip("게임 시작 시 플레이어의 말 개수만큼 복제할 이미지. 씬에서는 비활성화해 둡니다.")]
        [SerializeField] private Image availablePieceImageTemplate;
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
        private readonly List<Image> availablePieceImages = new List<Image>();
        private PlayerRuntimeData availablePieceRuntimeData;

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

            RefreshAvailablePieceImages(player);

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

        private void RefreshAvailablePieceImages(PlayerController player)
        {
            if (availablePieceImageTemplate == null) return;

            int visibleCount;
            if (availablePieceRuntimeData != player.RuntimeData)
            {
                // 새 게임의 첫 표시에는 로비에서 정한 시작 말 수를 사용합니다.
                availablePieceRuntimeData = player.RuntimeData;
                visibleCount = player.RuntimeData.Pieces.Count;
            }
            else
            {
                // 이후에는 아직 판에 올라가지 않은 대기 말만 표시합니다.
                visibleCount = 0;
                foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
                    if (piece.State == PieceState.Waiting) visibleCount++;
            }
            EnsurePieceImageCount(visibleCount);
            Sprite pieceSprite = player.SelectedCharacter != null
                ? player.SelectedCharacter.pieceImage : null;
            foreach (Image image in availablePieceImages)
            {
                if (image == null) continue;
                image.sprite = pieceSprite;
                image.enabled = pieceSprite != null;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
                image.gameObject.SetActive(true);
            }
        }

        private void EnsurePieceImageCount(int pieceCount)
        {
            availablePieceImageTemplate.gameObject.SetActive(false);
            while (availablePieceImages.Count < pieceCount)
            {
                int index = availablePieceImages.Count;
                Image image = Instantiate(availablePieceImageTemplate,
                    availablePieceImageTemplate.transform.parent, false);
                image.name = $"Piece{index + 1}Image";
                availablePieceImages.Add(image);
            }

            while (availablePieceImages.Count > pieceCount)
            {
                int last = availablePieceImages.Count - 1;
                Image image = availablePieceImages[last];
                availablePieceImages.RemoveAt(last);
                if (image != null)
                {
                    image.gameObject.SetActive(false);
                    Destroy(image.gameObject);
                }
            }
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
