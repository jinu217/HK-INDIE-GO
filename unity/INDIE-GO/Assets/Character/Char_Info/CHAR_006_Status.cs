using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_006_Status : CharacterStatusBehaviour
{
    // Capture and retirement still work; the existing harmful status effects do not.
    public override bool IsImmuneToEffect(CcDefine type) =>
        type == CcDefine.Stun || type == CcDefine.Binding || type == CcDefine.Silence;

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Sword Aura requires a piece on the board.");

        BoardTileId forward = CharacterBoardUtility.GetNextForwardTile(
            caster.CurrentTileId, caster.PreviousTileId, true);
        var affectedTiles = new HashSet<BoardTileId> { caster.CurrentTileId, forward };
        int retiredCount = 0;
        foreach (CharacterPieceReference enemy in
                 CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if (!affectedTiles.Contains(enemy.Piece.CurrentTileId)) continue;

            // Sword Aura is a retirement effect, so capture-only defenses and
            // capture bonus throws do not apply.
            CcBoardEffects.Retire(enemy, false);
            if (enemy.Piece.State == PieceState.Waiting)
                retiredCount++;
        }

        for (int i = 0; i < retiredCount; i++)
            Turns.GrantSkillExtraThrow();

        Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_006_Status)} retired {retiredCount} " +
            $"piece(s). Player={PlayerId}, Piece={PieceId}", this);
        return CharacterActiveResult.Success(
            $"Sword Aura retired {retiredCount} enemy piece(s) and granted the same number of throws.",
            suppressExtraThrow: true);
    }
}
