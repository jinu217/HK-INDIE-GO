using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    /// <summary>
    /// A visual position for one shared logical board tile inside a map prefab.
    /// </summary>
    public sealed class BoardTileAnchor : MonoBehaviour
    {
        [SerializeField] private BoardTileId tileId;

        public BoardTileId TileId => tileId;
        public Vector3 WorldPosition => transform.position;

        /// <summary>공통 보드 레이아웃을 런타임에 생성할 때 타일 ID를 지정합니다.</summary>
        public void Configure(BoardTileId value)
        {
            tileId = value;
        }
    }
}
