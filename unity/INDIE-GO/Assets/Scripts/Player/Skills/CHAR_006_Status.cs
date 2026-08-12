using UnityEngine;
using YutArena.Common;
using YutArena.InGame;
using YutArena.GameCore;

public sealed class CHAR_006_Status : CharacterStatusBehaviour
{
    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (Random.value < 0.25f)
            return CharacterCaptureDecision.Prevent;

        return base.EvaluateIncomingCapture(request);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Sword Aura requires a piece on the board.");
        if (Movement == null)
            return CharacterActiveResult.Failure("PieceMovementManager is not available.");
        if (!request.HasTarget)
            return CharacterActiveResult.Failure("Sword Aura requires an enemy target.");
        if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out CharacterPieceReference target))
            return CharacterActiveResult.Failure("The selected target does not exist.");
        if (Players.AreAllies(target.Player.PlayerId, PlayerId) ||
            target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Sword Aura can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");

        BoardTileId forward = BoardGraph.GetNextForward(
            caster.CurrentTileId,
            caster.PreviousTileId,
            true);
        if (target.Piece.CurrentTileId != caster.CurrentTileId &&
            target.Piece.CurrentTileId != forward)
            return CharacterActiveResult.Failure("The enemy is not on the caster's tile or the next tile.");

        if (!Movement.TryCapturePiece(
                PlayerId,
                PieceId,
                target.Player.PlayerId,
                target.Piece.PieceId,
                true,
                out _))
            return CharacterActiveResult.Failure("The capture was prevented by the target.");

        return CharacterActiveResult.Success("The selected enemy was captured by Sword Aura.");
    }
}
