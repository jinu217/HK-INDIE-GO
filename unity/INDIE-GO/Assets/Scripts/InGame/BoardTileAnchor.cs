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
    }
}
