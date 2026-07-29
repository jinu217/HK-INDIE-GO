using UnityEngine;
using YutArena.Common;
//BoardInteractionTest 스크립트로 정보 교환 테스트 한 스크립트
//기연이가 호출하는 스크립트
//2026-07-30 부로 데이터 교환 방식 수정 필요

namespace YutArena.Managers
{
    public class DataTranslTest : MonoBehaviour
    {
        [SerializeField] private PlayerMove playerMove;

        private void Start()
        {
            // 결과 이벤트 등록
            playerMove.OnMoveResolved += OnMoveResolved;

            // 테스트용 요청 생성
            BoardMoveRequest request = new BoardMoveRequest
            {
                pieceId = 1,
                yutResult = YutResult.Do,
                moveCount = 1
            };

            Debug.Log("===== DataTranslTest -> PlayerMove =====");

            // PlayerMove에게 요청
            playerMove.RequestMove(request);
        }

        private void OnMoveResolved(BoardMoveResult result)
        {
            Debug.Log("===== PlayerMove -> DataTranslTest =====");
            Debug.Log($"Piece ID : {result.pieceId}");
            Debug.Log($"Move Success : {result.moveSucceeded}");
            Debug.Log($"Arrived Tile : {result.arrivedTile}");
            Debug.Log($"Finished : {result.isFinished}");
        }
    }
}