using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.InGame
{
    /// <summary>
    /// Navigation view only. CharacterPieceInteractionController supplies legal
    /// piece/destination hints and owns selection, input and move requests.
    /// This component creates, positions, hovers and clears visual markers.
    /// </summary>
    public sealed class MoveDestinationSelector : MonoBehaviour
    {
        [Header("Dependencies (비우면 씬에서 자동 검색)")]
        [SerializeField] private PlayerManager playerManager;
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
        [SerializeField] private Color skillArrowColor = new Color(0.55f, 1f, 0.65f, 1f);
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
        private readonly Dictionary<MoveMarker, DebugPieceView> markerPieces =
            new Dictionary<MoveMarker, DebugPieceView>();
        private Func<int, int, bool> canSelectWaiting;
        private Func<int, int, bool> selectWaiting;

        public readonly struct PieceHint
        {
            public readonly DebugPieceView View;
            public readonly bool IsSkill;
            public PieceHint(DebugPieceView view, bool isSkill) { View = view; IsSkill = isSkill; }
        }

        public readonly struct DestinationHint
        {
            public readonly int PieceId;
            public readonly YutResult Result;
            public readonly BoardTileId Tile;
            public readonly bool ReachesFinish;
            public DestinationHint(int pieceId, YutResult result, BoardTileId tile, bool reachesFinish)
            { PieceId = pieceId; Result = result; Tile = tile; ReachesFinish = reachesFinish; }
        }

        private void Start()
        {
            if (playerManager == null) playerManager = FindFirstObjectByType<PlayerManager>();
            if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
            if (pieceDebugController == null) pieceDebugController = FindFirstObjectByType<InGamePieceDebugController>();
            if (!TryGetComponent<CharacterPieceInteractionController>(out var flow))
                flow = gameObject.AddComponent<CharacterPieceInteractionController>();
            flow.Initialize(this);
        }

        private void OnEnable()
        {
            if (TryGetComponent<CharacterPieceInteractionController>(out var flow)) flow.InvalidateNavigation();
        }

        private void OnDisable() => ClearAll();

        private void OnDestroy()
        {
            ClearAll();
            canSelectWaiting = null;
            selectWaiting = null;
            if (destinationArrowSprite != null) Destroy(destinationArrowSprite);
            if (escapeFlagSprite != null) Destroy(escapeFlagSprite);
        }

        public void BindWaitingPieceSelection(Func<int, int, bool> canSelect, Func<int, int, bool> select)
        { canSelectWaiting = canSelect; selectWaiting = select; }

        // Existing HUD bindings remain intact; all eligibility/selection belongs to Character.
        public bool CanSelectWaitingPiece(int playerId, int pieceId) =>
            isActiveAndEnabled && (canSelectWaiting?.Invoke(playerId, pieceId) ?? false);
        public bool TrySelectWaitingPiece(int playerId, int pieceId) =>
            isActiveAndEnabled && (selectWaiting?.Invoke(playerId, pieceId) ?? false);

        public void ShowNavigation(IReadOnlyList<PieceHint> pieces, IReadOnlyList<DestinationHint> destinations)
        {
            ClearAll();
            foreach (var hint in pieces) CreatePieceMarker(hint);
            escapeFlagCount = 0;
            var placed = new List<Vector3>();
            foreach (var destination in destinations) TryCreateDestinationMarker(destination, placed);
        }

        public bool TryPickPieceMarker(Camera camera, Vector2 screenPosition, out DebugPieceView piece)
        {
            piece = null;
            return MoveMarker.TryFindAtScreenPosition(camera, screenPosition, MoveMarkerRole.PieceSelect, out var marker) &&
                markerPieces.TryGetValue(marker, out piece) && piece != null;
        }

        public bool TryPickDestination(Camera camera, Vector2 screenPosition, out MoveMarker destination) =>
            MoveMarker.TryFindAtScreenPosition(camera, screenPosition, MoveMarkerRole.Destination, out destination) &&
            destMarkers.Contains(destination);

        public void HoverAt(Camera camera, Vector2 screenPosition)
        {
            TryPickDestination(camera, screenPosition, out var hovered);
            foreach (var marker in destMarkers)
            {
                if (marker == null) continue;
                marker.SetHovered(marker == hovered);
                ApplyOutlineShape(marker);
            }
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

        private void CreatePieceMarker(PieceHint hint)
        {
            if (hint.View == null) return;
            var obj = new GameObject(hint.IsSkill ? "SkillPieceHint" : "MovePieceHint");
            obj.transform.SetParent(transform, false);
            var marker = obj.AddComponent<MoveMarker>();
            marker.ConfigureAsPieceSelect(hint.View.PieceId, hint.View.transform,
                pieceArrowHeight, hint.IsSkill ? skillArrowColor : pieceArrowColor, pieceArrowScale, null);
            Sprite customArrow = GetDestinationArrowSprite();
            if (customArrow != null) marker.SetArrowSprite(customArrow);
            pieceArrows.Add(marker);
            markerPieces.Add(marker, hint.View);
        }

        private bool TryCreateDestinationMarker(DestinationHint destination, List<Vector3> placed)
        {
            int selectedPieceId = destination.PieceId;
            YutResult result = destination.Result;
            BoardTileId landingTile = destination.Tile;
            bool reachesFinish = destination.ReachesFinish;
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

        private void ClearPieceArrows()
        {
            foreach (MoveMarker m in pieceArrows)
                DestroyOwnedMarker(m);
            pieceArrows.Clear();
            markerPieces.Clear();
        }

        private void ClearDestMarkers()
        {
            foreach (MoveMarker m in destMarkers)
                DestroyOwnedMarker(m);
            destMarkers.Clear();
        }

        private static void DestroyOwnedMarker(MoveMarker marker)
        {
            if (marker == null) return;
            marker.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(marker.gameObject);
            else DestroyImmediate(marker.gameObject);
        }

        public void ClearAll()
        {
            ClearPieceArrows();
            ClearDestMarkers();
        }
    }
}
