using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutArena.Common;

namespace YutArena.UI
{
    public class LobbySceneUIController : MonoBehaviour
    {
        private readonly GameMode[] gameModeOptions =
        {
            GameMode.Classic,
            GameMode.Escape,
            GameMode.KillTheKing
        };

        private readonly int[] turnTimeOptions = { 30, 40, 50, 60 };

        [Header("Mode")]
        [SerializeField] private GameModeDatabase gameModeDatabase;
        [Tooltip("Classic, Escape, KillTheKing 순서의 카드 표시")]
        [SerializeField] private GameModeCardView[] gameModeCards;
        [Tooltip("Classic, Escape, KillTheKing 순서의 모드 선택 버튼")]
        [SerializeField] private Button[] gameModeButtons;
        [SerializeField] private GameObject[] gameModeFramesOn;
        [SerializeField] private GameObject[] gameModeFramesOff;

        [Header("Mode Rules")]
        [SerializeField] private GameObject classicRules;
        [SerializeField] private GameObject escapeRules;
        [SerializeField] private GameObject killTheKingRules;
        [SerializeField] private Button startPieceCountLeftButton;
        [SerializeField] private Button startPieceCountRightButton;
        [SerializeField] private TMP_Text startPieceCountValueText;
        [SerializeField] private Button escapeTargetLeftButton;
        [SerializeField] private Button escapeTargetRightButton;
        [SerializeField] private TMP_Text escapeTargetValueText;

        [Header("Players")]
        [Tooltip("플레이어 수 감소 버튼")]
        [SerializeField] private Button playerCountLeftButton;
        [Tooltip("플레이어 수 증가 버튼")]
        [SerializeField] private Button playerCountRightButton;
        [Tooltip("플레이어 수 텍스트")]
        [SerializeField] private TMP_Text playerCountValueText;
        [SerializeField] private Button soloModeButton;
        [SerializeField] private Button teamModeButton;
        [SerializeField] private GameObject soloModeOn;
        [SerializeField] private GameObject soloModeOff;
        [SerializeField] private GameObject teamModeOn;
        [SerializeField] private GameObject teamModeOff;
        [Tooltip("1P 입력 장치 이전 버튼")]
        [SerializeField] private Button sharedInputLeftButton;
        [Tooltip("1P 입력 장치 다음 버튼")]
        [SerializeField] private Button sharedInputRightButton;
        [Tooltip("1P 입력 장치 텍스트")]
        [SerializeField] private TMP_Text sharedInputLabel;
        [Tooltip("플레이어 행")]
        [SerializeField] private GameObject[] playerRows;
        [Tooltip("플레이어 팀 텍스트")]
        [SerializeField] private TMP_Text[] playerTeamValueTexts;
        [Tooltip("팀전에서 플레이어별 빨강/파랑을 바꾸는 버튼")]
        [SerializeField] private Button[] playerTeamRightButtons;
        [Tooltip("플레이어 카드의 색상 탭 이미지")]
        [SerializeField] private Image[] playerTabImages;
        [Tooltip("플레이어 카드의 깃발 이미지")]
        [SerializeField] private Image[] playerFlagImages;
        [Tooltip("개인전 플레이어별 탭 이미지")]
        [SerializeField] private Sprite[] soloTabSprites;
        [Tooltip("개인전 플레이어별 깃발 이미지")]
        [SerializeField] private Sprite[] soloFlagSprites;
        [SerializeField] private Sprite redTeamTabSprite;
        [SerializeField] private Sprite blueTeamTabSprite;
        [SerializeField] private Sprite redTeamFlagSprite;
        [SerializeField] private Sprite blueTeamFlagSprite;

        [Header("Map")]
        [SerializeField] private MapDatabase mapDatabase;
        [SerializeField] private Image mapImage;
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private TMP_Text mapDescriptionText;
        [Tooltip("맵 이전 버튼")]
        [SerializeField] private Button mapLeftButton;
        [Tooltip("맵 다음 버튼")]
        [SerializeField] private Button mapRightButton;

        [Header("Turn")]
        [Tooltip("턴 길이 감소 버튼")]
        [SerializeField] private Button turnLengthLeftButton;
        [Tooltip("턴 길이 증가 버튼")]
        [SerializeField] private Button turnLengthRightButton;
        [Tooltip("턴 길이 텍스트")]
        [SerializeField] private TMP_Text turnLengthValueText;

        [Header("Time")]
        [Tooltip("턴 시간 감소 버튼")]
        [SerializeField] private Button turnTimeLeftButton;
        [Tooltip("턴 시간 증가 버튼")]
        [SerializeField] private Button turnTimeRightButton;
        [Tooltip("턴 시간 텍스트")]
        [SerializeField] private TMP_Text turnTimeValueText;

