using System;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;
namespace YutArena.Managers
{
    // ===================================================================
    // 팀원(영서) 작업이 아직 안 됐을 때, TurnManager 흐름을 혼자 테스트해보기 위한 가짜(Mock) 구현.
    // IBoardExecutor 인터페이스만 만족하면 되니까, 이 컴포넌트를 TurnManager의
    // boardExecutorSource 자리에 꽂아두면 실제 보드 없이도 던지기->이동->턴종료 흐름을 확인할 수 있다.
    // 나중에 진짜 BoardExecutor 스크립트가 완성되면 인스펙터에서 이 컴포넌트만 빼고 교체하면 끝.
    // ===================================================================
    public class MockBoardExecutor : MonoBehaviour, IBoardExecutor
    {
        [Tooltip("테스트용: 이동 요청이 오면 강제로 완주 처리할지")]
        public bool forceFinishOnMove = false;

        [Tooltip("테스트용: 이동 요청이 오면 강제로 잡기가 발생한 것으로 처리할지")]
        public bool forceCaptureOnMove = false;

        // IBoardExecutor 인터페이스가 요구하는 이벤트를 실제로 구현
        public event Action<BoardMoveResult> OnMoveResolved;

        // IBoardExecutor가 요구하는 함수. 실제 보드였다면 여기서 좌표 계산, 갈림길, 잡기/업기/완주
        // 판정을 전부 했겠지만, 이건 가짜라서 인스펙터에서 켜둔 값대로 그냥 결과를 만들어서 돌려줌
        public void RequestMove(BoardMoveRequest request)
        {
            var result = new BoardMoveResult
            {
                pieceId = request.pieceId,
                moveSucceeded = true,
                arrivedTile = BoardTileId.Outer01, // 테스트용 고정값 (실제로는 보드 쪽이 계산해서 넣어줌)
                isFinished = forceFinishOnMove,
                isStacked = false
            };

            if (forceCaptureOnMove)
                result.capturedPieceIds.Add(-1); // 테스트용 임의 id

            // 실제 보드였다면 이동 애니메이션 등이 끝난 뒤에 이 이벤트를 호출했겠지만,
            // 여기선 테스트 편의를 위해 바로 호출함
            OnMoveResolved?.Invoke(result);
        }

        public bool CanMove(int pieceId) => true; // 테스트용이라 항상 이동 가능하다고 답함
    }
}