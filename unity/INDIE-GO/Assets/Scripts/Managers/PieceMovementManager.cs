using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.GameCore;
using YutArena.InGame;

/// <summary>
/// Unity adapter that validates a move command, applies the board rules, and
/// returns a complete result. Presentation code should react to the result
/// instead of re-reading and guessing changed runtime state.
/// </summary>
public sealed class PieceMovementManager : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;

    public event Action<PieceMoveResult> MoveResolved;

    /// <summary>Compatibility entry point for existing callers.</summary>
    public bool TryMovePiece(int playerId, int pieceId, int moveCount)
    {
        return TryMovePiece(playerId, pieceId, moveCount, out _);
    }

    public bool TryMovePiece(
        int playerId,
        int pieceId,
        int moveCount,
        out PieceMoveResult result,
        bool isActiveSkillMove = false)
    {
        return TryExecute(
            new PieceMoveCommand(playerId, pieceId, moveCount, isActiveSkillMove),
            out result);
    }

    public bool TryExecute(PieceMoveCommand command, out PieceMoveResult result)
    {
        if (!TryValidate(command, out PlayerController player,
                out PlayerRuntimeData.PieceRuntimeData selectedPiece, out string error))
        {
            result = PieceMoveResult.Failure(command, error);
            Debug.LogWarning(error, this);
            MoveResolved?.Invoke(result);
            return false;
        }

        bool isFirstBoardMove = selectedPiece.State == PieceState.Waiting;
        int appliedMoveCount = CharacterSkillRegistry.ModifyMoveCount(
            new CharacterMoveRequest(
                command.PlayerId,
                command.PieceId,
                command.MoveCount,
                isFirstBoardMove,
                command.IsActiveSkillMove));

        if (appliedMoveCount == 0)
        {
            result = PieceMoveResult.Failure(command, "A skill changed the move count to zero.");
            MoveResolved?.Invoke(result);
            return false;
        }

        result = PieceMoveResult.Success(command);
        result.AppliedMoveCount = appliedMoveCount;
        result.From = selectedPiece.CurrentTileId;

        List<PlayerRuntimeData.PieceRuntimeData> movingPieces = GetMovingPieces(player, selectedPiece);
        bool wasWaiting = selectedPiece.State == PieceState.Waiting;
        bool isBackward = appliedMoveCount < 0;
        int stepCount = Math.Abs(appliedMoveCount);

        for (int step = 0; step < stepCount; step++)
        {
            // Crossing the shared start/goal point completes the moving group.
            if (!isBackward && selectedPiece.State == PieceState.InBoard &&
                selectedPiece.CurrentTileId == BoardTileId.None)
            {
                SetGroupGoal(movingPieces);
                result.FinishedPieceCount = movingPieces.Count;
                break;
            }

            BoardTileId nextTile = isBackward
                ? BoardGraph.GetNextBackward(selectedPiece.CurrentTileId, selectedPiece.PreviousTileId)
                : BoardGraph.GetNextForward(
                    selectedPiece.CurrentTileId,
                    selectedPiece.PreviousTileId,
                    step == 0);

            foreach (PlayerRuntimeData.PieceRuntimeData movingPiece in movingPieces)
                movingPiece.MoveTo(nextTile);

            result.AddPath(nextTile);
        }

        result.To = selectedPiece.CurrentTileId;
        result.EnteredBoard = wasWaiting && selectedPiece.State == PieceState.InBoard;

        if (selectedPiece.State != PieceState.Goal)
        {
            ResolveCaptures(command, selectedPiece.CurrentTileId, movingPieces.Count, result);
            ResolveStacking(player, movingPieces, selectedPiece.CurrentTileId);
        }

        PublishLifecycle(result, movingPieces);
        MoveResolved?.Invoke(result);

        Debug.Log(
            $"Player {command.PlayerId}, Piece {command.PieceId + 1}: " +
            $"{result.From} -> {result.To} ({result.AppliedMoveCount} spaces)",
            this);
        return true;
    }

    /// <summary>
    /// Resolves a skill-originated capture through the same targetability and
    /// defensive-passive pipeline used by normal movement.
    /// </summary>
    public bool TryCapturePiece(
        int attackerPlayerId,
        int attackerPieceId,
        int targetPlayerId,
        int targetPieceId,
        bool wouldGrantExtraThrow,
        out PieceCaptureResult result)
    {
        result = default;
        if (playerManager == null ||
            !playerManager.TryGetPlayer(attackerPlayerId, out PlayerController attacker) ||
            !attacker.TryGetPieceData(attackerPieceId, out PlayerRuntimeData.PieceRuntimeData attackerPiece) ||
            !playerManager.TryGetPlayer(targetPlayerId, out PlayerController targetPlayer) ||
            !targetPlayer.TryGetPieceData(targetPieceId, out PlayerRuntimeData.PieceRuntimeData target) ||
            playerManager.AreAllies(attackerPlayerId, targetPlayerId) ||
            attackerPiece.State != PieceState.InBoard ||
            target.State != PieceState.InBoard ||
            !CharacterSkillRegistry.IsTargetable(targetPlayerId, targetPieceId))
        {
            return false;
        }

        var request = new CharacterCaptureRequest(
            attackerPlayerId,
            attackerPieceId,
            targetPlayerId,
            targetPieceId,
            1,
            wouldGrantExtraThrow);
        CharacterCaptureDecision decision = CharacterSkillRegistry.EvaluateIncomingCapture(request);
        CcDefine appliedCc = CcDefine.None;
        bool grantsBonus = false;

        if (decision == CharacterCaptureDecision.Proceed ||
            decision == CharacterCaptureDecision.LimitRetireToAttackingCount)
        {
            appliedCc = wouldGrantExtraThrow && decision == CharacterCaptureDecision.Proceed
                ? CcDefine.Kill
                : CcDefine.Retire;
            grantsBonus = appliedCc == CcDefine.Kill;
            target.SetCaptured(appliedCc);
            CharacterSkillRegistry.NotifyPieceRetired(targetPlayerId, targetPieceId);
            target.ClearCc();
        }

        if (appliedCc != CcDefine.None ||
            decision == CharacterCaptureDecision.ConsumeCloneWithoutBonus)
        {
            CharacterSkillRegistry.NotifyCaptureCompleted(request);
        }

        result = new PieceCaptureResult(
            targetPlayerId,
            targetPieceId,
            decision,
            appliedCc,
            grantsBonus);
        return appliedCc != CcDefine.None ||
               decision == CharacterCaptureDecision.ConsumeCloneWithoutBonus ||
               decision == CharacterCaptureDecision.ConvertToParts;
    }

    /// <summary>
    /// Removes a piece for effects that explicitly bypass capture defenses,
    /// such as self-destruction and path attacks.
    /// </summary>
    public bool TryForceRetire(int targetPlayerId, int targetPieceId)
    {
        if (playerManager == null ||
            !playerManager.TryGetPlayer(targetPlayerId, out PlayerController player) ||
            !player.TryGetPieceData(targetPieceId, out PlayerRuntimeData.PieceRuntimeData piece) ||
            piece.State != PieceState.InBoard)
        {
            return false;
        }

        piece.SetCaptured(CcDefine.Retire);
        CharacterSkillRegistry.NotifyPieceRetired(targetPlayerId, targetPieceId);
        piece.ClearCc();
        return true;
    }

    private bool TryValidate(
        PieceMoveCommand command,
        out PlayerController player,
        out PlayerRuntimeData.PieceRuntimeData piece,
        out string error)
    {
        player = null;
        piece = null;

        if (playerManager == null)
        {
            error = "PieceMovementManager requires a PlayerManager reference.";
            return false;
        }

        if (command.MoveCount == 0)
        {
            error = "Move count cannot be zero.";
            return false;
        }

        if (!playerManager.TryGetPlayer(command.PlayerId, out player) ||
            !player.TryGetPieceData(command.PieceId, out piece))
        {
            error = $"Could not find Player {command.PlayerId}, Piece {command.PieceId + 1}.";
            return false;
        }

        if (piece.CurrentCc == CcDefine.Stun)
        {
            error = $"Player {command.PlayerId}, Piece {command.PieceId + 1} is stunned.";
            return false;
        }

        if (piece.State == PieceState.Goal)
        {
            error = $"Player {command.PlayerId}, Piece {command.PieceId + 1} already reached the goal.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ResolveCaptures(
        PieceMoveCommand command,
        BoardTileId landingTile,
        int attackingPieceCount,
        PieceMoveResult result)
    {
        bool defaultBonus = result.AppliedMoveCount == -1 ||
                            (result.AppliedMoveCount >= 1 && result.AppliedMoveCount <= 3);
        var limitedRetireCountByGroup = new Dictionary<(int playerId, int groupId), int>();

        foreach (PlayerController otherPlayer in playerManager.ActivePlayers)
        {
            if (playerManager.AreAllies(otherPlayer.PlayerId, command.PlayerId)) continue;

            // Snapshot prevents a skill callback from invalidating enumeration.
            var targets = new List<PlayerRuntimeData.PieceRuntimeData>();
            foreach (PlayerRuntimeData.PieceRuntimeData candidate in otherPlayer.RuntimeData.Pieces)
            {
                if (candidate.State == PieceState.InBoard && candidate.CurrentTileId == landingTile)
                    targets.Add(candidate);
            }

            foreach (PlayerRuntimeData.PieceRuntimeData target in targets)
            {
                if (!CharacterSkillRegistry.IsTargetable(otherPlayer.PlayerId, target.PieceId))
                    continue;

                bool isBonusCandidate = defaultBonus &&
                    (!target.IsStacked || target.PieceId == target.StackLeaderPieceId);
                var request = new CharacterCaptureRequest(
                    command.PlayerId,
                    command.PieceId,
                    otherPlayer.PlayerId,
                    target.PieceId,
                    attackingPieceCount,
                    isBonusCandidate);
                CharacterCaptureDecision decision =
                    CharacterSkillRegistry.EvaluateIncomingCapture(request);

                CcDefine appliedCc = CcDefine.None;
                bool grantsBonus = false;

                if (decision == CharacterCaptureDecision.Proceed)
                {
                    appliedCc = isBonusCandidate ? CcDefine.Kill : CcDefine.Retire;
                    grantsBonus = appliedCc == CcDefine.Kill;
                    target.SetCaptured(appliedCc);
                }
                else if (decision == CharacterCaptureDecision.LimitRetireToAttackingCount)
                {
                    int groupId = target.IsStacked ? target.StackGroupId : -target.PieceId - 1;
                    var key = (otherPlayer.PlayerId, groupId);
                    limitedRetireCountByGroup.TryGetValue(key, out int retired);
                    if (retired < attackingPieceCount)
                    {
                        target.SetCaptured(CcDefine.Retire);
                        appliedCc = CcDefine.Retire;
                        limitedRetireCountByGroup[key] = retired + 1;
                    }
                }
                // Prevent, clone consumption, and parts conversion deliberately
                // leave the target runtime position unchanged.

                result.AddCapture(new PieceCaptureResult(
                    otherPlayer.PlayerId,
                    target.PieceId,
                    decision,
                    appliedCc,
                    grantsBonus));

                if (appliedCc == CcDefine.Kill || appliedCc == CcDefine.Retire)
                {
                    CharacterSkillRegistry.NotifyPieceRetired(otherPlayer.PlayerId, target.PieceId);
                    target.ClearCc();
                }

                if (appliedCc != CcDefine.None ||
                    decision == CharacterCaptureDecision.ConsumeCloneWithoutBonus)
                {
                    CharacterSkillRegistry.NotifyCaptureCompleted(request);
                }
            }
        }
    }

    private static void PublishLifecycle(
        PieceMoveResult result,
        List<PlayerRuntimeData.PieceRuntimeData> movingPieces)
    {
        if (result.EnteredBoard)
        {
            foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
                CharacterSkillRegistry.NotifyPieceEnteredBoard(result.PlayerId, piece.PieceId);
        }

        foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
        {
            CharacterSkillRegistry.NotifyMoveCompleted(new CharacterMoveRecord(
                result.PlayerId,
                piece.PieceId,
                result.From,
                result.To,
                result.Path));
        }
    }

    private static List<PlayerRuntimeData.PieceRuntimeData> GetMovingPieces(
        PlayerController player,
        PlayerRuntimeData.PieceRuntimeData selectedPiece)
    {
        var result = new List<PlayerRuntimeData.PieceRuntimeData>();
        if (!selectedPiece.IsStacked)
        {
            result.Add(selectedPiece);
            return result;
        }

        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
        {
            if (piece.StackGroupId == selectedPiece.StackGroupId) result.Add(piece);
        }
        return result;
    }

    private static void SetGroupGoal(List<PlayerRuntimeData.PieceRuntimeData> pieces)
    {
        foreach (PlayerRuntimeData.PieceRuntimeData piece in pieces) piece.SetGoal();
    }

    private static void ResolveStacking(
        PlayerController player,
        List<PlayerRuntimeData.PieceRuntimeData> movingPieces,
        BoardTileId landingTile)
    {
        var piecesOnTile = new List<PlayerRuntimeData.PieceRuntimeData>();
        PlayerRuntimeData.PieceRuntimeData stationary = null;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
        {
            if (piece.State != PieceState.InBoard || piece.CurrentTileId != landingTile) continue;
            piecesOnTile.Add(piece);
            if (stationary == null && !movingPieces.Contains(piece)) stationary = piece;
        }

        if (stationary == null) return;

        int groupId = stationary.IsStacked
            ? stationary.StackGroupId
            : player.RuntimeData.CreateStackGroupId();
        int leaderId = stationary.IsStacked
            ? stationary.StackLeaderPieceId
            : stationary.PieceId;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in piecesOnTile)
            piece.SetStackGroup(groupId, leaderId);
    }
}
