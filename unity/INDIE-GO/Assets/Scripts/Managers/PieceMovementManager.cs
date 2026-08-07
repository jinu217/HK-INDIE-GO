using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

/// <summary>
/// Resolves piece movement, stacking, and captures. Positive move counts move
/// forward; negative values are back-do moves.
/// </summary>
public sealed class PieceMovementManager : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;

    public bool TryMovePiece(int playerId, int pieceId, int moveCount)
    {
        if (playerManager == null)
        {
            Debug.LogError("PieceMovementManager requires a PlayerManager reference.", this);
            return false;
        }

        if (moveCount == 0)
        {
            //나중에 낙 처리(movecount를 0으로 주면 낙으로 처리 예정)
            Debug.LogWarning("Move count cannot be zero.", this);
            return false;
        }

        if (!playerManager.TryGetPlayer(playerId, out PlayerController player) ||
            !player.TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData selectedPiece))
        {
            Debug.LogError($"Could not find Player {playerId}, Piece {pieceId + 1}.", this);
            return false;
        }

        List<PlayerRuntimeData.PieceRuntimeData> movingPieces = GetMovingPieces(player, selectedPiece);
        BoardTileId startingTile = selectedPiece.CurrentTileId;
        bool isBackward = moveCount < 0;
        int stepCount = Mathf.Abs(moveCount);

        for (int step = 0; step < stepCount; step++)
        {
            // A piece that has stopped on the shared start tile goals only when
            // it attempts to move past that tile.
            if (!isBackward && selectedPiece.State == PieceState.InBoard &&
                selectedPiece.CurrentTileId == BoardTileId.None)
            {
                SetGroupGoal(movingPieces);
                break;
            }

            BoardTileId nextTile = isBackward
                ? GetNextBackwardTile(selectedPiece)
                : GetNextForwardTile(selectedPiece, step == 0);

            foreach (PlayerRuntimeData.PieceRuntimeData movingPiece in movingPieces)
                movingPiece.MoveTo(nextTile);
        }

        // Goal pieces have been removed from the board and cannot interact.
        if (selectedPiece.State == PieceState.Goal)
        {
            Debug.Log($"Player {playerId}, Piece {pieceId + 1} reached Goal.", this);
            return true;
        }

        ResolveCaptures(playerId, selectedPiece.CurrentTileId, moveCount);
        ResolveStacking(player, movingPieces, selectedPiece.CurrentTileId);

        Debug.Log(
            $"Player {playerId}, Piece {pieceId + 1}: {startingTile} -> " +
            $"{selectedPiece.CurrentTileId} ({moveCount} spaces)",
            this);
        return true;
    }

    private static List<PlayerRuntimeData.PieceRuntimeData> GetMovingPieces(
        PlayerController player,
        PlayerRuntimeData.PieceRuntimeData selectedPiece)
    {
        var movingPieces = new List<PlayerRuntimeData.PieceRuntimeData>();

        if (!selectedPiece.IsStacked)
        {
            movingPieces.Add(selectedPiece);
            return movingPieces;
        }

        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
        {
            if (piece.StackGroupId == selectedPiece.StackGroupId)
                movingPieces.Add(piece);
        }

        return movingPieces;
    }

    private static void SetGroupGoal(List<PlayerRuntimeData.PieceRuntimeData> movingPieces)
    {
        foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
            piece.SetGoal();
    }

    private void ResolveCaptures(int movingPlayerId, BoardTileId landingTile, int moveCount)
    {
        foreach (PlayerController otherPlayer in playerManager.ActivePlayers)
        {
            if (otherPlayer.PlayerId == movingPlayerId)
                continue;

            foreach (PlayerRuntimeData.PieceRuntimeData targetPiece in otherPlayer.RuntimeData.Pieces)
            {
                if (targetPiece.State != PieceState.InBoard || targetPiece.CurrentTileId != landingTile)
                    continue;

                CcDefine captureCc = GetCaptureCc(targetPiece, moveCount);
                targetPiece.SetCaptured(captureCc);
            }
        }
    }

    private static CcDefine GetCaptureCc(PlayerRuntimeData.PieceRuntimeData targetPiece, int moveCount)
    {
        bool grantsExtraThrow = moveCount == -1 || (moveCount >= 1 && moveCount <= 3);

        // A stacked group grants only one extra throw: its carrier is Kill,
        // and all carried pieces are Retire.
        if (grantsExtraThrow && (!targetPiece.IsStacked ||
                                 targetPiece.PieceId == targetPiece.StackLeaderPieceId))
        {
            return CcDefine.Kill;
        }

        return CcDefine.Retire;
    }

    private static void ResolveStacking(
        PlayerController player,
        List<PlayerRuntimeData.PieceRuntimeData> movingPieces,
        BoardTileId landingTile)
    {
        var piecesOnTile = new List<PlayerRuntimeData.PieceRuntimeData>();
        PlayerRuntimeData.PieceRuntimeData stationaryPiece = null;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
        {
            if (piece.State != PieceState.InBoard || piece.CurrentTileId != landingTile)
                continue;

            piecesOnTile.Add(piece);

            if (stationaryPiece == null && !movingPieces.Contains(piece))
                stationaryPiece = piece;
        }

        // No friendly piece was already on the tile, so an existing stack
        // remains unchanged and a lone piece stays unstacked.
        if (stationaryPiece == null)
            return;

        int stackGroupId = stationaryPiece.IsStacked
            ? stationaryPiece.StackGroupId
            : player.RuntimeData.CreateStackGroupId();
        int stackLeaderPieceId = stationaryPiece.IsStacked
            ? stationaryPiece.StackLeaderPieceId
            : stationaryPiece.PieceId;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in piecesOnTile)
            piece.SetStackGroup(stackGroupId, stackLeaderPieceId);
    }

    private static BoardTileId GetNextForwardTile(
        PlayerRuntimeData.PieceRuntimeData piece,
        bool isStartingThisMove)
    {
        BoardTileId current = piece.CurrentTileId;

        if (current == BoardTileId.None)
            return BoardTileId.Outer01;

        if (isStartingThisMove && current == BoardTileId.Corner01)
            return BoardTileId.Inner01;
        if (isStartingThisMove && current == BoardTileId.Corner02)
            return BoardTileId.Inner05;
        if (isStartingThisMove && current == BoardTileId.Center)
            return BoardTileId.Inner07;

        switch (current)
        {
            case BoardTileId.Outer01: return BoardTileId.Outer02;
            case BoardTileId.Outer02: return BoardTileId.Outer03;
            case BoardTileId.Outer03: return BoardTileId.Outer04;
            case BoardTileId.Outer04: return BoardTileId.Corner01;
            case BoardTileId.Corner01: return BoardTileId.Outer05;
            case BoardTileId.Outer05: return BoardTileId.Outer06;
            case BoardTileId.Outer06: return BoardTileId.Outer07;
            case BoardTileId.Outer07: return BoardTileId.Outer08;
            case BoardTileId.Outer08: return BoardTileId.Corner02;
            case BoardTileId.Corner02: return BoardTileId.Outer09;
            case BoardTileId.Outer09: return BoardTileId.Outer10;
            case BoardTileId.Outer10: return BoardTileId.Outer11;
            case BoardTileId.Outer11: return BoardTileId.Outer12;
            case BoardTileId.Outer12: return BoardTileId.Corner03;
            case BoardTileId.Corner03: return BoardTileId.Outer13;
            case BoardTileId.Outer13: return BoardTileId.Outer14;
            case BoardTileId.Outer14: return BoardTileId.Outer15;
            case BoardTileId.Outer15: return BoardTileId.Outer16;
            case BoardTileId.Outer16: return BoardTileId.None;
            case BoardTileId.Inner01: return BoardTileId.Inner02;
            case BoardTileId.Inner02: return BoardTileId.Center;
            case BoardTileId.Inner03: return BoardTileId.Inner04;
            case BoardTileId.Inner04: return BoardTileId.Corner03;
            case BoardTileId.Inner05: return BoardTileId.Inner06;
            case BoardTileId.Inner06: return BoardTileId.Center;
            case BoardTileId.Inner07: return BoardTileId.Inner08;
            case BoardTileId.Inner08: return BoardTileId.None;
            case BoardTileId.Center:
                return piece.PreviousTileId == BoardTileId.Inner02
                    ? BoardTileId.Inner03
                    : BoardTileId.Inner07;
            default:
                Debug.LogError($"Undefined forward tile: {current}");
                return BoardTileId.None;
        }
    }

    private static BoardTileId GetNextBackwardTile(PlayerRuntimeData.PieceRuntimeData piece)
    {
        switch (piece.CurrentTileId)
        {
            case BoardTileId.None:
                if (piece.PreviousTileId == BoardTileId.Outer01)
                    return BoardTileId.Outer16;
                if (piece.PreviousTileId == BoardTileId.Inner08)
                    return BoardTileId.Inner08;
                if (piece.PreviousTileId == BoardTileId.Outer16)
                    return BoardTileId.Outer16;
                return BoardTileId.None;

            case BoardTileId.Outer01: return BoardTileId.None;
            case BoardTileId.Outer02: return BoardTileId.Outer01;
            case BoardTileId.Outer03: return BoardTileId.Outer02;
            case BoardTileId.Outer04: return BoardTileId.Outer03;
            case BoardTileId.Corner01: return BoardTileId.Outer04;
            case BoardTileId.Outer05: return BoardTileId.Corner01;
            case BoardTileId.Outer06: return BoardTileId.Outer05;
            case BoardTileId.Outer07: return BoardTileId.Outer06;
            case BoardTileId.Outer08: return BoardTileId.Outer07;
            case BoardTileId.Corner02: return BoardTileId.Outer08;
            case BoardTileId.Outer09: return BoardTileId.Corner02;
            case BoardTileId.Outer10: return BoardTileId.Outer09;
            case BoardTileId.Outer11: return BoardTileId.Outer10;
            case BoardTileId.Outer12: return BoardTileId.Outer11;
            case BoardTileId.Corner03:
                return piece.PreviousTileId == BoardTileId.Inner04
                    ? BoardTileId.Inner04
                    : BoardTileId.Outer12;
            case BoardTileId.Outer13: return BoardTileId.Corner03;
            case BoardTileId.Outer14: return BoardTileId.Outer13;
            case BoardTileId.Outer15: return BoardTileId.Outer14;
            case BoardTileId.Outer16: return BoardTileId.Outer15;
            case BoardTileId.Inner01: return BoardTileId.Corner01;
            case BoardTileId.Inner02: return BoardTileId.Inner01;
            case BoardTileId.Inner03: return BoardTileId.Center;
            case BoardTileId.Inner04: return BoardTileId.Inner03;
            case BoardTileId.Inner05: return BoardTileId.Corner02;
            case BoardTileId.Inner06: return BoardTileId.Inner05;
            case BoardTileId.Inner07: return BoardTileId.Center;
            case BoardTileId.Inner08: return BoardTileId.Inner07;
            case BoardTileId.Center:
                return piece.PreviousTileId == BoardTileId.Inner03 ||
                       piece.PreviousTileId == BoardTileId.Inner02
                    ? BoardTileId.Inner02
                    : BoardTileId.Inner06;
            default:
                Debug.LogError($"Undefined back-do tile: {piece.CurrentTileId}");
                return BoardTileId.None;
        }
    }
}