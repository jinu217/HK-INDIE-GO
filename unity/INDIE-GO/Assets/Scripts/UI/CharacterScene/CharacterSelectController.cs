using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutArena.Common;

namespace YutArena.UI.CharacterScene
{
    public class CharacterSelectController : MonoBehaviour
    {
        private const int MaxPlayerCount = 8;

        [SerializeField] private InGameSettingsPanel settingsPanel;

        [Header("Characters")]
        [Tooltip("캐릭터 데이터베이스")]
        [SerializeField] private CharacterDatabase characterDatabase;

        [Header("Rules")]
        [Tooltip("기본 플레이어 수")]
        [SerializeField, Range(1, MaxPlayerCount)] private int fallbackPlayerCount = 4;
        [Tooltip("선택 제한 시간")]
        [SerializeField, Min(1f)] private float selectionTimeSeconds = 30f;
        [Tooltip("그리드 열 개수")]
        [SerializeField, Min(1)] private int gridColumnCount = 5;
        [Tooltip("인게임 씬 이름")]
        [SerializeField] private string inGameSceneName = "InGameScene";
        [Tooltip("로컬 로비 씬 이름")]
        [SerializeField] private string localLobbySceneName = "LocalLobbyScene";

        [Header("Common UI")]
        [Tooltip("남은 시간 텍스트")]
        [SerializeField] private TMP_Text remainingTimeText;
        [Tooltip("선택 완료 인원 텍스트")]
        [SerializeField] private TMP_Text selectionCompletedText;
        [Tooltip("뒤로가기 버튼")]
        [SerializeField] private Button backButton;
        [Tooltip("모든 플레이어가 선택을 마치면 누를 수 있는 게임 시작 버튼")]
        [SerializeField] private Button startGameButton;
        [Tooltip("게임 시작 시 표시할 이미지 게이지 로딩 패널")]
        [SerializeField] private GameStartLoadingPanel gameStartLoadingPanel;
        [Tooltip("설정 버튼")]
        [SerializeField] private Button settingButton;
        [Tooltip("도움말 버튼")]
        [SerializeField] private Button helpButton;
        [Tooltip("도움말 버튼을 누르면 열 패널")]
        [SerializeField] private GameObject helpPanel;

        [Header("Character UI")]
        [Tooltip("캐릭터 카드 목록")]
        [SerializeField] private CharacterCardView[] cardViews;
        [Tooltip("캐릭터 카드 앞에 표시할 랜덤 선택 카드")]
        [SerializeField] private CharacterCardView randomCardView;
        [Tooltip("랜덤 카드에 표시할 이미지")]
        [SerializeField] private Sprite randomCardSprite;
        [Tooltip("랜덤 선택 시 패널에 표시할 물음표 이미지")]
        [SerializeField] private Sprite randomPanelSprite;
        [Tooltip("플레이어 마커 목록")]
        [SerializeField] private PlayerSelectionMarkerView[] playerMarkerViews;
        [Tooltip("팀전 Red 플레이어에게 표시할 마커 이미지")]
        [SerializeField] private Sprite redTeamMarkerSprite;
        [Tooltip("팀전 Blue 플레이어에게 표시할 마커 이미지")]
        [SerializeField] private Sprite blueTeamMarkerSprite;
        [Tooltip("플레이어 상세 패널 목록")]
        [SerializeField] private PlayerCharacterPanelView[] playerPanelViews;

        private readonly int[] cursorIndexes = new int[MaxPlayerCount];
        private readonly bool[] selectedPlayers = new bool[MaxPlayerCount];
        private readonly CharacterData[] randomSelections = new CharacterData[MaxPlayerCount];
        // 2인 1조(팀전1): 각 플레이어가 따라가는 팀 대표의 index. 대표 본인이거나 팀 규칙이 없으면 자기 자신.
        private readonly int[] leaderIndexes = new int[MaxPlayerCount];
        // 2대2(팀전2): 같은 팀끼리 같은 캐릭터 선택 불가
        private bool uniqueCharacterPerTeam;
        private readonly TeamSlot[] personTeams = new TeamSlot[MaxPlayerCount];
        private readonly List<CharacterData> runtimeCharacters = new List<CharacterData>();

        private int playerCount;
        private float remainingTime;
        private bool isFinalized;
        private int keyboardPlayerIndex;

