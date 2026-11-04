using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_009_Status : CharacterStatusBehaviour
{
    private bool partsAlreadyActive;

    // 일반 잡기(Capture/Kill)와 Retire 모두 퇴장 전에 부품으로 대체합니다.
    // 부품 만료는 이 경로를 거치지 않으므로 다시 부품을 생성하지 않습니다.
    internal override bool TryReplaceRetire(PlayerRuntimeData.PieceRuntimeData piece)
    {
        if (partsAlreadyActive || piece.State != PieceState.InBoard ||
            !IsPassiveReady || !ApplyPassiveEffect(target: piece))
            return false;
        partsAlreadyActive = true;
        TryStartPassiveCooldown();
        return true;
    }

    public override void OnPieceEnteredBoard()
    {
        partsAlreadyActive = false;
    }

    public override void OnPieceRetired()
    {
        partsAlreadyActive = false;
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
            // 코너/중앙의 모든 분기를 포함하며 진입 경로에 영향을 받지 않습니다.
            if (!CharacterBoardUtility.IsWithinDistance(origin, reference.Piece.CurrentTileId, 1))
                continue;

            int realPieceCount = CcEffectService.StackMembers(reference.Piece).Count;
            ApplyActiveEffect(target: reference.Piece);
            if (reference.Piece.State == PieceState.Waiting) retiredCount += realPieceCount;
        }

        return CharacterActiveResult.Success(
            $"Self Destruct retired {retiredCount} piece(s) on this tile and all connected adjacent tiles.",
            suppressExtraThrow: true);
    }

}
