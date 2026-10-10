using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_005_Status : CharacterStatusBehaviour
{
    public override (YutResult, float)[] ModifyYutProbability((YutResult, float)[] currentTable)
    {
        if (IsPassiveReady && ApplyPassiveEffect()) TryStartPassiveCooldown();
        return base.ModifyYutProbability(currentTable);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard || !CcEffectService.CanMove(caster))
            return CharacterActiveResult.Failure("Issen requires a piece on the board.");

        List<BoardTileId> path = CharacterBoardUtility.GetForwardPath(caster, ActiveRange);
        for (int i = 0; i < path.Count; i++)
        {
            BoardTileId tile = path[i];
            if (tile != BoardTileId.None && tile != BoardTileId.Center &&
                tile != BoardTileId.Corner01 && tile != BoardTileId.Corner02 &&
                tile != BoardTileId.Corner03 && tile != BoardTileId.Corner04)
                continue;
            path.RemoveRange(i + 1, path.Count - i - 1);
            break;
        }
        if (path.Count == 0 || ActiveEffect(0) == null || ActiveEffect(1) == null)
            return CharacterActiveResult.Failure("Issen effect/path settings are missing.");
        int retiredCount = 0;
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile)
                {
                    ApplyActiveEffect(1, enemy.Piece);
                    if (enemy.Piece.State != PieceState.Waiting) continue;
                    retiredCount++;
                }
            }
        }

        if (!ApplyActiveEffect(path: path))
            return CharacterActiveResult.Failure("Issen movement could not be applied.");
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_005_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Moved {path.Count} spaces and retired {retiredCount} enemy piece(s) along the path.",
            suppressExtraThrow: true);
    }

}
