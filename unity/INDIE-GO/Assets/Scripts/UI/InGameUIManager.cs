using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.UI
{
    /// <summary>
    /// 인게임 HUD에 플레이어 정보, 게임/턴 제한시간, 현재 턴 수를 표시합니다.
    /// Player Panels는 Player 1부터 순서대로 등록해야 합니다.
    /// </summary>
    public sealed class InGameUIManager : MonoBehaviour
    {
        private const string GameTimePrefix = "GAME TIME \n";
        private const string TurnTimePrefix = "TURN TIME \n";
        private const string TurnCountPrefix = "TURN ";
        private const string UnlimitedText = "UNLIMITED";

        [Header("Managers")]
        [SerializeField] private TestGameManager gameManager;
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private PlayerManager playerManager;

        [Header("Player Panels (Player 1부터 순서대로)")]
        [SerializeField] private InGamePlayerPanelView[] playerPanels;

        [Header("Game Info")]
        [SerializeField] private TMP_Text gameTimeText;
        [SerializeField] private TMP_Text turnTimeText;
        [SerializeField] private TMP_Text turnCountText;

        [Header("Current Player Skills")]
        [Tooltip("현재 턴 플레이어 캐릭터의 패시브 스킬 이미지")]
        [SerializeField] private Image passiveSkillImage;
        [Tooltip("현재 턴 플레이어 캐릭터의 액티브 스킬 이미지")]
        [SerializeField] private Image activeSkillImage;
        [Tooltip("씬에 직접 만든 공용 액티브 스킬 버튼")]
        [SerializeField] private ActiveSkillButtonController activeSkillButtonController;
        [Tooltip("패시브 스킬 이미지에 붙인 Hover 트리거")]
        [SerializeField] private SkillTooltipTrigger passiveSkillTooltip;
        [Tooltip("액티브 스킬 버튼에 붙인 Hover 트리거")]
        [SerializeField] private SkillTooltipTrigger activeSkillTooltip;

        private void Awake()
        {
            if (gameManager == null) gameManager = TestGameManager.Instance;
            if (gameManager == null) gameManager = FindFirstObjectByType<TestGameManager>();
            if (turnManager == null) turnManager = FindFirstObjectByType<TestTurnManager>();
            if (playerManager == null) playerManager = FindFirstObjectByType<PlayerManager>();
        }

        private void OnEnable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted += HandleTurnStarted;
                turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted -= HandleTurnStarted;
                turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            }
        }

        private void Update()
        {
            RefreshTimers();
            RefreshEscapeCounts();
        }

        public void RefreshAll()
        {
            RefreshPlayerPanels();
            RefreshTurnInfo();
            RefreshTimers();
            RefreshCurrentPlayerSkills();
        }

        private void HandleTurnStarted(PlayerSlot player)
        {
            // PlayerManager.SetupPlayers 직후 첫 턴 이벤트가 오므로 이 시점에
            // 캐릭터 선택 결과까지 포함해 패널 전체를 다시 채웁니다.
            RefreshPlayerPanels();
            RefreshTurnHighlights(player);
            RefreshTurnInfo();
            RefreshCurrentPlayerSkills(player);
        }

        private void HandleTurnPhaseChanged(TurnContext turn)
        {
            RefreshTurnInfo();
        }

        private void RefreshPlayerPanels()
        {
            if (playerPanels == null) return;

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
                panel.Refresh(player, GetEscapeTarget());
            }

            PlayerSlot currentPlayer = turnManager != null && turnManager.CurrentTurn != null
                ? turnManager.CurrentTurn.currentPlayer
                : PlayerSlot.None;
            RefreshTurnHighlights(currentPlayer);
            RefreshEscapeCounts();
        }

        private void RefreshEscapeCounts()
        {
            if (playerPanels == null || playerManager == null) return;

            int target = GetEscapeTarget();
            int count = Mathf.Min(playerPanels.Length, playerManager.ActivePlayers.Count);
            for (int i = 0; i < count; i++)
            {
                InGamePlayerPanelView panel = playerPanels[i];
                if (panel == null) continue;
                panel.RefreshEscapeCount(playerManager.ActivePlayers[i], target);
            }
        }

        private int GetEscapeTarget()
        {
            GameStartSettings settings = GetSettings();
            if (settings == null) return 0;

            return settings.gameMode == GameMode.Escape
                ? settings.targetEscapeCount
                : settings.pieceCountPerPlayer;
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
            if (turnCountText == null) return;

            GameStartSettings settings = GetSettings();
            int current = turnManager != null && turnManager.CurrentTurn != null
                ? turnManager.CurrentTurn.turnNumber
                : 0;
            int maximum = settings != null ? settings.maxTurnCount : 0;

            turnCountText.text = maximum > 0
                ? TurnCountPrefix + current + "/" + maximum
                : TurnCountPrefix + current + "/" + UnlimitedText;
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

            SetSkillImage(passiveSkillImage, character != null ? character.passive_Icon : null);
            SetSkillImage(activeSkillImage, character != null ? character.active_Icon : null);

            if (passiveSkillTooltip != null)
            {
                passiveSkillTooltip.Configure(
                    character != null ? character.passive_Icon : null,
                    character != null ? character.passive_Name : string.Empty,
                    character != null ? character.passive_Desc : string.Empty);
            }

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
            if (gameTimeText != null)
            {
                gameTimeText.text = GameTimePrefix +
                                    (gameManager != null && gameManager.IsGameTimerLimited
                                        ? FormatTime(gameManager.RemainingGameSeconds)
                                        : UnlimitedText);
            }

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
