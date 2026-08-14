using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_009_Status : CharacterStatusBehaviour
{
    private bool isParts;
    private int partsRemainingOwnerTurns;
    private BoardTileId partsTile;

    public override bool IsTargetable => !isParts;

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData piece) ||
            piece.State != PieceState.InBoard || !TryStartPassiveCooldown())
            return CharacterCaptureDecision.Proceed;

        isParts = true;
        partsRemainingOwnerTurns = 3;
        partsTile = piece.CurrentTileId;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_009_Status)} converted to parts at " +
            $"{partsTile}. Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterCaptureDecision.ConvertToParts;
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        if (!isParts) return;

        partsRemainingOwnerTurns--;
        if (partsRemainingOwnerTurns > 0) return;

        if (TryGetPiece(out PlayerRuntimeData.PieceRuntimeData piece))
            CharacterBoardUtility.Retire(
                new CharacterPieceReference(Owner, piece),
                false);
        ClearParts();
    }

    public override void OnAnyPieceMoveCompleted(CharacterMoveRecord record)
    {
        if (!isParts || record.PlayerId != PlayerId || record.PieceId == PieceId)
            return;
        if (!CharacterBoardUtility.IsWithinDistance(record.To, partsTile, 1))
            return;

        if (TryGetPiece(out PlayerRuntimeData.PieceRuntimeData piece))
        {
            piece.MoveTo(partsTile);
            piece.ClearCc();
        }
        ClearParts();
    }

    public override void OnPieceRetired()
    {
        ClearParts();
        ResetPassiveCooldown();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Self Destruct requires a piece on the board.");

        BoardTileId origin = caster.CurrentTileId;
        List<CharacterPieceReference> pieces = CharacterBoardUtility.GetPiecesOnBoard(Players);
        int retiredCount = 0;

        foreach (CharacterPieceReference reference in pieces)
        {
            bool isCaster = reference.Player.PlayerId == PlayerId &&
                            reference.Piece.PieceId == PieceId;
            if (!isCaster &&
                !CharacterBoardUtility.IsWithinDistance(origin, reference.Piece.CurrentTileId, 1))
                continue;

            CharacterBoardUtility.Retire(reference, false);
            retiredCount++;
        }

        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_009_Status)} activated and retired " +
            $"{retiredCount} piece(s). Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Self Destruct retired {retiredCount} piece(s), including allies and the caster.",
            suppressExtraThrow: true);
    }

    private void ClearParts()
    {
        isParts = false;
        partsRemainingOwnerTurns = 0;
        partsTile = BoardTileId.None;
    }
}
