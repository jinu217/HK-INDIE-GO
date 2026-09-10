using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using YutArena.Common;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

namespace YutArena.InGame
{
    /// <summary>
    /// 이동 선택 3단계 흐름 (WaitAction 단계). 마커는 MoveMarker 하나로 역할만 다르게 쓴다.
    /// 1) 윷을 다 던지면 → 움직일 수 있는 현재 플레이어 말마다 파란 화살표(MoveMarker.PieceSelect).
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

        [Header("완주 마커 위치 (비우면 Start 근처로 대체)")]
        [SerializeField] private Transform finishMarkerAnchor;

        [Header("말 선택 화살표 (1단계)")]
        [SerializeField] private Color pieceArrowColor = new Color(0.45f, 0.85f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float pieceArrowScale = 0.35f;
        [Tooltip("말 원점에서 ▼ 촉까지 고정 높이 (모든 말 화살표 동일). 캐릭터 정수리 살짝 위가 되게 조정.")]
        [SerializeField] private float pieceArrowHeight = 1.15f;

        [Header("목적지 마커 (2단계)")]
        [SerializeField] private Color destColor = new Color(1f, 0.86f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float destScale = 0.45f;
        [Tooltip("말 없는 칸: ▼ 를 칸에서 이만큼 위에 표시 (살짝 위).")]
        [SerializeField] private float arrowHeightEmptyTile = 0.18f;
        [Tooltip("말 있는 칸: ▼ 를 말 1개 정수리에서 이만큼 위에 표시.")]
        [SerializeField] private float arrowHeightAbovePiece = 0.12f;

        [Header("칸 테두리 강조 (hover / 말 있는 칸) — 칸 Anchor 에 얹는 라운드 보더")]
        [Tooltip("테두리 한 변 길이(월드) = 타일 바깥 치수. 갈색 타일에 딱 맞을 때까지 조절.")]
        [SerializeField, Min(0.1f)] private float outlineTileSize = 1.25f;
        [Tooltip("바깥/꼭지점 칸 테두리 회전(도). 보통 0.")]
        [SerializeField, Range(-90f, 90f)] private float outlineAngleDeg = 0f;
        [Tooltip("대각선(지름길) 칸 테두리 회전(도): Inner01~08 + Center. 대각 타일에 맞을 때까지 조절.")]
        [SerializeField, Range(-90f, 90f)] private float outlineAngleDegDiagonal = 45f;
        [Tooltip("테두리 굵기 (한 변 대비 비율).")]
        [SerializeField, Range(0.02f, 0.25f)] private float outlineThickness = 0.07f;
        [Tooltip("모서리 둥글기 (한 변 대비 비율). 0 = 직각, 갈색 타일 라운드에 맞춰 조절.")]
        [SerializeField, Range(0f, 0.5f)] private float outlineCornerRadius = 0.16f;
        [Tooltip("테두리 색 (상시 = 칸에 말 있을 때).")]
        [SerializeField] private Color outlineColor = new Color(0.95f, 0.72f, 0.28f, 0.55f);
        [Tooltip("테두리 색 (마우스 호버 시). 더 밝게.")]
        [SerializeField] private Color outlineHoverColor = new Color(1f, 0.85f, 0.4f, 1f);

        [Header("화면 가장자리 UI 회피 (위/아래 패널에 안 가리게)")]
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
                    // Inspector 각도 슬라이더 실시간 반영 (대각선 칸은 전용 값).
                    m.SetOutlineAngle(m.OutlineIsDiagonal ? outlineAngleDegDiagonal : outlineAngleDeg);
                }
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            if (selectedPieceId < 0)
            {
                // 1단계: "말"을 직접 클릭해서 선택. 파란 ▼ 는 어떤 말이 선택 가능한지 보여주는 표시일 뿐.
                int playerId = (int)turnManager.CurrentTurn.currentPlayer;
                int pickedId = -1;

                if (DebugPieceView.TryFindAtScreenPosition(cam, screenPosition, out DebugPieceView pieceView) &&
                    pieceView.PlayerId == playerId && IsSelectablePiece(pieceView.PieceId))
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
            if (pendingResults.Count == 0) return;

            if (selectedPieceId < 0)
                BuildPieceSelectArrows();
            else
                BuildDestinationMarkers();
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

        // 1단계: 움직일 수 있는 현재 플레이어 말마다 화살표 (사용자가 이동할 말을 고름).
        // 화살표는 각 말 정수리 바로 위에 뜬다.
        private void BuildPieceSelectArrows()
        {
            int playerId = (int)turnManager.CurrentTurn.currentPlayer;
            bool onlyBackDo = OnlyBackDoRemaining();

            int count = 0;
            foreach (DebugPieceView view in FindObjectsByType<DebugPieceView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (view.PlayerId != playerId) continue;
                if (!playerManager.TryGetPlayer(playerId, out PlayerController player) ||
                    !player.TryGetPieceData(view.PieceId, out PlayerRuntimeData.PieceRuntimeData piece))
                    continue;
                if (!CanMovePiece(piece)) continue;
                if (onlyBackDo && piece.State != PieceState.InBoard) continue;

                var obj = new GameObject("PieceSelectArrow");
                obj.transform.SetParent(transform, false);
                var marker = obj.AddComponent<MoveMarker>();
                // 파란 화살표는 화면 UI 회피(ClampToScreenSafeArea) 안 씀 → null. (말마다 튕기는 것 방지)
                marker.ConfigureAsPieceSelect(view.PieceId, view.transform,
                    pieceArrowHeight, pieceArrowColor, pieceArrowScale, null);
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
            foreach (YutThrowData throwData in pendingResults)
            {
                YutResult result = throwData.result;
                if (!seen.Add(result)) continue;

                int moveCount = YutResultRule.GetMoveCount(result);
                if (moveCount == 0) continue;

                if (!pieceMovementManager.TryPreviewLanding(
                        playerId, selectedPieceId, moveCount,
                        out BoardTileId landingTile, out bool reachesFinish))
                {
                    if (verboseLog) Debug.Log($"[MoveSelector]   {result}: 목적지 없음", this);
                    continue;
                }

                if (!TryResolveWorldPosition(landingTile, reachesFinish, out Vector3 tilePos))
                {
                    if (verboseLog) Debug.LogWarning($"[MoveSelector]   {result}: {landingTile} 좌표 해석 실패", this);
                    continue;
                }

                // 두 결과가 같은 칸에 도착하는 경우만 살짝 옆으로.
                tilePos = OffsetIfOverlapping(tilePos, placed);
                placed.Add(tilePos);

                // ▼ 높이: 말 없는 칸 = 칸 중앙 살짝 위 / 말 있는 칸 = 말 1개 정수리 살짝 위.
                bool pieceOnTile = !reachesFinish && AnyPieceOnTile(landingTile);
                float arrowBaseY = pieceOnTile
                    ? OnePieceTopOnTile(landingTile) + arrowHeightAbovePiece
                    : arrowHeightEmptyTile;

                // 화면 위/아래 UI 패널에 ▼ 가 걸리면 안 가리게 민다 (타일 하이라이트는 그대로, ▼ 만 이동).
                Vector3 arrowWorld = ClampToScreenSafeArea(tilePos + Vector3.up * arrowBaseY);
                float arrowLocalY = arrowWorld.y - tilePos.y;

                // 보드 타일 앵커 (윤곽선을 자식으로 붙여 보드 기울기 물려받음).
                // 완주(참먹이 도착)면 landingTile 이 None 이라 Start 칸 앵커를 쓴다.
                Transform tileAnchor = null;
                if (pieceDebugController != null)
                    pieceDebugController.TryGetTileTransform(
                        reachesFinish ? BoardTileId.Start : landingTile, out tileAnchor);
                if (reachesFinish && finishMarkerAnchor != null)
                    tileAnchor = finishMarkerAnchor;

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
                destMarkers.Add(marker);
                if (verboseLog) Debug.Log($"[MoveSelector]   {result} → {landingTile} finish={reachesFinish} piece={pieceOnTile} {tilePos}", this);
            }

            if (destMarkers.Count == 0)
            {
                if (verboseLog) Debug.Log($"[MoveSelector] piece{selectedPieceId} 로 갈 수 있는 곳 없음 → 말 선택으로", this);
                selectedPieceId = -1;
                RefreshForCurrentState();
            }
        }

        private bool OnlyBackDoRemaining()
        {
            if (pendingResults.Count == 0) return false;
            foreach (YutThrowData t in pendingResults)
                if (t.result != YutResult.BackDo)
                    return false;
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
            if (tile == BoardTileId.None || tile == BoardTileId.Goal) return false;

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
            if (tile == BoardTileId.None || tile == BoardTileId.Goal) return 0f;

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
            if (piece.CurrentCc == CcDefine.Stun) return false;
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
