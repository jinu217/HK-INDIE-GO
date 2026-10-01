using YutArena.InGame;

public sealed class CHAR_010_Status : CharacterStatusBehaviour
{
    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (request.IsActiveSkillMove)
            return base.ModifyMoveCount(request);

        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster) || !caster.IsStacked)
            return base.ModifyMoveCount(request);

        int groupSize = 0;
        foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
        {
            if (piece.StackGroupId == caster.StackGroupId) groupSize++;
        }

        int carriedPieceCount = groupSize > 0 ? groupSize - 1 : 0;
        if (carriedPieceCount == 0) return base.ModifyMoveCount(request);
        if (!TryStartPassiveCooldown()) return base.ModifyMoveCount(request);

        ApplyEffect(CcDefine.MoveBonus, value: carriedPieceCount);
        return base.ModifyMoveCount(request);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (Movement == null)
            return CharacterActiveResult.Failure("PieceMovementManager is not available.");
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Tactical Retreat requires a piece on the board.");
        if (!ApplyEffect(CcDefine.Move, value: -1))
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
