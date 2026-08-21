using System;
using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    public enum MapSpecialTileEffect
    {
        None,
        Custom
    }

    [Serializable]
    public class MapSpecialTileSetting
    {
        [Tooltip("Only Center and Corner01 through Corner04 are supported as map special tiles.")]
        public BoardTileId tileId;
        public MapSpecialTileEffect effect;
        public int value;
    }

    /// <summary>
    /// Inspector-authored data for one selectable map. The prefab contains its board, background,
    /// visual tile objects, and BoardTileAnchor components.
    /// </summary>
    [CreateAssetMenu(menuName = "YutArena/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinition : ScriptableObject
    {
        public MapType mapType;
        public GameObject mapPrefab;
        public MapSpecialTileSetting[] specialTiles;
    }
}
