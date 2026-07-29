using System;
using System.Collections.Generic;
using YutArena.Common;
//기연이랑 나랑 데이터 교환하는 스크립트
//2026-07-30 부로 데이터 교환 방식 수정 필요

namespace YutArena.Managers
{
    // 저(TurnManager)가 영서님 코드에 "이 말을 이만큼 이동시켜줘"라고 요청할 때 보내는 데이터
    [Serializable]
    public class BoardMoveRequest
    {
        public int pieceId;          // 이동시킬 말의 고유 id
        public YutResult yutResult;  // 어떤 윷 결과로 이동하는지 (BackDo면 후진)
        public int moveCount;        // 실제로 몇 칸 이동하는지
    }

    // 영서님 코드가 이동/잡기/업기/완주 처리를 다 끝낸 뒤, 저에게 돌려주는 결과 데이터
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

    // 저와 영서님 코드 사이의 경계선(인터페이스). 영서님이 실제 보드 스크립트를 만들 때
    // 이 인터페이스를 구현하시면, 제 TurnManager 코드는 전혀 수정 없이 바로 연결됩니다.
    public interface IBoardExecutor
    {
        void RequestMove(BoardMoveRequest request);
        event Action<BoardMoveResult> OnMoveResolved;
        bool CanMove(int pieceId);
        //속박과 기절같은 cc기 차이에 따른 이동 가능 세부화 수정 
    }
}