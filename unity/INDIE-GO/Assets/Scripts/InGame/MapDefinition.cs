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
    /// Inspector-authored data for one selectable map. All maps share one logical board layout;
    /// this asset only supplies map-specific visual prefabs and special-tile effects.
    /// </summary>
    [CreateAssetMenu(menuName = "YutArena/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinition : ScriptableObject
    {
        public MapType mapType;
        [Tooltip("움직이지 않는 맵 배경 프리팹입니다.")]
        public GameObject backgroundPrefab;
        [Tooltip("기존 전체 보드 외형 프리팹입니다. 타일별 프리팹을 지정하지 않은 기존 맵과의 호환을 위해 유지합니다.")]
        public GameObject boardPrefab;

        [Header("Tile Visual Prefabs")]
        [Tooltip("Start/Goal 타일의 화면용 프리팹입니다. 비어 있으면 논리 Anchor만 생성됩니다.")]
        public GameObject startTilePrefab;
        [Tooltip("Outer01~Outer16에 공통으로 생성할 화면용 프리팹입니다.")]
        public GameObject outerTilePrefab;
        [Tooltip("Inner01~Inner08에 공통으로 생성할 화면용 프리팹입니다.")]
        public GameObject innerTilePrefab;
        public GameObject corner01TilePrefab;
        public GameObject corner02TilePrefab;
        public GameObject corner03TilePrefab;
        public GameObject corner04TilePrefab;
        public GameObject centerTilePrefab;

        public MapSpecialTileSetting[] specialTiles;

        public bool HasTileVisualPrefab =>
            startTilePrefab != null ||
            outerTilePrefab != null ||
            innerTilePrefab != null ||
            corner01TilePrefab != null ||
            corner02TilePrefab != null ||
            corner03TilePrefab != null ||
            corner04TilePrefab != null ||
            centerTilePrefab != null;

        public bool HasBoardVisual => boardPrefab != null || HasTileVisualPrefab;
    }
}
