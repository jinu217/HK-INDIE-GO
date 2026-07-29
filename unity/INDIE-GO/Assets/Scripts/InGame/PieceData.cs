using YutArena.Common;
using YutArena.InGame;
//각 말들의 정보를 담는 class
//CC정보, 말의 소유 Player, 업기 정보, 위치 정보 등을 정의


namespace YutArena.InGame
{
    public class PieceData
    {
        public int PieceId { get; private set; }

        public PlayerSlot Owner { get; private set; }

        public BoardTileId CurrentTile { get; set; }

        public bool IsFinished { get; set; }

        public CcDefine Status { get; private set; }

        public PieceData(int pieceId, PlayerSlot owner)
        {
            PieceId = pieceId;
            Owner = owner;

            CurrentTile = BoardTileId.Start;
            IsFinished = false;
            Status = CcDefine.None;
        }

        public bool HasStatus(CcDefine status)
        {
            return (Status & status) != 0;
        }

        public void AddStatus(CcDefine status)
        {
            Status |= status;
        }

        public void RemoveStatus(CcDefine status)
        {
            Status &= ~status;
        }

        public void ClearStatus()
        {
            Status = CcDefine.None;
        }
    }
}