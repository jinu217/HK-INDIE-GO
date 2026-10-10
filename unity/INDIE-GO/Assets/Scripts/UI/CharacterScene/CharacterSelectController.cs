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

        [Header("Lobby Settings UI")]
        [Tooltip("게임 모드 텍스트")]
        [SerializeField] private TMP_Text gameModeText;
        [Tooltip("플레이어 수 텍스트")]
        [SerializeField] private TMP_Text playerCountText;
        [Tooltip("팀 구성 텍스트")]
        [SerializeField] private TMP_Text teamCompositionText;
        [Tooltip("맵 텍스트")]
        [SerializeField] private TMP_Text mapText;
        [Tooltip("턴 수 텍스트")]
        [SerializeField] private TMP_Text turnCountText;

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
            RefreshLobbySettingsUI();

            remainingTime = selectionTimeSeconds;

            if (backButton != null)
            {
                backButton.onClick.AddListener(BackToLocalLobby);
            }
            if (startGameButton != null)
                startGameButton.onClick.AddListener(StartGame);

            InitializeCards();
            InitializePlayerMarkers();
            RefreshUI();
        }

        private void RefreshLobbySettingsUI()
        {
            GameStartSettings settings = GameStartSettingsHolder.Current;

            if (settings == null)
            {
                SetText(gameModeText, "-");
                SetText(playerCountText, $"{playerCount} Players");
                SetText(teamCompositionText, "-");
                SetText(mapText, "-");
                SetText(turnCountText, "-");
                return;
            }

            SetText(gameModeText, GetGameModeText(settings.gameMode));
            SetText(playerCountText, $"{settings.playerCount} Players");
            SetText(teamCompositionText, GetTeamCompositionText(settings));
            SetText(mapText, GetMapText(settings.mapType));
            SetText(turnCountText, $"{settings.maxTurnCount} Turns");
        }

        private static string GetGameModeText(GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.Classic => "Classic",
                GameMode.Escape => "Escape",
                GameMode.KillTheKing => "Kill The King",
                _ => gameMode.ToString()
            };
        }

        private static string GetMapText(MapType mapType)
        {
            return mapType switch
            {
                MapType.Random => "Random",
                MapType.Basic => "Basic",
                MapType.Grassland => "Grassland",
                MapType.Korean => "Korean",
                _ => mapType.ToString()
            };
        }

        private static string GetTeamCompositionText(GameStartSettings settings)
        {
            if (!settings.isTeamMode)
            {
                return "Solo";
            }

            return settings.matchComposition switch
            {
                MatchComposition.TwoVsTwo => "2:2",
                MatchComposition.ThreeVsThree => "3:3",
                MatchComposition.FourVsFour => "4:4",
                MatchComposition.TwoVsTwoVsTwo => "2:2:2",
                MatchComposition.TwoVsTwoVsTwoVsTwo => "2:2:2:2",
                _ => "Team"
            };
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
        }

        private void Update()
        {
            if (isFinalized || runtimeCharacters.Count == 0)
            {
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
                marker.gameObject.SetActive(playerIndex < playerCount);
            }
        }

        private void ProcessInputs()
        {
            LocalSelectionInput.Poll(playerCount);
            keyboardPlayerIndex = -1;
            int lastSharedPlayer = -1;
            for (int i = 0; i < playerCount; i++)
            {
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
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            {
                if (!selectedPlayers[keyboardPlayerIndex])
                {
                    for (int i = keyboardPlayerIndex - 1; i >= 0; i--)
                        if (LocalSelectionInput.GetGamepad(i) == null)
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
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !selectedPlayers[playerIndex])
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
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
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) MoveCursor(playerIndex, -1, 0);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) MoveCursor(playerIndex, 1, 0);
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveCursor(playerIndex, 0, -1);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveCursor(playerIndex, 0, 1);

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmSelection(playerIndex);
                }
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelSelection(playerIndex);
            }
        }

        private void StartGame()
        {
            if (!isFinalized && AreAllPlayersSelected())
                BeginGameStart(false);
        }

        private void OnGUI()
        {
            if (isFinalized) return;
            GUI.Label(new Rect(16, 16, 800, 28),
                $"{(LocalSelectionInput.SharedGamepadSelected ? "Shared Gamepad" : "Keyboard")} P{keyboardPlayerIndex + 1}: Select / Confirm / Cancel | Extra gamepads: A to join");
            string devices = string.Empty;
            for (int i = 0; i < playerCount; i++)
            {
                string device = LocalSelectionInput.GetGamepad(i) != null ? "Gamepad" :
                    LocalSelectionInput.SharedGamepadSelected ? "Shared Gamepad" : "Keyboard";
                devices += $"P{i + 1}: {device}  ";
            }
            GUI.Label(new Rect(16, 46, 800, 28), devices);
        }

        private void ProcessGamepadInput(int playerIndex, Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return;
            }

            if (!selectedPlayers[playerIndex])
            {
                if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame) MoveCursor(playerIndex, -1, 0);
                if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame) MoveCursor(playerIndex, 1, 0);
                if (gamepad.dpad.up.wasPressedThisFrame || gamepad.leftStick.up.wasPressedThisFrame) MoveCursor(playerIndex, 0, -1);
                if (gamepad.dpad.down.wasPressedThisFrame || gamepad.leftStick.down.wasPressedThisFrame) MoveCursor(playerIndex, 0, 1);

                if (gamepad.buttonSouth.wasPressedThisFrame)
                {
                    ConfirmSelection(playerIndex);
                }
            }
            else if (gamepad.buttonEast.wasPressedThisFrame)
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

                if (playerIndex >= playerCount || cursorIndexes[playerIndex] >= OptionCount)
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
                if (otherPlayerIndex < playerCount && cursorIndexes[otherPlayerIndex] == cardIndex)
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
                if (randomizeUnselectedPlayers && !selectedPlayers[playerIndex])
                {
                    randomSelections[playerIndex] = DrawRandomCharacter();
                    selectedPlayers[playerIndex] = true;
                }

                CharacterData selectedCharacter = randomSelections[playerIndex];
                if (selectedCharacter == null && IsRandomOption(cursorIndexes[playerIndex]))
                    selectedCharacter = randomSelections[playerIndex] = DrawRandomCharacter();
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
                gameStartLoadingPanel.SetPlayers(selectedCharacters, playerCount);
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

        private void ConfirmSelection(int playerIndex)
        {
            randomSelections[playerIndex] = IsRandomOption(cursorIndexes[playerIndex]) ? DrawRandomCharacter() : null;
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
