using UnityEngine;

/// <summary>
/// 말 이동 요청을 받아 해당 말의 런타임 위치 데이터를 갱신한다.
/// 현재는 임시 규칙으로 BoardPosition에 moveCount를 더한다.
/// 이후 보드 경로, 지름길, 골인, 업기/잡기 판정을 이 클래스에 확장한다.
/// </summary>
public sealed class PieceMovementManager : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;

    /// <summary>
    /// 기연(TurnManager)이 호출하는 말 이동 진입점.
    /// 예: TryMovePiece(1, 1, 3)은 Player 1의 2번 말을 3칸 이동한다.
    /// </summary>
    public bool TryMovePiece(int playerId, int pieceId, int moveCount)
    {
        if (playerManager == null)
        {
            Debug.LogError("PieceMovementManager에 PlayerManager 참조를 연결하세요.", this);
            return false;
        }

        if (moveCount == 0)
        {
            Debug.LogWarning("이동 칸 수가 0이므로 이동하지 않습니다.", this);
            return false;
        }

        if (!playerManager.TryGetPlayer(playerId, out PlayerController player))
        {
            Debug.LogError($"Player {playerId}을(를) 찾지 못했습니다.", this);
            return false;
        }

        if (!player.TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData piece))
        {
            Debug.LogError($"Player {playerId}의 {pieceId + 1}번 말을 찾지 못했습니다.", this);
            return false;
        }

        int previousPosition = piece.BoardPosition;
        int nextPosition = previousPosition + moveCount;
        piece.SetBoardPosition(nextPosition);

        Debug.Log(
            $"Player {playerId}의 {pieceId + 1}번 말: " +
            $"{previousPosition} -> {nextPosition} ({moveCount}칸 이동)",
            this);

        return true;
    }
}
