using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.InGame
{
    /// <summary>Temporary visible board and piece selection layer for InGameScene.</summary>
    public sealed class InGamePieceDebugController : MonoBehaviour
    {
        private static readonly Vector3[] DefaultHomePositions =
        {
            new Vector3(5.7f, -3.6f), new Vector3(5.7f, 3.6f),
            new Vector3(-5.7f, 3.6f), new Vector3(-5.7f, -3.6f)
        };

        [Header("Piece Home Position Anchors")]
        [Tooltip("Player 1~4 말의 초기 대기 위치입니다. 빈 오브젝트를 씬에서 원하는 위치로 옮긴 뒤 순서대로 연결하세요.")]
        [SerializeField] private Transform[] playerHomeAnchors = new Transform[4];

        [Header("Piece Visual and Home Formation")]
        [Tooltip("생성되는 모든 말 프리팹의 기본 크기 배율입니다.")]
        [SerializeField, Min(0.01f)] private float pieceScaleMultiplier = 1f;

        [Header("Player 1 Home Formation")]
        [Tooltip("Player 1의 Piece 0~3 초기 배치 오프셋입니다.")]
        [FormerlySerializedAs("homePieceOffsets")]
        [SerializeField] private Vector3[] player1HomePieceOffsets = CreateDefaultHomePieceOffsets();

        [Header("Player 2 Home Formation")]
        [Tooltip("Player 2의 Piece 0~3 초기 배치 오프셋입니다.")]
        [SerializeField] private Vector3[] player2HomePieceOffsets = CreateDefaultHomePieceOffsets();

        [Header("Player 3 Home Formation")]
        [Tooltip("Player 3의 Piece 0~3 초기 배치 오프셋입니다.")]
        [SerializeField] private Vector3[] player3HomePieceOffsets = CreateDefaultHomePieceOffsets();

        [Header("Player 4 Home Formation")]
        [Tooltip("Player 4의 Piece 0~3 초기 배치 오프셋입니다.")]
        [SerializeField] private Vector3[] player4HomePieceOffsets = CreateDefaultHomePieceOffsets();

        private readonly Dictionary<BoardTileId, Vector3> boardPositions = new Dictionary<BoardTileId, Vector3>();
        private readonly List<DebugPieceView> pieceViews = new List<DebugPieceView>();
        private readonly List<YutThrowData> pendingResults = new List<YutThrowData>();
        private PlayerManager playerManager;
        private TestTurnManager turnManager;
        private MapManager mapManager;

        private void Start() => StartCoroutine(InitializeAfterGameStarts());

        private IEnumerator InitializeAfterGameStarts()
        {
            yield return null;
            playerManager = FindFirstObjectByType<PlayerManager>();
            turnManager = FindFirstObjectByType<TestTurnManager>();
            mapManager = FindFirstObjectByType<MapManager>();
            if (playerManager == null || turnManager == null)
            {
                Debug.LogError("InGamePieceDebugController requires PlayerManager and TestTurnManager.", this);
                yield break;
            }

            // Keep the old marker board only when no map prefab has been loaded.
            if (mapManager == null || !mapManager.IsMapLoaded)
                BuildBoard();
            foreach (PlayerController player in playerManager.ActivePlayers)
            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            {
                if (player.JobPiecePrefab == null)
                {
                    Debug.LogError($"{player.name} has no Job Piece Prefab assigned.", player);
                    break;
                }

                GameObject pieceObject = Instantiate(player.JobPiecePrefab, player.transform);
                DebugPieceView view = pieceObject.GetComponent<DebugPieceView>();
                if (view == null)
                    view = pieceObject.AddComponent<DebugPieceView>();
                view.Configure(player.PlayerId, piece.PieceId, GetPlayerColor(player.PlayerId));
                view.SetBaseScaleMultiplier(pieceScaleMultiplier);
                pieceViews.Add(view);
            }

            turnManager.OnPendingResultsChanged += HandlePendingResultsChanged;
        }

        private void Update()
        {
            if (playerManager == null || turnManager == null) return;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame &&
                turnManager.CurrentTurn.currentPhase == TurnPhase.WaitThrow)
                turnManager.RequestThrow();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                TrySelectPieceAtPointer();
            RefreshPiecePositions();
        }

        private void OnDestroy()
        {
            if (turnManager != null) turnManager.OnPendingResultsChanged -= HandlePendingResultsChanged;
        }

        private void HandlePieceClicked(DebugPieceView view)
        {
            if (turnManager.CurrentTurn.currentPhase != TurnPhase.WaitAction ||
                view.PlayerId != (int)turnManager.CurrentTurn.currentPlayer || pendingResults.Count == 0) return;

            turnManager.RequestMovePiece(view.PieceId, pendingResults[0].result);
        }

        private void TrySelectPieceAtPointer()
        {
            if (Camera.main == null) return;

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPosition);
            DebugPieceView view = hit != null ? hit.GetComponent<DebugPieceView>() : null;
            if (view != null) HandlePieceClicked(view);
        }

        private void HandlePendingResultsChanged(List<YutThrowData> results)
        {
            pendingResults.Clear();
            pendingResults.AddRange(results);
        }

        private void BuildBoard()
        {
            foreach (KeyValuePair<BoardTileId, Vector3> entry in CreateBoardLayout())
            {
                boardPositions.Add(entry.Key, entry.Value);
                var marker = new GameObject(entry.Key.ToString());
                marker.transform.SetParent(transform);
                marker.transform.position = entry.Value;
                marker.transform.localScale = Vector3.one * 0.62f;
                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = CreateCircleSprite();
                renderer.color = entry.Key == BoardTileId.Start ? new Color(0.8f, 0.9f, 1f) : new Color(0.85f, 0.85f, 0.85f);
                renderer.sortingOrder = 1;
            }
        }

        private void RefreshPiecePositions()
        {
            foreach (DebugPieceView view in pieceViews)
            {
                if (!playerManager.TryGetPlayer(view.PlayerId, out PlayerController player) ||
                    !player.TryGetPieceData(view.PieceId, out PlayerRuntimeData.PieceRuntimeData piece)) continue;
                view.transform.position = GetDisplayPosition(piece, view.PlayerId, view.PieceId);
                bool canSelect = turnManager.CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                                 view.PlayerId == (int)turnManager.CurrentTurn.currentPlayer;
                view.SetSelected(canSelect);
                view.RefreshColor(canSelect);
            }
        }

        private Vector3 GetDisplayPosition(PlayerRuntimeData.PieceRuntimeData piece, int playerId, int pieceId)
        {
            if (piece.State == PieceState.Waiting) return GetHomePosition(playerId, pieceId);
            if (piece.State == PieceState.Goal) return new Vector3((playerId - 2.5f) * 1.2f, -4.7f) + GetOffset(pieceId);
            BoardTileId tile = piece.CurrentTileId == BoardTileId.None ? BoardTileId.Start : piece.CurrentTileId;
            if (mapManager != null && mapManager.TryGetTilePosition(tile, out Vector3 mapTilePosition))
                return mapTilePosition + GetOffset(pieceId);

            return boardPositions[tile] + GetOffset(pieceId);
        }

        private Vector3 GetHomePosition(int playerId, int pieceId)
        {
            int playerIndex = playerId - 1;
            if (playerHomeAnchors != null &&
                playerIndex >= 0 &&
                playerIndex < playerHomeAnchors.Length &&
                playerHomeAnchors[playerIndex] != null)
            {
                return playerHomeAnchors[playerIndex].position + GetHomePieceOffset(playerId, pieceId);
            }

            // Anchor를 아직 연결하지 않은 기존 씬도 이전 대기 위치를 유지합니다.
            return DefaultHomePositions[playerIndex] + GetHomePieceOffset(playerId, pieceId);
        }

        private Vector3 GetHomePieceOffset(int playerId, int pieceId)
        {
            Vector3[] offsets = playerId switch
            {
                1 => player1HomePieceOffsets,
                2 => player2HomePieceOffsets,
                3 => player3HomePieceOffsets,
                4 => player4HomePieceOffsets,
                _ => null
            };

            if (offsets != null && offsets.Length > 0)
                return offsets[pieceId % offsets.Length];

            return GetOffset(pieceId);
        }

        private static Vector3[] CreateDefaultHomePieceOffsets()
        {
            return new[]
            {
                new Vector3(-.58f, .58f, 0f), new Vector3(.58f, .58f, 0f),
                new Vector3(-.58f, -.58f, 0f), new Vector3(.58f, -.58f, 0f)
            };
        }

        private static Vector3 GetOffset(int pieceId)
        {
            Vector2[] offsets = { new Vector2(-.58f, .58f), new Vector2(.58f, .58f), new Vector2(-.58f, -.58f), new Vector2(.58f, -.58f) };
            return offsets[pieceId % offsets.Length];
        }

        private static Dictionary<BoardTileId, Vector3> CreateBoardLayout() => new Dictionary<BoardTileId, Vector3>
        {
            { BoardTileId.Start, new Vector3(4, -4) }, { BoardTileId.Outer01, new Vector3(4, -2.4f) }, { BoardTileId.Outer02, new Vector3(4, -.8f) }, { BoardTileId.Outer03, new Vector3(4, .8f) }, { BoardTileId.Outer04, new Vector3(4, 2.4f) }, { BoardTileId.Corner01, new Vector3(4, 4) },
            { BoardTileId.Outer05, new Vector3(2.4f, 4) }, { BoardTileId.Outer06, new Vector3(.8f, 4) }, { BoardTileId.Outer07, new Vector3(-.8f, 4) }, { BoardTileId.Outer08, new Vector3(-2.4f, 4) }, { BoardTileId.Corner02, new Vector3(-4, 4) },
            { BoardTileId.Outer09, new Vector3(-4, 2.4f) }, { BoardTileId.Outer10, new Vector3(-4, .8f) }, { BoardTileId.Outer11, new Vector3(-4, -.8f) }, { BoardTileId.Outer12, new Vector3(-4, -2.4f) }, { BoardTileId.Corner03, new Vector3(-4, -4) },
            { BoardTileId.Outer13, new Vector3(-2.4f, -4) }, { BoardTileId.Outer14, new Vector3(-.8f, -4) }, { BoardTileId.Outer15, new Vector3(.8f, -4) }, { BoardTileId.Outer16, new Vector3(2.4f, -4) },
            { BoardTileId.Inner01, new Vector3(2.65f, 2.65f) }, { BoardTileId.Inner02, new Vector3(1.3f, 1.3f) }, { BoardTileId.Center, Vector3.zero }, { BoardTileId.Inner03, new Vector3(-1.3f, -1.3f) }, { BoardTileId.Inner04, new Vector3(-2.65f, -2.65f) },
            { BoardTileId.Inner05, new Vector3(-2.65f, 2.65f) }, { BoardTileId.Inner06, new Vector3(-1.3f, 1.3f) }, { BoardTileId.Inner07, new Vector3(1.3f, -1.3f) }, { BoardTileId.Inner08, new Vector3(2.65f, -2.65f) }
        };

        private static Color GetPlayerColor(int playerId) => playerId switch
        {
            1 => new Color(.92f, .18f, .18f), 2 => new Color(1f, .52f, .08f),
            3 => new Color(.95f, .85f, .08f), 4 => new Color(.16f, .7f, .3f), _ => Color.white
        };

        private static Sprite CreateCircleSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(size * .45f - Vector2.Distance(new Vector2(x, y), center) + 1)));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }
    }
}
