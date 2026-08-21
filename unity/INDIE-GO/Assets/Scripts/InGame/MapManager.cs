using System;
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

        private readonly Dictionary<BoardTileId, Vector3> tilePositions = new Dictionary<BoardTileId, Vector3>();
        private readonly Dictionary<BoardTileId, MapSpecialTileSetting> specialTileSettings = new Dictionary<BoardTileId, MapSpecialTileSetting>();
        private GameObject loadedBackgroundInstance;
        private GameObject loadedBoardInstance;

        public MapDefinition CurrentMap { get; private set; }
        public bool IsMapLoaded => loadedBoardInstance != null;

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

            // TEMP_ALLOW_BACKGROUND_WITHOUT_BOARD_START: 보드 프리팹 제작 전에도 배경과 원형 디버그 보드를 확인하기 위한 임시 처리입니다. 커밋 전 삭제하세요.
            if (definition.boardPrefab == null)
            {
                Debug.LogWarning($"MapManager: {settings.mapType}에 Board Prefab이 없어 배경만 로드하고 원형 디버그 보드를 사용합니다.", this);
                return false;
            }
            // TEMP_ALLOW_BACKGROUND_WITHOUT_BOARD_END

            loadedBoardInstance = boardRoot != null
                ? Instantiate(definition.boardPrefab, boardRoot)
                : Instantiate(definition.boardPrefab);
            loadedBoardInstance.name = definition.boardPrefab.name;
            loadedBoardInstance.transform.localPosition = Vector3.zero;

            CacheTileAnchors();
            return true;
        }

        public bool TryGetTilePosition(BoardTileId tileId, out Vector3 position)
        {
            return tilePositions.TryGetValue(tileId, out position);
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
                        (definition.backgroundPrefab != null || definition.boardPrefab != null))
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

        private void CacheTileAnchors()
        {
            tilePositions.Clear();
            BoardTileAnchor[] anchors = loadedBoardInstance.GetComponentsInChildren<BoardTileAnchor>(true);
            foreach (BoardTileAnchor anchor in anchors)
            {
                if (anchor.TileId == BoardTileId.None || anchor.TileId == BoardTileId.Goal)
                    continue;

                if (tilePositions.ContainsKey(anchor.TileId))
                {
                    Debug.LogWarning($"MapManager: duplicate anchor for {anchor.TileId} in {CurrentMap.name}.", anchor);
                    continue;
                }

                tilePositions.Add(anchor.TileId, anchor.WorldPosition);
            }
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

        private void ClearLoadedMap()
        {
            tilePositions.Clear();
            specialTileSettings.Clear();
            CurrentMap = null;

            if (loadedBackgroundInstance != null)
                Destroy(loadedBackgroundInstance);
            if (loadedBoardInstance != null)
                Destroy(loadedBoardInstance);

            loadedBackgroundInstance = null;
            loadedBoardInstance = null;
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
