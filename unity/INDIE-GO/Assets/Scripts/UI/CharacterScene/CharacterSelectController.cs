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
        [Tooltip("플레이어 표시 간격")]
        [SerializeField, Min(0f)] private float sameCardPlayerObjectSpacing = 36f;
        [Tooltip("인게임 씬 이름")]
        [SerializeField] private string inGameSceneName = "InGameScene";

        [Header("Common UI")]
        [Tooltip("남은 시간 텍스트")]
        [SerializeField] private TMP_Text remainingTimeText;
        [Tooltip("선택 완료 인원 텍스트")]
        [SerializeField] private TMP_Text selectedCountText;
        [Tooltip("바로 시작 버튼")]
        [SerializeField] private Button startNowButton;

        [Header("Runtime Fallback UI")]
        [Tooltip("Shows a complete text-based selector when character portraits/UI are incomplete.")]
        [SerializeField] private bool showRuntimeSelectionUI = true;

        [Header("Character UI")]
        [Tooltip("캐릭터 카드 목록")]
        [SerializeField] private CharacterCardView[] cardViews;
        [Tooltip("플레이어 마커 목록")]
        [SerializeField] private PlayerSelectionMarkerView[] playerMarkerViews;
        [Tooltip("플레이어 상세 패널 목록")]
        [SerializeField] private PlayerCharacterPanelView[] playerPanelViews;

        private readonly int[] cursorIndexes = new int[MaxPlayerCount];
        private readonly bool[] selectedPlayers = new bool[MaxPlayerCount];
        private readonly Gamepad[] playerGamepads = new Gamepad[MaxPlayerCount];
        private readonly List<CharacterData> runtimeCharacters = new List<CharacterData>();

        private int playerCount;
        private int keyboardPlayerIndex;
        private float remainingTime;
        private bool isFinalized;
        private Vector2 runtimeScrollPosition;
        private GUIStyle runtimeTitleStyle;
        private GUIStyle runtimeCardStyle;
        private GUIStyle runtimeSelectedCardStyle;

        private void Awake()
        {
            CharacterSelectionResult.Clear();
            BuildRuntimeCharacterList();
            ResolvePlayers();
            remainingTime = selectionTimeSeconds;

            if (startNowButton != null)
            {
                startNowButton.onClick.AddListener(TryStartNow);
            }

            InitializeCards();
            InitializePlayerMarkers();
            RefreshUI();
        }

        private void OnDestroy()
        {
            if (startNowButton != null)
            {
                startNowButton.onClick.RemoveListener(TryStartNow);
            }
        }

        private void Update()
        {
            if (isFinalized || runtimeCharacters.Count == 0)
            {
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);
            ProcessInputs();

            if (remainingTime <= 0f)
            {
                FinalizeSelection(true);
                return;
            }

            RefreshUI();
        }

        private void OnGUI()
        {
            if (!showRuntimeSelectionUI || isFinalized || runtimeCharacters.Count == 0)
                return;

            GUI.depth = -100;
            EnsureRuntimeStyles();

            float width = Mathf.Min(Screen.width - 40f, 1120f);
            float height = Mathf.Min(Screen.height - 40f, 760f);
            Rect area = new Rect(
                (Screen.width - width) * .5f,
                (Screen.height - height) * .5f,
                width,
                height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("CHAMPION PICK", runtimeTitleStyle);
            DrawRuntimePlayerSummary();

            int activePlayer = Mathf.Clamp(keyboardPlayerIndex, 0, Mathf.Max(0, playerCount - 1));
            GUILayout.Label(
                $"P{activePlayer + 1} character selection — click a card, then confirm");

            runtimeScrollPosition = GUILayout.BeginScrollView(runtimeScrollPosition);
            const int columns = 4;
            for (int start = 0; start < runtimeCharacters.Count; start += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int characterIndex = start + column;
                    if (characterIndex >= runtimeCharacters.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    CharacterData data = runtimeCharacters[characterIndex];
                    bool highlighted = cursorIndexes[activePlayer] == characterIndex;
                    GUIStyle style = highlighted ? runtimeSelectedCardStyle : runtimeCardStyle;
                    string characterName = string.IsNullOrWhiteSpace(data.char_Name)
                        ? $"CHAR {data.char_ID:000}"
                        : data.char_Name;
                    string passive = string.IsNullOrWhiteSpace(data.passive_Name)
                        ? "Passive: -"
                        : $"Passive: {data.passive_Name}";
                    string active = string.IsNullOrWhiteSpace(data.active_Name)
                        ? "Active: -"
                        : $"Active: {data.active_Name}";

                    if (GUILayout.Button(
                            $"{characterName}\n{passive}\n{active}",
                            style,
                            GUILayout.MinWidth(220f),
                            GUILayout.Height(92f)))
                    {
                        cursorIndexes[activePlayer] = characterIndex;
                        if (selectedPlayers[activePlayer])
                            selectedPlayers[activePlayer] = false;
                        RefreshUI();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Confirm P{activePlayer + 1}", GUILayout.Height(44f)))
                ConfirmRuntimePlayer(activePlayer);

            GUI.enabled = AreAllPlayersSelected();
            if (GUILayout.Button("START GAME", GUILayout.Height(44f))) TryStartNow();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawRuntimePlayerSummary()
        {
            GUILayout.BeginHorizontal();
            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                CharacterData data = runtimeCharacters[cursorIndexes[playerIndex]];
                string state = selectedPlayers[playerIndex] ? "READY" : "SELECTING";
                GUILayout.Label(
                    $"P{playerIndex + 1}: {data.char_Name}\n{state}",
                    GUI.skin.box,
                    GUILayout.MinWidth(150f),
                    GUILayout.Height(44f));
            }
            GUILayout.EndHorizontal();
        }

        private void ConfirmRuntimePlayer(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerCount) return;
            selectedPlayers[playerIndex] = true;
            int next = FindNextKeyboardPlayer(playerIndex + 1);
            if (next >= 0) keyboardPlayerIndex = next;
            RefreshUI();
        }

        private void EnsureRuntimeStyles()
        {
            if (runtimeTitleStyle != null) return;

            runtimeTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            runtimeCardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            runtimeSelectedCardStyle = new GUIStyle(runtimeCardStyle);
            runtimeSelectedCardStyle.normal.textColor = new Color(.25f, .9f, 1f);
            runtimeSelectedCardStyle.fontStyle = FontStyle.Bold;
        }

        public void TryStartNow()
        {
            if (!isFinalized && AreAllPlayersSelected())
            {
                FinalizeSelection(false);
            }
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

            // P1 uses the keyboard. P2-P4 use gamepads in connection order.
            for (int playerIndex = 1; playerIndex < playerCount; playerIndex++)
            {
                int gamepadIndex = playerIndex - 1;
                playerGamepads[playerIndex] = gamepadIndex < Gamepad.all.Count
                    ? Gamepad.all[gamepadIndex]
                    : null;
            }

            keyboardPlayerIndex = FindNextKeyboardPlayer(0);
        }

        private void InitializeCards()
        {
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
            if (playerMarkerViews == null)
            {
                return;
            }

            int markerCount = Mathf.Min(playerMarkerViews.Length, MaxPlayerCount);

            for (int playerIndex = 0; playerIndex < markerCount; playerIndex++)
            {
                PlayerSelectionMarkerView marker = playerMarkerViews[playerIndex];
                if (marker == null) continue;

                marker.Initialize(playerIndex);
                marker.gameObject.SetActive(playerIndex < playerCount);
            }
        }

        private void ProcessInputs()
        {
            if (keyboardPlayerIndex >= 0)
                ProcessKeyboardInput(keyboardPlayerIndex);

            for (int playerIndex = 1; playerIndex < playerCount; playerIndex++)
            {
                if (playerGamepads[playerIndex] != null)
                    ProcessGamepadInput(playerIndex, playerGamepads[playerIndex]);
            }
        }

        private void ProcessKeyboardInput(int playerIndex)
        {
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
                    selectedPlayers[playerIndex] = true;
                    int next = FindNextKeyboardPlayer(playerIndex + 1);
                    if (next >= 0) keyboardPlayerIndex = next;
                }
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                selectedPlayers[playerIndex] = false;
            }
            else if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                     AreAllPlayersSelected())
            {
                TryStartNow();
            }
        }

        private int FindNextKeyboardPlayer(int startIndex)
        {
            for (int offset = 0; offset < playerCount; offset++)
            {
                int playerIndex = (startIndex + offset) % playerCount;
                bool hasDedicatedGamepad = playerIndex > 0 && playerGamepads[playerIndex] != null;
                if (!hasDedicatedGamepad && !selectedPlayers[playerIndex])
                    return playerIndex;
            }

            return -1;
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
                    selectedPlayers[playerIndex] = true;
                }
            }
            else if (gamepad.buttonEast.wasPressedThisFrame)
            {
                selectedPlayers[playerIndex] = false;
            }
        }

        private void MoveCursor(int playerIndex, int horizontal, int vertical)
        {
            int characterCount = runtimeCharacters.Count;
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
            int selectedCount = GetSelectedCount();

            if (remainingTimeText != null)
            {
                remainingTimeText.text = $"남은 시간 {Mathf.CeilToInt(remainingTime)}";
            }

            if (selectedCountText != null)
            {
                selectedCountText.text = $"{selectedCount}/{playerCount} 선택 완료";
            }

            if (startNowButton != null)
            {
                startNowButton.interactable = AreAllPlayersSelected();
            }

            RefreshPlayerMarkers();
            RefreshPlayerPanels();
        }

        private void RefreshPlayerMarkers()
        {
            if (cardViews == null || playerMarkerViews == null) return;

            int markerCount = Mathf.Min(playerMarkerViews.Length, MaxPlayerCount);

            for (int playerIndex = 0; playerIndex < markerCount; playerIndex++)
            {
                PlayerSelectionMarkerView marker = playerMarkerViews[playerIndex];
                if (marker == null) continue;

                if (playerIndex >= playerCount || cursorIndexes[playerIndex] >= cardViews.Length)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                CharacterCardView card = cardViews[cursorIndexes[playerIndex]];
                int sameCardOrder = GetSameCardOrder(playerIndex);
                bool showFrame = sameCardOrder == 0;

                marker.MoveTo(
                    card != null ? card.MarkerTarget : null,
                    showFrame,
                    sameCardOrder,
                    sameCardPlayerObjectSpacing);
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

                CharacterData data = runtimeCharacters[cursorIndexes[playerIndex]];
                panel.Refresh(playerIndex, data, selectedPlayers[playerIndex]);
            }
        }

        private void FinalizeSelection(bool randomizeUnselectedPlayers)
        {
            isFinalized = true;
            int[] selectedIds = new int[playerCount];

            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                if (randomizeUnselectedPlayers && !selectedPlayers[playerIndex])
                {
                    cursorIndexes[playerIndex] = UnityEngine.Random.Range(0, runtimeCharacters.Count);
                    selectedPlayers[playerIndex] = true;
                }

                selectedIds[playerIndex] = runtimeCharacters[cursorIndexes[playerIndex]].char_ID;
            }

            CharacterSelectionResult.Set(selectedIds, playerCount);
            RefreshUI();

            if (string.IsNullOrWhiteSpace(inGameSceneName))
            {
                Debug.LogWarning("In-game scene name is empty.", this);
                isFinalized = false;
                return;
            }

            SceneManager.LoadScene(inGameSceneName);
        }

        private int GetSelectedCount()
        {
            int count = 0;

            for (int i = 0; i < playerCount; i++)
            {
                if (selectedPlayers[i]) count++;
            }

            return count;
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
    }
}
