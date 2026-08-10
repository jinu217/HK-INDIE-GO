using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutArena.Common;

namespace YutArena.UI
{
    public class LobbySceneUIController : MonoBehaviour
    {
        private const int MaxTeamSlotCount = 4;

        private readonly GameMode[] gameModeOptions =
        {
            GameMode.Classic,
            GameMode.Escape,
            GameMode.KillTheKing
        };

        private readonly string[] gameModeLabels =
        {
            "Classic",
            "Escape",
            "Kill The King"
        };

        private readonly MapType[] mapOptions =
        {
            MapType.Random,
            MapType.Basic,
            MapType.Korean,
            MapType.Grassland
        };

        private readonly string[] mapLabels =
        {
            "Random",
            "Basic",
            "Korean",
            "Grassland"
        };

        private readonly string[] teamLabels =
        {
            "No Team",
            "Blue",
            "Yellow",
            "Red",
            "Green"
        };

        [Header("Mode")]
        [Tooltip("Game Mode Left")]
        [SerializeField] private Button gameModeLeftButton;
        [Tooltip("Game Mode Right")]
        [SerializeField] private Button gameModeRightButton;
        [Tooltip("Game Mode Value")]
        [SerializeField] private TMP_Text gameModeValueText;

        [Header("Players")]
        [Tooltip("Player Count Left")]
        [SerializeField] private Button playerCountLeftButton;
        [Tooltip("Player Count Right")]
        [SerializeField] private Button playerCountRightButton;
        [Tooltip("Player Count Value")]
        [SerializeField] private TMP_Text playerCountValueText;
        [Tooltip("Team Mode Left")]
        [SerializeField] private Button teamModeLeftButton;
        [Tooltip("Team Mode Right")]
        [SerializeField] private Button teamModeRightButton;
        [Tooltip("Team Mode Value")]
        [SerializeField] private TMP_Text teamModeValueText;
        [Tooltip("Player Rows")]
        [SerializeField] private GameObject[] playerRows;
        [Tooltip("Player Team Left Buttons")]
        [SerializeField] private Button[] playerTeamLeftButtons;
        [Tooltip("Player Team Right Buttons")]
        [SerializeField] private Button[] playerTeamRightButtons;
        [Tooltip("Player Team Value Texts")]
        [SerializeField] private TMP_Text[] playerTeamValueTexts;

        [Header("Map")]
        [Tooltip("Map Left")]
        [SerializeField] private Button mapLeftButton;
        [Tooltip("Map Right")]
        [SerializeField] private Button mapRightButton;
        [Tooltip("Map Value")]
        [SerializeField] private TMP_Text mapValueText;

        [Header("Turn")]
        [Tooltip("Turn Length Left")]
        [SerializeField] private Button turnLengthLeftButton;
        [Tooltip("Turn Length Right")]
        [SerializeField] private Button turnLengthRightButton;
        [Tooltip("Turn Length Value")]
        [SerializeField] private TMP_Text turnLengthValueText;

        [Header("Text")]
        [Tooltip("Start Condition Text")]
        [SerializeField] private TMP_Text startConditionText;

        [Header("Navigation")]
        [Tooltip("Start Game Button")]
        [SerializeField] private Button startGameButton;
        [Tooltip("Back Button")]
        [SerializeField] private Button backButton;
        [Tooltip("In Game Scene Name")]
        [SerializeField] private string inGameSceneName = "InGameScene";
        [Tooltip("Start Scene Name")]
        [SerializeField] private string startSceneName = "StartScene";

        private int gameModeIndex = 1;
        private int selectedPlayerCount = GameRuleDefine.DefaultPlayerCount;
        private int mapIndex;
        private int turnLength = GameRuleDefine.DefaultMaxTurnCount;
        private bool isTeamMode;
        private int[] playerTeamIndexes = { 0, 0, 0, 0 };

        private GameMode SelectedGameMode
        {
            get { return gameModeOptions[gameModeIndex]; }
        }

        private MapType SelectedMapType
        {
            get { return mapOptions[mapIndex]; }
        }

        private void Awake()
        {
            BindButtons();
            RefreshUI();
        }

        private void BindButtons()
        {
            AddClick(gameModeLeftButton, PreviousGameMode);
            AddClick(gameModeRightButton, NextGameMode);
            AddClick(playerCountLeftButton, PreviousPlayerCount);
            AddClick(playerCountRightButton, NextPlayerCount);
            AddClick(teamModeLeftButton, ToggleTeamMode);
            AddClick(teamModeRightButton, ToggleTeamMode);
            AddClick(mapLeftButton, PreviousMap);
            AddClick(mapRightButton, NextMap);
            AddClick(turnLengthLeftButton, DecreaseTurnLength);
            AddClick(turnLengthRightButton, IncreaseTurnLength);
            AddClick(startGameButton, StartGame);
            AddClick(backButton, BackToStartScene);

            BindTeamButtons(playerTeamLeftButtons, -1);
            BindTeamButtons(playerTeamRightButtons, 1);
        }

