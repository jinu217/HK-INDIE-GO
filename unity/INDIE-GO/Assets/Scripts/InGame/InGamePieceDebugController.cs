using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.InGame
{
    /// <summary>
    /// Playable local-game presentation for InGameScene. It binds to the piece
    /// objects created by PlayerController and provides a runtime IMGUI control
    /// panel until the authored UI/effects layer is ready.
    /// </summary>
    public sealed class InGamePieceDebugController : MonoBehaviour
    {
        private readonly Dictionary<BoardTileId, Vector3> boardPositions =
            new Dictionary<BoardTileId, Vector3>();
        private readonly List<DebugPieceView> pieceViews = new List<DebugPieceView>();
        private readonly List<YutThrowData> pendingResults = new List<YutThrowData>();

        private PlayerManager playerManager;
        private TestTurnManager turnManager;
        private TestGameManager gameManager;
        private int selectedResultIndex;
        private int activeCasterPieceId = -1;
        private string feedback = "Space: throw yut / Click a piece: move";
        private GameResultData gameResult;
        private GUIStyle titleStyle;
        private GUIStyle statusStyle;

        private void Start() => StartCoroutine(InitializeAfterGameStarts());

        private IEnumerator InitializeAfterGameStarts()
        {
            // Bootstrap and PlayerManager complete during Awake/Start.
            yield return null;

            playerManager = FindFirstObjectByType<PlayerManager>();
            turnManager = FindFirstObjectByType<TestTurnManager>();
            gameManager = FindFirstObjectByType<TestGameManager>();
            if (playerManager == null || turnManager == null || gameManager == null)
            {
                feedback = "Required in-game managers are missing.";
                Debug.LogError(feedback, this);
                yield break;
            }

            BuildBoard();
            BindSpawnedPieces();

            turnManager.OnPendingResultsChanged += HandlePendingResultsChanged;
            turnManager.OnTurnStarted += HandleTurnStarted;
            gameManager.OnGameEnded += HandleGameEnded;
            feedback = $"{GetPlayerLabel(turnManager.CurrentTurn.currentPlayer)} turn. Throw the yut.";
        }

        private void Update()
        {
            if (playerManager == null || turnManager == null || gameResult != null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame &&
                    turnManager.CurrentTurn.currentPhase == TurnPhase.WaitThrow)
                    RequestThrow();

                if (keyboard.qKey.wasPressedThisFrame) SelectResult(-1);
                if (keyboard.eKey.wasPressedThisFrame) SelectResult(1);
                if (keyboard.digit1Key.wasPressedThisFrame) TryMovePieceById(0);
                if (keyboard.digit2Key.wasPressedThisFrame) TryMovePieceById(1);
                if (keyboard.digit3Key.wasPressedThisFrame) TryMovePieceById(2);
                if (keyboard.digit4Key.wasPressedThisFrame) TryMovePieceById(3);
                if (keyboard.escapeKey.wasPressedThisFrame) CancelActiveTargeting();
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                TrySelectPieceAtPointer();

            RefreshPiecePositions();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawControlPanel();
            if (gameResult != null) DrawResultOverlay();
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnPendingResultsChanged -= HandlePendingResultsChanged;
                turnManager.OnTurnStarted -= HandleTurnStarted;
            }
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
        }

        private void BindSpawnedPieces()
        {
            pieceViews.Clear();
            foreach (PlayerController player in playerManager.ActivePlayers)
            {
                IReadOnlyList<GameObject> objects = player.SpawnedPieceObjects;
                if (objects.Count != player.RuntimeData.Pieces.Count)
                {
                    Debug.LogError(
                        $"{player.name}: runtime piece objects ({objects.Count}) do not match data " +
                        $"({player.RuntimeData.Pieces.Count}).",
                        player);
                }

                int count = Mathf.Min(objects.Count, player.RuntimeData.Pieces.Count);
                for (int pieceId = 0; pieceId < count; pieceId++)
                {
                    GameObject pieceObject = objects[pieceId];
                    if (pieceObject == null) continue;
                    DebugPieceView view = pieceObject.GetComponent<DebugPieceView>();
                    if (view == null) view = pieceObject.AddComponent<DebugPieceView>();
                    view.Configure(player.PlayerId, pieceId, GetPlayerColor(player.PlayerId));
                    pieceViews.Add(view);
                }
            }
        }

        private void DrawControlPanel()
        {
            const float width = 350f;
            Rect area = new Rect(Screen.width - width - 16f, 16f, width, Screen.height - 32f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("YUT ARENA", titleStyle);

            if (turnManager == null)
            {
                GUILayout.Label(feedback, statusStyle);
                GUILayout.EndArea();
                return;
            }

            TurnContext turn = turnManager.CurrentTurn;
            GUILayout.Label(
                $"Round {turn.roundNumber}  |  Turn {turn.turnNumber}\n" +
                $"{GetPlayerLabel(turn.currentPlayer)}  |  {GetPhaseLabel(turn.currentPhase)}",
                statusStyle);

            GUI.enabled = gameResult == null && turn.currentPhase == TurnPhase.WaitThrow;
            if (GUILayout.Button("THROW YUT  [Space]", GUILayout.Height(42f))) RequestThrow();
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label("Throw results (Q / E to select)");
            if (pendingResults.Count == 0)
            {
                GUILayout.Label("- no saved result -");
            }
            else
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < pendingResults.Count; i++)
                {
                    bool selected = i == selectedResultIndex;
                    Color previous = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = new Color(.3f, .85f, 1f);
                    if (GUILayout.Button(GetYutLabel(pendingResults[i].result), GUILayout.Height(34f)))
                        selectedResultIndex = i;
                    GUI.backgroundColor = previous;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            GUILayout.Label("Current player's pieces");
            DrawPieceControls(turn);

            GUILayout.FlexibleSpace();
            GUILayout.Label(feedback, statusStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Controls\nSpace: throw\nQ/E: select result\n1-4 or board click: move\nEsc: cancel skill target");
            GUILayout.EndArea();
        }

        private void DrawPieceControls(TurnContext turn)
        {
            int playerId = (int)turn.currentPlayer;
            if (!playerManager.TryGetPlayer(playerId, out PlayerController player)) return;

            bool canMove = turn.currentPhase == TurnPhase.WaitAction && pendingResults.Count > 0;
            bool canUseActive = turn.currentPhase == TurnPhase.WaitThrow ||
                                turn.currentPhase == TurnPhase.WaitAction;

            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(
                    $"#{piece.PieceId + 1}  {GetPieceStateLabel(piece)}",
                    GUILayout.Width(155f));

                GUI.enabled = canMove && piece.CurrentCc != CcDefine.Stun &&
                              piece.State != PieceState.Goal;
                if (GUILayout.Button("Move", GUILayout.Width(70f))) TryMovePieceById(piece.PieceId);

                GUI.enabled = canUseActive && CharacterSkillRegistry.TryGet(
                    playerId,
                    piece.PieceId,
                    out _);
                if (GUILayout.Button("Active", GUILayout.Width(75f))) BeginActive(piece.PieceId);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawResultOverlay()
        {
            Rect overlay = new Rect(Screen.width * .5f - 220f, Screen.height * .5f - 125f, 440f, 250f);
            GUILayout.BeginArea(overlay, GUI.skin.window);
            GUILayout.Label("GAME OVER", titleStyle);
            GUILayout.Label(
                $"Winner: {gameResult.winningTeam}\nReason: {gameResult.resultType}",
                statusStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Play Again", GUILayout.Height(40f)))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if (GUILayout.Button("Return to Lobby", GUILayout.Height(36f)))
                SceneManager.LoadScene("LocalLobbyScene");
            GUILayout.EndArea();
        }

        private void RequestThrow()
        {
            turnManager.RequestThrow();
            feedback = $"Yut result: {GetYutLabel(turnManager.CurrentTurn.lastYutResult)}";
        }

        private void TryMovePieceById(int pieceId)
        {
            if (turnManager == null || pendingResults.Count == 0 ||
                turnManager.CurrentTurn.currentPhase != TurnPhase.WaitAction)
                return;

            selectedResultIndex = Mathf.Clamp(selectedResultIndex, 0, pendingResults.Count - 1);
            YutResult selected = pendingResults[selectedResultIndex].result;
            turnManager.RequestMovePiece(pieceId, selected);
            feedback = $"Piece {pieceId + 1} used {GetYutLabel(selected)}.";
        }

        private void BeginActive(int pieceId)
        {
            int playerId = (int)turnManager.CurrentTurn.currentPlayer;
            CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(
                new CharacterActiveRequest(playerId, pieceId));
            if (result.Succeeded)
            {
                feedback = string.IsNullOrEmpty(result.Message) ? "Active skill used." : result.Message;
                activeCasterPieceId = -1;
                return;
            }

            activeCasterPieceId = pieceId;
            feedback = result.Message + " Click a target piece, or press Esc to cancel.";
        }

        private void TryUseActiveOnTarget(DebugPieceView target)
        {
            int playerId = (int)turnManager.CurrentTurn.currentPlayer;
            CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(
                new CharacterActiveRequest(
                    playerId,
                    activeCasterPieceId,
                    target.PlayerId,
                    target.PieceId));
            feedback = result.Message;
            if (result.Succeeded) activeCasterPieceId = -1;
        }

        private void CancelActiveTargeting()
        {
            if (activeCasterPieceId < 0) return;
            activeCasterPieceId = -1;
            feedback = "Active skill targeting canceled.";
        }

        private void HandlePieceClicked(DebugPieceView view)
        {
            if (activeCasterPieceId >= 0)
            {
                TryUseActiveOnTarget(view);
                return;
            }

            if (view.PlayerId == (int)turnManager.CurrentTurn.currentPlayer)
                TryMovePieceById(view.PieceId);
        }

        private void TrySelectPieceAtPointer()
        {
            if (Camera.main == null || Mouse.current == null) return;
            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPosition);
            DebugPieceView view = hit != null ? hit.GetComponent<DebugPieceView>() : null;
            if (view != null) HandlePieceClicked(view);
        }

        private void SelectResult(int delta)
        {
            if (pendingResults.Count == 0) return;
            selectedResultIndex = (selectedResultIndex + delta + pendingResults.Count) % pendingResults.Count;
        }

        private void HandlePendingResultsChanged(List<YutThrowData> results)
        {
            pendingResults.Clear();
            pendingResults.AddRange(results);
            selectedResultIndex = Mathf.Clamp(selectedResultIndex, 0, Mathf.Max(0, pendingResults.Count - 1));
        }

        private void HandleTurnStarted(PlayerSlot player)
        {
            activeCasterPieceId = -1;
            feedback = $"{GetPlayerLabel(player)} turn.";
        }

        private void HandleGameEnded(GameResultData result)
        {
            gameResult = result;
            feedback = $"{result.winningTeam} wins.";
        }

        private void BuildBoard()
        {
            Sprite markerSprite = CreateCircleSprite();
            foreach (KeyValuePair<BoardTileId, Vector3> entry in CreateBoardLayout())
            {
                boardPositions[entry.Key] = entry.Value;
                var marker = new GameObject(entry.Key.ToString());
                marker.transform.SetParent(transform);
                marker.transform.position = entry.Value;
                marker.transform.localScale = Vector3.one * .62f;
                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = markerSprite;
                renderer.color = entry.Key == BoardTileId.Start
                    ? new Color(.45f, .75f, 1f)
                    : new Color(.72f, .72f, .72f);
                renderer.sortingOrder = 1;
            }
        }

        private void RefreshPiecePositions()
        {
            foreach (DebugPieceView view in pieceViews)
            {
                if (!playerManager.TryGetPlayer(view.PlayerId, out PlayerController player) ||
                    !player.TryGetPieceData(view.PieceId, out PlayerRuntimeData.PieceRuntimeData piece))
                    continue;

                view.transform.position = GetDisplayPosition(piece, view.PlayerId, view.PieceId);
                bool selectable = gameResult == null &&
                                  view.PlayerId == (int)turnManager.CurrentTurn.currentPlayer &&
                                  turnManager.CurrentTurn.currentPhase == TurnPhase.WaitAction;
                view.SetSelected(selectable);
                view.RefreshColor(selectable);
            }
        }

        private Vector3 GetDisplayPosition(
            PlayerRuntimeData.PieceRuntimeData piece,
            int playerId,
            int pieceId)
        {
            if (piece.State == PieceState.Waiting) return GetHomePosition(playerId, pieceId);
            if (piece.State == PieceState.Goal)
                return new Vector3((playerId - 2.5f) * 1.2f, -4.7f) + GetOffset(pieceId);

            BoardTileId tile = piece.CurrentTileId == BoardTileId.None
                ? BoardTileId.Start
                : piece.CurrentTileId;
            return boardPositions.TryGetValue(tile, out Vector3 position)
                ? position + GetOffset(pieceId)
                : GetHomePosition(playerId, pieceId);
        }

        private static Vector3 GetHomePosition(int playerId, int pieceId)
        {
            Vector3[] homes =
            {
                new Vector3(5.7f, -3.6f), new Vector3(5.7f, 3.6f),
                new Vector3(-5.7f, 3.6f), new Vector3(-5.7f, -3.6f)
            };
            int index = Mathf.Clamp(playerId - 1, 0, homes.Length - 1);
            return homes[index] + GetOffset(pieceId);
        }

        private static Vector3 GetOffset(int pieceId)
        {
            Vector2[] offsets =
            {
                new Vector2(-.38f, .38f), new Vector2(.38f, .38f),
                new Vector2(-.38f, -.38f), new Vector2(.38f, -.38f)
            };
            return offsets[pieceId % offsets.Length];
        }

        private static Dictionary<BoardTileId, Vector3> CreateBoardLayout() =>
            new Dictionary<BoardTileId, Vector3>
            {
                { BoardTileId.Start, new Vector3(4, -4) }, { BoardTileId.Outer01, new Vector3(4, -2.4f) }, { BoardTileId.Outer02, new Vector3(4, -.8f) }, { BoardTileId.Outer03, new Vector3(4, .8f) }, { BoardTileId.Outer04, new Vector3(4, 2.4f) }, { BoardTileId.Corner01, new Vector3(4, 4) },
                { BoardTileId.Outer05, new Vector3(2.4f, 4) }, { BoardTileId.Outer06, new Vector3(.8f, 4) }, { BoardTileId.Outer07, new Vector3(-.8f, 4) }, { BoardTileId.Outer08, new Vector3(-2.4f, 4) }, { BoardTileId.Corner02, new Vector3(-4, 4) },
                { BoardTileId.Outer09, new Vector3(-4, 2.4f) }, { BoardTileId.Outer10, new Vector3(-4, .8f) }, { BoardTileId.Outer11, new Vector3(-4, -.8f) }, { BoardTileId.Outer12, new Vector3(-4, -2.4f) }, { BoardTileId.Corner03, new Vector3(-4, -4) },
                { BoardTileId.Outer13, new Vector3(-2.4f, -4) }, { BoardTileId.Outer14, new Vector3(-.8f, -4) }, { BoardTileId.Outer15, new Vector3(.8f, -4) }, { BoardTileId.Outer16, new Vector3(2.4f, -4) },
                { BoardTileId.Inner01, new Vector3(2.65f, 2.65f) }, { BoardTileId.Inner02, new Vector3(1.3f, 1.3f) }, { BoardTileId.Center, Vector3.zero }, { BoardTileId.Inner03, new Vector3(-1.3f, -1.3f) }, { BoardTileId.Inner04, new Vector3(-2.65f, -2.65f) },
                { BoardTileId.Inner05, new Vector3(-2.65f, 2.65f) }, { BoardTileId.Inner06, new Vector3(-1.3f, 1.3f) }, { BoardTileId.Inner07, new Vector3(1.3f, -1.3f) }, { BoardTileId.Inner08, new Vector3(2.65f, -2.65f) }
            };

        private static string GetPlayerLabel(PlayerSlot player) =>
            player == PlayerSlot.None ? "Waiting" : $"Player {(int)player}";

        private static string GetPhaseLabel(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.WaitThrow: return "Throw yut";
                case TurnPhase.WaitAction: return "Choose result and piece";
                case TurnPhase.GameEnd: return "Game finished";
                default: return phase.ToString();
            }
        }

        private static string GetYutLabel(YutResult result)
        {
            switch (result)
            {
                case YutResult.BackDo: return "Back-do (-1)";
                case YutResult.Do: return "Do (1)";
                case YutResult.Gae: return "Gae (2)";
                case YutResult.Geol: return "Geol (3)";
                case YutResult.Yut: return "Yut (4)";
                case YutResult.Mo: return "Mo (5)";
                case YutResult.Nak: return "Nak";
                default: return result.ToString();
            }
        }

        private static string GetPieceStateLabel(PlayerRuntimeData.PieceRuntimeData piece)
        {
            if (piece.CurrentCc == CcDefine.Stun) return "Stunned";
            switch (piece.State)
            {
                case PieceState.Waiting: return "Home";
                case PieceState.Goal: return "Goal";
                default: return piece.CurrentTileId == BoardTileId.None
                    ? "Start"
                    : piece.CurrentTileId.ToString();
            }
        }

        private static Color GetPlayerColor(int playerId)
        {
            switch (playerId)
            {
                case 1: return new Color(.92f, .18f, .18f);
                case 2: return new Color(1f, .52f, .08f);
                case 3: return new Color(.95f, .85f, .08f);
                case 4: return new Color(.16f, .7f, .3f);
                default: return Color.white;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true
            };
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float alpha = Mathf.Clamp01(
                    size * .45f - Vector2.Distance(new Vector2(x, y), center) + 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }
    }
}
