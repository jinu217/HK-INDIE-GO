using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_009_Status : CharacterStatusBehaviour
{
    private bool partsAlreadyActive;

    // CcEffectService calls this only for an actual Retire effect. A Kill does
    // not become Parts, and expired Parts cannot immediately recreate itself.
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

            ApplyActiveEffect(target: reference.Piece);
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
