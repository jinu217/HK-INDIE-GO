using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    /// <summary>
    /// Loads the visual board selected by GameStartSettings.mapType and exposes its tile anchors.
    /// Special-tile gameplay effects are delegated to the configured event assets.
    /// </summary>
    public sealed class MapManager : MonoBehaviour
    {
        [SerializeField] private MapDefinition[] mapDefinitions;
        [SerializeField] private Transform backgroundRoot;
        [Tooltip("타일 프리팹이 하나도 지정되지 않은 맵에서만 표시할 씬의 기본 말판입니다. 직접 맞춘 위치/회전/크기를 그대로 사용합니다.")]
        [SerializeField] private Transform fallbackBoardVisual;

        [Header("Common Board Layout")]
        [Tooltip("InGameScene Hierarchy에 배치한 공통 Start, Outer01 등의 타일 Anchor 루트입니다. 이 자식들의 위치를 수정하면 말 이동 위치에 반영됩니다.")]
        [SerializeField] private Transform commonBoardLayoutRoot;

        [Tooltip("PieceAnchor가 없는 타일에 자동 기준점을 만들 때 모델 앞쪽으로 확보할 월드 거리입니다. 여러 말의 분산 배치가 모델에 묻히지 않도록 여유를 둡니다. 프리팹에 직접 둔 PieceAnchor에는 적용하지 않습니다.")]
        [SerializeField, Min(0f)] private float automaticPieceAnchorClearance = 0.3f;

        private readonly Dictionary<BoardTileId, Vector3> tilePositions = new Dictionary<BoardTileId, Vector3>();
        private readonly Dictionary<BoardTileId, Transform> commonTileAnchors = new Dictionary<BoardTileId, Transform>();
        private readonly Dictionary<BoardTileId, Transform> pieceAnchors = new Dictionary<BoardTileId, Transform>();
        private readonly Dictionary<BoardTileId, MapSpecialTileSetting> specialTileSettings = new Dictionary<BoardTileId, MapSpecialTileSetting>();
        private readonly List<GameObject> loadedTileVisualInstances = new List<GameObject>();
        private GameObject loadedBackgroundInstance;
        private bool isCommonBoardLayoutCached;
        private bool useSpecialTiles;
        private bool isResolvingSpecialTile;
        private BoardTileRuntimeStateManager boardRuntimeState;

        public MapDefinition CurrentMap { get; private set; }
        public bool IsMapLoaded => CurrentMap != null && isCommonBoardLayoutCached;
        public bool HasMapVisual => loadedBackgroundInstance != null ||
                                    loadedTileVisualInstances.Count > 0 ||
                                    (fallbackBoardVisual != null && fallbackBoardVisual.gameObject.activeInHierarchy);
        public BoardTileRuntimeStateManager BoardRuntimeState => EnsureBoardRuntimeState();

        private void Awake()
        {
            EnsureBoardRuntimeState();
        }

        public bool LoadMap(GameStartSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("MapManager cannot load a null GameStartSettings.", this);
                return false;
            }

            MapDefinition definition = FindDefinition(settings.mapType);
            if (definition == null)
            {
                Debug.LogError($"MapManager: no map definition is registered for {settings.mapType}.", this);
                return false;
            }

            ClearLoadedMap();
            EnsureCommonBoardLayout();
            if (!isCommonBoardLayoutCached)
                return false;

            if (definition.backgroundPrefab != null)
            {
                loadedBackgroundInstance = backgroundRoot != null
                    ? Instantiate(definition.backgroundPrefab, backgroundRoot)
                    : Instantiate(definition.backgroundPrefab);
                loadedBackgroundInstance.name = definition.backgroundPrefab.name;
                loadedBackgroundInstance.transform.localPosition = Vector3.zero;
            }

            CurrentMap = definition;
            useSpecialTiles = settings.useSpecialTile;
            CacheSpecialTileSettings(definition);

            // 씬에 놓은 기본 말판과 맵별 타일 프리팹을 동시에 표시하지 않습니다.
            if (fallbackBoardVisual != null)
                fallbackBoardVisual.gameObject.SetActive(!definition.HasTileVisualPrefab);

            CreateTileVisuals(definition);

            if (!HasMapVisual)
                Debug.LogWarning($"MapManager: {settings.mapType}에 보드 외형 프리팹이 없어 논리 Anchor와 원형 디버그 보드를 사용합니다.", this);

            return IsMapLoaded;
        }

        public bool TryGetTilePosition(BoardTileId tileId, out Vector3 position)
        {
            if (commonTileAnchors.TryGetValue(tileId, out Transform anchor) && anchor != null)
            {
                position = anchor.position;
                return true;
            }

            return tilePositions.TryGetValue(tileId, out position);
        }

        /// <summary>공통 보드 레이아웃의 실제 타일 Transform을 반환합니다.</summary>
        public bool TryGetTileAnchor(BoardTileId tileId, out Transform anchor)
        {
            return commonTileAnchors.TryGetValue(tileId, out anchor) && anchor != null;
        }

        /// <summary>말 배치 중심점입니다. 자식 Transform이므로 칸을 옮기면 즉시 따라갑니다.</summary>
        public bool TryGetPiecePosition(BoardTileId tileId, out Vector3 position)
        {
            if (pieceAnchors.TryGetValue(tileId, out Transform anchor) && anchor != null)
            {
                position = anchor.position;
                return true;
            }

            // 타일 외형/앵커가 없는 기존 맵은 이전 보정을 유지합니다.
            if (TryGetTilePosition(tileId, out position))
            {
                position += new Vector3(0f, 0.098f, -0.15f);
                return true;
            }

            return false;
        }

        public bool TryGetSpecialTileSetting(BoardTileId tileId, out MapSpecialTileSetting setting)
        {
            return specialTileSettings.TryGetValue(tileId, out setting);
        }

        /// <summary>이동한 말 묶음이 특수 칸에 도착하면 연결된 이벤트를 한 번 실행합니다.</summary>
        public void ResolveSpecialTile(
            PlayerController player,
            PlayerRuntimeData.PieceRuntimeData piece,
            int movingPieceCount,
            PieceMovementManager movement)
        {
            if (!useSpecialTiles || isResolvingSpecialTile || player == null || piece == null ||
                piece.State != PieceState.InBoard ||
                !specialTileSettings.TryGetValue(piece.CurrentTileId, out MapSpecialTileSetting setting) ||
                setting.tileEvent == null)
                return;

            // 이벤트가 다시 말을 이동시키더라도 특수 타일 이벤트가 무한 연쇄되지 않게 합니다.
            isResolvingSpecialTile = true;
            try
            {
                setting.tileEvent.Execute(new MapSpecialTileContext(
                    this, player, piece, movingPieceCount, setting.value, movement));
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, setting.tileEvent);
            }
            finally
            {
                isResolvingSpecialTile = false;
            }
        }

        private MapDefinition FindDefinition(MapType requestedMap)
        {
            if (mapDefinitions == null || mapDefinitions.Length == 0)
                return null;

            if (requestedMap == MapType.Random)
            {
                var candidates = new List<MapDefinition>();
                foreach (MapDefinition definition in mapDefinitions)
                {
                    if (definition != null && definition.mapType != MapType.Random &&
                        (definition.backgroundPrefab != null || definition.HasTileVisualPrefab))
                        candidates.Add(definition);
                }

                return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            foreach (MapDefinition definition in mapDefinitions)
            {
                if (definition != null && definition.mapType == requestedMap)
                    return definition;
            }

            return null;
        }

        private void EnsureCommonBoardLayout()
        {
            if (isCommonBoardLayoutCached)
                return;

            if (commonBoardLayoutRoot == null)
            {
                Debug.LogError("MapManager: InGameScene의 Common Board Layout Root가 지정되지 않았습니다.", this);
                return;
            }

            tilePositions.Clear();
            commonTileAnchors.Clear();
            BoardTileAnchor[] anchors = commonBoardLayoutRoot.GetComponentsInChildren<BoardTileAnchor>(true);
            foreach (BoardTileAnchor anchor in anchors)
            {
                if (anchor == null || anchor.TileId == BoardTileId.None || anchor.TileId == BoardTileId.Goal)
                    continue;

                if (commonTileAnchors.ContainsKey(anchor.TileId))
                {
                    Debug.LogWarning($"MapManager: Common Board Layout에 중복 타일 {anchor.TileId}이 있습니다.", this);
                    continue;
                }

                commonTileAnchors.Add(anchor.TileId, anchor.transform);
            }

            isCommonBoardLayoutCached = true;
        }

        private void CacheSpecialTileSettings(MapDefinition definition)
        {
            specialTileSettings.Clear();
            if (definition.specialTiles == null)
                return;

            foreach (MapSpecialTileSetting setting in definition.specialTiles)
            {
                if (setting == null || !IsSpecialTileId(setting.tileId))
                    continue;

                specialTileSettings[setting.tileId] = setting;
            }
        }

        /// <summary>
        /// 공통 논리 Anchor 아래에, 현재 맵 Definition에서 지정한 화면용 타일 프리팹만 생성합니다.
        /// 프리팹 필드가 비어 있으면 해당 타일은 논리 Anchor만 유지합니다.
        /// </summary>
        private void CreateTileVisuals(MapDefinition definition)
        {
            foreach (KeyValuePair<BoardTileId, Transform> pair in commonTileAnchors)
            {
                GameObject tilePrefab = GetTileVisualPrefab(definition, pair.Key);
                if (tilePrefab == null || pair.Value == null)
                    continue;

                GameObject visualInstance = Instantiate(tilePrefab, pair.Value);
                visualInstance.name = tilePrefab.name;
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                loadedTileVisualInstances.Add(visualInstance);
                pieceAnchors[pair.Key] = FindOrCreatePieceAnchor(visualInstance.transform);
            }
        }

        private Transform FindOrCreatePieceAnchor(Transform tileVisual)
        {
            // 프리팹에서 직접 지정한 점을 최우선으로 사용합니다. 모델의 피벗은 변경하지 않습니다.
            foreach (Transform child in tileVisual.GetComponentsInChildren<Transform>(true))
            {
                if (child != tileVisual && child.name == "PieceAnchor")
                    return child;
            }

            var anchor = new GameObject("PieceAnchor").transform;
            anchor.SetParent(tileVisual, false);

            // 기존 프리팹도 바로 사용할 수 있게 화면상 모델 중심의 카메라 쪽에 초기 배치합니다.
            // Bounds 기반의 근사값이며 정확한 표면 위치는 프리팹에 PieceAnchor를 추가해 조절합니다.
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in tileVisual.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            if (hasBounds)
            {
                Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                Vector3 absoluteForward = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));
                float depth = Vector3.Dot(absoluteForward, bounds.extents);
                anchor.position = bounds.center - forward * (depth + Mathf.Max(0f, automaticPieceAnchorClearance));
            }
            else
                anchor.position = tileVisual.position + new Vector3(0f, 0.098f, -0.15f);

            return anchor;
        }

        private static GameObject GetTileVisualPrefab(MapDefinition definition, BoardTileId tileId)
        {
            switch (tileId)
            {
                case BoardTileId.Start: return definition.startTilePrefab;

                case BoardTileId.Outer01:
                case BoardTileId.Outer02:
                case BoardTileId.Outer03:
                case BoardTileId.Outer04:
                case BoardTileId.Outer05:
                case BoardTileId.Outer06:
                case BoardTileId.Outer07:
                case BoardTileId.Outer08:
                case BoardTileId.Outer09:
                case BoardTileId.Outer10:
                case BoardTileId.Outer11:
                case BoardTileId.Outer12:
                case BoardTileId.Outer13:
                case BoardTileId.Outer14:
                case BoardTileId.Outer15:
                case BoardTileId.Outer16:
                    return definition.outerTilePrefab;

                case BoardTileId.Inner01:
                case BoardTileId.Inner02:
                case BoardTileId.Inner03:
                case BoardTileId.Inner04:
                case BoardTileId.Inner05:
                case BoardTileId.Inner06:
                case BoardTileId.Inner07:
                case BoardTileId.Inner08:
                    return definition.innerTilePrefab;

                case BoardTileId.Corner01: return definition.corner01TilePrefab;
                case BoardTileId.Corner02: return definition.corner02TilePrefab;
                case BoardTileId.Corner03: return definition.corner03TilePrefab;
                case BoardTileId.Center: return definition.centerTilePrefab;
                default: return null;
            }
        }

        private void ClearLoadedMap()
        {
            if (boardRuntimeState != null)
                boardRuntimeState.ClearAll();

            tilePositions.Clear();
            specialTileSettings.Clear();
            pieceAnchors.Clear();
            CurrentMap = null;
            useSpecialTiles = false;

            if (loadedBackgroundInstance != null)
                Destroy(loadedBackgroundInstance);
            if (fallbackBoardVisual != null)
                fallbackBoardVisual.gameObject.SetActive(false);
            foreach (GameObject tileVisualInstance in loadedTileVisualInstances)
            {
                if (tileVisualInstance != null)
                    Destroy(tileVisualInstance);
            }

            loadedBackgroundInstance = null;
            loadedTileVisualInstances.Clear();
        }

        private static bool IsSpecialTileId(BoardTileId tileId)
        {
            return tileId == BoardTileId.Center ||
                   tileId == BoardTileId.Corner01 ||
                   tileId == BoardTileId.Corner02 ||
                   tileId == BoardTileId.Corner03;
        }

        private BoardTileRuntimeStateManager EnsureBoardRuntimeState()
        {
            if (boardRuntimeState == null)
                boardRuntimeState = GetComponent<BoardTileRuntimeStateManager>();
            if (boardRuntimeState == null && Application.isPlaying)
                boardRuntimeState = gameObject.AddComponent<BoardTileRuntimeStateManager>();

            return boardRuntimeState;
        }
    }
}
