using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_009_Status : CharacterStatusBehaviour
{
    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        var existing = base.EvaluateIncomingCapture(request);
        if (existing != CharacterCaptureDecision.Proceed) return existing;
        if (!TryGetPiece(out var piece) || piece.State != PieceState.InBoard ||
            !TryStartPassiveCooldown()) return CharacterCaptureDecision.Proceed;
        ApplyEffect(CcDefine.Parts, 3);
        return CharacterCaptureDecision.ConvertToParts;
    }

    public override void OnPieceRetired() { ResetPassiveCooldown(); }

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

}