        [Header("Navigation")]
        [Tooltip("게임 시작 버튼")]
        [SerializeField] private Button startGameButton;
        [Tooltip("뒤로가기 버튼")]
        [SerializeField] private Button backButton;
        [Tooltip("인게임 씬 이름")]
        [SerializeField] private string inGameSceneName = "InGameScene";
        [Tooltip("시작 씬 이름")]
        [SerializeField] private string startSceneName = "StartScene";

        private int gameModeIndex = 1;
        private int selectedPlayerCount = GameRuleDefine.DefaultPlayerCount;
        private int mapIndex;
        private MapEntry[] maps = System.Array.Empty<MapEntry>();
        private int turnLength = GameRuleDefine.DefaultMaxTurnCount;
        private int turnTimeIndex;
        private int startPieceCount = GameRuleDefine.DefaultPieceCountPerPlayer;
        private int targetEscapeCount = GameRuleDefine.DefaultTargetEscapeCount;
        private bool isTeamMode;
        private readonly int[] playerTeamIndexes = { 1, 2, 1, 2 };
        private ButtonSound startButtonSound;
        private EventSystem navigationEventSystem;
        private bool previousNavigationEvents;

        private GameMode SelectedGameMode
        {
            get { return gameModeOptions[gameModeIndex]; }
        }

        private MapType SelectedMapType
        {
            get { return maps.Length > 0 ? maps[mapIndex].mapType : MapType.Random; }
        }

        private void Awake()
        {
            if (startGameButton != null)
            {
                startButtonSound = startGameButton.GetComponent<ButtonSound>();
            }

            maps = mapDatabase != null ? mapDatabase.GetOrderedMaps() : System.Array.Empty<MapEntry>();
            mapIndex = 0;
            BindButtons();
            RefreshUI();
        }

        private void Update()
        {
            LocalSelectionInput.Poll(selectedPlayerCount);
            if (sharedInputLabel != null)
                sharedInputLabel.text = LocalSelectionInput.SharedGamepadSelected ? "Pad" : "Keyboard";
            DisableNavigationEvents();
            RefreshStartAvailability();
        }

        private void ToggleSharedInput()
        {
            if (LocalSelectionInput.SharedGamepadSelected)
            {
                LocalSelectionInput.SelectSharedKeyboard();
                return;
            }
            LocalSelectionInput.SelectSharedGamepadMode();
        }

        private void OnEnable()
        {
            DisableNavigationEvents();
        }

        private void DisableNavigationEvents()
        {
            if (navigationEventSystem != null || EventSystem.current == null) return;

            navigationEventSystem = EventSystem.current;
            previousNavigationEvents = navigationEventSystem.sendNavigationEvents;
            // 로비 설정은 포인터로만 조작합니다. Move/Submit/Cancel 입력을 차단합니다.
            navigationEventSystem.sendNavigationEvents = false;
        }

        private void OnDisable()
        {
            if (navigationEventSystem != null)
                navigationEventSystem.sendNavigationEvents = previousNavigationEvents;
            navigationEventSystem = null;
        }

        private void BindButtons()
        {
            for (int i = 0; i < GetArrayLength(gameModeButtons); i++)
            {
                int modeIndex = i;
                AddClick(gameModeButtons[i], () => SelectGameMode(modeIndex));
            }
            AddClick(playerCountLeftButton, PreviousPlayerCount);
            AddClick(playerCountRightButton, NextPlayerCount);
            AddClick(soloModeButton, () => SelectTeamMode(false));
            AddClick(teamModeButton, () => SelectTeamMode(true));
            AddClick(sharedInputLeftButton, ToggleSharedInput);
            AddClick(sharedInputRightButton, ToggleSharedInput);
            AddClick(mapLeftButton, PreviousMap);
            AddClick(mapRightButton, NextMap);
            AddClick(turnLengthLeftButton, DecreaseTurnLength);
            AddClick(turnLengthRightButton, IncreaseTurnLength);
            AddClick(turnTimeLeftButton, PreviousTurnTime);
            AddClick(turnTimeRightButton, NextTurnTime);
            AddClick(startPieceCountLeftButton, () => ChangeStartPieceCount(-1));
            AddClick(startPieceCountRightButton, () => ChangeStartPieceCount(1));
            AddClick(escapeTargetLeftButton, () => ChangeEscapeTarget(-1));
            AddClick(escapeTargetRightButton, () => ChangeEscapeTarget(1));
            AddClick(startGameButton, StartGame);
            AddClick(backButton, BackToStartScene);

            for (int i = 0; i < GetArrayLength(playerTeamRightButtons); i++)
            {
                int playerIndex = i;
                AddClick(playerTeamRightButtons[i], () => ChangePlayerTeam(playerIndex));
            }
        }

