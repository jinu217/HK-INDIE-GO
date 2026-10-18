using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    /// <summary>도착한 말과 맵 정보를 이벤트 스크립트에 전달합니다.</summary>
    public readonly struct MapSpecialTileContext
    {
        public MapManager Map { get; }
        public PlayerController Player { get; }
        public PlayerRuntimeData.PieceRuntimeData Piece { get; }
        public BoardTileId TileId { get; }
        public int MovingPieceCount { get; }
        public int Value { get; }
        public PieceMovementManager Movement { get; }

        public MapSpecialTileContext(
            MapManager map,
            PlayerController player,
            PlayerRuntimeData.PieceRuntimeData piece,
            int movingPieceCount,
            int value,
            PieceMovementManager movement)
        {
            Map = map;
            Player = player;
            Piece = piece;
            TileId = piece.CurrentTileId;
            MovingPieceCount = movingPieceCount;
            Value = value;
            Movement = movement;
        }
    }

    /// <summary>
    /// 이 클래스를 상속하고 Execute에 효과를 작성한 뒤 CreateAssetMenu로 이벤트 에셋을 만드세요.
    /// MapDefinition의 Special Tiles > Tile Event에 그 에셋을 연결합니다.
    /// 여러 맵/말이 공유하므로 에셋 자체에 한 판의 가변 상태를 저장하지 마세요.
    /// </summary>
    public abstract class MapSpecialTileEvent : ScriptableObject
    {
        public abstract void Execute(MapSpecialTileContext context);
    }
}
