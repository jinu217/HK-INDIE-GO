using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_009_Status : CharacterStatusBehaviour
{
    private bool partsAlreadyActive;

    // CcEffectService calls this only for an actual Retire effect. A Kill does
    // not become Parts, and expired Parts cannot immediately recreate itself.
    internal bool TryEnterParts(PlayerRuntimeData.PieceRuntimeData piece)
    {
        if (partsAlreadyActive || piece.State != PieceState.InBoard ||
            !IsPassiveReady || !CcEffectService.Apply(piece, CcDefine.Parts, 3,
                sourcePlayerId: PlayerId, sourcePieceId: PieceId))
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
        var affectedTiles = new HashSet<BoardTileId>
        {
            origin,
            CharacterBoardUtility.GetNextForwardTile(origin, caster.PreviousTileId, true),
            CharacterBoardUtility.GetNextBackwardTile(origin, caster.PreviousTileId)
        };
        List<CharacterPieceReference> pieces = CharacterBoardUtility.GetPiecesOnBoard(Players);
        int retiredCount = 0;

        foreach (CharacterPieceReference reference in pieces)
        {
            if (!affectedTiles.Contains(reference.Piece.CurrentTileId))
                continue;

            CharacterBoardUtility.Retire(reference, false);
            if (reference.Piece.State == PieceState.Waiting) retiredCount++;
        }

        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_009_Status)} activated and retired " +
            $"{retiredCount} piece(s). Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Self Destruct retired {retiredCount} piece(s) on this tile and its adjacent route tiles.",
            suppressExtraThrow: true);
    }

}
