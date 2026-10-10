using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_006_Status : CharacterStatusBehaviour
{
    // 면역 ID는 CharacterData.passive_Immunities에서 읽습니다.

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Sword Aura requires a piece on the board.");

        if (ActiveEffect(0) == null || ActiveEffect(1) == null)
            return CharacterActiveResult.Failure("Sword Aura effect settings are missing.");
        var affectedTiles = new HashSet<BoardTileId>(CharacterBoardUtility.GetForwardPath(caster, ActiveRange))
            { caster.CurrentTileId };
        int retiredCount = 0;
        foreach (CharacterPieceReference enemy in
                 CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if (!affectedTiles.Contains(enemy.Piece.CurrentTileId)) continue;

            // Sword Aura is a retirement effect, so capture-only defenses and
            // capture bonus throws do not apply.
            ApplyActiveEffect(target: enemy.Piece);
            if (enemy.Piece.State == PieceState.Waiting)
                retiredCount++;
        }

        if (retiredCount > 0)
            ApplyActiveEffect(1, amount: retiredCount * ActiveEffect(1).amount);

        Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_006_Status)} retired {retiredCount} " +
            $"piece(s). Player={PlayerId}, Piece={PieceId}", this);
        return CharacterActiveResult.Success(
            $"Sword Aura retired {retiredCount} enemy piece(s) and granted the same number of throws.",
            suppressExtraThrow: true);
    }
}
