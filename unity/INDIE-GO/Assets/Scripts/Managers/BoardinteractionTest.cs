using System;
using System.Collections.Generic;
using YutArena.Common;
namespace YutArena.Managers
{
    [Serializable]
    public class BoardMoveRequest
    {
        public int pieceId;          // 이동시킬 말의 고유 id
        public YutResult yutResult;  // 어떤 윷 결과로 이동하는지 (BackDo면 후진)
        public int moveCount;        // 실제로 몇 칸 이동하는지
    }

    [Serializable]

    public class BoardMoveResult
    {
        public int pieceId;
        public bool moveSucceeded;
        public BoardTileId arrivedTile;                          // 도착한 칸
        public bool isFinished;                                   // 이번 이동으로 완주했는지
        public List<int> capturedPieceIds = new List<int>();      // 이번 이동으로 잡힌 상대 말 id들
        public bool isStacked;                                    // 이번 이동으로 업기가 발생했는지
        public List<int> stackedWithPieceIds = new List<int>();   // 업힌 대상 말 id들
    }

    public interface IBoardExecutor
    {
        void RequestMove(BoardMoveRequest request);
        event Action<BoardMoveResult> OnMoveResolved;
        bool CanMove(int pieceId);
    }
}