        private void ChangePlayerTeam(int playerIndex)
        {
            if (!isTeamMode || playerIndex < 0 || playerIndex >= playerTeamIndexes.Length) return;
            playerTeamIndexes[playerIndex] = playerTeamIndexes[playerIndex] == 1 ? 2 : 1;
            RefreshUI();
        }

        private void SelectGameMode(int index)
        {
            if (index < 0 || index >= gameModeOptions.Length) return;
            gameModeIndex = index;
            RefreshUI();
        }

        private void ChangeStartPieceCount(int step)
        {
            if (SelectedGameMode != GameMode.Escape) return;
            startPieceCount = Mathf.Clamp(startPieceCount + step,
                GameRuleDefine.MinPieceCountPerPlayer, GameRuleDefine.MaxPieceCountPerPlayer);
            RefreshUI();
        }

        private void ChangeEscapeTarget(int step)
        {
            if (SelectedGameMode != GameMode.Escape) return;
            targetEscapeCount = Mathf.Clamp(targetEscapeCount + step,
                GameRuleDefine.MinTargetEscapeCount, GameRuleDefine.MaxTargetEscapeCount);
            RefreshUI();
        }

        private void RefreshGameModeSelection()
        {
            for (int i = 0; i < GetArrayLength(gameModeCards) && i < gameModeOptions.Length; i++)
            {
                if (gameModeCards[i] != null)
                    gameModeCards[i].Refresh(gameModeDatabase != null
                        ? gameModeDatabase.FindByMode(gameModeOptions[i]) : null);
            }
            for (int i = 0; i < GetArrayLength(gameModeFramesOn); i++)
                SetActive(gameModeFramesOn[i], i == gameModeIndex);
            for (int i = 0; i < GetArrayLength(gameModeFramesOff); i++)
                SetActive(gameModeFramesOff[i], i != gameModeIndex);
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

        private void SelectTeamMode(bool teamMode)
        {
            isTeamMode = teamMode && CanUseTeamMode();
            RefreshUI();
        }

        private void RefreshMap()
        {
            MapEntry map = maps.Length > 0 ? maps[mapIndex] : null;
            SetText(mapNameText, map != null ? map.mapName : string.Empty);
            SetText(mapDescriptionText, map != null ? map.description : string.Empty);
            if (mapImage != null)
            {
                mapImage.sprite = map != null ? map.image : null;
                mapImage.enabled = mapImage.sprite != null;
            }
            SetInteractable(mapLeftButton, maps.Length > 1);
            SetInteractable(mapRightButton, maps.Length > 1);
        }

        private void PreviousMap()
        {
            mapIndex = WrapIndex(mapIndex - 1, maps.Length);
            RefreshUI();
        }

        private void NextMap()
        {
            mapIndex = WrapIndex(mapIndex + 1, maps.Length);
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

        private void PreviousTurnTime()
        {
            turnTimeIndex = Mathf.Max(0, turnTimeIndex - 1);
            RefreshUI();
        }

        private void NextTurnTime()
        {
            turnTimeIndex = Mathf.Min(turnTimeOptions.Length - 1, turnTimeIndex + 1);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (!CanUseTeamMode())
            {
                isTeamMode = false;
            }

            RefreshGameModeSelection();
            SetActive(classicRules, SelectedGameMode == GameMode.Classic);
            SetActive(escapeRules, SelectedGameMode == GameMode.Escape);
            SetActive(killTheKingRules, SelectedGameMode == GameMode.KillTheKing);
            SetText(startPieceCountValueText, startPieceCount.ToString());
            SetText(escapeTargetValueText, targetEscapeCount.ToString());
            SetInteractable(startPieceCountLeftButton, startPieceCount > GameRuleDefine.MinPieceCountPerPlayer);
            SetInteractable(startPieceCountRightButton, startPieceCount < GameRuleDefine.MaxPieceCountPerPlayer);
            SetInteractable(escapeTargetLeftButton, targetEscapeCount > GameRuleDefine.MinTargetEscapeCount);
            SetInteractable(escapeTargetRightButton, targetEscapeCount < GameRuleDefine.MaxTargetEscapeCount);
            SetText(playerCountValueText, selectedPlayerCount + " Players");
            SetActive(soloModeOn, !isTeamMode);
            SetActive(soloModeOff, isTeamMode);
            SetActive(teamModeOn, isTeamMode);
            SetActive(teamModeOff, !isTeamMode);
            RefreshMap();
            SetText(turnLengthValueText, turnLength + " Turns");
            SetText(turnTimeValueText, turnTimeOptions[turnTimeIndex] + " Seconds");

            SetInteractable(playerCountLeftButton, selectedPlayerCount > GameRuleDefine.MinDemoPlayerCount);
            SetInteractable(playerCountRightButton, selectedPlayerCount < GameRuleDefine.MaxDemoPlayerCount);
            SetInteractable(soloModeButton, true);
            SetInteractable(teamModeButton, CanUseTeamMode());
            SetInteractable(turnLengthLeftButton, turnLength > GameRuleDefine.MinMaxTurnCount);
            SetInteractable(turnLengthRightButton, turnLength < GameRuleDefine.MaxMaxTurnCount);
            SetInteractable(turnTimeLeftButton, turnTimeIndex > 0);
            SetInteractable(turnTimeRightButton, turnTimeIndex < turnTimeOptions.Length - 1);

            RefreshPlayerRows();

            RefreshStartAvailability();
        }

        private void RefreshStartAvailability()
        {
            SetInteractable(startGameButton, CanStartGame());
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
                SetActive(playerTeamValueTexts[i] != null ? playerTeamValueTexts[i].gameObject : null, i < selectedPlayerCount);
            }

            for (int i = 0; i < GetArrayLength(playerTeamRightButtons); i++)
            {
                Button button = playerTeamRightButtons[i];
                SetActive(button != null ? button.gameObject : null, i < selectedPlayerCount && isTeamMode);
                SetInteractable(button, i < selectedPlayerCount && isTeamMode);
            }

            for (int i = 0; i < GetArrayLength(playerTabImages); i++)
            {
                if (playerTabImages[i] != null)
                    playerTabImages[i].sprite = isTeamMode
                        ? (playerTeamIndexes[i] == 1 ? redTeamTabSprite : blueTeamTabSprite)
                        : soloTabSprites[i];
            }

            for (int i = 0; i < GetArrayLength(playerFlagImages); i++)
            {
                if (playerFlagImages[i] != null)
                    playerFlagImages[i].sprite = isTeamMode
                        ? (playerTeamIndexes[i] == 1 ? redTeamFlagSprite : blueTeamFlagSprite)
                        : soloFlagSprites[i];
            }

        }

        private bool CanStartGame()
        {
            if (!LocalSelectionInput.SharedInputAvailable)
                return false;

            if (SelectedGameMode == GameMode.KillTheKing)
                return false;

            if (selectedPlayerCount < GameRuleDefine.MinDemoPlayerCount || selectedPlayerCount > GameRuleDefine.MaxDemoPlayerCount)
                return false;

            if (isTeamMode)
            {
                if (!CanUseTeamMode()) return false;
                if (!HasBalancedTeams()) return false;
            }

            return true;
        }

        private bool HasBalancedTeams()
        {
            int redCount = 0;
            int blueCount = 0;

            for (int i = 0; i < selectedPlayerCount; i++)
            {
                if (playerTeamIndexes[i] == 1) redCount++;
                else if (playerTeamIndexes[i] == 2) blueCount++;
            }

            return redCount == 2 && blueCount == 2;
        }

        private int GetConnectedRequiredPlayerCount()
        {
            int connectedCount = 0;

            for (int playerIndex = 0; playerIndex < selectedPlayerCount; playerIndex++)
            {
                if (LocalPlayerJoinState.IsJoined(playerIndex))
                {
                    connectedCount++;
                }
            }

            return connectedCount;
        }

        private void StartGame()
        {
            if (!CanStartGame())
            {
                PlayInvalidStartSound();
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

        private void PlayInvalidStartSound()
        {
            if (startButtonSound != null)
            {
                startButtonSound.PlayInvalidClickSound();
            }
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
                pieceCountPerPlayer = SelectedGameMode == GameMode.Escape
                    ? startPieceCount : GameRuleDefine.DefaultPieceCountPerPlayer,
                targetEscapeCount = SelectedGameMode == GameMode.Escape
                    ? targetEscapeCount : GameRuleDefine.DefaultTargetEscapeCount,
                maxTurnCount = turnLength,
                turnTimeSeconds = turnTimeOptions[turnTimeIndex],
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
            if (playerIndex < 0 || playerIndex >= selectedPlayerCount) return string.Empty;
            if (isTeamMode) return playerTeamIndexes[playerIndex] == 1 ? "빨강" : "파랑";

            switch (playerIndex)
            {
                case 0: return "빨강";
                case 1: return "파랑";
                case 2: return "초록";
                case 3: return "노랑";
                default: return string.Empty;
            }
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
