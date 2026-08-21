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
    /// Inspector-authored data for one selectable map. Background and board are separate so the
    /// board can animate independently while its tile anchors keep tracking the board motion.
    /// </summary>
    [CreateAssetMenu(menuName = "YutArena/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinition : ScriptableObject
    {
        public MapType mapType;
        [Tooltip("움직이지 않는 맵 배경 프리팹입니다.")]
        public GameObject backgroundPrefab;
        [Tooltip("보드 메시와 BoardTileAnchor를 포함하는 프리팹입니다.")]
        public GameObject boardPrefab;
        public MapSpecialTileSetting[] specialTiles;
    }
}
