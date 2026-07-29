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

        [Header("Mode")]
        [Tooltip("게임 모드 드롭다운")]
        [SerializeField] private TMP_Dropdown gameModeDropdown;

        [Header("Players")]
        [Tooltip("플레이어 수 드롭다운")]
        [SerializeField] private TMP_Dropdown playerCountDropdown;
        [Tooltip("팀전 토글")]
        [SerializeField] private Toggle teamModeToggle;
        [Tooltip("팀 선택 드롭다운들")]
        [SerializeField] private TMP_Dropdown[] playerTeamDropdowns;
        [Tooltip("플레이어 줄들")]
        [SerializeField] private GameObject[] playerRows;

        [Header("Map")]
        [Tooltip("맵 드롭다운")]
        [SerializeField] private TMP_Dropdown mapDropdown;

        [Header("Turn")]
        [Tooltip("턴 길이 슬라이더")]
        [SerializeField] private Slider turnLengthSlider;
        [Tooltip("턴 길이 텍스트")]
        [SerializeField] private TMP_Text turnLengthText;

        [Header("Text")]
        [Tooltip("시작 조건 텍스트")]
        [SerializeField] private TMP_Text startConditionText;

        [Header("Navigation")]
        [Tooltip("게임 시작 버튼")]
        [SerializeField] private Button startGameButton;
        [Tooltip("뒤로 가기 버튼")]
        [SerializeField] private Button backButton;
        [Tooltip("인게임 씬 이름")]
        [SerializeField] private string inGameSceneName = "InGameScene";
        [Tooltip("시작 씬 이름")]
        [SerializeField] private string startSceneName = "StartScene";

        private GameMode selectedGameMode = GameMode.Escape;
        private int selectedPlayerCount = GameRuleDefine.DefaultPlayerCount;
        private MapType selectedMapType = MapType.Random;

        private void Awake()
        {
            SetupGameModeDropdown();
            SetupPlayerCountDropdown();
            SetupMapDropdown();
            SetupTurnLengthSlider();
            SetupTeamControls();
            BindButtons();
            RefreshUI();
        }

        private void SetupGameModeDropdown()
        {
            if (gameModeDropdown == null)
            {
                return;
            }

            gameModeDropdown.ClearOptions();
            gameModeDropdown.options.Add(new TMP_Dropdown.OptionData("Classic"));
            gameModeDropdown.options.Add(new TMP_Dropdown.OptionData("Escape"));
            gameModeDropdown.options.Add(new TMP_Dropdown.OptionData("Kill The King"));
            gameModeDropdown.value = 1;
            gameModeDropdown.RefreshShownValue();
            gameModeDropdown.onValueChanged.AddListener(SetGameModeByDropdown);
        }

        private void SetupPlayerCountDropdown()
        {
            if (playerCountDropdown == null)
            {
                return;
            }

            playerCountDropdown.ClearOptions();
            playerCountDropdown.options.Add(new TMP_Dropdown.OptionData("2 Players"));
            playerCountDropdown.options.Add(new TMP_Dropdown.OptionData("3 Players"));
            playerCountDropdown.options.Add(new TMP_Dropdown.OptionData("4 Players"));
            playerCountDropdown.value = GameRuleDefine.DefaultPlayerCount - GameRuleDefine.MinDemoPlayerCount;
            playerCountDropdown.RefreshShownValue();
            playerCountDropdown.onValueChanged.AddListener(SetPlayerCountByDropdown);
        }

        private void SetupMapDropdown()
        {
            if (mapDropdown == null)
            {
                return;
            }

            mapDropdown.ClearOptions();
            mapDropdown.options.Add(new TMP_Dropdown.OptionData("Random"));
            mapDropdown.options.Add(new TMP_Dropdown.OptionData("Basic"));
            mapDropdown.options.Add(new TMP_Dropdown.OptionData("Korean"));
            mapDropdown.options.Add(new TMP_Dropdown.OptionData("Grassland"));
            mapDropdown.value = 0;
            mapDropdown.RefreshShownValue();
            mapDropdown.onValueChanged.AddListener(SetMapByDropdown);
        }

        private void SetupTurnLengthSlider()
        {
            if (turnLengthSlider == null)
            {
                return;
            }

            turnLengthSlider.wholeNumbers = true;
            turnLengthSlider.minValue = GameRuleDefine.MinMaxTurnCount;
            turnLengthSlider.maxValue = GameRuleDefine.MaxMaxTurnCount;
            turnLengthSlider.value = GameRuleDefine.DefaultMaxTurnCount;
            turnLengthSlider.onValueChanged.AddListener(delegate { RefreshUI(); });
        }

        private void SetupTeamControls()
        {
            if (teamModeToggle != null)
            {
                teamModeToggle.isOn = false;
                teamModeToggle.onValueChanged.AddListener(delegate { RefreshUI(); });
            }

            if (playerTeamDropdowns == null)
            {
                return;
            }

            for (int i = 0; i < playerTeamDropdowns.Length; i++)
            {
                TMP_Dropdown dropdown = playerTeamDropdowns[i];

                if (dropdown == null)
                {
                    continue;
                }

                dropdown.ClearOptions();
                dropdown.options.Add(new TMP_Dropdown.OptionData("No Team"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Blue"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Yellow"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Red"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Green"));
                dropdown.value = 0;
                dropdown.RefreshShownValue();
                dropdown.onValueChanged.AddListener(delegate { RefreshUI(); });
            }
        }

        private void BindButtons()
        {
            AddClick(startGameButton, StartGame);
            AddClick(backButton, BackToStartScene);
        }

        private void SetGameModeByDropdown(int index)
        {
            switch (index)
            {
                case 0:
                    selectedGameMode = GameMode.Classic;
                    break;
                case 1:
                    selectedGameMode = GameMode.Escape;
                    break;
                case 2:
                    selectedGameMode = GameMode.KillTheKing;
                    break;
            }

            RefreshUI();
        }

        private void SetPlayerCountByDropdown(int index)
        {
            selectedPlayerCount = GameRuleDefine.MinDemoPlayerCount + index;
            RefreshUI();
        }

        private void SetMapByDropdown(int index)
        {
            switch (index)
            {
                case 0:
                    selectedMapType = MapType.Random;
                    break;
                case 1:
                    selectedMapType = MapType.Basic;
                    break;
                case 2:
                    selectedMapType = MapType.Korean;
                    break;
                case 3:
                    selectedMapType = MapType.Grassland;
                    break;
            }

            RefreshUI();
        }

        private void RefreshUI()
        {
            bool isTeamMode = IsTeamModeSelected();
            bool canUseTeamMode = selectedPlayerCount == GameRuleDefine.DefaultPlayerCount;

            if (teamModeToggle != null)
            {
                teamModeToggle.interactable = canUseTeamMode;

                if (!canUseTeamMode && teamModeToggle.isOn)
                {
                    teamModeToggle.isOn = false;
                    isTeamMode = false;
                }
            }

            RefreshPlayerRows(isTeamMode);
            SetText(turnLengthText, "Turn Length: " + GetTurnLength() + " Turns");

            string reason;
            bool canStart = CanStartGame(out reason);
            SetInteractable(startGameButton, canStart);
            SetText(startConditionText, canStart ? "Ready" : reason);
        }

        private void RefreshPlayerRows(bool isTeamMode)
        {
            for (int i = 0; i < GetArrayLength(playerRows); i++)
            {
                SetActive(playerRows[i], i < selectedPlayerCount);
            }

            for (int i = 0; i < GetArrayLength(playerTeamDropdowns); i++)
            {
                TMP_Dropdown dropdown = playerTeamDropdowns[i];

                if (dropdown == null)
                {
                    continue;
                }

                dropdown.gameObject.SetActive(i < selectedPlayerCount && isTeamMode);
            }
        }

        private bool CanStartGame(out string reason)
        {
            if (selectedGameMode == GameMode.KillTheKing)
            {
                reason = "Kill The King is coming soon.";
                return false;
            }

            if (selectedPlayerCount < GameRuleDefine.MinDemoPlayerCount || selectedPlayerCount > GameRuleDefine.MaxDemoPlayerCount)
            {
                reason = "Only 2-4 players are available for now.";
                return false;
            }

            if (IsTeamModeSelected() && !IsValidTeamSelection(out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool IsValidTeamSelection(out string reason)
        {
            if (selectedPlayerCount != GameRuleDefine.DefaultPlayerCount)
            {
                reason = "Team mode is only available with 4 players for now.";
                return false;
            }

            int[] teamCounts = new int[MaxTeamSlotCount];

            for (int i = 0; i < selectedPlayerCount; i++)
            {
                int teamIndex = GetTeamDropdownValue(i) - 1;

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
                gameMode = (int)selectedGameMode,
                mapType = (int)selectedMapType,
                matchComposition = (int)GetMatchComposition(),
                playerCount = selectedPlayerCount,
                maxTurnCount = GetTurnLength(),
                isTeamMode = IsTeamModeSelected(),
                playerTeams = CreatePlayerTeamData()
            };
        }

        private int[] CreatePlayerTeamData()
        {
            int[] teams = new int[selectedPlayerCount];

            for (int i = 0; i < teams.Length; i++)
            {
                teams[i] = IsTeamModeSelected() ? GetTeamDropdownValue(i) : 0;
            }

            return teams;
        }

        private MatchComposition GetMatchComposition()
        {
            if (IsTeamModeSelected())
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

        private bool IsTeamModeSelected()
        {
            return teamModeToggle != null && teamModeToggle.isOn;
        }

        private int GetTurnLength()
        {
            return turnLengthSlider != null ? Mathf.RoundToInt(turnLengthSlider.value) : GameRuleDefine.DefaultMaxTurnCount;
        }

        private int GetTeamDropdownValue(int index)
        {
            if (playerTeamDropdowns == null || index < 0 || index >= playerTeamDropdowns.Length || playerTeamDropdowns[index] == null)
            {
                return 0;
            }

            return playerTeamDropdowns[index].value;
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
