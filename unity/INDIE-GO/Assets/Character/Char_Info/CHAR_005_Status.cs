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
        int retiredCount = 0;
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile &&
                    CharacterSkillRegistry.IsTargetable(enemy.Player.PlayerId, enemy.Piece.PieceId) &&
                    CharacterBoardUtility.TryCapture(
                        PlayerId,
                        PieceId,
                        enemy,
                        1,
                        false,
                        out _))
                {
                    retiredCount++;
                }
            }
        }

        CharacterBoardUtility.MoveStackAlongPath(Owner, caster, path);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_005_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Moved three spaces and retired {retiredCount} enemy piece(s) along the path.",
            suppressExtraThrow: true);
    }

}
