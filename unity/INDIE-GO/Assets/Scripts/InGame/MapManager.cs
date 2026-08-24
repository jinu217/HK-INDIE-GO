using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    /// <summary>
    /// Loads the visual board selected by GameStartSettings.mapType and exposes its tile anchors.
    /// It does not own movement or special-tile gameplay rules.
    /// </summary>
    public sealed class MapManager : MonoBehaviour
    {
        [SerializeField] private MapDefinition[] mapDefinitions;
        [SerializeField] private Transform backgroundRoot;
        [SerializeField] private Transform boardRoot;

        [Header("Common Board Layout")]
        [Tooltip("InGameScene Hierarchy에 배치한 공통 Start, Outer01 등의 타일 Anchor 루트입니다. 이 자식들의 위치를 수정하면 말 이동 위치에 반영됩니다.")]
        [SerializeField] private Transform commonBoardLayoutRoot;

        private readonly Dictionary<BoardTileId, Vector3> tilePositions = new Dictionary<BoardTileId, Vector3>();
        private readonly Dictionary<BoardTileId, Transform> commonTileAnchors = new Dictionary<BoardTileId, Transform>();
        private readonly Dictionary<BoardTileId, MapSpecialTileSetting> specialTileSettings = new Dictionary<BoardTileId, MapSpecialTileSetting>();
        private readonly List<GameObject> loadedTileVisualInstances = new List<GameObject>();
        private GameObject loadedBackgroundInstance;
        private GameObject loadedBoardInstance;
        private bool isCommonBoardLayoutCached;

        public MapDefinition CurrentMap { get; private set; }
        public bool IsMapLoaded => loadedBoardInstance != null || loadedTileVisualInstances.Count > 0;

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
            CacheSpecialTileSettings(definition);

            /*if (definition.boardPrefab != null)
            {
                loadedBoardInstance = boardRoot != null
                    ? Instantiate(definition.boardPrefab, boardRoot)
                    : Instantiate(definition.boardPrefab);
                loadedBoardInstance.name = definition.boardPrefab.name;
                loadedBoardInstance.transform.localPosition = Vector3.zero;
            }*/ 
            //(제거요망)

            CreateTileVisuals(definition);

            if (!IsMapLoaded)
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

        public bool TryGetSpecialTileSetting(BoardTileId tileId, out MapSpecialTileSetting setting)
        {
            return specialTileSettings.TryGetValue(tileId, out setting);
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
                        (definition.backgroundPrefab != null /*|| definition.HasBoardVisual*/))
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
            }
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
                case BoardTileId.Corner04: return definition.corner04TilePrefab;
                case BoardTileId.Center: return definition.centerTilePrefab;
                default: return null;
            }
        }

        private void ClearLoadedMap()
        {
            tilePositions.Clear();
            specialTileSettings.Clear();
            CurrentMap = null;

            if (loadedBackgroundInstance != null)
                Destroy(loadedBackgroundInstance);
            if (loadedBoardInstance != null)
                Destroy(loadedBoardInstance);
            foreach (GameObject tileVisualInstance in loadedTileVisualInstances)
            {
                if (tileVisualInstance != null)
                    Destroy(tileVisualInstance);
            }

            loadedBackgroundInstance = null;
            loadedBoardInstance = null;
            loadedTileVisualInstances.Clear();
        }

        private static bool IsSpecialTileId(BoardTileId tileId)
        {
            return tileId == BoardTileId.Center ||
                   tileId == BoardTileId.Corner01 ||
                   tileId == BoardTileId.Corner02 ||
                   tileId == BoardTileId.Corner03 ||
                   tileId == BoardTileId.Corner04;
        }
    }
}
