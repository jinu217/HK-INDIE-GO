using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.InGame
{
    /// <summary>
    /// 이동 선택 3단계 흐름 (WaitAction 단계). 마커는 MoveMarker 하나로 역할만 다르게 쓴다.
    /// 1) 윷을 다 던지면 → 보드 위 말은 직접 클릭, 대기 말은 공용 HUD의 말 버튼으로 선택.
    /// 2) 화살표 클릭 → 그 말이 남은 결과(모/개…)로 갈 수 있는 보드 칸에 노란 핀(MoveMarker.Destination, 안에 결과 이름).
    /// 3) 노란 핀 클릭 → 그 결과로 이동. 결과가 소비되면 다시 1단계(말 선택)로.
    /// </summary>
    public sealed class MoveDestinationSelector : MonoBehaviour
    {
        [Header("Dependencies (비우면 씬에서 자동 검색)")]
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private PieceMovementManager pieceMovementManager;
        [SerializeField] private MapManager mapManager;
        [SerializeField] private InGamePieceDebugController pieceDebugController;

        [Header("완주 마커 위치 (비우면 참먹이 칸의 탈출 지점 자동 계산)")]
        [SerializeField] private Transform finishMarkerAnchor;
        [Tooltip("참먹이에 '멈추는' 화살표를 정확히 놓을 자리 (씬에서 빈 오브젝트를 끌어다 넣기). 비우면 아래 Offset 사용.")]
        [SerializeField] private Transform startStopMarkerAnchor;
        [Header("참먹이 칸 나눠 쓰기 (참먹이 말 서는 자리 기준, 화면 방향 월드 거리: x = 오른쪽+, y = 위쪽+)")]
        [Tooltip("참먹이에 '멈추는' 이동의 화살표 위치. 기본: 말 자리에서 살짝 왼쪽 위.")]
        [SerializeField] private Vector2 startStopArrowOffset = new Vector2(-0.2f, 0.1f);
        [Tooltip("참먹이를 지나 '탈출'하는 이동의 깃발 위치. 기본: 말 자리에서 오른쪽 아래.")]
        [SerializeField] private Vector2 startTileEscapeOffset = new Vector2(0.38f, -0.38f);
        private int escapeFlagCount;   // 이번에 띄운 탈출 깃발 수 (항상 1개만 띄움)

        [Header("말 선택 화살표 (1단계)")]
        [SerializeField] private Color pieceArrowColor = new Color(0.45f, 0.85f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float pieceArrowScale = 0.35f;
        [Tooltip("말 원점에서 ▼ 촉까지 고정 높이 (모든 말 화살표 동일). 캐릭터 정수리 살짝 위가 되게 조정.")]
        [SerializeField] private float pieceArrowHeight = 1.15f;

        [Header("목적지 마커 (2단계)")]
        [SerializeField] private Color destColor = new Color(1f, 0.86f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float destScale = 0.45f;
        [Tooltip("목적지 ▼ 대신 쓸 화살표 이미지. Sprites/인게임창/arrow.png 를 끌어다 넣기 (조각으로 잘려 있어도 원본 전체를 한 장으로 씀). 비우면 기본 노란 ▼.")]
        [SerializeField] private Texture2D destinationArrowTexture;
        private Sprite destinationArrowSprite;
        [Tooltip("탈출(참먹이 통과) 표시 이미지. 이미지를 Sprites/인게임창 에 넣고 여기에 끌어다 넣기. 비우면 코드로 그린 임시 깃발.")]
        [SerializeField] private Texture2D escapeFlagTexture;
        private Sprite escapeFlagSprite;
        [Tooltip("말 없는 칸: ▼ 를 칸에서 이만큼 위에 표시 (살짝 위).")]
        [SerializeField] private float arrowHeightEmptyTile = 0.18f;
        [Tooltip("말 있는 칸: ▼ 를 말 1개 정수리에서 이만큼 위에 표시.")]
        [SerializeField] private float arrowHeightAbovePiece = 0.12f;

        [Header("칸 테두리 강조 (hover / 말 있는 칸) — 칸 Anchor 에 얹는 라운드 보더")]
        [Tooltip("테두리 한 변 길이(월드) = 타일 바깥 치수. 갈색 타일에 딱 맞을 때까지 조절.")]
        [SerializeField, Min(0.1f)] private float outlineTileSize = 1.25f;
        [Tooltip("큰 칸(참먹이, 모/뒷모/찌모/날모, 방) 테두리 한 변 길이. 일반 칸보다 큰 칸에 맞춰 조절. Play 중 실시간 반영.")]
        [SerializeField, Min(0.1f)] private float outlineTileSizeLarge = 1.25f;
        [Tooltip("테두리 위치 보정 (칸 Anchor 기준 보드 평면 X/Y). 칸 모델이 중앙에서 비껴 있으면 조절. Play 중 실시간 반영.")]
        [SerializeField] private Vector2 outlineOffset = Vector2.zero;
        [Tooltip("바깥/꼭지점 칸 테두리 회전(도). 보통 0.")]
        [SerializeField, Range(-90f, 90f)] private float outlineAngleDeg = 0f;
        [Tooltip("대각선(지름길) 칸 테두리 회전(도): Inner01~08 + Center. 대각 타일에 맞을 때까지 조절.")]
        [SerializeField, Range(-90f, 90f)] private float outlineAngleDegDiagonal = 45f;
        [Tooltip("테두리 굵기 (한 변 대비 비율). 새 보드 칸 모델 윤곽선이면 칸보다 이 비율만큼 크게 그림 (0.07 = 7%). Play 중 실시간 반영.")]
        [SerializeField, Range(0.02f, 0.25f)] private float outlineThickness = 0.07f;
        [Tooltip("모서리 둥글기 (한 변 대비 비율). 0 = 직각, 갈색 타일 라운드에 맞춰 조절.")]
        [SerializeField, Range(0f, 0.5f)] private float outlineCornerRadius = 0.16f;
        [Tooltip("테두리 색 (상시 = 칸에 말 있을 때).")]
        [SerializeField] private Color outlineColor = new Color(0.95f, 0.72f, 0.28f, 0.55f);
        [Tooltip("테두리 색 (마우스 호버 시). 더 밝게.")]
        [SerializeField] private Color outlineHoverColor = new Color(1f, 0.85f, 0.4f, 1f);

        [Header("화면 가장자리 UI 회피 (위/아래 패널에 안 가리게)")]
        [Tooltip("켜면 화면 아래/위 픽셀 안에 들어온 목적지 화살표·깃발을 칸에서 떼서 밀어냄. 새 화면에선 아래쪽 칸 표시가 엉뚱한 데 떠서 기본 끔.")]
        [SerializeField] private bool avoidScreenEdgeUi = false;
        [Tooltip("화면 아래 이 픽셀 높이 안이면 마커를 위로 민다 (타이머/던지기 버튼).")]
        [SerializeField] private float bottomUiPixels = 210f;
        [Tooltip("화면 위 이 픽셀 높이 안이면 마커를 아래로 민다 (GAME TIME 패널).")]
        [SerializeField] private float topUiPixels = 140f;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private readonly List<MoveMarker> pieceArrows = new List<MoveMarker>();
        private readonly List<MoveMarker> destMarkers = new List<MoveMarker>();
        private readonly List<YutThrowData> pendingResults = new List<YutThrowData>();
        private int selectedPieceId = -1;
        // 윤누리 첫 이동 +1 대기 중: 그 말의 추가 1칸 화살표만 띄우고, 빈 곳 클릭으로 취소 불가
        private bool forcedMoveActive;
        private bool subscribed;
        private MoveMarker lastHoverLogged;

        private void Start() => StartCoroutine(InitializeAfterGameStarts());

        private IEnumerator InitializeAfterGameStarts()
        {
            yield return null;

            if (turnManager == null) turnManager = FindFirstObjectByType<TestTurnManager>();
            if (playerManager == null) playerManager = FindFirstObjectByType<PlayerManager>();
            if (pieceMovementManager == null) pieceMovementManager = FindFirstObjectByType<PieceMovementManager>();
            if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
            if (pieceDebugController == null) pieceDebugController = FindFirstObjectByType<InGamePieceDebugController>();

            if (turnManager == null || playerManager == null || pieceMovementManager == null)
            {
                Debug.LogError("MoveDestinationSelector: 필수 매니저 참조를 찾지 못했습니다.", this);
                yield break;
            }

            turnManager.OnPendingResultsChanged += HandlePendingResultsChanged;
            turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            turnManager.OnTurnEnded += HandleTurnEnded;
            subscribed = true;

            pendingResults.Clear();
            pendingResults.AddRange(turnManager.PendingResults);
            RefreshForCurrentState();
        }

        private void OnDestroy()
        {
            if (!subscribed || turnManager == null) return;
            turnManager.OnPendingResultsChanged -= HandlePendingResultsChanged;
            turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            turnManager.OnTurnEnded -= HandleTurnEnded;
        }

        private void HandlePendingResultsChanged(List<YutThrowData> results)
        {
            pendingResults.Clear();
            if (results != null)
                pendingResults.AddRange(results);
            // 던졌거나 결과를 하나 소비함 → 말 선택 단계로 되돌린다(이동마다 말 재선택).
            selectedPieceId = -1;
            RefreshForCurrentState();
        }

        private void HandleTurnPhaseChanged(TurnContext context)
        {
            if (context == null || context.currentPhase != TurnPhase.WaitAction)
            {
                ClearAll();
                return;
            }
            RefreshForCurrentState();
        }

        private void HandleTurnEnded(PlayerSlot _) => ClearAll();

        /// <summary>공용 HUD의 대기 말 버튼이 지금 출루 선택에 사용 가능한지 검사합니다.</summary>
        public bool CanSelectWaitingPiece(int playerId, int pieceId)
        {
            if (!isActiveAndEnabled || !subscribed || turnManager == null ||
                playerManager == null || pieceMovementManager == null ||
                turnManager.CurrentTurn == null || turnManager.CurrentTurn.isGameEnded ||
                turnManager.CurrentTurn.currentPhase != TurnPhase.WaitAction ||
                (int)turnManager.CurrentTurn.currentPlayer != playerId ||
                turnManager.TryGetPendingForcedMove(out _) ||
                !playerManager.TryGetPlayer(playerId, out PlayerController player) ||
                !player.TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData piece) ||
                piece.State != PieceState.Waiting || !CanMovePiece(piece))
            {
                return false;
            }

            foreach (YutThrowData result in turnManager.PendingResults)
            {
                if (YutResultRule.GetMoveCount(result.result) > 0)
                    return true;
            }

            return false;
        }

        /// <summary>대기 말은 즉시 이동시키지 않고, 선택한 말의 출루 목적지만 표시합니다.</summary>
        public bool TrySelectWaitingPiece(int playerId, int pieceId)
        {
            if (!CanSelectWaitingPiece(playerId, pieceId))
                return false;

            pendingResults.Clear();
            pendingResults.AddRange(turnManager.PendingResults);
            selectedPieceId = pieceId;
            BuildDestinationMarkers();
            return selectedPieceId == pieceId && destMarkers.Count > 0;
        }

        private void Update()
        {
            if (turnManager == null || Mouse.current == null) return;
            if (turnManager.CurrentTurn.currentPhase != TurnPhase.WaitAction) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 screenPosition = Mouse.current.position.ReadValue();

            // 매 프레임: 목적지 칸 위에 커서 있으면 그 칸 강조 (이동 가능 칸만).
            if (selectedPieceId >= 0 && destMarkers.Count > 0)
            {
                bool found = MoveMarker.TryFindAtScreenPosition(cam, screenPosition, MoveMarkerRole.Destination, out MoveMarker hoveredMarker);
                if (verboseLog && found && hoveredMarker != lastHoverLogged)
                {
                    Debug.Log($"[MoveSelector] hover 감지: {hoveredMarker.Result}", this);
                    lastHoverLogged = hoveredMarker;
                }
                if (!found) lastHoverLogged = null;
                foreach (MoveMarker m in destMarkers)
                {
                    if (m == null) continue;
                    m.SetHovered(m == hoveredMarker);
                    // Inspector 크기/위치/각도 실시간 반영 (큰 칸·대각선 칸은 전용 값).
                    ApplyOutlineShape(m);
                }
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            // 대기 말 버튼 등 UI를 누른 클릭이 보드까지 전달되어 선택을 취소/실행하지 않도록 합니다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (forcedMoveActive)
            {
                if (MoveMarker.TryFindAtScreenPosition(cam, screenPosition, MoveMarkerRole.Destination, out MoveMarker _))
                {
                    if (verboseLog) Debug.Log($"[MoveSelector] 윤누리 추가 1칸 칸 클릭 → RequestForcedMove(piece={selectedPieceId})", this);
                    turnManager.RequestForcedMove();
                }
                return;
            }

            if (selectedPieceId < 0)
            {
                // 1단계: "말"을 직접 클릭해서 선택. 파란 ▼ 는 어떤 말이 선택 가능한지 보여주는 표시일 뿐.
                int playerId = (int)turnManager.CurrentTurn.currentPlayer;
                int pickedId = -1;

                if (TryFindSelectablePieceAtScreenPosition(cam, screenPosition, playerId, out DebugPieceView pieceView))
                {
                    pickedId = pieceView.PieceId;
                }
                else if (MoveMarker.TryFindAtScreenPosition(
                             cam, screenPosition, MoveMarkerRole.PieceSelect, out MoveMarker pick))
                {
                    pickedId = pick.PieceId;   // ▼ 를 직접 눌러도 선택되게 (보조)
                }

                if (pickedId >= 0)
                {
                    selectedPieceId = pickedId;
                    if (verboseLog)
                    {
                        string st = playerManager.TryGetPlayer(playerId, out PlayerController pc) &&
                                    pc.TryGetPieceData(selectedPieceId, out PlayerRuntimeData.PieceRuntimeData pd)
                            ? pd.State.ToString() : "?";
                        Debug.Log($"[MoveSelector] 말 선택: piece{selectedPieceId} (state={st})", this);
                    }
                    BuildDestinationMarkers();
                }
                return;
            }

            // 3단계: 목적지 "칸" 클릭 → 이동 (콜라이더가 칸 바닥~▼ 까지라 칸을 눌러도 잡힘)
            if (MoveMarker.TryFindAtScreenPosition(cam, screenPosition, MoveMarkerRole.Destination, out MoveMarker dest))
            {
                if (verboseLog) Debug.Log($"[MoveSelector] 목적지 칸 클릭 → RequestMovePiece(piece={selectedPieceId}, {dest.Result})", this);
                turnManager.RequestMovePiece(selectedPieceId, dest.Result);
                // 이동 성공 시 OnPendingResultsChanged 가 selectedPieceId 를 -1 로 되돌린다.
                // 아직 >=0 이면 이동이 거부된 것 → 선택 풀고 1단계로 복구 (말이 고정되는 버그 방지).
                if (selectedPieceId >= 0)
                {
                    if (verboseLog) Debug.Log("[MoveSelector] 이동 거부됨 → 선택 해제", this);
                    selectedPieceId = -1;
                    RefreshForCurrentState();
                }
                return;
            }

            // 빈 곳 클릭 → 말 선택 취소, 다시 1단계
            if (verboseLog) Debug.Log("[MoveSelector] 선택 취소 → 말 선택 단계로", this);
            selectedPieceId = -1;
            RefreshForCurrentState();
        }

        private void RefreshForCurrentState()
        {
            ClearAll();

            if (turnManager.CurrentTurn.currentPhase != TurnPhase.WaitAction) return;

            if (turnManager.TryGetPendingForcedMove(out int forcedPieceId))
            {
                forcedMoveActive = true;
                selectedPieceId = forcedPieceId;
                BuildForcedMoveMarker();
                return;
            }
            if (forcedMoveActive)
            {
                forcedMoveActive = false;
                selectedPieceId = -1; // 추가 이동 끝남 → 남은 결과는 다시 말 선택부터
            }

            if (pendingResults.Count == 0) return;

            if (selectedPieceId < 0)
                BuildPieceSelectArrows();
            else
                BuildDestinationMarkers();
        }

        // 큰 칸: 참먹이(Start) + 모/뒷모/찌모/날모(Corner01~04) + 방(Center). 새 보드에서 일반 칸보다 커서 테두리 크기를 따로 준다.
        private static bool IsLargeTile(BoardTileId tile)
        {
            switch (tile)
            {
                case BoardTileId.None:      // 완주(참먹이 도착) 마커
                case BoardTileId.Start:
                case BoardTileId.Corner01:
                case BoardTileId.Corner02:
                case BoardTileId.Corner03:
                case BoardTileId.Corner04:
                case BoardTileId.Center:
                    return true;
                default:
                    return false;
            }
        }

        // 화살표 이미지 전체를 한 장짜리 스프라이트로 만듦 (한 번만). 기본 ▼ 와 똑같이 아래 가운데가 기준점, 높이 1.
        private Sprite GetDestinationArrowSprite()
        {
            if (destinationArrowSprite == null && destinationArrowTexture != null)
            {
                destinationArrowSprite = Sprite.Create(
                    destinationArrowTexture,
                    new Rect(0f, 0f, destinationArrowTexture.width, destinationArrowTexture.height),
                    new Vector2(0.5f, 0f),
                    destinationArrowTexture.height);
                destinationArrowSprite.name = destinationArrowTexture.name + "_Full";
            }
            return destinationArrowSprite;
        }

        // 탈출 깃발 이미지 전체를 한 장짜리 스프라이트로 (한 번만). 기준점 = 아래 가운데, 높이 1 (화살표와 같은 크기 기준).
        private Sprite GetEscapeFlagSprite()
        {
            if (escapeFlagSprite == null && escapeFlagTexture != null)
            {
                escapeFlagSprite = Sprite.Create(
                    escapeFlagTexture,
                    new Rect(0f, 0f, escapeFlagTexture.width, escapeFlagTexture.height),
                    new Vector2(0.5f, 0f),
                    escapeFlagTexture.height);
                escapeFlagSprite.name = escapeFlagTexture.name + "_Full";
            }
            return escapeFlagSprite;
        }

        private void ApplyOutlineShape(MoveMarker marker)
        {
            marker.SetOutlineShape(
                marker.OutlineIsLarge ? outlineTileSizeLarge : outlineTileSize,
                outlineOffset,
                marker.OutlineIsDiagonal ? outlineAngleDegDiagonal : outlineAngleDeg,
                outlineThickness);
        }

        // 대각선(지름길) 칸: Center(방) + Inner01~08(모도/모개/.../방수기). 이 칸들만 테두리 각도를 따로 준다.
        private static bool IsDiagonalShortcutTile(BoardTileId tile)
        {
            return tile == BoardTileId.Center ||
                   (tile >= BoardTileId.Inner01 && tile <= BoardTileId.Inner08);
        }

        // 이 말이 이번에 선택 가능한 말인지 (= 파란 ▼ 가 떠 있는 말인지).
        // BuildPieceSelectArrows 가 CanMovePiece/onlyBackDo 판정을 이미 했으므로 그 결과를 재사용한다.
        private bool IsSelectablePiece(int pieceId)
        {
            foreach (MoveMarker m in pieceArrows)
                if (m != null && m.PieceId == pieceId) return true;
            return false;
        }

        // 업힌 말끼리 겹쳐 있을 때 말 클릭 판정. DebugPieceView.TryFindAtScreenPosition은
        // "카메라에 가장 가까운 말"을 그냥 골라서, 그게 선택 불가능한(업힌) 말이면 조용히
        // 실패한다 (그럼 사용자 입장에선 ▼만 눌러야 선택되는 것처럼 보임). 그래서 여기선
        // "이 플레이어의 선택 가능한(=▼ 뜬) 말"만 후보로 두고 직접 레이캐스트한다.
        private bool TryFindSelectablePieceAtScreenPosition(
            Camera cam, Vector2 screenPosition, int playerId, out DebugPieceView found)
        {
            found = null;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPosition);
            float nearest = float.PositiveInfinity;
            foreach (DebugPieceView view in FindObjectsByType<DebugPieceView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (view.PlayerId != playerId || !IsSelectablePiece(view.PieceId)) continue;

                foreach (Collider col in view.GetComponentsInChildren<Collider>(false))
                {
                    if (!col.enabled ||
                        !col.Raycast(ray, out RaycastHit hit, cam.farClipPlane) ||
                        hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    found = view;
                }
            }
            return found != null;
        }

        // 1단계: 보드 위의 움직일 수 있는 현재 플레이어 말마다 화살표.
        // Waiting 상태의 말은 공용 HUD 버튼으로만 출루 선택합니다.
        // 화살표는 각 말 정수리 바로 위에 뜬다.
        private void BuildPieceSelectArrows()
        {
            int playerId = (int)turnManager.CurrentTurn.currentPlayer;
            int count = 0;
            foreach (DebugPieceView view in FindObjectsByType<DebugPieceView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (view.PlayerId != playerId) continue;
                if (!playerManager.TryGetPlayer(playerId, out PlayerController player) ||
                    !player.TryGetPieceData(view.PieceId, out PlayerRuntimeData.PieceRuntimeData piece))
                    continue;
                if (piece.State != PieceState.InBoard || !CanMovePiece(piece)) continue;

                var obj = new GameObject("PieceSelectArrow");
                obj.transform.SetParent(transform, false);
                var marker = obj.AddComponent<MoveMarker>();
                // 파란 화살표는 화면 UI 회피(ClampToScreenSafeArea) 안 씀 → null. (말마다 튕기는 것 방지)
                marker.ConfigureAsPieceSelect(view.PieceId, view.transform,
                    pieceArrowHeight, pieceArrowColor, pieceArrowScale, null);
                Sprite customArrow = GetDestinationArrowSprite();
                if (customArrow != null) marker.SetArrowSprite(customArrow);   // 말 선택 화살표도 같은 디자인 화살표
                pieceArrows.Add(marker);
                count++;
            }

            if (verboseLog) Debug.Log($"[MoveSelector] 말 선택 화살표 {count}개 (P{playerId})", this);
        }

        // 2단계: 선택한 말이 남은 결과별로 갈 수 있는 보드 칸에 마커.
        private void BuildDestinationMarkers()
        {
            ClearAll(); // 1단계 말 선택 화살표도 같이 치운다

            int playerId = (int)turnManager.CurrentTurn.currentPlayer;
            if (!playerManager.TryGetPlayer(playerId, out PlayerController player) ||
                !player.TryGetPieceData(selectedPieceId, out PlayerRuntimeData.PieceRuntimeData _))
            {
                selectedPieceId = -1;
                return;
            }

            var seen = new HashSet<YutResult>();
            var placed = new List<Vector3>();
            escapeFlagCount = 0;
            // 작은 결과부터 처리 → 탈출 가능한 결과가 여러 개면 가장 작은 결과가 탈출 깃발을 차지함
            var orderedResults = new List<YutThrowData>(pendingResults);
            orderedResults.Sort((a, b) =>
                YutResultRule.GetMoveCount(a.result).CompareTo(YutResultRule.GetMoveCount(b.result)));
            foreach (YutThrowData throwData in orderedResults)
            {
                YutResult result = throwData.result;
                if (!seen.Add(result)) continue;

                int moveCount = YutResultRule.GetMoveCount(result);
                if (moveCount == 0) continue;

                TryCreateDestinationMarker(playerId, result, moveCount, placed);
            }

            if (destMarkers.Count == 0)
            {
                if (verboseLog) Debug.Log($"[MoveSelector] piece{selectedPieceId} 로 갈 수 있는 곳 없음 → 말 선택으로", this);
                selectedPieceId = -1;
                RefreshForCurrentState();
            }
        }

        // 윤누리 첫 이동 +1: 말이 이미 첫 착지 칸에 서 있으므로 "여기서 새로 1칸"을 미리보기.
        // 실제 추가 이동도 새 이동이라, 모서리에서는 똑같이 안쪽(지름길)으로 꺾인다.
        private void BuildForcedMoveMarker()
        {
            int playerId = (int)turnManager.CurrentTurn.currentPlayer;
            if (verboseLog) Debug.Log($"[MoveSelector] 윤누리 추가 1칸 대기: piece{selectedPieceId}", this);
            escapeFlagCount = 0;
            if (!TryCreateDestinationMarker(playerId, YutResult.Do, 1, new List<Vector3>()))
            {
                // 갈 곳이 없으면(정상적으론 없음) 대기를 풀어야 턴이 멈추지 않음
                Debug.LogWarning("[MoveSelector] 윤누리 추가 1칸 목적지를 못 찾아 바로 실행합니다.", this);
                turnManager.RequestForcedMove();
            }
        }

        private bool TryCreateDestinationMarker(int playerId, YutResult result, int moveCount, List<Vector3> placed)
        {
            if (!pieceMovementManager.TryPreviewLanding(
                    playerId, selectedPieceId, moveCount,
                    out BoardTileId landingTile, out bool reachesFinish))
            {
                if (verboseLog) Debug.Log($"[MoveSelector]   {result}: 목적지 없음", this);
                return false;
            }

            if (!TryResolveWorldPosition(landingTile, reachesFinish, out Vector3 tilePos))
            {
                if (verboseLog) Debug.LogWarning($"[MoveSelector]   {result}: {landingTile} 좌표 해석 실패", this);
                return false;
            }

            // ▼ 높이: 말 없는 칸 = 칸 중앙 살짝 위 / 말 있는 칸 = 말 1개 정수리 살짝 위.
            bool pieceOnTile = !reachesFinish && AnyPieceOnTile(landingTile);

            // 시작 위치 = 말이 서는 자리(영서 칸 프리팹의 PieceAnchor). 일반 칸은 아래에서 화면상 칸 중앙으로 옮김.
            // 참먹이 칸은 나눠 씀 (QA 10/23): 멈추기 화살표 = 살짝 왼쪽 위, 탈출 깃발 = 오른쪽 아래. 씬 앵커가 있으면 그 자리 우선.
            bool startTile = landingTile == BoardTileId.Start || landingTile == BoardTileId.None;
            if (reachesFinish)
            {
                // QA 10/24: 탈출 깃발은 항상 1개. 탈출 가능한 결과가 여러 개면(예: 윷·개) 가장 작은 결과 하나만 깃발로 띄움
                // (결과는 작은 것부터 처리됨). 예전엔 여러 깃발을 옆으로 벌리다 옆 칸으로 밀려나 헷갈렸음.
                if (escapeFlagCount > 0)
                {
                    if (verboseLog) Debug.Log($"[MoveSelector]   {result}: 탈출 깃발은 이미 있음 (더 작은 결과로 탈출) → 생략", this);
                    return false;
                }
                escapeFlagCount++;
                if (finishMarkerAnchor == null && TryGetStartTilePoint(startTileEscapeOffset, out Vector3 escapePoint))
                    tilePos = escapePoint;
            }
            else if (!pieceOnTile && startTile)
            {
                if (startStopMarkerAnchor != null)
                    tilePos = startStopMarkerAnchor.position;
                else if (TryGetStartTilePoint(startStopArrowOffset, out Vector3 stopPoint))
                    tilePos = stopPoint;
            }

            // 기획 요청(10/24): 참먹이를 뺀 모든 칸은 화살표를 칸 중앙에 (말이 있는 칸도 포함).
            // 중앙 = 화면에 보이는 칸 모델의 가로·세로 가운데 (두꺼운 3D 칸이라 말 자리/3D 상자 중심은 어긋남)
            bool centerOnTile = !reachesFinish && !startTile;
            if (centerOnTile && TryGetTileScreenCenter(landingTile, tilePos, out Vector3 tileCenter))
                tilePos = tileCenter;

            // 두 결과가 같은 칸에 도착하는 경우만 살짝 옆으로 (탈출 깃발은 1개뿐이라 제외).
            if (!reachesFinish)
                tilePos = OffsetIfOverlapping(tilePos, placed);
            placed.Add(tilePos);
            float arrowBaseY = pieceOnTile && !centerOnTile
                ? OnePieceTopOnTile(landingTile) + arrowHeightAbovePiece   // 참먹이에 말이 있을 때만 말 머리 위
                : arrowHeightEmptyTile;

            // 화면 위/아래 UI 패널에 ▼ 가 걸리면 안 가리게 민다 (타일 하이라이트는 그대로, ▼ 만 이동).
            // 수정(10/23): 새 화면에선 아래쪽 칸(참먹이 등) 화살표·깃발이 전부 위로 밀려서 엉뚱한 데 떴음 → 기본 끔
            Vector3 arrowWorld = tilePos + Vector3.up * arrowBaseY;
            if (avoidScreenEdgeUi)
                arrowWorld = ClampToScreenSafeArea(arrowWorld);
            float arrowLocalY = arrowWorld.y - tilePos.y;

            // 보드 타일 앵커 (윤곽선을 자식으로 붙여 보드 기울기 물려받음).
            // 탈출(깃발)도 마우스 올리면 참먹이 칸 윤곽선을 켬 (10/24 요청). 멈춤은 금색 화살표, 탈출은 깃발이라 구별됨.
            // 참먹이에 멈추거나 탈출하면 도착 칸이 None 으로 옴 → Start 칸 윤곽선
            Transform tileAnchor = null;
            if (pieceDebugController != null)
                pieceDebugController.TryGetTileTransform(
                    startTile || reachesFinish ? BoardTileId.Start : landingTile, out tileAnchor);

            bool diagonalTile = IsDiagonalShortcutTile(landingTile);
            float tileAngle = diagonalTile ? outlineAngleDegDiagonal : outlineAngleDeg;

            var obj = new GameObject("MoveDestMarker");
            obj.transform.SetParent(transform, false);
            var marker = obj.AddComponent<MoveMarker>();
            marker.ConfigureAsDestination(
                result, selectedPieceId, tilePos, arrowLocalY, pieceOnTile,
                tileAnchor, diagonalTile,
                outlineTileSize, tileAngle, outlineThickness, outlineCornerRadius,
                destColor, outlineColor, outlineHoverColor, destScale);
            if (reachesFinish)
                marker.SetEscapeMarker(GetEscapeFlagSprite());   // 탈출(통과) = 깃발 이미지 + 결과 글자, 멈춤은 금색 화살표로 구별
            else
            {
                Sprite customArrow = GetDestinationArrowSprite();
                if (customArrow != null) marker.SetArrowSprite(customArrow);   // 디자인 화살표 이미지로 교체
            }
            marker.OutlineIsLarge = IsLargeTile(reachesFinish ? BoardTileId.Start : landingTile);
            ApplyOutlineShape(marker); // 큰 칸 크기 + 위치 보정 적용
            destMarkers.Add(marker);
            if (verboseLog) Debug.Log($"[MoveSelector]   {result} → {landingTile} finish={reachesFinish} piece={pieceOnTile} {tilePos}", this);
            return true;
        }

        private bool TryResolveWorldPosition(BoardTileId tile, bool reachesFinish, out Vector3 position)
        {
            if (reachesFinish)
            {
                if (finishMarkerAnchor != null)
                {
                    position = finishMarkerAnchor.position;
                    return true;
                }
                if (TryTilePosition(BoardTileId.Start, out Vector3 startPos))
                {
                    position = startPos + new Vector3(0f, 0f, -0.4f);
                    return true;
                }
                Debug.LogWarning("MoveDestinationSelector: 완주 마커 위치를 못 찾음. finishMarkerAnchor 지정 필요.", this);
                position = Vector3.zero;
                return false;
            }

            BoardTileId lookup = tile == BoardTileId.None ? BoardTileId.Start : tile;
            return TryTilePosition(lookup, out position);
        }

        // 칸 중앙 = 화면에 보이는 칸 모델의 가로·세로 가운데. 깊이는 depthReference(말 서는 자리) 기준.
        private bool TryGetTileScreenCenter(BoardTileId tile, Vector3 depthReference, out Vector3 center)
        {
            center = depthReference;
            Camera cam = Camera.main;
            if (cam == null || pieceDebugController == null ||
                !pieceDebugController.TryGetTileTransform(tile, out Transform tileAnchor) || tileAnchor == null)
                return false;

            // 칸 모델(말, 윤곽선 복사본 제외) 범위 꼭짓점을 화면에 찍어서 화면상 가로·세로 가운데를 구함
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool found = false;
            foreach (MeshRenderer tileRenderer in tileAnchor.GetComponentsInChildren<MeshRenderer>())
            {
                if (tileRenderer.name == MoveMarker.TileHullName ||
                    tileRenderer.GetComponentInParent<DebugPieceView>() != null)
                    continue;
                Bounds localBounds = tileRenderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = localBounds.center + Vector3.Scale(localBounds.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 screenPoint = cam.WorldToScreenPoint(tileRenderer.transform.TransformPoint(corner));
                    if (screenPoint.z <= 0f) continue;
                    min = Vector2.Min(min, screenPoint);
                    max = Vector2.Max(max, screenPoint);
                    found = true;
                }
            }
            if (!found) return false;

            // 깊이는 말 서는 자리(칸 윗면) 기준으로 둬서 화살표가 칸 표면에 붙어 보이게 함
            float depth = cam.WorldToScreenPoint(depthReference).z;
            if (depth <= 0f) return false;
            Vector2 screenCenter = (min + max) * 0.5f;
            center = cam.ScreenToWorldPoint(new Vector3(screenCenter.x, screenCenter.y, depth));
            return true;
        }

        // 참먹이 칸 위의 한 지점 = 참먹이 말 서는 자리에서 화면 방향(카메라 오른쪽/위)으로 offset 만큼 (월드 거리).
        // 예: (0.38, -0.38) = 말 자리에서 오른쪽으로 0.38, 아래로 0.38.
        private bool TryGetStartTilePoint(Vector2 offset, out Vector3 point)
        {
            if (!TryTilePosition(BoardTileId.Start, out point)) return false;
            Camera cam = Camera.main;
            if (cam != null)
                point += cam.transform.right * offset.x + cam.transform.up * offset.y;
            return true;
        }

        private bool TryTilePosition(BoardTileId tile, out Vector3 position)
        {
            if (pieceDebugController != null && pieceDebugController.TryGetTileWorldPosition(tile, out position))
                return true;
            if (mapManager != null && mapManager.TryGetTilePosition(tile, out position))
                return true;
            position = Vector3.zero;
            return false;
        }

        private static Vector3 OffsetIfOverlapping(Vector3 pos, List<Vector3> placed)
        {
            int n = 0;
            foreach (Vector3 p in placed)
                if ((p - pos).sqrMagnitude < 0.09f) n++;
            return n == 0 ? pos : pos + new Vector3(0.35f * n, 0f, 0f);
        }

        // 마커의 화면 y 를 [bottomUiPixels, Screen.height - topUiPixels] 안으로 넣는다.
        // 카메라가 내려다보는 각도라 월드 +Y = 화면 위쪽 이동. 아래 걸리면 올리고, 위 걸리면 내린다.
        public Vector3 ClampToScreenSafeArea(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null) return worldPos;

            float topLimit = Screen.height - Mathf.Max(0f, topUiPixels);
            for (int i = 0; i < 16; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(worldPos);
                if (sp.z <= 0f) break; // 카메라 뒤면 포기

                if (bottomUiPixels > 0f && sp.y < bottomUiPixels)
                    worldPos += Vector3.up * 0.25f;
                else if (topUiPixels > 0f && sp.y > topLimit)
                    worldPos += Vector3.down * 0.25f;
                else
                    break;
            }
            return worldPos;
        }

        // 지정 칸에 서 있는(InBoard) 말이 하나라도 있는지.
        private bool AnyPieceOnTile(BoardTileId tile)
        {
            if (tile == BoardTileId.Goal) return false; // None = 참먹이 (판 위 말만 세서 대기 말은 안 섞임)

            foreach (DebugPieceView v in FindObjectsByType<DebugPieceView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!playerManager.TryGetPlayer(v.PlayerId, out PlayerController p)) continue;
                if (!p.TryGetPieceData(v.PieceId, out PlayerRuntimeData.PieceRuntimeData piece)) continue;
                if (piece.State == PieceState.InBoard && piece.CurrentTileId == tile) return true;
            }
            return false;
        }

        // 지정 칸에 있는 "말 1개" 기준 모델 최상단 높이(칸 기준). 회의 스펙: 말이 몇 개든 1개 기준 고정.
        private float OnePieceTopOnTile(BoardTileId tile)
        {
            if (tile == BoardTileId.Goal) return 0f;

            foreach (DebugPieceView v in FindObjectsByType<DebugPieceView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!playerManager.TryGetPlayer(v.PlayerId, out PlayerController p)) continue;
                if (!p.TryGetPieceData(v.PieceId, out PlayerRuntimeData.PieceRuntimeData piece)) continue;
                if (piece.State != PieceState.InBoard || piece.CurrentTileId != tile) continue;

                return PieceModelTop(v.transform); // 첫 번째 말 하나로 고정
            }
            return 0f;
        }

        // 말(자식 렌더러 포함) 최상단 y 를 말 원점 기준 높이로.
        private static float PieceModelTop(Transform pieceTransform)
        {
            Renderer[] renderers = pieceTransform.GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0) return 0f;

            float maxY = float.NegativeInfinity;
            foreach (Renderer r in renderers)
                maxY = Mathf.Max(maxY, r.bounds.max.y);
            return Mathf.Max(0f, maxY - pieceTransform.position.y);
        }

        private bool CanMovePiece(PlayerRuntimeData.PieceRuntimeData piece)
        {
            if (piece.State == PieceState.Goal) return false;   // 완주한 말은 더 이상 이동 대상 아님
            if (!CcEffectService.CanMove(piece)) return false;
            if (piece.IsStacked && piece.PieceId != piece.StackLeaderPieceId) return false; // 업힌 말
            return true;
        }

        private void ClearPieceArrows()
        {
            foreach (MoveMarker m in pieceArrows)
                if (m != null) Destroy(m.gameObject);
            pieceArrows.Clear();
        }

        private void ClearDestMarkers()
        {
            foreach (MoveMarker m in destMarkers)
                if (m != null) Destroy(m.gameObject);
            destMarkers.Clear();
        }

        private void ClearAll()
        {
            ClearPieceArrows();
            ClearDestMarkers();
        }
    }
}
