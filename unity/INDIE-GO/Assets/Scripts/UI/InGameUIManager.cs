using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.UI
{
    /// <summary>
    /// 인게임 HUD에 플레이어 정보, 턴 제한시간, 현재 턴 수를 표시합니다.
    /// Player Panels는 Player 1부터 순서대로 등록해야 합니다.
    /// </summary>
    public sealed class InGameUIManager : MonoBehaviour
    {
        private const string TurnTimePrefix = "TURN TIME \n";
        private const string UnlimitedText = "UNLIMITED";

        [Header("Managers")]
        [SerializeField] private TestGameManager gameManager;
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private TestWinConditionManager winConditionManager;

        [Header("Player Panels (Player 1부터 순서대로)")]
        [SerializeField] private InGamePlayerPanelView[] playerPanels;

        [Header("Game Info")]
        [SerializeField] private TMP_Text turnTimeText;
        [Tooltip("현재 턴(라운드) 숫자")]
        [SerializeField] private TMP_Text turnCountText;
        [Tooltip("최대 턴 숫자. 제한이 없으면 UNLIMITED 표시")]
        [SerializeField] private TMP_Text maxTurnCountText;

        [Header("Current Player Skills")]
        [Tooltip("현재 턴 플레이어 캐릭터의 액티브 스킬 이미지")]
        [SerializeField] private Image activeSkillImage;
        [Tooltip("씬에 직접 만든 공용 액티브 스킬 버튼")]
        [SerializeField] private ActiveSkillButtonController activeSkillButtonController;
        [Tooltip("액티브 스킬 버튼에 붙인 Hover 트리거")]
        [SerializeField] private SkillTooltipTrigger activeSkillTooltip;

        [Header("Current Player SP")]
        [Tooltip("SP 이미지를 나열할 빈 UI 컨테이너 (Layout Group 없이 사용)")]
        [SerializeField] private RectTransform spContainer;
        [Tooltip("현재 보유한 SP를 표시할 이미지")]
        [SerializeField] private Sprite spSprite;
        [Tooltip("아직 채워지지 않은 SP를 표시할 이미지")]
        [SerializeField] private Sprite emptySpSprite;
        [Tooltip("SP 이미지 한 개의 표시 크기")]
        [SerializeField] private Vector2 spImageSize = new Vector2(32f, 32f);
        [Tooltip("SP 이미지 사이 간격")]
        [SerializeField, Min(0f)] private float spSpacing = 3f;

        private readonly List<Image> spImages = new List<Image>();

        private void Awake()
        {
            if (gameManager == null) gameManager = TestGameManager.Instance;
            if (gameManager == null) gameManager = FindFirstObjectByType<TestGameManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<TestTurnManager>();
            if (playerManager == null) playerManager = FindFirstObjectByType<PlayerManager>();
            if (winConditionManager == null) winConditionManager = FindFirstObjectByType<TestWinConditionManager>();
        }

        private void OnEnable()
        {
            CharacterSkillRegistry.SkillPointChanged += HandleSkillPointChanged;
            if (turnManager != null)
            {
                turnManager.OnTurnStarted += HandleTurnStarted;
                turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            CharacterSkillRegistry.SkillPointChanged -= HandleSkillPointChanged;
            foreach (Image image in spImages)
                if (image != null) image.gameObject.SetActive(false);
            if (turnManager != null)
            {
                turnManager.OnTurnStarted -= HandleTurnStarted;
                turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            }
        }

        private void Update()
        {
            RefreshTimers();
            RefreshVictoryConditions();
        }

        public void RefreshAll()
        {
            RefreshPlayerPanels();
            RefreshTurnInfo();
            RefreshTimers();
            RefreshCurrentPlayerSkills();
            RefreshCurrentPlayerSP();
        }

        private void HandleTurnStarted(PlayerSlot player)
        {
            // PlayerManager.SetupPlayers 직후 첫 턴 이벤트가 오므로 이 시점에
            // 캐릭터 선택 결과까지 포함해 패널 전체를 다시 채웁니다.
            RefreshPlayerPanels();
            RefreshTurnHighlights(player);
            RefreshTurnInfo();
            RefreshCurrentPlayerSkills(player);
            RefreshCurrentPlayerSP();
        }

        private void HandleTurnPhaseChanged(TurnContext turn)
        {
            RefreshTurnInfo();
        }

        private void HandleSkillPointChanged(SkillPointChange change)
        {
            if (turnManager != null && turnManager.CurrentTurn != null &&
                change.PlayerId == (int)turnManager.CurrentTurn.currentPlayer)
                RefreshCurrentPlayerSP();
        }

        private void RefreshCurrentPlayerSP()
        {
            if (spContainer == null || spSprite == null)
            {
                foreach (Image image in spImages)
                    if (image != null) image.gameObject.SetActive(false);
                return;
            }
            int playerId = turnManager != null && turnManager.CurrentTurn != null
                ? (int)turnManager.CurrentTurn.currentPlayer : 0;
            int count = CharacterSkillRegistry.GetSkillPoints(playerId);
            int maximum = CharacterSkillRegistry.GetMaxSkillPoints(playerId);
            RefreshSPImages(count, maximum);
        }

        private void RefreshSPImages(int count, int maximum)
        {
            maximum = Mathf.Max(0, maximum);
            count = Mathf.Clamp(count, 0, maximum);
            Vector2 size = new Vector2(Mathf.Max(1f, spImageSize.x), Mathf.Max(1f, spImageSize.y));
            while (spImages.Count < maximum)
            {
                GameObject icon = new GameObject($"SP_{spImages.Count + 1}", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(spContainer, false);
                Image image = icon.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                spImages.Add(image);
            }
            for (int i = 0; i < spImages.Count; i++)
            {
                Image image = spImages[i];
                if (image == null) continue;
                bool filled = i < count;
                image.sprite = filled || emptySpSprite == null ? spSprite : emptySpSprite;
                image.color = !filled && emptySpSprite == null
                    ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
                image.gameObject.SetActive(i < maximum);
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2(i * (size.x + spSpacing), 0f);
            }
        }

        private void OnDestroy()
        {
            foreach (Image image in spImages)
                if (image != null) Destroy(image.gameObject);
        }

        private void RefreshPlayerPanels()
        {
            if (playerPanels == null) return;

            GameStartSettings settings = GetSettings();
            int activeCount = playerManager != null ? playerManager.ActivePlayers.Count : 0;
            for (int i = 0; i < playerPanels.Length; i++)
            {
                InGamePlayerPanelView panel = playerPanels[i];
                if (panel == null) continue;

                bool isActive = i < activeCount;
                if (!isActive)
                {
                    panel.gameObject.SetActive(false);
                    continue;
                }

                PlayerController player = playerManager.ActivePlayers[i];
                panel.Refresh(player, settings, GetEscapeScore(settings, player));
            }

            PlayerSlot currentPlayer = turnManager != null && turnManager.CurrentTurn != null
                ? turnManager.CurrentTurn.currentPlayer
                : PlayerSlot.None;
            RefreshTurnHighlights(currentPlayer);
            RefreshVictoryConditions();
        }

        private void RefreshVictoryConditions()
        {
            if (playerPanels == null || playerManager == null) return;

            GameStartSettings settings = GetSettings();
            int count = Mathf.Min(playerPanels.Length, playerManager.ActivePlayers.Count);
            for (int i = 0; i < count; i++)
            {
                InGamePlayerPanelView panel = playerPanels[i];
                if (panel == null) continue;
                PlayerController player = playerManager.ActivePlayers[i];
                panel.RefreshVictoryCondition(player, settings, GetEscapeScore(settings, player));
            }
        }

        private int GetEscapeScore(GameStartSettings settings, PlayerController player)
        {
            if (settings == null || player == null || winConditionManager == null ||
                settings.gameMode != GameMode.Escape) return 0;
            TeamSlot team = MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)player.PlayerId);
            return winConditionManager.GetEscapeCount(team);
        }

        private void RefreshTurnHighlights(PlayerSlot currentPlayer)
        {
            if (playerPanels == null) return;

            // 이전 턴의 표시를 모두 끈 뒤 현재/다음 플레이어만 다시 켭니다.
            for (int i = 0; i < playerPanels.Length; i++)
            {
                InGamePlayerPanelView panel = playerPanels[i];
                if (panel != null) panel.SetTurnState(false, false);
            }

            SetPlayerTurnState((int)currentPlayer, true, false);
            SetPlayerTurnState(GetNextPlayerId(currentPlayer), false, true);
        }

        private void SetPlayerTurnState(int playerId, bool isCurrentTurn, bool isNextTurn)
        {
            int panelIndex = playerId - 1;
            if (panelIndex < 0 || panelIndex >= playerPanels.Length) return;

            InGamePlayerPanelView panel = playerPanels[panelIndex];
            if (panel != null) panel.SetTurnState(isCurrentTurn, isNextTurn);
        }

        private int GetNextPlayerId(PlayerSlot currentPlayer)
        {
            if (turnManager == null || turnManager.CurrentTurn == null || turnManager.TurnOrder == null ||
                turnManager.TurnOrder.order == null || turnManager.TurnOrder.order.Count <= 1)
                return -1;

            int currentIndex = turnManager.TurnOrder.order.IndexOf(currentPlayer);
            if (currentIndex < 0) return -1;

            int nextIndex = (currentIndex + 1) % turnManager.TurnOrder.order.Count;
            return (int)turnManager.TurnOrder.order[nextIndex];
        }

        private void RefreshTurnInfo()
        {
            if (turnCountText == null && maxTurnCountText == null) return;

            GameStartSettings settings = GetSettings();
            int current = turnManager != null && turnManager.CurrentTurn != null
                ? turnManager.CurrentTurn.roundNumber // 턴 수 = 전원 1바퀴(라운드) 기준
                : 0;
            int maximum = settings != null ? settings.maxTurnCount : 0;

            if (turnCountText != null)
                turnCountText.text = current.ToString();
            if (maxTurnCountText != null)
                maxTurnCountText.text = maximum > 0 ? maximum.ToString() : UnlimitedText;
        }

        private void RefreshCurrentPlayerSkills()
        {
            PlayerSlot currentPlayer = turnManager != null && turnManager.CurrentTurn != null
                ? turnManager.CurrentTurn.currentPlayer
                : PlayerSlot.None;
            RefreshCurrentPlayerSkills(currentPlayer);
        }

        private void RefreshCurrentPlayerSkills(PlayerSlot currentPlayer)
        {
            CharacterData character = null;
            if (playerManager != null &&
                playerManager.TryGetPlayer((int)currentPlayer, out PlayerController player))
            {
                character = player.SelectedCharacter;
            }

            SetSkillImage(activeSkillImage, character != null ? character.active_Icon : null);

            if (activeSkillTooltip != null)
            {
                activeSkillTooltip.Configure(
                    character != null ? character.active_Icon : null,
                    character != null ? character.active_Name : string.Empty,
                    character != null ? character.active_Desc : string.Empty);
            }

            if (activeSkillButtonController != null)
                activeSkillButtonController.RefreshForCurrentTurn();
        }

        private void RefreshTimers()
        {
            if (turnTimeText != null)
            {
                turnTimeText.text = TurnTimePrefix +
                                    (turnManager != null && turnManager.IsTurnTimerLimited
                                        ? FormatTime(turnManager.RemainingTurnSeconds)
                                        : UnlimitedText);
            }
        }

        private GameStartSettings GetSettings()
        {
            if (gameManager != null && gameManager.Settings != null) return gameManager.Settings;
            return turnManager != null ? turnManager.Settings : null;
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
        }

        private static void SetSkillImage(Image target, Sprite sprite)
        {
            if (target == null) return;

            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }
}
