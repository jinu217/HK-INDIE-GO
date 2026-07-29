using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
//BoradInteractionTest를 통해 데이터 교환 테스트
//내가 작성하는 스크립트
//2026-07-30 부로 데이터 교환 방식 수정 필요

namespace YutArena.Managers
{
    public class PlayerMove : MonoBehaviour, IBoardExecutor
    {
        public event Action<BoardMoveResult> OnMoveResolved;

        public PieceMover[] pieces;

        public void RequestMove(BoardMoveRequest request)
        {
            Debug.Log("===== RequestMove 수신 =====");
            Debug.Log($"Piece ID : {request.pieceId}");
            Debug.Log($"Yut Result : {request.yutResult}");
            Debug.Log($"Move Count : {request.moveCount}");

            //Target Piece Object Generate
            PieceMover targetPiece = pieces[request.pieceId];

            //Call Piece Move Function
            targetPiece.MovePiece(request.moveCount);

            // 테스트용 결과 생성
            BoardMoveResult result = new BoardMoveResult
            {
                pieceId = request.pieceId,
                moveSucceeded = true,
                arrivedTile = BoardTileId.Start,   // 테스트용 값
                isFinished = false,
                isStacked = false,
                capturedPieceIds = new List<int>(),
                stackedWithPieceIds = new List<int>()
            };

            Debug.Log("===== MoveResult 송신 =====");
            Debug.Log($"Piece ID : {result.pieceId}");
            Debug.Log($"Move Success : {result.moveSucceeded}");
            Debug.Log($"Arrived Tile : {result.arrivedTile}");
            Debug.Log($"Finished : {result.isFinished}");
            Debug.Log($"Captured Count : {result.capturedPieceIds.Count}");
            Debug.Log($"Stacked : {result.isStacked}");

            // TurnManager로 결과 전달
            OnMoveResolved?.Invoke(result);
        }

        public bool CanMove(int pieceId)
        {
            Debug.Log($"CanMove 호출 : Piece ID = {pieceId}");
            return true;
        }
    }
}