using System;
using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    [Serializable]
    public class MapSpecialTileSetting
    {
        [Tooltip("Center, Corner01~Corner03에 도착했을 때 발동합니다. Start와 겹치는 Corner04는 사용하지 않습니다.")]
        public BoardTileId tileId;
        [Tooltip("MapSpecialTileEvent를 상속한 스크립트로 만든 이벤트 에셋을 연결하세요. 비어 있으면 발동하지 않습니다.")]
        public MapSpecialTileEvent tileEvent;
        [Tooltip("이벤트 스크립트에 전달할 맵별 설정값입니다.")]
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
        [Header("Tile Visual Prefabs")]
        [Tooltip("타일 프리팹이 하나라도 지정되면 씬의 기본 말판은 숨깁니다. Start/Goal 타일의 화면용 프리팹입니다.")]
        public GameObject startTilePrefab;
        [Tooltip("Outer01~Outer16에 공통으로 생성할 화면용 프리팹입니다.")]
        public GameObject outerTilePrefab;
        [Tooltip("Inner01~Inner08에 공통으로 생성할 화면용 프리팹입니다.")]
        public GameObject innerTilePrefab;
        public GameObject corner01TilePrefab;
        public GameObject corner02TilePrefab;
        public GameObject corner03TilePrefab;
        public GameObject centerTilePrefab;

        public MapSpecialTileSetting[] specialTiles;

        public bool HasTileVisualPrefab =>
            startTilePrefab != null ||
            outerTilePrefab != null ||
            innerTilePrefab != null ||
            corner01TilePrefab != null ||
            corner02TilePrefab != null ||
            corner03TilePrefab != null ||
            centerTilePrefab != null;
    }
}