        private void Awake()
        {
            CharacterSelectionResult.Clear();
            BuildRuntimeCharacterList();
            ResolvePlayers();

            remainingTime = selectionTimeSeconds;

            if (backButton != null)
            {
                backButton.onClick.AddListener(BackToLocalLobby);
            }
            if (startGameButton != null)
                startGameButton.onClick.AddListener(StartGame);
            if (settingButton != null)
                settingButton.onClick.AddListener(ToggleSettingPanel);
            if (helpButton != null)
                helpButton.onClick.AddListener(ToggleHelpPanel);
            SetActive(helpPanel, false);

            InitializeCards();
            InitializePlayerMarkers();
            RefreshUI();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToLocalLobby);
            }
            if (startGameButton != null)
                startGameButton.onClick.RemoveListener(StartGame);
            if (settingButton != null)
                settingButton.onClick.RemoveListener(ToggleSettingPanel);
            if (helpButton != null)
                helpButton.onClick.RemoveListener(ToggleHelpPanel);
        }

        public void ToggleSettingPanel()
        {
            SetActive(helpPanel, false);
            if (InGameSettingsPanel.IsOpen) return;
            if (settingsPanel != null) settingsPanel.Open();
        }

        public void ToggleHelpPanel()
        {
            if (helpPanel == null) return;
            bool shouldOpen = !helpPanel.activeSelf;
            SetActive(helpPanel, shouldOpen);
        }

        public void CloseSettingPanel()
        {
            if (!InGameSettingsPanel.IsOpen) return;
            if (settingsPanel != null) settingsPanel.Close();
        }

        public void CloseHelpPanel() => SetActive(helpPanel, false);

        public void CloseInfoPanels()
        {
            CloseSettingPanel();
            CloseHelpPanel();
        }

        private void Update()
        {
            if (YutArena.UI.SettingsPanel.IsBlockingInput) return;
            if (isFinalized || runtimeCharacters.Count == 0)
            {
                return;
            }

            if (IsInfoPanelOpen)
            {
                RefreshUI();
                return;
            }

            if (LocalSelectionInput.SharedInputAvailable)
                remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);
            ProcessInputs();

            if (remainingTime <= 0f && LocalSelectionInput.SharedInputAvailable)
            {
                BeginGameStart(true);
                return;
            }

            RefreshUI();
        }

        private bool IsInfoPanelOpen =>
            (helpPanel != null && helpPanel.activeInHierarchy);

        public void BackToLocalLobby()
        {
            if (isFinalized)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(localLobbySceneName))
            {
                Debug.LogWarning("Local lobby scene name is empty.", this);
                return;
            }

            CharacterSelectionResult.Clear();
            SceneManager.LoadScene(localLobbySceneName);
        }

        private void BuildRuntimeCharacterList()
        {
            runtimeCharacters.Clear();

            if (characterDatabase == null)
            {
                return;
            }

            HashSet<int> ids = new HashSet<int>();

            foreach (CharacterData data in characterDatabase.Characters)
            {
                if (data == null)
                {
                    continue;
                }

                if (!ids.Add(data.char_ID))
                {
                    Debug.LogWarning($"Duplicate character ID: {data.char_ID}", this);
                }

                runtimeCharacters.Add(data);
            }
        }

        private void ResolvePlayers()
        {
            GameStartSettings settings = GameStartSettingsHolder.Current;
            playerCount = settings != null
                ? Mathf.Clamp(settings.playerCount, 1, MaxPlayerCount)
                : Mathf.Clamp(fallbackPlayerCount, 1, MaxPlayerCount);

            // 2인 1조(팀전1)는 팀마다 index가 가장 앞인 플레이어가 대표로 선택하고, 팀원은 대표의 선택을 따라간다.
            bool sharedTeamCharacter = MatchCompositionRule.UsesSharedTeamCharacter(settings);
            for (int i = 0; i < MaxPlayerCount; i++)
            {
                leaderIndexes[i] = i;
                if (!sharedTeamCharacter || i >= playerCount) continue;

                TeamSlot team = MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)(i + 1));
                if (team == TeamSlot.None) continue;
                for (int other = 0; other < i; other++)
                {
                    if (MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)(other + 1)) == team)
                    {
                        leaderIndexes[i] = other;
                        break;
                    }
                }
            }

            // 2대2(팀전2)는 같은 팀끼리 같은 캐릭터를 고를 수 없다. 사람마다 팀을 기억해 둔다.
            uniqueCharacterPerTeam = settings != null && settings.isTeamMode && settings.teamMode == TeamMode.TeamMode2;
            for (int i = 0; i < MaxPlayerCount; i++)
                personTeams[i] = uniqueCharacterPerTeam && i < playerCount
                    ? MatchCompositionRule.GetPersonTeamSlot(settings, (PlayerSlot)(i + 1))
                    : TeamSlot.None;
        }

        private bool AreTeammates(int a, int b) =>
            uniqueCharacterPerTeam && a != b && personTeams[a] != TeamSlot.None && personTeams[a] == personTeams[b];

        // 선택을 확정한 플레이어의 캐릭터 (랜덤은 뽑힌 캐릭터). 아직 확정 전이거나 모르면 null
        private CharacterData GetConfirmedCharacter(int playerIndex)
        {
            if (!selectedPlayers[playerIndex]) return null;
            if (randomSelections[playerIndex] != null) return randomSelections[playerIndex];
            int option = cursorIndexes[playerIndex];
            if (IsRandomOption(option)) return null;
            int index = GetCharacterIndex(option);
            return index >= 0 && index < runtimeCharacters.Count ? runtimeCharacters[index] : null;
        }

        // 같은 팀이 이미 확정한 캐릭터 목록
        private HashSet<CharacterData> GetTeammateCharacters(int playerIndex)
        {
            var result = new HashSet<CharacterData>();
            for (int i = 0; i < playerCount; i++)
            {
                if (!AreTeammates(playerIndex, i)) continue;
                CharacterData character = GetConfirmedCharacter(i);
                if (character != null) result.Add(character);
            }
            return result;
        }

        private bool IsFollower(int playerIndex) => leaderIndexes[playerIndex] != playerIndex;

        // 팀원은 대표의 커서·선택 상태를 그대로 복사한다.
        private void SyncFollowers()
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (!IsFollower(i)) continue;
                int leader = leaderIndexes[i];
                cursorIndexes[i] = cursorIndexes[leader];
                selectedPlayers[i] = selectedPlayers[leader];
                randomSelections[i] = randomSelections[leader];
            }
        }

        private void InitializeCards()
        {
            if (randomCardView != null)
            {
                randomCardView.SetPortrait(randomCardSprite);
            }
            if (cardViews == null)
            {
                return;
            }

            for (int i = 0; i < cardViews.Length; i++)
            {
                CharacterCardView card = cardViews[i];
                if (card == null) continue;

                bool isUsed = i < runtimeCharacters.Count;
                card.gameObject.SetActive(isUsed);
                card.SetCharacter(isUsed ? runtimeCharacters[i] : null);
            }
        }

        private void InitializePlayerMarkers()
        {
            GameStartSettings settings = GameStartSettingsHolder.Current;
            PlayerSelectionMarkerView[] markers = playerMarkerViews;
            if (markers == null)
            {
                return;
            }

            int markerCount = Mathf.Min(markers.Length, MaxPlayerCount);

            for (int playerIndex = 0; playerIndex < markerCount; playerIndex++)
            {
                PlayerSelectionMarkerView marker = markers[playerIndex];
                if (marker == null) continue;

                bool isTeamMode = settings != null && settings.isTeamMode;
                int teamIndex = isTeamMode && settings.playerTeams != null && playerIndex < settings.playerTeams.Length
                    ? settings.playerTeams[playerIndex] : 0;
                marker.SetTeamImage(isTeamMode, teamIndex, redTeamMarkerSprite, blueTeamMarkerSprite);
                marker.gameObject.SetActive(playerIndex < playerCount && !IsFollower(playerIndex));
            }
        }

        private void ProcessInputs()
        {
            LocalSelectionInput.Poll(playerCount);
            keyboardPlayerIndex = -1;
            int lastSharedPlayer = -1;
            for (int i = 0; i < playerCount; i++)
            {
                // 팀원은 직접 선택하지 않는다 (대표 선택을 따라감).
                if (IsFollower(i)) continue;
                Gamepad assignedPad = LocalSelectionInput.GetGamepad(i);
                if (assignedPad != null)
                {
                    // Joining must not also confirm the default character on the same A press.
                    if (!LocalSelectionInput.JoinedThisFrame(i)) ProcessGamepadInput(i, assignedPad);
                    continue;
                }
                lastSharedPlayer = i;
                if (keyboardPlayerIndex < 0 && !selectedPlayers[i]) keyboardPlayerIndex = i;
            }
            if (keyboardPlayerIndex < 0) keyboardPlayerIndex = lastSharedPlayer;
            if (keyboardPlayerIndex < 0) return;
            if (LocalSelectionInput.SharedGamepadSelected)
            {
                Gamepad sharedPad = LocalSelectionInput.GetInputGamepad(keyboardPlayerIndex);
                if (sharedPad != null) ProcessGamepadInput(keyboardPlayerIndex, sharedPad);
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if ((keyboard != null && GameInput.UiPressed(UiAction.Cancel, keyboard)) ||
                (Mouse.current != null && GameInput.UiPressed(UiAction.SecondaryClick, Mouse.current)))
            {
                if (!selectedPlayers[keyboardPlayerIndex])
                {
                    for (int i = keyboardPlayerIndex - 1; i >= 0; i--)
                        if (!IsFollower(i) && LocalSelectionInput.GetGamepad(i) == null)
                        {
                            keyboardPlayerIndex = i;
                            break;
                        }
                }
                CancelSelection(keyboardPlayerIndex);
                return;
            }
            ProcessKeyboardInput(keyboardPlayerIndex);
        }

        private void ProcessKeyboardInput(int playerIndex)
        {
            if (Mouse.current != null && GameInput.UiPressed(UiAction.Click, Mouse.current) &&
                !selectedPlayers[playerIndex])
            {
                Vector2 pointer = GameInput.PointerPosition;
                for (int i = 0; i < OptionCount; i++)
                {
                    CharacterCardView card = GetCard(i);
                    Canvas canvas = card != null ? card.GetComponentInParent<Canvas>() : null;
                    Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? canvas.worldCamera : null;
                    if (card != null && RectTransformUtility.RectangleContainsScreenPoint(
                        card.transform as RectTransform, pointer, camera))
                    {
                        cursorIndexes[playerIndex] = i;
                        ConfirmSelection(playerIndex);
                        return;
                    }
                }
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!selectedPlayers[playerIndex])
            {
                if (GameInput.UiPressed(UiAction.Left, keyboard)) MoveCursor(playerIndex, -1, 0);
                if (GameInput.UiPressed(UiAction.Right, keyboard)) MoveCursor(playerIndex, 1, 0);
                if (GameInput.UiPressed(UiAction.Up, keyboard)) MoveCursor(playerIndex, 0, -1);
                if (GameInput.UiPressed(UiAction.Down, keyboard)) MoveCursor(playerIndex, 0, 1);

                if (GameInput.UiPressed(UiAction.Submit, keyboard))
                {
                    ConfirmSelection(playerIndex);
                }
            }
            else if (GameInput.UiPressed(UiAction.Cancel, keyboard))
            {
                CancelSelection(playerIndex);
            }
        }

        private void StartGame()
        {
            if (!isFinalized && AreAllPlayersSelected())
                BeginGameStart(false);
        }

        private void ProcessGamepadInput(int playerIndex, Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return;
            }

            if (!selectedPlayers[playerIndex])
            {
                if (GameInput.UiPressed(UiAction.Left, gamepad)) MoveCursor(playerIndex, -1, 0);
                if (GameInput.UiPressed(UiAction.Right, gamepad)) MoveCursor(playerIndex, 1, 0);
                if (GameInput.UiPressed(UiAction.Up, gamepad)) MoveCursor(playerIndex, 0, -1);
                if (GameInput.UiPressed(UiAction.Down, gamepad)) MoveCursor(playerIndex, 0, 1);

                if (GameInput.UiPressed(UiAction.Submit, gamepad))
                {
                    ConfirmSelection(playerIndex);
                }
            }
            else if (GameInput.UiPressed(UiAction.Cancel, gamepad))
            {
                CancelSelection(playerIndex);
            }
        }

        private void MoveCursor(int playerIndex, int horizontal, int vertical)
        {
            int characterCount = OptionCount;
            if (characterCount == 0) return;

            int currentIndex = cursorIndexes[playerIndex];

            if (horizontal != 0)
            {
                cursorIndexes[playerIndex] = (currentIndex + horizontal + characterCount) % characterCount;
                return;
            }

            int rowCount = Mathf.CeilToInt(characterCount / (float)gridColumnCount);
            int row = currentIndex / gridColumnCount;
            int column = currentIndex % gridColumnCount;
            int targetRow = (row + vertical + rowCount) % rowCount;
            int targetIndex = targetRow * gridColumnCount + column;

            cursorIndexes[playerIndex] = targetIndex < characterCount
                ? targetIndex
                : characterCount - 1;
        }

        private void RefreshUI()
        {
            SyncFollowers();
            if (remainingTimeText != null)
            {
                remainingTimeText.text = $"남은 시간 {Mathf.CeilToInt(remainingTime)}";
            }

            int completedCount = 0;
            for (int i = 0; i < playerCount; i++)
                if (selectedPlayers[i]) completedCount++;
            SetText(selectionCompletedText, $"{completedCount}/{playerCount} 선택완료");
            if (startGameButton != null)
                startGameButton.interactable = !isFinalized && AreAllPlayersSelected();

            RefreshPlayerMarkers();
            RefreshPlayerPanels();
        }

        private void RefreshPlayerMarkers()
        {
            if (playerMarkerViews == null) return;

            int markerCount = Mathf.Min(playerMarkerViews.Length, MaxPlayerCount);

            for (int playerIndex = 0; playerIndex < markerCount; playerIndex++)
            {
                PlayerSelectionMarkerView marker = playerMarkerViews[playerIndex];
                if (marker == null) continue;

                if (playerIndex >= playerCount || IsFollower(playerIndex) || cursorIndexes[playerIndex] >= OptionCount)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                CharacterCardView card = GetCard(cursorIndexes[playerIndex]);
                int sameCardOrder = GetSameCardOrder(playerIndex);
                bool showFrame = sameCardOrder == 0;
                marker.MoveTo(
                    card != null ? card.MarkerTarget : null,
                    showFrame);
            }
        }

        private int GetSameCardOrder(int playerIndex)
        {
            int order = 0;
            int cardIndex = cursorIndexes[playerIndex];

            // 낮은 P 번호부터 원래 위치를 차지하고, 이후 플레이어는 오른쪽으로 이동한다.
            for (int otherPlayerIndex = 0; otherPlayerIndex < playerIndex; otherPlayerIndex++)
            {
                if (otherPlayerIndex < playerCount && !IsFollower(otherPlayerIndex) &&
                    cursorIndexes[otherPlayerIndex] == cardIndex)
                {
                    order++;
                }
            }

            return order;
        }

        private void RefreshPlayerPanels()
        {
            if (playerPanelViews == null) return;

            int viewCount = Mathf.Min(playerPanelViews.Length, MaxPlayerCount);

            for (int playerIndex = 0; playerIndex < viewCount; playerIndex++)
            {
                PlayerCharacterPanelView panel = playerPanelViews[playerIndex];
                if (panel == null) continue;

                bool active = playerIndex < playerCount;
                panel.gameObject.SetActive(active);

                if (!active || runtimeCharacters.Count == 0) continue;

                if (IsRandomOption(cursorIndexes[playerIndex]))
                    panel.RefreshRandom(playerIndex, selectedPlayers[playerIndex], randomPanelSprite);
                else
                    panel.Refresh(playerIndex, runtimeCharacters[GetCharacterIndex(cursorIndexes[playerIndex])], selectedPlayers[playerIndex]);
            }
        }

        private void BeginGameStart(bool randomizeUnselectedPlayers)
        {
            if (isFinalized) return;

            isFinalized = true;
            RefreshUI();
            StartCoroutine(FinalizeSelectionAfterDelay(randomizeUnselectedPlayers));
        }

        private IEnumerator FinalizeSelectionAfterDelay(bool randomizeUnselectedPlayers)
        {
            yield return new WaitForSecondsRealtime(1f);

            int[] selectedIds = new int[playerCount];

            // CHAMPION_PIECE_PREFAB_FLOW_START: InGameScene에서 플레이어별 챔피언 말 프리팹을 결정할 수 있도록 선택 CharacterData도 함께 전달합니다.
            CharacterData[] selectedCharacters = new CharacterData[playerCount];

            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                // 팀원은 대표가 최종 확정한 챔피언을 그대로 받는다 (대표 index가 항상 더 앞이라 이미 확정됨).
                if (IsFollower(playerIndex))
                {
                    int leader = leaderIndexes[playerIndex];
                    selectedPlayers[playerIndex] = true;
                    randomSelections[playerIndex] = selectedCharacters[leader];
                    selectedIds[playerIndex] = selectedIds[leader];
                    selectedCharacters[playerIndex] = selectedCharacters[leader];
                    continue;
                }

                if (randomizeUnselectedPlayers && !selectedPlayers[playerIndex])
                {
                    randomSelections[playerIndex] = DrawRandomCharacter(GetTeammateCharacters(playerIndex));
                    selectedPlayers[playerIndex] = true;
                }

                CharacterData selectedCharacter = randomSelections[playerIndex];
                if (selectedCharacter == null && IsRandomOption(cursorIndexes[playerIndex]))
                    selectedCharacter = randomSelections[playerIndex] = DrawRandomCharacter(GetTeammateCharacters(playerIndex));
                if (selectedCharacter == null)
                    selectedCharacter = runtimeCharacters[GetCharacterIndex(cursorIndexes[playerIndex])];
                // 기존 구현: selectedIds[playerIndex] = runtimeCharacters[cursorIndexes[playerIndex]].char_ID;
                selectedIds[playerIndex] = selectedCharacter.char_ID;
                selectedCharacters[playerIndex] = selectedCharacter;
            }

            // 기존 구현: 챔피언 ID만 전달했습니다.
            // CharacterSelectionResult.Set(selectedIds, playerCount);
            CharacterSelectionResult.Set(selectedIds, selectedCharacters, playerCount);
            // CHAMPION_PIECE_PREFAB_FLOW_END
            RefreshUI();

            if (string.IsNullOrWhiteSpace(inGameSceneName))
            {
                Debug.LogWarning("In-game scene name is empty.", this);
                isFinalized = false;
                RefreshUI();
                yield break;
            }

            if (gameStartLoadingPanel != null)
            {
                gameStartLoadingPanel.SetPlayers(selectedCharacters, playerCount, playerMarkerViews);
                gameStartLoadingPanel.LoadScene(inGameSceneName);
            }
            else
                SceneManager.LoadScene(inGameSceneName);
        }

        private bool AreAllPlayersSelected()
        {
            if (playerCount <= 0) return false;

            for (int i = 0; i < playerCount; i++)
            {
                if (!selectedPlayers[i]) return false;
            }

            return true;
        }

        private bool RandomPickEnabled => randomCardView != null && randomCardView.gameObject.activeInHierarchy;

        private int OptionCount => Mathf.Min(cardViews != null ? cardViews.Length : 0, runtimeCharacters.Count)
            + (RandomPickEnabled ? 1 : 0);

        private bool IsRandomOption(int optionIndex) => RandomPickEnabled && optionIndex == 0;

        private int GetCharacterIndex(int optionIndex) => optionIndex - (RandomPickEnabled ? 1 : 0);

        private CharacterCardView GetCard(int optionIndex)
        {
            if (IsRandomOption(optionIndex)) return randomCardView;
            int index = GetCharacterIndex(optionIndex);
            return cardViews != null && index >= 0 && index < cardViews.Length ? cardViews[index] : null;
        }

        private CharacterData DrawRandomCharacter() => runtimeCharacters[UnityEngine.Random.Range(0, runtimeCharacters.Count)];

        // excluded 에 없는 캐릭터 중에서 랜덤 (남은 게 없으면 전체에서)
        private CharacterData DrawRandomCharacter(HashSet<CharacterData> excluded)
        {
            if (excluded == null || excluded.Count == 0) return DrawRandomCharacter();
            var candidates = runtimeCharacters.FindAll(c => !excluded.Contains(c));
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : DrawRandomCharacter();
        }

        private void ConfirmSelection(int playerIndex)
        {
            HashSet<CharacterData> teammateCharacters = GetTeammateCharacters(playerIndex);
            if (IsRandomOption(cursorIndexes[playerIndex]))
            {
                randomSelections[playerIndex] = DrawRandomCharacter(teammateCharacters);
            }
            else
            {
                // 2대2: 같은 팀이 이미 고른 캐릭터는 확정 불가
                int index = GetCharacterIndex(cursorIndexes[playerIndex]);
                if (index >= 0 && index < runtimeCharacters.Count && teammateCharacters.Contains(runtimeCharacters[index]))
                    return;
                randomSelections[playerIndex] = null;
            }
            selectedPlayers[playerIndex] = true;
        }

        private void CancelSelection(int playerIndex)
        {
            selectedPlayers[playerIndex] = false;
            randomSelections[playerIndex] = null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
