using UnityEngine;

namespace YutArena.InGame
{
    /// <summary>연결/발동 확인용 예제입니다. 실제 게임 효과는 별도의 이벤트 스크립트에 작성합니다.</summary>
    [CreateAssetMenu(menuName = "YutArena/Map Events/Log", fileName = "LogMapSpecialTileEvent")]
    public sealed class LogMapSpecialTileEvent : MapSpecialTileEvent
    {
        [SerializeField] private string message = "특수 타일 이벤트 발동";

        public override void Execute(MapSpecialTileContext context)
        {
            Debug.Log(
                $"[MapEvent] {message}: Map={context.Map.CurrentMap.name}, " +
                $"Tile={context.TileId}, Player={context.Player.PlayerId}, " +
                $"Piece={context.Piece.PieceId}, Count={context.MovingPieceCount}, Value={context.Value}",
                context.Map);
        }
    }
}
