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
        [SerializeField] private Transform mapRoot;

        private readonly Dictionary<BoardTileId, Vector3> tilePositions = new Dictionary<BoardTileId, Vector3>();
        private readonly Dictionary<BoardTileId, MapSpecialTileSetting> specialTileSettings = new Dictionary<BoardTileId, MapSpecialTileSetting>();
        private GameObject loadedMapInstance;

        public MapDefinition CurrentMap { get; private set; }
        public bool IsMapLoaded => CurrentMap != null;

        public bool LoadMap(GameStartSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("MapManager cannot load a null GameStartSettings.", this);
                return false;
            }

            MapDefinition definition = FindDefinition(settings.mapType);
            if (definition == null || definition.mapPrefab == null)
            {
                Debug.LogError($"MapManager: no map prefab is registered for {settings.mapType}.", this);
                return false;
            }

            ClearLoadedMap();

            Transform parent = mapRoot != null ? mapRoot : transform;
            loadedMapInstance = Instantiate(definition.mapPrefab, parent);
            loadedMapInstance.name = definition.mapPrefab.name;
            CurrentMap = definition;

            CacheTileAnchors();
            CacheSpecialTileSettings(definition);
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
                    if (definition != null && definition.mapType != MapType.Random && definition.mapPrefab != null)
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
            BoardTileAnchor[] anchors = loadedMapInstance.GetComponentsInChildren<BoardTileAnchor>(true);
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

            if (loadedMapInstance != null)
                Destroy(loadedMapInstance);

            loadedMapInstance = null;
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
