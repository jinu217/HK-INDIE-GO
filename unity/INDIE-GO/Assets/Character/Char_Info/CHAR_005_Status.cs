using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_005_Status : CharacterStatusBehaviour
{
    public override (YutResult, float)[] ModifyYutProbability((YutResult, float)[] currentTable)
    {
        if (TryStartPassiveCooldown()) ApplyEffect(CcDefine.RemoveBackDo);
        return base.ModifyYutProbability(currentTable);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Issen requires a piece on the board.");

        List<BoardTileId> path = CharacterBoardUtility.GetForwardPath(caster, 3);
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
        int retiredCount = 0;
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile)
                {
                    CcBoardEffects.Retire(enemy, false);
                    if (enemy.Piece.State != PieceState.Waiting) continue;
                    retiredCount++;
                }
            }
        }

        CharacterBoardUtility.MoveStackAlongPath(Owner, caster, path, isSimpleMove: false);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_005_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Moved three spaces and retired {retiredCount} enemy piece(s) along the path.",
            suppressExtraThrow: true);
    }

}