        private void BindTeamButtons(Button[] buttons, int direction)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                int playerIndex = i;
                AddClick(buttons[i], delegate { ChangePlayerTeam(playerIndex, direction); });
            }
        }

        private void PreviousGameMode()
        {
            gameModeIndex = WrapIndex(gameModeIndex - 1, gameModeOptions.Length);
            RefreshUI();
        }

        private void NextGameMode()
        {
            gameModeIndex = WrapIndex(gameModeIndex + 1, gameModeOptions.Length);
            RefreshUI();
        }

        private void PreviousPlayerCount()
        {
            selectedPlayerCount = Mathf.Max(GameRuleDefine.MinDemoPlayerCount, selectedPlayerCount - 1);
            RefreshUI();
        }

        private void NextPlayerCount()
        {
            selectedPlayerCount = Mathf.Min(GameRuleDefine.MaxDemoPlayerCount, selectedPlayerCount + 1);
            RefreshUI();
        }

        private void ToggleTeamMode()
        {
            if (!CanUseTeamMode())
            {
                isTeamMode = false;
                RefreshUI();
                return;
            }

            isTeamMode = !isTeamMode;
            RefreshUI();
        }

        private void PreviousMap()
        {
            mapIndex = WrapIndex(mapIndex - 1, mapOptions.Length);
            RefreshUI();
        }

        private void NextMap()
        {
            mapIndex = WrapIndex(mapIndex + 1, mapOptions.Length);
            RefreshUI();
        }

        private void DecreaseTurnLength()
        {
            turnLength = Mathf.Max(GameRuleDefine.MinMaxTurnCount, turnLength - 1);
            RefreshUI();
        }

        private void IncreaseTurnLength()
        {
            turnLength = Mathf.Min(GameRuleDefine.MaxMaxTurnCount, turnLength + 1);
            RefreshUI();
        }

        private void ChangePlayerTeam(int playerIndex, int direction)
        {
            if (!isTeamMode || playerIndex < 0 || playerIndex >= playerTeamIndexes.Length)
            {
                return;
            }

            playerTeamIndexes[playerIndex] = WrapIndex(playerTeamIndexes[playerIndex] + direction, teamLabels.Length);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (!CanUseTeamMode())
            {
                isTeamMode = false;
            }

            SetText(gameModeValueText, gameModeLabels[gameModeIndex]);
            SetText(playerCountValueText, selectedPlayerCount + " Players");
            SetText(teamModeValueText, isTeamMode ? "Team" : "Solo");
            SetText(mapValueText, mapLabels[mapIndex]);
            SetText(turnLengthValueText, turnLength + " Turns");

            SetInteractable(playerCountLeftButton, selectedPlayerCount > GameRuleDefine.MinDemoPlayerCount);
            SetInteractable(playerCountRightButton, selectedPlayerCount < GameRuleDefine.MaxDemoPlayerCount);
            SetInteractable(teamModeLeftButton, CanUseTeamMode());
            SetInteractable(teamModeRightButton, CanUseTeamMode());
            SetInteractable(turnLengthLeftButton, turnLength > GameRuleDefine.MinMaxTurnCount);
            SetInteractable(turnLengthRightButton, turnLength < GameRuleDefine.MaxMaxTurnCount);

            RefreshPlayerRows();

            string reason;
            bool canStart = CanStartGame(out reason);
            SetInteractable(startGameButton, canStart);
            SetText(startConditionText, canStart ? "Ready" : reason);
        }

        private void RefreshPlayerRows()
        {
            for (int i = 0; i < GetArrayLength(playerRows); i++)
            {
                SetActive(playerRows[i], i < selectedPlayerCount);
            }

            for (int i = 0; i < GetArrayLength(playerTeamValueTexts); i++)
            {
                SetText(playerTeamValueTexts[i], GetTeamLabel(i));
                SetActive(playerTeamValueTexts[i] != null ? playerTeamValueTexts[i].gameObject : null, i < selectedPlayerCount && isTeamMode);
            }

            for (int i = 0; i < GetArrayLength(playerTeamLeftButtons); i++)
            {
                SetActive(playerTeamLeftButtons[i] != null ? playerTeamLeftButtons[i].gameObject : null, i < selectedPlayerCount && isTeamMode);
            }

            for (int i = 0; i < GetArrayLength(playerTeamRightButtons); i++)
            {
                SetActive(playerTeamRightButtons[i] != null ? playerTeamRightButtons[i].gameObject : null, i < selectedPlayerCount && isTeamMode);
            }
        }

        private bool CanStartGame(out string reason)
        {
            if (SelectedGameMode == GameMode.KillTheKing)
            {
                reason = "Kill The King is coming soon.";
                return false;
            }

            if (selectedPlayerCount < GameRuleDefine.MinDemoPlayerCount || selectedPlayerCount > GameRuleDefine.MaxDemoPlayerCount)
            {
                reason = "Only 2-4 players are available for now.";
                return false;
            }

            if (isTeamMode && !IsValidTeamSelection(out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool IsValidTeamSelection(out string reason)
        {
            if (!CanUseTeamMode())
            {
                reason = "Team mode is only available with 4 players for now.";
                return false;
            }

            int[] teamCounts = new int[MaxTeamSlotCount];

            for (int i = 0; i < selectedPlayerCount; i++)
            {
                int teamIndex = playerTeamIndexes[i] - 1;

                if (teamIndex < 0)
                {
                    reason = "Every player must choose a team.";
                    return false;
                }

                teamCounts[teamIndex]++;
            }

            int expectedCount = 0;
            int usedTeamCount = 0;

            foreach (int count in teamCounts)
            {
                if (count <= 0)
                {
                    continue;
                }

                if (expectedCount == 0)
                {
                    expectedCount = count;
                }

                if (count != expectedCount)
                {
                    reason = "Each team must have the same number of players.";
                    return false;
                }

                usedTeamCount++;
            }

            if (usedTeamCount < 2)
            {
                reason = "Team mode needs at least 2 teams.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void StartGame()
        {
            string reason;

            if (!CanStartGame(out reason))
            {
                SetText(startConditionText, reason);
                return;
            }

            RoomSettingsData roomSettings = CreateRoomSettingsData();
            GameStartSettingsHolder.CurrentRoomSettings = roomSettings;
            GameStartSettingsHolder.Current = roomSettings.ToGameStartSettings();

            if (string.IsNullOrWhiteSpace(inGameSceneName))
            {
                Debug.LogWarning("In-game scene name is empty.");
                return;
            }

            SceneManager.LoadScene(inGameSceneName);
        }

        public RoomSettingsData GetCurrentRoomSettingsData()
        {
            return CreateRoomSettingsData();
        }

        public string GetCurrentRoomSettingsJson()
        {
            return CreateRoomSettingsData().ToJson();
        }

        private GameStartSettings CreateGameStartSettings()
        {
            return CreateRoomSettingsData().ToGameStartSettings();
        }

        private RoomSettingsData CreateRoomSettingsData()
        {
            return new RoomSettingsData
            {
                gameMode = (int)SelectedGameMode,
                mapType = (int)SelectedMapType,
                matchComposition = (int)GetMatchComposition(),
                playerCount = selectedPlayerCount,
                maxTurnCount = turnLength,
                isTeamMode = isTeamMode,
                playerTeams = CreatePlayerTeamData()
            };
        }

        private int[] CreatePlayerTeamData()
        {
            int[] teams = new int[selectedPlayerCount];

            for (int i = 0; i < teams.Length; i++)
            {
                teams[i] = isTeamMode ? playerTeamIndexes[i] : 0;
            }

            return teams;
        }

        private MatchComposition GetMatchComposition()
        {
            if (isTeamMode)
            {
                return MatchComposition.TwoVsTwo;
            }

            switch (selectedPlayerCount)
            {
                case 2:
                    return MatchComposition.OneVsOne;
                case 3:
                    return MatchComposition.OneVsOneVsOne;
                case 4:
                    return MatchComposition.OneVsOneVsOneVsOne;
                default:
                    return MatchComposition.None;
            }
        }

        private void BackToStartScene()
        {
            if (string.IsNullOrWhiteSpace(startSceneName))
            {
                Debug.LogWarning("Start scene name is empty.");
                return;
            }

            SceneManager.LoadScene(startSceneName);
        }

        private bool CanUseTeamMode()
        {
            return selectedPlayerCount == GameRuleDefine.DefaultPlayerCount;
        }

        private string GetTeamLabel(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerTeamIndexes.Length)
            {
                return teamLabels[0];
            }

            return teamLabels[playerTeamIndexes[playerIndex]];
        }

        private static int WrapIndex(int index, int length)
        {
            if (length <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return length - 1;
            }

            if (index >= length)
            {
                return 0;
            }

            return index;
        }

        private static int GetArrayLength<T>(T[] array)
        {
            return array != null ? array.Length : 0;
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
