using UnityEngine;
using YutArena.Common;

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