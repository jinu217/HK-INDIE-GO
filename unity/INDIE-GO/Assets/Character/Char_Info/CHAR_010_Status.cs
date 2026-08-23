using YutArena.InGame;

public sealed class CHAR_010_Status : CharacterStatusBehaviour
{
    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (request.IsActiveSkillMove)
            return request.MoveCount;

        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster) || !caster.IsStacked)
            return request.MoveCount;

        int groupSize = 0;
        foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
        {
            if (piece.StackGroupId == caster.StackGroupId) groupSize++;
        }

        int carriedPieceCount = groupSize > 0 ? groupSize - 1 : 0;
        if (carriedPieceCount == 0) return request.MoveCount;
        if (!TryStartPassiveCooldown()) return request.MoveCount;

        int modifiedMoveCount = request.MoveCount < 0
            ? request.MoveCount - carriedPieceCount
            : request.MoveCount + carriedPieceCount;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_010_Status)} stack bonus: " +
            $"{request.MoveCount} -> {modifiedMoveCount}. Player={PlayerId}, Piece={PieceId}",
            this);
        return modifiedMoveCount;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (Movement == null)
            return CharacterActiveResult.Failure("PieceMovementManager is not available.");
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Tactical Retreat requires a piece on the board.");
        if (!Movement.TryMovePiece(PlayerId, PieceId, -1, true))
            return CharacterActiveResult.Failure("The one-tile retreat could not be resolved.");

        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_010_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            "Moved one tile backward.",
            suppressExtraThrow: true);
    }
}
