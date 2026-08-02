using UnityEngine;

/// <summary>
/// 런타임 말 데이터를 읽고, 이동 요청이 정상적으로 처리되는지 확인하는 개발용 도구.
/// Play Mode에서 컴포넌트 메뉴를 실행한다.
/// </summary>
public sealed class PlayerDataDebugTester : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PieceMovementManager pieceMovementManager;

    [Header("Move Test Input (positive: forward, negative: back-do)")]
    [SerializeField] private int testPlayerId = 1;
    [SerializeField] private int testPieceId;
    [SerializeField] private int testMoveCount = 1;

    [ContextMenu("Log All Piece Data")]
    public void LogAllPieceData()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("이 테스트는 Play Mode에서 실행해야 합니다.", this);
            return;
        }

        if (playerManager == null)
        {
            Debug.LogError("PlayerManager 참조를 Inspector에 연결하세요.", this);
            return;
        }

        if (playerManager.ActivePlayers.Count == 0)
        {
            Debug.LogError("활성 플레이어가 없습니다. SetupPlayers()가 먼저 호출되어야 합니다.", this);
            return;
        }

        Debug.Log($"===== Piece Data: {playerManager.ActivePlayers.Count} Players =====", this);

        foreach (PlayerController player in playerManager.ActivePlayers)
        {
            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            {
                Debug.Log(
                    $"[Player {player.PlayerId}] Piece {piece.PieceId + 1} | " +
                    $"Current: {piece.CurrentTileId} | " +
                    $"Previous: {piece.PreviousTileId} | " +
                    $"State: {piece.State} | " +
                    $"StackGroup: {piece.StackGroupId} | " +
                    $"StackLeader: {piece.StackLeaderPieceId} | " +
                    $"Finished: {piece.IsFinished} | " +
                    $"CC: {piece.CurrentCc} (remaining: {piece.RemainingCcTurns})",
                    player);
            }
        }
    }

    [ContextMenu("Run Configured Move Test")]
    public void RunConfiguredMoveTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("이 테스트는 Play Mode에서 실행해야 합니다.", this);
            return;
        }

        if (pieceMovementManager == null)
        {
            Debug.LogError("PieceMovementManager 참조를 Inspector에 연결하세요.", this);
            return;
        }

        pieceMovementManager.TryMovePiece(testPlayerId, testPieceId, testMoveCount);
    }
}
