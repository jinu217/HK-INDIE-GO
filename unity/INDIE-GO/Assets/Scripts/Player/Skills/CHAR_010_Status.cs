using YutArena.InGame;

public sealed class CHAR_010_Status : CharacterStatusBehaviour
{
    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster) || !caster.IsStacked)
            return request.MoveCount;

        int groupSize = 0;
        foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
        {
            if (piece.StackGroupId == caster.StackGroupId) groupSize++;
        }

        int carriedPieceCount = groupSize > 0 ? groupSize - 1 : 0;
        if (carriedPieceCount == 0) return request.MoveCount;

        return request.MoveCount < 0
            ? request.MoveCount - carriedPieceCount
            : request.MoveCount + carriedPieceCount;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (Movement == null)
            return CharacterActiveResult.Failure("PieceMovementManager is not available.");
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Tactical Retreat requires a piece on the board.");
        if (!Movement.TryMovePiece(PlayerId, PieceId, -1, out _, true))
            return CharacterActiveResult.Failure("The one-tile retreat could not be resolved.");

        return CharacterActiveResult.Success(
            "Moved one tile backward.",
            suppressExtraThrow: true);
    }
}
